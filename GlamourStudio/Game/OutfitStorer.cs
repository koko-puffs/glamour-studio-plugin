using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>One set to gather: its item, its loose pieces in the dresser, and the set's own place when it's stored already (-1: a new set).</summary>
public sealed record OutfitJob(uint SetItem, IReadOnlyList<DresserPiece> Pieces, int StoredAt);

/// <summary>
/// Making room for you: gathers outfit sets' loose pieces in the glamour dresser into their sets, as
/// doing it by hand does. The game stores an outfit only from your bags or armoury chest (the outfit
/// window lists nothing from the dresser), so for each set: each loose piece is restored to your bags
/// (<c>MirageManager.RestorePrismBoxItem</c>, the dresser's "Restore item to its original form"), and
/// once they're there they go back in as the set (<c>StoreNewOutfit</c>, or <c>StoreExistingOutfit</c>
/// into the set stored already: each piece at its set column, the rest <c>Invalid</c>), spending a
/// glamour prism each, as the game's own storing does.
/// <para>
/// Before a set's first piece leaves the dresser, every piece is found (its place now), the bags are
/// checked for room, and a Unique piece you already hold is left. Each step is waited for: a restore
/// until the dresser has one copy fewer and a new bag place holds it; a store until those places are
/// empty. When a piece doesn't come, the ones that did still go in as the set (when enough for one),
/// and it stops, saying what's left in your bags. Stop waits for a step the game is answering. With
/// the glamour dresser open. Framework thread. Not yet watched in the game.
/// </para>
/// </summary>
public static unsafe class OutfitStorer
{
    private enum Phase { Idle, Restoring, WaitRestored, Storing, WaitStored, Between }

    private static Phase phase;
    private static readonly Queue<OutfitJob> jobs = new();
    private static OutfitJob? job;
    private static List<DresserPiece> pieces = new();
    private static GameData? data;
    private static int pieceAt;
    private static DateTime deadline;
    private static HashSet<(InventoryType Type, int Slot)> before = new();
    private static int dresserBefore;

    /// <summary>How many times the piece at work was asked for and refused.</summary>
    private static int tries;

    /// <summary>The pieces of the set at work that are in your bags now (and where).</summary>
    private static readonly List<(DresserPiece Piece, InventoryType Type, int Slot)> restored = new();

    /// <summary>The pieces handed to the game to store (what the wait watches).</summary>
    private static readonly List<(DresserPiece Piece, InventoryType Type, int Slot)> storing = new();

    private static Action<string, bool>? report;
    private static bool stopAsked;

    /// <summary>Why it stops once the set at work is stored (a piece that didn't come), or null.</summary>
    private static string? stopAfterStore;

    /// <summary>Whether it's at work.</summary>
    public static bool Busy => phase != Phase.Idle;

    /// <summary>Sets done and places freed so far (this run).</summary>
    public static int SetsDone { get; private set; }

    public static int PlacesFreed { get; private set; }

    /// <summary>Sets left to do (the one at work included).</summary>
    public static int SetsLeft => jobs.Count + (job != null ? 1 : 0);

    /// <summary>Starts gathering <paramref name="todo"/>; <paramref name="onEnd"/> hears how it ended. False when it's busy already.</summary>
    public static bool Start(IEnumerable<OutfitJob> todo, GameData gameData, Action<string, bool> onEnd)
    {
        if (Busy || DresserTaker.Busy) return false;
        data = gameData;
        report = onEnd;
        jobs.Clear();
        foreach (var j in todo) jobs.Enqueue(j);
        SetsDone = PlacesFreed = 0;
        stopAsked = false;
        stopAfterStore = null;
        Recorder.Log($"OUTFITS start: {jobs.Count} sets, {jobs.Sum(j => j.Pieces.Count)} pieces");
        Next();
        return true;
    }

    /// <summary>Stops once no step is waiting on the game (at once, when <paramref name="now"/>: the plugin's closing).</summary>
    public static void Stop(string why = "Stopped", bool now = false)
    {
        if (!Busy) return;
        if (now || phase is Phase.Restoring or Phase.Between) Finish(why, false);
        else
        {
            stopAsked = true;
            Recorder.Log($"OUTFITS stop asked: {why}");
        }
    }

    /// <summary>How many glamour prisms you have (in your bags), and how many free bag places.</summary>
    public static (int Prisms, int FreeBag) Supplies(GameData gameData)
    {
        try
        {
            var mgr = InventoryManager.Instance();
            if (mgr == null) return (0, 0);
            var prisms = gameData.GlamourPrisms().Sum(id => mgr->GetInventoryItemCount(id) + mgr->GetInventoryItemCount(id, true));
            return (prisms, (int)mgr->GetEmptySlotsInBag());
        }
        catch
        {
            return (0, 0);
        }
    }

    public static void Tick()
    {
        if (phase == Phase.Idle) return;
        var waiting = phase is Phase.WaitRestored or Phase.WaitStored;
        if (!GameWindows.IsShown("MiragePrismPrismBox") && !waiting)
        {
            Finish("The glamour dresser closed", false);
            return;
        }
        if (Services.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] && !waiting)
        {
            Finish("You're in combat", false);
            return;
        }
        var mgr = MirageManager.Instance();
        if (mgr == null || !mgr->PrismBoxLoaded) return;
        var now = DateTime.UtcNow;
        switch (phase)
        {
            case Phase.Restoring:
            {
                if (now < deadline) break;
                if (stopAsked)
                {
                    StoreWhatCameOrFinish("Stopped");
                    break;
                }
                var piece = pieces[pieceAt];
                var id = LookJson.BaseId(piece.Item);
                // Where it is now: the place read before, else wherever a loose undyed copy is.
                var at = DresserPlaces.Find(mgr, id, 0, 0, piece.Index);
                if (at < 0)
                {
                    StoreWhatCameOrFinish($"{data!.ItemName(piece.Item)} isn't in the dresser any more");
                    break;
                }
                before = DresserPlaces.InBags(id);
                dresserBefore = DresserPlaces.Count(mgr, id);
                var ok = mgr->RestorePrismBoxItem((uint)at);
                Recorder.Log($"  RestorePrismBoxItem {at} (read at {piece.Index}, {piece.Item}) → {ok}, dresser had {dresserBefore}{(tries > 0 ? $", try {tries + 1}" : string.Empty)}");
                if (!ok)
                {
                    // The game says no now and then with room to spare (a recording: the fifth piece,
                    // half a second after the fourth): asked again a little later, three times.
                    if (++tries < 4)
                    {
                        deadline = now + TimeSpan.FromSeconds(1.5 * tries);
                        break;
                    }
                    StoreWhatCameOrFinish($"The game wouldn't restore {data!.ItemName(piece.Item)}");
                    break;
                }
                tries = 0;
                phase = Phase.WaitRestored;
                deadline = now + TimeSpan.FromSeconds(6);
                break;
            }
            case Phase.WaitRestored:
            {
                var piece = pieces[pieceAt];
                var id = LookJson.BaseId(piece.Item);
                var fresh = DresserPlaces.InBags(id).Except(before).ToList();
                // Arrived: the dresser has one copy fewer and a bag place holds it that didn't before
                // (a place set apart by a list, never compared with default: bag 1's first place is that).
                if (fresh.Count >= 1 && DresserPlaces.Count(mgr, id) < dresserBefore)
                {
                    restored.Add((piece, fresh[0].Type, fresh[0].Slot));
                    Recorder.Log($"  restored {id} to {fresh[0].Type}:{fresh[0].Slot}{(fresh.Count > 1 ? $" ({fresh.Count} new places held it: the first taken)" : string.Empty)}");
                    pieceAt++;
                    phase = pieceAt < pieces.Count ? Phase.Restoring : Phase.Storing;
                    deadline = now + TimeSpan.FromMilliseconds(900);
                    break;
                }
                if (now > deadline) StoreWhatCameOrFinish($"{data!.ItemName(piece.Item)} didn't come to your bags (it may come yet: check them)");
                break;
            }
            case Phase.Storing:
            {
                if (now < deadline) break;
                Store(mgr, now);
                break;
            }
            case Phase.WaitStored:
            {
                if (storing.All(r => DresserPlaces.SlotItem(r.Type, r.Slot) != LookJson.BaseId(r.Piece.Item)))
                {
                    var freed = job!.StoredAt >= 0 ? storing.Count : Math.Max(0, storing.Count - 1);
                    SetsDone++;
                    PlacesFreed += freed;
                    Recorder.Log($"  stored {job.SetItem}: {storing.Count} pieces, {freed} places freed");
                    restored.RemoveAll(r => storing.Any(s => s.Piece.Index == r.Piece.Index && s.Type == r.Type && s.Slot == r.Slot));
                    storing.Clear();
                    if (stopAfterStore is { } why)
                    {
                        Finish(why, false);
                        break;
                    }
                    if (restored.Count > 0)
                    {
                        Finish("Some pieces couldn't be placed in their set", false);
                        break;
                    }
                    if (stopAsked)
                    {
                        Finish("Stopped", false);
                        break;
                    }
                    job = null;
                    phase = Phase.Between;
                    // (A set's first piece asked for a second after the last set was stored was
                    // refused every time in a recording, and taken 1.5 s later: two seconds between sets.)
                    deadline = now + TimeSpan.FromSeconds(2);
                    break;
                }
                if (now > deadline)
                {
                    // The ask went: whether the game took it isn't known here.
                    var names = Words.List(storing.Select(r => data!.ItemName(r.Piece.Item)));
                    storing.Clear();
                    restored.Clear();
                    Finish($"{data!.ItemName(job!.SetItem)}: the game hasn't answered. {names} may have gone into the set: check the dresser and your bags", false);
                }
                break;
            }
            case Phase.Between:
                if (stopAsked)
                {
                    Finish("Stopped", false);
                    break;
                }
                if (now >= deadline) Next();
                break;
        }
    }

    /// <summary>The set's pieces that came go in as the set when there are enough for one, then it stops; else it stops now.</summary>
    private static void StoreWhatCameOrFinish(string why)
    {
        var enough = job != null && restored.Count >= (job.StoredAt >= 0 ? 1 : 2);
        if (!enough)
        {
            Finish(why, false);
            return;
        }
        Recorder.Log($"  {why}: storing the {restored.Count} that came, then stopping");
        stopAfterStore = why;
        phase = Phase.Storing;
        deadline = DateTime.MinValue;
    }

    private static void Store(MirageManager* mgr, DateTime now)
    {
        var containers = stackalloc InventoryType[11];
        var slots = stackalloc ushort[11];
        for (var i = 0; i < 11; i++)
        {
            containers[i] = InventoryType.Invalid;
            slots[i] = 0;
        }
        var columns = data!.SetPieces(job!.SetItem);
        storing.Clear();
        for (var k = 0; k < restored.Count; k++)
        {
            var (piece, type, slot) = restored[k];
            var id = LookJson.BaseId(piece.Item);
            // Still where it came (you may have moved it meanwhile): else wherever it is in your bags now.
            if (DresserPlaces.SlotItem(type, slot) != id)
            {
                var others = DresserPlaces.InBags(id).Where(p => storing.All(s => s.Type != p.Type || s.Slot != p.Slot)).ToList();
                if (others.Count == 0) continue;
                (type, slot) = others[0];
                restored[k] = (piece, type, slot);
            }
            // The piece's place in its set's list (the set's pieces in order, none left out): the
            // outfit window picks a piece by that (Tradewinds' body to feet were 0 to 3 in a
            // recording), and 0.6.1 sending the column (3 to 6) had the game take nothing.
            var at = -1;
            for (var c = 0; c < columns.Count; c++)
                if (LookJson.BaseId(columns[c].Item) == id)
                {
                    at = c;
                    break;
                }
            if (at is < 0 or > 10 || containers[at] != InventoryType.Invalid) continue;
            containers[at] = type;
            slots[at] = (ushort)slot;
            storing.Add((piece, type, slot));
        }
        if (storing.Count < (job.StoredAt >= 0 ? 1 : 2))
        {
            Finish(stopAfterStore ?? "The set's pieces in your bags couldn't be found again", false);
            return;
        }
        var storedAt = -1;
        if (job.StoredAt >= 0)
        {
            // The set's place now (should the dresser have closed up as pieces left it).
            storedAt = DresserPlaces.FindSet(mgr, job.SetItem, job.StoredAt);
            if (storedAt < 0)
            {
                storing.Clear();
                Finish($"{data.ItemName(job.SetItem)} isn't in the dresser any more", false);
                return;
            }
        }
        var ok = storedAt >= 0
            ? mgr->StoreExistingOutfit((uint)storedAt, containers, slots)
            : mgr->StoreNewOutfit(job.SetItem, containers, slots);
        Recorder.Log($"  {(storedAt >= 0 ? $"StoreExistingOutfit {storedAt} (read at {job.StoredAt})" : $"StoreNewOutfit {job.SetItem}")}: "
                     + string.Join(" ", Enumerable.Range(0, 11).Where(i => containers[i] != InventoryType.Invalid).Select(i => $"[{i}]={containers[i]}:{slots[i]}")) + $" → {ok}");
        if (!ok)
        {
            storing.Clear();
            Finish($"The game wouldn't store {data.ItemName(job.SetItem)} (glamour prisms?)", false);
            return;
        }
        phase = Phase.WaitStored;
        deadline = now + TimeSpan.FromSeconds(8);
    }

    /// <summary>The next set: its pieces found and checked before any leaves the dresser.</summary>
    private static void Next()
    {
        restored.Clear();
        storing.Clear();
        stopAfterStore = null;
        while (jobs.Count > 0)
        {
            job = jobs.Dequeue();
            var mgr = MirageManager.Instance();
            var inv = InventoryManager.Instance();
            if (mgr == null || inv == null) break;
            var ready = new List<DresserPiece>();
            var notes = new List<string>();
            foreach (var p in job.Pieces)
            {
                var id = LookJson.BaseId(p.Item);
                if (DresserPlaces.Find(mgr, id, 0, 0, p.Index) < 0)
                {
                    notes.Add($"{p.Item} gone");
                    continue;
                }
                // A Unique piece you hold already wouldn't come out of the dresser.
                if (data!.Item(p.Item)?.Unique == true && DresserPlaces.Holding(id))
                {
                    notes.Add($"{p.Item} unique, held");
                    continue;
                }
                ready.Add(p);
            }
            // The set's pieces in your bags already (left by a run that stopped, or never stored) go in
            // with it, as they are: they're the set's too.
            var inBags = new List<(DresserPiece Piece, InventoryType Type, int Slot)>();
            foreach (var (_, item) in data!.SetPieces(job.SetItem))
            {
                var id = LookJson.BaseId(item);
                if (ready.Any(p => LookJson.BaseId(p.Item) == id)) continue;
                var place = DresserPlaces.InBags(id).OrderBy(p => p.Type).ThenBy(p => p.Slot).ToList();
                if (place.Count > 0) inBags.Add((new DresserPiece(-1, id, 0, 0), place[0].Type, place[0].Slot));
            }
            var min = job.StoredAt >= 0 ? 1 : 2;
            var free = (int)inv->GetEmptySlotsInBag();
            if (ready.Count + inBags.Count < min || ready.Count > free)
            {
                Recorder.Log($"OUTFITS set {job.SetItem} left: {ready.Count} pieces ready (needs {min}), {free} free bag places{(notes.Count > 0 ? "; " + string.Join(", ", notes) : string.Empty)}");
                if (ready.Count > free)
                {
                    Finish($"Not enough room in your bags for {data!.ItemName(job.SetItem)} ({ready.Count} needed, {free} free)", false);
                    return;
                }
                continue;
            }
            pieces = ready;
            pieceAt = 0;
            tries = 0;
            restored.AddRange(inBags);
            if (inBags.Count > 0) Recorder.Log($"  in your bags already: {string.Join(" ", inBags.Select(b => $"{b.Piece.Item} at {b.Type}:{b.Slot}"))}");
            phase = pieces.Count > 0 ? Phase.Restoring : Phase.Storing;
            deadline = DateTime.MinValue;
            Recorder.Log($"OUTFITS set {job.SetItem}{(job.StoredAt >= 0 ? $" (stored at {job.StoredAt})" : string.Empty)}: pieces {string.Join(" ", pieces.Select(p => $"{p.Index}:{p.Item}"))}{(notes.Count > 0 ? "; left " + string.Join(", ", notes) : string.Empty)}");
            return;
        }
        job = null;
        Finish(SetsDone == 0 ? "No set could be gathered (its pieces gone, or Unique ones you hold)" : $"{Words.Count(SetsDone, "set")} stored, {Words.Count(PlacesFreed, "place")} freed", SetsDone > 0);
    }

    private static void Finish(string words, bool allWent)
    {
        var left = restored.Select(r => data?.ItemName(r.Piece.Item) ?? r.Piece.Item.ToString()).ToList();
        if (phase == Phase.WaitRestored && pieceAt < pieces.Count) left.Add($"{data?.ItemName(pieces[pieceAt].Item) ?? "a piece"} (maybe: it was on its way)");
        if (!allWent && left.Count > 0) words += $". In your bags now: {Words.List(left)} (store them in the dresser yourself)";
        if (!allWent && SetsDone > 0) words += $". Done before that: {Words.Count(SetsDone, "set")}, {Words.Count(PlacesFreed, "place")} freed";
        Recorder.Log($"OUTFITS end: {words}");
        phase = Phase.Idle;
        jobs.Clear();
        job = null;
        restored.Clear();
        storing.Clear();
        stopAsked = false;
        stopAfterStore = null;
        var r = report;
        report = null;
        r?.Invoke(words, allWent);
    }
}

/// <summary>Finding things in the dresser and your bags, for <see cref="OutfitStorer"/> and <see cref="DresserTaker"/>.</summary>
public static unsafe class DresserPlaces
{
    private static readonly InventoryType[] Bags = { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 };

    /// <summary>
    /// A loose copy's dresser place, with these dyes: the one read if it still holds it, else the first
    /// that does (should the dresser close up its places as pieces leave); -1 when none does.
    /// </summary>
    public static int Find(MirageManager* mgr, uint id, byte dye1, byte dye2, int readAt)
    {
        var n = mgr->PrismBoxItemIds.Length;
        if (readAt >= 0 && readAt < n && Fits(mgr, readAt, id, dye1, dye2)) return readAt;
        for (var i = 0; i < n; i++)
            if (Fits(mgr, i, id, dye1, dye2))
                return i;
        return -1;
    }

    private static bool Fits(MirageManager* mgr, int i, uint id, byte dye1, byte dye2) =>
        LookJson.BaseId(mgr->PrismBoxItemIds[i]) == id && mgr->PrismBoxStain0Ids[i] == dye1 && mgr->PrismBoxStain1Ids[i] == dye2;

    /// <summary>How many places of the dresser hold the item (loose copies; a set is its own item).</summary>
    public static int Count(MirageManager* mgr, uint id)
    {
        var c = 0;
        var n = mgr->PrismBoxItemIds.Length;
        for (var i = 0; i < n; i++)
            if (LookJson.BaseId(mgr->PrismBoxItemIds[i]) == id)
                c++;
        return c;
    }

    /// <summary>The stored set's place: the one read if it still holds the set, else wherever it is; -1 when it's gone.</summary>
    public static int FindSet(MirageManager* mgr, uint setItem, int readAt)
    {
        var id = LookJson.BaseId(setItem);
        var n = mgr->PrismBoxItemIds.Length;
        if (readAt >= 0 && readAt < n && LookJson.BaseId(mgr->PrismBoxItemIds[readAt]) == id) return readAt;
        for (var i = 0; i < n; i++)
            if (LookJson.BaseId(mgr->PrismBoxItemIds[i]) == id)
                return i;
        return -1;
    }

    /// <summary>Whether you hold a copy in your bags, armoury chest or on you.</summary>
    public static bool Holding(uint item)
    {
        var where = new List<InventoryType>(Bags)
        {
            InventoryType.EquippedItems, InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody,
            InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist, InventoryType.ArmoryRings,
        };
        return Occupied(item, where).Count > 0;
    }

    /// <summary>The bag places holding an item now.</summary>
    public static HashSet<(InventoryType Type, int Slot)> InBags(uint item) => Occupied(item, Bags);

    private static HashSet<(InventoryType Type, int Slot)> Occupied(uint item, IEnumerable<InventoryType> containers)
    {
        var set = new HashSet<(InventoryType, int)>();
        var mgr = InventoryManager.Instance();
        if (mgr == null) return set;
        foreach (var bag in containers)
        {
            var c = mgr->GetInventoryContainer(bag);
            if (c == null || !c->IsLoaded) continue;
            for (var i = 0; i < c->Size; i++)
            {
                var it = c->GetInventorySlot(i);
                if (it != null && !it->IsEmpty() && LookJson.BaseId(it->GetBaseItemId()) == item) set.Add((bag, i));
            }
        }
        return set;
    }

    /// <summary>The item in a bag place (normal quality), or 0.</summary>
    public static uint SlotItem(InventoryType type, int slot)
    {
        var mgr = InventoryManager.Instance();
        if (mgr == null || type == InventoryType.Invalid) return 0;
        var c = mgr->GetInventoryContainer(type);
        if (c == null) return 0;
        var it = c->GetInventorySlot(slot);
        return it == null || it->IsEmpty() ? 0 : LookJson.BaseId(it->GetBaseItemId());
    }
}

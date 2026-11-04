using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>One set to gather: its item, its loose pieces in the dresser, and the set's own place when it's stored already (-1: a new set).</summary>
public sealed record OutfitJob(uint SetItem, IReadOnlyList<DresserPiece> Pieces, int StoredAt, bool Hq = false)
{
    /// <summary>The set as the game names it: a high-quality set's id is a million more (the outfit window sent 1045518 for Plain Pajamas of high-quality pieces).</summary>
    public uint GameSetId => Hq ? LookJson.BaseId(SetItem) + LookJson.HqOffset : LookJson.BaseId(SetItem);
}

/// <summary>
/// Making room for you: gathers outfit sets' loose pieces in the glamour dresser into their sets, as
/// doing it by hand does. The game stores an outfit only from your bags or armoury chest (the outfit
/// window lists nothing from the dresser), so for each set: each loose piece is restored to your bags
/// (<c>MirageManager.RestorePrismBoxItem</c>, the dresser's "Restore item to its original form"), and
/// once they're there they go back in as the set (<c>StoreNewOutfit</c>, or <c>StoreExistingOutfit</c>
/// into the set stored already, sending only the pieces it lacks: the pieces sent first to last in the
/// set's order, from the first place with no gaps, the rest <c>Invalid</c>), spending a glamour prism
/// each, as the game's own storing does. A set the game doesn't take is left (its pieces stay in your
/// bags, said at the end) and the run goes on; three in a row and it stops.
/// <para>
/// Before a set's first piece leaves the dresser, every piece is found (its place now), the bags are
/// checked for room, and a Unique piece you already hold is left. Each step is waited for: a restore
/// until the dresser has one copy fewer and a new bag place holds it; a store until those places are
/// empty. An ask the game refuses (it does while it settles the last step) is asked again every
/// quarter second, up to ten seconds, so it goes as fast as the game takes them. When a piece doesn't
/// come, the ones that did still go in as the set (when enough for one), and it stops, saying what's
/// left in your bags. Stop waits for a step the game is answering. With the glamour dresser open.
/// Framework thread.
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

    /// <summary>How many times the step at work was asked for and refused, and since when.</summary>
    private static int tries;

    private static DateTime refusedSince;

    /// <summary>Whether the set at work was handed to the game a second time (the first ask unanswered).</summary>
    private static bool storeAskedAgain;

    /// <summary>How many of the set were in the dresser before a new set was stored (seeing it arrive).</summary>
    private static int setsBefore;

    // The pace. The game refuses an ask while it's still settling the last one (a refusal sends
    // nothing), so instead of waiting set times between steps it's asked again every quarter second
    // until it takes it: as fast as the game goes. Recordings saw a refusal right after a piece came
    // and after a set was stored, each taken a moment later.
    internal static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(250);
    internal static readonly TimeSpan GiveUp = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AfterStep = TimeSpan.FromMilliseconds(150);

    /// <summary>The pieces of the set at work that are in your bags now (and where).</summary>
    private static readonly List<(DresserPiece Piece, InventoryType Type, int Slot)> restored = new();

    /// <summary>The pieces handed to the game to store (what the wait watches).</summary>
    private static readonly List<(DresserPiece Piece, InventoryType Type, int Slot)> storing = new();

    private static Action<string, bool>? report;
    private static bool stopAsked;

    /// <summary>Sets the game didn't take this run (their pieces left in your bags), and how many in a row.</summary>
    private static readonly List<string> failedSets = new();

    private static readonly List<string> leftInBags = new();
    private static int failedInARow;

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
        failedSets.Clear();
        leftInBags.Clear();
        failedInARow = 0;
        stopAsked = false;
        stopAfterStore = null;
        Recorder.Log($"OUTFITS start: {jobs.Count} sets, {jobs.Sum(j => j.Pieces.Count)} pieces");
        // The first set on the next frame (Between): an end at once is then told after Start returns,
        // never before (the caller would cover it with "Storing…").
        job = null;
        phase = Phase.Between;
        deadline = DateTime.MinValue;
        return true;
    }

    /// <summary>Stops once no step is waiting on the game (at once, when <paramref name="now"/>: the plugin's closing).</summary>
    public static void Stop(string why = "Stopped", bool now = false)
    {
        if (!Busy) return;
        // At once only between sets (nothing of a set out of the dresser); while a set's under way,
        // the pieces that came go in as the set first (Restoring and the waits read stopAsked).
        // Closing the plugin: at once, and nobody's told (its windows are gone).
        if (now) report = null;
        if (now || phase == Phase.Between) Finish(why, false);
        else
        {
            stopAsked = true;
            Recorder.Log($"OUTFITS stop asked: {why}");
        }
    }

    private static ((int Prisms, int FreeBag) Have, DateTime At) supplies;

    /// <summary>How many glamour prisms you have (in your bags), and how many free bag places (read again at most every quarter second: the panels ask every frame).</summary>
    public static (int Prisms, int FreeBag) Supplies(GameData gameData)
    {
        var now = DateTime.UtcNow;
        if (now - supplies.At < TimeSpan.FromMilliseconds(250)) return supplies.Have;
        try
        {
            var mgr = InventoryManager.Instance();
            if (mgr == null) return (0, 0);
            var prisms = gameData.GlamourPrisms().Sum(id => mgr->GetInventoryItemCount(id) + mgr->GetInventoryItemCount(id, true));
            supplies = ((prisms, (int)mgr->GetEmptySlotsInBag()), now);
            return supplies.Have;
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
        var now = DateTime.UtcNow;
        if (mgr == null || !mgr->PrismBoxLoaded)
        {
            // The dresser's data gone (a zone change, logging out) while a step was waited on: its
            // deadline still ends it, else it would wait for good.
            if (now > deadline + TimeSpan.FromSeconds(5)) Finish("The dresser's data went away (did you change zone?)", false);
            return;
        }
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
                var at = DresserPlaces.Find(mgr, piece.Item, piece.Dye1, piece.Dye2, piece.Index);
                if (at < 0)
                {
                    StoreWhatCameOrFinish($"{data!.ItemName(piece.Item)} isn't in the dresser any more");
                    break;
                }
                before = DresserPlaces.InBags(id, piece.Item >= LookJson.HqOffset);
                dresserBefore = DresserPlaces.Count(mgr, id);
                var ok = mgr->RestorePrismBoxItem((uint)at);
                if (!ok)
                {
                    // The game says no while it's settling the last step (with room to spare):
                    // asked again in a moment, for up to ten seconds.
                    if (tries++ == 0)
                    {
                        refusedSince = now;
                        Recorder.Log($"  RestorePrismBoxItem {at} (read at {piece.Index}, {piece.Item}) → refused, asking again");
                    }
                    if (now - refusedSince < GiveUp)
                    {
                        deadline = now + Retry;
                        break;
                    }
                    Recorder.Log($"  RestorePrismBoxItem {at}: refused {tries} times over {(now - refusedSince).TotalSeconds:0.0} s");
                    tries = 0;
                    StoreWhatCameOrFinish($"The game wouldn't restore {data!.ItemName(piece.Item)}");
                    break;
                }
                Recorder.Log($"  RestorePrismBoxItem {at} (read at {piece.Index}, {piece.Item}) → true, dresser had {dresserBefore}{(tries > 0 ? $" (after {tries} refusals, {(now - refusedSince).TotalSeconds:0.0} s)" : string.Empty)}");
                tries = 0;
                phase = Phase.WaitRestored;
                deadline = now + TimeSpan.FromSeconds(6);
                break;
            }
            case Phase.WaitRestored:
            {
                var piece = pieces[pieceAt];
                var id = LookJson.BaseId(piece.Item);
                var fresh = DresserPlaces.InBags(id, piece.Item >= LookJson.HqOffset).Except(before).ToList();
                // Arrived: the dresser has one copy fewer and a bag place holds it that didn't before
                // (a place set apart by a list, never compared with default: bag 1's first place is that).
                if (fresh.Count >= 1 && DresserPlaces.Count(mgr, id) < dresserBefore)
                {
                    restored.Add((piece, fresh[0].Type, fresh[0].Slot));
                    Recorder.Log($"  restored {id} to {fresh[0].Type}:{fresh[0].Slot}{(fresh.Count > 1 ? $" ({fresh.Count} new places held it: the first taken)" : string.Empty)}");
                    pieceAt++;
                    phase = pieceAt < pieces.Count ? Phase.Restoring : Phase.Storing;
                    deadline = now + AfterStep;
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
                // Stored: the places it was sent from are empty (or a new set's come into the dresser).
                var emptied = storing.All(r => DresserPlaces.SlotItem(r.Type, r.Slot) != LookJson.BaseId(r.Piece.Item));
                var arrived = job!.StoredAt < 0 && DresserPlaces.Count(mgr, LookJson.BaseId(job.SetItem)) > setsBefore;
                if (emptied || arrived)
                {
                    if (!emptied) Recorder.Log("  the set's in the dresser, its places in your bags not yet emptied");
                    // (Places freed: the pieces that came from the dresser; those already in your bags held none.)
                    var fromDresser = storing.Count(s => s.Piece.Index >= 0);
                    var freed = job!.StoredAt >= 0 ? fromDresser : Math.Max(0, fromDresser - 1);
                    SetsDone++;
                    PlacesFreed += freed;
                    failedInARow = 0;
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
                        // Pieces that came but had no place in the set (it had them already): left in your bags, said at the end.
                        Recorder.Log($"  left in your bags (no place in the set): {string.Join(" ", restored.Select(r => r.Piece.Item))}");
                        leftInBags.AddRange(restored.Select(r => data!.ItemName(r.Piece.Item)));
                        restored.Clear();
                    }
                    if (stopAsked)
                    {
                        Finish("Stopped", false);
                        break;
                    }
                    job = null;
                    phase = Phase.Between;
                    // (The next set's first piece is refused while the game settles this one: asked
                    // again until it's taken.)
                    deadline = now + AfterStep;
                    break;
                }
                if (now > deadline)
                {
                    var untouched = storing.All(r => DresserPlaces.SlotItem(r.Type, r.Slot) == LookJson.BaseId(r.Piece.Item));
                    if (untouched && !storeAskedAgain)
                    {
                        // Nothing moved: the ask seems lost. Asked once more (the game refuses it
                        // while the first is still on its way, and storing twice isn't possible:
                        // the pieces are in one place).
                        Recorder.Log($"  no answer to the store in 15 s, nothing moved: asking once more");
                        storeAskedAgain = true;
                        storing.Clear();
                        phase = Phase.Storing;
                        deadline = DateTime.MinValue;
                        tries = 0;
                        break;
                    }
                    var names = Words.List(storing.Select(r => data!.ItemName(r.Piece.Item)));
                    storing.Clear();
                    if (untouched)
                    {
                        // Still in your bags (listed at the end): on with the next set.
                        SetFailed($"The game didn't take {data!.ItemName(job.SetItem)}, asked twice");
                        break;
                    }
                    restored.Clear();
                    Finish($"{data!.ItemName(job.SetItem)}: the game hasn't answered. {names} may have gone into the set: check the dresser and your bags", false);
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

    /// <summary>
    /// The set at work wasn't taken: its pieces stay in your bags (said at the end), and it goes on
    /// with the next set, unless three in a row weren't, or it was to stop after this one.
    /// </summary>
    private static void SetFailed(string why)
    {
        Recorder.Log($"  set failed: {why}");
        failedSets.Add(data?.ItemName(job?.SetItem ?? 0) ?? "a set");
        leftInBags.AddRange(restored.Select(r => data?.ItemName(r.Piece.Item) ?? r.Piece.Item.ToString()));
        restored.Clear();
        storing.Clear();
        if (++failedInARow >= 3 || stopAfterStore != null || stopAsked)
        {
            Finish(failedInARow >= 3 ? $"{why} (the third set in a row the game didn't take: stopped)" : why, false);
            return;
        }
        job = null;
        phase = Phase.Between;
        tries = 0;
        deadline = DateTime.UtcNow + AfterStep;
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
        tries = 0;
        deadline = DateTime.MinValue;
    }

    private static void Store(MirageManager* mgr, DateTime now)
    {
        var storedAt = -1;
        if (job!.StoredAt >= 0)
        {
            // The set's place now (should the dresser have closed up as pieces left it).
            storedAt = DresserPlaces.FindSet(mgr, job.GameSetId, job.StoredAt);
            if (storedAt < 0)
            {
                SetFailed($"{data!.ItemName(job.SetItem)} isn't in the dresser any more");
                return;
            }
        }
        var columns = data!.SetPieces(job.SetItem);
        var sending = new List<(int Column, DresserPiece Piece, InventoryType Type, int Slot)>();
        var lost = new List<int>();
        for (var k = 0; k < restored.Count; k++)
        {
            var (piece, type, slot) = restored[k];
            var id = LookJson.BaseId(piece.Item);
            // Still where it came (you may have moved it meanwhile): else wherever it is in your bags now.
            if (DresserPlaces.SlotItem(type, slot) != id)
            {
                var others = DresserPlaces.InBags(id, job.Hq).Where(p => sending.All(s => s.Type != p.Type || s.Slot != p.Slot)).ToList();
                if (others.Count == 0)
                {
                    // Not in your bags any more (moved out, sold): nothing to store, nor to say is left there.
                    Recorder.Log($"  {piece.Item} isn't in your bags any more: left out");
                    lost.Add(k);
                    continue;
                }
                (type, slot) = others[0];
                restored[k] = (piece, type, slot);
            }
            var column = columns.Where(c => LookJson.BaseId(c.Item) == id).Select(c => c.Column).DefaultIfEmpty(-1).First();
            if (column < 0 || sending.Any(x => x.Column == column)) continue;
            // A stored set's own pieces aren't sent again (0.6.5 sent them, and the game refused it).
            if (storedAt >= 0 && mgr->IsSetSlotUnlocked((uint)storedAt, column)) continue;
            sending.Add((column, piece, type, slot));
        }
        for (var k = lost.Count - 1; k >= 0; k--) restored.RemoveAt(lost[k]);
        // The pieces sent go first to last in the set's order, from the first place on, with no gaps
        // (as the outfit window sends them, and other plugins do): a whole set's pieces are at their
        // place in its list, but a set sent with a gap (Gleaner's Attire's three pieces at 1 to 3, a
        // stored set's one missing piece at 1) was taken and never answered.
        sending.Sort((a, b) => a.Column.CompareTo(b.Column));
        var containers = stackalloc InventoryType[11];
        var slots = stackalloc ushort[11];
        for (var i = 0; i < 11; i++)
        {
            containers[i] = InventoryType.Invalid;
            slots[i] = 0;
        }
        storing.Clear();
        foreach (var (_, piece, type, slot) in sending.Take(11))
        {
            containers[storing.Count] = type;
            slots[storing.Count] = (ushort)slot;
            storing.Add((piece, type, slot));
        }
        if (storing.Count < (storedAt >= 0 ? 1 : 2))
        {
            storing.Clear();
            SetFailed(stopAfterStore ?? $"{data.ItemName(job.SetItem)}: its pieces in your bags couldn't be found again");
            return;
        }
        setsBefore = DresserPlaces.Count(mgr, LookJson.BaseId(job.SetItem));
        var ok = storedAt >= 0
            ? mgr->StoreExistingOutfit((uint)storedAt, containers, slots)
            : mgr->StoreNewOutfit(job.GameSetId, containers, slots);
        var what = $"{(storedAt >= 0 ? $"StoreExistingOutfit {storedAt} (read at {job.StoredAt})" : $"StoreNewOutfit {job.GameSetId}")}: "
                   + string.Join(" ", Enumerable.Range(0, 11).Where(i => containers[i] != InventoryType.Invalid).Select(i => $"[{i}]={containers[i]}:{slots[i]}"));
        if (!ok)
        {
            storing.Clear();
            // Refused as a restore is, while the game settles the last piece (0.6.3 stopped here,
            // with prisms to spare): asked again in a moment, for up to ten seconds.
            if (tries++ == 0)
            {
                refusedSince = now;
                Recorder.Log($"  {what} → refused, asking again");
            }
            if (now - refusedSince < GiveUp)
            {
                deadline = now + Retry;
                return;
            }
            Recorder.Log($"  {what}: refused {tries} times over {(now - refusedSince).TotalSeconds:0.0} s");
            tries = 0;
            var (prisms, _) = Supplies(data);
            if (prisms < restored.Count)
            {
                Finish($"The game wouldn't store {data.ItemName(job.SetItem)}: not enough glamour prisms?", false);
                return;
            }
            SetFailed($"The game wouldn't store {data.ItemName(job.SetItem)}");
            return;
        }
        Recorder.Log($"  {what} → true{(tries > 0 ? $" (after {tries} refusals, {(now - refusedSince).TotalSeconds:0.0} s)" : string.Empty)}");
        tries = 0;
        phase = Phase.WaitStored;
        deadline = now + TimeSpan.FromSeconds(15);
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
            if (mgr == null || inv == null)
            {
                Finish("The game isn't ready", false);
                return;
            }
            var ready = new List<DresserPiece>();
            var notes = new List<string>();
            foreach (var p in job.Pieces)
            {
                var id = LookJson.BaseId(p.Item);
                if (DresserPlaces.Find(mgr, p.Item, p.Dye1, p.Dye2, p.Index) < 0)
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
            var setAt = job.StoredAt >= 0 ? DresserPlaces.FindSet(mgr, job.GameSetId, job.StoredAt) : -1;
            foreach (var (column, item) in data!.SetPieces(job.SetItem))
            {
                var id = LookJson.BaseId(item);
                if (ready.Any(p => LookJson.BaseId(p.Item) == id)) continue;
                // A stored set's own pieces stay out (a copy of one in your bags isn't the set's to take).
                if (setAt >= 0 && mgr->IsSetSlotUnlocked((uint)setAt, column)) continue;
                var place = DresserPlaces.InBags(id, job.Hq).OrderBy(p => p.Type).ThenBy(p => p.Slot).ToList();
                if (place.Count > 0) inBags.Add((new DresserPiece(-1, job.Hq ? id + LookJson.HqOffset : id, 0, 0), place[0].Type, place[0].Slot));
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
            storeAskedAgain = false;
            restored.AddRange(inBags);
            if (inBags.Count > 0) Recorder.Log($"  in your bags already: {string.Join(" ", inBags.Select(b => $"{b.Piece.Item} at {b.Type}:{b.Slot}"))}");
            phase = pieces.Count > 0 ? Phase.Restoring : Phase.Storing;
            deadline = DateTime.MinValue;
            Recorder.Log($"OUTFITS set {job.SetItem}{(job.StoredAt >= 0 ? $" (stored at {job.StoredAt})" : string.Empty)}: pieces {string.Join(" ", pieces.Select(p => $"{p.Index}:{p.Item}"))}{(notes.Count > 0 ? "; left " + string.Join(", ", notes) : string.Empty)}");
            return;
        }
        job = null;
        if (failedSets.Count > 0)
        {
            Finish($"{Words.Count(SetsDone, "set")} stored, {Words.Count(PlacesFreed, "place")} freed; the game didn't take {Words.List(failedSets.Distinct())}", false);
            return;
        }
        Finish(SetsDone == 0 ? "No set could be gathered (its pieces gone, or Unique ones you hold)" : $"{Words.Count(SetsDone, "set")} stored, {Words.Count(PlacesFreed, "place")} freed", SetsDone > 0);
    }

    private static void Finish(string words, bool allWent)
    {
        var left = leftInBags.Concat(restored.Select(r => data?.ItemName(r.Piece.Item) ?? r.Piece.Item.ToString())).ToList();
        if (phase == Phase.WaitRestored && pieceAt < pieces.Count) left.Add($"{data?.ItemName(pieces[pieceAt].Item) ?? "a piece"} (maybe: it was on its way)");
        if (!allWent && left.Count > 0) words += $". In your bags now: {Words.List(left)} (store them in the dresser yourself)";
        if (!allWent && SetsDone > 0 && !words.Contains(" stored, ", StringComparison.Ordinal)) words += $". Done before that: {Words.Count(SetsDone, "set")}, {Words.Count(PlacesFreed, "place")} freed";
        Recorder.Log($"OUTFITS end: {words}");
        phase = Phase.Idle;
        jobs.Clear();
        job = null;
        restored.Clear();
        storing.Clear();
        leftInBags.Clear();
        failedSets.Clear();
        failedInARow = 0;
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
    /// A loose copy's dresser place, with these dyes and quality (<paramref name="item"/> as read: a
    /// high-quality copy's id is a million more): the one read if it still holds it, else the first
    /// that does (should the dresser close up its places as pieces leave); -1 when none does.
    /// </summary>
    public static int Find(MirageManager* mgr, uint item, byte dye1, byte dye2, int readAt)
    {
        var n = mgr->PrismBoxItemIds.Length;
        if (readAt >= 0 && readAt < n && Fits(mgr, readAt, item, dye1, dye2)) return readAt;
        for (var i = 0; i < n; i++)
            if (Fits(mgr, i, item, dye1, dye2))
                return i;
        return -1;
    }

    private static bool Fits(MirageManager* mgr, int i, uint item, byte dye1, byte dye2)
    {
        var there = mgr->PrismBoxItemIds[i];
        return LookJson.BaseId(there) == LookJson.BaseId(item) && there >= LookJson.HqOffset == item >= LookJson.HqOffset
               && mgr->PrismBoxStain0Ids[i] == dye1 && mgr->PrismBoxStain1Ids[i] == dye2;
    }

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

    /// <summary>
    /// The stored set's place (<paramref name="setItem"/> as the dresser has it: a high-quality set's
    /// a million more, a set of its own): the one read if it still holds it, else wherever it is; -1
    /// when it's gone.
    /// </summary>
    public static int FindSet(MirageManager* mgr, uint setItem, int readAt)
    {
        var n = mgr->PrismBoxItemIds.Length;
        if (readAt >= 0 && readAt < n && mgr->PrismBoxItemIds[readAt] == setItem) return readAt;
        // Down from where it was read first (places close up downwards as earlier ones leave), so a
        // second copy of the set is found before the first; then up.
        for (var i = Math.Min(readAt, n - 1); i >= 0; i--)
            if (mgr->PrismBoxItemIds[i] == setItem)
                return i;
        for (var i = Math.Max(readAt + 1, 0); i < n; i++)
            if (mgr->PrismBoxItemIds[i] == setItem)
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

    /// <summary>The bag places holding an item now (of one quality when <paramref name="hq"/> is given: a set's pieces are all one).</summary>
    public static HashSet<(InventoryType Type, int Slot)> InBags(uint item, bool? hq = null) => Occupied(item, Bags, hq);

    /// <summary>The items in your bags, a high-quality copy's id a million more (a set's pieces there go in with it).</summary>
    public static HashSet<uint> ItemsInBags()
    {
        var set = new HashSet<uint>();
        var mgr = InventoryManager.Instance();
        if (mgr == null) return set;
        foreach (var bag in Bags)
        {
            var c = mgr->GetInventoryContainer(bag);
            if (c == null || !c->IsLoaded) continue;
            for (var i = 0; i < c->Size; i++)
            {
                var it = c->GetInventorySlot(i);
                if (it != null && !it->IsEmpty())
                    set.Add(LookJson.BaseId(it->GetBaseItemId()) + (it->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) ? LookJson.HqOffset : 0));
            }
        }
        return set;
    }

    private static HashSet<(InventoryType Type, int Slot)> Occupied(uint item, IEnumerable<InventoryType> containers, bool? hq = null)
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
                if (it != null && !it->IsEmpty() && LookJson.BaseId(it->GetBaseItemId()) == item && (hq == null || it->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) == hq)) set.Add((bag, i));
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

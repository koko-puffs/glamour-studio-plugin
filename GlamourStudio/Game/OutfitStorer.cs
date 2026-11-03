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
/// once they're all there they go back in as the set (<c>StoreNewOutfit</c>, or <c>StoreExistingOutfit</c>
/// into the set stored already: each piece at its set column, the rest <c>Invalid</c>), spending a
/// glamour prism each, as the game's own storing does. One step at a time, each waited for (the piece
/// in your bags, the set in the dresser); the first that doesn't come stops it all, and says what's
/// left in your bags. With the glamour dresser open. Framework thread. Not yet watched in the game.
/// </summary>
public static unsafe class OutfitStorer
{
    private enum Phase { Idle, Restoring, WaitRestored, Storing, WaitStored, Between }

    private static readonly InventoryType[] Bags = { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 };

    private static Phase phase;
    private static readonly Queue<OutfitJob> jobs = new();
    private static OutfitJob? job;
    private static GameData? data;
    private static int pieceAt;
    private static DateTime deadline;
    private static HashSet<(InventoryType, int)> before = new();
    private static readonly List<(DresserPiece Piece, InventoryType Type, int Slot)> restored = new();
    private static Action<string, bool>? report;

    /// <summary>Whether it's at work.</summary>
    public static bool Busy => phase != Phase.Idle;

    /// <summary>Sets done and places freed so far (this run).</summary>
    public static int SetsDone { get; private set; }

    public static int PlacesFreed { get; private set; }

    /// <summary>Sets left to do (the one at work included).</summary>
    public static int SetsLeft => jobs.Count + (job != null ? 1 : 0);

    /// <summary>Starts gathering <paramref name="todo"/>; <paramref name="onEnd"/> hears how it ended (the words, and whether all went).</summary>
    public static void Start(IEnumerable<OutfitJob> todo, GameData gameData, Action<string, bool> onEnd)
    {
        if (Busy) return;
        data = gameData;
        report = onEnd;
        jobs.Clear();
        foreach (var j in todo) jobs.Enqueue(j);
        SetsDone = PlacesFreed = 0;
        Recorder.Log($"OUTFITS start: {jobs.Count} sets, {jobs.Sum(j => j.Pieces.Count)} pieces");
        Next();
    }

    /// <summary>Stops after the step at work (pieces restored stay in your bags).</summary>
    public static void Stop(string why = "Stopped")
    {
        if (!Busy) return;
        Finish(why, false);
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
        if (!GameWindows.IsShown("MiragePrismPrismBox"))
        {
            Finish("The glamour dresser closed", false);
            return;
        }
        if (Services.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
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
                var piece = job!.Pieces[pieceAt];
                // Where it is now: the place read before, else wherever a loose undyed copy is (should
                // the dresser close up its places as pieces leave it).
                var at = PlaceOf(mgr, piece);
                if (at < 0)
                {
                    Finish($"{data!.ItemName(piece.Item)} isn't in the dresser any more", false);
                    return;
                }
                before = Occupied(LookJson.BaseId(piece.Item));
                var ok = mgr->RestorePrismBoxItem((uint)at);
                Recorder.Log($"  RestorePrismBoxItem {at} (read at {piece.Index}, {piece.Item}) → {ok}");
                if (!ok)
                {
                    Finish($"The game wouldn't restore {data!.ItemName(piece.Item)} (a unique item you have, or no room in your bags)", false);
                    return;
                }
                phase = Phase.WaitRestored;
                deadline = now + TimeSpan.FromSeconds(6);
                break;
            }
            case Phase.WaitRestored:
            {
                var piece = job!.Pieces[pieceAt];
                var id = LookJson.BaseId(piece.Item);
                var fresh = Occupied(id).Except(before).FirstOrDefault();
                if (fresh != default)
                {
                    restored.Add((piece, fresh.Item1, fresh.Item2));
                    Recorder.Log($"  restored {id} to {fresh.Item1}:{fresh.Item2}");
                    pieceAt++;
                    phase = pieceAt < job.Pieces.Count ? Phase.Restoring : Phase.Storing;
                    deadline = now + TimeSpan.FromMilliseconds(400);
                    break;
                }
                if (now > deadline) Finish($"{data!.ItemName(piece.Item)} didn't come to your bags", false);
                break;
            }
            case Phase.Storing:
            {
                if (now < deadline) break;
                var containers = stackalloc InventoryType[11];
                var slots = stackalloc ushort[11];
                for (var i = 0; i < 11; i++)
                {
                    containers[i] = InventoryType.Invalid;
                    slots[i] = 0;
                }
                var columns = data!.SetPieces(job!.SetItem);
                var placed = 0;
                foreach (var (piece, type, slot) in restored)
                {
                    var col = columns.FirstOrDefault(c => LookJson.BaseId(c.Item) == LookJson.BaseId(piece.Item));
                    if (col.Item == 0 || col.Column is < 0 or > 10) continue;
                    containers[col.Column] = type;
                    slots[col.Column] = (ushort)slot;
                    placed++;
                }
                if (placed == 0)
                {
                    Finish("None of the set's pieces could be matched to its columns", false);
                    return;
                }
                var ok = job.StoredAt >= 0
                    ? mgr->StoreExistingOutfit((uint)job.StoredAt, containers, slots)
                    : mgr->StoreNewOutfit(job.SetItem, containers, slots);
                Recorder.Log($"  {(job.StoredAt >= 0 ? $"StoreExistingOutfit {job.StoredAt}" : $"StoreNewOutfit {job.SetItem}")}: "
                             + string.Join(" ", Enumerable.Range(0, 11).Where(i => containers[i] != InventoryType.Invalid).Select(i => $"col{i}={containers[i]}:{slots[i]}")) + $" → {ok}");
                if (!ok)
                {
                    Finish($"The game wouldn't store {data.ItemName(job.SetItem)} (glamour prisms?)", false);
                    return;
                }
                phase = Phase.WaitStored;
                deadline = now + TimeSpan.FromSeconds(8);
                break;
            }
            case Phase.WaitStored:
            {
                var gone = restored.All(r => SlotItem(r.Type, r.Slot) != LookJson.BaseId(r.Piece.Item));
                if (gone)
                {
                    var freed = job!.StoredAt >= 0 ? restored.Count : Math.Max(0, restored.Count - 1);
                    SetsDone++;
                    PlacesFreed += freed;
                    Recorder.Log($"  stored {job.SetItem}: {restored.Count} pieces, {freed} places freed");
                    restored.Clear();
                    job = null;
                    phase = Phase.Between;
                    deadline = now + TimeSpan.FromSeconds(1);
                    break;
                }
                if (now > deadline) Finish($"{data!.ItemName(job!.SetItem)} wasn't stored (the game didn't take its pieces)", false);
                break;
            }
            case Phase.Between:
                if (now >= deadline) Next();
                break;
        }
    }

    private static void Next()
    {
        restored.Clear();
        if (jobs.Count == 0)
        {
            Finish($"{Words.Count(SetsDone, "set")} stored, {Words.Count(PlacesFreed, "place")} freed", true);
            return;
        }
        job = jobs.Dequeue();
        pieceAt = 0;
        phase = job.Pieces.Count > 0 ? Phase.Restoring : Phase.Between;
        Recorder.Log($"OUTFITS set {job.SetItem}{(job.StoredAt >= 0 ? $" (stored at {job.StoredAt})" : string.Empty)}: pieces {string.Join(" ", job.Pieces.Select(p => $"{p.Index}:{p.Item}"))}");
    }

    private static void Finish(string words, bool allWent)
    {
        var left = restored.Select(r => data?.ItemName(r.Piece.Item) ?? r.Piece.Item.ToString()).ToList();
        if (!allWent && left.Count > 0) words += $". In your bags now: {Words.List(left)} (store them in the dresser yourself)";
        if (!allWent && SetsDone > 0) words += $". Done before that: {Words.Count(SetsDone, "set")}, {Words.Count(PlacesFreed, "place")} freed";
        Recorder.Log($"OUTFITS end: {words}");
        phase = Phase.Idle;
        jobs.Clear();
        job = null;
        restored.Clear();
        var r = report;
        report = null;
        r?.Invoke(words, allWent);
    }

    /// <summary>The dresser place of a loose undyed copy of the piece: the one read if it's still there, else the first; -1 when none.</summary>
    private static int PlaceOf(MirageManager* mgr, DresserPiece piece)
    {
        var id = LookJson.BaseId(piece.Item);
        var n = mgr->PrismBoxItemIds.Length;
        if (piece.Index >= 0 && piece.Index < n && Fits(mgr, piece.Index, id)) return piece.Index;
        for (var i = 0; i < n; i++)
            if (Fits(mgr, i, id))
                return i;
        return -1;
    }

    private static bool Fits(MirageManager* mgr, int i, uint id) =>
        LookJson.BaseId(mgr->PrismBoxItemIds[i]) == id && mgr->PrismBoxStain0Ids[i] == 0 && mgr->PrismBoxStain1Ids[i] == 0;

    /// <summary>The bag places holding an item now.</summary>
    private static HashSet<(InventoryType, int)> Occupied(uint item)
    {
        var set = new HashSet<(InventoryType, int)>();
        var mgr = InventoryManager.Instance();
        if (mgr == null) return set;
        foreach (var bag in Bags)
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

    private static uint SlotItem(InventoryType type, int slot)
    {
        var mgr = InventoryManager.Instance();
        if (mgr == null) return 0;
        var c = mgr->GetInventoryContainer(type);
        if (c == null) return 0;
        var it = c->GetInventorySlot(slot);
        return it == null || it->IsEmpty() ? 0 : LookJson.BaseId(it->GetBaseItemId());
    }
}

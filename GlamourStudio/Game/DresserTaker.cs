using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>
/// Taking pieces out of the glamour dresser to your bags for you (its duplicates, and a stored set's
/// copy another copy of the set has, whose pieces all come out): each restored with
/// <c>MirageManager.RestorePrismBoxItem</c>, as the dresser's "Restore item to its original form"
/// does, one at a time, each waited for (the dresser one copy fewer, a new bag place holding it).
/// Its place is found again first (the same item with the same dyes), a Unique piece you hold is
/// left, and it stops when your bags are full or a piece doesn't come. Nothing's thrown away: what
/// comes out is yours to sell or discard. A refused ask is asked again in a moment, as
/// <see cref="OutfitStorer"/> does. With the glamour dresser open. Framework thread.
/// </summary>
public static unsafe class DresserTaker
{
    private enum Phase { Idle, Taking, Waiting }

    private static Phase phase;
    private static readonly Queue<DresserPiece> queue = new();
    private static DresserPiece current;
    private static GameData? data;
    private static DateTime deadline;
    private static HashSet<(InventoryType Type, int Slot)> before = new();
    private static int dresserBefore;
    private static int tries;
    private static DateTime refusedSince;

    /// <summary>Whether the one at work is a whole stored set (a copy of a set another copy has).</summary>
    private static bool isSet;
    private static readonly List<string> skipped = new();
    private static Action<string, bool>? report;
    private static bool stopAsked;

    public static bool Busy => phase != Phase.Idle;

    /// <summary>Pieces taken out so far (this run), and left to take.</summary>
    public static int Done { get; private set; }

    public static int Left => queue.Count + (phase == Phase.Waiting || tries > 0 ? 1 : 0);

    /// <summary>Starts taking <paramref name="todo"/> out; false when this or the outfit storer is at work.</summary>
    public static bool Start(IEnumerable<DresserPiece> todo, GameData gameData, Action<string, bool> onEnd)
    {
        if (Busy || OutfitStorer.Busy) return false;
        data = gameData;
        report = onEnd;
        queue.Clear();
        foreach (var p in todo) queue.Enqueue(p);
        Done = 0;
        tries = 0;
        skipped.Clear();
        stopAsked = false;
        phase = Phase.Taking;
        deadline = DateTime.MinValue;
        Recorder.Log($"TAKE OUT start: {queue.Count} pieces");
        return true;
    }

    /// <summary>Stops once no piece is on its way (at once, when <paramref name="now"/>).</summary>
    public static void Stop(string why = "Stopped", bool now = false)
    {
        if (!Busy) return;
        // Closing the plugin: at once, and nobody's told (its windows are gone).
        if (now) report = null;
        if (now || phase == Phase.Taking) Finish(why, false);
        else stopAsked = true;
    }

    public static void Tick()
    {
        if (phase == Phase.Idle) return;
        if (phase == Phase.Taking && !GameWindows.IsShown("MiragePrismPrismBox"))
        {
            Finish("The glamour dresser closed", false);
            return;
        }
        if (phase == Phase.Taking && Services.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
        {
            Finish("You're in combat", false);
            return;
        }
        var mgr = MirageManager.Instance();
        var inv = InventoryManager.Instance();
        var now = DateTime.UtcNow;
        if (mgr == null || inv == null || !mgr->PrismBoxLoaded)
        {
            // The dresser's data gone while a piece was waited on: its deadline still ends it.
            if (now > deadline + TimeSpan.FromSeconds(5)) Finish("The dresser's data went away (did you change zone?)", false);
            return;
        }
        switch (phase)
        {
            case Phase.Taking:
            {
                if (now < deadline) break;
                if (stopAsked)
                {
                    Finish("Stopped", false);
                    break;
                }
                if (queue.Count == 0)
                {
                    Finish($"{Words.Count(Done, "duplicate")} taken out to your bags", true);
                    break;
                }
                if (inv->GetEmptySlotsInBag() == 0)
                {
                    Finish("Your bags are full", false);
                    break;
                }
                if (tries == 0) current = queue.Dequeue();
                var id = LookJson.BaseId(current.Item);
                isSet = data!.SetPieces(id).Count > 0;
                if (isSet)
                {
                    // A copy of a stored set: restoring it puts its pieces in your bags, so room for
                    // them all; and another copy of the set must still be there.
                    var setAt = DresserPlaces.FindSet(mgr, current.Item, current.Index);
                    var holds = setAt < 0 ? 0 : data.SetPieces(id).Count(c => mgr->IsSetSlotUnlocked((uint)setAt, c.Column));
                    if (setAt < 0 || DresserPlaces.Count(mgr, id) < 2)
                    {
                        skipped.Add(data.ItemName(id));
                        Recorder.Log($"  set {current.Item} at {current.Index}: left ({(setAt < 0 ? "gone" : "the last copy")})");
                        tries = 0;
                        break;
                    }
                    if (inv->GetEmptySlotsInBag() < holds)
                    {
                        Finish($"Your bags need {holds} free places for {data.ItemName(id)}", false);
                        break;
                    }
                    dresserBefore = DresserPlaces.Count(mgr, id);
                    if (!mgr->RestorePrismBoxItem((uint)setAt))
                    {
                        if (tries++ == 0)
                        {
                            refusedSince = now;
                            Recorder.Log($"  RestorePrismBoxItem {setAt} (set {current.Item}) → refused, asking again");
                        }
                        if (now - refusedSince < OutfitStorer.GiveUp)
                        {
                            deadline = now + OutfitStorer.Retry;
                            break;
                        }
                        tries = 0;
                        Finish($"The game wouldn't take out {data.ItemName(id)}", false);
                        break;
                    }
                    Recorder.Log($"  RestorePrismBoxItem {setAt} (set {current.Item}, {holds} pieces) → true{(tries > 0 ? $" (after {tries} refusals)" : string.Empty)}");
                    tries = 0;
                    phase = Phase.Waiting;
                    deadline = now + TimeSpan.FromSeconds(6);
                    break;
                }
                var at = DresserPlaces.Find(mgr, current.Item, current.Dye1, current.Dye2, current.Index);
                if (at < 0 || DresserPlaces.Count(mgr, id) < 2 && !current.InSet && !InASet(mgr, id))
                {
                    // Gone, or the last copy now (taking it would leave none): left.
                    skipped.Add(data!.ItemName(current.Item));
                    Recorder.Log($"  {current.Item} at {current.Index}: left ({(at < 0 ? "gone" : "the last copy")})");
                    tries = 0;
                    break;
                }
                if (data!.Item(current.Item)?.Unique == true && DresserPlaces.Holding(id))
                {
                    skipped.Add(data.ItemName(current.Item));
                    Recorder.Log($"  {current.Item}: left (Unique, you hold one)");
                    tries = 0;
                    break;
                }
                before = DresserPlaces.InBags(id);
                dresserBefore = DresserPlaces.Count(mgr, id);
                var ok = mgr->RestorePrismBoxItem((uint)at);
                if (!ok)
                {
                    // The game says no while it settles the last piece (with room to spare): asked
                    // again every quarter second, up to ten seconds, as the outfit storer does.
                    if (tries++ == 0)
                    {
                        refusedSince = now;
                        Recorder.Log($"  RestorePrismBoxItem {at} (read at {current.Index}, {current.Item}) → refused, asking again");
                    }
                    if (now - refusedSince < OutfitStorer.GiveUp)
                    {
                        deadline = now + OutfitStorer.Retry;
                        break;
                    }
                    Recorder.Log($"  RestorePrismBoxItem {at}: refused {tries} times over {(now - refusedSince).TotalSeconds:0.0} s");
                    tries = 0;
                    Finish($"The game wouldn't take out {data.ItemName(current.Item)}", false);
                    break;
                }
                Recorder.Log($"  RestorePrismBoxItem {at} (read at {current.Index}, {current.Item} {current.Dye1}/{current.Dye2}) → true{(tries > 0 ? $" (after {tries} refusals)" : string.Empty)}");
                tries = 0;
                phase = Phase.Waiting;
                deadline = now + TimeSpan.FromSeconds(6);
                break;
            }
            case Phase.Waiting:
            {
                var id = LookJson.BaseId(current.Item);
                // (A set: its copy gone from the dresser; its pieces come to your bags with it.)
                if ((isSet || DresserPlaces.InBags(id).Except(before).Any()) && DresserPlaces.Count(mgr, id) < dresserBefore)
                {
                    Done++;
                    phase = Phase.Taking;
                    deadline = now + TimeSpan.FromMilliseconds(150);
                    break;
                }
                if (now > deadline) Finish($"{data!.ItemName(current.Item)} didn't come to your bags (it may come yet: check them)", false);
                break;
            }
        }
    }

    /// <summary>Whether a stored set in the dresser holds the item (then a loose copy isn't the last).</summary>
    private static bool InASet(MirageManager* mgr, uint id)
    {
        var sets = data!.SetOf(id);
        return sets != 0 && DresserPlaces.Count(mgr, sets) > 0;
    }

    private static void Finish(string words, bool allWent)
    {
        if (skipped.Count > 0) words += $". Left in the dresser: {Words.List(skipped.Distinct().Take(6))}{(skipped.Count > 6 ? " and more" : string.Empty)}";
        if (!allWent && Done > 0) words += $". Taken out before that: {Done}";
        Recorder.Log($"TAKE OUT end: {words}");
        phase = Phase.Idle;
        queue.Clear();
        stopAsked = false;
        var r = report;
        report = null;
        r?.Invoke(words, allWent);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>
/// Tries a whole look on in the game's fitting room (<c>Tryon</c>), anywhere: no dresser needed.
/// <para>
/// The game takes one piece at a time, and only once its window is open and the character in it
/// has loaded: pieces sent while it's still opening are lost, all but the last (the second
/// recording saw ten sent in one frame and the window show only the weapon). So this opens the
/// window with the first piece, waits until it's ready, then sends the pieces one a frame, and at
/// the end checks what the window lists, sending any piece that didn't stick once more.
/// </para>
/// Ticked every frame from the plugin's update.
/// </summary>
public static unsafe class FittingRoom
{
    private readonly record struct Want(Slot Slot, uint Item, byte Dye1, byte Dye2);

    private enum Phase { Idle, Closing, Opening, Adding, Checking }

    private static Phase phase;
    private static List<Want> wants = new();
    private static readonly Queue<Want> queue = new();
    private static Action<int, int>? done;
    private static DateTime deadline;
    private static DateTime visibleSince;
    private static int waitFrames;
    private static bool retried;

    /// <summary>Whether a look is being tried on now.</summary>
    public static bool Busy => phase != Phase.Idle;

    /// <summary>
    /// Starts trying <paramref name="pieces"/> on. <paramref name="onDone"/> gets how many pieces the
    /// fitting room shows and how many were asked for (0 shown: it didn't open).
    /// </summary>
    public static void Start(IReadOnlyDictionary<Slot, Piece> pieces, Action<int, int> onDone)
    {
        wants = Slots.All.Where(pieces.ContainsKey).Select(s => (Slot: s, Piece: pieces[s]))
            .Where(x => x.Piece.Item != 0)
            .Select(x => new Want(x.Slot, LookJson.BaseId(x.Piece.Item), x.Piece.Dye1, x.Piece.Dye2))
            .ToList();
        done = onDone;
        retried = false;
        queue.Clear();
        if (wants.Count == 0)
        {
            Finish(0);
            return;
        }

        foreach (var w in wants) queue.Enqueue(w);
        Recorder.Log($"TRY ON {wants.Count} pieces: {string.Join(" ", wants.Select(w => $"{Slots.Key(w.Slot)}:{w.Item} {w.Dye1}/{w.Dye2}"))}; window {(WindowShown ? "open" : "closed")}");

        if (WindowShown)
        {
            // A fresh window, so pieces tried on before (in slots this look leaves empty) don't stay.
            var agent = AgentTryon.Instance();
            if (agent != null) agent->AgentInterface.Hide();
            Recorder.Log("  fitting room open already: closing it first");
            phase = Phase.Closing;
            deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            return;
        }

        Open();
    }

    /// <summary>Stops trying on (the plugin is closing).</summary>
    public static void Stop()
    {
        phase = Phase.Idle;
        queue.Clear();
        done = null;
    }

    public static void Tick()
    {
        switch (phase)
        {
            case Phase.Closing:
                // Closed, or it wouldn't close: open it (again) either way.
                if (!WindowShown || DateTime.UtcNow > deadline) Open();
                break;

            case Phase.Opening:
                if (WindowShown)
                {
                    if (visibleSince == default) visibleSince = DateTime.UtcNow;
                    // Its character loaded, or (should that flag never turn) a moment after it showed.
                    if (CharacterLoaded || DateTime.UtcNow - visibleSince > TimeSpan.FromSeconds(1.5))
                    {
                        Recorder.Log($"  fitting room ready (character loaded {CharacterLoaded}): {Listed()}");
                        phase = Phase.Adding;
                        waitFrames = 2;
                    }
                }
                else if (DateTime.UtcNow > deadline)
                {
                    Recorder.Log("  the fitting room didn't open");
                    Finish(0);
                }
                break;

            case Phase.Adding:
                if (!WindowShown)
                {
                    Recorder.Log("  the fitting room closed while trying on");
                    Finish(0);
                    break;
                }
                if (waitFrames-- > 0) break;
                // Pieces the window lists already (the first, when it stuck) aren't sent again.
                while (queue.Count > 0 && Shows(queue.Peek(), dyes: true)) queue.Dequeue();
                if (queue.Count > 0)
                {
                    var w = queue.Dequeue();
                    var ok = Send(w);
                    Recorder.Log($"  tried on {Slots.Key(w.Slot)} {w.Item} {w.Dye1}/{w.Dye2} → {ok}");
                    waitFrames = 2;
                    break;
                }
                phase = Phase.Checking;
                waitFrames = 10;
                break;

            case Phase.Checking:
                if (!WindowShown)
                {
                    Finish(0);
                    break;
                }
                if (waitFrames-- > 0) break;
                var missing = wants.Where(w => !Shows(w, dyes: false)).ToList();
                Recorder.Log($"  fitting room: {Listed()}; not drawn {missing.Count}{(missing.Count > 0 && !retried ? ", sending those again" : string.Empty)}");
                if (missing.Count > 0 && !retried)
                {
                    retried = true;
                    foreach (var w in missing) queue.Enqueue(w);
                    phase = Phase.Adding;
                    waitFrames = 2;
                    break;
                }
                // The count: pieces drawn, or at least on the window's list (the view's data may not
                // hold every piece it draws; the recording's line above shows both).
                Finish(wants.Count(w => Shows(w, dyes: false) || Listed(w)));
                break;
        }
    }

    private static void Open()
    {
        phase = Phase.Opening;
        visibleSince = default;
        deadline = DateTime.UtcNow + TimeSpan.FromSeconds(6);
        // The first piece opens the window (it may not stick: it's sent again once the window's ready).
        var first = queue.Peek();
        var ok = Send(first);
        Recorder.Log($"  opening the fitting room with {Slots.Key(first.Slot)} {first.Item} → {ok}");
        if (!ok && !WindowShown)
        {
            // The game said no straight away (a piece it won't try on): the next one may open it.
            queue.Dequeue();
            if (queue.Count > 0) Open();
            else Finish(0);
        }
    }

    private static bool Send(Want w)
    {
        try
        {
            return AgentTryon.TryOn(0, w.Item, w.Dye1, w.Dye2, 0, false);
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't try a piece on");
            return false;
        }
    }

    private static void Finish(int shown)
    {
        var wanted = wants.Count;
        var callback = done;
        phase = Phase.Idle;
        queue.Clear();
        done = null;
        Recorder.Log($"  TRY ON done: {shown} of {wanted} in the fitting room");
        callback?.Invoke(shown, wanted);
    }

    private static bool WindowShown
    {
        get
        {
            var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName("Tryon").Address;
            return addon != null && addon->IsVisible;
        }
    }

    private static bool CharacterLoaded
    {
        get
        {
            var agent = AgentTryon.Instance();
            return agent != null && agent->CharaView.CharacterLoaded;
        }
    }

    /// <summary>
    /// Whether the fitting room shows this piece on the character (with its dyes, when asked). Read
    /// from what its view draws, not from its list: the list can hold a piece the view lost.
    /// </summary>
    private static bool Shows(Want w, bool dyes)
    {
        var agent = AgentTryon.Instance();
        if (agent == null) return false;
        foreach (ref var it in agent->CharaView.Items)
        {
            var shown = it.GlamourItemId != 0 ? it.GlamourItemId : it.ItemId;
            if (shown == 0 || LookJson.BaseId(shown) != w.Item) continue;
            var (d1, d2) = it.GlamourItemId != 0 ? (it.GlamourStain0Id, it.GlamourStain1Id) : (it.Stain0Id, it.Stain1Id);
            if (!dyes || (d1 == w.Dye1 && d2 == w.Dye2)) return true;
        }
        return false;
    }

    /// <summary>Whether the fitting room's list holds this piece.</summary>
    private static bool Listed(Want w)
    {
        var agent = AgentTryon.Instance();
        if (agent == null) return false;
        foreach (ref var it in agent->TryOnItems)
            if (it.Id != 0 && LookJson.BaseId(it.Id) == w.Item) return true;
        return false;
    }

    /// <summary>The fitting room's list and what its view draws, for the recording.</summary>
    public static string Listed()
    {
        var agent = AgentTryon.Instance();
        if (agent == null) return "no agent";
        var listed = new List<string>();
        var i = 0;
        foreach (ref var it in agent->TryOnItems)
        {
            if (it.Id != 0) listed.Add($"{i}:{it.Id} {it.Stain0Id}/{it.Stain1Id}");
            i++;
        }
        var drawn = new List<string>();
        foreach (ref var it in agent->CharaView.Items)
        {
            if (it.ItemId == 0 && it.GlamourItemId == 0) continue;
            drawn.Add($"{it.SlotId}:{it.ItemId}{(it.GlamourItemId != 0 ? $" as {it.GlamourItemId}" : string.Empty)} {it.Stain0Id}/{it.Stain1Id}");
        }
        return $"list [{string.Join("; ", listed)}], drawn [{string.Join("; ", drawn)}]";
    }
}

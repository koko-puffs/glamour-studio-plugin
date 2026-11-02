using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GlamourStudio.Core;
using ItemSource = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMiragePrismMiragePlateData.ItemSource;

namespace GlamourStudio.Game;

/// <summary>What filling a plate did: how many slots were set and emptied, and what went wrong.</summary>
public sealed record FillResult(int Plate, int Set, int Cleared, int Dyed, IReadOnlyList<string> Problems)
{
    public bool Ok => Problems.Count == 0;
}

/// <summary>
/// The game's glamour plate editor (the dresser's Glamour Plates window, <c>MiragePrismMiragePlate</c>),
/// driven through its own agent the way choosing a piece in it is: for each slot, the slot is picked
/// (<c>SelectedItemIndex</c>) and given a dresser piece or an armoire piece with
/// <c>SetSelectedItemData</c> (the source, its place, the item and its dyes), or emptied with
/// <c>ClearItemData</c>; then the plate is marked changed, so the game asks before it's left and
/// Save keeps it. Nothing is saved for you: you look, then press Save.
/// <para>
/// Dyeing from your bags (off unless Settings says so) gives the slot pending dyes from bottles in
/// your bags with <c>SetSelectedItemStains</c>, as dragging a dye onto it does; the game spends them
/// when the plate is saved. [unsure: written from FFXIVClientStructs' signatures, not yet watched in
/// the game, hence off by default.]
/// </para>
/// Framework thread only.
/// </summary>
public static unsafe class PlateEditor
{
    private const string Window = "MiragePrismMiragePlate";

    private static AgentMiragePrismMiragePlate* Agent()
    {
        var agent = AgentMiragePrismMiragePlate.Instance();
        return agent != null && agent->Data != null ? agent : null;
    }

    /// <summary>
    /// Whether the plate editor is open at a glamour dresser, where plates can be changed. The same
    /// window opened from the Character window (its Glamour Plate button) only shows and applies
    /// plates: the agent's OpenMode is 0 at a dresser and 1 from there (seen in a recording).
    /// </summary>
    public static bool IsOpen
    {
        get
        {
            try
            {
                var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(Window).Address;
                var agent = Agent();
                return a != null && a->IsVisible && agent != null && agent->Data->OpenMode == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>The plate open in the editor (1 to 20), or 0 when it isn't open.</summary>
    public static int SelectedPlate
    {
        get
        {
            if (!IsOpen) return 0;
            var n = (int)Agent()->Data->SelectedMiragePlateIndex + 1;
            return n is >= 1 and <= Plates.Count ? n : 0;
        }
    }

    /// <summary>What the open plate has on now (unsaved changes included).</summary>
    public static Dictionary<Slot, Piece> Current()
    {
        var pieces = new Dictionary<Slot, Piece>();
        if (!IsOpen) return pieces;
        var items = Agent()->Data->CurrentItems;
        for (var i = 0; i < items.Length; i++)
        {
            ref var it = ref items[i];
            if (it.ItemId == 0 || Slots.FromPlateIndex(i) is not { } slot) continue;
            pieces[slot] = new Piece(LookJson.BaseId(it.ItemId), it.StainIds[0], it.StainIds[1]);
        }
        return pieces;
    }

    /// <summary>
    /// Whether the open plate has changes not yet saved: its slots against the plate as the game
    /// keeps it saved, and any dye waiting to be applied. (The agent's own HasChanges stayed false
    /// through a removed piece and a dye in a recording, while the game asked to discard them.)
    /// </summary>
    public static bool HasChanges
    {
        get
        {
            if (!IsOpen) return false;
            var data = Agent()->Data;
            var plate = (int)data->SelectedMiragePlateIndex;
            var mm = MirageManager.Instance();
            if (mm == null || !mm->GlamourPlatesLoaded || plate < 0 || plate >= mm->GlamourPlates.Length) return false;
            var saved = mm->GlamourPlates[plate];
            var items = data->CurrentItems;
            for (var i = 0; i < items.Length && i < saved.ItemIds.Length; i++)
            {
                ref var it = ref items[i];
                if (it.StainState != 0) return true;
                if (it.ItemId != saved.ItemIds[i] || it.StainIds[0] != saved.Stain0Ids[i] || it.StainIds[1] != saved.Stain1Ids[i]) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Fills the open plate as planned. <paramref name="dyeItems"/> finds a bottle of a dye in your bags
    /// (for the slots dyed from them).
    /// </summary>
    public static FillResult Fill(PlatePlan plan, Func<byte, (InventoryType Type, int Slot)?> dyeItems)
    {
        var problems = new List<string>();
        var agent = Agent();
        if (!IsOpen || agent == null) return new FillResult(0, 0, 0, 0, new[] { "The plate editor closed." });
        var data = agent->Data;
        var plate = (int)data->SelectedMiragePlateIndex + 1;
        var keep = data->SelectedItemIndex;
        int set = 0, cleared = 0, dyed = 0;
        try
        {
            foreach (var step in plan.Steps)
            {
                var index = Slots.PlateIndex(step.Slot);
                if (index < 0) continue;
                try
                {
                    switch (step.Kind)
                    {
                        case StepKind.Set when step.Want is { } want:
                            data->SelectedItemIndex = (uint)index;
                            agent->SetSelectedItemData(step.Source == PlateSource.Dresser ? ItemSource.PrismBox : ItemSource.Cabinet,
                                step.SourceId, step.Item != 0 ? step.Item : want.Item, step.Has1, step.Has2);
                            Recorder.Log($"  SetSelectedItemData slot {index} ({Slots.Key(step.Slot)}): source {step.Source} {step.SourceId}, item {(step.Item != 0 ? step.Item : want.Item)}, stains {step.Has1}/{step.Has2}"
                                         + $" → now {data->CurrentItems[index].ItemId} src {data->CurrentItems[index].Source}/{data->CurrentItems[index].SourceId} stains {data->CurrentItems[index].StainIds[0]}/{data->CurrentItems[index].StainIds[1]}");
                            set++;
                            if (step.DyeFromBags && Dye(agent, step, want, dyeItems)) dyed++;
                            break;
                        case StepKind.Clear:
                            if (data->CurrentItems[index].ItemId == 0) break;
                            data->SelectedItemIndex = (uint)index;
                            agent->ClearItemData((uint)index);
                            Recorder.Log($"  ClearItemData slot {index} ({Slots.Key(step.Slot)}) → now {data->CurrentItems[index].ItemId}");
                            cleared++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Services.Log.Warning(ex, "Glamour Studio couldn't fill a plate's {Slot}", step.Slot);
                    problems.Add($"{Slots.Word(step.Slot)} couldn't be set.");
                }
            }
        }
        finally
        {
            // (The agent's HasChanges isn't written: in a recording it isn't the flag the game goes by.)
            data->SelectedItemIndex = keep;
        }
        return new FillResult(plate, set, cleared, dyed, problems);
    }

    private static bool Dye(AgentMiragePrismMiragePlate* agent, PlateStep step, Piece want, Func<byte, (InventoryType Type, int Slot)?> dyeItems)
    {
        var mgr = InventoryManager.Instance();
        if (mgr == null) return false;
        InventoryItem* Bottle(byte stain)
        {
            if (dyeItems(stain) is not { } at) return null;
            var c = mgr->GetInventoryContainer(at.Type);
            return c == null ? null : c->GetInventorySlot(at.Slot);
        }
        var b0 = step.Dye1 == DyeOutcome.FromBags ? Bottle(want.Dye1) : null;
        var b1 = step.Dye2 == DyeOutcome.FromBags ? Bottle(want.Dye2) : null;
        if (b0 == null && b1 == null) return false;
        agent->SetSelectedItemStains(
            b0, b0 != null ? want.Dye1 : step.Has1, b0 != null ? b0->GetBaseItemId() : 0,
            b1, b1 != null ? want.Dye2 : step.Has2, b1 != null ? b1->GetBaseItemId() : 0);
        Recorder.Log($"  SetSelectedItemStains {Slots.Key(step.Slot)}: dye1 {(b0 != null ? $"{want.Dye1} from bottle {b0->GetBaseItemId()}" : $"kept {step.Has1}")}, dye2 {(b1 != null ? $"{want.Dye2} from bottle {b1->GetBaseItemId()}" : $"kept {step.Has2}")}");
        return true;
    }

    /// <summary>
    /// The plate editor in words, for the recorder: closed, or the plate, the slot picked, whether
    /// it's changed, how it was opened, and each slot (the item, where it came from, its dyes, and
    /// with <paramref name="detail"/> its icon, flags and pending dyes).
    /// </summary>
    public static string Describe(GameData names, bool detail)
    {
        var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(Window).Address;
        var agent = AgentMiragePrismMiragePlate.Instance();
        if (agent == null) return "no agent";
        // (The agent's data is read only with its window there: closed, it may be gone.)
        if (a == null) return "window closed";
        var window = a->IsVisible ? "window open" : "window hidden";
        if (agent->Data == null) return $"{window}, agent has no data";
        var data = agent->Data;
        var sb = new System.Text.StringBuilder();
        sb.Append($"{window}, plate {data->SelectedMiragePlateIndex + 1}, slot {data->SelectedItemIndex}, changes {data->HasChanges}, mode {data->OpenMode}, gearset {data->GearsetId}, "
                  + $"context {data->ContextMenuItemIndex}, opener {data->OpenerAddonId}");
        var items = data->CurrentItems;
        sb.Append(", slots:");
        for (var i = 0; i < items.Length; i++)
        {
            ref var it = ref items[i];
            var key = Slots.FromPlateIndex(i) is { } sl ? Slots.Key(sl) : i.ToString();
            if (it.ItemId == 0)
            {
                if (detail) sb.Append($" {key}=empty;");
                continue;
            }
            sb.Append($" {key}={it.ItemId} {names.ItemName(it.ItemId % 500000)} ({it.Source}:{it.SourceId}) {it.StainIds[0]}/{it.StainIds[1]}");
            // A dye dragged on by hand shows as pending until the plate's saved.
            if (!detail && (it.StainState != 0 || it.PendingStainIds[0] != 0 || it.PendingStainIds[1] != 0))
                sb.Append($" stainState {it.StainState} pending {it.PendingStainIds[0]}/{it.PendingStainIds[1]} bottles {it.PendingStainItemIds[0]}/{it.PendingStainItemIds[1]}");
            if (detail)
                sb.Append($" icon {it.IconId} flags {it.Flags} cat {it.EquipSlotCategory} stainState {it.StainState} pending {it.PendingStainIds[0]}/{it.PendingStainIds[1]}"
                          + $" bottles {it.PendingStainItemIds[0]}/{it.PendingStainItemIds[1]} at {it.PendingStainInventoryTypes[0]}:{it.PendingStainInventorySlots[0]}/{it.PendingStainInventoryTypes[1]}:{it.PendingStainInventorySlots[1]}");
            sb.Append(';');
        }
        return sb.ToString();
    }

    /// <summary>Tries a look on in the game's fitting room (anywhere: no dresser needed).</summary>
    public static int TryOn(IReadOnlyDictionary<Slot, Piece> pieces)
    {
        var shown = 0;
        foreach (var slot in Slots.All)
        {
            if (!pieces.TryGetValue(slot, out var p) || p.Item == 0) continue;
            try
            {
                if (AgentTryon.TryOn(0, LookJson.BaseId(p.Item), p.Dye1, p.Dye2, 0, false)) shown++;
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, "Glamour Studio couldn't try a piece on");
            }
        }
        return shown;
    }

    /// <summary>
    /// Wears a glamour plate now, with the gearset you're on, as <c>/gearset change &lt;set&gt; &lt;plate&gt;</c>
    /// does (the game has no command for a plate alone: <c>/glamourplate</c> isn't one). In a
    /// sanctuary, on a job whose gear the plate fits. False when you're on no gearset.
    /// </summary>
    public static bool Wear(int plate)
    {
        if (plate is < 1 or > Plates.Count) return false;
        var ui = UIModule.Instance();
        var gs = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.Instance();
        if (ui == null || gs == null) return false;
        var current = gs->CurrentGearsetIndex;
        if (current < 0 || !gs->IsValidGearset(current)) return false;
        var command = $"/gearset change {current + 1} {plate}";
        var text = Utf8String.FromString(command);
        try
        {
            Recorder.Log($"WEAR {command}");
            ui->ProcessChatBoxEntry(text, nint.Zero, false);
            return true;
        }
        finally
        {
            text->Dtor(true);
        }
    }
}

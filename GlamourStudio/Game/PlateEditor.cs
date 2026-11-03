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
/// your bags with <c>SetItemStain</c>, as dragging a dye onto it does; the game spends them
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

    /// <summary>The plate window's mode: 0 opened at a dresser (editing), 1 from the Character window (applying only); -1 when it isn't open.</summary>
    public static int Mode
    {
        get
        {
            try
            {
                var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(Window).Address;
                var agent = Agent();
                return a != null && a->IsVisible && agent != null ? (int)agent->Data->OpenMode : -1;
            }
            catch
            {
                return -1;
            }
        }
    }

    /// <summary>The plate picked in the plate window, opened either way (1 to 20), or 0.</summary>
    public static int PickedPlate
    {
        get
        {
            if (Mode < 0) return 0;
            var n = (int)Agent()->Data->SelectedMiragePlateIndex + 1;
            return n is >= 1 and <= Plates.Count ? n : 0;
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
                            if (step.DyeFromBags && Dye(agent, index, step, want, dyeItems)) dyed++;
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

    /// <summary>
    /// Dyes a slot's piece with bottles from your bags, channel by channel, with <c>SetItemStain</c>
    /// (the slot named outright). 0.3.x used <c>SetSelectedItemStains</c>, which dyes the slot the
    /// editor has picked for its own menu, not the one just filled: a recording saw every dye land on
    /// the weapon, the last one winning.
    /// </summary>
    private static bool Dye(AgentMiragePrismMiragePlate* agent, int index, PlateStep step, Piece want, Func<byte, (InventoryType Type, int Slot)?> dyeItems)
    {
        var mgr = InventoryManager.Instance();
        if (mgr == null) return false;
        var c = 0;
        foreach (var (channel, outcome, stain) in new[] { (0, step.Dye1, want.Dye1), (1, step.Dye2, want.Dye2) })
        {
            if (outcome != DyeOutcome.FromBags || stain == 0 || dyeItems(stain) is not { } at) continue;
            var container = mgr->GetInventoryContainer(at.Type);
            var bottle = container == null ? null : container->GetInventorySlot(at.Slot);
            if (bottle == null || bottle->IsEmpty()) continue;
            agent->SetItemStain((uint)index, stain, bottle, bottle->GetBaseItemId(), channel);
            ref var now = ref agent->Data->CurrentItems[index];
            Recorder.Log($"  SetItemStain slot {index} ({Slots.Key(step.Slot)}) channel {channel}: dye {stain} from bottle {bottle->GetBaseItemId()} at {at.Type}:{at.Slot}"
                         + $" → pending {now.PendingStainIds[0]}/{now.PendingStainIds[1]}, stain state {now.StainState}");
            c++;
        }
        return c > 0;
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

    /// <summary>A plate picked in the window, to apply once the game shows it picked (and when it gives up).</summary>
    private static (int Plate, DateTime Until)? applyAfterPick;

    /// <summary>
    /// Applies a plate in the plate window opened from the Character window (mode 1), as doing it by
    /// hand does: the plate picked first (<c>[i:17, u:plate−1]</c>), then the window's apply
    /// (<c>[i:15, u:0]</c>, which applies the plate picked: its number isn't the plate; 0.6.6 sent
    /// the plate there and the window applied plate 1, picked when it opens). The apply goes on the
    /// frame the game shows the plate picked (<see cref="Tick"/>). <c>/gearset change</c> is refused
    /// while that window's open ("…while occupied", a recording saw). Null when asked of the game,
    /// else why not.
    /// </summary>
    public static string? ApplyInWindow(int plate)
    {
        if (plate is < 1 or > Plates.Count) return "There's no such plate";
        if (Mode != 1) return "Open Glamour Plate from the Character window first";
        if (Services.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]) return "Not in combat";
        try
        {
            if (!Fire(17, (uint)(plate - 1))) return "The Glamour Plate window closed";
            Recorder.Log($"APPLY plate {plate} in the plate window: picked it [i:17, u:{plate - 1}]");
            applyAfterPick = (plate, DateTime.UtcNow + TimeSpan.FromSeconds(2));
            return null;
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't apply a plate");
            return "The game wouldn't apply it";
        }
    }

    /// <summary>Applies the plate picked by <see cref="ApplyInWindow"/> once the window shows it picked. Framework thread.</summary>
    public static void Tick()
    {
        if (applyAfterPick is not { } want) return;
        try
        {
            if (Mode != 1 || DateTime.UtcNow > want.Until)
            {
                Recorder.Log($"APPLY plate {want.Plate}: {(Mode != 1 ? "the window closed" : $"plate {PickedPlate} still picked")}, not applied");
                applyAfterPick = null;
                return;
            }
            if (PickedPlate != want.Plate) return;
            applyAfterPick = null;
            Fire(15, 0);
            Recorder.Log($"APPLY plate {want.Plate} in the plate window: applied [i:15, u:0]");
        }
        catch (Exception ex)
        {
            applyAfterPick = null;
            Services.Log.Warning(ex, "Glamour Studio couldn't apply a plate");
        }
    }

    /// <summary>The plate window's callback <c>[i:<paramref name="op"/>, u:<paramref name="arg"/>]</c>; false when it isn't shown.</summary>
    private static bool Fire(int op, uint arg)
    {
        var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(Window).Address;
        if (a == null || !a->IsVisible) return false;
        var values = stackalloc AtkValue[2];
        values[0].Type = AtkValueType.Int;
        values[0].Int = op;
        values[1].Type = AtkValueType.UInt;
        values[1].UInt = arg;
        a->FireCallback(2, values, true);
        return true;
    }

    /// <summary>
    /// Wears a glamour plate now, with the gearset you're on, as <c>/gearset change &lt;set&gt; &lt;plate&gt;</c>
    /// does (the game has no command for a plate alone: <c>/glamourplate</c> isn't one). In a
    /// sanctuary, on a job whose gear the plate fits; not in combat, nor at a dresser
    /// (<see cref="Gearsets.Blocked"/>).
    /// </summary>
    /// <returns>Null when it was asked of the game, else why not, in words.</returns>
    public static string? Wear(int plate)
    {
        if (plate is < 1 or > Plates.Count) return "There's no such plate";
        if (Gearsets.Blocked(true) is { } why) return why;
        var ui = UIModule.Instance();
        var gs = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.Instance();
        if (ui == null || gs == null) return "The game isn't ready";
        var current = gs->CurrentGearsetIndex;
        if (current < 0 || !gs->IsValidGearset(current)) return "You're on no gearset: change to one first";
        var command = $"/gearset change {current + 1} {plate}";
        var text = Utf8String.FromString(command);
        try
        {
            Recorder.Log($"WEAR {command}");
            ui->ProcessChatBoxEntry(text, nint.Zero, false);
            return null;
        }
        finally
        {
            text->Dtor(true);
        }
    }
}

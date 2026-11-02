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

    /// <summary>Whether the plate editor is open and ready.</summary>
    public static bool IsOpen
    {
        get
        {
            try
            {
                var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(Window).Address;
                return a != null && a->IsVisible && Agent() != null;
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

    /// <summary>Whether the open plate has changes not yet saved.</summary>
    public static bool HasChanges => IsOpen && Agent()->Data->HasChanges;

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
                            set++;
                            if (step.DyeFromBags && Dye(agent, step, want, dyeItems)) dyed++;
                            break;
                        case StepKind.Clear:
                            if (data->CurrentItems[index].ItemId == 0) break;
                            data->SelectedItemIndex = (uint)index;
                            agent->ClearItemData((uint)index);
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
            data->SelectedItemIndex = keep;
            if (set + cleared > 0) data->HasChanges = true;
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
        return true;
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

    /// <summary>Wears a glamour plate now, as <c>/glamourplate</c> does (in a sanctuary, on a job whose gear it fits).</summary>
    public static bool Wear(int plate)
    {
        if (plate is < 1 or > Plates.Count) return false;
        var ui = UIModule.Instance();
        if (ui == null) return false;
        var text = Utf8String.FromString($"/glamourplate {plate}");
        try
        {
            ui->ProcessChatBoxEntry(text, nint.Zero, false);
            return true;
        }
        finally
        {
            text->Dtor(true);
        }
    }
}

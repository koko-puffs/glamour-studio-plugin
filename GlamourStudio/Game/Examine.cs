using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>Someone's outfit, from the game's Examine window: their name and what they show (glamour and all).</summary>
public sealed record Examined(uint EntityId, string Name, Body Body, Dictionary<Slot, Piece> Pieces);

/// <summary>
/// The game's Examine window (<c>CharacterInspect</c>): while it's open, the gear it shows is in the
/// <c>Examine</c> container, in the order of what you wear (the waist's place empty), each piece's
/// glamour in <c>GlamourId</c>. Framework thread.
/// </summary>
public static unsafe class Examine
{
    public static bool IsOpen
    {
        get
        {
            try
            {
                var a = (AtkUnitBase*)Services.GameGui.GetAddonByName("CharacterInspect").Address;
                return a != null && a->IsVisible;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>What the open Examine window shows, or null when it's closed or still loading.</summary>
    public static Examined? Read(GameData data)
    {
        if (!IsOpen) return null;
        try
        {
            var agent = AgentInspect.Instance();
            var mgr = InventoryManager.Instance();
            if (agent == null || mgr == null) return null;
            var c = mgr->GetInventoryContainer(InventoryType.Examine);
            if (c == null || !c->IsLoaded) return null;
            var pieces = new Dictionary<Slot, Piece>();
            for (var i = 0; i < c->Size; i++)
            {
                if (Slots.FromEquippedIndex(i) is not { } slot) continue;
                var item = c->GetInventorySlot(i);
                if (item == null || item->IsEmpty()) continue;
                var shown = item->GlamourId != 0 ? LookJson.BaseId(item->GlamourId) : item->GetBaseItemId();
                if (shown == 0 || data.Item(shown)?.IsGear != true) continue;
                pieces[slot] = new Piece(shown, item->GetStain(0), item->GetStain(1));
            }
            if (pieces.Count == 0) return null;
            var id = agent->CurrentEntityId;
            var who = Services.Objects.SearchByEntityId(id);
            var name = who?.Name.TextValue ?? string.Empty;
            // Their body for the site's mannequin (Customize's second byte: 1 is the feminine one).
            var body = who is Dalamud.Game.ClientState.Objects.Types.ICharacter ch && ch.Customize.Length > 1 && ch.Customize[1] == 0
                ? Body.Masculine
                : Body.Feminine;
            return new Examined(id, name, body, pieces);
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't read the Examine window");
            return null;
        }
    }
}

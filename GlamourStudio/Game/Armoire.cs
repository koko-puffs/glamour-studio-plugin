using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GlamourStudio.Core;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace GlamourStudio.Game;

/// <summary>
/// Putting pieces away in the armoire, as its window's Store does (<c>Cabinet.StoreCabinetItem</c>,
/// by the piece's Cabinet row): only with the armoire open (at an inn), and only for pieces the
/// armoire takes. The game takes the piece from your bags or armoury chest. [unsure: written from
/// FFXIVClientStructs' signature, not yet watched in the game.] Framework thread.
/// </summary>
public static unsafe class Armoire
{
    /// <summary>Whether the armoire's window is open (and its contents loaded).</summary>
    public static bool IsOpen
    {
        get
        {
            try
            {
                var a = (AtkUnitBase*)Services.GameGui.GetAddonByName("Cabinet").Address;
                var ui = UIState.Instance();
                return a != null && a->IsVisible && ui != null && ui->Cabinet.IsCabinetLoaded();
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Whether you're wearing a copy of the item now (the armoire isn't offered it: take it off first).</summary>
    public static bool IsWorn(uint item)
    {
        try
        {
            var c = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            if (c == null || !c->IsLoaded) return false;
            var want = LookJson.BaseId(item);
            for (var i = 0; i < c->Size; i++)
            {
                var it = c->GetInventorySlot(i);
                if (it != null && !it->IsEmpty() && LookJson.BaseId(it->GetBaseItemId()) == want) return true;
            }
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't read what you wear");
        }
        return false;
    }

    /// <summary>Stores the piece of a Cabinet row; false when the game said no (or couldn't be asked).</summary>
    public static bool Store(uint cabinetRow)
    {
        try
        {
            var ui = UIState.Instance();
            if (ui == null || !IsOpen || ui->Cabinet.IsItemInCabinet(cabinetRow)) return false;
            return ui->Cabinet.StoreCabinetItem(cabinetRow);
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't put a piece in the armoire");
            return false;
        }
    }
}

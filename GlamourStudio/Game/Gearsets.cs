using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>A gearset of yours: its number (as the game counts from 1), name, job and the glamour plate linked to it (0: none).</summary>
public sealed record Gearset(int Id, string Name, uint Job, int Plate)
{
    public int Number => Id + 1;
}

/// <summary>
/// Your gearsets (the game's RaptureGearsetModule): which glamour plate each has linked, linking one
/// (as the gearset window's Link Glamour Plate does: <c>LinkGlamourPlate</c>, 0 unlinks), and
/// changing to one (<c>EquipGearset</c>: its plate comes with it, where the game allows). Framework thread.
/// </summary>
public static unsafe class Gearsets
{
    /// <summary>Every gearset that exists, in the game's order.</summary>
    public static List<Gearset> All()
    {
        var list = new List<Gearset>();
        try
        {
            var m = RaptureGearsetModule.Instance();
            if (m == null) return list;
            var entries = m->Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                ref var e = ref entries[i];
                if (!e.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
                list.Add(new Gearset(i, e.NameString, e.ClassJob, e.GlamourSetLink is var p and >= 1 and <= Plates.Count ? p : 0));
            }
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't read your gearsets");
        }
        return list;
    }

    /// <summary>Links a plate (1 to 20) to a gearset, or (0) unlinks it. False when the game wouldn't be asked.</summary>
    public static bool Link(int gearset, int plate)
    {
        if (plate is < 0 or > Plates.Count) return false;
        try
        {
            var m = RaptureGearsetModule.Instance();
            if (m == null || !m->IsValidGearset(gearset)) return false;
            m->LinkGlamourPlate(gearset, (byte)plate);
            return true;
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't link a plate to a gearset");
            return false;
        }
    }

    /// <summary>Changes to a gearset (its linked plate goes on where the game allows it).</summary>
    public static bool Equip(int gearset)
    {
        try
        {
            var m = RaptureGearsetModule.Instance();
            if (m == null || !m->IsValidGearset(gearset)) return false;
            return m->EquipGearset(gearset, 0) == 0;
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't change gearset");
            return false;
        }
    }
}

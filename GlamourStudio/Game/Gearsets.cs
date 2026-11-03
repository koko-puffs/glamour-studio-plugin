using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
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
            Recorder.Log($"LinkGlamourPlate gearset {gearset + 1} plate {plate} → its link now {m->Entries[gearset].GlamourSetLink}");
            return true;
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't link a plate to a gearset");
            return false;
        }
    }

    /// <summary>Changes to a gearset (its linked plate goes on where the game allows it).</summary>
    /// <summary>
    /// Why changing gearset (or wearing a plate, <paramref name="plate"/>) won't work now, in words;
    /// null when it will. From the recordings: the game refuses in combat ("Unable to execute command
    /// during combat") and at a glamour dresser or with Glamour Plates opened there ("…while
    /// occupied"), and changes gearset outside a sanctuary but puts no plate on ("Unable to apply
    /// glamour plates here").
    /// </summary>
    public static string? Blocked(bool plate)
    {
        try
        {
            var c = Services.Condition;
            if (c[ConditionFlag.InCombat]) return "Not in combat: the game won't change gearset then";
            if (PlateEditor.Mode == 0 || GameWindows.IsShown("MiragePrismPrismBox")) return "Close Glamour Plates and the dresser first";
            if (PlateEditor.Mode == 1) return "Close the Glamour Plate window first (or apply it there)";
            if (c[ConditionFlag.BetweenAreas] || c[ConditionFlag.BetweenAreas51]) return "Wait until you've arrived";
            if (c[ConditionFlag.OccupiedInEvent] || c[ConditionFlag.OccupiedInQuestEvent] || c[ConditionFlag.Occupied] || c[ConditionFlag.OccupiedSummoningBell] || c[ConditionFlag.Casting])
                return "Not while you're busy (talking to someone, a bell, casting)";
            if (plate)
            {
                var t = TerritoryInfo.Instance();
                if (t != null && !t->InSanctuary) return "Plates only go on in a sanctuary: a city, an inn or a residential area";
            }
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't tell whether a gearset change would work");
        }
        return null;
    }

    /// <summary>Whether you're in a sanctuary (where a plate goes on), true when it can't be told.</summary>
    public static bool InSanctuary
    {
        get
        {
            try
            {
                var t = TerritoryInfo.Instance();
                return t == null || t->InSanctuary;
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>Changes to a gearset: null when the game took it, else why not, in words.</summary>
    public static string? Equip(int gearset)
    {
        if (Blocked(false) is { } why) return why;
        try
        {
            var m = RaptureGearsetModule.Instance();
            if (m == null || !m->IsValidGearset(gearset)) return "That gearset is gone";
            var r = m->EquipGearset(gearset, 0);
            Recorder.Log($"EquipGearset {gearset + 1} → returned {r}, current now {m->CurrentGearsetIndex + 1}");
            return r == 0 ? null : "The game wouldn't change gearset now";
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't change gearset");
            return "The game wouldn't change gearset now";
        }
    }
}

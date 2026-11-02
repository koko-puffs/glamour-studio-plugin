using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourStudio.Core;

/// <summary>Where a plate's piece comes from (the plate editor's own two: <c>ItemSource</c>).</summary>
public enum PlateSource
{
    Dresser,
    Armoire,
}

/// <summary>What happens to one slot of the plate.</summary>
public enum StepKind
{
    /// <summary>The piece goes on, from the dresser or the armoire.</summary>
    Set,

    /// <summary>The slot is emptied (the look leaves it bare).</summary>
    Clear,

    /// <summary>Left as it is (a weapon the look doesn't have, or a bare slot kept by choice).</summary>
    Keep,

    /// <summary>The look's piece isn't in the dresser or the armoire: the slot is left as it is.</summary>
    Missing,
}

/// <summary>A dye channel's outcome on a piece put on a plate.</summary>
public enum DyeOutcome
{
    /// <summary>The copy used already has the dye (or the look wants none and it has none).</summary>
    Same,

    /// <summary>Dyed from a dye in your bags as the plate is saved.</summary>
    FromBags,

    /// <summary>It keeps the copy's own dye: no dye for it in your bags (or dyeing from bags is off).</summary>
    Differs,
}

/// <summary>One slot of a plate as it will be filled.</summary>
/// <param name="Want">The look's piece for the slot, or null where it has none.</param>
/// <param name="SourceId">The dresser's place (0 to 799) or the armoire's (Cabinet) row.</param>
/// <param name="Has1">The copy's own first dye (what the plate shows without dyeing).</param>
/// <param name="Elsewhere">For a missing piece: where else you have it, if anywhere.</param>
/// <param name="Item">The copy's own item id (a high-quality copy's is its own).</param>
public sealed record PlateStep(
    Slot Slot,
    Piece? Want,
    StepKind Kind,
    PlateSource Source = PlateSource.Dresser,
    uint SourceId = 0,
    uint Item = 0,
    byte Has1 = 0,
    byte Has2 = 0,
    DyeOutcome Dye1 = DyeOutcome.Same,
    DyeOutcome Dye2 = DyeOutcome.Same,
    Place? Elsewhere = null,
    bool InSet = false)
{
    /// <summary>A dye the piece still needs that it won't get (no dye in your bags).</summary>
    public bool DyeLeft => Kind == StepKind.Set && (Dye1 == DyeOutcome.Differs || Dye2 == DyeOutcome.Differs);

    /// <summary>A dye that comes from your bags.</summary>
    public bool DyeFromBags => Kind == StepKind.Set && (Dye1 == DyeOutcome.FromBags || Dye2 == DyeOutcome.FromBags);
}

/// <summary>What filling the plate with a look will do, slot by slot (in the plate's order).</summary>
public sealed record PlatePlan(IReadOnlyList<PlateStep> Steps)
{
    public IEnumerable<PlateStep> Set => Steps.Where(s => s.Kind == StepKind.Set);

    public IEnumerable<PlateStep> Missing => Steps.Where(s => s.Kind == StepKind.Missing);

    /// <summary>The look's pieces (each slot it fills).</summary>
    public int Wanted => Steps.Count(s => s.Want != null);

    /// <summary>The look's pieces that go on.</summary>
    public int Ready => Steps.Count(s => s.Want != null && s.Kind == StepKind.Set);

    /// <summary>Anything to do at all (a piece to set or a slot to empty).</summary>
    public bool Any => Steps.Any(s => s.Kind is StepKind.Set or StepKind.Clear);

    /// <summary>The dyes taken from your bags: each stain and how many.</summary>
    public IReadOnlyDictionary<byte, int> BagDyes => Steps.Where(s => s.Kind == StepKind.Set)
        .SelectMany(s => new[] { s.Dye1 == DyeOutcome.FromBags ? s.Want!.Value.Dye1 : (byte)0, s.Dye2 == DyeOutcome.FromBags ? s.Want!.Value.Dye2 : (byte)0 })
        .Where(d => d != 0).GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
}

/// <summary>The choices that change how a plate is filled (Settings).</summary>
/// <param name="ClearBare">Empty the armour and accessory slots the look leaves bare (weapons are kept unless the look has one).</param>
/// <param name="BagDyes">Dye pieces whose copy has other dyes with the dyes in your bags (null: don't).</param>
public sealed record PlateOptions(bool ClearBare = true, IReadOnlyDictionary<byte, int>? BagDyes = null);

/// <summary>
/// Works out how a look goes on a glamour plate, before anything's touched: for each piece, the copy
/// to use (in the dresser, the one whose dyes are closest to the look's; else the armoire's), and
/// what becomes of its dyes; what's missing and where it is instead. The game's plate takes a piece
/// from the dresser or the armoire only, and one dresser copy fills one slot.
/// </summary>
public static class PlatePlanner
{
    /// <param name="pieces">The look's pieces.</param>
    /// <param name="dresser">The dresser's pieces, as the game has them now.</param>
    /// <param name="armoire">The armoire's stored pieces: each item and its Cabinet row.</param>
    /// <param name="dyeChannels">How many dyes an item takes (0, 1 or 2).</param>
    /// <param name="elsewhere">Where else a piece is (the plugin's record of your wardrobe), for what's missing.</param>
    public static PlatePlan Plan(
        IReadOnlyDictionary<Slot, Piece> pieces,
        IReadOnlyList<DresserPiece> dresser,
        IReadOnlyDictionary<uint, uint> armoire,
        Func<uint, int> dyeChannels,
        Func<uint, Place?> elsewhere,
        PlateOptions options)
    {
        var byItem = dresser.ToLookup(d => LookJson.BaseId(d.Item));
        var used = new HashSet<int>();
        var usedArmoire = new HashSet<uint>();
        var bag = options.BagDyes?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<byte, int>();
        var steps = new List<PlateStep>();
        foreach (var slot in Slots.PlateOrder)
        {
            if (!pieces.TryGetValue(slot, out var want) || want.Item == 0)
            {
                var clear = options.ClearBare && !Slots.IsWeapon(slot);
                steps.Add(new PlateStep(slot, null, clear ? StepKind.Clear : StepKind.Keep));
                continue;
            }
            var id = LookJson.BaseId(want.Item);
            var channels = Math.Clamp(dyeChannels(id), 0, 2);
            var w1 = channels >= 1 ? want.Dye1 : (byte)0;
            var w2 = channels >= 2 ? want.Dye2 : (byte)0;

            // The dresser's copies of it, the one whose dyes match most first (then the one first in the dresser).
            var copy = byItem[id]
                .Where(d => !used.Contains(d.Index))
                .OrderByDescending(d => (d.Dye1 == w1 ? 1 : 0) + (d.Dye2 == w2 ? 1 : 0))
                .ThenBy(d => d.InSet ? 1 : 0)
                .ThenBy(d => d.Index)
                .Cast<DresserPiece?>()
                .FirstOrDefault();
            if (copy is { } c)
            {
                used.Add(c.Index);
                steps.Add(Dyed(new PlateStep(slot, want, StepKind.Set, PlateSource.Dresser, (uint)c.Index, c.Item, c.Dye1, c.Dye2, InSet: c.InSet),
                    w1, w2, options.BagDyes != null, bag));
                continue;
            }
            if (armoire.TryGetValue(id, out var cabinet) && usedArmoire.Add(id))
            {
                // The armoire keeps pieces undyed.
                steps.Add(Dyed(new PlateStep(slot, want, StepKind.Set, PlateSource.Armoire, cabinet, id), w1, w2, options.BagDyes != null, bag));
                continue;
            }
            steps.Add(new PlateStep(slot, want, StepKind.Missing, Elsewhere: elsewhere(id)));
        }
        return new PlatePlan(steps);
    }

    private static PlateStep Dyed(PlateStep step, byte w1, byte w2, bool fromBags, Dictionary<byte, int> bag)
    {
        DyeOutcome Channel(byte want, byte has)
        {
            if (want == has) return DyeOutcome.Same;
            // Taking a dye off isn't something a bag's dye does.
            if (want != 0 && fromBags && bag.TryGetValue(want, out var n) && n > 0)
            {
                bag[want] = n - 1;
                return DyeOutcome.FromBags;
            }
            return DyeOutcome.Differs;
        }
        return step with { Dye1 = Channel(w1, step.Has1), Dye2 = Channel(w2, step.Has2) };
    }
}

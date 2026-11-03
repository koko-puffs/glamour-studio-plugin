using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourStudio.Core;

/// <summary>An outfit set the dresser could keep as one: its item, and the loose pieces of it in the dresser.</summary>
/// <param name="Stored">The set's own item is in the dresser already (the pieces would join it).</param>
/// <param name="FromBags">Pieces of a new set that are in your bags (they go in with it).</param>
public sealed record OutfitChance(uint SetItem, IReadOnlyList<DresserPiece> Pieces, bool Stored, int FromBags = 0)
{
    /// <summary>Dresser places it would free: every loose piece's (a new set takes one place of its own).</summary>
    public int Frees => Stored ? Pieces.Count : Math.Max(0, Pieces.Count - 1);
}

/// <summary>
/// Making room in the glamour dresser (800 places): how full it is, the outfit sets whose loose
/// pieces could be kept as one (each set one place), and the pieces no look or plate of yours uses.
/// </summary>
public static class DresserCleanup
{
    /// <summary>The dresser's places.</summary>
    public const int Capacity = 800;

    /// <summary>Places used: a set's pieces share their set's place.</summary>
    public static int Used(IEnumerable<DresserPiece> dresser) => dresser.Select(d => d.Index).Distinct().Count();

    /// <summary>
    /// Sets the dresser's loose pieces could be gathered into, the most places freed first: a stored
    /// set with one loose piece or more it lacks; a new set with two pieces or more, loose or in
    /// <paramref name="inBags"/> (those go in with it: a set's pieces left in your bags are put away). Only a normal-quality undyed loose copy is used: the
    /// game refused a set of high-quality pieces every time, and a set may not keep a piece's dyes.
    /// <paramref name="sets"/>: each set item and its pieces' items.
    /// </summary>
    public static List<OutfitChance> Outfits(IReadOnlyList<DresserPiece> dresser, IReadOnlyDictionary<uint, uint[]> sets, IReadOnlySet<uint>? inBags = null) =>
        Gather(dresser, sets, inBags).Chances;

    /// <summary>As <see cref="Outfits"/>, with how many sets are left only for want of a normal-quality undyed copy.</summary>
    public static (List<OutfitChance> Chances, int HeldBack) Gather(IReadOnlyList<DresserPiece> dresser, IReadOnlyDictionary<uint, uint[]> sets, IReadOnlySet<uint>? inBags = null)
    {
        var looseAll = dresser.Where(d => !d.InSet).ToList();
        var usable = looseAll.Where(d => d.Item < LookJson.HqOffset && d.Dye1 == 0 && d.Dye2 == 0)
            .GroupBy(d => LookJson.BaseId(d.Item)).ToDictionary(g => g.Key, g => g.First());
        var anyLoose = looseAll.Select(d => LookJson.BaseId(d.Item)).ToHashSet();
        // What each stored set has in it already: a loose copy of one of those wouldn't join it.
        var inSets = dresser.Where(d => d.InSet).GroupBy(d => LookJson.BaseId(d.SetItem))
            .ToDictionary(g => g.Key, g => g.Select(d => LookJson.BaseId(d.Item)).ToHashSet());
        var chances = new List<OutfitChance>();
        var heldBack = 0;
        foreach (var (set, pieces) in sets)
        {
            var ids = pieces.Select(LookJson.BaseId).Distinct().ToList();
            if (inSets.TryGetValue(LookJson.BaseId(set), out var already))
            {
                var join = ids.Where(i => usable.ContainsKey(i) && !already.Contains(i)).Select(i => usable[i]).ToList();
                if (join.Count >= 1) chances.Add(new OutfitChance(set, join, true));
                continue;
            }
            var have = ids.Where(usable.ContainsKey).Select(i => usable[i]).ToList();
            var fromBags = ids.Count(i => !usable.ContainsKey(i) && (inBags?.Contains(i) ?? false));
            // (Pieces of it in your bags, from a run that stopped, are put away as the set too.)
            if (have.Count >= 2 || have.Count + fromBags >= 2 && fromBags > 0)
                chances.Add(new OutfitChance(set, have, false, fromBags));
            else if (ids.Count(anyLoose.Contains) >= 2)
                heldBack++;
        }
        return (chances.OrderByDescending(c => c.Frees).ThenBy(c => c.SetItem).ToList(), heldBack);
    }

    /// <summary>
    /// The places gathering the sets would free in all, in their order: a loose piece two sets share
    /// goes to the first (stored in one set, it's gone from the dresser's loose pieces), and a new set
    /// that then lacks it can't be made.
    /// </summary>
    public static int Frees(IEnumerable<OutfitChance> chances)
    {
        var taken = new HashSet<int>();
        var total = 0;
        foreach (var c in chances)
        {
            if (!c.Stored && c.Pieces.Any(p => taken.Contains(p.Index))) continue;
            var fresh = c.Pieces.Count(p => taken.Add(p.Index));
            total += Math.Max(0, c.Stored ? fresh : fresh - 1);
        }
        return total;
    }

    /// <summary>
    /// The dresser's duplicates: a second (third…) loose copy of an item with the same dyes as one
    /// kept, and an undyed loose copy of a piece a stored outfit set already holds. Copies dyed
    /// otherwise aren't duplicates (a plate takes the copy whose dyes are closest), nor are a set's own
    /// pieces. In the dresser's order; the first copy of each is the one kept.
    /// </summary>
    public static List<DresserPiece> Duplicates(IReadOnlyList<DresserPiece> dresser)
    {
        var inSets = dresser.Where(d => d.InSet).Select(d => LookJson.BaseId(d.Item)).ToHashSet();
        var kept = new HashSet<(uint, byte, byte)>();
        var extra = new List<DresserPiece>();
        foreach (var d in dresser.Where(d => !d.InSet).GroupBy(d => d.Index).Select(g => g.First()).OrderBy(d => d.Index))
        {
            var id = LookJson.BaseId(d.Item);
            if (d.Dye1 == 0 && d.Dye2 == 0 && inSets.Contains(id)) extra.Add(d);
            else if (!kept.Add((id, d.Dye1, d.Dye2))) extra.Add(d);
        }
        return extra;
    }

    /// <summary>The dresser's loose pieces that none of <paramref name="outfits"/> (your looks and plates) has, in the dresser's order.</summary>
    public static List<DresserPiece> Unused(IReadOnlyList<DresserPiece> dresser, IEnumerable<IReadOnlyDictionary<Slot, Piece>> outfits)
    {
        var used = outfits.SelectMany(o => o.Values).Select(p => LookJson.BaseId(p.Item)).ToHashSet();
        return dresser.Where(d => !d.InSet && !used.Contains(LookJson.BaseId(d.Item))).GroupBy(d => d.Index).Select(g => g.First()).OrderBy(d => d.Index).ToList();
    }
}

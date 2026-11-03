using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourStudio.Core;

/// <summary>An outfit set the dresser could keep as one: its item, and the loose pieces of it in the dresser.</summary>
/// <param name="Stored">The set's own item is in the dresser already (the pieces would join it).</param>
public sealed record OutfitChance(uint SetItem, IReadOnlyList<DresserPiece> Pieces, bool Stored)
{
    /// <summary>Dresser places it would free: every loose piece's (a new set takes one place of its own).</summary>
    public int Frees => Stored ? Pieces.Count : Pieces.Count - 1;
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
    /// Sets with at least two loose pieces in the dresser (or one, when the set itself is stored), the
    /// most places freed first. <paramref name="sets"/>: each set item and its pieces' items.
    /// </summary>
    public static List<OutfitChance> Outfits(IReadOnlyList<DresserPiece> dresser, IReadOnlyDictionary<uint, uint[]> sets)
    {
        var loose = dresser.Where(d => !d.InSet).GroupBy(d => LookJson.BaseId(d.Item)).ToDictionary(g => g.Key, g => g.First());
        // What each stored set has in it already: a loose copy of one of those wouldn't join it.
        var inSets = dresser.Where(d => d.InSet).GroupBy(d => LookJson.BaseId(d.SetItem))
            .ToDictionary(g => g.Key, g => g.Select(d => LookJson.BaseId(d.Item)).ToHashSet());
        var chances = new List<OutfitChance>();
        foreach (var (set, pieces) in sets)
        {
            var stored = inSets.TryGetValue(LookJson.BaseId(set), out var already);
            var have = pieces.Select(LookJson.BaseId).Distinct()
                .Where(i => loose.ContainsKey(i) && !(already?.Contains(i) ?? false))
                .Select(i => loose[i]).ToList();
            if (have.Count >= (stored ? 1 : 2)) chances.Add(new OutfitChance(set, have, stored));
        }
        return chances.OrderByDescending(c => c.Frees).ThenBy(c => c.SetItem).ToList();
    }

    /// <summary>
    /// The places gathering the sets would free in all, in their order: a loose piece two sets share
    /// is counted for the first only (stored in one set, it's gone from the dresser's loose pieces).
    /// </summary>
    public static int Frees(IEnumerable<OutfitChance> chances)
    {
        var taken = new HashSet<int>();
        var total = 0;
        foreach (var c in chances)
        {
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

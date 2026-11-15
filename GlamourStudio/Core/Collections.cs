using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GlamourStudio.Core;

/// <summary>
/// What a look has besides the plate's twelve: its facewear and its fashion accessory, each kept by
/// the item that unlocks it (a real item id, as the site keeps them: the closet, the wishlist and
/// "where to get it" work as for gear). Plates don't hold either; Wear puts them on with the game's
/// <c>/facewear</c> and <c>/fashion</c>.
/// </summary>
/// <param name="Face">The facewear style's unlock item ("The Faces We Wear - Oval Spectacles"), 0 for none.</param>
/// <param name="Colour">The facewear's colour, 1 to 12 (1 undyed, 2 silver … 12 brown: the same twelve for every style).</param>
/// <param name="Fashion">The fashion accessory's unlock item ("Parasol"), 0 for none.</param>
public readonly record struct Extras(uint Face = 0, byte Colour = 0, uint Fashion = 0)
{
    [JsonIgnore]
    public bool HasFace => Face != 0 && Colour is >= 1 and <= Collections.Colours;

    [JsonIgnore]
    public bool HasFashion => Fashion != 0;

    [JsonIgnore]
    public bool Any => HasFace || HasFashion;

    /// <summary>The same with only what's sound kept (a facewear with no colour is none).</summary>
    [JsonIgnore]
    public Extras Clean => new(HasFace ? Face : 0, HasFace ? Colour : (byte)0, Fashion);
}

/// <summary>One colour of a facewear style, as the game's Glasses sheet has it.</summary>
/// <param name="Model">Packed as gear's: the low 16 bits the model set (5501), the next 16 the colour's variant.</param>
public readonly record struct GlassesRow(uint Id, uint Style, uint Model, string Name, uint Icon);

/// <summary>A facewear style (GlassesStyle): its name, icon and its colours' Glasses rows (twelve; 0 where there's none).</summary>
public readonly record struct GlassesStyleRow(uint Id, string Name, uint Icon, uint[] Glasses);

/// <summary>A fashion accessory (Ornament): its name (the sheet's Singular) and icon.</summary>
public readonly record struct OrnamentRow(uint Id, string Name, uint Icon);

/// <summary>
/// An item that unlocks something when used: its ItemAction's type (<c>Action</c>: 37312 a facewear
/// style, 20086 a fashion accessory), the action's first number (<c>Data[0]</c>: the Ornament row) and
/// the item's <c>AdditionalData</c> (a facewear style's first Glasses row).
/// </summary>
public readonly record struct UnlockItemRow(uint Item, uint Action, uint Data0, uint AdditionalData);

/// <summary>A facewear style as the plugin knows it: the style, the item that unlocks it, its name and icon, and its colours.</summary>
public sealed record FaceStyle(uint Style, uint Item, string Name, uint Icon, uint[] Glasses);

/// <summary>A fashion accessory as the plugin knows it: the Ornament row, its unlock item, name and icon.</summary>
public sealed record FashionPiece(uint Ornament, uint Item, string Name, uint Icon);

/// <summary>
/// The game's facewear and fashion accessories, made from its sheets (built by <c>GameData</c> from
/// Glasses, GlassesStyle, Ornament and the unlock items; game-free and tested here). A worn Glasses
/// row is its style's unlock item and a colour (its place in the style's twelve, which is the colour's
/// model variant too: <c>Model &gt;&gt; 16</c>, the same for all 731 named rows, 15 November 2026); a worn
/// Ornament is its unlock item (the item whose ItemAction 20086 has the row as <c>Data[0]</c>).
/// </summary>
public sealed class Collections
{
    /// <summary>An item's ItemAction type that unlocks a facewear style (its AdditionalData: the style's first Glasses row).</summary>
    public const uint GlassesAction = 37312;

    /// <summary>An item's ItemAction type that unlocks a fashion accessory (its Data[0]: the Ornament row).</summary>
    public const uint OrnamentAction = 20086;

    /// <summary>How many colours every facewear style has.</summary>
    public const int Colours = 12;

    public static readonly Collections Empty = new(new List<FaceStyle>(), new List<FashionPiece>(), new Dictionary<uint, GlassesRow>());

    private readonly Dictionary<uint, FaceStyle> styleByItem = new();
    private readonly Dictionary<uint, FaceStyle> styleById = new();
    private readonly Dictionary<uint, FashionPiece> fashionByItem = new();
    private readonly Dictionary<uint, FashionPiece> fashionByOrnament = new();
    private readonly Dictionary<uint, GlassesRow> glasses;

    private Collections(List<FaceStyle> styles, List<FashionPiece> fashion, Dictionary<uint, GlassesRow> glasses)
    {
        Styles = styles;
        Fashion = fashion;
        this.glasses = glasses;
        foreach (var s in styles)
        {
            styleByItem.TryAdd(s.Item, s);
            styleById.TryAdd(s.Style, s);
        }
        foreach (var f in fashion)
        {
            fashionByItem.TryAdd(f.Item, f);
            fashionByOrnament.TryAdd(f.Ornament, f);
        }
    }

    /// <summary>Every facewear style that has an unlock item, in the sheet's order.</summary>
    public IReadOnlyList<FaceStyle> Styles { get; }

    /// <summary>Every fashion accessory that has an unlock item, in the sheet's order.</summary>
    public IReadOnlyList<FashionPiece> Fashion { get; }

    public bool Known => Styles.Count > 0 || Fashion.Count > 0;

    /// <summary>
    /// Made from the sheets' rows. A style or accessory no item unlocks is left out (GlassesStyle 34 is
    /// a placeholder; Ornament 0, 21 and 29 are empty); so is an unlock item whose row isn't there.
    /// </summary>
    public static Collections Build(IEnumerable<GlassesStyleRow> styles, IEnumerable<GlassesRow> glassesRows, IEnumerable<OrnamentRow> ornaments,
        IEnumerable<UnlockItemRow> items)
    {
        var glasses = new Dictionary<uint, GlassesRow>();
        foreach (var g in glassesRows)
            if (g.Id != 0)
                glasses.TryAdd(g.Id, g);
        var styleRows = new Dictionary<uint, GlassesStyleRow>();
        var styleOrder = new List<uint>();
        foreach (var s in styles)
            if (styleRows.TryAdd(s.Id, s))
                styleOrder.Add(s.Id);
        var ornamentRows = new Dictionary<uint, OrnamentRow>();
        var ornamentOrder = new List<uint>();
        foreach (var o in ornaments)
            if (o.Id != 0 && ornamentRows.TryAdd(o.Id, o))
                ornamentOrder.Add(o.Id);

        // Each style's and accessory's unlock item (the first, should two unlock the same).
        var styleItem = new Dictionary<uint, uint>();
        var ornamentItem = new Dictionary<uint, uint>();
        foreach (var it in items.OrderBy(i => i.Item))
        {
            if (it.Item == 0) continue;
            if (it.Action == GlassesAction && glasses.TryGetValue(it.AdditionalData, out var first) && styleRows.ContainsKey(first.Style))
                styleItem.TryAdd(first.Style, it.Item);
            else if (it.Action == OrnamentAction && ornamentRows.ContainsKey(it.Data0))
                ornamentItem.TryAdd(it.Data0, it.Item);
        }

        var faceList = new List<FaceStyle>();
        foreach (var id in styleOrder)
        {
            if (!styleItem.TryGetValue(id, out var item)) continue;
            var row = styleRows[id];
            var colours = new uint[Colours];
            for (var i = 0; i < Colours && i < row.Glasses.Length; i++)
                colours[i] = glasses.ContainsKey(row.Glasses[i]) ? row.Glasses[i] : 0;
            var name = row.Name.Length > 0 ? row.Name : colours[0] != 0 ? glasses[colours[0]].Name : $"Facewear {id}";
            faceList.Add(new FaceStyle(id, item, name, row.Icon, colours));
        }
        var fashionList = new List<FashionPiece>();
        foreach (var id in ornamentOrder)
        {
            if (!ornamentItem.TryGetValue(id, out var item)) continue;
            var row = ornamentRows[id];
            fashionList.Add(new FashionPiece(id, item, row.Name.Length > 0 ? row.Name : $"Fashion accessory {id}", row.Icon));
        }
        return new Collections(faceList, fashionList, glasses);
    }

    /// <summary>The facewear a worn Glasses row is (its style's unlock item and its colour), or null for none or one not known.</summary>
    public (uint Item, byte Colour)? FaceOf(uint glassesId)
    {
        if (glassesId == 0 || !glasses.TryGetValue(glassesId, out var row) || !styleById.TryGetValue(row.Style, out var style)) return null;
        var at = Array.IndexOf(style.Glasses, glassesId);
        // (Its place among the style's twelve; else the colour's model variant, which is the same.)
        var colour = at >= 0 ? at + 1 : (int)(row.Model >> 16);
        if (colour is < 1 or > Colours) return null;
        return (style.Item, (byte)colour);
    }

    /// <summary>The Glasses row of a facewear style's colour, or 0.</summary>
    public uint GlassesOf(uint item, byte colour) =>
        colour is >= 1 and <= Colours && styleByItem.TryGetValue(item, out var s) ? s.Glasses[colour - 1] : 0;

    /// <summary>The unlock item of a worn Ornament row, or 0.</summary>
    public uint FashionOf(uint ornamentId) => fashionByOrnament.TryGetValue(ornamentId, out var f) ? f.Item : 0;

    /// <summary>The Ornament row an unlock item gives, or 0.</summary>
    public uint OrnamentOf(uint item) => fashionByItem.TryGetValue(item, out var f) ? f.Ornament : 0;

    /// <summary>What someone wears, as a look keeps it (unknown rows are none).</summary>
    public Extras Worn(uint glassesId, uint ornamentId)
    {
        var face = FaceOf(glassesId);
        return new Extras(face?.Item ?? 0, face?.Colour ?? 0, FashionOf(ornamentId));
    }

    public FaceStyle? Style(uint item) => styleByItem.TryGetValue(item, out var s) ? s : null;

    public FashionPiece? Accessory(uint item) => fashionByItem.TryGetValue(item, out var f) ? f : null;

    /// <summary>
    /// A facewear colour's name as the game has it ("Silver Oval Spectacles"; the first colour is the
    /// style's own name), the one <c>/facewear</c> takes; null when it isn't known.
    /// </summary>
    public string? FaceName(uint item, byte colour)
    {
        var g = GlassesOf(item, colour);
        if (g != 0 && glasses.TryGetValue(g, out var row) && row.Name.Length > 0) return row.Name;
        return Style(item)?.Name;
    }

    /// <summary>A facewear colour's icon (the style's when the colour has none).</summary>
    public uint FaceIcon(uint item, byte colour)
    {
        var g = GlassesOf(item, colour);
        if (g != 0 && glasses.TryGetValue(g, out var row) && row.Icon != 0) return row.Icon;
        return Style(item)?.Icon ?? 0;
    }

    /// <summary>
    /// The unlock items of what's unlocked, asked of the game by the style's and the accessory's own
    /// rows (the game keeps a facewear style unlocked with all twelve colours).
    /// </summary>
    public HashSet<uint> Unlocked(Func<uint, bool> styleUnlocked, Func<uint, bool> ornamentUnlocked)
    {
        var items = new HashSet<uint>();
        foreach (var s in Styles)
            if (styleUnlocked(s.Style))
                items.Add(s.Item);
        foreach (var f in Fashion)
            if (ornamentUnlocked(f.Ornament))
                items.Add(f.Item);
        return items;
    }

    /// <summary>The twelve colours' names, as the facewear's descriptions give them ("1 undyed, 2 silver …").</summary>
    public static string ColourName(byte colour) => colour switch
    {
        1 => "Undyed",
        2 => "Silver",
        3 => "Gold",
        4 => "Black",
        5 => "White",
        6 => "Grey",
        7 => "Red",
        8 => "Blue",
        9 => "Green",
        10 => "Brass",
        11 => "Purple",
        12 => "Brown",
        _ => "No colour",
    };
}

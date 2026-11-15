using System.Text.Json;
using System.Text.Json.Nodes;
using GlamourStudio.Core;
using Xunit;

namespace GlamourStudio.Tests;

/// <summary>
/// Facewear and fashion accessories: the game's sheets as rows (a few real ones, as XIVAPI has them on
/// 15 November 2026), mapped to their unlock items and colours, and what a look and a sync carry.
/// </summary>
public class CollectionsTests
{
    private static readonly string[] Colours =
        { "", "Silver ", "Gold ", "Black ", "White ", "Grey ", "Red ", "Blue ", "Green ", "Brass ", "Purple ", "Brown " };

    /// <summary>A style's twelve Glasses rows from <paramref name="first"/>, model set <paramref name="set"/> with the colour as its variant.</summary>
    private static IEnumerable<GlassesRow> Colourways(uint style, uint first, uint set, string name) =>
        Enumerable.Range(0, 12).Select(i => new GlassesRow(first + (uint)i, style, set | ((uint)(i + 1) << 16), Colours[i] + name, 200049 + (uint)i));

    private static Collections Sample()
    {
        var glasses = Colourways(0, 1, 5501, "Oval Spectacles")
            .Concat(Colourways(1, 13, 5502, "Shaded Spectacles"))
            // GlassesStyle 34: a placeholder, its rows with no model and no names, no item unlocks it.
            .Concat(Enumerable.Range(0, 12).Select(i => new GlassesRow(400 + (uint)i, 34, 0, string.Empty, 0)))
            .ToList();
        var styles = new[]
        {
            new GlassesStyleRow(0, "Oval Spectacles", 200049, Enumerable.Range(1, 12).Select(i => (uint)i).ToArray()),
            new GlassesStyleRow(1, "Shaded Spectacles", 200061, Enumerable.Range(13, 12).Select(i => (uint)i).ToArray()),
            new GlassesStyleRow(34, "●RTI2シリーズ（キャスター）", 0, Enumerable.Range(400, 12).Select(i => (uint)i).ToArray()),
        };
        var ornaments = new[]
        {
            new OrnamentRow(1, "Parasol", 8001),
            new OrnamentRow(2, "Sky Blue Parasol", 8002),
            new OrnamentRow(21, string.Empty, 0),
            new OrnamentRow(58, "Antique Lantern", 8058),
        };
        var items = new[]
        {
            new UnlockItemRow(44259, Collections.GlassesAction, 0, 1),   // The Faces We Wear - Oval Spectacles
            new UnlockItemRow(44260, Collections.GlassesAction, 0, 13),  // … - Shaded Spectacles
            new UnlockItemRow(30269, Collections.OrnamentAction, 1, 0),  // Parasol
            new UnlockItemRow(30270, Collections.OrnamentAction, 2, 0),  // Sky Blue Parasol
            new UnlockItemRow(52289, Collections.OrnamentAction, 58, 0), // Antique Lantern
            new UnlockItemRow(6000, 853, 1, 0),                         // something else an item does
            new UnlockItemRow(99999, Collections.OrnamentAction, 77, 0), // an accessory the sheet hasn't
        };
        return Collections.Build(styles, glasses, ornaments, items);
    }

    [Fact]
    public void KnowsEachStyleAndAccessoryByItsUnlockItem()
    {
        var c = Sample();
        Assert.Equal(new uint[] { 44259, 44260 }, c.Styles.Select(s => s.Item));
        Assert.Equal(new uint[] { 30269, 30270, 52289 }, c.Fashion.Select(f => f.Item));
        Assert.Equal("Oval Spectacles", c.Style(44259)!.Name);
        Assert.Null(c.Style(30269));
        Assert.Equal("Parasol", c.Accessory(30269)!.Name);
        Assert.Equal(58u, c.OrnamentOf(52289));
        Assert.Equal(0u, c.OrnamentOf(44259));
        Assert.True(c.Known);
        Assert.False(Collections.Empty.Known);
    }

    [Fact]
    public void AWornGlassesRowIsItsStylesItemAndColour()
    {
        var c = Sample();
        Assert.Equal((44259u, (byte)1), c.FaceOf(1));
        Assert.Equal((44259u, (byte)2), c.FaceOf(2));
        Assert.Equal((44260u, (byte)12), c.FaceOf(24));
        Assert.Null(c.FaceOf(0));
        Assert.Null(c.FaceOf(400));
        Assert.Null(c.FaceOf(9999));
        // The colour is the place among the style's twelve, which is the model's variant too.
        for (uint g = 1; g <= 24; g++)
        {
            var (item, colour) = c.FaceOf(g)!.Value;
            Assert.Equal(g, c.GlassesOf(item, colour));
            Assert.Equal((g - 1) % 12 + 1, colour);
        }
        Assert.Equal(0u, c.GlassesOf(44259, 0));
        Assert.Equal(0u, c.GlassesOf(44259, 13));
        Assert.Equal(0u, c.GlassesOf(30269, 1));
    }

    [Fact]
    public void AColourOutOfItsStylesListFallsBackOnTheModelsVariant()
    {
        var glasses = new[] { new GlassesRow(7, 5, 5530 | (4u << 16), "Black Monocle", 1) };
        var styles = new[] { new GlassesStyleRow(5, "Monocle", 1, new uint[12]) };
        var c = Collections.Build(styles, glasses, Array.Empty<OrnamentRow>(), new[] { new UnlockItemRow(50000, Collections.GlassesAction, 0, 7) });
        Assert.Equal((50000u, (byte)4), c.FaceOf(7));
    }

    [Fact]
    public void WornIsWhatALookKeeps()
    {
        var c = Sample();
        Assert.Equal(new Extras(44260, 3, 30270), c.Worn(15, 2));
        Assert.Equal(new Extras(0, 0, 30269), c.Worn(0, 1));
        Assert.Equal(default, c.Worn(0, 0));
        Assert.Equal(default, c.Worn(400, 21));
    }

    [Fact]
    public void NamesAreTheColoursOwnForTheCommand()
    {
        var c = Sample();
        Assert.Equal("Oval Spectacles", c.FaceName(44259, 1));
        Assert.Equal("Silver Oval Spectacles", c.FaceName(44259, 2));
        Assert.Equal("Brown Shaded Spectacles", c.FaceName(44260, 12));
        Assert.Equal(200050u, c.FaceIcon(44259, 2));
        Assert.Null(c.FaceName(30269, 1));
        Assert.Equal("Silver", Collections.ColourName(2));
        Assert.Equal("Brown", Collections.ColourName(12));
    }

    [Fact]
    public void UnlockedAreTheUnlockItems()
    {
        var c = Sample();
        var items = c.Unlocked(style => style == 1, ornament => ornament is 1 or 58);
        Assert.Equal(new HashSet<uint> { 44260, 30269, 52289 }, items);
        Assert.Empty(c.Unlocked(_ => false, _ => false));
    }

    [Fact]
    public void ExtrasAreSoundOnlyWithAColour()
    {
        Assert.False(default(Extras).Any);
        Assert.True(new Extras(44259, 2).HasFace);
        Assert.False(new Extras(44259, 0).HasFace);
        Assert.False(new Extras(44259, 13).HasFace);
        Assert.Equal(new Extras(0, 0, 30269), new Extras(44259, 0, 30269).Clean);
        Assert.True(new Extras(Fashion: 30269).Any);
    }
}

public class ExtrasJsonTests
{
    private const string Id = "6168b2b8-f0fe-4c50-b1a0-8748b5235533";

    [Fact]
    public void WritesFaceAndFashionBesideTheSlots()
    {
        var o = LookJson.Write(new Dictionary<Slot, Piece> { [Slot.Body] = new(100, 5) }, new Extras(44260, 2, 30269));
        Assert.Equal("""{"item":100,"dyes":[5,0]}""", o["body"]!.ToJsonString());
        Assert.Equal("""{"item":44260,"colour":2,"dyes":[0,0]}""", o["face"]!.ToJsonString());
        Assert.Equal("""{"item":30269,"dyes":[0,0]}""", o["fashion"]!.ToJsonString());
        // None of either: the keys aren't there (an older site never sees them).
        var bare = LookJson.Write(new Dictionary<Slot, Piece> { [Slot.Body] = new(100) });
        Assert.False(bare.ContainsKey("face"));
        Assert.False(bare.ContainsKey("fashion"));
        Assert.False(LookJson.Write(new Dictionary<Slot, Piece>(), new Extras(44260, 0)).ContainsKey("face"));
    }

    [Fact]
    public void ReadsThemBackAndLeavesThemOutOfTheTwelve()
    {
        var node = JsonNode.Parse("""{"body":{"item":100,"dyes":[5,0]},"face":{"item":44260,"colour":7,"dyes":[0,0]},"fashion":{"item":"30269"}}""");
        Assert.Single(LookJson.ReadPieces(node));
        Assert.Equal(new Extras(44260, 7, 30269), LookJson.ReadExtras(node));
        // No colour (or one past the twelve): the first; an item it can't read: none.
        Assert.Equal(new Extras(44260, 1), LookJson.ReadExtras(JsonNode.Parse("""{"face":{"item":44260}}""")));
        Assert.Equal(new Extras(44260, 1), LookJson.ReadExtras(JsonNode.Parse("""{"face":{"item":44260,"colour":13}}""")));
        Assert.Equal(default, LookJson.ReadExtras(JsonNode.Parse("""{"face":{"item":-1},"fashion":{"item":"x"}}""")));
        Assert.Equal(default, LookJson.ReadExtras(null));
    }

    [Fact]
    public void ALookCarriesThem()
    {
        var look = LookJson.ReadLook(JsonNode.Parse("""{"id":"ID","name":"Rainy day","pieces":{"head":{"item":5},"fashion":{"item":30270}}}""".Replace("ID", Id)))!;
        Assert.Equal(new Extras(Fashion: 30270), look.Extras);
        Assert.Equal(1, look.Count);
        var plain = LookJson.ReadLook(JsonNode.Parse("""{"id":"ID","pieces":{"head":{"item":5}}}""".Replace("ID", Id)))!;
        Assert.False(plain.Extras.Any);
    }
}

public class UnlockedPlaceTests
{
    [Fact]
    public void UnlockedIsTheHandiestPlace()
    {
        Assert.Equal(Place.Unlocked, Places.ByPriority[0]);
        Assert.Equal("unlocked", Places.Key(Place.Unlocked));
        Assert.Equal(7, Places.ByPriority.Length);
        Assert.Equal(Places.ByPriority.Length, Places.ByPriority.Distinct().Count());
    }

    [Fact]
    public void TheClosetSendsTheUnlockItemsAndTheOutfitItsExtras()
    {
        var me = new CharacterWardrobe { Key = "k1", Name = "Koko Puffs", World = "Lich", OutfitReadAt = DateTime.UtcNow };
        me.Read[Place.Unlocked] = new PlaceRead { ReadAt = DateTime.UtcNow, Items = new HashSet<uint> { 44260, 30269 } };
        me.Read[Place.Armoury] = new PlaceRead { ReadAt = DateTime.UtcNow, Items = new HashSet<uint> { 100 } };
        me.Outfit[Slot.Body] = new Piece(100);
        var (closet, read) = Wardrobe.Closet(new[] { me });
        Assert.Equal(new[] { Place.Unlocked, Place.Armoury }, read);
        Assert.Equal((44260u, Place.Unlocked), closet.Single(p => p.Item == 44260));
        Assert.Equal(Place.Unlocked, me.Where(30269));

        var before = Wardrobe.Fingerprint(closet, read, me);
        me.OutfitExtras = new Extras(44260, 2);
        Assert.NotEqual(before, Wardrobe.Fingerprint(closet, read, me));

        var args = Wardrobe.SyncArgs(closet, read, me);
        Assert.Contains("unlocked", args["p_places"]!.AsArray().Select(n => (string)n!));
        Assert.Contains("""{"item":30269,"place":"unlocked"}""", args["p_closet"]!.ToJsonString());
        Assert.Equal("""{"item":44260,"colour":2,"dyes":[0,0]}""", args["p_outfit"]!["pieces"]!["face"]!.ToJsonString());
    }

    [Fact]
    public void TheWardrobeFileKeepsThem()
    {
        var store = new Wardrobe();
        var me = new CharacterWardrobe { Key = "k1", OutfitExtras = new Extras(44259, 5, 52289) };
        me.Read[Place.Unlocked] = new PlaceRead { ReadAt = DateTime.UtcNow, Items = new HashSet<uint> { 44259 } };
        store.Characters["k1"] = me;
        var json = JsonSerializer.Serialize(store);
        Assert.Contains("\"OutfitExtras\":{\"Face\":44259,\"Colour\":5,\"Fashion\":52289}", json);
        var back = JsonSerializer.Deserialize<Wardrobe>(json)!;
        Assert.Equal(new Extras(44259, 5, 52289), back.Characters["k1"].OutfitExtras);
        Assert.Contains(44259u, back.Characters["k1"].Read[Place.Unlocked].Items);
        // An older file (no extras) reads as none.
        var old = JsonSerializer.Deserialize<CharacterWardrobe>("""{"Key":"k2","Outfit":{}}""")!;
        Assert.False(old.OutfitExtras.Any);
    }

    [Fact]
    public void TheSlipAndWearSayWhatPlatesCantHold()
    {
        Assert.Null(Words.NotOnPlates(default));
        Assert.Contains("facewear and fashion accessory", Words.NotOnPlates(new Extras(1, 2, 3)));
        Assert.StartsWith("Its facewear doesn't", Words.NotOnPlates(new Extras(1, 2)));
        Assert.StartsWith("Its fashion accessory doesn't", Words.NotOnPlates(new Extras(Fashion: 3)));
        Assert.Null(Words.ExtrasOutcome(Array.Empty<string>(), Array.Empty<string>()));
        Assert.Equal("Parasol isn't unlocked on this character yet", Words.ExtrasOutcome(Array.Empty<string>(), new[] { "Parasol" }));
        Assert.StartsWith("The game didn't put on Silver Oval Spectacles and Parasol", Words.ExtrasOutcome(new[] { "Silver Oval Spectacles", "Parasol" }, Array.Empty<string>()));
    }
}

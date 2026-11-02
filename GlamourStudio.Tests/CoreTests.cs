using System.Net;
using System.Text.Json.Nodes;
using GlamourStudio.Core;
using Xunit;

namespace GlamourStudio.Tests;

public class SlotTests
{
    [Fact]
    public void PlateOrderIsTheGearWindows()
    {
        Assert.Equal(12, Slots.PlateOrder.Length);
        Assert.Equal(Slot.Main, Slots.FromPlateIndex(0));
        Assert.Equal(Slot.Head, Slots.FromPlateIndex(2));
        Assert.Equal(Slot.RingL, Slots.FromPlateIndex(11));
        Assert.Null(Slots.FromPlateIndex(12));
        foreach (var s in Slots.All) Assert.Equal(s, Slots.FromPlateIndex(Slots.PlateIndex(s)));
    }

    [Fact]
    public void WhatYouWearSkipsTheWaistAndTheSoulCrystal()
    {
        Assert.Equal(Slot.Hands, Slots.FromEquippedIndex(4));
        Assert.Null(Slots.FromEquippedIndex(5));
        Assert.Equal(Slot.Legs, Slots.FromEquippedIndex(6));
        Assert.Equal(Slot.RingL, Slots.FromEquippedIndex(12));
        Assert.Null(Slots.FromEquippedIndex(13));
    }

    [Fact]
    public void KeysAreTheSites()
    {
        foreach (var s in Slots.All)
        {
            Assert.True(Slots.TryParse(Slots.Key(s), out var back));
            Assert.Equal(s, back);
        }
        Assert.False(Slots.TryParse("waist", out _));
        Assert.Equal("ringR", Slots.Key(Slot.RingR));
        Assert.Equal(new[] { Slot.RingR, Slot.RingL }, Slots.OfCategory(false, false, false, false, false, false, false, false, false, false, true));
    }
}

public class LookJsonTests
{
    private const string Id = "6168b2b8-f0fe-4c50-b1a0-8748b5235533";

    [Fact]
    public void ReadsPiecesAndLeavesOutWhatItCant()
    {
        var node = JsonNode.Parse("""{"body":{"item":100,"dyes":[5,0]},"head":{"item":"200"},"waist":{"item":3},"legs":{"item":-1},"feet":{"item":7,"dyes":[999,2]}}""");
        var pieces = LookJson.ReadPieces(node);
        Assert.Equal(3, pieces.Count);
        Assert.Equal(new Piece(100, 5, 0), pieces[Slot.Body]);
        Assert.Equal(new Piece(200), pieces[Slot.Head]);
        Assert.Equal(new Piece(7, 0, 2), pieces[Slot.Feet]);
    }

    [Fact]
    public void WritesTheSitesShapeWithNormalQualityIds()
    {
        var o = LookJson.Write(new Dictionary<Slot, Piece> { [Slot.RingL] = new(1_000_042, 3, 4) });
        Assert.Equal("""{"ringL":{"item":42,"dyes":[3,4]}}""", o.ToJsonString());
        Assert.Equal(42u, LookJson.BaseId(1_000_042));
        Assert.Equal(42u, LookJson.BaseId(500_042));
    }

    [Fact]
    public void ReadsTheLibraryAndTheInbox()
    {
        var lib = LookJson.ReadLibrary(JsonNode.Parse("""
            {"mine":[{"id":"ID","name":"  Winter   Court ","body":"masculine","pieces":{"head":{"item":1}},"mine":true,"maker":"Koko","loves":3}],
             "loved":[{"id":"nope"}],"wishes":[5,"x",6]}
            """.Replace("ID", Id)));
        Assert.NotNull(lib);
        var look = Assert.Single(lib!.Mine);
        Assert.Equal("Winter Court", look.Name);
        Assert.Equal(Body.Masculine, look.Body);
        Assert.True(look.Mine);
        Assert.Empty(lib.Loved);
        Assert.Equal(new HashSet<uint> { 5, 6 }, lib.Wishes);

        var inbox = LookJson.ReadInbox(JsonNode.Parse("""
            [{"id":"ID","plate":3,"created_at":"2026-11-02T03:00:00Z","look":{"id":"ID","name":"A","pieces":{}}},
             {"id":"ID","plate":30,"created_at":"2026-11-02T04:00:00Z","look":{"id":"ID","name":"B"}},
             {"id":"bad","look":{"id":"ID"}}]
            """.Replace("ID", Id)));
        Assert.NotNull(inbox);
        Assert.Equal(new[] { "B", "A" }, inbox!.Select(s => s.Look.Name));
        Assert.Null(inbox[0].Plate);
        Assert.Equal(3, inbox[1].Plate);
        Assert.Null(LookJson.ReadInbox(JsonNode.Parse("{}")));
    }

    [Fact]
    public void FindsTheLookAPlateIs()
    {
        var pieces = new Dictionary<Slot, Piece> { [Slot.Body] = new(10, 1, 0), [Slot.Head] = new(11) };
        var look = new Look(Id, "Same", Body.Feminine, new Dictionary<Slot, Piece> { [Slot.Body] = new(1_000_010, 1, 0), [Slot.Head] = new(11) }, "private", true, "Me", 0, null);
        var other = look with { Name = "Other", Pieces = new Dictionary<Slot, Piece> { [Slot.Body] = new(10, 2, 0), [Slot.Head] = new(11) } };
        Assert.Equal("Same", Plates.Matching(pieces, new[] { other, look })?.Name);
        Assert.Null(Plates.Matching(new Dictionary<Slot, Piece>(), new[] { look }));
    }
}

public class WardrobeTests
{
    private static CharacterWardrobe Character(string key, params (Place Place, uint[] Items)[] places)
    {
        var c = new CharacterWardrobe { Key = key, Name = key };
        foreach (var (place, items) in places)
        {
            if (place == Place.Retainer) c.Retainers["1"] = new PlaceRead { ReadAt = DateTime.UtcNow, Items = items.ToHashSet() };
            else c.Read[place] = new PlaceRead { ReadAt = DateTime.UtcNow, Items = items.ToHashSet() };
        }
        return c;
    }

    [Fact]
    public void ClosetKeepsEachPieceOnceInItsHandiestPlace()
    {
        var a = Character("aaaaaaaaaaaaaaaaaaaaaa", (Place.Retainer, new uint[] { 1, 2 }), (Place.Dresser, new uint[] { 2, 1_000_003 }));
        var b = Character("bbbbbbbbbbbbbbbbbbbbbb", (Place.Armoire, new uint[] { 1, 4 }));
        var (pieces, read) = Wardrobe.Closet(new[] { a, b });
        Assert.Equal(new[] { (2u, Place.Dresser), (3u, Place.Dresser), (1u, Place.Armoire), (4u, Place.Armoire) }, pieces);
        Assert.Equal(new[] { Place.Dresser, Place.Armoire, Place.Retainer }, read);

        var (only, onlyRead) = Wardrobe.Closet(new[] { a, b }, b.Key);
        Assert.Equal(new[] { (1u, Place.Armoire), (4u, Place.Armoire) }, only);
        Assert.Equal(new[] { Place.Armoire }, onlyRead);
    }

    [Fact]
    public void ACharactersKeyNeverIsItsId()
    {
        var key = Wardrobe.KeyOf(18014398509481984);
        Assert.Equal(22, key.Length);
        Assert.Matches("^[A-Za-z0-9_-]{22}$", key);
        Assert.Equal(key, Wardrobe.KeyOf(18014398509481984));
        Assert.NotEqual(key, Wardrobe.KeyOf(18014398509481985));
        Assert.DoesNotContain("18014398509481984", key);
    }

    [Fact]
    public void SendsPlatesAndTheOutfitOnlyOnceRead()
    {
        var c = Character("cccccccccccccccccccccc", (Place.Inventory, new uint[] { 9 }));
        var (pieces, read) = Wardrobe.Closet(new[] { c });
        var args = Wardrobe.SyncArgs(pieces, read, c);
        Assert.Equal("""[{"item":9,"place":"inventory"}]""", args["p_closet"]!.ToJsonString());
        Assert.Equal("""["inventory"]""", args["p_places"]!.ToJsonString());
        Assert.NotNull(args["p_character"]);
        Assert.Null(args["p_plates"]);
        Assert.Null(args["p_outfit"]);

        c.PlatesReadAt = DateTime.UtcNow;
        c.Plates = new List<PlateRead>
        {
            new() { Plate = 1, Pieces = new Dictionary<Slot, Piece> { [Slot.Head] = new(5, 1, 0) } },
            new() { Plate = 2 },
        };
        c.OutfitReadAt = DateTime.UtcNow;
        c.Body = Body.Masculine;
        args = Wardrobe.SyncArgs(pieces, read, c);
        Assert.Equal("""[{"plate":1,"pieces":{"head":{"item":5,"dyes":[1,0]}}}]""", args["p_plates"]!.ToJsonString());
        Assert.Equal("masculine", args["p_outfit"]!["body"]!.GetValue<string>());
    }

    [Fact]
    public void TheFingerprintChangesWithWhatsSent()
    {
        var c = Character("dddddddddddddddddddddd", (Place.Dresser, new uint[] { 1 }));
        var (p1, r1) = Wardrobe.Closet(new[] { c });
        var f1 = Wardrobe.Fingerprint(p1, r1, c);
        Assert.Equal(f1, Wardrobe.Fingerprint(p1, r1, c));
        c.Read[Place.Dresser].Items.Add(2);
        var (p2, r2) = Wardrobe.Closet(new[] { c });
        Assert.NotEqual(f1, Wardrobe.Fingerprint(p2, r2, c));
    }

    [Fact]
    public void RetainersCountTogether()
    {
        var c = new CharacterWardrobe();
        Assert.Null(c.Get(Place.Retainer));
        c.Retainers["1"] = new PlaceRead { Items = new HashSet<uint> { 1 } };
        c.Retainers["2"] = new PlaceRead { Items = new HashSet<uint> { 2 } };
        Assert.Equal(new HashSet<uint> { 1, 2 }, c.Get(Place.Retainer)!.Items);
        Assert.Equal(Place.Retainer, c.Where(1_000_002));
        Assert.Null(c.Where(3));
    }
}

public class PlatePlanTests
{
    private static readonly IReadOnlyDictionary<uint, uint> NoArmoire = new Dictionary<uint, uint>();

    private static PlatePlan Plan(Dictionary<Slot, Piece> pieces, DresserPiece[] dresser, IReadOnlyDictionary<uint, uint>? armoire = null,
        PlateOptions? options = null, Func<uint, Place?>? elsewhere = null) =>
        PlatePlanner.Plan(pieces, dresser, armoire ?? NoArmoire, _ => 2, elsewhere ?? (_ => null), options ?? new PlateOptions());

    [Fact]
    public void TakesTheDresserCopyWhoseDyesMatch()
    {
        var plan = Plan(new() { [Slot.Body] = new(10, 5, 0) },
            new[] { new DresserPiece(3, 10, 1, 0), new DresserPiece(7, 1_000_010, 5, 0) });
        var step = plan.Steps.Single(s => s.Slot == Slot.Body);
        Assert.Equal(StepKind.Set, step.Kind);
        Assert.Equal(PlateSource.Dresser, step.Source);
        Assert.Equal(7u, step.SourceId);
        Assert.Equal(1_000_010u, step.Item);
        Assert.False(step.DyeLeft);
        Assert.Equal(1, plan.Ready);
        Assert.Equal(1, plan.Wanted);
    }

    [Fact]
    public void FallsBackToTheArmoireThenSaysWhereElseItIs()
    {
        var plan = Plan(new() { [Slot.Head] = new(20), [Slot.Feet] = new(30, 4, 0) }, Array.Empty<DresserPiece>(),
            new Dictionary<uint, uint> { [20] = 900 }, elsewhere: id => id == 30 ? Place.Retainer : null);
        var head = plan.Steps.Single(s => s.Slot == Slot.Head);
        Assert.Equal(PlateSource.Armoire, head.Source);
        Assert.Equal(900u, head.SourceId);
        var feet = plan.Steps.Single(s => s.Slot == Slot.Feet);
        Assert.Equal(StepKind.Missing, feet.Kind);
        Assert.Equal(Place.Retainer, feet.Elsewhere);
        Assert.Equal(1, plan.Ready);
    }

    [Fact]
    public void ADresserCopyFillsOneSlot()
    {
        var plan = Plan(new() { [Slot.RingR] = new(50), [Slot.RingL] = new(50) }, new[] { new DresserPiece(1, 50, 0, 0) });
        Assert.Equal(StepKind.Set, plan.Steps.Single(s => s.Slot == Slot.RingR).Kind);
        Assert.Equal(StepKind.Missing, plan.Steps.Single(s => s.Slot == Slot.RingL).Kind);
    }

    [Fact]
    public void EmptiesBareSlotsButNotWeapons()
    {
        var plan = Plan(new() { [Slot.Body] = new(10) }, new[] { new DresserPiece(0, 10, 0, 0) });
        Assert.Equal(StepKind.Clear, plan.Steps.Single(s => s.Slot == Slot.Head).Kind);
        Assert.Equal(StepKind.Keep, plan.Steps.Single(s => s.Slot == Slot.Main).Kind);
        var keep = Plan(new() { [Slot.Body] = new(10) }, new[] { new DresserPiece(0, 10, 0, 0) }, options: new PlateOptions(ClearBare: false));
        Assert.Equal(StepKind.Keep, keep.Steps.Single(s => s.Slot == Slot.Head).Kind);
    }

    [Fact]
    public void DyesFromTheBagsWhileThereAreBottles()
    {
        var bag = new Dictionary<byte, int> { [5] = 1 };
        var plan = Plan(new() { [Slot.Body] = new(10, 5, 0), [Slot.Legs] = new(11, 5, 0) },
            new[] { new DresserPiece(0, 10, 0, 0), new DresserPiece(1, 11, 0, 0) }, options: new PlateOptions(BagDyes: bag));
        var body = plan.Steps.Single(s => s.Slot == Slot.Body);
        var legs = plan.Steps.Single(s => s.Slot == Slot.Legs);
        Assert.Equal(DyeOutcome.FromBags, body.Dye1);
        Assert.Equal(DyeOutcome.Differs, legs.Dye1);
        Assert.True(legs.DyeLeft);
        Assert.Equal(new Dictionary<byte, int> { [5] = 1 }, plan.BagDyes);
        // The bag's count is the plan's own: the caller's stays as it was.
        Assert.Equal(1, bag[5]);
    }

    [Fact]
    public void ADyeAPieceCantTakeIsNoDifference()
    {
        var plan = PlatePlanner.Plan(new Dictionary<Slot, Piece> { [Slot.Body] = new(10, 5, 6) }, new[] { new DresserPiece(0, 10, 0, 0) },
            NoArmoire, _ => 0, _ => null, new PlateOptions());
        Assert.False(plan.Steps.Single(s => s.Slot == Slot.Body).DyeLeft);
    }
}

public class ReadinessTests
{
    [Fact]
    public void SaysWhatsReadyForAPlate()
    {
        var pieces = new Dictionary<Slot, Piece> { [Slot.Head] = new(1), [Slot.Body] = new(2), [Slot.Legs] = new(3) };
        var r = Readiness.Of(pieces, id => id switch { 1 => Place.Dresser, 2 => Place.Armoury, _ => null });
        Assert.Equal(new Readiness(3, 1, 1, 1), r);
        Assert.Equal("1 of 3 ready", r.Short);
        Assert.Equal(2, r.Owned);
        Assert.Equal("Ready for a plate", Readiness.Of(pieces, _ => Place.Armoire).Short);
        Assert.Equal("3 to store", Readiness.Of(pieces, _ => Place.Saddlebag).Short);
        Assert.Equal("None yours yet", Readiness.Of(pieces, _ => null).Short);
    }
}

public class SiteLinkTests
{
    private const string Token = "gsp_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ";

    [Fact]
    public void KnowsATokenWhenItSeesOne()
    {
        Assert.Equal(47, Token.Length);
        Assert.True(SiteLink.LooksRight(Token));
        Assert.True(SiteLink.LooksRight(SiteLink.Clean($"  \"{Token}\"\n")));
        Assert.False(SiteLink.LooksRight("hsp_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ"));
        Assert.False(SiteLink.LooksRight(Token + "x"));
        Assert.Equal("gsp_••••••••NOPQ", SiteLink.Masked(Token));
    }

    [Fact]
    public void SaysTheSitesRefusals()
    {
        var (why, hint) = SiteLink.Refusal(HttpStatusCode.BadRequest, """{"message":"That token was revoked, or never made. Make a new one on the site.","hint":"token","code":"28000"}""");
        Assert.Equal("token", hint);
        Assert.EndsWith("Link again in Settings with a new one.", why);
        (why, _) = SiteLink.Refusal(HttpStatusCode.NotFound, """{"message":"Could not find the function public.gs_plugin_sync","code":"PGRST202"}""");
        Assert.DoesNotContain("function", why);
        (why, hint) = SiteLink.Refusal(HttpStatusCode.BadRequest, """{"message":"That’s a lot from the plugin this hour. Try again later.","hint":"rate"}""");
        Assert.Equal("rate", hint);
        Assert.StartsWith("That’s a lot", why);
        Assert.Equal("server", SiteLink.Refusal(HttpStatusCode.BadGateway, "<html>").Hint);
    }

    [Fact]
    public void ReadsWhatASyncKept()
    {
        var r = SiteLink.ReadSync(JsonNode.Parse("""{"closet":{"total":1204,"added":12,"moved":0,"removed":2},"plates":5,"read_at":"2026-11-02T04:00:00Z"}"""));
        Assert.Equal(new SyncResult(1204, 12, 0, 2, 5, DateTimeOffset.Parse("2026-11-02T04:00:00Z")), r);
        Assert.Equal("Your closet on the site: 1,204 pieces (12 new, 2 gone)", Words.Synced(r!));
        Assert.Null(SiteLink.ReadSync(JsonNode.Parse("[]")));
    }
}

public class WordsTests
{
    [Fact]
    public void SaysTimesAndCounts()
    {
        var now = DateTimeOffset.Parse("2026-11-02T12:00:00Z");
        Assert.Equal("just now", Words.Ago(now.AddSeconds(-20), now));
        Assert.Equal("4 min ago", Words.Ago(now.AddMinutes(-4), now));
        Assert.Equal("3 h ago", Words.Ago(now.AddHours(-3), now));
        Assert.Equal("1 day ago", Words.Ago(now.AddDays(-1), now));
        Assert.Equal("1 piece", Words.Count(1, "piece"));
        Assert.Equal("2,000 pieces", Words.Count(2000, "piece"));
        Assert.Equal("A, B and C", Words.List(new[] { "A", "B", "C" }));
        Assert.Equal("All 8 pieces are yours", Words.Owned(8, 8));
        Assert.Equal("6 of 8 pieces are yours", Words.Owned(6, 8));
        Assert.Equal("None of the 3 pieces is yours yet", Words.Owned(0, 3));
    }
}

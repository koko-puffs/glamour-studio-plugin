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
    public void TheFingerprintSaysWhoseItIsWithPlatesOff()
    {
        var a = Character("dddddddddddddddddddddd", (Place.Dresser, new uint[] { 1 }));
        var b = Character("ffffffffffffffffffffff", (Place.Dresser, new uint[] { 1 }));
        var (pa, ra) = Wardrobe.Closet(new[] { a });
        var (pb, rb) = Wardrobe.Closet(new[] { b });
        Assert.NotEqual(Wardrobe.Fingerprint(pa, ra, null, a.Key), Wardrobe.Fingerprint(pb, rb, null, b.Key));
        Assert.Equal(Wardrobe.Fingerprint(pa, ra, null, a.Key), Wardrobe.Fingerprint(pb, rb, null, a.Key));
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
        Assert.Equal(new SyncResult(1204, 12, 0, 2, 5, DateTimeOffset.Parse("2026-11-02T04:00:00Z")), r! with { WishesGot = null });
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

public class FeatureTests
{
    private const string Id = "6168b2b8-f0fe-4c50-b1a0-8748b5235533";
    private const string Id2 = "7168b2b8-f0fe-4c50-b1a0-8748b5235533";

    private static string LookText(string id) =>
        """{"id":"ID","name":"Rose","body":"feminine","pieces":{"body":{"item":100}},"visibility":"public","mine":false,"maker":"Koko","loves":3}""".Replace("ID", id);

    [Fact]
    public void ReadsPlansOnePerPlateInOrder()
    {
        var node = JsonNode.Parse("[" + string.Join(",",
            "{\"plate\":5,\"look\":" + LookText(Id) + "}",
            "{\"plate\":2,\"look\":" + LookText(Id2) + "}",
            "{\"plate\":5,\"look\":" + LookText(Id2) + "}",
            "{\"plate\":21,\"look\":" + LookText(Id) + "}",
            "{\"plate\":3,\"look\":{\"id\":\"nope\"}}") + "]");
        var plans = LookJson.ReadPlans(node)!;
        Assert.Equal(new[] { 2, 5 }, plans.Select(p => p.Plate));
        Assert.Equal(Id, plans[1].Look.Id);
        Assert.Empty(LookJson.ReadPlans(null)!);
        Assert.Null(LookJson.ReadPlans(JsonNode.Parse("{}")));
    }

    [Fact]
    public void ReadsWearableLooks()
    {
        var looks = LookJson.ReadLooks(JsonNode.Parse($"[{LookText(Id)},{{\"id\":\"x\"}}]"))!;
        Assert.Single(looks);
        Assert.Equal("public", looks[0].Visibility);
        Assert.Empty(LookJson.ReadLooks(null)!);
        Assert.Null(LookJson.ReadLooks(JsonNode.Parse("{}")));
    }

    [Fact]
    public void ReadsTheWishesTheSyncTookOff()
    {
        var r = SiteLink.ReadSync(JsonNode.Parse("""{"closet":{"total":5,"added":1,"moved":0,"removed":0},"plates":2,"wishes_got":[100,200,0,-4]}"""))!;
        Assert.Equal(new uint[] { 100, 200 }, r.WishesGot);
        var none = SiteLink.ReadSync(JsonNode.Parse("""{"closet":{"total":5}}"""))!;
        Assert.Empty(none.WishesGot!);
    }

    [Fact]
    public void ReadsTheShotsAnswer()
    {
        var ok = SiteLink.ReadShot(HttpStatusCode.OK, """{"id":"ID","name":"Rose"}""".Replace("ID", Id));
        Assert.True(ok.Ok);
        Assert.Equal("Rose", ok.Value!.LookName);
        var odd = SiteLink.ReadShot(HttpStatusCode.OK, "{}");
        Assert.False(odd.Ok);
        var full = SiteLink.ReadShot(HttpStatusCode.Conflict, """{"message":"This look has three shots of yours already.","hint":"full"}""");
        Assert.Equal("full", full.Hint);
        Assert.Contains("three shots", full.Why);
        var token = SiteLink.ReadShot(HttpStatusCode.Unauthorized, """{"message":"The site doesn't know this token.","hint":"token"}""");
        Assert.Equal("token", token.Hint);
        Assert.False(SiteLink.ReadShot(HttpStatusCode.NotFound, null).Ok);
    }

    [Fact]
    public void FindsOutfitsToGather()
    {
        var dresser = new List<DresserPiece>
        {
            new(0, 10, 0, 0), new(1, 11, 0, 0), new(2, 12, 0, 0), // set 1's pieces, loose
            new(3, 20, 0, 0), // set 2: only one loose piece
            new(4, 31, 0, 0, 30), new(4, 32, 0, 0, 30), new(5, 33, 0, 0), // set 3 stored, one piece loose
            new(6, 1_000_011, 0, 0), // an HQ copy of 11, a second place
        };
        var sets = new Dictionary<uint, uint[]> { [1] = new uint[] { 10, 11, 12 }, [2] = new uint[] { 20, 21 }, [30] = new uint[] { 31, 32, 33 } };
        var chances = DresserCleanup.Outfits(dresser, sets);
        Assert.Equal(new uint[] { 1, 30 }, chances.Select(c => c.SetItem));
        Assert.Equal(2, chances[0].Frees);
        Assert.True(chances[1].Stored);
        Assert.Equal(1, chances[1].Frees);
        Assert.Equal(7, DresserCleanup.Used(dresser));
    }

    [Fact]
    public void FindsUnusedPieces()
    {
        var dresser = new List<DresserPiece> { new(0, 10, 0, 0), new(1, 11, 0, 0), new(2, 31, 0, 0, 30), new(3, 1_000_012, 0, 0) };
        var looks = new[] { (IReadOnlyDictionary<Slot, Piece>)new Dictionary<Slot, Piece> { [Slot.Body] = new(10) } };
        var unused = DresserCleanup.Unused(dresser, looks);
        Assert.Equal(new uint[] { 11, 1_000_012 }, unused.Select(d => d.Item));
        var plate = new[] { (IReadOnlyDictionary<Slot, Piece>)new Dictionary<Slot, Piece> { [Slot.Head] = new(12), [Slot.Body] = new(11) } };
        Assert.Equal(new uint[] { 10 }, DresserCleanup.Unused(dresser, plate).Select(d => d.Item));
    }

    [Fact]
    public void FindsDuplicates()
    {
        var dresser = new List<DresserPiece>
        {
            new(0, 10, 0, 0), new(1, 1_000_010, 0, 0), // 10 twice (NQ and HQ, undyed): the second's a duplicate
            new(2, 10, 5, 0), // 10 dyed otherwise: kept
            new(3, 10, 5, 0), // and again dyed the same: a duplicate
            new(4, 31, 0, 0, 30), new(4, 32, 0, 0, 30), // a stored set with 31 and 32
            new(5, 31, 0, 0), // a loose undyed 31: the set has it
            new(6, 32, 7, 0), // a loose dyed 32: kept
            new(7, 40, 0, 0), // one copy only
        };
        Assert.Equal(new[] { 1, 3, 5 }, DresserCleanup.Duplicates(dresser).Select(d => d.Index));
    }

    [Fact]
    public void FitsShotsToTheLongEdge()
    {
        Assert.Equal((2560, 1440), SiteLink.FitShot(3840, 2160, 2560));
        Assert.Equal((1280, 720), SiteLink.FitShot(1280, 720, 2560));
        Assert.Null(SiteLink.FitShot(30, 720, 2560));
    }
}

public class CleanupEdgeTests
{
    [Fact]
    public void ASetsTwoQualitiesAreTwoSets()
    {
        // Set 30 stored in high quality (1_000_030) with 31 and 32: a loose normal 33 doesn't join it,
        // a loose high-quality 33 does; normal 31 and 33 loose make the normal set anew.
        var dresser = new List<DresserPiece>
        {
            new(0, 31, 0, 0, 1_000_030), new(0, 32, 0, 0, 1_000_030),
            new(1, 1_000_033, 0, 0),
            new(2, 31, 0, 0), new(3, 33, 0, 0),
        };
        var chances = DresserCleanup.Outfits(dresser, new Dictionary<uint, uint[]> { [30] = new uint[] { 31, 32, 33 } });
        var join = chances.Single(c => c.Stored);
        Assert.True(join.Hq);
        Assert.Equal(new uint[] { 1_000_033 }, join.Pieces.Select(p => p.Item));
        var fresh = chances.Single(c => !c.Stored);
        Assert.False(fresh.Hq);
        Assert.Equal(new[] { 2, 3 }, fresh.Pieces.Select(p => p.Index));
    }

    [Fact]
    public void ASetCopyAnotherCopyHasIsADuplicate()
    {
        // Set 30 three times: {31,32,33} at 0, {32} at 1 (all in the first: a duplicate), and the
        // high-quality {34} at 2 (a piece the others lack: kept).
        var dresser = new List<DresserPiece>
        {
            new(0, 31, 0, 0, 30), new(0, 32, 0, 0, 30), new(0, 33, 0, 0, 30),
            new(1, 32, 0, 0, 30),
            new(2, 34, 0, 0, 1_000_030),
        };
        var dupes = DresserCleanup.Duplicates(dresser);
        Assert.Single(dupes);
        Assert.Equal(1, dupes[0].Index);
        Assert.Equal(30u, dupes[0].Item);
        Assert.False(dupes[0].InSet);
    }

    [Fact]
    public void ASetsPiecesAreOneQuality()
    {
        // Set 1: two of three loose (a new set needn't be whole), 12 in the bags. Set 2: one loose,
        // one in the bags (put away together, freeing nothing). Set 3: two high quality and one
        // normal: the high-quality two. Set 4: one normal, one high quality: held back. Set 5: a dyed
        // copy is used when there's no undyed one, the undyed one when there is.
        var dresser = new List<DresserPiece>
        {
            new(0, 10, 0, 0), new(1, 11, 0, 0),
            new(2, 20, 0, 0),
            new(4, 1_000_030, 0, 0), new(5, 1_000_031, 0, 0), new(6, 32, 0, 0),
            new(7, 40, 0, 0), new(8, 1_000_041, 0, 0),
            new(9, 50, 3, 0), new(10, 51, 4, 0), new(11, 51, 0, 0),
        };
        var sets = new Dictionary<uint, uint[]>
        {
            [1] = new uint[] { 10, 11, 12 }, [2] = new uint[] { 20, 21, 22 }, [3] = new uint[] { 30, 31, 32 }, [4] = new uint[] { 40, 41 },
            [5] = new uint[] { 50, 51 },
        };
        var (chances, heldBack) = DresserCleanup.Gather(dresser, sets, new HashSet<uint> { 12, 22 });
        Assert.Equal(new uint[] { 1, 2, 3, 5 }, chances.Select(c => c.SetItem).OrderBy(i => i));
        Assert.Equal(1, chances.Single(c => c.SetItem == 1).FromBags);
        Assert.Equal(0, chances.Single(c => c.SetItem == 2).Frees);
        var three = chances.Single(c => c.SetItem == 3);
        Assert.True(three.Hq);
        Assert.Equal(new uint[] { 1_000_030, 1_000_031 }, three.Pieces.Select(p => p.Item));
        Assert.Equal(new[] { 9, 11 }, chances.Single(c => c.SetItem == 5).Pieces.Select(p => p.Index));
        Assert.Equal(1, heldBack);
    }

    [Fact]
    public void AStoredSetTakesAnyOfItsPieces()
    {
        var dresser = new List<DresserPiece> { new(0, 31, 0, 0, 30), new(1, 32, 0, 0), new(2, 1_000_033, 0, 0) };
        var chances = DresserCleanup.Outfits(dresser, new Dictionary<uint, uint[]> { [30] = new uint[] { 31, 32, 33, 34 } });
        Assert.Single(chances);
        Assert.Equal(new uint[] { 32 }, chances[0].Pieces.Select(p => p.Item));
    }

    [Fact]
    public void AStoredSetsOwnPiecesDontJoinItAgain()
    {
        var dresser = new List<DresserPiece> { new(0, 31, 0, 0, 30), new(1, 31, 0, 0) };
        Assert.Empty(DresserCleanup.Outfits(dresser, new Dictionary<uint, uint[]> { [30] = new uint[] { 31, 32 } }));
    }

    [Fact]
    public void ASharedPieceIsCountedOnce()
    {
        var dresser = new List<DresserPiece> { new(0, 10, 0, 0), new(1, 11, 0, 0), new(2, 12, 0, 0) };
        var chances = DresserCleanup.Outfits(dresser, new Dictionary<uint, uint[]> { [1] = new uint[] { 10, 11 }, [2] = new uint[] { 10, 12 } });
        Assert.Equal(2, chances.Count);
        Assert.Equal(1, DresserCleanup.Frees(chances));
    }
}

public class ReviewFixTests
{
    [Fact]
    public void WishesGotReadsAsItemIds()
    {
        var r = SiteLink.ReadSync(JsonNode.Parse("""{"closet":{"total":1},"wishes_got":[100,"200",1000012,100.5,0,99999999,100]}"""))!;
        Assert.Equal(new uint[] { 100, 200, 12 }, r.WishesGot);
    }

    [Fact]
    public void AShotTooThinToSendIsntSized()
    {
        Assert.Null(SiteLink.FitShot(64, 100000, 2560));
        Assert.Equal((2560, 1440), SiteLink.FitShot(2560, 1440, 2560));
    }

    [Fact]
    public void FilledSaysWhatWasSet()
    {
        var plan = PlatePlanner.Plan(
            new Dictionary<Slot, Piece> { [Slot.Body] = new(100), [Slot.Legs] = new(200) },
            new List<DresserPiece> { new(0, 100, 0, 0), new(1, 200, 0, 0) },
            new Dictionary<uint, uint>(), _ => 0, _ => null, new PlateOptions(false, null));
        Assert.StartsWith("Plate 3 has the whole look", Words.Filled(3, plan));
        Assert.StartsWith("Plate 3 has 1 of the look's 2 pieces", Words.Filled(3, plan, 1));
    }
}

public class ConsentTests
{
    [Fact]
    public void TheSitesWordWins()
    {
        Assert.Equal(ConsentStep.Send, Consent.Decide(null, SendChoice.Yes));
        Assert.Equal(ConsentStep.Send, Consent.Decide(false, SendChoice.Yes));
        Assert.Equal(ConsentStep.Hold, Consent.Decide(true, SendChoice.No));
    }

    [Fact]
    public void AnAnswerTheSiteDoesntKnowIsToldToIt()
    {
        Assert.Equal(ConsentStep.TellYes, Consent.Decide(true, SendChoice.Ask));
        Assert.Equal(ConsentStep.TellNo, Consent.Decide(false, SendChoice.Ask));
    }

    [Fact]
    public void NobodySaidSoItsAsked()
    {
        Assert.Equal(ConsentStep.AskPlayer, Consent.Decide(null, SendChoice.Ask));
    }

    [Fact]
    public void NothingIsSentBeforeTheSitesBeenAsked()
    {
        // (Not read yet: a yes here waits; a no holds.)
        Assert.Equal(ConsentStep.Wait, Consent.Decide(true, null));
        Assert.Equal(ConsentStep.Wait, Consent.Decide(null, null));
        Assert.Equal(ConsentStep.Hold, Consent.Decide(false, null));
    }

    [Fact]
    public void ReadsWhatTheSiteHas()
    {
        var map = Consent.ReadCharacters(JsonNode.Parse("""[{"key":"a","send":"yes"},{"key":"b","send":"no"},{"key":"c","send":"ask"},{"key":"d"},{"send":"yes"},7]"""))!;
        Assert.Equal(SendChoice.Yes, map["a"]);
        Assert.Equal(SendChoice.No, map["b"]);
        Assert.Equal(SendChoice.Ask, map["c"]);
        Assert.Equal(SendChoice.Ask, map["d"]);
        Assert.Equal(4, map.Count);
        Assert.Null(Consent.ReadCharacters(JsonNode.Parse("""{"key":"a"}""")));
    }

    [Fact]
    public void ANoSendsNeitherNameNorWorld()
    {
        var no = Consent.CharacterArgs("key", "Koko Nyan", "Lich", false);
        Assert.Equal("", no["p_character"]!["name"]!.GetValue<string>());
        Assert.Equal("", no["p_character"]!["world"]!.GetValue<string>());
        Assert.False(no["p_send"]!.GetValue<bool>());
        var yes = Consent.CharacterArgs("key", "Koko Nyan", "Lich", true);
        Assert.Equal("Koko Nyan", yes["p_character"]!["name"]!.GetValue<string>());
        Assert.True(yes["p_send"]!.GetValue<bool>());
    }

    [Fact]
    public void TheQuestionNamesTheCharacter()
    {
        Assert.Equal("Send Koko Nyan's wardrobe to Glamour Studio?", Consent.Question("Koko Nyan"));
        Assert.Equal("Send this character's wardrobe to Glamour Studio?", Consent.Question(""));
        Assert.Equal("A new character on Lich", Consent.Eyebrow("Lich"));
    }
}

public class AppearanceTests
{
    /// <summary>A human.cmp of colours each saying where it is (red, green: its index's two bytes).</summary>
    private static byte[] Cmp()
    {
        var b = new byte[(4608 + 32 * 1280) * 4];
        for (var i = 0; i < b.Length / 4; i++)
        {
            b[i * 4] = (byte)(i >> 8);
            b[i * 4 + 1] = (byte)i;
            b[i * 4 + 2] = 0x07;
            b[i * 4 + 3] = 0xff;
        }
        return b;
    }

    private static string At(int i) => $"#{(byte)(i >> 8):x2}{(byte)i:x2}07";

    private static byte[] Raen()
    {
        var c = new byte[26];
        c[0] = 6; // Au Ra
        c[1] = 1; // feminine
        c[3] = 80;
        c[4] = 11; // Raen
        c[5] = 2;
        c[6] = 101;
        c[7] = 0x80; // highlights on
        c[8] = 10;
        c[9] = 3;
        c[10] = 20;
        c[11] = 4;
        c[13] = 5;
        c[15] = 6;
        c[19] = 0x80 | 2; // lipstick
        c[20] = 130; // a light one
        return c;
    }

    [Fact]
    public void ColoursComeFromTheClansAndSharedPalettes()
    {
        var o = Appearance.Colours(Raen(), Cmp());
        var chunk = 4608 + ((11 - 1) * 2 + 1) * 1280;
        Assert.Equal(At(chunk + 768 + 10), o["skin"]);
        Assert.Equal(At(chunk + 1024 + 20), o["hair"]);
        Assert.Equal(At(3), o["eyes"]);
        Assert.Equal(At(6), o["eyesLeft"]);
        Assert.Equal(At(5), o["feature"]);
        Assert.Equal(At(256 + 4), o["highlights"]);
        Assert.Equal(At(1792 + 2), o["lips"]);
    }

    [Fact]
    public void WhatsOffHasNoColour()
    {
        var c = Raen();
        c[7] = 0;
        c[19] = 2;
        var o = Appearance.Colours(c, Cmp());
        Assert.False(o.ContainsKey("highlights"));
        Assert.False(o.ContainsKey("lips"));
        Assert.Empty(Appearance.Colours(c, null));
        Assert.Empty(Appearance.Colours(new byte[3], Cmp()));
    }

    [Fact]
    public void ItsSentWithTheOutfit()
    {
        Assert.Null(Appearance.Json(null, null));
        var c = new CharacterWardrobe { Key = "eeeeeeeeeeeeeeeeeeeeee", Name = "A", World = "B", OutfitReadAt = DateTime.UtcNow };
        var (p, r) = Wardrobe.Closet(new[] { c });
        Assert.Null(Wardrobe.SyncArgs(p, r, c)["p_outfit"]!["appearance"]);
        var before = Wardrobe.Fingerprint(p, r, c);
        c.Customize = Raen();
        c.Colours = Appearance.Colours(c.Customize, Cmp());
        var look = Wardrobe.SyncArgs(p, r, c)["p_outfit"]!["appearance"]!;
        Assert.Equal(26, look["customize"]!.AsArray().Count);
        Assert.Equal(6, look["customize"]![0]!.GetValue<int>());
        Assert.Equal(c.Colours["skin"], look["colours"]!["skin"]!.GetValue<string>());
        Assert.NotEqual(before, Wardrobe.Fingerprint(p, r, c));
    }
}

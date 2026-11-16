using System.Text.Json.Nodes;
using GlamourStudio.Core;
using Xunit;

namespace GlamourStudio.Tests;

/// <summary>Fill one of my plates (0.8.0): the site's sends, the slots of pieces you don't have, telling the site it's saved.</summary>
public class PlatesSendTests
{
    private const string Id = "6168b2b8-f0fe-4c50-b1a0-8748b5235533";

    private static string LookText(string id) =>
        """{"id":"ID","name":"Moonlit Scholar","body":"feminine","pieces":{"body":{"item":100}},"visibility":"public","mine":false,"maker":"Mira Sol","loves":3}""".Replace("ID", id);

    [Fact]
    public void ReadsWhatASendSaid()
    {
        var node = JsonNode.Parse("[" + string.Join(",",
            "{\"plate\":4,\"look\":" + LookText(Id) + ",\"missing\":\"empty\",\"sent_at\":\"2026-11-16T20:00:00Z\",\"saved_at\":null,\"waiting\":true,\"variant\":\"Black\"}",
            "{\"plate\":2,\"look\":" + LookText(Id) + ",\"missing\":\"keep\",\"sent_at\":null,\"waiting\":false}",
            // An older site's plan: none of it said.
            "{\"plate\":7,\"look\":" + LookText(Id) + "}") + "]");
        var plans = LookJson.ReadPlans(node)!;
        var sent = plans.Single(p => p.Plate == 4);
        Assert.Equal(MissingChoice.Empty, sent.Missing);
        Assert.True(sent.Waiting);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 20, 0, 0, TimeSpan.Zero), sent.SentAt);
        Assert.Null(sent.SavedAt);
        Assert.Equal("Black", sent.Variant);
        var planned = plans.Single(p => p.Plate == 2);
        Assert.Equal(MissingChoice.Keep, planned.Missing);
        Assert.False(planned.Waiting);
        Assert.Null(planned.SentAt);
        var old = plans.Single(p => p.Plate == 7);
        Assert.Equal(MissingChoice.Keep, old.Missing);
        Assert.False(old.Waiting);
        Assert.Null(old.Variant);
    }

    private static PlatePlan Plan(PlateOptions options) => PlatePlanner.Plan(
        new Dictionary<Slot, Piece> { [Slot.Head] = new(10), [Slot.Hands] = new(20), [Slot.Legs] = new(30), [Slot.Feet] = new(40) },
        new[] { new DresserPiece(0, 10, 0, 0) },
        new Dictionary<uint, uint>(),
        _ => 2,
        id => id == 20 ? Place.Armoury : null,
        options);

    [Fact]
    public void EmptiesTheSlotsOfPiecesYouDontHaveWhenTheSendSaysSo()
    {
        var plan = Plan(new PlateOptions(EmptyMissing: true));
        Assert.True(plan.Steps.Single(s => s.Slot == Slot.Legs).Empty);
        Assert.True(plan.Steps.Single(s => s.Slot == Slot.Feet).Empty);
        // One of yours elsewhere keeps its slot, to fill once it's fetched.
        var hands = plan.Steps.Single(s => s.Slot == Slot.Hands);
        Assert.Equal(StepKind.Missing, hands.Kind);
        Assert.False(hands.Empty);
        Assert.Equal(new[] { Slot.Legs, Slot.Feet }, plan.LeftEmpty.Select(s => s.Slot));
        Assert.Equal(4, plan.Missing.Count() + plan.Ready);
    }

    [Fact]
    public void KeepsThemAsTheyAreOtherwise()
    {
        var plan = Plan(new PlateOptions());
        Assert.Empty(plan.LeftEmpty);
        Assert.All(plan.Missing, s => Assert.False(s.Empty));
    }

    [Fact]
    public void CountsWhatGoesOn()
    {
        var plan = Plan(new PlateOptions(EmptyMissing: true));
        Assert.Equal(1, plan.ToPutOn(new Dictionary<Slot, Piece>()));
        Assert.Equal(0, plan.ToPutOn(new Dictionary<Slot, Piece> { [Slot.Head] = new(10) }));
        Assert.Equal(1, plan.ToPutOn(new Dictionary<Slot, Piece> { [Slot.Head] = new(10, 3, 0) }));
    }

    [Fact]
    public void WordsTheToastTheCardAndWhatsLeft()
    {
        Assert.Equal("Moonlit Scholar is waiting for plate 4", Words.Waiting("Moonlit Scholar", 4));
        Assert.Equal("Moonlit Scholar is waiting for plate 4 (and 2 more)", Words.Waiting("Moonlit Scholar", 4, 2));
        var now = new DateTimeOffset(2026, 11, 16, 20, 3, 0, TimeSpan.Zero);
        Assert.Equal("Sent from the site · 3 min ago", Words.SentFromSite(now.AddMinutes(-3), now));
        Assert.Equal("by Mira Sol · sent by you on the site", Words.SentBy(false, "Mira Sol"));
        Assert.Equal("your look · in Black · sent by you on the site", Words.SentBy(true, "Koko", "Black"));
        Assert.Equal("4 of 7 pieces on it.", Words.OnIt(4, 7));
        Assert.Equal("All 7 pieces on it.", Words.OnIt(7, 7));
        Assert.Equal(("Rings", "wrists and rings emptied"), Words.Emptied(new[] { Slot.Wrists, Slot.RingR, Slot.RingL }));
        Assert.Equal(("Ears", "ears emptied"), Words.Emptied(new[] { Slot.Ears }));
        Assert.Equal("Pedagogy Halfgloves in the armoury chest, Moonlet on Mog Dahl: fill now and add them later, or fetch them first",
            Words.ToFetch(new[] { ("Pedagogy Halfgloves", "in the armoury chest"), ("Moonlet", "on Mog Dahl") }));
        Assert.Null(Words.ToFetch(Array.Empty<(string, string)>()));
    }

    [Fact]
    public void TellsTheSiteTheSavedPlate()
    {
        var at = new DateTimeOffset(2026, 11, 16, 20, 5, 0, TimeSpan.FromHours(1));
        var args = SiteLink.PlateDoneArgs("abcdefghijklmnop1234", 4, Id, new Dictionary<Slot, Piece> { [Slot.Head] = new(1_000_010, 5, 0) }, at);
        Assert.Equal("abcdefghijklmnop1234", (string?)args["p_character"]);
        Assert.Equal(4, (int?)args["p_plate"]);
        Assert.Equal(Id, (string?)args["p_look"]);
        Assert.Equal(10u, (uint?)args["p_pieces"]!["head"]!["item"]);
        Assert.Equal("2026-11-16T19:05:00.0000000+00:00", (string?)args["p_saved_at"]);
        Assert.Null(SiteLink.PlateDoneArgs("abcdefghijklmnop1234", 4, "not a look", new Dictionary<Slot, Piece>(), at)["p_look"]);

        var done = SiteLink.ReadPlateDone(JsonNode.Parse("""{"plate":4,"kept":true,"on":3,"of":7,"told":true}"""));
        Assert.Equal(new PlateDone(4, true, 3, 7), done);
        Assert.False(SiteLink.ReadPlateDone(JsonNode.Parse("""{"plate":4,"kept":false}"""))!.Kept);
        Assert.Null(SiteLink.ReadPlateDone(JsonNode.Parse("[]")));
    }
}

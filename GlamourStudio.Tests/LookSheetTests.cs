using GlamourStudio.Core;
using GlamourStudio.UI.Model;
using Xunit;

namespace GlamourStudio.Tests;

/// <summary>A look's sheet, its game-free parts (UI/Model/LookSheet.cs).</summary>
public class LookSheetTests
{
    [Fact]
    public void EchoRowsTakeTurnsFromTheLastWord()
    {
        var rows = LookSheet.EchoRows("Fuath at Dusk", 748);
        Assert.Equal(4, rows.Count);
        Assert.StartsWith("FUATH AT DUSK · FUATH AT DUSK", rows[0].Text);
        Assert.StartsWith("DUSK · FUATH AT DUSK", rows[1].Text);
        Assert.StartsWith("FUATH AT DUSK", rows[2].Text);
        Assert.StartsWith("DUSK · FUATH", rows[3].Text);
        Assert.Equal((-60f, 120f), (rows[0].XCss, rows[0].YCss));
        Assert.Equal(40f, rows[1].XCss);
        // Each row runs past the field's right edge (capitals about 0.62em across).
        foreach (var r in rows) Assert.True(r.Text.Length * 104 * 0.55f >= 748 - r.XCss);
    }

    [Fact]
    public void EchoRowsOfOneWordAndOfNothingDrawable()
    {
        var rows = LookSheet.EchoRows("Witch", 600);
        Assert.All(rows, r => Assert.StartsWith("WITCH · WITCH", r.Text));
        Assert.Empty(LookSheet.EchoRows("月の学者", 600));
        Assert.Empty(LookSheet.EchoRows(null, 600));
    }

    [Theory]
    [InlineData(712, 214)]
    [InlineData(540, 154)]
    [InlineData(400, 150)]
    public void GearColumnsNarrowWithTheWindow(float inner, float expected) =>
        Assert.Equal(expected, LookSheet.GearWidth(inner));

    [Theory]
    [InlineData(601, 88)]
    [InlineData(440, 63.333336f)]
    [InlineData(100, 40)]
    public void GearRowsFitTheField(float inner, float expected) =>
        Assert.Equal(expected, LookSheet.GearRow(inner), 3);

    [Fact]
    public void BareSlotsSayWhatHappensToThem()
    {
        Assert.Equal(("—", "Weapon", "kept: a plate's weapon is its job's"), LookSheet.Bare(Slot.Main, true));
        Assert.Equal(("—", "Off hand", "kept"), LookSheet.Bare(Slot.Off, true));
        Assert.Equal(("×", "No bracelets", "emptied on the plate"), LookSheet.Bare(Slot.Wrists, true));
        Assert.Equal(("—", "No ring (L)", "left as it is"), LookSheet.Bare(Slot.RingL, false));
    }

    [Theory]
    [InlineData(7, 7, 6, 1, "All seven go on: six from the dresser, one from the armoire")]
    [InlineData(5, 5, 5, 0, "All five go on, from the dresser")]
    [InlineData(2, 5, 0, 2, "Two of five go on, from the armoire")]
    [InlineData(1, 5, 1, 0, "One of five goes on, from the dresser")]
    [InlineData(1, 1, 1, 0, "Its one piece goes on, from the dresser")]
    [InlineData(0, 4, 0, 0, "None of its pieces is in your dresser or armoire")]
    [InlineData(0, 0, 0, 0, "It has no pieces")]
    public void GoesOnSaysHowManyAndFromWhere(int ready, int wanted, int dresser, int armoire, string expected) =>
        Assert.Equal(expected, LookSheet.GoesOn(ready, wanted, dresser, armoire));

    [Fact]
    public void SlipWords()
    {
        Assert.Equal("The three slots it leaves bare are emptied", LookSheet.Emptied(3));
        Assert.Equal("The slot it leaves bare is emptied", LookSheet.Emptied(1));
        Assert.Equal("Plate 3, when it's ready", LookSheet.WhenReady(3, false));
        Assert.Equal("Plate 3", LookSheet.WhenReady(3, true));
        Assert.Equal("A plate, when it's ready", LookSheet.WhenReady(0, false));
        Assert.Equal("Ready for a plate: all seven in your dresser or armoire", LookSheet.ReadyHead(new Readiness(7, 7, 0, 0)));
        Assert.Equal("Not ready yet: two of five in your dresser or armoire", LookSheet.ReadyHead(new Readiness(5, 2, 2, 1)));
    }

    [Fact]
    public void PlateMenuGroupsPlannedThenEmptyThenTheRest()
    {
        var groups = LookSheet.PlateGroups(8, n => n == 5, n => n is 4 or 5 or 6);
        Assert.Equal(3, groups.Count);
        Assert.Equal("Planned for this look", groups[0].Heading);
        Assert.Equal(new[] { 5 }, groups[0].Plates);
        Assert.Equal(new[] { 4, 6 }, groups[1].Plates);
        Assert.Equal(new[] { 1, 2, 3, 7, 8 }, groups[2].Plates);
        // Nothing planned and the plates not read: one group.
        var plain = LookSheet.PlateGroups(3, _ => false, _ => false);
        Assert.Single(plain);
        Assert.Equal("The rest", plain[0].Heading);
    }

    [Fact]
    public void PlateSubLines()
    {
        Assert.Equal("Empty · planned: Fuath at Dusk", LookSheet.PlateSub("Empty", "Fuath at Dusk"));
        Assert.Equal("Tailor's Day Off", LookSheet.PlateSub("Tailor's Day Off", null));
        Assert.Equal("planned: Moonlit Scholar", LookSheet.PlateSub(null, "Moonlit Scholar"));
        Assert.Null(LookSheet.PlateSub(null, null));
        Assert.Equal("05", LookSheet.PlateNumber(5));
        Assert.Equal("20", LookSheet.PlateNumber(20));
    }
}

using System.Numerics;
using GlamourStudio.UI.Model;
using Xunit;

namespace GlamourStudio.Tests;

/// <summary>The Kit's game-free logic for the "Character Sheet" redesign (UI/Model/Sheet.cs, Dashes.Circle).</summary>
public class KitModelTests
{
    [Theory]
    [InlineData("Moonlit Scholar", "MOONLIT SCHOLAR")]
    [InlineData("  Fête   d'été ", "FETE D'ETE")]
    [InlineData("Plate 3", "PLATE 3")]
    [InlineData("Straße", "STRASSE")]
    [InlineData("Tailor’s Day Off", "TAILOR'S DAY OFF")]
    [InlineData("月の学者", "")]
    [InlineData("Witch 🌙 Hour", "WITCH HOUR")]
    [InlineData(null, "")]
    public void EchoCleanKeepsOnlyWhatTheEchoFontHas(string? name, string expected)
    {
        var clean = EchoText.Clean(name);
        Assert.Equal(expected, clean);
        Assert.All(clean, c => Assert.Contains(c, EchoText.Glyphs));
    }

    [Fact]
    public void EchoRowRepeatsTheNameUntilLongEnough()
    {
        var row = EchoText.Row("Plate 3", 20);
        Assert.Equal("PLATE 3 · PLATE 3 · PLATE 3", row);
        Assert.True(row.Length >= 20);
        Assert.Equal("PLATE 3", EchoText.Row("Plate 3", 1));
        Assert.Equal(string.Empty, EchoText.Row("月", 50));
    }

    [Fact]
    public void EchoRowIsBounded()
    {
        Assert.True(EchoText.Row("A", 100000).Length <= 400 + 4);
    }

    [Theory]
    [InlineData("Koko Nyan", "KN")]
    [InlineData("mira sol", "MS")]
    [InlineData("Tal", "T")]
    [InlineData("A'zure Moon Light", "AM")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    [InlineData("   ", "?")]
    public void InitialsAreTheFirstTwoWordsLetters(string? name, string expected) => Assert.Equal(expected, Initials.Of(name));

    [Fact]
    public void TurnRotatesClockwiseOnScreen()
    {
        // 90° clockwise on screen (y down): right of the centre goes below it.
        var p = Turn.Point(new Vector2(10, 0), Vector2.Zero, 90);
        Assert.Equal(0, p.X, 3);
        Assert.Equal(10, p.Y, 3);
        // A full turn comes back, about any centre.
        var c = new Vector2(5, 7);
        var q = Turn.Point(new Vector2(12, -3), c, 360);
        Assert.Equal(12, q.X, 3);
        Assert.Equal(-3, q.Y, 3);
        Span<Vector2> pts = stackalloc Vector2[] { new(1, 0), new(0, 1) };
        Turn.Points(pts, Vector2.Zero, -90);
        Assert.Equal(-1, pts[0].Y, 3);
        Assert.Equal(1, pts[1].X, 3);
    }

    [Fact]
    public void BarFitsFullThenWithoutCountsThenIcons()
    {
        // brand 200, places 500 / 420 / 170, pill 260 (least 160) / 50, gap 12.
        (BarMode, float) Fit(float room, bool narrow = false) => BarFit.Choose(room, narrow, 200, 60, 500, 420, 170, 260, 160, 50, 12);

        Assert.Equal((BarMode.Full, 260f), Fit(1000));
        // The pill shrinks before the counts go.
        var (mode, pill) = Fit(900);
        Assert.Equal(BarMode.Full, mode);
        Assert.Equal(188f, pill);
        // Too little for the full pill's least: the counts go.
        (mode, pill) = Fit(840);
        Assert.Equal(BarMode.NoCounts, mode);
        Assert.Equal(208f, pill);
        (mode, _) = Fit(700);
        Assert.Equal(BarMode.Icons, mode);
        // Under 1000 wide: icons, whatever fits.
        Assert.Equal((BarMode.Icons, 50f), Fit(5000, narrow: true));
    }

    [Fact]
    public void BarKeepsAShortPillWhole()
    {
        // A pill shorter than its least shrink fits as it is.
        var (mode, pill) = BarFit.Choose(900, false, 200, 60, 500, 420, 170, 120, 160, 50, 12);
        Assert.Equal(BarMode.Full, mode);
        Assert.Equal(120f, pill);
    }

    [Fact]
    public void HoloSlidesThereAndBackWithinTheTile()
    {
        Assert.Equal(0f, HoloSlide.Offset(5, 0.3f, still: true));
        Assert.Equal(0f, HoloSlide.Offset(5, 1f, still: false));
        Assert.Equal(0f, HoloSlide.Offset(0, 0.3f, still: false), 3);
        // Halfway through the period it's at the far end; it never passes it.
        Assert.Equal(0.7f, HoloSlide.Offset(12, 0.3f, still: false, period: 24), 3);
        for (var t = 0.0; t < 100; t += 0.37)
        {
            var o = HoloSlide.Offset(t, 0.3f, still: false);
            Assert.InRange(o, 0f, 0.7f + 1e-4f);
        }
    }

    [Theory]
    [InlineData(0x181937u, true)]
    [InlineData(0x1e1e1eu, true)]
    [InlineData(0xf9f8f4u, false)]
    [InlineData(0xbbb5dau, false)]
    [InlineData(0x04afcdu, false)]
    public void DyeInkFollowsTheMocksRule(uint rgb, bool dark) => Assert.Equal(dark, DyeInk.IsDark(rgb));

    [Fact]
    public void ACircleIsClosedAndRound()
    {
        var c = new Vector2(10, 20);
        var pts = Dashes.Circle(c, 30, 64);
        Assert.Equal(65, pts.Count);
        Assert.Equal(pts[0], pts[^1]);
        Assert.All(pts, p => Assert.Equal(30, Vector2.Distance(p, c), 3));
        // From the top.
        Assert.Equal(c.Y - 30, pts[0].Y, 3);
        var dashes = Dashes.Along(pts, 6);
        Assert.InRange(dashes.Count, 30, 32);
    }
}

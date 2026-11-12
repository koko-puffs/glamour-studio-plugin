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

    // ---------------------------------------------------------------------------------------------
    // Characters a face hasn't got: never ImGui's "?".

    /// <summary>A stand-in face: Latin-1 and the base punctuation; the fallback: kana and kanji, and Ł.</summary>
    private static bool InFace(char c) => c is >= ' ' and <= '\u00FF' || c is '’' or '“' or '”' or '–' or '—' or '…';

    private static bool InFallback(char c) => c is >= '\u3040' and <= '\u30FF' or >= '\u4E00' and <= '\u9FFF' or 'Ł';

    [Theory]
    [InlineData("Moonlit Scholar")]
    [InlineData("Fête d’été · Ñoño Größe")]
    [InlineData("Tailor’s “Day” Off…")]
    public void LatinNamesStayInTheirFace(string text)
    {
        var wanted = new HashSet<char>();
        Assert.Null(Glyphs.Split(text, InFace, InFallback, wanted));
        Assert.Empty(wanted);
    }

    [Fact]
    public void OtherScriptsGoToTheFallbackInRuns()
    {
        var wanted = new HashSet<char>();
        var runs = Glyphs.Split("Look 月の学者 by Mira", InFace, InFallback, wanted);
        Assert.NotNull(runs);
        Assert.Equal(new[] { ("Look ", false), ("月の学者", true), (" by Mira", false) }, runs!);
        Assert.Empty(wanted);
        runs = Glyphs.Split("ŁUKASZ", InFace, InFallback, wanted);
        Assert.Equal(new[] { ("Ł", true), ("UKASZ", false) }, runs!);
    }

    [Fact]
    public void WhatNeitherHasIsLeftOutAndAskedFor()
    {
        var wanted = new HashSet<char>();
        // Hangul (not in either yet), an emoji (never: outside the BMP), a control.
        var runs = Glyphs.Split("Moon 달 🌙 x\u0001", InFace, InFallback, wanted);
        Assert.NotNull(runs);
        var drawn = string.Concat(runs!.Select(r => r.Text));
        Assert.Equal("Moon   x", drawn);
        Assert.DoesNotContain('?', drawn);
        Assert.Equal(new[] { '달' }, wanted.ToArray());
        // Nothing drawable at all: no runs, nothing drawn.
        Assert.Empty(Glyphs.Split("🌙", InFace, InFallback, wanted)!);
    }

    [Fact]
    public void LineBreaksStayWhereTheyAre()
    {
        var wanted = new HashSet<char>();
        // A tooltip's two lines: plain, then with a sign past ASCII, then beside the fallback.
        Assert.True(Glyphs.IsPlainAscii("Legs\nIn your armoire"));
        Assert.Null(Glyphs.Split("Legs · Snow White\nIn your armoire", c => c != '\n' && InFace(c), InFallback, wanted));
        var runs = Glyphs.Split("月の服\nIn your armoire", c => c != '\n' && InFace(c), InFallback, wanted);
        Assert.Equal(new[] { ("月の服", true), ("\nIn your armoire", false) }, runs!);
        Assert.Empty(wanted);
    }

    [Theory]
    [InlineData('é', true)]
    [InlineData('月', true)]
    [InlineData('\uD83C', false)]
    [InlineData('\uE040', false)]
    [InlineData('\u0007', false)]
    [InlineData('\uFFFD', false)]
    [InlineData('\u0085', false)]
    public void OnlyRealCharactersAreAskedFor(char c, bool wantable) => Assert.Equal(wantable, Glyphs.Wantable(c));

    [Fact]
    public void LatinExtrasGoToTheFacesToo()
    {
        Assert.True(Glyphs.IsLatinish('Ł'));
        Assert.True(Glyphs.IsLatinish('ș'));
        Assert.True(Glyphs.IsLatinish('ố'));
        Assert.False(Glyphs.IsLatinish('月'));
        Assert.False(Glyphs.IsLatinish('é')); // Latin-1: in every face from the start
    }

    [Fact]
    public void RangesAreSortedJoinedAndEndWithZero()
    {
        var r = Glyphs.Ranges(new[] { 'b', 'a', 'c', '月' }, withBase: false);
        Assert.Equal(new ushort[] { 'a', 'c', '月', '月', 0 }, r);
        var withBase = Glyphs.Ranges(new[] { 'Ł' });
        Assert.Equal(0, withBase[^1]);
        Assert.Equal(0x20, withBase[0]);
        // Every pair is in order and none overlaps the next.
        for (var i = 0; i + 3 < withBase.Length; i += 2)
        {
            Assert.True(withBase[i] <= withBase[i + 1]);
            Assert.True(withBase[i + 1] + 1 < withBase[i + 2]);
        }
        // Latin-1's letters (é ö ñ ß) and the curly apostrophe are in every face's atlas.
        foreach (var c in "éöñßÆøÿ’“”–—…")
            Assert.Contains(Glyphs.Base, b => c >= b.From && c <= b.To);
    }

    [Theory]
    [InlineData(10, 11)]
    [InlineData(13, 13)]
    [InlineData(14, 15)]
    [InlineData(20, 24)]
    [InlineData(60, 34)]
    public void TheFallbackSizeIsTheNearestAtLeastAsBig(int px, int expected) =>
        Assert.Equal(expected, Glyphs.Nearest(px, new[] { 11, 13, 15, 18, 24, 34 }));
}

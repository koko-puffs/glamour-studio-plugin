using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace GlamourStudio.UI.Model;

/// <summary>
/// The echo's words: a name in the capitals and digits the echo's font has (its atlas holds only
/// those, at 72 and 104px, to stay small), repeated into rows. Pure, so it's tested.
/// </summary>
public static class EchoText
{
    /// <summary>Every character the echo's font is built with (the space included).</summary>
    public const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ·-'&!?.,:/+#()";

    /// <summary>
    /// The name as the echo can draw it: in capitals, its accents taken off (É → E), anything else
    /// it hasn't a glyph for left out, runs of spaces made one. Empty when nothing's left (a name in
    /// kana): the echo is then not drawn.
    /// </summary>
    public static string Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var decomposed = name.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var space = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            // (Letters with no one capital of their own: ß is SS.)
            if (c is 'ß' or 'ẞ')
            {
                if (space) sb.Append(' ');
                space = false;
                sb.Append("SS");
                continue;
            }
            var ch = c switch
            {
                'Æ' => 'A',
                'Ø' => 'O',
                '’' or '‘' => '\'',
                '–' or '—' => '-',
                _ => c,
            };
            if (char.IsWhiteSpace(ch))
            {
                space = sb.Length > 0;
                continue;
            }
            if (Glyphs.IndexOf(ch) < 0) continue;
            if (space) sb.Append(' ');
            space = false;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// One row of the echo: the (cleaned) name and " · " over and over until the row has at least
    /// <paramref name="minChars"/> characters (the mock's "PLATE 3 · PLATE 3 · PLATE 3 ·"). Empty
    /// when the name has nothing the echo can draw.
    /// </summary>
    public static string Row(string? name, int minChars)
    {
        var clean = Clean(name);
        if (clean.Length == 0) return string.Empty;
        minChars = Math.Clamp(minChars, 1, 400);
        var sb = new StringBuilder(minChars + clean.Length + 3);
        while (sb.Length < minChars)
        {
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(clean);
        }
        return sb.ToString();
    }
}

/// <summary>A character's initials, as the header's pill and the maker's seal show them.</summary>
public static class Initials
{
    /// <summary>The first letters of the first two words, in capitals ("Koko Nyan" → "KN"); "?" for no name.</summary>
    public static string Of(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var sb = new StringBuilder(2);
        foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var first = word.TrimStart('\'', '"', '(', '-');
            if (first.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(first[0]));
            if (sb.Length == 2) break;
        }
        return sb.Length == 0 ? "?" : sb.ToString();
    }
}

/// <summary>Turning points about a centre: the seal (−4°) and the sign (−5°) are drawn, then their vertices turned.</summary>
public static class Turn
{
    /// <summary>The point turned by <paramref name="degrees"/> (clockwise on screen, as CSS rotate) about <paramref name="center"/>.</summary>
    public static Vector2 Point(Vector2 p, Vector2 center, float degrees)
    {
        var a = degrees * MathF.PI / 180f;
        var (sin, cos) = MathF.SinCos(a);
        var d = p - center;
        return center + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
    }

    /// <summary>Every point turned in place.</summary>
    public static void Points(Span<Vector2> points, Vector2 center, float degrees)
    {
        for (var i = 0; i < points.Length; i++) points[i] = Point(points[i], center, degrees);
    }
}

/// <summary>How the header's bar fits the window: its places with their words and counts, without the counts, or as icons.</summary>
public enum BarMode
{
    /// <summary>The wordmark, each place's icon, name and count, the pill's line.</summary>
    Full,

    /// <summary>As full, but the counts go to the places' tooltips.</summary>
    NoCounts,

    /// <summary>The logo alone, the places as icons, the pill as the face and its dot (under 1000 wide, as the mock).</summary>
    Icons,
}

/// <summary>The bar's widths, measured by the window, and the choice made from them. Pure, so it's tested.</summary>
public static class BarFit
{
    /// <summary>
    /// The first of Full, NoCounts and Icons that fits <paramref name="room"/> (the bar between its
    /// padding, less Refresh and Settings), and how wide the pill is then: in Full and NoCounts it
    /// shrinks (its line cut with "…") down to <paramref name="pillMin"/> before the next mode is tried.
    /// <paramref name="narrow"/> (a window under 1000 wide) is Icons whatever fits.
    /// </summary>
    /// <param name="brandFull">The logo, the wordmark and the slash.</param>
    /// <param name="brandIcon">The logo and the slash.</param>
    /// <param name="placesFull">The five places with names and counts, and their gaps.</param>
    /// <param name="placesNoCounts">The five places with names only.</param>
    /// <param name="placesIcons">The five places as icons.</param>
    /// <param name="pillFull">The pill with its whole line.</param>
    /// <param name="pillMin">The least the pill shrinks to with a line.</param>
    /// <param name="pillCompact">The pill as the face and its dot.</param>
    /// <param name="gap">The least room between the places and the pill.</param>
    public static (BarMode Mode, float Pill) Choose(float room, bool narrow, float brandFull, float brandIcon,
        float placesFull, float placesNoCounts, float placesIcons, float pillFull, float pillMin, float pillCompact, float gap)
    {
        if (!narrow)
        {
            var left = room - brandFull - gap;
            if (left - placesFull >= MathF.Min(pillFull, pillMin)) return (BarMode.Full, MathF.Min(pillFull, left - placesFull));
            if (left - placesNoCounts >= MathF.Min(pillFull, pillMin)) return (BarMode.NoCounts, MathF.Min(pillFull, left - placesNoCounts));
        }
        return (BarMode.Icons, pillCompact);
    }
}

/// <summary>The holo's slow slide: where a strip of the holo tile starts, so it shifts as the site's does.</summary>
public static class HoloSlide
{
    /// <summary>
    /// The strip's left edge in the tile (0 to 1 − <paramref name="span"/>), going there and back
    /// once every <paramref name="period"/> seconds; 0 when it stays still (reduced motion) or when
    /// the strip is as wide as the tile.
    /// </summary>
    public static float Offset(double seconds, float span, bool still, double period = 24)
    {
        var room = 1f - Math.Clamp(span, 0f, 1f);
        if (still || room <= 0 || period <= 0) return 0;
        var phase = (float)(seconds % period / period);
        var tri = phase < 0.5f ? phase * 2 : 2 - phase * 2;
        return room * tri;
    }
}

/// <summary>Whether a dye's name on the palette strip is written light (on a dark dye) or dark: the mock's rule.</summary>
public static class DyeInk
{
    /// <summary>True when the colour is dark enough for light words (0.3 R + 0.59 G + 0.11 B under 110).</summary>
    public static bool IsDark(uint rgb)
    {
        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        return r * 0.3 + g * 0.59 + b * 0.11 < 110;
    }
}

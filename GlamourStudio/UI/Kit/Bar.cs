using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// The header bar's parts (the mock's <c>.bar</c>, 56 tall under Dalamud's title bar): the brand
/// (the logo and GLAMOUR STUDIO), a place (a tab with its count and the holo underline), the who-pill
/// (your character's face, a dot and one line for what's going on). The window lays them
/// (MainWindow.DrawBar); the companion panels' header uses <see cref="Brand"/>'s pieces too.
/// </summary>
public static partial class Kit
{
    /// <summary>The bar's height, its line included (CSS px).</summary>
    public const float BarHeightCss = 56;

    /// <summary>The wordmark's words.</summary>
    public const string Wordmark = "Glamour Studio";

    /// <summary>The brand's width: the 30px logo, and with <paramref name="wordmark"/> 10px then GLAMOUR STUDIO (Bricolage 15, 0.04em).</summary>
    public static float BrandWidth(bool wordmark) => S(30) + (wordmark ? S(10) + DisplayWidth(Wordmark, 15, 0.04f) : 0);

    /// <summary>
    /// The brand (the mock's <c>.brand</c>): the logo (30px) and the wordmark in Bricolage's capitals,
    /// in a row <paramref name="rowHeight"/> tall from <paramref name="pos"/>. Returns its width.
    /// </summary>
    public static float Brand(Vector2 pos, float rowHeight, bool wordmark = true)
    {
        Logo(new Vector2(pos.X, pos.Y + (rowHeight - S(30)) / 2), 30);
        if (!wordmark) return S(30);
        var x = pos.X + S(30 + 10);
        var w = Display(new Vector2(x, pos.Y + (rowHeight - DisplayHeight(15)) / 2), Wordmark, 15, Palette.Ink, 0, false, 0.04f);
        return x + w - pos.X;
    }

    /// <summary>A place's width: 9px either side of its 16px icon, 7px, its name (Figtree 600 13.5) and its count (mono 11); the icon alone in <paramref name="icons"/>.</summary>
    public static float PlaceWidth(string label, string? count, bool icons = false)
    {
        if (icons) return S(9 + 16 + 9);
        return S(9) * 2 + S(16 + 7) + Txt.Measure(label, Face.SansSemibold, 14).X + (count != null ? S(7) + Txt.Measure(count, Face.Mono, 11).X : 0);
    }

    /// <summary>
    /// A place in the bar (the mock's <c>.place</c>): its icon, its name and its count, muted; where
    /// you are in Ink with a lilac icon and count and the holo underline at its foot, over the bar's
    /// line. In <paramref name="icons"/> (a narrow window) the icon alone, its name and count the
    /// tooltip (as they are when <paramref name="count"/> is left out for room: pass
    /// <paramref name="tipCount"/>). <paramref name="height"/> is the bar's (screen px). True when pressed.
    /// </summary>
    public static bool Place(string id, Vector2 pos, float height, Icon icon, string label, string? count, bool on, bool icons = false, string? tipCount = null)
    {
        var w = PlaceWidth(label, count, icons);
        var (clicked, hovered, _) = Hit(id, pos, new Vector2(w, height));
        var dl = Dl;
        var ink = on ? Palette.Ink : hovered ? Palette.Body : Palette.Muted;
        var x = pos.X + S(9);
        Icons.Draw(dl, icon, new Vector2(x, pos.Y + (height - S(16)) / 2), S(16), on ? Palette.Accent : ink);
        if (!icons)
        {
            x += S(16 + 7);
            x += Txt.Line(dl, new Vector2(x, pos.Y), label, Face.SansSemibold, 14, ink, height / S(1));
            if (count != null)
                Txt.Line(dl, new Vector2(x + S(7), pos.Y + S(1)), count, Face.Mono, 11, on ? Palette.Accent : Palette.Faint, height / S(1));
        }
        if (hovered && (icons || count == null))
        {
            var c = count ?? tipCount;
            Tooltip(c != null ? $"{label} {c}" : label);
        }
        if (on) HoloUnderline(pos.X + S(9), pos.X + w - S(9), pos.Y + height);
        FocusRing(pos + new Vector2(S(2), S(10)), pos + new Vector2(w - S(2), height - S(10)), S(8));
        return clicked;
    }

    /// <summary>The who-pill's width with its line (its face, dot and padding included); <paramref name="compact"/>, the face and dot alone.</summary>
    public static float WhoPillWidth(string text, bool compact = false) =>
        compact ? S(5 + 24 + 8 + 7 + 8) : S(5 + 24 + 8 + 7 + 8 + 12) + Txt.Measure(text, Face.SansMedium, 12).X;

    /// <summary>
    /// The who-pill (the mock's <c>.who</c>): 34 tall on the card, your character's face (their
    /// initials on the periwinkle; on the soft tile when <paramref name="off"/>), a dot for how it's
    /// going, and one line, cut to <paramref name="maxWidth"/> (screen px). The tone colours its edge
    /// and dot (live and recording ringed, live breathing with <paramref name="pulse"/>; danger's line
    /// red too). <paramref name="compact"/>: the face and dot alone, the line the tooltip.
    /// <paramref name="tip"/> shows when it's pointed at (the name and world). Returns (pressed, width).
    /// </summary>
    public static (bool Pressed, float Width) WhoPill(string id, Vector2 pos, string initials, string text, Tone tone, bool off = false,
        bool pulse = false, float maxWidth = 0, bool compact = false, string? tip = null)
    {
        var (ink, _, edge) = ToneColors(tone);
        var w = WhoPillWidth(text, compact);
        var full = text;
        if (!compact && maxWidth > 0 && w > maxWidth)
        {
            text = Txt.Fit(text, Face.SansMedium, 12, MathF.Max(S(20), maxWidth - (w - Txt.Measure(text, Face.SansMedium, 12).X)));
            w = MathF.Min(maxWidth, WhoPillWidth(text));
        }
        var h = S(34);
        var (clicked, hovered, _) = Hit(id, pos, new Vector2(w, h));
        var dl = Dl;
        var max = pos + new Vector2(w, h);
        dl.AddRectFilled(pos, max, Palette.U32(Palette.Card), h / 2);
        dl.AddRect(pos + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(hovered ? Palette.LineHover : edge), h / 2, ImDrawFlags.None, 1f);
        Avatar(new Vector2(pos.X + S(5 + 12), pos.Y + h / 2), initials, 24, off);
        StatusDot(new Vector2(pos.X + S(5 + 24 + 8) + S(3.5f), pos.Y + h / 2), tone, pulse);
        if (!compact)
            Txt.Line(dl, new Vector2(pos.X + S(5 + 24 + 8 + 7 + 8), pos.Y), text, Face.SansMedium, 12, ink, 34);
        if (hovered)
        {
            // (A line cut to fit is said in full, as the rail's foot had it.)
            if (compact || !string.Equals(text, full, StringComparison.Ordinal)) Tooltip(tip, full);
            else if (tip != null) Tooltip(tip);
        }
        FocusRing(pos, max, h / 2);
        return (clicked, w);
    }
}

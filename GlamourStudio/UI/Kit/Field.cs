using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.UI.Model;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// The colours that turn over on the field: words, edges and tracks are ink-violet there (as the
/// site's <c>.gs-moment</c> has them) and the ink's own everywhere else. A part, or a screen drawing
/// its own words, asks here rather than <see cref="Palette"/> when it may be drawn on a field.
/// </summary>
public static class Tint
{
    private static bool F => Kit.IsOnField;

    /// <summary>Headings and names: Ink, OnField on the field.</summary>
    public static Vector4 Ink => F ? Palette.OnField : Palette.Ink;

    /// <summary>What you read: Body, OnField.</summary>
    public static Vector4 Body => F ? Palette.OnField : Palette.Body;

    /// <summary>A line under a name: Muted, OnField2.</summary>
    public static Vector4 Muted => F ? Palette.OnField2 : Palette.Muted;

    /// <summary>What hardly needs reading: Faint, OnField2 at 85%.</summary>
    public static Vector4 Faint => F ? Palette.WithAlpha(Palette.OnField2, 0.85f) : Palette.Faint;

    /// <summary>Eyebrows, the hand voice, the seal, a link: lilac, OnField.</summary>
    public static Vector4 Accent => F ? Palette.OnField : Palette.Accent;

    /// <summary>An outlined pill's or a field's edge: LineStrong, the field's ink at 35%.</summary>
    public static Vector4 Edge => F ? Palette.OnFieldLine : Palette.LineStrong;

    /// <summary>A hairline between rows: Line, the field's ink at 18%.</summary>
    public static Vector4 Line => F ? Palette.OnFieldTrack : Palette.Line;

    /// <summary>A pointed-at fill: the lilac wash, the field's ink at 14%.</summary>
    public static Vector4 Wash => F ? Palette.OnFieldWash : Palette.AccentWash;

    /// <summary>A bar's or ring's track: Tile, the field's ink at 18%.</summary>
    public static Vector4 Track => F ? Palette.OnFieldTrack : Palette.Tile;

    /// <summary>A bar's or ring's fill: ice, the field's ink.</summary>
    public static Vector4 Fill => F ? Palette.OnField : Palette.Ice;

    /// <summary>A dashed empty tile's edge: LineStrong, the field's ink at 40%.</summary>
    public static Vector4 Dashed => F ? Palette.WithAlpha(Palette.OnField, 0.4f) : Palette.LineStrong;

    /// <summary>The sticker's (and the sign's) hard block: deep violet, black on the field.</summary>
    public static Vector4 Block => F ? Palette.Black : Palette.StickerShadow;
}

/// <summary>A row of the echo: its words (from <see cref="EchoText.Row"/>) and where its line box starts, from the field's top-left, in CSS px.</summary>
public readonly record struct EchoRow(string Text, float XCss, float YCss);

/// <summary>
/// The sheet's grounds: the field (a screen's one moment) and what turns over on it, the callout
/// (an ink card on the field), the plate slip (a ticket), the echo (a name huge behind), the
/// display and hand voices, and the window's textures (the logo, holo, the sparkle, the cloud).
/// </summary>
public static partial class Kit
{
    // ---------------------------------------------------------------------------------------------
    // Surfaces: on the field or on the ink. A stack, so a callout on a field is ink again inside.

    private static readonly Stack<bool> Surfaces = new();

    /// <summary>True while drawing on a field (inside <see cref="FieldPanel"/> or <see cref="OnField"/>, and not in a callout on it).</summary>
    public static bool IsOnField => Surfaces.Count > 0 && Surfaces.Peek();

    /// <summary>A surface pushed: pops it (and the ImGui text colour pushed with it) when disposed.</summary>
    public readonly struct SurfaceScope : IDisposable
    {
        private readonly bool pushedColor;

        internal SurfaceScope(bool field, bool pushColor)
        {
            Surfaces.Push(field);
            pushedColor = pushColor;
            if (pushColor) ImGui.PushStyleColor(ImGuiCol.Text, field ? Palette.OnField : Palette.Body);
        }

        public void Dispose()
        {
            if (Surfaces.Count > 0) Surfaces.Pop();
            if (pushedColor) ImGui.PopStyleColor();
        }
    }

    /// <summary>
    /// Everything drawn inside is on the field: words, eyebrows, outlines, tracks and the sticker
    /// turn to the field's colours (<see cref="Tint"/>), and ImGui's own text is ink-violet.
    /// <c>using (Kit.OnField()) { … }</c>. <see cref="FieldPanel"/> draws the field and does this.
    /// </summary>
    public static SurfaceScope OnField() => new(true, true);

    /// <summary>Back on the ink inside a field (what a callout does): <c>using (Kit.OnInk()) { … }</c>.</summary>
    public static SurfaceScope OnInk() => new(false, true);

    // ---------------------------------------------------------------------------------------------
    // The field.

    /// <summary>
    /// The field (the mock's <c>.field</c>): flat periwinkle with <paramref name="radiusCss"/> corners,
    /// its floor deeper (FieldDeep at 55% from 62% of the way down, as a gradient on the shape's own
    /// vertices) and its page dotted every 14px. Returns the on-field scope:
    /// <c>using (Kit.FieldPanel(min, max)) { … }</c>. Draw the echo first, inside it.
    /// </summary>
    public static SurfaceScope FieldPanel(Vector2 min, Vector2 max, float radiusCss = Palette.RadiusXl, bool dots = true)
    {
        DrawField(min, max, radiusCss, dots);
        return OnField();
    }

    /// <summary>The field drawn alone (see <see cref="FieldPanel"/>), for a part that sets its own scope.</summary>
    public static void DrawField(Vector2 min, Vector2 max, float radiusCss = Palette.RadiusXl, bool dots = true)
    {
        var dl = Dl;
        var size = max - min;
        if (size.X <= 1 || size.Y <= 1) return;
        var r = MathF.Min(S(radiusCss), MathF.Min(size.X, size.Y) / 2);
        dl.AddRectFilled(min, max, Palette.U32(Palette.Field), r);

        // The floor: the bottom 38% again, its vertices shaded from the field at its top to the deep
        // violet's 55% at the foot (the field is opaque, so that's the same as the mock's wash over it).
        var y0 = min.Y + size.Y * 0.62f;
        if (max.Y - y0 > r + 1)
        {
            var start = dl.VtxBuffer.Size;
            dl.AddRectFilled(new Vector2(min.X, y0), max, Palette.U32(Palette.Field), r, ImDrawFlags.RoundCornersBottom);
            var end = dl.VtxBuffer.Size;
            ImGuiP.ShadeVertsLinearColorGradientKeepAlpha(dl, start, end, new Vector2(min.X, y0), new Vector2(min.X, max.Y),
                Palette.U32(Palette.Field), Palette.U32(Palette.Mix(Palette.Field, Palette.FieldDeep, 0.55f)));
        }

        if (dots) FieldDots(dl, min, max, r);
    }

    /// <summary>
    /// The field's dotted page: a 2px dot of the ink at 16% in the middle of every 14px square, none
    /// past the rounded corners. Plain squares (four vertices each, no edge smoothing), so a whole
    /// field is a few thousand vertices.
    /// </summary>
    private static void FieldDots(ImDrawListPtr dl, Vector2 min, Vector2 max, float r)
    {
        var step = S(14);
        // (A field the size of a big window would be tens of thousands of dots: it goes plain.)
        if (step < 4 || (max.X - min.X) * (max.Y - min.Y) / (step * step) > 8000) return;
        var half = MathF.Max(1, S(1));
        var col = Palette.U32(Palette.OnFieldDots);
        var inner = MathF.Max(0, r - S(2));
        for (var y = min.Y + step / 2; y < max.Y; y += step)
        {
            for (var x = min.X + step / 2; x < max.X; x += step)
            {
                // Inside the rounded shape: past a corner's square, within its arc.
                var cx = x < min.X + r ? min.X + r : x > max.X - r ? max.X - r : x;
                var cy = y < min.Y + r ? min.Y + r : y > max.Y - r ? max.Y - r : y;
                if ((cx != x || cy != y) && Vector2.DistanceSquared(new Vector2(cx, cy), new Vector2(x, y)) > inner * inner) continue;
                dl.AddRectFilled(new Vector2(x - half, y - half), new Vector2(x + half, y + half), col);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The echo.

    private static readonly Dictionary<(string Name, int MinChars), string> EchoRowsMade = new();

    /// <summary>The echo's row for a name, made once (the name in capitals, " · " between, at least <paramref name="minChars"/> long).</summary>
    public static string EchoRowOf(string name, int minChars)
    {
        if (EchoRowsMade.TryGetValue((name, minChars), out var row)) return row;
        if (EchoRowsMade.Count >= 256) EchoRowsMade.Clear();
        return EchoRowsMade[(name, minChars)] = EchoText.Row(name, minChars);
    }

    /// <summary>
    /// The echo (the mock's <c>.echo</c>): rows of words in Bricolage at <paramref name="px"/> (104
    /// or 72: the only sizes the echo's font has) in the field's tone, behind everything else on the
    /// field, clipped to its rounded shape. Rows' line boxes are 0.92em tall. Draw it right after the
    /// field, before what's on it.
    /// </summary>
    public static void Echo(Vector2 min, Vector2 max, float radiusCss, ReadOnlySpan<EchoRow> rows, int px = 104, Vector4? color = null)
    {
        if (rows.Length == 0) return;
        var dl = Dl;
        var size = max - min;
        var r = MathF.Min(S(radiusCss), MathF.Min(size.X, size.Y) / 2);
        var col = Palette.U32(color ?? Palette.FieldTone);
        using (Txt.Fonts.Push(Face.Echo, px))
        {
            var fontSize = ImGui.GetFontSize();
            var lineBox = S(px * 0.92f);
            var font = ImGui.GetFont();
            // Rect clips can't round a corner: the middle bands whole, and each corner's square in
            // three strips, each as wide as its arc lets it be at its top (the words are opaque, so
            // what two clips both let through is drawn twice and looks the same).
            Span<Vector4> clips = stackalloc Vector4[14];
            var n = 0;
            clips[n++] = new Vector4(min.X + r, min.Y, max.X - r, max.Y);
            clips[n++] = new Vector4(min.X, min.Y + r, max.X, max.Y - r);
            for (var k = 1; k < 4; k++)
            {
                var t0 = r * k / 4;
                var t1 = r * (k + 1) / 4;
                var inset = r - MathF.Sqrt(MathF.Max(0, r * r - (r - t0) * (r - t0)));
                clips[n++] = new Vector4(min.X + inset, min.Y + t0, min.X + r, min.Y + t1);
                clips[n++] = new Vector4(max.X - r, min.Y + t0, max.X - inset, min.Y + t1);
                clips[n++] = new Vector4(min.X + inset, max.Y - t1, min.X + r, max.Y - t0);
                clips[n++] = new Vector4(max.X - r, max.Y - t1, max.X - inset, max.Y - t0);
            }
            foreach (var c in clips[..n])
            {
                if (c.Z - c.X < 1 || c.W - c.Y < 1) continue;
                dl.PushClipRect(new Vector2(c.X, c.Y), new Vector2(c.Z, c.W), true);
                foreach (var row in rows)
                {
                    if (row.Text.Length == 0) continue;
                    var top = min.Y + S(row.YCss) + (lineBox - fontSize) / 2;
                    if (top > c.W || top + fontSize < c.Y) continue;
                    dl.AddText(font, fontSize, new Vector2(MathF.Round(min.X + S(row.XCss)), MathF.Round(top)), col, row.Text);
                }
                dl.PopClipRect();
            }
        }
    }

    private static readonly EchoRow[] EchoScratch = new EchoRow[8];

    /// <summary>
    /// The echo of a name filling the field (as the mock's look and plate frames): <paramref name="rowCount"/>
    /// rows from just above its top, each starting further in or out (34%, 10%, 38%, 16% of the way
    /// across) and running past its right edge. Nothing when the name has nothing the echo can draw.
    /// </summary>
    public static void Echo(Vector2 min, Vector2 max, float radiusCss, string name, int rowCount = 3, int px = 104, Vector4? color = null)
    {
        rowCount = Math.Clamp(rowCount, 1, EchoScratch.Length);
        var wCss = (max.X - min.X) / S(1);
        ReadOnlySpan<float> xs = stackalloc float[] { 0.34f, 0.10f, 0.38f, 0.16f };
        for (var i = 0; i < rowCount; i++)
        {
            var x = wCss * xs[i % xs.Length];
            // Bricolage's capitals are about 0.62em across; a row runs a little past the edge.
            var chars = (int)MathF.Ceiling((wCss - x) / (px * 0.55f)) + 3;
            EchoScratch[i] = new EchoRow(EchoRowOf(name, chars), x, -px * 0.1f + i * px * 0.92f);
        }
        if (EchoScratch[0].Text.Length == 0) return;
        Echo(min, max, radiusCss, EchoScratch.AsSpan(0, rowCount), px, color);
    }

    // ---------------------------------------------------------------------------------------------
    // Callouts and the plate slip: panels laid like BeginPanel's (drawn behind at last frame's height).

    /// <summary>
    /// A callout drawn at a rectangle (the mock's <c>.callout</c>): the card, a 2px black edge, a hard
    /// 4px black block down-right; <paramref name="missing"/> washes it pink (a piece you haven't
    /// got). What's in it is on the ink again: wrap it in <c>using (Kit.OnInk())</c>, or lay it
    /// with <see cref="BeginCallout"/>, which does.
    /// </summary>
    public static void Callout(Vector2 min, Vector2 max, float radiusCss = Palette.RadiusMenu, bool missing = false)
    {
        var dl = Dl;
        var r = S(radiusCss);
        var b = S(4);
        dl.AddRectFilled(min + new Vector2(b), max + new Vector2(b), Palette.U32(Palette.Black), r);
        dl.AddRectFilled(min, max, Palette.U32(Palette.Card), r);
        if (missing) dl.AddRectFilled(min, max, Palette.U32(Palette.PinkRow), r);
        var e = S(2);
        dl.AddRect(min + new Vector2(e / 2), max - new Vector2(e / 2), Palette.U32(Palette.Black), MathF.Max(0, r - e / 2), ImDrawFlags.None, e);
    }

    private enum PanelKind
    {
        Card,
        Callout,
        Slip,
    }

    /// <summary>A callout or slip being laid: its kind, the ground its notches cut to, where its tear is (0: none).</summary>
    private static readonly Stack<(PanelKind Kind, Vector4 Ground, float TearY, bool Missing)> Sheets = new();

    /// <summary>
    /// A callout laid at the cursor across the box (as <see cref="BeginPanel"/>): padding in CSS px,
    /// what's laid in it on the ink. End it with <see cref="EndCallout"/>.
    /// </summary>
    public static void BeginCallout(string id, float padTop = 12, float padX = 14, float padBottom = 12, bool missing = false)
    {
        var at = At;
        var w = Width;
        var key = ImGui.GetID(id);
        if (Heights.TryGetValue(key, out var h) && h > 0) Callout(at, at + new Vector2(w, h), Palette.RadiusMenu, missing);
        Sheets.Push((PanelKind.Callout, Vector4.Zero, 0, missing));
        Surfaces.Push(false);
        BeginPanelAt(key, at, w, padTop, padX, padBottom, Vector4.Zero, Palette.RadiusMenu);
    }

    public static void EndCallout()
    {
        var (start, w, h) = EndPanelAt();
        if (Surfaces.Count > 0) Surfaces.Pop();
        var sheet = Sheets.Count > 0 ? Sheets.Pop() : default;
        // The first frame (no height yet): the edge alone, the block and card come the next.
        var e = S(2);
        var r = S(Palette.RadiusMenu);
        Dl.AddRect(start + new Vector2(e / 2), start + new Vector2(w, h) - new Vector2(e / 2), Palette.U32(Palette.Black), MathF.Max(0, r - e / 2), ImDrawFlags.None, e);
        _ = sheet;
    }

    /// <summary>
    /// The plate slip (the mock's <c>.slip</c>): a ticket laid at the cursor, the card with its line
    /// edge and 18px corners; <see cref="SlipTear"/> between its two halves draws the dashed tear line
    /// and the two notches cut at its sides (circles of <paramref name="ground"/>: the ground it sits
    /// on, <see cref="Palette.Ground"/> or <see cref="Palette.Ground2"/> in a side pane). End with <see cref="EndSlip"/>.
    /// </summary>
    public static void BeginSlip(string id, float padTop = 16, float padX = 18, Vector4? ground = null)
    {
        var at = At;
        var w = Width;
        var key = ImGui.GetID(id);
        if (Heights.TryGetValue(key, out var h) && h > 0) Dl.AddRectFilled(at, at + new Vector2(w, h), Palette.U32(Palette.Card), S(Palette.RadiusLg));
        Sheets.Push((PanelKind.Slip, ground ?? Palette.Ground, 0, false));
        BeginPanelAt(key, at, w, padTop, padX, 0, Vector4.Zero, Palette.RadiusLg);
    }

    /// <summary>The slip's tear, at the cursor: 14px above it, the dashed line (14px in from either side), 14px under it.</summary>
    public static void SlipTear(float gapCss = 14)
    {
        Gap(gapCss);
        var y = ImGui.GetCursorScreenPos().Y;
        var (start, w, _) = Panel;
        var x0 = start.X + S(14);
        var x1 = start.X + w - S(14);
        var t = S(2);
        // The dashes: 2px, as long as their gaps (as the browser's dashed border).
        var dash = S(6);
        var col = Palette.U32(Palette.LineStrong);
        for (var x = x0; x < x1; x += dash * 2)
            Dl.AddRectFilled(new Vector2(x, y - t / 2), new Vector2(MathF.Min(x + dash, x1), y + t / 2), col);
        if (Sheets.Count > 0 && Sheets.Peek().Kind == PanelKind.Slip)
        {
            var s = Sheets.Pop();
            Sheets.Push(s with { TearY = y });
        }
        Gap(gapCss);
    }

    public static void EndSlip(float padBottom = 16)
    {
        Gap(padBottom);
        var (start, w, h) = EndPanelAt();
        var sheet = Sheets.Count > 0 ? Sheets.Pop() : default;
        var dl = Dl;
        var r = S(Palette.RadiusLg);
        dl.AddRect(start + new Vector2(0.5f), start + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(Palette.Line), r, ImDrawFlags.None, 1f);
        if (sheet.Kind != PanelKind.Slip || sheet.TearY <= 0) return;
        // The notches: the ground cut out at each side (over the card's edge), their own hairline the inner half.
        var nr = S(9);
        var line = Palette.U32(Palette.Line);
        var ground = Palette.U32(sheet.Ground);
        var left = new Vector2(start.X, sheet.TearY);
        var right = new Vector2(start.X + w, sheet.TearY);
        dl.AddCircleFilled(left, nr, ground, 32);
        dl.AddCircleFilled(right, nr, ground, 32);
        dl.PathArcTo(left, nr - 0.5f, -MathF.PI / 2, MathF.PI / 2, 16);
        dl.PathStroke(line, ImDrawFlags.None, 1f);
        dl.PathArcTo(right, nr - 0.5f, MathF.PI / 2, MathF.PI * 1.5f, 16);
        dl.PathStroke(line, ImDrawFlags.None, 1f);
    }

    // ---------------------------------------------------------------------------------------------
    // The two voices.

    /// <summary>
    /// Display words (the mock's <c>.d</c>): Bricolage 800 in capitals, 0.02em apart, Ink (OnField on
    /// the field), cut with "…" to <paramref name="maxWidth"/> (screen px; 0: whole). With
    /// <paramref name="stacked"/>, the field's title as the mock stacks it: its shadows 3px down-right
    /// in FieldDeep and 5px in black under the words (in Ink then, white on the field). Its glyph
    /// box's top is at <paramref name="pos"/>; returns its width.
    /// </summary>
    public static float Display(Vector2 pos, string text, int px, Vector4? color = null, float maxWidth = 0, bool stacked = false, float em = 0.02f)
    {
        var caps = Txt.SpacedFit(text, Face.Display, px, em, maxWidth);
        var dl = Dl;
        if (stacked)
        {
            Txt.Spaced(dl, pos + new Vector2(S(5)), caps, Face.Display, px, Palette.Black, em);
            Txt.Spaced(dl, pos + new Vector2(S(3)), caps, Face.Display, px, Palette.FieldDeep, em);
        }
        return Txt.Spaced(dl, pos, caps, Face.Display, px, color ?? (stacked ? Palette.Ink : Tint.Ink), em);
    }

    /// <summary>Display words' width, as <see cref="Display"/> would draw them (in capitals, spaced).</summary>
    public static float DisplayWidth(string text, int px, float em = 0.02f) => Txt.SpacedWidth(Txt.Upper(text), Face.Display, px, em);

    /// <summary>Display words' glyph box (Bricolage's own line, 1.2em).</summary>
    public static float DisplayHeight(int px) => Txt.Natural(Face.Display, px);

    /// <summary>
    /// The hand voice (the mock's <c>.hand</c>): Shantell Sans 560 in lilac (OnField on the field),
    /// wrapped to <paramref name="maxWidth"/> (screen px; 0: one line) in line boxes of
    /// <paramref name="lineCss"/> (0: 1.3em). Returns its height.
    /// </summary>
    public static float Hand(Vector2 pos, string text, int px = 15, float maxWidth = 0, Vector4? color = null, float lineCss = 0)
    {
        var line = lineCss > 0 ? lineCss : px * 1.3f;
        var run = new Run(text, Face.Hand, px, color ?? Tint.Accent);
        if (maxWidth <= 0)
        {
            Txt.Line(Dl, pos, text, Face.Hand, px, run.Color, line);
            return S(line);
        }
        HandRun[0] = run;
        return Txt.Flow(pos, maxWidth, HandRun, line, out _);
    }

    private static readonly Run[] HandRun = new Run[1];

    // ---------------------------------------------------------------------------------------------
    // The textures.

    /// <summary>
    /// Holo over a rectangle (the mock's <c>--holo</c>, a texture: holo means special): a piece of the
    /// marble tile at the mock's scale (220 CSS px a tile), sliding slowly (still under reduced
    /// motion). Lilac until the texture's made.
    /// </summary>
    public static void Holo(Vector2 min, Vector2 max, float rounding = 0, ImDrawFlags corners = ImDrawFlags.RoundCornersAll)
    {
        var dl = Dl;
        var tex = Textures.Current?.Get(Tex.Holo);
        if (tex == null)
        {
            dl.AddRectFilled(min, max, Palette.U32(Palette.Accent), rounding, corners);
            return;
        }
        var tile = S(220);
        var span = Vector2.Min((max - min) / tile, Vector2.One);
        var t = ImGui.GetTime();
        var uv0 = new Vector2(HoloSlide.Offset(t, span.X, ReducedMotion), HoloSlide.Offset(t + 9, span.Y, ReducedMotion, 31));
        dl.AddImageRounded(tex.Handle, min, max, uv0, uv0 + span, Palette.U32(Vector4.One), rounding, corners);
    }

    /// <summary>The holo underline under where you are (a place in the bar, a tab): 3px, its top corners rounded, its foot at <paramref name="bottom"/>.</summary>
    public static void HoloUnderline(float x0, float x1, float bottom) =>
        Holo(new Vector2(x0, bottom - S(3)), new Vector2(x1, bottom), S(3), ImDrawFlags.RoundCornersTop);

    /// <summary>The logo (the clothes tag), <paramref name="sizeCss"/> square (the mock's 18, 22, 30, 48, 96), the texture nearest its drawn size.</summary>
    public static void Logo(Vector2 pos, float sizeCss = 30)
    {
        var size = S(sizeCss);
        var dl = Dl;
        var tex = Textures.Current?.Get(Textures.LogoFor(size));
        if (tex != null)
        {
            dl.AddImage(tex.Handle, pos, pos + new Vector2(size), Vector2.Zero, Vector2.One, Palette.U32(Vector4.One));
            return;
        }
        // Until it's made: the tag's periwinkle and its hanger.
        dl.AddRectFilled(pos + new Vector2(size * 0.1f), pos + new Vector2(size * 0.95f, size * 0.9f), Palette.U32(Palette.Field), size * 0.12f);
        Icons.Draw(dl, Icon.Hanger, pos + new Vector2(size * 0.22f), size * 0.62f, Palette.White, 1.9f);
    }

    /// <summary>The holo sparkle (new, a wish come true, good news), <paramref name="sizeCss"/> square (the mock's 12, 16, 22).</summary>
    public static void Spark(Vector2 pos, float sizeCss = 12)
    {
        var size = S(sizeCss);
        var dl = Dl;
        var tex = Textures.Current?.Get(Tex.Spark);
        if (tex != null)
        {
            dl.AddImage(tex.Handle, pos, pos + new Vector2(size), Vector2.Zero, Vector2.One, Palette.U32(Vector4.One));
            return;
        }
        Icons.Draw(dl, Icon.Sparkles, pos, size, Palette.Accent);
    }

    /// <summary>The cloud behind an empty page, tinted (the mock's lilac at 20%), <paramref name="sizeCss"/> (120 × 60 in the mock).</summary>
    public static void Cloud(Vector2 pos, Vector2 sizeCss, Vector4? tint = null)
    {
        var tex = Textures.Current?.Get(Tex.Cloud);
        if (tex == null) return;
        Dl.AddImage(tex.Handle, pos, pos + sizeCss * S(1), Vector2.Zero, Vector2.One, Palette.U32(tint ?? Palette.WithAlpha(Palette.Accent, 0.2f)));
    }

    // ---------------------------------------------------------------------------------------------
    // Turning what's drawn (the seal −4°, the sign −5°, Refresh's arrow while it reads).

    /// <summary>
    /// Refresh (the bar's, a panel's): quiet and round, dimmed while it reads (its tip then "Reading…"),
    /// its arrows turning (still under reduced motion). True when pressed.
    /// </summary>
    public static bool RefreshButton(string id, Vector2 pos, bool busy, string tip, float sizeCss = 36, float iconCss = 16)
    {
        var dl = Dl;
        var start = TurnStart(dl);
        var pressed = IconButton(id, pos, Icon.RefreshCw, busy ? "Reading…" : tip, sizeCss, Btn.Quiet, busy, iconCss);
        if (busy && !ReducedMotion) TurnSince(dl, start, pos + new Vector2(S(sizeCss) / 2), (float)(ImGui.GetTime() * 240 % 360));
        return pressed;
    }

    /// <summary>Where the draw list's vertices are now: draw, then <see cref="TurnSince"/> turns what was drawn since.</summary>
    public static int TurnStart(ImDrawListPtr dl) => dl.VtxBuffer.Size;

    /// <summary>Turns every vertex drawn since <paramref name="start"/> by <paramref name="degrees"/> about <paramref name="center"/> (clockwise, as CSS rotate).</summary>
    public static void TurnSince(ImDrawListPtr dl, int start, Vector2 center, float degrees)
    {
        var buf = dl.VtxBuffer;
        var end = buf.Size;
        for (var i = start; i < end; i++)
        {
            ref var v = ref buf.Ref(i);
            v.Pos = Turn.Point(v.Pos, center, degrees);
        }
    }
}

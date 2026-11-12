using System.Linq;
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using GlamourStudio.UI.Model;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>A piece of styled text in a paragraph: words in a face, size and colour, or a dye swatch, or a link.</summary>
public readonly record struct Run(string Text, Face Face, int Px, Vector4 Color, string? Link = null, uint? Swatch = null)
{
    public static Run Note(string text, Vector4? color = null) => new(text, Face.Sans, 13, color ?? Palette.Muted);

    public static Run Dot(uint? rgb) => new(string.Empty, Face.Sans, 13, Palette.Muted, Swatch: rgb ?? 0xFFFFFFFF);
}

/// <summary>
/// Text as the browser lays it out: line boxes of a CSS line height with the glyphs centred in them
/// (half-leading), letter-spacing drawn glyph by glyph, ellipses, and paragraphs of mixed runs
/// wrapped at word boundaries with clickable links.
/// </summary>
public static class Txt
{
    public static Fonts Fonts { get; set; } = null!;

    // ---------------------------------------------------------------------------------------------
    // What's measured, kept: the same words are measured many times a frame, every frame. Every cache
    // here is keyed by everything its answer depends on besides the glyphs, and all of them are
    // emptied together when the glyphs change (Dalamud's global scale, a font rebuild) or one grows
    // past its bound (a list of ever-new words mustn't grow it for ever).

    private const int CacheBound = 2000;

    private static readonly Dictionary<(string Text, Face Face, int Px), Vector2> Sizes = new();
    private static readonly Dictionary<(string Text, Face Face, int Px, float Width), string> Fits = new();
    private static readonly Dictionary<(string Text, Face Face, int Px, float Em), SpacedLayout> SpacedLayouts = new();
    private static readonly Dictionary<(string Text, Face Face, int Px, float Em, float Width), string> SpacedFits = new();
    private static readonly Dictionary<string, string> Uppers = new();
    private static readonly Dictionary<(string Text, Face Face, int Px), RunWords> Words = new();
    private static readonly Dictionary<(string Text, Face Face, int Px), Mixed?> Plans = new();

    private static float stampScale = -1;
    private static int stampGeneration = -1;
    private static Fonts? stampFonts;

    /// <summary>Empties every cache when what they were measured with has changed.</summary>
    private static void Check()
    {
        Fonts.Tick();
        var scale = ImGuiHelpers.GlobalScale;
        var generation = Fonts.Generation;
        if (scale == stampScale && generation == stampGeneration && ReferenceEquals(Fonts, stampFonts)) return;
        stampScale = scale;
        stampGeneration = generation;
        stampFonts = Fonts;
        Sizes.Clear();
        Fits.Clear();
        SpacedLayouts.Clear();
        SpacedFits.Clear();
        Words.Clear();
        Plans.Clear();
    }

    private static void Keep<TKey, TValue>(Dictionary<TKey, TValue> cache, TKey key, TValue value) where TKey : notnull
    {
        if (cache.Count >= CacheBound) cache.Clear();
        cache[key] = value;
    }

    public static Vector2 Measure(string text, Face face, int px)
    {
        Check();
        if (Sizes.TryGetValue((text, face, px), out var size)) return size;
        using (Fonts.Push(face, px))
        {
            size = ImGui.CalcTextSize(text);
            if (PlanOf(text, face, px) != null) size.X = WidthPushed(text, face, px);
        }
        Keep(Sizes, (text, face, px), size);
        return size;
    }

    // ---------------------------------------------------------------------------------------------
    // Characters a face hasn't got (a Japanese look's name, a maker's hangul): drawn in the
    // fallback (Noto Sans CJK), scaled to the size asked for and on the face's baseline; one
    // neither has yet is left out and asked for (Fonts.Want), so ImGui's "?" never shows.

    /// <summary>A string in runs: each run's words and whether it's in the fallback.</summary>
    private sealed record Mixed(string[] Parts, bool[] Fallback);

    private static readonly HashSet<char> Asked = new();

    /// <summary>The text's runs in this face and size, worked out once (null: the face has it all, the usual case).</summary>
    private static Mixed? PlanOf(string text, Face face, int px)
    {
        if (Glyphs.IsPlainAscii(text) || face is Face.Echo or Face.Fallback) return null;
        Check();
        if (Plans.TryGetValue((text, face, px), out var plan)) return plan;
        Asked.Clear();
        List<(string Text, bool Fallback)>? runs;
        // Each font looked at only while it's pushed: Dalamud promises a pushed font only until it's
        // popped (and the atlas may be swapped for a rebuilt one in between).
        using (Fonts.Push(face, px))
        {
            var font = ImGui.GetFont();
            using (Fonts.Fallback(px).Handle.Push())
            {
                var fallback = ImGui.GetFont();
                runs = Glyphs.Split(text, c => Has(font, c), c => Has(fallback, c), Asked);
            }
        }
        if (Asked.Count > 0) Fonts.Want(Asked);
        plan = runs == null ? null : new Mixed(runs.ConvertAll(r => r.Text).ToArray(), runs.ConvertAll(r => r.Fallback).ToArray());
        Keep(Plans, (text, face, px), plan);
        return plan;
    }

    private static unsafe bool Has(ImFontPtr font, char c) => font.FindGlyphNoFallback(c) != null;

    /// <summary>True when some of the text isn't in the face (it's drawn, in part, in the fallback).</summary>
    public static bool NeedsFallback(string text, Face face, int px) => PlanOf(text, face, px) != null;

    /// <summary>
    /// For text ImGui draws itself (a tooltip, a text field): the face pushed, or the fallback at
    /// that size when the text has characters the face hasn't got (up to 15px it has Latin-1 too:
    /// <see cref="Fonts"/>' FallbackLatinUpTo, which covers every size this is asked for). Pair with
    /// <see cref="Drawable"/> for the words.
    /// </summary>
    public static IDisposable PushFor(string text, Face face, int px) =>
        NeedsFallback(text, face, px) ? Fonts.Fallback(px).Handle.Push() : Fonts.Push(face, px);

    /// <summary>The text without the characters neither the face nor the fallback has yet (for ImGui to draw: no "?").</summary>
    public static string Drawable(string text, Face face, int px) =>
        PlanOf(text, face, px) is { } plan ? string.Concat(plan.Parts) : text;

    /// <summary>The text's width, its face already pushed.</summary>
    private static float WidthPushed(string text, Face face, int px)
    {
        var plan = PlanOf(text, face, px);
        if (plan == null) return ImGui.CalcTextSize(text).X;
        var w = 0f;
        for (var i = 0; i < plan.Parts.Length; i++)
        {
            if (!plan.Fallback[i])
            {
                w += ImGui.CalcTextSize(plan.Parts[i]).X;
                continue;
            }
            var (handle, at) = Fonts.Fallback(px);
            using (handle.Push()) w += ImGui.CalcTextSize(plan.Parts[i]).X * px / at;
        }
        return w;
    }

    /// <summary>Draws the text with its glyph box's top at <paramref name="pos"/>, its face already pushed; returns its width.</summary>
    private static float Put(ImDrawListPtr dl, Vector2 pos, string text, Face face, int px, uint col)
    {
        var plan = PlanOf(text, face, px);
        if (plan == null)
        {
            dl.AddText(Snap(pos), col, text);
            return ImGui.CalcTextSize(text).X;
        }
        var font = ImGui.GetFont();
        var size = ImGui.GetFontSize();
        var baseline = pos.Y + font.Ascent * (size / MathF.Max(1, font.FontSize));
        var x = pos.X;
        for (var i = 0; i < plan.Parts.Length; i++)
        {
            var part = plan.Parts[i];
            if (!plan.Fallback[i])
            {
                dl.AddText(Snap(new Vector2(x, pos.Y)), col, part);
                x += ImGui.CalcTextSize(part).X;
                continue;
            }
            var (handle, at) = Fonts.Fallback(px);
            using (handle.Push())
            {
                var f = ImGui.GetFont();
                var k = (float)px / at;
                var fs = ImGui.GetFontSize() * k;
                // On the face's baseline.
                var top = baseline - f.Ascent * (fs / MathF.Max(1, f.FontSize));
                dl.AddText(f, fs, Snap(new Vector2(x, top)), col, part);
                x += ImGui.CalcTextSize(part).X * k;
            }
        }
        return x - pos.X;
    }

    /// <summary>The text in capitals (as the spaced labels are), made once rather than every frame.</summary>
    public static string Upper(string text)
    {
        if (Uppers.TryGetValue(text, out var caps)) return caps;
        caps = text.ToUpperInvariant();
        Keep(Uppers, text, caps);
        return caps;
    }

    private static readonly Dictionary<string, string> Sentences = new();

    /// <summary>The text with its first letter a capital ("trying it on…" → "Trying it on…"), made once.</summary>
    public static string Sentence(string text)
    {
        if (text.Length == 0 || !char.IsLower(text[0])) return text;
        if (Sentences.TryGetValue(text, out var s)) return s;
        s = char.ToUpperInvariant(text[0]) + text[1..];
        Keep(Sentences, text, s);
        return s;
    }

    /// <summary>The font's own line (ImGui's font size): CSS "line-height: normal".</summary>
    public static float Natural(Face face, int px)
    {
        using (Fonts.Push(face, px)) return ImGui.GetFontSize();
    }

    /// <summary>The ascent, for lining up baselines of different sizes.</summary>
    public static float Ascent(Face face, int px)
    {
        using (Fonts.Push(face, px)) return ImGui.GetFont().Ascent;
    }

    /// <summary>Draws text with its glyph box's top at <paramref name="pos"/>; returns its width.</summary>
    public static float Draw(ImDrawListPtr dl, Vector2 pos, string text, Face face, int px, Vector4 color)
    {
        using (Fonts.Push(face, px)) return Put(dl, pos, text, face, px, Palette.U32(color));
    }

    /// <summary>Draws one line centred in a CSS line box <paramref name="lineCss"/> tall (0: the font's own line).</summary>
    public static float Line(ImDrawListPtr dl, Vector2 lineTop, string text, Face face, int px, Vector4 color, float lineCss = 0)
    {
        using (Fonts.Push(face, px))
        {
            var size = ImGui.GetFontSize();
            var box = lineCss > 0 ? S(lineCss) : size;
            return Put(dl, lineTop + new Vector2(0, (box - size) / 2), text, face, px, Palette.U32(color));
        }
    }

    /// <summary>Each of the first 256 characters as a string, made once (spaced capitals are drawn a letter at a time, every frame).</summary>
    private static readonly string[] OneChar = Enumerable.Range(0, 256).Select(c => ((char)c).ToString()).ToArray();

    /// <summary>Spaced text laid out once: each letter as a string, where it starts, and the whole width.</summary>
    private sealed record SpacedLayout(string[] Glyphs, float[] Xs, float Width);

    private static SpacedLayout LayOutSpaced(string text, Face face, int px, float em)
    {
        Check();
        if (SpacedLayouts.TryGetValue((text, face, px, em), out var layout)) return layout;
        var glyphs = new string[text.Length];
        var xs = new float[text.Length];
        var x = 0f;
        using (Fonts.Push(face, px))
        {
            var spacing = S(px * em);
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                var s = ch < OneChar.Length ? OneChar[ch] : ch.ToString();
                glyphs[i] = s;
                xs[i] = x;
                x += WidthPushed(s, face, px) + spacing;
            }
        }
        layout = new SpacedLayout(glyphs, xs, x);
        Keep(SpacedLayouts, (text, face, px, em), layout);
        return layout;
    }

    /// <summary>
    /// Letter-spaced text (CSS letter-spacing, which also follows the last letter): eyebrows at
    /// .18em, stamps at .1em. Returns the width.
    /// </summary>
    public static float Spaced(ImDrawListPtr dl, Vector2 pos, string text, Face face, int px, Vector4 color, float em, bool draw = true)
    {
        var layout = LayOutSpaced(text, face, px, em);
        if (!draw) return layout.Width;
        using (Fonts.Push(face, px))
        {
            var col = Palette.U32(color);
            for (var i = 0; i < layout.Glyphs.Length; i++)
                Put(dl, new Vector2(pos.X + layout.Xs[i], pos.Y), layout.Glyphs[i], face, px, col);
        }
        return layout.Width;
    }

    public static float SpacedWidth(string text, Face face, int px, float em) => LayOutSpaced(text, face, px, em).Width;

    /// <summary>
    /// The text in spaced capitals, cut with "…" (two letters at a time, as the eyebrows always have
    /// been) until it fits <paramref name="maxWidth"/> (0: whole).
    /// </summary>
    public static string SpacedFit(string text, Face face, int px, float em, float maxWidth)
    {
        var caps = Upper(text);
        if (maxWidth <= 0) return caps;
        Check();
        if (SpacedFits.TryGetValue((caps, face, px, em, maxWidth), out var cut)) return cut;
        cut = caps;
        while (cut.Length > 1 && SpacedWidth(cut, face, px, em) > maxWidth)
            cut = cut[..^2].TrimEnd() + "…";
        Keep(SpacedFits, (caps, face, px, em, maxWidth), cut);
        return cut;
    }

    /// <summary>The text cut to fit a width, with an ellipsis.</summary>
    public static string Fit(string text, Face face, int px, float width)
    {
        if (Measure(text, face, px).X <= width) return text;
        if (Fits.TryGetValue((text, face, px, width), out var cut)) return cut;
        using (Fonts.Push(face, px))
        {
            const string dots = "…";
            var lo = 0;
            var hi = text.Length;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (WidthPushed(text[..mid].TrimEnd() + dots, face, px) <= width) lo = mid;
                else hi = mid - 1;
            }
            cut = lo == 0 ? dots : text[..lo].TrimEnd() + dots;
        }
        Keep(Fits, (text, face, px, width), cut);
        return cut;
    }

    private readonly record struct Token(string Text, int Run, float Width, float Trailing);

    /// <summary>A run's words, measured once: each word, its width, and the width of the spaces after it.</summary>
    private sealed record RunWords(string[] Texts, float[] Widths, float[] Trailing);

    private static RunWords WordsOf(string text, Face face, int px)
    {
        Check();
        if (Words.TryGetValue((text, face, px), out var words)) return words;
        var texts = new List<string>();
        var widths = new List<float>();
        var trailing = new List<float>();
        using (Fonts.Push(face, px))
        {
            var space = ImGui.CalcTextSize(" ").X;
            var i = 0;
            while (i < text.Length)
            {
                var j = i;
                while (j < text.Length && text[j] != ' ') j++;
                var word = text[i..j];
                var spaces = 0;
                while (j < text.Length && text[j] == ' ')
                {
                    spaces++;
                    j++;
                }
                texts.Add(word);
                widths.Add(word.Length == 0 ? 0 : WidthPushed(word, face, px));
                trailing.Add(spaces * space);
                i = j;
            }
        }
        words = new RunWords(texts.ToArray(), widths.ToArray(), trailing.ToArray());
        Keep(Words, (text, face, px), words);
        return words;
    }

    /// <summary>The words and lines of the paragraph being laid, reused (the window is drawn on one thread, and a paragraph never lays another).</summary>
    private static readonly List<Token> FlowTokens = new();

    private static readonly List<(int From, int To, float Width)> FlowLines = new();

    private static bool flowing;

    /// <summary>
    /// Lays out runs of text wrapped to <paramref name="width"/>, in line boxes <paramref name="lineCss"/>
    /// CSS pixels tall, from <paramref name="pos"/>. Links are underlined on hover and clickable;
    /// <paramref name="clicked"/> is the link pressed this frame. Leaves the cursor where it was.
    /// Returns the height.
    /// </summary>
    public static float Flow(Vector2 pos, float width, IReadOnlyList<Run> runs, float lineCss, out string? clicked, bool center = false, bool draw = true)
    {
        // Should one ever lay another part way, it gets lists of its own rather than the shared ones.
        var nested = flowing;
        var tokens = nested ? new List<Token>() : FlowTokens;
        var lines = nested ? new List<(int From, int To, float Width)>() : FlowLines;
        tokens.Clear();
        lines.Clear();
        flowing = true;
        try
        {
            return FlowInto(tokens, lines, pos, width, runs, lineCss, out clicked, center, draw);
        }
        finally
        {
            flowing = nested;
        }
    }

    private static float FlowInto(List<Token> tokens, List<(int From, int To, float Width)> lines, Vector2 pos, float width, IReadOnlyList<Run> runs,
        float lineCss, out string? clicked, bool center, bool draw)
    {
        clicked = null;
        for (var r = 0; r < runs.Count; r++)
        {
            var run = runs[r];
            if (run.Swatch != null)
            {
                tokens.Add(new Token(string.Empty, r, S(10), S(6)));
                continue;
            }
            var words = WordsOf(run.Text, run.Face, run.Px);
            for (var k = 0; k < words.Texts.Length; k++) tokens.Add(new Token(words.Texts[k], r, words.Widths[k], words.Trailing[k]));
        }

        // Break into lines.
        var from = 0;
        var x = 0f;
        var lineWidth = 0f;
        for (var t = 0; t < tokens.Count; t++)
        {
            var tok = tokens[t];
            if (t > from && x + tok.Width > width + 0.5f)
            {
                lines.Add((from, t, lineWidth));
                from = t;
                x = 0;
            }
            lineWidth = x + tok.Width;
            x += tok.Width + tok.Trailing;
        }
        if (from < tokens.Count) lines.Add((from, tokens.Count, lineWidth));

        var box = S(lineCss);
        if (!draw) return lines.Count * box;

        var dl = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();

        // The link pointed at, as a whole: every word of its run lights up together.
        var hoveredRun = -1;
        if (!Kit.Blocked && ImGui.IsWindowHovered())
        {
            var hy = pos.Y;
            foreach (var (a, b, w) in lines)
            {
                var hx = pos.X + (center ? (width - w) / 2 : 0);
                for (var t = a; t < b && hoveredRun < 0; t++)
                {
                    var tok = tokens[t];
                    var link = runs[tok.Run].Link;
                    var wide = tok.Width + (t < b - 1 && link != null && runs[tokens[t + 1].Run].Link == link ? tok.Trailing : 0);
                    if (link != null && ImGui.IsMouseHoveringRect(new Vector2(hx, hy), new Vector2(hx + wide, hy + box))) hoveredRun = tok.Run;
                    hx += tok.Width + tok.Trailing;
                }
                hy += box;
            }
        }

        var y = pos.Y;
        var linkIndex = 0;
        // The face is pushed once for each stretch of words in the same face and size, not for every word.
        IDisposable? pushed = null;
        var pushedFace = default(Face);
        var pushedPx = -1;
        var size = 0f;
        try
        {
            foreach (var (a, b, w) in lines)
            {
                var lx = pos.X + (center ? (width - w) / 2 : 0);
                for (var t = a; t < b; t++)
                {
                    var tok = tokens[t];
                    var run = runs[tok.Run];
                    if (run.Swatch is { } rgb)
                    {
                        Kit.Swatch(dl, new Vector2(lx + S(5), y + box / 2), rgb == 0xFFFFFFFF ? null : rgb);
                        lx += tok.Width + tok.Trailing;
                        continue;
                    }
                    if (pushed == null || pushedFace != run.Face || pushedPx != run.Px)
                    {
                        var last = pushed;
                        pushed = null;
                        last?.Dispose();
                        pushed = Fonts.Push(run.Face, run.Px);
                        pushedFace = run.Face;
                        pushedPx = run.Px;
                        size = ImGui.GetFontSize();
                    }
                    var top = new Vector2(lx, y + (box - size) / 2);
                    var color = run.Color;
                    if (run.Link != null)
                    {
                        var min = new Vector2(lx, y);
                        var max = new Vector2(lx + tok.Width + (t < b - 1 && runs[tokens[t + 1].Run].Link == run.Link ? tok.Trailing : 0), y + box);
                        ImGui.SetCursorScreenPos(min);
                        var hit = ImGui.InvisibleButton($"##link{run.Link}{linkIndex++}", Vector2.Max(max - min, new Vector2(1)));
                        if (hit && !Kit.Blocked) clicked = run.Link;
                        if (hoveredRun == tok.Run)
                        {
                            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                            color = Palette.Ink;
                        }
                    }
                    if (tok.Text.Length > 0) Put(dl, top, tok.Text, run.Face, run.Px, Palette.U32(color));
                    lx += tok.Width + tok.Trailing;
                }
                y += box;
            }
        }
        finally
        {
            pushed?.Dispose();
        }
        ImGui.SetCursorScreenPos(cursor);
        return lines.Count * box;
    }

    /// <summary>A paragraph at the cursor across the box's width; the cursor moves below it.</summary>
    public static string? Para(IReadOnlyList<Run> runs, float lineCss = 19.5f, bool center = false, float? width = null)
    {
        var at = Kit.At;
        var w = width ?? Kit.Width;
        var h = Flow(at, w, runs, lineCss, out var clicked, center);
        Kit.Reserve(at, w, h);
        return clicked;
    }

    /// <summary>A note (13px ash, line-height 1.5) at the cursor.</summary>
    public static void Note(string text, Vector4? color = null, bool center = false) => Para(new[] { Run.Note(text, color) }, 19.5f, center);

    private static Vector2 Snap(Vector2 p) => new(MathF.Round(p.X), MathF.Round(p.Y));
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>The buttons of the design system (the mock's <c>.btn</c> and <c>.sticker</c>).</summary>
public enum Btn
{
    /// <summary>The screen's one thing to do: drawn as the sticker (<see cref="Kit.Sticker"/>), turned over on the field.</summary>
    Primary,

    /// <summary>Outlined: the strong edge, ink words; pointed at, the lilac edge and wash. Everything else you press.</summary>
    Glass,

    /// <summary>Words only (Body), the lilac wash when pointed at.</summary>
    Quiet,

    /// <summary>A danger outline: Stop, Unlink in a row.</summary>
    Danger,

    /// <summary>The honest red filled, dark words: a danger question's answer (Forget, Unlink).</summary>
    DangerFilled,

    /// <summary>A toggle that's on: the field's periwinkle with ink words (the segmented pick's).</summary>
    Picked,

    /// <summary>The raised pill: the soft tile fill, ink words (the mock's <c>.btn.ink</c>).</summary>
    Ink,

    /// <summary>Amber edge, words and wash: a button pressed once that asks to be pressed again (something to see to first).</summary>
    Amber,
}

/// <summary>Control heights, as the mock's: 28 (xs), 34 (sm), 40, 46 for the one big call.</summary>
public enum BtnSize
{
    Tiny,
    Sm,
    Md,
    Lg,
}

/// <summary>The colour of a status pill (the mock's <c>.who</c> states).</summary>
public enum Tone
{
    /// <summary>Ice: done, sent, linked.</summary>
    Success,

    /// <summary>Lilac, its dot breathing: something's going on (reading, filling a plate).</summary>
    Live,

    /// <summary>Lilac, quieter: ready, waiting for you.</summary>
    Waiting,

    /// <summary>Grey: nothing going on.</summary>
    Neutral,

    /// <summary>Red: something's wrong.</summary>
    Danger,

    /// <summary>Recording what you do: the danger edge and a ringed red dot, but the words stay quiet.</summary>
    Recording,
}

/// <summary>
/// The site's controls, as Glamour Studio draws them since the "Character Sheet" redesign, with
/// ImGui's draw lists: the sticker (the one thing to do) and outlined and quiet pills, round icon
/// buttons, switches, segmented pills, cards with 1px edges, the dashed "add" outline, the bar,
/// tick boxes, steppers, text fields, tooltips and menus. Each takes the screen position to draw at
/// (layout is the views'), hit-tests with an invisible ImGui button (so keyboard navigation works),
/// and shows hover, press, focus (a 2px lilac ring, 2px out) and disabled (50% for buttons and the
/// sticker, 40% for small controls). On a field (<see cref="FieldPanel"/>) they turn over to the
/// field's colours (<see cref="Tint"/>). Made from Housing Studio's plugin kit (by way of Raid
/// Planner's), in Glamour Studio's colours.
/// </summary>
public static partial class Kit
{
    /// <summary>While a dialog is open, everything under it is drawn but can't be pointed at or pressed.</summary>
    public static bool Blocked { get; set; }

    // ---------------------------------------------------------------------------------------------
    // Layout: a stack of boxes (left edge and width) the views lay rows into, top to bottom.

    private static readonly Stack<(float Left, float Width)> Boxes = new();

    public static void PushBox(float left, float width) => Boxes.Push((left, MathF.Max(0, width)));

    /// <summary>At the start of each frame: nothing left over from a frame whose drawing threw part way.</summary>
    public static void ResetFrame()
    {
        Boxes.Clear();
        Panels.Clear();
        Panes.Clear();
        Surfaces.Clear();
        Sheets.Clear();
        // A control that threw while drawn disabled left the whole window faded (1.0.0).
        Palette.Opacity = 1f;
    }

    public static void PopBox() => Boxes.Pop();

    public static float Left => Boxes.Count > 0 ? Boxes.Peek().Left : ImGui.GetCursorScreenPos().X;

    public static float Width => Boxes.Count > 0 ? Boxes.Peek().Width : ImGui.GetContentRegionAvail().X;

    public static float Right => Left + Width;

    /// <summary>Where the next row starts: the box's left edge, at the cursor's height.</summary>
    public static Vector2 At => new(Left, ImGui.GetCursorScreenPos().Y);

    /// <summary>Takes up a row's space, so the next row goes under it.</summary>
    public static void Reserve(Vector2 start, float width, float height)
    {
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(MathF.Max(width, 1), MathF.Max(height, 0)));
    }

    /// <summary>A vertical gap of CSS pixels.</summary>
    public static void Gap(float px)
    {
        var at = At;
        Reserve(at, 1, S(px));
    }

    public static ImDrawListPtr Dl => ImGui.GetWindowDrawList();

    // ---------------------------------------------------------------------------------------------
    // Hit testing.

    /// <summary>An invisible button over a rectangle: (clicked, hovered, held). Disabled and blocked areas never react.</summary>
    public static (bool Clicked, bool Hovered, bool Held) Hit(string id, Vector2 pos, Vector2 size, bool disabled = false)
    {
        var keep = ImGui.GetCursorScreenPos();
        var hit = HitHere(id, pos, size, disabled);
        ImGui.SetCursorScreenPos(keep);
        return hit;
    }

    /// <summary>As <see cref="Hit"/>, but the cursor stays after the button (a menu's rows go one under another).</summary>
    private static (bool Clicked, bool Hovered, bool Held) HitHere(string id, Vector2 pos, Vector2 size, bool disabled = false)
    {
        ImGui.SetCursorScreenPos(pos);
        var clicked = ImGui.InvisibleButton(id, Vector2.Max(size, new Vector2(1)));
        var live = !disabled && !Blocked;
        var hovered = live && ImGui.IsItemHovered();
        var held = live && ImGui.IsItemActive();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return (clicked && live, hovered, held);
    }

    /// <summary>The small numbers as strings, made once: the ids of a control's parts (under its own id), drawn every frame.</summary>
    private static readonly string[] Numbers = Enumerable.Range(0, 64).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    private static string Part(int i) => i >= 0 && i < Numbers.Length ? Numbers[i] : i.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The focus ring (lilac, 2px out, as the site's), when the keyboard is driving and the last item has focus.</summary>
    public static void FocusRing(Vector2 min, Vector2 max, float radius)
    {
        if (Blocked || !ImGui.IsItemFocused() || !ImGui.GetIO().NavVisible) return;
        var o = S(2);
        Dl.AddRect(min - new Vector2(o + S(1)), max + new Vector2(o + S(1)), Palette.U32(Palette.Accent), radius + o + S(1), ImDrawFlags.None, S(2));
    }

    // ---------------------------------------------------------------------------------------------
    // Buttons.

    /// <summary>The mock's pills: xs 28 (12px words), sm 34 (13px), 40 (13.5px), lg 46 (14.5px); their padding either side.</summary>
    private static (float Height, float Pad, int Px) Spec(BtnSize size) => size switch
    {
        BtnSize.Tiny => (28, 10, 12),
        BtnSize.Sm => (34, 13, 13),
        BtnSize.Lg => (46, 21, 15),
        _ => (40, 16, 14),
    };

    /// <summary>Every pill's words in Figtree 600, as the mock's (the sticker's are 800).</summary>
    private static Face FaceOf(Btn style) => Face.SansSemibold;

    /// <summary>The gap between a pill's icon and its words: 7, 5 on the smallest.</summary>
    private static float IconGap(BtnSize size) => S(size == BtnSize.Tiny ? 5 : 7);

    private static StickerSize StickerOf(BtnSize size) => size switch
    {
        BtnSize.Tiny or BtnSize.Sm => StickerSize.Sm,
        BtnSize.Lg => StickerSize.Lg,
        _ => StickerSize.Md,
    };

    public static float ButtonHeight(BtnSize size) => S(Spec(size).Height);

    public static float ButtonWidth(string label, BtnSize size = BtnSize.Md, Icon? icon = null, Btn style = Btn.Glass)
    {
        if (style == Btn.Primary && label.Length > 0) return StickerWidth(label, StickerOf(size), icon);
        var (h, pad, px) = Spec(size);
        if (label.Length == 0) return S(h);
        var text = Txt.Measure(label, FaceOf(style), px).X;
        return S(pad) * 2 + text + (icon != null ? S(16) + IconGap(size) : 0);
    }

    /// <summary>
    /// A pill button (the mock's <c>.btn</c>), or the sticker for <see cref="Btn.Primary"/>. Width 0
    /// fits its words; an empty label makes it a round icon button. <paramref name="width"/> and
    /// <paramref name="height"/> are screen px. A disabled one is drawn at half strength and its
    /// <paramref name="tip"/> (say why) still shows.
    /// </summary>
    public static bool Button(string id, Vector2 pos, string label, Btn style = Btn.Glass, BtnSize size = BtnSize.Md, Icon? icon = null,
        float width = 0, bool disabled = false, string? tip = null, float height = 0, float iconCss = 16)
    {
        if (style == Btn.Primary && label.Length > 0)
            return Sticker(id, pos, label, StickerOf(size), icon, width, disabled, tip);
        var (hCss, padCss, px) = Spec(size);
        var h = height > 0 ? height : S(hCss);
        var face = FaceOf(style);
        var gap = IconGap(size);
        // A button that fits its words measures them once, for its width and for where they go.
        var textW = width <= 0 && label.Length > 0 ? Txt.Measure(label, face, px).X : 0;
        var w = width > 0 ? width : label.Length == 0 ? h : S(padCss) * 2 + textW + (icon != null ? S(16) + gap : 0);
        var (clicked, hovered, held) = Hit(id, pos, new Vector2(w, h), disabled);
        if (tip != null && !Blocked && ImGui.IsMouseHoveringRect(pos, pos + new Vector2(w, h)) && ImGui.IsWindowHovered()) Tooltip(tip);

        var old = Palette.Opacity;
        if (disabled) Palette.Opacity = old * 0.5f;
        var field = IsOnField;
        Vector4 fill, edge, ink;
        switch (style)
        {
            case Btn.Primary:
                // (A round icon-only Primary: the sticker's colours in a circle.)
                fill = field ? Palette.OnField : hovered ? Palette.FieldHi : Palette.Field;
                edge = field ? Palette.Black : Palette.StickerEdge;
                ink = field ? Palette.Ink : Palette.OnField;
                break;
            case Btn.Ink:
                fill = held ? Palette.Raised : hovered ? Palette.BlushTile : Palette.Tile;
                edge = hovered ? Palette.Accent : Palette.LineStrong;
                ink = Palette.Ink;
                break;
            case Btn.Quiet:
                fill = held ? Tint.Wash * new Vector4(1, 1, 1, 1.6f) : hovered ? Tint.Wash : Vector4.Zero;
                edge = Vector4.Zero;
                ink = hovered ? Tint.Ink : Tint.Body;
                break;
            case Btn.Danger:
                fill = held ? Palette.DangerWash : hovered ? Palette.WithAlpha(Palette.Danger, 0.06f) : Vector4.Zero;
                edge = hovered ? Palette.Danger : Palette.WithAlpha(Palette.Danger, 0.5f);
                ink = Palette.Danger;
                break;
            case Btn.DangerFilled:
                fill = held ? Palette.DangerSolid : hovered ? Palette.Mix(Palette.Danger, Palette.Ink, 0.18f) : Palette.Danger;
                edge = fill;
                ink = Palette.OnDanger;
                break;
            case Btn.Amber:
                fill = held ? Palette.WithAlpha(Palette.Amber, 0.22f) : hovered ? Palette.WithAlpha(Palette.Amber, 0.18f) : Palette.AmberWash;
                edge = Palette.Amber;
                ink = Palette.Amber;
                break;
            case Btn.Picked:
                fill = field ? Palette.OnField : held ? Palette.FieldDeep : hovered ? Palette.FieldHi : Palette.Field;
                edge = fill;
                ink = field ? Palette.Ink : Palette.OnField;
                break;
            default:
                // Outlined; pointed at, the lilac edge and its wash (the mock's .btn.hover); on the field, its ink.
                fill = held ? Tint.Wash * new Vector4(1, 1, 1, 1.6f) : hovered ? Tint.Wash : Vector4.Zero;
                edge = hovered ? (field ? Palette.OnField : Palette.Accent) : Tint.Edge;
                ink = Tint.Ink;
                break;
        }
        var dl = Dl;
        var max = pos + new Vector2(w, h);
        var r = h / 2;
        if (fill.W > 0) dl.AddRectFilled(pos, max, Palette.U32(fill), r);
        if (edge.W > 0) dl.AddRect(pos + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(edge), r, ImDrawFlags.None, 1f);

        var iconPx = S(iconCss);
        // A label wider than a button of a set width is cut to fit (1.0.0: it ran past the pill and the window).
        if (width > 0 && label.Length > 0)
        {
            label = Txt.Fit(label, face, px, MathF.Max(0, w - S(padCss) * 2 - (icon != null ? iconPx + gap : 0) + 1));
            textW = Txt.Measure(label, face, px).X;
        }
        var content = (icon != null ? iconPx : 0) + (icon != null && label.Length > 0 ? gap : 0) + textW;
        var x = pos.X + (w - content) / 2;
        if (icon is { } ic)
        {
            Icons.Draw(dl, ic, new Vector2(x, pos.Y + (h - iconPx) / 2), iconPx, ink);
            x += iconPx + (label.Length > 0 ? gap : 0);
        }
        if (label.Length > 0) Txt.Line(dl, new Vector2(x, pos.Y), label, face, px, ink, h / S(1));
        Palette.Opacity = old;
        FocusRing(pos, max, r);
        return clicked;
    }

    /// <summary>A round (or square-pill) button with only an icon; its words are the tooltip, as aria-label is.</summary>
    public static bool IconButton(string id, Vector2 pos, Icon icon, string tip, float size = 40, Btn style = Btn.Glass, bool disabled = false, float iconCss = 16) =>
        Button(id, pos, string.Empty, style, BtnSize.Md, icon, S(size), disabled, tip, S(size), iconCss);

    // ---------------------------------------------------------------------------------------------
    // Switch, segmented pill, chips, pills.

    /// <summary>
    /// The mock's switch, 40×24: off, the soft tile with the strong edge and a muted 16px knob; on,
    /// the field's periwinkle with an ink knob. Returns true when flipped.
    /// </summary>
    public static bool Switch(string id, Vector2 pos, bool on, bool disabled = false)
    {
        var size = new Vector2(S(40), S(24));
        var (clicked, hovered, _) = Hit(id, pos, size, disabled);
        var storage = ImGui.GetStateStorage();
        var key = ImGui.GetID(id + "##knob");
        var target = on ? 1f : 0f;
        var t = storage.GetFloat(key, target);
        // The site's 160ms.
        t = ReducedMotion ? target : MoveTowards(t, target, ImGui.GetIO().DeltaTime / 0.16f);
        storage.SetFloat(key, t);

        var old = Palette.Opacity;
        if (disabled) Palette.Opacity = old * 0.4f;
        var dl = Dl;
        var max = pos + size;
        var r = size.Y / 2;
        var e = Ease(t);
        dl.AddRectFilled(pos, max, Palette.U32(Mix(Palette.Tile, Palette.Field, e)), r);
        var edge = Mix(hovered ? Palette.LineHover : Palette.LineStrong, Palette.Field, e);
        dl.AddRect(pos + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(edge), r, ImDrawFlags.None, 1f);
        var knob = pos + new Vector2(S(3 + 8) + S(16) * e, size.Y / 2);
        dl.AddCircleFilled(knob, S(8), Palette.U32(Mix(Palette.Muted, Palette.OnField, e)), 24);
        Palette.Opacity = old;
        FocusRing(pos, max, r);
        return clicked;
    }

    /// <summary>
    /// Where a group's picked pill is drawn this frame, from the group's left edge: gliding to the
    /// picked item's place and width, as the site's segmented switches do (glide.ts, over about
    /// --gs-glide's 420ms), and put there at once the first time the group's drawn and under reduced
    /// motion.
    /// </summary>
    internal static (float X, float W) Glide(string id, float x, float w)
    {
        var storage = ImGui.GetStateStorage();
        var kx = ImGui.GetID(id + "##glide-x");
        var kw = ImGui.GetID(id + "##glide-w");
        var seen = ImGui.GetID(id + "##glide-seen");
        if (ReducedMotion || storage.GetInt(seen, 0) == 0)
        {
            storage.SetInt(seen, 1);
            storage.SetFloat(kx, x);
            storage.SetFloat(kw, w);
            return (x, w);
        }
        var gx = storage.GetFloat(kx, x);
        var gw = storage.GetFloat(kw, w);
        // (Most of the way in about 0.3s, slowing as it arrives.)
        var k = 1 - MathF.Exp(-ImGui.GetIO().DeltaTime / 0.075f);
        gx = MathF.Abs(x - gx) < 0.5f ? x : gx + (x - gx) * k;
        gw = MathF.Abs(w - gw) < 0.5f ? w : gw + (w - gw) * k;
        storage.SetFloat(kx, gx);
        storage.SetFloat(kw, gw);
        return (gx, gw);
    }

    private static float MoveTowards(float v, float target, float step) =>
        v < target ? MathF.Min(target, v + step) : MathF.Max(target, v - step);

    /// <summary>cubic-bezier(.3,.7,.4,1), near enough.</summary>
    private static float Ease(float t) => 1 - MathF.Pow(1 - t, 3);

    private static Vector4 Mix(Vector4 a, Vector4 b, float t) => a + (b - a) * t;

    /// <summary>The segmented pill's width; with <paramref name="maxWidth"/>, what it shrinks to (its words keep at least 6px either side).</summary>
    public static float SegmentedWidth(IReadOnlyList<string> items, float maxWidth = 0) =>
        SegmentedFrame(items) + items.Count * 2 * SegmentPad(items, maxWidth);

    /// <summary>The pill's edges, inner padding, gaps and words: everything but the words' own padding.</summary>
    private static float SegmentedFrame(IReadOnlyList<string> items)
    {
        var w = S(3) * 2 + S(1) * 2 + S(2) * (items.Count - 1);
        foreach (var s in items) w += Txt.Measure(s, Face.SansMedium, 13).X;
        return w;
    }

    /// <summary>The padding either side of each item's words: 14px, less when the pill must fit a narrower width.</summary>
    private static float SegmentPad(IReadOnlyList<string> items, float maxWidth) => SegmentPad(items, maxWidth, SegmentedFrame(items));

    private static float SegmentPad(IReadOnlyList<string> items, float maxWidth, float frame)
    {
        var pad = S(14);
        if (maxWidth <= 0 || items.Count == 0) return pad;
        var full = frame + items.Count * 2 * pad;
        if (full <= maxWidth) return pad;
        return MathF.Max(S(6), pad - (full - maxWidth) / (items.Count * 2));
    }

    /// <summary>
    /// The segmented pill (Settings → Pace, Ready's Keep / Clear out / Swap): 40 tall outside, 32
    /// inside, the picked one violet with white words. Items whose bit is set in <paramref name="disabledMask"/>
    /// are at 40% and can't be picked; <paramref name="tips"/> are each item's tooltip (null for none).
    /// With <paramref name="maxWidth"/>, the items' padding shrinks so the pill fits.
    /// </summary>
    public static int Segmented(string id, Vector2 pos, IReadOnlyList<string> items, int selected, int disabledMask = 0, float maxWidth = 0,
        IReadOnlyList<string?>? tips = null) =>
        SegmentedPress(id, pos, items, selected, disabledMask, maxWidth, tips) is var pressed and >= 0 ? pressed : selected;

    /// <summary>As <see cref="Segmented"/>, but returns the item pressed (the one already picked too), or -1 when none was.</summary>
    public static int SegmentedPress(string id, Vector2 pos, IReadOnlyList<string> items, int selected, int disabledMask = 0, float maxWidth = 0,
        IReadOnlyList<string?>? tips = null)
    {
        var dl = Dl;
        var frame = SegmentedFrame(items);
        var pad = SegmentPad(items, maxWidth, frame);
        var total = frame + items.Count * 2 * pad;
        var h = S(36);
        dl.AddRectFilled(pos, pos + new Vector2(total, h), Palette.U32(Palette.Card), h / 2);
        dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(total, h) - new Vector2(0.5f), Palette.U32(Palette.Line), h / 2, ImDrawFlags.None, 1f);
        var x = pos.X + S(1) + S(3);
        var result = -1;
        // The picked one's pill, gliding to it.
        if (selected >= 0 && selected < items.Count)
        {
            var px = x;
            for (var i = 0; i < selected; i++) px += Txt.Measure(items[i], Face.SansMedium, 13).X + pad * 2 + S(2);
            var (gx, gw) = Glide(id, px - pos.X, Txt.Measure(items[selected], Face.SansMedium, 13).X + pad * 2);
            var gp = new Vector2(pos.X + gx, pos.Y + S(1) + S(3));
            dl.AddRectFilled(gp, gp + new Vector2(gw, S(28)), Palette.U32(Palette.Picked), S(14));
        }
        ImGui.PushID(id);
        // (Popped whatever happens: a throw inside would leave the ID stack unbalanced.)
        try
        {
        for (var i = 0; i < items.Count; i++)
        {
            var w = Txt.Measure(items[i], Face.SansMedium, 13).X + pad * 2;
            var p = new Vector2(x, pos.Y + S(1) + S(3));
            var size = new Vector2(w, S(28));
            var on = i == selected;
            var disabled = (disabledMask & (1 << i)) != 0;
            var (clicked, hovered, _) = Hit(Part(i), p, size, disabled);
            if (clicked) result = i;
            if (tips != null && i < tips.Count && tips[i] is { } tip && !Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(p, p + size))
                Tooltip(tip);
            var old = Palette.Opacity;
            if (disabled) Palette.Opacity = old * 0.4f;
            if (!on && hovered) dl.AddRectFilled(p, p + size, Palette.U32(Palette.AccentWash), size.Y / 2);
            Txt.Line(dl, new Vector2(p.X + pad, p.Y), items[i], Face.SansMedium, 13, on ? Palette.OnPicked : hovered ? Palette.Body : Palette.Muted, 28);
            Palette.Opacity = old;
            FocusRing(p + new Vector2(S(4)), p + size - new Vector2(S(4)), size.Y / 2 - S(4));
            x += w + S(2);
        }
        }
        finally
        {
            ImGui.PopID();
        }
        return result;
    }

    /// <summary>The pill's line colour and its dot's, by tone (the mock's <c>.who</c> states).</summary>
    private static (Vector4 Ink, Vector4 Dot, Vector4 Edge) ToneColors(Tone tone) => tone switch
    {
        Tone.Success => (Palette.Muted, Palette.Ice, Palette.Line),
        Tone.Live => (Palette.Muted, Palette.Accent, Palette.AccentLine),
        Tone.Waiting => (Palette.Muted, Palette.Accent, Palette.Line),
        Tone.Danger => (Palette.Danger, Palette.Danger, Palette.DangerEdge),
        Tone.Recording => (Palette.Muted, Palette.Danger, Palette.DangerEdge),
        _ => (Palette.Muted, Palette.Faint, Palette.Line),
    };

    public static float StatusPillWidth(string text) => S(12) * 2 + S(7) + S(8) + Txt.Measure(text, Face.SansMedium, 12).X;

    /// <summary>
    /// A status pill without a face (the header's pill is <see cref="WhoPill"/>): 28 tall on the
    /// card, a 7px dot (ringed while live), 12px words. Returns its width.
    /// </summary>
    public static float StatusPill(Vector2 pos, string text, Tone tone, bool pulse = false, float maxWidth = 0)
    {
        var (ink, dot, edge) = ToneColors(tone);
        var full = StatusPillWidth(text);
        if (maxWidth > 0 && full > maxWidth) text = Txt.Fit(text, Face.SansMedium, 12, maxWidth - (full - Txt.Measure(text, Face.SansMedium, 12).X));
        var w = StatusPillWidth(text);
        var h = S(28);
        var dl = Dl;
        dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(Palette.Card), h / 2);
        dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(edge), h / 2, ImDrawFlags.None, 1f);
        StatusDot(pos + new Vector2(S(12) + S(3.5f), h / 2), tone, pulse);
        Txt.Line(dl, new Vector2(pos.X + S(12 + 7 + 8), pos.Y), text, Face.SansMedium, 12, ink, 28);
        return w;
    }

    /// <summary>The small chip ("0 of 11 done"), 24 tall: now a tag (the soft tile), ice with a tick when it's all done.</summary>
    public static float Chip(Vector2 pos, string text, bool done = false) =>
        Tag(pos, text, done ? TagKind.Ok : TagKind.Plain, done ? Icon.Check : null, TagSize.Lg);

    public static float ChipWidth(string text, bool done = false) => TagWidth(text, done ? Icon.Check : null, TagSize.Lg);

    // ---------------------------------------------------------------------------------------------
    // Small things.

    /// <summary>
    /// A game icon (an item's), drawn from the game's own files in a rounded tile, on the soft tile
    /// while it loads (or when there's none); <paramref name="dim"/> faded to 35% (a piece you haven't
    /// got). <paramref name="size"/> is screen px. <see cref="Item"/> adds the corner dot.
    /// </summary>
    public static void GameIcon(Vector2 pos, float size, uint iconId, float radius = Palette.RadiusSm, bool dim = false)
    {
        var dl = Dl;
        var r = S(radius);
        dl.AddRectFilled(pos, pos + new Vector2(size), Palette.U32(Palette.Tile), r);
        if (iconId == 0) return;
        // An icon the game hasn't got is asked for once, not every frame (asking threw each time).
        if (!MissingIcons.Contains(iconId))
        {
            try
            {
                if (Services.Textures.TryGetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId), out var texture))
                {
                    var wrap = texture.GetWrapOrDefault();
                    if (wrap != null)
                        dl.AddImageRounded(wrap.Handle, pos, pos + new Vector2(size), Vector2.Zero, Vector2.One,
                            Palette.U32(dim ? new Vector4(1, 1, 1, 0.35f) : Vector4.One), r);
                }
                else
                {
                    if (MissingIcons.Count >= 4096) MissingIcons.Clear();
                    MissingIcons.Add(iconId);
                }
            }
            catch
            {
                // No icon: the tile stays.
            }
        }
    }

    private static readonly HashSet<uint> MissingIcons = new();

    /// <summary>
    /// A dye's swatch (the mock's <c>.swatch</c>): a dot in its colour with a faint light ring, or an
    /// empty faint ring when undyed. <paramref name="sizeCss"/> 10 in a line of text, 12 on its own.
    /// </summary>
    public static void Swatch(ImDrawListPtr dl, Vector2 center, uint? rgb, float sizeCss = 10)
    {
        var r = S(sizeCss / 2);
        if (rgb is { } v)
        {
            dl.AddCircleFilled(center, r, Palette.U32(Palette.Hex(v)), 16);
            dl.AddCircle(center, r + 0.5f, Palette.U32(Palette.Rgba(255, 255, 255, 0.18f)), 16, 1f);
        }
        else dl.AddCircle(center, r - 0.5f, Palette.U32(Palette.Faint), 16, 1f);
    }

    /// <summary>A round well with an icon (the blush and lilac for the steps, mint for done).</summary>
    public static void Round(Vector2 pos, float diameter, Icon icon, float iconPx, Vector4 fill, Vector4 ink, float stroke = 1.8f)
    {
        var dl = Dl;
        var c = pos + new Vector2(diameter / 2);
        dl.AddCircleFilled(c, diameter / 2, Palette.U32(fill), 48);
        Icons.Draw(dl, icon, c - new Vector2(iconPx / 2), iconPx, ink, stroke);
    }

    /// <summary>A tick box (the mock's <c>.check</c>), 18px with 6px corners: the soft tile and strong edge, the field's periwinkle with an ink tick when ticked.</summary>
    public static void TickBox(Vector2 pos, bool on, bool hovered)
    {
        var dl = Dl;
        var s = S(18);
        var r = S(6);
        if (on)
        {
            dl.AddRectFilled(pos, pos + new Vector2(s), Palette.U32(hovered ? Palette.FieldHi : Palette.Field), r);
            Icons.Draw(dl, Icon.Check, pos + new Vector2(S(3)), S(12), Palette.OnField, 2.6f);
        }
        else
        {
            dl.AddRectFilled(pos, pos + new Vector2(s), Palette.U32(Palette.Tile), r);
            dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(s) - new Vector2(0.5f), Palette.U32(hovered ? Palette.Accent : Palette.LineStrong), r, ImDrawFlags.None, 1f);
        }
    }

    /// <summary>A stepper for a number in Settings: − value +, a 36px pill. Returns −1, 0 or +1.</summary>
    public static int Stepper(string id, Vector2 pos, string value, bool canDown, bool canUp)
    {
        var dl = Dl;
        var h = S(36);
        var tw = Txt.Measure(value, Face.Mono, 13).X;
        var valueW = MathF.Max(S(56), tw + S(16));
        var w = h * 2 + valueW;
        dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(Palette.LineStrong), h / 2, ImDrawFlags.None, 1f);
        var result = 0;
        ImGui.PushID(id);
        try
        {
        for (var side = 0; side < 2; side++)
        {
            var down = side == 0;
            var dir = down ? -1 : 1;
            var icon = down ? Icon.Minus : Icon.Plus;
            var can = down ? canDown : canUp;
            var p = new Vector2(down ? pos.X : pos.X + h + valueW, pos.Y);
            var (clicked, hovered, held) = Hit(down ? "-1" : "1", p, new Vector2(h), !can);
            if (clicked) result = dir;
            var old = Palette.Opacity;
            if (!can) Palette.Opacity = old * 0.4f;
            if (hovered || held) dl.AddCircleFilled(p + new Vector2(h / 2), h / 2 - S(4), Palette.U32(held ? Palette.LinePicked : Palette.LineFaint), 32);
            Icons.Draw(dl, icon, p + new Vector2((h - S(16)) / 2), S(16), hovered ? Palette.Ink : Palette.Muted);
            Palette.Opacity = old;
            FocusRing(p + new Vector2(S(4)), p + new Vector2(h - S(4)), h / 2 - S(4));
        }
        }
        finally
        {
            ImGui.PopID();
        }
        Txt.Line(dl, new Vector2(pos.X + h + (valueW - tw) / 2, pos.Y), value, Face.Mono, 13, Palette.Ink, 36);
        return result;
    }

    public static float StepperWidth(string value) => S(36) * 2 + MathF.Max(S(56), Txt.Measure(value, Face.Mono, 13).X + S(16));

    /// <summary>
    /// A text field (the mock's <c>.input</c>; "field" here is ImGui's word, not the periwinkle):
    /// raised ink, the line edge (lilac while you type in it), an icon before the words, the hint in
    /// faint. <paramref name="heightCss"/> 40, or 34 for the small one (12px corners); 36 is what the
    /// screens had. <paramref name="pill"/> rounds its ends (the search); <paramref name="mono"/> types
    /// in Geist Mono (the token). With <paramref name="clearTip"/>, a × at the right end empties it
    /// while it has words (its own item beside the words, so it takes a click while you type), and
    /// Escape while typing empties it too. Returns true when the text changed.
    /// </summary>
    /// <param name="flags">ImGui's own, as Password for a secret.</param>
    public static bool Field(string id, Vector2 pos, float width, ref string text, string hint, Icon? icon = null, int maxLength = 128,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None, float heightCss = 36, bool pill = false, bool bad = false, bool mono = false,
        string? clearTip = null)
    {
        var dl = Dl;
        var h = S(heightCss);
        var r = pill ? h / 2 : S(heightCss <= 34 ? 12 : Palette.RadiusMd);
        var max = pos + new Vector2(width, h);
        dl.AddRectFilled(pos, max, Palette.U32(Palette.Input), r);
        var x = pos.X + S(pill || heightCss <= 34 ? 12 : 14);
        if (icon is { } ic)
        {
            var ip = S(heightCss <= 34 ? 14 : 16);
            Icons.Draw(dl, ic, new Vector2(x, pos.Y + (h - ip) / 2), ip, Palette.Faint);
            x += ip + S(8);
        }
        // The ×: 28 round, 6 in from the right end; the words stop short of it.
        var clearSize = S(28);
        var clearPos = new Vector2(max.X - S(6) - clearSize, pos.Y + (h - clearSize) / 2);
        var canClear = clearTip != null && text.Length > 0;
        var wordsEnd = canClear ? clearPos.X - S(4) : max.X - S(14);
        bool changed;
        bool active;
        // What's typed in the fallback when the face hasn't its letters (the fallback has Latin too).
        // (Mono at 13, as the mock's token.)
        var inputPx = heightCss <= 34 || mono ? 13 : 14;
        var face = mono ? Face.Mono : Face.Sans;
        using (Txt.PushFor(text.Length > 0 ? text : hint, face, inputPx))
        {
            var fh = ImGui.GetFontSize();
            var keep = ImGui.GetCursorScreenPos();
            bool hovered;
            using (ImRaii.PushColor(ImGuiCol.Text, Palette.Ink).Push(ImGuiCol.TextDisabled, Palette.Faint))
            using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(0, (h - fh) / 2)))
            {
                ImGui.SetCursorScreenPos(new Vector2(x, pos.Y));
                ImGui.SetNextItemWidth(MathF.Max(S(20), wordsEnd - x));
                using (ImRaii.Disabled(Blocked))
                    changed = ImGui.InputTextWithHint(id, hint, ref text, maxLength, flags);
                active = ImGui.IsItemActive();
                // Escape while typing: ImGui puts back what was there and lets go; a field that clears empties it.
                if (clearTip != null && ImGui.IsItemDeactivated() && ImGui.IsKeyPressed(ImGuiKey.Escape) && text.Length > 0)
                {
                    text = string.Empty;
                    changed = true;
                }
                hovered = ImGui.IsItemHovered() && !Blocked;
            }
            ImGui.SetCursorScreenPos(keep);
            var edge = bad ? Palette.WithAlpha(Palette.Danger, 0.6f) : active ? Palette.Accent : hovered ? Palette.LineHover : Palette.Line;
            dl.AddRect(pos + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(edge), r, ImDrawFlags.None, 1f);
        }
        if (canClear)
        {
            // After the words' item: a click on it while typing first lets go of the words (ImGui's
            // click outside), then lands here.
            bool cleared, clearHover;
            using (ImRaii.PushId(id))
                (cleared, clearHover, _) = Hit("##clear", clearPos, new Vector2(clearSize));
            if (clearHover)
            {
                dl.AddCircleFilled(clearPos + new Vector2(clearSize / 2), clearSize / 2, Palette.U32(Palette.AccentWash), 24);
                Tooltip(clearTip!);
            }
            Icons.Draw(dl, Icon.X, clearPos + new Vector2((clearSize - S(14)) / 2), S(14), clearHover ? Palette.Ink : Palette.Body);
            if (cleared)
            {
                text = string.Empty;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>The 8px bar (the mock's <c>.meter.l</c>): the tile's track and ice for what's done (the field's ink on the field). See <see cref="Meter"/>.</summary>
    public static void Progress(Vector2 pos, float width, float fraction) => Meter(pos, width, fraction, MeterKind.Ice, large: true);

    /// <summary>
    /// An eyebrow over a title (the mock's <c>.eyebrow</c>): Figtree 600 11px in capitals 0.08em
    /// apart, lilac (OnField on the field; or a colour), with a 14px icon before it if given, cut
    /// with "…" to <paramref name="maxWidth"/>. Returns its height.
    /// </summary>
    public static float Eyebrow(Vector2 pos, string text, Vector4? color = null, float maxWidth = 0, Icon? icon = null)
    {
        var col = color ?? Tint.Accent;
        if (icon is { } ic)
        {
            var ip = S(14);
            Icons.Draw(Dl, ic, new Vector2(pos.X, pos.Y + (EyebrowHeight - ip) / 2), ip, col);
            pos.X += ip + S(6);
            if (maxWidth > 0) maxWidth = MathF.Max(0, maxWidth - ip - S(6));
        }
        var caps = Txt.SpacedFit(text, Face.SansSemibold, 11, 0.08f, maxWidth);
        Txt.Spaced(Dl, pos, caps, Face.SansSemibold, 11, col, 0.08f);
        return EyebrowHeight;
    }

    /// <summary>An eyebrow's width (with its icon, if it has one).</summary>
    public static float EyebrowWidth(string text, Icon? icon = null) =>
        Txt.SpacedWidth(Txt.Upper(text), Face.SansSemibold, 11, 0.08f) + (icon != null ? S(14 + 6) : 0);

    /// <summary>An eyebrow's height.</summary>
    public static float EyebrowHeight => Txt.Natural(Face.SansSemibold, 11);

    /// <summary>
    /// A small label (<c>.gs-label</c>): Figtree 600 12px in normal capitals, muted. Returns its
    /// width. (DM Mono 10px in spaced capitals until 0.6.16.)
    /// </summary>
    public static float Label(Vector2 pos, string text, Vector4? color = null) =>
        Txt.Draw(Dl, pos, text, Face.SansSemibold, 12, color ?? Palette.Muted);

    public static float LabelWidth(string text) => Txt.Measure(text, Face.SansSemibold, 12).X;

    /// <summary>A label's height.</summary>
    public static float LabelHeight => Txt.Natural(Face.SansSemibold, 12);

    /// <summary>An eyebrow on its own row.</summary>
    public static void EyebrowRow(string text, Vector4? color = null)
    {
        var at = At;
        var h = Eyebrow(at, text, color, Width);
        Reserve(at, Width, h);
    }

    /// <summary>A row divider: the faint line.</summary>
    public static void Divider(float x0, float x1, float y) => Dl.AddLine(new Vector2(x0, y + 0.5f), new Vector2(x1, y + 0.5f), Palette.U32(Tint.Line), 1f);

    /// <summary>
    /// A dashed rounded rectangle, as the browser draws a dashed border: dashes three times the
    /// line's width with gaps as long, round the whole edge (corners too), evened out so the pattern
    /// closes. The dashes come from <see cref="Model.Dashes"/>, whose loops are bounded.
    /// </summary>
    public static void Dashed(Vector2 min, Vector2 max, float radius, Vector4 color, float thickness)
    {
        var dashes = DashesFor(max - min, radius, thickness);
        var col = Palette.U32(color);
        var dl = Dl;
        foreach (var dash in dashes)
        {
            if (dash.Length < 2) continue;
            if (DashBuffer.Length < dash.Length) DashBuffer = new Vector2[Math.Max(dash.Length, DashBuffer.Length * 2)];
            for (var i = 0; i < dash.Length; i++) DashBuffer[i] = min + dash[i];
            dl.AddPolyline(ref DashBuffer[0], dash.Length, col, ImDrawFlags.None, thickness);
        }
    }

    /// <summary>
    /// The dashes round a rectangle of a size, from its top-left corner: worked out once per size,
    /// radius and line (they were walked anew for every dashed edge, every frame), moved into place
    /// when drawn.
    /// </summary>
    private static List<Vector2[]> DashesFor(Vector2 size, float radius, float thickness)
    {
        var key = (size.X, size.Y, radius, thickness);
        if (DashCache.TryGetValue(key, out var dashes)) return dashes;
        var half = thickness / 2;
        var outline = Model.Dashes.RoundedRect(new Vector2(half), size - new Vector2(half), MathF.Max(0, radius - half));
        dashes = Model.Dashes.Along(outline, thickness * 6);
        if (DashCache.Count >= 256) DashCache.Clear();
        DashCache[key] = dashes;
        return dashes;
    }

    private static readonly Dictionary<(float W, float H, float Radius, float Thickness), List<Vector2[]>> DashCache = new();

    private static Vector2[] DashBuffer = new Vector2[64];

    // ---------------------------------------------------------------------------------------------
    // Panels: the content is laid first, the glass is drawn behind it at the height it had last frame.

    private static readonly Dictionary<uint, float> Heights = new();
    private static readonly Stack<(uint Id, Vector2 Start, float Width, float PadBottom, Vector4 Edge, float Radius)> Panels = new();

    /// <summary>
    /// A panel, as the mock's cards (the card fill, the 1px line edge, 18px corners). Lay its rows
    /// between this and <see cref="EndPanel"/>; padding is CSS pixels (top, right, bottom, left).
    /// </summary>
    public static void BeginPanel(string id, float padTop, float padX, float padBottom, Vector4? fill = null, Vector4? edge = null, float radius = Palette.RadiusLg)
    {
        var at = At;
        var w = Width;
        var key = ImGui.GetID(id);
        if (Heights.TryGetValue(key, out var h) && h > 0)
            Dl.AddRectFilled(at, at + new Vector2(w, h), Palette.U32(fill ?? Palette.Card), S(radius));
        BeginPanelAt(key, at, w, padTop, padX, padBottom, edge ?? Palette.Line, radius);
    }

    /// <summary>Starts laying a panel's rows (what's behind it is the caller's): its padding pushed, its box narrowed.</summary>
    private static void BeginPanelAt(uint key, Vector2 at, float w, float padTop, float padX, float padBottom, Vector4 edge, float radius)
    {
        Panels.Push((key, at, w, S(padBottom), edge, S(radius)));
        ImGui.SetCursorScreenPos(at + new Vector2(0, S(padTop)));
        ImGui.Dummy(Vector2.Zero);
        ImGui.SetCursorScreenPos(at + new Vector2(0, S(padTop)));
        PushBox(at.X + S(padX), w - S(padX) * 2);
    }

    /// <summary>Ends a panel's rows: its height kept for the next frame's fill, its room taken. Returns where it is.</summary>
    private static (Vector2 Start, float Width, float Height) EndPanelAt()
    {
        PopBox();
        var (key, start, w, padBottom, _, _) = Panels.Pop();
        var bottom = ImGui.GetCursorScreenPos().Y + padBottom;
        var h = bottom - start.Y;
        Heights[key] = h;
        Reserve(start, w, h);
        return (start, w, h);
    }

    /// <summary>The panel being laid: its top-left and width (for rows that go edge to edge).</summary>
    public static (Vector2 Start, float Width, float Radius) Panel => Panels.Count > 0 ? (Panels.Peek().Start, Panels.Peek().Width, Panels.Peek().Radius) : (At, Width, 0);

    public static void EndPanel()
    {
        var edge = Panels.Count > 0 ? Panels.Peek().Edge : Palette.Line;
        var r = Panels.Count > 0 ? Panels.Peek().Radius : 0;
        var (start, w, h) = EndPanelAt();
        if (edge.W > 0) Dl.AddRect(start + new Vector2(0.5f), start + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(edge), r, ImDrawFlags.None, 1f);
    }

    // ---------------------------------------------------------------------------------------------
    // Tooltips and menus.

    /// <summary>A tooltip as the mock's (<c>.tip</c>): raised ink, the strong hairline, 10px corners, 13px Figtree, 260 wide at most.</summary>
    public static void Tooltip(string text) => Tooltip(null, text);

    /// <summary>
    /// A tooltip with a name over its words (the mock's "<b>Pedagogy Culottes</b> / Legs · …"): the
    /// title in Figtree 600 Ink, the body in Body (or <paramref name="bodyColor"/>: amber for what
    /// to see to). A disabled button's tooltip says why.
    /// </summary>
    public static void Tooltip(string? title, string text, Vector4? bodyColor = null)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(11), S(8)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, S(Palette.RadiusTip));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, S(2)));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Palette.Popup);
        ImGui.PushStyleColor(ImGuiCol.Border, Palette.LineStrong);
        ImGui.PushStyleColor(ImGuiCol.Text, Palette.Body);
        try
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(S(260));
            if (title != null)
            {
                // (A name the face hasn't the letters for is drawn in the fallback: never ImGui's "?".)
                using (Txt.PushFor(title, Face.SansSemibold, 13))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, Palette.Ink);
                    ImGui.TextUnformatted(Txt.Drawable(title, Face.SansSemibold, 13));
                    ImGui.PopStyleColor();
                }
            }
            if (text.Length > 0)
            {
                using (Txt.PushFor(text, Face.Sans, 13))
                {
                    if (bodyColor is { } bc) ImGui.PushStyleColor(ImGuiCol.Text, bc);
                    ImGui.TextUnformatted(Txt.Drawable(text, Face.Sans, 13));
                    if (bodyColor != null) ImGui.PopStyleColor();
                }
            }
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
        finally
        {
            ImGui.PopStyleColor(3);
            ImGui.PopStyleVar(4);
        }
    }

    /// <summary>
    /// Opens a popup styled as the mock's menu (raised ink, the strong hairline, 16px corners, 6px
    /// inside). Pair with <see cref="EndMenu"/> when true. Lay <see cref="MenuItem(string, string, Icon?, float)"/>s,
    /// <see cref="MenuHeading"/>s and <see cref="MenuSeparator"/>s in it.
    /// </summary>
    public static bool BeginMenu(string id)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(6)));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, S(Palette.RadiusMenu));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Palette.Popup);
        ImGui.PushStyleColor(ImGuiCol.Border, Palette.LineStrong);
        var open = ImGui.BeginPopup(id, ImGuiWindowFlags.NoMove);
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
        if (open)
        {
            Frame.Open.Push(true);
            // What's laid in the menu (a note) is laid in the menu, 300 wide.
            PushBox(ImGui.GetCursorScreenPos().X, S(300));
        }
        return open;
    }

    public static void EndMenu()
    {
        EndStrays(popup: true);
        PopBox();
        if (Frame.Open.Count > 0) Frame.Open.Pop();
        ImGui.EndPopup();
    }

    private static bool warnedStray;

    /// <summary>
    /// Before a pane or menu is ended: ends what was begun inside it and never ended (a pane in a menu,
    /// a menu in a pane), with its box, so the wrong one isn't ended in its place (ImGui asserts when a
    /// child is ended as a popup or the other way about). A slip in a view, so it's logged, once.
    /// </summary>
    private static void EndStrays(bool popup)
    {
        while (Frame.Open.Count > 0 && Frame.Open.Peek() != popup)
        {
            if (!warnedStray)
            {
                warnedStray = true;
                Services.Log.Warning(popup ? "Glamour Studio ended a menu with a pane still open in it" : "Glamour Studio ended a pane with a menu still open in it");
            }
            var stray = Frame.Open.Pop();
            if (!stray && Panes.Count > 0) Panes.Pop();
            if (Boxes.Count > 0) Boxes.Pop();
            if (stray) ImGui.EndPopup();
            else ImGui.EndChild();
        }
    }

    /// <summary>A row in a menu: 34 tall, 10px corners, the lilac wash when pointed at.</summary>
    public static bool MenuItem(string id, string label, Icon? icon = null, float width = 0) => MenuItem(id, label, icon, width, null, null, false);

    /// <summary>
    /// A row in a menu (the mock's <c>.mi</c>): an icon, its words (Figtree 500 13), a faint second
    /// line (<paramref name="sub"/>) after them, a code in Geist Mono at its right (<paramref name="key"/>:
    /// a job's PLD), or the lilac tick when it's the one <paramref name="on"/>. True when pressed.
    /// </summary>
    public static bool MenuItem(string id, string label, Icon? icon, float width, string? sub, string? key, bool on, bool disabled = false)
    {
        var pos = ImGui.GetCursorScreenPos();
        var subW = sub != null ? S(6) + Txt.Measure(sub, Face.Sans, 11).X : 0;
        var keyW = key != null ? S(12) + Txt.Measure(key, Face.Mono, 11).X : on ? S(12 + 14) : 0;
        var w = width > 0 ? width : MathF.Max(S(200), Txt.Measure(label, Face.SansMedium, 13).X + S(20) + (icon != null ? S(25) : 0) + subW + keyW);
        var h = S(34);
        // The kit's own hit test (nothing reacts under a dialog); a menu opened in the dialog is drawn after it's unblocked.
        var (clicked, hovered, _) = HitHere(id, pos, new Vector2(w, h), disabled);
        var dl = ImGui.GetWindowDrawList();
        var old = Palette.Opacity;
        if (disabled) Palette.Opacity = old * 0.45f;
        if (hovered || on) dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(Palette.AccentWash), S(10));
        var x = pos.X + S(10);
        if (icon is { } ic)
        {
            Icons.Draw(dl, ic, new Vector2(x, pos.Y + (h - S(16)) / 2), S(16), hovered || on ? Palette.Ink : Palette.Muted);
            x += S(16 + 9);
        }
        var right = pos.X + w - S(10);
        var room = right - x - keyW;
        var lw = Txt.Line(dl, new Vector2(x, pos.Y), Txt.Fit(label, Face.SansMedium, 13, room), Face.SansMedium, 13, hovered || on ? Palette.Ink : Palette.Body, 34);
        if (sub != null && lw + subW < room)
            Txt.Line(dl, new Vector2(x + lw + S(6), pos.Y + S(1)), sub, Face.Sans, 11, Palette.Faint, 34);
        if (key != null)
            Txt.Line(dl, new Vector2(right - Txt.Measure(key, Face.Mono, 11).X, pos.Y), key, Face.Mono, 11, Palette.Faint, 34);
        else if (on)
            Icons.Draw(dl, Icon.Check, new Vector2(right - S(14), pos.Y + (h - S(14)) / 2), S(14), Palette.Accent);
        Palette.Opacity = old;
        FocusRing(pos, pos + new Vector2(w, h), S(10));
        return clicked;
    }

    /// <summary>A group's name in a menu (the mock's <c>.mh</c>): faint spaced capitals, 10.5px.</summary>
    public static void MenuHeading(string text)
    {
        var pos = ImGui.GetCursorScreenPos();
        var h = S(8 + 4) + Txt.Natural(Face.SansSemibold, 10);
        Txt.Spaced(Dl, pos + new Vector2(S(10), S(8)), Txt.Upper(text), Face.SansSemibold, 10, Palette.Faint, 0.08f);
        ImGui.Dummy(new Vector2(S(200), h));
    }

    /// <summary>A line between a menu's groups.</summary>
    public static void MenuSeparator()
    {
        var pos = ImGui.GetCursorScreenPos();
        Dl.AddLine(pos + new Vector2(S(6), S(4) + 0.5f), pos + new Vector2(S(194), S(4) + 0.5f), Palette.U32(Palette.Line), 1f);
        ImGui.Dummy(new Vector2(S(200), S(9)));
    }
}

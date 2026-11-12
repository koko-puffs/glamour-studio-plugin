using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// A chip's colour (drawn as the mock's tags now): plain; ice (yours, where a plate can take it,
/// done); lilac (to do); or pink (you don't have it yet).
/// </summary>
public enum ChipKind
{
    Plain,
    Ok,
    Accent,
    Love,
}

/// <summary>
/// The wide layout's parts: panes (a region of the window that scrolls on its own: the rail, a list,
/// the side pane), the rail's items, the pick-one tabs with counts, chips in their three colours,
/// the ready ring, small mono labels, links, columns side by side.
/// </summary>
public static partial class Kit
{
    // ---------------------------------------------------------------------------------------------
    // Panes.

    internal static readonly Stack<(float PadBottom, Vector2 Min, Vector2 Size)> Panes = new();

    /// <summary>
    /// A pane at <paramref name="min"/>, <paramref name="size"/> big: its own child window (it scrolls
    /// when what's in it is taller, unless <paramref name="scroll"/> is false), with padding in CSS
    /// pixels. Lay rows into it as into the window; end it with <see cref="EndPane"/>.
    /// </summary>
    public static void BeginPane(string id, Vector2 min, Vector2 size, float padX, float padTop, float padBottom, bool scroll = true, float padRight = -1)
    {
        ImGui.SetCursorScreenPos(min);
        size = Vector2.Max(size, new Vector2(S(20)));
        ImGui.BeginChild(id, size, false, scroll ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        Frame.Open.Push(false);
        var at = ImGui.GetCursorScreenPos();
        var right = padRight >= 0 ? padRight : padX;
        Panes.Push((padBottom, min, size));
        PushBox(at.X + S(padX), ImGui.GetContentRegionAvail().X - S(padX) - S(right));
        Reserve(at, 1, S(padTop));
    }

    public static void EndPane()
    {
        EndStrays(popup: false);
        var (padBottom, _, _) = Panes.Pop();
        Gap(padBottom);
        PopBox();
        if (Frame.Open.Count > 0) Frame.Open.Pop();
        ImGui.EndChild();
    }

    /// <summary>The room left in the pane below the cursor, as if it weren't scrolled, less its bottom padding.</summary>
    public static float PaneRoom()
    {
        if (Panes.Count == 0) return 0;
        var (padBottom, min, size) = Panes.Peek();
        var y = ImGui.GetCursorScreenPos().Y + ImGui.GetScrollY();
        return min.Y + size.Y - S(padBottom) - y;
    }

    /// <summary>A card (fill, 1px edge, radius) drawn at a rectangle, behind what's laid in it.</summary>
    public static void Card(Vector2 min, Vector2 max, Vector4? fill = null, Vector4? edge = null, float radius = Palette.RadiusLg, bool dashed = false)
    {
        var r = S(radius);
        var dl = Dl;
        if ((fill ?? Palette.Card).W > 0) dl.AddRectFilled(min, max, Palette.U32(fill ?? Palette.Card), r);
        if (dashed) Dashed(min, max, r, edge ?? Palette.LineDashed, 1f);
        else dl.AddRect(min + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(edge ?? Palette.Line), r, ImDrawFlags.None, 1f);
    }

    /// <summary>
    /// A card at the cursor, <paramref name="height"/> tall, whose inside scrolls on its own (a long list
    /// in a card of a set height). Padding in CSS pixels. End it with <see cref="EndScrollCard"/>.
    /// </summary>
    public static void BeginScrollCard(string id, float height, float pad, Vector4? fill = null, Vector4? edge = null, float radius = Palette.RadiusLg)
    {
        var at = At;
        var w = Width;
        Card(at, at + new Vector2(w, height), fill, edge, radius);
        ScrollCards.Push((at, w, height));
        BeginPane(id, at + new Vector2(1), new Vector2(w - 2, height - 2), pad, pad, pad);
    }

    public static void EndScrollCard()
    {
        EndPane();
        var (at, w, h) = ScrollCards.Pop();
        Reserve(at, w, h);
    }

    internal static readonly Stack<(Vector2 At, float Width, float Height)> ScrollCards = new();

    /// <summary>
    /// Two columns side by side from the cursor: <paramref name="left"/> laid in the first
    /// (<paramref name="split"/> of the room), <paramref name="right"/> in the second, a gap between;
    /// the cursor goes under the taller.
    /// </summary>
    public static void Columns(float split, float gapCss, Action left, Action right)
    {
        var at = At;
        var w = Width;
        var gap = S(gapCss);
        var lw = MathF.Floor((w - gap) * split);
        var rw = w - gap - lw;
        PushBox(at.X, lw);
        ImGui.SetCursorScreenPos(at);
        left();
        var lb = ImGui.GetCursorScreenPos().Y;
        PopBox();
        PushBox(at.X + lw + gap, rw);
        ImGui.SetCursorScreenPos(new Vector2(at.X + lw + gap, at.Y));
        right();
        var rb = ImGui.GetCursorScreenPos().Y;
        PopBox();
        Reserve(at, w, MathF.Max(lb, rb) - at.Y);
    }

    // ---------------------------------------------------------------------------------------------
    // The rail.

    /// <summary>
    /// One of the rail's items (40 tall, radius 12): its icon, its name and a count on the right in
    /// mono; where you are on the lilac wash with pale lilac words and a lilac icon. Compact, the icon alone (its name the
    /// tooltip). True when it's pressed.
    /// </summary>
    public static bool NavItem(string id, Vector2 pos, float width, Icon icon, string label, string? count, bool on, bool compact = false)
    {
        var h = S(40);
        var (clicked, hovered, _) = Hit(id, pos, new Vector2(width, h));
        var dl = Dl;
        var max = pos + new Vector2(width, h);
        if (on) dl.AddRectFilled(pos, max, Palette.U32(Palette.AccentWash), S(12));
        else if (hovered) dl.AddRectFilled(pos, max, Palette.U32(Palette.RowHover), S(12));
        var ink = on ? Palette.Ink : hovered ? Palette.Body : Palette.Muted;
        if (compact)
        {
            Icons.Draw(dl, icon, new Vector2(pos.X + (width - S(16)) / 2, pos.Y + (h - S(16)) / 2), S(16), on ? Palette.Accent : ink);
            if (hovered) Tooltip(count != null ? $"{label} · {count}" : label);
        }
        else
        {
            Icons.Draw(dl, icon, new Vector2(pos.X + S(12), pos.Y + (h - S(16)) / 2), S(16), on ? Palette.Accent : ink);
            var lx = pos.X + S(12 + 16 + 12);
            var room = width - S(12) - (lx - pos.X);
            var cw = count != null ? Txt.Measure(count, Face.Mono, 11).X : 0;
            // The name whole first: a count that leaves it no room goes to the tooltip ("Make ro… 662 / 800").
            if (count != null && Txt.Measure(label, Face.SansMedium, 14).X > room - cw - S(6))
            {
                if (hovered) Tooltip($"{label} · {count}");
                count = null;
                cw = 0;
            }
            Txt.Line(dl, new Vector2(lx, pos.Y), Txt.Fit(label, Face.SansMedium, 14, room - (cw > 0 ? cw + S(6) : 0)), Face.SansMedium, 14, ink, 40);
            if (count != null) Txt.Line(dl, new Vector2(pos.X + width - S(12) - cw, pos.Y), count, Face.Mono, 11, on ? Palette.Accent : Palette.Faint, 40);
        }
        FocusRing(pos, max, S(12));
        return clicked;
    }

    // ---------------------------------------------------------------------------------------------
    // Tabs with counts: the pick-one pill of the layout.

    private static float SegItemWidth(TabSpec t, float pad) =>
        S(pad) * 2 + Txt.Measure(t.Label, Face.SansSemibold, 13).X + (t.Count != null ? S(6) + Txt.Measure(t.Count, Face.Mono, 11).X : 0);

    public static float SegWidth(IReadOnlyList<TabSpec> tabs, float pad = 14)
    {
        var w = S(1) * 2 + S(3) * 2 + S(2) * Math.Max(0, tabs.Count - 1);
        foreach (var t in tabs) w += SegItemWidth(t, pad);
        return w;
    }

    public static float SegHeight => S(40);

    /// <summary>
    /// The pick-one pill (40 tall: 4px inside the strong edge, 30px items 3px apart, 600 13px words and
    /// a mono count): the picked item violet with white words, its pill gliding to it. Returns the item picked.
    /// </summary>
    public static int Seg(string id, Vector2 pos, IReadOnlyList<TabSpec> tabs, int selected, float pad = 14)
    {
        var w = SegWidth(tabs, pad);
        var h = SegHeight;
        var dl = Dl;
        dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(Palette.Card), h / 2);
        dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(Palette.Line), h / 2, ImDrawFlags.None, 1f);
        var x = pos.X + S(1) + S(3);
        var y = pos.Y + S(1) + S(3);
        var result = selected;
        // The picked one's pill, gliding to it.
        if (selected >= 0 && selected < tabs.Count)
        {
            var px = x;
            for (var i = 0; i < selected; i++) px += SegItemWidth(tabs[i], pad) + S(2);
            var (gx, gw) = Glide(id, px - pos.X, SegItemWidth(tabs[selected], pad));
            var gp = new Vector2(pos.X + gx, y);
            dl.AddRectFilled(gp, gp + new Vector2(gw, S(32)), Palette.U32(Palette.Picked), S(16));
        }
        for (var i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];
            var iw = SegItemWidth(t, pad);
            var p = new Vector2(x, y);
            var size = new Vector2(iw, S(32));
            ImGui.PushID(id);
            var (clicked, hovered, _) = Hit(Part(i), p, size);
            ImGui.PopID();
            if (clicked) result = i;
            var on = i == selected;
            if (!on && hovered) dl.AddRectFilled(p, p + size, Palette.U32(Palette.AccentWash), size.Y / 2);
            var ink = on ? Palette.OnPicked : hovered ? Palette.Body : Palette.Muted;
            var tx = p.X + S(pad);
            tx += Txt.Line(dl, new Vector2(tx, p.Y), t.Label, Face.SansSemibold, 13, ink, 32);
            if (t.Count != null) Txt.Line(dl, new Vector2(tx + S(6), p.Y), t.Count, Face.Mono, 11, on ? Palette.OnField2 : Palette.Faint, 32);
            FocusRing(p, p + size, size.Y / 2);
            x += iw + S(2);
        }
        return result;
    }

    // ---------------------------------------------------------------------------------------------
    // Chips in three colours and three sizes.

    private static TagSize ChipTag(int size) => size switch
    {
        18 => TagSize.Sm,
        22 => TagSize.Md,
        _ => TagSize.Lg,
    };

    private static TagKind ChipTagKind(ChipKind kind) => kind switch
    {
        ChipKind.Ok => TagKind.Ok,
        ChipKind.Accent => TagKind.Ink,
        ChipKind.Love => TagKind.Miss,
        _ => IsOnField ? TagKind.OnField : TagKind.Plain,
    };

    public static float ChipW(string text, bool tick = false, int size = 24) => TagWidth(text, tick ? Icon.Check : null, ChipTag(size));

    /// <summary>A chip (24, 22 or 18 tall: the mock's tags at 24, 21 and 19), plain, ice, lilac or pink, with a tick before its words if asked. Returns its width. See <see cref="Tag"/>.</summary>
    public static float Chip(Vector2 pos, string text, ChipKind kind, bool tick = false, int size = 24, float minWidth = 0)
    {
        var w = ChipW(text, tick, size);
        // A chip given a least width keeps its words in its middle.
        var x = minWidth > w ? pos.X + (minWidth - w) / 2 : pos.X;
        if (minWidth > w)
        {
            var (fill, _) = ChipTagKind(kind) switch
            {
                TagKind.Ok => (Palette.IceWash, 0),
                TagKind.Ink => (Palette.AccentWash, 0),
                TagKind.Miss => (Palette.PinkWash, 0),
                TagKind.OnField => (Palette.OnFieldWash, 0),
                _ => (Palette.Tile, 0),
            };
            Dl.AddRectFilled(pos, pos + new Vector2(minWidth, TagHeight(ChipTag(size))), Palette.U32(fill), S(Palette.RadiusTag));
        }
        Tag(new Vector2(x, pos.Y), text, ChipTagKind(kind), tick ? Icon.Check : null, ChipTag(size));
        return MathF.Max(minWidth, w);
    }

    // ---------------------------------------------------------------------------------------------
    // Small things.

    /// <summary>A small line's face and size: Figtree 500, 12px.</summary>
    public const Face SubFace = Face.SansMedium;

    public const int SubPx = 12;

    /// <summary>
    /// A small line beside a heading or over a strip ("On it now · 7 pieces"): Figtree 500 12px, dim
    /// (or a colour), its first letter a capital, cut to fit. Returns its width. (DM Mono in spaced capitals until 0.6.16: the
    /// mono is for numbers now, as the site's is.)
    /// </summary>
    public static float Sub(Vector2 pos, string text, Vector4? color = null, float maxWidth = 0)
    {
        text = Txt.Sentence(text);
        var t = maxWidth > 0 ? Txt.Fit(text, SubFace, SubPx, maxWidth) : text;
        return Txt.Draw(Dl, pos, t, SubFace, SubPx, color ?? Tint.Muted);
    }

    public static float SubWidth(string text) => Txt.Measure(Txt.Sentence(text), SubFace, SubPx).X;

    /// <summary>A small line's height.</summary>
    public static float SubHeight => Txt.Natural(SubFace, SubPx);

    /// <summary>
    /// A heading in Bricolage capitals (Ink; OnField on the field) with a small line on its right, on
    /// one row; when the two don't fit side by side, the line goes under the heading. (Outfit 600 in
    /// normal capitals until the redesign; <see cref="SectionHead"/> is the mock's with a count.)
    /// </summary>
    public static void Heading(string text, string? sub = null, float px = 15, Vector4? subColor = null)
    {
        var at = At;
        var w = Width;
        var ipx = (int)px;
        var h = DisplayHeight(ipx);
        var subW = sub != null ? SubWidth(sub) : 0;
        var textW = DisplayWidth(text, ipx);
        if (sub == null || textW + S(12) + subW <= w)
        {
            Display(at, text, ipx, Tint.Ink, w - subW - (sub != null ? S(12) : 0));
            if (sub != null)
                Sub(new Vector2(at.X + w - subW, at.Y + (h - SubHeight) / 2 + S(1)), sub, subColor);
            Reserve(at, w, h);
            return;
        }
        Display(at, text, ipx, Tint.Ink, w);
        var sh = SubHeight;
        Sub(new Vector2(at.X, at.Y + h + S(3)), sub, subColor, w);
        Reserve(at, w, h + S(3) + sh);
    }

    /// <summary>A heading with a lilac link on its right (true when the link's pressed).</summary>
    public static bool HeadingLink(string id, string text, string link)
    {
        var at = At;
        var w = Width;
        var h = DisplayHeight(16);
        Display(at, text, 16, Tint.Ink, w * 0.6f);
        var lw = Txt.Measure(link, Face.Sans, 13).X;
        var lh = Txt.Natural(Face.Sans, 13);
        var pos = new Vector2(at.X + w - lw, at.Y + (h - lh) / 2);
        var pressed = Link(id, pos, link);
        Reserve(at, w, h);
        return pressed;
    }

    /// <summary>Lilac words that are a link (ink when pointed at); true when pressed.</summary>
    public static bool Link(string id, Vector2 pos, string text, int px = 13, Face face = Face.Sans)
    {
        var size = Txt.Measure(text, face, px);
        var (clicked, hovered, _) = Hit(id, pos, size);
        Txt.Draw(Dl, pos, text, face, px, hovered ? Tint.Ink : Tint.Accent);
        return clicked;
    }

    /// <summary>
    /// The ready ring (the mock's <c>.ring</c>: two arcs): a <paramref name="thickness"/> track (the
    /// tile; the field's ink at 18% on the field) and the arc for what's ready, round-ended from the
    /// top, ice (the field's ink on the field; or <paramref name="arc"/>). Screen px. See <see cref="ReadyRing"/>.
    /// </summary>
    public static void Ring(Vector2 center, float radius, float thickness, float fraction, Vector4? arc = null)
    {
        var dl = Dl;
        var r = radius - thickness / 2;
        dl.PathArcTo(center, r, 0, MathF.PI * 2, 64);
        dl.PathStroke(Palette.U32(Tint.Track), ImDrawFlags.Closed, thickness);
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0) return;
        var col = Palette.U32(arc ?? Tint.Fill);
        var start = -MathF.PI / 2;
        var end = start + MathF.PI * 2 * fraction;
        dl.PathArcTo(center, r, start, end, Math.Max(8, (int)(64 * fraction)));
        dl.PathStroke(col, fraction >= 1 ? ImDrawFlags.Closed : ImDrawFlags.None, thickness);
        if (fraction >= 1) return;
        // Round ends.
        dl.AddCircleFilled(center + new Vector2(MathF.Cos(start), MathF.Sin(start)) * r, thickness / 2, col, 12);
        dl.AddCircleFilled(center + new Vector2(MathF.Cos(end), MathF.Sin(end)) * r, thickness / 2, col, 12);
    }

    /// <summary>An empty slot's tile: the raised ground, a hairline (dashed when it's somewhere to add).</summary>
    public static void EmptyTile(Vector2 pos, float size, float radius = Palette.RadiusSm, float alpha = 1f, bool dashed = false)
    {
        var r = S(radius);
        var old = Palette.Opacity;
        Palette.Opacity = old * alpha;
        if (dashed) Dashed(pos, pos + new Vector2(size), r, Tint.Dashed, 1f);
        else Dl.AddRectFilled(pos, pos + new Vector2(size), Palette.U32(Palette.Tile), r);
        Palette.Opacity = old;
    }

    /// <summary>A round well with an icon: lit (the blush, its lilac edge, the lilac icon) or quiet (the soft tile, a hairline, dim).</summary>
    public static void Well(Vector2 pos, float size, Icon icon, bool lit = true, float iconPx = 16)
    {
        var dl = Dl;
        var c = pos + new Vector2(size / 2);
        dl.AddCircleFilled(c, size / 2, Palette.U32(lit ? Palette.BlushTile : Palette.Tile), 48);
        if (lit) dl.AddCircle(c, size / 2 - 0.5f, Palette.U32(Palette.AccentLine), 48, 1f);
        Icons.Draw(dl, icon, c - new Vector2(S(iconPx) / 2), S(iconPx), lit ? Palette.Accent : Palette.Muted);
    }

    /// <summary>A dot of a colour, 7px (or another size).</summary>
    public static void Dot(Vector2 center, Vector4 color, float sizeCss = 7)
    {
        Dl.AddCircleFilled(center, S(sizeCss) / 2, Palette.U32(color), 16);
    }
}

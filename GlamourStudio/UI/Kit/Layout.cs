using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>A chip's colour: the outline, green (where a plate can take it, done), or rose (to do).</summary>
public enum ChipKind
{
    Plain,
    Ok,
    Rose,
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
    /// mono; where you are in the rose wash with rose-pale words. Compact, the icon alone (its name the
    /// tooltip). True when it's pressed.
    /// </summary>
    public static bool NavItem(string id, Vector2 pos, float width, Icon icon, string label, string? count, bool on, bool compact = false)
    {
        var h = S(40);
        var (clicked, hovered, _) = Hit(id, pos, new Vector2(width, h));
        var dl = Dl;
        var max = pos + new Vector2(width, h);
        if (on) dl.AddRectFilled(pos, max, Palette.U32(Palette.RosePicked), S(12));
        else if (hovered) dl.AddRectFilled(pos, max, Palette.U32(Palette.RowHover), S(12));
        var ink = on ? Palette.RoseSoft : hovered ? Palette.Ink : Palette.Muted;
        if (compact)
        {
            Icons.Draw(dl, icon, new Vector2(pos.X + (width - S(16)) / 2, pos.Y + (h - S(16)) / 2), S(16), on ? Palette.Rose : ink);
            if (hovered) Tooltip(count != null ? $"{label} · {count}" : label);
        }
        else
        {
            Icons.Draw(dl, icon, new Vector2(pos.X + S(12), pos.Y + (h - S(16)) / 2), S(16), on ? Palette.Rose : ink);
            var cw = count != null ? Txt.Measure(count, Face.Mono, 11).X : 0;
            var lx = pos.X + S(12 + 16 + 12);
            Txt.Line(dl, new Vector2(lx, pos.Y), Txt.Fit(label, Face.SansMedium, 14, width - S(12) - (lx - pos.X) - cw - S(6)), Face.SansMedium, 14, ink, 40);
            if (count != null) Txt.Line(dl, new Vector2(pos.X + width - S(12) - cw, pos.Y), count, Face.Mono, 11, Palette.Dim, 40);
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
        var w = S(1) * 2 + S(4) * 2 + S(3) * Math.Max(0, tabs.Count - 1);
        foreach (var t in tabs) w += SegItemWidth(t, pad);
        return w;
    }

    public static float SegHeight => S(40);

    /// <summary>
    /// The pick-one pill (40 tall: 4px inside the strong edge, 30px items 3px apart, 600 13px words and
    /// a mono count): the picked item filled ink with dark words. Returns the item picked.
    /// </summary>
    public static int Seg(string id, Vector2 pos, IReadOnlyList<TabSpec> tabs, int selected, float pad = 14)
    {
        var w = SegWidth(tabs, pad);
        var h = SegHeight;
        var dl = Dl;
        dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(Palette.LineStrong), h / 2, ImDrawFlags.None, 1f);
        var x = pos.X + S(1) + S(4);
        var y = pos.Y + S(1) + S(4);
        var result = selected;
        for (var i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];
            var iw = SegItemWidth(t, pad);
            var p = new Vector2(x, y);
            var size = new Vector2(iw, S(30));
            var (clicked, hovered, _) = Hit($"{id}{i}", p, size);
            if (clicked) result = i;
            var on = i == selected;
            if (on) dl.AddRectFilled(p, p + size, Palette.U32(Palette.Picked), size.Y / 2);
            else if (hovered) dl.AddRectFilled(p, p + size, Palette.U32(Palette.LineFaint), size.Y / 2);
            var ink = on ? Palette.OnPicked : hovered ? Palette.Ink : Palette.Muted;
            var tx = p.X + S(pad);
            tx += Txt.Line(dl, new Vector2(tx, p.Y), t.Label, Face.SansSemibold, 13, ink, 30);
            if (t.Count != null) Txt.Line(dl, new Vector2(tx + S(6), p.Y), t.Count, Face.Mono, 11, Palette.WithAlpha(ink, 0.75f), 30);
            FocusRing(p, p + size, size.Y / 2);
            x += iw + S(3);
        }
        return result;
    }

    // ---------------------------------------------------------------------------------------------
    // Chips in three colours and three sizes.

    private static (float H, float Pad, int Px) ChipSpec(int size) => size switch
    {
        18 => (18, 6, 10),
        22 => (22, 8, 11),
        _ => (24, 10, 12),
    };

    public static float ChipW(string text, bool tick = false, int size = 24)
    {
        var (_, pad, px) = ChipSpec(size);
        return S(pad) * 2 + (tick ? S(12 + 6) : 0) + Txt.Measure(text, Face.Sans, px).X;
    }

    /// <summary>A chip (24, 22 or 18 tall): the strong outline, or green, or rose, with a tick before its words if asked. Returns its width.</summary>
    public static float Chip(Vector2 pos, string text, ChipKind kind, bool tick = false, int size = 24, float minWidth = 0)
    {
        var (hc, pad, px) = ChipSpec(size);
        var h = S(hc);
        var w = MathF.Max(minWidth, ChipW(text, tick, size));
        var (fill, edge, ink) = kind switch
        {
            ChipKind.Ok => (Palette.SuccessChip, Palette.SuccessEdge, Palette.Success),
            ChipKind.Rose => (Palette.RoseChip, Palette.RoseEdge, Palette.RoseText),
            _ => (System.Numerics.Vector4.Zero, Palette.LineStrong, Palette.Soft),
        };
        var dl = Dl;
        if (fill.W > 0) dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(fill), h / 2);
        dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(edge), h / 2, ImDrawFlags.None, 1f);
        var content = ChipW(text, tick, size) - S(pad) * 2;
        var x = pos.X + (w - content) / 2;
        if (tick)
        {
            Icons.Draw(dl, Icon.Check, new Vector2(x, pos.Y + (h - S(12)) / 2), S(12), ink, 2.2f);
            x += S(12 + 6);
        }
        Txt.Line(dl, new Vector2(x, pos.Y), text, Face.Sans, px, ink, hc);
        return w;
    }

    // ---------------------------------------------------------------------------------------------
    // Small things.

    /// <summary>A small mono label ("ON IT NOW · 7 PIECES"): DM Mono 11px, .06em, dim (or a colour). Returns its width.</summary>
    public static float Sub(Vector2 pos, string text, Vector4? color = null, float maxWidth = 0)
    {
        var caps = text.ToUpperInvariant();
        while (maxWidth > 0 && caps.Length > 1 && Txt.SpacedWidth(caps, Face.Mono, 11, 0.06f) > maxWidth)
            caps = caps[..^2].TrimEnd() + "…";
        return Txt.Spaced(Dl, pos, caps, Face.Mono, 11, color ?? Palette.Dim, 0.06f);
    }

    public static float SubWidth(string text) => Txt.SpacedWidth(text.ToUpperInvariant(), Face.Mono, 11, 0.06f);

    /// <summary>A heading (15px 600, ink) with a small label on its right, on one row.</summary>
    public static void Heading(string text, string? sub = null, float px = 15, Vector4? subColor = null)
    {
        var at = At;
        var w = Width;
        var ipx = (int)px;
        var h = Txt.Natural(Face.SansSemibold, ipx);
        var subW = sub != null ? SubWidth(sub) : 0;
        Txt.Line(Dl, at, Txt.Fit(text, Face.SansSemibold, ipx, w - subW - S(12)), Face.SansSemibold, ipx, Palette.Ink);
        if (sub != null)
            Sub(new Vector2(at.X + w - subW, at.Y + (h - Txt.Natural(Face.Mono, 11)) / 2 + S(1)), sub, subColor);
        Reserve(at, w, h);
    }

    /// <summary>A heading with a rose link on its right (true when the link's pressed).</summary>
    public static bool HeadingLink(string id, string text, string link)
    {
        var at = At;
        var w = Width;
        var h = Txt.Natural(Face.SansSemibold, 15);
        Txt.Line(Dl, at, text, Face.SansSemibold, 15, Palette.Ink);
        var lw = Txt.Measure(link, Face.Sans, 13).X;
        var lh = Txt.Natural(Face.Sans, 13);
        var pos = new Vector2(at.X + w - lw, at.Y + (h - lh) / 2);
        var pressed = Link(id, pos, link);
        Reserve(at, w, h);
        return pressed;
    }

    /// <summary>Rose words that are a link (ink when pointed at); true when pressed.</summary>
    public static bool Link(string id, Vector2 pos, string text, int px = 13, Face face = Face.Sans)
    {
        var size = Txt.Measure(text, face, px);
        var (clicked, hovered, _) = Hit(id, pos, size);
        Txt.Draw(Dl, pos, text, face, px, hovered ? Palette.Ink : Palette.RoseText);
        return clicked;
    }

    /// <summary>The ready ring: a <paramref name="thickness"/> track with the green arc for what's ready, round from the top.</summary>
    public static void Ring(Vector2 center, float radius, float thickness, float fraction)
    {
        var dl = Dl;
        var r = radius - thickness / 2;
        dl.PathArcTo(center, r, 0, MathF.PI * 2, 64);
        dl.PathStroke(Palette.U32(Palette.Line), ImDrawFlags.Closed, thickness);
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0) return;
        var start = -MathF.PI / 2;
        dl.PathArcTo(center, r, start, start + MathF.PI * 2 * fraction, Math.Max(8, (int)(64 * fraction)));
        dl.PathStroke(Palette.U32(Palette.Success), fraction >= 1 ? ImDrawFlags.Closed : ImDrawFlags.None, thickness);
    }

    /// <summary>An empty slot's tile: the raised ground, a hairline (dashed when it's somewhere to add).</summary>
    public static void EmptyTile(Vector2 pos, float size, float radius = Palette.RadiusSm, float alpha = 1f, bool dashed = false)
    {
        var r = S(radius);
        var old = Palette.Opacity;
        Palette.Opacity = old * alpha;
        if (dashed) Dashed(pos, pos + new Vector2(size), r, Palette.LineDashed, 1f);
        else
        {
            Dl.AddRectFilled(pos, pos + new Vector2(size), Palette.U32(Palette.Raised), r);
            Dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(size) - new Vector2(0.5f), Palette.U32(Palette.LineFaint), r, ImDrawFlags.None, 1f);
        }
        Palette.Opacity = old;
    }

    /// <summary>A round well with an icon: rose (the blush, its edge, the rose icon) or quiet (the soft tile, a hairline, dim).</summary>
    public static void Well(Vector2 pos, float size, Icon icon, bool rose = true, float iconPx = 16)
    {
        var dl = Dl;
        var c = pos + new Vector2(size / 2);
        dl.AddCircleFilled(c, size / 2, Palette.U32(rose ? Palette.RoseTile : Palette.Tile), 48);
        dl.AddCircle(c, size / 2 - 0.5f, Palette.U32(rose ? Palette.RoseEdge : Palette.Line), 48, 1f);
        Icons.Draw(dl, icon, c - new Vector2(S(iconPx) / 2), S(iconPx), rose ? Palette.Rose : Palette.Dim);
    }

    /// <summary>A dot of a colour, 7px (or another size).</summary>
    public static void Dot(Vector2 center, Vector4 color, float sizeCss = 7)
    {
        Dl.AddCircleFilled(center, S(sizeCss) / 2, Palette.U32(color), 16);
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>One of Settings' tabs: its words and, when it has one, a count after them.</summary>
public readonly record struct TabSpec(string Label, string? Count = null);

/// <summary>Lays things left to right from the cursor, wrapping at the box's edge.</summary>
public sealed class Wrap
{
    private readonly float gap;
    private readonly Vector2 start;
    private float x;
    private float y;
    private float rowH;

    public Wrap(float gapCss)
    {
        gap = S(gapCss);
        start = Kit.At;
        x = start.X;
        y = start.Y;
    }

    /// <summary>Where the next thing of this size goes.</summary>
    public Vector2 Next(float w, float h)
    {
        if (x > start.X && x + w > Kit.Right + 0.5f)
        {
            x = start.X;
            y += rowH + gap;
            rowH = 0;
        }
        var p = new Vector2(x, y);
        x += w + gap;
        rowH = MathF.Max(rowH, h);
        return p;
    }

    /// <summary>Takes up the rows' room.</summary>
    public void End() => Kit.Reserve(start, Kit.Width, y + rowH - start.Y);
}

/// <summary>Tabs as a pick-one pill (the row of them the screens share).</summary>
public static partial class Kit
{
    // ---------------------------------------------------------------------------------------------
    // Tabs.

    public static float TabWidth(TabSpec tab, float pad = 14) =>
        S(pad) * 2 + Txt.Measure(tab.Label, Face.SansSemibold, 13).X + (tab.Count != null ? S(6) + Txt.Measure(tab.Count, Face.Mono, 11).X : 0);

    /// <summary>
    /// Tabs as the site's pick-one pill (36 tall, the strong edge, 3px inside): the picked one a soft
    /// violet pill with white words (gliding to it), the others grey. Returns the tab picked (the one given when none was pressed).
    /// </summary>
    public static int Tabs(string id, IReadOnlyList<TabSpec> tabs, int selected)
    {
        var at = At;
        var h = S(36);
        var inner = S(28);
        // Too wide for the room: the counts go first, then the tabs' padding tightens.
        var pad = 14f;
        var room = Width;
        var counts = true;
        float Total(IReadOnlyList<TabSpec> ts, float pd)
        {
            var sum = S(4) * 2 + S(3) * (ts.Count - 1);
            foreach (var t in ts) sum += TabWidth(counts ? t : t with { Count = null }, pd);
            return sum;
        }
        if (Total(tabs, pad) > room) counts = false;
        while (pad > 8 && Total(tabs, pad) > room) pad -= 1;
        var total = Total(tabs, pad);
        var dl = Dl;
        dl.AddRect(at + new Vector2(0.5f), at + new Vector2(total, h) - new Vector2(0.5f), Palette.U32(Palette.LineStrong), h / 2, ImDrawFlags.None, 1f);
        var result = selected;
        var x = at.X + S(4);
        // The picked one's pill, gliding to it.
        if (selected >= 0 && selected < tabs.Count)
        {
            var px = x;
            for (var i = 0; i < selected; i++) px += TabWidth(counts ? tabs[i] : tabs[i] with { Count = null }, pad) + S(3);
            var (gx, gw) = Glide(id, px - at.X, TabWidth(counts ? tabs[selected] : tabs[selected] with { Count = null }, pad));
            var gp = new Vector2(at.X + gx, at.Y + S(4));
            dl.AddRectFilled(gp, gp + new Vector2(gw, inner), Palette.U32(Palette.Picked), inner / 2);
        }
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = counts ? tabs[i] : tabs[i] with { Count = null };
            var w = TabWidth(tab, pad);
            var p = new Vector2(x, at.Y + S(4));
            var max = p + new Vector2(w, inner);
            ImGui.PushID(id);
            var (clicked, hovered, held) = Hit(Part(i), p, new Vector2(w, inner));
            ImGui.PopID();
            if (clicked) result = i;
            var on = i == selected;
            if (!on && (held || hovered)) dl.AddRectFilled(p, max, Palette.U32(held ? Palette.LinePicked : Palette.LineFaint), inner / 2);
            var tx = p.X + S(pad);
            tx += Txt.Line(dl, new Vector2(tx, p.Y), tab.Label, Face.SansSemibold, 13, on ? Palette.OnPicked : hovered ? Palette.Ink : Palette.Muted, 28);
            if (tab.Count != null) Txt.Line(dl, new Vector2(tx + S(6), p.Y), tab.Count, Face.Mono, 11, on ? Palette.WithAlpha(Palette.OnPicked, 0.75f) : Palette.Faint, 28);
            FocusRing(p, max, inner / 2);
            x += w + S(3);
        }
        Reserve(at, total, h);
        return result;
    }
}

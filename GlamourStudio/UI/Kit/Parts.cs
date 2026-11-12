using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.UI.Model;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>A tag's colour (the mock's <c>.tag</c> kinds).</summary>
public enum TagKind
{
    /// <summary>The soft tile, Body words.</summary>
    Plain,

    /// <summary>Ice: owned, in the dresser, ready (with ✓).</summary>
    Ok,

    /// <summary>Pink: missing.</summary>
    Miss,

    /// <summary>Lavender: on your wishlist (★).</summary>
    Star,

    /// <summary>Amber: to see to (in your bags, 2 to store).</summary>
    Amber,

    /// <summary>Red: went wrong.</summary>
    Danger,

    /// <summary>Lilac on its wash: changes, a note.</summary>
    Ink,

    /// <summary>Holo with ink words: new (holo means special).</summary>
    New,

    /// <summary>The field's ink at 14% with ink words: a tag on the field.</summary>
    OnField,
}

/// <summary>A tag's height: 19 (in a callout or pane row), 21, 24 (large).</summary>
public enum TagSize
{
    Sm,
    Md,
    Lg,
}

/// <summary>An item tile's corner dot: where the piece is, in the honest colours.</summary>
public enum Own
{
    None,

    /// <summary>Owned (in the dresser): ice.</summary>
    Ice,

    /// <summary>Missing: pink.</summary>
    Pink,

    /// <summary>To see to (the armoire, a dye from your bags, to put away): amber.</summary>
    Amber,

    /// <summary>On your wishlist: lavender.</summary>
    Star,
}

/// <summary>A bar's fill.</summary>
public enum MeterKind
{
    /// <summary>Ice: how much is done.</summary>
    Ice,

    /// <summary>The field's periwinkle: while it's working.</summary>
    Working,

    /// <summary>Amber: nearly full.</summary>
    Amber,
}

/// <summary>A dot's meaning (the mock's <c>.dot</c>s).</summary>
public enum DotKind
{
    Ice,
    Live,
    Recording,
    No,
    Danger,
    Amber,
    Pink,
}

/// <summary>
/// The sheet's small parts: tags, item tiles with their corner dot, the palette strip, the seal,
/// the sign and the empty page, the rings, the bar, section headings, the underlined tabs, the
/// combo, the face and the dots.
/// </summary>
public static partial class Kit
{
    // ---------------------------------------------------------------------------------------------
    // Tags.

    private static (float H, float Pad, int Px, float Icon) TagSpec(TagSize size) => size switch
    {
        TagSize.Sm => (19, 6, 10, 10),
        TagSize.Lg => (24, 9, 12, 12),
        _ => (21, 7, 11, 11),
    };

    public static float TagHeight(TagSize size = TagSize.Md) => S(TagSpec(size).H);

    public static float TagWidth(string text, Icon? icon = null, TagSize size = TagSize.Md)
    {
        var (_, pad, px, ic) = TagSpec(size);
        return S(pad) * 2 + Txt.Measure(text, Face.SansSemibold, px).X + (icon != null ? S(ic + 4) : 0);
    }

    /// <summary>
    /// A tag (the mock's <c>.tag</c>): 6px corners, Figtree 600 (the mock's 650) 11px, the kind's
    /// wash and colour, an icon (✓ for Ok) before the words if given. Returns its width.
    /// </summary>
    public static float Tag(Vector2 pos, string text, TagKind kind, Icon? icon = null, TagSize size = TagSize.Md)
    {
        var (hc, pad, px, ic) = TagSpec(size);
        var h = S(hc);
        var w = TagWidth(text, icon, size);
        var (fill, ink) = kind switch
        {
            TagKind.Ok => (Palette.IceWash, Palette.Ice),
            TagKind.Miss => (Palette.PinkWash, Palette.Pink),
            TagKind.Star => (Palette.StarredWash, Palette.Starred),
            TagKind.Amber => (Palette.AmberWash, Palette.Amber),
            TagKind.Danger => (Palette.DangerWash, Palette.Danger),
            TagKind.Ink => (Palette.AccentWash, Palette.Accent),
            TagKind.New => (Vector4.Zero, Palette.OnField),
            TagKind.OnField => (Palette.OnFieldWash, Palette.OnField),
            _ => (Palette.Tile, Palette.Body),
        };
        var dl = Dl;
        var r = S(Palette.RadiusTag);
        if (kind == TagKind.New) Holo(pos, pos + new Vector2(w, h), r);
        else dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(fill), r);
        var x = pos.X + S(pad);
        if (icon is { } i)
        {
            Icons.Draw(dl, i, new Vector2(x, pos.Y + (h - S(ic)) / 2), S(ic), ink, 2.4f);
            x += S(ic + 4);
        }
        Txt.Line(dl, new Vector2(x, pos.Y), text, Face.SansSemibold, px, ink, hc);
        return w;
    }

    // ---------------------------------------------------------------------------------------------
    // Item tiles.

    /// <summary>An item tile's corners for its size (the mock's: 6 at 24, 7 at 28, 8 at 32–36, 9 at 40, 10 at 44–48, 12 at 56).</summary>
    public static float TileRadius(float sizeCss) => sizeCss switch
    {
        <= 26 => 6,
        <= 30 => 7,
        <= 38 => 8,
        <= 42 => 9,
        <= 50 => 10,
        _ => 12,
    };

    /// <summary>
    /// An item tile (the mock's <c>.it</c>): the game's icon on the soft tile, <paramref name="sizeCss"/>
    /// square, its corner dot for where it is (<paramref name="own"/>; ringed in <paramref name="ring"/>,
    /// the ground under it: Card by default), faded when <paramref name="ghost"/> (a piece you
    /// haven't got).
    /// </summary>
    public static void Item(Vector2 pos, float sizeCss, uint iconId, Own own = Own.None, bool ghost = false, Vector4? ring = null)
    {
        var size = S(sizeCss);
        GameIcon(pos, size, iconId, TileRadius(sizeCss), ghost);
        if (own != Own.None) OwnDot(pos + new Vector2(size), own, ring);
    }

    /// <summary>The corner dot alone, its tile's bottom-right corner at <paramref name="corner"/>: 8px with a 2px ring of the ground.</summary>
    public static void OwnDot(Vector2 corner, Own own, Vector4? ring = null)
    {
        var c = corner - new Vector2(S(2));
        var col = own switch
        {
            Own.Pink => Palette.Pink,
            Own.Amber => Palette.Amber,
            Own.Star => Palette.Starred,
            _ => Palette.Ice,
        };
        Dl.AddCircleFilled(c, S(4), Palette.U32(ring ?? Palette.Card), 16);
        Dl.AddCircleFilled(c, S(2), Palette.U32(col), 12);
    }

    /// <summary>
    /// An empty slot's tile (the mock's <c>.it.empty</c>): a dashed 1px edge (the field's ink at 40%
    /// on the field; pink with <paramref name="pink"/>), and "×" in faint mono with <paramref name="cross"/>
    /// (a slot emptied).
    /// </summary>
    public static void ItemEmpty(Vector2 pos, float sizeCss, bool cross = false, bool pink = false)
    {
        var size = S(sizeCss);
        var edge = pink ? Palette.WithAlpha(Palette.Pink, 0.6f) : Tint.Dashed;
        Dashed(pos, pos + new Vector2(size), S(TileRadius(sizeCss)), edge, 1f);
        if (!cross) return;
        var tw = Txt.Measure("×", Face.Mono, 10);
        Txt.Draw(Dl, pos + (new Vector2(size) - tw) / 2, "×", Face.Mono, 10, Tint.Faint);
    }

    // ---------------------------------------------------------------------------------------------
    // The palette strip.

    /// <summary>
    /// The palette strip (the mock's <c>.palette</c>): the dyes side by side, each an equal share.
    /// Small, 10px tall with 3px corners and a faint dark edge; <paramref name="big"/>, 34px with 8px
    /// corners, a 2px black edge and each dye's name at its foot (light on a dark dye).
    /// <paramref name="names"/> may be null (small) or as long as <paramref name="rgbs"/>.
    /// </summary>
    public static void PaletteStrip(Vector2 pos, float width, IReadOnlyList<uint> rgbs, IReadOnlyList<string>? names = null, bool big = false)
    {
        if (rgbs.Count == 0 || width <= 0) return;
        var dl = Dl;
        var h = S(big ? 34 : 10);
        var r = S(big ? 8 : 3);
        var max = pos + new Vector2(width, h);
        if (big) dl.AddRect(pos - new Vector2(S(1)), max + new Vector2(S(1)), Palette.U32(Palette.Black), r + S(1), ImDrawFlags.None, S(2));
        var each = width / rgbs.Count;
        for (var i = 0; i < rgbs.Count; i++)
        {
            var a = new Vector2(pos.X + each * i, pos.Y);
            var b = new Vector2(i == rgbs.Count - 1 ? max.X : MathF.Round(pos.X + each * (i + 1)), max.Y);
            var flags = rgbs.Count == 1 ? ImDrawFlags.RoundCornersAll
                : i == 0 ? ImDrawFlags.RoundCornersLeft
                : i == rgbs.Count - 1 ? ImDrawFlags.RoundCornersRight
                : ImDrawFlags.RoundCornersNone;
            dl.AddRectFilled(a, b, Palette.U32(Palette.FromRgb(rgbs[i])), flags == ImDrawFlags.RoundCornersNone ? 0 : r, flags);
            if (big && names != null && i < names.Count && names[i].Length > 0)
            {
                var ink = DyeInk.IsDark(rgbs[i]) ? Palette.Ink : Palette.OnField;
                var name = Txt.Fit(names[i], Face.SansSemibold, 10, b.X - a.X - S(10));
                Txt.Draw(dl, new Vector2(a.X + S(7), b.Y - S(5) - Txt.Natural(Face.SansSemibold, 10)), name, Face.SansSemibold, 10, ink);
            }
        }
        if (!big) dl.AddRect(pos - new Vector2(0.5f), max + new Vector2(0.5f), Palette.U32(Palette.Rgba(0, 0, 0, 0.35f)), r, ImDrawFlags.None, 1f);
    }

    // ---------------------------------------------------------------------------------------------
    // The seal and the sign: drawn, then turned.

    /// <summary>
    /// The maker's seal (the mock's <c>.seal</c>): their initials in Bricolage in a square stamp,
    /// 2px edge with a 1px outline 2px outside it, turned −4°; lilac on the ink, ink on the field.
    /// <paramref name="sizeCss"/> 30 (24 on a small sheet card, its words then 9px).
    /// </summary>
    public static void Seal(Vector2 center, string initials, float sizeCss = 30, float degrees = -4, Vector4? color = null)
    {
        var dl = Dl;
        var col = Palette.U32(color ?? Tint.Accent);
        var start = TurnStart(dl);
        var half = S(sizeCss) / 2;
        var e = S(2);
        dl.AddRect(center - new Vector2(half - e / 2), center + new Vector2(half - e / 2), col, S(3), ImDrawFlags.None, e);
        var o = half + S(2) + S(0.5f);
        dl.AddRect(center - new Vector2(o), center + new Vector2(o), col, S(4), ImDrawFlags.None, S(1));
        var px = sizeCss <= 26 ? 10 : 11;
        var caps = Txt.Upper(initials);
        var tw = Txt.SpacedWidth(caps, Face.Display, px, 0.04f);
        var th = Txt.Natural(Face.Display, px);
        Txt.Spaced(dl, center - new Vector2(tw / 2, th / 2), caps, Face.Display, px, color ?? Tint.Accent, 0.04f);
        TurnSince(dl, start, center, degrees);
    }

    /// <summary>The sign's size for its words (before it's turned).</summary>
    public static Vector2 SignSize(string title, string? hand)
    {
        var tw = DisplayWidth(title, 24, 0.01f);
        var hw = hand != null ? Txt.Measure(hand, Face.Hand, 13).X : 0;
        var h = S(14) + Txt.Natural(Face.Display, 24) * 0.8f + (hand != null ? S(3) + Txt.Natural(Face.Hand, 13) : 0) + S(15);
        return new Vector2(MathF.Max(tw + S(3), hw) + S(22) * 2, h);
    }

    /// <summary>
    /// The sign (an empty page's title, the mock's <c>.sign</c>): a white card with a 3px black edge
    /// and a hard 5px block (deep violet; black on the field), its title in Bricolage capitals with a
    /// FieldHi shadow, a hand-lettered line under it, the whole turned −5° about its middle.
    /// </summary>
    public static void Sign(Vector2 center, string title, string? hand = null, float degrees = -5)
    {
        var dl = Dl;
        var size = SignSize(title, hand);
        var min = center - size / 2;
        var max = center + size / 2;
        var start = TurnStart(dl);
        var b = S(5);
        dl.AddRectFilled(min + new Vector2(b), max + new Vector2(b), Palette.U32(Tint.Block), S(6));
        dl.AddRectFilled(min, max, Palette.U32(Palette.Ink), S(6));
        var e = S(3);
        dl.AddRect(min + new Vector2(e / 2), max - new Vector2(e / 2), Palette.U32(Palette.Black), S(6) - e / 2, ImDrawFlags.None, e);
        var caps = Txt.Upper(title);
        var tw = Txt.SpacedWidth(caps, Face.Display, 24, 0.01f);
        // Bricolage's capitals sit in the top of its line: the line box is 0.95em, so pull it up a little.
        var ty = min.Y + S(14) - Txt.Natural(Face.Display, 24) * 0.1f;
        Txt.Spaced(dl, new Vector2(center.X - tw / 2 + S(3), ty + S(3)), caps, Face.Display, 24, Palette.FieldHi, 0.01f);
        Txt.Spaced(dl, new Vector2(center.X - tw / 2, ty), caps, Face.Display, 24, Palette.OnField, 0.01f);
        if (hand != null)
        {
            var hw = Txt.Measure(hand, Face.Hand, 13).X;
            Txt.Draw(dl, new Vector2(center.X - hw / 2, min.Y + S(14) + Txt.Natural(Face.Display, 24) * 0.8f + S(3)), hand, Face.Hand, 13, Palette.OnField2);
        }
        TurnSince(dl, start, center, degrees);
    }

    /// <summary>
    /// An empty page (the mock's <c>.empty-s</c>), laid at the cursor across the box: the dashed
    /// edge (28px corners; none with <paramref name="border"/> false), the cloud low at its left, then
    /// centred: the sign (<paramref name="sign"/>, with <paramref name="hand"/> under it) or a display
    /// title (<paramref name="title"/>, 17px), the <paramref name="line"/> (muted 13.5, 360 wide at
    /// most) and an outlined small button (<paramref name="action"/>). <paramref name="heightCss"/>
    /// 0 fits what's in it; taller, it's centred. True when the button's pressed.
    /// </summary>
    public static bool EmptyState(string id, string line, string? sign = null, string? hand = null, string? title = null,
        string? action = null, Icon? actionIcon = null, float heightCss = 0, bool border = true)
    {
        var at = At;
        var w = Width;
        var gap = S(14);
        var inner = MathF.Min(S(360), w - S(36));
        // Measure first: what's in it, top to bottom.
        var signSize = sign != null ? SignSize(sign, hand) : Vector2.Zero;
        var titleH = title != null ? DisplayHeight(17) : 0;
        EmptyLine[0] = new Run(line, Face.Sans, 14, Tint.Muted);
        var lineH = Txt.Flow(Vector2.Zero, inner, EmptyLine, 20, out _, center: true, draw: false);
        var btnH = action != null ? ButtonHeight(BtnSize.Sm) : 0;
        var parts = (sign != null ? 1 : 0) + (title != null ? 1 : 0) + 1 + (action != null ? 1 : 0);
        // The sign turned reaches past its box a little: room for that and its block.
        var content = (sign != null ? signSize.Y + S(14) : 0) + titleH + lineH + btnH + gap * (parts - 1);
        var h = MathF.Max(S(heightCss), content + S(30 + 24));
        var dl = Dl;
        var max = at + new Vector2(w, h);
        if (border) Dashed(at, max, S(Palette.RadiusXl), Palette.LineStrong, 1f);
        Cloud(new Vector2(at.X + w * 0.07f, max.Y - S(8 + 60)), new Vector2(120, 60));
        var y = at.Y + (h - content) / 2 + S(heightCss > 0 ? 0 : 3);
        var cx = at.X + w / 2;
        if (sign != null)
        {
            Sign(new Vector2(cx, y + S(7) + signSize.Y / 2), sign, hand);
            y += signSize.Y + S(14) + gap;
        }
        if (title != null)
        {
            var tw = MathF.Min(DisplayWidth(title, 17), w - S(36));
            Display(new Vector2(cx - tw / 2, y), title, 17, Tint.Ink, w - S(36));
            y += titleH + gap;
        }
        Txt.Flow(new Vector2(cx - inner / 2, y), inner, EmptyLine, 20, out _, center: true);
        y += lineH + gap;
        var pressed = false;
        if (action != null)
        {
            var bw = ButtonWidth(action, BtnSize.Sm, actionIcon);
            pressed = Button(id, new Vector2(cx - bw / 2, y), action, Btn.Glass, BtnSize.Sm, actionIcon, iconCss: 14);
        }
        Reserve(at, w, h);
        return pressed;
    }

    private static readonly Run[] EmptyLine = new Run[1];

    // ---------------------------------------------------------------------------------------------
    // Rings and the bar.

    /// <summary>
    /// The readiness ring with its numbers (the mock's <c>.ring</c>, 64 or 120 across): the track and
    /// the arc for <paramref name="have"/> of <paramref name="of"/> (round ends, from the top), and
    /// "5/7" in Bricolage in its middle (17px, 30 on the big one). Ice on the ink; on the field, the
    /// field's ink on its 18% track.
    /// </summary>
    public static void ReadyRing(Vector2 center, int have, int of, float sizeCss = 64, Vector4? arc = null)
    {
        var big = sizeCss >= 100;
        var thick = S(big ? 9 : 6);
        Ring(center, S(sizeCss) / 2, thick, of > 0 ? (float)have / of : 0, arc);
        var text = $"{have}/{of}";
        var px = big ? 30 : 17;
        var tw = Txt.Measure(text, Face.Display, px);
        Txt.Draw(Dl, center - tw / 2, text, Face.Display, px, Tint.Ink);
    }

    /// <summary>
    /// The halo: a dashed circle (2px dashes as long as their gaps) round something to point it out,
    /// lilac on the ink, the field's ink on the field.
    /// </summary>
    public static void Halo(Vector2 center, float radiusCss, Vector4? color = null, float thicknessCss = 1.5f)
    {
        var r = S(radiusCss);
        var t = S(thicknessCss);
        var key = (r, t);
        if (!HaloCache.TryGetValue(key, out var dashes))
        {
            dashes = Model.Dashes.Along(Model.Dashes.Circle(Vector2.Zero, r, 96), t * 4);
            if (HaloCache.Count >= 32) HaloCache.Clear();
            HaloCache[key] = dashes;
        }
        var col = Palette.U32(color ?? Tint.Accent);
        var dl = Dl;
        foreach (var dash in dashes)
        {
            if (dash.Length < 2) continue;
            if (DashBuffer.Length < dash.Length) DashBuffer = new Vector2[Math.Max(dash.Length, DashBuffer.Length * 2)];
            for (var i = 0; i < dash.Length; i++) DashBuffer[i] = center + dash[i];
            dl.AddPolyline(ref DashBuffer[0], dash.Length, col, ImDrawFlags.None, t);
        }
    }

    private static readonly Dictionary<(float R, float T), List<Vector2[]>> HaloCache = new();

    /// <summary>
    /// The bar (the mock's <c>.meter</c>): 6px (8 with <paramref name="large"/>), the tile's track,
    /// its fill ice (or the field's periwinkle while working, amber when nearly full); on the field,
    /// the field's ink on its 18% track.
    /// </summary>
    public static void Meter(Vector2 pos, float width, float fraction, MeterKind kind = MeterKind.Ice, bool large = false)
    {
        var dl = Dl;
        var h = S(large ? 8 : 6);
        dl.AddRectFilled(pos, pos + new Vector2(width, h), Palette.U32(Tint.Track), h / 2);
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0) return;
        var fill = IsOnField ? Palette.OnField : kind switch
        {
            MeterKind.Working => Palette.Field,
            MeterKind.Amber => Palette.Amber,
            _ => Palette.Ice,
        };
        dl.AddRectFilled(pos, pos + new Vector2(MathF.Max(h, width * fraction), h), Palette.U32(fill), h / 2);
    }

    // ---------------------------------------------------------------------------------------------
    // Headings and tabs.

    /// <summary>
    /// A section's heading (the mock's <c>.panel-h</c>), laid at the cursor across the box: its title
    /// in Bricolage capitals (17px; 13 in a side pane), a small count after it in faint mono, and a
    /// lilac link with an arrow at the right. 10px under it. True when the link's pressed.
    /// </summary>
    public static bool SectionHead(string id, string title, string? count = null, string? link = null, int px = 17)
    {
        var at = At;
        var w = Width;
        var h = DisplayHeight(px);
        var countPx = px <= 13 ? 11 : 12;
        var linkW = link != null ? Txt.Measure(link, Face.SansSemibold, 13).X + S(4 + 14) : 0;
        var countW = count != null ? S(10) + Txt.Measure(count, Face.Mono, countPx).X : 0;
        var tw = Display(at, title, px, Tint.Ink, w - linkW - countW - (link != null ? S(12) : 0));
        if (count != null)
        {
            // On the title's baseline.
            var cy = at.Y + Txt.Ascent(Face.Display, px) - Txt.Ascent(Face.Mono, countPx);
            Txt.Draw(Dl, new Vector2(at.X + tw + S(10), cy), count, Face.Mono, countPx, Tint.Faint);
        }
        var pressed = false;
        if (link != null)
        {
            var lh = Txt.Natural(Face.SansSemibold, 13);
            var lp = new Vector2(at.X + w - linkW, at.Y + (h - lh) / 2);
            var (clicked, hovered, _) = Hit(id, lp - new Vector2(S(4)), new Vector2(linkW, lh) + new Vector2(S(8)));
            var col = hovered ? Tint.Ink : Tint.Accent;
            var lw = Txt.Draw(Dl, lp, link, Face.SansSemibold, 13, col);
            Icons.Draw(Dl, Icon.ArrowRight, new Vector2(lp.X + lw + S(4), lp.Y + (lh - S(14)) / 2), S(14), col);
            pressed = clicked;
        }
        Reserve(at, w, h + S(10));
        return pressed;
    }

    private static float UnderTabWidth(TabSpec t) =>
        S(10) * 2 + Txt.Measure(t.Label, Face.SansSemibold, 14).X + (t.Count != null ? S(7) + Txt.Measure(t.Count, Face.Mono, 11).X : 0);

    /// <summary>
    /// Tabs underlined (the mock's <c>.tabs</c>, Looks' Your looks · Loved · Wear today), laid at the
    /// cursor across the box: 40 tall, Figtree 600, a count in faint mono after each (lilac on the one
    /// picked), the holo underline under the one picked, the line under them all. Returns the tab picked.
    /// </summary>
    public static int UnderlineTabs(string id, IReadOnlyList<TabSpec> tabs, int selected)
    {
        var at = At;
        var w = Width;
        var h = S(40);
        var dl = Dl;
        dl.AddLine(new Vector2(at.X, at.Y + h - 0.5f), new Vector2(at.X + w, at.Y + h - 0.5f), Palette.U32(Palette.Line), 1f);
        var result = selected;
        var x = at.X;
        ImGui.PushID(id);
        try
        {
            for (var i = 0; i < tabs.Count; i++)
            {
                var t = tabs[i];
                var tw = UnderTabWidth(t);
                var p = new Vector2(x, at.Y);
                var (clicked, hovered, _) = Hit(Part(i), p, new Vector2(tw, h));
                if (clicked) result = i;
                var on = i == selected;
                var ink = on ? Palette.Ink : hovered ? Palette.Body : Palette.Muted;
                var tx = p.X + S(10);
                tx += Txt.Line(dl, new Vector2(tx, p.Y), t.Label, Face.SansSemibold, 14, ink, 40);
                if (t.Count != null) Txt.Line(dl, new Vector2(tx + S(7), p.Y + S(1)), t.Count, Face.Mono, 11, on ? Palette.Accent : Palette.Faint, 40);
                if (on) HoloUnderline(p.X + S(10), p.X + tw - S(10), p.Y + h);
                FocusRing(p, p + new Vector2(tw, h), S(8));
                x += tw + S(4);
            }
        }
        finally
        {
            ImGui.PopID();
        }
        Reserve(at, w, h);
        return result;
    }

    // ---------------------------------------------------------------------------------------------
    // The combo.

    public static float ComboWidth(string label, Icon? lead = null) =>
        S(13 + 11) + Txt.Measure(label, Face.SansSemibold, 13).X + S(7 + 14) + (lead != null ? S(14 + 7) : 0);

    /// <summary>
    /// A combo's pill (the mock's <c>.combo</c>): 34 tall, the strong edge (lilac while open), its
    /// words, a lead icon if given (✓ in ice for "Ready"), the chevron (up while open). True when
    /// pressed: open your popup with <see cref="BeginMenu"/> under it.
    /// </summary>
    public static bool Combo(string id, Vector2 pos, string label, bool open = false, Icon? lead = null, Vector4? leadColor = null, float width = 0)
    {
        var w = width > 0 ? width : ComboWidth(label, lead);
        var h = S(34);
        var (clicked, hovered, _) = Hit(id, pos, new Vector2(w, h));
        var dl = Dl;
        var max = pos + new Vector2(w, h);
        if (hovered || open) dl.AddRectFilled(pos, max, Palette.U32(Tint.Wash), h / 2);
        dl.AddRect(pos + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(open || hovered ? (IsOnField ? Palette.OnField : Palette.Accent) : Tint.Edge), h / 2, ImDrawFlags.None, 1f);
        var x = pos.X + S(13);
        if (lead is { } l)
        {
            Icons.Draw(dl, l, new Vector2(x, pos.Y + (h - S(14)) / 2), S(14), leadColor ?? Palette.Ice);
            x += S(14 + 7);
        }
        var room = max.X - S(11 + 14 + 7) - x;
        Txt.Line(dl, new Vector2(x, pos.Y), Txt.Fit(label, Face.SansSemibold, 13, room), Face.SansSemibold, 13, Tint.Ink, 34);
        Icons.Draw(dl, open ? Icon.ChevronUp : Icon.ChevronDown, new Vector2(max.X - S(11 + 14), pos.Y + (h - S(14)) / 2), S(14), Tint.Muted);
        FocusRing(pos, max, h / 2);
        return clicked;
    }

    // ---------------------------------------------------------------------------------------------
    // The face and the dots.

    /// <summary>
    /// A character's face (the mock's <c>.face</c>): their initials in Bricolage on a periwinkle circle,
    /// <paramref name="sizeCss"/> across (24 in the pill, 34 in Settings); <paramref name="off"/> on the
    /// soft tile in muted (not linked, not sent).
    /// </summary>
    public static void Avatar(Vector2 center, string initials, float sizeCss = 24, bool off = false)
    {
        var dl = Dl;
        dl.AddCircleFilled(center, S(sizeCss) / 2, Palette.U32(off ? Palette.Tile : Palette.Field), 32);
        var px = sizeCss >= 30 ? 12 : 10;
        var caps = Txt.Upper(initials);
        var tw = Txt.SpacedWidth(caps, Face.Display, px, 0.02f);
        var th = Txt.Natural(Face.Display, px);
        Txt.Spaced(dl, center - new Vector2(tw / 2, th / 2), caps, Face.Display, px, off ? Palette.Muted : Palette.OnField, 0.02f);
    }

    /// <summary>A 7px dot (the mock's <c>.dot</c>): live and recording ringed (3px of their colour at 18–20%), live's ring breathing.</summary>
    public static void StatusDot(Vector2 center, DotKind kind, bool pulse = false)
    {
        var dl = Dl;
        var col = kind switch
        {
            DotKind.Live => Palette.Accent,
            DotKind.Recording or DotKind.Danger => Palette.Danger,
            DotKind.No => Palette.Faint,
            DotKind.Amber => Palette.Amber,
            DotKind.Pink => Palette.Pink,
            _ => Palette.Ice,
        };
        if (kind is DotKind.Live or DotKind.Recording)
        {
            var grow = pulse ? Pulse() : 1f;
            dl.AddCircleFilled(center, S(3.5f) + S(3) * grow, Palette.U32(Palette.WithAlpha(col, kind == DotKind.Live ? 0.18f : 0.2f)), 20);
        }
        dl.AddCircleFilled(center, S(3.5f), Palette.U32(col), 16);
    }

    /// <summary>A status pill's dot for its tone.</summary>
    public static void StatusDot(Vector2 center, Tone tone, bool pulse = false) => StatusDot(center, DotOf(tone), pulse);

    public static DotKind DotOf(Tone tone) => tone switch
    {
        Tone.Live or Tone.Waiting => DotKind.Live,
        Tone.Recording => DotKind.Recording,
        Tone.Danger => DotKind.Danger,
        Tone.Neutral => DotKind.No,
        _ => DotKind.Ice,
    };
}

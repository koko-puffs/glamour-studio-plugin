using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>The sticker's sizes: 34 (sm), 40, 46 (lg).</summary>
public enum StickerSize
{
    Sm,
    Md,
    Lg,
}

/// <summary>The sticker: a screen's one thing to do.</summary>
public static partial class Kit
{
    /// <summary>Height, padding either side, words' size (Figtree 800; the mock's 13.5 and 14.5 drawn at 14 and 15), corners, icon.</summary>
    private static (float H, float Pad, int Px, float Radius, float Icon) StickerSpec(StickerSize size) => size switch
    {
        StickerSize.Sm => (34, 13, 13, 12, 14),
        StickerSize.Lg => (46, 22, 15, 14, 16),
        _ => (40, 17, 14, 14, 16),
    };

    public static float StickerHeight(StickerSize size = StickerSize.Md) => S(StickerSpec(size).H);

    /// <summary>The sticker's width for its words (and icon): its padding and 2px edges included, its block not.</summary>
    public static float StickerWidth(string label, StickerSize size = StickerSize.Md, Icon? icon = null)
    {
        var (_, pad, px, _, iconCss) = StickerSpec(size);
        return S(2 + pad) * 2 + Txt.Measure(label, Face.SansExtraBold, px).X + (icon != null ? S(iconCss + 8) : 0);
    }

    /// <summary>
    /// The sticker (the mock's <c>.sticker</c>): the field's periwinkle, ink words in Figtree 800, a
    /// 2px ink edge and a hard 3px block of deep violet down-right (a second rect). Pointed at,
    /// FieldHi; pressed, it moves 3px into its block and the block goes. On the field (or with
    /// <paramref name="turned"/>) it turns over: ink with white words, a black edge and block.
    /// Disabled, it stays a sticker at half strength and its <paramref name="tip"/> (say why) still
    /// shows. <paramref name="width"/> is screen px (0 fits the words). Its block reaches 3px past
    /// its size: leave room. True when pressed.
    /// </summary>
    public static bool Sticker(string id, Vector2 pos, string label, StickerSize size = StickerSize.Md, Icon? icon = null,
        float width = 0, bool disabled = false, string? tip = null, bool? turned = null)
    {
        var (hCss, padCss, px, rCss, iconCss) = StickerSpec(size);
        var h = S(hCss);
        var w = width > 0 ? width : StickerWidth(label, size, icon);
        var (clicked, hovered, held) = Hit(id, pos, new Vector2(w, h), disabled);
        if (tip != null && !Blocked && ImGui.IsMouseHoveringRect(pos, pos + new Vector2(w, h)) && ImGui.IsWindowHovered()) Tooltip(tip);

        var over = turned ?? IsOnField;
        var old = Palette.Opacity;
        if (disabled) Palette.Opacity = old * 0.5f;
        var fill = over ? (hovered ? Palette.OnField2 : Palette.OnField) : (hovered || held ? Palette.FieldHi : Palette.Field);
        var edge = over ? Palette.Black : Palette.StickerEdge;
        var block = over ? Palette.Black : Palette.StickerShadow;
        var ink = over ? Palette.Ink : Palette.OnField;

        var dl = Dl;
        var r = S(rCss);
        var off = S(3);
        var pressed = held && !disabled;
        var p = pressed ? pos + new Vector2(off) : pos;
        var max = p + new Vector2(w, h);
        if (!pressed) dl.AddRectFilled(pos + new Vector2(off), pos + new Vector2(w, h) + new Vector2(off), Palette.U32(block), r);
        dl.AddRectFilled(p, max, Palette.U32(fill), r);
        var e = S(2);
        dl.AddRect(p + new Vector2(e / 2), max - new Vector2(e / 2), Palette.U32(edge), MathF.Max(0, r - e / 2), ImDrawFlags.None, e);

        var iconPx = S(iconCss);
        var room = w - S(2 + padCss) * 2 - (icon != null ? iconPx + S(8) : 0);
        var text = Txt.Fit(label, Face.SansExtraBold, px, MathF.Max(0, room + 1));
        var textW = Txt.Measure(text, Face.SansExtraBold, px).X;
        var content = (icon != null ? iconPx + (text.Length > 0 ? S(8) : 0) : 0) + textW;
        var x = p.X + (w - content) / 2;
        if (icon is { } ic)
        {
            Icons.Draw(dl, ic, new Vector2(x, p.Y + (h - iconPx) / 2), iconPx, ink, 2f);
            x += iconPx + S(8);
        }
        if (text.Length > 0) Txt.Line(dl, new Vector2(x, p.Y), text, Face.SansExtraBold, px, ink, hCss);
        Palette.Opacity = old;
        FocusRing(p, max, r);
        return clicked;
    }
}

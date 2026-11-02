using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace GlamourStudio.UI;

/// <summary>
/// Glamour Studio's colours, as the site has them (src/features/glamour-studio/styles/glamour.css,
/// <c>--gs-*</c>): dark only, the night sky of near black, panels a shade lighter with hairline
/// edges, quiet greys for words, and one rose that glows where it matters: the thing to do, where
/// you are, what's yours to get. Filled, the rose is the favicon's deeper one with white words
/// (<see cref="RoseFill"/>); the bright one is for words, icons, rings and glows on the dark.
/// </summary>
public static class Palette
{
    // Surfaces.
    public static readonly Vector4 Ground = Hex(0x050506);                 // --gs-ground: the window
    public static readonly Vector4 Footer = Hex(0x0d0c0e);                 // the title bar, the window's foot
    public static readonly Vector4 Card = Hex(0x0d0c0e);                   // --gs-card: cards
    public static readonly Vector4 Raised = Hex(0x151317);                 // --gs-raised: boxed lists, the question's card
    public static readonly Vector4 Float = Hex(0x0d0c0e);                  // menus, tooltips, toasts
    public static readonly Vector4 Field = Hex(0x151317);                  // fields (on a raised ground)
    public static readonly Vector4 Tile = Hex(0x1c191f);                   // --gs-soft: icon tiles and wells
    public static readonly Vector4 ButtonFill = Rgba(0, 0, 0, 0);          // an outlined pill's inside
    public static readonly Vector4 Picked = Hex(0xf3eff1);                 // --gs-chip: the picked one in a segmented group
    public static readonly Vector4 OnPicked = Hex(0x121113);               // --gs-on-chip

    // Words, brightest to quietest.
    public static readonly Vector4 White = Hex(0xffffff);
    public static readonly Vector4 Ink = Hex(0xf3eff1);                    // --gs-ink: headings
    public static readonly Vector4 Soft = Hex(0xd3cdd0);                   // --gs-body: what you read
    public static readonly Vector4 Light = Hex(0xbdb7bb);                  // quiet buttons, links
    public static readonly Vector4 Muted = Hex(0xa7a1a6);                  // --gs-muted
    public static readonly Vector4 Faint = Hex(0x6f6970);                  // --gs-faint: what hardly needs reading

    // The rose: the thing to do, where you are, what's yours to get.
    public static readonly Vector4 Rose = Hex(0xff4f86);                   // --gs-accent
    public static readonly Vector4 RoseHover = Hex(0xff6f9c);              // --gs-accent-hover
    public static readonly Vector4 RoseSoft = Hex(0xffc2d5);               // words on its wash
    public static readonly Vector4 RoseText = Hex(0xff8fb2);               // rose notes, easier to read than the accent
    public static readonly Vector4 RoseFill = Hex(0xd0104c);               // --gs-accent-fill: what's filled rose (the rose pill, a switch that's on), the favicon's
    public static readonly Vector4 RoseFillHover = Hex(0xe01d5b);          // --gs-accent-fill-hover
    public static readonly Vector4 OnRose = Hex(0xffffff);                 // --gs-on-accent: white on the filled rose (5.1:1)
    public static readonly Vector4 RoseWash = Hex(0x22121a);               // --gs-blush: a highlighted card
    public static readonly Vector4 RoseTile = Hex(0x3a1a28);               // --gs-blush-2: the round wells behind its icons
    public static readonly Vector4 RosePicked = Rgba(255, 79, 134, 0.13f); // --gs-accent-soft
    public static readonly Vector4 RoseEdge = Rgba(255, 79, 134, 0.45f);   // --gs-accent-line
    public static readonly Vector4 Glow = Rgba(255, 79, 134, 0.34f);       // --gs-glow

    // States.
    public static readonly Vector4 Danger = Hex(0xff8080);
    public static readonly Vector4 DangerSolid = Hex(0xff4f4f);
    public static readonly Vector4 DangerWash = Rgba(255, 79, 79, 0.1f);
    public static readonly Vector4 DangerEdge = Rgba(255, 79, 79, 0.45f);
    public static readonly Vector4 Success = Hex(0x5fd3a1);                // --gs-ok: yours, done, linked
    public static readonly Vector4 SuccessWash = Rgba(95, 211, 161, 0.1f);

    // Lines.
    public static readonly Vector4 LineFaint = Rgba(255, 255, 255, 0.05f); // dividers in cards, hover fills
    public static readonly Vector4 LinePicked = Rgba(255, 255, 255, 0.09f);
    public static readonly Vector4 Line = Hex(0x26232a);                   // --gs-line: cards
    public static readonly Vector4 LineStrong = Hex(0x3a353f);             // --gs-line-strong: things you press or type in
    public static readonly Vector4 LineHover = Hex(0x57505d);              // any edge pointed at
    public static readonly Vector4 LineDashed = Hex(0x3a353f);             // things you can add
    public static readonly Vector4 RowHover = Rgba(255, 255, 255, 0.04f);
    public static readonly Vector4 Track = Hex(0x26232a);                  // a progress bar's track

    // Radii (CSS px, before Dalamud's global scale): the site's corners.
    public const float RadiusSm = 8;     // --gs-radius-xs: icon tiles, swatches, tick boxes
    public const float RadiusMd = 12;    // --gs-radius-sm: fields, menu rows, list rows
    public const float RadiusLg = 18;    // --gs-radius: cards, menus
    public const float RadiusXl = 22;    // the question's card
    public const float Radius2Xl = 22;
    public const float RadiusCard = 18;
    public const float RadiusPill = 999; // pills, segmented groups, switches, chips

    /// <summary>Everything drawn is multiplied by this (0.5 for a disabled button, 0.4 for a small control).</summary>
    public static float Opacity { get; set; } = 1f;

    public static Vector4 Hex(uint rgb, float alpha = 1f) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

    public static Vector4 Rgba(int r, int g, int b, float a) => new(r / 255f, g / 255f, b / 255f, a);

    /// <summary>A colour from the game's 0xRRGGBB (a dye's swatch).</summary>
    public static Vector4 FromRgb(uint rgb) => Hex(rgb & 0xFFFFFF);

    /// <summary>A translucent colour flattened over an opaque one.</summary>
    public static Vector4 Over(Vector4 top, Vector4 under) =>
        new(under.X + (top.X - under.X) * top.W, under.Y + (top.Y - under.Y) * top.W, under.Z + (top.Z - under.Z) * top.W, 1f);

    public static Vector4 WithAlpha(Vector4 c, float a) => new(c.X, c.Y, c.Z, a);

    public static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, c.W * Opacity));
}

using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace GlamourStudio.UI;

/// <summary>
/// Glamour Studio's colours, as the site has them since its "Moonlit Atelier" redesign (November
/// 2026; src/features/glamour-studio/styles/glamour.css, <c>--gs-*</c>): dark only, a violet night
/// for the window, cards a shade lighter with lilac-tinted hairline edges, lavender greys for words;
/// lilac (<see cref="Accent"/>) for words, rings and where you are, filled violet
/// (<see cref="AccentFill"/>, white words) for the one thing to do; orchid (<see cref="Love"/>) for
/// loves and the pieces you don't have yet; mint (<see cref="Success"/>) for what's yours, done or
/// linked. (It was a near-black window with one rose for all three until 0.6.16.)
/// </summary>
public static class Palette
{
    // Surfaces.
    public static readonly Vector4 Ground = Hex(0x0b0912);                 // --gs-ground: the window
    public static readonly Vector4 Footer = Hex(0x100c1a);                 // --gs-ground-2: the title bar, the window's foot
    public static readonly Vector4 Card = Hex(0x151022);                   // --gs-card: cards
    public static readonly Vector4 Raised = Hex(0x1d172e);                 // --gs-raised: boxed lists, the question's card
    public static readonly Vector4 Float = Hex(0x151022);                  // menus, tooltips, toasts
    public static readonly Vector4 Field = Hex(0x1d172e);                  // fields (on a raised ground)
    public static readonly Vector4 Tile = Hex(0x261f3c);                   // --gs-soft: icon tiles and wells
    public static readonly Vector4 ButtonFill = Rgba(0, 0, 0, 0);          // an outlined pill's inside
    public static readonly Vector4 Rail = Hex(0x0e0a17);                   // the navigation rail and the side panes
    public static readonly Vector4 Divider = Hex(0x211a36);                // between the header, the rail, the panes
    public static readonly Vector4 Picked = Hex(0x7b55f0);                 // --gs-chip: the picked one in a segmented group (violet)
    public static readonly Vector4 OnPicked = Hex(0xffffff);               // --gs-on-chip

    // Words, brightest to quietest.
    public static readonly Vector4 White = Hex(0xffffff);
    public static readonly Vector4 Ink = Hex(0xf6f1ff);                    // --gs-ink: headings
    public static readonly Vector4 Soft = Hex(0xd8cff0);                   // --gs-body: what you read
    public static readonly Vector4 Light = Hex(0xc2b8e0);                  // quiet buttons, links
    public static readonly Vector4 Muted = Hex(0xa69cc6);                  // --gs-muted
    public static readonly Vector4 Dim = Hex(0x8a80ad);                    // counts, slot lines, small labels (readable at 11px)
    public static readonly Vector4 Faint = Hex(0x6e6591);                  // --gs-faint: what hardly needs reading

    // The accent: lilac for words, icons, rings and where you are; filled, violet with white words.
    public static readonly Vector4 Accent = Hex(0xb9a3ff);                 // --gs-accent
    public static readonly Vector4 AccentHover = Hex(0xcdbcff);            // --gs-accent-hover
    public static readonly Vector4 AccentSoft = Hex(0xe6ddff);             // words on its wash
    public static readonly Vector4 AccentText = Hex(0xc9b8ff);             // lilac notes and links, easier to read than the accent
    public static readonly Vector4 AccentFill = Hex(0x7b55f0);             // --gs-accent-fill: what's filled (the violet pill, a switch that's on)
    public static readonly Vector4 AccentFillHover = Hex(0x6c46e4);        // --gs-accent-fill-hover
    public static readonly Vector4 OnAccent = Hex(0xffffff);               // --gs-on-accent: white on the violet
    public static readonly Vector4 Blush = Hex(0x1d1534);                  // --gs-blush: a highlighted card
    public static readonly Vector4 BlushTile = Hex(0x2c2150);              // --gs-blush-2: the round wells behind its icons
    public static readonly Vector4 AccentPicked = Rgba(185, 163, 255, 0.14f); // --gs-accent-soft
    public static readonly Vector4 AccentEdge = Rgba(185, 163, 255, 0.45f);   // --gs-accent-line
    public static readonly Vector4 AccentChip = Rgba(185, 163, 255, 0.08f);   // a lilac chip's inside
    public static readonly Vector4 AccentRow = Rgba(185, 163, 255, 0.1f);     // the row picked in a list
    public static readonly Vector4 Glow = Rgba(140, 100, 255, 0.4f);          // --gs-glow

    // Orchid: loves, and the pieces you don't have yet.
    public static readonly Vector4 Love = Hex(0xf59ee6);                   // --gs-love
    public static readonly Vector4 LoveFill = Hex(0xc03a9c);               // --gs-love-fill
    public static readonly Vector4 LoveChip = Rgba(245, 158, 230, 0.08f);  // an orchid chip's inside
    public static readonly Vector4 LoveWash = Rgba(245, 158, 230, 0.14f);  // --gs-love-soft
    public static readonly Vector4 LoveEdge = Rgba(245, 158, 230, 0.45f);  // --gs-love-line

    // The moon's other colours (the iridescent progress bar: lilac to orchid to ice).
    public static readonly Vector4 Ice = Hex(0x8fd9ff);                    // --gs-ice
    public static readonly Vector4 Amber = Hex(0xffd27f);                  // --gs-amber

    // States.
    public static readonly Vector4 Danger = Hex(0xff8a9b);                 // --gs-danger
    public static readonly Vector4 DangerSolid = Hex(0xff6b81);
    public static readonly Vector4 DangerWash = Rgba(255, 138, 155, 0.1f);
    public static readonly Vector4 DangerEdge = Rgba(255, 138, 155, 0.45f);
    public static readonly Vector4 Success = Hex(0x7ff0cf);                // --gs-ok: yours, done, linked (mint)
    public static readonly Vector4 SuccessWash = Rgba(127, 240, 207, 0.1f);
    public static readonly Vector4 SuccessChip = Rgba(127, 240, 207, 0.08f);  // a mint chip's inside
    public static readonly Vector4 SuccessEdge = Rgba(127, 240, 207, 0.4f);   // a mint chip's and card's edge

    // Lines.
    public static readonly Vector4 LineFaint = Rgba(200, 185, 255, 0.06f); // dividers in cards, hover fills
    public static readonly Vector4 LinePicked = Rgba(200, 185, 255, 0.1f);
    public static readonly Vector4 Line = Hex(0x2d2547);                   // --gs-line: cards
    public static readonly Vector4 LineStrong = Hex(0x463a6e);             // --gs-line-strong: things you press or type in
    public static readonly Vector4 LineHover = Hex(0x6b5aa6);              // any edge pointed at (the lilac line, solid)
    public static readonly Vector4 LineDashed = Hex(0x463a6e);             // things you can add
    public static readonly Vector4 RowHover = Rgba(185, 163, 255, 0.05f);
    public static readonly Vector4 Track = Hex(0x2d2547);                  // a progress bar's track

    // Radii (CSS px, before Dalamud's global scale): the site's corners.
    public const float RadiusSm = 9;     // --gs-radius-xs: icon tiles, swatches, tick boxes
    public const float RadiusMd = 14;    // --gs-radius-sm: fields, menu rows, list rows
    public const float RadiusLg = 20;    // --gs-radius: cards, menus
    public const float RadiusXl = 28;    // --gs-radius-xl: the question's card
    public const float Radius2Xl = 28;
    public const float RadiusCard = 20;
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

    /// <summary>Between two colours, <paramref name="t"/> of the way from the first.</summary>
    public static Vector4 Mix(Vector4 a, Vector4 b, float t) => a + (b - a) * t;

    /// <summary>The iridescent gradient (<c>--gs-iris</c>: lilac, orchid at 48%, ice) at <paramref name="t"/>, 0 to 1.</summary>
    public static Vector4 Iris(float t) =>
        t <= 0.48f ? Mix(Accent, Love, t / 0.48f) : Mix(Love, Ice, (t - 0.48f) / 0.52f);

    public static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, c.W * Opacity));
}

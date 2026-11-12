using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace GlamourStudio.UI;

/// <summary>
/// Glamour Studio's colours, as the site has them since its "Character Sheet" redesign (November
/// 2026; the plugin's mock-up, docs/redesign/assets/kit.css, holds them as these names): two
/// grounds, the ink (the studio: <see cref="Ground"/>, cards a step up, opaque lilac-grey hairlines)
/// and the field (flat periwinkle <see cref="Field"/>, ink-violet words <see cref="OnField"/>: a
/// screen's one moment, and the sticker). Three honest colours for what you have: owned
/// <see cref="Ice"/>, missing <see cref="Pink"/>, the wishlist <see cref="Starred"/>; amber for what
/// to see to, red only for what went wrong. (A violet night with mint and orchid, "Moonlit
/// Atelier", until the redesign: mint is ice now, orchid pink, and the iris gradient went.)
///
/// The names from before the redesign that the screens still use are kept, with the new values
/// (<see cref="Soft"/> stays the words you read; the mock's "Soft" fill is <see cref="Tile"/>).
/// </summary>
public static class Palette
{
    // ---------------------------------------------------------------------------------------------
    // The ink: the studio.

    public static readonly Vector4 Ground = Hex(0x0c0a12);                 // the window
    public static readonly Vector4 Ground2 = Hex(0x100d18);                // Dalamud's title bar, the side panes
    public static readonly Vector4 Card = Hex(0x14111d);                   // cards, callouts
    public static readonly Vector4 Raised = Hex(0x1c1828);                 // wells, text fields
    public static readonly Vector4 Tile = Hex(0x242036);                   // the mock's Soft: icon tiles, tags, tracks, a switch that's off
    public static readonly Vector4 Popup = Hex(0x18142a);                  // menus, tooltips, toasts (solid: no blur in the game)
    public static readonly Vector4 Blush = Hex(0x1c1730);                  // the picked card (a plate picked)
    public static readonly Vector4 Black = Hex(0x000000);                  // a callout's edge and block, the sticker's block on the field

    // Words on the ink, brightest to quietest.
    public static readonly Vector4 White = Hex(0xffffff);
    public static readonly Vector4 Ink = Hex(0xf6f3ff);                    // headings, names
    public static readonly Vector4 Body = Hex(0xd8d1f2);                   // what you read
    public static readonly Vector4 Muted = Hex(0xa59cc8);                  // lines under names
    public static readonly Vector4 Faint = Hex(0x77709a);                  // counts, what hardly needs reading

    // Lines (opaque, so they read the same on any ground).
    public static readonly Vector4 Line = Hex(0x252036);                   // cards, rows
    public static readonly Vector4 LineStrong = Hex(0x3a3356);             // what you press or type in, popups

    // ---------------------------------------------------------------------------------------------
    // The field: a screen's one moment, and the sticker.

    public static readonly Vector4 Field = Hex(0x8f7cf6);                  // the periwinkle; the sticker's fill on the ink
    public static readonly Vector4 FieldHi = Hex(0xa897ff);                // the sticker pointed at
    public static readonly Vector4 FieldDeep = Hex(0x6a55e6);              // the field's floor, the stacked title's first shadow
    public static readonly Vector4 FieldTone = Hex(0x7f6bf0);              // the echo (the name huge behind)
    public static readonly Vector4 OnField = Hex(0x160d33);                // words on the field, the sticker's words and edge
    public static readonly Vector4 OnField2 = Hex(0x2d1f66);               // quieter words on the field
    public static readonly Vector4 OnFieldLine = Rgba(22, 13, 51, 0.35f);  // an outlined pill's edge on the field
    public static readonly Vector4 OnFieldWash = Rgba(22, 13, 51, 0.14f);  // a tag on the field
    public static readonly Vector4 OnFieldTrack = Rgba(22, 13, 51, 0.18f); // a bar's or ring's track on the field
    public static readonly Vector4 OnFieldDots = Rgba(22, 13, 51, 0.16f);  // the field's dotted page
    public static readonly Vector4 StickerShadow = Hex(0x4b37c9);          // the sticker's block on the ink (black on the field)
    public static readonly Vector4 StickerEdge = Hex(0x160d33);            // the sticker's 2px edge on the ink (black on the field)

    // ---------------------------------------------------------------------------------------------
    // Lilac: where you are, words and rings on the ink; and the honest colours.

    public static readonly Vector4 Accent = Hex(0xc9b8ff);                 // where you are, links, eyebrows
    public static readonly Vector4 AccentWash = Rgba(201, 184, 255, 0.12f);  // the mock's accent-soft: a picked row, a hovered pill
    public static readonly Vector4 AccentLine = Rgba(201, 184, 255, 0.4f);   // the mock's accent-line: a picked row's edge, the live pill

    public static readonly Vector4 Ice = Hex(0x8ce2ff);                    // owned, done, linked
    public static readonly Vector4 IceWash = Rgba(140, 226, 255, 0.13f);
    public static readonly Vector4 Pink = Hex(0xf5a3dc);                   // missing, loves
    public static readonly Vector4 PinkWash = Rgba(245, 163, 220, 0.13f);
    public static readonly Vector4 PinkRow = Rgba(245, 163, 220, 0.07f);   // a missing piece's row
    public static readonly Vector4 Starred = Hex(0xc9b8ff);                // on your wishlist (lavender)
    public static readonly Vector4 StarredWash = Rgba(201, 184, 255, 0.13f);
    public static readonly Vector4 Amber = Hex(0xffc46b);                  // to see to: in your bags, a dye to do
    public static readonly Vector4 AmberWash = Rgba(255, 196, 107, 0.14f);
    public static readonly Vector4 AmberLine = Rgba(255, 196, 107, 0.5f);  // a warning toast's edge
    public static readonly Vector4 Danger = Hex(0xff7a9a);                 // went wrong
    public static readonly Vector4 DangerWash = Rgba(255, 122, 154, 0.12f);
    public static readonly Vector4 DangerEdge = Rgba(255, 122, 154, 0.45f);
    public static readonly Vector4 OnDanger = Hex(0x2a0711);               // words on the filled danger answer

    // ---------------------------------------------------------------------------------------------
    // The names from before the redesign, with the new values (the screens move to the names above).

    public static readonly Vector4 Footer = Ground2;
    public static readonly Vector4 Rail = Ground2;                         // (the rail went; the side panes)
    public static readonly Vector4 Divider = Line;
    public static readonly Vector4 Float = Popup;
    public static readonly Vector4 Input = Raised;                         // a text field's inside (was Palette.Field)
    public static readonly Vector4 ButtonFill = Rgba(0, 0, 0, 0);
    public static readonly Vector4 Picked = Field;                         // the picked one in a segmented group
    public static readonly Vector4 OnPicked = OnField;
    public static readonly Vector4 Soft = Body;                            // what you read (not the mock's Soft fill: that's Tile)
    public static readonly Vector4 Light = Body;                           // quiet buttons
    public static readonly Vector4 Dim = Hex(0x8e86b1);                    // between Muted and Faint: small lines that are still read
    public static readonly Vector4 AccentHover = Hex(0xdacdff);
    public static readonly Vector4 AccentSoft = Hex(0xe6ddff);             // words on the lilac wash
    public static readonly Vector4 AccentText = Accent;
    public static readonly Vector4 AccentFill = Field;                     // what's filled (a switch that's on)
    public static readonly Vector4 AccentFillHover = FieldHi;
    public static readonly Vector4 OnAccent = OnField;
    public static readonly Vector4 BlushTile = Hex(0x2a2245);              // the round wells behind icons
    public static readonly Vector4 AccentPicked = AccentWash;
    public static readonly Vector4 AccentEdge = AccentLine;
    public static readonly Vector4 AccentChip = Rgba(201, 184, 255, 0.12f);
    public static readonly Vector4 AccentRow = AccentWash;
    public static readonly Vector4 Glow = Rgba(143, 124, 246, 0.4f);
    public static readonly Vector4 Love = Pink;
    public static readonly Vector4 LoveFill = Hex(0xc0569e);
    public static readonly Vector4 LoveChip = PinkWash;
    public static readonly Vector4 LoveWash = PinkWash;
    public static readonly Vector4 LoveEdge = Rgba(245, 163, 220, 0.45f);
    public static readonly Vector4 DangerSolid = Hex(0xff5c82);
    public static readonly Vector4 Success = Ice;                          // (mint until the redesign)
    public static readonly Vector4 SuccessWash = IceWash;
    public static readonly Vector4 SuccessChip = IceWash;
    public static readonly Vector4 SuccessEdge = Rgba(140, 226, 255, 0.4f);
    public static readonly Vector4 LineFaint = Rgba(201, 184, 255, 0.06f); // dividers inside cards, hover fills
    public static readonly Vector4 LinePicked = Rgba(201, 184, 255, 0.1f);
    public static readonly Vector4 LineHover = Hex(0x5b5088);              // an edge pointed at
    public static readonly Vector4 LineDashed = LineStrong;                // things you can add
    public static readonly Vector4 RowHover = Rgba(201, 184, 255, 0.05f);
    public static readonly Vector4 Track = Tile;                           // a bar's track

    // ---------------------------------------------------------------------------------------------
    // Radii (CSS px, before Dalamud's global scale): the mock's corners.

    public const float RadiusTag = 6;     // tags, the seal's square (3)
    public const float RadiusSm = 9;      // icon tiles at 40, swatches
    public const float RadiusMd = 14;     // text fields, the sticker, list rows
    public const float RadiusLg = 18;     // cards, the plate slip
    public const float RadiusMenu = 16;   // menus, popups, callouts
    public const float RadiusTip = 10;    // tooltips
    public const float RadiusXl = 28;     // the field, the question's card, an empty page
    public const float Radius2Xl = 28;
    public const float RadiusCard = 18;
    public const float RadiusPill = 999;  // pills, segmented groups, switches

    /// <summary>Everything drawn is multiplied by this (0.5 for a disabled sticker or button, 0.4 for a small control).</summary>
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

    /// <summary>
    /// What was the iridescent gradient: now the field's periwinkle to ice (holo means special, and
    /// holo is a texture: <see cref="Kit.Holo"/>). Kept for the screens that still call it.
    /// </summary>
    public static Vector4 Iris(float t) => Mix(Field, Ice, System.Math.Clamp(t, 0, 1));

    public static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, c.W * Opacity));
}

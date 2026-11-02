using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GlamourStudio.Core;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>What the screens share: where a piece is, a look's row, a piece's row, dyes as swatches.</summary>
public sealed partial class MainWindow
{
    /// <summary>Every piece you have and its handiest place, made again whenever the wardrobe changes.</summary>
    private Dictionary<uint, Place> placeIndex = new();

    private (int Version, string? Key, bool Dresser, bool Armoire) placeIndexFor = (-1, null, false, false);

    /// <summary>Where a piece is: the dresser or armoire as the game has them now when it does, else the plugin's record.</summary>
    private Place? Where(uint item)
    {
        var me = wardrobe.Current;
        var stamp = (wardrobe.Version, me?.Key, wardrobe.DresserLive, wardrobe.ArmoireLive);
        if (stamp != placeIndexFor)
        {
            placeIndexFor = stamp;
            var index = new Dictionary<uint, Place>();
            if (me != null)
            {
                foreach (var place in Enumerable.Reverse(Places.ByPriority))
                    if (me.Get(place) is { } r)
                        foreach (var id in r.Items)
                            index[LookJson.BaseId(id)] = place;
                if (wardrobe.ArmoireLive)
                    foreach (var id in wardrobe.ArmoireNow.Keys)
                        index[id] = Place.Armoire;
                if (wardrobe.DresserLive)
                    foreach (var d in me.Dresser)
                        index[LookJson.BaseId(d.Item)] = Place.Dresser;
            }
            placeIndex = index;
        }
        return placeIndex.TryGetValue(LookJson.BaseId(item), out var p) ? p : null;
    }

    private Readiness ReadinessOf(Look look) => Readiness.Of(look.Pieces, Where);

    private bool Linked => sync.Linked && !sync.TokenRefused;

    /// <summary>The look's pieces' icons, four at most, in a 2×2 tile (<paramref name="size"/> square).</summary>
    private void Mosaic(Vector2 pos, float size, IReadOnlyDictionary<Slot, Piece> pieces)
    {
        var dl = Kit.Dl;
        var r = S(Palette.RadiusMd);
        dl.AddRectFilled(pos, pos + new Vector2(size), Palette.U32(Palette.Raised), r);
        var shown = Slots.All.Where(pieces.ContainsKey).Take(4).ToList();
        if (shown.Count == 0)
        {
            Icons.Draw(dl, Icon.Hanger, pos + new Vector2(size * 0.25f), size * 0.5f, Palette.Faint);
            return;
        }
        var gap = S(2);
        var cell = (size - gap * 3) / 2;
        for (var i = 0; i < shown.Count; i++)
        {
            var p = pos + new Vector2(gap + (cell + gap) * (i % 2), gap + (cell + gap) * (i / 2));
            Kit.GameIcon(p, cell, data.Item(pieces[shown[i]].Item)?.Icon ?? 0, 6);
        }
    }

    /// <summary>
    /// A look as a row: its pieces' icons, its name and a line (pieces, who made it, a note), and on
    /// the right how ready it is for a plate. True when it's pressed.
    /// </summary>
    private bool LookRow(string id, int index, Look look, string? note = null)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var nameH = Txt.Natural(Face.SansSemibold, 14);
        var tile = S(48);
        var h = S(10) * 2 + MathF.Max(tile, nameH + S(2) + S(19.5f));
        // A long list draws only the rows in view (each its icons from the game).
        if (!Dalamud.Bindings.ImGui.ImGui.IsRectVisible(at, at + new Vector2(w, h)))
        {
            Kit.Reserve(at, w, h);
            return false;
        }
        if (index > 0) dl.AddLine(new Vector2(at.X + S(10), at.Y - 0.5f), new Vector2(at.X + w - S(10), at.Y - 0.5f), Palette.U32(Palette.Line), 1f);
        var (clicked, hovered, _) = Kit.Hit(id, at, new Vector2(w, h));
        if (hovered) dl.AddRectFilled(at, at + new Vector2(w, h), Palette.U32(Palette.RowHover), S(Palette.RadiusMd));

        var x = at.X + S(10);
        Mosaic(new Vector2(x, at.Y + (h - tile) / 2), tile, look.Pieces);
        x += tile + S(12);
        var ready = ReadinessOf(look);
        var chip = ready.Short;
        var chipW = Kit.ChipWidth(chip, ready.PlateReady);
        var chevronX = at.X + w - S(10) - S(16);
        var textW = chevronX - S(10) - chipW - S(10) - x;
        var top = at.Y + (h - (nameH + S(2) + S(19.5f))) / 2;
        Txt.Line(dl, new Vector2(x, top), Txt.Fit(look.Name, Face.SansSemibold, 14, textW), Face.SansSemibold, 14, hovered ? Palette.White : Palette.Ink);
        var line = note ?? (look.Mine ? Words.Count(look.Count, "piece") : $"{Words.Count(look.Count, "piece")} · by {look.Maker}");
        Txt.Line(dl, new Vector2(x, top + nameH + S(2)), Txt.Fit(line, Face.Sans, 13, textW), Face.Sans, 13, Palette.Muted, 19.5f);
        if (textW > S(60)) Kit.Chip(new Vector2(chevronX - S(10) - chipW, at.Y + (h - S(24)) / 2), chip, ready.PlateReady);
        Icons.Draw(dl, Icon.ChevronRight, new Vector2(chevronX, at.Y + (h - S(16)) / 2), S(16), hovered ? Palette.Ink : Palette.Faint);
        Kit.Reserve(at, w, h);
        return clicked;
    }

    /// <summary>A dye as words with its swatch before it ("● Snow White"); returns the width drawn.</summary>
    private float DyeWords(Vector2 pos, byte dye, float lineCss, Vector4 color)
    {
        var dl = Kit.Dl;
        var info = data.Dye(dye);
        var lh = S(lineCss);
        Kit.Swatch(dl, new Vector2(pos.X + S(5), pos.Y + lh / 2), info?.Rgb);
        var w = Txt.Line(dl, new Vector2(pos.X + S(14), pos.Y), info?.Name ?? $"Dye {dye}", Face.Sans, 12, color, lineCss);
        return S(14) + w;
    }

    /// <summary>The words for a piece's dyes ("Snow White, Jet Black"), or null when it has none.</summary>
    private string? DyeLine(Piece p)
    {
        var names = new[] { p.Dye1, p.Dye2 }.Where(d => d != 0).Select(d => data.DyeName(d)).ToList();
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    /// <summary>A piece's status chip: where it is (green when a plate can take it), or what's to do.</summary>
    private (string Text, bool Done, string Tip) PieceChip(Place? where, bool wished) => where switch
    {
        Place.Dresser => ("Dresser", true, "In your glamour dresser: ready for a plate."),
        Place.Armoire => ("Armoire", true, "In your armoire: ready for a plate."),
        { } p => (Places.Word(p), false, $"Yours, {Places.Where(p)}. A plate takes pieces from the dresser or the armoire: put it in your glamour dresser first."),
        null => (wished ? "On your list" : "Missing", false, wished
            ? "On your shopping list on the site: the plugin hasn't seen it in your wardrobe yet."
            : "The plugin hasn't seen it in your wardrobe. Open your dresser, armoire, saddlebags and retainers once, and it knows them."),
    };

    /// <summary>
    /// The pieces of an outfit as rows (the site's order): each its icon, its name, its slot and dyes,
    /// and where it is. With a <paramref name="plan"/>, a piece whose dye won't be as the look's says so.
    /// </summary>
    private void PieceRows(string id, IReadOnlyDictionary<Slot, Piece> pieces, PlatePlan? plan = null)
    {
        Kit.BeginPanel(id, 6, 6, 6);
        var dl = Kit.Dl;
        var i = 0;
        foreach (var slot in Slots.All)
        {
            if (!pieces.TryGetValue(slot, out var p)) continue;
            var at = Kit.At;
            var w = Kit.Width;
            var item = data.Item(p.Item);
            var step = plan?.Steps.FirstOrDefault(s => s.Slot == slot);
            string? warn = null;
            if (step is { DyeLeft: true } && step.Want is { } want)
            {
                var keeps = new[] { step.Dye1 == DyeOutcome.Differs ? step.Has1 : (byte)0, step.Dye2 == DyeOutcome.Differs ? step.Has2 : (byte)0 }.Where(d => d != 0).ToList();
                var wants = new[] { step.Dye1 == DyeOutcome.Differs ? want.Dye1 : (byte)0, step.Dye2 == DyeOutcome.Differs ? want.Dye2 : (byte)0 }.Where(d => d != 0).ToList();
                warn = wants.Count > 0
                    ? $"Dye it {Words.List(wants.Select(data.DyeName))} in the plate editor" + (keeps.Count > 0 ? $" (your copy is {Words.List(keeps.Select(data.DyeName))})" : string.Empty)
                    : $"Your copy is dyed {Words.List(keeps.Select(data.DyeName))}";
            }
            else if (step is { DyeFromBags: true }) warn = "Dyed with a dye from your bags as you save";

            var nameH = Txt.Natural(Face.SansMedium, 14);
            var lines = 1 + (warn != null ? 1 : 0);
            var textH = nameH + S(2) + S(18) * lines;
            var icon = S(40);
            var h = S(9) * 2 + MathF.Max(icon, textH);
            if (i > 0) dl.AddLine(new Vector2(at.X + S(10), at.Y - 0.5f), new Vector2(at.X + w - S(10), at.Y - 0.5f), Palette.U32(Palette.Line), 1f);
            var x = at.X + S(10);
            Kit.GameIcon(new Vector2(x, at.Y + (h - icon) / 2), icon, item?.Icon ?? 0);
            x += icon + S(12);

            var where = Where(p.Item);
            var (chip, done, tip) = PieceChip(where, sync.Library.Wishes.Contains(LookJson.BaseId(p.Item)));
            var chipW = Kit.ChipWidth(chip, done);
            var textW = at.X + w - S(10) - chipW - S(10) - x;
            var top = at.Y + (h - textH) / 2;
            Txt.Line(dl, new Vector2(x, top), Txt.Fit(item?.Name ?? $"Item {p.Item}", Face.SansMedium, 14, textW), Face.SansMedium, 14, Palette.Ink);
            var ly = top + nameH + S(2);
            var lx = x + Txt.Line(dl, new Vector2(x, ly), Slots.Word(slot), Face.Sans, 12, Palette.Muted, 18);
            foreach (var d in new[] { p.Dye1, p.Dye2 }.Where(d => d != 0))
            {
                if (lx + S(60) > x + textW) break;
                lx += S(10);
                lx += DyeWords(new Vector2(lx, ly), d, 18, Palette.Muted);
            }
            if (warn != null)
                Txt.Line(dl, new Vector2(x, ly + S(18)), Txt.Fit(warn, Face.Sans, 12, textW), Face.Sans, 12, Palette.RoseText, 18);
            var chipPos = new Vector2(at.X + w - S(10) - chipW, at.Y + (h - S(24)) / 2);
            Kit.Chip(chipPos, chip, done);
            if (!Kit.Blocked && Dalamud.Bindings.ImGui.ImGui.IsWindowHovered() && Dalamud.Bindings.ImGui.ImGui.IsMouseHoveringRect(chipPos, chipPos + new Vector2(chipW, S(24))))
                Kit.Tooltip(tip);
            Kit.Reserve(at, w, h);
            i++;
        }
        if (i == 0) Txt.Note("Nothing on it.", Palette.Muted);
        Kit.EndPanel();
    }

    /// <summary>An empty state: an icon well, a heading and a line, in the middle of a dashed card (the site's <c>.gs-empty</c>).</summary>
    private static void Empty(string id, Icon icon, string head, string line)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var pad = S(22);
        var inner = w - pad * 2;
        var y = at.Y + pad;
        Kit.IconWell(new Vector2(at.X + (w - S(44)) / 2, y), S(44), icon, quiet: true);
        y += S(44) + S(12);
        var hw = Txt.Measure(head, Face.SansSemibold, 15).X;
        Txt.Draw(Kit.Dl, new Vector2(at.X + (w - MathF.Min(hw, inner)) / 2, y), Txt.Fit(head, Face.SansSemibold, 15, inner), Face.SansSemibold, 15, Palette.Ink);
        y += Txt.Natural(Face.SansSemibold, 15) + S(4);
        var lh = Txt.Flow(new Vector2(at.X + pad, y), inner, new[] { new Run(line, Face.Sans, 13, Palette.Muted) }, 19.5f, out _, center: true);
        y += lh + pad;
        Kit.Dashed(at, new Vector2(at.X + w, y), S(Palette.RadiusLg), Palette.LineDashed, 1f);
        Kit.Reserve(at, w, y - at.Y);
        _ = id;
    }
}

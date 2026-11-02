using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GlamourStudio.Core;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Make room in your dresser: how full it is (800 places), the outfit sets whose loose pieces could
/// be kept as one outfit (each set takes one place), and the pieces none of your looks, the looks you
/// love or your plates uses. It only shows and says how: the dresser's own window does the storing.
/// </summary>
public sealed partial class MainWindow
{
    private (int Wardrobe, int Sync, List<OutfitChance> Outfits, List<DresserPiece> Unused)? cleanup;

    private void OpenCleanup()
    {
        cleanup = null;
        screen = Screen.Cleanup;
        IsOpen = true;
    }

    /// <summary>The sets and the unused pieces, worked out again only when the wardrobe or your looks change.</summary>
    private (List<OutfitChance> Outfits, List<DresserPiece> Unused) CleanupNow(CharacterWardrobe me)
    {
        if (cleanup is { } c && c.Wardrobe == wardrobe.Version && c.Sync == sync.Version) return (c.Outfits, c.Unused);
        var outfits = DresserCleanup.Outfits(me.Dresser, data.AllSets);
        var used = sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Inbox.Select(s => s.Look)).Concat(sync.Plans.Select(p => p.Look))
            .Select(l => l.Pieces)
            .Concat(me.Plates.Select(p => (IReadOnlyDictionary<Slot, Piece>)p.Pieces))
            .Append(me.Outfit);
        var unused = DresserCleanup.Unused(me.Dresser, used);
        cleanup = (wardrobe.Version, sync.Version, outfits, unused);
        return (outfits, unused);
    }

    private void DrawCleanup()
    {
        Frame.BeginHeader(20);
        if (ScreenHeader("Back to your closet", "Glamour dresser", "Make room"))
        {
            screen = Screen.Home;
            homeTab = HomeTab.Closet;
        }
        Frame.EndHeader();

        Frame.BeginBody("##cleanup", null, 18, 24);
        var me = wardrobe.Current;
        if (me == null || me.Get(Place.Dresser) == null)
        {
            Txt.Note("Open your glamour dresser once (at an inn or a glamour dresser in a city) and the plugin knows what's in it.");
            Frame.EndBody();
            return;
        }
        var (outfits, unused) = CleanupNow(me);
        var usedPlaces = DresserCleanup.Used(me.Dresser);
        var frees = outfits.Sum(o => o.Frees);
        Kit.Facts("##room-facts", new List<Kit.Fact>
        {
            new("Used", usedPlaces.ToString(), $"of {DresserCleanup.Capacity}"),
            new("Free", Math.Max(0, DresserCleanup.Capacity - usedPlaces).ToString()),
            new("Outfits could free", frees.ToString()),
        });
        Kit.Gap(22);

        Kit.Section("Keep as outfits", outfits.Count > 0 ? Words.Count(outfits.Count, "set") : null);
        Kit.Gap(8);
        if (outfits.Count == 0)
            Txt.Note("No outfit set has loose pieces in your dresser: nothing to gather.", Palette.Muted);
        else
        {
            Txt.Note("An outfit set's pieces can be kept as one outfit in the dresser, in one place. At a glamour dresser, choose the set under Outfit Glamour and store it.", Palette.Muted);
            Kit.Gap(10);
            Kit.BeginPanel("##sets", 6, 6, 6);
            for (var i = 0; i < outfits.Count; i++)
            {
                var o = outfits[i];
                var total = data.SetPieces(o.SetItem).Count;
                var line = o.Stored
                    ? $"The set's in your dresser: {Words.Count(o.Pieces.Count, "loose piece")} could join it"
                    : $"{o.Pieces.Count} of {total} pieces loose in your dresser";
                ItemRow($"##set{i}", i, o.SetItem, line, o.Frees > 0 ? $"Frees {o.Frees}" : "Tidier", o.Frees > 0);
            }
            Kit.EndPanel();
        }
        Kit.Gap(22);

        Kit.Section("Not in any look or plate", unused.Count > 0 ? Words.Count(unused.Count, "piece") : null);
        Kit.Gap(8);
        if (unused.Count == 0)
            Txt.Note("Every loose piece in your dresser is in one of your looks, the looks you love, or your plates.", Palette.Muted);
        else
        {
            Txt.Note("None of your looks, the looks you love, the plans for your plates or the plates themselves uses these. Take out the ones you can do without at a glamour dresser.", Palette.Muted);
            Kit.Gap(10);
            Kit.BeginPanel("##unused", 6, 6, 6);
            for (var i = 0; i < unused.Count; i++)
            {
                var d = unused[i];
                var item = data.Item(d.Item);
                var slot = item?.Slots.Length > 0 ? Slots.Word(item.Slots[0]) : "Gear";
                ItemRow($"##unused{i}", i, d.Item, slot + (DyeLine(new Piece(d.Item, d.Dye1, d.Dye2)) is { } dyes ? $" · {dyes}" : string.Empty), null, false);
            }
            Kit.EndPanel();
        }
        Frame.EndBody();
    }

    /// <summary>An item as a row: its icon, name and a line, and a chip on the right. Only drawn in view.</summary>
    private void ItemRow(string id, int index, uint itemId, string line, string? chip, bool done)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var nameH = Txt.Natural(Face.SansMedium, 14);
        var icon = S(36);
        var h = S(9) * 2 + MathF.Max(icon, nameH + S(2) + S(18));
        if (!Dalamud.Bindings.ImGui.ImGui.IsRectVisible(at, at + new Vector2(w, h)))
        {
            Kit.Reserve(at, w, h);
            return;
        }
        var dl = Kit.Dl;
        if (index > 0) dl.AddLine(new Vector2(at.X + S(10), at.Y - 0.5f), new Vector2(at.X + w - S(10), at.Y - 0.5f), Palette.U32(Palette.Line), 1f);
        var item = data.Item(itemId);
        Kit.GameIcon(new Vector2(at.X + S(10), at.Y + (h - icon) / 2), icon, item?.Icon ?? 0);
        var x = at.X + S(10) + icon + S(12);
        var chipW = chip != null ? Kit.ChipWidth(chip, done) : 0;
        var tw = at.X + w - S(10) - (chip != null ? chipW + S(10) : 0) - x;
        var top = at.Y + (h - (nameH + S(2) + S(18))) / 2;
        Txt.Line(dl, new Vector2(x, top), Txt.Fit(item?.Name ?? $"Item {itemId}", Face.SansMedium, 14, tw), Face.SansMedium, 14, Palette.Ink);
        Txt.Line(dl, new Vector2(x, top + nameH + S(2)), Txt.Fit(line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted, 18);
        if (chip != null) Kit.Chip(new Vector2(at.X + w - S(10) - chipW, at.Y + (h - S(24)) / 2), chip, done);
        Kit.Reserve(at, w, h);
        _ = id;
    }
}

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
    /// <summary>A row as drawn: the item, its line and chip (worked out once, not every frame).</summary>
    private sealed record CleanupRow(uint Item, string Line, string? Chip, bool Done);

    private sealed record CleanupNowRead(int Used, int Frees, List<CleanupRow> Sets, List<CleanupRow> Unused);

    private (int Wardrobe, int Sync, string Key, CleanupNowRead Read)? cleanup;

    private void OpenCleanup()
    {
        cleanup = null;
        screen = Screen.Cleanup;
        IsOpen = true;
    }

    /// <summary>The sets and the unused pieces, as rows, worked out again only when the wardrobe or your looks change.</summary>
    private CleanupNowRead CleanupNow(CharacterWardrobe me)
    {
        if (cleanup is { } c && c.Wardrobe == wardrobe.Version && c.Sync == sync.Version && c.Key == me.Key) return c.Read;
        var outfits = DresserCleanup.Outfits(me.Dresser, data.AllSets);
        var plans = Enumerable.Range(1, Plates.Count).Select(sync.PlanFor).OfType<Look>();
        var used = sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Inbox.Select(s => s.Look)).Concat(plans)
            .Select(l => l.Pieces)
            .Concat(me.Plates.Select(p => (IReadOnlyDictionary<Slot, Piece>)p.Pieces))
            .Append(me.Outfit);
        var sets = outfits.Select(o =>
        {
            var line = o.Stored
                ? $"The set's in your dresser: {Words.Count(o.Pieces.Count, "loose piece")} could join it"
                : $"{o.Pieces.Count} of {data.SetPieces(o.SetItem).Count} pieces loose in your dresser";
            return new CleanupRow(o.SetItem, line, o.Frees > 0 ? $"Frees {o.Frees}" : "Tidier", o.Frees > 0);
        }).ToList();
        var unused = DresserCleanup.Unused(me.Dresser, used).Select(d =>
        {
            var item = data.Item(d.Item);
            var slot = item?.Slots.Length > 0 ? Slots.Word(item.Slots[0]) : "Gear";
            var dyes = DyeLine(new Piece(d.Item, d.Dye1, d.Dye2));
            return new CleanupRow(d.Item, dyes != null ? $"{slot} · {dyes}" : slot, null, false);
        }).ToList();
        var read = new CleanupNowRead(DresserCleanup.Used(me.Dresser), DresserCleanup.Frees(outfits), sets, unused);
        cleanup = (wardrobe.Version, sync.Version, me.Key, read);
        return read;
    }

    /// <summary>Your looks are read (so a piece in none of them is truly unused).</summary>
    private bool LooksKnown => Linked && sync.LibraryAt != null;

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
        var now = CleanupNow(me);
        var usedPlaces = now.Used;
        var frees = now.Frees;
        var outfits = now.Sets;
        var unused = now.Unused;
        Kit.Facts("##room-facts", new List<Kit.Fact>
        {
            new("Used", usedPlaces.ToString(), $"of {DresserCleanup.Capacity}"),
            new("Free", Math.Max(0, DresserCleanup.Capacity - usedPlaces).ToString()),
            new("Outfits could free", frees > 0 ? $"up to {frees}" : "0"),
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
                ItemRow(i, outfits[i]);
            Kit.EndPanel();
        }
        Kit.Gap(22);

        Kit.Section("Not in any look or plate", unused.Count > 0 && LooksKnown ? Words.Count(unused.Count, "piece") : null);
        Kit.Gap(8);
        if (!LooksKnown)
            Txt.Note(Linked
                ? "Reading your looks from the site… (the pieces they use aren't counted as unused)."
                : "Link your account: then the pieces none of your looks or plates uses show here.", Palette.Muted);
        else if (unused.Count == 0)
            Txt.Note("Every loose piece in your dresser is in one of your looks, the looks you love, or your plates.", Palette.Muted);
        else
        {
            Txt.Note("None of your looks, the looks you love, the plans for your plates or the plates themselves uses these. Take out the ones you can do without at a glamour dresser.", Palette.Muted);
            Kit.Gap(10);
            Kit.BeginPanel("##unused", 6, 6, 6);
            for (var i = 0; i < unused.Count; i++)
                ItemRow(i, unused[i]);
            Kit.EndPanel();
        }
        Frame.EndBody();
    }

    /// <summary>An item as a row: its icon, name and a line, and a chip on the right. Only drawn in view.</summary>
    private void ItemRow(int index, CleanupRow row)
    {
        var (itemId, line, chip, done) = (row.Item, row.Line, row.Chip, row.Done);
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
    }
}

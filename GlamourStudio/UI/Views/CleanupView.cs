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

    private void DrawCleanup(Vector2 min, Vector2 size)
    {
        Kit.BeginPane("##room", min, size, SideCss, 20, 20, scroll: false);
        var at = Kit.At;
        var w = Kit.Width;
        var y = at.Y;
        y += Kit.Eyebrow(new Vector2(at.X, y), "Glamour dresser", null, w) + S(4);
        y += Title(new Vector2(at.X, y), "Make room", w, 20, 1);
        Kit.Reserve(at, w, y - at.Y);
        Kit.Gap(16);
        var me = wardrobe.Current;
        if (me == null || me.Get(Place.Dresser) == null)
        {
            Txt.Note("Open your glamour dresser once (at an inn, or a dresser in a house) and the plugin knows what's in it.");
            Kit.EndPane();
            return;
        }
        var now = CleanupNow(me);
        DrawRoomFacts(now);
        Kit.Gap(12);
        DrawAutoRoom(me);
        Kit.Gap(16);
        var room = MathF.Max(S(200), Kit.PaneRoom());
        var lat = Kit.At;
        var gap = S(16);
        var cw = (w - gap) / 2;
        RoomList("##sets", new Vector2(lat.X, lat.Y), new Vector2(cw, room), "Keep as outfits", Words.Count(now.Sets.Count, "set"),
            now.Sets.Count == 0
                ? "No outfit set has loose pieces in your dresser: nothing to gather."
                : "A set's pieces kept as one outfit take one place. Store them for me (above, with the dresser open), or pick the set under Outfit Glamour yourself.",
            now.Sets, 38, 500);
        string note;
        List<CleanupRow> rows;
        if (!LooksKnown)
        {
            rows = new List<CleanupRow>();
            note = !Linked
                ? sync.TokenRefused
                    ? "The site doesn't know this token any more. Link again in Settings → Account, and the pieces none of your looks or plates uses show here."
                    : "Link your account: then the pieces none of your looks or plates uses show here."
                : sync.LibraryProblem is { } why
                    ? $"Your looks couldn't be read, so nothing's called unused yet. {why}"
                    : "Reading your looks from the site… (the pieces they use aren't counted as unused).";
        }
        else
        {
            rows = now.Unused;
            note = rows.Count == 0
                ? "Every loose piece in your dresser is in one of your looks, the looks you love, your plans or your plates."
                : "None of your looks, the ones you love, your plans or your plates uses these. Take out the ones you can do without.";
        }
        RoomList("##unused", new Vector2(lat.X + cw + gap, lat.Y), new Vector2(cw, room), "Not in any look or plate",
            LooksKnown ? Words.Count(rows.Count, "piece") : null, note, rows, 32, 400);
        Kit.Reserve(lat, w, room);
        Kit.EndPane();
    }

    /// <summary>Used of 800, free, and up to how many places gathering outfits would free: one card split in three.</summary>
    private void DrawRoomFacts(CleanupNowRead now)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(14) * 2 + Txt.Natural(Face.Mono, 10) + S(6) + Txt.Natural(Face.Display, 26);
        Kit.Card(at, at + new Vector2(w, h), Palette.Card, Palette.Line, 14);
        var dl = Kit.Dl;
        var cw = w / 3;
        var facts = new (string Label, string Value, string? Note, Vector4 Color)[]
        {
            ("Used", now.Used.ToString("N0"), $"of {DresserCleanup.Capacity}", Palette.Ink),
            ("Free", Math.Max(0, DresserCleanup.Capacity - now.Used).ToString("N0"), null, Palette.Ink),
            ("Outfits could free", now.Frees > 0 ? $"up to {now.Frees}" : "0", null, now.Frees > 0 ? Palette.Success : Palette.Ink),
        };
        for (var i = 0; i < 3; i++)
        {
            var p = new Vector2(at.X + cw * i, at.Y);
            if (i > 0) dl.AddLine(new Vector2(p.X + 0.5f, at.Y + 1), new Vector2(p.X + 0.5f, at.Y + h - 1), Palette.U32(Palette.Line), 1f);
            var (label, value, note, color) = facts[i];
            var q = p + new Vector2(S(18), S(14));
            Txt.Spaced(dl, q, label.ToUpperInvariant(), Face.Mono, 10, Palette.Dim, 0.12f);
            var vy = q.Y + Txt.Natural(Face.Mono, 10) + S(6);
            var vw = Txt.Draw(dl, new Vector2(q.X, vy), Txt.Fit(value, Face.Display, 26, cw - S(36)), Face.Display, 26, color);
            if (note != null && vw + S(6) < cw - S(36))
                Txt.Draw(dl, new Vector2(q.X + vw + S(6), vy + Txt.Ascent(Face.Display, 26) - Txt.Ascent(Face.Sans, 13)), Txt.Fit(note, Face.Sans, 13, cw - S(36) - vw - S(6)), Face.Sans, 13, Palette.Muted);
        }
        Kit.Reserve(at, w, h);
    }

    /// <summary>A card of the Make room screen: a heading and a note, then its rows, which scroll on their own.</summary>
    private void RoomList(string id, Vector2 pos, Vector2 size, string title, string? sub, string note, List<CleanupRow> rows, float iconCss, int nameWeight)
    {
        Kit.Card(pos, pos + size, Palette.Card, Palette.Line, 18);
        Kit.PushBox(pos.X + S(18), size.X - S(36));
        Dalamud.Bindings.ImGui.ImGui.SetCursorScreenPos(pos + new Vector2(S(18), S(16)));
        Kit.Heading(title, sub);
        Kit.Gap(10);
        Txt.Para(new[] { new Run(note, Face.Sans, 12, Palette.Muted) }, 18);
        Kit.Gap(10);
        var top = Kit.At;
        var listH = pos.Y + size.Y - S(16) - top.Y;
        Kit.PopBox();
        if (rows.Count == 0 || listH < S(40)) return;
        Kit.BeginPane(id, new Vector2(pos.X + S(12), top.Y), new Vector2(size.X - S(24), listH), 12, 0, 0, padRight: 6);
        for (var i = 0; i < rows.Count; i++) RoomRow(rows[i], iconCss, nameWeight);
        Kit.EndPane();
    }

    /// <summary>A row of Make room: the item's icon, name and line, and a chip on the right. Only drawn in view.</summary>
    private void RoomRow(CleanupRow row, float iconCss, int nameWeight)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var icon = S(iconCss);
        var h = S(8) * 2 + icon;
        if (!Dalamud.Bindings.ImGui.ImGui.IsRectVisible(at, at + new Vector2(w, h)))
        {
            Kit.Reserve(at, w, h);
            return;
        }
        var dl = Kit.Dl;
        var item = data.Item(row.Item);
        Kit.GameIcon(new Vector2(at.X, at.Y + S(8)), icon, item?.Icon ?? 0);
        var x = at.X + icon + S(10);
        var chipW = row.Chip != null ? Kit.ChipW(row.Chip) : 0;
        var tw = at.X + w - (row.Chip != null ? chipW + S(10) : 0) - x;
        var face = nameWeight >= 500 ? Face.SansMedium : Face.Sans;
        var nh = Txt.Natural(face, 13);
        var lh = Txt.Natural(Face.Sans, 11);
        var y = at.Y + (h - nh - lh) / 2;
        Txt.Draw(dl, new Vector2(x, y), Txt.Fit(item?.Name ?? $"Item {row.Item}", face, 13, tw), face, 13, Palette.Ink);
        Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(row.Line, Face.Sans, 11, tw), Face.Sans, 11, Palette.Dim);
        if (row.Chip != null) Kit.Chip(new Vector2(at.X + w - chipW, at.Y + (h - S(24)) / 2), row.Chip, row.Done ? ChipKind.Ok : ChipKind.Plain);
        dl.AddLine(new Vector2(at.X, at.Y + h - 0.5f), new Vector2(at.X + w, at.Y + h - 0.5f), Palette.U32(Palette.Raised), 1f);
        Kit.Reserve(at, w, h);
    }
}

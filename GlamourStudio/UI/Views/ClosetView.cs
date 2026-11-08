using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.Core;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Closet: where your gear is. The glamour dresser first, how full it is (to Make room); then each
/// other place with how many pieces and when it was read, or what to open so it can be; your closet
/// on the site; and beside it, your wishlist, each piece with where it is if you have it now.
/// </summary>
public sealed partial class MainWindow
{
    private static readonly (Place Place, Icon Icon)[] ClosetPlaces =
    {
        (Place.Armoire, Icon.Archive), (Place.Armoury, Icon.Swords), (Place.Inventory, Icon.Backpack),
        (Place.Saddlebag, Icon.Package), (Place.Retainer, Icon.Users),
    };

    private void DrawCloset(Vector2 min, Vector2 size)
    {
        var asideW = MathF.Min(S(340), size.X * 0.42f);
        var mainSize = new Vector2(size.X - asideW, size.Y);
        var me = wardrobe.Current;
        Kit.BeginPane("##closet-main", min, mainSize, SideCss, 20, 20, padRight: 20);
        if (me == null)
        {
            Kit.Heading("Where your gear is");
            Kit.Gap(12);
            Txt.Note("Log in to a character to read its wardrobe.");
            Kit.EndPane();
        }
        else
        {
            var total = PiecesKnown();
            Kit.Heading("Where your gear is", $"{Words.Count(total, "piece")}{(me.Name.Length > 0 ? " · " + me.Name : string.Empty)}");
            Kit.Gap(16);
            DrawDresserCard(me);
            Kit.Gap(16);
            DrawPlaceCards(me);
            Kit.EndPane();
        }

        var asideMin = new Vector2(min.X + mainSize.X, min.Y);
        var dl = Kit.Dl;
        dl.AddRectFilled(asideMin, asideMin + new Vector2(asideW, size.Y), Palette.U32(Palette.Rail), S(14), ImDrawFlags.RoundCornersBottomRight);
        dl.AddLine(new Vector2(asideMin.X + 0.5f, asideMin.Y), new Vector2(asideMin.X + 0.5f, asideMin.Y + size.Y), Palette.U32(Palette.Divider), 1f);
        Kit.BeginPane("##closet-aside", asideMin + new Vector2(1, 0), new Vector2(asideW - 1, size.Y), 22, 20, 20, scroll: false);
        DrawWishlist();
        Kit.EndPane();
    }

    /// <summary>The glamour dresser: read when, places used of 800 with the bar, and what gathering outfits would free. Press for Make room.</summary>
    private void DrawDresserCard(CharacterWardrobe me)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var read = me.Get(Place.Dresser);
        var pad = S(16);
        var h = pad * 2 + S(48);
        var (clicked, hovered, _) = Kit.Hit("##dresser-card", at, new Vector2(w, h), read == null);
        Kit.Card(at, at + new Vector2(w, h), Palette.Card, read == null ? Palette.LineStrong : hovered ? Palette.LineHover : Palette.Line, 18, dashed: read == null);
        var p = at + new Vector2(S(18), pad);
        Kit.Well(new Vector2(p.X, at.Y + (h - S(40)) / 2), S(40), Icon.Hanger, read != null, 18);
        var x = p.X + S(40) + S(16);
        var tw = at.X + w - S(18) - x;
        var y = p.Y;
        var nw = Txt.Draw(dl, new Vector2(x, y), "Glamour dresser", Face.SansSemibold, 14, Palette.Ink);
        if (read == null)
        {
            Txt.Draw(dl, new Vector2(x, y + Txt.Natural(Face.SansSemibold, 14) + S(6)), Txt.Fit(Places.HowToRead(Place.Dresser), Face.Sans, 12, tw), Face.Sans, 12, Palette.AccentText);
            Kit.Reserve(at, w, h);
            return;
        }
        Txt.Draw(dl, new Vector2(x + nw + S(10), y + (Txt.Natural(Face.SansSemibold, 14) - Txt.Natural(Face.Sans, 12)) / 2),
            Txt.Fit($"read {Words.Ago(new DateTimeOffset(read.ReadAt, TimeSpan.Zero), DateTimeOffset.UtcNow)}", Face.Sans, 12, MathF.Max(S(20), tw - nw - S(10))), Face.Sans, 12, Palette.Muted);
        var used = DresserUsed(me);
        var of = $" of {DresserCleanup.Capacity} places";
        var ofW = Txt.Measure(of, Face.Sans, 13).X;
        var usedW = Txt.Measure(used.ToString("N0"), Face.SansSemibold, 13).X;
        var ry = y + (Txt.Natural(Face.SansSemibold, 14) - Txt.Natural(Face.Sans, 13)) / 2;
        Txt.Draw(dl, new Vector2(x + tw - ofW - usedW, ry), used.ToString("N0"), Face.SansSemibold, 13, Palette.Ink);
        Txt.Draw(dl, new Vector2(x + tw - ofW, ry), of, Face.Sans, 13, Palette.Soft);
        y += Txt.Natural(Face.SansSemibold, 14) + S(8);
        Kit.Progress(new Vector2(x, y), tw, (float)used / DresserCleanup.Capacity);
        y += S(8) + S(8);
        var now = CleanupNow(me);
        var line = now.Sets.Count > 0
            ? $"{Words.Count(now.Sets.Count, "outfit set")} could free up to {Words.Count(now.Frees, "place")} · "
            : "No outfit set to gather · ";
        var lw = Txt.Draw(dl, new Vector2(x, y), Txt.Fit(line, Face.Sans, 12, tw - S(70)), Face.Sans, 12, Palette.Muted);
        Txt.Draw(dl, new Vector2(x + lw, y), "Make room", Face.Sans, 12, hovered ? Palette.Ink : Palette.AccentText);
        if (clicked) Later(() => Go(Screen.MakeRoom));
        Kit.Reserve(at, w, h + S(4));
    }

    /// <summary>The other places, three to a row, and the site's card last.</summary>
    private void DrawPlaceCards(CharacterWardrobe me)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var gap = S(10);
        var cols = w >= S(420) ? 3 : 2;
        var cw = (w - gap * (cols - 1)) / cols;
        var dl = Kit.Dl;
        var now = DateTimeOffset.UtcNow;
        // Every card as tall as the tallest one's words need (two lines of note at least).
        var noteW = cw - S(28);
        var tallestNote = S(18) * 2;
        foreach (var (place, _) in ClosetPlaces)
            tallestNote = MathF.Max(tallestNote, Txt.Flow(Vector2.Zero, noteW, new[] { new Run(PlaceNote(me, place, now), Face.Sans, 12, Palette.Muted) }, 18, out _, draw: false));
        var placeH = S(14) * 2 + S(34) + S(10) + Txt.Natural(Face.Display, 26) + S(10) + tallestNote;
        var siteH = S(14) * 2 + Kit.EyebrowHeight + S(10)
                    + Txt.Flow(Vector2.Zero, noteW, new[] { new Run(SiteWords(now), Face.Sans, 13, Palette.Soft) }, 19.5f, out _, draw: false)
                    + S(12) + Kit.ButtonHeight(BtnSize.Sm);
        var ch = MathF.Max(placeH, siteH);
        var i = 0;
        foreach (var (place, icon) in ClosetPlaces)
        {
            var p = new Vector2(at.X + (cw + gap) * (i % cols), at.Y + (ch + gap) * (i / cols));
            i++;
            var read = me.Get(place);
            Kit.Card(p, p + new Vector2(cw, ch), Palette.Card, read == null ? Palette.LineStrong : Palette.Line, 14, dashed: read == null);
            var q = p + new Vector2(S(14));
            Kit.Well(q, S(34), icon, read != null);
            Txt.Line(dl, new Vector2(q.X + S(34) + S(10), q.Y), Txt.Fit(Places.Word(place), Face.SansMedium, 14, cw - S(28) - S(44)), Face.SansMedium, 14, Palette.Ink, 34);
            var y = q.Y + S(34) + S(10);
            Txt.Draw(dl, new Vector2(q.X, y), read == null ? "—" : read.Items.Count.ToString("N0"), Face.Display, 26, read == null ? Palette.Dim : Palette.Ink);
            y += Txt.Natural(Face.Display, 26) + S(10);
            var note = PlaceNote(me, place, now);
            Txt.Flow(new Vector2(q.X, y), cw - S(28), new[] { new Run(note, Face.Sans, 12, read == null ? Palette.AccentText : Palette.Muted) }, 18, out _);
        }

        // The site.
        {
            var p = new Vector2(at.X + (cw + gap) * (i % cols), at.Y + (ch + gap) * (i / cols));
            Kit.Card(p, p + new Vector2(cw, ch), Palette.Blush, Palette.AccentEdge, 14);
            var q = p + new Vector2(S(14));
            var inner = cw - S(28);
            Kit.Eyebrow(q, "On the site", null, inner);
            var y = q.Y + Kit.EyebrowHeight + S(10);
            var said = SiteWords(now);
            Txt.Flow(new Vector2(q.X, y), inner, new[] { new Run(said, Face.Sans, 13, sync.SyncProblem != null && Linked ? Palette.Danger : Palette.Soft) }, 19.5f, out _);
            var by = p.Y + ch - S(14) - Kit.ButtonHeight(BtnSize.Sm);
            if (Linked)
            {
                // Both buttons at their full width: "Send now" then "Send" if both don't fit, and
                // Open left out (the closet's on the site's menu too) when even that doesn't.
                var send = sync.Syncing ? "Sending…" : "Send now";
                var ow = Kit.ButtonWidth("Open", BtnSize.Sm, null, Btn.Quiet);
                var sw = Kit.ButtonWidth(send, BtnSize.Sm, Icon.Upload);
                if (sw + S(6) + ow > inner && !sync.Syncing)
                {
                    send = "Send";
                    sw = Kit.ButtonWidth(send, BtnSize.Sm, Icon.Upload);
                }
                sw = MathF.Min(sw, inner);
                if (Kit.Button("##sync-now", new Vector2(q.X, by), send, Btn.Glass, BtnSize.Sm, Icon.Upload, sw, sync.Syncing, "Sends what the plugin has read to your closet on the site"))
                    sync.SyncNow();
                if (sw + S(6) + ow <= inner
                    && Kit.Button("##open-closet", new Vector2(q.X + sw + S(6), by), "Open", Btn.Quiet, BtnSize.Sm, null, ow, false, "Your closet on Glamour Studio"))
                    Dalamud.Utility.Util.OpenLink(SiteLink.ClosetUrl);
            }
            else if (Kit.Button("##closet-link", new Vector2(q.X, by), "Link your account", Btn.Primary, BtnSize.Sm, Icon.Link, MathF.Min(inner, Kit.ButtonWidth("Link your account", BtnSize.Sm, Icon.Link, Btn.Primary))))
                Later(() => OpenSettings(SettingsTab.Account));
            i++;
        }
        var rows = (i + cols - 1) / cols;
        Kit.Reserve(at, w, rows * ch + (rows - 1) * gap);
    }

    /// <summary>A place card's line: when it was read, or what to open so it can be.</summary>
    private static string PlaceNote(CharacterWardrobe me, Place place, DateTimeOffset now)
    {
        var read = me.Get(place);
        if (read == null) return Places.HowToRead(place);
        var ago = Words.Ago(new DateTimeOffset(read.ReadAt, TimeSpan.Zero), now);
        return place == Place.Retainer ? $"{Words.Count(me.Retainers.Count, "retainer")} read · last {ago}" : $"Read {ago}";
    }

    /// <summary>The site card's words: what was sent and when, or what's wrong.</summary>
    private string SiteWords(DateTimeOffset now) => !Linked
        ? sync.TokenRefused ? "The site doesn't know this token any more. Link again in Settings." : "Link your account and your closet on the site keeps itself up to date."
        : sync.SyncProblem ?? (sync.LastSync is { } r ? Words.Synced(r) + (sync.LastSyncAt is { } t ? $", sent {Words.Ago(t, now)}." : ".")
            : config.AutoSync ? "It's sent by itself as it changes." : "Sending by itself is off (Settings).");

    // ---------------------------------------------------------------------------------------------
    // Your wishlist.

    private (int Sync, Dictionary<uint, string> For)? wishLooks;

    /// <summary>For each piece on your list, a look of yours (or one you love, or could wear) that has it.</summary>
    private Dictionary<uint, string> WishLooks()
    {
        if (wishLooks is { } c && c.Sync == sync.Version) return c.For;
        var map = new Dictionary<uint, string>();
        foreach (var l in sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Wearable))
            foreach (var p in l.Pieces.Values)
                map.TryAdd(LookJson.BaseId(p.Item), l.Name);
        wishLooks = (sync.Version, map);
        return map;
    }

    private (IReadOnlySet<uint> Wishes, int Version, List<uint> Shown)? wishesShown;

    /// <summary>The ones you have somewhere first (they're the news), then by name.</summary>
    private List<uint> WishesShown(IReadOnlySet<uint> wishes)
    {
        if (wishesShown is { } c && ReferenceEquals(c.Wishes, wishes) && c.Version == wardrobe.Version) return c.Shown;
        var shown = wishes.OrderBy(id => Where(id) == null).ThenBy(id => data.ItemName(id), StringComparer.OrdinalIgnoreCase).ToList();
        wishesShown = (wishes, wardrobe.Version, shown);
        return shown;
    }

    private void DrawWishlist()
    {
        var wishes = sync.Library.Wishes;
        Kit.Heading("Your wishlist", Linked ? Words.Count(wishes.Count, "piece") : null);
        Kit.Gap(12);
        if (!Linked)
        {
            Txt.Para(new[] { new Run("Link your account: the pieces on your wishlist on the site show here, each with where it is once you have it.", Face.Sans, 13, Palette.Muted) }, 19.5f);
            return;
        }
        var w = Kit.Width;
        var dl = Kit.Dl;
        if (sync.LastWishesGot is { } got && DateTime.UtcNow - got.At < TimeSpan.FromMinutes(30) && got.Items.Count > 0)
        {
            Kit.BeginPanel("##wishes-got", 12, 14, 12, Palette.WithAlpha(Palette.Success, 0.06f), Palette.SuccessEdge, 14);
            var a = Kit.At;
            var iw = Kit.Width;
            Icons.Draw(dl, Icon.Check, new Vector2(a.X, a.Y + S(2)), S(16), Palette.Success, 2.2f);
            var names = Words.List(got.Items.Take(3).Select(data.ItemName)) + (got.Items.Count > 3 ? $" and {got.Items.Count - 3} more" : string.Empty);
            var h = Txt.Flow(new Vector2(a.X + S(26), a.Y), iw - S(26), new[]
            {
                new Run($"{Words.Count(got.Items.Count, "piece")} came off it", Face.SansSemibold, 13, Palette.Ink),
                new Run($" {Words.Ago(new DateTimeOffset(got.At, TimeSpan.Zero), DateTimeOffset.UtcNow)}: {names}, seen in your wardrobe.", Face.Sans, 13, Palette.Soft),
            }, 19.5f, out _);
            Kit.Reserve(a, iw, h);
            Kit.EndPanel();
            Kit.Gap(12);
        }
        if (wishes.Count == 0)
        {
            Txt.Para(new[] { new Run("Nothing on it. On the site, a look's pieces you don't have yet can go on your list.", Face.Sans, 13, Palette.Muted) }, 19.5f);
            return;
        }
        var footH = Txt.Natural(Face.Sans, 12) * 2 + S(12);
        var listH = MathF.Max(S(80), Kit.PaneRoom() - footH);
        var at = Kit.At;
        Kit.BeginPane("##wishes", at, new Vector2(w, listH), 0, 0, 0);
        var forLook = WishLooks();
        foreach (var id in WishesShown(wishes))
        {
            var r = Kit.At;
            var rw = Kit.Width;
            var h = S(7) * 2 + S(32);
            if (!ImGui.IsRectVisible(r, r + new Vector2(rw, h)))
            {
                // (As tall as a drawn row and its gap, so nothing moves as rows scroll in and out.)
                Kit.Reserve(r, rw, h);
                Kit.Gap(2);
                continue;
            }
            var item = data.Item(id);
            dl = Kit.Dl;
            Kit.GameIcon(new Vector2(r.X, r.Y + S(7)), S(32), item?.Icon ?? 0);
            var where = Where(id);
            var chip = where switch
            {
                Place.Dresser => "In your dresser",
                Place.Armoire => "In your armoire",
                Place.Armoury => "In your armoury",
                Place.Inventory => "In your bags",
                Place.Saddlebag => "In your saddlebags",
                Place.Retainer => "With a retainer",
                _ => "Not yet",
            };
            var cw = Kit.ChipW(chip);
            var xw = S(28);
            var x = r.X + S(32) + S(10);
            var tw = r.X + rw - xw - S(8) - cw - S(10) - x;
            var nh = Txt.Natural(Face.Sans, 13);
            var lh = Txt.Natural(Face.Sans, 11);
            var y = r.Y + (h - nh - lh) / 2;
            Txt.Draw(dl, new Vector2(x, y), Txt.Fit(item?.Name ?? $"Item {id}", Face.Sans, 13, tw), Face.Sans, 13, Palette.Ink);
            Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(forLook.TryGetValue(id, out var ln) ? $"For {ln}" : "On your wishlist", Face.Sans, 11, tw), Face.Sans, 11, Palette.Dim);
            Kit.Chip(new Vector2(r.X + rw - xw - S(8) - cw, r.Y + (h - S(24)) / 2), chip, where != null ? ChipKind.Ok : ChipKind.Plain);
            var gone = id;
            if (Kit.IconButton($"##unwish-{id}", new Vector2(r.X + rw - xw, r.Y + (h - xw) / 2), Icon.X, "Take it off the list", 28, Btn.Quiet, false, 14))
                Later(() => Unwish(gone));
            Kit.Dl.AddLine(new Vector2(r.X, r.Y + h - 0.5f), new Vector2(r.X + rw, r.Y + h - 0.5f), Palette.U32(Palette.Raised), 1f);
            Kit.Reserve(r, rw, h);
            Kit.Gap(2);
        }
        Kit.EndPane();
        Kit.Reserve(at, w, listH);
        Kit.Gap(12);
        Txt.Para(new[] { new Run("A piece the plugin sees in your wardrobe comes off the list by itself; × takes one off.", Face.Sans, 12, Palette.Muted) }, 18);
    }
}

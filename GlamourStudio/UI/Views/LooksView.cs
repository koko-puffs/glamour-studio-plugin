using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Looks: your looks, the ones you love and the public ones you could wear now, as a list (tabs with
/// counts, a search, Ready for a plate first, a job and a mood), and the look picked beside it: its
/// pieces and where each is, how to put it on a plate (Put on plate N with the plate editor open),
/// Try on, a shot, the site, and Open the look.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>The tab: 0 your looks, 1 the ones you love, 2 the ones you could wear now.</summary>
    private int looksTab;

    private string search = string.Empty;
    private bool readyFirst = true;
    private string? jobFilter;
    private string? moodFilter;

    /// <summary>The look picked in the list (its id), or null for the first.</summary>
    private string? pickedLook;

    private (int Tab, string Search, bool Ready, string? Job, string? Mood, int Sync, int Wardrobe, List<Look> Looks)? shownLooks;

    private IReadOnlyList<Look> TabLooks(int tab) => tab switch
    {
        1 => sync.Library.Loved,
        2 => sync.Wearable,
        _ => sync.Library.Mine,
    };

    /// <summary>The looks shown, filtered and sorted, worked out again only when something they're made from changes.</summary>
    private List<Look> ShownLooks()
    {
        var q = search.Trim();
        if (shownLooks is { } c && c.Tab == looksTab && c.Search == q && c.Ready == readyFirst && c.Job == jobFilter && c.Mood == moodFilter
            && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version)
            return c.Looks;
        IEnumerable<Look> all = TabLooks(looksTab);
        if (q.Length > 0)
            all = all.Where(l => l.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || l.Maker.Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || (l.Job?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                                 || (l.Mood?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        if (jobFilter != null) all = all.Where(l => string.Equals(l.Job, jobFilter, StringComparison.OrdinalIgnoreCase));
        if (moodFilter != null) all = all.Where(l => string.Equals(l.Mood, moodFilter, StringComparison.OrdinalIgnoreCase));
        var list = all.ToList();
        // Ready for a plate first, as they were among themselves (a stable sort).
        if (readyFirst) list = list.OrderByDescending(l => ReadinessOf(l).PlateReady).ToList();
        shownLooks = (looksTab, q, readyFirst, jobFilter, moodFilter, sync.Version, wardrobe.Version, list);
        return list;
    }

    private static readonly string[] LooksHints = { "Pick a look to see it here", "Public looks you love", "Public looks you own every piece of" };

    private void DrawLooks(Vector2 min, Vector2 size)
    {
        var asideW = MathF.Min(S(372), size.X * 0.45f);
        var listSize = new Vector2(size.X - asideW, size.Y);
        Kit.BeginPane("##looks-list", min, listSize, SideCss, 20, 20, scroll: false, padRight: 20);
        var looks = Linked ? ShownLooks() : new List<Look>();
        DrawLooksTop();
        Kit.Gap(14);
        DrawLooksFilters();
        Kit.Gap(14);

        Look? picked = null;
        if (!Linked)
            Empty("##looks-link", Icon.Hanger, "Your looks",
                sync.TokenRefused ? "The site doesn't know this token any more. Link again in Settings → Account." : "Link your account (Settings → Account) and your looks from Glamour Studio are here, ready to put on a plate.");
        else if (sync.LibraryAt == null && sync.LibraryBusy && looksTab != 2)
            Txt.Note("Reading your looks…");
        else if (TabLooks(looksTab).Count == 0)
        {
            var (head, line) = looksTab switch
            {
                1 => ("No loved looks yet", "Love a look on Glamour Studio's Discover page and it's here."),
                2 => ("None yet", "No public look is all yours yet. As your closet grows (open your dresser, armoire and retainers once), they show here."),
                _ => ("No looks yet", "Dress the mannequin in Glamour Studio's dressing room and save a look: it's here."),
            };
            Empty("##looks-none", looksTab == 1 ? Icon.Heart : Icon.Sparkles, head, line);
            if (sync.LibraryProblem is { } problem)
            {
                Kit.Gap(10);
                Txt.Note(problem, Palette.Danger);
            }
        }
        else if (looks.Count == 0)
            Txt.Note(search.Trim().Length > 0 ? $"No look matches “{search.Trim()}”." : "No look matches those filters.");
        else
        {
            picked = looks.FirstOrDefault(l => l.Id == pickedLook) ?? looks[0];
            var room = MathF.Max(S(120), Kit.PaneRoom());
            Kit.BeginScrollCard("##looks-rows", room, 6);
            for (var i = 0; i < looks.Count; i++)
            {
                var look = looks[i];
                if (LooksRow(look, ReferenceEquals(look, picked))) pickedLook = look.Id;
                Kit.Gap(2);
            }
            Kit.EndScrollCard();
        }
        Kit.EndPane();

        // The look picked, beside the list.
        var asideMin = new Vector2(min.X + listSize.X, min.Y);
        var dl = Kit.Dl;
        dl.AddRectFilled(asideMin, asideMin + new Vector2(asideW, size.Y), Palette.U32(Palette.Rail), S(14), ImDrawFlags.RoundCornersBottomRight);
        dl.AddLine(new Vector2(asideMin.X + 0.5f, asideMin.Y), new Vector2(asideMin.X + 0.5f, asideMin.Y + size.Y), Palette.U32(Palette.Divider), 1f);
        Kit.BeginPane("##looks-aside", asideMin + new Vector2(1, 0), new Vector2(asideW - 1, size.Y), 22, 20, 20);
        if (picked != null) DrawLookAside(picked);
        else Txt.Note(Linked ? "Pick a look to see it here." : "Your looks show here once you're linked.", Palette.Muted);
        Kit.EndPane();
    }

    private void DrawLooksTop()
    {
        var at = Kit.At;
        var w = Kit.Width;
        var tabs = new[]
        {
            new TabSpec("Your looks", Linked && sync.LibraryAt != null ? sync.Library.Mine.Count.ToString() : null),
            new TabSpec("Loved", Linked && sync.LibraryAt != null ? sync.Library.Loved.Count.ToString() : null),
            new TabSpec("Wear now", Linked ? sync.Wearable.Count.ToString() : null),
        };
        var segW = Kit.SegWidth(tabs);
        var picked = Kit.Seg("##looks-tab", at, tabs, looksTab);
        if (picked != looksTab)
        {
            looksTab = picked;
            pickedLook = null;
            // A job or mood of one tab's looks may be none of the next's.
            jobFilter = moodFilter = null;
        }
        var fieldW = w - segW - S(12);
        if (fieldW >= S(160))
        {
            var text = search;
            if (Kit.Field("##search", new Vector2(at.X + segW + S(12), at.Y + (Kit.SegHeight - S(36)) / 2), fieldW, ref text, "Name, maker, job or mood", Icon.Search, 60))
                search = text;
            Kit.Reserve(at, w, Kit.SegHeight);
        }
        else
        {
            Kit.Reserve(at, w, Kit.SegHeight);
            Kit.Gap(10);
            var fat = Kit.At;
            var text = search;
            if (Kit.Field("##search", fat, w, ref text, "Name, maker, job or mood", Icon.Search, 60)) search = text;
            Kit.Reserve(fat, w, S(36));
        }
    }

    private void DrawLooksFilters()
    {
        var at = Kit.At;
        var w = Kit.Width;
        var x = at.X;
        // Ready for a plate first: lilac with a tick when on.
        const string ready = "Ready for a plate first";
        var rw = Kit.ChipW(ready, readyFirst);
        var (rc, rh, _) = Kit.Hit("##ready-first", at, new Vector2(rw, S(24)));
        Kit.Chip(at, ready, readyFirst ? ChipKind.Accent : ChipKind.Plain, readyFirst);
        if (rh) Kit.Tooltip(readyFirst ? "The looks ready for a plate first (press for the site's order)" : "Press to put the looks ready for a plate first");
        if (rc) readyFirst = !readyFirst;
        x += rw + S(8);

        // A job and a mood, each from the looks in this tab.
        var (jobs, moods) = FilterChoices();
        x += FilterChip("##job", new Vector2(x, at.Y), jobFilter ?? "Any job", jobFilter != null, jobs, "Any job", v => jobFilter = v) + S(8);
        x += FilterChip("##mood", new Vector2(x, at.Y), moodFilter ?? "Any mood", moodFilter != null, moods, "Any mood", v => moodFilter = v) + S(8);

        var hint = LooksHints[Math.Clamp(looksTab, 0, 2)];
        var hw = Txt.Measure(hint, Face.Sans, 12).X;
        if (at.X + w - hw > x + S(8))
            Txt.Line(Kit.Dl, new Vector2(at.X + w - hw, at.Y), hint, Face.Sans, 12, Palette.Muted, 24);
        Kit.Reserve(at, w, S(24));
    }

    private (int Tab, int Sync, List<string> Jobs, List<string> Moods)? filterChoices;

    /// <summary>The jobs and moods the tab's looks say, worked out again only when they change.</summary>
    private (List<string> Jobs, List<string> Moods) FilterChoices()
    {
        if (filterChoices is { } c && c.Tab == looksTab && c.Sync == sync.Version) return (c.Jobs, c.Moods);
        var looks = TabLooks(looksTab);
        var jobs = looks.Select(l => l.Job).OfType<string>().Where(j => j.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(j => j).ToList();
        var moods = looks.Select(l => l.Mood).OfType<string>().Where(m => m.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(m => m).ToList();
        filterChoices = (looksTab, sync.Version, jobs, moods);
        return (jobs, moods);
    }

    /// <summary>A chip that opens a menu of choices (and the "any" one first); returns its width.</summary>
    private float FilterChip(string id, Vector2 pos, string text, bool on, IReadOnlyList<string> choices, string any, Action<string?> pick)
    {
        var w = Kit.ChipW(text);
        var none = choices.Count == 0 && !on;
        var (clicked, _, _) = Kit.Hit(id, pos, new Vector2(w, S(24)), none);
        var old = Palette.Opacity;
        if (none) Palette.Opacity = old * 0.5f;
        Kit.Chip(pos, text, on ? ChipKind.Accent : ChipKind.Plain);
        Palette.Opacity = old;
        if (none && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + new Vector2(w, S(24))))
            Kit.Tooltip("None of these looks says one");
        if (clicked) ImGui.OpenPopup(id + "-menu");
        if (Kit.BeginMenu(id + "-menu"))
        {
            if (Kit.MenuItem(id + "-any", any))
            {
                pick(null);
                ImGui.CloseCurrentPopup();
            }
            foreach (var c in choices)
                if (Kit.MenuItem(id + "-" + c, c))
                {
                    pick(c);
                    ImGui.CloseCurrentPopup();
                }
            Kit.EndMenu();
        }
        return w;
    }

    /// <summary>A look's row in the list: its pieces' icons, name, a line and how ready it is; the picked one in lilac. True when pressed.</summary>
    private bool LooksRow(Look look, bool on)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(10) * 2 + S(48);
        if (!ImGui.IsRectVisible(at, at + new Vector2(w, h)))
        {
            Kit.Reserve(at, w, h);
            return false;
        }
        // By the look's id, not its place: a list read again mid-click doesn't press another look.
        var (clicked, hovered, _) = Kit.Hit("##lr" + look.Id, at, new Vector2(w, h));
        var dl = Kit.Dl;
        if (on)
        {
            dl.AddRectFilled(at, at + new Vector2(w, h), Palette.U32(Palette.AccentRow), S(12));
            dl.AddRect(at + new Vector2(0.5f), at + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(Palette.AccentEdge), S(12), ImDrawFlags.None, 1f);
        }
        else if (hovered) dl.AddRectFilled(at, at + new Vector2(w, h), Palette.U32(Palette.RowHover), S(12));
        Mosaic(new Vector2(at.X + S(10), at.Y + S(10)), S(48), look.Pieces);
        var x = at.X + S(10) + S(48) + S(12);
        var ready = ReadinessOf(look);
        var chip = ready.PlateReady ? "Ready for a plate" : ready.Short;
        var chipW = Kit.ChipW(chip);
        var tw = at.X + w - S(10) - chipW - S(12) - x;
        var nameH = Txt.Natural(Face.SansSemibold, 14);
        var lineH = Txt.Natural(Face.Sans, 12);
        var y = at.Y + (h - nameH - S(3) - lineH) / 2;
        Txt.Draw(dl, new Vector2(x, y), Txt.Fit(look.Name, Face.SansSemibold, 14, tw), Face.SansSemibold, 14, Palette.Ink);
        Txt.Draw(dl, new Vector2(x, y + nameH + S(3)), Txt.Fit(LookLine(look), Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted);
        if (tw > S(60)) Kit.Chip(new Vector2(at.X + w - S(10) - chipW, at.Y + (h - S(24)) / 2), chip, ready.PlateReady ? ChipKind.Ok : ChipKind.Plain);
        Kit.Reserve(at, w, h);
        return clicked;
    }

    /// <summary>A look's line in a list: whose it is and how big.</summary>
    private string LookLine(Look look) => looksTab == 2
        ? $"Public · by {look.Maker} · {Words.Count(look.Loves, "love")}"
        : look.Mine ? $"Your look · {Words.Count(look.Count, "piece")}"
        : $"Loved · by {look.Maker}";

    /// <summary>The eyebrow over a look's name: whose it is (or that it was sent, or that you own it all).</summary>
    private string LookEyebrow(Look look)
    {
        if (openSent != null && openSent.Look.Id == look.Id)
            return "Sent from the site" + (openSent.Plate is { } sp ? $" · for plate {sp}" : string.Empty);
        var plannedFor = PlannedPlateOf(look.Id);
        var who = look.Mine ? "Your look" : sync.Library.Loved.Any(l => l.Id == look.Id) ? $"Loved · by {look.Maker}" : $"Wear now · by {look.Maker}";
        return plannedFor > 0 ? $"{who} · planned for plate {plannedFor}" : who;
    }

    private (int Sync, string? Key, Dictionary<string, int> Plates)? plannedPlates;

    /// <summary>The plate a look's planned for on the site for this character (0: none), worked out once per change.</summary>
    private int PlannedPlateOf(string lookId)
    {
        var key = wardrobe.Current?.Key;
        if (plannedPlates is not { } c || c.Sync != sync.Version || c.Key != key)
        {
            var map = new Dictionary<string, int>();
            for (var p = Plates.Count; p >= 1; p--)
                if (sync.PlanFor(p) is { } l) map[l.Id] = p;
            plannedPlates = c = (sync.Version, key, map);
        }
        return c.Plates.TryGetValue(lookId, out var n) ? n : 0;
    }

    // ---------------------------------------------------------------------------------------------
    // The look picked, beside the list.

    private void DrawLookAside(Look look)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        Mosaic(at, S(72), look.Pieces, 14);
        var tx = at.X + S(72) + S(14);
        var tw = w - (tx - at.X);
        var y = at.Y;
        var eyebrow = looksTab switch
        {
            2 => "Wear now · you own it all",
            1 => $"Loved · by {look.Maker}",
            _ => look.Mine ? "Your look" : $"Loved · by {look.Maker}",
        };
        y += Kit.Eyebrow(new Vector2(tx, y), eyebrow, null, tw) + S(5);
        y += Title(new Vector2(tx, y), look.Name, tw, 19, 2) + S(5);
        var chips = new[] { look.Mood, look.Job }.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();
        var cx = tx;
        foreach (var c in chips)
        {
            var cw = Kit.ChipW(c);
            if (cx + cw > at.X + w) break;
            Kit.Chip(new Vector2(cx, y), c, ChipKind.Plain);
            cx += cw + S(6);
        }
        if (chips.Count > 0) y += S(24);
        Kit.Reserve(at, w, MathF.Max(S(72), y - at.Y));
        Kit.Gap(16);

        DrawPlateCard(look, compact: true);
        Kit.Gap(16);

        // Try on, a shot, the site.
        var row = Kit.At;
        var bw = (w - S(16)) / 3;
        if (Kit.Button("##aside-try", row, "Try on", Btn.Glass, BtnSize.Sm, Icon.Eye, bw, false, "Opens the game's fitting room with the look on you"))
            Later(() => TryOn(look.Pieces));
        var isPublic = look.Visibility == "public";
        if (Kit.Button("##aside-shot", new Vector2(row.X + bw + S(8), row.Y), "Shot", Btn.Glass, BtnSize.Sm, Icon.Camera, bw, !isPublic || !Linked,
                isPublic ? "Opens the look, to take a shot for its Worn in game on the site" : "Only a public look takes shots on the site"))
            Later(() => OpenLook(look, null, Screen.Looks));
        if (Kit.Button("##aside-site", new Vector2(row.X + (bw + S(8)) * 2, row.Y), "Site", Btn.Quiet, BtnSize.Sm, Icon.ExternalLink, bw, false, "The look's page on Glamour Studio"))
            Dalamud.Utility.Util.OpenLink(SiteLink.LookUrl(look.Id));
        Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Sm));
        Kit.Gap(16);

        var ready = ReadinessOf(look);
        Kit.Heading("Pieces", ReadyWords(ready), 14);
        Kit.Gap(16);
        var openH = Kit.ButtonHeight(BtnSize.Md);
        // The list takes what's left above Open the look (the pane scrolls when there's too little).
        var listH = MathF.Max(S(80), Kit.PaneRoom() - openH - S(16));
        var lat = Kit.At;
        Kit.BeginPane("##aside-pieces", lat, new Vector2(w, listH), 0, 0, 0);
        foreach (var slot in Slots.All)
        {
            if (!look.Pieces.TryGetValue(slot, out var p)) continue;
            CompactPieceRow(slot, p);
            Kit.Gap(2);
        }
        Kit.EndPane();
        Kit.Reserve(lat, w, listH);
        Kit.Gap(16);
        var bat = Kit.At;
        const string open = "Open the look";
        // The words then the chevron, centred together (a padded label leaves room for the icon after).
        var textW = Txt.Measure(open, Face.SansMedium, 14).X;
        var group = textW + S(8) + S(16);
        if (Kit.Button("##aside-open", bat, string.Empty, Btn.Glass, BtnSize.Md, null, w, false, null, openH))
            Later(() => OpenLook(look, null, Screen.Looks));
        var gx = bat.X + (w - group) / 2;
        Txt.Line(dl, new Vector2(gx, bat.Y), open, Face.SansMedium, 14, Palette.Ink, openH / S(1));
        Icons.Draw(dl, Icon.ChevronRight, new Vector2(gx + textW + S(8), bat.Y + (openH - S(16)) / 2), S(16), Palette.Ink);
        Kit.Reserve(bat, w, openH);
    }

    /// <summary>How ready a look is, in the small capitals of the pieces' heading.</summary>
    private static string ReadyWords(Readiness r)
    {
        if (r.Total == 0) return "Nothing on it";
        if (r.PlateReady) return $"{r.Total} of {r.Total} in your dresser or armoire";
        var bits = new List<string> { $"{r.Ready} of {r.Total} ready" };
        if (r.ToStore > 0) bits.Add($"{r.ToStore} to store");
        if (r.Missing > 0) bits.Add($"{r.Missing} missing");
        return string.Join(" · ", bits);
    }

    /// <summary>A piece in a short list: its icon (30), name, slot and dyes, and where it is.</summary>
    private void CompactPieceRow(Slot slot, Piece p)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(5) * 2 + S(30);
        var dl = Kit.Dl;
        var item = data.Item(p.Item);
        Kit.GameIcon(new Vector2(at.X, at.Y + S(5)), S(30), item?.Icon ?? 0);
        var where = Where(p.Item);
        var (chip, kind, tip) = WhereChip(where, sync.Library.Wishes.Contains(LookJson.BaseId(p.Item)));
        var cw = Kit.ChipW(chip);
        var x = at.X + S(30) + S(10);
        var tw = at.X + w - cw - S(10) - x;
        var nh = Txt.Natural(Face.Sans, 13);
        var lh = Txt.Natural(Face.Sans, 11);
        var y = at.Y + (h - nh - lh) / 2;
        Txt.Draw(dl, new Vector2(x, y), Txt.Fit(item?.Name ?? $"Item {p.Item}", Face.Sans, 13, tw), Face.Sans, 13, Palette.Ink);
        var line = Slots.Word(slot) + (DyeLine(p) is { } d ? $" · {d}" : string.Empty);
        Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(line, Face.Sans, 11, tw), Face.Sans, 11, Palette.Dim);
        var cp = new Vector2(at.X + w - cw, at.Y + (h - S(24)) / 2);
        Kit.Chip(cp, chip, kind);
        if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(cp, cp + new Vector2(cw, S(24)))) Kit.Tooltip(tip);
        Kit.Reserve(at, w, h);
    }

    /// <summary>Where a piece is, as a chip: mint where a plate takes it from, lilac where it's to put away, orchid when it isn't yours yet (as the site marks the pieces you don't own).</summary>
    private static (string Text, ChipKind Kind, string Tip) WhereChip(Place? where, bool wished) => where switch
    {
        Place.Dresser => ("Dresser", ChipKind.Ok, "In your glamour dresser: ready for a plate."),
        Place.Armoire => ("Armoire", ChipKind.Ok, "In your armoire: ready for a plate."),
        { } p => (Places.Word(p), ChipKind.Accent, $"Yours, {Places.Where(p)}. A plate takes pieces from the dresser or the armoire: put it away first."),
        null => (wished ? "On your wishlist" : "Missing", ChipKind.Love, wished
            ? "On your wishlist on the site: the plugin hasn't seen it in your wardrobe yet."
            : "The plugin hasn't seen it in your wardrobe. Open your dresser, armoire, saddlebags and retainers once, and it knows them."),
    };
}

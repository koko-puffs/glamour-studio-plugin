using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.Core;
using GlamourStudio.Game;
using GlamourStudio.UI.Model;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Looks (docs/redesign/pages/looks.html): a list down the left (Your looks · Loved · Wear today
/// with their counts, a search, Ready first, a job and a mood; each row four of its pieces, its
/// name in display capitals, whose it is and how ready in the honest colours) and the look picked
/// on the right as a small character sheet: its name on the field with its echo, the maker's seal
/// and its dyes as a palette strip; Try it on (the sticker when no plate is open), Shot, Site and
/// Open the look; the plate line (closed) or the plate slip with Put on plate N (Glamour Plates
/// open); its pieces and where each is.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>The tab: 0 your looks, 1 the ones you love, 2 the ones you could wear today.</summary>
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
        // Ready for a plate first, as they were among themselves (a stable sort). Wear today's are all yours.
        if (readyFirst && looksTab != 2) list = list.OrderByDescending(l => ReadinessOf(l).PlateReady).ToList();
        shownLooks = (looksTab, q, readyFirst, jobFilter, moodFilter, sync.Version, wardrobe.Version, list);
        looksRows.Clear();
        return list;
    }

    // ---------------------------------------------------------------------------------------------
    // The rows' words, made once per look while the list stays as it is.

    private sealed class LooksRowInfo
    {
        public string Line = string.Empty;
        public string Short = string.Empty;
        public (string Text, TagKind Kind, Icon? Icon)[] Tags = Array.Empty<(string, TagKind, Icon?)>();
        public (string Text, TagKind Kind, Icon? Icon) ShortTag;
        public string? Loves;
        public uint[] Icons = Array.Empty<uint>();
    }

    private readonly Dictionary<string, LooksRowInfo> looksRows = new();

    private (int Sync, Dictionary<string, int> Plates)? sentPlates;

    /// <summary>The plate a look was sent from the site for (0: none, or not sent), by its id.</summary>
    private int SentPlateOf(string lookId)
    {
        if (sentPlates is not { } c || c.Sync != sync.Version)
        {
            var map = new Dictionary<string, int>();
            foreach (var s in sync.Inbox)
                if (s.Plate is { } p && !map.ContainsKey(s.Look.Id)) map[s.Look.Id] = p;
            sentPlates = c = (sync.Version, map);
        }
        return c.Plates.TryGetValue(lookId, out var n) ? n : 0;
    }

    private LooksRowInfo RowInfo(Look look)
    {
        if (looksRows.TryGetValue(look.Id, out var info)) return info;
        info = new LooksRowInfo
        {
            Icons = Slots.All.Where(look.Pieces.ContainsKey).Take(4).Select(s => data.Item(look.Pieces[s].Item)?.Icon ?? 0).ToArray(),
        };
        if (looksTab == 2)
        {
            info.Line = $"{look.Maker} · {(look.Job is { Length: > 0 } j ? j : look.Mood is { Length: > 0 } m ? m : "any job")}";
            info.Short = look.Maker;
            info.Loves = look.Loves.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            var who = look.Mine ? "Your look" : $"Loved · by {look.Maker}";
            var planned = PlannedPlateOf(look.Id);
            var sent = SentPlateOf(look.Id);
            var what = planned > 0 ? $"planned for plate {planned}"
                : sent > 0 ? $"sent for plate {sent}"
                : string.Join(" · ", new[] { look.Job, look.Mood }.Where(s => !string.IsNullOrWhiteSpace(s)));
            info.Line = what.Length > 0 ? $"{who} · {what}" : who;
            info.Short = look.Mine ? "Your look" : $"Loved · {look.Maker}";
            var r = ReadinessOf(look);
            if (r.Total == 0) info.Tags = new[] { ("Nothing on it", TagKind.Plain, (Icon?)null) };
            else if (r.PlateReady) info.Tags = new[] { ("Ready for a plate", TagKind.Ok, (Icon?)Icon.Check) };
            else if (r.Missing == 0) info.Tags = new[] { ($"{r.ToStore} to store", TagKind.Amber, (Icon?)null) };
            else if (r.Owned == 0) info.Tags = new[] { ("None yours yet", TagKind.Miss, (Icon?)null) };
            else info.Tags = new[] { ($"{r.Owned} of {r.Total} yours", TagKind.Plain, (Icon?)null), ($"{r.Missing} missing", TagKind.Miss, (Icon?)null) };
            info.ShortTag = r.PlateReady ? ("Ready", TagKind.Ok, Icon.Check) : info.Tags[^1];
        }
        looksRows[look.Id] = info;
        return info;
    }

    // ---------------------------------------------------------------------------------------------
    // The screen.

    private void DrawLooks(Vector2 min, Vector2 size)
    {
        // Under 1000 wide the list takes 420 and the words shorten (extras.html, the window at its least).
        var narrow = size.X < S(1000);
        var listW = MathF.Min(S(narrow ? 420 : 548), size.X * 0.55f);
        var looks = Linked ? ShownLooks() : null;
        Look? picked = null;
        if (looks is { Count: > 0 }) picked = looks.FirstOrDefault(l => l.Id == pickedLook) ?? looks[0];

        Kit.BeginPane("##looks-list", min, new Vector2(listW, size.Y), SideCss, 14, 0, scroll: false, padRight: 20);
        DrawLooksTabs();
        if (Linked) DrawLooksFilters(narrow);
        DrawLooksBody(looks, picked, narrow, new Vector2(min.X + listW, min.Y + size.Y));
        Kit.EndPane();

        // The look picked, beside the list, on the side pane's ground.
        var asideMin = new Vector2(min.X + listW, min.Y);
        var asideW = size.X - listW;
        var dl = Kit.Dl;
        dl.AddRectFilled(asideMin, asideMin + new Vector2(asideW, size.Y), Palette.U32(Palette.Ground2), S(14), ImDrawFlags.RoundCornersBottomRight);
        dl.AddLine(new Vector2(asideMin.X + 0.5f, asideMin.Y), new Vector2(asideMin.X + 0.5f, asideMin.Y + size.Y), Palette.U32(Palette.Line), 1f);
        Kit.BeginPane("##looks-aside", asideMin + new Vector2(1, 0), new Vector2(asideW - 1, size.Y), narrow ? 18 : 22, 18, 18);
        if (picked != null) DrawLookPick(picked, narrow);
        else
        {
            var at = Kit.At;
            Kit.Reserve(at, Kit.Width, Kit.Hand(at, Linked ? "pick a look to see it here" : "your looks show here once you're linked", 15, Kit.Width));
        }
        Kit.EndPane();
    }

    private (int Sync, bool Linked, bool Read, TabSpec[] Tabs)? looksTabs;

    private void DrawLooksTabs()
    {
        var read = Linked && sync.LibraryAt != null;
        if (looksTabs is not { } c || c.Sync != sync.Version || c.Linked != Linked || c.Read != read)
        {
            var en = System.Globalization.CultureInfo.InvariantCulture;
            var tabs = new[]
            {
                new TabSpec("Your looks", read ? sync.Library.Mine.Count.ToString(en) : null),
                new TabSpec("Loved", read ? sync.Library.Loved.Count.ToString(en) : null),
                new TabSpec("Wear today", Linked && read ? sync.Wearable.Count.ToString(en) : null),
            };
            looksTabs = c = (sync.Version, Linked, read, tabs);
        }
        var picked = Kit.UnderlineTabs("##looks-tab", c.Tabs, looksTab);
        if (picked != looksTab)
        {
            looksTab = picked;
            pickedLook = null;
            // A job or mood of one tab's looks may be none of the next's.
            jobFilter = moodFilter = null;
        }
    }

    /// <summary>Search, Ready first (not on Wear today: they're all yours), a job and a mood, on one 34 row (12 above, 8 below).</summary>
    private void DrawLooksFilters(bool narrow)
    {
        Kit.Gap(12);
        var at = Kit.At;
        var w = Kit.Width;
        var gap = S(6);
        var showReady = looksTab != 2;
        var readyLabel = narrow ? "Ready" : "Ready first";
        var jobLabel = jobFilter ?? (narrow ? "Job" : "Any job");
        var moodLabel = moodFilter ?? (narrow ? "Mood" : "Any mood");
        var readyW = showReady ? ReadyPillWidth(readyLabel, readyFirst) : 0;
        var jobW = Kit.ComboWidth(jobLabel);
        var moodW = Kit.ComboWidth(moodLabel);
        var rest = (showReady ? readyW + gap : 0) + jobW + gap + moodW + gap;
        var fieldW = w - rest;
        // Too narrow for all of it on one row: the choices go under the search.
        var wrap = fieldW < S(110);
        if (wrap) fieldW = w;
        var hint = looksTab switch
        {
            0 => narrow ? "Search" : "Search your looks",
            1 => narrow ? "Search" : "Search the looks you love",
            _ => narrow ? "Search" : "Name, maker, job or mood",
        };

        // The × that clears the search: its hit first, so it's pointed at rather than the field under it.
        var clearSize = S(28);
        var clearPos = new Vector2(at.X + fieldW - S(6) - clearSize, at.Y + (S(34) - clearSize) / 2);
        var hasText = search.Length > 0;
        var (cleared, clearHover, _) = hasText ? Kit.Hit("##search-clear", clearPos, new Vector2(clearSize)) : default;
        var text = search;
        if (Kit.Field("##search", at, fieldW, ref text, hint, Icon.Search, 60, ImGuiInputTextFlags.None, 34, pill: true)) search = text;
        if (hasText)
        {
            var dl = Kit.Dl;
            if (clearHover)
            {
                dl.AddCircleFilled(clearPos + new Vector2(clearSize / 2), clearSize / 2, Palette.U32(Palette.AccentWash), 24);
                Kit.Tooltip("Clear the search");
            }
            Icons.Draw(dl, Icon.X, clearPos + new Vector2((clearSize - S(14)) / 2), S(14), clearHover ? Palette.Ink : Palette.Body);
            if (cleared) search = string.Empty;
        }

        var x = wrap ? at.X : at.X + fieldW + gap;
        var y = wrap ? at.Y + S(34) + S(8) : at.Y;
        if (showReady)
        {
            if (ReadyPill("##ready-first", new Vector2(x, y), readyLabel, readyFirst)) readyFirst = !readyFirst;
            x += readyW + gap;
        }
        var (jobs, moods) = FilterChoices();
        FilterCombo("##job", new Vector2(x, y), jobLabel, jobFilter, jobs, "Any job", 0);
        x += jobW + gap;
        FilterCombo("##mood", new Vector2(x, y), moodLabel, moodFilter, moods, "Any mood", 1);
        Kit.Reserve(at, w, (y - at.Y) + S(34));
        Kit.Gap(8);
    }

    private static float ReadyPillWidth(string label, bool on) =>
        S(13) * 2 + Txt.Measure(label, Face.SansSemibold, 13).X + (on ? S(14 + 7) : 0);

    /// <summary>Ready first: a pill like the combos' that turns on and off (the ice tick when on). True when pressed.</summary>
    private static bool ReadyPill(string id, Vector2 pos, string label, bool on)
    {
        var w = ReadyPillWidth(label, on);
        var h = S(34);
        var (clicked, hovered, _) = Kit.Hit(id, pos, new Vector2(w, h));
        var dl = Kit.Dl;
        var max = pos + new Vector2(w, h);
        if (hovered) dl.AddRectFilled(pos, max, Palette.U32(Palette.AccentWash), h / 2);
        dl.AddRect(pos + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(hovered ? Palette.Accent : Palette.LineStrong), h / 2, ImDrawFlags.None, 1f);
        var x = pos.X + S(13);
        if (on)
        {
            Icons.Draw(dl, Icon.Check, new Vector2(x, pos.Y + (h - S(14)) / 2), S(14), Palette.Ice);
            x += S(14 + 7);
        }
        Txt.Line(dl, new Vector2(x, pos.Y), label, Face.SansSemibold, 13, on ? Palette.Ink : Palette.Muted, 34);
        if (hovered) Kit.Tooltip(on ? "The looks ready for a plate first (press for the site's order)" : "Press to put the looks ready for a plate first");
        Kit.FocusRing(pos, max, h / 2);
        return clicked;
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

    /// <summary>A job's or mood's combo and its menu (the "any" one first, the one picked ticked); <paramref name="which"/> 0 the job, 1 the mood.</summary>
    private void FilterCombo(string id, Vector2 pos, string label, string? on, IReadOnlyList<string> choices, string any, int which)
    {
        var menu = id + "-menu";
        var open = ImGui.IsPopupOpen(menu);
        var none = choices.Count == 0 && on == null;
        var old = Palette.Opacity;
        if (none) Palette.Opacity = old * 0.5f;
        var pressed = Kit.Combo(id, pos, label, open);
        Palette.Opacity = old;
        if (none && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + new Vector2(Kit.ComboWidth(label), S(34))))
            Kit.Tooltip(which == 0 ? "None of these looks says a job" : "None of these looks says a mood");
        if (pressed && !none) ImGui.OpenPopup(menu);
        if (!open && !pressed) return;
        ImGui.SetNextWindowPos(new Vector2(pos.X, pos.Y + S(34 + 4)));
        if (!Kit.BeginMenu(menu)) return;
        var w = S(220);
        string? picked = null;
        var chose = false;
        if (Kit.MenuItem(id + "-any", any, null, w, null, null, on == null))
        {
            chose = true;
            ImGui.CloseCurrentPopup();
        }
        foreach (var c in choices)
        {
            if (Kit.MenuItem(id + "-" + c, c, null, w, null, null, string.Equals(c, on, StringComparison.OrdinalIgnoreCase)))
            {
                chose = true;
                picked = c;
                ImGui.CloseCurrentPopup();
            }
        }
        Kit.EndMenu();
        if (!chose) return;
        if (which == 0) jobFilter = picked;
        else moodFilter = picked;
    }

    // ---------------------------------------------------------------------------------------------
    // The list's body: its rows, or what's in their place.

    private void DrawLooksBody(List<Look>? looks, Look? picked, bool narrow, Vector2 listMax)
    {
        if (!Linked)
        {
            if (sync.TokenRefused) DrawTokenRefused();
            Kit.Gap(16);
            Kit.EmptyState("##looks-link", "Link your account (Settings → Account) and your looks from Glamour Studio are here, ready to put on a plate.",
                title: "Your looks show here once you're linked", heightCss: 190);
            return;
        }
        if (sync.LibraryAt == null && sync.LibraryBusy && looksTab != 2)
        {
            DrawLooksLoading();
            return;
        }
        var room = (listMax.Y - Kit.At.Y) / S(1);
        if (TabLooks(looksTab).Count == 0)
        {
            Kit.Gap(8);
            var (sign, hand, line, action) = looksTab switch
            {
                1 => ("No loved looks yet", "go find one you love", "Love a look on Glamour Studio's Discover page and it's here.", "Open Discover"),
                2 => ("None yet", "your closet's still growing", "No public look is all yours yet. As your closet grows (open your dresser, armoire and retainers once), they show here.", (string?)null),
                _ => ("No looks yet", "dress the mannequin", "Dress the mannequin in Glamour Studio's dressing room and save a look: it's here.", "Open the dressing room"),
            };
            var problem = sync.LibraryProblem;
            var h = Math.Clamp(room - 8 - 24 - (problem != null ? 40 : 0), 200, 360);
            if (Kit.EmptyState("##looks-none", line, sign, hand, null, action, Icon.ExternalLink, h))
                Dalamud.Utility.Util.OpenLink(looksTab == 1 ? DiscoverUrl : SiteLink.SiteUrl + "/dressing-room");
            if (problem != null)
            {
                Kit.Gap(10);
                Txt.Note(problem, Palette.Danger);
            }
            return;
        }
        if (looks == null || looks.Count == 0)
        {
            Kit.Gap(32);
            if (Kit.EmptyState("##looks-nothing", NothingWords(), null, null, "Nothing matches", "Clear the filters", null, 0, border: false))
            {
                search = string.Empty;
                jobFilter = moodFilter = null;
            }
            return;
        }

        // The rows, in a pane of their own that scrolls; 12 past the list's padding either side (the mock's .lrow margin).
        var at = Kit.At;
        var left = Kit.Left - S(12);
        var paneMin = new Vector2(left, at.Y);
        var paneSize = new Vector2(listMax.X - left, listMax.Y - at.Y);
        Kit.BeginPane("##looks-rows", paneMin, paneSize, 0, 0, 12, padRight: 8);
        for (var i = 0; i < looks.Count; i++)
        {
            var look = looks[i];
            if (LooksRow(look, ReferenceEquals(look, picked), narrow)) pickedLook = look.Id;
        }
        Kit.EndPane();
        Kit.Reserve(at, Kit.Width, paneSize.Y);
    }

    /// <summary>What was searched for, in words: "No look matches “gold saucer” for a healer."</summary>
    private string NothingWords()
    {
        var q = search.Trim();
        var bits = string.Empty;
        if (jobFilter != null) bits += $" for {(jobFilter.Length > 0 && "AEIOUaeiou".Contains(jobFilter[0]) ? "an" : "a")} {jobFilter.ToLowerInvariant()}";
        if (moodFilter != null) bits += $"{(jobFilter != null ? " and" : string.Empty)} that's {moodFilter.ToLowerInvariant()}";
        return q.Length > 0 ? $"No look matches “{q}”{bits}." : $"No look here{bits}.";
    }

    /// <summary>A refused token, said as a problem (the danger edge): Link again, Settings → Account, Get a token.</summary>
    private void DrawTokenRefused()
    {
        Kit.Gap(16);
        Kit.BeginPanel("##looks-refused", 14, 16, 14, Palette.Card, Palette.DangerEdge);
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        Icons.Draw(dl, Icon.Key, at, S(20), Palette.Danger);
        var x = at.X + S(20 + 12);
        var tw = w - S(20 + 12);
        var y = at.Y;
        Txt.Draw(dl, new Vector2(x, y), "Link again", Face.SansSemibold, 14, Palette.Ink);
        y += Txt.Natural(Face.SansSemibold, 14) + S(6);
        RefusedRun[0] = new Run("The site doesn't know this token any more (it was revoked, or made again). Make a new one on the site and paste it in Settings → Account.", Face.Sans, 13, Palette.Muted);
        y += Txt.Flow(new Vector2(x, y), tw, RefusedRun, 19, out _) + S(10);
        const string settings = "Settings → Account";
        if (Kit.Button("##refused-settings", new Vector2(x, y), settings, Btn.Glass, BtnSize.Sm))
            Later(() => OpenSettings(SettingsTab.Account));
        var bx = x + Kit.ButtonWidth(settings, BtnSize.Sm) + S(8);
        if (Kit.Button("##refused-token", new Vector2(bx, y), "Get a token", Btn.Quiet, BtnSize.Sm, Icon.ExternalLink, iconCss: 14))
            Dalamud.Utility.Util.OpenLink(SiteLink.TokensUrl);
        y += Kit.ButtonHeight(BtnSize.Sm);
        Kit.Reserve(at, w, y - at.Y);
        Kit.EndPanel();
    }

    private static readonly Run[] RefusedRun = new Run[1];

    /// <summary>Reading your looks: the rows' shapes in the soft ink, fading down the list and breathing slowly, and the hand saying so.</summary>
    private static void DrawLooksLoading()
    {
        var dl = Kit.Dl;
        var breath = 0.75f + 0.25f * Pulse();
        ReadOnlySpan<float> fade = stackalloc float[] { 1f, 0.7f, 0.45f };
        ReadOnlySpan<float> names = stackalloc float[] { 180, 150, 200 };
        ReadOnlySpan<float> lines = stackalloc float[] { 110, 90, 120 };
        var old = Palette.Opacity;
        for (var i = 0; i < 3; i++)
        {
            var at = Kit.At;
            var h = S(64);
            Palette.Opacity = old * fade[i] * breath;
            var col = Palette.U32(Palette.Tile);
            var y = at.Y + (h - S(32)) / 2;
            dl.AddRectFilled(new Vector2(at.X, y), new Vector2(at.X + S(140), y + S(32)), col, S(8));
            var x = at.X + S(140 + 12);
            var ty = at.Y + (h - S(12 + 6 + 10)) / 2;
            dl.AddRectFilled(new Vector2(x, ty), new Vector2(x + S(names[i]), ty + S(12)), col, S(8));
            Palette.Opacity = old * fade[i] * breath * 0.6f;
            dl.AddRectFilled(new Vector2(x, ty + S(18)), new Vector2(x + S(lines[i]), ty + S(28)), Palette.U32(Palette.Tile), S(8));
            Kit.Reserve(at, Kit.Width, h);
        }
        Palette.Opacity = old;
        Kit.Gap(12);
        var hat = Kit.At;
        Kit.Reserve(hat, Kit.Width, Kit.Hand(hat, "reading your looks…", 13));
    }

    /// <summary>
    /// A look's row (the mock's .lrow, 64 tall): four of its pieces, its name in display capitals,
    /// whose it is and what for, and how ready on the right (or its loves, on Wear today); the one
    /// picked lilac-washed with a lilac hairline. True when pressed.
    /// </summary>
    private bool LooksRow(Look look, bool on, bool narrow)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(64);
        if (!ImGui.IsRectVisible(at, at + new Vector2(w, h)))
        {
            Kit.Reserve(at, w, h);
            return false;
        }
        var info = RowInfo(look);
        // By the look's id, not its place: a list read again mid-click doesn't press another look.
        ImGui.PushID(look.Id);
        (bool Clicked, bool Hovered, bool Held) hit;
        try
        {
            hit = Kit.Hit("##row", at, new Vector2(w, h));
        }
        finally
        {
            ImGui.PopID();
        }
        var dl = Kit.Dl;
        var max = at + new Vector2(w, h);
        if (on)
        {
            dl.AddRectFilled(at, max, Palette.U32(Palette.AccentWash), S(14));
            dl.AddRect(at + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(Palette.AccentLine), S(14), ImDrawFlags.None, 1f);
        }
        else if (hit.Hovered) dl.AddRectFilled(at, max, Palette.U32(Palette.RowHover), S(14));

        var size = narrow ? 28f : 32f;
        var n = narrow ? 3 : 4;
        var x = at.X + S(12);
        for (var i = 0; i < n && i < info.Icons.Length; i++)
            Kit.Item(new Vector2(x + (S(size) + S(4)) * i, at.Y + (h - S(size)) / 2), size, info.Icons[i]);
        x += S(size) * n + S(4) * (n - 1) + S(12);

        // How ready, on the right: one or two tags, stacked (one, shortened, when narrow); or the loves.
        var right = max.X - S(12);
        var tagsW = 0f;
        var tagH = Kit.TagHeight();
        if (info.Loves != null)
        {
            tagsW = LovesTagWidth(info.Loves);
            LovesTag(new Vector2(right - tagsW, at.Y + (h - tagH) / 2), info.Loves);
        }
        else if (narrow || info.Tags.Length == 1)
        {
            var (t, k, ic) = narrow ? info.ShortTag : info.Tags[0];
            tagsW = Kit.TagWidth(t, ic);
            Kit.Tag(new Vector2(right - tagsW, at.Y + (h - tagH) / 2), t, k, ic);
        }
        else
        {
            var stack = tagH * info.Tags.Length + S(4) * (info.Tags.Length - 1);
            var ty = at.Y + (h - stack) / 2;
            foreach (var (t, k, ic) in info.Tags)
            {
                var tw = Kit.TagWidth(t, ic);
                tagsW = MathF.Max(tagsW, tw);
                Kit.Tag(new Vector2(right - tw, ty), t, k, ic);
                ty += tagH + S(4);
            }
        }

        var textW = right - tagsW - S(12) - x;
        var nameBox = S(14 * 1.1f);
        var lineH = Txt.Natural(Face.Sans, 12);
        var y = at.Y + (h - nameBox - lineH) / 2;
        Kit.Display(new Vector2(x, y + (nameBox - Kit.DisplayHeight(14)) / 2), look.Name, 14, Palette.Ink, textW);
        var line = narrow ? info.Short : info.Line;
        Txt.Draw(dl, new Vector2(x, y + nameBox), Txt.Fit(line, Face.Sans, 12, textW), Face.Sans, 12, Palette.Muted);
        Kit.Reserve(at, w, h);
        return hit.Clicked;
    }

    private static float LovesTagWidth(string loves) => S(7) * 2 + S(11 + 4) + Txt.Measure(loves, Face.SansSemibold, 11).X;

    /// <summary>A look's loves as a tag (pink, as the site's: loves are pink), its heart filled.</summary>
    private static void LovesTag(Vector2 pos, string loves)
    {
        var w = LovesTagWidth(loves);
        var h = Kit.TagHeight();
        var dl = Kit.Dl;
        dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(Palette.PinkWash), S(Palette.RadiusTag));
        DrawHeart(new Vector2(pos.X + S(7) + S(5.5f), pos.Y + h / 2), S(11), Palette.Pink);
        Txt.Line(dl, new Vector2(pos.X + S(7 + 11 + 4), pos.Y), loves, Face.SansSemibold, 11, Palette.Pink, 21);
    }

    /// <summary>The eyebrow over an opened look's name: whose it is (or that it was sent, or that you own it all).</summary>
    private string LookEyebrow(Look look)
    {
        if (openSent != null && openSent.Look.Id == look.Id)
            return "Sent from the site" + (openSent.Plate is { } sp ? $" · for plate {sp}" : string.Empty);
        var plannedFor = PlannedPlateOf(look.Id);
        var who = look.Mine ? "Your look" : sync.Library.Loved.Any(l => l.Id == look.Id) ? $"Loved · by {look.Maker}" : $"Wear today · by {look.Maker}";
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
    // The look picked: a small character sheet.

    /// <summary>What the picked look's sheet says, made once per look, tab and change.</summary>
    private sealed class PickInfo
    {
        public string Eyebrow = string.Empty;
        public string Short = string.Empty;
        public string Initials = string.Empty;
        public uint[] Rgbs = Array.Empty<uint>();
        public string[] Names = Array.Empty<string>();
        public string PiecesCount = string.Empty;
        public readonly List<PieceInfo> Pieces = new();
    }

    private sealed record PieceInfo(Piece Piece, uint Icon, string Name, string Line, string Short, string Tag, TagKind Kind, Icon? TagIcon, Own Own, string Tip, bool Amber);

    private (Look Look, int Tab, int Sync, int Wardrobe, PickInfo Info)? pickInfo;

    private PickInfo PickInfoOf(Look look)
    {
        if (pickInfo is { } c && ReferenceEquals(c.Look, look) && c.Tab == looksTab && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version) return c.Info;
        var info = new PickInfo();
        var bits = new List<string>();
        if (looksTab == 2)
        {
            info.Eyebrow = $"Wear today · by {look.Maker} · you own it all";
            info.Short = $"Wear today · by {look.Maker}";
        }
        else
        {
            var who = look.Mine ? "Your look" : $"Loved · by {look.Maker}";
            bits.Add(who);
            if (!string.IsNullOrWhiteSpace(look.Job)) bits.Add(look.Job);
            if (!string.IsNullOrWhiteSpace(look.Mood)) bits.Add(look.Mood);
            var planned = PlannedPlateOf(look.Id);
            var sent = SentPlateOf(look.Id);
            if (planned > 0) bits.Add($"planned for plate {planned}");
            else if (sent > 0) bits.Add($"sent for plate {sent}");
            info.Eyebrow = string.Join(" · ", bits);
            info.Short = who;
        }
        info.Initials = Initials.Of(look.Maker);
        (info.Rgbs, info.Names) = DyesOf(look.Pieces, 6);
        var r = ReadinessOf(look);
        info.PiecesCount = looksTab == 2 && r.Owned == r.Total ? $"{r.Total} · all yours" : r.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var slot in Slots.All)
        {
            if (!look.Pieces.TryGetValue(slot, out var p)) continue;
            var item = data.Item(p.Item);
            var where = Where(p.Item);
            var wished = sync.Library.Wishes.Contains(LookJson.BaseId(p.Item));
            var (tag, kind, icon, own, tip) = WhereTag(where, wished);
            var line = Slots.Word(slot) + (DyeLine(p) is { } d ? $" · {d}" : data.DyeChannels(p.Item) > 0 ? " · undyed" : string.Empty);
            info.Pieces.Add(new PieceInfo(p, item?.Icon ?? 0, item?.Name ?? $"Item {p.Item}", line, Slots.Word(slot), tag, kind, icon, own, tip, kind == TagKind.Amber));
        }
        pickInfo = (look, looksTab, sync.Version, wardrobe.Version, info);
        return info;
    }

    private static readonly EchoRow[] PickEcho = new EchoRow[2];

    private void DrawLookPick(Look look, bool narrow)
    {
        var info = PickInfoOf(look);
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(narrow ? 168 : 196);
        var max = at + new Vector2(w, h);
        using (Kit.FieldPanel(at, max))
        {
            // The name echoed behind at 72 (two rows; one when narrow).
            var wCss = w / S(1);
            PickEcho[0] = new EchoRow(Kit.EchoRowOf(look.Name, (int)MathF.Ceiling(wCss / (72 * 0.55f)) + 3), narrow ? 100 : 120, narrow ? 40 : 54);
            PickEcho[1] = new EchoRow(Kit.EchoRowOf(look.Name, (int)MathF.Ceiling((wCss + 40) / (72 * 0.55f)) + 6), -40, 54 + 72 * 0.92f);
            Kit.Echo(at, max, Palette.RadiusXl, PickEcho.AsSpan(0, narrow ? 1 : 2), 72);
            var padX = S(narrow ? 18 : 20);
            var x = at.X + padX;
            var y = at.Y + S(narrow ? 16 : 18);
            var seal = !narrow && info.Initials.Length > 0;
            var room = w - padX * 2 - (seal ? S(30 + 16) : 0);
            if (seal) Kit.Seal(new Vector2(max.X - S(20) - S(15), at.Y + S(18) + S(15)), info.Initials);
            y += Kit.Eyebrow(new Vector2(x, y), narrow ? info.Short : info.Eyebrow, null, room) + S(8);
            var px = narrow ? 28 : 34;
            var box = S(px * 0.98f);
            Kit.Display(new Vector2(x, y + (box - Kit.DisplayHeight(px)) / 2), look.Name, px, null, MathF.Min(room, S(400)), stacked: true, em: 0.015f);
            y += box + S(8 + 4);
            if (info.Rgbs.Length > 0) Kit.PaletteStrip(new Vector2(x, y), MathF.Min(S(420), w - padX * 2), info.Rgbs, info.Names, big: true);
            else Txt.Draw(Kit.Dl, new Vector2(x, y), "Undyed", Face.SansMedium, 13, Tint.Muted);
        }
        Kit.Reserve(at, w, h);

        var plate = editorPlate;
        var sticker = plate == 0 && !PlateItHere;
        Kit.Gap(sticker && !narrow ? 16 : 12);
        DrawPickButtons(look, narrow, sticker);

        // The plate (the look screen's slip, compact): Glamour Plates open, the slip with Put on plate N;
        // the dresser open, Put on a plate…; just filled, what happened; else the line saying how ready it is.
        Kit.Gap(sticker ? 16 : 12);
        DrawPlateCard(look, compact: true);

        // Its pieces and where each is.
        Kit.Gap(16);
        var hat = Kit.At;
        var hw = Kit.Display(hat, "Pieces", 15, Palette.Ink);
        var cy = hat.Y + Txt.Ascent(Face.Display, 15) - Txt.Ascent(Face.Mono, 12);
        Txt.Draw(Kit.Dl, new Vector2(hat.X + hw + S(10), cy), info.PiecesCount, Face.Mono, 12, Palette.Faint);
        Kit.Reserve(hat, Kit.Width, Kit.DisplayHeight(15) + S(2));
        for (var i = 0; i < info.Pieces.Count; i++) PickPieceRow(info.Pieces[i], i > 0, narrow);
    }

    private void DrawPickButtons(Look look, bool narrow, bool sticker)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var smH = Kit.ButtonHeight(BtnSize.Sm);
        var size = narrow ? StickerSize.Sm : StickerSize.Md;
        var rowH = sticker ? Kit.StickerHeight(size) : smH;
        var by = at.Y + (rowH - smH) / 2;
        var x = at.X;
        const string tryOn = "Try it on";
        const string tryTip = "Opens the game's fitting room with the look on you";
        if (sticker)
        {
            if (Kit.Sticker("##pick-try", at, tryOn, size, Icon.Shirt, 0, false, tryTip)) Later(() => TryOn(look.Pieces));
            x += Kit.StickerWidth(tryOn, size, Icon.Shirt) + S(3 + 8);
        }
        else
        {
            if (Kit.Button("##pick-try", new Vector2(x, by), tryOn, Btn.Glass, BtnSize.Sm, Icon.Shirt, 0, false, tryTip, iconCss: 14)) Later(() => TryOn(look.Pieces));
            x += Kit.ButtonWidth(tryOn, BtnSize.Sm, Icon.Shirt) + S(8);
        }
        // A shot is for a public look's Worn in game on the site.
        if (look.Visibility == "public")
        {
            const string shotTip = "Opens the look, to take a shot for its Worn in game on the site";
            var shot = narrow
                ? Kit.IconButton("##pick-shot", new Vector2(x, by), Icon.Camera, shotTip, 34, Btn.Glass, !Linked, 14)
                : Kit.Button("##pick-shot", new Vector2(x, by), "Shot", Btn.Glass, BtnSize.Sm, Icon.Camera, 0, !Linked, shotTip, iconCss: 14);
            if (shot) Later(() => OpenLook(look, null, Screen.Looks));
            x += (narrow ? S(34) : Kit.ButtonWidth("Shot", BtnSize.Sm, Icon.Camera)) + S(8);
        }
        const string siteTip = "The look's page on Glamour Studio";
        var site = narrow
            ? Kit.IconButton("##pick-site", new Vector2(x, by), Icon.ExternalLink, siteTip, 34, Btn.Glass, false, 14)
            : Kit.Button("##pick-site", new Vector2(x, by), "Site", Btn.Quiet, BtnSize.Sm, Icon.ExternalLink, 0, false, siteTip, iconCss: 14);
        if (site) Dalamud.Utility.Util.OpenLink(SiteLink.LookUrl(look.Id));
        x += (narrow ? S(34) : Kit.ButtonWidth("Site", BtnSize.Sm, Icon.ExternalLink, Btn.Quiet)) + S(8);

        // Open the look, at the end: its words then the arrow.
        var open = narrow ? "Open" : "Open the look";
        var textW = Txt.Measure(open, Face.SansSemibold, 13).X;
        var ow = S(13) * 2 + textW + (narrow ? 0 : S(7 + 14));
        var ox = MathF.Max(x, at.X + w - ow);
        if (Kit.Button("##pick-open", new Vector2(ox, by), string.Empty, Btn.Glass, BtnSize.Sm, null, ow, false, narrow ? "Open the look" : null))
            Later(() => OpenLook(look, null, Screen.Looks));
        var dl = Kit.Dl;
        Txt.Line(dl, new Vector2(ox + S(13), by), open, Face.SansSemibold, 13, Palette.Ink, 34);
        if (!narrow) Icons.Draw(dl, Icon.ArrowRight, new Vector2(ox + S(13) + textW + S(7), by + (smH - S(14)) / 2), S(14), Palette.Ink);
        Kit.Reserve(at, w, rowH + (sticker ? S(3) : 0));
    }

    /// <summary>A piece (the mock's .prow): its icon with the corner dot, name, slot and dyes, and where it is; a hairline above all but the first. Pointed at, the whole line.</summary>
    private void PickPieceRow(PieceInfo p, bool line, bool narrow)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(52);
        var dl = Kit.Dl;
        if (line) Kit.Divider(at.X, at.X + w, at.Y);
        Kit.Item(new Vector2(at.X, at.Y + (h - S(36)) / 2), 36, p.Icon, p.Own, ring: Palette.Ground2);
        var tagW = Kit.TagWidth(p.Tag, p.TagIcon);
        Kit.Tag(new Vector2(at.X + w - tagW, at.Y + (h - Kit.TagHeight()) / 2), p.Tag, p.Kind, p.TagIcon);
        var x = at.X + S(36 + 11);
        var tw = at.X + w - tagW - S(11) - x;
        var nh = S(13 * 1.25f);
        var lh = S(12 * 1.35f);
        var y = at.Y + (h - nh - lh) / 2;
        Txt.Line(dl, new Vector2(x, y), Txt.Fit(p.Name, Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink, 13 * 1.25f);
        Txt.Line(dl, new Vector2(x, y + nh), Txt.Fit(narrow ? p.Short : p.Line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted, 12 * 1.35f);
        if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(at, at + new Vector2(w, h)))
            Kit.Tooltip(p.Name, $"{p.Line}\n{p.Tip}", p.Amber ? Palette.Amber : (Vector4?)null);
        Kit.Reserve(at, w, h);
    }

    /// <summary>Where a piece is, as a tag in the honest colours: ice where a plate takes it from, amber when it's to put away, lavender wished, pink missing.</summary>
    private static (string Text, TagKind Kind, Icon? Icon, Own Own, string Tip) WhereTag(Place? where, bool wished) => where switch
    {
        Place.Dresser => ("Dresser", TagKind.Ok, Icon.Check, Own.Ice, "In your glamour dresser: ready for a plate."),
        Place.Armoire => ("Armoire", TagKind.Ok, Icon.Check, Own.Ice, "In your armoire: ready for a plate."),
        { } p => (Txt.Sentence(Places.Where(p)), TagKind.Amber, Icon.Bag, Own.Amber,
            $"Yours, {Places.Where(p)}. A plate takes pieces from the dresser or the armoire: put it away first."),
        null => wished
            ? ("On your wishlist", TagKind.Star, Icon.Star, Own.Star, "On your wishlist on the site: the plugin hasn't seen it in your wardrobe yet.")
            : ("Missing", TagKind.Miss, null, Own.Pink,
                "The plugin hasn't seen it in your wardrobe. Open your dresser, armoire, saddlebags and retainers once, and it knows them."),
    };

    /// <summary>Where a piece is, as a chip (for a look's own screen; this screen's are <see cref="WhereTag"/>).</summary>
    private static (string Text, ChipKind Kind, string Tip) WhereChip(Place? where, bool wished)
    {
        var (text, kind, _, _, tip) = WhereTag(where, wished);
        return (text, kind switch
        {
            TagKind.Ok => ChipKind.Ok,
            TagKind.Miss => ChipKind.Love,
            TagKind.Amber or TagKind.Star => ChipKind.Accent,
            _ => ChipKind.Plain,
        }, tip);
    }
}

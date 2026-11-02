using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>Home's tabs.</summary>
public enum HomeTab
{
    Looks,
    Loved,
    WearNow,
    Plates,
    Closet,
}

/// <summary>
/// Home: the title with Settings and a pill for what the plugin's doing; the plate editor's card when
/// it's open (the plate it's on, the look planned for it on the site, Fill it); someone's outfit while
/// the Examine window's open (Try it on, Keep it as a look); the looks sent from the site; then your
/// looks, the ones you love, the public looks you could wear now, your plates and your closet as
/// tabs, with a search over the looks. Not linked yet: three steps to begin with, and the plates
/// and closet the game shows work all the same.
/// </summary>
public sealed partial class MainWindow
{
    private static readonly string[] HomeTabNames = { "Looks", "Loved", "Wear now", "Plates", "Closet" };

    private HomeTab homeTab;
    private string search = string.Empty;

    /// <summary>The open plate's pieces and the look they are, as last worked out.</summary>
    private (IReadOnlyDictionary<Slot, Piece>? Pieces, int SyncVersion, Look? Look) editorMatch;

    /// <summary>The looks shown, as last filtered and sorted, and what they were worked out from.</summary>
    private (string Search, bool Loved, int Sync, int Wardrobe, bool Editor, List<Look> Looks)? shownLooks;

    /// <summary>Each plate's (and the outfit's) look, by the plate (0 the outfit), for the versions they were worked out at.</summary>
    private readonly Dictionary<int, Look?> plateMatches = new();

    private (int Sync, int Wardrobe) plateMatchesFor = (-1, -1);

    /// <summary>The look a plate of yours is (0: what you wear), worked out once per change.</summary>
    private Look? PlateMatch(int plate, IReadOnlyDictionary<Slot, Piece> pieces)
    {
        var stamp = (sync.Version, wardrobe.Version);
        if (stamp != plateMatchesFor)
        {
            plateMatchesFor = stamp;
            plateMatches.Clear();
        }
        if (!plateMatches.TryGetValue(plate, out var match))
            plateMatches[plate] = match = Plates.Matching(pieces, sync.Library.Mine.Concat(sync.Library.Loved));
        return match;
    }

    /// <summary>The header pill: what the plugin is doing now.</summary>
    private (string Text, Tone Tone, bool Pulse) StatusNow()
    {
        if (editorPlate > 0) return ($"Plate editor open · plate {editorPlate}", Tone.Live, true);
        if (!sync.Linked) return ("Not linked to Glamour Studio", Tone.Neutral, false);
        if (sync.TokenRefused) return ("Link again: the site doesn't know this token", Tone.Danger, false);
        if (sync.Syncing) return ("Sending your closet…", Tone.Live, true);
        if (sync.SyncProblem is { } p) return (p, Tone.Danger, false);
        if (sync.LastSyncAt is { } at) return ($"Closet up to date · sent {Words.Ago(at, DateTimeOffset.UtcNow)}", Tone.Success, false);
        return (config.AutoSync ? "Linked · sends your closet as you play" : "Linked", Tone.Success, false);
    }

    private void DrawHome()
    {
        Frame.BeginHeader(20);
        var at = Kit.At;
        var w = Kit.Width;
        // The title beside the Settings button, centred on its 36px.
        var ty = at.Y + MathF.Max(0, (S(36) - S(24 * 1.05f)) / 2);
        Title(new Vector2(at.X, ty), "Glamour Studio", w - S(48), 24, 1);
        SettingsButton(new Vector2(at.X + w - S(36), at.Y));
        var (status, tone, pulse) = StatusNow();
        var pillY = at.Y + S(36) + S(12);
        Kit.StatusPill(new Vector2(at.X, pillY), status, tone, pulse, w);
        Kit.Reserve(at, w, pillY + S(28) - at.Y);
        Frame.EndHeader();

        Frame.BeginBody("##home", null, 18, 24);
        if (editorPlate > 0) DrawEditorCard(editorPlate);
        if (Examine.IsOpen) DrawExamined();
        if (!sync.Linked) DrawGetStarted();
        DrawSent();

        var counts = new[]
        {
            Linked ? sync.Library.Mine.Count.ToString() : null,
            Linked ? sync.Library.Loved.Count.ToString() : null,
            Linked && sync.Wearable.Count > 0 ? sync.Wearable.Count.ToString() : null,
            wardrobe.Current?.Plates.Count(p => p.Pieces.Count > 0) is { } n and > 0 ? n.ToString() : null,
            null,
        };
        var tabs = HomeTabNames.Select((t, i) => new TabSpec(t, counts[i])).ToList();
        homeTab = (HomeTab)Kit.Tabs("##home-tab", tabs, (int)homeTab);
        Kit.Gap(14);
        switch (homeTab)
        {
            case HomeTab.Plates:
                DrawPlatesTab();
                break;
            case HomeTab.Closet:
                DrawClosetTab();
                break;
            case HomeTab.WearNow:
                DrawWearNowTab();
                break;
            default:
                DrawLooksTab(homeTab == HomeTab.Loved);
                break;
        }
        Frame.EndBody();
    }

    // ---------------------------------------------------------------------------------------------
    // The plate editor, open.

    private void DrawEditorCard(int plate)
    {
        Kit.BeginPanel("##editor", 16, 18, 16, Palette.RoseWash, Palette.RoseEdge);
        var at = Kit.At;
        var w = Kit.Width;
        Kit.IconWell(at, S(40), Icon.Layers);
        var x = at.X + S(40) + S(14);
        var tw = w - (x - at.X);
        Kit.Eyebrow(new Vector2(x, at.Y), "Plate editor", Palette.Rose, tw);
        var ty = at.Y + Txt.Natural(Face.Mono, 11) + S(3);
        var th = Title(new Vector2(x, ty), $"Plate {plate}", tw, 20, 1);
        var current = PlateEditor.Current();
        // The look it is, worked out again only when the plate or your looks change.
        if (editorMatch.Pieces == null || editorMatch.SyncVersion != sync.Version || !Plates.Same(current, editorMatch.Pieces))
            editorMatch = (current, sync.Version, Plates.Matching(current, sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Inbox.Select(s => s.Look))));
        var match = editorMatch.Look;
        var line = current.Count == 0
            ? "It's empty. Pick a look below and put it on."
            : match != null ? $"It's your look “{match.Name}”." : $"It has {Words.Count(current.Count, "piece")}. Pick a look below to put it on instead.";
        if (PlateEditor.HasChanges) line += " Not saved yet.";
        var lh = Txt.Flow(new Vector2(x, ty + th + S(2)), tw, new[] { new Run(line, Face.Sans, 13, Palette.Soft) }, 19.5f, out _);
        Kit.Reserve(at, w, MathF.Max(S(40), ty + th + S(2) + lh - at.Y));

        // The look planned for this plate on the site: one press to fill it.
        if (sync.PlanFor(plate) is { } planned)
        {
            Kit.Gap(12);
            var row = Kit.At;
            if (Plates.Same(current, planned.Pieces))
                Txt.Line(Kit.Dl, row, Txt.Fit($"✓ It's the look planned for it, “{planned.Name}”.", Face.SansMedium, 13, w), Face.SansMedium, 13, Palette.Success, 30);
            else
            {
                var label = $"Fill it with “{planned.Name}”";
                var bw = MathF.Min(w - S(44), Kit.ButtonWidth(label, BtnSize.Md, Icon.Layers, Btn.Primary));
                if (Kit.Button("##fill-planned", row, label, Btn.Primary, BtnSize.Md, Icon.Layers, bw, false,
                        "The look planned for this plate on the site (Closet → Plates). Nothing's saved until you press Save in the game."))
                    Later(() => Fill(planned, PlanFor(planned.Pieces)));
                if (Kit.IconButton("##open-planned", new Vector2(row.X + bw + S(8), row.Y), Icon.ChevronRight, "See the look first", 36))
                    Later(() => OpenLook(planned, null));
            }
            Kit.Reserve(row, w, S(36));
        }
        Kit.EndPanel();
        Kit.Gap(18);
    }

    // ---------------------------------------------------------------------------------------------
    // Someone's outfit, while the Examine window's open.

    private Examined? examined;
    private DateTime examinedAt;

    private void DrawExamined()
    {
        // (Read again twice a second at most: the window's open a while.)
        if (DateTime.UtcNow - examinedAt > TimeSpan.FromSeconds(0.5) || examined == null)
        {
            examined = Examine.Read(data);
            examinedAt = DateTime.UtcNow;
        }
        if (examined is not { } e) return;
        Kit.BeginPanel("##examine", 16, 18, 16);
        var at = Kit.At;
        var w = Kit.Width;
        Mosaic(at, S(48), e.Pieces);
        var x = at.X + S(48) + S(14);
        var tw = w - (x - at.X);
        Kit.Eyebrow(new Vector2(x, at.Y), "Examining", Palette.Rose, tw);
        var ty = at.Y + Txt.Natural(Face.Mono, 11) + S(3);
        var th = Title(new Vector2(x, ty), e.Name.Length > 0 ? $"{e.Name}'s outfit" : "Their outfit", tw, 16, 1);
        Txt.Line(Kit.Dl, new Vector2(x, ty + th + S(2)), Words.Count(e.Pieces.Count, "piece"), Face.Sans, 13, Palette.Muted, 19.5f);
        Kit.Reserve(at, w, MathF.Max(S(48), ty + th + S(2) + S(19.5f) - at.Y));
        Kit.Gap(12);
        var row = Kit.At;
        if (Kit.Button("##ex-try", row, "Try it on", Btn.Glass, BtnSize.Sm, Icon.Eye))
        {
            var pieces = e.Pieces;
            Later(() => TryOn(pieces));
        }
        var bx = row.X + Kit.ButtonWidth("Try it on", BtnSize.Sm, Icon.Eye) + S(8);
        const string keep = "Keep it as a look";
        if (Kit.Button("##ex-keep", new Vector2(bx, row.Y), keepingExamined ? "Keeping…" : keep, Btn.Glass, BtnSize.Sm, Icon.Sparkles, 0, !Linked || keepingExamined,
                Linked ? "A private look of yours on the site, to remix in the dressing room" : "Link your account first (Settings → Account)"))
        {
            keepingExamined = true;
            var name = e.Name.Length > 0 ? $"{e.Name}'s outfit" : "An outfit I saw";
            sync.SaveLook(name, e.Body, e.Pieces, r =>
            {
                keepingExamined = false;
                Notify(r.Ok ? $"Kept as “{r.Value!.Name}” in your looks" : r.Why ?? "The site said no");
            });
        }
        Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Sm));
        Kit.EndPanel();
        Kit.Gap(18);
    }

    private bool keepingExamined;

    // ---------------------------------------------------------------------------------------------
    // Looks you could wear now: public looks you own every piece of.

    private void DrawWearNowTab()
    {
        if (!Linked)
        {
            Empty("##wear-empty", Icon.Shirt, "Looks you could wear now",
                "Link your account and the public looks you own every piece of are here, from the most loved.");
            return;
        }
        var looks = sync.Wearable;
        if (looks.Count == 0)
        {
            Empty("##wear-none", Icon.Shirt, "None yet",
                "No public look on Glamour Studio is all yours yet. As your closet grows (open your dresser, armoire and retainers once), they show here.");
            return;
        }
        Txt.Note("Public looks on Glamour Studio you own every piece of, the most loved first.", Palette.Muted);
        Kit.Gap(10);
        Kit.BeginPanel("##wear", 6, 6, 6);
        for (var i = 0; i < looks.Count; i++)
        {
            var look = looks[i];
            if (LookRow($"##wear{i}", i, look)) Later(() => OpenLook(look, null));
        }
        Kit.EndPanel();
    }

    // ---------------------------------------------------------------------------------------------
    // Sent from the site.

    private void DrawSent()
    {
        var sent = sync.Inbox;
        if (!Linked || sent.Count == 0) return;
        Kit.Section("Sent from the site", Words.Count(sent.Count, "look"));
        Kit.Gap(10);
        Kit.BeginPanel("##sent", 6, 6, 6, Palette.Card, Palette.RoseEdge);
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < sent.Count && i < 6; i++)
        {
            var s = sent[i];
            var note = $"Sent {Words.Ago(s.CreatedAt, now)}" + (s.Plate is { } p ? $" · for plate {p}" : string.Empty);
            if (LookRow($"##sent{i}", i, s.Look, note)) Later(() => OpenLook(s.Look, s));
        }
        Kit.EndPanel();
        Kit.Gap(22);
    }

    // ---------------------------------------------------------------------------------------------
    // Your looks, and the ones you love.

    private void DrawLooksTab(bool loved)
    {
        if (!Linked)
        {
            Empty("##looks-empty", Icon.Hanger, loved ? "The looks you love" : "Your looks",
                "Link your account (Settings → Account) and your looks from Glamour Studio are here, ready to put on a plate.");
            return;
        }
        var all = loved ? sync.Library.Loved : sync.Library.Mine;
        var at = Kit.At;
        var text = search;
        if (Kit.Field("##search", at, Kit.Width - S(36) - S(8), ref text, loved ? "Find a look you love" : "Find one of your looks", Icon.Search, 60))
            search = text;
        if (Kit.IconButton("##refresh", new Vector2(at.X + Kit.Width - S(36), at.Y), Icon.Refresh,
                sync.LibraryBusy ? "Reading your looks…" : "Read your looks from the site again", 36, Btn.Glass, sync.LibraryBusy))
            sync.RefreshLibrary();
        Kit.Reserve(at, Kit.Width, S(36));
        Kit.Gap(12);

        if (sync.LibraryProblem is { } problem)
        {
            Txt.Note(problem, Palette.Danger);
            Kit.Gap(10);
        }
        if (sync.LibraryAt == null && sync.LibraryBusy)
        {
            Txt.Note("Reading your looks…");
            return;
        }
        var q = search.Trim();
        var editor = editorPlate > 0;
        List<Look> shown;
        if (shownLooks is { } c && c.Search == q && c.Loved == loved && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version && c.Editor == editor)
            shown = c.Looks;
        else
        {
            shown = q.Length == 0
                ? all.ToList()
                : all.Where(l => l.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || l.Maker.Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || (l.Job?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                                 || (l.Mood?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            // With the plate editor open, the ones ready for a plate first, as they were among themselves.
            if (editor) shown = shown.OrderByDescending(l => ReadinessOf(l).PlateReady).ToList();
            shownLooks = (q, loved, sync.Version, wardrobe.Version, editor, shown);
        }
        if (all.Count == 0)
        {
            Empty("##none", loved ? Icon.Heart : Icon.Sparkles, loved ? "No loved looks yet" : "No looks yet",
                loved ? "Love a look on Glamour Studio's front page and it's here." : "Dress the mannequin in Glamour Studio's dressing room and save a look: it's here.");
            Kit.Gap(12);
            if (Kit.Button("##open-site", Kit.At, "Open Glamour Studio", Btn.Glass, BtnSize.Md, Icon.ExternalLink))
                Dalamud.Utility.Util.OpenLink(SiteLink.SiteUrl);
            Kit.Reserve(Kit.At, Kit.Width, Kit.ButtonHeight(BtnSize.Md));
            return;
        }
        if (shown.Count == 0)
        {
            Txt.Note($"No look matches “{q}”.");
            return;
        }
        Kit.BeginPanel(loved ? "##loved" : "##mine", 6, 6, 6);
        for (var i = 0; i < shown.Count; i++)
        {
            var look = shown[i];
            if (LookRow($"##look{i}", i, look)) Later(() => OpenLook(look, null));
        }
        Kit.EndPanel();
    }

    // ---------------------------------------------------------------------------------------------
    // Nothing linked yet.

    private static readonly (Icon Icon, string Title, string Line)[] Steps =
    {
        (Icon.Link, "Link your account", "Make a token on Glamour Studio (Settings → Glamour Studio → In-game plugin) and paste it in Settings → Account."),
        (Icon.Archive, "Open your dresser and armoire once", "The game only shows them then. The plugin remembers what's in them, and in your bags, armoury chest, saddlebags and retainers."),
        (Icon.Layers, "Put a look on a plate", "At a glamour dresser, open Glamour Plates, pick a look here and press Put on plate."),
    };

    private void DrawGetStarted()
    {
        Kit.Section("Get started", "Three steps");
        Kit.Gap(12);
        var dl = Kit.Dl;
        for (var i = 0; i < Steps.Length; i++)
        {
            var (icon, title, line) = Steps[i];
            Kit.BeginPanel($"##step{i}", 14, 16, 14);
            var at = Kit.At;
            var w = Kit.Width;
            Kit.IconWell(at, S(40), icon);
            var x = at.X + S(40) + S(14);
            var num = (i + 1).ToString();
            var numW = Txt.Measure(num, Face.Display, 20).X;
            Txt.Draw(dl, new Vector2(at.X + w - numW, at.Y), num, Face.Display, 20, Palette.Faint);
            var tw = w - (x - at.X) - numW - S(12);
            Txt.Line(dl, new Vector2(x, at.Y), Txt.Fit(title, Face.SansSemibold, 14, tw), Face.SansSemibold, 14, Palette.Ink);
            var h = Txt.Flow(new Vector2(x, at.Y + Txt.Natural(Face.SansSemibold, 14) + S(2)), tw, new[] { new Run(line, Face.Sans, 13, Palette.Muted) }, 19.5f, out _);
            Kit.Reserve(at, w, MathF.Max(S(40), Txt.Natural(Face.SansSemibold, 14) + S(2) + h));
            Kit.EndPanel();
            Kit.Gap(8);
        }
        Kit.Gap(6);
        var row = Kit.At;
        const string link = "Link your account";
        if (Kit.Button("##gs-link", row, link, Btn.Primary, BtnSize.Md, Icon.Link, 0, false, "Once: then your closet keeps itself up to date on the site"))
            OpenSettings(SettingsTab.Account);
        var x0 = row.X + Kit.ButtonWidth(link, BtnSize.Md, Icon.Link, Btn.Primary) + S(8);
        if (Kit.Button("##gs-open", new Vector2(x0, row.Y), "Open Glamour Studio", Btn.Glass, BtnSize.Md, Icon.ExternalLink))
            Dalamud.Utility.Util.OpenLink(SiteLink.SiteUrl);
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Md));
        Kit.Gap(26);
    }

    // ---------------------------------------------------------------------------------------------
    // Your plates, as the game has them.

    private void DrawPlatesTab()
    {
        var me = wardrobe.Current;
        if (me == null)
        {
            Txt.Note("Log in to a character to see its plates.");
            return;
        }
        Kit.BeginPanel("##plates", 6, 6, 6);
        var i = 0;
        // What you're wearing first: it can be kept as a look too.
        if (me.Outfit.Count > 0)
        {
            var worn = new Look("worn", "What you're wearing", me.Body, me.Outfit, "private", true, me.Name, 0, null);
            var match = PlateMatch(0, me.Outfit);
            if (LookRow("##worn", i++, worn, match != null ? $"Your look “{match.Name}”" : Words.Count(me.Outfit.Count, "piece")))
                Later(() => OpenPlate(0));
        }
        foreach (var plate in me.Plates.Where(p => p.Pieces.Count > 0).OrderBy(p => p.Plate))
        {
            var look = new Look($"plate{plate.Plate}", $"Plate {plate.Plate}", me.Body, plate.Pieces, "private", true, me.Name, 0, null);
            var match = PlateMatch(plate.Plate, plate.Pieces);
            var n = plate.Plate;
            if (LookRow($"##plate{n}", i++, look, match != null ? $"Your look “{match.Name}”" : Words.Count(plate.Pieces.Count, "piece")))
                Later(() => OpenPlate(n));
        }
        Kit.EndPanel();
        if (me.PlatesReadAt == null)
        {
            Kit.Gap(12);
            Txt.Note("Your plates show here once you've opened Glamour Plates at a glamour dresser (the game only has them then).");
        }
        else if (!me.Plates.Any(p => p.Pieces.Count > 0))
        {
            Kit.Gap(12);
            Txt.Note("All your plates are empty. Pick a look, open Glamour Plates at a glamour dresser, and put it on one.");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Your closet: what the plugin has read, and the site.

    private static readonly (Place Place, Icon Icon)[] PlaceIcons =
    {
        (Place.Dresser, Icon.Hanger), (Place.Armoire, Icon.Archive), (Place.Armoury, Icon.Swords), (Place.Inventory, Icon.Backpack),
        (Place.Saddlebag, Icon.Package), (Place.Retainer, Icon.Users),
    };

    private void DrawClosetTab()
    {
        var me = wardrobe.Current;
        if (me == null)
        {
            Txt.Note("Log in to a character to read its wardrobe.");
            return;
        }
        var dresserRead = me.Get(Place.Dresser) != null;
        Kit.Facts("##closet-facts", new List<Kit.Fact>
        {
            new("Dresser", dresserRead ? DresserCleanup.Used(me.Dresser).ToString() : "—", dresserRead ? $"of {DresserCleanup.Capacity}" : null),
            new("Armoire", me.Get(Place.Armoire)?.Items.Count.ToString() ?? "—"),
            new("Plates", me.PlatesReadAt != null ? me.Plates.Count(p => p.Pieces.Count > 0).ToString() : "—"),
        });
        if (dresserRead)
        {
            Kit.Gap(10);
            var roomRow = Kit.At;
            if (Kit.Button("##room", roomRow, "Make room in your dresser", Btn.Glass, BtnSize.Sm, Icon.Archive, 0, false,
                    "Outfits you could store as one, and pieces no look or plate of yours uses"))
                Later(() => OpenCleanup());
            Kit.Reserve(roomRow, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        }
        Kit.Gap(16);
        Kit.Section("Where your gear is", me.Name.Length > 0 ? me.Name : null);
        Kit.Gap(10);
        Kit.BeginPanel("##places", 6, 6, 6);
        var dl = Kit.Dl;
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < PlaceIcons.Length; i++)
        {
            var (place, icon) = PlaceIcons[i];
            var read = me.Get(place);
            var at = Kit.At;
            var w = Kit.Width;
            var nameH = Txt.Natural(Face.SansMedium, 14);
            var h = S(10) * 2 + MathF.Max(S(36), nameH + S(2) + S(19.5f));
            if (i > 0) dl.AddLine(new Vector2(at.X + S(10), at.Y - 0.5f), new Vector2(at.X + w - S(10), at.Y - 0.5f), Palette.U32(Palette.Line), 1f);
            var x = at.X + S(10);
            Kit.IconWell(new Vector2(x, at.Y + (h - S(36)) / 2), S(36), icon, quiet: read == null);
            x += S(36) + S(12);
            var tw = at.X + w - S(10) - x;
            var top = at.Y + (h - (nameH + S(2) + S(19.5f))) / 2;
            Txt.Line(dl, new Vector2(x, top), Places.Word(place), Face.SansMedium, 14, Palette.Ink);
            var line = read == null
                ? Places.HowToRead(place)
                : $"{Words.Count(read.Items.Count, "piece")} · read {Words.Ago(new DateTimeOffset(read.ReadAt, TimeSpan.Zero), now)}"
                  + (place == Place.Retainer ? $" · {Words.Count(me.Retainers.Count, "retainer")}" : string.Empty);
            Txt.Line(dl, new Vector2(x, top + nameH + S(2)), Txt.Fit(line, Face.Sans, 13, tw), Face.Sans, 13, read == null ? Palette.RoseText : Palette.Muted, 19.5f);
            Kit.Reserve(at, w, h);
        }
        Kit.EndPanel();

        Kit.Gap(18);
        DrawWishes(me);
        Kit.Section("On the site");
        Kit.Gap(10);
        if (!Linked)
        {
            Txt.Note(sync.TokenRefused
                ? "The site doesn't know this token any more. Link again in Settings → Account."
                : "Link your account and your closet on Glamour Studio keeps itself up to date: every piece you own, where it is, your plates and what you wear.");
            Kit.Gap(12);
            if (Kit.Button("##closet-link", Kit.At, "Link your account", Btn.Primary, BtnSize.Md, Icon.Link)) OpenSettings(SettingsTab.Account);
            Kit.Reserve(Kit.At, Kit.Width, Kit.ButtonHeight(BtnSize.Md));
            return;
        }
        var said = sync.SyncProblem
                   ?? (sync.LastSync is { } r ? Words.Synced(r) + (sync.LastSyncAt is { } t ? $", sent {Words.Ago(t, now)}." : ".")
                       : config.AutoSync ? "It's sent by itself as it changes." : "Sending by itself is off (Settings).");
        Txt.Note(said, sync.SyncProblem != null ? Palette.Danger : Palette.Muted);
        Kit.Gap(12);
        var row = Kit.At;
        var send = sync.Syncing ? "Sending…" : "Send it now";
        if (Kit.Button("##sync-now", row, send, Btn.Glass, BtnSize.Md, Icon.Upload, 0, sync.Syncing, "Sends what the plugin has read to your closet on the site"))
            sync.SyncNow();
        var x2 = row.X + Kit.ButtonWidth(send, BtnSize.Md, Icon.Upload) + S(8);
        if (Kit.Button("##open-closet", new Vector2(x2, row.Y), "Open my closet", Btn.Quiet, BtnSize.Md, Icon.ExternalLink))
            Dalamud.Utility.Util.OpenLink(SiteLink.ClosetUrl);
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Md));
    }

    // ---------------------------------------------------------------------------------------------
    // Your shopping list: where each piece is, if you have it now.

    private void DrawWishes(CharacterWardrobe me)
    {
        var wishes = sync.Library.Wishes;
        if (!Linked || wishes.Count == 0) return;
        Kit.Section("Your shopping list", Words.Count(wishes.Count, "piece"));
        Kit.Gap(10);
        Kit.BeginPanel("##wishes", 6, 6, 6);
        var dl = Kit.Dl;
        var i = 0;
        foreach (var id in wishes.Take(30))
        {
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(10) * 2 + S(32);
            if (i++ > 0) dl.AddLine(new Vector2(at.X + S(10), at.Y - 0.5f), new Vector2(at.X + w - S(10), at.Y - 0.5f), Palette.U32(Palette.Line), 1f);
            var item = data.Item(id);
            Kit.GameIcon(new Vector2(at.X + S(10), at.Y + S(10)), S(32), item?.Icon ?? 0);
            var where = Where(id);
            var chip = where is { } p ? Places.Word(p) : "Not yet";
            var chipW = Kit.ChipWidth(chip, where != null);
            var x = at.X + S(10) + S(32) + S(12);
            Txt.Line(dl, new Vector2(x, at.Y + S(10)), Txt.Fit(item?.Name ?? $"Item {id}", Face.SansMedium, 14, at.X + w - S(20) - chipW - x), Face.SansMedium, 14, Palette.Ink, 32);
            Kit.Chip(new Vector2(at.X + w - S(10) - chipW, at.Y + (h - S(24)) / 2), chip, where != null);
            Kit.Reserve(at, w, h);
        }
        Kit.EndPanel();
        Kit.Gap(8);
        Txt.Note("A piece the plugin sees in your wardrobe comes off the list by itself.", Palette.Faint);
        Kit.Gap(18);
    }
}

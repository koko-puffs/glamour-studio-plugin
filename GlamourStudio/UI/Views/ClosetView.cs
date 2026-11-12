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
/// Closet (the site's Closet → In the game, from the game's side): the glamour dresser first (places
/// used of 800 as a big number and a bar, free, what gathering outfit sets would free, Make room),
/// then each other place with how many pieces and when it was read, or what to open so it can be;
/// your closet on the site (what was sent and when, Send now, Open, and its other states); and in the
/// side pane your wishlist, each piece with the look it's for and where it is once it's yours. No
/// field and no sticker: nothing here is the one thing to do.
/// </summary>
public sealed partial class MainWindow
{
    private static readonly (Place Place, Icon Icon)[] ClosetPlaces =
    {
        (Place.Armoire, Icon.Archive), (Place.Armoury, Icon.Shield), (Place.Inventory, Icon.Bag),
        (Place.Saddlebag, Icon.Package), (Place.Retainer, Icon.Users),
    };

    /// <summary>The side pane's width (the mock's <c>.cv</c>: 380).</summary>
    private const float ClosetAsideCss = 380;

    private void DrawCloset(Vector2 min, Vector2 size)
    {
        var asideW = MathF.Min(S(ClosetAsideCss), size.X * 0.42f);
        var mainSize = new Vector2(size.X - asideW, size.Y);
        var me = wardrobe.Current;
        // The mock's .left: 18 above, 24 at the left, 20 at the right; its cards 14 apart.
        Kit.BeginPane("##closet-main", min, mainSize, SideCss, 18, 20, padRight: 20);
        if (me == null)
        {
            Kit.EmptyState("##closet-nobody", "Log in to a character and the plugin reads where its gear is.", "Not logged in", "your gear is your character's",
                heightCss: MathF.Max(240, Kit.PaneRoom() / S(1)));
        }
        else
        {
            DrawDresserCard(me);
            Kit.Gap(14);
            DrawPlaceList(me);
            Kit.Gap(14);
            DrawSiteCard(me, size.X < S(1000));
        }
        Kit.EndPane();

        var asideMin = new Vector2(min.X + mainSize.X, min.Y);
        var dl = Kit.Dl;
        dl.AddRectFilled(asideMin, asideMin + new Vector2(asideW, size.Y), Palette.U32(Palette.Ground2), S(14), ImDrawFlags.RoundCornersBottomRight);
        dl.AddLine(new Vector2(asideMin.X + 0.5f, asideMin.Y), new Vector2(asideMin.X + 0.5f, asideMin.Y + size.Y), Palette.U32(Palette.Line), 1f);
        Kit.BeginPane("##closet-aside", asideMin + new Vector2(1, 0), new Vector2(asideW - 1, size.Y), 20, 18, 18, scroll: false);
        DrawWishlist();
        Kit.EndPane();
    }

    // ---------------------------------------------------------------------------------------------
    // The dresser.

    /// <summary>
    /// The glamour dresser (the mock's first card): its name and when it was read (always: the game
    /// has it only at a dresser), Make room; places used as a big number, of 800 and free, what
    /// gathering outfit sets would free (amber: something to see to), and the bar.
    /// </summary>
    private void DrawDresserCard(CharacterWardrobe me)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var read = me.Get(Place.Dresser);
        var padX = S(20);
        var padY = S(18);
        var inner = w - padX * 2;
        var bh = Kit.ButtonHeight(BtnSize.Sm);
        var bigH = S(40);
        var h = read == null
            ? padY * 2 + bh + S(8) + Txt.Natural(Face.Sans, 12)
            : padY * 2 + bh + S(12) + bigH + S(10) + S(8);
        Kit.Card(at, at + new Vector2(w, h), read == null ? Vector4.Zero : Palette.Card, read == null ? Palette.LineStrong : Palette.Line, 18, dashed: read == null);
        var x = at.X + padX;
        var y = at.Y + padY;

        // The head row: the hanger, GLAMOUR DRESSER, read when; Make room at the right.
        Icons.Draw(dl, Icon.Hanger, new Vector2(x, y + (bh - S(18)) / 2), S(18), read == null ? Palette.Faint : Palette.Accent);
        var tx = x + S(18) + S(8);
        const string room = "Make room";
        var rw = Kit.ButtonWidth(room, BtnSize.Sm, Icon.Package);
        if (Kit.Button("##closet-room", new Vector2(x + inner - rw, y), room, Btn.Glass, BtnSize.Sm, Icon.Package, 0, false, "How full the dresser is, and Make room for me", 0, 14))
            Later(() => Go(Screen.MakeRoom));
        var tw = Kit.Display(new Vector2(tx, y + (bh - Kit.DisplayHeight(17)) / 2), "Glamour dresser", 17, Palette.Ink, x + inner - rw - S(16) - tx);
        if (read == null)
        {
            Txt.Draw(dl, new Vector2(x, y + bh + S(8)), Txt.Fit(PlaceNote(me, Place.Dresser, DateTimeOffset.UtcNow).Note, Face.Sans, 12, inner), Face.Sans, 12, Palette.Amber);
            Kit.Reserve(at, w, h);
            return;
        }
        var ago = $"read {Words.Ago(new DateTimeOffset(read.ReadAt, TimeSpan.Zero), DateTimeOffset.UtcNow)}";
        var agoRoom = x + inner - rw - S(16) - (tx + tw + S(8));
        if (agoRoom > S(30))
            Txt.Draw(dl, new Vector2(tx + tw + S(8), y + (bh - Txt.Natural(Face.Sans, 12)) / 2), Txt.Fit(ago, Face.Sans, 12, agoRoom), Face.Sans, 12, Palette.Muted);
        y += bh + S(12);

        // 612 · of 800 places used · 188 free … gathering outfit sets frees up to 23, on one baseline.
        var used = DresserUsed(me);
        var free = Math.Max(0, DresserCleanup.Capacity - used);
        var bigText = used.ToString("N0");
        var baseline = y + Txt.Ascent(Face.Display, 40) - S(40) * 0.07f;
        var bw = Kit.Display(new Vector2(x, y - S(40) * 0.07f - (Kit.DisplayHeight(40) - bigH) / 2), bigText, 40, Palette.Ink, 0, false, 0.01f);
        var ofText = $"of {DresserCleanup.Capacity} places used · {free:N0} free";
        var ofW = Txt.Draw(dl, new Vector2(x + bw + S(10), baseline - Txt.Ascent(Face.Sans, 13) - (Kit.DisplayHeight(40) - bigH) / 2), ofText, Face.Sans, 13, Palette.Muted);
        var now = CleanupNow(me);
        if (now.Frees > 0)
        {
            var gather = $"gathering outfit sets frees up to {now.Frees}";
            var gw = Txt.Measure(gather, Face.Sans, 12).X;
            var gx = x + inner - gw;
            if (gx > x + bw + S(10) + ofW + S(10))
                Txt.Draw(dl, new Vector2(gx, baseline - Txt.Ascent(Face.Sans, 12) - (Kit.DisplayHeight(40) - bigH) / 2), gather, Face.Sans, 12, Palette.Amber);
            else if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(x, y), new Vector2(x + inner, y + bigH)))
                Kit.Tooltip(gather);
        }
        y += bigH + S(10);
        Kit.Meter(new Vector2(x, y), inner, (float)used / DresserCleanup.Capacity, MeterKind.Ice, large: true);
        Kit.Reserve(at, w, h);
    }

    // ---------------------------------------------------------------------------------------------
    // The other places.

    private (DateTime At, List<(ulong Id, string Name)> All) retainerList = (DateTime.MinValue, new List<(ulong, string)>());

    /// <summary>Your retainers as the game has them (read again at most every two seconds: it's asked every frame).</summary>
    private List<(ulong Id, string Name)> RetainersNow()
    {
        if (DateTime.UtcNow - retainerList.At > TimeSpan.FromSeconds(2)) retainerList = (DateTime.UtcNow, GameWindows.Retainers());
        return retainerList.All;
    }

    /// <summary>
    /// Each place and when it was read (the mock's list card): its icon on the tile, its name, when it
    /// was read (or dashed, with what to open so it can be, in amber), and how many pieces in Geist Mono.
    /// </summary>
    private void DrawPlaceList(CharacterWardrobe me)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var rowH = S(50);
        var h = S(4) * 2 + rowH * ClosetPlaces.Length;
        Kit.Card(at, at + new Vector2(w, h), Palette.Card, Palette.Line, 18);
        var x = at.X + S(18);
        var inner = w - S(36);
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < ClosetPlaces.Length; i++)
        {
            var (place, icon) = ClosetPlaces[i];
            var y = at.Y + S(4) + rowH * i;
            if (i > 0) dl.AddLine(new Vector2(x, y + 0.5f), new Vector2(x + inner, y + 0.5f), Palette.U32(Palette.Line), 1f);
            var read = me.Get(place);
            var pi = new Vector2(x, y + (rowH - S(34)) / 2);
            if (read == null) Kit.Dashed(pi, pi + new Vector2(S(34)), S(10), Palette.LineStrong, 1f);
            else dl.AddRectFilled(pi, pi + new Vector2(S(34)), Palette.U32(Palette.Tile), S(10));
            Icons.Draw(dl, icon, pi + new Vector2(S(9)), S(16), read == null ? Palette.Faint : Palette.Accent);
            var cntW = S(70);
            var tx = x + S(34) + S(12);
            var tw = x + inner - cntW - S(12) - tx;
            var nh = Txt.Natural(Face.SansSemibold, 13);
            var lh = Txt.Natural(Face.Sans, 12);
            var ty = y + (rowH - nh - lh) / 2;
            Txt.Draw(dl, new Vector2(tx, ty), Txt.Fit(Places.Word(place), Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink);
            var (note, amber) = PlaceNote(me, place, now);
            Txt.Draw(dl, new Vector2(tx, ty + nh), Txt.Fit(note, Face.Sans, 12, tw), Face.Sans, 12, amber ? Palette.Amber : Palette.Muted);
            if (Txt.Measure(note, Face.Sans, 12).X > tw && !Kit.Blocked && ImGui.IsWindowHovered()
                && ImGui.IsMouseHoveringRect(new Vector2(tx, y), new Vector2(tx + tw, y + rowH)))
                Kit.Tooltip(note);
            var count = read == null ? "—" : read.Items.Count.ToString("N0");
            var cw = Txt.Measure(count, Face.MonoMedium, 13).X;
            Txt.Draw(dl, new Vector2(x + inner - cw, y + (rowH - Txt.Natural(Face.MonoMedium, 13)) / 2), count, Face.MonoMedium, 13, read == null ? Palette.Faint : Palette.Ink);
        }
        Kit.Reserve(at, w, h);
    }

    /// <summary>A place's line: when it was read, or what to open so it can be (amber).</summary>
    private (string Note, bool Amber) PlaceNote(CharacterWardrobe me, Place place, DateTimeOffset now)
    {
        var read = me.Get(place);
        if (read == null)
        {
            var how = Places.HowToRead(place);
            return ($"not read yet: {char.ToLowerInvariant(how[0])}{how[1..]}", true);
        }
        var ago = Words.Ago(new DateTimeOffset(read.ReadAt, TimeSpan.Zero), now);
        if (place != Place.Retainer) return ($"read {ago}", false);
        // The retainers the game knows of (once a bell's been used), and whose bags aren't read yet.
        var all = RetainersNow();
        if (all.Count == 0) return ($"{Words.Count(me.Retainers.Count, "retainer")} read · last {ago}", false);
        var readCount = 0;
        string? unread = null;
        var unreadCount = 0;
        foreach (var (id, name) in all)
        {
            if (me.Retainers.ContainsKey(id.ToString())) readCount++;
            else
            {
                unread ??= name;
                unreadCount++;
            }
        }
        if (unreadCount == 0) return ($"all {all.Count} read · last {ago}", false);
        return ($"{readCount} of {all.Count} read · {(unreadCount == 1 ? $"{unread}'s bags" : $"{unreadCount} more")} not yet", false);
    }

    // ---------------------------------------------------------------------------------------------
    // The site.

    private readonly Run[] siteLines = new Run[1];

    /// <summary>
    /// Your closet on Glamour Studio (the mock's third card, and its states): what was sent and when,
    /// Send now and Open (under 1000 wide, "Send" and Open as an icon); sending, with a bar; a problem
    /// said as one, with Try again; sending by itself off; this character not asked yet (Ask me) or
    /// said no; not linked (dashed, the way to link).
    /// </summary>
    private void DrawSiteCard(CharacterWardrobe me, bool narrow)
    {
        var step = sync.StepFor(me);
        var now = DateTimeOffset.UtcNow;
        // What it says, in which colours, and what can be pressed.
        Icon lead;
        Vector4 leadColor;
        string? eyebrow;
        var eyebrowColor = Palette.Accent;
        string head;
        string line;
        var dashed = false;
        var bad = false;
        var face = false;
        var meter = false;
        if (!Linked)
        {
            dashed = true;
            lead = Icon.Key;
            leadColor = Palette.Faint;
            eyebrow = null;
            head = sync.TokenRefused ? "The site doesn't know this token any more." : "Link your account and your closet on the site keeps itself up to date.";
            line = sync.TokenRefused ? "Link again in Settings → Account: nothing's sent meanwhile." : "The wishlist shows here too, each piece with where it is once you have it.";
        }
        else if (sync.Syncing)
        {
            lead = Icon.Globe;
            leadColor = Palette.Accent;
            eyebrow = "Your closet on Glamour Studio";
            head = "Sending your closet…";
            line = string.Empty;
            meter = true;
        }
        else if (sync.SyncProblem is { } problem)
        {
            bad = true;
            lead = Icon.AlertTriangle;
            leadColor = Palette.Danger;
            eyebrow = "Closet not sent";
            eyebrowColor = Palette.Danger;
            head = problem;
            line = config.AutoSync ? "Your closet is safe here; it's sent again in a few minutes, or now." : "Your closet is safe here; it's sent when you try again.";
        }
        else if (step == ConsentStep.AskPlayer)
        {
            face = true;
            lead = Icon.User;
            leadColor = Palette.Accent;
            eyebrow = "Not sent yet";
            head = $"{(me.Name.Length > 0 ? me.Name : "This character")} isn't sent to the site: say yes or no";
            line = "Nothing of this character is sent until you say yes.";
        }
        else if (step is ConsentStep.Hold or ConsentStep.TellNo)
        {
            face = true;
            lead = Icon.User;
            leadColor = Palette.Faint;
            eyebrow = "Not sent";
            eyebrowColor = Palette.Faint;
            head = $"{(me.Name.Length > 0 ? me.Name : "This character")} isn't sent to the site";
            line = "You said no for this character: nothing of it is sent (Settings → Account to change it).";
        }
        else
        {
            var off = !config.AutoSync;
            lead = Icon.Globe;
            leadColor = off ? Palette.Faint : Palette.Accent;
            eyebrow = "Your closet on Glamour Studio";
            eyebrowColor = off ? Palette.Faint : Palette.Accent;
            head = sync.LastSync is { } r
                ? $"{Words.Count(r.Total, "piece")}, sent {Words.Ago(sync.LastSyncAt ?? now, now)}" + (r.Added > 0 ? $" ({r.Added:N0} new)" : string.Empty)
                : "Not sent yet since the plugin loaded";
            line = off ? "Sending by itself is off (Settings): it's sent only when you press Send now." : "It's sent by itself as it changes, every few minutes at most.";
        }

        // The buttons, right to left, as wide as they need (under 1000 wide: "Send", Open as an icon).
        string? main = null;
        Icon? mainIcon = null;
        var mainOff = false;
        string? mainTip = null;
        var open = false;
        if (!Linked)
        {
            main = sync.TokenRefused ? "Link again" : "Link your account";
        }
        else if (sync.Syncing)
        {
            main = "Sending…";
            mainIcon = Icon.Send;
            mainOff = true;
            open = true;
        }
        else if (sync.SyncProblem != null)
        {
            main = "Try again";
            mainIcon = Icon.RefreshCw;
            mainTip = "Sends what the plugin has read to your closet on the site";
        }
        else if (step == ConsentStep.AskPlayer)
        {
            main = "Ask me";
            mainTip = "Asks again whether this character's closet may go to the site";
        }
        else if (step is not (ConsentStep.Hold or ConsentStep.TellNo))
        {
            main = narrow ? "Send" : "Send now";
            mainIcon = Icon.Send;
            mainTip = "Sends what the plugin has read to your closet on the site";
            open = config.AutoSync;
        }

        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var padX = S(20);
        var padY = S(18);
        var bh = Kit.ButtonHeight(BtnSize.Sm);
        var openW = open ? narrow ? S(34) : Kit.ButtonWidth("Open", BtnSize.Sm, Icon.ExternalLink, Btn.Quiet) : 0;
        var mainW = main != null ? Kit.ButtonWidth(main, BtnSize.Sm, mainIcon) : 0;
        var buttonsW = mainW + (open ? S(14) + openW : 0);
        var leadW = face ? S(34) : S(20);
        var tx = at.X + padX + leadW + S(14);
        var tw = at.X + w - padX - (buttonsW > 0 ? buttonsW + S(14) : 0) - tx;

        // The words' column: eyebrow, the line in ink 600, the small line (or the bar).
        var ebH = eyebrow != null ? Kit.EyebrowHeight + S(4) : 0;
        siteLines[0] = new Run(head, Face.SansSemibold, 13, Palette.Ink);
        var headH = Txt.Flow(Vector2.Zero, tw, siteLines, 17, out _, draw: false);
        siteLines[0] = new Run(line, Face.Sans, 12, Palette.Muted);
        var lineH = meter ? S(6) : line.Length > 0 ? Txt.Flow(Vector2.Zero, tw, siteLines, 16, out _, draw: false) : 0;
        var colH = ebH + headH + (lineH > 0 ? S(4) + lineH : 0);
        var innerH = MathF.Max(colH, MathF.Max(bh, leadW));
        var h = padY * 2 + innerH;
        var edge = bad ? Palette.WithAlpha(Palette.Danger, 0.45f) : dashed ? Palette.LineStrong : Palette.Line;
        Kit.Card(at, at + new Vector2(w, h), dashed ? Vector4.Zero : Palette.Card, edge, 18, dashed);
        // (A problem and the not-asked card hang from the top, as the mock's .row.top; the rest are centred.)
        var top = bad || face;
        var cy = at.Y + padY + innerH / 2;
        var ly = top ? at.Y + padY : cy - leadW / 2;
        if (face) Kit.Avatar(new Vector2(at.X + padX + S(17), ly + S(17)), InitialsOf(me.Name), 34, step != ConsentStep.AskPlayer);
        else Icons.Draw(dl, lead, new Vector2(at.X + padX, ly), S(20), leadColor);
        var y = top ? at.Y + padY : cy - colH / 2;
        if (eyebrow != null)
        {
            Kit.Eyebrow(new Vector2(tx, y), eyebrow, eyebrowColor, tw);
            y += Kit.EyebrowHeight + S(4);
        }
        siteLines[0] = new Run(head, Face.SansSemibold, 13, Palette.Ink);
        y += Txt.Flow(new Vector2(tx, y), tw, siteLines, 17, out _);
        if (meter)
        {
            // The site doesn't say how far it's got: a stretch of violet going round the track.
            y += S(4);
            var mw = MathF.Min(S(220), tw);
            dl.AddRectFilled(new Vector2(tx, y), new Vector2(tx + mw, y + S(6)), Palette.U32(Palette.Tile), S(3));
            var t = ReducedMotion ? 0.3f : (float)(ImGui.GetTime() * 0.6 % 1.0);
            var seg = mw * 0.45f;
            var sx = tx + (mw - seg) * (0.5f - 0.5f * MathF.Cos(t * MathF.PI * 2));
            dl.AddRectFilled(new Vector2(sx, y), new Vector2(sx + seg, y + S(6)), Palette.U32(Palette.Field), S(3));
        }
        else if (line.Length > 0)
        {
            y += S(4);
            siteLines[0] = new Run(line, Face.Sans, 12, Palette.Muted);
            Txt.Flow(new Vector2(tx, y), tw, siteLines, 16, out _);
        }

        // The buttons.
        var by = top ? at.Y + padY : cy - bh / 2;
        var bx = at.X + w - padX - buttonsW;
        if (main != null)
        {
            if (Kit.Button("##site-main", new Vector2(bx, by), main, Btn.Glass, BtnSize.Sm, mainIcon, 0, mainOff, mainTip, 0, 14))
            {
                if (!Linked) Later(() => OpenSettings(SettingsTab.Account));
                else sync.SyncNow();
            }
            bx += mainW + S(14);
        }
        if (open)
        {
            var pressed = narrow
                ? Kit.IconButton("##open-closet", new Vector2(bx, by), Icon.ExternalLink, "Your closet on Glamour Studio", 34, Btn.Quiet, false, 14)
                : Kit.Button("##open-closet", new Vector2(bx, by), "Open", Btn.Quiet, BtnSize.Sm, Icon.ExternalLink, 0, false, "Your closet on Glamour Studio", 0, 14);
            if (pressed) Dalamud.Utility.Util.OpenLink(SiteLink.ClosetUrl);
        }
        Kit.Reserve(at, w, h);
    }

    // ---------------------------------------------------------------------------------------------
    // Your wishlist.

    private (int Sync, Dictionary<uint, (string Look, string? Dyes)> For)? wishLooks;

    /// <summary>For each piece on your list, a look of yours (or one you love, or could wear) that has it, and the dyes it has there.</summary>
    private Dictionary<uint, (string Look, string? Dyes)> WishLooks()
    {
        if (wishLooks is { } c && c.Sync == sync.Version) return c.For;
        var map = new Dictionary<uint, (string, string?)>();
        foreach (var l in sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Wearable))
            foreach (var p in l.Pieces.Values)
                map.TryAdd(LookJson.BaseId(p.Item), (l.Name, DyeLine(p)));
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

    private readonly Run[] wishLine = new Run[1];

    /// <summary>
    /// Your wishlist (the side pane): its heading and count, a wish come true (the holo sparkle and the
    /// hand voice: which pieces came off the list), and each piece with a lavender dot, the look it's
    /// for and its dyes there, "Yours now" in ice with where it is, and × to take it off; the list
    /// scrolls on its own, the note under it stays.
    /// </summary>
    private void DrawWishlist()
    {
        var wishes = sync.Library.Wishes;
        Kit.SectionHead("##wish-head", "Your wishlist", Linked ? wishes.Count.ToString() : null);
        if (!Linked)
        {
            Txt.Para(new[] { new Run("Link your account: the pieces on your wishlist on the site show here, each with where it is once you have it.", Face.Sans, 13, Palette.Muted) }, 19.5f);
            return;
        }
        var w = Kit.Width;
        var dl = Kit.Dl;
        if (sync.LastWishesGot is { } got && got.At > closedAt && got.Items.Count > 0)
        {
            // A wish come true is a little joy: the sparkle (special), the hand voice, which came off. It
            // stays until the window closes (one come true while it was shut shows when it opens).
            var a = Kit.At;
            var padX = S(13);
            var padY = S(11);
            var tx = a.X + padX + S(16) + S(10);
            var tw = a.X + w - padX - tx;
            var names = Words.List(got.Items.Take(3).Select(data.ItemName)) + (got.Items.Count > 3 ? $" and {got.Items.Count - 3} more" : string.Empty);
            wishLine[0] = new Run($"{Words.Ago(new DateTimeOffset(got.At, TimeSpan.Zero), DateTimeOffset.UtcNow)}: {names}, seen in your wardrobe. {(got.Items.Count == 1 ? "It's" : "They're")} off the list.",
                Face.Sans, 12, Palette.Muted);
            var handH = S(13 * 1.3f);
            var lineH = Txt.Flow(Vector2.Zero, tw, wishLine, 16, out _, draw: false);
            var h = padY * 2 + handH + S(4) + lineH;
            Kit.Card(a, a + new Vector2(w, h), Palette.Card, Palette.AccentLine, 18);
            Kit.Spark(new Vector2(a.X + padX, a.Y + padY + S(4)), 16);
            Kit.Hand(new Vector2(tx, a.Y + padY), got.Items.Count == 1 ? "a wish came true!" : "wishes came true!", 13);
            Txt.Flow(new Vector2(tx, a.Y + padY + handH + S(4)), tw, wishLine, 16, out _);
            Kit.Reserve(a, w, h);
            Kit.Gap(10);
        }
        if (wishes.Count == 0)
        {
            Kit.EmptyState("##wish-empty", "On the site, a look's pieces you don't have yet can go on your list.", "Nothing on it", "wish for something");
            return;
        }
        var footH = Txt.Natural(Face.Sans, 12) * 2 + S(8);
        var listH = MathF.Max(S(80), Kit.PaneRoom() - footH);
        var at = Kit.At;
        Kit.BeginPane("##wishes", at, new Vector2(w, listH), 0, 0, 0);
        var forLook = WishLooks();
        var first = true;
        foreach (var id in WishesShown(wishes))
        {
            var r = Kit.At;
            var rw = Kit.Width;
            var h = S(52);
            if (!ImGui.IsRectVisible(r, r + new Vector2(rw, h)))
            {
                // (As tall as a drawn row, so nothing moves as rows scroll in and out.)
                Kit.Reserve(r, rw, h);
                first = false;
                continue;
            }
            dl = Kit.Dl;
            if (!first) dl.AddLine(new Vector2(r.X, r.Y + 0.5f), new Vector2(r.X + rw, r.Y + 0.5f), Palette.U32(Palette.Line), 1f);
            first = false;
            var item = data.Item(id);
            var where = Where(id);
            Kit.Item(new Vector2(r.X, r.Y + (h - S(36)) / 2), 36, item?.Icon ?? 0, where != null ? Own.Ice : Own.Star, false, Palette.Ground2);
            var xw = S(28);
            var x = r.X + S(36) + S(11);
            var tw = r.X + rw - xw - S(11) - x;
            var nh = Txt.Natural(Face.SansSemibold, 13);
            var lh = Txt.Natural(Face.Sans, 12);
            var y = r.Y + (h - nh - lh) / 2;
            Txt.Draw(dl, new Vector2(x, y), Txt.Fit(item?.Name ?? $"Item {id}", Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink);
            string line;
            if (where is { } place) line = $"Yours now · {Places.Where(place)}";
            else if (forLook.TryGetValue(id, out var f)) line = f.Dyes != null ? $"For {f.Look} · {f.Dyes}" : $"For {f.Look}";
            else line = "On your wishlist";
            Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(line, Face.Sans, 12, tw), Face.Sans, 12, where != null ? Palette.Ice : Palette.Muted);
            var gone = id;
            if (Kit.IconButton($"##unwish-{id}", new Vector2(r.X + rw - xw, r.Y + (h - xw) / 2), Icon.X, "Take it off the list", 28, Btn.Quiet, false, 14))
                Later(() => Unwish(gone));
            Kit.Reserve(r, rw, h);
        }
        Kit.EndPane();
        Kit.Reserve(at, w, listH);
        Kit.Gap(8);
        Txt.Para(new[] { new Run("A piece the plugin sees in your wardrobe comes off the list by itself; × takes one off.", Face.Sans, 12, Palette.Faint) }, 16);
    }
}

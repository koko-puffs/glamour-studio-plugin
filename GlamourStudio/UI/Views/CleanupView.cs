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
/// Make room (the dresser's number is the moment): on the field, places used of 800 in heavy
/// capitals, the bar, what's free and what gathering would free, with Make room for me (the sticker:
/// it asks first, and does one set the first time). While it works the field shows its progress and
/// Stop; when it's done, what it made. Below, two columns: Keep as outfits (each set, what freeing it
/// frees; each set's progress during a run), Not in any look or plate, and the duplicates.
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
        var outfits = DresserCleanup.Outfits(me.Dresser, data.AllSets, DresserPlaces.ItemsInBags());
        var plans = Enumerable.Range(1, Plates.Count).Select(sync.PlanFor).OfType<Look>();
        var used = sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Inbox.Select(s => s.Look)).Concat(plans)
            .Select(l => l.Pieces)
            .Concat(me.Plates.Select(p => (IReadOnlyDictionary<Slot, Piece>)p.Pieces))
            .Append(me.Outfit);
        var sets = outfits.Select(o => new CleanupRow(o.SetItem, SetLine(o), o.Frees > 0 ? $"Frees {o.Frees}" : "Tidier", o.Frees > 0)).ToList();
        var unused = DresserCleanup.Unused(me.Dresser, used).Select(d =>
        {
            var item = data.Item(d.Item);
            var slot = item?.Slots.Length > 0 ? Slots.Word(item.Slots[0]) : "Gear";
            var dyes = DyeLine(new Piece(d.Item, d.Dye1, d.Dye2));
            return new CleanupRow(d.Item, $"{slot} · {dyes ?? "undyed"}", null, false);
        }).ToList();
        var read = new CleanupNowRead(DresserCleanup.Used(me.Dresser), DresserCleanup.Frees(outfits), sets, unused);
        cleanup = (wardrobe.Version, sync.Version, me.Key, read);
        return read;
    }

    /// <summary>
    /// A set's line (the mock's): how many of its pieces are loose, joining the stored set, in your
    /// bags, high quality, or dyed (a dyed copy loses its dye).
    /// </summary>
    private string SetLine(OutfitChance o)
    {
        var n = o.Pieces.Count;
        var dyed = o.Pieces.Count(p => p.Dye1 != 0 || p.Dye2 != 0);
        var extras = new List<string>();
        if (o.FromBags > 0) extras.Add($"{o.FromBags} in your bags");
        if (o.Hq) extras.Add("high quality");
        if (dyed > 0) extras.Add(dyed == n ? n == 1 ? "it's dyed" : $"all {n} dyed" : $"{dyed} of them dyed");
        if (o.Stored)
            return $"The set's in your dresser: {Words.Count(n, "piece")} join{(n == 1 ? "s" : string.Empty)} it" + (extras.Count > 0 ? " · " + string.Join(" · ", extras) : string.Empty);
        var total = data.SetPieces(o.SetItem).Count;
        return extras.Count == 0
            ? $"{n} of {total} pieces loose in your dresser"
            : $"{n} of {total} loose · {string.Join(" · ", extras)}";
    }

    /// <summary>Your looks are read (so a piece in none of them is truly unused).</summary>
    private bool LooksKnown => Linked && sync.LibraryAt != null;

    /// <summary>Not in any look or plate shows all its pieces (else as many as sit beside the sets).</summary>
    private bool unusedAll;

    private void DrawCleanup(Vector2 min, Vector2 size)
    {
        // (The page scrolls: the lists are as long as they are.)
        Kit.BeginPane("##room", min, size, SideCss, PageTopCss, PageTopCss);
        var me = wardrobe.Current;
        if (me == null || me.Get(Place.Dresser) == null)
        {
            Kit.EmptyState("##room-unread",
                me == null ? "Log in to a character, open its glamour dresser once, and the plugin knows what's in it." : "Open your glamour dresser once (at an inn, or a dresser in a house) and the plugin knows what's in it.",
                "Not read yet", "the game has it only at a dresser", heightCss: MathF.Max(240, Kit.PaneRoom() / S(1)));
            Kit.EndPane();
            return;
        }
        var run = RunHere(me);
        if (run != null) FollowRun(run);
        var now = CleanupNow(me);
        var (plan, dupes) = AutoRoomNow(me);
        var have = OutfitStorer.Supplies(data);
        DrawRoomField(me, plan, have, run);
        Kit.Gap(18);

        var storing = run is { Dupes: false, Words: null } && OutfitStorer.Busy;
        var takingOut = run is { Dupes: true, Words: null } && DresserTaker.Busy;
        var warn = run is { Words: not null } r && (!r.Ok || r.Rows.Any(x => x == RunRow.Left));
        if (storing || takingOut)
        {
            TwoColumns(
                () => SetsCard(now, storing ? run : null),
                () => WhileItWorks(takingOut));
        }
        else if (warn)
        {
            TwoColumns(() => RunWarnCard(run!), () => DupesCard(me, dupes, have));
            Kit.Gap(18);
            TwoColumns(() => SetsCard(now, null), () => UnusedCard(now, 0));
        }
        else
        {
            var leftH = 0f;
            TwoColumns(
                () =>
                {
                    var top = Kit.At.Y;
                    SetsCard(now, null);
                    leftH = Kit.At.Y - top;
                },
                () =>
                {
                    UnusedCard(now, leftH - S(14) - DupesCardHeight);
                    Kit.Gap(14);
                    DupesCard(me, dupes, have);
                });
        }
        Kit.EndPane();
    }

    /// <summary>Two equal columns 18 apart (the mock's .cols2), the cursor under the taller.</summary>
    private static void TwoColumns(Action left, Action right) => Kit.Columns(0.5f, 18, left, right);

    // ---------------------------------------------------------------------------------------------
    // The field.

    /// <summary>The echo's words, long enough for any field, and the rows cut from them (made once a width).</summary>
    private const string EchoMakeRoom = "MAKE ROOM · MAKE ROOM · MAKE ROOM · MAKE ROOM · MAKE ROOM · MAKE ROOM · MAKE ROOM · ";

    private const string EchoStoring = "STORING · STORING · STORING · STORING · STORING · STORING · STORING · STORING · ";

    private const string EchoTaking = "TAKING OUT · TAKING OUT · TAKING OUT · TAKING OUT · TAKING OUT · TAKING OUT · ";

    private readonly Dictionary<(string, int, int), string> echoCuts = new();

    private readonly EchoRow[] roomEcho = new EchoRow[3];

    /// <summary>A row of the echo: <paramref name="words"/> from <paramref name="from"/>, as many letters as reach past the field's right edge.</summary>
    private string EchoCut(string words, int from, float xCss, float fieldCss)
    {
        // Bricolage's capitals at 104 are about 64px apart; a few more so the row runs off the edge.
        var chars = Math.Clamp((int)MathF.Ceiling((fieldCss - xCss) / 56f) + 2, 1, words.Length - from);
        if (echoCuts.TryGetValue((words, from, chars), out var cut)) return cut;
        if (echoCuts.Count >= 64) echoCuts.Clear();
        return echoCuts[(words, from, chars)] = words.Substring(from, chars).TrimEnd();
    }

    /// <summary>
    /// The field (the mock's .mr-field: 206 tall, 22 and 26 in, the right column 330): the echo, then
    /// at the left the eyebrow, the big number and the bar and a line; at the right the sticker and the
    /// hand voice, or Stop while it works. Idle, working, or done.
    /// </summary>
    private void DrawRoomField(CharacterWardrobe me, AutoRoomPlan plan, (int Prisms, int FreeBag) have, RoomRun? run)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(206);
        var min = at;
        var max = at + new Vector2(w, h);
        var storing = run is { Dupes: false, Words: null } && OutfitStorer.Busy;
        var takingOut = run is { Dupes: true, Words: null } && DresserTaker.Busy;
        var done = !storing && !takingOut && run is { Words: not null };
        var dl = Kit.Dl;
        using (Kit.FieldPanel(min, max))
        {
            // The echo: "Make room ·" (three rows idle, two when it's at work or done), as the mock places them.
            var fieldCss = w / S(1);
            var words = storing ? EchoStoring : takingOut ? EchoTaking : EchoMakeRoom;
            roomEcho[0] = new EchoRow(EchoCut(words, 0, 460, fieldCss), 460, -6);
            roomEcho[1] = new EchoRow(EchoCut(words, 0, 200, fieldCss), 200, -6 + 104 * 0.92f);
            var rows = 2;
            if (!storing && !takingOut && !done)
            {
                roomEcho[2] = new EchoRow(EchoCut(words, 5, 520, fieldCss), 520, -6 + 104 * 0.92f * 2);
                rows = 3;
            }
            Kit.Echo(min, max, Palette.RadiusXl, roomEcho.AsSpan(0, rows), 104);

            var padX = S(26);
            var padY = S(22);
            var inner = w - padX * 2;
            var rightW = MathF.Min(S(330), inner * 0.4f);
            var leftW = inner - rightW - S(26);
            var x = min.X + padX;
            var y = min.Y + padY;
            var used = DresserUsed(me);
            var free = Math.Max(0, DresserCleanup.Capacity - used);

            // The left: eyebrow, the number, the bar, the line.
            string eyebrow;
            Icon eyebrowIcon;
            string big;
            string small;
            float fraction;
            string line;
            if (storing)
            {
                var finished = run!.Seen;
                eyebrow = "Storing sets for you · keep the dresser open";
                eyebrowIcon = Icon.Package;
                big = finished.ToString();
                small = $" of {Words.Count(run.Total, "set")}";
                fraction = run.Total > 0 ? (float)finished / run.Total : 0;
                var atWork = finished < run.Jobs.Count ? data.ItemName(run.Jobs[finished].SetItem) : null;
                line = $"{Words.Count(OutfitStorer.PlacesFreed, "place")} freed so far" + (atWork != null ? $" · now: {atWork}" : string.Empty);
            }
            else if (takingOut)
            {
                var outNow = DresserTaker.Done;
                var total = Math.Max(run!.Total, outNow + DresserTaker.Left);
                eyebrow = "Taking out duplicates · keep the dresser open";
                eyebrowIcon = Icon.Trash;
                big = outNow.ToString();
                small = $" of {total} out";
                fraction = total > 0 ? (float)outNow / total : 0;
                line = $"Taking out duplicates… {outNow} out, {DresserTaker.Left} to go";
            }
            else if (done && run!.Dupes)
            {
                eyebrow = run.Ok ? "Took out the duplicates" : "Taking out stopped";
                eyebrowIcon = run.Ok ? Icon.Check : Icon.AlertTriangle;
                big = used.ToString("N0", En);
                small = $" / {DresserCleanup.Capacity}";
                fraction = (float)used / DresserCleanup.Capacity;
                line = $"{free.ToString("N0", En)} free · they're in your bags: sell or discard them yourself";
            }
            else if (done)
            {
                eyebrow = run!.Stored > 0 ? $"Made room · {Words.Count(run.PlacesFreed, "place")} freed" : "Made no room";
                eyebrowIcon = run.Ok ? Icon.Check : Icon.AlertTriangle;
                big = used.ToString("N0", En);
                small = $" / {DresserCleanup.Capacity}";
                fraction = (float)used / DresserCleanup.Capacity;
                line = $"{free.ToString("N0", En)} free · {run.Stored} of {Words.Count(run.Total, "set")} stored";
            }
            else
            {
                var read = me.Get(Place.Dresser);
                eyebrow = $"Glamour dresser · read {(read != null ? Words.Ago(new DateTimeOffset(read.ReadAt, TimeSpan.Zero), DateTimeOffset.UtcNow) : "never")}";
                eyebrowIcon = Icon.Hanger;
                big = used.ToString("N0", En);
                small = $" / {DresserCleanup.Capacity}";
                fraction = (float)used / DresserCleanup.Capacity;
                line = plan.Jobs.Count > 0
                    ? $"{free.ToString("N0", En)} free · gathering {Words.Count(plan.Jobs.Count, "outfit set")} frees up to {Words.Count(plan.Frees, "place")}"
                    : $"{free.ToString("N0", En)} free · no outfit set to gather";
            }
            Kit.Eyebrow(new Vector2(x, y), eyebrow, null, leftW, eyebrowIcon);
            y += Kit.EyebrowHeight + S(8);
            // The number in heavy capitals, its shadows stacked (the line box is 0.95em: the glyphs sit high in theirs).
            var lineBox = S(60 * 0.95f);
            var bigTop = y + (lineBox - Kit.DisplayHeight(60)) / 2 - S(60 * 0.06f);
            var bw = Kit.Display(new Vector2(x, bigTop), big, 60, null, leftW, true, 0);
            var smallTop = bigTop + Txt.Ascent(Face.Display, 60) - Txt.Ascent(Face.Display, 30);
            if (bw + S(20) < leftW) Kit.Display(new Vector2(x + bw, smallTop), small, 30, null, leftW - bw, true, 0);
            y += lineBox + S(8);
            Kit.Meter(new Vector2(x, y), MathF.Min(S(560), leftW), fraction, MeterKind.Ice, large: true);
            y += S(8) + S(8);
            Txt.Draw(dl, new Vector2(x, y), Txt.Fit(line, Face.SansMedium, 13, leftW), Face.SansMedium, 13, Palette.OnField2);

            // The right: the sticker and the hand voice, or Stop while it works.
            var rx = x + leftW + S(26);
            var innerH = h - padY * 2;
            if (storing || takingOut)
            {
                var bh = Kit.ButtonHeight(BtnSize.Md);
                const string note = "Stops once the step the game is answering is done.";
                var noteH = Txt.Flow(Vector2.Zero, rightW, StopNote(note), 16, out _, draw: false);
                var top = min.Y + padY + (innerH - bh - S(10) - noteH) / 2;
                if (Kit.Button("##room-stop", new Vector2(rx, top), "Stop", Btn.Glass, BtnSize.Md, Icon.Square, 0, false, "Stops once the step the game is answering is done", 0, 14))
                    Later(() =>
                    {
                        OutfitStorer.Stop();
                        DresserTaker.Stop();
                    });
                Txt.Flow(new Vector2(rx, top + bh + S(10)), rightW, StopNote(note), 16, out _);
            }
            else if (!done || plan.Jobs.Count > 0)
            {
                var blocked = AutoRoomBlocked(plan, have);
                var hand = !config.OutfitStoreWorked
                    ? "it asks first, and does one set the first time so you can check"
                    : plan.Jobs.Count > 1 ? $"it asks first, then stores all {Few(plan.Jobs.Count)} sets, and you can stop it" : "it asks first, and you can stop it";
                var sh = Kit.StickerHeight(StickerSize.Lg);
                var handH = Txt.Flow(Vector2.Zero, rightW, HandLine(hand), 15.6f, out _, draw: false);
                var top = min.Y + padY + (innerH - sh - S(10) - handH) / 2;
                if (Kit.Sticker("##room-go", new Vector2(rx, top), "Make room for me", StickerSize.Lg, Icon.Package, 0, blocked != null,
                        blocked ?? "Gathers your outfit sets' loose pieces into their sets, freeing places: it asks first"))
                {
                    var p = plan;
                    Later(() => AskAutoRoom(p, have));
                }
                Txt.Flow(new Vector2(rx, top + sh + S(10)), rightW, HandLine(hand), 15.6f, out _);
            }
        }
        Kit.Reserve(at, w, h);
    }

    private readonly Run[] fieldRun = new Run[1];

    private Run[] HandLine(string text)
    {
        fieldRun[0] = new Run(text, Face.Hand, 13, Palette.OnField);
        return fieldRun;
    }

    private Run[] StopNote(string text)
    {
        fieldRun[0] = new Run(text, Face.SansMedium, 12, Palette.OnField2);
        return fieldRun;
    }

    // ---------------------------------------------------------------------------------------------
    // The cards under it.

    /// <summary>A card's heading (the mock's .panel-h): display capitals 17, a faint mono count, and words at its right (amber: something to see to).</summary>
    private static void CardHead(string title, string? count, string? end = null, Vector4? endColor = null, float underCss = 10)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var h = Kit.DisplayHeight(17);
        var endW = end != null ? Txt.Measure(end, Face.Sans, 12).X : 0;
        var tw = Kit.Display(at, title, 17, Palette.Ink, w - endW - (end != null ? S(12) : 0));
        var cx = at.X + tw + S(10);
        if (count != null)
        {
            var cy = at.Y + Txt.Ascent(Face.Display, 17) - Txt.Ascent(Face.Mono, 12);
            cx += Txt.Draw(dl, new Vector2(cx, cy), count, Face.Mono, 12, Palette.Faint) + S(10);
        }
        if (end != null && at.X + w - endW > cx)
            Txt.Draw(dl, new Vector2(at.X + w - endW, at.Y + (h - Txt.Natural(Face.Sans, 12)) / 2), end, Face.Sans, 12, endColor ?? Palette.Muted);
        Kit.Reserve(at, w, h + S(underCss));
    }

    /// <summary>
    /// Keep as outfits (the mock's left card): each set with two or more loose pieces (one when the
    /// set's stored already), the most freed first, what it frees; during a run, the run's sets and
    /// where each is (done in ice with what it freed, the one at work turning, the rest waiting, dimmed).
    /// </summary>
    private void SetsCard(CleanupNowRead now, RoomRun? run)
    {
        Kit.BeginPanel("##room-sets", 14, 16, 14, Palette.Card, Palette.Line, 18);
        if (run != null)
        {
            CardHead("Keep as outfits", $"{run.Stored} of {run.Total} done");
            var firstWaiting = true;
            for (var i = 0; i < run.Jobs.Count; i++)
            {
                var job = run.Jobs[i];
                var state = run.Rows[i];
                string line;
                TagKind? tag = null;
                string? tagText = null;
                var spin = false;
                var dim = false;
                if (state == RunRow.Done)
                {
                    line = (job.StoredAt >= 0 ? "Joined the stored set" : "Stored") + $" · freed {run.Freed[i]}";
                    tag = TagKind.Ok;
                    tagText = "Done";
                }
                else if (state == RunRow.Left)
                {
                    line = "Left: the game didn't take it";
                    tag = TagKind.Amber;
                    tagText = "Not taken";
                }
                else if (i == run.Seen)
                {
                    line = "Pieces to your bags, then in as the set…";
                    spin = true;
                }
                else
                {
                    line = firstWaiting ? "Next" : "Waiting";
                    firstWaiting = false;
                    dim = true;
                }
                SetRow($"##run-set{i}", job.SetItem, line, tagText, tag ?? TagKind.Plain, tag == TagKind.Ok ? Icon.Check : null, spin, dim);
            }
            Kit.EndPanel();
            return;
        }
        CardHead("Keep as outfits", now.Sets.Count.ToString(), now.Frees > 0 ? $"frees up to {now.Frees}" : null, Palette.Amber);
        if (now.Sets.Count == 0)
            Txt.Para(new[] { new Run("No outfit set has loose pieces in your dresser: nothing to gather.", Face.Sans, 12, Palette.Muted) }, 18);
        for (var i = 0; i < now.Sets.Count; i++)
        {
            var row = now.Sets[i];
            SetRow($"##room-set{i}", row.Item, row.Line, row.Chip, row.Done ? TagKind.Ink : TagKind.Plain, null, false, false);
        }
        Kit.EndPanel();
    }

    /// <summary>A set's row (the mock's .setrow: 50 tall, a hairline over it): its icon, name and line, a tag or the turning ring at the right.</summary>
    private void SetRow(string id, uint setItem, string line, string? tag, TagKind kind, Icon? tagIcon, bool spin, bool dim)
    {
        _ = id;
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(50);
        if (!ImGui.IsRectVisible(at, at + new Vector2(w, h)))
        {
            Kit.Reserve(at, w, h);
            return;
        }
        var dl = Kit.Dl;
        dl.AddLine(new Vector2(at.X, at.Y + 0.5f), new Vector2(at.X + w, at.Y + 0.5f), Palette.U32(Palette.Line), 1f);
        var old = Palette.Opacity;
        if (dim) Palette.Opacity = old * 0.55f;
        try
        {
            Kit.Item(new Vector2(at.X, at.Y + (h - S(36)) / 2), 36, data.Item(setItem)?.Icon ?? 0);
            var right = at.X + w;
            if (tag != null)
            {
                var tw = Kit.TagWidth(tag, tagIcon);
                right -= tw;
                Kit.Tag(new Vector2(right, at.Y + (h - Kit.TagHeight()) / 2), tag, kind, tagIcon);
                right -= S(10);
            }
            else if (spin)
            {
                right -= S(16);
                Spinner(new Vector2(right + S(8), at.Y + h / 2), 16);
                right -= S(10);
            }
            var x = at.X + S(36) + S(10);
            var tw2 = right - x;
            var nh = Txt.Natural(Face.SansSemibold, 13);
            var lh = Txt.Natural(Face.Sans, 12);
            var y = at.Y + (h - nh - lh) / 2;
            var name = data.ItemName(setItem);
            Txt.Draw(dl, new Vector2(x, y), Txt.Fit(name, Face.SansSemibold, 13, tw2), Face.SansSemibold, 13, Palette.Ink);
            Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(line, Face.Sans, 12, tw2), Face.Sans, 12, Palette.Muted);
            if (Txt.Measure(line, Face.Sans, 12).X > tw2 && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(at, at + new Vector2(w, h)))
                Kit.Tooltip(name, line);
        }
        finally
        {
            Palette.Opacity = old;
        }
        Kit.Reserve(at, w, h);
    }

    /// <summary>The small turning ring (the mock's .spin: 16px, a 2px track in the tile and a lilac quarter going round). Not a Kit part.</summary>
    private static void Spinner(Vector2 center, float sizeCss)
    {
        var dl = Kit.Dl;
        var t = S(2);
        var r = S(sizeCss) / 2 - t / 2;
        dl.AddCircle(center, r, Palette.U32(Palette.Tile), 24, t);
        var a = ReducedMotion ? 0f : (float)(ImGui.GetTime() * 5.0 % (Math.PI * 2));
        dl.PathArcTo(center, r, a - MathF.PI / 2, a, 10);
        dl.PathStroke(Palette.U32(Palette.Accent), ImDrawFlags.None, t);
    }

    /// <summary>The duplicates card's height (for laying Not in any look or plate beside the sets).</summary>
    private static float DupesCardHeight => S(14) * 2 + MathF.Max(Kit.ButtonHeight(BtnSize.Sm), S(15 * 1.02f) + S(4) + Txt.Natural(Face.Sans, 12) * 2);

    /// <summary>
    /// Not in any look or plate (the mock's card): shown once your looks have been read; its pieces,
    /// as many as sit beside the sets (<paramref name="roomBeside"/>; 0: a few), "and 29 more" for the rest.
    /// </summary>
    private void UnusedCard(CleanupNowRead now, float roomBeside)
    {
        Kit.BeginPanel("##room-unused", 14, 16, 14, Palette.Card, Palette.Line, 18);
        List<CleanupRow> rows;
        string note;
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
                : "None of your looks, the ones you love, your plans or your plates uses these.";
        }
        // (The note sits 4 closer than a list would: the mock's margin -4 0 4.)
        CardHead("Not in any look or plate", LooksKnown ? rows.Count.ToString() : null, underCss: 6);
        Txt.Para(new[] { new Run(note, Face.Sans, 12, Palette.Muted) }, 16);
        Kit.Gap(4);
        // As many as sit beside the sets (two at least), the rest behind "and N more".
        var rowH = S(52);
        var fits = roomBeside != 0 ? (int)MathF.Floor((roomBeside - (Kit.At.Y - Kit.Panel.Start.Y) - S(14) - S(24)) / rowH) : 4;
        var shown = unusedAll ? rows.Count : Math.Min(rows.Count, Math.Max(2, fits));
        if (rows.Count - shown == 1) shown = rows.Count;
        var dl = Kit.Dl;
        for (var i = 0; i < shown; i++)
        {
            var row = rows[i];
            var at = Kit.At;
            var w = Kit.Width;
            if (!ImGui.IsRectVisible(at, at + new Vector2(w, rowH)))
            {
                Kit.Reserve(at, w, rowH);
                continue;
            }
            if (i > 0) dl.AddLine(new Vector2(at.X, at.Y + 0.5f), new Vector2(at.X + w, at.Y + 0.5f), Palette.U32(Palette.Line), 1f);
            Kit.Item(new Vector2(at.X, at.Y + (rowH - S(32)) / 2), 32, data.Item(row.Item)?.Icon ?? 0);
            var x = at.X + S(32) + S(11);
            var tw = at.X + w - x;
            var nh = Txt.Natural(Face.SansSemibold, 13);
            var lh = Txt.Natural(Face.Sans, 12);
            var y = at.Y + (rowH - nh - lh) / 2;
            Txt.Draw(dl, new Vector2(x, y), Txt.Fit(data.ItemName(row.Item), Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink);
            Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(row.Line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted);
            Kit.Reserve(at, Kit.Width, rowH);
        }
        if (rows.Count > shown || unusedAll && rows.Count > 2)
        {
            Kit.Gap(4);
            var lat = Kit.At;
            var label = rows.Count > shown ? $"and {rows.Count - shown} more" : "Show fewer";
            if (Kit.Link("##room-unused-more", lat, label, 12, Face.SansSemibold)) unusedAll = !unusedAll;
            Kit.Reserve(lat, Kit.Width, Txt.Natural(Face.SansSemibold, 12));
        }
        Kit.EndPanel();
    }

    /// <summary>Duplicates (the mock's card): what they are in a line, and Take out duplicates (asks first).</summary>
    private void DupesCard(CharacterWardrobe me, List<DresserPiece> dupes, (int Prisms, int FreeBag) have)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var h = DupesCardHeight;
        Kit.Card(at, at + new Vector2(w, h), Palette.Card, Palette.Line, 18);
        var x = at.X + S(16);
        var inner = w - S(32);
        var blocked = DupesBlocked(dupes, have);
        const string label = "Take out duplicates";
        var bw = dupes.Count > 0 ? Kit.ButtonWidth(label, BtnSize.Sm) : 0;
        var tw = inner - (bw > 0 ? bw + S(12) : 0);
        var summary = DupesSummary(dupes);
        var colH = S(15 * 1.02f) + S(4) + Txt.Natural(Face.Sans, 12) * 2;
        var y = at.Y + (h - colH) / 2;
        Kit.Display(new Vector2(x, y - S(15 * 0.09f)), "Duplicates", 15, Palette.Ink, tw);
        y += S(15 * 1.02f) + S(4);
        fieldRun[0] = new Run(summary, Face.Sans, 12, Palette.Muted);
        Txt.Flow(new Vector2(x, y), tw, fieldRun, 15, out _);
        if (dupes.Count > 0
            && Kit.Button("##room-dupes", new Vector2(x + inner - bw, at.Y + (h - Kit.ButtonHeight(BtnSize.Sm)) / 2), label, Btn.Glass, BtnSize.Sm, null, 0, blocked != null,
                blocked ?? "Takes them out to your bags, one at a time: it asks first"))
            Later(() => AskDupes(me, dupes, have));
        Kit.Reserve(at, w, h);
    }

    /// <summary>While it works (the mock's card beside the run): how it goes, in words.</summary>
    private static void WhileItWorks(bool dupes)
    {
        Kit.BeginPanel("##room-while", 14, 16, 14, Palette.Card, Palette.Line, 18);
        var at = Kit.At;
        Kit.Display(new Vector2(at.X, at.Y - S(15 * 0.09f)), "While it works", 15, Palette.Ink, Kit.Width);
        Kit.Reserve(at, Kit.Width, S(15 * 1.02f));
        Kit.Gap(6);
        Txt.Para(new[]
        {
            new Run(dupes
                ? "Each duplicate is restored to your bags, one at a time, and waited for. Never the last copy, never a Unique piece you hold; a set's copy comes out whole. It stops when your bags are full."
                : "Each piece is restored to your bags, then they go in as the set, a glamour prism a piece, as storing by hand spends. A refused step is asked again for up to ten seconds; a set the game doesn't take is left, its pieces in your bags, and the run goes on (three in a row and it stops).",
                Face.Sans, 12, Palette.Muted),
        }, 16);
        Kit.EndPanel();
    }

    /// <summary>What's left after a run, said plainly in amber (something to see to): which set wasn't taken and where its pieces are, or why it stopped.</summary>
    private void RunWarnCard(RoomRun run)
    {
        var left = run.Dupes ? new List<string>() : run.Jobs.Where((j, i) => run.Rows[i] == RunRow.Left).Select(j => data.ItemName(j.SetItem)).ToList();
        var title = left.Count > 0
            ? $"{Words.List(left)} {(left.Count == 1 ? "wasn't" : "weren't")} taken"
            : run.Dupes ? "Taking out stopped" : "It stopped";
        Kit.BeginPanel("##room-warn", 14, 16, 14, Palette.Card, Palette.WithAlpha(Palette.Amber, 0.4f), 18);
        var at = Kit.At;
        var w = Kit.Width;
        Icons.Draw(Kit.Dl, Icon.AlertTriangle, new Vector2(at.X, at.Y + S(4)), S(18), Palette.Amber);
        var x = at.X + S(18) + S(12);
        var tw = w - S(30);
        var y = at.Y;
        fieldRun[0] = new Run(title, Face.SansSemibold, 13, Palette.Ink);
        y += Txt.Flow(new Vector2(x, y), tw, fieldRun, 17, out _) + S(4);
        fieldRun[0] = new Run(run.Words ?? string.Empty, Face.Sans, 12, Palette.Muted);
        y += Txt.Flow(new Vector2(x, y), tw, fieldRun, 16, out _);
        Kit.Reserve(at, w, MathF.Max(y - at.Y, S(22)));
        Kit.EndPanel();
    }
}

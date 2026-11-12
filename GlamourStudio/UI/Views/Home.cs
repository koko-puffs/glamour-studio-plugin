using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Home (docs/redesign/pages/home.html). At a glamour dresser with Glamour Plates open, the screen's
/// one field: the plate open in the game, the look planned for it in heavy capitals, the sticker
/// that fills it and a callout of its twelve slots now and after (after filling: what happened,
/// the hand saying Save, and a ring of how many went on). Away from one, a quiet card saying how to
/// get there. Not linked, the first run is the field: Link your game and its three steps. Under
/// it, the looks ready for a plate as small character sheets (the planned ones first) and on the
/// right the outfit you're examining, the looks sent from the site and Wear today.
/// </summary>
public sealed partial class MainWindow
{
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

    /// <summary>The bar's pill: what the plugin is doing now (the first that's true wins).</summary>
    private (string Text, Tone Tone, bool Pulse) StatusNow()
    {
        if (recorder.Recording) return ($"Recording what you do · {Words.Count(recorder.LineCount, "line")}", Tone.Recording, true);
        if (editorPlate > 0) return ($"Plate editor open · plate {editorPlate}", Tone.Live, true);
        if (!sync.Linked) return ("Not linked to Glamour Studio", Tone.Neutral, false);
        if (sync.TokenRefused) return ("Link again: the site doesn't know this token", Tone.Danger, false);
        if (sync.Syncing) return ("Sending your closet…", Tone.Live, true);
        // (What went wrong is said in full on Closet; the pill points there.)
        if (sync.SyncProblem != null) return ("Closet not sent: see Closet", Tone.Danger, false);
        switch (sync.StepFor(wardrobe.Current))
        {
            case ConsentStep.AskPlayer:
                return ("This character isn't sent yet: say yes or no", Tone.Neutral, false);
            case ConsentStep.Hold or ConsentStep.TellNo:
                return ("This character isn't sent to the site", Tone.Neutral, false);
        }
        if (sync.LastSyncAt is { } at) return ($"Closet up to date · sent {Words.Ago(at, DateTimeOffset.UtcNow)}", Tone.Success, false);
        return (config.AutoSync ? "Linked · sends your closet as you play" : "Linked", Tone.Success, false);
    }

    private static readonly CultureInfo HomeEn = CultureInfo.InvariantCulture;

    /// <summary>The site's plates (where a look is planned for each), and Discover (other people's looks).</summary>
    private const string PlansUrl = SiteLink.ClosetUrl + "?tab=plates";

    private const string DiscoverUrl = SiteLink.SiteUrl;

    /// <summary>"Plate 1" to "Plate 20" and "Ready for plate N", made once.</summary>
    private static readonly string[] PlateWords = Enumerable.Range(0, Plates.Count + 1).Select(i => $"Plate {i}").ToArray();

    private static readonly string[] ReadyForPlateWords = Enumerable.Range(0, Plates.Count + 1).Select(i => i == 0 ? "Ready for a plate" : $"Ready for plate {i}").ToArray();

    private static string PlateWord(int plate) => plate >= 0 && plate < PlateWords.Length ? PlateWords[plate] : $"Plate {plate}";

    private Action? homeLeft;
    private Action? homeRight;

    private void DrawHome(Vector2 min, Vector2 size)
    {
        Kit.BeginPane("##home", min, size, SideCss, PageTopCss, PageTopCss);
        SeeSentLooks();
        if (editorPlate > 0)
        {
            DrawEditorHero(editorPlate);
            Kit.Gap(20);
            if (!Linked)
            {
                // The plate's the field here, so the first run's steps are a plain card.
                DrawGetStarted(field: false);
                Kit.Gap(18);
            }
        }
        else if (!Linked)
        {
            DrawGetStarted(field: true);
            Kit.Gap(18);
        }
        else
        {
            DrawIdleHero();
            Kit.Gap(18);
        }
        // Below: the ready looks, and a 356 column on the right (the mock's .lower).
        var w = Kit.Width;
        var gap = S(20);
        var split = Math.Clamp((w - gap - S(356)) / MathF.Max(1, w - gap), 0.3f, 0.8f);
        homeLeft ??= DrawHomeLeft;
        homeRight ??= DrawHomeSide;
        Kit.Columns(split, 20, homeLeft, homeRight);
        Kit.EndPane();
    }

    // ---------------------------------------------------------------------------------------------
    // The plate editor, open: the field.

    private (IReadOnlyDictionary<Slot, Piece>? Pieces, int SyncVersion, Look? Look) editorMatch;

    /// <summary>The field's height last frame (it's drawn before what's on it): at least the mock's 236.</summary>
    private float heroH;

    private static readonly EchoRow[] HeroEchoRows = new EchoRow[3];

    /// <summary>The echo's rows, as the mock places them (from the field's top-left, CSS px).</summary>
    private static void FillEcho(string name, float wCss, ReadOnlySpan<float> xs, ReadOnlySpan<float> ys, int px)
    {
        for (var i = 0; i < HeroEchoRows.Length; i++)
        {
            var chars = (int)MathF.Ceiling(MathF.Max(0, wCss - xs[i]) / (px * 0.55f)) + 3;
            HeroEchoRows[i] = new EchoRow(Kit.EchoRowOf(name, chars), xs[i], ys[i]);
        }
    }

    private void DrawEditorHero(int plate)
    {
        var current = EditorPieces(plate);
        // The look it is, worked out again only when the plate or your looks change.
        if (editorMatch.Pieces == null || editorMatch.SyncVersion != sync.Version || !Plates.Same(current, editorMatch.Pieces))
            editorMatch = (current, sync.Version, Plates.Matching(current, sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Inbox.Select(s => s.Look))));
        var match = editorMatch.Look;
        var planned = sync.PlanFor(plate);
        // (On it as the panels say: a plate keeping slots the look leaves bare counts, when bare slots aren't emptied.)
        var plannedOn = planned != null && (Plates.Same(current, planned.Pieces) || (!config.ClearBare && OnPlate(planned.Pieces, current, true)));
        var plan = planned != null && !plannedOn ? PlanCached(planned) : null;
        var filled = lastFill is { } lf && lf.Result.Plate == plate ? lf : ((string LookId, FillResult Result, PlatePlan Plan)?)null;
        var words = HeroWordsFor(plate, current, match, planned, plannedOn, plan, filled);

        var at = Kit.At;
        var w = Kit.Width;
        var h = MathF.Max(S(236), heroH);
        var max = at + new Vector2(w, h);
        float need;
        using (Kit.FieldPanel(at, max))
        {
            FillEcho(PlateWord(plate), w / S(1), stackalloc float[] { 380, 120, 420 }, stackalloc float[] { -10, 85.7f, 181.4f }, 104);
            Kit.Echo(at, max, Palette.RadiusXl, HeroEchoRows, 104);
            need = DrawHeroInside(at, w, h, words, planned, plan != null, filled != null);
        }
        heroH = need;
        Kit.Reserve(at, w, h);
    }

    /// <summary>What the hero says, worked out again only when something it's made from changes.</summary>
    private sealed class HeroWords
    {
        public string Eyebrow = string.Empty;
        public Icon EyebrowIcon = Icon.Layers;
        public string Title = string.Empty;
        public string Line = string.Empty;
        public readonly List<string> Tags = new();
        public string? Hand;
        public string? FillLabel;
        public bool CanFill;
        public string FillTip = string.Empty;
        public Look? SeeLook;
        public string CalloutNote = string.Empty;
        public readonly List<(DotKind? Dot, string Text)> Legend = new();
        public string RingTitle = string.Empty;
        public string RingLine = string.Empty;
        public int RingHave;
        public int RingOf;
        public Piece?[] Now = new Piece?[12];
        public (Piece? Piece, AfterKind Kind, Own Own)[]? After;
    }

    /// <summary>A slot of the plate after filling it.</summary>
    private enum AfterKind
    {
        /// <summary>The look's piece goes on (its dot says from where).</summary>
        Set,

        /// <summary>Left as it is (a weapon kept, a bare slot kept): faded.</summary>
        Kept,

        /// <summary>Emptied: ×.</summary>
        Cleared,

        /// <summary>The look's piece isn't in the dresser or armoire: kept as it is, a pink dot.</summary>
        Missing,
    }

    private (int Plate, IReadOnlyDictionary<Slot, Piece>? Current, Look? Match, Look? Planned, bool On, PlatePlan? Plan, FillResult? Fill, bool Changes, int Sync)? heroKey;
    private readonly HeroWords heroWords = new();

    private HeroWords HeroWordsFor(int plate, Dictionary<Slot, Piece> current, Look? match, Look? planned, bool plannedOn, PlatePlan? plan,
        (string LookId, FillResult Result, PlatePlan Plan)? filled)
    {
        var key = (plate, (IReadOnlyDictionary<Slot, Piece>?)current, match, planned, plannedOn, plan, filled?.Result, editorChanges, sync.Version);
        if (heroKey == key) return heroWords;
        heroKey = key;
        var hw = heroWords;
        hw.Tags.Clear();
        hw.Legend.Clear();
        hw.Hand = null;
        hw.FillLabel = null;
        hw.SeeLook = null;
        hw.After = null;
        for (var i = 0; i < 12; i++)
            hw.Now[i] = current.TryGetValue(Slots.PlateOrder[i], out var p) ? p : null;
        var unsaved = editorChanges ? " · not saved yet" : string.Empty;

        if (filled is { } f)
        {
            var r = f.Result;
            var fp = f.Plan;
            hw.EyebrowIcon = Icon.Check;
            hw.Eyebrow = $"Plate {plate} · filled" + (editorChanges ? " · not saved yet" : " · saved");
            hw.Title = lastFillName.Length > 0 ? lastFillName : "The look";
            var set = Math.Min(r.Set, fp.Ready);
            var line = set == fp.Wanted
                ? fp.Wanted == 1 ? "Its one piece went on" : $"All {Lower(fp.Wanted)} went on"
                : $"{Words.InWords(set)} of {Lower(fp.Wanted)} went on";
            var bag = fp.BagDyes;
            var bagN = bag.Values.Sum();
            if (bagN > 0) line += $", {Lower(bagN)} dyed {Words.List(bag.Keys.Select(d => data.DyeName(d)))} from your bags";
            line += ".";
            var cleared = fp.Steps.Count(s => s.Kind == StepKind.Clear);
            if (cleared > 0) line += cleared == 1 ? " One slot emptied." : $" {Words.InWords(cleared)} slots emptied.";
            if (r.Problems.Count > 0) line += " " + string.Join(" ", r.Problems);
            hw.Line = line;
            hw.Hand = editorChanges ? "now press Save in the game's window" : "saved: close the plate windows to wear it";
            hw.SeeLook = FindLook(f.LookId);
            hw.RingHave = set;
            hw.RingOf = fp.Wanted;
            hw.RingTitle = $"On plate {plate}";
            // (No Wear plate N here: the game won't change gearset with Glamour Plates open at a dresser, so the words say how.)
            hw.RingLine = (editorChanges ? "To wear it: save, close" : "To wear it: close")
                          + $" Glamour Plates and the dresser, then Wear plate {plate} in Plates. The game won't change gearset while they're open.";
            return hw;
        }

        hw.EyebrowIcon = Icon.Layers;
        hw.Eyebrow = $"Plate {plate} · open in the game{unsaved}";
        hw.CalloutNote = Words.Count(current.Count, "piece");
        if (planned != null && plan != null)
        {
            hw.Title = planned.Name;
            hw.Line = $"Planned for this plate on the site · by {planned.Maker} · {Words.Count(planned.Count, "piece")}";
            var dresserN = plan.Set.Count(s => s.Source == PlateSource.Dresser);
            var armoireN = plan.Set.Count(s => s.Source == PlateSource.Armoire);
            if (dresserN > 0) hw.Tags.Add($"{dresserN} go on");
            if (armoireN > 0) hw.Tags.Add(dresserN > 0 ? $"{armoireN} from the armoire" : $"{armoireN} go on from the armoire");
            var away = plan.Missing.Count(m => m.Elsewhere != null);
            var gone = plan.Missing.Count() - away;
            if (away > 0) hw.Tags.Add($"{away} to put away first");
            if (gone > 0) hw.Tags.Add($"{gone} missing");
            var bagN = plan.BagDyes.Values.Sum();
            if (bagN > 0) hw.Tags.Add(Words.Count(bagN, "dye") + " from your bags");
            var dyeLeft = plan.Steps.Count(s => s.DyeLeft);
            if (dyeLeft > 0) hw.Tags.Add($"{dyeLeft} to dye in the editor");
            var cleared = plan.Steps.Count(s => s.Kind == StepKind.Clear);
            if (cleared > 0) hw.Tags.Add(Words.Count(cleared, "slot") + " emptied");
            hw.FillLabel = $"Fill plate {plate}";
            hw.CanFill = plan.Ready > 0 || cleared > 0;
            hw.FillTip = hw.CanFill
                ? $"Fills plate {plate} with “{planned.Name}”. Nothing's saved until you press Save in the game."
                : "None of its pieces is in your dresser or armoire";
            hw.Hand = "you press Save in the game";
            hw.SeeLook = planned;

            // After: what goes on (ice from the dresser, amber from the armoire or with a dye to see to), kept, emptied, missing.
            hw.After = new (Piece?, AfterKind, Own)[12];
            var kept = new List<string>();
            for (var i = 0; i < 12; i++)
            {
                var slot = Slots.PlateOrder[i];
                var step = plan.Steps.FirstOrDefault(s => s.Slot == slot);
                var now = hw.Now[i];
                var kind = step?.Kind ?? StepKind.Keep;
                hw.After[i] = kind switch
                {
                    StepKind.Set when step != null => (step.Want, AfterKind.Set,
                        step.Source == PlateSource.Armoire || step.DyeFromBags || step.DyeLeft ? Own.Amber : Own.Ice),
                    StepKind.Clear => (null, AfterKind.Cleared, Own.None),
                    StepKind.Missing => (now, AfterKind.Missing, Own.Pink),
                    _ => (now, AfterKind.Kept, Own.None),
                };
                if (kind == StepKind.Keep && now != null && slot is Slot.Main or Slot.Off && !kept.Contains("weapon kept")) kept.Add("weapon kept");
            }
            if (cleared > 0) kept.Add("bare slots emptied");
            hw.CalloutNote = string.Join(" · ", kept);
            hw.Legend.Add((DotKind.Ice, "dresser"));
            var amber = new List<string>();
            if (armoireN > 0) amber.Add("armoire");
            if (bagN > 0) amber.Add("dyed from your bags");
            if (dyeLeft > 0) amber.Add("to dye in the editor");
            if (amber.Count > 0) hw.Legend.Add((DotKind.Amber, string.Join(" · ", amber)));
            if (away + gone > 0) hw.Legend.Add((DotKind.Pink, "not in the dresser or armoire"));
            if (cleared > 0) hw.Legend.Add((null, "emptied"));
            return hw;
        }
        if (planned != null)
        {
            hw.Title = planned.Name;
            hw.Line = $"It has the look planned for it on the site · by {planned.Maker} · {Words.Count(planned.Count, "piece")}";
            hw.SeeLook = planned;
            if (editorChanges) hw.Hand = "you press Save in the game";
            return hw;
        }
        hw.Title = match?.Name ?? PlateWord(plate);
        hw.Line = current.Count == 0 ? "It's empty. Pick a look below to put it on."
            : match != null ? $"It's your look, {Words.Count(current.Count, "piece")}. Nothing is planned for it on the site."
            : $"It has {Words.Count(current.Count, "piece")}. Pick a look below to put it on instead.";
        hw.SeeLook = match;
        return hw;
    }

    /// <summary>A look of yours, one you love, one sent to you or planned, by its id.</summary>
    private Look? FindLook(string id) =>
        sync.Library.Mine.FirstOrDefault(l => l.Id == id) ?? sync.Library.Loved.FirstOrDefault(l => l.Id == id)
        ?? sync.Inbox.FirstOrDefault(s => s.Look.Id == id)?.Look ?? sync.Plans.FirstOrDefault(p => p.Look.Id == id)?.Look;

    /// <summary>A number in words, in a sentence ("seven").</summary>
    private static string Lower(int n) => Words.InWords(n).ToLower(HomeEn);

    /// <summary>
    /// What's on the field: the words and the sticker on the left (470 wide at most), the callout of
    /// the twelve slots (or, filled, the ring) on the right. Returns the field's height it needs.
    /// </summary>
    private float DrawHeroInside(Vector2 at, float w, float h, HeroWords hw, Look? planned, bool withPlan, bool filledNow)
    {
        var padX = S(26);
        var padY = S(22);
        var inner = w - padX * 2;
        var calloutWant = filledNow ? S(382) : S(500);
        var leftW = MathF.Min(S(470), inner - S(26) - calloutWant);
        if (leftW < S(300)) leftW = Math.Clamp(inner * 0.5f, S(220), S(470));
        var x = at.X + padX;
        var y = at.Y + padY;
        y += Kit.Eyebrow(new Vector2(x, y), hw.Eyebrow, null, leftW, hw.EyebrowIcon) + S(8);
        var titleBox = S(44 * 0.98f);
        Kit.Display(new Vector2(x, y + (titleBox - Kit.DisplayHeight(44)) / 2), hw.Title, 44, null, leftW, stacked: true, em: 0.015f);
        y += titleBox + S(8);
        if (filledNow)
        {
            y += HeroLine(new Vector2(x, y), leftW, hw.Line);
            if (hw.Hand != null) y += S(12) + Kit.Hand(new Vector2(x, y), hw.Hand, 18, leftW, null, 18 * 1.1f);
            y += S(8);
            var bx = x;
            const string again = "Fill it again";
            if (Kit.Button("##hero-again", new Vector2(bx, y), again, Btn.Glass, BtnSize.Sm))
                lastFill = null;
            bx += Kit.ButtonWidth(again, BtnSize.Sm) + S(10);
            if (hw.SeeLook is { } see && Kit.Button("##hero-see", new Vector2(bx, y), "See the look", Btn.Quiet, BtnSize.Sm))
                Later(() => OpenLook(see, null, Screen.Home));
            y += Kit.ButtonHeight(BtnSize.Sm);
        }
        else
        {
            y += HeroLine(new Vector2(x, y), leftW, hw.Line);
            if (hw.Tags.Count > 0)
            {
                y += S(6);
                var tx = x;
                var ty = y;
                foreach (var tag in hw.Tags)
                {
                    var tw = Kit.TagWidth(tag);
                    if (tx > x && tx + tw > x + leftW)
                    {
                        tx = x;
                        ty += Kit.TagHeight() + S(6);
                    }
                    Kit.Tag(new Vector2(tx, ty), tag, TagKind.OnField);
                    tx += tw + S(6);
                }
                y = ty + Kit.TagHeight() + S(8);
            }
            y += S(8);
            var bx = x;
            var rowH = hw.FillLabel != null ? Kit.StickerHeight(StickerSize.Lg) : Kit.ButtonHeight(BtnSize.Md);
            if (hw.FillLabel is { } fill && planned != null)
            {
                var thePlanned = planned;
                if (Kit.Sticker("##hero-fill", new Vector2(bx, y), fill, StickerSize.Lg, Icon.Layers, 0, !hw.CanFill, hw.FillTip))
                    Later(() => Fill(thePlanned, PlanFor(thePlanned.Pieces)));
                bx += Kit.StickerWidth(fill, StickerSize.Lg, Icon.Layers) + S(10);
            }
            if (hw.SeeLook is { } see)
            {
                const string seeWords = "See the look";
                if (Kit.Button("##hero-see", new Vector2(bx, y + (rowH - Kit.ButtonHeight(BtnSize.Md)) / 2), seeWords, Btn.Glass, BtnSize.Md))
                    Later(() => OpenLook(see, null, Screen.Home));
                bx += Kit.ButtonWidth(seeWords, BtnSize.Md) + S(16);
            }
            if (hw.Hand is { } hand)
            {
                var room = MathF.Min(S(130), x + leftW - bx);
                if (room > S(60))
                {
                    HandRun[0] = new Run(hand, Face.Hand, 13, Tint.Accent);
                    var hh = Txt.Flow(Vector2.Zero, room, HandRun, 13 * 1.15f, out _, draw: false);
                    Kit.Hand(new Vector2(bx, y + (rowH - hh) / 2), hand, 13, room, null, 13 * 1.15f);
                }
            }
            if (hw.FillLabel != null || hw.SeeLook != null) y += rowH;
            else y -= S(8);
        }
        var leftBottom = y + padY;

        // The right: a callout, centred down the field.
        var rx = x + leftW + S(26);
        var rw = at.X + w - padX - rx;
        float rightNeed;
        using (Kit.OnInk())
        {
            rightNeed = filledNow
                ? DrawRingCallout(new Vector2(rx, at.Y), rw, h, hw)
                : DrawSlotsCallout(new Vector2(rx, at.Y), rw, h, hw, withPlan);
        }
        return MathF.Max(leftBottom - at.Y, rightNeed + padY * 2);
    }

    private static readonly Run[] HandRun = new Run[1];
    private static readonly Run[] HeroLineRun = new Run[1];

    /// <summary>The field's line under the title (13, 500, the field's quieter ink), wrapped. Returns its height.</summary>
    private static float HeroLine(Vector2 pos, float width, string text)
    {
        HeroLineRun[0] = new Run(text, Face.SansMedium, 13, Tint.Muted);
        return Txt.Flow(pos, width, HeroLineRun, 18, out _);
    }

    /// <summary>The slot rows' label (the mock's .slotlab: 44 wide, 10px 650 capitals, faint; After in lilac).</summary>
    private static void SlotLabel(Vector2 pos, float rowH, string text, Vector4 color)
    {
        var caps = Txt.Upper(text);
        var th = Txt.Natural(Face.SansSemibold, 10);
        Txt.Spaced(Kit.Dl, new Vector2(pos.X, pos.Y + (rowH - th) / 2), caps, Face.SansSemibold, 10, color, 0.08f);
    }

    /// <summary>
    /// The plate's twelve slots in a callout (the mock's): its heading and a note, the slots now and
    /// (with a plan) after filling, and what the dots mean. Returns its height.
    /// </summary>
    private float DrawSlotsCallout(Vector2 fieldTop, float width, float fieldH, HeroWords hw, bool after)
    {
        var padX = S(14 + 2);
        var padY = S(12 + 2);
        var labW = S(44);
        var gap = S(5);
        var innerW = width - padX * 2;
        var tile = MathF.Min(S(30), (innerW - labW - S(6) - gap * 11) / 12);
        var tileCss = MathF.Max(12, tile / S(1));
        tile = S(tileCss);
        var headH = Kit.EyebrowHeight;
        var legendH = Txt.Natural(Face.Sans, 12);
        var contentH = headH + S(10) + tile + (after ? S(10) + tile + S(10) + legendH : 0);
        var h = contentH + padY * 2;
        var min = new Vector2(fieldTop.X, fieldTop.Y + (fieldH - h) / 2);
        var max = min + new Vector2(width, h);
        Kit.Callout(min, max);
        var x = min.X + padX;
        var y = min.Y + padY;
        var dl = Kit.Dl;

        Kit.Eyebrow(new Vector2(x, y), "The plate's twelve slots", Palette.Faint, innerW * 0.55f);
        if (hw.CalloutNote.Length > 0)
        {
            var note = Txt.Fit(hw.CalloutNote, Face.Sans, 12, innerW * 0.45f);
            var nw = Txt.Measure(note, Face.Sans, 12).X;
            Txt.Line(dl, new Vector2(x + innerW - nw, y), note, Face.Sans, 12, Palette.Muted, headH / S(1));
        }
        y += headH + S(10);
        SlotLabel(new Vector2(x, y), tile, "Now", Palette.Faint);
        var sx = x + labW + S(6);
        for (var i = 0; i < 12; i++)
        {
            var p = new Vector2(sx + (tile + gap) * i, y);
            if (hw.Now[i] is { } piece)
            {
                Kit.Item(p, tileCss, data.Item(piece.Item)?.Icon ?? 0);
                SlotTip(p, tile, i, piece, null);
            }
            else Kit.ItemEmpty(p, tileCss);
        }
        y += tile;
        if (!after || hw.After == null) return h;

        y += S(10);
        SlotLabel(new Vector2(x, y), tile, "After", Palette.Accent);
        for (var i = 0; i < 12; i++)
        {
            var p = new Vector2(sx + (tile + gap) * i, y);
            var (piece, kind, own) = hw.After[i];
            switch (kind)
            {
                case AfterKind.Cleared:
                    Kit.ItemEmpty(p, tileCss, cross: true);
                    break;
                case AfterKind.Missing when piece == null:
                    Kit.ItemEmpty(p, tileCss, pink: true);
                    break;
                default:
                    if (piece is { } pc)
                    {
                        Kit.Item(p, tileCss, data.Item(pc.Item)?.Icon ?? 0, own, ghost: kind is AfterKind.Kept or AfterKind.Missing);
                        SlotTip(p, tile, i, pc, kind);
                    }
                    else Kit.ItemEmpty(p, tileCss);
                    break;
            }
        }
        y += tile + S(10);

        // What the dots mean.
        var lx = x;
        foreach (var (dot, text) in hw.Legend)
        {
            var tw = Txt.Measure(text, Face.Sans, 12).X;
            var mark = dot != null ? S(7) : Txt.Measure("×", Face.Mono, 11).X;
            if (lx > x && lx + mark + S(4) + tw > x + innerW) break;
            if (dot is { } d) Kit.StatusDot(new Vector2(lx + S(3.5f), y + legendH / 2), d);
            else Txt.Line(dl, new Vector2(lx, y), "×", Face.Mono, 11, Palette.Faint, legendH / S(1));
            lx += mark + S(4);
            Txt.Line(dl, new Vector2(lx, y), text, Face.Sans, 12, Palette.Muted, legendH / S(1));
            lx += tw + S(12);
        }
        return h;
    }

    /// <summary>A slot tile pointed at: its slot, the piece and its dyes (and why it's kept).</summary>
    private void SlotTip(Vector2 p, float tile, int i, Piece piece, AfterKind? kind)
    {
        if (Kit.Blocked || !ImGui.IsWindowHovered() || !ImGui.IsMouseHoveringRect(p, p + new Vector2(tile))) return;
        var line = Slots.Word(Slots.PlateOrder[i]) + (DyeLine(piece) is { } d ? $" · {d}" : string.Empty);
        var why = kind switch
        {
            AfterKind.Missing => "Kept: the look's piece isn't in your dresser or armoire.",
            AfterKind.Kept => "Kept as it is.",
            _ => null,
        };
        Kit.Tooltip(data.ItemName(piece.Item), why != null ? $"{line}\n{why}" : line, why != null ? Palette.Amber : (Vector4?)null);
    }

    /// <summary>After filling: how many went on in a ring, "On plate N" and how to wear it, in a callout at the field's right. Returns its height.</summary>
    private static float DrawRingCallout(Vector2 fieldTop, float width, float fieldH, HeroWords hw)
    {
        var w = MathF.Min(S(382), width);
        var padX = S(18 + 2);
        var padY = S(14 + 2);
        var ring = S(76);
        var textW = w - padX * 2 - ring - S(16);
        LineRun[0] = new Run(hw.RingLine, Face.Sans, 12, Palette.Muted);
        var lineH = Txt.Flow(Vector2.Zero, textW, LineRun, 17, out _, draw: false);
        var titleBox = S(17 * 1.02f);
        var textH = titleBox + S(4) + lineH;
        var h = MathF.Max(ring, textH) + padY * 2;
        var min = new Vector2(fieldTop.X + width - w, fieldTop.Y + (fieldH - h) / 2);
        Kit.Callout(min, min + new Vector2(w, h));
        var center = new Vector2(min.X + padX + ring / 2, min.Y + h / 2);
        Kit.Ring(center, ring / 2 - S(3.5f), S(7), hw.RingOf > 0 ? (float)hw.RingHave / hw.RingOf : 0);
        var count = $"{hw.RingHave}/{hw.RingOf}";
        var cs = new Vector2(Txt.Measure(count, Face.Display, 17).X, Kit.DisplayHeight(17));
        Txt.Draw(Kit.Dl, center - cs / 2, count, Face.Display, 17, Palette.Ink);
        var tx = min.X + padX + ring + S(16);
        var ty = min.Y + (h - textH) / 2;
        Kit.Display(new Vector2(tx, ty + (titleBox - Kit.DisplayHeight(17)) / 2), hw.RingTitle, 17, Palette.Ink, textW);
        Txt.Flow(new Vector2(tx, ty + titleBox + S(4)), textW, LineRun, 17, out _);
        return h;
    }

    private static readonly Run[] LineRun = new Run[1];

    private (int Plate, DateTime At, Dictionary<Slot, Piece> Pieces) editorPieces = (0, DateTime.MinValue, new Dictionary<Slot, Piece>());

    /// <summary>The open plate's pieces, read from the game ten times a second at most (and at once when the plate changes).</summary>
    private Dictionary<Slot, Piece> EditorPieces(int plate)
    {
        var now = DateTime.UtcNow;
        if (editorPieces.Plate != plate || now - editorPieces.At > TimeSpan.FromMilliseconds(100))
            editorPieces = (plate, now, PlateEditor.Current());
        return editorPieces.Pieces;
    }

    // ---------------------------------------------------------------------------------------------
    // The plate editor, closed: how to get there (no field, no sticker: nothing here is the one thing to do).

    private void DrawIdleHero()
    {
        Kit.BeginPanel("##idle", 16, 20, 16);
        var at = Kit.At;
        var w = Kit.Width;
        const string plans = "Plan plates on the site";
        var bw = Kit.ButtonWidth(plans, BtnSize.Sm, Icon.ExternalLink, Btn.Quiet);
        var tx = at.X + S(40) + S(16);
        var tw = at.X + w - bw - S(16) - tx;
        var (_, sub) = PlannedWords();
        IdleRuns[0] = new Run("Open Glamour Plates and pick a plate: it shows here with the look planned for it, ready to fill.", Face.SansSemibold, 14, Palette.Ink);
        IdleSub[0] = new Run(sub, Face.Sans, 12, Palette.Muted);
        var mainH = Txt.Flow(Vector2.Zero, tw, IdleRuns, 19, out _, draw: false);
        var subH = Txt.Flow(Vector2.Zero, tw, IdleSub, 17, out _, draw: false);
        var blockH = Kit.EyebrowHeight + S(4) + mainH + S(4) + subH;
        var h = MathF.Max(S(40), blockH);
        Kit.Round(new Vector2(at.X, at.Y + (h - S(40)) / 2), S(40), Icon.Layers, S(20), Palette.Tile, Palette.Accent);
        var y = at.Y + (h - blockH) / 2;
        y += Kit.Eyebrow(new Vector2(tx, y), "At a glamour dresser", null, tw) + S(4);
        y += Txt.Flow(new Vector2(tx, y), tw, IdleRuns, 19, out _) + S(4);
        Txt.Flow(new Vector2(tx, y), tw, IdleSub, 17, out _);
        if (Kit.Button("##idle-plans", new Vector2(at.X + w - bw, at.Y + (h - Kit.ButtonHeight(BtnSize.Sm)) / 2), plans, Btn.Quiet, BtnSize.Sm, Icon.ExternalLink, 0, false,
                "Your plates on Glamour Studio: plan a look for each, and it's here at the dresser", iconCss: 14))
            Dalamud.Utility.Util.OpenLink(PlansUrl);
        Kit.Reserve(at, w, h);
        Kit.EndPanel();
    }

    private static readonly Run[] IdleRuns = new Run[1];
    private static readonly Run[] IdleSub = new Run[1];

    private (int Sync, string? Key, int Count, string Words) plannedCount = (-1, null, 0, string.Empty);

    /// <summary>How many of your plates have a look planned on the site, and the line saying which, worked out again only when the plans or the character change.</summary>
    private (int Count, string Words) PlannedWords()
    {
        var key = wardrobe.Current?.Key;
        if (plannedCount.Sync != sync.Version || plannedCount.Key != key)
        {
            var plates = Enumerable.Range(1, Plates.Count).Where(p => sync.PlanFor(p) != null).ToList();
            var words = "Every inn room has a glamour dresser."
                        + (plates.Count switch
                        {
                            0 => string.Empty,
                            > 4 => $" You've planned {Words.Count(plates.Count, "plate")} on the site.",
                            _ => $" You've planned {Words.Count(plates.Count, "plate")} on the site: {Words.List(plates.Select(p => $"plate {p}"))}.",
                        });
            plannedCount = (sync.Version, key, plates.Count, words);
        }
        return (plannedCount.Count, plannedCount.Words);
    }

    // ---------------------------------------------------------------------------------------------
    // Nothing linked yet: the first run.

    private static readonly (string Title, string Line)[] GetStartedSteps =
    {
        ("Link your account", "Make a token on the site (Settings → Glamour Studio → In-game plugin) and paste it in Settings → Account."),
        ("Open your dresser and armoire once", "The game only shows them then. The plugin remembers them, and your bags, armoury chest, saddlebags and retainers."),
        ("Put a look on a plate", "At a glamour dresser, open Glamour Plates, pick a look and press Put on plate."),
    };

    private static readonly string[] StepNumbers = { "1", "2", "3" };

    private float getStartedH;

    /// <summary>
    /// Link your game: on the field (the first run's moment), or as a plain card when the plate
    /// editor's field is already there. The three steps as ink callouts, Link your account the
    /// sticker, Open Glamour Studio beside it.
    /// </summary>
    private void DrawGetStarted(bool field)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = MathF.Max(S(field ? 352 : 300), getStartedH);
        var max = at + new Vector2(w, h);
        float need;
        if (field)
        {
            using (Kit.FieldPanel(at, max))
            {
                FillEcho("Get started", w / S(1), stackalloc float[] { 470, 260, 520 }, stackalloc float[] { 0, 95.7f, 191.4f }, 104);
                Kit.Echo(at, max, Palette.RadiusXl, HeroEchoRows, 104);
                need = DrawGetStartedInside(at, w, true);
            }
        }
        else
        {
            Kit.Card(at, max, Palette.Card, Palette.Line, Palette.RadiusLg);
            need = DrawGetStartedInside(at, w, false);
        }
        getStartedH = need;
        Kit.Reserve(at, w, h);
    }

    private static readonly Run[] StepRun = new Run[1];

    private float DrawGetStartedInside(Vector2 at, float w, bool field)
    {
        var padX = S(28);
        var padTop = S(24);
        var x = at.X + padX;
        var inner = w - padX * 2;
        var y = at.Y + padTop;
        y += Kit.Eyebrow(new Vector2(x, y), "Three steps · once", null, inner) + S(10);
        var titleBox = S(44 * 0.98f);
        Kit.Display(new Vector2(x, y + (titleBox - Kit.DisplayHeight(44)) / 2), "Link your game", 44, null, inner, stacked: field, em: 0.015f);
        y += titleBox + S(10);
        HeroLineRun[0] = new Run("Your looks from Glamour Studio on your plates, and your closet on the site kept up to date by itself.", Face.SansMedium, 14, Tint.Muted);
        y += Txt.Flow(new Vector2(x, y), MathF.Min(S(520), inner), HeroLineRun, 20, out _) + S(14);

        // The steps, three ink callouts side by side, as tall as the tallest.
        var gap = S(12);
        var cw = (inner - gap * 2) / 3;
        var tw = cw - S(16) * 2;
        var stepH = 0f;
        for (var i = 0; i < 3; i++)
        {
            StepRun[0] = new Run(GetStartedSteps[i].Line, Face.Sans, 12, Palette.Muted);
            var lh = Txt.Flow(Vector2.Zero, tw, StepRun, 17, out _, draw: false);
            StepRun[0] = new Run(GetStartedSteps[i].Title, Face.SansSemibold, 13, Palette.Ink);
            var th = Txt.Flow(Vector2.Zero, tw, StepRun, 17, out _, draw: false);
            stepH = MathF.Max(stepH, S(26) + S(4) + th + S(4) + lh);
        }
        var boxH = stepH + S(14) * 2;
        using (Kit.OnInk())
        {
            var dl = Kit.Dl;
            for (var i = 0; i < 3; i++)
            {
                var p = new Vector2(x + (cw + gap) * i, y);
                Kit.Callout(p, p + new Vector2(cw, boxH));
                var sy = p.Y + S(14);
                Txt.Line(dl, new Vector2(p.X + S(16), sy), StepNumbers[i], Face.Display, 26, Palette.Accent, 26);
                sy += S(26) + S(4);
                StepRun[0] = new Run(GetStartedSteps[i].Title, Face.SansSemibold, 13, Palette.Ink);
                sy += Txt.Flow(new Vector2(p.X + S(16), sy), tw, StepRun, 17, out _) + S(4);
                StepRun[0] = new Run(GetStartedSteps[i].Line, Face.Sans, 12, Palette.Muted);
                Txt.Flow(new Vector2(p.X + S(16), sy), tw, StepRun, 17, out _);
            }
        }
        y += boxH + S(4) + S(14);

        const string link = "Link your account";
        if (Kit.Sticker("##gs-link", new Vector2(x, y), link, StickerSize.Lg, Icon.Key, 0, false, "Once: then your closet keeps itself up to date on the site"))
            Later(() => OpenSettings(SettingsTab.Account));
        var bx = x + Kit.StickerWidth(link, StickerSize.Lg, Icon.Key) + S(10);
        var lgH = Kit.StickerHeight(StickerSize.Lg);
        if (Kit.Button("##gs-open", new Vector2(bx, y + (lgH - Kit.ButtonHeight(BtnSize.Md)) / 2), "Open Glamour Studio", Btn.Glass, BtnSize.Md, Icon.ExternalLink, iconCss: 14))
            Dalamud.Utility.Util.OpenLink(SiteLink.SiteUrl);
        y += lgH + S(3) + S(24);
        return y - at.Y;
    }

    /// <summary>The left, not linked: the drawn empty page (the sign and its cloud).</summary>
    private void DrawNotLinkedLooks() =>
        Kit.EmptyState("##home-none", "Dress the mannequin on Glamour Studio and save a look: it's here, ready for a plate.",
            "No looks yet", "link and they're all here", heightCss: 222);

    private static readonly (Icon Icon, string Title, string Line, Screen To, string Tip)[] AlreadyHere =
    {
        (Icon.Shirt, "Try it on", "Any plate in the fitting room, no account needed.", Screen.Plates, "Your plates: Try it on any of them"),
        (Icon.Package, "Make room", "Outfit sets to gather in your dresser.", Screen.MakeRoom, "Make room in your glamour dresser"),
        (Icon.Layers, "Your plates", "Wear one, link it to a gearset.", Screen.Plates, "Your twenty plates and your gearsets"),
    };

    private static readonly string[] AlreadyHereIds = { "##here-0", "##here-1", "##here-2" };

    /// <summary>The right, not linked: what works without an account (each opens its screen).</summary>
    private void DrawAlreadyHere()
    {
        Kit.BeginPanel("##already", 14, 16, 14);
        Kit.SectionHead("##already-h", "Already here");
        var dl = Kit.Dl;
        for (var i = 0; i < AlreadyHere.Length; i++)
        {
            if (i > 0) Kit.Gap(10);
            var (icon, title, line, to, tip) = AlreadyHere[i];
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(36);
            var (clicked, hovered, _) = Kit.Hit(AlreadyHereIds[i], at - new Vector2(S(6), S(3)), new Vector2(w + S(12), h + S(6)));
            if (hovered)
            {
                dl.AddRectFilled(at - new Vector2(S(6), S(3)), at + new Vector2(w + S(6), h + S(3)), Palette.U32(Palette.RowHover), S(12));
                Kit.Tooltip(tip);
            }
            Kit.Round(at, h, icon, S(16), Palette.Tile, hovered ? Palette.Ink : Palette.Body);
            var tx = at.X + h + S(10);
            var tw = w - h - S(10);
            var th = Txt.Natural(Face.SansSemibold, 13);
            var lh = Txt.Natural(Face.Sans, 12);
            var ty = at.Y + (h - th - lh) / 2;
            Txt.Draw(dl, new Vector2(tx, ty), Txt.Fit(title, Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink);
            Txt.Draw(dl, new Vector2(tx, ty + th), Txt.Fit(line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted);
            if (clicked) Later(() => Go(to));
            Kit.Reserve(at, w, h);
        }
        Kit.EndPanel();
    }

    // ---------------------------------------------------------------------------------------------
    // Ready for a plate: small character sheets.

    private (int Sync, int Wardrobe, int Plate, Look? Planned, string? Key, List<Look> Looks, int Total)? readyLooks;

    /// <summary>Whether more looks are ready than the last ReadyLooks list holds (it holds 20).</summary>
    private bool ReadyMore => readyLooks is { } r && r.Total > r.Looks.Count;

    /// <summary>
    /// The looks to show as ready: the one planned for the plate open first (away from one, the
    /// ready ones planned for any plate first), then yours, the ones you love and the ones sent to
    /// you that are ready; 20 at most, with how many there are in all.
    /// </summary>
    private List<Look> ReadyLooks(int plate, Look? planned)
    {
        var key = wardrobe.Current?.Key;
        if (readyLooks is { } c && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version && c.Plate == plate && ReferenceEquals(c.Planned, planned) && c.Key == key)
            return c.Looks;
        var list = new List<Look>();
        var seen = new HashSet<string>();
        var total = 0;
        if (planned != null)
        {
            list.Add(planned);
            seen.Add(planned.Id);
            total++;
        }
        var all = sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Inbox.Select(s => s.Look)).ToList();
        // Away from a dresser, the ones planned for a plate first (as they were among themselves).
        if (plate == 0) all = all.OrderByDescending(l => PlannedPlateOf(l.Id) > 0).ToList();
        foreach (var l in all)
        {
            if (!seen.Add(l.Id) || !ReadinessOf(l).PlateReady) continue;
            total++;
            if (list.Count < 20) list.Add(l);
        }
        readyLooks = (sync.Version, wardrobe.Version, plate, planned, key, list, total);
        return list;
    }

    /// <summary>What a ready card shows besides the look: its line, the maker's initials, its dyes and its name in lines for a width.</summary>
    private sealed class SheetCardInfo
    {
        public string Line = string.Empty;
        public string Initials = string.Empty;
        public uint[] Dyes = Array.Empty<uint>();
        public (uint Icon, Own Own)[] Tiles = Array.Empty<(uint, Own)>();
        public bool Ready;
        public string Tag = string.Empty;
        public float LinesFor = -1;
        public string[] NameLines = Array.Empty<string>();
    }

    private readonly Dictionary<string, SheetCardInfo> sheetCards = new();
    private object? sheetCardsFor;
    private (string Count, int Shown, int Total) readyCount = (string.Empty, -1, -1);

    private SheetCardInfo CardInfo(Look look, Look? planned, int plate)
    {
        if (!ReferenceEquals(sheetCardsFor, readyLooks?.Looks))
        {
            sheetCardsFor = readyLooks?.Looks;
            sheetCards.Clear();
        }
        if (sheetCards.TryGetValue(look.Id, out var info)) return info;
        info = new SheetCardInfo();
        var plannedFor = ReferenceEquals(look, planned) ? plate : PlannedPlateOf(look.Id);
        var loved = !look.Mine && sync.Library.Loved.Any(l => l.Id == look.Id);
        var who = look.Mine ? null : loved ? $"loved · by {look.Maker}" : $"sent · by {look.Maker}";
        info.Line = plannedFor > 0
            ? (plannedFor == plate && plate > 0 ? "Planned" : $"Planned for plate {plannedFor}") + (who != null ? $" · {who}" : $" · {Words.Count(look.Count, "piece")}")
            : who != null ? Txt.Sentence(who) : $"Your look · {Words.Count(look.Count, "piece")}";
        if (plannedFor == plate && plate > 0 && who == null) info.Line = $"Planned for this plate · {Words.Count(look.Count, "piece")}";
        info.Initials = Model.Initials.Of(look.Maker);
        info.Dyes = DyesOf(look.Pieces, 6).Rgbs;
        info.Tiles = Slots.All.Where(look.Pieces.ContainsKey).Take(4)
            .Select(s => (data.Item(look.Pieces[s].Item)?.Icon ?? 0, OwnOf(look.Pieces[s].Item))).ToArray();
        var r = ReadinessOf(look);
        info.Ready = r.PlateReady;
        info.Tag = r.PlateReady ? "Ready for a plate" : r.Short;
        sheetCards[look.Id] = info;
        return info;
    }

    /// <summary>A piece's corner dot: ice where a plate takes it from, amber elsewhere of yours, lavender wished, pink missing.</summary>
    private Own OwnOf(uint item) => Where(item) switch
    {
        Place.Dresser or Place.Armoire => Own.Ice,
        null => sync.Library.Wishes.Contains(LookJson.BaseId(item)) ? Own.Star : Own.Pink,
        _ => Own.Amber,
    };

    private readonly Dictionary<IReadOnlyDictionary<Slot, Piece>, (uint[] Rgbs, string[] Names)> lookDyes = new(ReferenceEqualityComparer.Instance);

    /// <summary>A look's dyes in its slots' order, each once (<paramref name="most"/> at most): their colours and names, made once per look.</summary>
    private (uint[] Rgbs, string[] Names) DyesOf(IReadOnlyDictionary<Slot, Piece> pieces, int most)
    {
        if (lookDyes.TryGetValue(pieces, out var d)) return d;
        if (lookDyes.Count > 256) lookDyes.Clear();
        var ids = new List<byte>();
        foreach (var slot in Slots.All)
        {
            if (!pieces.TryGetValue(slot, out var p)) continue;
            if (p.Dye1 != 0 && !ids.Contains(p.Dye1)) ids.Add(p.Dye1);
            if (p.Dye2 != 0 && !ids.Contains(p.Dye2)) ids.Add(p.Dye2);
        }
        var known = ids.Select(id => data.Dye(id)).OfType<DyeInfo>().Take(most).ToList();
        d = (known.Select(k => k.Rgb).ToArray(), known.Select(k => k.Name).ToArray());
        lookDyes[pieces] = d;
        return d;
    }

    /// <summary>Display words wrapped to lines of a width (the last cut with "…"), made when asked.</summary>
    private static string[] WrapDisplay(string text, int px, float width, int lines)
    {
        var rows = new List<string>();
        var cur = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length == 0 || Kit.DisplayWidth(candidate, px) <= width) cur = candidate;
            else
            {
                rows.Add(cur);
                cur = word;
            }
        }
        rows.Add(cur);
        if (rows.Count > lines) rows = rows.Take(lines - 1).Append(string.Join(' ', rows.Skip(lines - 1))).ToList();
        return rows.ToArray();
    }

    private void DrawHomeLeft()
    {
        if (!Linked) DrawNotLinkedLooks();
        else DrawReadyColumn();
    }

    private void DrawReadyColumn()
    {
        var plate = editorPlate;
        var planned = plate > 0 ? sync.PlanFor(plate) : null;
        var looks = ReadyLooks(plate, planned);
        var total = readyLooks?.Total ?? looks.Count;
        var shown = Math.Min(6, looks.Count);
        if (readyCount.Shown != shown || readyCount.Total != total)
            readyCount = (shown < total ? $"{shown} of {total}" : total.ToString(HomeEn), shown, total);
        var title = plate >= 0 && plate < ReadyForPlateWords.Length ? ReadyForPlateWords[plate] : "Ready for a plate";
        if (Kit.SectionHead("##all-looks", title, shown > 0 ? readyCount.Count : null, "All your looks"))
            Later(() =>
            {
                looksTab = 0;
                readyFirst = true;
                Go(Screen.Looks);
            });
        if (looks.Count == 0)
        {
            if (sync.LibraryAt == null)
            {
                var hat = Kit.At;
                Kit.Reserve(hat, Kit.Width, Kit.Hand(hat, "reading your looks…", 13));
            }
            else
                Kit.EmptyState("##ready-none", "A look is ready once every piece is in your glamour dresser or armoire. Open a look to see what to put away.",
                    title: "None ready yet");
            return;
        }
        var at = Kit.At;
        var w = Kit.Width;
        var gap = S(12);
        var cols = (w - gap * 2) / 3 >= S(200) ? 3 : 2;
        var cw = (w - gap * (cols - 1)) / cols;
        var y = at.Y;
        for (var row = 0; row * cols < shown; row++)
        {
            var rowH = 0f;
            for (var c = 0; c < cols && row * cols + c < shown; c++)
                rowH = MathF.Max(rowH, SheetCardHeight(CardInfo(looks[row * cols + c], planned, plate), looks[row * cols + c], cw));
            for (var c = 0; c < cols && row * cols + c < shown; c++)
            {
                var i = row * cols + c;
                var look = looks[i];
                var p = new Vector2(at.X + (cw + gap) * c, y);
                if (SheetCard(SheetCardIds[i], p, new Vector2(cw, rowH), look, CardInfo(look, planned, plate)))
                    Later(() => OpenLook(look, null, Screen.Home));
            }
            y += rowH + gap;
        }
        Kit.Reserve(at, w, y - gap - at.Y);
    }

    private static readonly string[] SheetCardIds = Enumerable.Range(0, 6).Select(i => $"##ready{i}").ToArray();

    /// <summary>The well of a card's pieces: 10 in, four tiles 6 apart (square), 10 in.</summary>
    private static float WellHeight(float cardW) => S(10) * 2 + (cardW - S(12) * 2 - S(10) * 2 - S(6) * 3) / 4;

    private float SheetCardHeight(SheetCardInfo info, Look look, float cw)
    {
        var nameW = cw - S(12) * 2 - S(24) - S(4) - S(8);
        if (info.LinesFor != nameW)
        {
            info.LinesFor = nameW;
            info.NameLines = WrapDisplay(look.Name, 14, nameW, 2);
        }
        var nameH = info.NameLines.Length * S(14 * 1.08f) + Txt.Natural(Face.Sans, 12);
        return S(12) + WellHeight(cw) + S(10) + MathF.Max(nameH, S(26)) + S(10) + Kit.TagHeight() + S(10) + S(10) + S(12);
    }

    /// <summary>
    /// A look as a small character sheet (the mock's .sheet-card): its pieces on a raised well with
    /// their corner dots, its name in display capitals and its line, the maker's seal turned in the
    /// corner, how ready it is, and its dyes as a palette strip along the foot. True when pressed.
    /// </summary>
    private bool SheetCard(string id, Vector2 pos, Vector2 size, Look look, SheetCardInfo info)
    {
        var (clicked, hovered, _) = Kit.Hit(id, pos, size);
        Kit.Card(pos, pos + size, Palette.Card, hovered ? Palette.LineHover : Palette.Line, Palette.RadiusCard);
        var dl = Kit.Dl;
        var x = pos.X + S(12);
        var iw = size.X - S(24);
        var y = pos.Y + S(12);
        var wellH = WellHeight(size.X);
        dl.AddRectFilled(new Vector2(x, y), new Vector2(x + iw, y + wellH), Palette.U32(Palette.Raised), S(12));
        var tile = (iw - S(10) * 2 - S(6) * 3) / 4;
        var tileCss = tile / S(1);
        for (var i = 0; i < info.Tiles.Length; i++)
            Kit.Item(new Vector2(x + S(10) + (tile + S(6)) * i, y + S(10)), tileCss, info.Tiles[i].Icon, info.Tiles[i].Own, ring: Palette.Raised);
        y += wellH + S(10);

        var nameW = iw - S(24) - S(4) - S(8);
        var lineBox = S(14 * 1.08f);
        for (var i = 0; i < info.NameLines.Length; i++)
            Kit.Display(new Vector2(x, y + lineBox * i + (lineBox - Kit.DisplayHeight(14)) / 2), info.NameLines[i], 14, hovered ? Palette.White : Palette.Ink, nameW);
        Txt.Draw(dl, new Vector2(x, y + lineBox * info.NameLines.Length), Txt.Fit(info.Line, Face.Sans, 12, nameW), Face.Sans, 12, Palette.Muted);
        Kit.Seal(new Vector2(x + iw - S(4) - S(12), y + S(2) + S(12)), info.Initials, 24);

        var foot = pos.Y + size.Y - S(12);
        Kit.PaletteStrip(new Vector2(x, foot - S(10)), iw, info.Dyes);
        var ty = foot - S(10) - S(10) - Kit.TagHeight();
        if (Kit.TagWidth(info.Tag, info.Ready ? Icon.Check : null) <= iw)
            Kit.Tag(new Vector2(x, ty), info.Tag, info.Ready ? TagKind.Ok : TagKind.Plain, info.Ready ? Icon.Check : null);
        return clicked;
    }

    // ---------------------------------------------------------------------------------------------
    // The right: someone examined, sent from the site, Wear today.

    private void DrawHomeSide()
    {
        if (Examine.IsOpen) DrawExamined();
        else
        {
            examined = null;
            examinedSeen = null;
            examinedAt = DateTime.MinValue;
        }
        if (!Linked)
        {
            DrawAlreadyHere();
            return;
        }
        var sent = sync.Inbox;
        if (sent.Count > 0)
        {
            DrawSent(sent);
            Kit.Gap(12);
        }
        // Examining takes the column: Wear today waits until the Examine window closes.
        if (examined == null && sync.Wearable.Count > 0) DrawWearToday(sent.Count > 0 ? 2 : 4);
    }

    /// <summary>The ids of looks sent from the site seen on an earlier visit to Home, and on this one.</summary>
    private readonly HashSet<string> sentSeen = new();

    private readonly HashSet<string> sentSeenNow = new();
    private int homeFrame = -10;

    /// <summary>A new visit to Home (it wasn't drawn last frame): the sent looks seen last time aren't new any more.</summary>
    private void SeeSentLooks()
    {
        var frame = ImGui.GetFrameCount();
        if (frame - homeFrame > 1) sentSeen.UnionWith(sentSeenNow);
        homeFrame = frame;
    }

    private (int Sync, DateTime At, string[] Lines)? sentLines;

    /// <summary>Each sent look's line ("For plate 3 · sent 4 min ago"), made again when the list changes or every 20 seconds.</summary>
    private string[] SentLines(IReadOnlyList<SentLook> sent)
    {
        var now = DateTime.UtcNow;
        if (sentLines is { } c && c.Sync == sync.Version && now - c.At < TimeSpan.FromSeconds(20) && c.Lines.Length == sent.Count) return c.Lines;
        var at = DateTimeOffset.UtcNow;
        var lines = sent.Select(s => s.Plate is { } p ? $"For plate {p} · sent {Words.Ago(s.CreatedAt, at)}" : Txt.Sentence($"sent {Words.Ago(s.CreatedAt, at)}")).ToArray();
        sentLines = (sync.Version, now, lines);
        return lines;
    }

    private static readonly string[] SentIds = Enumerable.Range(0, 5).Select(i => $"##sent{i}").ToArray();
    private static readonly string[] WearIds = Enumerable.Range(0, 4).Select(i => $"##wear{i}").ToArray();

    private void DrawSent(IReadOnlyList<SentLook> sent)
    {
        Kit.BeginPanel("##sent", 14, 16, 14);
        Kit.SectionHead("##sent-h", "Sent from the site", sent.Count.ToString(HomeEn));
        var lines = SentLines(sent);
        for (var i = 0; i < sent.Count && i < SentIds.Length; i++)
        {
            var s = sent[i];
            var isNew = !sentSeen.Contains(s.Id);
            sentSeenNow.Add(s.Id);
            if (SmallLookRow(SentIds[i], s.Look, lines[i], isNew, -1)) Later(() => OpenLook(s.Look, s, Screen.Home));
        }
        // A look sent and never put on a plate stays: cleared here (they come off as they go on a plate too).
        Kit.Gap(4);
        var at = Kit.At;
        var text = sent.Count == 1 ? "Done with it: take it off the list" : "Done with these: clear the list";
        var size = Txt.Measure(text, Face.SansSemibold, 12);
        var (clicked, hovered, _) = Kit.Hit("##sent-clear", at, size);
        Txt.Draw(Kit.Dl, at, text, Face.SansSemibold, 12, hovered ? Palette.Ink : Palette.Muted);
        if (clicked)
        {
            var ids = sent.Select(x => x.Id).ToList();
            Later(() =>
            {
                foreach (var id in ids) sync.DoneWith(id);
                Notify(ids.Count == 1 ? "Taken off the list" : "List cleared");
            });
        }
        Kit.Reserve(at, Kit.Width, size.Y);
        Kit.EndPanel();
    }

    private readonly Dictionary<string, string> loveWords = new();

    /// <summary>A number of loves as the site writes it ("1,284"), made once.</summary>
    private string LovesOf(Look look)
    {
        if (loveWords.TryGetValue(look.Id, out var s)) return s;
        if (loveWords.Count > 256) loveWords.Clear();
        return loveWords[look.Id] = look.Loves.ToString("N0", HomeEn);
    }

    private void DrawWearToday(int most)
    {
        var wear = sync.Wearable;
        Kit.BeginPanel("##wear-now", 14, 16, 14);
        if (Kit.SectionHead("##wear-all", "Wear today", wear.Count.ToString(HomeEn), "All"))
            Later(() =>
            {
                looksTab = 2;
                pickedLook = null;
                Go(Screen.Looks);
            });
        for (var i = 0; i < wear.Count && i < most && i < WearIds.Length; i++)
        {
            var look = wear[i];
            if (SmallLookRow(WearIds[i], look, look.Maker, false, look.Loves)) Later(() => OpenLook(look, null, Screen.Home));
        }
        Kit.EndPanel();
    }

    /// <summary>
    /// A look as a row in a card (the mock's .lrow, 8 past the card's padding either side): two of its
    /// pieces, its name in display capitals (the holo sparkle before it when it's new) and its line,
    /// with a pink heart and its loves when <paramref name="loves"/> isn't −1. True when pressed.
    /// </summary>
    private bool SmallLookRow(string id, Look look, string line, bool isNew, int loves)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(64);
        var min = new Vector2(at.X - S(8), at.Y);
        var max = new Vector2(at.X + w + S(8), at.Y + h);
        var (clicked, hovered, _) = Kit.Hit(id, min, max - min);
        var dl = Kit.Dl;
        if (hovered) dl.AddRectFilled(min, max, Palette.U32(Palette.RowHover), S(14));
        var x = min.X + S(12);
        var n = 0;
        foreach (var slot in Slots.All)
        {
            if (!look.Pieces.TryGetValue(slot, out var p)) continue;
            Kit.Item(new Vector2(x + S(36) * n, at.Y + (h - S(32)) / 2), 32, data.Item(p.Item)?.Icon ?? 0);
            if (++n == 2) break;
        }
        x += S(32) * 2 + S(4) + S(12);
        var tw = max.X - S(12) - x;
        var nameBox = S(14 * 1.1f);
        var lineH = Txt.Natural(Face.Sans, 12);
        var y = at.Y + (h - nameBox - lineH) / 2;
        var nx = x;
        if (isNew)
        {
            Kit.Spark(new Vector2(nx, y + (nameBox - S(12)) / 2));
            nx += S(12 + 6);
        }
        Kit.Display(new Vector2(nx, y + (nameBox - Kit.DisplayHeight(14)) / 2), look.Name, 14, hovered ? Palette.White : Palette.Ink, x + tw - nx);
        y += nameBox;
        if (loves >= 0)
        {
            var lw = Txt.Draw(dl, new Vector2(x, y), Txt.Fit(line, Face.Sans, 12, tw * 0.6f), Face.Sans, 12, Palette.Muted);
            var dot = " · ";
            lw += Txt.Draw(dl, new Vector2(x + lw, y), dot, Face.Sans, 12, Palette.Muted);
            DrawHeart(new Vector2(x + lw + S(5), y + lineH / 2), S(10), Palette.Pink);
            Txt.Draw(dl, new Vector2(x + lw + S(14), y), LovesOf(look), Face.Sans, 12, Palette.Muted);
        }
        else Txt.Draw(dl, new Vector2(x, y), Txt.Fit(line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted);
        Kit.Reserve(at, w, h);
        return clicked;
    }

    /// <summary>A filled heart (loves are pink), <paramref name="size"/> across, centred: two circles and the point under them.</summary>
    private static void DrawHeart(Vector2 center, float size, Vector4 color)
    {
        var dl = Kit.Dl;
        var col = Palette.U32(color);
        var r = size * 0.27f;
        var top = center.Y - size * 0.12f;
        dl.AddCircleFilled(new Vector2(center.X - size * 0.23f, top), r, col, 12);
        dl.AddCircleFilled(new Vector2(center.X + size * 0.23f, top), r, col, 12);
        dl.AddTriangleFilled(new Vector2(center.X - size * 0.49f, top + size * 0.04f), new Vector2(center.X + size * 0.49f, top + size * 0.04f),
            new Vector2(center.X, center.Y + size * 0.45f), col);
    }

    // ---------------------------------------------------------------------------------------------
    // Someone's outfit, while the Examine window's open.

    private Examined? examined;
    private DateTime examinedAt;

    /// <summary>A first read of someone new, waiting for a second to agree.</summary>
    private Examined? examinedSeen;

    private bool keepingExamined;

    private void DrawExamined()
    {
        if (ExaminedNow() is not { } e) return;
        DrawExaminedCard(e);
        Kit.Gap(12);
    }

    /// <summary>
    /// The outfit the Examine window shows (read again twice a second at most: the window's open a
    /// while), or null. Someone new counts once two reads half a second apart agree (the window may
    /// still hold the last one's gear for a moment while the new one's loads).
    /// </summary>
    private Examined? ExaminedNow()
    {
        if (DateTime.UtcNow - examinedAt > TimeSpan.FromSeconds(0.5))
        {
            examinedAt = DateTime.UtcNow;
            var read = Examine.Read(data);
            if (read == null) examined = examinedSeen = null;
            else if (examined != null && read.EntityId == examined.EntityId) examined = read;
            else if (examinedSeen != null && read.EntityId == examinedSeen.EntityId && Plates.Same(read.Pieces, examinedSeen.Pieces))
            {
                examined = read;
                examinedSeen = null;
            }
            else
            {
                examined = null;
                examinedSeen = read;
            }
        }
        return examined;
    }

    private (Examined? For, int Wardrobe, string Name, string Initials, string Yours, (uint Icon, Own Own, Piece Piece)[] Tiles)? examinedWords;

    private void DrawExaminedCard(Examined e)
    {
        if (examinedWords is not { } ew || !ReferenceEquals(ew.For, e) || ew.Wardrobe != wardrobe.Version)
        {
            var made = Slots.All.Where(e.Pieces.ContainsKey).Select(s => e.Pieces[s])
                .Select(p => (data.Item(p.Item)?.Icon ?? 0, Where(p.Item) != null ? Own.Ice : Own.Pink, p)).ToArray();
            var yours = made.Count(t => t.Item2 == Own.Ice);
            examinedWords = ew = (e, wardrobe.Version, e.Name.Length > 0 ? $"{e.Name}'s outfit" : "Their outfit",
                Model.Initials.Of(e.Name.Length > 0 ? e.Name : null), $"{yours} of {made.Length} yours", made);
        }
        Kit.BeginPanel("##examine", 14, 16, 14);
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        // The heading, and how many of the pieces are yours at its right.
        var tagW = Kit.TagWidth(ew.Yours);
        var headH = Kit.DisplayHeight(17);
        Kit.Display(at, "Examining", 17, Palette.Ink, w - tagW - S(10));
        Kit.Tag(new Vector2(at.X + w - tagW, at.Y + (headH - Kit.TagHeight()) / 2), ew.Yours, TagKind.Ok);
        Kit.Reserve(at, w, headH + S(10));

        // Their face and name.
        var row = Kit.At;
        var face = S(34);
        var c = row + new Vector2(face / 2);
        dl.AddCircleFilled(c, face / 2, Palette.U32(Palette.Tile), 32);
        var iw = Txt.SpacedWidth(ew.Initials, Face.Display, 12, 0.02f);
        Txt.Spaced(dl, c - new Vector2(iw / 2, Kit.DisplayHeight(12) / 2), ew.Initials, Face.Display, 12, Palette.Accent, 0.02f);
        var tx = row.X + face + S(10);
        var tw = w - face - S(10);
        var nh = Txt.Natural(Face.SansSemibold, 13);
        var lh = Txt.Natural(Face.Sans, 12);
        var ty = row.Y + (face - nh - lh) / 2;
        Txt.Draw(dl, new Vector2(tx, ty), Txt.Fit(ew.Name, Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink);
        Txt.Draw(dl, new Vector2(tx, ty + nh), "Glamour and dyes as shown", Face.Sans, 12, Palette.Muted);
        Kit.Reserve(row, w, face);

        // Their pieces (a pink dot on the ones you haven't got), wrapping.
        Kit.Gap(10);
        var tiles = Kit.At;
        var size = S(36);
        var step = size + S(4);
        var perRow = Math.Max(1, (int)((w + S(4)) / step));
        for (var i = 0; i < ew.Tiles.Length; i++)
        {
            var p = new Vector2(tiles.X + step * (i % perRow), tiles.Y + step * (i / perRow));
            Kit.Item(p, 36, ew.Tiles[i].Icon, ew.Tiles[i].Own);
            if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(p, p + new Vector2(size)))
            {
                var piece = ew.Tiles[i].Piece;
                Kit.Tooltip(data.ItemName(piece.Item), (DyeLine(piece) is { } d ? d + "\n" : string.Empty)
                    + (ew.Tiles[i].Own == Own.Ice ? "Yours" : "Not yours yet"));
            }
        }
        var rows = (ew.Tiles.Length + perRow - 1) / perRow;
        Kit.Reserve(tiles, w, rows > 0 ? rows * step - S(4) : 0);

        Kit.Gap(12);
        var brow = Kit.At;
        const string tryOn = "Try it on";
        if (Kit.Button("##ex-try", brow, tryOn, Btn.Glass, BtnSize.Sm, Icon.Shirt, 0, false, "Opens the game's fitting room with their outfit on you", iconCss: 14))
        {
            var pieces = e.Pieces;
            Later(() => TryOn(pieces));
        }
        var bx = brow.X + Kit.ButtonWidth(tryOn, BtnSize.Sm, Icon.Shirt) + S(6);
        const string keep = "Keep it as a look";
        var kw = MathF.Min(Kit.ButtonWidth(keep, BtnSize.Sm), brow.X + w - bx);
        if (Kit.Button("##ex-keep", new Vector2(bx, brow.Y), keepingExamined ? "Keeping…" : keep, Btn.Glass, BtnSize.Sm, null, kw, !Linked || keepingExamined,
                Linked ? "A private look of yours on the site, to remix in the dressing room" : "Link your account first (Settings → Account)"))
        {
            keepingExamined = true;
            var lookName = e.Name.Length > 0 ? $"{e.Name}'s outfit" : "An outfit I saw";
            sync.SaveLook(lookName, e.Body, e.Pieces, r =>
            {
                keepingExamined = false;
                if (r.Ok) Notify($"Kept as “{r.Value!.Name}” in your looks");
                else Frame.Warn(r.Why ?? "The site said no");
            });
        }
        Kit.Reserve(brow, w, Kit.ButtonHeight(BtnSize.Sm));
        Kit.EndPanel();
    }

    // ---------------------------------------------------------------------------------------------
    // Counts the bar and Closet share.

    private (int Version, string Key, int Used) dresserUsed = (-1, string.Empty, 0);

    /// <summary>The dresser's places used, counted again only when the wardrobe changes.</summary>
    private int DresserUsed(CharacterWardrobe me)
    {
        if (dresserUsed.Version != wardrobe.Version || dresserUsed.Key != me.Key)
            dresserUsed = (wardrobe.Version, me.Key, DresserCleanup.Used(me.Dresser));
        return dresserUsed.Used;
    }
}

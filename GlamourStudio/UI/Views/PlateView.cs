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
/// Plates (the site's Closet → Plates, in the game): what you're wearing first (Try it on, Copy my
/// game outfit), then all twenty plates as numbered cards five across (up to six of its pieces on a
/// raised well, the look it is or how many pieces, a lilac dot when a look's planned for it on the
/// site, the jobs of the gearsets linked to it, On in ice for the one you're wearing; an empty one
/// dashed, "empty" in the hand voice), and the plate picked in the side pane: Wear it (the sticker),
/// Try it on, Keep it as a look, its gearsets (Change to it, unlink, Link to a gearset) and its
/// twelve slots.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>The plate picked (1 to 20).</summary>
    private int pickedPlate = 1;

    /// <summary>A look kept from the game just now, by the plate it came from (0 what you wear), with the pieces it had then.</summary>
    private readonly Dictionary<(string Character, int Plate), (SavedLook Look, IReadOnlyDictionary<Slot, Piece> Pieces)> keptFrom = new();

    /// <summary>The plates being kept now (each its own: one keeping doesn't hold up another).</summary>
    private readonly HashSet<(string Character, int Plate)> keeping = new();

    private static readonly IReadOnlyDictionary<Slot, Piece> NoPieces = new Dictionary<Slot, Piece>();

    /// <summary>The side pane's width (the mock's <c>.pv</c>: 352).</summary>
    private const float PlatesAsideCss = 352;

    private IReadOnlyDictionary<Slot, Piece> PlatePieces(CharacterWardrobe me, int plate) =>
        plate == 0 ? me.Outfit : me.Plates.FirstOrDefault(p => p.Plate == plate)?.Pieces ?? NoPieces;

    // ---------------------------------------------------------------------------------------------
    // What each card shows, worked out once a change (not twenty LINQ walks a frame).

    /// <summary>A plate's card: its first six pieces' icons, how many it has, the look it is, its plan and whether it's on it, and whether you wear it.</summary>
    private sealed record PlateCardInfo(uint[] Icons, int Count, Look? Match, Look? Planned, bool OnPlan, bool Worn, string Name);

    private PlateCardInfo[] plateCards = Array.Empty<PlateCardInfo>();

    private (int Wardrobe, int Sync, string Key) plateCardsFor = (-1, -1, string.Empty);

    /// <summary>Plates with something on them, and "14/20" for the heading (made with the cards).</summary>
    private string platesFilled = string.Empty;

    private PlateCardInfo[] PlateCards(CharacterWardrobe me)
    {
        var stamp = (wardrobe.Version, sync.Version, me.Key);
        if (stamp == plateCardsFor && plateCards.Length == Plates.Count + 1) return plateCards;
        plateCardsFor = stamp;
        var cards = new PlateCardInfo[Plates.Count + 1];
        var filled = 0;
        for (var n = 0; n <= Plates.Count; n++)
        {
            var pieces = PlatePieces(me, n);
            var icons = Slots.All.Where(pieces.ContainsKey).Take(6).Select(s => data.Item(pieces[s].Item)?.Icon ?? 0u).ToArray();
            var match = pieces.Count > 0 ? PlateMatch(n, pieces) : null;
            var planned = n > 0 ? sync.PlanFor(n) : null;
            var onPlan = planned != null && pieces.Count > 0 && Plates.Same(pieces, planned.Pieces);
            var worn = n > 0 && pieces.Count > 0 && me.Outfit.Count > 0 && (OnPlate(pieces, me.Outfit) || Plates.Same(pieces, me.Outfit));
            if (n > 0 && pieces.Count > 0) filled++;
            cards[n] = new PlateCardInfo(icons, pieces.Count, match, planned, onPlan, worn, match?.Name ?? Words.Count(pieces.Count, "piece"));
        }
        platesFilled = $"{filled}/{Plates.Count}";
        plateCards = cards;
        return cards;
    }

    // ---------------------------------------------------------------------------------------------
    // The screen.

    private void DrawPlates(Vector2 min, Vector2 size)
    {
        var me = wardrobe.Current;
        var read = me?.PlatesReadAt != null;
        // The side pane only once there are plates to pick from (the mock's frame 3 is one column).
        var asideW = me != null && read ? MathF.Min(S(PlatesAsideCss), size.X * 0.42f) : 0;
        var mainSize = new Vector2(size.X - asideW, size.Y);
        var sets = GearsetsNow();

        // The mock's .grid: 18 above, 24 at the left, 20 at the right.
        Kit.BeginPane("##plates-main", min, mainSize, SideCss, 18, 20, padRight: asideW > 0 ? 20 : SideCss);
        if (me == null)
        {
            Kit.Gap(2);
            Kit.EmptyState("##plates-nobody", "Log in to a character to see its plates.", "Not logged in", "plates are your character's",
                heightCss: MathF.Max(240, Kit.PaneRoom() / S(1)));
            Kit.EndPane();
            return;
        }
        if (me.Outfit.Count > 0) DrawWornRow(me);
        if (!read)
        {
            Kit.Gap(20);
            Kit.EmptyState("##plates-unread", "Your plates show here once you've opened Glamour Plates at a glamour dresser (every inn room has one).",
                "Not read yet", "the game keeps them at the dresser", heightCss: MathF.Max(240, Kit.PaneRoom() / S(1)));
            Kit.EndPane();
            return;
        }
        var cards = PlateCards(me);
        if (me.Outfit.Count > 0) Kit.Gap(16);
        DrawPlatesHeading(me);
        Kit.Gap(12);

        // The twenty, five across (the mock's .plates: 10 apart; a card 178 tall at the mock's width,
        // its well two rows of icons, its name two lines and a line under it, whatever's on it).
        var gat = Kit.At;
        var w = Kit.Width;
        var gap = S(10);
        var cardW = (w - gap * 4) / 5;
        var icon = MathF.Max(S(12), (cardW - S(20) - S(12) - S(8)) / 3);
        var cardH = S(10 + 18 + 7) + WellHeight(2, icon) + S(7) + S(PlateNameCss) * 2 + S(7) + S(PlateLnCss) + S(10);
        for (var i = 0; i < Plates.Count; i++)
        {
            var n = i + 1;
            var p = new Vector2(gat.X + (cardW + gap) * (i % 5), gat.Y + (cardH + gap) * (i / 5));
            if (PlateCard(n, p, new Vector2(cardW, cardH), icon, cards[n])) pickedPlate = n;
        }
        var rows = (Plates.Count + 4) / 5;
        Kit.Reserve(gat, w, cardH * rows + gap * (rows - 1));
        Kit.EndPane();

        // The side pane: the window's second ground, a hairline at its left (the mock's .side).
        var asideMin = new Vector2(min.X + mainSize.X, min.Y);
        var wdl = Kit.Dl;
        wdl.AddRectFilled(asideMin, asideMin + new Vector2(asideW, size.Y), Palette.U32(Palette.Ground2), S(14), ImDrawFlags.RoundCornersBottomRight);
        wdl.AddLine(new Vector2(asideMin.X + 0.5f, asideMin.Y), new Vector2(asideMin.X + 0.5f, asideMin.Y + size.Y), Palette.U32(Palette.Line), 1f);
        Kit.BeginPane("##plates-aside", asideMin + new Vector2(1, 0), new Vector2(asideW - 1, size.Y), 20, 18, 18);
        var picked = Math.Clamp(pickedPlate, 1, Plates.Count);
        DrawPlateAside(me, picked, cards[picked], sets);
        Kit.EndPane();
    }

    /// <summary>A card's name line (the mock's <c>.nm</c>: 11.5px display capitals, 1.1) and its small line (<c>.ln</c>: 11px, 1.2).</summary>
    private const float PlateNameCss = 12.65f;

    private const float PlateLnCss = 13.2f;

    /// <summary>The raised well of icons: 6 in, rows of three 4 apart.</summary>
    private static float WellHeight(int rows, float icon) => S(12) + icon * rows + S(4) * (rows - 1);

    /// <summary>"Your glamour plates 14/20", and at the right when the game last had them (shorter, or none, when there's no room).</summary>
    private void DrawPlatesHeading(CharacterWardrobe me)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var h = Kit.DisplayHeight(17);
        var tw = Kit.Display(at, "Your glamour plates", 17, Palette.Ink, w);
        var cx = at.X + tw + S(10);
        var cy = at.Y + Txt.Ascent(Face.Display, 17) - Txt.Ascent(Face.Mono, 12);
        var cw = Txt.Draw(dl, new Vector2(cx, cy), platesFilled, Face.Mono, 12, Palette.Faint);
        if (me.PlatesReadAt is { } readAt)
        {
            var ago = Words.Ago(new DateTimeOffset(readAt, TimeSpan.Zero), DateTimeOffset.UtcNow);
            var room = at.X + w - (cx + cw + S(16));
            var said = $"As the game had them when you last opened Glamour Plates · {ago}";
            if (Txt.Measure(said, Face.Sans, 12).X > room) said = $"Read {ago}";
            var sw = Txt.Measure(said, Face.Sans, 12).X;
            if (sw <= room) Txt.Draw(dl, new Vector2(at.X + w - sw, at.Y + (h - Txt.Natural(Face.Sans, 12)) / 2), said, Face.Sans, 12, Palette.Faint);
        }
        Kit.Reserve(at, w, h);
    }

    /// <summary>A plate's card (the mock's <c>.pl</c>); true when pressed.</summary>
    private bool PlateCard(int plate, Vector2 pos, Vector2 size, float icon, PlateCardInfo card)
    {
        var filled = card.Count > 0;
        var on = plate == pickedPlate;
        var (clicked, hovered, _) = Kit.Hit($"##plate-card{plate}", pos, size);
        var dl = Kit.Dl;
        var r = S(16);
        var max = pos + size;
        Kit.FocusRing(pos, max, r);
        if (on)
        {
            // Lilac-edged on the violet-tinted ground (the site's blush), the edge inset once more (box-shadow: inset 0 0 0 1px).
            dl.AddRectFilled(pos, max, Palette.U32(Palette.Blush), r);
            dl.AddRect(pos + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(Palette.Accent), r, ImDrawFlags.None, 1f);
            dl.AddRect(pos + new Vector2(1.5f), max - new Vector2(1.5f), Palette.U32(Palette.Accent), r - 1, ImDrawFlags.None, 1f);
        }
        else if (filled) Kit.Card(pos, max, Palette.Card, hovered ? Palette.LineHover : Palette.Line, 16);
        else Kit.Dashed(pos, max, r, hovered ? Palette.LineHover : Palette.LineStrong, 1f);

        // The top: its number, the planned dot, and at the right On and the gearsets' jobs.
        var x = pos.X + S(10);
        var y = pos.Y + S(10);
        var inner = size.X - S(20);
        var rowH = S(18);
        var no = PlateNumber(plate);
        var nw = Kit.Display(new Vector2(x, y + (rowH - Kit.DisplayHeight(16)) / 2), no, 16, Palette.Ink, 0, false, 0);
        var after = x + nw + S(6);
        if (card.Planned != null)
        {
            Kit.StatusDot(new Vector2(after + S(3.5f), y + rowH / 2), DotKind.Live);
            after += S(7 + 6);
        }
        var right = pos.X + size.X - S(10);
        if (card.Worn)
        {
            var ow = JobTagWidth("On");
            if (right - ow >= after)
            {
                right -= ow;
                JobTag(new Vector2(right, y), "On", true);
                right -= S(6);
            }
        }
        if (plate < jobsByPlate.Length)
        {
            // Right to left, the last first, so the first linked is leftmost (as the mock's WHM SGE).
            var jobs = jobsByPlate[plate];
            var total = 0f;
            var fit = 0;
            for (var j = 0; j < jobs.Length; j++)
            {
                var jw = JobTagWidth(jobs[j]) + (j > 0 ? S(6) : 0);
                if (right - total - jw < after) break;
                total += jw;
                fit++;
            }
            var jx = right - total;
            for (var j = 0; j < fit; j++)
            {
                jx += JobTag(new Vector2(jx, y), jobs[j]) + S(6);
            }
        }
        y += rowH + S(7);

        if (filled)
        {
            var wellRows = card.Icons.Length > 3 ? 2 : 1;
            var wellH = WellHeight(wellRows, icon);
            dl.AddRectFilled(new Vector2(x, y), new Vector2(x + inner, y + wellH), Palette.U32(Palette.Raised), S(10));
            for (var k = 0; k < card.Icons.Length; k++)
                Kit.GameIcon(new Vector2(x + S(6) + (icon + S(4)) * (k % 3), y + S(6) + (icon + S(4)) * (k / 3)), icon, card.Icons[k], 6);
            y += wellH + S(7);
            var lines = NameLines(card.Name, inner);
            Kit.Display(new Vector2(x, y + (S(PlateNameCss) - Kit.DisplayHeight(12)) / 2), lines.First, 12, Palette.Ink, inner);
            y += S(PlateNameCss);
            if (lines.Second != null)
            {
                Kit.Display(new Vector2(x, y + (S(PlateNameCss) - Kit.DisplayHeight(12)) / 2), lines.Second, 12, Palette.Ink, inner);
                y += S(PlateNameCss);
            }
            y += S(7);
        }
        else
        {
            // The well dashed, "empty" in the hand voice (the site's plates have it so).
            var wellH = S(82);
            Kit.Dashed(new Vector2(x, y), new Vector2(x + inner, y + wellH), S(10), Palette.LineStrong, 1f);
            var ew = Txt.Measure("empty", Face.Hand, 15).X;
            Kit.Hand(new Vector2(x + (inner - ew) / 2, y + (wellH - S(15 * 1.3f)) / 2), "empty", 15, 0, Palette.Faint);
            y += wellH + S(7);
        }
        // A look planned for it that isn't on it yet.
        if (card.Planned != null && !card.OnPlan)
        {
            // Two lines when the room's there (a one-line name, or an empty plate), else one cut short.
            var run = PlannedRun(card.Planned.Name);
            var lines = (int)MathF.Floor((pos.Y + size.Y - S(10) - y + 1) / S(PlateLnCss));
            if (lines >= 2 && Txt.Flow(Vector2.Zero, inner, run, PlateLnCss, out _, draw: false) <= S(PlateLnCss) * 2 + 0.5f)
                Txt.Flow(new Vector2(x, y), inner, run, PlateLnCss, out _);
            else
                Txt.Line(dl, new Vector2(x, y), Txt.Fit(run[0].Text, Face.Sans, 11, inner), Face.Sans, 11, Palette.Muted, PlateLnCss);
        }

        if (hovered)
            Kit.Tooltip($"Plate {plate}", (filled ? card.Match != null ? $"“{card.Match.Name}”" : card.Name : "Empty")
                + (card.Worn ? " · you're wearing it" : string.Empty)
                + (card.Planned != null ? card.OnPlan ? " · planned on the site, and on it" : $" · planned on the site: {card.Planned.Name}" : string.Empty));
        return clicked;
    }

    private static readonly string[] PlateNumbers = Enumerable.Range(0, Plates.Count + 1).Select(n => n.ToString("00")).ToArray();

    private static string PlateNumber(int plate) => plate >= 0 && plate < PlateNumbers.Length ? PlateNumbers[plate] : plate.ToString("00");

    private readonly Run[] plannedRun = new Run[1];

    /// <summary>"Planned: Moonlit Scholar", muted 11px (two lines at most in a card).</summary>
    private Run[] PlannedRun(string name)
    {
        plannedRun[0] = new Run($"Planned: {name}", Face.Sans, 11, Palette.Muted);
        return plannedRun;
    }

    private readonly Dictionary<(string Text, int Width), (string First, string? Second)> nameLines = new();

    /// <summary>A card's name on two lines at most (the mock's line-clamp: 2), the second cut with "…"; worked out once per name and width.</summary>
    private (string First, string? Second) NameLines(string text, float maxWidth)
    {
        var key = (text, (int)maxWidth);
        if (nameLines.TryGetValue(key, out var lines)) return lines;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = string.Empty;
        var i = 0;
        for (; i < words.Length; i++)
        {
            var next = first.Length == 0 ? words[i] : first + " " + words[i];
            if (first.Length > 0 && Kit.DisplayWidth(next, 12) > maxWidth) break;
            first = next;
        }
        lines = (first, i < words.Length ? string.Join(' ', words.Skip(i)) : null);
        if (nameLines.Count >= 256) nameLines.Clear();
        nameLines[key] = lines;
        return lines;
    }

    // ---------------------------------------------------------------------------------------------
    // The job tag (the mock's .tag.job; not a Kit part: the Kit's tags are Figtree).

    /// <summary>A job tag's width: 5 in either side of Geist Mono 10px.</summary>
    private static float JobTagWidth(string text) => S(5) * 2 + Txt.Measure(text, Face.Mono, 10).X;

    /// <summary>A job tag (<c>.tag.job</c>): 18 tall, 6px corners, Geist Mono 10px on the tile, or ice on its wash for On. Returns its width.</summary>
    private static float JobTag(Vector2 pos, string text, bool ok = false)
    {
        var w = JobTagWidth(text);
        var h = S(18);
        Kit.Dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(ok ? Palette.IceWash : Palette.Tile), S(Palette.RadiusTag));
        Txt.Line(Kit.Dl, new Vector2(pos.X + S(5), pos.Y), text, Face.Mono, 10, ok ? Palette.Ice : Palette.Body, 18);
        return w;
    }

    // ---------------------------------------------------------------------------------------------
    // What you're wearing.

    private readonly Run[] wornLine = new Run[1];

    /// <summary>
    /// What you're wearing (the mock's <c>.card.wearing</c>): the eyebrow and whose outfit, up to six of
    /// its pieces, Try it on and Copy my game outfit (the site's words: a new private look from what you
    /// wear; then Open it on the site).
    /// </summary>
    private void DrawWornRow(CharacterWardrobe me)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var cards = me.PlatesReadAt != null ? PlateCards(me) : null;
        var match = PlateMatch(0, me.Outfit);
        var name = me.Name.Length > 0 ? $"{me.Name}'s outfit" : "My outfit";
        var kept = keptFrom.TryGetValue((me.Key, 0), out var k) && Plates.Same(k.Pieces, me.Outfit) ? k.Look : null;
        var busy = keeping.Contains((me.Key, 0));

        // The column at the left: 150 wide, its line wrapping.
        var colW = MathF.Min(S(150), w * 0.3f);
        wornLine[0] = new Run(match != null ? $"Your look “{match.Name}”" : $"{name} · {Words.Count(me.Outfit.Count, "piece")}", Face.Sans, 12, Palette.Muted);
        var lineH = Txt.Flow(Vector2.Zero, colW, wornLine, 15.6f, out _, draw: false);
        var colH = Kit.EyebrowHeight + S(4) + lineH;
        var bh = Kit.ButtonHeight(BtnSize.Sm);
        var innerH = MathF.Max(colH, MathF.Max(S(32), bh));
        var h = S(12) * 2 + innerH;
        Kit.Card(at, at + new Vector2(w, h), Palette.Card, Palette.Line, 18);
        var x0 = at.X + S(14);
        var cy = at.Y + S(12) + innerH / 2;
        var colY = cy - colH / 2;
        Kit.Eyebrow(new Vector2(x0, colY), "What you're wearing", null, colW);
        Txt.Flow(new Vector2(x0, colY + Kit.EyebrowHeight + S(4)), colW, wornLine, 15.6f, out _);

        // The buttons at the right.
        var copyLabel = kept != null ? "Open it on the site" : busy ? "Copying…" : "Copy my game outfit";
        var copyIcon = kept != null ? Icon.ExternalLink : (Icon?)null;
        var copyW = Kit.ButtonWidth(copyLabel, BtnSize.Sm, copyIcon);
        var tryW = Kit.ButtonWidth("Try it on", BtnSize.Sm, Icon.Shirt);
        var right = at.X + w - S(14);
        var by = cy - bh / 2;
        var copyX = right - copyW;
        var tryX = copyX - S(14) - tryW;
        if (Kit.Button("##worn-try", new Vector2(tryX, by), "Try it on", Btn.Glass, BtnSize.Sm, Icon.Shirt, 0, false, "Shows it on you (Glamourer), as it is: nothing's changed in the game", 0, 14))
            Later(() => TryOn(me.Outfit));
        if (kept != null)
        {
            if (Kit.Button("##worn-open", new Vector2(copyX, by), copyLabel, Btn.Glass, BtnSize.Sm, copyIcon, 0, false, $"Copied as “{kept.Name}” in your looks", 0, 14))
                Dalamud.Utility.Util.OpenLink(SiteLink.DressingUrl(kept.Id));
        }
        else if (Kit.Button("##worn-copy", new Vector2(copyX, by), copyLabel, Btn.Glass, BtnSize.Sm, null, 0, !Linked || busy || match != null,
                     match != null ? $"It's your look “{match.Name}” already"
                     : Linked ? $"A new private look of yours on the site from what you wear, as “{name}”, to remix in the dressing room"
                     : "Link your account first (Settings → Account)"))
            KeepAsLook(me, 0, name, me.Outfit);

        // Its pieces between, as many as fit (six at most).
        var ix = x0 + colW + S(14);
        var room = tryX - S(14) - ix;
        var shown = Math.Min(Math.Min(6, cards?[0].Icons.Length ?? WornIcons(me).Length), (int)MathF.Floor((room + S(4)) / S(36)));
        var icons = cards?[0].Icons ?? WornIcons(me);
        for (var i = 0; i < shown; i++)
        {
            var p = new Vector2(ix + S(36) * i, cy - S(16));
            Kit.Item(p, 32, icons[i]);
        }
        Kit.Reserve(at, w, h);
    }

    private (int Version, string Key, uint[] Icons) wornIcons = (-1, string.Empty, Array.Empty<uint>());

    /// <summary>What you wear's first six icons, while the plates (and their cards) aren't read.</summary>
    private uint[] WornIcons(CharacterWardrobe me)
    {
        if (wornIcons.Version == wardrobe.Version && wornIcons.Key == me.Key) return wornIcons.Icons;
        var icons = Slots.All.Where(me.Outfit.ContainsKey).Take(6).Select(s => data.Item(me.Outfit[s].Item)?.Icon ?? 0u).ToArray();
        wornIcons = (wardrobe.Version, me.Key, icons);
        return icons;
    }

    private void KeepAsLook(CharacterWardrobe me, int plate, string name, IReadOnlyDictionary<Slot, Piece> pieces)
    {
        var body = me.Body;
        var key = me.Key;
        var snapshot = new Dictionary<Slot, Piece>(pieces);
        keeping.Add((key, plate));
        sync.SaveLook(name, body, snapshot, r =>
        {
            keeping.Remove((key, plate));
            if (r.Ok)
            {
                keptFrom[(key, plate)] = (r.Value!, snapshot);
                Notify($"Kept as “{r.Value!.Name}” in your looks");
            }
            else Frame.Warn(r.Why ?? "The site said no");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // The plate picked.

    private readonly Run[] asideLine = new Run[2];

    private void DrawPlateAside(CharacterWardrobe me, int plate, PlateCardInfo card, List<Gearset> sets)
    {
        var pieces = PlatePieces(me, plate);
        var match = card.Match;
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        var y = at.Y;

        // Glamour plate · PLATE 3 · what's on it.
        y += Kit.Eyebrow(new Vector2(at.X, y), "Glamour plate", null, w) + S(4);
        // (Bricolage's capitals sit high in their 1.2em box: the mock's line box is 1.02em.)
        Kit.Display(new Vector2(at.X, y - S(34 * 0.09f)), $"Plate {plate}", 34, Palette.Ink, w);
        y += S(34 * 1.02f) + S(4);
        if (match != null)
        {
            asideLine[0] = new Run(match.Name, Face.SansSemibold, 13, Palette.Ink);
            asideLine[1] = new Run(" · your look, already", Face.Sans, 13, Palette.Muted);
        }
        else if (pieces.Count > 0)
        {
            asideLine[0] = new Run(Words.Count(pieces.Count, "piece"), Face.SansSemibold, 13, Palette.Ink);
            asideLine[1] = new Run(" · not one of your looks", Face.Sans, 13, Palette.Muted);
        }
        else
        {
            asideLine[0] = new Run("Empty", Face.SansSemibold, 13, Palette.Ink);
            asideLine[1] = new Run(" · put a look on it at a glamour dresser", Face.Sans, 13, Palette.Muted);
        }
        y += Txt.Flow(new Vector2(at.X, y), w, asideLine, 18, out _);

        // Its plan on the site: the dot, "Planned on the site: <the look>" (pressed, the look opens), On it / Not on it yet.
        if (card.Planned is { } planned)
        {
            y += S(4);
            var lh = S(18);
            Kit.StatusDot(new Vector2(at.X + S(3.5f), y + lh / 2), DotKind.Live);
            var tag = card.OnPlan ? "On it" : "Not on it yet";
            var tagW = Kit.TagWidth(tag);
            var tx = at.X + S(7 + 6);
            var lw = Txt.Draw(dl, new Vector2(tx, y + (lh - Txt.Natural(Face.Sans, 12)) / 2), "Planned on the site: ", Face.Sans, 12, Palette.Muted);
            var nameRoom = at.X + w - tagW - S(6) - tx - lw;
            var nm = Txt.Fit(planned.Name, Face.Sans, 12, MathF.Max(S(20), nameRoom));
            var nmSize = Txt.Measure(nm, Face.Sans, 12);
            var np = new Vector2(tx + lw, y + (lh - Txt.Natural(Face.Sans, 12)) / 2);
            var (open, over, _) = Kit.Hit("##plan-look", np, nmSize);
            Txt.Draw(dl, np, nm, Face.Sans, 12, over ? Palette.Ink : Palette.Muted);
            if (over)
            {
                dl.AddLine(new Vector2(np.X, np.Y + nmSize.Y), new Vector2(np.X + nmSize.X, np.Y + nmSize.Y), Palette.U32(Palette.Ink), 1f);
                Kit.Tooltip("Open the look");
            }
            if (open) Later(() => OpenLook(planned, null, Screen.Plates));
            Kit.Tag(new Vector2(tx + lw + nmSize.X + S(6), y + (lh - Kit.TagHeight()) / 2), tag, card.OnPlan ? TagKind.Ok : TagKind.Ink);
            y += lh;
        }
        Kit.Reserve(at, w, y - at.Y);

        // Wear it (the sticker), Try it on, Keep it as a look.
        Kit.Gap(14);
        var wrap = new Wrap(8);
        const string wearLabel = "Wear it";
        var wearW = Kit.StickerWidth(wearLabel, StickerSize.Md, Icon.Shirt);
        var wearBlocked = pieces.Count == 0 ? "Nothing's on this plate" : Gearsets.Blocked(true);
        // (Its block reaches 3px past it: the wrap leaves that room.)
        if (Kit.Sticker("##wear-plate", wrap.Next(wearW + S(3), Kit.StickerHeight() + S(3)), wearLabel, StickerSize.Md, Icon.Shirt, wearW, wearBlocked != null,
                wearBlocked ?? "Changes to your gearset with this plate, as /gearset change does (in a sanctuary, on a job its gear fits)"))
            Later(() =>
            {
                var why = PlateEditor.Wear(plate);
                recorder.Note($"WEAR plate {plate}: {why ?? "asked the game"}");
                if (why != null) Frame.Warn(why);
            });
        const string tryLabel = "Try it on";
        var tryW = Kit.ButtonWidth(tryLabel, BtnSize.Sm);
        var tryAt = wrap.Next(tryW, Kit.StickerHeight());
        if (Kit.Button("##try-plate", tryAt + new Vector2(0, (Kit.StickerHeight() - Kit.ButtonHeight(BtnSize.Sm)) / 2), tryLabel, Btn.Glass, BtnSize.Sm, null, 0, pieces.Count == 0,
                pieces.Count == 0 ? "Nothing's on this plate" : "Shows it on you (Glamourer): nothing's changed in the game"))
            Later(() => TryOn(pieces));
        if (pieces.Count > 0)
        {
            var kept = keptFrom.TryGetValue((me.Key, plate), out var k) && Plates.Same(k.Pieces, pieces) ? k.Look : null;
            if (kept != null)
            {
                const string open = "Open it on the site";
                var ow = Kit.ButtonWidth(open, BtnSize.Sm, Icon.ExternalLink, Btn.Quiet);
                var oAt = wrap.Next(ow, Kit.StickerHeight());
                if (Kit.Button("##kept", oAt + new Vector2(0, (Kit.StickerHeight() - Kit.ButtonHeight(BtnSize.Sm)) / 2), open, Btn.Quiet, BtnSize.Sm, Icon.ExternalLink, 0, false,
                        $"Kept as “{kept.Name}” in your looks", 0, 14))
                    Dalamud.Utility.Util.OpenLink(SiteLink.DressingUrl(kept.Id));
            }
            else
            {
                var busy = keeping.Contains((me.Key, plate));
                var label = busy ? "Keeping…" : "Keep it as a look";
                var kw = Kit.ButtonWidth(label, BtnSize.Sm, null, Btn.Quiet);
                var kAt = wrap.Next(kw, Kit.StickerHeight());
                if (Kit.Button("##keep", kAt + new Vector2(0, (Kit.StickerHeight() - Kit.ButtonHeight(BtnSize.Sm)) / 2), label, Btn.Quiet, BtnSize.Sm, null, 0, !Linked || busy || match != null,
                        match != null ? $"It's your look “{match.Name}” already"
                        : Linked ? $"Saves it to your looks on Glamour Studio as “Plate {plate}” (private: only you see it)"
                        : "Link your account first (Settings → Account)"))
                    KeepAsLook(me, plate, $"Plate {plate}", pieces);
            }
        }
        wrap.End();

        // Its gearsets (none at all: nothing to link to).
        if (sets.Count > 0)
        {
            Kit.Gap(14);
            DrawGearsets(plate, sets);
        }

        // Its twelve slots.
        Kit.Gap(14);
        var hAt = Kit.At;
        Kit.Display(hAt, "Its twelve slots", 15, Palette.Ink, w);
        Kit.Reserve(hAt, w, S(15 * 1.02f) + S(2));
        for (var i = 0; i < Slots.PlateOrder.Length; i++)
        {
            var slot = Slots.PlateOrder[i];
            var r = Kit.At;
            var rh = S(38);
            if (i > 0) dl.AddLine(new Vector2(r.X, r.Y + 0.5f), new Vector2(r.X + w, r.Y + 0.5f), Palette.U32(Palette.Line), 1f);
            var ly = r.Y + (rh - Txt.Natural(Face.SansSemibold, 11)) / 2;
            Txt.Draw(dl, new Vector2(r.X, ly), Slots.Word(slot), Face.SansSemibold, 11, Palette.Faint);
            var ix = r.X + S(64) + S(11);
            var iy = r.Y + (rh - S(28)) / 2;
            var nx = ix + S(28) + S(11);
            var ny = r.Y + (rh - Txt.Natural(Face.SansSemibold, 13)) / 2;
            if (pieces.TryGetValue(slot, out var piece))
            {
                Kit.Item(new Vector2(ix, iy), 28, data.Item(piece.Item)?.Icon ?? 0);
                var nm = data.ItemName(piece.Item);
                Txt.Draw(dl, new Vector2(nx, ny), Txt.Fit(nm, Face.SansSemibold, 13, r.X + w - nx), Face.SansSemibold, 13, Palette.Ink);
                if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(r, r + new Vector2(w, rh)))
                    Kit.Tooltip(nm, Slots.Word(slot) + (DyeLine(piece) is { } d ? $" · {d}" : " · undyed"));
            }
            else
            {
                Kit.ItemEmpty(new Vector2(ix, iy), 28);
                Txt.Draw(dl, new Vector2(nx, r.Y + (rh - Txt.Natural(Face.Sans, 13)) / 2), "Nothing", Face.Sans, 13, Palette.Faint);
            }
            Kit.Reserve(r, w, rh);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Gearsets.

    private List<Gearset> gearsets = new();
    private DateTime gearsetsAt;

    /// <summary>Each plate's linked gearsets' jobs (their numbers when there's no job), made with the list.</summary>
    private string[][] jobsByPlate = Array.Empty<string[]>();

    /// <summary>Each gearset's number as "03", made with the list.</summary>
    private readonly Dictionary<int, string> gearsetNumbers = new();

    /// <summary>Your gearsets, read again a second after the last read (they change in the game's own window).</summary>
    private List<Gearset> GearsetsNow()
    {
        if (DateTime.UtcNow - gearsetsAt > TimeSpan.FromSeconds(1))
        {
            gearsets = Gearsets.All();
            gearsetsAt = DateTime.UtcNow;
            var jobs = new string[Plates.Count + 1][];
            for (var p = 0; p <= Plates.Count; p++)
                jobs[p] = gearsets.Where(g => g.Plate == p).Select(GearsetJob).ToArray();
            jobsByPlate = jobs;
            foreach (var g in gearsets)
                if (!gearsetNumbers.ContainsKey(g.Number)) gearsetNumbers[g.Number] = g.Number.ToString("00");
        }
        return gearsets;
    }

    private string GearsetJob(Gearset g)
    {
        var job = data.Job(g.Job);
        return job.Length > 0 ? job : g.Number.ToString();
    }

    private string GearsetNumber(Gearset g) => gearsetNumbers.TryGetValue(g.Number, out var n) ? n : g.Number.ToString("00");

    /// <summary>
    /// The plate's gearsets (the mock's card): its heading with a count and Link to a gearset (a menu
    /// of the others with the plate each has now), then each linked one: its number, job and name,
    /// Change to it (the plate comes with it) and × to unlink.
    /// </summary>
    private void DrawGearsets(int plate, List<Gearset> all)
    {
        var linked = all.Where(g => g.Plate == plate).ToList();
        Kit.BeginPanel("##gearsets", 14, 16, 14, Palette.Card, Palette.Line, 18);
        var dl = Kit.Dl;
        var at = Kit.At;
        var w = Kit.Width;

        // The heading row.
        var hh = Kit.ButtonHeight(BtnSize.Tiny);
        var tw = Kit.Display(new Vector2(at.X, at.Y + (hh - Kit.DisplayHeight(15)) / 2), "Gearsets", 15, Palette.Ink, w * 0.5f);
        var cy = at.Y + (hh - Kit.DisplayHeight(15)) / 2 + Txt.Ascent(Face.Display, 15) - Txt.Ascent(Face.Mono, 12);
        Txt.Draw(dl, new Vector2(at.X + tw + S(10), cy), linked.Count.ToString(), Face.Mono, 12, Palette.Faint);
        const string linkLabel = "Link to a gearset";
        var lw = Kit.ButtonWidth(linkLabel, BtnSize.Tiny, Icon.Plus);
        var menuOpen = ImGui.IsPopupOpen("##gs-link-menu");
        if (Kit.Button("##gs-link", new Vector2(at.X + w - lw, at.Y), linkLabel, menuOpen ? Btn.Ink : Btn.Glass, BtnSize.Tiny, Icon.Plus, 0, false,
                "Pick a gearset: changing to it then puts this plate on", 0, 12))
            ImGui.OpenPopup("##gs-link-menu");
        Kit.Reserve(at, w, hh);
        if (Kit.BeginMenu("##gs-link-menu"))
        {
            Kit.MenuHeading("Your gearsets · the plate each has now");
            var others = all.Where(g => g.Plate != plate).ToList();
            if (others.Count == 0) Txt.Note("Every gearset has this plate.", Palette.Muted);
            foreach (var g in others)
            {
                if (GearsetMenuRow($"##gs-pick{g.Id}", g, S(288)))
                {
                    var id = g.Id;
                    Later(() =>
                    {
                        var ok = Gearsets.Link(id, plate);
                        recorder.Note($"GEARSET LINK {id + 1} to plate {plate}: {(ok ? "asked the game" : "couldn't ask")}");
                        if (ok) Notify($"Gearset {id + 1} wears plate {plate} now");
                        else Frame.Warn("The game wouldn't link it");
                        gearsetsAt = DateTime.MinValue;
                    });
                    ImGui.CloseCurrentPopup();
                }
            }
            Kit.EndMenu();
        }

        if (linked.Count == 0)
        {
            Kit.Gap(8);
            Txt.Para(new[] { new Run("Link it to a gearset and changing to that gearset puts the plate on.", Face.Sans, 12, Palette.Muted) }, 18);
        }
        else Kit.Gap(8);
        var equipBlocked = linked.Count > 0 ? Gearsets.Blocked(false) : null;
        for (var i = 0; i < linked.Count; i++)
        {
            var g = linked[i];
            var r = Kit.At;
            var h = S(46);
            dl.AddLine(new Vector2(r.X, r.Y + 0.5f), new Vector2(r.X + w, r.Y + 0.5f), Palette.U32(Palette.Line), 1f);
            var x = r.X;
            Txt.Draw(dl, new Vector2(x, r.Y + (h - Txt.Natural(Face.MonoMedium, 12)) / 2), GearsetNumber(g), Face.MonoMedium, 12, Palette.Muted);
            x += S(22) + S(10);
            var job = GearsetJob(g);
            x += JobTag(new Vector2(x, r.Y + (h - S(18)) / 2), job) + S(10);
            const string change = "Change to it";
            var cw = Kit.ButtonWidth(change, BtnSize.Tiny);
            var ux = r.X + w - S(28);
            var bx = ux - S(10) - cw;
            Txt.Draw(dl, new Vector2(x, r.Y + (h - Txt.Natural(Face.Sans, 13)) / 2), Txt.Fit(g.Name, Face.Sans, 13, MathF.Max(0, bx - S(10) - x)), Face.Sans, 13, Palette.Ink);
            var id = g.Id;
            if (Kit.Button($"##gs-equip{i}", new Vector2(bx, r.Y + (h - Kit.ButtonHeight(BtnSize.Tiny)) / 2), change, Btn.Glass, BtnSize.Tiny, null, 0, equipBlocked != null,
                    equipBlocked ?? (Gearsets.InSanctuary ? "Changes to this gearset: its plate comes with it" : "Changes to this gearset (its plate goes on only in a sanctuary: a city, an inn or a residential area)")))
                Later(() =>
                {
                    var why = Gearsets.Equip(id);
                    recorder.Note($"GEARSET EQUIP {id + 1}: {why ?? "the game took it"}");
                    if (why != null) Frame.Warn(why);
                    else if (!Gearsets.InSanctuary) Notify("Changed. Its plate goes on only in a sanctuary.");
                });
            if (Kit.IconButton($"##gs-unlink{i}", new Vector2(ux, r.Y + (h - S(28)) / 2), Icon.X, "Unlink the plate from this gearset", 28, Btn.Quiet, false, 14))
                Later(() =>
                {
                    var ok = Gearsets.Link(id, 0);
                    recorder.Note($"GEARSET UNLINK {id + 1} (was plate {plate}): {(ok ? "asked the game" : "couldn't ask")}");
                    if (ok) Notify($"Gearset {id + 1} has no plate now");
                    else Frame.Warn("The game wouldn't unlink it");
                    gearsetsAt = DateTime.MinValue;
                });
            Kit.Reserve(r, w, h);
        }
        Kit.EndPanel();
    }

    /// <summary>
    /// A row of Link to a gearset's menu (the mock's <c>.mi</c> with a number and a job): the
    /// gearset's number in mono, its job tag, its name, and the plate it has now at the right. True
    /// when pressed.
    /// </summary>
    private bool GearsetMenuRow(string id, Gearset g, float w)
    {
        var pos = ImGui.GetCursorScreenPos();
        var h = S(34);
        var (clicked, hovered, _) = Kit.Hit(id, pos, new Vector2(w, h));
        Kit.FocusRing(pos, pos + new Vector2(w, h), S(10));
        var dl = Kit.Dl;
        if (hovered) dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(Palette.AccentWash), S(10));
        var x = pos.X + S(10);
        x += Txt.Line(dl, new Vector2(x, pos.Y), GearsetNumber(g), Face.Mono, 12, Palette.Muted, 34) + S(9);
        x += JobTag(new Vector2(x, pos.Y + (h - S(18)) / 2), GearsetJob(g)) + S(9);
        var key = g.Plate > 0 ? $"plate {g.Plate}" : "none";
        var kw = Txt.Measure(key, Face.Mono, 11).X;
        var right = pos.X + w - S(10);
        Txt.Line(dl, new Vector2(right - kw, pos.Y), key, Face.Mono, 11, Palette.Faint, 34);
        Txt.Line(dl, new Vector2(x, pos.Y), Txt.Fit(g.Name, Face.SansMedium, 13, MathF.Max(0, right - kw - S(12) - x)), Face.SansMedium, 13, hovered ? Palette.Ink : Palette.Body, 34);
        Kit.Reserve(pos, w, h);
        return clicked;
    }
}

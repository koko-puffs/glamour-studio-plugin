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
/// A look, opened: its pieces laid out as the game's gear window lays them (weapon and armour down the
/// left, the off hand and accessories down the right), each with where it is, and in the middle how
/// ready it is for a plate (the ring), its dyes and Try it on. On the right what to do: Put on plate N
/// with the plate editor open (or how to get there; after filling, Wear plate N), the pieces to put
/// away first (Into the armoire while it's open), and a shot of it worn for the site.
/// </summary>
public sealed partial class MainWindow
{
    private Look? openLook;

    /// <summary>The look as it was sent from the site (done with once it's on a plate).</summary>
    private SentLook? openSent;

    /// <summary>The last plate filled, and with which look.</summary>
    private (string LookId, FillResult Result, PlatePlan Plan)? lastFill;

    private void OpenLook(Look look, SentLook? sent, Screen from)
    {
        openLook = look;
        openSent = sent;
        if (from != Screen.Look && from != Screen.Settings) lookFrom = from;
        screen = Screen.Look;
        IsOpen = true;
    }

    private (IReadOnlyDictionary<Slot, Piece> Pieces, int Wardrobe, bool Armoire, bool Clear, bool Dye, IReadOnlyDictionary<byte, int> Bag) planFor;
    private PlatePlan? plan;

    /// <summary>The plan for a look, worked out again only when the wardrobe or the settings changed.</summary>
    private PlatePlan PlanCached(Look look)
    {
        var key = (look.Pieces, wardrobe.Version, wardrobe.ArmoireLive, config.ClearBare, config.DyeFromBags, wardrobe.BagDyes);
        if (plan == null || key != planFor)
        {
            planFor = key;
            plan = PlanFor(look.Pieces);
        }
        return plan;
    }

    /// <summary>How the look would go on the plate open now (or any plate: the dresser and armoire as last read).</summary>
    private PlatePlan PlanFor(IReadOnlyDictionary<Slot, Piece> pieces)
    {
        var me = wardrobe.Current;
        var dresser = (IReadOnlyList<DresserPiece>?)me?.Dresser ?? Array.Empty<DresserPiece>();
        IReadOnlyDictionary<uint, uint> armoire = wardrobe.ArmoireLive
            ? wardrobe.ArmoireNow
            : (me?.Get(Place.Armoire)?.Items ?? new HashSet<uint>())
                .Select(i => (Item: i, Row: data.CabinetOf(i)))
                .Where(x => x.Row != null)
                .ToDictionary(x => x.Item, x => x.Row!.Value);
        return PlatePlanner.Plan(pieces, dresser, armoire, data.DyeChannels, Where,
            new PlateOptions(config.ClearBare, config.DyeFromBags ? wardrobe.BagDyes : null));
    }

    private static readonly Slot[] LeftSlots = { Slot.Main, Slot.Head, Slot.Body, Slot.Hands, Slot.Legs, Slot.Feet };
    private static readonly Slot[] RightSlots = { Slot.Off, Slot.Ears, Slot.Neck, Slot.Wrists, Slot.RingR, Slot.RingL };

    private void DrawLook(Look look, Vector2 min, Vector2 size)
    {
        var pad = S(SideCss);
        var top = S(20);
        var sideW = S(340);
        var gap = S(20);
        var inner = new Vector2(size.X - pad * 2, size.Y - top * 2);
        var gearW = inner.X - sideW - gap;
        var gearMin = new Vector2(min.X + pad, min.Y + top);
        var plate = editorPlate;
        var thePlan = PlanCached(look);
        DrawGear(look, gearMin, new Vector2(gearW, inner.Y), plate > 0 ? thePlan : null);

        var sideMin = new Vector2(gearMin.X + gearW + gap, gearMin.Y);
        Kit.BeginPane("##look-side", sideMin, new Vector2(sideW, inner.Y), 0, 0, 0);
        DrawPlateCard(look, compact: false);
        var ready = ReadinessOf(look);
        if (ready.ToStore > 0)
        {
            Kit.Gap(14);
            DrawPutAway(look);
        }
        if (look.Visibility == "public")
        {
            Kit.Gap(14);
            DrawShot(look);
        }
        Kit.EndPane();
    }

    // ---------------------------------------------------------------------------------------------
    // The gear window.

    private void DrawGear(Look look, Vector2 min, Vector2 size, PlatePlan? withPlan)
    {
        var dl = Kit.Dl;
        Kit.Card(min, min + size);
        var pad = S(18);
        var inner = new Vector2(size.X - pad * 2, size.Y - pad * 2);
        var centerW = S(168);
        var gap = S(8);
        var colW = (inner.X - centerW - gap * 2) / 2;
        var p0 = min + new Vector2(pad);
        var rowH = S(7) * 2 + S(44);
        var space = MathF.Max(0, (inner.Y - rowH * 6) / 5);
        for (var i = 0; i < 6; i++)
        {
            var y = p0.Y + (rowH + space) * i;
            GearSlot(look, LeftSlots[i], new Vector2(p0.X, y), colW, rowH, false, withPlan);
            GearSlot(look, RightSlots[i], new Vector2(p0.X + colW + gap + centerW + gap, y), colW, rowH, true, withPlan);
        }

        // The middle: how ready, the dyes, Try it on.
        var cx = p0.X + colW + gap;
        dl.AddLine(new Vector2(cx + 0.5f, p0.Y), new Vector2(cx + 0.5f, p0.Y + inner.Y), Palette.U32(Palette.Divider), 1f);
        dl.AddLine(new Vector2(cx + centerW - 0.5f, p0.Y), new Vector2(cx + centerW - 0.5f, p0.Y + inner.Y), Palette.U32(Palette.Divider), 1f);
        var ready = ReadinessOf(look);
        var dyes = look.Pieces.Values.SelectMany(p => new[] { p.Dye1, p.Dye2 }).Where(d => d != 0).Distinct().Take(8).ToList();
        var cw = centerW - S(20);
        var lineRuns = new[] { new Run(ReadyLine(look, ready), Face.Sans, 12, Palette.Muted) };
        var lineH = Txt.Flow(Vector2.Zero, cw, lineRuns, 18, out _, center: true, draw: false);
        var subH = Txt.Natural(Face.Mono, 11);
        var ringD = S(112);
        var tryH = Kit.ButtonHeight(BtnSize.Sm);
        var dyesH = dyes.Count > 0 ? subH + S(6) + S(14) : 0;
        var blockH = subH + S(14) + ringD + S(14) + lineH + (dyes.Count > 0 ? S(14) + dyesH : 0) + S(14) + tryH;
        var y0 = p0.Y + MathF.Max(0, (inner.Y - blockH) / 2);
        var mid = cx + centerW / 2;
        var label = ready.PlateReady || ready.Ready > 0 ? "Ready for a plate" : "None ready yet";
        Kit.Sub(new Vector2(mid - Kit.SubWidth(label) / 2, y0), label);
        y0 += subH + S(14);
        var center = new Vector2(mid, y0 + ringD / 2);
        Kit.Ring(center, ringD / 2, S(9), ready.Total == 0 ? 0 : (float)ready.Ready / ready.Total);
        var num = ready.Ready.ToString();
        var numSize = Txt.Measure(num, Face.Display, 28);
        var ofText = $"of {Words.Count(ready.Total, "piece")}";
        var ofSize = Txt.Measure(ofText, Face.Sans, 11);
        var stackH = numSize.Y + ofSize.Y;
        Txt.Draw(dl, new Vector2(mid - numSize.X / 2, center.Y - stackH / 2), num, Face.Display, 28, Palette.Ink);
        Txt.Draw(dl, new Vector2(mid - ofSize.X / 2, center.Y - stackH / 2 + numSize.Y), ofText, Face.Sans, 11, Palette.Muted);
        y0 += ringD + S(14);
        Txt.Flow(new Vector2(cx + S(10), y0), cw, lineRuns, 18, out _, center: true);
        y0 += lineH;
        if (dyes.Count > 0)
        {
            y0 += S(14);
            Kit.Sub(new Vector2(mid - Kit.SubWidth("Dyes") / 2, y0), "Dyes");
            y0 += subH + S(6);
            var sw = S(14);
            var sg = S(6);
            var total = dyes.Count * sw + (dyes.Count - 1) * sg;
            var sx = mid - total / 2;
            foreach (var d in dyes)
            {
                var c = new Vector2(sx + sw / 2, y0 + sw / 2);
                var info = data.Dye(d);
                if (info != null) dl.AddCircleFilled(c, sw / 2, Palette.U32(Palette.FromRgb(info.Rgb)), 24);
                dl.AddCircle(c, sw / 2 - 0.5f, Palette.U32(Palette.WithAlpha(Palette.White, 0.34f)), 24, 1f);
                if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(c - new Vector2(sw / 2), c + new Vector2(sw / 2)))
                    Kit.Tooltip(data.DyeName(d));
                sx += sw + sg;
            }
            y0 += sw;
        }
        y0 += S(14);
        const string tryLabel = "Try it on";
        var tw = Kit.ButtonWidth(tryLabel, BtnSize.Sm, Icon.Eye);
        if (Kit.Button("##gear-try", new Vector2(mid - tw / 2, y0), tryLabel, Btn.Glass, BtnSize.Sm, Icon.Eye, 0, false,
                "Opens the game's fitting room with the look on you (any piece, yours or not)"))
            Later(() => TryOn(look.Pieces));
    }

    /// <summary>The line under the ring: what stands between the look and a plate.</summary>
    private string ReadyLine(Look look, Readiness r)
    {
        if (r.Total == 0) return "It has no pieces.";
        if (r.PlateReady) return "Every piece is in your dresser or armoire.";
        var away = look.Pieces.Values.Select(p => Where(p.Item)).Where(p => p is { } x && x != Place.Dresser && x != Place.Armoire).Select(p => p!.Value).ToList();
        var parts = new List<string>();
        if (away.Count == 1) parts.Add($"One is {Places.Where(away[0])}: put it away");
        else if (away.Count > 1) parts.Add($"{Words.InWords(away.Count)} are elsewhere: put them away");
        if (r.Missing > 0) parts.Add($"{Words.InWords(r.Missing).ToLowerInvariant()} you don't have yet");
        var line = string.Join("; ", parts);
        if (r.Missing == 0) line += $" and all {Words.InWords(r.Total).ToLowerInvariant()} go on";
        return line + ".";
    }

    /// <summary>A slot of the gear window: its piece's icon, name, slot and dyes, and where it is (on the right side, mirrored).</summary>
    private void GearSlot(Look look, Slot slot, Vector2 pos, float width, float height, bool right, PlatePlan? withPlan)
    {
        var dl = Kit.Dl;
        var hover = !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + new Vector2(width, height));
        if (hover) dl.AddRectFilled(pos, pos + new Vector2(width, height), Palette.U32(Palette.RowHover), S(12));
        var icon = S(44);
        var iconX = right ? pos.X + width - S(8) - icon : pos.X + S(8);
        var iconY = pos.Y + (height - icon) / 2;
        var tx = right ? pos.X + S(8) : iconX + icon + S(10);
        var tw = width - S(16) - icon - S(10);
        var has = look.Pieces.TryGetValue(slot, out var piece);
        string name, line, where;
        Vector4 whereColor;
        string? tip = null;
        if (has)
        {
            var item = data.Item(piece.Item);
            Kit.GameIcon(new Vector2(iconX, iconY), icon, item?.Icon ?? 0);
            name = item?.Name ?? $"Item {piece.Item}";
            line = Slots.Word(slot) + (DyeLine(piece) is { } d ? $" · {d}" : string.Empty);
            var w = Where(piece.Item);
            var step = withPlan?.Steps.FirstOrDefault(s => s.Slot == slot);
            (where, whereColor) = w switch
            {
                Place.Dresser => ("Dresser", Palette.Success),
                Place.Armoire => ("Armoire", Palette.Success),
                { } p => ($"{Places.Word(p)}: put it away", Palette.RoseText),
                null => (sync.Library.Wishes.Contains(LookJson.BaseId(piece.Item)) ? "On your list" : "Not yours yet", Palette.Dim),
            };
            if (step is { DyeFromBags: true }) where += " · dyed from your bags";
            else if (step is { DyeLeft: true }) (where, whereColor) = (where + " · dye it in the editor", Palette.RoseText);
            tip = $"{name}\n{line}\n{WhereChip(w, sync.Library.Wishes.Contains(LookJson.BaseId(piece.Item))).Tip}";
        }
        else
        {
            Kit.EmptyTile(new Vector2(iconX, iconY), icon, Palette.RadiusSm, 0.5f);
            name = $"No {Slots.Word(slot).ToLowerInvariant()}";
            line = Slots.Word(slot);
            var weapon = slot is Slot.Main or Slot.Off;
            where = weapon || !config.ClearBare ? "Left as it is" : "Emptied on the plate";
            whereColor = Palette.Dim;
        }
        var nameH = Txt.Natural(Face.SansMedium, 13);
        var lineH = Txt.Natural(Face.Sans, 11);
        var block = nameH + S(2) + lineH + S(2) + lineH;
        var y = pos.Y + (height - block) / 2;
        void Text(string t, Face f, int px, Vector4 c)
        {
            var fit = Txt.Fit(t, f, px, tw);
            var wdt = Txt.Measure(fit, f, px).X;
            Txt.Draw(dl, new Vector2(right ? tx + tw - wdt : tx, y), fit, f, px, c);
        }
        Text(name, Face.SansMedium, 13, has ? Palette.Ink : Palette.Muted);
        y += nameH + S(2);
        Text(line, Face.Sans, 11, Palette.Dim);
        y += lineH + S(2);
        // Where, with its dot (before the words on the left, after them on the right).
        var wfit = Txt.Fit(where, Face.Sans, 11, tw - S(11));
        var ww = Txt.Measure(wfit, Face.Sans, 11).X;
        var dotY = y + lineH / 2;
        if (right)
        {
            Txt.Draw(dl, new Vector2(tx + tw - ww - S(11), y), wfit, Face.Sans, 11, whereColor);
            Kit.Dot(new Vector2(tx + tw - S(3), dotY), whereColor, 6);
        }
        else
        {
            Kit.Dot(new Vector2(tx + S(3), dotY), whereColor, 6);
            Txt.Draw(dl, new Vector2(tx + S(11), y), wfit, Face.Sans, 11, whereColor);
        }
        if (hover && tip != null) Kit.Tooltip(tip);
    }

    // ---------------------------------------------------------------------------------------------
    // Putting it on a plate.

    /// <summary>
    /// The plate card: with the plate editor open, Put on plate N and what it will do (compact in the
    /// Looks side pane); closed, how to get there; just filled, what happened and Wear plate N.
    /// </summary>
    private void DrawPlateCard(Look look, bool compact)
    {
        var plate = editorPlate;
        var filled = lastFill is { } lf && lf.LookId == look.Id ? lf : ((string, FillResult, PlatePlan)?)null;
        // What was just filled stays (with Wear plate N) after the plate editor closes; a different plate open fills afresh.
        if (filled is { } f && (plate == 0 || f.Item2.Plate == plate))
        {
            DrawFilled(f.Item2, f.Item3);
            return;
        }
        var w = Kit.Width;
        if (plate > 0)
        {
            var thePlan = PlanCached(look);
            Kit.BeginPanel("##put", compact ? 14 : 18, compact ? 14 : 18, compact ? 14 : 18, Palette.RoseWash, Palette.RoseEdge);
            if (!compact)
            {
                Kit.EyebrowRow($"Plate editor open · plate {plate}");
                Kit.Gap(12);
            }
            var at = Kit.At;
            var iw = Kit.Width;
            var size = compact ? BtnSize.Md : BtnSize.Lg;
            var can = thePlan.Ready > 0 || thePlan.Steps.Any(s => s.Kind == StepKind.Clear);
            if (Kit.Button("##put-on", at, $"Put on plate {plate}", Btn.Primary, size, Icon.Layers, iw, !can,
                    can ? "Fills the plate open in the game. Nothing's saved until you press Save there." : "None of its pieces is in your dresser or armoire"))
                Later(() => Fill(look, thePlan));
            Kit.Reserve(at, iw, Kit.ButtonHeight(size));
            Kit.Gap(12);
            Txt.Para(PlanWords(thePlan, compact), 19.5f);
            if (!compact)
            {
                Kit.Gap(12);
                Txt.Para(new[] { new Run("Nothing's saved until you press Save in the game.", Face.Sans, 12, Palette.Muted) }, 18);
            }
            if (openSent?.Plate is { } meant && meant != plate && openSent.Look.Id == look.Id)
            {
                Kit.Gap(8);
                Txt.Note($"It was sent for plate {meant}: pick plate {meant} in the game's window to put it there.", Palette.RoseText);
            }
            Kit.EndPanel();
            return;
        }
        Kit.BeginPanel("##how", 14, 16, 14, Palette.Card, Palette.Line, 14);
        var a = Kit.At;
        var iw2 = Kit.Width;
        Icons.Draw(Kit.Dl, Icon.Layers, new Vector2(a.X, a.Y + S(2)), S(16), Palette.Rose);
        var tx = a.X + S(16) + S(12);
        var h = Txt.Flow(new Vector2(tx, a.Y), iw2 - (tx - a.X), new[]
        {
            new Run("To put it on a plate, ", Face.SansSemibold, 13, Palette.Ink),
            new Run("open Glamour Plates at a glamour dresser and pick a plate. The button shows here.", Face.Sans, 13, Palette.Soft),
        }, 19.5f, out _);
        Kit.Reserve(a, iw2, h);
        Kit.EndPanel();
        _ = w;
    }

    /// <summary>What filling will do, in a line or two (shorter in the side pane).</summary>
    private Run[] PlanWords(PlatePlan plan, bool compact = false)
    {
        var runs = new List<Run>();
        var missing = plan.Missing.ToList();
        var head = missing.Count == 0
            ? $"All {Words.Count(plan.Wanted, "piece")} go on."
            : $"{plan.Ready} of {Words.Count(plan.Wanted, "piece")} go on.";
        runs.Add(new Run(head + " ", Face.SansSemibold, 13, Palette.Ink));
        if (missing.Count > 0)
        {
            var stored = missing.Where(m => m.Elsewhere != null).ToList();
            var gone = missing.Count - stored.Count;
            var bits = new List<string>();
            if (stored.Count > 0)
                bits.Add(compact
                    ? $"{Words.Count(stored.Count, "piece")} to put away first"
                    : $"{Words.List(stored.Select(m => data.ItemName(m.Want!.Value.Item)))} {(stored.Count == 1 ? "is" : "are")} {Places.Where(stored[0].Elsewhere!.Value)}{(stored.Count > 1 ? " and the like" : string.Empty)}");
            if (gone > 0) bits.Add($"{Words.Count(gone, "piece")} you don't have");
            runs.Add(new Run(string.Join("; ", bits) + ". ", Face.Sans, 13, Palette.Soft));
        }
        if (compact) return runs.ToArray();
        var dyes = plan.Steps.Count(s => s.DyeLeft);
        if (dyes > 0) runs.Add(new Run($"{Words.Count(dyes, "piece")} will need dyeing in the plate editor. ", Face.Sans, 13, Palette.Soft));
        var bag = plan.BagDyes;
        if (bag.Count > 0) runs.Add(new Run($"Dyes from your bags: {Words.List(bag.Select(kv => $"{data.DyeName(kv.Key)}{(kv.Value > 1 ? $" ×{kv.Value}" : string.Empty)}"))}. ", Face.Sans, 13, Palette.Soft));
        var cleared = plan.Steps.Count(s => s.Kind == StepKind.Clear);
        if (cleared > 0 && config.ClearBare) runs.Add(new Run("The slots it leaves bare are emptied.", Face.Sans, 13, Palette.Muted));
        return runs.ToArray();
    }

    private void Fill(Look look, PlatePlan plan)
    {
        if (!PlateEditor.IsOpen)
        {
            Notify("The plate editor closed");
            return;
        }
        // The dresser and armoire as the game has them this moment.
        wardrobe.Tick(now: true);
        plan = PlanFor(look.Pieces);
        var result = PlateEditor.Fill(plan, wardrobe.FindDye);
        recorder.Note($"FILL plate {result.Plate} with “{look.Name}” ({look.Id}): set {result.Set}, cleared {result.Cleared}, dyed {result.Dyed}"
                      + (result.Problems.Count > 0 ? $"; problems: {string.Join(" | ", result.Problems)}" : string.Empty));
        // Nothing went on (the editor closed meanwhile): nothing to show as filled.
        if (result.Plate > 0 && result.Set + result.Cleared > 0) lastFill = (look.Id, result, plan);
        Notify(result.Ok ? $"Plate {result.Plate}: done. Press Save in the game's window." : result.Problems[0]);
        // A look sent from the site is done with once it's on a plate (only that look: Home's planned fill isn't it).
        if (openSent != null && openSent.Look.Id == look.Id && result.Set > 0)
        {
            sync.DoneWith(openSent.Id);
            openSent = null;
        }
    }

    /// <summary>After filling: what happened, and what's next (Save in the game; then Wear it).</summary>
    private void DrawFilled(FillResult result, PlatePlan plan)
    {
        Kit.BeginPanel("##filled", 16, 18, 16, Palette.SuccessWash, Palette.SuccessEdge);
        var at = Kit.At;
        var w = Kit.Width;
        Kit.Round(at, S(40), Icon.Check, S(18), Palette.WithAlpha(Palette.Success, 0.16f), Palette.Success, 2.2f);
        var x = at.X + S(40) + S(14);
        var tw = w - (x - at.X);
        Txt.Line(Kit.Dl, new Vector2(x, at.Y), $"On plate {result.Plate}", Face.SansSemibold, 14, Palette.Ink);
        var line = Words.Filled(result.Plate, plan, result.Set);
        if (result.Problems.Count > 0) line += " " + string.Join(" ", result.Problems);
        var h = Txt.Flow(new Vector2(x, at.Y + Txt.Natural(Face.SansSemibold, 14) + S(2)), tw, new[] { new Run(line, Face.Sans, 13, Palette.Soft) }, 19.5f, out _);
        Kit.Reserve(at, w, MathF.Max(S(40), Txt.Natural(Face.SansSemibold, 14) + S(2) + h));
        Kit.Gap(12);
        var row = Kit.At;
        var saved = !editorChanges;
        var wear = $"Wear plate {result.Plate}";
        if (Kit.Button("##wear", row, wear, Btn.Glass, BtnSize.Sm, Icon.Shirt, 0, !saved,
                saved ? "Puts the plate on your current gearset, as /gearset change does (in a city or an inn, on a job its gear fits)" : "Save the plate in the game first"))
            Later(() =>
            {
                if (!PlateEditor.Wear(result.Plate)) Notify("You're on no gearset: change to one first");
            });
        var x2 = row.X + Kit.ButtonWidth(wear, BtnSize.Sm, Icon.Shirt) + S(8);
        if (Kit.Button("##again", new Vector2(x2, row.Y), "Fill it again", Btn.Quiet, BtnSize.Sm, Icon.Refresh))
            lastFill = null;
        Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Sm));
        Kit.EndPanel();
    }

    private void TryOn(IReadOnlyDictionary<Slot, Piece> pieces)
    {
        var n = PlateEditor.TryOn(pieces);
        recorder.Note($"TRY ON {pieces.Count} pieces: {n} went to the fitting room");
        Notify(n > 0 ? "In the fitting room" : "The fitting room didn't open");
    }

    // ---------------------------------------------------------------------------------------------
    // Putting away: the look's pieces that are yours but not where a plate takes them from.

    /// <summary>Armoire rows asked to be stored, and when (a second ask waits for the game's answer).</summary>
    private readonly Dictionary<uint, DateTime> storing = new();

    private void DrawPutAway(Look look)
    {
        var away = Slots.All.Where(look.Pieces.ContainsKey).Select(s => look.Pieces[s].Item)
            .Select(i => (Item: i, Where: Where(i)))
            .Where(x => x.Where is { } p && p != Place.Dresser && p != Place.Armoire)
            .ToList();
        if (away.Count == 0) return;
        var armoireOpen = Armoire.IsOpen;
        var now = DateTime.UtcNow;
        Kit.BeginPanel("##away", 16, 16, 16);
        var hat = Kit.At;
        Kit.Heading("Put away first", Words.Count(away.Count, "piece"), 14);
        if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(hat, new Vector2(hat.X + Kit.Width, Kit.At.Y)))
            Kit.Tooltip(armoireOpen
                ? "The armoire's open: the ones it takes go in from your bags or armoury chest. The rest go in your dresser."
                : "At a glamour dresser, store these from your bags or armoury chest (take them from saddlebags or retainers first). At an inn, with the armoire open, the ones it takes go in with a press.");
        Kit.Gap(10);
        var dl = Kit.Dl;
        for (var i = 0; i < away.Count; i++)
        {
            var (itemId, where) = away[i];
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(4) * 2 + S(34);
            var item = data.Item(itemId);
            Kit.GameIcon(new Vector2(at.X, at.Y + S(4)), S(34), item?.Icon ?? 0);
            var x = at.X + S(34) + S(10);
            var row = data.CabinetOf(itemId);
            var worn = row != null && armoireOpen && Armoire.IsWorn(itemId);
            var canArmoire = row != null && where is Place.Inventory or Place.Armoury && armoireOpen && !worn;
            // Asked already: the game takes a moment to answer, and a second ask would be a second store.
            var asked = row is { } rr && storing.TryGetValue(rr, out var askedAt) && now - askedAt < TimeSpan.FromSeconds(5);
            var put = asked ? "Storing…" : "Into the armoire";
            var bw = canArmoire ? Kit.ButtonWidth(put, BtnSize.Sm, Icon.Archive) : 0;
            var tw = at.X + w - (bw > 0 ? bw + S(10) : 0) - x;
            var nh = Txt.Natural(Face.Sans, 13);
            var lh = Txt.Natural(Face.Sans, 11);
            var y = at.Y + (h - nh - lh) / 2;
            Txt.Draw(dl, new Vector2(x, y), Txt.Fit(item?.Name ?? $"Item {itemId}", Face.Sans, 13, tw), Face.Sans, 13, Palette.Ink);
            var line = $"{Places.Word(where!.Value)}" + (row == null ? " · for the dresser" : worn ? " · take it off for the armoire" : " · the armoire takes it");
            Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(line, Face.Sans, 11, tw), Face.Sans, 11, Palette.Dim);
            if (bw > 0)
            {
                var cabinet = row!.Value;
                var name = item?.Name ?? "It";
                if (Kit.Button($"##armoire{i}", new Vector2(at.X + w - bw, at.Y + (h - Kit.ButtonHeight(BtnSize.Sm)) / 2), put, Btn.Glass, BtnSize.Sm, Icon.Archive, 0, asked))
                    Later(() =>
                    {
                        if (storing.TryGetValue(cabinet, out var t) && DateTime.UtcNow - t < TimeSpan.FromSeconds(5)) return;
                        var ok = Armoire.Store(cabinet);
                        recorder.Note($"ARMOIRE STORE row {cabinet} ({name}): the game {(ok ? "took the ask" : "said no")}");
                        if (!ok)
                        {
                            Notify("The armoire didn't take it");
                            return;
                        }
                        storing[cabinet] = DateTime.UtcNow;
                        wardrobe.Tick(now: true);
                        Notify($"{name}: in the armoire");
                    });
            }
            Kit.Reserve(at, w, h);
        }
        Kit.EndPanel();
    }

    // ---------------------------------------------------------------------------------------------
    // A shot from the game for the look's Worn in game on the site.

    private string shotCaption = string.Empty;

    private void DrawShot(Look look)
    {
        Kit.BeginPanel("##shot-card", 16, 16, 16);
        Kit.Heading("Worn in game", "A shot for the site", 14);
        Kit.Gap(12);
        var w = Kit.Width;
        var taken = shots.Taken is { } t && t.LookId == look.Id ? t : null;
        var dl = Kit.Dl;
        if (taken == null)
        {
            // The dashed box, filling what's left of the pane (90 at least).
            var at = Kit.At;
            var boxH = MathF.Max(S(90), Kit.PaneRoom() - S(16) - Kit.ButtonHeight(BtnSize.Md) - S(12) - (shots.Problem != null ? S(30) : 0) - S(2));
            Kit.Dashed(at, at + new Vector2(w, boxH), S(12), Palette.LineDashed, 1f);
            var line = "Wear it, frame it, take a shot. You see it here before it's sent.";
            var runs = new[] { new Run(line, Face.Sans, 12, Palette.Muted) };
            var lh = Txt.Flow(Vector2.Zero, w - S(24), runs, 18, out _, center: true, draw: false);
            var block = S(22) + S(8) + lh;
            var y = at.Y + (boxH - block) / 2;
            Icons.Draw(dl, Icon.Camera, new Vector2(at.X + (w - S(22)) / 2, y), S(22), Palette.Rose);
            Txt.Flow(new Vector2(at.X + S(12), y + S(22) + S(8)), w - S(24), runs, 18, out _, center: true);
            Kit.Reserve(at, w, boxH);
            Kit.Gap(12);
            var row = Kit.At;
            var label = shots.Capturing ? "Taking it…" : "Take a shot";
            if (Kit.Button("##shot", row, label, Btn.Glass, BtnSize.Md, Icon.Camera, w, !Linked || shots.Capturing,
                    Linked ? "Hides the HUD, takes the game's picture, and shows it here to send" : "Link your account first (Settings → Account)"))
            {
                var id = look.Id;
                var name = look.Name;
                Later(() =>
                {
                    shotCaption = string.Empty;
                    recorder.Note($"SHOT taking for “{name}” ({id})");
                    if (!shots.Take(id, name, out var why)) Notify(why);
                });
            }
            Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Md));
            if (shots.Problem is { } problem && !shots.Capturing)
            {
                Kit.Gap(8);
                Txt.Para(new[] { new Run(problem, Face.Sans, 12, Palette.Danger) }, 18);
            }
            Kit.EndPanel();
            return;
        }

        if (taken.Thumb is { } thumb && thumb.Width > 0 && thumb.Height > 0)
        {
            var at = Kit.At;
            var h = MathF.Min(w * thumb.Height / thumb.Width, S(220));
            var iw = h * thumb.Width / thumb.Height;
            var p = new Vector2(at.X + (w - iw) / 2, at.Y);
            dl.AddImageRounded(thumb.Handle, p, p + new Vector2(iw, h), Vector2.Zero, Vector2.One, Palette.U32(Vector4.One), S(12));
            Kit.Reserve(at, w, h);
            Kit.Gap(8);
        }
        if (taken.Jpeg is { } jpeg) Txt.Para(new[] { new Run($"{taken.Width} × {taken.Height} · {jpeg.Length / 1024} KB", Face.Sans, 12, Palette.Dim) }, 18);
        Kit.Gap(10);
        if (taken.Sent)
        {
            if (taken.Note is { } n) Txt.Para(new[] { new Run(n.Text, Face.Sans, 13, Palette.Success) }, 19.5f);
            Kit.Gap(10);
            var row = Kit.At;
            if (Kit.Button("##shot-site", row, "See it on the site", Btn.Glass, BtnSize.Sm, Icon.ExternalLink))
                Dalamud.Utility.Util.OpenLink(SiteLink.LookUrl(look.Id));
            var x = row.X + Kit.ButtonWidth("See it on the site", BtnSize.Sm, Icon.ExternalLink) + S(8);
            if (Kit.Button("##shot-done", new Vector2(x, row.Y), "Done", Btn.Quiet, BtnSize.Sm, Icon.Check))
                Later(shots.Discard);
            Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Sm));
            Kit.EndPanel();
            return;
        }
        var field = Kit.At;
        var text = shotCaption;
        if (Kit.Field("##shot-caption", field, w, ref text, "A caption (if you like)", null, 200)) shotCaption = text;
        Kit.Reserve(field, w, S(36));
        Kit.Gap(10);
        if (taken.Note is { Bad: true } bad)
        {
            Txt.Para(new[] { new Run(bad.Text, Face.Sans, 12, Palette.Danger) }, 18);
            Kit.Gap(10);
        }
        var r2 = Kit.At;
        var send = taken.Sending ? "Sending…" : "Send it to the look";
        var sendW = Kit.ButtonWidth(send, BtnSize.Sm, Icon.Upload, Btn.Primary);
        if (Kit.Button("##shot-send", r2, send, Btn.Primary, BtnSize.Sm, Icon.Upload, 0, taken.Sending || !Linked,
                "Goes on the look's page under Worn in game, as yours (three per look at most)"))
        {
            var caption = shotCaption.Trim();
            var lookName = look.Name;
            shots.Send(caption, r =>
            {
                recorder.Note($"SHOT sent for “{lookName}”: {(r.Ok ? "ok" : $"refused ({r.Hint}): {r.Why}")}");
                Notify(r.Ok ? "Sent to the look" : r.Why ?? "The site said no");
            });
        }
        var x2 = r2.X + sendW + S(8);
        var anotherW = Kit.ButtonWidth("Take another", BtnSize.Sm, Icon.Camera);
        var rowH = Kit.ButtonHeight(BtnSize.Sm);
        var anotherAt = new Vector2(x2, r2.Y);
        if (x2 + anotherW > r2.X + w)
        {
            anotherAt = new Vector2(r2.X, r2.Y + rowH + S(8));
            rowH = rowH * 2 + S(8);
        }
        if (Kit.Button("##shot-again", anotherAt, "Take another", Btn.Glass, BtnSize.Sm, Icon.Camera, 0, taken.Sending))
        {
            var id = look.Id;
            var name = look.Name;
            Later(() =>
            {
                if (!shots.Take(id, name, out var why)) Notify(why);
            });
        }
        var dx = anotherAt.X + anotherW + S(8);
        var discardW = Kit.ButtonWidth("Discard", BtnSize.Sm, Icon.Trash, Btn.Quiet);
        var discardAt = new Vector2(dx, anotherAt.Y);
        if (dx + discardW > r2.X + w)
        {
            discardAt = new Vector2(r2.X, anotherAt.Y + Kit.ButtonHeight(BtnSize.Sm) + S(8));
            rowH = discardAt.Y + Kit.ButtonHeight(BtnSize.Sm) - r2.Y;
        }
        if (Kit.Button("##shot-discard", discardAt, "Discard", Btn.Quiet, BtnSize.Sm, Icon.Trash, 0, taken.Sending))
            Later(shots.Discard);
        Kit.Reserve(r2, w, rowH);
        Kit.EndPanel();
    }
}

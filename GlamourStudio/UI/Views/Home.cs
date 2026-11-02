using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Home. At a glamour dresser with Glamour Plates open: the plate open in the game in a big rose card
/// (the look planned for it on the site, Fill plate N, and its twelve slots now and after); else how
/// to get there. Not linked: three steps to begin with. Then, side by side, the looks ready for a
/// plate (the planned one first) and on the right the looks sent from the site and, while the
/// Examine window's open, the outfit you're looking at (with the looks you could wear now when
/// there's neither).
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

    /// <summary>The header pill: what the plugin is doing now.</summary>
    private (string Text, Tone Tone, bool Pulse) StatusNow()
    {
        if (recorder.Recording) return ($"Recording what you do · {Words.Count(recorder.LineCount, "line")}", Tone.Live, true);
        if (editorPlate > 0) return ($"Plate editor open · plate {editorPlate}", Tone.Live, true);
        if (!sync.Linked) return ("Not linked to Glamour Studio", Tone.Neutral, false);
        if (sync.TokenRefused) return ("Link again: the site doesn't know this token", Tone.Danger, false);
        if (sync.Syncing) return ("Sending your closet…", Tone.Live, true);
        if (sync.SyncProblem is { } p) return (p, Tone.Danger, false);
        if (sync.LastSyncAt is { } at) return ($"Closet up to date · sent {Words.Ago(at, DateTimeOffset.UtcNow)}", Tone.Success, false);
        return (config.AutoSync ? "Linked · sends your closet as you play" : "Linked", Tone.Success, false);
    }

    private void DrawHome(Vector2 min, Vector2 size)
    {
        Kit.BeginPane("##home", min, size, SideCss, 20, 20);
        if (editorPlate > 0) DrawEditorHero(editorPlate);
        else DrawIdleHero();
        Kit.Gap(20);
        if (!Linked)
        {
            DrawGetStarted();
            Kit.Gap(20);
        }
        Kit.Columns(1.65f / 2.65f, 20, DrawReadyColumn, DrawHomeSide);
        Kit.EndPane();
    }

    // ---------------------------------------------------------------------------------------------
    // The plate editor, open: the one thing to do.

    private (IReadOnlyDictionary<Slot, Piece>? Pieces, int SyncVersion, Look? Look) editorMatch;

    private void DrawEditorHero(int plate)
    {
        var current = EditorPieces(plate);
        // The look it is, worked out again only when the plate or your looks change.
        if (editorMatch.Pieces == null || editorMatch.SyncVersion != sync.Version || !Plates.Same(current, editorMatch.Pieces))
            editorMatch = (current, sync.Version, Plates.Matching(current, sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Inbox.Select(s => s.Look))));
        var match = editorMatch.Look;
        var planned = sync.PlanFor(plate);
        var plannedOn = planned != null && Plates.Same(current, planned.Pieces);
        var plan = planned != null && !plannedOn ? PlanCached(planned) : null;
        var filled = lastFill is { } lf && lf.Result.Plate == plate ? lf : ((string LookId, FillResult Result, PlatePlan Plan)?)null;
        var filledLook = filled is { } fl ? sync.Library.Mine.Concat(sync.Library.Loved).Concat(sync.Inbox.Select(s => s.Look)).FirstOrDefault(l => l.Id == fl.LookId) : null;

        Kit.BeginPanel("##editor", 20, 22, 20, Palette.RoseWash, Palette.RoseEdge);
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        Kit.Well(at, S(44), Icon.Layers, true, 20);

        // The buttons on the right.
        var lgH = Kit.ButtonHeight(BtnSize.Lg);
        var bx = at.X + w;
        var buttons = new List<Action<Vector2>>();
        var widths = new List<float>();
        if (filled is { } f)
        {
            var wear = $"Wear plate {plate}";
            var ww = Kit.ButtonWidth(wear, BtnSize.Lg, Icon.Shirt, Btn.Primary);
            widths.Add(ww);
            buttons.Add(p =>
            {
                if (Kit.Button("##hero-wear", p, wear, Btn.Primary, BtnSize.Lg, Icon.Shirt, 0, editorChanges,
                        editorChanges ? "Press Save in the game's window first" : "Puts the plate on your current gearset, as /gearset change does (in a city or an inn, on a job its gear fits)"))
                    Later(() =>
                    {
                        if (!PlateEditor.Wear(plate)) Notify("You're on no gearset: change to one first");
                    });
            });
            const string again = "Fill it again";
            var aw = Kit.ButtonWidth(again, BtnSize.Sm, null, Btn.Quiet);
            widths.Insert(0, aw);
            buttons.Insert(0, p =>
            {
                if (Kit.Button("##hero-again", new Vector2(p.X, p.Y + (lgH - Kit.ButtonHeight(BtnSize.Sm)) / 2), again, Btn.Quiet, BtnSize.Sm))
                    lastFill = null;
            });
        }
        else if (planned != null && !plannedOn)
        {
            const string see = "See the look";
            var sw = Kit.ButtonWidth(see, BtnSize.Sm, null, Btn.Quiet);
            widths.Add(sw);
            buttons.Add(p =>
            {
                if (Kit.Button("##hero-see", p, see, Btn.Quiet, BtnSize.Sm))
                    Later(() => OpenLook(planned, null, Screen.Home));
            });
            var fillLabel = $"Fill plate {plate}";
            var fw = Kit.ButtonWidth(fillLabel, BtnSize.Lg, Icon.Layers, Btn.Primary);
            widths.Add(fw);
            var can = plan!.Ready > 0 || plan.Steps.Any(s => s.Kind == StepKind.Clear);
            buttons.Add(p =>
            {
                if (Kit.Button("##hero-fill", p, fillLabel, Btn.Primary, BtnSize.Lg, Icon.Layers, 0, !can,
                        can ? $"Fills plate {plate} with “{planned.Name}”. Nothing's saved until you press Save in the game." : "None of its pieces is in your dresser or armoire"))
                    Later(() => Fill(planned, PlanFor(planned.Pieces)));
            });
        }
        else if (planned != null)
        {
            const string see = "See the look";
            var sw = Kit.ButtonWidth(see, BtnSize.Sm, null, Btn.Quiet);
            widths.Add(sw);
            buttons.Add(p =>
            {
                if (Kit.Button("##hero-see", p, see, Btn.Quiet, BtnSize.Sm))
                    Later(() => OpenLook(planned, null, Screen.Home));
            });
        }
        var allW = widths.Sum() + S(8) * Math.Max(0, widths.Count - 1);
        var x = bx - allW;
        for (var i = 0; i < buttons.Count; i++)
        {
            buttons[i](new Vector2(x, at.Y));
            x += widths[i] + S(8);
        }

        // The words.
        var tx = at.X + S(44) + S(16);
        var tw = bx - allW - (allW > 0 ? S(16) : 0) - tx;
        var y = at.Y;
        y += Kit.Eyebrow(new Vector2(tx, y), "Plate editor · open in the game", null, tw) + S(4);
        y += Title(new Vector2(tx, y), $"Plate {plate}", tw, 24, 1) + S(4);
        var runs = new List<Run>();
        void Say(string text) => runs.Add(new Run(text, Face.Sans, 13, Palette.Soft));
        void Bold(string text) => runs.Add(new Run(text, Face.SansSemibold, 13, Palette.Ink));
        if (filled is { } ff)
        {
            Say("Filled with ");
            Bold(filledLook?.Name ?? "the look");
            Say($": {Words.Filled(plate, ff.Plan, ff.Result.Set).Replace($"Plate {plate} has ", string.Empty, StringComparison.Ordinal)}");
            if (ff.Result.Problems.Count > 0) Say(" " + string.Join(" ", ff.Result.Problems));
        }
        else if (planned != null && plannedOn)
        {
            Say("It has the look planned for it on the site, ");
            Bold(planned.Name);
            Say(".");
        }
        else if (planned != null)
        {
            Say("Planned on the site for ");
            Bold(planned.Name);
            Say(". ");
            Say(plan!.Ready == plan.Wanted ? "Every piece is in your dresser or armoire." : $"{plan.Ready} of its {Words.Count(plan.Wanted, "piece")} can go on now.");
        }
        else if (current.Count == 0) Say("It's empty. Pick a look below to put it on.");
        else if (match != null)
        {
            Say("It's your look ");
            Bold(match.Name);
            Say(".");
        }
        else Say($"It has {Words.Count(current.Count, "piece")}. Pick a look below to put it on instead.");
        if (editorChanges) Say(" Not saved yet.");
        y += Txt.Flow(new Vector2(tx, y), tw, runs, 19.5f, out _);
        Kit.Reserve(at, w, MathF.Max(MathF.Max(S(44), lgH), y - at.Y));

        // The twelve slots: now, and after when there's a plan to fill it with.
        Kit.Gap(18);
        var row = Kit.At;
        if (plan != null)
        {
            var colW = (w - S(36) - S(24)) / 2;
            var dyes = plan.BagDyes.Values.Sum();
            SlotStrip(new Vector2(row.X, row.Y), colW, $"On it now · {Words.Count(current.Count, "piece")}", Palette.Dim, Slots.PlateOrder.Select(s => current.TryGetValue(s, out var p) ? (Piece?)p : null).ToList(), null);
            var ax = row.X + colW + S(12);
            var stripH = StripHeight(colW);
            Icons.Draw(dl, Icon.ArrowRight, new Vector2(ax + (S(36) - S(20)) / 2, row.Y + (stripH - S(20)) / 2), S(20), Palette.Rose);
            var after = AfterPieces(current, plan);
            var afterCount = after.Count(p => p != null);
            var label = $"After · {Words.Count(afterCount, "piece")}" + (dyes > 0 ? $" · {Words.Count(dyes, "dye")} from your bags" : string.Empty);
            SlotStrip(new Vector2(ax + S(36) + S(12), row.Y), colW, label, Palette.RoseText, after, plan);
            Kit.Reserve(row, w, stripH);
        }
        else
        {
            SlotStrip(row, w, $"On it now · {Words.Count(current.Count, "piece")}", Palette.Dim, Slots.PlateOrder.Select(s => current.TryGetValue(s, out var p) ? (Piece?)p : null).ToList(), null);
            Kit.Reserve(row, w, StripHeight(w));
        }
        Kit.Gap(18);
        var nat = Kit.At;
        Txt.Line(dl, nat, "Nothing's saved until you press Save in the game's window.", Face.Sans, 12, Palette.Muted);
        Kit.Reserve(nat, w, Txt.Natural(Face.Sans, 12));
        Kit.EndPanel();
    }

    /// <summary>The plate's slots after filling it: what goes on, what's emptied, what's kept.</summary>
    private static List<Piece?> AfterPieces(IReadOnlyDictionary<Slot, Piece> now, PlatePlan plan)
    {
        var list = new List<Piece?>();
        foreach (var slot in Slots.PlateOrder)
        {
            var step = plan.Steps.FirstOrDefault(s => s.Slot == slot);
            list.Add(step?.Kind switch
            {
                StepKind.Set => step.Want,
                StepKind.Clear => null,
                _ => now.TryGetValue(slot, out var p) ? p : null,
            });
        }
        return list;
    }

    /// <summary>A slot tile's width: twelve across with 4 between.</summary>
    private static float TileOf(float width) => (width - S(4) * 11) / 12;

    /// <summary>The strip: its label, 8, and the tiles 30 tall.</summary>
    private static float StripHeight(float width) => Txt.Natural(Face.Mono, 11) + S(8) + S(30);

    /// <summary>A label and twelve slot tiles in the plate's order (the empty ones dim; a missing piece dashed).</summary>
    private void SlotStrip(Vector2 pos, float width, string label, System.Numerics.Vector4 labelColor, IReadOnlyList<Piece?> pieces, PlatePlan? plan)
    {
        Kit.Sub(pos, label, labelColor, width);
        var y = pos.Y + Txt.Natural(Face.Mono, 11) + S(8);
        var tile = TileOf(width);
        var th = S(30);
        var dl = Kit.Dl;
        for (var i = 0; i < 12; i++)
        {
            var p = new Vector2(pos.X + (tile + S(4)) * i, y);
            var slot = Slots.PlateOrder[i];
            var missing = plan?.Steps.FirstOrDefault(s => s.Slot == slot)?.Kind == StepKind.Missing;
            var max = p + new Vector2(tile, th);
            if (pieces[i] is { } piece)
            {
                // The tile 30 tall, the game's square icon in its middle.
                dl.AddRectFilled(p, max, Palette.U32(Palette.Tile), S(8));
                var sq = MathF.Min(tile, th);
                Kit.GameIcon(p + new Vector2((tile - sq) / 2, (th - sq) / 2), sq, data.Item(piece.Item)?.Icon ?? 0);
                dl.AddRect(p + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(Palette.WithAlpha(Palette.White, 0.07f)), S(8), Dalamud.Bindings.ImGui.ImDrawFlags.None, 1f);
                if (!Kit.Blocked && Dalamud.Bindings.ImGui.ImGui.IsWindowHovered() && Dalamud.Bindings.ImGui.ImGui.IsMouseHoveringRect(p, max))
                    Kit.Tooltip($"{Slots.Word(slot)}: {data.ItemName(piece.Item)}" + (DyeLine(piece) is { } d ? $" · {d}" : string.Empty)
                                + (missing ? " (kept: the look's piece isn't in your dresser or armoire)" : string.Empty));
            }
            else
            {
                var old = Palette.Opacity;
                Palette.Opacity = old * 0.6f;
                if (missing) Kit.Dashed(p, max, S(8), Palette.LineDashed, 1f);
                else
                {
                    dl.AddRectFilled(p, max, Palette.U32(Palette.Raised), S(8));
                    dl.AddRect(p + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(Palette.LineFaint), S(8), Dalamud.Bindings.ImGui.ImDrawFlags.None, 1f);
                }
                Palette.Opacity = old;
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The plate editor, closed: how to get there.

    private void DrawIdleHero()
    {
        Kit.BeginPanel("##idle", 18, 22, 18);
        var at = Kit.At;
        var w = Kit.Width;
        Kit.Well(at, S(44), Icon.Layers, true, 20);
        const string plates = "Your plates";
        var bw = Kit.ButtonWidth(plates, BtnSize.Sm, Icon.Layers);
        if (Kit.Button("##idle-plates", new Vector2(at.X + w - bw, at.Y + (S(44) - Kit.ButtonHeight(BtnSize.Sm)) / 2), plates, Btn.Glass, BtnSize.Sm, Icon.Layers))
            Later(() => Go(Screen.Plates));
        var tx = at.X + S(44) + S(16);
        var tw = at.X + w - bw - S(16) - tx;
        var y = at.Y;
        y += Kit.Eyebrow(new Vector2(tx, y), "At a glamour dresser", null, tw) + S(4);
        var planned = Enumerable.Range(1, Plates.Count).Count(p => sync.PlanFor(p) != null);
        var line = "Open Glamour Plates (every inn room has a glamour dresser) and pick a plate: it shows here with the look planned for it, ready to fill."
                   + (planned > 0 ? $" You've planned {Words.Count(planned, "plate")} on the site." : string.Empty);
        y += Txt.Flow(new Vector2(tx, y), tw, new[] { new Run(line, Face.Sans, 13, Palette.Soft) }, 19.5f, out _);
        Kit.Reserve(at, w, MathF.Max(S(44), y - at.Y));
        Kit.EndPanel();
    }

    // ---------------------------------------------------------------------------------------------
    // Nothing linked yet.

    private static readonly (Icon Icon, string Title, string Line)[] Steps =
    {
        (Icon.Link, "Link your account", "Make a token on Glamour Studio (Settings → Glamour Studio → In-game plugin) and paste it in Settings → Account."),
        (Icon.Archive, "Open your dresser and armoire once", "The game only shows them then. The plugin remembers them, and your bags, armoury chest, saddlebags and retainers."),
        (Icon.Layers, "Put a look on a plate", "At a glamour dresser, open Glamour Plates, pick a look and press Put on plate."),
    };

    private void DrawGetStarted()
    {
        Kit.Heading("Get started", "Three steps");
        Kit.Gap(10);
        var at = Kit.At;
        var w = Kit.Width;
        var gap = S(10);
        var cw = (w - gap * 2) / 3;
        var dl = Kit.Dl;
        var heights = new float[3];
        for (var i = 0; i < 3; i++)
        {
            var (icon, title, line) = Steps[i];
            var p = new Vector2(at.X + (cw + gap) * i, at.Y);
            var inner = cw - S(28);
            var lh = Txt.Flow(Vector2.Zero, inner, new[] { new Run(line, Face.Sans, 12, Palette.Muted) }, 18, out _, draw: false);
            heights[i] = S(14) + S(34) + S(10) + Txt.Natural(Face.SansSemibold, 14) + S(4) + lh + S(14);
        }
        var h = heights.Max();
        for (var i = 0; i < 3; i++)
        {
            var (icon, title, line) = Steps[i];
            var p = new Vector2(at.X + (cw + gap) * i, at.Y);
            Kit.Card(p, p + new Vector2(cw, h), Palette.Card, Palette.Line, 14);
            Kit.Well(p + new Vector2(S(14)), S(34), icon);
            var num = (i + 1).ToString();
            Txt.Draw(dl, new Vector2(p.X + cw - S(14) - Txt.Measure(num, Face.Display, 20).X, p.Y + S(14)), num, Face.Display, 20, Palette.Faint);
            var ty = p.Y + S(14) + S(34) + S(10);
            Txt.Line(dl, new Vector2(p.X + S(14), ty), Txt.Fit(title, Face.SansSemibold, 14, cw - S(28)), Face.SansSemibold, 14, Palette.Ink);
            Txt.Flow(new Vector2(p.X + S(14), ty + Txt.Natural(Face.SansSemibold, 14) + S(4)), cw - S(28), new[] { new Run(line, Face.Sans, 12, Palette.Muted) }, 18, out _);
        }
        Kit.Reserve(at, w, h);
        Kit.Gap(12);
        var row = Kit.At;
        const string link = "Link your account";
        if (Kit.Button("##gs-link", row, link, Btn.Primary, BtnSize.Md, Icon.Link, 0, false, "Once: then your closet keeps itself up to date on the site"))
            Later(() => OpenSettings(SettingsTab.Account));
        var x0 = row.X + Kit.ButtonWidth(link, BtnSize.Md, Icon.Link, Btn.Primary) + S(8);
        if (Kit.Button("##gs-open", new Vector2(x0, row.Y), "Open Glamour Studio", Btn.Glass, BtnSize.Md, Icon.ExternalLink))
            Dalamud.Utility.Util.OpenLink(SiteLink.SiteUrl);
        Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Md));
    }

    // ---------------------------------------------------------------------------------------------
    // Ready for a plate.

    private (int Sync, int Wardrobe, int Plate, Look? Planned, List<Look> Looks)? readyLooks;

    /// <summary>The looks to show as ready: the one planned for the plate open first, then yours and the ones you love that are ready.</summary>
    private List<Look> ReadyLooks(int plate, Look? planned)
    {
        if (readyLooks is { } c && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version && c.Plate == plate && ReferenceEquals(c.Planned, planned)) return c.Looks;
        var list = new List<Look>();
        if (planned != null) list.Add(planned);
        foreach (var l in sync.Library.Mine.Concat(sync.Library.Loved))
        {
            if (list.Count >= 6) break;
            if (list.All(x => x.Id != l.Id) && ReadinessOf(l).PlateReady) list.Add(l);
        }
        readyLooks = (sync.Version, wardrobe.Version, plate, planned, list);
        return list;
    }

    private void DrawReadyColumn()
    {
        var plate = editorPlate;
        if (Kit.HeadingLink("##all-looks", plate > 0 ? $"Ready for plate {plate}" : "Ready for a plate", "All your looks")) Later(() => Go(Screen.Looks));
        Kit.Gap(12);
        if (!Linked)
        {
            Txt.Note("Link your account and your looks from Glamour Studio are here, the ones ready for a plate first.", Palette.Muted);
            return;
        }
        var planned = plate > 0 ? sync.PlanFor(plate) : null;
        var looks = ReadyLooks(plate, planned);
        if (looks.Count == 0)
        {
            Empty("##ready-none", Icon.Hanger, sync.LibraryAt == null ? "Reading your looks…" : "None ready yet",
                "A look is ready once every piece is in your glamour dresser or armoire. Open a look to see what to put away.");
            return;
        }
        var at = Kit.At;
        var w = Kit.Width;
        var gap = S(10);
        var cw = (w - gap) / 2;
        var ch = S(12) * 2 + S(64);
        for (var i = 0; i < looks.Count; i++)
        {
            var look = looks[i];
            var p = new Vector2(at.X + (cw + gap) * (i % 2), at.Y + (ch + gap) * (i / 2));
            var line = ReferenceEquals(look, planned) ? $"Planned for this plate · {Words.Count(look.Count, "piece")}"
                : look.Mine ? $"Your look · {Words.Count(look.Count, "piece")}"
                : $"Loved · by {look.Maker}";
            if (LookCard($"##ready{i}", p, new Vector2(cw, ch), look, line)) Later(() => OpenLook(look, null, Screen.Home));
        }
        var rows = (looks.Count + 1) / 2;
        Kit.Reserve(at, w, rows * ch + (rows - 1) * gap);
    }

    /// <summary>A look as a card: its four pieces' icons, its name and a line, and the green Ready for a plate. True when pressed.</summary>
    private bool LookCard(string id, Vector2 pos, Vector2 size, Look look, string line)
    {
        var (clicked, hovered, _) = Kit.Hit(id, pos, size);
        Kit.Card(pos, pos + size, Palette.Card, hovered ? Palette.LineHover : Palette.Line, 14);
        var dl = Kit.Dl;
        var m = S(64);
        Mosaic(new Vector2(pos.X + S(12), pos.Y + (size.Y - m) / 2), m, look.Pieces);
        var x = pos.X + S(12) + m + S(14);
        var tw = pos.X + size.X - S(12) - x;
        var nameH = Txt.Natural(Face.SansSemibold, 14);
        var lineH = Txt.Natural(Face.Sans, 12);
        var block = nameH + S(6) + lineH + S(6) + S(24);
        var y = pos.Y + (size.Y - block) / 2;
        Txt.Draw(dl, new Vector2(x, y), Txt.Fit(look.Name, Face.SansSemibold, 14, tw), Face.SansSemibold, 14, hovered ? Palette.White : Palette.Ink);
        y += nameH + S(6);
        Txt.Draw(dl, new Vector2(x, y), Txt.Fit(line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted);
        y += lineH + S(6);
        var ready = ReadinessOf(look);
        if (Kit.ChipW(ready.PlateReady ? "Ready for a plate" : ready.Short, ready.PlateReady) <= tw)
            Kit.Chip(new Vector2(x, y), ready.PlateReady ? "Ready for a plate" : ready.Short, ready.PlateReady ? ChipKind.Ok : ChipKind.Plain, ready.PlateReady);
        return clicked;
    }

    // ---------------------------------------------------------------------------------------------
    // The right: sent from the site, someone examined, looks to wear now.

    private void DrawHomeSide()
    {
        var any = false;
        var sent = sync.Inbox;
        if (Linked && sent.Count > 0)
        {
            any = true;
            Kit.Heading("Sent from the site", Words.Count(sent.Count, "look"));
            Kit.Gap(10);
            Kit.BeginPanel("##sent", 6, 6, 6, Palette.Card, Palette.RoseEdge);
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < sent.Count && i < 5; i++)
            {
                var s = sent[i];
                var note = $"Sent {Words.Ago(s.CreatedAt, now)}" + (s.Plate is { } p ? $" · for plate {p}" : string.Empty);
                if (SmallLookRow($"##sent{i}", s.Look, note)) Later(() => OpenLook(s.Look, s, Screen.Home));
            }
            Kit.EndPanel();
            Kit.Gap(16);
        }

        if (Examine.IsOpen) DrawExamined();
        else
        {
            examined = null;
            examinedSeen = null;
            examinedAt = DateTime.MinValue;
        }
        if (examined != null) any = true;

        if (!any && Linked && sync.Wearable.Count > 0)
        {
            Kit.Heading("You could wear now", Words.Count(sync.Wearable.Count, "look"));
            Kit.Gap(10);
            Kit.BeginPanel("##wear-now", 6, 6, 6);
            for (var i = 0; i < sync.Wearable.Count && i < 4; i++)
            {
                var look = sync.Wearable[i];
                if (SmallLookRow($"##wear{i}", look, $"Public · by {look.Maker} · {Words.Count(look.Loves, "love")}")) Later(() => OpenLook(look, null, Screen.Home));
            }
            Kit.EndPanel();
            Kit.Gap(8);
            var at = Kit.At;
            if (Kit.Link("##wear-all", at, "All looks you could wear now")) Later(() =>
            {
                looksTab = 2;
                Go(Screen.Looks);
            });
            Kit.Reserve(at, Kit.Width, Txt.Natural(Face.Sans, 13));
        }
    }

    /// <summary>A look as a small row (a 40px mosaic, its name, a line, a chevron); true when pressed.</summary>
    private bool SmallLookRow(string id, Look look, string line)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(8) * 2 + S(40);
        var (clicked, hovered, _) = Kit.Hit(id, at, new Vector2(w, h));
        var dl = Kit.Dl;
        if (hovered) dl.AddRectFilled(at, at + new Vector2(w, h), Palette.U32(Palette.RowHover), S(12));
        Mosaic(new Vector2(at.X + S(10), at.Y + S(8)), S(40), look.Pieces, 9);
        var x = at.X + S(10) + S(40) + S(12);
        var chev = at.X + w - S(10) - S(16);
        var tw = chev - S(8) - x;
        var nameH = Txt.Natural(Face.SansSemibold, 13);
        var lineH = Txt.Natural(Face.Sans, 12);
        var y = at.Y + (h - nameH - lineH) / 2;
        Txt.Draw(dl, new Vector2(x, y), Txt.Fit(look.Name, Face.SansSemibold, 13, tw), Face.SansSemibold, 13, hovered ? Palette.White : Palette.Ink);
        Txt.Draw(dl, new Vector2(x, y + nameH), Txt.Fit(line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted);
        Icons.Draw(dl, Icon.ChevronRight, new Vector2(chev, at.Y + (h - S(16)) / 2), S(16), hovered ? Palette.Ink : Palette.Dim);
        Kit.Reserve(at, w, h);
        return clicked;
    }

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
    // Someone's outfit, while the Examine window's open.

    private Examined? examined;
    private DateTime examinedAt;

    /// <summary>A first read of someone new, waiting for a second to agree.</summary>
    private Examined? examinedSeen;

    private bool keepingExamined;

    private void DrawExamined()
    {
        // (Read again twice a second at most: the window's open a while.)
        if (DateTime.UtcNow - examinedAt > TimeSpan.FromSeconds(0.5))
        {
            examinedAt = DateTime.UtcNow;
            var read = Examine.Read(data);
            // Someone new: shown once two reads half a second apart agree (the window may still hold
            // the last one's gear for a moment while the new one's loads).
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
        if (examined is not { } e) return;
        Kit.BeginPanel("##examine", 16, 16, 16);
        var at = Kit.At;
        var w = Kit.Width;
        Mosaic(at, S(48), e.Pieces);
        var x = at.X + S(48) + S(12);
        var tw = w - (x - at.X);
        var y = at.Y;
        y += Kit.Eyebrow(new Vector2(x, y), "Examining", null, tw) + S(3);
        var name = e.Name.Length > 0 ? $"{e.Name}'s outfit" : "Their outfit";
        Txt.Draw(Kit.Dl, new Vector2(x, y), Txt.Fit(name, Face.SansSemibold, 14, tw), Face.SansSemibold, 14, Palette.Ink);
        y += Txt.Natural(Face.SansSemibold, 14) + S(3);
        Txt.Draw(Kit.Dl, new Vector2(x, y), $"{Words.Count(e.Pieces.Count, "piece")} · glamour and dyes", Face.Sans, 12, Palette.Muted);
        y += Txt.Natural(Face.Sans, 12);
        Kit.Reserve(at, w, MathF.Max(S(48), y - at.Y));
        Kit.Gap(12);
        var row = Kit.At;
        if (Kit.Button("##ex-try", row, "Try it on", Btn.Glass, BtnSize.Sm, Icon.Eye))
        {
            var pieces = e.Pieces;
            Later(() => TryOn(pieces));
        }
        var bx = row.X + Kit.ButtonWidth("Try it on", BtnSize.Sm, Icon.Eye) + S(8);
        const string keep = "Keep it as a look";
        var kw = MathF.Min(Kit.ButtonWidth(keep, BtnSize.Sm, Icon.Sparkles), row.X + w - bx);
        if (Kit.Button("##ex-keep", new Vector2(bx, row.Y), keepingExamined ? "Keeping…" : keep, Btn.Glass, BtnSize.Sm, Icon.Sparkles, kw, !Linked || keepingExamined,
                Linked ? "A private look of yours on the site, to remix in the dressing room" : "Link your account first (Settings → Account)"))
        {
            keepingExamined = true;
            var lookName = e.Name.Length > 0 ? $"{e.Name}'s outfit" : "An outfit I saw";
            sync.SaveLook(lookName, e.Body, e.Pieces, r =>
            {
                keepingExamined = false;
                Notify(r.Ok ? $"Kept as “{r.Value!.Name}” in your looks" : r.Why ?? "The site said no");
            });
        }
        Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Sm));
        Kit.EndPanel();
        Kit.Gap(16);
    }

    // ---------------------------------------------------------------------------------------------
    // Counts the rail and Closet share.

    private (int Version, string Key, int Used) dresserUsed = (-1, string.Empty, 0);

    /// <summary>The dresser's places used, counted again only when the wardrobe changes.</summary>
    private int DresserUsed(CharacterWardrobe me)
    {
        if (dresserUsed.Version != wardrobe.Version || dresserUsed.Key != me.Key)
            dresserUsed = (wardrobe.Version, me.Key, DresserCleanup.Used(me.Dresser));
        return dresserUsed.Used;
    }
}

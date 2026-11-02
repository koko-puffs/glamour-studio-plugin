using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// A look: its name, then the one thing to do. With the plate editor open, Put on plate N (the
/// plate open in the game), with what it will do in a line; without it, how to open it. Try it on
/// and the look's page beside. After filling, what happened, and Wear it once it's saved. Then the
/// look's pieces, each with where you have it.
/// </summary>
public sealed partial class MainWindow
{
    private Look? openLook;

    /// <summary>The look as it was sent from the site (done with once it's on a plate).</summary>
    private SentLook? openSent;

    /// <summary>The last plate filled, and with which look.</summary>
    private (string LookId, FillResult Result, PlatePlan Plan)? lastFill;

    private void OpenLook(Look look, SentLook? sent)
    {
        openLook = look;
        openSent = sent;
        screen = Screen.Look;
        IsOpen = true;
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

    private void DrawLook(Look look)
    {
        var eyebrow = openSent != null ? "Sent from the site"
            : look.Mine ? "Your look"
            : $"Loved · by {look.Maker}";
        Frame.BeginHeader(20);
        if (ScreenHeader("Back", eyebrow, look.Name))
        {
            screen = Screen.Home;
            openSent = null;
        }
        Frame.EndHeader();

        Frame.BeginBody("##look", null, 18, 24);
        var plate = PlateEditor.SelectedPlate;
        var plan = PlanFor(look.Pieces);
        DrawPutOn(look, plate, plan);
        Kit.Gap(22);
        var ready = ReadinessOf(look);
        Kit.Section("Pieces", Words.Owned(ready.Owned, ready.Total));
        Kit.Gap(10);
        PieceRows("##look-pieces", look.Pieces, plate > 0 ? plan : null);
        if (ready.ToStore > 0)
        {
            Kit.Gap(10);
            Txt.Note("A plate takes pieces from your glamour dresser or armoire only: put the ones elsewhere in your dresser first.", Palette.Faint);
        }
        Frame.EndBody();
    }

    /// <summary>The one thing to do: Put on plate N, or how to get there; Try it on and the site beside.</summary>
    private void DrawPutOn(Look look, int plate, PlatePlan plan)
    {
        var filled = lastFill is { } lf && lf.LookId == look.Id ? lf : ((string, FillResult, PlatePlan)?)null;
        if (filled is { } f && f.Item2.Plate == plate && plate > 0)
        {
            DrawFilled(f.Item2, f.Item3);
            return;
        }
        if (plate > 0)
        {
            Kit.BeginPanel("##put", 18, 18, 18, Palette.RoseWash, Palette.RoseEdge);
            var at = Kit.At;
            var w = Kit.Width;
            var label = $"Put on plate {plate}";
            var can = plan.Ready > 0 || plan.Steps.Any(s => s.Kind == StepKind.Clear);
            if (Kit.Button("##put-on", at, label, Btn.Primary, BtnSize.Lg, Icon.Layers, w, !can,
                    can ? "Fills the plate open in the game. Nothing's saved until you press Save there." : "None of its pieces is in your dresser or armoire"))
                Later(() => Fill(look, plan));
            Kit.Reserve(at, w, Kit.ButtonHeight(BtnSize.Lg));
            Kit.Gap(12);
            Txt.Para(PlanWords(plan), 19.5f);
            if (openSent?.Plate is { } meant && meant != plate)
            {
                Kit.Gap(8);
                Txt.Note($"It was sent for plate {meant}: pick plate {meant} in the game's window to put it there.", Palette.RoseText);
            }
            Kit.EndPanel();
        }
        else
        {
            Kit.BeginPanel("##how", 16, 18, 16);
            var at = Kit.At;
            var w = Kit.Width;
            Kit.IconWell(at, S(40), Icon.Layers);
            var x = at.X + S(40) + S(14);
            var tw = w - (x - at.X);
            Txt.Line(Kit.Dl, new Vector2(x, at.Y), "To put it on a plate", Face.SansSemibold, 14, Palette.Ink);
            var ready = ReadinessOf(look);
            var line = "At a glamour dresser (every inn room has one), open Glamour Plates and pick a plate: the button to fill it shows here. "
                       + (ready.PlateReady ? "Every piece is in your dresser or armoire." : ready.Short + ".");
            var h = Txt.Flow(new Vector2(x, at.Y + Txt.Natural(Face.SansSemibold, 14) + S(2)), tw, new[] { new Run(line, Face.Sans, 13, Palette.Muted) }, 19.5f, out _);
            Kit.Reserve(at, w, MathF.Max(S(40), Txt.Natural(Face.SansSemibold, 14) + S(2) + h));
            Kit.EndPanel();
        }
        Kit.Gap(12);
        var row = Kit.At;
        if (Kit.Button("##try", row, "Try it on", Btn.Glass, BtnSize.Md, Icon.Eye, 0, false, "Opens the game's fitting room with the look on you (any piece, yours or not)"))
            Later(() => TryOn(look.Pieces));
        var x2 = row.X + Kit.ButtonWidth("Try it on", BtnSize.Md, Icon.Eye) + S(8);
        if (Kit.Button("##site", new Vector2(x2, row.Y), "On the site", Btn.Quiet, BtnSize.Md, Icon.ExternalLink, 0, false, "The look's page on Glamour Studio"))
            Dalamud.Utility.Util.OpenLink(SiteLink.LookUrl(look.Id));
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Md));
    }

    /// <summary>What filling will do, in a line or two.</summary>
    private Run[] PlanWords(PlatePlan plan)
    {
        var runs = new List<Run>();
        var missing = plan.Missing.ToList();
        var head = missing.Count == 0
            ? $"All {Words.Count(plan.Wanted, "piece")} go on."
            : $"{plan.Ready} of {Words.Count(plan.Wanted, "piece")} go on.";
        runs.Add(new Run(head + " ", Face.SansMedium, 13, Palette.Ink));
        if (missing.Count > 0)
        {
            var stored = missing.Where(m => m.Elsewhere != null).ToList();
            var gone = missing.Count - stored.Count;
            var bits = new List<string>();
            if (stored.Count > 0)
                bits.Add($"{Words.List(stored.Select(m => data.ItemName(m.Want!.Value.Item)))} {(stored.Count == 1 ? "is" : "are")} {Places.Where(stored[0].Elsewhere!.Value)}{(stored.Count > 1 ? " and the like" : string.Empty)}: put {(stored.Count == 1 ? "it" : "them")} in your dresser first");
            if (gone > 0) bits.Add($"{Words.Count(gone, "piece")} you don't have");
            runs.Add(new Run(string.Join("; ", bits) + ". ", Face.Sans, 13, Palette.Soft));
        }
        var dyes = plan.Steps.Count(s => s.DyeLeft);
        if (dyes > 0) runs.Add(new Run($"{Words.Count(dyes, "piece")} will need dyeing in the plate editor. ", Face.Sans, 13, Palette.Soft));
        var bag = plan.BagDyes;
        if (bag.Count > 0) runs.Add(new Run($"Dyes from your bags: {Words.List(bag.Select(kv => $"{data.DyeName(kv.Key)}{(kv.Value > 1 ? $" ×{kv.Value}" : string.Empty)}"))}. ", Face.Sans, 13, Palette.Soft));
        var cleared = plan.Steps.Count(s => s.Kind == StepKind.Clear);
        if (cleared > 0 && config.ClearBare) runs.Add(new Run("The slots it leaves bare are emptied. ", Face.Sans, 13, Palette.Muted));
        runs.Add(new Run("Nothing's saved until you press Save in the game.", Face.Sans, 13, Palette.Muted));
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
        lastFill = (look.Id, result, plan);
        Notify(result.Ok ? $"Plate {result.Plate}: done. Press Save in the game's window." : result.Problems[0]);
        if (openSent != null && result.Set > 0)
        {
            sync.DoneWith(openSent.Id);
            openSent = null;
        }
    }

    /// <summary>After filling: what happened, and what's next (Save in the game; then Wear it).</summary>
    private void DrawFilled(FillResult result, PlatePlan plan)
    {
        Kit.BeginPanel("##filled", 16, 18, 16, Palette.SuccessWash, Palette.WithAlpha(Palette.Success, 0.4f));
        var at = Kit.At;
        var w = Kit.Width;
        Kit.Round(at, S(40), Icon.Check, S(18), Palette.WithAlpha(Palette.Success, 0.16f), Palette.Success, 2.2f);
        var x = at.X + S(40) + S(14);
        var tw = w - (x - at.X);
        Txt.Line(Kit.Dl, new Vector2(x, at.Y), $"On plate {result.Plate}", Face.SansSemibold, 14, Palette.Ink);
        var line = Words.Filled(result.Plate, plan);
        if (result.Problems.Count > 0) line += " " + string.Join(" ", result.Problems);
        var h = Txt.Flow(new Vector2(x, at.Y + Txt.Natural(Face.SansSemibold, 14) + S(2)), tw, new[] { new Run(line, Face.Sans, 13, Palette.Soft) }, 19.5f, out _);
        Kit.Reserve(at, w, MathF.Max(S(40), Txt.Natural(Face.SansSemibold, 14) + S(2) + h));
        Kit.Gap(12);
        var row = Kit.At;
        var saved = !PlateEditor.HasChanges;
        if (Kit.Button("##wear", row, $"Wear plate {result.Plate}", Btn.Glass, BtnSize.Sm, Icon.Shirt, 0, !saved,
                saved ? "Puts the plate on what you wear now (in a city or an inn, on a job its gear fits)" : "Save the plate in the game first"))
            Later(() =>
            {
                if (!PlateEditor.Wear(result.Plate)) Notify("Couldn't ask the game to wear it");
            });
        var x2 = row.X + Kit.ButtonWidth($"Wear plate {result.Plate}", BtnSize.Sm, Icon.Shirt) + S(8);
        if (Kit.Button("##again", new Vector2(x2, row.Y), "Fill it again", Btn.Quiet, BtnSize.Sm, Icon.Refresh))
            lastFill = null;
        Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Sm));
        Kit.EndPanel();
    }

    private void TryOn(IReadOnlyDictionary<Slot, Piece> pieces)
    {
        var n = PlateEditor.TryOn(pieces);
        Notify(n > 0 ? "In the fitting room" : "The fitting room didn't open");
    }
}

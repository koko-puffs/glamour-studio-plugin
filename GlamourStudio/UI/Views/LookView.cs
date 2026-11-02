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
        var plate = editorPlate;
        var plan = PlanCached(look);
        DrawPutOn(look, plate, plan);
        Kit.Gap(22);
        var ready = ReadinessOf(look);
        Kit.Section("Pieces", Words.Owned(ready.Owned, ready.Total));
        Kit.Gap(10);
        PieceRows("##look-pieces", look.Pieces, plate > 0 ? plan : null);
        if (ready.ToStore > 0)
        {
            Kit.Gap(22);
            DrawPutAway(look);
        }
        if (look.Visibility == "public")
        {
            Kit.Gap(22);
            DrawShot(look);
        }
        Frame.EndBody();
    }

    // ---------------------------------------------------------------------------------------------
    // Putting away: the look's pieces that are yours but not where a plate takes them from.

    private void DrawPutAway(Look look)
    {
        var away = Slots.All.Where(look.Pieces.ContainsKey).Select(s => look.Pieces[s].Item)
            .Select(i => (Item: i, Where: Where(i)))
            .Where(x => x.Where is { } p && p != Place.Dresser && p != Place.Armoire)
            .ToList();
        if (away.Count == 0) return;
        var armoireOpen = Armoire.IsOpen;
        var now = DateTime.UtcNow;
        Kit.Section("Put away first", Words.Count(away.Count, "piece"));
        Kit.Gap(8);
        Txt.Note(armoireOpen
            ? "A plate takes pieces from your dresser or armoire. The armoire's open: put in it the ones it takes, from your bags or armoury chest."
            : "A plate takes pieces from your dresser or armoire. At a glamour dresser, store these from your bags or armoury chest (take them from your saddlebags or retainers first). At an inn, the armoire takes the ones it can.",
            Palette.Muted);
        Kit.Gap(10);
        Kit.BeginPanel("##away", 6, 6, 6);
        var dl = Kit.Dl;
        for (var i = 0; i < away.Count; i++)
        {
            var (itemId, where) = away[i];
            var at = Kit.At;
            var w = Kit.Width;
            var nameH = Txt.Natural(Face.SansMedium, 14);
            var icon = S(36);
            var h = S(9) * 2 + MathF.Max(icon, nameH + S(2) + S(18));
            if (i > 0) dl.AddLine(new Vector2(at.X + S(10), at.Y - 0.5f), new Vector2(at.X + w - S(10), at.Y - 0.5f), Palette.U32(Palette.Line), 1f);
            var item = data.Item(itemId);
            Kit.GameIcon(new Vector2(at.X + S(10), at.Y + (h - icon) / 2), icon, item?.Icon ?? 0);
            var x = at.X + S(10) + icon + S(12);
            var row = data.CabinetOf(itemId);
            var canArmoire = row != null && where is Place.Inventory or Place.Armoury && armoireOpen && !Armoire.IsWorn(itemId);
            // Asked already: the game takes a moment to answer, and a second ask would be a second store.
            var asked = row is { } rr && storing.TryGetValue(rr, out var askedAt) && now - askedAt < TimeSpan.FromSeconds(5);
            var put = asked ? "Storing…" : "Into the armoire";
            var bw = canArmoire ? Kit.ButtonWidth(put, BtnSize.Sm, Icon.Archive) : 0;
            var tw = at.X + w - S(10) - (bw > 0 ? bw + S(10) : 0) - x;
            var top = at.Y + (h - (nameH + S(2) + S(18))) / 2;
            Txt.Line(dl, new Vector2(x, top), Txt.Fit(item?.Name ?? $"Item {itemId}", Face.SansMedium, 14, tw), Face.SansMedium, 14, Palette.Ink);
            var worn = row != null && armoireOpen && Armoire.IsWorn(itemId);
            var line = $"{Places.Word(where!.Value)}" + (row == null ? " · for the dresser" : worn ? " · take it off for the armoire" : " · the armoire takes it");
            Txt.Line(dl, new Vector2(x, top + nameH + S(2)), Txt.Fit(line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted, 18);
            if (bw > 0)
            {
                var cabinet = row!.Value;
                var name = item?.Name ?? "It";
                if (Kit.Button($"##armoire{i}", new Vector2(at.X + w - S(10) - bw, at.Y + (h - Kit.ButtonHeight(BtnSize.Sm)) / 2), put, Btn.Glass, BtnSize.Sm, Icon.Archive, 0, asked))
                    Later(() =>
                    {
                        if (storing.TryGetValue(cabinet, out var t) && DateTime.UtcNow - t < TimeSpan.FromSeconds(5)) return;
                        if (!Armoire.Store(cabinet))
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

    /// <summary>Armoire rows asked to be stored, and when (a second ask waits for the game's answer).</summary>
    private readonly Dictionary<uint, DateTime> storing = new();

    private string shotCaption = string.Empty;

    private void DrawShot(Look look)
    {
        Kit.Section("Worn in game", "A shot for the site");
        Kit.Gap(8);
        var taken = shots.Taken is { } t && t.LookId == look.Id ? t : null;
        if (taken == null)
        {
            Txt.Note("Wear the look, frame it with the camera, and take a shot: the HUD hides for a moment. You see it before it goes to the look's page on Glamour Studio.", Palette.Muted);
            Kit.Gap(10);
            var row = Kit.At;
            var label = shots.Capturing ? "Taking it…" : "Take a shot";
            if (Kit.Button("##shot", row, label, Btn.Glass, BtnSize.Md, Icon.Camera, 0, !Linked || shots.Capturing,
                    Linked ? "Hides the HUD, takes the game's picture, and shows it here to send" : "Link your account first (Settings → Account)"))
            {
                var id = look.Id;
                var name = look.Name;
                Later(() =>
                {
                    shotCaption = string.Empty;
                    if (!shots.Take(id, name, out var why)) Notify(why);
                });
            }
            Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Md));
            if (shots.Problem is { } problem && !shots.Capturing)
            {
                Kit.Gap(8);
                Txt.Note(problem, Palette.Danger);
            }
            return;
        }

        Kit.BeginPanel("##shot-taken", 14, 14, 14);
        var at = Kit.At;
        var w = Kit.Width;
        if (taken.Thumb is { } thumb && thumb.Width > 0 && thumb.Height > 0)
        {
            var h = MathF.Min(w * thumb.Height / thumb.Width, S(320));
            var iw = h * thumb.Width / thumb.Height;
            var p = new Vector2(at.X + (w - iw) / 2, at.Y);
            Kit.Dl.AddImageRounded(thumb.Handle, p, p + new Vector2(iw, h), Vector2.Zero, Vector2.One, Palette.U32(Vector4.One), S(Palette.RadiusMd));
            Kit.Reserve(at, w, h);
        }
        Kit.Gap(10);
        if (taken.Jpeg is { } jpeg) Txt.Note($"{taken.Width} × {taken.Height} · {jpeg.Length / 1024} KB", Palette.Faint);
        Kit.Gap(10);
        if (taken.Sent)
        {
            if (taken.Note is { } n) Txt.Note(n.Text, Palette.Success);
            Kit.Gap(10);
            var row = Kit.At;
            if (Kit.Button("##shot-site", row, "See it on the site", Btn.Glass, BtnSize.Sm, Icon.ExternalLink))
                Dalamud.Utility.Util.OpenLink(SiteLink.LookUrl(look.Id));
            var x = row.X + Kit.ButtonWidth("See it on the site", BtnSize.Sm, Icon.ExternalLink) + S(8);
            if (Kit.Button("##shot-done", new Vector2(x, row.Y), "Done", Btn.Quiet, BtnSize.Sm, Icon.Check))
                Later(shots.Discard);
            Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Sm));
        }
        else
        {
            var field = Kit.At;
            var text = shotCaption;
            if (Kit.Field("##shot-caption", field, w, ref text, "A caption (if you like)", null, 200)) shotCaption = text;
            Kit.Reserve(field, w, S(36));
            Kit.Gap(10);
            if (taken.Note is { Bad: true } n)
            {
                Txt.Note(n.Text, Palette.Danger);
                Kit.Gap(10);
            }
            var row = Kit.At;
            var send = taken.Sending ? "Sending…" : "Send it to the look";
            if (Kit.Button("##shot-send", row, send, Btn.Primary, BtnSize.Sm, Icon.Upload, 0, taken.Sending || !Linked,
                    "Goes on the look's page under Worn in game, as yours (three per look at most)"))
            {
                var caption = shotCaption.Trim();
                shots.Send(caption, r => Notify(r.Ok ? "Sent to the look" : r.Why ?? "The site said no"));
            }
            var x = row.X + Kit.ButtonWidth(send, BtnSize.Sm, Icon.Upload, Btn.Primary) + S(8);
            if (Kit.Button("##shot-again", new Vector2(x, row.Y), "Take another", Btn.Glass, BtnSize.Sm, Icon.Camera, 0, taken.Sending))
            {
                var id = look.Id;
                var name = look.Name;
                Later(() =>
                {
                    if (!shots.Take(id, name, out var why)) Notify(why);
                });
            }
            x += Kit.ButtonWidth("Take another", BtnSize.Sm, Icon.Camera) + S(8);
            // Narrow: Discard on a line of its own.
            var discardAt = new Vector2(x, row.Y);
            var rowH = Kit.ButtonHeight(BtnSize.Sm);
            if (x + Kit.ButtonWidth("Discard", BtnSize.Sm, Icon.Trash) > row.X + w)
            {
                discardAt = new Vector2(row.X, row.Y + rowH + S(8));
                rowH = rowH * 2 + S(8);
            }
            if (Kit.Button("##shot-discard", discardAt, "Discard", Btn.Quiet, BtnSize.Sm, Icon.Trash, 0, taken.Sending))
                Later(shots.Discard);
            Kit.Reserve(row, w, rowH);
        }
        Kit.EndPanel();
    }

    /// <summary>The one thing to do: Put on plate N, or how to get there; Try it on and the site beside.</summary>
    private void DrawPutOn(Look look, int plate, PlatePlan plan)
    {
        var filled = lastFill is { } lf && lf.LookId == look.Id ? lf : ((string, FillResult, PlatePlan)?)null;
        // What was just filled stays (with Wear plate N) after the plate editor closes; a different plate open fills afresh.
        if (filled is { } f && (plate == 0 || f.Item2.Plate == plate))
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
        Kit.BeginPanel("##filled", 16, 18, 16, Palette.SuccessWash, Palette.WithAlpha(Palette.Success, 0.4f));
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

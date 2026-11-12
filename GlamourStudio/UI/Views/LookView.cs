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

    /// <summary>The name of <see cref="lastFill"/>'s look.</summary>
    private string lastFillName = string.Empty;

    /// <summary>The filled plate's pieces, read a moment after filling it (null until then), and when it was filled.</summary>
    private IReadOnlyDictionary<Slot, Piece>? fillPieces;

    private DateTime fillAt;

    /// <summary>When the plate editor was first seen off the filled plate (null while it's on it), and whether it had changes not saved then.</summary>
    private DateTime? fillLeftAt;

    private bool fillLeftUnsaved;

    /// <summary>The plates' read last checked against the fill.</summary>
    private DateTime? fillReadChecked;

    /// <summary>The plate editor as it was the last frame drawn (the plate, and whether it had changes).</summary>
    private int editorWas;

    private bool editorWasChanged;

    /// <summary>
    /// Whether the plate last filled still has what went on: changed by hand in the editor since, or
    /// left with the changes not saved (the game throws them away), or saved otherwise since, it hasn't,
    /// and what was filled is let go (with its Wear plate N).
    /// </summary>
    private void CheckFill()
    {
        var wasPlate = editorWas;
        var wasChanged = editorWasChanged;
        editorWas = editorPlate;
        editorWasChanged = editorChanges;
        if (lastFill is not { } lf) return;
        var plate = lf.Result.Plate;
        var now = DateTime.UtcNow;
        if (editorPlate == plate)
        {
            fillLeftAt = null;
            var on = EditorPieces(plate);
            // What went on, read a moment after (the game has shown it by then).
            if (fillPieces == null)
            {
                if (now - fillAt > TimeSpan.FromMilliseconds(300)) fillPieces = on;
            }
            else if (!SameItems(on, fillPieces)) lastFill = null;
            return;
        }
        if (fillLeftAt == null)
        {
            fillLeftAt = now;
            fillLeftUnsaved = wasPlate == plate && wasChanged;
            fillReadChecked = null;
        }
        var left = fillLeftAt.Value;
        // The plates as saved, read since (a second after leaving: a Save just before has gone in): the fill stands if the plate has it.
        var me = wardrobe.Current;
        if (fillPieces != null && me?.PlatesReadAt is { } readAt && readAt > left + TimeSpan.FromSeconds(1))
        {
            if (readAt == fillReadChecked) return;
            fillReadChecked = readAt;
            var stands = true;
            foreach (var p in me.Plates)
                if (p.Plate == plate && !SameItems(p.Pieces, fillPieces))
                    stands = false;
            if (stands) fillLeftUnsaved = false;
            else lastFill = null;
        }
        // No read of them since: left with changes not saved, the fill's gone.
        else if (fillLeftUnsaved && now - left > TimeSpan.FromSeconds(6)) lastFill = null;
    }

    /// <summary>Two plates' pieces the same, slot by slot (the dyes aside: dyeing it after doesn't undo the fill).</summary>
    private static bool SameItems(IReadOnlyDictionary<Slot, Piece> a, IReadOnlyDictionary<Slot, Piece> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (slot, piece) in a)
            if (!b.TryGetValue(slot, out var other) || LookJson.BaseId(other.Item) != LookJson.BaseId(piece.Item))
                return false;
        return true;
    }

    private void OpenLook(Look look, SentLook? sent, Screen from)
    {
        openLook = look;
        openSent = sent;
        openLookSync = sync.Version;
        openLookListed = InLibrary(look.Id) != null;
        if (from != Screen.Look && from != Screen.Settings) lookFrom = from;
        screen = Screen.Look;
        IsOpen = true;
    }

    /// <summary>The site's version the open look was last looked for at.</summary>
    private int openLookSync = -1;

    /// <summary>Whether the open look was among your looks or the ones you love when last looked for.</summary>
    private bool openLookListed;

    private Look? InLibrary(string id) =>
        sync.Library.Mine.FirstOrDefault(l => l.Id == id) ?? sync.Library.Loved.FirstOrDefault(l => l.Id == id);

    /// <summary>
    /// The open look as the site has it now, after each read: the new copy wherever it is (your looks,
    /// the ones you love, Wear now, the ones sent to you, your plans). Gone from your looks once
    /// they've been read again (deleted on the site, or loved no more), the look closes; one opened
    /// from elsewhere (sent to you, say, then done with) stays as it was.
    /// </summary>
    private void CheckOpenLook()
    {
        if (openLook == null || openLookSync == sync.Version) return;
        openLookSync = sync.Version;
        var id = openLook.Id;
        var listed = InLibrary(id);
        var now = listed
                  ?? sync.Wearable.FirstOrDefault(l => l.Id == id)
                  ?? sync.Inbox.FirstOrDefault(s => s.Look.Id == id)?.Look
                  ?? sync.Plans.FirstOrDefault(p => p.Look.Id == id)?.Look;
        if (now != null)
        {
            openLook = now;
            openLookListed = listed != null;
            return;
        }
        if (!openLookListed || sync.LibraryAt == null || sync.LibraryBusy) return;
        // (The site sends your 300 latest looks and 200 loved: one gone from a full list may only have
        // been pushed past its end, so it stays open.)
        if (sync.Library.Mine.Count >= 300 || sync.Library.Loved.Count >= 200) return;
        if (screen == Screen.Look) Frame.Warn(openLook.Mine ? "That look isn't on the site any more" : "That look isn't one you love any more");
        openLook = null;
        openSent = null;
    }

    /// <summary>What <see cref="plans"/> were worked out for: the wardrobe and the settings.</summary>
    private (int Wardrobe, bool Armoire, bool Clear, bool Dye, IReadOnlyDictionary<byte, int>? Bag) plansFor;

    /// <summary>Each look's plan, by its pieces (the main window and the panel beside the game's ask for different looks in a frame).</summary>
    private readonly Dictionary<IReadOnlyDictionary<Slot, Piece>, PlatePlan> plans = new(ReferenceEqualityComparer.Instance);

    /// <summary>The plan for a look, worked out again only when the wardrobe or the settings changed.</summary>
    private PlatePlan PlanCached(Look look)
    {
        var key = (wardrobe.Version, wardrobe.ArmoireLive, config.ClearBare, config.DyeFromBags, (IReadOnlyDictionary<byte, int>?)wardrobe.BagDyes);
        // (Kept small: looks read again from the site are new copies, and the old ones' plans would stay.)
        if (key != plansFor || plans.Count > 32)
        {
            plansFor = key;
            plans.Clear();
        }
        if (!plans.TryGetValue(look.Pieces, out var plan)) plans[look.Pieces] = plan = PlanFor(look.Pieces);
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

    /// <summary>
    /// A look, opened (docs/redesign/pages/look.html): the sheet on the field at the left (24 in, 18
    /// above and under it), and at the right the side (348 wide, 300 under 1000; Ground2 with its line):
    /// the plate slip, what to put away first, and Worn in game.
    /// </summary>
    private void DrawLook(Look look, Vector2 min, Vector2 size)
    {
        var sideW = S(size.X < S(1000) ? 300 : 348);
        var fieldMin = new Vector2(min.X + S(SideCss), min.Y + S(18));
        var fieldMax = new Vector2(min.X + size.X - sideW, min.Y + size.Y - S(18));
        DrawSheet(look, fieldMin, fieldMax);

        var dl = Kit.Dl;
        var sideMin = new Vector2(min.X + size.X - sideW, min.Y);
        var max = min + size;
        dl.AddRectFilled(sideMin, max, Palette.U32(Palette.Ground2), ImGui.GetStyle().WindowRounding, ImDrawFlags.RoundCornersBottomRight);
        dl.AddLine(new Vector2(sideMin.X + 0.5f, sideMin.Y), new Vector2(sideMin.X + 0.5f, max.Y), Palette.U32(Palette.Line), 1f);
        Kit.BeginPane("##look-side", sideMin + new Vector2(1, 0), new Vector2(sideW - 1, size.Y), 20, 18, 18);
        DrawLookSide(look);
        Kit.EndPane();
    }

    /// <summary>The side: the plate slip first when it has the thing to do, else what to put away first; then Worn in game (public looks).</summary>
    private void DrawLookSide(Look look)
    {
        var (slip, _) = SlipOf(look);
        var ready = ReadinessOf(look);
        var away = ready.ToStore > 0;
        var leads = slip != SlipKind.Closed;
        var shotTaken = shots.Taken is { } t && t.LookId == look.Id;
        var any = false;
        void Next()
        {
            if (any) Kit.Gap(14);
            any = true;
        }
        if (away && !leads)
        {
            Next();
            DrawPutAway(look);
        }
        // (While a shot's taken, its preview has the side: the closed slip waits.)
        if (leads || !shotTaken)
        {
            Next();
            DrawPlateCard(look, compact: false);
        }
        if (away && leads)
        {
            Next();
            DrawPutAway(look);
        }
        if (look.Visibility == "public")
        {
            Next();
            DrawShot(look);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The sheet: the field, the name echoed behind, the pieces as callouts round the middle.

    /// <summary>The echo's rows for the open look (made again for another look or another width).</summary>
    private (string Name, float WidthCss, EchoRow[] Rows)? sheetEcho;

    private void DrawSheet(Look look, Vector2 min, Vector2 max)
    {
        if (max.X - min.X < S(100) || max.Y - min.Y < S(100)) return;
        using (Kit.FieldPanel(min, max))
        {
            // The rows run past the field's edge, so a few px of width don't matter (64px steps).
            var wCss = MathF.Ceiling((max.X - min.X) / S(1) / 64) * 64;
            if (sheetEcho is not { } echo || echo.Name != look.Name || echo.WidthCss != wCss)
            {
                var rows = LookSheet.EchoRows(look.Name, wCss).Select(r => new EchoRow(r.Text, r.XCss, r.YCss)).ToArray();
                sheetEcho = echo = (look.Name, wCss, rows);
            }
            Kit.Echo(min, max, Palette.RadiusXl, echo.Rows, 104);

            var pad = S(18);
            var inner = max - min - new Vector2(pad * 2);
            var colW = S(LookSheet.GearWidth(inner.X / S(1)));
            var rowH = S(LookSheet.GearRow(inner.Y / S(1)));
            var gap = S(16);
            var p0 = min + new Vector2(pad);
            var withPlan = editorPlate > 0 ? PlanCached(look) : null;
            for (var i = 0; i < 6; i++)
            {
                var y = p0.Y + (rowH + S(12)) * i;
                GearCallout(look, LeftSlots[i], new Vector2(p0.X, y), colW, rowH, withPlan);
                GearCallout(look, RightSlots[i], new Vector2(p0.X + inner.X - colW, y), colW, rowH, withPlan);
            }
            DrawSheetMiddle(look, new Vector2(p0.X + colW + gap, p0.Y), new Vector2(inner.X - (colW + gap) * 2, inner.Y));
        }
    }

    /// <summary>The look's dyes for its palette strip (distinct, in its pieces' order, six at most), kept with its pieces.</summary>
    private (IReadOnlyDictionary<Slot, Piece>? Pieces, List<uint> Rgbs, List<string> Names) sheetDyes = (null, new(), new());

    private (List<uint> Rgbs, List<string> Names) DyesOf(Look look)
    {
        if (!ReferenceEquals(sheetDyes.Pieces, look.Pieces))
        {
            var ids = Slots.All.Where(look.Pieces.ContainsKey).SelectMany(s => new[] { look.Pieces[s].Dye1, look.Pieces[s].Dye2 })
                .Where(d => d != 0).Distinct().Select(d => data.Dye(d)).OfType<DyeInfo>().Take(6).ToList();
            sheetDyes = (look.Pieces, ids.Select(d => d.Rgb & 0xFFFFFF).ToList(), ids.Select(d => d.Name).ToList());
        }
        return (sheetDyes.Rgbs, sheetDyes.Names);
    }

    /// <summary>
    /// The middle of the sheet (the mock's <c>.mid</c>, 14 apart, in the middle of the field's
    /// height): the big ring, its words, what stands between it and a plate (a small callout), the
    /// palette strip and Try it on: the sticker when the plate slip hasn't one, else outlined.
    /// </summary>
    private void DrawSheetMiddle(Look look, Vector2 pos, Vector2 size)
    {
        var dl = Kit.Dl;
        var ready = ReadinessOf(look);
        var (rgbs, names) = DyesOf(look);
        var sticker = !SlipHasSticker(look);
        var gap = S(14);
        var ringD = S(120);
        var titleH = S(17 * 1.02f);
        var lineH = Txt.Natural(Face.SansMedium, 12);
        var rowH = Txt.Natural(Face.Sans, 12);
        var blockers = (ready.ToStore > 0 ? 1 : 0) + (ready.Missing > 0 ? 1 : 0);
        var blockH = blockers > 0 && !ready.PlateReady ? S(2 + 12) * 2 + rowH * blockers + S(6) * (blockers - 1) : 0;
        var stripH = rgbs.Count > 0 ? S(34) : 0;
        const string tryLabel = "Try it on";
        var btnH = sticker ? Kit.StickerHeight() + S(3) : Kit.ButtonHeight(BtnSize.Sm);
        var total = ringD + gap + titleH + S(4) + lineH + (blockH > 0 ? gap + blockH : 0) + (stripH > 0 ? gap + stripH : 0) + gap + btnH;
        var y = pos.Y + MathF.Max(0, (size.Y - total) / 2);
        var cx = pos.X + size.X / 2;

        Kit.ReadyRing(new Vector2(cx, y + ringD / 2), ready.Ready, ready.Total, 120);
        y += ringD + gap;
        var title = ready.Total == 0 ? "Nothing on it" : ready.PlateReady ? "Ready for a plate" : "Not ready yet";
        Kit.Display(new Vector2(cx - Kit.DisplayWidth(title, 17) / 2, y + (titleH - Kit.DisplayHeight(17)) / 2), title, 17, null, size.X);
        y += titleH + S(4);
        var line = ready.Total == 0 ? "it has no pieces yet" : ready.PlateReady ? "every piece is in your dresser or armoire" : "what stands between it and a plate:";
        var fit = Txt.Fit(line, Face.SansMedium, 12, size.X);
        Txt.Draw(dl, new Vector2(cx - Txt.Measure(fit, Face.SansMedium, 12).X / 2, y), fit, Face.SansMedium, 12, Tint.Muted);
        y += lineH;

        if (blockH > 0)
        {
            y += gap;
            var bmin = new Vector2(pos.X, y);
            Kit.Callout(bmin, bmin + new Vector2(size.X, blockH));
            using (Kit.OnInk())
            {
                var ry = y + S(2 + 12);
                void Row(Vector4 color, string text)
                {
                    Kit.Dot(new Vector2(pos.X + S(2 + 14) + S(3.5f), ry + rowH / 2), color);
                    var tx = pos.X + S(2 + 14 + 7 + 6);
                    Txt.Draw(dl, new Vector2(tx, ry), Txt.Fit(text, Face.Sans, 12, pos.X + size.X - S(16) - tx), Face.Sans, 12, Palette.Body);
                    ry += rowH + S(6);
                }
                if (ready.ToStore > 0) Row(Palette.Amber, $"{ready.ToStore} to put away first");
                if (ready.Missing > 0) Row(Palette.Pink, $"{ready.Missing} you don't have yet");
            }
            y += blockH;
        }

        if (stripH > 0)
        {
            y += gap;
            var sp = new Vector2(pos.X, y);
            Kit.PaletteStrip(sp, size.X, rgbs, names, big: true);
            // A dye's name in full when it's pointed at (the strip cuts long ones).
            if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(sp, sp + new Vector2(size.X, stripH)))
            {
                var i = Math.Clamp((int)((ImGui.GetMousePos().X - sp.X) / (size.X / rgbs.Count)), 0, rgbs.Count - 1);
                Kit.Tooltip(names[i]);
            }
            y += stripH;
        }

        y += gap;
        const string tryTip = "Opens the game's fitting room with the look on you (any piece, yours or not)";
        if (sticker)
        {
            var sw = Kit.StickerWidth(tryLabel, StickerSize.Md, Icon.Shirt);
            if (Kit.Sticker("##look-try", new Vector2(cx - sw / 2, y), tryLabel, StickerSize.Md, Icon.Shirt, 0, false, tryTip))
                Later(() => TryOn(look.Pieces));
        }
        else
        {
            var bw = Kit.ButtonWidth(tryLabel, BtnSize.Sm, Icon.Shirt);
            if (Kit.Button("##look-try", new Vector2(cx - bw / 2, y), tryLabel, Btn.Glass, BtnSize.Sm, Icon.Shirt, 0, false, tryTip, iconCss: 14))
                Later(() => TryOn(look.Pieces));
        }
    }

    /// <summary>One slot's step in the plan (no lookups made: twelve slots a frame).</summary>
    private static PlateStep? StepOf(PlatePlan? plan, Slot slot)
    {
        if (plan == null) return null;
        foreach (var s in plan.Steps)
            if (s.Slot == slot)
                return s;
        return null;
    }

    /// <summary>
    /// Where a piece is, as its callout's tag and corner dot say it in the honest colours: ice where a
    /// plate takes it from (amber with a dye to see to), amber elsewhere of yours, lavender on your
    /// wishlist, pink missing. With the tooltip's last line and its colour.
    /// </summary>
    private static (string Text, TagKind Kind, Icon? Icon, Own Own, string Tip, Vector4 TipColor) WhereTag(Place? where, bool wished, PlateStep? step)
    {
        switch (where)
        {
            case Place.Dresser or Place.Armoire:
                var name = where == Place.Dresser ? "Dresser" : "Armoire";
                var tip = $"{(where == Place.Dresser ? "In your glamour dresser" : "In your armoire")}: ready for a plate.";
                if (step is { DyeFromBags: true })
                    return ($"{name} · dye from bags", TagKind.Amber, Icon.Droplet, Own.Amber, tip + " Dyed from a dye in your bags as the plate is saved.", Palette.Amber);
                if (step is { DyeLeft: true })
                    return ($"{name} · dye in the editor", TagKind.Amber, Icon.Droplet, Own.Amber, tip + " Its dye isn't in your bags: dye it in the plate editor.", Palette.Amber);
                return (name, TagKind.Ok, Icon.Check, Own.Ice, tip, Palette.Body);
            case { } p:
                var (text, icon) = p switch
                {
                    Place.Inventory => ("In your bags", Icon.Bag),
                    Place.Armoury => ("Armoury chest", Icon.Shield),
                    Place.Saddlebag => ("Saddlebags", Icon.Backpack),
                    _ => ("On a retainer", Icon.User),
                };
                return (text, TagKind.Amber, icon, Own.Amber,
                    $"Yours, {Places.Where(p)}. A plate takes pieces from the dresser or the armoire: put it away first.", Palette.Amber);
            default:
                return wished
                    ? ("On your wishlist", TagKind.Star, Icon.Star, Own.Pink, "On your wishlist on the site: the plugin hasn't seen it in your wardrobe yet.", Palette.Pink)
                    : ("Missing", TagKind.Miss, null, Own.Pink,
                        "The plugin hasn't seen it in your wardrobe. Open your dresser, armoire, saddlebags and retainers once, and it knows them.", Palette.Pink);
        }
    }

    private static readonly Run[] SheetRun = new Run[1];

    /// <summary>
    /// A piece's callout (the mock's <c>.co.callout</c>): the ink card with its black edge and block,
    /// the icon (40) with its corner dot, the name, its slot and its dyes as swatches (an empty ring
    /// for a channel undyed), and where it is as a tag. A piece you haven't got is washed pink, its
    /// icon faded. A slot the look leaves bare is a dashed outline.
    /// </summary>
    private void GearCallout(Look look, Slot slot, Vector2 pos, float width, float height, PlatePlan? withPlan)
    {
        if (!look.Pieces.TryGetValue(slot, out var piece) || piece.Item == 0)
        {
            BareSlot(slot, pos, width, height);
            return;
        }
        var dl = Kit.Dl;
        var max = pos + new Vector2(width, height);
        var item = data.Item(piece.Item);
        var where = Where(piece.Item);
        var wished = sync.Library.Wishes.Contains(LookJson.BaseId(piece.Item));
        var (tagText, kind, icon, own, tip, tipColor) = WhereTag(where, wished, StepOf(withPlan, slot));
        var missing = where == null;
        Kit.Callout(pos, max, Palette.RadiusMenu, missing);
        using (Kit.OnInk())
        {
            var ring = missing ? Palette.Mix(Palette.Card, Palette.WithAlpha(Palette.PinkRow, 1f), Palette.PinkRow.W) : Palette.Card;
            Kit.Item(new Vector2(pos.X + S(12), pos.Y + (height - S(40)) / 2), 40, item?.Icon ?? 0, own, missing, ring);
            var tx = pos.X + S(12 + 40 + 10);
            var tw = max.X - S(12) - tx;
            var nameH = Txt.Natural(Face.SansSemibold, 13);
            var lineH = Txt.Natural(Face.Sans, 12);
            var tagH = Kit.TagHeight(TagSize.Sm);
            // Squeezed (a short window): the name and where it is; the slot and dyes in the tooltip.
            var full = height >= S(74);
            var block = nameH + S(3) + (full ? lineH + S(3) : 0) + tagH;
            var y = pos.Y + (height - block) / 2;
            var name = item?.Name ?? $"Item {LookJson.BaseId(piece.Item)}";
            Txt.Draw(dl, new Vector2(tx, y), Txt.Fit(name, Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink);
            y += nameH + S(3);
            if (full)
            {
                var x = tx + Txt.Draw(dl, new Vector2(tx, y), item != null ? Slots.Word(slot) + " ·" : Slots.Word(slot), Face.Sans, 12, Palette.Muted);
                var channels = item != null ? data.DyeChannels(piece.Item) : -1;
                if (channels == 0) Txt.Draw(dl, new Vector2(x, y), " no dye", Face.Sans, 12, Palette.Muted);
                for (var c = 1; c <= channels && c <= 2; c++)
                {
                    var dye = c == 1 ? piece.Dye1 : piece.Dye2;
                    uint? rgb = dye != 0 && data.Dye(dye) is { } info ? info.Rgb & 0xFFFFFF : null;
                    Kit.Swatch(dl, new Vector2(x + S(5) + S(6), y + lineH / 2), rgb, 12);
                    x += S(5 + 12);
                }
                y += lineH + S(3);
            }
            // The tag as wide as the callout lets it be (its words cut to fit).
            var iconW = icon != null ? S(10 + 4) : 0;
            var room = max.X - S(4) - tx - S(6) * 2 - iconW;
            Kit.Tag(new Vector2(tx, y), Txt.Fit(tagText, Face.SansSemibold, 10, room), kind, icon, TagSize.Sm);
        }
        // (Only for the callout under the mouse: twelve of them a frame otherwise.)
        if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, max))
            PieceTip(item?.Name ?? $"Item {LookJson.BaseId(piece.Item)}", Slots.Word(slot) + (DyeLine(piece) is { } d ? $" · {d}" : string.Empty), tip, tipColor);
    }

    /// <summary>
    /// A piece's tooltip as the mock's (extras.html): its name in 600, its slot and dyes, and where it
    /// is in its colour (amber for what to see to). The Kit's has one colour for all its words; this
    /// is its look with three lines, the words through <see cref="Txt"/> (names in any script).
    /// </summary>
    private static void PieceTip(string name, string line, string where, Vector4 whereColor)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(11), S(8)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, S(Palette.RadiusTip));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Palette.Popup);
        ImGui.PushStyleColor(ImGuiCol.Border, Palette.LineStrong);
        try
        {
            ImGui.BeginTooltip();
            var at = ImGui.GetCursorScreenPos();
            // As wide as its longest line, 260 at most (as the Kit's).
            var w = MathF.Min(S(260), MathF.Max(Txt.Measure(name, Face.SansSemibold, 13).X,
                MathF.Max(Txt.Measure(line, Face.Sans, 13).X, Txt.Measure(where, Face.Sans, 13).X)) + 1);
            var dl = ImGui.GetWindowDrawList();
            var y = at.Y;
            var nameFit = Txt.Fit(name, Face.SansSemibold, 13, w);
            var right = Txt.Draw(dl, new Vector2(at.X, y), nameFit, Face.SansSemibold, 13, Palette.Ink);
            y += Txt.Natural(Face.SansSemibold, 13) + S(2);
            SheetRun[0] = new Run(line, Face.Sans, 13, Palette.Body);
            var lh = Txt.Flow(new Vector2(at.X, y), w, SheetRun, 18, out _);
            y += lh + S(2);
            SheetRun[0] = new Run(where, Face.Sans, 13, whereColor);
            var wh = Txt.Flow(new Vector2(at.X, y), w, SheetRun, 18, out _);
            y += wh;
            ImGui.Dummy(new Vector2(MathF.Max(right, w), y - at.Y));
            ImGui.EndTooltip();
        }
        finally
        {
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(3);
        }
    }

    /// <summary>
    /// A slot the look leaves bare (the mock's <c>.co.bare</c>): a 2px dashed outline in the field's
    /// ink, a dashed tile with — (kept) or × (emptied on the plate), and what becomes of it.
    /// </summary>
    private void BareSlot(Slot slot, Vector2 pos, float width, float height)
    {
        var dl = Kit.Dl;
        var (mark, title, line) = LookSheet.Bare(slot, config.ClearBare);
        Kit.Dashed(pos, pos + new Vector2(width, height), S(Palette.RadiusMenu), Palette.WithAlpha(Palette.OnField, 0.38f), S(2));
        var b = new Vector2(pos.X + S(12), pos.Y + (height - S(40)) / 2);
        Kit.Dashed(b, b + new Vector2(S(40)), S(9), Palette.WithAlpha(Palette.OnField, 0.4f), 1f);
        var ms = Txt.Measure(mark, Face.Mono, 13);
        Txt.Draw(dl, b + (new Vector2(S(40)) - ms) / 2, mark, Face.Mono, 13, Palette.OnField2);
        var tx = b.X + S(40 + 8);
        var tw = pos.X + width - S(12) - tx;
        SheetRun[0] = new Run(line, Face.SansMedium, 11, Palette.OnField2);
        var subH = Txt.Flow(Vector2.Zero, tw, SheetRun, 11 * 1.25f, out _, draw: false);
        var titleH = S(15);
        var y = pos.Y + (height - titleH - subH) / 2;
        Txt.Line(dl, new Vector2(tx, y), Txt.Fit(title, Face.SansSemibold, 12, tw), Face.SansSemibold, 12, Palette.OnField2, 15);
        Txt.Flow(new Vector2(tx, y + titleH), tw, SheetRun, 11 * 1.25f, out _);
    }

    // ---------------------------------------------------------------------------------------------
    // The plate slip: putting it on a plate.

    private enum SlipKind
    {
        /// <summary>No plate editor, no dresser: how to get to a plate.</summary>
        Closed,

        /// <summary>At the dresser, Glamour Plates not open: Put on a plate… (the plugin opens it).</summary>
        AtDresser,

        /// <summary>The plate editor open: Put on plate N.</summary>
        Open,

        /// <summary>Just filled: what went on, and Wear plate N.</summary>
        Filled,
    }

    /// <summary>What the slip shows for a look now, and the fill when it's just been filled.</summary>
    private (SlipKind Kind, (string LookId, FillResult Result, PlatePlan Plan)? Fill) SlipOf(Look look)
    {
        var plate = editorPlate;
        // What was just filled stays (with Wear plate N) after the plate editor closes; a different plate open fills afresh.
        // (Left with changes not saved, it waits for the plates read again to say whether Save went in.)
        var doubtful = fillLeftAt != null && fillLeftUnsaved;
        if (lastFill is { } f && f.LookId == look.Id && (plate == 0 || f.Result.Plate == plate) && !doubtful) return (SlipKind.Filled, f);
        if (plate > 0) return (SlipKind.Open, null);
        return (PlateItHere ? SlipKind.AtDresser : SlipKind.Closed, null);
    }

    /// <summary>
    /// Whether the plate slip has the screen's sticker (Put on plate N, Put on a plate…, Wear plate
    /// N): then Try it on is outlined. For the Looks pane as for the look.
    /// </summary>
    private bool SlipHasSticker(Look look) => SlipOf(look).Kind != SlipKind.Closed;

    /// <summary>The plate a look was sent from the site for (0: none): the one opened, else one waiting.</summary>
    private int SentPlateOf(Look look)
    {
        if (openSent != null && openSent.Look.Id == look.Id) return openSent.Plate ?? 0;
        foreach (var s in sync.Inbox)
            if (s.Look.Id == look.Id)
                return s.Plate ?? 0;
        return 0;
    }

    /// <summary>The plate a look's planned for on the site for this character (0: none).</summary>
    private int PlanPlateOf(Look look)
    {
        for (var n = 1; n <= Plates.Count; n++)
            if (sync.PlanFor(n)?.Id == look.Id)
                return n;
        return 0;
    }

    /// <summary>
    /// The plate slip (the mock's <c>.slip</c>, a ticket; the Looks pane's with <paramref name="compact"/>):
    /// with the plate editor open, which plate and what it'll do, Put on plate N; at the dresser, Put
    /// on a plate… and its menu; just filled, what happened and Wear plate N; else how to get there
    /// (in the Looks pane, a line saying how ready it is).
    /// </summary>
    private void DrawPlateCard(Look look, bool compact)
    {
        var (kind, filled) = SlipOf(look);
        switch (kind)
        {
            case SlipKind.Filled:
                DrawFilled(filled!.Value.Result, filled.Value.Plan, compact);
                break;
            case SlipKind.Open:
                DrawOpenSlip(look, compact);
                break;
            case SlipKind.AtDresser:
                DrawDresserSlip(look, compact);
                break;
            default:
                if (compact) DrawPlateLine(look);
                else DrawClosedSlip(look);
                break;
        }
    }

    /// <summary>
    /// The slip's top half (the mock's <c>.slip .top</c>): an eyebrow, the title in display capitals
    /// (two lines at most) and a tag at the right, in the middle of their height.
    /// </summary>
    private static void SlipTop(string eyebrow, string title, int px, string? tag = null, TagKind kind = TagKind.Ink, Icon? icon = null, string? tagTip = null)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var tagW = tag != null ? Kit.TagWidth(tag, icon, TagSize.Lg) + S(10) : 0;
        var eh = Kit.Eyebrow(at, eyebrow, null, w - tagW);
        var th = Title(new Vector2(at.X, at.Y + eh + S(4)), title, w - tagW, px, 2);
        var colH = eh + S(4) + th;
        if (tag != null)
        {
            var tp = new Vector2(at.X + w - tagW + S(10), at.Y + (colH - Kit.TagHeight(TagSize.Lg)) / 2);
            Kit.Tag(tp, tag, kind, icon, TagSize.Lg);
            if (tagTip != null && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(tp, tp + new Vector2(tagW - S(10), Kit.TagHeight(TagSize.Lg))))
                Kit.Tooltip(tagTip);
        }
        Kit.Reserve(at, w, colH);
    }

    /// <summary>Words laid at the cursor across the box, wrapped (the slip's and cards' lines).</summary>
    private static void Words12(string text, Vector4 color, int px = 12, float lineCss = 18)
    {
        var at = Kit.At;
        SheetRun[0] = new Run(text, Face.Sans, px, color);
        var h = Txt.Flow(at, Kit.Width, SheetRun, lineCss, out _);
        Kit.Reserve(at, Kit.Width, h);
    }

    /// <summary>A line with its dot before it (the mock's <c>.row.g8</c> with a <c>.dot</c>), wrapped under its words.</summary>
    private static void DotLine(Vector4 dot, string text, int px)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var lineCss = px * 1.4f;
        Kit.Dot(new Vector2(at.X + S(3.5f), at.Y + S(lineCss) / 2), dot);
        SheetRun[0] = new Run(text, Face.Sans, px, Palette.Body);
        var h = Txt.Flow(new Vector2(at.X + S(7 + 8), at.Y), w - S(7 + 8), SheetRun, lineCss, out _);
        Kit.Reserve(at, w, h);
    }

    /// <summary>The open slip's lines (what filling will do), worked out again only when the plan, the plate or the look's sending change.</summary>
    private (PlatePlan Plan, int Plate, int Sent, bool Clear, List<(Vector4 Dot, string Text)> Lines)? openSlipLines;

    private List<(Vector4 Dot, string Text)> SlipLines(PlatePlan plan, int plate, int sent)
    {
        if (openSlipLines is { } c && ReferenceEquals(c.Plan, plan) && c.Plate == plate && c.Sent == sent && c.Clear == config.ClearBare) return c.Lines;
        var lines = new List<(Vector4, string)>();
        var set = plan.Set.ToList();
        lines.Add((plan.Ready > 0 ? Palette.Ice : Palette.Pink,
            LookSheet.GoesOn(plan.Ready, plan.Wanted, set.Count(s => s.Want != null && s.Source == PlateSource.Dresser), set.Count(s => s.Want != null && s.Source == PlateSource.Armoire))));
        string DyeNames(PlateStep s, DyeOutcome outcome) => Words.List(new[] { s.Dye1 == outcome ? s.Want!.Value.Dye1 : (byte)0, s.Dye2 == outcome ? s.Want!.Value.Dye2 : (byte)0 }
            .Where(d => d != 0).Select(d => data.DyeName(d)));
        var bag = set.Where(s => s.DyeFromBags).ToList();
        if (bag.Count is > 0 and <= 3)
            foreach (var s in bag) lines.Add((Palette.Amber, $"{data.ItemName(s.Want!.Value.Item)} dyed {DyeNames(s, DyeOutcome.FromBags)} from your bags"));
        else if (bag.Count > 3)
            lines.Add((Palette.Amber, $"{Words.InWords(bag.Count)} dyed from your bags: {Words.List(plan.BagDyes.Select(kv => $"{data.DyeName(kv.Key)}{(kv.Value > 1 ? $" ×{kv.Value}" : string.Empty)}"))}"));
        var left = set.Where(s => s.DyeLeft).ToList();
        if (left.Count is > 0 and <= 3)
            foreach (var s in left) lines.Add((Palette.Amber, $"{data.ItemName(s.Want!.Value.Item)}: dye {DyeNames(s, DyeOutcome.Differs)} in the plate editor"));
        else if (left.Count > 3)
            lines.Add((Palette.Amber, $"{Words.InWords(left.Count)} to dye in the plate editor"));
        var stored = plan.Missing.Where(m => m.Elsewhere != null).ToList();
        if (stored.Count == 1)
            lines.Add((Palette.Amber, $"{data.ItemName(stored[0].Want!.Value.Item)} is {Places.Where(stored[0].Elsewhere!.Value)}: put it away first"));
        else if (stored.Count > 1)
            lines.Add((Palette.Amber, $"{Words.InWords(stored.Count)} are elsewhere: put them away first"));
        var gone = plan.Missing.Where(m => m.Elsewhere == null).ToList();
        if (gone.Count == 1)
            lines.Add((Palette.Pink, $"{data.ItemName(gone[0].Want!.Value.Item)}: you don't have it yet, so its slot is left as it is"));
        else if (gone.Count > 1)
            lines.Add((Palette.Pink, $"{Words.InWords(gone.Count)} you don't have yet: their slots are left as they are"));
        var cleared = plan.Steps.Count(s => s.Kind == StepKind.Clear);
        if (cleared > 0 && config.ClearBare) lines.Add((Palette.Faint, LookSheet.Emptied(cleared)));
        if (sent > 0 && sent != plate) lines.Add((Palette.Amber, $"It was sent for plate {sent}: pick plate {sent} in the game's window to put it there"));
        openSlipLines = (plan, plate, sent, config.ClearBare, lines);
        return lines;
    }

    /// <summary>The slip with the plate editor open (look.html's first frame; the Looks pane's second): Put on plate N.</summary>
    private void DrawOpenSlip(Look look, bool compact)
    {
        var plate = editorPlate;
        var plan = PlanCached(look);
        var sent = SentPlateOf(look);
        Kit.BeginSlip("##slip", 16, 18, Palette.Ground2);
        if (sent == plate) SlipTop("Take it into the game", $"Plate {plate}", 26, "Sent for this plate", TagKind.Ok, Icon.Check);
        else if (sent > 0) SlipTop("Take it into the game", $"Plate {plate}", 26, $"Sent for plate {sent}", TagKind.Amber, null, $"Pick plate {sent} in the game's window to put it there.");
        else SlipTop("Take it into the game", $"Plate {plate}", 26, "Open now");
        Kit.SlipTear();
        var px = compact ? 13 : 12;
        var lines = SlipLines(plan, plate, sent);
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0) Kit.Gap(4);
            DotLine(lines[i].Dot, lines[i].Text, px);
        }
        Kit.Gap(10);
        var at = Kit.At;
        var w = Kit.Width;
        var can = plan.Ready > 0 || plan.Steps.Any(s => s.Kind == StepKind.Clear);
        var label = $"Put on plate {plate}";
        if (Kit.Sticker("##put-on", at, label, StickerSize.Lg, Icon.Layers, 0, !can,
                can ? "Fills the plate open in the game. Nothing's saved until you press Save there." : "None of its pieces is in your dresser or armoire"))
            Later(() => Fill(look, plan));
        var sh = Kit.StickerHeight(StickerSize.Lg) + S(3);
        var sw = Kit.StickerWidth(label, StickerSize.Lg, Icon.Layers) + S(3);
        const string handShort = "nothing's saved till you press Save";
        var handX = at.X + sw + S(10);
        if (compact && w - (handX - at.X) >= S(120))
        {
            // Beside the sticker (the Looks pane), in the middle of its height.
            SheetRun[0] = new Run(handShort, Face.Hand, 13, Tint.Accent);
            var hh = Txt.Flow(Vector2.Zero, at.X + w - handX, SheetRun, 13 * 1.15f, out _, draw: false);
            Kit.Hand(new Vector2(handX, at.Y + (sh - S(3) - hh) / 2), handShort, 13, at.X + w - handX, null, 13 * 1.15f);
            Kit.Reserve(at, w, sh);
        }
        else
        {
            Kit.Reserve(at, w, sh);
            Kit.Gap(10);
            var hat = Kit.At;
            Kit.Reserve(hat, w, Kit.Hand(hat, "nothing's saved till you press Save in the game", 13, w));
        }
        Kit.EndSlip();
    }

    /// <summary>The slip at the glamour dresser, Glamour Plates not open (look.html's third frame): Put on a plate… and its menu.</summary>
    private void DrawDresserSlip(Look look, bool compact)
    {
        Kit.BeginSlip("##slip", 16, 18, Palette.Ground2);
        SlipTop("At the glamour dresser", "Put it on a plate", 26);
        Kit.SlipTear();
        Words12("Pick a plate: the plugin opens Glamour Plates, picks it and fills it. Nothing's saved until you press Save there.", Palette.Muted, compact ? 13 : 12);
        Kit.Gap(10);
        var at = Kit.At;
        var busy = plateIt != null || AutoBusy;
        var tip = plateIt != null ? "Putting a look on a plate…" : AutoBusy ? "Wait for Make room for me to finish first" : "Pick a plate: the plugin opens Glamour Plates, picks it and fills it.";
        if (MenuSticker("##put-on-a", at, "Put on a plate…", Icon.Layers, busy, tip, out var size))
            OpenPlateItMenu("##lv-plate-it", look);
        Kit.Reserve(at, Kit.Width, size.Y + S(3));
        // The menu hangs under the sticker, its right edge at the slip's.
        var (start, sw, _) = Kit.Panel;
        DrawPlateItMenu("##lv-plate-it", new Vector2(start.X + sw - S(2), at.Y + size.Y + S(10)));
        Kit.EndSlip();
    }

    /// <summary>The closed slip's words, worked out again only when your looks, the wardrobe or the look change.</summary>
    private (int Sync, int Wardrobe, IReadOnlyDictionary<Slot, Piece> Pieces, string Line, List<Piece> Wishable)? closedWords;

    private (string Line, List<Piece> Wishable) ClosedWords(Look look, Readiness r)
    {
        if (closedWords is { } c && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version && ReferenceEquals(c.Pieces, look.Pieces)) return (c.Line, c.Wishable);
        string line;
        if (r.Total == 0) line = "It has no pieces to put on a plate.";
        else if (r.PlateReady) line = "Open your glamour dresser: the plugin opens Glamour Plates, picks the plate and fills it.";
        else
        {
            var bits = new List<string>
            {
                r.Ready == 0 ? "none of it can go on yet" : r.Ready == 1 ? "the one in your dresser or armoire goes on now" : $"the {LookSheet.Lower(r.Ready)} in your dresser or armoire go on now",
            };
            if (r.ToStore > 0) bits.Add(r.ToStore == 1 ? "the one to put away once it's in" : $"the {LookSheet.Lower(r.ToStore)} to put away once they're in");
            var missing = Slots.All.Where(s => look.Pieces.TryGetValue(s, out var p) && p.Item != 0 && Where(p.Item) == null).ToList();
            if (missing.Count == 1) bits.Add($"the {Slots.Word(missing[0]).ToLowerInvariant()} slot is left as it is");
            else if (missing.Count > 1) bits.Add($"the {LookSheet.Lower(missing.Count)} slots it can't fill are left as they are");
            line = "Open Glamour Plates at a glamour dresser: " + string.Join(", ", bits) + ".";
        }
        var wishable = look.Pieces.Values.Where(p => p.Item != 0 && Where(p.Item) == null && !sync.Library.Wishes.Contains(LookJson.BaseId(p.Item))).ToList();
        closedWords = (sync.Version, wardrobe.Version, look.Pieces, line, wishable);
        return (line, wishable);
    }

    /// <summary>The slip, closed (look.html's second frame): which plate when it's ready, how to get there, and the missing pieces' wishlist.</summary>
    private void DrawClosedSlip(Look look)
    {
        var ready = ReadinessOf(look);
        var meant = SentPlateOf(look);
        if (meant == 0) meant = PlanPlateOf(look);
        Kit.BeginSlip("##slip", 16, 18, Palette.Ground2);
        SlipTop("Take it into the game", LookSheet.WhenReady(meant, ready.PlateReady), 20);
        Kit.SlipTear();
        var (line, wishable) = ClosedWords(look, ready);
        Words12(line, Palette.Muted);
        if (Linked && wishable.Count > 0)
        {
            Kit.Gap(8);
            var at = Kit.At;
            var label = wishing ? "Putting them on your wishlist…"
                : wishable.Count == 1 ? $"Add {data.ItemName(wishable[0].Item)} to your wishlist" : $"Add the {LookSheet.Lower(wishable.Count)} to your wishlist";
            var bw = MathF.Min(Kit.ButtonWidth(label, BtnSize.Sm), Kit.Width);
            if (Kit.Button("##slip-wish", at, label, Btn.Glass, BtnSize.Sm, null, bw, wishing, "On your wishlist on the site, and the market board's and shops' panels point them out"))
            {
                var list = wishable;
                Later(() => AddWishes(list));
            }
            Kit.Reserve(at, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        }
        Kit.EndSlip();
    }

    /// <summary>
    /// The Looks pane's plate line, closed (looks.html's first frame, the mock's <c>.card.pad-s.row</c>):
    /// the plates icon, how ready it is, and how to get it on a plate.
    /// </summary>
    private void DrawPlateLine(Look look)
    {
        var ready = ReadinessOf(look);
        Kit.BeginPanel("##plate-line", 11, 14, 11);
        var at = Kit.At;
        var w = Kit.Width;
        var tx = at.X + S(18 + 10);
        var tw = w - S(18 + 10);
        SheetRun[0] = new Run(LookSheet.ReadyHead(ready), Face.SansSemibold, 13, Palette.Ink);
        var h1 = Txt.Flow(new Vector2(tx, at.Y), tw, SheetRun, 17, out _);
        var how = ready.Ready > 0 ? "At a glamour dresser, open Glamour Plates and it goes on in one press."
            : "Once its pieces are in your dresser or armoire, it goes on a plate in one press.";
        SheetRun[0] = new Run(how, Face.Sans, 12, Palette.Muted);
        var h2 = Txt.Flow(new Vector2(tx, at.Y + h1), tw, SheetRun, 16, out _);
        Icons.Draw(Kit.Dl, Icon.Layers, new Vector2(at.X, at.Y + (h1 + h2 - S(18)) / 2), S(18), Palette.Accent);
        Kit.Reserve(at, w, h1 + h2);
        Kit.EndPanel();
    }

    private void Fill(Look look, PlatePlan plan)
    {
        if (!PlateEditor.IsOpen)
        {
            Frame.Warn("The plate editor closed");
            return;
        }
        // The dresser and armoire as the game has them this moment.
        wardrobe.Tick(now: true);
        plan = PlanFor(look.Pieces);
        var result = PlateEditor.Fill(plan, wardrobe.FindDye);
        recorder.Note($"FILL plate {result.Plate} with “{look.Name}” ({look.Id}): set {result.Set}, cleared {result.Cleared}, dyed {result.Dyed}"
                      + (result.Problems.Count > 0 ? $"; problems: {string.Join(" | ", result.Problems)}" : string.Empty));
        // Nothing went on (the editor closed meanwhile): nothing to show as filled.
        if (result.Plate > 0 && result.Set + result.Cleared > 0)
        {
            lastFill = (look.Id, result, plan);
            lastFillName = look.Name;
            fillPieces = null;
            fillAt = DateTime.UtcNow;
            fillLeftAt = null;
        }
        if (result.Ok) Notify($"Plate {result.Plate}: done. Press Save in the game's window.");
        else Frame.Warn(result.Problems[0]);
        // A look sent from the site is done with once it's on a plate (only that look: Home's planned fill isn't it).
        if (openSent != null && openSent.Look.Id == look.Id && result.Set > 0)
        {
            sync.DoneWith(openSent.Id);
            openSent = null;
        }
    }

    /// <summary>The slip after filling: what went on, then Save in the game, then Wear plate N (the sticker) or Fill it again.</summary>
    private void DrawFilled(FillResult result, PlatePlan plan, bool compact)
    {
        var saved = !editorChanges;
        Kit.BeginSlip("##slip", 16, 18, Palette.Ground2);
        SlipTop(editorPlate == result.Plate && !saved ? "Filled: press Save in the game" : "Filled", $"Plate {result.Plate}", 26, "On the plate", TagKind.Ok, Icon.Check);
        Kit.SlipTear();
        Words12(Words.Filled(result.Plate, plan, result.Set), Palette.Body, compact ? 13 : 12);
        foreach (var problem in result.Problems)
        {
            Kit.Gap(4);
            DotLine(Palette.Amber, problem, compact ? 13 : 12);
        }
        Kit.Gap(10);
        var row = Kit.At;
        var blocked = Gearsets.Blocked(true);
        var wear = $"Wear plate {result.Plate}";
        if (Kit.Sticker("##wear", row, wear, StickerSize.Md, Icon.Shirt, 0, !saved || blocked != null,
                !saved ? "Save the plate in the game first" : blocked ?? "Puts the plate on your current gearset, as /gearset change does (in a sanctuary, on a job its gear fits)"))
            Later(() =>
            {
                if (PlateEditor.Wear(result.Plate) is { } why) Frame.Warn(why);
            });
        var x2 = row.X + Kit.StickerWidth(wear, StickerSize.Md, Icon.Shirt) + S(3 + 8);
        if (Kit.Button("##again", new Vector2(x2, row.Y + (Kit.StickerHeight() - Kit.ButtonHeight(BtnSize.Sm)) / 2), "Fill it again", Btn.Quiet, BtnSize.Sm, Icon.Refresh, iconCss: 14))
            lastFill = null;
        Kit.Reserve(row, Kit.Width, Kit.StickerHeight() + S(3));
        Kit.EndSlip();
    }

    /// <param name="say">Where to say how it went (a toast, else).</param>
    private void TryOn(IReadOnlyDictionary<Slot, Piece> pieces, Action<string>? say = null)
    {
        recorder.Note($"TRY ON pressed: {pieces.Values.Count(p => p.Item != 0)} pieces");
        var toast = say == null;
        if (toast) Notify("Opening the fitting room");
        FittingRoom.Start(pieces, (shown, wanted) =>
        {
            var text = shown == 0 ? "The fitting room didn't open"
                : shown == wanted ? "In the fitting room"
                : $"In the fitting room: {shown} of {wanted} pieces (the game wouldn't show the rest)";
            if (!toast) say!(text);
            else if (shown == wanted) Notify(text);
            else Frame.Warn(text);
        });
    }

    // ---------------------------------------------------------------------------------------------
    // Putting away: the look's pieces that are yours but not where a plate takes them from.

    /// <summary>Armoire rows asked to be stored, and when (a second ask waits for the game's answer).</summary>
    private readonly Dictionary<uint, DateTime> storing = new();

    /// <summary>
    /// Put away first (look.html's second frame, a card): each piece that's yours but not in the
    /// dresser or armoire, where it is, and with the armoire's window open, Into the armoire for the
    /// ones it takes (Storing… for five seconds once asked; not for a piece you're wearing).
    /// </summary>
    private void DrawPutAway(Look look)
    {
        var away = Slots.All.Where(look.Pieces.ContainsKey).Select(s => look.Pieces[s].Item)
            .Select(i => (Item: i, Where: Where(i)))
            .Where(x => x.Where is { } p && p != Place.Dresser && p != Place.Armoire)
            .ToList();
        if (away.Count == 0) return;
        var armoireOpen = Armoire.IsOpen;
        var now = DateTime.UtcNow;
        Kit.BeginPanel("##away", 14, 16, 14);
        Kit.SectionHead("##away-h", "Put away first", away.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Words12(armoireOpen
            ? "The armoire's open: the ones it takes go in from your bags or armoury chest. The rest go in your dresser."
            : "At a glamour dresser, store these from your bags or armoury chest (take them from saddlebags or retainers first). At an inn, with the armoire open, the ones it takes go in with a press.", Palette.Muted);
        Kit.Gap(2);
        var dl = Kit.Dl;
        for (var i = 0; i < away.Count; i++)
        {
            var (itemId, where) = away[i];
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(52);
            if (i > 0) Kit.Divider(at.X, at.X + w, at.Y);
            var item = data.Item(itemId);
            Kit.Item(new Vector2(at.X, at.Y + (h - S(36)) / 2), 36, item?.Icon ?? 0, Own.Amber);
            var x = at.X + S(36 + 11);
            var row = data.CabinetOf(itemId);
            var worn = row != null && armoireOpen && Armoire.IsWorn(itemId);
            var canArmoire = row != null && where is Place.Inventory or Place.Armoury && armoireOpen && !worn;
            // Asked already: the game takes a moment to answer, and a second ask would be a second store.
            var asked = row is { } rr && storing.TryGetValue(rr, out var askedAt) && now - askedAt < TimeSpan.FromSeconds(5);
            var put = asked ? "Storing…" : "Into the armoire";
            var bw = canArmoire ? Kit.ButtonWidth(put, BtnSize.Tiny) : 0;
            var tw = at.X + w - (bw > 0 ? bw + S(11) : 0) - x;
            var nh = Txt.Natural(Face.SansSemibold, 13);
            var lh = Txt.Natural(Face.Sans, 12);
            var y = at.Y + (h - nh - lh) / 2;
            Txt.Draw(dl, new Vector2(x, y), Txt.Fit(item?.Name ?? $"Item {itemId}", Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink);
            var place = Places.Where(where!.Value);
            var line = char.ToUpperInvariant(place[0]) + place[1..]
                       + (row == null ? " · for the dresser" : worn ? " · take it off for the armoire" : asked ? " · storing…" : " · the armoire takes it");
            Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted);
            if (bw > 0)
            {
                var cabinet = row!.Value;
                var name = item?.Name ?? "It";
                if (Kit.Button($"##armoire{i}", new Vector2(at.X + w - bw, at.Y + (h - Kit.ButtonHeight(BtnSize.Tiny)) / 2), put, Btn.Glass, BtnSize.Tiny, null, 0, asked))
                    Later(() =>
                    {
                        if (storing.TryGetValue(cabinet, out var t) && DateTime.UtcNow - t < TimeSpan.FromSeconds(5)) return;
                        var ok = Armoire.Store(cabinet);
                        recorder.Note($"ARMOIRE STORE row {cabinet} ({name}): the game {(ok ? "took the ask" : "said no")}");
                        if (!ok)
                        {
                            Frame.Warn("The armoire didn't take it");
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

    /// <summary>What the site said when it refused the shot last sent (shown on the line under the card, in amber).</summary>
    private string? shotRefused;

    /// <summary>The card's head: Worn in game in display capitals, a tag at the right.</summary>
    private static void ShotHead(string tag)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var th = Kit.TagHeight();
        var tw = Kit.TagWidth(tag);
        var dh = S(15 * 1.02f);
        var h = MathF.Max(dh, th);
        Kit.Display(new Vector2(at.X, at.Y + (h - Kit.DisplayHeight(15)) / 2), "Worn in game", 15, null, w - tw - S(10));
        Kit.Tag(new Vector2(at.X + w - tw, at.Y + (h - th) / 2), tag, TagKind.Plain);
        Kit.Reserve(at, w, h);
    }

    /// <summary>
    /// Worn in game (public looks; look.html's first and fourth frames): Take a shot; taken, its
    /// preview (cropped to fill 150 tall), its size, a caption, Send it to the look (outlined: the
    /// sticker stays elsewhere), Take another and Discard; sent, a line with the sparkle and See it.
    /// </summary>
    private void DrawShot(Look look)
    {
        var taken = shots.Taken is { } t && t.LookId == look.Id ? t : null;
        Kit.BeginPanel("##shot-card", 14, 16, 14);
        if (taken == null)
        {
            ShotHead("Public look");
            Kit.Gap(8);
            Words12("Wear it, frame it, take a shot. You see it here before it's sent to the look's page.", Palette.Muted);
            Kit.Gap(8);
            var row = Kit.At;
            var label = shots.Capturing ? "Taking it…" : "Take a shot";
            if (Kit.Button("##shot", row, label, Btn.Glass, BtnSize.Sm, Icon.Camera, 0, !Linked || shots.Capturing,
                    Linked ? "Hides the HUD, takes the game's picture, and shows it here to send" : "Link your account first (Settings → Account)", iconCss: 14))
                TakeShot(look);
            Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
            if (shots.Problem is { } problem && !shots.Capturing)
            {
                Kit.Gap(8);
                Words12(problem, Palette.Danger);
            }
            Kit.EndPanel();
            return;
        }

        ShotHead("3 a look at most");
        Kit.Gap(10);
        var w = Kit.Width;
        var dl = Kit.Dl;
        var at = Kit.At;
        var ph = S(150);
        var r = S(12);
        if (taken.Thumb is { } thumb && thumb.Width > 0 && thumb.Height > 0)
        {
            // Cropped to fill the box, as the mock's cover.
            var box = w / ph;
            var pic = (float)thumb.Width / thumb.Height;
            var span = pic > box ? new Vector2(box / pic, 1) : new Vector2(1, pic / box);
            var uv0 = (Vector2.One - span) / 2;
            dl.AddImageRounded(thumb.Handle, at, at + new Vector2(w, ph), uv0, uv0 + span, Palette.U32(Vector4.One), r);
        }
        else dl.AddRectFilled(at, at + new Vector2(w, ph), Palette.U32(Palette.Black), r);
        dl.AddRect(at + new Vector2(0.5f), at + new Vector2(w, ph) - new Vector2(0.5f), Palette.U32(Palette.Line), r, ImDrawFlags.None, 1f);
        Kit.Reserve(at, w, ph);
        if (taken.Jpeg is { } jpeg)
        {
            Kit.Gap(10);
            var sat = Kit.At;
            var size = $"{taken.Width} × {taken.Height} · {(jpeg.Length / 1024).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} KB";
            Txt.Draw(dl, sat, size, Face.Mono, 11, Palette.Faint);
            Kit.Reserve(sat, w, Txt.Natural(Face.Mono, 11));
        }
        // A word about the shot itself (the HUD in it), in amber under its preview; the site's refusal goes on the line under the card.
        if (taken.Note is { Bad: true } bad && bad.Text != shotRefused && !taken.Sent)
        {
            Kit.Gap(6);
            Words12(bad.Text, Palette.Amber);
        }
        Kit.Gap(10);
        var bh = Kit.ButtonHeight(BtnSize.Sm);
        if (!taken.Sent)
        {
            var field = Kit.At;
            var text = shotCaption;
            if (Kit.Field("##shot-caption", field, w, ref text, "A caption (if you like)", null, 200, heightCss: 34)) shotCaption = text;
            Kit.Reserve(field, w, S(34));
            Kit.Gap(10);
            var row = Kit.At;
            var send = taken.Sending ? "Sending…" : "Send it to the look";
            var sendW = Kit.ButtonWidth(send, BtnSize.Sm, Icon.Send, Btn.Ink);
            if (Kit.Button("##shot-send", row, send, Btn.Ink, BtnSize.Sm, Icon.Send, 0, taken.Sending || !Linked,
                    Linked ? "Goes on the look's page under Worn in game, as yours (three per look at most)" : "Link your account first (Settings → Account)", iconCss: 14))
                SendShot(look);
            var anotherW = Kit.ButtonWidth("Take another", BtnSize.Sm, null, Btn.Quiet);
            var another = new Vector2(row.X + sendW + S(8), row.Y);
            var rowH = bh;
            if (another.X + anotherW > row.X + w)
            {
                another = new Vector2(row.X, row.Y + bh + S(8));
                rowH = bh * 2 + S(8);
            }
            if (Kit.Button("##shot-again", another, "Take another", Btn.Quiet, BtnSize.Sm, null, 0, taken.Sending))
                TakeShot(look);
            Kit.Reserve(row, w, rowH);
        }
        else
        {
            var row = Kit.At;
            if (Kit.Button("##shot-again", row, "Take another", Btn.Glass, BtnSize.Sm, Icon.Camera, 0, false, null, iconCss: 14))
                TakeShot(look);
            Kit.Reserve(row, w, bh);
        }
        // The hand's words, and at the end of their line Discard (Done once it's sent).
        Kit.Gap(10);
        var foot = Kit.At;
        var end = taken.Sent ? "Done" : "Discard";
        var endW = Kit.ButtonWidth(end, BtnSize.Sm, null, Btn.Quiet);
        var handH = S(13 * 1.3f);
        Kit.Hand(new Vector2(foot.X, foot.Y + (bh - handH) / 2), "wore it? show us!", 13, w - endW - S(8));
        if (Kit.Button("##shot-discard", new Vector2(foot.X + w - endW, foot.Y), end, Btn.Quiet, BtnSize.Sm, null, 0, taken.Sending,
                taken.Sent ? null : "Lets this shot go (nothing's sent)"))
            Later(shots.Discard);
        Kit.Reserve(foot, w, bh);
        Kit.EndPanel();

        // Sent, or refused: a line under the card.
        var sent = taken.Sent;
        var refused = !sent && taken.Note is { Bad: true } n && n.Text == shotRefused ? n.Text : null;
        if (!sent && refused == null) return;
        Kit.Gap(14);
        Kit.BeginPanel("##shot-line", 10, 14, 10);
        var lat = Kit.At;
        var lw = Kit.Width;
        const string see = "See it";
        var seeW = sent ? Txt.Measure(see, Face.SansSemibold, 12).X : 0;
        var tx = lat.X + S(16 + 10);
        var tw = lw - S(16 + 10) - (sent ? seeW + S(10) : 0);
        SheetRun[0] = new Run(sent ? "Sent to the look: it's on its page under Worn in game." : refused!, Face.Sans, 12, sent ? Palette.Body : Palette.Amber);
        var th = Txt.Flow(new Vector2(tx, lat.Y), tw, SheetRun, 17, out _);
        var lh = MathF.Max(th, S(16));
        if (sent) Kit.Spark(new Vector2(lat.X, lat.Y + (lh - S(16)) / 2), 16);
        else Icons.Draw(dl, Icon.AlertTriangle, new Vector2(lat.X, lat.Y + (lh - S(16)) / 2), S(16), Palette.Amber);
        if (sent && Kit.Link("##shot-see", new Vector2(lat.X + lw - seeW, lat.Y + (lh - Txt.Natural(Face.SansSemibold, 12)) / 2), see, 12, Face.SansSemibold))
            Dalamud.Utility.Util.OpenLink(SiteLink.LookUrl(look.Id));
        Kit.Reserve(lat, lw, lh);
        Kit.EndPanel();
    }

    /// <summary>Takes a shot for the look (after the frame: the HUD's hidden for it).</summary>
    private void TakeShot(Look look)
    {
        var id = look.Id;
        var name = look.Name;
        Later(() =>
        {
            shotCaption = string.Empty;
            shotRefused = null;
            recorder.Note($"SHOT taking for “{name}” ({id})");
            if (!shots.Take(id, name, out var why)) Frame.Warn(why);
        });
    }

    private void SendShot(Look look)
    {
        var caption = shotCaption.Trim();
        var lookName = look.Name;
        shotRefused = null;
        shots.Send(caption, r =>
        {
            recorder.Note($"SHOT sent for “{lookName}”: {(r.Ok ? "ok" : $"refused ({r.Hint}): {r.Why}")}");
            if (r.Ok)
            {
                Notify("Sent to the look");
                return;
            }
            // (The shot keeps the same words as its note: the line under the card shows them.)
            shotRefused = r.Why ?? "The site said no.";
            Frame.Warn(r.Why ?? "The site said no");
        });
    }
}

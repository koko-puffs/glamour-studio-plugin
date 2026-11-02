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
/// Plates: all twenty of your glamour plates at once (each its number, its pieces' icons, the look it
/// is, a rose dot when one's planned for it on the site, the jobs of the gearsets it's linked to),
/// what you're wearing under them, and the plate picked beside: what's on it, its plan, Wear it, Try
/// it on, Keep it as a look, its gearsets (Change to it, unlink, Link to a gearset) and its slots.
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

    private IReadOnlyDictionary<Slot, Piece> PlatePieces(CharacterWardrobe me, int plate) =>
        plate == 0 ? me.Outfit : me.Plates.FirstOrDefault(p => p.Plate == plate)?.Pieces ?? NoPieces;

    private void DrawPlates(Vector2 min, Vector2 size)
    {
        var asideW = MathF.Min(S(340), size.X * 0.42f);
        var mainSize = new Vector2(size.X - asideW, size.Y);
        var me = wardrobe.Current;
        var sets = GearsetsNow();
        // The side pane's ground, on the window's own list (before the panes).
        var asideMin = new Vector2(min.X + mainSize.X, min.Y);
        var wdl = Kit.Dl;
        wdl.AddRectFilled(asideMin, asideMin + new Vector2(asideW, size.Y), Palette.U32(Palette.Rail), S(14), ImDrawFlags.RoundCornersBottomRight);
        wdl.AddLine(new Vector2(asideMin.X + 0.5f, asideMin.Y), new Vector2(asideMin.X + 0.5f, asideMin.Y + size.Y), Palette.U32(Palette.Divider), 1f);
        Kit.BeginPane("##plates-main", min, mainSize, SideCss, 20, 20, padRight: 20);
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        // The heading and what the marks mean.
        Txt.Line(dl, at, "Your glamour plates", Face.SansSemibold, 15, Palette.Ink);
        var hh = Txt.Natural(Face.SansSemibold, 15);
        // What the marks mean, on the right: each part only when it clears the heading.
        var titleEnd = at.X + Txt.Measure("Your glamour plates", Face.SansSemibold, 15).X + S(12);
        const string legend2 = "Gearset linked";
        var l2w = Txt.Measure(legend2, Face.Sans, 12).X;
        var chipW = Kit.ChipW("WHM", false, 18);
        var lx = at.X + w - l2w - S(6) - chipW;
        if (lx > titleEnd)
        {
            Txt.Draw(dl, new Vector2(at.X + w - l2w, at.Y + (hh - Txt.Natural(Face.Sans, 12)) / 2), legend2, Face.Sans, 12, Palette.Muted);
            Kit.Chip(new Vector2(lx, at.Y + (hh - S(18)) / 2), "WHM", ChipKind.Plain, false, 18);
            const string legend1 = "Planned on the site";
            var l1w = Txt.Measure(legend1, Face.Sans, 12).X;
            lx -= S(12) + l1w;
            if (lx - S(14) > titleEnd)
            {
                Txt.Draw(dl, new Vector2(lx, at.Y + (hh - Txt.Natural(Face.Sans, 12)) / 2), legend1, Face.Sans, 12, Palette.Muted);
                Kit.Dot(new Vector2(lx - S(10), at.Y + hh / 2), Palette.Rose, 8);
            }
        }
        Kit.Reserve(at, w, hh);
        Kit.Gap(12);

        if (me == null)
        {
            Txt.Note("Log in to a character to see its plates.");
            Kit.EndPane();
            Kit.BeginPane("##plates-aside", asideMin + new Vector2(1, 0), new Vector2(asideW - 1, size.Y), 22, 20, 20);
            Txt.Note("The plate you pick shows here.", Palette.Muted);
            Kit.EndPane();
            return;
        }
        var read = me.PlatesReadAt != null;

        // The grid: five across.
        var gat = Kit.At;
        var gap = S(8);
        var tileW = (w - gap * 4) / 5;
        var mosaic = MathF.Min(S(58), tileW - S(18));
        var tileH = S(9) + Txt.Natural(Face.Display, 15) + S(7) + mosaic + S(7) + Txt.Natural(Face.Sans, 11) + S(7) + S(18) + S(9);
        for (var i = 0; i < Plates.Count; i++)
        {
            var n = i + 1;
            var p = new Vector2(gat.X + (tileW + gap) * (i % 5), gat.Y + (tileH + gap) * (i / 5));
            if (PlateTile(n, p, new Vector2(tileW, tileH), mosaic, me, read, sets)) pickedPlate = n;
        }
        Kit.Reserve(gat, w, tileH * 4 + gap * 3);
        Kit.Gap(12);
        var foot = read && me.PlatesReadAt is { } readAt
            ? $"As the game had them when you last opened Glamour Plates · {Words.Ago(new DateTimeOffset(readAt, TimeSpan.Zero), DateTimeOffset.UtcNow)}"
            : "Your plates show here once you've opened Glamour Plates at a glamour dresser (the game only has them then).";
        Txt.Para(new[] { new Run(foot, Face.Sans, 12, read ? Palette.Muted : Palette.RoseText) }, 18);

        // What you're wearing.
        if (me.Outfit.Count > 0)
        {
            Kit.Gap(16);
            DrawWornRow(me);
        }
        Kit.EndPane();

        Kit.BeginPane("##plates-aside", asideMin + new Vector2(1, 0), new Vector2(asideW - 1, size.Y), 22, 20, 20);
        DrawPlateAside(me, Math.Clamp(pickedPlate, 1, Plates.Count), read, sets);
        Kit.EndPane();
    }

    /// <summary>A plate in the grid; true when pressed.</summary>
    private bool PlateTile(int plate, Vector2 pos, Vector2 size, float mosaic, CharacterWardrobe me, bool read, List<Gearset> sets)
    {
        var pieces = PlatePieces(me, plate);
        var filled = pieces.Count > 0;
        var on = plate == pickedPlate;
        var (clicked, hovered, _) = Kit.Hit($"##plate-tile{plate}", pos, size);
        var dl = Kit.Dl;
        var r = S(14);
        if (on)
        {
            dl.AddRectFilled(pos, pos + size, Palette.U32(Palette.RoseWash), r);
            dl.AddRect(pos + new Vector2(0.5f), pos + size - new Vector2(0.5f), Palette.U32(Palette.Rose), r, ImDrawFlags.None, 1f);
            dl.AddRect(pos - new Vector2(0.5f), pos + size + new Vector2(0.5f), Palette.U32(Palette.Rose), r + 1, ImDrawFlags.None, 1f);
        }
        else if (filled) Kit.Card(pos, pos + size, Palette.Card, hovered ? Palette.LineHover : Palette.Line, 14);
        else Kit.Dashed(pos, pos + size, r, hovered ? Palette.LineHover : Palette.LineStrong, 1f);
        var x = pos.X + S(9);
        var y = pos.Y + S(9);
        Txt.Draw(dl, new Vector2(x, y), plate.ToString(), Face.Display, 15, Palette.Ink);
        if (sync.PlanFor(plate) != null) Kit.Dot(new Vector2(pos.X + size.X - S(13), pos.Y + S(13)), Palette.Rose, 8);
        y += Txt.Natural(Face.Display, 15) + S(7);
        if (filled) Mosaic(new Vector2(x, y), mosaic, pieces, 10, 6);
        else
        {
            Kit.Dashed(new Vector2(x, y), new Vector2(x + mosaic, y + mosaic), S(10), Palette.LineStrong, 1f);
            Icons.Draw(dl, Icon.Plus, new Vector2(x + (mosaic - S(14)) / 2, y + (mosaic - S(14)) / 2), S(14), Palette.Dim);
        }
        y += mosaic + S(7);
        var match = filled ? PlateMatch(plate, pieces) : null;
        var name = !read ? "Not read yet" : !filled ? "Empty" : match?.Name ?? Words.Count(pieces.Count, "piece");
        Txt.Draw(dl, new Vector2(x, y), Txt.Fit(name, Face.Sans, 11, size.X - S(18)), Face.Sans, 11, match != null ? Palette.Ink : Palette.Muted);
        y += Txt.Natural(Face.Sans, 11) + S(7);
        var cx = x;
        foreach (var g in sets.Where(g => g.Plate == plate))
        {
            var job = data.Job(g.Job);
            if (job.Length == 0) job = g.Number.ToString();
            var cw = Kit.ChipW(job, false, 18);
            if (cx + cw > pos.X + size.X - S(9)) break;
            Kit.Chip(new Vector2(cx, y), job, ChipKind.Plain, false, 18);
            cx += cw + S(3);
        }
        if (hovered) Kit.Tooltip(match != null ? $"Plate {plate}: “{match.Name}”" : $"Plate {plate}: {name}");
        return clicked;
    }

    /// <summary>What you're wearing, under the grid: Try it on and Keep it as a look.</summary>
    private void DrawWornRow(CharacterWardrobe me)
    {
        Kit.BeginPanel("##worn", 12, 14, 12, Palette.Card, Palette.Line, 14);
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        Mosaic(at, S(40), me.Outfit, 9);
        var match = PlateMatch(0, me.Outfit);
        var name = me.Name.Length > 0 ? $"{me.Name}'s outfit" : "My outfit";
        var kept = keptFrom.TryGetValue((me.Key, 0), out var k) && Plates.Same(k.Pieces, me.Outfit) ? k.Look : null;
        var busy = keeping.Contains((me.Key, 0));
        var keepLabel = kept != null ? "Open it on the site" : busy ? "Keeping…" : "Keep it as a look";
        var keepIcon = kept != null ? Icon.ExternalLink : Icon.Sparkles;
        var kw = Kit.ButtonWidth(keepLabel, BtnSize.Sm, keepIcon);
        var tw2 = Kit.ButtonWidth("Try it on", BtnSize.Sm, Icon.Eye);
        var bx = at.X + w - kw - S(8) - tw2;
        var by = at.Y + (S(40) - Kit.ButtonHeight(BtnSize.Sm)) / 2;
        if (Kit.Button("##worn-try", new Vector2(bx, by), "Try it on", Btn.Glass, BtnSize.Sm, Icon.Eye)) Later(() => TryOn(me.Outfit));
        if (kept != null)
        {
            if (Kit.Button("##worn-open", new Vector2(bx + tw2 + S(8), by), keepLabel, Btn.Glass, BtnSize.Sm, keepIcon, 0, false, $"Kept as “{kept.Name}” in your looks"))
                Dalamud.Utility.Util.OpenLink(SiteLink.DressingUrl(kept.Id));
        }
        else if (Kit.Button("##worn-keep", new Vector2(bx + tw2 + S(8), by), keepLabel, Btn.Glass, BtnSize.Sm, keepIcon, 0, !Linked || busy || match != null,
                     match != null ? $"It's your look “{match.Name}” already" : Linked ? $"Saves it to your looks as “{name}” (private)" : "Link your account first (Settings → Account)"))
            KeepAsLook(me, 0, name, me.Outfit);
        var x = at.X + S(40) + S(12);
        var tw = bx - S(12) - x;
        var nh = Txt.Natural(Face.SansSemibold, 13);
        var lh = Txt.Natural(Face.Sans, 12);
        var y = at.Y + (S(40) - nh - lh) / 2;
        Txt.Draw(dl, new Vector2(x, y), Txt.Fit("What you're wearing", Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink);
        Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(match != null ? $"Your look “{match.Name}”" : $"{Words.Count(me.Outfit.Count, "piece")} · glamour and dyes", Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted);
        Kit.Reserve(at, w, S(40));
        Kit.EndPanel();
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
            else Notify(r.Why ?? "The site said no");
        });
    }

    // ---------------------------------------------------------------------------------------------
    // The plate picked.

    private void DrawPlateAside(CharacterWardrobe me, int plate, bool read, List<Gearset> sets)
    {
        var pieces = PlatePieces(me, plate);
        var match = pieces.Count > 0 ? PlateMatch(plate, pieces) : null;
        var at = Kit.At;
        var w = Kit.Width;
        var y = at.Y;
        y += Kit.Eyebrow(new Vector2(at.X, y), "Glamour plate", null, w) + S(4);
        y += Title(new Vector2(at.X, y), $"Plate {plate}", w, 22, 1) + S(4);
        var about = !read ? "Not read yet: open Glamour Plates at a glamour dresser once."
            : pieces.Count == 0 ? "Empty. Put a look on it at a glamour dresser."
            : match != null ? $"It's your look “{match.Name}”."
            : $"It has {Words.Count(pieces.Count, "piece")}: not one of your looks.";
        y += Txt.Flow(new Vector2(at.X, y), w, new[] { new Run(about, Face.Sans, 13, Palette.Muted) }, 19.5f, out _);
        Kit.Reserve(at, w, y - at.Y);

        // Its plan on the site.
        if (sync.PlanFor(plate) is { } planned)
        {
            Kit.Gap(14);
            var on = Plates.Same(pieces, planned.Pieces);
            Kit.BeginPanel("##plan", 12, 14, 12, Palette.Card, on ? Palette.SuccessEdge : Palette.RoseEdge, 14);
            var pa = Kit.At;
            var pw = Kit.Width;
            var chip = on ? "On it" : "Not on it yet";
            var cw = Kit.ChipW(chip, on, 22);
            var ph = S(24);
            Kit.Dot(new Vector2(pa.X + S(4), pa.Y + ph / 2), on ? Palette.Success : Palette.Rose, 8);
            var tx = pa.X + S(18);
            var lab = Txt.Line(Kit.Dl, new Vector2(tx, pa.Y), "Planned: ", Face.Sans, 13, Palette.Soft, 24);
            var nameRoom = pa.X + pw - cw - S(10) - tx - lab;
            var nm = Txt.Fit(planned.Name, Face.SansSemibold, 13, nameRoom);
            var nh = Txt.LineBox(Face.SansSemibold, 13, 24);
            if (Kit.Link("##plan-look", new Vector2(tx + lab, pa.Y + (nh - Txt.Natural(Face.SansSemibold, 13)) / 2), nm, 13, Face.SansSemibold))
                Later(() => OpenLook(planned, null, Screen.Plates));
            Kit.Chip(new Vector2(pa.X + pw - cw, pa.Y + S(1)), chip, on ? ChipKind.Ok : ChipKind.Rose, on, 22);
            Kit.Reserve(pa, pw, ph);
            Kit.EndPanel();
        }

        // Wear it, Try it on, Keep it as a look.
        Kit.Gap(14);
        var wrap = new Wrap(8);
        const string wearLabel = "Wear it";
        var wearW = Kit.ButtonWidth(wearLabel, BtnSize.Sm, Icon.Shirt, Btn.Primary);
        var wearBlocked = Gearsets.Blocked(true);
        if (Kit.Button("##wear-plate", wrap.Next(wearW, Kit.ButtonHeight(BtnSize.Sm)), wearLabel, Btn.Primary, BtnSize.Sm, Icon.Shirt, 0, pieces.Count == 0 || wearBlocked != null,
                wearBlocked ?? "Puts the plate on your current gearset, as /gearset change does (in a sanctuary, on a job its gear fits)"))
            Later(() =>
            {
                var why = PlateEditor.Wear(plate);
                recorder.Note($"WEAR plate {plate}: {why ?? "asked the game"}");
                if (why != null) Notify(why);
            });
        const string tryLabel = "Try it on";
        var tryW = Kit.ButtonWidth(tryLabel, BtnSize.Sm, Icon.Eye);
        if (Kit.Button("##try-plate", wrap.Next(tryW, Kit.ButtonHeight(BtnSize.Sm)), tryLabel, Btn.Glass, BtnSize.Sm, Icon.Eye, 0, pieces.Count == 0))
            Later(() => TryOn(pieces));
        if (pieces.Count > 0 && match == null)
        {
            var kept = keptFrom.TryGetValue((me.Key, plate), out var k) && Plates.Same(k.Pieces, pieces) ? k.Look : null;
            if (kept != null)
            {
                const string open = "Open it on the site";
                var ow = Kit.ButtonWidth(open, BtnSize.Sm, Icon.ExternalLink);
                if (Kit.Button("##kept", wrap.Next(ow, Kit.ButtonHeight(BtnSize.Sm)), open, Btn.Glass, BtnSize.Sm, Icon.ExternalLink, 0, false, $"Kept as “{kept.Name}” in your looks"))
                    Dalamud.Utility.Util.OpenLink(SiteLink.DressingUrl(kept.Id));
            }
            else
            {
                var busy = keeping.Contains((me.Key, plate));
                var label = busy ? "Keeping…" : "Keep it as a look";
                var kw = Kit.ButtonWidth(label, BtnSize.Sm);
                if (Kit.Button("##keep", wrap.Next(kw, Kit.ButtonHeight(BtnSize.Sm)), label, Btn.Glass, BtnSize.Sm, null, 0, !Linked || busy,
                        Linked ? $"Saves it to your looks on Glamour Studio as “Plate {plate}” (private: only you see it)" : "Link your account first (Settings → Account)"))
                    KeepAsLook(me, plate, $"Plate {plate}", pieces);
            }
        }
        wrap.End();

        // Its gearsets.
        if (sets.Count > 0)
        {
            Kit.Gap(14);
            DrawGearsets(plate, sets);
        }

        // Its slots.
        Kit.Gap(14);
        Kit.Heading("On it", Words.Count(pieces.Count, "piece"), 14);
        Kit.Gap(14);
        var gat = Kit.At;
        var gap = S(6);
        var tile = (w - gap * 5) / 6;
        for (var i = 0; i < 12; i++)
        {
            var slot = Slots.PlateOrder[i];
            var p = new Vector2(gat.X + (tile + gap) * (i % 6), gat.Y + (tile + gap) * (i / 6));
            if (pieces.TryGetValue(slot, out var piece))
            {
                Kit.GameIcon(p, tile, data.Item(piece.Item)?.Icon ?? 0);
                if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(p, p + new Vector2(tile)))
                    Kit.Tooltip($"{Slots.Word(slot)}: {data.ItemName(piece.Item)}" + (DyeLine(piece) is { } d ? $" · {d}" : string.Empty));
            }
            else Kit.EmptyTile(p, tile);
        }
        Kit.Reserve(gat, w, tile * 2 + gap);
    }

    // ---------------------------------------------------------------------------------------------
    // Gearsets.

    private List<Gearset> gearsets = new();
    private DateTime gearsetsAt;

    /// <summary>Your gearsets, read again a second after the last read (they change in the game's own window).</summary>
    private List<Gearset> GearsetsNow()
    {
        if (DateTime.UtcNow - gearsetsAt > TimeSpan.FromSeconds(1))
        {
            gearsets = Gearsets.All();
            gearsetsAt = DateTime.UtcNow;
        }
        return gearsets;
    }

    private void DrawGearsets(int plate, List<Gearset> all)
    {
        var linked = all.Where(g => g.Plate == plate).ToList();
        Kit.Heading("Gearsets", "Change to one: its plate comes with it", 14);
        Kit.Gap(14);
        Kit.BeginPanel("##gearsets", 4, 4, 4, Palette.Card, Palette.Line, 14);
        var dl = Kit.Dl;
        for (var i = 0; i < linked.Count; i++)
        {
            var g = linked[i];
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(8) * 2 + S(30);
            var x = at.X + S(10);
            var job = data.Job(g.Job);
            var chip = job.Length > 0 ? job : "—";
            Kit.Chip(new Vector2(x, at.Y + (h - S(22)) / 2), chip, ChipKind.Plain, false, 22, S(40));
            x += MathF.Max(S(40), Kit.ChipW(chip, false, 22)) + S(10);
            const string change = "Change to it";
            var cw = Kit.ButtonWidth(change, BtnSize.Sm, null, Btn.Quiet) - S(8);
            var ux = at.X + w - S(10) - S(30);
            var bx = ux - S(10) - cw;
            var num = g.Number.ToString("00");
            var nw = Txt.Draw(dl, new Vector2(x, at.Y + (h - Txt.Natural(Face.Mono, 13)) / 2), num, Face.Mono, 13, Palette.Dim);
            Txt.Draw(dl, new Vector2(x + nw + S(6), at.Y + (h - Txt.Natural(Face.Sans, 13)) / 2), Txt.Fit(g.Name, Face.Sans, 13, bx - S(8) - x - nw - S(6)), Face.Sans, 13, Palette.Ink);
            var id = g.Id;
            var equipBlocked = Gearsets.Blocked(false);
            if (Kit.Button($"##gs-equip{i}", new Vector2(bx, at.Y + (h - S(30)) / 2), change, Btn.Quiet, BtnSize.Sm, null, cw, equipBlocked != null,
                    equipBlocked ?? (Gearsets.InSanctuary ? "Changes to this gearset: its plate comes with it" : "Changes to this gearset (its plate goes on only in a sanctuary: a city, an inn or a residential area)"), S(30)))
                Later(() =>
                {
                    var why = Gearsets.Equip(id);
                    recorder.Note($"GEARSET EQUIP {id + 1}: {why ?? "the game took it"}");
                    if (why != null) Notify(why);
                    else if (!Gearsets.InSanctuary) Notify("Changed. Its plate goes on only in a sanctuary.");
                });
            if (Kit.IconButton($"##gs-unlink{i}", new Vector2(ux, at.Y + (h - S(30)) / 2), Icon.X, "Unlink the plate from this gearset", 30, Btn.Glass, false, 14))
                Later(() =>
                {
                    var ok = Gearsets.Link(id, 0);
                    recorder.Note($"GEARSET UNLINK {id + 1} (was plate {plate}): {(ok ? "asked the game" : "couldn't ask")}");
                    Notify(ok ? $"Gearset {id + 1} has no plate now" : "The game wouldn't unlink it");
                    gearsetsAt = DateTime.MinValue;
                });
            Kit.Reserve(at, w, h);
        }
        if (linked.Count > 0) Kit.Gap(4);
        // Link to a gearset: the dashed row, a menu of the others.
        var la = Kit.At;
        var lw = Kit.Width;
        var lh = S(40);
        var (clicked, hovered, _) = Kit.Hit("##gs-link", la, new Vector2(lw, lh));
        Kit.Dashed(la, la + new Vector2(lw, lh), S(12), hovered ? Palette.LineHover : Palette.LineStrong, 1f);
        Icons.Draw(dl, Icon.Link, new Vector2(la.X + S(12), la.Y + (lh - S(16)) / 2), S(16), hovered ? Palette.Ink : Palette.Light);
        Txt.Line(dl, new Vector2(la.X + S(12 + 16 + 10), la.Y), "Link to a gearset", Face.SansMedium, 13, hovered ? Palette.Ink : Palette.Light, 40);
        if (clicked) ImGui.OpenPopup("##gs-link-menu");
        Kit.Reserve(la, lw, lh);
        if (Kit.BeginMenu("##gs-link-menu"))
        {
            var others = all.Where(g => g.Plate != plate).ToList();
            if (others.Count == 0) Txt.Note("Every gearset has this plate.", Palette.Muted);
            foreach (var g in others)
            {
                var job = data.Job(g.Job);
                var label = $"{g.Number:00}  {(job.Length > 0 ? job + " · " : string.Empty)}{g.Name}" + (g.Plate > 0 ? $"  (plate {g.Plate} now)" : string.Empty);
                if (Kit.MenuItem($"##gs-pick{g.Id}", label, null, S(300)))
                {
                    var id = g.Id;
                    Later(() =>
                    {
                        var ok = Gearsets.Link(id, plate);
                        recorder.Note($"GEARSET LINK {id + 1} to plate {plate}: {(ok ? "asked the game" : "couldn't ask")}");
                        Notify(ok ? $"Gearset {id + 1} wears plate {plate} now" : "The game wouldn't link it");
                        gearsetsAt = DateTime.MinValue;
                    });
                    ImGui.CloseCurrentPopup();
                }
            }
            Kit.EndMenu();
        }
        if (linked.Count == 0)
        {
            Kit.Gap(6);
            Txt.Para(new[] { new Run("Link it to a gearset and changing to that gearset puts the plate on.", Face.Sans, 12, Palette.Muted) }, 18);
            Kit.Gap(4);
        }
        Kit.EndPanel();
    }
}

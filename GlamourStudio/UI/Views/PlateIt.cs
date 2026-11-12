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
/// Putting a look on a plate in one go, with the glamour dresser open: pick the plate (a menu of the
/// twenty, what each has on it), and the plugin opens Glamour Plates (the dresser's own button),
/// picks that plate (as clicking it does), waits for the game to show it, and fills it, as Fill plate
/// does. Nothing's saved: Save is pressed in the game's window. It won't pick another plate over
/// changes not yet saved (the game would ask about them).
/// </summary>
public sealed partial class MainWindow
{
    private enum PlateItStep { Opening, Picking, Settling }

    private sealed class PlateItJob
    {
        public required Look Look { get; init; }
        public required int Plate { get; init; }
        public PlateItStep Step { get; set; }
        public bool Asked { get; set; }
        public DateTime Until { get; set; }

        /// <summary>When the editor's open long enough for its plate to be read (unsaved changes, the plate picked).</summary>
        public DateTime ReadyAt { get; set; }
    }

    private PlateItJob? plateIt;

    /// <summary>The look whose plate menu is open (the menu is drawn where its button is).</summary>
    private Look? plateItMenu;

    /// <summary>Whether a look can go on a plate in one go now: the dresser, or its plate editor, open.</summary>
    private static bool PlateItHere => PlateEditor.IsOpen || GameWindows.IsShown("MiragePrismPrismBox");

    /// <summary>Starts putting <paramref name="look"/> on <paramref name="plate"/>.</summary>
    private void StartPlateIt(Look look, int plate)
    {
        if (AutoBusy)
        {
            Frame.Warn("Wait for Make room for me to finish first");
            return;
        }
        if (PlateEditor.IsOpen && PlateEditor.SelectedPlate != plate && PlateEditor.HasChanges)
        {
            Frame.Warn($"Plate {PlateEditor.SelectedPlate} has changes not saved: save them, or put it back, first");
            return;
        }
        if (!PlateItHere)
        {
            Frame.Warn("Open your glamour dresser first");
            return;
        }
        plateIt = new PlateItJob { Look = look, Plate = plate, Step = PlateItStep.Opening, Until = DateTime.UtcNow + TimeSpan.FromSeconds(6) };
        recorder.Note($"PLATE IT “{look.Name}” ({look.Id}) on plate {plate}");
    }

    /// <summary>The next step of putting a look on a plate (every frame).</summary>
    private void TickPlateIt()
    {
        if (plateIt is not { } job) return;
        var now = DateTime.UtcNow;
        void Fail(string why)
        {
            recorder.Note($"PLATE IT plate {job.Plate}: {why}");
            Frame.Warn(why);
            plateIt = null;
        }
        switch (job.Step)
        {
            case PlateItStep.Opening:
                if (PlateEditor.IsOpen)
                {
                    job.Step = PlateItStep.Picking;
                    job.Asked = false;
                    // (A moment before its plate is read: on its first frames the editor's slots may not be in yet.)
                    job.ReadyAt = now + TimeSpan.FromMilliseconds(300);
                    job.Until = now + TimeSpan.FromSeconds(3);
                    break;
                }
                // The dresser closed before Glamour Plates opened: neither window there after 3 s (opening
                // it may hide the dresser, and its first opening loads the plates).
                if (job.Asked && !GameWindows.IsShown("MiragePrismPrismBox") && !GameWindows.IsShown("MiragePrismMiragePlate")
                    && now > job.Until - TimeSpan.FromSeconds(3))
                {
                    Fail("The glamour dresser closed");
                    break;
                }
                if (!job.Asked)
                {
                    job.Asked = true;
                    if (!GameWindows.OpenPlatesFromDresser())
                    {
                        Fail("Open your glamour dresser first");
                        break;
                    }
                }
                if (now > job.Until) Fail("Glamour Plates didn't open");
                break;
            case PlateItStep.Picking:
                if (!PlateEditor.IsOpen)
                {
                    Fail("Glamour Plates closed");
                    break;
                }
                if (now < job.ReadyAt) break;
                if (PlateEditor.SelectedPlate == job.Plate)
                {
                    // A moment for the plate's slots to come in before they're read.
                    job.Step = PlateItStep.Settling;
                    job.Until = now + TimeSpan.FromMilliseconds(300);
                    break;
                }
                if (!job.Asked)
                {
                    if (PlateEditor.HasChanges)
                    {
                        Fail($"Plate {PlateEditor.SelectedPlate} has changes not saved: save them, or put it back, first");
                        break;
                    }
                    job.Asked = true;
                    PlateEditor.PickPlate(job.Plate);
                }
                if (now > job.Until) Fail($"Plate {job.Plate} didn't come up: pick it in the game's window, then Fill plate {job.Plate}");
                break;
            case PlateItStep.Settling:
                if (now < job.Until) break;
                if (!PlateEditor.IsOpen || PlateEditor.SelectedPlate != job.Plate)
                {
                    Fail($"Plate {job.Plate} isn't the one open any more");
                    break;
                }
                plateIt = null;
                // The plates panel shows this look for that plate.
                companionPick = job.Look;
                companionPickPlate = job.Plate;
                Fill(job.Look, PlanFor(job.Look.Pieces));
                break;
        }
    }

    /// <summary>The menu's rows for a look, worked out again only when your looks, the wardrobe or the look change.</summary>
    private (int Sync, int Wardrobe, string LookId, bool Extra, IReadOnlyList<(string Heading, int[] Plates)> Groups, string?[] Subs)? plateMenu;

    private (IReadOnlyList<(string Heading, int[] Plates)> Groups, string?[] Subs) PlateMenuFor(Look look)
    {
        var extra = !config.ClearBare;
        if (plateMenu is { } c && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version && c.LookId == look.Id && c.Extra == extra)
            return (c.Groups, c.Subs);
        var me = wardrobe.Current;
        var read = me?.PlatesReadAt != null;
        var subs = new string?[Plates.Count + 1];
        var empty = new bool[Plates.Count + 1];
        for (var n = 1; n <= Plates.Count; n++)
        {
            var pieces = me?.Plates.FirstOrDefault(p => p.Plate == n)?.Pieces;
            empty[n] = read && (pieces == null || pieces.Count == 0);
            subs[n] = LookSheet.PlateSub(PlateOn(n, look, pieces, read), sync.PlanFor(n)?.Name);
        }
        var groups = LookSheet.PlateGroups(Plates.Count, n => sync.PlanFor(n)?.Id == look.Id, n => empty[n]);
        plateMenu = (sync.Version, wardrobe.Version, look.Id, extra, groups, subs);
        return (groups, subs);
    }

    /// <summary>What a plate has on, in a few words ("Empty", this look, another of your looks, "8 pieces"), or null when the plates aren't read.</summary>
    private string? PlateOn(int plate, Look look, IReadOnlyDictionary<Slot, Piece>? pieces, bool read)
    {
        if (!read) return null;
        if (pieces == null || pieces.Count == 0) return "Empty";
        if (OnPlate(look.Pieces, pieces, !config.ClearBare)) return "This look";
        var on = PaneLooks().Concat(sync.Inbox.Select(s => s.Look)).FirstOrDefault(l => OnPlate(l.Pieces, pieces));
        return on != null ? on.Name : Words.Count(pieces.Count, "piece");
    }

    /// <summary>The plate menu's width (the mock's <c>.platepick</c>: 330 with its 6px inside).</summary>
    private const float PlateMenuCss = 330;

    /// <summary>
    /// The menu of plates for a look (opened by <see cref="OpenPlateItMenu"/>; the mock's
    /// <c>.platepick</c>): the plates planned for it first, then the empty ones, then the rest, each
    /// with its number in mono, its name and what it has on under it. Opened from the plate slip it
    /// hangs under its button, its right edge at <paramref name="rightTop"/>; elsewhere at the mouse.
    /// </summary>
    private void DrawPlateItMenu(string id, Vector2? rightTop = null)
    {
        if (plateItMenu is not { } look) return;
        if (rightTop is { } rt && ImGui.IsPopupOpen(id))
            ImGui.SetNextWindowPos(new Vector2(rt.X - S(PlateMenuCss), rt.Y), ImGuiCond.Always);
        if (!Kit.BeginMenu(id)) return;
        var (groups, subs) = PlateMenuFor(look);
        // Over changes not saved in the plate open, no other plate can be picked (the game would ask about them).
        var held = PlateEditor.IsOpen && PlateEditor.HasChanges ? PlateEditor.SelectedPlate : 0;
        var w = S(PlateMenuCss - 12);
        for (var g = 0; g < groups.Count; g++)
        {
            var (heading, plates) = groups[g];
            if (g > 0) MenuLine(w);
            Kit.MenuHeading(heading);
            var planned = g == 0 && plates.Length > 0 && sync.PlanFor(plates[0])?.Id == look.Id;
            foreach (var plate in plates)
            {
                var blocked = held > 0 && plate != held;
                var sub = blocked ? $"plate {held}'s changes aren't saved: save them first" : subs[plate];
                if (PlateRow($"{id}-{plate}", plate, sub, w, planned, blocked))
                {
                    ImGui.CloseCurrentPopup();
                    var l = look;
                    var p = plate;
                    Later(() => StartPlateIt(l, p));
                }
            }
        }
        Kit.EndMenu();
    }

    /// <summary>Opens the plate menu for a look (call where the menu's drawn, the same frame).</summary>
    private void OpenPlateItMenu(string id, Look look)
    {
        plateItMenu = look;
        ImGui.OpenPopup(id);
    }

    /// <summary>
    /// A plate in the menu (the mock's <c>.platepick .mi</c>, 40 tall at least): its number in Geist
    /// Mono, "Plate 5" and under it what it has on in faint; the one planned for the look lilac-washed
    /// with an arrow. The Kit's <see cref="Kit.MenuItem(string, string, Icon?, float, string?, string?, bool, bool)"/>
    /// has its second line beside the words, so this is the menu's own.
    /// </summary>
    private static bool PlateRow(string id, int plate, string? sub, float width, bool on, bool disabled)
    {
        var pos = ImGui.GetCursorScreenPos();
        var nh = Txt.Natural(Face.SansMedium, 13);
        var sh = sub != null ? Txt.Natural(Face.Sans, 11) : 0;
        var h = MathF.Max(S(40), S(5) * 2 + nh + sh);
        var (clicked, hovered, _) = Kit.Hit(id, pos, new Vector2(width, h), disabled);
        var dl = Kit.Dl;
        var old = Palette.Opacity;
        if (disabled) Palette.Opacity = old * 0.45f;
        if (hovered || on) dl.AddRectFilled(pos, pos + new Vector2(width, h), Palette.U32(Palette.AccentWash), S(10));
        var x = pos.X + S(10);
        var num = LookSheet.PlateNumber(plate);
        Txt.Draw(dl, new Vector2(x, pos.Y + (h - Txt.Natural(Face.MonoMedium, 12)) / 2), num, Face.MonoMedium, 12, Palette.Muted);
        x += S(26 + 9);
        var right = pos.X + width - S(10) - (on ? S(14 + 9) : 0);
        var y = pos.Y + (h - nh - sh) / 2;
        Txt.Draw(dl, new Vector2(x, y), $"Plate {plate}", Face.SansMedium, 13, hovered || on ? Palette.Ink : Palette.Body);
        if (sub != null) Txt.Draw(dl, new Vector2(x, y + nh), Txt.Fit(sub, Face.Sans, 11, right - x), Face.Sans, 11, Palette.Faint);
        if (on) Icons.Draw(dl, Icon.ArrowRight, new Vector2(pos.X + width - S(10 + 14), pos.Y + (h - S(14)) / 2), S(14), Palette.Accent);
        Palette.Opacity = old;
        Kit.FocusRing(pos, pos + new Vector2(width, h), S(10));
        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(new Vector2(width, h));
        return clicked;
    }

    /// <summary>The line between the menu's groups (the mock's <c>.sep</c>: 4px above and under, 6 in from its sides), across the menu.</summary>
    private static void MenuLine(float width)
    {
        var pos = ImGui.GetCursorScreenPos();
        Kit.Dl.AddLine(pos + new Vector2(S(6), S(4) + 0.5f), pos + new Vector2(width - S(6), S(4) + 0.5f), Palette.U32(Palette.Line), 1f);
        ImGui.Dummy(new Vector2(width, S(9)));
    }

    /// <summary>
    /// The sticker that opens a menu (the mock's "Put on a plate… ⌄"): the Kit's sticker, large, with
    /// a chevron after its words. The Kit's has its icon before its words only, so this one is drawn
    /// here with the same measures (46 tall, 22 either side, Figtree 800 15, 14px corners, the 3px
    /// block), on the ink (the slip it sits in isn't on the field). Its block reaches 3px past it.
    /// </summary>
    private static bool MenuSticker(string id, Vector2 pos, string label, Icon icon, bool disabled, string? tip, out Vector2 size)
    {
        const int px = 15;
        var h = S(46);
        var chev = S(14);
        var iconPx = S(16);
        var textW = Txt.Measure(label, Face.SansExtraBold, px).X;
        var w = S(2 + 22) * 2 + iconPx + S(8) + textW + S(8) + chev;
        size = new Vector2(w, h);
        var (clicked, hovered, held) = Kit.Hit(id, pos, size, disabled);
        if (tip != null && !Kit.Blocked && ImGui.IsMouseHoveringRect(pos, pos + size) && ImGui.IsWindowHovered()) Kit.Tooltip(tip);
        var old = Palette.Opacity;
        if (disabled) Palette.Opacity = old * 0.5f;
        var dl = Kit.Dl;
        var r = S(14);
        var off = S(3);
        var pressed = held && !disabled;
        var p = pressed ? pos + new Vector2(off) : pos;
        if (!pressed) dl.AddRectFilled(pos + new Vector2(off), pos + size + new Vector2(off), Palette.U32(Palette.StickerShadow), r);
        dl.AddRectFilled(p, p + size, Palette.U32(hovered || held ? Palette.FieldHi : Palette.Field), r);
        var e = S(2);
        dl.AddRect(p + new Vector2(e / 2), p + size - new Vector2(e / 2), Palette.U32(Palette.StickerEdge), MathF.Max(0, r - e / 2), ImDrawFlags.None, e);
        var x = p.X + S(2 + 22);
        Icons.Draw(dl, icon, new Vector2(x, p.Y + (h - iconPx) / 2), iconPx, Palette.OnField, 2f);
        x += iconPx + S(8);
        Txt.Line(dl, new Vector2(x, p.Y), label, Face.SansExtraBold, px, Palette.OnField, 46);
        x += textW + S(8);
        Icons.Draw(dl, Icon.ChevronDown, new Vector2(x, p.Y + (h - chev) / 2), chev, Palette.OnField, 2f);
        Palette.Opacity = old;
        Kit.FocusRing(p, p + size, r);
        return clicked;
    }
}

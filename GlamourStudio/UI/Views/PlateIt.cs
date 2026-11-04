using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using GlamourStudio.Core;
using GlamourStudio.Game;
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
            Notify("Wait for Make room for me to finish first");
            return;
        }
        if (PlateEditor.IsOpen && PlateEditor.SelectedPlate != plate && PlateEditor.HasChanges)
        {
            Notify($"Plate {PlateEditor.SelectedPlate} has changes not saved: save them, or put it back, first");
            return;
        }
        if (!PlateItHere)
        {
            Notify("Open your glamour dresser first");
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
            Notify(why);
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
                // The dresser closed before Glamour Plates opened (a moment's grace: opening it may hide the dresser).
                if (job.Asked && !GameWindows.IsShown("MiragePrismPrismBox") && now > job.Until - TimeSpan.FromSeconds(4.5))
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

    private (int Sync, int Wardrobe, string LookId, bool Extra, string[] Lines)? plateLines;

    /// <summary>What each plate has on, for the menu: worked out again only when your looks, the wardrobe or the look change.</summary>
    private string PlateLineCached(int plate, Look look)
    {
        var extra = !config.ClearBare;
        if (plateLines is not { } c || c.Sync != sync.Version || c.Wardrobe != wardrobe.Version || c.LookId != look.Id || c.Extra != extra)
            plateLines = c = (sync.Version, wardrobe.Version, look.Id, extra, Enumerable.Range(1, Plates.Count).Select(n => PlateLine(n, look)).ToArray());
        return plate >= 1 && plate <= c.Lines.Length ? c.Lines[plate - 1] : PlateLine(plate, look);
    }

    /// <summary>What a plate has on, in a few words, for the menu.</summary>
    private string PlateLine(int plate, Look look)
    {
        var me = wardrobe.Current;
        var read = me?.PlatesReadAt != null;
        var pieces = me?.Plates.FirstOrDefault(p => p.Plate == plate)?.Pieces;
        string what;
        if (!read) what = string.Empty;
        else if (pieces == null || pieces.Count == 0) what = " · empty";
        else if (OnPlate(look.Pieces, pieces, !config.ClearBare)) what = " · this look";
        else
        {
            var on = PaneLooks().Concat(sync.Inbox.Select(s => s.Look)).FirstOrDefault(l => OnPlate(l.Pieces, pieces));
            what = on != null ? $" · {on.Name}" : $" · {Words.Count(pieces.Count, "piece")}";
        }
        var planned = sync.PlanFor(plate);
        var plan = planned != null && planned.Id != look.Id ? $" (planned: {planned.Name})" : planned != null ? " (planned for it)" : string.Empty;
        return $"Plate {plate}{what}{plan}";
    }

    /// <summary>
    /// The menu of plates for a look (opened by <see cref="OpenPlateItMenu"/>): the one planned for
    /// it first, then the empty ones, then the rest.
    /// </summary>
    private void DrawPlateItMenu(string id)
    {
        if (plateItMenu is not { } look) return;
        if (!Kit.BeginMenu(id))
        {
            return;
        }
        var me = wardrobe.Current;
        var empty = new HashSet<int>(Enumerable.Range(1, Plates.Count).Where(n => me?.PlatesReadAt != null && (me.Plates.FirstOrDefault(p => p.Plate == n)?.Pieces.Count ?? 0) == 0));
        var order = Enumerable.Range(1, Plates.Count)
            .OrderBy(n => sync.PlanFor(n)?.Id == look.Id ? 0 : empty.Contains(n) ? 1 : 2)
            .ThenBy(n => n);
        Txt.Note($"Put “{Txt.Fit(look.Name, Face.Sans, 12, S(220))}” on", Palette.Muted);
        foreach (var n in order)
        {
            var plate = n;
            if (Kit.MenuItem($"{id}-{plate}", PlateLineCached(plate, look), empty.Contains(plate) ? Icon.Layers : null, S(300)))
            {
                ImGui.CloseCurrentPopup();
                var l = look;
                Later(() => StartPlateIt(l, plate));
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
}

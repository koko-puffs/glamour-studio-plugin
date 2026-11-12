using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// The panels beside the game's windows: a small Glamour Studio panel docked to the edge of the
/// game window that's open (the dresser, Glamour Plates, the armoire, the fitting room, Examine…),
/// its top level with the window's, moving with it as it's dragged; on the side with room (or the
/// one picked in Settings). It folds to a slim tab on the window's edge, remembered for each window;
/// it's gone with its window, while the HUD's hidden, in cutscenes and in group pose. One at a time:
/// the game window you're working in (its focus) first, else the likeliest. It never opens the main
/// window by itself: its arrow button does.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>The game windows a panel sits beside, the likeliest first when two are open.</summary>
    private enum Pane
    {
        Plates,
        PlatesApply,
        OutfitSet,
        Dye,
        FittingRoom,
        Armoire,
        Dresser,
        Examine,
        Retainer,
        Market,
        Shop,
    }

    /// <summary>Each panel: its key (Settings, folding), its name in Settings, and the game windows it's beside.</summary>
    internal static readonly (string Key, string Name)[] CompanionList =
    {
        ("dresser", "Glamour Dresser"),
        ("plates", "Glamour Plates"),
        ("armoire", "Armoire"),
        ("outfit", "Storing outfit sets"),
        ("tryon", "Fitting Room"),
        ("examine", "Examine"),
        ("dye", "Dye"),
        ("plates-apply", "Plates from the Character window"),
        ("retainer", "Retainers' bags"),
        ("market", "Market board and shops"),
    };

    private static string KeyOf(Pane p) => p switch
    {
        Pane.Plates => "plates",
        Pane.PlatesApply => "plates-apply",
        Pane.OutfitSet => "outfit",
        Pane.Dye => "dye",
        Pane.FittingRoom => "tryon",
        Pane.Armoire => "armoire",
        Pane.Dresser => "dresser",
        Pane.Examine => "examine",
        Pane.Retainer => "retainer",
        _ => "market",
    };

    /// <summary>A look picked in a panel for the plate open (or at the dresser, for the plate you pick next).</summary>
    private Look? companionPick;

    /// <summary>The plate <see cref="companionPick"/> is for (0: the next one picked).</summary>
    private int companionPickPlate;

    /// <summary>
    /// The look picked, as the site has it now: the plans and your looks are new objects after each
    /// refresh, so it's found again by its id (the plan for the plate first), and an edit made on the
    /// site shows. The look as it was picked when it's in neither (one sent to you, say).
    /// </summary>
    private Look? PickedLook(Look? planned)
    {
        if (companionPick is not { } pick) return null;
        if (planned != null && planned.Id == pick.Id) return planned;
        return PaneLooks().FirstOrDefault(l => l.Id == pick.Id) ?? pick;
    }

    /// <summary>What the panels show, for a recording.</summary>
    internal string CompanionState() =>
        !config.Companions ? "panels off"
        : lastPane is { } p ? $"panel {KeyOf(p)}{(config.CompanionFolded.TryGetValue(KeyOf(p), out var f) && f ? " (folded)" : string.Empty)} at {lastRect.Pos.X:0},{lastRect.Pos.Y:0} {lastRect.Size.X:0}×{lastRect.Size.Y:0}"
                             + (companionPick != null ? $", picked “{companionPick.Name}” for plate {companionPickPlate}" : string.Empty)
                             + (p == Pane.Dye ? $", dye window piece {GameWindows.DyedItem(data)}" : string.Empty)
        : "no panel";

    /// <summary>The panel's content height last frame (it's as tall as that, up to its window's height).</summary>
    private float companionContentH;

    private Pane? lastPane;
    private WindowRect lastRect;
    private string? shopName;

    /// <summary>The game window's state the panel shows, read once a frame (only for the panel shown).</summary>
    private GameWindows.OutfitSetNow? paneOutfitSet;

    private uint paneDyed;
    private (ulong Id, string Name)? paneRetainer;

    /// <summary>The last failure of a panel, and when it was logged (one that keeps failing is logged once a minute, not every frame).</summary>
    private string? paneError;

    private DateTime paneErrorAt;

    /// <summary>Whether a panel's shown beside a game window (the site's looks are kept fresh meanwhile, as with the window open).</summary>
    internal bool CompanionShown => lastPane != null;

    /// <summary>Whether the plates panel is on (then the main window doesn't open with the plate editor).</summary>
    internal bool PlatesCompanionOn => config.Companions && !config.CompanionsOff.Contains("plates");

    /// <summary>Whether the panels should be away: the HUD hidden, a cutscene, group pose, logged out.</summary>
    private static bool CompanionsAway()
    {
        try
        {
            if (!Services.ClientState.IsLoggedIn || Services.GameGui.GameUiHidden || Services.ClientState.IsGPosing) return true;
            var c = Services.Condition;
            return c[ConditionFlag.OccupiedInCutSceneEvent] || c[ConditionFlag.WatchingCutscene] || c[ConditionFlag.WatchingCutscene78];
        }
        catch
        {
            return true;
        }
    }

    /// <summary>The panel to show and where its game window is; null when none is open (or each one open is off).</summary>
    private (Pane Pane, WindowRect Rect)? CompanionTarget()
    {
        var found = new List<(Pane, WindowRect, string)>();
        void Add(Pane p, WindowRect? r, string name)
        {
            if (r is { } rect && !config.CompanionsOff.Contains(KeyOf(p))) found.Add((p, rect, name));
        }
        var mode = PlateEditor.Mode;
        if (mode >= 0)
            Add(mode == 0 ? Pane.Plates : Pane.PlatesApply, GameWindows.RectNamed("MiragePrismMiragePlate"), "MiragePrismMiragePlate");
        Add(Pane.OutfitSet, GameWindows.RectNamed("MiragePrismPrismSetConvert"), "MiragePrismPrismSetConvert");
        Add(Pane.Dye, GameWindows.DyeRect(), "dye");
        Add(Pane.FittingRoom, GameWindows.RectNamed("Tryon"), "Tryon");
        Add(Pane.Armoire, GameWindows.RectNamed("Cabinet"), "Cabinet");
        Add(Pane.Dresser, GameWindows.RectNamed("MiragePrismPrismBox"), "MiragePrismPrismBox");
        Add(Pane.Examine, GameWindows.RectNamed("CharacterInspect"), "CharacterInspect");
        Add(Pane.Retainer, GameWindows.RectNamed("InventoryRetainerLarge") ?? GameWindows.RectNamed("InventoryRetainer"), "InventoryRetainer");
        Add(Pane.Market, GameWindows.RectNamed("ItemSearch") ?? GameWindows.RectNamed("ItemSearchResult"), "ItemSearch");
        if (!config.CompanionsOff.Contains("market") && GameWindows.ShopShown() is { } shop)
        {
            found.Add((Pane.Shop, shop.Rect, shop.Name));
            shopName = shop.Name;
        }

        // Trying a look on closes and opens the fitting room: its panel stays meanwhile.
        if (found.Count == 0 && lastPane == Pane.FittingRoom && FittingRoom.Busy && !config.CompanionsOff.Contains("tryon"))
            return (Pane.FittingRoom, lastRect);
        if (found.Count == 0) return null;
        // The window you're working in first.
        var focused = found.FirstOrDefault(f => f.Item2.Focused);
        var pick = focused.Item3 != null ? focused : found[0];
        return (pick.Item1, pick.Item2);
    }

    /// <summary>
    /// Draws the panel beside the game window that's open (after Dalamud's windows, every frame). A
    /// window of its own, not moved or resized by hand: it follows the game's.
    /// </summary>
    public void DrawCompanions()
    {
        // Store all goes on whatever's shown (and stops once the armoire closes).
        try
        {
            StoreNext();
        }
        catch (Exception ex)
        {
            storeQueue.Clear();
            storeRefused = default;
            Services.Log.Warning(ex, "Glamour Studio couldn't store a piece in the armoire");
        }
        try
        {
            TickPlateIt();
        }
        catch (Exception ex)
        {
            // Dropped, so it never throws every frame (and Plate it's buttons come back).
            plateIt = null;
            Services.Log.Warning(ex, "Glamour Studio couldn't put a look on a plate");
        }
        if (!config.Companions || CompanionsAway())
        {
            lastPane = null;
            return;
        }
        if (CompanionTarget() is not { } target)
        {
            // Glamour Plates closed: a look picked for its plate goes (one picked at the dresser waits for it to open).
            if (lastPane == Pane.Plates) companionPick = null;
            lastPane = null;
            return;
        }
        if (lastPane == Pane.Plates && target.Pane != Pane.Plates) companionPick = null;
        if (lastPane != target.Pane)
        {
            companionContentH = 0;
            companionMore = false;
        }
        // A pick made at the dresser stays for the plate editor; elsewhere it goes.
        if (target.Pane is not (Pane.Plates or Pane.Dresser)) companionPick = null;
        lastPane = target.Pane;
        lastRect = target.Rect;
        // What its title and body read of the game's window, once this frame.
        paneOutfitSet = target.Pane == Pane.OutfitSet ? GameWindows.OutfitSet(data) : null;
        paneDyed = target.Pane == Pane.Dye ? GameWindows.DyedItem(data) : 0;
        paneRetainer = target.Pane == Pane.Retainer ? GameWindows.RetainerOpen() : null;

        Kit.ResetFrame();
        Frame.ResetFrame();
        editorPlate = PlateEditor.SelectedPlate;
        editorChanges = editorPlate > 0 && PlateEditor.HasChanges;
        CheckCharacter();

        var key = KeyOf(target.Pane);
        var folded = config.CompanionFolded.TryGetValue(key, out var f) ? f : config.CompanionStartFolded;
        var rect = target.Rect;
        var vp = ImGui.GetMainViewport();
        var width = S(300);
        var gap = S(8);
        var right = config.CompanionSide switch
        {
            1 => true,
            2 => false,
            _ => rect.Max.X + gap + width <= vp.Pos.X + vp.Size.X || rect.Pos.X - gap - width < vp.Pos.X,
        };

        try
        {
            if (folded) DrawCompanionTab(target.Pane, key, rect, right);
            else DrawCompanionPanel(target.Pane, key, rect, right, width, gap, vp.Pos, vp.Size);
        }
        catch (Exception ex)
        {
            // A pane that keeps failing fails every frame: the same failure is logged once a minute.
            var what = ex.ToString();
            var now = DateTime.UtcNow;
            if (what != paneError || now - paneErrorAt > TimeSpan.FromMinutes(1))
            {
                paneError = what;
                paneErrorAt = now;
                Services.Log.Warning(ex, "Glamour Studio's panel beside the game window failed");
            }
        }
        RunLater();
    }

    private const ImGuiWindowFlags CompanionFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;

    /// <summary>The panel's header, its line included (the mock's <c>.pane .ph</c>), and the body's padding round what's in it.</summary>
    private const float PaneHeadCss = 54;

    private const float PanePadTopCss = 12, PanePadXCss = 14, PanePadBottomCss = 14;

    private void DrawCompanionPanel(Pane pane, string key, WindowRect rect, bool right, float width, float gap, Vector2 vpPos, Vector2 vpSize)
    {
        var headerH = S(PaneHeadCss);
        // As tall as what's in it, whatever the game window's height: it scrolls only when the screen
        // runs out (it was held to the game window's height, and a few pixels short of its content).
        var maxH = vpSize.Y - S(16);
        var bodyH = companionContentH > 0 ? companionContentH : S(240);
        var height = MathF.Min(headerH + bodyH, maxH);
        var x = right ? rect.Max.X + gap : rect.Pos.X - gap - width;
        x = Math.Clamp(x, vpPos.X, MathF.Max(vpPos.X, vpPos.X + vpSize.X - width));
        var y = Math.Clamp(rect.Pos.Y, vpPos.Y + S(8), MathF.Max(vpPos.Y + S(8), vpPos.Y + vpSize.Y - height - S(8)));

        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        Theme.PushFrame();
        // (The panel's corners are the mock's 14, the main window's 12.)
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, S(14));
        var shown = ImGui.Begin("##gs-companion", CompanionFlags);
        ImGui.PopStyleVar();
        Theme.Pop();
        try
        {
            if (!shown) return;
            Theme.PushInside();
            try
            {
                using (fonts.Push(Face.Sans, 13))
                {
                    try
                    {
                        var win = ImGui.GetWindowPos();
                        DrawCompanionHeader(pane, key, win, width, right);
                        var bodyTop = win.Y + headerH;
                        Kit.Dl.AddLine(new Vector2(win.X + 1, bodyTop - 0.5f), new Vector2(win.X + width - 1, bodyTop - 0.5f), Palette.U32(Palette.Line), 1f);
                        Kit.BeginPane("##gs-companion-body", new Vector2(win.X, bodyTop), new Vector2(width, height - headerH), PanePadXCss, PanePadTopCss, PanePadBottomCss);
                        var top = ImGui.GetCursorPosY();
                        paneLaid = false;
                        paneFirstInSec = true;
                        paneAfterRow = false;
                        DrawPaneBody(pane);
                        // What's in it, with the pane's padding above and below, and a pixel spare so a
                        // body that fits never shows a scrollbar.
                        var content = ImGui.GetCursorPosY() - top + S(PanePadTopCss) + S(PanePadBottomCss) + 1;
                        Kit.EndPane();
                        if (MathF.Abs(content - companionContentH) > 0.5f) companionContentH = content;
                    }
                    finally
                    {
                        // A pane that threw part way leaves its child and popups open: they're closed here, not by End.
                        Frame.Unwind();
                        Kit.ResetFrame();
                    }
                    if (!IsOpen) Frame.DrawToast();
                }
            }
            finally
            {
                Theme.Pop();
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    /// <summary>
    /// The panel's header (the mock's <c>.pane .ph</c>): the clothes tag (22), GLAMOUR STUDIO in small
    /// lilac capitals over what it's beside in Bricolage capitals (15), then Refresh, Open Glamour
    /// Studio and the fold, quiet and round (28), 9px apart.
    /// </summary>
    private void DrawCompanionHeader(Pane pane, string key, Vector2 win, float width, bool right)
    {
        // The header's 54 hold its line at the foot: what's in it sits in the 53 above.
        var inner = S(PaneHeadCss - 1);
        var btn = S(28);
        var gap = S(9);
        var by = win.Y + (inner - btn) / 2;
        var bx = win.X + width - S(10) - btn;
        if (Kit.IconButton("##c-fold", new Vector2(bx, by), right ? Icon.ChevronLeft : Icon.ChevronRight, "Fold it away (a tab stays on the window's edge)", 28, Btn.Quiet, false, 14))
            Later(() =>
            {
                config.CompanionFolded[key] = true;
                config.Save();
            });
        bx -= btn + gap;
        if (Kit.IconButton("##c-open", new Vector2(bx, by), Icon.ExternalLink, "Open Glamour Studio", 28, Btn.Quiet, false, 14))
            Later(() => OpenFromPane(pane));
        bx -= btn + gap;
        // Refresh as the bar's: dimmed while it reads, its arrows turning.
        var busy = sync.LibraryBusy;
        var dl = Kit.Dl;
        var turn = Kit.TurnStart(dl);
        if (Kit.IconButton("##c-refresh", new Vector2(bx, by), Icon.RefreshCw,
                busy ? "Reading…" : "Read it all again (your looks and list from the site, your wardrobe from the game)", 28, Btn.Quiet, busy, 14))
            Later(RefreshEverything);
        if (busy && !ReducedMotion)
            Kit.TurnSince(dl, turn, new Vector2(bx, by) + new Vector2(btn / 2), (float)(ImGui.GetTime() * 240 % 360));

        var logo = S(22);
        var lx = win.X + S(12);
        Kit.Logo(new Vector2(lx, win.Y + (inner - logo) / 2), 22);
        var tx = lx + logo + gap;
        var tw = MathF.Max(0, bx - gap - tx);
        // The eyebrow (9.5px, 0.1em apart, line 1.3) over the title (Bricolage 15, line 1.05), 2px between.
        var ebLine = S(10 * 1.3f);
        var tLine = S(15 * 1.05f);
        var ty = win.Y + (inner - ebLine - S(2) - tLine) / 2;
        var eb = Txt.SpacedFit("Glamour Studio", Face.SansSemibold, 10, 0.1f, tw);
        Txt.Spaced(dl, new Vector2(tx, ty + (ebLine - Txt.Natural(Face.SansSemibold, 10)) / 2), eb, Face.SansSemibold, 10, Palette.Accent, 0.1f);
        var titleTop = ty + ebLine + S(2);
        Kit.Display(new Vector2(tx, titleTop + (tLine - Kit.DisplayHeight(15)) / 2), PaneTitle(pane), 15, Palette.Ink, tw);
    }

    /// <summary>
    /// The slim tab a folded panel leaves on its window's edge (the mock's <c>.paneTab</c>): the arrow
    /// back out, its one number in lilac mono, the clothes tag under them.
    /// </summary>
    private void DrawCompanionTab(Pane pane, string key, WindowRect rect, bool right)
    {
        var w = S(34);
        var count = PaneCount(pane);
        var h = S(10 + 16) + (count != null ? S(8 + 11) : 0) + S(8 + 2 + 18 + 12);
        var vp = ImGui.GetMainViewport();
        var x = right ? rect.Max.X : rect.Pos.X - w;
        // On the screen, whichever side was asked (it's the only way back to the panel).
        if (x + w > vp.Pos.X + vp.Size.X) x = rect.Pos.X - w;
        if (x < vp.Pos.X) x = MathF.Min(rect.Max.X, vp.Pos.X + vp.Size.X - w);
        ImGui.SetNextWindowPos(new Vector2(x, Math.Clamp(rect.Pos.Y + S(20), vp.Pos.Y, MathF.Max(vp.Pos.Y, vp.Pos.Y + vp.Size.Y - h))), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
        Theme.PushFrame();
        var shown = ImGui.Begin("##gs-companion-tab", CompanionFlags);
        Theme.Pop();
        try
        {
            if (!shown) return;
            Theme.PushInside();
            try
            {
                var win = ImGui.GetWindowPos();
                var dl = Kit.Dl;
                var (clicked, hovered, _) = Kit.Hit("##c-unfold", win, new Vector2(w, h));
                if (hovered)
                {
                    dl.AddRectFilled(win, win + new Vector2(w, h), Palette.U32(Palette.RowHover), S(12));
                    Kit.Tooltip($"Glamour Studio: {PaneTitle(pane)}");
                }
                var cx = win.X + w / 2;
                var y = win.Y + S(10);
                Icons.Draw(dl, right ? Icon.ChevronRight : Icon.ChevronLeft, new Vector2(cx - S(8), y), S(16), Palette.Body);
                y += S(16 + 8);
                if (count != null)
                {
                    var t = Txt.Fit(count, Face.MonoMedium, 11, w - S(4));
                    var tw = Txt.Measure(t, Face.MonoMedium, 11).X;
                    Txt.Line(dl, new Vector2(cx - tw / 2, y), t, Face.MonoMedium, 11, Palette.Accent, 11);
                    y += S(11 + 8);
                }
                Kit.Logo(new Vector2(cx - S(9), y + S(2)), 18);
                if (clicked)
                    Later(() =>
                    {
                        config.CompanionFolded[key] = false;
                        config.Save();
                    });
            }
            finally
            {
                Theme.Pop();
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    /// <summary>What the panel's header says it's for.</summary>
    private string PaneTitle(Pane pane) => pane switch
    {
        Pane.Plates => editorPlate > 0 ? $"Plate {editorPlate}" : "Glamour Plates",
        Pane.PlatesApply => "Your plates, by look",
        Pane.OutfitSet => paneOutfitSet is { SetItem: > 0 } set ? data.ItemName(set.SetItem) : "Storing an outfit set",
        Pane.Dye => paneDyed is > 0 and var item ? data.ItemName(item) : "Dyeing",
        Pane.FittingRoom => "Trying on",
        Pane.Armoire => "At the armoire",
        Pane.Dresser => "At the dresser",
        Pane.Examine => examined is { Name.Length: > 0 } ex ? $"{ex.Name}'s outfit" : "Their outfit",
        Pane.Retainer => paneRetainer is { } r && r.Name.Length > 0 ? $"On {r.Name}" : "A retainer's bags",
        Pane.Market => "Wishlist",
        _ => "Wishlist",
    };

    /// <summary>The one number a folded tab shows, or null.</summary>
    private string? PaneCount(Pane pane)
    {
        try
        {
            var me = wardrobe.Current;
            return pane switch
            {
                Pane.Dresser when Linked => ReadyLooks(0, null).Count is > 0 and var n ? ReadyCount(n) : null,
                Pane.Plates when editorPlate > 0 => editorPlate.ToString(),
                Pane.Market or Pane.Shop when Linked => sync.Library.Wishes.Count is > 0 and var w ? w.ToString() : null,
                Pane.Retainer when me != null => RetainerForLooks().Count is > 0 and var r ? r.ToString() : null,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The arrow button: the plugin's window, on the screen that matches.</summary>
    private void OpenFromPane(Pane pane)
    {
        switch (pane)
        {
            case Pane.Plates when (PickedLook(sync.PlanFor(editorPlate)) ?? sync.PlanFor(editorPlate)) is { } look:
                OpenLook(look, null, Screen.Plates);
                break;
            case Pane.Plates or Pane.PlatesApply:
                Go(Screen.Plates);
                break;
            case Pane.Dresser:
                Go(Screen.MakeRoom);
                break;
            case Pane.Armoire or Pane.Retainer or Pane.Market or Pane.Shop or Pane.Dye:
                Go(Screen.Closet);
                break;
            case Pane.FittingRoom when FittingLook() is { } look:
                OpenLook(look, null, Screen.Looks);
                break;
            default:
                Go(Screen.Home);
                break;
        }
    }

    private void DrawPaneBody(Pane pane)
    {
        switch (pane)
        {
            case Pane.Plates:
                PanePlates();
                break;
            case Pane.PlatesApply:
                PanePlatesApply();
                break;
            case Pane.OutfitSet:
                PaneOutfitSet();
                break;
            case Pane.Dye:
                PaneDye();
                break;
            case Pane.FittingRoom:
                PaneFittingRoom();
                break;
            case Pane.Armoire:
                PaneArmoire();
                break;
            case Pane.Dresser:
                PaneDresser();
                break;
            case Pane.Examine:
                PaneExamine();
                break;
            case Pane.Retainer:
                PaneRetainer();
                break;
            case Pane.Market:
                PaneMarket();
                break;
            default:
                PaneShop();
                break;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // What the panels share (the mock's pane parts): sections and their gaps, a section's heading,
    // small words, a piece's row, a look's row, buttons, the cards for not linked and to see to.

    /// <summary>"Show more" pressed in this panel (a list cut short shows the rest).</summary>
    private bool companionMore;

    /// <summary>
    /// The body's flow, as the mock's grid lays it: sections 14 apart (<see cref="PaneSec"/>), what's in
    /// a section 6 apart (<see cref="PaneNext"/>), a hairline between piece rows one after another.
    /// </summary>
    private bool paneLaid, paneFirstInSec, paneAfterRow;

    private const float SecGapCss = 14, ItemGapCss = 6;

    /// <summary>Starts a section (or a card on its own): 14 under what's above.</summary>
    private void PaneSec()
    {
        if (paneLaid) Kit.Gap(SecGapCss);
        paneLaid = true;
        paneFirstInSec = true;
        paneAfterRow = false;
    }

    /// <summary>Before each thing in a section: 6 under the one before. True when a piece row follows another (its hairline).</summary>
    private bool PaneNext(bool row = false)
    {
        if (!paneFirstInSec) Kit.Gap(ItemGapCss);
        paneFirstInSec = false;
        paneLaid = true;
        var line = row && paneAfterRow;
        paneAfterRow = row;
        return line;
    }

    /// <summary>The pane's line height for its small words (13px/1.45 as the pane's own).</summary>
    private static float LineCss(int px) => px * 1.45f;

    private static readonly Run[] OneRun = new Run[1];

    /// <summary>The pane's small words (the mock's t11/t12), wrapped across the section, faint unless coloured.</summary>
    private void PaneText(string text, int px = 11, Vector4? color = null, bool center = false)
    {
        PaneNext();
        TextHere(text, px, color, center);
    }

    private static void TextHere(string text, int px = 11, Vector4? color = null, bool center = false)
    {
        OneRun[0] = new Run(text, Face.Sans, px, color ?? Palette.Faint);
        Txt.Para(OneRun, LineCss(px), center);
    }

    /// <summary>
    /// A section's heading (the mock's <c>.pane .sec-h</c>): Bricolage capitals at 13 (line 1.02), a
    /// faint mono count 8px after it on its baseline.
    /// </summary>
    private void SecHead(string title, string? count = null)
    {
        PaneNext();
        var at = Kit.At;
        var w = Kit.Width;
        var line = S(13 * 1.02f);
        var countW = count != null ? S(8) + Txt.Measure(count, Face.Mono, 11).X : 0;
        var top = at.Y + (line - Kit.DisplayHeight(13)) / 2;
        var tw = Kit.Display(new Vector2(at.X, top), title, 13, Palette.Ink, MathF.Max(S(40), w - countW));
        if (count != null)
            Txt.Draw(Kit.Dl, new Vector2(at.X + tw + S(8), top + Txt.Ascent(Face.Display, 13) - Txt.Ascent(Face.Mono, 11)), count, Face.Mono, 11, Palette.Faint);
        Kit.Reserve(at, w, line);
    }

    /// <summary>A line with an icon before it (the mock's <c>.row.g6.t12</c>: a droplet, a tick): 14px icon, 6px, 12px words wrapped.</summary>
    private void IconLine(Icon icon, Vector4 iconColor, string text, Vector4? textColor = null)
    {
        PaneNext();
        var at = Kit.At;
        var w = Kit.Width;
        var line = S(LineCss(12));
        Icons.Draw(Kit.Dl, icon, new Vector2(at.X, at.Y + (line - S(14)) / 2), S(14), iconColor);
        OneRun[0] = new Run(text, Face.Sans, 12, textColor ?? Palette.Body);
        var h = Txt.Flow(new Vector2(at.X + S(20), at.Y), w - S(20), OneRun, LineCss(12), out _);
        Kit.Reserve(at, w, MathF.Max(line, h));
    }

    // Buttons, at the mock's sizes: xs 28 (pad 10, 12px, a 12px icon 5 before), sm 34 (13, 13px, 14 icon 7 before).

    private static float BtnPadCss(BtnSize size) => size == BtnSize.Tiny ? 10 : 13;

    private static int BtnPx(BtnSize size) => size == BtnSize.Tiny ? 12 : 13;

    private static float BtnIconCss(BtnSize size) => size == BtnSize.Tiny ? 12 : 14;

    private static float BtnGapCss(BtnSize size) => size == BtnSize.Tiny ? 5 : 7;

    private static float BtnW(string label, BtnSize size, Icon? icon = null) =>
        S(BtnPadCss(size)) * 2 + Txt.Measure(label, Face.SansSemibold, BtnPx(size)).X + (icon != null ? S(BtnIconCss(size) + BtnGapCss(size)) : 0);

    private static bool BtnAt(string id, Vector2 pos, string label, BtnSize size = BtnSize.Tiny, Btn style = Btn.Glass, Icon? icon = null, bool disabled = false,
        string? tip = null, float width = 0) =>
        Kit.Button(id, pos, label, style, size, icon, width > 0 ? width : BtnW(label, size, icon), disabled, tip, 0, BtnIconCss(size));

    /// <summary>A button on its own at the section's left (the mock's <c>justify-self: start</c>), xs unless asked.</summary>
    private bool PaneButton(string id, string label, Icon? icon = null, BtnSize size = BtnSize.Tiny, Btn style = Btn.Glass, bool disabled = false, string? tip = null)
    {
        PaneNext();
        return ButtonHere(id, label, icon, size, style, disabled, tip);
    }

    private static bool ButtonHere(string id, string label, Icon? icon = null, BtnSize size = BtnSize.Tiny, Btn style = Btn.Glass, bool disabled = false, string? tip = null)
    {
        var at = Kit.At;
        var w = MathF.Min(BtnW(label, size, icon), Kit.Width);
        var pressed = BtnAt(id, at, label, size, style, icon, disabled, tip, w);
        Kit.Reserve(at, Kit.Width, Kit.ButtonHeight(size));
        return pressed;
    }

    /// <summary>
    /// A lilac link in the pane's words (600, 12px; ink when pointed at), a 12px chevron after it if
    /// asked. Returns whether it's pressed; <paramref name="width"/> what it took.
    /// </summary>
    private static bool PaneLink(string id, Vector2 pos, string text, out float width, Icon? after = null, bool disabled = false, string? tip = null)
    {
        var tw = Txt.Measure(text, Face.SansSemibold, 12).X;
        var lh = Txt.Natural(Face.SansSemibold, 12);
        width = tw + (after != null ? S(4 + 12) : 0);
        var (clicked, hovered, _) = Kit.Hit(id, pos, new Vector2(width, lh), disabled);
        if (tip != null && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + new Vector2(width, lh))) Kit.Tooltip(tip);
        var col = disabled ? Palette.Faint : hovered ? Palette.Ink : Palette.Accent;
        Txt.Draw(Kit.Dl, pos, text, Face.SansSemibold, 12, col);
        if (after is { } icon) Icons.Draw(Kit.Dl, icon, new Vector2(pos.X + tw + S(4), pos.Y + (lh - S(12)) / 2), S(12), col);
        return clicked;
    }

    /// <summary>A link on its own row ("and 2 more", "Which ones?").</summary>
    private bool PaneLinkRow(string id, string text, Icon? after = null)
    {
        PaneNext();
        var at = Kit.At;
        var pressed = PaneLink(id, at, Txt.Fit(text, Face.SansSemibold, 12, Kit.Width), out _, after);
        Kit.Reserve(at, Kit.Width, Txt.Natural(Face.SansSemibold, 12));
        return pressed;
    }

    /// <summary>A tag cut to <paramref name="maxWidth"/> (its words with "…"), drawn at the right of a row. Returns its width.</summary>
    private static float TagAtRight(float right, float rowTop, float rowH, string text, TagKind kind, Icon? icon, TagSize size, float maxWidth)
    {
        var w = Kit.TagWidth(text, icon, size);
        if (w > maxWidth)
        {
            var px = size == TagSize.Sm ? 10 : size == TagSize.Lg ? 12 : 11;
            text = Txt.Fit(text, Face.SansSemibold, px, MathF.Max(S(10), maxWidth - (w - Txt.Measure(text, Face.SansSemibold, px).X)));
            w = Kit.TagWidth(text, icon, size);
        }
        var th = Kit.TagHeight(size);
        Kit.Tag(new Vector2(right - w, rowTop + (rowH - th) / 2), text, kind, icon, size);
        return w;
    }

    /// <summary>A tag's words, kind and icon (where a piece is, what a slot does).</summary>
    private readonly record struct TagSpec(string Text, TagKind Kind, Icon? Icon = null);

    /// <summary>
    /// A piece's row (the mock's <c>.pane .prow</c>): its tile (32, or 28) with the corner dot, its name
    /// (600, 12.5) and a muted line under it, and at the right a tag or an xs button. 46 tall at
    /// least, a hairline over it when it follows another. True when the button's pressed.
    /// </summary>
    private bool PieceRow(string id, uint item, string? name = null, string? line = null, TagSpec? tag = null, string? button = null,
        bool buttonDisabled = false, string? tip = null, float tileCss = 32, Own own = Own.None, bool ghost = false, bool picked = false,
        Btn buttonStyle = Btn.Glass, Icon? buttonIcon = null)
    {
        var divider = PaneNext(row: true);
        var at = Kit.At;
        var w = Kit.Width;
        var tile = S(tileCss);
        var nameLine = S(13 * 1.25f);
        var subLine = S(12 * 1.35f);
        var textH = nameLine + (line != null ? subLine : 0);
        var h = MathF.Max(S(46), MathF.Max(tile, textH) + S(8));
        var dl = Kit.Dl;
        if (picked) dl.AddRectFilled(new Vector2(at.X - S(6), at.Y), new Vector2(at.X + w + S(6), at.Y + h), Palette.U32(Palette.AccentWash), S(10));
        if (divider) Kit.Divider(at.X, at.X + w, at.Y);
        Kit.Item(new Vector2(at.X, at.Y + (h - tile) / 2), tileCss, item != 0 ? data.Item(item)?.Icon ?? 0 : 0, own, ghost, Palette.Ground);
        var x = at.X + tile + S(9);
        var right = at.X + w;
        var clicked = false;
        if (button != null)
        {
            var bw = BtnW(button, BtnSize.Tiny, buttonIcon);
            var bh = Kit.ButtonHeight(BtnSize.Tiny);
            clicked = BtnAt(id, new Vector2(right - bw, at.Y + (h - bh) / 2), button, BtnSize.Tiny, buttonStyle, buttonIcon, buttonDisabled, tip, bw);
            right -= bw + S(9);
        }
        else if (tag is { } t)
            right -= TagAtRight(right, at.Y, h, t.Text, t.Kind, t.Icon, TagSize.Md, (w - tile) * 0.5f) + S(9);
        var tw = MathF.Max(S(10), right - x);
        var nameText = name ?? (item != 0 ? data.ItemName(item) : "Nothing");
        var ty = at.Y + (h - textH) / 2;
        Txt.Line(dl, new Vector2(x, ty), Txt.Fit(nameText, Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink, 13 * 1.25f);
        if (line != null) Txt.Line(dl, new Vector2(x, ty + nameLine), Txt.Fit(line, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted, 12 * 1.35f);
        if (button == null && tip != null && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(at, at + new Vector2(w, h)))
            Kit.Tooltip(tip);
        Kit.Reserve(at, w, h);
        return clicked;
    }

    /// <summary>The first pieces of a look as tiles side by side, in the slots' order. Returns the width taken.</summary>
    private float LookIcons(Vector2 pos, IReadOnlyDictionary<Slot, Piece> pieces, int most, float sizeCss, float gapCss = 4)
    {
        var x = pos.X;
        var n = 0;
        foreach (var slot in Slots.All)
        {
            if (n >= most) break;
            if (!pieces.TryGetValue(slot, out var p)) continue;
            Kit.Item(new Vector2(x, pos.Y), sizeCss, data.Item(p.Item)?.Icon ?? 0);
            x += S(sizeCss + gapCss);
            n++;
        }
        return n == 0 ? 0 : x - pos.X - S(gapCss);
    }

    /// <summary>
    /// A look's row (the mock's <c>.pane .lrow</c>): 54 tall, its fill reaching 6px past the section
    /// (lilac when it's the one picked, faint when pointed at), two of its pieces (26), its name in
    /// Bricolage capitals (12.5), a muted line (with the dyes' swatches before it, if given), and an
    /// xs button at the right. True when the button's pressed.
    /// </summary>
    private bool LookRow(string id, Look look, string? line, string? button = null, bool disabled = false, string? tip = null, bool on = false,
        IReadOnlyList<uint?>? swatches = null)
    {
        PaneNext();
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(54);
        var dl = Kit.Dl;
        var min = new Vector2(at.X - S(6), at.Y);
        var max = new Vector2(at.X + w + S(6), at.Y + h);
        var hovered = !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(min, max);
        if (on)
        {
            dl.AddRectFilled(min, max, Palette.U32(Palette.AccentWash), S(14));
            dl.AddRect(min + new Vector2(0.5f), max - new Vector2(0.5f), Palette.U32(Palette.AccentLine), S(14), ImDrawFlags.None, 1f);
        }
        else if (hovered) dl.AddRectFilled(min, max, Palette.U32(Palette.RowHover), S(14));
        LookIcons(new Vector2(at.X, at.Y + (h - S(26)) / 2), look.Pieces, 2, 26);
        var x = at.X + S(26 * 2 + 4 + 9);
        var right = at.X + w;
        var clicked = false;
        if (button != null)
        {
            var bw = BtnW(button, BtnSize.Tiny);
            var bh = Kit.ButtonHeight(BtnSize.Tiny);
            clicked = BtnAt(id, new Vector2(right - bw, at.Y + (h - bh) / 2), button, BtnSize.Tiny, Btn.Glass, null, disabled, tip, bw);
            right -= bw + S(9);
        }
        var tw = MathF.Max(S(10), right - x);
        var nameLine = S(13 * 1.1f);
        var subLine = line != null ? S(12 * 1.35f) : 0;
        var ty = at.Y + (h - nameLine - subLine) / 2;
        Kit.Display(new Vector2(x, ty + (nameLine - Kit.DisplayHeight(13)) / 2), look.Name, 13, Palette.Ink, tw);
        if (line != null)
        {
            var lx = x;
            var ly = ty + nameLine;
            if (swatches != null)
                foreach (var rgb in swatches)
                {
                    Kit.Swatch(dl, new Vector2(lx + S(6), ly + subLine / 2), rgb, 12);
                    lx += S(12 + 4);
                }
            Txt.Line(dl, new Vector2(lx, ly), Txt.Fit(line, Face.Sans, 12, MathF.Max(S(10), right - lx)), Face.Sans, 12, Palette.Muted, 12 * 1.35f);
        }
        Kit.Reserve(at, w, h);
        return clicked;
    }

    /// <summary>
    /// What a panel shows in place of what needs your looks when you aren't linked (the mock's
    /// <c>.card.dash</c>): a dashed card with what linking brings, and Link your account (Settings → Account).
    /// </summary>
    private void NotLinked(string text)
    {
        PaneSec();
        PaneNext();
        var at = Kit.At;
        var w = Kit.Width;
        Kit.BeginPanel("##c-not-linked", 14, 16, 14, Vector4.Zero, Vector4.Zero, 18);
        TextHere(text, 12, Palette.Muted);
        Kit.Gap(6);
        if (ButtonHere("##c-link-account", "Link your account"))
            Later(() =>
            {
                OpenSettings(SettingsTab.Account);
                IsOpen = true;
            });
        Kit.EndPanel();
        Kit.Dashed(at, new Vector2(at.X + w, ImGui.GetCursorScreenPos().Y), S(18), Palette.LineStrong, 1f);
    }

    /// <summary>Something to see to first (the mock's <c>.card.warn</c>): the amber edge, ⚠ and the words.</summary>
    private void WarnCard(string text)
    {
        PaneSec();
        PaneNext();
        Kit.BeginPanel("##c-warn", 14, 16, 14, Palette.Card, Palette.WithAlpha(Palette.Amber, 0.4f), 18);
        var at = Kit.At;
        var w = Kit.Width;
        var line = S(LineCss(12));
        Icons.Draw(Kit.Dl, Icon.AlertTriangle, new Vector2(at.X, at.Y + (line - S(16)) / 2), S(16), Palette.Amber);
        OneRun[0] = new Run(text, Face.Sans, 12, Palette.Body);
        var h = Txt.Flow(new Vector2(at.X + S(24), at.Y), w - S(24), OneRun, LineCss(12), out _);
        Kit.Reserve(at, w, MathF.Max(S(16), h));
        Kit.EndPanel();
    }

    /// <summary>A slot's key ("ringR") as the few words a slot's row starts with ("Ring R").</summary>
    private static string SlotShort(string key) => key switch
    {
        "ringR" => "Ring R",
        "ringL" => "Ring L",
        _ when key.Length > 0 => char.ToUpperInvariant(key[0]) + key[1..],
        _ => key,
    };

    /// <summary>
    /// Where a piece is, as its tag and corner dot in the honest colours: where a plate takes it from
    /// in ice, elsewhere of yours in amber (with an icon in the dresser's list), on your wishlist in
    /// lavender, not yours in pink.
    /// </summary>
    private (TagSpec Tag, Own Own) WhereOf(uint item, bool icons = false)
    {
        return Where(item) switch
        {
            Place.Dresser => (new TagSpec("Dresser", TagKind.Ok), Own.Ice),
            Place.Armoire => (new TagSpec("Armoire", TagKind.Ok), Own.Ice),
            Place.Inventory => (new TagSpec("Bags", TagKind.Amber, icons ? Icon.Bag : null), Own.Amber),
            Place.Armoury => (new TagSpec("Armoury", TagKind.Amber, icons ? Icon.Shirt : null), Own.Amber),
            Place.Saddlebag => (new TagSpec("Saddlebag", TagKind.Amber, icons ? Icon.Bag : null), Own.Amber),
            Place.Retainer => (new TagSpec("Retainer", TagKind.Amber, icons ? Icon.Users : null), Own.Amber),
            _ => sync.Library.Wishes.Contains(LookJson.BaseId(item))
                ? (new TagSpec("Wishlist", TagKind.Star, Icon.Star), Own.Star)
                : (new TagSpec("Don't have", TagKind.Miss), Own.Pink),
        };
    }

    /// <summary>Wishes being sent (so a second press waits for the first).</summary>
    private bool wishing;

    /// <summary>An xs button with a star that puts pieces on your wishlist (shown when linked and there's any to put).</summary>
    private void WishButton(string id, IReadOnlyList<Piece> pieces, string text)
    {
        if (!Linked || pieces.Count == 0) return;
        if (PaneButton(id, wishing ? "Putting them on the list…" : text, Icon.Star, disabled: wishing))
        {
            var list = pieces.ToList();
            Later(() => AddWishes(list));
        }
    }

    /// <summary>How something went: in chat when asked from the right-click menu, else a toast (a warning when it didn't).</summary>
    private static void Say(string text, bool ok, Action<string>? say)
    {
        if (say != null) say(text);
        else if (ok) Notify(text);
        else Frame.Warn(text);
    }

    /// <param name="say">Where to say how it went (a toast, else; a line in chat from the right-click menu).</param>
    private void AddWishes(IReadOnlyList<Piece> pieces, Action<string>? say = null)
    {
        wishing = true;
        sync.AddWishes(pieces, r =>
        {
            wishing = false;
            recorder.Note($"WISH added {pieces.Count}: {(r.Ok ? $"{r.Value} new" : r.Why)}");
            Say(!r.Ok ? r.Why ?? "The site said no"
                : pieces.Count == 1 ? $"{data.ItemName(pieces[0].Item)}: on your wishlist"
                : $"{Words.Count(pieces.Count, "piece")} on your wishlist", r.Ok, say);
        });
    }

    /// <summary>Takes a piece off your wishlist (at once here; back on should the site say no).</summary>
    private void Unwish(uint item, Action<string>? say = null)
    {
        sync.RemoveWishes(new[] { item }, r =>
        {
            recorder.Note($"WISH removed {item}: {(r.Ok ? $"{r.Value} off" : r.Why)}");
            Say(r.Ok ? $"{data.ItemName(item)}: off your wishlist" : r.Why ?? "The site said no", r.Ok, say);
        });
    }

    /// <summary>The looks the panels look through: yours, then the ones you love.</summary>
    private IEnumerable<Look> PaneLooks() => sync.Library.Mine.Concat(sync.Library.Loved);

    /// <summary>The plate a look is on now (its pieces on it as they are), or 0. One that's the look exactly first.</summary>
    private int PlateWith(Look look)
    {
        var me = wardrobe.Current;
        if (me == null) return 0;
        foreach (var p in me.Plates)
            if (p.Pieces.Count > 0 && OnPlate(look.Pieces, p.Pieces))
                return p.Plate;
        // With the slots a look leaves bare kept (Settings), a plate filled with it keeps what it had there.
        if (!config.ClearBare)
            foreach (var p in me.Plates)
                if (p.Pieces.Count > 0 && OnPlate(look.Pieces, p.Pieces, true))
                    return p.Plate;
        return 0;
    }

    /// <summary>
    /// A look on a plate: each of its pieces there with its dyes, and nothing else but a weapon it
    /// doesn't have (a plate keeps the weapon a look leaves out). With <paramref name="allowExtra"/>,
    /// anything else too: filling a plate keeps what the look leaves bare when Settings says so.
    /// </summary>
    private static bool OnPlate(IReadOnlyDictionary<Slot, Piece> look, IReadOnlyDictionary<Slot, Piece> plate, bool allowExtra = false)
    {
        if (look.Count == 0) return false;
        foreach (var (slot, want) in look)
        {
            if (!plate.TryGetValue(slot, out var has) || LookJson.BaseId(has.Item) != LookJson.BaseId(want.Item) || has.Dye1 != want.Dye1 || has.Dye2 != want.Dye2)
                return false;
        }
        return allowExtra || plate.Keys.All(s => look.ContainsKey(s) || Slots.IsWeapon(s));
    }

    /// <summary>A look's pieces as the bottles and pieces to put on a list: the missing ones.</summary>
    private List<Piece> MissingOf(IReadOnlyDictionary<Slot, Piece> pieces) =>
        pieces.Values.Where(p => p.Item != 0 && Where(p.Item) == null && !sync.Library.Wishes.Contains(LookJson.BaseId(p.Item))).ToList();
}

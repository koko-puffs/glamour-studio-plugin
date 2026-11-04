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

    /// <summary>Whether the plates panel is on (then the main window doesn't open with the plate editor).</summary>
    /// <summary>Whether a panel's shown beside a game window (the site's looks are kept fresh meanwhile, as with the window open).</summary>
    internal bool CompanionShown => lastPane != null;

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

    private void DrawCompanionPanel(Pane pane, string key, WindowRect rect, bool right, float width, float gap, Vector2 vpPos, Vector2 vpSize)
    {
        var headerH = S(56);
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
        var shown = ImGui.Begin("##gs-companion", CompanionFlags);
        Theme.Pop();
        try
        {
            if (!shown) return;
            Theme.PushInside();
            try
            {
                using (fonts.Push(Face.Sans, 14))
                {
                    try
                    {
                        var win = ImGui.GetWindowPos();
                        DrawCompanionHeader(pane, key, win, width, headerH, right);
                        var bodyTop = win.Y + headerH;
                        Kit.Dl.AddLine(new Vector2(win.X + 1, bodyTop - 0.5f), new Vector2(win.X + width - 1, bodyTop - 0.5f), Palette.U32(Palette.Divider), 1f);
                        Kit.BeginPane("##gs-companion-body", new Vector2(win.X, bodyTop), new Vector2(width, height - headerH), 14, 12, 14);
                        var top = ImGui.GetCursorPosY();
                        DrawPaneBody(pane);
                        // What's in it, with the pane's padding above (12) and below (14), and a pixel
                        // spare so a body that fits never shows a scrollbar.
                        var content = ImGui.GetCursorPosY() - top + S(12) + S(14) + 1;
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

    /// <summary>The panel's header: Glamour Studio over what it's beside, open the plugin, fold it away.</summary>
    private void DrawCompanionHeader(Pane pane, string key, Vector2 win, float width, float headerH, bool right)
    {
        var dl = Kit.Dl;
        var pad = S(14);

        var btn = S(28);
        var bx = win.X + width - S(12) - btn;
        var by = win.Y + (headerH - btn) / 2;
        if (Kit.IconButton("##c-fold", new Vector2(bx, by), right ? Icon.ChevronLeft : Icon.ChevronRight, "Fold it away (a tab stays on the window's edge)", 28, Btn.Glass, false, 13))
            Later(() =>
            {
                config.CompanionFolded[key] = true;
                config.Save();
            });
        bx -= btn + S(6);
        if (Kit.IconButton("##c-open", new Vector2(bx, by), Icon.ExternalLink, "Open Glamour Studio", 28, Btn.Glass, false, 13))
            Later(() => OpenFromPane(pane));
        bx -= btn + S(6);
        if (Kit.IconButton("##c-refresh", new Vector2(bx, by), Icon.Refresh, "Read it all again (your looks and list from the site, your wardrobe from the game)", 28, Btn.Glass, sync.LibraryBusy, 13))
            Later(RefreshEverything);

        var tx = win.X + pad;
        var tw = bx - S(8) - tx;
        var ebH = Txt.Natural(Face.Mono, 10);
        var tH = Txt.Natural(Face.SansSemibold, 15);
        var ty = win.Y + (headerH - ebH - tH) / 2;
        Txt.Spaced(dl, new Vector2(tx, ty), "GLAMOUR STUDIO", Face.Mono, 10, Palette.Rose, 0.16f);
        Txt.Draw(dl, new Vector2(tx, ty + ebH), Txt.Fit(PaneTitle(pane), Face.SansSemibold, 15, tw), Face.SansSemibold, 15, Palette.Ink);
    }

    /// <summary>The slim tab a folded panel leaves on its window's edge: an arrow, its one number.</summary>
    private void DrawCompanionTab(Pane pane, string key, WindowRect rect, bool right)
    {
        var w = S(34);
        var count = PaneCount(pane);
        var h = S(12) + S(16) + (count != null ? S(8) + Txt.Natural(Face.Mono, 11) : 0) + S(12);
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
                    dl.AddRectFilled(win, win + new Vector2(w, h), Palette.U32(Palette.RowHover), S(14));
                    Kit.Tooltip($"Glamour Studio: {PaneTitle(pane)}");
                }
                var cx = win.X + w / 2;
                var iy = win.Y + S(12);
                Icons.Draw(dl, right ? Icon.ChevronRight : Icon.ChevronLeft, new Vector2(cx - S(8), iy), S(16), Palette.Soft);
                if (count != null)
                {
                    var t = Txt.Fit(count, Face.Mono, 11, w - S(4));
                    var tw = Txt.Measure(t, Face.Mono, 11).X;
                    Txt.Draw(dl, new Vector2(cx - tw / 2, iy + S(16) + S(8)), t, Face.Mono, 11, Palette.RoseText);
                }
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
        Pane.Market => "Shopping list",
        _ => "Shopping list",
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
    // What the panels share: a piece's row, a look's row, a heading, a note, the shopping list.

    /// <summary>"Show more" pressed in this panel (a list cut short shows the rest).</summary>
    private bool companionMore;

    private static void PaneHeading(string text, string? sub = null)
    {
        Kit.Heading(text, sub, 13);
        Kit.Gap(8);
    }

    private static void PaneNote(string text, Vector4? color = null)
    {
        Txt.Para(new[] { new Run(text, Face.Sans, 12, color ?? Palette.Muted) }, 18);
    }

    /// <summary>
    /// A piece's row: its icon, a slot's mono label (if any), its name and a line under it, a chip on
    /// the right, or a small button there instead. True when the button's pressed.
    /// </summary>
    private bool PieceRow(string id, uint item, string? slotKey, string? name, string? line, string? chip, ChipKind kind, string? button = null,
        bool buttonDisabled = false, string? tip = null, bool picked = false)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = line != null ? S(40) : S(32);
        var dl = Kit.Dl;
        if (picked) dl.AddRectFilled(at, at + new Vector2(w, h), Palette.U32(Palette.RoseRow), S(8));
        var x = at.X + S(4);
        if (slotKey != null)
        {
            Txt.Spaced(dl, new Vector2(x, at.Y + (h - Txt.Natural(Face.Mono, 10)) / 2), slotKey.ToUpperInvariant(), Face.Mono, 10, Palette.Dim, 0.06f);
            x += S(40);
        }
        var icon = S(24);
        if (item != 0) Kit.GameIcon(new Vector2(x, at.Y + (h - icon) / 2), icon, data.Item(item)?.Icon ?? 0, 6);
        else Kit.EmptyTile(new Vector2(x, at.Y + (h - icon) / 2), icon, 6, 0.6f);
        x += icon + S(8);
        var rightW = 0f;
        var clicked = false;
        if (button != null)
        {
            var bw = Kit.ButtonWidth(button, BtnSize.Tiny);
            var bh = Kit.ButtonHeight(BtnSize.Tiny);
            clicked = Kit.Button(id, new Vector2(at.X + w - bw, at.Y + (h - bh) / 2), button, Btn.Glass, BtnSize.Tiny, null, 0, buttonDisabled, tip);
            rightW = bw + S(8);
        }
        else if (chip != null)
        {
            var cw = MathF.Min(Kit.ChipW(chip, false, 18), w * 0.45f);
            Kit.Chip(new Vector2(at.X + w - cw, at.Y + (h - S(18)) / 2), Txt.Fit(chip, Face.Sans, 10, cw - S(12)), kind, false, 18, cw);
            rightW = cw + S(8);
        }
        var tw = at.X + w - rightW - x;
        var nameText = name ?? (item != 0 ? data.ItemName(item) : "Nothing");
        var nh = Txt.Natural(Face.Sans, 12);
        if (line != null)
        {
            var lh = Txt.Natural(Face.Sans, 11);
            var ty = at.Y + (h - nh - lh) / 2;
            Txt.Draw(dl, new Vector2(x, ty), Txt.Fit(nameText, Face.Sans, 12, tw), Face.Sans, 12, item != 0 ? Palette.Ink : Palette.Dim);
            Txt.Draw(dl, new Vector2(x, ty + nh), Txt.Fit(line, Face.Sans, 11, tw), Face.Sans, 11, Palette.Muted);
        }
        else Txt.Draw(dl, new Vector2(x, at.Y + (h - nh) / 2), Txt.Fit(nameText, Face.Sans, 12, tw), Face.Sans, 12, item != 0 ? Palette.Ink : Palette.Dim);
        if (button == null && tip != null && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(at, at + new Vector2(w, h)))
            Kit.Tooltip(tip);
        Kit.Reserve(at, w, h);
        return clicked;
    }

    /// <summary>A look's row: its mosaic, its name, a line or a chip under it, a small button on the right. True when the button's pressed.</summary>
    private bool LookPaneRow(string id, Look look, string? line, string? chip, ChipKind kind, string? button, bool disabled = false, string? tip = null, bool picked = false)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(48);
        var dl = Kit.Dl;
        if (picked) dl.AddRectFilled(at, at + new Vector2(w, h), Palette.U32(Palette.RoseRow), S(8));
        var m = S(36);
        Mosaic(new Vector2(at.X + S(4), at.Y + (h - m) / 2), m, look.Pieces, 8, 4);
        var x = at.X + S(4) + m + S(10);
        var clicked = false;
        var rightW = 0f;
        if (button != null)
        {
            var bw = Kit.ButtonWidth(button, BtnSize.Tiny);
            var bh = Kit.ButtonHeight(BtnSize.Tiny);
            clicked = Kit.Button(id, new Vector2(at.X + w - bw, at.Y + (h - bh) / 2), button, Btn.Glass, BtnSize.Tiny, null, 0, disabled, tip);
            rightW = bw + S(8);
        }
        var tw = at.X + w - rightW - x;
        var nh = Txt.Natural(Face.SansMedium, 13);
        var subH = chip != null ? S(18) : line != null ? Txt.Natural(Face.Sans, 11) : 0;
        var ty = at.Y + (h - nh - (subH > 0 ? S(3) + subH : 0)) / 2;
        Txt.Draw(dl, new Vector2(x, ty), Txt.Fit(look.Name, Face.SansMedium, 13, tw), Face.SansMedium, 13, Palette.Ink);
        if (chip != null) Kit.Chip(new Vector2(x, ty + nh + S(3)), Txt.Fit(chip, Face.Sans, 10, tw - S(12)), kind, false, 18, MathF.Min(Kit.ChipW(chip, false, 18), tw));
        else if (line != null) Txt.Draw(dl, new Vector2(x, ty + nh + S(3)), Txt.Fit(line, Face.Sans, 11, tw), Face.Sans, 11, Palette.Muted);
        Kit.Reserve(at, w, h);
        return clicked;
    }

    /// <summary>A piece's whereabouts as a chip: where a plate takes it from in green, elsewhere outlined, not yours in rose.</summary>
    private (string Text, ChipKind Kind) WhereChipOf(uint item)
    {
        var w = Where(item);
        return w switch
        {
            Place.Dresser => ("Dresser", ChipKind.Ok),
            Place.Armoire => ("Armoire", ChipKind.Ok),
            Place.Inventory => ("Bags", ChipKind.Plain),
            Place.Armoury => ("Armoury", ChipKind.Plain),
            Place.Saddlebag => ("Saddlebag", ChipKind.Plain),
            Place.Retainer => ("Retainer", ChipKind.Plain),
            _ => sync.Library.Wishes.Contains(LookJson.BaseId(item)) ? ("On your list", ChipKind.Plain) : ("Don't have", ChipKind.Rose),
        };
    }

    /// <summary>Wishes being sent (so a second press waits for the first).</summary>
    private bool wishing;

    /// <summary>A rose link that puts pieces on your shopping list (shown when linked and there's any to put).</summary>
    private void WishLink(string id, IReadOnlyList<Piece> pieces, string text)
    {
        if (!Linked || pieces.Count == 0) return;
        var at = Kit.At;
        var label = wishing ? "Putting them on the list…" : text;
        if (Kit.Link(id, at, Txt.Fit(label, Face.Sans, 12, Kit.Width), 12) && !wishing)
        {
            var list = pieces.ToList();
            Later(() => AddWishes(list));
        }
        Kit.Reserve(at, Kit.Width, Txt.Natural(Face.Sans, 12));
    }

    /// <param name="say">Where to say how it went (a toast, else; a line in chat from the right-click menu).</param>
    private void AddWishes(IReadOnlyList<Piece> pieces, Action<string>? say = null)
    {
        wishing = true;
        sync.AddWishes(pieces, r =>
        {
            wishing = false;
            recorder.Note($"WISH added {pieces.Count}: {(r.Ok ? $"{r.Value} new" : r.Why)}");
            (say ?? Notify)(!r.Ok ? r.Why ?? "The site said no"
                : pieces.Count == 1 ? $"{data.ItemName(pieces[0].Item)}: on your shopping list"
                : $"{Words.Count(pieces.Count, "piece")} on your shopping list");
        });
    }

    /// <summary>Takes a piece off your shopping list (at once here; back on should the site say no).</summary>
    private void Unwish(uint item, Action<string>? say = null)
    {
        sync.RemoveWishes(new[] { item }, r =>
        {
            recorder.Note($"WISH removed {item}: {(r.Ok ? $"{r.Value} off" : r.Why)}");
            (say ?? Notify)(r.Ok ? $"{data.ItemName(item)}: off your shopping list" : r.Why ?? "The site said no");
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

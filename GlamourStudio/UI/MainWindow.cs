using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GlamourStudio.Core;
using GlamourStudio.Game;
using GlamourStudio.Session;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// The plugin's one window, in Glamour Studio's look (the site's night sky and its rose), wide: a
/// header across the top (the title, or the back arrow at the top left on a screen below; what the
/// plugin's doing; Settings at the top right), the rail down the left (Home, Looks, Plates, Closet,
/// Make room, and your character with how your closet's sent), and the screen beside it, most of
/// them a list or grid with the thing picked in a side pane. A look opened is a screen of its own,
/// the back arrow at its top left; Settings is one column, as Housing Studio's and Raid Planner's.
/// </summary>
public sealed partial class MainWindow : Window
{
    private enum Screen
    {
        Home,
        Looks,
        Look,
        Plates,
        Closet,
        MakeRoom,
        Settings,
    }

    private readonly Configuration config;
    private readonly GameData data;
    private readonly WardrobeReader wardrobe;
    private readonly SiteSync sync;
    private readonly Fonts fonts;
    private readonly Shots shots;
    private readonly Recorder recorder;
    private readonly List<Action> later = new();
    private Screen screen;

    /// <summary>Where a look was opened from: its back arrow goes there.</summary>
    private Screen lookFrom = Screen.Looks;

    public MainWindow(Configuration config, GameData data, WardrobeReader wardrobe, SiteSync sync, Fonts fonts, Shots shots, Recorder recorder)
        : base("Glamour Studio###GlamourStudioMain")
    {
        this.config = config;
        this.data = data;
        this.wardrobe = wardrobe;
        this.sync = sync;
        this.fonts = fonts;
        this.shots = shots;
        this.recorder = recorder;
        Txt.Fonts = fonts;

        Size = new Vector2(1120, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
        // 900 wide at the least: the rail as icons, a list and its side pane; 1000 and up, the rail with its words.
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(900, 600), MaximumSize = new Vector2(2400, 2400) };
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    /// <summary>Opens the window on Home.</summary>
    public void ShowMain()
    {
        screen = Screen.Home;
        openSent = null;
        IsOpen = true;
        sync.Wake();
    }

    /// <summary>The plate open in the game's plate editor this frame (0: it isn't open), looked up once a frame.</summary>
    private int editorPlate;

    /// <summary>The character last drawn for.</summary>
    private string? drawnFor;

    /// <summary>The open plate has changes not saved yet (this frame).</summary>
    private bool editorChanges;

    /// <summary>Opens Settings (Dalamud's cog, /gstudio settings).</summary>
    public void ShowSettings()
    {
        // Already open somewhere: Settings' back goes there, and a look sent from the site stays sent.
        if (!IsOpen)
        {
            openSent = null;
            settingsFrom = Screen.Home;
        }
        else if (screen != Screen.Settings) settingsFrom = screen;
        screen = Screen.Settings;
        IsOpen = true;
    }

    public override void OnClose()
    {
        settingsTab = SettingsTab.General;
    }

    public override void PreDraw()
    {
        if (!firstSizeFitted)
        {
            // The first size could be bigger than the screen, with the foot or the side off it.
            firstSizeFitted = true;
            var vp = ImGui.GetMainViewport().Size;
            var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            if (vp.Y > 0 && scale > 0 && Size is { } size)
            {
                var w = MathF.Min(size.X, MathF.Max(900, vp.X * 0.9f / scale));
                var h = MathF.Min(size.Y, MathF.Max(600, vp.Y * 0.9f / scale));
                if (w != size.X || h != size.Y) Size = new Vector2(w, h);
            }
        }
        Theme.PushFrame();
    }

    private bool firstSizeFitted;

    public override void PostDraw() => Theme.Pop();

    /// <summary>The header's height (16 above and below a 36px row), and the line under it.</summary>
    private const float HeaderCss = 68;

    /// <summary>The window's sides, as the layout's (24px).</summary>
    private const float SideCss = 24;

    public override void Draw()
    {
        Kit.ResetFrame();
        Frame.ResetFrame();
        editorPlate = PlateEditor.SelectedPlate;
        editorChanges = editorPlate > 0 && PlateEditor.HasChanges;
        CheckCharacter();
        Theme.PushInside();
        try
        {
            using (fonts.Push(Face.Sans, 14))
            {
                Kit.Blocked = confirm != null;
                try
                {
                    if (screen == Screen.Look && openLook == null) screen = lookFrom == Screen.Look ? Screen.Looks : lookFrom;
                    var win = ImGui.GetWindowPos();
                    var size = ImGui.GetWindowSize();
                    var top = ImGui.GetCursorScreenPos().Y;
                    // The hairline under Dalamud's title bar.
                    Kit.Dl.AddLine(new Vector2(win.X + 1, top + 0.5f), new Vector2(win.X + size.X - 1, top + 0.5f), Palette.U32(Palette.Line), 1f);
                    DrawHeader(new Vector2(win.X, top), size.X);
                    var bodyTop = top + S(HeaderCss);
                    Kit.Dl.AddLine(new Vector2(win.X + 1, bodyTop - 0.5f), new Vector2(win.X + size.X - 1, bodyTop - 0.5f), Palette.U32(Palette.Divider), 1f);
                    var body = new Vector2(win.X, bodyTop);
                    var bodySize = new Vector2(size.X, win.Y + size.Y - bodyTop);
                    switch (screen)
                    {
                        case Screen.Settings:
                            DrawSettings(body, bodySize);
                            break;
                        case Screen.Look:
                            DrawLook(openLook!, body, bodySize);
                            break;
                        default:
                            var railW = RailWidth(size.X);
                            DrawRail(body, new Vector2(railW, bodySize.Y));
                            var content = new Vector2(body.X + railW, body.Y);
                            var contentSize = new Vector2(bodySize.X - railW, bodySize.Y);
                            switch (screen)
                            {
                                case Screen.Looks:
                                    DrawLooks(content, contentSize);
                                    break;
                                case Screen.Plates:
                                    DrawPlates(content, contentSize);
                                    break;
                                case Screen.Closet:
                                    DrawCloset(content, contentSize);
                                    break;
                                case Screen.MakeRoom:
                                    DrawCleanup(content, contentSize);
                                    break;
                                default:
                                    screen = Screen.Home;
                                    DrawHome(content, contentSize);
                                    break;
                            }
                            break;
                    }
                }
                finally
                {
                    Frame.Unwind();
                    Frame.DrawToast();
                    Kit.Blocked = false;
                    DrawConfirm();
                }
            }
        }
        finally
        {
            Theme.Pop();
        }

        RunLater();
    }

    /// <summary>Another character: what was worked out for the last one goes.</summary>
    private void CheckCharacter()
    {
        if (wardrobe.Current?.Key == drawnFor) return;
        drawnFor = wardrobe.Current?.Key;
        lastFill = null;
        cleanup = null;
        examined = null;
        pickedLook = null;
        companionPick = null;
        storeQueue.Clear();
    }

    /// <summary>What was pressed this frame, done now that the frame's drawing is finished.</summary>
    private void RunLater()
    {
        if (later.Count > 0)
        {
            var actions = later.ToArray();
            later.Clear();
            foreach (var a in actions)
            {
                try
                {
                    a();
                }
                catch (Exception ex)
                {
                    Services.Log.Error(ex, "Glamour Studio action failed");
                    Notify("Something went wrong (see /xllog)");
                }
            }
        }
    }

    /// <summary>The rail's width: its words from 1000px of window up, its icons alone below.</summary>
    private static float RailWidth(float windowW) => windowW >= S(1000) ? S(212) : S(64);

    private void Go(Screen to)
    {
        screen = to;
        IsOpen = true;
    }

    // ---------------------------------------------------------------------------------------------
    // The header: the title (or back and the screen's title), what's going on, Settings.

    private void DrawHeader(Vector2 winTop, float width)
    {
        var x0 = winTop.X + S(SideCss);
        var x1 = winTop.X + width - S(SideCss);
        var y = winTop.Y + S(16);
        var dl = Kit.Dl;
        float left;
        switch (screen)
        {
            case Screen.Look when openLook != null:
            {
                if (Kit.IconButton("##back", new Vector2(x0, y), Icon.ArrowLeft, lookFrom switch
                    {
                        Screen.Home => "Back to Home",
                        Screen.Plates => "Back to your plates",
                        _ => "Back to your looks",
                    }, 36))
                    Later(LeaveLook);
                left = x0 + S(36 + 14);
                break;
            }
            case Screen.Settings:
            {
                if (Kit.IconButton("##back", new Vector2(x0, y), Icon.ArrowLeft, "Back", 36)) Later(LeaveSettings);
                Title(new Vector2(x0 + S(36 + 14), y + (S(36) - S(20 * 1.05f)) / 2), "Settings", x1 - x0 - S(50), 20, 1);
                return;
            }
            default:
                left = x0;
                break;
        }

        // Settings at the top right, always (but on Settings).
        SettingsButton(new Vector2(x1 - S(36), y));
        var right = x1 - S(36) - S(14);

        if (screen == Screen.Look && openLook != null)
        {
            const string site = "On the site";
            var sw = Kit.ButtonWidth(site, BtnSize.Sm, Icon.ExternalLink, Btn.Quiet);
            if (Kit.Button("##look-site", new Vector2(right - sw, y + (S(36) - Kit.ButtonHeight(BtnSize.Sm)) / 2), site, Btn.Quiet, BtnSize.Sm, Icon.ExternalLink, 0, false,
                    "The look's page on Glamour Studio"))
                Dalamud.Utility.Util.OpenLink(SiteLink.LookUrl(openLook.Id));
            right -= sw + S(14);
            var tw = right - left;
            var eyebrowH = Txt.Natural(Face.Mono, 11);
            var blockH = eyebrowH + S(4) + S(20 * 1.05f);
            var ty = y + (S(36) - blockH) / 2;
            Kit.Eyebrow(new Vector2(left, ty), LookEyebrow(openLook), null, tw);
            Title(new Vector2(left, ty + eyebrowH + S(4)), openLook.Name, tw, 20, 1);
            return;
        }

        // The title, then what's going on in a pill before Settings.
        var titleW = Txt.Measure("GLAMOUR STUDIO", Face.Display, 22).X;
        Title(new Vector2(left, y + (S(36) - S(22 * 1.05f)) / 2), "Glamour Studio", MathF.Min(titleW + S(4), right - left), 22, 1);
        var (status, tone, pulse) = StatusNow();
        var room = right - (left + titleW + S(24));
        if (room > S(80))
        {
            var pw = MathF.Min(Kit.StatusPillWidth(status), room);
            Kit.StatusPill(new Vector2(right - pw, y + S(4)), status, tone, pulse, room);
        }
        _ = dl;
    }

    private void LeaveLook()
    {
        openSent = null;
        screen = lookFrom is Screen.Look or Screen.Settings ? Screen.Looks : lookFrom;
    }

    private void LeaveSettings()
    {
        screen = settingsFrom switch
        {
            Screen.Look when openLook != null => Screen.Look,
            Screen.Settings => Screen.Home,
            var s => s,
        };
    }

    // ---------------------------------------------------------------------------------------------
    // The rail: where you can go, and your character.

    private static readonly (Screen Screen, Icon Icon, string Label)[] RailItems =
    {
        (Screen.Home, Icon.House, "Home"),
        (Screen.Looks, Icon.Hanger, "Looks"),
        (Screen.Plates, Icon.Layers, "Plates"),
        (Screen.Closet, Icon.Archive, "Closet"),
    };

    private void DrawRail(Vector2 min, Vector2 size)
    {
        var dl = Kit.Dl;
        dl.AddRectFilled(min, min + size, Palette.U32(Palette.Rail), S(14), ImDrawFlags.RoundCornersBottomLeft);
        dl.AddLine(new Vector2(min.X + size.X - 0.5f, min.Y), new Vector2(min.X + size.X - 0.5f, min.Y + size.Y), Palette.U32(Palette.Divider), 1f);
        var compact = size.X < S(120);
        Kit.BeginPane("##rail", min, new Vector2(size.X - 1, size.Y), 12, 16, 16, scroll: false);
        // (The rail's own draw list from here: what's drawn in it sits over its card, not under it.)
        dl = Kit.Dl;
        var x = Kit.Left;
        var w = Kit.Width;
        var me = wardrobe.Current;
        foreach (var (s, icon, label) in RailItems)
        {
            var at = Kit.At;
            if (Kit.NavItem($"##nav-{label}", at, w, icon, label, compact ? null : RailCount(s, me), screen == s, compact)) Later(() => Go(s));
            Kit.Reserve(at, w, S(40));
            Kit.Gap(2);
        }
        if (compact) Kit.Gap(14);
        else
        {
            Kit.Gap(14);
            var lat = Kit.At;
            Txt.Spaced(dl, new Vector2(x + S(12), lat.Y), "TIDY UP", Face.Mono, 10, Palette.Dim, 0.14f);
            Kit.Reserve(lat, w, Txt.Natural(Face.Mono, 10) + S(6));
            Kit.Gap(2);
        }
        {
            var at = Kit.At;
            if (Kit.NavItem("##nav-room", at, w, Icon.Package, "Make room", compact ? null : RailCount(Screen.MakeRoom, me), screen == Screen.MakeRoom, compact))
                Later(() => Go(Screen.MakeRoom));
            Kit.Reserve(at, w, S(40));
        }

        // Your character, at the foot.
        var cardH = compact ? S(44) : S(14) * 2 + S(34) + S(10) + S(16);
        var bottom = min.Y + size.Y - S(16);
        var cardTop = MathF.Max(Kit.At.Y + S(12), bottom - cardH);
        var ct = new Vector2(x, cardTop);
        var name = me?.Name is { Length: > 0 } n ? n : "Not logged in";
        var initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
        var (line, dot) = CharacterLine();
        if (compact)
        {
            var c = new Vector2(x + w / 2, ct.Y + S(22));
            dl.AddCircleFilled(c, S(17), Palette.U32(Palette.RoseTile), 40);
            var iw = Txt.Measure(initials, Face.SansSemibold, 12);
            Txt.Draw(dl, c - iw / 2, initials, Face.SansSemibold, 12, Palette.RoseText);
            Kit.Dot(c + new Vector2(S(12), S(12)), dot, 8);
            if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(c - new Vector2(S(17)), c + new Vector2(S(17))))
                Kit.Tooltip($"{name}{(me?.World is { Length: > 0 } wd ? " · " + wd : string.Empty)}\n{line}");
        }
        else
        {
            Kit.Card(ct, ct + new Vector2(w, cardH), Palette.Card, Palette.Line, 14);
            var p = ct + new Vector2(S(14));
            var c = p + new Vector2(S(17));
            dl.AddCircleFilled(c, S(17), Palette.U32(Palette.RoseTile), 40);
            var iw = Txt.Measure(initials, Face.SansSemibold, 12);
            Txt.Draw(dl, c - iw / 2, initials, Face.SansSemibold, 12, Palette.RoseText);
            var tx = p.X + S(34 + 10);
            var tw = ct.X + w - S(14) - tx;
            var nh = Txt.Natural(Face.SansSemibold, 13);
            var wh = Txt.Natural(Face.Sans, 12);
            var ty = c.Y - (nh + wh) / 2;
            Txt.Draw(dl, new Vector2(tx, ty), Txt.Fit(name, Face.SansSemibold, 13, tw), Face.SansSemibold, 13, Palette.Ink);
            Txt.Draw(dl, new Vector2(tx, ty + nh), Txt.Fit(me?.World ?? string.Empty, Face.Sans, 12, tw), Face.Sans, 12, Palette.Muted);
            var ly = p.Y + S(34) + S(10);
            Kit.Dot(new Vector2(p.X + S(3.5f), ly + S(8)), dot);
            Txt.Line(dl, new Vector2(p.X + S(15), ly), Txt.Fit(line, Face.Sans, 12, w - S(28) - S(15)), Face.Sans, 12, Palette.Muted, 16);
        }
        Kit.Reserve(new Vector2(x, cardTop), w, cardH);
        Kit.EndPane();
    }

    /// <summary>A rail item's count: your looks, plates with something on them of 20, pieces you have, the dresser's places used of 800.</summary>
    private string? RailCount(Screen s, CharacterWardrobe? me) => s switch
    {
        Screen.Looks when Linked && sync.LibraryAt != null => sync.Library.Mine.Count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
        Screen.Plates when me?.PlatesReadAt != null => $"{me.Plates.Count(p => p.Pieces.Count > 0)} / {Plates.Count}",
        Screen.Closet when me != null && PiecesKnown() > 0 => PiecesKnown().ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
        Screen.MakeRoom when me?.Get(Place.Dresser) != null => $"{DresserUsed(me)} / {DresserCleanup.Capacity}",
        _ => null,
    };

    /// <summary>How many pieces of gear you have, wherever they are (as the plugin knows).</summary>
    private int PiecesKnown()
    {
        Where(0);
        return placeIndex.Count;
    }

    /// <summary>The line under your character in the rail, and its dot's colour.</summary>
    private (string Line, Vector4 Dot) CharacterLine()
    {
        if (!sync.Linked) return ("Not linked to the site", Palette.Faint);
        if (sync.TokenRefused) return ("Link again: token refused", Palette.Danger);
        if (sync.Syncing) return ("Sending your closet…", Palette.Rose);
        if (sync.SyncProblem != null) return ("Closet not sent: see Closet", Palette.Danger);
        if (sync.LastSyncAt is { } at) return ($"Closet sent {Words.Ago(at, DateTimeOffset.UtcNow)}", Palette.Success);
        return (config.AutoSync ? "Linked · sends as you play" : "Linked", Palette.Success);
    }

    /// <summary>Runs after this frame's drawing is finished.</summary>
    private void Later(Action action) => later.Add(action);

    private static void Notify(string text) => Frame.Toast(text);

    // ---------------------------------------------------------------------------------------------
    // Titles and the header every screen below Home has.

    /// <summary>
    /// A title in Syne 800 capitals, as the site's display type, on up to <paramref name="lines"/>
    /// lines, cut with an ellipsis on the last. Returns its height; with <paramref name="draw"/> false, only measures.
    /// </summary>
    private static float Title(Vector2 pos, string text, float maxWidth, int px = 24, int lines = 2, bool draw = true, Vector4? color = null)
    {
        var lineCss = px * 1.05f;
        var lh = S(lineCss);
        maxWidth = MathF.Max(S(40), maxWidth);
        var caps = text.ToUpperInvariant();
        var rows = new List<string>();
        var cur = string.Empty;
        foreach (var word in caps.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length == 0 || Txt.Measure(candidate, Face.Display, px).X <= maxWidth) cur = candidate;
            else
            {
                rows.Add(cur);
                cur = word;
            }
        }
        rows.Add(cur);
        if (rows.Count > lines) rows = rows.Take(lines - 1).Append(string.Join(' ', rows.Skip(lines - 1))).ToList();
        if (!draw) return lh * rows.Count;
        var dl = Kit.Dl;
        for (var i = 0; i < rows.Count; i++)
            Txt.Line(dl, pos + new Vector2(0, lh * i), Txt.Fit(rows[i], Face.Display, px, maxWidth), Face.Display, px, color ?? Palette.Ink, lineCss);
        return lh * rows.Count;
    }

    /// <summary>The header's Settings button.</summary>
    private void SettingsButton(Vector2 pos)
    {
        if (Kit.IconButton("##settings", pos, Icon.Sliders, "Settings", 36)) OpenSettings(settingsTab);
    }

    // ---------------------------------------------------------------------------------------------
    // The question: a card over the window, its answers saying what happens.

    private sealed record Confirmation(string Title, string Body, string Yes, string No, Action OnYes, bool Danger);

    private Confirmation? confirm;
    private float confirmCardH;

    private void Ask(string title, string body, string yes, string no, Action onYes, bool danger = false) =>
        confirm = new Confirmation(title, body, yes, no, onYes, danger);

    /// <summary>
    /// The question over the plugin's window: a window of its own the size of the plugin's, kept in
    /// front, which darkens only the plugin's window and takes every click on it; the card in its middle.
    /// </summary>
    private void DrawConfirm()
    {
        if (confirm == null) return;
        var c = confirm;
        var winPos = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();
        ImGui.SetNextWindowPos(winPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(winSize, ImGuiCond.Always);
        ImGui.SetNextWindowFocus();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, S(14));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        var shown = ImGui.Begin("##gs-confirm-over", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
        try
        {
            if (!shown) return;
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(winPos, winPos + winSize, Palette.U32(Palette.WithAlpha(Palette.Ground, 0.75f)), S(14));
            var width = MathF.Min(S(420), winSize.X - S(48));
            var pad = S(24);
            var inner = width - pad * 2;
            var cardH = confirmCardH > 0 ? confirmCardH : S(200);
            var cardPos = new Vector2(winPos.X + (winSize.X - width) / 2, winPos.Y + MathF.Max(S(40), (winSize.Y - cardH) / 2));
            var r = S(Palette.RadiusLg);
            // The site's glow under its dialogs.
            for (var g = 3; g >= 1; g--)
                dl.AddRectFilled(cardPos - new Vector2(S(6 * g)), cardPos + new Vector2(width, cardH) + new Vector2(S(6 * g)),
                    Palette.U32(Palette.WithAlpha(Palette.Rose, 0.025f)), r + S(6 * g));
            dl.AddRectFilled(cardPos, cardPos + new Vector2(width, cardH), Palette.U32(Palette.Card), r);
            dl.AddRect(cardPos, cardPos + new Vector2(width, cardH), Palette.U32(Palette.LineStrong), r);

            var at = cardPos + new Vector2(pad, pad);
            ImGui.SetCursorScreenPos(at);
            Kit.PushBox(at.X, inner);
            var titleH = Title(at, c.Title, inner, 20, 2);
            Kit.Reserve(at, inner, titleH);
            Kit.Gap(10);
            Txt.Para(new[] { new Run(c.Body, Face.Sans, 14, Palette.Soft) }, 21);
            Kit.Gap(20);
            var buttons = Kit.At;
            var yesStyle = c.Danger ? Btn.Danger : Btn.Primary;
            var yesW = Kit.ButtonWidth(c.Yes, BtnSize.Md, null, yesStyle);
            var noW = Kit.ButtonWidth(c.No);
            var close = false;
            if (Kit.Button("##no", new Vector2(buttons.X + inner - yesW - S(8) - noW, buttons.Y), c.No)) close = true;
            if (Kit.Button("##yes", new Vector2(buttons.X + inner - yesW, buttons.Y), c.Yes, yesStyle))
            {
                close = true;
                Later(c.OnYes);
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Escape)) close = true;
            Kit.Reserve(buttons, inner, Kit.ButtonHeight(BtnSize.Md));
            Kit.PopBox();
            confirmCardH = Kit.At.Y - cardPos.Y + pad;
            if (close && ReferenceEquals(confirm, c)) confirm = null;
        }
        finally
        {
            ImGui.End();
        }
    }
}

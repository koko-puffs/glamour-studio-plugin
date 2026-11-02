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
/// The plugin's one window, in Glamour Studio's look (the site's night sky and its rose). Five
/// screens: Home (what the plugin's doing; the plate editor when it's open, with the look planned
/// for its plate; someone you examine; looks sent from the site; your looks, the ones you love, the
/// looks you could wear now, your plates and your closet as tabs), a look (its pieces, where each
/// is, Put on plate N, putting pieces away, a shot for the site), a plate of yours (its pieces, its
/// plan, its gearsets, Keep it as a look, Wear it), Make room in your dresser, and Settings
/// (General and Account). Below Home every screen has the back arrow at its top left, as
/// Housing Studio's and Raid Planner's plugins have it.
/// </summary>
public sealed partial class MainWindow : Window
{
    private enum Screen
    {
        Home,
        Look,
        Plate,
        Cleanup,
        Settings,
    }

    private readonly Configuration config;
    private readonly GameData data;
    private readonly WardrobeReader wardrobe;
    private readonly SiteSync sync;
    private readonly Fonts fonts;
    private readonly Shots shots;
    private readonly List<Action> later = new();
    private Screen screen;

    public MainWindow(Configuration config, GameData data, WardrobeReader wardrobe, SiteSync sync, Fonts fonts, Shots shots)
        : base("Glamour Studio###GlamourStudioMain")
    {
        this.config = config;
        this.data = data;
        this.wardrobe = wardrobe;
        this.sync = sync;
        this.fonts = fonts;
        this.shots = shots;
        Txt.Fonts = fonts;

        Size = new Vector2(520, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
        // 490 wide at the least: the title in full beside Settings, and Home's five tabs on one line (their counts left out when narrow).
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(490, 500), MaximumSize = new Vector2(1400, 2400) };
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
        openSent = null;
        settingsFrom = Screen.Home;
        screen = Screen.Settings;
        IsOpen = true;
    }

    public override void OnClose()
    {
        openSent = null;
        settingsTab = SettingsTab.General;
    }

    public override void PreDraw()
    {
        if (!firstSizeFitted)
        {
            // The first size could be taller than the screen, with the foot off it.
            firstSizeFitted = true;
            var screenH = ImGui.GetMainViewport().Size.Y;
            var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            if (screenH > 0 && scale > 0 && Size is { } size && size.Y * scale > screenH * 0.9f)
                Size = new Vector2(size.X, MathF.Max(500, screenH * 0.9f / scale));
        }
        Theme.PushFrame();
    }

    private bool firstSizeFitted;

    public override void PostDraw() => Theme.Pop();

    public override void Draw()
    {
        Kit.ResetFrame();
        Frame.ResetFrame();
        editorPlate = PlateEditor.SelectedPlate;
        editorChanges = editorPlate > 0 && PlateEditor.HasChanges;
        // Another character: what was worked out for the last one goes.
        if (wardrobe.Current?.Key != drawnFor)
        {
            drawnFor = wardrobe.Current?.Key;
            lastFill = null;
            cleanup = null;
            examined = null;
        }
        Theme.PushInside();
        try
        {
            using (fonts.Push(Face.Sans, 14))
            {
                Kit.Blocked = confirm != null;
                try
                {
                    switch (screen)
                    {
                        case Screen.Settings:
                            DrawSettings();
                            break;
                        case Screen.Look when openLook != null:
                            DrawLook(openLook);
                            break;
                        case Screen.Plate when openPlate >= 0:
                            DrawPlate(openPlate);
                            break;
                        case Screen.Cleanup:
                            DrawCleanup();
                            break;
                        default:
                            screen = Screen.Home;
                            DrawHome();
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

    /// <summary>
    /// The header of every screen below Home: the round back arrow at the top left, the eyebrow (rose
    /// mono capitals) and the title beside it, and the screen's own round buttons on the right
    /// (<paramref name="right"/> draws them from the top-left corner it's given, in
    /// <paramref name="rightWidth"/>). True when back was pressed.
    /// </summary>
    private static bool ScreenHeader(string backTip, string? eyebrow, string title, float rightWidth = 0, Action<Vector2>? right = null)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var tx = at.X + S(36) + S(12);
        var textW = at.X + w - tx - (rightWidth > 0 ? rightWidth + S(12) : 0);
        var back = Kit.IconButton("##back", at, Icon.ArrowLeft, backTip, 36);
        var y = at.Y;
        if (!string.IsNullOrEmpty(eyebrow))
        {
            y += Kit.Eyebrow(new Vector2(tx, y), eyebrow, null, textW);
            y += S(4);
        }
        else y += MathF.Max(0, (S(36) - S(20 * 1.05f)) / 2);
        y += Title(new Vector2(tx, y), title, textW, 20);
        right?.Invoke(new Vector2(at.X + w - rightWidth, at.Y));
        Kit.Reserve(at, w, MathF.Max(S(36), y - at.Y));
        return back;
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

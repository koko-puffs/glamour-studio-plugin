using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GlamourStudio.Core;
using GlamourStudio.Game;
using GlamourStudio.Session;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// The plugin's one window, in Glamour Studio's "Character Sheet" look (docs/redesign): under
/// Dalamud's own title bar, the site's bar (the logo and GLAMOUR STUDIO; Home · Looks · Plates ·
/// Closet · Make room with their counts and the holo underline under the one you're on; your
/// character's pill with what's going on; Refresh and Settings), and the screen under it across the
/// whole width, most of them a list or grid with the thing picked in a side pane. A look opened and
/// Settings are screens below: the bar holds the back arrow at the top left and the screen's title.
/// (A rail down the left held the places and your character until the redesign.)
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
        linkedToken = config.SiteToken;
        Txt.Fonts = fonts;

        Size = new Vector2(1120, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
        // 900 wide at the least: the bar's places as icons, a list and its side pane; 1000 and up, the places with their words.
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

    /// <summary>When the window last closed (UTC; MinValue: not since the plugin loaded).</summary>
    private DateTime closedAt = DateTime.MinValue;

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
        // A wish come true stays until the window closes (Closet's card).
        closedAt = DateTime.UtcNow;
        // A question left open goes with the window (its answer was for what was on screen then).
        confirm = null;
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

    /// <summary>The bar's height under Dalamud's title bar, its line included (the mock's 56).</summary>
    private const float HeaderCss = Kit.BarHeightCss;

    /// <summary>The page's sides, as the mock's (<c>.page</c>: 20px top and bottom, 24px either side).</summary>
    private const float SideCss = 24;

    /// <summary>The page's top and bottom padding.</summary>
    private const float PageTopCss = 20;

    public override void Draw()
    {
        Kit.ResetFrame();
        Frame.ResetFrame();
        editorPlate = PlateEditor.SelectedPlate;
        editorChanges = editorPlate > 0 && PlateEditor.HasChanges;
        CheckCharacter();
        CheckOpenLook();
        var drawing = screen;
        var failed = false;
        var noteAt = Vector2.Zero;
        Theme.PushInside();
        try
        {
            using (fonts.Push(Face.Sans, 14))
            {
                Kit.Blocked = confirm != null;
                try
                {
                    if (screen == Screen.Look && openLook == null) screen = lookFrom == Screen.Look ? Screen.Looks : lookFrom;
                    drawing = screen;
                    var win = ImGui.GetWindowPos();
                    var size = ImGui.GetWindowSize();
                    var top = ImGui.GetCursorScreenPos().Y;
                    var bodyTop = top + S(HeaderCss);
                    // The bar's line; the place you're on draws its holo underline over it.
                    Kit.Dl.AddLine(new Vector2(win.X + 1, bodyTop - 0.5f), new Vector2(win.X + size.X - 1, bodyTop - 0.5f), Palette.U32(Palette.Line), 1f);
                    DrawBar(new Vector2(win.X, top), size.X);
                    // The page: the whole width under the bar (each screen pads itself: the mock's 20 and 24).
                    var body = new Vector2(win.X, bodyTop);
                    var bodySize = new Vector2(size.X, win.Y + size.Y - bodyTop);
                    noteAt = body + new Vector2(S(SideCss), S(PageTopCss));
                    switch (screen)
                    {
                        case Screen.Settings:
                            DrawSettings(body, bodySize);
                            break;
                        case Screen.Look:
                            DrawLook(openLook!, body, bodySize);
                            break;
                        case Screen.Looks:
                            DrawLooks(body, bodySize);
                            break;
                        case Screen.Plates:
                            DrawPlates(body, bodySize);
                            break;
                        case Screen.Closet:
                            DrawCloset(body, bodySize);
                            break;
                        case Screen.MakeRoom:
                            DrawCleanup(body, bodySize);
                            break;
                        default:
                            screen = Screen.Home;
                            DrawHome(body, bodySize);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Said once (not every frame it fails again), and a note in its place.
                    failed = true;
                    if (failures.Count < 64 && failures.Add($"{drawing}|{ex.GetType().FullName}|{ex.Message}"))
                        Services.Log.Error(ex, "Glamour Studio's {Screen} screen failed", drawing);
                }
                finally
                {
                    Frame.Unwind();
                    if (failed) DrawFailed(noteAt);
                    Frame.DrawToast();
                    Kit.Blocked = false;
                    DrawConfirm();
                }
            }
        }
        finally
        {
            Theme.Pop();
            // What was pressed is done even on a frame that failed.
            RunLater();
        }
    }

    /// <summary>The screens' failures already in the log (by screen, kind and message).</summary>
    private readonly HashSet<string> failures = new();

    /// <summary>A screen that failed: a short note where it would be (the mock's card with the danger edge, the warning sign, the words).</summary>
    private static void DrawFailed(Vector2 at)
    {
        Palette.Opacity = 1f;
        if (at == Vector2.Zero) at = ImGui.GetWindowPos() + new Vector2(S(SideCss), S(HeaderCss) + S(PageTopCss));
        const string text = "This screen failed (see /xllog)";
        var size = Txt.Measure(text, Face.Sans, 13);
        var dl = Kit.Dl;
        var box = new Vector2(S(16 + 16 + 10) + size.X + S(16), MathF.Max(size.Y, S(16)) + S(14) * 2);
        dl.AddRectFilled(at, at + box, Palette.U32(Palette.Card), S(Palette.RadiusLg));
        dl.AddRect(at + new Vector2(0.5f), at + box - new Vector2(0.5f), Palette.U32(Palette.DangerEdge), S(Palette.RadiusLg), ImDrawFlags.None, 1f);
        Icons.Draw(dl, Icon.AlertTriangle, at + new Vector2(S(16), (box.Y - S(16)) / 2), S(16), Palette.Danger);
        Txt.Draw(dl, at + new Vector2(S(16 + 16 + 10), (box.Y - size.Y) / 2), text, Face.Sans, 13, Palette.Body);
    }

    /// <summary>Another character: what was worked out for the last one goes.</summary>
    private void CheckCharacter()
    {
        CheckLink();
        if (wardrobe.Current?.Key != drawnFor)
        {
            drawnFor = wardrobe.Current?.Key;
            lastFill = null;
            cleanup = null;
            examined = null;
            pickedLook = null;
            companionPick = null;
            storeQueue.Clear();
            storeRefused = default;
            // A question asked for the last character isn't this one's.
            confirm = null;
        }
        CheckFill();
    }

    /// <summary>The token the window last drew for.</summary>
    private string linkedToken;

    /// <summary>Linked again (another account, or none): the last account's looks and what was kept to it go.</summary>
    private void CheckLink()
    {
        if (string.Equals(config.SiteToken, linkedToken, StringComparison.Ordinal)) return;
        linkedToken = config.SiteToken;
        openLook = null;
        openSent = null;
        pickedLook = null;
        companionPick = null;
        keptFrom.Clear();
        keeping.Clear();
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
                    Frame.Warn("Something went wrong (see /xllog)");
                }
            }
        }
    }

    private void Go(Screen to)
    {
        screen = to;
        IsOpen = true;
    }

    // ---------------------------------------------------------------------------------------------
    // The bar: the brand and the places (or back and the screen's title), the pill, Refresh, Settings.

    /// <summary>The bar's places, in its order (the mock's: Make room is the fifth, no longer apart under Tidy up).</summary>
    private static readonly (Screen Screen, Icon Icon, string Label)[] BarPlaces =
    {
        (Screen.Home, Icon.House, "Home"),
        (Screen.Looks, Icon.Hanger, "Looks"),
        (Screen.Plates, Icon.Layers, "Plates"),
        (Screen.Closet, Icon.Box, "Closet"),
        (Screen.MakeRoom, Icon.Package, "Make room"),
    };

    /// <summary>Each place's id, made once (not a new string every frame).</summary>
    private static readonly string[] BarPlaceIds = Array.ConvertAll(BarPlaces, p => "##place-" + p.Label);

    /// <summary>Each place's count this frame (null: none yet), worked out once a frame for the bar.</summary>
    private readonly string?[] placeCounts = new string?[5];

    /// <summary>
    /// The bar (the mock's <c>.bar</c>): 56 tall, 18px in at the left and 14 at the right. On the
    /// five places: the brand, a slash, the places, then at the right the pill, Refresh and Settings.
    /// Under 1000 wide (or when they don't fit) the places' counts go, then their words, the
    /// wordmark and the pill's line (each to a tooltip). On a look or Settings: the back arrow, the
    /// screen's eyebrow and title, and what's at its right.
    /// </summary>
    private void DrawBar(Vector2 winTop, float width)
    {
        var h = S(HeaderCss) - 1;
        var x0 = winTop.X + S(18);
        var x1 = winTop.X + width - S(14);
        var y = winTop.Y;
        var btnY = y + (h - S(36)) / 2;

        if (screen == Screen.Settings)
        {
            DrawBackBar(x0, x1, y, h, "Back", LeaveSettings, null, "Settings", false);
            return;
        }
        if (screen == Screen.Look && openLook != null)
        {
            var back = lookFrom switch
            {
                Screen.Home => "Back to Home",
                Screen.Plates => "Back to your plates",
                _ => "Back to your looks",
            };
            DrawBackBar(x0, x1, y, h, back, LeaveLook, LookEyebrow(openLook), openLook.Name, true);
            return;
        }

        // Refresh and Settings at the right, quiet; the pill before them.
        var right = DrawBarEnd(x1, btnY);
        var narrow = width < S(1000);
        var me = wardrobe.Current;
        for (var i = 0; i < BarPlaces.Length; i++) placeCounts[i] = RailCount(BarPlaces[i].Screen, me);
        float PlacesWidth(bool counts, bool icons)
        {
            var sum = 0f;
            for (var i = 0; i < BarPlaces.Length; i++) sum += Kit.PlaceWidth(BarPlaces[i].Label, counts ? placeCounts[i] : null, icons) + S(2);
            return sum;
        }
        var (pillText, pillTone, pillOff, pulse) = PillNow();
        var slash = S(6 + 8) + Txt.Measure("/", Face.Sans, 16).X;
        var (mode, pillW) = Model.BarFit.Choose(right - x0, narrow,
            Kit.BrandWidth(true) + S(4) + slash, Kit.BrandWidth(false) + S(4) + slash,
            PlacesWidth(true, false), PlacesWidth(false, false), PlacesWidth(false, true),
            Kit.WhoPillWidth(pillText), S(160), Kit.WhoPillWidth(pillText, compact: true), S(12));
        var icons = mode == Model.BarMode.Icons;

        var x = x0 + Kit.Brand(new Vector2(x0, y), h, wordmark: !icons) + S(4);
        x += S(6);
        Txt.Line(Kit.Dl, new Vector2(x, y), "/", Face.Sans, 16, Palette.Faint, h / S(1));
        x += Txt.Measure("/", Face.Sans, 16).X + S(8);
        for (var i = 0; i < BarPlaces.Length; i++)
        {
            var (s, icon, label) = BarPlaces[i];
            var count = mode == Model.BarMode.Full ? placeCounts[i] : null;
            if (Kit.Place(BarPlaceIds[i], new Vector2(x, y), h, icon, label, count, screen == s, icons, placeCounts[i]))
                Later(() => Go(s));
            x += Kit.PlaceWidth(label, count, icons) + S(2);
        }

        // Your character: pointing at it shows the name and world; pressing it opens Settings → Account.
        var name = me?.Name is { Length: > 0 } n ? n : null;
        var tip = name != null ? (me?.World is { Length: > 0 } world ? $"{name} · {world}" : name) : null;
        var pillX = right - S(6) - pillW;
        var (pressed, _) = Kit.WhoPill("##who", new Vector2(pillX, y + (h - S(34)) / 2), InitialsOf(name), pillText, pillTone, pillOff, pulse,
            pillW, icons, tip);
        if (pressed) Later(() => OpenSettings(SettingsTab.Account));
    }

    /// <summary>Refresh and Settings at the bar's right (quiet, round, 36), 6px apart. Returns where what's before them ends.</summary>
    private float DrawBarEnd(float x1, float btnY)
    {
        var x = x1 - S(36);
        SettingsButton(new Vector2(x, btnY));
        x -= S(6) + S(36);
        DrawRefresh(new Vector2(x, btnY));
        return x;
    }

    /// <summary>Refresh: dimmed while it reads, its arrows turning (still under reduced motion).</summary>
    private void DrawRefresh(Vector2 pos)
    {
        if (Kit.RefreshButton("##refresh", pos, sync.LibraryBusy, "Read it all again: your looks, list and plans from the site, your wardrobe from the game"))
            Later(RefreshEverything);
    }

    /// <summary>
    /// The bar of a screen below (a look, Settings): the back arrow (outlined, round 36), the
    /// screen's eyebrow and title (Bricolage 19), then at the right On the site (a look), Refresh and
    /// Settings (not on Settings itself).
    /// </summary>
    private void DrawBackBar(float x0, float x1, float y, float h, string backTip, Action back, string? eyebrow, string title, bool look)
    {
        var btnY = y + (h - S(36)) / 2;
        if (Kit.IconButton("##back", new Vector2(x0, btnY), Icon.ArrowLeft, backTip, 36)) Later(back);
        var left = x0 + S(36 + 12);
        var right = x1;
        if (look)
        {
            right = DrawBarEnd(x1, btnY) - S(6);
            const string site = "On the site";
            var sw = Kit.ButtonWidth(site, BtnSize.Sm, Icon.ExternalLink, Btn.Quiet);
            if (Kit.Button("##look-site", new Vector2(right - sw, y + (h - Kit.ButtonHeight(BtnSize.Sm)) / 2), site, Btn.Quiet, BtnSize.Sm, Icon.ExternalLink, 0, false,
                    "The look's page on Glamour Studio", iconCss: 14) && openLook != null)
                Dalamud.Utility.Util.OpenLink(SiteLink.LookUrl(openLook.Id));
            right -= sw + S(12);
        }
        var tw = MathF.Max(S(40), right - left);
        var titleH = Kit.DisplayHeight(19) * 0.86f;
        var blockH = (eyebrow != null ? Kit.EyebrowHeight + S(3) : 0) + titleH;
        var ty = y + (h - blockH) / 2;
        if (eyebrow != null)
        {
            Kit.Eyebrow(new Vector2(left, ty), eyebrow, null, tw);
            ty += Kit.EyebrowHeight + S(3);
        }
        // (Bricolage's capitals sit high in its line: the line box is the mock's 1.05em.)
        Kit.Display(new Vector2(left, ty - Kit.DisplayHeight(19) * 0.07f), title, 19, Palette.Ink, tw);
    }

    /// <summary>The pill's line and how it looks: the header's status (the first that's true wins), or "Log in to a character".</summary>
    private (string Text, Tone Tone, bool Off, bool Pulse) PillNow()
    {
        var (text, tone, pulse) = StatusNow();
        if (recorder.Recording) return (text, Tone.Recording, false, true);
        if (wardrobe.Current == null && editorPlate <= 0) return ("Log in to a character", Tone.Neutral, true, false);
        // Not linked, or not sent: the face greys.
        return (text, tone, tone == Tone.Neutral, pulse);
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

    private (string? Name, string Initials) initials = (null, "?");

    /// <summary>A name's initials (the first two words'), worked out again only when the name changes.</summary>
    private string InitialsOf(string? name)
    {
        if (name != initials.Name) initials = (name, Model.Initials.Of(name));
        return initials.Initials;
    }

    /// <summary>A place's count: your looks, plates with something on them of 20, pieces you have, the dresser's places used of 800.</summary>
    private string? RailCount(Screen s, CharacterWardrobe? me) => s switch
    {
        Screen.Looks when Linked && sync.LibraryAt != null => sync.Library.Mine.Count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
        Screen.Plates when me?.PlatesReadAt != null => $"{me.Plates.Count(p => p.Pieces.Count > 0)}/{Plates.Count}",
        Screen.Closet when me != null && PiecesKnown() > 0 => PiecesKnown().ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
        Screen.MakeRoom when me?.Get(Place.Dresser) != null => $"{DresserUsed(me)}/{DresserCleanup.Capacity}",
        _ => null,
    };

    /// <summary>How many pieces of gear you have, wherever they are (as the plugin knows).</summary>
    private int PiecesKnown()
    {
        Where(0);
        return placeIndex.Count;
    }

    /// <summary>Runs after this frame's drawing is finished.</summary>
    private void Later(Action action) => later.Add(action);

    /// <summary>
    /// Reads everything again now: your looks, wishlist, plans, sent looks and what you can wear
    /// from the site, and your wardrobe from the game (the dresser and armoire only while they're
    /// open: the game has them only then).
    /// </summary>
    internal void RefreshEverything() => RefreshEverything(null);

    /// <param name="say">Where it says so (a toast when not given: the command says it in chat, no window may be open).</param>
    internal void RefreshEverything(Action<string>? say)
    {
        wardrobe.Tick(now: true);
        sync.RefreshAll();
        recorder.Note("REFRESH everything");
        (say ?? Notify)(Linked ? "Reading it all again" : "Your wardrobe's read again (link your account for your looks)");
    }

    private static void Notify(string text) => Frame.Toast(text);

    // ---------------------------------------------------------------------------------------------
    // Titles and the header every screen below Home has.

    /// <summary>A title's line, times its size (the mock's display lines are tight: 0.98 to 1.05).</summary>
    private const float TitleLine = 1.05f;

    /// <summary>
    /// A title in Bricolage 800 capitals, as the mock's display type (Outfit 700 in normal capitals
    /// until the redesign), on up to <paramref name="lines"/> lines, cut with an ellipsis on the last.
    /// Returns its height; with <paramref name="draw"/> false, only measures. (<see cref="Kit.Display"/>
    /// is the Kit's, one line, with the stacked shadows the field's titles have.)
    /// </summary>
    private static float Title(Vector2 pos, string text, float maxWidth, int px = 24, int lines = 2, bool draw = true, Vector4? color = null)
    {
        var lineCss = px * TitleLine;
        var lh = S(lineCss);
        maxWidth = MathF.Max(S(40), maxWidth);
        var rows = new List<string>();
        var cur = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length == 0 || Kit.DisplayWidth(candidate, px) <= maxWidth) cur = candidate;
            else
            {
                rows.Add(cur);
                cur = word;
            }
        }
        rows.Add(cur);
        if (rows.Count > lines) rows = rows.Take(lines - 1).Append(string.Join(' ', rows.Skip(lines - 1))).ToList();
        if (!draw) return lh * rows.Count;
        // Bricolage's line is 1.2em; the title's is tighter, so each line's glyphs are centred in it.
        var nudge = (lh - Kit.DisplayHeight(px)) / 2;
        for (var i = 0; i < rows.Count; i++)
            Kit.Display(pos + new Vector2(0, lh * i + nudge), rows[i], px, color ?? Tint.Ink, maxWidth);
        return lh * rows.Count;
    }

    /// <summary>The tool's mark: the logo texture (the clothes tag), <paramref name="size"/> screen px across.</summary>
    private static void Mark(ImDrawListPtr dl, Vector2 pos, float size)
    {
        _ = dl;
        Kit.Logo(pos, size / S(1));
    }

    /// <summary>The bar's Settings button (quiet, round 36).</summary>
    private void SettingsButton(Vector2 pos)
    {
        if (Kit.IconButton("##settings", pos, Icon.SlidersH, "Settings", 36, Btn.Quiet)) OpenSettings(settingsTab);
    }

    // ---------------------------------------------------------------------------------------------
    // The question: a card over the window, its answers saying what happens.

    /// <summary>A line of the question's list: a dot in an honest colour and what it says (the mock's <c>.dot</c> rows).</summary>
    private readonly record struct QuestionLine(Vector4 Dot, string Text);

    /// <summary>
    /// A question over the window (the mock's <c>.dialog</c>): its eyebrow (optional), title, the
    /// paragraph, the list of what it'll do in the honest colours (optional), a muted second paragraph
    /// (optional), and the answers. A danger question's yes is the filled red, else the sticker.
    /// </summary>
    private sealed record Question(string Title, IReadOnlyList<Run> Body, string Yes, string No, Action OnYes, bool Danger)
    {
        public string? Eyebrow { get; init; }

        public IReadOnlyList<QuestionLine>? Lines { get; init; }

        public IReadOnlyList<Run>? Note { get; init; }
    }

    private Question? confirm;
    private float confirmCardH;

    /// <summary>The question's paragraph in its words (14px, the body colour).</summary>
    private static Run QuestionRun(string text) => new(text, Face.Sans, 14, Palette.Body);

    /// <summary>A name in the question's paragraph, picked out (the mock's <c>b.ink</c>).</summary>
    private static Run QuestionName(string text) => new(text, Face.SansBold, 14, Palette.Ink);

    private void Ask(string title, string body, string yes, string no, Action onYes, bool danger = false, string? note = null) =>
        Ask(new Question(title, new[] { QuestionRun(body) }, yes, no, onYes, danger) { Note = NoteRuns(note) });

    private void Ask(Question question) => confirm = question;

    private static Run[]? NoteRuns(string? note) => note == null ? null : new[] { new Run(note, Face.Sans, 13, Palette.Muted) };

    /// <summary>
    /// The question's words at the cursor, across the box: the eyebrow (after the logo, when
    /// <paramref name="logo"/>), the title in display capitals, the paragraph, the dotted lines and
    /// the muted second paragraph (the mock's <c>.dialog</c> and the character question's <c>.ask</c>).
    /// </summary>
    private static void QuestionWords(float inner, string? eyebrow, bool logo, string title, int titlePx, int titleLines, IReadOnlyList<Run> body,
        IReadOnlyList<QuestionLine>? lines, IReadOnlyList<Run>? note)
    {
        if (eyebrow != null || logo)
        {
            var at = Kit.At;
            var rowH = logo ? S(22) : Kit.EyebrowHeight;
            var x = at.X;
            if (logo)
            {
                Kit.Logo(at, 22);
                x += S(22 + 10);
            }
            if (eyebrow != null) Kit.Eyebrow(new Vector2(x, at.Y + (rowH - Kit.EyebrowHeight) / 2), eyebrow, Palette.Accent, inner - (x - at.X));
            Kit.Reserve(at, inner, rowH);
            Kit.Gap(logo ? 10 : 6);
        }
        var tAt = Kit.At;
        var titleH = Title(tAt, title, inner, titlePx, titleLines);
        Kit.Reserve(tAt, inner, titleH);
        Kit.Gap(10);
        Txt.Para(body, 21);
        if (lines is { Count: > 0 })
        {
            // Each a 7px dot, 8 before its words (13px), 6 between (the slip's dotted lines).
            Kit.Gap(12);
            for (var i = 0; i < lines.Count; i++)
            {
                if (i > 0) Kit.Gap(6);
                DotLine(lines[i].Dot, lines[i].Text, 13);
            }
        }
        if (note is { Count: > 0 })
        {
            Kit.Gap(lines is { Count: > 0 } ? 10 : 8);
            Txt.Para(note, 19.5f);
        }
    }

    /// <summary>
    /// The question over the plugin's window: a window of its own the size of the plugin's, kept in
    /// front, which darkens only the plugin's window and takes every click on it; the card in its
    /// middle (the mock's <c>.dialog</c>: 440 wide, 28px corners, the strong hairline, no glow; the
    /// title in display capitals 26, the yes the sticker, or the filled red for a danger question;
    /// Escape is No).
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
        bool shown;
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero).Push(ImGuiStyleVar.WindowBorderSize, 0f).Push(ImGuiStyleVar.WindowRounding, S(14)))
        using (ImRaii.PushColor(ImGuiCol.WindowBg, Vector4.Zero))
        {
            shown = ImGui.Begin("##gs-confirm-over", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing);
        }
        try
        {
            if (!shown) return;
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(winPos, winPos + winSize, Palette.U32(Palette.WithAlpha(Palette.Ground, 0.75f)), S(12));
            // 440 wide; one with a list of what it'll do, 460 (the mock's Make room question).
            var width = MathF.Min(S(c.Lines is { Count: > 0 } ? 460 : 440), winSize.X - S(48));
            var pad = S(26);
            var inner = width - pad * 2;
            var cardH = confirmCardH > 0 ? confirmCardH : S(200);
            var cardPos = new Vector2(winPos.X + (winSize.X - width) / 2, winPos.Y + MathF.Max(S(40), (winSize.Y - cardH) / 2));
            var r = S(Palette.RadiusXl);
            dl.AddRectFilled(cardPos, cardPos + new Vector2(width, cardH), Palette.U32(Palette.Card), r);
            dl.AddRect(cardPos + new Vector2(0.5f), cardPos + new Vector2(width, cardH) - new Vector2(0.5f), Palette.U32(Palette.LineStrong), r, ImDrawFlags.None, 1f);

            var at = cardPos + new Vector2(pad, S(24));
            ImGui.SetCursorScreenPos(at);
            Kit.PushBox(at.X, inner);
            try
            {
                QuestionWords(inner, c.Eyebrow, false, c.Title, 26, 2, c.Body, c.Lines, c.Note);
                Kit.Gap(20);
                var buttons = Kit.At;
                var yesStyle = c.Danger ? Btn.DangerFilled : Btn.Primary;
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
                confirmCardH = Kit.At.Y - cardPos.Y + S(22);
                if (close && ReferenceEquals(confirm, c)) confirm = null;
            }
            finally
            {
                Kit.PopBox();
            }
        }
        finally
        {
            ImGui.End();
        }
    }
}

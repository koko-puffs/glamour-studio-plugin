using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using GlamourStudio.Core;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>Settings' tabs: how the plugin works, and the site.</summary>
public enum SettingsTab
{
    General,
    Account,
}

/// <summary>
/// Settings, plain and quiet as the site's account pages (docs/redesign/pages/settings.html): one
/// column 640 wide in the middle, General and Account as a segmented pick, each group a quiet label
/// over a card of rows, each row a setting's name, one line of why, and its switch or button at the
/// row's end (the longer words are the tooltip over the words). A row that can't be changed is
/// dimmed with the reason as its line. The recorder is General's last group; the version and What's
/// new under the column.
/// </summary>
public sealed partial class MainWindow
{
    private const string ReleasesUrl = "https://github.com/koko-puffs/glamour-studio-plugin/releases";

    private static readonly TabSpec[] SettingsTabs = { new("General"), new("Account") };

    private SettingsTab settingsTab;

    /// <summary>Where Settings was opened from: its back arrow goes there.</summary>
    private Screen settingsFrom = Screen.Home;

    private string tokenDraft = string.Empty;
    private string? tokenProblem;

    /// <summary>Each window's panel unrolled to its switches (Choose).</summary>
    private bool panelsUnrolled;

    private void OpenSettings(SettingsTab tab)
    {
        settingsTab = tab;
        if (screen != Screen.Settings) settingsFrom = screen;
        screen = Screen.Settings;
    }

    private void DrawSettings(Vector2 min, Vector2 size)
    {
        // One column, 640 at most, in the middle: as Housing Studio's and Raid Planner's Settings.
        var colW = MathF.Min(S(640), size.X - S(SideCss) * 2);
        var padX = (size.X - colW) / 2 / ImGuiHelpers.GlobalScale;
        Kit.BeginPane(settingsTab == SettingsTab.Account ? "##settings-account" : "##settings-general", min, size, padX, PageTopCss, PageTopCss);
        var at = Kit.At;
        settingsTab = (SettingsTab)Kit.Seg("##settings-tab", at, SettingsTabs, (int)settingsTab, 13);
        Kit.Reserve(at, Kit.Width, Kit.SegHeight);
        if (settingsTab == SettingsTab.Account) DrawAccountTab();
        else DrawGeneralTab();
        DrawVersionLine(settingsTab == SettingsTab.Account ? 20 : 24);
        Kit.EndPane();
    }

    /// <summary>"Glamour Studio 0.6.16 ·", made once.</summary>
    private static readonly string VersionWords = typeof(MainWindow).Assembly.GetName().Version is { } v
        ? $"Glamour Studio {v.Major}.{v.Minor}.{v.Build} ·"
        : "Glamour Studio ·";

    /// <summary>The version and What's new under the column (the 18px logo, faint words, the lilac link).</summary>
    private static void DrawVersionLine(float gapCss)
    {
        Kit.Gap(gapCss);
        var at = Kit.At;
        var dl = Kit.Dl;
        var h = S(18);
        Kit.Logo(at, 18);
        var x = at.X + S(18 + 8);
        x += Txt.Line(dl, new Vector2(x, at.Y), VersionWords, Face.Sans, 12, Palette.Faint, 18) + S(6);
        if (Kit.Link("##whats-new", new Vector2(x, at.Y + (h - Txt.Natural(Face.SansSemibold, 12)) / 2), "What's new", 12, Face.SansSemibold))
            Dalamud.Utility.Util.OpenLink(ReleasesUrl);
        Kit.Reserve(at, Kit.Width, h);
    }

    /// <summary>A group's label over its card ("Glamour plates"): Figtree 600 12px, muted, 22px under what's above.</summary>
    private static void GroupLabel(string text)
    {
        Kit.Gap(22);
        var at = Kit.At;
        Txt.Line(Kit.Dl, new Vector2(at.X + S(2), at.Y), Txt.Fit(text, Face.SansSemibold, 12, Kit.Width - S(4)), Face.SansSemibold, 12, Palette.Muted, 15.6f);
        Kit.Reserve(at, Kit.Width, S(15.6f));
        Kit.Gap(8);
    }

    /// <summary>A card of rows: the card's fill and edge (or another edge), 18px in at the sides.</summary>
    private static void BeginRows(string id, Vector4? edge = null) => Kit.BeginPanel(id, 0, 18, 0, null, edge);

    /// <summary>Side: shown Auto · Left · Right; kept as 0 (auto), 1 (right), 2 (left).</summary>
    private static readonly string[] SideChoices = { "Auto", "Left", "Right" };

    private static readonly int[] SideKept = { 0, 2, 1 };

    private void DrawGeneralTab()
    {
        GroupLabel("Glamour plates");
        BeginRows("##set-plates");
        if (SwitchRow(0, "##clear", "Empty the slots a look leaves bare",
                config.ClearBare ? "So the plate is the look, exactly. Weapons are left alone unless the look has one." : "They keep what the plate had. Weapons are left alone unless the look has one.",
                config.ClearBare,
                "On, a look without, say, earrings empties the plate's earrings. Weapons are left alone unless the look has one (a plate's weapon is for its job)."))
        {
            config.ClearBare = !config.ClearBare;
            config.Save();
        }
        // (New, and not yet watched in the game: the amber tag says so.)
        if (SwitchRow(1, "##dye", "Dye pieces with the dyes in your bags",
                "When your copy has other dyes. The game spends them when you save. Off: the plugin says which to dye.", config.DyeFromBags,
                "A piece whose copy in your dresser is dyed otherwise than the look gets the look's dyes from the bottles in your bags, as dragging a dye onto it in the plate editor does. The game spends them when you save. Off, the plugin says which pieces to dye.",
                tag: "New"))
        {
            config.DyeFromBags = !config.DyeFromBags;
            config.Save();
        }
        // The panel beside Glamour Plates does it instead while it's on: dimmed, the switch as you left it.
        if (SwitchRow(2, "##open-editor", "Open with the plate editor", PlatesCompanionOn ? "Off while the panel beside Glamour Plates is on" : "The window opens when Glamour Plates does",
                config.OpenWithEditor,
                "At a glamour dresser, opening Glamour Plates opens this window too, on Home: the plate open, and the look planned for it. The panel beside Glamour Plates does this instead while it's on.",
                PlatesCompanionOn))
        {
            config.OpenWithEditor = !config.OpenWithEditor;
            config.Save();
        }
        Kit.EndPanel();

        DrawBesideGroup();

        GroupLabel("In menus and tooltips");
        BeginRows("##set-menus");
        if (SwitchRow(0, "##tooltip", "Glamour lines under item tooltips", "Which looks use a piece, its plate, a copy in the dresser, whether it's on your wishlist.", config.TooltipLines,
                "Under the game's tooltip of a piece of gear: which of your looks have it, the plates it's on, whether your dresser has a copy, and whether it's on your wishlist."))
        {
            config.TooltipLines = !config.TooltipLines;
            config.Save();
        }
        if (SwitchRow(1, "##menu", "Glamour Studio in the right-click menu", "On gear in your bags, the armoury, retainers and chat links.", config.ContextMenu,
                "Right-click a piece: add it to one of your looks, try it on with a look that has it, see which looks use it, put it on your wishlist, or store it in the armoire while that's open."))
        {
            config.ContextMenu = !config.ContextMenu;
            config.Save();
        }
        Kit.EndPanel();

        GroupLabel("Your closet on the site");
        BeginRows("##set-sync");
        const string linkFirst = "Link your account first (Account, above)";
        if (SwitchRow(0, "##auto", "Keep it up to date by itself", Linked ? "Sends what changed, every few minutes at most. Off: only when you press Send now (Closet)." : linkFirst,
                config.AutoSync,
                "What's in your glamour dresser, armoire, bags, armoury chest, saddlebags and retainers goes to your closet on Glamour Studio as it changes. Off: only when you press Send now (Closet).",
                !Linked))
        {
            config.AutoSync = !config.AutoSync;
            config.Save();
        }
        if (SwitchRow(1, "##plates", "Send my plates and outfit",
                Linked ? "For the site's Plates and Copy my game outfit, with your character's name and world (only you see them)." : linkFirst, config.SendPlates,
                "Your glamour plates and what you're wearing go along, with your character's name and world (only you see them on the site).",
                !Linked))
        {
            config.SendPlates = !config.SendPlates;
            config.Save();
        }
        if (SwitchRow(2, "##chat-notes", "Say in chat when a wish comes true", "A piece from your wishlist, seen in your wardrobe.", config.ChatNotes,
                "Pieces on your wishlist on the site come off it by themselves once the plugin sees them in your wardrobe. On, a line in chat says which."))
        {
            config.ChatNotes = !config.ChatNotes;
            config.Save();
        }
        Kit.EndPanel();

        GroupLabel("What the plugin has read");
        BeginRows("##set-read");
        var me = wardrobe.Current;
        var who = me is { Name.Length: > 0 } ? me.Name : null;
        if (ButtonRow(0, "##forget", "Forget this character",
                me == null ? "Log in to a character first" : $"What the plugin has read of {who ?? "it"} here. It's read again as you play.",
                "Forget", Btn.Danger, null, me == null, null,
                "Its dresser, armoire, saddlebags, retainers and plates as last read are forgotten (they're read again as you open them). The site keeps what it was sent until you forget the character there too (or turn it off in Settings → Account → Characters).",
                dim: me == null))
        {
            if (me != null)
                Ask("Forget this character?",
                    $"{(who != null ? who + "'s" : "Its")} dresser, armoire, saddlebags, retainers and plates as last read are forgotten here (they're read again as you open them).",
                    "Forget", "Keep",
                    () =>
                    {
                        wardrobe.Forget(me.Key);
                        Notify("Forgotten");
                    }, danger: true,
                    note: "The site keeps what it was sent until you forget the character there too, or turn it off in Account → Characters.");
        }
        Kit.EndPanel();

        DrawReportGroup();
    }

    /// <summary>Beside the game's windows: the panels on or off, their side, folded, and (unrolled) each window's panel.</summary>
    private void DrawBesideGroup()
    {
        GroupLabel("Beside the game's windows");
        BeginRows("##set-companions");
        if (SwitchRow(0, "##companions", "Show Glamour Studio beside the game's windows",
                "At the dresser, Glamour Plates, the armoire, the fitting room, Examine and the rest: what the plugin knows for that window, beside it.",
                config.Companions,
                "A small panel docked to the game's window, moving with it; this window stays closed. Fold one to a tab with its arrow."))
        {
            config.Companions = !config.Companions;
            config.Save();
        }
        if (config.Companions)
        {
            var kept = Math.Clamp(config.CompanionSide, 0, 2);
            var shown = Array.IndexOf(SideKept, kept);
            var segW = SmallSegWidth(SideChoices);
            var r = Row(1, "Side", "Auto takes the side with room.", segW, S(34));
            var picked = SmallSeg("##side", r.Control, SideChoices, shown);
            if (picked != shown)
            {
                config.CompanionSide = SideKept[picked];
                config.Save();
            }
            if (SwitchRow(2, "##folded", "Start folded", "Just the tab on the window's edge, until you open it.", config.CompanionStartFolded,
                    "A window's panel you haven't opened or folded yourself starts as its tab on the window's edge."))
            {
                config.CompanionStartFolded = !config.CompanionStartFolded;
                config.Save();
            }
            var on = 0;
            foreach (var (key, _) in CompanionList)
                if (!config.CompanionsOff.Contains(key)) on++;
            const string choose = "Choose";
            var cw = ChooseWidth(choose);
            var row = Row(3, "Each window's panel", $"{on} of {CompanionList.Length} on", cw, S(28), tip: "Glamour Dresser, Glamour Plates, the armoire and the rest: each panel on or off.");
            if (ChooseButton("##panels", row.Control, choose, panelsUnrolled)) panelsUnrolled = !panelsUnrolled;
        }
        Kit.EndPanel();

        if (!config.Companions || !panelsUnrolled) return;
        GroupLabel("Beside the game's windows · Each window's panel");
        Kit.BeginPanel("##set-companion-list", 6, 18, 6);
        var j = 0;
        foreach (var (key, name) in CompanionList)
        {
            var isOn = !config.CompanionsOff.Contains(key);
            if (SubSwitchRow(j++, $"##comp-{key}", name, isOn))
            {
                if (isOn) config.CompanionsOff.Add(key);
                else config.CompanionsOff.Remove(key);
                config.Save();
            }
        }
        Kit.EndPanel();
    }

    // ---------------------------------------------------------------------------------------------
    // Making a report: the recorder.

    private string markDraft = string.Empty;

    /// <summary>A saved recording's path as shown (from pluginConfigs on), worked out once a path.</summary>
    private (string? Path, string Shown) savedShown;

    private const string RecordTip =
        "While on, it writes down every game window that opens or closes with its words, every click's values, every event the glamour windows get, "
        + "the game's messages, and the plate editor, dresser, armoire, gearsets, Examine and what you wear as they change. It sends nothing anywhere.";

    private void DrawReportGroup()
    {
        var recording = recorder.Recording;
        var lines = recorder.LineCount;
        var stopped = !recording && lines > 0;

        GroupLabel("Making a report");
        // Recording: the one red-edged card.
        BeginRows("##set-report", recording ? Palette.DangerEdge : null);
        var i = 0;
        if (recording)
        {
            // (The recorder's own start: one begun by /gstudio record while Settings was shut is timed right too.)
            var started = recorder.StartedAt;
            var since = started != default ? DateTime.UtcNow - started : TimeSpan.Zero;
            const string stop = "Stop";
            var sw = SmWidth(stop, Icon.Square);
            var r = Row(i++, $"Recording · {Words.Count(lines, "line")} · {Model.Elapsed.Clock(since)}",
                "Before each step, say what you'll do (or /gstudio mark …): the whole state is written down with it.", sw, Kit.ButtonHeight(BtnSize.Sm), S(10), tip: RecordTip);
            // The recording dot, as any recorder has: red, ringed.
            Kit.Dl.AddCircleFilled(new Vector2(r.LeadX + S(5), r.Mid), S(5) + S(4), Palette.U32(Palette.WithAlpha(Palette.Danger, 0.18f)), 24);
            Kit.Dl.AddCircleFilled(new Vector2(r.LeadX + S(5), r.Mid), S(5), Palette.U32(Palette.Danger), 20);
            if (SmButton("##rec-stop", r.Control, stop, Btn.Glass, Icon.Square))
                Later(() => recorder.Stop());
            DrawMarkRow();
            i++;
            if (ButtonRow(i++, "##rec-dump", "Write down the windows' values",
                    "Every value the open glamour windows hold, and the whole state. Do it with the window you're asked about open.", "Write down", Btn.Glass, null, false, null,
                    "Adds every value the plate editor, the dresser, the armoire, Examine and the menus hold right now (/gstudio dump)."))
                Later(() =>
                {
                    recorder.DumpWindows();
                    Notify("Written down");
                });
        }
        else if (stopped)
        {
            const string again = "Start again";
            var aw = SmWidth(again, Icon.Play);
            var marks = recorder.Marks > 0 ? $" · Mark {recorder.Marks} written" : string.Empty;
            var r = Row(i++, $"Stopped · {Words.Count(lines, "line")}{marks}",
                recorder.SetupError ?? "Copy it with what the plugin knows, to paste in a message; or save it as a file for a long one.", aw, Kit.ButtonHeight(BtnSize.Sm), S(7),
                tip: RecordTip, noteColor: recorder.SetupError != null ? Palette.Amber : null);
            Kit.StatusDot(new Vector2(r.LeadX + S(3.5f), r.Mid), DotKind.No);
            if (SmButton("##rec-start", r.Control, again, Btn.Glass, Icon.Play, false, "Starts a new recording: this one goes, so copy or save it first"))
                Later(StartRecording);
            DrawCopySaveRow(i++);
            if (recorder.SavedTo is { } saved) DrawSavedRow(i++, saved);
        }
        else
        {
            const string start = "Start";
            var stw = DotButtonWidth(start);
            var r = Row(i++, "Record what I do",
                recorder.SetupError ?? "For the steps Koko asks you to record. It writes down what the game's glamour windows do and what the plugin does; it sends nothing anywhere.",
                stw, Kit.ButtonHeight(BtnSize.Sm), tip: RecordTip, noteColor: recorder.SetupError != null ? Palette.Amber : null);
            if (DotButton("##rec-start", r.Control, start)) Later(StartRecording);
        }
        Kit.EndPanel();
    }

    private void StartRecording() => recorder.Start();

    /// <summary>Mark a step: a small field and Mark (Enter marks too); an empty mark isn't written.</summary>
    private void DrawMarkRow()
    {
        const string mark = "Mark";
        var mw = SmWidth(mark, null);
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(62);
        Kit.Divider(at.X, at.X + w, at.Y);
        var y = at.Y + (h - S(34)) / 2;
        var fw = w - mw - S(8);
        var text = markDraft;
        if (Kit.Field("##mark-text", new Vector2(at.X, y), fw, ref text, "e.g. 2. Fill plate 3 from the armoire", null, 160, ImGuiInputTextFlags.None, 34)) markDraft = text;
        var enter = ImGui.IsItemDeactivated() && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter));
        var pressed = SmButton("##mark-btn", new Vector2(at.X + fw + S(8), y), mark, Btn.Glass, null, false,
            "Before each step, say what you'll do: the whole state is written down with it (/gstudio mark …)");
        if ((pressed || enter) && markDraft.Trim().Length > 0)
        {
            var what = markDraft;
            markDraft = string.Empty;
            Later(() =>
            {
                recorder.Mark(what);
                Notify($"Mark {recorder.Marks} written");
            });
        }
        Kit.Reserve(at, w, h);
    }

    /// <summary>Copy the recording and Save it as a file, side by side.</summary>
    private void DrawCopySaveRow(int index)
    {
        const string copy = "Copy the recording", save = "Save it as a file";
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(62);
        if (index > 0) Kit.Divider(at.X, at.X + w, at.Y);
        var y = at.Y + (h - Kit.ButtonHeight(BtnSize.Sm)) / 2;
        if (SmButton("##rec-copy", new Vector2(at.X, y), copy, Btn.Glass, Icon.Copy, false, "With what the plugin knows (its version, Dalamud's and the game's, the plugins loaded, settings, the last send), to paste in a message"))
        {
            ImGui.SetClipboardText(Diagnostics());
            recorder.Copied();
            Notify("Recording copied");
        }
        if (SmButton("##rec-save", new Vector2(at.X + SmWidth(copy, Icon.Copy) + S(8), y), save, Btn.Glass, Icon.Save, false,
                "Writes it to pluginConfigs/GlamourStudio/recordings, named by the time (/gstudio save). Its folder opens after."))
        {
            var path = recorder.Save(Diagnostics(), out var why);
            if (path != null)
            {
                Notify("Saved");
                OpenFolderOf(path);
            }
            else Frame.Warn(why ?? "Couldn't save it");
        }
        Kit.Reserve(at, w, h);
    }

    /// <summary>Where it was saved (from pluginConfigs on, in mono) and Show, which opens its folder.</summary>
    private void DrawSavedRow(int index, string saved)
    {
        if (!string.Equals(savedShown.Path, saved, StringComparison.Ordinal))
        {
            var from = saved.IndexOf("pluginConfigs", StringComparison.OrdinalIgnoreCase);
            savedShown = (saved, (from >= 0 ? saved[from..] : saved).Replace('\\', '/'));
        }
        const string show = "Show";
        var r = Row(index, "Saved", savedShown.Shown, SmWidth(show, Icon.Folder), Kit.ButtonHeight(BtnSize.Sm), S(16), mono: true);
        Icons.Draw(Kit.Dl, Icon.Check, new Vector2(r.LeadX, r.Mid - S(8)), S(16), Palette.Ice);
        if (SmButton("##rec-show", r.Control, show, Btn.Quiet, Icon.Folder, false, "Opens its folder")) OpenFolderOf(saved);
    }

    private static void OpenFolderOf(string path)
    {
        try
        {
            Dalamud.Utility.Util.OpenLink(System.IO.Path.GetDirectoryName(path)!);
        }
        catch
        {
            // The path's in the row; opening the folder is a nicety.
        }
    }

    /// <summary>The recording with what the plugin knows around it, to copy or save.</summary>
    public string Diagnostics()
    {
        var sb = new System.Text.StringBuilder();
        var me = wardrobe.Current;
        sb.AppendLine($"Glamour Studio {typeof(MainWindow).Assembly.GetName().Version} · Dalamud {typeof(Dalamud.Plugin.IDalamudPlugin).Assembly.GetName().Version} · game {Plugin.GameVersion()} · client {Services.ClientState.ClientLanguage} · {DateTime.Now:yyyy-MM-dd HH:mm}");
        try
        {
            // Plugins that touch glamour (Glamourer, Glamaholic…) can change what the game shows.
            sb.AppendLine("Plugins loaded: " + string.Join(", ", Services.PluginInterface.InstalledPlugins.Where(p => p.IsLoaded).Select(p => $"{p.InternalName} {p.Version}")));
        }
        catch
        {
            // Not needed to go on.
        }
        if (recorder.HookReport.Count > 0) sb.AppendLine("Recorder hooks: " + string.Join("; ", recorder.HookReport));
        if (recorder.SetupError != null) sb.AppendLine("Recorder: " + recorder.SetupError);
        sb.AppendLine($"Character: {(me != null ? $"{me.Name}@{me.World} (sending: {sync.StepFor(me)})" : "none")} · linked {sync.Linked} (refused {sync.TokenRefused}) · looks {sync.Library.Mine.Count} mine, {sync.Library.Loved.Count} loved, {sync.Wearable.Count} wearable, {sync.Inbox.Count} sent, {sync.Plans.Count} plans");
        sb.AppendLine($"Settings: empty bare slots {config.ClearBare}, dye from bags {config.DyeFromBags}, open with editor {config.OpenWithEditor}, auto send {config.AutoSync}, send plates {config.SendPlates}, chat notes {config.ChatNotes}");
        if (me != null)
            sb.AppendLine("Read: " + string.Join(", ", Places.ByPriority.Select(p => $"{Places.Key(p)} {(me.Get(p) is { } r ? $"{r.Items.Count} at {r.ReadAt.ToLocalTime():yyyy-MM-dd HH:mm}" : "never")}"))
                          + $", plates {(me.PlatesReadAt is { } pr ? pr.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "never")}, retainers {me.Retainers.Count}, dresser places used {DresserCleanup.Used(me.Dresser)}");
        sb.AppendLine($"Last send: {(sync.LastSync is { } r2 ? Words.Synced(r2) : "none")} {(sync.SyncProblem != null ? $"· problem: {sync.SyncProblem}" : string.Empty)}");
        sb.AppendLine();
        sb.AppendLine(recorder.AsText());
        return sb.ToString();
    }

    // ---------------------------------------------------------------------------------------------
    // Account.

    private const string TokenWarning = "Anyone with the token can read and change your closet, so keep it to yourself. Revoke it on the site if it gets out.";

    private void DrawAccountTab()
    {
        GroupLabel("Glamour Studio account");
        var refused = sync.Linked && sync.TokenRefused;
        // Refused by the site: the danger edge.
        BeginRows("##set-account", refused ? Palette.DangerEdge : null);
        if (sync.Linked)
        {
            const string unlink = "Unlink";
            var r = Row(0, refused ? "Link again" : "Linked to Glamour Studio",
                refused ? "The site doesn't know this token any more: unlink, and paste a new one" : SiteLink.Masked(config.SiteToken),
                SmWidth(unlink, null), Kit.ButtonHeight(BtnSize.Sm), S(7), mono: !refused);
            Kit.StatusDot(new Vector2(r.LeadX + S(3.5f), r.Mid), refused ? DotKind.Danger : DotKind.Ice);
            if (SmButton("##site-unlink", r.Control, unlink, Btn.Glass, null, false, "Forget the token here. Revoke it on the site too if you won't use it again."))
                Ask("Unlink from Glamour Studio?", "Forget the token here. Revoke it on the site too if you won't use it again.", "Unlink", "Keep", Unlink, danger: true);
            DrawSiteRow(1, getToken: true);
        }
        else
        {
            const string get = "Get a token";
            var r = Row(0, "Not linked", "Make a token on the site (Settings → Glamour Studio → In-game plugin) and paste it here.",
                SmWidth(get, Icon.ExternalLink), Kit.ButtonHeight(BtnSize.Sm), S(7));
            Kit.StatusDot(new Vector2(r.LeadX + S(3.5f), r.Mid), DotKind.No);
            if (SmButton("##site-get", r.Control, get, Btn.Quiet, Icon.ExternalLink, false, "Settings → Glamour Studio → In-game plugin on the site"))
                Dalamud.Utility.Util.OpenLink(SiteLink.TokensUrl);
            DrawTokenRow(1);
            DrawSiteRow(2, getToken: false);
        }
        Kit.EndPanel();
        DrawCharactersGroup();
    }

    private void Unlink()
    {
        config.SiteToken = string.Empty;
        config.Save();
        sync.Relinked();
        Notify("Unlinked from Glamour Studio");
    }

    /// <summary>The token's field and Link (the sticker: the only one in Settings); a token that can't be one says why under it, and Link waits.</summary>
    private void DrawTokenRow(int index)
    {
        const string link = "Link";
        var at = Kit.At;
        var w = Kit.Width;
        var linkW = Kit.StickerWidth(link);
        var fw = w - linkW - S(14);
        var problemH = 0f;
        if (tokenProblem != null)
        {
            RowNote[0] = new Run(tokenProblem, Face.Sans, 12, Palette.Danger);
            problemH = S(6) + Txt.Flow(Vector2.Zero, fw, RowNote, NoteLine, out _, draw: false);
        }
        var contentH = S(40) + problemH;
        var h = MathF.Max(S(62), S(11) * 2 + MathF.Max(contentH, Kit.StickerHeight()));
        if (index > 0) Kit.Divider(at.X, at.X + w, at.Y);
        var top = at.Y + (h - contentH) / 2;
        var text = tokenDraft;
        if (Kit.Field("##site-token", new Vector2(at.X, top), fw, ref text, "gsp_…", null, 128, ImGuiInputTextFlags.Password, 40, bad: tokenProblem != null, mono: true))
        {
            tokenDraft = text;
            tokenProblem = null;
        }
        if (tokenProblem != null)
        {
            RowNote[0] = new Run(tokenProblem, Face.Sans, 12, Palette.Danger);
            Txt.Flow(new Vector2(at.X, top + S(40) + S(6)), fw, RowNote, NoteLine, out _);
        }
        var empty = tokenDraft.Trim().Length == 0;
        if (Kit.Sticker("##site-link-btn", new Vector2(at.X + w - linkW, at.Y + (h - Kit.StickerHeight()) / 2), link, StickerSize.Md, null, 0,
                empty || tokenProblem != null, empty ? "Paste the token from the site first" : tokenProblem))
            LinkToken();
        Kit.Reserve(at, w, h);
    }

    /// <summary>Keep the token to yourself, with Get a token (once linked) and Open Glamour Studio, quiet.</summary>
    private static void DrawSiteRow(int index, bool getToken)
    {
        const string get = "Get a token", open = "Open Glamour Studio";
        var gw = getToken ? SmWidth(get, Icon.ExternalLink) + S(14) : 0;
        var ow = SmWidth(open, Icon.ExternalLink);
        var r = Row(index, string.Empty, TokenWarning, gw + ow, Kit.ButtonHeight(BtnSize.Sm));
        if (getToken && SmButton("##site-get-2", r.Control, get, Btn.Quiet, Icon.ExternalLink, false, "Settings → Glamour Studio → In-game plugin on the site"))
            Dalamud.Utility.Util.OpenLink(SiteLink.TokensUrl);
        if (SmButton("##site", new Vector2(r.Control.X + gw, r.Control.Y), open, Btn.Quiet, Icon.ExternalLink, false, "kokos-tools.com/glamour-studio"))
            Dalamud.Utility.Util.OpenLink(SiteLink.SiteUrl);
    }

    private void LinkToken()
    {
        var token = SiteLink.Clean(tokenDraft);
        if (!SiteLink.LooksRight(token))
        {
            tokenProblem = "That isn't a token: it starts gsp_ and is 47 letters long.";
            return;
        }
        config.SiteToken = token;
        config.Save();
        tokenDraft = string.Empty;
        tokenProblem = null;
        sync.Relinked();
        sync.SyncNow();
        sync.RefreshLibrary();
        Notify("Linked to Glamour Studio");
    }

    // ---------------------------------------------------------------------------------------------
    // Rows (the mock's .srow): at least 62 tall, 11 above and below, 14 between what's in them, a
    // hairline between rows; the name Figtree 600 13.5, its line Figtree 12 muted.

    /// <summary>A name's line box and a note's (CSS px: 13.5 × 1.3, 12 × 1.4).</summary>
    private const float NameLine = 17.5f;

    private const float NoteLine = 16.8f;

    private static readonly Run[] RowNote = new Run[1];

    /// <summary>Where a row's parts go: its control's top-left, its lead's left edge, its middle.</summary>
    private readonly record struct RowAt(Vector2 Control, float LeadX, float Mid);

    /// <summary>
    /// A row: something before the words <paramref name="leadW"/> wide (a dot, drawn by the caller at
    /// <see cref="RowAt.LeadX"/>), the name and its line (an empty name: the line alone), and room at the
    /// end for a control <paramref name="ctrlW"/> by <paramref name="ctrlH"/> (drawn by the caller at
    /// <see cref="RowAt.Control"/>). Dimmed when <paramref name="dim"/>; <paramref name="tip"/> over the words.
    /// </summary>
    private static RowAt Row(int index, string name, string? note, float ctrlW, float ctrlH, float leadW = 0, bool dim = false, string? tip = null,
        Vector4? noteColor = null, bool mono = false)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var gap = S(14);
        var wordsX = at.X + (leadW > 0 ? leadW + gap : 0);
        var wordsW = MathF.Max(S(40), at.X + w - (ctrlW > 0 ? ctrlW + gap : 0) - wordsX);
        var nameH = name.Length > 0 ? S(NameLine) : 0;
        var noteH = 0f;
        if (note != null)
        {
            if (mono) note = Txt.Fit(note, Face.Mono, 12, wordsW);
            RowNote[0] = new Run(note, mono ? Face.Mono : Face.Sans, 12, noteColor ?? Palette.Muted);
            noteH = mono ? S(NoteLine) : Txt.Flow(Vector2.Zero, wordsW, RowNote, NoteLine, out _, draw: false);
        }
        var wordsH = nameH + noteH;
        var h = MathF.Max(S(62), S(11) * 2 + MathF.Max(wordsH, ctrlH));
        var dl = Kit.Dl;
        if (index > 0) Kit.Divider(at.X, at.X + w, at.Y);
        var top = at.Y + (h - wordsH) / 2;
        var old = Palette.Opacity;
        if (dim) Palette.Opacity = old * 0.45f;
        try
        {
            if (name.Length > 0)
                Txt.Line(dl, new Vector2(wordsX, top), Txt.Fit(name, Face.SansSemibold, 14, wordsW), Face.SansSemibold, 14, Palette.Ink, NameLine);
            if (note != null)
            {
                if (mono) Txt.Line(dl, new Vector2(wordsX, top + nameH), note, Face.Mono, 12, noteColor ?? Palette.Muted, NoteLine);
                else
                {
                    RowNote[0] = new Run(note, Face.Sans, 12, noteColor ?? Palette.Muted);
                    Txt.Flow(new Vector2(wordsX, top + nameH), wordsW, RowNote, NoteLine, out _);
                }
            }
        }
        finally
        {
            Palette.Opacity = old;
        }
        if (tip != null && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(wordsX, top), new Vector2(wordsX + wordsW, top + wordsH)))
            Kit.Tooltip(tip);
        Kit.Reserve(at, w, h);
        return new RowAt(new Vector2(at.X + w - ctrlW, at.Y + (h - ctrlH) / 2), at.X, at.Y + h / 2);
    }

    /// <summary>A setting with a switch (and an amber tag before it, as New); true when it's flipped.</summary>
    private static bool SwitchRow(int index, string id, string name, string? note, bool on, string? tip = null, bool disabled = false, string? tag = null)
    {
        var tagW = tag != null ? Kit.TagWidth(tag) + S(14) : 0;
        var r = Row(index, name, note, S(40) + tagW, S(24), 0, disabled, tip);
        if (tag != null) Kit.Tag(new Vector2(r.Control.X, r.Mid - Kit.TagHeight() / 2), tag, TagKind.Amber);
        return Kit.Switch(id, new Vector2(r.Control.X + tagW, r.Mid - S(12)), on, disabled);
    }

    /// <summary>A setting with a small button at its end; true when it's pressed.</summary>
    private static bool ButtonRow(int index, string id, string name, string? note, string label, Btn style, Icon? icon, bool disabled = false,
        string? buttonTip = null, string? tip = null, bool dim = false)
    {
        var r = Row(index, name, note, SmWidth(label, icon), Kit.ButtonHeight(BtnSize.Sm), 0, dim, tip);
        return SmButton(id, r.Control, label, style, icon, disabled, buttonTip);
    }

    /// <summary>One of each window's panel's switches (the mock's .srow.sub: 40 tall, 18 in, Figtree 500 13).</summary>
    private static bool SubSwitchRow(int index, string id, string name, bool on)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(40);
        if (index > 0) Kit.Divider(at.X, at.X + w, at.Y);
        var x = at.X + S(18);
        Txt.Line(Kit.Dl, new Vector2(x, at.Y), Txt.Fit(name, Face.SansMedium, 13, w - S(18 + 14 + 40)), Face.SansMedium, 13, Palette.Ink, 40);
        var flipped = Kit.Switch(id, new Vector2(at.X + w - S(40), at.Y + (h - S(24)) / 2), on);
        Kit.Reserve(at, w, h);
        return flipped;
    }

    // ---------------------------------------------------------------------------------------------
    // Small buttons as the mock draws them in Settings.

    /// <summary>A small button's width (the mock's .btn.sm: 13 either side, Figtree 600 13, a 14px icon and 7 after it).</summary>
    private static float SmWidth(string label, Icon? icon) =>
        S(13) * 2 + Txt.Measure(label, Face.SansSemibold, 13).X + (icon != null ? S(14 + 7) : 0);

    private static bool SmButton(string id, Vector2 pos, string label, Btn style, Icon? icon, bool disabled = false, string? tip = null) =>
        Kit.Button(id, pos, label, style, BtnSize.Sm, icon, SmWidth(label, icon), disabled, tip, 0, 14);

    /// <summary>Start: the small outlined button with the recording dot (8px, red) before its words.</summary>
    private static float DotButtonWidth(string label) => S(13) * 2 + S(8 + 7) + Txt.Measure(label, Face.SansSemibold, 13).X;

    private static bool DotButton(string id, Vector2 pos, string label)
    {
        var w = DotButtonWidth(label);
        var h = Kit.ButtonHeight(BtnSize.Sm);
        // The pill (its edge, its wash when pointed at) from the Kit; the dot and the words drawn in it.
        var pressed = Kit.Button(id, pos, string.Empty, Btn.Glass, BtnSize.Sm, null, w, false, null, h);
        var dl = Kit.Dl;
        dl.AddCircleFilled(new Vector2(pos.X + S(13 + 4), pos.Y + h / 2), S(4), Palette.U32(Palette.Danger), 16);
        Txt.Line(dl, new Vector2(pos.X + S(13 + 8 + 7), pos.Y), label, Face.SansSemibold, 13, Palette.Ink, 34);
        return pressed;
    }

    /// <summary>Choose: the extra-small outlined button (28 tall, 10 either side, 12px words) with its chevron after the words.</summary>
    private static float ChooseWidth(string label) => S(10) * 2 + Txt.Measure(label, Face.SansSemibold, 12).X + S(5 + 12);

    private static bool ChooseButton(string id, Vector2 pos, string label, bool open)
    {
        var w = ChooseWidth(label);
        var h = S(28);
        var pressed = Kit.Button(id, pos, string.Empty, Btn.Glass, BtnSize.Tiny, null, w, false, null, h);
        var dl = Kit.Dl;
        var tw = Txt.Line(dl, new Vector2(pos.X + S(10), pos.Y), label, Face.SansSemibold, 12, Palette.Ink, 28);
        Icons.Draw(dl, open ? Icon.ChevronUp : Icon.ChevronDown, new Vector2(pos.X + S(10) + tw + S(5), pos.Y + (h - S(12)) / 2), S(12), Palette.Ink);
        return pressed;
    }

    // ---------------------------------------------------------------------------------------------
    // The small segmented pick (the mock's .seg.sm: 34 tall, 26px items 10 in, Figtree 600 12).

    private static readonly string[] SegIds = { "0", "1", "2", "3", "4", "5" };

    private static float SmallSegItem(string item) => S(10) * 2 + Txt.Measure(item, Face.SansSemibold, 12).X;

    private static float SmallSegWidth(string[] items)
    {
        var w = S(1 + 3) * 2 + S(2) * (items.Length - 1);
        foreach (var s in items) w += SmallSegItem(s);
        return w;
    }

    /// <summary>The small pick-one pill, the picked one the field's violet with ink words, gliding to it. Returns the one picked.</summary>
    private static int SmallSeg(string id, Vector2 pos, string[] items, int selected)
    {
        var dl = Kit.Dl;
        var w = SmallSegWidth(items);
        var h = S(34);
        dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(Palette.Card), h / 2);
        dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(Palette.Line), h / 2, ImDrawFlags.None, 1f);
        var x = pos.X + S(4);
        var y = pos.Y + S(4);
        var ih = S(26);
        if (selected >= 0 && selected < items.Length)
        {
            var px = x;
            for (var i = 0; i < selected; i++) px += SmallSegItem(items[i]) + S(2);
            var (gx, gw) = Kit.Glide(id, px - pos.X, SmallSegItem(items[selected]));
            dl.AddRectFilled(new Vector2(pos.X + gx, y), new Vector2(pos.X + gx + gw, y + ih), Palette.U32(Palette.Field), ih / 2);
        }
        var result = selected;
        ImGui.PushID(id);
        try
        {
            for (var i = 0; i < items.Length && i < SegIds.Length; i++)
            {
                var iw = SmallSegItem(items[i]);
                var p = new Vector2(x, y);
                var size = new Vector2(iw, ih);
                var on = i == selected;
                var (clicked, hovered, _) = Kit.Hit(SegIds[i], p, size);
                if (clicked) result = i;
                if (!on && hovered) dl.AddRectFilled(p, p + size, Palette.U32(Palette.AccentWash), ih / 2);
                Txt.Line(dl, new Vector2(p.X + S(10), p.Y), items[i], Face.SansSemibold, 12, on ? Palette.OnField : hovered ? Palette.Body : Palette.Muted, 26);
                Kit.FocusRing(p, p + size, ih / 2);
                x += iw + S(2);
            }
        }
        finally
        {
            ImGui.PopID();
        }
        return result;
    }
}

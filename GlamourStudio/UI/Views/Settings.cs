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
/// Settings, laid out as Housing Studio's and Raid Planner's plugins lay theirs: the back arrow at the
/// top left, the title, the tabs under it, and a card of rows, each a setting's name, one line under
/// it, and its control on the right (the longer words are its tooltip). General holds the few things
/// worth choosing; Account links the plugin to the site. The version and What's new at the foot.
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
        Kit.BeginPane(settingsTab == SettingsTab.Account ? "##settings-account" : "##settings-general", min, size, padX, 22, 0);
        var at = Kit.At;
        var picked = Kit.Seg("##settings-tab", at, SettingsTabs, (int)settingsTab, 18);
        settingsTab = (SettingsTab)picked;
        Kit.Reserve(at, Kit.Width, Kit.SegHeight);
        Kit.Gap(14);
        if (settingsTab == SettingsTab.Account) DrawAccountTab();
        else DrawGeneralTab();

        // The version and What's new at the foot (under what's above when it's taller).
        var footH = S(14) + Txt.Natural(Face.Sans, 13) + S(18);
        var room = Kit.PaneRoom() - footH;
        Kit.Gap(MathF.Max(18, room / ImGuiHelpers.GlobalScale));
        var fat = Kit.At;
        Kit.Dl.AddLine(new Vector2(fat.X, fat.Y + 0.5f), new Vector2(fat.X + Kit.Width, fat.Y + 0.5f), Palette.U32(Palette.Divider), 1f);
        Kit.Gap(14);
        var v = typeof(MainWindow).Assembly.GetName().Version;
        var runs = new[]
        {
            new Run(v == null ? "Glamour Studio · " : $"Glamour Studio {v.Major}.{v.Minor}.{v.Build} · ", Face.Sans, 13, Palette.Muted),
            new Run("What's new", Face.Sans, 13, Palette.Light, Link: "news"),
        };
        if (Txt.Para(runs, 19.5f, center: true) == "news") Dalamud.Utility.Util.OpenLink(ReleasesUrl);
        Kit.Gap(18);
        Kit.EndPane();
    }

    /// <summary>A group's label over its card ("GLAMOUR PLATES"): DM Mono 11px, .18em, muted.</summary>
    private static void GroupLabel(string text)
    {
        var at = Kit.At;
        Txt.Spaced(Kit.Dl, at, text.ToUpperInvariant(), Face.Mono, 11, Palette.Muted, 0.18f);
        Kit.Reserve(at, Kit.Width, Txt.Natural(Face.Mono, 11));
        Kit.Gap(10);
    }

    private void DrawGeneralTab()
    {
        GroupLabel("Glamour plates");
        Kit.BeginPanel("##set-plates", 0, 0, 0);
        var i = 0;
        if (SwitchRow(i++, "##clear", "Empty the slots a look leaves bare", config.ClearBare ? "So the plate is the look, exactly" : "They keep what the plate had",
                config.ClearBare,
                "On, a look without, say, earrings empties the plate's earrings. Weapons are left alone unless the look has one (a plate's weapon is for its job)."))
        {
            config.ClearBare = !config.ClearBare;
            config.Save();
        }
        if (SwitchRow(i++, "##dye", "Dye pieces with the dyes in your bags", "When your copy has other dyes (new: tell us how it goes)", config.DyeFromBags,
                "A piece whose copy in your dresser is dyed otherwise than the look gets the look's dyes from the bottles in your bags, as dragging a dye onto it in the plate editor does. The game spends them when you save. Off, the plugin says which pieces to dye."))
        {
            config.DyeFromBags = !config.DyeFromBags;
            config.Save();
        }
        if (SwitchRow(i, "##open-editor", "Open with the plate editor", "The window opens when Glamour Plates does", config.OpenWithEditor,
                "At a glamour dresser, opening Glamour Plates opens this window too, on your looks."))
        {
            config.OpenWithEditor = !config.OpenWithEditor;
            config.Save();
        }
        Kit.EndPanel();

        Kit.Gap(20);
        GroupLabel("Your closet on the site");
        Kit.BeginPanel("##set-sync", 0, 0, 0);
        i = 0;
        if (SwitchRow(i++, "##auto", "Keep it up to date by itself", "Sends what changed, every few minutes at most", config.AutoSync,
                "What's in your glamour dresser, armoire, bags, armoury chest, saddlebags and retainers goes to your closet on Glamour Studio as it changes. Off: only when you press Send it now (Home → Closet).",
                !Linked))
        {
            config.AutoSync = !config.AutoSync;
            config.Save();
        }
        if (SwitchRow(i++, "##plates", "Send my plates and outfit", "For the site's Plates and Copy my outfit from the game", config.SendPlates,
                "Your glamour plates and what you're wearing go along, with your character's name and world (only you see them on the site).",
                !Linked))
        {
            config.SendPlates = !config.SendPlates;
            config.Save();
        }
        if (SwitchRow(i++, "##chat-notes", "Say in chat when a wish comes true", "A piece from your shopping list, seen in your wardrobe", config.ChatNotes,
                "Pieces on your shopping list on the site come off it by themselves once the plugin sees them in your wardrobe. On, a line in chat says which."))
        {
            config.ChatNotes = !config.ChatNotes;
            config.Save();
        }
        if (ButtonRow(i, "##forget", "Forget this character", "What the plugin has read of it, here", "Forget", Btn.Glass, wardrobe.Current == null, null,
                "Its dresser, armoire, saddlebags, retainers and plates as last read are forgotten (they're read again as you open them). The site keeps what it was sent until you forget the character there too."))
        {
            var me = wardrobe.Current;
            if (me != null)
                Ask("Forget this character?", $"What the plugin has read of {(me.Name.Length > 0 ? me.Name : "it")} is forgotten here. It's read again as you play.", "Forget", "Keep",
                    () =>
                    {
                        wardrobe.Forget(me.Key);
                        Notify("Forgotten");
                    }, danger: true);
        }
        Kit.EndPanel();

        Kit.Gap(20);
        DrawReportGroup();
    }

    // ---------------------------------------------------------------------------------------------
    // Making a report: the recorder.

    private string markDraft = string.Empty;

    private void DrawReportGroup()
    {
        GroupLabel("Making a report");
        Kit.BeginPanel("##set-report", 0, 0, 0);
        var i = 0;
        var recording = recorder.Recording;
        var note = recording
            ? $"Recording · {Words.Count(recorder.LineCount, "line")}, {Words.Count(recorder.Marks, "mark")} so far"
            : recorder.SetupError ?? (recorder.LineCount > 0 ? $"Stopped · {Words.Count(recorder.LineCount, "line")} to copy or save" : "For the steps Koko asks you to record");
        if (ButtonRow(i++, recording ? "##rec-stop" : "##rec-start", "Record what I do", note, recording ? "Stop" : "Start", recording ? Btn.Danger : Btn.Glass, false, null,
                "While on, it writes down every game window that opens or closes with its words, every click's values, every event the glamour windows get, "
                + "the game's messages, and the plate editor, dresser, armoire, gearsets, Examine and what you wear as they change. It sends nothing anywhere."))
            Later(() =>
            {
                if (recorder.Recording) recorder.Stop();
                else recorder.Start();
            });
        if (recording)
        {
            // A mark before each step: what you're about to do.
            const string mark = "Mark";
            var mw = Kit.ButtonWidth(mark, BtnSize.Sm, Icon.Bookmark, Btn.Primary);
            SetRow(i++, "Mark a step", "Before each step, say what you'll do (or /gstudio mark …): the whole state is written down with it", Kit.Width - S(36), S(36), p =>
            {
                var fw = Kit.Width - S(36) - mw - S(8);
                var text = markDraft;
                if (Kit.Field("##mark-text", p, fw, ref text, "e.g. 2. Fill plate 3 from the armoire", Icon.Bookmark, 160)) markDraft = text;
                var enter = ImGui.IsItemDeactivated() && ImGui.IsKeyPressed(ImGuiKey.Enter);
                if (Kit.Button("##mark-btn", new Vector2(p.X + fw + S(8), p.Y + (S(36) - Kit.ButtonHeight(BtnSize.Sm)) / 2), mark, Btn.Primary, BtnSize.Sm, Icon.Bookmark) || enter)
                {
                    var what = markDraft;
                    markDraft = string.Empty;
                    Later(() =>
                    {
                        recorder.Mark(what);
                        Notify($"Mark {recorder.Marks} written");
                    });
                }
            }, below: true);
            if (ButtonRow(i++, "##rec-dump", "Write down the windows' values", "Every value the open glamour windows hold, and the whole state", "Write down", Btn.Glass, false, null,
                    "Adds every value the plate editor, the dresser, the armoire, Examine and the menus hold right now. Do it with the window you're asked about open."))
                Later(() =>
                {
                    recorder.DumpWindows();
                    Notify("Written down");
                });
        }
        if (ButtonRow(i++, "##rec-copy", "Copy the recording", "With what the plugin knows, to paste in a message", "Copy", Btn.Glass, recorder.LineCount == 0))
        {
            ImGui.SetClipboardText(Diagnostics());
            Notify("Recording copied");
        }
        var saved = recorder.SavedTo;
        if (ButtonRow(i, "##rec-save", "Save it as a file", saved != null ? $"Saved: {saved}" : "For a long one: a text file in the plugin's folder", "Save", Btn.Glass, recorder.LineCount == 0, null,
                "Writes it to pluginConfigs/GlamourStudio/recordings, named by the time. The file opens in its folder after."))
        {
            var path = recorder.Save(Diagnostics(), out var why);
            if (path != null)
            {
                Notify("Saved");
                try
                {
                    Dalamud.Utility.Util.OpenLink(System.IO.Path.GetDirectoryName(path)!);
                }
                catch
                {
                    // The path's in the row; opening the folder is a nicety.
                }
            }
            else Notify(why ?? "Couldn't save it");
        }
        Kit.EndPanel();
    }

    /// <summary>The recording with what the plugin knows around it, to copy or save.</summary>
    public string Diagnostics()
    {
        var sb = new System.Text.StringBuilder();
        var me = wardrobe.Current;
        sb.AppendLine($"Glamour Studio {typeof(MainWindow).Assembly.GetName().Version} · client {Services.ClientState.ClientLanguage} · {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Character: {(me != null ? $"{me.Name}@{me.World}" : "none")} · linked {sync.Linked} (refused {sync.TokenRefused}) · looks {sync.Library.Mine.Count} mine, {sync.Library.Loved.Count} loved, {sync.Wearable.Count} wearable, {sync.Inbox.Count} sent, {sync.Plans.Count} plans");
        sb.AppendLine($"Settings: empty bare slots {config.ClearBare}, dye from bags {config.DyeFromBags}, open with editor {config.OpenWithEditor}, auto send {config.AutoSync}, send plates {config.SendPlates}, chat notes {config.ChatNotes}");
        if (me != null)
            sb.AppendLine("Read: " + string.Join(", ", Places.ByPriority.Select(p => $"{Places.Key(p)} {(me.Get(p) is { } r ? $"{r.Items.Count} at {r.ReadAt:yyyy-MM-dd HH:mm}" : "never")}"))
                          + $", plates {(me.PlatesReadAt is { } pr ? pr.ToString("yyyy-MM-dd HH:mm") : "never")}, retainers {me.Retainers.Count}, dresser places used {DresserCleanup.Used(me.Dresser)}");
        sb.AppendLine($"Last send: {(sync.LastSync is { } r2 ? Words.Synced(r2) : "none")} {(sync.SyncProblem != null ? $"· problem: {sync.SyncProblem}" : string.Empty)}");
        sb.AppendLine();
        sb.AppendLine(recorder.AsText());
        return sb.ToString();
    }

    private void DrawAccountTab()
    {
        GroupLabel("Glamour Studio account");
        Kit.BeginPanel("##set-account", 18, 18, 18);
        var at = Kit.At;
        var w = Kit.Width;
        var dl = Kit.Dl;
        // Linked or not, in a line.
        var (state, color, line) = !sync.Linked ? ("Not linked", Palette.Faint, "Make a token on the site (Settings → Glamour Studio → In-game plugin) and paste it here.")
            : sync.TokenRefused ? ("Link again", Palette.Danger, "The site doesn't know this token any more: unlink, and paste a new one")
            : ("Linked", Palette.Success, "Your closet goes to your account on the site");
        var lh = Txt.Natural(Face.SansMedium, 14);
        Kit.Dot(new Vector2(at.X + S(4), at.Y + lh / 2), color, 8);
        var sw = Txt.Draw(dl, new Vector2(at.X + S(18), at.Y), state, Face.SansMedium, 14, Palette.Ink);
        var nx = at.X + S(18) + sw + S(10);
        var nh = Txt.Flow(new Vector2(nx, at.Y + (lh - Txt.Natural(Face.Sans, 12)) / 2), at.X + w - nx, new[] { new Run(line, Face.Sans, 12, Palette.Muted) }, 18, out _);
        Kit.Reserve(at, w, MathF.Max(lh, nh));
        Kit.Gap(14);

        // The token: masked once linked, with Unlink; else a field and Link.
        var row = Kit.At;
        if (sync.Linked)
        {
            const string unlink = "Unlink";
            var uw = Kit.ButtonWidth(unlink, BtnSize.Sm);
            var fw = w - uw - S(8);
            var fh = S(40);
            dl.AddRectFilled(row, row + new Vector2(fw, fh), Palette.U32(Palette.Field), S(12));
            dl.AddRect(row + new Vector2(0.5f), row + new Vector2(fw, fh) - new Vector2(0.5f), Palette.U32(Palette.LineStrong), S(12), ImDrawFlags.None, 1f);
            Txt.Line(dl, row + new Vector2(S(12), 0), Txt.Fit(SiteLink.Masked(config.SiteToken), Face.Mono, 12, fw - S(24)), Face.Mono, 12, Palette.Muted, 40);
            if (Kit.Button("##site-unlink", new Vector2(row.X + fw + S(8), row.Y + (fh - Kit.ButtonHeight(BtnSize.Sm)) / 2), unlink, Btn.Glass, BtnSize.Sm, null, 0, false,
                    "Forget the token here. Revoke it on the site too if you won't use it again."))
            {
                config.SiteToken = string.Empty;
                config.Save();
                sync.Relinked();
                Notify("Unlinked from Glamour Studio");
            }
            Kit.Reserve(row, w, fh);
        }
        else
        {
            const string link = "Link";
            var linkW = Kit.ButtonWidth(link, BtnSize.Md, null, Btn.Primary);
            var fieldW = w - linkW - S(8);
            var text = tokenDraft;
            if (Kit.Field("##site-token", new Vector2(row.X, row.Y + S(2)), fieldW, ref text, "gsp_…", Icon.Link, 128, ImGuiInputTextFlags.Password))
            {
                tokenDraft = text;
                tokenProblem = null;
            }
            if (Kit.Button("##site-link-btn", new Vector2(row.X + fieldW + S(8), row.Y), link, Btn.Primary, BtnSize.Md, null, 0, tokenDraft.Trim().Length == 0))
                LinkToken();
            Kit.Reserve(row, w, S(40));
            if (tokenProblem != null)
            {
                Kit.Gap(8);
                Txt.Para(new[] { new Run(tokenProblem, Face.Sans, 12, Palette.Danger) }, 18);
            }
        }
        Kit.Gap(14);
        var brow = Kit.At;
        const string get = "Get a token";
        if (Kit.Button("##site-get", brow, get, Btn.Glass, BtnSize.Sm, Icon.ExternalLink, 0, false, "Settings → Glamour Studio → In-game plugin on the site"))
            Dalamud.Utility.Util.OpenLink(SiteLink.TokensUrl);
        var gx = brow.X + Kit.ButtonWidth(get, BtnSize.Sm, Icon.ExternalLink) + S(8);
        if (Kit.Button("##site", new Vector2(gx, brow.Y), "Open Glamour Studio", Btn.Quiet, BtnSize.Sm, null, 0, false, "kokos-tools.com/glamour-studio"))
            Dalamud.Utility.Util.OpenLink(SiteLink.SiteUrl);
        Kit.Reserve(brow, w, Kit.ButtonHeight(BtnSize.Sm));
        Kit.EndPanel();
        Kit.Gap(14);
        Txt.Para(new[] { new Run("Anyone with the token can read and change your closet, so keep it to yourself. Revoke it on the site if it gets out.", Face.Sans, 12, Palette.Muted) }, 18);
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
    // Rows.

    private static readonly Run[] SetNote = new Run[1];

    /// <summary>
    /// A setting's row (12px 16px inside, a hairline between): its name (with an icon before it when
    /// given) and one line under it, the control on the right; the longer words are the tooltip over
    /// the words. <paramref name="below"/> puts the control under the words instead, for wide ones.
    /// </summary>
    private static void SetRow(int index, string name, string? note, float controlW, float controlH, Action<Vector2> control, bool below = false,
        string? tip = null, bool disabled = false, Icon? icon = null)
    {
        var at = Kit.At;
        var w = Kit.Width;
        if (index > 0) Kit.Dl.AddLine(new Vector2(at.X + 1, at.Y - 0.5f), new Vector2(at.X + w - 1, at.Y - 0.5f), Palette.U32(Palette.LineFaint), 1f);
        var x = at.X + S(18);
        var wordsW = below || controlW <= 0 ? w - S(36) : w - S(36) - controlW - S(16);
        var nameH = Txt.Natural(Face.SansMedium, 14);
        if (note != null) SetNote[0] = new Run(note, Face.Sans, 12, Palette.Muted);
        var noteH = note != null ? Txt.Flow(Vector2.Zero, wordsW, SetNote, 18, out _, draw: false) : 0;
        var wordsH = nameH + (note != null ? S(3) + noteH : 0);
        var h = below ? S(14) * 2 + wordsH + (controlH > 0 ? S(10) + controlH : 0) : S(14) * 2 + MathF.Max(wordsH, controlH);
        var dl = Kit.Dl;
        var top = below ? at.Y + S(14) : at.Y + (h - wordsH) / 2;
        var old = Palette.Opacity;
        if (disabled) Palette.Opacity = old * 0.5f;
        var nx = x;
        if (icon is { } ic)
        {
            Icons.Draw(dl, ic, new Vector2(nx, top + (nameH - S(14)) / 2), S(14), Palette.Rose);
            nx += S(14 + 6);
        }
        Txt.Line(dl, new Vector2(nx, top), Txt.Fit(name, Face.SansMedium, 14, x + wordsW - nx), Face.SansMedium, 14, Palette.Ink);
        if (note != null)
        {
            SetNote[0] = new Run(note, Face.Sans, 12, Palette.Muted);
            Txt.Flow(new Vector2(x, top + nameH + S(3)), wordsW, SetNote, 18, out _);
        }
        Palette.Opacity = old;
        if (tip != null && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(x, top), new Vector2(x + wordsW, top + wordsH)))
            Kit.Tooltip(tip);
        if (below) control(new Vector2(x, top + wordsH + S(10)));
        else if (controlW > 0) control(new Vector2(at.X + w - S(18) - controlW, at.Y + (h - controlH) / 2));
        Kit.Reserve(at, w, h);
    }

    /// <summary>A setting with a switch; true when it's flipped.</summary>
    private static bool SwitchRow(int index, string id, string name, string? note, bool on, string? tip = null, bool disabled = false)
    {
        var flipped = false;
        SetRow(index, name, note, S(44), S(24), p => flipped = Kit.Switch(id, p, on, disabled), false, tip, disabled);
        return flipped;
    }

    /// <summary>A setting with a − value + stepper; returns −1, 0 or +1.</summary>
    private static int StepperRow(int index, string id, string name, string? note, string value, bool canDown, bool canUp, string? tip = null)
    {
        var delta = 0;
        SetRow(index, name, note, Kit.StepperWidth(value), S(36), p => delta = Kit.Stepper(id, p, value, canDown, canUp), false, tip);
        return delta;
    }

    /// <summary>A setting with a small button on the right; true when it's pressed.</summary>
    private static bool ButtonRow(int index, string id, string name, string? note, string label, Btn style = Btn.Glass, bool disabled = false,
        string? buttonTip = null, string? tip = null)
    {
        var pressed = false;
        SetRow(index, name, note, Kit.ButtonWidth(label, BtnSize.Sm, null, style), Kit.ButtonHeight(BtnSize.Sm),
            p => pressed = Kit.Button(id, p, label, style, BtnSize.Sm, null, 0, disabled, buttonTip), false, tip);
        return pressed;
    }
}

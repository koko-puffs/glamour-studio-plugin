using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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

    private void DrawSettings()
    {
        Frame.BeginHeader(20);
        if (ScreenHeader("Back", null, "Settings"))
            screen = settingsFrom switch
            {
                Screen.Look when openLook != null => Screen.Look,
                Screen.Plate when openPlate >= 0 => Screen.Plate,
                _ => Screen.Home,
            };
        Kit.Gap(16);
        settingsTab = (SettingsTab)Kit.Tabs("##settings-tab", SettingsTabs, (int)settingsTab);
        Frame.EndHeader();

        Frame.BeginBody(settingsTab == SettingsTab.Account ? "##settings-account" : "##settings-general", null, 18, 22);
        if (settingsTab == SettingsTab.Account) DrawAccountTab();
        else DrawGeneralTab();

        Frame.BeginBottom("settings-bottom");
        var v = typeof(MainWindow).Assembly.GetName().Version;
        var runs = new[]
        {
            new Run(v == null ? "Glamour Studio · " : $"Glamour Studio {v.Major}.{v.Minor}.{v.Build} · ", Face.Sans, 13, Palette.Faint),
            new Run("What's new", Face.SansMedium, 13, Palette.Light, Link: "news"),
        };
        if (Txt.Para(runs, 19.5f, center: true) == "news") Dalamud.Utility.Util.OpenLink(ReleasesUrl);
        Frame.EndBottom();
        Frame.EndBody();
    }

    private void DrawGeneralTab()
    {
        Kit.EyebrowRow("Glamour plates", Palette.Muted);
        Kit.Gap(10);
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

        Kit.Gap(22);
        Kit.EyebrowRow("Your closet on the site", Palette.Muted);
        Kit.Gap(10);
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
    }

    private void DrawAccountTab()
    {
        Kit.BeginPanel("##set-account", 0, 0, 0);
        DrawAccountRow(0);
        if (ButtonRow(1, "##site", "Glamour Studio", "Dress the mannequin, save looks, find what to wear", "Open", Btn.Glass, false, null,
                "kokos-tools.com/glamour-studio"))
            Dalamud.Utility.Util.OpenLink(SiteLink.SiteUrl);
        Kit.EndPanel();
        Kit.Gap(14);
        Txt.Note("Anyone with the token can read and change your closet, so keep it to yourself. Revoke it on the site if it gets out.", Palette.Faint);
    }

    private void DrawAccountRow(int index)
    {
        const string name = "Glamour Studio account";
        if (sync.Linked)
        {
            const string unlink = "Unlink";
            var note = sync.TokenRefused ? "The site doesn't know this token any more: unlink, and link a new one" : $"Linked · {SiteLink.Masked(config.SiteToken)}";
            SetRow(index, name, note, Kit.ButtonWidth(unlink, BtnSize.Tiny, null, Btn.Glass), Kit.ButtonHeight(BtnSize.Tiny), p =>
            {
                if (Kit.Button("##site-unlink", p, unlink, Btn.Glass, BtnSize.Tiny, null, 0, false,
                        "Forget the token here. Revoke it on the site too if you won't use it again."))
                {
                    config.SiteToken = string.Empty;
                    config.Save();
                    sync.Relinked();
                    Notify("Unlinked from Glamour Studio");
                }
            }, tip: "Your closet goes to your account on the site, and your looks come from it.", icon: Icon.Link);
            return;
        }
        var width = Kit.Width - S(28);
        const string link = "Link";
        const string get = "Get a token";
        var linkW = Kit.ButtonWidth(link, BtnSize.Md, null, Btn.Primary);
        var getW = Kit.ButtonWidth(get, BtnSize.Md, Icon.ExternalLink, Btn.Glass);
        SetRow(index, name, tokenProblem ?? "Not linked yet. Make a token on the site (Settings → Glamour Studio → In-game plugin) and paste it here.", width, S(36), p =>
        {
            var fieldW = MathF.Max(S(120), width - linkW - getW - S(16));
            var text = tokenDraft;
            if (Kit.Field("##site-token", p, fieldW, ref text, "gsp_…", Icon.Link, 128, ImGuiInputTextFlags.Password))
            {
                tokenDraft = text;
                tokenProblem = null;
            }
            var x = p.X + fieldW + S(8);
            if (Kit.Button("##site-link-btn", new Vector2(x, p.Y), link, Btn.Primary, BtnSize.Md, null, 0, tokenDraft.Trim().Length == 0))
                LinkToken();
            if (Kit.Button("##site-get", new Vector2(x + linkW + S(8), p.Y), get, Btn.Glass, BtnSize.Md, Icon.ExternalLink))
                Dalamud.Utility.Util.OpenLink(SiteLink.TokensUrl);
        }, below: true,
            tip: "Linking keeps your closet on Glamour Studio up to date and brings your looks here. The token is made on the site, shown once, and can be revoked there.",
            icon: Icon.Link);
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
        if (index > 0) Kit.Dl.AddLine(new Vector2(at.X + 1, at.Y - 0.5f), new Vector2(at.X + w - 1, at.Y - 0.5f), Palette.U32(Palette.Line), 1f);
        var x = at.X + S(16);
        var wordsW = below || controlW <= 0 ? w - S(32) : w - S(32) - controlW - S(14);
        var nameH = Txt.Natural(Face.SansMedium, 14);
        if (note != null) SetNote[0] = new Run(note, Face.Sans, 12, Palette.Muted);
        var noteH = note != null ? Txt.Flow(Vector2.Zero, wordsW, SetNote, 18, out _, draw: false) : 0;
        var wordsH = nameH + (note != null ? S(2) + noteH : 0);
        var h = below ? S(13) * 2 + wordsH + (controlH > 0 ? S(10) + controlH : 0) : S(13) * 2 + MathF.Max(wordsH, controlH);
        var dl = Kit.Dl;
        var top = below ? at.Y + S(13) : at.Y + (h - wordsH) / 2;
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
            Txt.Flow(new Vector2(x, top + nameH + S(2)), wordsW, SetNote, 18, out _);
        }
        Palette.Opacity = old;
        if (tip != null && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(x, top), new Vector2(x + wordsW, top + wordsH)))
            Kit.Tooltip(tip);
        if (below) control(new Vector2(x, top + wordsH + S(10)));
        else if (controlW > 0) control(new Vector2(at.X + w - S(16) - controlW, at.Y + (h - controlH) / 2));
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
        SetRow(index, name, note, Kit.ButtonWidth(label, BtnSize.Tiny, null, style), Kit.ButtonHeight(BtnSize.Tiny),
            p => pressed = Kit.Button(id, p, label, style, BtnSize.Tiny, null, 0, disabled, buttonTip), false, tip);
        return pressed;
    }
}

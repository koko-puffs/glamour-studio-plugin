using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GlamourStudio.Core;
using GlamourStudio.Game;
using GlamourStudio.Session;
using GlamourStudio.UI;

namespace GlamourStudio;

/// <summary>
/// Glamour Studio: your wardrobe on Koko's Glamour Studio, and its looks on your glamour plates.
/// /gstudio opens the window; /gstudio settings opens Settings; /gstudio send sends your closet to
/// the site now.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/gstudio";

    private readonly Configuration config;
    private readonly GameData data;
    private readonly WardrobeReader wardrobe;
    private readonly SiteSync sync;
    private readonly Fonts fonts;
    private readonly Shots shots;
    private readonly Recorder recorder;
    private readonly WindowSystem windows = new("GlamourStudio");
    private readonly MainWindow window;

    /// <summary>Whether the plate editor was open last frame (to open the window as it opens).</summary>
    private bool editorWasOpen;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        config = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        try
        {
            data = new GameData();
            wardrobe = new WardrobeReader(data);
            sync = new SiteSync(config, wardrobe);
            fonts = new Fonts();
            shots = new Shots(sync.Http, () => config.SiteToken);
            recorder = new Recorder(data)
            {
                Header = RecordingHeader,
                PluginState = () =>
                    $"linked {sync.Linked} (refused {sync.TokenRefused}), syncing {sync.Syncing}, last sync {(sync.LastSyncAt is { } at ? at.ToLocalTime().ToString("HH:mm:ss") : "never")}{(sync.SyncProblem != null ? $" problem \"{sync.SyncProblem}\"" : string.Empty)}, "
                    + $"wardrobe v{wardrobe.Version} dresser live {wardrobe.DresserLive} armoire live {wardrobe.ArmoireLive}, shot {(shots.Capturing ? "capturing" : shots.Taken is { } t ? (t.Sending ? "sending" : t.Sent ? "sent" : "taken") : "none")}, {window?.CompanionState() ?? "no panel"}",
            };
            window = new MainWindow(config, data, wardrobe, sync, fonts, shots, recorder);
            // A shot says which character wore it, when the site takes that character.
            shots.WhoWore = () => sync.Sends(wardrobe.Current) ? wardrobe.Current!.Key : null;
            shots.TokenRefused = sync.TokenWasRefused;
            // The site's calls, as SITE lines in a recording (any thread; no token, no body).
            Core.SiteLink.Trace = Recorder.Raw;
            windows.AddWindow(window);

            Services.Commands.AddHandler(Command, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Glamour Studio. /gstudio settings opens Settings; /gstudio send sends your closet now; /gstudio refresh reads it all again; "
                              + "/gstudio record starts or stops a recording for a report; /gstudio mark <what you'll do> notes a step in it; /gstudio dump writes down the glamour windows' values; /gstudio save saves it as a file.",
            });
            Services.PluginInterface.UiBuilder.Draw += windows.Draw;
            Services.PluginInterface.UiBuilder.Draw += window.DrawCompanions;
            Services.PluginInterface.UiBuilder.Draw += window.DrawTooltipLines;
            Services.PluginInterface.UiBuilder.Draw += window.DrawCharacterQuestion;
            Services.ContextMenu.OnMenuOpened += window.OnContextMenu;
            Services.PluginInterface.UiBuilder.OpenMainUi += window.ShowMain;
            Services.PluginInterface.UiBuilder.OpenConfigUi += window.ShowSettings;
            Services.Framework.Update += OnUpdate;
            sync.WishesGot += OnWishesGot;
        }
        catch
        {
            // Dalamud doesn't dispose a plugin whose constructor threw: what was hooked into the game is let go of here.
            Dispose();
            throw;
        }
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        var space = trimmed.IndexOf(' ');
        var verb = (space < 0 ? trimmed : trimmed[..space]).ToLowerInvariant();
        var rest = space < 0 ? string.Empty : trimmed[(space + 1)..].Trim();
        switch (verb)
        {
            case "record":
                if (recorder.Recording) recorder.Stop();
                else recorder.Start();
                Services.Chat.Print(recorder.Recording
                    ? "Glamour Studio: recording. Before each step: /gstudio mark <what you'll do>. /gstudio record again stops it."
                    : $"Glamour Studio: recording stopped ({recorder.LineCount:N0} lines). /gstudio save saves it as a file; Settings → Making a report copies it.");
                return;
            case "mark":
            case "dump":
                if (!recorder.Recording)
                {
                    // A stopped recording not yet saved isn't wiped by starting a new one.
                    if (recorder.LineCount > 0 && !recorder.HandedOver)
                    {
                        Services.Chat.Print("Glamour Studio: the last recording isn't saved yet. /gstudio save (or copy it in Settings), then /gstudio record to start a new one.");
                        return;
                    }
                    recorder.Start();
                    Services.Chat.Print("Glamour Studio: recording started.");
                }
                if (verb == "mark")
                {
                    recorder.Mark(rest);
                    Services.Chat.Print($"Glamour Studio: mark {recorder.Marks}{(rest.Length > 0 ? $": {rest}" : string.Empty)}");
                }
                else
                {
                    recorder.DumpWindows();
                    Services.Chat.Print("Glamour Studio: the glamour windows' values are written down.");
                }
                return;
            case "save":
                if (recorder.LineCount == 0)
                {
                    Services.Chat.Print("Glamour Studio: nothing recorded yet (/gstudio record).");
                    return;
                }
                var path = recorder.Save(window.Diagnostics(), out var why);
                Services.Chat.Print(path != null ? $"Glamour Studio: saved to {path}" : $"Glamour Studio: {why}");
                return;
        }
        switch (verb)
        {
            case "settings":
            case "config":
                window.ShowSettings();
                break;
            case "refresh":
            case "reload":
                window.RefreshEverything(s => Services.Chat.Print($"Glamour Studio: {s}."));
                break;
            case "send":
            case "sync":
                if (sync.Linked) sync.SyncNow();
                else window.ShowSettings();
                break;
            default:
                window.Toggle();
                break;
        }
    }

    private void OnUpdate(IFramework framework)
    {
        // Each on its own: one failing never stops the others (the shot's tick shows the HUD again).
        Step(() => wardrobe.Tick(), "reading");
        Step(() => sync.Tick(window.IsOpen || window.CompanionShown), "the site");
        Step(shots.Tick, "the shot");
        Step(recorder.Tick, "the recording");
        Step(FittingRoom.Tick, "the fitting room");
        Step(OutfitStorer.Tick, "storing outfits");
        Step(DresserTaker.Tick, "taking out duplicates");
        Step(PlateEditor.Tick, "applying a plate");
        Step(() =>
        {
            var editor = PlateEditor.IsOpen;
            // (Not while the panel beside Glamour Plates is on: it's there instead.)
            if (editor && !editorWasOpen && config.OpenWithEditor && !window.PlatesCompanionOn && !window.IsOpen) window.ShowMain();
            editorWasOpen = editor;
        }, "the plate editor");
    }

    /// <summary>Pieces from your wishlist the plugin has just seen in your wardrobe: a line in chat.</summary>
    private void OnWishesGot(System.Collections.Generic.IReadOnlyList<uint> items)
    {
        if (!config.ChatNotes || items.Count == 0) return;
        var names = Core.Words.List(items.Take(4).Select(data.ItemName));
        var more = items.Count > 4 ? $" and {items.Count - 4} more" : string.Empty;
        Services.Chat.Print($"Glamour Studio: {names}{more} came off your wishlist.");
    }

    /// <summary>Per step: when its next failure may be logged.</summary>
    private readonly System.Collections.Generic.Dictionary<string, DateTime> quietUntil = new();

    private void Step(Action step, string what)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            // Said once every ten seconds at most, should it fail every frame.
            if (quietUntil.TryGetValue(what, out var until) && DateTime.UtcNow < until) return;
            quietUntil[what] = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            Services.Log.Warning(ex, $"Glamour Studio's update failed ({what})");
        }
    }

    public void Dispose()
    {
        Safely(() => Services.Framework.Update -= OnUpdate);
        if (sync != null) Safely(() => sync.WishesGot -= OnWishesGot);
        Safely(() => Services.PluginInterface.UiBuilder.Draw -= windows.Draw);
        if (window != null)
        {
            Safely(() => Services.PluginInterface.UiBuilder.Draw -= window.DrawCompanions);
            Safely(() => Services.PluginInterface.UiBuilder.Draw -= window.DrawTooltipLines);
            Safely(() => Services.PluginInterface.UiBuilder.Draw -= window.DrawCharacterQuestion);
            Safely(() => Services.ContextMenu.OnMenuOpened -= window.OnContextMenu);
            Safely(() => Services.PluginInterface.UiBuilder.OpenMainUi -= window.ShowMain);
            Safely(() => Services.PluginInterface.UiBuilder.OpenConfigUi -= window.ShowSettings);
        }
        Safely(() => Services.Commands.RemoveHandler(Command));
        Safely(windows.RemoveAllWindows);
        Safely(() => Core.SiteLink.Trace = null);
        Safely(FittingRoom.Stop);
        Safely(() => OutfitStorer.Stop("The plugin closed", now: true));
        Safely(() => DresserTaker.Stop("The plugin closed", now: true));
        Safely(() => recorder?.Dispose());
        Safely(() => shots?.Dispose());
        Safely(() => sync?.Dispose());
        Safely(() => wardrobe?.Flush());
        Safely(() => fonts?.Dispose());
    }

    /// <summary>A recording's first lines: the plugin, the character, the settings, what's been read (its times local, as the recording's start is: they're kept in UTC).</summary>
    private IEnumerable<string> RecordingHeader()
    {
        var me = wardrobe.Current;
        yield return $"Character {(me != null ? $"{me.Name}@{me.World} key {me.Key} sending {sync.StepFor(me)}" : "none")}, linked {sync.Linked}, Dalamud {typeof(IDalamudPlugin).Assembly.GetName().Version}, game {GameVersion()}";
        yield return $"Settings: empty bare slots {config.ClearBare}, dye from bags {config.DyeFromBags}, open with editor {config.OpenWithEditor}, auto send {config.AutoSync}, send plates {config.SendPlates}, chat notes {config.ChatNotes}";
        yield return me == null ? "Read: nothing" : "Read: " + string.Join(", ", Places.ByPriority.Select(p => $"{Places.Key(p)} {(me.Get(p) is { } r ? $"{r.Items.Count} at {r.ReadAt.ToLocalTime():HH:mm}" : "never")}"))
                                                     + $", plates {(me.PlatesReadAt is { } pr ? pr.ToLocalTime().ToString("HH:mm") : "never")}, retainers {me.Retainers.Count}";
    }

    /// <summary>The game's version (its ffxiv repository's), or "?".</summary>
    internal static string GameVersion()
    {
        try
        {
            return Services.Data.GameData.Repositories.TryGetValue("ffxiv", out var repo) ? repo.Version ?? "?" : "?";
        }
        catch
        {
            return "?";
        }
    }

    private static void Safely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't let go of something while unloading");
        }
    }
}

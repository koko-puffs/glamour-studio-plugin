using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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
            window = new MainWindow(config, data, wardrobe, sync, fonts, shots);
            windows.AddWindow(window);

            Services.Commands.AddHandler(Command, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Glamour Studio. /gstudio settings opens Settings; /gstudio send sends your closet to the site now.",
            });
            Services.PluginInterface.UiBuilder.Draw += windows.Draw;
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
        switch (args.Trim().ToLowerInvariant())
        {
            case "settings":
            case "config":
                window.ShowSettings();
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
        Step(() => sync.Tick(window.IsOpen), "the site");
        Step(shots.Tick, "the shot");
        Step(() =>
        {
            var editor = PlateEditor.IsOpen;
            if (editor && !editorWasOpen && config.OpenWithEditor && !window.IsOpen) window.ShowMain();
            editorWasOpen = editor;
        }, "the plate editor");
    }

    /// <summary>Pieces from your shopping list the plugin has just seen in your wardrobe: a line in chat.</summary>
    private void OnWishesGot(System.Collections.Generic.IReadOnlyList<uint> items)
    {
        if (!config.ChatNotes || items.Count == 0) return;
        var names = Core.Words.List(items.Take(4).Select(data.ItemName));
        var more = items.Count > 4 ? $" and {items.Count - 4} more" : string.Empty;
        Services.Chat.Print($"Glamour Studio: {names}{more} came off your shopping list.");
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
            Safely(() => Services.PluginInterface.UiBuilder.OpenMainUi -= window.ShowMain);
            Safely(() => Services.PluginInterface.UiBuilder.OpenConfigUi -= window.ShowSettings);
        }
        Safely(() => Services.Commands.RemoveHandler(Command));
        Safely(windows.RemoveAllWindows);
        Safely(() => shots?.Dispose());
        Safely(() => sync?.Dispose());
        Safely(() => wardrobe?.Flush());
        Safely(() => fonts?.Dispose());
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

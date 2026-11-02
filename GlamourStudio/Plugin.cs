using System;
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
            window = new MainWindow(config, data, wardrobe, sync, fonts);
            windows.AddWindow(window);

            Services.Commands.AddHandler(Command, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Glamour Studio. /gstudio settings opens Settings; /gstudio send sends your closet to the site now.",
            });
            Services.PluginInterface.UiBuilder.Draw += windows.Draw;
            Services.PluginInterface.UiBuilder.OpenMainUi += window.ShowMain;
            Services.PluginInterface.UiBuilder.OpenConfigUi += window.ShowSettings;
            Services.Framework.Update += OnUpdate;
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
        try
        {
            wardrobe.Tick();
            sync.Tick(window.IsOpen);
            var editor = PlateEditor.IsOpen;
            if (editor && !editorWasOpen && config.OpenWithEditor && !window.IsOpen) window.ShowMain();
            editorWasOpen = editor;
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio's update failed");
        }
    }

    public void Dispose()
    {
        Safely(() => Services.Framework.Update -= OnUpdate);
        Safely(() => Services.PluginInterface.UiBuilder.Draw -= windows.Draw);
        if (window != null)
        {
            Safely(() => Services.PluginInterface.UiBuilder.OpenMainUi -= window.ShowMain);
            Safely(() => Services.PluginInterface.UiBuilder.OpenConfigUi -= window.ShowSettings);
        }
        Safely(() => Services.Commands.RemoveHandler(Command));
        Safely(windows.RemoveAllWindows);
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

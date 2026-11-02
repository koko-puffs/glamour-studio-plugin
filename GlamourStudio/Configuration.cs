using System;
using Dalamud.Configuration;

namespace GlamourStudio;

/// <summary>The plugin's settings, kept by Dalamud with the plugin's config.</summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>The token from the site (Settings → Glamour Studio → In-game plugin). Never logged.</summary>
    public string SiteToken { get; set; } = string.Empty;

    /// <summary>Keep your closet on the site up to date by itself (when something changes, at most every few minutes).</summary>
    public bool AutoSync { get; set; } = true;

    /// <summary>Send your glamour plates and what you're wearing too.</summary>
    public bool SendPlates { get; set; } = true;

    /// <summary>Empty the armour and accessory slots a look leaves bare when it goes on a plate.</summary>
    public bool ClearBare { get; set; } = true;

    /// <summary>Dye pieces whose copy has other dyes with the dyes in your bags (new: not yet watched in the game).</summary>
    public bool DyeFromBags { get; set; }

    /// <summary>Open the window by itself when the plate editor opens.</summary>
    public bool OpenWithEditor { get; set; } = true;

    /// <summary>A line in chat when pieces from your shopping list are seen in your wardrobe (and come off it).</summary>
    public bool ChatNotes { get; set; } = true;

    public void Save() => Services.PluginInterface.SavePluginConfig(this);
}

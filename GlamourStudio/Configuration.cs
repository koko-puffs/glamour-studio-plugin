using System;
using System.Collections.Generic;
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

    /// <summary>A small panel beside the game's glamour windows (the dresser, Glamour Plates and the rest), moving with the window.</summary>
    public bool Companions { get; set; } = true;

    /// <summary>Which side of the game window the panel takes: 0 the side with room, 1 right, 2 left.</summary>
    public int CompanionSide { get; set; }

    /// <summary>A window's panel starts as its tab, until it's opened.</summary>
    public bool CompanionStartFolded { get; set; }

    /// <summary>The panels turned off, by their keys ("dresser", "plates"…).</summary>
    public HashSet<string> CompanionsOff { get; set; } = new();

    /// <summary>Each window's panel folded or open, as you left it (by key: true folded).</summary>
    public Dictionary<string, bool> CompanionFolded { get; set; } = new();

    /// <summary>Lines under the game's item tooltips: which of your looks use the piece, its plate, a copy in the dresser.</summary>
    public bool TooltipLines { get; set; } = true;

    /// <summary>A Glamour Studio entry in the right-click menu on gear.</summary>
    public bool ContextMenu { get; set; } = true;

    public void Save() => Services.PluginInterface.SavePluginConfig(this);
}

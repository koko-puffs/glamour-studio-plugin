using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace GlamourStudio.Core;

/// <summary>
/// Whether the site takes a character's data, as the site keeps it (<c>gs_game_characters.send</c>):
/// <see cref="Ask"/> nobody's said yet (an older plugin sent it without asking, or the site's never
/// heard of it), <see cref="Yes"/>, <see cref="No"/>.
/// </summary>
public enum SendChoice
{
    Ask,
    Yes,
    No,
}

/// <summary>What to do about the character you're on.</summary>
public enum ConsentStep
{
    /// <summary>The site takes it: send as you play.</summary>
    Send,

    /// <summary>No: nothing of it is sent.</summary>
    Hold,

    /// <summary>You haven't said: ask, and send nothing until you do.</summary>
    AskPlayer,

    /// <summary>You said yes here, the site doesn't know yet: tell it, then send.</summary>
    TellYes,

    /// <summary>You said no here, the site doesn't know yet: tell it (it lets go of what it had).</summary>
    TellNo,

    /// <summary>What the site has isn't known yet (not read, or not linked): wait, sending nothing.</summary>
    Wait,
}

/// <summary>A character you answered for, as kept in the plugin's settings (its name and world to list it).</summary>
[Serializable]
public sealed class KnownCharacter
{
    public string Name { get; set; } = string.Empty;

    public string World { get; set; } = string.Empty;

    /// <summary>Your answer: true send it, false don't, null not asked yet.</summary>
    public bool? Send { get; set; }

    public DateTime? AnsweredAt { get; set; }

    /// <summary>
    /// Your answer here the site hasn't confirmed yet: it stands over what the site had (nothing of the
    /// character is sent meanwhile) and is told again until the site has it. Kept, so a game closed before
    /// then doesn't let the site's old answer undo it.
    /// </summary>
    public bool Pending { get; set; }
}

/// <summary>
/// The plugin asks you on each character it hasn't been told about whether to send its data to the site,
/// and sends nothing for it until you say yes. Your answer is kept here (per character, by its key) and on
/// the site (which turns away a character you said no to, whatever plugin asks); the site's word wins,
/// since you can change it there too (Settings → Glamour Studio → Characters), once it has your latest
/// answer here (until then, yours stands). Game-free, tested.
/// </summary>
public static class Consent
{
    public static SendChoice Parse(string? s) => s switch
    {
        "yes" => SendChoice.Yes,
        "no" => SendChoice.No,
        _ => SendChoice.Ask,
    };

    public static string Key(SendChoice c) => c switch
    {
        SendChoice.Yes => "yes",
        SendChoice.No => "no",
        _ => "ask",
    };

    /// <summary>
    /// What to do for a character: <paramref name="mine"/> is your answer here (null: not asked), and
    /// <paramref name="site"/> what the site has (null: not read yet; <see cref="SendChoice.Ask"/>: it has
    /// nothing, or nobody said). The site's yes or no wins; else your answer is told to it; else you're asked.
    /// </summary>
    public static ConsentStep Decide(bool? mine, SendChoice? site)
    {
        if (site == SendChoice.Yes) return ConsentStep.Send;
        if (site == SendChoice.No) return ConsentStep.Hold;
        if (site == null) return mine == false ? ConsentStep.Hold : ConsentStep.Wait;
        return mine switch
        {
            true => ConsentStep.TellYes,
            false => ConsentStep.TellNo,
            _ => ConsentStep.AskPlayer,
        };
    }

    /// <summary><c>gs_plugin_characters</c>' answer: each character's key and what the site does with it; null when it isn't one.</summary>
    public static Dictionary<string, SendChoice>? ReadCharacters(JsonNode? node)
    {
        if (node is not JsonArray list) return null;
        var result = new Dictionary<string, SendChoice>(StringComparer.Ordinal);
        foreach (var e in list.OfType<JsonObject>())
        {
            if (e["key"] is not JsonValue k || !k.TryGetValue<string>(out var key) || string.IsNullOrEmpty(key)) continue;
            var send = e["send"] is JsonValue s && s.TryGetValue<string>(out var t) ? t : null;
            result[key] = Parse(send);
        }
        return result;
    }

    /// <summary><c>gs_plugin_character</c>'s arguments (the token added by the caller).</summary>
    public static JsonObject CharacterArgs(string key, string name, string world, bool send) => new()
    {
        ["p_character"] = new JsonObject { ["key"] = key, ["name"] = send ? name : string.Empty, ["world"] = send ? world : string.Empty },
        ["p_send"] = send,
    };

    /// <summary>The question's words for a character.</summary>
    public static string Question(string name) => $"Send {(name.Length > 0 ? name : "this character")}'s wardrobe to Glamour Studio?";

    /// <summary>The question's eyebrow: "A new character on Lich".</summary>
    public static string Eyebrow(string world) => world.Length > 0 ? $"A new character on {world}" : "A new character";

    public const string QuestionBody =
        "Its glamour dresser, armoire, bags, armoury chest, saddlebags and retainers, its glamour plates and what it wears go to your closet on the site, "
        + "with its name and world. It stays private: only you see it, unless you choose to show it on the site (Settings → Glamour Studio → Characters). "
        + "Nothing of it is sent until you say yes; you can change your mind in Settings → Account.";
}

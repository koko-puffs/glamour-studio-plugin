using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace GlamourStudio.Core;

/// <summary>
/// How your character looks, for the site's dressing room to draw it as its mannequin ("Your
/// character" in its Character menu): the game's 26 customize bytes as they are (race, clan, sex,
/// height, face, hairstyle, the facial features, the bust, the tail…), and the colours they pick
/// out of the character creator's palettes (<c>human.cmp</c>), as <c>#rrggbb</c>, since the site
/// hasn't those palettes. Sent with what you're wearing (<c>p_outfit.appearance</c>), so only for a
/// character whose data the site takes.
/// </summary>
public static class Appearance
{
    /// <summary>The game's file of the character creator's colours.</summary>
    public const string HumanCmp = "chara/xls/charamake/human.cmp";

    /// <summary>How many customize bytes there are.</summary>
    public const int Length = 26;

    // The bytes (as Glamourer's CustomizeIndex names them).
    private const int Sex = 1, Clan = 4, Highlights = 7, Skin = 8, EyeRight = 9, Hair = 10, HighlightColour = 11,
        FeatureColour = 13, EyeLeft = 15, Mouth = 19, LipColour = 20;

    // human.cmp: colours of 4 bytes (red, green, blue, alpha). The shared palettes first (the eyes'
    // at 0, the highlights' at 256, the dark lip colours at 512, the dark face paint's at 640, the
    // facial features' at 768, the light lip colours at 1024, the light face paint's at 1152; then
    // unused ones to 2304, and the same again for the character creator's buttons to 4608), then a
    // chunk of 1280 for each clan and sex (♂ first), its skin's palette at 768 and its hair's at
    // 1024. As Penumbra.GameData's CmpData lays it out (Glamourer reads it so) and the site's
    // extraction reads it. (Up to 0.6.15 the light lips were read at 1792, one of the unused
    // palettes: yellow lips; and the features' colour from the eyes' palette.)
    private const int Shared = 4608, Chunk = 1280, SkinAt = 768, HairAt = 1024;
    private const int EyesAt = 0, HighlightsAt = 256, DarkLipsAt = 512, FeaturesAt = 768, LightLipsAt = 1024;

    /// <summary>
    /// The colours the customize bytes pick, by name (<c>skin</c>, <c>hair</c>, <c>eyes</c>,
    /// <c>eyesLeft</c>, <c>feature</c>, and <c>highlights</c> and <c>lips</c> when they're on), or
    /// none where the bytes or the file are too short.
    /// </summary>
    public static Dictionary<string, string> Colours(byte[]? customize, byte[]? cmp)
    {
        var o = new Dictionary<string, string>();
        if (customize is not { Length: >= Length } || cmp == null) return o;
        var clan = customize[Clan];
        if (clan is >= 1 and <= 16)
        {
            var chunk = Shared + ((clan - 1) * 2 + (customize[Sex] == 1 ? 1 : 0)) * Chunk;
            Put(o, "skin", cmp, chunk + SkinAt + customize[Skin]);
            Put(o, "hair", cmp, chunk + HairAt + customize[Hair]);
        }
        Put(o, "eyes", cmp, EyesAt + customize[EyeRight]);
        Put(o, "eyesLeft", cmp, EyesAt + customize[EyeLeft]);
        Put(o, "feature", cmp, FeaturesAt + customize[FeatureColour]);
        if ((customize[Highlights] & 0x80) != 0) Put(o, "highlights", cmp, HighlightsAt + customize[HighlightColour]);
        // (Lipstick: the mouth's top bit; the colour's 0–95 dark, 128 and up light.)
        if ((customize[Mouth] & 0x80) != 0)
        {
            var lip = customize[LipColour];
            Put(o, "lips", cmp, lip >= 128 ? LightLipsAt + (lip - 128) : DarkLipsAt + lip);
        }
        return o;
    }

    private static void Put(Dictionary<string, string> o, string name, byte[] cmp, int colour)
    {
        var at = colour * 4;
        if (colour < 0 || at + 3 > cmp.Length) return;
        o[name] = $"#{cmp[at]:x2}{cmp[at + 1]:x2}{cmp[at + 2]:x2}";
    }

    /// <summary>What's sent (<c>{customize: [26 numbers], colours: {…}}</c>), or null when it isn't known.</summary>
    public static JsonObject? Json(byte[]? customize, IReadOnlyDictionary<string, string>? colours)
    {
        if (customize is not { Length: >= Length }) return null;
        var o = new JsonObject
        {
            ["customize"] = new JsonArray(customize.Take(Length).Select(b => (JsonNode)(int)b).ToArray()),
        };
        if (colours is { Count: > 0 })
        {
            var c = new JsonObject();
            foreach (var (k, v) in colours.OrderBy(kv => kv.Key, StringComparer.Ordinal)) c[k] = v;
            o["colours"] = c;
        }
        return o;
    }
}

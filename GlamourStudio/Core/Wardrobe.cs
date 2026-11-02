using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace GlamourStudio.Core;

/// <summary>Where you keep a piece, as the site's closet names it (<c>gs_closet.place</c>).</summary>
public enum Place
{
    Dresser,
    Armoire,
    Armoury,
    Inventory,
    Saddlebag,
    Retainer,
}

/// <summary>The places by name, and the order a piece found in two is kept by (the handiest first).</summary>
public static class Places
{
    /// <summary>The handiest first: a piece in the dresser and on a retainer is "in the dresser".</summary>
    public static readonly Place[] ByPriority = { Place.Dresser, Place.Armoire, Place.Armoury, Place.Inventory, Place.Saddlebag, Place.Retainer };

    public static string Key(Place p) => p switch
    {
        Place.Dresser => "dresser",
        Place.Armoire => "armoire",
        Place.Armoury => "armoury",
        Place.Inventory => "inventory",
        Place.Saddlebag => "saddlebag",
        _ => "retainer",
    };

    /// <summary>"Glamour dresser", "Armoire"… as a list names them.</summary>
    public static string Word(Place p) => p switch
    {
        Place.Dresser => "Glamour dresser",
        Place.Armoire => "Armoire",
        Place.Armoury => "Armoury chest",
        Place.Inventory => "Bags",
        Place.Saddlebag => "Saddlebags",
        _ => "Retainers",
    };

    /// <summary>A piece's whereabouts in a sentence: "in your armoury chest".</summary>
    public static string Where(Place p) => p switch
    {
        Place.Dresser => "in your glamour dresser",
        Place.Armoire => "in your armoire",
        Place.Armoury => "in your armoury chest",
        Place.Inventory => "in your bags",
        Place.Saddlebag => "in your saddlebags",
        _ => "on a retainer",
    };

    /// <summary>How to let the plugin see a place it hasn't read yet.</summary>
    public static string HowToRead(Place p) => p switch
    {
        Place.Dresser => "Open your glamour dresser once (at an inn, or a dresser in a house)",
        Place.Armoire => "Open your armoire once (at an inn)",
        Place.Saddlebag => "Open your chocobo saddlebags once",
        Place.Retainer => "Open each retainer's bags once",
        _ => "Log in: it's read by itself",
    };

    public static int Rank(Place p) => Array.IndexOf(ByPriority, p);
}

/// <summary>A piece in the glamour dresser: its place in the dresser (0 to 799), the item and its dyes; an outfit set's piece knows its set.</summary>
/// <param name="SetItem">The outfit set's own item when this is one of its pieces (it's set by the set's place).</param>
public readonly record struct DresserPiece(int Index, uint Item, byte Dye1, byte Dye2, uint SetItem = 0)
{
    public bool InSet => SetItem != 0;
}

/// <summary>
/// One place as last read: when, and the pieces in it (the item ids, normal quality). A place never
/// read is null in a <see cref="CharacterWardrobe"/>, which is not the same as read and empty.
/// </summary>
public sealed class PlaceRead
{
    public DateTime ReadAt { get; set; }

    public HashSet<uint> Items { get; set; } = new();
}

/// <summary>One plate as last read: its number (1 to 20) and its pieces (none: empty).</summary>
public sealed class PlateRead
{
    public int Plate { get; set; }

    public Dictionary<Slot, Piece> Pieces { get; set; } = new();
}

/// <summary>
/// What the plugin knows of one character's wardrobe, kept between sessions (the game only shows the
/// dresser, armoire, saddlebags and retainers once you've opened them): each place as last read, the
/// dresser's pieces with their dyes, the plates and what they wore. Retainers are kept one by one
/// (their bags are only seen while one is open) and count together as one place.
/// </summary>
public sealed class CharacterWardrobe
{
    /// <summary>The character's key (<see cref="Wardrobe.KeyOf"/>), never the game's id.</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string World { get; set; } = string.Empty;

    public Body Body { get; set; }

    /// <summary>Each place but the retainers, as last read.</summary>
    public Dictionary<Place, PlaceRead> Read { get; set; } = new();

    /// <summary>Each retainer as last seen, by its id (as text: a JSON key).</summary>
    public Dictionary<string, PlaceRead> Retainers { get; set; } = new();

    /// <summary>The dresser's pieces with their dyes (for a plate's dyes); the item ids are in <see cref="Read"/> too.</summary>
    public List<DresserPiece> Dresser { get; set; } = new();

    /// <summary>The plates as last read (an empty list: never read).</summary>
    public List<PlateRead> Plates { get; set; } = new();

    public DateTime? PlatesReadAt { get; set; }

    /// <summary>What they wear (their glamour, as seen), last time it was looked at.</summary>
    public Dictionary<Slot, Piece> Outfit { get; set; } = new();

    public DateTime? OutfitReadAt { get; set; }

    /// <summary>The retainers merged into one place, kept until <see cref="RetainersChanged"/>.</summary>
    private PlaceRead? retainers;

    /// <summary>Say so when <see cref="Retainers"/> changed (its merged place is made again).</summary>
    public void RetainersChanged() => retainers = null;

    /// <summary>A place as one read (the retainers together: every one seen), or null when it was never read.</summary>
    public PlaceRead? Get(Place place)
    {
        if (place != Place.Retainer) return Read.TryGetValue(place, out var r) ? r : null;
        if (Retainers.Count == 0) return null;
        return retainers ??= new PlaceRead
        {
            ReadAt = Retainers.Values.Max(r => r.ReadAt),
            Items = Retainers.Values.SelectMany(r => r.Items).ToHashSet(),
        };
    }

    /// <summary>Where a piece is, the handiest place first; null when it's in none read.</summary>
    public Place? Where(uint item)
    {
        var id = LookJson.BaseId(item);
        foreach (var p in Places.ByPriority)
            if (Get(p)?.Items.Contains(id) == true)
                return p;
        return null;
    }
}

/// <summary>Every character's wardrobe, as kept in the plugin's folder (wardrobe.json).</summary>
public sealed class Wardrobe
{
    /// <summary>2 since 0.1.1 (0.1.0's saddlebags read before they were opened are dropped on loading).</summary>
    public int Version { get; set; } = 2;

    public Dictionary<string, CharacterWardrobe> Characters { get; set; } = new();

    /// <summary>
    /// A character's key: from its id, so the site can tell your characters apart without ever being
    /// told the id (SHA-256 of it, the first 16 bytes in base64url: 22 letters).
    /// </summary>
    public static string KeyOf(ulong contentId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"glamour-studio:{contentId}"));
        return Convert.ToBase64String(hash, 0, 16).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// The closet the site is sent: every piece of every character's (or just <paramref name="only"/>'s)
    /// as one list, each piece once, in its handiest place; and the places read in full (any character's).
    /// The site keeps 5000 pieces: the handiest go first.
    /// </summary>
    public static (List<(uint Item, Place Place)> Pieces, List<Place> Read) Closet(IEnumerable<CharacterWardrobe> characters, string? only = null)
    {
        var best = new Dictionary<uint, Place>();
        var read = new HashSet<Place>();
        foreach (var c in characters)
        {
            if (only != null && c.Key != only) continue;
            foreach (var place in Places.ByPriority)
            {
                if (c.Get(place) is not { } r) continue;
                read.Add(place);
                foreach (var raw in r.Items)
                {
                    var id = LookJson.BaseId(raw);
                    if (id == 0) continue;
                    if (!best.TryGetValue(id, out var was) || Places.Rank(place) < Places.Rank(was)) best[id] = place;
                }
            }
        }
        var pieces = best.Select(kv => (kv.Key, kv.Value))
            .OrderBy(p => Places.Rank(p.Value)).ThenBy(p => p.Key)
            .Take(5000)
            .ToList();
        return (pieces, Places.ByPriority.Where(read.Contains).ToList());
    }

    /// <summary>What changes what's sent, as one short text: to send only when something did.</summary>
    public static string Fingerprint(IReadOnlyList<(uint Item, Place Place)> closet, IReadOnlyList<Place> read, CharacterWardrobe? character)
    {
        var sb = new StringBuilder();
        foreach (var p in read) sb.Append(Places.Key(p)).Append(',');
        sb.Append('|');
        foreach (var (item, place) in closet.OrderBy(p => p.Item)) sb.Append(item).Append(':').Append((int)place).Append(';');
        if (character != null)
        {
            sb.Append('|').Append(character.Key).Append(character.Name).Append(character.World);
            foreach (var plate in character.Plates.OrderBy(p => p.Plate))
                sb.Append('#').Append(plate.Plate).Append(LookJson.Write(plate.Pieces).ToJsonString());
            sb.Append('|').Append(LookJson.Write(character.Outfit).ToJsonString()).Append(character.Body);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>The call's arguments for <c>gs_plugin_sync</c> (the token added by the caller).</summary>
    public static JsonObject SyncArgs(IReadOnlyList<(uint Item, Place Place)> closet, IReadOnlyList<Place> read, CharacterWardrobe? character)
    {
        var args = new JsonObject
        {
            ["p_closet"] = new JsonArray(closet.Select(p => (JsonNode)new JsonObject { ["item"] = p.Item, ["place"] = Places.Key(p.Place) }).ToArray()),
            ["p_places"] = new JsonArray(read.Select(p => (JsonNode)Places.Key(p)).ToArray()),
        };
        if (character != null)
        {
            args["p_character"] = new JsonObject { ["key"] = character.Key, ["name"] = character.Name, ["world"] = character.World };
            if (character.PlatesReadAt != null)
                args["p_plates"] = new JsonArray(character.Plates.Where(p => p.Pieces.Count > 0 && p.Plate is >= 1 and <= Plates.Count)
                    .OrderBy(p => p.Plate)
                    .Select(p => (JsonNode)new JsonObject { ["plate"] = p.Plate, ["pieces"] = LookJson.Write(p.Pieces) }).ToArray());
            if (character.OutfitReadAt != null)
                args["p_outfit"] = new JsonObject { ["body"] = LookJson.BodyKey(character.Body), ["pieces"] = LookJson.Write(character.Outfit) };
        }
        return args;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GlamourStudio.Core;

/// <summary>
/// A piece of an outfit: the game's item id (its normal-quality id: a high-quality copy is the same
/// piece) and its two dyes (the Stain sheet's ids, 0 for none).
/// </summary>
public readonly record struct Piece(uint Item, byte Dye1 = 0, byte Dye2 = 0)
{
    public bool Dyed => Dye1 != 0 || Dye2 != 0;

    /// <summary>The same item with these dyes.</summary>
    public Piece WithDyes(byte d1, byte d2) => this with { Dye1 = d1, Dye2 = d2 };
}

/// <summary>The site's two mannequins (a look's <c>body</c>).</summary>
public enum Body
{
    Feminine,
    Masculine,
}

/// <summary>
/// A look from Glamour Studio, as the plugin reads it (the site's <c>gs_plugin_look_json</c>): what to
/// show, and its pieces by slot.
/// </summary>
/// <param name="Mine">Your own look (else one you love, or one sent to you).</param>
/// <param name="Maker">Who made it, as the site names them.</param>
public sealed record Look(
    string Id,
    string Name,
    Body Body,
    IReadOnlyDictionary<Slot, Piece> Pieces,
    string Visibility,
    bool Mine,
    string Maker,
    int Loves,
    DateTimeOffset? UpdatedAt,
    string? Mood = null,
    string? Job = null)
{
    public int Count => Pieces.Count;
}

/// <summary>A look sent from the site to the game (<c>gs_plugin_inbox_list</c>), with the plate it was meant for.</summary>
public sealed record SentLook(string Id, int? Plate, DateTimeOffset CreatedAt, Look Look);

/// <summary>A look planned on the site for one of a character's plates (<c>gs_plugin_plans</c>).</summary>
public sealed record PlannedPlate(int Plate, Look Look);

/// <summary>Your looks and the ones you love (<c>gs_plugin_looks</c>), and what's on your shopping list.</summary>
public sealed record Library(IReadOnlyList<Look> Mine, IReadOnlyList<Look> Loved, IReadOnlySet<uint> Wishes)
{
    public static readonly Library Empty = new(Array.Empty<Look>(), Array.Empty<Look>(), new HashSet<uint>());
}

/// <summary>Reading looks from the site's JSON, and writing pieces for it.</summary>
public static class LookJson
{
    /// <summary>A high-quality copy's id is its item's plus a million (the game's way).</summary>
    public const uint HqOffset = 1_000_000;

    /// <summary>An item id as the site keeps it: the normal-quality id (a collectable's and an HQ copy's offsets taken off).</summary>
    public static uint BaseId(uint id) => id % 500_000;

    public static string BodyKey(Body body) => body == Body.Masculine ? "masculine" : "feminine";

    public static Body ReadBody(string? s) => s == "masculine" ? Body.Masculine : Body.Feminine;

    /// <summary>Pieces as the site keeps them: <c>{"body": {"item": 123, "dyes": [5, 0]}, …}</c>.</summary>
    public static JsonObject Write(IReadOnlyDictionary<Slot, Piece> pieces)
    {
        var o = new JsonObject();
        foreach (var slot in Slots.All)
        {
            if (!pieces.TryGetValue(slot, out var p) || p.Item == 0) continue;
            o[Slots.Key(slot)] = new JsonObject
            {
                ["item"] = BaseId(p.Item),
                ["dyes"] = new JsonArray(p.Dye1, p.Dye2),
            };
        }
        return o;
    }

    /// <summary>Pieces from the site's JSON; slots, items or dyes it can't read are left out (a dye past the game's 255 is none).</summary>
    public static Dictionary<Slot, Piece> ReadPieces(JsonNode? node)
    {
        var pieces = new Dictionary<Slot, Piece>();
        if (node is not JsonObject o) return pieces;
        foreach (var (key, value) in o)
        {
            if (!Slots.TryParse(key, out var slot) || value is not JsonObject p) continue;
            var item = Long(p["item"]);
            if (item is not (>= 1 and <= 9_999_999)) continue;
            byte d1 = 0, d2 = 0;
            if (p["dyes"] is JsonArray dyes)
            {
                d1 = Dye(dyes.Count > 0 ? dyes[0] : null);
                d2 = Dye(dyes.Count > 1 ? dyes[1] : null);
            }
            pieces[slot] = new Piece((uint)item, d1, d2);
        }
        return pieces;
    }

    /// <summary>An item id as the site writes it (a number, or one in a string), normal quality; null when it isn't one.</summary>
    public static uint? ItemId(JsonNode? n) => Long(n) is { } id and >= 1 and <= 9_999_999 ? BaseId((uint)id) : null;

    private static byte Dye(JsonNode? n) => Long(n) is { } d and >= 0 and <= 255 ? (byte)d : (byte)0;

    private static long? Long(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<double>(out var d) && double.IsFinite(d) && Math.Floor(d) == d) return (long)d;
        if (v.TryGetValue<string>(out var s) && long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var p)) return p;
        return null;
    }

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static DateTimeOffset? Time(JsonNode? n) =>
        Str(n) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : null;

    private static readonly Regex Uuid = new(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.CultureInvariant);

    public static bool IsUuid(string? id) => id != null && Uuid.IsMatch(id);

    /// <summary>A name as the site shows it, its spaces tidied; <paramref name="otherwise"/> when blank.</summary>
    public static string Named(JsonNode? n, string otherwise)
    {
        var s = Regex.Replace(Str(n) ?? string.Empty, @"\s+", " ").Trim();
        return s.Length == 0 ? otherwise : s;
    }

    /// <summary>One look; null when it has no id the site would give.</summary>
    public static Look? ReadLook(JsonNode? node)
    {
        if (node is not JsonObject o || Str(o["id"]) is not { } id || !IsUuid(id)) return null;
        var loves = Long(o["loves"]) ?? 0;
        return new Look(
            id,
            Named(o["name"], "Untitled look"),
            ReadBody(Str(o["body"])),
            ReadPieces(o["pieces"]),
            Str(o["visibility"]) ?? "private",
            o["mine"] is JsonValue m && m.TryGetValue<bool>(out var mine) && mine,
            Named(o["maker"], "Someone"),
            (int)Math.Clamp(loves, 0, int.MaxValue),
            Time(o["updated_at"]),
            Str(o["mood"]),
            Str(o["job"]));
    }

    /// <summary>The plugin's library: your looks, the ones you love, your shopping list. Null when it isn't one.</summary>
    public static Library? ReadLibrary(JsonNode? node)
    {
        if (node is not JsonObject o) return null;
        var mine = (o["mine"] as JsonArray)?.Select(ReadLook).OfType<Look>().ToList() ?? new List<Look>();
        var loved = (o["loved"] as JsonArray)?.Select(ReadLook).OfType<Look>().ToList() ?? new List<Look>();
        var wishes = new HashSet<uint>();
        if (o["wishes"] is JsonArray w)
            foreach (var n in w)
                if (ItemId(n) is { } id)
                    wishes.Add(id);
        return new Library(mine, loved, wishes);
    }

    /// <summary>A list of looks (<c>gs_plugin_wearable</c>); ones it can't read are left out. Null when it isn't a list.</summary>
    public static List<Look>? ReadLooks(JsonNode? node)
    {
        if (node is null) return new List<Look>();
        return node is JsonArray rows ? rows.Select(ReadLook).OfType<Look>().ToList() : null;
    }

    /// <summary>A character's plate plans, by plate; rows it can't read are left out. Null when it isn't a list.</summary>
    public static List<PlannedPlate>? ReadPlans(JsonNode? node)
    {
        if (node is null) return new List<PlannedPlate>();
        if (node is not JsonArray rows) return null;
        var list = new List<PlannedPlate>();
        foreach (var r in rows.OfType<JsonObject>())
            if (Long(r["plate"]) is { } p and >= 1 and <= Plates.Count && ReadLook(r["look"]) is { } look && list.All(x => x.Plate != p))
                list.Add(new PlannedPlate((int)p, look));
        return list.OrderBy(p => p.Plate).ToList();
    }

    /// <summary>What's waiting from the site, newest first; rows it can't read are left out. Null when it isn't a list.</summary>
    public static List<SentLook>? ReadInbox(JsonNode? node)
    {
        if (node is null) return new List<SentLook>();
        if (node is not JsonArray rows) return null;
        var list = new List<SentLook>();
        foreach (var r in rows.OfType<JsonObject>())
        {
            var id = Str(r["id"]);
            if (!IsUuid(id) || ReadLook(r["look"]) is not { } look) continue;
            var plate = Long(r["plate"]) is { } p and >= 1 and <= Plates.Count ? (int?)p : null;
            list.Add(new SentLook(id!, plate, Time(r["created_at"]) ?? DateTimeOffset.MinValue, look));
        }
        return list.OrderByDescending(s => s.CreatedAt).ToList();
    }
}

/// <summary>The game's glamour plates.</summary>
public static class Plates
{
    /// <summary>How many plates the game has.</summary>
    public const int Count = 20;

    /// <summary>A plate's pieces the same as a look's (item and dyes, slot by slot)?</summary>
    public static bool Same(IReadOnlyDictionary<Slot, Piece> a, IReadOnlyDictionary<Slot, Piece> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var p)
                                         && LookJson.BaseId(p.Item) == LookJson.BaseId(kv.Value.Item)
                                         && p.Dye1 == kv.Value.Dye1 && p.Dye2 == kv.Value.Dye2);

    /// <summary>The look a plate is (its pieces exactly a look's), or null.</summary>
    public static Look? Matching(IReadOnlyDictionary<Slot, Piece> plate, IEnumerable<Look> looks) =>
        plate.Count == 0 ? null : looks.FirstOrDefault(l => Same(plate, l.Pieces));
}

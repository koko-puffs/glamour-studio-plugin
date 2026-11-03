using System;
using System.Collections.Generic;
using System.Linq;
using GlamourStudio.Core;
using Lumina.Excel.Sheets;

namespace GlamourStudio.Game;

/// <summary>An item as the window shows it and the plate needs it.</summary>
/// <param name="Slots">The slots it fits (a ring both hands; none: it isn't gear).</param>
/// <param name="Dyes">How many dyes it takes (0, 1 or 2).</param>
public sealed record ItemInfo(uint Id, string Name, uint Icon, Slot[] Slots, int Dyes, int Level, bool Untradable = false)
{
    public bool IsGear => Slots.Length > 0;
}

/// <summary>A dye: its name, its swatch (0xRRGGBB) and the items that are it (the dye, and the general-purpose one).</summary>
public sealed record DyeInfo(byte Id, string Name, uint Rgb, uint[] Items);

/// <summary>
/// The game's sheets the plugin reads, looked up once and kept: items (name, icon, slot, dyes), dyes
/// (Stain: name, colour, the dye items), the armoire's rows (Cabinet: which item each stores) and the
/// outfit sets the dresser keeps as one (MirageStoreSetItem: a set's item and its pieces).
/// </summary>
public sealed class GameData
{
    private readonly Dictionary<uint, ItemInfo?> items = new();
    private readonly Dictionary<byte, DyeInfo?> dyes = new();
    private Dictionary<uint, uint>? cabinetByItem;
    private Dictionary<uint, uint>? itemByCabinet;
    private Dictionary<uint, (int Column, uint Item)[]>? sets;
    private Dictionary<uint, List<byte>>? stainsByItem;

    /// <summary>An item, or null when the game has none by that id. Its normal-quality id is looked up.</summary>
    public ItemInfo? Item(uint id)
    {
        id = LookJson.BaseId(id);
        if (items.TryGetValue(id, out var known)) return known;
        ItemInfo? info = null;
        try
        {
            if (Services.Data.GetExcelSheet<Item>().GetRowOrDefault(id) is { } row && row.RowId != 0)
            {
                var name = row.Name.ExtractText();
                var cat = row.EquipSlotCategory.ValueNullable;
                var slots = cat is { } c
                    ? Slots.OfCategory(c.MainHand > 0, c.OffHand > 0, c.Head > 0, c.Body > 0, c.Gloves > 0, c.Legs > 0, c.Feet > 0,
                        c.Ears > 0, c.Neck > 0, c.Wrists > 0, c.FingerL > 0 || c.FingerR > 0)
                    : Array.Empty<Slot>();
                if (cat is { SoulCrystal: > 0 }) slots = Array.Empty<Slot>();
                info = new ItemInfo(id, string.IsNullOrWhiteSpace(name) ? $"Item {id}" : name, row.Icon, slots, Math.Clamp((int)row.DyeCount, 0, 2), row.LevelEquip, row.IsUntradable);
            }
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't read item {Id}", id);
        }
        items[id] = info;
        return info;
    }

    public string ItemName(uint id) => Item(id)?.Name ?? $"Item {LookJson.BaseId(id)}";

    /// <summary>How many dyes an item takes.</summary>
    public int DyeChannels(uint id) => Item(id)?.Dyes ?? 0;

    /// <summary>A dye, or null for none (0) or one the game hasn't.</summary>
    public DyeInfo? Dye(byte id)
    {
        if (id == 0) return null;
        if (dyes.TryGetValue(id, out var known)) return known;
        DyeInfo? info = null;
        try
        {
            if (Services.Data.GetExcelSheet<Stain>().GetRowOrDefault(id) is { } row)
            {
                var list = new List<uint>();
                foreach (var r in row.Item)
                    if (r.RowId != 0) list.Add(r.RowId);
                var name = row.Name.ExtractText();
                info = new DyeInfo(id, string.IsNullOrWhiteSpace(name) ? $"Dye {id}" : Capital(name), row.Color & 0xFFFFFF, list.ToArray());
            }
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't read dye {Id}", id);
        }
        dyes[id] = info;
        return info;
    }

    public string DyeName(byte id) => id == 0 ? "No dye" : Dye(id)?.Name ?? $"Dye {id}";

    /// <summary>
    /// The dyes an item can make: a colour's own bottle one, a Spectrum dye (the general-purpose
    /// bottles the game now uses, seen in a recording) many. Empty when it isn't a dye.
    /// </summary>
    public IReadOnlyList<byte> StainsOfItem(uint item)
    {
        if (stainsByItem == null)
        {
            stainsByItem = new Dictionary<uint, List<byte>>();
            try
            {
                foreach (var row in Services.Data.GetExcelSheet<Stain>())
                {
                    if (row.RowId is 0 or > 255) continue;
                    foreach (var r in row.Item)
                    {
                        if (r.RowId == 0) continue;
                        if (!stainsByItem.TryGetValue(r.RowId, out var list)) stainsByItem[r.RowId] = list = new List<byte>();
                        if (!list.Contains((byte)row.RowId)) list.Add((byte)row.RowId);
                    }
                }
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, "Glamour Studio couldn't read the dyes");
            }
        }
        return stainsByItem.TryGetValue(LookJson.BaseId(item), out var l) ? l : Array.Empty<byte>();
    }

    /// <summary>Whether a bottle (an item) can make a dye.</summary>
    public bool Makes(uint item, byte stain) => stain != 0 && StainsOfItem(item).Contains(stain);

    /// <summary>The armoire's row that stores an item, or null when the armoire doesn't take it.</summary>
    public uint? CabinetOf(uint item)
    {
        BuildCabinet();
        return cabinetByItem!.TryGetValue(LookJson.BaseId(item), out var row) ? row : null;
    }

    /// <summary>Every armoire row and the item it stores.</summary>
    public IReadOnlyDictionary<uint, uint> CabinetRows
    {
        get
        {
            BuildCabinet();
            return itemByCabinet!;
        }
    }

    private void BuildCabinet()
    {
        if (cabinetByItem != null) return;
        cabinetByItem = new Dictionary<uint, uint>();
        itemByCabinet = new Dictionary<uint, uint>();
        try
        {
            foreach (var row in Services.Data.GetExcelSheet<Cabinet>())
            {
                var item = row.Item.RowId;
                if (row.RowId == 0 || item == 0) continue;
                cabinetByItem.TryAdd(item, row.RowId);
                itemByCabinet[row.RowId] = item;
            }
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't read the armoire's list");
        }
    }

    /// <summary>Every outfit set: its item and its pieces' items.</summary>
    public IReadOnlyDictionary<uint, uint[]> AllSets
    {
        get
        {
            SetPieces(0);
            return allSets ??= sets!.ToDictionary(kv => kv.Key, kv => kv.Value.Select(p => p.Item).ToArray());
        }
    }

    private Dictionary<uint, uint[]>? allSets;

    private readonly Dictionary<uint, string> jobs = new();

    /// <summary>A class or job's abbreviation ("WHM"), or "" for none.</summary>
    public string Job(uint classJob)
    {
        if (jobs.TryGetValue(classJob, out var known)) return known;
        var abbr = string.Empty;
        try
        {
            if (Services.Data.GetExcelSheet<ClassJob>().GetRowOrDefault(classJob) is { } row) abbr = row.Abbreviation.ExtractText();
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't read job {Id}", classJob);
        }
        return jobs[classJob] = abbr;
    }

    private uint[]? prisms;

    /// <summary>The glamour prisms' items (storing in the dresser spends one a piece), found by name in the Item sheet.</summary>
    public IReadOnlyList<uint> GlamourPrisms()
    {
        if (prisms != null) return prisms;
        try
        {
            prisms = Services.Data.GetExcelSheet<Item>()
                .Where(r => r.RowId != 0 && r.Name.ExtractText() is var n && n.StartsWith("Glamour Prism", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.RowId).ToArray();
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't find the glamour prisms");
            prisms = Array.Empty<uint>();
        }
        return prisms;
    }

    private Dictionary<uint, uint>? setOfPiece;

    /// <summary>The outfit set a piece belongs to (its own item), or 0.</summary>
    public uint SetOf(uint piece)
    {
        if (setOfPiece == null)
        {
            var map = new Dictionary<uint, uint>();
            foreach (var (set, pieces) in AllSets)
                foreach (var p in pieces)
                    map.TryAdd(LookJson.BaseId(p), set);
            setOfPiece = map;
        }
        return setOfPiece.TryGetValue(LookJson.BaseId(piece), out var s) ? s : 0;
    }

    /// <summary>An outfit set's pieces (its column, as the dresser counts them, and the item); empty when it isn't a set.</summary>
    public IReadOnlyList<(int Column, uint Item)> SetPieces(uint setItem)
    {
        if (sets == null)
        {
            sets = new Dictionary<uint, (int, uint)[]>();
            try
            {
                foreach (var row in Services.Data.GetExcelSheet<MirageStoreSetItem>())
                {
                    if (row.RowId == 0) continue;
                    var cols = new[]
                    {
                        row.MainHand.RowId, row.OffHand.RowId, row.Head.RowId, row.Body.RowId, row.Hands.RowId, row.Legs.RowId,
                        row.Feet.RowId, row.Earrings.RowId, row.Necklace.RowId, row.Bracelets.RowId, row.Ring.RowId,
                    };
                    var pieces = cols.Select((item, col) => (col, item)).Where(p => p.item != 0).ToArray();
                    if (pieces.Length > 0) sets[row.RowId] = pieces;
                }
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, "Glamour Studio couldn't read the outfit sets");
            }
        }
        return sets.TryGetValue(LookJson.BaseId(setItem), out var p) ? p : Array.Empty<(int, uint)>();
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>
/// Reads your wardrobe from the game, once every two seconds on the framework thread: your bags and
/// armoury chest (always there), your saddlebags, glamour dresser and armoire (once you've opened
/// them this session: the game only fetches them then), a retainer's bags while one is open, your
/// glamour plates (once you've opened them at a dresser), and what you wear. What it read is kept per
/// character in wardrobe.json in the plugin's folder, so a place read yesterday still counts today.
/// Only gear is kept (anything with a slot). <see cref="Version"/> goes up whenever something changed.
/// </summary>
public sealed unsafe class WardrobeReader
{
    private static readonly TimeSpan ReadEvery = TimeSpan.FromSeconds(2);

    /// <summary>Saved again at most this often when only the times changed.</summary>
    private static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(5);

    /// <summary>A change is saved this long after it at the soonest (many changes close together are one write).</summary>
    private static readonly TimeSpan SaveAfter = TimeSpan.FromSeconds(30);

    private static readonly InventoryType[] Bags = { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 };

    private static readonly InventoryType[] Armoury =
    {
        InventoryType.EquippedItems, InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody,
        InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist, InventoryType.ArmoryRings,
    };

    private static readonly InventoryType[] Saddlebags =
    {
        InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2,
    };

    private static readonly InventoryType[] RetainerBags =
    {
        InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3, InventoryType.RetainerPage4,
        InventoryType.RetainerPage5, InventoryType.RetainerPage6, InventoryType.RetainerPage7, InventoryType.RetainerEquippedItems,
    };

    private static readonly string[] RetainerWindows = { "InventoryRetainer", "InventoryRetainerLarge" };

    // The windows that show a place: the game sends a place when one is opened, and until then its
    // containers can say they're loaded while empty (or still hold another character's).
    private static readonly string[] SaddlebagWindows = { "InventoryBuddy", "InventoryBuddy2" };
    // (Not Glamour Plates: opened from the Character window it doesn't fetch the dresser, and what
    // the game still holds may be another character's; at a dresser, the dresser was open first.)
    private static readonly string[] DresserWindows = { "MiragePrismPrismBox" };
    private static readonly string[] ArmoireWindows = { "Cabinet" };
    private static readonly string[] PlateWindows = { "MiragePrismMiragePlate" };

    /// <summary>The windows seen open since this character logged in.</summary>
    private readonly HashSet<string> seenOpen = new();

    /// <summary>The retainer whose bags are open, and since when (its pages are read a second after, so they're its own).</summary>
    private (ulong Id, DateTime Since) retainerOpen;

    private readonly GameData data;
    private readonly string path;
    private readonly object writing = new();
    private Wardrobe store = new();
    private DateTime nextRead;
    private DateTime savedAt;
    private bool dirty;
    private IReadOnlyList<CharacterWardrobe>? all;
    private (int Version, string? Key) allFor = (-1, null);
    private long saveNumber;
    private long writtenNumber;
    private int version;

    public WardrobeReader(GameData data)
    {
        this.data = data;
        path = Path.Combine(Services.PluginInterface.GetPluginConfigDirectory(), "wardrobe.json");
        try
        {
            if (File.Exists(path)) store = JsonSerializer.Deserialize<Wardrobe>(File.ReadAllText(path)) ?? new Wardrobe();
            // 0.1.0 could take saddlebags as read (and empty) before they were opened: read again.
            if (store.Version < 2)
            {
                foreach (var c in store.Characters.Values) c.Read.Remove(Place.Saddlebag);
                store.Version = 2;
            }
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't read wardrobe.json: your wardrobe is read again as you open it");
            store = new Wardrobe();
            // Kept aside, not overwritten by the next save (a newer version's file, say, can be read again by it).
            try
            {
                if (File.Exists(path)) File.Move(path, $"{path}.unread-{DateTime.UtcNow:yyyyMMddHHmmss}", true);
            }
            catch (Exception moveEx)
            {
                Services.Log.Debug(moveEx, "Glamour Studio couldn't set the unread wardrobe.json aside");
            }
        }
    }

    /// <summary>Goes up whenever what's known of a wardrobe changes.</summary>
    public int Version => Volatile.Read(ref version);

    /// <summary>The character you're playing, as known (null while logged out).</summary>
    public CharacterWardrobe? Current { get; private set; }

    /// <summary>Every character known, the one you're playing first.</summary>
    public IReadOnlyList<CharacterWardrobe> All
    {
        get
        {
            var stamp = (Version, Current?.Key);
            if (all == null || stamp != allFor)
            {
                allFor = stamp;
                all = store.Characters.Values.OrderByDescending(c => Current != null && c.Key == Current.Key).ThenBy(c => c.Name).ToList();
            }
            return all;
        }
    }

    /// <summary>The armoire's stored pieces now (item → its Cabinet row); empty until it's been opened this session.</summary>
    public IReadOnlyDictionary<uint, uint> ArmoireNow { get; private set; } = new Dictionary<uint, uint>();

    /// <summary>Whether the dresser and armoire have been read this session (the plate editor uses both live).</summary>
    public bool DresserLive { get; private set; }

    public bool ArmoireLive { get; private set; }

    /// <summary>The dyes in your bags: each dye and how many bottles.</summary>
    public IReadOnlyDictionary<byte, int> BagDyes { get; private set; } = new Dictionary<byte, int>();

    /// <summary>Reads again (every two seconds, or now).</summary>
    public void Tick(bool now = false)
    {
        var at = DateTime.UtcNow;
        if (!now && at < nextRead) return;
        nextRead = at + ReadEvery;
        try
        {
            Read(at);
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't read your wardrobe");
            nextRead = at + TimeSpan.FromSeconds(30);
        }
    }

    private void Read(DateTime at)
    {
        if (!Services.PlayerState.IsLoaded || Services.PlayerState.ContentId == 0)
        {
            if (Current != null) Changed();
            Current = null;
            DresserLive = ArmoireLive = false;
            ArmoireNow = new Dictionary<uint, uint>();
            seenOpen.Clear();
            return;
        }
        var key = Wardrobe.KeyOf(Services.PlayerState.ContentId);
        if (!store.Characters.TryGetValue(key, out var me))
        {
            store.Characters[key] = me = new CharacterWardrobe { Key = key };
            Changed();
        }
        if (Current?.Key != key)
        {
            DresserLive = ArmoireLive = false;
            ArmoireNow = new Dictionary<uint, uint>();
            seenOpen.Clear();
            // Everything worked out from the wardrobe is worked out again for this character.
            Changed();
        }
        Current = me;
        foreach (var w in SaddlebagWindows.Concat(DresserWindows).Concat(ArmoireWindows).Concat(PlateWindows))
            if (!seenOpen.Contains(w) && IsOpen(w))
                seenOpen.Add(w);
        var changed = false;

        var name = Services.PlayerState.CharacterName ?? string.Empty;
        var world = Services.PlayerState.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;
        var body = Services.PlayerState.Sex == Dalamud.Game.Player.Sex.Male ? Body.Masculine : Body.Feminine;
        if (name.Length > 0 && (me.Name != name || me.World != world || me.Body != body))
        {
            me.Name = name;
            me.World = world;
            me.Body = body;
            changed = true;
        }

        var mgr = InventoryManager.Instance();
        if (mgr != null)
        {
            // Every container of a place there, or the place isn't read this time (at login they come one by one).
            changed |= Keep(me.Read, Place.Inventory, Gear(mgr, Bags, needLoaded: true), at);
            changed |= Keep(me.Read, Place.Armoury, Gear(mgr, Armoury, needLoaded: true), at);
            if (Seen(SaddlebagWindows) && Gear(mgr, Saddlebags, needLoaded: true) is { } saddle)
                changed |= Keep(me.Read, Place.Saddlebag, saddle, at);
            changed |= ReadRetainer(me, mgr, at);
            changed |= ReadOutfit(me, mgr, at);
            var dyes = CountDyes(mgr);
            // Only a new one when the count changed (the window keys its plate plan on it).
            if (dyes.Count != BagDyes.Count || dyes.Any(kv => BagDyes.GetValueOrDefault(kv.Key) != kv.Value)) BagDyes = dyes;
        }
        if (Seen(DresserWindows)) changed |= ReadDresser(me, at);
        if (Seen(ArmoireWindows)) changed |= ReadArmoire(me, at);
        if (Seen(PlateWindows)) changed |= ReadPlates(me, at);

        if (changed)
        {
            Changed();
            dirty = true;
            Recorder.Log($"READ changed: wardrobe v{Version}, dresser {me.Dresser.Count} pieces, plates {(me.PlatesReadAt != null ? me.Plates.Count(p => p.Pieces.Count > 0) + " with pieces" : "not read")}, retainers {me.Retainers.Count}");
        }
        // Written off the framework thread's hot path: a change after 30 s, the read times every 5 minutes.
        if ((dirty && at - savedAt > SaveAfter) || at - savedAt > SaveEvery)
        {
            dirty = false;
            Save(at);
        }
    }

    private void Changed() => Interlocked.Increment(ref version);

    private bool Seen(string[] windows) => windows.Any(seenOpen.Contains);

    /// <summary>Keeps a place as read now; true when its pieces changed.</summary>
    private static bool Keep(Dictionary<Place, PlaceRead> read, Place place, HashSet<uint>? items, DateTime at)
    {
        if (items == null) return false;
        if (read.TryGetValue(place, out var was) && was.Items.SetEquals(items))
        {
            was.ReadAt = at;
            return false;
        }
        read[place] = new PlaceRead { ReadAt = at, Items = items };
        Recorder.Log($"READ {Places.Key(place)}: {items.Count} pieces (was {(was != null ? was.Items.Count.ToString() : "never read")})");
        return true;
    }

    /// <summary>The gear in some containers (normal-quality ids); null when <paramref name="needLoaded"/> and one isn't loaded.</summary>
    private HashSet<uint>? Gear(InventoryManager* mgr, InventoryType[] types, bool needLoaded = false)
    {
        var set = new HashSet<uint>();
        foreach (var t in types)
        {
            var c = mgr->GetInventoryContainer(t);
            if (c == null || !c->IsLoaded)
            {
                if (needLoaded) return null;
                continue;
            }
            for (var i = 0; i < c->Size; i++)
            {
                var item = c->GetInventorySlot(i);
                if (item == null || item->IsEmpty()) continue;
                var id = item->GetBaseItemId();
                if (data.Item(id)?.IsGear == true) set.Add(id);
                // A glamour cast on a piece is a piece of yours too only once it's back (it's spent): not counted.
            }
        }
        return set;
    }

    private Dictionary<byte, int> CountDyes(InventoryManager* mgr)
    {
        var dyes = new Dictionary<byte, int>();
        foreach (var t in Bags)
        {
            var c = mgr->GetInventoryContainer(t);
            if (c == null || !c->IsLoaded) continue;
            for (var i = 0; i < c->Size; i++)
            {
                var item = c->GetInventorySlot(i);
                if (item == null || item->IsEmpty()) continue;
                // A Spectrum bottle counts for every colour it makes (so a count is "bottles that could make it").
                foreach (var dye in data.StainsOfItem(item->GetBaseItemId()))
                    dyes[dye] = dyes.GetValueOrDefault(dye) + (int)item->GetQuantity();
            }
        }
        return dyes;
    }

    /// <summary>A bottle of a dye in your bags (its container and slot), or null when there's none.</summary>
    public (InventoryType Type, int Slot)? FindDye(byte stain)
    {
        if (stain == 0) return null;
        var mgr = InventoryManager.Instance();
        if (mgr == null) return null;
        // The colour's own bottle first; a Spectrum bottle (it makes many) only when there's none.
        (InventoryType, int)? general = null;
        foreach (var t in Bags)
        {
            var c = mgr->GetInventoryContainer(t);
            if (c == null || !c->IsLoaded) continue;
            for (var i = 0; i < c->Size; i++)
            {
                var item = c->GetInventorySlot(i);
                if (item == null || item->IsEmpty()) continue;
                var makes = data.StainsOfItem(item->GetBaseItemId());
                if (!makes.Contains(stain)) continue;
                if (makes.Count == 1) return (t, i);
                general ??= (t, i);
            }
        }
        return general;
    }

    /// <summary>A retainer's bags while its inventory window is open (the game has them only then).</summary>
    private bool ReadRetainer(CharacterWardrobe me, InventoryManager* mgr, DateTime at)
    {
        if (!RetainerWindows.Any(IsOpen))
        {
            retainerOpen = default;
            return false;
        }
        var rm = RetainerManager.Instance();
        if (rm == null || !rm->IsReady) return false;
        var active = rm->GetActiveRetainer();
        if (active == null || active->RetainerId == 0) return false;
        // A second after its bags open, so the pages are this retainer's and not the last one's.
        if (retainerOpen.Id != active->RetainerId)
        {
            retainerOpen = (active->RetainerId, at);
            return false;
        }
        if (at - retainerOpen.Since < TimeSpan.FromSeconds(1)) return false;
        var items = Gear(mgr, RetainerBags, needLoaded: true);
        if (items == null) return false;
        var id = active->RetainerId.ToString();
        if (me.Retainers.TryGetValue(id, out var was) && was.Items.SetEquals(items))
        {
            was.ReadAt = at;
            return false;
        }
        me.Retainers[id] = new PlaceRead { ReadAt = at, Items = items };
        me.RetainersChanged();
        return true;
    }

    private static bool IsOpen(string window)
    {
        var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(window).Address;
        return a != null && a->IsVisible;
    }

    /// <summary>What you wear: each slot's glamour (or the piece itself) and its dyes.</summary>
    private bool ReadOutfit(CharacterWardrobe me, InventoryManager* mgr, DateTime at)
    {
        var c = mgr->GetInventoryContainer(InventoryType.EquippedItems);
        if (c == null || !c->IsLoaded) return false;
        var outfit = new Dictionary<Slot, Piece>();
        for (var i = 0; i < c->Size; i++)
        {
            if (Slots.FromEquippedIndex(i) is not { } slot) continue;
            var item = c->GetInventorySlot(i);
            if (item == null || item->IsEmpty()) continue;
            var shown = item->GlamourId != 0 ? LookJson.BaseId(item->GlamourId) : item->GetBaseItemId();
            if (shown == 0 || data.Item(shown)?.IsGear != true) continue;
            outfit[slot] = new Piece(shown, item->GetStain(0), item->GetStain(1));
        }
        me.OutfitReadAt = at;
        var looks = ReadAppearance(me);
        if (Plates.Same(outfit, me.Outfit)) return looks;
        me.Outfit = outfit;
        return true;
    }

    /// <summary>The game's file of the character creator's colours (read once), or null.</summary>
    private byte[]? humanCmp;
    private bool humanCmpTried;

    /// <summary>The characters whose colours were worked out this session (stored ones may be from an older version, or from a read that failed).</summary>
    private readonly HashSet<string> coloursDone = new();

    /// <summary>How you look now (your customize bytes and the colours they pick), when it changed.</summary>
    private bool ReadAppearance(CharacterWardrobe me)
    {
        byte[] now;
        try
        {
            if (Services.Objects.LocalPlayer is not { } player) return false;
            now = player.Customize.ToArray();
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't read how your character looks");
            return false;
        }
        if (now.Length < Appearance.Length || now[0] == 0) return false;
        var same = me.Customize != null && me.Customize.AsSpan().SequenceEqual(now);
        if (same && me.Colours != null && coloursDone.Contains(me.Key)) return false;
        if (!humanCmpTried)
        {
            humanCmpTried = true;
            try
            {
                humanCmp = Services.Data.GetFile(Appearance.HumanCmp)?.Data;
            }
            catch (Exception ex)
            {
                Services.Log.Debug(ex, "Glamour Studio couldn't read the character creator's colours");
            }
        }
        var colours = Appearance.Colours(now, humanCmp);
        coloursDone.Add(me.Key);
        var changed = !same || me.Colours == null || me.Colours.Count != colours.Count
                      || colours.Any(kv => !me.Colours.TryGetValue(kv.Key, out var v) || v != kv.Value);
        me.Customize = now;
        me.Colours = colours;
        return changed;
    }

    /// <summary>The dresser, once the game has it (opened this session): each piece with its dyes, outfit sets as their pieces.</summary>
    private bool ReadDresser(CharacterWardrobe me, DateTime at)
    {
        var mm = MirageManager.Instance();
        if (mm == null || !mm->PrismBoxLoaded)
        {
            DresserLive = false;
            return false;
        }
        DresserLive = true;
        var pieces = new List<DresserPiece>();
        var ids = mm->PrismBoxItemIds;
        var s0 = mm->PrismBoxStain0Ids;
        var s1 = mm->PrismBoxStain1Ids;
        for (var i = 0; i < ids.Length; i++)
        {
            var raw = ids[i];
            if (raw == 0) continue;
            var id = LookJson.BaseId(raw);
            var set = data.SetPieces(id);
            if (set.Count > 0)
            {
                foreach (var (column, piece) in set)
                {
                    bool stored;
                    try
                    {
                        stored = mm->IsSetSlotUnlocked((uint)i, column);
                    }
                    catch
                    {
                        stored = true;
                    }
                    // (The set as the dresser has it: a high-quality set's id is a million more, a set of its own.)
                    if (stored) pieces.Add(new DresserPiece(i, piece, s0[i], s1[i], raw >= LookJson.HqOffset ? id + LookJson.HqOffset : id));
                }
                continue;
            }
            if (data.Item(id)?.IsGear == true) pieces.Add(new DresserPiece(i, raw, s0[i], s1[i]));
        }
        var same = pieces.Count == me.Dresser.Count && pieces.Zip(me.Dresser).All(p => p.First == p.Second);
        me.Dresser = pieces;
        var items = pieces.Select(p => LookJson.BaseId(p.Item)).ToHashSet();
        return Keep(me.Read, Place.Dresser, items, at) || !same;
    }

    /// <summary>The armoire, once the game has it (opened this session).</summary>
    private bool ReadArmoire(CharacterWardrobe me, DateTime at)
    {
        var ui = UIState.Instance();
        if (ui == null || !ui->Cabinet.IsCabinetLoaded())
        {
            if (ArmoireLive) ArmoireNow = new Dictionary<uint, uint>();
            ArmoireLive = false;
            return false;
        }
        ArmoireLive = true;
        var now = new Dictionary<uint, uint>();
        foreach (var (row, item) in data.CabinetRows)
            if (ui->Cabinet.IsItemInCabinet(row))
                now.TryAdd(item, row);
        ArmoireNow = now;
        return Keep(me.Read, Place.Armoire, now.Keys.ToHashSet(), at);
    }

    /// <summary>Your glamour plates as saved, once the game has them (you've opened them at a dresser).</summary>
    private bool ReadPlates(CharacterWardrobe me, DateTime at)
    {
        var mm = MirageManager.Instance();
        if (mm == null || !mm->GlamourPlatesLoaded) return false;
        var plates = new List<PlateRead>();
        var all = mm->GlamourPlates;
        for (var p = 0; p < all.Length && p < Plates.Count; p++)
        {
            var plate = all[p];
            var pieces = new Dictionary<Slot, Piece>();
            var ids = plate.ItemIds;
            var d0 = plate.Stain0Ids;
            var d1 = plate.Stain1Ids;
            for (var s = 0; s < ids.Length; s++)
            {
                if (ids[s] == 0 || Slots.FromPlateIndex(s) is not { } slot) continue;
                pieces[slot] = new Piece(LookJson.BaseId(ids[s]), d0[s], d1[s]);
            }
            plates.Add(new PlateRead { Plate = p + 1, Pieces = pieces });
        }
        me.PlatesReadAt = at;
        var same = plates.Count == me.Plates.Count && plates.Zip(me.Plates).All(x => x.First.Plate == x.Second.Plate && Plates.Same(x.First.Pieces, x.Second.Pieces));
        if (same) return false;
        me.Plates = plates;
        return true;
    }

    /// <summary>Forgets a character the plugin read (Settings).</summary>
    public void Forget(string key)
    {
        if (!store.Characters.Remove(key)) return;
        if (Current?.Key == key) Current = null;
        Changed();
        Save(DateTime.UtcNow);
    }

    /// <summary>Writes what's unsaved now, and waits for it (the plugin unloading).</summary>
    public void Flush()
    {
        if (!dirty) return;
        dirty = false;
        try
        {
            var json = JsonSerializer.Serialize(store);
            lock (writing)
            {
                var temp = path + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, path, true);
                writtenNumber = Interlocked.Increment(ref saveNumber);
            }
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't save your wardrobe");
        }
    }

    private void Save(DateTime at)
    {
        savedAt = at;
        string json;
        try
        {
            json = JsonSerializer.Serialize(store);
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't save your wardrobe");
            return;
        }
        // Numbered: two saves close together can run in either order, and the older mustn't land last.
        var number = Interlocked.Increment(ref saveNumber);
        Task.Run(() =>
        {
            try
            {
                lock (writing)
                {
                    if (number <= writtenNumber) return;
                    var temp = path + ".tmp";
                    File.WriteAllText(temp, json);
                    File.Move(temp, path, true);
                    writtenNumber = number;
                }
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, "Glamour Studio couldn't save your wardrobe");
            }
        });
    }
}

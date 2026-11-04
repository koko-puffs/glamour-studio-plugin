using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>A game window's place on the screen, in ImGui's screen pixels.</summary>
public readonly record struct WindowRect(Vector2 Pos, Vector2 Size, bool Focused)
{
    public Vector2 Max => Pos + Size;
}

/// <summary>
/// The game's windows the panels sit beside: where each is on the screen, and the little each
/// panel reads or does in them that isn't the plugin's elsewhere (the dresser's Glamour Plates
/// button, the outfit set being stored, the piece in the dye window, the market board's search
/// and its prices, a shop's wares, the retainer open). Framework thread.
/// </summary>
public static unsafe class GameWindows
{
    /// <summary>A window by its name when it's shown, else null.</summary>
    public static AtkUnitBase* Shown(string name)
    {
        try
        {
            var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(name).Address;
            return a != null && a->IsVisible && a->RootNode != null ? a : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Whether a window (by name) is shown.</summary>
    public static bool IsShown(string name) => Shown(name) != null;

    /// <summary>A window by its id when it's shown, else null.</summary>
    public static AtkUnitBase* ShownById(uint id)
    {
        try
        {
            if (id == 0) return null;
            var stage = AtkStage.Instance();
            if (stage == null) return null;
            var a = stage->RaptureAtkUnitManager->AtkUnitManager.GetAddonById((ushort)id);
            return a != null && a->IsVisible && a->RootNode != null ? a : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Where a window is: its top left and size on the screen, scaled as the game draws it.</summary>
    public static WindowRect RectOf(AtkUnitBase* a)
    {
        var origin = ImGuiOrigin();
        var w = a->RootNode->Width * a->Scale;
        var h = a->RootNode->Height * a->Scale;
        var focused = false;
        try
        {
            var stage = AtkStage.Instance();
            focused = stage != null && stage->RaptureAtkUnitManager->AtkUnitManager.FocusedAddon == a;
        }
        catch
        {
            // Not known: not focused.
        }
        return new WindowRect(origin + new Vector2(a->X, a->Y), new Vector2(w, h), focused);
    }

    /// <summary>Where a window is when it's shown (by name), else null.</summary>
    public static WindowRect? RectNamed(string name)
    {
        var a = Shown(name);
        return a == null ? null : RectOf(a);
    }

    /// <summary>Where the dye window is when it's open, else null.</summary>
    public static WindowRect? DyeRect()
    {
        var a = DyeWindow();
        return a == null ? null : RectOf(a);
    }

    /// <summary>The first shop window shown (its name and place), else null.</summary>
    public static (string Name, WindowRect Rect)? ShopShown()
    {
        foreach (var name in ShopWindows)
        {
            var a = Shown(name);
            if (a != null) return (name, RectOf(a));
        }
        return null;
    }

    /// <summary>The window under the mouse that has the game's focus (its name), or null.</summary>
    public static string? FocusedName()
    {
        try
        {
            var stage = AtkStage.Instance();
            var a = stage == null ? null : stage->RaptureAtkUnitManager->AtkUnitManager.FocusedAddon;
            return a == null ? null : a->NameString;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Of <paramref name="wanted"/>, what a shop window (by name) sells, as far as its values tell.</summary>
    public static HashSet<uint> InShop(string name, IReadOnlySet<uint> wanted) => InShop(Shown(name), wanted);

    private static Vector2 ImGuiOrigin()
    {
        try
        {
            return Dalamud.Interface.Utility.ImGuiHelpers.MainViewport.Pos;
        }
        catch
        {
            return Vector2.Zero;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The glamour dresser.

    /// <summary>
    /// The dresser's own Glamour Plates button: the window sends its agent <c>[u:5]</c> and closes
    /// (seen in the first recording, just before the plates opened). False when it isn't open.
    /// </summary>
    public static bool OpenPlatesFromDresser()
    {
        var a = Shown("MiragePrismPrismBox");
        if (a == null) return false;
        try
        {
            var values = stackalloc AtkValue[3];
            values[0].Type = AtkValueType.UInt;
            values[0].UInt = 5;
            values[1].Type = AtkValueType.Undefined;
            values[2].Type = AtkValueType.Undefined;
            a->FireCallback(3, values, true);
            Recorder.Log("DRESSER fired [u:5] (its Glamour Plates button)");
            return true;
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't open Glamour Plates from the dresser");
            return false;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Storing an outfit set.

    /// <summary>The set being stored (its own item, 0 when it can't be told) and its pieces: each item and the bag it's put from (Invalid: not picked).</summary>
    public sealed record OutfitSetNow(uint SetItem, IReadOnlyList<(uint Item, InventoryType Where)> Pieces);

    private static (long Key, uint Set)? outfitSetCache;

    /// <summary>
    /// The outfit set window's set. Its agent's "source" is the piece it was opened from (or 0, adding
    /// to a set stored already: a recording saw both), so the set is the one its pieces belong to.
    /// </summary>
    public static OutfitSetNow? OutfitSet(GameData data)
    {
        if (Shown("MiragePrismPrismSetConvert") == null) return null;
        try
        {
            var agent = AgentMiragePrismPrismSetConvert.Instance();
            if (agent == null || agent->Data == null) return null;
            var d = agent->Data;
            var list = new List<(uint, InventoryType)>();
            var n = (int)Math.Min(d->NumItemsInSet, (uint)d->Items.Length);
            for (var i = 0; i < n; i++)
            {
                ref var it = ref d->Items[i];
                if (it.ItemId == 0) continue;
                list.Add((LookJson.BaseId(it.ItemId), it.InventoryType));
            }
            if (list.Count == 0) return null;
            // The set holding every piece listed (a piece can be in more than one set), else the first
            // piece's; looked for once while the window lists the same pieces (it's read every frame).
            var key = list.Aggregate(17L, (h, p) => h * 31 + p.Item1);
            uint set;
            if (outfitSetCache is { } c && c.Key == key) set = c.Set;
            else
            {
                var ids = list.Select(p => p.Item1).ToHashSet();
                set = data.AllSets.Where(kv => ids.All(i => kv.Value.Any(x => LookJson.BaseId(x) == i))).Select(kv => kv.Key).FirstOrDefault();
                if (set == 0) set = list.Select(p => data.SetOf(p.Item1)).FirstOrDefault(s => s != 0);
                // (Not kept while no set's known: the sets may not be read yet.)
                if (set != 0) outfitSetCache = (key, set);
            }
            if (set == 0 && d->SourceItemId != 0) set = data.SetOf(d->SourceItemId);
            return new OutfitSetNow(set, list);
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't read the outfit set window");
            return null;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The dye window.

    /// <summary>The dye window (its agent's window), when it's open.</summary>
    public static AtkUnitBase* DyeWindow()
    {
        try
        {
            var agent = AgentColorant.Instance();
            if (agent == null || !agent->AgentInterface.IsAgentActive()) return null;
            return ShownById(agent->AgentInterface.GetAddonId());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// The piece in the dye window: the one dyeable piece its preview shows, else the first dyeable
    /// gear in its values (the game names neither outright, and a value may be an icon's id; the
    /// recorder writes down which was found).
    /// </summary>
    public static uint DyedItem(GameData data)
    {
        var a = DyeWindow();
        if (a == null) return 0;
        try
        {
            var agent = AgentColorant.Instance();
            uint only = 0;
            var count = 0;
            foreach (ref var it in agent->CharaView.Items)
            {
                if (it.ItemId == 0) continue;
                var item = data.Item(LookJson.BaseId(it.ItemId));
                if (item is not { IsGear: true, Dyes: > 0 }) continue;
                only = item.Id;
                count++;
            }
            if (count == 1) return only;
            if (a->AtkValues == null) return 0;
            for (var i = 0; i < a->AtkValuesCount; i++)
            {
                ref var v = ref a->AtkValues[i];
                var t = (int)v.Type & 0xF;
                var id = t == 5 ? v.UInt : t == 3 && v.Int > 0 ? (uint)v.Int : 0;
                if (id is 0 or > 9_999_999) continue;
                var item = data.Item(LookJson.BaseId(id));
                if (item is { IsGear: true, Dyes: > 0 }) return item.Id;
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The market board.

    /// <summary>
    /// Searches the market board for a name: types it into the search window's box and presses
    /// search. Not yet watched in the game: false when the window isn't open or it couldn't.
    /// </summary>
    public static bool SearchMarket(string name)
    {
        var a = (AddonItemSearch*)Shown("ItemSearch");
        if (a == null) return false;
        try
        {
            if (a->SearchTextInput != null) a->SearchTextInput->SetText(name);
            a->SearchText.SetString(name);
            a->RunSearch(false);
            Recorder.Log($"MARKET searched for \"{name}\"");
            return true;
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't search the market board");
            return false;
        }
    }

    /// <summary>The cheapest listing of an item the market board shows now (its results open for it), or null.</summary>
    public static uint? CheapestListed(uint item)
    {
        if (Shown("ItemSearchResult") == null) return null;
        try
        {
            var proxy = InfoProxyItemSearch.Instance();
            if (proxy == null || LookJson.BaseId(proxy->SearchItemId) != LookJson.BaseId(item)) return null;
            uint? best = null;
            var n = (int)Math.Min(proxy->ListingCount, (uint)proxy->Listings.Length);
            for (var i = 0; i < n; i++)
            {
                var price = proxy->Listings[i].UnitPrice;
                if (price > 0 && (best == null || price < best)) best = price;
            }
            return best;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The item whose market results are open, or 0.</summary>
    public static uint MarketResultItem()
    {
        if (Shown("ItemSearchResult") == null) return 0;
        try
        {
            var proxy = InfoProxyItemSearch.Instance();
            return proxy == null ? 0 : LookJson.BaseId(proxy->SearchItemId);
        }
        catch
        {
            return 0;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Shops.

    /// <summary>The shop windows the plugin looks through for your shopping list.</summary>
    public static readonly string[] ShopWindows =
    {
        "Shop", "ShopExchangeItem", "ShopExchangeCurrency", "ShopExchangeCoin", "InclusionShop", "GrandCompanyExchange", "FreeShop", "SkyIslandExchange",
    };

    /// <summary>Of <paramref name="wanted"/>, the items a shop window's values name (what it sells, as far as can be told).</summary>
    public static HashSet<uint> InShop(AtkUnitBase* a, IReadOnlySet<uint> wanted)
    {
        var found = new HashSet<uint>();
        if (a == null || a->AtkValues == null || wanted.Count == 0) return found;
        try
        {
            for (var i = 0; i < a->AtkValuesCount; i++)
            {
                ref var v = ref a->AtkValues[i];
                var t = (int)v.Type & 0xF;
                var id = t == 5 ? v.UInt : t == 3 && v.Int > 0 ? (uint)v.Int : 0;
                if (id == 0) continue;
                var b = LookJson.BaseId(id);
                if (wanted.Contains(b)) found.Add(b);
            }
        }
        catch
        {
            // What was read stays.
        }
        return found;
    }

    // ---------------------------------------------------------------------------------------------
    // Retainers.

    /// <summary>The retainer whose bags are open: its id and name.</summary>
    public static (ulong Id, string Name)? RetainerOpen()
    {
        if (Shown("InventoryRetainerLarge") == null && Shown("InventoryRetainer") == null) return null;
        try
        {
            var rm = RetainerManager.Instance();
            if (rm == null || !rm->IsReady) return null;
            var r = rm->GetActiveRetainer();
            return r == null || r->RetainerId == 0 ? null : (r->RetainerId, r->NameString);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Your retainers (id and name), in the game's order; empty until the game has their list.</summary>
    public static List<(ulong Id, string Name)> Retainers()
    {
        var list = new List<(ulong, string)>();
        try
        {
            var rm = RetainerManager.Instance();
            if (rm == null || !rm->IsReady) return list;
            foreach (ref var r in rm->Retainers)
                if (r.RetainerId != 0)
                    list.Add((r.RetainerId, r.NameString));
        }
        catch
        {
            // None known.
        }
        return list;
    }
}

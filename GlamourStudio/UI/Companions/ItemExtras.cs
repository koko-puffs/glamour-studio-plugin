using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.ContextMenu;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Two small additions to the game's own item windows: lines under an item's tooltip (which of your
/// looks use the piece, the plates it's on, a copy in the dresser, on your wishlist), drawn
/// just under the game's tooltip; and a Glamour Studio entry in the right-click menu on gear (in
/// your bags, the armoury chest, a retainer's, and an item linked in chat).
/// </summary>
public sealed partial class MainWindow
{
    private (uint Item, int Sync, int Wardrobe, (bool, bool) Live, List<string> Lines)? tooltipCache;

    /// <summary>What a tooltip says of a piece (nothing when none of your looks, plates or list has it).</summary>
    private List<string> TooltipFor(uint item)
    {
        // (Where a piece is changes with the dresser and armoire read live, too.)
        var live = (wardrobe.DresserLive, wardrobe.ArmoireLive);
        if (tooltipCache is { } c && c.Item == item && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version && c.Live == live) return c.Lines;
        var lines = new List<string>();
        var looks = PaneLooks().Where(l => l.Pieces.Values.Any(p => LookJson.BaseId(p.Item) == item)).Select(l => l.Name).Distinct().ToList();
        var me = wardrobe.Current;
        var plates = me?.Plates.Where(p => p.Pieces.Values.Any(x => LookJson.BaseId(x.Item) == item)).Select(p => p.Plate).OrderBy(p => p).ToList() ?? new List<int>();
        var wish = sync.Library.Wishes.Contains(item);
        if (looks.Count > 0)
        {
            var names = looks.Take(3).ToList();
            var more = looks.Count > 3 ? $" and {looks.Count - 3} more" : string.Empty;
            lines.Add(looks.Count == 1 ? $"In your look {names[0]}" : $"In {looks.Count} of your looks: {string.Join(", ", names)}{more}");
        }
        if (plates.Count > 0) lines.Add(plates.Count == 1 ? $"On plate {plates[0]}" : $"On plates {Words.List(plates.Select(p => p.ToString(En)))}");
        if (wish) lines.Add(Where(item) == null ? "On your wishlist" : "On your wishlist (and yours now)");
        if (lines.Count > 0)
            switch (Where(item))
            {
                case Place.Dresser:
                    lines.Add("Your glamour dresser has a copy");
                    break;
                case Place.Armoire:
                    lines.Add("It's in your armoire");
                    break;
            }
        tooltipCache = (item, sync.Version, wardrobe.Version, live, lines);
        return lines;
    }

    /// <summary>The lines under the game's item tooltip (every frame, after Dalamud's windows).</summary>
    public void DrawTooltipLines()
    {
        if (!config.TooltipLines || CompanionsAway()) return;
        try
        {
            var hovered = Services.GameGui.HoveredItem;
            if (hovered == 0 || hovered >= 2_000_000) return;
            var item = LookJson.BaseId((uint)hovered);
            if (data.Item(item) is not { IsGear: true }) return;
            if (GameWindows.RectNamed("ItemDetail") is not { } rect) return;
            var lines = TooltipFor(item);
            if (lines.Count == 0) return;

            var width = MathF.Max(rect.Size.X, S(220));
            var pad = S(10);
            var lineH = Txt.Natural(Face.Sans, 12) + S(2);
            var ebH = Kit.EyebrowHeight + S(4);
            var height = pad * 2 + ebH + lineH * lines.Count;
            var vp = ImGui.GetMainViewport();
            var y = rect.Max.Y + S(4);
            if (y + height > vp.Pos.Y + vp.Size.Y) y = rect.Pos.Y - S(4) - height;
            ImGui.SetNextWindowPos(new Vector2(rect.Pos.X, y), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
            Theme.PushFrame();
            var shown = ImGui.Begin("##gs-tooltip", CompanionFlags | ImGuiWindowFlags.NoInputs);
            Theme.Pop();
            try
            {
                if (!shown) return;
                var dl = ImGui.GetWindowDrawList();
                var win = ImGui.GetWindowPos();
                var at = win + new Vector2(pad);
                var tw = width - pad * 2;
                // As the panels' header: whose lines these are, in small lilac capitals (an eyebrow).
                Txt.Spaced(dl, at, "GLAMOUR STUDIO", Face.SansSemibold, 11, Palette.Accent, 0.08f);
                for (var i = 0; i < lines.Count; i++)
                    Txt.Draw(dl, new Vector2(at.X, at.Y + ebH + lineH * i), Txt.Fit(lines[i], Face.Sans, 12, tw), Face.Sans, 12, i == 0 ? Palette.Ink : Palette.Soft);
            }
            finally
            {
                ImGui.End();
            }
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio's tooltip lines failed");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The right-click menu.

    /// <summary>A Glamour Studio entry on gear you right-click (Dalamud's context menu; the game's thread).</summary>
    public void OnContextMenu(IMenuOpenedArgs args)
    {
        if (!config.ContextMenu) return;
        try
        {
            uint id = 0;
            byte d1 = 0, d2 = 0;
            var inBags = false;
            if (args.Target is MenuTargetInventory inv && inv.TargetItem is { } gi)
            {
                id = gi.BaseItemId;
                var stains = gi.Stains;
                if (stains.Length > 0) d1 = stains[0];
                if (stains.Length > 1) d2 = stains[1];
                inBags = gi.ContainerType.ToString() is var t && (t.StartsWith("Inventory", StringComparison.Ordinal) || t.StartsWith("Armory", StringComparison.Ordinal));
            }
            else if (args.AddonName == "ChatLog")
            {
                unsafe
                {
                    var agent = AgentChatLog.Instance();
                    if (agent != null) id = agent->ContextItemId;
                }
            }
            else if (args.AddonName is "ItemSearch" or "ItemSearchResult" or "RecipeNote" or "ShopExchangeItem" or "ShopExchangeCurrency" or "Shop")
                id = (uint)Math.Min(Services.GameGui.HoveredItem, uint.MaxValue);
            id = LookJson.BaseId(id);
            if (id == 0 || data.Item(id) is not { IsGear: true } info) return;
            var piece = new Piece(id, d1, d2);
            var items = MenuFor(info, piece, inBags);
            if (items.Count == 0) return;
            args.AddMenuItem(new MenuItem
            {
                Name = "Glamour Studio",
                PrefixChar = 'G',
                IsSubmenu = true,
                OnClicked = a => a.OpenSubmenu(items),
            });
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't add to the right-click menu");
        }
    }

    private List<MenuItem> MenuFor(ItemInfo info, Piece piece, bool inBags)
    {
        var items = new List<MenuItem>();
        var id = info.Id;
        var name = info.Name;
        var looks = PaneLooks().Where(l => l.Pieces.Values.Any(p => LookJson.BaseId(p.Item) == id)).ToList();

        if (Linked && sync.Library.Mine.Count > 0)
            items.Add(new MenuItem
            {
                Name = "Add it to a look…",
                IsSubmenu = true,
                OnClicked = a => a.OpenSubmenu(sync.Library.Mine.Take(12).Select(look => new MenuItem
                {
                    Name = look.Name,
                    OnClicked = _ => PutOnLook(look, info, piece),
                }).ToList()),
            });
        // Try it on with a look that has it: the whole look in the fitting room (a submenu when more than one has it).
        MenuItem TryWith(Look look, string name) => new()
        {
            Name = name,
            OnClicked = _ =>
            {
                triedId = look.Id;
                TryOn(look.Pieces, Chat);
            },
        };
        if (looks.Count == 1)
            items.Add(TryWith(looks[0], $"Try it on with {looks[0].Name}"));
        else if (looks.Count > 1)
            items.Add(new MenuItem
            {
                Name = "Try it on with…",
                IsSubmenu = true,
                OnClicked = a => a.OpenSubmenu(looks.Take(12).Select(l => TryWith(l, l.Name)).ToList()),
            });
        items.Add(new MenuItem
        {
            Name = "Which looks use it",
            OnClicked = _ => Services.Chat.Print(looks.Count == 0
                ? $"Glamour Studio: none of your looks uses {name}."
                : $"Glamour Studio: {name} is in {Words.List(looks.Take(8).Select(l => l.Name))}{(looks.Count > 8 ? $" and {looks.Count - 8} more" : string.Empty)}."),
        });
        if (Linked && Where(id) == null && !sync.Library.Wishes.Contains(id))
            items.Add(new MenuItem { Name = "Put it on your wishlist", OnClicked = _ => AddWishes(new[] { piece }, Chat) });
        if (Linked && sync.Library.Wishes.Contains(id))
            items.Add(new MenuItem { Name = "Take it off your wishlist", OnClicked = _ => Unwish(id, Chat) });
        if (inBags && Armoire.IsOpen && data.CabinetOf(id) is { } row && !wardrobe.ArmoireNow.ContainsKey(id))
            items.Add(new MenuItem
            {
                Name = "Store it in the armoire",
                // In line with the panel's Store and Store all (the game says no to a store while it's
                // settling the last), and how it went is said in chat.
                OnClicked = _ => QueueStore(row, name, Chat),
            });
        return items;
    }

    /// <summary>A line in chat (what the right-click menu does says how it went there: no window may be open to show a toast).</summary>
    private static void Chat(string text) => Services.Chat.Print($"Glamour Studio: {text}");

    /// <summary>Puts a piece on one of your looks, in its slot (a ring on the free hand).</summary>
    private void PutOnLook(Look look, ItemInfo info, Piece piece)
    {
        var slot = info.Slots[0];
        if (slot == Slot.RingR && look.Pieces.ContainsKey(Slot.RingR) && !look.Pieces.ContainsKey(Slot.RingL)) slot = Slot.RingL;
        sync.PutOnLook(look.Id, slot, piece, r =>
        {
            recorder.Note($"LOOK PUT {info.Id} on {Slots.Key(slot)} of {look.Id}: {(r.Ok ? "ok" : r.Why)}");
            Services.Chat.Print(r.Ok ? $"Glamour Studio: {info.Name} is on {r.Value} ({Slots.Word(slot).ToLowerInvariant()})." : $"Glamour Studio: {r.Why ?? "the site said no"}");
        });
    }
}

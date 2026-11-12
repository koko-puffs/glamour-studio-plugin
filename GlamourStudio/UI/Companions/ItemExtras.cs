using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Two small additions to the game's own item windows: lines under an item's tooltip (which of your
/// looks use the piece, the plates it's on, on your wishlist, a copy in the dresser or armoire), in a
/// small box in the plugin's look just under the game's tooltip; and a Glamour Studio entry (its name
/// lilac) in the game's right-click menu on gear (in your bags, the armoury chest, a retainer's, and
/// an item linked in chat).
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>A line under a tooltip: its icon, words, and their colour (the icon lilac unless the line has a colour of its own).</summary>
    private readonly record struct TipLine(Icon Icon, string Text, Vector4 Color, bool Tinted);

    private (uint Item, int Sync, int Wardrobe, (bool, bool) Live, List<TipLine> Lines)? tooltipCache;

    /// <summary>What a tooltip says of a piece (nothing when none of your looks, plates or list has it).</summary>
    private List<TipLine> TooltipFor(uint item)
    {
        // (Where a piece is changes with the dresser and armoire read live, too.)
        var live = (wardrobe.DresserLive, wardrobe.ArmoireLive);
        if (tooltipCache is { } c && c.Item == item && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version && c.Live == live) return c.Lines;
        var lines = new List<TipLine>();
        var looks = PaneLooks().Where(l => l.Pieces.Values.Any(p => LookJson.BaseId(p.Item) == item)).Select(l => l.Name).Distinct().ToList();
        var me = wardrobe.Current;
        var plates = me?.Plates.Where(p => p.Pieces.Values.Any(x => LookJson.BaseId(x.Item) == item)).Select(p => p.Plate).OrderBy(p => p).ToList() ?? new List<int>();
        var wish = sync.Library.Wishes.Contains(item);
        if (looks.Count > 0)
        {
            var names = looks.Take(3).ToList();
            var more = looks.Count > 3 ? $" and {looks.Count - 3} more" : string.Empty;
            lines.Add(new TipLine(Icon.Hanger, looks.Count == 1 ? $"In your look {names[0]}" : $"In {looks.Count} of your looks: {string.Join(", ", names)}{more}", Palette.Body, false));
        }
        if (plates.Count > 0)
            lines.Add(new TipLine(Icon.Layers, plates.Count == 1 ? $"On plate {plates[0]}" : $"On plates {Words.List(plates.Select(p => p.ToString(En)))}", Palette.Body, false));
        if (wish) lines.Add(new TipLine(Icon.Star, Where(item) == null ? "On your wishlist" : "On your wishlist (and yours now)", Palette.Starred, true));
        if (lines.Count > 0)
            switch (Where(item))
            {
                case Place.Dresser:
                    lines.Add(new TipLine(Icon.Check, "Your glamour dresser has a copy", Palette.Ice, true));
                    break;
                case Place.Armoire:
                    lines.Add(new TipLine(Icon.Check, "It's in your armoire", Palette.Ice, true));
                    break;
            }
        tooltipCache = (item, sync.Version, wardrobe.Version, live, lines);
        return lines;
    }

    /// <summary>
    /// The lines under the game's item tooltip (every frame, after Dalamud's windows): a small box in
    /// the plugin's look (the mock's <c>.gs-tip</c>: the card, the strong hairline, 12px corners, 10px
    /// in and 12 across), as wide as the game's tooltip, 4px under it (over it with no room below): the
    /// tag and GLAMOUR STUDIO, then each line with its icon, wrapped.
    /// </summary>
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
            var padY = S(10);
            var padX = S(12);
            var gap = S(4);
            var head = S(18);
            var textX = S(14 + 6);
            var tw = width - padX * 2 - textX;
            var height = padY * 2 + head;
            foreach (var l in lines)
            {
                OneRun[0] = new Run(l.Text, Face.Sans, 12, l.Color);
                height += gap + MathF.Max(S(14), Txt.Flow(Vector2.Zero, tw, OneRun, TipLineCss, out _, draw: false));
            }
            var vp = ImGui.GetMainViewport();
            var y = rect.Max.Y + gap;
            if (y + height > vp.Pos.Y + vp.Size.Y) y = rect.Pos.Y - gap - height;
            ImGui.SetNextWindowPos(new Vector2(rect.Pos.X, y), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
            Theme.PushFrame();
            ImGui.PushStyleColor(ImGuiCol.WindowBg, Palette.Card);
            ImGui.PushStyleColor(ImGuiCol.Border, Palette.LineStrong);
            var shown = ImGui.Begin("##gs-tooltip", CompanionFlags | ImGuiWindowFlags.NoInputs);
            ImGui.PopStyleColor(2);
            Theme.Pop();
            try
            {
                if (!shown) return;
                var dl = ImGui.GetWindowDrawList();
                var win = ImGui.GetWindowPos();
                var at = win + new Vector2(padX, padY);
                // As the panels' header: the tag and whose lines these are, in small lilac capitals.
                Kit.Logo(at, 18);
                Txt.Spaced(dl, new Vector2(at.X + S(18 + 6), at.Y + (head - Txt.Natural(Face.SansSemibold, 10)) / 2),
                    Txt.SpacedFit("Glamour Studio", Face.SansSemibold, 10, 0.08f, width - padX * 2 - S(24)), Face.SansSemibold, 10, Palette.Accent, 0.08f);
                var ly = at.Y + head;
                var line = S(TipLineCss);
                foreach (var l in lines)
                {
                    ly += gap;
                    Icons.Draw(dl, l.Icon, new Vector2(at.X, ly + (line - S(14)) / 2), S(14), l.Tinted ? l.Color : Palette.Accent);
                    OneRun[0] = new Run(l.Text, Face.Sans, 12, l.Color);
                    ly += MathF.Max(S(14), Txt.Flow(new Vector2(at.X + textX, ly), tw, OneRun, TipLineCss, out _));
                }
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

    /// <summary>The tooltip lines' line box: 12px words at the mock's 1.55.</summary>
    private const float TipLineCss = 18.5f;

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
                // The game's own menu, in its own look: only our entry's name is tinted lilac (an
                // SeString colour), so it's ours at a glance.
                Name = OurName(),
                IsSubmenu = true,
                OnClicked = a => a.OpenSubmenu(items),
            });
        }
        catch (Exception ex)
        {
            Services.Log.Debug(ex, "Glamour Studio couldn't add to the right-click menu");
        }
    }

    /// <summary>"Glamour Studio" in the plugin's lilac, as the game colours text.</summary>
    private static Dalamud.Game.Text.SeStringHandling.SeString OurName() =>
        new Lumina.Text.SeStringBuilder().PushColorRgba(0xC9, 0xB8, 0xFF, 0xFF).Append("Glamour Studio").PopColor().ToReadOnlySeString().ToDalamudString();

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

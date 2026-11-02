using System;
using System.Collections.Generic;

namespace GlamourStudio.Core;

/// <summary>An outfit's twelve slots, as Glamour Studio has them (the site's <c>SLOTS</c>, in its order).</summary>
public enum Slot
{
    Head,
    Body,
    Hands,
    Legs,
    Feet,
    Ears,
    Neck,
    Wrists,
    RingR,
    RingL,
    Main,
    Off,
}

/// <summary>
/// The slots by every name and order they go by: the site's keys ("ringR"), the words shown
/// ("Ring, right"), and the game's own orders. The game keeps a glamour plate's pieces (MirageManager's
/// plates and the plate editor's) in the order of its gear window: main hand, off hand, head, body,
/// hands, legs, feet, ears, neck, wrists, right ring, left ring (the waist went in 6.0). What you wear
/// (the EquippedItems container) still has the waist's place at 5, and the soul crystal at 13. An
/// outfit set in the dresser (MirageStoreSetItem) lists main hand, off hand, head, body, hands, legs,
/// feet, earrings, necklace, bracelets and one ring.
/// </summary>
public static class Slots
{
    /// <summary>Every slot, in the site's order (gear, accessories, weapons).</summary>
    public static readonly Slot[] All =
    {
        Slot.Head, Slot.Body, Slot.Hands, Slot.Legs, Slot.Feet, Slot.Ears, Slot.Neck, Slot.Wrists,
        Slot.RingR, Slot.RingL, Slot.Main, Slot.Off,
    };

    /// <summary>A glamour plate's slots, in the game's order (index = the plate's slot index).</summary>
    public static readonly Slot[] PlateOrder =
    {
        Slot.Main, Slot.Off, Slot.Head, Slot.Body, Slot.Hands, Slot.Legs, Slot.Feet, Slot.Ears, Slot.Neck,
        Slot.Wrists, Slot.RingR, Slot.RingL,
    };

    /// <summary>The plate's slot index of a slot (0 to 11).</summary>
    public static int PlateIndex(Slot slot) => Array.IndexOf(PlateOrder, slot);

    /// <summary>The slot at a plate's slot index, or null past the twelve.</summary>
    public static Slot? FromPlateIndex(int index) => index >= 0 && index < PlateOrder.Length ? PlateOrder[index] : null;

    /// <summary>The slot of a place in what you wear (EquippedItems): null for the waist (5), the soul crystal (13) and beyond.</summary>
    public static Slot? FromEquippedIndex(int index) => index switch
    {
        0 => Slot.Main,
        1 => Slot.Off,
        2 => Slot.Head,
        3 => Slot.Body,
        4 => Slot.Hands,
        6 => Slot.Legs,
        7 => Slot.Feet,
        8 => Slot.Ears,
        9 => Slot.Neck,
        10 => Slot.Wrists,
        11 => Slot.RingR,
        12 => Slot.RingL,
        _ => null,
    };

    /// <summary>The slot an outfit set's piece fills, by its column (0 main hand … 10 the ring); null past them.</summary>
    public static Slot? FromSetColumn(int column) => column switch
    {
        0 => Slot.Main,
        1 => Slot.Off,
        2 => Slot.Head,
        3 => Slot.Body,
        4 => Slot.Hands,
        5 => Slot.Legs,
        6 => Slot.Feet,
        7 => Slot.Ears,
        8 => Slot.Neck,
        9 => Slot.Wrists,
        10 => Slot.RingR,
        _ => null,
    };

    /// <summary>The site's key for a slot ("head", "ringR", "main").</summary>
    public static string Key(Slot slot) => slot switch
    {
        Slot.Head => "head",
        Slot.Body => "body",
        Slot.Hands => "hands",
        Slot.Legs => "legs",
        Slot.Feet => "feet",
        Slot.Ears => "ears",
        Slot.Neck => "neck",
        Slot.Wrists => "wrists",
        Slot.RingR => "ringR",
        Slot.RingL => "ringL",
        Slot.Main => "main",
        _ => "off",
    };

    private static readonly Dictionary<string, Slot> ByKey = new(StringComparer.Ordinal);

    static Slots()
    {
        foreach (var s in All) ByKey[Key(s)] = s;
    }

    /// <summary>A slot from the site's key; false for anything else.</summary>
    public static bool TryParse(string? key, out Slot slot)
    {
        slot = default;
        return key != null && ByKey.TryGetValue(key, out slot);
    }

    /// <summary>The slot as shown (the site's <c>SLOT_LABEL</c>).</summary>
    public static string Word(Slot slot) => slot switch
    {
        Slot.Head => "Head",
        Slot.Body => "Body",
        Slot.Hands => "Hands",
        Slot.Legs => "Legs",
        Slot.Feet => "Feet",
        Slot.Ears => "Earrings",
        Slot.Neck => "Necklace",
        Slot.Wrists => "Bracelets",
        Slot.RingR => "Ring, right",
        Slot.RingL => "Ring, left",
        Slot.Main => "Main hand",
        _ => "Off hand",
    };

    /// <summary>A weapon's slot (left alone on a plate unless the look has one).</summary>
    public static bool IsWeapon(Slot slot) => slot is Slot.Main or Slot.Off;

    /// <summary>
    /// The slots an item fits, from its EquipSlotCategory's columns (each 1 where it goes): a ring fits
    /// either hand; a two-handed weapon and a body piece that covers more are still its main slot.
    /// </summary>
    public static Slot[] OfCategory(bool mainHand, bool offHand, bool head, bool body, bool gloves, bool legs, bool feet,
        bool ears, bool neck, bool wrists, bool ring)
    {
        if (mainHand) return new[] { Slot.Main };
        if (offHand) return new[] { Slot.Off };
        if (head) return new[] { Slot.Head };
        if (body) return new[] { Slot.Body };
        if (gloves) return new[] { Slot.Hands };
        if (legs) return new[] { Slot.Legs };
        if (feet) return new[] { Slot.Feet };
        if (ears) return new[] { Slot.Ears };
        if (neck) return new[] { Slot.Neck };
        if (wrists) return new[] { Slot.Wrists };
        if (ring) return new[] { Slot.RingR, Slot.RingL };
        return Array.Empty<Slot>();
    }
}

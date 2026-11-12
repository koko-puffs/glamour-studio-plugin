using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GlamourStudio.Core;

namespace GlamourStudio.UI.Model;

/// <summary>
/// A look's sheet (docs/redesign/pages/look.html), the parts with no game in them: the echo's four
/// rows, the gear columns' widths, the words for a bare slot, the plate slip's lines and the plate
/// menu's groups. Pure, so it's tested.
/// </summary>
public static class LookSheet
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    // ---------------------------------------------------------------------------------------------
    // The echo.

    /// <summary>Where the mock's four rows start, from the field's top-left in CSS px (their line boxes 0.92em apart at 104px).</summary>
    private static readonly (float X, float Y)[] EchoSpots = { (-60, 120), (40, 215.68f), (-120, 311.36f), (80, 407.04f) };

    /// <summary>
    /// The look's echo as the mock lays it: four rows, the name ("MOONLIT SCHOLAR · …") and the name
    /// from its last word ("SCHOLAR · MOONLIT SCHOLAR · …") in turn, each long enough to run past the
    /// field's right edge (<paramref name="widthCss"/>). None when the echo's font can draw nothing of it.
    /// </summary>
    public static IReadOnlyList<(string Text, float XCss, float YCss)> EchoRows(string? name, float widthCss, int px = 104)
    {
        var clean = EchoText.Clean(name);
        if (clean.Length == 0) return Array.Empty<(string, float, float)>();
        var turn = clean.LastIndexOf(' ') + 1;
        var rows = new (string, float, float)[EchoSpots.Length];
        for (var i = 0; i < EchoSpots.Length; i++)
        {
            var (x, y) = EchoSpots[i];
            // Bricolage's capitals are about 0.62em across: 0.55 runs a little past the edge.
            var chars = (int)MathF.Ceiling(MathF.Max(0, widthCss - x) / (px * 0.55f)) + 3;
            rows[i] = (i % 2 == 0 ? EchoText.Row(clean, chars) : Turned(clean, turn, chars), x, y);
        }
        return rows;
    }

    /// <summary>The name over and over from its last word on ("DUSK · FUATH AT DUSK · …"); the name itself when it's one word.</summary>
    private static string Turned(string clean, int turn, int chars) =>
        turn <= 0 ? EchoText.Row(clean, chars) : EchoText.Row(clean, chars + turn)[turn..];

    // ---------------------------------------------------------------------------------------------
    // The field's columns.

    /// <summary>
    /// The gear columns' width in the field (CSS px): the mock's 214 each side, narrower as the window
    /// narrows so the middle keeps <paramref name="midMinCss"/> (150 at the least).
    /// </summary>
    public static float GearWidth(float innerCss, float gapCss = 16, float midMinCss = 200) =>
        Math.Clamp((innerCss - gapCss * 2 - midMinCss) / 2, 150, 214);

    /// <summary>A gear callout's height (CSS px): 88 as the mock's, less when six don't fit the field's height (gap 12).</summary>
    public static float GearRow(float innerHeightCss, float gapCss = 12) =>
        Math.Clamp((innerHeightCss - gapCss * 5) / 6, 40, 88);

    // ---------------------------------------------------------------------------------------------
    // Words.

    /// <summary>A number in words in a sentence: "seven", "no", "14".</summary>
    public static string Lower(int n) => Words.InWords(n).ToLower(En);

    /// <summary>
    /// A slot the look leaves bare, as its dashed outline says it: its mark ("—" kept, "×" emptied),
    /// its words ("No bracelets") and what happens to it on a plate.
    /// </summary>
    public static (string Mark, string Title, string Line) Bare(Slot slot, bool clearBare)
    {
        if (slot == Slot.Main) return ("—", "Weapon", "kept: a plate's weapon is its job's");
        if (slot == Slot.Off) return ("—", "Off hand", "kept");
        var title = slot switch
        {
            Slot.Head => "No headgear",
            Slot.Body => "No body piece",
            Slot.Hands => "No gloves",
            Slot.Legs => "No legwear",
            Slot.Feet => "No footwear",
            Slot.Ears => "No earrings",
            Slot.Neck => "No necklace",
            Slot.Wrists => "No bracelets",
            Slot.RingR => "No ring (R)",
            _ => "No ring (L)",
        };
        return clearBare ? ("×", title, "emptied on the plate") : ("—", title, "left as it is");
    }

    /// <summary>
    /// How many go on, and from where: "All seven go on: six from the dresser, one from the armoire",
    /// "Five of seven go on, from the dresser", "None of its pieces is in your dresser or armoire".
    /// </summary>
    public static string GoesOn(int ready, int wanted, int fromDresser, int fromArmoire)
    {
        if (wanted == 0) return "It has no pieces";
        if (ready == 0) return "None of its pieces is in your dresser or armoire";
        var from = fromDresser > 0 && fromArmoire > 0
            ? $": {Lower(fromDresser)} from the dresser, {Lower(fromArmoire)} from the armoire"
            : fromArmoire > 0 ? ", from the armoire" : ", from the dresser";
        if (wanted == 1) return "Its one piece goes on" + from;
        return ready == wanted
            ? $"All {Lower(wanted)} go on{from}"
            : $"{Words.InWords(ready)} of {Lower(wanted)} go{(ready == 1 ? "es" : string.Empty)} on{from}";
    }

    /// <summary>The bare slots emptied: "The three slots it leaves bare are emptied", "The slot it leaves bare is emptied".</summary>
    public static string Emptied(int n) => n == 1 ? "The slot it leaves bare is emptied" : $"The {Lower(n)} slots it leaves bare are emptied";

    /// <summary>The slip's title while the look isn't on a plate yet: "Plate 3", "Plate 3, when it's ready", "A plate, when it's ready".</summary>
    public static string WhenReady(int meant, bool ready) => meant > 0
        ? ready ? $"Plate {meant}" : $"Plate {meant}, when it's ready"
        : ready ? "Put it on a plate" : "A plate, when it's ready";

    /// <summary>The compact plate line's heading: "Ready for a plate: all seven in your dresser or armoire".</summary>
    public static string ReadyHead(Readiness r)
    {
        if (r.Total == 0) return "Nothing on it yet";
        if (r.PlateReady) return r.Total == 1 ? "Ready for a plate: it's in your dresser or armoire" : $"Ready for a plate: all {Lower(r.Total)} in your dresser or armoire";
        if (r.Ready == 0) return "Not ready yet: none in your dresser or armoire";
        return $"Not ready yet: {Lower(r.Ready)} of {Lower(r.Total)} in your dresser or armoire";
    }

    // ---------------------------------------------------------------------------------------------
    // The plate menu.

    /// <summary>
    /// The plate menu's groups, as the mock's: the plates planned for this look, then the empty ones,
    /// then the rest, each in order; groups with none left out.
    /// </summary>
    public static IReadOnlyList<(string Heading, int[] Plates)> PlateGroups(int count, Func<int, bool> plannedForIt, Func<int, bool> empty)
    {
        var planned = new List<int>();
        var blank = new List<int>();
        var rest = new List<int>();
        for (var n = 1; n <= count; n++)
        {
            if (plannedForIt(n)) planned.Add(n);
            else if (empty(n)) blank.Add(n);
            else rest.Add(n);
        }
        var groups = new List<(string, int[])>(3);
        if (planned.Count > 0) groups.Add(("Planned for this look", planned.ToArray()));
        if (blank.Count > 0) groups.Add(("Empty", blank.ToArray()));
        if (rest.Count > 0) groups.Add(("The rest", rest.ToArray()));
        return groups;
    }

    /// <summary>A plate's line under its name in the menu: what it has on, then what's planned for it ("Empty · planned: Fuath at Dusk").</summary>
    /// <param name="on">What it has on ("Empty", a look's name, "8 pieces"), or null when the plates aren't read.</param>
    /// <param name="planned">The look planned for it on the site, or null.</param>
    public static string? PlateSub(string? on, string? planned)
    {
        var plan = planned != null ? $"planned: {planned}" : null;
        if (on == null) return plan;
        return plan == null ? on : $"{on} · {plan}";
    }

    /// <summary>"05": a plate's number as the menu's mono column shows it.</summary>
    public static string PlateNumber(int plate) => plate.ToString("00", En);
}

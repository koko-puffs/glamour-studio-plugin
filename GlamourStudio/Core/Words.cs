using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GlamourStudio.Core;

/// <summary>
/// The window's words for times, counts and what happened, worked out away from the game so they're
/// tested. English whatever the PC's language (the theme's fonts, and the site, are).
/// </summary>
public static class Words
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    /// <summary>"1 piece", "3 pieces"; <paramref name="plural"/> when it isn't the word with an s.</summary>
    public static string Count(int n, string one, string? plural = null) =>
        $"{n.ToString("N0", En)} {(n == 1 ? one : plural ?? one + "s")}";

    /// <summary>A number in words up to twelve ("No", "One", "Two"…), figures past it.</summary>
    public static string InWords(int n)
    {
        string[] words = { "No", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve" };
        return n >= 0 && n < words.Length ? words[n] : n.ToString(En);
    }

    /// <summary>How long ago: "just now", "4 min ago", "3 h ago", "2 days ago", "on 2 Nov".</summary>
    public static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var span = now - at;
        if (span < TimeSpan.FromMinutes(1)) return "just now";
        if (span < TimeSpan.FromHours(1)) return $"{(int)span.TotalMinutes} min ago";
        if (span < TimeSpan.FromDays(1)) return $"{(int)span.TotalHours} h ago";
        if (span < TimeSpan.FromDays(14)) return Count((int)span.TotalDays, "day") + " ago";
        return "on " + at.ToLocalTime().ToString("d MMM", En);
    }

    /// <summary>"A, B and C".</summary>
    public static string List(IEnumerable<string> items)
    {
        var all = items.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        return all.Count switch
        {
            0 => string.Empty,
            1 => all[0],
            _ => string.Join(", ", all.Take(all.Count - 1)) + " and " + all[^1],
        };
    }

    /// <summary>What filling a plate did, in a line: "Plate 3 has 9 of 10 pieces. Press Save in the game's window."</summary>
    /// <param name="set">How many pieces were set on it (not only planned: a slot the game refused isn't).</param>
    public static string Filled(int plate, PlatePlan plan, int? set = null)
    {
        var on = Math.Min(set ?? plan.Ready, plan.Ready);
        var head = on == plan.Wanted
            ? $"Plate {plate} has the whole look ({Count(plan.Wanted, "piece")})."
            : $"Plate {plate} has {on} of the look's {Count(plan.Wanted, "piece")}.";
        return head + " Check it, then press Save in the game's window.";
    }

    /// <summary>What the site kept of a read: "Your closet: 1,204 pieces (12 new)".</summary>
    public static string Synced(SyncResult r)
    {
        var news = new List<string>();
        if (r.Added > 0) news.Add($"{r.Added.ToString("N0", En)} new");
        if (r.Moved > 0) news.Add($"{r.Moved.ToString("N0", En)} moved");
        if (r.Removed > 0) news.Add($"{r.Removed.ToString("N0", En)} gone");
        var tail = news.Count > 0 ? $" ({string.Join(", ", news)})" : string.Empty;
        return $"Your closet on the site: {Count(r.Total, "piece")}{tail}";
    }

    /// <summary>How much of a look you have: "All 8 pieces are yours", "6 of 8 pieces are yours", "None of the 3 pieces is yours yet".</summary>
    public static string Owned(int have, int total) => total == 0
        ? "Nothing on it yet"
        : have == total
            ? total == 1 ? "The piece is yours" : $"All {total} pieces are yours"
            : have == 0
                ? $"None of the {Count(total, "piece")} is yours yet"
                : $"{have} of {Count(total, "piece")} are yours";

    /// <summary>
    /// The plate slip's line for a look with facewear or a fashion accessory: plates can't hold them,
    /// Wear puts them on. Null when it has neither.
    /// </summary>
    public static string? NotOnPlates(Extras extras) => (extras.HasFace, extras.HasFashion) switch
    {
        (true, true) => "Its facewear and fashion accessory don't go on plates: Wear puts them on after the plate.",
        (true, false) => "Its facewear doesn't go on plates: Wear puts it on after the plate.",
        (false, true) => "Its fashion accessory doesn't go on plates: Wear puts it on after the plate.",
        _ => null,
    };

    /// <summary>What Wear did with a look's facewear and fashion accessory, in a line; null when all went on (or there was nothing to do).</summary>
    /// <param name="refused">The names the game didn't put on.</param>
    /// <param name="locked">The names not unlocked on this character (not asked for).</param>
    public static string? ExtrasOutcome(IReadOnlyList<string> refused, IReadOnlyList<string> locked)
    {
        var parts = new List<string>();
        if (locked.Count > 0) parts.Add($"{List(locked)} {(locked.Count == 1 ? "isn't" : "aren't")} unlocked on this character yet");
        if (refused.Count > 0)
            parts.Add($"the game didn't put on {List(refused)} (fashion accessories aren't allowed everywhere, and the game says why in chat)");
        if (parts.Count == 0) return null;
        var line = string.Join("; ", parts);
        return char.ToUpperInvariant(line[0]) + line[1..];
    }

    /// <summary>"Moonlit Scholar is waiting for plate 4" (the dresser's toast), with how many more wait.</summary>
    public static string Waiting(string look, int plate, int more = 0) =>
        $"{look} is waiting for plate {plate.ToString(En)}" + (more > 0 ? $" (and {more.ToString(En)} more)" : string.Empty);

    /// <summary>The send card's eyebrow: "Sent from the site · 3 min ago".</summary>
    public static string SentFromSite(DateTimeOffset? at, DateTimeOffset now) =>
        at is { } t ? $"Sent from the site · {Ago(t, now)}" : "Sent from the site";

    /// <summary>The send card's last line: "by Mira Sol · sent by you on the site" (your own look: "your look · …").</summary>
    public static string SentBy(bool mine, string maker, string? variant = null) =>
        (mine ? "your look" : $"by {maker}") + (variant != null ? $" · in {variant}" : string.Empty) + " · sent by you on the site";

    /// <summary>"4 of 7 pieces on it." ("All 7 pieces on it.", "It's on it.").</summary>
    public static string OnIt(int on, int of) =>
        of <= 0 ? "Nothing of it on it." : on >= of ? (of == 1 ? "It's on it." : $"All {Count(of, "piece")} on it.") : $"{on.ToString(En)} of {Count(of, "piece")} on it.";

    /// <summary>
    /// The slots a fill empties, in one row (the plate's own order): its label (the last slot's, "Rings"
    /// for both rings) and its words ("wrists and rings emptied").
    /// </summary>
    public static (string Label, string Text) Emptied(IReadOnlyList<Slot> slots)
    {
        if (slots.Count == 0) return (string.Empty, string.Empty);
        var rings = slots.Contains(Slot.RingR) && slots.Contains(Slot.RingL);
        var words = new List<string>();
        foreach (var s in slots)
        {
            if (rings && s is Slot.RingR or Slot.RingL)
            {
                if (!words.Contains("rings")) words.Add("rings");
                continue;
            }
            words.Add(Short(s).ToLowerInvariant());
        }
        var label = rings && slots[^1] is Slot.RingR or Slot.RingL ? "Rings" : Short(slots[^1]);
        return (label, $"{List(words)} emptied");
    }

    /// <summary>A slot's short word, as the panel's slot rows say it ("Ears", "Wrists", "Ring R").</summary>
    public static string Short(Slot slot) => slot switch
    {
        Slot.Ears => "Ears",
        Slot.Neck => "Neck",
        Slot.Wrists => "Wrists",
        Slot.RingR => "Ring R",
        Slot.RingL => "Ring L",
        Slot.Main => "Weapon",
        Slot.Off => "Off hand",
        _ => Slots.Word(slot),
    };

    /// <summary>
    /// The pieces still to fetch, in a line: "Halfgloves in the armoury chest, Moonlet on Mog Dahl: fill now
    /// and add them later, or fetch them first". Null when there are none.
    /// </summary>
    public static string? ToFetch(IReadOnlyList<(string Name, string Where)> pieces)
    {
        if (pieces.Count == 0) return null;
        var them = pieces.Count == 1 ? "it" : "them";
        return $"{string.Join(", ", pieces.Select(p => $"{p.Name} {p.Where}"))}: fill now and add {them} later, or fetch {them} first";
    }
}

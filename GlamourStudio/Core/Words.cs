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
}

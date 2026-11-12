using System;
using System.Globalization;

namespace GlamourStudio.UI.Model;

/// <summary>How long something's been going, as a clock reads it (a recording's "4:12"). Pure, so it's tested.</summary>
public static class Elapsed
{
    /// <summary>
    /// Minutes and seconds ("0:07", "4:12"), hours in front once there are any ("1:04:12"); nothing
    /// before nought (a clock set back reads 0:00).
    /// </summary>
    public static string Clock(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        var hours = (int)span.TotalHours;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{span.Minutes:00}:{span.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}:{span.Seconds:00}");
    }
}

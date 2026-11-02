using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace GlamourStudio.Game;

/// <summary>A game window's values as words, for the recorder (as Housing Studio's GameUi does it).</summary>
public static unsafe class AtkText
{
    public static bool IsText(AtkValueType type) => ((int)type & 0xF) is 8 or 0xA;

    public static string Text(byte* ptr)
    {
        if (ptr == null) return string.Empty;
        try
        {
            // Lumina's span reader: no copy of the bytes and no payload objects.
            return new Lumina.Text.ReadOnly.ReadOnlySeStringSpan(ptr).ExtractText().Trim();
        }
        catch
        {
            return Marshal.PtrToStringUTF8((nint)ptr) ?? string.Empty;
        }
    }

    /// <summary>Values as text ("i:1, u:7, s:\"Store\""), the first <paramref name="max"/>.</summary>
    /// <param name="hideText">Text values as "s:…" only (a menu of people's names).</param>
    public static string Describe(AtkValue* values, uint count, int max = 32, bool hideText = false)
    {
        if (values == null) return string.Empty;
        var parts = new List<string>();
        for (var i = 0; i < count && i < max; i++)
        {
            var v = values[i];
            var t = (int)v.Type & 0xF;
            parts.Add(t switch
            {
                0 => "empty",
                2 => $"b:{v.Byte != 0}",
                3 => $"i:{v.Int}",
                4 => $"i64:{v.Int64}",
                5 => $"u:{v.UInt}",
                6 => $"u64:{v.UInt64}",
                7 => $"f:{v.Float}",
                _ when IsText(v.Type) && v.String.Value != null => hideText ? "s:…" : $"s:\"{Text(v.String.Value)}\"",
                _ => $"type{(int)v.Type}",
            });
        }
        if (count > max) parts.Add($"… (+{count - max})");
        return string.Join(", ", parts);
    }

    /// <summary>Every text value a window holds, in order, the first <paramref name="max"/>.</summary>
    public static List<string> Strings(AtkUnitBase* a, int max = 40)
    {
        var list = new List<string>();
        if (a == null || a->AtkValues == null) return list;
        for (var i = 0; i < a->AtkValuesCount && list.Count < max; i++)
        {
            var v = a->AtkValues[i];
            if (IsText(v.Type) && v.String.Value != null)
            {
                var s = Text(v.String.Value);
                if (s.Length > 0) list.Add($"[{i}] {s}");
            }
        }
        return list;
    }

    /// <summary>Every value a window holds now, each "[i] value", the first <paramref name="max"/>.</summary>
    public static List<string> Values(string name, int max = 4000)
    {
        var list = new List<string>();
        try
        {
            var a = (AtkUnitBase*)Services.GameGui.GetAddonByName(name).Address;
            if (a == null || a->AtkValues == null) return list;
            var count = Math.Min((int)a->AtkValuesCount, max);
            for (var i = 0; i < count; i++) list.Add($"[{i}] {Describe(&a->AtkValues[i], 1)}");
        }
        catch (Exception ex)
        {
            list.Add($"couldn't read ({ex.GetType().Name})");
        }
        return list;
    }
}

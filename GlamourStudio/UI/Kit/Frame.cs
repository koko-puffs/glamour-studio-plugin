using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// What the window's frame keeps between its parts: the ImGui children and popups the kit has open
/// (so a frame that threw part way can end them), and the toast over the window's foot.
/// </summary>
public static class Frame
{
    /// <summary>At the start of each frame: nothing left over from a frame whose drawing threw part way.</summary>
    public static void ResetFrame()
    {
        Open.Clear();
    }

    /// <summary>The ImGui children and popups the kit has open now, innermost last (1.0.0).</summary>
    internal static readonly Stack<bool> Open = new();

    /// <summary>
    /// Ends what the kit left open (a view that threw between a Begin and its End), innermost first, so
    /// the window ends with ImGui's stack as it found it rather than with a child or popup still open
    /// (which ImGui asserts on). Nothing to do on a frame drawn to the end.
    /// </summary>
    public static void Unwind()
    {
        while (Open.Count > 0)
        {
            if (Open.Pop()) ImGui.EndPopup();
            else ImGui.EndChild();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Toasts: one line in the float material, near the window's foot, for a few seconds.

    private static string? toast;
    private static DateTime toastUntil;

    public static void Toast(string text)
    {
        toast = text;
        toastUntil = DateTime.UtcNow + TimeSpan.FromSeconds(2.6);
    }

    public static void DrawToast()
    {
        if (toast == null) return;
        var left = (toastUntil - DateTime.UtcNow).TotalSeconds;
        if (left <= 0)
        {
            toast = null;
            return;
        }
        var fade = (float)Math.Clamp(left / 0.3, 0, 1);
        var old = Palette.Opacity;
        Palette.Opacity = ReducedMotion ? 1 : fade;
        var win = ImGui.GetWindowPos();
        // Within the window (a long message is cut short), 28px up from its foot.
        var text = Txt.Fit(toast, Face.Sans, 13, MathF.Max(S(80), ImGui.GetWindowWidth() - S(56) - S(16) * 2));
        var size = Txt.Measure(text, Face.Sans, 13);
        var box = new Vector2(size.X + S(16) * 2, S(36));
        var bottom = win.Y + ImGui.GetWindowHeight();
        var y = bottom - box.Y - S(28);
        var pos = new Vector2(win.X + (ImGui.GetWindowWidth() - box.X) / 2, y);
        var dl = ImGui.GetForegroundDrawList();
        // shadow-float, softly.
        for (var i = 3; i >= 1; i--)
            dl.AddRectFilled(pos + new Vector2(0, S(6 * i)), pos + box + new Vector2(0, S(6 * i)), Palette.U32(Palette.WithAlpha(Palette.Ground, 0.12f)), box.Y / 2 + S(4 * i));
        dl.AddRectFilled(pos, pos + box, Palette.U32(Palette.Over(Palette.Float, Palette.Ground)), box.Y / 2);
        dl.AddRect(pos, pos + box, Palette.U32(Palette.Line), box.Y / 2);
        using (Txt.Fonts.Push(Face.Sans, 13))
            dl.AddText(pos + new Vector2(S(16), (box.Y - ImGui.GetFontSize()) / 2), Palette.U32(Palette.Ink), text);
        Palette.Opacity = old;
    }
}

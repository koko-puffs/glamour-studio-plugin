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
    // Toasts: one at a time, a pill of raised ink rising at the window's foot, for four seconds.

    private static string? toast;
    private static ToastKind toastKind;
    private static DateTime toastFrom;
    private static DateTime toastUntil;

    /// <summary>Good news (with the holo sparkle at its start).</summary>
    public static void Toast(string text) => Toast(text, ToastKind.Good);

    /// <summary>
    /// A toast of a kind: good news starts with the holo sparkle; a warning or a failure is amber
    /// with a warning sign, never the sparkle (a failure mustn't twinkle).
    /// </summary>
    public static void Toast(string text, ToastKind kind)
    {
        toast = text;
        toastKind = kind;
        toastFrom = DateTime.UtcNow;
        toastUntil = toastFrom + TimeSpan.FromSeconds(4);
    }

    /// <summary>A warning or failure toast (amber, the warning sign).</summary>
    public static void Warn(string text) => Toast(text, ToastKind.Warn);

    /// <summary>The toast (the mock's <c>.toast</c>), over the window's foot, 28px up from it.</summary>
    public static void DrawToast()
    {
        if (toast == null) return;
        var now = DateTime.UtcNow;
        var left = (toastUntil - now).TotalSeconds;
        if (left <= 0)
        {
            toast = null;
            return;
        }
        var shown = (now - toastFrom).TotalSeconds;
        var fade = (float)Math.Clamp(Math.Min(left / 0.3, shown / 0.18), 0, 1);
        var old = Palette.Opacity;
        Palette.Opacity = ReducedMotion ? 1 : fade;
        try
        {
            var win = ImGui.GetWindowPos();
            var warn = toastKind == ToastKind.Warn;
            var lead = S(16) + S(10);
            // Within the window (a long message is cut short).
            var text = Txt.Fit(toast, Face.SansMedium, 13, MathF.Max(S(80), ImGui.GetWindowWidth() - S(56) - S(13 + 16) - lead));
            var size = Txt.Measure(text, Face.SansMedium, 13);
            var box = new Vector2(S(13) + lead + size.X + S(16), S(40));
            var bottom = win.Y + ImGui.GetWindowHeight();
            // Rising into place as it comes.
            var rise = ReducedMotion ? 0 : S(10) * (1 - (float)Math.Clamp(shown / 0.18, 0, 1));
            var pos = new Vector2(win.X + (ImGui.GetWindowWidth() - box.X) / 2, bottom - box.Y - S(28) + rise);
            var dl = ImGui.GetForegroundDrawList();
            dl.AddRectFilled(pos, pos + box, Palette.U32(Palette.Popup), box.Y / 2);
            dl.AddRect(pos + new Vector2(0.5f), pos + box - new Vector2(0.5f), Palette.U32(warn ? Palette.AmberLine : Palette.LineStrong), box.Y / 2, ImDrawFlags.None, 1f);
            var ip = new Vector2(pos.X + S(13), pos.Y + (box.Y - S(16)) / 2);
            if (warn) Icons.Draw(dl, Icon.AlertTriangle, ip, S(16), Palette.Amber);
            else
            {
                var tex = Textures.Current?.Get(Tex.Spark);
                if (tex != null) dl.AddImage(tex.Handle, ip, ip + new Vector2(S(16)), Vector2.Zero, Vector2.One, Palette.U32(Vector4.One));
                else Icons.Draw(dl, Icon.Sparkles, ip, S(16), Palette.Accent);
            }
            using (Txt.Fonts.Push(Face.SansMedium, 13))
                dl.AddText(new Vector2(MathF.Round(pos.X + S(13) + lead), MathF.Round(pos.Y + (box.Y - ImGui.GetFontSize()) / 2)), Palette.U32(Palette.Ink), text);
        }
        finally
        {
            Palette.Opacity = old;
        }
    }
}

/// <summary>A toast's kind.</summary>
public enum ToastKind
{
    /// <summary>Good news: the holo sparkle.</summary>
    Good,

    /// <summary>A warning or a failure: amber, the warning sign.</summary>
    Warn,
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace GlamourStudio.UI;

/// <summary>
/// Pushes the window's look onto ImGui. Before ImGui.Begin only what can't touch the title bar's
/// buttons: colours (the ink's second step for Dalamud's title bar, the ink for the window, the
/// hairline edge), the mock's 12px window radius and no window padding. The title bar keeps Dalamud's own font and frame padding, so its ☰, ▼ and
/// × sit and hit exactly as in every other Dalamud window (0.3.0 pushed a 13px font and a 12px frame
/// padding there, which made each button's hit box about 41px wide while they stand 17px apart).
/// Inside the window: no item spacing (every gap is the mockup's own, placed by the views), and
/// ImGui's own controls in the mock's colours: popups and menus of raised ink with the strong
/// hairline and 16px corners, 6px scrollbars, the field's periwinkle for what's ticked or dragged.
/// </summary>
public static class Theme
{
    /// <summary>CSS pixels to screen pixels (Dalamud's global scale).</summary>
    public static float S(float px) => px * ImGuiHelpers.GlobalScale;

    public static bool ReducedMotion => Services.PluginInterface.UiBuilder.ShouldUseReducedMotion;

    private static readonly Stack<(int Colors, int Vars, IDisposable? Font)> Pushed = new();

    /// <summary>Before ImGui.Begin: the frame's colours, radius, edge and padding; nothing that sizes the title bar.</summary>
    public static void PushFrame()
    {
        var colors = 0;
        var vars = 0;
        void C(ImGuiCol col, Vector4 v)
        {
            ImGui.PushStyleColor(col, v);
            colors++;
        }
        void V(ImGuiStyleVar v, Vector2 value)
        {
            ImGui.PushStyleVar(v, value);
            vars++;
        }
        void F(ImGuiStyleVar v, float value)
        {
            ImGui.PushStyleVar(v, value);
            vars++;
        }

        C(ImGuiCol.WindowBg, Palette.Ground);
        C(ImGuiCol.Border, Palette.Line);
        C(ImGuiCol.BorderShadow, Vector4.Zero);
        C(ImGuiCol.TitleBg, Palette.Ground2);
        C(ImGuiCol.TitleBgActive, Palette.Ground2);
        C(ImGuiCol.TitleBgCollapsed, Palette.Ground2);
        C(ImGuiCol.Text, Palette.Ink);

        F(ImGuiStyleVar.WindowRounding, S(12));
        F(ImGuiStyleVar.WindowBorderSize, 1f);
        V(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        Pushed.Push((colors, vars, null));
    }

    /// <summary>Inside the window: no spacing of ImGui's own, and its controls in the tokens' colours.</summary>
    public static void PushInside()
    {
        var colors = 0;
        var vars = 0;
        void C(ImGuiCol col, Vector4 v)
        {
            ImGui.PushStyleColor(col, v);
            colors++;
        }
        void V(ImGuiStyleVar v, Vector2 value)
        {
            ImGui.PushStyleVar(v, value);
            vars++;
        }
        void F(ImGuiStyleVar v, float value)
        {
            ImGui.PushStyleVar(v, value);
            vars++;
        }

        C(ImGuiCol.Text, Palette.Body);
        C(ImGuiCol.TextDisabled, Palette.Faint);
        C(ImGuiCol.ChildBg, Vector4.Zero);
        C(ImGuiCol.PopupBg, Palette.Popup);
        C(ImGuiCol.Border, Palette.LineStrong);
        C(ImGuiCol.FrameBg, Vector4.Zero);
        C(ImGuiCol.FrameBgHovered, Vector4.Zero);
        C(ImGuiCol.FrameBgActive, Vector4.Zero);
        C(ImGuiCol.TextSelectedBg, Palette.WithAlpha(Palette.Accent, 0.3f));
        C(ImGuiCol.NavHighlight, Palette.Accent);
        C(ImGuiCol.Header, Palette.AccentWash);
        C(ImGuiCol.HeaderHovered, Palette.AccentWash);
        C(ImGuiCol.HeaderActive, Palette.LinePicked);
        C(ImGuiCol.Button, Palette.Tile);
        C(ImGuiCol.ButtonHovered, Palette.AccentWash);
        C(ImGuiCol.ButtonActive, Palette.LinePicked);
        C(ImGuiCol.CheckMark, Palette.Field);
        C(ImGuiCol.SliderGrab, Palette.Field);
        C(ImGuiCol.SliderGrabActive, Palette.FieldHi);
        C(ImGuiCol.Separator, Palette.Line);
        C(ImGuiCol.ModalWindowDimBg, Palette.WithAlpha(Palette.Ground, 0.75f));
        C(ImGuiCol.ScrollbarBg, Vector4.Zero);
        C(ImGuiCol.ScrollbarGrab, Palette.LineStrong);
        C(ImGuiCol.ScrollbarGrabHovered, Palette.LineHover);
        C(ImGuiCol.ScrollbarGrabActive, Palette.Muted);

        V(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        V(ImGuiStyleVar.ItemInnerSpacing, Vector2.Zero);
        V(ImGuiStyleVar.FramePadding, Vector2.Zero);
        F(ImGuiStyleVar.FrameBorderSize, 0f);
        F(ImGuiStyleVar.ChildBorderSize, 0f);
        F(ImGuiStyleVar.PopupRounding, S(Palette.RadiusMenu));
        F(ImGuiStyleVar.PopupBorderSize, 1f);
        V(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        F(ImGuiStyleVar.ScrollbarSize, S(6));
        F(ImGuiStyleVar.ScrollbarRounding, S(999));
        F(ImGuiStyleVar.GrabRounding, S(999));
        Pushed.Push((colors, vars, null));
    }

    public static void Pop()
    {
        if (Pushed.Count == 0) return;
        var (colors, vars, font) = Pushed.Pop();
        if (vars > 0) ImGui.PopStyleVar(vars);
        if (colors > 0) ImGui.PopStyleColor(colors);
        font?.Dispose();
    }

    /// <summary>A slow pulse, 0 to 1, for "this is live" (still under reduced motion).</summary>
    public static float Pulse()
    {
        if (ReducedMotion) return 0.6f;
        return 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 3.2f);
    }
}

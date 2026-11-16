using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// A look sent from the site waiting at the glamour dresser (0.8.0, the frame's toast): opening the dresser,
/// a pill at the top of the screen says so ("Moonlit Scholar is waiting for plate 4") with
/// <b>Open Glamour Plates</b>, which presses the dresser's own button and picks that plate (as Plate it
/// does, <see cref="StartPlateIt"/>) and stops there: the panel beside it shows the send, and
/// <b>Fill plate 4</b> waits for its press. Gone once Glamour Plates is open or the dresser closes.
/// </summary>
public sealed partial class MainWindow
{
    private bool wasAtDresser;

    /// <summary>At a glamour dresser: the plans read again on coming to it, then every minute (a send from your phone shows).</summary>
    private void WatchDresser()
    {
        var at = PlateEditor.IsOpen || GameWindows.IsShown("MiragePrismPrismBox");
        if (at) sync.WakePlans(!wasAtDresser);
        wasAtDresser = at;
    }

    private const string ToastAct = "Open Glamour Plates";

    private void DrawSendToast()
    {
        if (!Linked || PlateEditor.IsOpen || plateIt != null || AutoBusy || !GameWindows.IsShown("MiragePrismPrismBox")) return;
        var waiting = sync.WaitingSends;
        if (waiting.Count == 0) return;
        var send = waiting[0];

        // The plugin's toast (Frame.DrawToast): a 40 tall pill of raised ink, the holo sparkle, 13px words;
        // then a hairline and the action in lilac.
        var vp = ImGui.GetMainViewport();
        var lead = S(16) + S(10);
        var actW = Txt.Measure(ToastAct, Face.SansSemibold, 13).X;
        var tail = S(4 + 12) + actW;
        var text = Txt.Fit(Words.Waiting(send.Look.Name, send.Plate, waiting.Count - 1), Face.SansMedium, 13,
            MathF.Max(S(120), vp.Size.X * 0.6f - lead - tail));
        var textW = Txt.Measure(text, Face.SansMedium, 13).X;
        var box = new Vector2(S(13) + lead + textW + S(10) + tail + S(16), S(40));
        var pos = new Vector2(vp.Pos.X + (vp.Size.X - box.X) / 2, vp.Pos.Y + S(24));

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(box, ImGuiCond.Always);
        Theme.PushFrame();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, box.Y / 2);
        var shown = ImGui.Begin("##gs-send-toast", CompanionFlags | ImGuiWindowFlags.NoBackground);
        ImGui.PopStyleVar();
        Theme.Pop();
        try
        {
            if (!shown) return;
            Theme.PushInside();
            try
            {
                var dl = Kit.Dl;
                dl.AddRectFilled(pos, pos + box, Palette.U32(Palette.Popup), box.Y / 2);
                dl.AddRect(pos + new Vector2(0.5f), pos + box - new Vector2(0.5f), Palette.U32(Palette.LineStrong), box.Y / 2, ImDrawFlags.None, 1f);
                var ip = new Vector2(pos.X + S(13), pos.Y + (box.Y - S(16)) / 2);
                var tex = Textures.Current?.Get(Tex.Spark);
                if (tex != null) dl.AddImage(tex.Handle, ip, ip + new Vector2(S(16)), Vector2.Zero, Vector2.One, Palette.U32(Vector4.One));
                else Icons.Draw(dl, Icon.Sparkles, ip, S(16), Palette.Accent);
                var x = pos.X + S(13) + lead;
                Txt.Line(dl, new Vector2(x, pos.Y), text, Face.SansMedium, 13, Palette.Ink, box.Y / S(1));
                x += textW + S(10) + S(4);
                dl.AddLine(new Vector2(x, pos.Y + S(12)), new Vector2(x, pos.Y + box.Y - S(12)), Palette.U32(Palette.LineStrong), 1f);
                x += S(12);
                var actPos = new Vector2(x - S(6), pos.Y);
                var (clicked, hovered, _) = Kit.Hit("##gs-send-toast-open", actPos, new Vector2(actW + S(12), box.Y));
                if (hovered) Kit.Tooltip($"Opens Glamour Plates on plate {send.Plate}: the panel beside it fills it when you press Fill. You press Save in the game.");
                Txt.Line(dl, new Vector2(x, pos.Y), ToastAct, Face.SansSemibold, 13, hovered ? Palette.AccentHover : Palette.Accent, box.Y / S(1));
                Kit.FocusRing(actPos, actPos + new Vector2(actW + S(12), box.Y), box.Y / 2);
                if (clicked)
                {
                    var look = send.Look;
                    var plate = send.Plate;
                    Later(() => StartPlateIt(look, plate, openOnly: true));
                }
            }
            finally
            {
                Theme.Pop();
            }
        }
        finally
        {
            ImGui.End();
        }
    }
}

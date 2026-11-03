using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Your characters and the site: on a character the plugin hasn't been told about (linked), a small card
/// of its own at the top of the screen, whether the plugin's window is open or not, asks whether to send
/// that character's wardrobe to Glamour Studio (Yes, send it · No · Not now); nothing of it is sent until
/// you say yes. Settings → Account lists every character you answered for, each with its switch.
/// </summary>
public sealed partial class MainWindow
{
    private float questionH;

    /// <summary>The question about the character you're on (drawn every frame by Dalamud, as the panels are).</summary>
    public void DrawCharacterQuestion()
    {
        if (sync.ToAsk is not { } c || CompanionsAway()) return;
        try
        {
            DrawQuestionCard(c);
        }
        catch (Exception ex)
        {
            Frame.Unwind();
            Services.Log.Warning(ex, "Glamour Studio couldn't ask about this character");
        }
    }

    private void DrawQuestionCard(CharacterWardrobe c)
    {
        var vp = ImGui.GetMainViewport();
        var width = MathF.Min(S(440), vp.Size.X - S(32));
        var height = questionH > 0 ? questionH : S(220);
        ImGui.SetNextWindowPos(new Vector2(vp.Pos.X + (vp.Size.X - width) / 2, vp.Pos.Y + S(72)), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        Theme.PushFrame();
        var shown = ImGui.Begin("##gs-character-question", CompanionFlags);
        Theme.Pop();
        try
        {
            if (!shown) return;
            Theme.PushInside();
            try
            {
                using (fonts.Push(Face.Sans, 14))
                {
                    Kit.ResetFrame();
                    var win = ImGui.GetWindowPos();
                    var dl = Kit.Dl;
                    var r = S(Palette.RadiusLg);
                    dl.AddRectFilled(win, win + new Vector2(width, height), Palette.U32(Palette.Card), r);
                    dl.AddRect(win, win + new Vector2(width, height), Palette.U32(Palette.RoseEdge), r);
                    var pad = S(20);
                    var inner = width - pad * 2;
                    var at = win + new Vector2(pad, pad);
                    ImGui.SetCursorScreenPos(at);
                    Kit.PushBox(at.X, inner);
                    Kit.EyebrowRow(Consent.Eyebrow(c.World), Palette.Rose);
                    Kit.Gap(8);
                    var tAt = Kit.At;
                    var titleH = Title(tAt, Consent.Question(c.Name), inner, 18, 3);
                    Kit.Reserve(tAt, inner, titleH);
                    Kit.Gap(10);
                    Txt.Para(new[] { new Run(Consent.QuestionBody, Face.Sans, 13, Palette.Soft) }, 19.5f);
                    Kit.Gap(16);
                    var row = Kit.At;
                    const string yes = "Yes, send it", no = "No", later = "Not now";
                    var yesW = Kit.ButtonWidth(yes, BtnSize.Md, null, Btn.Primary);
                    var noW = Kit.ButtonWidth(no);
                    var key = c.Key;
                    var (name, world) = (c.Name, c.World);
                    if (Kit.Button("##q-later", row, later, Btn.Quiet, BtnSize.Md, null, 0, false,
                            "Not asked again until the plugin loads again (the next time you start the game). Nothing's sent meanwhile."))
                        Later(() => sync.NotNow(key));
                    if (Kit.Button("##q-no", new Vector2(row.X + inner - yesW - S(8) - noW, row.Y), no, Btn.Glass, BtnSize.Md, null, 0, false,
                            "Nothing of this character goes to the site (what an older plugin sent of it is taken off). Change it in Settings → Account."))
                        Later(() =>
                        {
                            sync.Answer(key, name, world, false);
                            Services.Chat.Print($"Glamour Studio: nothing of {(name.Length > 0 ? name : "this character")} goes to the site. Change it in Settings → Account.");
                        });
                    if (Kit.Button("##q-yes", new Vector2(row.X + inner - yesW, row.Y), yes, Btn.Primary))
                        Later(() =>
                        {
                            sync.Answer(key, name, world, true);
                            Services.Chat.Print($"Glamour Studio: {(name.Length > 0 ? name : "this character")}'s wardrobe goes to your closet on the site (private unless you show it there).");
                        });
                    Kit.Reserve(row, inner, Kit.ButtonHeight(BtnSize.Md));
                    Kit.PopBox();
                    var h = Kit.At.Y - win.Y + pad;
                    if (MathF.Abs(h - questionH) > 0.5f) questionH = h;
                }
            }
            finally
            {
                Frame.Unwind();
                Kit.ResetFrame();
                Theme.Pop();
            }
        }
        finally
        {
            ImGui.End();
        }
        RunLater();
    }

    /// <summary>Settings → Account: each character you answered for (and the one you're on), with its switch.</summary>
    private void DrawCharactersGroup()
    {
        var me = wardrobe.Current;
        var keys = config.Characters.Keys.ToList();
        if (me != null && me.Name.Length > 0 && !keys.Contains(me.Key)) keys.Insert(0, me.Key);
        if (keys.Count == 0) return;
        // The one you're on first, then by name.
        keys = keys.OrderBy(k => k == me?.Key ? 0 : 1)
            .ThenBy(k => config.Characters.TryGetValue(k, out var x) ? x.Name : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Kit.Gap(20);
        GroupLabel("Characters");
        Kit.BeginPanel("##set-characters", 0, 0, 0);
        var i = 0;
        foreach (var key in keys)
        {
            config.Characters.TryGetValue(key, out var known);
            var current = me?.Key == key;
            var name = current && me!.Name.Length > 0 ? me.Name : known?.Name is { Length: > 0 } n ? n : "A character";
            var world = current && me!.World.Length > 0 ? me.World : known?.World ?? string.Empty;
            var step = Consent.Decide(sync.MyAnswer(key), sync.SiteChoice(key));
            var on = step is ConsentStep.Send or ConsentStep.TellYes;
            var state = step switch
            {
                ConsentStep.Send => "Sent to the site",
                ConsentStep.TellYes => "Yes · telling the site",
                ConsentStep.TellNo => "No · telling the site",
                ConsentStep.Hold => "Not sent",
                ConsentStep.AskPlayer => "Not asked yet",
                _ => sync.Linked ? "Checking with the site…" : "Link your account to send it",
            };
            var line = $"{(world.Length > 0 ? world + " · " : string.Empty)}{state}{(current ? " · you're on it" : string.Empty)}";
            var (k2, nm, wd) = (key, name, world);
            if (SwitchRow(i++, $"##char-{key}", name, line, on,
                    "On: its glamour dresser, armoire, bags, armoury chest, saddlebags, retainers, plates and outfit go to your closet on the site, private unless you show the character there. "
                    + "Off: nothing of it is sent, and the site lets go of what it had of it.",
                    !sync.Linked))
                Later(() => sync.Answer(k2, nm, wd, !on));
        }
        Kit.EndPanel();
        Kit.Gap(10);
        Txt.Para(new[] { new Run("What's shown of each on the site, and where: Settings → Glamour Studio → Characters there.", Face.Sans, 12, Palette.Muted) }, 18);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Your characters and the site (docs/redesign/pages/characters.html): on a character the plugin
/// hasn't been told about (linked), a card of its own at the top of the screen, whether the plugin's
/// window is open or not, asks whether to send that character's wardrobe to Glamour Studio (Yes, send
/// it, the sticker · No · Not now); nothing of it is sent until you say yes. Settings → Account lists
/// every character you answered for, each with its face, where it stands and its switch.
/// </summary>
public sealed partial class MainWindow
{
    private float questionH;

    /// <summary>What goes, and that it stays private (the question's two paragraphs).</summary>
    private const string QuestionWhat =
        "Its glamour dresser, armoire, bags, armoury chest, saddlebags, retainers, plates and outfit go to your closet on the site, so it knows what you own.";

    private const string QuestionPrivate = "It stays private unless you show the character there. Nothing is sent until you say yes.";

    private static readonly Run[] QuestionWhatRun = { new(QuestionWhat, Face.Sans, 14, Palette.Body) };
    private static readonly Run[] QuestionPrivateRun = { new(QuestionPrivate, Face.Sans, 13, Palette.Muted) };

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

    /// <summary>
    /// The card (the mock's <c>.ask</c>): 440 wide at the top of the screen, ink with 28px corners and
    /// the strong hairline (no glow), the logo and an eyebrow saying why it's asking, the question in
    /// display capitals, what goes, that it stays private, and the answers: Not now (quiet), No
    /// (outlined), Yes, send it (the sticker).
    /// </summary>
    private void DrawQuestionCard(CharacterWardrobe c)
    {
        var vp = ImGui.GetMainViewport();
        var width = MathF.Min(S(440), vp.Size.X - S(32));
        var height = questionH > 0 ? questionH : S(240);
        ImGui.SetNextWindowPos(new Vector2(vp.Pos.X + (vp.Size.X - width) / 2, vp.Pos.Y + S(40)), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        // The window is the card: ImGui draws its ink, corners and hairline.
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Palette.Card);
        ImGui.PushStyleColor(ImGuiCol.Border, Palette.LineStrong);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, S(Palette.RadiusXl));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        var shown = ImGui.Begin("##gs-character-question", CompanionFlags);
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(3);
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
                    var padX = S(24);
                    var inner = width - padX * 2;
                    var at = win + new Vector2(padX, S(22));
                    ImGui.SetCursorScreenPos(at);
                    Kit.PushBox(at.X, inner);

                    // The logo and why it's asking.
                    Kit.Logo(at, 22);
                    Kit.Eyebrow(new Vector2(at.X + S(22 + 10), at.Y + (S(22) - Kit.EyebrowHeight) / 2), Consent.Eyebrow(c.World), Palette.Accent, inner - S(32));
                    Kit.Reserve(at, inner, S(22));
                    Kit.Gap(10);
                    var tAt = Kit.At;
                    var titleH = Title(tAt, Consent.Question(c.Name), inner, 22, 3);
                    Kit.Reserve(tAt, inner, titleH);
                    Kit.Gap(10);
                    Txt.Para(QuestionWhatRun, 21);
                    Kit.Gap(8);
                    Txt.Para(QuestionPrivateRun, 19.5f);
                    Kit.Gap(18);

                    var row = Kit.At;
                    const string yes = "Yes, send it", no = "No", later = "Not now";
                    var yesW = Kit.StickerWidth(yes);
                    var noW = Kit.ButtonWidth(no);
                    var rowH = Kit.ButtonHeight(BtnSize.Md);
                    var key = c.Key;
                    var (name, world) = (c.Name, c.World);
                    if (Kit.Button("##q-later", new Vector2(row.X, row.Y + (rowH - Kit.ButtonHeight(BtnSize.Sm)) / 2), later, Btn.Quiet, BtnSize.Sm, null, 0, false,
                            "Not asked again until the plugin loads again (the next time you start the game). Nothing's sent meanwhile."))
                        Later(() => sync.NotNow(key));
                    if (Kit.Button("##q-no", new Vector2(row.X + inner - yesW - S(8) - noW, row.Y), no, Btn.Glass, BtnSize.Md, null, 0, false,
                            "Nothing of this character goes to the site (what an older plugin sent of it is taken off). Change it in Settings → Account."))
                        Later(() =>
                        {
                            sync.Answer(key, name, world, false);
                            Services.Chat.Print($"Glamour Studio: nothing of {(name.Length > 0 ? name : "this character")} goes to the site. Change it in Settings → Account.");
                        });
                    if (Kit.Sticker("##q-yes", new Vector2(row.X + inner - yesW, row.Y), yes, StickerSize.Md, null, 0, false,
                            "Told to the site, then the closet goes at once, and its plans and Wear today are read as this character."))
                        Later(() =>
                        {
                            sync.Answer(key, name, world, true);
                            Services.Chat.Print($"Glamour Studio: {(name.Length > 0 ? name : "this character")}'s wardrobe goes to the site now (private unless you show it).");
                        });
                    Kit.Reserve(row, inner, rowH);
                    Kit.PopBox();
                    var h = Kit.At.Y - win.Y + S(20);
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

    /// <summary>The characters' initials, worked out once a name.</summary>
    private readonly Dictionary<string, string> characterInitials = new(StringComparer.Ordinal);

    private string CharacterInitials(string name)
    {
        if (characterInitials.TryGetValue(name, out var i)) return i;
        if (characterInitials.Count > 64) characterInitials.Clear();
        return characterInitials[name] = Model.Initials.Of(name);
    }

    private static readonly Run[] CharactersNote =
    {
        new("On: its glamour dresser, armoire, bags, armoury chest, saddlebags, retainers, plates and outfit go to your closet on the site, private unless you show the character there. "
            + "What's shown of each: the site's Settings → Glamour Studio → Characters.", Face.Sans, 12, Palette.Faint),
    };

    private const string CharacterTip =
        "On: its glamour dresser, armoire, bags, armoury chest, saddlebags, retainers, plates and outfit go to your closet on the site, private unless you show the character there. "
        + "Off: nothing of it is sent, and the site lets go of what it had of it.";

    /// <summary>
    /// Settings → Account → Characters: the one you're on first, then each you've answered for by name,
    /// each its face (on the field's violet when it's sent), its world and where it stands, and its
    /// switch. The card at half strength while not linked.
    /// </summary>
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

        GroupLabel("Characters");
        var dim = !sync.Linked;
        var old = Palette.Opacity;
        if (dim) Palette.Opacity = old * 0.5f;
        try
        {
            Kit.BeginPanel("##set-characters", 0, 18, 0);
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
                    ConsentStep.TellYes => "Yes · telling the site…",
                    ConsentStep.TellNo => "No · telling the site…",
                    ConsentStep.Hold => "Not sent",
                    ConsentStep.AskPlayer => "Not asked yet",
                    _ => sync.Linked ? "Checking with the site…" : "Link your account to send it",
                };
                var line = world.Length > 0 ? $"{world} · {state}" : state;
                var (k2, nm, wd) = (key, name, world);
                if (CharacterRow(i++, $"##char-{key}", name, line, step == ConsentStep.AskPlayer, current, on, !sync.Linked))
                    Later(() => sync.Answer(k2, nm, wd, !on));
            }
            Kit.EndPanel();
        }
        finally
        {
            Palette.Opacity = old;
        }
        Kit.Gap(12);
        var at = Kit.At;
        var w = Kit.Width - S(4);
        var h = Txt.Flow(new Vector2(at.X + S(2), at.Y), w, CharactersNote, 18, out _);
        Kit.Reserve(at, Kit.Width, h);
    }

    /// <summary>
    /// A character's row (the mock's <c>.char</c>, 58 tall): its face (34), its name (and "you're on
    /// it"), its world and where it stands (amber when it's not asked yet), its switch. True when flipped.
    /// </summary>
    private bool CharacterRow(int index, string id, string name, string line, bool amber, bool current, bool on, bool disabled)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var h = S(58);
        var dl = Kit.Dl;
        if (index > 0) Kit.Divider(at.X, at.X + w, at.Y);
        Kit.Avatar(new Vector2(at.X + S(17), at.Y + h / 2), CharacterInitials(name), 34, off: !on);
        var x = at.X + S(34 + 12);
        var wordsW = MathF.Max(S(40), w - S(34 + 12) - S(40 + 12));
        var wordsH = S(18) + S(17);
        var top = at.Y + (h - wordsH) / 2;
        const string here = "you're on it";
        var tagW = current ? Kit.TagWidth(here) + S(8) : 0;
        var nw = Txt.Line(dl, new Vector2(x, top), Txt.Fit(name, Face.SansSemibold, 13, MathF.Max(S(20), wordsW - tagW)), Face.SansSemibold, 13, Palette.Ink, 18);
        if (current) Kit.Tag(new Vector2(x + nw + S(8), top + (S(18) - Kit.TagHeight()) / 2), here, TagKind.Ink);
        Txt.Line(dl, new Vector2(x, top + S(18)), Txt.Fit(line, Face.Sans, 12, wordsW), Face.Sans, 12, amber ? Palette.Amber : Palette.Muted, 17);
        if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(x, top), new Vector2(x + wordsW, top + wordsH)))
            Kit.Tooltip(CharacterTip);
        var flipped = Kit.Switch(id, new Vector2(at.X + w - S(40), at.Y + (h - S(24)) / 2), on, disabled);
        Kit.Reserve(at, w, h);
        return flipped;
    }
}

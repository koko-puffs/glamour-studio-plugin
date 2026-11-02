using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// One of your glamour plates (or what you're wearing, plate 0) as the game has it: its pieces, the
/// look it is if it's one of yours, and what to do with it: keep it as a look on the site, try it on,
/// wear it.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>The plate open (1 to 20), 0 for what you're wearing, −1 for none.</summary>
    private int openPlate = -1;

    /// <summary>A look kept from the game just now, by the plate it came from.</summary>
    private readonly Dictionary<int, SavedLook> keptFrom = new();

    private bool keeping;

    private void OpenPlate(int plate)
    {
        openPlate = plate;
        screen = Screen.Plate;
        IsOpen = true;
    }

    private void DrawPlate(int plate)
    {
        var me = wardrobe.Current;
        var pieces = plate == 0 ? me?.Outfit : me?.Plates.FirstOrDefault(p => p.Plate == plate)?.Pieces;
        Frame.BeginHeader(20);
        if (ScreenHeader("Back to your plates", plate == 0 ? "What you're wearing" : "Glamour plate", plate == 0 ? "Your outfit" : $"Plate {plate}"))
        {
            screen = Screen.Home;
            homeTab = HomeTab.Plates;
        }
        Frame.EndHeader();

        Frame.BeginBody("##plate", null, 18, 24);
        if (me == null || pieces == null)
        {
            Txt.Note("Log in to a character to see its plates.");
            Frame.EndBody();
            return;
        }
        var all = sync.Library.Mine.Concat(sync.Library.Loved).ToList();
        var match = Plates.Matching(pieces, all);
        if (match != null)
        {
            var runs = new[]
            {
                new Run("It's your look ", Face.Sans, 13, Palette.Muted),
                new Run($"“{match.Name}”", Face.SansMedium, 13, Palette.Rose, Link: "look"),
                new Run(".", Face.Sans, 13, Palette.Muted),
            };
            if (Txt.Para(runs) == "look") Later(() => OpenLook(match, null));
            Kit.Gap(14);
        }

        var row = Kit.At;
        var x = row.X;
        if (pieces.Count > 0 && match == null)
        {
            var name = plate == 0 ? (me.Name.Length > 0 ? $"{me.Name}'s outfit" : "My outfit") : $"Plate {plate}";
            if (keptFrom.TryGetValue(plate, out var kept))
            {
                if (Kit.Button("##kept", row, "Open it on the site", Btn.Glass, BtnSize.Md, Icon.ExternalLink, 0, false, $"Kept as “{kept.Name}” in your looks"))
                    Dalamud.Utility.Util.OpenLink(SiteLink.DressingUrl(kept.Id));
                x += Kit.ButtonWidth("Open it on the site", BtnSize.Md, Icon.ExternalLink) + S(8);
            }
            else
            {
                const string keep = "Keep it as a look";
                var tip = Linked ? $"Saves it to your looks on Glamour Studio as “{name}” (private: only you see it)" : "Link your account first (Settings → Account)";
                if (Kit.Button("##keep", row, keeping ? "Keeping…" : keep, Btn.Primary, BtnSize.Md, Icon.Sparkles, 0, !Linked || keeping, tip))
                {
                    keeping = true;
                    var body = me.Body;
                    sync.SaveLook(name, body, pieces, r =>
                    {
                        keeping = false;
                        if (r.Ok)
                        {
                            keptFrom[plate] = r.Value!;
                            Notify($"Kept as “{r.Value!.Name}” in your looks");
                        }
                        else Notify(r.Why ?? "The site said no");
                    });
                }
                x += Kit.ButtonWidth(keep, BtnSize.Md, Icon.Sparkles, Btn.Primary) + S(8);
            }
        }
        if (pieces.Count > 0 && Kit.Button("##try-plate", new Vector2(x, row.Y), "Try it on", Btn.Glass, BtnSize.Md, Icon.Eye))
            Later(() => TryOn(pieces));
        x += Kit.ButtonWidth("Try it on", BtnSize.Md, Icon.Eye) + S(8);
        if (plate > 0 && Kit.Button("##wear-plate", new Vector2(x, row.Y), "Wear it", Btn.Glass, BtnSize.Md, Icon.Shirt, 0, false,
                "Puts the plate on what you wear now (in a city or an inn, on a job its gear fits)"))
            Later(() =>
            {
                if (!PlateEditor.Wear(plate)) Notify("Couldn't ask the game to wear it");
            });
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Md));
        Kit.Gap(22);
        Kit.Section("Pieces", Words.Count(pieces.Count, "piece"));
        Kit.Gap(10);
        PieceRows("##plate-pieces", pieces);
        if (plate == 0)
        {
            Kit.Gap(10);
            Txt.Note("What you're wearing, glamour and all, as the plugin last saw it. It's on the site too, for the dressing room's Copy my outfit from the game.", Palette.Faint);
        }
        Frame.EndBody();
    }
}

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

    /// <summary>A look kept from the game just now, by the plate it came from, with the pieces it had then.</summary>
    private readonly Dictionary<(string Character, int Plate), (SavedLook Look, IReadOnlyDictionary<Slot, Piece> Pieces)> keptFrom = new();

    /// <summary>The plates being kept now (each its own: one keeping doesn't hold up another).</summary>
    private readonly HashSet<(string Character, int Plate)> keeping = new();

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
        var match = PlateMatch(plate, pieces);
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
            // (Kept, and the plate's the same since: the look's on the site. Changed: it can be kept again.)
            if (keptFrom.TryGetValue((me.Key, plate), out var kept) && Plates.Same(kept.Pieces, pieces))
            {
                if (Kit.Button("##kept", row, "Open it on the site", Btn.Glass, BtnSize.Md, Icon.ExternalLink, 0, false, $"Kept as “{kept.Look.Name}” in your looks"))
                    Dalamud.Utility.Util.OpenLink(SiteLink.DressingUrl(kept.Look.Id));
                x += Kit.ButtonWidth("Open it on the site", BtnSize.Md, Icon.ExternalLink) + S(8);
            }
            else
            {
                const string keep = "Keep it as a look";
                var tip = Linked ? $"Saves it to your looks on Glamour Studio as “{name}” (private: only you see it)" : "Link your account first (Settings → Account)";
                var busy = keeping.Contains((me.Key, plate));
                if (Kit.Button("##keep", row, busy ? "Keeping…" : keep, Btn.Primary, BtnSize.Md, Icon.Sparkles, 0, !Linked || busy, tip))
                {
                    var body = me.Body;
                    var key = me.Key;
                    var snapshot = new Dictionary<Slot, Piece>(pieces);
                    keeping.Add((key, plate));
                    sync.SaveLook(name, body, snapshot, r =>
                    {
                        keeping.Remove((key, plate));
                        if (r.Ok)
                        {
                            keptFrom[(key, plate)] = (r.Value!, snapshot);
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
        if (plate > 0)
        {
            DrawPlatePlan(plate, pieces);
            DrawGearsets(plate);
        }
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

    // ---------------------------------------------------------------------------------------------
    // The plate's plan on the site, and the gearsets it's linked to.

    private void DrawPlatePlan(int plate, IReadOnlyDictionary<Slot, Piece> pieces)
    {
        if (sync.PlanFor(plate) is not { } planned) return;
        Kit.Gap(18);
        var on = Plates.Same(pieces, planned.Pieces);
        var runs = new[]
        {
            new Run(on ? "✓ It's the look planned for it on the site, " : "Planned for it on the site: ", Face.Sans, 13, on ? Palette.Success : Palette.Muted),
            new Run($"“{planned.Name}”", Face.SansMedium, 13, Palette.Rose, Link: "plan"),
            new Run(on ? "." : ". Open it to put it on at a glamour dresser.", Face.Sans, 13, on ? Palette.Success : Palette.Muted),
        };
        if (Txt.Para(runs) == "plan") Later(() => OpenLook(planned, null));
    }

    private List<Gearset> gearsets = new();
    private DateTime gearsetsAt;

    /// <summary>Your gearsets, read again a second after the last read (they change in the game's own window).</summary>
    private List<Gearset> GearsetsNow()
    {
        if (DateTime.UtcNow - gearsetsAt > TimeSpan.FromSeconds(1))
        {
            gearsets = Gearsets.All();
            gearsetsAt = DateTime.UtcNow;
        }
        return gearsets;
    }

    private void DrawGearsets(int plate)
    {
        var all = GearsetsNow();
        if (all.Count == 0) return;
        var linked = all.Where(g => g.Plate == plate).ToList();
        Kit.Gap(22);
        Kit.Section("Gearsets", linked.Count > 0 ? Words.Count(linked.Count, "linked", "linked") : "None linked");
        Kit.Gap(8);
        if (linked.Count > 0)
        {
            Kit.BeginPanel("##gearsets", 6, 6, 6);
            var dl = Kit.Dl;
            for (var i = 0; i < linked.Count; i++)
            {
                var g = linked[i];
                var at = Kit.At;
                var w = Kit.Width;
                var h = S(9) * 2 + S(36);
                if (i > 0) dl.AddLine(new Vector2(at.X + S(10), at.Y - 0.5f), new Vector2(at.X + w - S(10), at.Y - 0.5f), Palette.U32(Palette.Line), 1f);
                var x = at.X + S(10);
                var job = data.Job(g.Job);
                var chipW = Kit.ChipWidth(job.Length > 0 ? job : "—");
                Kit.Chip(new Vector2(x, at.Y + (h - S(24)) / 2), job.Length > 0 ? job : "—");
                x += chipW + S(10);
                const string change = "Change to it";
                var cw = Kit.ButtonWidth(change, BtnSize.Sm, Icon.Shirt);
                var ux = at.X + w - S(10) - S(32);
                var bx = ux - S(8) - cw;
                Txt.Line(dl, new Vector2(x, at.Y), Txt.Fit($"{g.Number}  {g.Name}", Face.SansMedium, 14, bx - S(10) - x), Face.SansMedium, 14, Palette.Ink, 54);
                var by = at.Y + (h - Kit.ButtonHeight(BtnSize.Sm)) / 2;
                var id = g.Id;
                if (Kit.Button($"##gs-equip{i}", new Vector2(bx, by), change, Btn.Glass, BtnSize.Sm, Icon.Shirt, 0, false,
                        "Changes to this gearset: its plate comes with it (in a city or an inn)"))
                    Later(() =>
                    {
                        if (!Gearsets.Equip(id)) Notify("The game wouldn't change gearset now");
                    });
                if (Kit.IconButton($"##gs-unlink{i}", new Vector2(ux, at.Y + (h - S(32)) / 2), Icon.X, "Unlink the plate from this gearset", 32))
                    Later(() =>
                    {
                        Notify(Gearsets.Link(id, 0) ? $"Gearset {id + 1} has no plate now" : "The game wouldn't unlink it");
                        gearsetsAt = DateTime.MinValue;
                    });
                Kit.Reserve(at, w, h);
            }
            Kit.EndPanel();
            Kit.Gap(10);
        }
        else
        {
            Txt.Note("Link it to a gearset and changing to that gearset puts the plate on.", Palette.Muted);
            Kit.Gap(10);
        }
        var row = Kit.At;
        const string link = "Link to a gearset";
        if (Kit.Button("##gs-link", row, link, Btn.Glass, BtnSize.Sm, Icon.Link)) Dalamud.Bindings.ImGui.ImGui.OpenPopup("##gs-link-menu");
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        if (Kit.BeginMenu("##gs-link-menu"))
        {
            var others = all.Where(g => g.Plate != plate).ToList();
            if (others.Count == 0) Txt.Note("Every gearset has this plate.", Palette.Muted);
            foreach (var g in others)
            {
                var job = data.Job(g.Job);
                var label = $"{g.Number}  {(job.Length > 0 ? job + " · " : string.Empty)}{g.Name}" + (g.Plate > 0 ? $"  (plate {g.Plate} now)" : string.Empty);
                if (Kit.MenuItem($"##gs-pick{g.Id}", label, null, S(300)))
                {
                    var id = g.Id;
                    Later(() =>
                    {
                        Notify(Gearsets.Link(id, plate) ? $"Gearset {id + 1} wears plate {plate} now" : "The game wouldn't link it");
                        gearsetsAt = DateTime.MinValue;
                    });
                    Dalamud.Bindings.ImGui.ImGui.CloseCurrentPopup();
                }
            }
            Kit.EndMenu();
        }
    }
}

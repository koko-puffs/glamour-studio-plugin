using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>What each panel beside a game window shows (see Companion.cs for the panel itself).</summary>
public sealed partial class MainWindow
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    // ---------------------------------------------------------------------------------------------
    // The glamour dresser: room left, looks ready for a plate, pieces to bring in.

    private (int Wardrobe, int Sync, (bool, bool) Live, List<(uint Item, Place Where, string Look)> Rows)? bringCache;

    /// <summary>Your looks' pieces that are yours but not where a plate takes them from (each once, with a look that has it).</summary>
    private List<(uint Item, Place Where, string Look)> BringToDresser()
    {
        if (bringCache is { } c && c.Wardrobe == wardrobe.Version && c.Sync == sync.Version && c.Live == (wardrobe.DresserLive, wardrobe.ArmoireLive)) return c.Rows;
        var rows = new List<(uint, Place, string)>();
        var seen = new HashSet<uint>();
        var plans = Enumerable.Range(1, Plates.Count).Select(sync.PlanFor).OfType<Look>();
        foreach (var look in plans.Concat(PaneLooks()))
            foreach (var p in look.Pieces.Values)
            {
                var id = LookJson.BaseId(p.Item);
                if (!seen.Add(id)) continue;
                if (Where(id) is { } w && w is not (Place.Dresser or Place.Armoire)) rows.Add((id, w, look.Name));
            }
        bringCache = (wardrobe.Version, sync.Version, (wardrobe.DresserLive, wardrobe.ArmoireLive), rows);
        return rows;
    }

    private void PaneDresser()
    {
        var me = wardrobe.Current;
        if (me == null)
        {
            PaneNote("Log in with a character to see your dresser here.");
            return;
        }
        var used = DresserUsed(me);
        PaneHeading("Dresser", $"{used} of {DresserCleanup.Capacity} used");
        var at = Kit.At;
        Kit.Progress(at, Kit.Width, (float)used / DresserCleanup.Capacity);
        Kit.Reserve(at, Kit.Width, S(8));
        Kit.Gap(8);
        var free = Math.Max(0, DresserCleanup.Capacity - used);
        if (LooksKnown)
        {
            var room = CleanupNow(me);
            var could = room.Frees + room.Unused.Count;
            PaneNote(could > 0
                ? $"{Words.Count(free, "place")} free. {Words.Count(could, "piece")} could leave without breaking a look."
                : $"{Words.Count(free, "place")} free.");
            if (could > 0)
            {
                var lat = Kit.At;
                if (Kit.Link("##c-room", lat, "Make room", 12)) Later(() => Go(Screen.MakeRoom));
                Kit.Reserve(lat, Kit.Width, Txt.Natural(Face.Sans, 12));
            }
        }
        else PaneNote($"{Words.Count(free, "place")} free.");
        Kit.Gap(8);
        PaneAutoRoom(me);
        Kit.Gap(16);

        if (!Linked)
        {
            PaneHeading("Ready for a plate");
            PaneNote("Link your account (Settings → Account) and your looks show here, the ones the dresser has everything for first.");
            return;
        }
        var ready = ReadyLooks(0, null);
        PaneHeading("Ready for a plate", $"{ReadyCount(ready.Count)} {(ready.Count == 1 ? "look" : "looks")}");
        if (ready.Count == 0) PaneNote("None of your looks has every piece in the dresser or armoire yet.");
        var shown = companionMore ? ready : ready.Take(4).ToList();
        foreach (var look in shown)
        {
            if (SkipUnseen(S(48))) continue;
            var fromArmoire = look.Pieces.Values.Count(p => Where(p.Item) == Place.Armoire);
            var chip = fromArmoire == 0 ? $"All {look.Count} here" : $"{fromArmoire} from the armoire";
            if (LookPaneRow($"##c-plate-{look.Id}", look, null, chip, fromArmoire == 0 ? ChipKind.Ok : ChipKind.Plain, "Plate it", plateIt != null || AutoBusy,
                    "Pick a plate: the plugin opens Glamour Plates, picks that plate and fills it. You press Save in the game's window.", companionPick?.Id == look.Id))
                OpenPlateItMenu("##c-plate-it", look);
        }
        DrawPlateItMenu("##c-plate-it");
        if (!companionMore && ready.Count > 4) MoreLink("##c-more-ready", ready.Count - 4, ready.Count >= ReadyCap);
        Kit.Gap(16);

        var bring = BringToDresser();
        PaneHeading("Bring to the dresser", Words.Count(bring.Count, "piece"));
        if (bring.Count == 0)
        {
            PaneNote("Every piece of your looks you have is in the dresser or armoire.");
            return;
        }
        foreach (var (item, _, look) in companionMore ? bring : bring.Take(6))
        {
            if (SkipUnseen(S(40))) continue;
            var (chip, kind) = WhereChipOf(item);
            PieceRow($"##c-bring-{item}", item, null, null, $"for {look}", chip, kind);
        }
        if (!companionMore && bring.Count > 6) MoreLink("##c-more-bring", bring.Count - 6);
        Kit.Gap(6);
        PaneNote("Put them in the dresser from your bags and they count as ready above.");
    }

    /// <summary>"and N more" under a list cut short: pressed, the panel's lists show in full.</summary>
    /// <param name="atLeast">The list itself was cut short (there may be more than it holds): "N+".</param>
    private void MoreLink(string id, int more, bool atLeast = false)
    {
        var at = Kit.At;
        if (Kit.Link(id, at, $"and {more}{(atLeast ? "+" : string.Empty)} more", 12)) Later(() => companionMore = true);
        Kit.Reserve(at, Kit.Width, Txt.Natural(Face.Sans, 12) + S(4));
    }

    /// <summary>How many looks <see cref="ReadyLooks"/> holds at most (it stops looking there).</summary>
    private const int ReadyCap = 20;

    /// <summary>How many looks are ready, "20+" when the list stopped at its most.</summary>
    private static string ReadyCount(int n) => n >= ReadyCap ? $"{n.ToString(En)}+" : n.ToString(En);

    /// <summary>
    /// For a row of <paramref name="h"/> at the cursor in a long list: when it's scrolled out of
    /// sight, its room is taken and true returned, so it isn't laid out for nothing.
    /// </summary>
    private static bool SkipUnseen(float h)
    {
        var at = Kit.At;
        var w = Kit.Width;
        if (ImGui.IsRectVisible(at, at + new Vector2(w, h))) return false;
        Kit.Reserve(at, w, h);
        return true;
    }

    // ---------------------------------------------------------------------------------------------
    // Glamour Plates at a dresser: the plan for the plate picked, slot by slot, Fill it.

    private void PanePlates()
    {
        var plate = editorPlate;
        if (plate == 0)
        {
            PaneNote("Pick a plate in the game's window.");
            return;
        }
        // A look picked here is for the plate it was picked for; one picked at the dresser is offered
        // on whichever plate you pick, until it fills one.
        if (companionPick != null && companionPickPlate != 0 && companionPickPlate != plate) companionPick = null;
        var planned = sync.PlanFor(plate);
        var look = PickedLook(planned) ?? planned;
        var current = EditorPieces(plate);
        if (look == null)
        {
            PaneNoPlan(plate);
            return;
        }

        // (With the slots a look leaves bare kept, Fill leaves what the plate had there.)
        var on = OnPlate(look.Pieces, current, !config.ClearBare);
        // The look: where it came from, its name (opens it in the plugin), on the plate or not yet.
        {
            var fill = on ? Palette.SuccessWash : Palette.RoseWash;
            var edge = on ? Palette.SuccessEdge : Palette.RoseEdge;
            Kit.BeginPanel("##c-plan", 10, 10, 10, fill, edge, 12);
            var at = Kit.At;
            var w = Kit.Width;
            var m = S(40);
            Mosaic(at, m, look.Pieces, 8, 4);
            var chip = on ? "On it" : "Not on it yet";
            var cw = Kit.ChipW(chip, false, 18);
            Kit.Chip(new Vector2(at.X + w - cw, at.Y + (m - S(18)) / 2), chip, on ? ChipKind.Ok : ChipKind.Rose, false, 18);
            var x = at.X + m + S(10);
            var tw = w - m - S(10) - cw - S(8);
            var lh = Txt.Natural(Face.Sans, 11);
            var nh = Txt.Natural(Face.SansSemibold, 13);
            var ty = at.Y + (m - lh - nh) / 2;
            var from = planned != null && look.Id == planned.Id ? "Planned on the site" : "Picked here";
            Txt.Draw(Kit.Dl, new Vector2(x, ty), from, Face.Sans, 11, Palette.Muted);
            if (Kit.Link("##c-plan-open", new Vector2(x, ty + lh), Txt.Fit(look.Name, Face.SansSemibold, 13, tw), 13, Face.SansSemibold))
            {
                var l = look;
                Later(() => OpenLook(l, null, Screen.Plates));
            }
            Kit.Reserve(at, w, m);
            Kit.EndPanel();
        }
        if (companionPick != null && planned != null && companionPick.Id != planned.Id)
        {
            Kit.Gap(6);
            var at = Kit.At;
            if (Kit.Link("##c-plan-back", at, $"Back to the plan ({planned.Name})", 12)) Later(() => companionPick = null);
            Kit.Reserve(at, Kit.Width, Txt.Natural(Face.Sans, 12));
        }
        Kit.Gap(12);

        var thePlan = PlanCached(look);
        var changes = thePlan.Steps.Count(s => s.Want != null && !(current.TryGetValue(s.Slot, out var has) && SamePiece(has, s.Want.Value)));
        PaneHeading("Slot by slot", on ? "all on it" : $"{changes} to change");
        foreach (var step in thePlan.Steps)
        {
            current.TryGetValue(step.Slot, out var has);
            if (step.Want == null && (step.Kind != StepKind.Clear || has.Item == 0)) continue;
            string chip;
            ChipKind kind;
            if (step.Want is { } want && has.Item != 0 && SamePiece(has, want)) (chip, kind) = ("On it", ChipKind.Ok);
            else
                (chip, kind) = step.Kind switch
                {
                    StepKind.Set when step.Source == PlateSource.Armoire => ("From the armoire", ChipKind.Plain),
                    StepKind.Set => ("Changes", ChipKind.Rose),
                    StepKind.Clear => ("Emptied", ChipKind.Plain),
                    StepKind.Missing when step.Elsewhere != null => (WhereChipOf(step.Want!.Value.Item).Text, ChipKind.Plain),
                    StepKind.Missing => ("Don't have", ChipKind.Rose),
                    _ => ("Kept", ChipKind.Plain),
                };
            var item = step.Want?.Item ?? 0;
            var line = step.Want is { } wp ? DyeLine(wp) : null;
            PieceRow($"##c-slot-{Slots.Key(step.Slot)}", item, Slots.Key(step.Slot), item == 0 ? "(nothing)" : null, line, chip, kind,
                tip: step.DyeLeft ? "The copy used has other dyes: dye it in the plate editor" : null);
        }
        Kit.Gap(10);

        // The dyes: from your bags, left to dye, bottles to get.
        var bag = thePlan.BagDyes;
        if (bag.Count > 0) PaneNote($"Dyes from your bags: {Words.List(bag.Select(kv => $"{data.DyeName(kv.Key)}{(kv.Value > 1 ? $" ×{kv.Value}" : string.Empty)}"))}.");
        var left = thePlan.Steps.Where(s => s.DyeLeft).ToList();
        if (left.Count > 0)
        {
            PaneNote($"{Words.Count(left.Count, "piece")} will need dyeing in the plate editor.");
            var bottles = left.SelectMany(s => new[] { s.Dye1 == DyeOutcome.Differs ? s.Want!.Value.Dye1 : (byte)0, s.Dye2 == DyeOutcome.Differs ? s.Want!.Value.Dye2 : (byte)0 })
                .Where(d => d != 0 && !wardrobe.BagDyes.ContainsKey(d)).Distinct()
                .Select(d => data.Dye(d)?.Items.FirstOrDefault() ?? 0).Where(i => i != 0 && !sync.Library.Wishes.Contains(i))
                .Select(i => new Piece(i)).ToList();
            WishLink("##c-plate-dyes", bottles, bottles.Count == 1 ? $"Add {data.ItemName(bottles[0].Item)} to the shopping list" : $"Add the {bottles.Count} dyes to the shopping list");
        }
        Kit.Gap(10);

        var filled = lastFill is { } lf && lf.LookId == look.Id && lf.Result.Plate == plate ? lf : ((string, FillResult, PlatePlan)?)null;
        if (filled is { } f && editorChanges)
        {
            Kit.BeginPanel("##c-save", 10, 12, 10, Palette.RoseWash, Palette.RoseEdge, 12);
            Txt.Para(new[] { new Run("Filled. ", Face.SansSemibold, 12, Palette.Ink), new Run("Press Save in the game to keep it.", Face.Sans, 12, Palette.Soft) }, 18);
            Kit.EndPanel();
            Kit.Gap(10);
        }
        else if (filled is { } && on)
        {
            // (Wearing it waits: the game won't change gearset with Glamour Plates open at a dresser.)
            Kit.BeginPanel("##c-saved", 10, 12, 10, Palette.SuccessWash, Palette.SuccessEdge, 12);
            Txt.Para(new[] { new Run("Saved. ", Face.SansSemibold, 12, Palette.Ink), new Run($"To wear it, close Glamour Plates and the dresser, then Wear plate {plate} in the plugin's Plates (or the Character window's Glamour Plate).", Face.Sans, 12, Palette.Soft) }, 18);
            Kit.EndPanel();
            Kit.Gap(10);
        }

        var row = Kit.At;
        var can = !on && (thePlan.Ready > 0 || thePlan.Steps.Any(s => s.Kind == StepKind.Clear));
        var label = $"Fill plate {plate}";
        if (Kit.Button("##c-fill", row, label, Btn.Primary, BtnSize.Sm, Icon.Layers, 0, !can,
                on ? "It's on the plate already" : can ? "Fills the plate in the game's window. Nothing's saved until you press Save there." : "None of its pieces is in your dresser or armoire"))
        {
            var l = look;
            var lp = thePlan;
            Later(() =>
            {
                if (companionPick != null) companionPickPlate = plate;
                Fill(l, lp);
            });
        }
        var ox = row.X + Kit.ButtonWidth(label, BtnSize.Sm, Icon.Layers, Btn.Primary) + S(6);
        const string other = "Another look";
        if (Kit.Button("##c-other", new Vector2(ox, row.Y), other, Btn.Quiet, BtnSize.Sm, Icon.ChevronDown))
            ImGui.OpenPopup("##c-pick-look");
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        LookPicker(plate, planned);
    }

    /// <summary>The menu of looks to pick for the plate: the ready ones, then the rest of yours.</summary>
    private void LookPicker(int plate, Look? planned)
    {
        if (!Kit.BeginMenu("##c-pick-look")) return;
        var ready = ReadyLooks(plate, planned);
        var rest = sync.Library.Mine.Where(l => ready.All(r => r.Id != l.Id)).Take(12);
        foreach (var l in ready.Concat(rest))
        {
            var r = ReadinessOf(l);
            if (Kit.MenuItem($"##c-pick-{l.Id}", $"{l.Name}{(r.PlateReady ? string.Empty : $" · {r.Short}")}", r.PlateReady ? Icon.Check : null, S(280)))
            {
                var pick = l;
                ImGui.CloseCurrentPopup();
                Later(() =>
                {
                    companionPick = pick;
                    companionPickPlate = plate;
                });
            }
        }
        if (ready.Count == 0 && !sync.Library.Mine.Any()) PaneNote(Linked ? "No looks yet." : "Link your account to pick from your looks.");
        Kit.EndMenu();
    }

    private void PaneNoPlan(int plate)
    {
        if (!Linked)
        {
            PaneNote($"No plan for plate {plate}. Link your account (Settings → Account) to fill plates with your looks.");
            return;
        }
        PaneNote($"No plan for plate {plate}. Fill it with one of the looks the dresser has everything for:");
        Kit.Gap(8);
        var ready = ReadyLooks(plate, null);
        if (ready.Count == 0) PaneNote("None is ready yet: their pieces aren't all in the dresser or armoire.");
        foreach (var look in ready.Take(5))
            if (LookPaneRow($"##c-fillwith-{look.Id}", look, Words.Count(look.Count, "piece"), null, ChipKind.Plain, "Fill with it", false,
                    "Fills the plate in the game's window. Nothing's saved until you press Save there."))
            {
                var l = look;
                Later(() =>
                {
                    companionPick = l;
                    companionPickPlate = plate;
                    Fill(l, PlanCached(l));
                });
            }
        Kit.Gap(8);
        var row = Kit.At;
        if (Kit.Button("##c-other", row, "Another look", Btn.Quiet, BtnSize.Sm, Icon.ChevronDown)) ImGui.OpenPopup("##c-pick-look");
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        LookPicker(plate, null);
        Kit.Gap(8);
        PaneNote("Or plan a look for this plate on the site: it shows up here.");
    }

    private static bool SamePiece(Piece a, Piece b) => LookJson.BaseId(a.Item) == LookJson.BaseId(b.Item) && a.Dye1 == b.Dye1 && a.Dye2 == b.Dye2;

    // ---------------------------------------------------------------------------------------------
    // Glamour Plates from the Character window: every plate by its look, Wear.

    private void PanePlatesApply()
    {
        var me = wardrobe.Current;
        if (me == null || me.PlatesReadAt == null)
        {
            PaneNote("Open Glamour Plates at a glamour dresser once, and your plates show here by their looks.");
            return;
        }
        var picked = PlateEditor.PickedPlate;
        var sets = GearsetsNow();
        // (This window applies plates itself: Wear presses its own apply, which /gearset change can't
        // while it's open.)
        var wearBlocked = Services.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ? "Not in combat" : null;
        var empty = 0;
        PaneNote("Wear puts a plate on you, as the window's own apply does. To fill a plate, open Glamour Plates at a glamour dresser.");
        Kit.Gap(8);
        for (var p = 1; p <= Plates.Count; p++)
        {
            var pieces = PlatePieces(me, p);
            if (pieces.Count == 0)
            {
                empty++;
                continue;
            }
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(40);
            var dl = Kit.Dl;
            if (p == picked) dl.AddRectFilled(at, at + new Vector2(w, h), Palette.U32(Palette.RoseRow), S(8));
            var num = p.ToString(En);
            Txt.Draw(dl, new Vector2(at.X + S(6), at.Y + (h - Txt.Natural(Face.Display, 13)) / 2), num, Face.Display, 13, Palette.Ink);
            var m = S(28);
            Mosaic(new Vector2(at.X + S(30), at.Y + (h - m) / 2), m, pieces, 6, 3);
            var x = at.X + S(30) + m + S(8);
            var worn = OnPlate(pieces, me.Outfit) || Plates.Same(pieces, me.Outfit);
            var bw = Kit.ButtonWidth("Wear", BtnSize.Tiny);
            var rx = at.X + w;
            if (!worn)
            {
                var plate = p;
                if (Kit.Button($"##c-wear-{p}", new Vector2(rx - bw, at.Y + (h - Kit.ButtonHeight(BtnSize.Tiny)) / 2), "Wear", Btn.Glass, BtnSize.Tiny, null, 0, wearBlocked != null,
                        wearBlocked ?? "Puts the plate on you, as the window's apply does"))
                    Later(() =>
                    {
                        var why = PlateEditor.ApplyInWindow(plate);
                        recorder.Note($"WEAR plate {plate} from the panel: {why ?? "asked the game"}");
                        Notify(why ?? $"Wearing plate {plate}");
                    });
                rx -= bw + S(6);
            }
            else
            {
                var cw = Kit.ChipW("On", false, 18);
                Kit.Chip(new Vector2(rx - cw, at.Y + (h - S(18)) / 2), "On", ChipKind.Ok, false, 18);
                rx -= cw + S(6);
            }
            foreach (var g in sets.Where(g => g.Plate == p).Take(2).Reverse())
            {
                var job = data.Job(g.Job);
                if (job.Length == 0) continue;
                var cw = Kit.ChipW(job, false, 18);
                Kit.Chip(new Vector2(rx - cw, at.Y + (h - S(18)) / 2), job, ChipKind.Plain, false, 18);
                rx -= cw + S(4);
            }
            var match = PlateMatch(p, pieces);
            var name = match?.Name ?? Words.Count(pieces.Count, "piece");
            Txt.Draw(dl, new Vector2(x, at.Y + (h - Txt.Natural(Face.Sans, 12)) / 2), Txt.Fit(name, Face.Sans, 12, rx - S(4) - x), Face.Sans, 12, match != null ? Palette.Ink : Palette.Muted);
            Kit.Reserve(at, w, h);
        }
        if (empty > 0)
        {
            Kit.Gap(6);
            Kit.Sub(Kit.At, $"and {Words.Count(empty, "empty plate")}");
            Kit.Reserve(Kit.At, Kit.Width, Txt.Natural(Face.Mono, 11));
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The armoire: your looks' pieces in it, what from your bags it takes, what could move there.

    /// <summary>
    /// Armoire rows waiting to be stored one by one (a row's Store, Store all, the right-click menu),
    /// and when the last went. Say: where to say how it went (a toast when null, else a line in chat).
    /// </summary>
    private readonly Queue<(uint Row, string Name, Action<string>? Say)> storeQueue = new();

    private DateTime lastStored;

    /// <summary>The row the game last said no to, and how many times (a count for another row starts again).</summary>
    private (uint Row, int Times) storeRefused;

    /// <summary>Puts a piece in line to be stored (once), and stores it now if nothing's before it.</summary>
    private void QueueStore(uint row, string name, Action<string>? say = null)
    {
        if (storeQueue.All(q => q.Row != row)) storeQueue.Enqueue((row, name, say));
        StoreNext();
    }

    /// <summary>
    /// Stores the next piece in line, half a second after the last (every frame). It stays at the head
    /// until the game takes it: the game says no while it's settling the last one, so it's asked again,
    /// and given up on (and said so) after three noes.
    /// </summary>
    private void StoreNext()
    {
        if (storeQueue.Count == 0 || DateTime.UtcNow - lastStored < TimeSpan.FromSeconds(0.6)) return;
        if (!Armoire.IsOpen)
        {
            storeQueue.Clear();
            return;
        }
        var (row, name, say) = storeQueue.Peek();
        lastStored = DateTime.UtcNow;
        // In there already (stored by hand meanwhile): nothing to ask.
        if (wardrobe.ArmoireNow.Values.Contains(row))
            storeQueue.Dequeue();
        else
        {
            var ok = Armoire.Store(row);
            recorder.Note($"ARMOIRE STORE row {row} ({name}): the game {(ok ? "took the ask" : "said no")}");
            if (ok)
            {
                storing[row] = DateTime.UtcNow;
                storeQueue.Dequeue();
                storeRefused = default;
                say?.Invoke($"{name} is in the armoire.");
            }
            else
            {
                storeRefused = (row, storeRefused.Row == row ? storeRefused.Times + 1 : 1);
                if (storeRefused.Times >= 3)
                {
                    storeQueue.Dequeue();
                    storeRefused = default;
                    (say ?? Notify)($"The armoire didn't take {name}");
                }
            }
        }
        if (storeQueue.Count == 0) wardrobe.Tick(now: true);
    }

    private bool armoireWhich;

    private (int Wardrobe, int Sync, bool Live, IReadOnlyDictionary<uint, uint> Now, Dictionary<uint, string> LookItems, List<uint> InArmoire,
        List<(uint Item, uint Row)> Bags, List<uint> FromDresser)? armoireCache;

    /// <summary>
    /// What the armoire panel lists: your looks' pieces (with a look that has each), those in the
    /// armoire, what from your bags it takes, what in the dresser it would. Worked out again only when
    /// the wardrobe, your looks or the armoire as read changed (what you wear is in the wardrobe).
    /// </summary>
    private (Dictionary<uint, string> LookItems, List<uint> InArmoire, List<(uint Item, uint Row)> Bags, List<uint> FromDresser) ArmoireLists(CharacterWardrobe me)
    {
        var armoireNow = wardrobe.ArmoireNow;
        if (armoireCache is { } c && c.Wardrobe == wardrobe.Version && c.Sync == sync.Version && c.Live == wardrobe.ArmoireLive && ReferenceEquals(c.Now, armoireNow))
            return (c.LookItems, c.InArmoire, c.Bags, c.FromDresser);
        var lookItems = new Dictionary<uint, string>();
        foreach (var l in PaneLooks())
            foreach (var p in l.Pieces.Values)
                lookItems.TryAdd(LookJson.BaseId(p.Item), l.Name);
        var inArmoire = armoireNow.Keys.Where(lookItems.ContainsKey).ToList();
        var bags = (me.Get(Place.Inventory)?.Items ?? new HashSet<uint>()).Concat(me.Get(Place.Armoury)?.Items ?? new HashSet<uint>())
            .Select(LookJson.BaseId).Distinct()
            .Select(i => (Item: i, Row: data.CabinetOf(i)))
            .Where(x => x.Row is { } r && !armoireNow.ContainsKey(x.Item) && !Armoire.IsWorn(x.Item))
            .Select(x => (x.Item, Row: x.Row!.Value))
            .ToList();
        var fromDresser = me.Dresser.Where(d => !d.InSet).Select(d => LookJson.BaseId(d.Item)).Distinct()
            .Where(i => data.CabinetOf(i) != null && !armoireNow.ContainsKey(i)).ToList();
        armoireCache = (wardrobe.Version, sync.Version, wardrobe.ArmoireLive, armoireNow, lookItems, inArmoire, bags, fromDresser);
        return (lookItems, inArmoire, bags, fromDresser);
    }

    private void PaneArmoire()
    {
        var me = wardrobe.Current;
        if (me == null)
        {
            PaneNote("Log in with a character to see your armoire here.");
            return;
        }
        var (lookItems, inArmoire, bags, fromDresser) = ArmoireLists(me);
        var armoireOpen = Armoire.IsOpen;
        PaneHeading("Your looks' pieces in here", Words.Count(inArmoire.Count, "piece"));
        if (!Linked) PaneNote("Link your account (Settings → Account) to see which of your looks' pieces are here.");
        else if (inArmoire.Count == 0) PaneNote("None of your looks' pieces is in the armoire.");
        foreach (var item in (companionMore ? inArmoire : inArmoire.Take(4)))
            if (!SkipUnseen(S(40)))
                PieceRow($"##c-arm-{item}", item, null, null, lookItems[item], null, ChipKind.Plain);
        if (!companionMore && inArmoire.Count > 4) MoreLink("##c-more-arm", inArmoire.Count - 4);
        if (inArmoire.Count > 0) PaneNote("Plates take these straight from the armoire: no need to move them.");
        Kit.Gap(16);

        // From your bags and armoury chest, what the armoire takes.
        PaneHeading("The armoire takes these", Words.Count(bags.Count, "piece"));
        PaneNote(bags.Count == 0 ? "Nothing in your bags or armoury chest that the armoire takes." : "From your bags and armoury chest.");
        Kit.Gap(4);
        var now = DateTime.UtcNow;
        foreach (var (item, row) in companionMore ? bags : bags.Take(6))
        {
            if (SkipUnseen(S(32))) continue;
            var asked = storing.TryGetValue(row, out var t) && now - t < TimeSpan.FromSeconds(5) || storeQueue.Any(q => q.Row == row);
            var name = data.ItemName(item);
            // (Not while Store all goes: one at a time, half a second apart.)
            if (PieceRow($"##c-store-{item}", item, null, null, null, null, ChipKind.Plain, asked ? "Storing…" : "Store", asked || !armoireOpen || storeQueue.Count > 0))
                Later(() => QueueStore(row, name));
        }
        if (!companionMore && bags.Count > 6) MoreLink("##c-more-bags", bags.Count - 6);
        if (bags.Count > 1)
        {
            Kit.Gap(8);
            var at = Kit.At;
            var label = $"Store all {bags.Count}";
            if (Kit.Button("##c-store-all", at, label, Btn.Primary, BtnSize.Sm, Icon.Archive, 0, storeQueue.Count > 0 || !armoireOpen,
                    "One by one, half a second apart"))
            {
                var all = bags.Select(b => (b.Row, data.ItemName(b.Item))).ToList();
                Later(() =>
                {
                    // (The first goes half a second after the last store, as the rest do.)
                    foreach (var (row, name) in all)
                        if (storeQueue.All(q => q.Row != row))
                            storeQueue.Enqueue((row, name, null));
                    Notify($"Storing {Words.Count(all.Count, "piece")}…");
                });
            }
            Kit.Reserve(at, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        }
        Kit.Gap(16);

        // In the dresser, pieces the armoire would take.
        if (fromDresser.Count > 0)
        {
            Kit.BeginPanel("##c-from-dresser", 10, 12, 10, Palette.Card, Palette.Line, 12);
            Txt.Para(new[] { new Run($"{Words.Count(fromDresser.Count, "piece")} in your dresser could live here", Face.SansSemibold, 12, Palette.Ink) }, 18);
            PaneNote($"Take them out of the dresser, then store them: {Words.Count(fromDresser.Count, "place")} freed.");
            var at = Kit.At;
            if (Kit.Link("##c-which", at, armoireWhich ? "Hide them" : "Which ones?", 12)) Later(() => armoireWhich = !armoireWhich);
            Kit.Reserve(at, Kit.Width, Txt.Natural(Face.Sans, 12));
            if (armoireWhich)
            {
                Kit.Gap(6);
                foreach (var item in fromDresser.Take(20)) PieceRow($"##c-fd-{item}", item, null, null, null, null, ChipKind.Plain);
                if (fromDresser.Count > 20) PaneNote($"and {fromDresser.Count - 20} more");
            }
            Kit.EndPanel();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Storing an outfit set: the room it frees, who uses it, where its pieces are.

    private void PaneOutfitSet()
    {
        var set = paneOutfitSet;
        if (set == null)
        {
            PaneNote("Pick an outfit set in the game's window.");
            return;
        }
        var me = wardrobe.Current;
        var pieces = set.SetItem != 0 ? data.SetPieces(set.SetItem).Select(p => LookJson.BaseId(p.Item)).ToList() : new List<uint>();
        if (pieces.Count == 0) pieces = set.Pieces.Select(p => p.Item).ToList();
        var loose = me?.Dresser.Count(d => !d.InSet && pieces.Contains(LookJson.BaseId(d.Item))) ?? 0;
        // Stored already: a set in the dresser of this very id (a high-quality copy's id is a million more, a set of its own).
        var stored = set.SetItem != 0 && me?.Dresser.Any(d => d.InSet && d.SetItem == set.SetItem) == true;
        var frees = stored ? loose : Math.Max(0, loose - 1);
        var users = PaneLooks().Where(l => l.Pieces.Values.Any(p => pieces.Contains(LookJson.BaseId(p.Item)))).ToList();

        // Two numbers.
        {
            var at = Kit.At;
            var w = Kit.Width;
            var cw = (w - S(8)) / 2;
            var ch = S(64);
            void Stat(Vector2 pos, string big, string small)
            {
                Kit.Card(pos, pos + new Vector2(cw, ch), Palette.Card, Palette.Line, 12);
                Txt.Draw(Kit.Dl, pos + new Vector2(S(12), S(10)), big, Face.Display, 22, Palette.Ink);
                Txt.Draw(Kit.Dl, pos + new Vector2(S(12), S(10) + Txt.Natural(Face.Display, 22) + S(2)), Txt.Fit(small, Face.Sans, 11, cw - S(24)), Face.Sans, 11, Palette.Muted);
            }
            Stat(at, frees.ToString(En), frees == 1 ? "dresser place freed" : "dresser places freed");
            Stat(new Vector2(at.X + cw + S(8), at.Y), Linked ? users.Count.ToString(En) : "–", users.Count == 1 ? "of your looks uses it" : "of your looks use it");
            Kit.Reserve(at, w, ch);
        }
        Kit.Gap(14);

        if (Linked && users.Count > 0)
        {
            PaneHeading("Used by");
            foreach (var l in users.Take(5))
            {
                var which = l.Pieces.Where(kv => pieces.Contains(LookJson.BaseId(kv.Value.Item))).Select(kv => Slots.Word(kv.Key).ToLowerInvariant());
                LookPaneRow($"##c-set-{l.Id}", l, string.Join(", ", which), null, ChipKind.Plain, null);
            }
            PaneNote("Plates still take each piece from the stored set.");
            Kit.Gap(14);
        }

        var inDresser = pieces.Count(i => Where(i) == Place.Dresser);
        PaneHeading("Its pieces", $"{inDresser} of {pieces.Count} in the dresser");
        foreach (var item in pieces)
        {
            var (chip, kind) = WhereChipOf(item);
            PieceRow($"##c-setp-{item}", item, null, null, null, chip, kind);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The fitting room: step through your looks, where each piece is, wear it or keep it.

    private int tryIndex;
    private bool tryReady;
    private string? triedId;

    private (int Sync, string? Tried, Dictionary<Slot, Piece> Shown, Look? Look)? fittingCache;

    /// <summary>
    /// The look the fitting room shows (the one tried on from the plugin, or the one of yours that
    /// matches most pieces: a hat of a look isn't the whole outfit), or null. Worked out again only
    /// when what's shown or your looks changed.
    /// </summary>
    private Look? FittingLook(IReadOnlyDictionary<Slot, Piece>? shownNow = null)
    {
        var shown = shownNow ?? FittingRoom.Shown(data);
        if (shown.Count == 0) return null;
        if (fittingCache is { } c && c.Sync == sync.Version && c.Tried == triedId && c.Shown.Count == shown.Count
            && shown.All(kv => c.Shown.TryGetValue(kv.Key, out var p) && p == kv.Value))
            return c.Look;
        var ids = shown.Values.Select(s => LookJson.BaseId(s.Item)).ToHashSet();
        bool AllShown(Look l) => l.Pieces.Values.All(p => ids.Contains(LookJson.BaseId(p.Item)));
        Look? found = null;
        if (triedId != null && PaneLooks().FirstOrDefault(l => l.Id == triedId) is { } tried && AllShown(tried))
            found = tried;
        else
            foreach (var l in PaneLooks())
                if (l.Count > (found?.Count ?? 0) && AllShown(l))
                    found = l;
        fittingCache = (sync.Version, triedId, new Dictionary<Slot, Piece>(shown), found);
        return found;
    }

    private (int Sync, int Wardrobe, bool Ready, (bool, bool) Live, List<Look> List)? tryListCache;

    /// <summary>The looks the fitting room steps through (the ready ones only, when picked), worked out again when they or the wardrobe change.</summary>
    private List<Look> TryList()
    {
        var live = (wardrobe.DresserLive, wardrobe.ArmoireLive);
        if (tryListCache is { } c && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version && c.Ready == tryReady && c.Live == live) return c.List;
        var list = PaneLooks().Where(l => l.Count > 0 && (!tryReady || ReadinessOf(l).PlateReady)).ToList();
        tryListCache = (sync.Version, wardrobe.Version, tryReady, live, list);
        return list;
    }

    private void TryOnLook(Look look)
    {
        triedId = look.Id;
        TryOn(look.Pieces);
    }

    private bool keepingTried;

    private void PaneFittingRoom()
    {
        var list = TryList();
        var seg = Kit.SegmentedPress("##c-try-seg", Kit.At, new[] { "All looks", "Ready for a plate" }, tryReady ? 1 : 0, 0, Kit.Width);
        if (seg >= 0)
            Later(() =>
            {
                tryReady = seg == 1;
                tryIndex = 0;
            });
        Kit.Reserve(Kit.At, Kit.Width, S(40));
        Kit.Gap(12);

        var shown = FittingRoom.Shown(data);
        var look = FittingLook(shown);
        if (look != null && list.FindIndex(l => l.Id == look.Id) is >= 0 and var idx) tryIndex = idx;
        // The arrows: the look before or after, put on in the fitting room.
        {
            var at = Kit.At;
            var w = Kit.Width;
            var b = S(34);
            var busy = FittingRoom.Busy || list.Count == 0;
            if (Kit.IconButton("##c-try-prev", at, Icon.ChevronLeft, "The look before", 34, Btn.Glass, busy))
            {
                var l = list[((tryIndex - 1) % list.Count + list.Count) % list.Count];
                Later(() => TryOnLook(l));
            }
            if (Kit.IconButton("##c-try-next", new Vector2(at.X + w - b, at.Y), Icon.ChevronRight, "The look after", 34, Btn.Glass, busy))
            {
                var l = list[(tryIndex + 1) % Math.Max(1, list.Count)];
                Later(() => TryOnLook(l));
            }
            var mid = at.X + w / 2;
            var tw = w - b * 2 - S(16);
            var title = look?.Name ?? (shown.Count > 0 ? "Tried on by hand" : "Nothing tried on");
            var sub = FittingRoom.Busy ? "trying it on…" : look != null && list.Any(l => l.Id == look.Id) ? $"{tryIndex + 1} of {list.Count}" : shown.Count > 0 ? "what the fitting room has" : $"{list.Count} looks";
            var t = Txt.Fit(title, Face.SansSemibold, 15, tw);
            var th = Txt.Natural(Face.SansSemibold, 15);
            var sh = Txt.Natural(Face.Mono, 11);
            var ty = at.Y + (b - th - sh) / 2;
            Txt.Draw(Kit.Dl, new Vector2(mid - Txt.Measure(t, Face.SansSemibold, 15).X / 2, ty), t, Face.SansSemibold, 15, Palette.Ink);
            var sw = Kit.SubWidth(sub);
            Kit.Sub(new Vector2(mid - MathF.Min(sw, tw) / 2, ty + th), sub, null, tw);
            Kit.Reserve(at, w, b);
        }
        Kit.Gap(14);
        if (!Linked) PaneNote("Link your account (Settings → Account) to step through your looks here.");

        var pieces = look?.Pieces ?? shown;
        if (pieces.Count == 0) return;
        var mine = pieces.Values.Count(p => Where(p.Item) != null);
        PaneHeading("Where each piece is", mine == pieces.Count ? $"all {pieces.Count} yours" : $"{mine} of {pieces.Count} yours");
        foreach (var slot in Slots.All)
        {
            if (!pieces.TryGetValue(slot, out var p)) continue;
            var (chip, kind) = WhereChipOf(p.Item);
            PieceRow($"##c-tp-{Slots.Key(slot)}", p.Item, Slots.Key(slot), null, DyeLine(p), chip, kind);
        }
        Kit.Gap(8);
        var missing = MissingOf(pieces);
        WishLink("##c-try-wish", missing, missing.Count == 1 ? "Add the one you don't have to the shopping list" : $"Add the {missing.Count} you don't have to the shopping list");
        Kit.Gap(10);

        var row = Kit.At;
        if (look != null)
        {
            var plate = PlateWith(look);
            var planned = PlannedPlateOf(look.Id);
            if (plate > 0)
            {
                var label = $"Wear plate {plate}";
                var blocked = Gearsets.Blocked(true);
                if (Kit.Button("##c-try-wear", row, label, Btn.Primary, BtnSize.Sm, Icon.Shirt, 0, blocked != null, blocked ?? "Puts the plate on your current gearset, as /gearset change does"))
                    Later(() => Notify(PlateEditor.Wear(plate) ?? $"Plate {plate} on"));
                Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
            }
            else
            {
                Txt.Para(new[] { new Run(planned > 0 ? $"Planned for plate {planned}: fill it at a glamour dresser." : "On no plate yet: put it on one at a glamour dresser.", Face.Sans, 12, Palette.Muted) }, 18);
            }
        }
        else
        {
            var me = wardrobe.Current;
            var label = keepingTried ? "Keeping…" : "Keep it as a look";
            if (Kit.Button("##c-try-keep", row, label, Btn.Primary, BtnSize.Sm, Icon.Plus, 0, !Linked || keepingTried || me == null,
                    Linked ? "A private look of yours on the site, to remix in the dressing room" : "Link your account first (Settings → Account)"))
            {
                var snapshot = new Dictionary<Slot, Piece>(shown);
                var body = me?.Body ?? Body.Feminine;
                Later(() =>
                {
                    keepingTried = true;
                    sync.SaveLook("Tried on", body, snapshot, r =>
                    {
                        keepingTried = false;
                        Notify(r.Ok ? $"Kept as “{r.Value!.Name}” in your looks" : r.Why ?? "The site said no");
                    });
                });
            }
            Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Examine: how much of their outfit you have, piece by piece, try it on, keep it.

    private void PaneExamine()
    {
        if (ExaminedNow() is not { } e)
        {
            PaneNote("Reading their outfit…");
            return;
        }
        var have = e.Pieces.Values.Count(p => Where(p.Item) != null);
        var total = e.Pieces.Count;
        {
            var at = Kit.At;
            var d = S(62);
            Kit.Ring(at + new Vector2(d / 2), d / 2, S(6), total == 0 ? 0 : (float)have / total);
            var t = $"{have}/{total}";
            var ts = Txt.Measure(t, Face.Display, 15);
            Txt.Draw(Kit.Dl, at + new Vector2(d / 2) - ts / 2, t, Face.Display, 15, Palette.Ink);
            var x = at.X + d + S(14);
            var nh = Txt.Natural(Face.SansSemibold, 14);
            var lh = Txt.Natural(Face.Sans, 11);
            var ty = at.Y + (d - nh - lh - S(3)) / 2;
            var tw = at.X + Kit.Width - x;
            Txt.Draw(Kit.Dl, new Vector2(x, ty), Txt.Fit($"You have {have} of {Words.Count(total, "piece")}", Face.SansSemibold, 14, tw), Face.SansSemibold, 14, Palette.Ink);
            Txt.Draw(Kit.Dl, new Vector2(x, ty + nh + S(3)), Txt.Fit("Dyes as they wear them.", Face.Sans, 11, tw), Face.Sans, 11, Palette.Muted);
            Kit.Reserve(at, Kit.Width, d);
        }
        Kit.Gap(12);
        foreach (var slot in Slots.All)
        {
            if (!e.Pieces.TryGetValue(slot, out var p)) continue;
            var (chip, kind) = WhereChipOf(p.Item);
            PieceRow($"##c-ex-{Slots.Key(slot)}", p.Item, Slots.Key(slot), null, DyeLine(p), chip, kind);
        }
        Kit.Gap(8);
        var missing = MissingOf(e.Pieces);
        WishLink("##c-ex-wish", missing, missing.Count == 1 ? "Add the one you don't have to the shopping list" : $"Add the {missing.Count} you don't have to the shopping list");
        Kit.Gap(10);
        var row = Kit.At;
        if (Kit.Button("##c-ex-try", row, "Try it on", Btn.Primary, BtnSize.Sm, Icon.Eye))
        {
            var pieces = e.Pieces;
            Later(() =>
            {
                triedId = null;
                TryOn(pieces);
            });
        }
        var bx = row.X + Kit.ButtonWidth("Try it on", BtnSize.Sm, Icon.Eye, Btn.Primary) + S(8);
        var keep = keepingExamined ? "Keeping…" : "Keep it as a look";
        if (Kit.Button("##c-ex-keep", new Vector2(bx, row.Y), keep, Btn.Glass, BtnSize.Sm, null, MathF.Min(Kit.ButtonWidth(keep, BtnSize.Sm), row.X + Kit.Width - bx), !Linked || keepingExamined,
                Linked ? "A private look of yours on the site" : "Link your account first (Settings → Account)"))
        {
            var pieces = new Dictionary<Slot, Piece>(e.Pieces);
            var body = e.Body;
            // Named after them, as Home's Keep it as a look does ("Their name's outfit").
            var lookName = e.Name.Length > 0 ? $"{e.Name}'s outfit" : "An outfit I saw";
            Later(() =>
            {
                keepingExamined = true;
                sync.SaveLook(lookName, body, pieces, r =>
                {
                    keepingExamined = false;
                    Notify(r.Ok ? $"Kept as “{r.Value!.Name}” in your looks" : r.Why ?? "The site said no");
                });
            });
        }
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
    }

    // ---------------------------------------------------------------------------------------------
    // The dye window: the colours your looks want for the piece, and the bottles you have.

    private void PaneDye()
    {
        var item = GameWindows.DyedItem(data);
        if (item == 0)
        {
            PaneNote("Pick a piece to dye: the colours your looks want for it show here.");
            return;
        }
        var users = PaneLooks().Select(l => (Look: l, Piece: l.Pieces.Values.FirstOrDefault(p => LookJson.BaseId(p.Item) == item)))
            .Where(x => x.Piece.Item != 0).ToList();
        PaneHeading("Your looks dye it", Words.Count(users.Count, "look"));
        if (!Linked) PaneNote("Link your account (Settings → Account) to see the colours your looks want.");
        else if (users.Count == 0) PaneNote("None of your looks has this piece.");
        foreach (var (look, piece) in users.Take(5))
        {
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(44);
            var m = S(32);
            Mosaic(new Vector2(at.X + S(4), at.Y + (h - m) / 2), m, look.Pieces, 6, 3);
            var x = at.X + S(4) + m + S(10);
            var nh = Txt.Natural(Face.SansMedium, 13);
            var ty = at.Y + (h - nh - S(16) - S(2)) / 2;
            Txt.Draw(Kit.Dl, new Vector2(x, ty), Txt.Fit(look.Name, Face.SansMedium, 13, w - (x - at.X)), Face.SansMedium, 13, Palette.Ink);
            var dx = x;
            var dy = ty + nh + S(2);
            foreach (var d in new[] { piece.Dye1, piece.Dye2 })
            {
                if (d == 0) continue;
                var c = new Vector2(dx + S(7), dy + S(8));
                var info = data.Dye(d);
                if (info != null) Kit.Dl.AddCircleFilled(c, S(7), Palette.U32(Palette.FromRgb(info.Rgb)), 20);
                Kit.Dl.AddCircle(c, S(7) - 0.5f, Palette.U32(Palette.WithAlpha(Palette.White, 0.3f)), 20, 1f);
                var name = Txt.Fit(data.DyeName(d), Face.Sans, 12, MathF.Max(S(20), at.X + w - dx - S(18)));
                Txt.Draw(Kit.Dl, new Vector2(dx + S(18), dy), name, Face.Sans, 12, Palette.Soft);
                dx += S(18) + Txt.Measure(name, Face.Sans, 12).X + S(12);
                if (dx > at.X + w - S(40)) break;
            }
            if (piece.Dye1 == 0 && piece.Dye2 == 0) Txt.Draw(Kit.Dl, new Vector2(dx, dy), "No dye", Face.Sans, 12, Palette.Muted);
            Kit.Reserve(at, w, h);
        }
        Kit.Gap(6);
        PaneNote("A plate dyes its own copy: dye the piece itself only to wear it without a plate.");
        Kit.Gap(14);

        var wanted = users.SelectMany(u => new[] { u.Piece.Dye1, u.Piece.Dye2 }).Where(d => d != 0).Distinct().ToList();
        if (wanted.Count == 0) return;
        PaneHeading("Bottles", "in your bags");
        var toGet = new List<Piece>();
        foreach (var d in wanted)
        {
            var have = wardrobe.BagDyes.TryGetValue(d, out var n) ? n : 0;
            var bottle = data.Dye(d)?.Items.FirstOrDefault() ?? 0;
            if (have == 0 && bottle != 0 && !sync.Library.Wishes.Contains(bottle)) toGet.Add(new Piece(bottle));
            PieceRow($"##c-dye-{d}", bottle, null, data.DyeName(d), null, have > 0 ? $"×{have}" : "none", have > 0 ? ChipKind.Ok : ChipKind.Rose);
        }
        Kit.Gap(6);
        WishLink("##c-dye-wish", toGet, toGet.Count == 1 ? $"Add {data.ItemName(toGet[0].Item)} to the shopping list" : $"Add the {toGet.Count} dyes to the shopping list");
    }

    // ---------------------------------------------------------------------------------------------
    // A retainer's bags: your looks' pieces on them, glamour the armoire would take, the others.

    private bool retainerWhich;

    private sealed class RetainerLists
    {
        public required int Wardrobe { get; init; }
        public required int Sync { get; init; }
        public required ulong Retainer { get; init; }
        public required (bool, bool) Live { get; init; }

        /// <summary>Its pieces your looks use and that aren't where a plate takes them from (with a look that has each).</summary>
        public required List<(uint Item, string Look)> ForLooks { get; init; }

        /// <summary>Its glamour no look uses that the armoire takes, and how much glamour no look uses.</summary>
        public required List<uint> Armoire { get; init; }

        public required int Glamour { get; init; }

        /// <summary>For each of your other retainers read: how many of its pieces your looks use (worked out as they're shown).</summary>
        public Dictionary<string, int> Others { get; } = new();
    }

    private RetainerLists? retainerCache;

    /// <summary>
    /// What the retainer panel lists for the retainer open (null when none is, or it isn't read yet),
    /// worked out again only when the wardrobe, your looks or the retainer changed.
    /// </summary>
    private RetainerLists? RetainerFor((ulong Id, string Name)? open)
    {
        var me = wardrobe.Current;
        if (me == null || open is not { } r || !me.Retainers.TryGetValue(r.Id.ToString(En), out var read)) return null;
        var live = (wardrobe.DresserLive, wardrobe.ArmoireLive);
        if (retainerCache is { } c && c.Wardrobe == wardrobe.Version && c.Sync == sync.Version && c.Retainer == r.Id && c.Live == live) return c;
        var lookItems = LookItems();
        var forLooks = new List<(uint, string)>();
        foreach (var raw in read.Items)
        {
            var id = LookJson.BaseId(raw);
            if (lookItems.TryGetValue(id, out var look) && Where(id) is not (Place.Dresser or Place.Armoire)) forLooks.Add((id, look));
        }
        var armoire = read.Items.Select(LookJson.BaseId).Distinct()
            .Where(i => !lookItems.ContainsKey(i) && data.CabinetOf(i) != null && !wardrobe.ArmoireNow.ContainsKey(i) && me.Get(Place.Armoire)?.Items.Contains(i) != true)
            .ToList();
        var glamour = read.Items.Select(LookJson.BaseId).Distinct().Count(i => !lookItems.ContainsKey(i));
        return retainerCache = new RetainerLists
        {
            Wardrobe = wardrobe.Version, Sync = sync.Version, Retainer = r.Id, Live = live,
            ForLooks = forLooks, Armoire = armoire, Glamour = glamour,
        };
    }

    /// <summary>The open retainer's pieces your looks use and that aren't where a plate takes them from (with a look that has each).</summary>
    private List<(uint Item, string Look)> RetainerForLooks() => RetainerFor(paneRetainer)?.ForLooks ?? new List<(uint, string)>();

    private (int Sync, string? Character, Dictionary<uint, string> Items)? lookItemsCache;

    /// <summary>Every piece in your looks (and the ones planned for your plates: the character's), with a look that has it.</summary>
    private Dictionary<uint, string> LookItems()
    {
        var character = wardrobe.Current?.Key;
        if (lookItemsCache is { } c && c.Sync == sync.Version && c.Character == character) return c.Items;
        var map = new Dictionary<uint, string>();
        var plans = Enumerable.Range(1, Plates.Count).Select(sync.PlanFor).OfType<Look>();
        foreach (var l in plans.Concat(PaneLooks()))
            foreach (var p in l.Pieces.Values)
                map.TryAdd(LookJson.BaseId(p.Item), l.Name);
        lookItemsCache = (sync.Version, character, map);
        return map;
    }

    private void PaneRetainer()
    {
        var me = wardrobe.Current;
        var open = paneRetainer;
        if (me == null || open is not { } r)
        {
            PaneNote("Reading the retainer's bags…");
            return;
        }
        if (RetainerFor(open) is not { } lists)
        {
            PaneNote("Reading the retainer's bags… (each page is read as it loads)");
            return;
        }
        var forLooks = lists.ForLooks;
        PaneHeading("Your looks use these", Words.Count(forLooks.Count, "piece"));
        if (!Linked) PaneNote("Link your account (Settings → Account) to see which of your looks' pieces are here.");
        else if (forLooks.Count == 0) PaneNote("None of your looks' pieces is on this retainer (or you have them in the dresser already).");
        foreach (var (item, look) in companionMore ? forLooks : forLooks.Take(6))
            if (!SkipUnseen(S(40)))
                PieceRow($"##c-ret-{item}", item, null, null, look, null, ChipKind.Plain);
        if (!companionMore && forLooks.Count > 6) MoreLink("##c-more-ret", forLooks.Count - 6);
        if (forLooks.Count > 0) PaneNote("Take them and put them in the dresser to plate those looks.");
        Kit.Gap(16);

        var armoire = lists.Armoire;
        var glamour = lists.Glamour;
        PaneHeading("Glamour no look uses", Words.Count(glamour, "piece"));
        if (armoire.Count > 0)
        {
            Kit.BeginPanel("##c-ret-arm", 10, 12, 10, Palette.Card, Palette.Line, 12);
            Txt.Para(new[] { new Run($"{Words.Count(armoire.Count, "of them", "of them")} the armoire takes", Face.SansSemibold, 12, Palette.Ink) }, 18);
            PaneNote($"Store them there and {(r.Name.Length > 0 ? r.Name : "your retainer")} has room again.");
            var at = Kit.At;
            if (Kit.Link("##c-ret-which", at, retainerWhich ? "Hide them" : "Which ones?", 12)) Later(() => retainerWhich = !retainerWhich);
            Kit.Reserve(at, Kit.Width, Txt.Natural(Face.Sans, 12));
            if (retainerWhich)
            {
                Kit.Gap(6);
                foreach (var item in armoire.Take(20)) PieceRow($"##c-ra-{item}", item, null, null, null, null, ChipKind.Plain);
                if (armoire.Count > 20) PaneNote($"and {armoire.Count - 20} more");
            }
            Kit.EndPanel();
        }
        else PaneNote("None of it is something the armoire takes.");
        Kit.Gap(16);

        var others = GameWindows.Retainers().Where(x => x.Id != r.Id).ToList();
        if (others.Count == 0) return;
        PaneHeading("Your other retainers");
        var cx = Kit.Left;
        var cy = Kit.At.Y;
        var right = Kit.Left + Kit.Width;
        foreach (var (id, name) in others)
        {
            string text;
            var kind = ChipKind.Plain;
            var key = id.ToString(En);
            if (!me.Retainers.TryGetValue(key, out var their)) text = $"{name} · not read yet";
            else
            {
                if (!lists.Others.TryGetValue(key, out var n))
                {
                    var lookItems = LookItems();
                    lists.Others[key] = n = their.Items.Select(LookJson.BaseId).Distinct().Count(i => lookItems.ContainsKey(i) && Where(i) is not (Place.Dresser or Place.Armoire));
                }
                text = n > 0 ? $"{name} · {n} for your looks" : $"{name} · none";
                if (n > 0) kind = ChipKind.Rose;
            }
            var w = MathF.Min(Kit.ChipW(text, false, 22), Kit.Width);
            if (cx + w > right)
            {
                cx = Kit.Left;
                cy += S(22) + S(6);
            }
            Kit.Chip(new Vector2(cx, cy), Txt.Fit(text, Face.Sans, 11, w - S(16)), kind, false, 22, w);
            cx += w + S(6);
        }
        Kit.Reserve(new Vector2(Kit.Left, Kit.At.Y), Kit.Width, cy + S(22) - Kit.At.Y);
    }

    // ---------------------------------------------------------------------------------------------
    // The market board and shops: the shopping list.

    private void PaneMarket()
    {
        if (!Linked)
        {
            PaneNote("Link your account (Settings → Account) and your shopping list shows here.");
            return;
        }
        var wishes = WishesShown(sync.Library.Wishes);
        var forLook = WishLooks();
        var sold = wishes.Where(i => data.Item(i)?.Untradable != true).ToList();
        var notSold = wishes.Where(i => data.Item(i)?.Untradable == true).ToList();
        PaneHeading("On the market board", $"{sold.Count} of {wishes.Count}");
        if (wishes.Count == 0) PaneNote("Your shopping list is empty. Put pieces on it from a look, Examine or the fitting room.");
        var open = GameWindows.MarketResultItem();
        foreach (var item in sold.Take(companionMore ? 40 : 8))
        {
            var line = forLook.TryGetValue(item, out var look) ? $"for {look}" : null;
            if (GameWindows.CheapestListed(item) is { } price) line = $"{(line != null ? line + " · " : string.Empty)}cheapest now {price.ToString("N0", En)} gil";
            if (Where(item) != null) line = $"Yours now ({WhereChipOf(item).Text})";
            var name = data.ItemName(item);
            if (PieceRow($"##c-mb-{item}", item, null, null, line, null, ChipKind.Plain, "Search", !GameWindows.IsShown("ItemSearch") && open == 0,
                    "Types it into the market board's search", open == item))
                Later(() =>
                {
                    if (!GameWindows.SearchMarket(name))
                    {
                        ImGui.SetClipboardText(name);
                        Notify("Copied its name: paste it into the search");
                    }
                });
        }
        if (!companionMore && sold.Count > 8) MoreLink("##c-more-mb", sold.Count - 8);
        Kit.Gap(6);
        PaneNote("Search types it into the market board. Buy one and it comes off the list by itself.");
        if (notSold.Count == 0) return;
        Kit.Gap(14);
        PaneHeading("Not on the market board", Words.Count(notSold.Count, "piece"));
        foreach (var item in notSold.Take(companionMore ? 40 : 6))
        {
            var id = item;
            if (PieceRow($"##c-ns-{item}", item, null, null, forLook.TryGetValue(item, out var look) ? $"for {look}" : null, null, ChipKind.Plain, "Take off", false,
                    "Takes it off your shopping list"))
                Later(() => Unwish(id));
        }
    }

    private (string Name, int Version, HashSet<uint> Found, DateTime At)? shopCache;

    private void PaneShop()
    {
        if (!Linked)
        {
            PaneNote("Link your account (Settings → Account) and what's on your shopping list is marked in shops.");
            return;
        }
        var wishes = sync.Library.Wishes;
        var name = shopName ?? string.Empty;
        if (shopCache is not { } c || c.Name != name || c.Version != sync.Version || DateTime.UtcNow - c.At > TimeSpan.FromSeconds(1))
            shopCache = c = (name, sync.Version, GameWindows.InShop(name, wishes), DateTime.UtcNow);
        var found = c.Found;
        var forLook = WishLooks();
        PaneHeading("Sold here", $"{found.Count} of {wishes.Count} on your list");
        if (found.Count == 0) PaneNote(wishes.Count == 0 ? "Your shopping list is empty." : "Nothing on your shopping list is sold here, as far as this shop's window tells.");
        foreach (var item in found.OrderBy(data.ItemName))
            PieceRow($"##c-shop-{item}", item, null, null, forLook.TryGetValue(item, out var look) ? $"for {look}" : null, "Sold here", ChipKind.Ok);
        Kit.Gap(6);
        PaneNote("Buy one and it comes off the list by itself.");
    }
}

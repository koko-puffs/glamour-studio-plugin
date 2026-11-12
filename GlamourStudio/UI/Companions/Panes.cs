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

/// <summary>What each panel beside a game window shows (see Companion.cs for the panel itself and its parts).</summary>
public sealed partial class MainWindow
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    /// <summary>
    /// For a row <paramref name="h"/> tall at the cursor in a long list: when it's scrolled out of
    /// sight, its room (and the gap before it) is taken and true returned, so it isn't laid out for nothing.
    /// </summary>
    private bool PaneSkip(float h, bool row = false)
    {
        var y = Kit.At.Y + (paneFirstInSec ? 0 : S(ItemGapCss));
        if (ImGui.IsRectVisible(new Vector2(Kit.Left, y), new Vector2(Kit.Right, y + h))) return false;
        PaneNext(row);
        Kit.Reserve(Kit.At, Kit.Width, h);
        return true;
    }

    /// <summary>"and N more" under a list cut short: pressed, the panel's lists show in full.</summary>
    /// <param name="atLeast">The list itself was cut short (there may be more than it holds): "N+".</param>
    private void MoreLink(string id, int more, bool atLeast = false)
    {
        if (PaneLinkRow(id, $"and {more}{(atLeast ? "+" : string.Empty)} more")) Later(() => companionMore = true);
    }

    // ---------------------------------------------------------------------------------------------
    // The glamour dresser: room left (Make room, pressed twice; the run), looks ready for a plate
    // (Plate it), pieces to bring in.

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

    private (int Sync, int Wardrobe, Dictionary<string, string> Lines)? readyLines;

    /// <summary>A ready look's line: where it's from (planned for a plate, sent, yours, loved), and what of it the armoire holds.</summary>
    private string ReadyLine(Look look)
    {
        if (readyLines is not { } c || c.Sync != sync.Version || c.Wardrobe != wardrobe.Version)
            readyLines = c = (sync.Version, wardrobe.Version, new Dictionary<string, string>());
        if (c.Lines.TryGetValue(look.Id, out var line)) return line;
        var planned = PlannedPlateOf(look.Id);
        var sent = sync.Inbox.FirstOrDefault(s => s.Look.Id == look.Id);
        var from = planned > 0 ? $"planned for plate {planned}"
            : sent != null ? sent.Plate is { } p ? $"sent for plate {p}" : "sent to you"
            : look.Mine ? "your look"
            : string.IsNullOrEmpty(look.Maker) ? "loved" : $"loved · {look.Maker}";
        var fromArmoire = look.Pieces.Values.Count(x => Where(x.Item) == Place.Armoire);
        line = fromArmoire > 0 ? $"{from} · {fromArmoire} from the armoire" : from;
        c.Lines[look.Id] = line;
        return line;
    }

    private void PaneDresser()
    {
        var me = wardrobe.Current;
        if (me == null)
        {
            PaneSec();
            PaneText("Log in with a character to see your dresser here.", 12, Palette.Muted);
            return;
        }
        PaneSec();
        if (AutoBusy) PaneDresserRun();
        else PaneDresserRoom(me);

        if (!Linked)
        {
            NotLinked("Link your account (Settings → Account) and your looks show here, the ones the dresser has everything for first.");
            return;
        }
        var ready = ReadyLooks(0, null);
        PaneSec();
        SecHead("Ready for a plate", ReadyCount(ready.Count));
        if (ready.Count == 0) PaneText("None of your looks has every piece in the dresser or armoire yet.", 12, Palette.Muted);
        var menuOpen = plateItMenu != null && ImGui.IsPopupOpen("##c-plate-it");
        var shown = companionMore ? ready.Count : Math.Min(4, ready.Count);
        for (var i = 0; i < shown; i++)
        {
            var look = ready[i];
            if (PaneSkip(S(54))) continue;
            var on = (menuOpen && plateItMenu!.Id == look.Id) || companionPick?.Id == look.Id;
            if (LookRow($"##c-plate-{look.Id}", look, ReadyLine(look), "Plate it", plateIt != null || AutoBusy,
                    "Pick a plate: the plugin opens Glamour Plates, picks that plate and fills it. You press Save in the game's window.", on))
                OpenPlateItMenu("##c-plate-it", look);
        }
        DrawPanePlateMenu("##c-plate-it");
        if (!companionMore && ready.Count > 4) MoreLink("##c-more-ready", ready.Count - 4, ReadyMore && ready.Count >= ReadyCap);

        var bring = BringToDresser();
        PaneSec();
        SecHead("Bring to the dresser", bring.Count.ToString(En));
        if (bring.Count == 0)
        {
            PaneText("Every piece of your looks you have is in the dresser or armoire.", 12, Palette.Muted);
            return;
        }
        foreach (var (item, _, look) in companionMore ? bring : bring.Take(6))
        {
            if (PaneSkip(S(46), row: true)) continue;
            var (tag, own) = WhereOf(item, icons: true);
            PieceRow($"##c-bring-{item}", item, null, $"for {look}", tag, own: own);
        }
        if (!companionMore && bring.Count > 6) MoreLink("##c-more-bring", bring.Count - 6);
        PaneText("Put them in the dresser from your bags and they count as ready above.");
    }

    /// <summary>
    /// The dresser in a line (places used of 800 in mono, free, Make room to open the screen; gone while
    /// a button below waits for its second press), the bar, how many pieces could leave; then storing
    /// sets and taking out duplicates, each pressed twice: the first press turns it amber and says what the second will do.
    /// </summary>
    private void PaneDresserRoom(CharacterWardrobe me)
    {
        var used = DresserUsed(me);
        var free = Math.Max(0, DresserCleanup.Capacity - used);
        var now = DateTime.UtcNow;
        var armed = now < autoRoomArmedUntil || now < dupesArmedUntil;
        {
            PaneNext();
            var at = Kit.At;
            var w = Kit.Width;
            var h = armed ? S(LineCss(13)) : Kit.ButtonHeight(BtnSize.Tiny);
            var dl = Kit.Dl;
            var count = $"{used.ToString(En)}/{DresserCleanup.Capacity.ToString(En)}";
            var cx = Txt.Line(dl, new Vector2(at.X, at.Y), count, Face.MonoMedium, 13, Palette.Ink, h / S(1));
            var right = at.X + w;
            if (!armed)
            {
                const string room = "Make room";
                var bw = BtnW(room, BtnSize.Tiny, Icon.Package);
                if (BtnAt("##c-room", new Vector2(right - bw, at.Y), room, BtnSize.Tiny, Btn.Glass, Icon.Package, false, "Make room in Glamour Studio: what could leave, and what the plugin can do for you"))
                    Later(() => Go(Screen.MakeRoom));
                right -= bw + S(8);
            }
            var lx = at.X + cx + S(8);
            Txt.Line(dl, new Vector2(lx, at.Y), Txt.Fit($"used · {free.ToString(En)} free", Face.Sans, 12, MathF.Max(S(10), right - lx)), Face.Sans, 12, Palette.Muted, h / S(1));
            Kit.Reserve(at, w, h);
        }
        {
            PaneNext();
            var at = Kit.At;
            Kit.Meter(at, Kit.Width, (float)used / DresserCleanup.Capacity);
            Kit.Reserve(at, Kit.Width, S(6));
        }
        if (LooksKnown)
        {
            var cleanup = CleanupNow(me);
            var could = cleanup.Frees + cleanup.Unused.Count;
            if (could > 0) PaneText($"{Words.Count(could, "piece")} could leave without breaking a look", 11, Palette.Muted);
        }

        var (plan, dupes) = AutoRoomNow(me);
        var have = OutfitStorer.Supplies(data);
        var words = PanelWords(me, plan, dupes, have);
        if (plan.Jobs.Count > 0)
        {
            var blocked = AutoRoomBlocked(plan, have);
            var line = blocked ?? $"{Words.Count(plan.Jobs.Count, "set")} could gather {Words.Count(plan.Pieces, "loose piece")}: frees {Words.Count(plan.Frees, "place")}, for {Words.Count(plan.Prisms, "glamour prism")} (you have {have.Prisms}). Needs {Words.Count(plan.MostAtOnce, "free bag place")} at a time (you have {have.FreeBag}).{HeldBackWords(plan)}";
            PaneArmed("##c-auto-room", config.OutfitStoreWorked ? $"Store {Words.Count(plan.Jobs.Count, "set")} for me" : "Try it on one set",
                config.OutfitStoreWorked ? "Press again to store them" : "Press again to try it", Icon.Package, blocked, words.Room,
                line, words.Armed, ref autoRoomArmedUntil, StartAutoRoom);
        }
        if (dupes.Count > 0)
        {
            var blocked = DupesBlocked(dupes, have);
            var line = blocked ?? $"{Words.Count(dupes.Count, "duplicate")} could come out to your bags: frees {Words.Count(dupes.Count, "place")}.";
            PaneArmed("##c-dupes", $"Take out {Words.Count(dupes.Count, "duplicate")}", "Press again to take them out", Icon.Trash, blocked, words.Dupes,
                line, $"Takes {Words.Count(dupes.Count, "duplicate")} out to your bags: frees {Words.Count(dupes.Count, "place")} (you have {have.FreeBag} free bag places).",
                ref dupesArmedUntil, StartDupes);
        }
        if (autoRoomLast is { } last) PaneText(last.Words, 11, last.Ok ? Palette.Ice : Palette.Amber);
    }

    /// <summary>What a second press of storing sets does, said under it once it's pressed (until it has worked here once, one set: <see cref="TrialJob"/>'s).</summary>
    private string ArmedSetsWords(AutoRoomPlan plan, (int Prisms, int FreeBag) have)
    {
        if (plan.Jobs.Count == 0) return string.Empty;
        if (!config.OutfitStoreWorked)
        {
            var trial = TrialJob(plan);
            return $"Tries it on one set first ({data.ItemName(trial.SetItem)}), so you can check it worked: {Words.Count(trial.Pieces.Count + trial.FromBags, "piece")} into their set, a glamour prism each (you have {have.Prisms}).";
        }
        return $"Stores {Words.Count(plan.Jobs.Count, "set")} for you: frees {Words.Count(plan.Frees, "place")}, spends {Words.Count(plan.Prisms, "glamour prism")} (you have {have.Prisms}).";
    }

    /// <summary>
    /// A panel button pressed twice: the first press arms it for five seconds (amber, saying what the
    /// second does), the second starts. Its line under it: what it could do (amber when it can't), or
    /// armed, what it will.
    /// </summary>
    private void PaneArmed(string id, string label, string armedLabel, Icon icon, string? blocked, string tip, string line, string armedLine,
        ref DateTime armedUntil, Action start)
    {
        var armed = blocked == null && DateTime.UtcNow < armedUntil;
        PaneNext();
        var at = Kit.At;
        var pressed = ArmedButton(id, at, armed ? armedLabel : label, icon, armed, blocked != null, blocked ?? tip);
        Kit.Reserve(at, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        PaneText(armed ? armedLine : line, 11, blocked != null ? Palette.Amber : Palette.Muted);
        if (!pressed) return;
        if (armed)
        {
            armedUntil = DateTime.MinValue;
            Later(start);
        }
        else armedUntil = DateTime.UtcNow + TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// A button that asks to be pressed again: outlined as the rest until it's pressed once, then amber
    /// (its edge, words and wash) with what a second press does.
    /// </summary>
    private static bool ArmedButton(string id, Vector2 pos, string label, Icon icon, bool armed, bool disabled, string? tip) =>
        BtnAt(id, pos, label, BtnSize.Sm, armed ? Btn.Amber : Btn.Glass, icon, disabled, tip, MathF.Min(BtnW(label, BtnSize.Sm, icon), Kit.Width));

    /// <summary>While Make room for me works: what it's at, Stop, the bar (periwinkle), places freed.</summary>
    private void PaneDresserRun()
    {
        var sets = OutfitStorer.Busy;
        {
            PaneNext();
            var at = Kit.At;
            var w = Kit.Width;
            var h = Kit.ButtonHeight(BtnSize.Tiny);
            const string stop = "Stop";
            var bw = BtnW(stop, BtnSize.Tiny, Icon.Square);
            if (BtnAt("##c-auto-stop", new Vector2(at.X + w - bw, at.Y), stop, BtnSize.Tiny, Btn.Glass, Icon.Square, false, "Stops once the step the game is answering is done"))
                Later(() =>
                {
                    OutfitStorer.Stop();
                    DresserTaker.Stop();
                });
            var text = sets ? $"Storing sets for you… {OutfitStorer.SetsDone.ToString(En)} done" : $"Taking out duplicates… {DresserTaker.Done.ToString(En)} out";
            Txt.Line(Kit.Dl, at, Txt.Fit(text, Face.SansSemibold, 12, w - bw - S(8)), Face.SansSemibold, 12, Palette.Ink, h / S(1));
            Kit.Reserve(at, w, h);
        }
        {
            PaneNext();
            var at = Kit.At;
            var done = sets ? OutfitStorer.SetsDone : DresserTaker.Done;
            var left = sets ? OutfitStorer.SetsLeft : DresserTaker.Left;
            Kit.Meter(at, Kit.Width, done + left > 0 ? (float)done / (done + left) : 0, MeterKind.Working);
            Kit.Reserve(at, Kit.Width, S(6));
        }
        PaneText(sets
            ? $"{Words.Count(OutfitStorer.PlacesFreed, "place")} freed. Keep the dresser open."
            : $"{DresserTaker.Left.ToString(En)} to go. Keep the dresser open.", 11, Palette.Muted);
    }

    /// <summary>How many looks <see cref="ReadyLooks"/> holds at most (it stops looking there).</summary>
    private const int ReadyCap = 20;

    /// <summary>How many looks are ready, "20+" when the list stopped at its most.</summary>
    private string ReadyCount(int n) => ReadyMore && n >= ReadyCap ? $"{n.ToString(En)}+" : n.ToString(En);

    // Plate it's menu: the twenty plates, the one planned for the look first, then the empty ones.

    private (int Sync, int Wardrobe, string LookId, bool Extra, (string? Sub, bool Empty)[] Rows)? plateMenuRows;

    private static readonly string[] PlateNos = Enumerable.Range(0, 21).Select(n => n.ToString("00", CultureInfo.InvariantCulture)).ToArray();

    private static readonly string[] PlateLabels = Enumerable.Range(0, 21).Select(n => $"Plate {n.ToString(CultureInfo.InvariantCulture)}").ToArray();

    /// <summary>What each plate has on and what's planned for it, for the menu: worked out again only when your looks, the wardrobe or the look change.</summary>
    private (string? Sub, bool Empty)[] PlateMenuRows(Look look)
    {
        var extra = !config.ClearBare;
        if (plateMenuRows is { } c && c.Sync == sync.Version && c.Wardrobe == wardrobe.Version && c.LookId == look.Id && c.Extra == extra) return c.Rows;
        var me = wardrobe.Current;
        var read = me?.PlatesReadAt != null;
        var rows = new (string?, bool)[Plates.Count];
        for (var n = 1; n <= Plates.Count; n++)
        {
            var pieces = me?.Plates.FirstOrDefault(p => p.Plate == n)?.Pieces;
            string? what = null;
            var empty = false;
            if (read)
            {
                if (pieces == null || pieces.Count == 0)
                {
                    what = "empty";
                    empty = true;
                }
                else if (OnPlate(look.Pieces, pieces, extra)) what = "this look";
                else
                {
                    var on = PaneLooks().Concat(sync.Inbox.Select(s => s.Look)).FirstOrDefault(l => OnPlate(l.Pieces, pieces));
                    what = on?.Name ?? Words.Count(pieces.Count, "piece");
                }
            }
            var planned = sync.PlanFor(n);
            var plan = planned != null ? $"planned: {planned.Name}" : null;
            rows[n - 1] = (what != null && plan != null ? $"{what} · {plan}" : what ?? plan, empty);
        }
        plateMenuRows = (sync.Version, wardrobe.Version, look.Id, extra, rows);
        return rows;
    }

    /// <summary>
    /// Plate it's menu (the mock's): its look's name over the plates, each its number in mono, "Plate N"
    /// and what's on it and planned under; the one planned for the look first (lit), the empty ones,
    /// then the rest. Seven show; it scrolls, a chevron at its foot while there's more.
    /// </summary>
    private void DrawPanePlateMenu(string id)
    {
        if (plateItMenu is not { } look) return;
        ImGui.SetNextWindowSizeConstraints(new Vector2(S(250), 0), new Vector2(S(250), S(320)));
        if (!Kit.BeginMenu(id)) return;
        try
        {
            var rows = PlateMenuRows(look);
            Kit.MenuHeading($"Put “{Txt.Fit(look.Name, Face.SansSemibold, 10, S(150))}” on");
            var firstGroup = 0;
            for (var pass = 0; pass < 3; pass++)
            {
                if (pass == 2 && firstGroup > 0)
                {
                    var sp = ImGui.GetCursorScreenPos();
                    Kit.Dl.AddLine(sp + new Vector2(S(6), S(4) + 0.5f), sp + new Vector2(S(238 - 6), S(4) + 0.5f), Palette.U32(Palette.Line), 1f);
                    ImGui.Dummy(new Vector2(S(238), S(9)));
                }
                for (var n = 1; n <= Plates.Count; n++)
                {
                    var planned = sync.PlanFor(n)?.Id == look.Id;
                    var group = planned ? 0 : rows[n - 1].Empty ? 1 : 2;
                    if (group != pass) continue;
                    if (pass < 2) firstGroup++;
                    var plate = n;
                    if (PlateMenuRow($"{id}-{plate}", plate, rows[n - 1].Sub, planned))
                    {
                        ImGui.CloseCurrentPopup();
                        var l = look;
                        Later(() => StartPlateIt(l, plate));
                    }
                }
            }
            // More below: the chevron at the foot (the wheel scrolls).
            if (ImGui.GetScrollMaxY() > 0 && ImGui.GetScrollY() < ImGui.GetScrollMaxY() - 1)
            {
                var wp = ImGui.GetWindowPos();
                var ws = ImGui.GetWindowSize();
                var dl = Kit.Dl;
                dl.PushClipRect(wp, wp + ws, false);
                var y1 = wp.Y + ws.Y - S(6);
                dl.AddRectFilled(new Vector2(wp.X + S(6), y1 - S(26)), new Vector2(wp.X + ws.X - S(12), y1), Palette.U32(Palette.Popup), S(10));
                Icons.Draw(dl, Icon.ChevronDown, new Vector2(wp.X + ws.X / 2 - S(7), y1 - S(20)), S(14), Palette.Faint);
                dl.PopClipRect();
            }
        }
        finally
        {
            Kit.EndMenu();
        }
    }

    /// <summary>A plate in Plate it's menu (the mock's two-line <c>.mi</c>): 34 tall at least, 10px corners, lit when pointed at or planned.</summary>
    private static bool PlateMenuRow(string id, int plate, string? sub, bool on)
    {
        var pos = ImGui.GetCursorScreenPos();
        var w = S(238);
        var nameLine = S(13 * 1.25f);
        var subLine = sub != null ? S(12 * 1.25f) : 0;
        var h = MathF.Max(S(34), nameLine + subLine + S(10));
        var clicked = ImGui.InvisibleButton(id, new Vector2(w, h));
        var live = !Kit.Blocked;
        var hovered = live && ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var dl = ImGui.GetWindowDrawList();
        if (hovered || on) dl.AddRectFilled(pos, pos + new Vector2(w, h), Palette.U32(Palette.AccentWash), S(10));
        var x = pos.X + S(10);
        Txt.Line(dl, new Vector2(x, pos.Y), PlateNos[Math.Clamp(plate, 0, 20)], Face.Mono, 12, Palette.Muted, h / S(1));
        x += S(20 + 9);
        var ty = pos.Y + (h - nameLine - subLine) / 2;
        Txt.Line(dl, new Vector2(x, ty), PlateLabels[Math.Clamp(plate, 0, 20)], Face.SansMedium, 13, hovered || on ? Palette.Ink : Palette.Body, 13 * 1.25f);
        if (sub != null)
            Txt.Line(dl, new Vector2(x, ty + nameLine), Txt.Fit(sub, Face.Sans, 12, pos.X + w - S(10) - x), Face.Sans, 12, Palette.Faint, 12 * 1.25f);
        Kit.FocusRing(pos, pos + new Vector2(w, h), S(10));
        return clicked && live;
    }

    // ---------------------------------------------------------------------------------------------
    // Glamour Plates at a dresser: the plan for the plate picked, slot by slot, the dyes, Fill it;
    // filled, saved, changes not saved, no plan.

    private void PanePlates()
    {
        var plate = editorPlate;
        if (plate == 0)
        {
            PaneSec();
            PaneText("Pick a plate in the game's window.", 12, Palette.Muted);
            return;
        }
        // A look picked here is for the plate it was picked for; one picked at the dresser is offered
        // on whichever plate you pick, until it fills one.
        if (companionPick != null && companionPickPlate != 0 && companionPickPlate != plate) companionPick = null;
        var planned = sync.PlanFor(plate);
        var look = PickedLook(planned) ?? planned;
        var current = EditorPieces(plate);
        var filled = look != null && lastFill is { } lf && lf.LookId == look.Id && lf.Result.Plate == plate;
        // Changes made by hand and not saved: Fill waits (it would mix its pieces in with them).
        if (editorChanges && !filled)
        {
            WarnCard($"Plate {plate} has changes not saved: save them, or put it back, first.");
            return;
        }
        if (look == null)
        {
            PaneNoPlan(plate);
            return;
        }

        // (With the slots a look leaves bare kept, Fill leaves what the plate had there.)
        var on = OnPlate(look.Pieces, current, !config.ClearBare);
        var thePlan = PlanCached(look);
        if (filled && editorChanges)
        {
            FilledCard(look, plate, planned, thePlan);
            return;
        }
        if (filled && on)
        {
            // (Wearing it waits: the game won't change gearset with Glamour Plates open at a dresser.)
            SavedCard(look, plate, planned);
            return;
        }

        PlanCard(look, planned, on);
        LookPicker(plate, planned);

        var changes = thePlan.Steps.Count(s => s.Want != null && !(current.TryGetValue(s.Slot, out var has) && SamePiece(has, s.Want.Value)));
        PaneSec();
        SecHead("Slot by slot", on ? "all on it" : $"{changes.ToString(En)} to change");
        SlotRows(thePlan, current);

        // The dyes: from your bags, left to dye, bottles to get.
        var bag = thePlan.BagDyes;
        var left = thePlan.Steps.Where(s => s.DyeLeft).ToList();
        if (bag.Count > 0 || left.Count > 0)
        {
            PaneSec();
            if (bag.Count > 0)
                IconLine(Icon.Droplet, Palette.Ice, $"Dyes from your bags: {Words.List(bag.Select(kv => $"{data.DyeName(kv.Key)}{(kv.Value > 1 ? $" ×{kv.Value}" : " ×1")}"))}");
            foreach (var s in left)
            {
                var want = s.Want!.Value;
                var names = new List<string>(2);
                if (s.Dye1 == DyeOutcome.Differs && want.Dye1 != 0) names.Add(data.DyeName(want.Dye1));
                if (s.Dye2 == DyeOutcome.Differs && want.Dye2 != 0) names.Add(data.DyeName(want.Dye2));
                IconLine(Icon.Droplet, Palette.Amber, names.Count > 0 ? $"{Slots.Word(s.Slot)}: dye {Words.List(names)} in the editor" : $"{Slots.Word(s.Slot)}: dye it in the editor");
            }
            if (left.Count > 0)
            {
                var bottles = left.SelectMany(s => new[] { s.Dye1 == DyeOutcome.Differs ? s.Want!.Value.Dye1 : (byte)0, s.Dye2 == DyeOutcome.Differs ? s.Want!.Value.Dye2 : (byte)0 })
                    .Where(d => d != 0 && !wardrobe.BagDyes.ContainsKey(d)).Distinct()
                    .Select(d => data.Dye(d)?.Items.FirstOrDefault() ?? 0).Where(i => i != 0 && !sync.Library.Wishes.Contains(i))
                    .Select(i => new Piece(i)).ToList();
                WishButton("##c-plate-dyes", bottles, bottles.Count == 1 ? $"Add {data.ItemName(bottles[0].Item)} to your wishlist" : $"Add the {bottles.Count} dyes to your wishlist");
            }
        }

        // Fill: the panel's sticker, the one thing to do beside Glamour Plates.
        var can = !on && (thePlan.Ready > 0 || thePlan.Steps.Any(s => s.Kind == StepKind.Clear));
        PaneSec();
        PaneNext();
        var at = Kit.At;
        if (Kit.Sticker("##c-fill", at, $"Fill plate {plate.ToString(En)}", StickerSize.Lg, Icon.Layers, Kit.Width, !can,
                on ? "It's on the plate already" : can ? "Fills the plate in the game's window. Nothing's saved until you press Save there." : "None of its pieces is in your dresser or armoire"))
            FillLater(look, thePlan, plate);
        Kit.Reserve(at, Kit.Width, Kit.StickerHeight(StickerSize.Lg));
        PaneText("Fills the plate in the game's window. Nothing's saved until you press Save there.", 11, Palette.Faint, center: true);
    }

    private void FillLater(Look look, PlatePlan plan, int plate) =>
        Later(() =>
        {
            if (companionPick != null) companionPickPlate = plate;
            Fill(look, plan);
        });

    /// <summary>
    /// The plan for the plate (the mock's <c>.card.plan</c>): where it's from, its name (opens it in
    /// the plugin) and whether it's on the plate, its pieces, Another look (and back to the plan).
    /// </summary>
    private void PlanCard(Look look, Look? planned, bool on)
    {
        PaneSec();
        PaneNext();
        Kit.BeginPanel("##c-plan", 12, 12, 12, Palette.Card, Palette.Line, 18);
        var picked = planned == null || look.Id != planned.Id;
        var at = Kit.At;
        var w = Kit.Width;
        Kit.Eyebrow(at, picked ? "Picked here" : "Planned on the site", Palette.Accent, w);
        Kit.Reserve(at, w, S(11 * 1.3f));
        Kit.Gap(8);

        at = Kit.At;
        var rowH = MathF.Max(S(15 * 1.1f), Kit.TagHeight());
        var tagW = TagAtRight(at.X + w, at.Y, rowH, on ? "On it" : "Not on it yet", on ? TagKind.Ok : TagKind.Plain, null, TagSize.Md, w * 0.45f);
        var nameW = MathF.Max(S(20), w - tagW - S(8));
        var lineH = S(15 * 1.1f);
        var namePos = new Vector2(at.X, at.Y + (rowH - lineH) / 2);
        var hitW = MathF.Min(Kit.DisplayWidth(look.Name, 15), nameW);
        var (open, hovered, _) = Kit.Hit("##c-plan-open", namePos, new Vector2(hitW, lineH));
        if (hovered) Kit.Tooltip("Open it in the plugin");
        Kit.Display(new Vector2(namePos.X, namePos.Y + (lineH - Kit.DisplayHeight(15)) / 2), look.Name, 15, hovered ? Palette.Accent : Palette.Ink, nameW);
        if (open)
        {
            var l = look;
            Later(() => OpenLook(l, null, Screen.Plates));
        }
        Kit.Reserve(at, w, rowH);
        Kit.Gap(8);

        at = Kit.At;
        LookIcons(at, look.Pieces, Math.Max(1, (int)((w + S(4)) / S(32))), 28);
        Kit.Reserve(at, w, S(28));
        Kit.Gap(8);

        at = Kit.At;
        var lh = Txt.Natural(Face.SansSemibold, 12);
        if (PaneLink("##c-other", at, "Another look", out var linkW, Icon.ChevronDown)) ImGui.OpenPopup("##c-pick-look");
        if (picked && planned != null)
        {
            const string back = "Back to the plan";
            var bw = Txt.Measure(back, Face.SansSemibold, 12).X;
            if (PaneLink("##c-plan-back", new Vector2(at.X + w - bw, at.Y), back, out _, null, false, $"The plan for this plate: {planned.Name}"))
                Later(() => companionPick = null);
        }
        else
        {
            const string hint = "opens it in the plugin";
            var hw = Txt.Measure(hint, Face.Sans, 11).X;
            if (hw < w - linkW - S(8)) Txt.Draw(Kit.Dl, new Vector2(at.X + w - hw, at.Y + (lh - Txt.Natural(Face.Sans, 11)) / 2), hint, Face.Sans, 11, Palette.Faint);
        }
        Kit.Reserve(at, w, lh);
        Kit.EndPanel();
    }

    /// <summary>Slot by slot (the mock's <c>.slotrow</c>s): the slot, the piece (24), its name, a small tag in the honest colours.</summary>
    private void SlotRows(PlatePlan plan, Dictionary<Slot, Piece> current)
    {
        PaneNext();
        var first = true;
        foreach (var step in plan.Steps)
        {
            current.TryGetValue(step.Slot, out var has);
            if (step.Want == null && (step.Kind != StepKind.Clear || has.Item == 0)) continue;
            TagSpec tag;
            if (step.Want is { } want && has.Item != 0 && SamePiece(has, want)) tag = new TagSpec("On it", TagKind.Ok);
            else
                tag = step.Kind switch
                {
                    StepKind.Set when step.Source == PlateSource.Armoire => new TagSpec("Armoire", TagKind.Ok),
                    StepKind.Set => new TagSpec("Changes", TagKind.Ink),
                    StepKind.Clear => new TagSpec("Emptied", TagKind.Plain),
                    StepKind.Missing when step.Elsewhere is { } place => new TagSpec(place switch
                    {
                        Place.Inventory => "In your bags…",
                        Place.Armoury => "In the armoury chest…",
                        Place.Saddlebag => "In your saddlebags…",
                        Place.Retainer => "On a retainer…",
                        _ => "Elsewhere…",
                    }, TagKind.Amber),
                    StepKind.Missing => new TagSpec("Don't have", TagKind.Miss),
                    _ => new TagSpec("Kept", TagKind.Plain),
                };
            var item = step.Want?.Item ?? 0;
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(34);
            var dl = Kit.Dl;
            if (!first) Kit.Divider(at.X, at.X + w, at.Y);
            first = false;
            var slotLine = S(11 * 1.1f);
            Txt.Line(dl, new Vector2(at.X, at.Y + (h - slotLine) / 2), Txt.Fit(SlotShort(Slots.Key(step.Slot)), Face.SansSemibold, 11, S(52)), Face.SansSemibold, 11, Palette.Faint, 11 * 1.1f);
            var x = at.X + S(52 + 8);
            var right = at.X + w - TagAtRight(at.X + w, at.Y, h, tag.Text, tag.Kind, null, TagSize.Sm, w * 0.42f) - S(8);
            if (item != 0)
            {
                Kit.Item(new Vector2(x, at.Y + (h - S(24)) / 2), 24, data.Item(item)?.Icon ?? 0, Own.None, step.Kind == StepKind.Missing && step.Elsewhere == null, Palette.Ground);
                x += S(24 + 8);
            }
            var name = item != 0 ? data.ItemName(item) : "emptied";
            Txt.Line(dl, new Vector2(x, at.Y), Txt.Fit(name, Face.SansMedium, 12, MathF.Max(S(10), right - x)), Face.SansMedium, 12, item != 0 ? Palette.Ink : Palette.Faint, h / S(1));
            if (step.DyeLeft && !Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(at, at + new Vector2(w, h)))
                Kit.Tooltip("The copy used has other dyes: dye it in the plate editor");
            Kit.Reserve(at, w, h);
        }
    }

    /// <summary>Filled, not saved yet: the ice edge, the hand voice says the one thing left; Fill it again stays, quiet.</summary>
    private void FilledCard(Look look, int plate, Look? planned, PlatePlan plan)
    {
        PaneSec();
        PaneNext();
        Kit.BeginPanel("##c-filled", 12, 12, 12, Palette.Card, Palette.WithAlpha(Palette.Ice, 0.45f), 18);
        var at = Kit.At;
        var w = Kit.Width;
        Kit.Eyebrow(at, "Filled · not saved yet", Palette.Ice, w, Icon.Check);
        Kit.Reserve(at, w, S(11 * 1.3f));
        Kit.Gap(8);
        at = Kit.At;
        Kit.Display(new Vector2(at.X, at.Y + (S(15 * 1.1f) - Kit.DisplayHeight(15)) / 2), look.Name, 15, Palette.Ink, w);
        Kit.Reserve(at, w, S(15 * 1.1f));
        Kit.Gap(8);
        at = Kit.At;
        Kit.Reserve(at, w, Kit.Hand(at, "now press Save in the game", 13, w));
        Kit.EndPanel();

        PaneSec();
        PaneNext();
        at = Kit.At;
        const string again = "Fill it again";
        if (BtnAt("##c-fill-again", at, again, BtnSize.Sm, Btn.Quiet, null, false, "Fills the plate again in the game's window"))
            FillLater(look, plan, plate);
        var lh = Txt.Natural(Face.SansSemibold, 12);
        var lw = Txt.Measure("Another look", Face.SansSemibold, 12).X + S(16);
        if (PaneLink("##c-other", new Vector2(at.X + w - lw, at.Y + (Kit.ButtonHeight(BtnSize.Sm) - lh) / 2), "Another look", out _, Icon.ChevronDown))
            ImGui.OpenPopup("##c-pick-look");
        Kit.Reserve(at, w, Kit.ButtonHeight(BtnSize.Sm));
        LookPicker(plate, planned);
    }

    /// <summary>Saved: on the plate, and how to wear it (the game won't change gearset at a dresser).</summary>
    private void SavedCard(Look look, int plate, Look? planned)
    {
        PaneSec();
        PaneNext();
        Kit.BeginPanel("##c-saved", 12, 12, 12, Palette.Card, Palette.Line, 18);
        var at = Kit.At;
        var w = Kit.Width;
        Kit.Eyebrow(at, $"Saved · on plate {plate.ToString(En)}", Palette.Ice, w, Icon.Check);
        Kit.Reserve(at, w, S(11 * 1.3f));
        Kit.Gap(8);
        at = Kit.At;
        Kit.Display(new Vector2(at.X, at.Y + (S(15 * 1.1f) - Kit.DisplayHeight(15)) / 2), look.Name, 15, Palette.Ink, w);
        Kit.Reserve(at, w, S(15 * 1.1f));
        Kit.Gap(8);
        TextHere($"To wear it, close Glamour Plates and the dresser, then Wear plate {plate.ToString(En)} in the plugin's Plates (or the Character window's Glamour Plate).", 12, Palette.Muted);
        Kit.Gap(8);
        at = Kit.At;
        if (PaneLink("##c-other", at, "Another look", out _, Icon.ChevronDown)) ImGui.OpenPopup("##c-pick-look");
        Kit.Reserve(at, w, Txt.Natural(Face.SansSemibold, 12));
        Kit.EndPanel();
        LookPicker(plate, planned);
    }

    /// <summary>The menu of looks to pick for the plate: the ready ones, then the rest of yours.</summary>
    private void LookPicker(int plate, Look? planned)
    {
        if (!Kit.BeginMenu("##c-pick-look")) return;
        try
        {
            var ready = ReadyLooks(plate, planned);
            var current = companionPick?.Id ?? planned?.Id;
            if (ready.Count > 0) Kit.MenuHeading("Ready for a plate");
            foreach (var l in ready)
                if (Kit.MenuItem($"##c-pick-{l.Id}", l.Name, null, S(280), null, null, l.Id == current))
                    Pick(l);
            var rest = sync.Library.Mine.Where(l => ready.All(r => r.Id != l.Id)).Take(12).ToList();
            if (rest.Count > 0)
            {
                if (ready.Count > 0) Kit.MenuSeparator();
                Kit.MenuHeading("Your other looks");
                foreach (var l in rest)
                    if (Kit.MenuItem($"##c-pick-{l.Id}", l.Name, null, S(280), ReadinessOf(l).Short, null, l.Id == current))
                        Pick(l);
            }
            if (ready.Count == 0 && rest.Count == 0) TextHere(Linked ? "No looks yet." : "Link your account to pick from your looks.", 12, Palette.Muted);
        }
        finally
        {
            Kit.EndMenu();
        }

        void Pick(Look pick)
        {
            ImGui.CloseCurrentPopup();
            Later(() =>
            {
                companionPick = pick;
                companionPickPlate = plate;
            });
        }
    }

    private void PaneNoPlan(int plate)
    {
        if (!Linked)
        {
            NotLinked($"No plan for plate {plate.ToString(En)}. Link your account (Settings → Account) to fill plates with your looks.");
            return;
        }
        PaneSec();
        PaneText($"No plan for plate {plate.ToString(En)}. Fill it with one of the looks the dresser has everything for:", 12, Palette.Muted);
        var ready = ReadyLooks(plate, null);
        PaneSec();
        if (ready.Count == 0) PaneText("None is ready yet: their pieces aren't all in the dresser or armoire.", 12, Palette.Muted);
        foreach (var look in ready.Take(5))
            if (LookRow($"##c-fillwith-{look.Id}", look, $"all {look.Count.ToString(En)} here", "Fill with it", false,
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
        if (PaneLinkRow("##c-other", "Another look", Icon.ChevronDown)) ImGui.OpenPopup("##c-pick-look");
        LookPicker(plate, null);
        PaneSec();
        PaneText("Or plan a look for this plate on the site: it shows up here.");
    }

    private static bool SamePiece(Piece a, Piece b) => LookJson.BaseId(a.Item) == LookJson.BaseId(b.Item) && a.Dye1 == b.Dye1 && a.Dye2 == b.Dye2;

    // ---------------------------------------------------------------------------------------------
    // Glamour Plates from the Character window: every plate by its look, its gearsets' jobs, On, Wear.

    private void PanePlatesApply()
    {
        var me = wardrobe.Current;
        if (me == null || me.PlatesReadAt == null)
        {
            PaneSec();
            PaneText("Open Glamour Plates at a glamour dresser once, and your plates show here by their looks.", 12, Palette.Muted);
            return;
        }
        var picked = PlateEditor.PickedPlate;
        var sets = GearsetsNow();
        // (This window applies plates itself: Wear presses its own apply, which /gearset change can't
        // while it's open.)
        var wearBlocked = Services.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ? "Not in combat" : null;
        var empty = 0;
        PaneSec();
        PaneNext();
        var first = true;
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
            var h = S(44);
            var dl = Kit.Dl;
            var sel = p == picked;
            if (sel) dl.AddRectFilled(new Vector2(at.X - S(8), at.Y), new Vector2(at.X + w + S(8), at.Y + h), Palette.U32(Palette.AccentWash), S(10));
            else if (!first) Kit.Divider(at.X - S(8), at.X + w + S(8), at.Y);
            first = false;
            Txt.Line(dl, at, PlateNos[p], Face.Mono, 12, Palette.Muted, h / S(1));
            var x = at.X + S(20 + 9);
            var worn = OnPlate(pieces, me.Outfit) || Plates.Same(pieces, me.Outfit);
            var rx = at.X + w;
            if (!worn)
            {
                var bw = BtnW("Wear", BtnSize.Tiny);
                var plate = p;
                if (BtnAt($"##c-wear-{p}", new Vector2(rx - bw, at.Y + (h - Kit.ButtonHeight(BtnSize.Tiny)) / 2), "Wear", BtnSize.Tiny, Btn.Glass, null, wearBlocked != null,
                        wearBlocked ?? "Puts the plate on you, as the window's apply does"))
                    Later(() =>
                    {
                        var why = PlateEditor.ApplyInWindow(plate);
                        recorder.Note($"WEAR plate {plate} from the panel: {why ?? "asked the game"}");
                        if (why != null) Frame.Warn(why);
                        else Notify($"Wearing plate {plate}");
                    });
                rx -= bw + S(9);
            }
            else rx -= TagAtRight(rx, at.Y, h, "On", TagKind.Ok, null, TagSize.Md, w) + S(9);
            foreach (var g in sets.Where(g => g.Plate == p).Take(2).Reverse())
            {
                var job = data.Job(g.Job);
                if (job.Length == 0) continue;
                rx -= JobTag(rx, at.Y, h, job) + S(6);
            }
            var match = PlateMatch(p, pieces);
            var name = match?.Name ?? Words.Count(pieces.Count, "piece");
            Txt.Line(dl, at with { X = x }, Txt.Fit(name, Face.SansSemibold, 13, MathF.Max(S(10), rx - S(3) - x)), Face.SansSemibold, 13, match != null ? Palette.Ink : Palette.Faint, h / S(1));
            Kit.Reserve(at, w, h);
        }
        if (empty > 0) PaneText($"and {Words.Count(empty, "empty plate")}");
        PaneSec();
        PaneText("Wear puts a plate on you, as the window's own apply does. To fill a plate, open Glamour Plates at a glamour dresser.");
    }

    /// <summary>A gearset's job on a plate's row, its right edge at <paramref name="right"/>: the Kit's job tag. Returns its width.</summary>
    private static float JobTag(float right, float rowTop, float rowH, string job) =>
        Kit.JobTag(new Vector2(right - Kit.JobTagWidth(job, 6), rowTop + (rowH - S(18)) / 2), job, padCss: 6);

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
            storeRefused = default;
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
                    Say($"The armoire didn't take {name}", false, say);
                }
            }
        }
        if (storeQueue.Count == 0) wardrobe.Tick(now: true);
    }

    private bool armoireWhich;

    private (int Wardrobe, int Sync, bool Live, IReadOnlyDictionary<uint, uint> Now, Dictionary<uint, string> LookItems, List<uint> InArmoire,
        List<(uint Item, uint Row, Place From)> Bags, List<uint> FromDresser)? armoireCache;

    /// <summary>
    /// What the armoire panel lists: your looks' pieces (with a look that has each), those in the
    /// armoire, what from your bags and armoury chest it takes (and from which), what in the dresser it
    /// would. Worked out again only when the wardrobe, your looks or the armoire as read changed (what
    /// you wear is in the wardrobe).
    /// </summary>
    private (Dictionary<uint, string> LookItems, List<uint> InArmoire, List<(uint Item, uint Row, Place From)> Bags, List<uint> FromDresser) ArmoireLists(CharacterWardrobe me)
    {
        var armoireNow = wardrobe.ArmoireNow;
        if (armoireCache is { } c && c.Wardrobe == wardrobe.Version && c.Sync == sync.Version && c.Live == wardrobe.ArmoireLive && ReferenceEquals(c.Now, armoireNow))
            return (c.LookItems, c.InArmoire, c.Bags, c.FromDresser);
        var lookItems = new Dictionary<uint, string>();
        foreach (var l in PaneLooks())
            foreach (var p in l.Pieces.Values)
                lookItems.TryAdd(LookJson.BaseId(p.Item), l.Name);
        var inArmoire = armoireNow.Keys.Where(lookItems.ContainsKey).ToList();
        var seen = new HashSet<uint>();
        var bags = new List<(uint, uint, Place)>();
        foreach (var from in new[] { Place.Inventory, Place.Armoury })
            foreach (var raw in me.Get(from)?.Items ?? new HashSet<uint>())
            {
                var i = LookJson.BaseId(raw);
                if (!seen.Add(i) || data.CabinetOf(i) is not { } r || armoireNow.ContainsKey(i) || Armoire.IsWorn(i)) continue;
                bags.Add((i, r, from));
            }
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
            PaneSec();
            PaneText("Log in with a character to see your armoire here.", 12, Palette.Muted);
            return;
        }
        var (lookItems, inArmoire, bags, fromDresser) = ArmoireLists(me);
        var armoireOpen = Armoire.IsOpen;
        if (!Linked) NotLinked("Link your account (Settings → Account) to see which of your looks' pieces are here.");
        else
        {
            PaneSec();
            SecHead("Your looks' pieces in here", inArmoire.Count.ToString(En));
            if (inArmoire.Count == 0) PaneText("None of your looks' pieces is in the armoire.", 12, Palette.Muted);
            else
            {
                // Their tiles (30), wrapped; each named, with a look that has it, when pointed at.
                PaneNext();
                var at = Kit.At;
                var w = Kit.Width;
                var x = at.X;
                var y = at.Y;
                var tile = S(30);
                var gap = S(4);
                var shown = companionMore ? inArmoire.Count : Math.Min(16, inArmoire.Count);
                for (var i = 0; i < shown; i++)
                {
                    if (x + tile > at.X + w + 0.5f)
                    {
                        x = at.X;
                        y += tile + gap;
                    }
                    var item = inArmoire[i];
                    var pos = new Vector2(x, y);
                    Kit.Item(pos, 30, data.Item(item)?.Icon ?? 0);
                    if (!Kit.Blocked && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + new Vector2(tile)))
                        Kit.Tooltip(data.ItemName(item), $"for {lookItems[item]}");
                    x += tile + gap;
                }
                Kit.Reserve(at, w, y + tile - at.Y);
                if (!companionMore && inArmoire.Count > 16) MoreLink("##c-more-arm", inArmoire.Count - 16);
                PaneText("Plates take these straight from the armoire: no need to move them.");
            }
        }

        // From your bags and armoury chest, what the armoire takes.
        PaneSec();
        SecHead("The armoire takes these", bags.Count.ToString(En));
        PaneText(bags.Count == 0 ? "Nothing in your bags or armoury chest that the armoire takes." : "From your bags and armoury chest.", 11, Palette.Muted);
        var now = DateTime.UtcNow;
        foreach (var (item, row, from) in companionMore ? bags : bags.Take(6))
        {
            if (PaneSkip(S(46), row: true)) continue;
            var asked = storing.TryGetValue(row, out var t) && now - t < TimeSpan.FromSeconds(5) || storeQueue.Any(q => q.Row == row);
            var name = data.ItemName(item);
            var where = from == Place.Inventory ? "bags" : "armoury chest";
            var forLook = lookItems.TryGetValue(item, out var look) ? look : null;
            // (Not while Store all goes: one at a time, half a second apart.)
            if (PieceRow($"##c-store-{item}", item, null, forLook != null ? $"{where} · for {forLook}" : where, button: asked ? "Storing…" : "Store",
                    buttonDisabled: asked || !armoireOpen || storeQueue.Count > 0, own: forLook != null ? Own.Amber : Own.None))
                Later(() => QueueStore(row, name));
        }
        if (!companionMore && bags.Count > 6) MoreLink("##c-more-bags", bags.Count - 6);
        if (bags.Count > 1)
        {
            if (PaneButton("##c-store-all", $"Store all {bags.Count.ToString(En)}", null, BtnSize.Sm, Btn.Glass, storeQueue.Count > 0 || !armoireOpen,
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
            PaneText("One by one, half a second apart.");
        }

        // In the dresser, pieces the armoire would take.
        if (fromDresser.Count == 0) return;
        PaneSec();
        SecHead("From your dresser", fromDresser.Count.ToString(En));
        PaneText($"The armoire would take {Words.Count(fromDresser.Count, "piece")} in your dresser: take them out, then store them, a place freed each.", 11, Palette.Muted);
        if (PaneLinkRow("##c-which", armoireWhich ? "Hide them" : "Which ones?")) Later(() => armoireWhich = !armoireWhich);
        if (!armoireWhich) return;
        foreach (var item in fromDresser.Take(20)) PieceRow($"##c-fd-{item}", item, null, lookItems.TryGetValue(item, out var l) ? $"for {l}" : null);
        if (fromDresser.Count > 20) PaneText($"and {fromDresser.Count - 20} more");
    }

    // ---------------------------------------------------------------------------------------------
    // Storing an outfit set: the room it frees, who uses it, where its pieces are.

    private void PaneOutfitSet()
    {
        var set = paneOutfitSet;
        if (set == null)
        {
            PaneSec();
            PaneText("Pick an outfit set in the game's window.", 12, Palette.Muted);
            return;
        }
        var me = wardrobe.Current;
        var pieces = set.SetItem != 0 ? data.SetPieces(set.SetItem).Select(p => LookJson.BaseId(p.Item)).ToList() : new List<uint>();
        if (pieces.Count == 0) pieces = set.Pieces.Select(p => p.Item).ToList();
        var loose = me?.Dresser.Count(d => !d.InSet && pieces.Contains(LookJson.BaseId(d.Item))) ?? 0;
        // Stored already: a set in the dresser of this very id (a high-quality copy's id is a million more, a set of its own).
        var stored = set.SetItem != 0 && me?.Dresser.Any(d => d.InSet && d.SetItem == set.SetItem + (set.Hq ? LookJson.HqOffset : 0)) == true;
        var frees = stored ? loose : Math.Max(0, loose - 1);
        var users = PaneLooks().Where(l => l.Pieces.Values.Any(p => pieces.Contains(LookJson.BaseId(p.Item)))).ToList();

        // The places it frees, big in ice.
        {
            PaneSec();
            PaneNext();
            var at = Kit.At;
            var w = Kit.Width;
            var big = frees.ToString(En);
            var bigLine = S(26 * 1.02f);
            var bw = Kit.DisplayWidth(big, 26);
            var why = loose == 0 ? "none of its pieces is loose in the dresser"
                : stored ? $"its {Words.Count(loose, "loose piece")}: the set's stored already"
                : $"its {Words.Count(loose, "loose piece")}, less the one place the set takes";
            OneRun[0] = new Run($"{(frees == 1 ? "dresser place" : "dresser places")} freed by storing it ({why})", Face.Sans, 12, Palette.Muted);
            var tx = at.X + bw + S(10);
            var th = Txt.Flow(new Vector2(tx, at.Y), at.X + w - tx, OneRun, LineCss(12), out _, draw: false);
            var h = MathF.Max(bigLine, th);
            Kit.Display(new Vector2(at.X, at.Y + (h - bigLine) / 2 + (bigLine - Kit.DisplayHeight(26)) / 2), big, 26, Palette.Ice);
            Txt.Flow(new Vector2(tx, at.Y + (h - th) / 2), at.X + w - tx, OneRun, LineCss(12), out _);
            Kit.Reserve(at, w, h);
        }

        if (!Linked) NotLinked("Link your account (Settings → Account) to see which of your looks use it.");
        else if (users.Count > 0)
        {
            PaneSec();
            SecHead("Used by", users.Count.ToString(En));
            foreach (var l in users.Take(5))
            {
                var which = l.Pieces.Where(kv => pieces.Contains(LookJson.BaseId(kv.Value.Item))).Select(kv => Slots.Word(kv.Key).ToLowerInvariant()).ToList();
                LookRow($"##c-set-{l.Id}", l, which.Count == pieces.Count ? $"all {Words.InWords(which.Count).ToLowerInvariant()} of its pieces" : $"the {Words.List(which)}");
            }
            PaneText("Plates still take each piece from the stored set.");
        }

        PaneSec();
        SecHead("Its pieces", pieces.Count.ToString(En));
        foreach (var item in pieces)
        {
            var (tag, own) = WhereOf(item);
            PieceRow($"##c-setp-{item}", item, null, null, tag, own: own, ghost: Where(item) == null);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The fitting room: step through your looks, where each piece is, wear it or keep it.

    /// <summary>
    /// The look stepped to last in <see cref="TryList"/> (or the one the fitting room shows); -1 before
    /// the first, so the first press of the right arrow puts on the first look, not the second.
    /// </summary>
    private int tryIndex = -1;

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

    private static readonly string[] TryTabs = { "Ready", "All" };

    private void PaneFittingRoom()
    {
        var list = TryList();
        var shown = FittingRoom.Shown(data);
        var look = FittingLook(shown);
        var inList = false;
        if (look != null && list.FindIndex(l => l.Id == look.Id) is >= 0 and var idx)
        {
            tryIndex = idx;
            inList = true;
        }

        // Ready or All, and the arrows: the look before or after, put on in the fitting room.
        {
            PaneSec();
            PaneNext();
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(34);
            var seg = SegSm("##c-try-seg", at, TryTabs, tryReady ? 0 : 1);
            if (seg >= 0)
                Later(() =>
                {
                    tryReady = seg == 0;
                    tryIndex = -1;
                });
            var b = S(28);
            var by = at.Y + (h - b) / 2;
            var busy = FittingRoom.Busy || list.Count == 0;
            var nx = at.X + w - b;
            if (Kit.IconButton("##c-try-next", new Vector2(nx, by), Icon.ChevronRight, "The look after", 28, Btn.Glass, busy, 14))
            {
                var l = list[(Math.Max(-1, tryIndex) + 1) % list.Count];
                Later(() => TryOnLook(l));
            }
            var count = list.Count == 0 ? "0 of 0" : inList ? $"{(tryIndex + 1).ToString(En)} of {list.Count.ToString(En)}" : $"– of {list.Count.ToString(En)}";
            var cw = Txt.Measure(count, Face.Mono, 12).X;
            var cx = nx - S(8) - cw;
            Txt.Line(Kit.Dl, new Vector2(cx, at.Y), count, Face.Mono, 12, Palette.Ink, 34);
            if (Kit.IconButton("##c-try-prev", new Vector2(cx - S(8) - b, by), Icon.ChevronLeft, "The look before", 28, Btn.Glass, busy, 14))
            {
                var l = list[tryIndex <= 0 ? list.Count - 1 : (tryIndex - 1) % list.Count];
                Later(() => TryOnLook(l));
            }
            Kit.Reserve(at, w, h);
        }

        // The look on (or nothing yet): its name, pieces, and the plate it's on.
        if (look != null || shown.Count == 0)
        {
            PaneSec();
            PaneNext();
            Kit.BeginPanel("##c-try-card", 14, 16, 14, Palette.Card, Palette.Line, 18);
            var at = Kit.At;
            var w = Kit.Width;
            var title = look?.Name ?? "Nothing tried on";
            Kit.Display(new Vector2(at.X, at.Y + (S(15 * 1.02f) - Kit.DisplayHeight(15)) / 2), title, 15, Palette.Ink, w);
            Kit.Reserve(at, w, S(15 * 1.02f));
            Kit.Gap(6);
            if (look == null) TextHere(list.Count > 0 ? $"Step through your {Words.Count(list.Count, "look")} with the arrows." : "Your looks show here to step through.", 12, Palette.Muted);
            else
            {
                at = Kit.At;
                LookIcons(at, look.Pieces, Math.Max(1, (int)((w + S(4)) / S(32))), 28);
                Kit.Reserve(at, w, S(28));
                Kit.Gap(6);
                at = Kit.At;
                if (FittingRoom.Busy) Kit.Reserve(at, w, Kit.Hand(at, "trying it on…", 13, w));
                else
                {
                    var plate = PlateWith(look);
                    var planned = PlannedPlateOf(look.Id);
                    if (plate > 0)
                    {
                        var rowH = Kit.TagHeight();
                        Kit.Tag(at, $"On plate {plate.ToString(En)}", TagKind.Ok);
                        var label = $"Wear it · plate {plate.ToString(En)}";
                        var lw = Txt.Measure(label, Face.SansSemibold, 12).X;
                        var blocked = Gearsets.Blocked(true);
                        if (PaneLink("##c-try-wear", new Vector2(at.X + w - lw, at.Y + (rowH - Txt.Natural(Face.SansSemibold, 12)) / 2), label, out _, null, blocked != null,
                                blocked ?? "Puts the plate on your current gearset, as /gearset change does"))
                            Later(() =>
                            {
                                var why = PlateEditor.Wear(plate);
                                if (why != null) Frame.Warn(why);
                                else Notify($"Plate {plate} on");
                            });
                        Kit.Reserve(at, w, rowH);
                    }
                    else TextHere(planned > 0 ? $"Planned for plate {planned}: fill it at a glamour dresser." : "On no plate yet: put it on one at a glamour dresser.", 12, Palette.Muted);
                }
            }
            Kit.EndPanel();
        }
        if (!Linked) NotLinked("Link your account (Settings → Account) to step through your looks here.");

        var pieces = look?.Pieces ?? shown;
        if (pieces.Count > 0)
        {
            PaneSec();
            SecHead("Where each piece is");
            foreach (var slot in Slots.All)
            {
                if (!pieces.TryGetValue(slot, out var p)) continue;
                var (tag, own) = WhereOf(p.Item);
                PieceRow($"##c-tp-{Slots.Key(slot)}", p.Item, null, DyeLine(p), tag, tileCss: 28, own: own, ghost: Where(p.Item) == null);
            }
            var missing = MissingOf(pieces);
            WishButton("##c-try-wish", missing, missing.Count == 1 ? "Add the one you don't have to your wishlist" : $"Add the {missing.Count} you don't have to your wishlist");
        }

        // What's on that isn't the look's: tried on by hand, to keep as a look of yours.
        if (shown.Count == 0) return;
        var byHand = new List<uint>();
        foreach (var slot in Slots.All)
        {
            if (!shown.TryGetValue(slot, out var p)) continue;
            if (look == null || !look.Pieces.TryGetValue(slot, out var lp) || LookJson.BaseId(lp.Item) != LookJson.BaseId(p.Item)) byHand.Add(p.Item);
        }
        if (byHand.Count == 0) return;
        PaneSec();
        SecHead("Tried on by hand", byHand.Count.ToString(En));
        PaneNext();
        {
            var at = Kit.At;
            var w = Kit.Width;
            var me = wardrobe.Current;
            var label = keepingTried ? "Keeping…" : "Keep it as a look";
            var bw = BtnW(label, BtnSize.Tiny);
            var h = MathF.Max(S(28), Kit.ButtonHeight(BtnSize.Tiny));
            var x = at.X;
            foreach (var item in byHand)
            {
                if (x + S(28) > at.X + w - bw - S(8)) break;
                Kit.Item(new Vector2(x, at.Y), 28, data.Item(item)?.Icon ?? 0);
                x += S(28 + 4);
            }
            if (BtnAt("##c-try-keep", new Vector2(at.X + w - bw, at.Y + (h - Kit.ButtonHeight(BtnSize.Tiny)) / 2), label, BtnSize.Tiny, Btn.Glass, null,
                    !Linked || keepingTried || me == null,
                    Linked ? "Everything the fitting room has on, as a private look of yours on the site, to remix in the dressing room" : "Link your account first (Settings → Account)"))
            {
                var snapshot = new Dictionary<Slot, Piece>(shown);
                var body = me?.Body ?? Body.Feminine;
                Later(() =>
                {
                    keepingTried = true;
                    sync.SaveLook("Tried on", body, snapshot, r =>
                    {
                        keepingTried = false;
                        Say(r.Ok ? $"Kept as “{r.Value!.Name}” in your looks" : r.Why ?? "The site said no", r.Ok, null);
                    });
                });
            }
            Kit.Reserve(at, w, h);
        }
    }

    /// <summary>
    /// The small segmented pill (the mock's <c>.seg.sm</c>): 34 tall, the card fill and a hairline,
    /// items 26 tall (12px, 600), the one picked periwinkle with ink words. The kit's is 36 with 13px
    /// words. Returns the item pressed, or -1.
    /// </summary>
    private static int SegSm(string id, Vector2 pos, IReadOnlyList<string> items, int selected)
    {
        var dl = Kit.Dl;
        var pad = S(10);
        var total = S(3 + 1) * 2 + S(2) * (items.Count - 1);
        foreach (var s in items) total += Txt.Measure(s, Face.SansSemibold, 12).X + pad * 2;
        var h = S(34);
        dl.AddRectFilled(pos, pos + new Vector2(total, h), Palette.U32(Palette.Card), h / 2);
        dl.AddRect(pos + new Vector2(0.5f), pos + new Vector2(total, h) - new Vector2(0.5f), Palette.U32(Palette.Line), h / 2, ImDrawFlags.None, 1f);
        var x = pos.X + S(4);
        var result = -1;
        ImGui.PushID(id);
        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var w = Txt.Measure(items[i], Face.SansSemibold, 12).X + pad * 2;
                var p = new Vector2(x, pos.Y + S(4));
                var size = new Vector2(w, S(26));
                var on = i == selected;
                var (clicked, hovered, _) = Kit.Hit(PlateNos[Math.Min(i, 20)], p, size);
                if (clicked) result = i;
                if (on) dl.AddRectFilled(p, p + size, Palette.U32(Palette.Field), size.Y / 2);
                else if (hovered) dl.AddRectFilled(p, p + size, Palette.U32(Palette.AccentWash), size.Y / 2);
                Txt.Line(dl, new Vector2(p.X + pad, p.Y), items[i], Face.SansSemibold, 12, on ? Palette.OnField : hovered ? Palette.Body : Palette.Muted, 26);
                Kit.FocusRing(p, p + size, size.Y / 2);
                x += w + S(2);
            }
        }
        finally
        {
            ImGui.PopID();
        }
        return result;
    }

    // ---------------------------------------------------------------------------------------------
    // Examine: how much of their outfit you have, piece by piece, try it on, keep it.

    private void PaneExamine()
    {
        if (ExaminedNow() is not { } e)
        {
            // Reading, quietly, until two reads agree.
            PaneSec();
            PaneNext();
            var at = Kit.At;
            var w = Kit.Width;
            Kit.BeginPanel("##c-ex-reading", 14, 16, 14, Vector4.Zero, Vector4.Zero, 18);
            var row = Kit.At;
            var iw = Kit.Width;
            const string reading = "reading their outfit…";
            var rw = MathF.Min(Txt.Measure(reading, Face.Hand, 13).X, iw * 0.5f);
            var rh = Kit.Hand(row, reading, 13, rw + 1);
            OneRun[0] = new Run("someone new: two reads half a second apart", Face.Sans, 11, Palette.Faint);
            var nx = row.X + rw + S(8);
            var nh = Txt.Flow(new Vector2(nx, row.Y), row.X + iw - nx, OneRun, LineCss(11), out _, draw: false);
            Txt.Flow(new Vector2(nx, row.Y + MathF.Max(0, (rh - nh) / 2)), row.X + iw - nx, OneRun, LineCss(11), out _);
            Kit.Reserve(row, iw, MathF.Max(rh, nh));
            Kit.EndPanel();
            Kit.Dashed(at, new Vector2(at.X + w, ImGui.GetCursorScreenPos().Y), S(18), Palette.LineStrong, 1f);
            return;
        }
        var have = e.Pieces.Values.Count(p => Where(p.Item) != null);
        var total = e.Pieces.Count;
        {
            PaneSec();
            PaneNext();
            var at = Kit.At;
            var w = Kit.Width;
            var d = S(58);
            var c = at + new Vector2(d / 2);
            Kit.Ring(c, d / 2, S(6), total == 0 ? 0 : (float)have / total);
            var t = $"{have.ToString(En)}/{total.ToString(En)}";
            var tw = Kit.DisplayWidth(t, 15);
            Kit.Display(new Vector2(c.X - tw / 2, c.Y - Kit.DisplayHeight(15) / 2), t, 15, Palette.Ink);
            var x = at.X + d + S(12);
            OneRun[0] = new Run("of their pieces are yours, dyes as they wear them", Face.Sans, 12, Palette.Muted);
            var th = Txt.Flow(new Vector2(x, at.Y), at.X + w - x, OneRun, LineCss(12), out _, draw: false);
            Txt.Flow(new Vector2(x, at.Y + (d - th) / 2), at.X + w - x, OneRun, LineCss(12), out _);
            Kit.Reserve(at, w, d);
        }
        PaneSec();
        foreach (var slot in Slots.All)
        {
            if (!e.Pieces.TryGetValue(slot, out var p)) continue;
            var (tag, own) = WhereOf(p.Item);
            PieceRow($"##c-ex-{Slots.Key(slot)}", p.Item, null, DyeLine(p), tag, tileCss: 28, own: own, ghost: Where(p.Item) == null);
        }
        var missing = MissingOf(e.Pieces);
        WishButton("##c-ex-wish", missing, missing.Count == 1 ? "Add the 1 you don't have to your wishlist" : $"Add the {missing.Count} you don't have to your wishlist");

        PaneSec();
        PaneNext();
        var row2 = Kit.At;
        const string tryOn = "Try it on";
        if (BtnAt("##c-ex-try", row2, tryOn, BtnSize.Sm, Btn.Glass, Icon.Shirt, false, "Their outfit in the fitting room"))
        {
            var pieces = e.Pieces;
            Later(() =>
            {
                triedId = null;
                TryOn(pieces);
            });
        }
        var bx = row2.X + BtnW(tryOn, BtnSize.Sm, Icon.Shirt) + S(6);
        var keep = keepingExamined ? "Keeping…" : "Keep it as a look";
        if (BtnAt("##c-ex-keep", new Vector2(bx, row2.Y), keep, BtnSize.Sm, Btn.Glass, null, !Linked || keepingExamined,
                Linked ? "A private look of yours on the site" : "Link your account first (Settings → Account)", MathF.Min(BtnW(keep, BtnSize.Sm), row2.X + Kit.Width - bx)))
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
                    Say(r.Ok ? $"Kept as “{r.Value!.Name}” in your looks" : r.Why ?? "The site said no", r.Ok, null);
                });
            });
        }
        Kit.Reserve(row2, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
    }

    // ---------------------------------------------------------------------------------------------
    // The dye window: the colours your looks want for the piece, and the bottles you have.

    private void PaneDye()
    {
        // (Read once this frame with the panel's title: it walks the window's values.)
        var item = paneDyed;
        if (item == 0)
        {
            PaneSec();
            PaneText("Pick a piece to dye: the colours your looks want for it show here.", 12, Palette.Muted);
            return;
        }
        var users = PaneLooks().Select(l => (Look: l, Piece: l.Pieces.Values.FirstOrDefault(p => LookJson.BaseId(p.Item) == item)))
            .Where(x => x.Piece.Item != 0).ToList();
        if (!Linked)
        {
            NotLinked("Link your account (Settings → Account) to see the colours your looks want.");
            return;
        }
        PaneSec();
        SecHead("Your looks dye it", users.Count.ToString(En));
        if (users.Count == 0) PaneText("None of your looks has this piece.", 12, Palette.Muted);
        foreach (var (look, piece) in users.Take(5))
        {
            var swatches = new uint?[] { piece.Dye1 != 0 ? data.Dye(piece.Dye1)?.Rgb : null, piece.Dye2 != 0 ? data.Dye(piece.Dye2)?.Rgb : null };
            LookRow($"##c-dye-{look.Id}", look, DyeLine(piece) ?? "No dye", swatches: swatches);
        }

        var wanted = users.SelectMany(u => new[] { u.Piece.Dye1, u.Piece.Dye2 }).Where(d => d != 0).Distinct().ToList();
        if (wanted.Count > 0)
        {
            PaneSec();
            SecHead("Bottles");
            var toGet = new List<Piece>();
            var none = new List<string>();
            foreach (var d in wanted)
            {
                var have = wardrobe.BagDyes.TryGetValue(d, out var n) ? n : 0;
                var bottle = data.Dye(d)?.Items.FirstOrDefault() ?? 0;
                if (have > 0) IconLine(Icon.Droplet, Palette.Ice, $"{data.DyeName(d)} ×{have.ToString(En)} in your bags");
                else
                {
                    none.Add(data.DyeName(d));
                    if (bottle != 0 && !sync.Library.Wishes.Contains(bottle)) toGet.Add(new Piece(bottle));
                }
            }
            if (none.Count > 0) IconLine(Icon.Droplet, Palette.Pink, $"{string.Join(", ", none)}: none");
            WishButton("##c-dye-wish", toGet, toGet.Count == 1 ? $"Add {data.ItemName(toGet[0].Item)} to your wishlist" : $"Add the {toGet.Count} dyes to your wishlist");
        }
        PaneSec();
        PaneText("A plate dyes its own copy: dye the piece itself only to wear it without a plate.");
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
        // Each piece once: a normal and a high-quality copy are two ids in the bags, one piece here.
        var seen = new HashSet<uint>();
        foreach (var raw in read.Items)
        {
            var id = LookJson.BaseId(raw);
            if (seen.Add(id) && lookItems.TryGetValue(id, out var look) && Where(id) is not (Place.Dresser or Place.Armoire)) forLooks.Add((id, look));
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

    private readonly Run[] retainerRuns = new Run[2];

    private void PaneRetainer()
    {
        var me = wardrobe.Current;
        var open = paneRetainer;
        if (me == null || open is not { } r)
        {
            PaneSec();
            PaneText("Reading the retainer's bags…", 12, Palette.Muted);
            return;
        }
        if (RetainerFor(open) is not { } lists)
        {
            PaneSec();
            PaneText("Reading the retainer's bags… (each page is read as it loads)", 12, Palette.Muted);
            return;
        }
        var forLooks = lists.ForLooks;
        if (!Linked) NotLinked("Link your account (Settings → Account) to see which of your looks' pieces are here.");
        else
        {
            PaneSec();
            SecHead("Your looks' pieces here", forLooks.Count.ToString(En));
            if (forLooks.Count == 0) PaneText("None of your looks' pieces is on this retainer (or you have them in the dresser already).", 12, Palette.Muted);
            foreach (var (item, look) in companionMore ? forLooks : forLooks.Take(6))
                if (!PaneSkip(S(46), row: true))
                    PieceRow($"##c-ret-{item}", item, null, $"for {look}", own: Own.Amber);
            if (!companionMore && forLooks.Count > 6) MoreLink("##c-more-ret", forLooks.Count - 6);
            if (forLooks.Count > 0) PaneText("Take them and put them in the dresser to plate those looks.");
        }

        var armoire = lists.Armoire;
        PaneSec();
        SecHead("Glamour no look uses", lists.Glamour.ToString(En));
        if (armoire.Count > 0)
        {
            PaneNext();
            retainerRuns[0] = new Run($"The armoire takes {armoire.Count.ToString(En)} of them ", Face.Sans, 12, Palette.Muted);
            retainerRuns[1] = new Run(retainerWhich ? "(hide them)" : "(which ones?)", Face.Sans, 12, Palette.Accent, "which");
            if (Txt.Para(retainerRuns, LineCss(12)) != null) Later(() => retainerWhich = !retainerWhich);
            if (retainerWhich)
            {
                foreach (var item in armoire.Take(20)) PieceRow($"##c-ra-{item}", item);
                if (armoire.Count > 20) PaneText($"and {armoire.Count - 20} more");
            }
        }
        else PaneText("None of it is something the armoire takes.", 12, Palette.Muted);

        // (The game's list of them, read again every two seconds rather than every frame.)
        var others = RetainersNow().Where(x => x.Id != r.Id).ToList();
        if (others.Count == 0) return;
        PaneSec();
        SecHead("Your other retainers");
        PaneNext();
        var first = true;
        foreach (var (id, name) in others)
        {
            var at = Kit.At;
            var w = Kit.Width;
            var h = S(34);
            var dl = Kit.Dl;
            if (!first) Kit.Divider(at.X, at.X + w, at.Y);
            first = false;
            string text;
            Vector4 textColor = Palette.Muted, iconColor = Palette.Faint;
            var key = id.ToString(En);
            if (!me.Retainers.TryGetValue(key, out var their))
            {
                text = "not read yet";
                textColor = Palette.Amber;
            }
            else
            {
                if (!lists.Others.TryGetValue(key, out var n))
                {
                    var lookItems = LookItems();
                    lists.Others[key] = n = their.Items.Select(LookJson.BaseId).Distinct().Count(i => lookItems.ContainsKey(i) && Where(i) is not (Place.Dresser or Place.Armoire));
                }
                text = n > 0 ? $"{n.ToString(En)} for your looks" : "none for your looks";
                if (n > 0) iconColor = Palette.Accent;
            }
            Icons.Draw(dl, Icon.User, new Vector2(at.X, at.Y + (h - S(14)) / 2), S(14), iconColor);
            var tw = Txt.Measure(text, Face.Sans, 12).X;
            Txt.Line(dl, new Vector2(at.X + w - tw, at.Y), text, Face.Sans, 12, textColor, h / S(1));
            var nx = at.X + S(14 + 8);
            Txt.Line(dl, new Vector2(nx, at.Y), Txt.Fit(name, Face.Sans, 13, MathF.Max(S(10), at.X + w - tw - S(8) - nx)), Face.Sans, 13, Palette.Ink, h / S(1));
            Kit.Reserve(at, w, h);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The market board and shops: your wishlist.

    private void PaneMarket()
    {
        if (!Linked)
        {
            NotLinked("Link your account (Settings → Account) and your wishlist shows here.");
            return;
        }
        var wishes = WishesShown(sync.Library.Wishes);
        var forLook = WishLooks();
        var sold = wishes.Where(i => data.Item(i)?.Untradable != true).ToList();
        var notSold = wishes.Where(i => data.Item(i)?.Untradable == true).ToList();
        PaneSec();
        SecHead("On the market board", $"{sold.Count.ToString(En)} of {wishes.Count.ToString(En)}");
        if (wishes.Count == 0) PaneText("Your wishlist is empty. Put pieces on it from a look, Examine or the fitting room.", 12, Palette.Muted);
        // The game's windows read once, not for every row: only the item whose results are open has a price.
        var open = GameWindows.MarketResultItem();
        var openPrice = open != 0 ? GameWindows.CheapestListed(open) : null;
        var searchShut = !GameWindows.IsShown("ItemSearch") && open == 0;
        foreach (var item in sold.Take(companionMore ? 40 : 8))
        {
            var line = forLook.TryGetValue(item, out var look) ? $"for {look}" : null;
            if (item == open && openPrice is { } price) line = $"{(line != null ? line + " · " : string.Empty)}from {price.ToString("N0", En)} gil";
            if (Where(item) != null) line = $"Yours now ({WhereOf(item).Tag.Text})";
            var name = data.ItemName(item);
            if (PieceRow($"##c-mb-{item}", item, null, line, button: "Search", buttonDisabled: searchShut,
                    tip: "Types it into the market board's search", own: Own.Star, picked: open == item, buttonIcon: Icon.Search))
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
        PaneText("Search types it into the market board. Buy one and it comes off the list by itself.");
        if (notSold.Count == 0) return;
        PaneSec();
        SecHead("Not on the market board", notSold.Count.ToString(En));
        foreach (var item in notSold.Take(companionMore ? 40 : 6))
        {
            var id = item;
            if (PieceRow($"##c-ns-{item}", item, null, forLook.TryGetValue(item, out var look) ? $"for {look} · untradable" : "untradable", button: "Take off",
                    tip: "Takes it off your wishlist", own: Own.Star, buttonStyle: Btn.Quiet))
                Later(() => Unwish(id));
        }
    }

    /// <summary>What the shop open sells of your wishlist, by name (read again each second: its window's values change as you page through it).</summary>
    private (string Name, int Version, List<uint> Found, DateTime At)? shopCache;

    private void PaneShop()
    {
        if (!Linked)
        {
            NotLinked("Link your account (Settings → Account) and what's on your wishlist is marked in shops.");
            return;
        }
        var wishes = sync.Library.Wishes;
        var name = shopName ?? string.Empty;
        if (shopCache is not { } c || c.Name != name || c.Version != sync.Version || DateTime.UtcNow - c.At > TimeSpan.FromSeconds(1))
            shopCache = c = (name, sync.Version, GameWindows.InShop(name, wishes).OrderBy(data.ItemName).ToList(), DateTime.UtcNow);
        var found = c.Found;
        var forLook = WishLooks();
        PaneSec();
        SecHead("Sold here", $"{found.Count.ToString(En)} of {wishes.Count.ToString(En)} on your list");
        if (found.Count == 0) PaneText(wishes.Count == 0 ? "Your wishlist is empty." : "Nothing on your wishlist is sold here, as far as this shop's window tells.", 12, Palette.Muted);
        else
        {
            foreach (var item in found)
                PieceRow($"##c-shop-{item}", item, null,
                    forLook.TryGetValue(item, out var look) ? $"for {look} · buy one and it comes off the list" : "buy one and it comes off the list", own: Own.Star);
            PaneText("As far as this shop's window tells.");
        }
    }
}

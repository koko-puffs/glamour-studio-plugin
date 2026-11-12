using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Make room for me, on Make room and in the dresser's panel: the plugin gathers outfit sets' loose
/// pieces into their sets (<see cref="OutfitStorer"/>), and takes the dresser's duplicates out to your
/// bags (<see cref="DresserTaker"/>). What each will do is said first, it's asked before it starts
/// (on Make room a question, in the panel a second press), and it's worked out again from the dresser
/// as it is the moment you say yes. A set's pieces are all one quality; a dyed piece loses its dye.
/// While it works, each set's row says where it is (watched from the storer's counts: done, not
/// taken, at work, waiting), and when it ends the field says what it made.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>What gathering sets would do now (<c>Pieces</c>: from the dresser; <c>Prisms</c>: every piece stored, those in your bags too).</summary>
    private sealed record AutoRoomPlan(List<OutfitJob> Jobs, int Frees, int Pieces, int Prisms, int MostAtOnce, int HeldBack)
    {
        /// <summary>Dyed pieces among them (storing takes the dye off).</summary>
        public int Dyed => Jobs.Sum(j => j.Pieces.Count(p => p.Dye1 != 0 || p.Dye2 != 0));
    }

    private (int Wardrobe, string Key, AutoRoomPlan Plan, List<DresserPiece> Dupes)? autoRoomCache;

    /// <summary>A set's row in a run: waiting, at work, stored, or left (the game didn't take it, or its pieces had gone).</summary>
    private enum RunRow
    {
        Waiting,
        Done,
        Left,
    }

    /// <summary>
    /// A run of Make room for me (or of taking out duplicates), as the screen follows it: the sets it
    /// was given and what became of each (the storer says only how many are done and left, so a set
    /// finished while the stored count went up was stored, else left), and how it ended.
    /// </summary>
    private sealed class RoomRun
    {
        public required string Key { get; init; }

        public required bool Dupes { get; init; }

        /// <summary>The sets, in the order the storer works them (none for duplicates).</summary>
        public required List<OutfitJob> Jobs { get; init; }

        /// <summary>How many the run had to do (sets, or duplicates).</summary>
        public required int Total { get; init; }

        public required RunRow[] Rows { get; init; }

        public required int[] Freed { get; init; }

        /// <summary>Rows finished so far, and the storer's counts when they were.</summary>
        public int Seen { get; set; }

        public int DoneSeen { get; set; }

        public int FreedSeen { get; set; }

        /// <summary>How it ended (null while it works).</summary>
        public string? Words { get; set; }

        public bool Ok { get; set; }

        public int Stored => Rows.Count(r => r == RunRow.Done);

        public int PlacesFreed => Freed.Sum();
    }

    private RoomRun? roomRun;

    /// <summary>The run on this character (null: none, or another's).</summary>
    private RoomRun? RunHere(CharacterWardrobe me) => roomRun is { } r && r.Key == me.Key ? r : null;

    private bool AutoBusy => OutfitStorer.Busy || DresserTaker.Busy;

    private (AutoRoomPlan Plan, List<DresserPiece> Dupes) AutoRoomNow(CharacterWardrobe me)
    {
        if (autoRoomCache is { } c && c.Wardrobe == wardrobe.Version && c.Key == me.Key) return (c.Plan, c.Dupes);
        var jobs = new List<OutfitJob>();
        var taken = new HashSet<int>();
        var (chances, heldBack) = DresserCleanup.Gather(me.Dresser, data.AllSets, DresserPlaces.ItemsInBags());
        var fromBags = 0;
        foreach (var chance in chances)
        {
            // A piece two sets share goes to the first: a new set without it can't be made.
            if (!chance.Stored && chance.Pieces.Any(p => taken.Contains(p.Index))) continue;
            var pieces = chance.Pieces.Where(p => !taken.Contains(p.Index)).ToList();
            var storedAt = chance.Stored
                ? me.Dresser.Where(d => d.InSet && d.SetItem == chance.SetItem + (chance.Hq ? LookJson.HqOffset : 0)).Select(d => d.Index).DefaultIfEmpty(-1).First()
                : -1;
            if (chance.Stored && storedAt < 0) continue;
            if (storedAt >= 0 ? pieces.Count < 1 : pieces.Count + chance.FromBags < 2) continue;
            foreach (var p in pieces) taken.Add(p.Index);
            fromBags += chance.FromBags;
            jobs.Add(new OutfitJob(chance.SetItem, pieces, storedAt, chance.Hq, chance.FromBags));
        }
        var plan = new AutoRoomPlan(jobs,
            jobs.Sum(JobFrees),
            jobs.Sum(j => j.Pieces.Count),
            jobs.Sum(j => j.Pieces.Count) + fromBags,
            jobs.Count == 0 ? 0 : jobs.Max(j => j.Pieces.Count),
            heldBack);
        var dupes = DresserCleanup.Duplicates(me.Dresser);
        autoRoomCache = (wardrobe.Version, me.Key, plan, dupes);
        return (plan, dupes);
    }

    /// <summary>The places a set frees: each loose piece's, less the new set's own place.</summary>
    private static int JobFrees(OutfitJob j) => j.StoredAt >= 0 ? j.Pieces.Count : Math.Max(0, j.Pieces.Count - 1);

    /// <summary>
    /// The first time, one set to try it on: the one with the fewest pieces (should the game not take
    /// it, little's moved), the same one named in the question and stored.
    /// </summary>
    private static OutfitJob TrialJob(AutoRoomPlan plan) => plan.Jobs.OrderBy(j => j.Pieces.Count).ThenBy(j => j.SetItem).First();

    /// <summary>Why gathering sets can't start now, or null (the first time, for the one set it tries).</summary>
    private string? AutoRoomBlocked(AutoRoomPlan plan, (int Prisms, int FreeBag) have)
    {
        if (AutoBusy) return "Already at work";
        if (plateIt != null || PlateEditor.IsOpen) return "Close Glamour Plates first";
        if (plan.Jobs.Count == 0) return "No set has loose pieces to gather";
        if (!GameWindows.IsShown("MiragePrismPrismBox") || !wardrobe.DresserLive) return "Open your glamour dresser first";
        var trial = config.OutfitStoreWorked ? null : TrialJob(plan);
        // (A prism for each piece stored: those from your bags too.)
        var prisms = trial != null ? trial.Pieces.Count + trial.FromBags : plan.Prisms;
        var atOnce = trial?.Pieces.Count ?? plan.MostAtOnce;
        if (have.Prisms < prisms) return $"It needs {Words.Count(prisms, "glamour prism")}: you have {have.Prisms}";
        if (have.FreeBag < atOnce) return $"It needs {Words.Count(atOnce, "free bag place")} at once: you have {have.FreeBag}";
        return null;
    }

    /// <summary>Why taking out duplicates can't start now, or null.</summary>
    private string? DupesBlocked(List<DresserPiece> dupes, (int Prisms, int FreeBag) have)
    {
        if (AutoBusy) return "Already at work";
        if (plateIt != null || PlateEditor.IsOpen) return "Close Glamour Plates first";
        if (dupes.Count == 0) return "No duplicates in your dresser";
        if (!GameWindows.IsShown("MiragePrismPrismBox") || !wardrobe.DresserLive) return "Open your glamour dresser first";
        if (have.FreeBag == 0) return "Your bags are full";
        return null;
    }

    private string AutoRoomWords(AutoRoomPlan plan, (int Prisms, int FreeBag) have) =>
        $"{Words.Count(plan.Jobs.Count, "set")} from {Words.Count(plan.Pieces, "loose piece")}: frees {Words.Count(plan.Frees, "place")}. "
        + $"Each piece goes to your bags and back in as its set, spending a glamour prism ({plan.Prisms} of your {have.Prisms}); "
        + $"it needs {plan.MostAtOnce} free bag places at a time (you have {have.FreeBag})."
        + (plan.Prisms > plan.Pieces ? $" {Words.Count(plan.Prisms - plan.Pieces, "piece")} of these sets in your bags go in with them." : string.Empty)
        + HeldBackWords(plan);

    /// <summary>The dyed pieces that lose their dye, and the sets left out for pieces of both qualities.</summary>
    private static string HeldBackWords(AutoRoomPlan plan) =>
        (plan.Dyed > 0 ? $" {Words.Count(plan.Dyed, "dyed piece")} lose their dye (storing takes it off; your plates keep their own)." : string.Empty)
        + (plan.HeldBack > 0 ? $" {Words.Count(plan.HeldBack, "set")} left: their pieces are of both qualities, and a set takes one." : string.Empty);

    /// <summary>A small number in words ("three"), figures past twelve.</summary>
    private static string Few(int n) => n is >= 0 and <= 12 ? Words.InWords(n).ToLowerInvariant() : n.ToString("N0");

    /// <summary>The question the first time: the one set it tries, what it moves and spends, what it frees and needs (the mock's frame 2).</summary>
    private string TrialWords(AutoRoomPlan plan, (int Prisms, int FreeBag) have)
    {
        var t = TrialJob(plan);
        var n = t.Pieces.Count;
        var prisms = n + t.FromBags;
        var dyed = t.Pieces.Count(p => p.Dye1 != 0 || p.Dye2 != 0);
        var into = t.StoredAt >= 0 ? "into the set stored there" : "back in as the set";
        return $"It stores {data.ItemName(t.SetItem)}: its {Few(n)} loose {(n == 1 ? "piece goes" : "pieces go")} to your bags and {into}, "
               + $"spending {Few(prisms)} glamour {(prisms == 1 ? "prism" : "prisms")} (you have {have.Prisms})."
               + (t.FromBags > 0 ? " Its pieces already in your bags go in with it." : string.Empty)
               + (dyed > 0 ? $" {(dyed == 1 ? "One is dyed and loses its dye" : $"{Few(dyed)} are dyed and lose their dye")} (your plates keep their own)." : string.Empty)
               + $" It frees {Words.Count(JobFrees(t), "place")} in your dresser, and needs {Words.Count(n, "free bag place")} at a time (you have {have.FreeBag})."
               + " Keep the dresser open while it works."
               + (plan.Jobs.Count > 1 ? $" Once it's worked, the next press does the other {Few(plan.Jobs.Count - 1)}." : string.Empty);
    }

    /// <summary>The duplicates in a line: "4 second copies with the same dyes, and 1 set stored twice".</summary>
    private string DupesSummary(List<DresserPiece> dupes)
    {
        var sets = dupes.Count(d => data.SetPieces(LookJson.BaseId(d.Item)).Count > 0);
        var pieces = dupes.Count - sets;
        var parts = new List<string>();
        if (pieces > 0) parts.Add($"{Words.Count(pieces, "second copy", "second copies")} with the same dyes");
        if (sets > 0) parts.Add($"{Words.Count(sets, "set")} stored twice");
        return parts.Count == 0 ? "None: no second copy with the same dyes, no set stored twice." : string.Join(", and ", parts);
    }

    /// <summary>The question for the duplicates: what comes out, the bag places, and the two promises (the mock's frame 4).</summary>
    private string DupesWords(CharacterWardrobe me, List<DresserPiece> dupes, (int Prisms, int FreeBag) have)
    {
        var sets = dupes.Where(d => data.SetPieces(LookJson.BaseId(d.Item)).Count > 0).ToList();
        var pieces = dupes.Count - sets.Count;
        // A set's copy comes out whole: each of its pieces takes a bag place.
        var places = pieces + sets.Sum(s => Math.Max(1, me.Dresser.Count(d => d.Index == s.Index)));
        var what = new List<string>();
        if (pieces > 0)
        {
            var names = Words.List(dupes.Except(sets).Select(d => data.ItemName(d.Item)).Distinct().Take(3)) + (pieces > 3 ? " and more" : string.Empty);
            what.Add($"{Few(pieces)} {(pieces == 1 ? "piece" : "pieces")} ({names})");
        }
        if (sets.Count > 0)
            what.Add($"the second copy of {Words.List(sets.Select(s => data.ItemName(s.Item)).Distinct().Take(3))} ({(sets.Count == 1 ? "its pieces all come out" : "their pieces all come out")})");
        return $"They come out to your bags, one at a time: {string.Join(", and ", what)}. It needs {Words.Count(places, "free bag place")} (you have {have.FreeBag}; it stops when they're full). "
               + "Never the last copy, never a Unique piece you hold. Nothing's thrown away: sell or discard them yourself. Keep the dresser open while it works.";
    }

    /// <summary>Asks before gathering the sets (one set the first time).</summary>
    private void AskAutoRoom(AutoRoomPlan plan, (int Prisms, int FreeBag) have)
    {
        if (config.OutfitStoreWorked)
            Ask("Store the sets for you?",
                AutoRoomWords(plan, have) + " Keep the dresser open while it works; a set the game doesn't take is left (its pieces in your bags) and it goes on.",
                "Store them", "Not now", StartAutoRoom);
        else Ask("Try it on one set?", TrialWords(plan, have), "Try it", "Not now", StartAutoRoom);
    }

    private void AskDupes(CharacterWardrobe me, List<DresserPiece> dupes, (int Prisms, int FreeBag) have) =>
        Ask("Take out the duplicates?", DupesWords(me, dupes, have), "Take them out", "Not now", StartDupes);

    /// <summary>Gathers the sets: worked out again from the dresser as it is now, and checked, then started.</summary>
    private void StartAutoRoom()
    {
        if (AutoBusy || wardrobe.Current is not { } me) return;
        wardrobe.Tick(now: true);
        autoRoomCache = null;
        var (plan, _) = AutoRoomNow(me);
        if (AutoRoomBlocked(plan, OutfitStorer.Supplies(data)) is { } why)
        {
            Frame.Warn(why);
            return;
        }
        // Until it has worked here once, one set: should the game not take it, little's moved.
        var jobs = config.OutfitStoreWorked ? plan.Jobs : new List<OutfitJob> { TrialJob(plan) };
        var run = new RoomRun { Key = me.Key, Dupes = false, Jobs = jobs, Total = jobs.Count, Rows = new RunRow[jobs.Count], Freed = new int[jobs.Count] };
        roomRun = run;
        recorder.Note($"MAKE ROOM for me: {jobs.Count} of {plan.Jobs.Count} sets, frees {plan.Frees}{(config.OutfitStoreWorked ? string.Empty : " (trying one)")}");
        var started = OutfitStorer.Start(jobs, data, (words, ok) =>
        {
            FollowRun(run, ok, words);
            autoRoomCache = null;
            wardrobe.Tick(now: true);
            if (ok && !config.OutfitStoreWorked)
            {
                config.OutfitStoreWorked = true;
                config.Save();
                words += ". It worked: next time it does them all";
            }
            run.Words = words;
            run.Ok = ok;
            if (ok) Notify($"Made room: {words}");
            else Frame.Warn(words);
        });
        if (!started)
        {
            if (ReferenceEquals(roomRun, run)) roomRun = null;
            return;
        }
        if (!OutfitStorer.Busy) return;
        Notify(jobs.Count == 1 && !config.OutfitStoreWorked ? "Trying one set first…" : $"Storing {Words.Count(jobs.Count, "set")}…");
    }

    /// <summary>Takes out the duplicates: worked out again, checked, then started.</summary>
    private void StartDupes()
    {
        if (AutoBusy || wardrobe.Current is not { } me) return;
        wardrobe.Tick(now: true);
        autoRoomCache = null;
        var (_, dupes) = AutoRoomNow(me);
        if (DupesBlocked(dupes, OutfitStorer.Supplies(data)) is { } why)
        {
            Frame.Warn(why);
            return;
        }
        recorder.Note($"TAKE OUT duplicates: {dupes.Count}");
        var run = new RoomRun { Key = me.Key, Dupes = true, Jobs = new List<OutfitJob>(), Total = dupes.Count, Rows = Array.Empty<RunRow>(), Freed = Array.Empty<int>() };
        roomRun = run;
        var started = DresserTaker.Start(dupes, data, (words, ok) =>
        {
            autoRoomCache = null;
            wardrobe.Tick(now: true);
            run.Words = words;
            run.Ok = ok;
            if (ok) Notify(words);
            else Frame.Warn(words);
        });
        if (!started)
        {
            if (ReferenceEquals(roomRun, run)) roomRun = null;
            return;
        }
        if (!DresserTaker.Busy) return;
        Notify($"Taking out {Words.Count(dupes.Count, "duplicate")}…");
    }

    /// <summary>
    /// Brings a storing run's rows up to date from the storer's counts: each set finished since is
    /// stored when the stored count went up with it, else left (the earlier finished first: a set
    /// stored and a set skipped in one frame are in that order). At the end (<paramref name="endedOk"/>
    /// given), the sets stored meanwhile are the next rows; a run that went through has finished every
    /// row (the rest were left), one that stopped left the set at work and never reached the rest.
    /// </summary>
    private void FollowRun(RoomRun run, bool? endedOk = null, string? words = null)
    {
        if (run.Dupes || run.Words != null) return;
        var finished = endedOk != null ? run.Total : Math.Clamp(run.Total - OutfitStorer.SetsLeft, 0, run.Total);
        var stoppedAt = -1;
        // At the end the words name the sets the game didn't take (should the screen not have watched it all).
        var notTaken = words != null && words.IndexOf("didn't take ", StringComparison.Ordinal) is var k and >= 0 ? words[(k + 12)..] : string.Empty;
        while (run.Seen < finished)
        {
            var newlyDone = OutfitStorer.SetsDone - run.DoneSeen;
            if (notTaken.Length > 0 && notTaken.Contains(data.ItemName(run.Jobs[run.Seen].SetItem), StringComparison.Ordinal))
                run.Rows[run.Seen] = RunRow.Left;
            else if (newlyDone > 0)
            {
                var freedNow = Math.Max(0, OutfitStorer.PlacesFreed - run.FreedSeen);
                var freed = newlyDone == 1 ? freedNow : Math.Min(JobFrees(run.Jobs[run.Seen]), freedNow);
                run.Rows[run.Seen] = RunRow.Done;
                run.Freed[run.Seen] = freed;
                run.DoneSeen++;
                run.FreedSeen += freed;
            }
            else if (endedOk == false)
            {
                // Stopped: the set at work was left; the rest weren't reached (they stay waiting).
                if (stoppedAt < 0)
                {
                    stoppedAt = run.Seen;
                    run.Rows[run.Seen] = RunRow.Left;
                }
                run.Seen++;
                continue;
            }
            else run.Rows[run.Seen] = RunRow.Left;
            run.Seen++;
        }
        if (stoppedAt >= 0) run.Seen = stoppedAt + 1;
    }

    // ---------------------------------------------------------------------------------------------
    // In the dresser's panel (Companions/Panes.cs lays it under the dresser's line).

    /// <summary>
    /// Make room for me in the dresser's panel (the mock's companion frame, pins 6 and 7): while it
    /// works, what it's at with Stop, the violet bar and what's freed; else each button pressed twice
    /// (the first press turns it amber with what the second will do: no question card over the game),
    /// a line under it, and how the last run ended.
    /// </summary>
    private void PaneAutoRoom(CharacterWardrobe me)
    {
        if (RunHere(me) is { } live) FollowRun(live);
        if (AutoBusy)
        {
            var row = Kit.At;
            var w = Kit.Width;
            var stopW = Kit.ButtonWidth("Stop", BtnSize.Tiny, Icon.Square);
            var rh = Kit.ButtonHeight(BtnSize.Tiny);
            var storing = OutfitStorer.Busy;
            var done = storing ? OutfitStorer.SetsDone : DresserTaker.Done;
            var head = storing ? $"Storing sets for you… {done} done" : $"Taking out duplicates… {done} out";
            Txt.Draw(Kit.Dl, new Vector2(row.X, row.Y + (rh - Txt.Natural(Face.SansSemibold, 12)) / 2), Txt.Fit(head, Face.SansSemibold, 12, w - stopW - S(8)), Face.SansSemibold, 12, Palette.Ink);
            if (Kit.Button("##c-auto-stop", new Vector2(row.X + w - stopW, row.Y), "Stop", Btn.Glass, BtnSize.Tiny, Icon.Square, 0, false, "Stops once the step the game is answering is done", 0, 12))
                Later(() =>
                {
                    OutfitStorer.Stop();
                    DresserTaker.Stop();
                });
            Kit.Reserve(row, w, rh);
            Kit.Gap(8);
            var left = storing ? OutfitStorer.SetsLeft : DresserTaker.Left;
            var total = done + left;
            var mat = Kit.At;
            Kit.Meter(mat, w, total > 0 ? (float)(total - left) / total : 0, MeterKind.Working);
            Kit.Reserve(mat, w, S(6));
            Kit.Gap(8);
            Txt.Para(new[] { new Run(storing ? $"{Words.Count(OutfitStorer.PlacesFreed, "place")} freed. Keep the dresser open." : $"{left} to go. Keep the dresser open.", Face.Sans, 11, Palette.Muted) }, 15);
            return;
        }
        var (plan, dupes) = AutoRoomNow(me);
        var have = OutfitStorer.Supplies(data);
        if (plan.Jobs.Count > 0)
        {
            var blocked = AutoRoomBlocked(plan, have);
            PaneArmedButton("##c-auto-room", config.OutfitStoreWorked ? $"Store {Words.Count(plan.Jobs.Count, "set")} for me" : "Try it on one set",
                config.OutfitStoreWorked ? "Press again to store them" : "Press again to try it", Icon.Package, blocked, AutoRoomWords(plan, have),
                ref autoRoomArmedUntil, StartAutoRoom);
            Kit.Gap(6);
            var say = blocked ?? (config.OutfitStoreWorked
                ? $"Stores {Words.Count(plan.Jobs.Count, "set")} for you: frees {Words.Count(plan.Frees, "place")}, spends {Words.Count(plan.Prisms, "glamour prism")} (you have {have.Prisms}).{HeldBackWords(plan)}"
                : $"The first time, one set ({data.ItemName(TrialJob(plan).SetItem)}), so you can check it worked: frees {Words.Count(JobFrees(TrialJob(plan)), "place")}.");
            Txt.Para(new[] { new Run(say, Face.Sans, 11, blocked != null ? Palette.Amber : Palette.Muted) }, 15);
            Kit.Gap(10);
        }
        if (dupes.Count > 0)
        {
            var blocked = DupesBlocked(dupes, have);
            PaneArmedButton("##c-dupes", $"Take out {Words.Count(dupes.Count, "duplicate")}", "Press again to take them out", Icon.Trash, blocked, DupesWords(me, dupes, have),
                ref dupesArmedUntil, StartDupes);
            Kit.Gap(6);
            Txt.Para(new[] { new Run(blocked ?? $"{DupesSummary(dupes)}: they come out to your bags, freeing {Words.Count(dupes.Count, "place")}.", Face.Sans, 11, blocked != null ? Palette.Amber : Palette.Muted) }, 15);
            Kit.Gap(10);
        }
        if (RunHere(me) is { Words: { } words } last)
            Txt.Para(new[] { new Run(words, Face.Sans, 11, last.Ok ? Palette.Ice : Palette.Amber) }, 15);
    }

    /// <summary>The panel's buttons pressed once: pressed again before then, they start.</summary>
    private DateTime autoRoomArmedUntil, dupesArmedUntil;

    /// <summary>
    /// A panel button pressed twice: outlined; the first press arms it for five seconds, amber with
    /// "Press again…"; the second starts.
    /// </summary>
    private void PaneArmedButton(string id, string label, string armedLabel, Icon icon, string? blocked, string tip, ref DateTime armedUntil, Action start)
    {
        var row = Kit.At;
        var armed = blocked == null && DateTime.UtcNow < armedUntil;
        var text = armed ? armedLabel : label;
        var w = MathF.Min(Kit.ButtonWidth(text, BtnSize.Sm, icon), Kit.Width);
        bool pressed;
        if (!armed) pressed = Kit.Button(id, row, text, Btn.Glass, BtnSize.Sm, icon, w, blocked != null, blocked ?? tip, 0, 14);
        else
        {
            // Amber: something to see to before the second press (the Kit's pills have no amber of their own).
            var h = Kit.ButtonHeight(BtnSize.Sm);
            var (clicked, hovered, _) = Kit.Hit(id, row, new Vector2(w, h));
            Kit.FocusRing(row, row + new Vector2(w, h), h / 2);
            var dl = Kit.Dl;
            dl.AddRectFilled(row, row + new Vector2(w, h), Palette.U32(hovered ? Palette.WithAlpha(Palette.Amber, 0.2f) : Palette.AmberWash), h / 2);
            dl.AddRect(row + new Vector2(0.5f), row + new Vector2(w, h) - new Vector2(0.5f), Palette.U32(Palette.Amber), h / 2, Dalamud.Bindings.ImGui.ImDrawFlags.None, 1f);
            var room = w - S(13 * 2 + 14 + 7);
            var shown = Txt.Fit(text, Face.SansSemibold, 13, room);
            var tw = Txt.Measure(shown, Face.SansSemibold, 13).X;
            var x = row.X + (w - S(14 + 7) - tw) / 2;
            Icons.Draw(dl, icon, new Vector2(x, row.Y + (h - S(14)) / 2), S(14), Palette.Amber);
            Txt.Line(dl, new Vector2(x + S(14 + 7), row.Y), shown, Face.SansSemibold, 13, Palette.Amber, 34);
            if (hovered) Kit.Tooltip(tip);
            pressed = clicked;
        }
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        if (!pressed) return;
        if (armed)
        {
            armedUntil = DateTime.MinValue;
            Later(start);
        }
        else armedUntil = DateTime.UtcNow + TimeSpan.FromSeconds(5);
    }

    /// <summary>How the last run on this character ended (null: none yet, or it's at work).</summary>
    private (string Words, bool Ok)? autoRoomLast =>
        wardrobe.Current is { } me && RunHere(me) is { Words: { } words } r ? (words, r.Ok) : null;

    /// <summary>The duplicates question's words for the character you're on.</summary>
    private string DupesWords(List<DresserPiece> dupes, (int Prisms, int FreeBag) have) =>
        wardrobe.Current is { } me ? DupesWords(me, dupes, have) : DupesSummary(dupes);
}

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

    /// <summary>How it ended last time, for the screen (and whether all went).</summary>
    private (string Words, bool Ok)? autoRoomLast;

    /// <summary>The panel's buttons pressed once: pressed again before then, they start.</summary>
    private DateTime autoRoomArmedUntil, dupesArmedUntil;

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
            jobs.Sum(j => j.StoredAt >= 0 ? j.Pieces.Count : Math.Max(0, j.Pieces.Count - 1)),
            jobs.Sum(j => j.Pieces.Count),
            jobs.Sum(j => j.Pieces.Count) + fromBags,
            jobs.Count == 0 ? 0 : jobs.Max(j => j.Pieces.Count),
            heldBack);
        var dupes = DresserCleanup.Duplicates(me.Dresser);
        autoRoomCache = (wardrobe.Version, me.Key, plan, dupes);
        return (plan, dupes);
    }

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

    private string DupesWords(List<DresserPiece> dupes, (int Prisms, int FreeBag) have)
    {
        var names = Words.List(dupes.Select(d => data.ItemName(d.Item)).Distinct().Take(4)) + (dupes.Count > 4 ? " and more" : string.Empty);
        var sets = dupes.Count(d => data.SetPieces(LookJson.BaseId(d.Item)).Count > 0);
        return $"{Words.Count(dupes.Count, "duplicate")} ({names}): a second copy with the same dyes, an undyed copy of a piece a stored set holds"
               + (sets > 0 ? $", or a stored set another copy of it has every piece of ({sets}: its pieces all come out)" : string.Empty) + ". "
               + $"They come out to your bags (free {Words.Count(dupes.Count, "place")} here; you have {have.FreeBag} free bag places, it stops when they're full). "
               + "Nothing's thrown away: sell or discard them yourself.";
    }

    /// <summary>Gathers the sets: worked out again from the dresser as it is now, and checked, then started.</summary>
    private void StartAutoRoom()
    {
        if (AutoBusy || wardrobe.Current is not { } me) return;
        wardrobe.Tick(now: true);
        autoRoomCache = null;
        var (plan, _) = AutoRoomNow(me);
        if (AutoRoomBlocked(plan, OutfitStorer.Supplies(data)) is { } why)
        {
            Notify(why);
            return;
        }
        // Until it has worked here once, one set: should the game not take it, little's moved.
        var jobs = config.OutfitStoreWorked ? plan.Jobs : new List<OutfitJob> { TrialJob(plan) };
        autoRoomLast = null;
        recorder.Note($"MAKE ROOM for me: {jobs.Count} of {plan.Jobs.Count} sets, frees {plan.Frees}{(config.OutfitStoreWorked ? string.Empty : " (trying one)")}");
        var started = OutfitStorer.Start(jobs, data, (words, ok) =>
        {
            autoRoomLast = (words, ok);
            autoRoomCache = null;
            wardrobe.Tick(now: true);
            if (ok && !config.OutfitStoreWorked)
            {
                config.OutfitStoreWorked = true;
                config.Save();
                words += ". It worked: next time it does them all";
                autoRoomLast = (words, ok);
            }
            Notify(ok ? $"Made room: {words}" : words);
        });
        if (!started || !OutfitStorer.Busy) return;
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
            Notify(why);
            return;
        }
        recorder.Note($"TAKE OUT duplicates: {dupes.Count}");
        autoRoomLast = null;
        var started = DresserTaker.Start(dupes, data, (words, ok) =>
        {
            autoRoomLast = (words, ok);
            autoRoomCache = null;
            wardrobe.Tick(now: true);
            Notify(words);
        });
        if (!started || !DresserTaker.Busy) return;
        Notify($"Taking out {Words.Count(dupes.Count, "duplicate")}…");
    }

    /// <summary>On Make room: what each would do, its button (asked first), or how it's going.</summary>
    private void DrawAutoRoom(CharacterWardrobe me)
    {
        Kit.BeginPanel("##auto-room", 14, 16, 14, Palette.RoseWash, Palette.RoseEdge, 14);
        var w = Kit.Width;
        if (AutoBusy)
        {
            var doing = OutfitStorer.Busy
                ? $"Storing sets for you… {OutfitStorer.SetsDone} done, {Words.Count(OutfitStorer.PlacesFreed, "place")} freed, {OutfitStorer.SetsLeft} to go."
                : $"Taking out duplicates… {DresserTaker.Done} out, {DresserTaker.Left} to go.";
            Txt.Para(new[] { new Run(doing + " Keep the dresser open.", Face.Sans, 13, Palette.Soft) }, 19.5f);
            Kit.Gap(10);
            var row = Kit.At;
            if (Kit.Button("##auto-stop", row, "Stop", Btn.Danger, BtnSize.Sm, Icon.Square, 0, false, "Stops once the step the game is answering is done"))
                Later(() =>
                {
                    OutfitStorer.Stop();
                    DresserTaker.Stop();
                });
            Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Sm));
            Kit.EndPanel();
            return;
        }
        var (plan, dupes) = AutoRoomNow(me);
        var have = OutfitStorer.Supplies(data);

        var setsBlocked = AutoRoomBlocked(plan, have);
        AutoRow("##auto-room-go", plan.Jobs.Count > 0 ? $"Gather the sets: frees {Words.Count(plan.Frees, "place")}" : "Gather the sets",
            setsBlocked ?? AutoRoomWords(plan, have), setsBlocked != null,
            plan.Jobs.Count == 0 ? "Store the sets for me" : config.OutfitStoreWorked ? $"Store {Words.Count(plan.Jobs.Count, "set")} for me" : "Try it on one set", Icon.Package,
            () => Ask(config.OutfitStoreWorked ? "Store the sets for you?" : "Try it on one set?",
                (config.OutfitStoreWorked ? AutoRoomWords(plan, have) : $"The first time, it stores one set ({data.ItemName(TrialJob(plan).SetItem)}), so you can check it worked before it does the other {plan.Jobs.Count - 1}. Its pieces already in your bags go in with it.")
                + " Keep the dresser open while it works; a set the game doesn't take is left (its pieces in your bags) and it goes on.",
                config.OutfitStoreWorked ? "Store them" : "Try it", "Not now", StartAutoRoom));
        Kit.Gap(12);
        Kit.Divider(Kit.Left, Kit.Left + w, Kit.At.Y);
        Kit.Gap(12);
        var dupesBlocked = DupesBlocked(dupes, have);
        AutoRow("##auto-dupes-go", dupes.Count > 0 ? $"Duplicates: {Words.Count(dupes.Count, "place")} to free" : "Duplicates",
            dupesBlocked ?? DupesWords(dupes, have), dupesBlocked != null,
            dupes.Count > 0 ? $"Take out {Words.Count(dupes.Count, "duplicate")}" : "Take out duplicates", Icon.Trash,
            () => Ask("Take out the duplicates?", DupesWords(dupes, have) + " Keep the dresser open while it works.", "Take them out", "Not now", StartDupes));
        if (autoRoomLast is { } last)
        {
            Kit.Gap(10);
            Txt.Para(new[] { new Run(last.Words, Face.Sans, 12, last.Ok ? Palette.Success : Palette.RoseText) }, 18);
        }
        Kit.EndPanel();
    }

    /// <summary>One of Make room for me's rows: a heading and its words, its button beside them (under them in a narrow window).</summary>
    private void AutoRow(string id, string head, string line, bool blocked, string label, Icon icon, Action ask)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var bw = Kit.ButtonWidth(label, BtnSize.Md, icon, Btn.Primary);
        var stacked = w - bw - S(16) < S(260);
        var textW = stacked ? w : w - bw - S(16);
        Txt.Draw(Kit.Dl, at, Txt.Fit(head, Face.SansSemibold, 14, textW), Face.SansSemibold, 14, Palette.Ink);
        var lh = Txt.Flow(new Vector2(at.X, at.Y + Txt.Natural(Face.SansSemibold, 14) + S(4)), textW, new[] { new Run(line, Face.Sans, 12, blocked ? Palette.RoseText : Palette.Soft) }, 18, out _);
        var blockH = Txt.Natural(Face.SansSemibold, 14) + S(4) + lh;
        var bh = Kit.ButtonHeight(BtnSize.Md);
        var bpos = stacked ? new Vector2(at.X, at.Y + blockH + S(10)) : new Vector2(at.X + w - bw, at.Y + MathF.Max(0, (blockH - bh) / 2));
        if (Kit.Button(id, bpos, label, Btn.Primary, BtnSize.Md, icon, 0, blocked, blocked ? line : "Asks first. New: not yet watched in the game"))
            Later(ask);
        Kit.Reserve(at, w, stacked ? blockH + S(10) + bh : MathF.Max(blockH, bh));
    }

    /// <summary>In the dresser's panel: the same, smaller; each button pressed once asks to be pressed again.</summary>
    private void PaneAutoRoom(CharacterWardrobe me)
    {
        if (AutoBusy)
        {
            PaneNote(OutfitStorer.Busy
                ? $"Storing sets for you: {OutfitStorer.SetsDone} done, {OutfitStorer.SetsLeft} to go. Keep the dresser open."
                : $"Taking out duplicates: {DresserTaker.Done} out, {DresserTaker.Left} to go. Keep the dresser open.", Palette.RoseText);
            Kit.Gap(6);
            var srow = Kit.At;
            if (Kit.Button("##c-auto-stop", srow, "Stop", Btn.Danger, BtnSize.Sm, Icon.Square))
                Later(() =>
                {
                    OutfitStorer.Stop();
                    DresserTaker.Stop();
                });
            Kit.Reserve(srow, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
            return;
        }
        var (plan, dupes) = AutoRoomNow(me);
        var have = OutfitStorer.Supplies(data);
        if (plan.Jobs.Count > 0)
        {
            var blocked = AutoRoomBlocked(plan, have);
            PaneNote(blocked ?? $"{Words.Count(plan.Jobs.Count, "set")} could gather {Words.Count(plan.Pieces, "loose piece")}: frees {Words.Count(plan.Frees, "place")}, for {Words.Count(plan.Prisms, "glamour prism")} (you have {have.Prisms}). Needs {Words.Count(plan.MostAtOnce, "free bag place")} at a time (you have {have.FreeBag}).{HeldBackWords(plan)}",
                blocked != null ? Palette.RoseText : null);
            Kit.Gap(6);
            PaneArmedButton("##c-auto-room", config.OutfitStoreWorked ? $"Store {Words.Count(plan.Jobs.Count, "set")} for me" : "Try it on one set",
                config.OutfitStoreWorked ? "Press again to store them" : "Press again to try it", Icon.Package, blocked, AutoRoomWords(plan, have),
                ref autoRoomArmedUntil, StartAutoRoom);
            Kit.Gap(10);
        }
        if (dupes.Count > 0)
        {
            var blocked = DupesBlocked(dupes, have);
            PaneNote(blocked ?? $"{Words.Count(dupes.Count, "duplicate")} could come out to your bags: frees {Words.Count(dupes.Count, "place")}.", blocked != null ? Palette.RoseText : null);
            Kit.Gap(6);
            PaneArmedButton("##c-dupes", $"Take out {Words.Count(dupes.Count, "duplicate")}", "Press again to take them out", Icon.Trash, blocked, DupesWords(dupes, have),
                ref dupesArmedUntil, StartDupes);
            Kit.Gap(10);
        }
        if (autoRoomLast is { } last) PaneNote(last.Words, last.Ok ? Palette.Success : Palette.RoseText);
    }

    /// <summary>A panel button pressed twice: the first press arms it for five seconds, the second starts.</summary>
    private void PaneArmedButton(string id, string label, string armedLabel, Icon icon, string? blocked, string tip, ref DateTime armedUntil, Action start)
    {
        var row = Kit.At;
        var armed = DateTime.UtcNow < armedUntil;
        var pressed = Kit.Button(id, row, armed ? armedLabel : label, armed ? Btn.Primary : Btn.Glass, BtnSize.Sm, icon, 0, blocked != null, blocked ?? tip);
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        if (!pressed) return;
        if (armed)
        {
            armedUntil = DateTime.MinValue;
            Later(start);
        }
        else armedUntil = DateTime.UtcNow + TimeSpan.FromSeconds(5);
    }
}

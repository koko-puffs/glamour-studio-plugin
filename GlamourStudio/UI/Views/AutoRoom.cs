using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GlamourStudio.Core;
using GlamourStudio.Game;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>
/// Make room for me: the plugin gathers outfit sets' loose pieces in the dresser into their sets
/// (<see cref="OutfitStorer"/>), on Make room and in the dresser's panel. What it'll do is said
/// first (the sets, the places freed, the glamour prisms it spends, the bag places it needs), and
/// it's asked before it starts. Pieces with dyes are left loose: a set may not keep a piece's dyes.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>What making room for you would do now.</summary>
    private sealed record AutoRoomPlan(List<OutfitJob> Jobs, int Frees, int Pieces, int MostAtOnce, int SkippedDyed);

    private (int Wardrobe, string Key, AutoRoomPlan Plan)? autoRoomCache;

    /// <summary>How it ended last time, for the screen (and whether all went).</summary>
    private (string Words, bool Ok)? autoRoomLast;

    /// <summary>The panel's Store them for me pressed once: pressed again before then, it starts.</summary>
    private DateTime autoRoomArmedUntil;

    private AutoRoomPlan AutoRoomNow(CharacterWardrobe me)
    {
        if (autoRoomCache is { } c && c.Wardrobe == wardrobe.Version && c.Key == me.Key) return c.Plan;
        var jobs = new List<OutfitJob>();
        var taken = new HashSet<int>();
        var skipped = 0;
        foreach (var chance in DresserCleanup.Outfits(me.Dresser, data.AllSets))
        {
            var pieces = new List<DresserPiece>();
            foreach (var p in chance.Pieces)
            {
                if (taken.Contains(p.Index)) continue;
                if (p.Dye1 != 0 || p.Dye2 != 0)
                {
                    skipped++;
                    continue;
                }
                pieces.Add(p);
            }
            var storedAt = chance.Stored
                ? me.Dresser.Where(d => d.InSet && LookJson.BaseId(d.SetItem) == LookJson.BaseId(chance.SetItem)).Select(d => d.Index).DefaultIfEmpty(-1).First()
                : -1;
            if (chance.Stored && storedAt < 0) continue;
            // A new set of one piece frees nothing.
            if (pieces.Count < (storedAt >= 0 ? 1 : 2)) continue;
            foreach (var p in pieces) taken.Add(p.Index);
            jobs.Add(new OutfitJob(chance.SetItem, pieces, storedAt));
        }
        var plan = new AutoRoomPlan(jobs,
            jobs.Sum(j => j.StoredAt >= 0 ? j.Pieces.Count : j.Pieces.Count - 1),
            jobs.Sum(j => j.Pieces.Count),
            jobs.Count == 0 ? 0 : jobs.Max(j => j.Pieces.Count),
            skipped);
        autoRoomCache = (wardrobe.Version, me.Key, plan);
        return plan;
    }

    /// <summary>Why making room for you can't start now, or null.</summary>
    private string? AutoRoomBlocked(AutoRoomPlan plan, (int Prisms, int FreeBag) have)
    {
        if (plan.Jobs.Count == 0) return "No set has loose pieces to gather";
        if (!GameWindows.IsShown("MiragePrismPrismBox") || !wardrobe.DresserLive) return "Open your glamour dresser first";
        if (have.Prisms < plan.Pieces) return $"It needs {Words.Count(plan.Pieces, "glamour prism")}: you have {have.Prisms}";
        if (have.FreeBag < plan.MostAtOnce) return $"It needs {Words.Count(plan.MostAtOnce, "free bag place")} at once: you have {have.FreeBag}";
        return null;
    }

    private string AutoRoomWords(AutoRoomPlan plan, (int Prisms, int FreeBag) have) =>
        $"{Words.Count(plan.Jobs.Count, "set")} from {Words.Count(plan.Pieces, "loose piece")}: frees {Words.Count(plan.Frees, "place")}. "
        + $"Each piece goes to your bags and back in as its set, spending a glamour prism ({plan.Pieces} of your {have.Prisms}); "
        + $"it needs {plan.MostAtOnce} free bag places at a time (you have {have.FreeBag})."
        + (plan.SkippedDyed > 0 ? $" {Words.Count(plan.SkippedDyed, "dyed piece")} stay loose (a set may not keep its dyes)." : string.Empty);

    private void StartAutoRoom(AutoRoomPlan plan)
    {
        wardrobe.Tick(now: true);
        recorder.Note($"MAKE ROOM for me: {plan.Jobs.Count} sets, {plan.Pieces} pieces, frees {plan.Frees}");
        autoRoomLast = null;
        OutfitStorer.Start(plan.Jobs, data, (words, ok) =>
        {
            autoRoomLast = (words, ok);
            autoRoomCache = null;
            wardrobe.Tick(now: true);
            Notify(ok ? $"Made room: {words}" : words);
        });
        Notify($"Storing {Words.Count(plan.Jobs.Count, "set")}…");
    }

    /// <summary>On Make room: what it would do, Store them for me (asked first), or how it's going.</summary>
    private void DrawAutoRoom(CharacterWardrobe me)
    {
        Kit.BeginPanel("##auto-room", 14, 16, 14, Palette.RoseWash, Palette.RoseEdge, 14);
        var at = Kit.At;
        var w = Kit.Width;
        if (OutfitStorer.Busy)
        {
            Txt.Para(new[]
            {
                new Run("Storing sets for you… ", Face.SansSemibold, 13, Palette.Ink),
                new Run($"{OutfitStorer.SetsDone} done, {Words.Count(OutfitStorer.PlacesFreed, "place")} freed, {OutfitStorer.SetsLeft} to go. Keep the dresser open.", Face.Sans, 13, Palette.Soft),
            }, 19.5f);
            Kit.Gap(10);
            var row = Kit.At;
            if (Kit.Button("##auto-stop", row, "Stop", Btn.Danger, BtnSize.Sm, Icon.Square, 0, false, "Stops after the step at work"))
                Later(() => OutfitStorer.Stop());
            Kit.Reserve(row, w, Kit.ButtonHeight(BtnSize.Sm));
            Kit.EndPanel();
            return;
        }
        var plan = AutoRoomNow(me);
        var have = OutfitStorer.Supplies(data);
        var blocked = AutoRoomBlocked(plan, have);
        var label = plan.Jobs.Count > 0 ? $"Store {Words.Count(plan.Jobs.Count, "set")} for me" : "Store the sets for me";
        var bw = Kit.ButtonWidth(label, BtnSize.Md, Icon.Package, Btn.Primary);
        var textW = w - bw - S(16);
        var head = plan.Jobs.Count > 0 ? $"Make room for me: frees {Words.Count(plan.Frees, "place")}" : "Make room for me";
        Txt.Draw(Kit.Dl, at, Txt.Fit(head, Face.SansSemibold, 14, textW), Face.SansSemibold, 14, Palette.Ink);
        var line = blocked ?? AutoRoomWords(plan, have);
        var lh = Txt.Flow(new Vector2(at.X, at.Y + Txt.Natural(Face.SansSemibold, 14) + S(4)), textW, new[] { new Run(line, Face.Sans, 12, blocked != null ? Palette.RoseText : Palette.Soft) }, 18, out _);
        var blockH = Txt.Natural(Face.SansSemibold, 14) + S(4) + lh;
        if (Kit.Button("##auto-room-go", new Vector2(at.X + w - bw, at.Y + MathF.Max(0, (blockH - Kit.ButtonHeight(BtnSize.Md)) / 2)), label, Btn.Primary, BtnSize.Md, Icon.Package, 0,
                blocked != null, blocked ?? "Asks first. New: not yet watched in the game"))
        {
            var words = AutoRoomWords(plan, have);
            Ask("Store the sets for you?", words + " Keep the dresser open while it works; it stops at the first thing that doesn't go.", "Store them", "Not now",
                () => StartAutoRoom(plan));
        }
        Kit.Reserve(at, w, MathF.Max(blockH, Kit.ButtonHeight(BtnSize.Md)));
        if (autoRoomLast is { } last)
        {
            Kit.Gap(8);
            Txt.Para(new[] { new Run(last.Words, Face.Sans, 12, last.Ok ? Palette.Success : Palette.RoseText) }, 18);
        }
        Kit.EndPanel();
    }

    /// <summary>In the dresser's panel: the same, smaller; pressed once it asks to be pressed again.</summary>
    private void PaneAutoRoom(CharacterWardrobe me)
    {
        if (OutfitStorer.Busy)
        {
            PaneNote($"Storing sets for you: {OutfitStorer.SetsDone} done, {OutfitStorer.SetsLeft} to go. Keep the dresser open.", Palette.RoseText);
            Kit.Gap(6);
            var srow = Kit.At;
            if (Kit.Button("##c-auto-stop", srow, "Stop", Btn.Danger, BtnSize.Sm, Icon.Square)) Later(() => OutfitStorer.Stop());
            Kit.Reserve(srow, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
            return;
        }
        var plan = AutoRoomNow(me);
        if (plan.Jobs.Count == 0)
        {
            if (autoRoomLast is { } done) PaneNote(done.Words, done.Ok ? Palette.Success : Palette.RoseText);
            return;
        }
        var have = OutfitStorer.Supplies(data);
        var blocked = AutoRoomBlocked(plan, have);
        var armed = DateTime.UtcNow < autoRoomArmedUntil;
        PaneNote(blocked ?? $"{Words.Count(plan.Jobs.Count, "set")} could gather {Words.Count(plan.Pieces, "loose piece")}: frees {Words.Count(plan.Frees, "place")}, for {Words.Count(plan.Pieces, "glamour prism")} (you have {have.Prisms}).",
            blocked != null ? Palette.RoseText : null);
        Kit.Gap(6);
        var row = Kit.At;
        var label = armed ? "Press again to store them" : $"Store {Words.Count(plan.Jobs.Count, "set")} for me";
        if (Kit.Button("##c-auto-room", row, label, armed ? Btn.Primary : Btn.Glass, BtnSize.Sm, Icon.Package, 0, blocked != null, blocked ?? AutoRoomWords(plan, have)))
            Later(() =>
            {
                if (DateTime.UtcNow < autoRoomArmedUntil)
                {
                    autoRoomArmedUntil = DateTime.MinValue;
                    StartAutoRoom(plan);
                }
                else autoRoomArmedUntil = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            });
        Kit.Reserve(row, Kit.Width, Kit.ButtonHeight(BtnSize.Sm));
        if (autoRoomLast is { } last)
        {
            Kit.Gap(6);
            PaneNote(last.Words, last.Ok ? Palette.Success : Palette.RoseText);
        }
    }
}

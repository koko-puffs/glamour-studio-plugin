using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using GlamourStudio.Core;

namespace GlamourStudio.Game;

/// <summary>
/// Puts a look's facewear and fashion accessory on, after Wear's gearset change: plates hold neither,
/// so the game's own commands do it, <c>/facewear "Silver Oval Spectacles"</c> and
/// <c>/fashion "Parasol"</c>, with the names from the client's own sheets (its language). The gearset
/// change goes first (it puts on the gearset's own facewear); two seconds later the facewear, half a
/// second after it the accessory, each only when it isn't on already (the same accessory again would
/// put it away) and when it's unlocked on this character. Three seconds after, what you wear is read
/// to see whether each went on: the game refuses an accessory in some places (and says so in chat).
/// Framework thread (<see cref="Tick"/>).
/// </summary>
public static unsafe class FaceAndFashion
{
    private enum Step
    {
        Wait,
        Face,
        Fashion,
        Check,
    }

    private sealed class Job
    {
        public required GameData Data { get; init; }

        public required Extras Want { get; init; }

        public required Action<string?> Done { get; init; }

        public Step Step { get; set; }

        public DateTime At { get; set; }

        /// <summary>When it gives up waiting for the game to be free.</summary>
        public DateTime Until { get; init; }

        public bool AskedFace { get; set; }

        public bool AskedFashion { get; set; }

        public List<string> Locked { get; } = new();
    }

    private static Job? job;

    /// <summary>Whether it's putting them on now.</summary>
    public static bool Busy => job != null;

    /// <summary>
    /// Puts a look's facewear and fashion accessory on once the gearset change has gone through.
    /// <paramref name="done"/> hears null when all went on (or nothing needed to), else a line saying
    /// what didn't and why.
    /// </summary>
    public static void PutOn(GameData data, Extras extras, Action<string?> done)
    {
        extras = extras.Clean;
        if (!extras.Any) return;
        job = new Job { Data = data, Want = extras, Done = done, Step = Step.Wait, At = DateTime.UtcNow + TimeSpan.FromSeconds(2), Until = DateTime.UtcNow + TimeSpan.FromSeconds(25) };
        Recorder.Log($"WEAR extras: facewear {extras.Face} colour {extras.Colour}, fashion accessory {extras.Fashion}, after the gearset change");
    }

    public static void Stop() => job = null;

    public static void Tick()
    {
        if (job is not { } j || DateTime.UtcNow < j.At) return;
        try
        {
            switch (j.Step)
            {
                case Step.Wait:
                    // (Not while the game's still busy with the change: arriving somewhere, casting…)
                    if (Gearsets.Blocked(false) is { } why)
                    {
                        if (DateTime.UtcNow > j.Until) Finish(j, $"Your facewear and fashion accessory weren't put on: {Lower(why)}");
                        else j.At = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                        return;
                    }
                    j.Step = Step.Face;
                    break;
                case Step.Face:
                    j.AskedFace = AskFace(j);
                    j.Step = Step.Fashion;
                    j.At = DateTime.UtcNow + TimeSpan.FromSeconds(j.AskedFace ? 0.5 : 0);
                    break;
                case Step.Fashion:
                    j.AskedFashion = AskFashion(j);
                    j.Step = Step.Check;
                    j.At = DateTime.UtcNow + TimeSpan.FromSeconds(j.AskedFace || j.AskedFashion ? 3 : 0);
                    break;
                default:
                    Check(j);
                    break;
            }
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamourie couldn't put on the look's facewear or fashion accessory");
            Finish(j, "The game wouldn't put on the look's facewear or fashion accessory");
        }
    }

    private static bool AskFace(Job j)
    {
        var known = j.Data.Collections;
        if (!j.Want.HasFace || known.Style(j.Want.Face) is not { } style) return false;
        var name = known.FaceName(j.Want.Face, j.Want.Colour) ?? style.Name;
        if (Worn().Glasses == known.GlassesOf(j.Want.Face, j.Want.Colour)) return false;
        var ps = PlayerState.Instance();
        if (ps != null && ps->IsLoaded && !ps->IsGlassesStyleUnlocked((ushort)style.Style))
        {
            j.Locked.Add(name);
            return false;
        }
        return Send($"/facewear \"{Quoted(name)}\"");
    }

    private static bool AskFashion(Job j)
    {
        var known = j.Data.Collections;
        if (!j.Want.HasFashion || known.Accessory(j.Want.Fashion) is not { } piece) return false;
        if (Worn().Ornament == piece.Ornament) return false;
        var ps = PlayerState.Instance();
        if (ps != null && ps->IsLoaded && !ps->IsOrnamentUnlocked(piece.Ornament))
        {
            j.Locked.Add(piece.Name);
            return false;
        }
        return Send($"/fashion \"{Quoted(piece.Name)}\"");
    }

    private static void Check(Job j)
    {
        var known = j.Data.Collections;
        var now = Worn();
        var refused = new List<string>();
        if (j.AskedFace && now.Glasses != known.GlassesOf(j.Want.Face, j.Want.Colour))
            refused.Add(known.FaceName(j.Want.Face, j.Want.Colour) ?? "the facewear");
        if (j.AskedFashion && known.Accessory(j.Want.Fashion) is { } piece && now.Ornament != piece.Ornament)
            refused.Add(piece.Name);
        Finish(j, Words.ExtrasOutcome(refused, j.Locked));
    }

    private static void Finish(Job j, string? said)
    {
        if (job == j) job = null;
        Recorder.Log($"WEAR extras: {said ?? "all on"}");
        j.Done(said);
    }

    /// <summary>The Glasses row and Ornament row you wear now (0: none).</summary>
    private static (uint Glasses, uint Ornament) Worn()
    {
        if (Services.Objects.LocalPlayer is not { } player || player.Address == nint.Zero) return (0, 0);
        var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)player.Address;
        return (chara->DrawData.GlassesIds[0], chara->OrnamentData.OrnamentId);
    }

    /// <summary>A command typed into the chat box; false when the game isn't ready.</summary>
    private static bool Send(string command)
    {
        var ui = UIModule.Instance();
        if (ui == null) return false;
        var text = Utf8String.FromString(command);
        try
        {
            Recorder.Log($"WEAR {command}");
            ui->ProcessChatBoxEntry(text, nint.Zero, false);
            return true;
        }
        finally
        {
            text->Dtor(true);
        }
    }

    /// <summary>A name as the command takes it between its quotes.</summary>
    private static string Quoted(string name) => name.Replace("\"", string.Empty).Trim();

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

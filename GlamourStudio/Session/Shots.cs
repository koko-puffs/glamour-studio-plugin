using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.UI;
using GlamourStudio.Core;

namespace GlamourStudio.Session;

/// <summary>A shot taken for a look: its picture (a JPEG), size, a preview, and how sending went.</summary>
public sealed class Shot
{
    public required string LookId { get; init; }

    public required string LookName { get; init; }

    public byte[]? Jpeg { get; set; }

    public int Width { get; init; }

    public int Height { get; init; }

    public IDalamudTextureWrap? Thumb { get; set; }

    public bool Sending { get; set; }

    public bool Sent { get; set; }

    /// <summary>What happened last (sent, or why not), and whether it's bad news.</summary>
    public (string Text, bool Bad)? Note { get; set; }
}

/// <summary>
/// Shots from the game for a look's Worn in game, as Housing Studio's plugin takes them of a home:
/// the HUD is hidden (as Scroll Lock does) for a few frames, the game's next frame is grabbed before
/// ImGui draws, the HUD shown again, then the picture is made at most 2560 on its long side (1920 if
/// that's still too big) as a JPEG under the shots bucket's 3 MB, with a preview, off the framework
/// thread. Sent to the edge function <c>gs-plugin-shot</c> with the token. Framework thread, but for
/// the making and the sending.
/// </summary>
public sealed class Shots : IDisposable
{
    /// <summary>WIC's JPEG container (GUID_ContainerFormatJpeg), if the encoders' list doesn't name it by its MIME type.</summary>
    private static readonly Guid JpegContainer = new("19e4a5aa-5662-4fc5-a0c0-1758028e1057");

    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM.</summary>
    private const int Bgra = 87;

    private static readonly int[] LongEdges = { 2560, 1920 };

    private static readonly float[] Qualities = { 0.9f, 0.84f, 0.78f, 0.72f, 0.65f, 0.58f, 0.5f };

    private readonly HttpClient http;
    private readonly Func<string> token;
    private readonly CancellationTokenSource stop = new();
    private readonly object textures = new();
    private readonly List<IDalamudTextureWrap> owned = new();
    private bool disposed;

    private (string LookId, string LookName, DateTime At)? hudWait;
    private int hudWaitFrames;
    private bool hidHud;
    private Task<IDalamudTextureWrap>? grab;
    private (string LookId, string LookName, int Generation)? grabFor;

    /// <summary>The HUD was showing when the frame was grabbed.</summary>
    private bool hudInShot;
    private int generation;
    private Task? making;

    /// <summary>When the frame was asked for: one that never comes is given up on (and the HUD shown again).</summary>
    private DateTime grabAt;

    public Shots(HttpClient http, Func<string> token)
    {
        this.http = http;
        this.token = token;
    }

    /// <summary>
    /// The key of the character wearing it, when the site takes that character's data (else null): said to
    /// the site after the shot, which shows it on the shot only while you show that character on your shots.
    /// </summary>
    public Func<string?>? WhoWore { get; set; }

    /// <summary>The shot taken, until it's sent and closed, or discarded.</summary>
    public Shot? Taken { get; private set; }

    /// <summary>A shot is being taken (the HUD hidden, the frame grabbed, the picture made).</summary>
    public bool Capturing { get; private set; }

    /// <summary>Why the last shot didn't come out, or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>Takes a shot for a look (framework thread); false, with why, when it can't.</summary>
    public bool Take(string lookId, string lookName, out string why)
    {
        why = string.Empty;
        if (Capturing)
        {
            why = "A shot is being taken.";
            return false;
        }
        if (Taken is { Sending: true })
        {
            why = "The last shot is still being sent.";
            return false;
        }
        Discard();
        Problem = null;
        Capturing = true;
        Game.Recorder.Log($"SHOT take for {lookName}: hud {(Services.GameGui.GameUiHidden ? "already hidden" : "shown, hiding it")}, gpose {Services.ClientState.IsGPosing}");
        if (!Services.GameGui.GameUiHidden && SetHud(false))
        {
            hidHud = true;
            hudWait = (lookId, lookName, DateTime.UtcNow);
            hudWaitFrames = 0;
            return true;
        }
        return Grab(lookId, lookName, out why);
    }

    private bool Grab(string lookId, string lookName, out string why)
    {
        why = string.Empty;
        try
        {
            grab = Services.Textures.CreateFromImGuiViewportAsync(
                new ImGuiViewportTextureArgs { ViewportId = ImGui.GetMainViewport().ID, TakeBeforeImGuiRender = true },
                "Glamour Studio shot", stop.Token);
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Glamour Studio couldn't grab the game's picture");
            why = "Couldn't take the picture (see /xllog).";
            Failed(why);
            return false;
        }
        grabFor = (lookId, lookName, ++generation);
        grabAt = DateTime.UtcNow;
        // The HUD back before the frame (Scroll Lock pressed meanwhile, as a recording saw): the shot says so.
        hudInShot = !Services.GameGui.GameUiHidden && !Services.ClientState.IsGPosing;
        Game.Recorder.Log($"SHOT grabbing the frame (hud hidden {Services.GameGui.GameUiHidden})");
        return true;
    }

    /// <summary>Framework thread, every frame: the HUD's wait, then the grabbed frame made into a picture.</summary>
    public void Tick()
    {
        if (hudWait is { } w)
        {
            hudWaitFrames++;
            var waited = DateTime.UtcNow - w.At;
            if ((hudWaitFrames < 3 || waited < TimeSpan.FromMilliseconds(150) || !Services.GameGui.GameUiHidden) && waited < TimeSpan.FromSeconds(1)) return;
            hudWait = null;
            if (!Grab(w.LookId, w.LookName, out _)) ShowHudAgain();
            return;
        }
        if (grab is { IsCompleted: false } late && DateTime.UtcNow - grabAt > TimeSpan.FromSeconds(5))
        {
            // Should the frame come after all, it's let go of then.
            late.ContinueWith(x =>
            {
                if (x.IsCompletedSuccessfully) x.Result.Dispose();
            }, TaskScheduler.Default);
            grab = null;
            grabFor = null;
            generation++;
            ShowHudAgain();
            Failed("The game's picture never came. Try again.");
            return;
        }
        if (grab is not { IsCompleted: true } t || grabFor is not { } f) return;
        grab = null;
        grabFor = null;
        ShowHudAgain();
        if (!t.IsCompletedSuccessfully)
        {
            if (t.Exception != null) Services.Log.Error(t.Exception, "Glamour Studio couldn't grab the game's picture");
            Failed("Couldn't take the picture (see /xllog).");
            return;
        }
        var wrap = Own(t.Result);
        // (The token taken here, on the framework thread: after Dispose the source can't be asked.)
        var cancel = stop.Token;
        making = Task.Run(async () =>
        {
            var made = await Make(wrap, cancel).ConfigureAwait(false);
            _ = Services.Framework.RunOnFrameworkThread(() => Made(made, f.LookId, f.LookName, f.Generation));
        });
    }

    private sealed record MadeShot(byte[]? Jpeg, int Width, int Height, IDalamudTextureWrap? Thumb, string? Why);

    private void Made(MadeShot m, string lookId, string lookName, int gen)
    {
        if (gen != generation || disposed)
        {
            Release(m.Thumb);
            return;
        }
        Capturing = false;
        if (m.Jpeg == null || m.Why != null)
        {
            Release(m.Thumb);
            Failed(m.Why ?? "Couldn't make the picture.");
            return;
        }
        Taken = new Shot
        {
            LookId = lookId, LookName = lookName, Jpeg = m.Jpeg, Width = m.Width, Height = m.Height, Thumb = m.Thumb,
            Note = hudInShot ? ("The HUD came back before the picture was taken: it's in the shot. Take another for a clean one.", true) : null,
        };
        Game.Recorder.Log($"SHOT made: {m.Width}×{m.Height}, {m.Jpeg.Length:N0} bytes, hud hidden now {Services.GameGui.GameUiHidden}");
        // With the HUD hidden (or in Group Pose) Dalamud hides the plugin's windows too.
        if (Services.GameGui.GameUiHidden || Services.ClientState.IsGPosing)
            Services.Chat.Print("Glamour Studio: shot taken. Show the HUD again (or leave Group Pose) to see it and send it.");
    }

    private void Failed(string why)
    {
        Game.Recorder.Log($"SHOT failed: {why}");
        Capturing = false;
        Problem = why;
        Services.Log.Warning($"Glamour Studio: no shot: {why}");
    }

    private static Guid? JpegCodec()
    {
        foreach (var codec in Services.TextureReadback.GetSupportedImageEncoderInfos())
        {
            if (codec.ContainerGuid == JpegContainer || codec.MimeTypes.Any(m => m.Trim().Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)))
                return codec.ContainerGuid;
        }
        return null;
    }

    /// <summary>
    /// On a worker: the grabbed frame made 2560 at most on its long side, written as a JPEG at the first
    /// quality that's small enough (1920 if none at 2560 is), and a preview. The frame and the sized
    /// picture are let go of here whatever happens; the preview is the shot's.
    /// </summary>
    private async Task<MadeShot> Make(IDalamudTextureWrap grabbed, CancellationToken cancel)
    {
        IDalamudTextureWrap? sized = null;
        try
        {
            if (JpegCodec() is not { } codec) return new MadeShot(null, 0, 0, null, "This PC can't write JPEG pictures.");
            var format = Services.Textures.IsDxgiFormatSupportedForCreateFromExistingTextureAsync(Bgra) ? Bgra : 0;
            byte[]? jpeg = null;
            (int Width, int Height)? tried = null;
            foreach (var edge in LongEdges)
            {
                if (Fit(grabbed.Width, grabbed.Height, edge) is not { } fit) return new MadeShot(null, 0, 0, null, "The game's picture is too small to send.");
                // (A screen 1920 or under: the second pass would be the first again.)
                if (tried == fit) break;
                tried = fit;
                Release(sized);
                sized = Own(await Services.Textures.CreateFromExistingTextureAsync(grabbed,
                    new TextureModificationArgs { NewWidth = fit.Width, NewHeight = fit.Height, MakeOpaque = true, DxgiFormat = format },
                    leaveWrapOpen: true, "Glamour Studio shot (sized)", cancel).ConfigureAwait(false));
                foreach (var q in Qualities)
                {
                    using var stream = new MemoryStream();
                    await Services.TextureReadback.SaveToStreamAsync(sized, codec, stream, new Dictionary<string, object> { ["ImageQuality"] = q },
                        leaveWrapOpen: true, leaveStreamOpen: true, cancel).ConfigureAwait(false);
                    if (stream.Length > SiteLink.ShotMaxBytes) continue;
                    jpeg = stream.ToArray();
                    break;
                }
                if (jpeg != null) break;
            }
            if (jpeg == null || sized == null) return new MadeShot(null, 0, 0, null, "The picture is too big to send, even smaller.");
            var scale = Math.Min(1.0, 480.0 / Math.Max(sized.Width, sized.Height));
            var thumb = Own(await Services.Textures.CreateFromExistingTextureAsync(sized,
                new TextureModificationArgs { NewWidth = Math.Max(1, (int)(sized.Width * scale)), NewHeight = Math.Max(1, (int)(sized.Height * scale)), DxgiFormat = format },
                leaveWrapOpen: true, "Glamour Studio shot (preview)", cancel).ConfigureAwait(false));
            return new MadeShot(jpeg, sized.Width, sized.Height, thumb, null);
        }
        catch (OperationCanceledException)
        {
            return new MadeShot(null, 0, 0, null, "Stopped.");
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Glamour Studio couldn't make the picture");
            return new MadeShot(null, 0, 0, null, "Couldn't make the picture (see /xllog).");
        }
        finally
        {
            Release(grabbed);
            Release(sized);
        }
    }

    /// <summary>A size with its longest side at most <paramref name="longEdge"/>, its shape kept; null when it's too small.</summary>
    public static (int Width, int Height)? Fit(int width, int height, int longEdge) => SiteLink.FitShot(width, height, longEdge);

    /// <summary>Sends the shot taken, with its caption; <paramref name="done"/> hears how it went (framework thread).</summary>
    public void Send(string caption, Action<SiteReply<ShotSent>> done)
    {
        if (Taken is not { Jpeg: { } jpeg, Sending: false, Sent: false } s) return;
        s.Sending = true;
        s.Note = null;
        var t = token();
        var who = WhoWore?.Invoke();
        var cancel = stop.Token;
        Task.Run(async () =>
        {
            var r = await SiteLink.ShotAsync(http, t, s.LookId, caption, jpeg, cancel).ConfigureAwait(false);
            // Which character wore it (nothing's said if this one doesn't go through: the shot's there either way).
            if (r.Ok && who != null) await SiteLink.ShotCharacterAsync(http, t, r.Value!.Id, who, cancel).ConfigureAwait(false);
            _ = Services.Framework.RunOnFrameworkThread(() =>
            {
                s.Sending = false;
                if (r.Ok)
                {
                    s.Sent = true;
                    s.Jpeg = null;
                    s.Note = ($"On “{r.Value!.LookName}”'s Worn in game.", false);
                }
                else s.Note = (r.Why ?? "The site said no.", true);
                done(r);
            });
        });
    }

    /// <summary>Lets the shot taken go.</summary>
    public void Discard()
    {
        if (Taken is { } s)
        {
            Release(s.Thumb);
            s.Thumb = null;
        }
        Taken = null;
    }

    private void ShowHudAgain()
    {
        hudWait = null;
        if (!hidHud) return;
        Game.Recorder.Log($"SHOT showing the HUD again (hidden now {Services.GameGui.GameUiHidden})");
        hidHud = false;
        if (Services.GameGui.GameUiHidden) SetHud(true);
    }

    private static unsafe bool SetHud(bool visible)
    {
        try
        {
            var ui = UIModule.Instance();
            if (ui == null) return false;
            var atk = ui->GetRaptureAtkModule();
            if (atk == null) return false;
            atk->SetUiVisibility(visible);
            Game.Recorder.Log($"SHOT SetUiVisibility({visible}) → hud hidden now {Services.GameGui.GameUiHidden}");
            return true;
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, $"Glamour Studio couldn't {(visible ? "show" : "hide")} the HUD");
            return false;
        }
    }

    private T Own<T>(T wrap) where T : IDalamudTextureWrap
    {
        lock (textures)
        {
            if (disposed)
            {
                wrap.Dispose();
                return wrap;
            }
            owned.Add(wrap);
            return wrap;
        }
    }

    private void Release(IDalamudTextureWrap? wrap)
    {
        if (wrap == null) return;
        lock (textures)
        {
            owned.Remove(wrap);
        }
        wrap.Dispose();
    }

    public void Dispose()
    {
        stop.Cancel();
        ShowHudAgain();
        lock (textures)
        {
            disposed = true;
            foreach (var w in owned) w.Dispose();
            owned.Clear();
        }
        Taken = null;
        // (The source isn't disposed: a worker still finishing may look at its token.)
    }
}

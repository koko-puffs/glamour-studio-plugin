using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;

namespace GlamourStudio.UI;

/// <summary>The window's own pictures (Resources/Textures, embedded in the DLL).</summary>
public enum Tex
{
    /// <summary>The logo (the clothes tag with its hanger, the site's favicon-glamour.svg) at 32px.</summary>
    Logo32,

    Logo64,

    Logo128,

    Logo256,

    /// <summary>The holo marble, a 256px tile that stands for 220 CSS px of it (the mock's <c>--holo</c>): loves, new, the tab you're on.</summary>
    Holo,

    /// <summary>The holo four-point sparkle, 48px: something new, a wish come true, good news in a toast.</summary>
    Spark,

    /// <summary>The cloud (the site's motif), in white to be tinted, 240 × 120: behind an empty page.</summary>
    Cloud,
}

/// <summary>
/// The window's textures, each read from the DLL and made once, when the window's fonts are (on
/// the plugin's load), and let go with them: never per frame. Drawing asks <see cref="Get"/>, which
/// is null until the texture's made (the part then draws without it, or a flat stand-in).
/// </summary>
public sealed class Textures : IDisposable
{
    /// <summary>The ones the Kit draws with (null before the plugin's fonts are made, and after they're gone).</summary>
    public static Textures? Current { get; internal set; }

    private static readonly string[] Files =
    {
        "logo-32.png", "logo-64.png", "logo-128.png", "logo-256.png", "holo.png", "spark.png", "cloud.png",
    };

    private readonly Task<IDalamudTextureWrap>?[] loads = new Task<IDalamudTextureWrap>?[Files.Length];
    private readonly CancellationTokenSource cancel = new();
    private bool disposed;

    public Textures()
    {
        var asm = typeof(Textures).Assembly;
        var names = asm.GetManifestResourceNames();
        for (var i = 0; i < Files.Length; i++)
        {
            var file = Files[i];
            var name = Array.Find(names, n => n.EndsWith("Textures." + file, StringComparison.Ordinal));
            if (name == null)
            {
                Services.Log.Warning("Glamour Studio is missing its texture {File}", file);
                continue;
            }
            try
            {
                using var s = asm.GetManifestResourceStream(name)!;
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                loads[i] = Services.Textures.CreateFromImageAsync(ms.ToArray(), $"GlamourStudio {file}", cancel.Token);
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, "Glamour Studio couldn't load its texture {File}", file);
            }
        }
    }

    /// <summary>The texture, once it's made (null until then, or if it couldn't be).</summary>
    public IDalamudTextureWrap? Get(Tex tex)
    {
        if (disposed) return null;
        var t = loads[(int)tex];
        return t is { IsCompletedSuccessfully: true } ? t.Result : null;
    }

    /// <summary>The logo texture to draw at <paramref name="screenPx"/>: the smallest that's at least that big (drawn smaller than half its size, a texture without mipmaps shimmers).</summary>
    public static Tex LogoFor(float screenPx) => screenPx switch
    {
        <= 32 => Tex.Logo32,
        <= 64 => Tex.Logo64,
        <= 128 => Tex.Logo128,
        _ => Tex.Logo256,
    };

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        cancel.Cancel();
        foreach (var t in loads)
        {
            if (t == null) continue;
            if (t.IsCompletedSuccessfully) t.Result.Dispose();
            // Still being made: let go of it once it is.
            else if (!t.IsCompleted) t.ContinueWith(done => { if (done.IsCompletedSuccessfully) done.Result.Dispose(); }, TaskScheduler.Default);
        }
        cancel.Dispose();
    }
}

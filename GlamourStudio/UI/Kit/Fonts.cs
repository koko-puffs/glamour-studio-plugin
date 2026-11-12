using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dalamud.Game;
using Dalamud.Interface.ManagedFontAtlas;
using GlamourStudio.UI.Model;

namespace GlamourStudio.UI;

/// <summary>Glamour Studio's faces, as the site uses them since its "Character Sheet" redesign.</summary>
public enum Face
{
    /// <summary>Figtree 400: what you read.</summary>
    Sans,

    /// <summary>Figtree 500: names in lists, the small lines under them, toasts.</summary>
    SansMedium,

    /// <summary>Figtree 600 (and the mock's 650): buttons, places, eyebrows and tags (in capitals).</summary>
    SansSemibold,

    /// <summary>Figtree 700.</summary>
    SansBold,

    /// <summary>Figtree 800: the sticker's words.</summary>
    SansExtraBold,

    /// <summary>
    /// Bricolage Grotesque 800, drawn in capitals (<see cref="Kit.Display"/> does that): what a place
    /// or a look is (titles, section headings, a look's name, the wordmark, big numbers). Outfit 700
    /// until the redesign.
    /// </summary>
    Display,

    /// <summary>The same as <see cref="Display"/> now (it was Outfit 600 for a card's heading); kept so the screens still build.</summary>
    Heading,

    /// <summary>Shantell Sans 560: the hand-lettered voice that says one thing to you ("you press Save in the game").</summary>
    Hand,

    /// <summary>Bricolage 800 at 72 and 104 with only capitals, digits and a few signs (<see cref="EchoText.Glyphs"/>): the echo.</summary>
    Echo,

    /// <summary>Geist Mono 400: numbers that change, lined up (counts, a token).</summary>
    Mono,

    /// <summary>Geist Mono 500: a number that leads.</summary>
    MonoMedium,

    /// <summary>
    /// Noto Sans CJK Medium (Dalamud's own), at 11 13 15 18 24 34, holding only the characters the
    /// window's faces haven't got and that it has been asked to draw (kana, kanji, hangul, …): Txt
    /// draws such characters in it by itself. Not for the screens to ask for.
    /// </summary>
    Fallback,
}

/// <summary>
/// The faces at the sizes the window uses, loaded through Dalamud's font atlas (so they follow
/// Dalamud's global scale and rebuild with it). The mock-up's type sheet (docs/redesign, "Type ·
/// four faces, fixed sizes"): Bricolage Grotesque 800 at 13 15 17 20 26 34 44 60 (and the sizes
/// between that the screens ask for), the echo at 72 and 104; Shantell Sans 560 at 13 15 18;
/// Figtree 400/500/600/700 at 10 to 15 and 800 at 13 to 15; Geist Mono 400/500 at 10 to 13. Under
/// the SIL Open Font License 1.1, with their licences (Resources/Fonts); each a static instance of
/// the variable font made with fontTools' instancer from Google Fonts' files (Bricolage at its
/// 96pt optical size and full width, as the site's variable font is fixed; Shantell's other axes at
/// their defaults), cut to Latin with their hinting and layout tables left out (ImGui uses neither).
///
/// Sizes are CSS sizes (the em). ImGui sizes a font by its line (ascent to descent), so each face is
/// loaded at its em times its line ratio, and 14px Figtree here is as tall as 14px in the browser.
/// The mock's half sizes (13.5, 14.5) are drawn at the nearest whole size loaded.
///
/// On a Japanese client the sans sizes are Dalamud's own font (it has the kana and kanji that item
/// names need); the other faces keep the design's (with Dalamud's language's glyphs merged in up to
/// 30px: past that, a CJK atlas at every big size would be many megabytes; the fallback draws them).
///
/// No name ever shows ImGui's "?": each face's atlas holds Latin-1 and the common punctuation
/// (<see cref="Glyphs.Base"/>); a character a face hasn't got is drawn by <see cref="Txt"/> in
/// <see cref="Face.Fallback"/> (Noto Sans CJK) at the nearest of its sizes, scaled to the size
/// asked for; one neither has yet is left out for a moment and asked for (<see cref="Want"/>): the
/// atlas is built again with it, at most once a second, the Latin ones (Ł, ő, ș) in the faces too.
/// The atlas grows only by what's shown: about 4,000 to 5,500 px² a CJK character at 100% scale
/// (its six sizes; four times that at 200%), so 500 different kanji are 2 to 2.7 million px. Merging
/// Japanese's common set (about 3,200 glyphs) into the big display sizes instead would be about 21
/// million px for 44 and 60 alone.
/// </summary>
public sealed class Fonts : IDisposable
{
    /// <summary>(hhea ascent − descent) / em, read from the font files.</summary>
    private const float FigtreeLine = 1.2f;

    private const float BricolageLine = 1.2f;

    private const float ShantellLine = 1.34f;

    private const float GeistMonoLine = 1.3f;

    /// <summary>Noto Sans CJK's (ascent − descent) / em.</summary>
    private const float NotoLine = 1.448f;

    /// <summary>The fallback's sizes (CSS px, ascending): the window's text sizes, and 34 for titles (bigger ones are drawn from it, scaled).</summary>
    public static readonly int[] FallbackSizes = { 11, 13, 15, 18, 24, 34 };

    /// <summary>The most characters ever asked for (a list of ever-new names mustn't grow the atlas for ever).</summary>
    private const int MostWanted = 6000;

    /// <summary>The biggest size that has Dalamud's language's glyphs merged in.</summary>
    private const int ExtraGlyphsUpTo = 30;

    private static readonly (Face Face, int Px)[] Wanted =
    {
        (Face.Sans, 15), (Face.Sans, 14), (Face.Sans, 13), (Face.Sans, 12), (Face.Sans, 11), (Face.Sans, 10),
        (Face.SansMedium, 15), (Face.SansMedium, 14), (Face.SansMedium, 13), (Face.SansMedium, 12), (Face.SansMedium, 11),
        (Face.SansSemibold, 15), (Face.SansSemibold, 14), (Face.SansSemibold, 13), (Face.SansSemibold, 12),
        (Face.SansSemibold, 11), (Face.SansSemibold, 10),
        (Face.SansBold, 14), (Face.SansBold, 13),
        (Face.SansExtraBold, 15), (Face.SansExtraBold, 14), (Face.SansExtraBold, 13),
        (Face.Display, 60), (Face.Display, 44), (Face.Display, 40), (Face.Display, 34), (Face.Display, 30), (Face.Display, 28),
        (Face.Display, 26), (Face.Display, 24), (Face.Display, 22), (Face.Display, 20), (Face.Display, 19), (Face.Display, 17),
        (Face.Display, 16), (Face.Display, 15), (Face.Display, 14), (Face.Display, 13), (Face.Display, 12), (Face.Display, 11),
        (Face.Display, 10),
        (Face.Echo, 104), (Face.Echo, 72),
        (Face.Hand, 18), (Face.Hand, 15), (Face.Hand, 13),
        (Face.Mono, 13), (Face.Mono, 12), (Face.Mono, 11), (Face.Mono, 10),
        (Face.MonoMedium, 13), (Face.MonoMedium, 12), (Face.MonoMedium, 11), (Face.MonoMedium, 10),
        (Face.Fallback, 11), (Face.Fallback, 13), (Face.Fallback, 15), (Face.Fallback, 18), (Face.Fallback, 24), (Face.Fallback, 34),
    };

    // What the faces' atlases hold besides Latin-1, as the builds read it (on Dalamud's build thread:
    // swapped whole, never changed in place).
    private static volatile ushort[] faceRanges = Glyphs.Ranges(Array.Empty<char>());
    private static volatile ushort[] fallbackRanges = Glyphs.Ranges(new[] { ' ' }, withBase: false);

    /// <summary>Every character asked for so far, and whether some came since the last build.</summary>
    private readonly HashSet<char> wanted = new();

    private bool pending;
    private DateTime lastBuild = DateTime.MinValue;
    private readonly IFontAtlas atlas;

    private readonly Dictionary<(Face, int), IFontHandle> handles = new();

    /// <summary>
    /// A size that isn't loaded, mapped once to the nearest loaded one (looking it up anew sorted the
    /// loaded sizes on every call). These are the same handles as above, so only those are disposed.
    /// </summary>
    private readonly Dictionary<(Face, int), IFontHandle> aliases = new();

    /// <summary>The window's textures (the logo, holo, the sparkle, the cloud) live and go with its fonts: Plugin makes and disposes this.</summary>
    private readonly Textures textures;

    private int generation;

    /// <summary>
    /// Goes up whenever any face is rebuilt (Dalamud's scale or font settings changed, or a face has
    /// just finished loading), so what was measured with the old glyphs is measured again.
    /// </summary>
    public int Generation => Volatile.Read(ref generation);

    public Fonts()
    {
        atlas = Services.PluginInterface.UiBuilder.FontAtlas;
        var japanese = Services.ClientState.ClientLanguage == ClientLanguage.Japanese;
        foreach (var (face, px) in Wanted)
        {
            var file = face switch
            {
                Face.Sans => "Figtree-Regular.ttf",
                Face.SansMedium => "Figtree-Medium.ttf",
                Face.SansSemibold => "Figtree-SemiBold.ttf",
                Face.SansBold => "Figtree-Bold.ttf",
                Face.SansExtraBold => "Figtree-ExtraBold.ttf",
                Face.Display or Face.Heading or Face.Echo => "BricolageGrotesque-ExtraBold.ttf",
                Face.Hand => "ShantellSans-560.ttf",
                Face.Fallback => string.Empty,
                Face.MonoMedium => "GeistMono-Medium.ttf",
                _ => "GeistMono-Regular.ttf",
            };
            var line = face switch
            {
                Face.Display or Face.Heading or Face.Echo => BricolageLine,
                Face.Hand => ShantellLine,
                Face.Fallback => NotoLine,
                Face.Mono or Face.MonoMedium => GeistMonoLine,
                _ => FigtreeLine,
            };
            var dalamudFont = japanese && face is Face.Sans or Face.SansMedium or Face.SansSemibold or Face.SansBold or Face.SansExtraBold;
            var handle = face == Face.Fallback
                ? MakeFallback(atlas, px * line)
                : Make(atlas, file, px * line, dalamudFont, face == Face.Echo, px <= ExtraGlyphsUpTo);
            // Dalamud may say so from another thread.
            handle.ImFontChanged += (_, _) => Interlocked.Increment(ref generation);
            handles[(face, px)] = handle;
        }
        textures = new Textures();
        Textures.Current = textures;
    }

    /// <summary>Pushes a face at a CSS size (the nearest loaded size when that one isn't).</summary>
    public IDisposable Push(Face face, int px) => Handle(face, px).Push();

    public IFontHandle Handle(Face face, int px)
    {
        if (handles.TryGetValue((face, px), out var h) || aliases.TryGetValue((face, px), out h)) return h;
        // Heading is Display now (one face, Bricolage 800).
        var real = face == Face.Heading ? Face.Display : face;
        var best = handles.Keys.Where(k => k.Item1 == real).OrderBy(k => Math.Abs(k.Item2 - px)).FirstOrDefault();
        h = handles.TryGetValue(best, out var near) ? near : handles[(Face.Sans, 14)];
        aliases[(face, px)] = h;
        return h;
    }

    /// <summary>The echo's glyphs as ImGui's ranges (each character a range of one; the list ends with 0).</summary>
    private static readonly ushort[] EchoRanges = EchoText.Glyphs.Distinct().OrderBy(c => c)
        .SelectMany(c => new[] { (ushort)c, (ushort)c }).Append((ushort)0).ToArray();

    private static IFontHandle Make(IFontAtlas atlas, string file, float sizePx, bool dalamudFont, bool echo, bool extraGlyphs) =>
        atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            if (echo)
            {
                // Only what the echo draws: at 104px every glyph is a big square of the atlas.
                tk.AddFontFromMemory(Read(file), new SafeFontConfig { SizePx = sizePx, GlyphRanges = EchoRanges }, file);
                return;
            }
            var font = dalamudFont
                ? tk.AddDalamudDefaultFont(sizePx, null)
                : tk.AddFontFromMemory(Read(file), new SafeFontConfig { SizePx = sizePx, GlyphRanges = faceRanges }, file);
            // The game's own symbols (the HQ mark in item names) and whatever Dalamud's language needs.
            tk.AddGameSymbol(new SafeFontConfig { SizePx = sizePx, MergeFont = font });
            if (extraGlyphs || dalamudFont)
                tk.AttachExtraGlyphsForDalamudLanguage(new SafeFontConfig { SizePx = sizePx, MergeFont = font });
        }));

    /// <summary>The fallback at a size: Noto Sans CJK with only the characters asked for (and the space).</summary>
    private static IFontHandle MakeFallback(IFontAtlas atlas, float sizePx) =>
        atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
            tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansCjkMedium, new SafeFontConfig { SizePx = sizePx, GlyphRanges = fallbackRanges })));

    /// <summary>The fallback's handle for drawing at <paramref name="px"/>, and the size it was made at (CSS px).</summary>
    public (IFontHandle Handle, int Px) Fallback(int px)
    {
        var at = Glyphs.Nearest(px, FallbackSizes);
        return (handles[(Face.Fallback, at)], at);
    }

    /// <summary>Asks for characters the faces and the fallback haven't got: they're in the next build (see <see cref="Tick"/>).</summary>
    public void Want(IEnumerable<char> chars)
    {
        foreach (var c in chars)
        {
            if (wanted.Count >= MostWanted) return;
            if (Glyphs.Wantable(c) && wanted.Add(c)) pending = true;
        }
    }

    /// <summary>
    /// Once a frame or so (Txt calls it): builds the atlas again with what's been asked for, at most
    /// once a second and never while it's building. The faces' "Generation" goes up when it's done,
    /// and the text measured without the new glyphs is measured again.
    /// </summary>
    public void Tick()
    {
        if (!pending) return;
        var now = DateTime.UtcNow;
        if (now - lastBuild < TimeSpan.FromSeconds(1)) return;
        if (atlas.BuildTask is { IsCompleted: false }) return;
        pending = false;
        lastBuild = now;
        var latin = new List<char>();
        foreach (var c in wanted)
            if (Glyphs.IsLatinish(c)) latin.Add(c);
        faceRanges = Glyphs.Ranges(latin);
        var all = new List<char>(wanted) { ' ' };
        fallbackRanges = Glyphs.Ranges(all, withBase: false);
        try
        {
            if (atlas.AutoRebuildMode == FontAtlasAutoRebuildMode.OnNewFrame) atlas.BuildFontsOnNextFrame();
            else _ = atlas.BuildFontsAsync();
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamour Studio couldn't build its fonts again for new characters");
        }
    }

    private static readonly Dictionary<string, byte[]> Files = new();

    private static byte[] Read(string file)
    {
        lock (Files)
        {
            if (Files.TryGetValue(file, out var cached)) return cached;
            var asm = typeof(Fonts).Assembly;
            var name = asm.GetManifestResourceNames().First(n => n.EndsWith(file, StringComparison.Ordinal));
            using var s = asm.GetManifestResourceStream(name)!;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return Files[file] = ms.ToArray();
        }
    }

    public void Dispose()
    {
        if (ReferenceEquals(Textures.Current, textures)) Textures.Current = null;
        textures.Dispose();
        foreach (var h in handles.Values) h.Dispose();
        handles.Clear();
        aliases.Clear();
    }
}

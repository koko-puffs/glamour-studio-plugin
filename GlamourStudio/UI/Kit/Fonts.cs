using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dalamud.Game;
using Dalamud.Interface.ManagedFontAtlas;

namespace GlamourStudio.UI;

/// <summary>Glamour Studio's faces, as the site uses them since its "Moonlit Atelier" redesign.</summary>
public enum Face
{
    /// <summary>Figtree 400: what you read.</summary>
    Sans,

    /// <summary>Figtree 500: names in lists, the small lines under them.</summary>
    SansMedium,

    /// <summary>Figtree 600: buttons, labels, eyebrows (in capitals).</summary>
    SansSemibold,

    /// <summary>Figtree 700: the violet pill's words.</summary>
    SansBold,

    /// <summary>Outfit 700, in normal capitals: what's big (the window's title, a look's name, big numbers).</summary>
    Display,

    /// <summary>Outfit 600: a card's or a pane's heading (the site's <c>.gs-h3</c>).</summary>
    Heading,

    /// <summary>Geist Mono 400: numbers that change, lined up (counts, a token).</summary>
    Mono,

    /// <summary>Geist Mono 500: a number that leads.</summary>
    MonoMedium,
}

/// <summary>
/// The faces at the sizes the window uses, loaded through Dalamud's font atlas (so they follow
/// Dalamud's global scale and rebuild with it): Figtree 15/14/13/12/11/10 (400, 500, 600) and 14/13
/// (700); Outfit 30 to 13 (700) and 20 to 13 (600); Geist Mono 13/12/11/10 (400) and 12/11 (500).
/// The site's faces since its "Moonlit Atelier" redesign (November 2026; until 0.6.16 Syne 800 in
/// capitals, DM Sans, and DM Mono for labels in spaced capitals too: now the mono is for numbers only,
/// and labels and eyebrows are Figtree). Under the SIL Open Font License 1.1, with their licences
/// (Resources/Fonts); each the variable font's static instance at that weight, made with fontTools'
/// instancer from Google Fonts' files (nothing else changed).
///
/// Sizes are CSS sizes (the em). ImGui sizes a font by its line (ascent to descent), so each face is
/// loaded at its em times its line ratio, and 14px Figtree here is as tall as 14px in the browser.
///
/// On a Japanese client the sans sizes are Dalamud's own font (it has the kana and kanji that item
/// names need); Outfit and the mono keep the design's face (with Dalamud's language's glyphs merged in).
/// </summary>
public sealed class Fonts : IDisposable
{
    /// <summary>(hhea ascent − descent) / em, read from the font files: Figtree, Outfit, Geist Mono.</summary>
    private const float FigtreeLine = 1.2f;

    private const float OutfitLine = 1.26f;

    private const float GeistMonoLine = 1.3f;

    private static readonly (Face Face, int Px)[] Wanted =
    {
        (Face.Sans, 15), (Face.Sans, 14), (Face.Sans, 13), (Face.Sans, 12), (Face.Sans, 11), (Face.Sans, 10),
        (Face.SansMedium, 15), (Face.SansMedium, 14), (Face.SansMedium, 13), (Face.SansMedium, 12), (Face.SansMedium, 11),
        (Face.Heading, 16), (Face.SansSemibold, 14), (Face.SansSemibold, 13), (Face.SansSemibold, 12),
        (Face.SansSemibold, 11), (Face.SansSemibold, 10),
        (Face.SansBold, 14), (Face.SansBold, 13),
        (Face.Display, 30), (Face.Display, 28), (Face.Display, 26), (Face.Display, 24), (Face.Display, 22), (Face.Display, 20),
        (Face.Display, 19), (Face.Display, 16), (Face.Display, 15), (Face.Display, 13),
        (Face.Heading, 20), (Face.Heading, 18), (Face.Heading, 16), (Face.Heading, 15), (Face.Heading, 14), (Face.Heading, 13),
        (Face.Mono, 13), (Face.Mono, 12), (Face.Mono, 11), (Face.Mono, 10),
        (Face.MonoMedium, 12), (Face.MonoMedium, 11),
    };

    private readonly Dictionary<(Face, int), IFontHandle> handles = new();

    /// <summary>
    /// A size that isn't loaded, mapped once to the nearest loaded one (looking it up anew sorted the
    /// loaded sizes on every call). These are the same handles as above, so only those are disposed.
    /// </summary>
    private readonly Dictionary<(Face, int), IFontHandle> aliases = new();

    private int generation;

    /// <summary>
    /// Goes up whenever any face is rebuilt (Dalamud's scale or font settings changed, or a face has
    /// just finished loading), so what was measured with the old glyphs is measured again.
    /// </summary>
    public int Generation => Volatile.Read(ref generation);

    public Fonts()
    {
        var atlas = Services.PluginInterface.UiBuilder.FontAtlas;
        var japanese = Services.ClientState.ClientLanguage == ClientLanguage.Japanese;
        foreach (var (face, px) in Wanted)
        {
            var file = face switch
            {
                Face.Sans => "Figtree-Regular.ttf",
                Face.SansMedium => "Figtree-Medium.ttf",
                Face.SansSemibold => "Figtree-SemiBold.ttf",
                Face.SansBold => "Figtree-Bold.ttf",
                Face.Display => "Outfit-Bold.ttf",
                Face.Heading => "Outfit-SemiBold.ttf",
                Face.MonoMedium => "GeistMono-Medium.ttf",
                _ => "GeistMono-Regular.ttf",
            };
            var line = face switch
            {
                Face.Display or Face.Heading => OutfitLine,
                Face.Mono or Face.MonoMedium => GeistMonoLine,
                _ => FigtreeLine,
            };
            var dalamudFont = japanese && face is Face.Sans or Face.SansMedium or Face.SansSemibold or Face.SansBold;
            var handle = Make(atlas, file, px * line, dalamudFont);
            // Dalamud may say so from another thread.
            handle.ImFontChanged += (_, _) => Interlocked.Increment(ref generation);
            handles[(face, px)] = handle;
        }
    }

    /// <summary>Pushes a face at a CSS size (the nearest loaded size when that one isn't).</summary>
    public IDisposable Push(Face face, int px) => Handle(face, px).Push();

    public IFontHandle Handle(Face face, int px)
    {
        if (handles.TryGetValue((face, px), out var h) || aliases.TryGetValue((face, px), out h)) return h;
        var best = handles.Keys.Where(k => k.Item1 == face).OrderBy(k => Math.Abs(k.Item2 - px)).FirstOrDefault();
        h = handles.TryGetValue(best, out var near) ? near : handles[(Face.Sans, 14)];
        aliases[(face, px)] = h;
        return h;
    }

    private static IFontHandle Make(IFontAtlas atlas, string file, float sizePx, bool dalamudFont) =>
        atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var font = dalamudFont
                ? tk.AddDalamudDefaultFont(sizePx, null)
                : tk.AddFontFromMemory(Read(file), new SafeFontConfig { SizePx = sizePx }, file);
            // The game's own symbols (the HQ mark in item names) and whatever Dalamud's language needs.
            tk.AddGameSymbol(new SafeFontConfig { SizePx = sizePx, MergeFont = font });
            tk.AttachExtraGlyphsForDalamudLanguage(new SafeFontConfig { SizePx = sizePx, MergeFont = font });
        }));

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
        foreach (var h in handles.Values) h.Dispose();
        handles.Clear();
        aliases.Clear();
    }
}

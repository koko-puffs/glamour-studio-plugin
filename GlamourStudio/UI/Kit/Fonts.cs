using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game;
using Dalamud.Interface.ManagedFontAtlas;

namespace GlamourStudio.UI;

/// <summary>Glamour Studio's faces, as the site uses them.</summary>
public enum Face
{
    /// <summary>DM Sans 400: what you read.</summary>
    Sans,

    /// <summary>DM Sans 500: names in lists.</summary>
    SansMedium,

    /// <summary>DM Sans 600: buttons, small headings.</summary>
    SansSemibold,

    /// <summary>DM Sans 700: the rose pill's words.</summary>
    SansBold,

    /// <summary>Syne 800, in capitals: what's big (the window's titles, a look's name).</summary>
    Display,

    /// <summary>DM Mono 400: labels and eyebrows in spaced capitals, counts that line up.</summary>
    Mono,

    /// <summary>DM Mono 500: a label that leads.</summary>
    MonoMedium,
}

/// <summary>
/// The faces at the sizes the window uses, loaded through Dalamud's font atlas (so they follow
/// Dalamud's global scale and rebuild with it): DM Sans 15/14/13/12 (400, 500, 600) and 14/13 (700);
/// Syne 30/24/20/16 (800); DM Mono 12/11/10 (400) and 12/11 (500). Under the SIL Open Font License
/// 1.1, with their licences (Resources/Fonts); DM Sans and Syne are the variable fonts' static
/// instances at those weights, made with fontTools (nothing else changed).
///
/// Sizes are CSS sizes (the em). ImGui sizes a font by its line (ascent to descent), so each face is
/// loaded at its em times its line ratio, and 14px DM Sans here is as tall as 14px in the browser.
///
/// On a Japanese client the sans sizes are Dalamud's own font (it has the kana and kanji that item
/// names need); Syne and the mono keep the design's face (they only ever show the plugin's words).
/// </summary>
public sealed class Fonts : IDisposable
{
    /// <summary>(hhea ascent − descent) / em, read from the font files.</summary>
    private const float DmLine = 1.302f;

    private const float SyneLine = 1.2f;

    private static readonly (Face Face, int Px)[] Wanted =
    {
        (Face.Sans, 15), (Face.Sans, 14), (Face.Sans, 13), (Face.Sans, 12),
        (Face.SansMedium, 15), (Face.SansMedium, 14), (Face.SansMedium, 13), (Face.SansMedium, 12),
        (Face.SansSemibold, 18), (Face.SansSemibold, 15), (Face.SansSemibold, 14), (Face.SansSemibold, 13),
        (Face.SansBold, 14), (Face.SansBold, 13),
        (Face.Display, 30), (Face.Display, 24), (Face.Display, 20), (Face.Display, 16),
        (Face.Mono, 12), (Face.Mono, 11), (Face.Mono, 10),
        (Face.MonoMedium, 12), (Face.MonoMedium, 11),
    };

    private readonly Dictionary<(Face, int), IFontHandle> handles = new();

    public Fonts()
    {
        var atlas = Services.PluginInterface.UiBuilder.FontAtlas;
        var japanese = Services.ClientState.ClientLanguage == ClientLanguage.Japanese;
        foreach (var (face, px) in Wanted)
        {
            var file = face switch
            {
                Face.Sans => "DMSans-Regular.ttf",
                Face.SansMedium => "DMSans-Medium.ttf",
                Face.SansSemibold => "DMSans-SemiBold.ttf",
                Face.SansBold => "DMSans-Bold.ttf",
                Face.Display => "Syne-ExtraBold.ttf",
                Face.MonoMedium => "DMMono-Medium.ttf",
                _ => "DMMono-Regular.ttf",
            };
            var line = face == Face.Display ? SyneLine : DmLine;
            var dalamudFont = japanese && face is Face.Sans or Face.SansMedium or Face.SansSemibold or Face.SansBold;
            handles[(face, px)] = Make(atlas, file, px * line, dalamudFont);
        }
    }

    /// <summary>Pushes a face at a CSS size (the nearest loaded size when that one isn't).</summary>
    public IDisposable Push(Face face, int px) => Handle(face, px).Push();

    public IFontHandle Handle(Face face, int px)
    {
        if (handles.TryGetValue((face, px), out var h)) return h;
        var best = handles.Keys.Where(k => k.Item1 == face).OrderBy(k => Math.Abs(k.Item2 - px)).FirstOrDefault();
        return handles.TryGetValue(best, out h) ? h : handles[(Face.Sans, 14)];
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
    }
}

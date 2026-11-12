using System;
using System.Collections.Generic;
using System.Text;

namespace GlamourStudio.UI.Model;

/// <summary>
/// Which characters a face can draw, and what to do with the rest: the window's faces are Latin
/// (Bricolage, Shantell, Figtree, Geist Mono, each with Latin-1 and the common punctuation in its
/// atlas), and a look's, a maker's or a character's name can be anything. A string is split into
/// runs drawn in its face and runs drawn in the fallback (Noto Sans CJK, which has kana, kanji,
/// hangul, hanzi and more), and a character neither has yet is left out (never ImGui's "?") and
/// asked for: the atlas is built again with it. Pure, so it's tested.
/// </summary>
public static class Glyphs
{
    /// <summary>What every face's atlas holds from the start: Latin-1 and the punctuation and signs the window uses (pairs, inclusive).</summary>
    public static readonly (char From, char To)[] Base =
    {
        (' ', '~'), (' ', 'ÿ'), ('‐', '‧'), ('‰', '›'), ('€', '€'),
        ('™', '™'), ('←', '↓'), ('★', '☆'), ('♥', '♥'), ('✓', '✓'), ('✕', '✕'),
    };

    /// <summary>
    /// True when every character is printable ASCII (in every face) or a line break: the usual case,
    /// decided without a lookup.
    /// </summary>
    public static bool IsPlainAscii(string text)
    {
        foreach (var c in text)
            if ((c < 0x20 && c != '\n') || c > 0x7E) return false;
        return true;
    }

    /// <summary>
    /// A Latin letter or sign past Latin-1 (Latin Extended A and B, Extended Additional, the general
    /// punctuation and signs): asked for, it's added to the window's own faces as well (most have
    /// it), so "Łukasz" stays in one face; anything else comes from the fallback alone.
    /// </summary>
    public static bool IsLatinish(char c) => c is >= 'Ā' and <= 'ɏ' or >= 'Ḁ' and <= 'ỿ' or >= ' ' and <= '⯿';

    /// <summary>
    /// A character worth asking the atlas for: not a control, not half of a pair outside the BMP
    /// (emoji: ImGui's atlas holds 16-bit characters only), not the private use area (the game's
    /// own symbols are merged in apart), not the replacement or zero-width marks.
    /// </summary>
    public static bool Wantable(char c) =>
        c >= 0x20 && !char.IsSurrogate(c) && c is not (>= '' and <= '') && c is not ('�' or '﻿' or '​' or '‌' or '‍')
        && !(c >= 0x7F && c < 0xA0);

    /// <summary>
    /// The text in runs: each (its characters, whether they're drawn in the fallback). A character
    /// <paramref name="inFace"/> has goes in the face's run; one only <paramref name="inFallback"/> has,
    /// in a fallback run; one neither has is left out, and added to <paramref name="wanted"/> if it's
    /// <see cref="Wantable"/>. Null when the face has every character (draw the text as it is). A
    /// line break is the face's: no font has a glyph for it, ImGui breaks the line there itself (a
    /// tooltip's second line ran into its first once the text had a "·" in it).
    /// </summary>
    public static List<(string Text, bool Fallback)>? Split(string text, Func<char, bool> inFace, Func<char, bool> inFallback, ISet<char>? wanted)
    {
        List<(string Text, bool Fallback)>? runs = null;
        var sb = new StringBuilder();
        var fallback = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            bool fb;
            if (c == '\n' || inFace(c)) fb = false;
            else if (inFallback(c)) fb = true;
            else
            {
                if (Wantable(c)) wanted?.Add(c);
                runs ??= Begin(text, i);
                continue;
            }
            if (fb && runs == null) runs = Begin(text, i);
            if (runs == null) continue;
            if (sb.Length > 0 && fb != fallback)
            {
                runs.Add((sb.ToString(), fallback));
                sb.Clear();
            }
            fallback = fb;
            sb.Append(c);
        }
        if (runs == null) return null;
        if (sb.Length > 0) runs.Add((sb.ToString(), fallback));
        return runs;

        // The first character that isn't in the face: what's before it is one run in the face.
        List<(string, bool)> Begin(string t, int at)
        {
            var list = new List<(string, bool)>();
            if (at > 0)
            {
                sb.Append(t, 0, at);
                fallback = false;
            }
            return list;
        }
    }

    /// <summary>
    /// ImGui's glyph ranges (pairs, inclusive, ending with 0) for <see cref="Base"/> and the characters
    /// given, sorted and joined where they touch.
    /// </summary>
    public static ushort[] Ranges(IEnumerable<char> extra, bool withBase = true)
    {
        var points = new SortedSet<char>(extra);
        var ranges = new List<(int From, int To)>();
        if (withBase)
            foreach (var (from, to) in Base) ranges.Add((from, to));
        foreach (var c in points) ranges.Add((c, c));
        ranges.Sort((a, b) => a.From.CompareTo(b.From));
        var merged = new List<(int From, int To)>();
        foreach (var r in ranges)
        {
            if (merged.Count > 0 && r.From <= merged[^1].To + 1)
                merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, r.To));
            else merged.Add(r);
        }
        var result = new ushort[merged.Count * 2 + 1];
        for (var i = 0; i < merged.Count; i++)
        {
            result[i * 2] = (ushort)merged[i].From;
            result[i * 2 + 1] = (ushort)merged[i].To;
        }
        return result;
    }

    /// <summary>
    /// The fallback's size to draw <paramref name="px"/> with: the smallest of <paramref name="sizes"/>
    /// (ascending) at least as big (drawn a little smaller stays sharp), else the biggest (drawn bigger).
    /// </summary>
    public static int Nearest(int px, IReadOnlyList<int> sizes)
    {
        foreach (var s in sizes)
            if (s >= px) return s;
        return sizes[^1];
    }
}

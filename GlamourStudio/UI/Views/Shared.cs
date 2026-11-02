using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GlamourStudio.Core;
using static GlamourStudio.UI.Theme;

namespace GlamourStudio.UI;

/// <summary>What the screens share: where a piece is, a look's row, a piece's row, dyes as swatches.</summary>
public sealed partial class MainWindow
{
    /// <summary>Every piece you have and its handiest place, made again whenever the wardrobe changes.</summary>
    private Dictionary<uint, Place> placeIndex = new();

    private (int Version, string? Key, bool Dresser, bool Armoire) placeIndexFor = (-1, null, false, false);

    /// <summary>Where a piece is: the dresser or armoire as the game has them now when it does, else the plugin's record.</summary>
    private Place? Where(uint item)
    {
        var me = wardrobe.Current;
        var stamp = (wardrobe.Version, me?.Key, wardrobe.DresserLive, wardrobe.ArmoireLive);
        if (stamp != placeIndexFor)
        {
            placeIndexFor = stamp;
            var index = new Dictionary<uint, Place>();
            if (me != null)
            {
                foreach (var place in Enumerable.Reverse(Places.ByPriority))
                    if (me.Get(place) is { } r)
                        foreach (var id in r.Items)
                            index[LookJson.BaseId(id)] = place;
                if (wardrobe.ArmoireLive)
                    foreach (var id in wardrobe.ArmoireNow.Keys)
                        index[id] = Place.Armoire;
                if (wardrobe.DresserLive)
                    foreach (var d in me.Dresser)
                        index[LookJson.BaseId(d.Item)] = Place.Dresser;
            }
            placeIndex = index;
        }
        return placeIndex.TryGetValue(LookJson.BaseId(item), out var p) ? p : null;
    }

    private Readiness ReadinessOf(Look look) => Readiness.Of(look.Pieces, Where);

    private bool Linked => sync.Linked && !sync.TokenRefused;

    /// <summary>The look's pieces' icons, four at most, in a 2×2 tile (<paramref name="size"/> square).</summary>
    private void Mosaic(Vector2 pos, float size, IReadOnlyDictionary<Slot, Piece> pieces, float radius = Palette.RadiusMd, float cellRadius = 8)
    {
        var dl = Kit.Dl;
        var r = S(radius);
        dl.AddRectFilled(pos, pos + new Vector2(size), Palette.U32(Palette.Raised), r);
        var shown = Slots.All.Where(pieces.ContainsKey).Take(4).ToList();
        if (shown.Count == 0)
        {
            Icons.Draw(dl, Icon.Hanger, pos + new Vector2(size * 0.25f), size * 0.5f, Palette.Faint);
            return;
        }
        var gap = S(2);
        var cell = (size - gap * 3) / 2;
        for (var i = 0; i < shown.Count; i++)
        {
            var p = pos + new Vector2(gap + (cell + gap) * (i % 2), gap + (cell + gap) * (i / 2));
            Kit.GameIcon(p, cell, data.Item(pieces[shown[i]].Item)?.Icon ?? 0, cellRadius);
        }
    }

    /// <summary>The words for a piece's dyes ("Snow White, Jet Black"), or null when it has none.</summary>
    private string? DyeLine(Piece p)
    {
        var names = new[] { p.Dye1, p.Dye2 }.Where(d => d != 0).Select(d => data.DyeName(d)).ToList();
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    /// <summary>An empty state: an icon well, a heading and a line, in the middle of a dashed card (the site's <c>.gs-empty</c>).</summary>
    private static void Empty(string id, Icon icon, string head, string line)
    {
        var at = Kit.At;
        var w = Kit.Width;
        var pad = S(22);
        var inner = w - pad * 2;
        var y = at.Y + pad;
        Kit.Well(new Vector2(at.X + (w - S(44)) / 2, y), S(44), icon, rose: false, iconPx: 20);
        y += S(44) + S(12);
        var hw = Txt.Measure(head, Face.SansSemibold, 15).X;
        Txt.Draw(Kit.Dl, new Vector2(at.X + (w - MathF.Min(hw, inner)) / 2, y), Txt.Fit(head, Face.SansSemibold, 15, inner), Face.SansSemibold, 15, Palette.Ink);
        y += Txt.Natural(Face.SansSemibold, 15) + S(4);
        var lh = Txt.Flow(new Vector2(at.X + pad, y), inner, new[] { new Run(line, Face.Sans, 13, Palette.Muted) }, 19.5f, out _, center: true);
        y += lh + pad;
        Kit.Dashed(at, new Vector2(at.X + w, y), S(Palette.RadiusLg), Palette.LineDashed, 1f);
        Kit.Reserve(at, w, y - at.Y);
        _ = id;
    }
}

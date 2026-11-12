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

    /// <summary>The words for a piece's dyes ("Snow White, Jet Black"), or null when it has none.</summary>
    private string? DyeLine(Piece p)
    {
        var names = new[] { p.Dye1, p.Dye2 }.Where(d => d != 0).Select(d => data.DyeName(d)).ToList();
        return names.Count == 0 ? null : string.Join(", ", names);
    }
}

using System;
using System.Collections.Generic;

namespace GlamourStudio.Core;

/// <summary>Where a look's piece is, for a plate: a plate takes it from the dresser or the armoire only.</summary>
public enum PieceState
{
    /// <summary>In the glamour dresser or the armoire: ready for a plate.</summary>
    Ready,

    /// <summary>Yours, elsewhere (bags, armoury chest, saddlebags, a retainer): put it in the dresser first.</summary>
    ToStore,

    /// <summary>Not yours, as far as the plugin has read.</summary>
    Missing,
}

/// <summary>How ready a look is to go on a plate.</summary>
public sealed record Readiness(int Total, int Ready, int ToStore, int Missing)
{
    public bool PlateReady => Total > 0 && Ready == Total;

    /// <summary>Yours at all (wherever it is).</summary>
    public int Owned => Ready + ToStore;

    /// <summary>In a few words, for a row: "Ready for a plate", "2 to store", "6 of 8 ready".</summary>
    public string Short => Total == 0 ? "Nothing on it"
        : PlateReady ? "Ready for a plate"
        : Missing == 0 ? $"{ToStore} to store"
        : Ready == 0 && ToStore == 0 ? "None yours yet"
        : $"{Ready} of {Total} ready";

    public static PieceState StateOf(Place? where) => where switch
    {
        Place.Dresser or Place.Armoire => PieceState.Ready,
        null => PieceState.Missing,
        _ => PieceState.ToStore,
    };

    /// <summary>A look's pieces against your wardrobe (<paramref name="where"/> says where a piece is, or null).</summary>
    public static Readiness Of(IReadOnlyDictionary<Slot, Piece> pieces, Func<uint, Place?> where)
    {
        int ready = 0, store = 0, missing = 0;
        foreach (var p in pieces.Values)
        {
            switch (StateOf(where(LookJson.BaseId(p.Item))))
            {
                case PieceState.Ready:
                    ready++;
                    break;
                case PieceState.ToStore:
                    store++;
                    break;
                default:
                    missing++;
                    break;
            }
        }
        return new Readiness(pieces.Count, ready, store, missing);
    }
}

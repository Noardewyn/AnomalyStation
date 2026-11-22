using Anomaly.Core.DmShim;
using Anomaly.Core.Motion;

namespace Anomaly.Core.Tg;

// tg: no such var. DEVIATION(DV507): a map says what is over a turf (docs/ai/specs/map-editor.md 5.12).
public partial class turf
{
    /// <summary>
    /// The ceiling over the turf as its map sets it, a var of the turf's member in the map's file: null, the game's own rule (plating over what is
    /// indoors, nothing over space and outdoor areas: <see cref="CeilingSystem.DefaultPresent"/>); <c>"none"</c>, open to the sky; <c>"lattice"</c> or
    /// <c>"catwalk"</c>; or the path of a floor type, the block the ceiling is (<c>"/turf/open/floor/glass"</c>). It is read when the map is loaded; in a
    /// round the ceiling is the ceiling system's and is built and broken there.
    /// </summary>
    public object ceiling;
}

public sealed partial class CeilingSystem
{
    /// <summary>The names a map's <c>ceiling</c> var takes beside a floor type's path.</summary>
    public const string MapNone = "none", MapLattice = "lattice", MapCatwalk = "catwalk";

    // The ceiling a turf's map asks for (turf.ceiling): false when the map says nothing, or something that is no ceiling, and the game's rule holds.
    private bool MapCeiling(turf t, out CeilingState state)
    {
        state = default;
        if (t.ceiling is not string asked || asked.Length == 0)
            return false;
        switch (asked)
        {
            case MapNone:
                return true;
            case MapLattice:
                state = CeilingState.Of(CeilingLattice.Lattice);
                return true;
            case MapCatwalk:
                state = CeilingState.Of(CeilingLattice.Catwalk);
                return true;
            default:
                if (asked.Length > CeilingState.MaxTurfLength || Proto(asked) is not turf_open_floor)
                    return false;
                state = CeilingState.Of(asked);
                return true;
        }
    }

    // The ceiling a turf starts with: its map's (DV507), else plating where the game's rule puts one.
    private CeilingState StartOf(turf t)
    {
        if (MapCeiling(t, out CeilingState asked))
            return asked;
        string area = t.loc is area a ? DmTypes.PathOf(a.GetType()) : "";
        return DefaultPresent(DmTypes.PathOf(t.GetType()), area, t.loc is area o && Dm.truthy(o.outdoors)) ? CeilingState.Of(CeilingState.Plating) : default;
    }
}

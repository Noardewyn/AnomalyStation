// Engine ("BYOND builtin") part of tg's /area (docs/ai/translation-rules.md 1.1; docs/ai/specs/object-model.md section 2;
// docs/ai/specs/move-chain.md E1). Not translated from a tg file: BYOND declares /area; tg's own vars and procs are in
// game/area/areas.cs (whose dm_init_vars override is the class's only one, rules 10.2: this part does not override it).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// RULE-GAP: as for /datum (Builtin/datum.builtin.cs), the engine part of a BYOND builtin type carries its [DmPath].
/// <summary>
/// An area holds turfs: a turf's loc is its area, and the area's contents list its turfs (move-chain.md E1;
/// <c>area.contents += turf</c> moves the turf into the area). An area is never inside anything (its loc is always null).
/// "For each area type defined, one area object is created at runtime" on the map; others are created with a null
/// location (BYOND ref <c>/area</c>). The default area of a grown map is the <c>world.area</c> instance
/// (<see cref="MapGrid"/>).
/// </summary>
// RULE-GAP: BYOND's area.contents also lists the movables standing directly on the area's turfs (BYOND ref
// /atom/var/contents); the contents list keeps only the turfs (and movables whose loc is the area itself), and a loop over
// the area walks both (dm_each_contents: tg's linkup, get_control_console, burglaralert).
[DmPath("/area")]
public partial class area : atom, IDmAtom
{
    DmList IDmAtom.dm_each_contents
    {
        get
        {
            DmList all = contents.Copy();
            foreach (turf t in Dm.each<turf>(contents))
                foreach (object thing in Dm.each(t))
                    Dm.append(ref all, thing);
            return all;
        }
    }

    /// <summary>
    /// BYOND's defaults of /area: layer 1 (AREA_LAYER), luminosity 1, the type-derived name (BYOND ref; OpenDream
    /// DMStandard/Types/Atoms/Area.dm). tg's overrides of /area follow in <see cref="dm_tg_vars"/> (rules 10.2,
    /// "Builtin types").
    /// </summary>
    public area()
    {
        layer = AREA_LAYER;
        luminosity = 1;
        name = dm_type_name(GetType());
        dm_tg_vars();
    }

    // tg's overrides of /area's defaults, implemented in game/area/areas.cs (rules 10.2, "Builtin types").
    private partial void dm_tg_vars();

    /// <summary>
    /// An area's coordinate is that of its turf "with the lowest z, y, and x coordinate (in that order)" (BYOND ref
    /// <c>/atom/var/x</c>); 0 when it holds no turf of the map.
    /// </summary>
    private protected override float dm_coord_raw(int axis)
    {
        turf lowest = dm_lowest_turf();
        return lowest == null ? 0 : axis == 0 ? lowest.dm_grid_x : axis == 1 ? lowest.dm_grid_y : lowest.dm_grid_z;
    }

    // The area's turf of the map with the lowest z, then y, then x.
    internal turf dm_lowest_turf()
    {
        turf lowest = null;
        foreach (atom thing in dm_held)
        {
            if (thing is not turf candidate || !candidate.dm_on_grid) continue;
            if (lowest == null
                || candidate.dm_grid_z < lowest.dm_grid_z
                || (candidate.dm_grid_z == lowest.dm_grid_z && (candidate.dm_grid_y < lowest.dm_grid_y
                    || (candidate.dm_grid_y == lowest.dm_grid_y && candidate.dm_grid_x < lowest.dm_grid_x))))
                lowest = candidate;
        }
        return lowest;
    }

    /// <summary>
    /// A new area's placement before New: an area is never inside anything; created at a turf, it takes that turf
    /// ("Newly created areas or turfs replace any existing area or turf at the specified location", BYOND ref
    /// <c>/proc/new</c>). Created with null (tg's map reader, reader.dm:917-927) it holds nothing yet.
    /// </summary>
    private protected override void dm_place_new(object initial_loc)
    {
        if (initial_loc is turf at)
            at.dm_set_loc(this);
    }
}

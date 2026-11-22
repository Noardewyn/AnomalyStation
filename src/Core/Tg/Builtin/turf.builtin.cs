// Engine ("BYOND builtin") part of tg's /turf (docs/ai/translation-rules.md 1.1; docs/ai/specs/object-model.md sections 2
// and 4; docs/ai/specs/move-chain.md E1, E3). Not translated from a tg file: BYOND declares /turf; tg's own vars and procs
// are in game/turfs/turf.cs and the other mirror files.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// RULE-GAP: as for /datum (Builtin/datum.builtin.cs), the engine part of a BYOND builtin type carries its [DmPath].
/// <summary>
/// A turf is one cell of the map grid (<see cref="MapGrid"/>): its x, y and z are fixed by the grid, its loc is its area,
/// and its contents are the movables standing on it. "Turfs cannot be moved. They can only be created or destroyed by
/// changing world.maxx, world.maxy, or world.maxz. When you create a new turf with new(), it always replaces the old one"
/// (BYOND ref <c>/turf</c>).
/// </summary>
[DmPath("/turf")]
public partial class turf : atom
{
    /// <summary>
    /// BYOND's defaults of /turf: layer 2 (TURF_LAYER), the type-derived name (BYOND ref <c>/atom/var/layer</c>,
    /// <c>name</c>). tg's overrides of /turf follow in <see cref="dm_tg_vars"/> (rules 10.2, "Builtin types").
    /// </summary>
    public turf()
    {
        layer = 2; // BYOND's TURF_LAYER (tg #defines that name as an #error, __DEFINES/layers.dm:160)
        name = dm_type_name(GetType());
        dm_tg_vars();
    }

    // tg's overrides of /turf's defaults, implemented in game/turfs/turf.cs (rules 10.2, "Builtin types").
    private partial void dm_tg_vars();

    // The grid coordinates; 0 off the map (a turf created without a turf location, or a replaced one). Hashed.
    private float _x;
    private float _y;
    private float _z;

    /// <summary>The turf's own coordinate (0 x, 1 y, 2 z); 0 off the map.</summary>
    private protected override float dm_coord_raw(int axis) => axis == 0 ? _x : axis == 1 ? _y : _z;

    // Whether the turf is a cell of its world's map grid.
    internal bool dm_on_grid => _z != 0;

    internal int dm_grid_x => (int)_x;
    internal int dm_grid_y => (int)_y;
    internal int dm_grid_z => (int)_z;

    // The grid gives the turf its coordinates when it places or replaces it (0, 0, 0 takes it off the map).
    internal void dm_set_grid_coords(int x, int y, int z)
    {
        _x = x;
        _y = y;
        _z = z;
    }

    /// <summary>
    /// A new turf's placement before New (object-model.md section 4, step 5.2; scope R16): a turf the grid creates for a
    /// grown map takes its cell in the default area; <c>new /turf/T(old)</c> with a turf of the map replaces it (BYOND ref
    /// <c>/proc/new</c>: "Newly created areas or turfs replace any existing area or turf at the specified location"): the
    /// new turf takes its coordinates and its place in the area, the old turf's movables move onto it in their order,
    /// silently, and the old turf leaves the map and the world (<see cref="MapGrid.Replace"/>). A turf created with
    /// anything else as its location stays off the map (x = y = z = 0, no area).
    /// </summary>
    // RULE-GAP: BYOND does not say what new /turf/T(null) gives; the port keeps the turf off the map (flagged for the
    // BYOND oracle list). ChangeTurf's turf identity (a reference to the old turf naming the new one) is the M3 decision.
    private protected override void dm_place_new(object initial_loc)
    {
        MapGrid grid = MapGrid.Of(World.Current);
        grid.Sync();
        if (grid.TakeSlot(out int x, out int y, out int z, out area in_area))
        {
            grid.Place(this, x, y, z, in_area);
            return;
        }
        if (initial_loc is turf old && old.dm_on_grid)
        {
            grid.Replace(old, this);
            // TURF-IDENTITY: the area's per-z turf lists hold the location in BYOND, so the old turf's entries name this one
            old.dm_replace_listed_turf(this);
        }
    }
}

// Engine ("BYOND builtin") part of tg's /atom/movable (docs/ai/translation-rules.md 1.1; docs/ai/specs/object-model.md
// section 2). Not translated from a tg file: BYOND declares /atom/movable and its builtin vars; tg's own vars and procs
// are in game/atoms_movable.cs and the other mirror files.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// RULE-GAP: as for /datum (Builtin/datum.builtin.cs), the engine part of a BYOND builtin type carries its [DmPath].
[DmPath("/atom/movable")]
public partial class atom_movable : atom
{
    /// <summary>
    /// BYOND's default of a movable's <c>name</c>: the runtime type's (atom.dm_type_name), set first so that any explicit
    /// name of the type or an ancestor, assigned by a later constructor, wins (BYOND ref <c>/atom/var/name</c>). tg's
    /// overrides of /atom/movable follow in <see cref="dm_tg_vars"/> (rules 10.2, "Builtin types").
    /// </summary>
    public atom_movable()
    {
        name = dm_type_name(GetType());
        dm_tg_vars();
    }

    // tg's overrides of /atom/movable's defaults, implemented in game/atoms_movable.cs (rules 10.2, "Builtin types").
    private partial void dm_tg_vars();

    private float _glide_size;
    /// <summary>BYOND <c>glide_size</c> (default 0); an assignment to a registered atom notifies <c>Changed(Glide)</c>.</summary>
    public float glide_size
    {
        get => _glide_size;
        set
        {
            _glide_size = value;
            dm_glide_changed();
        }
    }

    /// <summary>
    /// BYOND <c>locs</c>: the turfs the movable covers, a new list on each read: <c>list(loc)</c> for a movable with a
    /// container (also when the container is not a turf: "If loc is not a turf, it will be the only item in the locs
    /// list"), empty in nullspace (BYOND ref <c>/atom/movable/var/locs</c>). On a turf, a movable whose bounds are wider or
    /// taller than a tile (tg's set_bounds of a multi-tile door, the M5 leftovers) covers the turfs under its bounding box:
    /// from its turf offset by <c>bound_x</c> and <c>bound_y</c>, <c>bound_width</c> by <c>bound_height</c> pixels (the
    /// port has no pixel steps: <c>step_x</c> and <c>step_y</c> stay 0), in BYOND's order (rows from the bottom, each from
    /// the left), the turfs past the map's edge left out. Moving and crossing still use the loc only (a multi-tile door's
    /// filler blocks its other tiles, as in tg).
    /// </summary>
    // RULE-GAP: object-model.md section 2 says "list(loc) for a movable on a turf, empty otherwise"; the BYOND reference
    // gives list(loc) for any non-null loc, which is followed here (tg reads locs only after an isturf(loc) test).
    public DmList locs
    {
        get
        {
            atom where = loc;
            if (where == null)
                return Dm.list();
            if (where is not turf t || (bound_width <= 32 && bound_height <= 32 && bound_x == 0 && bound_y == 0))
                return Dm.list(where);
            var covered = Dm.list();
            int x0 = (int)t.x + (int)MathF.Floor(bound_x / 32), y0 = (int)t.y + (int)MathF.Floor(bound_y / 32);
            int x1 = (int)t.x + (int)MathF.Ceiling((bound_x + bound_width) / 32) - 1, y1 = (int)t.y + (int)MathF.Ceiling((bound_y + bound_height) / 32) - 1;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (Builtins.locate(x, y, t.z) is turf covering)
                        Dm.append(covered, covering);
            return covered;
        }
    }

    /// <summary>BYOND <c>animate_movement</c> (default <c>FORWARD_STEPS</c>).</summary>
    public float animate_movement = FORWARD_STEPS;
    /// <summary>BYOND <c>step_x</c>.</summary>
    public float step_x;
    /// <summary>BYOND <c>step_y</c>.</summary>
    public float step_y;
    /// <summary>BYOND <c>bound_x</c>.</summary>
    public float bound_x;
    /// <summary>BYOND <c>bound_y</c>.</summary>
    public float bound_y;
    /// <summary>BYOND <c>bound_width</c> (default 32, one tile at tg's world.icon_size).</summary>
    public float bound_width = 32;
    /// <summary>BYOND <c>bound_height</c> (default 32).</summary>
    public float bound_height = 32;
    /// <summary>BYOND <c>screen_loc</c>.</summary>
    public string screen_loc;
    /// <summary>BYOND <c>particles</c>.</summary>
    public object particles;

    // presentation-seam.md section 5: glide_size is an appearance var of movables (forwarded to the replication hint sink
    // like every appearance change, atom.dm_appearance_changed).
    private void dm_glide_changed() => dm_appearance_changed(AppearanceAspect.Glide);

    /// <summary>
    /// BYOND's builtin <c>/atom/movable/Bump(Obstacle)</c>: called when a movement fails because of an obstacle; the
    /// default does nothing and returns null (BYOND ref <c>/atom/movable/proc/Bump</c>; move-chain.md E4). tg's
    /// <c>/atom/movable/Bump</c> (atoms_movable.dm:980) is the slot's root and reaches this with <c>..()</c> (rules 7.5).
    /// </summary>
    public object Bump__d0(atom Obstacle = null) => null;
}

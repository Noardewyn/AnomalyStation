// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/cliff.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the cliffs, which are entered only from their top and slide whoever steps on them down to their bottom
// (SScliff_falling) with a knockdown and a spin at every cliff tile, and the snowy cliff with its underlay (the ice moon, the bitrunning
// glacier grind). The var blocks are generated.

/// A cliff tile from where people can fall. Should generally fall downwards, but you can change it if you want
// tg: code/game/turfs/open/cliff.dm:2
public partial class turf_open_cliff : turf_open
{
    // tg: code/game/turfs/open/cliff.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        RegisterSignal(this, COMSIG_TURF_MOVABLE_THROW_LANDED, nameof(on_turf_movable_throw_landed));

        if (underlay_tile == null)
            return dot;
        image underlay = image(icon_state: Dm.initial(underlay_tile, (atom p) => p.icon_state), icon: Dm.initial(underlay_tile, (atom p) => p.icon));
        underlay.pixel_w = undertile_pixel_x; //if there's a pixel offset, correct it because we should be lined up with the grid
        underlay.pixel_z = undertile_pixel_y;
        SET_PLANE(underlay, Dm.truthy(underlay_plane) ? Dm.num(underlay_plane) : plane, this);
        Dm.append(underlays, underlay);
        return dot;
    }

    // tg: code/game/turfs/open/cliff.dm:38
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, COMSIG_TURF_MOVABLE_THROW_LANDED);
        return base.Destroy(force);
    }

    // tg: code/game/turfs/open/cliff.dm:42
    public override async DmTask<object> CanPass(atom_movable mover = null, object border_dir = null)
    {
        await base.CanPass(mover, border_dir);

        if (Dm.truthy(Dm.bitand(border_dir, can_fall_from_direction)) || !Dm.truthy(can_fall(mover)))
            return TRUE;

        return FALSE;
    }

    // tg: code/game/turfs/open/cliff.dm:50
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);

        try_fall(arrived);
        return dot;
    }

    // tg: code/game/turfs/open/cliff.dm:55
    public override async DmTask initialize_occupant(atom_movable occupant = null)
    {
        await base.initialize_occupant(occupant);
        try_fall(occupant);
    }

    // tg: code/game/turfs/open/cliff.dm:59
    public override async DmTask<object> zImpact(atom_movable falling = null, float? levels = null, turf prev_turf = null, float? flags = null)
    {
        flags ??= NONE;
        return await base.zImpact(falling, levels, prev_turf, flags: Dm.num(Dm.bitor(flags, FALL_INTERCEPTED)));
    }

    /// Something landed on us
    // tg: code/game/turfs/open/cliff.dm:63
    [SignalHandler]
    public virtual void on_turf_movable_throw_landed(turf turf = null, atom_movable arrived = null)
    {
        try_fall(arrived);
    }

    /// Try and make them fall
    // tg: code/game/turfs/open/cliff.dm:69
    public virtual void try_fall(atom_movable arrived = null)
    {
        if (Dm.truthy(can_fall(arrived)))
            fall(arrived);
    }

    /// Check if they can fall from us
    // tg: code/game/turfs/open/cliff.dm:74
    public virtual object can_fall(atom_movable arrived = null)
    {
        // Check if we're a protected type that doesnt make sense to fall (like effects and bullets)
        if (Dm.truthy(is_type_in_list(arrived, protected_types)))
            return FALSE;

        if (arrived.throwing != null || HAS_TRAIT(arrived, TRAIT_CLIFF_WALKER) || HAS_TRAIT(arrived, TRAIT_MOVE_FLYING))
            return FALSE;

        // We're already falling
        if (Dm.truthy(arrived.anchored) || Dm.@in(arrived, SScliff_falling.cliff_grinders))
            return FALSE;

        // We can walk infront of the bottom cliff turf, so check that here
        if (get_step(this, fall_direction) is not turf_open_cliff && !Dm.truthy(Dm.bitand(get_dir(arrived, this), fall_direction)))
            return FALSE;

        // gravity
        // marked in UNLINT due to a spacemandmm bug: https://github.com/SpaceManiac/SpacemanDMM/issues/382 (REMOVE ONCE FIXED!)
        if (!Dm.truthy(arrived.has_gravity(this)))
            return FALSE;

        return TRUE;
    }

    /// Make them fall!
    // tg: code/game/turfs/open/cliff.dm:98
    public virtual void fall(atom_movable arrived = null)
    {
        SScliff_falling.start_falling(arrived, this); //the movement is handled by the subsystem, but we get asked about behaviour later
        on_fall(arrived);
    }

    /// We just fell onto this chasm tile
    // tg: code/game/turfs/open/cliff.dm:103
    public virtual void on_fall(atom_movable faller = null)
    {
        if (!isliving(faller))
            return;
        mob_living living = faller as mob_living;
        Waiting.NoSleep(living.Knockdown(fall_speed)); //OUCH- OW- CRAP- SHIT- OW-
        living.spin(fall_speed, fall_speed);
    }

    /// Check if the movement direction we're moving on (while already falling on us) is valid
    // tg: code/game/turfs/open/cliff.dm:111
    public virtual object can_move(atom_movable mover = null, turf target = null)
    {
        //check if the relative direction we're moving is allowed, if not we block the movement
        if (!Dm.truthy(Dm.bitand(valid_move_dirs, get_dir(this, target))))
            return FALSE;

        //we're trying to leave the cliff from somewhere that's not the bottom? no can do pall
        if (target is not turf_open_cliff && !Dm.eq(get_dir(this, target), fall_direction))
            return FALSE;

        return TRUE;
    }
}

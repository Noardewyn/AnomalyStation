// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/shuttle_cling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///Gets added to all movables that enter hyperspace and are supposed to suffer from "hyperspace drift"
///This lets people fly around shuttles during transit using jetpacks, or cling to the side if they got a spacesuit
///Dumping into deepspace is handled by the hyperspace turf, not the component.
///Not giving something this component while on hyperspace is safe, it just means free movement like carps
// tg: code/datums/components/shuttle_cling.dm:17
public partial class datum_component_shuttle_cling : datum_component
{
    ///Instead of a high move force we just get launched away dramatically because we're that hopeless
    private const int SUPER_NOT_HOLDING_ON = 0;
    ///We're not holdin on and will get thrown off
    private const int NOT_HOLDING_ON = 1;
    ///We're holding on, but will be pulled slowly
    private const int CLINGING = 2;
    ///We're holding on really well and aren't suffering from any pull
    private const int ALL_GOOD = 3;

    // tg: code/datums/components/shuttle_cling.dm:31
    [DmParams("direction")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        direction = Dm.arg(__extra, 0);

        ADD_TRAIT(parent, TRAIT_HYPERSPACED, REF(this));

        RegisterSignals(parent, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MOVABLE_UNBUCKLE, COMSIG_ATOM_NO_LONGER_PULLED), nameof(update_state));
        RegisterSignal(parent, SIGNAL_REMOVETRAIT(TRAIT_FREE_HYPERSPACE_MOVEMENT), nameof(initialize_loop));
        RegisterSignal(parent, SIGNAL_ADDTRAIT(TRAIT_FREE_HYPERSPACE_MOVEMENT), nameof(clear_loop));

        //Items have this cool thing where they're first put on the floor if you grab them from storage, and then into your hand, which isn't caught by movement signals that well
        if (isitem(parent))
            RegisterSignal(parent, COMSIG_ITEM_PICKUP, nameof(do_remove));

        if (!HAS_TRAIT(parent, TRAIT_FREE_HYPERSPACE_MOVEMENT))
            initialize_loop();

        update_state(); //otherwise we'll get moved 1 tile before we can correct ourselves, which isnt super bad but just looks jank
        return null;
    }

    // tg: code/datums/components/shuttle_cling.dm:54
    [SignalHandler]
    public virtual void initialize_loop()
    {
        hyperloop = GLOB.move_manager.move(
            moving: parent,
            direction: direction,
            delay: not_clinging_move_delay,
            subsystem: SShyperspace_drift,
            priority: MOVEMENT_ABOVE_SPACE_PRIORITY,
            flags: Dm.bitor(MOVEMENT_LOOP_NO_DIR_UPDATE, MOVEMENT_LOOP_OUTSIDE_CONTROL)) as datum_move_loop_move;
        update_state();
    }

    // tg: code/datums/components/shuttle_cling.dm:58
    [SignalHandler]
    public virtual void clear_loop()
    {
        qdel(hyperloop);
        hyperloop = null;
    }

    ///Check if we're in hyperspace and our state in hyperspace
    // tg: code/datums/components/shuttle_cling.dm:62
    [SignalHandler]
    public virtual void update_state()
    {
        var clinger = (atom_movable)parent;
        if (!is_on_hyperspace(clinger))
        {
            qdel(this);
            return;
        }

        if (hyperloop == null)
            return;

        bool should_loop = false;

        switch (is_holding_on(clinger))
        {
            case SUPER_NOT_HOLDING_ON:
                launch_very_hard(clinger);
                should_loop = true;
                break;
            case NOT_HOLDING_ON:
                hyperloop.set_delay(not_clinging_move_delay);
                should_loop = true;
                hyperloop.direction = direction; //we're not close to anything so reset direction if we got diagonalized
                break;
            case CLINGING:
                hyperloop.set_delay(clinging_move_delay);
                should_loop = true;
                update_drift_direction(clinger);
                break;
            case ALL_GOOD:
                should_loop = false;
                break;
        }

        // the hyperloop can get reset to null from the above procs
        if (hyperloop == null)
            return;

        //Do pause/unpause/nothing for the hyperloop
        if (should_loop && Dm.truthy(Dm.bitand(hyperloop.status, MOVELOOP_STATUS_PAUSED)))
            hyperloop.resume_loop();
        else if (!should_loop && !Dm.truthy(Dm.bitand(hyperloop.status, MOVELOOP_STATUS_PAUSED)))
            hyperloop.pause_loop();
    }

    ///Check if we're "holding on" to the shuttle
    // tg: code/datums/components/shuttle_cling.dm:96
    public virtual int is_holding_on(atom_movable movee)
    {
        if (movee.pulledby != null || !isturf(movee.loc) || HAS_TRAIT(movee, TRAIT_FREE_HYPERSPACE_MOVEMENT))
            return ALL_GOOD;

        if (movee is not mob_living living)
        {
            if (HAS_TRAIT(movee, TRAIT_FORCED_GRAVITY)) // nothing can block the singularity
                return SUPER_NOT_HOLDING_ON;

            if (is_tile_solid(get_step(movee, Dm.num(direction)) as turf)) //something is blocking us so do the cool drift
                return CLINGING;

            return SUPER_NOT_HOLDING_ON;
        }

        //Check if we can interact with stuff (checks for alive, arms, stun, etc)
        if (!Dm.truthy(Waiting.NoSleep(living.can_perform_action(living, Dm.bitor(FORBID_TELEKINESIS_REACH, NEED_HANDS)))))
            return NOT_HOLDING_ON;

        if (living.buckled != null)
            return ALL_GOOD;

        foreach (object handlebar in Dm.each(range(1, living)))
        {
            if (isclosedturf(handlebar))
                return CLINGING;
            if (handlebar is obj @object && Dm.truthy(@object.anchored) && Dm.truthy(@object.density))
                return CLINGING;
        }
        return NOT_HOLDING_ON;
    }

    ///Are we on a hyperspace tile? There's some special bullshit with lattices so we just wrap this check
    // tg: code/datums/components/shuttle_cling.dm:130
    public virtual bool is_on_hyperspace(atom_movable clinger)
        => Dm.istype(clinger.loc, hyperspace_type) && !HAS_TRAIT((atom)clinger.loc, TRAIT_HYPERSPACE_STOPPED);

    ///Launch the atom very hard, away from hyperspace
    // tg: code/datums/components/shuttle_cling.dm:136
    public virtual void launch_very_hard(atom_movable byebye)
    {
        Waiting.Detach(byebye.safe_throw_at(get_edge_target_turf(byebye, direction) as atom, 200, 1, spin: TRUE, force: MOVE_FORCE_EXTREMELY_STRONG));
    }

    ///Check if we arent just being blocked, and if we are give us some diagonal push so we cant just infinitely cling to the front
    // tg: code/datums/components/shuttle_cling.dm:140
    public virtual void update_drift_direction(atom_movable clinger)
    {
        var potential_blocker = get_step(clinger, Dm.num(direction)) as turf;
        //We are not being blocked, so just give us cardinal drift
        if (!is_tile_solid(potential_blocker))
        {
            hyperloop.direction = direction;
            return;
        }

        //We're already moving diagonally
        if (!Dm.eq(hyperloop.direction, direction))
        {
            float side_dir = Dm.num(hyperloop.direction) - Dm.num(direction);

            if (is_tile_solid(get_step(clinger, side_dir) as turf))
                hyperloop.direction = Dm.num(direction) + REVERSE_DIR(side_dir); //We're bumping a wall to the side, so switch to the other side_dir (yes this adds pingpong protocol)
            return;
        }

        //Get the directions from the side of our current drift direction (so if we have drift south, get all cardinals and remove north and south, leaving only east and west)
        var side_dirs = (DmList)shuffle(Dm.sub(Dm.sub(GLOB.cardinals, direction), REVERSE_DIR(Dm.num(direction))) as DmList);

        //We check if one side is solid
        if (!is_tile_solid(get_step(clinger, Dm.num(side_dirs[1])) as turf))
            hyperloop.direction = Dm.num(direction) + Dm.num(side_dirs[1]);
        else //if one side isnt solid, send it to the other side (it can also be solid but we dont care cause we're boxed in then and not like itll matter much then)
            hyperloop.direction = Dm.num(direction) + Dm.num(side_dirs[2]);
    }

    ///Check if it's a closed turf or contains a dense object
    // tg: code/datums/components/shuttle_cling.dm:173
    public virtual bool is_tile_solid(turf maybe_solid)
    {
        if (isclosedturf(maybe_solid))
            return true;
        foreach (obj blocker in Dm.each<obj>(maybe_solid.contents))
        {
            if (Dm.truthy(blocker.density))
                return true;
        }
        return false;
    }

    ///This is just for signals and doesn't run for most removals, so dont add behaviour here expecting it to do much
    // tg: code/datums/components/shuttle_cling.dm:182
    [SignalHandler]
    public virtual void do_remove()
    {
        qdel(this);
    }

    // tg: code/datums/components/shuttle_cling.dm:187
    public override object Destroy(float? force = null)
    {
        REMOVE_TRAIT(parent, TRAIT_HYPERSPACED, REF(this));
        qdel(hyperloop);
        hyperloop = null;

        return base.Destroy(force);
    }
}

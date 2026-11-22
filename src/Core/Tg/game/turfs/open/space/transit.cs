// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/space/transit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G1 (D272): hyperspace, the turfs of the reserved level's transit docks.

// tg: code/game/turfs/open/space/transit.dm:1
public partial class turf_open_space_transit : turf_open_space
{
    // tg: code/game/turfs/open/space/transit.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        RegisterSignal(this, COMSIG_TURF_RESERVATION_RELEASED, nameof(launch_contents));
        RegisterSignal(this, COMSIG_ATOM_ENTERED, nameof(initialize_drifting));
        RegisterSignal(this, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(initialize_drifting_but_from_initialize));
        return dot;
    }

    // tg: code/game/turfs/open/space/transit.dm:17
    public override object Destroy(float? force = null)
    {
        //Signals are NOT removed from turfs upon replacement, and we get replaced ALOT, so unregister our signal
        UnregisterSignal(this, Dm.list(COMSIG_TURF_RESERVATION_RELEASED, COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));

        return base.Destroy(force);
    }

    // tg: code/game/turfs/open/space/transit.dm:23
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        object dot = base.get_smooth_underlay_icon(underlay_appearance, asking_turf, adjacency_dir);
        underlay_appearance.icon_state = Dm.text("speedspace_ns_[]", get_transit_state(asking_turf));
        underlay_appearance.transform = Dm.turn(Dm.matrix(), Dm.num(get_transit_angle(asking_turf))) as matrix;
        return dot;
    }

    // tg: code/game/turfs/open/space/transit.dm:28
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        transform = Dm.turn(Dm.matrix(), Dm.num(get_transit_angle(this))) as matrix;
        return dot;
    }

    // tg: code/game/turfs/open/space/transit.dm:32
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("speedspace_ns_[]", get_transit_state(this));
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/turfs/open/space/transit.dm:36
    public virtual void initialize_drifting(atom entered = null, atom_movable enterer = null)
    {
        if (enterer != null && !HAS_TRAIT(enterer, TRAIT_HYPERSPACED) && !HAS_TRAIT(this, TRAIT_HYPERSPACE_STOPPED))
        {
            Waiting.NoSleep(enterer.AddComponent(typeof(datum_component_shuttle_cling), REVERSE_DIR(Dm.num(dir))));
        }
    }

    // tg: code/game/turfs/open/space/transit.dm:42
    public virtual void initialize_drifting_but_from_initialize(atom_movable location = null, atom_movable enterer = null, object mapload = null)
    {
        if (!Dm.truthy(mapload) && !Dm.truthy(enterer.anchored))
            initialize_drifting(this, enterer); // INVOKE_ASYNC (the handler does not sleep)
    }

    // tg: code/game/turfs/open/space/transit.dm:48
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);

        object location = gone.loc;
        if (location is turf_open_space && !Dm.istype(location, GetType()))//they got forced out of transit area into default space tiles
            dump_in_space(gone); //launch them into game space, away from transitspace
        return dot;
    }

    // tg: code/game/turfs/open/space/transit.dm:80
    public override object CanBuildHere()
    {
        return SSshuttle.is_in_shuttle_bounds(this);
    }

    ///Get rid of all our contents, called when our reservation is released (which in our case means the shuttle arrived)
    // tg: code/game/turfs/open/space/transit.dm:56
    public virtual void launch_contents(datum_turf_reservation reservation = null)
    {
        foreach (atom_movable movable in Dm.each<atom_movable>(contents))
            dump_in_space(movable);
    }

}

public static partial class GlobalProcs
{
    ///Dump a movable in a random valid spacetile
    // tg: code/game/turfs/open/space/transit.dm:63
    public static void dump_in_space(atom_movable dumpee = null)
    {
        if (HAS_TRAIT(dumpee, TRAIT_DEL_ON_SPACE_DUMP))
        {
            qdel(dumpee);
            return;
        }

        float max = Dm.world.maxx - TRANSITIONEDGE;
        float min = 1 + TRANSITIONEDGE;

        DmList possible_transtitons = Dm.list();
        foreach (datum_space_level level in Dm.each<datum_space_level>(SSmapping.z_list))
        {
            if (Dm.eq(level.linkage, CROSSLINKED))
                Dm.append(ref possible_transtitons, level.z_value);
        }
        if (!Dm.truthy(length(possible_transtitons))) //No space to throw them to - try throwing them onto mining
        {
            possible_transtitons = SSmapping.levels_by_trait(ZTRAIT_MINING) as DmList;
            if (!Dm.truthy(length(possible_transtitons))) //Just throw them back on station, if not just runtime.
                possible_transtitons = SSmapping.levels_by_trait(ZTRAIT_STATION) as DmList;
        }

        //move the dumpee to a random coordinate turf
        Waiting.NoSleep(dumpee.forceMove(locate(rand(min, max), rand(min, max), Dm.num(pick(possible_transtitons)))));
    }

    // tg: code/game/turfs/open/space/transit.dm:102
    public static object get_transit_state(turf T = null)
    {
        float p = 9;
        float dot = 1;
        float tdir = Dm.num(T.dir);
        switch (tdir)
        {
            case var _ when tdir == NORTH:
                dot = Dm.mod(-p * T.x + T.y, 15) + 1;
                if (dot < 1)
                    dot += 15;
                break;
            case var _ when tdir == EAST:
                dot = Dm.mod(T.x + p * T.y, 15) + 1;
                break;
            case var _ when tdir == WEST:
                dot = Dm.mod(T.x - p * T.y, 15) + 1;
                if (dot < 1)
                    dot += 15;
                break;
            default:
                dot = Dm.mod(p * T.x + T.y, 15) + 1;
                break;
        }
        return dot;
    }

    // tg: code/game/turfs/open/space/transit.dm:120
    public static object get_transit_angle(turf T = null)
    {
        float dot = 0;
        float tdir = Dm.num(T.dir);
        switch (tdir)
        {
            case var _ when tdir == NORTH:
                dot = 180;
                break;
            case var _ when tdir == EAST:
                dot = 90;
                break;
            case var _ when tdir == WEST:
                dot = -90;
                break;
        }
        return dot;
    }
}

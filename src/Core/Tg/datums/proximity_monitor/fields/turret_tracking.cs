// Translated from tgstation (snapshot 1d99bd7f): code/datums/proximity_monitor/fields/turret_tracking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4d, D316). The var block is generated (D111).

/// Proximity monitor that checks to see to see if anything worth shooting is within the bound of a turret.
/// Used so turrets don't waste time processing if nothing is near.
// tg: code/datums/proximity_monitor/fields/turret_tracking.dm:3
public partial class datum_proximity_monitor_advanced_turret_tracking : datum_proximity_monitor_advanced
{
    /// Typecache of "interesting" objects.
    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:8  var/static/list/interesting_typecache
    internal static ref DmList interesting_typecache => ref TgState.Current.datum_proximity_monitor_advanced_turret_tracking__interesting_typecache;

    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:10
    [DmParams("_host", "range", "_ignore_if_not_on_turf")]
    public override async DmTask New(params object[] __extra)
    {
        atom _host = Dm.arg<atom>(__extra, 0);
        if (!istype(_host, typeof(obj_machinery_porta_turret)))
            CRASH("Turret trackers should only be used with /obj/machinery/porta_turret as a host!");
        await base.New(__extra);
        if (interesting_typecache == null)
        {
            interesting_typecache = typecacheof(Dm.list(
                typeof(mob_living_basic),
                typeof(mob_living_carbon),
                typeof(mob_living_silicon_robot),
                typeof(mob_living_simple_animal),
                typeof(obj_structure_blob),
                typeof(obj_vehicle_sealed_mecha))) as DmList;
        }
    }

    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:24
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        tracking = null; // LAZYNULL(tracking): just to be sure
        return dot;
    }

    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:28
    public override void setup_field_turf(turf target = null)
    {
        foreach (atom_movable thing in Dm.each<atom_movable>(target.contents))
            start_tracking(thing);
    }

    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:32
    public override void cleanup_field_turf(turf target = null)
    {
        foreach (atom_movable thing in Dm.each<atom_movable>(target.contents))
            stop_tracking(thing);
    }

    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:36
    public override void field_turf_crossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        start_tracking(movable);
    }

    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:39
    public override void field_turf_uncrossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        if (get_dist(new_location, host) > Dm.num(current_range))
            stop_tracking(movable);
    }

    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:43
    public virtual void start_tracking(atom_movable thing = null)
    {
        if (QDELETED(this) || QDELETED(thing))
            return;
        if (thing.invisibility > SEE_INVISIBLE_LIVING || !Dm.truthy(is_type_in_typecache(thing, interesting_typecache)))
            return;
        if (Dm.@in(thing, tracking)) // check this last
            return;
        obj_machinery_porta_turret turret = host as obj_machinery_porta_turret;
        if (isliving(thing) && Dm.truthy(turret.in_faction(thing as mob))) // hopefully no weird edge-cases where something's faction changes while its in range... right???
            return;
        LAZYADD(ref tracking, thing);
        RegisterSignal(thing, COMSIG_QDELETING, nameof(stop_tracking));
        refresh_turret_processing();
    }

    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:57
    [SignalHandler]
    public virtual void stop_tracking(atom_movable thing = null)
    {
        if (thing == null || !Dm.@in(thing, tracking))
            return;
        UnregisterSignal(thing, COMSIG_QDELETING);
        // not using LAZYREMOVE so we can just do LAZYLEN once
        Dm.remove(tracking, thing);
        if (!Dm.truthy(LAZYLEN(tracking)))
        {
            tracking = null; // LAZYNULL(tracking)
            refresh_turret_processing();
        }
    }

    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:69
    public virtual void refresh_turret_processing()
    {
        obj_machinery_porta_turret turret = host as obj_machinery_porta_turret;
        if (QDELETED(this) || QDELETED(turret))
            return;
        if (!Dm.eq(turret.check_should_process(), FALSE)) // if check_should_process did something other than end processing, we have nothing more to do here.
            return;
        if (Dm.truthy(turret.raised) && !Dm.truthy(turret.always_up)) // if its cover is up, then we'll pop the turret's cover down.
            turret.popDown(); // INVOKE_ASYNC: popDown cannot sleep
    }
}

public sealed partial class TgState
{
    // tg: code/datums/proximity_monitor/fields/turret_tracking.dm:8  /datum/proximity_monitor/advanced/turret_tracking/var/static/list/interesting_typecache
    internal DmList datum_proximity_monitor_advanced_turret_tracking__interesting_typecache;
}

// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_gravity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice, part 2 (D167): the whole file, the gravitational anomaly (the high one with its gravity field and the
// big one too) and its circle wave. The high one's detonation blacks out the station's gravity generator (since gaps E1b). The var
// blocks are generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:2
public partial class obj_effect_anomaly_grav : obj_effect_anomaly
{
    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:13  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_effect_anomaly_grav__Initialize__loc_connections;

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:11
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        apply_wibbly_filters(this);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:19
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        await base.anomalyEffect(seconds_per_tick);
        boing = 1;
        foreach (obj O in Dm.each<obj>(orange(4, this)))
        {
            if (!Dm.truthy(O.anchored))
                await step_towards(O, this);
        }
        foreach (mob_living M in Dm.each<mob_living>(range(0, this)))
        {
            if (InertSeam.IsInert(M)) // DEVIATION(DV83): an inert mob (D112: no DNA, organs or limbs) is left alone
                continue;
            if (!Dm.truthy(M.mob_negates_gravity()))
                await gravShock(M);
        }
        foreach (mob_living M in Dm.each<mob_living>(orange(4, this)))
        {
            if (InertSeam.IsInert(M)) // DEVIATION(DV83)
                continue;
            if (!Dm.truthy(M.mob_negates_gravity()))
                await LocomotionSeam.PullOrStep(M, this); // DEVIATION(DV357)
        }
        foreach (obj O in Dm.each<obj>(range(0, this)))
        {
            if (Dm.truthy(O.anchored) || HAS_TRAIT(O, TRAIT_UNDERFLOOR))
                continue;
            mob_living target = Dm.locate<mob_living>(view(4, this));
            if (Dm.truthy(target) && !IS_UNCONSCIOUS_OR_CRIT(target) && prob(object_launch_prob))
                await O.throw_at(target, 5, 10);
        }
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:38
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        Waiting.NoSleep(gravShock(AM as mob_living));
    }

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:42
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        await gravShock(bumped_atom as mob_living);
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:45
    public override object Bumped(atom_movable bumped_atom = null)
    {
        Waiting.NoSleep(gravShock(bumped_atom as mob_living));
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:48
    public virtual async DmTask gravShock(mob_living living_debris = null)
    {
        if (living_debris != null && InertSeam.IsInert(living_debris)) // DEVIATION(DV83): the anomaly's touch leaves an inert mob alone
            return;
        if (Dm.truthy(boing) && isliving(living_debris) && !IS_UNCONSCIOUS_OR_CRIT(living_debris) && !Dm.truthy(living_debris.mob_negates_gravity()))
        {
            await living_debris.Knockdown(4 * SECONDS);
            atom target = get_edge_target_turf(living_debris, get_dir(this, get_step_away(living_debris, this))) as atom;
            await living_debris.throw_at(target, 5, 1);
            boing = 0;
        }
    }

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:55
    public override async DmTask detonate()
    {
        Dm.@new<obj_effect_temp_visual_circle_wave_gravity>(get_turf(this));
        playsound(this, Dm.resource("sound/effects/magic/cosmic_energy.ogg"), vol: 50);
    }
}

// tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:59
public partial class obj_effect_anomaly_grav_high : obj_effect_anomaly_grav
{
    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:62
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.Detach(setup_grav_field());
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:66
    public virtual async DmTask setup_grav_field()
    {
        grav_field = Dm.@new<datum_proximity_monitor_advanced_gravity>(this, 7, TRUE, rand(0, 3));
    }

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:69
    public override async DmTask detonate()
    {
        await base.detonate();
        foreach (obj_machinery_gravity_generator_main the_generator in Dm.each_cast<obj_machinery_gravity_generator_main>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_gravity_generator_main))))
        {
            if (Dm.truthy(is_station_level(the_generator.z)))
                await the_generator.blackout();
        }
    }

    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:75
    public override object Destroy(float? force = null)
    {
        object dot = null;
        QDEL_NULL(ref grav_field);
        dot = base.Destroy(force);
        return dot;
    }
}

///Bigger, meaner, immortal gravity anomaly. although this is just the super grav anomaly but bigger and shattering move force
// tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:80
public partial class obj_effect_anomaly_grav_high_big : obj_effect_anomaly_grav_high
{
    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:85
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        transform = Dm.mul(transform, 3);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/anomalies/anomalies_gravity.dm:13  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_effect_anomaly_grav__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_anomaly_grav.on_entered)));
}

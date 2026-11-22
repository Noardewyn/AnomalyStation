// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_bioscrambler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;


namespace Anomaly.Core.Tg;

// The M7 delamination slice (G6, D167): the whole file, the bioscrambler anomaly (the delamination's effect_anomaly makes the docile
// one). Its pulse's bioscramble swaps an organ and a limb (carbon_defense.cs; DEVIATION(DV89): never for an inert type). The anomaly
// core waits for M9 (_anomalies.cs). The var block is generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_bioscrambler.dm:2
public partial class obj_effect_anomaly_bioscrambler : obj_effect_anomaly
{
    // tg: code/game/objects/effects/anomalies/anomalies_bioscrambler.dm:19
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        pursuit_target = WEAKREF(find_nearest_target() as datum) as datum_weakref;
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_bioscrambler.dm:23
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.anomalyEffect(seconds_per_tick);
        if (!(pulse_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return dot;

        Dm.@new<obj_effect_temp_visual_circle_wave_bioscrambler>(get_turf(this));
        playsound(this, Dm.resource("sound/effects/magic/cosmic_energy.ogg"), vol: 50, vary: TRUE);
        pulse_cooldown = world.time + pulse_delay; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
        foreach (mob_living_carbon nearby in Dm.each<mob_living_carbon>(hearers(range, this)))
            await nearby.bioscramble(name);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_bioscrambler.dm:34
    public override async DmTask move_anomaly()
    {
        update_target();
        if (pursuit_target == null)
        {
            await base.move_anomaly();
            return;
        }
        turf step_turf = get_step(this, get_dir(this, pursuit_target.resolve()));
        if (!HAS_TRAIT(step_turf, TRAIT_CONTAINMENT_FIELD))
            await Move(step_turf);
    }

    /// Select a new target if we need one
    // tg: code/game/objects/effects/anomalies/anomalies_bioscrambler.dm:43
    public virtual void update_target()
    {
        mob_living current_target = pursuit_target?.resolve() as mob_living;
        if (QDELETED(current_target))
            pursuit_target = null;
        if (pursuit_target != null && prob(80))
            return;
        mob_living new_target = find_nearest_target() as mob_living;
        if (new_target == null)
        {
            pursuit_target = null;
            return;
        }
        if (new_target == current_target)
            return;
        current_target = new_target;
        pursuit_target = WEAKREF(new_target) as datum_weakref;
        new_target.ominous_nosebleed();
    }

    /// Returns the closest conscious carbon on our z level or null if there somehow isn't one
    // tg: code/game/objects/effects/anomalies/anomalies_bioscrambler.dm:60
    public virtual object find_nearest_target()
    {
        float closest_distance = INFINITY;
        mob_living_carbon closest_target = null;
        foreach (mob_living_carbon target in Dm.each<mob_living_carbon>(GLOB.player_list))
        {
            if (target.z != z)
                continue;
            if (HAS_TRAIT(target, TRAIT_GODMODE) || IS_UNCONSCIOUS(target))
                continue; // Don't just haunt a corpse
            float distance_from_target = get_dist(this, target);
            if (distance_from_target >= closest_distance)
                continue;
            closest_distance = distance_from_target;
            closest_target = target;
        }

        return closest_target;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_bioscrambler.dm:81
    public override async DmTask detonate()
    {
        pulse_cooldown = 0; // COOLDOWN_RESET (__DEFINES/cooldowns.dm:115), inline (rules 11.1 (a))
        await anomalyEffect();
    }
}

/// A bioscrambler anomaly subtype which does not pursue people, for purposes of a space ruin
// tg: code/game/objects/effects/anomalies/anomalies_bioscrambler.dm:77
public partial class obj_effect_anomaly_bioscrambler_docile : obj_effect_anomaly_bioscrambler
{
    // tg: code/game/objects/effects/anomalies/anomalies_bioscrambler.dm:79
    public override void update_target()
    {
        return;
    }
}

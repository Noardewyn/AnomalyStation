// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_vortex.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;


namespace Anomaly.Core.Tg;

// The M7 delamination slice (G6, D167): the whole file, the vortex anomaly the delamination's effect_anomaly makes. The anomaly
// core waits for M9 (_anomalies.cs). The var block is generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_vortex.dm:2
public partial class obj_effect_anomaly_bhole : obj_effect_anomaly
{
    // tg: code/game/objects/effects/anomalies/anomalies_vortex.dm:8
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        await base.anomalyEffect(seconds_per_tick);
        if (!isturf(loc)) //blackhole cannot be contained inside anything. Weird stuff might happen
        {
            qdel(this);
            return null;
        }

        await grav(rand(0, 3), rand(2, 3), 50, 25);

        //Throwing stuff around!
        foreach (obj O in Dm.each<obj>(range(2, this)))
        {
            if (O == this)
                return null; //DON'T DELETE YOURSELF GOD DAMN
            if (!Dm.truthy(O.anchored))
            {
                mob_living target = Dm.locate<mob_living>(view(4, this));
                if (Dm.truthy(target) && !IS_UNCONSCIOUS_OR_CRIT(target))
                    await O.throw_at(target, 7, 5);
            }
            else
                Dm.append(ref SSexplosions.med_mov_atom, O);
        }
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_vortex.dm:28
    public virtual async DmTask grav(object r = null, object ex_act_force = null, object pull_chance = null, object turf_removal_chance = null)
    {
        for (float t = -Dm.num(r); t < Dm.num(r); t++)
        {
            await affect_coord(x + t, y - Dm.num(r), ex_act_force, pull_chance, turf_removal_chance);
            await affect_coord(x - t, y + Dm.num(r), ex_act_force, pull_chance, turf_removal_chance);
            await affect_coord(x + Dm.num(r), y + t, ex_act_force, pull_chance, turf_removal_chance);
            await affect_coord(x - Dm.num(r), y - t, ex_act_force, pull_chance, turf_removal_chance);
        }
    }

    // tg: code/game/objects/effects/anomalies/anomalies_vortex.dm:35
    public virtual async DmTask affect_coord(object x = null, object y = null, object ex_act_force = null, object pull_chance = null, object turf_removal_chance = null)
    {
        //Get turf at coordinate
        turf T = locate(Dm.num(x), Dm.num(y), z);
        if (T == null)
            return;

        //Pulling and/or ex_act-ing movable atoms in that turf
        if (prob(Dm.num(pull_chance)))
        {
            foreach (obj O in Dm.each<obj>(T.contents))
            {
                if (Dm.truthy(O.anchored))
                {
                    switch (Dm.num(ex_act_force))
                    {
                        case EXPLODE_DEVASTATE:
                            Dm.append(ref SSexplosions.high_mov_atom, O);
                            break;
                        case EXPLODE_HEAVY:
                            Dm.append(ref SSexplosions.med_mov_atom, O);
                            break;
                        case EXPLODE_LIGHT:
                            Dm.append(ref SSexplosions.low_mov_atom, O);
                            break;
                    }
                }
                else
                    await step_towards(O, this);
            }
            foreach (mob_living M in Dm.each<mob_living>(T.contents))
                await LocomotionSeam.PullOrStep(M, this); // DEVIATION(DV357)
        }

        //Damaging the turf
        if (Dm.truthy(T) && prob(Dm.num(turf_removal_chance)))
        {
            switch (Dm.num(ex_act_force))
            {
                case EXPLODE_DEVASTATE:
                    Dm.append(ref SSexplosions.highturf, T);
                    break;
                case EXPLODE_HEAVY:
                    Dm.append(ref SSexplosions.medturf, T);
                    break;
                case EXPLODE_LIGHT:
                    Dm.append(ref SSexplosions.lowturf, T);
                    break;
            }
        }
    }

    // tg: code/game/objects/effects/anomalies/anomalies_vortex.dm:66
    public override async DmTask detonate()
    {
        Dm.@new<obj_effect_temp_visual_circle_wave_vortex_small>(get_turf(this));
        playsound(this, Dm.resource("sound/effects/hallucinations/far_noise.ogg"), vol: 50);
    }
}

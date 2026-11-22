// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_pyroclastic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice, part 2 (D167): the whole file, the pyroclastic anomaly (and the big one). Its slime (a pyroclastic
// slime: the basic slime mob, made by makepyroslime) and the ghost poll that picks its player are deferred (the slimes and the
// ghosts' polls, M10). The var blocks are generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:2
public partial class obj_effect_anomaly_pyro : obj_effect_anomaly
{
    // tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:16
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        apply_wibbly_filters(this);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:20
    public override object Destroy(float? force = null)
    {
        chosen_one = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:24
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        await base.anomalyEffect(seconds_per_tick);
        if (!Dm.truthy(immortal) && !Dm.truthy(already_polling) && Dm.num(death_time) < (world.time - poll_time))
            await start_poll();
        ticks += Dm.num(seconds_per_tick);
        if (ticks < releasedelay)
            return FALSE;
        else
            ticks -= releasedelay;
        turf_open tile = get_turf(this) as turf_open;
        if (tile is turf_open)
            tile.atmos_spawn_air(Dm.text("[]=5;[]=5;[]", GAS_O2, GAS_PLASMA, TURF_TEMPERATURE(1000)));
        return TRUE;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:38
    public override async DmTask detonate()
    {
        Waiting.Detach(makepyroslime());
    }

    // tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:41
    public virtual async DmTask start_poll()
    {
        already_polling = TRUE;
        // chosen_one = SSpolling.poll_ghosts_for_target(check_jobban = ROLE_SENTIENCE, poll_time = poll_time, checked_target = src,
        //     ignore_category = POLL_IGNORE_PYROSLIME, alert_pic = src, role_name_text = "pyroclastic anomaly slime")
        Dm.Deferred("code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:43", "start_poll: the ghosts' poll for the pyroclastic slime's player (the ghosts' polls, M10+)");
    }

    // tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:45
    public virtual async DmTask makepyroslime()
    {
        turf_open tile = get_turf(this) as turf_open;
        if (tile is turf_open)
            tile.atmos_spawn_air(Dm.text("[]=500;[]=500;[]", GAS_O2, GAS_PLASMA, TURF_TEMPERATURE(1000))); //Make it hot and burny for the new slime

        // var/new_colour = pick(/datum/slime_type/red, /datum/slime_type/orange)
        // var/mob/living/basic/slime/pyro = new(tile, new_colour, SLIME_LIFE_STAGE_ADULT); pyro.set_enraged_behaviour()
        // if(chosen_one) PossessByPlayer, the pyro_slime antag datum and the game log
        Dm.Deferred("code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:50", "makepyroslime: the enraged adult pyroclastic slime and its player (the slimes and the ghosts' polls, M10+)");
        chosen_one = null;
    }
}

///Bigger, meaner, immortal pyro anomaly
// tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:62
public partial class obj_effect_anomaly_pyro_big : obj_effect_anomaly_pyro
{
    // tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:68
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        transform = Dm.mul(transform, 3);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:73
    public override object Bumped(atom_movable bumped_atom = null)
    {
        object dot = null;
        dot = base.Bumped(bumped_atom);

        if (isliving(bumped_atom))
        {
            mob_living living = (mob_living)bumped_atom;
            Waiting.NoSleep(living.dust());
        }
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_pyroclastic.dm:80
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.anomalyEffect(seconds_per_tick);

        if (!Dm.truthy(dot))
            return dot;

        turf turf = get_turf(this);
        if (!isgroundlessturf(turf))
            await turf.TerraformTurf(typeof(turf_open_lava_smooth_weak), flags: CHANGETURF_INHERIT_AIR);
        return dot;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/dash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/datums/actions/mobs/dash.dm:1
public partial class datum_action_cooldown_mob_cooldown_dash : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/dash.dm:10
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        await dash_to(target_atom);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    // tg: code/datums/actions/mobs/dash.dm:17
    public virtual async DmTask dash_to(atom dash_target = null)
    {
        DmList accessable_turfs = Dm.list();
        float self_dist_to_target = 0;
        turf own_turf = get_turf(owner);
        if (!QDELETED(dash_target))
            self_dist_to_target += get_dist(dash_target, own_turf);
        foreach (turf_open check_turf in Dm.each<turf_open>(RANGE_TURFS(dash_range, own_turf)))
        {
            float turf_dist_to_target = 0;
            if (!QDELETED(dash_target))
                turf_dist_to_target += get_dist(dash_target, check_turf);
            if (get_dist(owner, check_turf) >= dash_range && turf_dist_to_target <= self_dist_to_target && !islava(check_turf) && !ischasm(check_turf))
            {
                bool valid = true;
                foreach (turf T in Dm.each<turf>(get_line(own_turf, check_turf)))
                {
                    if (Dm.truthy(await T.is_blocked_turf(TRUE)))
                    {
                        valid = false;
                        continue;
                    }
                }
                if (valid)
                    accessable_turfs[check_turf] = turf_dist_to_target;
            }
        }
        turf target_turf;
        if (!QDELETED(dash_target))
        {
            float closest_dist = dash_range;
            foreach (object t in Dm.each(accessable_turfs))
            {
                if (Dm.num(accessable_turfs[t]) < closest_dist)
                    closest_dist = Dm.num(accessable_turfs[t]);
            }
            foreach (object t in Dm.each(accessable_turfs))
            {
                if (Dm.num(accessable_turfs[t]) != closest_dist)
                    Dm.remove(ref accessable_turfs, t);
            }
        }
        if (!Dm.truthy(LAZYLEN(accessable_turfs)))
            return;
        target_turf = pick(accessable_turfs) as turf;
        turf step_back_turf = get_step(target_turf, get_cardinal_dir(target_turf, own_turf));
        turf step_forward_turf = get_step(own_turf, get_cardinal_dir(own_turf, target_turf));
        Dm.@new<obj_effect_temp_visual_small_smoke_halfsecond>(step_back_turf);
        Dm.@new<obj_effect_temp_visual_small_smoke_halfsecond>(step_forward_turf);
        obj_effect_temp_visual_decoy_fading_halfsecond D = Dm.@new<obj_effect_temp_visual_decoy_fading_halfsecond>(own_turf, owner);
        await owner.forceMove(step_back_turf);
        playsound(own_turf, Dm.resource("sound/items/weapons/punchmiss.ogg"), 40, TRUE, -1);
        owner.alpha = 0;
        animate(owner, Dm.named("alpha", 255), Dm.named("time", 5));
        await sleep(0.2f * SECONDS); // SLEEP_CHECK_DEATH(0.2 SECONDS, owner)
        if (QDELETED(owner) || owner.stat == DEAD)
            return;
        await D.forceMove(step_forward_turf);
        await owner.forceMove(target_turf);
        playsound(target_turf, Dm.resource("sound/items/weapons/punchmiss.ogg"), 40, TRUE, -1);
        await sleep(0.1f * SECONDS); // SLEEP_CHECK_DEATH(0.1 SECONDS, owner)
        if (QDELETED(owner) || owner.stat == DEAD)
            return;
    }
}

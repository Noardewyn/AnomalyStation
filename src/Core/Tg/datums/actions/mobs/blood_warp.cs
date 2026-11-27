// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/blood_warp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/datums/actions/mobs/blood_warp.dm:1
public partial class datum_action_cooldown_mob_cooldown_blood_warp : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/blood_warp.dm:14
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        await blood_warp(target_atom);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    // tg: code/datums/actions/mobs/blood_warp.dm:21
    public virtual async DmTask<object> blood_warp(atom target = null)
    {
        if (Dm.truthy(await owner.Adjacent(target)))
            return FALSE;

        turf target_turf = get_turf(target);
        turf owner_turf = get_turf(owner);

        if (target_turf.z != owner_turf.z)
            return FALSE;

        DmList can_jaunt = get_bloodcrawlable_pools(owner_turf, 1) as DmList;
        if (!Dm.truthy(can_jaunt.len))
            return FALSE;

        object chosen_pick_range = get_pick_range();
        DmList pools = get_bloodcrawlable_pools(target_turf, chosen_pick_range) as DmList;
        if (Dm.truthy(remove_inner_pools))
        {
            DmList pools_to_remove = get_bloodcrawlable_pools(target_turf, Dm.num(chosen_pick_range) - 1) as DmList;
            Dm.remove(ref pools, pools_to_remove);
        }
        if (!Dm.truthy(pools.len))
            return FALSE;

        obj_effect_temp_visual_decoy DA = Dm.@new<obj_effect_temp_visual_decoy>(owner.loc, owner);
        DA.color = COLOR_RED;
        object oldtransform = DA.transform;
        DA.transform = Dm.mul(Dm.matrix(), 2);
        animate(DA, Dm.named("alpha", 255), Dm.named("color", Dm.initial(DA, (atom p) => p.color)), Dm.named("transform", oldtransform), Dm.named("time", 3));
        // SLEEP_CHECK_DEATH(0.3 SECONDS, owner) (__DEFINES/mobs.dm:627), expanded inline
        await sleep(0.3f * SECONDS);
        if (QDELETED(owner) || owner.stat == DEAD)
            return null;
        qdel(DA);

        obj_effect_decal_cleanable found_bloodpool = null; // tg types it blood; any bloodcrawlable decal is picked
        pools = get_bloodcrawlable_pools(target_turf, chosen_pick_range) as DmList;
        if (Dm.truthy(remove_inner_pools))
        {
            DmList pools_to_remove = get_bloodcrawlable_pools(target_turf, Dm.num(chosen_pick_range) - 1) as DmList;
            Dm.remove(ref pools, pools_to_remove);
        }
        if (Dm.truthy(pools.len))
        {
            shuffle_inplace(pools);
            found_bloodpool = pick(pools) as obj_effect_decal_cleanable;
        }
        if (found_bloodpool != null)
        {
            owner.visible_message(span_danger(I18n.T("[] sinks into the blood...", owner)));
            playsound(owner_turf, Dm.resource("sound/effects/magic/enter_blood.ogg"), 100, TRUE, -1);
            await owner.forceMove(get_turf(found_bloodpool));
            playsound(get_turf(owner), Dm.resource("sound/effects/magic/exit_blood.ogg"), 100, TRUE, -1);
            owner.visible_message(span_danger(I18n.T("And springs back out!")));
            owner.SEND_SIGNAL(COMSIG_BLOOD_WARP, Dm.list(owner));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/datums/actions/mobs/blood_warp.dm:73
    public virtual object get_pick_range()
    {
        if (owner.client != null)
            return client_pick_range;
        return pick_range;
    }
}

public static partial class GlobalProcs
{
    // tg: code/datums/actions/mobs/blood_warp.dm:78
    public static object get_bloodcrawlable_pools(turf T = null, object range = null)
    {
        if (Dm.num(range) < 0)
            return Dm.list();
        DmList dot = Dm.list();
        foreach (obj_effect_decal_cleanable nearby in Dm.each<obj_effect_decal_cleanable>(view(T, range)))
        {
            if (Dm.truthy(nearby.can_bloodcrawl_in()))
                Dm.append(ref dot, nearby);
        }
        return dot;
    }
}

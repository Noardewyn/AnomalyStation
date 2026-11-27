// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The ash drake's tail spike, which the ice whelp drops too (ice_whelp.dm:15); the megafauna's other trophies come with them.

//ash drake
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:21
public partial class obj_item_crusher_trophy_tail_spike : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:27
    public override string effect_desc()
    {
        return I18n.T("mark detonation to do <b>[]</b> damage to nearby creatures and push them back", bonus_value);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:30
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        foreach (mob_living living_target in Dm.each<mob_living>(oview(2, user)))
        {
            if (Dm.truthy(user.faction_check_atom(living_target)) || living_target.stat == DEAD)
                continue;
            playsound(living_target, Dm.resource("sound/effects/magic/fireball.ogg"), 20, TRUE);
            Dm.@new<obj_effect_temp_visual_fire>(living_target.loc);
            _addtimer(Dm.@new<datum_callback>(this, nameof(pushback), living_target, user), 1, file: "code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm", line: 37); //no free backstabs, we push AFTER module stuff is done
            await living_target.adjust_fire_loss(bonus_value, forced: TRUE);
        }
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:40
    public virtual async DmTask pushback(mob_living target = null, mob_living user = null)
    {
        if (!QDELETED(target) && !QDELETED(user) && (!Dm.truthy(target.anchored) || ismegafauna(target)) && target.move_resist < INFINITY) //megafauna will always be pushed
            await step(target, get_dir(user, target));
    }
}

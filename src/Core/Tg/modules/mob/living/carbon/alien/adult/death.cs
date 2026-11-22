// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/adult/death.dm:1
public partial class mob_living_carbon_alien_adult : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/death.dm:1
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (stat == DEAD)
            return null;

        object dot = await base.death(gibbed, __extra);

        await update_icons();
        status_flags = Dm.bitor(status_flags, CANPUSH);
        return dot;
    }
}

//When the alien queen dies, all others must pay the price for letting her die.
// tg: code/modules/mob/living/carbon/alien/adult/death.dm:11
public partial class mob_living_carbon_alien_adult_royal_queen : mob_living_carbon_alien_adult_royal
{
    // tg: code/modules/mob/living/carbon/alien/adult/death.dm:11
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (stat == DEAD)
            return null;

        foreach (mob_living_carbon C in Dm.each<mob_living_carbon>(GLOB.alive_mob_list))
        {
            if (C == this) //Make sure not to proc it on ourselves.
                continue;
            obj_item_organ_alien_hivenode node = C.get_organ_by_type(typeof(obj_item_organ_alien_hivenode)) as obj_item_organ_alien_hivenode;
            if (node is obj_item_organ_alien_hivenode) // just in case someone would ever add a diffirent node to hivenode slot
                await node.queen_death();
        }

        return await base.death(gibbed, __extra);
    }
}

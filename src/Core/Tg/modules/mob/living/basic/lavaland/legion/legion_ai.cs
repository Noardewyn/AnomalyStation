// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/legion/legion_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Target nearby friendlies if they are hurt (and are not themselves Legions)
// tg: code/modules/mob/living/basic/lavaland/legion/legion_ai.dm:25
public partial class datum_targeting_strategy_basic_legion : datum_targeting_strategy_basic
{
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_ai.dm:28
    public override object faction_check(datum_ai_controller controller = null, mob_living living_mob = null, mob_living the_target = null)
    {
        if (!Dm.truthy(living_mob.faction_check_atom(the_target, exact_match: check_factions_exactly)))
            return FALSE;
        if (istype(the_target, living_mob.type))
            return TRUE;
        atom created_by = (living_mob.ai_controller as datum_ai_controller).blackboard[BB_LEGION_BROOD_CREATOR] as atom;
        if (!QDELETED(created_by) && istype(the_target, created_by.type))
            return TRUE;
        return Dm.num(the_target.stat == DEAD || Dm.num(the_target.health) >= Dm.num(the_target.maxHealth));
    }
}

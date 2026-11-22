// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/dont_target_friends.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Don't target an atom in our friends list (or turfs), anything else is fair game
// tg: code/datums/ai/basic_mobs/targeting_strategies/dont_target_friends.dm:2
public partial class datum_targeting_strategy_basic_not_friends : datum_targeting_strategy_basic
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/dont_target_friends.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        if (Dm.truthy(attack_closed_turf) && isclosedturf(target))
            return TRUE;

        if (Dm.truthy(attack_obj) && isobj(target))
        {
            if (!Dm.truthy(Dm.bitand(((obj)target).resistance_flags, INDESTRUCTIBLE)))
                return TRUE;
        }

        if (isliving(target) && Dm.@in(target, ((datum_ai_controller)living_mob.ai_controller).blackboard[BB_FRIENDS_LIST]))
            return FALSE;

        return await base.is_valid_target(living_mob, target, vision_range, controller);
    }
}

/// Subtype that allows us to target items while deftly avoiding attacking our allies. Be careful when it comes to targeting items as an AI could get trapped targeting something it can't destroy.
// tg: code/datums/ai/basic_mobs/targeting_strategies/dont_target_friends.dm:38
public partial class datum_targeting_strategy_basic_not_friends_allow_items : datum_targeting_strategy_basic_not_friends
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/dont_target_friends.dm:40
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (isitem(target))
        {
            // trust fall exercise
            return TRUE;
        }
        return dot;
    }
}

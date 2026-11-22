// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/slime_food.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Slimes hunt people, and anything else when they're starving and rabid
// tg: code/datums/ai/basic_mobs/targeting_strategies/slime_food.dm:2
public partial class datum_targeting_strategy_slime_food : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/slime_food.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;

        if (controller == null)
            return FALSE;

        if (Dm.@in(target, controller.blackboard[BB_BASIC_MOB_RETALIATE_LIST]))
        {
            datum_targeting_strategy retaliate_strategy = GET_TARGETING_STRATEGY(typeof(datum_targeting_strategy_basic_not_friends));
            return await retaliate_strategy.is_valid_target(living_mob, target, vision_range);
        }

        mob_living_basic_slime hunter = living_mob as mob_living_basic_slime;
        mob_living candidate = target as mob_living;
        if (!isliving(candidate))
            return FALSE;

        if (hunter.buckled == candidate)
            return TRUE;

        DmList slime_faction = TgState.Current.datum_targeting_strategy_slime_food__slime_faction ??= string_list(Dm.list(FACTION_SLIME)) as DmList;

        if (Dm.truthy(FAST_FACTION_CHECK(slime_faction, candidate.get_faction(), hunter.allies, candidate.allies, FALSE)))
            return FALSE;

        if (!Dm.truthy(hunter.can_feed_on(candidate, check_adjacent: FALSE)))
            return FALSE;

        if (candidate == controller.blackboard[BB_CURRENT_TARGET])
            return can_see(hunter, candidate, Dm.num(vision_range));

        if (Dm.eq(controller.blackboard[BB_SLIME_HUNGER_LEVEL], SLIME_HUNGER_STARVING) && Dm.truthy(controller.blackboard[BB_SLIME_RABID]))
            return can_see(hunter, candidate, Dm.num(vision_range));

        if (isalien(candidate) || ishuman(candidate))
            return can_see(hunter, candidate, Dm.num(vision_range));

        return FALSE;
    }
}

public sealed partial class TgState
{
    internal DmList datum_targeting_strategy_slime_food__slime_faction;
}

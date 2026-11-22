// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/food_or_drink.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Like food_or_drink but always accepts drinks regardless of BB_IGNORE_DRINKS.
// tg: code/datums/ai/basic_mobs/targeting_strategies/food_or_drink.dm:23
public partial class datum_targeting_strategy_pickup_item_food_or_drink_include_drinks : datum_targeting_strategy_pickup_item_food_or_drink
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/food_or_drink.dm:25
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        return Dm.num(Dm.truthy(_is_food(target)) || Dm.truthy(_is_drink(target)));
    }
}

/// Accepts items that are edible or consumable from a bowl, or drinks when allowed by the controller blackboard.
// tg: code/datums/ai/basic_mobs/targeting_strategies/food_or_drink.dm:2
public partial class datum_targeting_strategy_pickup_item_food_or_drink : datum_targeting_strategy_pickup_item
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/food_or_drink.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        object find_drinks = controller?.blackboard[BB_IGNORE_DRINKS];
        return Dm.num(Dm.truthy(_is_food(target)) || (Dm.truthy(find_drinks) && Dm.truthy(_is_drink(target))));
    }

    // tg: code/datums/ai/basic_mobs/targeting_strategies/food_or_drink.dm:11
    public virtual object _is_food(atom thing = null) // obj/item in tg, but the sources hand it any atom in view (DM reads the var on any of them)
    {
        if (IS_EDIBLE(thing))
            return TRUE;
        if (thing is obj_item_reagent_containers_cup_bowl)
            return Dm.num(Dm.num((thing.reagents as datum_reagents)?.total_volume) > 0); // an inert type (the bar's glasses) has no holder yet: an empty one
        return FALSE;
    }

    // tg: code/datums/ai/basic_mobs/targeting_strategies/food_or_drink.dm:18
    public virtual object _is_drink(atom thing = null)
    {
        if (thing is obj_item_reagent_containers_cup_glass)
            return Dm.num(Dm.num((thing.reagents as datum_reagents)?.total_volume) > 0); // likewise
        return FALSE;
    }
}

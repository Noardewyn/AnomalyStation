// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/_targeting_strategy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///Datum for basic mobs to define what they can attack,
///Global, just like ai_behaviors
// tg: code/datums/ai/basic_mobs/targeting_strategies/_targeting_strategy.dm:3
public partial class datum_targeting_strategy : datum
{
    ///Returns true or false depending on if the target can be attacked by the mob.
    ///Base proc checks if target is within vision_range distance.
    // tg: code/datums/ai/basic_mobs/targeting_strategies/_targeting_strategy.dm:7
    public virtual async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        if (QDELETED(target))
            return FALSE;

        if (!Dm.truthy(vision_range))
            return TRUE;

        return Dm.num(get_dist(living_mob, target) <= Dm.num(vision_range));
    }

    /// Returns an atom the target might be hiding inside of, or null if none.
    // tg: code/datums/ai/basic_mobs/targeting_strategies/_targeting_strategy.dm:17
    public virtual object find_hidden_mobs(mob_living living_mob = null, atom target = null)
    {
        return null;
    }

    /// Returns TRUE if we should keep tracking an existing target when no new candidates are visible.
    /// Called with the loss range (typically larger than vision_range).
    /// Default delegates to is_valid_target so all normal checks still apply.
    // tg: code/datums/ai/basic_mobs/targeting_strategies/_targeting_strategy.dm:23
    public virtual async DmTask<object> can_keep_target(mob_living living_mob = null, atom target = null, object range = null, datum_ai_controller controller = null)
    {
        return await is_valid_target(living_mob, target, range, controller);
    }
}

/// Simply always returns true if you have a target, so only use this if you're pre-checking the targets somewhere else
// tg: code/datums/ai/basic_mobs/targeting_strategies/_targeting_strategy.dm:27
public partial class datum_targeting_strategy_anything : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/_targeting_strategy.dm:29
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        return TRUE;
    }
}

///A very simple targeting strategy that checks that the target is a valid fishing spot.
// tg: code/datums/ai/basic_mobs/targeting_strategies/_targeting_strategy.dm:36
public partial class datum_targeting_strategy_fishing : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/_targeting_strategy.dm:38
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        return Dm.num(HAS_TRAIT(target, TRAIT_FISHING_SPOT));
    }
}

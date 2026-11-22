// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The vars are generated.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/ai/ai.dm:12
    public static bool TARGETING_FACTION_CHECK(datum_targeting_strategy_basic strategy, datum_ai_controller controller, mob_living living_mob, mob_living the_target)
        => Dm.truthy(strategy.ignore_faction) || Dm.truthy(controller.blackboard[BB_ALWAYS_IGNORE_FACTION]) || Dm.truthy(controller.blackboard[BB_TEMPORARILY_IGNORE_FACTION])
            ? Dm.truthy(strategy.invert_faction_check)
            : Dm.truthy(living_mob.faction_check_atom(the_target, exact_match: strategy.check_factions_exactly)) != Dm.truthy(strategy.invert_faction_check);
}

// tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:1
public partial class datum_targeting_strategy_basic : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:19
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        atom the_target = target;
        // checks are ordered cheapest first so invalid targets are rejected before the expensive sight check
        if (isturf(the_target) || the_target == null) // bail out on invalids
            return FALSE;

        datum_ai_controller our_controller = living_mob.ai_controller as datum_ai_controller;

        if (our_controller == null)
            return FALSE;

        if (Dm.num(living_mob.see_invisible) < Dm.num(the_target.invisibility)) //Target's invisible to us, forget it
            return FALSE;

        if (!isturf(living_mob.loc))
            return FALSE;
        if (isturf(the_target.loc) && living_mob.z != the_target.z) // z check will always fail if target is in a mech or pawn is shapeshifted or jaunting
            return FALSE;

        if (isobj(the_target.loc))
        {
            obj container = (obj)the_target.loc;
            if (Dm.truthy(Dm.bitand(container.resistance_flags, INDESTRUCTIBLE)))
                return FALSE;
        }

        if (ismob(the_target)) //Target is in godmode, ignore it.
        {
            if (living_mob.loc == the_target)
                return FALSE; // We've either been eaten or are shapeshifted, let's assume the latter because we're still alive
            if (HAS_TRAIT(the_target, TRAIT_GODMODE))
                return FALSE;
        }

        if (Dm.truthy(vision_range) && get_dist(living_mob, the_target) > Dm.num(vision_range))
            return FALSE;

        if (isliving(the_target)) //Targeting vs living mobs
        {
            if (!Dm.truthy(ignore_target_status))
            {
                mob_living living_target = (mob_living)the_target;
                if (Dm.truthy(custom_faction_check) ? Dm.truthy(faction_check(our_controller, living_mob, living_target)) : TARGETING_FACTION_CHECK(this, our_controller, living_mob, living_target))
                    return FALSE;

                float checked_stat = living_target.stat;
                if (Dm.truthy(our_controller.blackboard[BB_TREAT_UNCONSCIOUS_AS_HARDCRIT]) && IS_UNCONSCIOUS(living_target))
                    checked_stat = max(checked_stat, HARD_CRIT);

                if (checked_stat > Dm.num(our_controller.blackboard[minimum_stat_key]))
                    return FALSE;
                if (Dm.truthy(target_wounded_key) && Dm.truthy(our_controller.blackboard[target_wounded_key]) && living_target.health == living_target.maxHealth)
                    return FALSE;
            }
        }
        else if (ismecha(the_target)) //Targeting vs mechas
        {
            obj_vehicle_sealed_mecha M = (obj_vehicle_sealed_mecha)the_target;
            bool valid_occupant = false;
            foreach (object occupant in Dm.each(M.occupants))
            {
                if (Dm.truthy(await is_valid_target(living_mob, occupant as atom))) //Can we attack any of the occupants?
                {
                    valid_occupant = true;
                    break;
                }
            }
            if (!valid_occupant)
                return FALSE;
        }
        else if (the_target is obj_machinery_porta_turret P) //Cringe turret! kill it!
        {
            if (Dm.truthy(P.in_faction(living_mob))) //Don't attack if the turret is in the same faction
                return FALSE;
            if (Dm.truthy(P.has_cover) && !Dm.truthy(P.raised)) //Don't attack invincible turrets
                return FALSE;
            if (Dm.truthy(Dm.bitand(P.machine_stat, BROKEN))) //Or turrets that are already broken
                return FALSE;
        }
        else //Not a type of thing we can target
            return FALSE;

        if (!Dm.truthy(ignore_sight) && !Dm.truthy(can_see(living_mob, the_target, vision_range))) //Sight check goes last, it's by far the most expensive
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:92
    public override object find_hidden_mobs(mob_living living_mob = null, atom target = null)
    {
        base.find_hidden_mobs(living_mob, target);
        if (target.loc is obj_structure_closet || target.loc is obj_machinery_disposal || target.loc is obj_machinery_sleeper)
            return target.loc;
        return null;
    }

    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:98
    public override async DmTask<object> can_keep_target(mob_living living_mob = null, atom target = null, object range = null, datum_ai_controller controller = null)
    {
        return await is_valid_target(living_mob, target, range, controller);
    }

    /// Returns true if the mob and target share factions.
    /// Slow path for subtypes with custom_faction_check set; everything else uses TARGETING_FACTION_CHECK directly
    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:103
    public virtual object faction_check(datum_ai_controller controller = null, mob_living living_mob = null, mob_living the_target = null)
    {
        return Dm.num(TARGETING_FACTION_CHECK(this, controller, living_mob, the_target));
    }
}

/// Subtype more forgiving for items.
/// Careful, this can go wrong and keep a mob hyper-focused on an item it can't lose aggro on
// tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:108
public partial class datum_targeting_strategy_basic_allow_items : datum_targeting_strategy_basic
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:110
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

/// Only targets with one of the traits of BB_TARGET_ONLY_WITH_TRAITS.
// tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:116
public partial class datum_targeting_strategy_basic_require_traits : datum_targeting_strategy_basic
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:118
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        DmList required_traits = ((datum_ai_controller)living_mob.ai_controller).blackboard[BB_TARGET_ONLY_WITH_TRAITS] as DmList;
        if (!Dm.truthy(length(required_traits)))
            return TRUE;

        foreach (object trait in Dm.each(required_traits))
        {
            if (HAS_TRAIT(target, trait))
                return TRUE;
        }
        return FALSE;
    }
}

/// Subtype which searches for mobs of a size relative to ours
// tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:132
public partial class datum_targeting_strategy_basic_of_size : datum_targeting_strategy_basic
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:134
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        if (!isliving(target))
            return FALSE;
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;

        mob_living mob_target = (mob_living)target;
        if (Dm.truthy(inclusive) && Dm.eq(living_mob.mob_size, mob_target.mob_size))
            return TRUE;
        if (Dm.num(living_mob.mob_size) > Dm.num(mob_target.mob_size))
            return find_smaller;
        return Dm.num(!Dm.truthy(find_smaller));
    }
}

/// Also accepts turfs.
// tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:167
public partial class datum_targeting_strategy_basic_allow_turfs : datum_targeting_strategy_basic
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:169
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        if (isturf(target))
            return TRUE;
        return await base.is_valid_target(living_mob, target, vision_range, controller);
    }
}

/// Subtype which searches for mobs that havent been gutted by megafauna
// tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:175
public partial class datum_targeting_strategy_basic_no_gutted_mobs : datum_targeting_strategy_basic
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:177
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        if (target is not mob_living living_target || Dm.truthy(living_target.has_status_effect(typeof(datum_status_effect_gutted))))
            return FALSE;
        return await base.is_valid_target(living_mob, target, vision_range, controller);
    }
}

/// The exact faction match, sparing the friends list.
// tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:185
public partial class datum_targeting_strategy_basic_exact_match_ignore_friends : datum_targeting_strategy_basic_exact_match
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/basic_targeting_strategy.dm:187
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        DmList friends = ((datum_ai_controller)living_mob.ai_controller).blackboard[BB_FRIENDS_LIST] as DmList;
        if (Dm.truthy(friends) && Dm.@in(target, friends))
            return FALSE;
        return TRUE;
    }
}

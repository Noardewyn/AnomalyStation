// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/unobserved_actor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Blocks certain actions while this mob is being observed by something.
// tg: code/datums/components/unobserved_actor.dm:6
public partial class datum_component_unobserved_actor : datum_component
{
    // tg: code/datums/components/unobserved_actor.dm:14
    [DmParams("unobserved_flags", "affected_actions")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object unobserved_flags = Dm.arg(__extra, 0) ?? NONE;
        await base.Initialize(__extra);
        if (!isliving(parent))
            return ELEMENT_INCOMPATIBLE;
        if (Dm.eq(unobserved_flags, NONE))
            throw Dm.CRASH("No behaviour flags provided to unobserved actor element", "code/datums/components/unobserved_actor.dm", 19);
        this.unobserved_flags = Dm.num(unobserved_flags);
        this.affected_actions = Dm.arg(__extra, 1) as DmList;
        return null;
    }

    // tg: code/datums/components/unobserved_actor.dm:23
    public override async DmTask RegisterWithParent()
    {
        if (Dm.truthy(Dm.bitand(unobserved_flags, NO_OBSERVED_MOVEMENT)))
        {
            RegisterSignal(parent, COMSIG_MOVABLE_PRE_MOVE, nameof(on_tried_move));
            RegisterSignal(parent, COMSIG_ATOM_PRE_DIR_CHANGE, nameof(on_tried_turn));
        }
        if (Dm.truthy(Dm.bitand(unobserved_flags, NO_OBSERVED_ACTIONS)))
        {
            RegisterSignal(parent, COMSIG_MOB_ABILITY_STARTED, nameof(on_tried_ability));
            RegisterSignal(parent, COMSIG_MOB_BEFORE_SPELL_CAST, nameof(on_tried_spell));
        }
        if (Dm.truthy(Dm.bitand(unobserved_flags, NO_OBSERVED_ATTACKS)))
            RegisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_tried_attack));
    }

    // tg: code/datums/components/unobserved_actor.dm:33
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_MOVABLE_PRE_MOVE, COMSIG_ATOM_PRE_DIR_CHANGE, COMSIG_MOB_ABILITY_STARTED, COMSIG_MOB_BEFORE_SPELL_CAST, COMSIG_HOSTILE_PRE_ATTACKINGTARGET));
        await base.UnregisterFromParent(__extra);
    }

    /// Called when the mob tries to move
    // tg: code/datums/components/unobserved_actor.dm:44
    [SignalHandler]
    public virtual object on_tried_move(mob_living source = null)
    {
        if (!Dm.truthy(check_if_seen(source)))
            return null;
        return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
    }

    /// Called when the mob tries to change direction
    // tg: code/datums/components/unobserved_actor.dm:51
    [SignalHandler]
    public virtual object on_tried_turn(mob_living source = null)
    {
        if (!Dm.truthy(check_if_seen(source)))
            return null;
        return COMPONENT_ATOM_BLOCK_DIR_CHANGE;
    }

    /// Called when the mob tries to use an ability
    // tg: code/datums/components/unobserved_actor.dm:58
    [SignalHandler]
    public virtual object on_tried_ability(mob_living source = null, datum action = null)
    {
        if (!Dm.truthy(check_if_seen(source)))
            return null;
        if (affected_actions != null && !Dm.@in(action.type, affected_actions))
            return null;
        return COMPONENT_BLOCK_ABILITY_START;
    }

    /// Called when the mob tries to cast a spell
    // tg: code/datums/components/unobserved_actor.dm:67
    [SignalHandler]
    public virtual object on_tried_spell(mob_living source = null, datum action = null)
    {
        if (!Dm.truthy(check_if_seen(source)))
            return null;
        if (affected_actions != null && !Dm.@in(action.type, affected_actions))
            return null;
        return SPELL_CANCEL_CAST;
    }

    /// Called when the mob tries to attack
    // tg: code/datums/components/unobserved_actor.dm:76
    [SignalHandler]
    public virtual object on_tried_attack(mob_living source = null)
    {
        if (!Dm.truthy(check_if_seen(source)))
            return null;
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    /// Checks if the mob is visible to something else, and provides a balloon alert of feedback if appropriate.
    // tg: code/datums/components/unobserved_actor.dm:83
    public virtual object check_if_seen(mob_living source = null)
    {
        object observed = can_be_seen(source);
        if (Dm.truthy(observed) && message_cooldown <= world.time) // COOLDOWN_FINISHED, inline (rules 11.1)
        {
            source.balloon_alert(source, "something can see you!");
            message_cooldown = world.time + 1 * SECONDS; // COOLDOWN_START
        }
        return observed;
    }

    /// Returns true if you can be seen by something.
    // tg: code/datums/components/unobserved_actor.dm:94
    public virtual object can_be_seen(mob_living source = null)
    {
        turf my_turf = get_turf(source);
        // Check for darkness
        if (my_turf.lighting_object != null && Dm.truthy(my_turf.check_lumcount_below(0.1f))) // No one can see us in the darkness, right?
            return FALSE;

        // We aren't in darkness, loop for viewers.
        foreach (mob_living mob_target in Dm.each<mob_living>(oview(7, my_turf))) // They probably cannot see us if we cannot see them... can they?
        {
            if (mob_target.client != null && !Dm.truthy(mob_target.is_blind()) && !HAS_TRAIT(mob_target, TRAIT_UNOBSERVANT))
                return TRUE;
        }
        foreach (obj_vehicle_sealed_mecha mecha_mob_target in Dm.each<obj_vehicle_sealed_mecha>(oview(7, my_turf)))
        {
            foreach (mob mechamob_target in Dm.each_cast<mob>(mecha_mob_target.occupants))
            {
                if (mechamob_target.client != null && !Dm.truthy((mechamob_target as mob_living)?.is_blind()) && !HAS_TRAIT(mechamob_target, TRAIT_UNOBSERVANT))
                    return TRUE;
            }
        }

        return FALSE;
    }
}

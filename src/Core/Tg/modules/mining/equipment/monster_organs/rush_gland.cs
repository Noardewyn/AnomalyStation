// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/monster_organs/rush_gland.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * On use in hand, makes you run faster for a bit and ignore injury movement decrease.
 * On use when implanted, run for longer and ignore all negative movement. Automatically triggers if health is low (to escape).
 */
// tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:8
public partial class obj_item_organ_monster_core_rush_gland : obj_item_organ_monster_core
{
    /// Health under which implanted gland will automatically activate
    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:2  #define HEALTH_DANGER_ZONE 30 (file-local)
    private const float HEALTH_DANGER_ZONE = 30;

    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:19
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (owner.health <= HEALTH_DANGER_ZONE)
            await trigger_organ_action();
    }

    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:24
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_GOLIATH_TENTACLED_GRABBED, nameof(trigger_organ_action_on_sig));
        RegisterSignal(organ_owner, COMSIG_TENDRIL_TENTACLED_GRABBED, nameof(trigger_organ_action_on_sig));
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:29
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(organ_owner as datum, COMSIG_GOLIATH_TENTACLED_GRABBED);
    }

    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:33
    public override void on_triggered_internal()
    {
        Waiting.NoSleep(owner.apply_status_effect(typeof(datum_status_effect_lobster_rush_extended)));
    }

    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:36
    [SignalHandler]
    public virtual object trigger_organ_action_on_sig(datum source = null)
    {
        Waiting.Detach(trigger_organ_action());
        return COMPONENT_GOLIATH_CANCEL_TENTACLE_GRAB;
    }
}

/**
 * Status effect: Makes you run faster and ignore damage speed penalties for a short duration.
 * If you run into a wall indoors you will fall over and lose the buff.
 * If you run into someone you both fall over.
 */
// tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:47
public partial class datum_status_effect_lobster_rush : datum_status_effect
{
    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:60
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(on_move));
        RegisterSignal(owner, COMSIG_MOVABLE_BUMP, nameof(on_bump));
        ADD_TRAIT(owner, TRAIT_TENTACLE_IMMUNE, TRAIT_STATUS_EFFECT(id));
        owner.add_movespeed_mod_immunities(id, typeof(datum_movespeed_modifier_damage_slowdown));
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_lobster_rush));
        to_chat(owner, span_notice(I18n.T("You feel your blood pumping!")));
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:69
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MOVABLE_BUMP));
        REMOVE_TRAIT(owner, TRAIT_TENTACLE_IMMUNE, TRAIT_STATUS_EFFECT(id));
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_lobster_rush));
        owner.remove_movespeed_mod_immunities(id, typeof(datum_movespeed_modifier_damage_slowdown));
        to_chat(owner, span_notice(I18n.T("Your pulse returns to normal.")));
        return dot;
    }

    /// Spawn an afterimage every other step, because every step was too many
    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:78
    [SignalHandler]
    public virtual void on_move(datum source = null, atom old_loc = null, object dir = null)
    {
        if (!isturf(old_loc) || !isturf(owner.loc))
            return;
        if (!Dm.truthy(spawned_last_move))
            Dm.@new<obj_effect_temp_visual_decoy_fading>(old_loc, owner, 150);
        spawned_last_move = Dm.truthy(spawned_last_move) ? FALSE : TRUE;
    }

    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:86
    [SignalHandler]
    public virtual void on_bump(mob_living source = null, atom target = null)
    {
        if (!Dm.truthy(target.density))
            return;
        if (Dm.truthy(lavaland_equipment_pressure_check(get_turf(source))))
            return;
        smack_into(source);
        source.visible_message(span_warning(I18n.T("[] crashes into []!", source, target)));
        if (isliving(target))
            smack_into(target as mob_living);
        qdel(this);
    }

    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:98
    public virtual void smack_into(mob_living target = null)
    {
        Waiting.NoSleep(target.Knockdown(2 * SECONDS));
        Waiting.NoSleep(target.apply_damage(30, STAMINA));
        Waiting.NoSleep(target.apply_damage(10, BRUTE, spread_damage: TRUE));
    }
}

/// You get a longer buff if you take the time to implant it in yourself
// tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:104
public partial class datum_status_effect_lobster_rush_extended : datum_status_effect_lobster_rush
{
    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:107
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        ADD_TRAIT(owner, TRAIT_IGNORESLOWDOWN, TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/rush_gland.dm:111
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        REMOVE_TRAIT(owner, TRAIT_IGNORESLOWDOWN, TRAIT_STATUS_EFFECT(id));
        return dot;
    }
}

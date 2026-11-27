// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/*

BLOOD-DRUNK MINER

Effectively a highly aggressive miner, the blood-drunk miner has very few attacks but compensates by being highly aggressive in its AI as well as damage.

When the blood-drunk miner dies, it leaves behind the cleaving saw it was using and its kinetic accelerator.

Difficulty: Medium

*/
// tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:13
public partial class mob_living_basic_boss_blood_drunk_miner : mob_living_basic_boss
{
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, INNATE_TRAIT);
        RegisterSignal(this, COMSIG_MOVABLE_PRE_MOVE, nameof(on_premove));
        Waiting.NoSleep(AddElement(typeof(datum_element_relay_attackers)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), Dm.named("footstep_type", FOOTSTEP_MOB_HEAVY)));

        RegisterSignal(this, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(attack_override));

        miner_saw = Dm.@new<obj_item_melee_cleaving_saw_miner>(this);
        RegisterSignal(miner_saw, COMSIG_PREQDELETED, nameof(on_saw_deleted));
        RegisterSignal(miner_saw, COMSIG_MOVABLE_PRE_MOVE, nameof(on_saw_premove));

        grant_actions_by_list(get_innate_actions() as DmList);
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_BDM_RANGED_ATTACK_COOLDOWN, ranged_attack_cooldown_duration);

        RegisterSignal(this, COMSIG_LIVING_DROP_LOOT, nameof(death_effect));

        Waiting.NoSleep(AddComponent(typeof(datum_component_boss_music), Dm.resource("sound/music/boss/bdm_boss.ogg"), COMSIG_AI_BLACKBOARD_KEY_SET(BB_CURRENT_TARGET)));
        return dot;
    }

    /// Block deletion of their saw under normal circumstances. It is fused to their hands as far as we're concerned.
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:79
    [SignalHandler]
    public virtual object on_saw_deleted(datum source = null, object force = null)
    {
        if (!Dm.truthy(force))
            return TRUE;
        return null;
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:85
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(miner_saw, Dm.list(COMSIG_PREQDELETED, COMSIG_MOVABLE_PRE_MOVE)); // unblock deletion, we are dead.
        QDEL_NULL(ref miner_saw);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:90
    public override object get_hud_x_offset()
    {
        return 0;
    }

    /// Returns a list of innate actions for the blood-drunk miner.
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:94
    public virtual object get_innate_actions()
    {
        DmList innate_abilities = Dm.list(
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_charge_basic_charge_blood_drunk_miner), BB_BDM_DASH_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_kinetic_accelerator), BB_BDM_KINETIC_ACCELERATOR_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_dash_attack), BB_BDM_DASH_ATTACK_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_transform_weapon), BB_BDM_TRANSFORM_WEAPON_ABILITY));
        return innate_abilities;
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:103
    public virtual async DmTask transform_saw()
    {
        await miner_saw.attack_self(this);
        bool saw_open = HAS_TRAIT(miner_saw, TRAIT_TRANSFORM_ACTIVE);
        rapid_melee_hits = saw_open ? 3 : 5;
        icon_state = Dm.text("[][]", base_icon_state, saw_open ? "_transformed" : "");
        icon_living = Dm.text("[][]", base_icon_state, saw_open ? "_transformed" : "");
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:110
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        datum_action_cooldown_mob_cooldown dash_ability = (ai_controller as datum_ai_controller).blackboard[BB_BDM_DASH_ABILITY] as datum_action_cooldown_mob_cooldown;
        if (Dm.truthy(Waiting.NoSleep(dash_ability.Trigger(null, null, target))))
            return FALSE;
        return base.ex_act(severity, target, __extra);
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:116
    public override async DmTask do_attack_animation(atom attacked_atom = null, object visual_effect_icon = null, obj_item used_item = null, object no_effect = null, float? fov_effect = null, object item_animation_override = null)
    {
        if (used_item == null && !isturf(attacked_atom))
            used_item = miner_saw;
        await base.do_attack_animation(attacked_atom, visual_effect_icon, used_item, no_effect, fov_effect, item_animation_override);
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:121
    public override async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        float adjustment_amount = Dm.num(amount) * 0.1f;
        if (world.time + adjustment_amount > Dm.num(next_move))
            changeNext_move(adjustment_amount); //attacking it interrupts it attacking, but only briefly
        await base.on_damage_loss_changed(amount, updating_health, forced, damage_type);
    }

    /// Handles spawning a death effect when the blood-drunk miner dies. Tied to COMSIG_LIVING_DROP_LOOT so the timings of spawning the effect should approximately work out with the loot appearing.
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:128
    [SignalHandler]
    public virtual void death_effect(datum source = null, DmList spawn_loot = null, object gibbed = null)
    {
        Dm.@new<obj_effect_temp_visual_dir_setting_miner_death>(loc, dir);
    }

    /// Prevent running into a chasm and other undesirable movements.
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:133
    [SignalHandler]
    public virtual object on_premove(datum source = null, atom new_location = null)
    {
        if (new_location == null)
            return null;

        if (!Dm.eq(new_location.z, z))
            return null;

        turf_open locus = get_turf(new_location) as turf_open;
        if (!ischasm(locus) || Dm.truthy(locus.can_cross_safely(this))) // if it's not a chasm we don't care to check the proc.
            return null;

        return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
    }

    /// Prevent their saw from being moved at all
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:146
    [SignalHandler]
    public virtual object on_saw_premove(datum source = null, atom new_location = null)
    {
        return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
    }

    /// Handles our attack behavior when we're doing melee attacks to override the default basic melee attack behavior when our AI calls upon us to use it.
    /// Namely, we just use the miner saw to rapidly hit the target multiple times
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:151
    [SignalHandler]
    public virtual object attack_override(mob_living source = null, atom target = null, object proximity = null, object modifiers = null)
    {
        if (!isliving(target))
            return null;

        mob_living victim = (mob_living)target;
        if (Dm.truthy(should_devour(victim)))
        {
            Waiting.NoSleep(devour(victim));
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        Waiting.NoSleep(do_chain_attack(victim, modifiers));
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:165
    public virtual async DmTask do_chain_attack(mob_living victim = null, object modifiers = null, float? sequence_hit = null)
    {
        sequence_hit ??= 1;
        if (!Dm.truthy(await Adjacent(victim)))
        {
            post_attack_effects(victim, modifiers as DmList);
            return;
        }

        changeNext_move(CLICK_CD_MELEE);
        victim.visible_message(
            span_danger(I18n.T("[] slashes at [] with [] cleaving saw!", this, victim, p_their())),
            span_userdanger(I18n.T("You are slashed at by []'s cleaving saw!", this)));

        float delay = HAS_TRAIT(miner_saw, TRAIT_TRANSFORM_ACTIVE) ? 0.5f * SECONDS : 0.3f * SECONDS;
        await apply_status_effect(typeof(datum_status_effect_saw_slashes_slowdown), delay);
        Waiting.Detach(miner_saw.melee_attack_chain(this, victim, modifiers as DmList)); // INVOKE_ASYNC

        if (sequence_hit >= rapid_melee_hits)
        {
            post_attack_effects(victim, modifiers as DmList);
            return;
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(do_chain_attack), victim, modifiers, sequence_hit + 1), delay, file: "code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm", line: 184);
    }

    /// Hook for potential additional behaviors after attacking
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:205
    public virtual void post_attack_effects(mob_living victim = null, DmList modifiers = null)
    {
        return;
    }
}

// tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:186
public partial class datum_status_effect_saw_slashes_slowdown : datum_status_effect
{
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:191
    [DmParams("new_owner", "new_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        duration = Dm.num(Dm.arg(__extra, 0));
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:195
    [DmParams("effect", "new_duration")]
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        duration = Dm.num(Dm.arg(__extra, 0));
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:198
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_saw_slashes_slowdown));
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/_blood_drunk_miner.dm:202
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_saw_slashes_slowdown));
        return dot;
    }
}

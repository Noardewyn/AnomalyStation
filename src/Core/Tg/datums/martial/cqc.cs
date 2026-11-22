// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/cqc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): CQC (its five combos, the leg sweep, the neck snap, the chokehold and the throw mode block) and
// the chef's Close Quarters Cooking, whole. The var blocks are generated (D111).

// tg: code/datums/martial/cqc.dm:7
public partial class datum_martial_art_cqc : datum_martial_art
{
    // tg: code/datums/martial/cqc.dm:1-5  #define SLAM_COMBO "GH" ... (file-local, #undef at :424-428)
    private const string SLAM_COMBO = "GH";
    private const string KICK_COMBO = "HH";
    private const string RESTRAIN_COMBO = "GG";
    private const string PRESSURE_COMBO = "DG";
    private const string CONSECUTIVE_COMBO = "DDH";

    // tg: code/datums/martial/cqc.dm:18
    public override async DmTask activate_style(mob_living new_holder = null)
    {
        await base.activate_style(new_holder);
        RegisterSignal(new_holder, COMSIG_ATOM_ATTACKBY, nameof(on_attackby));
        RegisterSignal(new_holder, COMSIG_LIVING_CHECK_BLOCK, nameof(check_block));
    }

    // tg: code/datums/martial/cqc.dm:23
    public override async DmTask deactivate_style(mob_living remove_from = null)
    {
        UnregisterSignal(remove_from, Dm.list(COMSIG_ATOM_ATTACKBY, COMSIG_LIVING_CHECK_BLOCK));
        await base.deactivate_style(remove_from);
    }

    ///Signal from getting attacked with an item, for a special interaction with touch spells
    // tg: code/datums/martial/cqc.dm:28
    [SignalHandler]
    public virtual object on_attackby(mob_living cqc_user = null, obj_item attack_weapon = null, mob attacker = null, DmList modifiers = null)
    {
        if (attack_weapon is not obj_item_melee_touch_attack touch_weapon)
            return null;
        if (!Dm.truthy(can_use(cqc_user)))
            return null;
        cqc_user.visible_message(
            span_danger(I18n.T("[] twists []'s arm, sending their [] back towards them!", cqc_user, attacker, attack_weapon)),
            span_userdanger(I18n.T("Making sure to avoid []'s [], you twist their arm to send it right back at them!", attacker, attack_weapon)));
        var touch_spell = touch_weapon.spell_which_made_us?.resolve() as datum_action_cooldown_spell_touch;
        if (touch_spell == null)
            return null;
        // INVOKE_ASYNC(touch_spell, TYPE_PROC_REF(/datum/action/cooldown/spell/touch, do_hand_hit), touch_weapon, attacker, attacker)
        Dm.Deferred("code/datums/martial/cqc.dm:43", "on_attackby: the touch spell's do_hand_hit on its caster (the touch spells are not ported)");
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/martial/cqc.dm:46
    [SignalHandler]
    public virtual object check_block(mob_living cqc_user = null, atom_movable hitby = null, object damage = null, object attack_text = null, object attack_type = null, params object[] __extra)
    {
        if (!Dm.truthy(can_use(cqc_user)) || !Dm.truthy(cqc_user.throw_mode) || Dm.truthy(INCAPACITATED_IGNORING(cqc_user, INCAPABLE_GRAB)))
            return NONE;
        if (Dm.eq(attack_type, PROJECTILE_ATTACK))
            return NONE;

        string blocking_text = "block";
        string blocking_text_s = "blocks";
        float potential_block_chance = block_chance;

        if (Dm.eq(attack_type, OVERWHELMING_ATTACK))
        {
            blocking_text = "dodge";
            blocking_text_s = "dodges";
            potential_block_chance = clamp(round(potential_block_chance / (Dm.eq(attack_type, OVERWHELMING_ATTACK) ? 2 : 1), 1), 0, 100);
        }

        if (!prob(potential_block_chance))
            return NONE;

        var attacker = get(hitby, typeof(mob_living)) as mob_living; // GET_ASSAILANT(hitby)
        if (attacker != null && Dm.truthy(Waiting.NoSleep(cqc_user.Adjacent(attacker))))
        {
            cqc_user.visible_message(
                span_danger(I18n.T("[] [] [] and twists []'s arm behind [] back!", cqc_user, blocking_text_s, attack_text, attacker, attacker.p_their())),
                span_userdanger(I18n.T("You [] []!", blocking_text, attack_text)));
            Waiting.NoSleep(attacker.Stun(4 * SECONDS));
        }
        else
        {
            cqc_user.visible_message(
                span_danger(I18n.T("[] [] []!", cqc_user, blocking_text_s, attack_text)),
                span_userdanger(I18n.T("You [] []!", blocking_text, attack_text)));
        }
        return SUCCESSFUL_BLOCK;
    }

    // tg: code/datums/martial/cqc.dm:81
    public override async DmTask reset_streak(mob_living new_target = null, float? update_icon = null)
    {
        if (!IS_WEAKREF_OF(new_target, restraining_mob))
            restraining_mob = null;
        await base.reset_streak(new_target, update_icon);
    }

    // tg: code/datums/martial/cqc.dm:86
    public virtual async DmTask<object> check_streak(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(findtext(streak, SLAM_COMBO)))
        {
            await reset_streak();
            return await Slam(attacker, defender);
        }
        if (Dm.truthy(findtext(streak, KICK_COMBO)))
        {
            await reset_streak();
            return await Kick(attacker, defender);
        }
        if (Dm.truthy(findtext(streak, RESTRAIN_COMBO)))
        {
            await reset_streak();
            return await Restrain(attacker, defender);
        }
        if (Dm.truthy(findtext(streak, PRESSURE_COMBO)))
        {
            await reset_streak();
            return await Pressure(attacker, defender);
        }
        if (Dm.truthy(findtext(streak, CONSECUTIVE_COMBO)))
        {
            await reset_streak();
            return await Consecutive(attacker, defender);
        }
        return FALSE;
    }

    // tg: code/datums/martial/cqc.dm:104
    public virtual async DmTask<object> Slam(mob_living attacker = null, mob_living defender = null)
    {
        if (!Dm.eq(defender.body_position, STANDING_UP))
            return FALSE;

        await attacker.do_attack_animation(defender);
        defender.visible_message(
            span_danger(I18n.T("[] slams [] into the ground!", attacker, defender)),
            span_userdanger(I18n.T("You're slammed into the ground by []!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            null,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You slam [] into the ground!", defender)));
        playsound(attacker, "sound/items/weapons/slam.ogg", 50, TRUE, -1);
        await defender.apply_damage(10, BRUTE);
        await defender.Paralyze(12 * SECONDS);
        log_combat(attacker, defender, "slammed (CQC)");
        return TRUE;
    }

    // tg: code/datums/martial/cqc.dm:123
    public virtual async DmTask<object> Kick(mob_living attacker = null, mob_living defender = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(defender))
            return FALSE;

        await attacker.do_attack_animation(defender);
        if (Dm.eq(defender.body_position, LYING_DOWN) && !IS_UNCONSCIOUS(defender) && Dm.num(defender.get_stamina_loss()) >= 100)
        {
            log_combat(attacker, defender, "knocked out (Head kick)(CQC)");
            defender.visible_message(
                span_danger(I18n.T("[] kicks []'s head, knocking [] out!", attacker, defender, defender.p_them())),
                span_userdanger(I18n.T("You're knocked unconscious by []!", attacker)),
                span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                null,
                attacker);
            to_chat(attacker, span_danger(I18n.T("You kick []'s head, knocking [] out!", defender, defender.p_them())));
            playsound(attacker, "sound/items/weapons/genhit1.ogg", 50, TRUE, -1);

            float helmet_protection = Dm.num(defender.run_armor_check(BODY_ZONE_HEAD, MELEE));
            await defender.apply_effect(20 * SECONDS, EFFECT_KNOCKDOWN, helmet_protection);
            await defender.apply_effect(10 * SECONDS, EFFECT_UNCONSCIOUS, helmet_protection);
            await defender.adjust_organ_loss(ORGAN_SLOT_BRAIN, 15, 150);
        }
        else
        {
            defender.visible_message(
                span_danger(I18n.T("[] kicks [] back!", attacker, defender)),
                span_userdanger(I18n.T("You're kicked back by []!", attacker)),
                span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                COMBAT_MESSAGE_RANGE,
                attacker);
            to_chat(attacker, span_danger(I18n.T("You kick [] back!", defender)));
            playsound(attacker, "sound/items/weapons/cqchit1.ogg", 50, TRUE, -1);
            var throw_target = get_edge_target_turf(defender, attacker.dir) as atom;
            await defender.throw_at(throw_target, 1, 14, attacker);
            await defender.apply_damage(10, Dm.str(attacker.get_attack_type()));
            if (Dm.eq(defender.body_position, LYING_DOWN) && !IS_UNCONSCIOUS(defender))
                await defender.adjust_stamina_loss(45);
            log_combat(attacker, defender, "kicked (CQC)");
        }

        return TRUE;
    }

    // tg: code/datums/martial/cqc.dm:164
    public virtual async DmTask<object> Pressure(mob_living attacker = null, mob_living defender = null)
    {
        await attacker.do_attack_animation(defender);
        log_combat(attacker, defender, "pressured (CQC)");
        defender.visible_message(
            span_danger(I18n.T("[] punches []'s neck!", attacker, defender)),
            span_userdanger(I18n.T("Your neck is punched by []!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You punch []'s neck!", defender)));
        await defender.adjust_stamina_loss(60);
        playsound(attacker, "sound/items/weapons/cqchit1.ogg", 50, TRUE, -1);
        return TRUE;
    }

    // tg: code/datums/martial/cqc.dm:179
    public virtual async DmTask<object> Restrain(mob_living attacker = null, mob_living defender = null)
    {
        if (restraining_mob?.resolve() != null)
            return FALSE;
        if (IS_UNCONSCIOUS_OR_CRIT(defender))
            return FALSE;

        log_combat(attacker, defender, "restrained (CQC)");
        defender.visible_message(
            span_warning(I18n.T("[] locks [] into a restraining position!", attacker, defender)),
            span_userdanger(I18n.T("You're locked into a restraining position by []!", attacker)),
            span_hear(I18n.T("You hear shuffling and a muffled groan!")),
            null,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You lock [] into a restraining position!", defender)));
        await defender.adjust_stamina_loss(20);
        await defender.Stun(10 * SECONDS);
        restraining_mob = WEAKREF(defender) as datum_weakref;
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_restraining_mob)), 5 * SECONDS, TIMER_UNIQUE, file: "code/datums/martial/cqc.dm", line: 197);
        return TRUE;
    }

    // tg: code/datums/martial/cqc.dm:197  VARSET_CALLBACK(src, restraining_mob, null)
    public virtual void clear_restraining_mob() => restraining_mob = null;

    // tg: code/datums/martial/cqc.dm:200
    public virtual async DmTask<object> Consecutive(mob_living attacker = null, mob_living defender = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(defender))
            return FALSE;

        await attacker.do_attack_animation(defender);
        log_combat(attacker, defender, "consecutive CQC'd (CQC)");
        defender.visible_message(
            span_danger(I18n.T("[] strikes []'s abdomen, neck and back consecutively", attacker, defender)),
            span_userdanger(I18n.T("Your abdomen, neck and back are struck consecutively by []!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You strike []'s abdomen, neck and back consecutively!", defender)));
        playsound(defender, "sound/items/weapons/cqchit2.ogg", 50, TRUE, -1);
        var held_item = defender.get_active_held_item() as obj_item;
        if (held_item != null && Dm.truthy(await defender.temporarilyRemoveItemFromInventory(held_item)))
            await attacker.put_in_hands(held_item);
        await defender.adjust_stamina_loss(50);
        await defender.apply_damage(25, Dm.str(attacker.get_attack_type()));
        return TRUE;
    }

    // tg: code/datums/martial/cqc.dm:222
    public override async DmTask<object> grab_act(mob_living attacker = null, mob_living defender = null)
    {
        if (attacker == defender)
            return MARTIAL_ATTACK_INVALID;
        if (Dm.truthy(await defender.check_block(attacker, 0, attacker.name, UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;

        await add_to_streak("G", defender);
        if (Dm.truthy(await check_streak(attacker, defender))) //if a combo is made no grab upgrade is done
            return MARTIAL_ATTACK_SUCCESS;
        if (Dm.eq(attacker.body_position, LYING_DOWN))
            return MARTIAL_ATTACK_INVALID;

        object old_grab_state = attacker.grab_state;
        await defender.grabbedby(attacker, TRUE);
        if (Dm.eq(old_grab_state, GRAB_PASSIVE))
        {
            await defender.drop_all_held_items();
            await attacker.setGrabState(GRAB_AGGRESSIVE); //Instant aggressive grab if on grab intent
            log_combat(attacker, defender, "grabbed", addition: "aggressively");
            defender.visible_message(
                span_warning(I18n.T("[] violently grabs []!", attacker, defender)),
                span_userdanger(I18n.T("You're grabbed violently by []!", attacker)),
                span_hear(I18n.T("You hear sounds of aggressive fondling!")),
                COMBAT_MESSAGE_RANGE,
                attacker);
            to_chat(attacker, span_danger(I18n.T("You violently grab []!", defender)));
        }
        return MARTIAL_ATTACK_SUCCESS;
    }

    // tg: code/datums/martial/cqc.dm:250
    public override async DmTask<object> harm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.eq(attacker.grab_state, GRAB_KILL)
            && Dm.eq(attacker.zone_selected, BODY_ZONE_HEAD)
            && attacker.pulling == defender
            && !Dm.eq(defender.stat, DEAD))
        {
            var head = defender.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
            if (head != null)
            {
                playsound(defender, "sound/effects/wounds/crack1.ogg", 100);
                defender.visible_message(
                    span_danger(I18n.T("[] snaps the neck of []!", attacker, defender)),
                    span_userdanger(I18n.T("Your neck is snapped by []!", attacker)),
                    span_hear(I18n.T("You hear a sickening snap!")),
                    ignored_mobs: attacker);
                to_chat(attacker, span_danger(I18n.T("In a swift motion, you snap the neck of []!", defender)));
                log_combat(attacker, defender, "snapped neck");
                await defender.apply_damage(100, BRUTE, BODY_ZONE_HEAD, wound_bonus: CANT_WOUND);
                if (!HAS_TRAIT(defender, TRAIT_NODEATH))
                {
                    await defender.death();
                    defender.investigate_log(Dm.text("has had [] neck snapped by [].", defender.p_their(), attacker), INVESTIGATE_DEATHS);
                }
                return MARTIAL_ATTACK_SUCCESS;
            }
        }

        if (Dm.truthy(await defender.check_block(attacker, 10, attacker.name, UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;

        if (Dm.truthy(attacker.resting) && !Dm.eq(defender.stat, DEAD) && Dm.eq(defender.body_position, STANDING_UP))
        {
            defender.visible_message(
                span_danger(I18n.T("[] leg sweeps []!", attacker, defender)),
                span_userdanger(I18n.T("Your legs are sweeped by []!", attacker)),
                span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                null,
                attacker);
            to_chat(attacker, span_danger(I18n.T("You leg sweep []!", defender)));
            playsound(attacker, "sound/effects/hit_kick.ogg", 50, TRUE, -1);
            await attacker.do_attack_animation(defender);
            await defender.apply_damage(10, BRUTE);
            await defender.Knockdown(5 * SECONDS);
            log_combat(attacker, defender, "sweeped (CQC)");
            await reset_streak();
            return MARTIAL_ATTACK_SUCCESS;
        }

        await add_to_streak("H", defender);
        if (Dm.truthy(await check_streak(attacker, defender)))
            return MARTIAL_ATTACK_SUCCESS;
        await attacker.do_attack_animation(defender);
        string picked_hit_type = Dm.str(pick("CQC", "Big Boss"));
        float bonus_damage = 13;
        if (Dm.eq(defender.body_position, LYING_DOWN))
        {
            bonus_damage += 5;
            picked_hit_type = Dm.str(pick("kick", "stomp"));
        }
        await defender.apply_damage(bonus_damage, BRUTE);

        playsound(defender, picked_hit_type == "kick" || picked_hit_type == "stomp" ? "sound/items/weapons/cqchit2.ogg" : "sound/items/weapons/cqchit1.ogg", 50, TRUE, -1);

        defender.visible_message(
            span_danger(I18n.T("[] []ed []!", attacker, picked_hit_type, defender)),
            span_userdanger(I18n.T("You're []ed by []!", picked_hit_type, attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You [] []!", picked_hit_type, defender)));
        log_combat(attacker, defender, Dm.text("attacked ([]'d)(CQC)", picked_hit_type));
        return MARTIAL_ATTACK_SUCCESS;
    }

    // tg: code/datums/martial/cqc.dm:317
    public override async DmTask<object> disarm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(await defender.check_block(attacker, 0, attacker.name, UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;

        await add_to_streak("D", defender);
        if (Dm.truthy(await check_streak(attacker, defender)))
            return MARTIAL_ATTACK_SUCCESS;

        if (IS_WEAKREF_OF(attacker.pulling, restraining_mob))
        {
            log_combat(attacker, defender, "disarmed (CQC)", addition: "knocked out (CQC Chokehold)");
            defender.visible_message(
                span_danger(I18n.T("[] puts [] into a chokehold!", attacker, defender)),
                span_userdanger(I18n.T("You're put into a chokehold by []!", attacker)),
                span_hear(I18n.T("You hear shuffling and a muffled groan!")),
                null,
                attacker);
            to_chat(attacker, span_danger(I18n.T("You put [] into a chokehold!", defender)));
            await defender.SetSleeping(40 * SECONDS);
            restraining_mob = null;
            if (Dm.num(attacker.grab_state) < GRAB_NECK && !HAS_TRAIT(attacker, TRAIT_PACIFISM))
                await attacker.setGrabState(GRAB_NECK);
            return MARTIAL_ATTACK_SUCCESS;
        }

        await attacker.do_attack_animation(defender, ATTACK_EFFECT_DISARM);
        if (prob(65) && (!IS_UNCONSCIOUS_OR_CRIT(defender) || !Dm.truthy(defender.IsParalyzed()) || restraining_mob?.resolve() == null))
        {
            var disarmed_item = defender.get_active_held_item() as obj_item;
            if (disarmed_item != null && Dm.truthy(await defender.temporarilyRemoveItemFromInventory(disarmed_item)))
            {
                await defender.dropItemToGround(disarmed_item);
                if (isturf(disarmed_item.loc)) //If it fell on the ground we can take it, otherwise assume it's attached to something.
                    await attacker.put_in_hands(disarmed_item);
                else
                    disarmed_item = null;
            }
            else
            {
                disarmed_item = null;
            }

            defender.visible_message(
                span_danger(I18n.T("[] strikes []'s jaw with their hand[]!", attacker, defender, disarmed_item != null ? Dm.text(", disarming [] of []", defender.p_them(), disarmed_item) : "")),
                span_userdanger(I18n.T("[] strikes your jaw,[] leaving you disoriented!", attacker, disarmed_item != null ? Dm.text(" disarming you of [] and", disarmed_item) : "")),
                span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                COMBAT_MESSAGE_RANGE,
                attacker);
            to_chat(attacker, span_danger(I18n.T("You strike []'s jaw,[] leaving [] disoriented!", defender, disarmed_item != null ? Dm.text(" disarming [] of [] and", defender.p_them(), disarmed_item) : "", defender.p_them())));
            playsound(defender, "sound/items/weapons/cqchit1.ogg", 50, TRUE, -1);
            await defender.set_jitter_if_lower(4 * SECONDS);
            await defender.apply_damage(5, Dm.str(attacker.get_attack_type()));
            log_combat(attacker, defender, "disarmed (CQC)", addition: disarmed_item != null ? Dm.text("(disarmed of [])", disarmed_item) : null);
            return MARTIAL_ATTACK_SUCCESS;
        }

        defender.visible_message(
            span_danger(I18n.T("[] fails to disarm []!", attacker, defender)),
            span_userdanger(I18n.T("You're nearly disarmed by []!", attacker)),
            span_hear(I18n.T("You hear a swoosh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);
        to_chat(attacker, span_warning(I18n.T("You fail to disarm []!", defender)));
        playsound(defender, "sound/items/weapons/punchmiss.ogg", 25, TRUE, -1);
        log_combat(attacker, defender, "failed to disarm (CQC)");
        return MARTIAL_ATTACK_FAIL;
    }

    // tg: code/datums/martial/cqc.dm:380
    public override object get_style_help()
    {
        DmList dot = Dm.list();

        Dm.append(dot, I18n.T("<b><i>You try to remember some of the basics of CQC.</i></b>"));

        Dm.append(dot, I18n.T("[]: Grab Punch. Slam opponent into the ground, knocking them down.", span_notice("Slam")));
        Dm.append(dot, I18n.T("[]: Punch Punch. Knocks opponent away. Knocks out stunned opponents and does stamina damage.", span_notice("CQC Kick")));
        Dm.append(dot, I18n.T("[]: Grab Grab. Locks opponents into a restraining position, disarm to knock them out with a chokehold.", span_notice("Restrain")));
        Dm.append(dot, I18n.T("[]: Shove Grab. Decent stamina damage.", span_notice("Pressure")));
        Dm.append(dot, I18n.T("[]: Shove Shove Punch. Mainly offensive move, huge damage and decent stamina damage.", span_notice("Consecutive CQC")));

        Dm.append(dot, I18n.T("<b><i>In addition, by having your throw mode on when being attacked, you enter an active defense mode where you have a chance to block and sometimes even counter attacks done to you.</i></b>"));
        return dot;
    }
}

///Subtype of CQC. Only used for the chef.
// tg: code/datums/martial/cqc.dm:395
public partial class datum_martial_art_cqc_under_siege : datum_martial_art_cqc
{
    /// Refreshes the valid areas from the cook's mapping config, adding areas in config to the list of possible areas.
    // tg: code/datums/martial/cqc.dm:401
    public virtual void refresh_valid_areas()
    {
        // CHECK_MAP_JOB_CHANGE(JOB_COOK, "additional_cqc_areas") (__DEFINES/maps.dm:243), expanded inline (rules 11.1)
        object additional_cqc_areas = (SSmapping?.current_map?.job_changes?[JOB_COOK] as DmList)?["additional_cqc_areas"];
        if (!Dm.truthy(additional_cqc_areas))
            return;

        if (additional_cqc_areas is not DmList area_paths)
        {
            stack_trace("Incorrect CQC area format from mapping configs. Expected /list, got: [" + Dm.str((additional_cqc_areas as datum)?.type) + "]", file: "code/datums/martial/cqc.dm", line: 407);
            return;
        }

        foreach (object path_as_text in Dm.each(area_paths))
        {
            object path = text2path(Dm.str(path_as_text));
            if (!ispath(path, typeof(area)))
            {
                stack_trace("Invalid path in mapping config for chef CQC: [" + Dm.str(path_as_text) + "]", file: "code/datums/martial/cqc.dm", line: 413);
                continue;
            }

            if (!Dm.@in(path, kitchen_areas)) // kitchen_areas |= path
                Dm.append(kitchen_areas, path);
        }
    }

    /// Limits where the chef's CQC can be used to only whitelisted areas.
    // tg: code/datums/martial/cqc.dm:419
    public override object can_use(mob_living martial_artist = null)
    {
        if (!Dm.truthy(is_type_in_list(get_area(martial_artist) as datum, kitchen_areas)))
            return FALSE;
        return base.can_use(martial_artist);
    }
}

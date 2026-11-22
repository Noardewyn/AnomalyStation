// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/medbot/medbot_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG lines (gaps package C3).

// tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:21
public partial class datum_ai_movement_jps_bot_medbot : datum_ai_movement_jps_bot
{
    // only AI isn't allowed to move when this flag is set, sentient players can
    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:26
    public override object allowed_to_move(datum_move_loop source = null)
    {
        datum_ai_controller controller = source.extra_info as datum_ai_controller;
        mob_living_basic_bot_medbot bot_pawn = (mob_living_basic_bot_medbot)controller.pawn;
        if (Dm.truthy(Dm.bitand(bot_pawn.medical_mode_flags, MEDBOT_STATIONARY_MODE)))
            return FALSE;
        return base.allowed_to_move(source);
    }
}

// tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:11
public partial class datum_ai_controller_basic_controller_bot_medbot : datum_ai_controller_basic_controller_bot
{
    /// Medbot's note_unreachable_target skips blacklisting while stationary, matching the old set_if_can_reach bypass.
    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:39
    public override void note_unreachable_target(atom target = null)
    {
        mob_living_basic_bot_medbot bot_pawn = (mob_living_basic_bot_medbot)pawn;
        if (Dm.truthy(Dm.bitand(bot_pawn.medical_mode_flags, MEDBOT_STATIONARY_MODE)))
            return;
        base.note_unreachable_target(target);
    }
}

/// Gathers nearby humans as patients; range is clamped to adjacent tiles when the medbot is in stationary mode. I should probably just make this a blackboard thing but I cannot be arsed right now.
// tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:46
public partial class datum_target_source_oview_single_type_human_mob_medbot_patient : datum_target_source_oview_single_type_human_mob
{
    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:48
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        mob_living_basic_bot_medbot bot_pawn = (mob_living_basic_bot_medbot)pawn;
        if (Dm.truthy(Dm.bitand(bot_pawn.medical_mode_flags, MEDBOT_STATIONARY_MODE)))
            range = 1;
        return base.collect_candidates(pawn, controller, range);
    }
}

/// Valid if the patient needs the damage type this medbot heals (or is a conscious target while emagged).
// tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:55
public partial class datum_targeting_strategy_treatable_patient : datum_targeting_strategy
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not mob_living_carbon_human patient || patient.stat == DEAD)
            return FALSE;
        mob_living_basic_bot_medbot bot_pawn = (mob_living_basic_bot_medbot)living_mob;
        if (Dm.truthy(Dm.bitand(bot_pawn.bot_access_flags, BOT_COVER_EMAGGED)) && patient.stat == STABLE)
            return TRUE;
        if (Dm.eq(bot_pawn.damage_type_healer, HEAL_ALL_DAMAGE))
            return Dm.num(Dm.num(patient.get_total_damage()) > bot_pawn.heal_threshold);
        return Dm.num(Dm.num(patient.get_current_damage_of_type(damagetype: bot_pawn.damage_type_healer)) > bot_pawn.heal_threshold);
    }
}

/// Finds a patient to treat, announcing that the bot is on its way when the patient isn't already adjacent.
// tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:70
public partial class datum_bt_node_ai_behavior_acquire_target_update_interaction_target_medbot_patient : datum_bt_node_ai_behavior_acquire_target_update_interaction_target
{
    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:72
    public override void on_target_found(datum_ai_controller controller = null, atom target = null, datum_targeting_strategy strategy = null)
    {
        if (QDELETED(controller.pawn) || Dm.num(get_dist(controller.pawn, target)) <= 1)
            return;
        datum_action_cooldown_bot_announcement announcement = controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        announcement?.announce(pick(controller.blackboard[BB_WAIT_SPEECH] as DmList));
    }
}

// tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:80
public partial class datum_bt_node_ai_behavior_tend_to_patient : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:83
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_carbon_human patient = controller.blackboard[target_key] as mob_living_carbon_human;
        if (QDELETED(patient) || patient.stat == DEAD)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (Dm.num(get_dist(controller.pawn, patient)) > 1)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED; //We technically failed, but we want to try again so succeed.
        mob_living_basic_bot_medbot bot_pawn = (mob_living_basic_bot_medbot)controller.pawn;
        if (Dm.truthy(check_if_healed(patient, bot_pawn.heal_threshold, bot_pawn.damage_type_healer, bot_pawn.bot_access_flags)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        if (patient.stat >= HARD_CRIT && prob(5))
        {
            datum_action_cooldown_bot_announcement announcement = controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
            announcement?.announce(pick(controller.blackboard[BB_NEAR_DEATH_SPEECH] as DmList));
        }
        Waiting.NoSleep(bot_pawn.melee_attack(patient));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:101
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        datum_ai_controller_basic_controller_bot bot_controller = (datum_ai_controller_basic_controller_bot)controller;
        mob_living_basic_bot_medbot bot_pawn = (mob_living_basic_bot_medbot)bot_controller.pawn;
        atom target = bot_controller.blackboard[target_key] as atom;
        bool is_stationary = Dm.truthy(Dm.bitand(bot_pawn.medical_mode_flags, MEDBOT_STATIONARY_MODE));
        if (!Dm.truthy(succeeded))
        {
            if (target != null && !is_stationary)
                bot_controller.add_to_blacklist(target);
            bot_controller.clear_blackboard_key(target_key);
            return;
        }
        if (QDELETED(target) || !Dm.truthy(check_if_healed(target as mob_living_carbon_human, bot_pawn.heal_threshold, bot_pawn.damage_type_healer, bot_pawn.bot_access_flags)))
            return;
        datum_action_cooldown_bot_announcement announcement = bot_controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        announcement?.announce(pick(bot_controller.blackboard[BB_AFTERHEAL_SPEECH] as DmList));
        bot_controller.clear_blackboard_key(target_key);
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:117
    public virtual object check_if_healed(mob_living_carbon_human patient = null, object threshold = null, object damage_type_healer = null, object access_flags = null)
    {
        if (Dm.truthy(Dm.bitand(access_flags, BOT_COVER_EMAGGED)))
            return Dm.num(patient.stat != STABLE);
        object patient_damage = Dm.eq(damage_type_healer, HEAL_ALL_DAMAGE) ? patient.get_total_damage() : patient.get_current_damage_of_type(damagetype: damage_type_healer as string);
        return Dm.num(Dm.num(patient_damage) <= Dm.num(threshold));
    }
}

// tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:125
public partial class datum_bt_node_ai_behavior_handle_medbot_speech : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:129
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_bot_medbot bot_pawn = (mob_living_basic_bot_medbot)controller.pawn;
        bool currently_tipped = Dm.truthy(Dm.bitand(bot_pawn.medical_mode_flags, MEDBOT_TIPPED_MODE));
        float speech_chance = (Dm.truthy(Dm.bitand(bot_pawn.bot_access_flags, BOT_COVER_EMAGGED)) || currently_tipped) ? 15 : 5;
        if (!SPT_PROB(speech_chance, seconds_per_tick))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        datum_action_cooldown_bot_announcement announcement = controller.blackboard[announce_key] as datum_action_cooldown_bot_announcement;
        DmList speech_to_pick_from = null;
        if (currently_tipped)
            speech_to_pick_from = controller.blackboard[BB_WORRIED_ANNOUNCEMENTS] as DmList;
        else if (Dm.truthy(Dm.bitand(bot_pawn.bot_access_flags, BOT_COVER_EMAGGED)))
            speech_to_pick_from = controller.blackboard[BB_EMAGGED_SPEECH] as DmList;
        else if (Dm.eq(bot_pawn.mode, BOT_IDLE))
            speech_to_pick_from = controller.blackboard[BB_IDLE_SPEECH] as DmList;
        mob_living living_pawn = controller.pawn as mob_living;
        if (Dm.locate<obj_item_clothing_head_costume_chicken>(living_pawn) != null)
            Dm.append(ref speech_to_pick_from, MEDIBOT_VOICED_CHICKEN);
        if (!Dm.truthy(length(speech_to_pick_from)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        announcement.announce(pick(speech_to_pick_from));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// Valid if the patient is at least unconscious, has a mind, and is visible  used to announce medical emergencies.
// tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:153
public partial class datum_targeting_strategy_crit_patient : datum_targeting_strategy
{
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target is not mob_living_carbon_human patient || patient.stat < HARD_CRIT || patient.mind == null)
            return FALSE;
        return can_see(living_mob, patient, vision_range);
    }
}

// tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:163
public partial class datum_bt_node_ai_behavior_announce_patient : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:167
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_target = controller.blackboard[target_key] as mob_living;
        if (QDELETED(living_target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        datum_action_cooldown_bot_announcement announcement = controller.blackboard[BB_ANNOUNCE_ABILITY] as datum_action_cooldown_bot_announcement;
        if (QDELETED(announcement))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        object text_to_announce = Dm.text("Medical emergency! [] is in critical condition at []!", living_target, get_area(living_target));
        announcement.announce(text_to_announce, controller.blackboard[BB_RADIO_CHANNEL]);
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot_ai.dm:178
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/_bots.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The bot root (gaps package C3). The pAI host is a row: a pAI card stays inert (the pAI is not ported), so insertpai and ejectpai are translated and unreachable until it is.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/say.dm:56  #define RUNECHAT_BOLD(str) "+[str]+"
    public static string RUNECHAT_BOLD(object str) => Dm.text("+[]+", str);
}

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mob/living/basic/bots/_bots.dm:1
    public DmList command_strings;

    public virtual void InitGlobalcommand_strings()
    {
        command_strings = Dm.list(Dm.kv("patroloff", "STOP PATROL"), Dm.kv("patrolon", "START PATROL"), Dm.kv("stop", "STOP"), Dm.kv("go", "GO"), Dm.kv("home", "RETURN HOME"));
        Dm.append(ref gvars_datum_init_order, "command_strings");
    }
}

// tg: code/modules/mob/living/basic/bots/_bots.dm:11
public partial class mob_living_basic_bot : mob_living_basic
{
    private const float SENTIENT_BOT_RESET_TIMER = 45 * SECONDS;

    ///list of traits we apply and remove when turning on/off
    // tg: code/modules/mob/living/basic/bots/_bots.dm:98
    private static DmList on_toggle_traits => TgState.Current.mob_living_basic_bot__on_toggle_traits ??= Dm.list(TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED);

    // tg: code/modules/mob/living/basic/bots/_bots.dm:114
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        add_traits(Dm.list(TRAIT_REAGENT_SCANNER, TRAIT_UNOBSERVANT), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(handle_loop_movement));
        RegisterSignal(this, COMSIG_ATOM_WAS_ATTACKED, nameof(after_attacked));
        RegisterSignal(this, COMSIG_MOB_RETRIEVE_ACCESS, nameof(retrieve_access));
        add_traits(Dm.list(TRAIT_NO_GLIDE, TRAIT_SILICON_EMOTES_ALLOWED), INNATE_TRAIT);
        Waiting.NoSleep(LoadComponent(typeof(datum_component_bloodysoles_bot)));
        Dm.append(ref GLOB.bots_list, this);

        // Give bots a fancy new ID card that can hold any access.
        access_card = Dm.@new<obj_item_card_id_advanced_simple_bot>(this);
        // This access is so bots can be immediately set to patrol and leave Robotics, instead of having to be let out first.
        // (SSid_access?: a test harness world without Master has no SSid_access, as in the card's Initialize, cards_ids.cs)
        if (SSid_access != null)
        {
            access_card.set_access(Dm.list(ACCESS_ROBOTICS));
            provide_additional_access();
        }

        internal_radio = Dm.@new<obj_item_radio>(this);
        if (radio_key != null)
            internal_radio.keyslot = Dm.@new(radio_key) as obj_item_encryptionkey;
        internal_radio.subspace_transmission = TRUE;
        internal_radio.canhear_range = 0; // anything greater will have the bot broadcast the channel as if it were saying it out loud.
        internal_radio.recalculateChannels();

        //Adds bot to the diagnostic HUD system
        prepare_huds();
        datum_atom_hud_data_diagnostic diag_hud = GLOB.huds[DATA_HUD_DIAGNOSTIC] as datum_atom_hud_data_diagnostic;
        diag_hud.add_atom_to_hud(this);
        diag_hud_set_bothealth();
        diag_hud_set_botstat();
        diag_hud_set_botmode();

        //If a bot has its own HUD (for player bots), provide it.
        if (data_hud_type != null)
            ADD_TRAIT(this, data_hud_type, INNATE_TRAIT);

        if (Dm.truthy(mapload) && Dm.truthy(is_station_level(z)) && Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_CAN_BE_SAPIENT)) && Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ROUNDSTART_POSSESSION)))
            Waiting.NoSleep(enable_possession(mapload: Dm.num(mapload)));

        if (Dm.truthy(length(facepaint_overlays)))
            Waiting.NoSleep(AddComponent(typeof(datum_component_defaceable), Dm.resource("icons/mob/silicon/aibot_faces.dmi"), facepaint_overlays, "a face"));

        // tg: the named argument automated_announcements is /datum/action/cooldown/bot_announcement/New's third
        pa_system = (announcement_type == null ? Dm.@new<datum_action_cooldown_bot_announcement>(this, null, generate_speak_list()) : Dm.@new(announcement_type, this, null, generate_speak_list())) as datum_action_cooldown_bot_announcement;
        Waiting.NoSleep(pa_system.Grant(this));
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        controller.set_blackboard_key(BB_ANNOUNCE_ABILITY, pa_system);
        controller.set_blackboard_key(BB_RADIO_CHANNEL, radio_channel);
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:167
    [ShouldCallParent]
    public virtual void set_mode_flags(object mode_flags = null)
    {
        bot_mode_flags = Dm.num(mode_flags);
        if (Dm.truthy(_listen_lookup?[COMSIG_BOT_MODE_FLAGS_SET]))
            _SendSignal(COMSIG_BOT_MODE_FLAGS_SET, Dm.list(this, mode_flags));
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:172
    public virtual object get_mode()
    {
        if (client != null) //Player bots do not have modes, thus the override. Also an easy way for PDA users/AI to know when a bot is a player.
            return span_bold(paicard != null ? "pAI Controlled" : "Autonomous");

        if (!Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
            return span_bad("Inactive");

        return span_average(mode);
    }

    /**
     * Returns a status string about the bot's current status, if it's moving, manually controlled, or idle.
     */
    // tg: code/modules/mob/living/basic/bots/_bots.dm:184
    public virtual object get_mode_ui()
    {
        if (client != null)
            return paicard != null ? "pAI Controlled" : "Autonomous";

        if (!Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
            return "Inactive";

        return mode;
    }

    /**
     * Returns a string of flavor text for emagged bots as defined by policy.
     */
    // tg: code/modules/mob/living/basic/bots/_bots.dm:196
    public virtual object get_emagged_message()
    {
        return get_policy(ROLE_EMAGGED_BOT) ?? "You are a malfunctioning bot! Disrupt everyone and cause chaos!";
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:199
    public virtual object turn_on(mob user = null)
    {
        if (stat == DEAD)
            return FALSE;
        set_mode_flags(Dm.bitor(bot_mode_flags, BOT_MODE_ON));
        remove_traits(Dm.list(TRAIT_INCAPACITATED, TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), POWER_LACK_TRAIT);
        Waiting.NoSleep(set_light_on(Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)) ? TRUE : FALSE));
        update_appearance();
        balloon_alert(this, "turned on");
        diag_hud_set_botstat();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:210
    public virtual async DmTask turn_off()
    {
        set_mode_flags(Dm.bitand(bot_mode_flags, Dm.bitnot(BOT_MODE_ON)));
        add_traits(on_toggle_traits, POWER_LACK_TRAIT);
        Waiting.NoSleep(set_light_on(Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)) ? TRUE : FALSE));
        await bot_reset(); //Resets an AI's call, should it exist.
        balloon_alert(this, "turned off");
        update_appearance();
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:218
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.bots_list, this);
        calling_ai_ref = null;
        clear_path_hud();
        qdel(paicard);
        paicard = null;
        qdel(pa_system);
        pa_system = null;
        qdel(internal_radio);
        internal_radio = null;
        qdel(access_card);
        access_card = null;
        return base.Destroy(force);
    }

    /// Allows this bot to be controlled by a ghost, who will become its mind
    // tg: code/modules/mob/living/basic/bots/_bots.dm:229
    public virtual async DmTask enable_possession(object user = null, float? mapload = null)
    {
        mapload ??= FALSE;
        if (paicard != null)
        {
            balloon_alert(user as mob, "already sapient!");
            return;
        }
        can_be_possessed = TRUE;
        bool can_announce = !Dm.truthy(mapload) && offer_ghosts_cooldown <= world.time; // COOLDOWN_FINISHED, inline
        await AddComponent(typeof(datum_component_ghost_direct_control),
            Dm.named("ban_type", ROLE_BOT), Dm.named("poll_candidates", can_announce ? TRUE : FALSE), Dm.named("poll_ignore_key", "bots"), Dm.named("assumed_control_message", Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)) ? get_emagged_message() : possessed_message), Dm.named("extra_control_checks", Dm.@new<datum_callback>(this, nameof(check_possession))), Dm.named("after_assumed_control", Dm.@new<datum_callback>(this, nameof(post_possession))));
        if (can_announce)
            offer_ghosts_cooldown = world.time + 30 * SECONDS; // COOLDOWN_START, inline

        // log_silicon: admin logs
    }

    /// Disables this bot from being possessed by ghosts
    // tg: code/modules/mob/living/basic/bots/_bots.dm:251
    public virtual void disable_possession(mob user = null)
    {
        // log_silicon: admin logs
        can_be_possessed = FALSE;
        if (key == null)
            return;
        if (user != null)
            log_combat(user, this, Dm.text("ejected [] from control of [] ([]).", key_name(this), this, Dm.initial(this, (mob_living_basic_bot p) => p.name)));
        to_chat(this, span_warning("You feel yourself fade as your personality matrix is reset!"));
        ghostize(can_reenter_corpse: FALSE);
        playsound(this, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);
        speak("Personality matrix reset!");
        key = null;
    }

    /// Returns true if this mob can be controlled
    // tg: code/modules/mob/living/basic/bots/_bots.dm:266
    public virtual object check_possession(mob potential_possessor = null)
    {
        if (!Dm.truthy(can_be_possessed))
            to_chat(potential_possessor, span_warning("The bot's personality download has been disabled!"));
        return can_be_possessed;
    }

    /// Fired after something takes control of this mob
    // tg: code/modules/mob/living/basic/bots/_bots.dm:272
    public virtual async DmTask post_possession()
    {
        playsound(this, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);
        speak("New personality installed successfully!");
        await rename(this);
    }

    /// Allows renaming the bot to something else
    // tg: code/modules/mob/living/basic/bots/_bots.dm:278
    public virtual async DmTask rename(mob user = null)
    {
        object new_name = await sanitize_name(
            reject_bad_text(await tgui_input_text(
                user: user,
                message: Dm.text("This machine is designated []. Would you like to update [] registration?", real_name, p_their()),
                title: "Name change",
                @default: real_name,
                max_length: MAX_NAME_LEN)),
            allow_numbers: TRUE);
        if (new_name == null || QDELETED(this))
            return;
        if (key != null && user != this)
        {
            object accepted = await tgui_alert(
                this,
                message: Dm.text("Do you wish to be renamed to []?", new_name),
                title: "Name change",
                buttons: Dm.list("Yes", "No"));
            if (!Dm.eq(accepted, "Yes") || QDELETED(this))
                return;
        }
        await fully_replace_character_name(real_name, new_name);
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:302
    public override async DmTask<object> allowed__proc(mob accessor = null)
    {
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED))) // Unlocked.
            return TRUE;
        return await base.allowed__proc(accessor);
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:307
    public override object bee_friendly()
    {
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:310
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (paicard != null)
            await ejectpai();
        await explode();
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:316
    public virtual async DmTask explode()
    {
        visible_message(span_boldnotice(Dm.text("[] blows apart!", this)));
        do_sparks(3, TRUE, this);
        atom location_destroyed = drop_location() as atom;
        if (prob(50))
            await drop_part(robot_arm, location_destroyed);
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:323
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED))) //First emag application unlocks the bot's interface. Apply a screwdriver to use the emag again.
        {
            bot_access_flags = Dm.bitand(bot_access_flags, Dm.bitnot(BOT_COVER_LOCKED));
            balloon_alert(user, "cover unlocked");
            return TRUE;
        }
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) || !Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN))) //Bot panel is unlocked by ID or emag, and the panel is screwed open. Ready for emagging.
        {
            balloon_alert(user, "open maintenance panel first!");
            return FALSE;
        }
        bot_access_flags = Dm.bitor(bot_access_flags, BOT_COVER_EMAGGED);
        bot_access_flags = Dm.bitor(bot_access_flags, BOT_COVER_LOCKED);
        set_mode_flags(Dm.bitand(bot_mode_flags, Dm.bitnot(BOT_MODE_REMOTE_ENABLED))); //Manually emagging the bot also locks the AI from controlling it.
        await bot_reset();
        turn_on(); //The bot automatically turns on when emagged, unless recently hit with EMP.
        to_chat(this, span_userdanger("(#$*#$^^( OVERRIDE DETECTED"));
        to_chat(this, span_boldnotice(get_emagged_message() as string));
        if (user != null)
            log_combat(user, this, "emagged");
        emag_effects(user);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:344
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.num(health) < Dm.num(maxHealth))
        {
            if (Dm.num(health) > (Dm.num(maxHealth) * 0.3f))
                Dm.append(ref dot, Dm.text("[]'s parts look loose.", this));
            else
                Dm.append(ref dot, Dm.text("[]'s parts look very loose!", this));
        }
        else
            Dm.append(ref dot, Dm.text("[] is in pristine condition.", this));

        bool open = Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN));
        bool locked = Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED));
        Dm.append(ref dot, span_notice(Dm.text("[] maintenance panel is [].", p_Their(), open ? "open" : "closed")));
        Dm.append(ref dot, span_info(Dm.text("You can use a <b>screwdriver</b> to [] [].", open ? "close" : "open", p_them())));

        if (open)
        {
            Dm.append(ref dot, span_notice(Dm.text("[] control panel is [].", p_Their(), locked ? "locked" : "unlocked")));
            if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)) && (issilicon(user) || Dm.truthy(await user.Adjacent(this))))
                Dm.append(ref dot, span_info(Dm.text("Alt-click [][] to [] [] control panel.", issilicon(user) ? "" : "or use your ID on ", p_them(), locked ? "unlock" : "lock", p_their())));
        }
        if (paicard == null)
            return dot;
        Dm.append(ref dot, span_notice(Dm.text("[] [] a pAI device installed.", p_They(), p_have())));
        if (!open)
            Dm.append(ref dot, span_info("You can use a <b>hemostat</b> to remove it."));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:367
    public override async DmTask updatehealth()
    {
        await base.updatehealth();
        diag_hud_set_bothealth();
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:371
    public override void med_hud_set_health()
    {
        //we use a different hud
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:374
    public override object med_hud_set_status()
    {
        return null; //we use a different hud
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:377
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy((user as mob_living)?.combat_mode))
        {
            await ui_interact(user);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:383
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (!Dm.truthy(await topic_denied(user)))
        {
            await ui_interact(user);
            return null;
        }
        to_chat(user, span_warning(Dm.text("[]'s interface is not responding!", this)));
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:389
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, bot_ui, name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:395
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        unlock_with_id(user as mob_living);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:399
    public virtual object unlock_with_id(mob_living user = null)
    {
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
        {
            balloon_alert(user, "error!");
            return null;
        }
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)))
        {
            balloon_alert(user, "access panel must be closed!");
            return null;
        }
        if (!Dm.truthy(Waiting.NoSleep(allowed__proc(user))))
        {
            balloon_alert(user, "no access");
            return null;
        }
        bot_access_flags = Dm.bitxor(bot_access_flags, BOT_COVER_LOCKED);
        to_chat(user, span_notice(Dm.text("Controls are now [].", Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) ? "locked" : "unlocked")));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:413
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)))
        {
            to_chat(user, span_warning("The maintenance panel is locked!"));
            return ITEM_INTERACT_SUCCESS;
        }

        tool.play_tool_sound(this);
        bot_access_flags = Dm.bitxor(bot_access_flags, BOT_COVER_MAINTS_OPEN);
        to_chat(user, span_notice(Dm.text("The maintenance panel is now [].", Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)) ? "opened" : "closed")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:423
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        user.changeNext_move(CLICK_CD_MELEE);
        if (Dm.truthy(user.combat_mode))
            return FALSE;

        if (Dm.num(health) >= Dm.num(maxHealth))
        {
            user.balloon_alert(user, "no repairs needed!");
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)))
        {
            user.balloon_alert(user, "maintenance panel closed!");
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, volume: 40)))
            return ITEM_INTERACT_SUCCESS;

        await heal_overall_damage(10);
        user.visible_message(span_notice(Dm.text("[] repairs []!", user, this)), span_notice(Dm.text("You repair [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:444
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(tool.GetID()))
        {
            unlock_with_id(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_pai_card pai_card)
        {
            await insertpai(user, pai_card);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.eq(tool.tool_behaviour, TOOL_HEMOSTAT) || paicard == null)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)))
        {
            balloon_alert(user, "open the access panel!");
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, "removing pAI...");
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)) || paicard == null)
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(
            span_notice(Dm.text("[] uses [] to pull [] out of []!", user, tool, paicard, Dm.initial(this, (mob_living_basic_bot p) => p.name))),
            span_notice(Dm.text("You pull [] out of [] with [].", paicard, Dm.initial(this, (mob_living_basic_bot p) => p.name), tool)));

        await ejectpai(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:472
    public override async DmTask<object> attack_effects(object damage_done = null, object hit_zone = null, object armor_block = null, obj_item attacking_item = null, mob_living attacker = null)
    {
        object dot = null;
        if (Dm.num(damage_done) > 0 && !Dm.eq(attacking_item?.damtype, STAMINA) && stat != DEAD)
        {
            do_sparks(5, TRUE, this);
            dot = TRUE;
        }
        object parent_result = await base.attack_effects(damage_done, hit_zone, armor_block, attacking_item, attacker);
        return Dm.truthy(parent_result) ? parent_result : dot;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:478
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (prob(25) || !Dm.eq(dot, BULLET_ACT_HIT))
            return dot;
        if (!Dm.eq(hitting_projectile.damage_type, BRUTE) && !Dm.eq(hitting_projectile.damage_type, BURN))
            return dot;
        if (!Dm.truthy(hitting_projectile.is_hostile_projectile()) || Dm.num(hitting_projectile.damage) <= 0)
            return dot;
        do_sparks(5, TRUE, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:488
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        Dm.@new<obj_effect_temp_visual_emp>(loc);
        if (paicard != null)
        {
            await paicard.emp_act(severity);
            visible_message(span_notice(Dm.text("[] flies out of []!", paicard, Dm.initial(this, (mob_living_basic_bot p) => p.name))), span_warning(Dm.text("You are forcefully ejected from []!", Dm.initial(this, (mob_living_basic_bot p) => p.name))));
            await ejectpai();
        }

        if (QDELETED(this))
            return dot;

        if (Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
            await turn_off();
        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(turn_on)), Dm.num(severity) * 30 * SECONDS, file: "code/modules/mob/living/basic/bots/_bots.dm", line: 504);

        if (!prob(70 / Dm.num(severity)) || !Dm.truthy(length(GLOB.uncommon_roundstart_languages)))
            return dot;

        remove_all_languages(source: LANGUAGE_EMP);
        grant_random_uncommon_language(source: LANGUAGE_EMP);
        return dot;
    }

    /**
     * Pass a message to have the bot say() it, passing through our announcement action to potentially also play a sound.
     * Optionally pass a frequency to say it on the radio.
     */
    // tg: code/modules/mob/living/basic/bots/_bots.dm:516
    public virtual void speak(object message = null, object channel = null)
    {
        if (!Dm.truthy(message))
            return;
        pa_system.announce(message, channel);
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:521
    public override async DmTask<object> radio__proc(object message = null, DmList message_mods = null, DmList spans = null, object language = null)
    {
        object dot = await base.radio__proc(message, message_mods, spans, language);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(message_mods[MODE_HEADSET]))
        {
            internal_radio.talk_into(this, message, null, spans, language, message_mods);
            return REDUCE_RANGE;
        }
        if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_DEPARTMENT))
        {
            internal_radio.talk_into(this, message, message_mods[RADIO_EXTENSION], spans, language, message_mods);
            return REDUCE_RANGE;
        }
        if (Dm.@in(message_mods[RADIO_EXTENSION], GLOB.default_radio_channels))
        {
            internal_radio.talk_into(this, message, message_mods[RADIO_EXTENSION], spans, language, message_mods);
            return REDUCE_RANGE;
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:536
    public virtual async DmTask drop_part(object drop_item = null, object dropzone = null)
    {
        obj_item item_to_drop;
        if (ispath(drop_item))
            item_to_drop = await Dm.new_async(drop_item as Type, dropzone) as obj_item;
        else
        {
            item_to_drop = drop_item as obj_item;
            await item_to_drop.forceMove(dropzone as atom);
        }

        if (item_to_drop is obj_item_stock_parts_power_store_cell dropped_cell)
        {
            dropped_cell.charge = 0;
            return;
        }

        if (item_to_drop is obj_item_storage)
        {
            // item_to_drop.contents = list()
            foreach (atom_movable content in Dm.each_cast<atom_movable>(item_to_drop.contents.Copy()))
                qdel(content);
            return;
        }

        if (item_to_drop is not obj_item_gun_energy dropped_gun)
            return;
        dropped_gun.cell.charge = 0;
        dropped_gun.update_appearance();
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:559
    public virtual async DmTask bot_reset(float? bypass_ai_reset = null)
    {
        bypass_ai_reset ??= FALSE;
        if (Dm.truthy(_listen_lookup?[COMSIG_BOT_RESET]))
            _SendSignal(COMSIG_BOT_RESET, Dm.list(this));
        access_card.set_access(initial_access);
        update_bot_mode(new_mode: Dm.initial(this, (mob_living_basic_bot p) => p.mode));
        diag_hud_set_botstat();
        diag_hud_set_botmode();
        clear_path_hud();
        if (Dm.truthy(bypass_ai_reset) || calling_ai_ref == null)
            return;
        mob_living ai_caller = calling_ai_ref.resolve() as mob_living;
        if (ai_caller == null)
            return;
        to_chat(ai_caller, span_danger("Call command to a bot has been reset."));
        calling_ai_ref = null;
    }

    //PDA control. Some bots, especially MULEs, may have more parameters.
    // tg: code/modules/mob/living/basic/bots/_bots.dm:575
    public virtual async DmTask<object> bot_control(object command = null, mob user = null, DmList user_access = null, params object[] __extra)
    {
        user_access ??= Dm.list();
        if (!Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)) || Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)) || !Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_REMOTE_ENABLED))) //Emagged bots do not respect anyone's authority! Bots with their remote controls off cannot get commands.
            return TRUE; //ACCESS DENIED
        if (client != null && !Dm.eq(command, "ejectpai"))
            bot_control_message(command, user);
        // process control input
        switch (command as string)
        {
            case "patroloff":
                await set_patrol_off();
                break;
            case "patrolon":
                set_mode_flags(Dm.bitor(bot_mode_flags, BOT_MODE_AUTOPATROL));
                break;
            case "summon":
                await summon_bot(user, user_access: user_access);
                break;
            case "ejectpai":
                await eject_pai_remote(user);
                break;
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:591
    public virtual async DmTask set_patrol_off()
    {
        await bot_reset();
        set_mode_flags(Dm.bitand(bot_mode_flags, Dm.bitnot(BOT_MODE_AUTOPATROL)));
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:595
    public virtual object bot_control_message(object command = null, object user = null)
    {
        if (Dm.eq(command, "summon"))
            return Dm.text("PRIORITY ALERT:[] in []!", user, get_area_name(user as atom));
        return GLOB.command_strings[command] ?? Dm.text("Unidentified control sequence received:[]", command);
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:600
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["can_hack"] = Dm.num(HAS_SILICON_ACCESS(user));
        data["custom_controls"] = Dm.list();
        data["emagged"] = Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED);
        data["has_access"] = await allowed__proc(user);
        data["locked"] = Dm.bitand(bot_access_flags, BOT_COVER_LOCKED);
        DmList settings = Dm.list();
        data["settings"] = settings;
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) || HAS_SILICON_ACCESS(user))
        {
            settings["pai_inserted"] = Dm.num(paicard != null);
            settings["allow_possession"] = Dm.bitand(bot_mode_flags, BOT_MODE_CAN_BE_SAPIENT);
            settings["possession_enabled"] = can_be_possessed;
            settings["airplane_mode"] = Dm.num(!Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_REMOTE_ENABLED)));
            settings["maintenance_lock"] = Dm.num(!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)));
            settings["power"] = Dm.bitand(bot_mode_flags, BOT_MODE_ON);
            settings["patrol_station"] = Dm.bitand(bot_mode_flags, BOT_MODE_AUTOPATROL);
        }
        return data;
    }

    // Actions received from TGUI
    // tg: code/modules/mob/living/basic/bots/_bots.dm:619
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob the_user = ui.user;
        if (!Dm.truthy(await allowed__proc(the_user)))
        {
            balloon_alert(the_user, "access denied!");
            return null;
        }

        if (Dm.eq(action, "lock"))
            bot_access_flags = Dm.bitxor(bot_access_flags, BOT_COVER_LOCKED);

        switch (action as string)
        {
            case "power":
                if (Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
                    await turn_off();
                else
                    turn_on();
                break;
            case "maintenance":
                bot_access_flags = Dm.bitxor(bot_access_flags, BOT_COVER_MAINTS_OPEN);
                break;
            case "patrol":
                set_mode_flags(Dm.bitxor(bot_mode_flags, BOT_MODE_AUTOPATROL));
                await bot_reset();
                break;
            case "airplane":
                set_mode_flags(Dm.bitxor(bot_mode_flags, BOT_MODE_REMOTE_ENABLED));
                break;
            case "hack":
                if (!HAS_SILICON_ACCESS(the_user))
                    return null;
                if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
                {
                    bot_access_flags = Dm.bitor(bot_access_flags, Dm.bitor(BOT_COVER_LOCKED, Dm.bitor(BOT_COVER_EMAGGED, BOT_COVER_HACKED)));
                    emag_effects(the_user);
                    to_chat(the_user, span_warning(Dm.text("You overload []'s [].", this, hackables)));
                    message_admins(Dm.text("Safety lock of [] was disabled by [] in []", ADMIN_LOOKUPFLW(this), ADMIN_LOOKUPFLW(the_user), ADMIN_VERBOSEJMP(the_user)));
                    the_user.log_message(Dm.text("disabled safety lock of []", the_user), LOG_GAME);
                    await bot_reset();
                    to_chat(this, span_userdanger("(#$*#$^^( OVERRIDE DETECTED"));
                    to_chat(this, span_boldnotice(get_emagged_message() as string));
                    return null;
                }
                if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_HACKED)))
                {
                    to_chat(the_user, span_bolddanger(Dm.text("You fail to repair []'s [].", this, hackables)));
                    return null;
                }
                bot_access_flags = Dm.bitand(bot_access_flags, Dm.bitnot(Dm.bitor(BOT_COVER_EMAGGED, BOT_COVER_HACKED)));
                to_chat(the_user, span_notice(Dm.text("You reset the []'s [].", this, hackables)));
                the_user.log_message(Dm.text("re-enabled safety lock of []", this), LOG_GAME);
                await bot_reset();
                to_chat(this, span_userdanger("Software restored to standard."));
                to_chat(this, span_boldnotice(possessed_message));
                break;
            case "eject_pai":
                if (paicard == null)
                    return null;
                to_chat(the_user, span_notice(Dm.text("You eject [] from [].", paicard, Dm.initial(this, (mob_living_basic_bot p) => p.name))));
                await ejectpai(the_user);
                break;
            case "toggle_personality":
                if (Dm.truthy(can_be_possessed))
                    disable_possession(the_user);
                else
                    await enable_possession(the_user);
                break;
            case "rename":
                await rename(the_user);
                break;
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:679
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state ?? Dm.initial(this, (mob_living_basic_bot p) => p.icon_state), Dm.bitand(bot_mode_flags, BOT_MODE_ON));
        return await base.update_icon_state(__extra);
    }

    /// Access check proc for bot topics! Remember to place in a bot's individual Topic if desired.
    // tg: code/modules/mob/living/basic/bots/_bots.dm:684
    public virtual async DmTask<object> topic_denied(mob user = null)
    {
        if (!Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
            return TRUE;
        // 0 for access, 1 for denied.
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED))) //An emagged bot cannot be controlled by humans, silicons can if one hacked it.
            return FALSE;
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_HACKED))) //Manually emagged by a human - access denied to all.
            return TRUE;
        if (!HAS_SILICON_ACCESS(user)) //Bot is hacked, so only silicons and admins are allowed access.
            return TRUE;

        return FALSE;
    }

    /// Places a pAI in control of this mob
    // tg: code/modules/mob/living/basic/bots/_bots.dm:698
    public virtual async DmTask<object> insertpai(mob user = null, obj_item_pai_card card = null)
    {
        Dm.Deferred("code/modules/mob/living/basic/bots/_bots.dm:698", "insertpai: the pAI (/mob/living/silicon/pai, its card) is not ported: the slot refuses");
        balloon_alert(user, "incompatible firmware!");
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:731
    public override void ghost(params object[] __extra)
    {
        if (stat != DEAD) // Only ghost if we're doing this while alive, the pAI probably isn't dead yet.
        {
            base.ghost(__extra);
            return;
        }
        if (paicard != null && (client == null || stat == DEAD))
            Waiting.NoSleep(ejectpai());
    }

    /// Ejects a pAI from this bot
    // tg: code/modules/mob/living/basic/bots/_bots.dm:738
    public virtual async DmTask ejectpai(mob user = null, float? announce = null)
    {
        if (paicard == null)
            return;
        Dm.Deferred("code/modules/mob/living/basic/bots/_bots.dm:738", "ejectpai: the pAI (/mob/living/silicon/pai, its card) is not ported: no card ever sits here");
    }

    /// Ejects the pAI remotely.
    // tg: code/modules/mob/living/basic/bots/_bots.dm:764
    public virtual async DmTask eject_pai_remote(mob user = null)
    {
        if (!Dm.truthy(await allowed__proc(user)) || paicard == null)
            return;
        speak("Ejecting personality chip.", radio_channel);
        await ejectpai(user);
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:770
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        REMOVE_TRAIT(this, TRAIT_NO_GLIDE, INNATE_TRAIT);
        speed = 2;

        diag_hud_set_botmode();
        clear_path_hud();
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:780
    public override async DmTask<object> Logout()
    {
        object dot = await base.Logout();
        await bot_reset();
        speed = Dm.initial(this, (mob_living_basic_bot p) => p.speed);
        ADD_TRAIT(this, TRAIT_NO_GLIDE, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:786
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        object dot = await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        if (!Dm.truthy(dot))
            return dot;
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:792
    public override async DmTask<object> rust_heretic_act(object rust_strength = null)
    {
        await adjust_brute_loss(400);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:796
    [SignalHandler]
    public virtual void retrieve_access(mob bot = null, DmList player_access = null)
    {
        Dm.append(player_access, access_card.GetAccess());
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:800
    public virtual object generate_speak_list()
    {
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:803
    public virtual void provide_additional_access()
    {
        datum_id_trim additional_trim = SSid_access.trim_singletons_by_path[additional_access] as datum_id_trim;
        if (additional_trim == null)
            return;
        access_card.add_access(Dm.add(additional_trim.access, additional_trim.wildcard_access) as DmList);
        initial_access = access_card.access.Copy();
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:811
    public virtual async DmTask<object> summon_bot(atom summoner = null, turf turf_destination = null, object user_access = null, float? grant_all_access = null)
    {
        user_access ??= Dm.list();
        grant_all_access ??= FALSE;
        if (isAI(summoner) && !Dm.truthy(set_ai_caller(summoner as mob_living)))
            return FALSE;
        await bot_reset(bypass_ai_reset: Dm.num(isAI(summoner)));
        turf destination = turf_destination ?? get_turf(summoner);
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BOT_SUMMON_TARGET, destination);
        DmList access_to_grant = Dm.truthy(grant_all_access) ? SSid_access.accesses_by_region[REGION_ALL_STATION] as DmList : Dm.add(user_access, initial_access) as DmList;
        access_card.set_access(access_to_grant);
        speak("Responding.", radio_channel);
        update_bot_mode(new_mode: BOT_SUMMON);
        if (client != null) //if we're sentient, we reset ourselves after a short period
            _addtimer(Dm.@new<datum_callback>(this, nameof(bot_reset)), SENTIENT_BOT_RESET_TIMER, file: "code/modules/mob/living/basic/bots/_bots.dm", line: 822);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:825
    public virtual object set_ai_caller(mob_living ai_caller = null)
    {
        atom calling_ai = calling_ai_ref?.resolve() as atom;
        if (calling_ai != null && calling_ai != this)
            return FALSE;
        calling_ai_ref = WEAKREF(ai_caller) as datum_weakref;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:832
    public virtual void update_bot_mode(object new_mode = null, object update_hud = null)
    {
        update_hud ??= TRUE; // an object: the cleaner's callbacks append their source here, truthy (Appendix A)
        mode = new_mode as string;
        update_appearance();
        if (Dm.truthy(update_hud))
            diag_hud_set_botmode();
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:838
    [SignalHandler]
    public virtual void after_attacked(datum source = null, atom attacker = null, object attack_flags = null)
    {
        if (Dm.truthy(Dm.bitand(attack_flags, ATTACKER_DAMAGING_ATTACK)))
            do_sparks(number: 5, cardinal_only: TRUE, source: this);
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:844
    public virtual object emag_effects(object user = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:847
    public override object get_hit_area_message(object input_area = null)
    {
        // we just get hit, there's no complexity for hitting an arm (if it exists) or anything.
        // we also need to return an empty string as otherwise it would falsely say that we get hit in the chest or something strange like that (bots don't have "chests")
        return "";
    }

    // tg: code/modules/mob/living/basic/bots/_bots.dm:852
    public virtual async DmTask on_bot_movement(atom_movable source = null, atom oldloc = null, object dir = null, object forced = null)
    {
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList mob_living_basic_bot__on_toggle_traits;
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/secbot/secbot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3); the fine came with the economy (gaps P4a).

// tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:1
public partial class mob_living_basic_bot_secbot : mob_living_basic_bot
{
    // (a class with a hand-declared var keeps its constructor by hand, types-converter.md)
    public obj_item weapon; // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:37 (hand-declared: an instance)

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:2
    public mob_living_basic_bot_secbot()
    {
        name = Dm.text("\\improper Securitron");
        desc = "A little security robot. He looks less than thrilled.";
        icon = Dm.resource("icons/mob/silicon/aibots.dmi");
        icon_state = "secbot";
        base_icon_state = "secbot";
        light_color = "#f56275";
        light_power = 0.8f;
        gender = "male";
        density = 0;
        anchored = 0;
        health = 25;
        maxHealth = 25;
        pass_flags = 528;
        combat_mode = 1;
        can_buckle_to = 0;
        radio_key = typeof(obj_item_encryptionkey_secbot);
        radio_channel = "Security";
        bot_type = "Securitron";
        bot_mode_flags = 16777207;
        data_hud_type = "sec_hud";
        hackables = "target identification systems";
        path_image_color = "#FF0000";
        possessed_message = "You are a securitron! Guard the station to the best of your ability!";
        additional_access = typeof(datum_id_trim_job_detective);
        ai_controller = typeof(datum_ai_controller_basic_controller_bot_secbot);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:14
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        physiology = Dm.list(Dm.kv("brute", 0.5f), Dm.kv("burn", 0.7f), Dm.kv("toxin", 0), Dm.kv("oxygen", 0), Dm.kv("stamina", 0)); // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:14
        req_one_access = Dm.list("security"); // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:19
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), 620), Dm.kv(typeof(datum_material_glass), 32)); // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:30
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:57
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        weapon = Dm.@new(baton_type, this) as obj_item;
        update_appearance(UPDATE_ICON);
        if (Dm.truthy(commissioned))
            ADD_TRAIT(this, TRAIT_COMMISSIONED, INNATE_TRAIT);

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), TgState.Current.mob_living_basic_bot_secbot__loc_connections ??= Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_security_vision), Dm.named("judgement_criteria", NONE), Dm.named("update_judgement_criteria", Dm.@new<datum_callback>(this, nameof(judgement_criteria)))));
        Waiting.NoSleep(add_arrest_component());
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:72
    public override object Destroy(float? force = null)
    {
        qdel(weapon);
        weapon = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:76
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.eq(mode, BOT_HUNT))
            icon_state = Dm.text("[]-c", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:81
    public override async DmTask turn_off()
    {
        await base.turn_off();
        update_bot_mode(new_mode: BOT_IDLE);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:85
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        object dot = await base.on_saboteur(source, disrupt_duration);
        if (!Dm.truthy(Dm.bitand(security_mode_flags, SECBOT_SABOTEUR_AFFECTED)))
        {
            security_mode_flags = Dm.bitor(security_mode_flags, SECBOT_SABOTEUR_AFFECTED);
            _addtimer(Dm.@new<datum_callback>(this, nameof(remove_saboteur_effect)), Dm.num(disrupt_duration), file: "code/modules/mob/living/basic/bots/secbot/secbot.dm", line: 89);
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:92
    public virtual void remove_saboteur_effect()
    {
        security_mode_flags = Dm.bitand(security_mode_flags, Dm.bitnot(SECBOT_SABOTEUR_AFFECTED));
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:95
    public override async DmTask<object> electrocute_act(object shock_damage = null, object source = null, float? siemens_coeff = null, float? flags = null, params object[] __extra) //shocks only make him angry
    {
        if (speed >= Dm.initial(this, (mob_living_basic_bot_secbot p) => p.speed) + 3)
            return null;
        speed += 3;
        _addtimer(Dm.@new<datum_callback>(this, nameof(speed__varset), speed - 3), 6 * SECONDS, file: "code/modules/mob/living/basic/bots/secbot/secbot.dm", line: 99);
        playsound(this, Dm.resource("sound/machines/defib/defib_zap.ogg"), 50);
        visible_message(span_warning(Dm.text("[] shakes and speeds up!", this)));
        return null;
    }

    // VARSET_CALLBACK(src, speed, ...) (__HELPERS/varset_callback.dm:3): ___callbackvarset is not declared (as powernet.cs's flickering__varset)
    public virtual void speed__varset(object value = null)
    {
        speed = Dm.num(value);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:103
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == weapon)
        {
            weapon = null;
            update_appearance();
        }
        return dot;
    }

    // Variables sent to TGUI
    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:110
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) || HAS_SILICON_ACCESS(user))
        {
            DmList custom = data["custom_controls"] as DmList;
            custom["check_id"] = Dm.bitand(security_mode_flags, SECBOT_CHECK_IDS);
            custom["check_weapons"] = Dm.bitand(security_mode_flags, SECBOT_CHECK_WEAPONS);
            custom["check_warrants"] = Dm.bitand(security_mode_flags, SECBOT_CHECK_RECORDS);
            custom["handcuff_targets"] = Dm.bitand(security_mode_flags, SECBOT_HANDCUFF_TARGET);
            custom["arrest_alert"] = Dm.bitand(security_mode_flags, SECBOT_DECLARE_ARRESTS);
        }
        return data;
    }

    // Actions received from TGUI
    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:121
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        if (Dm.truthy(dot) || (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !HAS_SILICON_ACCESS(user)))
            return dot;

        switch (action as string)
        {
            case "check_id":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_CHECK_IDS);
                return TRUE;
            case "check_weapons":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_CHECK_WEAPONS);
                return TRUE;
            case "check_warrants":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_CHECK_RECORDS);
                return TRUE;
            case "handcuff_targets":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_HANDCUFF_TARGET);
                return TRUE;
            case "arrest_alert":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_DECLARE_ARRESTS);
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:145
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        // Turns an oversight into a feature. Beepsky will now announce when pacifists taunt him over sec comms.
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            user.visible_message(span_notice(Dm.text("[] taunts [], daring [] to give chase!", user, this, p_them())),
                span_notice(Dm.text("You taunt [], daring [] to chase you!", this, p_them())), span_hear("You hear someone shout a daring taunt!"), DEFAULT_MESSAGE_RANGE, user);
            speak(Dm.text("Taunted by pacifist scumbag [] in [].", RUNECHAT_BOLD(user), get_area(this)), radio_channel);

            // Interrupt the attack chain. We've already handled this scenario for pacifists.
            return null;
        }

        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:158
    public virtual void retrieve_emag_message()
    {
        audible_message(span_danger(Dm.text("[] buzzes oddly!", this)));
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:161
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
            return null;
        if (user != null)
            balloon_alert(user, "target assessment circuits shorted");

        retrieve_emag_message();
        security_mode_flags = Dm.bitand(security_mode_flags, Dm.bitnot(SECBOT_DECLARE_ARRESTS));
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:173
    public virtual void post_arrest(mob_living_carbon current_target = null)
    {
        playsound(this, SFX_LAW, 50, FALSE);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:176
    public virtual async DmTask post_stun(mob_living_carbon current_target = null, float? harm = null)
    {
        flick(Dm.text("[]-c", base_icon_state), this);
        object threat = (ai_controller as datum_ai_controller).blackboard[BB_CURRENT_CRIMINAL_ASSESSMENT] ?? 5;
        if (Dm.truthy(Dm.bitand(security_mode_flags, SECBOT_DECLARE_ARRESTS)))
        {
            area location = get_area(this) as area;
            speak(Dm.text("[] level [] scumbag [] in [].", Dm.truthy(Dm.bitand(security_mode_flags, SECBOT_HANDCUFF_TARGET)) ? "Arresting" : "Detaining", threat, RUNECHAT_BOLD(current_target), location), radio_channel);
        }
        await payment_check(current_target as mob_living_carbon_human);
        update_bot_mode(new_mode: BOT_PREP_ARREST);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:185
    public override async DmTask explode()
    {
        atom drop_location = this.drop_location() as atom;
        await retrieve_secbot_drops(drop_location);
        Dm.@new<obj_effect_decal_cleanable_blood_oil>(loc);
        await base.explode();
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:191
    public virtual async DmTask retrieve_secbot_drops(atom drop_location = null)
    {
        obj_item_bot_assembly_secbot secbot_assembly = Dm.@new<obj_item_bot_assembly_secbot>(drop_location);
        secbot_assembly.build_step = ASSEMBLY_FIRST_STEP;
        secbot_assembly.add_overlay("hs_hole");
        secbot_assembly.created_name = name;
        Dm.@new<obj_item_assembly_prox_sensor>(drop_location);
        await drop_part(weapon, drop_location);
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:199
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable to_be_tripped = null)
    {
        mob_living possible_target = (ai_controller as datum_ai_controller).blackboard[BB_CURRENT_TARGET] as mob_living;
        if (!Dm.truthy(has_gravity()) || !ismob(to_be_tripped) || possible_target == null)
            return;
        if (to_be_tripped is mob_living_carbon tripped_mob && !Dm.truthy(in_range(this, possible_target)))
            Waiting.NoSleep(tripped_mob.knockOver(tripped_mob));
    }

    /// Returns true if the current target is unable to pay to be detained/arrested
    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:208
    public virtual async DmTask<object> payment_check(mob_living_carbon_human human_target = null)
    {
        float fair_market_price = Dm.truthy(Dm.bitand(security_mode_flags, SECBOT_HANDCUFF_TARGET)) ? price_arrest : price_detain;
        if (fair_market_price <= 0)
            return FALSE;
        if (!ishuman(human_target))
            return FALSE;
        var target_id = human_target.get_idcard() as obj_item_card_id;
        if (target_id == null)
        {
            await say(I18n.T("Unable to pay fine: No ID card found."));
            return TRUE;
        }
        datum_bank_account insurance = target_id.registered_account;
        if (insurance == null)
        {
            await say(I18n.T("Unable to pay fine: No bank account found."));
            return TRUE;
        }
        if (!Dm.truthy(insurance.adjust_money(-fair_market_price, I18n.T("Securitron fine"))))
        {
            await say(I18n.T("Unable to pay fine: Not enough funds in account."));
            return TRUE;
        }

        SSeconomy.get_dep_account(payment_department)?.adjust_money(fair_market_price);
        await say(I18n.T("Fine paid: Thank you for your compliance. Your account been charged [] [].", fair_market_price, MONEY_NAME));
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:231
    public override object generate_speak_list()
    {
        return TgState.Current.mob_living_basic_bot_secbot__secbot_lines ??= Dm.list(
            Dm.kv(BEEPSKY_VOICED_CRIMINAL_DETECTED, Dm.resource("sound/mobs/non-humanoids/beepsky/criminal.ogg")),
            Dm.kv(BEEPSKY_VOICED_FREEZE, Dm.resource("sound/mobs/non-humanoids/beepsky/freeze.ogg")),
            Dm.kv(BEEPSKY_VOICED_JUSTICE, Dm.resource("sound/mobs/non-humanoids/beepsky/justice.ogg")),
            Dm.kv(BEEPSKY_VOICED_YOUR_MOVE, Dm.resource("sound/mobs/non-humanoids/beepsky/creep.ogg")),
            Dm.kv(BEEPSKY_VOICED_I_AM_THE_LAW, Dm.resource("sound/mobs/non-humanoids/beepsky/iamthelaw.ogg")),
            Dm.kv(BEEPSKY_VOICED_SECURE_DAY, Dm.resource("sound/mobs/non-humanoids/beepsky/secureday.ogg")));
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:243
    public virtual object judgement_criteria()
    {
        float final = FALSE;
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
            final = Dm.bitor(final, JUDGE_EMAGGED);
        if (Dm.truthy(Dm.bitand(security_mode_flags, SECBOT_CHECK_IDS)))
            final = Dm.bitor(final, JUDGE_IDCHECK);
        if (Dm.truthy(Dm.bitand(security_mode_flags, SECBOT_CHECK_RECORDS)))
            final = Dm.bitor(final, JUDGE_RECORDCHECK);
        if (Dm.truthy(Dm.bitand(security_mode_flags, SECBOT_CHECK_WEAPONS)))
            final = Dm.bitor(final, JUDGE_WEAPONCHECK);
        if (Dm.truthy(Dm.bitand(security_mode_flags, SECBOT_SABOTEUR_AFFECTED)))
            final = Dm.bitor(final, JUDGE_CHILLOUT);
        return final;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot.dm:257
    public virtual async DmTask add_arrest_component()
    {
        await AddComponent(typeof(datum_component_stun_n_cuff),
            Dm.named("stun_sound", stun_sound), Dm.named("post_stun_callback", Dm.@new<datum_callback>(this, nameof(post_stun))), Dm.named("post_arrest_callback", Dm.@new<datum_callback>(this, nameof(post_arrest))), Dm.named("handcuff_type", cuff_type));
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList mob_living_basic_bot_secbot__loc_connections;
    internal DmList mob_living_basic_bot_secbot__secbot_lines;
}

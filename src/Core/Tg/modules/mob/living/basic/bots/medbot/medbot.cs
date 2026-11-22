// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/medbot/medbot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the emissive overlays (DV12), the combat logs and the nukie's tactical map (gaps package C3); the linked techweb
// and its research upgrades since silicons SI3 (D326).

// tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:3
public partial class mob_living_basic_bot_medbot : mob_living_basic_bot
{
    // (a class with a hand-declared var keeps its constructor by hand, types-converter.md)
    public object health_analyzer = typeof(obj_item_healthanalyzer); // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:98 (hand-declared: a type, or the instance a crafted bot holds)
    public object medkit_type = typeof(obj_item_storage_medkit); // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:100 (hand-declared, as above)

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:4
    public mob_living_basic_bot_medbot()
    {
        name = Dm.text("\\improper Medibot");
        desc = "A little medical robot. He looks somewhat underwhelmed.";
        icon = Dm.resource("icons/mob/silicon/aibots.dmi");
        icon_state = "medbot_generic_idle";
        base_icon_state = "medbot";
        health = 20;
        maxHealth = 20;
        speed = 2;
        light_power = 0.8f;
        light_color = "#99ccff";
        pass_flags = 528;
        status_flags = 1;
        ai_controller = typeof(datum_ai_controller_basic_controller_bot_medbot);
        radio_key = typeof(obj_item_encryptionkey_headset_med);
        radio_channel = "Medical";
        bot_type = "Medibot";
        data_hud_type = "med_hud";
        hackables = "health processor circuits";
        possessed_message = "You are a medbot! Ensure good health among the crew to the best of your ability!";
        additional_access = typeof(datum_id_trim_medibot);
        announcement_type = typeof(datum_action_cooldown_bot_announcement_medbot);
        path_image_color = "#d9d9f4";
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:17
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), 630), Dm.kv(typeof(datum_material_glass), 25)); // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:17
        req_one_access = Dm.list("robotics", "medical"); // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:19
    }

    private const string TEND_DAMAGE_INTERACTION = "tend_damage_interaction";

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:35
    internal static DmList wait_announcements => TgState.Current.mob_living_basic_bot_medbot__wait_announcements ??= Dm.list(
        Dm.kv(MEDIBOT_VOICED_HOLD_ON, Dm.resource("sound/mobs/non-humanoids/medbot/coming.ogg")),
        Dm.kv(MEDIBOT_VOICED_WANT_TO_HELP, Dm.resource("sound/mobs/non-humanoids/medbot/help.ogg")),
        Dm.kv(MEDIBOT_VOICED_YOU_ARE_INJURED, Dm.resource("sound/mobs/non-humanoids/medbot/injured.ogg")));
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:42
    internal static DmList afterheal_announcements => TgState.Current.mob_living_basic_bot_medbot__afterheal_announcements ??= Dm.list(
        Dm.kv(MEDIBOT_VOICED_ALL_PATCHED_UP, Dm.resource("sound/mobs/non-humanoids/medbot/patchedup.ogg")),
        Dm.kv(MEDIBOT_VOICED_APPLE_A_DAY, Dm.resource("sound/mobs/non-humanoids/medbot/apple.ogg")),
        Dm.kv(MEDIBOT_VOICED_FEEL_BETTER, Dm.resource("sound/mobs/non-humanoids/medbot/feelbetter.ogg")));
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:49
    internal static DmList near_death_announcements => TgState.Current.mob_living_basic_bot_medbot__near_death_announcements ??= Dm.list(
        Dm.kv(MEDIBOT_VOICED_STAY_WITH_ME, Dm.resource("sound/mobs/non-humanoids/medbot/no.ogg")),
        Dm.kv(MEDIBOT_VOICED_LIVE, Dm.resource("sound/mobs/non-humanoids/medbot/live.ogg")),
        Dm.kv(MEDIBOT_VOICED_NEVER_LOST, Dm.resource("sound/mobs/non-humanoids/medbot/lost.ogg")));
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:55
    internal static DmList idle_lines => TgState.Current.mob_living_basic_bot_medbot__idle_lines ??= Dm.list(
        Dm.kv(MEDIBOT_VOICED_DELICIOUS, Dm.resource("sound/mobs/non-humanoids/medbot/delicious.ogg")),
        Dm.kv(MEDIBOT_VOICED_PLASTIC_SURGEON, Dm.resource("sound/mobs/non-humanoids/medbot/surgeon.ogg")),
        Dm.kv(MEDIBOT_VOICED_MASK_ON, Dm.resource("sound/mobs/non-humanoids/medbot/radar.ogg")),
        Dm.kv(MEDIBOT_VOICED_ALWAYS_A_CATCH, Dm.resource("sound/mobs/non-humanoids/medbot/catch.ogg")),
        Dm.kv(MEDIBOT_VOICED_LIKE_FLIES, Dm.resource("sound/mobs/non-humanoids/medbot/flies.ogg")),
        Dm.kv(MEDIBOT_VOICED_SUFFER, Dm.resource("sound/mobs/non-humanoids/medbot/why.ogg")));
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:64
    internal static DmList emagged_announcements => TgState.Current.mob_living_basic_bot_medbot__emagged_announcements ??= Dm.list(
        Dm.kv(MEDIBOT_VOICED_FUCK_YOU, Dm.resource("sound/mobs/non-humanoids/medbot/fuck_you.ogg")),
        Dm.kv(MEDIBOT_VOICED_NOT_A_GAME, Dm.resource("sound/mobs/non-humanoids/medbot/turn_off.ogg")),
        Dm.kv(MEDIBOT_VOICED_IM_DIFFERENT, Dm.resource("sound/mobs/non-humanoids/medbot/im_different.ogg")),
        Dm.kv(MEDIBOT_VOICED_FOURTH_WALL, Dm.resource("sound/mobs/non-humanoids/medbot/close.ogg")),
        Dm.kv(MEDIBOT_VOICED_SHINDEMASHOU, Dm.resource("sound/mobs/non-humanoids/medbot/shindemashou.ogg")));
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:72
    internal static DmList tipped_announcements => TgState.Current.mob_living_basic_bot_medbot__tipped_announcements ??= Dm.list(
        Dm.kv(MEDIBOT_VOICED_WAIT, Dm.resource("sound/mobs/non-humanoids/medbot/hey_wait.ogg")),
        Dm.kv(MEDIBOT_VOICED_DONT, Dm.resource("sound/mobs/non-humanoids/medbot/please_dont.ogg")),
        Dm.kv(MEDIBOT_VOICED_TRUSTED_YOU, Dm.resource("sound/mobs/non-humanoids/medbot/i_trusted_you.ogg")),
        Dm.kv(MEDIBOT_VOICED_NO_SAD, Dm.resource("sound/mobs/non-humanoids/medbot/nooo.ogg")),
        Dm.kv(MEDIBOT_VOICED_OH_FUCK, Dm.resource("sound/mobs/non-humanoids/medbot/oh_fuck.ogg")));
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:80
    internal static DmList untipped_announcements => TgState.Current.mob_living_basic_bot_medbot__untipped_announcements ??= Dm.list(
        Dm.kv(MEDIBOT_VOICED_FORGIVE, Dm.resource("sound/mobs/non-humanoids/medbot/forgive.ogg")),
        Dm.kv(MEDIBOT_VOICED_THANKS, Dm.resource("sound/mobs/non-humanoids/medbot/thank_you.ogg")),
        Dm.kv(MEDIBOT_VOICED_GOOD_PERSON, Dm.resource("sound/mobs/non-humanoids/medbot/youre_good.ogg")));
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:86
    internal static DmList worried_announcements => TgState.Current.mob_living_basic_bot_medbot__worried_announcements ??= Dm.list(
        Dm.kv(MEDIBOT_VOICED_PUT_BACK, Dm.resource("sound/mobs/non-humanoids/medbot/please_put_me_back.ogg")),
        Dm.kv(MEDIBOT_VOICED_IM_SCARED, Dm.resource("sound/mobs/non-humanoids/medbot/please_im_scared.ogg")),
        Dm.kv(MEDIBOT_VOICED_NEED_HELP, Dm.resource("sound/mobs/non-humanoids/medbot/dont_like.ogg")),
        Dm.kv(MEDIBOT_VOICED_THIS_HURTS, Dm.resource("sound/mobs/non-humanoids/medbot/pain_is_real.ogg")),
        Dm.kv(MEDIBOT_VOICED_THE_END, Dm.resource("sound/mobs/non-humanoids/medbot/is_this_the_end.ogg")),
        Dm.kv(MEDIBOT_VOICED_NOOO, Dm.resource("sound/mobs/non-humanoids/medbot/nooo.ogg")));
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:94
    internal static DmList misc_announcements => TgState.Current.mob_living_basic_bot_medbot__misc_announcements ??= Dm.list(Dm.kv(MEDIBOT_VOICED_CHICKEN, Dm.resource("sound/mobs/non-humanoids/medbot/i_am_chicken.ogg")));

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:121
    public virtual void set_speech_keys()
    {
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        if (controller == null)
            return;
        controller.set_blackboard_key(BB_NEAR_DEATH_SPEECH, near_death_announcements);
        controller.set_blackboard_key(BB_WAIT_SPEECH, wait_announcements);
        controller.set_blackboard_key(BB_AFTERHEAL_SPEECH, afterheal_announcements);
        controller.set_blackboard_key(BB_IDLE_SPEECH, idle_lines);
        controller.set_blackboard_key(BB_EMAGGED_SPEECH, emagged_announcements);
        controller.set_blackboard_key(BB_WORRIED_ANNOUNCEMENTS, worried_announcements);
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:131
    [DmParams("mapload", "new_skin")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_skin = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        set_speech_keys();

        if (new_skin != null)
        {
            skin = new_skin as string;
            update_appearance();
        }
        Waiting.NoSleep(AddComponent(typeof(datum_component_tippable),
            Dm.named("tip_time", 3 * SECONDS), Dm.named("untip_time", 3 * SECONDS), Dm.named("self_right_time", 3.5f * MINUTES), Dm.named("pre_tipped_callback", Dm.@new<datum_callback>(this, nameof(pre_tip_over))), Dm.named("post_tipped_callback", Dm.@new<datum_callback>(this, nameof(after_tip_over))), Dm.named("post_untipped_callback", Dm.@new<datum_callback>(this, nameof(after_righted)))));

        Waiting.NoSleep(AddComponent(typeof(datum_component_defaceable),
            Dm.named("drawing_of", "eyes"), Dm.named("on_defaced", Dm.@new<datum_callback>(this, nameof(on_defaced)))));

        DmList hat_offsets = TgState.Current.mob_living_basic_bot_medbot__hat_offsets ??= Dm.list(4, -9);
        DmList remove_hat = TgState.Current.mob_living_basic_bot_medbot__remove_hat ??= Dm.list(SIGNAL_ADDTRAIT(TRAIT_MOB_TIPPED));
        DmList prevent_checks = TgState.Current.mob_living_basic_bot_medbot__prevent_checks ??= Dm.list(TRAIT_MOB_TIPPED);
        Waiting.NoSleep(AddElement(typeof(datum_element_hat_wearer), hat_offsets, remove_hat, prevent_checks));
        RegisterSignal(this, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(pre_attack));

        if (!HAS_TRAIT(SSstation, STATION_TRAIT_MEDBOT_MANIA) || !Dm.truthy(mapload) || !Dm.truthy(is_station_level(z)))
            return INITIALIZE_HINT_LATELOAD;

        skin = "adv";
        update_appearance();
        damage_type_healer = HEAL_ALL_DAMAGE;
        if (prob(50))
            name += ", PhD.";

        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:172
    public override object Destroy(float? force = null)
    {
        if (!ispath(health_analyzer))
        {
            qdel(health_analyzer as datum);
            health_analyzer = null;
        }
        if (!ispath(medkit_type))
        {
            qdel(medkit_type as datum);
            medkit_type = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:179
    public override void LateInitialize(params object[] __extra)
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && linked_techweb == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref linked_techweb, this);

        if (linked_techweb != null)
        {
            RegisterSignal(linked_techweb, COMSIG_TECHWEB_ADD_DESIGN, nameof(on_techweb_research));
            RegisterSignal(linked_techweb, COMSIG_TECHWEB_REMOVE_DESIGN, nameof(on_techweb_unresearch));

            foreach (datum_design_medibot_upgrade design in Dm.each<datum_design_medibot_upgrade>(linked_techweb.get_researched_design_datums()))
                heal_multiplier += Dm.num(design.additive_multiplier);
        }
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:190
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        obj_item_storage_medkit medkit = Dm.locate<obj_item_storage_medkit>(contents);
        medkit_type = medkit;
        health_analyzer = Dm.locate<obj_item_healthanalyzer>(contents);
        skin = medkit.get_medbot_skin() as string;
        object healed = Dm.initial(medkit, (obj_item_storage_medkit p) => p.damagetype_healed);
        damage_type_healer = Dm.truthy(healed) ? healed as string : BRUTE;
        update_appearance();
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:199
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);

        string mode_suffix = Dm.eq(mode, BOT_HEALING) ? "active" : "idle";
        icon_state = Dm.text("[]_[]_[]", base_icon_state, skin, mode_suffix);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:205
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;

        if (!Dm.truthy(Dm.bitand(medical_mode_flags, MEDBOT_STATIONARY_MODE)))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_overlay_wheels", base_icon_state)));

        string mode_suffix = Dm.eq(mode, BOT_HEALING) ? "active" : "idle";
        if (HAS_TRAIT(this, TRAIT_DEFACED))
        {
            mutable_appearance face = mutable_appearance(Dm.resource("icons/mob/silicon/aibot_faces.dmi"), Dm.text("medbot_[]", mode_suffix)) as mutable_appearance;
            face.color = painted_face_colour;
            Dm.append(ref dot, face);
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/mob/silicon/aibot_faces.dmi"), Dm.text("medbot_highlight_[]", mode_suffix)));
        }
        else
        {
            if (HAS_TRAIT(this, TRAIT_INCAPACITATED))
            {
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_overlay_incapacitated", base_icon_state)));
                // emissive_appearance: DV12
            }
            else if (Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
            {
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_overlay_on_[]", base_icon_state, mode_suffix)));
                // emissive_appearance: DV12
            }
        }
        return dot;
    }

    /// Set our drawn-on ink colour
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:226
    [SignalHandler]
    public virtual void on_defaced(object ink_colour = null)
    {
        painted_face_colour = ink_colour as string;
    }

    //this is sin
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:231
    public override object generate_speak_list()
    {
        return TgState.Current.mob_living_basic_bot_medbot__finalized_speak_list ??= Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(idle_lines, wait_announcements), afterheal_announcements), near_death_announcements), emagged_announcements), tipped_announcements), untipped_announcements), worried_announcements), misc_announcements) as DmList;
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:236
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:239
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (tool is obj_item_multitool multitool && !QDELETED(multitool.buffer as datum) && multitool.buffer is datum_techweb techweb)
            linked_techweb = techweb;
        return ITEM_INTERACT_SUCCESS;
    }

    // Variables sent to TGUI
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:245
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) || HAS_SILICON_ACCESS(user))
        {
            DmList custom = data["custom_controls"] as DmList;
            custom["heal_threshold"] = heal_threshold;
            custom["speaker"] = Dm.bitand(medical_mode_flags, MEDBOT_SPEAK_MODE);
            custom["crit_alerts"] = Dm.bitand(medical_mode_flags, MEDBOT_DECLARE_CRIT);
            custom["stationary_mode"] = Dm.bitand(medical_mode_flags, MEDBOT_STATIONARY_MODE);
        }
        return data;
    }

    // Actions received from TGUI
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:255
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        if (Dm.truthy(dot) || !isliving(ui.user) || (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !HAS_SILICON_ACCESS(user)))
            return dot;
        switch (action as string)
        {
            case "heal_threshold":
                float adjust_num = Dm.num(round(Dm.num(text2num(@params["threshold"]))));
                heal_threshold = adjust_num;
                if (heal_threshold < 5)
                    heal_threshold = 5;
                if (heal_threshold > 75)
                    heal_threshold = 75;
                break;
            case "speaker":
                medical_mode_flags = Dm.bitxor(medical_mode_flags, MEDBOT_SPEAK_MODE);
                break;
            case "crit_alerts":
                medical_mode_flags = Dm.bitxor(medical_mode_flags, MEDBOT_DECLARE_CRIT);
                break;
            case "stationary_mode":
                medical_mode_flags = Dm.bitxor(medical_mode_flags, MEDBOT_STATIONARY_MODE);
                break;
        }

        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:277
    public override object emag_effects(object user = null)
    {
        medical_mode_flags = Dm.bitand(medical_mode_flags, Dm.bitnot(MEDBOT_DECLARE_CRIT));
        balloon_alert(user as mob, "reagent synthesis circuits shorted");
        audible_message(span_danger(Dm.text("[] buzzes oddly!", this)));
        flick_overlay_view(mutable_appearance(icon, Dm.text("[]_spark", base_icon_state)), 1 * SECONDS);
        playsound(this, SFX_SPARKS, 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:285
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(Dm.bitand(medical_mode_flags, MEDBOT_TIPPED_MODE)))
            return dot;
        DmList panic_state = TgState.Current.mob_living_basic_bot_medbot__panic_state ??= Dm.list(
            "It appears to be tipped over, and is quietly waiting for someone to set it right.",
            "It is tipped over and requesting help.",
            "They are tipped over and appear visibly distressed.",
            span_warning("They are tipped over and visibly panicking!"),
            span_warning(span_bold("They are freaking out from being tipped over!")));
        Dm.append(ref dot, pick(panic_state));
        return dot;
    }

    /*
     * Proc used in a callback for before this medibot is tipped by the tippable component.
     *
     * user - the mob who is tipping us over
     */
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:302
    public virtual void pre_tip_over(mob user = null)
    {
        speak(pick(worried_announcements));
    }

    /*
     * Proc used in a callback for after this medibot is tipped by the tippable component.
     *
     * user - the mob who tipped us over
     */
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:310
    public virtual void after_tip_over(mob user = null)
    {
        medical_mode_flags = Dm.bitor(medical_mode_flags, MEDBOT_TIPPED_MODE);
        tipper = WEAKREF(user) as datum_weakref;
        playsound(this, Dm.resource("sound/machines/warning-buzzer.ogg"), 50);
        if (prob(10))
            speak(Dm.text("PSYCH ALERT: Crewmember [] recorded displaying antisocial tendencies torturing bots in []. Please schedule psych evaluation.", user.name, get_area(this)), radio_channel);
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:317
    public override async DmTask explode()
    {
        atom our_loc = drop_location() as atom;
        await drop_part(medkit_type, our_loc);
        await drop_part(health_analyzer, our_loc);
        await base.explode();
    }

    /*
     * Proc used in a callback for after this medibot is righted, either by themselves or by a mob, by the tippable component.
     *
     * user - the mob who righted us. Can be null.
     */
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:328
    public virtual void after_righted(mob user = null)
    {
        mob tipper_mob = user == null ? null : tipper?.resolve() as mob;
        tipper = null;
        medical_mode_flags = Dm.bitand(medical_mode_flags, Dm.bitnot(MEDBOT_TIPPED_MODE));
        if (tipper_mob == null)
            return;
        if (tipper_mob == user)
        {
            speak(MEDIBOT_VOICED_FORGIVE);
            return;
        }
        speak(pick(untipped_announcements));
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:339
    [SignalHandler]
    public virtual object pre_attack(mob_living puncher = null, atom target = null)
    {
        if (HAS_TRAIT(this, TRAIT_HANDS_BLOCKED))
            return null;
        if (!iscarbon(target))
            return null;
        Waiting.Detach(medicate_patient(target as mob_living_carbon_human));
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:349
    public virtual async DmTask medicate_patient(mob_living_carbon_human patient = null)
    {
        if (Dm.truthy(DOING_INTERACTION(this, TEND_DAMAGE_INTERACTION)))
            return;

        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
        {
            if ((Dm.eq(damage_type_healer, HEAL_ALL_DAMAGE) && Dm.num(patient.get_total_damage()) <= heal_threshold) || (!Dm.eq(damage_type_healer, HEAL_ALL_DAMAGE) && Dm.num(patient.get_current_damage_of_type(damage_type_healer)) <= heal_threshold))
            {
                to_chat(this, Dm.text("[] is healthy! Your programming prevents you from tending the wounds of anyone with less than [] [] damage.", patient, heal_threshold + 1, Dm.eq(damage_type_healer, HEAL_ALL_DAMAGE) ? "total" : damage_type_healer));
                return;
            }
        }

        update_bot_mode(new_mode: BOT_HEALING, update_hud: FALSE);
        patient.visible_message(Dm.text("[] is trying to tend the wounds of []", this, patient), span_userdanger(Dm.text("[] is trying to tend your wounds!", this)));
        if (!Dm.truthy(await do_after(this, delay: 2 * SECONDS, target: patient, interaction_key: TEND_DAMAGE_INTERACTION)))
        {
            update_bot_mode(new_mode: BOT_IDLE);
            return;
        }
        float modified_heal_amount = heal_amount * heal_multiplier;
        bool done_healing = false;
        if (Dm.eq(damage_type_healer, BRUTE) && Dm.eq(medkit_type, typeof(obj_item_storage_medkit_brute)))
            modified_heal_amount *= 1.1f;
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
        {
            if (patient.reagents is datum_reagents patient_reagents)
                await patient_reagents.add_reagent(typeof(datum_reagent_toxin_chloralhydrate), 5);
            log_combat(this, patient, "pretended to tend wounds on", "internal tools");
        }
        else if (Dm.eq(damage_type_healer, HEAL_ALL_DAMAGE))
        {
            await patient.heal_ordered_damage(amount: modified_heal_amount, damage_types: Dm.list(BRUTE, BURN, TOX, OXY));
            log_combat(this, patient, "tended the wounds of", "internal tools");
            if (Dm.num(patient.get_total_damage()) <= heal_threshold)
                done_healing = true;
        }
        else
        {
            await patient.heal_damage_type(heal_amount: modified_heal_amount, damagetype: damage_type_healer);
            log_combat(this, patient, "tended the wounds of", "internal tools");
            if (Dm.num(patient.get_current_damage_of_type(damage_type_healer)) <= heal_threshold)
                done_healing = true;
        }

        patient.visible_message(span_notice(Dm.text("[] tends the wounds of []!", this, patient)), span_infoplain(span_green(Dm.text("[] tends your wounds!", this))));

        if (done_healing)
        {
            visible_message(span_infoplain(Dm.text("[] places [] tools back into [].", this, p_their(), p_themselves())));
            to_chat(this, Dm.text("[] is now healthy!", patient));
            update_bot_mode(new_mode: BOT_IDLE);
            return;
        }

        if (Dm.truthy(await patient.IsReachableBy(this)))
            await melee_attack(patient);
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:392
    [SignalHandler]
    public virtual void on_techweb_research(datum source = null, datum_design_medibot_upgrade design = null)
    {
        if (design == null)
            return;

        heal_multiplier += Dm.num(design.additive_multiplier);
        speak(Dm.text("New knowledge found! Surgical efficacy improved to []%!", round(heal_multiplier * 100)));
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:401
    [SignalHandler]
    public virtual void on_techweb_unresearch(datum source = null, datum_design_medibot_upgrade design = null)
    {
        if (design == null)
            return;

        heal_multiplier -= Dm.num(design.additive_multiplier);
        speak(Dm.text("Error! Surgical efficacy decreased to []%!", round(heal_multiplier * 100)));
    }
}

// tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:460
public partial class mob_living_basic_bot_medbot_nukie : mob_living_basic_bot_medbot
{
    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:476
    [DmParams("mapload", "new_skin")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/mob/living/basic/bots/medbot/medbot.dm:478", "Initialize: the nuclear operatives' tactical map action and the minimap blip (nuke ops)");
        RegisterSignal(SSdcs, COMSIG_GLOB_NUKE_DEVICE_DISARMED, nameof(nuke_disarm));
        RegisterSignal(SSdcs, COMSIG_GLOB_NUKE_DEVICE_ARMED, nameof(nuke_arm));
        RegisterSignal(SSdcs, COMSIG_GLOB_NUKE_DEVICE_DETONATING, nameof(nuke_detonate));
        internal_radio.set_frequency(FREQ_SYNDICATE);
        internal_radio.freqlock = RADIO_FREQENCY_LOCKED;
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:487
    [SignalHandler]
    public virtual void nuke_disarm()
    {
        speak(pick(untipped_announcements));
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:492
    [SignalHandler]
    public virtual void nuke_arm()
    {
        speak(pick(worried_announcements));
    }

    // tg: code/modules/mob/living/basic/bots/medbot/medbot.dm:497
    [SignalHandler]
    public virtual void nuke_detonate()
    {
        speak(pick(emagged_announcements));
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList mob_living_basic_bot_medbot__wait_announcements;
    internal DmList mob_living_basic_bot_medbot__afterheal_announcements;
    internal DmList mob_living_basic_bot_medbot__near_death_announcements;
    internal DmList mob_living_basic_bot_medbot__idle_lines;
    internal DmList mob_living_basic_bot_medbot__emagged_announcements;
    internal DmList mob_living_basic_bot_medbot__tipped_announcements;
    internal DmList mob_living_basic_bot_medbot__untipped_announcements;
    internal DmList mob_living_basic_bot_medbot__worried_announcements;
    internal DmList mob_living_basic_bot_medbot__misc_announcements;
    internal DmList mob_living_basic_bot_medbot__hat_offsets;
    internal DmList mob_living_basic_bot_medbot__remove_hat;
    internal DmList mob_living_basic_bot_medbot__prevent_checks;
    internal DmList mob_living_basic_bot_medbot__finalized_speak_list;
    internal DmList mob_living_basic_bot_medbot__panic_state;
}

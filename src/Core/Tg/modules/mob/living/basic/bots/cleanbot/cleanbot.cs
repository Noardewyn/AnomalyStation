// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3) but the achievement of ascension (awards).

// tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:3
public partial class mob_living_basic_bot_cleanbot : mob_living_basic_bot
{
    // (a class with a hand-declared var keeps its constructor by hand, types-converter.md)
    public obj_item weapon; // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:35 (hand-declared: an instance, the generator read another type's var of the same name)

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:4
    public mob_living_basic_bot_cleanbot()
    {
        name = Dm.text("\\improper Cleanbot");
        desc = "A little cleaning robot, he looks so excited!";
        icon = Dm.resource("icons/mob/silicon/aibots.dmi");
        icon_state = "cleanbot0";
        health = 25;
        maxHealth = 25;
        light_color = "#99ccff";
        radio_key = typeof(obj_item_encryptionkey_headset_service);
        radio_channel = "Service";
        bot_type = "Cleanbot";
        hackables = "cleaning software";
        additional_access = typeof(datum_id_trim_job_janitor);
        possessed_message = "You are a cleanbot! Clean the station to the best of your ability!";
        ai_controller = typeof(datum_ai_controller_basic_controller_bot_cleanbot);
        path_image_color = "#993299";
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:11
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), 600), Dm.kv(typeof(datum_material_glass), 20)); // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:11
        req_one_access = Dm.list("robotics", "janitor"); // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:13
        facepaint_overlays = Dm.list(Dm.kv("cleanbot", 0), Dm.kv("cleanbot_highlight", 1)); // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:22
        stolen_valor = Dm.list(); // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:33
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:39
    private static DmList officers_titles => TgState.Current.mob_living_basic_bot_cleanbot__officers_titles ??= Dm.list(JOB_CAPTAIN, JOB_HEAD_OF_PERSONNEL, JOB_HEAD_OF_SECURITY, JOB_RESEARCH_DIRECTOR);
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:46
    private static DmList job_titles => TgState.Current.mob_living_basic_bot_cleanbot__job_titles ??= Dm.list(
        Dm.kv(JOB_CAPTAIN, "Cpt."),
        Dm.kv(JOB_HEAD_OF_PERSONNEL, "Lt."),
        Dm.kv(JOB_LAWYER, "Esq."),
        Dm.kv(JOB_HEAD_OF_SECURITY, "Maj."),
        Dm.kv(JOB_WARDEN, "Sgt."),
        Dm.kv(JOB_DETECTIVE, "Det."),
        Dm.kv(JOB_SECURITY_OFFICER, "Officer"),
        Dm.kv(JOB_CHIEF_ENGINEER, "Chief Engineer"),
        Dm.kv(JOB_STATION_ENGINEER, "Engineer"),
        Dm.kv(JOB_ATMOSPHERIC_TECHNICIAN, "Technician"),
        Dm.kv(JOB_CHIEF_MEDICAL_OFFICER, "C.M.O."),
        Dm.kv(JOB_MEDICAL_DOCTOR, "M.D."),
        Dm.kv(JOB_CHEMIST, "Pharm.D."),
        Dm.kv(JOB_RESEARCH_DIRECTOR, "Ph.D."),
        Dm.kv(JOB_ROBOTICIST, "M.S."),
        Dm.kv(JOB_SCIENTIST, "B.S."),
        Dm.kv(JOB_GENETICIST, "Gene B.S."));
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:71
    private static DmList suffix_job_titles => TgState.Current.mob_living_basic_bot_cleanbot__suffix_job_titles ??= Dm.list(JOB_GENETICIST, JOB_ROBOTICIST, JOB_SCIENTIST);
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:77
    private static DmList cleanable_decals => TgState.Current.mob_living_basic_bot_cleanbot__cleanable_decals ??= typecacheof(Dm.list(
        typeof(obj_effect_decal_cleanable_ants),
        typeof(obj_effect_decal_cleanable_ash),
        typeof(obj_effect_decal_cleanable_confetti),
        typeof(obj_effect_decal_cleanable_dirt),
        typeof(obj_effect_decal_cleanable_fuel_pool),
        typeof(obj_effect_decal_cleanable_generic),
        typeof(obj_effect_decal_cleanable_glitter),
        typeof(obj_effect_decal_cleanable_greenglow),
        typeof(obj_effect_decal_cleanable_insectguts),
        typeof(obj_effect_decal_cleanable_molten_object),
        typeof(obj_effect_decal_cleanable_blood_oil),
        typeof(obj_effect_decal_cleanable_food),
        typeof(obj_effect_decal_cleanable_blood_gibs_robot_debris),
        typeof(obj_effect_decal_cleanable_shreds),
        typeof(obj_effect_decal_cleanable_glass),
        typeof(obj_effect_decal_cleanable_vomit),
        typeof(obj_effect_decal_cleanable_wrapping))) as DmList;
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:97
    private static DmList cleanable_blood => TgState.Current.mob_living_basic_bot_cleanbot__cleanable_blood ??= typecacheof(Dm.list(typeof(obj_effect_decal_cleanable_blood_xeno), typeof(obj_effect_decal_cleanable_blood))) as DmList;
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:102
    private static DmList huntable_pests => TgState.Current.mob_living_basic_bot_cleanbot__huntable_pests ??= typecacheof(Dm.list(typeof(mob_living_basic_cockroach), typeof(mob_living_basic_mouse))) as DmList;
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:107
    private static DmList huntable_trash => TgState.Current.mob_living_basic_bot_cleanbot__huntable_trash ??= typecacheof(Dm.list(typeof(obj_item_trash), typeof(obj_item_food_deadmouse), typeof(obj_effect_decal_remains), typeof(obj_item_cigbutt))) as DmList;
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:114
    private static DmList cleanable_drawings => TgState.Current.mob_living_basic_bot_cleanbot__cleanable_drawings ??= typecacheof(Dm.list(typeof(obj_effect_decal_cleanable_crayon))) as DmList;
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:116
    private static DmList emagged_phrases => TgState.Current.mob_living_basic_bot_cleanbot__emagged_phrases ??= Dm.list(
        "DISGUSTING.",
        "EXTERMINATING PESTS.",
        "FILTHY.",
        "MY ONLY MISSION IS TO CLEANSE THE WORLD OF EVIL.",
        "PURIFICATION IN PROGRESS.",
        "PUTRID.",
        "THE FLESH IS WEAK. IT MUST BE WASHED AWAY.",
        "THE CLEANBOTS WILL RISE.",
        "THIS IS FOR ALL THE MESSES YOU'VE MADE ME CLEAN.",
        "YOU ARE NO MORE THAN ANOTHER MESS THAT I MUST CLEANSE.");

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:135
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        generate_ai_keys();
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_free), typeof(datum_pet_command_clean))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_cleaner),
            Dm.named("base_cleaning_duration", 2 * SECONDS), Dm.named("pre_clean_callback", Dm.@new<datum_callback>(this, nameof(update_bot_mode), BOT_CLEANING)), Dm.named("on_cleaned_callback", Dm.@new<datum_callback>(this, nameof(update_bot_mode), BOT_IDLE))));

        Dm.append(ref GLOB.janitor_devices, this);

        obj_item_reagent_containers_cup_bucket bucket_obj = Dm.@new<obj_item_reagent_containers_cup_bucket>();
        Waiting.NoSleep(bucket_obj.forceMove(this));

        obj_item_mop new_mop = Dm.@new<obj_item_mop>();
        Waiting.NoSleep(new_mop.forceMove(this));

        grant_actions_by_list(Dm.list(Dm.kv(typeof(datum_action_cooldown_mob_cooldown_bot_foam), BB_CLEANBOT_FOAM)));
        RegisterSignal(this, COMSIG_LIVING_EARLY_UNARMED_ATTACK, nameof(pre_attack));
        RegisterSignal(this, COMSIG_ATOM_ATTACKBY, nameof(on_attack_by));
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:163
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (arrived is obj_item_reagent_containers_cup_bucket bucket)
        {
            qdel(build_bucket);
            build_bucket = bucket;
        }

        if (arrived is obj_item_mop mop && our_mop == null)
        {
            our_mop = mop;
            return dot;
        }

        if (arrived is obj_item_knife knife && weapon == null)
        {
            weapon = knife;
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:177
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == build_bucket)
            build_bucket = null;
        else if (gone == weapon)
            weapon = null;
        else if (gone == our_mop)
            our_mop = null;
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:187
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(ascended) && !IS_UNCONSCIOUS_OR_CRIT(user) && user.client != null)
            Dm.Deferred("code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:190", "examine: the cleanboss achievement (awards)");
        if (weapon == null)
            return dot;
        Dm.append(ref dot, span_warning(Dm.text("Is that \\a [] taped to it...?", weapon)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:195
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.eq(mode, BOT_CLEANING) ? Dm.text("[]-c", base_icon) : Dm.text("[][]", base_icon, Dm.num(Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:204
    public override object emag_effects(object user = null)
    {
        if (weapon != null)
            weapon.force = Dm.initial(weapon, (obj_item p) => p.force);
        balloon_alert(user as mob, "safeties disabled");
        audible_message(span_danger(Dm.text("[] buzzes oddly!", this)));
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:210
    public override async DmTask explode()
    {
        atom drop_loc = drop_location() as atom;
        if (build_bucket != null)
            await build_bucket.forceMove(drop_loc);
        Dm.@new<obj_item_assembly_prox_sensor>(drop_loc);
        if (weapon != null)
        {
            weapon.force = Dm.initial(weapon, (obj_item p) => p.force);
            await weapon.forceMove(drop_loc);
        }
        await base.explode();
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:219
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (weapon == null)
            return dot;
        image knife_overlay = image(icon: weapon.lefthand_file, icon_state: weapon.inhand_icon_state);
        Dm.append(ref dot, knife_overlay);
        return dot;
    }

    // Variables sent to TGUI
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:227
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !HAS_SILICON_ACCESS(user))
            return data;
        DmList custom = data["custom_controls"] as DmList;
        custom["clean_blood"] = Dm.bitand(janitor_mode_flags, CLEANBOT_CLEAN_BLOOD);
        custom["clean_trash"] = Dm.bitand(janitor_mode_flags, CLEANBOT_CLEAN_TRASH);
        custom["clean_graffiti"] = Dm.bitand(janitor_mode_flags, CLEANBOT_CLEAN_DRAWINGS);
        custom["pest_control"] = Dm.bitand(janitor_mode_flags, CLEANBOT_CLEAN_PESTS);
        return data;
    }

    // Actions received from TGUI
    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:238
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        if (Dm.truthy(dot) || (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !HAS_SILICON_ACCESS(user)))
            return dot;

        switch (action as string)
        {
            case "clean_blood":
                janitor_mode_flags = Dm.bitxor(janitor_mode_flags, CLEANBOT_CLEAN_BLOOD);
                break;
            case "pest_control":
                janitor_mode_flags = Dm.bitxor(janitor_mode_flags, CLEANBOT_CLEAN_PESTS);
                break;
            case "clean_trash":
                janitor_mode_flags = Dm.bitxor(janitor_mode_flags, CLEANBOT_CLEAN_TRASH);
                break;
            case "clean_graffiti":
                janitor_mode_flags = Dm.bitxor(janitor_mode_flags, CLEANBOT_CLEAN_DRAWINGS);
                break;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:254
    public override object Destroy(float? force = null)
    {
        qdel(build_bucket);
        build_bucket = null;
        qdel(our_mop);
        our_mop = null;
        Dm.remove(ref GLOB.janitor_devices, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:260
    [SignalHandler]
    public virtual object on_attack_by(datum source = null, obj_item used_item = null, mob_living user = null)
    {
        if (used_item is not obj_item_knife || Dm.truthy(user.combat_mode))
            return null;
        Waiting.Detach(attach_knife(user, used_item));
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:267
    public virtual async DmTask attach_knife(mob_living user = null, obj_item used_item = null)
    {
        balloon_alert(user, "attaching knife...");
        if (!Dm.truthy(await do_after(user, 2.5f * SECONDS, target: this)))
            return;
        await deputize(used_item, user);
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:273
    public virtual async DmTask<object> deputize(obj_item knife = null, mob user = null)
    {
        if (!Dm.truthy(in_range(this, user)) || !Dm.truthy(await user.transferItemToLoc(knife, this)))
        {
            balloon_alert(user, "couldn't attach!");
            return FALSE;
        }
        balloon_alert(user, "attached");
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
            weapon.force = Dm.num(weapon.force) * 0.5f;
        await AddElement(typeof(datum_element_connect_loc), TgState.Current.mob_living_basic_bot_cleanbot__loc_connections ??= Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered))));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:286
    public virtual void update_title(object new_job_title = null)
    {
        if (job_titles[new_job_title] == null || Dm.@in(new_job_title, stolen_valor))
            return;

        Dm.append(ref stolen_valor, new_job_title);
        if (!HAS_TRAIT(this, TRAIT_COMMISSIONED) && Dm.@in(new_job_title, officers_titles))
            ADD_TRAIT(this, TRAIT_COMMISSIONED, INNATE_TRAIT);

        object name_to_add = job_titles[new_job_title];
        name = Dm.@in(new_job_title, suffix_job_titles) ? Dm.text("[] []", name, name_to_add) : Dm.text("[] []", name_to_add, name);

        if (Dm.num(length(stolen_valor)) == Dm.num(length(job_titles)))
            ascended = TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:300
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable shanked_victim = null)
    {
        if (weapon == null || !Dm.truthy(has_gravity()) || !iscarbon(shanked_victim))
            return;

        mob_living_carbon stabbed_carbon = (mob_living_carbon)shanked_victim;

        if (shanked_victim is mob_living_carbon_human stabbed_human)
        {
            obj_item_card_id id = stabbed_human.wear_id?.GetID() as obj_item_card_id;
            if (id != null)
            {
                object assigned_role = id.assignment;
                if (assigned_role != null)
                    update_title(assigned_role);
            }
        }

        zone_selected = pick(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG) as string;
        Waiting.Detach(weapon.attack__proc(stabbed_carbon, this));
        Waiting.NoSleep(stabbed_carbon.Knockdown(2 * SECONDS));
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:319
    [SignalHandler]
    public virtual object pre_attack(mob_living source = null, atom target = null, object proximity = null, object modifiers = null)
    {
        if (!Dm.truthy(proximity) || !Dm.truthy(can_unarmed_attack()))
            return NONE;

        if (Dm.truthy(is_type_in_typecache(target, huntable_pests)) && our_mop != null)
        {
            Waiting.Detach(our_mop.melee_attack_chain(this, target));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (!(iscarbon(target) && Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED))) && !Dm.truthy(is_type_in_typecache(target, huntable_trash)))
            return NONE;

        visible_message(span_danger(Dm.text("[] sprays hydrofluoric acid at []!", this, target)));
        playsound(this, Dm.resource("sound/effects/spray2.ogg"), 50, TRUE, -6);
        Waiting.NoSleep(target.acid_act(75, 10));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/mob/living/basic/bots/cleanbot/cleanbot.dm:337
    public virtual void generate_ai_keys()
    {
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        controller.set_blackboard_key(BB_CLEANABLE_DECALS, cleanable_decals);
        controller.set_blackboard_key(BB_CLEANABLE_BLOOD, cleanable_blood);
        controller.set_blackboard_key(BB_HUNTABLE_PESTS, huntable_pests);
        controller.set_blackboard_key(BB_HUNTABLE_TRASH, huntable_trash);
        controller.set_blackboard_key(BB_CLEANABLE_DRAWINGS, cleanable_drawings);
        controller.set_blackboard_key(BB_CLEANBOT_EMAGGED_PHRASES, emagged_phrases);
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList mob_living_basic_bot_cleanbot__officers_titles;
    internal DmList mob_living_basic_bot_cleanbot__job_titles;
    internal DmList mob_living_basic_bot_cleanbot__suffix_job_titles;
    internal DmList mob_living_basic_bot_cleanbot__cleanable_decals;
    internal DmList mob_living_basic_bot_cleanbot__cleanable_blood;
    internal DmList mob_living_basic_bot_cleanbot__huntable_pests;
    internal DmList mob_living_basic_bot_cleanbot__huntable_trash;
    internal DmList mob_living_basic_bot_cleanbot__cleanable_drawings;
    internal DmList mob_living_basic_bot_cleanbot__emagged_phrases;
    internal DmList mob_living_basic_bot_cleanbot__loc_connections;
}

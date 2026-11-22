// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/virtual_pet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): the Virtual Pet, a PDA's Orbie: summoned and recalled, fed with the virtual chocolate dropped at a
// random station area, cleaned, levelled up by walking and playing (lights at level 2, a camera at 3), dressed in a hologram hat and a
// colour, taught a trick sequence, and its milestones on the shared feed of every pet. tg's pictures (the profile, the state icons, the
// feed's) are icon2base64 of a flattened icon; the server reads no .dmi (D104), so a picture is sent as its parts (icon, state, colour,
// hat) and the client draws them (DV405). A photo the pet takes waits for NtOS's image files (not-translated section 164). The var
// block is generated (D111).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:1  GLOBAL_LIST_EMPTY(global_pet_updates)
    public DmList global_pet_updates;

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:1
    public virtual void InitGlobalglobal_pet_updates()
    {
        global_pet_updates = Dm.list();
        Dm.append(ref gvars_datum_init_order, "global_pet_updates");
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:2  GLOBAL_LIST_EMPTY(virtual_pets_list)
    public DmList virtual_pets_list;

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:2
    public virtual void InitGlobalvirtual_pets_list()
    {
        virtual_pets_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "virtual_pets_list");
    }
}

// tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:15
public partial class datum_computer_file_program_virtual_pet : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:4-13  the file's defines
    private const float MAX_UPDATE_LENGTH = 50;
    private const float PET_MAX_STEPS_RECORD = 50000;
    private const float PET_EAT_BONUS = 500;
    private const float PET_CLEAN_BONUS = 250;
    private const float PET_PLAYMATE_BONUS = 500;
    private const string PET_STATE_HUNGRY = "hungry";
    private const string PET_STATE_ASLEEP = "asleep";
    private const string PET_STATE_HAPPY = "happy";
    private const string PET_STATE_NEUTRAL = "neutral";

    /// The port's own (DV405): the profile picture as the parts the client draws (icon, icon_state, color, hat_icon, hat_icon_state,
    /// hat_pixel_w, hat_pixel_z), where tg keeps a flattened icon (profile_picture, left null)
    public DmList profile_picture_parts;

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:48  var/static/list/pet_state_icons (rules 10.6)
    private static DmList pet_state_icons => TgState.Current.virtual_pet__pet_state_icons ??= Dm.list(
        Dm.kv(PET_STATE_HUNGRY, Dm.list(Dm.kv("icon", Dm.resource("icons/ui/virtualpet/pet_state.dmi")), Dm.kv("icon_state", "pet_hungry"))),
        Dm.kv(PET_STATE_HAPPY, Dm.list(Dm.kv("icon", Dm.resource("icons/ui/virtualpet/pet_state.dmi")), Dm.kv("icon_state", "pet_happy"))),
        Dm.kv(PET_STATE_ASLEEP, Dm.list(Dm.kv("icon", Dm.resource("icons/ui/virtualpet/pet_state.dmi")), Dm.kv("icon_state", "pet_asleep"))),
        Dm.kv(PET_STATE_NEUTRAL, Dm.list(Dm.kv("icon", Dm.resource("icons/ui/virtualpet/pet_state.dmi")), Dm.kv("icon_state", "pet_neutral"))));
    ///hat options and what level they will be unlocked at
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:55
    private static DmList hat_selections => TgState.Current.virtual_pet__hat_selections ??= Dm.list(
        Dm.kv(typeof(obj_item_clothing_head_hats_tophat), 1),
        Dm.kv(typeof(obj_item_clothing_head_fedora), 1),
        Dm.kv(typeof(obj_item_clothing_head_soft_fishing_hat), 1),
        Dm.kv(typeof(obj_item_cigarette_dart), 1),
        Dm.kv(typeof(obj_item_clothing_head_hats_bowler), 2),
        Dm.kv(typeof(obj_item_clothing_head_hats_warden_police), 2),
        Dm.kv(typeof(obj_item_clothing_head_wizard_tape), 2),
        Dm.kv(typeof(obj_item_clothing_head_utility_hardhat_cakehat_energycake), 2),
        Dm.kv(typeof(obj_item_clothing_head_cowboy_bounty), 2),
        Dm.kv(typeof(obj_item_clothing_head_hats_warden_red), 3),
        Dm.kv(typeof(obj_item_clothing_head_hats_caphat), 3),
        Dm.kv(typeof(obj_item_clothing_head_costume_crown_fancy), 3));
    ///A list of hats that override the hat offsets and transform variable
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:79
    private static DmList special_hat_placement => TgState.Current.virtual_pet__special_hat_placement ??= Dm.list(
        Dm.kv(typeof(obj_item_cigarette_dart), Dm.list(
            Dm.kv("west", Dm.list(2, -1)),
            Dm.kv("east", Dm.list(-2, -1)),
            Dm.kv("north", Dm.list(0, 0)),
            Dm.kv("south", Dm.list(0, -3)),
            Dm.kv("transform", Dm.list(1, 1)))));
    ///manage hat offsets for when we turn directions
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:91
    private static DmList hat_offsets => TgState.Current.virtual_pet__hat_offsets ??= Dm.list(
        Dm.kv("west", Dm.list(0, 1)),
        Dm.kv("east", Dm.list(0, 1)),
        Dm.kv("north", Dm.list(1, 1)),
        Dm.kv("south", Dm.list(1, 1)));
    ///possible colors our pet can have
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:100
    private static DmList possible_colors => TgState.Current.virtual_pet__possible_colors ??= Dm.list(
        Dm.kv("white", null), //default color state
        Dm.kv("light blue", "#c3ecf3"),
        Dm.kv("light green", "#b1ffe8"));
    ///areas we wont drop the chocolate in
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:106
    private static DmList restricted_areas => TgState.Current.virtual_pet__restricted_areas ??= typecacheof(Dm.list(
        typeof(area_station_security),
        typeof(area_station_command),
        typeof(area_station_ai),
        typeof(area_station_maintenance),
        typeof(area_station_solars))) as DmList;

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:125
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        // profile_picture = getFlatIcon(image(icon = 'icons/ui/virtualpet/pet_state.dmi', icon_state = "pet_preview")): its parts (DV405)
        profile_picture_parts = Dm.list(Dm.kv("icon", Dm.resource("icons/ui/virtualpet/pet_state.dmi")), Dm.kv("icon_state", "pet_preview"));
        GLOB.virtual_pets_list.Add(this);
        pet = Dm.@new(pet_type, computer) as mob_living;
        Waiting.NoSleep(pet.forceMove(computer));
        Waiting.NoSleep(pet.AddComponent(typeof(datum_component_leash), computer, 9, Dm.named("force_teleport_out_effect", typeof(obj_effect_temp_visual_guardian_phase_out))));
        RegisterSignal(pet, COMSIG_QDELETING, nameof(remove_pet));
        RegisterSignal(pet, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_overlays_updated)); //hologramic hat management
        RegisterSignal(pet, COMSIG_ATOM_DIR_CHANGE, nameof(on_change_dir));
        RegisterSignal(pet, COMSIG_MOVABLE_MOVED, nameof(after_pet_move));
        RegisterSignal(pet, COMSIG_MOB_ATE, nameof(after_pet_eat)); // WE ATEEE
        RegisterSignal(pet, COMSIG_ATOM_PRE_CLEAN, nameof(pet_pre_clean));
        RegisterSignal(pet, COMSIG_LIVING_DEATH, nameof(on_death));
        RegisterSignal(pet, COMSIG_COMPONENT_CLEAN_ACT, nameof(post_cleaned));
        RegisterSignal(pet, COMSIG_AI_BLACKBOARD_KEY_SET(BB_NEARBY_PLAYMATE), nameof(on_playmate_find));
        RegisterSignal(computer, COMSIG_ATOM_ENTERED, nameof(on_pet_entered));
        RegisterSignal(computer, COMSIG_ATOM_EXITED, nameof(on_pet_exit));
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:143
    public override object Destroy(float? force = null)
    {
        GLOB.virtual_pets_list.Remove(this);
        if (!QDELETED(pet))
        {
            mob_living doomed = pet;
            QDEL_NULL(ref doomed);
            pet = null;
        }
        STOP_PROCESSING(SSprocessing, this);
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:150
    [SignalHandler]
    public virtual void on_death(datum source = null)
    {
        Waiting.NoSleep(pet.forceMove(computer));
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:156
    [SignalHandler]
    public virtual void on_message_receive(datum source = null, object sender_title = null, object inbound_message = null, object photo_message = null)
    {
        string message_to_display = I18n.T("[] has sent you a message []: []!", sender_title, Dm.truthy(photo_message) ? I18n.T("with a photo attached") : "", inbound_message);
        (pet.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_LAST_RECEIVED_MESSAGE, message_to_display);
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:162
    [SignalHandler]
    public virtual object pet_pre_clean(atom source = null, mob user = null)
    {
        if (!(on_clean_cooldown <= world.time)) // COOLDOWN_FINISHED(src, on_clean_cooldown)
        {
            source.balloon_alert(user, I18n.T("already clean!"));
            return COMSIG_ATOM_CANCEL_CLEAN;
        }
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:169
    [SignalHandler]
    public virtual void on_playmate_find(datum source = null)
    {
        happiness = Math.Min(Dm.num(happiness) + PET_PLAYMATE_BONUS, max_happiness);
        START_PROCESSING(SSprocessing, this);
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:175
    [SignalHandler]
    public virtual object post_cleaned(mob source = null, mob user = null)
    {
        float dot = NONE;
        source.spin(spintime: 2 * SECONDS, speed: 1); //celebrate!
        happiness = Math.Min(Dm.num(happiness) + PET_CLEAN_BONUS, max_happiness);
        on_clean_cooldown = world.time + 1 * MINUTES; // COOLDOWN_START(src, on_clean_cooldown, 1 MINUTES)
        START_PROCESSING(SSprocessing, this);
        dot = Dm.bitor(dot, Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP));
        return dot;
    }

    ///manage the pet's hat offsets when he changes direction
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:185
    [SignalHandler]
    public virtual void on_change_dir(datum source = null, object old_dir = null, object new_dir = null)
    {
        if (!Dm.truthy(length(selected_hat)))
            return;
        set_hat_offsets(new_dir);
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:192
    [SignalHandler]
    public virtual void on_photo_captured(datum source = null, atom target = null, atom user = null, datum_picture photo = null)
    {
        if (photo == null)
            return;
        // computer.store_file(new /datum/computer_file/image(photo.picture_image, display_name = photo.picture_name)): NtOS's image files
        // (not-translated section 164)
        Dm.Deferred("code/modules/modular_computers/file_system/programs/virtual_pet.dm:197", "on_photo_captured: the photo as an image file (NtOS's image files)");
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:199
    public virtual void set_hat_offsets(object new_dir = null)
    {
        object direction_text = dir2text(new_dir);
        object hat_type = selected_hat["type"];
        DmList offsets_list = ((special_hat_placement[hat_type] as DmList)?[direction_text] as DmList) ?? hat_offsets[direction_text] as DmList;
        mutable_appearance hat_appearance = selected_hat["appearance"] as mutable_appearance;
        hat_appearance.pixel_w = Dm.num(offsets_list[1]);
        hat_appearance.pixel_z = Dm.num(offsets_list[2]) + Dm.num(selected_hat["worn_offset"]);
        pet.update_appearance(UPDATE_OVERLAYS);
    }

    ///give our pet his hologram hat
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:209
    [SignalHandler]
    public virtual void on_overlays_updated(atom source = null, DmList overlays = null)
    {
        if (!Dm.truthy(length(selected_hat)))
            return;
        Dm.append(ref overlays, selected_hat["appearance"]);
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:216
    public virtual void alter_profile_picture()
    {
        // var/image/pet_preview = image(icon = 'icons/ui/virtualpet/pet_state.dmi', icon_state = "pet_preview"), flattened: its parts (DV405)
        DmList pet_preview = Dm.list(Dm.kv("icon", Dm.resource("icons/ui/virtualpet/pet_state.dmi")), Dm.kv("icon_state", "pet_preview"));
        if (pet.cached_color_filter != null)
        {
            DmList filter = pet.cached_color_filter as DmList;
            pet_preview["color"] = apply_matrix_to_color(COLOR_WHITE, filter["color"] as DmList, Dm.truthy(filter["space"]) ? Dm.num(filter["space"]) : COLORSPACE_RGB);
        }
        else if (Dm.truthy(pet.color))
            pet_preview["color"] = pet.color;

        if (Dm.truthy(length(selected_hat)))
        {
            mutable_appearance our_selected_hat = selected_hat["appearance"] as mutable_appearance;
            pet_preview["hat_icon"] = our_selected_hat.icon;
            pet_preview["hat_icon_state"] = our_selected_hat.icon_state;
            float hat_pixel_w = 0;
            float hat_pixel_z = -9 + Dm.num(selected_hat["worn_offset"]);
            DmList spec_hat = (special_hat_placement[selected_hat["type"]] as DmList)?["south"] as DmList;
            if (spec_hat != null)
            {
                hat_pixel_w += Dm.num(spec_hat[1]);
                hat_pixel_z += Dm.num(spec_hat[2]);
            }
            pet_preview["hat_pixel_w"] = hat_pixel_w;
            pet_preview["hat_pixel_z"] = hat_pixel_z;
        }

        profile_picture_parts = pet_preview; // profile_picture = getFlatIcon(pet_preview, no_anim = TRUE)
        alter_appearance_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START(src, alter_appearance_cooldown, 10 SECONDS)
    }

    ///decrease the pet's hunger after it eats
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:240
    [SignalHandler]
    public virtual void after_pet_eat(datum source = null)
    {
        hunger = Math.Min(hunger + PET_EAT_BONUS, max_hunger);
        happiness = Math.Min(Dm.num(happiness) + PET_EAT_BONUS, max_happiness);
        START_PROCESSING(SSprocessing, this);
    }

    ///start processing if we enter the pda and need healing
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:248
    [SignalHandler]
    public virtual void on_pet_entered(atom_movable source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (arrived != pet)
            return;
        ADD_TRAIT(pet, TRAIT_AI_PAUSED, REF(this));
        if (Dm.truthy(Dm.bitand(datum_flags, DF_ISPROCESSING)))
            return;
        if (Dm.num(pet.health) < Dm.num(pet.maxHealth)) //if we're in the pda, heal up
            START_PROCESSING(SSprocessing, this);
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:259
    [SignalHandler]
    public virtual void on_pet_exit(atom_movable source = null, atom_movable exited = null)
    {
        if (exited != pet)
            return;
        REMOVE_TRAIT(pet, TRAIT_AI_PAUSED, REF(this));
        if (Dm.truthy(Dm.bitand(datum_flags, DF_ISPROCESSING)))
            return;
        if (hunger > 0 || Dm.num(happiness) > 0) //if were outside the pda, we become hungry and happiness decreases
            START_PROCESSING(SSprocessing, this);
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:270
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (pet.loc == computer)
        {
            if (Dm.num(pet.health) >= Dm.num(pet.maxHealth))
                return PROCESS_KILL;
            if (pet.stat == DEAD)
                Waiting.NoSleep(pet.revive(ADMIN_HEAL_ALL));
            Waiting.NoSleep(pet.heal_overall_damage(5));
            return null;
        }

        if (hunger > 0)
            hunger--;

        if (Dm.num(happiness) > 0)
            happiness = Dm.num(happiness) - 1;

        if (hunger <= 0 && Dm.num(happiness) <= 0)
            return PROCESS_KILL;
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:288
    [SignalHandler]
    public virtual void after_pet_move(atom_movable movable = null, atom old_loc = null)
    {
        if (!Dm.truthy(isturf(pet.loc)) || !Dm.truthy(isturf(old_loc)))
            return;
        steps_counter = Math.Min(steps_counter + 1, PET_MAX_STEPS_RECORD);
        increment_exp();
        if (steps_counter % 2000 == 0) //every 2000 steps, announce the milestone to the world!
            announce_global_updates(message: I18n.T("has walked [] steps!", steps_counter));
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:298
    public virtual void increment_exp()
    {
        float modifier = 1;
        float hunger_happiness = hunger + Dm.num(happiness);
        float max_hunger_happiness = max_hunger + max_happiness;

        float ratio = hunger_happiness / max_hunger_happiness;
        if (ratio >= 0.8f && ratio <= 1)
            modifier = 3;
        else if (ratio >= 0.5f && ratio <= 0.8f)
            modifier = 2;

        current_level_progress = Math.Min(current_level_progress + modifier, to_next_level);
        if (current_level_progress >= to_next_level)
            handle_level_up();
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:313
    public virtual void handle_level_up()
    {
        current_level_progress = 0;
        level = Dm.num(level) + 1;
        grant_level_abilities();
        (pet.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_VIRTUAL_PET_LEVEL, level);
        playsound(computer.loc, Dm.resource("sound/mobs/non-humanoids/orbie/orbie_level_up.ogg"), 50);
        to_next_level += MathF.Pow(Dm.num(level), 2) + 500;
        if (Dm.truthy(pet._listen_lookup?[COMSIG_VIRTUAL_PET_LEVEL_UP])) //its a signal so different path types of virtual pets can handle leveling up differently
            pet._SendSignal(COMSIG_VIRTUAL_PET_LEVEL_UP, Dm.list(pet, level));
        announce_global_updates(message: I18n.T("has reached level []!", level));
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:324
    public virtual void grant_level_abilities()
    {
        switch (Dm.num(level))
        {
            case 2:
            {
                RegisterSignal(computer, COMSIG_COMPUTER_RECEIVED_MESSAGE, nameof(on_message_receive)); // we will now read out PDA messages
                datum_action_cooldown_mob_cooldown_lights lights = Dm.@new<datum_action_cooldown_mob_cooldown_lights>(pet);
                Waiting.NoSleep(lights.Grant(pet));
                (pet.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_LIGHTS_ABILITY, lights);
                break;
            }
            case 3:
            {
                datum_action_cooldown_mob_cooldown_capture_photo photo_ability = Dm.@new<datum_action_cooldown_mob_cooldown_capture_photo>(pet);
                Waiting.NoSleep(photo_ability.Grant(pet));
                (pet.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_PHOTO_ABILITY, photo_ability);
                RegisterSignal(photo_ability.internal_camera, COMSIG_CAMERA_IMAGE_CAPTURED, nameof(on_photo_captured));
                break;
            }
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:337
    public virtual void announce_global_updates(object message = null)
    {
        if (message == null)
            return;
        DmList message_to_announce = Dm.list(
            Dm.kv("name", pet.name),
            Dm.kv("pet_picture", profile_picture_parts), // icon2base64(profile_picture): its parts (DV405)
            Dm.kv("message", message),
            Dm.kv("likers", Dm.list(REF(this))));
        if (Dm.num(length(GLOB.global_pet_updates)) >= MAX_UPDATE_LENGTH)
            GLOB.global_pet_updates.Cut(1, 2);

        GLOB.global_pet_updates.Add(Dm.list(message_to_announce));
        playsound(computer.loc, Dm.resource("sound/mobs/non-humanoids/orbie/orbie_notification_sound.ogg"), 50);
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:352
    [SignalHandler]
    public virtual void remove_pet(datum source = null)
    {
        pet = null;
        if (QDELETED(this))
            return;
        computer.remove_file(this); //all is lost we no longer have a reason to exist
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:359
    public override object kill_program(mob user = null)
    {
        if (pet != null && pet.loc != computer)
            Waiting.NoSleep(pet.forceMove(computer)); //recall the hologram back to the pda
        STOP_PROCESSING(SSprocessing, this);
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:365
    public virtual string get_pet_state()
    {
        if (pet == null)
            return null;

        if (pet.loc == computer)
            return PET_STATE_ASLEEP;

        if (Dm.num(happiness) / max_happiness > 0.8f)
            return PET_STATE_HAPPY;

        if (hunger / max_hunger < 0.5f)
            return PET_STATE_HUNGRY;

        return PET_STATE_NEUTRAL;
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:380
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        Type hat_type = selected_hat?["type"] as Type;
        data["currently_summoned"] = pet.loc != computer ? TRUE : FALSE;
        data["selected_area"] = selected_area != null ? selected_area.name : "No location set";
        data["pet_state"] = get_pet_state();
        data["hunger"] = hunger;
        data["maximum_hunger"] = max_hunger;
        data["pet_hat"] = hat_type != null ? Dm.initial(hat_type, (obj_item h) => h.name) : "none";
        data["can_reroll"] = area_reroll <= world.time ? TRUE : FALSE; // COOLDOWN_FINISHED
        data["can_summon"] = summon_cooldown <= world.time ? TRUE : FALSE;
        data["can_alter_appearance"] = alter_appearance_cooldown <= world.time ? TRUE : FALSE;
        data["pet_name"] = pet.name;
        data["steps_counter"] = steps_counter;
        data["in_dropzone"] = selected_area != null && get_area(computer)?.GetType().IsAssignableTo(selected_area.GetType()) == true ? TRUE : FALSE; // istype(get_area(computer), selected_area)
        data["pet_area"] = pet.loc != computer ? get_area_name(pet) : "Sleeping in PDA";
        data["current_exp"] = current_level_progress;
        data["required_exp"] = to_next_level;
        data["happiness"] = happiness;
        data["maximum_happiness"] = max_happiness;
        data["level"] = level;
        data["pet_color"] = "";
        object color_value = LAZYACCESS(pet.atom_colours, FIXED_COLOUR_PRIORITY);
        foreach (object index in Dm.each(possible_colors))
        {
            if (Dm.eq(possible_colors[index], color_value))
            {
                data["pet_color"] = index;
                break;
            }
        }

        data["pet_gender"] = pet.gender;

        data["pet_updates"] = Dm.list();

        for (int i = (int)Dm.num(length(GLOB.global_pet_updates)); i >= 1; i--)
        {
            DmList update = GLOB.global_pet_updates[i] as DmList;

            if (update == null)
                continue;

            Dm.append_at(data, "pet_updates", Dm.list(Dm.list(
                Dm.kv("update_id", i),
                Dm.kv("update_name", update["name"]),
                Dm.kv("update_picture", update["pet_picture"]),
                Dm.kv("update_message", update["message"]),
                Dm.kv("update_likers", length(update["likers"])),
                Dm.kv("update_already_liked", Dm.@in(REF(this), update["likers"]) ? TRUE : FALSE))));
        }

        data["all_pets"] = Dm.list();
        foreach (datum_computer_file_program_virtual_pet program in Dm.each<datum_computer_file_program_virtual_pet>(GLOB.virtual_pets_list))
        {
            Dm.append_at(data, "all_pets", Dm.list(Dm.list(
                Dm.kv("other_pet_name", program.pet?.name),
                Dm.kv("other_pet_picture", program.profile_picture_parts)))); // icon2base64(program.profile_picture): its parts (DV405)
        }
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:432
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["pet_state_icons"] = Dm.list();
        foreach (object list_index in Dm.each(pet_state_icons))
        {
            DmList sprite_location = pet_state_icons[list_index] as DmList;
            Dm.append_at(data, "pet_state_icons", Dm.list(Dm.list(
                Dm.kv("name", list_index),
                Dm.kv("icon", Dm.list(Dm.kv("icon", sprite_location["icon"]), Dm.kv("icon_state", sprite_location["icon_state"])))))); // icon2base64(getFlatIcon(image(...))): its parts (DV405)
        }

        data["hat_selections"] = Dm.list(Dm.list(
            Dm.kv("hat_id", null),
            Dm.kv("hat_name", "none")));

        foreach (object type_index in Dm.each(hat_selections))
        {
            if (Dm.num(level) >= Dm.num(hat_selections[type_index]))
            {
                Type hat = type_index as Type;
                object hat_name = Dm.initial(hat, (obj_item h) => h.name);
                // if(length(SSachievements.achievements)): the achievements subsystem is not ported (inert), so no hat is locked, as in tg
                // when the subsystem is not active
                Dm.append_at(data, "hat_selections", Dm.list(Dm.list(
                    Dm.kv("hat_id", type_index),
                    Dm.kv("hat_name", hat_name))));
            }
        }

        data["possible_colors"] = Dm.list();
        foreach (object color in Dm.each(possible_colors))
        {
            Dm.append_at(data, "possible_colors", Dm.list(Dm.list(
                Dm.kv("color_name", color),
                Dm.kv("color_value", possible_colors[color]))));
        }

        DmList possible_emotes = TgState.Current.virtual_pet__possible_emotes ??= Dm.list(
            typeof(datum_emote_flip),
            typeof(datum_emote_jump),
            typeof(datum_emote_living_shiver),
            typeof(datum_emote_spin),
            typeof(datum_emote_silicon_beep));
        data["possible_emotes"] = Dm.list("none");
        foreach (Type target_emote in Dm.each_cast<Type>(possible_emotes))
            Dm.append_at(data, "possible_emotes", Dm.initial(target_emote, (datum_emote e) => e.key));

        data["preview_icon"] = profile_picture_parts; // icon2base64(profile_picture): its parts (DV405)
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:481
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await base.ui_act(action, @params, ui, state);
        switch (action)
        {
            case "summon_pet":
                if (!(summon_cooldown <= world.time)) // COOLDOWN_FINISHED(src, summon_cooldown)
                    return TRUE;
                if (pet.loc == computer)
                    await release_pet(ui.user as mob_living);
                else
                    recall_pet(ui.user as mob_living);
                summon_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START(src, summon_cooldown, 10 SECONDS)
                break;

            case "apply_customization":
            {
                if (!(alter_appearance_cooldown <= world.time))
                    return TRUE;
                Type chosen_type = text2path(@params["chosen_hat"] as string) as Type;
                if (chosen_type == null)
                    selected_hat.Cut();
                else if (Dm.truthy(hat_selections[chosen_type]))
                {
                    // the achievements' lock: the subsystem is not ported (inert), so no hat is locked (as ui_static_data)
                    selected_hat["type"] = chosen_type;
                    object worn_icon_state = Dm.initial(chosen_type, (obj_item h) => h.worn_icon_state);
                    object state_to_use = Dm.truthy(worn_icon_state) ? worn_icon_state : Dm.initial(chosen_type, (obj_item h) => h.icon_state);
                    mutable_appearance selected_hat_appearance = mutable_appearance(Dm.initial(chosen_type, (obj_item h) => h.worn_icon), state_to_use as string, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
                    selected_hat["worn_offset"] = Dm.initial(chosen_type, (obj_item h) => h.worn_y_offset);
                    DmList scale_list = (special_hat_placement[chosen_type] as DmList)?["scale"] as DmList;
                    if (scale_list != null)
                        selected_hat_appearance.transform = (selected_hat_appearance.transform as matrix).Scale(Dm.num(scale_list[1]), Dm.num(scale_list[2]));
                    else
                        selected_hat_appearance.transform = (selected_hat_appearance.transform as matrix).Scale(0.8f, 1);
                    selected_hat["appearance"] = selected_hat_appearance;
                    set_hat_offsets(pet.dir);
                }

                object chosen_color = @params["chosen_color"];
                if (chosen_color == null)
                    await pet.remove_atom_colour(FIXED_COLOUR_PRIORITY);
                else
                    await pet.add_atom_colour(chosen_color, FIXED_COLOUR_PRIORITY);

                object input_name = await sanitize_name(@params["chosen_name"], allow_numbers: TRUE);
                pet.name = Dm.truthy(input_name) ? input_name as string : Dm.initial(pet, (mob_living p) => p.name);
                Dm.@new<obj_effect_temp_visual_guardian_phase>(pet.loc);

                switch (@params["chosen_gender"])
                {
                    case "male":
                        pet.gender = MALE;
                        break;
                    case "female":
                        pet.gender = FEMALE;
                        break;
                    case "neuter":
                        pet.gender = NEUTER;
                        break;
                }

                pet.update_appearance();
                alter_profile_picture();
                await update_static_data(ui.user, ui);
                break;
            }

            case "get_feed_location":
                generate_petfeed_area();
                break;

            case "drop_feed":
                drop_feed();
                break;

            case "like_update":
            {
                object index = @params["update_reference"];
                DmList update_message = GLOB.global_pet_updates[index] as DmList;
                if (update_message == null)
                    return TRUE;
                object our_reference = REF(this);
                DmList likers = update_message["likers"] as DmList;
                if (Dm.@in(our_reference, likers))
                    likers.Remove(our_reference);
                else
                    likers.Add(our_reference);
                break;
            }

            case "teach_tricks":
            {
                object trick_name = @params["trick_name"];
                DmList trick_sequence = @params["tricks"] as DmList;
                if (pet.ai_controller == null)
                    return TRUE;
                datum_ai_controller controller = pet.ai_controller as datum_ai_controller;
                if (trick_name != null)
                    controller.set_blackboard_key(BB_TRICK_NAME, trick_name);
                foreach (object trick_move in Dm.each(trick_sequence?.Copy()))
                {
                    if (!Dm.truthy(length(GLOB.emote_list[LOWER_TEXT(trick_move)])))
                        trick_sequence.Remove(trick_move);
                }
                controller.override_blackboard_key(BB_TRICK_SEQUENCE, trick_sequence);
                playsound(computer.loc, Dm.resource("sound/mobs/non-humanoids/orbie/orbie_trick_learned.ogg"), 50);
                break;
            }
        }

        return TRUE;
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:572
    public virtual void generate_petfeed_area()
    {
        if (!(area_reroll <= world.time)) // COOLDOWN_FINISHED(src, area_reroll)
            return;
        DmList filter_area_list = typecache_filter_list(GLOB.the_station_areas, restricted_areas) as DmList;
        DmList target_area_list = Dm.sub(GLOB.the_station_areas.Copy(), filter_area_list) as DmList;
        if (!Dm.truthy(length(target_area_list)))
            return;
        selected_area = pick(target_area_list) as area;
        area_reroll = world.time + 2 * MINUTES; // COOLDOWN_START(src, area_reroll, 2 MINUTES)
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:582
    public virtual void drop_feed()
    {
        if (selected_area == null || get_area(computer)?.GetType().IsAssignableTo(selected_area.GetType()) != true) // istype(get_area(computer), selected_area)
            return;
        announce_global_updates(message: I18n.T("has found a chocolate at []", selected_area.name));
        selected_area = null;
        obj_item_food_virtual_chocolate chocolate = Dm.@new<obj_item_food_virtual_chocolate>(get_turf(computer));
        chocolate.fade_into_nothing(life_time: 30 * SECONDS); //we cant maintain its existence for too long!
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:591
    public virtual void recall_pet(mob_living friend = null)
    {
        Dm.animate(pet, Dm.named("transform", (Dm.@new<matrix>() as matrix).Scale(0.3f, 0.3f)), Dm.named("time", 1.5f * SECONDS));
        _addtimer(Dm.@new<datum_callback>(pet, nameof(atom_movable.forceMove), computer), 1.5f * SECONDS, file: "code/modules/modular_computers/file_system/programs/virtual_pet.dm", line: 593);
        if (Dm.truthy(pet._listen_lookup?[COMSIG_VIRTUAL_PET_RECALLED]))
            pet._SendSignal(COMSIG_VIRTUAL_PET_RECALLED, Dm.list(pet, friend));
    }

    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:596
    public virtual async DmTask release_pet(mob_living our_user = null)
    {
        turf drop_zone = null;
        DmList turfs_list = get_adjacent_open_turfs(computer.drop_location() as atom) as DmList;
        foreach (turf possible_turf in Dm.each<turf>(turfs_list))
        {
            if (Dm.truthy(await possible_turf.is_blocked_turf()))
                continue;
            drop_zone = possible_turf;
            break;
        }
        atom final_turf = drop_zone ?? computer.drop_location() as atom;
        if (pet is mob_living_basic basic_pet)
            await basic_pet.befriend(our_user); //befriend whoever set us out
        Dm.animate(pet, Dm.named("transform", Dm.@new<matrix>()), Dm.named("time", 1.5f * SECONDS));
        await pet.forceMove(final_turf);
        playsound(computer.loc, Dm.resource("sound/mobs/non-humanoids/orbie/orbie_send_out.ogg"), 20);
        if (Dm.truthy(pet._listen_lookup?[COMSIG_VIRTUAL_PET_SUMMONED]))
            pet._SendSignal(COMSIG_VIRTUAL_PET_SUMMONED, Dm.list(pet, our_user));
        Dm.@new<obj_effect_temp_visual_guardian_phase>(pet.loc);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/file_system/programs/virtual_pet.dm:48-112, :464  the program's static lists (rules 10.6)
    internal DmList virtual_pet__pet_state_icons;
    internal DmList virtual_pet__hat_selections;
    internal DmList virtual_pet__special_hat_placement;
    internal DmList virtual_pet__hat_offsets;
    internal DmList virtual_pet__possible_colors;
    internal DmList virtual_pet__restricted_areas;
    internal DmList virtual_pet__possible_emotes;
}

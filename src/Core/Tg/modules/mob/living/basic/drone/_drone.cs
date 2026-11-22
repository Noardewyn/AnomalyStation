// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/drone/_drone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the syndicate campaign X3: examine; silicons SI7, D326: the rest). [DmPath] is on the skeleton part in
// __HELPERS/names.cs; the var block is generated (D111), its head slot hand-declared (an item, the name grep types it a path).

/**
 * Maintenance Drone
 *
 * Small player controlled fixer-upper
 *
 * The maintenace drone is a ghost role with the objective to repair and
 * maintain the station.
 *
 * Featuring two dexterous hands, and a built in toolbox stocked with
 * tools.
 *
 * They have laws to prevent them from doing anything else.
 *
 */
// tg: code/modules/mob/living/basic/drone/_drone.dm:15
public partial class mob_living_basic_drone : mob_living_basic
{
    /// Headwear slot
    // tg: code/modules/mob/living/basic/drone/_drone.dm:68 (hand-declared: an item)
    public obj_item head;

    // tg: code/modules/mob/living/basic/drone/_drone.dm:17 (the defaults by hand: a hand-declared var drops the generated constructor)
    public mob_living_basic_drone()
    {
        name = "Drone";
        desc = "A maintenance drone, an expendable robot built to perform station repairs.";
        icon = Dm.resource("icons/mob/silicon/drone.dmi");
        icon_state = "drone_maint_grey";
        icon_living = "drone_maint_grey";
        icon_dead = "drone_maint_dead";
        health = 45;
        maxHealth = 45;
        unsuitable_atmos_damage = 0;
        unsuitable_cold_damage = 0;
        unsuitable_heat_damage = 0;
        speed = 0;
        density = 0;
        pass_flags = 17;
        sight = 24;
        status_flags = 11;
        gender = "neuter";
        mob_biotypes = 4;
        speech_span = "robot";
        bubble_icon = "machine";
        initial_language_holder = typeof(datum_language_holder_drone);
        mob_size = 1;
        unique_name = 1;
        hud_type = typeof(datum_hud_dextrous_drone);
        lighting_cutoff_red = 30;
        lighting_cutoff_green = 35;
        lighting_cutoff_blue = 25;
        worn_slot_flags = 64;
        inhand_holder_type = typeof(obj_item_mob_holder_drone);
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:35
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        speak_emote = Dm.list("chirps");
        physiology = Dm.list(Dm.kv("toxin", 0), Dm.kv("oxygen", 0), Dm.kv("stamina", 0));
        hud_possible = Dm.list("8", "9", "19");
        faction = Dm.list("neutral", "silicon", "turret");
        drone_overlays = Dm.@new<DmList>(1); // tg: _drone.dm:55
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        GLOB.drones_list.Add(this);
        laws = get_default_laws();
        Waiting.NoSleep(AddElement(typeof(datum_element_dextrous), Dm.named("hud_type", hud_type)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_basic_inhands), Dm.named("y_offset", getItemPixelShiftY())));
        // (SSid_access?: a test harness world without Master has no SSid_access, as in the bots' Initialize, _bots.cs)
        Waiting.NoSleep(AddComponent(typeof(datum_component_simple_access), SSid_access?.get_region_access_list(Dm.list(REGION_ALL_GLOBAL))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_personal_crafting))); // Kind of hard to be a drone and not be able to make tiles
        Waiting.NoSleep(LoadComponent(typeof(datum_component_bloodysoles_bot)));

        // Only station drones get a camera.
        if (Dm.truthy(is_station_level(loc.z)))
        {
            built_in_camera = Dm.@new<obj_machinery_camera_silicon>(this);
            built_in_camera.c_tag = real_name;
            built_in_camera.network = Dm.list(CAMERANET_NETWORK_SS13);
        }

        if (default_storage != null)
        {
            var storage = (obj_item)Dm.@new(default_storage, this);
            Waiting.NoSleep(equip_to_slot_or_del(storage, ITEM_SLOT_DEX_STORAGE));
        }

        foreach (object holiday_name in Dm.each(GLOB.holidays)) // gaps R3
        {
            var holiday_today = (datum_holiday)GLOB.holidays[holiday_name];
            Type potential_hat = holiday_today.holiday_hat;
            if (potential_hat != null && default_headwear == null) //If our drone type doesn't start with a hat, we take the holiday one.
                default_headwear = potential_hat;
        }

        if (default_headwear != null)
        {
            var new_hat = (obj_item)Dm.@new(default_headwear, this);
            Waiting.NoSleep(equip_to_slot_or_del(new_hat, ITEM_SLOT_HEAD));
        }

        shy_update();
        alert_drones(span_notice(I18n.T("DRONE NETWORK: [] connected.", name))); // DRONE_NET_CONNECT

        var diag_hud = GLOB.huds[DATA_HUD_DIAGNOSTIC] as datum_atom_hud_data_diagnostic;
        diag_hud.add_atom_to_hud(this);

        add_traits(Dm.list(
            TRAIT_VENTCRAWLER_ALWAYS,
            TRAIT_NEGATES_GRAVITY,
            TRAIT_LITERATE,
            TRAIT_KNOW_ENGI_WIRES,
            TRAIT_ADVANCEDTOOLUSER,
            TRAIT_SILICON_ACCESS,
            TRAIT_REAGENT_SCANNER,
            TRAIT_UNOBSERVANT,
            TRAIT_SILICON_EMOTES_ALLOWED), INNATE_TRAIT);

        listener = Dm.@new<datum_alarm_listener>(Dm.list(ALARM_ATMOS, ALARM_FIRE, ALARM_POWER), Dm.list(z));
        RegisterSignal(listener, COMSIG_ALARM_LISTENER_TRIGGERED, nameof(alarm_triggered));
        RegisterSignal(listener, COMSIG_ALARM_LISTENER_CLEARED, nameof(alarm_cleared));
        listener.RegisterSignal(this, COMSIG_LIVING_DEATH, nameof(datum_alarm_listener.prevent_alarm_changes));
        listener.RegisterSignal(this, COMSIG_LIVING_REVIVE, nameof(datum_alarm_listener.allow_alarm_changes));

        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:164
    public virtual string get_default_laws()
    {
        string base_laws = Dm.initial(typeof(mob_living_basic_drone), (mob_living_basic_drone d) => d.laws);
        if (Dm.initial(this, (mob_living_basic_drone d) => d.laws) != base_laws) //subtype lawset, the config doesn't apply
            return Dm.initial(this, (mob_living_basic_drone d) => d.laws);
        DmList lines = Dm.list();
        foreach (object line in Dm.each(world.file2list(Dm.text("[]/drone_laws.txt", config.directory))))
        {
            if (!Dm.truthy(line))
                continue;
            if (Dm.truthy(findtextEx(line, "#", 1, 2)))
                continue;
            lines.Add(Dm.text("[]. []", Dm.length(lines) + 1, line));
        }
        return Dm.length(lines) > 0 ? Dm.str(jointext(lines, "\n")) : base_laws;
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:177
    public override void med_hud_set_health()
    {
        set_hud_image_state(DIAG_HUD, hud_state: Dm.text("huddiag[]", RoundDiagBar(health / maxHealth)));
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:180
    public override object med_hud_set_status()
    {
        if (stat == DEAD)
        {
            set_hud_image_state(DIAG_STAT_HUD, hud_state: "huddead2");
            return null;
        }

        if (Dm.truthy(incapacitated))
        {
            set_hud_image_state(DIAG_STAT_HUD, hud_state: "hudoffline");
            return null;
        }

        set_hud_image_state(DIAG_STAT_HUD, hud_state: "hudstat");
        return null;
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:191
    public override object Destroy(float? force = null)
    {
        GLOB.drones_list.Remove(this);
        QDEL_NULL(ref listener);
        QDEL_NULL(ref built_in_camera);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:197
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        check_laws();

        object flavor = get_policy(Dm.text("[]", type));
        if (!Dm.truthy(flavor))
            flavor = flavortext;
        if (Dm.truthy(flavor))
            to_chat(this, Dm.text("[]", flavor));

        if (!Dm.truthy(picked))
            await pickVisualAppearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:210
    public override object auto_deadmin_on_login()
    {
        if (client?.holder == null)
            return TRUE;
        Dm.Deferred("code/modules/mob/living/basic/drone/_drone.dm:213", "auto_deadmin_on_login: the admins' auto-deadmin for silicons (admin tooling)");
        return base.auto_deadmin_on_login();
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:216
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        await base.death(gibbed, __extra);
        if (internal_storage != null)
            await dropItemToGround(internal_storage);
        if (head != null)
            await dropItemToGround(head);

        alert_drones(span_danger(I18n.T("DRONE NETWORK: [] is not responding.", name))); // DRONE_NET_DISCONNECT
        return null;
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:225
    public override async DmTask gib(float? drop_bitflags = null)
    {
        await dust();
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:228
    public override object get_butt_sprite() => Dm.icon(Dm.resource("icons/mob/butts.dmi"), BUTT_SPRITE_DRONE);

    // tg: code/modules/mob/living/basic/drone/_drone.dm:274
    public override object assess_threat(object judgement_criteria = null, string lasercolor = null, datum_callback weaponcheck = null) //Secbots won't hunt maintenance drones.
        => -10;

    // tg: code/modules/mob/living/basic/drone/_drone.dm:277
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await Stun(70);
        to_chat(this, span_danger("<b>ER@%R: MME^RY CO#RU9T!</b> R&$b@0tin)..."));
        if (Dm.num(severity) == 1)
        {
            await adjust_brute_loss(heavy_emp_damage);
            to_chat(this, span_userdanger("HeAV% DA%^MMA+G TO I/O CIR!%UUT!"));
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:287
    public virtual void alarm_triggered(datum source = null, object alarm_type = null, area source_area = null)
    {
        to_chat(this, Dm.text("--- [] alarm detected in []!", alarm_type, source_area.name));
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:291
    public virtual void alarm_cleared(datum source = null, object alarm_type = null, area source_area = null)
    {
        to_chat(this, Dm.text("--- [] alarm in [] has been cleared.", alarm_type, source_area.name));
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:295
    public virtual void set_shy(object new_shy = null)
    {
        shy = Dm.num(new_shy);
        shy_update();
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:299
    public virtual void shy_update()
    {
        if (Dm.truthy(shy))
        {
            REMOVE_TRAIT(this, TRAIT_CAN_STRIP, DRONE_SHY_TRAIT); // To shy to touch someone elses hat
            ADD_TRAIT(this, TRAIT_PACIFISM, DRONE_SHY_TRAIT);
        }
        else
        {
            ADD_TRAIT(this, TRAIT_CAN_STRIP, DRONE_SHY_TRAIT); // ...I wonder if I can ware pants like a hat
            REMOVE_TRAIT(this, TRAIT_PACIFISM, DRONE_SHY_TRAIT);
        }
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:307
    public override async DmTask<object> flash_act(float? intensity = null, float? override_blindness_check = null, float? affect_silicon = null, float? visual = null, Type type = null, float? length = null)
    {
        if (Dm.truthy(affect_silicon))
            return await base.flash_act(intensity, override_blindness_check, affect_silicon, visual, type, length);
        return null;
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:311
    public override object bee_friendly() => TRUE; // Why would bees pay attention to drones?

    // tg: code/modules/mob/living/basic/drone/_drone.dm:315
    public override async DmTask<object> electrocute_act(object shock_damage = null, object source = null, float? siemens_coeff = null, float? flags = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return FALSE; //So they don't die trying to fix wiring
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:318
    public override object can_track(mob_living user = null)
    {
        if (Dm.truthy(built_in_camera?.can_use()))
            return TRUE;
        return base.can_track(user);
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:228
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        object dot = Dm.list();

        //Hands
        foreach (obj_item held_thing in Dm.each<obj_item>(held_items))
        {
            if (Dm.truthy(Dm.bitand(held_thing.item_flags, ABSTRACT | HAND_ITEM)) || HAS_TRAIT(held_thing, TRAIT_EXAMINE_SKIP))
                continue;
            Dm.append(ref dot, I18n.T("It has [] in its [].", held_thing.examine_title(user), get_held_index_name(get_held_index_of_item(held_thing))));
        }

        //Internal storage
        if (internal_storage != null && !Dm.truthy(Dm.bitand(internal_storage.item_flags, ABSTRACT)))
            Dm.append(ref dot, I18n.T("It is holding [] in its internal storage.", internal_storage.examine_title(user)));

        //Cosmetic hat - provides no function other than looks
        if (head != null && !Dm.truthy(Dm.bitand(head.item_flags, ABSTRACT)))
            Dm.append(ref dot, I18n.T("It is wearing [] on its head.", head.examine_title(user)));

        //Braindead
        if (client == null && stat != DEAD)
            Dm.append(ref dot, I18n.T("Its status LED is blinking at a steady rate."));

        //Hacked
        if (Dm.truthy(hacked))
            Dm.append(ref dot, span_warning(I18n.T("Its display is glowing red!")));

        //Damaged
        if (health != maxHealth)
        {
            if (health > maxHealth * 0.33f) //Between maxHealth and about a third of maxHealth, between 30 and 10 for normal drones
                Dm.append(ref dot, span_warning(I18n.T("Its screws are slightly loose.")));
            else //otherwise, below about 33%
                Dm.append(ref dot, span_boldwarning(I18n.T("Its screws are very loose!")));
        }

        //Dead
        if (stat == DEAD)
        {
            if (client != null)
                Dm.append(ref dot, span_deadsay(I18n.T("A message repeatedly flashes on its display: \"REBOOT -- REQUIRED\".")));
            else
                Dm.append(ref dot, span_deadsay(I18n.T("A message repeatedly flashes on its display: \"ERROR -- OFFLINE\".")));
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/drone/_drone.dm:316  (the syndicate campaign G2: the hypnotic flash and grenade ask it)
    public override object hypnosis_vulnerable()
    {
        return FALSE; //It obeys its laws
    }
}

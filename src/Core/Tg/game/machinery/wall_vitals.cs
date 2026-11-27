// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/wall_vitals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MD1: the vitals display (MetaStation has 6), its advanced kind and their frames. It connects to the operating table, stasis
// bed, cryo cell, sleeper or another patient machine within three tiles (a mapped id tag first), shows the patient on its screen
// (the EKG and the breathing, each limb's damage, the blood, oxygen and toxin bars; a simple mob's outline) and beeps at each change
// of the patient's state, examines into tg's condensed health and chemical scan, flickers when EMPed and reconnects with a
// multitool. The var blocks are generated (D111), but for `connected` below.
// Its USB port takes the vitals monitor's circuit component. Not here: the screen's outline on the emissive plane
// (DEVIATION DV12).

// tg: code/game/machinery/wall_vitals.dm:24
public partial class obj_machinery_vitals_reader : obj_machinery
{
    /// What machine are we talking to
    // tg: code/game/machinery/wall_vitals.dm:74  VAR_FINAL/obj/machinery/connected
    // Hand-written: V3 types it Type (tg writes `connected = /obj/...` elsewhere and this subtree assigns it no literal), and tg keeps
    // an operating table, a structure, in it as well as machines.
    public obj connected;

    // The type's defaults (tg: code/game/machinery/wall_vitals.dm:25): with connected declared above, the var block is hand-written
    // (types-converter.md 3.2, mode Hand), so the constructor the generator wrote is here.
    public obj_machinery_vitals_reader()
    {
        name = "vitals display";
        desc = "A screen that displays the vitals of a patient.";
        icon = Dm.resource("icons/obj/machines/vitals_monitor.dmi");
        icon_state = "frame";
        verb_say = "beeps";
        verb_ask = "beeps";
        verb_exclaim = "beeps";
        layer = 3.3f;
        interaction_flags_machine = 8;
        use_power = 0;
        idle_power_usage = 10;
        active_power_usage = 100;
        processing_flags = 2;
        density = 0;
        max_integrity = 150;
        payment_department = "MED";
        armor_type = typeof(datum_armor_obj_machinery_vitals_reader);
        light_range = 1.5f;
        light_power = 0.75f;
        light_color = "#CAF0FF";
    }

    /// Static typecache of things the vitals display can connect to.
    // tg: code/game/machinery/wall_vitals.dm:56  var/static/list/connectable_typecache (a type static, rules 10.6)
    private static DmList connectable_typecache => TgState.Current.obj_machinery_vitals_reader__connectable_typecache ??= typecacheof(Dm.list(
        typeof(obj_machinery_abductor_experiment),
        typeof(obj_machinery_cryo_cell),
        typeof(obj_machinery_dna_scannernew),
        typeof(obj_machinery_gulag_teleporter),
        typeof(obj_machinery_hypnochair),
        typeof(obj_machinery_implantchair),
        typeof(obj_machinery_sleeper),
        typeof(obj_machinery_stasis),
        typeof(obj_structure_table_optable)
    )) as DmList;

    // tg: code/game/machinery/wall_vitals.dm:245  #define LOWER_BAR_OFFSET -3 (file-local)
    private const float LOWER_BAR_OFFSET = -3;

    // tg: code/game/machinery/wall_vitals.dm:96
    [DmParams("mapload", "C")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_vitals_monitor)), only_root_path: TRUE)));
        register_context();
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/wall_vitals.dm:102
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        find_machine(prioritize_by_id: true); // mappers can set an id tag to connect it to specific machines
        if (Dm.truthy(is_operational))
            await set_light_on(TRUE);
    }

    // tg: code/game/machinery/wall_vitals.dm:108
    public override object Destroy(float? force = null)
    {
        set_connection(null);
        set_patient(null); // unset connection also unsets patient, but just in case
        return base.Destroy(force);
    }

    // tg: code/game/machinery/wall_vitals.dm:113
    [SignalHandler]
    public virtual void comsig_unset_connected(params object[] __extra)
    {
        set_connection(null);
    }

    // tg: code/game/machinery/wall_vitals.dm:117
    [SignalHandler]
    public virtual void connected_occupant_changed(datum source = null, mob_living new_patient = null)
    {
        set_patient(new_patient);
    }

    // tg: code/game/machinery/wall_vitals.dm:122
    [SignalHandler]
    public virtual void connected_moved(params object[] __extra)
    {
        if (Dm.num(get_dist(this, connected)) > connection_range)
            set_connection(null);
    }

    /// Sets the machine as connected to this vitals reader
    /// If there is already a connection, it will be unset first.
    // tg: code/game/machinery/wall_vitals.dm:129
    public virtual void set_connection(obj new_connected)
    {
        if (ReferenceEquals(connected, new_connected))
            return;
        if (connected != null)
        {
            UnregisterSignal(connected, COMSIG_QDELETING);
            UnregisterSignal(connected, Dm.list(COMSIG_OPERATING_TABLE_SET_PATIENT, COMSIG_MACHINERY_SET_OCCUPANT));
            UnregisterSignal(connected, COMSIG_MOVABLE_MOVED);
            connected = null;
            set_patient(null);
            update_use_power(NO_POWER_USE);
        }
        if (new_connected == null)
            return;

        connected = new_connected;
        RegisterSignal(connected, COMSIG_QDELETING, nameof(comsig_unset_connected));
        RegisterSignals(connected, Dm.list(COMSIG_OPERATING_TABLE_SET_PATIENT, COMSIG_MACHINERY_SET_OCCUPANT), nameof(connected_occupant_changed));
        RegisterSignal(connected, COMSIG_MOVABLE_MOVED, nameof(connected_moved));
        update_use_power(IDLE_POWER_USE);

        if (connected is obj_machinery connected_machine)
            set_patient(connected_machine.occupant as mob_living);
        else if (connected is obj_structure_table_optable connected_table)
            set_patient(connected_table.patient);
    }

    // tg: code/game/machinery/wall_vitals.dm:155
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        balloon_alert(user, I18n.T("detaching..."));
        if (Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS, volume: 50)))
        {
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            await deconstruct(TRUE);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/wall_vitals.dm:162
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(is_operational))
            return NONE;

        if (connected == null)
        {
            if (find_machine() != null)
                balloon_alert(user, I18n.T("connected to []", connected.name));
            else
                balloon_alert(user, I18n.T("no connectable machines nearby!"));
            return ITEM_INTERACT_SUCCESS;
        }

        balloon_alert(user, I18n.T("disconnecting..."));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("disconnected from []", connected.name));
        set_connection(null);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Find and connects to a nearby machine
    /// If prioritize_by_id is TRUE, will first try to find a machine with the same id_tag as this vitals reader
    // tg: code/game/machinery/wall_vitals.dm:183
    public virtual obj find_machine(bool prioritize_by_id = false)
    {
        foreach (obj nearby_thing in Dm.each<obj>(view(connection_range, this)))
        {
            if (prioritize_by_id && !Dm.eq(nearby_thing.id_tag, id_tag))
                continue;
            if (!Dm.truthy(is_type_in_typecache(nearby_thing, connectable_typecache)))
                continue;

            set_connection(nearby_thing);
            return nearby_thing;
        }

        if (prioritize_by_id)
            return find_machine(prioritize_by_id: false);
        return null;
    }

    // tg: code/game/machinery/wall_vitals.dm:197
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        atom drop_loc = drop_location() as atom;
        if (Dm.truthy(disassembled))
            Dm.@new(frame, drop_loc);
        else
        {
            Dm.@new<obj_item_stack_sheet_iron>(drop_loc, 2);
            Dm.@new<obj_item_shard>(drop_loc);
            Dm.@new<obj_item_shard>(drop_loc);
        }
        qdel(this);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/machinery/wall_vitals.dm:207
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(is_operational))
            return dot;

        if (connected == null)
        {
            Dm.append(ref dot, span_notice(I18n.T("The display is currently not connected to anything. Use a [] to connect it to a neighboring machine.", EXAMINE_HINT(I18n.T("multitool")))));
            return dot;
        }

        if (patient == null || Dm.truthy(user.is_blind()))
            return dot;

        if (Dm.truthy(Dm.bitand(machine_stat, EMPED)))
        {
            Dm.append(ref dot, span_warning(I18n.T("The display is flickering erratically!")));
            return dot;
        }

        if (!issilicon(user) && !isobserver(user) && Dm.num(get_dist(this, user)) > 2)
            Dm.append(ref dot, span_notice(I18n.T("<i>You are too far away to read the display.</i>")));
        else if (HAS_TRAIT(user, TRAIT_DUMB) || !Dm.truthy(user.can_read(this, reading_check_flags: READING_CHECK_LITERACY, silent: TRUE)))
            Dm.append(ref dot, span_warning(I18n.T("You try to comprehend the display, but it's too complex for you to understand.")));
        else
        {
            Dm.append(ref dot, healthscan(user, patient, mode: SCANNER_CONDENSED, scanpower: scanpower, tochat: FALSE));
            Dm.append(ref dot, chemscan(user as mob_living, patient, tochat: FALSE));
        }
        return dot;
    }

    // tg: code/game/machinery/wall_vitals.dm:232
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = null;
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Detach";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.truthy(is_operational))
        {
            if (connected == null && Dm.eq(held_item?.tool_behaviour, TOOL_MULTITOOL))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Connect to nearby machine";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
            if (patient != null)
            {
                context[SCREENTIP_CONTEXT_SHIFT_LMB] = "Examine vitals";
                dot = CONTEXTUAL_SCREENTIP_SET;
            }
        }
        return dot;
    }

    /**
     * Returns all overlays to be shown when a simple / basic animal patient is detected
     *
     * * hp_color - color being used for general, overrall health
     */
    // tg: code/game/machinery/wall_vitals.dm:252
    public virtual DmList get_simple_mob_overlays(object hp_color)
    {
        return Dm.list(
            construct_overlay("mob", hp_color),
            construct_overlay("blood", COLOR_GRAY),
            construct_overlay("bar9", COLOR_GRAY),
            construct_overlay("bar9", COLOR_GRAY, LOWER_BAR_OFFSET));
    }

    /**
     * Returns all overlays to be shown when a humanoid patient is detected
     *
     * * hp_color - color being used for general, overrall health
     */
    // tg: code/game/machinery/wall_vitals.dm:265
    public virtual DmList get_humanoid_overlays(object hp_color)
    {
        DmList returned_overlays = Dm.list();

        foreach (object body_zone in Dm.each(GLOB.all_body_zones))
        {
            var real_part = patient.get_bodypart(Dm.str(body_zone)) as obj_item_bodypart;
            object bodypart_color = real_part == null ? COLOR_GRAY : percent_to_color((real_part.brute_dam + real_part.burn_dam) / real_part.max_damage);
            Dm.append(ref returned_overlays, construct_overlay(Dm.text("human_[]", body_zone), bodypart_color));
        }

        if (Dm.truthy(CAN_HAVE_BLOOD(patient)))
        {
            string blood_color = "#a51919";
            float blood_ratio = (patient.blood_volume - BLOOD_VOLUME_SURVIVE) / (BLOOD_VOLUME_NORMAL - BLOOD_VOLUME_SURVIVE);
            // DM's switch takes the first range that holds the value, ends included
            if (blood_ratio <= 0.2f)
                blood_color = "#a1a1a1";
            else if (blood_ratio <= 0.4f)
                blood_color = "#a18282";
            else if (blood_ratio <= 0.6f)
                blood_color = "#a16363";
            else if (blood_ratio <= 0.8f)
                blood_color = "#a14444";
            else
                blood_color = "#a51919";

            Dm.append(ref returned_overlays, construct_overlay("blood", blood_color));
        }
        else
            Dm.append(ref returned_overlays, construct_overlay("blood", COLOR_GRAY));

        if (HAS_TRAIT(patient, TRAIT_NOBREATH))
            Dm.append(ref returned_overlays, construct_overlay("bar9", COLOR_GRAY));
        else
        {
            float oxy_percent = Dm.num(patient.get_oxy_loss()) / Dm.num(patient.maxHealth);
            Dm.append(ref returned_overlays, construct_overlay(percent_to_bar(oxy_percent), "#2A72AA"));
        }

        if (HAS_TRAIT(patient, TRAIT_TOXIMMUNE))
            Dm.append(ref returned_overlays, construct_overlay("bar9", COLOR_GRAY, LOWER_BAR_OFFSET));
        else
        {
            float tox_percent = Dm.num(patient.get_tox_loss()) / Dm.num(patient.maxHealth);
            Dm.append(ref returned_overlays, construct_overlay(percent_to_bar(tox_percent), "#5d9c11", LOWER_BAR_OFFSET));
        }

        return returned_overlays;
    }

    /**
     * Returns the EKG and Respiration overlays
     *
     * * hp_color - color being used for general, overrall health
     */
    // tg: code/game/machinery/wall_vitals.dm:310
    public virtual DmList get_ekg_and_resp(object hp_color)
    {
        string ekg_icon_state = "ekg";
        string resp_icon_state = "resp";
        if (IS_DEAD_OR_FAKING(patient))
        {
            ekg_icon_state = "ekg_flat";
            resp_icon_state = "resp_flat";
        }
        else if (patient.stat == HARD_CRIT || Dm.truthy(patient.has_status_effect(typeof(datum_status_effect_jitter))))
            ekg_icon_state = "ekg_fast";

        if (patient.losebreath != 0 || HAS_TRAIT(patient, TRAIT_NOBREATH))
            resp_icon_state = "resp_flat";

        return Dm.list(
            construct_overlay(ekg_icon_state, hp_color),
            construct_overlay(resp_icon_state, "#00f7ff"));
    }

    // tg: code/game/machinery/wall_vitals.dm:328
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(is_operational))
            return dot;

        // DEVIATION(DV12): . += emissive_appearance(icon, "outline", src, alpha = src.alpha), the screen's outline on the emissive
        // plane, is not created (lighting planes and emissives are not ported).
        if (patient == null)
            return dot;

        Dm.append(ref dot, "buttons");

        object hp_color = percent_to_color((Dm.num(patient.maxHealth) - Dm.num(patient.health)) / Dm.num(patient.maxHealth));
        Dm.append(ref dot, get_ekg_and_resp(hp_color));
        if (ishuman(patient))
            Dm.append(ref dot, get_humanoid_overlays(hp_color));
        else
            Dm.append(ref dot, get_simple_mob_overlays(hp_color));
        return dot;
    }

    /// Converts a percentage to a color
    // tg: code/game/machinery/wall_vitals.dm:347
    public virtual string percent_to_color(float percent)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, EMPED | EMAGGED)))
            percent = rand(1, 100) * 0.01f;

        if (percent == 0)
            return "#2A72AA";

        // DM's switch: the first range that holds the value, ends included
        if (percent >= 0 && percent <= 0.125f)
            return "#A6BD00";
        if (percent >= 0.125f && percent <= 0.25f)
            return "#BDA600";
        if (percent >= 0.25f && percent <= 0.375f)
            return "#BD7E00";
        if (percent >= 0.375f && percent <= 0.5f)
            return "#BD4200";

        return "#BD0600";
    }

    /// Converts a percentage to a bar icon state
    // tg: code/game/machinery/wall_vitals.dm:367
    public virtual string percent_to_bar(float percent)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, EMPED | EMAGGED)))
            percent = rand(1, 100) * 0.01f;

        if (percent >= 1)
            return "bar9";
        if (percent <= 0)
            return "bar1";

        // DM's switch: the first range that holds the value, ends included
        if (percent <= 0.125f)
            return "bar1";
        if (percent <= 0.25f)
            return "bar2";
        if (percent <= 0.375f)
            return "bar3";
        if (percent <= 0.5f)
            return "bar4";
        if (percent <= 0.625f)
            return "bar5";
        if (percent <= 0.75f)
            return "bar6";
        if (percent <= 0.875f)
            return "bar7";
        if (percent <= 1)
            return "bar8";

        return "bar9"; // ??
    }

    /**
     * Helper to construct an overlay for the vitals display
     *
     * * state_to_use - icon state to use, required
     * * color_to_use - color to use, optional
     * * y_offset - offset to apply to the y position of the overlay, defaults to 0
     */
    // tg: code/game/machinery/wall_vitals.dm:403
    public virtual mutable_appearance construct_overlay(string state_to_use, object color_to_use = null, float y_offset = 0)
    {
        var overlay = (mutable_appearance)mutable_appearance(icon, state_to_use, alpha: alpha);
        overlay.appearance_flags = Dm.bitor(overlay.appearance_flags, RESET_COLOR);
        overlay.color = color_to_use;
        overlay.pixel_z = Dm.num(overlay.pixel_z) + 32;
        overlay.pixel_y = Dm.num(overlay.pixel_y) + (-32 + y_offset);
        return overlay;
    }

    // tg: code/game/machinery/wall_vitals.dm:413
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        update_appearance();
        await set_light_on(is_operational);
    }

    // tg: code/game/machinery/wall_vitals.dm:417
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!(beep_cd <= world.time) /* COOLDOWN_FINISHED */ || !Dm.truthy(is_operational))
            return null;
        if (patient == null)
        {
            stack_trace(Dm.text("[] has no patient but is still processing!", this), "code/game/machinery/wall_vitals.dm", 421);
            end_processing();
            return null;
        }

        object patient_stat = patient.stat;
        if (Dm.truthy(Dm.bitand(machine_stat, EMPED | EMAGGED)))
            patient_stat = pick(STABLE, SOFT_CRIT, HARD_CRIT, DEAD, DEAD, DEAD);

        switch (Dm.num(patient_stat))
        {
            case DEAD:
                beep_cd = world.time + 11 * SECONDS; // COOLDOWN_START
                if (!Dm.eq(last_reported_stat, DEAD))
                {
                    beep_message(I18n.T("lets out a droning beep."));
                    last_reported_stat = DEAD;
                }
                break;
            case HARD_CRIT:
                beep_cd = world.time + 5 * SECONDS;
                if (!Dm.eq(last_reported_stat, HARD_CRIT))
                {
                    beep_message(I18n.T("lets out an alternating beep."));
                    last_reported_stat = HARD_CRIT;
                }
                break;
            case SOFT_CRIT:
                beep_cd = world.time + 7 * SECONDS;
                if (!Dm.eq(last_reported_stat, SOFT_CRIT))
                {
                    beep_message(I18n.T("lets out a high pitch beep."));
                    last_reported_stat = SOFT_CRIT;
                }
                break;
            default:
                beep_cd = world.time + 7 * SECONDS;
                if (!Dm.eq(last_reported_stat, STABLE))
                {
                    beep_message(I18n.T("lets out a beep."));
                    last_reported_stat = STABLE;
                }
                break;
        }
        return null;
    }

    // tg: code/game/machinery/wall_vitals.dm:451
    public virtual void beep_message(object message)
    {
        foreach (mob viewer in Dm.each<mob>(viewers(this)))
        {
            if (viewer.client == null || HAS_TRAIT(viewer, TRAIT_DEAF))
                continue;
            if (!Dm.truthy(viewer.runechat_prefs_check(viewer, EMOTE_MESSAGE)))
                continue;
            viewer.create_chat_message(
                speaker: this,
                raw_message: message,
                runechat_flags: EMOTE_MESSAGE);
        }
    }

    /// Sets the passed mob as the active patient
    /// If there is already a patient, it will be unset first.
    // tg: code/game/machinery/wall_vitals.dm:465
    public virtual void set_patient(mob_living new_patient = null)
    {
        if (ReferenceEquals(patient, new_patient))
            return;
        SEND_SIGNAL(COMSIG_VITALS_SET_PATIENT, Dm.list(this, patient, new_patient));
        if (patient != null)
        {
            UnregisterSignal(patient, Dm.list(
                COMSIG_QDELETING,
                COMSIG_CARBON_POST_REMOVE_LIMB,
                COMSIG_CARBON_POST_ATTACH_LIMB,
                COMSIG_LIVING_HEALTH_UPDATE,
                COMSIG_LIVING_UPDATE_BLOOD_STATUS));

            patient = null;
            end_processing();
            if (!QDELING(this))
            {
                update_use_power(IDLE_POWER_USE);
                update_appearance();
            }
        }
        if (new_patient == null)
            return;
        if (QDELING(this))
        {
            CRASH(Dm.text("[] is qdeleting but tried to set a new patient ([])!", this, new_patient), "code/game/machinery/wall_vitals.dm", 486);
            return;
        }

        patient = new_patient;
        RegisterSignal(patient, COMSIG_QDELETING, nameof(comsig_unset_patient));
        RegisterSignals(patient, Dm.list(
            COMSIG_CARBON_POST_REMOVE_LIMB,
            COMSIG_CARBON_POST_ATTACH_LIMB,
            COMSIG_LIVING_HEALTH_UPDATE,
            COMSIG_LIVING_UPDATE_BLOOD_STATUS), nameof(update_overlay_on_signal));
        update_appearance();
        begin_processing();
        update_use_power(ACTIVE_POWER_USE);
        last_reported_stat = null;
    }

    // tg: code/game/machinery/wall_vitals.dm:501
    [SignalHandler]
    public virtual void comsig_unset_patient(params object[] __extra)
    {
        set_patient(null);
    }

    /// Signal proc to update the display when a signal is received.
    // tg: code/game/machinery/wall_vitals.dm:506
    [SignalHandler]
    public virtual void update_overlay_on_signal(params object[] __extra)
    {
        update_appearance();
    }

    // tg: code/game/machinery/wall_vitals.dm:510
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        beep_cd = world.time + 1 * SECONDS * rand(10, Dm.eq(severity, EMP_HEAVY) ? 120 : 60); // COOLDOWN_START
        await set_machine_stat(Dm.bitor(machine_stat, EMPED));
        _addtimer(Dm.@new<datum_callback>(this, nameof(fix_emp)), Dm.eq(severity, EMP_HEAVY) ? 150 * SECONDS : 75 * SECONDS, TIMER_DELETE_ME,
            file: "code/game/machinery/wall_vitals.dm", line: 517);
        return dot;
    }

    // tg: code/game/machinery/wall_vitals.dm:519
    public virtual async DmTask fix_emp()
    {
        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(EMPED)));
    }

    // tg: code/game/machinery/wall_vitals.dm:522
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        switch (damage_type)
        {
            case BRUTE:
                if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                    playsound(this, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 70, TRUE);
                else
                    playsound(this, Dm.resource("sound/effects/glass/glasshit.ogg"), 75, TRUE);
                break;
            case BURN:
                playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
                break;
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/wall_vitals.dm:56  /obj/machinery/vitals_reader/var/static/list/connectable_typecache
    internal DmList obj_machinery_vitals_reader__connectable_typecache;
}

// tg: code/game/machinery/wall_vitals.dm:532
public partial class obj_item_circuit_component_vitals_monitor : obj_item_circuit_component
{
    // tg: code/game/machinery/wall_vitals.dm:570
    public override object Destroy(float? force = null)
    {
        if (SSclock_component != null) // rules 10.6: a test harness world may lack the subsystem
            STOP_PROCESSING(SSclock_component, this);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/wall_vitals.dm:574
    public override async DmTask populate_ports()
    {
        status_updated = add_output_port("Status Updated", PORT_TYPE_SIGNAL) as datum_port_output;
        patient_changed = add_output_port("Patient Changed", PORT_TYPE_SIGNAL) as datum_port_output;

        patient_entity = add_output_port("Patient", PORT_TYPE_ATOM) as datum_port_output;
        defib_state = add_output_port("Defib Status", PORT_TYPE_STRING) as datum_port_output;

        patient_overall_health = add_output_port("Overall Health", PORT_TYPE_NUMBER) as datum_port_output;
        patient_brute_damage = add_output_port("Brute Damage", PORT_TYPE_NUMBER) as datum_port_output;
        patient_burn_damage = add_output_port("Burn Damage", PORT_TYPE_NUMBER) as datum_port_output;
        patient_oxy_loss = add_output_port("Suffocation Damage", PORT_TYPE_NUMBER) as datum_port_output;
        patient_tox_loss = add_output_port("Toxin Damage", PORT_TYPE_NUMBER) as datum_port_output;
        patient_brain_damage = add_output_port("Brain Damage", PORT_TYPE_NUMBER) as datum_port_output;
        patient_blood_level = add_output_port("Blood Level", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/game/machinery/wall_vitals.dm:588
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        linked_monitor = shell as obj_machinery_vitals_reader;
        RegisterSignal(shell, COMSIG_VITALS_SET_PATIENT, nameof(handle_patient_change));

        obj_machinery_vitals_reader monitor = shell as obj_machinery_vitals_reader;
        if (monitor.patient != null)
            START_PROCESSING(SSclock_component, this);
    }

    // tg: code/game/machinery/wall_vitals.dm:597
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        object dot = base.unregister_usb_parent(shell);
        linked_monitor = null;
        UnregisterSignal(shell, COMSIG_VITALS_SET_PATIENT);
        if (SSclock_component != null) // rules 10.6: a test harness world may lack the subsystem
            STOP_PROCESSING(SSclock_component, this);
        return dot;
    }

    // tg: code/game/machinery/wall_vitals.dm:603
    public override object get_ui_notices()
    {
        object dot = base.get_ui_notices();
        Dm.append(ref dot, create_ui_notice(Dm.text("Initial Delay: []", DisplayTimeText(1 * SECONDS)), "orange", "clock"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Update Interval: []", DisplayTimeText(read_cooldown_period)), "orange", "clock"));
        return dot;
    }

    // tg: code/game/machinery/wall_vitals.dm:608
    [SignalHandler]
    public virtual void handle_patient_change(datum source = null, mob_living old_patient = null, mob_living new_patient = null)
    {
        if (new_patient == null)
            STOP_PROCESSING(SSclock_component, this);
        else
            START_PROCESSING(SSclock_component, this);
        patient_entity.set_output(new_patient);
        patient_changed.set_output(COMPONENT_SIGNAL);
        // wait a small amount (arbitrary) to provide initial data
        read_cooldown = world.time + 1 * SECONDS; // COOLDOWN_START
        _addtimer(Dm.@new<datum_callback>(this, nameof(delayed_reading)), 1 * SECONDS, TIMER_UNIQUE | TIMER_OVERRIDE | TIMER_DELETE_ME, file: "code/game/machinery/wall_vitals.dm", line: 619);
    }

    /// Reads a patient value and applies a random amount of error if the monitor is emped or emagged
    // tg: code/game/machinery/wall_vitals.dm:622  #define GET_PATIENT_VALUE(monitor, value_to_get), expanded through this file-local helper (rules 11.4)
    private static float GET_PATIENT_VALUE(obj_machinery_vitals_reader monitor, object value_to_get)
        => round(Dm.num(value_to_get) * (Dm.truthy(Dm.bitand(monitor.machine_stat, EMPED | EMAGGED)) ? rand(0, 100) * 0.01f : 1), DAMAGE_PRECISION);

    // tg: code/game/machinery/wall_vitals.dm:625
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (linked_monitor?.patient == null)
            return PROCESS_KILL;
        if (!(read_cooldown <= world.time)) // COOLDOWN_FINISHED
            return null;
        update_output_stats();
        return null;
    }

    // tg: code/game/machinery/wall_vitals.dm:632
    public virtual void delayed_reading()
    {
        if (linked_monitor?.patient == null)
            return;
        update_output_stats();
    }

    // tg: code/game/machinery/wall_vitals.dm:637
    public virtual void update_output_stats()
    {
        read_cooldown = world.time + read_cooldown_period; // COOLDOWN_START
        status_updated.set_output(COMPONENT_SIGNAL);

        mob_living patient = linked_monitor.patient;
        patient_overall_health.set_output(GET_PATIENT_VALUE(linked_monitor, patient.health));
        patient_brute_damage.set_output(GET_PATIENT_VALUE(linked_monitor, patient.get_brute_loss()));
        patient_burn_damage.set_output(GET_PATIENT_VALUE(linked_monitor, patient.get_fire_loss()));
        patient_oxy_loss.set_output(GET_PATIENT_VALUE(linked_monitor, patient.get_oxy_loss()));
        patient_tox_loss.set_output(GET_PATIENT_VALUE(linked_monitor, patient.get_tox_loss()));
        patient_brain_damage.set_output(GET_PATIENT_VALUE(linked_monitor, patient.get_organ_loss(ORGAN_SLOT_BRAIN)));
        patient_blood_level.set_output(GET_PATIENT_VALUE(linked_monitor, patient.get_blood_volume(TRUE)));

        object is_defib_possible = DEFIB_POSSIBLE;
        if (iscarbon(linked_monitor.patient))
        {
            mob_living_carbon carbo_patient = linked_monitor.patient as mob_living_carbon;
            is_defib_possible = carbo_patient.can_defib();
        }

        object sw__1 = is_defib_possible;
        if (Dm.eq(sw__1, DEFIB_POSSIBLE))
            defib_state.set_output("Possible");
        else if (Dm.eq(sw__1, DEFIB_FAIL_SUICIDE) || Dm.eq(sw__1, DEFIB_FAIL_BLACKLISTED) || Dm.eq(sw__1, DEFIB_FAIL_NO_INTELLIGENCE) || Dm.eq(sw__1, DEFIB_FAIL_GOLEM))
            defib_state.set_output("Impossible");
        else if (Dm.eq(sw__1, DEFIB_FAIL_HUSK))
            defib_state.set_output("Husked");
        else if (Dm.eq(sw__1, DEFIB_FAIL_TISSUE_DAMAGE))
            defib_state.set_output("Damaged");
        else if (Dm.eq(sw__1, DEFIB_FAIL_FAILING_HEART) || Dm.eq(sw__1, DEFIB_FAIL_NO_HEART))
            defib_state.set_output("Heart Failure");
        else if (Dm.eq(sw__1, DEFIB_FAIL_FAILING_BRAIN) || Dm.eq(sw__1, DEFIB_FAIL_NO_BRAIN))
            defib_state.set_output("Brain Failure");
    }
}

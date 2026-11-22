// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_signals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file but add_context (the screentips stay deferred, live-partial.txt). The lights' emissive
// overlays are lighting (DEVIATION DV12); the signals' own lights (set_light) are tg's.

/// Pedestrian crossing signal for tram
// tg: code/modules/transport/tram/tram_signals.dm:2
public partial class obj_machinery_transport_crossing_signal : obj_machinery_transport
{
    // tg: code/modules/transport/tram/tram_signals.dm:104
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(SStransport, COMSIG_TRANSPORT_UPDATED, nameof(wake_up));
        Dm.append(ref SStransport.crossing_signals, this);
        register_context();
        return dot;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:110
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        link_tram();
        link_sensor();
        find_uplink();
    }

    // tg: code/modules/transport/tram/tram_signals.dm:116
    public override object Destroy(float? force = null)
    {
        Dm.remove(SStransport.crossing_signals, this);
        return base.Destroy(force);
    }

    // tg: code/modules/transport/tram/tram_signals.dm:120
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/transport/tram/tram_signals.dm:123
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/transport/tram/tram_signals.dm:138
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The maintenance panel is [].", Dm.truthy(panel_open) ? I18n.T("open") : I18n.T("closed"))));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("It can be flipped or rotated with a []", EXAMINE_HINT("wrench."))));
        if (Dm.eq(operating_status, TRANSPORT_REMOTE_WARNING))
        {
            Dm.append(ref dot, span_notice(I18n.T("The orange [] light is on.", EXAMINE_HINT("remote warning"))));
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Check track sensor.")));
        }
        else if (Dm.eq(operating_status, TRANSPORT_REMOTE_FAULT))
        {
            Dm.append(ref dot, span_notice(I18n.T("The blue [] light is on.", EXAMINE_HINT("telecoms failure"))));
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Check telecommunications network.")));
        }
        else if (Dm.eq(operating_status, TRANSPORT_LOCAL_FAULT))
        {
            Dm.append(ref dot, span_notice(I18n.T("The red [] light is on.", EXAMINE_HINT("local fault"))));
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Repair required.")));
        }
        float facing = Dm.num(dir);
        if (facing == NORTH || facing == SOUTH)
            Dm.append(ref dot, span_notice(I18n.T("The tram configuration display shows EAST/WEST.")));
        else if (facing == EAST || facing == WEST)
            Dm.append(ref dot, span_notice(I18n.T("The tram configuration display shows NORTH/SOUTH.")));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:159
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        balloon_alert(user, I18n.T("disabled motion sensors"));
        operating_status = TRANSPORT_LOCAL_FAULT;
        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        return TRUE;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:167
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        obj_item tool = (user as mob_living)?.get_active_held_item() as obj_item;
        if (!Dm.truthy(panel_open) || !Dm.eq(tool?.tool_behaviour, TOOL_WRENCH))
            return CLICK_ACTION_BLOCKING;

        tool.play_tool_sound(this, 50);
        await setDir(turn(Dm.num(dir), -90));
        balloon_alert(user, I18n.T("rotated"));
        find_uplink();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:178
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
            return NONE;
        if (Dm.eq(sign_dir, INBOUND))
            sign_dir = OUTBOUND;
        else if (Dm.eq(sign_dir, OUTBOUND))
            sign_dir = INBOUND;

        to_chat(user, span_notice(I18n.T("You flip directions on [].", this)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:192
    public virtual void link_sensor()
    {
        sensor_ref = WEAKREF(find_closest_valid_sensor() as datum) as datum_weakref;
        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_signals.dm:196
    public virtual void unlink_sensor()
    {
        sensor_ref = null;
        if (operating_status < TRANSPORT_REMOTE_WARNING)
            operating_status = TRANSPORT_REMOTE_WARNING;
        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_signals.dm:202
    public virtual void wake_sensor()
    {
        obj_machinery_transport_guideway_sensor linked_sensor = sensor_ref?.resolve() as obj_machinery_transport_guideway_sensor;
        if (linked_sensor == null)
            operating_status = TRANSPORT_REMOTE_WARNING;
        else if (Dm.truthy(linked_sensor.trigger_sensor()))
            operating_status = TRANSPORT_SYSTEM_NORMAL;
        else
            operating_status = TRANSPORT_REMOTE_WARNING;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:213
    public virtual void clear_uplink()
    {
        inbound = null;
        outbound = null;
        update_appearance();
    }

    /**
     * Only process if the tram is actually moving
     */
    // tg: code/modules/transport/tram/tram_signals.dm:221
    public virtual void wake_up(datum source = null, object transport_controller = null, object controller_active = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            operating_status = TRANSPORT_LOCAL_FAULT;
            update_appearance();
            return;
        }

        if (prob(TRANSPORT_BREAKDOWN_RATE))
        {
            operating_status = TRANSPORT_LOCAL_FAULT;
            Waiting.Detach(local_fault());
            return;
        }

        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        obj_machinery_transport_guideway_sensor linked_sensor = sensor_ref?.resolve() as obj_machinery_transport_guideway_sensor;

        if (Dm.truthy(malfunctioning))
        {
            operating_status = TRANSPORT_LOCAL_FAULT;
        }
        else if (tram == null || (tram.status_bits & COMM_ERROR) != 0)
        {
            operating_status = TRANSPORT_REMOTE_FAULT;
        }
        else
        {
            operating_status = TRANSPORT_SYSTEM_NORMAL;
            if (linked_sensor == null)
                link_sensor();
            wake_sensor();
        }

        update_operating();
    }

    // tg: code/modules/transport/tram/tram_signals.dm:250
    public override async DmTask on_set_machine_stat(object old_value = null)
    {
        await base.on_set_machine_stat(old_value);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            operating_status = TRANSPORT_LOCAL_FAULT;
        else
            operating_status = TRANSPORT_SYSTEM_NORMAL;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:257
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        await base.on_set_is_operational(old_value);
        if (!Dm.truthy(is_operational))
            operating_status = TRANSPORT_LOCAL_FAULT;
        else
            operating_status = TRANSPORT_SYSTEM_NORMAL;
        update_operating();
    }

    // tg: code/modules/transport/tram/tram_signals.dm:265
    public virtual void comms_change(object source = null, object controller = null, object new_status = null)
    {
        datum_transport_controller_linear_tram updated_controller = controller as datum_transport_controller_linear_tram;

        if (!Dm.eq(updated_controller.specific_transport_id, configured_transport_id))
            return;

        if (Dm.truthy(new_status))
        {
            if (Dm.eq(operating_status, TRANSPORT_REMOTE_FAULT))
                operating_status = TRANSPORT_SYSTEM_NORMAL;
        }
        else if (Dm.eq(operating_status, TRANSPORT_SYSTEM_NORMAL))
        {
            operating_status = TRANSPORT_REMOTE_FAULT;
        }
    }

    /**
     * Update processing state.
     *
     * Returns whether we are still processing.
     */
    // tg: code/modules/transport/tram/tram_signals.dm:285
    public virtual void update_operating()
    {
        update_appearance();
        // Immediately process for snappy feedback
        bool should_process = !Dm.eq(process(), PROCESS_KILL);
        if (should_process)
        {
            update_use_power(ACTIVE_POWER_USE);
            begin_processing();
            return;
        }
        update_use_power(IDLE_POWER_USE);
        end_processing();
    }

    // tg: code/modules/transport/tram/tram_signals.dm:296
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        // idle aspect is green or blue depending on the signal status
        // degraded signal operating conditions of any type show blue
        float idle_aspect = Dm.eq(operating_status, TRANSPORT_SYSTEM_NORMAL) ? XING_STATE_GREEN : XING_STATE_MALF;
        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        // tg reads tram.controller_status before its null check (a runtime with no tram, which ends this process too)
        if (tram != null && (tram.status_bits & COMM_ERROR) != 0)
            idle_aspect = XING_STATE_MALF;

        // Check for stopped states. Will kill the process since tram starting up will restart process.
        if (tram == null || !Dm.truthy(tram.controller_operational) || !Dm.truthy(tram.controller_active) || !Dm.truthy(is_operational) || !Dm.truthy(inbound) || !Dm.truthy(outbound))
        {
            // Tram missing, we lost power, or something isn't right
            // Set idle and stop processing, since the tram won't be moving
            set_signal_state(idle_aspect, force: Dm.num(!Dm.truthy(is_operational)));
            return PROCESS_KILL;
        }

        obj_structure_transport_linear tram_part = tram.return_closest_platform_to(this) as obj_structure_transport_linear;

        // The structure is gone, so we're done here.
        if (QDELETED(tram_part))
        {
            set_signal_state(idle_aspect, force: Dm.num(!Dm.truthy(is_operational)));
            return PROCESS_KILL;
        }

        // Everything will be based on position and travel direction
        float signal_pos;
        float tram_pos;
        float tram_velocity_sign; // 1 for positive axis movement, -1 for negative
        // Try to be agnostic about N-S vs E-W movement
        if (Dm.truthy(Dm.bitand(tram.travel_direction, Dm.bitor(NORTH, SOUTH))))
        {
            signal_pos = y;
            tram_pos = tram_part.y;
            tram_velocity_sign = Dm.truthy(Dm.bitand(tram.travel_direction, NORTH)) ? 1 : -1;
        }
        else
        {
            signal_pos = x;
            tram_pos = tram_part.x;
            tram_velocity_sign = Dm.truthy(Dm.bitand(tram.travel_direction, EAST)) ? 1 : -1;
        }

        // How far away are we? negative if already passed.
        float approach_distance = tram_velocity_sign * (signal_pos - (tram_pos + DEFAULT_TRAM_MIDPOINT));

        // Check if tram is driving away from us.
        if (approach_distance < -Math.Abs(DEFAULT_TRAM_MIDPOINT))
        {
            // driving away. Green. In fact, in order to reverse, it'll have to stop, so let's go ahead and kill.
            set_signal_state(idle_aspect);
            return PROCESS_KILL;
        }

        // Check the tram's terminus station.
        // INBOUND 1 < 2 < 3
        // OUTBOUND 1 > 2 > 3
        if (Dm.truthy(Dm.bitand(tram.travel_direction, WEST)) && Dm.lt(inbound, tram.destination_platform.platform_code))
        {
            set_signal_state(idle_aspect);
            return PROCESS_KILL;
        }
        if (Dm.truthy(Dm.bitand(tram.travel_direction, EAST)) && Dm.gt(outbound, tram.destination_platform.platform_code))
        {
            set_signal_state(idle_aspect);
            return PROCESS_KILL;
        }

        // Finally the interesting part where it's ACTUALLY approaching
        if (approach_distance <= red_distance_threshold && Dm.eq(operating_status, TRANSPORT_SYSTEM_NORMAL))
        {
            set_signal_state(XING_STATE_RED);
            return null;
        }
        if (approach_distance <= amber_distance_threshold && Dm.eq(operating_status, TRANSPORT_SYSTEM_NORMAL))
        {
            set_signal_state(XING_STATE_AMBER);
            return null;
        }
        set_signal_state(idle_aspect);
        return null;
    }

    /**
     * Set the signal state and update appearance.
     */
    // tg: code/modules/transport/tram/tram_signals.dm:363
    public virtual void set_signal_state(float new_state, float? force = null)
    {
        force ??= FALSE;
        if (new_state == signal_state && !Dm.truthy(force))
            return;

        signal_state = new_state;
        flick_overlay();
        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_signals.dm:371
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        float facing = Dm.num(dir);
        if (facing == SOUTH || facing == EAST)
            pixel_y = 20;
        else if (facing == NORTH || facing == WEST)
            pixel_y = 0;

        if (Dm.eq(sign_dir, INBOUND))
        {
            icon_state = "crossing-inbound";
            base_icon_state = "crossing-inbound";
        }
        else if (Dm.eq(sign_dir, OUTBOUND))
        {
            icon_state = "crossing-outbound";
            base_icon_state = "crossing-outbound";
        }

        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/transport/tram/tram_signals.dm:403
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            Waiting.NoSleep(set_light(l_on: FALSE));
            return dot;
        }

        string new_color;
        if (Dm.eq(signal_state, XING_STATE_MALF))
            new_color = LIGHT_COLOR_BABY_BLUE;
        else if (Dm.eq(signal_state, XING_STATE_GREEN))
            new_color = LIGHT_COLOR_VIVID_GREEN;
        else if (Dm.eq(signal_state, XING_STATE_AMBER))
            new_color = LIGHT_COLOR_BRIGHT_YELLOW;
        else
            new_color = LIGHT_COLOR_FLARE;

        Waiting.NoSleep(set_light(l_on: TRUE, l_color: new_color));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:423
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return dot;

        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            operating_status = TRANSPORT_LOCAL_FAULT;

        string lights_overlay = Dm.text("[]-l[]", base_icon_state, signal_state);
        string status_overlay = Dm.text("[]-s[]", base_icon_state, operating_status);

        Dm.append(ref dot, mutable_appearance(icon, lights_overlay));
        Dm.append(ref dot, mutable_appearance(icon, status_overlay));
        // the two emissive_appearance copies are lighting (DV12)
        return dot;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:625
    public virtual object find_closest_valid_sensor()
    {
        if (!Dm.truthy(z))
            return FALSE;

        DmList sensor_candidates = Dm.list();

        foreach (object candidate in Dm.each(SStransport.sensors))
        {
            if (candidate is not obj_machinery_transport_guideway_sensor sensor)
                continue;
            if (!Dm.eq(sensor.z, z))
                continue;
            if (!Dm.eq(sensor.x, x) && !Dm.truthy(Dm.bitand(sensor.dir, Dm.bitor(NORTH, SOUTH))))
                continue;
            if (!Dm.eq(sensor.y, y) && !Dm.truthy(Dm.bitand(sensor.dir, Dm.bitor(EAST, WEST))))
                continue;

            Dm.append(ref sensor_candidates, sensor);
        }

        obj_machinery_transport_guideway_sensor selected_sensor = get_closest_atom(typeof(obj_machinery_transport_guideway_sensor), sensor_candidates, this) as obj_machinery_transport_guideway_sensor;
        float sensor_distance = Dm.num(get_dist(this, selected_sensor));
        if (sensor_distance <= DEFAULT_TRAM_LENGTH)
            return selected_sensor;

        return FALSE;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:647
    public virtual object find_uplink()
    {
        if (!Dm.truthy(z))
            return FALSE;

        DmList inbound_candidates = Dm.list();
        DmList outbound_candidates = Dm.list();

        inbound = null;
        outbound = null;

        foreach (object candidate in Dm.each(SStransport.nav_beacons[configured_transport_id]))
        {
            if (candidate is not obj_effect_landmark_transport_nav_beacon_tram_platform beacon)
                continue;
            if (!Dm.eq(beacon.z, z))
                continue;

            float facing = Dm.num(dir);
            if (facing == NORTH || facing == SOUTH)
            {
                if (Math.Abs(beacon.y - y) <= DEFAULT_TRAM_LENGTH)
                {
                    if (beacon.x < x)
                        Dm.append(ref inbound_candidates, beacon);
                    else
                        Dm.append(ref outbound_candidates, beacon);
                }
            }
            else if (facing == EAST || facing == WEST)
            {
                if (Math.Abs(beacon.x - x) <= DEFAULT_TRAM_LENGTH)
                {
                    if (beacon.y < y)
                        Dm.append(ref inbound_candidates, beacon);
                    else
                        Dm.append(ref outbound_candidates, beacon);
                }
            }
        }

        obj_effect_landmark_transport_nav_beacon_tram_platform selected_inbound = get_closest_atom(typeof(obj_effect_landmark_transport_nav_beacon_tram_platform), inbound_candidates, this) as obj_effect_landmark_transport_nav_beacon_tram_platform;
        if (selected_inbound == null)
            return FALSE;

        inbound = selected_inbound.platform_code;

        obj_effect_landmark_transport_nav_beacon_tram_platform selected_outbound = get_closest_atom(typeof(obj_effect_landmark_transport_nav_beacon_tram_platform), outbound_candidates, this) as obj_effect_landmark_transport_nav_beacon_tram_platform;
        if (selected_outbound == null)
            return FALSE;

        outbound = selected_outbound.platform_code;

        update_appearance();
        return null;
    }
}

// tg: code/modules/transport/tram/tram_signals.dm:71
public partial class obj_machinery_static_signal : obj_machinery
{
    // tg: code/modules/transport/tram/tram_signals.dm:388
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        float facing = Dm.num(dir);
        if (facing == SOUTH || facing == EAST)
            pixel_y = 20;
        else if (facing == NORTH || facing == WEST)
            pixel_y = 0;

        if (Dm.eq(sign_dir, INBOUND))
        {
            icon_state = "crossing-inbound";
            base_icon_state = "crossing-inbound";
        }
        else if (Dm.eq(sign_dir, OUTBOUND))
        {
            icon_state = "crossing-outbound";
            base_icon_state = "crossing-outbound";
        }

        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/transport/tram/tram_signals.dm:441
    public override object power_change()
    {
        object dot = base.power_change();

        if (!Dm.truthy(is_operational))
        {
            Waiting.NoSleep(set_light(l_on: FALSE));
            return dot;
        }

        Waiting.NoSleep(set_light(l_on: TRUE));
        return dot;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:450
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (!Dm.truthy(is_operational))
            return dot;

        Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-l0", base_icon_state)));
        Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-s0", base_icon_state)));
        // the two emissive_appearance copies are lighting (DV12)
        return dot;
    }
}

// tg: code/modules/transport/tram/tram_signals.dm:464
public partial class obj_machinery_transport_guideway_sensor : obj_machinery_transport
{
    // tg: code/modules/transport/tram/tram_signals.dm:478
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref SStransport.sensors, this);
        return dot;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:482
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        await pair_sensor();
        RegisterSignal(SStransport, COMSIG_TRANSPORT_UPDATED, nameof(wake_up));
    }

    // tg: code/modules/transport/tram/tram_signals.dm:498
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The maintenance panel is [].", Dm.truthy(panel_open) ? I18n.T("open") : I18n.T("closed"))));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("It can be rotated with a []", EXAMINE_HINT("wrench."))));
        if (Dm.eq(operating_status, TRANSPORT_REMOTE_WARNING))
        {
            Dm.append(ref dot, span_notice(I18n.T("The orange [] light is on.", EXAMINE_HINT("remote warning"))));
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Check paired sensor.")));
        }
        else if (Dm.eq(operating_status, TRANSPORT_REMOTE_FAULT))
        {
            Dm.append(ref dot, span_notice(I18n.T("The blue [] light is on.", EXAMINE_HINT("remote fault"))));
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Paired sensor not found.")));
        }
        else if (Dm.eq(operating_status, TRANSPORT_LOCAL_FAULT))
        {
            Dm.append(ref dot, span_notice(I18n.T("The red [] light is on.", EXAMINE_HINT("local fault"))));
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Repair required.")));
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:514
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/transport/tram/tram_signals.dm:517
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/transport/tram/tram_signals.dm:520
    public virtual async DmTask pair_sensor()
    {
        await set_machine_stat(Dm.bitor(machine_stat, MAINT));
        if (paired_sensor != null)
        {
            obj_machinery_transport_guideway_sensor divorcee = paired_sensor?.resolve() as obj_machinery_transport_guideway_sensor;
            await divorcee.set_machine_stat(Dm.bitor(machine_stat, MAINT));
            divorcee.paired_sensor = null;
            divorcee.update_appearance();
            paired_sensor = null;
        }

        foreach (object candidate in Dm.each(SStransport.sensors))
        {
            if (candidate is not obj_machinery_transport_guideway_sensor potential_sensor)
                continue;
            if (potential_sensor == this)
                continue;
            float facing = Dm.num(potential_sensor.dir);
            if (facing == NORTH || facing == SOUTH)
            {
                if (Dm.eq(potential_sensor.x, x))
                {
                    paired_sensor = WEAKREF(potential_sensor) as datum_weakref;
                    await set_machine_stat(Dm.bitand(machine_stat, ~MAINT));
                    break;
                }
            }
            else if (facing == EAST || facing == WEST)
            {
                if (Dm.eq(potential_sensor.y, y))
                {
                    paired_sensor = WEAKREF(potential_sensor) as datum_weakref;
                    await set_machine_stat(Dm.bitand(machine_stat, ~MAINT));
                    break;
                }
            }
        }

        update_appearance();

        obj_machinery_transport_guideway_sensor new_partner = paired_sensor?.resolve() as obj_machinery_transport_guideway_sensor;
        if (new_partner == null)
            return;

        new_partner.paired_sensor = WEAKREF(this) as datum_weakref;
        await new_partner.set_machine_stat(Dm.bitand(machine_stat, ~MAINT));
        new_partner.update_appearance();
        playsound(this, Dm.resource("sound/machines/synth/synth_yes.ogg"), 75, vary: FALSE, use_reverb: TRUE);
    }

    // tg: code/modules/transport/tram/tram_signals.dm:557
    public override object Destroy(float? force = null)
    {
        Dm.remove(SStransport.sensors, this);
        if (paired_sensor != null)
        {
            obj_machinery_transport_guideway_sensor divorcee = paired_sensor?.resolve() as obj_machinery_transport_guideway_sensor;
            if (divorcee != null) // a partner deleted first left a dead weakref (tg runtimes on it)
            {
                Waiting.NoSleep(divorcee.set_machine_stat(Dm.bitand(machine_stat, ~MAINT)));
                divorcee.paired_sensor = null;
                divorcee.update_appearance();
            }
            playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 75, vary: FALSE, use_reverb: TRUE);
            paired_sensor = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/transport/tram/tram_signals.dm:569
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);

        if (Dm.truthy(await default_change_direction_wrench(user, tool)))
        {
            await pair_sensor();
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:576
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        // the emissive_appearance copy of each state is lighting (DV12)
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) || Dm.truthy(malfunctioning))
        {
            operating_status = TRANSPORT_LOCAL_FAULT;
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("sensor-[]", TRANSPORT_LOCAL_FAULT)));
            return dot;
        }

        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
        {
            operating_status = TRANSPORT_REMOTE_FAULT;
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("sensor-[]", TRANSPORT_REMOTE_FAULT)));
            return dot;
        }

        obj_machinery_transport_guideway_sensor buddy = paired_sensor?.resolve() as obj_machinery_transport_guideway_sensor;
        if (buddy != null)
        {
            if (!Dm.truthy(buddy.is_operational))
            {
                operating_status = TRANSPORT_REMOTE_WARNING;
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("sensor-[]", TRANSPORT_REMOTE_WARNING)));
            }
            else
            {
                operating_status = TRANSPORT_SYSTEM_NORMAL;
                Dm.append(ref dot, mutable_appearance(icon, Dm.text("sensor-[]", TRANSPORT_SYSTEM_NORMAL)));
                return dot;
            }
        }
        else
        {
            operating_status = TRANSPORT_REMOTE_FAULT;
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("sensor-[]", TRANSPORT_REMOTE_FAULT)));
        }
        return dot;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:606
    public virtual object trigger_sensor()
    {
        obj_machinery_transport_guideway_sensor buddy = paired_sensor?.resolve() as obj_machinery_transport_guideway_sensor;
        if (buddy == null)
            return FALSE;

        if (!Dm.truthy(is_operational) || !Dm.truthy(buddy.is_operational))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/transport/tram/tram_signals.dm:615
    public virtual void wake_up()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            update_appearance();
            return;
        }

        if (prob(TRANSPORT_BREAKDOWN_RATE))
        {
            operating_status = TRANSPORT_LOCAL_FAULT;
            Waiting.Detach(local_fault());
        }

        obj_machinery_transport_guideway_sensor buddy = paired_sensor?.resolve() as obj_machinery_transport_guideway_sensor;

        if (buddy != null)
            Waiting.NoSleep(set_machine_stat(Dm.bitand(machine_stat, ~MAINT)));

        update_appearance();
    }

    // tg: code/modules/transport/tram/tram_signals.dm:633
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        await base.on_set_is_operational(old_value);

        obj_machinery_transport_guideway_sensor buddy = paired_sensor?.resolve() as obj_machinery_transport_guideway_sensor;
        if (buddy != null)
            buddy.update_appearance();

        update_appearance();
    }
}

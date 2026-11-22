// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E12b (docs/ai/specs/engineering.md): the whole file, the procs that build, check, warn about and destroy the HFR. Deferred
// leaves (not-translated.md 92): the investigate logs and the admins' message of the first emergency (admin tooling).

/**
 * This section contain all procs that helps building, destroy and control the hfr
 */
public partial class obj_machinery_atmospherics_components_unary_hypertorus_core
{
    /**
     * Called by multitool_act() in hfr_parts.dm, by atmos_process() in hfr_main_processes.dm and by fusion_process() in the same file
     * This proc checks the surrounding of the core to ensure that the machine has been build correctly, returns false if there is a missing piece/wrong placed one
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:9
    public virtual object check_part_connectivity()
    {
        float dot = TRUE;
        if (!Dm.truthy(anchored) || Dm.truthy(panel_open))
            return FALSE;

        foreach (obj_machinery_hypertorus @object in Dm.each<obj_machinery_hypertorus>(orange(1, this)))
        {
            if (dot == FALSE)
                break;

            if (Dm.truthy(@object.panel_open))
                dot = FALSE;

            if (@object is obj_machinery_hypertorus_corner)
            {
                float dir = get_dir(this, @object);
                if (Dm.@in(dir, GLOB.cardinals))
                    dot = FALSE;
                switch (dir)
                {
                    case SOUTHEAST:
                        if (@object.dir != SOUTH)
                            dot = FALSE;
                        break;
                    case SOUTHWEST:
                        if (@object.dir != WEST)
                            dot = FALSE;
                        break;
                    case NORTHEAST:
                        if (@object.dir != EAST)
                            dot = FALSE;
                        break;
                    case NORTHWEST:
                        if (@object.dir != NORTH)
                            dot = FALSE;
                        break;
                }
                Dm.combine(corners, @object);
                continue;
            }

            if (get_step(@object, REVERSE_DIR(@object.dir)) != loc)
                dot = FALSE;

            if (@object is obj_machinery_hypertorus_interface hfr_interface)
            {
                if (linked_interface != null && linked_interface != hfr_interface)
                    dot = FALSE;
                linked_interface = hfr_interface;
            }
        }

        foreach (obj_machinery_atmospherics_components_unary_hypertorus @object in Dm.each<obj_machinery_atmospherics_components_unary_hypertorus>(orange(1, this)))
        {
            if (dot == FALSE)
                break;

            if (Dm.truthy(@object.panel_open))
                dot = FALSE;

            if (get_step(@object, REVERSE_DIR(@object.dir)) != loc)
                dot = FALSE;

            if (@object is obj_machinery_atmospherics_components_unary_hypertorus_fuel_input fuel_input)
            {
                if (linked_input != null && linked_input != fuel_input)
                    dot = FALSE;
                linked_input = fuel_input;
                Dm.combine(machine_parts, @object);
            }

            if (@object is obj_machinery_atmospherics_components_unary_hypertorus_waste_output waste_output)
            {
                if (linked_output != null && linked_output != waste_output)
                    dot = FALSE;
                linked_output = waste_output;
                Dm.combine(machine_parts, @object);
            }

            if (@object is obj_machinery_atmospherics_components_unary_hypertorus_moderator_input moderator_input)
            {
                if (linked_moderator != null && linked_moderator != moderator_input)
                    dot = FALSE;
                linked_moderator = moderator_input;
                Dm.combine(machine_parts, @object);
            }
        }

        if (linked_interface == null || linked_input == null || linked_moderator == null || linked_output == null || corners.len != 4)
            dot = FALSE;
        return dot;
    }

    /**
     * Called by multitool_act() in hfr_parts.dm
     * It sets the pieces to active, allowing the player to start the main reaction
     * Arguments:
     * * -user: the player doing the action
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:86
    public virtual void activate(mob_living user = null)
    {
        if (Dm.truthy(active))
        {
            to_chat(user, span_notice(I18n.T("You already activated the machine.")));
            return;
        }
        to_chat(user, span_notice(I18n.T("You link all parts toghether.")));
        active = TRUE;
        update_appearance(UPDATE_ICON);
        linked_interface.active = TRUE;
        linked_interface.update_appearance(UPDATE_ICON);
        RegisterSignal(linked_interface, COMSIG_QDELETING, nameof(unregister_signals));
        linked_input.active = TRUE;
        linked_input.update_appearance(UPDATE_ICON);
        RegisterSignal(linked_input, COMSIG_QDELETING, nameof(unregister_signals));
        linked_output.active = TRUE;
        linked_output.update_appearance(UPDATE_ICON);
        RegisterSignal(linked_output, COMSIG_QDELETING, nameof(unregister_signals));
        linked_moderator.active = TRUE;
        linked_moderator.update_appearance(UPDATE_ICON);
        RegisterSignal(linked_moderator, COMSIG_QDELETING, nameof(unregister_signals));
        foreach (obj_machinery_hypertorus_corner corner in Dm.each<obj_machinery_hypertorus_corner>(corners))
        {
            corner.active = TRUE;
            corner.update_appearance(UPDATE_ICON);
            RegisterSignal(corner, COMSIG_QDELETING, nameof(unregister_signals));
        }
        soundloop = Dm.@new<datum_looping_sound_hypertorus>(this, TRUE);
        soundloop.volume = 5;
    }

    /**
     * Called when a part gets deleted around the hfr, called on Destroy() of the hfr core in hfr_core.dm
     * Unregister the signals attached to the core from the various machines, if only_signals is false it will also call deactivate()
     * Arguments:
     * * only_signals: default FALSE, if true the proc will not call the deactivate() proc
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:118
    // The parameter is object, not the slot table's float? (rules 3.3, a "-" cell): as COMSIG_QDELETING's handler it gets the deleted part,
    // a datum, which is truthy, so a part's deletion only drops the signals (the next process_atmos deactivates through
    // check_part_connectivity), as in tg.
    [SignalHandler]
    public virtual void unregister_signals(object only_signals = null)
    {
        only_signals ??= FALSE;
        if (linked_interface != null)
            UnregisterSignal(linked_interface, COMSIG_QDELETING);
        if (linked_input != null)
            UnregisterSignal(linked_input, COMSIG_QDELETING);
        if (linked_output != null)
            UnregisterSignal(linked_output, COMSIG_QDELETING);
        if (linked_moderator != null)
            UnregisterSignal(linked_moderator, COMSIG_QDELETING);
        foreach (obj_machinery_hypertorus_corner corner in Dm.each<obj_machinery_hypertorus_corner>(corners))
            UnregisterSignal(corner, COMSIG_QDELETING);
        if (!Dm.truthy(only_signals))
            deactivate();
    }

    /**
     * Called by unregister_signals() in this file, called when the main fusion processes check_part_connectivity() returns false
     * Deactivate the various machines by setting the active var to false, updates the machines icon and set the linked machine vars to null
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:137
    public virtual void deactivate()
    {
        if (!Dm.truthy(active))
            return;
        active = FALSE;
        update_appearance(UPDATE_ICON);
        if (linked_interface != null)
        {
            linked_interface.active = FALSE;
            linked_interface.update_appearance(UPDATE_ICON);
            linked_interface = null;
        }
        if (linked_input != null)
        {
            linked_input.active = FALSE;
            linked_input.update_appearance(UPDATE_ICON);
            linked_input = null;
        }
        if (linked_output != null)
        {
            linked_output.active = FALSE;
            linked_output.update_appearance(UPDATE_ICON);
            linked_output = null;
        }
        if (linked_moderator != null)
        {
            linked_moderator.active = FALSE;
            linked_moderator.update_appearance(UPDATE_ICON);
            linked_moderator = null;
        }
        if (Dm.truthy(corners.len))
        {
            foreach (obj_machinery_hypertorus_corner corner in Dm.each<obj_machinery_hypertorus_corner>(corners))
            {
                corner.active = FALSE;
                corner.update_appearance(UPDATE_ICON);
            }
            corners = Dm.list();
        }
        QDEL_NULL(ref soundloop);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:165
    public virtual void assert_gases()
    {
        //Assert the gases that will be used/created during the process

        internal_fusion.assert_gas(typeof(datum_gas_antinoblium));

        moderator_internal.assert_gases(Dm.arglist(GLOB.meta_gas_info[META_GAS_ID]));

        if (selected_fuel == null)
            return;

        internal_fusion.assert_gases(Dm.arglist(Dm.bitor(selected_fuel.requirements, selected_fuel.primary_products)));
    }

    /**
     * Updates all related pipenets from all connected components
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:180
    public virtual void update_pipenets()
    {
        update_parents();
        linked_input.update_parents();
        linked_output.update_parents();
        linked_moderator.update_parents();
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:186
    public virtual void update_temperature_status(object seconds_per_tick = null)
    {
        fusion_temperature_archived = fusion_temperature;
        fusion_temperature = internal_fusion.temperature;
        moderator_temperature_archived = moderator_temperature;
        moderator_temperature = moderator_internal.temperature;
        coolant_temperature_archived = coolant_temperature;
        coolant_temperature = ((datum_gas_mixture)airs[1]).temperature;
        output_temperature_archived = output_temperature;
        output_temperature = ((datum_gas_mixture)linked_output.airs[1]).temperature;
        temperature_period = Dm.num(seconds_per_tick);

        //Set the power level of the fusion process
        if (fusion_temperature <= 500) // -INFINITY to 500
            power_level = 0;
        else if (fusion_temperature <= 1e3f)
            power_level = 1;
        else if (fusion_temperature <= 1e4f)
            power_level = 2;
        else if (fusion_temperature <= 1e5f)
            power_level = 3;
        else if (fusion_temperature <= 1e6f)
            power_level = 4;
        else if (fusion_temperature <= 1e7f)
            power_level = 5;
        else
            power_level = 6;
    }

    /**
     * Infrequently plays accent sounds, and adjusts main loop parameters
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:217
    public virtual void play_ambience(object seconds_per_tick = null)
    {
        // We play delam/neutral sounds at a rate determined by power and critical_threshold_proximity
        if (last_accent_sound < world.time && SPT_PROB(10, seconds_per_tick))
        {
            float aggression = min(((critical_threshold_proximity / 800) * ((power_level) / 5)), 1.0f) * 100;
            if (critical_threshold_proximity >= 300)
                playsound(this, SFX_HYPERTORUS_MELTING, max(50, aggression), FALSE, 40, 30, falloff_distance: 10);
            else
                playsound(this, SFX_HYPERTORUS_CALM, max(50, aggression), FALSE, 25, 25, falloff_distance: 10);
            float next_sound = round((100 - aggression) * 5) + 5;
            last_accent_sound = world.time + max(HYPERTORUS_ACCENT_SOUND_MIN_COOLDOWN, next_sound);
        }

        float ambient_hum = 1;
        if (Dm.truthy(check_fuel()))
            ambient_hum = power_level + 1;
        soundloop.volume = clamp(ambient_hum * 8, 0, 50);
    }

    /**
     * Called by the main fusion processes in hfr_main_processes.dm
     * Getter for fusion fuel moles
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:237
    public virtual object check_fuel()
    {
        if (selected_fuel == null)
            return FALSE;
        if (!Dm.truthy(internal_fusion.total_moles()))
            return FALSE;
        foreach (object gas_type in Dm.each(selected_fuel.requirements))
        {
            internal_fusion.assert_gas(gas_type);
            if (Dm.num(internal_fusion.moles[gas_type]) < FUSION_MOLE_THRESHOLD)
                return FALSE;
        }
        return TRUE;
    }

    /**
     * Called by the main fusion processes in hfr_main_processes.dm
     * Check the power use of the machine, return TRUE if there is enough power in the powernet
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:252
    public virtual object check_power_use()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return FALSE;
        if (Dm.eq(use_power, ACTIVE_POWER_USE))
            update_mode_power_usage(ACTIVE_POWER_USE, (power_level + 1) * MIN_POWER_USAGE); //Max around 350 KW

        return TRUE;
    }

    ///Checks if the gases in the input are the ones needed by the recipe
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:261
    public virtual object check_gas_requirements()
    {
        datum_gas_mixture contents = linked_input.airs[1] as datum_gas_mixture;
        foreach (object gas_type in Dm.each(selected_fuel.requirements))
        {
            if (!Dm.truthy(contents.moles[gas_type]))
                return FALSE;
        }
        return TRUE;
    }

    ///Removes the gases from the internal gasmix when the recipe is changed
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:269
    public virtual async DmTask dump_gases()
    {
        datum_gas_mixture remove = internal_fusion.remove(internal_fusion.total_moles()) as datum_gas_mixture;
        ((datum_gas_mixture)linked_output.airs[1]).merge(remove);
        internal_fusion.garbage_collect();
        ((datum_gas_mixture)linked_input.airs[1]).garbage_collect();
    }

    /**
     * Called by alarm() in this file
     * Check the integrity level and returns the status of the machine
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:279
    public virtual object get_status()
    {
        float integrity = Dm.num(get_integrity_percent());
        if (integrity < HYPERTORUS_MELTING_PERCENT)
            return HYPERTORUS_MELTING;

        if (integrity < HYPERTORUS_EMERGENCY_PERCENT)
            return HYPERTORUS_EMERGENCY;

        if (integrity < HYPERTORUS_DANGER_PERCENT)
            return HYPERTORUS_DANGER;

        if (integrity < HYPERTORUS_WARNING_PERCENT)
            return HYPERTORUS_WARNING;

        if (power_level > 0)
            return HYPERTORUS_NOMINAL;
        return HYPERTORUS_INACTIVE;
    }

    /**
     * Called by check_alert() in this file
     * Play a sound from the machine, the type depends on the status of the hfr
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:301
    public virtual void alarm()
    {
        switch (Dm.num(get_status()))
        {
            case HYPERTORUS_MELTING:
                playsound(this, Dm.resource("sound/announcer/alarm/bloblarm.ogg"), 100, FALSE, 40, 30, falloff_distance: 10);
                break;
            case HYPERTORUS_EMERGENCY:
                playsound(this, Dm.resource("sound/machines/engine_alert/engine_alert1.ogg"), 100, FALSE, 30, 30, falloff_distance: 10);
                break;
            case HYPERTORUS_DANGER:
                playsound(this, Dm.resource("sound/machines/engine_alert/engine_alert2.ogg"), 100, FALSE, 30, 30, falloff_distance: 10);
                break;
            case HYPERTORUS_WARNING:
                playsound(this, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 75);
                break;
        }
    }

    /**
     * Getter for the machine integrity
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:315
    public virtual object get_integrity_percent()
    {
        float integrity = critical_threshold_proximity / melting_point;
        integrity = round(100 - integrity * 100, 0.01f);
        integrity = integrity < 0 ? 0 : integrity;
        return integrity;
    }

    /**
     * Get how charged the area's APC is
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:324
    public virtual object get_area_cell_percent()
    {
        // Make sure to get APC levels from the same area the core draws from
        // Just in case people build an HFR across boundaries
        area area = get_area(this) as area;
        if (area == null)
            return 0;
        obj_machinery_power_apc apc = area.apc;
        if (apc == null)
            return 0;
        obj_item_stock_parts_power_store cell = apc.cell;
        if (cell == null)
            return 0;
        return cell.percent();
    }

    /**
     * Called by process_atmos() in hfr_main_processes.dm
     * Called after checking the damage of the machine, calls alarm() and countdown()
     * Broadcast messages into engi and common radio
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:343
    public virtual void check_alert()
    {
        if (critical_threshold_proximity < warning_point)
            return;
        if ((REALTIMEOFDAY() - lastwarning) / 10 >= WARNING_TIME_DELAY)
        {
            alarm();

            if (critical_threshold_proximity > emergency_point)
            {
                radio.talk_into(this, I18n.T("[] Integrity: []%", emergency_alert, get_integrity_percent()), common_channel);
                lastwarning = REALTIMEOFDAY();
                if (!Dm.truthy(has_reached_emergency))
                {
                    investigate_log("has reached the emergency point for the first time.", INVESTIGATE_HYPERTORUS);
                    message_admins(Dm.text("[] has reached the emergency point [].", this, ADMIN_JMP(this)));
                    has_reached_emergency = TRUE;
                }
                send_radio_explanation();
            }
            else if (critical_threshold_proximity >= critical_threshold_proximity_archived) // The damage is still going up
            {
                radio.talk_into(this, I18n.T("[] Integrity: []%", warning_alert, get_integrity_percent()), engineering_channel);
                lastwarning = REALTIMEOFDAY() - (WARNING_TIME_DELAY * 5);
                send_radio_explanation();
            }
            else // Phew, we're safe
            {
                radio.talk_into(this, I18n.T("[] Integrity: []%", safe_alert, get_integrity_percent()), engineering_channel);
                lastwarning = REALTIMEOFDAY();
            }
        }

        //Melt
        if (critical_threshold_proximity > melting_point)
            countdown();
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:369
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        warning_damage_flags = Dm.bitor(warning_damage_flags, HYPERTORUS_FLAG_EMPED);
        return dot;
    }

    /**
     * Called by check_alert() in this file
     * Called to explain in radio what the issues are with the HFR
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:380
    public virtual void send_radio_explanation()
    {
        if (Dm.truthy(Dm.bitand(warning_damage_flags, HYPERTORUS_FLAG_EMPED)))
        {
            DmList characters = Dm.list();
            Dm.append(characters, GLOB.alphabet);
            Dm.append(characters, GLOB.alphabet_upper);
            Dm.append(characters, GLOB.numerals);
            Dm.append(characters, GLOB.space);
            Dm.append(characters, GLOB.space); //double the amount of them
            object message = random_string(rand(50, 70), characters);
            radio.talk_into(this, Dm.text("[]", message), engineering_channel);
            return;
        }

        if (Dm.truthy(Dm.bitand(warning_damage_flags, HYPERTORUS_FLAG_HIGH_POWER_DAMAGE)))
            radio.talk_into(this, I18n.T("Warning! Shield destabilizing due to excessive power!"), engineering_channel);
        if (Dm.truthy(Dm.bitand(warning_damage_flags, HYPERTORUS_FLAG_IRON_CONTENT_DAMAGE)))
            radio.talk_into(this, I18n.T("Warning! Iron shards are damaging the internal core shielding!"), engineering_channel);
        if (Dm.truthy(Dm.bitand(warning_damage_flags, HYPERTORUS_FLAG_HIGH_FUEL_MIX_MOLE)))
            radio.talk_into(this, I18n.T("Warning! Fuel mix moles reaching critical levels!"), engineering_channel);
        if (Dm.truthy(Dm.bitand(warning_damage_flags, HYPERTORUS_FLAG_IRON_CONTENT_INCREASE)))
            radio.talk_into(this, I18n.T("Warning! Iron amount inside the core is increasing!"), engineering_channel);
    }

    /**
     * Called by check_alert() in this file
     * Called when the damage has reached critical levels, start the countdown before the destruction, calls meltdown()
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:406
    public virtual void countdown()
    {
        Waiting.WaitforFalse(countdown__body());
    }

    private async DmTask countdown__body()
    {
        if (Dm.truthy(final_countdown)) // We're already doing it go away
            return;
        final_countdown = TRUE;

        float critical = Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_CRITICAL_MELTDOWN);
        if (Dm.truthy(critical))
            priority_announce(I18n.T("WARNING - The explosion will likely cover a big part of the station and the coming EMP will wipe out most of the electronics. Get as far away as possible from the reactor or find a way to shut it down."), I18n.T("Alert"), Dm.resource("sound/announcer/notice/notice3.ogg"));
        string speaking = I18n.T("[] The Hypertorus fusion reactor has reached critical integrity failure. Emergency magnetic dampeners online.", emergency_alert);
        radio.talk_into(this, speaking, common_channel);

        notify_ghosts(
            I18n.T("The [] has begun melting down!", this),
            source: this,
            header: I18n.T("Meltdown Incoming"),
            ghost_sound: "sound/machines/warning-buzzer.ogg",
            notify_volume: 75);

        for (float i = HYPERTORUS_COUNTDOWN_TIME, i__end = 0; i >= i__end; i += -10)
        {
            if (critical_threshold_proximity < melting_point) // Cutting it a bit close there engineers
            {
                radio.talk_into(this, I18n.T("[] Failsafe has been disengaged.", safe_alert), common_channel);
                final_countdown = FALSE;
                return;
            }
            else if ((Dm.mod(i, 50)) != 0 && i > 50) // A message once every 5 seconds until the final 5 seconds which count down individually
            {
                await sleep(1 * SECONDS);
                continue;
            }
            else if (i > 50)
            {
                if (i == 10 * SECONDS && Dm.truthy(critical))
                    sound_to_playing_players(Dm.resource("sound/machines/hypertorus/HFR_critical_explosion.ogg"));
                speaking = I18n.T("[] remain before total integrity failure.", DisplayTimeText(i, TRUE));
            }
            else
                speaking = I18n.T("[]...", i * 0.1f);
            radio.talk_into(this, speaking, common_channel);
            await sleep(1 * SECONDS);
        }

        await meltdown();
    }

    /**
     * Called by countdown() in this file
     * Create the explosion + the gas emission before deleting the machine core.
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:451
    public virtual async DmTask meltdown()
    {
        float flash_explosion = 0;
        float light_impact_explosion = 0;
        float heavy_impact_explosion = 0;
        float devastating_explosion = 0;
        float em_pulse = Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_EMP);
        float rad_pulse = Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_RADIATION_PULSE);
        float emp_light_size = 0;
        float emp_heavy_size = 0;
        float rad_pulse_size = 0;
        float gas_spread = 0;
        float gas_pockets = 0;
        float critical = Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_CRITICAL_MELTDOWN);

        if (Dm.truthy(Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_BASE_EXPLOSION)))
        {
            flash_explosion = power_level * 3;
            light_impact_explosion = power_level * 2;
        }

        if (Dm.truthy(Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_MEDIUM_EXPLOSION)))
        {
            flash_explosion = power_level * 6;
            light_impact_explosion = power_level * 5;
            heavy_impact_explosion = power_level * 0.5f;
        }

        if (Dm.truthy(Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_DEVASTATING_EXPLOSION)))
        {
            flash_explosion = power_level * 8;
            light_impact_explosion = power_level * 7;
            heavy_impact_explosion = power_level * 2;
            devastating_explosion = power_level;
        }

        if (Dm.truthy(Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_MINIMUM_SPREAD)))
        {
            if (Dm.truthy(em_pulse))
            {
                emp_light_size = power_level * 3;
                emp_heavy_size = power_level * 1;
            }
            if (Dm.truthy(rad_pulse))
                rad_pulse_size = 2 * power_level + 8;
            gas_pockets = 5;
            gas_spread = power_level * 2;
        }

        if (Dm.truthy(Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_MEDIUM_SPREAD)))
        {
            if (Dm.truthy(em_pulse))
            {
                emp_light_size = power_level * 5;
                emp_heavy_size = power_level * 3;
            }
            if (Dm.truthy(rad_pulse))
                rad_pulse_size = power_level + 24;
            gas_pockets = 7;
            gas_spread = power_level * 4;
        }

        if (Dm.truthy(Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_BIG_SPREAD)))
        {
            if (Dm.truthy(em_pulse))
            {
                emp_light_size = power_level * 7;
                emp_heavy_size = power_level * 5;
            }
            if (Dm.truthy(rad_pulse))
                rad_pulse_size = power_level + 34;
            gas_pockets = 10;
            gas_spread = power_level * 6;
        }

        if (Dm.truthy(Dm.bitand(selected_fuel.meltdown_flags, HYPERTORUS_FLAG_MASSIVE_SPREAD)))
        {
            if (Dm.truthy(em_pulse))
            {
                emp_light_size = power_level * 9;
                emp_heavy_size = power_level * 7;
            }
            if (Dm.truthy(rad_pulse))
                rad_pulse_size = power_level + 44;
            gas_pockets = 15;
            gas_spread = power_level * 8;
        }

        DmList around_turfs = circle_range_turfs(this, gas_spread) as DmList;
        foreach (turf turf in Dm.each<turf>(around_turfs.Copy()))
        {
            if (isclosedturf(turf) || isspaceturf(turf))
            {
                Dm.remove(around_turfs, turf);
                continue;
            }
        }
        datum_gas_mixture remove_fusion = null;
        if (Dm.num(internal_fusion.total_moles()) > 0)
        {
            remove_fusion = internal_fusion.remove_ratio(0.2f) as datum_gas_mixture;
            datum_gas_mixture remove = null;
            for (float i = 1, i__end = gas_pockets; i <= i__end; i++)
            {
                remove = remove_fusion.remove_ratio(1 / gas_pockets) as datum_gas_mixture;
                turf local = pick(around_turfs) as turf;
                await local.assume_air(remove);
            }
            await loc.assume_air(internal_fusion);
        }
        datum_gas_mixture remove_moderator = null;
        if (Dm.num(moderator_internal.total_moles()) > 0)
        {
            remove_moderator = moderator_internal.remove_ratio(0.2f) as datum_gas_mixture;
            datum_gas_mixture remove = null;
            for (float i = 1, i__end = gas_pockets; i <= i__end; i++)
            {
                remove = remove_moderator.remove_ratio(1 / gas_pockets) as datum_gas_mixture;
                turf local = pick(around_turfs) as turf;
                await local.assume_air(remove);
            }
            await loc.assume_air(moderator_internal);
        }

        //Max explosion ranges: devastation = 12, heavy = 24, light = 42
        await explosion(
            origin: this,
            devastation_range: Dm.truthy(critical) ? devastating_explosion * 2 : devastating_explosion,
            heavy_impact_range: Dm.truthy(critical) ? heavy_impact_explosion * 2 : heavy_impact_explosion,
            light_impact_range: light_impact_explosion,
            flash_range: flash_explosion,
            adminlog: TRUE,
            ignorecap: TRUE
            );

        if (Dm.truthy(rad_pulse))
        {
            radiation_pulse(
                source: loc,
                max_range: rad_pulse_size,
                threshold: 0.05f
            );
        }

        if (Dm.truthy(em_pulse))
        {
            await empulse(
                epicenter: loc,
                heavy_range: Dm.truthy(critical) ? emp_heavy_size * 2 : emp_heavy_size,
                light_range: Dm.truthy(critical) ? emp_light_size * 2 : emp_heavy_size,
                emp_source: this
                );
        }

        qdel(this);
    }

    /*
     * HFR cracking related procs
     */

    /**
     * Checks for any hypertorus part that is cracked and returns it if found, otherwise returns null.
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:575
    public virtual object check_cracked_parts()
    {
        foreach (obj_machinery_atmospherics_components_unary_hypertorus part in Dm.each<obj_machinery_atmospherics_components_unary_hypertorus>(machine_parts))
        {
            if (Dm.truthy(part.cracked))
                return part;
        }
        return null;
    }

    /**
     * Causes a random hypertorus part in machine_parts to become cracked and update their appearance.
     * Returns the hypertorus part.
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:584
    public virtual object create_crack()
    {
        obj_machinery_atmospherics_components_unary_hypertorus part = pick(machine_parts) as obj_machinery_atmospherics_components_unary_hypertorus;
        part.cracked = TRUE;
        part.update_appearance(UPDATE_ICON);
        return part;
    }

    /**
     * Takes a ratio portion of target_mix and moves it to the origin's location's air.
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:593
    public virtual void spill_gases(obj origin = null, datum_gas_mixture target_mix = null, object ratio = null)
    {
        datum_gas_mixture remove_mixture = target_mix.remove_ratio(ratio) as datum_gas_mixture;
        turf origin_turf = origin.loc as turf;
        if (origin_turf == null)
            return;
        Waiting.NoSleep(origin_turf.assume_air(remove_mixture));
    }

    /**
     * Processes leaking from moderator hypercriticality.
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_procs.dm:603
    public virtual async DmTask process_moderator_overflow(object seconds_per_tick = null)
    {
        obj_machinery_atmospherics_components_unary_hypertorus cracked_part = check_cracked_parts() as obj_machinery_atmospherics_components_unary_hypertorus;
        // Processing of a preexisting crack if any.
        if (cracked_part != null)
        {
            // We have an existing crack
            float leak_rate;
            if (Dm.num(moderator_internal.return_pressure()) < HYPERTORUS_MEDIUM_SPILL_PRESSURE)
            {
                // Not high pressure, but can still leak
                if (!prob(HYPERTORUS_WEAK_SPILL_CHANCE))
                    return;
                leak_rate = HYPERTORUS_WEAK_SPILL_RATE;
            }
            else if (Dm.num(moderator_internal.return_pressure()) < HYPERTORUS_STRONG_SPILL_PRESSURE)
                // Lots of gas in here, out we go
                leak_rate = HYPERTORUS_MEDIUM_SPILL_RATE;
            else
                // Gotta go fast
                leak_rate = HYPERTORUS_STRONG_SPILL_RATE;
            spill_gases(cracked_part, moderator_internal, ratio: 1 - Dm.pow(1 - leak_rate, Dm.num(seconds_per_tick)));
            return;
        }

        // No crack. Check for conditions to cause a leak and create a crack if possible.
        if (Dm.num(moderator_internal.total_moles()) < HYPERTORUS_HYPERCRITICAL_MOLES)
            return;
        cracked_part = create_crack() as obj_machinery_atmospherics_components_unary_hypertorus;
        // See if we do anything in the initial rupture
        if (Dm.num(moderator_internal.return_pressure()) < HYPERTORUS_MEDIUM_SPILL_PRESSURE)
            return;
        if (Dm.num(moderator_internal.return_pressure()) < HYPERTORUS_STRONG_SPILL_PRESSURE)
        {
            // Medium explosion on initial rupture
            await explosion(
                origin: cracked_part,
                devastation_range: 0,
                heavy_impact_range: 0,
                light_impact_range: 1,
                flame_range: 3,
                flash_range: 3
                );
            spill_gases(cracked_part, moderator_internal, ratio: HYPERTORUS_MEDIUM_SPILL_INITIAL);
            return;
        }
        // Enough pressure for a strong explosion. Oh dear, oh dear.
        await explosion(
            origin: cracked_part,
            devastation_range: 0,
            heavy_impact_range: 1,
            light_impact_range: 3,
            flame_range: 5,
            flash_range: 5
            );
        spill_gases(cracked_part, moderator_internal, ratio: HYPERTORUS_STRONG_SPILL_INITIAL);
        return;
    }
}

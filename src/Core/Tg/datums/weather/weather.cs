// Translated from tgstation (snapshot 1d99bd7f): code/datums/weather/weather.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file: a weather's stages (telegraph, start, wind down, end) with their messages, sounds and area
// overlays, the impacted areas and turfs, the mob, turf and thunder acts and the weather reagents. The area overlays are kept as
// tg sets them (the area icon reads them); the client draws a running weather from its stage, overlay state, colour and alpha
// (gaps E2a: the weather message). The var block is generated.

/**
 * Causes weather to occur on a z level in certain area types
 *
 * The effects of weather occur across an entire z-level. For instance, lavaland has periodic ash storms that scorch most unprotected creatures.
 * Weather always occurs on different z levels at different times, regardless of weather type.
 * Can have custom durations, targets, and can automatically protect indoor areas.
 */
// tg: code/datums/weather/weather.dm:15
public partial class datum_weather : datum
{
    // tg: code/datums/weather/weather.dm:4  #define MAX_TURFS_PER_TICK 500
    private const float MAX_TURFS_PER_TICK = 500;

    // tg: code/datums/weather/weather.dm:141
    [DmParams("z_levels", "weather_data")]
    public override async DmTask New(params object[] __extra)
    {
        var z_levels = Dm.arg(__extra, 0) as DmList;
        var weather_data = Dm.arg(__extra, 1) as DmList;
        await base.New(__extra);
        impacted_z_levels = z_levels.Copy();
        weather_flags = weather_data?[WEATHER_FORCED_FLAGS] == null ? weather_flags : Dm.num(weather_data[WEATHER_FORCED_FLAGS]);
        turf_thunder_chance = weather_data?[WEATHER_FORCED_THUNDER] == null ? turf_thunder_chance : Dm.num(weather_data[WEATHER_FORCED_THUNDER]);
        telegraph_duration = weather_data?[WEATHER_FORCED_TELEGRAPH] == null ? telegraph_duration : Dm.num(weather_data[WEATHER_FORCED_TELEGRAPH]);
        end_duration = weather_data?[WEATHER_FORCED_END] == null ? end_duration : Dm.num(weather_data[WEATHER_FORCED_END]);

        object custom_reagent = weather_data?[WEATHER_FORCED_REAGENT];
        object reagent_id = null;
        if (Dm.truthy(custom_reagent))
            reagent_id = custom_reagent;
        else if (whitelist_weather_reagents != null)
            reagent_id = pick_weight_recursive(whitelist_weather_reagents);
        else if (blacklist_weather_reagents != null) // randomized
            reagent_id = get_random_reagent_id(blacklist: blacklist_weather_reagents);

        if (Dm.truthy(reagent_id))
        {
            weather_reagent = chemical_reagents_list_built()[reagent_id] as datum_reagent; // GLOB.chemical_reagents_list (DV309: built on its first read)
            weather_color = weather_reagent.color;
            weather_reagent_holder = Dm.@new<obj_effect_abstract>((object)null); // spawns in nullspace
            weather_reagent_holder.create_reagents(WEATHER_REAGENT_VOLUME, NO_REACT);
            await ((datum_reagents)weather_reagent_holder.reagents).add_reagent(reagent_id, WEATHER_REAGENT_VOLUME);
            ((datum_reagents)weather_reagent_holder.reagents).set_temperature(weather_temperature);
        }

        if (Dm.truthy(Dm.bitand(weather_flags, WEATHER_MOBS)))
            Dm.append(subsystem_tasks, SSWEATHER_MOBS);
        if (Dm.truthy(Dm.bitand(weather_flags, WEATHER_TURFS)))
            Dm.append(subsystem_tasks, SSWEATHER_TURFS);
        if (Dm.truthy(Dm.bitand(weather_flags, WEATHER_THUNDER)))
            Dm.append(subsystem_tasks, SSWEATHER_THUNDER);

        setup_weather_areas(weather_data?[WEATHER_FORCED_AREAS] as DmList);
        setup_weather_turfs();
    }

    // tg: code/datums/weather/weather.dm:179
    public override object Destroy(float? force = null)
    {
        qdel(weather_reagent_holder); // QDEL_NULL
        weather_reagent_holder = null;
        return base.Destroy(force);
    }

    /**
     * Telegraphs the beginning of the weather on the impacted z levels
     *
     * Sends sounds and details to mobs in the area
     * Calculates duration and hit areas, and makes a callback for the actual weather to start
     */
    // tg: code/datums/weather/weather.dm:190
    public virtual object telegraph(DmList weather_data = null)
    {
        if (stage == STARTUP_STAGE)
            return null;
        stage = STARTUP_STAGE;
        SSdcs.SEND_SIGNAL(COMSIG_WEATHER_TELEGRAPH(type), Dm.list(SSdcs, this)); // SEND_GLOBAL_SIGNAL
        weather_duration = weather_data?[WEATHER_FORCED_DURATION] == null ? rand(weather_duration_lower, weather_duration_upper) : Dm.num(weather_data[WEATHER_FORCED_DURATION]);
        Dm.combine(SSweather.processing, this); // SSweather.processing |= src
        update_areas();
        if (Dm.truthy(telegraph_duration))
            send_alert(telegraph_message, telegraph_sound, telegraph_sound_vol);
        _addtimer(Dm.@new<datum_callback>(this, nameof(start)), telegraph_duration, TIMER_UNIQUE, file: "code/datums/weather/weather.dm", line: 200);

        // so /datum/element/weather_listener relies on z traits to determine when to set up sound managers
        // thus if we force weather on a z level that doesn't match the target trait, we need to manually set up sound managers
        if (Dm.truthy(target_trait))
            manually_setup_sound_manager();
        return TRUE;
    }

    /// Manually add a sound manager to all mobs
    // tg: code/datums/weather/weather.dm:209
    public virtual void manually_setup_sound_manager()
    {
        DmList filtered_zs = get_impacted_zs_without_trait();
        DmList playlist = get_playlist_ref();
        // we only need to manually handle sound managers if there are zs without the trait being affected, fortunately
        if (!Dm.truthy(filtered_zs.len) || playlist == null)
            return;

        // add in dead mobs so we can get observers covered too
        foreach (mob affected in Dm.each_cast<mob>((DmList)Dm.bitor(GLOB.mob_living_list, GLOB.dead_mob_list)))
        {
            if (affected.client == null)
            {
                // this registers 400+ odd signals... maybe we should reconsider
                RegisterSignal(affected, COMSIG_MOB_LOGIN, nameof(handle_mob_log_in));
                continue;
            }

            manually_setup_sound_manager_on_mob(affected, playlist, filtered_zs);
        }

        RegisterSignal(SSdcs, COMSIG_GLOB_MOB_CREATED, nameof(handle_new_mob_sound_manager));
    }

    /// Returns a reference to the "sound playlist" for this weather type
    // tg: code/datums/weather/weather.dm:229
    public virtual DmList get_playlist_ref()
    {
        return null;
    }

    /// Returns a list of z-levels impacted that do not have the target trait
    // tg: code/datums/weather/weather.dm:233
    public virtual DmList get_impacted_zs_without_trait()
    {
        var zs_without_trait = Dm.list();
        foreach (object z in Dm.each(impacted_z_levels))
        {
            if (!Dm.truthy(SSmapping.level_trait(z, target_trait)))
                Dm.append(zs_without_trait, z);
        }
        return zs_without_trait;
    }

    // tg: code/datums/weather/weather.dm:240
    public virtual void handle_new_mob_sound_manager(datum source = null, mob the_mob = null)
    {
        // SIGNAL_HANDLER
        if (the_mob.client == null)
        {
            RegisterSignal(the_mob, COMSIG_MOB_LOGIN, nameof(handle_mob_log_in));
            return;
        }

        manually_setup_sound_manager_on_mob(the_mob);
    }

    // tg: code/datums/weather/weather.dm:249
    public virtual void handle_mob_log_in(mob source = null)
    {
        // SIGNAL_HANDLER
        if (stage >= END_STAGE)
        {
            stack_trace("Attempted to add a sound manager to a mob after weather ended", "code/datums/weather/weather.dm", 253);
            UnregisterSignal(source, COMSIG_MOB_LOGIN);
            return;
        }

        manually_setup_sound_manager_on_mob(source);
    }

    // tg: code/datums/weather/weather.dm:259
    private void manually_setup_sound_manager_on_mob(mob affected = null, DmList playlist = null, DmList filtered_zs = null)
    {
        // PRIVATE_PROC(TRUE)
        playlist ??= get_playlist_ref();
        filtered_zs ??= get_impacted_zs_without_trait();
        DmList sound_change_signals = Dm.list(
            COMSIG_WEATHER_TELEGRAPH(type),
            COMSIG_WEATHER_START(type),
            COMSIG_WEATHER_WINDDOWN(type));

        var our_comp = Waiting.NoSleep(affected.AddComponent(
            typeof(datum_component_area_sound_manager),
            Dm.named("area_loop_pairs", playlist),
            Dm.named("acceptable_zs", filtered_zs))) as datum_component_area_sound_manager;
        our_comp?.RegisterSignals(SSdcs, sound_change_signals, nameof(datum_component_area_sound_manager.handle_change));
        our_comp?.RegisterSignal(SSdcs, COMSIG_WEATHER_END(type), nameof(datum_component_area_sound_manager.handle_removal));
    }

    // tg: code/datums/weather/weather.dm:277
    public virtual void setup_weather_areas(DmList forced_areas = null)
    {
        foreach (area affected_area in Dm.each_cast<area>(forced_areas ?? get_areas(area_type)))
        {
            if (Dm.truthy(is_type_in_list(affected_area, protected_areas)))
                continue;
            if (!Dm.truthy(Dm.bitand(weather_flags, WEATHER_INDOORS)) && !Dm.truthy(affected_area.outdoors))
                continue;

            foreach (object z in Dm.each(impacted_z_levels))
            {
                int zi = (int)Dm.num(z);
                float total_turfs = affected_area.turfs_by_zlevel != null && affected_area.turfs_by_zlevel.len >= zi
                    ? Dm.length(affected_area.turfs_by_zlevel[zi]) : 0;
                if (!Dm.truthy(total_turfs))
                    continue;

                Dm.combine(impacted_areas, affected_area); // impacted_areas |= affected_area

                if (!Dm.truthy(Dm.bitand(weather_flags, Dm.bitor(WEATHER_THUNDER, WEATHER_TURFS))))
                    continue;

                string z_string = Dm.text("[]", z); // num2text(z)
                if (!Dm.truthy(impacted_z_levels_weighted[z_string]))
                    impacted_z_levels_weighted[z_string] = 0;
                if (!Dm.truthy(impacted_areas_weighted[z_string]))
                    impacted_areas_weighted[z_string] = Dm.list();

                impacted_z_levels_weighted[z_string] = Dm.num(impacted_z_levels_weighted[z_string]) + total_turfs;
                ((DmList)impacted_areas_weighted[z_string])[affected_area] = total_turfs;
                total_impacted_turfs += total_turfs;
            }
        }
    }

    /// Selects a turf impacted by weather, if available, otherwise returns null
    // tg: code/datums/weather/weather.dm:298
    public virtual object pick_turf()
    {
        object z_string = pick_weight_recursive(impacted_z_levels_weighted);
        var selected_area = pick_weight_recursive(impacted_areas_weighted[z_string] as DmList) as area;
        object z = text2num(z_string);
        var available_turfs = selected_area?.get_turfs_by_zlevel(z) as DmList;
        // Areas or turfs may change during weather events. For example, a shuttle
        // landing and departing might leave an area in 'impacted_areas' but without
        // turfs on the expected z-level, resulting in an empty 'available_turfs' list.
        if (Dm.truthy(Dm.length(available_turfs)))
            return pick(available_turfs);
        return null;
    }

    // tg: code/datums/weather/weather.dm:310
    public virtual void setup_weather_turfs()
    {
        if (!Dm.truthy(Dm.bitand(weather_flags, Dm.bitor(WEATHER_TURFS, WEATHER_THUNDER))))
            return;
        if (!Dm.truthy(total_impacted_turfs))
            return;

        if (Dm.truthy(Dm.bitand(weather_flags, WEATHER_TURFS)))
        {
            weather_turfs_per_tick = total_impacted_turfs * turf_weather_chance;
            weather_turfs_per_tick = min(weather_turfs_per_tick, MAX_TURFS_PER_TICK);
        }
        if (Dm.truthy(Dm.bitand(weather_flags, WEATHER_THUNDER)))
        {
            thunder_turfs_per_tick = total_impacted_turfs * turf_thunder_chance;
            thunder_turfs_per_tick = min(thunder_turfs_per_tick, MAX_TURFS_PER_TICK);
        }
    }

    /**
     * Starts the actual weather and effects from it
     *
     * Updates area overlays and sends sounds and messages to mobs to notify them
     * Begins dealing effects from weather to mobs in the area
     */
    // tg: code/datums/weather/weather.dm:331
    public virtual object start()
    {
        if (stage >= MAIN_STAGE)
            return FALSE;
        SSdcs.SEND_SIGNAL(COMSIG_WEATHER_START(type), Dm.list(SSdcs, this)); // SEND_GLOBAL_SIGNAL
        stage = MAIN_STAGE;
        update_areas();
        send_alert(weather_message, weather_sound);
        if (!Dm.truthy(Dm.bitand(weather_flags, WEATHER_ENDLESS)))
            _addtimer(Dm.@new<datum_callback>(this, nameof(wind_down)), weather_duration, TIMER_UNIQUE, file: "code/datums/weather/weather.dm", line: 339);
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
            impacted_area.SEND_SIGNAL(COMSIG_WEATHER_BEGAN_IN_AREA(type), Dm.list(impacted_area, this));
        return TRUE;
    }

    /**
     * Weather enters the winding down phase, stops effects
     *
     * Updates areas to be in the winding down phase
     * Sends sounds and messages to mobs to notify them
     */
    // tg: code/datums/weather/weather.dm:352
    public virtual object wind_down()
    {
        if (stage >= WIND_DOWN_STAGE)
            return FALSE;
        SSdcs.SEND_SIGNAL(COMSIG_WEATHER_WINDDOWN(type), Dm.list(SSdcs, this)); // SEND_GLOBAL_SIGNAL
        stage = WIND_DOWN_STAGE;
        update_areas();
        send_alert(end_message, end_sound, end_sound_vol);
        _addtimer(Dm.@new<datum_callback>(this, nameof(end)), end_duration, TIMER_UNIQUE, file: "code/datums/weather/weather.dm", line: 359);
        return TRUE;
    }

    /**
     * Fully ends the weather
     *
     * Effects no longer occur and area overlays are removed
     * Removes weather from processing completely
     */
    // tg: code/datums/weather/weather.dm:369
    public virtual object end()
    {
        if (stage == END_STAGE)
            return FALSE;
        SSdcs.SEND_SIGNAL(COMSIG_WEATHER_END(type), Dm.list(SSdcs, this)); // SEND_GLOBAL_SIGNAL
        UnregisterSignal(SSdcs, COMSIG_GLOB_MOB_CREATED);
        stage = END_STAGE;
        Dm.remove(SSweather.processing, this);
        update_areas();
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
            impacted_area.SEND_SIGNAL(COMSIG_WEATHER_ENDED_IN_AREA(type), Dm.list(impacted_area, this));

        if (Dm.truthy(target_trait))
        {
            foreach (mob affected in Dm.each_cast<mob>((DmList)Dm.bitor(GLOB.mob_living_list, GLOB.dead_mob_list)))
                UnregisterSignal(affected, COMSIG_MOB_LOGIN);
        }
        return TRUE;
    }

    // handles sending all alerts
    // tg: code/datums/weather/weather.dm:387
    public virtual void send_alert(object alert_msg = null, object alert_sfx = null, object alert_sfx_vol = null)
    {
        float volume = alert_sfx_vol == null ? 100 : Dm.num(alert_sfx_vol);
        foreach (object z_level in Dm.each(impacted_z_levels))
        {
            if (datum_controller_subsystem_mobs.clients_by_zlevel == null || Dm.num(z_level) > datum_controller_subsystem_mobs.clients_by_zlevel.len)
                continue;
            foreach (mob player in Dm.each_cast<mob>(((DmList)datum_controller_subsystem_mobs.clients_by_zlevel[(int)Dm.num(z_level)]).Copy()))
            {
                if (!Dm.truthy(can_get_alert(player)))
                    continue;
                if (Dm.truthy(alert_msg))
                    to_chat(player, alert_msg);
                if (Dm.truthy(alert_sfx))
                {
                    player.stop_sound_channel(CHANNEL_WEATHER);
                    Dm.output_to(player, Dm.sound(alert_sfx, channel: CHANNEL_WEATHER, volume: volume)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
                }
            }
        }
    }

    // the checks for if a mob should receive alerts, returns TRUE if can
    // tg: code/datums/weather/weather.dm:399
    public virtual object can_get_alert(mob player = null)
    {
        turf mob_turf = get_turf(player);
        if (mob_turf == null)
            return FALSE;

        if (Dm.truthy(Dm.bitand(weather_flags, WEATHER_STRICT_ALERT)) && !Dm.truthy(can_see_weather(player)))
            return FALSE;

        return TRUE;
    }

    /// Checks if the player is in or can see an area affected by the weather
    // tg: code/datums/weather/weather.dm:410
    public virtual object can_see_weather(mob player = null)
    {
        if (HAS_MIND_TRAIT(player, TRAIT_DETECT_STORM))
            return TRUE;

        foreach (area nearby in Dm.each<area>(view(player)))
        {
            if (Dm.@in(nearby, impacted_areas))
                return TRUE;
        }

        return FALSE;
    }

    /**
     * Returns TRUE if the living mob can be affected by the weather
     */
    // tg: code/datums/weather/weather.dm:423
    public virtual object can_weather_act_mob(mob_living mob_to_check = null)
    {
        turf mob_turf = get_turf(mob_to_check);

        if (mob_turf == null)
            return null;

        if (!Dm.@in(mob_turf.z, impacted_z_levels))
            return null;

        if (!Dm.@in(mob_turf.loc, impacted_areas))
            return null;

        atom to_check = mob_to_check;
        while (to_check != null && !isturf(to_check))
        {
            if (Dm.truthy(recursive_weather_protection_check(to_check)))
                return null;
            to_check = to_check.loc;
        }
        return TRUE;
    }

    /**
     * Returns TRUE if the atom should protect itself or its contents from weather
     */
    // tg: code/datums/weather/weather.dm:445
    public virtual object recursive_weather_protection_check(atom to_check = null)
    {
        return HAS_TRAIT(to_check, TRAIT_WEATHER_IMMUNE) || (Dm.truthy(immunity_type) && HAS_TRAIT(to_check, immunity_type as string));
    }

    /**
     * Returns TRUE if the turf can be affected by the weather
     */
    // tg: code/datums/weather/weather.dm:451
    public virtual object can_weather_act_turf(turf valid_weather_turf = null)
    {
        // applying weather effects to solid walls is a waste since nothing will happen
        if (isclosedturf(valid_weather_turf))
            return null;
        // same logic for space and openspace turfs
        if (is_space_or_openspace(valid_weather_turf))
            return null;
        // solid windows are also worth skipping
        var window = Dm.locate<obj_structure_window>(valid_weather_turf);
        if (window != null && Dm.truthy(window.fulltile))
            return null;

        return TRUE;
    }

    /**
     * Affects the mob with whatever the weather does
     */
    // tg: code/datums/weather/weather.dm:468
    public virtual async DmTask weather_act_mob(mob_living living = null)
    {
        float temperature_delta = weather_temperature - living.bodytemperature;
        if (living is mob_living_carbon carbon_living)
        {
            object insulation_flag = Dm.truthy(Dm.bitand(weather_flags, WEATHER_TEMPERATURE_BYPASS_CLOTHING)) ? FALSE : TRUE;
            carbon_living.adjust_bodytemperature(temperature_delta, null, null, insulation_flag, TRUE); // use_insulation, use_steps
        }
        else // stolen from carbon/adjust_bodytemperature() which should really be universally applied to living/adjust_bodytemperature()
        {
            // Use the bodytemp divisors to get the change step, with max step size
            temperature_delta = (temperature_delta > 0) ? (temperature_delta / BODYTEMP_HEAT_DIVISOR) : (temperature_delta / BODYTEMP_COLD_DIVISOR);
            // Clamp the results to the min and max step size
            temperature_delta = (temperature_delta > 0) ? min(temperature_delta, BODYTEMP_HEATING_MAX) : max(temperature_delta, BODYTEMP_COOLING_MAX);
            living.adjust_bodytemperature(temperature_delta);
        }

        if (weather_reagent == null || weather_reagent_holder == null || Dm.truthy(living.IsObscured()))
            return;

        if (weather_reagent is datum_reagent_water)
            await living.wash();

        await ((datum_reagents)weather_reagent_holder.reagents).expose(living, TOUCH);
    }

    /**
     * Affects the turf with whatever the weather does
     */
    // tg: code/datums/weather/weather.dm:493
    public virtual async DmTask weather_act_turf(turf_open weather_turf = null)
    {
        if (weather_reagent == null || weather_reagent_holder == null)
            return;

        await ((datum_reagents)weather_reagent_holder.reagents).expose(weather_turf, TOUCH, TURF_REAGENT_VOLUME_MULTIPLIER);
        foreach (atom thing in Dm.each_cast<atom>(weather_turf.contents.Copy()))
        {
            if (Dm.truthy(thing.IsObscured()) || isliving(thing))
                continue;

            await ((datum_reagents)weather_reagent_holder.reagents).expose(thing, TOUCH, TURF_REAGENT_VOLUME_MULTIPLIER);

            // Time for the sophisticated art of catching sky-booze
            if (thing is not obj_item_reagent_containers container)
                continue;

            if (!Dm.truthy(container.is_open_container()) || Dm.truthy(((datum_reagents)container.reagents).holder_full()))
                continue;

            // tg passes TURF_REAGENT_VOLUME_MULTIPLIER as add_reagent's data (its third argument), which water and the rain reagents ignore
            await ((datum_reagents)container.reagents).add_reagent(weather_reagent.type, WEATHER_REAGENT_VOLUME);
        }

        if (weather_reagent is datum_reagent_water)
            await weather_turf.wash(CLEAN_ALL, TRUE);
    }

    /**
     * Affects the turf with thunder
     */
    // tg: code/datums/weather/weather.dm:520
    public virtual async DmTask thunder_act_turf(turf_open weather_turf = null)
    {
        var thunder = Dm.@new<obj_effect_temp_visual_thunderbolt>(weather_turf);
        thunder.flash_lighting_fx(6, 2, duration: thunder.duration);

        if (Dm.truthy(thunder_color))
            thunder.color = thunder_color;

        foreach (mob_living hit_mob in Dm.each<mob_living>(weather_turf.contents.Copy()))
        {
            to_chat(hit_mob, span_userdanger(I18n.T("You've been struck by lightning!")));
            await hit_mob.electrocute_act(50, "thunder", flags: Dm.bitor(SHOCK_TESLA, SHOCK_NOGLOVES));
        }

        foreach (obj_item_stack_ore hit_ore in Dm.each<obj_item_stack_ore>(weather_turf.contents.Copy()))
        {
            if (QDELETED(hit_ore))
                continue;
            // ores that get struck by thunder are smelted
            // a bolt of lightning can reach temperatures of 30,000 Kelvin which is 5x hotter than the sun
            await hit_ore.fire_act(30000);
        }

        playsound(weather_turf, Dm.resource("sound/effects/magic/lightningbolt.ogg"), 100, extrarange: 10, falloff_distance: 10);
        weather_turf.visible_message(span_danger(I18n.T("A thunderbolt strikes []!", weather_turf)));
        Dm.@new<obj_effect_hotspot>(weather_turf);
    }

    /**
     * Updates the overlays on impacted areas
     */
    // tg: code/datums/weather/weather.dm:547
    public virtual void update_areas()
    {
        DmList new_overlay_cache = generate_overlay_cache();
        foreach (area impacted in Dm.each_cast<area>(impacted_areas))
        {
            if (Dm.truthy(Dm.length(overlay_cache)))
            {
                Dm.remove(impacted.overlays, overlay_cache);
                if (Dm.truthy(impacted_areas_blend_modes[impacted]))
                {
                    // revert the blend mode to the old state
                    impacted.blend_mode = Dm.num(impacted_areas_blend_modes[impacted]);
                    impacted_areas_blend_modes[impacted] = null;
                }
            }
            if (Dm.truthy(Dm.length(new_overlay_cache)))
            {
                Dm.append(impacted.overlays, new_overlay_cache);
                // only change the blend mode if it's not default or overlay
                if (impacted.blend_mode > BLEND_OVERLAY)
                {
                    // save the old blend mode state
                    impacted_areas_blend_modes[impacted] = impacted.blend_mode;
                    impacted.blend_mode = BLEND_OVERLAY;
                }
            }
        }

        overlay_cache = new_overlay_cache;
    }

    /// Returns a list of visual offset -> overlays to use
    // tg: code/datums/weather/weather.dm:569
    public virtual DmList generate_overlay_cache()
    {
        // We're ending, so no overlays at all
        if (stage == END_STAGE)
            return Dm.list();

        object weather_state = "";
        switch ((int)stage)
        {
            case STARTUP_STAGE:
                weather_state = telegraph_overlay;
                break;
            case MAIN_STAGE:
                weather_state = weather_overlay;
                break;
            case WIND_DOWN_STAGE:
                weather_state = end_overlay;
                break;
        }

        // Use all possible offsets
        // Yes this is a bit annoying, but it's too slow to calculate and store these from turfs, and it shouldn't (I hope) look weird
        var gen_overlay_cache = Dm.list();
        for (int offset = 0; offset <= SSmapping.max_plane_offset; offset++)
        {
            // Note: what we do here is effectively apply two overlays to each area, for every unique multiz layer they inhabit
            // One is the base, which will be masked by lighting. the other is "glowing", and provides a nice contrast
            if (Dm.truthy(use_glow))
            {
                var glow_overlay = (mutable_appearance)mutable_appearance("icons/effects/glow_weather.dmi", weather_state as string, overlay_layer, null, WEATHER_GLOW_PLANE, 100f / 255 * weather_alpha, offset_const: offset);
                glow_overlay.color = weather_color;
                Dm.append(gen_overlay_cache, glow_overlay);
            }

            // By default we render ourselves to both particle and non-particle weather, as those are mutually exclusive
            // So that particle weather can have full alpha overlays when the pref is disabled, but partially transparent overlays when its enabled
            foreach (object overlay_plane in Dm.each(overlay_planes))
            {
                var new_weather_overlay = (mutable_appearance)mutable_appearance("icons/effects/weather_effects.dmi", weather_state as string, overlay_layer, plane: Dm.num(overlay_plane), alpha: weather_alpha, offset_const: offset);
                new_weather_overlay.color = weather_color;
                Dm.append(gen_overlay_cache, new_weather_overlay);
            }
        }

        return gen_overlay_cache;
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/dcs/signals/signals_global.dm:59-62  the weather's global signals, by its type
    public static string COMSIG_WEATHER_TELEGRAPH(object event_type) => Dm.text("!weather_telegraph []", event_type);
    public static string COMSIG_WEATHER_START(object event_type) => Dm.text("!weather_start []", event_type);
    public static string COMSIG_WEATHER_WINDDOWN(object event_type) => Dm.text("!weather_winddown []", event_type);
    public static string COMSIG_WEATHER_END(object event_type) => Dm.text("!weather_end []", event_type);

    // tg: code/__DEFINES/dcs/signals/signals_area.dm:38, :40  an area's weather signals, by the weather's type
    public static string COMSIG_WEATHER_BEGAN_IN_AREA(object event_type) => Dm.text("weather_began_in_area_[]", event_type);
    public static string COMSIG_WEATHER_ENDED_IN_AREA(object event_type) => Dm.text("weather_ended_in_area_[]", event_type);
}

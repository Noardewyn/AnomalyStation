// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/mining_weather_monitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the whole file. The barometric monitor (MetaStation has two, in cargo) carries tg's weather announcer: its screen
// shows the green, yellow or red weather overlay with the matching light. The doppler radar tower (Lavaland's and the buildable one) draws
// its cable's idle power before the machines process and then shows its on or off overlay, keeps the weather anomaly core put into it
// and, with one, opens tg's AnomalousWeatherTower window: the core's charges, the weather it could summon, the weather on its level to
// clear (gaps E2a: SSweather summons, lists and winds down the weather, with the summon's ghost notice and logs).
// Not here: its GPS (the GPS component, mining's row) and the emissive overlays (DEVIATION DV12). The var blocks are generated.

/// Wall mounted mining weather tracker
// tg: code/game/machinery/mining_weather_monitor.dm:2
public partial class obj_machinery_mining_weather_monitor : obj_machinery
{
    // tg: code/game/machinery/mining_weather_monitor.dm:11
    [DmParams("mapload", "ndir", "nbuild")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_weather_announcer),
            Dm.named("state_normal", "wallgreen"),
            Dm.named("state_warning", "wallyellow"),
            Dm.named("state_danger", "wallred"),
            Dm.named("radar_z_trait", ZTRAIT_MINING)));
        return dot;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:20
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || !Dm.truthy(powered__proc()))
            return dot;
        // DEVIATION(DV12): . += emissive_appearance(icon, "emissive", src), the screen's glow on the emissive plane, is not created
        // (lighting planes and emissives are not ported).
        Dm.Deferred("code/game/machinery/mining_weather_monitor.dm:24", "update_overlays: emissive_appearance of the screen (DEVIATION DV12)");
        return dot;
    }
}

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/machinery/mining_weather_monitor.dm:43
    public DmList weather_towers;

    // tg: code/game/machinery/mining_weather_monitor.dm:43  (GLOBAL_LIST_EMPTY(weather_towers), __DEFINES/__globals.dm)
    public virtual void InitGlobalweather_towers()
    {
        weather_towers = Dm.list();
        Dm.append(ref gvars_datum_init_order, "weather_towers");
    }
}

// tg: code/game/machinery/mining_weather_monitor.dm:45
public partial class obj_machinery_power_weather_tower : obj_machinery_power
{
    // tg: code/game/machinery/mining_weather_monitor.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(anchored))
        {
            connect_to_network();
            update_appearance();
        }
        Dm.append(ref SSmachines.processing_early, this);
        string z_key = Dm.text("[]", z);
        GLOB.weather_towers[z_key] ??= Dm.list(); // LAZYADD(GLOB.weather_towers["[src.z]"], src)
        Dm.append((DmList)GLOB.weather_towers[z_key], this);
        // AddComponent(/datum/component/gps, "Radar Tower")
        Dm.Deferred("code/game/machinery/mining_weather_monitor.dm:77", "Initialize: AddComponent(/datum/component/gps, \"Radar Tower\"): the GPS (mining, not ported)");
        return dot;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:79
    public override object Destroy(float? force = null)
    {
        weather_towers_remove(Dm.text("[]", z));
        QDEL_NULL(ref core);
        Dm.remove(ref SSmachines.processing_early, this);
        return base.Destroy(force);
    }

    // LAZYREMOVE(GLOB.weather_towers["[z]"], src) (__HELPERS/_lists.dm): the list is a value of the assoc list, inline (rules 11.1)
    private void weather_towers_remove(string z_key)
    {
        if (GLOB.weather_towers[z_key] is DmList towers)
        {
            Dm.remove(towers, this);
            if (!Dm.truthy(Dm.length(towers)))
                GLOB.weather_towers[z_key] = null;
        }
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:85
    public override object connect_to_network()
    {
        return Dm.truthy(anchored) ? base.connect_to_network() : anchored;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:88
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_plasteel>(loc, Dm.truthy(disassembled) ? 12 : 4);
        return null;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:91
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == core)
        {
            core = null;
            if (!QDELING(this))
                update_appearance();
        }
        return dot;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:98
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(active))
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_on", base_icon_state), alpha: alpha));
            // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]_em", src, alpha = src.alpha) is not created
        }
        else if (Dm.truthy(anchored))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_off", base_icon_state), alpha: alpha));

        if (core != null)
        {
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_core", base_icon_state), alpha: alpha));
            // DEVIATION(DV12): . += emissive_appearance(icon, "[base_icon_state]_core_em", src, alpha = src.alpha) is not created
        }
        return dot;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:110
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (core == null)
            name = Dm.initial(this, (obj_machinery_power_weather_tower p) => p.name);
        else
            name = Dm.text("anomalous []", Dm.initial(this, (obj_machinery_power_weather_tower p) => p.name));
        return dot;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:117
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (core == null)
            Dm.append(ref dot, span_info(I18n.T("It has a slot in which you could install a weather anomaly core.")));
        else
            Dm.append(ref dot, span_info(I18n.T("It has \\a [] installed, unlocking weather control.", core)));
        return dot;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:124
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (core == null)
            return null;

        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AnomalousWeatherTower");
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:134
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (core == null)
            return UI_CLOSE;
        if (!Dm.truthy(active))
            return UI_DISABLED;
        return await base.ui_status(user, state);
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:141
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        if (core == null)
            stack_trace("Tried to get weather tower UI data with no core installed!", "code/game/machinery/mining_weather_monitor.dm", 145);

        data["core_charges"] = core?.charges;
        data["can_summon_weather"] = Dm.num(summon_weather_cd <= world.time); // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline
        data["can_clear_weather"] = Dm.num(clear_weather_cd <= world.time);
        data["active_weather_on_z"] = Dm.list();
        foreach (datum_weather ongoing in Dm.each_cast<datum_weather>(get_active_weather_on_z()))
        {
            ((DmList)data["active_weather_on_z"]).Add(Dm.list(Dm.list(
                Dm.kv("id", REF(ongoing)),
                Dm.kv("name", capitalize(ongoing.name)),
                Dm.kv("desc", ongoing.desc))));
        }

        return data;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:159
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList summonable_weather_types = Dm.list();
        foreach (Type weather_type in Dm.each_cast<Type>(get_summonable_weather_types()))
        {
            summonable_weather_types.Add(Dm.list(Dm.list(
                Dm.kv("id", weather_type),
                Dm.kv("name", capitalize(Dm.initial(weather_type, (datum_weather p) => p.name))),
                Dm.kv("desc", Dm.initial(weather_type, (datum_weather p) => p.desc)))));
        }

        data["summonable_weather_types"] = summonable_weather_types;
        data["weather_charge_cost"] = weather_charge_cost();
        data["max_core_charge"] = Dm.initial(typeof(obj_item_assembly_signaler_anomaly_weather), (obj_item_assembly_signaler_anomaly_weather p) => p.charges);
        return data;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:174
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "summon_weather":
                await summon_weather(text2path(Dm.str(@params["weather_type"])), ui.user); // sanity checks in proc
                return TRUE;

            case "clear_weather":
                clear_weather(@params["weather_ref"], ui.user); // sanity checks in proc
                return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:188
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.istype(tool, typeof(obj_item_assembly_signaler_anomaly)))
        {
            if (core != null)
            {
                to_chat(user, span_warning(I18n.T("The weather core slot is already occupied.")));
                return ITEM_INTERACT_FAILURE;
            }

            if (!Dm.istype(tool, typeof(obj_item_assembly_signaler_anomaly_weather)))
            {
                to_chat(user, span_warning(I18n.T("[] probably won't do anything useful within [].", tool, this)));
                return ITEM_INTERACT_FAILURE;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("You can't seem to part ways with [].", tool)));
                return ITEM_INTERACT_FAILURE;
            }

            core = (obj_item_assembly_signaler_anomaly_weather)tool;
            update_appearance();
            to_chat(user, span_notice(I18n.T("You install [] into [].", tool, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:211
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        if (dot == null)
            return dot;

        if (Dm.truthy(anchored))
            connect_to_network();
        else
            disconnect_from_network();
        return dot;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:221
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (old_turf != null)
            weather_towers_remove(Dm.text("[]", old_turf.z));
        if (new_turf != null)
        {
            string z_key = Dm.text("[]", new_turf.z);
            GLOB.weather_towers[z_key] ??= Dm.list(); // LAZYADD(GLOB.weather_towers["[new_turf.z]"], src)
            Dm.append((DmList)GLOB.weather_towers[z_key], this);
        }
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:228
    public override object process_early(params object[] __extra)
    {
        if (Dm.truthy(anchored) && Dm.num(surplus()) >= Dm.num(idle_power_usage))
        {
            add_load(idle_power_usage);
            if (!Dm.truthy(active))
            {
                active = TRUE;
                update_appearance();
            }
        }
        else if (Dm.truthy(active))
        {
            active = FALSE;
            update_appearance();
        }
        return null;
    }

    // tg: code/game/machinery/mining_weather_monitor.dm:239
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return null;
    }

    /// Check whether this tower is on a station z-level or not
    // tg: code/game/machinery/mining_weather_monitor.dm:243
    public virtual object is_on_station()
    {
        return Dm.num(Dm.truthy(is_station_level(z)) && !Dm.truthy(SSmapping.is_planetary()));
    }

    /// Calculate the charge cost to summon weather based on whether the tower is on station or not
    // tg: code/game/machinery/mining_weather_monitor.dm:247
    public virtual object weather_charge_cost()
    {
        return Dm.truthy(is_on_station()) ? Dm.initial(typeof(obj_item_assembly_signaler_anomaly_weather), (obj_item_assembly_signaler_anomaly_weather p) => p.charges) * 0.5f : 1;
    }

    /// Summon a weather event of the given type on this tower's z-level
    // tg: code/game/machinery/mining_weather_monitor.dm:251
    public virtual async DmTask<object> summon_weather(Type weather_type = null, mob user = null)
    {
        if (core == null || !Dm.truthy(active))
            return FALSE;
        if (!(summon_weather_cd <= world.time)) // COOLDOWN_FINISHED, inline
            return FALSE;
        if (!Dm.@in(weather_type, get_summonable_weather_types()))
            return FALSE;

        float charge_amount = Dm.num(weather_charge_cost());
        if (core.charges < charge_amount)
            return FALSE;

        float used_flags = Dm.bitor(Dm.initial(weather_type, (datum_weather p) => p.weather_flags), WEATHER_THUNDER);
        bool is_station = Dm.truthy(is_on_station());
        DmList affected_zs = Dm.list(z);
        DmList affected_areas = null;
        if (is_station)
        {
            DmList storm_free_areas = typecacheof(Dm.list(
                typeof(area_station_ai),
                typeof(area_station_commons_storage_emergency),
                typeof(area_station_maintenance),
                typeof(area_station_security_prison_safe),
                typeof(area_station_security_prison_toilet))) as DmList;

            used_flags = Dm.bitor(used_flags, WEATHER_INDOORS);
            affected_zs = (DmList)Dm.bitor(affected_zs, SSmapping.levels_by_trait(ZTRAIT_STATION));
            affected_areas = Dm.list();
            foreach (area_station station_area in Dm.each<area_station>(GLOB.areas))
            {
                if (Dm.truthy(is_type_in_typecache(station_area, storm_free_areas)))
                    continue;
                Dm.append(affected_areas, station_area);
            }
            // keep the summoner safe as well
            Dm.remove(affected_areas, get_area(this));
        }

        datum_weather weather = SSweather.run_weather(
            weather_type,
            affected_zs,
            Dm.list(
                Dm.kv(WEATHER_FORCED_AREAS, affected_areas),
                Dm.kv(WEATHER_FORCED_FLAGS, used_flags),
                Dm.kv(WEATHER_FORCED_THUNDER, 0),
                Dm.kv(WEATHER_FORCED_TELEGRAPH, 30 * SECONDS),
                Dm.kv(WEATHER_FORCED_END, 4 * MINUTES),
                Dm.kv(WEATHER_FORCED_DURATION, 30 * SECONDS)));

        bool success = weather != null;
        if (success)
        {
            visible_message(span_notice(I18n.T("The [] hums as it summons a [].", this, weather.name)));
            use_core_charge(charge_amount);
            summon_weather_cd = world.time + 8 * MINUTES; // COOLDOWN_START, inline
            clear_weather_cd = world.time + 4 * MINUTES;
            if (is_station)
            {
                notify_ghosts(I18n.T("Someone summoned weather on the station!"), this);
                log_game(Dm.text("[] summoned [] weather on the station using [] []", user != null ? key_name(user) : "Unknown", weather.name, this, AREACOORD(this)));
                message_admins(Dm.text("[] summoned [] weather on the station using [] [].", user != null ? ADMIN_LOOKUPFLW(user) : "Unknown", weather.name, this, ADMIN_COORDJMP(this)));
            }
            else
                log_game(Dm.text("[] summoned [] weather using [] [].", user != null ? key_name(user) : "Unknown", weather.name, this, AREACOORD(this)));
        }
        else
        {
            audible_message(span_warning(I18n.T("The [] emits a frustrated buzz as nothing happens.", this)));
            summon_weather_cd = world.time + 1 * MINUTES; // COOLDOWN_START, inline
        }

        return Dm.num(success);
    }

    /// Subtract a charge and handle core depletion
    // tg: code/game/machinery/mining_weather_monitor.dm:316
    public virtual void use_core_charge(object amount = null)
    {
        if (core == null)
            throw new DmRuntimeException("Tried to use weather core charge when no core is installed!");

        core.charges -= Dm.num(amount);
        if (core.charges <= 0)
        {
            visible_message(span_boldwarning(I18n.T("[] expends all of its energy and disintegrates!", core)));
            Dm.@new<obj_effect_decal_cleanable_ash_large>(loc);
            QDEL_NULL(ref core);
        }
    }

    /// Clears whatever weather datum is referenced with weather_ref
    // tg: code/game/machinery/mining_weather_monitor.dm:328
    public virtual object clear_weather(object weather_ref = null, mob user = null)
    {
        if (core == null || !Dm.truthy(active))
            return FALSE;
        if (!(clear_weather_cd <= world.time)) // COOLDOWN_FINISHED, inline
            return FALSE;

        // clear weather on zlevel
        foreach (datum_weather ongoing in Dm.each_cast<datum_weather>(get_active_weather_on_z()))
        {
            if (Dm.eq(REF(ongoing), weather_ref))
            {
                log_game(Dm.text("[] cleared [] using [] [].", user != null ? key_name(user) : "Unknown", ongoing.name, this, AREACOORD(this)));
                ongoing.wind_down();
                clear_weather_cd = world.time + 2 * MINUTES; // COOLDOWN_START, inline
                summon_weather_cd = world.time + 1 * MINUTES;
                use_core_charge(1);
                return TRUE;
            }
        }

        return FALSE;
    }

    /// Return a list of weather typepaths that this tower can summon when given a weather core.
    // tg: code/game/machinery/mining_weather_monitor.dm:347
    public virtual object get_summonable_weather_types()
    {
        DmList dot = Dm.list(
            typeof(datum_weather_particle_ash_storm),
            typeof(datum_weather_particle_rain_storm),
            typeof(datum_weather_sand_storm),
            typeof(datum_weather_snow_storm));
        if (Dm.truthy(is_on_station()))
            Dm.append(dot, typeof(datum_weather_rad_storm));
        return dot;
    }

    /// Returns a list of active weather datums that are active on this tower's z-level.
    // tg: code/game/machinery/mining_weather_monitor.dm:358
    public virtual object get_active_weather_on_z()
    {
        DmList dot = Dm.list();
        foreach (datum_weather ongoing in Dm.each_cast<datum_weather>(SSweather.processing))
        {
            if (ongoing.stage != MAIN_STAGE)
                continue;
            if (Dm.@in(z, ongoing.impacted_z_levels))
                Dm.append(dot, ongoing);
        }
        return dot;
    }
}

// tg: code/game/machinery/mining_weather_monitor.dm:371
public partial class obj_machinery_power_weather_tower_core : obj_machinery_power_weather_tower
{
    // tg: code/game/machinery/mining_weather_monitor.dm:373
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        core = Dm.@new<obj_item_assembly_signaler_anomaly_weather>(this);
        update_appearance();
        return dot;
    }
}

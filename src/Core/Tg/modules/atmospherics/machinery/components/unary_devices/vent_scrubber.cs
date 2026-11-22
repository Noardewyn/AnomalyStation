// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the air scrubbers): the init (the id tag, the filter list, the area, the atmos exposure), Destroy,
// the area assignment, the filters, the picture, the power use, the modes, update_name, the exposure (should_atmos_process,
// atmos_expose, scrub, check_turfs), power_change, can_unwrench and disconnect. Deferred leaf: the investigate log of the
// mode change (M10). Generated live-partial stub: the welder act (M4a); attack_alien since gaps X5. The var
// block is generated.

// tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:1
public partial class obj_machinery_atmospherics_components_unary_vent_scrubber : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:36
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(id_tag))
            id_tag = assign_random_name();
        object dot = base.Initialize(mapload, __extra);
        foreach (object to_filter in Dm.each(filter_types))
        {
            if (istext(to_filter))
            {
                Dm.remove(ref filter_types, to_filter);
                Dm.append(ref filter_types, gas_id2path(to_filter));
            }
        }
        assign_to_area();
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:49
    public override object Destroy(float? force = null)
    {
        disconnect_from_area();
        adjacent_turfs.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:54
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);

        area old_area = get_area(old_loc) as area;
        area new_area = get_area(this) as area;

        if (old_area == new_area)
            return dot;

        disconnect_from_area(old_area);
        assign_to_area(new_area);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:66
    public override void on_enter_area(datum source = null, area area_to_register = null)
    {
        assign_to_area(area_to_register);
        base.on_enter_area(source, area_to_register);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:70
    public virtual void assign_to_area(area target_area = null)
    {
        target_area ??= get_area(this) as area;
        //this scrubber is already assigned to an area. Unassign it from here first before reassigning it to an new area
        if (target_area == null || assigned_area != null)
            return;
        assigned_area = target_area;
        Dm.append(ref assigned_area.air_scrubbers, this);
        update_appearance(UPDATE_NAME);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:78
    public virtual void disconnect_from_area(area target_area = null)
    {
        target_area ??= get_area(this) as area;
        //you cannot unassign from an area we never were assigned to
        if (target_area == null || assigned_area != target_area)
            return;
        Dm.remove(ref assigned_area.air_scrubbers, this);
        assigned_area = null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:85
    public override void on_exit_area(datum source = null, area area_to_unregister = null)
    {
        base.on_exit_area(source, area_to_unregister);
        disconnect_from_area(area_to_unregister);
    }

    ///remove a gas or list of gases from our filter_types.used so that the scrubber can check if its supposed to be processing after each change
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:90
    public virtual object remove_filters(object filter_or_filters = null)
    {
        if (!islist(filter_or_filters))
            filter_or_filters = Dm.list(filter_or_filters);

        foreach (object gas_to_filter in Dm.each(filter_or_filters))
        {
            object translated_gas = istext(gas_to_filter) ? gas_id2path(gas_to_filter) : gas_to_filter;

            if (ispath(translated_gas, typeof(datum_gas)))
            {
                Dm.remove(ref filter_types, translated_gas);
                continue;
            }
        }

        atmos_conditions_changed();
        return TRUE;
    }

    // WARNING: This proc takes untrusted user input from toggle_filter in air alarm's ui_act
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:105
    public virtual object toggle_filters(object filter_or_filters = null)
    {
        if (!islist(filter_or_filters))
            filter_or_filters = Dm.list(filter_or_filters);

        foreach (object gas_to_filter in Dm.each(filter_or_filters))
        {
            object translated_gas = istext(gas_to_filter) ? gas_id2path(gas_to_filter) : gas_to_filter;

            if (ispath(translated_gas, typeof(datum_gas)))
            {
                if (Dm.@in(translated_gas, filter_types))
                    Dm.remove(ref filter_types, translated_gas);
                else
                    Dm.combine(ref filter_types, translated_gas);
            }
        }

        atmos_conditions_changed();
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:121
    public override void update_icon_nopipes(params object[] __extra)
    {
        cut_overlays();
        if (Dm.truthy(underfloor_state))
        {
            object cap = get_pipe_image(icon, "scrub_cap", initialize_directions);
            add_overlay(cap);
        }
        else
            PIPING_LAYER_SHIFT(this, PIPING_LAYER_DEFAULT);

        if (Dm.truthy(welded))
        {
            icon_state = "scrub_welded";
            return;
        }

        if (!Dm.truthy(nodes[1]) || !Dm.truthy(on) || !Dm.truthy(is_operational))
        {
            icon_state = "scrub_off";
            return;
        }

        if (scrubbing == ATMOS_DIRECTION_SCRUBBING)
        {
            if (Dm.truthy(widenet))
                icon_state = "scrub_wide";
            else
                icon_state = "scrub_on";
        }
        else //scrubbing == SIPHONING
            icon_state = "scrub_purge";
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:145
    public virtual void update_power_usage()
    {
        idle_power_usage = Dm.num(Dm.initial(this, (obj_machinery p) => p.idle_power_usage));
        active_power_usage = Dm.num(Dm.initial(this, (obj_machinery p) => p.idle_power_usage));
        float new_power_usage = 0;
        if (scrubbing == ATMOS_DIRECTION_SCRUBBING)
        {
            new_power_usage = idle_power_usage + idle_power_usage * Dm.length(filter_types);
            update_use_power(IDLE_POWER_USE);
        }
        else
        {
            new_power_usage = active_power_usage;
            update_use_power(ACTIVE_POWER_USE);
        }

        if (Dm.truthy(widenet))
            new_power_usage += new_power_usage * (Dm.length(adjacent_turfs) * (Dm.length(adjacent_turfs) / 2));

        update_mode_power_usage(scrubbing == ATMOS_DIRECTION_SCRUBBING ? IDLE_POWER_USE : ACTIVE_POWER_USE, new_power_usage);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:162
    public virtual void set_scrubbing(object scrubbing = null, mob user = null)
    {
        if (!Dm.eq(this.scrubbing, scrubbing))
        {
            investigate_log(Dm.text("was toggled to [] mode by []", Dm.truthy(scrubbing) ? "scrubbing" : "siphon", user == null ? "the game" : key_name(user)), INVESTIGATE_ATMOS);
        }
        this.scrubbing = Dm.num(scrubbing);
        atmos_conditions_changed();
        update_power_usage();
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:171
    public virtual void set_widenet(object widenet = null)
    {
        this.widenet = Dm.num(widenet);
        update_power_usage();
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:176
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (Dm.truthy(override_naming))
            return dot;
        name = Dm.text("\\proper [] [] []", get_area_name(this), name, id_tag);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:182
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (Dm.truthy(welded) || !Dm.truthy(is_operational))
            return FALSE;
        if (!Dm.truthy(nodes[1]) || !Dm.truthy(on) || (!Dm.truthy(filter_types) && scrubbing != ATMOS_DIRECTION_SIPHONING))
        {
            set_on(FALSE);
            return FALSE;
        }
        if (!Dm.truthy(air.moles))
            return FALSE;
        if (scrubbing == ATMOS_DIRECTION_SIPHONING || Dm.truthy(Dm.length(Dm.bitand(filter_types, air.moles))))
            return TRUE;

        return FALSE;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:197
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (Dm.truthy(welded) || !Dm.truthy(is_operational))
            return FALSE;
        if (!Dm.truthy(nodes[1]) || !Dm.truthy(on))
        {
            set_on(FALSE);
            return FALSE;
        }
        turf_open us = loc as turf_open;
        if (!istype(us, typeof(turf_open)))
            return null;
        if (Dm.truthy(scrub(us)))
            us.air_update_turf(FALSE, FALSE);

        if (Dm.truthy(widenet))
        {
            if (check_turfs_cooldown <= world.time) // COOLDOWN_FINISHED(src, check_turfs_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            {
                check_turfs();
                check_turfs_cooldown = world.time + (2 * SECONDS); // COOLDOWN_START(src, check_turfs_cooldown, 2 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
            }
            foreach (turf tile in Dm.each<turf>(adjacent_turfs))
            {
                if (Dm.truthy(scrub(tile)))
                    tile.air_update_turf(FALSE, FALSE);
            }
        }
        return TRUE;
    }

    ///filtered gases at or below this amount automatically get removed from the mix
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:219  #define MINIMUM_MOLES_TO_SCRUB (MOLAR_ACCURACY*100) (file-local, #undef at :276)
    private const float MINIMUM_MOLES_TO_SCRUB = MOLAR_ACCURACY * 100;

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:221
    public virtual object scrub(turf tile = null)
    {
        if (!istype(tile, typeof(turf)))
            return FALSE;
        datum_gas_mixture environment = Waiting.NoSleep(tile.return_air()) as datum_gas_mixture;
        datum_gas_mixture air_contents = airs[1] as datum_gas_mixture;
        DmList env_cached_moles = environment.moles;

        if (Dm.num(air_contents.return_pressure()) >= 50 * ONE_ATMOSPHERE)
            return FALSE;

        if (scrubbing == ATMOS_DIRECTION_SCRUBBING)
        {
            if (Dm.truthy(Dm.length(Dm.bitand(env_cached_moles, filter_types))))
            {
                ///contains all of the gas we're sucking out of the tile, gets put into our parent pipenet
                datum_gas_mixture filtered_out = Dm.@new<datum_gas_mixture>();
                DmList filtered_out_cached_moles = filtered_out.moles;
                filtered_out.temperature = environment.temperature;

                ///maximum percentage of the turfs gas we can filter
                float removal_ratio = min(1, volume_rate / Dm.num(environment.volume));

                float total_moles_to_remove = 0;
                foreach (object gas_id in Dm.each(Dm.bitand(filter_types, env_cached_moles)))
                    total_moles_to_remove += Dm.num(env_cached_moles[gas_id]);

                if (total_moles_to_remove == 0) //sometimes this gets non gc'd values
                {
                    environment.garbage_collect();
                    return FALSE;
                }

                foreach (object gas_id in Dm.each(Dm.bitand(filter_types, env_cached_moles)))
                {
                    filtered_out.add_gas(gas_id);
                    //take this gases portion of removal_ratio of the turfs air, or all of that gas_id if less than or equal to MINIMUM_MOLES_TO_SCRUB
                    float env_moles = Dm.num(env_cached_moles[gas_id]);
                    float transferred_moles = max(QUANTIZE(env_moles * removal_ratio * (env_moles / total_moles_to_remove)), min(MINIMUM_MOLES_TO_SCRUB, env_moles));

                    filtered_out_cached_moles[gas_id] = transferred_moles;
                    Dm.remove_at(env_cached_moles, gas_id, transferred_moles);
                }

                environment.garbage_collect();

                AirflowSeam.Vent(this, filtered_out, environment, outward: false); // DEVIATION(DV34): the clients' vent streams
                //Remix the resulting gases
                air_contents.merge(filtered_out);
                update_parents();
            }
        }
        else //Just siphoning all air
        {
            float transfer_moles = Dm.num(environment.total_moles()) * (volume_rate / Dm.num(environment.volume));

            datum_gas_mixture removed = tile.remove_air(transfer_moles) as datum_gas_mixture;

            AirflowSeam.Vent(this, removed, environment, outward: false); // DEVIATION(DV34)
            air_contents.merge(removed);
            update_parents();
        }

        return TRUE;
    }

    ///we populate a list of turfs with nonatmos-blocked cardinal turfs AND
    /// diagonal turfs that can share atmos with *both* of the cardinal turfs
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:278
    public virtual void check_turfs()
    {
        adjacent_turfs.Cut();
        turf local_turf = get_turf(this);
        adjacent_turfs = local_turf.get_atmos_adjacent_turfs(alldir: TRUE) as DmList;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:283
    public override object power_change()
    {
        object dot = base.power_change();
        update_icon_nopipes();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:306
    public override object can_unwrench__proc(mob user = null)
    {
        object dot = base.can_unwrench__proc(user);
        if (Dm.truthy(dot) && Dm.truthy(on) && Dm.truthy(is_operational))
        {
            to_chat(user, span_warning(Dm.text("You cannot unwrench [], turn it off first!", this)));
            return FALSE;
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:312
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(welded))
            Dm.append(ref dot, I18n.T("It seems welded shut."));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:348
    public override void disconnect(obj_machinery_atmospherics reference = null)
    {
        base.disconnect(reference);
        set_on(FALSE);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:287
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        obj_item welder = tool;
        await base.welder_act(user, tool);
        if (!Dm.truthy(welder.tool_start_check(user, amount: 1)))
            return TRUE;
        to_chat(user, span_notice(I18n.T("Now welding the scrubber.")));
        if (Dm.truthy(await welder.use_tool(this, user, 20, volume: 50)))
        {
            if (!Dm.truthy(welded))
            {
                user.visible_message(span_notice(I18n.T("[] welds the scrubber shut.", user)), span_notice(I18n.T("You weld the scrubber shut.")), span_hear(I18n.T("You hear welding.")));
                welded = TRUE;
            }
            else
            {
                user.visible_message(span_notice(I18n.T("[] unwelds the scrubber.", user)), span_notice(I18n.T("You unweld the scrubber.")), span_hear(I18n.T("You hear welding.")));
                welded = FALSE;
            }
            update_appearance(UPDATE_ICON);
            // pipe_vision_img = image(src, loc, dir = dir); SET_PLANE_EXPLICIT(pipe_vision_img, ABOVE_HUD_PLANE, src): the
            // ventcrawlers' pipe vision (ventcrawling, M10+)
            investigate_log(Dm.text("was [] by []", Dm.truthy(welded) ? "welded shut" : "unwelded", key_name(user)), INVESTIGATE_ATMOS);
            // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:303
            add_fingerprint(user);
        }
        return TRUE;
    }
}

// tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:317  (gaps X5: an alien clawing a welded scrubber open)
public partial class obj_machinery_atmospherics_components_unary_vent_scrubber : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_scrubber.dm:317
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(welded) || !Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return null;
        user.visible_message(span_warning(I18n.T("[] furiously claws at []!", user, this)), span_notice(I18n.T("You manage to clear away the stuff blocking the scrubber.")), span_hear(I18n.T("You hear loud scraping noises.")));
        welded = FALSE;
        update_appearance(UPDATE_ICON);
        pipe_vision_img = Dm.image(this, loc, dir: dir);
        SET_PLANE_EXPLICIT(pipe_vision_img, ABOVE_HUD_PLANE, this);
        playsound(loc, Dm.resource("sound/items/weapons/bladeslice.ogg"), 100, TRUE);
        return null;
    }
}

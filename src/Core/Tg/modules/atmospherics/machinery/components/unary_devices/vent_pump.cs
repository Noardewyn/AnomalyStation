// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the air vents): the init (with the vent's id tag and area), Destroy, the area assignment (Moved, on_enter_area, assign_to_area, disconnect_from_area, on_exit_area), the pictures
// (update_overlays, update_icon_nopipes), toggle_overclock, process_atmos (the pump), update_name, can_unwrench,
// power_change and the high-volume vent's init; the overclock fan's sound loop (M4b audio slice 5). Deferred leaves: the
// tool screentips (M9), the fan's break sound (audio slice 6), the investigate logs (M10); M5 slice 1: the overclock fan damage (take_damage, which DV27's guard keeps from the vent
// until its atom_fix and atom_break rows are translated). Generated
// live-partial stubs: on_update_integrity, atom_fix and atom_break (the fan's condition and breaking, the integrity of M5),
// attack_alien since gaps X5. The air sensor link (multitool_act) is translated (package E5). The var
// blocks are generated.

// tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:1  #define NO_BOUND 3 (file-local, #undef at :465)

// tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:3
public partial class obj_machinery_atmospherics_components_unary_vent_pump : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:50
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(id_tag))
        {
            id_tag = assign_random_name();
            // var/static/list/tool_screentips (TOOL_MULTITOOL "Log to link later with air sensor", TOOL_SCREWDRIVER
            // "Repair") and AddElement(/datum/element/contextual_screentip_tools, tool_screentips): screentips (M9)
            Dm.Deferred("code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:53", "Initialize: AddElement(/datum/element/contextual_screentip_tools, tool_screentips): screentips (M10+)");
        }
        object dot = base.Initialize(mapload, __extra);
        sound_loop = Dm.@new<datum_looping_sound_vent_pump_overclock>(this);
        assign_to_area();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:131
    public override object Destroy(float? force = null)
    {
        disconnect_from_area();
        QDEL_NULL(ref sound_loop);

        area vent_area = get_area(this) as area;
        if (vent_area != null)
            Dm.remove(ref vent_area.air_vents, this);

        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:141
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

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:153
    public override void on_enter_area(datum source = null, area area_to_register = null)
    {
        assign_to_area(area_to_register);
        base.on_enter_area(source, area_to_register);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:157
    public virtual void assign_to_area(area target_area = null)
    {
        target_area ??= get_area(this) as area;
        //this vent is already assigned to an area. Unassign it from here first before reassigning it to an new area
        if (target_area == null || assigned_area != null)
            return;
        assigned_area = target_area;
        Dm.append(ref assigned_area.air_vents, this);
        update_appearance(UPDATE_NAME);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:165
    public virtual void disconnect_from_area(area target_area = null)
    {
        target_area ??= get_area(this) as area;
        //you cannot unassign from an area we never were assigned to
        if (target_area == null || assigned_area != target_area)
            return;
        Dm.remove(ref assigned_area.air_vents, this);
        assigned_area = null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:172
    public override void on_exit_area(datum source = null, area area_to_unregister = null)
    {
        base.on_exit_area(source, area_to_unregister);
        disconnect_from_area(area_to_unregister);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:176
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (!Dm.truthy(powered__proc()))
            return dot;

        if (Dm.num(get_integrity()) <= 0)
            Dm.append(ref dot, mutable_appearance(icon, "broken"));

        else if (Dm.truthy(fan_overclocked))
            Dm.append(ref dot, mutable_appearance(icon, "overclocked"));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:187
    public override void update_icon_nopipes(params object[] __extra)
    {
        cut_overlays();
        if (Dm.truthy(underfloor_state))
        {
            object cap = get_pipe_image(icon, "vent_cap", initialize_directions);
            add_overlay(cap);
        }
        else
            PIPING_LAYER_SHIFT(this, PIPING_LAYER_DEFAULT);

        if (Dm.truthy(welded))
        {
            icon_state = "vent_welded";
            return;
        }

        if (!Dm.truthy(nodes[1]) || !Dm.truthy(on) || !Dm.truthy(is_operational))
        {
            if (icon_state == "vent_welded")
            {
                icon_state = "vent_off";
                return;
            }

            if (Dm.truthy(Dm.bitand(pump_direction, ATMOS_DIRECTION_RELEASING)))
            {
                icon_state = "vent_off";
                flick("vent_out-shutdown", this);
            }
            else // pump_direction == SIPHONING
            {
                icon_state = "vent_off";
                flick("vent_in-shutdown", this);
            }
            return;
        }

        if (icon_state == "vent_off")
        {
            if (Dm.truthy(Dm.bitand(pump_direction, ATMOS_DIRECTION_RELEASING)))
            {
                icon_state = "vent_out";
                flick("vent_out-starting", this);
            }
            else // pump_direction == SIPHONING
            {
                icon_state = "vent_in";
                flick("vent_in-starting", this);
            }
            return;
        }

        if (Dm.truthy(Dm.bitand(pump_direction, ATMOS_DIRECTION_RELEASING)))
            icon_state = "vent_out";
        else // pump_direction == SIPHONING
            icon_state = "vent_in";
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:226
    public virtual void toggle_overclock(object source = null, float? from_break = null)
    {
        from_break ??= FALSE;
        fan_overclocked = Dm.truthy(fan_overclocked) ? FALSE : TRUE;

        if (Dm.truthy(from_break))
        {
            playsound(this, Dm.resource("sound/machines/fan/fan_break.ogg"), 100); // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:230
            fan_overclocked = FALSE;
        }

        if (Dm.truthy(fan_overclocked))
            sound_loop.start();
        else
            sound_loop.stop();

        investigate_log(Dm.text("had its overlock setting [] by []", Dm.truthy(fan_overclocked) ? "enabled" : "disabled", source), INVESTIGATE_ATMOS);

        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:242
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        if (!Dm.truthy(is_operational))
            return dot.Value;
        if (!Dm.truthy(nodes[1]))
            set_on(FALSE);
        if (!Dm.truthy(on) || Dm.truthy(welded))
            return dot.Value;
        turf_open us = loc as turf_open;
        if (!istype(us, typeof(turf_open)))
            return dot.Value;

        if (Dm.truthy(fan_overclocked))
        {
            await take_damage(fan_damage_rate, sound_effect: FALSE);
            if (Dm.eq(get_integrity(), 0))
            {
                investigate_log("was destroyed as a result of overclocking", INVESTIGATE_ATMOS);
                return dot.Value;
            }
        }

        float percent_integrity = Dm.num(get_integrity_percentage());
        datum_gas_mixture air_contents = airs[1] as datum_gas_mixture;
        datum_gas_mixture environment = await us.return_air() as datum_gas_mixture;
        float environment_pressure = Dm.num(environment.return_pressure());

        if (Dm.truthy(Dm.bitand(pump_direction, ATMOS_DIRECTION_RELEASING))) // internal -> external
        {
            float pressure_delta = 10000;

            if (Dm.truthy(Dm.bitand(pressure_checks, ATMOS_EXTERNAL_BOUND)))
                pressure_delta = min(pressure_delta, (external_pressure_bound - environment_pressure));
            if (Dm.truthy(Dm.bitand(pressure_checks, ATMOS_INTERNAL_BOUND)))
                pressure_delta = min(pressure_delta, (Dm.num(air_contents.return_pressure()) - internal_pressure_bound));

            if (pressure_delta > 0)
            {
                if (air_contents.temperature > 0)
                {
                    if (!Dm.truthy(fan_overclocked) && (environment_pressure >= 50 * ONE_ATMOSPHERE))
                        return FALSE;

                    float transfer_moles = (pressure_delta * Dm.num(environment.volume)) / (air_contents.temperature * R_IDEAL_GAS_EQUATION);
                    if (!Dm.truthy(fan_overclocked) && (percent_integrity < 1))
                        transfer_moles *= percent_integrity;

                    datum_gas_mixture removed = air_contents.remove(transfer_moles) as datum_gas_mixture;

                    if (removed == null || !Dm.truthy(removed.total_moles()))
                        return dot.Value;

                    AirflowSeam.Vent(this, removed, environment, outward: true); // DEVIATION(DV34): the clients' vent streams
                    await loc.assume_air(removed);
                    update_parents();
                }
            }
        }
        else // external -> internal
        {
            float pressure_delta = 10000;
            if (Dm.truthy(Dm.bitand(pressure_checks, ATMOS_EXTERNAL_BOUND)))
                pressure_delta = min(pressure_delta, (environment_pressure - external_pressure_bound));
            if (Dm.truthy(Dm.bitand(pressure_checks, ATMOS_INTERNAL_BOUND)))
                pressure_delta = min(pressure_delta, (internal_pressure_bound - Dm.num(air_contents.return_pressure())));

            if (pressure_delta > 0 && environment.temperature > 0)
            {
                if (!Dm.truthy(fan_overclocked) && (Dm.num(air_contents.return_pressure()) >= 50 * ONE_ATMOSPHERE))
                    return FALSE;

                float transfer_moles = (pressure_delta * Dm.num(air_contents.volume)) / (environment.temperature * R_IDEAL_GAS_EQUATION);
                if (!Dm.truthy(fan_overclocked) && (percent_integrity < 1))
                    transfer_moles *= percent_integrity;

                datum_gas_mixture removed = loc.remove_air(transfer_moles) as datum_gas_mixture;

                if (removed == null || !Dm.truthy(removed.total_moles())) //No venting from space 4head
                    return dot.Value;

                AirflowSeam.Vent(this, removed, environment, outward: false); // DEVIATION(DV34)
                air_contents.merge(removed);
                update_parents();
            }
        }
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:312
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (Dm.truthy(override_naming))
            return dot;
        name = Dm.text("\\proper [] [] []", get_area_name(this), name, id_tag);
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:337
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

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:348
    public override object power_change()
    {
        object dot = base.power_change();
        update_icon_nopipes();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:318
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        obj_item welder = tool;
        await base.welder_act(user, tool);
        if (!Dm.truthy(welder.tool_start_check(user, amount: 1)))
            return TRUE;
        to_chat(user, span_notice(I18n.T("You begin welding the vent...")));
        if (Dm.truthy(await welder.use_tool(this, user, 20, volume: 50)))
        {
            if (!Dm.truthy(welded))
            {
                user.visible_message(span_notice(I18n.T("[] welds the vent shut.", user)), span_notice(I18n.T("You weld the vent shut.")), span_hear(I18n.T("You hear welding.")));
                welded = TRUE;
            }
            else
            {
                user.visible_message(span_notice(I18n.T("[] unwelded the vent.", user)), span_notice(I18n.T("You unweld the vent.")), span_hear(I18n.T("You hear welding.")));
                welded = FALSE;
            }
            update_appearance(UPDATE_ICON);
            // pipe_vision_img = image(src, loc, dir = dir); SET_PLANE_EXPLICIT(pipe_vision_img, ABOVE_HUD_PLANE, src): the
            // ventcrawlers' pipe vision (ventcrawling, M10+)
            investigate_log(Dm.text("was [] by []", Dm.truthy(welded) ? "welded shut" : "unwelded", key_name(user)), INVESTIGATE_ATMOS);
            // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:334
            add_fingerprint(user);
        }
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:84  (the earlier of the type's two examine definitions: rules 7.5)
    public async DmTask<object> examine__d0(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You can link it with an air sensor using a multitool.")));

        if (Dm.truthy(fan_overclocked))
            Dm.append(ref dot, span_warning(I18n.T("It is currently overclocked causing it to take damage over time.")));

        if (Dm.num(get_integrity()) > 0)
            Dm.append(ref dot, span_notice(Dm.str(examine_condition)));
        else
            Dm.append(ref dot, span_warning(I18n.T("The fan is broken.")));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:343
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await examine__d0(user, __extra);
        if (Dm.truthy(welded))
            Dm.append(ref dot, I18n.T("It seems welded shut."));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:96  (package E5: the air sensor link)
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multi_tool = (obj_item_multitool)tool;
        if (multi_tool.buffer is obj_machinery_air_sensor sensor)
        {
            multi_tool.set_buffer(this);
            await sensor.multitool_act(user, multi_tool);
            return ITEM_INTERACT_SUCCESS;
        }

        balloon_alert(user, I18n.T("vent saved in buffer"));
        multi_tool.set_buffer(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:107
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        float time_to_repair = (10 * SECONDS) * (1 - Dm.num(get_integrity_percentage()));
        if (time_to_repair == 0)
            return FALSE;

        balloon_alert(user, I18n.T("repairing vent..."));
        if (Dm.truthy(await do_after(user, time_to_repair, this)))
        {
            balloon_alert(user, I18n.T("vent repaired"));
            await repair_damage(max_integrity);
        }
        else
            balloon_alert(user, I18n.T("interrupted!"));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:362
public partial class obj_machinery_atmospherics_components_unary_vent_pump_high_volume : obj_machinery_atmospherics_components_unary_vent_pump
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:366
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_gas_mixture air_contents = airs[1] as datum_gas_mixture;
        air_contents.volume = 1000;
        return dot;
    }
}

// M5 slice 7 (machinery integrity, D148): the fan's condition text, its break (it stops) and its repair.
// tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:1
public partial class obj_machinery_atmospherics_components_unary_vent_pump : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:68
    public override void on_update_integrity(object old_value = null, object new_value = null)
    {
        base.on_update_integrity(old_value, new_value);
        string condition_string;
        float sw__1 = Dm.num(get_integrity_percentage());
        if (sw__1 == 1)
            condition_string = I18n.T("perfect");
        else if (sw__1 >= 0.75f && sw__1 <= 0.99f)
            condition_string = I18n.T("good");
        else if (sw__1 >= 0.50f && sw__1 <= 0.74f)
            condition_string = I18n.T("okay");
        else if (sw__1 >= 0.25f && sw__1 <= 0.49f)
            condition_string = I18n.T("bad");
        else
            condition_string = I18n.T("terrible");
        examine_condition = I18n.T("The fan is in [] condition.", condition_string);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:121
    public override async DmTask atom_fix()
    {
        await set_is_operational(TRUE);
        update_appearance(UPDATE_ICON);
        await base.atom_fix();
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:126
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        await set_is_operational(FALSE);
        update_appearance();
        return await base.atom_break(damage_flag);
    }
}

// tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:352  (gaps X5: an alien clawing a welded vent open)
public partial class obj_machinery_atmospherics_components_unary_vent_pump : obj_machinery_atmospherics_components_unary
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/vent_pump.dm:352
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(welded) || !Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return null;
        user.visible_message(span_warning(I18n.T("[] furiously claws at []!", user, this)), span_notice(I18n.T("You manage to clear away the stuff blocking the vent.")), span_hear(I18n.T("You hear loud scraping noises.")));
        welded = FALSE;
        update_appearance(UPDATE_ICON);
        pipe_vision_img = Dm.image(this, loc, dir: dir);
        SET_PLANE_EXPLICIT(pipe_vision_img, ABOVE_HUD_PLANE, this);
        playsound(loc, Dm.resource("sound/items/weapons/bladeslice.ogg"), 100, TRUE);
        return null;
    }
}

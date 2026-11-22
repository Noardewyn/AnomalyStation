// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/other/meter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 4 (the engine's cooling loop): the gas flow meter, the whole file but the circuit component (/obj/item/circuit_component/atmos_meter
// stays inert with the other circuits; the meter's USB port is a deferred leaf). The emissive glow of its pressure bars is
// DEVIATION(DV12). The var block is generated. The turf meter (tg's `meter/turf`, meter.dm:237) keeps a turf in `target`, which tg
// types as a pipe and the generated field holds as one: its turf is `target_turf`, and `target_atom` reads either (GAP(types)).

// tg: code/modules/atmospherics/machinery/other/meter.dm:1
public partial class obj_machinery_meter : obj_machinery
{
    ///The turf of a turf meter (tg keeps it in `target`, meter.dm:241)
    // GAP(types): `target` is generated as the pipe tg declares (meter.dm:16)
    public turf target_turf;

    // What the meter reads: its pipe, or its turf (tg's `target`)
    private atom target_atom => target != null ? target : target_turf;

    // tg: code/modules/atmospherics/machinery/other/meter.dm:28
    public override object Destroy(float? force = null)
    {
        SSair.stop_processing_machine(this);
        if (target_atom != null)
        {
            UnregisterSignal(target_atom, COMSIG_QDELETING);
            target = null;
            target_turf = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:35
    [DmParams("mapload", "new_piping_layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_piping_layer = Dm.arg(__extra, 0);
        if (new_piping_layer != null)
            target_layer = Dm.num(new_piping_layer);

        SSair.start_processing_machine(this);

        if (target_atom == null)
            reattach_to_layer();
        // AddComponent(/datum/component/usb_port, typecacheof(list(/obj/item/circuit_component/atmos_meter), only_root_path = TRUE)): circuits
        Dm.Deferred("code/modules/atmospherics/machinery/other/meter.dm:43", "Initialize: AddComponent(/datum/component/usb_port): the circuit USB port (circuits)");
        return base.Initialize(mapload, Dm.forward(__extra, new_piping_layer));
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:50
    public virtual void reattach_to_layer()
    {
        obj_machinery_atmospherics_pipe candidate = null;
        foreach (obj_machinery_atmospherics_pipe pipe in Dm.each<obj_machinery_atmospherics_pipe>(loc))
        {
            if (Dm.eq(pipe.piping_layer, target_layer))
                candidate = pipe;
        }
        if (candidate != null)
        {
            target = candidate;
            RegisterSignal(target, COMSIG_QDELETING, nameof(drop_meter));
            setAttachLayer(candidate.piping_layer);
        }
    }

    ///Called when the parent pipe is removed
    // tg: code/modules/atmospherics/machinery/other/meter.dm:61
    [SignalHandler]
    public virtual void drop_meter(datum source = null, params object[] __extra)
    {
        Waiting.NoSleep(deconstruct(FALSE));
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:65
    public virtual void setAttachLayer(object new_layer = null)
    {
        target_layer = Dm.num(new_layer);
        PIPING_LAYER_DOUBLE_SHIFT(this, target_layer);
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:69
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        await base.on_set_is_operational(old_value);
        if (Dm.truthy(is_operational))
            SSair.start_processing_machine(this); //dont set icon_state here because it will be reset on next process() if it ever happens
        else
        {
            icon_state = "meter";
            SSair.stop_processing_machine(this);
        }
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:76
    public override async DmTask<object> return_air()
    {
        object pipe_air = target_atom != null ? await target_atom.return_air() : null;
        return Dm.truthy(pipe_air) ? pipe_air : await base.return_air();
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:79
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        datum_gas_mixture pipe_air = (target_atom != null ? await target_atom.return_air() : null) as datum_gas_mixture;
        string new_meter_value = "0";
        string new_button_value = "0";

        if (pipe_air == null)
        {
            icon_state = "meter0";
            if (!Dm.eq(meter_value, new_meter_value) || !Dm.eq(button_value, new_button_value))
            {
                meter_value = new_meter_value;
                button_value = new_button_value;
                update_appearance(UPDATE_OVERLAYS);
            }
            dot.Value = FALSE;
            return dot.Value;
        }

        float env_pressure = Dm.num(pipe_air.return_pressure());
        if (env_pressure <= 0.15f * ONE_ATMOSPHERE)
        {
            icon_state = "meter0";
            new_meter_value = "0";
            new_button_value = "0";
        }
        else if (env_pressure <= 1.8f * ONE_ATMOSPHERE)
        {
            float val = round(env_pressure / (ONE_ATMOSPHERE * 0.3f) + 0.5f);
            icon_state = Dm.text("meter1_[]", val);
            new_meter_value = Dm.text("1_[]", val);
            new_button_value = "1";
        }
        else if (env_pressure <= 30 * ONE_ATMOSPHERE)
        {
            float val = round(env_pressure / (ONE_ATMOSPHERE * 5) - 0.35f) + 1;
            icon_state = Dm.text("meter2_[]", val);
            new_meter_value = Dm.text("2_[]", val);
            new_button_value = "2";
        }
        else if (env_pressure <= 59 * ONE_ATMOSPHERE)
        {
            float val = round(env_pressure / (ONE_ATMOSPHERE * 5) - 6) + 1;
            icon_state = Dm.text("meter3_[]", val);
            new_meter_value = Dm.text("3_[]", val);
            new_button_value = "3";
        }
        else
        {
            icon_state = "meter4";
            new_meter_value = "4";
            new_button_value = "4";
        }

        float env_temperature = pipe_air.temperature;
        object new_greyscale = greyscale_colors;

        if (env_pressure == 0 || env_temperature == 0)
            new_greyscale = COLOR_GRAY;
        else
        {
            float sw__1 = env_temperature;
            if (Dm.in_to(sw__1, BODYTEMP_HEAT_WARNING_3, INFINITY))
                new_greyscale = COLOR_RED;
            else if (Dm.in_to(sw__1, BODYTEMP_HEAT_WARNING_2, BODYTEMP_HEAT_WARNING_3))
                new_greyscale = COLOR_ORANGE;
            else if (Dm.in_to(sw__1, BODYTEMP_HEAT_WARNING_1, BODYTEMP_HEAT_WARNING_2))
                new_greyscale = COLOR_YELLOW;
            else if (Dm.in_to(sw__1, BODYTEMP_COLD_WARNING_1, BODYTEMP_HEAT_WARNING_1))
                new_greyscale = COLOR_VIBRANT_LIME;
            else if (Dm.in_to(sw__1, BODYTEMP_COLD_WARNING_2, BODYTEMP_COLD_WARNING_1))
                new_greyscale = COLOR_CYAN;
            else if (Dm.in_to(sw__1, BODYTEMP_COLD_WARNING_3, BODYTEMP_COLD_WARNING_2))
                new_greyscale = COLOR_BLUE;
            else
                new_greyscale = COLOR_VIOLET;
        }

        if (!Dm.eq(new_greyscale, greyscale_colors)) //dont update if nothing has changed since last update
        {
            greyscale_colors = new_greyscale;
            await set_greyscale(greyscale_colors);
        }

        if (!Dm.eq(meter_value, new_meter_value) || !Dm.eq(button_value, new_button_value))
        {
            meter_value = new_meter_value;
            button_value = new_button_value;
            update_appearance(UPDATE_OVERLAYS);
        }
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:148
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (!Dm.truthy(is_operational))
            return dot;

        // . += emissive_appearance('icons/obj/pipes_n_cables/meter.dmi', "buttons[button_value]-emissive", src, alpha = src.alpha) and, with a pressure,
        // "pressure[meter_value]": DEVIATION(DV12), the glow on the emissive plane
        Dm.Deferred("code/modules/atmospherics/machinery/other/meter.dm:153", "update_overlays: emissive_appearance of the pressure bars and the buttons (DEVIATION DV12)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:157
    public virtual object status()
    {
        object dot = null;
        if (target_atom != null)
        {
            datum_gas_mixture pipe_air = Waiting.NoSleep(target_atom.return_air()) as datum_gas_mixture;
            if (pipe_air != null)
                dot = I18n.T("The pressure gauge reads [] kPa; [] K ([]&deg;C).", round(Dm.num(pipe_air.return_pressure()), 0.01f), round(pipe_air.temperature, 0.01f), round(pipe_air.temperature - T0C, 0.01f));
            else
                dot = I18n.T("The sensor error light is blinking.");
        }
        else
            dot = I18n.T("The connect error light is blinking.");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:167
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, status());
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:171
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        to_chat(user, span_notice(I18n.T("You begin to unfasten \\the []...", this)));
        if (Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
        {
            user.visible_message(
                I18n.T("[] unfastens \\the [].", user, this),
                span_notice(I18n.T("You unfasten \\the [].", this)),
                span_hear(I18n.T("You hear ratchet.")));
            dm_unwrencher = user; // DEVIATION(DV356)
            await deconstruct();
        }
        return TRUE;
    }

    // DEVIATION(DV356): port-only, who unfastened it (on_deconstruction drops the meter at their feet when it was in a wall)
    [NotHashed]
    public mob dm_unwrencher;

    // tg: code/modules/atmospherics/machinery/other/meter.dm:182
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        obj_item_pipe_meter meter_object = Dm.@new<obj_item_pipe_meter>(dm_out_of_wall(get_turf(this), dm_unwrencher));
        transfer_fingerprints_to(meter_object);
        return null;
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:186
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return null;
        else
            to_chat(user, status());
        return null;
    }

    // tg: code/modules/atmospherics/machinery/other/meter.dm:192
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct();
        return null;
    }
}

// TURF METER - REPORTS A TILE'S AIR CONTENTS
// why are you yelling?
// tg: code/modules/atmospherics/machinery/other/meter.dm:237
public partial class obj_machinery_meter_turf : obj_machinery_meter
{
    // tg: code/modules/atmospherics/machinery/other/meter.dm:240
    public override void reattach_to_layer()
    {
        target_turf = loc as turf;
    }
}

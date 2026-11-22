// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/turbine/turbine_computer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E11: the whole file. The gas turbine control computer: the map's mapping_id link to its core rotor, the
// multitool link from the rotor in a multitool's buffer, and tg's TurbineComputer window (the power switch, the intake regulator,
// the integrity, speed, temperatures and power). The var block is generated.

// tg: code/modules/power/turbine/turbine_computer.dm:1
public partial class obj_machinery_computer_turbine_computer : obj_machinery_computer
{
    // tg: code/modules/power/turbine/turbine_computer.dm:13
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        if (!Dm.truthy(mapping_id))
            return;
        foreach (obj_machinery_power_turbine_core_rotor main in Dm.each_cast<obj_machinery_power_turbine_core_rotor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_turbine_core_rotor))))
        {
            if (!Dm.eq(main.mapping_id, mapping_id))
                continue;
            register_machine(main);
            break;
        }
    }

    // tg: code/modules/power/turbine/turbine_computer.dm:24
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multitool = (obj_item_multitool)tool;
        object dot = ITEM_INTERACT_FAILURE;
        if (!(multitool.buffer is obj_machinery_power_turbine_core_rotor))
        {
            to_chat(user, span_notice(I18n.T("Wrong machine type in [] buffer...", multitool)));
            return dot;
        }
        if (Dm.truthy(turbine_core))
            to_chat(user, span_notice(I18n.T("Changing [] bluespace network...", this)));
        if (!Dm.truthy(await do_after(user, 0.2f * SECONDS, this)))
            return dot;

        playsound(get_turf(user), Dm.resource("sound/machines/click.ogg"), 10, TRUE);
        register_machine(multitool.buffer as obj_machinery_power_turbine_core_rotor);
        to_chat(user, span_notice(I18n.T("You link [] to the console in []'s buffer.", this, multitool)));
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Links the rotor with this computer
     * Arguments
     *
     * * obj/machinery/power/turbine/core_rotor/machine - the machine to link
     */
    // tg: code/modules/power/turbine/turbine_computer.dm:45
    public virtual void register_machine(obj_machinery_power_turbine_core_rotor machine = null)
    {
        // PRIVATE_PROC(TRUE)

        turbine_core = WEAKREF(machine) as datum_weakref;
    }

    // tg: code/modules/power/turbine/turbine_computer.dm:50
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "TurbineComputer", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/power/turbine/turbine_computer.dm:57
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();

        //do we have the main rotor with all parts connected
        obj_machinery_power_turbine_core_rotor main_control = turbine_core?.resolve() as obj_machinery_power_turbine_core_rotor;
        if (QDELETED(main_control) || !Dm.truthy(main_control.all_parts_connected))
        {
            dot["connected"] = FALSE;
            return dot;
        }
        else
            dot["connected"] = TRUE;

        //operation status
        dot["active"] = main_control.active;
        dot["rpm"] = main_control.rpm;
        dot["power"] = energy_to_power(main_control.produced_energy);
        dot["integrity"] = main_control.get_turbine_integrity();

        //running parameters
        dot["max_rpm"] = main_control.max_allowed_rpm;
        dot["max_temperature"] = main_control.max_allowed_temperature;
        float? temp = main_control.compressor.input_turf?.air.temperature;
        dot["temp"] = Dm.truthy(temp) ? temp : 0;
        dot["regulator"] = main_control.compressor.intake_regulator;
        return dot;
    }

    // tg: code/modules/power/turbine/turbine_computer.dm:80
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "toggle_power":
            {
                obj_machinery_power_turbine_core_rotor main_control = turbine_core?.resolve() as obj_machinery_power_turbine_core_rotor;
                if (main_control == null)
                    return FALSE;

                if (!Dm.truthy(main_control.active)) //turning on the machine requires all part to be linked
                {
                    if (!Dm.truthy(await main_control.activate_parts(ui.user, check_only: TRUE)))
                        return FALSE;
                }
                else if (main_control.rpm > 1000) //turning off requires rpm to be less than 1000
                    return FALSE;

                main_control.toggle_power();
                main_control.rpm = 0;
                main_control.produced_energy = 0;
                return TRUE;
            }

            case "regulate":
            {
                object intake_size = @params["regulate"];
                if (intake_size == null)
                    return FALSE;

                intake_size = text2num(intake_size);
                if (intake_size == null)
                    return FALSE;

                obj_machinery_power_turbine_core_rotor main_control = turbine_core?.resolve() as obj_machinery_power_turbine_core_rotor;
                if (main_control == null)
                    return FALSE;

                if (QDELETED(main_control.compressor))
                    return FALSE;

                main_control.compressor.intake_regulator = clamp(Dm.num(intake_size), 0.01f, 1);
                return TRUE;
            }
        }
        return dot;
    }
}

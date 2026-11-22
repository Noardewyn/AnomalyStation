// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/rechargestation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the recharging station, whole. The var block is generated (D111) but the callback var charge_cell,
// declared here (the generator skips it: the machinery's proc of the same name is charge_cell__proc, rules B.2).

// tg: code/game/machinery/rechargestation.dm:1
public partial class obj_machinery_recharge_station : obj_machinery
{
    ///Callback for borgs & modsuits to provide their cell to us for charging
    // tg: code/game/machinery/rechargestation.dm:17
    public datum_callback charge_cell;

    // The type's overrides of inherited vars (the Hand mode, types-converter.md 3.2: the class declares charge_cell, so the generated
    // part holds only the vars it does not declare)
    // tg: code/game/machinery/rechargestation.dm:2
    public obj_machinery_recharge_station()
    {
        name = "recharging station";
        desc = "This device recharges energy-dependent lifeforms, like cyborgs, ethereals, and MODsuit users.";
        icon = Dm.resource("icons/obj/machines/borg_charger.dmi");
        icon_state = "borgcharger0";
        base_icon_state = "borgcharger";
        active_power_usage = 100;
        density = 0;
        state_open = 1;
        circuit = typeof(obj_item_circuitboard_machine_cyborgrecharger);
        processing_flags = 0;
    }

    // tg: code/game/machinery/rechargestation.dm:9
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        req_access = Dm.list("robotics");
        occupant_typecache = Dm.list(typeof(mob_living_silicon_robot), typeof(mob_living_carbon_human), typeof(mob_living_circuit_drone));
    }

    // tg: code/game/machinery/rechargestation.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        materials = Dm.@new<datum_remote_materials>(
            this,
            mapload,
            Dm.named("mat_container_flags", MATCONTAINER_NO_INSERT));
        charge_cell = Dm.@new<datum_callback>(this, nameof(charge_target_cell));

        update_appearance();
        if (Dm.truthy(is_operational))
            begin_processing();

        if (!Dm.truthy(mapload))
            return dot;

        area my_area = get_area(this) as area;
        if (!Dm.@in(my_area.type, GLOB.the_station_areas))
            return dot;

        object area_name = get_area_name(this, format_text: TRUE);
        if (Dm.@in(area_name, GLOB.roundstart_station_borgcharger_areas))
            return dot;
        Dm.append(ref GLOB.roundstart_station_borgcharger_areas, area_name);
        return dot;
    }

    // tg: code/game/machinery/rechargestation.dm:48
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref materials);
        charge_cell = null;
        return base.Destroy(force);
    }

    /**
     * Mobs & borgs invoke this through a callback to recharge their cells
     * Arguments
     *
     * * obj/item/stock_parts/power_store/cell/target - the cell to charge, optional if provided else will draw power used directly
     * * seconds_per_tick - supplied from process()
     */
    // tg: code/game/machinery/rechargestation.dm:60
    public virtual async DmTask<object> charge_target_cell(obj_item_stock_parts_power_store target = null, object seconds_per_tick = null)
    {
        // PRIVATE_PROC(TRUE)

        //charge the cell, account for heat loss from work done
        object charge_given = await charge_cell__proc(Dm.num(recharge_speed) * Dm.num(seconds_per_tick), target, grid_only: TRUE);
        if (Dm.truthy(charge_given))
            await use_energy((Dm.num(charge_given) + Dm.num(active_power_usage)) * 0.01f);

        return charge_given;
    }

    // tg: code/game/machinery/rechargestation.dm:70
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        recharge_speed = 0f;
        repairs = 0f;
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            recharge_speed = Dm.num(recharge_speed) + 5e-3f * Dm.num(capacitor.tier);
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            repairs = Dm.num(repairs) + Dm.num(servo.tier) - 1;
        foreach (obj_item_stock_parts_power_store cell in Dm.each<obj_item_stock_parts_power_store>(component_parts))
            recharge_speed = Dm.num(recharge_speed) * Dm.num(cell.maxcharge);
        return dot;
    }

    // tg: code/game/machinery/rechargestation.dm:81
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (in_range(user, this) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Recharging: <b>[]</b>.", display_power(recharge_speed, convert: FALSE))));
            if (materials.silo != null)
                Dm.append(ref dot, span_notice(I18n.T("The ore silo link indicator is lit, and cyborg restocking can be toggled by <b>Right-Clicking</b> [].", this)));
            if (Dm.truthy(repairs))
                Dm.append(ref dot, span_notice(I18n.T("[] has been upgraded to support automatic repairs.", this)));
        }
        return dot;
    }

    // tg: code/game/machinery/rechargestation.dm:90
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        if (Dm.truthy(old_value)) //Turned off
            end_processing();
        else //Turned on
            begin_processing();
    }

    // tg: code/game/machinery/rechargestation.dm:96
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user))
            return null;
        await open_machine();
        return null;
    }

    // tg: code/game/machinery/rechargestation.dm:101
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
        {
            if (occupant != null && !Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
                await occupant.emp_act(severity);
            if (!Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
                await open_machine();
        }
        return dot;
    }

    // tg: code/game/machinery/rechargestation.dm:109
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return Dm.truthy(state_open) ? default_deconstruction_screwdriver(user, tool) : NONE;
    }

    // tg: code/game/machinery/rechargestation.dm:112
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, close_after_pry: FALSE, open_density: FALSE, closed_density: TRUE, deconstruct_on_fail: TRUE);
    }

    // tg: code/game/machinery/rechargestation.dm:115
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        await toggle_restock(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/rechargestation.dm:119
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await toggle_restock(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/rechargestation.dm:123
    public virtual async DmTask<object> toggle_restock(mob user = null)
    {
        if (Dm.truthy(sendmats))
        {
            sendmats = FALSE;
            await say(I18n.T("Restocking from ore silo: disabled."));
            return null;
        }
        if (Dm.truthy(state_open) || occupant == null)
            return null;
        if (!iscyborg(occupant))
            return null;
        if (materials.silo == null)
        {
            await say(I18n.T("Error: ore silo connection offline."));
            return null;
        }
        if (materials.on_hold())
        {
            await say(I18n.T("Error: ore silo access denied."));
            return FALSE;
        }
        sendmats = TRUE;
        await say(I18n.T("Restocking from ore silo: enabled."));
        return null;
    }

    // tg: code/game/machinery/rechargestation.dm:141
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        await toggle_open();
        return TRUE;
    }

    // tg: code/game/machinery/rechargestation.dm:145
    public virtual async DmTask toggle_open()
    {
        if (Dm.truthy(state_open))
        {
            await close_machine(density_to_set: TRUE);
            await toggle_restock(); //defaults to enabled
        }
        else
            await open_machine();
    }

    // tg: code/game/machinery/rechargestation.dm:152
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        drop ??= TRUE;
        density_to_set ??= FALSE;
        object dot = await base.open_machine(drop, density_to_set);
        sendmats = FALSE;
        update_use_power(IDLE_POWER_USE);
        return dot;
    }

    // tg: code/game/machinery/rechargestation.dm:157
    public override async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        density_to_set ??= TRUE;
        object dot = await base.close_machine(target, density_to_set);
        if (occupant != null)
        {
            update_use_power(ACTIVE_POWER_USE); //It always tries to charge, even if it can't.
            add_fingerprint(occupant as mob);
        }
        return dot;
    }

    // tg: code/game/machinery/rechargestation.dm:163
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            icon_state = "borgdecon2";
            return await base.update_icon_state(__extra);
        }
        if (!Dm.truthy(is_operational))
        {
            icon_state = Dm.text("[]-u[]", base_icon_state, Dm.truthy(state_open) ? 0 : 1);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(state_open) ? 0 : (occupant != null ? 1 : 2));
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/rechargestation.dm:173
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (QDELETED(occupant) || !Dm.truthy(is_operational))
            return null;

        if (Dm.truthy(occupant._listen_lookup?[COMSIG_PROCESS_BORGCHARGER_OCCUPANT]))
            occupant._SendSignal(COMSIG_PROCESS_BORGCHARGER_OCCUPANT, Dm.list(occupant, charge_cell, seconds_per_tick, repairs, sendmats));
        return null;
    }
}

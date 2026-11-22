// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mech_bay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/mech_bay.dm:1
public partial class obj_machinery_mech_bay_recharge_port : obj_machinery
{
    // tg: code/modules/vehicles/mecha/mech_bay.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        recharging_turf = get_step(loc, Dm.num(dir)) as turf;

        if (!Dm.truthy(mapload))
            return dot;

        area my_area = get_area(this) as area;
        if (!Dm.@in(my_area.type, GLOB.the_station_areas))
            return dot;

        object area_name = get_area_name(this, format_text: TRUE);
        if (Dm.@in(area_name, GLOB.roundstart_station_mechcharger_areas))
            return dot;
        Dm.append(ref GLOB.roundstart_station_mechcharger_areas, area_name);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:34
    public override object Destroy(float? force = null)
    {
        if (recharge_console?.recharge_port == this)
            recharge_console.recharge_port = null;
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:39
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        recharging_turf = get_step(loc, Dm.num(dir)) as turf;
    }

    // tg: code/modules/shuttle/mobile_port/shuttle_move_callbacks.dm:257
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);
        recharging_turf = get_step(loc, Dm.num(dir)) as turf;
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:43
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float total_rating = 0;
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            total_rating += Dm.num(capacitor.tier);
        recharge_power = total_rating * 0.0125f * STANDARD_CELL_RATE;
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:50
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Recharge power <b>[]</b>.", siunit(recharge_power, "W", 1))));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:55
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) || recharge_console == null)
            return null;
        Waiting.Detach(recharge(Dm.num(seconds_per_tick)));
        return null;
    }

    private async DmTask recharge(float seconds_per_tick)
    {
        obj_vehicle_sealed_mecha recharging_mech = recharging_mech_ref?.resolve() as obj_vehicle_sealed_mecha;
        if (recharging_mech == null)
        {
            recharging_mech = Dm.locate<obj_vehicle_sealed_mecha>(recharging_turf?.contents);
            if (recharging_mech != null)
            {
                recharging_mech_ref = WEAKREF(recharging_mech) as datum_weakref;
                recharge_console.update_appearance();
            }
        }
        if (recharging_mech?.cell == null)
            return;
        if (Dm.truthy(recharging_mech.cell.used_charge()))
        {
            //charge cell, account for heat loss given from work done
            object charge_given = await charge_cell__proc(recharge_power * seconds_per_tick, recharging_mech.cell, grid_only: TRUE);
            if (Dm.truthy(charge_given))
                await use_energy((Dm.num(charge_given) + Dm.num(active_power_usage)) * 0.01f);
        }
        else
            recharge_console.update_appearance();
        if (recharging_mech.loc != recharging_turf)
        {
            recharging_mech_ref = null;
            recharge_console.update_appearance();
        }
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:76
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = (Dm.truthy(panel_open) ? (object)Dm.text("[]-o", base_icon_state) : base_icon_state) as string;
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:80
    public override DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null) => DmTask<object>.FromResult(default_deconstruction_screwdriver(user, tool));

    // tg: code/modules/vehicles/mecha/mech_bay.dm:83
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null) => await default_deconstruction_crowbar(user, tool);

    // tg: code/modules/vehicles/mecha/mech_bay.dm:86
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await default_change_direction_wrench(user, tool);
        recharging_turf = get_step(loc, Dm.num(dir)) as turf;
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/mech_bay.dm:91
public partial class obj_machinery_computer_mech_bay_power_console : obj_machinery_computer
{
    // tg: code/modules/vehicles/mecha/mech_bay.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        reconnect();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:106
    public override object Destroy(float? force = null)
    {
        if (recharge_port?.recharge_console == this)
            recharge_port.recharge_console = null;
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:111
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "MechBayPowerConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:117
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "reconnect"))
        {
            reconnect();
            dot = TRUE;
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:127
    public override DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        if (QDELETED(recharge_port))
            return DmTask<object>.FromResult(data);

        DmList port = Dm.list(Dm.kv("mech", null));
        data["recharge_port"] = port;
        obj_vehicle_sealed_mecha recharging_mech = recharge_port.recharging_mech_ref?.resolve() as obj_vehicle_sealed_mecha;

        if (recharging_mech == null)
            return DmTask<object>.FromResult(data);
        DmList mech = Dm.list(Dm.kv("health", recharging_mech.get_integrity()), Dm.kv("maxhealth", recharging_mech.max_integrity), Dm.kv("cell", null), Dm.kv("name", recharging_mech.name));
        port["mech"] = mech;

        if (QDELETED(recharging_mech.cell))
            return DmTask<object>.FromResult(data);
        mech["cell"] = Dm.list(
            Dm.kv("charge", recharging_mech.cell.charge),
            Dm.kv("maxcharge", recharging_mech.cell.maxcharge));
        return DmTask<object>.FromResult(data);
    }

    ///Checks for nearby recharge ports to link to
    // tg: code/modules/vehicles/mecha/mech_bay.dm:148
    public virtual void reconnect()
    {
        if (recharge_port != null)
            return;
        recharge_port = Dm.locate<obj_machinery_mech_bay_recharge_port>(range(1, this));
        if (recharge_port == null)
        {
            foreach (object direction in Dm.each<object>(GLOB.cardinals))
            {
                turf target = get_step(this, Dm.num(direction)) as turf;
                target = get_step(target, Dm.num(direction)) as turf;
                recharge_port = Dm.locate<obj_machinery_mech_bay_recharge_port>(target?.contents);
                if (recharge_port != null)
                    break;
            }
        }
        if (recharge_port == null)
            return;
        if (recharge_port.recharge_console == null)
            recharge_port.recharge_console = this;
        else
            recharge_port = null;
    }

    // tg: code/modules/vehicles/mecha/mech_bay.dm:166
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return dot;
        obj_vehicle_sealed_mecha recharging_mech = recharge_port?.recharging_mech_ref?.resolve() as obj_vehicle_sealed_mecha;

        if (recharging_mech?.cell == null)
            return dot;
        if (Dm.num(recharging_mech.cell.charge) >= Dm.num(recharging_mech.cell.maxcharge))
            return dot;
        Dm.append(ref dot, "recharge_comp_on");
        return dot;
    }
}

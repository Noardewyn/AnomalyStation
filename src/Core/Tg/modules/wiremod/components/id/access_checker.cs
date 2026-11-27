// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/id/access_checker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, with its CircuitAccessChecker window. The var block is generated.

// tg: code/modules/wiremod/components/id/access_checker.dm:1
public partial class obj_item_circuit_component_compare_access : obj_item_circuit_component_compare
{
    // tg: code/modules/wiremod/components/id/access_checker.dm:19
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice("When \"Check Any\" is true, returns true if \"Access To Check\" contains ANY value in \"Required Access\".", "orange", "info"));
        Dm.append(dot, create_ui_notice("When \"Check Any\" is false, returns true only if \"Access To Check\" contains ALL values in \"Required Access\".", "orange", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:24
    public override async DmTask populate_custom_ports()
    {
        subject_accesses = add_input_port("Access To Check", PORT_TYPE_LIST(PORT_TYPE_STRING)) as datum_port_input;
        required_accesses = add_input_port("Required Access", PORT_TYPE_LIST(PORT_TYPE_STRING)) as datum_port_input;
        check_any = add_input_port("Check Any", PORT_TYPE_BOOLEAN) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:29
    public override void save_data_to_list(DmList component_data = null)
    {
        base.save_data_to_list(component_data);
        component_data["input_ports_stored_data"] = Dm.list(Dm.kv(required_accesses.name, Dm.list(Dm.kv("stored_data", required_accesses.value))));
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:33
    public override async DmTask<object> add_to(obj_item_integrated_circuit added_to = null)
    {
        object dot = await base.add_to(added_to);
        RegisterSignal(added_to, COMSIG_CIRCUIT_POST_LOAD, nameof(on_post_load));
        return dot;
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:37
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        UnregisterSignal(removed_from, COMSIG_CIRCUIT_POST_LOAD);
        base.removed_from(removed_from);
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:41
    public virtual void on_post_load(datum source = null)
    {
        regenerate_access();
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:44
    public virtual void regenerate_access()
    {
        object check_any_value = check_any.value;
        object required_accesses_list = required_accesses.value;
        if (!islist(required_accesses_list))
            return;
        if (Dm.truthy(check_any_value))
        {
            LAZYCLEARLIST(req_access);
            req_one_access = ((DmList)required_accesses_list).Copy();
        }
        else
        {
            LAZYCLEARLIST(req_one_access);
            req_access = ((DmList)required_accesses_list).Copy();
        }
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:56
    public override object do_comparisons()
    {
        return check_access_list(subject_accesses.value as DmList);
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:59
    public override async DmTask ui_perform_action(mob user = null, object action = null)
    {
        if (Dm.truthy(LAZYLEN(required_accesses.connected_ports)))
        {
            balloon_alert(user, I18n.T("disconnect port before manually configuring!"));
            return;
        }
        await interact(user);
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:65
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "CircuitAccessChecker", display_name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:71
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList regions = Dm.list();
        DmList tgui_region_data = SSid_access.all_region_access_tgui;
        foreach (object region in Dm.each(SSid_access.station_regions))
            Dm.append(regions, tgui_region_data[region]);
        if (Dm.truthy(parent?.admin_only))
        {
            Dm.append(regions, tgui_region_data[REGION_CENTCOM]);
            Dm.append(regions, tgui_region_data[REGION_ALL_GLOBAL]);
        }
        data["regions"] = regions;
        return data;
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:84
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["accesses"] = required_accesses.value is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.list();
        data["oneAccess"] = check_any.value;
        return data;
    }

    // tg: code/modules/wiremod/components/id/access_checker.dm:90
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "clear_all"))
        {
            required_accesses.set_value(Dm.list());
            check_any.set_value(0);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "grant_all"))
        {
            required_accesses.set_value(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "one_access"))
        {
            check_any.set_value(Dm.not(check_any.value));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "set"))
        {
            DmList required_accesses_list = required_accesses.value as DmList;
            DmList new_accesses_value = LAZYCOPY(required_accesses_list);
            object access = @params["access"];
            if (!Dm.@in(access, new_accesses_value))
                Dm.append(ref new_accesses_value, access);
            else
                Dm.remove(ref new_accesses_value, access);
            required_accesses.set_value(new_accesses_value);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "grant_region"))
        {
            DmList required_accesses_list = required_accesses.value as DmList;
            DmList required_accesses_value = LAZYCOPY(required_accesses_list);
            object region = @params["region"];
            if (region == null)
                return dot;
            required_accesses.set_value(Dm.bitor(required_accesses_value, SSid_access.get_region_access_list(Dm.list(region))));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "deny_region"))
        {
            DmList required_accesses_list = required_accesses.value as DmList;
            DmList required_accesses_value = LAZYCOPY(required_accesses_list);
            object region = @params["region"];
            if (region == null)
                return dot;
            required_accesses.set_value(Dm.sub(required_accesses_value, SSid_access.get_region_access_list(Dm.list(region))));
            dot = TRUE;
        }
        if (Dm.truthy(dot))
        {
            regenerate_access();
            SStgui.update_uis(parent);
        }
        return dot;
    }
}

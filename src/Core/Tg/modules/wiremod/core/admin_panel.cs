// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/admin_panel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the admins' circuit panel (the console's circuits, D275): every circuit with its place
// and creator, the circuit sounds' switch, duplicating a circuit, opening its editor and saving it. Following a circuit, its
// variable editor and the player panel are the admin tools the port does not have (deferred, as the other admin windows' are).
// The var block is generated (D111).

// tg: code/modules/wiremod/core/admin_panel.dm:2
public partial class client : datum
{
    // ADMIN_VERB(view_all_circuits, R_ADMIN, "View All Circuits", "List all circuits in the game.", ADMIN_CATEGORY_GAME): the verb's
    // body, which the admin console's circuits runs (D275; the verb's rights are the console's)
    // tg: code/modules/wiremod/core/admin_panel.dm:2
    public async DmTask view_all_circuits()
    {
        datum_circuit_admin_panel circuit_admin_panel = view_all_circuits__circuit_admin_panel ??= Dm.@new<datum_circuit_admin_panel>();
        await circuit_admin_panel.ui_interact(mob);
    }

    // tg: code/modules/wiremod/core/admin_panel.dm:3  var/static/datum/circuit_admin_panel/circuit_admin_panel = new (a proc-level
    // static of the verb; the use's ??= keeps the creation lazy, rules 10.6)
    private static ref datum_circuit_admin_panel view_all_circuits__circuit_admin_panel => ref TgState.Current.client__view_all_circuits__circuit_admin_panel;
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/core/admin_panel.dm:3  (var/static/datum/circuit_admin_panel/circuit_admin_panel in ADMIN_VERB(view_all_circuits))
    internal datum_circuit_admin_panel client__view_all_circuits__circuit_admin_panel;
}

// tg: code/modules/wiremod/core/admin_panel.dm:6
public partial class datum_circuit_admin_panel : datum
{
    // tg: code/modules/wiremod/core/admin_panel.dm:8
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList circuits = Dm.list();
        data["circuits"] = circuits;

        foreach (obj_item_integrated_circuit circuit in Dm.each_cast<obj_item_integrated_circuit>(GLOB.integrated_circuits))
        {
            datum_mind inserter = circuit.inserter_mind?.resolve() as datum_mind;

            Dm.append(circuits, Dm.list(Dm.list(
                Dm.kv("ref", REF(circuit)),
                Dm.kv("name", Dm.text("[] in []", circuit.name, loc_name(circuit))),
                Dm.kv("creator", circuit.get_creator()),
                Dm.kv("has_inserter", Dm.num(inserter != null)))));
        }

        return data;
    }

    // tg: code/modules/wiremod/core/admin_panel.dm:24
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "disable_circuit_sounds":
                await config.Set(typeof(datum_config_entry_flag_disallow_circuit_sounds), Dm.num(!Dm.truthy(config.Get(typeof(datum_config_entry_flag_disallow_circuit_sounds)))));
                message_admins(Dm.text("[] has toggled all circuit sounds [].", key_name_admin(usr), Dm.truthy(config.Get(typeof(datum_config_entry_flag_disallow_circuit_sounds))) ? "off" : "on"));
                log_admin(Dm.text("[] has toggled all circuit sounds [].", key_name(usr), Dm.truthy(config.Get(typeof(datum_config_entry_flag_disallow_circuit_sounds))) ? "off" : "on"));
                return TRUE;
        }

        if (!istext(@params["circuit"]))
            return FALSE;

        obj_item_integrated_circuit circuit = Dm.locate_ref(@params["circuit"]) as obj_item_integrated_circuit;
        if (!istype(circuit, typeof(obj_item_integrated_circuit)))
        {
            to_chat(usr, span_warning(I18n.T("That circuit no longer exists.")));
            return FALSE;
        }

        switch (action)
        {
            case "duplicate_circuit":
            {
                // ADAPTED: BYOND's alert() (Dm.alert answers nothing in the port) asks through tg's tgui_alert window
                if (!Dm.eq(await tgui_alert(usr, "This will spawn the new circuit at where you are, are you sure?", "Confirm", Dm.list("Yes", "No")), "Yes"))
                    return FALSE;

                DmList errors = Dm.list();

                obj_item_integrated_circuit_loaded new_circuit = await Dm.new_async<obj_item_integrated_circuit_loaded>(usr.drop_location());
                await new_circuit.load_circuit_data(circuit.convert_to_json(), errors);

                if (Dm.truthy(length(errors)))
                {
                    to_chat(usr, span_warning(I18n.T("Somehow, duplicating the circuit failed:")));
                    foreach (object error in Dm.each(errors))
                        to_chat(usr, span_warning(Dm.str(error)));
                }
                break;
            }
            case "follow_circuit":
                // usr.client?.admin_follow(circuit): the admins' follow (an observer orbit; the admin tools, D275)
                Dm.Deferred("code/modules/wiremod/core/admin_panel.dm:58", "ui_act follow_circuit: the admins' follow (admin_follow, D275)");
                break;
            case "save_circuit":
                circuit.attempt_save_to(usr.client);
                break;
            case "vv_circuit":
                // usr.client?.debug_variables(circuit): the variable editor is not ported (D275)
                Dm.Deferred("code/modules/wiremod/core/admin_panel.dm:62", "ui_act vv_circuit: the variable editor (debug_variables, D275)");
                break;
            case "open_circuit":
                await circuit.ui_interact(usr);
                break;
            case "open_player_panel":
                // SSadmin_verbs.dynamic_invoke_verb(usr, /datum/admin_verb/show_player_panel, inserter?.current): the player panel (D275)
                Dm.Deferred("code/modules/wiremod/core/admin_panel.dm:67", "ui_act open_player_panel: the player panel (show_player_panel, D275)");
                break;
        }

        return TRUE;
    }

    // tg: code/modules/wiremod/core/admin_panel.dm:73
    public override object ui_state(mob user = null) => ADMIN_STATE(R_ADMIN);

    // tg: code/modules/wiremod/core/admin_panel.dm:76
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "CircuitAdminPanel");
            await ui.open();
        }
        return null;
    }
}

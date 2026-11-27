// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/admin/signal_handler/signal_list.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the signals the admins' Signal Handler component offers, with its CircuitSignalHandler window. The var block
// is generated.

public static partial class GlobalProcs
{
    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:1
    [ShouldBePure]
    public static DmList circuit_signal_response(object name = null, object bitflag = null)
    {
        return Dm.list(
            Dm.kv("name", name),
            Dm.kv("bitflag", bitflag),
            Dm.kv("is_response", TRUE)
        );
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:9
    [ShouldBePure]
    public static DmList circuit_signal_param(object name = null, object type = null)
    {
        return Dm.list(
            Dm.kv("name", name),
            Dm.kv("type", type),
            Dm.kv("is_response", FALSE)
        );
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:19
    public static DmList generate_circuit_signal_list()
    {
        DmList cancel_attack = circuit_signal_response("Cancel Attack", COMPONENT_CANCEL_ATTACK_CHAIN);
        DmList secondary_cancel_attack = circuit_signal_response("Cancel Attack", COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN);
        DmList secondary_continue_attack = circuit_signal_response("Continue Attack", COMPONENT_SECONDARY_CONTINUE_ATTACK_CHAIN);
        DmList target = circuit_signal_param("Target", PORT_TYPE_ATOM);
        DmList user = circuit_signal_param("User", PORT_TYPE_ATOM);
        DmList item = circuit_signal_param("Item", PORT_TYPE_ATOM);
        DmList entity = circuit_signal_param("Entity", PORT_TYPE_ATOM);

        return Dm.list(
            Dm.kv(COMSIG_QDELETING, Dm.list()),
            Dm.kv(COMSIG_ATOM_ATTACKBY, Dm.list(circuit_signal_response("Cancel Attack", COMPONENT_NO_AFTERATTACK), item, user)),
            Dm.kv(COMSIG_ATOM_ATTACKBY_SECONDARY, Dm.list(circuit_signal_response("Cancel Attack", COMPONENT_NO_AFTERATTACK), item, user)),
            Dm.kv(COMSIG_ATOM_EXAMINE, Dm.list(user)),

            Dm.kv(COMSIG_ATOM_ATTACK_HAND, Dm.list(cancel_attack, user)),
            Dm.kv(COMSIG_ATOM_ATTACK_GHOST, Dm.list(cancel_attack, user)),
            Dm.kv(COMSIG_ATOM_BUMPED, Dm.list(entity)),
            Dm.kv(COMSIG_ATOM_HITBY, Dm.list(entity)),

            Dm.kv(COMSIG_ITEM_ATTACK, Dm.list(cancel_attack, target, user)),
            Dm.kv(COMSIG_ITEM_PRE_ATTACK, Dm.list(cancel_attack, target, user)),
            Dm.kv(COMSIG_ITEM_AFTERATTACK, Dm.list(cancel_attack, target, user)),
            Dm.kv(COMSIG_ITEM_ATTACK_SECONDARY, Dm.list(secondary_cancel_attack, secondary_continue_attack, target, user)),
            Dm.kv(COMSIG_ITEM_PRE_ATTACK_SECONDARY, Dm.list(secondary_cancel_attack, secondary_continue_attack, target, user)),
            Dm.kv(COMSIG_ITEM_ATTACK_SELF, Dm.list(cancel_attack, user)),
            Dm.kv(COMSIG_ITEM_ATTACK_SELF_SECONDARY, Dm.list(cancel_attack, user))
        );
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:47
    public static DmList generate_global_circuit_signal_list()
    {
        DmList client_ent = circuit_signal_param("Client", PORT_TYPE_ATOM);
        DmList entity = circuit_signal_param("Entity", PORT_TYPE_ATOM);

        return Dm.list(
            Dm.kv(COMSIG_GLOB_MOB_DEATH, Dm.list(entity, circuit_signal_param("Gibbed", PORT_TYPE_NUMBER))),
            Dm.kv(COMSIG_GLOB_MOB_CREATED, Dm.list(entity)),
            Dm.kv(COMSIG_GLOB_CLIENT_CONNECT, Dm.list(client_ent)),

            Dm.kv(COMSIG_GLOB_JOB_AFTER_LATEJOIN_SPAWN, Dm.list(circuit_signal_param("Job Subsystem", PORT_TYPE_ATOM), entity, client_ent)),
            Dm.kv(COMSIG_GLOB_CREWMEMBER_JOINED, Dm.list(circuit_signal_param("Crewmember", PORT_TYPE_ATOM), circuit_signal_param("Rank", PORT_TYPE_STRING)))
        );
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:17  GLOBAL_LIST_INIT(integrated_circuit_signal_ids, generate_circuit_signal_list())
    public DmList integrated_circuit_signal_ids;

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:17  (GLOBAL_MANAGED(integrated_circuit_signal_ids, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalintegrated_circuit_signal_ids()
    {
        integrated_circuit_signal_ids = generate_circuit_signal_list();
        Dm.append(ref gvars_datum_init_order, "integrated_circuit_signal_ids");
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:45  GLOBAL_LIST_INIT(integrated_circuit_global_signal_ids, generate_global_circuit_signal_list())
    public DmList integrated_circuit_global_signal_ids;

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:45  (GLOBAL_MANAGED(integrated_circuit_global_signal_ids, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalintegrated_circuit_global_signal_ids()
    {
        integrated_circuit_global_signal_ids = generate_global_circuit_signal_list();
        Dm.append(ref gvars_datum_init_order, "integrated_circuit_global_signal_ids");
    }
}

// tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:62
public partial class obj_item_circuit_component_signal_handler : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:62
    public override object ui_state(mob user = null)
    {
        return ADMIN_STATE(R_ADMIN);
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:65
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["global_port_types"] = GLOB.wiremod_fundamental_types;
        return dot;
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:70
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "CircuitSignalHandler", name);
            await ui.open();
            ui.set_autoupdate(FALSE);
        }
        return null;
    }

    // tg: code/modules/wiremod/components/admin/signal_handler/signal_list.dm:77
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object signal_id = @params["signal_id"];

        if (!istext(signal_id))
            return dot;

        object responses = @params["responses"];
        object parameters = @params["parameters"];
        if (!islist(responses) || !islist(parameters))
            return dot;

        DmList sanitized_data = Dm.list();

        foreach (object data__it in Dm.each(responses))
        {
            DmList data = (DmList)data__it;
            Dm.append(sanitized_data, Dm.list(circuit_signal_response(data["name"], text2num(data["bitflag"]))));
        }
        foreach (object data__it in Dm.each(parameters))
        {
            DmList data = (DmList)data__it;
            Dm.append(sanitized_data, Dm.list(circuit_signal_param(data["name"], data["datatype"])));
        }

        string extra_info = "";
        if (Dm.truthy(@params["global"]))
        {
            GLOB.integrated_circuit_global_signal_ids[signal_id] = sanitized_data;
            extra_info = " as a global signal";
        }
        else
            GLOB.integrated_circuit_signal_ids[signal_id] = sanitized_data;
        balloon_alert(usr, I18n.T("successfully added [][]", signal_id, extra_info));
        return dot;
    }
}

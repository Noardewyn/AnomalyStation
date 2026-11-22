// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/dcs_components/component_add_port.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

/// Helper component that handles users adding/removing ports from a circuit component.
// tg: code/modules/wiremod/dcs_components/component_add_port.dm:2
public partial class datum_component_circuit_component_add_port : datum_component
{
    // tg: code/modules/wiremod/dcs_components/component_add_port.dm:24
    [DmParams("port_list", "add_action", "remove_action", "port_type", "is_output", "prefix", "order", "minimum_amount", "maximum_amount")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList port_list = Dm.arg<DmList>(__extra, 0);
        object add_action = Dm.arg(__extra, 1);
        object remove_action = Dm.arg(__extra, 2);
        object port_type = Dm.arg(__extra, 3);
        object is_output = Dm.arg(__extra, 4) ?? FALSE;
        object prefix = Dm.arg(__extra, 5) ?? "Port";
        object order = Dm.arg(__extra, 6) ?? 1;
        object minimum_amount = Dm.arg(__extra, 7) ?? 1;
        object maximum_amount = Dm.arg(__extra, 8) ?? 10;
        await base.Initialize(__extra);
        if (!istype(parent, typeof(obj_item_circuit_component)))
            return COMPONENT_INCOMPATIBLE;
        this.port_list = port_list;
        this.add_action = add_action;
        this.remove_action = remove_action;
        this.port_type = Dm.str(port_type);
        this.is_output = Dm.num(is_output);
        this.prefix = Dm.str(prefix);
        this.order = Dm.num(order);
        this.minimum_amount = Dm.num(minimum_amount);
        this.maximum_amount = Dm.num(maximum_amount);

        if (this.minimum_amount > 0)
        {
            for (float i = 1, i__end = this.minimum_amount; i <= i__end; i++)
                Dm.append(port_list, add_port());
        }
        return null;
    }

    // tg: code/modules/wiremod/dcs_components/component_add_port.dm:42
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_CIRCUIT_COMPONENT_PERFORM_ACTION, nameof(on_action));
        RegisterSignal(parent, COMSIG_CIRCUIT_COMPONENT_SAVE_DATA, nameof(on_data_saved));
        RegisterSignal(parent, COMSIG_CIRCUIT_COMPONENT_LOAD_DATA, nameof(on_data_loaded));
    }

    // tg: code/modules/wiremod/dcs_components/component_add_port.dm:47
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_CIRCUIT_COMPONENT_PERFORM_ACTION,
            COMSIG_CIRCUIT_COMPONENT_SAVE_DATA,
            COMSIG_CIRCUIT_COMPONENT_LOAD_DATA));
    }

    // tg: code/modules/wiremod/dcs_components/component_add_port.dm:54
    [SignalHandler]
    public virtual void on_action(obj_item_circuit_component component = null, mob user = null, object action = null)
    {
        if (Dm.truthy(length(port_list)))
        {
            /// Take the port type of the first stored list element, useful if the types of the ports change
            port_type = Dm.str((port_list[1] as datum_port).datatype);
        }

        if (Dm.eq(action, add_action))
        {
            if (length(port_list) >= maximum_amount)
                return;
            Dm.append(port_list, add_port());
        }
        else if (Dm.eq(action, remove_action))
        {
            if (length(port_list) <= minimum_amount)
                return;
            if (Dm.truthy(is_output))
                component.remove_output_port(pop(port_list) as datum_port_output);
            else
                component.remove_input_port(pop(port_list) as datum_port_input);
        }
    }

    // tg: code/modules/wiremod/dcs_components/component_add_port.dm:72
    public virtual object add_port()
    {
        obj_item_circuit_component component = parent as obj_item_circuit_component;
        string port_name = Dm.text("[] []", prefix, length(port_list) + 1);
        float port_order = Dm.num(this.order) + (length(port_list) + 1) * 0.001f;
        if (Dm.truthy(is_output))
            return component.add_output_port(port_name, port_type, order: port_order);
        else
            return component.add_input_port(port_name, port_type, order: port_order);
    }

    // tg: code/modules/wiremod/dcs_components/component_add_port.dm:80
    [SignalHandler]
    public virtual void on_data_saved(datum source = null, DmList data = null)
    {
        data["port_count"] = length(port_list);
    }

    // tg: code/modules/wiremod/dcs_components/component_add_port.dm:84
    [SignalHandler]
    public virtual void on_data_loaded(datum source = null, DmList data = null)
    {
        object count = data["port_count"];

        if (!Dm.truthy(count) || Dm.num(count) <= length(port_list))
            return;

        while (length(port_list) < Dm.num(count))
            Dm.append(port_list, add_port());
    }
}

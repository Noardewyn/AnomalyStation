// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/wirenet/receive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/modules/wiremod/components/wirenet/receive.dm:1
public partial class obj_item_circuit_component_wirenet_receive : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/wirenet/receive.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_circuit_component_wirenet_connection),
            Dm.named("connection_callback", Dm.@new<datum_callback>(this, nameof(on_powernet_connection))),
            Dm.named("disconnection_callback", Dm.@new<datum_callback>(this, nameof(on_powernet_disconnection))),
            Dm.named("post_set_cable_layer_callback", Dm.@new<datum_callback>(this, nameof(on_set_cable_layer)))
        ));
        return dot;
    }

    // tg: code/modules/wiremod/components/wirenet/receive.dm:34
    public virtual void on_powernet_connection(datum_powernet new_powernet = null)
    {
        RegisterSignal(new_powernet, COMSIG_POWERNET_CIRCUIT_TRANSMISSION, nameof(on_circuit_transmission));
    }

    // tg: code/modules/wiremod/components/wirenet/receive.dm:37
    public virtual void on_powernet_disconnection(datum_powernet old_powernet = null)
    {
        UnregisterSignal(old_powernet, COMSIG_POWERNET_CIRCUIT_TRANSMISSION);
    }

    // tg: code/modules/wiremod/components/wirenet/receive.dm:40
    public virtual void on_set_cable_layer(object new_layer = null)
    {
        cable_layer = Dm.num(new_layer);
    }

    // tg: code/modules/wiremod/components/wirenet/receive.dm:43
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice("Set the cable layer to connect to with the \"1\", \"2\", and \"3\" buttons.", "green", "info"));
        Dm.append(dot, create_ui_notice(Dm.text("Currently connected to: []", GLOB.cable_layer_to_name[Dm.text("[]", cable_layer)]), "green", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/wirenet/receive.dm:48
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/wirenet/receive.dm:51
    public override async DmTask populate_ports()
    {
        data_package = add_output_port("Data Package", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_output;
        enc_key = add_input_port("Encryption Key", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/wirenet/receive.dm:55
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            data_package.set_datatype(PORT_TYPE_LIST(new_datatype));
        }
    }

    // tg: code/modules/wiremod/components/wirenet/receive.dm:60
    [SignalHandler]
    public virtual void on_circuit_transmission(object _source = null, DmList data = null)
    {
        if (!Dm.eq(data["enc_key"], enc_key.value))
            return;

        datum_weakref @ref = data["port"] as datum_weakref;
        // tg types the local as an input port, but the transmitters send their data or list output port: DM does not check the
        // type, and only the port's datatype is read, so the local is the port base here
        datum_port port = @ref?.resolve() as datum_port;
        if (port == null)
            return;

        datum_circuit_datatype datatype_handler = data_package.datatype_handler;
        if (!Dm.truthy(datatype_handler?.can_receive_from_datatype(port.datatype)))
            return;

        data_package.set_output(data["data"]);
        trigger_output.set_output(COMPONENT_SIGNAL);
    }
}

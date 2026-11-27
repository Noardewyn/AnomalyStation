// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/wirenet/send.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/modules/wiremod/components/wirenet/send.dm:1
public partial class obj_item_circuit_component_wirenet_send : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/wirenet/send.dm:28
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

    // tg: code/modules/wiremod/components/wirenet/send.dm:37
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        connected_powernet = null;
        return dot;
    }

    // tg: code/modules/wiremod/components/wirenet/send.dm:41
    public virtual void on_powernet_connection(datum_powernet new_powernet = null)
    {
        connected_powernet = new_powernet;
    }

    // tg: code/modules/wiremod/components/wirenet/send.dm:44
    public virtual void on_powernet_disconnection(datum_powernet old_powernet = null)
    {
        connected_powernet = null;
    }

    // tg: code/modules/wiremod/components/wirenet/send.dm:47
    public virtual void on_set_cable_layer(object new_layer = null)
    {
        cable_layer = Dm.num(new_layer);
    }

    // tg: code/modules/wiremod/components/wirenet/send.dm:50
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice("Set the cable layer to connect to with the \"1\", \"2\", and \"3\" buttons.", "green", "info"));
        Dm.append(dot, create_ui_notice(Dm.text("Currently connected to: []", GLOB.cable_layer_to_name[Dm.text("[]", cable_layer)]), "green", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/wirenet/send.dm:55
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/wirenet/send.dm:58
    public override async DmTask populate_ports()
    {
        data_package = add_input_port("Data Package", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
        enc_key = add_input_port("Encryption Key", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/wirenet/send.dm:62
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            data_package.set_datatype(PORT_TYPE_LIST(new_datatype));
        }
    }

    // tg: code/modules/wiremod/components/wirenet/send.dm:67
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        connected_powernet?.data_transmission(data_package.value as DmList, enc_key.value, WEAKREF(data_package) as datum_weakref);
        return null;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/wirenet/send_literal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/modules/wiremod/components/wirenet/send_literal.dm:1
public partial class obj_item_circuit_component_list_literal_wirenet_send : obj_item_circuit_component_list_literal
{
    // tg: code/modules/wiremod/components/wirenet/send_literal.dm:22
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

    // tg: code/modules/wiremod/components/wirenet/send_literal.dm:31
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        connected_powernet = null;
        return dot;
    }

    // tg: code/modules/wiremod/components/wirenet/send_literal.dm:35
    public virtual void on_powernet_connection(datum_powernet new_powernet = null)
    {
        connected_powernet = new_powernet;
    }

    // tg: code/modules/wiremod/components/wirenet/send_literal.dm:38
    public virtual void on_powernet_disconnection(datum_powernet old_powernet = null)
    {
        connected_powernet = null;
    }

    // tg: code/modules/wiremod/components/wirenet/send_literal.dm:41
    public virtual void on_set_cable_layer(object new_layer = null)
    {
        cable_layer = Dm.num(new_layer);
    }

    // tg: code/modules/wiremod/components/wirenet/send_literal.dm:44
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice("Set the cable layer to connect to with the \"1\", \"2\", and \"3\" buttons.", "green", "info"));
        Dm.append(dot, create_ui_notice(Dm.text("Currently connected to: []", GLOB.cable_layer_to_name[Dm.text("[]", cable_layer)]), "green", "info"));
        return dot;
    }

    // tg: code/modules/wiremod/components/wirenet/send_literal.dm:49
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        enc_key = add_input_port("Encryption Key", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/wirenet/send_literal.dm:53
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        connected_powernet?.data_transmission(list_output.value as DmList, enc_key.value, WEAKREF(list_output) as datum_weakref);
        return null;
    }
}

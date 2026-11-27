// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/ntnet/ntnet_send.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # NTNet Transmitter Component
 *
 * Sends a data package through NTNet
 */
// tg: code/modules/wiremod/components/ntnet/ntnet_send.dm:7
public partial class obj_item_circuit_component_ntnet_send : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/ntnet/ntnet_send.dm:24
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/ntnet/ntnet_send.dm:27
    public override async DmTask populate_ports()
    {
        data_package = add_input_port("Data Package", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
        enc_key = add_input_port("Encryption Key", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/ntnet/ntnet_send.dm:31
    public override object should_receive_input(datum_port_input port = null)
    {
        object dot = base.should_receive_input(port);
        if (!Dm.truthy(dot))
            return FALSE;
        /// If the server is down, don't use power or attempt to send data
        return find_functional_ntnet_relay();
    }

    // tg: code/modules/wiremod/components/ntnet/ntnet_send.dm:38
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            data_package.set_datatype(PORT_TYPE_LIST(new_datatype));
        }
    }

    // tg: code/modules/wiremod/components/ntnet/ntnet_send.dm:43
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        send_ntnet_data(data_package, enc_key.value);
        return null;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/ntnet/ntnet_send_literal.dm
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
 * # NTNet Transmitter List Literal Component
 *
 * Create a list literal and send a data package through NTNet
 *
 * This file is based off of ntnet_send.dm
 * Any changes made to those files should be copied over with discretion
 */
// tg: code/modules/wiremod/components/ntnet/ntnet_send_literal.dm:9
public partial class obj_item_circuit_component_list_literal_ntnet_send : obj_item_circuit_component_list_literal
{
    // tg: code/modules/wiremod/components/ntnet/ntnet_send_literal.dm:17
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        enc_key = add_input_port("Encryption Key", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/ntnet/ntnet_send_literal.dm:21
    public override object should_receive_input(datum_port_input port = null)
    {
        object dot = base.should_receive_input(port);
        if (!Dm.truthy(dot))
            return FALSE;
        /// If the server is down, don't use power or attempt to send data
        return find_functional_ntnet_relay();
    }

    // tg: code/modules/wiremod/components/ntnet/ntnet_send_literal.dm:28
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object dot = base.input_received(port, return_values);
        send_ntnet_data(list_output, enc_key.value);
        return dot;
    }
}

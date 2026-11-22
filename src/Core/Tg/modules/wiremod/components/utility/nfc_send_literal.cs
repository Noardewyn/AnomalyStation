// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/nfc_send_literal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

/**
 * # NFC Transmitter List Literal Component
 *
 * Create a list literal and send a data package through NFC
 *
 * This file is based off of nfc_sendl.dm
 * Any changes made to those files should be copied over with discretion
 */
// tg: code/modules/wiremod/components/utility/nfc_send_literal.dm:9
public partial class obj_item_circuit_component_list_literal_nfc_send : obj_item_circuit_component_list_literal
{
    // tg: code/modules/wiremod/components/utility/nfc_send_literal.dm:20
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        enc_key = add_input_port("Encryption Key", PORT_TYPE_STRING) as datum_port_input;
        target = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/utility/nfc_send_literal.dm:25
    public override object should_receive_input(datum_port_input port = null)
    {
        object dot = base.should_receive_input(port);
        if (!Dm.truthy(dot))
            return FALSE;
        /// If the server is down, don't use power or attempt to send data
        return find_functional_ntnet_relay();
    }

    // tg: code/modules/wiremod/components/utility/nfc_send_literal.dm:32
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        object dot = base.input_received(port, return_values);
        if (isatom(target.value))
        {
            atom target_enty = target.value as atom;
            // tg sends no sender here, unlike nfc_send.dm:48: the receiver's nfc_receive reads the data list as its sender (kept as in tg)
            target_enty.SEND_SIGNAL(COMSIG_CIRCUIT_NFC_DATA_SENT, Dm.list(target_enty, Dm.list(
                Dm.kv("data", list_output.value),
                Dm.kv("enc_key", enc_key.value),
                Dm.kv("port", WEAKREF(list_output)))));
        }
        return dot;
    }
}

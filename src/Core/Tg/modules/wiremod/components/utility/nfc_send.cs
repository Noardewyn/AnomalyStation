// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/nfc_send.dm
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
 * # NFC Transmitter Component
 *
 * Sends a data package through NFC
 * Only the targeted shell will receive the message
 */
// tg: code/modules/wiremod/components/utility/nfc_send.dm:8
public partial class obj_item_circuit_component_nfc_send : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/utility/nfc_send.dm:27
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/utility/nfc_send.dm:30
    public override async DmTask populate_ports()
    {
        data_package = add_input_port("Data Package", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_input;
        enc_key = add_input_port("Encryption Key", PORT_TYPE_STRING) as datum_port_input;
        target = add_input_port("Target", PORT_TYPE_ATOM) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/utility/nfc_send.dm:35
    public override object should_receive_input(datum_port_input port = null)
    {
        object dot = base.should_receive_input(port);
        if (!Dm.truthy(dot))
            return FALSE;
        return dot;
    }

    // tg: code/modules/wiremod/components/utility/nfc_send.dm:40
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            data_package.set_datatype(PORT_TYPE_LIST(new_datatype));
        }
    }

    // tg: code/modules/wiremod/components/utility/nfc_send.dm:45
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (isatom(target.value))
        {
            atom target_enty = target.value as atom;
            target_enty.SEND_SIGNAL(COMSIG_CIRCUIT_NFC_DATA_SENT, Dm.list(target_enty, parent, Dm.list(
                Dm.kv("data", data_package.value),
                Dm.kv("enc_key", enc_key.value),
                Dm.kv("port", WEAKREF(data_package)))));
        }
        return null;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/ntnet/ntnet_receive.dm
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
 * # NTNet Receiver Component
 *
 * Receives data through NTNet.
 */
// tg: code/modules/wiremod/components/ntnet/ntnet_receive.dm:6
public partial class obj_item_circuit_component_ntnet_receive : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/ntnet/ntnet_receive.dm:23
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/ntnet/ntnet_receive.dm:26
    public override async DmTask populate_ports()
    {
        data_package = add_output_port("Data Package", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_output;
        enc_key = add_input_port("Encryption Key", PORT_TYPE_STRING) as datum_port_input;
        RegisterSignal(SSdcs, COMSIG_GLOB_CIRCUIT_NTNET_DATA_SENT, nameof(ntnet_receive));
    }

    // tg: code/modules/wiremod/components/ntnet/ntnet_receive.dm:31
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            data_package.set_datatype(PORT_TYPE_LIST(new_datatype));
        }
    }

    // tg: code/modules/wiremod/components/ntnet/ntnet_receive.dm:37
    [SignalHandler]
    public virtual void ntnet_receive(obj_item_circuit_component_ntnet_send source = null, DmList data = null)
    {
        if (!Dm.truthy(find_functional_ntnet_relay()))
            return;
        if (!Dm.eq(data["enc_key"], enc_key.value))
            return;

        datum_weakref @ref = data["port"] as datum_weakref;
        // tg types the local as an input port, but the list literal transmitters send their output port: DM does not check the
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

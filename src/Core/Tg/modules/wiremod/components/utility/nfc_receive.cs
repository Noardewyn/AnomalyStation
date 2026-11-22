// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/utility/nfc_receive.dm
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
 * # NFC Receiver Component
 *
 * Sends a data package through NFC directly to a shell
 * if we ever get more shells like BCI's that are nested, keep in mind this may not work correctly unless adjusted
 */
// tg: code/modules/wiremod/components/utility/nfc_receive.dm:9
public partial class obj_item_circuit_component_nfc_receive : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/utility/nfc_receive.dm:25
    public override void populate_options()
    {
        list_options = add_option_port("List Type", GLOB.wiremod_basic_types) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/utility/nfc_receive.dm:28
    public override async DmTask populate_ports()
    {
        data_package = add_output_port("Data Package", PORT_TYPE_LIST(PORT_TYPE_ANY)) as datum_port_output;
        enc_key = add_input_port("Encryption Key", PORT_TYPE_STRING) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/utility/nfc_receive.dm:32
    public override async DmTask register_shell(atom_movable shell = null)
    {
        RegisterSignal(shell, COMSIG_CIRCUIT_NFC_DATA_SENT, nameof(nfc_receive));
        RegisterSignal(shell, COMSIG_ORGAN_IMPLANTED, nameof(on_organ_implanted));
        RegisterSignal(shell, COMSIG_ORGAN_REMOVED, nameof(on_organ_removed));
    }

    // tg: code/modules/wiremod/components/utility/nfc_receive.dm:37
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, Dm.list(
            COMSIG_CIRCUIT_NFC_DATA_SENT,
            COMSIG_ORGAN_IMPLANTED,
            COMSIG_ORGAN_REMOVED));
    }

    // tg: code/modules/wiremod/components/utility/nfc_receive.dm:44
    [SignalHandler]
    public virtual void on_organ_implanted(datum source = null, mob_living_carbon owner = null)
    {
        RegisterSignal(owner, COMSIG_CIRCUIT_NFC_DATA_SENT, nameof(nfc_receive));
    }

    // tg: code/modules/wiremod/components/utility/nfc_receive.dm:49
    [SignalHandler]
    public virtual void on_organ_removed(datum source = null, mob_living_carbon owner = null)
    {
        UnregisterSignal(owner, COMSIG_CIRCUIT_NFC_DATA_SENT);
    }

    // tg: code/modules/wiremod/components/utility/nfc_receive.dm:55
    public override void pre_input_received(datum_port_input port = null)
    {
        if (port == list_options)
        {
            object new_datatype = list_options.value;
            data_package.set_datatype(PORT_TYPE_LIST(new_datatype));
        }
    }

    // tg: code/modules/wiremod/components/utility/nfc_receive.dm:61
    [SignalHandler]
    public virtual void nfc_receive(obj_item_circuit_component source = null, obj sender = null, DmList data = null)
    {
        if (get_dist(sender, parent) >= 10)
            return;

        if (!Dm.eq(data["enc_key"], enc_key.value))
            return;

        datum_weakref @ref = data["port"] as datum_weakref;
        datum_port_input port = @ref?.resolve() as datum_port_input;
        if (port == null)
            return;

        datum_circuit_datatype datatype_handler = data_package.datatype_handler;
        if (!Dm.truthy(datatype_handler?.can_receive_from_datatype(port.datatype)))
            return;

        data_package.set_output(data["data"]);
        trigger_output.set_output(COMPONENT_SIGNAL);
    }
}

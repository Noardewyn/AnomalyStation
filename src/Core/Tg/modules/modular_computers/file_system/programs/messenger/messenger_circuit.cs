// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the messenger's circuit component, which sends PDA messages from a circuit plugged into the computer, reports
// the messages it receives and plays or sets its ringtone. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:6
public partial class obj_item_circuit_component_mod_program_messenger : obj_item_circuit_component_mod_program
{
    private const float MESSENGER_CIRCUIT_MIN_COOLDOWN = 5 * SECONDS;
    private const float MESSENGER_CIRCUIT_MAX_COOLDOWN = 45 * SECONDS;
    private const float MESSENGER_CIRCUIT_CD_PER_RECIPIENT = 1.5f * SECONDS;
    private const float MESSENGER_CIRCUIT_RINGTONE_CD = 1.5f * SECONDS;

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:34
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        received_message = add_output_port("Message", PORT_TYPE_STRING) as datum_port_output;
        sender_name = add_output_port("Sender Name", PORT_TYPE_STRING) as datum_port_output;
        sender_job = add_output_port("Sender Job", PORT_TYPE_STRING) as datum_port_output;
        sender_device = add_output_port("Sender Device", PORT_TYPE_ATOM) as datum_port_output;
        received = add_output_port("Received", PORT_TYPE_SIGNAL) as datum_port_output;

        message = add_input_port("Message", PORT_TYPE_STRING) as datum_port_input;
        targets = add_input_port("Targets", PORT_TYPE_LIST(PORT_TYPE_ATOM)) as datum_port_input;

        ring = add_input_port("Play Ringtone", PORT_TYPE_SIGNAL, trigger: nameof(play_ringtone)) as datum_port_input;
        set_ring = add_input_port("Set Ringtone", PORT_TYPE_STRING, trigger: nameof(set_ringtone)) as datum_port_input;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:48
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        DmList messenger_targets = Dm.list();
        foreach (object entry in Dm.each(targets.value))
        {
            datum_weakref @ref = entry as datum_weakref;
            obj_item_modular_computer modpc = @ref?.resolve() as obj_item_modular_computer; //entity ports are hardrefs, entity list ports are weakref. :thonking:
            if (modpc == null)
                continue;
            datum_computer_file_program_messenger target_messenger = Dm.locate_type(typeof(datum_computer_file_program_messenger), modpc.stored_files) as datum_computer_file_program_messenger;
            if (target_messenger != null)
                Dm.combine(ref messenger_targets, target_messenger);
        }
        if (!Dm.truthy(length(messenger_targets)))
            return null;
        datum_computer_file_program_messenger messenger = associated_program as datum_computer_file_program_messenger;
        object filterd_message = censor_ic_filter_for_pdas(message.value);

        ///We need to async send_message() because some tcomms devices might sleep. Also because of (non-existent) user tgui alerts.
        Waiting.Detach(messenger.send_message(this, filterd_message, messenger_targets));
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:65
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        obj_item_modular_computer computer = (associated_program as datum_computer_file_program).computer;
        RegisterSignal(computer, COMSIG_MODULAR_PDA_MESSAGE_RECEIVED, nameof(message_received));
        RegisterSignal(computer, COMSIG_MODULAR_PDA_MESSAGE_SENT, nameof(message_sent));
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:70
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal((associated_program as datum_computer_file_program).computer, Dm.list(COMSIG_MODULAR_PDA_MESSAGE_RECEIVED, COMSIG_MODULAR_PDA_MESSAGE_SENT));
        await base.unregister_shell(shell);
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:74
    public override object get_ui_notices()
    {
        object dot = base.get_ui_notices();
        Dm.append(ref dot, create_ui_notice(Dm.text("Cooldown per recipient: []", DisplayTimeText(MESSENGER_CIRCUIT_CD_PER_RECIPIENT)), "orange", "stopwatch"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Minimum cooldown: []", DisplayTimeText(MESSENGER_CIRCUIT_MIN_COOLDOWN)), "orange", "stopwatch"));
        Dm.append(ref dot, create_ui_notice(Dm.text("Maximum cooldown: []", DisplayTimeText(MESSENGER_CIRCUIT_MAX_COOLDOWN)), "orange", "stopwatch"));
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:80
    [SignalHandler]
    public virtual void message_received(datum source = null, datum_signal_subspace_messaging_tablet_message signal = null, object message_job = null, object message_name = null)
    {
        received_message.set_value(signal.data["message"]);
        sender_name.set_output(message_name);
        sender_job.set_output(message_job);

        atom source_device = null;
        // a messenger's signal names its computer as the source, so this branch (which would cast the receiving computer instead of
        // signal.source) never runs; kept as tg has it
        if ((object)signal.source is datum_computer_file_program_messenger)
        {
            datum_computer_file_program_messenger sender_messenger = source as datum_computer_file_program_messenger;
            source_device = sender_messenger?.computer;
        }
        else if (isatom(signal.source))
            source_device = signal.source as atom;

        sender_device.set_output(source_device);
        received.set_output(COMPONENT_SIGNAL);
    }

    ///Set the cooldown after the message was sent (by us)
    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:97
    [SignalHandler]
    public virtual void message_sent(datum source = null, atom origin = null, datum_signal_subspace_messaging_tablet_message signal = null)
    {
        if (origin != this)
            return;
        float targets_length = length(signal.data["targets"]);
        datum_computer_file_program_messenger messenger = associated_program as datum_computer_file_program_messenger;
        float cool = Dm.num(clamp(targets_length * MESSENGER_CIRCUIT_CD_PER_RECIPIENT, MESSENGER_CIRCUIT_MIN_COOLDOWN, MESSENGER_CIRCUIT_MAX_COOLDOWN));
        messenger.last_text = Dm.num(world.time) + cool; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:106
    public virtual void set_ringtone(datum_port port = null)
    {
        datum_computer_file_program_messenger messenger = associated_program as datum_computer_file_program_messenger;
        messenger.set_ringtone(set_ring.value);
    }

    // tg: code/modules/modular_computers/file_system/programs/messenger/messenger_circuit.dm:110
    public virtual void play_ringtone(datum_port port = null)
    {
        if (!(ring_cd <= Dm.num(world.time))) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
            return;
        ring_cd = Dm.num(world.time) + MESSENGER_CIRCUIT_RINGTONE_CD; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        datum_computer_file_program_messenger messenger = associated_program as datum_computer_file_program_messenger;
        messenger.computer.ring(messenger.ringtone);
    }
}

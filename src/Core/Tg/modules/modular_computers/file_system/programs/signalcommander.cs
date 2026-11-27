// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/signalcommander.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SignalCommander (gaps S4b, D327): a PDA's built-in signaler, with its NtosSignaler window (lines 1-98), and its circuit component
// (:100-123) that sets the frequency and code and signals from a circuit plugged into the computer. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:1
public partial class datum_computer_file_program_signal_commander : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:24
    public override async DmTask<object> on_start(mob_living user = null)
    {
        object dot = await base.on_start(user);
        set_frequency(signal_frequency);
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:28
    public override object kill_program(mob user = null)
    {
        object dot = base.kill_program(user);
        SSradio.remove_object(computer, signal_frequency);
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:32
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["frequency"] = signal_frequency;
        data["cooldown"] = signal_cooldown_time;
        data["code"] = signal_code;
        data["minFrequency"] = MIN_FREE_FREQ;
        data["maxFrequency"] = MAX_FREE_FREQ;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:41
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (action)
        {
            case "signal":
                signal(usr as atom); // INVOKE_ASYNC of a proc that never sleeps
                dot = TRUE;
                break;
            case "freq":
            {
                float new_signal_frequency = sanitize_frequency(unformat_frequency(@params["freq"]), TRUE);
                set_frequency(new_signal_frequency);
                dot = TRUE;
                break;
            }
            case "code":
                signal_code = Dm.num(text2num(@params["code"]));
                signal_code = Dm.round(signal_code);
                dot = TRUE;
                break;
            case "reset":
                if (Dm.eq(@params["reset"], "freq"))
                    signal_frequency = Dm.initial(this, (datum_computer_file_program_signal_commander s) => s.signal_frequency);
                else
                    signal_code = Dm.initial(this, (datum_computer_file_program_signal_commander s) => s.signal_code);
                dot = TRUE;
                break;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:62
    public virtual void signal(atom source = null)
    {
        if (radio_connection == null)
            return;

        mob user = null;
        obj_item_circuit_component signaling = null;
        if (ismob(source))
            user = source as mob;
        else if (source is obj_item_circuit_component)
            signaling = source as obj_item_circuit_component;

        // COOLDOWN_FINISHED(src, signal_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(signal_cooldown <= world.time))
        {
            if (user != null)
                computer.balloon_alert(user, "cooling down!");
            return;
        }

        // COOLDOWN_START(src, signal_cooldown, signal_cooldown_time) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        signal_cooldown = world.time + signal_cooldown_time;
        if (user != null)
            computer.balloon_alert(user, "signaled");

        string time = time2text(world.realtime, "hh:mm:ss", TIMEZONE_UTC);
        turf T = get_turf(computer) as turf;
        object user_deets;
        if (signaling != null)
            user_deets = Dm.text("[]", signaling.parent.get_creator()); // gaps CI1: the circuit's creator
        else
            user_deets = Dm.text("[]", key_name(usr));
        string logging_data = Dm.text("[] <B>:</B> [] used the computer '[]' @ location ([],[],[]) <B>:</B> []/[]", time, user_deets,
            Dm.initial(computer, (obj_item_modular_computer c) => c.name), T.x, T.y, T.z, format_frequency(signal_frequency), signal_code);
        add_to_signaler_investigate_log(logging_data);

        datum_signal signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("code", signal_code), Dm.kv("key", signaling?.parent.owner_id)), null, logging_data);
        radio_connection.post_signal(computer, signal);
    }

    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:95
    public virtual void set_frequency(object new_frequency = null)
    {
        SSradio.remove_object(computer, signal_frequency);
        signal_frequency = Dm.num(new_frequency);
        radio_connection = SSradio.add_object(computer, signal_frequency, RADIO_SIGNALER);
    }
}

// tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:100
public partial class obj_item_circuit_component_mod_program_signaler : obj_item_circuit_component_mod_program
{
    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:109
    public override async DmTask populate_ports()
    {
        await base.populate_ports();
        freq = add_input_port("Frequency", PORT_TYPE_NUMBER, trigger: nameof(set_freq), @default: FREQ_SIGNALER) as datum_port_input;
        code = add_input_port("Code", PORT_TYPE_NUMBER, trigger: nameof(set_code), @default: DEFAULT_SIGNALER_CODE) as datum_port_input;
    }

    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:114
    public virtual void set_freq(datum_port port = null)
    {
        datum_computer_file_program_signal_commander signaler = associated_program as datum_computer_file_program_signal_commander;
        signaler.set_frequency(clamp(freq.value, MIN_FREE_FREQ, MAX_FREE_FREQ));
    }

    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:118
    public virtual void set_code(datum_port port = null)
    {
        datum_computer_file_program_signal_commander signaler = associated_program as datum_computer_file_program_signal_commander;
        signaler.signal_code = Dm.round(Dm.num(clamp(code.value, 1, 100)));
    }

    // tg: code/modules/modular_computers/file_system/programs/signalcommander.dm:122
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        datum_computer_file_program_signal_commander program = associated_program as datum_computer_file_program_signal_commander;
        Waiting.DetachSync(() => program.signal(this));
        return null;
    }
}

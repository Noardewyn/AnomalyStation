// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/radio.dm
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
 * # Radio Component
 *
 * Listens out for signals on the designated frequencies and sends signals on designated frequencies
 */
// tg: code/modules/wiremod/components/action/radio.dm:9
public partial class obj_item_circuit_component_radio : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/radio.dm:1
    private const string COMP_RADIO_PUBLIC = "public";
    // tg: code/modules/wiremod/components/action/radio.dm:2
    private const string COMP_RADIO_PRIVATE = "private";

    // tg: code/modules/wiremod/components/action/radio.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (signal_cooldown_time > 0)
            desc = Dm.text("[] It has a [] second cooldown between sending signals.", desc, signal_cooldown_time * 0.1f);
        return dot;
    }

    // tg: code/modules/wiremod/components/action/radio.dm:42
    public override async DmTask register_shell(atom_movable shell = null)
    {
        parent_shell = shell;
        object potential_fingerprints = shell.fingerprintslast;
        if (potential_fingerprints != null)
            owner_ckey = potential_fingerprints;
    }

    // tg: code/modules/wiremod/components/action/radio.dm:48
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        parent_shell = null;
    }

    // tg: code/modules/wiremod/components/action/radio.dm:51  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_radio__populate_options__component_options;

    // tg: code/modules/wiremod/components/action/radio.dm:51
    public override void populate_options()
    {
        public_options = add_option_port("Encryption Options", populate_options__component_options) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/components/action/radio.dm:58
    public override async DmTask populate_ports()
    {
        freq = add_input_port("Frequency", PORT_TYPE_NUMBER, @default: FREQ_SIGNALER) as datum_port_input;
        code = add_input_port("Code", PORT_TYPE_NUMBER, @default: DEFAULT_SIGNALER_CODE) as datum_port_input;
        trigger_component();
        // These are cleaned up on the parent
        trigger_input = add_input_port("Send", PORT_TYPE_SIGNAL) as datum_port_input;
        trigger_output = add_output_port("Received", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/action/radio.dm:66
    public override object Destroy(float? force = null)
    {
        SSradio?.remove_object(this, current_freq); // rules 10.6: a test harness world may lack the subsystem
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/components/action/radio.dm:70
    public override void pre_input_received(datum_port_input port = null)
    {
        freq.set_value(sanitize_frequency(freq.value, TRUE));
    }

    // tg: code/modules/wiremod/components/action/radio.dm:73
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        Waiting.DetachSync(() => handle_radio_input(port));
        return null;
    }

    // tg: code/modules/wiremod/components/action/radio.dm:76
    public virtual void handle_radio_input(datum_port_input port = null)
    {
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(parent, COOLDOWN_SIGNALLER_SEND)))
            return;

        object frequency = freq.value;
        if (!Dm.eq(frequency, current_freq))
        {
            SSradio.remove_object(this, current_freq);
            radio_connection = SSradio.add_object(this, frequency, RADIO_SIGNALER);
            current_freq = Dm.num(frequency);
        }

        if (COMPONENT_TRIGGERED_BY(trigger_input, port))
        {
            float signal_code = Dm.round(Dm.num(code.value));
            turf location = get_turf(this);
            string time = time2text(world.realtime, "hh:mm:ss", TIMEZONE_UTC);

            DmList loggable_strings = Dm.list(Dm.text("[] <B>:</B> The [] @ location ([],[],[]) transmitted the following signal <B>:</B> []/[] via the radio circuit component.", time, QDELETED(parent_shell) ? "null circuit shell(?)" : parent_shell, location.x, location.y, location.z, format_frequency(current_freq), signal_code));
            if (owner_ckey != null)
                Dm.append(loggable_strings, Dm.text("<B>:</B> The person who inserted the signalling circuit component was very likely [].", owner_ckey));
            if (!QDELETED(parent_shell))
                Dm.append(loggable_strings, Dm.text("<B>:</B> The last fingerprints on the containing shell was [].", parent_shell.fingerprintslast));

            string loggable_string = loggable_strings.Join(" ");
            add_to_signaler_investigate_log(loggable_string);
            // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
            obj_item_integrated_circuit cd_source = parent;
            LAZYSET(ref cd_source.cooldowns, COOLDOWN_SIGNALLER_SEND, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), cd_source, COOLDOWN_SIGNALLER_SEND), signal_cooldown_time, file: "code/modules/wiremod/components/action/radio.dm", line: 98));

            datum_signal signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("code", signal_code), Dm.kv("key", parent?.owner_id)), Dm.named("logging_data", loggable_string));
            radio_connection.post_signal(this, signal);
        }
    }

    // tg: code/modules/wiremod/components/action/radio.dm:103
    public override object receive_signal(datum_signal signal = null)
    {
        object dot = FALSE;
        if (signal == null)
            return dot;
        if (!Dm.eq(signal.data["code"], Dm.round(Dm.num(code.value is var or__1 && Dm.truthy(or__1) ? or__1 : 0))))
            return dot;

        if (Dm.eq(public_options.value, COMP_RADIO_PRIVATE) && !Dm.eq(parent?.owner_id, signal.data["key"]))
            return dot;

        trigger_output.set_output(COMPONENT_SIGNAL);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/action/radio.dm:52
    internal DmList obj_item_circuit_component_radio__populate_options__component_options = Dm.list(
        "public",
        "private");
}

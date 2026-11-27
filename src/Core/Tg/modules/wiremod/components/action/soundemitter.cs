// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/soundemitter.dm
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
 * # Sound Emitter Component
 *
 * A component that emits a sound when it receives an input.
 */
// tg: code/modules/wiremod/components/action/soundemitter.dm:6
public partial class obj_item_circuit_component_soundemitter : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/soundemitter.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(config?.Get(typeof(datum_config_entry_flag_disallow_circuit_sounds))))
            update_ui_alerts(new_flag: CIRCUIT_FLAG_DISABLED);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/wiremod/components/action/soundemitter.dm:39
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        Dm.append(dot, create_ui_notice(Dm.text("Sound Cooldown: []", DisplayTimeText(sound_cooldown)), "orange", "stopwatch"));
        if (Dm.truthy(config?.Get(typeof(datum_config_entry_flag_disallow_circuit_sounds))))
        {
            Dm.append(dot, create_ui_notice("Non-functional", "red", "exclamation"));
            update_ui_alerts(new_flag: CIRCUIT_FLAG_DISABLED);
        }
        else
            update_ui_alerts(remove_flag: CIRCUIT_FLAG_DISABLED);
        return dot;
    }

    // tg: code/modules/wiremod/components/action/soundemitter.dm:49
    public override async DmTask populate_ports()
    {
        volume = add_input_port("Volume", PORT_TYPE_NUMBER, @default: 35) as datum_port_input;
        frequency = add_input_port("Frequency", PORT_TYPE_NUMBER, @default: 0) as datum_port_input;
        backwards = add_input_port("Play Backwards", PORT_TYPE_BOOLEAN, @default: FALSE) as datum_port_input;
    }

    // tg: code/modules/wiremod/components/action/soundemitter.dm:54  var/static/component_options
    private static ref DmList populate_options__component_options => ref TgState.Current.obj_item_circuit_component_soundemitter__populate_options__component_options;

    // tg: code/modules/wiremod/components/action/soundemitter.dm:54
    public override void populate_options()
    {
        sound_file = add_option_port("Sound Option", populate_options__component_options) as datum_port_input_option;
        options_map = populate_options__component_options;
    }

    // tg: code/modules/wiremod/components/action/soundemitter.dm:80
    public override void pre_input_received(datum_port_input port = null)
    {
        volume.set_value(clamp(volume.value, 0, 100));
        frequency.set_value(clamp(frequency.value, min_pitch, max_pitch));
        backwards.set_value(clamp(backwards.value, 0, 1));
    }

    // tg: code/modules/wiremod/components/action/soundemitter.dm:85
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (Dm.truthy(config?.Get(typeof(datum_config_entry_flag_disallow_circuit_sounds))))
        {
            // Without constantly checking the config 24/7 or sending a signal to every circuit, best we can do to update existing emitters is this.
            update_ui_alerts(new_flag: CIRCUIT_FLAG_DISABLED);
            return null;
        }
        else
            update_ui_alerts(remove_flag: CIRCUIT_FLAG_DISABLED);

        if (parent.shell == null)
            return null;

        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(parent.shell, COOLDOWN_CIRCUIT_SOUNDEMITTER)))
            return null;

        object sound_to_play = options_map[sound_file.value];
        if (!Dm.truthy(sound_to_play))
            return null;

        float actual_frequency = 1 + (Dm.num(frequency.value) / 100);
        float actual_volume = max_volume * (Dm.num(volume.value) / 100);

        if (Dm.truthy(backwards.value))
            actual_frequency = -actual_frequency;

        playsound(this, sound_to_play, actual_volume, TRUE, frequency: actual_frequency);

        // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
        atom_movable cd_source = parent.shell;
        LAZYSET(ref cd_source.cooldowns, COOLDOWN_CIRCUIT_SOUNDEMITTER, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), cd_source, COOLDOWN_CIRCUIT_SOUNDEMITTER), sound_cooldown, file: "code/modules/wiremod/components/action/soundemitter.dm", line: 114));
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/action/soundemitter.dm:55
    internal DmList obj_item_circuit_component_soundemitter__populate_options__component_options = Dm.list(
        Dm.kv("Buzz", Dm.resource("sound/machines/buzz/buzz-sigh.ogg")),
        Dm.kv("Buzz Twice", Dm.resource("sound/machines/buzz/buzz-two.ogg")),
        Dm.kv("Chime", Dm.resource("sound/machines/chime.ogg")),
        Dm.kv("Honk", Dm.resource("sound/items/bikehorn.ogg")),
        Dm.kv("Ping", Dm.resource("sound/machines/ping.ogg")),
        Dm.kv("Sad Trombone", Dm.resource("sound/misc/sadtrombone.ogg")),
        Dm.kv("Warn", Dm.resource("sound/machines/warning-buzzer.ogg")),
        Dm.kv("Slow Clap", Dm.resource("sound/machines/slowclap.ogg")),
        Dm.kv("Moth Buzz", Dm.resource("sound/mobs/humanoids/moth/scream_moth.ogg")),
        Dm.kv("Squeak", Dm.resource("sound/items/toy_squeak/toysqueak1.ogg")),
        Dm.kv("Rip", Dm.resource("sound/items/poster/poster_ripped.ogg")),
        Dm.kv("Coinflip", Dm.resource("sound/items/coinflip.ogg")),
        Dm.kv("Megaphone", Dm.resource("sound/items/megaphone.ogg")),
        Dm.kv("Warpwhistle", Dm.resource("sound/effects/magic/warpwhistle.ogg")),
        Dm.kv("Hiss", Dm.resource("sound/mobs/non-humanoids/hiss/hiss1.ogg")),
        Dm.kv("Lizard", Dm.resource("sound/mobs/humanoids/lizard/lizard_scream_1.ogg")),
        Dm.kv("Flashbang", Dm.resource("sound/items/weapons/flashbang.ogg")),
        Dm.kv("Flash", Dm.resource("sound/items/weapons/flash.ogg")),
        Dm.kv("Whip", Dm.resource("sound/items/weapons/whip.ogg")),
        Dm.kv("Laugh Track", Dm.resource("sound/items/sitcom_laugh/sitcomLaugh1.ogg")),
        Dm.kv("Gavel", Dm.resource("sound/items/gavel.ogg")));
}

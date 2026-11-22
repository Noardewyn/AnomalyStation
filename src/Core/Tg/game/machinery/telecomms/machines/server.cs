// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machines/server.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: telecomms): the telecommunication server, its log entries, its channel names and colours and
// its presets. The var blocks are generated (D111).

/**
 * The server logs all traffic and signal data. Once it records the signal, it
 * sends it to the subspace broadcaster.
 *
 * Store a maximum of `MAX_LOG_ENTRIES` (400) log entries and then deletes them.
 */
// tg: code/game/machinery/telecomms/machines/server.dm:9
public partial class obj_machinery_telecomms_server : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machines/server.dm:1  #define MAX_LOG_ENTRIES 400 (file-local)
    private const float MAX_LOG_ENTRIES = 400;

    // tg: code/game/machinery/telecomms/machines/server.dm:27
    public override async DmTask receive_information(datum_signal signal_in = null, obj_machinery_telecomms machine_from = null)
    {
        // can't log non-vocal signals
        if (!(signal_in is datum_signal_subspace_vocal signal) || !Dm.truthy(signal.data["message"]) || !is_freq_listening(signal))
            return;

        if (traffic > 0)
            total_traffic += traffic; // add current traffic to total traffic

        DmList freq_info = frequency_infos[Dm.text("[]", signal.frequency)] as DmList;
        if (freq_info != null)
        {
            signal.data["frequency_name"] = freq_info["name"];
            signal.data["frequency_color"] = freq_info["color"];
        }

        // Delete particularly old logs
        if (log_entries.len >= MAX_LOG_ENTRIES)
            log_entries.Cut(1, 2);

        // Don't create a log if the frequency is banned from being logged
        if (!Dm.@in(signal.frequency, banned_frequencies))
        {
            datum_comm_log_entry log = Dm.@new<datum_comm_log_entry>();
            log.parameters["mobtype"] = signal.virt.source?.GetType();
            log.parameters["name"] = signal.data["name"];
            log.parameters["job"] = signal.data["job"];
            log.parameters["message"] = signal.data["message"];
            log.parameters["language"] = signal.language;

            // If the signal is still compressed, make the log entry gibberish
            object compression = signal.data["compression"];
            if (Dm.gt(compression, NONE))
            {
                log.input_type = "Corrupt File";
                float replace_characters = Dm.ge(compression, 20) ? TRUE : FALSE;
                log.parameters["name"] = Gibberish(signal.data["name"], replace_characters);
                log.parameters["job"] = Gibberish(signal.data["job"], replace_characters);
                log.parameters["message"] = Gibberish(signal.data["message"], replace_characters);
            }

            // Give the log a name and store it
            string identifier = num2text(Dm.rand(-1000, 1000) + world.time);
            log.name = Dm.text("data packet ([])", md5(identifier));
            Dm.append(ref log_entries, log);
        }

        object can_send = await relay_information(signal, typeof(obj_machinery_telecomms_hub));
        if (!Dm.truthy(can_send))
            await relay_information(signal, typeof(obj_machinery_telecomms_broadcaster));

        await use_energy(idle_power_usage);
    }
}

// Preset Servers
// tg: code/game/machinery/telecomms/machines/server.dm:86
public partial class obj_machinery_telecomms_server_presets : obj_machinery_telecomms_server
{
    // tg: code/game/machinery/telecomms/machines/server.dm:89
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = id;
        return dot;
    }
}

// tg: code/game/machinery/telecomms/machines/server.dm:94
public partial class obj_machinery_telecomms_server_presets_science : obj_machinery_telecomms_server_presets
{
    // tg: code/game/machinery/telecomms/machines/server.dm:99
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        frequency_infos[Dm.text("[]", FREQ_SCIENCE)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_SCIENCE),
            Dm.kv("color", RADIO_COLOR_SCIENCE));
    }
}

// tg: code/game/machinery/telecomms/machines/server.dm:106
public partial class obj_machinery_telecomms_server_presets_medical : obj_machinery_telecomms_server_presets
{
    // tg: code/game/machinery/telecomms/machines/server.dm:111
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        frequency_infos[Dm.text("[]", FREQ_MEDICAL)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_MEDICAL),
            Dm.kv("color", RADIO_COLOR_MEDICAL));
    }
}

// tg: code/game/machinery/telecomms/machines/server.dm:118
public partial class obj_machinery_telecomms_server_presets_supply : obj_machinery_telecomms_server_presets
{
    // tg: code/game/machinery/telecomms/machines/server.dm:123
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        frequency_infos[Dm.text("[]", FREQ_SUPPLY)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_SUPPLY),
            Dm.kv("color", RADIO_COLOR_SUPPLY));
    }
}

// tg: code/game/machinery/telecomms/machines/server.dm:130
public partial class obj_machinery_telecomms_server_presets_service : obj_machinery_telecomms_server_presets
{
    // tg: code/game/machinery/telecomms/machines/server.dm:135
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        frequency_infos[Dm.text("[]", FREQ_SERVICE)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_SERVICE),
            Dm.kv("color", RADIO_COLOR_SERVICE));
        frequency_infos[Dm.text("[]", FREQ_ENTERTAINMENT)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_ENTERTAINMENT),
            Dm.kv("color", RADIO_COLOR_ENTERTAIMENT));
    }
}

// tg: code/game/machinery/telecomms/machines/server.dm:146
public partial class obj_machinery_telecomms_server_presets_common : obj_machinery_telecomms_server_presets
{
    // tg: code/game/machinery/telecomms/machines/server.dm:151
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        frequency_infos[Dm.text("[]", FREQ_COMMON)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_COMMON),
            Dm.kv("color", RADIO_COLOR_COMMON));
        frequency_infos[Dm.text("[]", FREQ_AI_PRIVATE)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_AI_PRIVATE),
            Dm.kv("color", RADIO_COLOR_AI_PRIVATE));
    }

    // tg: code/game/machinery/telecomms/machines/server.dm:162
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Common and other radio frequencies for people to freely use
        for (float i = MIN_FREQ; i <= MAX_FREQ; i += 2)
            Dm.combine(freq_listening, i);
        return dot;
    }
}

// tg: code/game/machinery/telecomms/machines/server.dm:168
public partial class obj_machinery_telecomms_server_presets_command : obj_machinery_telecomms_server_presets
{
    // tg: code/game/machinery/telecomms/machines/server.dm:173
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        frequency_infos[Dm.text("[]", FREQ_COMMAND)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_COMMAND),
            Dm.kv("color", RADIO_COLOR_COMMAND));
    }
}

// tg: code/game/machinery/telecomms/machines/server.dm:180
public partial class obj_machinery_telecomms_server_presets_engineering : obj_machinery_telecomms_server_presets
{
    // tg: code/game/machinery/telecomms/machines/server.dm:185
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        frequency_infos[Dm.text("[]", FREQ_ENGINEERING)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_ENGINEERING),
            Dm.kv("color", RADIO_COLOR_ENGINEERING));
    }
}

// tg: code/game/machinery/telecomms/machines/server.dm:192
public partial class obj_machinery_telecomms_server_presets_security : obj_machinery_telecomms_server_presets
{
    // tg: code/game/machinery/telecomms/machines/server.dm:197
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        frequency_infos[Dm.text("[]", FREQ_SECURITY)] = Dm.list(
            Dm.kv("name", RADIO_CHANNEL_SECURITY),
            Dm.kv("color", RADIO_COLOR_SECURITY));
    }
}

// tg: code/game/machinery/telecomms/machines/server.dm:204
public partial class obj_machinery_telecomms_server_presets_common_birdstation : obj_machinery_telecomms_server_presets_common
{
    // tg: code/game/machinery/telecomms/machines/server.dm:204
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        freq_listening = Dm.list();
        return dot;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machines/bus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: telecomms): the bus mainframe and its presets. The var blocks are generated (D111).

/**
 * The bus mainframe idles and waits for hubs to relay them signals. They act
 * as junctions for the network.
 *
 * They transfer uncompressed subspace packets to processor units, and then take
 * the processed packet to a server for logging.
 *
 * Can be linked to a telecommunications hub or a broadcaster in the absence
 * of a server, at the cost of some added latency.
 */
// tg: code/game/machinery/telecomms/machines/bus.dm:11
public partial class obj_machinery_telecomms_bus : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machines/bus.dm:24
    public override async DmTask receive_information(datum_signal signal_in = null, obj_machinery_telecomms machine_from = null)
    {
        if (!(signal_in is datum_signal_subspace signal) || !is_freq_listening(signal))
            return;

        if (Dm.truthy(change_frequency) && !Dm.@in(signal.frequency, banned_frequencies))
            signal.frequency = Dm.num(change_frequency);

        if (!(machine_from is obj_machinery_telecomms_processor) && machine_from != this) // Signal must be ready (stupid assuming machine), let's send it
        {
            // send to one linked processor unit
            if (Dm.truthy(await relay_information(signal, typeof(obj_machinery_telecomms_processor))))
                return;

            // failed to send to a processor, relay information anyway
            signal.data["slow"] = Dm.add(signal.data["slow"], Dm.rand(1, 5)); // slow the signal down only slightly
        }

        // Try sending it!
        DmList try_send = Dm.list(signal.server_type, typeof(obj_machinery_telecomms_hub), typeof(obj_machinery_telecomms_broadcaster));

        float i = 0;
        foreach (Type send in Dm.each_cast<Type>(try_send))
        {
            if (i != 0)
                signal.data["slow"] = Dm.add(signal.data["slow"], Dm.rand(0, 1)); // slow the signal down only slightly
            i++;
            if (Dm.truthy(await relay_information(signal, send)))
                break;
        }

        await use_energy(idle_power_usage);
    }
}

// tg: code/game/machinery/telecomms/machines/bus.dm:72
public partial class obj_machinery_telecomms_bus_preset_four : obj_machinery_telecomms_bus
{
    // tg: code/game/machinery/telecomms/machines/bus.dm:78
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // We want to include every freely-available frequency on this one, so they
        // get processed quickly when used on-station.
        for (float i = MIN_FREQ; i <= MAX_FREQ; i += 2)
            Dm.combine(freq_listening, i);
        return dot;
    }
}

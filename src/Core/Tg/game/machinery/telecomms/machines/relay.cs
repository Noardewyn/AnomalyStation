// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machines/relay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: telecomms): the telecommunication relay and its presets. The var blocks are generated (D111).

/**
 * The relay idles until it receives information. It then passes on that information
 * depending on where it came from.
 *
 * The relay is needed in order to send information to different Z levels. It
 * must be linked with a hub, the only other machine that can send to/receive
 * from other Z levels.
 */
// tg: code/game/machinery/telecomms/machines/relay.dm:9
public partial class obj_machinery_telecomms_relay : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machines/relay.dm:24
    public override async DmTask receive_information(datum_signal signal_in = null, obj_machinery_telecomms machine_from = null)
    {
        datum_signal_subspace signal = signal_in as datum_signal_subspace;
        // Add our level and send it back
        turf relay_turf = get_turf(this);
        if (can_send(signal) && relay_turf != null)
        {
            // Relays send signals to all ZTRAIT_STATION z-levels
            if (Dm.truthy(SSmapping.level_trait(relay_turf.z, ZTRAIT_STATION)))
            {
                foreach (object z_level in Dm.each(SSmapping.levels_by_trait(ZTRAIT_STATION)))
                    Dm.combine(ref signal.levels, SSmapping.get_connected_levels(z_level));
            }
            else
                Dm.combine(ref signal.levels, SSmapping.get_connected_levels(relay_turf));
        }

        await use_energy(idle_power_usage);
    }

    /**
     * Checks to see if the relay can send/receive the signal, by checking if it's
     * on, and if it's listening to the frequency of the signal.
     *
     * Returns `TRUE` if it can listen to the signal, `FALSE` if not.
     */
    // tg: code/game/machinery/telecomms/machines/relay.dm:43
    public virtual bool can_listen_to_signal(datum_signal signal = null)
    {
        if (!Dm.truthy(on))
            return false;
        if (!is_freq_listening(signal))
            return false;
        return true;
    }

    /**
     * Checks to see if the relay can send this signal, which requires it to have
     * `broadcasting` set to `TRUE`.
     *
     * Returns `TRUE` if it can send the signal, `FALSE` if not.
     */
    // tg: code/game/machinery/telecomms/machines/relay.dm:56
    public virtual bool can_send(datum_signal signal = null)
    {
        if (!can_listen_to_signal(signal))
            return false;
        return Dm.truthy(broadcasting);
    }

    /**
     * Checks to see if the relay can receive this signal, which requires it to have
     * `receiving` set to `TRUE`.
     *
     * Returns `TRUE` if it can receive the signal, `FALSE` if not.
     */
    // tg: code/game/machinery/telecomms/machines/relay.dm:67
    public virtual bool can_receive(datum_signal signal = null)
    {
        if (!can_listen_to_signal(signal))
            return false;
        return Dm.truthy(receiving);
    }

    // tg: code/game/machinery/telecomms/machines/relay.dm:76
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (autolinkers.len > 0) //We want lateloaded presets to autolink (lateloaded aways/ruins/shuttles)
            return INITIALIZE_HINT_LATELOAD;
        return dot;
    }
}

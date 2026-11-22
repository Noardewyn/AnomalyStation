// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machines/hub.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: telecomms): the telecommunication hub, its sound loop and its preset. The var blocks are generated
// (D111).

/**
 * The HUB idles until it receives information. It then passes on that information
 * depending on where it came from.
 *
 * This is the heart of the Telecommunications Network, sending information where it
 * is needed. It mainly receives information from long-distance Relays and then sends
 * that information to be processed. Afterwards it gets the uncompressed information
 * from Servers/Buses and sends that back to the relay, to then be broadcasted.
 */
// tg: code/game/machinery/telecomms/machines/hub.dm:10
public partial class obj_machinery_telecomms_hub : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machines/hub.dm:21
    public override async DmTask receive_information(datum_signal signal = null, obj_machinery_telecomms machine_from = null)
    {
        if (!is_freq_listening(signal))
            return;

        if (machine_from is obj_machinery_telecomms_receiver)
        {
            // It's probably compressed so send it to the bus.
            await relay_information(signal as datum_signal_subspace, typeof(obj_machinery_telecomms_bus), TRUE);
        }
        else
        {
            // Send it to each relay so their levels get added...
            await relay_information(signal as datum_signal_subspace, typeof(obj_machinery_telecomms_relay));
            // Then broadcast that signal to
            await relay_information(signal as datum_signal_subspace, typeof(obj_machinery_telecomms_broadcaster));
        }

        await use_energy(idle_power_usage);
    }

    // tg: code/game/machinery/telecomms/machines/hub.dm:36
    public override void update_power()
    {
        float old_on = on;
        if (Dm.truthy(toggled))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, Dm.bitor(NOPOWER, EMPED)))))
            {
                on = FALSE;
                soundloop.stop();
            }
            else
            {
                on = TRUE;
                soundloop.start();
            }
        }
        else
            on = FALSE;
        if (old_on != on)
            update_appearance();
    }

    // tg: code/game/machinery/telecomms/machines/hub.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        soundloop = Dm.@new<datum_looping_sound_server>(this, on);
        return dot;
    }

    // tg: code/game/machinery/telecomms/machines/hub.dm:54
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref soundloop);
        return base.Destroy(force);
    }
}

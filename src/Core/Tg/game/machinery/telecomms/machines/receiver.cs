// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machines/receiver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: telecomms): the subspace receiver and its presets. The var blocks are generated (D111).

/**
 * The receiver idles and receives messages from subspace-compatible radio equipment,
 * primarily headsets. Then they just relay this information to all linked devices,
 * which would usually be through the telecommunications hub.
 *
 * Link to Processor Units in case receiver can't send to a telecommunication hub.
 */
// tg: code/game/machinery/telecomms/machines/receiver.dm:8
public partial class obj_machinery_telecomms_receiver : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machines/receiver.dm:17  (receive_signal cannot sleep, waiting-model.md 5.4: the relay may, so it runs on its own)
    public override object receive_signal(datum_signal signal = null)
    {
        Waiting.Detach(receive_signal__body(signal as datum_signal_subspace));
        return null;
    }

    private async DmTask receive_signal__body(datum_signal_subspace signal)
    {
        if (!Dm.truthy(on) || signal == null || !check_receive_level(signal) || !Dm.eq(signal.transmission_method, TRANSMISSION_SUBSPACE))
            return;
        if (!is_freq_listening(signal))
            return;

        // Make a copy of the signal so that other receivers can still receive this signal
        datum_signal_subspace signal_copy = signal.copy();

        // Signal has been received, so remove receiving levels. This list will be used later on to determine broadcasting levels.
        signal_copy.levels = Dm.list();

        // Send the signal to a hub if possible, or a bus otherwise.
        if (!Dm.truthy(await relay_information(signal_copy, typeof(obj_machinery_telecomms_hub))))
            await relay_information(signal_copy, typeof(obj_machinery_telecomms_bus));

        await use_energy(idle_power_usage);
    }

    /**
     * Checks whether the signal can be received by this receiver or not, based on
     * if it's in the signal's `levels`, or if there's a liked hub with a linked
     * relay that can receive the signal for it.
     *
     * Returns `TRUE` if it can receive the signal, `FALSE` if not.
     */
    // tg: code/game/machinery/telecomms/machines/receiver.dm:42
    public virtual bool check_receive_level(datum_signal_subspace signal = null)
    {
        if (Dm.@in(z, signal.levels))
            return true;

        foreach (obj_machinery_telecomms_hub linked_hub in Dm.each<obj_machinery_telecomms_hub>(links))
        {
            foreach (obj_machinery_telecomms_relay linked_relay in Dm.each<obj_machinery_telecomms_relay>(linked_hub.links))
            {
                if (linked_relay.can_receive(signal) && Dm.@in(linked_relay.z, signal.levels))
                    return true;
            }
        }

        return false;
    }
}

// Preset Receivers

//--PRESET RIGHT--//
// tg: code/game/machinery/telecomms/machines/receiver.dm:64
public partial class obj_machinery_telecomms_receiver_preset_right : obj_machinery_telecomms_receiver
{
    // tg: code/game/machinery/telecomms/machines/receiver.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Also add common and other freely-available radio frequencies for people
        // to have access to.
        for (float i = MIN_FREQ; i <= MAX_FREQ; i += 2)
            Dm.combine(freq_listening, i);
        return dot;
    }
}

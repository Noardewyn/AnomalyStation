// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machines/broadcaster.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: telecomms): the subspace broadcaster, the recent-message spam guard and its presets. The var blocks
// are generated (D111).

public partial class datum_controller_global_vars
{
    /// Global list of recent messages broadcasted : used to circumvent massive radio spam
    // tg: code/game/machinery/telecomms/machines/broadcaster.dm:2  GLOBAL_LIST_EMPTY(recent_messages)
    public DmList recent_messages;

    // tg: code/game/machinery/telecomms/machines/broadcaster.dm:2  (GLOBAL_MANAGED(recent_messages, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalrecent_messages()
    {
        recent_messages = Dm.list();
        Dm.append(ref gvars_datum_init_order, "recent_messages");
    }

    /// Used to make sure restarting the recent_messages list is kept in sync.
    // tg: code/game/machinery/telecomms/machines/broadcaster.dm:4  GLOBAL_VAR_INIT(message_delay, FALSE)
    public object message_delay;

    // tg: code/game/machinery/telecomms/machines/broadcaster.dm:4  (GLOBAL_MANAGED(message_delay, FALSE), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmessage_delay()
    {
        message_delay = FALSE;
        Dm.append(ref gvars_datum_init_order, "message_delay");
    }
}

/**
 * The broadcaster sends processed messages to all radio devices in the game. They
 * do not have to be headsets; intercoms and station-bounced radios suffice.
 *
 * They receive their message from a server after the message has been logged.
 */
// tg: code/game/machinery/telecomms/machines/broadcaster.dm:12
public partial class obj_machinery_telecomms_broadcaster : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machines/broadcaster.dm:21
    public override async DmTask receive_information(datum_signal signal_in = null, obj_machinery_telecomms machine_from = null)
    {
        if (!(signal_in is datum_signal_subspace signal))
            return;

        // Don't broadcast rejected signals
        if (Dm.truthy(signal.data["reject"]))
            return;
        if (!Dm.truthy(signal.data["message"]))
            return;

        string signal_message = Dm.text("[]:[]:[]", signal.frequency, signal.data["message"], signal.data["name"]);
        if (Dm.@in(signal_message, GLOB.recent_messages))
            return;

        // Prevents massive radio spam
        signal.mark_done();
        datum_signal_subspace original = signal.original;
        if (original != null && Dm.@in("compression", signal.data))
            original.data["compression"] = signal.data["compression"];

        turf current_turf = get_turf(this);
        if (current_turf != null)
            Dm.combine(ref signal.levels, SSmapping.get_connected_levels(current_turf));

        Dm.append(ref GLOB.recent_messages, signal_message);

        if (Dm.gt(signal.data["slow"], 0))
            await Dm.sleep(Dm.num(signal.data["slow"])); // simulate the network lag if necessary

        signal.broadcast();

        if (!Dm.truthy(GLOB.message_delay))
        {
            GLOB.message_delay = TRUE;
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_message_delay)), 1 * SECONDS, file: "code/game/machinery/telecomms/machines/broadcaster.dm", line: 54);
        }

        /* --- Do a snazzy animation! --- */
        flick("broadcaster_send", this);

        await use_energy(idle_power_usage);
    }

    // tg: code/game/machinery/telecomms/machines/broadcaster.dm:70
    public override object Destroy(float? force = null)
    {
        // In case message_delay is left on 1, otherwise it won't reset the list and people can't say the same thing twice anymore.
        if (Dm.truthy(GLOB.message_delay))
            GLOB.message_delay = FALSE;
        return base.Destroy(force);
    }
}

public static partial class GlobalProcs
{
    /**
     * Simply resets the message delay and the recent messages list, to ensure that
     * recent messages can be sent again. Is called on a one second timer after a
     * delay is set, from `/obj/machinery/telecomms/broadcaster/receive_information()`
     */
    // tg: code/game/machinery/telecomms/machines/broadcaster.dm:66
    public static void end_message_delay()
    {
        GLOB.message_delay = FALSE;
        GLOB.recent_messages = Dm.list();
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machines/allinone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: telecomms): the all-in-one mainframe (the away sites' and the nuclear operatives' telecomms).
// The var blocks are generated (D111).

/**
 * Basically just an empty shell for receiving and broadcasting radio messages. Not
 * very flexible, but it gets the job done.
 */
// tg: code/game/machinery/telecomms/machines/allinone.dm:5
public partial class obj_machinery_telecomms_allinone : obj_machinery_telecomms
{
    /// List of all frequencies to their name/color
    // tg: code/game/machinery/telecomms/machines/allinone.dm:15  var/static/alist/frequency_infos (one per world, built on the first read)
    public DmList frequency_infos => TgState.Current.obj_machinery_telecomms_allinone__frequency_infos ??= Dm.list(
        Dm.kv(Dm.text("[]", FREQ_SCIENCE), Dm.list(Dm.kv("name", RADIO_CHANNEL_SCIENCE), Dm.kv("color", RADIO_COLOR_SCIENCE))),
        Dm.kv(Dm.text("[]", FREQ_MEDICAL), Dm.list(Dm.kv("name", RADIO_CHANNEL_MEDICAL), Dm.kv("color", RADIO_COLOR_MEDICAL))),
        Dm.kv(Dm.text("[]", FREQ_SUPPLY), Dm.list(Dm.kv("name", RADIO_CHANNEL_SUPPLY), Dm.kv("color", RADIO_COLOR_SUPPLY))),
        Dm.kv(Dm.text("[]", FREQ_SERVICE), Dm.list(Dm.kv("name", RADIO_CHANNEL_SERVICE), Dm.kv("color", RADIO_COLOR_SERVICE))),
        Dm.kv(Dm.text("[]", FREQ_ENTERTAINMENT), Dm.list(Dm.kv("name", RADIO_CHANNEL_ENTERTAINMENT), Dm.kv("color", RADIO_COLOR_ENTERTAIMENT))),
        Dm.kv(Dm.text("[]", FREQ_COMMON), Dm.list(Dm.kv("name", RADIO_CHANNEL_COMMON), Dm.kv("color", RADIO_COLOR_COMMON))),
        Dm.kv(Dm.text("[]", FREQ_AI_PRIVATE), Dm.list(Dm.kv("name", RADIO_CHANNEL_AI_PRIVATE), Dm.kv("color", RADIO_COLOR_AI_PRIVATE))),
        Dm.kv(Dm.text("[]", FREQ_COMMAND), Dm.list(Dm.kv("name", RADIO_CHANNEL_COMMAND), Dm.kv("color", RADIO_COLOR_COMMAND))),
        Dm.kv(Dm.text("[]", FREQ_ENGINEERING), Dm.list(Dm.kv("name", RADIO_CHANNEL_ENGINEERING), Dm.kv("color", RADIO_COLOR_ENGINEERING))),
        Dm.kv(Dm.text("[]", FREQ_SECURITY), Dm.list(Dm.kv("name", RADIO_CHANNEL_SECURITY), Dm.kv("color", RADIO_COLOR_SECURITY))));

    // tg: code/game/machinery/telecomms/machines/allinone.dm:73  (receive_signal cannot sleep, waiting-model.md 5.4: the network lag may, so it runs on its own)
    public override object receive_signal(datum_signal signal = null)
    {
        Waiting.Detach(receive_signal__body(signal));
        return null;
    }

    private async DmTask receive_signal__body(datum_signal signal_in)
    {
        if (!(signal_in is datum_signal_subspace signal) || !Dm.eq(signal.transmission_method, TRANSMISSION_SUBSPACE))  // receives on subspace only
            return;
        if (!Dm.truthy(on) || !is_freq_listening(signal))  // has to be on to receive messages
            return;
        if (!Dm.truthy(syndicate) && !Dm.@in(z, signal.levels) && !Dm.@in(RADIO_NO_Z_LEVEL_RESTRICTION, signal.levels))  // has to be syndicate or on the right level
            return;

        DmList freq_info = frequency_infos[Dm.text("[]", signal.frequency)] as DmList;
        if (freq_info != null)
        {
            signal.data["frequency_name"] = freq_info["name"];
            signal.data["frequency_color"] = freq_info["color"];
        }

        // Decompress the signal and mark it done
        if (Dm.truthy(syndicate))
            signal.levels = Dm.list(0);  // Signal is broadcast to agents anywhere

        signal.data["compression"] = 0;
        signal.mark_done();
        if (Dm.gt(signal.data["slow"], 0))
            await Dm.sleep(Dm.num(signal.data["slow"])); // simulate the network lag if necessary
        signal.broadcast();
    }

    // tg: code/game/machinery/telecomms/machines/allinone.dm:96
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await attack_hand(user);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/machinery/telecomms/machines/allinone.dm:64
public partial class obj_machinery_telecomms_allinone_indestructible : obj_machinery_telecomms_allinone
{
    // tg: code/game/machinery/telecomms/machines/allinone.dm:68
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/tool_blocker, TOOL_SCREWDRIVER) and (..., TOOL_CROWBAR): the tool blocker element comes with
        // its other users (the away sites' indestructible machines)
        Dm.Deferred("code/game/machinery/telecomms/machines/allinone.dm:70", "Initialize: AddElement(/datum/element/tool_blocker), the indestructible mainframe's blocked screwdriver and crowbar (the away sites)");
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/telecomms/machines/allinone.dm:15  /obj/machinery/telecomms/allinone's var/static/alist/frequency_infos
    internal DmList obj_machinery_telecomms_allinone__frequency_infos;
}

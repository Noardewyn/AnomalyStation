// Translated from tgstation (snapshot 1d99bd7f): code/game/communications.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the radio): the radios' frequency lists (GLOB.all_radios, add_radio, remove_radio), the channel and
// frequency lists that speech reads (default_radio_channels, reserved_radio_frequencies, reserved_radio_colors, M9 slice A),
// /datum/radio_frequency (the device signals: signalers, airlocks, the C4), the root of /obj/proc/receive_signal (M5x slice 4) and
// /datum/signal. Not translated: the long comment at the top (how SSradio works).

// tg: code/game/communications.dm:214
public partial class obj : atom_movable
{
    /**
     * Proc for reacting to a received `/datum/signal`. To be implemented as needed,
     * does nothing by default.
     */
    // tg: code/game/communications.dm:214  (set waitfor = FALSE: this definition cannot sleep, a plain method, waiting-model.md
    // 5.4; an override that sleeps runs its body detached)
    public virtual object receive_signal(datum_signal signal = null)
    {
        return null;
    }
}

/* the radio controller is a confusing piece of shit and didnt work
	so i made radios not use the radio controller.
*/
public partial class datum_controller_global_vars
{
    // tg: code/game/communications.dm:66  GLOBAL_LIST_EMPTY(all_radios)
    public DmList all_radios;

    // tg: code/game/communications.dm:66  (GLOBAL_MANAGED(all_radios, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_radios()
    {
        all_radios = Dm.list();
        Dm.append(ref gvars_datum_init_order, "all_radios");
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/communications.dm:68
    public static object add_radio(obj_item_radio radio = null, object freq = null)
    {
        if (!Dm.truthy(freq) || radio == null)
            return null;
        if (!Dm.truthy(GLOB.all_radios[Dm.text("[]", freq)]))
        {
            GLOB.all_radios[Dm.text("[]", freq)] = Dm.list(radio);
            return freq;
        }

        Dm.combine((DmList)GLOB.all_radios[Dm.text("[]", freq)], radio);
        return freq;
    }

    // tg: code/game/communications.dm:78
    public static void remove_radio(obj_item_radio radio = null, object freq = null)
    {
        if (!Dm.truthy(freq) || radio == null)
            return;
        if (!Dm.truthy(GLOB.all_radios[Dm.text("[]", freq)]))
            return;

        Dm.remove((DmList)GLOB.all_radios[Dm.text("[]", freq)], radio);
    }

    // tg: code/game/communications.dm:86
    public static void remove_radio_all(obj_item_radio radio = null)
    {
        foreach (object freq in Dm.each(GLOB.all_radios))
            Dm.remove(GLOB.all_radios[Dm.text("[]", freq)] as DmList, radio);
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/communications.dm:94  GLOBAL_LIST_INIT(default_radio_channels, list(...))
    public DmList default_radio_channels;

    // tg: code/game/communications.dm:94
    public virtual void InitGlobaldefault_radio_channels()
    {
        default_radio_channels = Dm.list(
            Dm.kv(RADIO_CHANNEL_COMMON, FREQ_COMMON),
            Dm.kv(RADIO_CHANNEL_SCIENCE, FREQ_SCIENCE),
            Dm.kv(RADIO_CHANNEL_COMMAND, FREQ_COMMAND),
            Dm.kv(RADIO_CHANNEL_MEDICAL, FREQ_MEDICAL),
            Dm.kv(RADIO_CHANNEL_ENGINEERING, FREQ_ENGINEERING),
            Dm.kv(RADIO_CHANNEL_SECURITY, FREQ_SECURITY),
            Dm.kv(RADIO_CHANNEL_CENTCOM, FREQ_CENTCOM),
            Dm.kv(RADIO_CHANNEL_SYNDICATE, FREQ_SYNDICATE),
            Dm.kv(RADIO_CHANNEL_UPLINK, FREQ_UPLINK),
            Dm.kv(RADIO_CHANNEL_SUPPLY, FREQ_SUPPLY),
            Dm.kv(RADIO_CHANNEL_SERVICE, FREQ_SERVICE),
            Dm.kv(RADIO_CHANNEL_AI_PRIVATE, FREQ_AI_PRIVATE),
            Dm.kv(RADIO_CHANNEL_ENTERTAINMENT, FREQ_ENTERTAINMENT),
            Dm.kv(RADIO_CHANNEL_CTF_BLUE, FREQ_CTF_BLUE),
            Dm.kv(RADIO_CHANNEL_CTF_GREEN, FREQ_CTF_GREEN),
            Dm.kv(RADIO_CHANNEL_CTF_RED, FREQ_CTF_RED),
            Dm.kv(RADIO_CHANNEL_CTF_YELLOW, FREQ_CTF_YELLOW),
            Dm.kv(STATUS_DISPLAY_RELAY, FREQ_STATUS_DISPLAYS));
        Dm.append(ref gvars_datum_init_order, "default_radio_channels");
    }

    // tg: code/game/communications.dm:115  GLOBAL_LIST_INIT(reserved_radio_frequencies, list(...))
    public DmList reserved_radio_frequencies;

    // tg: code/game/communications.dm:115
    public virtual void InitGlobalreserved_radio_frequencies()
    {
        reserved_radio_frequencies = Dm.list(
            Dm.kv(Dm.text("[]", FREQ_CENTCOM), RADIO_CHANNEL_CENTCOM),
            Dm.kv(Dm.text("[]", FREQ_SYNDICATE), RADIO_CHANNEL_SYNDICATE),
            Dm.kv(Dm.text("[]", FREQ_UPLINK), RADIO_CHANNEL_UPLINK),
            Dm.kv(Dm.text("[]", FREQ_CTF_RED), RADIO_CHANNEL_CTF_RED),
            Dm.kv(Dm.text("[]", FREQ_CTF_BLUE), RADIO_CHANNEL_CTF_BLUE),
            Dm.kv(Dm.text("[]", FREQ_CTF_GREEN), RADIO_CHANNEL_CTF_GREEN),
            Dm.kv(Dm.text("[]", FREQ_CTF_YELLOW), RADIO_CHANNEL_CTF_YELLOW),
            Dm.kv(Dm.text("[]", FREQ_STATUS_DISPLAYS), STATUS_DISPLAY_RELAY));
        Dm.append(ref gvars_datum_init_order, "reserved_radio_frequencies");
    }

    // tg: code/game/communications.dm:126  GLOBAL_LIST_INIT(reserved_radio_colors, list(...))
    public DmList reserved_radio_colors;

    // tg: code/game/communications.dm:126
    public virtual void InitGlobalreserved_radio_colors()
    {
        reserved_radio_colors = Dm.list(
            Dm.kv(RADIO_CHANNEL_CENTCOM, "#686868"),
            Dm.kv(RADIO_CHANNEL_SYNDICATE, "#6d3f40"),
            Dm.kv(RADIO_CHANNEL_UPLINK, "#6d3f40"),
            Dm.kv(RADIO_CHANNEL_CTF_RED, "#ff0000"),
            Dm.kv(RADIO_CHANNEL_CTF_BLUE, "#0000ff"),
            Dm.kv(RADIO_CHANNEL_CTF_GREEN, "#00ff00"),
            Dm.kv(RADIO_CHANNEL_CTF_YELLOW, "#d1ba22"),
            Dm.kv(STATUS_DISPLAY_RELAY, "#00ff99"));
        Dm.append(ref gvars_datum_init_order, "reserved_radio_colors");
    }
}

// tg: code/game/communications.dm:137
public partial class datum_radio_frequency : datum
{
    // tg: code/game/communications.dm:143
    [DmParams("freq")]
    public override async DmTask New(params object[] __extra)
    {
        frequency = Dm.arg(__extra, 0);
    }

    //If range > 0, only post to devices on the same z_level and within range
    //Use range = -1, to restrain to the same z_level without limiting range
    // tg: code/game/communications.dm:148
    public virtual void post_signal(obj source = null, datum_signal signal = null, object filter = null, object range = null)
    {
        // Ensure the signal's data is fully filled
        signal.source = source;
        signal.frequency = Dm.num(frequency);

        //Apply filter to the signal. If none supply, broadcast to every devices
        //_default channel is always checked
        DmList filter_list;

        if (Dm.truthy(filter))
            filter_list = Dm.list(filter, "_default");
        else
            filter_list = devices;

        //If checking range, find the source turf
        turf start_point = null;
        if (Dm.truthy(range))
        {
            start_point = get_turf(source);
            if (start_point == null)
                return;
        }

        //Send the data
        foreach (object current_filter in Dm.each(filter_list.Copy()))
        {
            foreach (datum_weakref device_ref in Dm.each_cast<datum_weakref>((devices[current_filter] as DmList)?.Copy()))
            {
                obj device = device_ref.resolve() as obj;
                if (device == null)
                {
                    Dm.remove((DmList)devices[current_filter], device_ref);
                    continue;
                }
                if (device == source)
                    continue;
                if (Dm.truthy(range))
                {
                    turf end_point = get_turf(device);
                    if (end_point == null)
                        continue;
                    if (!Dm.eq(start_point.z, end_point.z) || (Dm.num(range) > 0 && Dm.num(get_dist(start_point, end_point)) > Dm.num(range)))
                        continue;
                }
                device.receive_signal(signal);
                CHECK_TICK();
            }
        }
    }

    /// Handles adding a listener to the radio frequency.
    // tg: code/game/communications.dm:184
    public virtual object add_listener(obj device = null, object filter = null)
    {
        if (!Dm.truthy(filter))
            filter = "_default";

        datum_weakref new_listener = WEAKREF(device) as datum_weakref;
        if (new_listener == null)
        {
            stack_trace("null, non-datum, or qdeleted device", "code/game/communications.dm", 194);
            return null;
        }
        DmList devices_line = devices[filter] as DmList;
        if (devices_line == null)
            devices[filter] = devices_line = Dm.list();
        Dm.append(ref devices_line, new_listener);
        return null;
    }

    /// Handles removing a listener from this radio frequency.
    // tg: code/game/communications.dm:197
    public virtual void remove_listener(obj device = null)
    {
        foreach (object devices_filter in Dm.each(devices.Copy()))
        {
            DmList devices_line = devices[devices_filter] as DmList;
            if (devices_line == null)
            {
                Dm.remove(devices, devices_filter);
                continue; // tg reads .len of the null line next, a runtime that ends the loop's step
            }
            Dm.remove(devices_line, WEAKREF(device));
            if (devices_line.len == 0)
                Dm.remove(devices, devices_filter);
        }
    }
}

// tg: code/game/communications.dm:218
public partial class datum_signal : datum
{
    /// The source of this signal.
    // tg: code/game/communications.dm:220
    public obj source;

    // tg: code/game/communications.dm:233
    [DmParams("data", "transmission_method", "logging_data")]
    public override async DmTask New(params object[] __extra)
    {
        object data = Dm.arg(__extra, 0);
        object transmission_method = Dm.arg(__extra, 1) ?? TRANSMISSION_RADIO;
        object logging_data = Dm.arg(__extra, 2);
        this.data = data as DmList ?? Dm.list();
        this.transmission_method = transmission_method;
        this.logging_data = logging_data;
    }
}

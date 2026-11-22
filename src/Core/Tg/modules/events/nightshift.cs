// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/nightshift.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the night shift event (the station's APCs dim their lights for about twenty minutes, held off while
// the alert is red or above; only with the enable_night_shifts config flag). The var blocks are generated (D111).

// tg: code/modules/events/nightshift.dm:1
public partial class datum_round_event_control_nightshift : datum_round_event_control
{
    // tg: code/modules/events/nightshift.dm:10
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_enable_night_shifts))))
            max_occurrences = 0;
    }
}

// tg: code/modules/events/nightshift.dm:15
public partial class datum_round_event_nightshift : datum_round_event
{
    // tg: code/modules/events/nightshift.dm:27
    public override async DmTask announce__proc(object fake = null)
    {
        update_nightshift(active: TRUE, announce: TRUE);
    }

    // tg: code/modules/events/nightshift.dm:30
    public override async DmTask tick()
    {
        float emergency = Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED ? TRUE : FALSE;
        if (nightshift_disabled != emergency)
        {
            nightshift_disabled = emergency;
            if (Dm.truthy(emergency))
                update_nightshift(active: FALSE, resume: TRUE);
            else
                update_nightshift(active: TRUE, resume: TRUE);
        }

        update_machines();
    }

    // tg: code/modules/events/nightshift.dm:41
    public override async DmTask end()
    {
        update_nightshift(active: FALSE, announce: TRUE);
    }

    ///Called several times, to start & stop nightlights including during red alert/de-red alerting.
    // tg: code/modules/events/nightshift.dm:45
    public virtual void update_nightshift(float? active = null, float? resume = null, float? announce = null)
    {
        resume ??= FALSE;
        announce ??= FALSE;
        currentrun = (SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc)) as DmList)?.Copy();

        if (Dm.truthy(announce))
        {
            if (Dm.truthy(active))
                send_announcement("Good evening, crew. To reduce power consumption and stimulate the circadian rhythms of some species, all of the lights aboard the station have been dimmed for the night.");
            else
                send_announcement("Good morning, crew. As it is now day time, all of the lights aboard the station have been restored to their former brightness.");
        }

        if (Dm.truthy(resume))
        {
            if (Dm.truthy(active))
                send_announcement("Restoring night lighting configuration to normal operation.");
            else
                send_announcement("Disabling night lighting: Station is in a state of emergency.");
        }

        update_machines();
    }

    ///Called on process that will slowly update all APCs to be nightlight
    // tg: code/modules/events/nightshift.dm:63
    public virtual void update_machines()
    {
        foreach (obj_machinery_power_apc APC in Dm.each_cast<obj_machinery_power_apc>(currentrun?.Copy()))
        {
            Dm.remove(ref currentrun, APC);
            if (APC.area != null && Dm.@in(APC.area.type, GLOB.the_station_areas))
                APC.set_nightshift(!Dm.truthy(nightshift_disabled) ? TRUE : FALSE);
            if (TICK_CHECK())
                return;
        }
    }

    ///Custom messages sent throughout the event that we'll do here, instead of using the `announce` proc that's only at the start.
    // tg: code/modules/events/nightshift.dm:72
    public virtual void send_announcement(object message = null)
    {
        priority_announce(
            text: message,
            sound: "sound/announcer/notice/notice2.ogg",
            sender_override: "Automated Lighting System Announcement",
            color_override: "grey");
    }
}

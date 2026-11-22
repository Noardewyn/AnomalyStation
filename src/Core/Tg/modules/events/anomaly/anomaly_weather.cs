// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/anomaly/anomaly_weather.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the weather anomaly's events (the plain and the thundering one). Not here: the admin setup of a forced event (the weather
// type and thunder chance lists, /datum/event_admin_setup, as for the other events). The var blocks are generated (D111).

// tg: code/modules/events/anomaly/anomaly_weather.dm:1
public partial class datum_round_event_control_anomaly_anomaly_weather : datum_round_event_control_anomaly
{
    // tg: code/modules/events/anomaly/anomaly_weather.dm:19
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        // weathers have some funky global state that may break if multiple are running. better safe than sorry.
        return Dm.truthy(base.can_spawn_event(players_amt, allow_magic)) && !Dm.truthy(Dm.length(SSweather.processing));
    }
}

// tg: code/modules/events/anomaly/anomaly_weather.dm:23
public partial class datum_round_event_anomaly_anomaly_weather : datum_round_event_anomaly
{
    // tg: code/modules/events/anomaly/anomaly_weather.dm:31
    public override async DmTask announce__proc(object fake = null)
    {
        if (impact_area == null)
            impact_area = placer.findValidArea() as area;
        priority_announce(Dm.text("Barometric anomaly detected on [] [].", ANOMALY_ANNOUNCE_HARMFUL_TEXT, impact_area.name), "Anomaly Alert");
    }

    // tg: code/modules/events/anomaly/anomaly_weather.dm:36
    public override object make_anomaly(turf anomaly_turf = null)
    {
        return Dm.@new(anomaly_path, anomaly_turf, null, null, forced_weather_type, forced_thunder_chance);
    }
}

// tg: code/modules/events/anomaly/anomaly_weather.dm:50
public partial class datum_round_event_anomaly_anomaly_weather_thundering : datum_round_event_anomaly_anomaly_weather
{
    // tg: code/modules/events/anomaly/anomaly_weather.dm:55
    public override async DmTask announce__proc(object fake = null)
    {
        if (impact_area == null)
            impact_area = placer.findValidArea() as area;
        priority_announce(Dm.text("Severe barometric anomaly detected on [] [].", ANOMALY_ANNOUNCE_DANGEROUS_TEXT, impact_area.name), "Anomaly Alert");
    }
}

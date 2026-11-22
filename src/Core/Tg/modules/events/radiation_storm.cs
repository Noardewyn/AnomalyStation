// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/radiation_storm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file, the radiation storm event (the station's weather: datums/weather/weather_types/radiation_storm.dm).
// The var blocks are generated (D111).

// tg: code/modules/events/radiation_storm.dm:1
public partial class datum_round_event_control_radiation_storm : datum_round_event_control
{
    // tg: code/modules/events/radiation_storm.dm:10
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (max_occurrences > 0 && weight > 0 && Dm.truthy(check_holidays(CHERNOBYL_ANNIVERSARY)))
        {
            weight *= 2;
            max_occurrences += 2;
        }
    }
}

// tg: code/modules/events/radiation_storm.dm:16
public partial class datum_round_event_radiation_storm : datum_round_event
{
    // tg: code/modules/events/radiation_storm.dm:18
    public override async DmTask<object> setup()
    {
        start_when = 3;
        end_when = start_when + 1;
        announce_when = 1;
        return null;
    }

    // tg: code/modules/events/radiation_storm.dm:23
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("High levels of radiation detected near the station. Maintenance is best shielded from radiation.", "Anomaly Alert", ANNOUNCER_RADIATION);
        //sound not longer matches the text, but an audible warning is probably good
    }

    // tg: code/modules/events/radiation_storm.dm:27
    public override async DmTask<object> start()
    {
        SSweather.run_weather(typeof(datum_weather_rad_storm));
        return null;
    }
}

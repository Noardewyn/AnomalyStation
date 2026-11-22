// Translated from tgstation (snapshot 1d99bd7f): code/datums/weather/weather_types/snow_storm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file, the icy levels' snowstorm (colder than their air, through any clothing) and the endless storm, with its
// looping sound per area. The var blocks are generated (D111).

// tg: code/datums/weather/weather_types/snow_storm.dm:1
public partial class datum_weather_snow_storm : datum_weather
{
    // tg: code/datums/weather/weather_types/snow_storm.dm:36
    public override DmList get_playlist_ref() => GLOB.snowstorm_sounds;

    // tg: code/datums/weather/weather_types/snow_storm.dm:39
    public override object start()
    {
        GLOB.snowstorm_sounds.Cut(); // it's passed by ref
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
            GLOB.snowstorm_sounds[impacted_area] = typeof(datum_looping_sound_snowstorm);
        return base.start();
    }

    // tg: code/datums/weather/weather_types/snow_storm.dm:45
    public override object end()
    {
        GLOB.snowstorm_sounds.Cut();
        return base.end();
    }
}

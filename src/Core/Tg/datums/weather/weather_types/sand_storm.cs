// Translated from tgstation (snapshot 1d99bd7f): code/datums/weather/weather_types/sand_storm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file, the sandstorm of the sand levels and its harmless sandfall, with their winds' looping sounds per area.
// The var blocks are generated (D111).

//Darude sandstorm starts playing
// tg: code/datums/weather/weather_types/sand_storm.dm:2
public partial class datum_weather_sand_storm : datum_weather
{
    // tg: code/datums/weather/weather_types/sand_storm.dm:30
    public override DmList get_playlist_ref() => GLOB.sand_storm_sounds;

    // tg: code/datums/weather/weather_types/sand_storm.dm:33
    public override object telegraph(DmList weather_data = null)
    {
        GLOB.sand_storm_sounds.Cut();
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
            GLOB.sand_storm_sounds[impacted_area] = typeof(datum_looping_sound_weak_outside_ashstorm);
        return base.telegraph(weather_data);
    }

    // tg: code/datums/weather/weather_types/sand_storm.dm:39
    public override object start()
    {
        GLOB.sand_storm_sounds.Cut();
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
            GLOB.sand_storm_sounds[impacted_area] = typeof(datum_looping_sound_active_outside_ashstorm);
        return base.start();
    }

    // tg: code/datums/weather/weather_types/sand_storm.dm:45
    public override object wind_down()
    {
        GLOB.sand_storm_sounds.Cut();
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
            GLOB.sand_storm_sounds[impacted_area] = typeof(datum_looping_sound_weak_outside_ashstorm);
        return base.wind_down();
    }

    // tg: code/datums/weather/weather_types/sand_storm.dm:51
    public override async DmTask weather_act_mob(mob_living victim = null)
    {
        await victim.adjust_brute_loss(5, required_bodytype: BODYTYPE_ORGANIC);
        await base.weather_act_mob(victim);
    }
}

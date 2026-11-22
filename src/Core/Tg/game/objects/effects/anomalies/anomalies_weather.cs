// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_weather.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file, the weather anomaly: it brings rain, snow or sand (or a thunderstorm, the thundering one) indoors over
// its area and the small areas beside it, winds the weather down when neutralized and blows things away when it detonates. The var
// blocks are generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_weather.dm:1
public partial class obj_effect_anomaly_weather : obj_effect_anomaly
{
    // tg: code/game/objects/effects/anomalies/anomalies_weather.dm:13
    [DmParams("mapload", "new_lifespan", "drops_core", "forced_anomaly_type", "forced_thunder_chance")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        var forced_anomaly_type = Dm.arg(__extra, 2) as Type ?? weather_type;
        object forced_thunder_chance = Dm.arg(__extra, 3) ?? thunder_chance;
        object dot = base.Initialize(mapload, __extra);
        if (impact_area == null) // tg's base asked for a qdel (no area): tg would runtime on impact_area below
            return dot;
        Waiting.NoSleep(add_atom_colour(COLOR_MATRIX_INVERT(), FIXED_COLOUR_PRIORITY));
        weather_type = forced_anomaly_type ?? select_weather() as Type;
        thunder_chance = Dm.num(forced_thunder_chance);
        active_weathers = Dm.list();
        float telegraph = Dm.num(lifespan) / 5; // 1/5th of the time is dedicated to telegraphing, to give people time to find and defuse it
        float end_dur = Dm.num(lifespan) / 15; // then 1/15th of the time is dedicated to winding down
        float total_dur = Dm.num(lifespan) - telegraph - end_dur;
        DmList affected_areas = Dm.list(impact_area);
        float num_turfs = Dm.length(impact_area.get_turfs_from_all_zlevels());
        foreach (object spread_dir in Dm.each(GLOB.alldirs))
        {
            var nearby = find_adjacent_impacted_area(spread_dir) as area;
            // prevents nearby the central hallway from basically always being included
            if (nearby == null || Dm.length(nearby.get_turfs_from_all_zlevels()) > num_turfs * 1.5f)
                continue;
            Dm.combine(affected_areas, nearby);
        }

        datum_weather weather = SSweather?.run_weather(
            weather_type,
            z,
            Dm.list(
                Dm.kv(WEATHER_FORCED_AREAS, affected_areas),
                Dm.kv(WEATHER_FORCED_FLAGS, Dm.bitor(Dm.bitor(Dm.bitor(Dm.initial(weather_type, (datum_weather p) => p.weather_flags), WEATHER_INDOORS), WEATHER_THUNDER), WEATHER_STRICT_ALERT)),
                Dm.kv(WEATHER_FORCED_THUNDER, thunder_chance),
                Dm.kv(WEATHER_FORCED_TELEGRAPH, telegraph),
                Dm.kv(WEATHER_FORCED_END, end_dur),
                Dm.kv(WEATHER_FORCED_DURATION, total_dur)));
        if (weather == null) // run_weather crashed (or a world without SSweather): tg would runtime on the null below
            return dot;
        RegisterSignal(weather, COMSIG_QDELETING, nameof(clear_ref));
        Dm.append(active_weathers, weather);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_weather.dm:44
    public virtual void clear_ref(datum_weather weather_datum = null)
    {
        // SIGNAL_HANDLER
        Dm.remove(active_weathers, weather_datum);
        UnregisterSignal(weather_datum, COMSIG_QDELETING);
    }

    // tg: code/game/objects/effects/anomalies/anomalies_weather.dm:49
    public virtual object select_weather()
    {
        return pick(
            typeof(datum_weather_particle_rain_storm),
            typeof(datum_weather_snow_storm),
            typeof(datum_weather_sand_storm));
    }

    /// steps outward from src to find an area that is not the impact_area.
    // tg: code/game/objects/effects/anomalies/anomalies_weather.dm:57
    public virtual object find_adjacent_impacted_area(object check_dir = null)
    {
        float limit = 9;
        var next_turf = get_step(this, Dm.num(check_dir)) as turf;
        while (next_turf != null && next_turf.loc == impact_area && limit > 0)
        {
            next_turf = get_step(next_turf, Dm.num(check_dir)) as turf;
            if (next_turf == null)
                return null;
            limit -= 1;
        }
        return next_turf?.loc;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_weather.dm:67
    public override async DmTask anomalyNeutralize()
    {
        foreach (datum_weather weather_datum in Dm.each_cast<datum_weather>(active_weathers.Copy()))
            weather_datum?.wind_down();
        await base.anomalyNeutralize();
    }

    // tg: code/game/objects/effects/anomalies/anomalies_weather.dm:72
    public override async DmTask detonate()
    {
        playsound(this, Dm.resource("sound/effects/magic/repulse.ogg"), 100, TRUE);
        foreach (atom_movable repulsed in Dm.each<atom_movable>(range(this, 5)))
        {
            if (repulsed == this || Dm.truthy(repulsed.anchored))
                continue;
            var throwtarget = get_edge_target_turf(this, get_dir(this, get_step_away(repulsed, this))) as atom;
            Waiting.Detach(repulsed.safe_throw_at(throwtarget, 6, 2, this, force: MOVE_FORCE_EXTREMELY_STRONG));
        }
    }

    // tg: code/game/objects/effects/anomalies/anomalies_weather.dm:80
    public override object Destroy(float? force = null)
    {
        foreach (datum_weather weather_datum in Dm.each_cast<datum_weather>(active_weathers?.Copy()))
            clear_ref(weather_datum);
        return base.Destroy(force);
    }
}

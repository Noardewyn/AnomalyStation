// Translated from tgstation (snapshot 1d99bd7f): code/datums/weather/weather_types/ash_storm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file, the lava levels' ash storm (fire through anything but fireproof gear, thunder, the dug basalt refilled
// after it) and the harmless emberfall, with their particles' severity and the storm's looping sounds per area. The var blocks are
// generated (D111).

/// Ash storms happen frequently on lavaland. They heavily obscure vision, and cause high fire damage to anyone caught outside.
// tg: code/datums/weather/weather_types/ash_storm.dm:2
public partial class datum_weather_particle_ash_storm : datum_weather_particle
{
    // tg: code/datums/weather/weather_types/ash_storm.dm:85
    public override DmList get_playlist_ref() => GLOB.ash_storm_sounds;

    // tg: code/datums/weather/weather_types/ash_storm.dm:88
    public override object telegraph(DmList weather_data = null)
    {
        foreach (area impacted_area in Dm.each_cast<area>(impacted_areas))
        {
            if (Dm.truthy(impacted_area.outdoors))
            {
                weak_sounds[impacted_area] = typeof(datum_looping_sound_weak_outside_ashstorm);
                strong_sounds[impacted_area] = typeof(datum_looping_sound_active_outside_ashstorm);
            }
            else
            {
                weak_sounds[impacted_area] = typeof(datum_looping_sound_weak_inside_ashstorm);
                strong_sounds[impacted_area] = typeof(datum_looping_sound_active_inside_ashstorm);
            }
        }
        //We modify this list instead of setting it to weak/stron sounds in order to preserve things that hold a reference to it
        //It's essentially a playlist for a bunch of components that chose what sound to loop based on the area a player is in
        Dm.append(GLOB.ash_storm_sounds, weak_sounds);
        return base.telegraph(weather_data);
    }

    // tg: code/datums/weather/weather_types/ash_storm.dm:102
    public override object start()
    {
        Dm.remove(GLOB.ash_storm_sounds, weak_sounds);
        Dm.append(GLOB.ash_storm_sounds, strong_sounds);
        return base.start();
    }

    // tg: code/datums/weather/weather_types/ash_storm.dm:107
    public override object wind_down()
    {
        Dm.remove(GLOB.ash_storm_sounds, strong_sounds);
        Dm.append(GLOB.ash_storm_sounds, weak_sounds);
        return base.wind_down();
    }

    // tg: code/datums/weather/weather_types/ash_storm.dm:112
    public override object recursive_weather_protection_check(atom to_check = null)
    {
        object dot = base.recursive_weather_protection_check(to_check);
        if (Dm.truthy(dot) || !ishuman(to_check))
            return dot;
        var human_to_check = (mob_living_carbon_human)to_check;
        if (Dm.num(human_to_check.get_thermal_protection()) >= FIRE_IMMUNITY_MAX_TEMP_PROTECT)
            return TRUE;
        return dot;
    }

    // tg: code/datums/weather/weather_types/ash_storm.dm:120
    public override async DmTask weather_act_mob(mob_living victim = null)
    {
        await victim.adjust_fire_loss(4, required_bodytype: BODYTYPE_ORGANIC);
        await base.weather_act_mob(victim);
    }

    // tg: code/datums/weather/weather_types/ash_storm.dm:124
    public override object end()
    {
        Dm.remove(GLOB.ash_storm_sounds, weak_sounds);
        Dm.remove(GLOB.ash_storm_sounds, strong_sounds);
        foreach (turf_open_misc_asteroid_basalt basalt in Dm.each_cast<turf_open_misc_asteroid_basalt>(GLOB.dug_up_basalt.Copy()))
        {
            if (!Dm.@in(basalt.loc, impacted_areas) || !Dm.@in(basalt.z, impacted_z_levels))
                continue;
            Waiting.NoSleep(basalt.refill_dug());
        }
        return base.end();
    }
}

// tg: code/datums/weather/weather_types/ash_storm.dm:42
public partial class particles_weather_ash_storm : particles_weather
{
    // tg: code/datums/weather/weather_types/ash_storm.dm:59
    public override void animate_severity(float severity = 0, float wind_sign = 0)
    {
        base.animate_severity(severity, wind_sign);
        if (gravity is DmList gravity_list && gravity_list.len > 1)
            gravity_list[2] = gravity_power * severity;
        else
            gravity = Dm.list(gravity is DmList g && g.len > 0 ? g[1] : 0, gravity_power * severity, 0.5f);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/sand.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The beach's sand and coast (Lavaland's beach biodome) with their lazy fishing spot and its loot from an explosion. The var
// blocks are generated.

// tg: code/game/turfs/open/sand.dm:1
public partial class turf_open_misc_beach : turf_open_misc
{
    // tg: code/game/turfs/open/sand.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_lazy_fishing(typeof(datum_fish_source_sand));
        return dot;
    }

    // tg: code/game/turfs/open/sand.dm:18
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.truthy(fish_source))
            ((datum_fish_source)GLOB.preset_fish_sources[fish_source]).spawn_reward_from_explosion(this, severity);
        return FALSE;
    }

    // tg: code/game/turfs/open/sand.dm:23
    public override void add_footprint(mob_living_carbon_human walker = null, object movement_direction = null)
    {
        if (!Dm.truthy(SSmapping.level_trait(z, ZTRAIT_SANDSTORM)))
        {
            base.add_footprint(walker, movement_direction);
            return;
        }

        // if an active sand storm affecting this turf is currently in its main or wind down stage, skip footprint creation
        foreach (datum_weather_sand_storm active_weather in Dm.each<datum_weather_sand_storm>(SSweather.processing))
        {
            if (!Dm.eq(active_weather.stage, MAIN_STAGE) && !Dm.eq(active_weather.stage, WIND_DOWN_STAGE))
                continue;
            if (!Dm.@in(loc, active_weather.impacted_areas))
                continue;
            return;
        }

        base.add_footprint(walker, movement_direction);
        // when a sand storm enters its main stage, clear all of our footprints
        foreach (Type sand_type in Dm.each<Type>(typesof(typeof(datum_weather_sand_storm))))
            RegisterSignal(SSdcs, COMSIG_WEATHER_START(sand_type), nameof(sand_clear_footprints), @override: TRUE);
    }

    // tg: code/game/turfs/open/sand.dm:40
    [SignalHandler]
    public virtual void sand_clear_footprints(datum source = null, datum_weather storm = null)
    {
        if (!Dm.@in(loc, storm.impacted_areas))
            return;

        clear_footprints();
        foreach (Type sand_type in Dm.each<Type>(typesof(typeof(datum_weather_sand_storm))))
            UnregisterSignal(SSdcs, COMSIG_WEATHER_START(sand_type));
    }
}

// tg: code/game/turfs/open/sand.dm:51
public partial class turf_open_misc_beach_sand : turf_open_misc_beach
{
    // tg: code/game/turfs/open/sand.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(15))
            icon_state = Dm.text("sand[]", Dm.rand(1, 4));
        return dot;
    }
}

// tg: code/game/turfs/open/sand.dm:64
public partial class turf_open_misc_beach_coast : turf_open_misc_beach
{
    // tg: code/game/turfs/open/sand.dm:74
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_MESSAGE_IN_A_BOTTLE_LOCATION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/turfs/open/sand.dm:78
    public override object break_tile()
    {
        object dot = base.break_tile();
        icon_state = "beach";
        return dot;
    }
}

// tg: code/game/turfs/open/sand.dm:83
public partial class turf_open_misc_beach_coast_corner : turf_open_misc_beach_coast
{
    // tg: code/game/turfs/open/sand.dm:87
    public override object break_tile()
    {
        object dot = base.break_tile();
        icon_state = "beach-corner";
        return dot;
    }
}

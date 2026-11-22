// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/weather.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file: the running weather's mob, turf and thunder tasks, the random weather of the levels whose
// trait a weather type targets, run_weather (the events, the admins, the mining weather monitor) and the lookups the barometer,
// the weather announcer and the area icon read. The particle holders per level are kept as tg keeps them; the client draws the
// weather from SSweather.processing (gaps E2a: the weather message), not from the holders' vis_contents. The var block is generated.

/// Used for all kinds of weather, ex. lavaland ash storms.
// tg: code/controllers/subsystem/weather.dm:2  SUBSYSTEM_DEF(weather)
public partial class datum_controller_subsystem_weather : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(weather) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSweather) expanded inline.
    // tg: code/controllers/subsystem/weather.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSweather != this)
        {
            if (SSweather is datum_controller_subsystem_weather)
            {
                Recover();
                qdel(SSweather);
            }
            SSweather = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/weather.dm:18
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        // process active weather
        foreach (datum_weather weather_event in Dm.each_cast<datum_weather>(processing.Copy()))
        {
            if (!Dm.truthy(weather_event.subsystem_tasks.len))
                continue;

            if (weather_event is datum_weather_particle particle_event)
                particle_event.process_particles();

            if (weather_event.stage != MAIN_STAGE)
                continue;

            if (Dm.eq(weather_event.subsystem_tasks[(int)weather_event.task_index], SSWEATHER_MOBS))
            {
                if (!Dm.truthy(resumed))
                    weather_event.current_mobs = GLOB.mob_living_list.Copy();
                DmList current_mobs_cache = weather_event.current_mobs; // cache for performance
                while (current_mobs_cache.len > 0)
                {
                    var target = current_mobs_cache[(int)current_mobs_cache.len] as mob_living;
                    current_mobs_cache.len--;
                    if (QDELETED(target))
                        continue;
                    if (Dm.truthy(weather_event.can_weather_act_mob(target)))
                        await weather_event.weather_act_mob(target);
                    if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                        return;
                }
                resumed = FALSE;
                weather_event.task_index = WRAP_UP(weather_event.task_index, weather_event.subsystem_tasks.len);
            }

            if (Dm.eq(weather_event.subsystem_tasks[(int)weather_event.task_index], SSWEATHER_TURFS))
            {
                if (!Dm.truthy(resumed))
                    weather_event.turf_iteration = ROUND_PROB(weather_event.weather_turfs_per_tick);
                while (weather_event.turf_iteration > 0)
                {
                    weather_event.turf_iteration--;
                    var selected_turf = weather_event.pick_turf() as turf;
                    if (selected_turf != null && Dm.truthy(weather_event.can_weather_act_turf(selected_turf)))
                        await weather_event.weather_act_turf(selected_turf as turf_open);
                    if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK, inline (rules 11.3)
                        return;
                }
                resumed = FALSE;
                weather_event.task_index = WRAP_UP(weather_event.task_index, weather_event.subsystem_tasks.len);
            }

            if (Dm.eq(weather_event.subsystem_tasks[(int)weather_event.task_index], SSWEATHER_THUNDER))
            {
                if (!Dm.truthy(resumed))
                    weather_event.thunder_iteration = ROUND_PROB(weather_event.thunder_turfs_per_tick);
                while (weather_event.thunder_iteration > 0)
                {
                    weather_event.thunder_iteration--;
                    var selected_turf = weather_event.pick_turf() as turf;
                    if (selected_turf != null && Dm.truthy(weather_event.can_weather_act_turf(selected_turf)))
                        await weather_event.thunder_act_turf(selected_turf as turf_open);
                    if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK, inline (rules 11.3)
                        return;
                }
                resumed = FALSE;
                weather_event.task_index = WRAP_UP(weather_event.task_index, weather_event.subsystem_tasks.len);
            }
        }

        // start random weather on relevant levels
        foreach (object z in Dm.each(eligible_zlevels.Copy()))
        {
            var possible_weather = eligible_zlevels[z] as DmList;
            var weather_event = pick_weight(possible_weather) as Type;
            run_weather(weather_event, Dm.list(text2num(z)));
            Dm.remove(eligible_zlevels, z);
            float randTime = rand(5 * MINUTES, 10 * MINUTES);
            next_hit_by_zlevel[Dm.text("[]", z)] = _addtimer(Dm.@new<datum_callback>(this, nameof(make_eligible), z, possible_weather),
                randTime + Dm.initial(weather_event, (datum_weather p) => p.weather_duration_upper), Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE),
                file: "code/controllers/subsystem/weather.dm", line: 74);
        }
    }

    // tg: code/controllers/subsystem/weather.dm:76
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        generate_holders(1, world.maxz);
        foreach (Type weather in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_weather)) as DmList))
        {
            float probability = Dm.initial(weather, (datum_weather p) => p.probability);
            string target_trait = Dm.initial(weather, (datum_weather p) => p.target_trait);

            // any weather with a probability set may occur at random
            if (Dm.truthy(probability))
            {
                foreach (object z in Dm.each(SSmapping.levels_by_trait(target_trait) as DmList))
                {
                    string z_key = Dm.text("[]", z);
                    if (eligible_zlevels[z_key] is not DmList) // LAZYINITLIST
                        eligible_zlevels[z_key] = Dm.list();
                    ((DmList)eligible_zlevels[z_key])[weather] = probability;
                }
            }
        }
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/weather.dm:90
    public virtual void generate_holders(object z_min = null, object z_max = null)
    {
        for (int i = (int)Dm.num(z_min); i <= Dm.num(z_max); i++)
            z_particles[i] = Dm.alist();
    }

    // tg: code/controllers/subsystem/weather.dm:94
    public virtual void add_weather_objects(DmList new_objects = null, object z_level = null)
    {
        var holders_by_plane = z_particles[z_level] as DmList;
        if (holders_by_plane == null)
        {
            stack_trace(Dm.text("No particle holder slot found for z level []", z_level), "code/controllers/subsystem/weather.dm", 97);
            return;
        }

        foreach (obj_effect_abstract_weather_holder new_object in Dm.each_cast<obj_effect_abstract_weather_holder>(new_objects))
        {
            var holder = holders_by_plane[new_object.plane] as obj_effect_abstract_weather_holder;
            if (holder == null)
            {
                holder = Dm.@new<obj_effect_abstract_weather_holder_z_level>();
                holder.plane = new_object.plane;
                holders_by_plane[new_object.plane] = new_object;
            }

            Dm.append(holder.vis_contents, new_object);
        }
    }

    // tg: code/controllers/subsystem/weather.dm:111
    public virtual void remove_weather_objects(DmList old_objects = null, object z_level = null)
    {
        var holders_by_plane = z_particles[z_level] as DmList;
        if (holders_by_plane == null)
        {
            CRASH(Dm.text("No particle holder slot found for z level []", z_level));
            return;
        }

        foreach (object plane in Dm.each(holders_by_plane))
        {
            var holder = holders_by_plane[plane] as obj_effect_abstract_weather_holder;
            if (holder != null)
                Dm.remove(holder.vis_contents, old_objects);
        }
    }

    // tg: code/controllers/subsystem/weather.dm:120
    public virtual void update_z_level(datum_space_level level = null)
    {
        object z = level.z_value;
        foreach (Type weather in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_weather)) as DmList))
        {
            float probability = Dm.initial(weather, (datum_weather p) => p.probability);
            string target_trait = Dm.initial(weather, (datum_weather p) => p.target_trait);
            if (Dm.truthy(probability) && Dm.truthy(level.traits[target_trait]))
            {
                string z_key = Dm.text("[]", z);
                if (eligible_zlevels[z_key] is not DmList) // LAZYINITLIST
                    eligible_zlevels[z_key] = Dm.list();
                ((DmList)eligible_zlevels[z_key])[weather] = probability;
            }
        }
    }

    // tg: code/controllers/subsystem/weather.dm:130
    public virtual datum_weather run_weather(object weather_datum_type = null, object z_levels = null, DmList weather_data = null)
    {
        if (istext(weather_datum_type))
        {
            foreach (Type weather in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_weather)) as DmList))
            {
                if (Dm.eq(Dm.initial(weather, (datum_weather p) => p.name), weather_datum_type))
                {
                    weather_datum_type = weather;
                    break;
                }
            }
        }
        if (!(weather_datum_type is Type weather_type && ispath(weather_type, typeof(datum_weather))))
        {
            CRASH(Dm.text("run_weather called with invalid weather_datum_type: []", weather_datum_type ?? "null"));
            return null;
        }

        if (z_levels == null)
            z_levels = SSmapping.levels_by_trait(Dm.initial(weather_type, (datum_weather p) => p.target_trait));
        else if (isnum(z_levels))
            z_levels = Dm.list(z_levels);
        else if (!islist(z_levels))
        {
            CRASH(Dm.text("run_weather called with invalid z_levels: []", z_levels ?? "null"));
            return null;
        }

        var weather_datum = Waiting.NoSleep(Dm.new_async(weather_type, z_levels, weather_data)) as datum_weather;
        weather_datum.telegraph(weather_data);
        return weather_datum;
    }

    // tg: code/controllers/subsystem/weather.dm:152
    public virtual void make_eligible(object z = null, object possible_weather = null)
    {
        eligible_zlevels[z] = possible_weather;
        next_hit_by_zlevel[Dm.text("[]", z)] = null;
    }

    // tg: code/controllers/subsystem/weather.dm:156
    public virtual datum_weather get_weather(object z = null, area active_area = null)
    {
        datum_weather A = null;
        foreach (datum_weather W in Dm.each_cast<datum_weather>(processing))
        {
            if (Dm.@in(z, W.impacted_z_levels) && W.area_type == active_area.type)
            {
                A = W;
                break;
            }
        }
        return A;
    }

    ///Returns an active storm by its type
    // tg: code/controllers/subsystem/weather.dm:166
    public virtual datum_weather get_weather_by_type(Type type = null)
    {
        return Dm.locate_type(type, processing) as datum_weather;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/weather.dm:2  SUBSYSTEM_DEF(weather): GLOBAL_REAL(SSweather, ...)
    internal datum_controller_subsystem_weather SSweather;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_weather SSweather => ref TgState.Current.SSweather;

    // tg: code/__DEFINES/maths.dm:38  #define ROUND_PROB(x) ( floor(x) + (prob(fract(x) * 100)) )
    public static float ROUND_PROB(float x) => MathF.Floor(x) + (prob((x - MathF.Floor(x)) * 100) ? 1 : 0);
}

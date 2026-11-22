// Translated from tgstation (snapshot 1d99bd7f): code/datums/weather/particle_weather.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file: the particle weathers' holders per level, their severity that wanders with the stage, and the
// wind. The holders and their particle sets are kept as tg keeps them; the client draws the particles from the weather datum's
// severity and wind (gaps E2a: the weather message), as BYOND particles are not drawn. The var blocks are generated.

/// Subtype which uses particle systems instead of overlays to display its effects
// tg: code/datums/weather/particle_weather.dm:2
public partial class datum_weather_particle : datum_weather
{
    // tg: code/datums/weather/particle_weather.dm:32
    [DmParams("z_levels", "weather_data")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (particle_type == null && emissive_type == null)
        {
            CRASH(Dm.text("[] ([]) attempted to initialize without normal or emissive particle types!", this, type));
            return;
        }

        foreach (object z_level in Dm.each(impacted_z_levels))
        {
            object offset = GET_Z_PLANE_OFFSET(Dm.num(z_level));
            var object_list = Dm.list();
            if (particle_type != null)
            {
                var holder = Dm.@new<obj_effect_abstract_weather_holder>();
                SET_PLANE_W_SCALAR(holder, RENDER_PLANE_PARTICLE_WEATHER, offset);
                holder.particles = Dm.@new(particle_type);
                Dm.append(object_list, holder);
            }

            if (emissive_type != null)
            {
                var holder = Dm.@new<obj_effect_abstract_weather_holder>();
                holder.particles = Dm.@new(emissive_type);
                SET_PLANE_W_SCALAR(holder, RENDER_PLANE_EMISSIVE_PARTICLE_WEATHER, offset);
                Dm.append(object_list, holder);
            }

            SSweather.add_weather_objects(object_list, z_level);

            if (weather_objects[z_level] is not DmList existing) // weather_objects[z_level] += object_list
                weather_objects[z_level] = object_list;
            else
                Dm.append(existing, object_list);
        }
    }

    // tg: code/datums/weather/particle_weather.dm:55
    public override object Destroy(float? force = null)
    {
        foreach (object z_level in Dm.each(weather_objects.Copy()))
        {
            var object_list = weather_objects[z_level] as DmList;
            SSweather.remove_weather_objects(object_list, z_level);
            QDEL_LIST(object_list);
        }
        weather_objects = null;
        return base.Destroy(force);
    }

    // tg: code/datums/weather/particle_weather.dm:63
    public override object telegraph(DmList weather_data = null)
    {
        object dot = base.telegraph(weather_data);
        if (!Dm.truthy(dot))
            return dot;
        animate_severity(0);
        return dot;
    }

    // tg: code/datums/weather/particle_weather.dm:69
    public override object end()
    {
        object dot = base.end();
        if (!Dm.truthy(dot))
            return dot;
        animate_severity(0);
        return dot;
    }

    /// Adjust our severity by a random number based on our stage
    // tg: code/datums/weather/particle_weather.dm:76
    public virtual void process_particles()
    {
        if (last_severity_tick + severity_cooldown > world.time)
            return;

        last_severity_tick = world.time;
        float new_severity = severity;
        switch ((int)stage)
        {
            case STARTUP_STAGE:
                // Aims at minimum severity, and can only go up
                new_severity += rand() * severity_variation * (1 - severity / min_severity);
                break;
            case MAIN_STAGE:
                // Tries to stay close to optimal severity
                new_severity += LERP(-severity_variation * clamp(INVERSE_LERP(min_severity, optimal_severity, severity), 0, 1),
                    severity_variation * clamp(INVERSE_LERP(max_severity, optimal_severity, severity), 0, 1), rand());
                new_severity = clamp(new_severity, min(new_severity, min_severity), max_severity);
                break;
            case WIND_DOWN_STAGE:
                // Slowly goes down to zero
                new_severity += rand() * -severity_variation * 4 * max(severity / min_severity, 1);
                break;
        }

        animate_severity(new_severity);
    }

    // tg: code/datums/weather/particle_weather.dm:97
    public virtual void animate_severity(float new_severity = 0)
    {
        if (!Dm.truthy(wind_sign))
            wind_sign = Dm.num(pick(-1, 1));

        severity = new_severity;
        foreach ((object _z_level, object holder_list) in Dm.pairs(weather_objects))
        {
            foreach (obj_effect_abstract_weather_holder holder in Dm.each_cast<obj_effect_abstract_weather_holder>(holder_list))
            {
                var particle_effect = holder.particles as particles_weather;
                particle_effect?.animate_severity(severity / MAXIMUM_WEATHER_SEVERITY, wind_sign);
            }
        }
    }

    // tg: code/datums/weather/particle_weather.dm:107
    public override DmList generate_overlay_cache()
    {
        DmList dot = base.generate_overlay_cache();

        if (stage == END_STAGE)
            return dot;

        // Change alpha of overlays on the particle weather plane
        foreach (mutable_appearance overlay in Dm.each_cast<mutable_appearance>(dot))
        {
            if (Dm.eq(PLANE_TO_TRUE(Dm.num(overlay.plane)), PARTICLE_WEATHER_PLANE))
                overlay.alpha = particle_weather_alpha;
        }

        for (int offset = 0; offset <= SSmapping.max_plane_offset; offset++)
            Dm.append(dot, mutable_appearance("icons/effects/weather_overlay.dmi", "weather_overlay", overlay_layer, null, WEATHER_MASK_PLANE, offset_const: offset));
        return dot;
    }
}

// tg: code/datums/weather/particle_weather.dm:142
public partial class particles_weather : particles
{
    /// Changes the strength of the weather visual effect, severity should be between 0 and 1
    // tg: code/datums/weather/particle_weather.dm:151
    public virtual void animate_severity(float severity = 0, float wind_sign = 0)
    {
        // Stop spawning if severity is zero or negative
        if (severity <= 0)
        {
            spawning = 0;
            return;
        }

        float wind = wind_strength * severity * wind_sign;
        spawning = LERP(min_spawn, max_spawn, severity);
        // Might already be set on init, in which case we preserve y and z components
        if (gravity is DmList gravity_list && gravity_list.len > 0)
            gravity_list[1] = wind;
        else
            gravity = Dm.list(wind);
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/maths.dm  #define INVERSE_LERP(a, b, value) ((value - a) / (b - a))
    public static float INVERSE_LERP(float a, float b, float value) => (value - a) / (b - a);
}

using System.IO;
using System.Text;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Protocol;
using TgParticles = Anomaly.Core.Tg.particles;

namespace Anomaly.Server.Replication;

/// <summary>
/// Protocol version 99 (DV687, docs/ai/specs/protocol.md section 95): an atom's BYOND particle set as the JSON its appearance carries, the
/// vars the client's emitter draws: <c>icon</c> (the file), <c>states</c> (the icon states and their weights), <c>color</c>, the numbers
/// <c>width</c>, <c>height</c>, <c>count</c>, <c>spawning</c>, <c>lifespan</c>, <c>fade</c>, <c>fadein</c>, <c>friction</c>, <c>rotation</c>,
/// <c>spin</c>, and the vectors (a number is all of its axes) <c>position</c>, <c>velocity</c>, <c>gravity</c>, <c>drift</c>,
/// <c>scale</c>, <c>grow</c>. A var that is unset, or a generator (not made, DV687), is left out.
/// </summary>
public static class ParticleSets
{
    /// <summary>The JSON of <paramref name="set"/>, or null when it is none or does not fit a table string.</summary>
    public static string Text(object set)
    {
        if (set is not TgParticles p || p.gc_destroyed != null)
            return null;
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            if (p.icon is DmResource resource && !string.IsNullOrEmpty(resource.Path))
                w.WriteString("icon", resource.Path);
            else if (p.icon is string file && file.Length > 0)
                w.WriteString("icon", file);
            States(w, p.icon_state);
            if (p.color is string color && color.Length > 0)
                w.WriteString("color", color);
            Number(w, "width", p.width);
            Number(w, "height", p.height);
            Number(w, "count", p.count);
            Number(w, "spawning", p.spawning);
            Number(w, "lifespan", p.lifespan);
            Number(w, "fade", p.fade);
            Number(w, "fadein", p.fadein);
            Number(w, "friction", p.friction);
            Number(w, "rotation", p.rotation);
            Number(w, "spin", p.spin);
            Vector(w, "position", p.position);
            Vector(w, "velocity", p.velocity);
            Vector(w, "gravity", p.gravity);
            Vector(w, "drift", p.drift);
            Vector(w, "scale", p.scale);
            Vector(w, "grow", p.grow);
            w.WriteEndObject();
        }
        string text = Encoding.UTF8.GetString(stream.ToArray());
        return Encoding.UTF8.GetByteCount(text) <= Limits.MaxTableStringBytes ? text : null;
    }

    private static void States(Utf8JsonWriter w, object state)
    {
        if (state is string one)
        {
            w.WriteStartObject("states");
            w.WriteNumber(one, 1);
            w.WriteEndObject();
        }
        else if (state is DmList weighted && weighted.Count > 0)
        {
            w.WriteStartObject("states");
            for (int i = 1; i <= weighted.Count; i++)
            {
                if (weighted[(float)i] is not string name)
                    continue;
                object weight = weighted[name];
                w.WriteNumber(name, weight is float or int or double ? Convert.ToDouble(weight) : 1);
            }
            w.WriteEndObject();
        }
    }

    private static void Number(Utf8JsonWriter w, string name, object value)
    {
        if (value is float or int or double && double.IsFinite(Convert.ToDouble(value)))
            w.WriteNumber(name, Convert.ToDouble(value));
    }

    private static void Vector(Utf8JsonWriter w, string name, object value)
    {
        if (value is float or int or double)
        {
            Number(w, name, value);
            return;
        }
        if (value is not DmList list || list.Count == 0)
            return;
        w.WriteStartArray(name);
        for (int i = 1; i <= list.Count && i <= 3; i++)
        {
            object axis = list[(float)i];
            double n = axis is float or int or double ? Convert.ToDouble(axis) : 0;
            w.WriteNumberValue(double.IsFinite(n) ? n : 0);
        }
        w.WriteEndArray();
    }
}

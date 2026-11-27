using System.Globalization;
using System.Text.Json;

namespace Anomaly.Client.Shared.Play;

/// <summary>
/// DV687 (protocol version 99): a BYOND particle set as the server sends it with an appearance (docs/ai/specs/protocol.md section 95),
/// and what the client's emitter makes of it. BYOND's particle vars are in pixels and ticks; a tick is read as tg's decisecond
/// (tg writes its lifespans in SECONDS), a pixel as a tg pixel of a 2 m tile. BYOND picks each particle's icon state from the weighted
/// list; the emitter draws them all in the heaviest one. Engine-free, so its sums are tested without Godot.
/// </summary>
public sealed class ParticleSet
{
    /// <summary>The seconds of a BYOND particle tick (tg's decisecond).</summary>
    public const double TickSeconds = 0.1;

    /// <summary>One tg pixel in metres (a 2 m tile of 32 pixels).</summary>
    public const double PixelMetres = 2.0 / 32.0;

    /// <summary>The most particles one emitter keeps alive (BYOND's <c>count</c> is capped to it).</summary>
    public const int MaxAmount = 200;

    public string Icon { get; private init; }
    public string State { get; private init; }
    public uint Rgba { get; private init; } = 0xFFFFFFFF;
    public double Count { get; private init; } = 100;
    public double Spawning { get; private init; } = 1;
    public double Lifespan { get; private init; } = 10;
    public double Fade { get; private init; }
    public double FadeIn { get; private init; }
    public double Friction { get; private init; }
    public double Rotation { get; private init; }
    public double Spin { get; private init; }
    public (double X, double Y, double Z) Position { get; private init; }
    public (double X, double Y, double Z) Velocity { get; private init; }
    public (double X, double Y, double Z) Gravity { get; private init; }
    public (double X, double Y) Scale { get; private init; } = (1, 1);
    public (double X, double Y) Grow { get; private init; }

    /// <summary>Parses the server's JSON; null when it is not a particle set.</summary>
    public static ParticleSet Parse(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            (double sx, double sy, _) = Vector(root, "scale", (1, 1, 1));
            (double gx, double gy, _) = Vector(root, "grow", (0, 0, 0));
            return new ParticleSet
            {
                Icon = root.TryGetProperty("icon", out JsonElement icon) && icon.ValueKind == JsonValueKind.String ? icon.GetString() : null,
                State = Heaviest(root),
                Rgba = root.TryGetProperty("color", out JsonElement color) && color.ValueKind == JsonValueKind.String ? ParseColor(color.GetString()) : 0xFFFFFFFF,
                Count = Number(root, "count", 100),
                Spawning = Number(root, "spawning", 1),
                Lifespan = Number(root, "lifespan", 10),
                Fade = Number(root, "fade", 0),
                FadeIn = Number(root, "fadein", 0),
                Friction = Math.Clamp(Number(root, "friction", 0), 0, 1),
                Rotation = Number(root, "rotation", 0),
                Spin = Number(root, "spin", 0),
                Position = Vector(root, "position", (0, 0, 0)),
                Velocity = Vector(root, "velocity", (0, 0, 0)),
                Gravity = Vector(root, "gravity", (0, 0, 0)),
                Scale = (sx, sy),
                Grow = (gx, gy),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A particle's life in seconds.</summary>
    public double LifetimeSeconds => Math.Max(TickSeconds, Lifespan * TickSeconds);

    /// <summary>The particles alive at once: BYOND's spawn rate over a life, at most <c>count</c> and <see cref="MaxAmount"/>, at least 1.</summary>
    public int Amount => (int)Math.Clamp(Math.Ceiling(Math.Max(0, Spawning) * Math.Max(1, Lifespan)), 1, Math.Min(Math.Max(1, Count), MaxAmount));

    /// <summary>False when the set spawns nothing (BYOND's <c>spawning</c> 0: a weather at rest).</summary>
    public bool Emits => Spawning > 0 && Lifespan > 0;

    /// <summary>The starting velocity in metres a second (BYOND x right, y up, z towards the viewer).</summary>
    public (double X, double Y, double Z) VelocityMetres => Scaled(Velocity, PixelMetres / TickSeconds);

    /// <summary>The acceleration in metres a second squared.</summary>
    public (double X, double Y, double Z) GravityMetres => Scaled(Gravity, PixelMetres / (TickSeconds * TickSeconds));

    /// <summary>The spawn point's offset from the atom's middle, in metres.</summary>
    public (double X, double Y, double Z) PositionMetres => Scaled(Position, PixelMetres);

    /// <summary>The deceleration that friction gives the starting speed (BYOND's share lost a tick, as a constant rate).</summary>
    public double DampingMetres
    {
        get
        {
            var v = VelocityMetres;
            double speed = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            return Friction / TickSeconds * speed;
        }
    }

    /// <summary>A particle's starting scale (the mean of BYOND's x and y).</summary>
    public double StartScale => Math.Max(0.01, (Scale.X + Scale.Y) / 2);

    /// <summary>A particle's scale at the end of its life over its starting one (BYOND's grow a tick).</summary>
    public double EndScaleRatio => Math.Max(0, StartScale + (Grow.X + Grow.Y) / 2 * Lifespan) / StartScale;

    /// <summary>The alpha a particle has at <paramref name="life"/> (0 to 1 of its life): faded in over fadein, out over fade, times the colour's.</summary>
    public double AlphaAt(double life)
    {
        double ticks = Math.Clamp(life, 0, 1) * Lifespan;
        double alpha = (Rgba & 0xFF) / 255.0;
        if (FadeIn > 0 && ticks < FadeIn)
            alpha *= ticks / FadeIn;
        if (Fade > 0 && ticks > Lifespan - Fade)
            alpha *= Math.Max(0, (Lifespan - ticks) / Fade);
        return alpha;
    }

    private static (double, double, double) Scaled((double X, double Y, double Z) v, double by) => (v.X * by, v.Y * by, v.Z * by);

    private static string Heaviest(JsonElement root)
    {
        if (!root.TryGetProperty("states", out JsonElement states) || states.ValueKind != JsonValueKind.Object)
            return string.Empty;
        string best = null;
        double weight = double.NegativeInfinity;
        foreach (JsonProperty state in states.EnumerateObject())
        {
            double w = state.Value.ValueKind == JsonValueKind.Number ? state.Value.GetDouble() : 1;
            if (w > weight)
            {
                weight = w;
                best = state.Name;
            }
        }
        return best ?? string.Empty;
    }

    private static double Number(JsonElement root, string name, double fallback)
        => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && double.IsFinite(value.GetDouble()) ? value.GetDouble() : fallback;

    // A vector var: a list of up to three numbers (missing axes 0), or one number for all of its axes.
    private static (double, double, double) Vector(JsonElement root, string name, (double, double, double) fallback)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
            return fallback;
        if (value.ValueKind == JsonValueKind.Number)
        {
            double n = value.GetDouble();
            return (n, n, n);
        }
        if (value.ValueKind != JsonValueKind.Array)
            return fallback;
        double[] axes = new double[3];
        int i = 0;
        foreach (JsonElement axis in value.EnumerateArray())
        {
            if (i >= 3)
                break;
            axes[i++] = axis.ValueKind == JsonValueKind.Number ? axis.GetDouble() : 0;
        }
        return (axes[0], axes[1], axes[2]);
    }

    /// <summary>BYOND's <c>#rrggbb</c> or <c>#rrggbbaa</c> as <c>0xRRGGBBAA</c>; white for anything else.</summary>
    public static uint ParseColor(string text)
    {
        if (string.IsNullOrEmpty(text) || text[0] != '#')
            return 0xFFFFFFFF;
        string hex = text.Substring(1);
        if (hex.Length == 6)
            hex += "ff";
        return hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgba) ? rgba : 0xFFFFFFFF;
    }
}

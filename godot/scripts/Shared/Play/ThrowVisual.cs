namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// D127: an entity flagged Thrown gets an arc and a spin; the tiles it flies over and where it lands are the server's.

/// <summary>The arc and the spin of a thrown thing: a pure function of how long it has been in flight.</summary>
public static class ThrowVisual
{
    /// <summary>How high the arc rises (metres above the thing's own height).</summary>
    public const double PeakMetres = 0.8;

    /// <summary>How long one arc lasts (seconds); a longer flight rides low after it, until the server lands the thing.</summary>
    public const double ArcSeconds = 0.55;

    /// <summary>The spin (radians per second): about two turns a second.</summary>
    public const double SpinRadiansPerSecond = 12.5;

    /// <summary>The height above the thing's resting height <paramref name="ageSeconds"/> after the throw began: a half sine over <see cref="ArcSeconds"/>, 0 after.</summary>
    public static double Height(double ageSeconds)
        => ageSeconds <= 0 || ageSeconds >= ArcSeconds ? 0.0 : PeakMetres * Math.Sin(Math.PI * ageSeconds / ArcSeconds);

    /// <summary>The spin angle (radians) after <paramref name="ageSeconds"/>.</summary>
    public static double Spin(double ageSeconds) => ageSeconds <= 0 ? 0.0 : ageSeconds * SpinRadiansPerSecond;

    /// <summary>The horizontal scale a spin about the vertical axis gives a flat picture: |cos(angle)|, never thinner than a fifth.</summary>
    public static double SpinScale(double ageSeconds) => Math.Max(0.2, Math.Abs(Math.Cos(Spin(ageSeconds))));
}

/// <summary>Remembers when each thrown entity was first seen in flight (the renderer asks for the age; the flag going off forgets it).</summary>
public sealed class ThrowClock
{
    private readonly Dictionary<long, double> _startedAt = new();

    /// <summary>How long entity <paramref name="id"/> has been in flight at <paramref name="now"/>; 0 and forgotten when it is not flagged.</summary>
    public double Age(long id, bool thrown, double now)
    {
        if (!thrown)
        {
            _startedAt.Remove(id);
            return 0;
        }
        if (!_startedAt.TryGetValue(id, out double start))
        {
            _startedAt[id] = now;
            return 0;
        }
        return now - start;
    }

    /// <summary>Entities in flight now.</summary>
    public int InFlight => _startedAt.Count;

    /// <summary>Forgets a despawned entity.</summary>
    public void Forget(long id) => _startedAt.Remove(id);
}

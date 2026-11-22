using Anomaly.Core.Motion;

namespace Anomaly.Client.Shared;

// Owner: the singularity's look (the owner's request, 2026-10-19). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// DEVIATION(DV67): tg's singularity steps a tile once a second (singularity.dm's component, <c>time_since_last_eat &gt; 1</c>) and BYOND glides each
/// step at its default glide size (8 pixels a tick: a fifth of a second), so it jumps a tile and stands still; the owner asked for it to drift as
/// smoothly as the own body in zero gravity. The client draws it with the own body's glide (<see cref="OwnGlide"/>) along the tiles the server names,
/// at the pace its steps come (not its glide size), about half a tile to a tile and a half behind the server's tile; the server's tile stays where it eats and pulls.
/// </summary>
public static class SingularityDrift
{
    /// <summary>tg's gravitational singularity and its kinds (the dark matter one, the bloodthirsty one).</summary>
    public const string Type = "/obj/singularity";

    /// <summary>Its pace before its own is measured: one tile a second, as tg's component steps (tiles a tick).</summary>
    public const double DefaultTilesPerTick = 1.0 / MotionMath.TicksPerSecond;

    /// <summary>A pause after which a step starts a new drift (seconds): longer than its one second between steps.</summary>
    public const double RestSeconds = 2.5;

    /// <summary>
    /// How far behind its steps it aims (seconds): half a step, so a step that comes a little late does not stop it on its tile, and a turn
    /// arrives while the corner's rounding has its whole width (a point near the path's end has a narrow one, which widened with a jump).
    /// </summary>
    public const double MarginSeconds = 0.5;

    /// <summary>Whether a thing of <paramref name="typePath"/> drifts so.</summary>
    public static bool Applies(string typePath) => typePath != null && typePath.StartsWith(Type, StringComparison.Ordinal);

    /// <summary>A new glide for one singularity.</summary>
    public static OwnGlide NewGlide() => new(RestSeconds, DefaultTilesPerTick, MarginSeconds) { MeasuredFirst = true };

    /// <summary>
    /// Feeds <paramref name="glide"/> the server's tile (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) and lets <paramref name="seconds"/>
    /// pass; returns where it is drawn. The first call starts it on that tile.
    /// </summary>
    public static (float X, float Y, int Z) Step(OwnGlide glide, int x, int y, int z, double seconds) => TileDrift.Step(glide, x, y, z, seconds, 0f); // its pace, not its glide size (which would rush each tile and stop)
}

/// <summary>
/// DEVIATION(DV67): everything else tg moves tile by tile (an item the singularity or a gravity pull steps toward itself once a second, a thing an
/// explosion throws one or two tiles a tick, a ghost) was drawn by BYOND's glide: each step at its glide size, then still until the next, so a pulled
/// item hopped and a thrown one changed speed every tick (the owner, 2026-10-19). It is drawn by the own body's glide too: along the tiles at an even
/// speed, the pace its steps come at once three are known (its glide size before), a share of a step behind the server.
/// </summary>
public static class TileDrift
{
    /// <summary>How far behind the server's steps it aims, as a share of a step's time.</summary>
    public const double MarginShare = 0.35;

    /// <summary>How fast it may speed up (tiles a second, a second): a thing an explosion throws is at its 40 tiles a second within a tenth of one.</summary>
    public const double Acceleration = 400;

    /// <summary>A pause after which a step starts a new flight (seconds): longer than the second between a pull's steps.</summary>
    public const double RestSeconds = 1.5;

    /// <summary>A new glide for a thing of <paramref name="typePath"/> (the singularity's own pace, <see cref="SingularityDrift"/>).</summary>
    public static OwnGlide NewGlide(string typePath) =>
        SingularityDrift.Applies(typePath) ? SingularityDrift.NewGlide() : new OwnGlide(RestSeconds) { MeasuredFirst = true, MarginShare = MarginShare, Acceleration = Acceleration };

    /// <summary>
    /// Feeds <paramref name="glide"/> the server's tile and lets <paramref name="seconds"/> pass, <paramref name="glideSize"/> tg's glide size of the thing
    /// (0: unknown); returns where it is drawn. The first call starts it on that tile.
    /// </summary>
    public static (float X, float Y, int Z) Step(OwnGlide glide, int x, int y, int z, double seconds, float glideSize)
    {
        if (!glide.Active)
            glide.Start(x, y, z);
        glide.FollowTile(x, y, z);
        glide.Advance(seconds, glideSize);
        return (glide.X, glide.Y, glide.Z);
    }
}

namespace Anomaly.Client.Shared;

// Owner: the space play-test fixes (2026-10-15). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// DEVIATION(DV46): how the first-person client draws tg's particle effects (<c>/obj/effect/particle_effect</c>: the jetpack's ion trail, the
/// extinguisher's water puffs, smoke, sparks) next to the own body. tg puts them on a tile under or behind the own sprite (the trail on the
/// turf the holder just left, the puffs on the sprayer's own turf), where the sprite covers them and the glide carries the sprite away from
/// them. In first person the own body is drawn where the client's glide has it, which in zero gravity is about a step behind the server's
/// tile (<see cref="OwnGlide"/>), so the turf the server's mob just left is at or ahead of the eye, and a tile-sized picture there fills the
/// view (the owner's screenshot, 2026-10-15: two trail particles right in front of the eye).
/// <list type="bullet">
/// <item><description><b>Drawn from the drawn body</b> (<see cref="OwnShift"/>): a particle that appears within two tiles of the own mob's
/// server tile keeps tg's place relative to the mob, but measured from where the own body is drawn: it is moved by the own view's lag
/// (the drawn point less the server's tile) as it was when the particle appeared, for its whole life. A trail particle is then a step
/// behind the drawn body, the puffs leave from the drawn body.</description></item>
/// <item><description><b>Faded near the eye</b> (<see cref="Alpha"/>): hidden within <see cref="HiddenTiles"/> of the eye on the ground,
/// whole from <see cref="ShownTiles"/>, smoothly between, as the own sprite would cover it.</description></item>
/// </list>
/// Pure presentation: the server's particles, their tiles and their lives are tg's.
/// </summary>
public static class NearEyeFade
{
    /// <summary>tg's particle effects.</summary>
    public const string ParticlePrefix = World.StaticPropRule.ParticlePrefix;

    /// <summary>Nearer than this (tiles, on the ground) a particle is not drawn.</summary>
    public const float HiddenTiles = 0.5f;

    /// <summary>From this far (tiles) a particle is drawn whole.</summary>
    public const float ShownTiles = 1.0f;

    /// <summary>Whether entities of this type fade near the eye.</summary>
    public static bool Applies(string typePath) => typePath != null && typePath.StartsWith(ParticlePrefix, StringComparison.Ordinal);

    /// <summary>A particle this close to the own mob's server tile (tiles, on either axis) is the own body's (<see cref="OwnShift"/>).</summary>
    public const int OwnReach = 2;

    /// <summary>The largest lag the shift carries (tiles); a larger one (a jump) leaves the particle where tg put it.</summary>
    public const float MaxShift = 3f;

    /// <summary>
    /// The shift (tiles) of a particle that appears at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) while the own mob's
    /// server tile is (<paramref name="ownX"/>, <paramref name="ownY"/>, <paramref name="ownZ"/>) and the own body is drawn at
    /// (<paramref name="drawnX"/>, <paramref name="drawnY"/>): the own view's lag for a particle of the own body, else none.
    /// </summary>
    public static (float X, float Y) OwnShift(int x, int y, int z, int ownX, int ownY, int ownZ, float drawnX, float drawnY)
    {
        if (z != ownZ || Math.Max(Math.Abs(x - ownX), Math.Abs(y - ownY)) > OwnReach)
            return (0f, 0f);
        float dx = drawnX - ownX, dy = drawnY - ownY;
        return Math.Max(Math.Abs(dx), Math.Abs(dy)) > MaxShift ? (0f, 0f) : (dx, dy);
    }

    /// <summary>The alpha factor (0 to 1) of a particle (<paramref name="dxTiles"/>, <paramref name="dyTiles"/>) away from the eye on the ground.</summary>
    public static float Alpha(float dxTiles, float dyTiles)
    {
        float d = MathF.Sqrt(dxTiles * dxTiles + dyTiles * dyTiles);
        if (d <= HiddenTiles)
            return 0f;
        if (d >= ShownTiles)
            return 1f;
        float t = (d - HiddenTiles) / (ShownTiles - HiddenTiles);
        return t * t * (3f - 2f * t);
    }
}

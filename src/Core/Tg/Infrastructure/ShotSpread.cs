using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

/// <summary>
/// DEVIATION(DV96, the owner 2026-11-20: the Cybersun S-120's spray felt huge): the share of tg's deviation an aimed shot's 3D cone keeps (the gun's
/// spread, a burst's, the casing's variance, a pellet's). A first-person view shows the cone's angle as it is, and a drawn body is narrower than
/// tg's tile, so tg's numbers miss far more than in 2D. The server's setting (<c>ServerConfig.SpreadScale</c>, 0.5 by default); 1, tg's, with no
/// setting (the core's tests).
/// </summary>
public sealed class ShotSpread
{
    public const float TgScale = 1f, MinScale = 0f, MaxScale = 2f;

    public float Scale { get; private set; } = TgScale;

    public static ShotSpread Of(World world) => world.GetExtension(static _ => new ShotSpread());

    /// <summary>A scale <see cref="Set"/> takes: <see cref="MinScale"/> to <see cref="MaxScale"/>.</summary>
    public static bool IsValid(float scale) => scale >= MinScale && scale <= MaxScale;

    public void Set(float scale)
    {
        if (!IsValid(scale))
            throw new ArgumentOutOfRangeException(nameof(scale), scale, $"the spread scale is {MinScale} to {MaxScale}");
        Scale = scale;
    }

    /// <summary>The scale of <paramref name="world"/> (tg's with no world).</summary>
    public static float ScaleOf(World world) => world == null ? TgScale : Of(world).Scale;
}

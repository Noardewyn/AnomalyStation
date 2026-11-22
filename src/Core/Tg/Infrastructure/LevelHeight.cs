using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

/// <summary>
/// The owner (2026-11-20, revising D119): the height of a level (its walls, its ceiling, the floor of the level over it) is one for the world, the
/// server's setting (<c>ServerConfig.WallHeight</c>), sent to the clients (protocol version 91, <c>MapGrid.WallHeightMillimetres</c>), so the server
/// knows where every ceiling is. Heights stay in tile pixels (32 to the 2 m tile): a level is <see cref="Pixels"/> of them tall.
/// </summary>
public sealed class LevelHeight
{
    public const float DefaultMetres = 2f, MinMetres = 2f, MaxMetres = 4f;

    /// <summary>Tile pixels to a metre (32 to the 2 m tile).</summary>
    public const float PixelsPerMetre = 16f;

    public float Metres { get; private set; } = DefaultMetres;

    public static LevelHeight Of(World world) => world.GetExtension(static _ => new LevelHeight());

    /// <summary>A height <see cref="Set"/> takes: <see cref="MinMetres"/> to <see cref="MaxMetres"/>.</summary>
    public static bool IsValid(float metres) => metres >= MinMetres && metres <= MaxMetres;

    public void Set(float metres)
    {
        if (!IsValid(metres))
            throw new ArgumentOutOfRangeException(nameof(metres), metres, $"a level is {MinMetres} to {MaxMetres} m high");
        Metres = metres;
    }

    /// <summary>The height of a level of <paramref name="world"/> in tile pixels (the default's with no world).</summary>
    public static float Pixels(World world) => (world == null ? DefaultMetres : Of(world).Metres) * PixelsPerMetre;
}

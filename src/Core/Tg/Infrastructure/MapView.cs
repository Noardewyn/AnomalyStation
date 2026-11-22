using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Owner: M1 contracts step (complete). A host seam, not tg code.

/// <summary>
/// Read-only access to a world's map for hosts that read the world from outside it (the replicator after
/// <c>World.Step</c>, <c>Anomaly.Server.Replication.IReplicator</c>): the size and the turf of a cell, with no ambient
/// <see cref="World"/>. Unlike <c>locate(x, y, z)</c> it applies no pending DM contents edit and runs no tg code, so a read
/// never changes the world (or its state hash). Atoms themselves are read through their builtin vars (<c>loc</c>,
/// <c>x</c>, <c>y</c>, <c>z</c>, <c>dir</c>), which outside a world scope also read raw.
/// </summary>
public static class MapView
{
    /// <summary>BYOND <c>world.maxx</c> as the map grid has built it (0 before a map exists).</summary>
    public static int Width(World world) => MapGrid.Of(world).maxx;

    /// <summary>BYOND <c>world.maxy</c> as built.</summary>
    public static int Height(World world) => MapGrid.Of(world).maxy;

    /// <summary>BYOND <c>world.maxz</c> as built.</summary>
    public static int Levels(World world) => MapGrid.Of(world).maxz;

    /// <summary>The turf of the cell (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), null outside the map.</summary>
    public static turf TurfAt(World world, int x, int y, int z) => MapGrid.Of(world).TurfAt(x, y, z);
}

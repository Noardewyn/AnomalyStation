using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Protocol version 31 (M7 slice T3, DV81): what a mob with SEE_TURFS (tg's TRAIT_MESON_VISION) sees of the turfs through the walls and the dark.
// Engine-free (no Godot API: compiled into tests/Anomaly.Server.Tests too). In tg BYOND's client draws every turf of the mob's view, walls and
// floors alike, whatever stands in the way and however dark it is; the 3D client draws the turfs of the view round the wearer as a faint
// outline through everything (Render/MesonLayer): a wall as its box, an open floor as its square. This class says which cells those are.

/// <summary>What is drawn at a cell for a mob that sees the turfs.</summary>
public enum MesonCell : byte
{
    /// <summary>Nothing (outside the map, or space: tg's space turf has no structure to see).</summary>
    None = 0,

    /// <summary>A floor: an open turf.</summary>
    Floor = 1,

    /// <summary>A wall: an opaque turf.</summary>
    Wall = 2,
}

public static class MesonView
{
    /// <summary>The turfs seen round the wearer: tg's view is 15 tiles wide (<c>world.view</c> 7 each way); the owner (2026-10-30): 4 tiles more.</summary>
    public const int Radius = 11;

    /// <summary>The cells of the square of <see cref="Radius"/> round (<paramref name="x"/>, <paramref name="y"/>) on level <paramref name="z"/>, row after row from the south-west.</summary>
    public static MesonCell[] Plan(ReplicaWorld replica, int x, int y, int z, int radius = Radius)
    {
        int side = radius * 2 + 1;
        var cells = new MesonCell[side * side];
        if (replica == null)
            return cells;
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                cells[(dy + radius) * side + dx + radius] = At(replica, x + dx, y + dy, z);
        return cells;
    }

    /// <summary>What is drawn at one cell.</summary>
    public static MesonCell At(ReplicaWorld replica, int x, int y, int z)
    {
        string type = replica.TurfTypeAt(x, y, z);
        if (type == null || type.StartsWith("/turf/open/space", StringComparison.Ordinal))
            return MesonCell.None;
        return replica.IsOpaque(x, y, z) ? MesonCell.Wall : MesonCell.Floor;
    }
}

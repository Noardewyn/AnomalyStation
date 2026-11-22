using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Light;

// Owner: M3 P7. Engine-free.

/// <summary>
/// <see cref="ILightCells"/> over the replica's version-3 map state (<c>ReplicaWorld.IsOpaque</c>, <c>BaseLightIndexAt</c>,
/// <c>BaseLights</c>, <c>TurfTypeAt</c>); P5 fills those members.
/// </summary>
public sealed class ReplicaLightCells : ILightCells
{
    private const string SpacePrefix = "/turf/open/space";

    private readonly ReplicaWorld _world;
    private readonly Dictionary<string, bool> _spaceTypes = new();

    public ReplicaLightCells(ReplicaWorld world, int maxX, int maxY)
    {
        _world = world;
        MaxX = maxX;
        MaxY = maxY;
    }

    public int MaxX { get; }

    public int MaxY { get; }

    public bool IsOpaque(int x, int y, int z) => _world.IsOpaque(x, y, z);

    public uint BaseLight(int x, int y, int z)
    {
        int index = _world.BaseLightIndexAt(x, y, z);
        if (index <= 0)
            return 0u;
        IReadOnlyList<BaseLight> table = _world.BaseLights;
        return index <= table.Count ? table[index - 1].Rgba : 0u;
    }

    public bool IsOpenToSky(int x, int y, int z)
    {
        if (_world.IsOpaque(x, y, z) || !_world.CeilingAt(x, y, z).TransmitsLight)
            return false;
        // D325: a cell under a level of its multi-z stack sees the sky only through open space (or space) all the way up
        for (int up = z + 1; _world.IsLinkedBelow(up); up++)
        {
            if (_world.IsOpaque(x, y, up) || TurfStyle.Classify(_world.TurfTypeAt(x, y, up)) != TurfKind.Void || !_world.CeilingAt(x, y, up).TransmitsLight)
                return false;
        }
        return true;
    }

    /// <summary>
    /// <see cref="IsOpenToSky"/> of every level of the stack in one pass down each column (the owner, 2026-11-15: filled level by level, each cell
    /// looking up the whole stack, a 13-level stack froze the game for half a second at every change of level).
    /// </summary>
    public void FillSkyStack(byte[][] levels, int low, int x0 = 1, int y0 = 1, int x1 = int.MaxValue, int y1 = int.MaxValue)
    {
        int w = MaxX + 1;
        for (int y = Math.Max(1, y0); y <= Math.Min(MaxY, y1); y++)
        {
            for (int x = Math.Max(1, x0); x <= Math.Min(MaxX, x1); x++)
            {
                bool open = true; // nothing over this cell of the level below blocks the sky so far
                for (int i = levels.Length - 1; i >= 0; i--)
                {
                    int z = low + i;
                    bool clear = open && !_world.IsOpaque(x, y, z) && _world.CeilingAt(x, y, z).TransmitsLight;
                    levels[i][y * w + x] = clear ? (byte)255 : (byte)0;
                    open = clear && IsVoid(_world.TurfTypeAt(x, y, z));
                }
            }
        }
    }

    private readonly Dictionary<string, bool> _voidTypes = new();

    private bool IsVoid(string type)
    {
        if (type == null)
            return TurfStyle.Classify(type) == TurfKind.Void;
        if (!_voidTypes.TryGetValue(type, out bool v))
            _voidTypes[type] = v = TurfStyle.Classify(type) == TurfKind.Void;
        return v;
    }

    public bool IsSpaceTurf(int x, int y, int z)
    {
        string type = _world.TurfTypeAt(x, y, z);
        if (type == null)
            return false;
        if (!_spaceTypes.TryGetValue(type, out bool space))
        {
            space = type.StartsWith(SpacePrefix, StringComparison.Ordinal);
            _spaceTypes[type] = space;
        }

        return space;
    }
}

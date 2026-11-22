using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

public sealed partial class ReplicaWorld
{
    private readonly Dictionary<(int, int, int), CeilingState> _ceilings = new();
    private readonly Dictionary<(int, int, int), TableRef> _ceilingLooks = new();
    public CeilingState CeilingAt(int x, int y, int z) => _ceilings.GetValueOrDefault((x, y, z));
    /// <summary>The public-table picture of the ceiling's block (D295); none without a ceiling.</summary>
    public TableRef CeilingLookAt(int x, int y, int z) => _ceilingLooks.GetValueOrDefault((x, y, z));
    /// <summary>Protocol version 60: whether level <paramref name="z"/> has tg's <c>ZTRAIT_NOPARALLAX</c> (Lavaland): no space behind it.</summary>
    public bool IsNoParallax(int z) => _map?.NoParallaxLevels.Contains(z) ?? false;

    /// <summary>Version 76 (D325): level <paramref name="z"/> stands on level z - 1 (tg's <c>ZTRAIT_DOWN</c>), one multi-z stack.</summary>
    public bool IsLinkedBelow(int z) => _map?.LinkedBelowLevels.Contains(z) ?? false;

    /// <summary>Version 76 (D325): whether two levels are one multi-z stack (the same level is).</summary>
    public bool SameStack(int a, int b)
    {
        if (a == b) return true;
        (int low, int high) = a < b ? (a, b) : (b, a);
        for (int z = high; z > low; z--)
            if (!IsLinkedBelow(z)) return false;
        return true;
    }
    /// <summary>
    /// D325, DV370: the own capsule's guess of tg's level change when its centre crosses from (x, y, z) across the face <paramref name="dir"/>
    /// (<see cref="ICollisionView.LevelShift"/>): +1 off the top stair of a flight going that way with open space over it and a floor
    /// beyond on the level above (stairs.dm isTerminator, stair_ascend); minus the levels it drops onto open space with nothing that holds
    /// it (openspace.dm zPassOut: a lattice or a catwalk); else 0.
    /// </summary>
    public int LevelShiftGuess(int x, int y, int z, byte dir)
    {
        (int dx, int dy) = dir switch { MotionDir.North => (0, 1), MotionDir.South => (0, -1), MotionDir.East => (1, 0), MotionDir.West => (-1, 0), _ => (0, 0) };
        if (dx == 0 && dy == 0)
            return 0;
        if (StairsAt(x, y, z, dir) && !StairsAt(x + dx, y + dy, z, dir))
            return IsLinkedBelow(z + 1) && IsOpenSpace(x, y, z + 1) && !IsOpenSpace(x + dx, y + dy, z + 1) && TurfTypeAt(x + dx, y + dy, z + 1) != null ? 1 : 0;
        int levels = 0;
        for (int at = z; IsOpenSpace(x + dx, y + dy, at) && IsLinkedBelow(at) && !HoldsAbove(x + dx, y + dy, at) && TakesFall(x + dx, y + dy, at - 1); at--)
            levels++;
        return -levels;
    }

    // tg's zPassIn(DOWN): a closed turf takes no faller; DV398: nor does an open one under a full-tile window (a window on a solid cell)
    private bool TakesFall(int x, int y, int z)
    {
        if (TurfTypeAt(x, y, z) is not string type || type.StartsWith("/turf/closed", StringComparison.Ordinal))
            return false;
        if (!IsSolid(x, y, z))
            return true;
        foreach (ReplicaEntity e in Entities)
            if (e.State.X == x && e.State.Y == y && e.State.Z == z && IsOrUnder(e.TypePath, "/obj/structure/window"))
                return false;
        return true;
    }

    private bool IsOpenSpace(int x, int y, int z)
        => TurfTypeAt(x, y, z) is string type && (type == "/turf/open/openspace" || type.StartsWith("/turf/open/openspace/", StringComparison.Ordinal));

    private bool StairsAt(int x, int y, int z, byte dir)
    {
        foreach (ReplicaEntity e in Entities)
            if (e.State.X == x && e.State.Y == y && e.State.Z == z && e.State.Dir == dir
                && (e.TypePath == "/obj/structure/stairs" || e.TypePath?.StartsWith("/obj/structure/stairs/", StringComparison.Ordinal) == true))
                return true;
        return false;
    }

    // tg's BLOCK_Z_OUT_DOWN things hold what stands on open space: the lattice and catwalk, a ladder, an elevator's platform, a tram's floor (tram_floors.dm:194)
    private bool HoldsAbove(int x, int y, int z)
    {
        foreach (ReplicaEntity e in Entities)
            if (e.State.X == x && e.State.Y == y && e.State.Z == z
                && (IsOrUnder(e.TypePath, "/obj/structure/lattice") || IsOrUnder(e.TypePath, "/obj/structure/ladder") || IsOrUnder(e.TypePath, "/obj/structure/transport/linear")
                    || IsOrUnder(e.TypePath, "/obj/structure/thermoplastic")))
                return true;
        return false;
    }

    /// <summary>D325: whether an elevator's platform (tg's /obj/structure/transport/linear) or a ladder stands on the tile.</summary>
    public bool LiftAt(int x, int y, int z)
    {
        foreach (ReplicaEntity e in Entities)
            if (e.State.X == x && e.State.Y == y && e.State.Z == z && (IsOrUnder(e.TypePath, "/obj/structure/transport/linear") || IsOrUnder(e.TypePath, "/obj/structure/ladder")))
                return true;
        return false;
    }

    private readonly HashSet<(int X, int Y, int Z)> _ladders = new();
    private long _laddersTick = long.MinValue;
    private int _laddersCount = -1;

    /// <summary>
    /// DV397: whether a ladder (tg's /obj/structure/ladder) stands on the tile: its rails block capsules (<c>Anomaly.Core.Motion.LadderBody</c>), as the server's
    /// <c>LocomotionSeam.LadderAt</c>. The ladders' tiles are gathered once per applied tick (the predictor asks every step).
    /// </summary>
    public bool LadderAt(int x, int y, int z)
    {
        if (_laddersTick != _tick || _laddersCount != Count)
        {
            _ladders.Clear();
            foreach (ReplicaEntity e in Entities)
                if (IsOrUnder(e.TypePath, "/obj/structure/ladder") && !(TryGetAppearance(e.State.Appearance, out AppearanceEntry look) && ResolveString(look.IconState) == LadderBody.LyingState))
                    _ladders.Add((e.State.X, e.State.Y, e.State.Z));
            (_laddersTick, _laddersCount) = (_tick, Count);
        }
        return _ladders.Contains((x, y, z));
    }

    private static bool IsOrUnder(string path, string root)
        => path != null && path.StartsWith(root, StringComparison.Ordinal) && (path.Length == root.Length || path[root.Length] == '/');

    public bool ReceivesExteriorLight(int x, int y, int z)
        => !IsOpaque(x, y, z) && CeilingAt(x, y, z).TransmitsLight
            && (IsStarlit(x, y, z) || TurfTypeAt(x, y, z) is string type
                && !type.StartsWith("/turf/open/space", StringComparison.Ordinal)
                && !type.StartsWith("/turf/open/openspace", StringComparison.Ordinal));

    private void TakeCeilings(MapGrid grid)
    {
        _ceilings.Clear();
        _ceilingLooks.Clear();
        foreach (var c in grid.Ceilings)
        {
            CheckCell(c.X, c.Y, c.Z);
            if (!_ceilings.TryAdd((c.X, c.Y, c.Z), c.State)) throw Bad("duplicate ceiling");
            _ceilingLooks[(c.X, c.Y, c.Z)] = c.Look;
        }
    }
    private void ApplyCeilings(Delta delta)
    {
        foreach (var c in delta.Ceilings)
        {
            CheckCell(c.X, c.Y, c.Z);
            if (c.State.Exists) { _ceilings[(c.X, c.Y, c.Z)] = c.State; _ceilingLooks[(c.X, c.Y, c.Z)] = c.Look; }
            else { _ceilings.Remove((c.X, c.Y, c.Z)); _ceilingLooks.Remove((c.X, c.Y, c.Z)); }
            _cellsChanged.Add((c.X, c.Y, c.Z));
        }
    }
}

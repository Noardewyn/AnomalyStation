using Anomaly.Core.Motion;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// A test collision view (M2 P4): a rectangular map of open tiles (1..Width, 1..Height on level 1; outside is solid, as
/// the contract says), a set of solid tiles, blocked faces, and standing capsules. What the server's bridge and a client's
/// replica give the controller, without a world.
/// </summary>
internal sealed class GridView : ICollisionView
{
    private readonly HashSet<(int X, int Y)> _solid = new();
    private readonly Dictionary<(int X, int Y), TileEdges> _edges = new();
    private readonly HashSet<(int X, int Y)> _climbWalkable = new();

    public GridView(int width = 20, int height = 20)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The standing capsules (a test edits the list; ids are what GatherCapsules sorts by).</summary>
    public List<CapsuleObstacle> Capsules { get; } = new();

    public int IsSolidCalls { get; private set; }

    public GridView Solid(int x, int y)
    {
        _solid.Add((x, y));
        return this;
    }

    /// <summary>D239: a tile solid only through climb_walkable structures (a table).</summary>
    public GridView Table(int x, int y)
    {
        _solid.Add((x, y));
        _climbWalkable.Add((x, y));
        return this;
    }

    public bool IsClimbWalkable(int x, int y, int z) => z == 1 && _climbWalkable.Contains((x, y));

    private readonly HashSet<(int X, int Y)> _flaps = new();

    /// <summary>DV360: tg's airtight plastic flaps on a tile (not solid).</summary>
    public GridView Flaps(int x, int y)
    {
        _flaps.Add((x, y));
        return this;
    }

    public bool IsFlaps(int x, int y, int z) => z == 1 && _flaps.Contains((x, y));

    private readonly HashSet<(int X, int Y)> _ladders = new();

    /// <summary>DV397: a ladder on a tile (its rails block, LadderBody).</summary>
    public GridView Ladder(int x, int y)
    {
        _ladders.Add((x, y));
        return this;
    }

    public bool LadderAt(int x, int y, int z) => z == 1 && _ladders.Contains((x, y));

    public GridView Edge(int x, int y, TileEdges edges)
    {
        _edges[(x, y)] = edges;
        return this;
    }

    public bool IsSolid(int x, int y, int z)
    {
        IsSolidCalls++;
        if (z != 1 || x < 1 || y < 1 || x > Width || y > Height) return true;
        return _solid.Contains((x, y));
    }

    public TileEdges EdgesOf(int x, int y, int z) => z == 1 && _edges.TryGetValue((x, y), out TileEdges e) ? e : TileEdges.None;

    public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
    {
        if (z != 1) return;
        foreach (CapsuleObstacle c in Capsules.OrderBy(c => c.Id))
        {
            if (c.Id == exclude) continue;
            if (Math.Abs(c.X - x) <= range && Math.Abs(c.Y - y) <= range)
                into.Add(c);
        }
    }

    /// <summary>Whether the capsule's square (half-side r subunits) overlaps the interior of a solid tile.</summary>
    public bool BoxInSolid(int cx, int cy, int r)
    {
        for (int tx = MotionMath.TileOf(cx - r) - 1; tx <= MotionMath.TileOf(cx + r) + 1; tx++)
        {
            for (int ty = MotionMath.TileOf(cy - r) - 1; ty <= MotionMath.TileOf(cy + r) + 1; ty++)
            {
                if (!IsSolid(tx, ty, 1)) continue;
                int left = tx * 256 - 128, right = tx * 256 + 128, bottom = ty * 256 - 128, top = ty * 256 + 128;
                bool overlap = cx - r < right && cx + r > left && cy - r < top && cy + r > bottom;
                if (overlap) return true;
            }
        }
        return false;
    }

    /// <summary>Whether the capsule's circle overlaps a standing capsule (touching is allowed).</summary>
    public bool InCapsule(int cx, int cy, int r)
    {
        foreach (CapsuleObstacle c in Capsules)
        {
            long min = r + MotionMath.Quantize(c.Radius);
            long dx = cx - c.X, dy = cy - c.Y;
            if (dx * dx + dy * dy < min * min) return true;
        }
        return false;
    }
}

/// <summary>Helpers shared by the motion tests.</summary>
internal static class MotionTest
{
    public static readonly MotionParams Run = MotionParams.DefaultRun;

    public static MotionInput Frame(sbyte x, sbyte y, uint seq = 1, ushort yaw = 0) => new(seq, x, y, yaw, MotionInputFlags.None);

    public static List<MotionEvent> Step(ref MotionState state, MotionInput input, MotionParams parameters, ICollisionView view)
    {
        var events = new MotionEvents();
        MotionStep.Step(ref state, in input, in parameters, view, events);
        var list = new List<MotionEvent>();
        for (int i = 0; i < events.Count; i++) list.Add(events[i]);
        return list;
    }

    /// <summary>An FNV-1a hash of a state, for the determinism goldens.</summary>
    public static ulong Hash(ulong h, in MotionState s)
    {
        h = Mix(h, (uint)s.X);
        h = Mix(h, (uint)s.Y);
        h = Mix(h, (uint)s.Z);
        h = Mix(h, s.Yaw);
        h = Mix(h, (uint)s.Posture);
        h = Mix(h, (uint)s.Driver);
        h = Mix(h, (uint)s.Slide.TargetX);
        h = Mix(h, (uint)s.Slide.TargetY);
        h = Mix(h, s.Slide.TicksLeft);
        h = Mix(h, (uint)(ushort)s.PressTimer);
        return h;
    }

    public static ulong Mix(ulong h, uint v)
    {
        for (int i = 0; i < 4; i++)
        {
            h ^= (byte)(v >> (8 * i));
            h *= 1099511628211UL;
        }
        return h;
    }

    public const ulong FnvOffset = 14695981039346656037UL;
}

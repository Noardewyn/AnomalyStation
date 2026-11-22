using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

/// <summary>
/// What a touring bot knows about the map (M3 P5, docs/ai/specs/m3-bots.md in protocol.md section "Bots v3"): solidity, blocked
/// faces, the door tiles and the space tiles of its replica. A small interface so that the route planner is tested on plain
/// grids, with no replica and no codec.
/// </summary>
public interface IRouteMap : ISolidity
{
    /// <summary>The map's size (tiles 1..Width, 1..Height, levels 1..Levels).</summary>
    int Width { get; }

    int Height { get; }

    int Levels { get; }

    /// <summary>The face between (x, y, z) and its neighbour toward the cardinal <paramref name="dir"/> (<see cref="MotionDir"/>) is blocked (<see cref="TileFaces.IsFaceBlocked"/>).</summary>
    bool IsFaceBlocked(int x, int y, int z, byte dir);

    /// <summary>A door stands on the tile (an entity of <c>/obj/machinery/door</c>): a closed one is solid, and bumping into it opens it (tg's <c>Bumped</c>) for a bot with access.</summary>
    bool IsDoor(int x, int y, int z);

    /// <summary>The tile's turf is space (<c>/turf/open/space</c>): tg's <c>Process_Spacemove</c> holds a mob there, so a route never enters it.</summary>
    bool IsSpace(int x, int y, int z);
}

/// <summary><see cref="IRouteMap"/> over a <see cref="ReplicaWorld"/>: MapGrid version 3's faces and turf types, the entities' types for the doors.</summary>
public sealed class ReplicaRouteMap : IRouteMap
{
    /// <summary>The type path prefix of the door objects (tg's <c>/obj/machinery/door</c>: airlocks, windoors, shutters, firedoors).</summary>
    public const string DoorPrefix = "/obj/machinery/door";

    private const string SpacePrefix = "/turf/open/space";

    private readonly ReplicaWorld _replica;
    private readonly HashSet<(int X, int Y, int Z)> _doors = new();
    private long _refreshedAt = long.MinValue;

    public ReplicaRouteMap(ReplicaWorld replica)
    {
        _replica = replica ?? throw new ArgumentNullException(nameof(replica));
    }

    public int Width => _replica.Map?.Width ?? 0;

    public int Height => _replica.Map?.Height ?? 0;

    public int Levels => _replica.Map?.Levels ?? 0;

    public bool IsSolid(int x, int y, int z) => _replica.IsSolid(x, y, z);

    public bool IsFaceBlocked(int x, int y, int z, byte dir) => TileFaces.IsFaceBlocked(_replica.CollisionView, x, y, z, dir);

    public bool IsDoor(int x, int y, int z) => _doors.Contains((x, y, z));

    public bool IsSpace(int x, int y, int z)
    {
        string type = _replica.TurfTypeAt(x, y, z);
        return type != null && type.StartsWith(SpacePrefix, StringComparison.Ordinal);
    }

    /// <summary>Re-reads the door tiles from the entities when at least <paramref name="every"/> updates passed since the last time.</summary>
    public void Refresh(long now, int every = 20)
    {
        if (now - _refreshedAt < every) return;
        _refreshedAt = now;
        _doors.Clear();
        foreach (ReplicaEntity entity in _replica.Entities)
        {
            if (entity.TypePath.StartsWith(DoorPrefix, StringComparison.Ordinal))
                _doors.Add((entity.State.X, entity.State.Y, entity.State.Z));
        }
    }
}

/// <summary>A route: the tiles from the start (excluded) to the goal (included), four-connected.</summary>
public sealed class Route
{
    public Route(List<(int X, int Y)> tiles)
    {
        Tiles = tiles;
    }

    public List<(int X, int Y)> Tiles { get; }

    public int Length => Tiles.Count;
}

/// <summary>
/// The door-aware breadth-first route planner. A step from a tile to its cardinal neighbour is allowed when the face between
/// them is not blocked (the same predicate the controller uses), the neighbour is not space and is open, or is a door (a
/// closed door is solid but the route goes through it: the bot presses into the door's face, tg's <c>Bumped</c> opens it when
/// the bot has the access, and a bot without access never gets through and the stuck rule re-plans around it).
/// </summary>
public static class RoutePlanner
{
    private static readonly (int Dx, int Dy, byte Dir)[] Steps =
    {
        (0, 1, MotionDir.North), (1, 0, MotionDir.East), (0, -1, MotionDir.South), (-1, 0, MotionDir.West),
    };

    /// <summary>Whether a bot standing on (x, y) may step toward <paramref name="dir"/> (to (x + dx, y + dy)) on a route.</summary>
    public static bool CanStep(IRouteMap map, int x, int y, int z, int dx, int dy, byte dir)
    {
        int nx = x + dx, ny = y + dy;
        if (nx < 1 || ny < 1 || nx > map.Width || ny > map.Height) return false;
        if (map.IsFaceBlocked(x, y, z, dir)) return false;
        if (map.IsSpace(nx, ny, z)) return false;
        return !map.IsSolid(nx, ny, z) || map.IsDoor(nx, ny, z);
    }

    /// <summary>
    /// The nearest tile for which <paramref name="isGoal"/> is true, by breadth-first search from (sx, sy); null when none is
    /// reachable. <paramref name="avoid"/> lists tiles the route must not use (the stuck rule's); the start is never a goal.
    /// </summary>
    public static Route Nearest(IRouteMap map, int sx, int sy, int z, Func<int, int, bool> isGoal, ISet<(int, int)> avoid = null, int maxNodes = int.MaxValue)
    {
        int w = map.Width, h = map.Height;
        if (w < 1 || h < 1 || sx < 1 || sy < 1 || sx > w || sy > h) return null;
        var parent = new int[w * h];
        Array.Fill(parent, -2);
        var queue = new Queue<int>();
        int start = (sy - 1) * w + (sx - 1);
        parent[start] = -1;
        queue.Enqueue(start);
        int visited = 0;
        while (queue.Count > 0 && visited++ < maxNodes)
        {
            int at = queue.Dequeue();
            int x = at % w + 1, y = at / w + 1;
            if (at != start && isGoal(x, y))
            {
                var tiles = new List<(int X, int Y)>();
                for (int p = at; p != start; p = parent[p])
                    tiles.Add((p % w + 1, p / w + 1));
                tiles.Reverse();
                return new Route(tiles);
            }

            foreach ((int dx, int dy, byte dir) in Steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 1 || ny < 1 || nx > w || ny > h) continue;
                int next = (ny - 1) * w + (nx - 1);
                if (parent[next] != -2) continue;
                if (avoid != null && avoid.Contains((nx, ny))) continue;
                if (!CanStep(map, x, y, z, dx, dy, dir)) continue;
                parent[next] = at;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    /// <summary>The route to one tile (null when unreachable).</summary>
    public static Route To(IRouteMap map, int sx, int sy, int z, int gx, int gy, ISet<(int, int)> avoid = null) =>
        sx == gx && sy == gy ? new Route(new List<(int, int)>()) : Nearest(map, sx, sy, z, (x, y) => x == gx && y == gy, avoid);

    /// <summary>
    /// The walkable tiles reachable from (sx, sy) through open tiles and doors, never through space (the denominator of the
    /// coverage): open means not solid, not space; a door tile counts as walkable only when it is open.
    /// </summary>
    public static int CountReachable(IRouteMap map, int sx, int sy, int z, bool[] reachable = null)
    {
        int w = map.Width, h = map.Height;
        if (w < 1 || h < 1 || sx < 1 || sy < 1 || sx > w || sy > h) return 0;
        var seen = new bool[w * h];
        var queue = new Queue<int>();
        int start = (sy - 1) * w + (sx - 1);
        seen[start] = true;
        queue.Enqueue(start);
        int count = 0;
        while (queue.Count > 0)
        {
            int at = queue.Dequeue();
            int x = at % w + 1, y = at / w + 1;
            if (!map.IsSolid(x, y, z) && !map.IsSpace(x, y, z))
            {
                count++;
                if (reachable != null) reachable[at] = true;
            }

            foreach ((int dx, int dy, byte dir) in Steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 1 || ny < 1 || nx > w || ny > h) continue;
                int next = (ny - 1) * w + (nx - 1);
                if (seen[next] || !CanStep(map, x, y, z, dx, dy, dir)) continue;
                seen[next] = true;
                queue.Enqueue(next);
            }
        }

        return count;
    }
}

/// <summary>The tiles the tour has reached, shared by the bots of a run (the runner is single-threaded), and the door tiles bots stood on.</summary>
public sealed class TourCoverage
{
    private bool[] _covered;
    private bool[] _reachable;
    private int _width;
    private int _height;
    private int _coveredCount;
    private int _reachableCount;
    private readonly HashSet<(int, int)> _doors = new();

    /// <summary>Tiles reached that are walkable (open, not space).</summary>
    public int Covered => _coveredCount;

    /// <summary>Walkable tiles reachable from the first tile a bot reported (through doors, never space); 0 before one did.</summary>
    public int Reachable => _reachableCount;

    /// <summary>Covered over reachable, 0 to 1.</summary>
    public double Share => _reachableCount == 0 ? 0 : Math.Min(1.0, (double)_coveredCount / _reachableCount);

    /// <summary>Distinct door tiles a bot stood on, so a door that opened for it.</summary>
    public int DoorsPassed => _doors.Count;

    /// <summary>Marks the tile a bot stands on; the first call fixes the reachable set from there.</summary>
    public void Mark(IRouteMap map, int x, int y, int z)
    {
        if (_covered == null)
        {
            _width = map.Width;
            _height = map.Height;
            if (_width < 1 || _height < 1) return;
            _covered = new bool[_width * _height];
            _reachable = new bool[_width * _height];
            _reachableCount = RoutePlanner.CountReachable(map, x, y, z, _reachable);
        }

        if (x < 1 || y < 1 || x > _width || y > _height) return;
        int i = (y - 1) * _width + (x - 1);
        if (map.IsDoor(x, y, z)) _doors.Add((x, y));
        if (_covered[i] || !_reachable[i]) return;
        _covered[i] = true;
        _coveredCount++;
    }

    /// <summary>Whether the tile counts as reached, or is not worth reaching (not walkable or not reachable from where the tour began).</summary>
    public bool IsDone(int x, int y)
    {
        if (_covered == null || x < 1 || y < 1 || x > _width || y > _height) return false;
        int i = (y - 1) * _width + (x - 1);
        return _covered[i] || !_reachable[i];
    }
}

/// <summary>The knobs of the tour behaviour.</summary>
public sealed class TourSettings
{
    /// <summary>Ticks without moving after which the bot calls the next waypoint blocked and plans around it.</summary>
    public int BlockedTicks { get; init; } = 40;

    /// <summary>How long a blocked waypoint is avoided, in ticks.</summary>
    public int AvoidTicks { get; init; } = 600;

    /// <summary>Consecutive still ticks after which the bot counts as stuck (reported once per streak).</summary>
    public int StuckTicks { get; init; } = 600;

    /// <summary>A waypoint is reached when the centre is within this many 1/256 tile of its centre (it is a door tile or the last: the tile itself).</summary>
    public int ReachSubunits { get; init; } = 56;
}

/// <summary>
/// The tour behaviour as a function of what the bot sees (M3 P5): walk to the nearest tile no bot has reached yet (the
/// frontier of the coverage), by <see cref="RoutePlanner"/>'s door-aware routes. A closed door on the route is pressed into
/// until it opens; a bot that stays still re-plans around the tile it was pressing (the avoided tile expires), and is counted
/// stuck after <see cref="TourSettings.StuckTicks"/>. It never enters space. When nothing is left it stands still
/// (<see cref="Finished"/>).
/// </summary>
public sealed class TourPlanner
{
    private readonly TourSettings _settings;
    private readonly TourCoverage _coverage;
    private readonly Dictionary<(int, int), long> _avoidUntil = new();
    private Route _route;
    private int _index;
    private int _anchorX, _anchorY;
    private int _stillTicks;
    private int _streak;
    private long _now;
    private bool _streakCounted;

    public TourPlanner(TourCoverage coverage, TourSettings settings = null)
    {
        _coverage = coverage ?? throw new ArgumentNullException(nameof(coverage));
        _settings = settings ?? new TourSettings();
    }

    /// <summary>Routes planned.</summary>
    public int Plans { get; private set; }

    /// <summary>Re-plans because the bot stood still.</summary>
    public int Replans { get; private set; }

    /// <summary>Times the bot stood still for <see cref="TourSettings.StuckTicks"/> in a row.</summary>
    public int StuckEvents { get; private set; }

    /// <summary>The bot is in a stuck streak now.</summary>
    public bool IsStuck => _streak >= _settings.StuckTicks;

    /// <summary>No tile is left to reach (or none is reachable from here).</summary>
    public bool Finished { get; private set; }

    /// <summary>Ticks spent pressing into a closed door (the next waypoint is a solid door tile).</summary>
    public int DoorPressTicks { get; private set; }

    /// <summary>Forgets the route and the position (a new connection); the coverage stays.</summary>
    public void Reset()
    {
        _route = null;
        _index = 0;
        _stillTicks = 0;
        _streak = 0;
        _streakCounted = false;
        Finished = false;
    }

    /// <summary>One tick at update number <paramref name="now"/>; <paramref name="posX"/> and <paramref name="posY"/> are the centre in 1/256 tile.</summary>
    public BotWish Update(long now, bool havePosition, int posX, int posY, int z, IRouteMap map)
    {
        _now = now;
        if (!havePosition || map == null || map.Width < 1) return default;
        int tx = MotionMath.TileOf(posX), ty = MotionMath.TileOf(posY);
        _coverage.Mark(map, tx, ty, z);

        // Standing still: the blocked rule and the stuck streak.
        if (Math.Abs(posX - _anchorX) + Math.Abs(posY - _anchorY) > 6)
        {
            _anchorX = posX;
            _anchorY = posY;
            _stillTicks = 0;
            _streak = 0;
            _streakCounted = false;
        }
        else
        {
            _stillTicks++;
            _streak++;
            if (_streak >= _settings.StuckTicks && !_streakCounted)
            {
                StuckEvents++;
                _streakCounted = true;
            }
        }

        if (_route != null && _index < _route.Length)
        {
            (int nx, int ny) = _route.Tiles[_index];
            if (_stillTicks >= _settings.BlockedTicks)
            {
                // Pressed long enough: avoid that tile for a while and plan again.
                _avoidUntil[(nx, ny)] = now + _settings.AvoidTicks;
                Replans++;
                _stillTicks = 0;
                _route = null;
            }
        }

        if (_route == null || _index >= _route.Length || _coverage.IsDone(_route.Tiles[^1].X, _route.Tiles[^1].Y))
            Plan(map, tx, ty, z);
        if (_route == null || _route.Length == 0) return default;

        // Advance past the waypoints already reached (the centre is on them).
        while (_index < _route.Length)
        {
            (int wx, int wy) = _route.Tiles[_index];
            bool last = _index == _route.Length - 1;
            int reach = last ? _settings.ReachSubunits : _settings.ReachSubunits;
            if (Math.Abs(posX - wx * MotionMath.SubunitsPerTile) <= reach && Math.Abs(posY - wy * MotionMath.SubunitsPerTile) <= reach) _index++;
            else break;
        }

        if (_index >= _route.Length) return default;
        (int tx2, int ty2) = _route.Tiles[_index];
        if (map.IsSolid(tx2, ty2, z) && map.IsDoor(tx2, ty2, z)) DoorPressTicks++;
        double dx = tx2 * (double)MotionMath.SubunitsPerTile - posX;
        double dy = ty2 * (double)MotionMath.SubunitsPerTile - posY;
        // Walk the axis that is farthest off the tile line first, so the path along a corridor stays on its middle.
        double bearing = BotWish.BearingOf(dx, dy);
        return BotWish.Toward(bearing, MotionInput.MaxMove);
    }

    private void Plan(IRouteMap map, int tx, int ty, int z)
    {
        // Expired avoids go.
        List<(int, int)> expired = null;
        foreach (KeyValuePair<(int, int), long> a in _avoidUntil)
            if (a.Value <= _now) (expired ??= new List<(int, int)>()).Add(a.Key);
        if (expired != null)
            foreach ((int, int) key in expired) _avoidUntil.Remove(key);
        ISet<(int, int)> avoid = _avoidUntil.Count == 0 ? null : new HashSet<(int, int)>(_avoidUntil.Keys);
        Plans++;
        _route = RoutePlanner.Nearest(map, tx, ty, z, (x, y) => !_coverage.IsDone(x, y) && !map.IsSolid(x, y, z) && !map.IsSpace(x, y, z), avoid);
        _index = 0;
        Finished = _route == null && avoid == null;
    }
}

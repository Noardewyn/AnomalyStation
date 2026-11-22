using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class obj_projectile
{
    /// <summary>
    /// DEVIATION(DV527): how many ticks behind the world the shooter's client drew the others when it aimed (the click's <c>anomaly-rewind</c>,
    /// up to <see cref="ShotRewind.MaxTicks"/>); 0: the shot is tested against where they are.
    /// </summary>
    public float dm_rewind;
}

/// <summary>
/// DEVIATION(DV527): lag compensation of aimed shots. A client draws the other mobs as they were a render delay and its latency ago, and its
/// click reaches the server later still: a shot aimed at a running mob's drawn body went where the body had been. Each tick the walking
/// capsules' points are kept for <see cref="MaxTicks"/> ticks; an aimed shot whose click said how far behind its client drew
/// (<see cref="obj_projectile.dm_rewind"/>) tests a capsule mob at the point it had that many ticks before each tick of its flight
/// (interpolated as the client interpolates, <c>SampleInterpolator</c>), and only on the turf of that point: there the crossing (DV95)
/// decides, and the mob's body where it stands now neither blocks the shot nor is hit.
/// </summary>
public static class ShotRewind
{
    /// <summary>
    /// The most a shot is tested in the past: 12 ticks, 0.6 s. A client draws the others about 0.25 s behind the server's newest tick at a
    /// 0.13 s round trip (its render delay, the way here, its clock's margin), and the click takes the way back and the input buffer on top.
    /// </summary>
    public const int MaxTicks = 12;

    /// <summary>A move of more than this many tiles between two kept ticks is a jump (a teleport, a level change), not drawn between them.</summary>
    public const double JumpTiles = 2.5;

    /// <summary>The world phase that keeps the points, after the locomotion's follow phase.</summary>
    public const string PhaseName = "shot rewind";

    private const int Kept = MaxTicks + 2;

    private readonly record struct Point(long Tick, int X, int Y, int Z);

    private sealed class History
    {
        internal readonly SortedDictionary<long, Point[]> Points = new();
        internal readonly Dictionary<long, int> Next = new();
        internal long LastTick = -1;
    }

    private static History Of(World world) => world.GetExtension(static _ => new History());

    /// <summary>Keeps the points of the walking capsules at the end of this tick (the locomotion's follow phase, after every capsule followed its mob).</summary>
    internal static void Record(World world, IReadOnlyDictionary<long, LocomotionSeam.Capsule> capsules)
    {
        History h = Of(world);
        h.LastTick = world.Tick;
        foreach (long id in h.Points.Keys.Where(id => !capsules.ContainsKey(id)).ToList())
        {
            h.Points.Remove(id);
            h.Next.Remove(id);
        }
        foreach ((long id, LocomotionSeam.Capsule c) in capsules)
        {
            if (!h.Points.TryGetValue(id, out Point[] ring))
            {
                h.Points[id] = ring = new Point[Kept];
                h.Next[id] = 0;
            }
            int next = h.Next[id];
            ring[next] = new Point(world.Tick, c.State.X, c.State.Y, c.State.Z);
            h.Next[id] = (next + 1) % Kept;
        }
    }

    /// <summary>
    /// The centre (absolute tile pixels) mob <paramref name="mobId"/> had at <paramref name="tick"/> (a fraction between two kept ticks is
    /// interpolated); false when the tick is the last kept one or later (its point now applies) or nothing was kept of it.
    /// </summary>
    public static bool TryPointAt(World world, long mobId, double tick, out double x, out double y, out int z)
    {
        x = y = 0;
        z = 0;
        History h = Of(world);
        if (tick >= h.LastTick || !h.Points.TryGetValue(mobId, out Point[] ring))
            return false;
        Point before = default, after = default;
        bool hasBefore = false, hasAfter = false;
        foreach (Point p in ring)
        {
            if (p.Tick == 0) // a slot not filled yet (the world's ticks start at 1)
                continue;
            if (p.Tick <= tick && (!hasBefore || p.Tick > before.Tick))
                (before, hasBefore) = (p, true);
            if (p.Tick >= tick && (!hasAfter || p.Tick < after.Tick))
                (after, hasAfter) = (p, true);
        }
        if (!hasBefore && !hasAfter)
            return false;
        Point a = hasBefore ? before : after, b = hasAfter ? after : before; // older than the oldest kept: the oldest
        double f = b.Tick > a.Tick ? (tick - a.Tick) / (b.Tick - a.Tick) : 0;
        if (a.Z != b.Z || Math.Abs(b.X - a.X) > JumpTiles * 256 || Math.Abs(b.Y - a.Y) > JumpTiles * 256)
            f = f < 0.5 ? 0 : 1;
        x = (a.X + (b.X - a.X) * f) / 8.0;
        y = (a.Y + (b.Y - a.Y) * f) / 8.0;
        z = f < 0.5 ? a.Z : b.Z;
        return true;
    }

    /// <summary>The tick <paramref name="p"/> tests the others at now (its rewind before the world's tick), or null for a shot that is not rewound.</summary>
    public static double? TickOf(obj_projectile p) => p.dm_aimed && p.dm_rewind > 0 && World.Current is World world ? world.Tick - p.dm_rewind : null;

    /// <summary>The centre <paramref name="p"/> tests <paramref name="target"/> at (absolute tile pixels, z), when the shot is rewound and the mob kept.</summary>
    public static bool TryPointFor(obj_projectile p, mob_living target, out double x, out double y, out int z)
    {
        x = y = 0;
        z = 0;
        return TickOf(p) is double tick && TryPointAt(World.Current, target.dm_id, tick, out x, out y, out z);
    }

    /// <summary>Whether a rewound shot meets <paramref name="target"/>'s point of then on <paramref name="here"/> (the mob where it stands now, when nothing was kept).</summary>
    public static bool SeenOn(obj_projectile p, mob_living target, turf here)
    {
        if (here == null || !TryPointFor(p, target, out double x, out double y, out int z))
            return true;
        return z == (int)Dm.num(here.z) && TileOf(x) == (int)Dm.num(here.x) && TileOf(y) == (int)Dm.num(here.y);
    }

    /// <summary>Whether <paramref name="p"/> is rewound and <paramref name="target"/> has kept points: its body now does not block the shot.</summary>
    public static bool PassesNow(obj_projectile p, atom target)
        => target is mob_living living && TickOf(p) is double tick && TryPointAt(World.Current, living.dm_id, tick, out _, out _, out _);

    /// <summary>The kept mobs a rewound shot meets on <paramref name="here"/> that stand somewhere else now (those standing there are its contents).</summary>
    public static List<mob_living> ElsewhereSeenOn(obj_projectile p, turf here)
    {
        var found = new List<mob_living>();
        if (here == null || TickOf(p) is not double tick)
            return found;
        World world = World.Current;
        int hx = (int)Dm.num(here.x), hy = (int)Dm.num(here.y), hz = (int)Dm.num(here.z);
        foreach (long id in Of(world).Points.Keys)
        {
            if (!TryPointAt(world, id, tick, out double x, out double y, out int z) || z != hz || TileOf(x) != hx || TileOf(y) != hy)
                continue;
            if (world.Objects.TryGet(id, out object o) && o is mob_living mob && !QDELETED(mob) && mob.loc != here)
                found.Add(mob);
        }
        return found;
    }

    private static int TileOf(double pixels) => (int)Math.Floor(pixels / ShotGeometry.PixelsPerTile + 0.5);
}

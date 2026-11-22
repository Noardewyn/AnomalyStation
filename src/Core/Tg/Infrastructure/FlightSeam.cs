using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV416): the owner (2026-11-13), revising D127: throws are smooth and not by the tile; things fly by physics and fall correctly on a
// station of several levels, and explosions and the air move everything smoothly. Host code beside tg's throwing: a thrown thing that is no
// mob flies a ballistic arc (FlightArc) instead of tg's tile steps; every tile it crosses is still tg's own Move (so Bump, Cross, hitby,
// catching and every throw_impact are tg's), tg's per-tick checks of /datum/thrownthing/tick stand, and it lands (tg's finalize) when it
// touches the floor instead of at the target's tile or at maxrange. Open space under it lets it fall to the level below on its way; a
// ceiling turns it back down. After a hit in the air it falls where it was; the air's push slides a thing along the floor and friction stops
// it. Where it stops is kept as a rest point (atom_movable.dm_flight), drawn there until tg moves it again.

public partial class atom_movable
{
    /// <summary>DV416: the free motion the clients draw (protocol 81): a moving segment, a rest point, or none (drawn on its tile).</summary>
    public FlightArc dm_flight;
}

/// <summary>DV416: the free-motion bodies of a world (a Kernel extension): their physics, their tile crossings through tg, and what the replicator reads.</summary>
public sealed class FlightSeam
{
    /// <summary>The name of the world phase that moves the bodies no throw drives (a fall after a hit, a slide).</summary>
    public const string PhaseName = "flight";

    /// <summary>tg's throw speed is tiles a decisecond (30 tiles, 60 m a second for an item's 3); the 3D throw flies at this share of it.</summary>
    public const float ThrowPace = 0.4f;

    /// <summary>Tile pixels to a metre (a 2 m tile is 32 pixels high: ShotGeometry's heights).</summary>
    public const float PixelsPerMetre = 16f;

    /// <summary>Gravity, metres a second squared.</summary>
    public const float GravityMetres = 9.8f;

    /// <summary>The highest a point flies under a ceiling (tile pixels; a pixel under the level's top, <see cref="ShotGeometry.CeilingPixels"/>).</summary>
    public static float CeilingPixels => ShotGeometry.CeilingPixels - 1f;

    /// <summary>A table's top (0.75 m, the client's WorldScale.TableTopHeight): a thing lands on it.</summary>
    public const float TableTopPixels = 12f;

    /// <summary>Where a standing thrower lets go (about the hand: 1 m), and a lying one.</summary>
    public const float HandPixels = 16f, LyingHandPixels = 4f;

    /// <summary>Where a throw at a standing mob aims (ShotGeometry's unaimed height, a mob's middle), and at a lying one.</summary>
    public const float MobTargetPixels = ShotGeometry.UnaimedHeightPixels, LyingTargetPixels = 3f;

    /// <summary>The shortest flight (ticks): a throw at the own feet still leaves the hand.</summary>
    public const double MinFlightTicks = 3;

    /// <summary>The least a thing thrown from the floor rises (a blast's, a machine's: tg's flat speed would skid it; tile pixels, 0.4 m).</summary>
    public const float FloorThrowPeakPixels = 6f;

    /// <summary>How far from a wall's face a thing that hit it falls (tiles).</summary>
    public const float WallMargin = 0.2f;

    /// <summary>How much of its upward speed a thing keeps when a ceiling turns it back.</summary>
    public const float CeilingBounce = 0.3f;

    /// <summary>The floor's friction on a sliding thing, tiles a second squared.</summary>
    public const float SlideFrictionTiles = 2.4f;

    /// <summary>Ticks a big thing (no item) takes to settle from where it stopped onto its tile's middle.</summary>
    public const double CentreTicks = 4;

    private const double Epsilon = 1e-7;

    private enum Kind : byte { Throw, Fall, Slide }

    private sealed class Body
    {
        public atom_movable Thing;
        public FlightArc Arc;
        public Kind Kind;
        public double Clock; // the tick the arc has been followed to
        public datum_thrownthing Throw;
        public bool Bridging; // the seam is moving it through tg now: its own Moved does not end it
        public bool Centring; // a big thing's last slide onto its tile's middle (no tg moves)
        public float ImpactLevels; // DV435: tg's fall it is in: the levels fallen, tg's zImpact due at the floor it falls to (0: none)
        public turf ImpactFrom; // DV435: the turf it fell from (zImpact's prev_turf)
    }

    private readonly SortedDictionary<long, Body> _bodies = new();
    private readonly Dictionary<long, atom_movable> _announced = new();
    private readonly Dictionary<long, (long Tick, double East, double North, double Up, double Yaw, double Pitch)> _aims = new();

    /// <summary>The world's seam; the first use adds the flight phase and the hash contributor.</summary>
    public static FlightSeam Of(World world) => world.GetExtension(static w =>
    {
        var seam = new FlightSeam();
        w.AddPhase(PhaseName, RunPhase);
        w.AddStateHasher(nameof(FlightSeam), (World _, ref StateHasher h) => seam.Hash(ref h));
        return seam;
    });

    /// <summary>The movables whose motion changed since the replicator last read them, and every moving one.</summary>
    public IReadOnlyCollection<long> Announced => _announced.Keys;

    /// <summary>The replicator read <paramref name="id"/> at rest or with none: it is no longer polled.</summary>
    public void Forget(long id)
    {
        if (!_bodies.ContainsKey(id))
            _announced.Remove(id);
    }

    /// <summary>Whether the seam drives <paramref name="thing"/> now.</summary>
    public bool IsFlying(atom_movable thing) => thing != null && _bodies.ContainsKey(thing.dm_id);

    /// <summary>Whether a throw of <paramref name="thing"/> flies an arc: everything, mobs too (stage 2: a capsule mob's capsule follows the arc).</summary>
    public static bool Flies(atom_movable thing) => thing != null;

    /// <summary>How far under the ceiling a mob's feet stay in flight (a thrown body tumbles about half a metre high; tile pixels).</summary>
    public const float MobHeadroomPixels = 8f;

    /// <summary>DV94's camera aim of a click by <paramref name="clicker"/>, kept for a throw the click makes this tick.</summary>
    public static void NoteAim(mob clicker, DmList modifiers)
    {
        if (clicker == null || !ClientSeam.TryReadAim(modifiers, out double east, out double north, out double up, out double yaw, out double pitch))
            return;
        World world = World.Current;
        Of(world)._aims[clicker.dm_id] = (world.Tick, east, north, up, yaw, pitch);
    }

    /// <summary>tg moved <paramref name="thing"/> itself (a pick-up, a push, a teleport): the seam lets it go and its rest point is gone.</summary>
    public static void NoteMoved(atom_movable thing)
    {
        if (thing.dm_flight.IsNone)
            return;
        FlightSeam seam = Of(World.Current);
        if (seam._bodies.TryGetValue(thing.dm_id, out Body b) && b.Bridging)
            return;
        seam._bodies.Remove(thing.dm_id);
        seam.SetArc(thing, FlightArc.None);
    }

    /// <summary>
    /// DEVIATION(DV429): <paramref name="thing"/> is drawn at a point another motion carries it to (the AI's hologram where its eye flies), moving
    /// on level at (<paramref name="vx"/>, <paramref name="vy"/>) tiles a tick from this tick; with no speed it is a rest point. tg's own moves of it
    /// clear it as they clear any rest point (<see cref="NoteMoved"/>), so it is set after them. <see cref="FlightArc.None"/>: drawn on its tile.
    /// </summary>
    public static void Glide(atom_movable thing, FlightArc arc)
    {
        FlightSeam seam = Of(World.Current);
        if (!seam._bodies.ContainsKey(thing.dm_id))
            seam.SetArc(thing, arc);
    }

    // ---- the throw: /datum/thrownthing/tick's tile loop, replaced ----

    /// <summary>
    /// DV416: one tick of <paramref name="tt"/>'s flight, in place of tg's tile loop (throwing.dm:157-198): the first call launches the arc,
    /// each call follows it to the end of this tick and makes tg's move at every tile it crosses, with tg's checks around each move.
    /// </summary>
    public static async DmTask ThrowTick(datum_thrownthing tt)
    {
        World world = World.Current;
        FlightSeam seam = Of(world);
        atom_movable am = tt.thrownthing;
        if (!seam._bodies.TryGetValue(am.dm_id, out Body body) || body.Throw != tt)
            body = seam.Launch(tt, world);
        tt.last_move = world.Time;

        // tg: to catch sneaky things moving on our tile while we slept (throwing.dm:150-158)
        if (Dm.truthy(tt.dist_travelled) && await seam.HitOnTile(tt))
            return;
        await seam.Advance(body, world.Tick);
    }

    private Body Launch(datum_thrownthing tt, World world)
    {
        atom_movable am = tt.thrownthing;
        double now = world.Tick - 1; // as if let go at the tick's start, so the first tick (quickstart) already moves it
        (double sx, double sy, double sl) = PointNow(am, world.Tick);
        atom thrower = tt.get_thrower() as atom;
        (long Tick, double East, double North, double Up, double Yaw, double Pitch) aim = default;
        bool aimed = thrower is mob tm && _aims.TryGetValue(tm.dm_id, out aim) && world.Tick - aim.Tick <= 1;
        bool fromHand = false;
        if (thrower is mob_living m && ReferenceEquals(get_turf(m), get_turf(am)) && !_bodies.ContainsKey(am.dm_id))
        {
            fromHand = true;
            if (LocomotionSeam.TryGetState(world, m.dm_id, out MotionState state, out _))
                (sx, sy) = (state.X / (double)MotionMath.SubunitsPerTile, state.Y / (double)MotionMath.SubunitsPerTile);
            else
                (sx, sy) = (Dm.num(m.x), Dm.num(m.y));
            bool lying = m.body_position == LYING_DOWN;
            sl = aimed ? Math.Clamp(aim.Up - 4, 2, CeilingPixels) : lying ? LyingHandPixels : HandPixels;
        }

        atom target = tt.initial_target?.resolve() as atom ?? tt.target_turf;
        (double px, double py, double pl) = TargetPoint(target, aimed, aim);
        if (ismob(am))
            pl = Math.Min(pl, CeilingOf(am)); // a body's point is its feet: at most its headroom under the ceiling
        double dx = px - sx, dy = py - sy;
        // tg counts a diagonal step as one tile: the reach is maxrange steps
        double cheb = Math.Max(Math.Abs(dx), Math.Abs(dy)), range = Math.Max(0, Dm.num(tt.maxrange));
        if (cheb > range)
        {
            double k = range / cheb;
            (dx, dy, pl) = (dx * k, dy * k, 0);
        }
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double tilesPerTick = Math.Max(0.01, Dm.num(tt.speed) * ThrowPace * World.TickLag);
        bool gravity = Dm.truthy(am.has_gravity(get_turf(am) as turf));
        double g = gravity ? Gravity(world) : 0;
        // a throw from the floor hops at least FloorThrowPeakPixels high (its peak g T^2 / 8 over a level flight of T ticks)
        double least = gravity && !fromHand ? Math.Sqrt(8 * FloorThrowPeakPixels / g) : MinFlightTicks;
        double ticks = Math.Max(least, dist / tilesPerTick);
        double vl = gravity ? (pl - sl) / ticks + 0.5 * g * ticks : 0; // weightless: it flies on level, as tg's throw in space flies on
        var body = new Body
        {
            Thing = am,
            Kind = Kind.Throw,
            Throw = tt,
            Clock = now,
            Arc = new FlightArc(now, (float)sx, (float)sy, (int)Dm.num(am.z), (float)sl, (float)(dx / ticks), (float)(dy / ticks), (float)vl, (float)g),
        };
        _bodies[am.dm_id] = body;
        SetArc(am, body.Arc);
        return body;
    }

    // Where the throw goes: the crosshair's point on the target (DV94's aim of the click), else the target's middle (a mob's at its height)
    private static (double X, double Y, double Lift) TargetPoint(atom target, bool aimed, (long Tick, double East, double North, double Up, double Yaw, double Pitch) aim)
    {
        double tx = Dm.num(target?.x), ty = Dm.num(target?.y);
        if (target is atom_movable tm && !tm.dm_flight.IsNone && !tm.dm_flight.IsMoving)
            (tx, ty) = (tm.dm_flight.X0, tm.dm_flight.Y0);
        double lift = target is mob_living ml ? (ml.body_position == LYING_DOWN ? LyingTargetPixels : MobTargetPixels) : 0;
        if (!aimed)
            return (tx, ty, lift);
        double yaw = aim.Yaw * Math.PI / 180, pitch = aim.Pitch * Math.PI / 180;
        double ex = aim.East / FlightArc.PixelsPerTile, ey = aim.North / FlightArc.PixelsPerTile; // the eye, tiles
        double ux = Math.Sin(yaw), uy = Math.Cos(yaw);
        double along;
        if (target is turf t && !Dm.truthy(t.density) && aim.Pitch < -1)
        {
            // the floor the crosshair is on: where its ray meets the floor, kept on that tile
            along = aim.Up / Math.Tan(-pitch) / FlightArc.PixelsPerTile;
            double fx = Math.Clamp(ex + ux * along, tx - 0.49, tx + 0.49), fy = Math.Clamp(ey + uy * along, ty - 0.49, ty + 0.49);
            return (fx, fy, 0);
        }
        // a thing or a wall: the crosshair's point as far off as its middle
        along = Math.Sqrt((tx - ex) * (tx - ex) + (ty - ey) * (ty - ey));
        double up = Math.Clamp(aim.Up + along * FlightArc.PixelsPerTile * Math.Tan(pitch), 0, CeilingPixels);
        return (ex + ux * along, ey + uy * along, target is mob_living ? up : Math.Max(lift, up));
    }

    // tg: throwing.dm:150-158, the obstacle check at a tick's start
    private async DmTask<bool> HitOnTile(datum_thrownthing tt)
    {
        atom_movable am = tt.thrownthing;
        atom_movable actual_target = tt.initial_target?.resolve() as atom_movable;
        atom thrower = tt.get_thrower() as atom;
        foreach (atom_movable obstacle in Dm.each_cast<atom_movable>(get_turf(am)))
        {
            if (obstacle == am || (obstacle == thrower && !ismob(am)))
                continue;
            if (ismob(obstacle) && Dm.truthy(Dm.bitand(am.pass_flags, PASSMOB)) && (obstacle != actual_target))
                continue;
            if (Dm.truthy(Dm.bitand(obstacle.pass_flags_self, LETPASSTHROW)))
                continue;
            if (obstacle == actual_target || (Dm.truthy(obstacle.density) && !Dm.truthy(Dm.bitand(obstacle.flags_1, ON_BORDER_1)) && !Dm.@in(obstacle, am.buckled_mobs) && !Dm.@in(am, obstacle.buckled_mobs)))
            {
                await Finalize(tt, TRUE, obstacle);
                return true;
            }
        }
        return false;
    }

    // ---- the physics ----

    private static double Gravity(World world)
    {
        double secondsPerTick = World.TickLag / 10.0;
        return GravityMetres * PixelsPerMetre * secondsPerTick * secondsPerTick;
    }

    private static double Friction(World world)
    {
        double secondsPerTick = World.TickLag / 10.0;
        return SlideFrictionTiles * secondsPerTick * secondsPerTick;
    }

    // Follows the body's arc from its clock to `until`, stopping at each event (a tile crossing, the floor, the ceiling) to act on it.
    private async DmTask Advance(Body body, double until)
    {
        for (int guard = 0; guard < 256 && body.Clock < until - Epsilon; guard++)
        {
            atom_movable am = body.Thing;
            if (QDELETED(am) || am.loc is not turf here)
            {
                Drop(body);
                return;
            }
            FlightArc arc = body.Arc;
            int tileX = (int)Dm.num(here.x), tileY = (int)Dm.num(here.y);
            (double x, double y, double lift) = arc.At(body.Clock);
            (double vx, double vy, double vl) = arc.VelocityAt(body.Clock);

            // the next tile edge on each axis, the floor and the ceiling
            double tx = EdgeTime(arc, body.Clock, x, vx, tileX, true);
            double ty = EdgeTime(arc, body.Clock, y, vy, tileY, false);
            double floor = FloorLift(here);
            double tf = LiftTime(arc, body.Clock, lift, floor, down: true);
            bool roofed = Roofed(here);
            double tc = roofed ? LiftTime(arc, body.Clock, lift, CeilingOf(am), down: false)
                : GET_TURF_ABOVE(here) is turf ? LiftTime(arc, body.Clock, lift, FlightArc.PixelsPerTile, down: false)
                : double.PositiveInfinity; // nothing above (outside the station): nothing stops it
            double next = Math.Min(Math.Min(tx, ty), Math.Min(tf, tc));
            if (body.Centring)
                next = double.PositiveInfinity; // within its own tile, no events

            if (next > until)
            {
                body.Clock = until;
                PlaceCapsule(body);
                if (!arc.IsMoving || (body.Kind != Kind.Throw && arc.Gravity == 0 && arc.VLift == 0 && body.Clock - arc.StartTick >= arc.StopTicks))
                    await Rest(body);
                return;
            }
            body.Clock = Math.Max(body.Clock, next);
            if (next == tf)
                await Floor(body, here);
            else if (next == tc)
                await Ceiling(body, here, roofed);
            else
            {
                int dirX = next == tx ? Math.Sign(vx) : 0, dirY = next == ty ? Math.Sign(vy) : 0;
                if (dirX != 0 && dirY == 0 && Math.Abs(ty - tx) < 1e-6)
                    dirY = Math.Sign(vy);
                else if (dirY != 0 && dirX == 0 && Math.Abs(tx - ty) < 1e-6)
                    dirX = Math.Sign(vx);
                await Cross(body, dirX, dirY);
            }
        }
    }

    // When the point reaches the edge of its tile on one axis (infinity: never in this segment)
    private static double EdgeTime(FlightArc arc, double clock, double pos, double v, int tile, bool xAxis)
    {
        if (Math.Abs(v) < 1e-9)
            return double.PositiveInfinity;
        double edge = tile + (v > 0 ? 0.5 : -0.5);
        double need = edge - pos; // the way left to the edge along this axis
        double speed0 = xAxis ? arc.VX : arc.VY;
        if (arc.Friction <= 0f)
            return clock + Math.Max(0, need / v);
        // with friction the axis covers speed0 * travel, travel(t) = t - f t^2 / (2 s)
        double s = Math.Sqrt((double)arc.VX * arc.VX + (double)arc.VY * arc.VY);
        double startPos = (xAxis ? arc.X0 : arc.Y0);
        double travel = (edge - startPos) / speed0;
        double disc = 1 - 2 * arc.Friction * travel / s;
        if (disc < 0)
            return double.PositiveInfinity;
        double t = (1 - Math.Sqrt(disc)) * s / arc.Friction;
        return Math.Max(clock, arc.StartTick + t);
    }

    // When the lift reaches `level` going down (or up): infinity if it does not
    private static double LiftTime(FlightArc arc, double clock, double lift, double level, bool down)
    {
        double g = arc.Gravity, l0 = arc.Lift0 - level, vl = arc.VLift;
        double vNow = vl - g * (clock - arc.StartTick);
        if (down && lift <= level + Epsilon)
            return vNow < -1e-9 || (vNow <= 0 && g > 0) ? clock : double.PositiveInfinity; // a thing resting on it is no event
        if (!down && lift >= level - Epsilon)
            return vNow > 1e-9 ? clock : double.PositiveInfinity;
        // l0 + vl t - g t^2 / 2 = 0
        double t;
        if (g == 0)
        {
            if (vl == 0)
                return double.PositiveInfinity;
            t = -l0 / vl;
        }
        else
        {
            double disc = vl * vl + 2 * g * l0;
            if (disc < 0)
                return double.PositiveInfinity;
            double r = Math.Sqrt(disc);
            t = down ? (vl + r) / g : (vl - r) / g;
        }
        double at = arc.StartTick + t;
        return at >= clock - Epsilon ? Math.Max(clock, at) : double.PositiveInfinity;
    }

    private static double FloorLift(turf here)
    {
        foreach (obj_structure_table _ in Dm.each<obj_structure_table>(here))
            return TableTopPixels;
        return 0;
    }

    /// <summary>DV425: whether <paramref name="here"/> opens up into the level above (a hole or the stairs' opening over it, no ceiling).</summary>
    internal static bool OpensUp(turf here)
    {
        int z = (int)Dm.num(here?.z);
        if (z < 1 || z > Dm.num(length(SSmapping.multiz_levels)))
            return false; // a level tg's multi-z does not know (a test world's, nullspace): nothing above it
        return GET_TURF_ABOVE(here) is turf && !Roofed(here);
    }

    // Whether the level above shuts the tile in: the port's ceiling (D295), or a floor of the level above that nothing passes
    private static bool Roofed(turf here)
    {
        if (CeilingSystem.Of(World.Current) is { Initialized: true } ceilings && ceilings.At((int)Dm.num(here.x), (int)Dm.num(here.y), (int)Dm.num(here.z)).Exists)
            return true;
        if (GET_TURF_ABOVE(here) is not turf above)
            return false;
        return !(Dm.truthy(here.zPassOut(UP)) && Dm.truthy(above.zPassIn(UP)));
    }

    private void Restart(Body body, double x, double y, int z, double lift, double vx, double vy, double vl, double g, double friction = 0)
    {
        body.Arc = new FlightArc(body.Clock, (float)x, (float)y, z, (float)lift, (float)vx, (float)vy, (float)vl, (float)g, (float)friction);
        SetArc(body.Thing, body.Arc);
    }

    private async DmTask Cross(Body body, int dirX, int dirY)
    {
        atom_movable am = body.Thing;
        (double x, double y, double lift) = body.Arc.At(body.Clock);
        (double vx, double vy, double vl) = body.Arc.VelocityAt(body.Clock);
        float dir = (dirX > 0 ? EAST : dirX < 0 ? WEST : 0) + (dirY > 0 ? NORTH : dirY < 0 ? SOUTH : 0);
        turf from = am.loc as turf;
        turf to = get_step(am, dir) as turf;
        bool moved = false;
        datum_thrownthing tt = body.Throw;
        if (to == null)
        {
            // tg: going off the edge of the map makes get_step return null, don't let things go off the edge
            if (tt != null)
                await Finalize(tt);
            else
                StopAt(body, x, y, lift, Math.Min(vl, 0), dirX, dirY);
            return;
        }
        body.Bridging = true;
        try
        {
            moved = Dm.truthy(tt != null
                ? await am.Move(to, dir, DELAY_TO_GLIDE_SIZE(1 / Math.Max(0.01f, Dm.num(tt.speed))))
                : await am.Move(to, dir));
        }
        finally
        {
            body.Bridging = false;
        }
        if (QDELETED(am) || am.loc is not turf now)
        {
            Drop(body);
            return;
        }
        if (!moved || !ReferenceEquals(now, to))
        {
            int fell = LevelsBelow(now, to);
            if (fell > 0)
            {
                // tg's own fall as it entered (open space under a thing no throw holds): it falls on from where it was
                Fall(body, x, y, lift + fell * FlightArc.PixelsPerTile, vx, vy, vl, now);
                return;
            }
            fell = LevelsBelow(now, from);
            if (!ReferenceEquals(now, from) && fell == 0)
            {
                Drop(body); // something else put it elsewhere
                return;
            }
            // tg: we hit something during our move... but finalize() wasn't called on Bump() because of a higher level definition
            if (tt != null && Dm.truthy(am.throwing))
                await Finalize(tt);
            if (_bodies.ContainsKey(am.dm_id) && am.loc is turf still && (ReferenceEquals(still, from) || LevelsBelow(still, from) > 0))
            {
                if (tt == null && ReferenceEquals(still, from))
                    am.dm_slide_stop = StopKey(from, dir);
                StopAt(body, x, y, lift + LevelsBelow(still, from) * FlightArc.PixelsPerTile, Math.Min(vl, 0), dirX, dirY);
            }
            return;
        }
        if (tt != null)
        {
            tt.dist_travelled++;
            atom_movable actual_target = tt.initial_target?.resolve() as atom_movable;
            if (Dm.truthy(actual_target) && !Dm.truthy(Dm.bitand(actual_target.pass_flags_self, LETPASSTHROW)) && actual_target.loc == am.loc)
            {
                await Finalize(tt, TRUE, actual_target);
                return;
            }
            if (tt.dist_travelled > datum_controller_subsystem_throwing.MAX_THROWING_DIST)
            {
                await Finalize(tt);
                return;
            }
        }
        // the air changes with the tile: gravity on or off; a slide off a table's edge, or over open space, falls
        bool gravity = Dm.truthy(am.has_gravity(now));
        if (body.Kind == Kind.Slide)
        {
            if (gravity && (lift > FloorLift(now) + 0.5 || OpenBelow(am, now)))
                Fall(body, x, y, lift, vx, vy, 0, now);
            return;
        }
        if (gravity != (body.Arc.Gravity > 0))
            Restart(body, x, y, body.Arc.Z, lift, vx, vy, gravity ? vl : 0, gravity ? Gravity(World.Current) : 0);
    }

    // A tile and a way out of it, as one number (atom_movable.dm_slide_stop)
    private static long StopKey(turf t, float dir) => ((long)(byte)dir << 56) | ((long)Dm.num(t.z) << 40) | ((long)Dm.num(t.y) << 20) | (uint)Dm.num(t.x);

    // Levels `now` lies under `above` in one column (0: not under it)
    private static int LevelsBelow(turf now, turf above)
    {
        if (now == null || above == null || Dm.num(now.x) != Dm.num(above.x) || Dm.num(now.y) != Dm.num(above.y))
            return 0;
        return Math.Max(0, (int)(Dm.num(above.z) - Dm.num(now.z)));
    }

    private static bool OpenBelow(atom_movable am, turf here) => GET_TURF_BELOW(here) is turf below && Dm.truthy(am.can_z_move(DOWN, here, below, 0));

    // It leaves the floor (or the throw let it go in the air): gravity takes it, its way kept, no friction
    private void Fall(Body body, double x, double y, double lift, double vx, double vy, double vl, turf here)
    {
        body.Kind = body.Throw != null ? Kind.Throw : Kind.Fall;
        Restart(body, x, y, (int)Dm.num(here.z), lift, vx, vy, vl, Dm.truthy(body.Thing.has_gravity(here)) ? Gravity(World.Current) : 0);
    }

    // It met a wall or a thing on its way: it stops there, short of the face, and falls on if it is in the air (its fall kept)
    private void StopAt(Body body, double x, double y, double lift, double vl, int dirX, int dirY)
    {
        atom_movable am = body.Thing;
        int tileX = (int)Dm.num(am.x), tileY = (int)Dm.num(am.y);
        if (dirX != 0)
            x = tileX + dirX * (0.5 - WallMargin);
        if (dirY != 0)
            y = tileY + dirY * (0.5 - WallMargin);
        bool gravity = Dm.truthy(am.has_gravity(am.loc as turf));
        body.Kind = Kind.Fall;
        body.Throw = null;
        Restart(body, x, y, (int)Dm.num(am.z), lift, 0, 0, gravity ? vl : 0, gravity ? Gravity(World.Current) : 0);
    }

    private async DmTask Floor(Body body, turf here)
    {
        atom_movable am = body.Thing;
        (double x, double y, double lift) = body.Arc.At(body.Clock);
        (double vx, double vy, double vl) = body.Arc.VelocityAt(body.Clock);
        if (body.ImpactLevels > 0)
        {
            // DV435: tg's zImpact as it reaches the floor it fell to: it falls on (tg's zFall to the next level), something takes it, or it lands (onZImpact)
            float levels = body.ImpactLevels;
            turf from = body.ImpactFrom;
            (body.ImpactLevels, body.ImpactFrom) = (0, null);
            body.Bridging = true;
            try
            {
                await here.zImpact(am, levels, from);
            }
            finally
            {
                body.Bridging = false;
            }
            if (body.ImpactLevels > 0 || !_bodies.ContainsKey(am.dm_id))
                return; // it falls on below, or tg moved it
            if (QDELETED(am) || !ReferenceEquals(am.loc, here))
            {
                Drop(body);
                return;
            }
            Restart(body, x, y, body.Arc.Z, lift, 0, 0, 0, 0); // it lies where it landed (Rest)
            return;
        }
        // open space under it: it goes on falling on the level below (tg's throw does not fall until it ends: can_z_move's ZMOVE_FALL_CHECKS)
        if (lift <= Epsilon && OpenBelow(am, here) && GET_TURF_BELOW(here) is turf below)
        {
            body.Bridging = true;
            try
            {
                await am.zMove(DOWN, below, 0);
            }
            finally
            {
                body.Bridging = false;
            }
            if (QDELETED(am) || !ReferenceEquals(am.loc, below))
            {
                Drop(body);
                return;
            }
            Restart(body, x, y, (int)Dm.num(below.z), lift + FlightArc.PixelsPerTile, vx, vy, vl, body.Arc.Gravity, body.Arc.Friction);
            return;
        }
        if (body.Throw is datum_thrownthing tt)
        {
            // tg's landing: finalize (throw_impact on the turf, the drift, zFall, the landed signals)
            Restart(body, x, y, body.Arc.Z, lift, 0, 0, 0, 0);
            await Finalize(tt);
            return;
        }
        Restart(body, x, y, body.Arc.Z, lift, vx, vy, 0, 0, body.Kind == Kind.Slide ? body.Arc.Friction : Friction(World.Current));
        body.Kind = Kind.Slide;
    }

    private async DmTask Ceiling(Body body, turf here, bool roofed)
    {
        atom_movable am = body.Thing;
        (double x, double y, double lift) = body.Arc.At(body.Clock);
        (double vx, double vy, double vl) = body.Arc.VelocityAt(body.Clock);
        if (!roofed && GET_TURF_ABOVE(here) is turf above && Dm.truthy(am.can_z_move(UP, here, above, 0)))
        {
            body.Bridging = true;
            try
            {
                await am.zMove(UP, above, 0);
            }
            finally
            {
                body.Bridging = false;
            }
            if (QDELETED(am) || !ReferenceEquals(am.loc, above))
            {
                Drop(body);
                return;
            }
            Restart(body, x, y, (int)Dm.num(above.z), lift - FlightArc.PixelsPerTile, vx, vy, vl, body.Arc.Gravity, body.Arc.Friction);
            return;
        }
        Restart(body, x, y, body.Arc.Z, Math.Min(lift, CeilingOf(am)), vx, vy, -Math.Abs(vl) * CeilingBounce, body.Arc.Gravity, body.Arc.Friction);
    }

    private static double CeilingOf(atom_movable am) => ismob(am) ? CeilingPixels - MobHeadroomPixels : CeilingPixels;

    // tg's finalize (waitfor = FALSE), with the seam holding the body: what it does to the thing decides where it rests
    private async DmTask Finalize(datum_thrownthing tt, float? hit = null, object target = null)
    {
        atom_movable am = tt.thrownthing;
        if (am == null)
            return;
        _bodies.TryGetValue(am.dm_id, out Body body);
        turf before = am.loc as turf;
        if (body != null)
            body.Bridging = true;
        try
        {
            tt.finalize(hit, target);
        }
        finally
        {
            if (body != null)
                body.Bridging = false;
        }
        if (body == null || !_bodies.ContainsKey(am.dm_id))
            return;
        body.Throw = null;
        if (QDELETED(am) || am.loc is not turf now)
        {
            Drop(body);
            return;
        }
        if (body.ImpactLevels > 0)
        {
            body.Kind = Kind.Fall; // DV435: finalize's zFall took it down: FallTo set its fall from where it was
            return;
        }
        (double x, double y, double lift) = body.Arc.At(body.Clock);
        double vl = Math.Min(0, body.Arc.VelocityAt(body.Clock).VLift);
        if (!ReferenceEquals(now, before))
        {
            // zFall took it down (an end over open space): it falls the rest of the way from where it was
            if (before != null && Dm.num(now.x) == Dm.num(before.x) && Dm.num(now.y) == Dm.num(before.y) && Dm.num(now.z) < Dm.num(before.z))
                lift += (Dm.num(before.z) - Dm.num(now.z)) * FlightArc.PixelsPerTile;
            else
            {
                Drop(body);
                return;
            }
        }
        bool gravity = Dm.truthy(am.has_gravity(now));
        body.Kind = Kind.Fall;
        Restart(body, x, y, (int)Dm.num(now.z), lift, 0, 0, gravity ? vl : 0, gravity ? Gravity(World.Current) : 0);
        if (!gravity || lift <= FloorLift(now) + Epsilon)
            await Rest(body);
    }

    // It stopped: an item keeps its point; a big thing settles onto its tile's middle
    private async DmTask Rest(Body body)
    {
        atom_movable am = body.Thing;
        if (QDELETED(am) || am.loc is not turf here)
        {
            Drop(body);
            return;
        }
        (double x, double y, double lift) = body.Arc.At(body.Clock);
        _bodies.Remove(am.dm_id);
        int z = (int)Dm.num(here.z);
        bool weightless = !Dm.truthy(am.has_gravity(here));
        if (!weightless)
            lift = Math.Max(lift, FloorLift(here));
        if (am is mob_living landed && LocomotionSeam.HasCapsule(World.Current, landed))
        {
            LocomotionSeam.PlaceInFlight(World.Current, landed, x, y);
            SetArc(am, FlightArc.None); // the capsule's own position from here on
            return;
        }
        if (am is obj_item || body.Centring)
        {
            SetArc(am, body.Centring ? FlightArc.None : FlightArc.Rest(body.Clock, (float)x, (float)y, z, (float)lift));
            return;
        }
        double cx = Dm.num(here.x), cy = Dm.num(here.y);
        double dx = cx - x, dy = cy - y, d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 0.01 && lift < 0.5)
        {
            SetArc(am, FlightArc.None);
            return;
        }
        // a slide that stops on the middle: v0 = 2d / T, friction v0 / T
        double v0 = 2 * d / CentreTicks;
        var centring = new Body { Thing = am, Kind = Kind.Slide, Clock = body.Clock, Centring = true };
        centring.Arc = new FlightArc(body.Clock, (float)x, (float)y, z, 0f, (float)(d < 1e-9 ? 0 : dx / d * v0), (float)(d < 1e-9 ? 0 : dy / d * v0), 0f, 0f, (float)(v0 / CentreTicks));
        _bodies[am.dm_id] = centring;
        SetArc(am, centring.Arc);
        await DmTask.CompletedTask;
    }

    // A capsule mob's capsule stands where its arc is now (LocomotionSeam.PlaceInFlight)
    private static void PlaceCapsule(Body body)
    {
        if (body.Thing is not mob_living mob || QDELETED(mob))
            return;
        (double x, double y, _) = body.Arc.At(body.Clock);
        LocomotionSeam.PlaceInFlight(World.Current, mob, x, y);
    }

    private void Drop(Body body)
    {
        _bodies.Remove(body.Thing.dm_id);
        if (!QDELETED(body.Thing))
            SetArc(body.Thing, FlightArc.None);
    }

    private void SetArc(atom_movable am, FlightArc arc)
    {
        if (am.dm_flight == arc)
            return;
        am.dm_flight = arc;
        _announced[am.dm_id] = am;
    }

    // The point of a thing now: its body's, its rest point, else its tile's middle on the floor
    private (double X, double Y, double Lift) PointNow(atom_movable am, double tick)
    {
        if (_bodies.TryGetValue(am.dm_id, out Body b))
            return b.Arc.At(tick);
        FlightArc rest = am.dm_flight;
        if (!rest.IsNone && !rest.IsMoving && rest.Z == (int)Dm.num(am.z))
            return (rest.X0, rest.Y0, rest.Lift0);
        if (am is mob_living mob && am.loc is turf && LocomotionSeam.TryGetState(World.Current, mob.dm_id, out MotionState capsule, out _))
            return (capsule.X / (double)MotionMath.SubunitsPerTile, capsule.Y / (double)MotionMath.SubunitsPerTile, 0);
        return (Dm.num(am.x), Dm.num(am.y), am.loc is turf t && Dm.truthy(am.has_gravity(t)) ? 0 : HandPixels);
    }

    // ---- tg's fall ----

    /// <summary>DV435: whether <paramref name="thing"/> is falling to a tg impact the seam makes when it reaches a floor.</summary>
    public static bool FallsToImpact(atom_movable thing)
        => thing != null && World.CurrentOrNull is World world && Of(world)._bodies.TryGetValue(thing.dm_id, out Body b) && b.ImpactLevels > 0;

    /// <summary>
    /// DV435: the first of the next ticks (1 the next one) on which <paramref name="thing"/>'s tg fall has landed on the lowest floor its open column
    /// lets it reach (protocol 50's UnblockTicks, so its client's prediction walks on with the server); 0 when it is in no such fall.
    /// </summary>
    public short FallLandsIn(atom_movable thing)
    {
        if (!_bodies.TryGetValue(thing.dm_id, out Body body) || body.ImpactLevels <= 0 || body.Arc.Gravity <= 0 || thing.loc is not turf here)
            return 0;
        int levels = 0;
        for (turf t = here; levels < 64 && OpenBelow(thing, t) && GET_TURF_BELOW(t) is turf below; t = below)
            levels++;
        (_, _, double lift) = body.Arc.At(body.Clock);
        double at = LiftTime(body.Arc, body.Clock, lift, -levels * FlightArc.PixelsPerTile, down: true);
        if (double.IsInfinity(at))
            return 0;
        return (short)Math.Clamp(Math.Ceiling(at - World.Current.Tick) + 1, 1, short.MaxValue);
    }

    /// <summary>
    /// DEVIATION(DV435): tg's fall of <paramref name="falling"/> from <paramref name="from"/> to <paramref name="target"/> (turf.dm:372-373): tg's zMove
    /// now, and tg's zImpact (its damage, its spin, a fall on to the next level) when the thing, falling under gravity from where it was, reaches the
    /// floor it fell to (Floor), not at once; at once where it does not fall down under gravity.
    /// </summary>
    public static async DmTask FallTo(atom_movable falling, turf from, turf target, float levels)
    {
        World world = World.Current;
        FlightSeam seam = Of(world);
        // where it is before tg's move, which ends a rest point
        seam._bodies.TryGetValue(falling.dm_id, out Body body);
        double x, y, lift, vx = 0, vy = 0, vl = 0;
        int z;
        if (body != null)
        {
            (x, y, lift) = body.Arc.At(body.Clock);
            (vx, vy, vl) = body.Arc.VelocityAt(body.Clock);
            z = body.Arc.Z;
        }
        else
        {
            (x, y, lift) = seam.PointNow(falling, world.Tick);
            z = (int)Dm.num(from.z);
        }
        if (body != null)
            body.Bridging = true;
        try
        {
            await falling.zMove(null, target, ZMOVE_CHECK_PULLEDBY);
        }
        finally
        {
            if (body != null)
                body.Bridging = false;
        }
        int below = (int)Dm.num(target.z);
        // the top of the stairs takes a one-level fall at once (stairs.dm intercept_zImpact): a walker steps down onto the flight, it does not drop to the floor under it
        if (QDELETED(falling) || !ReferenceEquals(falling.loc, target) || below >= (int)Dm.num(from.z) || !Dm.truthy(falling.has_gravity(target))
            || levels == 1 && Dm.each<obj_structure_stairs>(target.contents).Any(s => Dm.truthy(s.isTerminator())))
        {
            await target.zImpact(falling, levels, from);
            return;
        }
        if (body == null || !seam._bodies.ContainsKey(falling.dm_id))
        {
            body = new Body { Thing = falling, Clock = world.Tick };
            seam._bodies[falling.dm_id] = body;
        }
        body.Kind = body.Throw != null ? Kind.Throw : Kind.Fall;
        body.Centring = false;
        seam.Restart(body, x, y, below, lift + (z - below) * FlightArc.PixelsPerTile, vx, vy, vl, Gravity(world));
        (body.ImpactLevels, body.ImpactFrom) = (levels, from);
    }

    // ---- the air's push ----

    /// <summary>
    /// DV416: the air pushes <paramref name="thing"/> (no mob) along <paramref name="direction"/> at <paramref name="moveProb"/> (tg's move
    /// chance): instead of tg's chance of a tile step once an air pass, it slides at the step's mean pace (<paramref name="moveProb"/> percent of
    /// a tile an air pass, as DV357 carries a walker) and friction stops it once the wind does. False where it does not slide (no gravity: tg's
    /// step and drift stand).
    /// </summary>
    public static bool Push(atom_movable thing, float direction, float moveProb)
    {
        if (!Flies(thing) || thing.loc is not turf here || Dm.truthy(thing.throwing) || !Dm.truthy(thing.has_gravity(here)))
            return false;
        // its slide already met what stands that way: tg's step bumps it again and it stays put (a new slide would be drawn into it and back)
        // shortcut: an obstacle taken away meanwhile lets tg's step move it a whole tile once, until a play test shows that seen
        if (thing.dm_slide_stop == StopKey(here, direction))
            return false;
        World world = World.Current;
        FlightSeam seam = Of(world);
        double passTicks = Math.Max(1, DS2TICKS(Dm.num(SSair.wait)));
        double friction = Friction(world);
        double target = Math.Min(100, moveProb) / 100.0 / passTicks + friction * passTicks / 2; // the mean pace over the pass, friction made up
        double ux = (Dm.truthy(Dm.bitand(direction, EAST)) ? 1 : 0) - (Dm.truthy(Dm.bitand(direction, WEST)) ? 1 : 0);
        double uy = (Dm.truthy(Dm.bitand(direction, NORTH)) ? 1 : 0) - (Dm.truthy(Dm.bitand(direction, SOUTH)) ? 1 : 0);
        double n = Math.Sqrt(ux * ux + uy * uy);
        if (n == 0)
            return false;
        (ux, uy) = (ux / n, uy / n);
        double now = world.Tick;
        (double x, double y, double lift) = seam.PointNow(thing, now);
        double vx = 0, vy = 0, vl = 0, g = 0;
        if (seam._bodies.TryGetValue(thing.dm_id, out Body body))
        {
            if (body.Kind == Kind.Throw)
                return false;
            (vx, vy, vl) = body.Arc.VelocityAt(now);
            g = body.Arc.Gravity;
        }
        else
            body = new Body { Thing = thing, Clock = now };
        double along = vx * ux + vy * uy;
        if (along < target)
        {
            vx += (target - along) * ux;
            vy += (target - along) * uy;
        }
        body.Centring = false;
        body.Kind = g != 0 || vl != 0 ? Kind.Fall : Kind.Slide;
        body.Clock = now;
        seam._bodies[thing.dm_id] = body;
        seam.Restart(body, x, y, (int)Dm.num(thing.z), lift, vx, vy, vl, g, body.Kind == Kind.Slide ? friction : 0);
        return true;
    }

    // ---- the phase ----

    private static void RunPhase(World world)
    {
        FlightSeam seam = Of(world);
        if (seam._announced.Count > 0)
        {
            // a thing deleted before the replicator read it is not polled for ever
            List<long> gone = null;
            foreach ((long id, atom_movable thing) in seam._announced)
                if (QDELETED(thing)) (gone ??= new List<long>()).Add(id);
            if (gone != null)
                foreach (long id in gone) { seam._announced.Remove(id); seam._bodies.Remove(id); }
        }
        if (seam._bodies.Count == 0)
        {
            seam._aims.Clear();
            return;
        }
        foreach (long id in new List<long>(seam._bodies.Keys))
        {
            if (!seam._bodies.TryGetValue(id, out Body body))
                continue;
            if (body.Throw != null)
            {
                if (body.Throw.thrownthing == body.Thing && ReferenceEquals(body.Thing.throwing, body.Throw) && !QDELETED(body.Throw))
                    continue; // SSthrowing drives it
                // the throw ended without the seam (deleted, ended by another hand): it falls from where it is
                body.Throw = null;
                body.Kind = Kind.Fall;
                if (QDELETED(body.Thing) || body.Thing.loc is not turf t)
                {
                    seam.Drop(body);
                    continue;
                }
                (double x, double y, double lift) = body.Arc.At(body.Clock);
                seam.Restart(body, x, y, (int)Dm.num(t.z), lift, 0, 0, 0, Dm.truthy(body.Thing.has_gravity(t)) ? Gravity(world) : 0);
            }
            Waiting.Detach(seam.Advance(body, world.Tick));
        }
        // a click's aim lives for its tick and the next
        if (seam._aims.Count > 0)
        {
            var stale = new List<long>();
            foreach ((long mobId, var aim) in seam._aims)
                if (world.Tick - aim.Tick > 1) stale.Add(mobId);
            foreach (long mobId in stale) seam._aims.Remove(mobId);
        }
    }

    private void Hash(ref StateHasher h)
    {
        if (_bodies.Count == 0)
            return;
        foreach ((long id, Body b) in _bodies)
        {
            h.Add(id);
            h.Add((int)b.Kind);
            h.Add(b.Clock);
            FlightArc a = b.Arc;
            h.Add(a.StartTick);
            h.Add(a.X0);
            h.Add(a.Y0);
            h.Add(a.Z);
            h.Add(a.Lift0);
            h.Add(a.VX);
            h.Add(a.VY);
            h.Add(a.VLift);
            h.Add(a.Gravity);
            h.Add(a.Friction);
            h.Add(b.Centring);
            h.Add(b.ImpactLevels);
        }
    }
}

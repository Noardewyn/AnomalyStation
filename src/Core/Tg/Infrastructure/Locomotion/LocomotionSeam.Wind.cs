using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV357): the space wind and gravity pulls carry a walking capsule continuously. tg steps a mob a tile now and then
// (experience_pressure_difference's step once an air pass, LINDA_turf_tile.dm:410; the singularity's, the gravitational anomaly's, the
// vortex's, the supermatter's and the HFR's step_towards), which the client could not foresee: every push was a correction, and the body
// jumped a tile and stood still. The bridge gives the push to the capsule as a speed instead (MotionParams.Wind and Pull), added to the
// walker's own wish on the server and in its client's prediction alike, so it glides, is predicted and can be leaned against.

public static partial class LocomotionSeam
{
    /// <summary>
    /// The largest share of tg's chance to be pushed (its <c>move_prob</c>, percent) that counts toward the wind's speed: tg's pass pushes
    /// at most a tile, at 100; four tiles a pass (8 tiles a second) let a strong wind outrun a running mob, a weaker one be walked against.
    /// </summary>
    public const float MaxWindProb = 400f;

    /// <summary>A gravity pull's pace before its own is measured (ticks a tile): the singularity's step a second.</summary>
    public const int DefaultPullTicks = 20;

    /// <summary>The longest pace a gravity pull is measured at (ticks a tile); its steps further apart than that are separate pulls.</summary>
    public const int MaxPullTicks = 40;

    private static long WindKey(int x, int y, int z) => ((long)z << 40) | ((long)y << 20) | (uint)x;

    private static bool Cardinal(byte dir) => dir == MotionDir.North || dir == MotionDir.South || dir == MotionDir.East || dir == MotionDir.West;

    /// <summary>SSair's pressure pass is about to push the movables on <paramref name="turf"/>: its wind joins the pass's (a cardinal one only, DV179).</summary>
    public static void NoteWind(turf_open turf)
    {
        World world = World.CurrentOrNull;
        if (world == null || turf == null || !(turf.pressure_difference > 0))
            return;
        byte dir = (byte)Dm.num(turf.pressure_direction);
        if (Cardinal(dir))
            Seam.Of(world).NextWinds[WindKey((int)turf.x, (int)turf.y, (int)turf.z)] = (dir, turf.pressure_difference);
    }

    /// <summary>SSair finished a pass over <c>high_pressure_delta</c>: its wind is the wind until the next pass ends.</summary>
    public static void WindCycleEnd()
    {
        World world = World.CurrentOrNull;
        if (world == null)
            return;
        Seam seam = Seam.Of(world);
        if (seam.Winds.Count == 0 && seam.NextWinds.Count == 0)
            return; // calm: nothing to expire (and the hash of a world without wind stays as it was)
        seam.Winds = seam.NextWinds;
        seam.NextWinds = new();
        seam.WindCycles++;
    }

    /// <summary>
    /// tg's wind pushes <paramref name="moving"/> toward <paramref name="direction"/> with <paramref name="moveProb"/> percent (its
    /// <c>move_prob</c>) on a tile of <paramref name="difference"/> kPa: true when the bridge carries it (a walking capsule mob), which then
    /// takes no tg step; the push holds through the next air pass.
    /// </summary>
    public static bool Blow(atom_movable moving, float direction, float difference, float moveProb)
    {
        byte dir = (byte)direction;
        if (!Cardinal(dir) || !TryWalkingCapsule(moving, out Seam seam, out Capsule c))
            return false;
        long key = WindKey(c.State.TileX, c.State.TileY, c.State.Z);
        seam.Winds[key] = seam.NextWinds[key] = (dir, difference); // a push outside a pass (a test, an admin) blows its tile at once
        float waitTicks = Math.Max(1f, Dm.num(SSair?.wait) / MotionMath.TickDeciseconds); // a tile a pass at 100
        float tiles = Math.Clamp(moveProb, 0f, MaxWindProb) / 100f / waitTicks * c.Scaled(seam.SpeedScale);
        c.WindSpeed = (short)Math.Clamp(MotionMath.Quantize(tiles), 1, short.MaxValue);
        c.WindCycle = seam.WindCycles;
        return true;
    }

    /// <summary>
    /// A gravity pull (tg's <c>step_towards(<paramref name="moving"/>, <paramref name="center"/>)</c>) of a walking capsule mob: true when
    /// the bridge carries it toward the centre's tile at the pace the pull's steps come, and tg's step is not taken.
    /// </summary>
    public static bool Pull(atom_movable moving, atom center)
    {
        if (!TryWalkingCapsule(moving, out Seam seam, out Capsule c) || get_turf(center) is not turf t || (int)t.z != c.State.Z)
            return false;
        long now = World.Current.Tick, since = now - c.PullTick;
        if (c.PullSpeed == 0 || since != 0) // two pulls in one tick keep the pace
        {
            long pace = c.PullTick != 0 && since > 0 && since <= MaxPullTicks ? since : DefaultPullTicks;
            c.PullSpeed = (short)Math.Clamp(MotionMath.Quantize(1f / pace * c.Scaled(seam.SpeedScale)), 1, short.MaxValue);
            c.PullUntil = now + pace + (pace + 1) / 2;
            c.PullTick = now;
        }
        c.PullX = (int)t.x;
        c.PullY = (int)t.y;
        c.PullZ = (int)t.z;
        return true;
    }

    /// <summary><see cref="Pull"/>, else tg's own <c>step_towards</c>.</summary>
    public static async DmTask<object> PullOrStep(atom_movable moving, atom center)
        => Pull(moving, center) ? Dm.TRUE : await Builtins.step_towards(moving, center);

    // The wind and the pull that carry this capsule now (DV357), added to its parameters.
    internal static MotionParams Carried(MotionParams parameters, Capsule c)
    {
        if (c.WindSpeed == 0 && c.PullSpeed == 0)
            return parameters;
        bool walks = WalksOnGrid(c);
        Seam seam = Seam.Of(World.Current);
        BeltField wind = default;
        if (walks && c.WindSpeed != 0 && c.WindCycle >= seam.WindCycles - 1 && seam.Winds.Count > 0)
        {
            int x0 = c.State.TileX, y0 = c.State.TileY, z = c.State.Z;
            float resistance = c.Mob.pressure_resistance;
            var field = new BeltField(0, 0, 0, 0, x0, y0, 0);
            bool any = false;
            for (int dy = -BeltField.Reach; dy <= BeltField.Reach; dy++)
            {
                for (int dx = -BeltField.Reach; dx <= BeltField.Reach; dx++)
                {
                    // tg's chance there without the mob's own changes (its moving, a wall beside it) above PROBABILITY_OFFSET: it may push
                    if (!seam.Winds.TryGetValue(WindKey(x0 + dx, y0 + dy, z), out (byte Dir, float Difference) w)
                        || (resistance > 0 && w.Difference / resistance * 75 - 25 <= 25 && (dx != 0 || dy != 0)))
                        continue;
                    field = field.With(dx, dy, w.Dir);
                    any = true;
                }
            }
            if (any)
                wind = field with { Speed = c.WindSpeed };
        }
        bool pulled = walks && c.PullSpeed != 0 && World.Current.Tick <= c.PullUntil && c.PullZ == c.State.Z;
        return parameters with { Wind = wind, PullX = pulled ? c.PullX : 0, PullY = pulled ? c.PullY : 0, PullSpeed = pulled ? c.PullSpeed : (short)0 };
    }

    // A registered capsule mob that walks on a grid, which the wind and pulls carry (DV357): not weightless (DV69: tg's steps and drift
    // move it there), buckled (what it is buckled to is moved) or through the dev noclip.
    private static bool WalksOnGrid(Capsule c)
        => c.OnGrid && !c.State.Slide.Active && !IsNoClip(c.Mob) && !Dm.truthy(c.Mob.buckled) && !UsesZeroG(c.Mob)
           && (c.State.Driver == MotionDriver.Grounded || c.State.Driver == MotionDriver.Prone);

    private static bool TryWalkingCapsule(atom_movable moving, out Seam seam, out Capsule c)
    {
        seam = null;
        c = null;
        World world = World.CurrentOrNull;
        if (moving is not mob_living mob || world == null)
            return false;
        seam = Seam.Of(world);
        return seam.Capsules.TryGetValue(mob.dm_id, out c) && ReferenceEquals(c.Mob, mob) && WalksOnGrid(c);
    }
}

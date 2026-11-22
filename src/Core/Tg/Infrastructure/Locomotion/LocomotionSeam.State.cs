using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Owner: M2 P4 (docs/ai/specs/locomotion.md section 4). The seam's per-world state: the registered capsules in dm_id order,
// the speed scale, the server's collision view, and the state hash contributor.

public static partial class LocomotionSeam
{
    // The seam of one world (World.GetExtension: no static state). World state: changed only inside World.Step (and by
    // the host's boot-time SetSpeedScale), hashed by the contributor Install registers.
    internal sealed class Seam
    {
        internal bool Installed;
        internal float SpeedScale = 1f;
        // DV263: the walking capsules' speed ramp and slower directions (the host at boot, the dev command movefeel); none in a Core world
        internal MoveFeel Feel;
        // DV233: the subunits walked per footstep stride, running and walking (the dev command footstride)
        internal int RunStride = DefaultStride, WalkStride = DefaultStride;
        internal readonly SortedDictionary<long, Capsule> Capsules = new();
        internal readonly SortedDictionary<long, GhostCapsule> Ghosts = new(); // DV68: the ghosts' free flight
        internal readonly SortedDictionary<long, MechCapsule> Mechs = new(); // DV358: the exosuits' walk
        internal readonly SortedDictionary<long, EyeCapsule> Eyes = new(); // DV423: the AI eyes' free flight
        // DV357: the wind of SSair's last finished pass over high_pressure_delta (WindKey: its direction and pressure difference),
        // the pass in progress, and the passes finished
        internal SortedDictionary<long, (byte Dir, float Difference)> Winds = new(), NextWinds = new();
        internal long WindCycles;
        internal readonly ServerView View;

        private Seam(World world)
        {
            View = new ServerView(world, this);
        }

        internal static Seam Of(World world) => world.GetExtension(static w => new Seam(w));

        // Adds nothing while no capsule is registered and the scale is 1 (World.AddStateHasher).
        internal void Hash(ref StateHasher h)
        {
            if (SpeedScale != 1f)
                h.Add(SpeedScale);
            if (Feel != default)
            {
                h.Add((int)Feel.AccelTicks); // hashed only when set, as the scale
                h.Add((int)Feel.DecelTicks);
                h.Add(Feel.BackSlow);
                h.Add(Feel.StrafeSlow);
            }
            if (RunStride != DefaultStride || WalkStride != DefaultStride)
            {
                h.Add(RunStride); // hashed only when set, as the scale
                h.Add(WalkStride);
            }
            if (WindCycles != 0)
            {
                h.Add(WindCycles); // hashed only once SSair blew, as the scale
                foreach (SortedDictionary<long, (byte Dir, float Difference)> winds in new[] { Winds, NextWinds })
                {
                    h.Add(winds.Count);
                    foreach (KeyValuePair<long, (byte Dir, float Difference)> wind in winds)
                    {
                        h.Add(wind.Key);
                        h.Add((int)wind.Value.Dir);
                        h.Add(wind.Value.Difference);
                    }
                }
            }
            foreach (Capsule c in Capsules.Values)
                c.Hash(ref h);
            foreach (GhostCapsule g in Ghosts.Values)
                g.Hash(ref h);
            foreach (MechCapsule m in Mechs.Values)
                m.Hash(ref h);
            foreach (EyeCapsule e in Eyes.Values)
                e.Hash(ref h);
        }
    }

    // One capsule mob.
    internal sealed class Capsule
    {
        internal mob_living Mob;
        internal long Id;
        internal MotionState State;

        // The frame of this tick, or the last one (repeated when none came); Framed: a frame was ever set.
        internal MotionInput Frame;
        internal bool Framed;

        // tg's slowdown (deciseconds per tile) and the parameters for the next tick (the private block, TryGetState).
        internal float Slowdown;
        internal MotionParams NextParams;

        // The dev command speed: this capsule's own multiplier over the world's scale; the sum stays within SetSpeedScale's cap.
        // shortcut: held by the capsule, so a mob that unregisters (leaves, ghosts) comes back at x1; keep it elsewhere if that gets in the way.
        internal float OwnSpeed = 1f;

        internal float Scaled(float worldScale) => Math.Min(worldScale * OwnSpeed, MaxSpeedScale);

        // M10 slice J: GravityAround's mask, the tile and the tick it was taken on, and whether that tile was weightless.
        internal ulong GravityMask;
        internal turf GravityTile;
        internal bool GravityWeightless;
        internal long GravityTick;

        // Per other capsule: the tick from which a contact runs MobBump's signals again (once per move delay per pair).
        internal readonly SortedDictionary<long, long> ContactReady = new();

        // A step that slept is still running: the next phase skips this capsule until it returns.
        internal bool Busy;

        // DV69: scoped to the bridge's Move call, not retained as momentum after that call.
        internal float? CrossingInertiaAngle;

        // DV233: the walked distance (subunits) since the last stride, and whether a bridge Move is running (its Moved plays no step).
        internal int StrideTravel;
        internal bool Bridging;
        internal bool Striding; // DV233: it walked last tick (its stop sends the halt)

        // DV535: a press of this step pushed what it pressed (PushAM): the step is taken again through the freed tile
        internal bool Pushed;

        // DV69: weightless, the gate's answer holds until this tick (tg's client/Move cadence: its push-off pushes once a step).
        internal long GateAgainTick;
        internal bool GateAnswer;

        // DV15: the BYOND direction the gate's COMSIG_MOB_CLIENT_PRE_MOVE handlers left in client/Move's direct (a confusion's
        // scramble), held with the weightless answer; 0 when no handler turned it.
        internal byte GateDir;

        // DV15: on a floor, a mob with a PRE_MOVE handler moves by tg steps: the direction the gate's handlers left at a step's start
        // (TurnDir, 0 for the wish's own) holds until TurnAgainTick, a move delay later, or until the wish (TurnWish) changes.
        internal long TurnAgainTick;
        internal byte TurnWish;
        internal byte TurnDir;

        // DV357: the wind's push (Blow): its speed (subunits a tick) and the count of SSair's finished passes when it blew.
        internal short WindSpeed;
        internal long WindCycle;

        // DV357: a gravity pull (Pull): its tile, its speed (subunits a tick), the tick of its last step and the tick it ends.
        internal int PullX, PullY, PullZ;
        internal short PullSpeed;
        internal long PullTick, PullUntil;

        internal static Capsule Create(mob_living mob, float speedScale)
        {
            var c = new Capsule { Mob = mob, Id = mob.dm_id };
            byte dir = (byte)Dm.num(mob.dir);
            ushort yaw = MotionFacing.YawOf(dir);
            turf where = mob.loc as turf ?? get_turf(mob) as turf;
            c.State = where != null
                ? MotionState.AtTile((int)where.x, (int)where.y, (int)where.z, yaw)
                : new MotionState { Yaw = yaw };
            SyncPosture(c);
            c.Frame = MotionInput.Idle(0, yaw);
            c.Slowdown = Dm.num(mob.cached_multiplicative_slowdown);
            c.NextParams = NextParamsOf(c, speedScale);
            return c;
        }

        internal void Hash(ref StateHasher h)
        {
            h.Add(Id);
            h.Add(State.X);
            h.Add(State.Y);
            h.Add(State.Z);
            h.Add((int)State.Yaw);
            h.Add((int)State.Posture);
            h.Add((int)State.Driver);
            h.Add(State.Slide.TargetX);
            h.Add(State.Slide.TargetY);
            h.Add((int)State.Slide.TicksLeft);
            h.Add((int)State.PressTimer);
            if (State.VelX != 0 || State.VelY != 0)
            {
                h.Add(State.VelX); // DV263: hashed only when moving, so a world without a ramp hashes as before
                h.Add(State.VelY);
            }
            h.Add((long)Frame.Seq);
            h.Add((int)Frame.MoveX);
            h.Add((int)Frame.MoveY);
            h.Add((int)Frame.Yaw);
            h.Add((int)Frame.Flags);
            h.Add(Framed);
            h.Add(Slowdown);
            h.Add(NextParams.TilesPerTick);
            h.Add(NextParams.CanMove);
            if (OwnSpeed != 1f)
                h.Add(OwnSpeed); // hashed only when set, as NoClip
            if (NextParams.NoClip)
                h.Add(0x4E43); // hashed only when set, so the hashes of the worlds without a noclip capsule stay as they were
            if (NextParams.OnClimbable)
                h.Add(0x434C); // D239: hashed only when set, as NoClip
            if (NextParams.DriftX != 0 || NextParams.DriftY != 0)
            {
                h.Add((int)NextParams.DriftX);
                h.Add((int)NextParams.DriftY);
            }
            h.Add((int)NextParams.MoveDelayTicks);
            if (NextParams.Belt.Active)
            {
                h.Add(0x424C); // DV347: hashed only when set, as NoClip
                h.Add(NextParams.Belt.W0);
                h.Add(NextParams.Belt.W1);
                h.Add(NextParams.Belt.W2);
                h.Add(NextParams.Belt.W3);
                h.Add(NextParams.Belt.X);
                h.Add(NextParams.Belt.Y);
                h.Add((int)NextParams.Belt.Speed);
            }
            if (NextParams.Wind.Active || NextParams.PullSpeed != 0)
            {
                h.Add(0x574E); // DV357: hashed only when set, as NoClip
                h.Add(NextParams.Wind.W0);
                h.Add(NextParams.Wind.W1);
                h.Add(NextParams.Wind.W2);
                h.Add(NextParams.Wind.W3);
                h.Add(NextParams.Wind.X);
                h.Add(NextParams.Wind.Y);
                h.Add((int)NextParams.Wind.Speed);
                h.Add(NextParams.PullX);
                h.Add(NextParams.PullY);
                h.Add((int)NextParams.PullSpeed);
            }
            h.Add(Busy);
            if (CrossingInertiaAngle is float crossingAngle)
            {
                h.Add(0x4941); // a Move that suspends retains its scoped crossing angle
                h.Add(crossingAngle);
            }
            if (StrideTravel != 0 || Bridging || Striding)
            {
                h.Add(0x5354); // hashed only when set, as NoClip
                h.Add(StrideTravel);
                h.Add(Bridging);
                h.Add(Striding);
            }
            if (GateAgainTick != 0)
            {
                h.Add(GateAgainTick); // hashed only once set, so the hashes of the worlds without a weightless capsule stay as they were
                h.Add(GateAnswer);
                h.Add((int)GateDir);
            }
            if (TurnAgainTick != 0)
            {
                h.Add(TurnAgainTick); // hashed only once set, so the hashes of the worlds without a PRE_MOVE handler stay as they were
                h.Add((int)TurnWish);
                h.Add((int)TurnDir);
            }
            if (WindSpeed != 0 || PullSpeed != 0)
            {
                h.Add(0x5750); // DV357: hashed only when set, as NoClip
                h.Add((int)WindSpeed);
                h.Add(WindCycle);
                h.Add(PullX);
                h.Add(PullY);
                h.Add(PullZ);
                h.Add((int)PullSpeed);
                h.Add(PullTick);
                h.Add(PullUntil);
            }
            h.Add(ContactReady.Count);
            foreach (KeyValuePair<long, long> pair in ContactReady)
            {
                h.Add(pair.Key);
                h.Add(pair.Value);
            }
        }

        // The tile the capsule stands for: a slide's target while it slides (tg already moved the mob there).
        internal (int X, int Y, int Z) Tile => State.Slide.Active
            ? (MotionMath.TileOf(State.Slide.TargetX), MotionMath.TileOf(State.Slide.TargetY), State.Z)
            : (State.TileX, State.TileY, State.Z);

        // On a turf (in a locker or in nullspace the capsule takes no part in collisions).
        internal bool OnGrid => Mob.loc is turf;
    }

    /// <summary>
    /// The server's collision view (<see cref="CollisionView"/>): <see cref="IsSolid"/> over the world's map, no blocked
    /// faces in M2, and the registered standing capsules on a turf (lying ones, D51, and those of deleted mobs are left
    /// out), at their positions in the phase (the capsules stepped earlier in the tick are where their step left them).
    /// </summary>
    internal sealed class ServerView : ICollisionView
    {
        private readonly World _world;
        private readonly Seam _seam;

        internal ServerView(World world, Seam seam)
        {
            _world = world;
            _seam = seam;
        }

        public bool IsSolid(int x, int y, int z) => LocomotionSeam.IsSolid(_world, x, y, z);

        public bool IsOutsideMap(int x, int y, int z) => MapView.TurfAt(_world, x, y, z) == null;

        public bool IsClimbWalkable(int x, int y, int z) => LocomotionSeam.IsClimbWalkable(_world, x, y, z);

        public bool IsFlaps(int x, int y, int z) => LocomotionSeam.IsFlaps(_world, x, y, z);

        public bool LadderAt(int x, int y, int z) => LocomotionSeam.LadderAt(_world, x, y, z);

        public TileEdges EdgesOf(int x, int y, int z) => LocomotionSeam.FacesOf(_world, x, y, z);

        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
        {
            ArgumentNullException.ThrowIfNull(into);
            foreach (Capsule c in _seam.Capsules.Values) // dm_id order
            {
                if (c.Id == exclude || c.State.Z != z || c.State.Posture != Posture.Standing)
                    continue;
                if (c.Mob.gc_destroyed != null || c.Mob.dm_loc_raw is not turf)
                    continue;
                if (Math.Abs(c.State.X - x) > range || Math.Abs(c.State.Y - y) > range)
                    continue;
                into.Add(new CapsuleObstacle(c.Id, c.State.X, c.State.Y, MotionMath.DefaultRadius));
            }
            foreach (MechCapsule m in _seam.Mechs.Values) // DV358: the exosuits, in dm_id order after the mobs
            {
                if (m.Id == exclude || m.State.Z != z || m.Mech.gc_destroyed != null || m.Mech.dm_loc_raw is not turf)
                    continue;
                if (Math.Abs(m.State.X - x) > range || Math.Abs(m.State.Y - y) > range)
                    continue;
                into.Add(new CapsuleObstacle(m.Id, m.State.X, m.State.Y, MotionMath.MechRadius));
            }
        }
    }
}

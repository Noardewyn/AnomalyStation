using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Owner: the exosuits (the owner, 2026-11-07: a mech walks as smoothly and in every direction as a human). DEVIATION(DV358): a mech is a
// capsule of its own: it walks continuously at tg's movedelay, its driver's frames steering it. tg's vehicle_move survives as the gate at
// its cadence (the energy, the parts, the push-off), every tile crossing is tg's Move and every press tg's Bump, so doors open and walls are
// smashed as in tg. Its dir (the armour's facing) follows the driver's view unless the strafe lock holds it. The client predicts this capsule
// as its own: the mech is sent as the own entity of its occupants (OwnBody).

public static partial class LocomotionSeam
{
    // The walked distance per step sound: tg's step is a tile.
    private const int MechStride = MotionMath.SubunitsPerTile;

    // One exosuit's capsule.
    internal sealed class MechCapsule
    {
        internal obj_vehicle_sealed_mecha Mech;
        internal long Id;
        internal MotionState State;
        internal MotionParams NextParams;
        internal mob_living Driver; // the occupant whose frames steer it this tick
        internal bool Busy;
        internal bool Bridging;
        internal int StrideTravel;

        // vehicle_move's cooldown_vehicle_move: the gate's answer holds until this tick, and its random direction (MECHA_INT_CONTROL_LOST).
        internal long GateAgainTick;
        internal bool GateAnswer = true;
        internal byte GateDir;

        // Per other capsule: the tick from which a contact bumps it again (once per move delay per pair).
        internal readonly SortedDictionary<long, long> ContactReady = new();

        internal static MechCapsule Create(obj_vehicle_sealed_mecha mech)
        {
            var m = new MechCapsule { Mech = mech, Id = mech.dm_id };
            ushort yaw = MotionFacing.YawOf((byte)Dm.num(mech.dir));
            turf where = get_turf(mech) as turf;
            m.State = where != null ? MotionState.AtTile((int)where.x, (int)where.y, (int)where.z, yaw) : new MotionState { Yaw = yaw };
            return m;
        }

        internal void Hash(ref StateHasher h)
        {
            h.Add(0x4D454348); // "MECH": an exosuit's capsule
            h.Add(Id);
            h.Add(State.X);
            h.Add(State.Y);
            h.Add(State.Z);
            h.Add((int)State.Yaw);
            h.Add(State.Slide.TargetX);
            h.Add(State.Slide.TargetY);
            h.Add((int)State.Slide.TicksLeft);
            h.Add((int)State.PressTimer);
            h.Add(State.VelX);
            h.Add(State.VelY);
            h.Add(NextParams.TilesPerTick);
            h.Add(NextParams.CanMove);
            h.Add(Busy);
            h.Add(StrideTravel);
            h.Add(GateAgainTick);
            h.Add(GateAnswer);
            h.Add((int)GateDir);
            h.Add(ContactReady.Count);
            foreach (KeyValuePair<long, long> pair in ContactReady)
            {
                h.Add(pair.Key);
                h.Add(pair.Value);
            }
        }

        // The tile the capsule stands for: a slide's target while it slides (tg already moved the mech there).
        internal (int X, int Y, int Z) Tile => State.Slide.Active
            ? (MotionMath.TileOf(State.Slide.TargetX), MotionMath.TileOf(State.Slide.TargetY), State.Z)
            : (State.TileX, State.TileY, State.Z);
    }

    /// <summary>From now on the bridge walks <paramref name="mech"/> (its Initialize); a world without the locomotion phase keeps tg's tile steps.</summary>
    public static void RegisterMech(obj_vehicle_sealed_mecha mech)
    {
        ArgumentNullException.ThrowIfNull(mech);
        World world = World.CurrentOrNull;
        if (world == null)
            return;
        Seam seam = Seam.Of(world);
        if (!seam.Installed || QDELETED(mech) || seam.Mechs.ContainsKey(mech.dm_id))
            return;
        MechCapsule m = MechCapsule.Create(mech);
        m.NextParams = MechParamsOf(m, seam, true);
        seam.Mechs.Add(mech.dm_id, m);
    }

    /// <summary>The bridge stops walking <paramref name="mech"/> (its Destroy).</summary>
    public static void UnregisterMech(obj_vehicle_sealed_mecha mech)
    {
        ArgumentNullException.ThrowIfNull(mech);
        World world = World.CurrentOrNull;
        if (world != null)
            Seam.Of(world).Mechs.Remove(mech.dm_id);
    }

    /// <summary>Whether the bridge walks <paramref name="mech"/>.</summary>
    public static bool IsMechWalked(obj_vehicle_sealed_mecha mech)
    {
        World world = World.CurrentOrNull;
        return mech != null && world != null && Seam.Of(world).Mechs.ContainsKey(mech.dm_id);
    }

    /// <summary>
    /// The entity a session's client treats as its body: the exosuit the mob <paramref name="mobId"/> sits in, else the mob itself. Read-only, outside the
    /// world after <c>World.Step</c>, like <see cref="TryGetState"/>: the capsule the client predicts is that entity's.
    /// </summary>
    public static long OwnBody(World world, long mobId)
    {
        ArgumentNullException.ThrowIfNull(world);
        Seam seam = Seam.Of(world);
        return seam.Capsules.TryGetValue(mobId, out Capsule c) && c.Mob.dm_loc_raw is obj_vehicle_sealed_mecha mech && mech.dm_loc_raw is turf && seam.Mechs.ContainsKey(mech.dm_id)
            ? mech.dm_id
            : mobId;
    }

    private static void RunMechs(World world, Seam seam)
    {
        if (seam.Mechs.Count == 0)
            return;
        var ids = new List<long>(seam.Mechs.Keys); // a step may destroy a mech
        foreach (long id in ids)
        {
            if (!seam.Mechs.TryGetValue(id, out MechCapsule m))
                continue;
            if (QDELETED(m.Mech))
            {
                seam.Mechs.Remove(id);
                continue;
            }
            if (m.Busy)
                continue;
            m.Driver = DriverOf(seam, m.Mech);
            object caller_usr = world.usr;
            try
            {
                world.usr = m.Driver;
                Waiting.Detach(StepMech(world, seam, m));
            }
            finally
            {
                world.usr = caller_usr;
            }
        }
    }

    private static void FollowMechs(Seam seam)
    {
        if (seam.Mechs.Count == 0)
            return;
        var ids = new List<long>(seam.Mechs.Keys);
        foreach (long id in ids)
        {
            MechCapsule m = seam.Mechs[id];
            if (QDELETED(m.Mech))
            {
                seam.Mechs.Remove(id);
                continue;
            }
            if (!m.Busy)
                FollowMech(m);
        }
    }

    // The first driver with a capsule (a session's mob): its frames steer the mech.
    private static mob_living DriverOf(Seam seam, obj_vehicle_sealed_mecha mech)
    {
        if (mech.occupants == null || mech.occupants.Count == 0)
            return null;
        foreach (mob driver in Dm.each<mob>(mech.return_drivers()))
        {
            if (driver is mob_living living && seam.Capsules.ContainsKey(living.dm_id))
                return living;
        }
        return null;
    }

    // The capsule follows a loc change tg made (a teleport, a drift, a push): tg's glide time for a neighbouring tile, else a snap.
    private static void FollowMech(MechCapsule m)
    {
        turf here = get_turf(m.Mech) as turf;
        if (here == null)
            return;
        (int tx, int ty, int tz) = m.Tile;
        if ((int)here.x == tx && (int)here.y == ty && (int)here.z == tz)
            return;
        MotionStep.Displace(ref m.State, (int)here.x, (int)here.y, (int)here.z, GlideTicks(m.Mech.glide_size));
    }

    private static MotionParams MechParamsOf(MechCapsule m, Seam seam, bool canMove)
    {
        float delay = Dm.num(m.Mech.movedelay);
        return new MotionParams(MotionMath.TilesPerTickFor(delay, seam.SpeedScale), MotionMath.MechRadius, canMove, MoveDelayTicks(delay / seam.SpeedScale), Feel: seam.Feel);
    }

    private static async DmTask StepMech(World world, Seam seam, MechCapsule m)
    {
        m.Busy = true;
        try
        {
            obj_vehicle_sealed_mecha mech = m.Mech;
            FollowMech(m);
            ForgetOldMechContacts(m, world.Tick);
            MotionInput frame = m.Driver != null && seam.Capsules.TryGetValue(m.Driver.dm_id, out Capsule pilot) ? pilot.Frame : MotionInput.Idle(0, m.State.Yaw);
            byte wishDir = WishDir(frame.MoveX, frame.MoveY);
            bool mayDrive = m.Driver != null && mech.capsule_may_drive();

            if (mech.loc is not turf)
            {
                // Inside another object (tg's relaymove from a movable loc): no capsule step, as a mob in a locker
                if (m.State.Slide.Active)
                    MotionStep.Step(ref m.State, in frame, MechParamsOf(m, seam, false), seam.View, null, m.Id);
                if (mayDrive && wishDir != MotionDir.None)
                    await mech.vehicle_move((float)wishDir);
                if (!QDELETED(mech))
                    m.NextParams = MechParamsOf(m, seam, false);
                return;
            }

            // tg's vehicle_move up to its step, at its cadence: the step's energy and the parts' messages come once a move delay
            if (mayDrive && wishDir != MotionDir.None && world.Tick >= m.GateAgainTick)
            {
                m.GateAgainTick = world.Tick + MoveDelayTicks(Dm.num(mech.movedelay) / seam.SpeedScale);
                float gate = await mech.capsule_drive_gate((float)wishDir);
                if (QDELETED(mech))
                    return;
                m.GateAnswer = gate != 0;
                m.GateDir = m.GateAnswer && (byte)gate != wishDir ? (byte)gate : MotionDir.None;
            }
            else if (wishDir == MotionDir.None)
            {
                m.GateAnswer = true;
                m.GateDir = MotionDir.None;
            }
            bool canMove = mayDrive && m.GateAnswer && mech.capsule_can_move();
            if (m.GateDir != MotionDir.None)
                frame = TurnWish(frame, wishDir, m.GateDir);

            // tg turns the mech toward the way it goes; here the armour faces the driver's view, as a mob's dir does, unless the strafe lock holds it
            bool turns = mayDrive && !mech.capsule_facing_locked();
            if (turns)
            {
                byte current = (byte)Dm.num(mech.dir);
                byte facing = MotionFacing.DirOf(frame.Yaw, current);
                if (facing != current)
                    await mech.capsule_turn((float)facing);
                if (QDELETED(mech))
                    return;
            }

            MotionParams parameters = MechParamsOf(m, seam, canMove);
            var events = new MotionEvents();
            int fromX = m.State.X, fromY = m.State.Y;
            ushort yawBefore = m.State.Yaw;
            MotionStep.Step(ref m.State, in frame, in parameters, seam.View, events, m.Id);
            if (!turns)
                m.State.Yaw = yawBefore; // the replicated yaw is the armour's; a prediction steps its own view's, and only the position is reconciled
            await ApplyMechEvents(world, seam, m, events, parameters, MovedDir(m.State.X - fromX, m.State.Y - fromY, wishDir));
            if (QDELETED(mech))
                return;
            if (!m.State.Slide.Active)
            {
                long dx = m.State.X - fromX, dy = m.State.Y - fromY;
                m.StrideTravel += (int)Math.Sqrt(dx * dx + dy * dy);
                if (m.StrideTravel >= MechStride)
                {
                    m.StrideTravel %= MechStride;
                    mech.play_stepsound();
                }
            }
            FollowMech(m);
            m.NextParams = MechParamsOf(m, seam, canMove);
        }
        finally
        {
            m.Busy = false;
        }
    }

    // The way it moved this tick as a BYOND direction, for tg's pushes (the wish's when it stands against a body).
    private static byte MovedDir(int dx, int dy, byte wishDir) => dx == 0 && dy == 0 ? wishDir : SectorDir(dx, dy);

    private static void ForgetOldMechContacts(MechCapsule m, long tick)
    {
        if (m.ContactReady.Count == 0)
            return;
        var old = new List<long>();
        foreach (KeyValuePair<long, long> pair in m.ContactReady)
        {
            if (pair.Value <= tick)
                old.Add(pair.Key);
        }
        foreach (long id in old)
            m.ContactReady.Remove(id);
    }

    // As ApplyEvents for a mob: a crossing is tg's Move, a refused one clamps the capsule back; a press is a Move into the blocked tile, so Bump runs
    // (doors open by the occupants' access, try_bumpsmash); a contact is the Bump tg's dense mech gives a body in its way.
    private static async DmTask ApplyMechEvents(World world, Seam seam, MechCapsule m, MotionEvents events, MotionParams parameters, byte movingDir)
    {
        obj_vehicle_sealed_mecha mech = m.Mech;
        int z = m.State.Z;
        bool moving = true;
        mech.capsule_bump_dir = movingDir;
        for (int i = 0; i < events.Count; i++)
        {
            if (QDELETED(mech))
                return;
            MotionEvent e = events[i];
            switch (e.Kind)
            {
                case MotionEventKind.Crossed:
                {
                    if (!moving)
                        break;
                    if (!MechStandsOn(mech, e.FromX, e.FromY, z))
                    {
                        moving = false;
                        break;
                    }
                    if (!await MechMove(m, locate(e.ToX, e.ToY, z), e.Dir, seam.SpeedScale))
                    {
                        moving = false;
                        if (MechStandsOn(mech, e.FromX, e.FromY, z))
                            MotionStep.ClampToTile(ref m.State, e.FromX, e.FromY, MotionMath.MechRadius);
                    }
                    break;
                }
                case MotionEventKind.Pressing:
                {
                    if (!moving || !MechStandsOn(mech, e.FromX, e.FromY, z))
                        break;
                    turf to = locate(e.ToX, e.ToY, z);
                    if (to == null)
                        break;
                    if (await MechMove(m, to, e.Dir, seam.SpeedScale))
                        moving = false; // the face was not blocked after all: the capsule follows loc
                    break;
                }
                case MotionEventKind.Contact:
                {
                    atom other = seam.Capsules.TryGetValue(e.OtherId, out Capsule body) ? body.Mob : seam.Mechs.TryGetValue(e.OtherId, out MechCapsule second) ? second.Mech : null;
                    if (other == null || QDELETED(other))
                        break;
                    long tick = world.Tick;
                    if (m.ContactReady.TryGetValue(e.OtherId, out long ready) && tick < ready)
                        break;
                    m.ContactReady[e.OtherId] = tick + parameters.MoveDelayTicks;
                    await mech.Bump(other);
                    break;
                }
            }
        }
        if (!QDELETED(mech))
            mech.capsule_bump_dir = 0;
    }

    private static bool MechStandsOn(obj_vehicle_sealed_mecha mech, int x, int y, int z)
        => mech.loc is turf t && (int)t.x == x && (int)t.y == y && (int)t.z == z;

    // tg's step of the mech: vehicle_move's set_glide_size and Move, update_dir FALSE (DV15: the view owns dir). PASSMOB and PASSVEHICLE for the call: the bodies
    // on the tile are capsules, met by the circles (DV16's reason). True when it stands on that turf after it.
    private static async DmTask<bool> MechMove(MechCapsule m, turf to, byte dir, float speedScale)
    {
        if (to == null)
            return false;
        obj_vehicle_sealed_mecha mech = m.Mech;
        mech.set_glide_size(DELAY_TO_GLIDE_SIZE(Dm.num(mech.movedelay) / speedScale));
        float pass = mech.pass_flags;
        Dm.combine(ref mech.pass_flags, PASSMOB);
        Dm.combine(ref mech.pass_flags, PASSVEHICLE);
        m.Bridging = true;
        object moved;
        try
        {
            moved = await mech.Move(to, (float)dir, 0, FALSE);
        }
        finally
        {
            m.Bridging = false;
            mech.pass_flags = pass;
        }
        return Dm.truthy(moved) && ReferenceEquals(mech.loc, to);
    }
}

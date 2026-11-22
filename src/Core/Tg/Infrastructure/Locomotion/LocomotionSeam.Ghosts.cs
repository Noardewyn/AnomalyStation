using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Owner: the ghost's free flight (the owner, 2026-10-19). The capsules of ghosts (tg's observers): continuous, through walls,
// their crossings tg's own client/Move gate for a mob that is not living and the observer's Move.

public static partial class LocomotionSeam
{
    /// <summary>The share of DV54's ghost speed a ghost flies at: the owner asked for 30 per cent less (2026-10-19).</summary>
    public const float GhostSpeedShare = 0.7f;

    /// <summary>
    /// DEVIATION(DV68): a ghost's speed, tiles a tick: DV54's tile every <see cref="client.GhostMoveDelay"/> deciseconds (10 tiles a second;
    /// tg's client/Move moves an observer every tick) times <see cref="GhostSpeedShare"/>: 7 tiles a second.
    /// </summary>
    public static float GhostTilesPerTick => MotionMath.TickDeciseconds / client.GhostMoveDelay * GhostSpeedShare;

    // One ghost's capsule: as a living mob's, but it collides with nothing but the map's edge and touches no one.
    internal sealed class GhostCapsule
    {
        internal mob_dead_observer Mob;
        internal long Id;
        internal MotionState State;
        internal MotionInput Frame;
        internal bool Framed;
        internal MotionParams NextParams;
        internal bool Busy;

        internal static GhostCapsule Create(mob_dead_observer ghost)
        {
            var g = new GhostCapsule { Mob = ghost, Id = ghost.dm_id };
            ushort yaw = MotionFacing.YawOf((byte)Dm.num(ghost.dir));
            turf where = get_turf(ghost) as turf;
            g.State = where != null ? MotionState.AtTile((int)where.x, (int)where.y, (int)where.z, yaw) : new MotionState { Yaw = yaw };
            g.Frame = MotionInput.Idle(0, yaw);
            g.NextParams = ParamsOf(ghost);
            return g;
        }

        internal void Hash(ref StateHasher h)
        {
            h.Add(0x47484F53); // "GHOS": a ghost's capsule
            h.Add(Id);
            h.Add(State.X);
            h.Add(State.Y);
            h.Add(State.Z);
            h.Add((int)State.Yaw);
            h.Add(State.Slide.TargetX);
            h.Add(State.Slide.TargetY);
            h.Add((int)State.Slide.TicksLeft);
            h.Add((long)Frame.Seq);
            h.Add((int)Frame.MoveX);
            h.Add((int)Frame.MoveY);
            h.Add((int)Frame.Yaw);
            h.Add(Framed);
            h.Add(NextParams.CanMove);
            h.Add(Busy);
        }

        internal (int X, int Y, int Z) Tile => State.Slide.Active
            ? (MotionMath.TileOf(State.Slide.TargetX), MotionMath.TileOf(State.Slide.TargetY), State.Z)
            : (State.TileX, State.TileY, State.Z);
    }

    /// <summary>From now on the bridge flies <paramref name="ghost"/> (inside the world: the player became it, or joined back into it).</summary>
    public static void RegisterGhost(mob_dead_observer ghost)
    {
        ArgumentNullException.ThrowIfNull(ghost);
        Seam seam = Seam.Of(World.Current);
        if (!seam.Installed)
            throw new InvalidOperationException("LocomotionSeam.RegisterGhost: install the locomotion phase first (LocomotionSeam.Install).");
        if (QDELETED(ghost) || seam.Ghosts.ContainsKey(ghost.dm_id))
            return;
        seam.Ghosts.Add(ghost.dm_id, GhostCapsule.Create(ghost));
    }

    // The parameters of a ghost's capsule: client/Move's own checks for a mob that is not living (mob_movement.dm:55-61) that
    // can be read without sending its signal; the gate's signal is asked at each crossing.
    private static MotionParams ParamsOf(mob_dead_observer ghost)
    {
        bool canMove = ghost.client != null && ghost.loc != null && !HAS_TRAIT(ghost, TRAIT_NO_TRANSFORM);
        return new MotionParams(GhostTilesPerTick, MotionMath.DefaultRadius, canMove, 1, NoClip: true);
    }

    private static void RunGhosts(World world, Seam seam)
    {
        if (seam.Ghosts.Count == 0)
            return;
        var ids = new List<long>(seam.Ghosts.Keys);
        foreach (long id in ids)
        {
            if (!seam.Ghosts.TryGetValue(id, out GhostCapsule g))
                continue;
            if (QDELETED(g.Mob))
            {
                seam.Ghosts.Remove(id);
                continue;
            }
            if (g.Busy)
                continue;
            object caller_usr = world.usr;
            try
            {
                world.usr = g.Mob;
                Waiting.Detach(StepGhost(world, seam, g));
            }
            finally
            {
                world.usr = caller_usr;
            }
        }
    }

    private static void FollowGhosts(Seam seam)
    {
        if (seam.Ghosts.Count == 0)
            return;
        var ids = new List<long>(seam.Ghosts.Keys);
        foreach (long id in ids)
        {
            GhostCapsule g = seam.Ghosts[id];
            if (QDELETED(g.Mob))
            {
                seam.Ghosts.Remove(id);
                continue;
            }
            if (!g.Busy)
                FollowGhost(g);
        }
    }

    private static async DmTask StepGhost(World world, Seam seam, GhostCapsule g)
    {
        g.Busy = true;
        try
        {
            mob_dead_observer ghost = g.Mob;
            FollowGhost(g); // tg moved it: an orbit, a teleport, a jump
            MotionParams parameters = ParamsOf(ghost);
            var events = new MotionEvents();
            MotionStep.Step(ref g.State, in g.Frame, in parameters, seam.View, events, g.Id);
            int z = g.State.Z;
            for (int i = 0; i < events.Count; i++)
            {
                if (QDELETED(ghost))
                    return;
                MotionEvent e = events[i];
                if (e.Kind != MotionEventKind.Crossed)
                    continue;
                turf from = ghost.loc as turf;
                if (from == null || (int)from.x != e.FromX || (int)from.y != e.FromY || (int)from.z != z)
                    break; // tg put it elsewhere: it follows
                turf to = locate(e.ToX, e.ToY, z);
                if (to == null || !await GhostMove(ghost, to, e.Dir))
                {
                    MotionStep.ClampToTile(ref g.State, e.FromX, e.FromY, MotionMath.DefaultRadius);
                    break;
                }
            }
            if (!QDELETED(ghost))
            {
                FollowGhost(g);
                g.NextParams = ParamsOf(ghost);
            }
        }
        finally
        {
            g.Busy = false;
        }
    }

    // tg's client/Move for a mob that is not living (mob_movement.dm:55-61): its pre-move signal may refuse, then the observer's
    // Move (observer.dm:328: abstract_move, its dir and glide). True when the ghost is on that turf after it.
    private static async DmTask<bool> GhostMove(mob_dead_observer ghost, turf to, byte dir)
    {
        if (HAS_TRAIT(ghost, TRAIT_NO_TRANSFORM))
            return false;
        if (Dm.truthy(ghost._listen_lookup?[COMSIG_MOB_CLIENT_PRE_NON_LIVING_MOVE])
            && Dm.truthy(Dm.bitand(ghost._SendSignal(COMSIG_MOB_CLIENT_PRE_NON_LIVING_MOVE, Dm.list(ghost, to, (float)dir)), COMSIG_MOB_CLIENT_BLOCK_PRE_NON_LIVING_MOVE)))
            return false;
        await ghost.Move(to, (float)dir);
        return ReferenceEquals(ghost.loc, to);
    }

    // The ghost's capsule follows a loc change it did not make: it snaps (a ghost's moves are abstract_move, never glided by tg's delay).
    private static void FollowGhost(GhostCapsule g)
    {
        turf here = get_turf(g.Mob) as turf;
        if (here == null)
            return;
        (int tx, int ty, int tz) = g.Tile;
        if ((int)here.x == tx && (int)here.y == ty && (int)here.z == tz)
            return;
        MotionStep.Displace(ref g.State, (int)here.x, (int)here.y, (int)here.z, 0);
    }
}

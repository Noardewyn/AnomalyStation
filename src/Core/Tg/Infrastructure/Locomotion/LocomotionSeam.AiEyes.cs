using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Owner: the AI eye's free flight (the owner, 2026-11-14). DEVIATION(DV423): the AI's eye is a capsule like a ghost's (DV68), through
// walls, flown by its AI's frame while tg's client/Move gate lets the AI move (capsule_move_gate's isAI branch, FlyAiEye), at a third of
// tg's lowest speed times tg's sprint steps (AIMove_capsule); each tile it crosses is tg's setLoc of the eye, as AIMove's steps.
// DEVIATION(DV429): a holocall's eye (tg's relaymove steps it a tile at a time) flies the same way at the ghost's speed, flown by its
// caller's frame (FlyRemoteEye), and the hologram either eye projects glides where the eye is (GlideHologram).

public static partial class LocomotionSeam
{
    /// <summary>The AI eye's speed, tiles a tick, at tg's lowest sprint: the ghost's 7 tiles a second (tg steps it 20 a second).</summary>
    public static float AiEyeTilesPerTick => GhostTilesPerTick;

    internal sealed class EyeCapsule
    {
        internal mob_eye_camera Eye; // the AI's eye, or a holocall's (DV429)
        internal long Id;
        internal MotionState State;
        internal MotionInput Frame;
        internal float Speed; // tiles a tick the gate granted
        internal long GrantTick = -1; // the tick the AI's gate let the eye move

        internal void Hash(ref StateHasher h)
        {
            h.Add(0x41494559); // "AIEY": an AI eye's capsule
            h.Add(Id);
            h.Add(State.X);
            h.Add(State.Y);
            h.Add(State.Z);
            h.Add((int)State.Yaw);
            h.Add(Speed);
            h.Add(GrantTick);
        }
    }

    /// <summary>
    /// The isAI branch of the movement gate: the AI's eye flies by its capsule this tick (tg's AIMove bookkeeping runs, no steps).
    /// False when there is no eye to fly (no locomotion seam, no eye, the AI not a capsule mob): tg's AIMove steps it.
    /// </summary>
    public static bool FlyAiEye(mob_living_silicon_ai ai)
    {
        World world = World.CurrentOrNull;
        mob_eye_camera_ai eye = ai?.eyeobj;
        if (world == null || eye == null || QDELETED(eye) || eye.loc is not turf here)
            return false;
        Seam seam = Seam.Of(world);
        if (!seam.Installed || !seam.Capsules.TryGetValue(ai.dm_id, out Capsule body) || !ReferenceEquals(body.Mob, ai))
            return false;
        if (!seam.Eyes.TryGetValue(eye.dm_id, out EyeCapsule e) || !ReferenceEquals(e.Eye, eye))
        {
            e = new EyeCapsule { Eye = eye, Id = eye.dm_id, State = MotionState.AtTile((int)here.x, (int)here.y, (int)here.z, body.Frame.Yaw) };
            seam.Eyes[eye.dm_id] = e;
        }
        e.Frame = body.Frame;
        e.Speed = AiEyeTilesPerTick * ai.AIMove_capsule();
        e.GrantTick = world.Tick;
        return true;
    }

    /// <summary>
    /// DV429: the remote_control branch of the movement gate for a holocall's eye: it flies by its capsule this tick, flown by its
    /// caller's frame, instead of tg's relaymove steps. False when it cannot (no locomotion seam, the caller not a capsule mob): relaymove steps it.
    /// </summary>
    public static bool FlyRemoteEye(mob_living user, mob_eye_camera_remote_holo eye)
    {
        World world = World.CurrentOrNull;
        if (world == null || user == null || eye == null || QDELETED(eye) || eye.loc is not turf here)
            return false;
        Seam seam = Seam.Of(world);
        if (!seam.Installed || !seam.Capsules.TryGetValue(user.dm_id, out Capsule body) || !ReferenceEquals(body.Mob, user))
            return false;
        if (!seam.Eyes.TryGetValue(eye.dm_id, out EyeCapsule e) || !ReferenceEquals(e.Eye, eye))
        {
            e = new EyeCapsule { Eye = eye, Id = eye.dm_id, State = MotionState.AtTile((int)here.x, (int)here.y, (int)here.z, body.Frame.Yaw) };
            seam.Eyes[eye.dm_id] = e;
        }
        e.Frame = body.Frame;
        e.Speed = AiEyeTilesPerTick;
        e.GrantTick = world.Tick;
        return true;
    }

    // The eye's owner is gone (the AI's eye lost its AI, a holocall's eye its caller): it no longer flies
    private static bool Orphaned(mob_eye_camera eye)
        => eye is mob_eye_camera_ai ai_eye ? ai_eye.ai == null : (eye as mob_eye_camera_remote)?.user_ref?.resolve() == null;

    // After the capsules' steps (their gates granted this tick's flights): each eye flies, its crossings tg's setLoc.
    private static void RunAiEyes(World world, Seam seam)
    {
        if (seam.Eyes.Count == 0)
            return;
        foreach (long id in new List<long>(seam.Eyes.Keys))
        {
            EyeCapsule e = seam.Eyes[id];
            if (QDELETED(e.Eye) || Orphaned(e.Eye))
            {
                seam.Eyes.Remove(id);
                continue;
            }
            FollowEye(e);
            (int bx, int by, int bz) = (e.State.X, e.State.Y, e.State.Z);
            bool granted = e.GrantTick == world.Tick;
            MotionInput frame = granted ? e.Frame : MotionInput.Idle(e.Frame.Seq, e.Frame.Yaw);
            var parameters = new MotionParams(e.Speed, MotionMath.DefaultRadius, granted, 1, NoClip: true);
            var events = new MotionEvents();
            MotionStep.Step(ref e.State, in frame, in parameters, seam.View, events, e.Id);
            for (int i = 0; i < events.Count; i++)
            {
                MotionEvent ev = events[i];
                if (ev.Kind != MotionEventKind.Crossed)
                    continue;
                turf from = e.Eye.loc as turf;
                if (from == null || (int)from.x != ev.FromX || (int)from.y != ev.FromY)
                    break; // tg put it elsewhere: it follows
                turf to = locate(ev.ToX, ev.ToY, e.State.Z);
                if (to != null)
                    e.Eye.setLoc(to);
                if (to == null || !ReferenceEquals(e.Eye.loc, to))
                {
                    MotionStep.ClampToTile(ref e.State, ev.FromX, ev.FromY, MotionMath.DefaultRadius);
                    break;
                }
            }
            FollowEye(e);
            GlideHologram(world, e, bx, by, bz);
        }
    }

    // DEVIATION(DV429): the hologram the AI projects stands where its eye flies and glides with it (tg's move_hologram still moves it a tile
    // at a time as the eye crosses them), so it is no hop from tile middle to tile middle. Off its eye's tile it is drawn on its own.
    private static void GlideHologram(World world, EyeCapsule e, int bx, int by, int bz)
    {
        // the AI's hologram is its holopad's masters[ai]; a caller's is masters[caller] of the pad that answered (the eye's origin)
        (obj_machinery_holopad pad, datum owner) = e.Eye is mob_eye_camera_ai ai_eye
            ? (ai_eye.ai?.current as obj_machinery_holopad, (datum)ai_eye.ai)
            : ((e.Eye as mob_eye_camera_remote_holo)?.origin_ref?.resolve() as obj_machinery_holopad, (e.Eye as mob_eye_camera_remote)?.user_ref?.resolve() as datum);
        if (pad == null || owner == null || pad.masters?[owner] is not obj_effect_overlay_holo_pad_hologram holo || QDELETED(holo))
            return;
        MotionState s = e.State;
        if (holo.loc is not turf t || (int)t.x != s.TileX || (int)t.y != s.TileY || (int)t.z != s.Z)
        {
            FlightSeam.Glide(holo, FlightArc.None);
            return;
        }
        const float unit = MotionMath.SubunitsPerTile;
        float vx = (s.X - bx) / unit, vy = (s.Y - by) / unit;
        if (bz != s.Z || Math.Abs(vx) > 1 || Math.Abs(vy) > 1)
            (vx, vy) = (0, 0); // a jump (a camera click, a tracking snap): no glide across it
        FlightArc was = holo.dm_flight;
        if (vx == 0 && vy == 0 && !was.IsNone && !was.IsMoving && was.X0 == s.X / unit && was.Y0 == s.Y / unit && was.Z == s.Z)
            return; // still: its rest point stands
        FlightSeam.Glide(holo, new FlightArc(world.Tick, s.X / unit, s.Y / unit, s.Z, 0, vx, vy, 0, 0));
    }

    private static void FollowAiEyes(Seam seam)
    {
        foreach (EyeCapsule e in seam.Eyes.Values)
            if (!QDELETED(e.Eye))
                FollowEye(e);
    }

    // The eye's capsule follows a setLoc it did not make (a camera click, view_core, a tracking jump): it snaps, as tg's setLoc does.
    private static void FollowEye(EyeCapsule e)
    {
        if (get_turf(e.Eye) is not turf here)
            return;
        int tx = e.State.Slide.Active ? MotionMath.TileOf(e.State.Slide.TargetX) : e.State.TileX;
        int ty = e.State.Slide.Active ? MotionMath.TileOf(e.State.Slide.TargetY) : e.State.TileY;
        if ((int)here.x == tx && (int)here.y == ty && (int)here.z == e.State.Z)
            return;
        MotionStep.Displace(ref e.State, (int)here.x, (int)here.y, (int)here.z, 0);
    }
}

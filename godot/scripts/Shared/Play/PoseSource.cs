using Anomaly.Core.Motion;
using Anomaly.Protocol;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The poses the renderer draws (<see cref="IPoseSource"/>, M2 scope 2d, 1.6): the own capsule from the predictor, every other
/// capsule mob from its samples at the render time (<see cref="SampleInterpolator"/>: the newest sample held, a late Delta
/// bridged by at most a tick of extrapolation, the visible error of a catch-up bounded by <see cref="PoseSmoother"/>), and the
/// tile-driven movables (items, the singularity) by the own body's glide along their tiles (<see cref="TileDrift"/>, DV67). Each entity is evaluated once per frame (<see cref="BeginFrame"/>
/// starts a frame); P7's entity layer, the pick and the speed statistics all read the same answer.
/// </summary>
public sealed class PoseSource : IPoseSource
{
    private readonly ReplicaWorld _replica;
    private readonly OwnPrediction _own;
    private readonly InterpolationOptions _options = new();
    private readonly Dictionary<long, Track> _tracks = new();
    private readonly PoseSample[] _buffer;
    private long _frame;
    private double _renderTick;
    private double _dt;
    private double _clock; // the real time of the frames so far (seconds)
    private readonly Func<int, int, int, bool> _ladderAt;

    public PoseSource(ReplicaWorld replica, OwnPrediction own)
    {
        _replica = replica ?? throw new ArgumentNullException(nameof(replica));
        _own = own;
        _buffer = new PoseSample[replica.History];
        _ladderAt = replica.LadderAt;
    }

    /// <summary>Starts a frame: the render time (server ticks with a fraction) and the real time since the last frame.</summary>
    public void BeginFrame(double renderTick, double dtSeconds)
    {
        _frame++;
        _renderTick = renderTick;
        _dt = dtSeconds;
        _clock += Math.Max(0, dtSeconds);
        bool drawn = _own != null && _own.Active && !_own.TileDriven;
        if (drawn)
        {
            (float x, float y, int z) = _own.RenderPosition();
            (_ownMoveX, _ownMoveY) = _ownDrawn && z == _ownZ ? (x - _ownX, y - _ownY) : (0f, 0f);
            _ownStep = MathF.Sqrt(_ownMoveX * _ownMoveX + _ownMoveY * _ownMoveY);
            _ownTravel += _ownStep;
            (_ownX, _ownY, _ownZ) = (x, y, z);
        }
        _ownDrawn = drawn;
    }

    /// <summary>DV533: the entity the own mob last ctrl-clicked (tg's pull, <c>base_click_ctrl</c>), 0 for none: drawn trailing the own body while it is pulled.</summary>
    public long PullTarget { get; set; }

    /// <summary>DV533: how far behind the own body's centre a pulled thing is drawn (tiles): tg's pulled thing stands in the next tile.</summary>
    public const float TrailTiles = 1f;

    /// <summary>DV533: the nearest a pulled thing comes to the own body's centre (tiles) when the puller walks back into it.</summary>
    public const float TrailNearest = 0.7f;

    /// <summary>DV533: how fast the drawn point is brought back toward the tile the server says (seconds: the time constant).</summary>
    public const double TrailCorrectionSeconds = 0.4;

    /// <summary>DV533: how far past its server tile's edges the drawn point may be before it is brought back (tiles).</summary>
    public const float TrailSlack = 0.3f;

    /// <summary>DV533: the own body's walk after the thing's last step (tiles) after which it is no longer taken for pulled (tg moves it every tile).</summary>
    public const double TrailWalk = 2.5;

    /// <summary>DV533: the time the drawing takes to stop trailing (seconds); it starts trailing where it is drawn, at once.</summary>
    public const double TrailBlendSeconds = 0.25;

    /// <summary>DV533: the rope taken in per tile walked (tiles) when trailing started farther than <see cref="TrailTiles"/> behind.</summary>
    public const float TrailReel = 0.5f;

    /// <summary>DV535: the nearest a pushed thing is drawn to the own body's centre along the push (tiles): the capsule's edge on the thing's side.</summary>
    public const float PushContact = 0.5f + MotionMath.DefaultRadius;

    /// <summary>DV535: how a pushed thing too slow to slide eases onto its tile (seconds: the time constant).</summary>
    public const double PushEaseSeconds = 0.1;

    /// <summary>DV535: the speed below which a pushed thing creeps onto its tile instead of sliding (tiles a second).</summary>
    public const float PushCreep = 0.5f;

    private bool _ownDrawn;
    private float _ownX, _ownY;
    private int _ownZ;
    private double _ownTravel; // DV533: tiles the own body has been drawn walking
    private float _ownStep; // DV533: of them, this frame's
    private float _ownMoveX, _ownMoveY; // DV535: this frame's walk

    /// <summary>D325: the stairs' rise under a point (DV370), so a climb up the flight is no level jump for <see cref="LevelRise"/>.</summary>
    public RampHeight RampOn { get; set; }

    /// <summary>Frees what is kept for an entity that despawned.</summary>
    public void Forget(long id) => _tracks.Remove(id);

    public bool TryGetPose(ReplicaEntity entity, out RenderPose pose)
    {
        if (!_tracks.TryGetValue(entity.Id, out Track track))
        {
            track = new Track();
            _tracks[entity.Id] = track;
        }
        if (track.Frame == _frame)
        {
            pose = track.Pose;
            return track.Has;
        }
        track.Frame = _frame;
        track.Has = Evaluate(entity, track, out track.Pose);
        if (track.Has && !entity.IsOwn)
            track.Pose = Carry(entity, track, track.Pose);
        // DEVIATION(DV573): a mob buckled to an object is drawn where that object is drawn (a dragged bed and its patient glide as one; the
        // mob's own glide and the object's trail behind the puller differ, and a diagonal step snapped the mob)
        if (track.Has && !entity.IsOwn && PostureBits.IsBuckled(entity.State.Posture) && BuckleHost(entity, track) is ReplicaEntity host
            && TryGetPose(host, out RenderPose carrier) && carrier.Z == track.Pose.Z)
            track.Pose = track.Pose with { X = carrier.X, Y = carrier.Y };
        if (track.Has)
        {
            // DV371: no level change is a jump (an announced ride crosses the level over its ticks, a fall eases down)
            RenderPose p = track.Pose;
            float ramp = RampOn != null && RampOn(p.X, p.Y, p.Z, out float h) ? h : 0f;
            // DV397: a ladder's climber is drawn on the ladder
            (float px, float py) = track.Pull.Pull(p.X, p.Y, p.Z, entity.State.Ride, _renderTick, _replica.Tick, _dt, _ladderAt);
            // DV416: a free motion's height above its level's floor counts as the drawn height, so its fall to the level below is no jump to ease
            track.Pose = p with { X = px, Y = py, Rise = track.Rise.Rise(p.X, p.Y, p.Z, ramp + track.Lift, entity.State.Ride, _renderTick, _replica.Tick, _clock) + track.Lift };
        }
        pose = track.Pose;
        return track.Has;
    }

    // What a buckled mob is buckled to: the object on its server tile (tg moves the two together), not a mob, an item or a visual; of several, the
    // last moved. Kept while both stay on one tile; else looked up at most once a tick (the replica has no index by tile).
    private ReplicaEntity BuckleHost(ReplicaEntity mob, Track track)
    {
        EntityState s = mob.State;
        if (track.HostId != 0 && _replica.TryGet(track.HostId, out ReplicaEntity known) && (known.State.X, known.State.Y, known.State.Z) == (s.X, s.Y, s.Z))
            return known;
        if (track.HostSearched == _replica.Tick)
            return null; // none here this tick (the object's move may come in a later tick's update)
        track.HostSearched = _replica.Tick;
        track.HostId = 0;
        ReplicaEntity best = null;
        foreach (ReplicaEntity e in _replica.Entities)
        {
            EntityState o = e.State;
            if ((o.X, o.Y, o.Z) != (s.X, s.Y, s.Z) || e.Id == mob.Id || e.IsVisual || e.IsMob() || e.TypePath?.StartsWith("/obj/", StringComparison.Ordinal) != true
                || e.TypePath.StartsWith("/obj/item", StringComparison.Ordinal) || e.TypePath.StartsWith("/obj/effect", StringComparison.Ordinal))
                continue;
            if (best == null || o.LastMoveTick > best.State.LastMoveTick)
                best = e;
        }
        track.HostId = best?.Id ?? 0;
        return best;
    }

    private bool Evaluate(ReplicaEntity entity, Track track, out RenderPose pose)
    {
        EntityState state = entity.State;
        // DEVIATION(DV416) stage 2: the own body thrown is drawn on the server's arc, the camera with it (its capsule takes no wish in the air)
        track.Lift = 0f;
        FlightArc arc = entity.FlightAt(_renderTick);
        if (!arc.IsNone && (arc.IsMoving || !entity.IsOwn))
            return ArcPose(entity, track, arc, out pose);
        // D151: a ghost (tile-driven, no capsule) is drawn as any entity, by tg's glide from its replicated tiles.
        if (entity.IsOwn && _own != null && _own.Active && !_own.TileDriven)
        {
            (float x, float y, int z) = _own.RenderPosition();
            MotionState predicted = _own.Predicted;
            bool lying = predicted.Posture == Posture.Lying;
            int angle = PostureBits.IsLying(state.Posture) ? PostureBits.LyingAngle(state.Posture) : 90;
            byte posture = PostureBits.Encode(lying, lying ? angle : 0, PostureBits.IsLimp(state.Posture));
            pose = new RenderPose(x, y, z, state.Dir, MoveWish.YawByte(_own.Yaw), posture);
            return true;
        }
        // DEVIATION(DV403): a thing on a moving tram is drawn in the tram's frame: where it stands on the car, the car where it is at the own
        // prediction's time (the own capsule rides it there), moving evenly
        bool onTram = _replica.TryTramAt(state.HasPos ? MotionMath.TileOf(state.PosX) : state.X, state.HasPos ? MotionMath.TileOf(state.PosY) : state.Y, state.Z, _replica.Tick, out TramRide tram);
        if (entity.HasPos)
        {
            int count = entity.SampleCount;
            (int ux, int uy) = tram.Unit;
            for (int age = 0; age < count; age++)
            {
                EntitySample s = entity.Sample(age);
                int px = s.PosX, py = s.PosY;
                if (onTram && s.Z == state.Z && tram.Covers(MotionMath.TileOf(px), MotionMath.TileOf(py), tram.StepsBy(s.Tick)))
                {
                    int carried = tram.StepsBy(s.Tick) * MotionMath.SubunitsPerTile; // the sample on the car, as at the trip's start
                    px -= carried * ux;
                    py -= carried * uy;
                }
                _buffer[age] = new PoseSample(s.Tick, px, py, s.Z, s.Yaw, s.Posture);
            }
            if (!SampleInterpolator.TryEvaluate(_buffer.AsSpan(0, count), _renderTick, _replica.Tick, _options, out SampledPose sampled))
            {
                pose = default;
                return false;
            }
            (float dx, float dy) = track.Smoother.Follow(sampled.X, sampled.Y, sampled.Z, _dt);
            if (onTram)
            {
                float steps = (float)tram.StepsAt(DrawTick());
                dx += steps * ux;
                dy += steps * uy;
            }
            pose = new RenderPose(dx, dy, sampled.Z, state.Dir, (byte)((int)MathF.Round(sampled.Yaw) & 255), sampled.Posture);
            return true;
        }
        if (onTram)
        {
            (float tx, float ty) = _replica.TramDrawShift(state.X, state.Y, state.Z, _replica.Tick, DrawTick());
            pose = new RenderPose(state.X + tx, state.Y + ty, state.Z, state.Dir, BillboardMath.YawOfDir(state.Dir), 0);
            return true;
        }
        if (entity.SampleCount > 0)
        {
            // DV67: a tile-driven thing (an item pulled or thrown, the singularity, a ghost) is drawn by the own body's glide along its tiles
            // (by the time since it was last drawn: a thing out of sight for a while is not evaluated every frame)
            double since = track.Drift == null ? 0 : _clock - track.DriftClock;
            track.DriftClock = _clock;
            if (track.Drift == null && track.HadFlight)
            {
                // DV416: from where its free motion left it to its tile
                track.Drift = TileDrift.NewGlide(entity.TypePath);
                track.Drift.Start(track.FlightEnd.X, track.FlightEnd.Y, track.FlightEnd.Z);
            }
            track.HadFlight = false;
            (float gx, float gy, int gz) = TileDrift.Step(track.Drift ??= TileDrift.NewGlide(entity.TypePath), state.X, state.Y, state.Z, since, state.GlideSize);
            pose = new RenderPose(gx, gy, gz, state.Dir, BillboardMath.YawOfDir(state.Dir), 0); // a mob with an AI turns by its dir
            return true;
        }
        pose = default;
        return false;
    }

    // DEVIATION(DV416): a thing in free motion (a throw's arc, a fall, a slide) or resting where one left it is drawn on the server's own
    // segment at the render time; when it ends the tile glide starts from the point last drawn
    private bool ArcPose(ReplicaEntity entity, Track track, FlightArc arc, out RenderPose pose)
    {
        EntityState state = entity.State;
        (double fx, double fy, double fl) = arc.At(_renderTick);
        track.Drift = null;
        track.FlightEnd = ((float)fx, (float)fy, arc.Z);
        track.HadFlight = true;
        track.Lift = (float)(fl * TileSpace.LevelHeight / FlightArc.PixelsPerTile);
        byte yaw = entity.HasPos ? state.Yaw : BillboardMath.YawOfDir(state.Dir);
        pose = new RenderPose((float)fx, (float)fy, arc.Z, state.Dir, yaw, state.Posture);
        return true;
    }

    // DEVIATION(DV533): what the own mob pulls is drawn trailing the own drawn body at tg's one tile, as on a rope: a capsule walking at an angle
    // makes tg step the pulled thing along a staircase at uneven times (into each tile the puller leaves), which its own glide or slide drew in
    // jerks, and about a step behind that tile, two tiles from the puller. The drawn point is kept within the tile the server says (seen from
    // where the own body is on the server, so the prediction's lead is no lag); it starts trailing where it is drawn (a rope that starts slack
    // or long is taken in as the body walks) and eases out of it.
    // DEVIATION(DV535): what the own body pushes is drawn no nearer it than the capsule's edge: its glide trails the server's tile, which tg
    // steps a tile ahead of the pusher, so it was drawn inside the body.
    private RenderPose Carry(ReplicaEntity entity, Track track, RenderPose pose)
    {
        EntityState s = entity.State;
        if ((s.X, s.Y, s.Z) != track.TrailTile)
        {
            (int ax, int ay, int az) = track.TrailTile;
            int sx = s.X - ax, sy = s.Y - ay;
            track.TrailTile = (s.X, s.Y, s.Z);
            track.TrailStep = _ownTravel;
            if (az == s.Z && Math.Abs(sx) + Math.Abs(sy) == 1 && !entity.HasPos && (entity.ServerState.Flags & EntityFlags.Pushable) != 0 && _ownDrawn)
            {
                // a step away from the own body right behind it (as the prediction has it) is tg's PushAM, which stops a pull of the thing
                MotionState me = _own.Predicted;
                if (me.Z == s.Z && ((me.TileX, me.TileY) == (ax - sx, ay - sy) || (me.TileX, me.TileY) == (ax, ay)))
                {
                    if (track.PushDir != (sx, sy))
                        (track.PushAlong, track.PushSpeed) = (float.NaN, 0f); // a new push starts where it is drawn
                    track.PushDir = (sx, sy);
                    if (entity.Id == PullTarget)
                        PullTarget = 0;
                }
            }
        }
        // the drawn body walks on through its cell in the tick in progress: the push starts there, before the frame that makes it
        if (track.PushDir == (0, 0) && _own?.PushingAhead is var (px, py, pz, pdx, pdy) && (s.X, s.Y, s.Z) == (px, py, pz)
            && !entity.HasPos && (entity.ServerState.Flags & EntityFlags.Pushable) != 0)
            (track.PushDir, track.PushAlong, track.PushSpeed) = ((pdx, pdy), float.NaN, 0f);
        if (entity.Id == PullTarget || track.TrailBlend > 0)
            pose = Trail(entity, track, pose, entity.Id == PullTarget);
        if (track.PushDir != (0, 0))
            pose = PushedOut(track, s, pose);
        return pose;
    }

    private RenderPose Trail(ReplicaEntity entity, Track track, RenderPose pose, bool target)
    {
        EntityState s = entity.State;
        OwnState own = _replica.Own;
        int apart = Math.Max(Math.Abs(s.X - own.Motion.TileX), Math.Abs(s.Y - own.Motion.TileY));
        // tg keeps it in the next tile or the puller's own; two while the replica has one of their steps and not the other
        bool pulled = target && _ownDrawn && own.HasMotion && s.Z == _ownZ && own.Motion.Z == s.Z && apart <= 2 && !track.HadFlight
            && _ownTravel - track.TrailStep < TrailWalk;
        if (!pulled)
        {
            track.TrailBlend = Math.Max(0, track.TrailBlend - _dt / TrailBlendSeconds);
            float left = (float)track.TrailBlend;
            return pose with { X = pose.X + (track.TrailX - pose.X) * left, Y = pose.Y + (track.TrailY - pose.Y) * left };
        }
        if (track.TrailBlend < 1)
        {
            // it starts where it is drawn, the rope as long as it is there
            float w = (float)track.TrailBlend;
            (track.TrailX, track.TrailY) = (pose.X + (track.TrailX - pose.X) * w, pose.Y + (track.TrailY - pose.Y) * w);
            track.TrailBlend = 1;
            float d0 = MathF.Sqrt((track.TrailX - _ownX) * (track.TrailX - _ownX) + (track.TrailY - _ownY) * (track.TrailY - _ownY));
            (track.TrailNear, track.TrailFar) = (Math.Min(TrailNearest, d0), Math.Max(TrailTiles, d0));
        }
        float dx = track.TrailX - _ownX, dy = track.TrailY - _ownY, d = MathF.Sqrt(dx * dx + dy * dy);
        // the rope: pulled taut to its length, pushed off the body when the puller walks back into it; a slack or long one is taken in
        track.TrailFar = Math.Max(TrailTiles, Math.Min(track.TrailFar - TrailReel * _ownStep, Math.Max(d, TrailTiles)));
        track.TrailNear = Math.Min(TrailNearest, Math.Max(track.TrailNear, d));
        if (d > 1e-4f && (d > track.TrailFar || d < track.TrailNear))
        {
            float k = Math.Clamp(d, track.TrailNear, track.TrailFar) / d;
            (track.TrailX, track.TrailY) = (_ownX + dx * k, _ownY + dy * k);
        }
        // back into its server tile, as seen from the own body's server point
        float leadX = _ownX - MotionMath.ToTiles(own.Motion.X), leadY = _ownY - MotionMath.ToTiles(own.Motion.Y);
        const float half = 0.5f + TrailSlack;
        float bx = Math.Clamp(track.TrailX, s.X + leadX - half, s.X + leadX + half), by = Math.Clamp(track.TrailY, s.Y + leadY - half, s.Y + leadY + half);
        float share = (float)(1 - Math.Exp(-_dt / TrailCorrectionSeconds));
        track.TrailX += (bx - track.TrailX) * share;
        track.TrailY += (by - track.TrailY) * share;
        return pose with { X = track.TrailX, Y = track.TrailY };
    }

    // DV535: along the push it is kept off the body while the body is in its row, and slides on by its own speed, slowed evenly to stop on its
    // tile (tg's step puts it up to a tile ahead of the body's touch, so it goes on a little when the body stops); across the push it is drawn as its glide has it. It is let go once it and its glide
    // are on the tile and the body no longer stands behind it (in the tile it pushes from or the one before).
    private RenderPose PushedOut(Track track, EntityState s, RenderPose pose)
    {
        (int ux, int uy) = track.PushDir;
        bool alongX = ux != 0;
        int dir = ux + uy;
        float glide = alongX ? pose.X : pose.Y, tile = alongX ? s.X : s.Y;
        if (float.IsNaN(track.PushAlong))
            track.PushAlong = glide;
        float before = track.PushAlong, left = (tile - before) * dir;
        float glideSpeed = (s.GlideSize > 0 ? s.GlideSize : 8f) / 32f * MotionMath.TicksPerSecond;
        if (left > 0.001f && _dt > 0)
        {
            // it slides on by its own speed (at most its glide's), slowed evenly to stop on its tile; one too slow for that creeps there
            float speed = MathF.Min(track.PushSpeed, glideSpeed);
            if (speed > PushCreep)
            {
                speed = MathF.Max(0f, speed - speed * speed / (2f * left) * (float)_dt);
                track.PushAlong += dir * MathF.Min(left, speed * (float)_dt);
            }
            else
                track.PushAlong += dir * MathF.Min(PushCreep * (float)_dt, left * (float)(1 - Math.Exp(-_dt / PushEaseSeconds)));
        }
        else if (left < -0.001f && _dt > 0)
            track.PushAlong += dir * MathF.Max(left, -PushCreep * (float)_dt); // drawn past its tile for a push that did not come: back to it
        if (_ownDrawn && (alongX ? MathF.Abs(pose.Y - _ownY) : MathF.Abs(pose.X - _ownX)) < PushContact)
        {
            float off = (alongX ? _ownX : _ownY) + dir * PushContact;
            track.PushAlong = dir > 0 ? MathF.Max(track.PushAlong, off) : MathF.Min(track.PushAlong, off);
        }
        if (_dt > 0)
            track.PushSpeed = Math.Clamp((track.PushAlong - before) * dir / (float)_dt, 0f, glideSpeed);
        MotionState me = _own.Predicted;
        bool behind = _ownDrawn && me.Z == s.Z && ((me.TileX, me.TileY) == (s.X - ux, s.Y - uy) || (me.TileX, me.TileY) == (s.X - 2 * ux, s.Y - 2 * uy));
        if (!behind && MathF.Abs(glide - tile) < 0.01f && MathF.Abs(track.PushAlong - tile) < 0.01f)
        {
            track.PushDir = (0, 0);
            return pose;
        }
        return alongX ? pose with { X = track.PushAlong } : pose with { Y = track.PushAlong };
    }

    // DV403: the time a tram's riders are drawn at: the own prediction's (its newest frame's tick and the share of the next one), so a rider
    // and the own capsule on one car keep their places; the render time without an own capsule
    private double DrawTick()
        => _own != null && _own.Active && !_own.TileDriven ? _replica.TickOfFrame(_own.NewestSeq) + _own.Phase : _renderTick;

    /// <summary>Adds to <paramref name="into"/> every mob that has a pose, as a candidate for the camera ray (the own mob included; the pick excludes it).</summary>
    public void GatherMobs(List<PickCandidate> into)
    {
        into.Clear();
        foreach (ReplicaEntity entity in _replica.Entities)
        {
            if (!entity.IsMob() || !TryGetPose(entity, out RenderPose pose))
                continue;
            into.Add(new PickCandidate(entity.Id, pose.X, pose.Y, pose.Z, PostureBits.IsLying(pose.Posture)));
        }
    }

    private sealed class Track
    {
        public long Frame = -1;
        public bool Has;
        public RenderPose Pose;
        public readonly PoseSmoother Smoother = new();
        public OwnGlide Drift;
        public double DriftClock;
        public readonly LevelRise Rise = new();
        public readonly LadderPull Pull = new();
        public float Lift; // DV416: the free motion's height above its level's floor this frame, metres
        public bool HadFlight; // DV416: it was in free motion when last drawn
        public (float X, float Y, int Z) FlightEnd; // DV416: where it was then
        public (int X, int Y, int Z) TrailTile; // DV533: its server tile when last drawn
        public double TrailStep; // DV533: the own body's walk when that tile came
        public float TrailX, TrailY; // DV533: the rope's end
        public double TrailBlend; // DV533: how much of the rope's end is drawn (1: trailing)
        public float TrailNear, TrailFar; // DV533: the rope's bounds now, from the body's centre
        public (int X, int Y) PushDir; // DV535: the own body's push of it, else (0, 0)
        public float PushAlong = float.NaN; // DV535: where it is drawn along the push
        public float PushSpeed; // DV535: its speed along the push, tiles a second
        public long HostId; // DV573: what it is buckled to, 0 for none found
        public long HostSearched = -1; // DV573: the replica tick it was last searched for
    }
}

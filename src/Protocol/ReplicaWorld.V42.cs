using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Version 42 (M10 slice B, MessagesV42.cs): the capsule mobs' positions from the unreliable Motion messages.
public sealed partial class ReplicaWorld
{
    private long _motionTick = -1;

    /// <summary>The newest tick a Motion message carried (-1 before any): it can run ahead of <see cref="Tick"/> while a Delta is held back.</summary>
    public long MotionTick => _motionTick;

    /// <summary>The newest server tick the replica has any data of: <see cref="Tick"/> or a newer <see cref="MotionTick"/>.</summary>
    public long NewestTick => Math.Max(_tick, _motionTick);

    /// <summary>
    /// Applies the positions of a Motion: an entity the replica holds takes an entry newer than its last position; an id it does not hold
    /// is skipped (its spawn may still be on the reliable channel, or it has gone), as is an entry not newer than the entity's last one
    /// (the unreliable channel may reorder).
    /// </summary>
    public void Apply(MotionMessage motion)
    {
        ArgumentNullException.ThrowIfNull(motion);
        if (!_hasSnapshot)
            return;
        foreach (MotionEntry e in motion.Entries)
        {
            if (!_byId.TryGetValue(e.Id, out ReplicaEntity entity))
                continue;
            CheckPosition(MotionMath.TileOf(e.PosX), MotionMath.TileOf(e.PosY), entity.State.Z, _map);
            entity.Move(e, motion.Tick);
        }
        if (motion.Tick > _motionTick)
            _motionTick = motion.Tick;
    }
}

public sealed partial class ReplicaEntity
{
    private long _motionTick = -1;

    /// <summary>A Motion entry at <paramref name="tick"/>: the newest position and yaw, as a sample in tick order.</summary>
    internal void Move(in MotionEntry e, long tick)
    {
        if (tick <= _motionTick || tick < _spawnTick)
            return;
        _motionTick = tick;
        _state = _state with { HasPos = true, PosX = e.PosX, PosY = e.PosY, Yaw = e.Yaw };
        if (_count > 0 && tick < _ring[_head].Tick)
        {
            // A Delta of a later tick already gave the newest sample: it takes the position (still older than that tick's, but newer than it held).
            _ring[_head] = _ring[_head] with { PosX = e.PosX, PosY = e.PosY, Yaw = e.Yaw };
            return;
        }
        Push(tick);
    }
}

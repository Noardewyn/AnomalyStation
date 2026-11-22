using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Version 42 (M10 slice B, protocol.md section 51): the capsule mobs' positions on the unreliable channel. Examine stamps a capsule mob's
// Pos and Yaw changes without putting them in its change record and notes the tick here; every tick each session gets the position and
// yaw of every such mob that changed in the last Limits.MotionResendTicks ticks, so a lost Motion is covered by the next one and a held
// back Delta no longer stops the others' movement on the client.
public sealed partial class Replicator
{
    private readonly Dictionary<long, long> _moved = new(); // id -> the tick its Pos or Yaw last changed
    private readonly List<long> _motionIds = new(); // the ids the tick's Motion messages name, in id order
    private readonly List<long> _motionExpired = new();
    private long _motionIdsTick = -1;
    private readonly List<MotionMessage> _motionPool = new(); // what BuildMotion hands out, valid until its next call

    /// <inheritdoc/>
    public void BuildMotion(int session, List<MotionMessage> into)
    {
        into.Clear();
        if (!_sessions.TryGetValue(session, out SessionState state))
            return;
        PrepareMotionIds();
        MotionMessage message = null;
        int bytes = 0;
        long previous = 0;
        foreach (long id in _motionIds)
        {
            ref Shadow s = ref _slots[_index[id]];
            if (_interest != null ? !state.Holds(id) : s.Layer != 0 && !state.HoldsLayer(id))
                continue; // D40: a ghost's position goes only to the clients that see it (version 22); M10 slice C: only the held entities
            int entry = VarUIntBytes((ulong)(id - previous)) + 5;
            if (message == null || bytes + entry > Limits.MaxMotionBytes)
            {
                // the messages are reused call to call (each session's were a fifth of the server's allocation at 100 players, M10 slice J)
                if (into.Count == _motionPool.Count) _motionPool.Add(new MotionMessage());
                message = _motionPool[into.Count];
                message.Tick = _stateTick;
                message.Entries.Clear();
                into.Add(message);
                bytes = 1 + VarUIntBytes((ulong)_stateTick) + 2; // the id, the tick, a count of up to 16383
                previous = 0;
                entry = VarUIntBytes((ulong)id) + 5;
            }
            message.Entries.Add(new MotionEntry(id, s.PosX, s.PosY, s.Yaw));
            bytes += entry;
            previous = id;
        }
    }

    // Once per tick: the moved ids still within the resend window, in id order; the older ones are dropped.
    private void PrepareMotionIds()
    {
        if (_motionIdsTick == _stateTick)
            return;
        _motionIdsTick = _stateTick;
        _motionIds.Clear();
        _motionExpired.Clear();
        foreach ((long id, long at) in _moved)
        {
            if (at <= _stateTick - Limits.MotionResendTicks) _motionExpired.Add(id);
            else _motionIds.Add(id);
        }
        foreach (long id in _motionExpired) _moved.Remove(id);
        _motionIds.Sort();
    }

    private static int VarUIntBytes(ulong value)
    {
        int n = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            n++;
        }
        return n;
    }
}

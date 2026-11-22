using System.Linq;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Protocol version 54 (D285, DV213): the held motion. tg's ballistic gun reports its user's load, unload and rack (HeldMotionSeam); this
// keeps each user's last motion of the tick and turns it into the session's HeldMotion message after the tick.

public sealed partial class Replicator
{
    // The motions since the last BuildPrivate: the user's id to its motion and the world tick it came in (a user no session takes it from,
    // a bot, is dropped two ticks later: PruneHeldMotions).
    private readonly Dictionary<long, (HeldMotion Motion, long Tick)> _heldMotions = new();
    private HeldMotionSinkImpl _heldMotionSink;

    /// <summary>The sink of <see cref="HeldMotionSeam"/>.</summary>
    public IHeldMotionSink HeldMotionSink => _heldMotionSink ??= new HeldMotionSinkImpl(this);

    private void PruneHeldMotions(long tick)
    {
        if (_heldMotions.Count == 0)
            return;
        foreach (long stale in _heldMotions.Where(kv => kv.Value.Tick < tick - 1).Select(kv => kv.Key).ToList())
            _heldMotions.Remove(stale);
    }

    private sealed class HeldMotionSinkImpl(Replicator owner) : IHeldMotionSink
    {
        public void Moved(mob user, HeldMotion motion)
        {
            if (user.dm_id != 0)
                owner._heldMotions[user.dm_id] = (motion, owner._world?.Tick ?? 0);
        }
    }

    private sealed partial class SessionState
    {
        /// <summary>The HeldMotion message of the last <see cref="Update"/>, or null.</summary>
        public HeldMotionMessage PendingHeldMotion { get; set; }

        private void UpdateHeldMotion(Replicator owner, long ownEntity)
        {
            PendingHeldMotion = null;
            if (ownEntity != 0 && owner._heldMotions.Remove(ownEntity, out (HeldMotion Motion, long _) held))
                PendingHeldMotion = new HeldMotionMessage((HeldMotionKind)held.Motion);
        }
    }

    /// <inheritdoc/>
    public HeldMotionMessage TakeHeldMotion(int session)
    {
        if (!_sessions.TryGetValue(session, out SessionState state) || state.PendingHeldMotion == null)
            return null;
        HeldMotionMessage taken = state.PendingHeldMotion;
        state.PendingHeldMotion = null;
        return taken;
    }
}

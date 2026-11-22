using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Version 23 (DV54): the ghost's body. tg names no place for a ghost's corpse on its map; the 3D overview loses it among the
// walls, so a session whose mob is a ghost that may re-enter its body (the conditions of tg's reenter_corpse, observer.dm:
// a mind whose current is not deleted, can_reenter_corpse) gets the body's turf (its get_turf: a body in a locker is marked at
// the locker) in its private block whenever it changes, and none once the mob is no such ghost.
public sealed partial class Replicator
{
    private sealed partial class SessionState
    {
        private MapCell? _bodySent;

        private void UpdateBody(client player, bool full)
        {
            MapCell? body = BodyOf(player?.mob as mob_dead_observer);
            OwnBlock block = Block;
            block.BodyChanged = full ? body != null : body != _bodySent;
            block.Body = body;
            _bodySent = body;
        }

        private static MapCell? BodyOf(mob_dead_observer ghost)
        {
            if (ghost == null || ghost.gc_destroyed != null || !Anomaly.Core.DmShim.Dm.truthy(ghost.can_reenter_corpse))
                return null;
            mob body = ghost.mind?.current;
            if (body == null || body.gc_destroyed != null || ReferenceEquals(body, ghost))
                return null;
            // get_turf read raw (the replicator runs outside the world's step): the first turf up the body's loc chain
            atom where = body;
            for (int depth = 0; where != null && where is not turf && depth < 64; depth++)
                where = (where as atom_movable)?.loc as atom;
            return where is turf t && t.x >= 1 && t.y >= 1 && t.z >= 1 ? new MapCell((int)t.x, (int)t.y, (int)t.z) : null;
        }
    }
}

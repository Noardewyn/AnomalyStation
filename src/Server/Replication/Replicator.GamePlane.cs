using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Version 29 (the eye blur, protocol.md section 38): the game plane. tg filters a player's game view through its hud's game plane
// master controller (plane_master_controller.dm: the eye blur adds its gaussian blur there); the port has no plane masters
// (rules 13), so the controller's own appearance, with the filters tg's add_filter left on it, is interned in the session's private
// table and named in its private block whenever it changes (an appearance hint of the controller, the screen sweep, another
// controller), and none once the mob has no hud or the controller has no filters.
public sealed partial class Replicator
{
    private sealed partial class SessionState
    {
        private TableRef _gamePlaneSent;
        private long _gamePlaneId;

        private void UpdateGamePlane(Replicator owner, client player, long tick, bool full)
        {
            atom_movable_plane_master_controller plane = GamePlaneOf(player);
            TableRef current = TableRef.None;
            if (plane != null)
            {
                long id = plane.dm_id;
                bool look = full || id != _gamePlaneId || owner._appearanceHinted.Contains(id) || (_sweepPeriod > 0 && (tick + id) % _sweepPeriod == 0);
                current = look ? InternPlane(plane) : _gamePlaneSent;
                _gamePlaneId = id;
            }
            else
            {
                _gamePlaneId = 0;
            }
            OwnBlock block = Block;
            block.GamePlaneChanged = full ? !current.IsNone : current != _gamePlaneSent;
            block.GamePlane = current;
            _gamePlaneSent = current;
        }

        private TableRef InternPlane(atom_movable plane)
        {
            uint id = Tables.InternPlane(plane);
            return id == 0 ? TableRef.None : new TableRef(id, true);
        }

        // tg's owner.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] of the client's mob, read raw (the replicator runs outside
        // the world's step); null without a mob, a hud or a live controller.
        private static atom_movable_plane_master_controller GamePlaneOf(client player)
        {
            DmList controllers = player?.mob?.hud_used?.plane_master_controllers;
            if (controllers == null)
                return null;
            return controllers[Defines.PLANE_MASTERS_GAME] is atom_movable_plane_master_controller plane && plane.gc_destroyed == null && plane.dm_id != 0 ? plane : null;
        }
    }
}

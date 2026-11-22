using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Net;

namespace Anomaly.Server;

// The airflow streams (D141, DEVIATION DV34; docs/ai/specs/replication.md section 16): after the tick's Deltas and joins, each
// joined session gets the changes of the air flow around its mob (AirflowFeed.Build) on the State channel, so they follow
// the MapGrid of a session that joined this tick. Outside the world, after the step; it reads and changes nothing of it.
public sealed partial class ServerHost
{
    private void SendAirflow()
    {
        if (_airflow == null) return;
        foreach (Session s in _sessions)
        {
            if (s.State != SessionState.Joined) continue;
            int x = 0, y = 0, z = 0;
            if (TurfOf(MobOf(s)) is turf where)
            {
                x = (int)Dm.num(where.x);
                y = (int)Dm.num(where.y);
                z = (int)Dm.num(where.z);
            }
            AirflowUpdate update = _airflow.Build(s.Airflow, x, y, z);
            // M4c slice 5: an update of the floor devices alone (no runs) goes out too; Build has marked its vents sent.
            if (update == null || (update.Runs.Count == 0 && update.Vents.Count == 0)) continue;
            Send(s, Channel.State, Codec.Encode(update));
        }
    }

    // The turf the atom is on, through what holds it (a locker, a mech); outside the world, so no get_turf (map lookups).
    private static turf TurfOf(atom thing)
    {
        for (int depth = 0; depth < 16 && thing != null; depth++)
        {
            if (thing is turf t) return t;
            thing = thing.loc as atom;
        }
        return null;
    }

    /// <summary>D141: the cells of the last SSair cycle with an air flow (0 without the airflow feed).</summary>
    public int AirflowCells => _airflow?.PublishedCount ?? 0;
}

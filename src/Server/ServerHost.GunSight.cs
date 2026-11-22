using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server;

// Protocol version 92 (DV96): the gun sight. After the tick, the cone of the gun in the session's mob's active hand and the count of the mob's
// shots that hit go to its client when they changed, for the shooter's crosshair.
public sealed partial class ServerHost
{
    private void SendGunSight(Session s)
    {
        GunSightMessage now = GunSightMessage.None;
        if (s.Client?.mob is mob m && m.gc_destroyed == null)
        {
            float? cone;
            using (World.Enter()) // after the tick: tg's procs read the ambient world
                cone = GunSight.ConeOf(m);
            now = new GunSightMessage(cone != null, cone ?? 0f, (byte)m.dm_shots_landed);
        }
        if (now == s.GunSight)
            return;
        s.GunSight = now;
        Send(s, Channel.State, Codec.Encode(now));
    }
}

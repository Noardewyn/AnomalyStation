using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>The owner (2026-11-02): the dev command <c>shuttle dock</c> docks the emergency shuttle with the station at once, called or not.</summary>
public class ShuttleDockCommandTests
{
    [MetaServerFact]
    public void ShuttleDockDocksTheEmergencyShuttleAtOnce()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 31, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(20);
        using (w.World.Enter())
            Assert.Equal(SHUTTLE_IDLE, Dm.str(SSshuttle.emergency.mode));
        alice.Session.DevCommand("shuttle dock");
        w.Run(3 * 20); // SSshuttle fires once a second
        using (w.World.Enter())
            Assert.Equal(SHUTTLE_DOCKED, Dm.str(SSshuttle.emergency.mode));
    }
}

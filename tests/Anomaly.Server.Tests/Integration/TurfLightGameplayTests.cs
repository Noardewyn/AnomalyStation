using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Protocol 96: a light floor lights the room as a turf light the clients draw (tg's set_light on the turf), a screwdriver turns it off
/// and on through the wire, a light floor laid as a ceiling lights the room under it, and a late joiner has both in its map.
/// </summary>
public class TurfLightGameplayTests
{
    [Fact]
    public void ALightFloorOnTheFloorAndOnTheCeilingLightsTheRoom()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 8, TimeoutSteps = 400 });
        var alice = w.Join("Alice");
        var mob = w.MobOf(alice);
        int x, y, z;
        using (w.World.Enter())
        {
            x = (int)mob.x + 1; y = (int)mob.y; z = (int)mob.z;
            Waiting.NoSleep(locate(x, y, z).ChangeTurf(typeof(turf_open_floor_light)));
            CeilingSystem.Of(w.World).Set(x - 1, y, z, CeilingState.Of("/turf/open/floor/light"));
        }
        w.Run(10);

        // tg's working light floor: its cyan, range 3 (light_floor.dm update_appearance)
        LightState floor = alice.Session.Replica.TurfLightAt(x, y, z, false);
        Assert.False(floor.IsNone);
        Assert.Equal(3f, floor.Range);
        Assert.Equal(0x7DE1E1FFu, floor.Rgba);
        Assert.Equal(floor, alice.Session.Replica.TurfLightAt(x - 1, y, z, true));
        Assert.True(alice.Session.Replica.TurfLightAt(x - 1, y, z, false).IsNone);

        // the screwdriver turns it off, then on again
        using (w.World.Enter())
            DevCommands.Give((mob_living_carbon_human)mob, "/obj/item/screwdriver");
        var tile = new ClickTarget(TargetKind.Turf, 0, x, y, z, 16, 16);
        Assert.True(alice.Session.Click(tile, ClickButton.Left, ClickModifiers.None));
        w.Run(20);
        Assert.True(alice.Session.Replica.TurfLightAt(x, y, z, false).IsNone);
        Assert.True(alice.Session.Click(tile, ClickButton.Left, ClickModifiers.None));
        w.Run(20);
        Assert.Equal(floor, alice.Session.Replica.TurfLightAt(x, y, z, false));

        var bob = w.Join("Bob");
        w.Run(5);
        Assert.Equal(floor, bob.Session.Replica.TurfLightAt(x, y, z, false));
        Assert.Equal(floor, bob.Session.Replica.TurfLightAt(x - 1, y, z, true));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}

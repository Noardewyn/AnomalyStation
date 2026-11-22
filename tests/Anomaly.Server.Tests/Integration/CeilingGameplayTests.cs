using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

public class CeilingGameplayTests
{
    [Fact]
    public void PlayerBuildsThroughTheWireAndBothClientsSeeTheSameBoundary()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 8, TimeoutSteps = 400 });
        var alice = w.Join("Alice");
        var bob = w.Join("Bob");
        var mob = w.MobOf(alice);
        int x, y, z;
        obj_item_stack_tile_iron tiles;
        string floor;
        using (w.World.Enter())
        {
            x = (int)mob.x; y = (int)mob.y; z = (int)mob.z;
            floor = DmTypes.PathOf(locate(x, y, z).GetType());
            for (int cy = 1; cy <= MapView.Height(w.World); cy++)
            for (int cx = 1; cx <= MapView.Width(w.World); cx++)
                if (locate(cx, cy, z) is turf_open open) open.air?.remove_ratio(1);
            CeilingSystem.Of(w.World).Set(x, y, z, CeilingState.Of(CeilingLattice.Lattice));
            tiles = Dm.@new<obj_item_stack_tile_iron>(locate(x, y, z), 6);
            Waiting.NoSleep(mob.put_in_hands(tiles));
        }
        w.Run(5);
        var target = new ClickTarget(TargetKind.Ceiling, 0, x, y, z);
        alice.Session.Click(target, ClickButton.Left, ClickModifiers.Shift);
        w.Run(10);
        Assert.Equal(CeilingLattice.Lattice, alice.Session.Replica.CeilingAt(x, y, z).Lattice);
        Assert.Equal(6, tiles.amount);
        // D295: tg's iron tile on a lattice makes plating at once (build_with_floor_tiles)
        alice.Session.Click(target, ClickButton.Left, ClickModifiers.None);
        w.Run(10);
        Assert.Equal(CeilingState.Plating, alice.Session.Replica.CeilingAt(x, y, z).Turf);
        Assert.Equal(5, tiles.amount);
        Assert.False(alice.Session.Replica.CeilingLookAt(x, y, z).IsNone);
        Assert.Equal(alice.Session.Replica.CeilingAt(x, y, z), bob.Session.Replica.CeilingAt(x, y, z));
        Assert.Equal(alice.Session.Replica.CeilingLookAt(x, y, z), bob.Session.Replica.CeilingLookAt(x, y, z));
        Assert.Equal(floor, bob.Session.Replica.TurfTypeAt(x, y, z));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_drag_from_or_onto_a_ceiling_goes_through_the_wire()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 8, TimeoutSteps = 400 });
        var alice = w.Join("Alice");
        var mob = w.MobOf(alice);
        int x, y, z;
        using (w.World.Enter())
            (x, y, z) = ((int)mob.x, (int)mob.y, (int)mob.z);
        w.Run(5);
        var ceiling = new ClickTarget(TargetKind.Ceiling, 0, x, y, z);
        var floor = new ClickTarget(TargetKind.Turf, 0, x, y, z);
        Assert.True(alice.Session.Drop(new DragRelease(ceiling, floor, ClickButton.Left, ClickModifiers.None, 5)));
        Assert.True(alice.Session.Drop(new DragRelease(floor, ceiling, ClickButton.Left, ClickModifiers.None, 5)));
        w.Run(5);
        Session s = w.Host.SessionOf(1);
        Assert.Equal((2, 0), (s.DropsApplied, s.DropsRejected));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}

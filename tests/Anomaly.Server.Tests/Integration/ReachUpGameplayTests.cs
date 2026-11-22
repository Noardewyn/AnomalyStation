using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using System.IO;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV425 through the real server with a headless client, on the fixture tests/fixtures/maps/t2/multiz (two linked levels, a 3 x 3 room each,
/// open space at (3, 3, 2)): a wrench lies on the upper floor at the hole's edge; standing under the hole Alice reaches up and picks it up
/// with a click, standing under the solid ceiling beside it she cannot (tg's other level is never in reach); from the upper floor she does
/// not reach down into the hole.
/// </summary>
public class ReachUpGameplayTests
{
    private static string Fixture()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tests", "fixtures", "maps", "t2", "multiz");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("tests/fixtures/maps/t2/multiz not found");
    }

    [Theory]
    [InlineData(3, 3, true)]  // under the hole: the wrench at its edge is in reach
    [InlineData(2, 2, false)] // under the upper floor: it is not
    public void AnItemOnTheUpperFloorAtTheHolesEdgeIsPickedUpFromUnderTheHole(int x, int y, bool reached)
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 21, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var mob = (mob_living_carbon_human)w.MobOf(alice);
        obj_item_wrench wrench;
        using (w.World.Enter())
        {
            Waiting.NoSleep(mob.forceMove(locate(x, y, 1)));
            wrench = Dm.@new<obj_item_wrench>((turf)locate(2, 3, 2));
        }
        w.Run(40);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, wrench.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        using (w.World.Enter())
        {
            Assert.Equal(reached, ReferenceEquals(mob.get_active_held_item(), wrench));
            if (!reached)
                Assert.Same(locate(2, 3, 2), wrench.loc);
        }
    }

    // The owner (2026-11-14): only up. Standing on the upper floor at the hole's edge Alice does not reach down to a wrench lying under the hole.
    [Fact]
    public void AnItemUnderTheHoleIsNotPickedUpFromTheUpperFloor()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 22, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("multiz", Fixture()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var mob = (mob_living_carbon_human)w.MobOf(alice);
        obj_item_wrench wrench;
        using (w.World.Enter())
        {
            Waiting.NoSleep(mob.forceMove(locate(2, 3, 2)));
            wrench = Dm.@new<obj_item_wrench>((turf)locate(3, 3, 1));
        }
        w.Run(40);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, wrench.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        using (w.World.Enter())
        {
            Assert.Null(mob.get_active_held_item());
            Assert.Same(locate(3, 3, 1), wrench.loc);
        }
    }
}

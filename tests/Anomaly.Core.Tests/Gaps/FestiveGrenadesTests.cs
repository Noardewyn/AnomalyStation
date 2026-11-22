using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// tg's festive.dm and antigravity.dm (the owner, 2026-11-18): a lit lighter lights a sparkler (it burns, lights the room and leaves a rod) and a
/// large firecracker (its fuse, then a light blast), wirecutters shorten the firecracker's fuse, and the antigravity grenade turns gravity off
/// for its view for 30 seconds.
/// </summary>
public class FestiveGrenadesTests
{
    private const int Second = 20;

    private static (AudioRoom R, mob_living_carbon_human Bob, obj_item_lighter Lighter) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        obj_item_lighter lighter = r.In(() =>
        {
            var l = Dm.@new<obj_item_lighter>(bob.loc);
            l.set_lit(TRUE);
            return l;
        });
        return (r, bob, lighter);
    }

    [Fact]
    public void ALitSparklerBurnsWithLightAndLeavesARod()
    {
        (AudioRoom r, mob_living_carbon_human bob, obj_item_lighter lighter) = Room(1701);
        var sparkler = r.In(() => Dm.@new<obj_item_sparkler>(locate(5, 4, 1)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(sparkler.item_interaction(bob, lighter, Dm.list()))));
        r.In(() =>
        {
            Assert.Equal("sparkler_on", sparkler.icon_state);
            Assert.Equal("lit sparkler", sparkler.name);
            Assert.Equal(BURN, sparkler.damtype);
            Assert.Equal(1000f, Dm.num(sparkler.get_temperature()));
            Assert.Equal(2f, Dm.num(sparkler.light_range));
        });
        r.Step(121 * Second);
        Assert.True(r.In(() => QDELETED(sparkler)));
        Assert.NotEmpty(r.In(() => Dm.each<obj_item_stack_rods>(locate(5, 4, 1)).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFirecrackerIsLitByAFlameOnlyAndBlowsAfterItsFuse()
    {
        (AudioRoom r, mob_living_carbon_human bob, obj_item_lighter lighter) = Room(1702);
        var cracker = r.In(() => Dm.@new<obj_item_grenade_firecracker>(locate(8, 4, 1)));
        r.In(() => Waiting.NoSleep(cracker.attack_self(bob)));
        Assert.False(r.In(() => Dm.truthy(cracker.active))); // no pin to pull: it needs a flame
        var cutters = r.In(() => Dm.@new<obj_item_wirecutters>(bob.loc));
        r.In(() => Waiting.NoSleep(cracker.wirecutter_act(bob, cutters)));
        Assert.Equal(20f, r.In(() => cracker.det_time));
        Assert.Equal("firecracker_20", r.In(() => cracker.icon_state));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(cracker.item_interaction(bob, lighter, Dm.list()))));
        Assert.True(r.In(() => Dm.truthy(cracker.active)));
        Assert.Equal("firecracker_active", r.In(() => cracker.icon_state));
        r.Step(1 * Second);
        Assert.False(r.In(() => QDELETED(cracker)));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(cracker)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAntigravityGrenadeTurnsGravityOffAroundItForThirtySeconds()
    {
        (AudioRoom r, mob_living_carbon_human bob, _) = Room(1703);
        Assert.True(r.In(() => Dm.truthy(bob.has_gravity())));
        var grenade = r.In(() => Dm.@new<obj_item_grenade_antigravity>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(grenade.detonate()));
        Assert.True(r.In(() => QDELETED(grenade)));
        Assert.False(r.In(() => Dm.truthy(bob.has_gravity())));
        r.Step(31 * Second);
        Assert.True(r.In(() => Dm.truthy(bob.has_gravity())));
        r.AssertNoRuntimes();
    }
}

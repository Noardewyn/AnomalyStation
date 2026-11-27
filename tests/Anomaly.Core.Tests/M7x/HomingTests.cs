using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// DV96 (the owner, 2026-11-20: the Abielle smart SMG sprayed everywhere): a homing shot homes in 3D. The smart SMG's 40 degree cone
/// throws its bullets up, down and aside, and its homing brings them back onto the man it was aimed at: the heading as tg turns it, the
/// pitch toward his middle, the line its hits are found on started again at each turn, and tg's homing offsets at the body's scale.
/// </summary>
public class HomingTests
{
    private const int LineY = 4;

    [Fact]
    public void TheSmartSmgsBurstsHomeOntoTheManTheyWereAimedAt()
    {
        var r = new AudioRoom(16, 9, seed: 4242);
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        Listen(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        r.Step(Second);
        var gun = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_smartgun>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(gun)));
        r.Step();
        // level along the line from Bob's eye: the cone alone would throw most of each burst past Rob, 7 tiles away
        string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(3 * 32, LineY * 32, 20.8, 90, 0);
        var pitches = new List<float>();
        r.World.Rng.Reseed(4242, 1); // the cone and homing rolls from here, whatever the boot drew
        for (int burst = 0; burst < 3; burst++)
        {
            r.In(() => { bob.next_click = -1; bob.next_move = -1; gun.fire_cd = 0; });
            Assert.True(Dm.truthy(r.In(() => Waiting.NoSleep(gun.try_fire_gun(rob, bob, aim)))));
            for (int t = 0; t < 2 * Second; t++)
            {
                r.Step();
                pitches.AddRange(r.In(() => Dm.each<obj_projectile>().Where(p => Dm.truthy(p.fired)).Select(p => p.dm_slope).ToList()));
            }
        }
        Assert.Contains(pitches, s => Math.Abs(s) > 0.2f); // the cone did throw shots steeply up or down
        // 12 bullets of 10 brute: they all hit the man they home on, as tg's do, wherever beside his drawn body their homing offsets put the line
        Assert.True(r.In(() => Dm.num(rob.get_brute_loss())) >= 110, $"Rob took {r.In(() => Dm.num(rob.get_brute_loss()))} brute");
        r.AssertNoRuntimes();
    }

    // DV96: a shot at the floor locks onto the man near it, never onto the shooter standing as near (tg's locate takes the first mob in range)
    [Fact]
    public void TheSmartSmgsLockOnAFloorShotSkipsTheShooter()
    {
        var r = new AudioRoom(16, 9, seed: 4242);
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        Listen(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(7, LineY); // two tiles past the floor shot as Bob is two before it: tg's lock takes Bob
        r.Step(Second);
        var gun = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_smartgun>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(gun)));
        r.Step();
        turf floor = r.In(() => Builtins.locate(5, LineY, Dm.num(bob.z)));
        r.In(() => { bob.next_click = -1; bob.next_move = -1; gun.fire_cd = 0; });
        Assert.True(Dm.truthy(r.In(() => Waiting.NoSleep(gun.try_fire_gun(floor, bob, "icon-x=16;icon-y=16;left=1;button=left")))));
        var targets = new List<atom>();
        var marked = new List<atom>();
        for (int t = 0; t < Second; t++)
        {
            r.Step();
            targets.AddRange(r.In(() => Dm.each<obj_projectile>().Where(p => Dm.truthy(p.fired)).Select(p => p.homing_target).ToList()));
            marked.AddRange(r.In(() => Dm.each<obj_effect_temp_visual_smartgun_target>().Select(m => m.loc).ToList()));
        }
        Assert.NotEmpty(targets);
        Assert.All(targets, t => Assert.Same(rob, t));
        // tg's lock reticle is shown on the man the bullets home on: made inside him, in his vis_contents (the clients draw it on him)
        Assert.NotEmpty(marked);
        Assert.All(marked, at => Assert.Same(rob, at));
        Assert.True(r.In(() => Dm.each<obj_effect_temp_visual_smartgun_target>(rob).All(m => Dm.@in(m, rob.vis_contents))));
        r.AssertNoRuntimes();
    }
}

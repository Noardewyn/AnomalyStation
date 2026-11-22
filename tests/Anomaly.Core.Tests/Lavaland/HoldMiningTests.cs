using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Lavaland;

/// <summary>DV348: a pick digs only while its button is held on the rock; the rock cracks on the digger's side as the pick strikes it.</summary>
public class HoldMiningTests
{
    private const string Params = "icon-x=16;icon-y=16;left=1;button=left";

    private static (AudioRoom R, mob_living_carbon_human Bob, turf_closed_mineral Rock) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        obj_item_pickaxe pick = r.In(() => Dm.@new<obj_item_pickaxe>(bob.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(pick)))));
        var rock = r.In(() => (turf_closed_mineral)Waiting.NoSleep(locate(4, 4, 1).ChangeTurf(typeof(turf_closed_mineral_iron))));
        r.Step(2);
        return (r, bob, rock);
    }

    private static void Press(AudioRoom r, mob who, atom target)
    {
        r.In(() => ClientSeam.MouseDown(who.client, target, Params));
        Click(r, who, target);
    }

    private static bool IsRock(AudioRoom r) => r.In(() => locate(4, 4, 1) is turf_closed_mineral);

    private static int Cracks(AudioRoom r, turf_closed_mineral rock) => r.In(() => (int)Dm.num(LAZYLEN(rock.mining_cracks)));

    [Fact]
    public void HeldTheRockCracksStrikeByStrikeAndBreaksInTgsTime()
    {
        var (r, bob, rock) = Room(9701);
        Press(r, bob, rock);
        r.Step(2 * Second);
        Assert.InRange(Cracks(r, rock), 2, 3); // a strike every 0.8 s
        byte face = r.In(() => (byte)Dm.num(((mutable_appearance)rock.mining_cracks[1]).dir));
        Assert.Equal(WEST, face); // Bob stands west of it
        Assert.True(IsRock(r));
        r.Step(Second + 10);
        Assert.Equal(4, Cracks(r, rock));
        r.In(() =>
        {
            // the picture fills x 4-25 and y 6-25 of its 32: all of it stays on the face
            foreach (mutable_appearance crack in Dm.each<mutable_appearance>(rock.mining_cracks))
            {
                Assert.InRange(Dm.num(crack.pixel_w), -4, 6);
                Assert.InRange(Dm.num(crack.pixel_z), -6, 6);
            }
        });
        r.Step(Second);
        Assert.False(IsRock(r));
        Assert.True(r.In(() => Dm.locate<obj_item_stack_ore_iron>(locate(4, 4, 1)) != null));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WalkingIntoTheRockWithAPickDoesNotDig() // DV354
    {
        var (r, bob, rock) = Room(9705);
        r.In(() =>
        {
            rock.Bumped(bob);
            Assert.Equal(0, (int)Dm.num(LAZYLEN(bob.do_afters))); // no dig started, not even one the hold rule cancels later
        });
        r.Step(6 * Second);
        Assert.Equal(0, Cracks(r, rock));
        Assert.True(IsRock(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ReleasingStopsTheDigAndTheRockHealsAndCanBeDugAgain()
    {
        var (r, bob, rock) = Room(9702);
        Press(r, bob, rock);
        r.Step(2 * Second);
        r.In(() => ClientSeam.MouseUp(bob.client, rock, Params));
        r.Step(4);
        Assert.Equal(0, Cracks(r, rock));
        r.Step(4 * Second);
        Assert.True(IsRock(r));

        Press(r, bob, rock); // the cooldown ended with the dig
        r.Step(5 * Second);
        Assert.False(IsRock(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void LookingAwayStopsTheDig()
    {
        var (r, bob, rock) = Room(9703);
        Press(r, bob, rock);
        r.Step(Second);
        r.In(() => ClientSeam.MouseDrag(bob.client, locate(3, 5, 1), Params));
        r.Step(5 * Second);
        Assert.True(IsRock(r));
        Assert.Equal(0, Cracks(r, rock));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WalkingAroundTheRockKeepsTheDigGoing()
    {
        var (r, bob, rock) = Room(9706);
        Press(r, bob, rock);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(3, 5, 1)))); // diagonal
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 5, 1)))); // the rock's north side
        r.Step(3 * Second);
        Assert.False(IsRock(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WalkingAwayFromTheRockStopsTheDig()
    {
        var (r, bob, rock) = Room(9707);
        Press(r, bob, rock);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(2, 4, 1))));
        r.Step(5 * Second);
        Assert.True(IsRock(r));
        Assert.Equal(0, Cracks(r, rock));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnActionWithoutTheFlagStillStopsOnAStep()
    {
        var (r, bob, rock) = Room(9708);
        bool? done = null;
        r.In(() => Waiting.Detach(Run()));
        async DmTask Run() => done = Dm.truthy(await do_after(bob, 2 * SECONDS, target: rock));
        r.Step(5);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(3, 5, 1))));
        r.Step(3 * Second);
        Assert.False(done);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AClickWithNoHeldButtonDigsNothing()
    {
        var (r, bob, rock) = Room(9704);
        Click(r, bob, rock);
        r.Step(5 * Second);
        Assert.True(IsRock(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMobWithNoPlayerDigsWithoutAButton()
    {
        var (r, bob, rock) = Room(9705);
        var pick = r.In(() => (obj_item)bob.get_active_held_item());
        mob_living_carbon_human npc = r.In(() =>
        {
            var m = Dm.@new<mob_living_carbon_human>(locate(5, 4, 1));
            ADD_TRAIT(m, TRAIT_ADVANCEDTOOLUSER, "test");
            return m;
        });
        r.Step(Second);
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(pick));
            Waiting.NoSleep(npc.put_in_active_hand(pick));
            Waiting.Detach(rock.manual_mine(npc, pick));
        });
        r.Step(5 * Second);
        Assert.False(IsRock(r));
        r.AssertNoRuntimes();
    }
}

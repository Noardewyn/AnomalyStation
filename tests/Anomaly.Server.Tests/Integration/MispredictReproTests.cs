using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Server.Tests.Integration.DragGestureGameplayTests;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's play test of 2026-10-27: the own prediction was corrected when walk and run were toggled while moving, when climbing onto a
/// table and stepping off it, and when walking off a floor into weightless space and drifting onto another floor. Each case runs through the real server and the predicting client with and
/// without latency and expects no correction of a move the player made.
/// </summary>
public class MispredictReproTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static (RealHeadlessWorld W, RealHeadlessWorld.Player P, mob_living Me) Prepare(int latency, int x, int y)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 2000, MinLatencySteps = latency, MaxLatencySteps = latency }, strict: false);
        var p = w.Join("Bob", inputBufferTarget: latency > 0 ? 2 : 1);
        var me = w.MobOf(p);
        Do(w, () =>
        {
            ADD_TRAIT(me, TRAIT_GODMODE, "test");
            Waiting.NoSleep(me.forceMove(locate(x, y, 1)));
        });
        p.Yaw = 0;
        w.Run(Second);
        return (w, p, me);
    }

    private string Report(RealHeadlessWorld.Player p, PredictionCounters before)
    {
        var now = p.Counters;
        string text = $"corrections {now.Corrections - before.Corrections}, mismatches {now.Mismatches - before.Mismatches}, snaps {now.Snaps - before.Snaps}, carried {now.Carried - before.Carried}, max {now.MaxErrorTiles:0.###}";
        output.WriteLine(text);
        return text;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void TogglingWalkAndRunWhileMovingIsPredicted(int latency)
    {
        var (w, p, me) = Prepare(latency, 4, 12);
        using (w)
        {
            var before = p.Counters;
            p.Keys = MoveKeys.Right;
            w.Run(4);
            p.Session.ToggleMoveIntent();
            w.Run(10);
            p.Session.ToggleMoveIntent();
            w.Run(4);
            p.Keys = MoveKeys.None;
            w.Run(Second);
            Report(p, before);
            Assert.Equal(before.Corrections, p.Counters.Corrections);
            Assert.Equal(before.Mismatches, p.Counters.Mismatches);
        }
    }

    // The owner's play test of 2026-11-07 (DV360): walking into airtight plastic flaps was corrected (the client walked in, tg's
    // CanAllowThrough refused the standing mob). Standing, the flaps stop the capsule at their tile as the server does; lying, it crawls
    // under them; the cold room's flaps (require_resting FALSE) let a walker through.
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void WalkingIntoAirtightFlapsAndCrawlingUnderThemIsPredicted(int latency)
    {
        var (w, p, me) = Prepare(latency, 6, 12);
        using (w)
        {
            In(w, () => Dm.@new<obj_structure_plasticflaps>(locate(8, 12, 1)));
            In(w, () => Dm.@new<obj_structure_plasticflaps_kitchen>(locate(8, 10, 1)));
            w.Run(4);
            var before = p.Counters;
            p.Keys = MoveKeys.Right;
            w.Run(Second);
            p.Keys = MoveKeys.None;
            w.Run(Second);
            Assert.Equal(7, w.ServerState(p).TileX);
            output.WriteLine("walked into them: " + Report(p, before));
            Assert.Equal(before.Corrections, p.Counters.Corrections);
            Assert.Equal(before.Mismatches, p.Counters.Mismatches);

            Do(w, () => Waiting.NoSleep(((mob_living)me).set_resting(1, 1, 1)));
            w.Run(Second);
            var lying = p.Counters;
            p.Keys = MoveKeys.Right;
            for (int i = 0; i < 10 * Second && w.ServerState(p).TileX < 9; i++)
                w.Run(1);
            p.Keys = MoveKeys.None;
            w.Run(Second);
            Assert.True(w.ServerState(p).TileX >= 9, "crawled under the flaps");
            output.WriteLine("crawled under them: " + Report(p, lying));
            Assert.Equal(lying.Corrections, p.Counters.Corrections);
            Assert.Equal(lying.Mismatches, p.Counters.Mismatches);

            Do(w, () =>
            {
                Waiting.NoSleep(((mob_living)me).set_resting(0, 1, 1));
                Waiting.NoSleep(me.forceMove(locate(6, 10, 1)));
            });
            w.Run(Second);
            var cold = p.Counters;
            p.Keys = MoveKeys.Right;
            for (int i = 0; i < 2 * Second && w.ServerState(p).TileX < 9; i++)
                w.Run(1);
            p.Keys = MoveKeys.None;
            w.Run(Second);
            Assert.True(w.ServerState(p).TileX >= 9, "walked through the cold room's flaps");
            output.WriteLine("cold room flaps: " + Report(p, cold));
            Assert.Equal(cold.Corrections, p.Counters.Corrections);
            Assert.Equal(cold.Mismatches, p.Counters.Mismatches);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ClimbingOntoATableAndSteppingOffIsPredicted(int latency)
    {
        var (w, p, me) = Prepare(latency, 7, 12);
        using (w)
        {
            var table = In(w, () => Dm.@new<obj_structure_table>(locate(8, 12, 1)));
            w.Run(4);
            var before = p.Counters;
            int shakes = p.Session.CorrectionShakes;
            Assert.True(p.Session.Drop(new DragRelease(Entity(me.dm_id), Entity(table.dm_id), ClickButton.Left, ClickModifiers.None, 255)));
            w.Run(Second * 5);
            Assert.Equal(8, w.ServerState(p).TileX);
            output.WriteLine("climbed: " + Report(p, before));
            var onTable = p.Counters;
            p.Keys = MoveKeys.Right;
            for (int i = 0; i < Second && w.ServerState(p).TileX != 9; i++)
                w.Run(1);
            p.Keys = MoveKeys.None;
            w.Run(Second);
            Assert.InRange(w.ServerState(p).TileX, 9, 10);
            output.WriteLine("stepped off: " + Report(p, onTable));
            // back toward the table from the floor: it is a wall again
            var floor = p.Counters;
            p.Keys = MoveKeys.Left;
            w.Run(Second);
            p.Keys = MoveKeys.None;
            w.Run(Second);
            output.WriteLine("walked into it: " + Report(p, floor));
            Assert.Equal(before.Corrections, p.Counters.Corrections);
            Assert.Equal(before.Mismatches, p.Counters.Mismatches);
            Assert.Equal(shakes, p.Session.CorrectionShakes); // climbing up shakes nothing
        }
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(2, false, false)]
    [InlineData(0, true, false)]
    [InlineData(2, true, false)]
    [InlineData(0, false, true)]
    [InlineData(2, false, true)]
    public void WalkingOffAFloorIntoSpaceAndOntoAnotherIsPredicted(int latency, bool wallAlong, bool jetpack)
    {
        var (w, p, me) = Prepare(latency, 6, 12);
        using (w)
        {
            Do(w, () =>
            {
                // airless floors either side (the owner's platforms in space): no wind out of the room pushes the body
                for (int x = 2; x <= 22; x++)
                    for (int y = 2; y <= 22; y++)
                        if (wallAlong && y == 13 && x >= 8 && x <= 13)
                            Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_closed_wall))); // something to push off along the way
                        else if (x >= 9 && x <= 12)
                            Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
                        else if (locate(x, y, 1) is turf_open open && open is not turf_open_space)
                            open.air = SSair.parse_gas_string(AIRLESS_ATMOS) as datum_gas_mixture;
            });
            Do(w, () => ADD_TRAIT(me, TRAIT_RESISTCOLD, "test")); // a space suit's: no cold slowdown while the body cools in the vacuum
            if (jetpack)
                Do(w, () =>
                {
                    if (me is mob_living_carbon_human human && human.back != null)
                        Waiting.NoSleep(human.dropItemToGround(human.back));
                    var pack = Dm.@new<obj_item_tank_jetpack_carbondioxide>(me.loc);
                    Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_or_del(pack, ITEM_SLOT_BACK))));
                    Assert.True(Dm.truthy(Waiting.NoSleep(pack.turn_on(me))));
                });
            Assert.True(In(w, () => Dm.truthy(me.has_gravity())));
            Assert.False(In(w, () => Dm.truthy(((turf)locate(10, 12, 1)).has_gravity())));
            for (int leg = 0; leg < 3; leg++)
            {
                var before = p.Counters;
                p.Keys = leg % 2 == 0 ? MoveKeys.Right : MoveKeys.Left;
                for (int i = 0; i < Second * 4 && (leg % 2 == 0 ? w.ServerState(p).TileX < 14 : w.ServerState(p).TileX > 6); i++)
                    w.Run(1);
                p.Keys = MoveKeys.None;
                w.Run(Second);
                output.WriteLine($"leg {leg}: at {w.ServerState(p).TileX}; " + Report(p, before));
                Assert.Equal(leg % 2 == 0 ? 13 : 7, leg % 2 == 0 ? Math.Min(w.ServerState(p).TileX, 13) : Math.Max(w.ServerState(p).TileX, 7)); // across the gap
                Assert.Equal(before.Corrections, p.Counters.Corrections);
                Assert.Equal(before.Mismatches, p.Counters.Mismatches);
                Assert.Equal(before.Snaps, p.Counters.Snaps);
            }
        }
    }
}

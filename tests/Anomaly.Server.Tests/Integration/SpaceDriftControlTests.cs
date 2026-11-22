using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

public class SpaceDriftControlTests(ITestOutputHelper output)
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter()) return f();
    }

    private static (RealHeadlessWorld W, RealHeadlessWorld.Player P, mob_living Me) Prepare(int latency, bool jetpack, bool platform = false, bool adverse = false)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions
        {
            Seed = 194, TimeoutSteps = 800, MinLatencySteps = latency, MaxLatencySteps = latency + (adverse ? 2 : 0),
            LossRate = adverse ? 0.05 : 0,
        }, strict: false);
        var p = w.Join("Bob", inputBufferTarget: latency > 0 ? 2 : 1);
        var me = w.MobOf(p);
        using (w.World.Enter())
        {
            ADD_TRAIT(me, TRAIT_GODMODE, "test");
            for (int x = 2; x <= 22; x++)
                for (int y = 2; y <= 22; y++)
                    if (platform && x >= 15 && x <= 18)
                    {
                        var floor = (turf_open)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_indestructible), flags: CHANGETURF_NO_AREA_CHANGE));
                        floor.force_no_gravity = TRUE;
                        floor.air = SSair.parse_gas_string(AIRLESS_ATMOS) as datum_gas_mixture;
                    }
                    else
                        Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
            Waiting.NoSleep(me.forceMove(locate(10, 10, 1)));
            if (jetpack)
            {
                if (me is mob_living_carbon_human human && human.back != null)
                    Waiting.NoSleep(human.dropItemToGround(human.back));
                var pack = Dm.@new<obj_item_tank_jetpack_carbondioxide>(me.loc);
                Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_or_del(pack, ITEM_SLOT_BACK))));
                Assert.True(Dm.truthy(Waiting.NoSleep(pack.turn_on(me))));
            }
        }
        w.Run(20);
        return (w, p, me);
    }

    private void Report(RealHeadlessWorld w, RealHeadlessWorld.Player p, PredictionCounters before)
    {
        var now = p.Counters;
        output.WriteLine($"corrections {now.Corrections - before.Corrections}, mismatches {now.Mismatches - before.Mismatches}, snaps {now.Snaps - before.Snaps}");
        Assert.Equal(before.Snaps, now.Snaps);
        Assert.Equal(FeedPhase.Playing, p.Client.Phase);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(2, true)]
    // tg (the owner, 2026-11-18, retiring DV178): a floor without gravity is no handhold, so a lit unstabilized jetpack
    // (TRAIT_NOGRAV_ALWAYS_DRIFT) drifts over a weightless platform, and the client predicts it without a snap.
    public void AnUnstabilizedJetpackDriftsOverAWeightlessPlatform(int latency, bool adverse)
    {
        var (w, p, me) = Prepare(latency, true, platform: true, adverse: adverse);
        using (w)
        {
            p.Keys = MoveKeys.Right;
            w.Run(3);
            p.Keys = MoveKeys.None;
            var before = p.Counters;
            w.Run(100);
            Assert.True(w.ServerState(p).TileX > 18);
            Report(w, p, before);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(2, true)]
    public void AOneTickJetpackTapRedirectsInertiaWithoutCrossingATile(int latency, bool adverse)
    {
        var (w, p, me) = Prepare(latency, true, adverse: adverse);
        using (w)
        {
            In(w, () => Waiting.NoSleep(me.newtonian_move(90)));
            w.Run(10);
            var before = p.Counters;
            p.Keys = MoveKeys.Forward;
            w.Run(1);
            p.Keys = MoveKeys.None;
            w.Run(2 * latency + 8);
            Assert.InRange(In(w, () => Dm.num(me.drift_handler.drifting_loop.angle)), -1, 1);
            var start = w.ServerState(p);
            var settled = p.Counters;
            w.Run(20);
            Assert.Equal(start.X, w.ServerState(p).X);
            Assert.True(w.ServerState(p).Y > start.Y);
            Assert.Equal(settled.Mismatches, p.Counters.Mismatches);
            Report(w, p, before);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(2, true)]
    public void RefusedSteeringDuringPassiveDriftDoesNotJerkOrMispredict(int latency, bool adverse)
    {
        var (w, p, me) = Prepare(latency, false, adverse: adverse);
        using (w)
        {
            In(w, () => Waiting.NoSleep(me.newtonian_move(90)));
            w.Run(2 * latency + 12);
            var before = p.Counters;
            var start = w.ServerState(p);
            float lastDrawnX = p.Session.Prediction.RenderPosition().X;
            for (int i = 0; i < 40; i++)
            {
                p.Keys = i % 2 == 0 ? MoveKeys.Forward : MoveKeys.None;
                w.Run(1);
                var drawn = p.Session.Prediction.RenderPosition();
                Assert.InRange(drawn.X - lastDrawnX, 0.05f, 0.16f);
                Assert.Equal(MotionMath.ToTiles(start.Y), drawn.Y);
                lastDrawnX = drawn.X;
            }
            Assert.Equal(start.Y, w.ServerState(p).Y);
            Assert.Equal(before.Mismatches, p.Counters.Mismatches);
            Assert.Equal(before.Corrections, p.Counters.Corrections);
            Report(w, p, before);
        }
    }

    // The owner's play test (2026-11-18): a drift halted beside a wall stopped a whole tile (2 m) short of it, where the capsule crossed
    // into the tile, and the client mispredicted the stop. The hand's reach (DV534) halts it a metre from the wall; the client predicts it.
    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(2, true)]
    public void ADriftHaltsHalfATileFromTheWallWithoutAMispredict(int latency, bool adverse)
    {
        var (w, p, me) = Prepare(latency, false, adverse: adverse);
        using (w)
        {
            using (w.World.Enter())
                for (int y = 2; y <= 22; y++)
                    Waiting.NoSleep(locate(16, y, 1).ChangeTurf(typeof(turf_closed_wall), flags: CHANGETURF_NO_AREA_CHANGE));
            w.Run(2 * latency + 4);
            In(w, () => Waiting.NoSleep(me.newtonian_move(90)));
            w.Run(2 * latency + 12);
            var before = p.Counters;
            w.Run(100);
            var stopped = w.ServerState(p);
            // DV534: it halts on the tick the wall comes within the hand's reach, a metre from the wall (the tile's centre), at most a tick's drift on
            Assert.Equal(15, stopped.TileX);
            Assert.InRange(stopped.X, 15 * MotionMath.SubunitsPerTile, 15 * MotionMath.SubunitsPerTile + 40);
            Assert.Null(In(w, () => me.drift_handler));
            Assert.Equal((stopped.X, stopped.Y), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y));
            if (!adverse)
            {
                // a drift is the server's move (CanMove false): a wrong halt shows as a carried correction, not a mismatch
                Assert.Equal(before.Mismatches, p.Counters.Mismatches);
                Assert.Equal(before.Corrections, p.Counters.Corrections);
                Assert.Equal(before.Carried, p.Counters.Carried);
            }
            Report(w, p, before);
        }
    }

    // The owner (2026-11-19, DV534): without gravity a mob holds on to a wall within a metre: it moves freely along it there, and a step
    // that takes it further away lets go and drifts on, as tg's push off does; the client predicts both.
    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(2, true)]
    public void AWallHoldsWithinAMetreAndAStepFurtherPushesOff(int latency, bool adverse)
    {
        var (w, p, me) = Prepare(latency, false, adverse: adverse);
        using (w)
        {
            using (w.World.Enter())
            {
                for (int y = 2; y <= 22; y++)
                    Waiting.NoSleep(locate(16, y, 1).ChangeTurf(typeof(turf_closed_wall), flags: CHANGETURF_NO_AREA_CHANGE));
                Waiting.NoSleep(me.forceMove(locate(15, 6, 1)));
            }
            w.Run(2 * latency + 10);
            var before = p.Counters;
            var start = w.ServerState(p);
            Assert.Equal(15 * MotionMath.SubunitsPerTile, start.X);

            p.Keys = MoveKeys.Forward; // along the wall, within reach
            w.Run(30);
            p.Keys = MoveKeys.None;
            w.Run(2 * latency + 6);
            var along = w.ServerState(p);
            Assert.True(along.TileY >= 9, $"moved along the wall to y {along.TileY}");
            Assert.Equal(start.X, along.X);
            Assert.Null(In(w, () => me.drift_handler));

            p.Keys = MoveKeys.Left; // away from the wall: out of reach, a push off
            w.Run(6);
            p.Keys = MoveKeys.None;
            w.Run(2 * latency + 4);
            Assert.NotNull(In(w, () => me.drift_handler));
            var off = w.ServerState(p);
            Assert.True(off.X < start.X - MotionMath.HandReach / 2);
            w.Run(40);
            var drifted = w.ServerState(p);
            Assert.True(drifted.X < off.X - MotionMath.SubunitsPerTile, $"drifted on west from {off.X} to {drifted.X}");
            Assert.Equal(along.Y, drifted.Y);
            if (!adverse)
            {
                Assert.Equal(before.Mismatches, p.Counters.Mismatches);
                Assert.Equal(before.Carried, p.Counters.Carried); // the push off's drift is the server's move
            }
            Report(w, p, before);
        }
    }
}

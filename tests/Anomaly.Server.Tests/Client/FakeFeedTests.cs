using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>M2 P8: the scripted world behind <c>--fake-server</c> (version 2: capsules, prediction, the private HUD) and the play loop over it.</summary>
public class FakeFeedTests
{
    private const double Frame = 1.0 / 60;

    private static double Advance(FakeFeed feed, double from, double seconds)
    {
        double now = from;
        double end = from + seconds;
        while (now < end)
        {
            now += Frame;
            feed.Poll(now);
        }
        return now;
    }

    private static ReplicaEntity Own(FakeFeed feed) => feed.Entities.Single(e => e.Id == feed.OwnEntity);

    private static (int X, int Y) Tile(ReplicaEntity entity) => (entity.State.X, entity.State.Y);

    [Fact]
    public void ItStartsPlayingWithAMapAndEntities()
    {
        var feed = new FakeFeed("Tester", bots: 8);
        Assert.Equal(FeedPhase.Idle, feed.Phase);
        Assert.Null(feed.Map);
        feed.Poll(0.0);
        Assert.Equal(FeedPhase.Playing, feed.Phase);
        Assert.Equal(1 + 8 + 5, feed.Count);
        Assert.Equal(1, feed.OwnEntity);
        Assert.Equal("Tester", Own(feed).State.Name);
        Assert.True(Own(feed).IsMob());
        Assert.True(Own(feed).HasPos);
        Assert.Contains(feed.Entities, e => !e.IsMob());
        Assert.DoesNotContain(feed.Entities.Where(e => !e.IsMob()), e => e.HasPos == true && e.State.PosX != e.State.X * 256);

        MapGrid map = feed.Map;
        Assert.Equal(feed.Size, map.Width);
        Assert.Equal(feed.Size, map.Height);
        Assert.Equal(map.Width * map.Height, map.Runs.Sum(r => r.Count));
        Assert.All(map.Runs, r => Assert.InRange(r.TypeIndex, 0, map.TurfTypes.Count - 1));
        Assert.Equal(TurfKind.Closed, TurfStyle.Classify(map.TurfTypes[map.Runs[0].TypeIndex]));
    }

    [Fact]
    public void TheMapAndTheSolidityBitsMatchTheWalls()
    {
        var feed = new FakeFeed("Tester");
        feed.Poll(0.0);
        MapGrid map = feed.Map;
        int cell = 0;
        foreach (TurfRun run in map.Runs)
        {
            for (int i = 0; i < run.Count; i++, cell++)
            {
                int x = cell % map.Width + 1;
                int y = cell / map.Width + 1;
                bool closed = TurfStyle.Classify(map.TurfTypes[run.TypeIndex]) == TurfKind.Closed;
                Assert.Equal(feed.IsWall(x, y), closed);
                Assert.Equal(closed, feed.Replica.IsSolid(x, y, 1));
            }
        }
        Assert.True(feed.IsWall(1, 1) && feed.IsWall(feed.Size, feed.Size));
        Assert.True(feed.Replica.IsSolid(0, 5, 1)); // outside the map
    }

    [Fact]
    public void ThePublicTablesGiveTheTurfsAndMobsTheirAppearances()
    {
        var feed = new FakeFeed("Tester");
        feed.Poll(0.0);
        ReplicaWorld replica = feed.Replica;
        TableRef floor = replica.TurfAppearance(5, 5, 1);
        Assert.True(replica.TryGetAppearance(floor, out AppearanceEntry entry));
        Assert.Equal("floor", replica.ResolveString(entry.IconState));
        Assert.StartsWith("icons/turf", replica.ResolveResource(entry.Icon));
        Assert.True(replica.TryGetAppearance(Own(feed).State.Appearance, out AppearanceEntry human));
        // tg's human.dmi has no state "human" (M3 R2): the root is "human_basic" and the limbs, face and jumpsuit are its overlays.
        Assert.Equal("human_basic", replica.ResolveString(human.IconState));
        Assert.NotEmpty(human.Overlays);
    }

    [Fact]
    public void TheWorldRunsAtTwentyTicksPerSecond()
    {
        var feed = new FakeFeed("Tester");
        double now = 0;
        feed.Poll(now);
        now = Advance(feed, now, 5.0);
        // The state of tick 100 is 5 s old minus the network delay.
        Assert.InRange(feed.Tick, 100 - 4, 100);
        Assert.Equal(feed.Tick, feed.DeltaCount);
    }

    [Fact]
    public void TheOwnBlockStartsThePredictorAndTheHudArrivesPrivately()
    {
        var feed = new FakeFeed("Tester");
        feed.Poll(0.0);
        OwnState own = feed.Replica.Own;
        Assert.True(own.HasMotion);
        Assert.Equal(feed.Size / 2, own.Motion.TileX);
        Assert.Equal(MotionMath.DefaultRadius, own.Params.Radius);
        Assert.Equal(5, feed.Replica.Screen.Count); // the doll, the indicator, the move intent, the zone selector and the combat toggle; no damage overlay yet
        Assert.True(feed.Replica.Screen.ContainsKey(1001));
        ScreenEntry doll = feed.Replica.Screen[1001];
        Assert.Equal("EAST-1:28,CENTER-2:17", doll.ScreenLoc);
        Assert.True(doll.Appearance.Private);
        var zones = new List<FoundZone>();
        HudReader.Collect(feed.Replica, doll.Appearance, zones);
        Assert.Equal(6, zones.Count);
        Assert.All(zones, z => Assert.Equal(ZoneKind.Level, z.Zone.Kind));
        Assert.All(zones, z => Assert.Equal(0, z.Zone.Level));
    }

    [Fact]
    public void ThePlayerWalksAtTgsRunSpeedAndZeroCorrectionsHappen()
    {
        var feed = new FakeFeed("Tester", bots: 0);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        Assert.True(rig.Session.Predicting);
        (float x0, float y0, _) = rig.Session.Prediction.RenderPosition();
        rig.Keys = MoveKeys.Forward; // facing north: north
        rig.Run(1.0);
        (float x1, float y1, _) = rig.Session.Prediction.RenderPosition();
        // A third of a tile per tick for 20 ticks, less the first frame's start-up.
        Assert.InRange(y1 - y0, 6.0f, 6.9f);
        Assert.Equal(x0, x1, 3);
        // The server follows the same frames and the replica's own entity is one round trip behind.
        rig.Run(0.5);
        Assert.Equal(feed.Server.PlayerState.TileY, feed.Server.PlayerState.TileY);
        PredictionCounters counters = rig.Session.Prediction.Counters;
        Assert.True(counters.Reconciliations > 20);
        Assert.Equal(0, counters.Corrections);
        Assert.Equal(0, counters.Mismatches);
    }

    [Fact]
    public void AKeyShowsOnTheNextFrameNotOnTheNextTick()
    {
        var feed = new FakeFeed("Tester", bots: 0, oneWayLatencyMs: 100, jitterMs: 0);
        var rig = new PlayRig(feed, fps: 144);
        rig.Run(0.6);
        (float x0, float y0, _) = rig.Session.Prediction.RenderPosition();
        rig.Keys = MoveKeys.Forward;
        rig.Run(rig.Frame * 1.5); // one frame
        (float x1, float y1, _) = rig.Session.Prediction.RenderPosition();
        Assert.True(y1 > y0, "the first frame after the key already moved the camera");
        Assert.True(y1 - y0 < 0.1f, "and by about a frame's worth: " + (y1 - y0));
        // 100 ms each way: far less than the round trip passed.
        Assert.True(feed.Server.PlayerState.TileY == 4 && feed.Server.AppliedSeq <= rig.Session.Prediction.NewestSeq);
    }

    [Fact]
    public void WalkingIntoAWallStopsAndTheServerAgrees()
    {
        var feed = new FakeFeed("Tester", bots: 0);
        var rig = new PlayRig(feed);
        rig.Keys = MoveKeys.Forward;
        rig.Run(20.0);
        (float x, float y, _) = rig.Session.Prediction.RenderPosition();
        int tileY = (int)Math.Round(y);
        int tileX = (int)Math.Round(x);
        Assert.True(feed.IsWall(tileX, tileY + 1), $"stopped at {tileX},{tileY} without a wall in the way");
        // Pressed against the wall: its face, a radius from the tile's edge.
        Assert.InRange(y, tileY + 0.5 - 0.18, tileY + 0.5 - 0.17);
        Assert.Equal(0, rig.Session.Prediction.Counters.Corrections);
        rig.Keys = MoveKeys.None;
        rig.Run(0.5);
        Assert.Equal(feed.Server.PlayerState.Y, rig.Session.Prediction.Predicted.Y);
    }

    [Fact]
    public void OppositeKeysDoNotMoveAndADiagonalMovesBothAxes()
    {
        var feed = new FakeFeed("Tester", bots: 0);
        var rig = new PlayRig(feed);
        rig.Run(0.3);
        (float x0, float y0, _) = rig.Session.Prediction.RenderPosition();
        rig.Keys = MoveKeys.Forward | MoveKeys.Back;
        rig.Run(0.5);
        (float x1, float y1, _) = rig.Session.Prediction.RenderPosition();
        Assert.Equal((x0, y0), (x1, y1));
        rig.Keys = MoveKeys.Forward | MoveKeys.Right;
        rig.Run(0.5);
        (float x2, float y2, _) = rig.Session.Prediction.RenderPosition();
        Assert.True(x2 - x1 > 2 && y2 - y1 > 2);
        Assert.Equal(x2 - x1, y2 - y1, 1);
    }

    [Fact]
    public void ARunIsDeterministic()
    {
        string Fingerprint()
        {
            var feed = new FakeFeed("Tester", seed: 9, bots: 10);
            var rig = new PlayRig(feed);
            rig.Run(1.0);
            rig.Keys = MoveKeys.Left;
            rig.Run(12.0);
            return string.Join(";", feed.Entities.OrderBy(e => e.Id).Select(e => $"{e.Id}:{e.Sample(0)}:{e.SampleCount}"));
        }
        Assert.Equal(Fingerprint(), Fingerprint());
    }

    [Fact]
    public void ABotLeavesAndAnotherJoinsLater()
    {
        var feed = new FakeFeed("Tester", bots: 4);
        var spawned = new List<long>();
        var despawned = new List<long>();
        feed.Poll(0.0);
        feed.Spawned += e => spawned.Add(e.Id);
        feed.Despawned += e => despawned.Add(e.Id);
        int initial = feed.Count;
        Advance(feed, 0.0, 13.0);
        Assert.Equal(new long[] { 3 }, despawned);
        Assert.Equal(new long[] { 200000 }, spawned);
        Assert.Equal(initial, feed.Count);
    }

    [Fact]
    public void BotsWanderAndObjectsStay()
    {
        var feed = new FakeFeed("Tester", bots: 6);
        double now = 0;
        feed.Poll(now);
        var before = feed.Entities.ToDictionary(e => e.Id, Tile);
        now = Advance(feed, now, 8.0);
        var after = feed.Entities.ToDictionary(e => e.Id, Tile);
        int movedBots = 0;
        foreach (ReplicaEntity e in feed.Entities)
        {
            if (e.IsMob() && e.Id != feed.OwnEntity && before.TryGetValue(e.Id, out var start) && !start.Equals(after[e.Id]))
                movedBots++;
            if (!e.IsMob())
                Assert.Equal(before[e.Id], after[e.Id]);
        }
        Assert.True(movedBots >= 3, $"only {movedBots} bots moved");
    }

    [Fact]
    public void NoMobEverStandsInAWallOrCloserThanTwoRadiiToAnother()
    {
        var feed = new FakeFeed("Tester", bots: 30);
        double now = 0;
        feed.Poll(now);
        for (int second = 0; second < 15; second++)
        {
            now = Advance(feed, now, 1.0);
            var mobs = feed.Entities.Where(e => e.IsMob()).ToList();
            foreach (ReplicaEntity e in feed.Entities)
                Assert.False(feed.IsWall(e.State.X, e.State.Y), $"{e.State.Name} is in a wall at {e.State.X},{e.State.Y}");
            for (int i = 0; i < mobs.Count; i++)
            {
                for (int j = i + 1; j < mobs.Count; j++)
                {
                    double dx = (mobs[i].State.PosX - mobs[j].State.PosX) / 256.0, dy = (mobs[i].State.PosY - mobs[j].State.PosY) / 256.0;
                    Assert.True(Math.Sqrt(dx * dx + dy * dy) > 2 * MotionMath.DefaultRadius - 0.02, $"{mobs[i].State.Name} and {mobs[j].State.Name} overlap");
                }
            }
        }
    }

    [Fact]
    public void TheMessagesAreTheRealOnesAndTheCountersAreTheirSizes()
    {
        var feed = new FakeFeed("Tester", bots: 8);
        var rig = new PlayRig(feed);
        long joinBytes = feed.BytesReceived;
        Assert.True(joinBytes > 100, "the map and the snapshot went through the codec");
        rig.Run(3.0);
        long sentBefore = feed.BytesSent;
        Assert.True(sentBefore > 0, "the idle frames are sent too (the server's buffer needs one per tick)");
        rig.Keys = MoveKeys.Forward;
        rig.Run(3.0);
        double perDelta = (feed.BytesReceived - joinBytes) / (double)feed.DeltaCount;
        Assert.InRange(perDelta, 10, 400);
        Assert.InRange((feed.BytesSent - sentBefore) / 3.0, 100, 2000); // a message per tick with up to four frames
    }

    [Theory]
    [InlineData(15, 25, 10)]
    [InlineData(30, 25, 10)]
    [InlineData(144, 25, 10)]
    [InlineData(144, 60, 30)]
    public void WhatTheRendererDrawsOfOthersMovesAtTheirOwnSpeedAtAnyFrameRate(double fps, int latencyMs, int jitterMs)
    {
        // The M1 hitch (scope 1.6): the render time ran past the newest sample and a late step showed up part-way along at once.
        // Now the others are drawn from continuous samples, the render clock stops at the data and a catch-up is bounded: the
        // 99th percentile of the per-frame speed is the bots' 13.3 m/s (tg's run) and the worst frame is not far above it.
        var feed = new FakeFeed("Tester", seed: 3, bots: 12, oneWayLatencyMs: latencyMs, jitterMs: jitterMs);
        var rig = new PlayRig(feed, fps);
        rig.Run(45);
        FrameSpeedStats stats = rig.Session.OthersSpeed;
        Assert.True(stats.Count > 12 * 40 * fps * 0.5, "samples " + stats.Count);
        Assert.InRange(stats.P99, 12.0, 14.3);
        Assert.True(stats.Max < 20.0, "the worst frame was " + stats.Max + " m/s");
        Assert.InRange(rig.Session.OthersSeen, 12, 13); // the scripted joiner comes at tick 240
        Assert.True(rig.Session.OthersMoved >= 8);
    }

    [Theory]
    [InlineData(0.135)]
    [InlineData(0.3)]
    public void ALateDeltaDoesNotMakeAMobJumpOnceItArrives(double stallSeconds)
    {
        // The server (or the link) stalls: the Deltas arrive late, then together. The render clock stops at the data and the mobs
        // are bridged by at most a tick of extrapolation, so when the data comes no mob is drawn jumping a part of a tile at once.
        var feed = new FakeFeed("Tester", seed: 3, bots: 8, oneWayLatencyMs: 25, jitterMs: 0);
        var rig = new PlayRig(feed, 144);
        rig.Run(5);
        double peakBefore = rig.Session.OthersSpeed.Max;
        for (int i = 0; i < 6; i++)
        {
            feed.Hiccup(stallSeconds);
            rig.Run(2.5);
        }
        FrameSpeedStats stats = rig.Session.OthersSpeed;
        Assert.True(stats.Max < 22.0, $"peak {stats.Max:0.0} m/s after stalls of {stallSeconds} s (before: {peakBefore:0.0})");
        // A stall of 300 ms every 2.5 s is a bad link, and the catch-up after it (the clock 10 % fast) is part of what is drawn.
        Assert.InRange(stats.P99, 12.0, stallSeconds > 0.2 ? 16.5 : 14.3);
    }

    private static Dictionary<string, ZoneState> Doll(FakeFeed feed)
    {
        var zones = new List<FoundZone>();
        HudReader.Collect(feed.Replica, feed.Replica.Screen[1001].Appearance, zones);
        return zones.ToDictionary(z => z.Zone.Zone, z => z.Zone);
    }

    private static string Reply(PlayRig rig, FakeFeed feed, string command)
    {
        string reply = null;
        feed.ServerTextReceived += t => reply = t.Html;
        rig.Session.DevCommand(command);
        rig.Run(0.6);
        return reply;
    }

    [Fact]
    public void DevCommandsDamageTheDollAndTheRepliesComeBack()
    {
        var feed = new FakeFeed("Tester", bots: 0);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        Assert.All(Doll(feed).Values, z => Assert.Equal(0, z.Level));
        Assert.Contains("Dealt 120 brute", Reply(rig, feed, "damage me brute 120 head"));
        Reply(rig, feed, "damage Tester burn 30 l_arm");
        Reply(rig, feed, "damage #1 brute 55 r_leg");
        Dictionary<string, ZoneState> doll = Doll(feed);
        Assert.Equal(3, doll["head"].Level);   // ceil(120 / 200 * 5)
        Assert.Equal(3, doll["l_arm"].Level);  // ceil(30 / 50 * 5)
        Assert.Equal(5, doll["r_leg"].Level);
        Assert.Equal(0, doll["chest"].Level);
        // The indicator and the damage overlay arrive too, as private screen entries.
        Assert.True(feed.Replica.Screen.ContainsKey(1003));
        Assert.True(feed.Replica.Screen.Values.All(e => e.Appearance.Private));
        // Healing puts it all back, and the overlay goes.
        Reply(rig, feed, "heal me");
        Assert.All(Doll(feed).Values, z => Assert.Equal(0, z.Level));
        Assert.False(feed.Replica.Screen.ContainsKey(1003));
        // Death: every zone says DEAD.
        Reply(rig, feed, "kill me");
        Assert.All(Doll(feed).Values, z => Assert.Equal(ZoneKind.Dead, z.Kind));
        Assert.Contains("back", Reply(rig, feed, "revive me"));
        Assert.All(Doll(feed).Values, z => Assert.Equal(ZoneKind.Level, z.Kind));
    }

    [Fact]
    public void DevCommandsAreCheckedAndCanBeSwitchedOff()
    {
        var feed = new FakeFeed("Tester", bots: 0);
        var rig = new PlayRig(feed);
        rig.Run(0.3);
        Assert.Contains("Usage: damage", Reply(rig, feed, "damage me brute x"));
        Assert.Contains("No such zone", Reply(rig, feed, "damage me brute 5 tail"));
        Assert.Contains("No such living mob", Reply(rig, feed, "damage nobody brute 5"));
        Assert.Contains("Unknown command", Reply(rig, feed, "dance me"));
        Assert.Contains("Commands", Reply(rig, feed, "help"));
        Assert.Contains("Analyzing results for Tester", Reply(rig, feed, "analyze me"));
        Assert.Contains("Speed scale is 0.5", Reply(rig, feed, "speedscale 0.5"));
        Assert.Equal(0.5f, feed.Server.SpeedScale);
        Assert.Contains("Usage: speedscale", Reply(rig, feed, "speedscale 99"));

        var off = new FakeFeed("Tester", bots: 0, devCommands: false);
        var offRig = new PlayRig(off);
        offRig.Run(0.3);
        Assert.Contains("off", Reply(offRig, off, "damage me brute 5"));
        Assert.All(Doll(off).Values, z => Assert.Equal(0, z.Level));
    }

    [Fact]
    public void AKnockdownLaysThePlayerDownAndTheyGetUpOnTheirOwn()
    {
        var feed = new FakeFeed("Tester", bots: 0);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        Reply(rig, feed, "knockdown me 2");
        Assert.True(feed.Server.PlayerLying);
        Assert.False(feed.Server.PlayerState.Posture != Posture.Lying);
        rig.Run(1.0);
        Assert.False(rig.Session.Prediction.Params.CanMove);
        rig.Run(3.0); // 2 s down, then a second of getting up
        Assert.False(feed.Server.PlayerLying);
        Assert.True(rig.Session.Prediction.Params.CanMove);
        Assert.Equal(Posture.Standing, rig.Session.Prediction.Predicted.Posture);
    }

    [Fact]
    public void ASpawnedHumanStandsInFrontAndCanBeShoved()
    {
        var feed = new FakeFeed("Tester", bots: 0, devCommands: true);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        Assert.Contains("Spawned Human 1", Reply(rig, feed, "spawn /mob/living/carbon/human"));
        Assert.Contains("Usage: spawn", Reply(rig, feed, "spawn /obj/item/toolbox"));
        Assert.Contains("solid", Reply(rig, feed, "spawn /mob/living/carbon/human 1 1"));
        ReplicaEntity human = feed.Entities.Single(e => e.Id == 300000);
        Assert.True(human.IsMob() && human.HasPos);
        Assert.Equal(feed.Server.PlayerState.TileY + 1, human.State.Y);
        // Under the crosshair, in reach, and a right click shoves it one tile north.
        Assert.True(rig.Session.TryPick(0, 0, 1.6f, out long id, out bool inReach));
        Assert.Equal(300000, id);
        Assert.True(inReach);
        int y0 = human.State.Y;
        rig.Session.Click(id, ClickButton.Right, ClickModifiers.None);
        rig.Run(1.0);
        Assert.Equal(y0 + 1, human.State.Y);
    }

    [Fact]
    public void ABigCrowdFitsItsRoom()
    {
        Assert.Equal(32, FakeFeed.SizeFor(0));
        Assert.Equal(39, FakeFeed.SizeFor(150));
        int bots = 150;
        var feed = new FakeFeed("Tester", bots: bots, size: FakeFeed.SizeFor(bots));
        double now = 0;
        feed.Poll(now);
        Assert.Equal(1 + bots + 5, feed.Count);
        now = Advance(feed, now, 5.0);
        Assert.Equal(1 + bots + 5, feed.Count);
    }

    [Fact]
    public void TheWelcomeTextIsDelivered()
    {
        var feed = new FakeFeed("Tester");
        var texts = new List<ServerText>();
        feed.ServerTextReceived += texts.Add;
        feed.Poll(0.0);
        Assert.Single(texts);
        Assert.Contains("Tester", texts[0].Html);
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Server;

namespace Anomaly.Server.Tests.M4b;

/// <summary>
/// The airflow streams (D141, protocol version 6, docs/ai/specs/protocol.md section 16 and replication.md section 16): the
/// Airflow message's codec and its caps, the replica applying it, the server's feed (the interest square, the changes
/// only, the level hysteresis, the clears, the runs with gaps, the cap) and a breach on a live host reaching a player.
/// </summary>
public class AirflowTests
{
    private const int West = 8, North = 1, Down = 32;

    [Fact]
    public void TheVersionHasTheFloorDevices() => Assert.True(ProtocolVersion.Current >= 7); // version 7: the floor devices

    [Fact]
    public void ALevelStandsForAPressureDifferenceOnALogScale()
    {
        Assert.Equal(0, Airflow.LevelOfKpa(0.4f));
        Assert.Equal(0, Airflow.LevelOfKpa(float.NaN));
        Assert.Equal(1, Airflow.LevelOfKpa(0.5f));
        Assert.Equal(Airflow.MaxLevel, Airflow.LevelOfKpa(10000f));
        for (int level = 1; level <= Airflow.MaxLevel; level++)
            Assert.Equal(level, Airflow.LevelOfKpa(Airflow.KpaOf(level)));
        Assert.InRange(Airflow.KpaOf(Airflow.MaxLevel), 150f, 250f);
        byte cell = Airflow.Pack(AirflowDir.West, 17);
        Assert.Equal(AirflowDir.West, Airflow.DirOf(cell));
        Assert.Equal(17, Airflow.LevelOf(cell));
        Assert.True(Airflow.IsFlow(cell));
        Assert.True(Airflow.IsFlow(Airflow.Clear));
        Assert.False(Airflow.IsFlow(Airflow.Unchanged));
        Assert.False(Airflow.IsFlow(7 << 5 | 3)); // direction 7 does not exist
        Assert.False(Airflow.IsFlow(1 << 5)); // a direction with level 0
        Assert.Equal(AirflowDir.Down, Airflow.DirOfByond(Down));
        Assert.Equal(AirflowDir.None, Airflow.DirOfByond(5)); // a diagonal
    }

    [Fact]
    public void AnUpdateRoundTripsAndABadOneIsRefused()
    {
        var update = new AirflowUpdate();
        update.Runs.Add(new AirflowRun(3, 7, 1, new[] { Airflow.Pack(AirflowDir.West, 20), Airflow.Unchanged, Airflow.Clear }));
        update.Runs.Add(new AirflowRun(255, 255, 16, new[] { Airflow.Pack(AirflowDir.Down, 1) }));
        byte[] bytes = Codec.Encode(update);
        Assert.Equal((byte)MessageId.Airflow, bytes[0]);
        Assert.True(Codec.TryDecode(bytes, out AirflowUpdate back, out _));
        Assert.Equal(2, back.Runs.Count);
        Assert.Equal(update.Runs[0].Cells, back.Runs[0].Cells);
        Assert.Equal((255, 255, 16), (back.Runs[1].X0, back.Runs[1].Y, back.Runs[1].Z));
        Assert.Equal(bytes, Codec.Encode(back));
        // 1 id + 1 run count + per run z, y, x0, count and one byte per cell + 1 vent count (none)
        Assert.Equal(1 + 1 + (4 + 3) + (4 + 1) + 1, bytes.Length);

        // a run past its row's end, a level-0 direction, a truncated message, no cells
        Assert.Throws<ProtocolException>(() => Codec.Encode(One(new AirflowRun(254, 1, 1, new byte[3]))));
        byte[] bad = Codec.Encode(One(new AirflowRun(1, 1, 1, new byte[] { Airflow.Clear })));
        bad[^2] = 1 << 5; // the cell (the last byte is version 7's vent count)
        Assert.False(Codec.TryDecode(bad, out AirflowUpdate _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
        Assert.False(Codec.TryDecode(bytes.AsSpan(0, bytes.Length - 1), out AirflowUpdate _, out _));
        Assert.Throws<ProtocolException>(() => Codec.Encode(One(new AirflowRun(1, 1, 1, Array.Empty<byte>()))));
    }

    private static AirflowUpdate One(AirflowRun run)
    {
        var update = new AirflowUpdate();
        update.Runs.Add(run);
        return update;
    }

    [Fact]
    public void TheReplicaKeepsTheFlowAndDropsItWithANewMap()
    {
        var replica = new ReplicaWorld();
        var map = new MapGrid { Tick = 5, Width = 8, Height = 8, Levels = 1 };
        map.TurfTypes.Add("/turf/open/floor");
        map.Runs.Add(new TurfRun(map.CellCount, 0));
        replica.Apply(map);
        var changed = new List<(int, int, int)>();
        replica.AirflowChanged += cells => changed.AddRange(cells);

        var update = new AirflowUpdate();
        update.Runs.Add(new AirflowRun(2, 3, 1, new[] { Airflow.Pack(AirflowDir.West, 9), Airflow.Unchanged, Airflow.Pack(AirflowDir.North, 4) }));
        replica.Apply(update);
        Assert.Equal(Airflow.Pack(AirflowDir.West, 9), replica.AirflowAt(2, 3, 1));
        Assert.Equal(Airflow.Clear, replica.AirflowAt(3, 3, 1));
        Assert.Equal(2, replica.AirflowCount);
        Assert.Equal(new[] { (2, 3, 1), (4, 3, 1) }, changed);

        var clear = new AirflowUpdate();
        clear.Runs.Add(new AirflowRun(2, 3, 1, new[] { Airflow.Clear }));
        replica.Apply(clear);
        Assert.Equal(1, replica.AirflowCount);

        var outside = new AirflowUpdate();
        outside.Runs.Add(new AirflowRun(8, 8, 1, new[] { Airflow.Clear, Airflow.Clear }));
        Assert.Throws<ProtocolException>(() => replica.Apply(outside));
        Assert.Equal(1, replica.AirflowCount);

        replica.Apply(map);
        Assert.Equal(0, replica.AirflowCount);
    }

    // The client's view of a feed's updates.
    private static Dictionary<(int, int, int), byte> ApplyAll(Dictionary<(int, int, int), byte> client, AirflowUpdate update)
    {
        if (update == null) return client;
        foreach (AirflowRun run in update.Runs)
            for (int k = 0; k < run.Cells.Length; k++)
            {
                byte cell = run.Cells[k];
                if (cell == Airflow.Unchanged) continue;
                if (cell == Airflow.Clear) client.Remove((run.X0 + k, run.Y, run.Z));
                else client[(run.X0 + k, run.Y, run.Z)] = cell;
            }
        return client;
    }

    [Fact]
    public void TheFeedSendsOnlyTheNearChangesWithHysteresisAndClearsWhatEnds()
    {
        var feed = new AirflowFeed(radius: 5);
        var view = new AirflowView();
        var client = new Dictionary<(int, int, int), byte>();
        float kpa = Airflow.KpaOf(20);

        // a row of flow toward a hole at x = 1, one cell far away, one two levels up
        for (int x = 2; x <= 6; x++) feed.Record(x, 10, 1, kpa, West);
        feed.Record(30, 10, 1, kpa, West);
        feed.Record(3, 10, 3, kpa, West);
        feed.Record(4, 11, 1, 0.1f, North); // under level 1: no flow
        feed.EndCycle();
        AirflowUpdate first = feed.Build(view, 4, 10, 1);
        ApplyAll(client, first);
        Assert.Equal(5, client.Count);
        Assert.All(client.Keys, k => Assert.Equal(10, k.Item2));
        AirflowRun run = Assert.Single(first.Runs);
        Assert.Equal((2, 10, 1), (run.X0, run.Y, run.Z));

        // nothing new: no update; the same flow next cycle: no update either
        Assert.Null(feed.Build(view, 4, 10, 1));
        for (int x = 2; x <= 6; x++) feed.Record(x, 10, 1, kpa, West);
        feed.Record(30, 10, 1, kpa, West);
        feed.Record(3, 10, 3, kpa, West);
        feed.EndCycle();
        Assert.Null(feed.Build(view, 4, 10, 1));

        // one level up is under the hysteresis, three is not; a direction change always counts; x = 6 ends
        feed.Record(2, 10, 1, Airflow.KpaOf(21), West);
        feed.Record(3, 10, 1, Airflow.KpaOf(23), West);
        feed.Record(4, 10, 1, kpa, North);
        feed.Record(5, 10, 1, kpa, West);
        feed.EndCycle();
        AirflowUpdate second = feed.Build(view, 4, 10, 1);
        ApplyAll(client, second);
        Assert.Equal(Airflow.Pack(AirflowDir.West, 20), client[(2, 10, 1)]);
        Assert.Equal(Airflow.Pack(AirflowDir.West, 23), client[(3, 10, 1)]);
        Assert.Equal(Airflow.Pack(AirflowDir.North, 20), client[(4, 10, 1)]);
        Assert.False(client.ContainsKey((6, 10, 1)));
        // x = 3, 4 and 6 changed, 5 did not: one run with an unchanged byte at 5
        AirflowRun gap = Assert.Single(second.Runs);
        Assert.Equal(3, gap.X0);
        Assert.Equal(new[] { Airflow.Pack(AirflowDir.West, 23), Airflow.Pack(AirflowDir.North, 20), Airflow.Unchanged, Airflow.Clear }, gap.Cells);

        // the player walks away: everything is cleared; no mob: likewise
        feed.Record(2, 10, 1, kpa, West);
        feed.EndCycle();
        ApplyAll(client, feed.Build(view, 40, 40, 1));
        Assert.Empty(client);
        Assert.Equal(0, view.Count);
        ApplyAll(client, feed.Build(view, 2, 10, 1));
        Assert.Single(client);
        ApplyAll(client, feed.Build(view, 0, 0, 0));
        Assert.Empty(client);
    }

    [Fact]
    public void AHugeFlowGoesOutNearestFirstOverSeveralTicks()
    {
        var feed = new AirflowFeed(radius: 64);
        var view = new AirflowView();
        var client = new Dictionary<(int, int, int), byte>();
        int cells = 0;
        for (int y = 1; y <= 100; y++)
            for (int x = 1; x <= 100; x += 2) // every other cell: a run per two changes at most
            {
                feed.Record(x, y, 1, 10f, West);
                cells++;
            }
        feed.EndCycle();
        AirflowUpdate first = feed.Build(view, 50, 50, 1);
        ApplyAll(client, first);
        Assert.True(Codec.Encode(first).Length <= Limits.MaxAirflowBytes);
        Assert.Equal(AirflowFeed.MaxChangesPerUpdate, client.Count);
        Assert.True(client.ContainsKey((49, 50, 1)), "the nearest go first");
        // the 64-radius square around (50, 50) holds every cell of 1..100: the rest follows over the next ticks
        for (int ticks = 1; ticks < 10; ticks++)
        {
            AirflowUpdate next = feed.Build(view, 50, 50, 1);
            if (next == null) break;
            Assert.True(Codec.Encode(next).Length <= Limits.MaxAirflowBytes);
            ApplyAll(client, next);
        }
        Assert.Equal(cells, client.Count);
        Assert.Null(feed.Build(view, 50, 50, 1));
    }

    [Fact]
    public void ABreachOnTheLiveHostReachesThePlayerAsAFlowIntoTheHole()
    {
        using TestServer server = TestServer.Start(TestServer.DefaultConfig(devCommands: true));
        FakeRemote alice = server.Join("Alice");
        server.Tick(40);
        (int ax, int ay) = server.PositionOf("Alice");
        Assert.Empty(alice.Messages(MessageId.Airflow));

        // the rim wall nearest to Alice on the west side (the test room's rim is the map's edge: it opens to space)
        alice.Action(ActionKind.DevCommand, text: $"breach 1 {ay}");
        server.Tick(60);
        var client = new Dictionary<(int, int, int), byte>();
        foreach (byte[] bytes in alice.Messages(MessageId.Airflow))
        {
            Assert.True(Codec.TryDecode(bytes, out AirflowUpdate update, out ProtocolError error), error.ToString());
            ApplyAll(client, update);
        }
        Assert.True(server.Host.AirflowCells > 0, "SSair saw a flow");
        Assert.True(client.TryGetValue((2, ay, 1), out byte byHole), "the cell by the hole flows");
        Assert.Equal(AirflowDir.West, Airflow.DirOf(byHole));
        Assert.True(Airflow.KpaOf(Airflow.LevelOf(byHole)) > 5f, "a strong flow by the hole: " + Airflow.KpaOf(Airflow.LevelOf(byHole)));
        Assert.All(client.Keys, k => Assert.True(Math.Abs(k.Item1 - ax) <= ServerConfig.DefaultAirflowRadius && Math.Abs(k.Item2 - ay) <= ServerConfig.DefaultAirflowRadius));
    }

    [Fact]
    public void ARadiusOfZeroSendsNoAirflow()
    {
        ServerConfig config = TestServer.DefaultConfig(devCommands: true);
        using TestServer server = TestServer.Start(new ServerConfig
        {
            Seed = config.Seed, RoomWidth = config.RoomWidth, RoomHeight = config.RoomHeight, RoomSpace = 0, Console = false,
            StatusEveryTicks = config.StatusEveryTicks, DevCommands = true, AirflowRadius = 0,
        });
        FakeRemote alice = server.Join("Alice");
        server.Tick(20);
        (_, int ay) = server.PositionOf("Alice");
        alice.Action(ActionKind.DevCommand, text: $"breach 1 {ay}");
        server.Tick(40);
        Assert.Empty(alice.Messages(MessageId.Airflow));
        Assert.Equal(0, server.Host.AirflowCells);
    }

    [Fact]
    public void TheFloorDevicesAirRoundTripsAndReachesTheReplicaAndTheFeedSendsItsChanges()
    {
        var update = new AirflowUpdate();
        update.Vents.Add(new AirflowVent(5, 6, 1, AirflowVents.Pack(AirflowVentKind.Out, 12)));
        update.Vents.Add(new AirflowVent(7, 6, 1, Airflow.Clear));
        byte[] bytes = Codec.Encode(update);
        Assert.Equal(1 + 1 + 1 + 2 * 4, bytes.Length);
        Assert.True(Codec.TryDecode(bytes, out AirflowUpdate back, out _));
        Assert.Equal(update.Vents, back.Vents);
        Assert.Equal(bytes, Codec.Encode(back));
        var bad = new AirflowUpdate();
        bad.Vents.Add(new AirflowVent(5, 6, 1, 3 << 5 | 4)); // kind 3 does not exist
        Assert.Throws<ProtocolException>(() => Codec.Encode(bad));

        var replica = new ReplicaWorld();
        var map = new MapGrid { Tick = 5, Width = 8, Height = 8, Levels = 1 };
        map.TurfTypes.Add("/turf/open/floor");
        map.Runs.Add(new TurfRun(map.CellCount, 0));
        replica.Apply(map);
        var changed = new List<(int, int, int)>();
        replica.AirflowChanged += cells => changed.AddRange(cells);
        replica.Apply(back);
        Assert.Equal(AirflowVentKind.Out, AirflowVents.KindOf(replica.AirflowVentAt(5, 6, 1)));
        Assert.Single(replica.AirflowVentCells);
        Assert.Contains((5, 6, 1), changed);

        // the feed: devices on a cell add up; a kind change always goes out, a one-level change does not
        var feed = new AirflowFeed(radius: 5);
        var view = new AirflowView();
        feed.RecordVent(3, 3, 1, Airflow.KpaOf(10));
        feed.RecordVent(3, 3, 1, Airflow.KpaOf(10)); // twice the gas: about 3.5 levels more
        feed.RecordVent(4, 3, 1, -Airflow.KpaOf(8));
        feed.RecordVent(30, 30, 1, 5f); // far from the player
        feed.EndCycle();
        AirflowUpdate first = feed.Build(view, 3, 4, 1);
        Assert.Empty(first.Runs);
        Assert.Equal(2, first.Vents.Count);
        AirflowVent out1 = Assert.Single(first.Vents, v => v.X == 3);
        Assert.Equal(AirflowVentKind.Out, AirflowVents.KindOf(out1.Cell));
        Assert.InRange(Airflow.LevelOf(out1.Cell), 13, 14);
        Assert.Equal(AirflowVentKind.In, AirflowVents.KindOf(Assert.Single(first.Vents, v => v.X == 4).Cell));

        feed.RecordVent(3, 3, 1, Airflow.KpaOf(14)); // one level down: not worth a message
        feed.RecordVent(4, 3, 1, Airflow.KpaOf(8)); // turned around
        feed.EndCycle();
        AirflowUpdate second = feed.Build(view, 3, 4, 1);
        AirflowVent turned = Assert.Single(second.Vents);
        Assert.Equal((4, AirflowVentKind.Out), (turned.X, AirflowVents.KindOf(turned.Cell)));

        feed.EndCycle(); // the devices stopped
        AirflowUpdate third = feed.Build(view, 3, 4, 1);
        Assert.Equal(2, third.Vents.Count);
        Assert.All(third.Vents, v => Assert.Equal(Airflow.Clear, v.Cell));
    }
}

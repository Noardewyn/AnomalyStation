using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>M3 P6: the fake server serving a FakeMap (the FakeFeed version 3): the join, the collision on the map and the door animation.</summary>
public sealed class FakeMapServerTests
{
    private static FakeFeed Feed(string text = FakeMapTests.Room)
    {
        var feed = new FakeFeed("Tester", seed: 1, bots: 0, oneWayLatencyMs: 0, jitterMs: 0, map: FakeMap.Parse(text, "room"));
        feed.Poll(0);
        return feed;
    }

    [Fact]
    public void TheJoinCarriesTheMapAndItsEntities()
    {
        FakeFeed feed = Feed();
        Assert.Equal(FeedPhase.Playing, feed.Phase);
        Assert.Equal((6, 5, 1), (feed.Map.Width, feed.Map.Height, feed.Map.Levels));
        Assert.True(feed.Replica.TryGet(FakeServer.OwnId, out ReplicaEntity own));
        Assert.Equal((3, 4), (own.State.X, own.State.Y)); // the start landmark
        Assert.True(feed.Replica.TryGet(FakeServer.MapEntityIdBase + 1, out ReplicaEntity first));
        Assert.StartsWith("/obj/", first.TypePath);
        Assert.False(first.State.Appearance.IsNone);
        Assert.True(feed.Replica.IsSolid(1, 5, 1));
        Assert.False(feed.Replica.IsSolid(3, 4, 1));
        Assert.False(feed.Replica.TurfAppearance(1, 5, 1).IsNone);
    }

    [Fact]
    public void AnAirlockOpensAsTgsDoorDoes()
    {
        FakeFeed feed = Feed();
        Assert.True(feed.Replica.IsSolid(3, 5, 1));
        ReplicaEntity door = feed.Replica.Entities.First(e => e.TypePath.StartsWith("/obj/machinery/door/airlock"));
        Assert.Equal("closed", StateOf(feed, door));

        Assert.True(feed.Server.AnimateDoor(3, 5, 1, "open"));
        var seen = new List<string>();
        bool solidWhile = true;
        double t = 0;
        for (int tick = 0; tick < 20; tick++)
        {
            t += 0.05;
            feed.Poll(t);
            string state = StateOf(feed, door);
            if (seen.Count == 0 || seen[^1] != state)
                seen.Add(state);
            if (tick == 5)
                solidWhile = feed.Replica.IsSolid(3, 5, 1);
        }
        Assert.Equal(new[] { "opening", "open" }, seen);
        Assert.True(solidWhile, "the cell turns passable at 0.5 s, not before");
        Assert.False(feed.Replica.IsSolid(3, 5, 1));

        Assert.True(feed.Server.AnimateDoor(3, 5, 1, "close"));
        seen.Clear();
        for (int tick = 0; tick < 20; tick++)
        {
            t += 0.05;
            feed.Poll(t);
            string state = StateOf(feed, door);
            if (seen.Count == 0 || seen[^1] != state)
                seen.Add(state);
        }
        Assert.Equal(new[] { "closing", "closed" }, seen);
        Assert.True(feed.Replica.IsSolid(3, 5, 1));
    }

    [Fact]
    public void ADenyFlashesAndTheDoorStaysShut()
    {
        FakeFeed feed = Feed();
        ReplicaEntity door = feed.Replica.Entities.First(e => e.TypePath.StartsWith("/obj/machinery/door/airlock"));
        Assert.True(feed.Server.AnimateDoor(3, 5, 1, "deny"));
        double t = 0;
        var seen = new List<string>();
        for (int tick = 0; tick < 12; tick++)
        {
            t += 0.05;
            feed.Poll(t);
            string state = StateOf(feed, door);
            if (seen.Count == 0 || seen[^1] != state)
                seen.Add(state);
        }
        Assert.Equal(new[] { "deny", "closed" }, seen);
        Assert.True(feed.Replica.IsSolid(3, 5, 1));
    }

    [Fact]
    public void ABorderWindowBlocksOnlyItsFace()
    {
        FakeFeed feed = Feed();
        ICollisionViewProbe probe = new(feed.Server.View);
        Assert.False(feed.Server.View.IsSolid(2, 3, 1));
        Assert.True(probe.FaceBlocked(2, 3, Anomaly.Core.Motion.TileEdges.East));
        Assert.False(probe.FaceBlocked(2, 3, Anomaly.Core.Motion.TileEdges.North));
    }

    [Fact]
    public void TheReplicaSeesTheFacesFlagsAndStarlightTheServerCollidesWith()
    {
        FakeFeed feed = Feed();
        for (int y = 1; y <= 5; y++)
        {
            for (int x = 1; x <= 6; x++)
            {
                Assert.Equal(feed.Server.View.EdgesOf(x, y, 1), feed.Replica.FacesOf(x, y, 1));
                Assert.Equal(feed.Server.View.IsSolid(x, y, 1), feed.Replica.IsSolid(x, y, 1));
            }
        }
        Assert.Equal(CellFlags.FaceEast, feed.Replica.CellFlagsAt(2, 3, 1) & CellFlags.Faces);
        Assert.True(feed.Replica.IsOpaque(1, 5, 1));
        Assert.True(feed.Replica.IsOpaque(3, 5, 1), "a closed airlock blocks light");
        Assert.True(feed.Replica.IsStarlit(1, 2, 1));
        Assert.False(feed.Replica.Starlight.IsNone);
        // The airlock opening turns the cell passable and clear of opacity in the replica too.
        Assert.True(feed.Server.AnimateDoor(3, 5, 1, "open"));
        double t = 0;
        for (int tick = 0; tick < 14; tick++)
            feed.Poll(t += 0.05);
        Assert.False(feed.Replica.IsSolid(3, 5, 1));
        Assert.False(feed.Replica.IsOpaque(3, 5, 1));
    }

    // ---- bump-open doors (a stand-in until P3's doors run on the real server) ----

    // A 7 x 5 room: walls round it, floor inside; `extra` gives a cell's extra members (a door, the start landmark).
    private static string DoorRoom(Func<int, int, string> extra)
    {
        var text = new System.Text.StringBuilder("anomaly-map 1\nsize 7 5 1\nchunk 32\ndefault /turf/open/space/basic | /area/space\n\n[chunk 1 1 1]\n");
        for (int y = 5; y >= 1; y--)
        {
            for (int x = 1; x <= 7; x++)
            {
                bool wall = x == 1 || x == 7 || y == 1 || y == 5;
                text.Append(x).Append(',').Append(y).Append(": ").Append(extra(x, y)).Append(wall ? "/turf/closed/wall" : "/turf/open/floor/iron").Append(" | /area/station/hallway\n");
            }
        }
        return text.ToString();
    }

    // The player starts at (5, 2) with a windoor facing north at (5, 3), closed shutters at (3, 2) and an airlock in the north wall at (3, 5).
    private const string WindoorMember = "/obj/machinery/door/window/left/directional/north | ";

    private static FakeFeed DoorFeed(string windoor = WindoorMember, int startX = 5)
        => Feed(DoorRoom((x, y) =>
            (x, y) == (startX, 2) ? "/obj/effect/landmark/start | "
            : (x, y) == (5, 3) ? windoor
            : (x, y) == (3, 2) ? "/obj/machinery/door/poddoor/shutters | "
            : (x, y) == (3, 5) ? "/obj/machinery/door/airlock | "
            : string.Empty));

    private static bool NorthFaceShut(FakeFeed feed) => (feed.Replica.FacesOf(5, 3, 1) & Anomaly.Core.Motion.TileEdges.North) != 0;

    private static uint _walkSeq;

    // One wish vector as the next input frame; the server repeats it while no newer frame comes.
    private static void Walk(FakeFeed feed, sbyte moveX, sbyte moveY)
    {
        var message = new MotionInputMessage();
        _walkSeq = Math.Max(_walkSeq, feed.Server.AppliedSeq); // D331: past the seqs the repeats stood in for, as the client's prediction
        message.Frames.Add(new Anomaly.Core.Motion.MotionInput(++_walkSeq, moveX, moveY, 0, Anomaly.Core.Motion.MotionInputFlags.None));
        feed.SendMotion(message);
    }

    // Polls tick by tick until the door's state is `state` (at most `limit` ticks); the replica tick it was first seen at, or -1.
    private static long WaitFor(FakeFeed feed, ReplicaEntity door, string state, int limit, ref double t)
    {
        for (int i = 0; i < limit; i++)
        {
            feed.Poll(t += 0.05);
            if (StateOf(feed, door) == state)
                return feed.Tick;
        }
        return -1;
    }

    private static ReplicaEntity Windoor(FakeFeed feed) => feed.Replica.Entities.First(e => e.TypePath.StartsWith("/obj/machinery/door/window", StringComparison.Ordinal));

    [Fact]
    public void WalkingIntoAnAirlockOpensItAndItClosesAfterTgsEightSeconds()
    {
        FakeFeed feed = Feed();
        ReplicaEntity door = feed.Replica.Entities.First(e => e.TypePath.StartsWith("/obj/machinery/door/airlock"));
        double t = 0;
        feed.Poll(t);
        Assert.Equal("closed", StateOf(feed, door));
        Walk(feed, 0, 127); // north from (3, 4) into the airlock
        long opening = WaitFor(feed, door, "opening", 80, ref t);
        Assert.True(opening > 0, "the bump opens the door");
        Walk(feed, 0, -127); // back away: nobody stands in the doorway
        Assert.True(WaitFor(feed, door, "open", 30, ref t) > 0);
        Assert.False(feed.Replica.IsSolid(3, 5, 1));
        long closing = WaitFor(feed, door, "closing", 400, ref t);
        Assert.True(closing > 0);
        // tg: autoclose_in(8 SECONDS) when the opening starts (airlock.dm:1343): 160 ticks.
        Assert.InRange(closing - opening, 158, 162);
        Assert.True(WaitFor(feed, door, "closed", 30, ref t) > 0);
        Assert.True(feed.Replica.IsSolid(3, 5, 1));
    }

    [Fact]
    public void AnAirlockWithAMobInItsDoorwayDoesNotCloseAndTriesAgainAfterSixSeconds()
    {
        FakeFeed feed = Feed();
        ReplicaEntity door = feed.Replica.Entities.First(e => e.TypePath.StartsWith("/obj/machinery/door/airlock"));
        double t = 0;
        feed.Poll(t);
        Walk(feed, 0, 127);
        long opening = WaitFor(feed, door, "opening", 80, ref t);
        Assert.True(opening > 0);
        // The player walks on into the doorway (3, 5) and stands there, against the map's edge, past the eight seconds.
        for (int i = 0; i < 190; i++)
            feed.Poll(t += 0.05);
        Assert.Equal((3, 5), (feed.Server.PlayerState.TileX, feed.Server.PlayerState.TileY));
        Assert.Equal("open", StateOf(feed, door)); // 160 ticks passed with the doorway occupied: it did not close
        Walk(feed, 0, -127); // walks out at about tick 190 after the bump
        for (int i = 0; i < 20; i++)
            feed.Poll(t += 0.05);
        Assert.NotEqual((3, 5), (feed.Server.PlayerState.TileX, feed.Server.PlayerState.TileY));
        Assert.Equal("open", StateOf(feed, door)); // the retry is not due yet
        long closing = WaitFor(feed, door, "closing", 200, ref t);
        Assert.True(closing > 0);
        // door.dm close(): autoclose_in(DOOR_CLOSE_WAIT), 60 deciseconds after the failed attempt at 160 ticks.
        Assert.InRange(closing - opening, 160 + 120 - 2, 160 + 120 + 2);
    }

    [Fact]
    public void AWindoorOpensWhenWalkedIntoAndItsFaceFollowsTgsTiming()
    {
        FakeFeed feed = DoorFeed();
        ReplicaEntity door = Windoor(feed);
        double t = 0;
        feed.Poll(t);
        Assert.Equal("left", StateOf(feed, door));
        Assert.True(NorthFaceShut(feed));
        Walk(feed, 0, 127); // north from (5, 2): into the windoor's tile, then into its face
        long opening = WaitFor(feed, door, "leftopening", 80, ref t);
        Assert.True(opening > 0, "the bump opens the windoor");
        Assert.True(NorthFaceShut(feed)); // still shut: its face opens at 0.7 s
        long open = WaitFor(feed, door, "leftopen", 40, ref t);
        Assert.InRange(open - opening, 17, 19); // windowdoor.dm: 0.9 s
        Assert.False(NorthFaceShut(feed));
        // The player walks through and stands north of it; open_and_close() closes it 8 s after the opening ends (a secure windoor 5 s).
        for (int i = 0; i < 30; i++)
            feed.Poll(t += 0.05);
        Assert.Equal(4, feed.Server.PlayerState.TileY);
        long closing = WaitFor(feed, door, "leftclosing", 400, ref t);
        Assert.InRange(closing - opening, 18 + 160 - 2, 18 + 160 + 2);
        Assert.True(WaitFor(feed, door, "left", 40, ref t) > 0);
        Assert.True(NorthFaceShut(feed));
    }

    [Fact]
    public void ASecureWindoorClosesAfterFiveSeconds()
    {
        FakeFeed feed = DoorFeed("/obj/machinery/door/window/left/directional/north{icon_state = \"leftsecure\"} | ");
        ReplicaEntity door = Windoor(feed);
        double t = 0;
        feed.Poll(t);
        Assert.Equal("leftsecure", StateOf(feed, door));
        Walk(feed, 0, 127);
        long opening = WaitFor(feed, door, "leftsecureopening", 80, ref t);
        Assert.True(opening > 0);
        for (int i = 0; i < 30; i++)
            feed.Poll(t += 0.05);
        long closing = WaitFor(feed, door, "leftsecureclosing", 400, ref t);
        Assert.InRange(closing - opening, 18 + 100 - 2, 18 + 100 + 2);
    }

    [Fact]
    public void APodDoorOrShuttersPressedIntoStayAsMapped()
    {
        // can_open_with_hands is FALSE for them (door.dm Bumped): the player at (4, 2) walks west into the shut shutters at (3, 2) for five seconds.
        FakeFeed feed = DoorFeed(startX: 4);
        ReplicaEntity shutters = feed.Replica.Entities.First(e => e.TypePath == "/obj/machinery/door/poddoor/shutters");
        double t = 0;
        feed.Poll(t);
        Walk(feed, -127, 0);
        for (int i = 0; i < 100; i++)
            feed.Poll(t += 0.05);
        Assert.Equal(4, feed.Server.PlayerState.TileX);
        Assert.Equal("closed", StateOf(feed, shutters));
        Assert.True(feed.Replica.IsSolid(3, 2, 1));
    }

    [Fact]
    public void TheConsoleCommandStillOpensAndClosesDoorsAndLeavesThemAsTheyAre()
    {
        // The console's door command still works on an airlock and a windoor and leaves it as it is (no auto-close): only a bump of the own mob closes later.
        FakeFeed feed = DoorFeed();
        ReplicaEntity airlock = feed.Replica.Entities.First(e => e.TypePath == "/obj/machinery/door/airlock");
        ReplicaEntity windoor = Windoor(feed);
        double t = 0;
        feed.Poll(t);
        Assert.True(feed.Server.AnimateDoor(3, 5, 1, "open"));
        Assert.True(feed.Server.AnimateDoor(5, 3, 1, "open"));
        for (int i = 0; i < 400; i++)
            feed.Poll(t += 0.05);
        Assert.Equal("open", StateOf(feed, airlock));
        Assert.Equal("leftopen", StateOf(feed, windoor));
        Assert.True(feed.Server.AnimateDoor(5, 3, 1, "close"));
        Assert.True(WaitFor(feed, windoor, "left", 40, ref t) > 0);
    }

    [Fact]
    public void TheLightsOfTheMapReachTheReplica()
    {
        FakeFeed feed = Feed();
        ReplicaEntity light = feed.Replica.Entities.First(e => e.TypePath.StartsWith("/obj/machinery/light"));
        Assert.True(light.HasLight);
        Assert.True(light.Light.On);
    }

    private readonly struct ICollisionViewProbe
    {
        private readonly Anomaly.Core.Motion.ICollisionView _view;

        public ICollisionViewProbe(Anomaly.Core.Motion.ICollisionView view) => _view = view;

        public bool FaceBlocked(int x, int y, Anomaly.Core.Motion.TileEdges face) => (_view.EdgesOf(x, y, 1) & face) != 0;
    }

    private static string StateOf(FakeFeed feed, ReplicaEntity door)
    {
        Assert.True(feed.Replica.TryGetAppearance(door.State.Appearance, out AppearanceEntry entry));
        return feed.Replica.ResolveString(entry.IconState);
    }
}

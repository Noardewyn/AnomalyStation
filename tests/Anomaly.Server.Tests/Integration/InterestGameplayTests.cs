using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10 slice C (D23, D37, D40; multiplayer.md section 6) through the real server on MetaStation: a client holds what its eye is interested
/// in and nothing else. Bob in a room Alice does not see, beyond her near chunks, never reaches her client, not even while he walks; a light
/// fixture behind the walls within the far radius does; Bob next to her does, and stays for the hold after he leaves. A closed airlock
/// between Alice and the room beyond keeps Bob out of her client; opened, the room and Bob come in; closed again, they stay for the room's
/// hold (performance.md P3.8: a door's cycle within it sends nothing again) and leave after it and the entities' hold. A teleport into the
/// station's centre streams its spawns over ticks, nearest first (P3.7; the numbers are written to the output).
/// </summary>
public class InterestGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.truthy(t.opacity)
        && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || Dm.truthy(a.opacity));

    private static IEnumerable<turf> Floors()
    {
        for (int y = 1; y <= Dm.num(world.maxy); y++)
            for (int x = 1; x <= Dm.num(world.maxx); x++)
                if (locate(x, y, 1) is turf t && Clear(t))
                    yield return t;
    }

    private static int Ordinal(RealHeadlessWorld w, RealHeadlessWorld.Player p) => w.Host.Sessions.Last(s => s.Key == p.Name).Ordinal;

    private static void Put(RealHeadlessWorld w, mob m, turf t) => Do(w, () => Waiting.NoSleep(m.forceMove(t)));

    [Fact]
    public void A_client_holds_what_its_eye_sees_and_never_a_player_behind_the_walls()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 101, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob aliceMob = w.MobOf(alice), bobMob = w.MobOf(bob);
        int aliceSession = Ordinal(w, alice);
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        ReplicaWorld sees = alice.Session.Replica;

        // Alice in a corridor; a hidden spot: a floor 24 to 56 tiles off in an area her eye does not see.
        turf start = In(w, () => Floors().First(t => Enumerable.Range(1, 9).All(k => locate(t.x + k, t.y, 1) is turf o && Clear(o))));
        Put(w, aliceMob, start);
        w.Run(Second);
        turf hidden = In(w, () => Floors().First(t =>
        {
            float d = Math.Max(Math.Abs(t.x - start.x), Math.Abs(t.y - start.y));
            return d >= 24 && d <= 56 && !w.Replicator.SeesArea(aliceSession, (int)t.x, (int)t.y, 1);
        }));
        output.WriteLine($"Alice at {start.x},{start.y}; hidden spot {hidden.x},{hidden.y}");
        Put(w, bobMob, hidden);
        w.Run(3 * Second); // past the 2 s hold
        Assert.False(sees.TryGet(bobMob.dm_id, out _), "Bob behind the walls is still in Alice's client");
        Assert.False(w.Replicator.Holds(aliceSession, bobMob.dm_id));

        // Bob walks and turns over there: nothing of him reaches her (D40).
        var spawned = new List<long>();
        sees.Spawned += e => spawned.Add(e.Id);
        bob.Keys = MoveKeys.Forward;
        for (int i = 0; i < 2 * Second; i++)
        {
            bob.Yaw += 0.1;
            w.Run(1);
        }
        bob.Keys = MoveKeys.None;
        Assert.DoesNotContain(bobMob.dm_id, spawned);
        Assert.False(sees.TryGet(bobMob.dm_id, out _));

        // Far fewer entities than the map has; a light fixture behind the walls within the far radius is there (the client lights from it).
        int held = sees.Entities.Count(), all = w.Replicator.EntityCount;
        output.WriteLine($"Alice's client holds {held} of {all} entities");
        Assert.True(held < all / 3, $"held {held} of {all}");
        obj_machinery_light lamp = In(w, () => Dm.each<obj_machinery_light>(world.contents).FirstOrDefault(l =>
        {
            turf on = l.loc as turf;
            if (on == null) return false;
            float d = Math.Max(Math.Abs(on.x - start.x), Math.Abs(on.y - start.y));
            // a lit one: tg's map load breaks a random few, and a broken lamp lights nothing, so no client needs it
            return Dm.num(l.light_range) > 0 && d >= 24 && d <= 56 && !w.Replicator.SeesArea(aliceSession, (int)on.x, (int)on.y, 1);
        }));
        Assert.NotNull(lamp);
        Assert.True(sees.TryGet(lamp.dm_id, out _), "a light fixture behind the walls within the far radius");
        // So is an airlock in a room she does not see (seen far down a corridor, a door must not pop in as she nears it).
        obj_machinery_door_airlock door = In(w, () => Dm.each<obj_machinery_door_airlock>(world.contents).FirstOrDefault(a =>
        {
            turf on = a.loc as turf;
            if (on == null) return false;
            float d = Math.Max(Math.Abs(on.x - start.x), Math.Abs(on.y - start.y));
            return d >= 24 && d <= 56 && !w.Replicator.SeesArea(aliceSession, (int)on.x, (int)on.y, 1);
        }));
        Assert.NotNull(door);
        Assert.True(sees.TryGet(door.dm_id, out _), "an airlock behind the walls within the far radius");

        // Bob next to her: in; back to the hidden spot: still there within the hold, gone after it.
        Put(w, bobMob, In(w, () => locate(start.x + 3, start.y, 1) as turf));
        w.Run(5);
        Assert.True(sees.TryGet(bobMob.dm_id, out ReplicaEntity near));
        Assert.Equal(In(w, () => bobMob.name), near.State.Name);
        Put(w, bobMob, hidden);
        w.Run(Second);
        Assert.True(sees.TryGet(bobMob.dm_id, out _), "the hold keeps him for 2 s");
        w.Run(2 * Second);
        Assert.False(sees.TryGet(bobMob.dm_id, out _));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void An_airlock_between_two_rooms_decides_whether_the_room_beyond_reaches_the_client()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 103, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false, interest: new InterestOptions { NearChunks = 0, NearLeaveChunks = 0 }); // the rooms alone decide
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob aliceMob = w.MobOf(alice), bobMob = w.MobOf(bob);
        int aliceSession = Ordinal(w, alice);
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        ReplicaWorld sees = alice.Session.Replica;

        // An opaque airlock with clear floors of two different areas on its two sides, and a floor of the far area 9 to 30 tiles
        // from Alice's side, in another chunk, that her eye does not see while the airlock is closed.
        (obj_machinery_door_airlock door, turf mine, turf beyond)? found = null;
        foreach (obj_machinery_door_airlock door in In(w, () => Dm.each<obj_machinery_door_airlock>(world.contents)
                     .Where(d => d is not obj_machinery_door_airlock_glass && Dm.truthy(d.density) && d.loc is turf).ToList()))
        {
            turf at = In(w, () => door.loc as turf);
            foreach ((int dx, int dy) in new[] { (1, 0), (0, 1) })
            {
                turf a = In(w, () => locate(at.x - dx, at.y - dy, 1) as turf), b = In(w, () => locate(at.x + dx, at.y + dy, 1) as turf);
                if (a == null || b == null || !In(w, () => Clear(a) && Clear(b) && a.loc != b.loc)) continue;
                foreach ((turf side, turf other) in new[] { (a, b), (b, a) })
                {
                    Put(w, aliceMob, side);
                    w.Run(2);
                    turf spot = In(w, () => Floors().FirstOrDefault(t => t.loc == other.loc
                        && Math.Max(Math.Abs(t.x - side.x), Math.Abs(t.y - side.y)) is >= 9 and <= 30
                        && ((int)t.x - 1) / 8 != ((int)side.x - 1) / 8 | ((int)t.y - 1) / 8 != ((int)side.y - 1) / 8
                        && !w.Replicator.SeesArea(aliceSession, (int)t.x, (int)t.y, 1)));
                    if (spot != null)
                    {
                        found = (door, side, spot);
                        break;
                    }
                }
                if (found != null) break;
            }
            if (found != null) break;
        }
        Assert.NotNull(found);
        (obj_machinery_door_airlock airlock, turf here, turf there) = found.Value;
        output.WriteLine($"airlock at {In(w, () => ((turf)airlock.loc).x)},{In(w, () => ((turf)airlock.loc).y)}; Alice at {here.x},{here.y}; Bob at {there.x},{there.y}");
        Put(w, bobMob, there);
        w.Run(8 * Second); // past the room's hold of the rooms the search walked Alice through, and the entities' hold
        Assert.False(sees.TryGet(bobMob.dm_id, out _), "the closed airlock keeps the room beyond out");

        Do(w, () => Waiting.Detach(airlock.open(Defines.BYPASS_DOOR_CHECKS)));
        w.Run(2 * Second);
        Assert.True(In(w, () => !Dm.truthy(airlock.opacity)), "the airlock opened");
        Assert.True(w.Replicator.SeesArea(aliceSession, (int)there.x, (int)there.y, 1));
        Assert.True(sees.TryGet(bobMob.dm_id, out _), "the open airlock lets the room beyond in");

        // The room's things the client holds there only through the room: not on Alice's chunk (near), no light nor door (far), no mob nor a
        // visual (Bob's eyelids go with him).
        int aliceChunk = ((int)here.x - 1) / 8 * 1000 + ((int)here.y - 1) / 8;
        int Beyond() => sees.Entities.Count(e => !e.IsMob() && !e.IsVisual && !e.HasLight && !IsDoor(e) && (e.State.X - 1) / 8 * 1000 + (e.State.Y - 1) / 8 != aliceChunk
            && In(w, () => (locate(e.State.X, e.State.Y, e.State.Z) as turf)?.loc == there.loc));
        w.Run(3 * Second); // the rooms the airlock linked arrive through the queue
        int beyond = Beyond();
        output.WriteLine($"the room's things: {beyond}");
        Assert.True(beyond > 0, "the room's things came with it");

        // P3.8: closed for longer than the entities' hold but within the room's, the room's things stay (Bob, a mob, leaves after his hold, as
        // before: D40); opened again, nothing of the room is sent again.
        var spawned = new List<ReplicaEntity>();
        sees.Spawned += e => spawned.Add(e);
        Do(w, () => Waiting.Detach(airlock.close(Defines.BYPASS_DOOR_CHECKS)));
        w.Run(3 * Second);
        Assert.True(In(w, () => Dm.truthy(airlock.opacity)), "the airlock closed");
        output.WriteLine($"3 s after it shut: {Beyond()}");
        Assert.Equal(beyond, Beyond());
        Do(w, () => Waiting.Detach(airlock.open(Defines.BYPASS_DOOR_CHECKS)));
        w.Run(Second);
        output.WriteLine($"the door's cycle spawned {spawned.Count} entities: " + string.Join(", ", spawned.Select(e => e.TypePath)));
        Assert.True(spawned.Count(e => !e.IsMob() && !e.TypePath.StartsWith("/obj/effect/particle_effect", StringComparison.Ordinal)) < 5, $"the door's cycle sent {spawned.Count} entities again"); // sparks made meanwhile are new, not sent again

        // Closed for good: Bob leaves after the entities' hold (40 ticks), the room's things after the room's (100) and theirs.
        Do(w, () => Waiting.Detach(airlock.close(Defines.BYPASS_DOOR_CHECKS)));
        w.Run(4 * Second);
        Assert.True(In(w, () => Dm.truthy(airlock.opacity)), "the airlock closed");
        Assert.False(sees.TryGet(bobMob.dm_id, out _), "closed again, Bob leaves after the hold");
        Assert.Equal(beyond, Beyond());
        w.Run(5 * Second);
        Assert.Equal(0, Beyond());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void A_teleport_into_the_station_centre_streams_its_spawns_over_ticks()
    {
        var interest = new InterestOptions();
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 107, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false, interest: interest);
        // the client's messages as they arrive (the Snapshot and each Delta: the session's block is most of a Delta with interest on)
        var transport = w.Network.CreateClient();
        var sizes = new List<(long Tick, MessageId Id, int Bytes)>();
        transport.Received += (_, message) =>
        {
            if (Codec.TryPeekId(message, out MessageId id) && id is MessageId.Snapshot or MessageId.Delta) sizes.Add((w.Ticks, id, message.Length));
        };
        var client = new GameClient(transport, "127.0.0.1", w.Server.Port, "Alice");
        var spawns = new List<(long Tick, ReplicaEntity Entity)>();
        client.Spawned += e => spawns.Add((w.Ticks, e));
        var despawns = new List<long>();
        client.Despawned += _ => despawns.Add(w.Ticks);
        Assert.Equal(FeedPhase.Playing, w.Connect(client));
        Assert.True(client.Replica.TryGet(client.OwnEntity, out ReplicaEntity own));
        int atJoin = client.Replica.Entities.Count();
        int snapshot = sizes.Single(m => m.Id == MessageId.Snapshot).Bytes;
        output.WriteLine($"join: {atJoin} entities in the Snapshot, {snapshot} bytes");
        RealHeadlessWorld.Player alice = w.Adopt("Alice", client);
        w.Run(4 * Second);
        Report("join", w, spawns, sizes, 0);
        Assert.True(Queued(spawns.Where(p => p.Tick == spawns[0].Tick).Select(p => p.Entity), own.State.X, own.State.Y) <= interest.JoinSpawnBudget + 10, $"{atJoin} entities in the Snapshot");

        turf centre = In(w, () => Floors().OrderBy(t => Math.Abs(t.x - 131) + Math.Abs(t.y - 127)).First());
        long from = w.Ticks;
        Put(w, w.MobOf(alice), centre);
        w.Run(4 * Second);
        Report($"teleport to {centre.x},{centre.y}", w, spawns, sizes, from);

        // The doors of the near chunks come at once (with what lies on their tiles); the rest, far doors too, at most SpawnBudget a tick.
        var arrived = spawns.Where(p => p.Tick > from).ToList();
        long first = arrived[0].Tick;
        int cx = (int)centre.x, cy = (int)centre.y;
        Assert.All(arrived.Where(p => IsDoor(p.Entity) && NearDoor(p.Entity, cx, cy)), p => Assert.Equal(first, p.Tick));
        Assert.Contains(arrived, p => IsDoor(p.Entity) && !NearDoor(p.Entity, cx, cy) && p.Tick > first);
        foreach (IGrouping<long, (long Tick, ReplicaEntity Entity)> tick in arrived.GroupBy(p => p.Tick))
            Assert.True(Queued(tick.Select(p => p.Entity), cx, cy) <= interest.SpawnBudget + 10, $"tick +{tick.Key - from}: {Queued(tick.Select(p => p.Entity), cx, cy)} queued entities of {tick.Count()}: "
                + string.Join(", ", tick.GroupBy(p => p.Entity.TypePath).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key} {g.Count()}")));
        Assert.True(arrived.Select(p => p.Tick).Distinct().Count() > 10, "the teleport's spawns are spread over ticks");

        // The arrivals' things leave after their hold, at most DespawnBudget a tick (+ 10: what the world deleted that tick).
        var left = despawns.Where(t => t > from).GroupBy(t => t).ToList();
        output.WriteLine($"teleport: {left.Sum(g => g.Count())} despawns over {left.Count} ticks, at most {left.Max(g => g.Count())} a tick");
        Assert.All(left, g => Assert.True(g.Count() <= interest.DespawnBudget + 10, $"tick +{g.Key - from}: {g.Count()} despawns"));
        Assert.True(left.Sum(g => g.Count()) > interest.DespawnBudget, "the arrivals' despawns are more than a tick's budget");
    }

    private static bool IsDoor(ReplicaEntity e) => e.TypePath.StartsWith("/obj/machinery/door/", StringComparison.Ordinal);

    // A door in the near chunks of the eye at (x, y) (NearChunks 1: the 3 by 3), which skips the queue.
    private static bool NearDoor(ReplicaEntity e, int x, int y) => Math.Abs((e.State.X - 1) / 8 - (x - 1) / 8) <= 1 && Math.Abs((e.State.Y - 1) / 8 - (y - 1) / 8) <= 1;

    // The spawns of a tick that came through the queue: not a mob, a visual or a near door, nor on the tile of a door spawned with them (+ 10
    // in the checks: what the world made that tick, the hosts of visuals, and the other doors, mineral doors and flaps, with their tiles' things).
    private static int Queued(IEnumerable<ReplicaEntity> tick, int x, int y)
    {
        var doorTiles = tick.Where(IsDoor).Select(e => (e.State.X, e.State.Y, e.State.Z)).ToHashSet();
        return tick.Count(e => !e.IsMob() && !e.IsVisual && !(IsDoor(e) && NearDoor(e, x, y)) && (IsDoor(e) || !doorTiles.Contains((e.State.X, e.State.Y, e.State.Z))));
    }

    private void Report(string what, RealHeadlessWorld w, List<(long Tick, ReplicaEntity Entity)> spawns, List<(long Tick, MessageId Id, int Bytes)> sizes, long from)
    {
        var ticks = spawns.Where(p => p.Tick > from && p.Tick <= w.Ticks).GroupBy(p => p.Tick).OrderBy(g => g.Key).ToList();
        var deltas = sizes.Where(m => m.Id == MessageId.Delta && m.Tick > from).ToList();
        output.WriteLine($"{what}: {ticks.Sum(g => g.Count())} spawns over {ticks.Count} ticks, at most {ticks.DefaultIfEmpty().Max(g => g?.Count() ?? 0)} a tick "
            + $"({ticks.FirstOrDefault()?.Count(p => IsDoor(p.Entity)) ?? 0} doors and {ticks.FirstOrDefault()?.Count(p => p.Entity.IsMob()) ?? 0} mobs in the first); "
            + $"Deltas at most {deltas.Max(m => m.Bytes)} bytes, {deltas.Count(m => m.Bytes > 6000)} over 6 KB");
        output.WriteLine("  spawns by tick: " + string.Join(" ", ticks.Select(g => $"{g.Key - from}:{g.Count()}")));
        output.WriteLine("  Delta bytes by tick (over 1.5 KB): " + string.Join(" ", deltas.Where(m => m.Bytes > 1500).Select(m => $"{m.Tick - from}:{m.Bytes}")));
    }

    // ---- P3.7 and P3.8 in the test room (64 by 64, one area; the map's cell x is the room's plus 3: chunk c holds the cells 8c+1 to 8c+8) ----

    private static RealHeadlessWorld Room(InterestOptions interest) => new(config: new ServerConfig
    {
        Seed = 7, RoomWidth = 64, RoomHeight = 64, RoomLayout = TestRoomLayout.Classic, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f,
    }, interest: interest);

    private static bool Open(turf t) => t is turf_open and not turf_open_space && !Dm.truthy(t.density);

    // The open floor nearest the cell (x, y).
    private static turf Near(RealHeadlessWorld w, int x, int y) => In(w, () => Enumerable.Range(0, 4)
        .SelectMany(r => Enumerable.Range(-r, 2 * r + 1).SelectMany(dx => Enumerable.Range(-r, 2 * r + 1).Select(dy => locate(x + dx, y + dy, 1) as turf)))
        .First(t => t != null && Open(t)));

    // A crowbar on every open floor of the rectangle, made before the test looks (a thing the world makes in a tick comes at once).
    private static List<obj_item_crowbar> Crowbars(RealHeadlessWorld w, int x0, int y0, int x1, int y1, int step = 1) => In(w, () =>
    {
        var made = new List<obj_item_crowbar>();
        for (int y = y0; y <= y1; y += step)
            for (int x = x0; x <= x1; x += step)
                if (locate(x, y, 1) is turf t && Open(t))
                    made.Add(Dm.@new<obj_item_crowbar>(t));
        return made;
    });

    private static int Distance2(ReplicaEntity e, turf eye) => (e.State.X - (int)eye.x) * (e.State.X - (int)eye.x) + (e.State.Y - (int)eye.y) * (e.State.Y - (int)eye.y);

    [Fact]
    public void A_burst_larger_than_the_budget_is_spread_over_ticks_nearest_first_and_a_mob_comes_at_once()
    {
        using var w = Room(new InterestOptions { SpawnBudget = 10, FarTiles = 8, FarLeaveTiles = 0 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        Put(w, w.MobOf(alice), Near(w, 8, 8));
        Put(w, w.MobOf(bob), Near(w, 52, 52));
        var crowbars = Crowbars(w, 41, 41, 56, 56).Select(c => c.dm_id).ToHashSet();
        w.Run(3 * Second);
        ReplicaWorld sees = alice.Session.Replica;
        Assert.DoesNotContain(sees.Entities, e => crowbars.Contains(e.Id));

        var spawns = new List<(long Tick, ReplicaEntity Entity)>();
        sees.Spawned += e => spawns.Add((w.Ticks, e));
        turf eye = Near(w, 48, 48);
        Put(w, w.MobOf(alice), eye);
        w.Run(2 * Second);
        var ticks = spawns.Where(p => crowbars.Contains(p.Entity.Id)).GroupBy(p => p.Tick).OrderBy(g => g.Key).ToList();
        output.WriteLine($"{crowbars.Count} crowbars over {ticks.Count} ticks: " + string.Join(" ", ticks.Select(g => g.Count())));
        Assert.Equal(crowbars.Count, ticks.Sum(g => g.Count()));
        Assert.All(ticks, g => Assert.True(g.Count() <= 10, $"{g.Count()} crowbars in a tick"));
        for (int i = 1; i < ticks.Count; i++)
            Assert.True(ticks[i - 1].Max(p => Distance2(p.Entity, eye)) <= ticks[i].Min(p => Distance2(p.Entity, eye)), "nearest first");
        long bobAt = spawns.Single(p => p.Entity.Id == w.MobOf(bob).dm_id).Tick;
        Assert.True(bobAt <= ticks[0].Key, "a mob skips the queue");
        Assert.Equal(FeedPhase.Playing, alice.Client.Phase);
    }

    [Fact]
    public void A_queued_entity_gets_no_change_records_and_leaving_the_set_while_queued_sends_nothing()
    {
        using var w = Room(new InterestOptions { SpawnBudget = 2, FarTiles = 8, FarLeaveTiles = 0 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        Put(w, w.MobOf(alice), Near(w, 8, 8));
        var crowbars = Crowbars(w, 41, 41, 56, 56);
        var ids = crowbars.Select(c => c.dm_id).ToHashSet();
        w.Run(3 * Second);
        ReplicaWorld sees = alice.Session.Replica;
        var spawned = new List<long>();
        var despawned = new List<long>();
        sees.Spawned += e => spawned.Add(e.Id);
        sees.Despawned += e => despawned.Add(e.Id);

        Put(w, w.MobOf(alice), Near(w, 48, 48));
        w.Run(1);
        // The queued ones move a tile: a change record of an entity the client does not hold would end its connection (ReplicaWorld's check);
        // each one's spawn carries where it is then.
        var moved = crowbars.Where(c => !sees.TryGet(c.dm_id, out _)).ToList();
        Assert.True(moved.Count > crowbars.Count / 2);
        Do(w, () =>
        {
            foreach (obj_item_crowbar c in moved)
                Waiting.NoSleep(c.forceMove(locate(((turf)c.loc).x + 1, ((turf)c.loc).y, 1)));
        });
        w.Run(5);
        Assert.Equal(FeedPhase.Playing, alice.Client.Phase);
        foreach (obj_item_crowbar c in moved)
            if (sees.TryGet(c.dm_id, out ReplicaEntity e))
                Assert.Equal(In(w, () => (int)((turf)c.loc).x), e.State.X);
        int held = ids.Count(id => w.Replicator.Holds(Ordinal(w, alice), id)); // the server's: the last ticks' spawns are still on the way
        Assert.InRange(held, 2, 2 * 7); // two a tick

        // Away again: the queued ones just leave the queue (no spawn, no despawn); the held ones leave after the hold.
        Put(w, w.MobOf(alice), Near(w, 8, 8));
        w.Run(3 * Second);
        Assert.Equal(FeedPhase.Playing, alice.Client.Phase);
        Assert.Equal(held, spawned.Count(ids.Contains));
        Assert.Equal(held, despawned.Count(ids.Contains));
        Assert.DoesNotContain(sees.Entities, e => ids.Contains(e.Id));
    }

    [Fact]
    public void The_join_spawns_its_nearest_entities_within_the_budget_and_the_rest_follows()
    {
        var interest = new InterestOptions { JoinSpawnBudget = 40, SpawnBudget = 20 };
        using var w = Room(interest);
        var ids = Crowbars(w, 5, 5, 66, 66, step: 3).Select(c => c.dm_id).ToHashSet();
        w.Run(2);
        var client = new GameClient(w.Network.CreateClient(), "127.0.0.1", w.Server.Port, "Alice");
        Assert.Equal(FeedPhase.Playing, w.Connect(client));
        Assert.True(client.Replica.TryGet(client.OwnEntity, out ReplicaEntity own), "the own mob at once");
        turf at = In(w, () => locate(own.State.X, own.State.Y, own.State.Z) as turf);
        var atJoin = client.Replica.Entities.Where(e => ids.Contains(e.Id)).ToList();
        output.WriteLine($"{client.Replica.Entities.Count()} entities at the join, {atJoin.Count} of {ids.Count} crowbars; the others: "
            + string.Join(", ", client.Replica.Entities.Where(e => !ids.Contains(e.Id)).Select(e => e.TypePath)));
        Assert.True(atJoin.Count <= interest.JoinSpawnBudget);
        Assert.NotEmpty(atJoin);
        // nearest first: no crowbar left for later is nearer than one the Snapshot carried
        int farthest = atJoin.Max(e => Distance2(e, at));
        foreach (long id in ids.Where(id => !atJoin.Any(e => e.Id == id)))
            Assert.True(In(w, () => w.World.Objects.TryGet(id, out object o) && o is atom a && (a.x - at.x) * (a.x - at.x) + (a.y - at.y) * (a.y - at.y) >= farthest));

        RealHeadlessWorld.Player alice = w.Adopt("Alice", client);
        w.Run(ids.Count / interest.SpawnBudget + 5);
        Assert.Equal(ids.Count, alice.Session.Replica.Entities.Count(e => ids.Contains(e.Id)));
        Assert.Equal(FeedPhase.Playing, alice.Client.Phase);
    }

    [Fact]
    public void Walking_to_and_fro_over_a_chunk_border_does_not_send_the_near_column_again()
    {
        using var w = Room(new InterestOptions { FarTiles = 8, FarLeaveTiles = 0 }); // the far square as the near one: the near margin alone
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob aliceMob = w.MobOf(alice);
        // the crowbars in chunk column 1 (cells 9 to 16): near from chunk 2, still near (the leave margin) from chunk 3, gone from chunk 5
        var ids = Crowbars(w, 9, 25, 16, 32).Select(c => c.dm_id).ToHashSet();
        turf west = Near(w, 20, 28), east = Near(w, 28, 28), away = Near(w, 44, 28);
        Put(w, aliceMob, east);
        w.Run(3 * Second);
        ReplicaWorld sees = alice.Session.Replica;
        Assert.DoesNotContain(sees.Entities, e => ids.Contains(e.Id));
        var spawned = new List<long>();
        var despawned = new List<long>();
        sees.Spawned += e => spawned.Add(e.Id);
        sees.Despawned += e => despawned.Add(e.Id);

        for (int i = 0; i < 3; i++)
        {
            Put(w, aliceMob, west);
            w.Run(3 * Second); // past the 2 s hold on each side
            Put(w, aliceMob, east);
            w.Run(3 * Second);
            Assert.Equal(ids.Count, sees.Entities.Count(e => ids.Contains(e.Id)));
        }
        Assert.Equal(ids.Count, spawned.Count(ids.Contains));
        Assert.DoesNotContain(despawned, ids.Contains);

        Put(w, aliceMob, away);
        w.Run(3 * Second);
        Assert.DoesNotContain(sees.Entities, e => ids.Contains(e.Id));
    }
}

using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The ghosts (D151) through the real server on MetaStation with headless clients. Alice dies beside the Aux base's west wall
/// (42,155) and presses a movement key: tg's client/Move ghostizes her, and her client gets the ghost as its own entity in its
/// private block's observer layer (protocol 22). Bob, alive, never receives the ghost's entity in any tick; the ghost flies west
/// through the wall; then Bob dies and ghosts too, and from then on his client has Alice's ghost and hers has his. Alice
/// clicks her HUD's Re-enter Corpse button and is back in her body, and her ghost is gone from Bob's view. The second test runs
/// the steps twice from a fresh boot and compares the state hashes.
/// </summary>
public class GhostScenarioTests
{
    private const int Second = 20;
    private const int WallX = 42, WallY = 155;
    private const double West = Math.PI / 2; // Godot's yaw: 0 looks north, +90 degrees west

    private readonly ITestOutputHelper _output;

    public GhostScenarioTests(ITestOutputHelper output) => _output = output;

    private static RealHeadlessWorld Boot() => new(new LoopbackOptions { Seed = 51, TimeoutSteps = 8000 },
        MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

    private static void Kill(RealHeadlessWorld w, mob_living body)
    {
        using (w.World.Enter())
            Waiting.NoSleep(body.death());
    }

    // Holds the forward key along the yaw for some ticks, then lets go (the keys a player holds; tg's keyLoop moves a ghost).
    private static void Hold(RealHeadlessWorld w, RealHeadlessWorld.Player p, double yaw, int ticks, Action<long> afterTick = null)
    {
        p.Yaw = yaw;
        p.Keys = MoveKeys.Forward;
        w.Run(ticks, afterTick);
        p.Keys = MoveKeys.None;
        w.Run(3, afterTick);
    }

    private sealed record Run(RealHeadlessWorld.Player Alice, RealHeadlessWorld.Player Bob, mob_living AliceBody, mob_living BobBody,
        long AliceGhost, long BobGhost, int BobSawAliceGhost, ulong HashAfterReenter);

    private static Run Scenario(RealHeadlessWorld w, ITestOutputHelper output)
    {
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living aliceBody = w.MobOf(alice), bobBody = w.MobOf(bob);
        using (w.World.Enter())
        {
            Waiting.NoSleep(aliceBody.forceMove(At(WallX + 1, WallY)));
            Waiting.NoSleep(bobBody.forceMove(At(WallX + 3, WallY)));
        }
        w.Run(Second);

        // Alice dies and presses a key: tg's client/Move ghostizes her (mob_movement.dm:63-65).
        Kill(w, aliceBody);
        w.Run(5);
        Hold(w, alice, West, 1);
        var aliceGhost = MobOf(w, alice) as mob_dead_observer;
        Assert.NotNull(aliceGhost);
        long ghostId = In(w, () => aliceGhost.dm_id);
        Assert.Equal(ghostId, alice.Client.OwnEntity);
        int bobSaw = 0;
        int livingTicks = 0;
        void Watch(long tick)
        {
            // only while Bob is alive in his body (living, see_invisible 25); once he ghosts he sees ghosts, as in tg
            if (In(w, () => MobOf(w, bob) is mob_living living && living.stat != Defines.DEAD))
            {
                livingTicks++;
                if (bob.Session.Replica.TryGet(ghostId, out ReplicaEntity _)) bobSaw++;
            }
        }
        w.Run(5, Watch);
        Assert.True(alice.Session.Replica.TryGet(ghostId, out ReplicaEntity seen), "the ghost's own client has the ghost");
        Assert.Equal("/mob/dead/observer", seen.TypePath);
        Assert.True(alice.Session.Ghosting, "Alice's client drives a ghost (no capsule)");
        Assert.Equal(new MapCell(WallX + 1, WallY, 1), alice.Session.Replica.OwnBody); // DV54: the client marks where the body lies
        Assert.Null(bob.Session.Replica.OwnBody);
        Assert.Contains(alice.Session.Replica.Screen.Values, e => In(w, () => w.World.Objects.TryGet(e.Id, out object o) && o is atom_movable_screen_ghost_reenter_corpse));

        // The ghost flies west through the wall (abstract_move, observer.dm:328).
        Hold(w, alice, West, 8, Watch);
        w.Run(3, Watch); // the key's release reaches the server a tick or two after it left the client
        float ghostX = In(w, () => aliceGhost.x);
        output.WriteLine($"the ghost: from {WallX + 1},{WallY} to {ghostX},{In(w, () => aliceGhost.y)}; the wall at {WallX}: {In(w, () => DmTypes.PathOf(At(WallX, WallY).GetType()))}");
        Assert.True(ghostX < WallX, "the ghost passed the wall");
        Assert.True(alice.Session.Replica.TryGet(ghostId, out seen) && seen.State.X == (int)ghostX, "the ghost's client follows it");

        Assert.True(livingTicks >= 15, "Bob was watched alive for " + livingTicks + " ticks");

        // Bob dies and ghosts: now each ghost's client has the other ghost.
        Kill(w, bobBody);
        w.Run(5, Watch);
        Hold(w, bob, 0, 1, Watch); // north
        var bobGhost = MobOf(w, bob) as mob_dead_observer;
        Assert.NotNull(bobGhost);
        long bobGhostId = In(w, () => bobGhost.dm_id);
        w.Run(3);
        Assert.True(bob.Session.Replica.TryGet(ghostId, out _), "a ghost's client has the other ghost");
        Assert.True(alice.Session.Replica.TryGet(bobGhostId, out _), "the other ghost's client has the first one");

        // Alice clicks her HUD's Re-enter Corpse button (screen_objects/ghost.dm:31).
        long button = alice.Session.Replica.Screen.Values.Single(e => In(w, () => w.World.Objects.TryGet(e.Id, out object o) && o is atom_movable_screen_ghost_reenter_corpse)).Id;
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Same(aliceBody, MobOf(w, alice));
        Assert.Equal(In(w, () => aliceBody.dm_id), alice.Client.OwnEntity);
        Assert.True(In(w, () => QDELETED(aliceGhost)), "the empty ghost is deleted");
        Assert.False(bob.Session.Replica.TryGet(ghostId, out _), "the deleted ghost left the other ghost's view");
        Assert.False(alice.Session.Ghosting, "Alice drives her capsule again");
        Assert.Null(alice.Session.Replica.OwnBody); // back in the body: no marker
        Assert.Equal(new MapCell(WallX + 3, WallY, 1), bob.Session.Replica.OwnBody);
        return new Run(alice, bob, aliceBody, bobBody, ghostId, bobGhostId, bobSaw, w.World.ComputeStateHash());
    }

    [MetaServerFact]
    public void A_dead_player_ghosts_flies_through_a_wall_unseen_by_the_living_and_reenters_the_body_from_the_hud()
    {
        using RealHeadlessWorld w = Boot();
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        Run run = Scenario(w, _output);

        Assert.Equal(0, run.BobSawAliceGhost); // the living player never received the ghost (BYOND's see_invisible, D151)
        Assert.True(w.Replicator.LayerEntityCount >= 1, "Bob's ghost is in the observer layer");
        Assert.True(runtimes.Count == 0, string.Join("\n", runtimes.Take(3)));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void The_ghost_scenario_run_twice_from_a_fresh_boot_ends_with_the_same_state_hash()
    {
        (ulong Hash, long Tick) Once()
        {
            using RealHeadlessWorld w = Boot();
            Run run = Scenario(w, _output);
            return (run.HashAfterReenter, w.Ticks);
        }

        var first = Once();
        var second = Once();
        _output.WriteLine($"first run: {first.Hash:x16} at tick {first.Tick}; second: {second.Hash:x16} at tick {second.Tick}");
        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.Hash, second.Hash);
    }
}

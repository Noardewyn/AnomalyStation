using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.M1Client.T1.Dirs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: determinism of the client layer (D31: the same input script twice gives the same world state, tick by tick) and the
/// deviations of docs/ai/DEVIATIONS.md that the client layer meets (DV1 runtimes unwind to a boundary, DV3 virtual tick usage, DV7
/// no hard delete, DV14 default movement hotkeys) plus the one P1 reported (client/Del runs later in the tick).
/// </summary>
public class DeterminismAndDeviationTests
{
    private static float N(object v) => Dm.num(v);

    // ---- determinism ----

    private sealed record ScriptResult(List<ulong> Hashes, string Final, List<string> HostEvents);

    // Results of scripts the tests below only compare against (a script takes a second: it hashes the world every tick); the first
    // test runs its two scripts fresh. Test-side only, never read by the simulation.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(ulong, bool), ScriptResult> s_runs = new();

    private static ScriptResult Cached(ulong seed, bool extraPress = false) => s_runs.GetOrAdd((seed, extraPress), k => RunScript(k.Item1, k.Item2));

    /// <summary>
    /// A script with four players: held and tapped movement, a diagonal, a keysend flood kick, a disconnect and reconnect, a key
    /// reassigned to another mob, a release. The hash of the whole world is taken after every tick.
    /// </summary>
    private static ScriptResult RunScript(ulong seed, bool extraPress = false)
    {
        var room = new T1Room(seed);
        var hashes = new List<ulong>();
        void Tick(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                room.Step();
                hashes.Add(room.World.ComputeStateHash());
            }
        }

        mob alpha = room.NewMob(4, 4), bravo = room.NewMob(9, 9), charlie = room.NewMob(12, 3), delta = room.NewMob(7, 12);
        client ca = room.Join(alpha, "Alpha").Client;
        hashes.Add(room.World.ComputeStateHash());
        client cb = room.Join(bravo, "Bravo").Client;
        hashes.Add(room.World.ComputeStateHash());
        client cc = room.Join(charlie, "Charlie").Client;
        hashes.Add(room.World.ComputeStateHash());

        room.Act(() => { room.Down(ca, "W"); room.Down(cb, "W"); room.Down(cb, "D"); room.Down(cc, "D"); room.Up(cc, "D"); });
        Tick(4);
        if (extraPress)
            room.Act(() => room.Down(ca, "Space"));
        room.Act(() =>
        {
            for (int i = 0; i < 52; i++)
                room.Down(cb, "Space");
        });
        Tick(2);
        room.Act(() => ClientSeam.Disconnect(ca));
        Tick(2);
        ClientConnection again = null;
        room.Act(() => again = ClientSeam.Connect("Alpha"));
        Tick(3);
        room.Act(() => delta.key = "Charlie");
        Tick(4);
        room.Act(() => ClientSeam.SetHeldDirections(cc, South | West));
        Tick(3);
        room.Act(() => ClientSeam.SetHeldDirections(again.Client, 0));
        Tick(3);
        room.AssertNoRuntimes();
        string final = room.In(() => string.Join(" ", new[] { alpha, bravo, charlie, delta }.Select(m => T1Room.Where(m))));
        return new ScriptResult(hashes, final, room.Host.Events.Select(e => System.Text.RegularExpressions.Regex.Replace(e, @"\d+", "#")).ToList());
    }

    [Fact]
    public void TheSameInputScriptGivesTheSameWorldHashAtEveryTick()
    {
        ScriptResult one = RunScript(21);
        ScriptResult two = RunScript(21);
        Assert.Equal(one.Hashes, two.Hashes);
        Assert.Equal(one.Final, two.Final);
        Assert.Equal(one.HostEvents, two.HostEvents);
    }

    [Fact]
    public void TheScriptActuallyMovesKicksReconnectsAndSwitches()
    {
        // so that the hash comparison above compares something: Alpha walked and came back, Bravo was kicked, Charlie's mob switched
        ScriptResult result = Cached(21);
        Assert.NotEqual("4,4 9,9 12,3 7,12", result.Final);
        Assert.Contains(result.HostEvents, e => e == "deleted Bravo");
        Assert.Contains(result.HostEvents, e => e.StartsWith("mob Charlie # -> #", StringComparison.Ordinal));
        Assert.Equal(2, result.HostEvents.Count(e => e == "mob Alpha null -> #"));
    }

    [Fact]
    public void AnotherInputGivesAnotherHashSoTheHashSeesTheClientLayer()
    {
        // one extra key press of Alpha (tick 8 of the script, hash index 7): the held-keys list of its client differs until Alpha's
        // client is deleted by the disconnect two ticks later, and the hash of those ticks tells
        ScriptResult one = Cached(21);
        ScriptResult other = Cached(21, extraPress: true);
        Assert.NotEqual(one.Hashes[7], other.Hashes[7]);
        Assert.NotEqual(one.Hashes[8], other.Hashes[8]);
        Assert.Equal(one.Hashes.Take(7), other.Hashes.Take(7));
    }

    [Fact]
    public void AnotherSeedGivesAnotherHash()
    {
        Assert.NotEqual(Cached(21).Hashes[^1], Cached(22).Hashes[^1]);
    }

    [Fact]
    public void AClientsConnectionLeavesTheWorldHashDifferentFromAWorldWithoutIt()
    {
        var withClient = new T1Room(5);
        var without = new T1Room(5);
        without.NewMob(8, 8);
        withClient.JoinNew("Tester");
        withClient.Step(3);
        without.Step(4);
        Assert.NotEqual(withClient.World.ComputeStateHash(), without.World.ComputeStateHash());
    }

    // ---- DV1: a runtime unwinds to the nearest boundary ----

    [Fact]
    public void DV1ARuntimeInAMoveSignalHandlerIsReportedAndTheMoveGoesOn()
    {
        // signal handler calls in _SendSignal are an isolation boundary (DV1): DM ends only the failing handler and SEND_SIGNAL carries on
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(avatar);
            l.ThrowOnPreMove = true;
            return l;
        });
        room.Act(() => room.Down(c, "W"));
        room.Step(3);
        room.In(() => Assert.Equal("8,10", T1Room.Where(avatar))); // two fires, two steps, whatever the handler threw
        Assert.Equal(2, room.Room.Runtimes.Count);
        Assert.All(room.Room.Runtimes, e => Assert.Contains("t1 boom in a signal handler", e.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void DV1ARuntimeInAMobsKeyLoopDoesNotStopTheOtherPlayersOfTheSameLoop()
    {
        // SSinput's loop over the keyloop list: one mob's keyLoop that throws is a runtime at a boundary; the next mob still moves.
        // The throwing handler sits on the first mob only (the PRE_NON_LIVING_MOVE signal is per mob)
        var room = new T1Room();
        var (a, ca) = room.JoinNew("Alpha", 4, 4);
        var (b, cb) = room.JoinNew("Bravo", 9, 9);
        room.In(() =>
        {
            var l = Dm.@new<datum_test_m1t1_listener>();
            l.WatchMove(a);
            l.ThrowOnPreMove = true;
        });
        room.Act(() => { room.Down(ca, "W"); room.Down(cb, "W"); });
        room.Step(2);
        room.In(() =>
        {
            Assert.Equal("4,5", T1Room.Where(a));
            Assert.Equal("9,10", T1Room.Where(b));
        });
    }

    // ---- DV3: tick usage is virtual and 0 ----

    [Fact]
    public void DV3TickUsageIsZeroSoNoVerbIsQueuedAtTheDefaultThresholds()
    {
        var room = new T1Room();
        room.In(() =>
        {
            Assert.Equal(0f, N(TICK_USAGE()));
            Assert.True(VERB_DEFAULT_QUEUE_THRESHOLD > 0 && VERB_HIGH_PRIORITY_QUEUE_THRESHOLD > 0 && VERB_OVERTIME_QUEUE_THRESHOLD > 0);
        });
    }

    // ---- DV7: no hard delete ----

    [Fact]
    public void DV7ADeletedClientIsNotKeptByTheWorldAndStaysMarkedDeleted()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        long id = room.In(() => c.dm_id);
        room.Act(() => qdel(c));
        room.Step();
        room.In(() =>
        {
            Assert.False(World.Current.Objects.TryGet(id, out _)); // unregistered (hard delete)
            Assert.True(QDELETED(c)); // and a stale reference reads as deleted
            qdel(c); // a second qdel is quiet
        });
        room.AssertNoRuntimes();
    }

    // ---- DV14: default movement hotkeys ----

    [Fact]
    public void DV14TheMovementKeysAreTgsDefaultsUntilThePlayersOwnBindingsExist()
    {
        // client_procs.dm:1036-1038: with no preferences tg's update_special_keybinds returns and the table stays empty; the port
        // fills tg's default bindings (W/North, S/South, A/West, D/East)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            Assert.Equal(NORTH, c.movement_keys["W"]);
            Assert.Equal(NORTH, c.movement_keys["North"]);
            Assert.Equal(SOUTH, c.movement_keys["S"]);
            Assert.Equal(SOUTH, c.movement_keys["South"]);
            Assert.Equal(EAST, c.movement_keys["D"]);
            Assert.Equal(EAST, c.movement_keys["East"]);
            Assert.Equal(WEST, c.movement_keys["A"]);
            Assert.Equal(WEST, c.movement_keys["West"]);
            Assert.Equal(0f, N(c.intended_direction)); // calculate_move_dir ran at the end of update_special_keybinds
        });
    }

    [Fact]
    public void DV14TheKeyTableIsFilledByMacrosSetupAfterLoginNotBeforeAndAClientWithoutItHasNoMovementKeys()
    {
        // set_macros (setup.dm:22-37) fills movement_keys through update_special_keybinds and client/New calls it after BYOND's
        // part (client_procs.dm:421); a client made by the engine but not yet through New has an empty table
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        room.In(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            client made = client.dm_engine_create("Some Player", "127.0.0.1", null);
            Assert.Equal(0f, made.movement_keys.len);
        });
    }

    // ---- the client count and the Master's tick rate (client/New and Destroy: Master.UpdateTickRate, master.dm:1029-1036) ----

    [Fact]
    public void TheMasterSlowsToTheHighPopRateAboveSixtyFiveClientsAndBackBelowSixty()
    {
        // processing = base (1) below 60 clients, high_pop (1.1) above 65, unchanged in between (the hysteresis)
        var room = new T1Room();
        var clients = new List<client>();
        void Join(int i) => clients.Add(room.JoinNew("P" + i, 2 + i % 14, 2 + i / 14).Client);
        float Processing() => room.In(() => N(Master.processing));

        for (int i = 0; i < 59; i++)
            Join(i);
        Assert.Equal(1f, Processing());
        for (int i = 59; i < 65; i++)
            Join(i);
        Assert.Equal(65, clients.Count);
        Assert.Equal(1f, Processing());
        Join(65); // 66 clients
        Assert.Equal(1.1f, Processing(), 4);
        for (int i = 65; i >= 62; i--)
        {
            var leaving = clients[i];
            room.Act(() => ClientSeam.Disconnect(leaving));
            room.Step();
        }
        room.In(() => Assert.Equal(62f, GLOB.clients.len));
        Assert.Equal(1.1f, Processing(), 4); // 62: neither below 60 nor above 65
        for (int i = 61; i >= 58; i--)
        {
            var leaving = clients[i];
            room.Act(() => ClientSeam.Disconnect(leaving));
            room.Step();
        }
        room.In(() => Assert.Equal(58f, GLOB.clients.len));
        Assert.Equal(1f, Processing());
        room.AssertNoRuntimes();
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.P1;

/// <summary>
/// The M1 client layer end to end in a booted world (package P1's scenario): a plain <c>/mob</c> in the toy room, a client
/// attached by key, held NORTH that moves it through tg's own chain (keyDown, SSinput's fire, keyLoop, client/Move,
/// /atom/movable/Move) with the movement signals in tg's order (move-chain.md 3a), the release that stops it, the client
/// deleted (Logout ran, the mob keeps its key) and a new client of the same key that attaches to the same mob again.
/// </summary>
public class HeldNorthScenarioTests
{
    [Fact]
    public void HeldNorthMovesThePlainMobOneTilePerTickThroughSSinputUntilReleasedThenTheClientLeavesAndComesBack()
    {
        var room = new ClientRoom();
        mob avatar = room.NewMob(5, 5);
        ClientConnection first = room.Join(avatar, "Player One");
        Assert.True(first.Accepted);
        client player = first.Client;

        datum_test_m1p1_listener listener = room.In(() =>
        {
            var l = Dm.@new<datum_test_m1p1_listener>();
            l.Watch(avatar);
            l.WatchTurf(locate(5, 5, 1));
            l.WatchTurf(locate(5, 6, 1));
            return l;
        });

        // The InputFrame of the command phase: NORTH held (tg's keyDown verb with the North hotkey).
        room.Command(() => ClientSeam.SetHeldDirections(player, (int)NORTH));
        room.Step();
        long pressed = room.Tick;
        room.In(() =>
        {
            Assert.True(Dm.eq(player.intended_direction, NORTH));
            Assert.True(Dm.eq(player.next_move_dir_add, NORTH));
            Assert.Same(locate(5, 5, 1), avatar.loc); // SSinput fired before the command phase of this tick
        });

        // The next tick: SSinput (a ticker, fired by the Master at the start of the tick) runs keyLoop, which moves the mob.
        room.Step();
        room.In(() => Assert.Same(locate(5, 6, 1), avatar.loc));
        Assert.Equal(new[]
        {
            "client_pre_move 5,6 1",   // client/Move, the non-living branch (mob_movement.dm:58)
            "attempted_move 5,6 1",    // /atom/movable/Move (atoms_movable.dm:691)
            "dir_change 2 1",          // setDir before Exit (the mob starts facing SOUTH)
            "exit 5,5",                // loc.Exit (atoms_movable.dm:713)
            "pre_move 5,6",            // after Enter (atoms_movable.dm:730)
            "exited 5,5",              // oldloc.Exited
            "entered 5,6",             // newloc.Entered
            "moved 5,5 1",             // Moved (RESOLVE_ACTIVE_MOVEMENT)
        }, listener.Trace);
        room.In(() =>
        {
            Assert.True(Dm.eq(player.next_move_dir_add, NONE)); // client/Move clears the tap memory
            Assert.True(player.move_delay > 0);
        });

        // Held: one tile per tick (tg's plain-mob pace: move_delay = world.time + world.tick_lag, mob_movement.dm:52).
        room.Step(3);
        room.In(() => Assert.Same(locate(5, 9, 1), avatar.loc));
        Assert.Equal(pressed + 4, room.Tick);

        // Released: keyUp in the command phase; the mob stops.
        room.Command(() => ClientSeam.SetHeldDirections(player, 0));
        room.Step();
        turf stopped = room.In(() => avatar.loc as turf);
        room.Step(3);
        room.In(() =>
        {
            Assert.Same(stopped, avatar.loc);
            Assert.True(Dm.eq(player.intended_direction, NONE));
            Assert.Equal(0f, length(player.keys_held));
        });

        // The client is deleted (the transport ended): Logout ran, the mob keeps its key and has no client.
        room.Command(() => ClientSeam.Disconnect(player));
        room.Step();
        room.In(() =>
        {
            Assert.Null(avatar.client);
            Assert.Equal("Player One", avatar.key);
            Assert.Equal("playerone", avatar.ckey);
            Assert.Null(avatar.canon_client);
            Assert.False(Dm.@in(avatar, GLOB.player_list)); // remove_from_player_list in Logout (logout.dm:5)
            Assert.False(Dm.@in(avatar, GLOB.keyloop_list));
            Assert.False(Dm.@in(player, GLOB.clients));
            Assert.Null(GLOB.directory["playerone"]);
            Assert.True(player.dm_gone);
        });
        Assert.Contains("deleted Player One", room.Host.Events);

        // A new client of the same key attaches to the same mob (BYOND's client/New finds the mob by its key).
        ClientConnection again = null;
        room.Command(() => again = ClientSeam.Connect("Player One"));
        room.Step();
        Assert.True(again.Accepted);
        Assert.Same(avatar, again.Mob);
        Assert.NotSame(player, again.Client);
        room.In(() =>
        {
            Assert.Same(again.Client, avatar.client);
            Assert.Same(avatar, again.Client.mob);
            Assert.True(Dm.@in(avatar, GLOB.keyloop_list));
            // the persistent client of the key survived the disconnect (persistent_client.dm, client_procs.dm:265-267)
            Assert.Same(avatar.persistent_client, again.Client.persistent_client);
        });

        // It moves again with a held key.
        room.Command(() => ClientSeam.SetHeldDirections(again.Client, (int)EAST));
        room.Step(2);
        room.In(() => Assert.Same(locate(stopped.x + 1, stopped.y, 1), avatar.loc));
        room.AssertNoRuntimes();
    }
}

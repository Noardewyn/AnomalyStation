using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.P1;

/// <summary>
/// Smoke tests of package P1 (the client layer, M1): the translation compiles and runs on a booted world with
/// <c>NotTranslatedThrows</c> on, through the host entry points of the client seam. The DM-behaviour tests of the same code
/// are T1's (<c>tests/Anomaly.Core.Tests/Tg/M1Client/T1/</c>).
/// </summary>
public class ClientLayerSmokeTests
{
    [Fact]
    public void ConnectRunsClientNewAndLoginAndFillsTgsListsAndTheHostIsTold()
    {
        var room = new ClientRoom();
        mob avatar = room.NewMob(5, 5);
        ClientConnection connection = room.Join(avatar, "Some Player");
        Assert.True(connection.Accepted);
        client player = connection.Client;
        Assert.Same(avatar, connection.Mob);
        room.In(() =>
        {
            // BYOND's vars of the connection (ClientSeam.Connect)
            Assert.Equal("Some Player", player.key);
            Assert.Equal("someplayer", player.ckey);
            Assert.Equal("seeker", player.connection);
            Assert.Equal(516f, player.byond_version);
            Assert.Equal(1685f, player.byond_build);
            // client/New (client_procs.dm:262-271)
            Assert.True(Dm.@in(player, GLOB.clients));
            Assert.Same(player, GLOB.directory["someplayer"]);
            Assert.Same(player.persistent_client, GLOB.persistent_clients_by_ckey["someplayer"]);
            Assert.Same(player, player.persistent_client.client);
            Assert.Equal(516f, Dm.num(player.persistent_client.byond_version));
            Assert.True(Dm.eq(player.fully_created, TRUE));
            Assert.NotNull(player.connection_time);
            // BYOND's client/New: attached to the mob of the key (the symmetry of /client/var/mob)
            Assert.Same(avatar, player.mob);
            Assert.Same(player, avatar.client);
            Assert.Equal(player.key, avatar.key);
            Assert.Same(avatar, player.eye);
            // mob/Login (login.dm:33-36, :61, :84, :119)
            Assert.Same(player, avatar.canon_client);
            Assert.Same(avatar, player.persistent_client.mob);
            Assert.Same(player.persistent_client, avatar.persistent_client);
            Assert.True(Dm.@in(avatar, GLOB.player_list));
            Assert.True(Dm.@in(avatar, GLOB.alive_player_list));
            Assert.True(Dm.@in(avatar, GLOB.keyloop_list));
            Assert.Same(avatar, player.statobj);
            Assert.Equal(MOB_PERSPECTIVE, player.perspective);
            Assert.True(Dm.eq(avatar.next_move, 1));
            Assert.Equal("127.0.0.1", avatar.lastKnownIP);
            Assert.True(Dm.truthy(player.persistent_client.played_names.Find(Dm.text("[]", avatar.name))));
            // client_procs.dm:292-300: the preferences datum (a deferred leaf until M10 slice F translated the preferences)
            Assert.NotNull(player.prefs);
            Assert.Same(player, player.prefs.parent);
            Assert.DoesNotContain("code/modules/client/client_procs.dm:292", DmWorldState.Current.DeferredSites);
            // login.dm:48-58: the mob's HUD is made and shown (M2 integration; it was the deferred leaf login.dm:49)
            Assert.NotNull(avatar.hud_used);
            Assert.Same(avatar, avatar.hud_used.mymob);
        });
        Assert.Equal(new[] { "mob Some Player null -> " + RecordingHost.Id(avatar) }, room.Host.Events);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SSinputAndSSverbManagerAreLiveSubsystemsAndAClientGetsTgsDefaultMovementKeys()
    {
        var room = new ClientRoom();
        room.In(() =>
        {
            Assert.True(Dm.truthy(SSinput.initialized));
            Assert.Equal(5f, SSinput.macro_set.len); // setup_default_macro_sets (input.dm:35-41)
            Assert.True(SSinput.times_fired > 0);
            Assert.True(SSverb_manager.times_fired > 0);
            Assert.Equal(SS_TICKER, SSinput.ss_flags);
        });
        long fired = room.In(() => (long)SSinput.times_fired);
        room.Step();
        room.In(() => Assert.Equal(fired + 1, (long)SSinput.times_fired)); // a ticker fires every tick

        mob avatar = room.NewMob(5, 5);
        client player = room.Join(avatar, "Mover").Client;
        room.In(() =>
        {
            // set_macros -> update_special_keybinds (client_procs.dm:1036, setup.dm:36): tg's default movement hotkeys
            Assert.Equal(8f, player.movement_keys.len);
            Assert.True(Dm.eq(player.movement_keys["W"], NORTH));
            Assert.True(Dm.eq(player.movement_keys["North"], NORTH));
            Assert.True(Dm.eq(player.movement_keys["S"], SOUTH));
            Assert.True(Dm.eq(player.movement_keys["A"], WEST));
            Assert.True(Dm.eq(player.movement_keys["D"], EAST));
            Assert.True(Dm.eq(player.movement_keys["East"], EAST));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void OpposedKeysCancelAndADiagonalIsSplitByTgsMove()
    {
        var room = new ClientRoom();
        mob avatar = room.NewMob(8, 8);
        client player = room.Join(avatar, "Mover").Client;
        room.Command(() => ClientSeam.SetHeldDirections(player, (int)(NORTH + SOUTH))); // keyLoop drops both (bindings_atom.dm:22-23)
        room.Step(3);
        room.In(() => Assert.Same(locate(8, 8, 1), avatar.loc));
        room.Command(() => ClientSeam.SetHeldDirections(player, (int)(NORTH + EAST)));
        room.Step(2);
        room.In(() => Assert.Same(locate(9, 9, 1), avatar.loc)); // one diagonal step per tick
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AssigningAMobsKeyMovesTheConnectedClientToIt()
    {
        var room = new ClientRoom();
        mob first = room.NewMob(5, 5);
        mob second = room.NewMob(7, 7);
        client player = room.Join(first, "Body Swapper").Client;
        room.Command(() => second.key = "Body Swapper");
        room.Step();
        room.In(() =>
        {
            Assert.Same(second, player.mob);
            Assert.Same(player, second.client);
            Assert.Null(first.client);
            Assert.Null(first.key); // "Any other mob with the same key will lose it" (BYOND ref /mob/var/key)
            Assert.False(Dm.@in(first, GLOB.player_list)); // Logout on the old mob
            Assert.True(Dm.@in(second, GLOB.player_list)); // Login on the new one
            Assert.Same(player, second.canon_client);
            Assert.Null(first.canon_client);
        });
        Assert.Equal(new[]
        {
            "mob Body Swapper null -> " + RecordingHost.Id(first),
            "mob Body Swapper " + RecordingHost.Id(first) + " -> " + RecordingHost.Id(second),
        }, room.Host.Events);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AKeyGivenToAMobWithoutAPlayerIsTakenFromAnyOtherMob()
    {
        var room = new ClientRoom();
        mob a = room.NewMob(5, 5);
        mob b = room.NewMob(6, 6);
        room.In(() =>
        {
            a.key = "Somebody";
            Assert.Equal("somebody", a.ckey);
            b.ckey = "somebody";
            Assert.Null(a.key);
            Assert.Null(a.ckey);
            Assert.Equal("somebody", b.ckey);
            a.key = "@Admin"; // tg's admin ghosts read the @ back from the ckey (__DEFINES/client.dm:8)
            Assert.Equal("@admin", a.ckey);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void QdelOfAClientDisconnectsThePlayerAndTheMobKeepsItsKey()
    {
        var room = new ClientRoom();
        mob avatar = room.NewMob(5, 5);
        client player = room.Join(avatar, "Kicked").Client;
        room.Command(() => qdel(player)); // the kick of tg's admin topic "boot2" (topic.dm:233) and of the flood check
        room.Step();
        room.In(() =>
        {
            Assert.True(player.dm_gone);
            Assert.Null(avatar.client);
            Assert.Equal("Kicked", avatar.key);
            Assert.False(Dm.@in(player, GLOB.clients));
            Assert.False(Dm.@in(avatar, GLOB.player_list));
            Assert.Null(player.persistent_client);
            Assert.False(World.Current.Objects.TryGet(player.dm_id, out _));
        });
        Assert.Equal(new[] { player }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TgsKeysendFloodCheckKicksAPlayerWhoTripsItTwice()
    {
        var room = new ClientRoom();
        mob avatar = room.NewMob(5, 5);
        client player = room.Join(avatar, "Flooder").Client;
        int keyDowns = 0;
        room.Command(() =>
        {
            // 52 key presses in one tick: the 51st trips the check (cache >= MAX_KEYPRESS_AUTOKICK after the first press
            // reset the count, bindings_client.dm:12-22), the 52nd kicks (:23-28); nothing more reaches the deleted client.
            for (int i = 0; i < 60; i++)
            {
                ClientSeam.SetHeldDirections(player, (int)NORTH);
                if (player.gc_destroyed == null)
                    keyDowns++;
                ClientSeam.SetHeldDirections(player, 0);
            }
        });
        room.Step();
        Assert.Equal(51, keyDowns);
        Assert.Equal(new[] { player }, room.Host.Deleted);
        room.In(() =>
        {
            Assert.True(player.dm_gone);
            Assert.Null(avatar.client);
            Assert.Contains(Dm.each(GLOB.admin_activities), a => Dm.str(a).Contains("autokicked for flooding keysends", StringComparison.Ordinal));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADisconnectWhileLoginSleepsLeavesTheClientGoneAndEndsClientNewAsBYONDDoes()
    {
        var room = new ClientRoom();
        mob_test_m1p1_slow_login avatar = room.In(() => Dm.@new<mob_test_m1p1_slow_login>(locate(5, 5, 1)));
        ClientConnection connection = null;
        room.Command(() =>
        {
            ClientSeam.KeyMob(avatar, "Leaver");
            connection = ClientSeam.Connect("Leaver");
            Assert.False(connection.Done); // Login sleeps
            ClientSeam.Disconnect(connection.Client); // the player drops before Login returns (login.dm:2-5)
        });
        room.Step();
        room.Step();
        Assert.True(connection.Done);
        Assert.False(connection.Accepted);
        Assert.Null(connection.Error);
        room.In(() =>
        {
            Assert.True(connection.Client.dm_gone);
            Assert.Null(avatar.client);
            Assert.Null(avatar.canon_client); // tg's Login returned at `if(!client) return FALSE` (login.dm:30)
            Assert.Equal("Leaver", avatar.key);
            Assert.False(Dm.@in(connection.Client, GLOB.clients));
            Assert.False(Dm.@in(avatar, GLOB.player_list));
        });
        Assert.Equal(new[] { connection.Client }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ClientNewAndLoginRunWithUsrTheMobOfThePlayersKey()
    {
        var room = new ClientRoom();
        mob_test_m1p1_usr_login avatar = room.In(() => Dm.@new<mob_test_m1p1_usr_login>(locate(5, 5, 1)));
        Assert.True(room.Join(avatar, "Who").Accepted);
        Assert.Same(avatar, avatar.login_usr);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyNameOfAClientAndOfItsKeyNameTheMobAndWorldDelShutsDownQuietly()
    {
        var room = new ClientRoom();
        mob avatar = room.NewMob(5, 5);
        client player = room.Join(avatar, "Named").Client;
        room.In(() =>
        {
            avatar.real_name = "John Doe";
            Assert.Equal("Named/(John Doe)", key_name(player));
            Assert.Equal("Named/(John Doe)", key_name("Named"));
            Assert.Equal("Named/(John Doe)", key_name(avatar));
            world.Del();
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheSameJoinAndWalkGivesTheSameStateHashTwice()
    {
        static ulong Run()
        {
            var room = new ClientRoom(seed: 7);
            mob avatar = room.NewMob(5, 5);
            client player = room.Join(avatar, "Twice").Client;
            room.Command(() => ClientSeam.SetHeldDirections(player, (int)NORTH));
            room.Step(4);
            room.Command(() => ClientSeam.SetHeldDirections(player, (int)WEST));
            room.Step(3);
            room.Command(() => ClientSeam.Disconnect(player));
            room.Step(2);
            room.AssertNoRuntimes();
            return room.World.ComputeStateHash();
        }

        Assert.Equal(Run(), Run());
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: <c>/mob/Login</c> and <c>/mob/Logout</c> (code/modules/mob/login.dm, logout.dm), <c>become_uncliented</c>
/// (mob.dm:1551-1559), <c>client/Del</c> and <c>client/Destroy</c> (client_procs.dm:604-662), the signals around them, and the
/// player lists they maintain (mob_lists.dm:97-120). Written from the DM and the BYOND reference (<c>mob/Login</c>, <c>mob/Logout</c>,
/// <c>client/Del</c>, <c>mob/var/key</c>).
/// </summary>
public class LoginLogoutTests
{
    // ---- Login ----

    [Fact]
    public void LoginSendsTgsSignalsInTgsOrder()
    {
        // add_to_player_list (mob_lists.dm:97-105) sends the global PLAYER_LOGIN first; Login then sends COMSIG_MOB_LOGIN (:81),
        // COMSIG_MOB_CLIENT_LOGIN to the mob and COMSIG_CLIENT_MOB_LOGIN to the client (:132-133), the global MOB_LOGGED_IN
        // (:141), and client/New ends with the global CLIENT_CONNECT (client_procs.dm:608)
        var room = new T1Room();
        var listener = room.In(() => { var l = Dm.@new<datum_test_m1t1_listener>(); l.WatchGlobals(); return l; });
        var avatar = room.NewMob<mob_test_m1t1_watched>(8, 8);
        room.In(() => { avatar.Listener = listener; listener.WatchMobLife(avatar); });
        room.Join(avatar, "Some Player");
        Assert.Equal(new[]
        {
            "GLOB_PLAYER_LOGIN",
            "MOB_LOGIN",
            "MOB_CLIENT_LOGIN Some Player",
            "CLIENT_MOB_LOGIN",
            "GLOB_MOB_LOGGED_IN",
            "GLOB_CLIENT_CONNECT",
        }, listener.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LoginRecordsTheClientOnTheMobAndFillsThePlayerLists()
    {
        // login.dm:33-36 (canon_client, set_mob, add_to_player_list), mob_lists.dm:97-105 and :115-119
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        room.In(() =>
        {
            Assert.False(Dm.@in(avatar, GLOB.player_list));
            Assert.False(Dm.@in(avatar, GLOB.alive_player_list));
            Assert.False(Dm.@in(avatar, GLOB.keyloop_list));
            Assert.Null(avatar.canon_client);
        });
        var c = room.Join(avatar, "Some Player").Client;
        room.In(() =>
        {
            Assert.Same(c, avatar.canon_client);
            Assert.True(Dm.@in(avatar, GLOB.player_list));
            Assert.True(Dm.@in(avatar, GLOB.alive_player_list));
            Assert.True(Dm.@in(avatar, GLOB.keyloop_list));
            Assert.False(Dm.@in(avatar, GLOB.dead_player_list));
            Assert.Equal(1, Dm.each(GLOB.player_list).Count(x => ReferenceEquals(x, avatar)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LoginCopiesTheClientsAddressAndComputerIdAndResetsNextMoveAndStatobj()
    {
        // login.dm:37-38 (lastKnownIP, computer_id), :56 next_move = 1, :58 client.statobj = src
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        room.In(() => avatar.next_move = 99);
        ClientConnection connection = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            connection = ClientSeam.Connect("Some Player", "10.20.30.40", "1234567");
        });
        room.Step();
        var c = connection.Client;
        room.In(() =>
        {
            Assert.Equal("10.20.30.40", Dm.str(avatar.lastKnownIP));
            Assert.Equal("1234567", Dm.str(avatar.computer_id));
            Assert.Equal(1f, Dm.num(avatar.next_move));
            Assert.Same(avatar, c.statobj);
            Assert.Equal("10.20.30.40", c.address);
            Assert.Equal("1234567", c.computer_id);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LoginPutsTheEyeOnTheMobAndTheMobPerspective()
    {
        // login.dm:84 reset_perspective(loc) and mob.dm:512-543: for a turf loc equal to the mob's own loc, set_eye(client.mob)
        // and MOB_PERSPECTIVE; then COMSIG_MOB_RESET_PERSPECTIVE
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() =>
        {
            Assert.Same(avatar, c.eye);
            Assert.Equal(MOB_PERSPECTIVE, c.perspective);
            Assert.Equal(0f, c.images.len);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LoginMakesTheClientsKeyTheMobsKey()
    {
        // login.dm:82-83: if(key != client.key) key = client.key; BYOND ref client/var/mob: client.key == mob.key
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        room.In(() => avatar.key = "some player"); // the mob's desired key, in another form
        var c = room.Join(avatar, "Some Player").Client;
        room.In(() =>
        {
            Assert.Equal("Some Player", avatar.key); // client.key == mob.key (BYOND ref client/var/mob)
            Assert.Equal("someplayer", avatar.ckey);
            Assert.Same(avatar, c.mob);
        });
    }

    [Fact]
    public void ALoginOfAMobInNullspaceHasNoEyeAndTheEyePerspective()
    {
        // mob.dm:535-541 (reset_perspective with a null new_eye): `if(isturf(loc)) {... MOB_PERSPECTIVE} else { client.perspective =
        // EYE_PERSPECTIVE; client.set_eye(loc) }`, and login.dm:84 calls it with `loc`, which is null here; login.dm:86-87 skips on_log
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        var c = room.Join(avatar, "Some Player").Client;
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.In(() => Waiting.NoSleep(avatar.moveToNullspace()));
        ClientConnection again = null;
        room.Act(() => again = ClientSeam.Connect("Some Player"));
        room.Step();
        Assert.True(again.Accepted);
        room.In(() =>
        {
            Assert.Null(again.Client.eye);
            Assert.Equal(EYE_PERSPECTIVE, again.Client.perspective);
            Assert.Null(avatar.loc);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LoginIsRunWithUsrTheMobOfTheKeyAndBYONDsLogoutWithUsrTheMobToo()
    {
        // BYOND ref client/proc/New: "usr: The mob in the world with the same key as the player"
        var room = new T1Room();
        var avatar = room.NewMob<mob_test_m1t1_usr_recorder>(8, 8);
        var c = room.Join(avatar, "Some Player").Client;
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        Assert.Equal(new[] { "Login usr=self", "Logout usr=self" }, avatar.Log);
    }

    // ---- Logout through a disconnect ----

    [Fact]
    public void ADisconnectRunsTheClientDelSignalsThenLogoutWithTheMobStillKeyedAndListed()
    {
        // client/Del (client_procs.dm:604-614): COMSIG_QDELETING on the client, Destroy(), then BYOND's default Del calls
        // mob.Logout() (BYOND ref client/proc/Del), which sends COMSIG_MOB_LOGOUT first (logout.dm:2) and then
        // remove_from_player_list sends the global PLAYER_LOGOUT (mob_lists.dm:59-70). At the moment of Logout the mob has no client
        // but still its key (BYOND ref mob/proc/Logout: the key is non-null for a player who left the game)
        var room = new T1Room();
        var listener = room.In(() => { var l = Dm.@new<datum_test_m1t1_listener>(); l.WatchGlobals(); return l; });
        var avatar = room.NewMob<mob_test_m1t1_watched>(8, 8);
        room.In(() => { avatar.Listener = listener; listener.WatchMobLife(avatar); });
        var c = room.Join(avatar, "Some Player").Client;
        listener.Trace.Clear();
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        Assert.Equal(new[]
        {
            "CLIENT_QDELETING",
            "MOB_LOGOUT key=Some Player client=null listed=1",
            "GLOB_PLAYER_LOGOUT",
        }, listener.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AfterALogoutTheMobIsOutOfEveryPlayerListAndKeepsItsKey()
    {
        // logout.dm:2-9 and mob_lists.dm:59-70 (player_list, keyloop_list, alive_player_list), client/Destroy (clients, directory)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.In(() =>
        {
            Assert.False(Dm.@in(avatar, GLOB.player_list));
            Assert.False(Dm.@in(avatar, GLOB.keyloop_list));
            Assert.False(Dm.@in(avatar, GLOB.alive_player_list));
            Assert.False(Dm.@in(c, GLOB.clients));
            Assert.Null(GLOB.directory["someplayer"]);
            Assert.Null(avatar.client);
            Assert.Null(avatar.canon_client);
            Assert.Equal("Some Player", avatar.key); // BYOND ref client/proc/Del: the mob keeps its key
            Assert.Equal("someplayer", avatar.ckey);
            Assert.Same(avatar, avatar.persistent_client?.mob); // the record still points at the mob for the reconnect
            Assert.NotNull(c.gc_destroyed);
            Assert.True(c.dm_gone);
        });
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ALogoutStopsTheMobAndItsHeldKeys()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.Act(() => room.Down(c, "W"));
        room.Step(3);
        room.In(() => Assert.Equal("8,10", T1Room.Where(avatar)));
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step(5);
        room.In(() => Assert.Equal("8,11", T1Room.Where(avatar))); // the fire of the disconnect's tick still moved it once
        room.AssertNoRuntimes();
    }

    [Fact]
    public void DisconnectingTwiceRunsLogoutOnce()
    {
        var room = new T1Room();
        var avatar = room.NewMob<mob_test_m1t1_usr_recorder>(8, 8);
        var c = room.Join(avatar, "Some Player").Client;
        room.Act(() => { ClientSeam.Disconnect(c); ClientSeam.Disconnect(c); });
        room.Step();
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        Assert.Equal(new[] { "Login usr=self", "Logout usr=self" }, avatar.Log);
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void QdelAfterADisconnectChangesNothing()
    {
        var room = new T1Room();
        var avatar = room.NewMob<mob_test_m1t1_usr_recorder>(8, 8);
        var c = room.Join(avatar, "Some Player").Client;
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.Act(() => qdel(c));
        room.Step();
        Assert.Equal(new[] { "Login usr=self", "Logout usr=self" }, avatar.Log);
        Assert.Equal(new[] { c }, room.Host.Deleted);
    }

    [Fact]
    public void DisconnectAfterAQdelRunsLogoutOnce()
    {
        var room = new T1Room();
        var avatar = room.NewMob<mob_test_m1t1_usr_recorder>(8, 8);
        var c = room.Join(avatar, "Some Player").Client;
        room.Act(() => { qdel(c); ClientSeam.Disconnect(c); });
        room.Step();
        Assert.Equal(new[] { "Login usr=self", "Logout usr=self" }, avatar.Log);
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AQdeletedClientRunsTheSameLogoutAsADisconnect()
    {
        var room = new T1Room();
        var listener = room.In(() => { var l = Dm.@new<datum_test_m1t1_listener>(); l.WatchGlobals(); return l; });
        var avatar = room.NewMob<mob_test_m1t1_watched>(8, 8);
        room.In(() => { avatar.Listener = listener; listener.WatchMobLife(avatar); });
        var c = room.Join(avatar, "Some Player").Client;
        listener.Trace.Clear();
        room.Act(() => qdel(c));
        room.Step();
        Assert.Contains("MOB_LOGOUT key=Some Player client=null listed=1", listener.Trace);
        Assert.Contains("GLOB_PLAYER_LOGOUT", listener.Trace);
        room.In(() => Assert.False(Dm.@in(avatar, GLOB.player_list)));
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheDisconnectedClientIsOutOfTheWorldRegistry()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        long id = room.In(() => c.dm_id);
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.In(() => Assert.False(World.Current.Objects.TryGet(id, out _)));
    }

    // ---- the mob keeps its key and the player comes back ----

    [Fact]
    public void TheSameKeyComesBackToTheSameMobAndTheLoopAndListsTakeItAgain()
    {
        // BYOND ref client/proc/New: "Look for an existing mob with the same key as the player. If found, connect the player to that mob"
        var room = new T1Room();
        var (avatar, first) = room.JoinNew("Some Player");
        room.Act(() => ClientSeam.Disconnect(first));
        room.Step();
        ClientConnection again = null;
        room.Act(() => again = ClientSeam.Connect("Some Player"));
        room.Step();
        Assert.True(again.Accepted);
        Assert.Same(avatar, again.Mob);
        room.In(() =>
        {
            Assert.NotSame(first, again.Client);
            Assert.Same(again.Client, avatar.client);
            Assert.Same(again.Client, avatar.canon_client);
            Assert.True(Dm.@in(avatar, GLOB.player_list));
            Assert.True(Dm.@in(avatar, GLOB.keyloop_list));
            Assert.Same(again.Client, GLOB.directory["someplayer"]);
        });
        room.AssertNoRuntimes();
    }

    // ---- reassigning the key or the client's mob ----

    [Fact]
    public void ReassigningTheKeyToAnotherMobSendsLogoutToTheOldMobWithANullKeyAndThenLoginToTheNewOne()
    {
        // BYOND ref client: "setting N.client = M.client ... disconnects the player from M (calling M.Logout()) and connects the
        // player to N (calling N.Login()). Setting the mob's key has the same effect", mob/Logout: "in the case of a player
        // switching to another mob, by the time Logout() is called, the original mob's key will be null"
        var room = new T1Room();
        var listener = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        var first = room.NewMob(5, 5);
        var second = room.NewMob(9, 9);
        room.In(() => { listener.WatchMobLife(first); listener.WatchMobLife(second); });
        var c = room.Join(first, "Some Player").Client;
        listener.Trace.Clear();
        room.Act(() => second.key = "Some Player");
        room.Step();
        Assert.Equal(new[]
        {
            "MOB_LOGOUT key=null client=null listed=1",
            "MOB_LOGIN",
            "MOB_CLIENT_LOGIN Some Player",
        }, listener.Trace);
        room.In(() =>
        {
            Assert.Same(second, c.mob);
            Assert.Same(c, second.client);
            Assert.Null(first.client);
            Assert.Null(first.key);
            Assert.Equal("Some Player", second.key);
            Assert.Null(first.canon_client);
            Assert.Same(c, second.canon_client);
            Assert.False(Dm.@in(first, GLOB.player_list));
            Assert.True(Dm.@in(second, GLOB.player_list));
            Assert.False(Dm.@in(first, GLOB.keyloop_list));
            Assert.True(Dm.@in(second, GLOB.keyloop_list));
            Assert.Same(second, c.eye);
            Assert.Same(second, c.statobj);
            Assert.Same(second, c.persistent_client.mob);
            Assert.Same(c.persistent_client, second.persistent_client);
            Assert.Null(first.persistent_client);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AssigningTheClientsMobIsTheSameMove()
    {
        var room = new T1Room();
        var first = room.NewMob(5, 5);
        var second = room.NewMob(9, 9);
        var c = room.Join(first, "Some Player").Client;
        room.Act(() => c.mob = second);
        room.Step();
        room.In(() =>
        {
            Assert.Same(second, c.mob);
            Assert.Same(c, second.client);
            Assert.Null(first.client);
            Assert.Null(first.key);
            Assert.Equal("Some Player", second.key);
            Assert.False(Dm.@in(first, GLOB.player_list));
            Assert.True(Dm.@in(second, GLOB.player_list));
        });
        Assert.Equal(2, room.Host.Events.Count(e => e.StartsWith("mob ", StringComparison.Ordinal)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AfterTheSwitchTheHeldKeysMoveTheNewMobAndTheOldOneStops()
    {
        var room = new T1Room();
        var first = room.NewMob(5, 5);
        var second = room.NewMob(9, 9);
        var c = room.Join(first, "Some Player").Client;
        room.Act(() => room.Down(c, "W"));
        room.Step(2);
        room.In(() => Assert.Equal("5,6", T1Room.Where(first)));
        room.Act(() => second.key = "Some Player");
        room.Step(3);
        room.In(() =>
        {
            Assert.Equal("5,7", T1Room.Where(first)); // the fire of the switching tick still moved it once more
            Assert.Equal("9,11", T1Room.Where(second)); // the key is still held: two fires on the new mob
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMobKeyedForAPlayerWhoIsNotConnectedWaitsForThePlayerAndLosesItToAnotherMobWithTheSameKey()
    {
        // BYOND ref mob/var/key: "for non-player mobs (NPCs), this is the 'desired' key ... a player with that key logs in, connected
        // to that mob"; "Any other mob with the same key will lose it"; keys compare in canonical form
        var room = new T1Room();
        var a = room.NewMob(5, 5);
        var b = room.NewMob(9, 9);
        room.In(() =>
        {
            a.key = "Dan";
            Assert.Equal("Dan", a.key);
            Assert.Equal("dan", a.ckey);
            b.key = "dan";
            Assert.Null(a.key);
            Assert.Null(a.ckey);
            Assert.Equal("dan", b.key);
        });
        ClientConnection connection = null;
        room.Act(() => connection = ClientSeam.Connect("Dan"));
        room.Step();
        Assert.Same(b, connection.Mob);
        room.In(() => Assert.Equal("Dan", b.key)); // client.key == mob.key
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeysCompareInCanonicalFormSoPunctuationAndCaseDoNotMatter()
    {
        var room = new T1Room();
        var a = room.NewMob(5, 5);
        room.In(() => a.key = "Some.Player 1");
        ClientConnection connection = null;
        room.Act(() => connection = ClientSeam.Connect("some player 1"));
        room.Step();
        Assert.Same(a, connection.Mob);
        room.In(() =>
        {
            Assert.Equal("some player 1", a.key);
            Assert.Equal("someplayer1", a.ckey);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AssigningCkeyKeepsTheKeyTextAndLinksTheMobTheSameWay()
    {
        // tg's PossessByPlayer (mob.dm:119) assigns ckey: BYOND ref mob/var/ckey "the value of mob.key in canonical form"
        var room = new T1Room();
        var a = room.NewMob(5, 5);
        var b = room.NewMob(9, 9);
        room.In(() =>
        {
            a.ckey = "dan";
            Assert.Equal("dan", a.ckey);
            b.ckey = "dan";
            Assert.Null(a.ckey);
            Assert.Equal("dan", b.ckey);
        });
        ClientConnection connection = null;
        room.Act(() => connection = ClientSeam.Connect("Dan"));
        room.Step();
        Assert.Same(b, connection.Mob);
        room.AssertNoRuntimes();
    }

    // ---- a client that goes away while Login sleeps (login.dm:2-5) ----

    [Fact]
    public void ASleepingLoginKeepsTheConnectionOpenUntilItReturns()
    {
        var room = new T1Room();
        var avatar = room.NewMob<mob_test_m1t1_slow_login>(8, 8);
        ClientConnection connection = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            connection = ClientSeam.Connect("Some Player");
        });
        room.Step();
        Assert.False(connection.Done);
        room.In(() =>
        {
            // client/New has run up to BYOND's own part: the client is registered and attached, but tg's Login has not run
            Assert.True(Dm.@in(connection.Client, GLOB.clients));
            Assert.Same(connection.Client, GLOB.directory["someplayer"]);
            Assert.Same(connection.Client, avatar.client);
            Assert.False(Dm.@in(avatar, GLOB.player_list));
            Assert.Null(avatar.canon_client);
            Assert.False(Dm.truthy(connection.Client.fully_created));
        });
        room.Step();
        Assert.True(connection.Done);
        Assert.True(connection.Accepted);
        room.In(() =>
        {
            Assert.True(avatar.client_after_sleep);
            Assert.True(avatar.login_returned);
            Assert.True(Dm.@in(avatar, GLOB.player_list));
            Assert.True(Dm.truthy(connection.Client.fully_created));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADisconnectWhileLoginSleepsEndsLoginAtItsFirstClientCheckAndClientNewEndsSilently()
    {
        // login.dm:2-5 (the warning), :30-31 `if(!client) return FALSE`; BYOND ref del: procs with src equal to the deleted
        // client are silently killed, so client/New (src = the client) stops where it waited for Login
        var room = new T1Room();
        var listener = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        var avatar = room.NewMob<mob_test_m1t1_slow_login>(8, 8);
        room.In(() => listener.WatchMobLife(avatar));
        ClientConnection connection = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Leaver");
            connection = ClientSeam.Connect("Leaver");
            ClientSeam.Disconnect(connection.Client);
        });
        room.Step(2);
        Assert.True(connection.Done);
        Assert.False(connection.Accepted);
        Assert.Null(connection.Error);
        Assert.Null(connection.Mob);
        room.In(() =>
        {
            Assert.False(avatar.client_after_sleep);
            Assert.True(avatar.login_returned);
            Assert.False(Dm.truthy(avatar.login_result)); // Login returned FALSE
            Assert.Null(avatar.client);
            Assert.Null(avatar.canon_client);
            Assert.Equal("Leaver", avatar.key);
            Assert.False(Dm.@in(avatar, GLOB.player_list));
            Assert.False(Dm.@in(avatar, GLOB.keyloop_list));
            Assert.False(Dm.@in(connection.Client, GLOB.clients));
            Assert.Null(GLOB.directory["leaver"]);
            Assert.False(Dm.truthy(connection.Client.fully_created)); // the rest of client/New never ran
            Assert.Null(connection.Client.connection_time);
            Assert.Null(connection.Client.persistent_client);
            Assert.Null((GLOB.persistent_clients_by_ckey["leaver"] as datum_persistent_client)?.client);
        });
        // Logout ran (the player was connected to the mob) and Login never sent its signals
        Assert.Equal(new[] { "MOB_LOGOUT key=Leaver client=null listed=0" }, listener.Trace);
        Assert.Equal(new[] { connection.Client }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AKickWhileLoginSleepsEndsTheSameWay()
    {
        var room = new T1Room();
        var avatar = room.NewMob<mob_test_m1t1_slow_login>(8, 8);
        ClientConnection connection = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Leaver");
            connection = ClientSeam.Connect("Leaver");
            qdel(connection.Client);
        });
        room.Step(2);
        Assert.True(connection.Done);
        Assert.False(connection.Accepted);
        Assert.Null(connection.Error);
        room.In(() =>
        {
            Assert.Null(avatar.client);
            Assert.False(Dm.@in(avatar, GLOB.player_list));
            Assert.False(Dm.@in(connection.Client, GLOB.clients));
            Assert.Equal("Leaver", avatar.key);
        });
        Assert.Equal(new[] { connection.Client }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlayerCanComeBackAfterALoginThatWasCutShort()
    {
        var room = new T1Room();
        var avatar = room.NewMob<mob_test_m1t1_slow_login>(8, 8);
        ClientConnection first = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Leaver");
            first = ClientSeam.Connect("Leaver");
            ClientSeam.Disconnect(first.Client);
        });
        room.Step(2);
        ClientConnection second = null;
        room.Act(() => second = ClientSeam.Connect("Leaver"));
        room.Step(2);
        Assert.True(second.Accepted);
        room.In(() =>
        {
            Assert.Same(second.Client, avatar.client);
            Assert.True(Dm.@in(avatar, GLOB.player_list));
            Assert.Same(second.Client.persistent_client, GLOB.persistent_clients_by_ckey["leaver"]);
        });
        room.AssertNoRuntimes();
    }
}

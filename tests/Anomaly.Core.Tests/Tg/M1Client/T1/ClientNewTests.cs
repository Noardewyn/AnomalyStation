using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: tg's <c>client/New</c> (code/modules/client/client_procs.dm:255-611) and BYOND's own <c>client/New</c> that its
/// <c>. = ..()</c> reaches (BYOND ref <c>/client/proc/New</c>): the lists it fills, the connections it refuses, the mob it
/// attaches to or makes. A connecting client is made by the engine (<c>ClientSeam.Connect</c>); where a test needs other values
/// of BYOND's own client vars (the build, the connection type) it makes the engine's client itself and runs <c>New</c> as the
/// seam does.
/// </summary>
public class ClientNewTests
{
    private static string Text(T1Room room, object target) => string.Join("\n", room.Chat.To(target));

    // ---- what a plain connection fills ----

    [Fact]
    public void ANewClientIsInTheClientListAndTheDirectoryUnderItsCkey()
    {
        // client_procs.dm:262-263  GLOB.clients += src; GLOB.directory[ckey] = src
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() =>
        {
            Assert.Equal(1, Dm.each(GLOB.clients).Count(x => ReferenceEquals(x, c)));
            Assert.Same(c, GLOB.directory["someplayer"]);
            Assert.Equal(1f, GLOB.clients.len);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TwoClientsAreTwoEntriesInConnectionOrder()
    {
        var room = new T1Room();
        var (m1, c1) = room.JoinNew("Alice", 4, 4);
        var (m2, c2) = room.JoinNew("Bob", 6, 6);
        room.In(() =>
        {
            Assert.Equal(new object[] { c1, c2 }, Dm.each(GLOB.clients).ToArray());
            Assert.Same(c1, GLOB.directory["alice"]);
            Assert.Same(c2, GLOB.directory["bob"]);
        });
    }

    [Fact]
    public void AfterNewTheClientIsFullyCreatedWithItsConnectionTime()
    {
        // :453-455 connection_time = world.time ...; :610 fully_created = TRUE
        var room = new T1Room();
        float t = 0;
        var avatar = room.NewMob(8, 8);
        ClientConnection connection = null;
        room.Act(() =>
        {
            t = world.time;
            ClientSeam.KeyMob(avatar, "Some Player");
            connection = ClientSeam.Connect("Some Player");
        });
        room.Step();
        room.In(() =>
        {
            var c = connection.Client;
            Assert.True(Dm.truthy(c.fully_created));
            Assert.Equal(t, Dm.num(c.connection_time));
            Assert.NotNull(c.connection_realtime);
            Assert.NotNull(c.connection_timeofday);
        });
    }

    [Fact]
    public void ClientNewReturnsTheConnectedMob()
    {
        // BYOND ref client/proc/New: "Returns: The newly connected mob, client.mob"; tg's `. = ..()` keeps it (returns `.`)
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        var connection = room.Join(avatar, "Some Player");
        Assert.Same(avatar, connection.Mob);
    }

    [Fact]
    public void ClientNewSetsTheBYONDVarsOfTheConnection()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() =>
        {
            Assert.Equal("Some Player", c.key);
            Assert.Equal("someplayer", c.ckey);
            Assert.Equal("seeker", c.connection);
            Assert.Equal(516f, c.byond_version);
            Assert.Equal(1685f, c.byond_build);
            Assert.Same(avatar, c.mob);
        });
    }

    // ---- the mob of the key ----

    [Fact]
    public void AClientAttachesToTheMobWithItsKeyAndNotToAnother()
    {
        // BYOND ref client/proc/New: "Look for an existing mob with the same key as the player. If found, connect the player to that mob"
        var room = new T1Room();
        var a = room.NewMob(4, 4);
        var b = room.NewMob(6, 6);
        room.In(() => { a.key = "Alice"; b.key = "Bob"; });
        ClientConnection bob = null;
        room.Act(() => bob = ClientSeam.Connect("Bob"));
        room.Step();
        Assert.Same(b, bob.Mob);
        room.In(() =>
        {
            Assert.Null(a.client);
            Assert.Same(bob.Client, b.client);
            Assert.True(Dm.@in(b, GLOB.player_list));
            Assert.False(Dm.@in(a, GLOB.player_list));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutAMobOfItsKeyAClientGetsAMobOfTypeWorldMobNamedAfterTheKey()
    {
        // BYOND ref client/proc/New: "create a mob of type world.mob, give it the same name and gender as the player's key, and connect
        // the player to it"; tg's world.mob is /mob/dead/new_player (the lobby)
        var room = new T1Room();
        ClientConnection connection = null;
        room.Act(() => connection = ClientSeam.Connect("Stranger"));
        room.Step();
        Assert.True(connection.Accepted);
        room.In(() =>
        {
            mob made = connection.Mob;
            Assert.IsType<mob_dead_new_player>(made);
            Assert.Equal("Stranger", made.name);
            Assert.Equal("Stranger", made.key);
            Assert.Same(connection.Client, made.client);
            Assert.Same(made, connection.Client.mob);
            Assert.True(Dm.@in(made, GLOB.player_list));
        });
        Assert.Equal(new[] { "mob Stranger null -> " + T1Host.Id(connection.Mob) }, room.Host.Events);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutAMobOfItsKeyAndWithoutAWorldMobTheConnectionIsRefusedAndClosed()
    {
        // BYOND ref world/var/mob: "Setting world.mob to 0 prevents the creation of default mobs"; client/New then returns null, "null to
        // disallow the connection", and the engine closes it
        var room = new T1Room();
        room.In(() => Dm.world.mob = null);
        ClientConnection connection = null;
        room.Act(() => connection = ClientSeam.Connect("Stranger"));
        room.Step();
        Assert.True(connection.Done);
        Assert.False(connection.Accepted);
        Assert.Null(connection.Mob);
        room.In(() =>
        {
            Assert.False(Dm.@in(connection.Client, GLOB.clients));
            Assert.Null(GLOB.directory["stranger"]);
            Assert.True(connection.Client.dm_gone);
        });
        Assert.Equal(new[] { connection.Client }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    // ---- what tg's New refuses ----

    [Fact]
    public void AConnectionOfAnotherTypeThanSeekerOrWebIsRefusedBeforeAnythingIsRegistered()
    {
        // :259-260  if(connection != "seeker" && connection != "web") return null
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        room.In(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            client c = client.dm_engine_create("Some Player", "127.0.0.1", null);
            c.connection = "telnet";
            object result = Waiting.NoSleep(c.New());
            Assert.Null(result);
            Assert.False(Dm.@in(c, GLOB.clients));
            Assert.Null(GLOB.directory["someplayer"]);
            Assert.Null(c.persistent_client);
            Assert.Null(avatar.client); // BYOND's own New did not run either
        });
    }

    [Fact]
    public void ABlacklistedBYONDBuildIsToldAndKicked()
    {
        // :395-406  num2text(byond_build) in GLOB.blacklisted_builds ("1622": "Bug breaking rendering can lead to wallhacks.", :5-7)
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        client c = null;
        object result = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            c = client.dm_engine_create("Some Player", "127.0.0.1", null);
            c.byond_build = 1622;
            result = Waiting.NoSleep(c.New());
        });
        room.Step();
        string text = Text(room, c);
        Assert.Contains("Your version of BYOND is blacklisted.", text, StringComparison.Ordinal);
        Assert.Contains("BYOND build 1622 (516.1622) has been blacklisted for the following reason: Bug breaking rendering can lead to wallhacks.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("As an admin", text, StringComparison.Ordinal);
        room.In(() =>
        {
            Assert.NotNull(c.gc_destroyed); // qdel(src)
            Assert.False(Dm.@in(c, GLOB.clients));
            Assert.False(Dm.truthy(c.fully_created)); // `return` before the end of New
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnotherBuildIsNotBlacklisted()
    {
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        client c = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            c = client.dm_engine_create("Some Player", "127.0.0.1", null);
            c.byond_build = 1621;
            Waiting.NoSleep(c.New());
        });
        room.Step();
        room.In(() =>
        {
            Assert.Null(c.gc_destroyed);
            Assert.True(Dm.truthy(c.fully_created));
        });
        Assert.DoesNotContain("blacklisted", Text(room, c), StringComparison.Ordinal);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AWebClientIsRefusedWhileTheWebClientIsDisabled()
    {
        // :499-503  if(connection == "web" && !connecting_admin) { if(!CONFIG_GET(flag/allow_webclient)) { to_chat_immediate(...
        // "Web client is disabled"); qdel(src); return } }; the flag has no default, so it is off
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        client c = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            c = client.dm_engine_create("Some Player", "127.0.0.1", null);
            c.connection = "web";
            Waiting.NoSleep(c.New());
        });
        room.Step();
        Assert.Contains("Web client is disabled", Text(room, c), StringComparison.Ordinal);
        room.In(() =>
        {
            Assert.NotNull(c.gc_destroyed);
            Assert.False(Dm.truthy(c.fully_created));
        });
        room.AssertNoRuntimes();
    }

    // ---- the host of the round ----

    [Fact]
    public void ThePlayerWithNoAddressIsTheHostOfTheServerAndALaterOneIsNot()
    {
        // :507-510  if((world.address == address || !address) && !GLOB.host) { GLOB.host = key; world.update_status() }
        var room = new T1Room();
        var first = room.NewMob(4, 4);
        var second = room.NewMob(6, 6);
        room.In(() => Assert.False(Dm.truthy(GLOB.host)));
        ClientConnection c1 = null, c2 = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(first, "Hostess");
            c1 = ClientSeam.Connect("Hostess", null);
            ClientSeam.KeyMob(second, "Guest");
            c2 = ClientSeam.Connect("Guest", null);
        });
        room.Step();
        room.In(() => Assert.Equal("Hostess", Dm.str(GLOB.host)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void APlayerWithARemoteAddressIsNotTheHost()
    {
        var room = new T1Room();
        var avatar = room.NewMob(4, 4);
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Remote");
            ClientSeam.Connect("Remote", "203.0.113.9");
        });
        room.Step();
        room.In(() => Assert.False(Dm.truthy(GLOB.host)));
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData(null, true)]
    [InlineData("10.0.0.5", false)]
    [InlineData("203.0.113.9", false)]
    public void IsLocalhostIsTrueForTheLoopbackAddressesAndNoAddress(string address, bool expected)
    {
        // :173-176  var/static/localhost_addresses = list("127.0.0.1", "::1", null); return address in localhost_addresses
        var room = new T1Room();
        var avatar = room.NewMob(4, 4);
        ClientConnection connection = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            connection = ClientSeam.Connect("Some Player", address);
        });
        room.Step();
        room.In(() => Assert.Equal(expected, Dm.truthy(connection.Client.is_localhost())));
        room.AssertNoRuntimes();
    }

    // ---- the engine's connection variables ----

    [Fact]
    public void ThePlayersAddressAndComputerIdAreTheHostsAndTheMobGetsThem()
    {
        var room = new T1Room();
        var avatar = room.NewMob(4, 4);
        ClientConnection connection = null;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            connection = ClientSeam.Connect("Some Player", "198.51.100.7", "314159");
        });
        room.Step();
        room.In(() =>
        {
            Assert.Equal("198.51.100.7", connection.Client.address);
            Assert.Equal("314159", connection.Client.computer_id);
            Assert.Equal("198.51.100.7", Dm.str(avatar.lastKnownIP));
        });
    }

    [Fact]
    public void ConnectWithoutAKeyIsRefusedByTheSeam()
    {
        var room = new T1Room();
        room.In(() =>
        {
            Assert.Throws<ArgumentException>(() => ClientSeam.Connect(""));
            Assert.Throws<ArgumentException>(() => ClientSeam.Connect(null));
        });
    }

    [Fact]
    public void AnEngineMadeClientHasItsKeyBeforeNewFillsTheLists()
    {
        // BYOND sets the vars of a connecting client before it calls New (BYOND ref client/proc/New): the client is in the world
        // from the seam's call, with its key, before tg's New fills the lists
        var room = new T1Room();
        var avatar = room.NewMob(4, 4);
        client made = null;
        room.In(() =>
        {
            ClientSeam.KeyMob(avatar, "Some Player");
            made = client.dm_engine_create("Some Player", "127.0.0.1", null);
            Assert.Equal("Some Player", made.key);
            Assert.False(Dm.@in(made, GLOB.clients));
            Assert.Null(avatar.client);
        });
    }
}

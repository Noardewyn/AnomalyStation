using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: <c>/datum/persistent_client</c> and <c>log_played_names</c> (code/modules/client/persistent_client.dm): what tracks a
/// player between log in and log out, and how <c>client/New</c> reuses it on a reconnect (client_procs.dm:265-270).
/// </summary>
public class PersistentClientTests
{
    private static float N(object v) => Dm.num(v);

    private static datum_persistent_client NewPersistent(string ckey) => Dm.@new<datum_persistent_client>(ckey);

    // ---- the datum on its own (a booted world, no connection) ----

    [Fact]
    public void NewRegistersTheDatumInBothGlobalListsAndMakesItsAchievementData()
    {
        // persistent_client.dm:36-39  achievements = new(ckey); GLOB.persistent_clients_by_ckey[ckey] = src; GLOB.persistent_clients += src
        var room = new T1Room();
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            Assert.Same(pc, GLOB.persistent_clients_by_ckey["somebody"]);
            Assert.True(Dm.@in(pc, GLOB.persistent_clients));
            Assert.NotNull(pc.achievements);
            Assert.Equal("somebody", pc.achievements.owner_ckey);
            Assert.Equal(0f, pc.time_of_death);
            Assert.Null(pc.client);
            Assert.Null(pc.mob);
            Assert.Equal(0f, pc.player_actions.len);
            Assert.Equal(0f, pc.post_login_callbacks.len);
            Assert.Equal(0f, pc.post_logout_callbacks.len);
            Assert.Equal(0f, pc.played_names.len);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SetClientLinksBothWaysAndCopiesTheByondVersion()
    {
        // :52-63
        var room = new T1Room();
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            var c = Dm.@new<client>();
            c.byond_version = 516;
            c.byond_build = 1685;
            pc.set_client(c);
            Assert.Same(c, pc.client);
            Assert.Same(pc, c.persistent_client);
            Assert.Equal(516f, N(pc.byond_version));
            Assert.Equal(1685f, N(pc.byond_build));
        });
    }

    [Fact]
    public void SetClientWithTheSameClientAgainChangesNothing()
    {
        // :53-54  if(client == new_client) return
        var room = new T1Room();
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            var c = Dm.@new<client>();
            c.byond_version = 516;
            c.byond_build = 1685;
            pc.set_client(c);
            c.byond_build = 1700; // would be copied again if the call went on
            pc.set_client(c);
            Assert.Equal(1685f, N(pc.byond_build));
        });
    }

    [Fact]
    public void SetClientToAnotherClientDetachesTheOldOne()
    {
        // :56-57  if(client) client.persistent_client = null
        var room = new T1Room();
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            var first = Dm.@new<client>();
            var second = Dm.@new<client>();
            first.byond_version = 515;
            second.byond_version = 516;
            pc.set_client(first);
            pc.set_client(second);
            Assert.Null(first.persistent_client);
            Assert.Same(pc, second.persistent_client);
            Assert.Equal(516f, N(pc.byond_version));
        });
    }

    [Fact]
    public void SetClientNullDetachesAndKeepsTheLastVersion()
    {
        var room = new T1Room();
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            var c = Dm.@new<client>();
            c.byond_version = 516;
            c.byond_build = 1685;
            pc.set_client(c);
            pc.set_client(null);
            Assert.Null(pc.client);
            Assert.Null(c.persistent_client);
            Assert.Equal(516f, N(pc.byond_version)); // "Major version of BYOND this client was last using"
        });
    }

    [Fact]
    public void SetMobLinksBothWaysAndMovesOffTheOldMob()
    {
        // :66-74  mob?.persistent_client = null; new_mob?.persistent_client?.set_mob(null); mob = new_mob; new_mob?.persistent_client = src
        var room = new T1Room();
        var m1 = room.NewMob(4, 4);
        var m2 = room.NewMob(5, 5);
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            pc.set_mob(m1);
            Assert.Same(m1, pc.mob);
            Assert.Same(pc, m1.persistent_client);
            pc.set_mob(m2);
            Assert.Null(m1.persistent_client);
            Assert.Same(pc, m2.persistent_client);
            Assert.Same(m2, pc.mob);
            pc.set_mob(m2); // the same mob: nothing
            Assert.Same(pc, m2.persistent_client);
            pc.set_mob(null);
            Assert.Null(m2.persistent_client);
            Assert.Null(pc.mob);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SetMobTakingAMobThatAnotherPersistentClientHoldsMakesTheOtherLetGo()
    {
        // :71  new_mob?.persistent_client?.set_mob(null)
        var room = new T1Room();
        var m = room.NewMob(4, 4);
        room.In(() =>
        {
            var first = NewPersistent("first");
            var second = NewPersistent("second");
            first.set_mob(m);
            second.set_mob(m);
            Assert.Null(first.mob);
            Assert.Same(second, m.persistent_client);
            Assert.Same(m, second.mob);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void GetPlayedNamesJoinsEncodedNameAndTagPairs()
    {
        // :75-79  html_encode("[previous_name] ([played_names[previous_name]])") joined with "; "
        var room = new T1Room();
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            Assert.Equal("", Dm.str(pc.get_played_names()));
            pc.played_names = Dm.list(Dm.kv("Bob", "mob_1"), Dm.kv("<b>Eve</b>", "mob_2"));
            Assert.Equal("Bob (mob_1); &lt;b&gt;Eve&lt;/b&gt; (mob_2)", Dm.str(pc.get_played_names()));
        });
    }

    [Fact]
    public void FullByondVersionIsUnknownWithoutAVersionAndXxxWithoutABuild()
    {
        // :82-85  if(!byond_version) return "Unknown"; return "[byond_version].[byond_build || "xxx"]"
        var room = new T1Room();
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            Assert.Equal("Unknown", Dm.str(pc.full_byond_version()));
            pc.byond_version = 516;
            Assert.Equal("516.xxx", Dm.str(pc.full_byond_version()));
            pc.byond_build = 1685;
            Assert.Equal("516.1685", Dm.str(pc.full_byond_version()));
        });
    }

    [Fact]
    public void LogPlayedNamesAddsNewNamesWithTheirTagAndSkipsEmptyAndKnownOnes()
    {
        // :88-105
        var room = new T1Room();
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            log_played_names("somebody", Dm.list(Dm.kv("Bob", "mob_1"), Dm.kv("", "mob_9"), Dm.kv("Bob <3", "mob_2")));
            Assert.Equal(2f, pc.played_names.len);
            Assert.Equal("mob_1", Dm.str(pc.played_names["Bob"]));
            Assert.Equal("mob_2", Dm.str(pc.played_names["Bob &lt;3"])); // html_encode(name)
            log_played_names("somebody", Dm.list(Dm.kv("Bob", "mob_7"))); // known: kept as it is
            Assert.Equal(2f, pc.played_names.len);
            Assert.Equal("mob_1", Dm.str(pc.played_names["Bob"]));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LogPlayedNamesWithoutACkeyOrForAnUnknownCkeyDoesNothing()
    {
        var room = new T1Room();
        room.In(() =>
        {
            var pc = NewPersistent("somebody");
            log_played_names(null, Dm.list(Dm.kv("Bob", "mob_1")));
            log_played_names("", Dm.list(Dm.kv("Bob", "mob_1")));
            log_played_names("stranger", Dm.list(Dm.kv("Bob", "mob_1")));
            Assert.Equal(0f, pc.played_names.len);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void QdelOfAPersistentClientIsRefusedWithACrashAndTheDatumLives()
    {
        // :34-37  Destroy: SHOULD_CALL_PARENT(FALSE); . = QDEL_HINT_LETMELIVE; CRASH("Who the FUCK tried to delete ...")
        var room = new T1Room();
        persistent_holder holder = new persistent_holder();
        room.In(() =>
        {
            holder.Pc = NewPersistent("somebody");
            qdel(holder.Pc);
            Assert.Null(holder.Pc.gc_destroyed); // LETMELIVE: not deleted
            Assert.Same(holder.Pc, GLOB.persistent_clients_by_ckey["somebody"]);
            Assert.True(Dm.@in(holder.Pc, GLOB.persistent_clients));
        });
        Assert.Single(room.Room.Runtimes);
        Assert.Contains("Who the FUCK tried to delete a persistent client?", room.Room.Runtimes[0].Message, StringComparison.Ordinal);
    }

    private sealed class persistent_holder
    {
        public datum_persistent_client Pc;
    }

    // ---- with real connections ----

    [Fact]
    public void AFirstConnectionMakesThePersistentClientOfItsCkey()
    {
        // client_procs.dm:269-271 (else branch: persistent_client = new(ckey)); :272 persistent_client.set_client(src)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() =>
        {
            var pc = c.persistent_client;
            Assert.NotNull(pc);
            Assert.Same(pc, GLOB.persistent_clients_by_ckey["someplayer"]);
            Assert.Same(c, pc.client);
            Assert.Same(avatar, pc.mob); // mob/Login: client.persistent_client.set_mob(src)
            Assert.Same(pc, avatar.persistent_client);
            Assert.Equal(516f, N(pc.byond_version));
            Assert.Equal(1685f, N(pc.byond_build));
            Assert.Equal("516.1685", Dm.str(pc.full_byond_version()));
            Assert.Equal(1, Dm.each(GLOB.persistent_clients).Count(x => ReferenceEquals(x, pc)));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AReconnectReusesThePersistentClientOfTheKeyAndNeverMakesASecond()
    {
        // client_procs.dm:265-268  if(GLOB.persistent_clients_by_ckey[ckey]) { reconnecting = TRUE; persistent_client = ... }
        var room = new T1Room();
        var (avatar, first) = room.JoinNew("Some Player");
        datum_persistent_client original = null;
        object achievements = null;
        room.In(() => { original = first.persistent_client; achievements = original.achievements; });
        int listed = room.In(() => (int)GLOB.persistent_clients.len);
        room.Act(() => ClientSeam.Disconnect(first));
        room.Step();
        room.In(() =>
        {
            Assert.Null(first.persistent_client);
            Assert.Null(original.client); // Destroy: persistent_client.set_client(null)
            Assert.Same(original, GLOB.persistent_clients_by_ckey["someplayer"]); // the record stays
        });
        ClientConnection again = null;
        room.Act(() => again = ClientSeam.Connect("Some Player"));
        room.Step();
        Assert.True(again.Accepted);
        room.In(() =>
        {
            Assert.Same(original, again.Client.persistent_client);
            Assert.Same(achievements, original.achievements);
            Assert.Same(again.Client, original.client);
            Assert.Same(avatar, original.mob);
            Assert.Equal(listed, (int)GLOB.persistent_clients.len);
            Assert.Same(original, avatar.persistent_client);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlayedNamesOfAKeyGrowAcrossReconnectsWithoutRepeating()
    {
        // Login: log_played_names(client.ckey, list("[name]" = tag, "[real_name]" = tag)); an empty real_name has no entry
        var room = new T1Room();
        var (avatar, first) = room.JoinNew("Some Player");
        string tag = room.In(() => Dm.str(avatar.tag));
        room.In(() =>
        {
            var names = first.persistent_client.played_names;
            Assert.Equal(1f, names.len);
            Assert.Equal(tag, Dm.str(names["mob"])); // the mob's name is its type's name; real_name is null, whose text is ""
        });
        room.Act(() => ClientSeam.Disconnect(first));
        room.Step();
        room.In(() => avatar.real_name = "John Doe");
        ClientConnection again = null;
        room.Act(() => again = ClientSeam.Connect("Some Player"));
        room.Step();
        room.In(() =>
        {
            var names = again.Client.persistent_client.played_names;
            Assert.Equal(2f, names.len); // "mob" is known, "John Doe" is new
            Assert.Equal(tag, Dm.str(names["John Doe"]));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TwoPlayersHaveTheirOwnPersistentClients()
    {
        var room = new T1Room();
        var (m1, c1) = room.JoinNew("Alice", 4, 4);
        var (m2, c2) = room.JoinNew("Bob", 6, 6);
        room.In(() =>
        {
            Assert.NotSame(c1.persistent_client, c2.persistent_client);
            Assert.Same(c1.persistent_client, GLOB.persistent_clients_by_ckey["alice"]);
            Assert.Same(c2.persistent_client, GLOB.persistent_clients_by_ckey["bob"]);
            Assert.Same(m1, c1.persistent_client.mob);
            Assert.Same(m2, c2.persistent_client.mob);
        });
    }

    [Fact]
    public void PostLoginCallbacksOfAReturningPlayerRunInLoginBeforeTheClientLoginSignal()
    {
        // login.dm:71-79: after the player's actions and before log_played_names; the signals COMSIG_MOB_CLIENT_LOGIN follow (:100)
        var room = new T1Room();
        var listener = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        var avatar = room.NewMob<mob_test_m1t1_watched>(8, 8);
        room.In(() => { avatar.Listener = listener; listener.WatchMobLife(avatar); });
        var first = room.Join(avatar, "Some Player").Client;
        room.In(() => first.persistent_client.post_login_callbacks.Add(Dm.@new<datum_callback>(listener, nameof(datum_test_m1t1_listener.ping_login))));
        room.Act(() => ClientSeam.Disconnect(first));
        room.Step();
        listener.Trace.Clear();
        room.Act(() => ClientSeam.Connect("Some Player"));
        room.Step();
        Assert.Equal(new[] { "MOB_LOGIN", "CALLBACK post_login", "MOB_CLIENT_LOGIN Some Player" }, listener.Trace.Where(t => !t.StartsWith("CLIENT_", StringComparison.Ordinal)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void PostLogoutCallbacksRunOnceWhenTheMobLosesItsClientBeforeLogoutSignals()
    {
        // client/Destroy calls mob.become_uncliented() first (client_procs.dm:617-621): the callbacks run, canon_client is cleared;
        // BYOND's Logout then reaches mob/Logout (logout.dm), whose become_uncliented returns at once (`if(!canon_client) return`)
        var room = new T1Room();
        var listener = room.In(() => Dm.@new<datum_test_m1t1_listener>());
        var avatar = room.NewMob<mob_test_m1t1_watched>(8, 8);
        room.In(() => { avatar.Listener = listener; listener.WatchMobLife(avatar); });
        var c = room.Join(avatar, "Some Player").Client;
        room.In(() => c.persistent_client.post_logout_callbacks.Add(Dm.@new<datum_callback>(listener, nameof(datum_test_m1t1_listener.ping_logout))));
        listener.Trace.Clear();
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        Assert.Equal(new[]
        {
            "CLIENT_QDELETING",
            "CALLBACK post_logout",
            "MOB_LOGOUT key=Some Player client=null listed=1",
        }, listener.Trace);
        room.In(() => Assert.Null(avatar.canon_client));
        room.AssertNoRuntimes();
    }
}

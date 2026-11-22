using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: the client branches of <c>key_name</c> (code/__HELPERS/logging/_logging.dm:161-241), which every log line about a player uses,
/// and the client-layer part of <c>/mob/Destroy</c> (code/modules/mob/mob.dm:19-58).
/// </summary>
public class KeyNameAndMobDestroyTests
{
    [Fact]
    public void KeyNameOfAClientIsItsKeyAndTheMobsRealName()
    {
        // :170-174 and :232-239  key + "/(real_name)"
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() =>
        {
            avatar.real_name = "John Doe";
            Assert.Equal("Some Player/(John Doe)", Dm.str(key_name(c)));
            Assert.Equal("Some Player/(John Doe)", Dm.str(key_name(avatar)));
            Assert.Equal("Some Player/(John Doe)", Dm.str(key_name("Some Player")));
            Assert.Equal("some player/(John Doe)", Dm.str(key_name("some player"))); // the text is the key (:181); the player is looked up by ckey in GLOB.directory
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyNameFallsBackToTheMobsNameWhenItHasNoRealName()
    {
        // :234-237  if(M.real_name) ... else if(M.name)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() => Assert.Equal("Some Player/(mob)", Dm.str(key_name(c))));
    }

    [Fact]
    public void KeyNameWithoutTheNameIsJustTheKey()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() => Assert.Equal("Some Player", Dm.str(key_name(c, null, FALSE))));
    }

    [Fact]
    public void KeyNameOfAMobWithoutAKeyIsNoKeyAndItsName()
    {
        // :243-244  else . += "*no key*"
        var room = new T1Room();
        var idle = room.NewMob(4, 4);
        room.In(() => Assert.Equal("*no key*/(mob)", Dm.str(key_name(idle))));
    }

    [Fact]
    public void KeyNameOfNullIsNullInStars()
    {
        var room = new T1Room();
        room.In(() => Assert.Equal("*null*", Dm.str(key_name(null))));
    }

    [Fact]
    public void KeyNameOfAKeyWithNoClientAndNoMobIsTheKeyMarkedDisconnected()
    {
        // :224-225  if(!C) . += "\[DC\]"; no M and no fallback: no name
        var room = new T1Room();
        room.In(() => Assert.Equal("Nobody[DC]", Dm.str(key_name("Nobody"))));
    }

    [Fact]
    public void KeyNameOfADisconnectedPlayersMobHasTheDCMarkAfterTheKey()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() => avatar.real_name = "John Doe");
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.In(() => Assert.Equal("Some Player[DC]/(John Doe)", Dm.str(key_name(avatar))));
    }

    [Fact]
    public void KeyNameAdminLinksThePlayersKeyAndClosesTheLinkAfterTheDCMark()
    {
        // :217-228  <a href='byond://?priv_msg=[ckey]'>key[DC]</a>; key_name_admin is include_link = TRUE (:243-244)
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() => avatar.real_name = "John Doe");
        room.In(() => Assert.Equal("<a href='byond://?priv_msg=someplayer'>Some Player</a>/(John Doe)", Dm.str(key_name_admin(c))));
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.In(() => Assert.Equal("<a href='byond://?priv_msg=someplayer'>Some Player[DC]</a>/(John Doe)", Dm.str(key_name_admin(avatar))));
    }

    [Fact]
    public void KeyNameAndTagAddsTheTagOfAnAtom()
    {
        // :246-251
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() =>
        {
            string tag = Dm.str(avatar.tag);
            Assert.Equal("Some Player/(mob) (" + tag + ")", Dm.str(key_name_and_tag(avatar)));
            Assert.Equal("Some Player/(mob) (!tagless!)", Dm.str(key_name_and_tag(c)));
        });
    }

    [Fact]
    public void KeyNameOfSomethingWithoutAKeyIsItsNameInBrackets()
    {
        // :196-208  atom: "[name]" between \[ and \]
        var room = new T1Room();
        room.In(() =>
        {
            turf t = room.At(3, 3);
            Assert.Equal("[" + Dm.str(t.name) + "]", Dm.str(key_name(t)));
        });
    }

    // ---- /mob/Destroy ----

    [Fact]
    public void DeletingAConnectedPlayersMobIsAStackTraceAndThePlayerBecomesAGhost()
    {
        // mob.dm:20-21 if(client) stack_trace("Mob with client has been deleted."); :24 persistent_client?.set_mob(null); :47
        // ghostize(can_reenter_corpse = FALSE): the player goes on as a ghost (the ghosts, D151), which takes the persistent client.
        // The mob has a real name: the bare room has no name lists for tg's generate_random_mob_name (observer.dm:105).
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.In(() => avatar.real_name = "John Doe");
        datum_persistent_client pc = room.In(() => c.persistent_client);
        room.In(() => qdel(avatar));
        Assert.Contains(room.Room.Runtimes, e => e.Message.Contains("Mob with client has been deleted.", StringComparison.Ordinal));
        room.In(() =>
        {
            var ghost = c.mob as mob_dead_observer;
            Assert.NotNull(ghost);
            Assert.Same(ghost, pc.mob);
            Assert.Null(avatar.persistent_client);
            Assert.NotNull(avatar.gc_destroyed);
            Assert.False(Dm.truthy(ghost.can_reenter_corpse));
            Assert.Equal("John Doe", ghost.real_name);
        });
    }

    [Fact]
    public void DeletingAnUnconnectedPlayersMobWithItsCkeyIsAStackTraceWithTheCkey()
    {
        // :22-23  else if(ckey && !IS_FAKE_KEY(ckey)) stack_trace("Mob without client but with associated ckey, [ckey], has been deleted.")
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Some Player");
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.In(() => qdel(avatar));
        Assert.Contains(room.Room.Runtimes, e => e.Message.Contains("Mob without client but with associated ckey, someplayer, has been deleted.", StringComparison.Ordinal));
    }

    [Fact]
    public void DeletingAMobWithoutAKeyOrWithAFakeKeyIsQuiet()
    {
        // IS_FAKE_KEY (__DEFINES/client.dm): a ckey that starts with "@" (tg's aghosts)
        var room = new T1Room();
        var plain = room.NewMob(4, 4);
        var ghosty = room.NewMob(6, 6);
        room.In(() =>
        {
            ghosty.ckey = "@someplayer";
            qdel(plain);
            qdel(ghosty);
        });
        room.AssertNoRuntimes();
    }
}

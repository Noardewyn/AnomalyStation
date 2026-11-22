using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>
/// The log line builders of __HELPERS/logging/_logging.dm (key_name :161-237, key_name_admin :243, key_name_and_tag :246,
/// loc_name :253) and mob.dm's log_mob_tag (logging/mob.dm:6), for a world with no clients: a key is always "[DC]".
/// </summary>
public class MobLoggingTests
{
    private static T NewAt<T>(int x = 3, int y = 3) where T : atom => Dm.@new<T>(MobRoom.T(x, y));

    // ---- key_name ----

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData("")]
    public void KeyNameOfNothingOrAFalsyValueIsNull(object whom)
    {
        var room = new MobRoom();
        room.In(() => Assert.Equal("*null*", key_name(whom)));
    }

    [Fact]
    public void KeyNameOfAMobWithoutAKeyIsNoKeyAndItsNameInParentheses()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.Equal("*no key*/(mob)", key_name(m)); // real_name is null: name
            m.real_name = "John Doe";
            Assert.Equal("*no key*/(John Doe)", key_name(m)); // real_name wins (:218)
            m.real_name = "";
            m.name = "";
            Assert.Equal("*no key*", key_name(m)); // neither: no suffix
            m.real_name = "Jane";
            Assert.Equal("*no key*", key_name(m, include_name: FALSE));
        });
    }

    [Fact]
    public void KeyNameOfAMobWithAKeyAddsDcBecauseItHasNoClient()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.key = "Player";
            m.ckey = "player";
            m.real_name = "John Doe";
            Assert.Equal("Player[DC]/(John Doe)", key_name(m));
            Assert.Equal("Player[DC]", key_name(m, include_name: FALSE));
            m.key = null;
            m.ckey = null;
        });
    }

    [Fact]
    public void TheLinkIsOnlyMadeForARequestedLinkAndAKnownCkey()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.key = "Player";
            m.ckey = "player";
            m.real_name = "John Doe";
            Assert.Equal("<a href='byond://?priv_msg=player'>Player[DC]</a>/(John Doe)", key_name(m, TRUE));
            m.ckey = null; // `if(!ckey) include_link = FALSE` (:210)
            Assert.Equal("Player[DC]/(John Doe)", key_name(m, TRUE));
            m.key = null;
        });
    }

    [Fact]
    public void KeyNameAdminAlwaysAsksForTheLinkAndPassesIncludeName()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.key = "Player";
            m.ckey = "player";
            m.real_name = "John Doe";
            Assert.Equal("<a href='byond://?priv_msg=player'>Player[DC]</a>/(John Doe)", key_name_admin(m));
            Assert.Equal("<a href='byond://?priv_msg=player'>Player[DC]</a>", key_name_admin(m, include_name: FALSE));
            Assert.Equal("*null*", key_name_admin(null));
            m.key = null;
        });
    }

    [Fact]
    public void KeyNameOfTextTakesItAsTheKeyAndItsCkeyAndFindsNoClientForIt()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            Assert.Equal("Some Key[DC]", key_name("Some Key")); // no mob, no name part
            Assert.Equal("<a href='byond://?priv_msg=somekey'>Some Key[DC]</a>", key_name("Some Key", TRUE));
        });
    }

    [Fact]
    public void KeyNameOfAnAtomOrADatumWithoutAKeyIsItsTextInBrackets()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            Assert.Equal("[obj]", key_name(o));
            o.name = "toolbox";
            Assert.Equal("[toolbox]", key_name(o));
            Assert.Equal("[/datum]", key_name(Dm.@new<datum>())); // `"[whom]"` of a datum is its type path
            Assert.Equal("[" + MobRoom.T(3, 3).name + "]", key_name(MobRoom.T(3, 3)));
        });
    }

    [Fact]
    public void TgQuirk_AnAtomWithAnEmptyNameIsInvalid()
    {
        // `swhom = "[A.name]"` is empty, and `if(!swhom) swhom = "*invalid*"` (:195-203)
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            o.name = "";
            Assert.Equal("[*invalid*]", key_name(o));
        });
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-1.5f)]
    public void KeyNameOfANumberIsInvalid(object whom)
    {
        var room = new MobRoom();
        room.In(() => Assert.Equal("[*invalid*]", key_name(whom)));
    }

    // ---- key_name_and_tag ----

    [Fact]
    public void KeyNameAndTagAppendsTheTagOfAnAtomAndTaglessForOtherThings()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.Equal("*no key*/(mob) (mob_1)", key_name_and_tag(m));
            Assert.Equal("*no key* (mob_1)", key_name_and_tag(m, include_name: FALSE));
            var o = NewAt<obj>();
            Assert.Equal("[obj] ()", key_name_and_tag(o)); // an obj has no tag: `"[null]"` is empty
            Assert.Equal("*null* (!tagless!)", key_name_and_tag(null));
            Assert.Equal("Key[DC] (!tagless!)", key_name_and_tag("Key"));
            Assert.Equal("<a href='byond://?priv_msg=key'>Key[DC]</a> (!tagless!)", key_name_and_tag("Key", TRUE));
        });
    }

    // ---- loc_name ----

    [Fact]
    public void LocNameOfNothingIsAnInvalidLocation()
    {
        // `!istype(A)` (:254): the C# parameter is an atom, so the only non-atom a caller can pass is null
        var room = new MobRoom();
        room.In(() => Assert.Equal("(INVALID LOCATION)", loc_name(null)));
    }

    [Fact]
    public void LocNameOfAnAtomInNullspaceIsNull()
    {
        var room = new MobRoom();
        room.In(() => Assert.Null(loc_name(Dm.@new<obj>())));
    }

    [Fact]
    public void LocNameOfAnAtomInsideAContainerOutsideTheMapIsUnknown()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var box = Dm.@new<obj>();
            var thing = Dm.@new<obj>(box);
            Assert.Equal("(UNKNOWN (?, ?, ?))", loc_name(thing)); // no turf, but a loc (:260-261)
        });
    }

    [Fact]
    public void LocNameOfAnAtomOnTheMapIsItsAreaAndCoordinates()
    {
        // _logging.dm:262: "([AREACOORD(T)])", the area's name and the turf's coordinates
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            turf t = get_turf(o);
            string expected = Dm.text("([] ([],[],[]))", get_area_name(t, TRUE), t.x, t.y, t.z);
            Assert.Equal(expected, loc_name(o));
            Assert.Equal(expected, loc_name(t));
        });
    }

    // ---- warning, notice (wave 3) used by the stack_trace paths of mob ----

    [Fact]
    public void AMobDestroyedWithAKeyNamesTheCkeyInItsRuntime()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.ckey = "bob";
            qdel(m);
        });
        Assert.Contains("Mob without client but with associated ckey, bob, has been deleted.", Assert.Single(room.Runtimes).Message);
    }
}

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
/// T1: the host entry points of the client seam (<see cref="ClientSeam"/>, the contract in its doc comments) as the server host
/// (P3) calls them in the command phase: what an input frame turns into in tg's verbs, and the join and leave sequences. The
/// effects are read from tg's variables.
/// </summary>
public class HostSeamTests
{
    private static float N(object v) => Dm.num(v);

    private static List<string> Held(client c) => Dm.each(c.keys_held).Select(k => Dm.str(k)).ToList();

    [Theory]
    [InlineData(North, "North")]
    [InlineData(South, "South")]
    [InlineData(East, "East")]
    [InlineData(West, "West")]
    public void EachDirectionHasTheArrowKeyOfTgsMovementKeybinding(int dir, string key)
    {
        // datums/keybinding/movement.dm:5-31: hotkey_keys = list("W", "North") and so on: the second entry is the arrow
        Assert.Equal(key, ClientSeam.MovementHotkey(dir));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(16)]
    public void NoOtherNumberIsADirectionHotkey(int dir)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientSeam.MovementHotkey(dir));
    }

    [Fact]
    public void AnInputFrameOfHeldDirectionsHoldsExactlyThoseArrowKeysInOrder()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => ClientSeam.SetHeldDirections(c, North | South | East | West));
        room.Step();
        room.In(() => Assert.Equal(new[] { "North", "South", "East", "West" }, Held(c)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AChangedFrameReleasesTheDroppedKeysAndPressesTheNewOnes()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => ClientSeam.SetHeldDirections(c, South | East));
        room.Step();
        room.Act(() => ClientSeam.SetHeldDirections(c, North | West));
        room.Step();
        room.In(() =>
        {
            Assert.Equal(new[] { "North", "West" }, Held(c));
            Assert.Equal(North | West, (int)N(c.intended_direction));
        });
        room.Act(() => ClientSeam.SetHeldDirections(c, 0));
        room.Step();
        room.In(() =>
        {
            Assert.Empty(Held(c));
            Assert.Equal(0f, N(c.intended_direction));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AFrameThatChangesNothingSendsNoKeyEventsAtAll()
    {
        // a hundred identical frames are one press: nothing reaches the keysend flood count
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() =>
        {
            for (int i = 0; i < 100; i++)
                ClientSeam.SetHeldDirections(c, North);
        });
        room.Step();
        room.In(() =>
        {
            Assert.Equal(0f, c.client_keysend_amount); // press 1 counted and reset the amount, nothing after it
            Assert.Equal(new[] { "North" }, Held(c));
            Assert.Null(c.gc_destroyed);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ABitOutsideTheFourDirectionsIsRefusedBeforeAnyKeyIsSent()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.In(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientSeam.SetHeldDirections(c, 16));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientSeam.SetHeldDirections(c, North | 32));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClientSeam.SetHeldDirections(c, -1));
            Assert.Empty(Held(c));
        });
    }

    [Fact]
    public void KeyEventsOfTheSeamRunWithUsrTheClientsMobAndGiveTheCallersUsrBack()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var seen = new List<string>();
        var listener = room.In(() => { var l = Dm.@new<datum_test_m1t1_listener>(); l.WatchKeyDown(avatar); return l; });
        room.Act(() =>
        {
            World.Current.usr = null;
            ClientSeam.SetHeldDirections(c, North);
            seen.Add("after: " + (World.Current.usr == null ? "null" : "set"));
        });
        room.Step();
        Assert.Equal(new[] { "after: null" }, seen);
        Assert.Single(listener.Trace);
        Assert.Same(avatar, listener.UsrAtKeyDown);
    }

    [Fact]
    public void AnInputFrameForADisconnectedClientIsIgnored()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.Act(() => ClientSeam.SetHeldDirections(c, North));
        room.Step(3);
        room.In(() =>
        {
            Assert.Empty(Held(c));
            Assert.Equal("8,8", T1Room.Where(avatar));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADisconnectOfAClientThatIsAlreadyGoneIsIgnored()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyMobGivesTheAvatarItsKeyAndCkeyAndTakesItFromAnyOtherMob()
    {
        var room = new T1Room();
        var a = room.NewMob(4, 4);
        var b = room.NewMob(6, 6);
        room.In(() =>
        {
            ClientSeam.KeyMob(a, "Some Player");
            Assert.Equal("Some Player", a.key);
            Assert.Equal("someplayer", a.ckey);
            ClientSeam.KeyMob(b, "Some Player");
            Assert.Null(a.key);
            Assert.Equal("Some Player", b.key);
        });
    }

    [Fact]
    public void TheHostIsToldOfTheJoinInOrderMobChangedThenNothingElse()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        Assert.Equal(new[] { "mob Tester null -> " + T1Host.Id(avatar) }, room.Host.Events);
    }

    [Fact]
    public void TheHostIsToldOfALeaveWithDeletedAndOnlyOnce()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Host.Events.Clear();
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        Assert.Equal(new[] { "deleted Tester" }, room.Host.Events);
    }

    [Fact]
    public void AWorldWithoutAHostRunsTheSameJoinAndLeave()
    {
        var room = new T1Room();
        ClientSeam.SetHost(room.World, null);
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => ClientSeam.Disconnect(c));
        room.Step();
        room.In(() => Assert.Null(avatar.client));
        Assert.Null(ClientSeam.HostOf(room.World));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheJoinWelcomeCanBeSentTheSameTickBecauseClientNewReturnsAtOnce()
    {
        // a Login that does not sleep is Done in the tick of the Connect call (the host's Welcome rule, ClientConnection.Accepted)
        var room = new T1Room();
        var avatar = room.NewMob(8, 8);
        ClientConnection connection = null;
        bool done = false, accepted = false;
        room.Act(() =>
        {
            ClientSeam.KeyMob(avatar, "Tester");
            connection = ClientSeam.Connect("Tester");
            done = connection.Done;
            accepted = connection.Accepted;
        });
        room.Step();
        Assert.True(done);
        Assert.True(accepted);
        room.AssertNoRuntimes();
    }
}

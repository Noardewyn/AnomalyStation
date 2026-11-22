using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Server.Tests.Replication;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.M4aServer.P6;

/// <summary>The client seam's version-4 bodies (Tg/Infrastructure/ClientSeam.Click.cs and .Actions.cs) against a real tg world and client.</summary>
public class ClientSeamV4Tests
{
    // An object that records the click and the hover it receives, with the usr at the time.
    private sealed class obj_probe : obj
    {
        public readonly List<(object Location, object Control, string Params, object Usr)> Clicks = new();
        public readonly List<(object Location, object Control, object Usr)> Hovers = new();

        public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
        {
            Clicks.Add((location, control, @params as string, World.Current.usr));
            return null;
        }

        public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
        {
            Hovers.Add((location, control, World.Current.usr));
        }
    }

    // ---- the params text ----

    [Theory]
    [InlineData("left", false, false, false, 0, 0, null, "left=1;button=left")]
    [InlineData("left", false, false, false, 16, 9, null, "icon-x=16;icon-y=9;left=1;button=left")]
    [InlineData("right", true, false, true, 32, 1, null, "icon-x=32;icon-y=1;right=1;shift=1;alt=1;button=right")]
    [InlineData("middle", false, true, false, 0, 5, null, "icon-y=5;middle=1;ctrl=1;button=middle")]
    [InlineData("left", false, false, false, 3, 4, "WEST,CENTER-2", "icon-x=3;icon-y=4;left=1;button=left;screen-loc=WEST,CENTER-2")]
    [InlineData("left", false, false, false, 0, 0, "EAST-1:28,CENTER-1:15", "left=1;button=left;screen-loc=EAST-1:28,CENTER-1:15")]
    [InlineData("left", false, false, false, 0, 0, "", "left=1;button=left")]
    [InlineData("left", false, false, false, 0, 0, "a;shift=1", "left=1;button=left")]       // a screen_loc that would add keys is left out
    [InlineData("left", false, false, false, 0, 0, "a&b", "left=1;button=left")]
    [InlineData("left", false, false, false, 0, 0, "a=b", "left=1;button=left")]
    public void ClickParamsHasTgsKeys(string button, bool shift, bool ctrl, bool alt, int iconX, int iconY, string screenLoc, string expected)
    {
        string text = ClientSeam.ClickParams(button, shift, ctrl, alt, iconX, iconY, screenLoc);
        Assert.Equal(expected, text);
        // tg reads it with params2list.
        DmList list = Dm.params2list(text);
        Assert.Equal(button, list[BUTTON]);
        Assert.Equal(iconX > 0 ? iconX.ToString() : null, list[ICON_X] is object x ? x.ToString() : null);
        Assert.Equal(iconY > 0 ? iconY.ToString() : null, list[ICON_Y] is object y ? y.ToString() : null);
    }

    [Fact]
    public void ClickParamsRefusesWhatIsOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientSeam.ClickParams("left", false, false, false, 33, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientSeam.ClickParams("left", false, false, false, 1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientSeam.ClickParams("thumb", false, false, false, 1, 1));
    }

    // ---- Click: the location of a HUD click ----

    [Fact]
    public void AClickOnAMapObjectHasItsTurfAndAClickOnAHudObjectHasNoLocation()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", run.In(() => Dm.@new<mob_living_carbon_human>(run.Turf(4, 4))));
        obj_probe onTheFloor = run.In(() => Dm.@new<obj_probe>(run.Turf(5, 4)));
        obj_probe onTheScreen = run.In(() =>
        {
            var o = Dm.@new<obj_probe>(null);
            o.screen_loc = "WEST,SOUTH";
            me.Client.screen.Add(o);
            return o;
        });
        turf cell = run.In(() => run.Turf(6, 4));

        run.In(() =>
        {
            ClientSeam.Click(me.Client, onTheFloor, ClientSeam.ClickParams(LEFT_CLICK, false, false, false, 5, 6));
            ClientSeam.Click(me.Client, onTheScreen, ClientSeam.ClickParams(LEFT_CLICK, false, false, false, 7, 8, onTheScreen.screen_loc));
        });
        run.Step();
        var floor = Assert.Single(onTheFloor.Clicks);
        Assert.Same(run.In(() => run.Turf(5, 4)), floor.Location);
        Assert.Equal(SKIN_MAPWINDOW_MAP, floor.Control);
        Assert.Equal("icon-x=5;icon-y=6;left=1;button=left", floor.Params);
        Assert.Same(me.Avatar, floor.Usr);

        var hud = Assert.Single(onTheScreen.Clicks);
        Assert.Null(hud.Location);
        Assert.Equal("icon-x=7;icon-y=8;left=1;button=left;screen-loc=WEST,SOUTH", hud.Params);
        Assert.Same(me.Avatar, hud.Usr);

        // A click on a turf: tg's /turf/Click is the atom's (the turf is its own location).
        run.In(() => ClientSeam.Click(me.Client, cell, ClientSeam.ClickParams(LEFT_CLICK, false, false, false, 16, 16)));
        run.Step();
        Assert.Equal(0, run.World.World.RuntimeErrorCount);
    }

    // ---- Hover ----

    [Fact]
    public void AHoverIsMouseEnteredWithTheMobAsUsrAndTheSameLocationRule()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", run.In(() => Dm.@new<mob_living_carbon_human>(run.Turf(4, 4))));
        obj_probe onTheFloor = run.In(() => Dm.@new<obj_probe>(run.Turf(5, 4)));
        obj_probe onTheScreen = run.In(() =>
        {
            var o = Dm.@new<obj_probe>(null);
            me.Client.screen.Add(o);
            return o;
        });
        run.In(() =>
        {
            ClientSeam.Hover(me.Client, onTheFloor);
            ClientSeam.Hover(me.Client, onTheScreen);
        });
        var floor = Assert.Single(onTheFloor.Hovers);
        Assert.Same(run.In(() => run.Turf(5, 4)), floor.Location);
        Assert.Equal(SKIN_MAPWINDOW_MAP, floor.Control);
        Assert.Same(me.Avatar, floor.Usr);
        Assert.Null(Assert.Single(onTheScreen.Hovers).Location);

        // Nothing for a gone object, a gone client or none.
        run.In(() => GlobalProcs.qdel(onTheFloor));
        run.Step();
        run.In(() => ClientSeam.Hover(me.Client, onTheFloor));
        Assert.Single(onTheFloor.Hovers);
        Assert.Throws<ArgumentNullException>(() => ClientSeam.Hover(null, onTheScreen));
        run.In(() => ClientSeam.Hover(me.Client, null));
        Assert.Equal(0, run.World.World.RuntimeErrorCount);
    }

    // ---- Keybinding ----

    [Fact]
    public void AKeybindingCodeRunsTgsBindingAndAnUnknownOneDoesNothing()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", run.In(() => Dm.@new<mob_living_carbon_human>(run.Turf(4, 4))));
        long before = run.World.World.RuntimeErrorCount;
        run.In(() =>
        {
            foreach (byte code in new byte[] { 0, 9, 200, 255 })
                ClientSeam.Keybinding(me.Client, code, true);
        });
        run.Step();
        Assert.Equal(before, run.World.World.RuntimeErrorCount);
        foreach (byte code in Enumerable.Range(1, 8).Select(i => (byte)i))
        {
            Assert.NotNull(ClientSeam.KeybindingType(code));
            run.In(() =>
            {
                ClientSeam.Keybinding(me.Client, code, true);
                ClientSeam.Keybinding(me.Client, code, false);
            });
            run.Step();
        }

        // Rest still goes through the same path (tg's keyDown of one binding): the mob lies down.
        run.In(() => ClientSeam.Rest(me.Client));
        for (int i = 0; i < 4; i++) run.Step();
        Assert.Equal(LYING_DOWN, ((mob_living)me.Avatar).body_position);
    }
}

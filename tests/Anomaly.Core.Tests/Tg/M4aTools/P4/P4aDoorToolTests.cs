using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tests.Tg.M4aAccess.P5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aTools.P4;

/// <summary>
/// M4a P4a: the door family's tool acts through the real click chain (tg's door.dm, airlock.dm and windowdoor.dm): a lit
/// welder's right click welds an airlock shut after tg's 4 second do_after and a second one unwelds it, a screwdriver
/// opens and closes the maintenance panel, a crowbar pries an unpowered unbolted airlock and is refused by a powered,
/// bolted or welded one, a windoor's panel needs the windoor open and a crowbar forces an unpowered windoor, and an ID card
/// used on a door opens it when the card has the access (item_interaction's try_to_activate_door).
/// </summary>
public class P4aDoorToolTests
{
    private const int Second = 20; // ticks per second
    private const string Left = "left=1;button=left";
    private const string Right = "right=1;button=right";

    private static void Click(CapsuleRoom room, mob who, atom target, string @params, int seconds = 1)
    {
        room.Command(() => ClientSeam.Click(who.client, target, @params));
        room.Step(seconds * Second);
    }

    private static obj_machinery_door_airlock Airlock(CapsuleRoom room)
    {
        var door = room.In(() => Dm.@new<obj_machinery_door_airlock>(locate(6, 5, 1)));
        room.Step(Second);
        return door;
    }

    private static bool Dense(CapsuleRoom room, obj_machinery_door door) => room.In(() => Dm.truthy(door.density));

    private static void CutPower(CapsuleRoom room, obj_machinery_door door) =>
        room.In(() => Waiting.NoSleep(door.set_machine_stat(Dm.bitor(door.machine_stat, NOPOWER))));

    [Fact]
    public void ALitWelderWeldsAnAirlockShutAndUnweldsIt()
    {
        var (room, a) = P4aToolTests.Room(4611);
        var door = Airlock(room);
        var welder = P4aToolTests.Hold<obj_item_weldingtool>(room, a);

        // unlit: tool_start_check refuses, nothing starts
        Click(room, a, door, Right, 6);
        Assert.False(room.In(() => Dm.truthy(door.welded)), "an unlit welder does not weld");

        room.In(() => Waiting.NoSleep(welder.attack_self(a)));
        float before = P4aToolTests.Fuel(room, welder);
        Click(room, a, door, Right, 2);
        Assert.False(room.In(() => Dm.truthy(door.welded)), "not before tg's 4 second do_after");
        Assert.Equal(1f, room.In(() => Dm.num(a.do_after_count())));
        room.Step(3 * Second);
        Assert.True(room.In(() => Dm.truthy(door.welded)), "welded shut after the do_after");
        Assert.Equal(0f, room.In(() => Dm.num(a.do_after_count())));
        Assert.True(P4aToolTests.Fuel(room, welder) <= before, "the weld itself takes no fuel (use_tool's amount 0), the lit welder burns it");
        Assert.True(Dense(room, door));

        // a welded airlock does not open to a hand
        Click(room, a, door, Left, 2);
        Assert.True(Dense(room, door), "a welded airlock stays shut");

        Click(room, a, door, Right, 6);
        Assert.False(room.In(() => Dm.truthy(door.welded)), "a second weld unwelds it");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AScrewdriverOpensAndClosesTheAirlocksPanel()
    {
        var (room, a) = P4aToolTests.Room(4612);
        var door = Airlock(room);
        P4aToolTests.Hold<obj_item_screwdriver>(room, a);

        Click(room, a, door, Left);
        Assert.True(room.In(() => Dm.truthy(door.panel_open)), "the screwdriver opens the panel");
        Assert.True(Dense(room, door), "and does not open the door");
        Click(room, a, door, Left);
        Assert.False(room.In(() => Dm.truthy(door.panel_open)), "and closes it again");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACrowbarPriesAnUnpoweredAirlockButNotAPoweredBoltedOrWeldedOne()
    {
        var (room, a) = P4aToolTests.Room(4613);
        var door = Airlock(room);
        P4aToolTests.Hold<obj_item_crowbar>(room, a);

        Click(room, a, door, Left, 3);
        Assert.True(Dense(room, door), "the powered airlock's motors resist a crowbar");

        CutPower(room, door);
        room.In(() => door.bolt());
        Click(room, a, door, Left, 3);
        Assert.True(Dense(room, door), "the bolts prevent forcing it");
        room.In(() => door.unbolt());

        room.In(() => door.welded = TRUE);
        Click(room, a, door, Left, 3);
        Assert.True(Dense(room, door), "a welded airlock won't budge");
        room.In(() => door.welded = FALSE);

        Click(room, a, door, Left, 3);
        Assert.False(Dense(room, door), "an unpowered, unbolted airlock is pried open");
        Click(room, a, door, Left, 3);
        Assert.True(Dense(room, door), "and pried shut again");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AWindoorsPanelNeedsItOpenAndACrowbarForcesItWithoutPower()
    {
        var (room, a) = P4aToolTests.Room(4614);
        var windoor = room.In(() => Dm.@new<obj_machinery_door_window>(locate(6, 5, 1)));
        room.Step(Second);
        P4aToolTests.Hold<obj_item_screwdriver>(room, a);

        Click(room, a, windoor, Left);
        Assert.False(room.In(() => Dm.truthy(windoor.panel_open)), "a closed windoor's panel cannot be reached");

        P4aToolTests.Hold<obj_item_crowbar>(room, a);
        Click(room, a, windoor, Left, 3);
        Assert.True(Dense(room, windoor), "a powered windoor's motors resist");
        CutPower(room, windoor);
        Click(room, a, windoor, Left, 3);
        Assert.False(Dense(room, windoor), "the crowbar forces the unpowered windoor open");

        P4aToolTests.Hold<obj_item_screwdriver>(room, a);
        Click(room, a, windoor, Left);
        Assert.True(room.In(() => Dm.truthy(windoor.panel_open)), "the open windoor's panel opens");
        Click(room, a, windoor, Left);
        Assert.False(room.In(() => Dm.truthy(windoor.panel_open)), "and closes");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnIdCardUsedOnADoorOpensItWithTheAccessAndNotWithout()
    {
        var (room, a) = P4aToolTests.Room(4615);
        var door = room.In(() =>
        {
            var d = Dm.@new<obj_machinery_door_airlock>(locate(6, 5, 1));
            d.req_access = Dm.list(ACCESS_CAPTAIN);
            return d;
        });
        room.Step(Second);

        var card = room.In(() => IdCards.Hold(a, ACCESS_ENGINEERING));
        Click(room, a, door, Left, 2);
        Assert.True(Dense(room, door), "a card without the access is denied");

        card = room.In(() => IdCards.Hold(a, ACCESS_CAPTAIN));
        Assert.Same(card, room.In(() => a.get_active_held_item()));
        Click(room, a, door, Left, 2);
        Assert.False(Dense(room, door), "the swipe of a card with the access opens the door");
        room.AssertNoRuntimes();
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// The owner's play test (2026-10-10): a click on an airlock through the real click chain. tg's door attack_hand
/// (door.dm:306) asks try_to_activate_door: a door the clicker may use opens, a second click closes it; a door that needs
/// access the clicker lacks stays shut (the deny animation).
/// </summary>
public class DoorClickTests
{
    private const int Second = 20;

    private static void Click(CapsuleRoom room, mob who, atom target)
    {
        room.Command(() => ClientSeam.Click(who.client, target, "left=1;button=left"));
        room.Step(2 * Second);
    }

    [Fact]
    public void AClickOpensAnAirlockAndAnotherClosesIt()
    {
        var room = new CapsuleRoom(4321);
        mob_living_carbon_human alice = room.Join<mob_living_carbon_human>(5, 5, "Alice");
        var door = room.In(() => Dm.@new<obj_machinery_door_airlock>(locate(6, 5, 1)));
        room.Step(Second);
        Assert.True(room.In(() => Dm.truthy(door.density)));

        Click(room, alice, door);
        Assert.False(room.In(() => Dm.truthy(door.density)), "the click opens the airlock");

        Click(room, alice, door);
        Assert.True(room.In(() => Dm.truthy(door.density)), "a second click closes it");
        Assert.Equal(0, room.World.RuntimeErrorCount);
    }

    [Fact]
    public void AnAirlockThatNeedsAccessStaysShutWithoutIt()
    {
        var room = new CapsuleRoom(4322);
        mob_living_carbon_human alice = room.Join<mob_living_carbon_human>(5, 5, "Alice");
        var door = room.In(() =>
        {
            var d = Dm.@new<obj_machinery_door_airlock>(locate(6, 5, 1));
            d.req_access = Dm.list(ACCESS_CAPTAIN);
            return d;
        });
        room.Step(Second);

        Click(room, alice, door);
        Assert.True(room.In(() => Dm.truthy(door.density)), "no access: the airlock stays shut");
        Assert.Equal(0, room.World.RuntimeErrorCount);
    }
}

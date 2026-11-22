using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aTools.P4;

/// <summary>
/// M4a P4a: a light fixture's tube removed by hand (tg's light.dm:584 attack_hand_secondary, a right click): a lit tube
/// burns the bare hand for 5 burn and stays, an unlit one comes out into the active hand and leaves the fixture empty, and
/// an empty fixture has nothing to give.
/// </summary>
public class P4aLightTests
{
    private const int Second = 20;
    private const string Right = "right=1;button=right";

    [Fact]
    public void ALitTubeBurnsTheBareHandAndAnUnlitOneComesOutIntoIt()
    {
        var (room, a) = P4aToolTests.Room(4621);
        var lamp = room.In(() =>
        {
            var l = Dm.@new<obj_machinery_light>(locate(6, 5, 1));
            l.allow_break_on_init = FALSE;
            return l;
        });
        room.Step(Second);
        room.In(() =>
        {
            lamp.status = LIGHT_OK;
            lamp.on = TRUE;
        });

        room.Command(() => ClientSeam.Click(a.client, lamp, Right));
        room.Step(Second);
        Assert.Equal(5f, room.In(() => Dm.num(a.get_fire_loss())));
        Assert.Equal(LIGHT_OK, room.In(() => lamp.status));
        Assert.Null(room.In(() => a.get_active_held_item()));

        room.In(() => lamp.on = FALSE);
        room.Command(() => ClientSeam.Click(a.client, lamp, Right));
        room.Step(Second);
        Assert.Equal(LIGHT_EMPTY, room.In(() => lamp.status));
        var tube = room.In(() => a.get_active_held_item() as obj_item_light);
        Assert.NotNull(tube);
        Assert.Equal(LIGHT_OK, room.In(() => tube.status));
        Assert.Equal(5f, room.In(() => Dm.num(a.get_fire_loss()))); // an unlit tube does not burn

        room.In(() => Waiting.NoSleep(a.dropItemToGround(tube)));
        room.Command(() => ClientSeam.Click(a.client, lamp, Right));
        room.Step(Second);
        Assert.Null(room.In(() => a.get_active_held_item()));
        room.AssertNoRuntimes();
    }
}

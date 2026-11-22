using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// M4b slice 2 (the owner's play test, 2026-10-10): a fire alarm through the real click chain. tg's left click pulls it
/// (firealarm.dm:288 attack_hand: alarm), tg's right click resets it (:295 attack_hand_secondary: reset), which clears the
/// area's fire alarm and cracks the area's firelocks open, and the alarm's light and picture go back to normal.
/// </summary>
public class FireAlarmClickTests
{
    [Fact]
    public void ALeftClickPullsTheAlarmAndARightClickResetsIt()
    {
        var room = new CapsuleRoom(4311);
        mob_living_carbon_human alice = room.Join<mob_living_carbon_human>(5, 5, "Alice");
        // mapload: a built alarm is an empty frame with its panel open (firealarm.dm:62), as Meta's are not
        var alarm = room.In(() =>
        {
            SSatoms.set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, "test map");
            var a = Dm.@new<obj_machinery_firealarm>(locate(6, 5, 1));
            SSatoms.clear_tracked_initalize("test map");
            return a;
        });
        var firelock = room.In(() => Dm.@new<obj_machinery_door_firedoor>(locate(5, 7, 1)));
        room.Step(20);
        area place = room.In(() => alarm.my_area);

        room.Command(() => ClientSeam.Click(alice.client, alarm, "left=1;button=left"));
        room.Step(3 * 20);
        Assert.Equal(1f, room.In(() => Dm.num(place.active_alarms[ALARM_FIRE])));
        Assert.True(room.In(() => Dm.truthy(firelock.density)), "the pulled alarm closes the area's firelock");
        Assert.True(room.In(() => Dm.truthy(place.fire) || Dm.truthy(LAZYLEN(place.active_firelocks))), "the area is alarmed");

        room.Command(() => ClientSeam.Click(alice.client, alarm, "right=1;button=right"));
        room.Step(5 * 20);
        Assert.Equal(0f, room.In(() => Dm.num(place.active_alarms[ALARM_FIRE])));
        Assert.False(room.In(() => Dm.truthy(firelock.density)), "the reset cracks the firelock open");
        Assert.False(room.In(() => Dm.truthy(place.fire) || Dm.truthy(LAZYLEN(place.active_firelocks))), "the area is no longer alarmed");
        Assert.Equal(0, room.World.RuntimeErrorCount); // the left click reached can_interact_with and interact without a runtime
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// M4b slice 2 (the firelocks): tg's firelocks in a booted 16 x 16 toy room split by a wall with two doorways, each holding
/// a firelock. A cold neighbour turf (at or below BODYTEMP_COLD_DAMAGE_LIMIT) closes the whole row of firelocks (tg's merge
/// group), turns the area's fire effect on and sends the area's fire alarm; while the shared air stays that cold the row
/// stays shut, and warm air opens it again. A pulled
/// fire alarm closes the area's firelocks; its right click cracks them open, and with no problem left they stay open.
/// </summary>
public class FirelockTests
{
    private const int Second = 20; // ticks per second

    // A west half (x 2..7) and an east half (x 9..15) split by a wall at x = 8 with doorways at (8, 7) and (8, 8): a row of
    // two firelocks, one per doorway.
    private static (obj_machinery_door_firedoor a, obj_machinery_door_firedoor b) Build(T2Room r)
    {
        r.In(() =>
        {
            for (int y = 2; y < T2Room.Size; y++)
                if (y != 7 && y != 8)
                    Waiting.NoSleep(locate(8, y, 1).ChangeTurf(typeof(turf_closed_indestructible)));
        });
        var a = r.In(() => Dm.@new<obj_machinery_door_firedoor>(locate(8, 7, 1)));
        var b = r.In(() => Dm.@new<obj_machinery_door_firedoor>(locate(8, 8, 1)));
        r.Step(Second);
        return (a, b);
    }

    private static void SetTemperature(T2Room r, int x, int y, float kelvin) => r.In(() =>
    {
        var t = (turf_open)locate(x, y, 1);
        t.air.temperature = kelvin;
        SSair.add_to_active(t);
    });

    private static bool Closed(T2Room r, obj_machinery_door_firedoor d) => r.In(() => Dm.truthy(d.density));

    [Fact]
    public void TheRowOfFirelocksStartsOpenAndInOneMergeGroup()
    {
        var r = new T2Room(4301);
        var (a, b) = Build(r);
        Assert.False(Closed(r, a));
        Assert.False(Closed(r, b));
        datum_merger group = r.In(() => Waiting.NoSleep(a.GetMergeGroup(a.merger_id, obj_machinery_door_firedoor.merger_typecache)) as datum_merger);
        Assert.Equal(2, r.In(() => group.members.len));
        Assert.Contains(a, r.In(() => a.my_area.firedoors.CopyItems()));
        Assert.Contains("firelock", r.In(() => a.name).ToString());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AColdTurfClosesTheRowAndWarmAirOpensIt()
    {
        var r = new T2Room(4302);
        var (a, b) = Build(r);
        area room = r.In(() => a.my_area);
        // a cold block east of the row (x 9..11, y 5..10, 80 K): it stays under BODYTEMP_COLD_DAMAGE_LIMIT for a while after
        // it starts sharing (tg checks a turf after its share, LINDA_turf_tile.dm:373)
        for (int x = 9; x <= 11; x++)
            for (int y = 5; y <= 10; y++)
                SetTemperature(r, x, y, 80);
        r.Step(3 * Second);
        Assert.True(Closed(r, a), "the cold neighbour closes its firelock");
        Assert.True(Closed(r, b), "and the rest of the row");
        Assert.Equal(FIRELOCK_ALARM_TYPE_COLD, r.In(() => a.alarm_type));
        Assert.True(r.In(() => Dm.truthy(room.fire)), "the area's fire effect is on (the lights go red)");
        Assert.True(r.In(() => Dm.truthy(room.active_alarms[ALARM_FIRE])), "the area's fire alarm was sent");

        r.Step(30 * Second);
        // the east half shares the cold out to about 256 K, still under the limit: tg keeps the row shut
        Assert.True(Closed(r, a), "the shared cold keeps the row shut: " + r.In(() => ((turf_open)locate(9, 7, 1)).air.temperature));
        for (int x = 9; x < T2Room.Size; x++)
            for (int y = 2; y < T2Room.Size; y++)
                SetTemperature(r, x, y, T20C);
        r.Step(10 * Second);
        Assert.False(Closed(r, a), "warm air opens the firelock again: " + r.In(() => ((turf_open)locate(9, 7, 1)).air.temperature));
        Assert.False(Closed(r, b));
        Assert.False(r.In(() => Dm.truthy(room.fire)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APulledFireAlarmClosesTheAreasFirelocksAndItsResetOpensThem()
    {
        var r = new T2Room(4303);
        var (a, b) = Build(r);
        var alarm = r.In(() => Dm.@new<obj_machinery_firealarm>(locate(4, 4, 1)));
        r.Step(Second);
        r.In(() => alarm.alarm());
        r.Step(3 * Second);
        Assert.True(Closed(r, a));
        Assert.True(Closed(r, b));
        Assert.Equal(AREA_FAULT_MANUAL, r.In(() => a.my_area.fault_status));

        r.In(() => alarm.reset());
        r.Step(3 * Second);
        Assert.False(Closed(r, a), "the reset cracks the firelocks open");
        r.Step(10 * Second);
        Assert.False(Closed(r, a), "with no problem left they stay open");
        Assert.False(Closed(r, b));
        r.AssertNoRuntimes();
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4bAudio;

/// <summary>
/// The door sounds restored in M4b audio slice 1: the airlock's open, close, bolts and deny (airlock.dm:261, :279, :663-669),
/// heard with tg's falloff by a player 5 tiles away.
/// </summary>
public class DoorSoundTests
{
    private const int Second = 20;

    [Fact]
    public void AnAirlockOpeningIsHeardFiveTilesAway()
    {
        var room = new AudioRoom(16, 8, seed: 5401);
        mob_living_carbon_human listener = room.Join(10, 4, "Listener");
        var door = room.In(() => Dm.@new<obj_machinery_door_airlock>(locate(5, 4, 1)));
        room.Step(Second);
        room.Sounds.Heard.Clear();

        room.In(() => Waiting.Detach(door.open()));
        room.Step(2 * Second);
        Assert.False(room.In(() => Dm.truthy(door.density)), "the airlock opened");
        HeardSound open = Assert.Single(room.Sounds.Of(listener, "sound/machines/airlock/airlock.ogg"));
        Assert.Equal(13.890, open.Volume, 3); // vol 30, 5 tiles of 15, exponent 2.5, min_volume 5
        Assert.Equal(-5, open.X);
        Assert.InRange(open.Frequency, 32000, 55000); // vary = TRUE

        room.Sounds.Heard.Clear();
        room.In(() => Waiting.Detach(door.close()));
        room.Step(2 * Second);
        Assert.True(room.In(() => Dm.truthy(door.density)), "the airlock closed");
        Assert.Single(room.Sounds.Of(listener, "sound/machines/airlock/airlockclose.ogg"));

        room.Sounds.Heard.Clear();
        room.In(() => door.bolt());
        room.In(() => door.unbolt());
        HeardSound down = Assert.Single(room.Sounds.Of(listener, "sound/machines/airlock/boltsdown.ogg"));
        HeardSound up = Assert.Single(room.Sounds.Of(listener, "sound/machines/airlock/boltsup.ogg"));
        // vol 30, extrarange 3 (maxdistance 18), no vary
        double expected = 30 - Math.Pow(5.0 / 18, 1 / 2.5) * 25;
        Assert.Equal(expected, down.Volume, 3);
        Assert.Equal(expected, up.Volume, 3);
        Assert.Equal(0, down.Frequency);
        room.AssertNoRuntimes();
    }
}

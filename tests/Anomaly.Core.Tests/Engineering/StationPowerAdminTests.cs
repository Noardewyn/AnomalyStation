using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// The admins' station-wide power: power_failure, power_restore and power_restore_quick (gamemodes/events.dm) on an APC, a SMES and their
/// area of a station level, and an APC off the station that they leave alone.
/// </summary>
public class StationPowerAdminTests
{
    private const int Second = 20;
    private const int X = 3, Y = 2;

    private sealed record Rig(AudioRoom R, obj_machinery_power_apc Apc, obj_machinery_power_smes Smes, area Room);

    private static Rig Build(ulong seed)
    {
        obj_machinery_power_apc apc = null;
        obj_machinery_power_smes smes = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(X, Y, 1));
            smes = Dm.@new<obj_machinery_power_smes>(locate(X + 3, Y, 1));
        });
        // the room's level has no traits: is_station_level's cache says it is the station's
        r.In(() => { GLOB.station_levels_cache.len = 1; GLOB.station_levels_cache[1] = TRUE; });
        r.Step(Second);
        return new Rig(r, apc, smes, r.In(() => (area)GLOB.areas_by_type[typeof(area_misc_testroom)]));
    }

    [Fact]
    public void AFailureDrainsTheStationAndARestoreFillsItBack()
    {
        Rig g = Build(8301);
        AudioRoom r = g.R;
        Assert.True(r.In(() => Dm.truthy(is_station_level(g.Apc.z))), "the room is not a station level");
        r.In(() => Waiting.NoSleep(g.Smes.adjust_charge(-INFINITY)));
        r.In(() => Waiting.NoSleep(power_failure()));
        Assert.Equal(0, r.In(() => g.Apc.cell.charge));
        Assert.Equal(0, r.In(() => g.Smes.output_level));
        Assert.False(r.In(() => Dm.truthy(g.Smes.output_attempt)));
        Assert.Equal(FALSE, r.In(() => g.Room.power_light));
        Assert.Equal(FALSE, r.In(() => g.Room.power_equip));
        Assert.Equal(FALSE, r.In(() => g.Room.power_environ));

        r.In(() => g.Apc.failure_timer = 30);
        r.In(() => Waiting.NoSleep(power_restore()));
        Assert.Equal(r.In(() => g.Apc.cell.maxcharge), r.In(() => g.Apc.cell.charge));
        Assert.Equal(0, r.In(() => g.Apc.failure_timer));
        Assert.Equal(r.In(() => Dm.num(g.Smes.total_capacity)), r.In(() => Dm.num(g.Smes.total_charge())));
        Assert.Equal(r.In(() => g.Smes.output_level_max), r.In(() => g.Smes.output_level));
        Assert.True(r.In(() => Dm.truthy(g.Smes.output_attempt)));
        Assert.Equal(TRUE, r.In(() => g.Room.power_light));
    }

    [Fact]
    public void AQuickRestoreRechargesOnlyTheSmes()
    {
        Rig g = Build(8302);
        AudioRoom r = g.R;
        r.In(() => Waiting.NoSleep(g.Smes.adjust_charge(-INFINITY)));
        r.In(() => Waiting.NoSleep(power_failure()));
        r.In(() => Waiting.NoSleep(power_restore_quick()));
        Assert.Equal(r.In(() => Dm.num(g.Smes.total_capacity)), r.In(() => Dm.num(g.Smes.total_charge())));
        Assert.True(r.In(() => Dm.truthy(g.Smes.output_attempt)));
        Assert.Equal(0, r.In(() => g.Apc.cell.charge));
    }

    [Fact]
    public void AnAreaThatBlocksTheFailureKeepsItsPower()
    {
        Rig g = Build(8303);
        AudioRoom r = g.R;
        r.In(() => ADD_TRAIT(g.Room, TRAIT_AREA_BLOCK_POWER_FAIL, "test"));
        float charge = r.In(() => g.Apc.cell.charge);
        r.In(() => Waiting.NoSleep(power_failure()));
        Assert.Equal(charge, r.In(() => g.Apc.cell.charge));
        Assert.Equal(TRUE, r.In(() => g.Room.power_light));
    }
}

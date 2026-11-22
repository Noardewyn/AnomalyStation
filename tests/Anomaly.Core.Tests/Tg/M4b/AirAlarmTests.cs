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
/// M4b slice 2 (the air alarms): tg's air alarm in a booted 16 x 16 toy room. In the room's normal air it reads no danger
/// and starts in the filtering mode; when its turf's air goes to a vacuum it reads a hazard, sends the area's atmos alarm
/// and sets tg's warning text; good air again clears the alarm. A selected mode drives the area's air vents and
/// scrubbers (siphon: the vents off, the scrubbers siphoning; filtering: back to tg's defaults).
/// </summary>
public class AirAlarmTests
{
    private const int Second = 20; // ticks per second

    private static obj_machinery_airalarm Alarm(T2Room r, int x, int y) => r.In(() => Dm.@new<obj_machinery_airalarm>(locate(x, y, 1)));

    private static void SetAir(T2Room r, int x0, int x1, string mix) => r.In(() =>
    {
        for (int x = x0; x <= x1; x++)
            for (int y = 2; y < T2Room.Size; y++)
                if (locate(x, y, 1) is turf_open t)
                {
                    t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
                    SSair.add_to_active(t);
                }
    });

    [Fact]
    public void InNormalAirTheAlarmReadsNoDangerAndFilters()
    {
        var r = new T2Room(4401);
        var alarm = Alarm(r, 8, 8);
        r.Step(2 * Second);
        Assert.Equal(AIR_ALARM_ALERT_NONE, r.In(() => alarm.danger_level));
        Assert.IsType<datum_air_alarm_mode_filtering>(r.In(() => alarm.selected_mode));
        Assert.Equal(0f, r.In(() => Dm.num(alarm.my_area.active_alarms[ALARM_ATMOS])));
        Assert.Contains(alarm, r.In(() => GLOB.air_alarms.CopyItems()));
        Assert.Contains("Air Alarm", r.In(() => alarm.name).ToString());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AVacuumIsAHazardThatSendsTheAreasAtmosAlarmAndGoodAirClearsIt()
    {
        var r = new T2Room(4402);
        var alarm = Alarm(r, 8, 8);
        r.Step(Second);
        SetAir(r, 2, 15, AIRLESS_ATMOS);
        r.Step(2 * Second);
        Assert.Equal(AIR_ALARM_ALERT_HAZARD, r.In(() => alarm.danger_level));
        Assert.Equal(1f, r.In(() => Dm.num(alarm.my_area.active_alarms[ALARM_ATMOS])));
        Assert.Equal("Danger! Low pressure and temperature detected.", r.In(() => alarm.warning_message));

        SetAir(r, 2, 15, OPENTURF_DEFAULT_ATMOS);
        r.Step(2 * Second);
        Assert.Equal(AIR_ALARM_ALERT_NONE, r.In(() => alarm.danger_level));
        Assert.Null(r.In(() => alarm.warning_message));
        Assert.Equal(0f, r.In(() => Dm.num(alarm.my_area.active_alarms[ALARM_ATMOS])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASelectedModeDrivesTheAreasVentsAndScrubbers()
    {
        var r = new T2Room(4403);
        var vent = r.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_vent_pump_on>(locate(4, 4, 1)));
        var scrubber = r.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_vent_scrubber_on>(locate(6, 4, 1)));
        // each on a pipe south of it (their dir, SOUTH), joined as tg joins a template's machines: a scrubber without a node
        // turns itself off at its next atmos check (vent_scrubber.dm:186)
        var pipe1 = r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(4, 3, 1)));
        var pipe2 = r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(6, 3, 1)));
        r.In(() => SSair.setup_template_machinery(Dm.list(vent, scrubber, pipe1, pipe2)));
        var alarm = Alarm(r, 8, 8);
        Assert.Contains(vent, r.In(() => alarm.my_area.air_vents.CopyItems()));
        Assert.Contains(scrubber, r.In(() => alarm.my_area.air_scrubbers.CopyItems()));

        r.In(() => alarm.select_mode(alarm, typeof(datum_air_alarm_mode_siphon)));
        Assert.Equal(0f, r.In(() => vent.on));
        Assert.Equal(1f, r.In(() => scrubber.on));
        Assert.Equal(ATMOS_DIRECTION_SIPHONING, r.In(() => scrubber.scrubbing));

        r.In(() => alarm.select_mode(alarm, typeof(datum_air_alarm_mode_filtering)));
        Assert.Equal(1f, r.In(() => vent.on));
        Assert.Equal(ONE_ATMOSPHERE, r.In(() => vent.external_pressure_bound));
        Assert.Equal(ATMOS_DIRECTION_SCRUBBING, r.In(() => scrubber.scrubbing));
        Assert.Equal(new object[] { typeof(datum_gas_carbon_dioxide) }, r.In(() => scrubber.filter_types.CopyItems().ToArray()));

        // the flood mode needs an emagged alarm (air_alarm_modes.dm:195): the selection is refused
        r.In(() => alarm.select_mode(alarm, typeof(datum_air_alarm_mode_flood)));
        Assert.IsType<datum_air_alarm_mode_filtering>(r.In(() => alarm.selected_mode));
        r.AssertNoRuntimes();
    }
}

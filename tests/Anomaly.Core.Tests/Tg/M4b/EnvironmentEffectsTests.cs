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
/// M4b slice 1 (D134): the air's effects on a human, through tg's species procs: in a vacuum the low pressure hurts (brute
/// at <c>BASE_LOW_PRESSURE_DAMAGE</c>) with tg's low pressure alert, and the skin cools to tg's cold alert and the cold
/// damage; a very high pressure hurts with the high pressure alert; normal air gives no pressure alert.
/// </summary>
public class EnvironmentEffectsTests
{
    private const int Second = 20;

    private static object AlertOf(T2Room r, mob who, string category) => r.In(() => who.alerts?[category]);

    [Fact]
    public void NormalAirGivesNoPressureOrTemperatureAlert()
    {
        var r = new T2Room(4201);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        r.Step(10 * Second);
        Assert.Null(AlertOf(r, bob, ALERT_PRESSURE));
        Assert.Null(AlertOf(r, bob, ALERT_TEMPERATURE));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_brute_loss())));
        // tg's skin and core exchange (_species.dm:1070-1140) settles a human at 20 C a few degrees under BODYTEMP_NORMAL, far
        // from either damage limit
        Assert.InRange(r.In(() => bob.bodytemperature), BODYTEMP_COLD_DAMAGE_LIMIT + 20, BODYTEMP_HEAT_DAMAGE_LIMIT - 20);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AVacuumHurtsWithTheLowPressureAlertAndTheSkinFreezes()
    {
        var r = new T2Room(4202);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        r.Step(Second);
        r.In(() => DevCommands.Gas(AIRLESS_ATMOS));
        r.Step(6 * Second);
        object pressureAlert = AlertOf(r, bob, ALERT_PRESSURE);
        Assert.IsType<atom_movable_screen_alert_lowpressure>(pressureAlert);
        Assert.Equal(2f, r.In(() => ((atom_movable_screen_alert)pressureAlert).severity));
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > 0f, "the vacuum hurts (handle_environment_pressure)");

        r.Step(40 * Second);
        Assert.True(r.In(() => bob.bodytemperature) < BODYTEMP_COLD_DAMAGE_LIMIT, "the skin cools in the vacuum: " + r.In(() => bob.bodytemperature));
        Assert.IsType<atom_movable_screen_alert_cold>(AlertOf(r, bob, ALERT_TEMPERATURE));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AVeryHighPressureHurtsWithTheHighPressureAlert()
    {
        var r = new T2Room(4203);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        r.Step(Second);
        r.In(() => DevCommands.Gas("o2=900;n2=3400;TEMP=293.15"));
        r.Step(6 * Second);
        object pressureAlert = AlertOf(r, bob, ALERT_PRESSURE);
        Assert.IsType<atom_movable_screen_alert_highpressure>(pressureAlert);
        Assert.Equal(2f, r.In(() => ((atom_movable_screen_alert)pressureAlert).severity));
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) > 0f, "the high pressure hurts");
        r.AssertNoRuntimes();
    }
}

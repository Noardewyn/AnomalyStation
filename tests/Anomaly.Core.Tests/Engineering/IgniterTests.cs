using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.M5.FurnitureTests;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Package E8 (igniter.dm; doorcontrol.dm's igniter controller) in a booted toy room: the ignition switch's controller toggles the
/// floor igniters of its id (not the others), every 3 seconds at most; a burning igniter keeps its turf exposed to 1000 K, so the
/// plasma in the air catches fire; a hand toggles it too. The wall sparker of the same id sparks (the sparks and their sound) and
/// sets a plasma tile alight, at most once in 5 seconds; a screwdriver disconnects it; an EMP sparks it. A welder takes both apart
/// into what tg drops.
/// </summary>
public class IgniterTests
{
    private const int Second = 20;

    // 20 moles of plasma per tile in normal air (FireTests' lean mix)
    private const string PlasmaAir = "o2=22;n2=82;plasma=20;TEMP=293.15";

    private static void SetAir(AudioRoom r, int x, int y, string mix) => r.In(() =>
    {
        var t = (turf_open)locate(x, y, 1);
        t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
        SSair.add_to_active(t);
    });

    private static turf_open At(AudioRoom r, int x, int y) => r.In(() => (turf_open)locate(x, y, 1));

    private static obj_item_assembly_control_igniter Switch(AudioRoom r, object id) => r.In(() =>
    {
        var device = Dm.@new<obj_item_assembly_control_igniter>(locate(1, 1, 1));
        device.id = id;
        return device;
    });

    private static void Press(AudioRoom r, obj_item_assembly_control_igniter device) => r.In(() => Waiting.NoSleep(device.activate()));

    [Fact]
    public void TheIgnitionSwitchTogglesItsIgnitersWhichSetThePlasmaAlight()
    {
        var (r, bob) = Room(8811);
        var igniter = Place<obj_machinery_igniter_incinerator_atmos>(r, 7, 4);
        var other = Place<obj_machinery_igniter>(r, 9, 4);
        r.Step(Second);
        Assert.Equal(("igniter0", 0f), r.In(() => (Dm.str(igniter.icon_state), igniter.on)));
        var device = Switch(r, INCINERATOR_ATMOS_IGNITER);

        Press(r, device);
        r.In(() =>
        {
            Assert.Equal(1f, igniter.on);
            Assert.Equal("igniter1", Dm.str(igniter.icon_state));
            Assert.True(Dm.truthy(Dm.bitand(igniter.datum_flags, DF_ISPROCESSING)));
            Assert.Equal(0f, other.on);
        });
        // the controller's 3-second cooldown: a second press now does nothing
        Press(r, device);
        Assert.Equal(1f, r.In(() => igniter.on));

        // the plasma on its turf catches fire at the next SSmachines tick (hotspot_expose(1000, 500))
        SetAir(r, 7, 4, PlasmaAir);
        float before = r.In(() => At(r, 7, 4).air.temperature);
        r.Step(3 * Second);
        Assert.NotNull(r.In(() => At(r, 7, 4).active_hotspot));
        Assert.True(r.In(() => At(r, 7, 4).air.temperature) > before + 100, "the burning plasma heats the air");

        // after the cooldown the switch turns it off and it stops processing
        r.Step(Second);
        Press(r, device);
        r.In(() =>
        {
            Assert.Equal(0f, igniter.on);
            Assert.Equal("igniter0", Dm.str(igniter.icon_state));
            Assert.False(Dm.truthy(Dm.bitand(igniter.datum_flags, DF_ISPROCESSING)));
        });

        // a hand toggles the other one (attack_hand :104)
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(9, 5, 1))));
        Click(r, bob, other, Left, 1);
        Assert.Equal((1f, "igniter1"), r.In(() => (other.on, Dm.str(other.icon_state))));
        Click(r, bob, other, Left, 1);
        Assert.Equal(0f, r.In(() => other.on));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSparkerSparksByItsIdOnceInFiveSecondsUnlessDisconnectedAndAnEmpSparksIt()
    {
        var (r, bob) = Room(8812);
        var sparker = Place<obj_machinery_sparker_directional_north>(r, 7, 4);
        r.Step(Second);
        r.In(() => sparker.id = "Xenobio");
        Assert.Equal("migniter", r.In(() => Dm.str(sparker.icon_state)));
        SetAir(r, 7, 4, PlasmaAir);
        r.Sounds.Heard.Clear();

        Press(r, Switch(r, "Xenobio"));
        r.Step(2);
        Assert.InRange(r.In(() => sparker.last_spark), r.In(() => world.time) - 1, r.In(() => world.time) - 0.5f); // a tick is 0.5 ds
        Assert.NotNull(r.In(() => At(r, 7, 4).active_hotspot)); // hotspot_expose(1000, 2500)
        Assert.Contains(r.Sounds.Heard, h => h.File.StartsWith("sound/effects/sparks/", StringComparison.Ordinal)); // the spark's own sound
        // within 5 seconds of the last spark it does not spark again
        Assert.Equal(0f, Dm.num(r.In(() => Waiting.NoSleep(sparker.ignite()))));

        // a screwdriver disconnects it: "-d", not powered, no spark
        r.Step(6 * Second);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(7, 3, 1))));
        var screwdriver = Hold<obj_item_screwdriver>(r, bob);
        r.In(() => Waiting.NoSleep(sparker.screwdriver_act(bob, screwdriver)));
        r.In(() =>
        {
            Assert.Equal(1f, sparker.disable);
            Assert.Equal("migniter-d", Dm.str(sparker.icon_state));
            Assert.False(Dm.truthy(sparker.powered__proc()));
            Assert.Equal(0f, Dm.num(Waiting.NoSleep(sparker.ignite())));
        });
        r.In(() => Waiting.NoSleep(sparker.screwdriver_act(bob, screwdriver)));
        Assert.Equal((0f, "migniter"), r.In(() => (sparker.disable, Dm.str(sparker.icon_state))));

        // an EMP sparks it (emp_act :267)
        float last = r.In(() => sparker.last_spark);
        r.In(() => Waiting.NoSleep(sparker.emp_act(EMP_HEAVY)));
        Assert.True(r.In(() => sparker.last_spark) > last, "the EMP sparked it");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWelderTakesTheIgniterAndTheSparkerApart()
    {
        var (r, bob) = Room(8813);
        var igniter = Place<obj_machinery_igniter>(r, 5, 4);
        var sparker = Place<obj_machinery_sparker>(r, 6, 4);
        r.Step(Second);
        Hold<obj_item_weldingtool>(r, bob);

        // igniter.dm:44: 2.5 seconds and two units of fuel, then five sheets of iron and an igniter assembly
        Click(r, bob, igniter, Left, 3);
        Assert.True(r.In(() => QDELETED(igniter)), string.Join("\n", r.Room.RuntimeText));
        Assert.Equal(5f, Amount<obj_item_stack_sheet_iron>(r, 5, 4));
        Assert.Single(On<obj_item_assembly_igniter>(r, 5, 4));

        // igniter.dm:192: 1.5 seconds, the sparker's wall frame
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(6, 5, 1))));
        Click(r, bob, sparker, Left, 2);
        Assert.True(r.In(() => QDELETED(sparker)));
        Assert.Single(On<obj_item_wallframe_sparker>(r, 6, 4));
        r.AssertNoRuntimes();
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 slice D172: the four devices the rapid pipe dispenser dispenses that were inert until now, each laid by the RPD with Connect on and
/// fastened between two pipelines in a booted toy room: the pressure gate (binary_devices/pressure_valve.dm: passes gas only while its
/// input is above the target pressure), the temperature gate (temperature_gate.dm: by its temperature threshold, a multitool inverts it),
/// the temperature pump (temperature_pump.dm: moves heat from the input to the output at its rate) and the multi deck pipe adapter
/// (pipes/multiz.dm: on a one deck map it joins nothing). Their windows' ui_data and ui_act, the icon states, the examine text and
/// the unwrench check are tg's.
/// </summary>
public class RpdDeviceTests
{
    private const int Second = 20;
    private const int Y = 5;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";
    private const string Air = "o2=21;n2=79;TEMP=293.15";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_item_pipe_dispenser Rpd, datum_tgui Ui);

    private sealed record Line<T>(Rig G, T Device, datum_tgui Window, datum_pipeline Input, datum_pipeline Output) where T : obj_machinery_atmospherics_components_binary;

    private static Rig Build(ulong seed, int x, int y)
    {
        var r = new AudioRoom(13, 10, seed: seed);
        mob_living_carbon_human bob = r.Join(x, y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        var rpd = WallTests.Hold<obj_item_pipe_dispenser>(r, bob);
        datum_tgui ui = r.In(() => Dm.@new<datum_tgui>(bob, rpd, "RapidPipeDispenser", "rapid pipe dispenser"));
        return new Rig(r, bob, rpd, ui);
    }

    private static object Act(Rig g, string action, params (string Key, object Value)[] args)
    {
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        return g.R.In(() => Waiting.NoSleep(g.Rpd.ui_act(action, p, g.Ui, null)));
    }

    /// <summary>The recipe of that category and index facing east.</summary>
    private static void Pick(Rig g, string category, int index)
    {
        Act(g, "pipe_type", ("category", category), ("pipe_type", index));
        Act(g, "setdir", ("dir", "east"), ("flipped", 0f));
    }

    private static turf At(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));

    private static void Click(Rig g, atom target, float seconds = 1)
    {
        g.R.Command(() => ClientSeam.Click(g.Bob.client, target, Left));
        g.R.Step((int)(seconds * Second));
    }

    private static List<T> On<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.each<T>(locate(x, y, 1)).ToList());

    private static void Smart(AudioRoom r, int x0, int x1)
    {
        var made = new List<obj_machinery_atmospherics_pipe_smart>();
        for (int x = x0; x <= x1; x++)
        {
            int xx = x;
            made.Add(r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(xx, Y, 1), TRUE, EAST)));
        }
        r.In(() => SSair.setup_template_machinery(Dm.list(made.Cast<object>().ToArray())));
        r.Step(Second);
    }

    /// <summary>The recipe of the Binary category laid by the RPD at (6, Y) between a west and an east line of three pipes.</summary>
    private static Line<T> Lay<T>(ulong seed, int binaryIndex) where T : obj_machinery_atmospherics_components_binary
    {
        var g = Build(seed, 6, Y - 1);
        Smart(g.R, 3, 5);
        Smart(g.R, 7, 9);
        Pick(g, "Binary", binaryIndex);
        Click(g, At(g.R, 6, Y), 1);
        T device = Assert.Single(On<T>(g.R, 6, Y));
        g.R.Step(Second);
        datum_tgui window = null;
        datum_pipeline input = null, output = null;
        g.R.In(() =>
        {
            input = (datum_pipeline)device.parents[1];
            output = (datum_pipeline)device.parents[2];
            Assert.NotSame(input, output);
            Assert.False(Dm.truthy(device.on)); // build_pipe: a built device starts off
            window = Dm.@new<datum_tgui>(g.Bob, device, device.GetType().Name, "device");
        });
        return new Line<T>(g, device, window, input, output);
    }

    private static object UiAct<T>(Line<T> l, string action, params (string Key, object Value)[] args) where T : obj_machinery_atmospherics_components_binary
    {
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        return l.G.R.In(() => Waiting.NoSleep(l.Device.ui_act(action, p, l.Window, null)));
    }

    private static DmList UiData<T>(Line<T> l) where T : obj_machinery_atmospherics_components_binary => l.G.R.In(() => (DmList)Waiting.NoSleep(l.Device.ui_data(l.G.Bob)));

    private static void Fill<T>(Line<T> l, datum_pipeline pipeline, string gas) where T : obj_machinery_atmospherics_components_binary => l.G.R.In(() =>
    {
        pipeline.air.merge(SSair.parse_gas_string(gas) as datum_gas_mixture);
        pipeline.update = TRUE;
    });

    private static float Moles(AudioRoom r, datum_pipeline p) => r.In(() => Dm.num(p.air.total_moles()));

    private static float Temp(AudioRoom r, datum_pipeline p) => r.In(() => p.air.temperature);

    [Fact]
    public void ThePressureGatePassesGasOnlyAboveItsTargetPressure()
    {
        var l = Lay<obj_machinery_atmospherics_components_binary_pressure_valve>(17301, 6);
        var r = l.G.R;
        r.In(() =>
        {
            Assert.EndsWith("pressure gate", Dm.str(l.Device.name)); // update_name puts the paint colour first
            Assert.Equal(ONE_ATMOSPHERE, l.Device.target_pressure);
            Assert.Equal(EAST, Dm.num(l.Device.dir));
            Assert.Equal(typeof(obj_item_pipe_directional), l.Device.construction_type);
        });
        Fill(l, l.Input, Air);
        float pressure = r.In(() => Dm.num(l.Input.air.return_pressure()));
        Assert.True(pressure > ONE_ATMOSPHERE);

        // off: nothing passes, whatever the target
        r.In(() => l.Device.target_pressure = 1);
        r.Step(4 * Second);
        Assert.Equal(0f, Moles(r, l.Output));

        // on, the target above the input pressure: the gate stays shut and shows its "on" picture
        UiAct(l, "power");
        r.In(() =>
        {
            Assert.True(Dm.truthy(l.Device.on));
            l.Device.target_pressure = pressure * 2;
        });
        r.Step(4 * Second);
        Assert.Equal(0f, Moles(r, l.Output));
        r.In(() =>
        {
            Assert.False(Dm.truthy(l.Device.is_gas_flowing));
            Assert.Equal("pvalve_on-1", l.Device.icon_state);
        });

        // the target below the input pressure: gas passes, the picture shows the flow
        r.In(() => l.Device.target_pressure = pressure / 4);
        r.Step(4 * Second);
        Assert.True(Moles(r, l.Output) > 0, "the gate passed gas to the east pipeline");
        r.In(() =>
        {
            Assert.True(Dm.truthy(l.Device.is_gas_flowing));
            Assert.Equal("pvalve_flow-1", l.Device.icon_state);
        });

        // it releases to the input pressure, so the two sides end near the same pressure
        Assert.True(Dm.num(r.In(() => l.Output.air.return_pressure())) > pressure / 4 * 0.5f);

        // the window: tg's AtmosPump data and acts (pressure, max)
        DmList data = UiData(l);
        Assert.Equal(1f, Dm.num(data["on"]));
        Assert.Equal(round(pressure / 4), Dm.num(data["pressure"]));
        Assert.Equal(10132f, Dm.num(data["max_pressure"])); // round(ONE_ATMOSPHERE * 100): the shim rounds down (rules 4.6)
        UiAct(l, "pressure", ("pressure", 250f));
        Assert.Equal(250f, r.In(() => l.Device.target_pressure));
        UiAct(l, "pressure", ("pressure", -5f));
        Assert.Equal(0f, r.In(() => l.Device.target_pressure));
        UiAct(l, "pressure", ("pressure", "max"));
        Assert.Equal(ONE_ATMOSPHERE * 100, r.In(() => l.Device.target_pressure));
        UiAct(l, "pressure", ("pressure", 1e9f));
        Assert.Equal(ONE_ATMOSPHERE * 100, r.In(() => l.Device.target_pressure));
        UiAct(l, "pressure", ("pressure", "junk"));
        Assert.Equal(ONE_ATMOSPHERE * 100, r.In(() => l.Device.target_pressure));

        // while it is on it cannot be unwrenched (can_unwrench), once off it can
        r.In(() =>
        {
            Assert.False(Dm.truthy(l.Device.can_unwrench__proc(l.G.Bob)));
        });
        UiAct(l, "power");
        r.In(() =>
        {
            Assert.False(Dm.truthy(l.Device.on));
            Assert.Equal("pvalve_off-1", l.Device.icon_state);
            Assert.True(Dm.truthy(l.Device.can_unwrench__proc(l.G.Bob)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTemperatureGatePassesGasBelowItsTemperatureAndAboveItWhenAMultitoolInvertsIt()
    {
        var l = Lay<obj_machinery_atmospherics_components_binary_temperature_gate>(17302, 7);
        var r = l.G.R;
        r.In(() =>
        {
            Assert.EndsWith("temperature gate", Dm.str(l.Device.name)); // update_name puts the paint colour first
            Assert.Equal(T0C, l.Device.target_temperature);
            Assert.False(Dm.truthy(l.Device.inverted));
        });
        Fill(l, l.Input, Air); // 293.15 K
        UiAct(l, "power");
        Assert.True(r.In(() => Dm.truthy(l.Device.on)));

        // the input is warmer than the target: shut
        UiAct(l, "temperature", ("temperature", 250f));
        r.Step(4 * Second);
        Assert.Equal(0f, Moles(r, l.Output));
        r.In(() =>
        {
            Assert.False(Dm.truthy(l.Device.is_gas_flowing));
            Assert.Equal("tgate_on-1", l.Device.icon_state);
        });

        // the input is colder than the target: open
        UiAct(l, "temperature", ("temperature", 350f));
        r.Step(4 * Second);
        Assert.True(Moles(r, l.Output) > 0);
        r.In(() =>
        {
            Assert.True(Dm.truthy(l.Device.is_gas_flowing));
            Assert.Equal("tgate_flow-1", l.Device.icon_state);
        });

        // the multitool inverts it: now it passes gas only when the input is warmer than the target
        // examine's root (atom_examine.dm) is M9: until then the device's lines join as DM's += on a null does
        string Text() => r.In(() => Waiting.NoSleep(l.Device.examine(l.G.Bob)) is DmList lines ? string.Join("\n", Dm.each(lines).Select(Dm.str)) : Dm.str(Waiting.NoSleep(l.Device.examine(l.G.Bob))));
        Assert.Contains("lower than the temperature set in the interface", Text());
        Assert.Contains("The sensor's settings can be changed by using a multitool on the device.", Text());
        var tool = r.In(() => Dm.@new<obj_item_multitool>(get_turf(l.G.Bob)));
        Assert.True(Dm.truthy(r.In(() => Waiting.NoSleep(l.Device.multitool_act(l.G.Bob, tool)))));
        Assert.True(r.In(() => Dm.truthy(l.Device.inverted)));
        Assert.Contains("higher than the temperature set in the interface", Text());
        Assert.Contains("The device settings can be restored if a multitool is used on it.", Text());
        float before = Moles(r, l.Output);
        r.Step(4 * Second); // the input (now cooler than 350 K) is below the target: shut, the flow state is cleared
        r.In(() => Assert.False(Dm.truthy(l.Device.is_gas_flowing)));
        UiAct(l, "temperature", ("temperature", 250f)); // inverted, the input (293 K) is above the target: open again (once the input has gas to release)
        Fill(l, l.Input, Air);
        r.Step(4 * Second);
        r.In(() => Assert.True(Dm.truthy(l.Device.is_gas_flowing)));
        Assert.True(Moles(r, l.Output) >= before);
        Assert.True(Dm.truthy(r.In(() => Waiting.NoSleep(l.Device.multitool_act(l.G.Bob, tool)))));
        Assert.False(r.In(() => Dm.truthy(l.Device.inverted))); // a second use restores the default

        // the window: tg's AtmosTempGate data and acts (the temperature is clamped to [TCMB, 4500], max, junk is ignored)
        DmList data = UiData(l);
        Assert.Equal(1f, Dm.num(data["on"]));
        Assert.Equal(250f, Dm.num(data["temperature"]));
        Assert.Equal(2f, Dm.num(data["min_temperature"])); // round(TCMB): the shim rounds down
        Assert.Equal(4500f, Dm.num(data["max_temperature"]));
        UiAct(l, "temperature", ("temperature", 1f));
        Assert.Equal(TCMB, r.In(() => l.Device.target_temperature));
        UiAct(l, "temperature", ("temperature", 99999f));
        Assert.Equal(4500f, r.In(() => l.Device.target_temperature));
        UiAct(l, "temperature", ("temperature", 300f));
        UiAct(l, "temperature", ("temperature", "max"));
        Assert.Equal(4500f, r.In(() => l.Device.target_temperature));
        UiAct(l, "temperature", ("temperature", "junk"));
        Assert.Equal(4500f, r.In(() => l.Device.target_temperature));
        UiAct(l, "power");
        Assert.False(r.In(() => Dm.truthy(l.Device.on)));
        r.In(() => Assert.Equal("tgate_off-1", l.Device.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTemperaturePumpMovesHeatFromItsInputToItsOutputAtItsRate()
    {
        var l = Lay<obj_machinery_atmospherics_components_binary_temperature_pump>(17303, 8);
        var r = l.G.R;
        r.In(() =>
        {
            Assert.EndsWith("temperature pump", Dm.str(l.Device.name)); // update_name puts the paint colour first
            Assert.Equal(0f, l.Device.heat_transfer_rate);
            Assert.Equal(100f, l.Device.max_heat_transfer_rate);
        });
        Fill(l, l.Input, "o2=21;n2=79;TEMP=400");
        UiAct(l, "power");
        Assert.True(r.In(() => Dm.truthy(l.Device.on)));
        r.In(() => Assert.Equal("tpump_on-1", l.Device.icon_state));

        // an output without gas is left alone (tg: no transfer if there is no gas), even at the maximum rate
        UiAct(l, "rate", ("rate", "max"));
        r.Step(4 * Second);
        Assert.InRange(Temp(r, l.Input), 399.9f, 400.1f);
        UiAct(l, "rate", ("rate", 0f));
        Fill(l, l.Output, "o2=21;n2=79;TEMP=300");
        float total = Total();
        float Total() => r.In(() => Dm.num(l.Input.air.total_moles()) + Dm.num(l.Output.air.total_moles()) + Dm.num(((datum_gas_mixture)l.Device.airs[1]).total_moles()) + Dm.num(((datum_gas_mixture)l.Device.airs[2]).total_moles()));

        // rate 0: the heat transferred is nothing
        r.Step(4 * Second);
        Assert.InRange(Temp(r, l.Input), 399.9f, 400.1f);
        Assert.InRange(Temp(r, l.Output), 299.9f, 300.1f);

        // the window sets the rate (clamped to the maximum, "max")
        UiAct(l, "rate", ("rate", 50f));
        Assert.Equal(50f, r.In(() => l.Device.heat_transfer_rate));
        UiAct(l, "rate", ("rate", 5000f));
        Assert.Equal(100f, r.In(() => l.Device.heat_transfer_rate));
        UiAct(l, "rate", ("rate", -4f));
        Assert.Equal(0f, r.In(() => l.Device.heat_transfer_rate));
        UiAct(l, "rate", ("rate", "junk"));
        Assert.Equal(0f, r.In(() => l.Device.heat_transfer_rate));
        UiAct(l, "rate", ("rate", "max"));
        Assert.Equal(100f, r.In(() => l.Device.heat_transfer_rate));
        UiAct(l, "rate", ("rate", 30f));
        DmList data = UiData(l);
        Assert.Equal((1f, 30f, 100f), (Dm.num(data["on"]), Dm.num(data["rate"]), Dm.num(data["max_heat_transfer_rate"])));

        // the input cools and the output heats; no gas moves
        r.Step(10 * Second);
        float tin = Temp(r, l.Input), tout = Temp(r, l.Output);
        Assert.True(tin < 399f, "the input cooled: " + tin);
        Assert.True(tout > 301f, "the output heated: " + tout);
        Assert.True(tin > tout, "the pump never overshoots (the delta only shrinks)");
        Assert.Equal(total, Total(), 1);

        // a higher rate moves heat faster: the same time at max cools the input further than 30% did over its first steps
        UiAct(l, "rate", ("rate", "max"));
        r.Step(10 * Second);
        Assert.True(Temp(r, l.Input) < tin);
        Assert.True(Temp(r, l.Output) > tout);

        UiAct(l, "power");
        r.In(() => Assert.Equal("tpump_off-1", l.Device.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMultiDeckAdapterIsFastenedAndJoinsNoOtherDeckOnASingleDeckMap()
    {
        var g = Build(17304, 4, Y - 1);
        Pick(g, "Pipes", 5);
        Click(g, At(g.R, 4, Y), 1);
        var adapter = Assert.Single(On<obj_machinery_atmospherics_pipe_multiz>(g.R, 4, Y));
        g.R.Step(Second);
        g.R.In(() =>
        {
            Assert.EndsWith("multi deck pipe adapter", Dm.str(adapter.name));
            Assert.Equal("", adapter.icon_state);
            Assert.Equal(3f, Dm.num(adapter.device_type));
            Assert.Equal(3f, adapter.nodes.len);
            Assert.NotNull(adapter.center);
            Assert.Equal("adapter_center", adapter.center.icon_state);
            // GET_TURF_ABOVE and GET_TURF_BELOW are null on the one deck: the expansion is the node list, with nothing above or below
            DmList expansion = (DmList)adapter.pipeline_expansion();
            Assert.Same(adapter.nodes, expansion);
            Assert.Null(expansion[2]);
            Assert.Null(expansion[3]);
            // its picture: the pipe and the centre
            DmList overlays = (DmList)Waiting.NoSleep(adapter.update_overlays());
            Assert.Contains(adapter.pipe, Dm.each(overlays));
            Assert.Contains(adapter.center, Dm.each(overlays));
            Assert.Equal("pipe-3", adapter.pipe.icon_state);
        });
        Assert.Empty(On<obj_item_pipe>(g.R, 4, Y)); // the fitting became the adapter
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void EveryRecipeOfTheBinaryAndPipesListsIsFastenedWithoutNotTranslated()
    {
        // the four devices' fittings used to stay loose at construction.dm:301; now each becomes its device
        var g = Build(17305, 2, 2);
        int k = 0;
        foreach ((string category, int index, Type type) in new[]
        {
            ("Pipes", 5, typeof(obj_machinery_atmospherics_pipe_multiz)),
            ("Binary", 6, typeof(obj_machinery_atmospherics_components_binary_pressure_valve)),
            ("Binary", 7, typeof(obj_machinery_atmospherics_components_binary_temperature_gate)),
            ("Binary", 8, typeof(obj_machinery_atmospherics_components_binary_temperature_pump)),
        })
        {
            int bx = 2 + k * 2;
            g.R.In(() => Waiting.NoSleep(g.Bob.forceMove(locate(bx, 3, 1))));
            Pick(g, category, index);
            Click(g, At(g.R, bx + 1, 3), 1);
            Assert.Single(On<obj_machinery_atmospherics>(g.R, bx + 1, 3), a => type.IsInstanceOfType(a));
            k++;
        }
        g.R.AssertNoRuntimes();
    }
}

using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Engineering package E16: the client's BigManipulator window (tg's BigManipulator/index.tsx: the action panel, the disk row, the task
/// list with its editor modal and the new task row), ManufacturingSorter (the filters and the unmet side), Portagrav (the retro display
/// and keys) and AnomalousWeatherTower (the core charge, the active weather and the summon button), without the engine.
/// </summary>
public class ManipulatorManufacturingWindowsTests
{
    private sealed class Rig
    {
        public readonly TguiLocalState Local = new();
        public readonly List<(string Action, string Params)> Acts = new();
        public TguiContext Context;

        public UiNode Render(string name, Func<TguiContext, UiNode> render, string json)
        {
            Context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => Acts.Add((a, p?.ToJson())), Local);
            return render(Context);
        }
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Manipulator = "{\"active\":0,\"stopping\":0,\"current_task\":null,\"speed_multiplier\":1,\"min_speed_multiplier\":0.5,"
        + "\"max_speed_multiplier\":2,\"manipulator_position\":\"10,10\",\"tasking_strategy\":\"Sequential\",\"has_monkey\":0,\"disk_inserted\":0,"
        + "\"disk_read_only\":null,\"disk_task_count\":0,\"tasks_data\":["
        + "{\"name\":\"pickup\",\"id\":\"[0x1]\",\"task_type\":\"pickup\",\"turf\":\"-1,0\",\"filters_status\":0,\"item_filters\":[\"wrench\",\"crowbar\",\"screwdriver\",\"wirecutters\"],"
        + "\"settings_list\":[{\"name\":\"PICK UP ITEMS\",\"active\":1},{\"name\":\"PICK UP CLOSETS\",\"active\":0}],\"pickup_eagerness\":\"Wait For Suiting\"},"
        + "{\"name\":\"drop\",\"id\":\"[0x2]\",\"task_type\":\"drop\",\"turf\":\"1,0\",\"filters_status\":1,\"item_filters\":[],"
        + "\"settings_list\":[{\"name\":\"DROP IN STORAGE\",\"active\":1},{\"name\":\"DROP ON FLOOR\",\"active\":1}],\"overflow_status\":\"ALLOW\"},"
        + "{\"name\":\"wait\",\"id\":\"[0x3]\",\"task_type\":\"wait\",\"time\":3}]}";

    [Fact]
    public void TheManipulatorWindowShowsItsPanelDiskAndTasks()
    {
        var rig = new Rig();
        UiNode tree = rig.Render("BigManipulator", BigManipulator.Render, Manipulator);
        Assert.Equal((420.0, 560.0, "Manipulator Interface"), (rig.Context.WindowWidth, rig.Context.WindowHeight, rig.Context.WindowTitle));

        UiNode run = ByKey(tree, "run_cycle");
        Assert.Equal(("Run", "play", "good"), (run.Text, run.Icon, run.Color));
        UiNode speed = ByKey(tree, "speed");
        Assert.Equal((1.0, 0.5, 2.0, 0.1, "x"), (speed.Value, speed.MinValue, speed.MaxValue, speed.Step, speed.Unit));
        UiNode eject = ByKey(tree, "disk_eject");
        Assert.Equal(("No drives inserted", "info", true), (eject.Text, eject.Icon, eject.Disabled));
        Assert.True(ByKey(tree, "disk_read").Disabled);

        // the task rows: tg's labels, icons and the lines under them
        Assert.Equal(new[] { "Pick up...", "Drop...", "Wait..." }, tree.Walk().Where(n => n.Key == "type").Select(n => n.Text));
        Assert.Contains("...any of: wrench, crowbar, screwdriver and 1 more...", tree.AllText());
        Assert.Contains("...at [-1,0]...", tree.AllText());
        Assert.Contains("...for 3 seconds...", tree.AllText());
        Assert.True(ByKey(tree, "up:[0x1]").Disabled);
        Assert.True(ByKey(tree, "down:[0x3]").Disabled);
        Assert.Equal(("Sequential", "list-ol"), (ByKey(tree, "cycle_tasking_strategy").Text, ByKey(tree, "cycle_tasking_strategy").Icon));
        Assert.Equal("Pick up...", ByKey(tree, "task_type").TextValue);

        run.OnClick();
        speed.OnChange(1.5);
        ByKey(tree, "remove:[0x2]").OnClick();
        ByKey(tree, "cycle_tasking_strategy").OnClick();
        Assert.Equal(("run_cycle", (string)null), rig.Acts[0]);
        Assert.Equal(("adjust_interaction_speed", 1.5), (rig.Acts[1].Action, UiValue.FromJson(rig.Acts[1].Params).Num("new_speed")));
        UiValue remove = UiValue.FromJson(rig.Acts[2].Params);
        Assert.Equal(("adjust_task_param", "[0x2]", "remove_task"), (rig.Acts[2].Action, remove.Str("taskId"), remove.Str("param")));
        Assert.Equal("Strict order", UiValue.FromJson(rig.Acts[3].Params).Str("new_strategy"));

        // a new drop task
        ByKey(tree, "task_type").OnSelected("Drop...");
        tree = rig.Render("BigManipulator", BigManipulator.Render, Manipulator);
        ByKey(tree, "create_task").OnClick();
        Assert.Equal(("create_task", "drop"), (rig.Acts[^1].Action, UiValue.FromJson(rig.Acts[^1].Params).Str("task_type")));
    }

    [Fact]
    public void TheTaskEditorSendsTheTasksSettings()
    {
        var rig = new Rig();
        UiNode tree = rig.Render("BigManipulator", BigManipulator.Render, Manipulator);
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Modal);
        ByKey(tree, "edit:[0x1]").OnClick();
        tree = rig.Render("BigManipulator", BigManipulator.Render, Manipulator);
        UiNode modal = tree.Walk().Single(n => n.Kind == UiKind.Modal);
        Assert.Equal("Edit: pickup", ByKey(modal, "edit").Title);

        // the point grid: the task's tile (-1,0) is button 4, the centre is disabled
        Assert.Equal("good", ByKey(modal, "point:4").Color);
        Assert.True(ByKey(modal, "point:5").Disabled);
        Assert.Equal("arrow-up", ByKey(modal, "point:2").Icon);
        ByKey(modal, "point:6").OnClick();
        UiValue move = UiValue.FromJson(rig.Acts[^1].Params);
        Assert.Equal(("move_to", 6.0), (move.Str("param"), move.Field("value").Num("buttonNumber")));

        // the settings rows of a pickup, the filters and the priorities
        Assert.Equal(("FALSE", "Wait For Suiting"), (ByKey(modal, "toggle_filter_skip").Text, ByKey(modal, "cycle_pickup_eagerness").Text));
        Assert.DoesNotContain(modal.Walk(), n => n.Key == "cycle_overflow_status");
        Assert.Equal(new bool?[] { true, false }, modal.Walk().Where(n => n.Key?.StartsWith("toggle_priority:") == true).Select(n => n.Checkbox));
        Assert.True(ByKey(modal, "priority_move_up:0").Disabled);
        ByKey(modal, "toggle_priority:1").OnClick();
        UiValue toggle = UiValue.FromJson(rig.Acts[^1].Params);
        Assert.Equal(("toggle_priority", 1.0), (toggle.Str("param"), toggle.Num("value")));
        ByKey(modal, "delete_filter:2").OnClick();
        Assert.Equal(2.0, UiValue.FromJson(rig.Acts[^1].Params).Num("value"));

        // the editor follows the live data and closes
        tree = rig.Render("BigManipulator", BigManipulator.Render, Manipulator.Replace("\"pickup_eagerness\":\"Wait For Suiting\"", "\"pickup_eagerness\":\"Always Pick Up\""));
        Assert.Equal("Always Pick Up", ByKey(tree, "cycle_pickup_eagerness").Text);
        ByKey(tree, "close").OnClick();
        tree = rig.Render("BigManipulator", BigManipulator.Render, Manipulator);
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Modal);

        // a wait task's editor is its time slider
        ByKey(tree, "edit:[0x3]").OnClick();
        tree = rig.Render("BigManipulator", BigManipulator.Render, Manipulator);
        UiNode time = ByKey(tree, "wait_time");
        Assert.Equal((3.0, 1.0, 60.0, "s"), (time.Value, time.MinValue, time.MaxValue, time.Unit));
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "grid");
    }

    [Fact]
    public void TheSorterListsItsFiltersAndTheirButtons()
    {
        var rig = new Rig();
        UiNode tree = rig.Render("ManufacturingSorter", ManufacturingSorter.Render,
            "{\"unmet_dir\":2,\"filters\":[{\"name\":\"input is ore\",\"ref\":\"[0x9]\",\"inverted\":0,\"dir\":4},{\"name\":\"input is stack\",\"ref\":\"[0xa]\",\"inverted\":0,\"dir\":1}]}");
        Assert.Equal((450.0, 350.0, "Manufacturing Sorter"), (rig.Context.WindowWidth, rig.Context.WindowHeight, rig.Context.WindowTitle));
        Assert.Equal(new[] { "1. input is ore", "2. input is stack" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        Assert.Equal(("Output", "arrow-right"), (ByKey(tree, "rotate:[0x9]").Text, ByKey(tree, "rotate:[0x9]").Icon));
        Assert.Equal("arrow-down", ByKey(tree, "rotate_unmet").Icon);
        Assert.Contains("If no criteria is met, outputting to:", tree.AllText());

        ByKey(tree, "new_filter").OnClick();
        ByKey(tree, "rotate:[0x9]").OnClick();
        ByKey(tree, "edit:[0xa]").OnClick();
        ByKey(tree, "del:[0x9]").OnClick();
        ByKey(tree, "up:[0xa]").OnClick();
        ByKey(tree, "rotate_unmet").OnClick();
        Assert.Equal(new[] { "new_filter", "rotate", "edit", "del_filter", "shift", "rotate_unmet" }, rig.Acts.Select(a => a.Action));
        Assert.Equal("[0x9]", UiValue.FromJson(rig.Acts[1].Params).Str("ref"));
        Assert.Equal(-1.0, UiValue.FromJson(rig.Acts[4].Params).Num("amount"));
    }

    [Fact]
    public void ThePortagravShowsItsChargeGravityAndRange()
    {
        var rig = new Rig();
        UiNode tree = rig.Render("Portagrav", Portagrav.Render, "{\"percentage\":75,\"gravity\":1,\"range\":4,\"maxrange\":6,\"on\":1,\"wiremode\":0,\"draw\":\"4 kW\"}");
        Assert.Equal((320.0, 320.0, "retro"), (rig.Context.WindowWidth, rig.Context.WindowHeight, rig.Context.WindowTheme));
        Assert.Equal("75% - 1G", ByKey(tree, "power").Text);
        Assert.Equal("4 kW / Rng: 4/6", ByKey(tree, "draw").Text);
        ByKey(tree, "grav_up").OnClick();
        ByKey(tree, "range_down").OnClick();
        ByKey(tree, "toggle_power").OnClick();
        ByKey(tree, "toggle_wire").OnClick();
        Assert.Equal(new[] { "adjust_grav", "adjust_range", "toggle_power", "toggle_wire" }, rig.Acts.Select(a => a.Action));
        Assert.Equal((1.0, -1.0), (UiValue.FromJson(rig.Acts[0].Params).Num("adjustment"), UiValue.FromJson(rig.Acts[1].Params).Num("adjustment")));

        tree = rig.Render("Portagrav", Portagrav.Render, "{\"gravity\":2,\"range\":3,\"maxrange\":6,\"on\":0,\"wiremode\":0,\"draw\":\"3 kW\"}");
        Assert.Equal(("NO CELL - 2G", "OFF / Rng: 3/6"), (ByKey(tree, "power").Text, ByKey(tree, "draw").Text));
        tree = rig.Render("Portagrav", Portagrav.Render, "{\"percentage\":10,\"gravity\":1,\"range\":3,\"maxrange\":6,\"on\":0,\"wiremode\":1,\"draw\":\"3 kW\"}");
        Assert.Equal("WIRE POWERED - 1G", ByKey(tree, "power").Text);
    }

    [Fact]
    public void TheWeatherTowerShowsItsChargeAndSummonsTheSelectedWeather()
    {
        var rig = new Rig();
        const string json = "{\"core_charges\":2,\"max_core_charge\":8,\"weather_charge_cost\":1,\"can_summon_weather\":1,\"can_clear_weather\":0,"
            + "\"summonable_weather_types\":[{\"id\":\"/datum/weather/particle/ash_storm\",\"name\":\"Ash storm\",\"desc\":\"x\"},{\"id\":\"/datum/weather/snow_storm\",\"name\":\"Snow storm\",\"desc\":\"y\"}],"
            + "\"active_weather_on_z\":[]}";
        UiNode tree = rig.Render("AnomalousWeatherTower", AnomalousWeatherTower.Render, json);
        Assert.Equal((250.0, 420.0, "ntos_darkmode"), (rig.Context.WindowWidth, rig.Context.WindowHeight, rig.Context.WindowTheme));
        Assert.Equal(("25%", "red"), (ByKey(tree, "charges").Text, ByKey(tree, "charges").Color));
        Assert.Equal("None", ByKey(tree, "none").Text);
        Assert.Equal("Ash storm", ByKey(tree, "weather").TextValue);
        ByKey(tree, "weather").OnSelected("Snow storm");
        tree = rig.Render("AnomalousWeatherTower", AnomalousWeatherTower.Render, json);
        UiNode summon = ByKey(tree, "summon_weather");
        Assert.False(summon.Disabled);
        summon.OnClick();
        Assert.Equal(("summon_weather", "/datum/weather/snow_storm"), (rig.Acts[0].Action, UiValue.FromJson(rig.Acts[0].Params).Str("weather_type")));

        tree = rig.Render("AnomalousWeatherTower", AnomalousWeatherTower.Render, json.Replace("\"active_weather_on_z\":[]", "\"active_weather_on_z\":[{\"id\":\"[0x5]\",\"name\":\"Ash storm\",\"desc\":\"x\"}]")
            .Replace("\"core_charges\":2", "\"core_charges\":0"));
        Assert.Equal(450.0, rig.Context.WindowHeight);
        UiNode clear = ByKey(tree, "clear:[0x5]");
        Assert.Equal((true, "Weather inhibitors are recharging."), (clear.Disabled, clear.Tooltip));
        Assert.Equal((true, "Not enough charges to summon weather."), (ByKey(tree, "summon_weather").Disabled, ByKey(tree, "summon_weather").Tooltip));
    }

    [Fact]
    public void TheWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)BigManipulator.Render, TguiInterfaces.Find("BigManipulator"));
        Assert.Equal((TguiInterface)ManufacturingSorter.Render, TguiInterfaces.Find("ManufacturingSorter"));
        Assert.Equal((TguiInterface)Portagrav.Render, TguiInterfaces.Find("Portagrav"));
        Assert.Equal((TguiInterface)AnomalousWeatherTower.Render, TguiInterfaces.Find("AnomalousWeatherTower"));
    }
}

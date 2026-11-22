using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Engineering package E12b: the client's Hypertorus window (tg's interfaces/Hypertorus/*.tsx) without the engine. It reads tg's ui_static_data
/// and ui_data of the HFR interface the way the TSX does: the start buttons and their disabled rules, the recipe table (gases, tiers, the
/// effects' arrows and tooltips), the fusion and moderator gas lists (sorted, the sticky gases, the help icons), the reactor status gauges
/// (integrity, iron, area power, fusion level, energy, activity, instability), the four knobs with their max, default and min buttons, the
/// output control (waste remove, filtering rate, the moderator filters) and the temperature chart; and it sends tg's acts.
/// </summary>
public class HypertorusWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "Hypertorus", "HFR interface", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, Hypertorus.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    // tg's ui_static_data (hfr_parts.dm:189): the "Nothing" entry and two of the seven recipes
    private const string Static = "\"base_max_temperature\":100000000,\"selectable_fuel\":["
        + "{\"name\":\"Nothing\",\"id\":null},"
        + "{\"name\":\"Hydrogen + Tritium fuel\",\"id\":\"h2_t2_fuel\",\"requirements\":[\"hydrogen\",\"tritium\"],\"fusion_byproducts\":[\"helium\"],"
        + "\"product_gases\":[\"helium\",\"plasma\",\"o2\",\"n2\",\"bz\",\"hypernoblium\"],\"recipe_cooling_multiplier\":1,\"recipe_heating_multiplier\":1,"
        + "\"energy_loss_multiplier\":1,\"fuel_consumption_multiplier\":1,\"gas_production_multiplier\":1,\"temperature_multiplier\":0.85},"
        + "{\"name\":\"Plasma + Oxygen fuel\",\"id\":\"plasma_o2_fuel\",\"requirements\":[\"plasma\",\"o2\"],\"fusion_byproducts\":[\"co2\",\"water_vapor\"],"
        + "\"product_gases\":[\"co2\",\"water_vapor\",\"freon\",\"n2o\",\"pluoxium\",\"halon\"],\"recipe_cooling_multiplier\":2.5,\"recipe_heating_multiplier\":0.1,"
        + "\"energy_loss_multiplier\":10,\"fuel_consumption_multiplier\":3.3,\"gas_production_multiplier\":1.4,\"temperature_multiplier\":0.6}]";

    // an idle machine: no recipe, nothing inside, the switches off
    private const string Idle = "{" + Static + ",\"selected\":\"\",\"fusion_gases\":[{\"id\":\"antinoblium\",\"amount\":0}],\"moderator_gases\":[],"
        + "\"energy_level\":0,\"heat_limiter_modifier\":5,\"heat_output_min\":-0.05,\"heat_output_max\":5,\"heat_output\":0,\"instability\":0,"
        + "\"heating_conductor\":100,\"magnetic_constrictor\":100,\"fuel_injection_rate\":25,\"moderator_injection_rate\":25,\"current_damper\":0,"
        + "\"power_level\":0,\"apc_energy\":100,\"iron_content\":0,\"integrity\":100,"
        + "\"start_power\":0,\"start_cooling\":0,\"start_fuel\":0,\"start_moderator\":0,"
        + "\"internal_fusion_temperature\":0,\"moderator_internal_temperature\":0,\"internal_output_temperature\":0,\"internal_coolant_temperature\":0,"
        + "\"internal_fusion_temperature_archived\":0,\"moderator_internal_temperature_archived\":0,\"internal_output_temperature_archived\":0,"
        + "\"internal_coolant_temperature_archived\":0,\"temperature_period\":1,"
        + "\"waste_remove\":0,\"filter_types\":[{\"gas_id\":\"plasma\",\"gas_name\":\"Plasma\",\"enabled\":0},{\"gas_id\":\"helium\",\"gas_name\":\"Helium\",\"enabled\":1}],"
        + "\"cooling_volume\":100,\"mod_filtering_rate\":100}";

    // a running reaction at fusion level 3: hydrogen and tritium burning into helium, plasma and BZ in the moderator
    private const string Running = "{" + Static + ",\"selected\":\"h2_t2_fuel\","
        + "\"fusion_gases\":[{\"id\":\"helium\",\"amount\":120.5},{\"id\":\"hydrogen\",\"amount\":800.25},{\"id\":\"tritium\",\"amount\":790},{\"id\":\"antinoblium\",\"amount\":0}],"
        + "\"moderator_gases\":[{\"id\":\"plasma\",\"amount\":300},{\"id\":\"o2\",\"amount\":0.001}],"
        + "\"energy_level\":1e20,\"heat_limiter_modifier\":5000,\"heat_output_min\":-50,\"heat_output_max\":5000,\"heat_output\":2500,\"instability\":4,"
        + "\"heating_conductor\":200,\"magnetic_constrictor\":100,\"fuel_injection_rate\":25,\"moderator_injection_rate\":25,\"current_damper\":10,"
        + "\"power_level\":3,\"apc_energy\":42.5,\"iron_content\":0.3,\"integrity\":94.44,"
        + "\"start_power\":1,\"start_cooling\":1,\"start_fuel\":1,\"start_moderator\":0,"
        + "\"internal_fusion_temperature\":45000,\"moderator_internal_temperature\":3000,\"internal_output_temperature\":0,\"internal_coolant_temperature\":80,"
        + "\"internal_fusion_temperature_archived\":40000,\"moderator_internal_temperature_archived\":3000,\"internal_output_temperature_archived\":0,"
        + "\"internal_coolant_temperature_archived\":82,\"temperature_period\":2,"
        + "\"waste_remove\":1,\"filter_types\":[{\"gas_id\":\"plasma\",\"gas_name\":\"Plasma\",\"enabled\":0},{\"gas_id\":\"helium\",\"gas_name\":\"Helium\",\"enabled\":1}],"
        + "\"cooling_volume\":500,\"mod_filtering_rate\":20}";

    [Fact]
    public void AnIdleMachineShowsTgsSectionsTheSwitchesAndTheRecipeTable()
    {
        var (context, tree, acts) = Render(Idle);
        Assert.Equal((850.0, 980.0, "Hypertorus Fusion Reactor control panel"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        Assert.Equal(new[] { "Internal Fusion Gases", "Moderator Gases", "Gas Monitoring", "Reactor Status", "Reactor Control", "Output Control" },
            tree.Walk().Where(n => n.Kind == UiKind.Section && n.Title != null).Select(n => n.Title));

        // tg: index.tsx:68  the power is off and can start; the cooling needs the power first
        UiNode power = ByKey(tree, "start_power");
        Assert.Equal(("Off", "times", false, false), (power.Text, power.Icon, power.Selected, power.Disabled));
        Assert.True(ByKey(tree, "start_cooling").Disabled);
        power.OnClick();
        Assert.Equal(("start_power", (string)null), acts[^1]);

        // no recipe: the fusion section says so; the moderator list shows the sticky plasma, BZ and proto-nitrate at 0 with their help
        Assert.Equal(("No recipe selected", "red"), (ByKey(tree, "no-recipe").Text, ByKey(tree, "no-recipe").Color));
        Assert.Equal("0.00 moles", ByKey(tree, "start_moderator:plasma").Text);
        Assert.Equal("0.00 moles", ByKey(tree, "start_moderator:proto_nitrate").Text);
        Assert.Contains("Produces basic gases", ByKey(tree, "start_moderator:item:plasma").LabelNode.Walk().First(n => n.Kind == UiKind.Icon).Tooltip);
        Assert.True(ByKey(tree, "start_moderator").Disabled); // power and cooling off

        // the recipe table: the "Nothing" entry is left out; each recipe's button, gases and effect arrows (Recipes.tsx:183)
        UiNode table = tree.Walk().Single(n => n.Kind == UiKind.Table);
        List<UiNode> rows = table.Children;
        Assert.Equal(4, rows.Count); // two header rows and the two recipes
        Assert.All(rows, row => Assert.Equal(17, row.Children.Count)); // 1 + 2 + 2 + 6 + 6 columns
        UiNode h2t2 = ByKey(tree, "h2_t2_fuel");
        Assert.Equal(new[] { "H₂", "Tritium", "He", null }, h2t2.Children.Skip(1).Take(4).Select(c => c.Text));
        Assert.Equal(("white", "limegreen"), (h2t2.Children[1].Color, h2t2.Children[2].Color));
        Assert.Equal(new[] { "He", "Plasma", "O₂", "N₂", "BZ", "Hyper-nob" }, h2t2.Children.Skip(5).Take(6).Select(c => c.Text));
        // effects: all 1 against a base of 1 are "minus"; the max temperature 0.85 against its base 0.85 too, its tooltip tg's maximum
        Assert.Equal(new[] { "minus", "minus", "minus", "minus", "minus", "minus" }, h2t2.Children.Skip(11).Select(c => c.Children[0].Icon));
        Assert.Equal("Maximum: 8.5e+7 K", h2t2.Children[16].Tooltip);
        UiNode po = ByKey(tree, "plasma_o2_fuel");
        // cooling x2.5 (scale 3: up), heating x0.1 (double down), energy loss x10 (double up), fuel use x3.3 and production x1.4 (scale 1.5)
        Assert.Equal(new[] { "angle-up", "angle-double-down", "angle-double-up", "angle-double-up", "angle-up", "angle-double-down" },
            po.Children.Skip(11).Select(c => c.Children[0].Icon));
        Assert.Equal("x2.5", po.Children[11].Tooltip);
        UiNode select = ByKey(tree, "recipe:h2_t2_fuel");
        Assert.Equal(("power-off", false, false), (select.Icon, select.Selected, select.Disabled));
        select.OnClick();
        Assert.Equal(("fuel", "{\"mode\":\"h2_t2_fuel\"}"), acts[^1]);

        // the empty chart: every bar "Empty"
        Assert.Equal(("Empty", "red"), (ByKey(tree, "temp:Fusion").Text, ByKey(tree, "temp:Fusion").Color));
        Assert.DoesNotContain(tree.Walk(), n => n.Key?.StartsWith("bar:") == true);
    }

    [Fact]
    public void ARunningReactionShowsItsGasesGaugesKnobsAndTemperatures()
    {
        var (_, tree, acts) = Render(Running);

        // power on at level 3: the power button and the recipe buttons are locked, the cooling cannot be stopped
        Assert.True(ByKey(tree, "start_power").Disabled);
        Assert.Equal(("On", "power-off", true), (ByKey(tree, "start_power").Text, ByKey(tree, "start_power").Icon, ByKey(tree, "start_power").Selected));
        Assert.True(ByKey(tree, "start_cooling").Disabled);
        Assert.True(ByKey(tree, "recipe:h2_t2_fuel").Disabled);
        Assert.Equal(("times", true), (ByKey(tree, "recipe:h2_t2_fuel").Icon, ByKey(tree, "recipe:h2_t2_fuel").Selected));

        // the fusion gases by amount, the trace of O2 in the moderator dropped, the injection switches and rates
        Assert.Equal(new[] { "hydrogen", "tritium", "helium" },
            tree.Walk().Where(n => n.Kind == UiKind.ProgressBar && n.Key.StartsWith("start_fuel:")).Select(n => n.Key["start_fuel:".Length..]));
        UiNode h2 = ByKey(tree, "start_fuel:hydrogen");
        Assert.Equal(("800.25 moles", "white", 800.25, 500.0), (h2.Text, h2.Color, h2.Value, h2.MaxValue));
        Assert.Equal(new[] { "plasma", "bz", "proto_nitrate" },
            tree.Walk().Where(n => n.Kind == UiKind.ProgressBar && n.Key.StartsWith("start_moderator:")).Select(n => n.Key["start_moderator:".Length..]));
        UiNode fuelSwitch = ByKey(tree, "start_fuel");
        Assert.Equal(("On", false), (fuelSwitch.Text, fuelSwitch.Disabled));
        UiNode fuelRate = ByKey(tree, "fuel_injection_rate");
        Assert.Equal((25.0, 0.5, 150.0, "mol/s"), (fuelRate.Value, fuelRate.MinValue, fuelRate.MaxValue, fuelRate.Unit));
        fuelRate.OnChange(30);
        Assert.Equal(("fuel_injection_rate", "{\"fuel_injection_rate\":30}"), acts[^1]);

        // the gauges (Parameters.tsx)
        UiNode integrity = ByKey(tree, "integrity");
        Assert.Equal((94.44, 95.0, "94%"), (integrity.Value, integrity.AlertBefore.Value, integrity.Format(integrity.Value)));
        Assert.Equal("30%", ByKey(tree, "iron").Format(0.3));
        Assert.Equal(3.0, ByKey(tree, "power_level").Value);
        UiNode energy = ByKey(tree, "energy");
        Assert.Equal((20.0, "100.00 EJ"), (energy.Value, energy.Format(energy.Value)));
        UiNode activity = ByKey(tree, "activity");
        Assert.Equal((50.0, "50.0%"), (activity.Value, activity.Format(activity.Value))); // 2500 of a 5000 max
        UiNode instability = ByKey(tree, "instability");
        Assert.Equal("50.0%", instability.Format(instability.Value)); // 4 of the 8 before the reaction turns endothermic

        // the knobs: the heating conductor yellow while it heats, the damper yellow while on; the max, default and min buttons
        UiNode conductor = ByKey(tree, "heating_conductor");
        Assert.Equal((UiKind.Knob, 200.0, 50.0, 500.0, 5.0, "J/cm", "yellow"), (conductor.Kind, conductor.Value, conductor.MinValue, conductor.MaxValue, conductor.Step, conductor.Unit, conductor.Color));
        Assert.Equal("yellow", ByKey(tree, "current_damper").Color);
        Assert.Equal(25.0, ByKey(tree, "cooling_volume").Step);
        Assert.Equal(180.0, ByKey(tree, "magnetic_constrictor:icon").Rotation);
        conductor.OnChange(255);
        Assert.Equal(("heating_conductor", "{\"heating_conductor\":255}"), acts[^1]);
        ByKey(tree, "heating_conductor:max").OnClick();
        Assert.Equal(("heating_conductor", "{\"heating_conductor\":500}"), acts[^1]);
        ByKey(tree, "cooling_volume:default").OnClick();
        Assert.Equal(("cooling_volume", "{\"cooling_volume\":100}"), acts[^1]);
        ByKey(tree, "magnetic_constrictor:min").OnClick();
        Assert.Equal(("magnetic_constrictor", "{\"magnetic_constrictor\":50}"), acts[^1]);

        // the output control: waste remove on, the rate field, the filters as checkboxes
        Assert.Equal(("On", true), (ByKey(tree, "waste_remove").Text, ByKey(tree, "waste_remove").Selected));
        Assert.Equal((20.0, 5.0, 200.0), (ByKey(tree, "mod_filtering_rate").Value, ByKey(tree, "mod_filtering_rate").MinValue, ByKey(tree, "mod_filtering_rate").MaxValue));
        UiNode plasmaFilter = ByKey(tree, "filter:plasma");
        Assert.Equal(("Plasma", false), (plasmaFilter.Text, plasmaFilter.Checkbox.Value));
        Assert.True(ByKey(tree, "filter:helium").Checkbox.Value);
        plasmaFilter.OnClick();
        Assert.Equal(("filter", "{\"mode\":\"plasma\"}"), acts[^1]);

        // the temperatures: values and their change per second (over the 2 s period), the empty output port
        Assert.Equal(("4.5e+4 K", "+2500 K/s"), (ByKey(tree, "temp:Fusion").Text, ByKey(tree, "delta:Fusion").Text)); // over 5000: exponential
        Assert.Equal(("3000 K", "-"), (ByKey(tree, "temp:Moderator").Text, ByKey(tree, "delta:Moderator").Text));
        Assert.Equal(("80 K", "-1 K/s"), (ByKey(tree, "temp:Coolant").Text, ByKey(tree, "delta:Coolant").Text));
        Assert.Equal("Empty", ByKey(tree, "temp:Output").Text);
        // the bars on the log scale between 20 K and the next level's 1e5 K: the fusion bar the tallest, the coolant the lowest
        double fusion = Px(ByKey(tree, "bar:Fusion").Height), moderator = Px(ByKey(tree, "bar:Moderator").Height), coolant = Px(ByKey(tree, "bar:Coolant").Height);
        Assert.Equal(200 * (Math.Log10(45000) - Math.Log10(20)) / (Math.Log10(1e5) - Math.Log10(20)), fusion, 1e-6);
        Assert.True(fusion > moderator && moderator > coolant);
        Assert.Equal("#f2711c", ByKey(tree, "bar:Fusion").Color);
        // the axis labels: the previous and next fusion levels (1e4 and 1e5 K, shown exponential over 5000)
        Assert.Contains(tree.Walk(), n => n.Key == "prev_fusion_temp" && n.AllText().Contains("1.0e+4 K"));
        Assert.Contains(tree.Walk(), n => n.Key == "next_fusion_temp" && n.AllText().Contains("1.0e+5 K"));
    }

    private static double Px(object size) => double.Parse(((string)size)[..^2], System.Globalization.CultureInfo.InvariantCulture);
}

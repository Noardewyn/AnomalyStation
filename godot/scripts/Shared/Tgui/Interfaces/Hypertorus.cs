using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Hypertorus/ (index.tsx, Controls.tsx, Gases.tsx,
// Parameters.tsx, Recipes.tsx, Temperatures.tsx, helpers.tsx), engineering package E12b: the HFR interface's window. It reads tg's
// ui_static_data and ui_data of the interface (hfr_parts.dm:189, :211) and sends tg's acts (start_power, start_cooling, start_fuel,
// start_moderator, heating_conductor, magnetic_constrictor, fuel_injection_rate, moderator_injection_rate, current_damper, waste_remove,
// filter, mod_filtering_rate, fuel, cooling_volume). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Laid out as tg's wrapping Flex lays it out at the window's ideal width (850 px): the main controls; the gases beside the temperatures;
// the reactor status; the reactor controls; the output control. Not drawn (the kit has no such parts): the window's clamp to half and
// four fifths of the screen (the host gives an interface no screen size; the player resizes it), the temperature chart's x axis and its tick
// lines (its bars, its y axis and the axis labels are drawn), the recipe table's column spans (the spanned columns are empty cells) and its highlight of
// the active row (the active recipe's button is selected).

public static class Hypertorus
{
    // tg: Temperatures.tsx:36  const height = 200 (kept in sync with Hypertorus.scss)
    private const double ChartHeight = 200;

    // tg: Temperatures.tsx:  .hypertorus-temperatures__y-axis-marks width (Hypertorus.scss $yAxisMargin: 60 + 14 + 2 * 2 px)
    private const double YAxisMargin = 78;

    // the height of an axis label's line (12 px text and its leading), for laying the labels out down their column
    private const double LabelLine = 14;

    // tg: index.tsx:139
    public static UiNode Render(TguiContext c)
    {
        // tg: index.tsx:142  idealWidth = 850, idealHeight = 980 (then min with 50%/80% of the screen: see the header)
        // tg: index.tsx:153  <Window title="Hypertorus Fusion Reactor control panel"> <Window.Content scrollable>
        return c.Window(850, 980, "Hypertorus Fusion Reactor control panel", Layout(c));
    }

    // tg: index.tsx:112  HypertorusLayout
    private static UiNode Layout(TguiContext c) => new UiNode(UiKind.Stack) { Vertical = true }.With(
        new UiNode(UiKind.StackItem) { Key = "main" }.With(MainControls(c)),
        new UiNode(UiKind.StackItem) { Key = "gases-temperatures" }.With(
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 20, Width = "350px", MinWidth = "280px" }.With(Gases(c)),
                new UiNode(UiKind.StackItem) { Grow = 1, Width = "420px" }.With(Temperatures(c)))),
        new UiNode(UiKind.StackItem) { Key = "parameters" }.With(Parameters(c)),
        new UiNode(UiKind.StackItem) { Key = "controls" }.With(SecondaryControls(c)),
        new UiNode(UiKind.StackItem) { Key = "waste" }.With(WasteRemove(c)));

    // tg: index.tsx:54  HypertorusMainControls
    private static UiNode MainControls(TguiContext c)
    {
        UiValue data = c.Data;
        double start_power = data.Num("start_power");
        double start_cooling = data.Num("start_cooling");
        double start_fuel = data.Num("start_fuel");
        double start_moderator = data.Num("start_moderator");
        double power_level = data.Num("power_level");
        double base_max_temperature = data.Num("base_max_temperature");

        return new UiNode(UiKind.Section).With(
            new UiNode(UiKind.Stack).With(
                // tg: index.tsx:68
                new UiNode(UiKind.StackItem) { Key = "start-power" }.With(
                    new UiNode(UiKind.Box) { Color = "label", Text = "Start power: " },
                    new UiNode(UiKind.Button)
                    {
                        Key = "start_power",
                        Disabled = power_level > 0,
                        Icon = start_power != 0 ? "power-off" : "times",
                        Text = start_power != 0 ? "On" : "Off",
                        Selected = start_power != 0,
                        OnClick = () => c.Act("start_power"),
                    }),
                // tg: index.tsx:78
                new UiNode(UiKind.StackItem) { Key = "start-cooling" }.With(
                    new UiNode(UiKind.Box) { Color = "label", Text = "Start cooling: " },
                    new UiNode(UiKind.Button)
                    {
                        Key = "start_cooling",
                        Disabled = start_fuel == 1 || start_moderator == 1 || start_power == 0 || (start_cooling != 0 && power_level > 0),
                        Icon = start_cooling != 0 ? "power-off" : "times",
                        Text = start_cooling != 0 ? "On" : "Off",
                        Selected = start_cooling != 0,
                        OnClick = () => c.Act("start_cooling"),
                    })),
            // tg: index.tsx:94
            new UiNode(UiKind.Collapsible) { Key = "recipes", Title = "Recipe selection" }.With(
                Recipes(c, base_max_temperature, power_level == 0, id => c.Act("fuel", ("mode", id)))));
    }

    // ---- helpers.tsx ----

    // tg: helpers.tsx:7  to_exponential_if_big
    internal static string ToExponentialIfBig(double value)
    {
        if (Math.Abs(value) > 5000)
            return ToExponential(value, 1);
        return TguiFormat.JsNumber(TguiFormat.Round(value));
    }

    // JavaScript's Number.prototype.toExponential(fractionDigits): "1.2e+5"; without digits, as many as the value needs ("8.5e+7")
    internal static string ToExponential(double value, int? fractionDigits = null)
    {
        if (!double.IsFinite(value))
            return TguiFormat.JsNumber(value);
        if (fractionDigits is int digits)
            return value.ToString((digits > 0 ? "0." + new string('0', digits) : "0") + "e+0", CultureInfo.InvariantCulture);
        for (int d = 0; d < 17; d++)
        {
            string s = value.ToString((d > 0 ? "0." + new string('0', d) : "0") + "e+0", CultureInfo.InvariantCulture);
            if (double.Parse(s, CultureInfo.InvariantCulture) == value)
                return s;
        }
        return value.ToString("0.################e+0", CultureInfo.InvariantCulture);
    }

    // tg: helpers.tsx:15  HoverHelp: a question mark icon with a hover tooltip
    private static UiNode HoverHelp(string content) => new UiNode(UiKind.Icon) { Icon = "question-circle", Width = "12px", Tooltip = content };

    // tg: helpers.tsx:23  HelpDummy: the same room without an icon
    private static UiNode HelpDummy() => new UiNode(UiKind.Icon) { Icon = "", Width = "12px" };

    // ---- Gases.tsx ----

    // tg: Gases.tsx:46
    private static readonly Dictionary<string, string> ModeratorGasesHelp = new()
    {
        ["plasma"] = "Produces basic gases. Has a modest heat bonus to help kick start the early fusion process. When added in large quantities, its high heat capacity can help to slow down temperature changes to manageable speeds.",
        ["bz"] = "Produces intermediate gases at Fusion Level 3 or higher. Massively increases radiation, and induces hallucinations in bystanders.",
        ["proto_nitrate"] = "Produces advanced gases. Massively increases radiation, and accelerates the rate of temperature change. Make sure you have enough cooling.",
        ["o2"] = "When added in high quantities, rapidly purges iron content. Does not purge iron content fast enough to keep up with damage at high Fusion Levels.",
        ["healium"] = "Directly heals a heavily damaged HFR core at high Fusion Levels, but is rapidly consumed in the process.",
        ["antinoblium"] = "Provides huge amounts of energy and radiation. Can cause dangerous electrical storms even from a healthy HFR core when present in more than trace amounts. Wear appropriate electrical protection when handling.",
        ["freon"] = "Saps most forms of energy expression. Slows the rate of temperature change.",
    };

    // tg: Gases.tsx:62
    private static readonly string[] ModeratorGasesStickyOrder = { "plasma", "bz", "proto_nitrate" };

    // tg: Gases.tsx:79  GasList
    private static UiNode GasList(TguiContext c, string input_rate, string input_switch, double input_max, double input_min, IReadOnlyList<UiValue> raw_gases,
        double minimumScale, Func<string, UiNode> prepend, string rateHelp, IEnumerable<string> stickyGases)
    {
        UiValue data = c.Data;
        double start_power = data.Num("start_power");
        double start_cooling = data.Num("start_cooling");

        // sortBy(filter(raw_gases, amount >= 0.01), [-amount]): a stable sort
        var gases = raw_gases
            .Select(g => (Id: g.Str("id"), Amount: g.Num("amount")))
            .Where(g => g.Amount >= 0.01)
            .OrderBy(g => -g.Amount)
            .ToList();

        // tg: Gases.tsx:64  ensure_gases: the sticky gases that are absent, at the end with no moles
        if (stickyGases != null)
            foreach (string gasid in stickyGases)
                if (!gases.Any(g => g.Id == gasid))
                    gases.Add((gasid, 0));

        bool switched = data.Truthy(input_switch);
        return new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem)
            {
                Key = "item:" + input_switch,
                LabelNode = new UiNode(UiKind.Fragment).With(HoverHelp(rateHelp), new UiNode(UiKind.Box) { Text = "Injection control:" }),
            }.With(
                new UiNode(UiKind.Button)
                {
                    Key = input_switch,
                    Disabled = start_power == 0 || start_cooling == 0,
                    Icon = switched ? "power-off" : "times",
                    Text = switched ? "On" : "Off",
                    Selected = switched,
                    OnClick = () => c.Act(input_switch),
                },
                new UiNode(UiKind.NumberInput)
                {
                    Key = input_rate,
                    Step = 1,
                    Value = ParseFloat(data.Field(input_rate)),
                    Unit = "mol/s",
                    MinValue = input_min,
                    MaxValue = input_max,
                    OnChange = v => c.Act(input_rate, (input_rate, v)),
                })).With(
            gases.Select(gas => new UiNode(UiKind.LabeledItem)
            {
                Key = input_switch + ":item:" + gas.Id,
                LabelNode = new UiNode(UiKind.Fragment).With(prepend?.Invoke(gas.Id), new UiNode(UiKind.Box) { Text = TguiFormat.GetGasLabel(gas.Id) + ":" }),
            }.With(
                new UiNode(UiKind.ProgressBar)
                {
                    Key = input_switch + ":" + gas.Id,
                    Color = TguiFormat.GetGasColor(gas.Id),
                    Value = gas.Amount,
                    MinValue = 0,
                    MaxValue = minimumScale,
                    Text = TguiFormat.ToFixed(gas.Amount, 2) + " moles",
                })));
    }

    // tg: Gases.tsx:163  HypertorusGases
    private static UiNode Gases(TguiContext c)
    {
        UiValue data = c.Data;
        string selected = data.Str("selected");
        UiValue selected_fuel = data.Arr("selectable_fuel").FirstOrDefault(d => d.Field("id") is { Kind: UiValueKind.String } id && id.Text == selected);

        return new UiNode(UiKind.Fragment).With(
            // tg: Gases.tsx:176
            new UiNode(UiKind.Section) { Key = "fusion", Title = "Internal Fusion Gases" }.With(
                selected_fuel != null
                    ? GasList(c, "fuel_injection_rate", "start_fuel", 150, 0.5, data.Arr("fusion_gases"), 500, _ => HelpDummy(),
                        "The rate at which new fuel is added from the fuel input port." +
                        " This rate affects the rate of production," +
                        " even when input is not active.",
                        selected_fuel.Arr("requirements").Select(r => r.AsStr()))
                    : new UiNode(UiKind.Box) { Key = "no-recipe", TextAlign = "center", Color = "red", Text = "No recipe selected" }),
            // tg: Gases.tsx:198
            new UiNode(UiKind.Section) { Key = "moderator", Title = "Moderator Gases" }.With(
                GasList(c, "moderator_injection_rate", "start_moderator", 150, 0.5, data.Arr("moderator_gases"), 500,
                    gasId => ModeratorGasesHelp.TryGetValue(gasId, out string help) ? HoverHelp(help) : HelpDummy(),
                    "The rate at which new moderator gas is added from the moderator port.",
                    ModeratorGasesStickyOrder)));
    }

    // ---- Parameters.tsx ----

    // tg: Parameters.tsx:33  HypertorusParameters
    private static UiNode Parameters(TguiContext c)
    {
        UiValue data = c.Data;
        double apc_energy = data.Num("apc_energy");
        double energy_level = data.Num("energy_level");
        double heat_output_max = data.Num("heat_output_max");
        double heat_output_min = data.Num("heat_output_min");
        double heat_output = data.Num("heat_output");
        double instability = data.Num("instability");
        double integrity = data.Num("integrity");
        double iron_content = data.Num("iron_content");
        double power_level = data.Num("power_level");
        double start_power = data.Num("start_power");

        const int energy_minimum_exponent = 12;
        const int energy_minimum_suffix = energy_minimum_exponent / 3;

        double activity = heat_output / (heat_output < 0 ? heat_output_min : heat_output_max);
        if (double.IsNaN(activity) || !double.IsFinite(activity))
            activity = 0;

        static UiNode Gauge(string label, UiNode gauge) => new UiNode(UiKind.LabeledControl) { Key = label, Label = label }.With(gauge);

        return new UiNode(UiKind.Section) { Title = "Reactor Status" }.With(
            new UiNode(UiKind.Stack) { Justify = "space-between", Wrap = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 360, MinWidth = "120px" }.With(
                    new UiNode(UiKind.LabeledControls) { Justify = "space-around", Wrap = true }.With(
                        // tg: Parameters.tsx:62
                        Gauge("Reactor Integrity", new UiNode(UiKind.RoundGauge)
                        {
                            Key = "integrity",
                            Size = 1.75,
                            Value = integrity,
                            MinValue = 0,
                            MaxValue = 100,
                            AlertBefore = 95,
                            Format = v => TguiFormat.JsNumber(TguiFormat.Round(v)) + "%",
                            Ranges = new[] { ("good", 90.0, 100.0), ("average", 50.0, 90.0), ("bad", 0.0, 50.0) },
                        }),
                        // tg: Parameters.tsx:77
                        Gauge("Iron Content", new UiNode(UiKind.RoundGauge)
                        {
                            Key = "iron",
                            Size = 1.75,
                            Value = iron_content,
                            MinValue = 0,
                            MaxValue = 1,
                            AlertAfter = 0.25,
                            Format = v => TguiFormat.JsNumber(TguiFormat.Round(v * 100)) + "%",
                            Ranges = new[] { ("good", 0.0, 0.1), ("average", 0.1, 0.36), ("bad", 0.36, 1.0) },
                        }),
                        // tg: Parameters.tsx:92
                        Gauge("Area Power", new UiNode(UiKind.RoundGauge)
                        {
                            Key = "apc",
                            Size = 1.75,
                            Value = apc_energy,
                            MinValue = 0,
                            MaxValue = 100,
                            AlertBefore = 30,
                            Format = v => TguiFormat.JsNumber(TguiFormat.Round(v)) + "%",
                            // Keep these in line with the auto-off thresholds in apc.dm
                            Ranges = new[] { ("bad", 0.0, 15.0), ("average", 15.0, 30.0), ("good", 30.0, 100.0) },
                        }))),
                new UiNode(UiKind.StackItem) { Grow = 140, MinWidth = "140px", Align = "center" }.With(
                    new UiNode(UiKind.LabeledControls) { Justify = "space-around" }.With(
                        // tg: Parameters.tsx:112
                        Gauge("Fusion Level", new UiNode(UiKind.RoundGauge)
                        {
                            Key = "power_level",
                            Size = 3,
                            MinValue = 0,
                            MaxValue = 6,
                            Value = power_level,
                            AlertAfter = 4.5,
                            Ranges = new[] { ("grey", 0.0, 1.0), ("good", 1.0, 3.5), ("average", 3.5, 4.5), ("bad", 4.5, 6.0) },
                        }))),
                new UiNode(UiKind.StackItem) { Grow = 360, MinWidth = "120px" }.With(
                    new UiNode(UiKind.LabeledControls) { Justify = "space-around", Wrap = true }.With(
                        // tg: Parameters.tsx:132
                        Gauge("Energy", new UiNode(UiKind.RoundGauge)
                        {
                            Key = "energy",
                            Size = 1.75,
                            Value = Math.Max(0, Math.Log10(energy_level)),
                            MinValue = energy_minimum_exponent,
                            MaxValue = 30,
                            Format = v => TguiFormat.FormatSiUnit(Math.Pow(10, v), energy_minimum_suffix, "J"),
                            Ranges = new[] { ("black", (double)energy_minimum_exponent, 15.0), ("grey", 15.0, 18.0), ("yellow", 18.0, 24.0), ("orange", 24.0, 30.0) },
                        }),
                        // tg: Parameters.tsx:149
                        Gauge("Reaction activity", new UiNode(UiKind.RoundGauge)
                        {
                            Key = "activity",
                            Size = 1.75,
                            Value = activity * 100,
                            MinValue = 0,
                            MaxValue = 130, // Proto-nitrate lets us exceed 100%
                            Format = v => (start_power != 0 ? TguiFormat.ToFixed(v, 1) : "0") + "%",
                            Ranges = new[] { ("grey", 0.0, 70.0), ("blue", 70.0, 100.0), ("orange", 100.0, 130.0) },
                        }),
                        // tg: Parameters.tsx:163
                        Gauge("Instability", new UiNode(UiKind.RoundGauge)
                        {
                            Key = "instability",
                            Size = 1.75,
                            Value = Math.Max(instability, 0),
                            MinValue = 0,
                            MaxValue = 10,
                            Format = v => (start_power != 0 ? TguiFormat.ToFixed((v / 8) * 100, 1) : "0") + "%",
                            Ranges = new[] { ("orange", 0.0, 8.0), ("blue", 8.0, 10.0) }, // exothermic, endothermic
                        })))));
    }

    // ---- Controls.tsx ----

    // tg: Controls.tsx:50  ComboKnob: the knob, an icon left of it and the max, default and min buttons right of it
    private static UiNode ComboKnob(TguiContext c, string parameter, double value, string unit, double minValue, double defaultValue, double maxValue,
        string icon, string help, string color = null, double step = 5, bool flipIcon = false)
    {
        UiNode icon_element = icon != null
            ? new UiNode(UiKind.Icon) { Key = parameter + ":icon", Color = "label", FontSize = "200%", Size = 2, Icon = icon, Rotation = flipIcon ? 180 : 0, Tooltip = help }
            : null;

        return new UiNode(UiKind.Stack) { Key = parameter + ":combo", Align = "center" }.With(
            icon_element,
            new UiNode(UiKind.Box).With(
                new UiNode(UiKind.Knob)
                {
                    Key = parameter,
                    Color = color,
                    Size = 2,
                    Value = value,
                    MinValue = minValue,
                    MaxValue = maxValue,
                    Step = step,
                    StepPixelSize = 1,
                    Unit = unit,
                    OnChange = v => c.Act(parameter, (parameter, v)),
                },
                new UiNode(UiKind.Button)
                {
                    Key = parameter + ":max",
                    Color = "transparent",
                    Icon = "fast-forward",
                    OnClick = () => c.Act(parameter, (parameter, maxValue)),
                },
                new UiNode(UiKind.Button)
                {
                    Key = parameter + ":default",
                    Color = "transparent",
                    Icon = "undo",
                    OnClick = () => c.Act(parameter, (parameter, defaultValue)),
                },
                new UiNode(UiKind.Button)
                {
                    Key = parameter + ":min",
                    Color = "transparent",
                    Icon = "fast-backward",
                    OnClick = () => c.Act(parameter, (parameter, minValue)),
                }));
    }

    // tg: Controls.tsx:132  HypertorusSecondaryControls
    private static UiNode SecondaryControls(TguiContext c)
    {
        UiValue data = c.Data;
        double cooling_volume = data.Num("cooling_volume");
        double current_damper = data.Num("current_damper");
        double heat_output = data.Num("heat_output");
        double heating_conductor = data.Num("heating_conductor");
        double magnetic_constrictor = data.Num("magnetic_constrictor");

        return new UiNode(UiKind.Section) { Title = "Reactor Control" }.With(
            new UiNode(UiKind.LabeledControls) { Justify = "space-around", Wrap = true }.With(
                new UiNode(UiKind.LabeledControl) { Key = "control:heating_conductor", Label = "Heating Conductor" }.With(
                    ComboKnob(c, "heating_conductor", heating_conductor, "J/cm", 50, 100, 500, "fire",
                        "Adjusts the rate the fusion reaction heats or cools. Higher heating values improve production at the risk of a runaway reaction.",
                        color: heating_conductor > 50 && heat_output > 0 ? "yellow" : null)),
                new UiNode(UiKind.LabeledControl) { Key = "control:cooling_volume", Label = "Cooling Volume" }.With(
                    ComboKnob(c, "cooling_volume", cooling_volume, "L", 50, 100, 2000, "snowflake-o",
                        "Adjusts the HFR core's internal cooling space. A smaller space will provide less cooling internally, but will move most of the coolant outside of the HFR core, where it can be rapidly cooled when not needed.",
                        step: 25)),
                new UiNode(UiKind.LabeledControl) { Key = "control:magnetic_constrictor", Label = "Magnetic Constrictor" }.With(
                    ComboKnob(c, "magnetic_constrictor", magnetic_constrictor, "m³/T", 50, 100, 1000, "magnet",
                        "Adjusts the density of the fusion reaction. Denser reactions expose more energy, but may destabilize the reaction if too much mass is involved.",
                        flipIcon: true)),
                new UiNode(UiKind.LabeledControl) { Key = "control:current_damper", Label = "Current Damper" }.With(
                    ComboKnob(c, "current_damper", current_damper, "W", 0, 0, 1000, "sun-o",
                        "Destabilizes the reaction. A sufficiently destabilized reaction will halt production and become endothermic, cooling the Fusion Mix instead of heating it. Reactions with more iron are harder to destabilize.",
                        color: current_damper != 0 ? "yellow" : null))));
    }

    // tg: Controls.tsx:200  HypertorusWasteRemove
    private static UiNode WasteRemove(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> filter_types = data.Arr("filter_types");
        bool waste_remove = data.Truthy("waste_remove");
        double mod_filtering_rate = data.Num("mod_filtering_rate");

        return new UiNode(UiKind.Section) { Title = "Output Control" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem)
                {
                    Key = "item:waste_remove",
                    LabelNode = new UiNode(UiKind.Fragment).With(
                        HoverHelp("Remove waste gases from Fusion," + " and any selected gases from the Moderator."),
                        new UiNode(UiKind.Box) { Text = "Waste remove:" }),
                }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "waste_remove",
                        Icon = waste_remove ? "power-off" : "times",
                        Text = waste_remove ? "On" : "Off",
                        Selected = waste_remove,
                        OnClick = () => c.Act("waste_remove"),
                    }),
                new UiNode(UiKind.LabeledItem)
                {
                    Key = "item:mod_filtering_rate",
                    LabelNode = new UiNode(UiKind.Fragment).With(HelpDummy(), new UiNode(UiKind.Box) { Text = "Moderator filtering rate:" }),
                }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "mod_filtering_rate",
                        Value = mod_filtering_rate,
                        Unit = "mol/s",
                        Step = 1,
                        MinValue = 5,
                        MaxValue = 200,
                        OnChange = value => c.Act("mod_filtering_rate", ("mod_filtering_rate", value)),
                    }),
                new UiNode(UiKind.LabeledItem)
                {
                    Key = "item:filters",
                    Wrap = true,
                    LabelNode = new UiNode(UiKind.Fragment).With(HelpDummy(), new UiNode(UiKind.Box) { Text = "Filter from moderator mix:" }),
                }.With(
                    filter_types.Select(filter =>
                    {
                        string gas_id = filter.Str("gas_id");
                        return new UiNode(UiKind.Button)
                        {
                            Key = "filter:" + gas_id,
                            Checkbox = filter.Truthy("enabled"),
                            Text = filter.Str("gas_name"),
                            OnClick = () => c.Act("filter", ("mode", gas_id)),
                        };
                    }).ToArray())));
    }

    // ---- Recipes.tsx ----

    // tg: Recipes.tsx:50  recipe_effect_structure
    private static readonly (string Param, string Label, string[] Icon, double Scale, double? OverrideBase, bool MaxTemperatureTooltip)[] RecipeEffectStructure =
    {
        ("recipe_cooling_multiplier", "Cooling", new[] { "snowflake-o" }, 3, null, false),
        ("recipe_heating_multiplier", "Heating", new[] { "fire" }, 3, null, false),
        ("energy_loss_multiplier", "Energy loss", new[] { "sun-o" }, 3, null, false),
        ("fuel_consumption_multiplier", "Fuel use", new[] { "window-minimize", "arrow-down" }, 1.5, null, false),
        ("gas_production_multiplier", "Production", new[] { "window-minimize", "arrow-up" }, 1.5, null, false),
        ("temperature_multiplier", "Max temperature", new[] { "thermometer-full" }, 1.15, 0.85, true),
    };

    // tg: Recipes.tsx:91
    private static string EffectToIcon(double effect_value, double effect_scale, double @base)
    {
        if (effect_value == @base)
            return "minus";
        if (effect_value > @base)
        {
            if (effect_value > @base * effect_scale)
                return "angle-double-up";
            return "angle-up";
        }
        if (effect_value < @base / effect_scale)
            return "angle-double-down";
        return "angle-down";
    }

    // tg: Recipes.tsx:120  GasCellItem
    private static UiNode GasCellItem(string gasid) => string.IsNullOrEmpty(gasid)
        ? new UiNode(UiKind.TableCell)
        : new UiNode(UiKind.TableCell) { Key = gasid, Color = TguiFormat.GetGasColor(gasid), Text = TguiFormat.GetGasLabel(gasid) };

    // a header cell (tgui-core's header row: bold, the label colour, centred, Hypertorus.scss)
    private static UiNode HeaderCell(string text = null) => new UiNode(UiKind.TableCell) { Text = text, Bold = true, Color = "label", TextAlign = "center" };

    // a header cell spanning more columns (colSpan): the cell, then empty cells for the rest of the span
    private static IEnumerable<UiNode> Spanned(string text, int colSpan) => Enumerable.Range(0, colSpan).Select(i => HeaderCell(i == 0 ? text : null));

    // tg: Recipes.tsx:134  HypertorusRecipes
    private static UiNode Recipes(TguiContext c, double baseMaxTemperature, bool enableRecipeSelection, Action<string> onRecipe)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> selectable_fuel = data.Arr("selectable_fuel");
        string selected = data.Str("selected");

        static string Nth(IReadOnlyList<UiValue> list, int i) => i < list.Count ? list[i].AsStr() : null;

        UiNode table = new UiNode(UiKind.Table).With(
            // tg: Recipes.tsx:142
            new UiNode(UiKind.TableRow) { Key = "header1" }.With(HeaderCell()).With(Spanned("Fuel", 2)).With(Spanned("Fusion Byproducts", 2))
                .With(Spanned("Produced gases", 6)).With(Spanned("Effects", 6)),
            // tg: Recipes.tsx:149
            new UiNode(UiKind.TableRow) { Key = "header2" }.With(HeaderCell(), HeaderCell("Primary"), HeaderCell("Secondary")).With(Spanned(null, 2))
                .With(Enumerable.Range(1, 6).Select(t => HeaderCell("Tier " + t)))
                .With(RecipeEffectStructure.Select(e => new UiNode(UiKind.TableCell) { Key = e.Param, Color = "label", TextAlign = "center", Tooltip = e.Label }.With(EffectIcon(e.Icon, "label")))));

        // tg: Recipes.tsx:183  the recipes with an id (the "Nothing" entry has none)
        foreach (UiValue recipe in selectable_fuel.Where(r => r.Field("id") is { Kind: UiValueKind.String }))
        {
            string id = recipe.Str("id");
            bool active = id == selected;
            IReadOnlyList<UiValue> requirements = recipe.Arr("requirements");
            IReadOnlyList<UiValue> fusion_byproducts = recipe.Arr("fusion_byproducts");
            table.With(new UiNode(UiKind.TableRow) { Key = id, Candystripe = true }.With(
                new UiNode(UiKind.TableCell).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "recipe:" + id,
                        Icon = active ? "times" : "power-off",
                        Disabled = !enableRecipeSelection,
                        Selected = active,
                        OnClick = () => onRecipe(id),
                    }),
                GasCellItem(Nth(requirements, 0)),
                GasCellItem(Nth(requirements, 1)),
                GasCellItem(Nth(fusion_byproducts, 0)),
                GasCellItem(Nth(fusion_byproducts, 1))).With(
                recipe.Arr("product_gases").Select(g => GasCellItem(g.AsStr()))).With(
                RecipeEffectStructure.Select(e =>
                {
                    double value = recipe.Num(e.Param);
                    string tooltip = e.MaxTemperatureTooltip
                        ? "Maximum: " + ToExponential(baseMaxTemperature * value) + " K"
                        : "x" + TguiFormat.JsNumber(value);
                    // Note that the minus icon is wider than the arrow icons, so we set the width to work with both without jumping.
                    return new UiNode(UiKind.TableCell) { Key = id + ":" + e.Param, TextAlign = "center", Tooltip = tooltip }.With(
                        new UiNode(UiKind.Icon) { Icon = EffectToIcon(value, e.Scale, e.OverrideBase ?? 1), Color = "red", Width = "10px" });
                })));
        }
        // tg: Recipes.tsx:139  <Box overflowX="auto">
        return new UiNode(UiKind.Box) { Key = "recipes" }.With(table);
    }

    // tg: Recipes.tsx:160  an effect's pictographic header: one icon, or an Icon.Stack of two drawn over each other
    private static UiNode EffectIcon(string[] icons, string color) => icons.Length == 1
        ? new UiNode(UiKind.Icon) { Icon = icons[0], Color = color, Width = "10px" }
        : new UiNode(UiKind.Overlay) { Width = "14px", Height = "14px" }.With(icons.Select(i => new UiNode(UiKind.Icon) { Icon = i, Color = color }));

    // ---- Temperatures.tsx ----

    // tg: Temperatures.tsx:76  HypertorusTemperatures
    private static UiNode Temperatures(TguiContext c)
    {
        UiValue data = c.Data;
        double base_max_temperature = data.Num("base_max_temperature");
        double internal_coolant_temperature_archived = data.Num("internal_coolant_temperature_archived");
        double internal_coolant_temperature = data.Num("internal_coolant_temperature");
        double internal_fusion_temperature_archived = data.Num("internal_fusion_temperature_archived");
        double internal_fusion_temperature = data.Num("internal_fusion_temperature");
        double internal_output_temperature_archived = data.Num("internal_output_temperature_archived");
        double internal_output_temperature = data.Num("internal_output_temperature");
        double moderator_internal_temperature_archived = data.Num("moderator_internal_temperature_archived");
        double moderator_internal_temperature = data.Num("moderator_internal_temperature");
        double power_level = data.Num("power_level");
        string selected = data.Str("selected");
        double temperature_period = data.Num("temperature_period");

        double internal_fusion_temperature_delta = (internal_fusion_temperature - internal_fusion_temperature_archived) / temperature_period;
        double internal_output_temperature_delta = (internal_output_temperature - internal_output_temperature_archived) / temperature_period;
        double internal_coolant_temperature_delta = (internal_coolant_temperature - internal_coolant_temperature_archived) / temperature_period;
        double moderator_internal_temperature_delta = (moderator_internal_temperature - moderator_internal_temperature_archived) / temperature_period;

        UiValue selected_fuel = data.Arr("selectable_fuel").FirstOrDefault(d => d.Field("id") is { Kind: UiValueKind.String } id && id.Text == selected);

        double prev_power_level_temperature = Math.Pow(10, 1 + power_level);
        double next_power_level_temperature = Math.Pow(10, 2 + power_level);

        if (power_level == 0)
        {
            prev_power_level_temperature = 0;
            next_power_level_temperature = 500;
        }
        else if (power_level == 1)
            prev_power_level_temperature = 500;
        else if (power_level == 6)
            next_power_level_temperature = base_max_temperature * (selected_fuel?.Field("temperature_multiplier") is { Kind: UiValueKind.Number } m ? m.Number : 1);

        var temperatures = new List<double> { prev_power_level_temperature, next_power_level_temperature };
        temperatures.AddRange(new[] { internal_fusion_temperature, moderator_internal_temperature, internal_output_temperature, internal_coolant_temperature }.Where(d => d != 0));

        double maxTemperature = temperatures.Max();
        double minTemperature = Math.Max(2.73, Math.Min(20, temperatures.Where(d => d > 0).DefaultIfEmpty(double.PositiveInfinity).Min()));

        if (power_level == 6)
            next_power_level_temperature = 0;

        double value_to_y(double value)
        {
            double ratio = (Math.Log10(value) - Math.Log10(minTemperature)) / (Math.Log10(maxTemperature) - Math.Log10(minTemperature));
            return ChartHeight * ratio;
        }

        // tg: Temperatures.tsx:200  If two axis labels are too close to one another, don't show them.
        const double clutter_threshold = 20;
        bool label_legible(double l, double r) => Math.Abs(value_to_y(l) - value_to_y(r)) > clutter_threshold;

        bool show_min = label_legible(prev_power_level_temperature, minTemperature) || power_level == 0;
        bool show_max = label_legible(next_power_level_temperature, maxTemperature);

        // tg: Temperatures.tsx:140  TemperatureLabel: a label at its value's height (shown when it has a value, or forced)
        var labels = new List<(double Top, UiNode Label)>();
        void TemperatureLabel(string key, double value, string icon = null, string tooltip = null, bool force = false)
        {
            if (value == 0 && !force)
                return;
            double top = ChartHeight - value_to_y(value);
            UiNode label = new UiNode(UiKind.Stack) { Key = key, Justify = "end", Tooltip = tooltip }.With(
                icon != null ? new UiNode(UiKind.Icon) { Icon = icon, Color = "label" } : null,
                new UiNode(UiKind.Box) { Color = "label", TextAlign = "right", Text = ToExponentialIfBig(value) + " K" });
            labels.Add((double.IsFinite(top) ? top : ChartHeight, label));
        }

        if (show_min)
            TemperatureLabel("min_temp", minTemperature, force: true);
        TemperatureLabel("prev_fusion_temp", prev_power_level_temperature, "chevron-down", "Previous Fusion Level");
        TemperatureLabel("next_fusion_temp", next_power_level_temperature, "chevron-up", "Next Fusion Level");
        if (show_max)
            TemperatureLabel("max_temp", maxTemperature);

        // the labels' column: each label's top at its tick, as tg's absolutely placed anchors (a label too close to the one above it follows it)
        UiNode axis = new UiNode(UiKind.Stack) { Key = "y-axis-marks", Vertical = true, Width = Px(YAxisMargin) };
        double bottom = 0;
        foreach ((double top, UiNode label) in labels.OrderBy(l => l.Top))
        {
            if (top - bottom >= 1)
                axis.With(Spacer(top - bottom));
            axis.With(label);
            bottom = Math.Max(bottom, top) + LabelLine;
        }
        if (bottom < ChartHeight)
            axis.With(Spacer(ChartHeight - bottom));

        // tg: Temperatures.tsx:178  TemperatureBar: the vertical bar (its top at the value's height) and its label under it
        UiNode TemperatureBar(string label, double value, double delta, string color)
        {
            double y = value_to_y(value);
            UiNode bar = new UiNode(UiKind.Stack) { Vertical = true, Width = "17px" };
            if (value != 0 && double.IsFinite(y) && y >= 1)
            {
                double barHeight = Math.Min(y, ChartHeight);
                if (ChartHeight - barHeight >= 1)
                    bar.With(Spacer(ChartHeight - barHeight));
                bar.With(new UiNode(UiKind.ColorBox) { Key = "bar:" + label, Color = color, Width = "17px", Height = Px(barHeight) });
            }
            else
                bar.With(Spacer(ChartHeight));

            // tg: Temperatures.tsx:49  BarLabel
            UiNode barLabel = value > 0
                ? new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Box) { TextAlign = "center", Text = label },
                    new UiNode(UiKind.Box) { Key = "temp:" + label, TextAlign = "center", Text = ToExponentialIfBig(value) + " K" },
                    new UiNode(UiKind.Box)
                    {
                        Key = "delta:" + label,
                        TextAlign = "center",
                        Text = delta == 0 ? "-" : (delta < 0 ? "" : "+") + ToExponentialIfBig(delta) + " K/s",
                    })
                : new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Box) { TextAlign = "center", Text = label },
                    new UiNode(UiKind.Box) { Key = "temp:" + label, TextAlign = "center", Color = "red", Text = "Empty" },
                    new UiNode(UiKind.Box) { Text = " " });

            return new UiNode(UiKind.StackItem) { Key = label }.With(
                new UiNode(UiKind.Stack) { Vertical = true, Align = "center" }.With(
                    new UiNode(UiKind.StackItem).With(bar),
                    new UiNode(UiKind.StackItem) { Color = "label" }.With(barLabel)));
        }

        return new UiNode(UiKind.Section) { Title = "Gas Monitoring" }.With(
            new UiNode(UiKind.Stack) { Key = "temperatures" }.With(
                new UiNode(UiKind.StackItem).With(axis),
                // tg: .hypertorus-temperatures__y-axis: the axis line beside the labels
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.ColorBox) { Color = "label", Width = "1px", Height = Px(ChartHeight) }),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Stack) { Key = "chart", Justify = "space-around" }.With(
                        TemperatureBar("Fusion", internal_fusion_temperature, internal_fusion_temperature_delta, "#f2711c"),
                        TemperatureBar("Moderator", moderator_internal_temperature, moderator_internal_temperature_delta, "#e03997"),
                        TemperatureBar("Coolant", internal_coolant_temperature, internal_coolant_temperature_delta, "aliceblue"),
                        TemperatureBar("Output", internal_output_temperature, internal_output_temperature_delta, "#20b142")))));
    }

    // an empty box of a height in pixels (tg's absolute positions within the chart's 200 px)
    private static UiNode Spacer(double height) => new UiNode(UiKind.ColorBox) { Width = "1px", Height = Px(height) };

    private static string Px(double pixels) => pixels.ToString(CultureInfo.InvariantCulture) + "px";

    // JavaScript's parseFloat of a number or a numeric text
    private static double ParseFloat(UiValue v) => v?.Kind == UiValueKind.Number ? v.Number
        : v?.Kind == UiValueKind.String && double.TryParse(v.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : double.NaN;
}

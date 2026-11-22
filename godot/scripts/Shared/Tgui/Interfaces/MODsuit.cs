using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MODsuit.tsx (gaps MO1): the MODsuit's panel
// (mod/mod_ui.dm ui_data): the suit status (activation, the charge bar, the ID lock, the MODlink call, the open cover, the shorted
// circuits, the pAI eject, the status readout's operation time), the user status (the status readout module's vitals, radiation,
// DNA and diseases), the modules (select, configure, pin; each module's configuration entries: number, bool, color, list, button,
// pin) and the hardware (the control unit, the core, each part with its deploy button, the AI assistant). The content is
// MODsuitContent, which NtosMODsuit shows too. Engine-free (compiled into the tests too).
// Kit approximations: tgui-core's Dimmer over the user status (LockedModule) is a box at the top of that section, as other
// windows draw a Dimmer (AntagInfoChangeling.cs); Table.Cell's colSpan of the header's "Actions" is that cell and two empty ones;
// AnimatedNumber is its value as text; the parts section's text-transform capitalize is left to the kit's text.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MODsuit
{
    private static readonly (string, double, double)[] HealthRanges =
    {
        ("good", 0.5, double.PositiveInfinity),
        ("average", 0.2, 0.5),
        ("bad", double.NegativeInfinity, 0.2),
    };

    private static readonly (string, double, double)[] DamageRanges =
    {
        ("good", double.NegativeInfinity, 0.2),
        ("average", 0.2, 0.5),
        ("bad", 0.5, double.PositiveInfinity),
    };

    // JavaScript's a / b where the bar takes the quotient (a zero maximum gives NaN there; the bar shows it empty)
    private static double Ratio(double a, double b)
    {
        double r = a / b;
        return double.IsFinite(r) ? r : 0;
    }

    // JavaScript's Math.round (halves round up)
    private static double JsRound(double v) => Math.Floor(v + 0.5);

    // tg: MODsuit.tsx:131
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool interfaceBreak = data.Field("suit_status").Truthy("interface_break");
        c.WindowTheme = data.Str("ui_theme", null);
        // tg: MODsuit.tsx:136  <Window width={600} height={600} theme={ui_theme} title="MOD Interface Panel">
        return c.Window(600, 600, "MOD Interface Panel",
            new UiNode(UiKind.Box) { Key = "content", Fill = true, Scrollable = !interfaceBreak }.With(Content(c)));
    }

    /// <summary>tg's MODsuitContent (MODsuit.tsx:150): the locked interface, or the four sections.</summary>
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        bool interfaceBreak = data.Field("suit_status").Truthy("interface_break");
        if (interfaceBreak)
            return new UiNode(UiKind.Box) { Key = "modsuit" }.With(LockedInterface());
        return new UiNode(UiKind.Box) { Key = "modsuit" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(SuitStatusSection(c)),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(UserStatusSection(c)))),
            new UiNode(UiKind.StackItem).With(ModuleSection(c)),
            new UiNode(UiKind.StackItem).With(HardwareSection(c))));
    }

    // tg: MODsuit.tsx:295
    private static UiNode LockedInterface() =>
        new UiNode(UiKind.Section) { Key = "locked", TextAlign = "center", Fill = true }.With(
            new UiNode(UiKind.Icon) { Key = "locked_icon", Color = "red", Icon = "exclamation-triangle", Size = 15 },
            new UiNode(UiKind.Box) { Key = "locked_text", FontSize = "30px", Color = "red", Text = "ERROR: INTERFACE UNRESPONSIVE" });

    // tg: MODsuit.tsx:304  <Dimmer><Stack><Stack.Item fontSize="16px" color="blue">SUIT UNPOWERED</Stack.Item></Stack></Dimmer>
    private static UiNode LockedModule() =>
        new UiNode(UiKind.Box) { Key = "unpowered", FontSize = "16px", Color = "blue", TextAlign = "center", Text = "SUIT UNPOWERED" };

    // tg: MODsuit.tsx:344
    private static string ModuleTypeAction(double type) => type switch
    {
        1 => "Use",
        2 => "Toggle",
        3 => "Select",
        _ => null,
    };

    // tg: MODsuit.tsx:355
    private static string RadiationLevels(double level) => level switch
    {
        1 => "Low",
        2 => "Medium",
        3 => "High",
        4 => "Extreme",
        _ => "undefined",
    };

    // tg: MODsuit.tsx:368
    private static UiNode SuitStatusSection(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue suit = data.Field("suit_status");
        UiValue custom = data.Field("module_custom_status");
        bool active = suit.Truthy("active");
        bool malfunctioning = suit.Truthy("malfunctioning");
        bool locked = suit.Truthy("locked");
        string linkFreq = suit.Str("link_freq", null);
        string linkCall = suit.Str("link_call", null);
        bool hasFreq = !string.IsNullOrEmpty(linkFreq);
        bool calling = !string.IsNullOrEmpty(linkCall);
        string status = malfunctioning ? "Malfunctioning" : active ? "Active" : "Inactive";

        var list = new UiNode(UiKind.LabeledList) { Key = "suit_list" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "charge_row", Label = "Charge" }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "charge",
                Value = Ratio(suit.Num("charge_current"), suit.Num("charge_max")),
                Color = suit.Str("chargebar_color", null),
                Text = suit.Str("chargebar_string"),
            }),
            new UiNode(UiKind.LabeledItem) { Key = "lock_row", Label = "ID Lock" }.With(new UiNode(UiKind.Button)
            {
                Key = "lock",
                Icon = locked ? "lock" : "lock-open",
                Color = locked ? "good" : "default",
                Text = locked ? "Locked" : "Unlocked",
                OnClick = () => c.Act("lock"),
            }),
            new UiNode(UiKind.LabeledItem) { Key = "link_row", Label = "MODLink" }.With(new UiNode(UiKind.Button)
            {
                Key = "call",
                Icon = "wifi",
                Color = calling ? "good" : "default",
                Disabled = !hasFreq,
                Tooltip = hasFreq ? "" : "Set a frequency with a multitool!",
                Text = hasFreq ? (calling ? "Calling (" + linkCall + ")" : "Call (" + suit.Str("link_id") + ")") : "Frequency Unset",
                OnClick = () => c.Act("call"),
            }),
            suit.Truthy("open")
                ? new UiNode(UiKind.LabeledItem) { Key = "cover_row", Label = "Cover" }.With(new UiNode(UiKind.Box) { Key = "cover", Color = "red", Text = "Open" })
                : null,
            suit.Truthy("seconds_electrified")
                ? new UiNode(UiKind.LabeledItem) { Key = "circuits_row", Label = "Circuits" }.With(new UiNode(UiKind.Box) { Key = "circuits", Color = "red", Text = "Shorted" })
                : null,
            suit.Truthy("ai_name")
                ? new UiNode(UiKind.LabeledItem) { Key = "pai_row", Label = "pAI Control" }.With(suit.Truthy("has_pai")
                    ? new UiNode(UiKind.Button)
                    {
                        Key = "eject_pai",
                        Icon = "eject",
                        Text = "Eject pAI",
                        Disabled = suit.Truthy("is_ai"),
                        OnClick = () => c.Act("eject_pai"),
                    }
                    : null)
                : null);

        var section = new UiNode(UiKind.Section)
        {
            Key = "suit_status",
            Title = "Suit Status",
            Fill = true,
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "activate",
                Icon = "power-off",
                Color = active ? "good" : "default",
                Text = status,
                OnClick = () => c.Act("activate"),
            },
        }.With(list);

        // tg: MODsuit.tsx:470  the status readout's operation time
        if (custom.Truthy("display_time"))
        {
            string shiftId = custom.Str("shift_id");
            section.With(new UiNode(UiKind.Section) { Key = "operation", Title = "Operation" }.With(
                new UiNode(UiKind.LabeledItem) { Key = "time_row", Label = "Time" }.With(
                    new UiNode(UiKind.Box) { Key = "time", Text = active ? custom.Str("shift_time") : "00:00:00" }),
                new UiNode(UiKind.LabeledItem) { Key = "number_row", Label = "Number" }.With(
                    new UiNode(UiKind.Box) { Key = "number", Text = active && !string.IsNullOrEmpty(shiftId) ? shiftId : "???" })));
        }
        return section;
    }

    // tg: MODsuit.tsx:485
    private static UiNode HardwareSection(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue suit = data.Field("suit_status");
        string coreName = suit.Str("core_name");
        string aiName = suit.Str("ai_name");
        var list = new UiNode(UiKind.LabeledList) { Key = "hardware_list" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "control_row", Label = "Control Unit" }.With(
                new UiNode(UiKind.Box) { Key = "control", Text = data.Str("control") }),
            new UiNode(UiKind.LabeledItem) { Key = "core_row", Label = "Core" }.With(
                new UiNode(UiKind.Box) { Key = "core", Text = string.IsNullOrEmpty(coreName) ? "No Core Detected" : coreName }));
        list.With(ModParts(c));
        list.With(new UiNode(UiKind.LabeledItem) { Key = "ai_row", Label = "AI Assistant" }.With(
            new UiNode(UiKind.Box) { Key = "ai", Text = string.IsNullOrEmpty(aiName) ? "No AI Detected" : aiName }));
        return new UiNode(UiKind.Section) { Key = "hardware", Title = "Hardware" }.With(list);
    }

    // tg: MODsuit.tsx:505
    private static IEnumerable<UiNode> ModParts(TguiContext c)
    {
        foreach (UiValue part in c.Data.Arr("parts"))
        {
            bool deployed = part.Truthy("deployed");
            string slot = part.Str("slot");
            string partRef = part.Str("ref");
            yield return new UiNode(UiKind.LabeledItem)
            {
                Key = "part:" + slot,
                Label = slot + " Slot",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "deploy:" + slot,
                    Selected = deployed,
                    Icon = deployed ? "arrow-down" : "arrow-up",
                    Text = deployed ? "Retract" : "Deploy",
                    OnClick = () => c.Act("deploy", ("ref", partRef)),
                },
            }.With(new UiNode(UiKind.Box) { Key = "part_name:" + slot, Text = part.Str("name") });
        }
    }

    // tg: MODsuit.tsx:532
    private static UiNode UserStatusSection(TguiContext c)
    {
        UiValue data = c.Data;
        bool active = data.Field("suit_status").Truthy("active");
        UiValue user = data.Field("user_status");
        UiValue custom = data.Field("module_custom_status");
        double healthMax = custom.Num("health_max");

        // tgui's `x !== undefined`: the field is there (null included)
        bool Has(string key) => custom?.Get(key) != null;

        UiNode Bar(string key, string label, string field, (string, double, double)[] ranges, double max) =>
            Has(field)
                ? new UiNode(UiKind.LabeledItem) { Key = key + "_row", Label = label }.With(new UiNode(UiKind.ProgressBar)
                {
                    Key = key,
                    Value = active ? Ratio(custom.Num(field), max) : 0,
                    Ranges = ranges,
                    Text = TguiFormat.JsNumber(active ? custom.Num(field) : 0),
                })
                : null;

        UiNode radiation = null;
        if (Has("background_radiation_level"))
        {
            double level = custom.Num("background_radiation_level");
            UiNode shown = !active
                ? new UiNode(UiKind.Box) { Key = "radiation", Text = "Unknown" }
                : custom.Truthy("is_user_irradiated")
                    ? new UiNode(UiKind.NoticeBox) { Key = "radiation", Color = "danger", Text = "User Irradiated" }
                    : level != 0
                        ? new UiNode(UiKind.NoticeBox) { Key = "radiation", Text = "Background: " + RadiationLevels(level) }
                        : new UiNode(UiKind.NoticeBox) { Key = "radiation", Color = "info", Text = "Not Detected" };
            radiation = new UiNode(UiKind.LabeledItem) { Key = "radiation_row", Label = "Radiation" }.With(shown);
        }

        var list = new UiNode(UiKind.LabeledList) { Key = "user_list" }.With(
            Bar("health", "Health", "health", HealthRanges, healthMax),
            Bar("brute", "Brute Damage", "loss_brute", DamageRanges, healthMax),
            Bar("burn", "Burn Damage", "loss_fire", DamageRanges, healthMax),
            Bar("oxy", "Oxy Damage", "loss_oxy", DamageRanges, healthMax),
            Bar("tox", "Tox Damage", "loss_tox", DamageRanges, healthMax),
            radiation,
            Has("body_temperature")
                ? new UiNode(UiKind.LabeledItem) { Key = "temp_row", Label = "Body Temp" }.With(
                    new UiNode(UiKind.Box) { Key = "temp", Text = TguiFormat.JsNumber(active ? JsRound(custom.Num("body_temperature")) : 0) + " K" })
                : null,
            Has("nutrition")
                ? new UiNode(UiKind.LabeledItem) { Key = "nutrition_row", Label = "Satiety Level" }.With(
                    new UiNode(UiKind.Box) { Key = "nutrition", Text = TguiFormat.JsNumber(active ? JsRound(custom.Num("nutrition")) : 0) })
                : null,
            new UiNode(UiKind.LabeledItem) { Key = "name_row", Label = "Name" }.With(new UiNode(UiKind.Box) { Key = "user_name", Text = user.Str("user_name") }),
            new UiNode(UiKind.LabeledItem) { Key = "assignment_row", Label = "Assignment" }.With(
                new UiNode(UiKind.Box) { Key = "user_assignment", Text = user.Str("user_assignment") }),
            Has("dna_unique_identity")
                ? new UiNode(UiKind.LabeledItem) { Key = "fingerprints_row", Label = "Fingerprints" }.With(
                    new UiNode(UiKind.Box) { Key = "fingerprints", Wrap = true, Text = active ? custom.Str("dna_unique_identity") : "???" })
                : null,
            Has("dna_unique_enzymes")
                ? new UiNode(UiKind.LabeledItem) { Key = "enzymes_row", Label = "Enzymes" }.With(
                    new UiNode(UiKind.Box) { Key = "enzymes", Wrap = true, Text = active ? custom.Str("dna_unique_enzymes") : "???" })
                : null);

        var section = new UiNode(UiKind.Section) { Key = "user_status", Title = "User Status", Fill = true };
        if (!active)
            section.With(LockedModule());
        section.With(list);

        // tg: MODsuit.tsx:689  the diseases
        if (custom.Truthy("viruses"))
        {
            var diseases = new UiNode(UiKind.Section) { Key = "diseases", Title = "Diseases" };
            foreach (UiValue virus in custom.Arr("viruses"))
            {
                string name = virus.Str("name");
                diseases.With(new UiNode(UiKind.Collapsible) { Key = "virus:" + name, Title = name }.With(
                    new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Label = "Spread" }.With(new UiNode(UiKind.Box) { Text = virus.Str("type") }),
                        new UiNode(UiKind.LabeledItem) { Label = "Stage" }.With(
                            new UiNode(UiKind.Box) { Key = "stage:" + name, Text = virus.Str("stage") + "/" + virus.Str("maxstage") }),
                        new UiNode(UiKind.LabeledItem) { Label = "Cure" }.With(new UiNode(UiKind.Box) { Text = virus.Str("cure") }))));
            }
            section.With(diseases);
        }
        return section;
    }

    // tg: MODsuit.tsx:712
    private static UiNode ModuleSection(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> modules = data.Arr("module_info");
        string configureState = c.UseState("configureState", "");
        var section = new UiNode(UiKind.Section)
        {
            Key = "modules",
            Title = "Modules",
            Fill = true,
            Buttons = new UiNode(UiKind.Box)
            {
                Key = "complexity",
                Text = data.Field("suit_status").Str("complexity") + " of " + data.Str("complexity_max") + " complexity used",
            },
        };
        if (modules.Count == 0)
            return section.With(new UiNode(UiKind.NoticeBox) { Key = "no_modules", Text = "No Modules Detected" });

        UiNode HeaderIcon(string key, string icon, string tooltip) =>
            new UiNode(UiKind.TableCell) { Key = "header_" + key, Width = 1, TextAlign = "center" }.With(
                new UiNode(UiKind.Button) { Color = "transparent", Icon = icon, Tooltip = tooltip });

        var table = new UiNode(UiKind.Table) { Key = "module_table" }.With(new UiNode(UiKind.TableRow) { Key = "headers" }.With(
            new UiNode(UiKind.TableCell) { Key = "header_actions", Bold = true, Text = "Actions" },
            new UiNode(UiKind.TableCell) { Key = "header_actions_2" },
            new UiNode(UiKind.TableCell) { Key = "header_actions_3" },
            new UiNode(UiKind.TableCell) { Key = "header_name", Bold = true, Text = "Name" },
            HeaderIcon("idle", "plug", "Idle Power Cost (Watts)"),
            HeaderIcon("active", "lightbulb", "Active Power Cost (Watts)"),
            HeaderIcon("use", "bolt", "Use Energy Cost (Joules)"),
            HeaderIcon("complexity", "save", "Complexity")));

        foreach (UiValue module in modules)
        {
            string moduleRef = module.Str("ref");
            double moduleType = module.Num("module_type");
            bool moduleActive = module.Truthy("module_active");
            IReadOnlyList<KeyValuePair<string, UiValue>> configuration = module.Obj("configuration_data");
            bool configuring = configureState == moduleRef;

            var nameCell = new UiNode(UiKind.TableCell) { Key = "name:" + moduleRef }.With(
                new UiNode(UiKind.Collapsible)
                {
                    Key = "module:" + moduleRef,
                    Title = module.Str("module_name"),
                    Color = moduleActive ? "green" : "default",
                }.With(new UiNode(UiKind.Section) { Key = "description:" + moduleRef, Text = module.Str("description") }));
            if (configuring)
                nameCell.With(ConfigureScreen(c, configuration, moduleRef));

            table.With(new UiNode(UiKind.TableRow) { Key = "row:" + moduleRef }.With(
                new UiNode(UiKind.TableCell) { Width = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "select:" + moduleRef,
                    Icon = moduleType == 3 ? (moduleActive ? "check-square-o" : "square-o") : "power-off",
                    Selected = moduleActive,
                    Tooltip = ModuleTypeAction(moduleType),
                    Disabled = moduleType == 0 || module.Num("cooldown") > 0,
                    OnClick = () => c.Act("select", ("ref", moduleRef)),
                }),
                new UiNode(UiKind.TableCell) { Width = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "configure:" + moduleRef,
                    Icon = "cog",
                    Selected = configuring,
                    Tooltip = "Configure",
                    // tg's configuration_data.length === 0: an empty list arrives as [] (an array), a filled one as an object
                    Disabled = module.Get("configuration_data") is { Kind: UiValueKind.Array } empty && empty.Items.Count == 0,
                    OnClick = () => c.SetState("configureState", configureState == moduleRef ? "" : moduleRef),
                }),
                new UiNode(UiKind.TableCell) { Width = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "pin:" + moduleRef,
                    Icon = "thumbtack",
                    Selected = module.Truthy("pinned"),
                    Tooltip = "Pin",
                    Disabled = moduleType == 0,
                    OnClick = () => c.Act("pin", ("ref", moduleRef)),
                }),
                nameCell,
                new UiNode(UiKind.TableCell) { Key = "idle:" + moduleRef, TextAlign = "center", Text = TguiFormat.FormatSiUnit(module.Num("idle_power"), 0) },
                new UiNode(UiKind.TableCell) { Key = "active:" + moduleRef, TextAlign = "center", Text = TguiFormat.FormatSiUnit(module.Num("active_power"), 0) },
                new UiNode(UiKind.TableCell) { Key = "use:" + moduleRef, TextAlign = "center", Text = TguiFormat.FormatSiUnit(module.Num("use_energy"), 0) },
                new UiNode(UiKind.TableCell) { Key = "complexity:" + moduleRef, TextAlign = "center", Text = module.Str("module_complexity") }));
        }
        return section.With(table);
    }

    // tg: MODsuit.tsx:317
    private static UiNode ConfigureScreen(TguiContext c, IReadOnlyList<KeyValuePair<string, UiValue>> configuration, string moduleRef)
    {
        var list = new UiNode(UiKind.LabeledList) { Key = "config:" + moduleRef };
        foreach (KeyValuePair<string, UiValue> entry in configuration)
            list.With(ConfigureDataEntry(c, entry.Key, entry.Value, moduleRef));
        return new UiNode(UiKind.Box) { Key = "configure_screen:" + moduleRef }.With(list);
    }

    // tg: MODsuit.tsx:277
    private static UiNode ConfigureDataEntry(TguiContext c, string name, UiValue config, string moduleRef)
    {
        string type = config.Str("type");
        UiValue value = config.Get("value");
        string key = "config:" + moduleRef + ":" + name;
        UiNode control = type switch
        {
            // tg: MODsuit.tsx:174
            "number" => new UiNode(UiKind.NumberInput)
            {
                Key = key,
                Value = value.AsNum(),
                MinValue = -50,
                MaxValue = 50,
                Step = 1,
                StepPixelSize = 5,
                Width = "39px",
                OnChange = v => c.Act("configure", ("key", name), ("value", v), ("ref", moduleRef)),
            },
            // tg: MODsuit.tsx:196
            "bool" => new UiNode(UiKind.Button)
            {
                Key = key,
                Checkbox = value?.Truthy ?? false,
                OnClick = () => c.Act("configure", ("key", name), ("value", !(value?.Truthy ?? false)), ("ref", moduleRef)),
            },
            // tg: MODsuit.tsx:214  the button then the colour swatch
            "color" => new UiNode(UiKind.Stack) { Key = key }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = key + ":paint",
                    Icon = "paint-brush",
                    OnClick = () => c.Act("configure", ("key", name), ("ref", moduleRef)),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.ColorBox) { Key = key + ":swatch", Color = value.AsStr() })),
            // tg: MODsuit.tsx:233
            "list" => new UiNode(UiKind.Dropdown)
            {
                Key = key,
                TextValue = value.AsStr(),
                Options = config.Arr("values").Select(v => v.AsStr()).ToArray(),
                OnSelected = v => c.Act("configure", ("key", name), ("value", v), ("ref", moduleRef)),
            },
            // tg: MODsuit.tsx:268
            "button" => new UiNode(UiKind.Button)
            {
                Key = key,
                Icon = value.AsStr(),
                OnClick = () => c.Act("configure", ("key", name), ("ref", moduleRef)),
            },
            // tg: MODsuit.tsx:249
            "pin" => new UiNode(UiKind.Button)
            {
                Key = key,
                Icon = "thumbtack",
                Selected = value?.Truthy ?? false,
                Tooltip = "Pin",
                OnClick = () => c.Act("configure", ("key", name), ("value", !(value?.Truthy ?? false)), ("ref", moduleRef)),
            },
            _ => null,
        };
        return new UiNode(UiKind.LabeledItem) { Key = key + ":row", Label = config.Str("display_name") }.With(control);
    }
}

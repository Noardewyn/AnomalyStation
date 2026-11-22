using System;
using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Mecha/index.tsx, AlertPane.tsx, ModulesPane.tsx and data.ts
// (D321). useSharedState is the window's own state; the Dimmer over a missing scanning module is a NoticeBox; a module's h2 name is a
// bold box; italics are drawn at tg's opacity. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Mecha
{
    /// <summary>tg's "mecha_equipment" sheet at 32x32 (asset_cache/assets/mecha.dm), the module pictures by icon state.</summary>
    private const string Sheet = TguiSpritesheets.MechaEquipment32;

    private static readonly (string, double, double)[] HalfQuarter =
    {
        ("good", 0.5, double.PositiveInfinity),
        ("average", 0.25, 0.5),
        ("bad", double.NegativeInfinity, 0.25),
    };

    private static readonly (string, double, double)[] ThreeQuarters =
    {
        ("good", 0.75, double.PositiveInfinity),
        ("average", 0.25, 0.75),
        ("bad", double.NegativeInfinity, 0.25),
    };

    // tg: tgui/packages/tgui/interfaces/Mecha/index.tsx:21
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool showModuleList = c.UseState("showModuleList", true);
        const double mainWidth = 400, equipmentWidth = 400;
        c.WindowTheme = data.Str("ui_theme", null);
        // tg: index.tsx:30  <Window theme={data.ui_theme} width={windowWidth} height={560}>
        return c.Window(mainWidth + (showModuleList ? equipmentWidth : 0), 560, null, Content(c, data));
    }

    // tg: index.tsx:39
    private static UiNode Content(TguiContext c, UiValue data)
    {
        bool editAccess = c.UseState("edit_access", false);
        double mechaFlags = data.Num("mecha_flags");
        UiValue flagKeys = data.Field("mechflag_keys");
        bool idLock = Has(mechaFlags, flagKeys.Num("ID_LOCK_ON"));
        bool oneAccess = data.Truthy("one_access");

        var idLockItem = new UiNode(UiKind.LabeledItem) { Label = "ID Lock" }.With(
            new UiNode(UiKind.Button)
            {
                Key = "toggle_id_lock",
                Icon = idLock ? "lock" : "lock-open",
                Text = idLock ? "Enabled" : "Disabled",
                Selected = idLock,
                OnClick = () =>
                {
                    c.SetState("edit_access", false);
                    c.Act("toggle_id_lock");
                },
            });
        if (idLock)
        {
            idLockItem.With(
                new UiNode(UiKind.Button) { Key = "edit_access", Tooltip = "Edit Access", Icon = "id-card-o", Selected = editAccess, OnClick = () => c.SetState("edit_access", !editAccess) },
                new UiNode(UiKind.Button) { Key = "one_access", Tooltip = oneAccess ? "Require Any" : "Require All", Icon = oneAccess ? "check" : "check-double", OnClick = () => c.Act("one_access") });
        }

        var titleButtons = new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Button) { Key = "changename", Icon = "edit", Tooltip = "Rename", OnClick = () => c.Act("changename") });
        if (!data.Truthy("diagnostic_status"))
            titleButtons.With(new UiNode(UiKind.Button) { Key = "diagnostic", Icon = "tachograph-digital", Color = "violet", Tooltip = "Diagnostic", OnClick = () => c.Act("diagnostic") });

        string mechView = data.Str("mech_view");
        var main = new UiNode(UiKind.Stack) { Vertical = true, Fill = true, MinWidth = "270px" }.With(
            new UiNode(UiKind.StackItem) { Key = "main", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "mech", Fill = true, Title = data.Str("name"), Buttons = titleButtons }.With(
                    new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                        // tg: index.tsx:84  <ByondUi height="170px" params={{ id: mech_view, zoom: 5, type: 'map' }} />
                        new UiNode(UiKind.StackItem) { Key = "view" }.With(
                            new UiNode(UiKind.ByondUi) { Key = "map:" + mechView, MapRef = mechView, MapPicture = true, Width = "100%", Height = "170px" }),
                        new UiNode(UiKind.StackItem) { Key = "status" }.With(
                            new UiNode(UiKind.LabeledList).With(
                                IntegrityBar(data),
                                PowerBar(data),
                                LightsBar(c, data),
                                CabinSeal(c, data),
                                DnaLock(c, data),
                                idLockItem))))),
            new UiNode(UiKind.StackItem) { Key = "alerts" }.With(AlertPane(c, data)));

        // tg: index.tsx:139
        UiNode side = editAccess
            ? AccessConfig.Render(c,
                accesses: data.Arr("regions"),
                selectedList: data.Arr("accesses").Select(a => a.AsStr()).ToList(),
                accessMod: reference => c.Act("set", ("access", reference)),
                grantAll: () => c.Act("grant_all"),
                denyAll: () => c.Act("clear_all"),
                grantDep: reference => c.Act("grant_region", ("region", reference)),
                denyDep: reference => c.Act("deny_region", ("region", reference)))
            : ModulesPane(c, data);

        return new UiNode(UiKind.Stack) { Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "left", Grow = 1 }.With(main),
            new UiNode(UiKind.StackItem) { Key = "right", Grow = 2 }.With(side));
    }

    // tg: index.tsx:170
    private static UiNode PowerBar(UiValue data)
    {
        double level = data.Num("power_level"), max = data.Num("power_max");
        UiValue maxValue = data.Field("power_max");
        string text = maxValue == null || maxValue.IsNull ? "Power cell missing"
            : level == 1e31 ? "Infinite"
            : TguiFormat.FormatSiUnit(level, 0, "J") + " of " + TguiFormat.FormatSiUnit(max, 0, "J");
        return new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
            new UiNode(UiKind.ProgressBar) { Key = "power", Value = max != 0 ? level / max : 0, Ranges = HalfQuarter, Text = text });
    }

    // tg: index.tsx:201
    private static UiNode IntegrityBar(UiValue data)
    {
        double integrity = data.Num("integrity"), max = data.Num("integrity_max");
        bool scanned = data.Truthy("scanmod_rating");
        return new UiNode(UiKind.LabeledItem) { Label = "Integrity" }.With(
            new UiNode(UiKind.ProgressBar)
            {
                Key = "integrity",
                Value = scanned ? integrity / max : 0,
                Ranges = HalfQuarter,
                Text = !scanned ? "Unknown" : TguiFormat.JsNumber(integrity) + " of " + TguiFormat.JsNumber(max),
            });
    }

    // tg: index.tsx:224
    private static UiNode LightsBar(TguiContext c, UiValue data)
    {
        double flags = data.Num("mecha_flags");
        UiValue keys = data.Field("mechflag_keys");
        bool hasLights = Has(flags, keys.Num("HAS_LIGHTS")), lightsOn = Has(flags, keys.Num("LIGHTS_ON"));
        return new UiNode(UiKind.LabeledItem) { Label = "Lights" }.With(
            new UiNode(UiKind.Button)
            {
                Key = "toggle_lights",
                Icon = "lightbulb",
                Text = lightsOn ? "On" : "Off",
                Selected = lightsOn,
                Disabled = !hasLights || data.Num("power_max") == 0 || data.Num("power_level") == 0,
                OnClick = () => c.Act("toggle_lights"),
            });
    }

    // tg: index.tsx:243
    private static UiNode CabinSeal(TguiContext c, UiValue data)
    {
        bool sealedCabin = data.Truthy("cabin_sealed");
        double temp = data.Num("cabin_temp"), pressure = data.Num("cabin_pressure");
        bool tempWarning = temp < data.Num("cabin_temp_warning_min") || temp > data.Num("cabin_temp_warning_max");
        bool tempHazard = temp < data.Num("cabin_temp_hazard_min") || temp > data.Num("cabin_temp_hazard_max");
        bool pressureWarning = pressure < data.Num("cabin_pressure_warning_min") || pressure > data.Num("cabin_pressure_warning_max");
        bool pressureHazard = pressure < data.Num("cabin_pressure_hazard_min") || pressure > data.Num("cabin_pressure_hazard_max");
        return new UiNode(UiKind.LabeledItem)
        {
            Label = "Cabin Air",
            Buttons = !sealedCabin ? null : new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button)
                {
                    Key = "temp",
                    Color = tempHazard ? "danger" : tempWarning ? "average" : "transparent",
                    Icon = "temperature-low",
                    Tooltip = "Air temperature: " + TguiFormat.JsNumber(temp) + "°C",
                },
                new UiNode(UiKind.Button)
                {
                    Key = "pressure",
                    Color = pressureHazard ? "danger" : pressureWarning ? "average" : "transparent",
                    Icon = "gauge-high",
                    Tooltip = "Air pressure: " + TguiFormat.JsNumber(pressure) + " kPa",
                }),
        }.With(
            new UiNode(UiKind.Button)
            {
                Key = "toggle_cabin_seal",
                Icon = sealedCabin ? "mask-ventilator" : "wind",
                Text = sealedCabin ? "Sealed" : "Exposed",
                Disabled = !data.Truthy("enclosed"),
                Selected = sealedCabin,
                OnClick = () => c.Act("toggle_cabin_seal"),
            });
    }

    // tg: index.tsx:310
    private static UiNode DnaLock(TguiContext c, UiValue data)
    {
        string dna = data.Str("dna_lock", null);
        bool locked = !string.IsNullOrEmpty(dna);
        var item = new UiNode(UiKind.LabeledItem) { Label = "DNA Lock" }.With(
            new UiNode(UiKind.Button) { Key = "dna_lock", Icon = "syringe", Text = locked ? "Enabled" : "Unset", Tooltip = "Set new DNA key", Selected = locked, OnClick = () => c.Act("dna_lock") });
        if (locked)
        {
            item.With(
                new UiNode(UiKind.Button) { Key = "key", Icon = "key", Tooltip = "Key enzyme: " + dna },
                new UiNode(UiKind.Button) { Key = "reset_dna", Icon = "ban", Tooltip = "Reset DNA lock", OnClick = () => c.Act("reset_dna") });
        }
        return item;
    }

    // ---- AlertPane.tsx ----

    // tg: tgui/packages/tgui/interfaces/Mecha/AlertPane.tsx:13
    private static readonly Dictionary<string, (string Damaged, string Normal)> InternalDamageDesc = new()
    {
        ["MECHA_INT_FIRE"] = ("Internal fire detected", "No internal fires detected"),
        ["MECHA_INT_TEMP_CONTROL"] = ("Thermoregulator offline", "Thermoregulator active"),
        ["MECHA_CABIN_AIR_BREACH"] = ("Cabin breach detected", "Cabin sealing intact"),
        ["MECHA_INT_CONTROL_LOST"] = ("Motors damaged", "Motors active"),
        ["MECHA_INT_SHORT_CIRCUIT"] = ("Circuits shorted", "Circuits operational"),
    };

    // tg: AlertPane.tsx:29
    private static UiNode AlertPane(TguiContext c, UiValue data)
    {
        double damage = data.Num("internal_damage");
        bool canOverclock = data.Truthy("can_use_overclock"), overclocking = data.Truthy("overclock_mode");
        bool safetyAvailable = data.Truthy("overclock_safety_available"), safety = data.Truthy("overclock_safety");
        double heat = data.Num("overclock_temp_percentage");

        UiNode buttons = null;
        if (overclocking || canOverclock)
        {
            buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button)
                {
                    Key = "toggle_overclock",
                    Icon = "forward",
                    Color = overclocking ? (heat > 1 ? "bad" : heat > 0.5 ? "average" : "good") : null,
                    Text = (overclocking ? "Overclocking " : "Overclock ") + "(" + TguiFormat.JsNumber(Math.Round(Math.Max(heat * 100, 0), MidpointRounding.AwayFromZero)) + "%)",
                    OnClick = () =>
                    {
                        if (canOverclock)
                            c.Act("toggle_overclock");
                    },
                });
            if (safetyAvailable)
            {
                buttons.With(new UiNode(UiKind.Button)
                {
                    Key = "toggle_overclock_safety",
                    Icon = safety ? "temperature-arrow-down" : "temperature-arrow-up",
                    Color = safety ? "good" : "bad",
                    Tooltip = safety ? "OC safety prevents overheat." : "OC safety disabled.",
                    OnClick = () => c.Act("toggle_overclock_safety"),
                });
            }
        }

        var stack = new UiNode(UiKind.Stack) { Vertical = true };
        if (!data.Truthy("scanmod_rating"))
            stack.With(new UiNode(UiKind.NoticeBox) { Key = "no-scanner", Text = "Scanning module missing." });
        else
        {
            foreach (KeyValuePair<string, UiValue> key in data.Obj("internal_damage_keys"))
            {
                double flag = key.Value.AsNum();
                bool damaged = Has(damage, flag);
                (string damagedText, string normalText) = InternalDamageDesc.TryGetValue(key.Key, out var d) ? d : (key.Key, key.Key);
                var row = new UiNode(UiKind.Stack) { Justify = "space-between" }.With(
                    new UiNode(UiKind.StackItem) { Key = "desc" }.With(
                        new UiNode(UiKind.Box) { Key = "box", Color = damaged ? "red" : "green" }.With(
                            new UiNode(UiKind.Icon) { Key = "icon", Icon = damaged ? "warning" : "check" },
                            new UiNode(UiKind.Box) { Key = "text", Text = damaged ? damagedText : normalText })));
                if (damaged)
                {
                    row.With(new UiNode(UiKind.StackItem) { Key = "repair" }.With(
                        new UiNode(UiKind.Button) { Key = "repair", Color = "red", Text = "Repair", OnClick = () => c.Act("repair_int_damage", ("flag", flag)) }));
                }
                stack.With(new UiNode(UiKind.StackItem) { Key = key.Key }.With(row));
            }
        }
        return new UiNode(UiKind.Section) { Key = "status", Title = "Status", Buttons = buttons }.With(stack);
    }

    // ---- ModulesPane.tsx ----

    // tg: tgui/packages/tgui/interfaces/Mecha/ModulesPane.tsx:22
    private static string ModuleSlotIcon(string slot) => slot switch
    {
        "mecha_l_arm" or "mecha_r_arm" => "hand",
        "mecha_power" => "bolt",
        "mecha_armor" => "shield-halved",
        _ => "screwdriver-wrench",
    };

    // tg: ModulesPane.tsx:39
    private static string ModuleSlotLabel(string slot) => slot switch
    {
        "mecha_l_arm" => "Left arm module",
        "mecha_r_arm" => "Right arm module",
        "mecha_utility" => "Utility module",
        "mecha_power" => "Power module",
        "mecha_armor" => "Armor module",
        _ => "Common module",
    };

    // tg: ModulesPane.tsx:56
    private static UiNode ModulesPane(TguiContext c, UiValue data)
    {
        bool showModuleList = c.UseState("showModuleList", true);
        IReadOnlyList<UiValue> modules = data.Arr("modules");
        UiValue selectedValue = data.Field("selected_module_index");
        int selected = selectedValue == null || selectedValue.IsNull ? -1 : (int)selectedValue.AsNum();
        bool safety = data.Truthy("weapons_safety");

        var list = new UiNode(UiKind.StackItem) { Key = "list" };
        for (int i = 0; i < modules.Count; i++)
        {
            UiValue module = modules[i];
            int index = i;
            bool empty = string.IsNullOrEmpty(module.Str("ref"));
            UiNode picture = empty
                ? new UiNode(UiKind.StackItem) { Key = "icon", Width = "32px", Height = "32px", TextAlign = "center" }.With(
                    new UiNode(UiKind.Icon) { Key = "slot", Icon = ModuleSlotIcon(module.Str("slot")), FontSize = 1.5 })
                : new UiNode(UiKind.StackItem) { Key = "icon" }.With(
                    new UiNode(UiKind.Box) { Key = "picture", DmIcon = Sheet, DmIconState = module.Str("icon") });
            var row = new UiNode(UiKind.Stack).With(picture);
            if (showModuleList)
                row.With(new UiNode(UiKind.StackItem) { Key = "name", Text = TguiFormat.CapitalizeAll(empty ? ModuleSlotLabel(module.Str("slot")) + " Slot" : module.Str("name")) });
            list.With(new UiNode(UiKind.Button)
            {
                Key = "module" + i,
                Fluid = true,
                Color = empty ? "transparent" : null,
                Selected = !empty && i == selected,
                OnClick = empty ? null : () => c.Act("select_module", ("index", (double)index)),
            }.With(row));
        }

        var detail = new UiNode(UiKind.StackItem) { Key = "detail", Grow = 1 };
        if (selected >= 0 && selected < modules.Count && showModuleList)
            detail.With(ModuleDetails(c, data, modules[selected]));

        return new UiNode(UiKind.Section)
        {
            Key = "modules",
            Title = showModuleList ? "Equipment" : null,
            Fill = true,
            Scrollable = true,
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button)
                {
                    Key = "toggle_safety",
                    Icon = !safety ? "triangle-exclamation" : "helmet-safety",
                    Color = !safety ? "red" : "default",
                    Text = showModuleList ? (!safety ? "Safety Protocols Disabled" : "Safety Protocols Enabled") : null,
                    OnClick = () => c.Act("toggle_safety"),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "minimize",
                    Icon = showModuleList ? "arrow-left" : "arrow-right",
                    Text = showModuleList ? "Minimize" : "Max",
                    OnClick = () => c.SetState("showModuleList", !showModuleList),
                }),
        }.With(new UiNode(UiKind.Stack).With(list, detail));
    }

    // tg: ModulesPane.tsx:181
    private static UiNode ModuleDetails(TguiContext c, UiValue data, UiValue module)
    {
        string reference = module.Str("ref");
        UiValue snowflake = module.Field("snowflake");
        string snowflakeId = snowflake.Str("snowflake_id", null);

        var head = new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(
                new UiNode(UiKind.Box) { Key = "h2", Bold = true, FontSize = 1.5, Text = TguiFormat.CapitalizeAll(module.Str("name")) },
                new UiNode(UiKind.Box) { Key = "slot", Opacity = 0.5, Text = ModuleSlotLabel(module.Str("slot")) }));
        if (module.Truthy("detachable"))
        {
            head.With(new UiNode(UiKind.StackItem) { Key = "detach" }.With(
                new UiNode(UiKind.Button) { Key = "detach", Color = "transparent", Icon = "eject", Tooltip = "Detach", FontSize = 1.5, OnClick = () => Gear(c, reference, "detach") }));
        }

        UiNode body = snowflakeId switch
        {
            EjectorId => SnowflakeCargo(c, module, snowflake),
            AirTankId => SnowflakeAirTank(c, data, module, snowflake),
            OreboxId => SnowflakeOrebox(c, module, snowflake),
            _ => new UiNode(UiKind.LabeledList).With(ModuleDetailsBasic(c, data, module).Concat(snowflakeId != null ? ModuleDetailsExtra(c, data, module, snowflake, snowflakeId) : Array.Empty<UiNode>())),
        };

        return new UiNode(UiKind.Box) { Key = "details:" + reference }.With(
            new UiNode(UiKind.Section) { Key = "head" }.With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "title" }.With(head),
                    new UiNode(UiKind.StackItem) { Key = "desc", Text = module.Str("desc") })),
            new UiNode(UiKind.Section) { Key = "body" }.With(body));
    }

    // tg: ModulesPane.tsx:237
    private static IEnumerable<UiNode> ModuleDetailsBasic(TguiContext c, UiValue data, UiValue module)
    {
        string reference = module.Str("ref");
        double integrity = module.Num("integrity");
        if (integrity < 1)
            yield return IntegrityItem(c, reference, integrity);
        if (!data.Truthy("weapons_safety") && module.Str("slot") is "mecha_l_arm" or "mecha_r_arm")
            yield return new UiNode(UiKind.LabeledItem) { Key = "safety", Label = "Safety", Color = "red" }.With(new UiNode(UiKind.NoticeBox) { Key = "off", Color = "danger", Text = "SAFETY OFF" });
        double energy = module.Num("energy_per_use");
        if (energy != 0)
        {
            double power = data.Num("power_level");
            yield return new UiNode(UiKind.LabeledItem)
            {
                Key = "cost",
                Label = "Power Cost",
                Text = TguiFormat.FormatPower(energy) + ", " + (power != 0 ? TguiFormat.ToFixed(power / energy) : "0") + " uses left",
            };
        }
        if (module.Truthy("equip_cooldown"))
            yield return new UiNode(UiKind.LabeledItem) { Key = "cooldown", Label = "Cooldown", Text = module.Str("equip_cooldown") };
        bool active = module.Truthy("active");
        if (module.Truthy("can_be_toggled"))
        {
            yield return new UiNode(UiKind.LabeledItem) { Key = "toggle", Label = module.Str("active_label") }.With(
                new UiNode(UiKind.Button) { Key = "toggle", Icon = "power-off", Text = active ? "Enabled" : "Disabled", Selected = active, OnClick = () => Gear(c, reference, "toggle") });
        }
        if (module.Truthy("can_be_triggered"))
        {
            yield return new UiNode(UiKind.LabeledItem) { Key = "trigger", Label = module.Str("active_label") }.With(
                new UiNode(UiKind.Button) { Key = "trigger", Icon = "power-off", Text = "Activate", Disabled = active, OnClick = () => Gear(c, reference, "toggle") });
        }
    }

    private static UiNode IntegrityItem(TguiContext c, string reference, double integrity)
        => new UiNode(UiKind.LabeledItem)
        {
            Key = "integrity",
            Label = "Integrity",
            Buttons = new UiNode(UiKind.Button) { Key = "repair", Text = "Repair", Icon = "wrench", OnClick = () => Gear(c, reference, "repair") },
        }.With(new UiNode(UiKind.ProgressBar) { Key = "integrity", Ranges = ThreeQuarters, Value = integrity });

    // tg: ModulesPane.tsx:328
    private const string SleeperId = "sleeper_snowflake";
    private const string SyringeId = "syringe_snowflake";
    private const string ModeId = "mode_snowflake";
    private const string ExtinguisherId = "extinguisher_snowflake";
    private const string EjectorId = "ejector_snowflake";
    private const string OreboxId = "orebox_manager_snowflake";
    private const string RadioId = "radio_snowflake";
    private const string AirTankId = "air_tank_snowflake";
    private const string BallisticId = "ballistic_weapon_snowflake";
    private const string GeneratorId = "generator_snowflake";
    private const string OreScannerId = "orescanner_snowflake";
    private const string ClawId = "lawclaw_snowflake";
    private const string RcdId = "rcd_snowflake";

    // tg: ModulesPane.tsx:342
    private static IEnumerable<UiNode> ModuleDetailsExtra(TguiContext c, UiValue data, UiValue module, UiValue s, string id)
    {
        string reference = module.Str("ref");
        switch (id)
        {
            case BallisticId:
                return SnowflakeWeaponBallistic(c, reference, s);
            case ExtinguisherId:
                return SnowflakeExtinguisher(c, reference, s);
            case SleeperId:
                return SnowflakeSleeper(c, reference, s);
            case SyringeId:
                return SnowflakeSyringe(c, reference, s);
            case ModeId:
                return new[] { SnowflakeMode(c, reference, s) };
            case RadioId:
                return SnowflakeRadio(c, reference, s);
            case GeneratorId:
                UiValue fuel = s.Field("fuel");
                return new[]
                {
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "fuel",
                        Label = "Fuel Amount",
                        Text = fuel == null || fuel.IsNull ? "None" : TguiFormat.ToFixed(fuel.AsNum() * data.Num("sheet_material_amount"), 0) + " cm³",
                    },
                };
            case OreScannerId:
                return new[] { SnowflakeOreScanner(c, reference, s) };
            case ClawId:
                bool cuff = s.Truthy("autocuff");
                return new[]
                {
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "cuff",
                        Label = "Handcuff Suspects",
                        Buttons = new UiNode(UiKind.Button) { Key = "togglecuff", Text = "Toggle", Color = cuff ? "green" : "blue", Icon = "handcuffs", OnClick = () => Gear(c, reference, "togglecuff") },
                    },
                };
            case RcdId:
                return SnowflakeRcd(c, reference, s);
            default:
                return Array.Empty<UiNode>();
        }
    }

    // tg: ModulesPane.tsx:370
    private static IEnumerable<UiNode> SnowflakeWeaponBallistic(TguiContext c, string reference, UiValue s)
    {
        double projectiles = s.Num("projectiles"), magazine = s.Num("max_magazine");
        double cache = s.Num("projectiles_cache"), cacheMax = s.Num("projectiles_cache_max");
        if (s.Truthy("ammo_type"))
            yield return new UiNode(UiKind.LabeledItem) { Key = "ammo", Label = "Ammo", Text = s.Str("ammo_type") };
        yield return new UiNode(UiKind.LabeledItem)
        {
            Key = "loaded",
            Label = "Loaded",
            Buttons = !s.Truthy("disabledreload") && cache > 0
                ? new UiNode(UiKind.Button) { Key = "reload", Icon = "redo", Text = "Reload", Disabled = projectiles >= magazine, OnClick = () => Gear(c, reference, "reload") }
                : null,
        }.With(new UiNode(UiKind.ProgressBar) { Key = "loaded", Value = projectiles / magazine, Text = TguiFormat.JsNumber(projectiles) + " of " + TguiFormat.JsNumber(magazine) });
        if (cacheMax != 0)
        {
            yield return new UiNode(UiKind.LabeledItem) { Key = "stored", Label = "Stored" }.With(
                new UiNode(UiKind.ProgressBar) { Key = "stored", Value = cache / cacheMax, Text = TguiFormat.JsNumber(cache) + " of " + TguiFormat.JsNumber(cacheMax) });
        }
        if (s.Truthy("mode"))
            yield return SnowflakeMode(c, reference, s);
    }

    // tg: ModulesPane.tsx:423
    private static IEnumerable<UiNode> SnowflakeSleeper(TguiContext c, string reference, UiValue s)
    {
        UiValue patient = s.Field("patient");
        if (patient == null || !patient.Truthy)
        {
            yield return new UiNode(UiKind.LabeledItem) { Key = "patient", Label = "Patient", Text = "None" };
            yield break;
        }
        yield return new UiNode(UiKind.LabeledItem)
        {
            Key = "patient",
            Label = "Patient",
            Text = patient.Str("patient_name"),
            Buttons = new UiNode(UiKind.Button) { Key = "eject", Icon = "eject", Tooltip = "Eject", OnClick = () => Gear(c, reference, "eject") },
        };
        yield return new UiNode(UiKind.LabeledItem) { Key = "health", Label = "Health" }.With(
            new UiNode(UiKind.ProgressBar) { Key = "health", Ranges = ThreeQuarters, Value = patient.Num("patient_health") });
        yield return Stripe("state", "State", patient.Str("patient_state"));
        yield return Stripe("temp", "Temperature", patient.Str("core_temp") + " C");
        yield return Stripe("brute", "Brute Damage", patient.Str("brute_loss"));
        yield return Stripe("burn", "Burn Severity", patient.Str("burn_loss"));
        yield return Stripe("tox", "Toxin Content", patient.Str("toxin_loss"));
        yield return Stripe("oxy", "Respiratory Damage", patient.Str("oxygen_loss"));
        if (s.Truthy("has_brain_damage"))
            yield return Stripe("brain", "Detected", "Brain Damage");
        if (s.Truthy("has_traumas"))
            yield return Stripe("traumas", "Detected", "Traumatic Damage");
        yield return new UiNode(UiKind.LabeledItem) { Key = "reagents", Label = "Reagent Details" }.With(
            s.Arr("contained_reagents").Select(r => new UiNode(UiKind.LabeledItem) { Key = "r:" + r.Str("name"), Candystripe = true, Label = r.Str("name") }.With(
                new UiNode(UiKind.LabeledItem) { Key = "volume", Label = r.Str("volume") + "u" })));
        UiValue injectible = s.Field("injectible_reagents");
        yield return new UiNode(UiKind.LabeledItem) { Key = "inject", Label = "Reagent Injection", Text = injectible == null || !injectible.Truthy ? "Unavailable" : null }.With(
            s.Arr("injectible_reagents").Select(r => new UiNode(UiKind.LabeledItem) { Key = "i:" + r.Str("name"), Candystripe = true, Label = r.Str("name") }.With(
                new UiNode(UiKind.LabeledItem) { Key = "volume", Label = r.Str("volume") + "u" }.With(
                    new UiNode(UiKind.Button) { Key = "inject", Text = "Inject", OnClick = () => Gear(c, reference, "inject_reagent_" + r.Str("name")) }))));
    }

    private static UiNode Stripe(string key, string label, string text) => new(UiKind.LabeledItem) { Key = key, Candystripe = true, Label = label, Text = text };

    // tg: ModulesPane.tsx:542
    private static IEnumerable<UiNode> SnowflakeSyringe(TguiContext c, string reference, UiValue s)
    {
        double syringes = s.Num("syringe"), maxSyringes = s.Num("max_syringe"), reagents = s.Num("reagents"), total = s.Num("total_reagents");
        yield return new UiNode(UiKind.LabeledItem) { Key = "syringes", Label = "Syringes" }.With(
            new UiNode(UiKind.ProgressBar) { Key = "syringes", Value = syringes / maxSyringes, Text = TguiFormat.JsNumber(syringes) + " of " + TguiFormat.JsNumber(maxSyringes) });
        yield return new UiNode(UiKind.LabeledItem) { Key = "reagents", Label = "Reagents" }.With(
            new UiNode(UiKind.ProgressBar) { Key = "reagents", Value = reagents / total, Text = TguiFormat.JsNumber(reagents) + " of " + TguiFormat.JsNumber(total) + " units" });
        yield return new UiNode(UiKind.LabeledItem) { Key = "mode", Label = "Mode" }.With(
            new UiNode(UiKind.Button) { Key = "change_mode", Text = s.Str("mode"), OnClick = () => Gear(c, reference, "change_mode") });
        yield return new UiNode(UiKind.LabeledItem) { Key = "synth", Label = "Synthesizing" }.With(
            s.Arr("analyzed_reagents").Select(r => new UiNode(UiKind.LabeledItem) { Key = "a:" + r.Str("name"), Label = r.Str("name") }.With(
                new UiNode(UiKind.Button) { Key = "toggle", Checkbox = r.Truthy("enabled"), OnClick = () => Gear(c, reference, "toggle_reagent_" + r.Str("name")) })));
        yield return new UiNode(UiKind.LabeledItem) { Key = "purge_all" }.With(
            new UiNode(UiKind.Button) { Key = "purge_all", Text = "Purge All", OnClick = () => Gear(c, reference, "purge_all") });
        foreach (UiValue r in s.Arr("contained_reagents"))
        {
            string name = r.Str("name");
            yield return new UiNode(UiKind.LabeledItem) { Key = "c:" + name, Label = name }.With(
                new UiNode(UiKind.LabeledItem) { Key = "volume", Label = r.Str("volume") + "u" }.With(
                    new UiNode(UiKind.Button) { Key = "purge", Text = "Purge", OnClick = () => Gear(c, reference, "purge_reagent_" + name) }));
        }
    }

    // tg: ModulesPane.tsx:625
    private static UiNode SnowflakeMode(TguiContext c, string reference, UiValue s)
        => new UiNode(UiKind.LabeledItem) { Key = "mode", Label = s.Str("mode_label") }.With(
            new UiNode(UiKind.Button) { Key = "change_mode", Text = s.Str("mode"), OnClick = () => Gear(c, reference, "change_mode") });

    // tg: ModulesPane.tsx:644
    private static IEnumerable<UiNode> SnowflakeRadio(TguiContext c, string reference, UiValue s)
    {
        bool microphone = s.Truthy("microphone"), speaker = s.Truthy("speaker");
        yield return new UiNode(UiKind.LabeledItem) { Key = "mic", Label = "Microphone" }.With(
            new UiNode(UiKind.Button) { Key = "mic", Selected = microphone, Icon = microphone ? "microphone" : "microphone-slash", Text = (microphone ? "En" : "Dis") + "abled", OnClick = () => Gear(c, reference, "toggle_microphone") });
        yield return new UiNode(UiKind.LabeledItem) { Key = "speaker", Label = "Speaker" }.With(
            new UiNode(UiKind.Button) { Key = "speaker", Selected = speaker, Icon = speaker ? "volume-up" : "volume-mute", Text = (speaker ? "En" : "Dis") + "abled", OnClick = () => Gear(c, reference, "toggle_speaker") });
        yield return new UiNode(UiKind.LabeledItem) { Key = "freq", Label = "Frequency" }.With(
            new UiNode(UiKind.NumberInput)
            {
                Key = "freq",
                Unit = "kHz",
                Step = 0.2,
                StepPixelSize = 10,
                AllowFloats = true,
                MinValue = s.Num("minFrequency") / 10,
                MaxValue = s.Num("maxFrequency") / 10,
                Value = s.Num("frequency") / 10,
                Format = v => TguiFormat.ToFixed(v, 1),
                OnChange = v => c.Act("equip_act", ("ref", reference), ("gear_action", "set_frequency"), ("new_frequency", v * 10)),
            });
    }

    // tg: ModulesPane.tsx:703
    private static UiNode SnowflakeAirTank(TguiContext c, UiValue data, UiValue module, UiValue s)
    {
        string reference = module.Str("ref");
        bool sealedCabin = data.Truthy("cabin_sealed"), active = module.Truthy("active");
        double atmosphere = Math.Round(data.Num("one_atmosphere"), MidpointRounding.AwayFromZero);
        double integrity = module.Num("integrity");
        double release = s.Num("tank_release_pressure"), pump = s.Num("tank_pump_pressure");
        bool auto = s.Truthy("auto_pressurize_on_seal"), port = s.Truthy("port_connected");
        bool pumpActive = s.Truthy("tank_pump_active"), pumpIn = s.Truthy("tank_pump_direction");

        var integrityList = new UiNode(UiKind.LabeledList) { Key = "integrity" };
        if (integrity < 1)
            integrityList.With(IntegrityItem(c, reference, integrity));

        var sensors = new UiNode(UiKind.Section) { Key = "sensors", Title = "Sensors" }.With(
            new UiNode(UiKind.Collapsible) { Key = "tank_air", Title = "Tank Air" }.With(GasmixParser.Render(s.Field("tank_air"), null, null)),
            sealedCabin
                ? new UiNode(UiKind.Collapsible) { Key = "cabin_air", Title = "Cabin Air" }.With(GasmixParser.Render(s.Field("cabin_air"), null, null))
                : new UiNode(UiKind.NoticeBox) { Key = "open" }.With(
                    new UiNode(UiKind.Icon) { Key = "wind", Icon = "wind" },
                    new UiNode(UiKind.Box) { Key = "text", Text = "Cabin Open" }));

        return new UiNode(UiKind.Box) { Key = "airtank" }.With(
            integrityList,
            new UiNode(UiKind.Section)
            {
                Key = "tank",
                Title = "Tank",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "toggle",
                    Icon = "power-off",
                    Text = active ? (!sealedCabin ? "Release Paused" : "Pressurizing Cabin") : "Release Off",
                    Selected = active,
                    OnClick = () => Gear(c, reference, "toggle"),
                },
            }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "auto", Label = "Automation" }.With(
                    new UiNode(UiKind.Button) { Key = "auto", Text = auto ? "Pressurize on Seal" : "Manual", Selected = auto, OnClick = () => Gear(c, reference, "toggle_auto_pressurize") }),
                new UiNode(UiKind.LabeledItem) { Key = "cabin", Label = "Cabin Pressure" }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "release",
                        Value = release,
                        Unit = "kPa",
                        Width = "75px",
                        MinValue = s.Num("tank_release_pressure_min"),
                        MaxValue = s.Num("tank_release_pressure_max"),
                        Step = 10,
                        OnChange = v => c.Act("equip_act", ("ref", reference), ("gear_action", "set_cabin_pressure"), ("new_pressure", v)),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "release_reset",
                        Icon = "sync",
                        Disabled = release == atmosphere,
                        OnClick = () => c.Act("equip_act", ("ref", reference), ("gear_action", "set_cabin_pressure"), ("new_pressure", atmosphere)),
                    }),
                new UiNode(UiKind.LabeledItem)
                {
                    Key = "port",
                    Label = "Pipenet Port",
                    Buttons = new UiNode(UiKind.Button) { Key = "info", Icon = "info", Color = "transparent", Tooltip = "Park above atmospherics connector port to connect inernal air tank with a gas network." },
                }.With(
                    new UiNode(UiKind.Button) { Key = "toggle_port", Selected = port, Text = port ? "Connected" : "Disconnected", OnClick = () => Gear(c, reference, "toggle_port") }))),
            new UiNode(UiKind.Section)
            {
                Key = "pump",
                Title = "External Pump",
                Buttons = new UiNode(UiKind.Button) { Key = "toggle_pump", Icon = "power-off", Text = pumpActive ? "On" : "Off", Selected = pumpActive, OnClick = () => Gear(c, reference, "toggle_tank_pump") },
            }.With(
                new UiNode(UiKind.LabeledItem) { Key = "direction", Label = "Direction" }.With(
                    new UiNode(UiKind.Button) { Key = "direction", Text = pumpIn ? "Area → Tank" : "Tank → Area", OnClick = () => Gear(c, reference, "toggle_tank_pump_direction") }),
                new UiNode(UiKind.LabeledItem) { Key = "target", Label = "Target Pressure" }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "pump_pressure",
                        Value = pump,
                        Unit = "kPa",
                        Width = "75px",
                        MinValue = s.Num("tank_pump_pressure_min"),
                        MaxValue = s.Num("tank_pump_pressure_max"),
                        Step = 10,
                        Format = v => TguiFormat.JsNumber(Math.Round(v, MidpointRounding.AwayFromZero)),
                        OnChange = v => c.Act("equip_act", ("ref", reference), ("gear_action", "set_tank_pump_pressure"), ("new_pressure", v)),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "pump_reset",
                        Icon = "sync",
                        Disabled = pump == atmosphere,
                        OnClick = () => c.Act("equip_act", ("ref", reference), ("gear_action", "set_tank_pump_pressure"), ("new_pressure", atmosphere)),
                    })),
            sensors);
    }

    // tg: ModulesPane.tsx:916
    private static UiNode SnowflakeOrebox(TguiContext c, UiValue module, UiValue s)
    {
        string reference = module.Str("ref");
        IReadOnlyList<KeyValuePair<string, UiValue>> contents = s.Obj("contents");
        var section = new UiNode(UiKind.Section)
        {
            Key = "contents",
            Title = "Contents",
            Buttons = new UiNode(UiKind.Button) { Key = "dump", Icon = "arrows-down-to-line", Text = "Dump", Disabled = contents.Count == 0, OnClick = () => Gear(c, reference, "dump") },
        };
        if (contents.Count == 0)
            return section.With(new UiNode(UiKind.NoticeBox) { Key = "empty", Color = "info", Text = "Ore box is empty" });
        for (int i = 0; i < contents.Count; i++)
        {
            UiValue item = contents[i].Value;
            section.With(new UiNode(UiKind.Stack) { Key = "ore" + i }.With(
                new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Box) { Key = "picture", DmIcon = Sheet, DmIconState = item.Str("icon") }),
                new UiNode(UiKind.StackItem) { Key = "text", Text = TguiFormat.CapitalizeAll(item.Str("amount") + "x " + item.Str("name")) }));
        }
        return section;
    }

    // tg: ModulesPane.tsx:968
    private static UiNode SnowflakeCargo(TguiContext c, UiValue module, UiValue s)
    {
        string reference = module.Str("ref");
        IReadOnlyList<UiValue> cargo = s.Arr("cargo");
        var section = new UiNode(UiKind.Section)
        {
            Key = "contents",
            Title = "Contents",
            Buttons = new UiNode(UiKind.Box) { Key = "count", Text = cargo.Count + " of " + s.Str("cargo_capacity") },
        };
        if (cargo.Count == 0)
            section.With(new UiNode(UiKind.NoticeBox) { Key = "empty", Color = "info", Text = "Compartment is empty" });
        for (int i = 0; i < cargo.Count; i++)
        {
            UiValue item = cargo[i];
            string cargoRef = item.Str("ref");
            section.With(new UiNode(UiKind.Button)
            {
                Key = "cargo" + i,
                Fluid = true,
                Icon = "eject",
                Text = TguiFormat.CapitalizeAll(item.Str("name")),
                OnClick = () => c.Act("equip_act", ("ref", reference), ("cargoref", cargoRef), ("gear_action", "eject")),
            });
        }
        return new UiNode(UiKind.Box) { Key = "cargo" }.With(section);
    }

    // tg: ModulesPane.tsx:1007
    private static IEnumerable<UiNode> SnowflakeExtinguisher(TguiContext c, string reference, UiValue s)
    {
        double reagents = s.Num("reagents");
        yield return new UiNode(UiKind.LabeledItem)
        {
            Key = "water",
            Label = "Water",
            Buttons = new UiNode(UiKind.Button) { Key = "refill", Text = "Refill", Icon = "fill", OnClick = () => Gear(c, reference, "refill") },
        }.With(new UiNode(UiKind.ProgressBar) { Key = "water", Value = reagents, MinValue = 0, MaxValue = s.Num("total_reagents"), Text = TguiFormat.JsNumber(reagents) });
        yield return new UiNode(UiKind.LabeledItem) { Key = "extinguisher", Label = "Extinguisher" }.With(
            new UiNode(UiKind.Button) { Key = "activate", Text = "Activate", Color = "red", Disabled = reagents < s.Num("reagents_required"), Icon = "fire-extinguisher", OnClick = () => Gear(c, reference, "activate") });
    }

    // tg: ModulesPane.tsx:1065
    private static UiNode SnowflakeOreScanner(TguiContext c, string reference, UiValue s)
    {
        double cooldown = s.Num("cooldown");
        return new UiNode(UiKind.LabeledItem) { Key = "scanner", Label = "Vent Scanner" }.With(
            new UiNode(UiKind.NoticeBox) { Key = "notice", Color = cooldown <= 0 ? "info" : null }.With(
                new UiNode(UiKind.Box) { Key = "text", Text = cooldown / 10 > 0 ? "Recharging..." : "Ready to scan vents" },
                new UiNode(UiKind.Button)
                {
                    Key = "area_scan",
                    Width = "100%",
                    Icon = "satellite-dish",
                    Color = cooldown <= 0 ? "green" : "transparent",
                    Text = "Scan all nearby vents",
                    Disabled = !(cooldown <= 0),
                    OnClick = () => Gear(c, reference, "area_scan"),
                }));
    }

    // tg: ModulesPane.tsx:1117
    private static IEnumerable<UiNode> SnowflakeRcd(TguiContext c, string reference, UiValue s)
    {
        bool deconstructing = s.Truthy("deconstructing");
        yield return new UiNode(UiKind.LabeledItem) { Key = "scan", Label = "Destruction Scan" }.With(
            new UiNode(UiKind.Button) { Key = "rcd_scan", Icon = "satellite-dish", Color = s.Truthy("scan_ready") ? "green" : "transparent", OnClick = () => Gear(c, reference, "rcd_scan") });
        yield return new UiNode(UiKind.LabeledItem) { Key = "deconstruct", Label = "Deconstructing" }.With(
            new UiNode(UiKind.Button) { Key = "toggle_deconstruct", Icon = "power-off", Text = deconstructing ? "On" : "Off", Color = deconstructing ? "green" : "blue", OnClick = () => Gear(c, reference, "toggle_deconstruct") });
        yield return new UiNode(UiKind.LabeledItem) { Key = "mode", Label = "Construction Mode" }.With(
            new UiNode(UiKind.Button) { Key = "change_mode", Text = s.Str("mode"), OnClick = () => Gear(c, reference, "change_mode") });
    }

    private static void Gear(TguiContext c, string reference, string action) => c.Act("equip_act", ("ref", reference), ("gear_action", action));

    private static bool Has(double flags, double bit) => ((long)flags & (long)bit) != 0;
}

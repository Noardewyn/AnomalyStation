using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AirAlarm/ (index.tsx, AlarmStatus.tsx,
// AlarmControl.tsx, AlarmModal.tsx, screens/Home.tsx, Vents.tsx, Scrubbers.tsx, Modes.tsx, Thresholds.tsx), common/AtmosControls.tsx
// (Vent, Scrubber) and common/InterfaceLockNoticeBox.jsx (the air alarm window, 2026-10-16; tg's /obj/machinery/airalarm ui_data).
// The screen (AlarmControl's useState) and the threshold editor (index.tsx's ModalContext) are the window's local state.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AirAlarm
{
    private const string ScreenKey = "screen";
    private const string ModalKey = "modal";

    /// <summary>tg's ActiveModal (types.ts:59): the threshold being edited.</summary>
    public sealed record ActiveModal(string Id, string Name, double Type, string TypeVar, string TypeName, string Unit);

    // tg: tgui/packages/tgui/interfaces/AirAlarm/index.tsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> tlvSettings = data.Arr("tlvSettings");

        bool locked = data.Truthy("locked") && !data.Truthy("siliconUser");

        ActiveModal activeModal = c.UseState<ActiveModal>(ModalKey, null);

        // tg: index.tsx:23  <Window width={475} height={650}>
        return c.Window(475, 650, null,
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "lock_notice" }.With(InterfaceLockNoticeBox(c)),
                new UiNode(UiKind.StackItem) { Key = "status" }.With(AirAlarmStatus(c)),
                new UiNode(UiKind.StackItem) { Key = "control", Grow = 1 }.With(!locked ? AirAlarmControl(c) : null)),
            activeModal != null
                ? AlarmEditingModal(c, activeModal,
                    tlvSettings.FirstOrDefault(tlv => tlv.Str("id") == activeModal.Id) is UiValue tlv ? tlv.Num(activeModal.TypeVar) : double.NaN)
                : null);
    }

    // tg: tgui/packages/tgui/interfaces/common/InterfaceLockNoticeBox.jsx:19
    public static UiNode InterfaceLockNoticeBox(TguiContext c, string accessText = "an ID card", bool? siliconUser = null, bool? preventLocking = null, string lockAction = "lock")
    {
        UiValue data = c.Data;
        siliconUser ??= data.Truthy("siliconUser");
        bool locked = data.Truthy("locked");
        preventLocking ??= data.Truthy("preventLocking");
        // For silicon users
        if (siliconUser == true)
            return new UiNode(UiKind.NoticeBox) { Color = "grey" }.With(
                new UiNode(UiKind.Stack) { Align = "center" }.With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "Interface lock status:" }),
                    new UiNode(UiKind.StackItem) { Grow = 1 },
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "lock",
                            Color = locked ? "red" : "green",
                            Icon = locked ? "lock" : "unlock",
                            Text = locked ? "Locked" : "Unlocked",
                            Disabled = preventLocking == true,
                            OnClick = () => c.Act(lockAction), // tg: onLockedStatusChange, act('lock') by default
                        })));
        // For everyone else
        return new UiNode(UiKind.NoticeBox) { Text = "Swipe " + accessText + " to " + (locked ? "unlock" : "lock") + " this interface." };
    }

    // tg: tgui/packages/tgui/interfaces/AirAlarm/AlarmStatus.tsx:6
    private static (string Color, string Text) Danger(double level) => level switch
    {
        1 => ("average", "Caution"),
        2 => ("bad", "Danger (Internals Required)"),
        _ => ("good", "Optimal"),
    };

    // tg: AlarmStatus.tsx:21
    private static (string Color, string Text) Fault(double status) => status switch
    {
        1 => ("purple", "Manual Trigger"),
        2 => ("average", "Automatic Detection"),
        _ => ("good", "None"),
    };

    private static UiNode Item(string label, string color, string text) =>
        new UiNode(UiKind.LabeledItem) { Key = label, Label = label }.With(new UiNode(UiKind.Box) { Color = color, Text = text });

    // tg: tgui/packages/tgui/interfaces/AirAlarm/AlarmStatus.tsx:36
    public static UiNode AirAlarmStatus(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> envData = data.Arr("envData");

        (string Color, string Text) localStatus = Danger(data.Num("dangerLevel"));
        (string Color, string Text) areaFault = Fault(data.Num("faultStatus"));
        string faultLocation = data.Str("faultLocation");
        bool atmosAlarm = data.Truthy("atmosAlarm"), fireAlarm = data.Truthy("fireAlarm");

        var list = new UiNode(UiKind.LabeledList);
        if (envData.Count <= 0)
            list.With(Item("Warning", "bad", "Cannot obtain air sample for analysis."));
        else
        {
            foreach (UiValue entry in envData)
            {
                (string color, _) = Danger(entry.Num("danger"));
                list.With(Item(entry.Str("name"), color, entry.Str("value")));
            }
            list.With(
                Item("Local Status", localStatus.Color, localStatus.Text),
                Item("Area Status", atmosAlarm || fireAlarm ? "bad" : "good",
                    atmosAlarm ? "Atmosphere Alarm" : fireAlarm ? "Fire Alarm" : "Nominal"),
                Item("Fault Status", areaFault.Color, areaFault.Text),
                Item("Fault Location", !string.IsNullOrEmpty(faultLocation) ? "blue" : "good", string.IsNullOrEmpty(faultLocation) ? "None" : faultLocation));
        }
        if (data.Truthy("emagged"))
            list.With(Item("Warning", "bad", "Safety measures offline. Device may exhibit abnormal behavior."));
        return new UiNode(UiKind.Section) { Title = "Air Status" }.With(list);
    }

    // tg: tgui/packages/tgui/interfaces/AirAlarm/AlarmControl.tsx:11  AIR_ALARM_ROUTES
    private static readonly (string Screen, string Title, Func<TguiContext, UiNode> Component)[] Routes =
    {
        ("home", "Air Controls", AirAlarmControlHome),
        ("vents", "Vent Controls", AirAlarmControlVents),
        ("scrubbers", "Scrubber Controls", AirAlarmControlScrubbers),
        ("modes", "Operating Mode", AirAlarmControlModes),
        ("thresholds", "Alarm Thresholds", AirAlarmControlThresholds),
    };

    // tg: AlarmControl.tsx:34
    public static UiNode AirAlarmControl(TguiContext c)
    {
        string screen = c.UseState(ScreenKey, "home");

        (string Screen, string Title, Func<TguiContext, UiNode> Component) route = Routes.FirstOrDefault(r => r.Screen == screen);
        if (route.Component == null)
            route = Routes[0];
        bool isHome = route.Title == "Air Controls";

        return new UiNode(UiKind.Section)
        {
            Fill = true,
            Scrollable = true,
            Title = route.Title,
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "back",
                Icon = "arrow-left",
                OnClick = () => c.SetState(ScreenKey, "home"),
                Disabled = isHome,
                Text = "Back",
            },
        }.With(route.Component(c));
    }

    // tg: tgui/packages/tgui/interfaces/AirAlarm/screens/Home.tsx:12
    public static UiNode AirAlarmControlHome(TguiContext c)
    {
        UiValue data = c.Data;
        bool allowLinkChange = data.Truthy("allowLinkChange");
        bool atmosAlarm = data.Truthy("atmosAlarm");
        string filteringPath = data.Str("filteringPath");
        string panicSiphonPath = data.Str("panicSiphonPath");
        string selectedModePath = data.Str("selectedModePath");
        bool sensor = data.Truthy("sensor");

        bool isPanicSiphoning = selectedModePath == panicSiphonPath;

        UiNode Screen(string key, string icon, string text) => new UiNode(UiKind.StackItem) { Key = key }.With(
            new UiNode(UiKind.Button) { Icon = icon, OnClick = () => c.SetState(ScreenKey, key), Text = text });

        return new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "alarm" }.With(
                new UiNode(UiKind.Button)
                {
                    Icon = atmosAlarm ? "exclamation-triangle" : "exclamation",
                    Color = atmosAlarm ? "caution" : null,
                    OnClick = () => c.Act(atmosAlarm ? "reset" : "alarm"),
                    Text = "Area Atmosphere Alarm",
                }),
            new UiNode(UiKind.StackItem) { Key = "panic" }.With(
                new UiNode(UiKind.Button)
                {
                    Icon = isPanicSiphoning ? "exclamation-triangle" : "exclamation",
                    Color = isPanicSiphoning ? "danger" : null,
                    OnClick = () => c.Act("mode", ("mode", isPanicSiphoning ? filteringPath : panicSiphonPath)),
                    Text = "Panic Siphon",
                }),
            Screen("vents", "sign-out-alt", "Vent Controls"),
            Screen("scrubbers", "filter", "Scrubber Controls"),
            Screen("modes", "cog", "Operating Mode"),
            Screen("thresholds", "chart-bar", "Alarm Thresholds"),
            sensor && allowLinkChange
                ? new UiNode(UiKind.StackItem) { Key = "disconnect" }.With(
                    c.ButtonConfirm("disconnect_sensor", new UiNode(UiKind.Button)
                    {
                        Icon = "link-slash",
                        Color = "danger",
                        OnClick = () => c.Act("disconnect_sensor"),
                        Text = "Disconnect Sensor",
                    }))
                : null);
    }

    private static UiNode NothingToShow() => new UiNode(UiKind.NoticeBox) { Color = "info", TextAlign = "center", Text = "Nothing to show" };

    // tg: tgui/packages/tgui/interfaces/AirAlarm/screens/Vents.tsx:8
    public static UiNode AirAlarmControlVents(TguiContext c)
    {
        IReadOnlyList<UiValue> vents = c.Data.Arr("vents");

        if (vents.Count == 0)
            return NothingToShow();

        return new UiNode(UiKind.Fragment).With(vents.Select(vent => Vent(c, vent)));
    }

    // tg: tgui/packages/tgui/interfaces/AirAlarm/screens/Scrubbers.tsx:8
    public static UiNode AirAlarmControlScrubbers(TguiContext c)
    {
        IReadOnlyList<UiValue> scrubbers = c.Data.Arr("scrubbers");

        if (scrubbers.Count == 0)
            return NothingToShow();

        return new UiNode(UiKind.Fragment).With(scrubbers.Select(scrubber => Scrubber(c, scrubber)));
    }

    // tg: tgui/packages/tgui/interfaces/AirAlarm/screens/Modes.tsx:6
    public static UiNode AirAlarmControlModes(TguiContext c)
    {
        IReadOnlyList<UiValue> modes = c.Data.Arr("modes");
        string selectedModePath = c.Data.Str("selectedModePath");

        if (modes.Count == 0)
            return NothingToShow();

        return new UiNode(UiKind.Stack) { Vertical = true }.With(modes.Select(mode =>
        {
            string path = mode.Str("path");
            bool selected = path == selectedModePath;
            return new UiNode(UiKind.StackItem) { Key = path }.With(
                new UiNode(UiKind.Button)
                {
                    Icon = selected ? "check-square-o" : "square-o",
                    Color = selected ? (mode.Truthy("danger") ? "red" : "green") : null,
                    OnClick = () => c.Act("mode", ("mode", path)),
                    Text = mode.Str("name") + " - " + mode.Str("desc"),
                });
        }));
    }

    // tg: tgui/packages/tgui/interfaces/AirAlarm/screens/Thresholds.tsx:7
    public static UiNode AirAlarmControlThresholds(TguiContext c)
    {
        IReadOnlyList<UiValue> tlvSettings = c.Data.Arr("tlvSettings");
        UiValue thresholdTypeMap = c.Data.Field("thresholdTypeMap");

        UiNode Head(string text, string color) => new UiNode(UiKind.TableCell) { Bold = true, Color = color, Text = text };

        UiNode Edit(UiValue tlv, string typeVar, string typeName)
        {
            double value = tlv.Num(typeVar);
            return new UiNode(UiKind.TableCell) { Key = typeVar }.With(
                new UiNode(UiKind.Button)
                {
                    Fluid = true,
                    OnClick = () => c.SetState(ModalKey, new ActiveModal(tlv.Str("id"), tlv.Str("name"), thresholdTypeMap.Num(typeVar), typeVar, typeName, tlv.Str("unit"))),
                    Text = value == -1 ? "Disabled" : TguiFormat.JsNumber(value) + " " + tlv.Str("unit"),
                });
        }

        var table = new UiNode(UiKind.Table).With(
            new UiNode(UiKind.TableRow) { Key = "head" }.With(
                Head("Threshold", null),
                Head("Danger Below", "bad"),
                Head("Warning Below", "average"),
                Head("Warning Above", "average"),
                Head("Danger Above", "bad"),
                Head("Actions", null)));
        foreach (UiValue tlv in tlvSettings)
        {
            string id = tlv.Str("id");
            table.With(new UiNode(UiKind.TableRow) { Key = "tlv:" + tlv.Str("name"), Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Text = tlv.Str("name") },
                Edit(tlv, "hazard_min", "Minimum Hazard"),
                Edit(tlv, "warning_min", "Minimum Warning"),
                Edit(tlv, "warning_max", "Maximum Warning"),
                Edit(tlv, "hazard_max", "Maximum Hazard"),
                new UiNode(UiKind.TableCell) { Key = "actions" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "reset",
                        Color = "green",
                        Icon = "sync",
                        OnClick = () => c.Act("reset_threshold", ("threshold", id), ("threshold_type", thresholdTypeMap.Num("all"))),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "disable",
                        Color = "red",
                        Icon = "times",
                        OnClick = () => c.Act("set_threshold", ("threshold", id), ("threshold_type", thresholdTypeMap.Num("all")), ("value", -1)),
                    })));
        }
        return table;
    }

    // tg: tgui/packages/tgui/interfaces/AirAlarm/AlarmModal.tsx:6
    public static UiNode AlarmEditingModal(TguiContext c, ActiveModal modal, double oldValue)
    {
        void Finish() => c.SetState<ActiveModal>(ModalKey, null);

        return new UiNode(UiKind.Modal).With(
            new UiNode(UiKind.Section)
            {
                Title = "Threshold Value Editor",
                Buttons = new UiNode(UiKind.Button) { Key = "close", OnClick = Finish, Icon = "times", Color = "red" },
            }.With(
                new UiNode(UiKind.Box) { Text = "Editing the " + modal.TypeName.ToLowerInvariant() + " value for " + modal.Name.ToLowerInvariant() + "..." },
                oldValue == -1
                    ? new UiNode(UiKind.Button)
                    {
                        Key = "enable",
                        OnClick = () => c.Act("set_threshold", ("threshold", modal.Id), ("threshold_type", modal.Type), ("value", 0)),
                        Text = "Enable",
                    }
                    : new UiNode(UiKind.Fragment).With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "threshold:" + modal.Id + ":" + modal.TypeVar,
                            CommitOnly = true,
                            AllowFloats = true,
                            OnChange = value => c.Act("set_threshold", ("threshold", modal.Id), ("threshold_type", modal.Type), ("value", value)),
                            Unit = modal.Unit,
                            Value = oldValue,
                            MinValue = 0,
                            MaxValue = 100000,
                            Step = 10,
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "disable",
                            OnClick = () => c.Act("set_threshold", ("threshold", modal.Id), ("threshold_type", modal.Type), ("value", -1)),
                            Text = "Disable",
                        })));
    }

    private static UiNode Labeled(string label, params UiNode[] content) =>
        new UiNode(UiKind.LabeledItem) { Key = label, Label = label }.With(content);

    // tg: tgui/packages/tgui/interfaces/common/AtmosControls.tsx:43
    public static UiNode Vent(TguiContext c, UiValue props)
    {
        string refID = props.Str("refID");
        string long_name = props.Str("long_name");
        bool power = props.Truthy("power");
        bool overclock = props.Truthy("overclock");
        double integrity = props.Num("integrity");
        double checks = props.Num("checks");
        bool excheck = props.Truthy("excheck");
        bool incheck = props.Truthy("incheck");
        bool direction = props.Truthy("direction");
        double external = props.Num("external");
        double @internal = props.Num("internal");
        bool extdefault = props.Truthy("extdefault");
        bool intdefault = props.Truthy("intdefault");

        UiNode Target(string label, string key, double value, string setAction, string resetAction, bool isDefault) => Labeled(label,
            new UiNode(UiKind.NumberInput)
            {
                Key = key + ":" + refID,
                CommitOnly = true,
                Value = Math.Round(value),
                Unit = "kPa",
                Width = "75px",
                MinValue = 0,
                Step = 10,
                MaxValue = 5066,
                OnChange = v => c.Act(setAction, ("ref", refID), ("value", v)),
            },
            new UiNode(UiKind.Button)
            {
                Key = resetAction,
                Icon = "undo",
                Disabled = isDefault,
                Text = "Reset",
                OnClick = () => c.Act(resetAction, ("ref", refID)),
            });

        return new UiNode(UiKind.Section)
        {
            Key = refID,
            Title = TguiFormat.DecodeHtmlEntities(long_name),
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button)
                {
                    Key = "power",
                    Icon = power ? "power-off" : "times",
                    Selected = power,
                    Disabled = integrity <= 0,
                    Text = power ? "On" : "Off",
                    OnClick = () => c.Act("power", ("ref", refID), ("val", power ? 0 : 1)),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "overclock",
                    Icon = "gauge-high",
                    Color = overclock ? "green" : "yellow",
                    Disabled = integrity <= 0,
                    OnClick = () => c.Act("overclock", ("ref", refID)),
                    Tooltip = (overclock ? "Disable" : "Enable") + " overclocking",
                }),
        }.With(
            new UiNode(UiKind.LabeledList).With(
                Labeled("Integrity",
                    new UiNode(UiKind.Box)
                    {
                        Tooltip = "Overclocking will allow the vent to overpower extreme pressure conditions. However, it will also cause the vent to become damaged over time and eventually fail. The lower the integrity, the less effective the vent will be when in normal operation.",
                        Text = TguiFormat.ToFixed(integrity * 100, 2) + "%",
                    }),
                Labeled("Mode",
                    new UiNode(UiKind.Button)
                    {
                        Key = "direction",
                        Icon = "sign-in-alt",
                        Text = direction ? "Pressurizing" : "Siphoning",
                        Color = !direction ? "danger" : null,
                        OnClick = () => c.Act("direction", ("ref", refID), ("val", direction ? 0 : 1)),
                    }),
                Labeled("Pressure Regulator",
                    new UiNode(UiKind.Button)
                    {
                        Key = "incheck",
                        Icon = "sign-in-alt",
                        Text = "Internal",
                        Selected = incheck,
                        OnClick = () => c.Act("incheck", ("ref", refID), ("val", checks)),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "excheck",
                        Icon = "sign-out-alt",
                        Text = "External",
                        Selected = excheck,
                        OnClick = () => c.Act("excheck", ("ref", refID), ("val", checks)),
                    }),
                incheck ? Target("Internal Target", "internal", @internal, "set_internal_pressure", "reset_internal_pressure", intdefault) : null,
                excheck ? Target("External Target", "external", external, "set_external_pressure", "reset_external_pressure", extdefault) : null));
    }

    // tg: tgui/packages/tgui/interfaces/common/AtmosControls.tsx:192
    public static UiNode Scrubber(TguiContext c, UiValue props)
    {
        string long_name = props.Str("long_name");
        bool power = props.Truthy("power");
        bool scrubbing = props.Truthy("scrubbing");
        string refID = props.Str("refID");
        bool widenet = props.Truthy("widenet");
        IReadOnlyList<UiValue> filter_types = props.Arr("filter_types");

        return new UiNode(UiKind.Section)
        {
            Key = refID,
            Title = TguiFormat.DecodeHtmlEntities(long_name),
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "power",
                Icon = power ? "power-off" : "times",
                Text = power ? "On" : "Off",
                Selected = power,
                OnClick = () => c.Act("power", ("ref", refID), ("val", power ? 0 : 1)),
            },
        }.With(
            new UiNode(UiKind.LabeledList).With(
                Labeled("Mode",
                    new UiNode(UiKind.Button)
                    {
                        Key = "scrubbing",
                        Icon = scrubbing ? "filter" : "sign-in-alt",
                        Color = !scrubbing ? "danger" : null,
                        Text = scrubbing ? "Scrubbing" : "Siphoning",
                        OnClick = () => c.Act("scrubbing", ("ref", refID), ("val", scrubbing ? 0 : 1)),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "widenet",
                        Icon = widenet ? "expand" : "compress",
                        Selected = widenet,
                        Text = widenet ? "Expanded range" : "Normal range",
                        OnClick = () => c.Act("widenet", ("ref", refID), ("val", widenet ? 0 : 1)),
                    }),
                // the filter buttons wrap at the window's width, as tgui's inline buttons do
                new UiNode(UiKind.LabeledItem) { Key = "Filters", Label = "Filters", Wrap = true }.With(
                    scrubbing
                        ? filter_types.Select(filter =>
                        {
                            string gas_id = filter.Str("gas_id");
                            bool enabled = filter.Truthy("enabled");
                            return new UiNode(UiKind.Button)
                            {
                                Key = gas_id,
                                Icon = enabled ? "check-square-o" : "square-o",
                                Tooltip = filter.Str("gas_name"),
                                Selected = enabled,
                                OnClick = () => c.Act("toggle_filter", ("ref", refID), ("val", gas_id)),
                                Text = TguiFormat.GetGasLabel(gas_id, filter.Str("gas_name")),
                            };
                        }).ToArray()
                        : new[] { new UiNode(UiKind.Box) { Text = "N/A" } })));
    }
}

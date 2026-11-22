using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosRobotact.jsx (silicons SI2, D326: the cyborg tablet's
// self-management app). Engine-free (compiled into the tests too). Flex is the kit's Stack; AnimatedNumber shows its value; the log's
// green <font> is the box's colour.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosRobotact
{
    // tg: NtosRobotact.jsx:21  <NtosWindow width={800} height={600}>
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 800, 600, null, Content(c));

    private static string Slice(string text, int length) => text.Length > length ? text[..length] : text;

    private static UiNode Item(string key, string label, string color, string text) =>
        new UiNode(UiKind.LabeledItem) { Key = key, Label = label, Color = color, Text = text };

    // tg: NtosRobotact.jsx:31  NtosRobotactContent
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        int tabMain = c.UseState("tab_main", 1);
        int tabSub = c.UseState("tab_sub", 1);
        double charge = data.Num("charge");
        double maxcharge = data.Num("maxcharge");
        double integrity = data.Num("integrity");
        double lampIntensity = data.Num("lampIntensity");
        double lampConsumption = data.Num("lampConsumption");
        string cover = data.Str("cover");
        string locomotion = data.Str("locomotion");
        string wireModule = data.Str("wireModule");
        string wireCamera = data.Str("wireCamera");
        string wireAI = data.Str("wireAI");
        string wireLaw = data.Str("wireLaw");
        string sensors = data.Str("sensors");
        double printerPictures = data.Num("printerPictures");
        double printerToner = data.Num("printerToner");
        double printerTonerMax = data.Num("printerTonerMax");
        bool thrustersInstalled = data.Truthy("thrustersInstalled");
        string thrustersStatus = data.Str("thrustersStatus");
        bool selfDestructAble = data.Truthy("selfDestructAble");
        IReadOnlyList<UiValue> cyborgGroups = data.Arr("cyborg_groups");
        bool masterAiOnline = data.Truthy("masterAI_online");
        bool masterAiConnected = data.Truthy("MasterAI_connected");
        string borgName = data.Str("borgName");
        string borgType = data.Str("designation");
        string masterAI = data.Str("masterAI");
        IReadOnlyList<UiValue> laws = data.Arr("Laws");
        IReadOnlyList<UiValue> borgLog = data.Arr("borgLog");
        IReadOnlyList<UiValue> borgUpgrades = data.Arr("borgUpgrades");

        UiNode MainTab(int index, string text) => new UiNode(UiKind.Tab)
        {
            Key = "main:" + index,
            Icon = "list",
            Selected = tabMain == index,
            Text = text,
            OnClick = () => c.SetState("tab_main", index),
        };
        UiNode SubTab(int index, string text) => new UiNode(UiKind.Tab)
        {
            Key = "sub:" + index,
            Selected = tabSub == index,
            Text = text,
            OnClick = () => c.SetState("tab_sub", index),
        };

        var root = new UiNode(UiKind.Stack) { Key = "root", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "tabs" }.With(new UiNode(UiKind.Tabs).With(
                MainTab(1, "Status"), MainTab(2, "Logs"), MainTab(3, "Network"))));

        if (tabMain == 1)
        {
            var configuration = new UiNode(UiKind.Section) { Key = "config", Title = "Configuration", Fill = true }.With(new UiNode(UiKind.LabeledList).With(
                Item("unit", "Unit", null, Slice(borgName, 17)),
                Item("type", "Type", null, borgType),
                Item("ai", "AI", null, Slice(masterAI, 17))));

            var status = new UiNode(UiKind.Section) { Key = "status", Title = "Status" }.With(
                new UiNode(UiKind.Box) { Key = "chargelabel", Text = "Charge:" },
                new UiNode(UiKind.Button) { Key = "alertPower", Text = "Power Alert", Disabled = charge != 0, OnClick = () => c.Act("alertPower") },
                new UiNode(UiKind.ProgressBar)
                {
                    Key = "charge",
                    Value = maxcharge > 0 ? charge / maxcharge : 0,
                    Ranges = new[] { ("good", 0.5, double.PositiveInfinity), ("average", 0.1, 0.5), ("bad", double.NegativeInfinity, 0.1) },
                    Text = TguiFormat.FormatEnergy(charge),
                },
                new UiNode(UiKind.Box) { Key = "integritylabel", Text = "Chassis Integrity:" },
                new UiNode(UiKind.ProgressBar)
                {
                    Key = "integrity",
                    Value = integrity,
                    MinValue = 0,
                    MaxValue = 100,
                    Ranges = new[] { ("bad", double.NegativeInfinity, 25.0), ("average", 25.0, 75.0), ("good", 75.0, double.PositiveInfinity) },
                });
            var lamp = new UiNode(UiKind.Section) { Key = "lamp", Title = "Lamp Power" }.With(
                new UiNode(UiKind.Slider)
                {
                    Key = "lampIntensity",
                    Value = lampIntensity,
                    Step = 1,
                    StepPixelSize = 25,
                    MaxValue = 5,
                    MinValue = 1,
                    OnChange = value => c.Act("lampIntensity", ("ref", value)),
                },
                new UiNode(UiKind.Box) { Key = "usage", Text = "Lamp power usage: " + TguiFormat.FormatPower(lampIntensity * lampConsumption) });

            var right = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Section) { Key = "subtabs" }.With(new UiNode(UiKind.Tabs) { Fluid = true }.With(
                    SubTab(1, "Actions"), SubTab(2, "Upgrades"), SubTab(3, "Diagnostics"))));
            if (tabSub == 1)
            {
                var actions = new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "cover", Label = "Maintenance Cover" }.With(c.ButtonConfirm("coverunlock", new UiNode(UiKind.Button)
                    {
                        Text = "Unlock",
                        Disabled = cover == "UNLOCKED",
                        OnClick = () => c.Act("coverunlock"),
                    })),
                    new UiNode(UiKind.LabeledItem) { Key = "sensors", Label = "Sensor Overlay" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "sensors",
                        Text = sensors,
                        OnClick = () => c.Act("toggleSensors"),
                    }),
                    new UiNode(UiKind.LabeledItem) { Key = "photos", Label = "Stored Photos (" + TguiFormat.JsNumber(printerPictures) + ")" }.With(
                        new UiNode(UiKind.Button) { Key = "view", Text = "View", Disabled = printerPictures == 0, OnClick = () => c.Act("viewImage") },
                        new UiNode(UiKind.Button) { Key = "print", Text = "Print", Disabled = printerPictures == 0, OnClick = () => c.Act("printImage") }),
                    new UiNode(UiKind.LabeledItem) { Key = "toner", Label = "Printer Toner" }.With(new UiNode(UiKind.ProgressBar)
                    {
                        Key = "toner",
                        Value = printerTonerMax > 0 ? printerToner / printerTonerMax : 0,
                    }));
                if (thrustersInstalled)
                    actions.With(new UiNode(UiKind.LabeledItem) { Key = "thrusters", Label = "Toggle Thrusters" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "thrusters",
                        Text = thrustersStatus,
                        OnClick = () => c.Act("toggleThrusters"),
                    }));
                if (selfDestructAble)
                    actions.With(new UiNode(UiKind.LabeledItem) { Key = "selfdestruct", Label = "Self Destruct" }.With(c.ButtonConfirm("selfDestruct", new UiNode(UiKind.Button)
                    {
                        Text = "ACTIVATE",
                        Color = "red",
                        OnClick = () => c.Act("selfDestruct"),
                    })));
                right.With(new UiNode(UiKind.Section) { Key = "actions" }.With(actions));
            }
            if (tabSub == 2)
                right.With(new UiNode(UiKind.Section) { Key = "upgrades" }.With(
                    borgUpgrades.Select((upgrade, i) => new UiNode(UiKind.Box) { Key = "upgrade" + i, Text = upgrade.AsStr() }).ToArray()));
            if (tabSub == 3)
                right.With(new UiNode(UiKind.Section) { Key = "diagnostics" }.With(new UiNode(UiKind.LabeledList).With(
                    Item("ai", "AI Connection", wireAI == "FAULT" ? "red" : wireAI == "READY" ? "yellow" : "green", wireAI),
                    Item("law", "LawSync", wireLaw == "FAULT" ? "red" : "green", wireLaw),
                    Item("camera", "Camera", wireCamera == "FAULT" ? "red" : wireCamera == "DISABLED" ? "yellow" : "green", wireCamera),
                    Item("module", "Module Controller", wireModule == "FAULT" ? "red" : "green", wireModule),
                    Item("motor", "Motor Controller", locomotion == "FAULT" ? "red" : locomotion == "DISABLED" ? "yellow" : "green", locomotion),
                    Item("cover", "Maintenance Cover", cover == "UNLOCKED" ? "red" : "green", cover))));

            root.With(new UiNode(UiKind.StackItem) { Key = "status" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "left", Width = "30%" }.With(configuration),
                new UiNode(UiKind.StackItem) { Key = "middle", Grow = 1 }.With(status, lamp),
                new UiNode(UiKind.StackItem) { Key = "right", Width = "50%" }.With(right))));

            root.With(new UiNode(UiKind.StackItem) { Key = "laws", Height = 21 }.With(new UiNode(UiKind.Section)
            {
                Key = "laws",
                Title = "Laws",
                Fill = true,
                Scrollable = true,
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "lawstate", Text = "State Laws", OnClick = () => c.Act("lawstate") },
                    new UiNode(UiKind.Button) { Key = "lawchannel", Icon = "volume-off", OnClick = () => c.Act("lawchannel") }),
            }.With(laws.Select((law, i) => new UiNode(UiKind.Box) { Key = "law" + i, Text = law.AsStr() }).ToArray())));
        }

        if (tabMain == 2)
        {
            root.With(new UiNode(UiKind.StackItem) { Key = "logs", Height = 40 }.With(new UiNode(UiKind.Section)
            {
                Key = "log",
                Fill = true,
                Scrollable = true,
                Background = "black",
            }.With(borgLog.Select((log, i) => new UiNode(UiKind.Box) { Key = "log" + i, Color = "green", Text = log.AsStr() }).ToArray())));
        }

        if (tabMain == 3)
        {
            var network = new UiNode(UiKind.StackItem) { Key = "network", Height = 40 }.With(
                new UiNode(UiKind.Section) { Key = "master", Title = masterAiConnected ? masterAI : "NOT CONFIGURED", TextAlign = "center" }.With(
                    new UiNode(UiKind.LabeledList).With(new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status" }.With(new UiNode(UiKind.Box)
                    {
                        Key = "value",
                        Color = masterAiOnline ? "good" : "bad",
                        Text = !masterAiConnected ? "No Conection" : masterAiOnline ? "Online" : "Unresponsive",
                    }))));

            var groups = new UiNode(UiKind.Stack) { Key = "groups", Vertical = true };
            int groupIndex = 0;
            foreach (UiValue borggroup in cyborgGroups)
            {
                var row = new UiNode(UiKind.Stack);
                int borgIndex = 0;
                foreach (UiValue cyborg in borggroup.Items)
                {
                    bool notResponding = cyborg.Truthy("status");
                    bool lockedDown = cyborg.Truthy("locked_down");
                    double integ = cyborg.Num("integ");
                    UiValue chargeValue = cyborg.Get("charge");
                    bool hasCharge = chargeValue is { Kind: UiValueKind.Number };
                    double borgCharge = hasCharge ? chargeValue.Number : 0;
                    row.With(new UiNode(UiKind.StackItem) { Key = "borg" + borgIndex++, Width = "24.25%" }.With(
                        new UiNode(UiKind.Section) { Key = cyborg.Str("ref"), Title = Slice(cyborg.Str("otherBorgName"), 20) }.With(new UiNode(UiKind.LabeledList).With(
                            Item("status", "Status", notResponding ? "bad" : lockedDown ? "average" : "good",
                                notResponding ? "Not Responding" : lockedDown ? "Locked Down" : cyborg.Truthy("shell_discon") ? "Nominal/Disconnected" : "Nominal"),
                            Item("condition", "Condition", integ <= 25 ? "bad" : integ <= 75 ? "average" : "good",
                                integ == 0 ? "Hard Fault" : integ <= 25 ? "Functionality Disrupted" : integ <= 75 ? "Functionality Impaired" : "Operational"),
                            Item("charge", "Charge", borgCharge <= 30 ? "bad" : borgCharge <= 70 ? "average" : "good",
                                hasCharge ? TguiFormat.JsNumber(borgCharge) + "%" : "No Cell"),
                            Item("model", "Model", null, cyborg.Str("module"))))));
                }
                groups.With(new UiNode(UiKind.StackItem) { Key = "group" + groupIndex++ }.With(row, new UiNode(UiKind.Divider) { Key = "divider" }));
            }
            network.With(groups);
            if (cyborgGroups.Count == 0)
                network.With(new UiNode(UiKind.NoticeBox) { Key = "unavailable", TextAlign = "center" }.With(
                    new UiNode(UiKind.Box) { Key = "text", FontSize = "2em", Text = "CONNECTION UNAVAILABLE -- NETWORK STATUS UNKNOWN" }));
            root.With(network);
        }

        return root;
    }
}

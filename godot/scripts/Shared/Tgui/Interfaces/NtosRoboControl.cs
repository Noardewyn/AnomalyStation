using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosRoboControl.jsx (gaps S4b, D327): BotKeeper, the bots
// in range with their patrol, summon and pAI buttons, the MULEs' delivery controls, and the drones' access toggle and pings.
// useSharedState is the window's own state; a robot section's coloured border is left out (the kit's Section has no border style).
// Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosRoboControl
{
    private static readonly (string, double, double)[] PowerRanges =
    {
        ("good", 60, double.PositiveInfinity),
        ("average", 20, 60),
        ("bad", double.NegativeInfinity, 20),
    };

    // tg: NtosRoboControl.jsx:18
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int tabMain = c.UseState("tab_main", 1);
        bool droneaccess = data.Truthy("droneaccess");
        string idOwner = data.Get("id_owner") is { IsNull: false } owner ? owner.Text ?? "" : "";

        var content = new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(
            new UiNode(UiKind.Section) { Key = "console", Title = "Robot Control Console" }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "id", Label = "ID Card", Text = idOwner },
                new UiNode(UiKind.LabeledItem) { Key = "count", Label = "Bots In Range", Text = TguiFormat.JsNumber(data.Num("botcount")) })),
            new UiNode(UiKind.StackItem) { Key = "tabs" }.With(new UiNode(UiKind.Tabs).With(
                new UiNode(UiKind.Tab) { Key = "tab:bots", Icon = "robot", Text = "Bots", Selected = tabMain == 1, OnClick = () => c.SetState("tab_main", 1) },
                new UiNode(UiKind.Tab) { Key = "tab:drones", Icon = "hammer", Text = "Drones", Selected = tabMain == 2, OnClick = () => c.SetState("tab_main", 2) })));
        if (tabMain == 1)
        {
            var bots = new UiNode(UiKind.StackItem) { Key = "bots" }.With(new UiNode(UiKind.Section).With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "inrange", Label = "Bots in range", Text = TguiFormat.JsNumber(data.Num("botcount")) })));
            foreach (UiValue robot in data.Arr("bots"))
                bots.With(RobotInfo(c, robot));
            content.With(bots);
        }
        if (tabMain == 2)
        {
            var pings = new UiNode(UiKind.Section).With(
                new UiNode(UiKind.Button)
                {
                    Key = "droneaccess",
                    Icon = "address-card",
                    Tooltip = "Grant/Remove Drone access to interact with machines and wires that would otherwise be deemed dangerous.",
                    Color = droneaccess ? "good" : "bad",
                    Text = droneaccess ? "Grant Drone Access" : "Revoke Drone Access",
                    OnClick = () => c.Act("changedroneaccess"),
                },
                new UiNode(UiKind.Box) { Key = "pings", Text = "Drone Pings" });
            foreach (UiValue pingType in data.Arr("dronepingtypes"))
            {
                string type = pingType.Text;
                pings.With(new UiNode(UiKind.Button) { Key = "ping:" + type, Icon = "bullhorn", Tooltip = "Issue a drone ping.", Text = type, OnClick = () => c.Act("ping_drones", ("ping_type", type)) });
            }
            var drones = new UiNode(UiKind.StackItem) { Key = "drones", Grow = 1 }.With(pings);
            foreach (UiValue drone in data.Arr("drones"))
                drones.With(DroneInfo(drone));
            content.With(drones);
        }
        return NtosWindow.Render(c, 550, 550, null, content);
    }

    // tg: NtosRoboControl.jsx:100
    private static UiNode RobotInfo(TguiContext c, UiValue robot)
    {
        string botRef = robot.Str("bot_ref");
        // Get a mule object
        UiValue mule = robot.Truthy("mule_check") ? c.Data.Arr("mules").FirstOrDefault(m => m.Str("mule_ref") == botRef) : null;
        string muleRef = mule?.Str("mule_ref");
        UiNode Act(string key, string text, string action, string reference) =>
            new UiNode(UiKind.Button) { Key = key, Fluid = true, Text = text, OnClick = () => c.Act(action, ("robot", reference)) };
        UiNode Check(string key, string text, string field, string action) =>
            new UiNode(UiKind.Button) { Key = key, Fluid = true, Text = text, Checkbox = mule.Truthy(field), OnClick = () => c.Act(action, ("robot", muleRef)) };

        var info = new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "model", Label = "Model", Text = robot.Str("model") },
            new UiNode(UiKind.LabeledItem) { Key = "location", Label = "Location", Text = robot.Str("locat") },
            new UiNode(UiKind.LabeledItem) { Key = "mode", Label = "Status", Text = robot.Str("mode") });
        var controls = new UiNode(UiKind.StackItem) { Key = "controls", Width = "150px" };
        if (mule != null)
        {
            string load = mule.Str("load");
            string dest = mule.Str("dest");
            info.With(
                new UiNode(UiKind.LabeledItem) { Key = "botid", Label = "Bot ID", Text = mule.Str("id") },
                new UiNode(UiKind.LabeledItem) { Key = "load", Label = "Loaded Cargo", Text = string.IsNullOrEmpty(load) ? "N/A" : load },
                new UiNode(UiKind.LabeledItem) { Key = "home", Label = "Home", Text = mule.Str("home") },
                new UiNode(UiKind.LabeledItem) { Key = "dest", Label = "Destination", Text = string.IsNullOrEmpty(dest) ? "N/A" : dest },
                new UiNode(UiKind.LabeledItem) { Key = "power", Label = "Power" }.With(new UiNode(UiKind.ProgressBar)
                {
                    Value = mule.Num("power"),
                    MinValue = 0,
                    MaxValue = 100,
                    Ranges = PowerRanges,
                }));
            controls.With(
                Act("destination", "Set Destination", "destination", muleRef),
                Act("setid", "Set ID", "setid", muleRef),
                Act("sethome", "Set Home", "sethome", muleRef),
                Act("unload", "Unload Cargo", "unload", muleRef),
                Check("autoret", "Auto Return", "autoReturn", "autoret"),
                Check("autopick", "Auto Pickup", "autoPickup", "autopick"),
                Check("report", "Delivery Report", "reportDelivery", "report"));
        }
        else
        {
            controls.With(
                Act("patroloff", "Stop Patrol", "patroloff", botRef),
                Act("patrolon", "Start Patrol", "patrolon", botRef),
                Act("summon", "Summon", "summon", botRef),
                Act("ejectpai", "Eject PAi", "ejectpai", botRef));
        }

        return new UiNode(UiKind.Section)
        {
            Key = "bot:" + botRef,
            Title = robot.Str("name"),
            Buttons = mule == null ? null : new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "go", Icon = "play", Tooltip = "Go to Destination.", OnClick = () => c.Act("go", ("robot", muleRef)) }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "stop", Icon = "pause", Tooltip = "Stop Moving.", OnClick = () => c.Act("stop", ("robot", muleRef)) }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "gohome", Icon = "home", Tooltip = "Travel Home.", OnClick = () => c.Act("home", ("robot", muleRef)) })),
        }.With(new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem) { Key = "info", Grow = 1 }.With(info),
            controls));
    }

    // tg: NtosRoboControl.jsx:300
    private static UiNode DroneInfo(UiValue drone)
    {
        bool down = drone.Truthy("status");
        return new UiNode(UiKind.Section) { Key = "drone:" + drone.Str("drone_ref"), Title = drone.Str("name") }.With(new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status" }.With(new UiNode(UiKind.Box)
                {
                    Color = down ? "bad" : "good",
                    Text = down ? "Not Responding" : "Nominal",
                })))));
    }
}

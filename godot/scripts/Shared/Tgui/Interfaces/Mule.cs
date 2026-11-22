using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Mule.tsx (gaps package C3): the MULEbot's window (mulebot_control.dm:7).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// tg's own quirks kept: the power button sends "on" and the auto-return box "autored", which bot_control does not know: both do nothing in tg either.

public static class Mule
{
    // tg: Mule.tsx:154
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool powerStatus = data.Truthy("powerStatus");
        bool load = data.Truthy("load");
        bool mulebotLocked = data.Truthy("locked") && !data.Truthy("siliconUser");
        double cellPercent = data.Field("cellPercent") is { Kind: UiValueKind.Number } cell ? cell.Number : 0;

        // tg: Mule.tsx:169  <Window width={350} height={540}>
        return c.Window(350, 540, null,
            AirAlarm.InterfaceLockNoticeBox(c),
            new UiNode(UiKind.Section)
            {
                Title = "Status",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "rename", Icon = "fa-poll-h", Text = "Rename", OnClick = () => c.Act("rename") },
                    !mulebotLocked
                        ? new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Icon = powerStatus ? "power-off" : "times",
                            Selected = powerStatus,
                            Text = powerStatus ? "On" : "Off",
                            OnClick = () => c.Act("on"),
                        }
                        : null),
            }.With(
                new UiNode(UiKind.ProgressBar) { Key = "cell", Value = cellPercent / 100, Color = cellPercent > 0 ? "good" : "bad" },
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Mode", Color = data.Str("modeStatus"), Text = data.Str("mode") },
                    new UiNode(UiKind.LabeledItem)
                    {
                        Label = "Load",
                        Color = load ? "good" : "average",
                        Text = load ? TguiFormat.Capitalize(data.Str("loadName")) : "None",
                    })),
            !mulebotLocked ? MuleControls(c) : null);
    }

    // tg: Mule.tsx:37
    private static UiNode MuleControls(TguiContext c)
    {
        UiValue data = c.Data;
        string destination = data.Str("destination");
        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Section)
            {
                Title = "Controls",
                Buttons = new UiNode(UiKind.Fragment).With(
                    data.Truthy("load") ? new UiNode(UiKind.Button) { Key = "unload", Icon = "eject", Text = "Unload", OnClick = () => c.Act("unload") } : null,
                    data.Truthy("paiInserted") ? new UiNode(UiKind.Button) { Key = "eject_pai", Icon = "eject", Text = "Eject PAI", OnClick = () => c.Act("eject_pai") } : null),
            }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "ID" }.With(new UiNode(UiKind.Button) { Key = "setid", Text = data.Str("botId"), OnClick = () => c.Act("setid") }),
                new UiNode(UiKind.LabeledItem) { Label = "Home" }.With(new UiNode(UiKind.Button) { Key = "sethome", Text = data.Str("homeDestination"), OnClick = () => c.Act("sethome") }),
                new UiNode(UiKind.LabeledItem) { Label = "Destination" }.With(new UiNode(UiKind.Dropdown)
                {
                    Key = "destination",
                    Text = string.IsNullOrEmpty(destination) ? "None" : destination,
                    Options = (data.Field("destinationsList")?.Items ?? Array.Empty<UiValue>()).Select(o => o.AsStr()).ToList(),
                    Width = "188px",
                    OnSelected = value => c.Act("destination", ("value", value)),
                }))),
            new UiNode(UiKind.Section) { Title = "Actions" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button) { Key = "stop", Width = "60px", Icon = "stop", Color = "bad", Text = "Stop", OnClick = () => c.Act("stop") }),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button) { Key = "go", Width = "60px", Icon = "play", Color = "average", Text = "Go", OnClick = () => c.Act("go") }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "home", Width = "60px", Icon = "home", Text = "Home", OnClick = () => c.Act("home") }))),
            new UiNode(UiKind.Section) { Title = "Settings" }.With(
                new UiNode(UiKind.Button) { Key = "autoret", Checkbox = data.Truthy("autoReturn"), Text = "Auto-Return", OnClick = () => c.Act("autored") },
                new UiNode(UiKind.Button) { Key = "autopick", Checkbox = data.Truthy("autoPickup"), Text = "Auto-Pickup", OnClick = () => c.Act("autopick") },
                new UiNode(UiKind.Button) { Key = "report", Checkbox = data.Truthy("reportDelivery"), Text = "Report-Delivery", OnClick = () => c.Act("report") },
                data.Truthy("allowPossession")
                    ? new UiNode(UiKind.Button) { Key = "personality", Checkbox = data.Truthy("possessionEnabled"), Text = "Download Personality", OnClick = () => c.Act("toggle_personality") }
                    : null));
    }
}

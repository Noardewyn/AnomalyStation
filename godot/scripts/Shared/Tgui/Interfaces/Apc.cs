using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Apc.jsx and common/InterfaceLockNoticeBox.jsx (M7
// slice 3, the APC's flat window, D162; tg's /obj/machinery/power/apc ui_data, apc_main.dm:385).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Apc
{
    // tg: tgui/packages/tgui/interfaces/Apc.jsx:14
    public static UiNode Render(TguiContext c) =>
        c.Window(450, 445, null, ApcContent(c)); // <Window.Content scrollable>: the content in the window's own area

    // tg: Apc.jsx:24
    private static (string Color, string ExternalPowerText, string ChargingText) PowerStatus(double status) => status switch
    {
        2 => ("good", "External Power", "Fully Charged"),
        1 => ("average", "Low External Power", "Charging: "),
        _ => ("bad", "No External Power", "Not Charging"),
    };

    // tg: Apc.jsx:42
    private static (string Icon, string Content, string Action)? MalfStatus(double status) => status switch
    {
        1 => ("terminal", "Override Programming", "hack"),
        2 => ("caret-square-down", "Shunt Core Process", "occupy"),
        3 => ("caret-square-left", "Return to Main Core", "deoccupy"),
        4 => ("caret-square-down", "Shunt Core Process", "occupy"),
        _ => null,
    };

    // tg: Apc.jsx:65
    private static UiNode ApcContent(TguiContext c)
    {
        UiValue data = c.Data;
        bool locked = data.Truthy("locked") && !data.Truthy("siliconUser");
        var externalPowerStatus = PowerStatus(data.Num("externalPower"));
        var chargingStatus = PowerStatus(data.Num("chargingStatus"));
        IReadOnlyList<UiValue> channelArray = data.Arr("powerChannels");
        var malfStatus = MalfStatus(data.Num("malfStatus"));
        double adjustedCellChange = data.Num("powerCellStatus") / 100;
        if (data.Num("failTime") > 0)
        {
            // tg: Apc.jsx:76
            return new UiNode(UiKind.NoticeBox) { Key = "failure", Color = "info", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Bold = true, FontSize = "1.17em", Text = "SYSTEM FAILURE" },
                new UiNode(UiKind.Box) { Text = "I/O regulators have malfunctioned!" },
                new UiNode(UiKind.Box) { Text = "Awaiting system reboot." },
                new UiNode(UiKind.Box) { Text = "Executing software reboot in " + TguiFormat.JsNumber(data.Num("failTime")) + " seconds..." },
                new UiNode(UiKind.Button)
                {
                    Key = "reboot",
                    Icon = "sync",
                    Text = "Reboot Now",
                    Tooltip = "Force an interface reset.",
                    OnClick = () => c.Act("reboot"),
                });
        }

        var channels = new UiNode(UiKind.LabeledList) { Key = "channels" };
        foreach (UiValue channel in channelArray)
        {
            UiValue topicParams = channel.Field("topicParams");
            double status = channel.Num("status");
            string title = channel.Str("title");
            (string Key, object Value)[] Params(string which) =>
                topicParams.Field(which)?.Fields.Select(f => (f.Key, (object)f.Value)).ToArray() ?? Array.Empty<(string, object)>();
            // tg: Apc.jsx:147
            channels.With(new UiNode(UiKind.LabeledItem)
            {
                Key = title,
                Label = title,
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Box) { Key = "state", Color = status >= 2 ? "good" : "bad", Text = status >= 2 ? "On" : "Off" },
                    new UiNode(UiKind.Button)
                    {
                        Key = "auto",
                        Icon = "sync",
                        Text = "Auto",
                        Selected = !locked && (status == 1 || status == 3),
                        Disabled = locked,
                        OnClick = () => c.Act("channel", Params("auto")),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "on",
                        Icon = "power-off",
                        Text = "On",
                        Selected = !locked && status == 2,
                        Disabled = locked,
                        OnClick = () => c.Act("channel", Params("on")),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "off",
                        Icon = "times",
                        Text = "Off",
                        Selected = !locked && status == 0,
                        Disabled = locked,
                        OnClick = () => c.Act("channel", Params("off")),
                    }),
            }.With(new UiNode(UiKind.Box) { Text = channel.Str("powerLoad") }));
        }
        // tg: Apc.jsx:190
        channels.With(new UiNode(UiKind.LabeledItem) { Key = "total", Label = "Total Load" }.With(
            new UiNode(UiKind.Box) { Bold = true, Text = data.Str("totalLoad") }));

        bool isOperating = data.Truthy("isOperating");
        bool chargeMode = data.Truthy("chargeMode");
        bool coverLocked = data.Truthy("coverLocked");
        bool siliconUser = data.Truthy("siliconUser");
        return new UiNode(UiKind.Fragment).With(
            // tg: Apc.jsx:99
            AirAlarm.InterfaceLockNoticeBox(c, siliconUser: data.Truthy("remoteAccess") || siliconUser, preventLocking: data.Truthy("remoteAccess")),
            // tg: Apc.jsx:103
            new UiNode(UiKind.Section) { Key = "status", Title = "Power Status" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "breaker",
                        Label = "Main Breaker",
                        Buttons = new UiNode(UiKind.Button)
                        {
                            Key = "breaker",
                            Icon = isOperating ? "power-off" : "times",
                            Text = isOperating ? "On" : "Off",
                            Selected = isOperating && !locked,
                            Disabled = locked,
                            OnClick = () => c.Act("breaker"),
                        },
                    }.With(new UiNode(UiKind.Box) { Color = externalPowerStatus.Color, Text = "[ " + externalPowerStatus.ExternalPowerText + " ]" }),
                    new UiNode(UiKind.LabeledItem) { Key = "cell", Label = "Power Cell" }.With(
                        new UiNode(UiKind.ProgressBar) { Key = "cell", Color = "good", Value = adjustedCellChange }),
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "charge",
                        Label = "Charge Mode",
                        Buttons = new UiNode(UiKind.Button)
                        {
                            Key = "charge",
                            Icon = chargeMode ? "sync" : "times",
                            Text = chargeMode ? "Auto" : "Off",
                            Disabled = locked,
                            OnClick = () => c.Act("charge"),
                        },
                    }.With(new UiNode(UiKind.Box)
                    {
                        Color = chargingStatus.Color,
                        Text = "[ " + chargingStatus.ChargingText + (data.Num("chargingStatus") == 1 ? data.Str("chargingPowerDisplay") : "") + " ]",
                    }))),
            // tg: Apc.jsx:142
            new UiNode(UiKind.Section) { Key = "channels", Title = "Power Channels" }.With(channels),
            // tg: Apc.jsx:195
            new UiNode(UiKind.Section)
            {
                Key = "misc",
                Title = "Misc",
                Buttons = siliconUser
                    ? new UiNode(UiKind.Fragment).With(
                        data.Truthy("malfStatus") && malfStatus is { } malf
                            ? new UiNode(UiKind.Button) { Key = "malf", Icon = malf.Icon, Text = malf.Content, Color = "bad", OnClick = () => c.Act(malf.Action) }
                            : null,
                        new UiNode(UiKind.Button) { Key = "overload", Icon = "lightbulb-o", Text = "Overload", OnClick = () => c.Act("overload") })
                    : null,
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "cover",
                        Label = "Cover Lock",
                        Buttons = new UiNode(UiKind.Button)
                        {
                            Key = "cover",
                            Tooltip = "APC cover can be pried open with a crowbar.",
                            Icon = coverLocked ? "lock" : "unlock",
                            Text = coverLocked ? "Engaged" : "Disengaged",
                            Disabled = locked,
                            OnClick = () => c.Act("cover"),
                        },
                    },
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "emergency",
                        Label = "Emergency Lighting",
                        Buttons = new UiNode(UiKind.Button)
                        {
                            Key = "emergency_lighting",
                            Tooltip = "Lights use internal power cell when there is no power available.",
                            Icon = "lightbulb-o",
                            Text = data.Truthy("emergencyLights") ? "Enabled" : "Disabled",
                            Disabled = locked,
                            OnClick = () => c.Act("emergency_lighting"),
                        },
                    },
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "nightshift",
                        Label = "Night Shift Lighting",
                        Buttons = new UiNode(UiKind.Button)
                        {
                            Key = "toggle_nightshift",
                            Tooltip = "Dim lights to reduce power consumption.",
                            Icon = "lightbulb-o",
                            Text = data.Truthy("nightshiftLights") ? "Enabled" : "Disabled",
                            Disabled = data.Truthy("disable_nightshift_toggle"),
                            OnClick = () => c.Act("toggle_nightshift"),
                        },
                    })));
    }
}

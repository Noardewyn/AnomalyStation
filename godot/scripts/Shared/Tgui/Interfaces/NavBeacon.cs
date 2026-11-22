using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NavBeacon.tsx (gaps package C3): the navigation beacon's codes (navbeacon.dm:844).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NavBeacon
{
    // tg: NavBeacon.tsx:39
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool disabled = data.Truthy("locked") && !data.Truthy("siliconUser");

        // tg: NavBeacon.tsx:42  <Window title="Nagivational Beacon" width={400} height={350}>
        return c.Window(400, 350, "Nagivational Beacon",
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                AirAlarm.InterfaceLockNoticeBox(c),
                ControlSection(c, disabled),
                MaintenanceSection(c, disabled)));
    }

    // tg: NavBeacon.tsx:63
    private static UiNode ControlSection(TguiContext c, bool disabled)
    {
        UiValue controls = c.Data.Field("controls");
        UiValue staticControls = c.Data.Field("static_controls");
        bool patrol = controls?.Truthy("patrol_enabled") == true;
        bool delivery = controls?.Truthy("delivery_enabled") == true;
        string location = controls?.Field("location") is { IsNull: false } l ? l.AsStr() : "None set";
        string next = controls?.Field("patrol_next") is { IsNull: false } n ? n.AsStr() : "No next patrol location";
        return new UiNode(UiKind.Section) { Title = "Controls" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Location" }.With(new UiNode(UiKind.Button)
                {
                    Key = "location",
                    Fluid = true,
                    Text = location,
                    Icon = "pencil-alt",
                    Disabled = disabled,
                    OnClick = () => c.Act("set_location"),
                }),
                new UiNode(UiKind.LabeledItem) { Label = "Enable as Patrol Beacon" }.With(new UiNode(UiKind.Button)
                {
                    Key = "patrol",
                    Fluid = true,
                    Checkbox = patrol,
                    Text = patrol ? "Enabled" : "Disabled",
                    Disabled = disabled,
                    OnClick = () => c.Act("toggle_patrol"),
                }),
                new UiNode(UiKind.LabeledItem) { Label = "Next patrol" }.With(new UiNode(UiKind.Button)
                {
                    Key = "next",
                    Fluid = true,
                    Text = next,
                    Icon = "pencil-alt",
                    Disabled = disabled,
                    OnClick = () => c.Act("set_patrol_next"),
                }),
                new UiNode(UiKind.LabeledItem) { Label = "Enable as Delivery Beacon" }.With(new UiNode(UiKind.Button)
                {
                    Key = "delivery",
                    Fluid = true,
                    Checkbox = delivery,
                    Text = delivery ? "Enabled" : "Disabled",
                    Disabled = disabled,
                    OnClick = () => c.Act("toggle_delivery"),
                }),
                new UiNode(UiKind.LabeledItem) { Label = "Delivery Direction" }.With(new UiNode(UiKind.Dropdown)
                {
                    Key = "direction",
                    Disabled = disabled,
                    Options = staticControls?.Field("direction_options")?.Items.Select(o => o.AsStr()).ToList(),
                    Text = controls?.Str("delivery_direction"),
                    OnSelected = value => c.Act("set_delivery_direction", ("direction", value)),
                })));
    }

    // tg: NavBeacon.tsx:124
    private static UiNode MaintenanceSection(TguiContext c, bool disabled)
    {
        UiValue controls = c.Data.Field("controls");
        bool coverLocked = controls?.Truthy("cover_locked") == true;
        bool hasCodes = c.Data.Field("static_controls")?.Truthy("has_codes") == true;
        return new UiNode(UiKind.Section) { Title = "Maintenance" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Reset codes" }.With(
                    hasCodes
                        ? new UiNode(UiKind.Button)
                        {
                            Key = "reset",
                            Fluid = true,
                            Text = "Reset",
                            Icon = "power-off",
                            Disabled = disabled,
                            OnClick = () => c.Act("reset_codes"),
                        }
                        : new UiNode(UiKind.Box) { Key = "reset", Text = "No backup codes found" }),
                new UiNode(UiKind.LabeledItem) { Label = "Maintenance hatch cover" }.With(new UiNode(UiKind.Button)
                {
                    Key = "cover",
                    Fluid = true,
                    Checkbox = coverLocked,
                    Text = coverLocked ? "Locked" : "Unlocked",
                    Disabled = disabled,
                    OnClick = () => c.Act("toggle_cover"),
                })));
    }
}

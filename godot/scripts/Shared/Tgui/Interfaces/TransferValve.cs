using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TransferValve.jsx (syndicate G4b, D316: the tank
// transfer valve, the blast cannon's bomb). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TransferValve
{
    // tg: tgui/packages/tgui/interfaces/TransferValve.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasTankOne = data.Truthy("tank_one");
        bool hasTankTwo = data.Truthy("tank_two");
        bool hasDevice = data.Truthy("attached_device");
        bool valve = data.Truthy("valve");

        // tg: TransferValve.jsx:10  <Window width={310} height={300}>
        return c.Window(310, 300, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: TransferValve.jsx:14
                    new UiNode(UiKind.LabeledItem) { Label = "Valve Status" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "toggle",
                            Icon = valve ? "unlock" : "lock",
                            Text = valve ? "Open" : "Closed",
                            Disabled = !hasTankOne || !hasTankTwo,
                            OnClick = () => c.Act("toggle"),
                        }))),
            // tg: TransferValve.jsx:24
            new UiNode(UiKind.Section)
            {
                Title = "Valve Attachment",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "device",
                    Text = "Configure",
                    Icon = "cog",
                    Disabled = !hasDevice,
                    OnClick = () => c.Act("device"),
                },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Attachment" }.With(
                        hasDevice
                            ? new UiNode(UiKind.Button)
                            {
                                Key = "remove_device",
                                Icon = "eject",
                                Text = data.Str("attached_device"),
                                OnClick = () => c.Act("remove_device"),
                            }
                            : new UiNode(UiKind.Box) { Color = "average", Text = "No Assembly" }))),
            // tg: TransferValve.jsx:50
            TankSection(c, "Attachment One", "tank_one", "tankone", hasTankOne),
            // tg: TransferValve.jsx:65
            TankSection(c, "Attachment Two", "tank_two", "tanktwo", hasTankTwo));
    }

    private static UiNode TankSection(TguiContext c, string title, string key, string action, bool hasTank)
    {
        return new UiNode(UiKind.Section) { Title = title }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Attachment" }.With(
                    hasTank
                        ? new UiNode(UiKind.Button)
                        {
                            Key = action,
                            Icon = "eject",
                            Text = c.Data.Str(key),
                            OnClick = () => c.Act(action),
                        }
                        : new UiNode(UiKind.Box) { Color = "average", Text = "No Tank" })));
    }
}

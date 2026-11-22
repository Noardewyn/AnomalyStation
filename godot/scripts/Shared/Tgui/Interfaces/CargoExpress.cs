using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CargoExpress.tsx (gaps P4d, D298: the express supply
// console): the lock, the landing location (the cargo bay, the linked beacon, a new beacon) and the cargo catalog in express mode.
// Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CargoExpress
{
    // tg: CargoExpress.tsx:31
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiNode content;
        if (data.Truthy("locked"))
        {
            content = new UiNode(UiKind.Section) { Key = "locked", Fill = true }.With(
                new UiNode(UiKind.Stack) { Key = "lockStack", Fill = true, Vertical = true, TextAlign = "center", Justify = "center" }.With(
                    new UiNode(UiKind.StackItem) { Key = "lockItem", Bold = true, Color = "red" }.With(
                        new UiNode(UiKind.Icon) { Key = "lockIcon", Icon = "lock", Size = 7.5 },
                        new UiNode(UiKind.Box) { Key = "lockText", Text = "Swipe a Cargo Technician-level ID card to unlock this interface." })));
        }
        else
        {
            bool alert = data.Truthy("beaconError") || !data.Truthy("canBeacon");
            content = new UiNode(UiKind.Stack) { Key = "express", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.NoticeBox) { Key = "message", Color = alert ? "red" : "blue", Text = data.Str("message") },
                new UiNode(UiKind.StackItem) { Key = "content", Grow = 1 }.With(Content(c)));
        }
        return c.Window(600, 700, null, content);
    }

    // tg: CargoExpress.tsx:63
    private static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        bool usingBeacon = data.Truthy("using_beacon");
        var location = new UiNode(UiKind.LabeledItem) { Key = "landing", Label = "Landing Location" }.With(
            new UiNode(UiKind.Button) { Key = "cargoBay", Selected = !usingBeacon, Text = "Cargo Bay", OnClick = () => c.Act("LZCargo") },
            new UiNode(UiKind.Button) { Key = "beacon", Selected = usingBeacon, Disabled = !data.Truthy("hasBeacon"), Tooltip = data.Str("beaconzone"), Text = data.Str("beaconName"), OnClick = () => c.Act("LZBeacon") },
            new UiNode(UiKind.Button) { Key = "printBeacon", Disabled = !data.Truthy("canBuyBeacon"), Text = data.Str("printMsg"), OnClick = () => c.Act("printBeacon") });
        return new UiNode(UiKind.Stack) { Key = "contentStack", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "head" }.With(new UiNode(UiKind.Section)
            {
                Key = "expressSection",
                Title = "Cargo Express",
                Buttons = new UiNode(UiKind.Box) { Key = "points", Bold = true, Text = TguiFormat.JsNumber(Math.Round(data.Num("points"), MidpointRounding.AwayFromZero)) + " credits" },
            }.With(new UiNode(UiKind.LabeledList) { Key = "list" }.With(location))),
            new UiNode(UiKind.StackItem) { Key = "catalog", Grow = 1 }.With(Cargo.Catalog(c, express: true)));
    }
}

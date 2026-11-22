using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Teleporter.jsx (gaps TP1): the teleporter control
// console's link state, regime, target and calibration (machinery/computer/teleporter.dm:61 ui_data).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Teleporter
{
    // tg: Teleporter.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: Teleporter.jsx:17  <Window width={360} height={130}>
        return c.Window(360, 130, null, new UiNode(UiKind.Section) { Key = "teleporter" }.With(Body(c, data)));
    }

    private static UiNode Body(TguiContext c, UiValue data)
    {
        if (!data.Truthy("power_station"))
            return new UiNode(UiKind.Box) { Key = "no_station", Color = "bad", TextAlign = "center", Text = "No power station linked." };
        if (!data.Truthy("teleporter_hub"))
            return new UiNode(UiKind.Box) { Key = "no_hub", Color = "bad", TextAlign = "center", Text = "No hub linked." };
        UiNode calibration = data.Truthy("calibrating")
            ? new UiNode(UiKind.Box) { Key = "state", Color = "average", Text = "In Progress" }
            : data.Truthy("calibrated")
                ? new UiNode(UiKind.Box) { Key = "state", Color = "good", Text = "Optimal" }
                : new UiNode(UiKind.Box) { Key = "state", Color = "bad", Text = "Sub-Optimal" };
        return new UiNode(UiKind.LabeledList) { Key = "list" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "regime", Label = "Regime" }.With(
                new UiNode(UiKind.Button) { Key = "regimeset", Text = data.Str("regime_set"), OnClick = () => c.Act("regimeset") }),
            new UiNode(UiKind.LabeledItem) { Key = "target", Label = "Target" }.With(
                new UiNode(UiKind.Button) { Key = "settarget", Icon = "edit", Text = data.Str("target"), OnClick = () => c.Act("settarget") }),
            new UiNode(UiKind.LabeledItem)
            {
                Key = "calibration",
                Label = "Calibration",
                Buttons = new UiNode(UiKind.Button) { Key = "calibrate", Icon = "tools", Text = "Calibrate", OnClick = () => c.Act("calibrate") },
            }.With(calibration));
    }
}

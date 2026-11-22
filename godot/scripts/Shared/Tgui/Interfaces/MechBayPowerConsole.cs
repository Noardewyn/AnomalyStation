using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MechBayPowerConsole.jsx (D321).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MechBayPowerConsole
{
    private static readonly (string, double, double)[] Ranges =
    {
        ("good", 0.7, double.PositiveInfinity),
        ("average", 0.3, 0.7),
        ("bad", double.NegativeInfinity, 0.3),
    };

    // tg: tgui/packages/tgui/interfaces/MechBayPowerConsole.jsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue port = c.Data.Field("recharge_port");
        UiValue mech = port.Field("mech");
        UiValue cell = mech.Field("cell");
        bool hasPort = port?.Truthy ?? false, hasMech = mech?.Truthy ?? false, hasCell = cell?.Truthy ?? false;

        UiNode Missing(bool needsCell)
            => !hasPort ? Notice("No power port detected. Please re-sync.")
             : !hasMech ? Notice("No mech detected.")
             : needsCell && !hasCell ? Notice("No cell is installed.")
             : null;

        // tg: MechBayPowerConsole.jsx:20  <Window width={400} height={200}>
        return c.Window(400, 200, null,
            new UiNode(UiKind.Section)
            {
                Title = "Mech status",
                TextAlign = "center",
                Buttons = new UiNode(UiKind.Button) { Key = "reconnect", Icon = "sync", Text = "Sync", OnClick = () => c.Act("reconnect") },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: MechBayPowerConsole.jsx:33
                    new UiNode(UiKind.LabeledItem) { Label = "Integrity" }.With(
                        Missing(false) ?? new UiNode(UiKind.ProgressBar) { Value = mech.Num("health") / mech.Num("maxhealth"), Ranges = Ranges }),
                    // tg: MechBayPowerConsole.jsx:48
                    new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                        Missing(true) ?? new UiNode(UiKind.ProgressBar)
                        {
                            Value = cell.Num("charge") / cell.Num("maxcharge"),
                            Ranges = Ranges,
                            Text = TguiFormat.FormatEnergy(cell.Num("charge")) + "/" + TguiFormat.FormatEnergy(cell.Num("maxcharge")),
                        }))));
    }

    private static UiNode Notice(string text) => new UiNode(UiKind.NoticeBox) { Text = text };
}

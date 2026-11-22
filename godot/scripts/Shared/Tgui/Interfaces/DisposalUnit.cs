using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DisposalUnit.tsx (D325: the disposal system).
// The window reads tg's ui_data of the disposal bin (disposal/bin.dm:527) and sends handle-0/1, pump-0/1 and eject.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class DisposalUnit
{
    // tg: DisposalUnit.tsx:20
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool flush = data.Truthy("flush"), fullPressure = data.Truthy("full_pressure"), charging = data.Truthy("pressure_charging");
        bool panelOpen = data.Truthy("panel_open"), isAi = data.Truthy("isai");
        // tg: DisposalUnit.tsx:26
        (string stateColor, string stateText) = fullPressure ? ("good", "Ready")
            : panelOpen ? ("bad", "Power Disabled")
            : charging ? ("average", "Pressurizing")
            : ("bad", "Off");
        // tg: DisposalUnit.tsx:40  <Window width={300} height={180}>
        return c.Window(300, 180, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "State", Color = stateColor, Text = stateText },
                    new UiNode(UiKind.LabeledItem) { Label = "Pressure" }.With(
                        new UiNode(UiKind.ProgressBar) { Key = "per", Value = data.Num("per"), Color = "good" }),
                    new UiNode(UiKind.LabeledItem) { Label = "Handle" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "handle",
                            Icon = flush ? "toggle-on" : "toggle-off",
                            Disabled = isAi || panelOpen,
                            Text = flush ? "Disengage" : "Engage",
                            OnClick = () => c.Act(flush ? "handle-0" : "handle-1"),
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = "Eject" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "eject",
                            Icon = "sign-out-alt",
                            Disabled = isAi,
                            Text = "Eject Contents",
                            OnClick = () => c.Act("eject"),
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = "Power" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "power",
                            Icon = "power-off",
                            Disabled = panelOpen,
                            Selected = charging,
                            OnClick = () => c.Act(charging ? "pump-0" : "pump-1"),
                        }))));
    }
}

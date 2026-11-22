using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/InfraredEmitter.tsx (gaps AS1): the infrared emitter
// assembly (infrared.dm:271).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class InfraredEmitter
{
    // tg: InfraredEmitter.tsx:12
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool on = data.Truthy("on");
        bool visible = data.Truthy("visible");

        // tg: InfraredEmitter.tsx:17  <Window width={225} height={110}>
        return c.Window(225, 110, null,
            new UiNode(UiKind.Section).With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Status" }.With(new UiNode(UiKind.Button)
                {
                    Key = "power",
                    Icon = on ? "power-off" : "times",
                    Text = on ? "On" : "Off",
                    Selected = on,
                    OnClick = () => c.Act("power"),
                }),
                new UiNode(UiKind.LabeledItem) { Label = "Visibility" }.With(new UiNode(UiKind.Button)
                {
                    Key = "visibility",
                    Icon = visible ? "eye" : "eye-slash",
                    Text = visible ? "Visible" : "Invisible",
                    Selected = visible,
                    OnClick = () => c.Act("visibility"),
                }))));
    }
}

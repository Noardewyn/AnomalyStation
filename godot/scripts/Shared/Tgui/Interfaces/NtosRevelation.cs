using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosRevelation.tsx (gaps S4c, D327): Revelation, the
// virus's new name, its arming switch and its ACTIVATE button. Button.Input is an input that commits its value. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosRevelation
{
    // tg: NtosRevelation.tsx:11
    public static UiNode Render(TguiContext c)
    {
        bool armed = c.Data.Truthy("armed");
        return NtosWindow.Render(c, 400, 250, null, new UiNode(UiKind.Section).With(
            new UiNode(UiKind.Input) { Key = "obfuscate", Fluid = true, Tooltip = "Obfuscate Name...", TextValue = "", OnCommit = value => c.Act("PRG_obfuscate", ("new_name", value)) },
            new UiNode(UiKind.LabeledList).With(new UiNode(UiKind.LabeledItem)
            {
                Key = "status",
                Label = "Payload Status",
                Buttons = new UiNode(UiKind.Button) { Key = "arm", Text = armed ? "ARMED" : "DISARMED", Color = armed ? "bad" : "average", OnClick = () => c.Act("PRG_arm") },
            }),
            new UiNode(UiKind.Button) { Key = "activate", Fluid = true, Bold = true, Text = "ACTIVATE", TextAlign = "center", Color = "bad", Disabled = !armed, OnClick = () => c.Act("PRG_activate") }));
    }
}

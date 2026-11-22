using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemDebugSynthesizer.tsx (gaps package P5). The window
// reads the debug chemical synthesizer's ui_data (chem_synthesizer.dm:32 over the dispenser's) and sends amount, temp, purity, input
// and makecup. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemDebugSynthesizer
{
    // tg: ChemDebugSynthesizer.tsx:18
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue beaker = data.Get("beaker");
        bool hasBeaker = beaker?.Truthy ?? false;
        UiNode Number(string key, string unit, double value, double max, string act) => new UiNode(UiKind.NumberInput)
        {
            Key = key,
            Value = value,
            Unit = unit,
            MinValue = key == "amount" ? 1 : 0,
            MaxValue = max,
            Step = 1,
            StepPixelSize = 2,
            OnChange = v => c.Act(act, ("amount", v)),
        };
        UiNode buttons = hasBeaker
            ? new UiNode(UiKind.Fragment).With(
                Number("amount", "u", data.Num("amount"), beaker.Num("maxVolume"), "amount"),
                Number("temp", "K", data.Num("temp"), 1000, "temp"),
                Number("purity", "%", data.Num("purity"), 120, "purity"),
                new UiNode(UiKind.Button) { Key = "input", Icon = "plus", Text = "Input", OnClick = () => c.Act("input") })
            : new UiNode(UiKind.Button) { Key = "makecup", Icon = "plus", Text = "Create Beaker", OnClick = () => c.Act("makecup") };

        // tg: ChemDebugSynthesizer.tsx:23  <Window width={390} height={330}><Window.Content scrollable>
        return c.Window(390, 330, null,
            new UiNode(UiKind.Section) { Key = "recipient", Title = "Recipient", Buttons = buttons }.With(BeakerDisplay.Display(c, beaker, showPH: true)));
    }
}

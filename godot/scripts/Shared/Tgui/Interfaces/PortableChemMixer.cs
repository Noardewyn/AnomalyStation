using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PortableChemMixer.tsx (gaps package P5). The window
// reads the portable chemical mixer's ui_data (portable_chem_mixer.dm:157: the amount, the chemicals of the locked containers, the
// beaker) and sends amount, dispense and remove. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PortableChemMixer
{
    // tg: PortableChemMixer.tsx:29
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue beaker = data.Get("beaker");
        IReadOnlyList<UiValue> beakerTransferAmounts = beaker?.Truthy ?? false ? beaker.Arr("transferAmounts") : Array.Empty<UiValue>();
        // tg: :33  sortBy(data.chemicals, [chem => chem.id])
        List<UiValue> chemicals = TguiFormat.SortBy(data.Arr("chemicals"), chem => chem.Str("id"));

        UiNode dispense = new UiNode(UiKind.Section)
        {
            Key = "dispense",
            Title = "Dispense Controls",
            Buttons = new UiNode(UiKind.Fragment).With(beakerTransferAmounts.Select(amount => new UiNode(UiKind.Button)
            {
                Key = "amount:" + TguiFormat.JsNumber(amount.AsNum()),
                Icon = "plus",
                Selected = amount.AsNum() == data.Num("amount"),
                Text = TguiFormat.JsNumber(amount.AsNum()),
                OnClick = () => c.Act("amount", ("target", amount.AsNum())),
            }).ToArray()),
        }.With(new UiNode(UiKind.Box) { Key = "chemicals" }.With(chemicals.Select(chemical => new UiNode(UiKind.Button)
        {
            Key = "chem:" + chemical.Str("id"),
            Icon = "tint",
            Fluid = true,
            Tooltip = "pH: " + TguiFormat.JsNumber(chemical.Num("pH")),
            Text = "(" + TguiFormat.JsNumber(chemical.Num("volume")) + ") " + chemical.Str("title"),
            OnClick = () => c.Act("dispense", ("reagent", chemical.Str("id"))),
        }).ToArray()));

        UiNode disposal = new UiNode(UiKind.Section)
        {
            Key = "disposal",
            Title = "Disposal Controls",
            Buttons = new UiNode(UiKind.Fragment).With(beakerTransferAmounts.Select(amount => new UiNode(UiKind.Button)
            {
                Key = "remove:" + TguiFormat.JsNumber(amount.AsNum()),
                Icon = "minus",
                Text = TguiFormat.JsNumber(amount.AsNum()),
                OnClick = () => c.Act("remove", ("amount", amount.AsNum())),
            }).ToArray()),
        }.With(BeakerDisplay.Display(c, beaker, showPH: true));

        // tg: PortableChemMixer.tsx:37  <Window width={500} height={500}><Window.Content scrollable>
        return c.Window(500, 500, null, dispense, disposal);
    }
}

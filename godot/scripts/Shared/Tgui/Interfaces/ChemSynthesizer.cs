using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemSynthesizer.tsx (engineering package E15, plumbing).
// The window reads tg's ui_data and ui_static_data of the chemical synthesizer (synthesizer.dm:67, :71) and sends amount and select.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemSynthesizer
{
    // tg: ChemSynthesizer.tsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double amount = data.Num("amount");
        string current_reagent = data.Str("current_reagent", null);
        // tg: ChemSynthesizer.tsx:24  <Window width={300} height={375}>
        return c.Window(300, 375, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.Box).With(
                    data.Arr("possible_amounts").Select(v =>
                    {
                        double possible_amount = v.AsNum();
                        string text = TguiFormat.ToFixed(possible_amount, 0);
                        return new UiNode(UiKind.Button)
                        {
                            Key = "amount-" + text,
                            Icon = "plus",
                            Text = text,
                            Selected = possible_amount == amount,
                            OnClick = () => c.Act("amount", ("target", possible_amount)),
                        };
                    })),
                new UiNode(UiKind.Box).With(
                    data.Arr("chemicals").Select(chemical =>
                    {
                        string id = chemical.Str("id");
                        return new UiNode(UiKind.Button)
                        {
                            Key = "chem-" + id,
                            Icon = "tint",
                            Text = chemical.Str("title"),
                            Width = "129px",
                            Selected = id == current_reagent,
                            OnClick = () => c.Act("select", ("reagent", id)),
                        };
                    }))));
    }
}

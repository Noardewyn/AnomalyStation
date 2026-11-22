using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BorgHypo.tsx (silicons SI2, D326: the cyborg hypospray's
// reagent picker). Engine-free (compiled into the tests too). The bar's two text cells (the name left, the volume right) are one text.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BorgHypo
{
    // tg: BorgHypo.tsx:26
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double maxVolume = data.Num("maxVolume");
        IReadOnlyList<UiValue> reagents = data.Arr("reagents");
        string selected = data.Str("selectedReagent");
        c.WindowTheme = data.Str("theme", null);
        double dynamicHeight = reagents.Count * 25 + 60;
        return c.Window(400, dynamicHeight, null, new UiNode(UiKind.Section) { Key = "reagents" }.With(ReagentDisplay(c, reagents, selected, maxVolume)));
    }

    // tg: BorgHypo.tsx:47
    private static UiNode ReagentDisplay(TguiContext c, IReadOnlyList<UiValue> reagents, string selected, double maxVolume)
    {
        if (reagents.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No reagents available!" };
        return new UiNode(UiKind.Fragment).With(reagents.Select(reagent =>
        {
            string name = reagent.Str("name");
            double volume = reagent.Num("volume");
            return new UiNode(UiKind.Stack) { Key = name, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "bar", Grow = 1 }.With(new UiNode(UiKind.ProgressBar)
                {
                    Key = "volume",
                    Value = maxVolume > 0 ? volume / maxVolume : 0,
                    MinValue = 0,
                    MaxValue = 1,
                    Text = name + "  " + TguiFormat.ToFixed(volume) + "u",
                }),
                new UiNode(UiKind.StackItem) { Key = "info" }.With(new UiNode(UiKind.Button)
                {
                    Key = "info",
                    Icon = "info-circle",
                    TextAlign = "center",
                    Tooltip = reagent.Str("description"),
                }),
                new UiNode(UiKind.StackItem) { Key = "select" }.With(new UiNode(UiKind.Button)
                {
                    Key = "select",
                    Icon = "syringe",
                    Color = name == selected ? "green" : "default",
                    Text = "Select",
                    TextAlign = "center",
                    OnClick = () => c.Act(name),
                }));
        }).ToArray());
    }
}

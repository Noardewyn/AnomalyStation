using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemReactionChamber.tsx (engineering package E15,
// plumbing). The window reads tg's ui_data of the reaction chamber (reaction_chamber.dm:73, :222) and sends temperature, acidic,
// alkaline, add, remove, catalyst and catremove. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Drawn as plain text: AnimatedNumber's count-up; the pH gauge's absolute position in its row is the row's.

public static class ChemReactionChamber
{
    // tg: ChemReactionChamber.tsx:113  the pH gauge's ranges
    private static readonly (string, double, double)[] PhRanges =
    {
        ("red", -0.22, 1.5), ("orange", 1.5, 3), ("yellow", 3, 4.5), ("olive", 4.5, 5), ("good", 5, 6),
        ("green", 6, 8.5), ("teal", 8.5, 9.5), ("blue", 9.5, 11), ("purple", 11, 12.5), ("violet", 12.5, 14),
    };

    private const string PhTooltip = @"
                        In chemistry, pH is a scale used to specify
                        the acidity or basicity of an aqueous solution.
                        Acidic solutions are measured to have lower
                        pH values than basic or alkaline solutions.
                        The pH scale is logarithmic and inversely
                        indicates the concentration of hydrogen ions
                        in the solution.";

    private const string CatalystTooltip = @"
                                This button converts this reagent entry into a catalyst.
                                Catalyst reagents are not removed from the reaction chamber
                                on completion. Useful for certain reactions.";

    private static UiNode Limit(TguiContext c, string label, string key, string action, double value) =>
        new UiNode(UiKind.LabeledItem) { Label = label }.With(
            new UiNode(UiKind.NumberInput)
            {
                Key = key,
                Value = value,
                MinValue = 0,
                MaxValue = 14,
                Step = 1,
                StepPixelSize = 3,
                Width = "39px",
                OnChange = v => c.Act(action, ("target", v)),
            });

    // tg: ChemReactionChamber.tsx:31
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double ph = data.Num("ph");
        IReadOnlyList<UiValue> reagents = data.Arr("reagents");
        IReadOnlyList<UiValue> catalysts = data.Arr("catalysts");
        // tg: ChemReactionChamber.tsx:48  <Window width={290} height={570}>
        return c.Window(290, 570, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Section) { Title = "Conditions", Buttons = ChemMixingChamber.TargetButtons(c) }.With(
                        new UiNode(UiKind.Stack) { Vertical = true }.With(
                            new UiNode(UiKind.StackItem).With(
                                new UiNode(UiKind.Stack) { Fill = true }.With(
                                    ChemMixingChamber.CurrentTemperature(c),
                                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                                        new UiNode(UiKind.RoundGauge)
                                        {
                                            Key = "ph-gauge",
                                            Value = ph,
                                            MinValue = 0,
                                            MaxValue = 14,
                                            Format = _ => "",
                                            Size = 1.5,
                                            Ranges = PhRanges,
                                        }))),
                            new UiNode(UiKind.StackItem).With(
                                new UiNode(UiKind.Stack) { Fill = true }.With(
                                    ChemMixingChamber.TextItem("ph:", "label"),
                                    ChemMixingChamber.TextItem(TguiFormat.JsNumber(ph), grow: 15, key: "ph"),
                                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                                        new UiNode(UiKind.Button) { Key = "ph-help", Color = "transparent", Icon = "question", Tooltip = PhTooltip })))))),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Title = "Settings", Fill = true, Scrollable = true, Buttons = ChemMixingChamber.StateBox(c) }.With(
                        new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                            new UiNode(UiKind.StackItem).With(
                                new UiNode(UiKind.LabeledList).With(
                                    Limit(c, "Acidic pH limit", "acidic", "acidic", data.Num("reagentAcidic")),
                                    Limit(c, "Alkaline pH limit", "alkaline", "alkaline", data.Num("reagentAlkaline")))),
                            new UiNode(UiKind.StackItem).With(ChemMixingChamber.AddRow(c)),
                            new UiNode(UiKind.StackItem).With(
                                new UiNode(UiKind.Stack) { Vertical = true }.With(
                                    reagents.Select(reagent => ChemMixingChamber.ReagentRow(reagent, "reagent-",
                                        new UiNode(UiKind.Button)
                                        {
                                            Key = "catalyst-" + reagent.Str("name"),
                                            Color = "transparent",
                                            Tooltip = CatalystTooltip,
                                            Text = "C",
                                            OnClick = () => c.Act("catalyst", ("chem", reagent.Str("name"))),
                                        },
                                        new UiNode(UiKind.Button)
                                        {
                                            Key = "remove-" + reagent.Str("name"),
                                            Icon = "minus",
                                            Color = "bad",
                                            OnClick = () => c.Act("remove", ("chem", reagent.Str("name"))),
                                        }))))))),
                new UiNode(UiKind.StackItem) { Grow = 0.7 }.With(
                    new UiNode(UiKind.Section) { Title = "Catalysts", Fill = true, Scrollable = true }.With(
                        new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                            catalysts.Select(reagent => ChemMixingChamber.ReagentRow(reagent, "catalyst-row-",
                                new UiNode(UiKind.Button)
                                {
                                    Key = "catremove-" + reagent.Str("name"),
                                    Color = "bad",
                                    Text = "C",
                                    OnClick = () => c.Act("catremove", ("chem", reagent.Str("name"))),
                                })))))));
    }
}

using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemMixingChamber.tsx (engineering package E15,
// plumbing). The window reads tg's ui_data of the mixing chamber (reaction_chamber.dm:73) and sends temperature, add (with the amount
// field's local value) and remove. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Drawn as plain text: AnimatedNumber's count-up (the current temperature shows its value at once).

public static class ChemMixingChamber
{
    // a Stack.Item holding text (tgui's <Stack.Item textColor="label">text</Stack.Item>)
    public static UiNode TextItem(string text, string color = null, double grow = 0, string key = null) =>
        new UiNode(UiKind.StackItem) { Grow = grow }.With(new UiNode(UiKind.Box) { Key = key, Color = color, Text = text });

    // tg: ChemMixingChamber.tsx:43  the Conditions section's Target field (shared with ChemReactionChamber.tsx:52)
    public static UiNode TargetButtons(TguiContext c) =>
        new UiNode(UiKind.Stack).With(
            TextItem("Target:"),
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.NumberInput)
                {
                    Key = "target",
                    Width = "65px",
                    Unit = "K",
                    Step = 10,
                    StepPixelSize = 3,
                    Value = TguiFormat.Round(c.Data.Num("targetTemp"), 0.1),
                    MinValue = 0,
                    MaxValue = 1000,
                    OnChange = value => c.Act("temperature", ("target", value)),
                }));

    // tg: ChemMixingChamber.tsx:70  "Current Temperature:" and the AnimatedNumber of toFixed(value) K
    public static UiNode CurrentTemperature(TguiContext c) =>
        new UiNode(UiKind.Fragment).With(
            TextItem("Current Temperature:", "label"),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Box) { Key = "temperature", Text = TguiFormat.ToFixed(c.Data.Num("temperature")) + " K" }));

    // tg: ChemMixingChamber.tsx:88  the Settings section's state: Reacting in purple, else Emptying (bad) or Filling (good)
    public static UiNode StateBox(TguiContext c)
    {
        bool emptying = c.Data.Truthy("emptying");
        return c.Data.Truthy("isReacting")
            ? new UiNode(UiKind.Box) { Key = "state", Bold = true, Color = "purple", Text = "Reacting" }
            : new UiNode(UiKind.Box) { Key = "state", FontSize = "16px", Bold = true, Color = emptying ? "bad" : "good", Text = emptying ? "Emptying" : "Filling" };
    }

    // tg: ChemMixingChamber.tsx:107  the Add Reagent button and the amount it asks for (useState(1))
    public static UiNode AddRow(TguiContext c)
    {
        double reagentQuantity = c.UseState("reagentQuantity", 1.0);
        return new UiNode(UiKind.Stack) { Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "add",
                    Text = "Add Reagent",
                    Color = "good",
                    Icon = "plus",
                    OnClick = () => c.Act("add", ("amount", reagentQuantity)),
                }),
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.NumberInput)
                {
                    Key = "quantity",
                    Value = reagentQuantity,
                    MinValue = 1,
                    MaxValue = 100,
                    Step = 1,
                    StepPixelSize = 3,
                    Width = "39px",
                    OnChange = value => c.SetState("reagentQuantity", value),
                }));
    }

    // tg: ChemMixingChamber.tsx:137  a reagent's name, its volume and the buttons after them
    public static UiNode ReagentRow(UiValue reagent, string keyPrefix, params UiNode[] buttons) =>
        new UiNode(UiKind.StackItem) { Key = keyPrefix + reagent.Str("name") }.With(
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new[]
                {
                    TextItem(reagent.Str("name") + ":", "label"),
                    TextItem(TguiFormat.JsNumber(reagent.Num("volume")), grow: 1, key: keyPrefix + "volume-" + reagent.Str("name")),
                }.Concat(buttons.Select(b => new UiNode(UiKind.StackItem).With(b)))));

    // tg: ChemMixingChamber.tsx:29
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> reagents = c.Data.Arr("reagents");
        // tg: ChemMixingChamber.tsx:38  <Window width={290} height={400}>
        return c.Window(290, 400, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Section) { Title = "Conditions", Buttons = TargetButtons(c) }.With(
                        new UiNode(UiKind.Stack) { Vertical = true }.With(
                            new UiNode(UiKind.StackItem).With(
                                new UiNode(UiKind.Stack) { Fill = true }.With(CurrentTemperature(c)))))),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Title = "Settings", Fill = true, Scrollable = true, Buttons = StateBox(c) }.With(
                        new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                            new UiNode(UiKind.StackItem).With(AddRow(c)),
                            new UiNode(UiKind.StackItem).With(
                                new UiNode(UiKind.Stack) { Vertical = true }.With(
                                    reagents.Select(reagent => ReagentRow(reagent, "reagent-",
                                        new UiNode(UiKind.Button)
                                        {
                                            Key = "remove-" + reagent.Str("name"),
                                            Icon = "minus",
                                            Color = "bad",
                                            OnClick = () => c.Act("remove", ("chem", reagent.Str("name"))),
                                        })))))))));
    }
}

using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DnaVault.tsx (gaps R1: the DNA vault). The window reads
// the vault's ui_data (dna_vault.dm:145: the human, plant and animal samples against their goals, completed, used and the two gene
// therapy choices) and sends gene with the chosen mutation's name. The kit has no margin (the title box's mb={1}). Original copyright and
// license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class DnaVault
{
    // tg: DnaVault.tsx:27
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double animalsMax = data.Num("animals_max");
        double animals = data.Num("animals");
        string choiceA = data.Str("choiceA");
        string choiceB = data.Str("choiceB");
        bool completed = data.Truthy("completed");
        double dnaMax = data.Num("dna_max");
        double dna = data.Num("dna");
        double plantsMax = data.Num("plants_max");
        double plants = data.Num("plants");
        bool used = data.Truthy("used");

        // tg: DnaVault.tsx:43  <Window width={350} height={400}>
        return c.Window(350, 400, null,
            // tg: DnaVault.tsx:45
            new UiNode(UiKind.Section) { Key = "database", Title = "DNA Vault Database" }.With(
                new UiNode(UiKind.LabeledList).With(
                    Samples("dna", "Human DNA", dna, dnaMax),
                    Samples("plants", "Plant DNA", plants, plantsMax),
                    Samples("animals", "Animal DNA", animals, animalsMax))),
            // tg: DnaVault.tsx:64  {!!(completed && !used) && (...)}
            completed && !used
                ? new UiNode(UiKind.Section) { Key = "therapy", Title = "Personal Gene Therapy" }.With(
                    // tg: DnaVault.tsx:66
                    new UiNode(UiKind.Box) { Key = "therapy-title", Bold = true, TextAlign = "center", Text = "Applicable Gene Therapy Treatments" },
                    // tg: DnaVault.tsx:69
                    new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(Choice(c, "choiceA", choiceA)),
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(Choice(c, "choiceB", choiceB))))
                : null);
    }

    // tg: DnaVault.tsx:47-61  (<LabeledList.Item label><ProgressBar value={n / max}>{`${n} / ${max} Samples`}</ProgressBar>)
    private static UiNode Samples(string key, string label, double count, double max)
    {
        return new UiNode(UiKind.LabeledItem) { Label = label }.With(
            new UiNode(UiKind.ProgressBar)
            {
                Key = key,
                Value = max != 0 ? count / max : 0,
                Text = TguiFormat.JsNumber(count) + " / " + TguiFormat.JsNumber(max) + " Samples",
            });
    }

    // tg: DnaVault.tsx:71-96
    private static UiNode Choice(TguiContext c, string key, string choice)
    {
        return new UiNode(UiKind.Button)
        {
            Key = key,
            Fluid = true,
            Bold = true,
            TextAlign = "center",
            Text = choice,
            OnClick = () => c.Act("gene", ("choice", choice)),
        };
    }
}

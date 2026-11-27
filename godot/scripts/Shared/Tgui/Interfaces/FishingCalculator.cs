using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/FishingCalculator.tsx. The admins' fishing calculator:
// a fishing spot, a rod, a hook, a line and a bait typed in, then each catch of the spot with its weight, chance, difficulty and count.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class FishingCalculator
{
    // tg: FishingCalculator.tsx:23
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string[] spot_types = data.Arr("spot_types").Select(v => v.AsStr()).ToArray();
        string[] rod_types = data.Arr("rod_types").Select(v => v.AsStr()).ToArray();
        string[] hook_types = data.Arr("hook_types").Select(v => v.AsStr()).ToArray();
        string[] line_types = data.Arr("line_types").Select(v => v.AsStr()).ToArray();

        string bait = c.UseState("bait", "/obj/item/food/bait/worm");
        string spot = c.UseState("spot", spot_types.FirstOrDefault());
        string rod = c.UseState("rod", rod_types.FirstOrDefault());
        string hook = c.UseState("hook", hook_types.FirstOrDefault());
        string line = c.UseState("line", line_types.FirstOrDefault());

        var info = data.Arr("info");
        // data.info?.reduce((s, w) => s + w.weight, 0) || 1
        double weight_sum = info.Sum(w => w.Num("weight"));
        if (weight_sum == 0)
            weight_sum = 1;

        var table = new UiNode(UiKind.Table) { Key = "results" }.With(
            new UiNode(UiKind.TableRow) { Key = "header" }.With(
                HeaderCell("outcome", "Outcome"),
                HeaderCell("weight", "Weight"),
                HeaderCell("chance", "Probabilty"),
                HeaderCell("difficulty", "Difficulty"),
                HeaderCell("count", "Count")));
        int index = 0;
        foreach (UiValue result in info)
        {
            table.With(new UiNode(UiKind.TableRow) { Key = "result-" + index++ + "-" + result.Str("result") }.With(
                new UiNode(UiKind.TableCell) { Key = "outcome", Text = result.Str("result") },
                new UiNode(UiKind.TableCell) { Key = "weight", Text = result.Str("weight") },
                new UiNode(UiKind.TableCell) { Key = "chance", Text = TguiFormat.JsNumber(TguiFormat.Round(result.Num("weight") / weight_sum * 100, 2)) + "%" },
                new UiNode(UiKind.TableCell) { Key = "difficulty", Text = result.Str("difficulty") },
                new UiNode(UiKind.TableCell) { Key = "count", Text = result.Str("count") }));
        }

        // tg: FishingCalculator.tsx:35  <Window width={500} height={500}>
        return c.Window(500, 500, null,
            new UiNode(UiKind.Stack) { Key = "root", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "inputs" }.With(
                    Picker(c, "spot", spot_types, spot),
                    Picker(c, "rod", rod_types, rod),
                    Picker(c, "hook", hook_types, hook),
                    Picker(c, "line", line_types, line),
                    new UiNode(UiKind.Input)
                    {
                        Key = "bait",
                        TextValue = bait,
                        Tooltip = "Bait",
                        Fluid = true,
                        OnInput = value => c.SetState("bait", value ?? ""),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "calculate",
                        Text = "Calculate",
                        OnClick = () => c.Act("recalc", ("rod", rod), ("bait", bait), ("hook", hook), ("line", line), ("spot", spot)),
                    }),
                new UiNode(UiKind.StackItem) { Key = "table" }.With(table)));
    }

    // <Table.Row header>: tgui-core draws a header row's cells bold
    private static UiNode HeaderCell(string key, string text) => new UiNode(UiKind.TableCell) { Key = key, Text = text, Bold = true };

    private static UiNode Picker(TguiContext c, string key, string[] options, string selected) => new UiNode(UiKind.Dropdown)
    {
        Key = key,
        Width = "100%",
        Options = options,
        TextValue = selected,
        OnSelected = value => c.SetState(key, value),
    };
}

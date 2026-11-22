using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ModularShieldConsole.tsx (engineering package E10). The
// window reads tg's ui_data of the modular shield control console (computer/modular_shield.dm:41): one row per linked generator with
// its name field (rename), its strength and regeneration and its power button (toggle_shields). The cells' collapsing (shrink to
// their content) is not drawn, as NtosSupermatter's. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ModularShieldConsole
{
    // tg: tgui/packages/tgui/interfaces/ModularShieldConsole.tsx:27
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> generators = c.Data.Get("generators")?.Items ?? Array.Empty<UiValue>();
        // tg: ModularShieldConsole.tsx:31  <Window title="Modular Shield Console" width={450} height={275}> <Window.Content scrollable>
        return c.Window(450, 275, "Modular Shield Console",
            generators.Count == 0
                ? new UiNode(UiKind.NoticeBox) { Text = "No Generators Connected" }
                : new UiNode(UiKind.Section) { Height = "200px" }.With(GeneratorTable(c, generators)));
    }

    // tg: ModularShieldConsole.tsx:45
    private static UiNode GeneratorTable(TguiContext c, IReadOnlyList<UiValue> generators)
    {
        UiNode table = new UiNode(UiKind.Table).With(
            new UiNode(UiKind.TableRow) { Key = "head" }.With(
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Name" },
                new UiNode(UiKind.TableCell) { Bold = true, TextAlign = "center", Text = "Status" },
                new UiNode(UiKind.TableCell) { Bold = true, TextAlign = "center", Text = "Toggle" }));
        foreach (UiValue stat in generators)
            table.With(GeneratorTableEntry(c, stat));
        return table;
    }

    // tg: ModularShieldConsole.tsx:70
    private static UiNode GeneratorTableEntry(TguiContext c, UiValue generator)
    {
        string name = generator.Str("name");
        double id = generator.Num("id");
        double maxStrength = generator.Num("max_strength");
        double currentStrength = generator.Num("current_strength");
        bool active = generator.Truthy("active");
        bool recovering = generator.Truthy("recovering");
        double currentRegeneration = generator.Num("current_regeneration");
        string key = "generator:" + TguiFormat.JsNumber(id);

        return new UiNode(UiKind.TableRow) { Key = key, Candystripe = true }.With(
            new UiNode(UiKind.TableCell).With(
                new UiNode(UiKind.Input)
                {
                    Key = key + ":name",
                    TextValue = name,
                    Width = "170px",
                    OnCommit = value => c.Act("rename", ("id", id), ("name", value)),
                }),
            // tg: ModularShieldConsole.tsx:96
            new UiNode(UiKind.TableCell) { TextAlign = "center", Color = recovering ? "red" : "white" }.With(
                new UiNode(UiKind.ProgressBar)
                {
                    Key = key + ":strength",
                    Width = "170px",
                    Value = currentStrength,
                    MinValue = 0,
                    MaxValue = maxStrength,
                    Ranges = new[]
                    {
                        ("good", maxStrength * 0.75, maxStrength),
                        ("average", maxStrength * 0.25, maxStrength * 0.75),
                        ("bad", 0.0, maxStrength * 0.25),
                    },
                    Text = $"{TguiFormat.JsNumber(currentStrength)}/{TguiFormat.JsNumber(maxStrength)} + {TguiFormat.JsNumber(currentRegeneration)}",
                }),
            // tg: ModularShieldConsole.tsx:116
            new UiNode(UiKind.TableCell).With(
                new UiNode(UiKind.Button)
                {
                    Key = key + ":power",
                    Bold = true,
                    Disabled = recovering,
                    Selected = active,
                    Text = active ? "On" : "Off",
                    Icon = "power-off",
                    OnClick = () => c.Act("toggle_shields", ("id", id)),
                }));
    }
}

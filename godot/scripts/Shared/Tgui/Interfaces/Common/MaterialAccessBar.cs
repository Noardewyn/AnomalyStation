using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Fabrication/MaterialAccessBar.tsx and MaterialIcon.tsx
// (Lavaland L2a, D304), the bar of materials under the ore silo's window (and the fabricators' when they come). The native window has
// no hover, so each material's eject dock (x5, x10, x25, x50) is always shown under its count; a click on the count ejects one, as tg's.
// The icons are tg's "sheetmaterials" sheet, icons/obj/stack_objects.dmi inserted whole, drawn from the client's own copy (DV44).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MaterialAccessBar
{
    // tg: MaterialAccessBar.tsx:11  "by popular demand of discord people (who are always right and never wrong) this is completely made up"
    private static readonly Dictionary<string, int> MaterialRarity = new(StringComparer.Ordinal)
    {
        ["glass"] = 0, ["iron"] = 1, ["plastic"] = 2, ["titanium"] = 3, ["plasma"] = 4, ["silver"] = 5,
        ["gold"] = 6, ["uranium"] = 7, ["diamond"] = 8, ["bluespace crystal"] = 9, ["bananium"] = 10,
    };

    // tg: MaterialIcon.tsx:5  the stack's picture by its size
    private static readonly Dictionary<string, (int Sheets, string State)[]> MaterialIcons = new(StringComparer.Ordinal)
    {
        ["iron"] = Stack("sheet-metal"),
        ["glass"] = Stack("sheet-glass"),
        ["silver"] = Stack("sheet-silver"),
        ["gold"] = Stack("sheet-gold"),
        ["diamond"] = Stack("sheet-diamond"),
        ["plasma"] = Stack("sheet-plasma"),
        ["uranium"] = Stack("sheet-uranium"),
        ["bananium"] = Stack("sheet-bananium"),
        ["titanium"] = Stack("sheet-titanium"),
        ["bluespace crystal"] = new[] { (0, "bluespace_crystal") },
        ["plastic"] = Stack("sheet-plastic"),
    };

    private static (int, string)[] Stack(string state) => new[] { (0, state), (17, state + "_2"), (34, state + "_3") };

    private const string SheetFile = "icons/obj/stack_objects.dmi";

    /// <summary>tg: MaterialAccessBar.tsx:53  the materials by rarity; <paramref name="onEject"/> gets the material's ref and the sheets.</summary>
    public static UiNode Render(IReadOnlyList<UiValue> availableMaterials, double sheetMaterialAmount, Action<string, int> onEject)
    {
        var bar = new UiNode(UiKind.Stack) { Key = "materialBar", Fill = true };
        foreach (UiValue material in availableMaterials.OrderBy(m => MaterialRarity.TryGetValue(m.Str("name"), out int r) ? r : int.MaxValue))
            bar.With(new UiNode(UiKind.StackItem) { Key = "mat:" + material.Str("name"), Grow = 1 }.With(MaterialCounter(material, sheetMaterialAmount, onEject)));
        return bar;
    }

    // tg: MaterialAccessBar.tsx:80
    private static UiNode MaterialCounter(UiValue material, double sheetMaterialAmount, Action<string, int> onEject)
    {
        double sheets = sheetMaterialAmount > 0 ? material.Num("amount") / sheetMaterialAmount : 0;
        string @ref = material.Str("ref");
        var dock = new UiNode(UiKind.Stack) { Key = "dock", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "icon" }.With(MaterialIcon(material.Str("name"), sheets)),
            new UiNode(UiKind.StackItem) { Key = "label" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "one",
                    Fluid = true,
                    Color = "transparent",
                    TextAlign = "center",
                    Disabled = sheets < 1,
                    Text = TguiFormat.FormatSiUnit(sheets, 0),
                    OnClick = () => onEject(@ref, 1),
                }));
        foreach (int amount in new[] { 5, 10, 25, 50 })
            dock.With(new UiNode(UiKind.StackItem) { Key = "x" + amount }.With(new UiNode(UiKind.Button)
            {
                Key = "eject",
                Fluid = true,
                Color = "transparent",
                Opacity = amount > sheets ? 0.5 : 1,
                Text = "×" + amount,
                OnClick = () => onEject(@ref, amount),
            }));
        return dock;
    }

    // tg: MaterialIcon.tsx:74
    public static UiNode MaterialIcon(string materialName, double sheets)
    {
        if (!MaterialIcons.TryGetValue(materialName, out var icons))
            return new UiNode(UiKind.Icon) { Key = "unknown", Icon = "question-circle" };
        int activeIdx = 0;
        while (activeIdx + 1 < icons.Length && icons[activeIdx + 1].Sheets <= sheets)
            activeIdx += 1;
        return new UiNode(UiKind.Box) { Key = "pic", DmIcon = SheetFile, DmIconState = icons[activeIdx].State };
    }
}

using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ProcessingConsole.tsx (Lavaland L2b, D304).
// The window reads tg's ui_data and ui_static_data of the furnace (machine_processing.dm:180, :205) and sends setMaterial,
// setAlloy and toggle. A material's picture is its sheet's icon and state, drawn from the client's copy of the .dmi, where
// tg's is an icon2base64 picture (DV257). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ProcessingConsole
{
    // tg: ProcessingConsole.tsx:40
    public static UiNode Render(TguiContext c)
    {
        bool state = c.Data.Truthy("state");
        // tg: ProcessingConsole.tsx:45  <Window title="Processing Unit Console" width={580} height={500}>
        return c.Window(580, 500, "Processing Unit Console",
            new UiNode(UiKind.Stack) { Key = "root", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "lists", Grow = 1 }.With(
                    new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
                        new UiNode(UiKind.StackItem) { Key = "materials", Grow = 1.2 }.With(
                            new UiNode(UiKind.Section) { Key = "materials", Fill = true, TextAlign = "center", Title = "Materials" }.With(MaterialSelection(c))),
                        new UiNode(UiKind.StackItem) { Key = "alloys", Grow = 1 }.With(
                            new UiNode(UiKind.Section) { Key = "alloys", Fill = true, TextAlign = "center", Title = "Alloys" }.With(AlloySelection(c))))),
                new UiNode(UiKind.StackItem) { Key = "toggle" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "toggle",
                        Icon = "arrows-spin",
                        Spin = state,
                        Color = state ? "bad" : "good",
                        TextAlign = "center",
                        FontSize = 1.25,
                        Fluid = true,
                        Bold = true,
                        Text = state ? "Deactivate" : "Activate",
                        OnClick = () => c.Act("toggle"),
                    })));
    }

    // tg: ProcessingConsole.tsx:82
    private static UiNode MaterialSelection(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> materials = data.Arr("materials");
        IReadOnlyList<UiValue> materialIcons = data.Arr("materialIcons");
        string selectedMaterial = data.Str("selectedMaterial", null);
        double sheet = data.Num("SHEET_MATERIAL_AMOUNT");
        if (materials.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Color = "danger", Text = "No material recipes found!" };
        var table = new UiNode(UiKind.Table) { Key = "list" };
        foreach (UiValue material in materials)
        {
            string @ref = material.Str("ref");
            UiValue icon = materialIcons.FirstOrDefault(i => i.Str("id") == @ref);
            table.With(DisplayRow("mat:" + material.Str("name"), material.Str("name"), icon, Math.Floor(sheet > 0 ? material.Num("amount") / sheet : 0),
                selectedMaterial == material.Str("name"), () => c.Act("setMaterial", ("value", @ref))));
        }
        return table;
    }

    // tg: ProcessingConsole.tsx:106
    private static UiNode AlloySelection(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> alloys = data.Arr("alloys");
        IReadOnlyList<UiValue> alloyIcons = data.Arr("alloyIcons");
        string selectedAlloy = data.Str("selectedAlloy", null);
        if (alloys.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Color = "danger", Text = "No alloy recipes found!" };
        var table = new UiNode(UiKind.Table) { Key = "list" };
        foreach (UiValue alloy in alloys)
        {
            string path = alloy.Str("path");
            UiValue icon = alloyIcons.FirstOrDefault(i => i.Str("id") == path);
            table.With(DisplayRow("alloy:" + path, alloy.Str("name"), icon, null, selectedAlloy == path, () => c.Act("setAlloy", ("new_alloy", path))));
        }
        return table;
    }

    // tg: ProcessingConsole.tsx:139
    private static UiNode DisplayRow(string key, string name, UiValue icon, double? amount, bool selected, Action onSelect)
    {
        var row = new UiNode(UiKind.TableRow) { Key = key, Candystripe = true }.With(
            new UiNode(UiKind.TableCell) { Key = "icon" }.With(icon != null
                ? new UiNode(UiKind.Box) { Key = "pic", DmIcon = icon.Str("icon"), DmIconState = icon.Str("icon_state"), Width = "24px", Height = "24px" }
                : new UiNode(UiKind.Icon) { Key = "unknown", Icon = "circle-question" }),
            new UiNode(UiKind.TableCell) { Key = "name", TextAlign = "left", Text = TguiFormat.ToTitleCase(name) });
        if (amount is double sheets)
            row.With(new UiNode(UiKind.Box) { Key = "amount", Color = "label", Text = TguiFormat.FormatSiUnit(sheets, 0) + " " + (sheets == 1 ? "sheet" : "sheets") });
        return row.With(new UiNode(UiKind.TableCell) { Key = "select", TextAlign = "right" }.With(
            new UiNode(UiKind.Button) { Key = "check", Color = "transparent", Checkbox = selected, OnClick = onSelect }));
    }
}

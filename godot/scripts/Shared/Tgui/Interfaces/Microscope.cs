using System.Collections.Generic;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Microscope.tsx.
// Cytology's microscope: the window reads tg's ui_data of the microscope (microscope.dm:21), a list of the petri dish's cell lines
// and viruses, and sends eject_petridish. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Microscope
{
    // tg: Microscope.tsx:35
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasDish = data.Truthy("has_dish");
        IReadOnlyList<UiValue> cellLines = data.Get("cell_lines")?.Items ?? new List<UiValue>();
        // tg: Microscope.tsx:40  <Window width={620} height={620}> <Window.Content scrollable>: the content in the window's own area
        return c.Window(620, 620, null,
            new UiNode(UiKind.Section)
            {
                Key = "dish",
                Title = hasDish ? "Petri Dish Sample" : "No Petri Dish",
                Buttons = hasDish
                    ? new UiNode(UiKind.Button)
                    {
                        Key = "eject",
                        Icon = "eject",
                        Text = "Take Dish",
                        OnClick = () => c.Act("eject_petridish"),
                    }
                    : null,
            }.With(CellList(cellLines)));
    }

    // tg: Microscope.tsx:63  CellList
    private static UiNode[] CellList(IReadOnlyList<UiValue> cellLines)
    {
        if (cellLines.Count == 0)
            return new[] { new UiNode(UiKind.NoticeBox) { Key = "empty", Text = "No micro-organisms found" } };
        var nodes = new List<UiNode>();
        for (int i = 0; i < cellLines.Count; i++)
        {
            UiValue line = cellLines[i];
            string key = "line-" + i;
            nodes.Add(line.Str("type") != "virus" ? CellLine(line, key) : Virus(line, key));
        }
        return nodes.ToArray();
    }

    // tg: Microscope.tsx:73  one cell line: its grown mob's icon, its rates and the three reagent columns
    private static UiNode CellLine(UiValue line, string key)
    {
        var text = new List<UiNode>
        {
            new UiNode(UiKind.Box)
            {
                Key = "rates",
                Text = $"Consume {Format(line.Num("consumption_rate"))} units of every nutrient per second to grow by {Format(line.Num("growth_rate"))}%.",
            },
        };
        double suspectibility = line.Num("suspectibility");
        if (suspectibility > 0)
        {
            text.Add(new UiNode(UiKind.Box)
            {
                Key = "virus",
                Text = $"Reduced by {Format(suspectibility)}% when infected with viruses.",
            });
        }
        text.Add(new UiNode(UiKind.Stack) { Key = "reagents", Fill = true }.With(
            ReagentColumn("requireds", "Required Reagents", line.Get("requireds"), null, _ => null),
            ReagentColumn("supplementaries", "Supplements", line.Get("supplementaries"), "good", v => $"+{Format(v)}% growth/sec."),
            ReagentColumn("suppressives", "Supressives", line.Get("suppressives"), "bad", v => $"{Format(v)}% growth/sec.")));

        return new UiNode(UiKind.Stack) { Key = key }.With(
            new UiNode(UiKind.StackItem) { Key = "icon" }.With(
                new UiNode(UiKind.Box)
                {
                    Key = "sprite",
                    DmIcon = line.Str("icon"),
                    DmIconState = line.Str("icon_state"),
                    Size = 2,
                }),
            new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(
                new UiNode(UiKind.Section)
                {
                    Key = "section",
                    Title = line.Str("desc"),
                    Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "help",
                        Color = "transparent",
                        Icon = "circle-question",
                        Tooltip = "Put the sample into a Growing Vat and pour the required reagents.",
                    },
                }.With(text.ToArray())));
    }

    // tg: Microscope.tsx:147  a virus in the dish
    private static UiNode Virus(UiValue line, string key)
    {
        return new UiNode(UiKind.Stack) { Key = key }.With(
            new UiNode(UiKind.StackItem) { Key = "icon" }.With(
                new UiNode(UiKind.Icon) { Key = "viruses", Icon = "viruses", Color = "bad", Size = 4 }),
            new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "section", Title = line.Str("desc") }.With(
                    new UiNode(UiKind.Box)
                    {
                        Key = "text",
                        Text = "Reduces growth of other cell lines when not suppressed by Spaceacillin.",
                    })));
    }

    // tg: Microscope.tsx:103  one of the three columns: a title between dividers, then a button per reagent
    private static UiNode ReagentColumn(string key, string title, UiValue reagents, string color, System.Func<double, string> tooltip)
    {
        var children = new List<UiNode> { GroupTitle(title) };
        if (reagents != null)
        {
            foreach (KeyValuePair<string, UiValue> reagent in reagents.Fields)
            {
                children.Add(new UiNode(UiKind.Button)
                {
                    Key = reagent.Key,
                    Fluid = true,
                    Color = color,
                    Text = reagent.Key,
                    Tooltip = tooltip(reagent.Value.AsNum()),
                });
            }
        }
        return new UiNode(UiKind.StackItem) { Key = key, Grow = 1 }.With(children.ToArray());
    }

    // tg: Microscope.tsx:175  GroupTitle
    private static UiNode GroupTitle(string title)
    {
        return new UiNode(UiKind.Stack) { Key = "title" }.With(
            new UiNode(UiKind.StackItem) { Key = "left", Grow = 1 }.With(new UiNode(UiKind.Divider)),
            new UiNode(UiKind.StackItem) { Key = "text", Color = "gray", Text = title },
            new UiNode(UiKind.StackItem) { Key = "right", Grow = 1 }.With(new UiNode(UiKind.Divider)));
    }

    // JavaScript's number-to-text, as the TSX's template strings print it.
    private static string Format(double value) => TguiFormat.JsNumber(value);
}

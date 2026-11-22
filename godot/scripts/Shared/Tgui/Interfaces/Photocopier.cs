using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Photocopier.tsx (gaps S3, D298). Engine-free. The chosen
// blank and category are the window's local state (tg's useState); tgui-core's createSearch is a case-insensitive substring match.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Photocopier
{
    // tg: Photocopier.tsx:54
    public static UiNode Render(TguiContext c)
    {
        string selectedBlank = c.UseState("selectedBlank", "");
        string selectedCategory = c.UseState("selectedCategory", "");

        // tg: Photocopier.tsx:59  <Window title="Photocopier" width={selectedCategory ? 550 : 325} height={525}>
        return c.Window(string.IsNullOrEmpty(selectedCategory) ? 325 : 550, 525, "Photocopier",
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = string.IsNullOrEmpty(selectedCategory) ? 1 : 0.4 }.With(
                    new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                        new UiNode(UiKind.StackItem).With(Status(c, selectedBlank)),
                        new UiNode(UiKind.StackItem).With(Actions(c, selectedBlank)),
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(Categories(c, selectedCategory)))),
                string.IsNullOrEmpty(selectedCategory)
                    ? null
                    : new UiNode(UiKind.StackItem) { Grow = 0.6 }.With(
                        new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                            new UiNode(UiKind.StackItem) { Grow = 1 }.With(Blanks(c, selectedCategory, selectedBlank))))));
    }

    // tg: Photocopier.tsx:105
    private static UiNode Status(TguiContext c, string selectedBlank)
    {
        UiValue data = c.Data;
        bool has_toner = data.Truthy("has_toner");
        double copies_left = data.Num("copies_left"), num_copies = data.Num("num_copies");
        double current_toner = data.Num("current_toner"), max_toner = data.Num("max_toner");
        double paper_count = data.Num("paper_count"), max_paper_count = data.Num("max_paper_count");
        string created_paper = data.Str("created_paper");

        double average_toner = max_toner * 0.66, bad_toner = max_toner * 0.33;
        double average_paper = max_paper_count * 0.33, bad_paper = max_paper_count * 0.1;

        var paperTypes = new UiNode(UiKind.Stack) { Align = "center" }.With(data.Arr("paper_types").Select(paper =>
        {
            string type = paper.Str("type");
            double amount = paper.Num("amount");
            return new UiNode(UiKind.StackItem) { Grow = 1, Key = type }.With(new UiNode(UiKind.ImageButton)
            {
                Key = "paper:" + type,
                Fluid = true,
                DmIcon = paper.Str("icon"),
                DmIconState = paper.Str("icon_state"),
                Tooltip = $"{paper.Str("name")} amount is {TguiFormat.ToFixed(amount)}",
                Disabled = amount == 0,
                Selected = created_paper == type && amount != 0,
                OnClick = () => c.Act("select_paper_type", ("created_paper", type)),
            });
        }));

        return new UiNode(UiKind.Section)
        {
            Fill = true,
            Title = "Status",
            Buttons = new UiNode(UiKind.Button) { Key = "remove_toner", Icon = "eject", Text = "Eject Toner", Disabled = !has_toner, OnClick = () => c.Act("remove_toner") },
        }.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Label = "Toner" }.With(has_toner
                ? new UiNode(UiKind.ProgressBar)
                {
                    Key = "toner",
                    MinValue = 0,
                    Value = current_toner,
                    MaxValue = max_toner,
                    Ranges = new[] { ("average", bad_toner, average_toner), ("bad", 0.0, bad_toner) },
                }
                : new UiNode(UiKind.ProgressBar) { Key = "toner", Color = "bad", MinValue = 0, Value = 0, MaxValue = 1, Text = "No Cartridge" }),
            new UiNode(UiKind.LabeledItem) { Label = "Paper Stored" }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "paper",
                MinValue = 0,
                Value = paper_count,
                MaxValue = max_paper_count,
                Ranges = new[] { ("average", bad_paper, average_paper), ("bad", 0.0, bad_paper) },
                Text = $"{TguiFormat.ToFixed(paper_count)} / {TguiFormat.ToFixed(max_paper_count)}",
            }),
            new UiNode(UiKind.LabeledItem) { Label = "Queue" }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "queue",
                MinValue = 0,
                Value = copies_left,
                MaxValue = num_copies,
                Text = copies_left != 0 ? $"{TguiFormat.ToFixed(copies_left)} / {TguiFormat.ToFixed(num_copies)}" : "Empty",
            }),
            new UiNode(UiKind.LabeledItem) { Label = "Blank" }.With(new UiNode(UiKind.Box) { Bold = true, TextAlign = "center", Text = string.IsNullOrEmpty(selectedBlank) ? "Not Selected" : selectedBlank }),
            new UiNode(UiKind.LabeledItem) { Label = "Paper Type" }.With(paperTypes)));
    }

    // tg: Photocopier.tsx:213
    private static UiNode Actions(TguiContext c, string selectedBlank)
    {
        UiValue data = c.Data;
        bool has_item = data.Truthy("has_item"), is_photo = data.Truthy("is_photo"), isAI = data.Truthy("isAI");
        string color_mode = data.Str("color_mode");
        return new UiNode(UiKind.Section) { Fill = true, Title = "Actions" }.With(new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack) { Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Color = "label", Text = "Copies:" }),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Slider)
                {
                    Key = "copies",
                    MinValue = 1,
                    Value = data.Num("num_copies"),
                    MaxValue = data.Num("max_copies"),
                    StepPixelSize = 10,
                    OnChange = value => c.Act("set_copies", ("num_copies", value)),
                }))),
            isAI
                ? new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "ai_photo",
                    Fluid = true,
                    Icon = "images",
                    Text = "Print photo from database",
                    Disabled = !data.Truthy("can_AI_print"),
                    OnClick = () => c.Act("ai_photo", ("code", selectedBlank)),
                })
                : null,
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "print_blank",
                    Fluid = true,
                    Icon = "print",
                    Text = "Print",
                    Disabled = string.IsNullOrEmpty(selectedBlank),
                    OnClick = () => c.Act("print_blank", ("code", selectedBlank)),
                }),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "make_copy",
                    Fluid = true,
                    Icon = "copy",
                    Text = "Copy",
                    Disabled = !has_item,
                    OnClick = () => c.Act("make_copy"),
                }))),
            has_item && is_photo
                ? new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button) { Key = "color", Fluid = true, Icon = "palette", Text = "Color", Selected = color_mode == "Color", OnClick = () => c.Act("color_mode", ("mode", "Color")) }),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button) { Key = "greyscale", Fluid = true, Icon = "pen", Text = "Greyscale", Selected = color_mode == "Greyscale", OnClick = () => c.Act("color_mode", ("mode", "Greyscale")) })))
                : null,
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = "remove",
                Fluid = true,
                Icon = has_item ? "eject" : "times",
                Text = "Eject Item",
                Disabled = !has_item,
                OnClick = () => c.Act("remove"),
            })));
    }

    // tg: Photocopier.tsx:337
    private static UiNode Categories(TguiContext c, string selectedCategory)
    {
        var items = new List<UiNode>
        {
            new UiNode(UiKind.StackItem).With(CategoryButton(c, "All Blanks", selectedCategory)),
        };
        foreach (UiValue category in c.Data.Arr("categories"))
            items.Add(new UiNode(UiKind.StackItem) { Key = category.AsStr() }.With(CategoryButton(c, category.AsStr(), selectedCategory)));
        return new UiNode(UiKind.Section)
        {
            Fill = true,
            Scrollable = true,
            Title = "Blanks",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "close_category",
                Icon = "times",
                Tooltip = "Close selected blank category",
                Disabled = string.IsNullOrEmpty(selectedCategory),
                OnClick = () => c.SetState("selectedCategory", ""),
            },
        }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(items));
    }

    private static UiNode CategoryButton(TguiContext c, string category, string selectedCategory)
        => new UiNode(UiKind.Button)
        {
            Key = "category:" + category,
            Fluid = true,
            Icon = "chevron-right",
            Color = "transparent",
            Text = category,
            Selected = selectedCategory == category,
            OnClick = () => c.SetState("selectedCategory", category),
        };

    // tg: Photocopier.tsx:400
    private static UiNode Blanks(TguiContext c, string selectedCategory, string selectedBlank)
    {
        string searchText = c.UseState("searchText", "");
        List<UiValue> sortedBlanks = c.Data.Arr("blanks").OrderBy(b => b.Str("name"), StringComparer.Ordinal).ToList();
        IEnumerable<UiValue> visibleBlanks = !string.IsNullOrEmpty(searchText)
            ? sortedBlanks.Where(b => b.Str("name").Contains(searchText, StringComparison.OrdinalIgnoreCase))
            : selectedCategory == "All Blanks"
                ? sortedBlanks
                : sortedBlanks.Where(b => b.Str("category") == selectedCategory);

        return new UiNode(UiKind.Section)
        {
            Fill = true,
            Scrollable = true,
            Title = selectedCategory,
            Buttons = new UiNode(UiKind.Input)
            {
                Key = "search",
                Width = 8.75,
                TextValue = searchText,
                Tooltip = "Search blank...",
                OnInput = value => c.SetState("searchText", value),
            },
        }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(visibleBlanks.Select(blank =>
        {
            string code = blank.Str("code");
            return new UiNode(UiKind.StackItem) { Key = code }.With(new UiNode(UiKind.Button)
            {
                Key = "blank:" + code,
                Fluid = true,
                Color = "transparent",
                Text = blank.Str("name"),
                Selected = code == selectedBlank,
                OnClick = () => c.SetState("selectedBlank", code),
            });
        })));
    }
}

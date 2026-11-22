using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Biogenerator.tsx (gaps package P2). The window reads the
// biogenerator's ui_data (biogenerator.dm:474: the biomass, the beaker, processing) and ui_static_data (the design categories)
// and sends activate, eject, create and select. Each design's picture is a class of tg's "design" sheet (TguiSpritesheets.Design32, DV44).
// The ProgressBar's lineHeight and the button widths are not in the kit. Original copyright and license: tgstation, AGPL-3.0 (code).
// See ../tgstation/LICENSE.

public static class Biogenerator
{
    // tg: Biogenerator.tsx:48
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> categories = data.Arr("categories");
        bool beaker = data.Truthy("beaker");
        double beakerCurrentVolume = data.Num("beakerCurrentVolume");
        double beakerMaxVolume = data.Num("beakerMaxVolume");

        // tg: Biogenerator.tsx:52  const [selectedCategory, setSelectedCategory] = useState(data.categories[0]?.name)
        string selectedCategory = c.UseState("selectedCategory", categories.Count > 0 ? categories[0].Str("name") : null);

        IReadOnlyList<UiValue> items = categories.FirstOrDefault(category => category.Str("name") == selectedCategory)?.Arr("items") ?? Array.Empty<UiValue>();

        double space = beaker ? beakerMaxVolume - beakerCurrentVolume : 1;

        // tg: Biogenerator.tsx:62  <Window width={400} height={500}>
        return c.Window(400, 500, null,
            new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "controls" }.With(Controls(c)),
                new UiNode(UiKind.StackItem) { Key = "tabs" }.With(new UiNode(UiKind.Tabs) { Fluid = true }.With(
                    categories.Select(category =>
                    {
                        string name = category.Str("name");
                        return new UiNode(UiKind.Tab)
                        {
                            Key = "tab:" + name,
                            Text = name,
                            Selected = name == selectedCategory,
                            OnClick = () => c.SetState("selectedCategory", name),
                        };
                    }).ToArray())),
                new UiNode(UiKind.StackItem) { Key = "items", Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Key = "itemsSection", Fill = true, Scrollable = true }.With(
                        new UiNode(UiKind.Table) { Key = "itemsTable" }.With(items.Select(item => Item(c, item, space)).ToArray())))));
    }

    // tg: Biogenerator.tsx:96  function Controls()
    private static UiNode Controls(TguiContext c)
    {
        UiValue data = c.Data;
        bool beaker = data.Truthy("beaker");
        double beakerCurrentVolume = data.Num("beakerCurrentVolume");
        double beakerMaxVolume = data.Num("beakerMaxVolume");
        double biomass = data.Num("biomass");
        bool can_process = data.Truthy("can_process");
        double max_visual_biomass = data.Num("max_visual_biomass");
        bool processing = data.Truthy("processing");
        string reagent_color = data.Str("reagent_color");

        var list = new UiNode(UiKind.LabeledList) { Key = "controls" }.With(
            // tg: Biogenerator.tsx:111
            new UiNode(UiKind.LabeledItem)
            {
                Key = "biomass",
                Label = "Biomass",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "activate",
                    Icon = "cog",
                    Spin = processing,
                    Disabled = !can_process || processing,
                    Text = "Generate",
                    OnClick = () => c.Act("activate"),
                },
            }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "biomassBar",
                Value = biomass,
                MinValue = 0,
                MaxValue = max_visual_biomass,
                Color = "good",
                // tg: Biogenerator.tsx:137  `${parseFloat(biomass.toFixed(2))} units`
                Text = TguiFormat.JsNumber(double.Parse(TguiFormat.ToFixed(biomass, 2), System.Globalization.CultureInfo.InvariantCulture)) + " units",
            }));
        if (beaker)
        {
            // tg: Biogenerator.tsx:142
            list.With(new UiNode(UiKind.LabeledItem)
            {
                Key = "container",
                Label = "Container",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "eject",
                    Icon = "eject",
                    Text = "Eject",
                    OnClick = () => c.Act("eject"),
                },
            }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "beakerBar",
                Value = beakerCurrentVolume,
                MinValue = 0,
                MaxValue = beakerMaxVolume,
                Height = 2,
                Color = reagent_color,
                Text = TguiFormat.JsNumber(beakerCurrentVolume) + " of " + TguiFormat.JsNumber(beakerMaxVolume) + " units",
            }));
        }
        else
        {
            // tg: Biogenerator.tsx:175
            list.With(new UiNode(UiKind.LabeledItem) { Key = "container", Label = "Container" }.With(
                new UiNode(UiKind.NoticeBox) { Key = "noContainer", Height = 2, Text = "No liquid container" }));
        }
        return new UiNode(UiKind.Section) { Key = "controlsSection", Fill = true }.With(list);
    }

    // tg: Biogenerator.tsx:192  function Item(props: Props)
    private static UiNode Item(TguiContext c, UiValue item, double space)
    {
        UiValue data = c.Data;
        double cost = item.Num("cost");
        string path = item.Str("path");
        bool is_reagent = item.Truthy("is_reagent");
        string name = item.Str("name");
        string icon = item.Str("icon");
        double biomass = data.Num("biomass");
        bool beaker = data.Truthy("beaker");
        double efficiency = data.Num("efficiency");
        double max_output = data.Num("max_output");
        bool processing = data.Truthy("processing");

        double minAmount = is_reagent ? Math.Min(Math.Max(space, 1), 10) : 1;

        // tg: Biogenerator.tsx:201  const [amount, setAmount] = useState(minAmount)
        double amount = c.UseState("amount:" + path, minAmount);

        bool disabled = processing
            || (is_reagent && !beaker)
            || (is_reagent && space < amount)
            || biomass < Math.Ceiling(cost * amount / efficiency);

        double maxPossible = Math.Floor(efficiency * biomass / cost);

        double maxCapacity = is_reagent ? space : max_output;
        double maxAmount = Math.Max(1, Math.Min(maxCapacity, maxPossible));

        return new UiNode(UiKind.TableRow) { Key = "item:" + path }.With(
            // tg: Biogenerator.tsx:216  <span className={classes(['design32x32', icon])} /> <b>{name}</b>
            new UiNode(UiKind.TableCell) { Key = "name" }.With(new UiNode(UiKind.Stack) { Align = "center" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "icon", DmIcon = TguiSpritesheets.Design32, DmIconState = icon, Width = "32px", Height = "32px" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "label", Bold = true, Text = name }))),
            // tg: Biogenerator.tsx:224
            new UiNode(UiKind.TableCell) { Key = "amount" }.With(new UiNode(UiKind.NumberInput)
            {
                Key = "amountInput:" + path,
                Value = amount,
                Step = 1,
                Width = "40px",
                MinValue = 1,
                MaxValue = maxAmount,
                OnChange = value => c.SetState("amount:" + path, value),
            }),
            // tg: Biogenerator.tsx:234
            new UiNode(UiKind.TableCell) { Key = "create" }.With(new UiNode(UiKind.Button)
            {
                Key = "create:" + path,
                Icon = "leaf",
                TextAlign = "right",
                Disabled = disabled,
                Text = TguiFormat.JsNumber(double.Parse(TguiFormat.ToFixed(cost * amount, 2), System.Globalization.CultureInfo.InvariantCulture)),
                OnClick = () => c.Act("create", ("design_path", path), ("amount", amount)),
            }));
    }
}

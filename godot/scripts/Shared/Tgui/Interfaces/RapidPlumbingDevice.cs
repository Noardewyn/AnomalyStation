using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RapidPlumbingDevice.tsx (engineering package E15,
// plumbing). The window reads tg's ui_data and ui_static_data of the plumbing constructor (RPLD.dm:116, :144, with RHD.dm's matter and
// silo data) and sends color, piping_layer, recipe and toggle_silo. The designs and the layer picture are tg's "plumbing-tgui" sprite
// sheet (asset_cache/assets/plumbing.dm), drawn from the client's copy of tg's .dmi files (TguiSpritesheets, DV44).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// tg puts ColorItem, LayerSelect and MatterItem (LabeledList.Items) straight in a Stack.Item; the kit draws items only in a list, so they
// sit in one LabeledList.

public static class RapidPlumbingDevice
{
    // tg: RapidPlumbingDevice.tsx:39
    private static UiNode PlumbingTypeSection(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> categories = data.Arr("categories");
        string selected_recipe = data.Str("selected_recipe");
        // React's useState(selected_category): the value at the first render, kept
        string categoryName = c.Local.Get<string>("categoryName", null);
        if (categoryName == null)
        {
            categoryName = data.Str("selected_category");
            c.Local.SetQuiet("categoryName", categoryName);
        }
        UiValue shownCategory = categories.FirstOrDefault(category => category.Str("cat_name") == categoryName) ?? categories.FirstOrDefault();
        string shownName = shownCategory?.Str("cat_name");

        return new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Tabs).With(
                categories.Select(category =>
                {
                    string name = category.Str("cat_name");
                    return new UiNode(UiKind.Tab)
                    {
                        Key = "tab-" + name,
                        Selected = name == shownName,
                        Text = name,
                        OnClick = () => c.SetState("categoryName", name),
                    };
                })),
            new UiNode(UiKind.Fragment).With(
                (shownCategory?.Arr("recipes") ?? Array.Empty<UiValue>()).Select((recipe, index) =>
                {
                    string name = recipe.Str("name");
                    return new UiNode(UiKind.Button)
                    {
                        Key = "recipe-" + name,
                        Fluid = true,
                        Color = "transparent",
                        Selected = name == selected_recipe,
                        OnClick = () => c.Act("recipe", ("category", shownName), ("id", (double)index)),
                    }.With(
                        // tg: :73  <Box className={classes(['plumbing-tgui32x32', recipe.icon])} style={{ transform: 'scale(1.3) ...' }}
                        new UiNode(UiKind.Box) { Key = "icon-" + name, DmIcon = TguiSpritesheets.Plumbing32, DmIconState = recipe.Str("icon"), Size = 1.3 },
                        new UiNode(UiKind.Box) { Text = TguiFormat.CapitalizeAll(name) });
                })));
    }

    // tg: RapidPlumbingDevice.tsx:87
    public static UiNode LayerSelect(TguiContext c)
    {
        double piping_layer = c.Data.Num("piping_layer");
        return new UiNode(UiKind.LabeledItem) { Label = "Layer" }.With(
            new[] { 1, 2, 3, 4, 5 }.Select(layer => new UiNode(UiKind.Button)
            {
                Key = "layer-" + layer,
                Checkbox = layer == piping_layer,
                Text = layer.ToString(),
                OnClick = () => c.Act("piping_layer", ("piping_layer", (double)layer)),
            }));
    }

    // tg: RapidPlumbingDevice.tsx:107
    private static UiNode LayerIconSection(TguiContext c) =>
        new UiNode(UiKind.Box) { Key = "layer-icon", DmIcon = TguiSpritesheets.Plumbing32, DmIconState = c.Data.Str("layer_icon"), Size = 2 };

    // tg: RapidPlumbingDevice.tsx:121
    public static UiNode Render(TguiContext c) =>
        // tg: RapidPlumbingDevice.tsx:125  <Window width={480} height={575}>
        c.Window(480, 575, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Section).With(
                        new UiNode(UiKind.Stack).With(
                            new UiNode(UiKind.StackItem).With(
                                new UiNode(UiKind.LabeledList).With(
                                    RapidPipeDispenser.ColorItem(c),
                                    LayerSelect(c),
                                    RapidConstructionDevice.MatterItem(c),
                                    c.Data.Truthy("silo_upgraded") ? RapidConstructionDevice.SiloItem(c) : null)),
                            new UiNode(UiKind.StackItem).With(LayerIconSection(c))))),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(PlumbingTypeSection(c))));
}

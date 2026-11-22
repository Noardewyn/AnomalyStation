using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RapidPipeDispenser.tsx (M7 slice D172, the rapid pipe
// dispenser). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The pipe previews are tg's "pipes" sprite sheet (asset_cache/assets/pipes.dm), drawn from the client's copy of tg's .dmi files
// (TguiSpritesheets, DV44); tgui-core's StyleableSection is the kit's Section with the recipe's name as its title and the info icon in
// its title row. D320 (DV352): one picture button per recipe in place of one per direction (RecipeButton).

public static class RapidPipeDispenser
{
    // tg: RapidPipeDispenser.tsx:21
    private static readonly string[] ROOT_CATEGORIES = { "Atmospherics", "Disposals", "Transit Tubes" };

    // tg: RapidPipeDispenser.tsx:23
    public static readonly IReadOnlyDictionary<string, string> ICON_BY_CATEGORY_NAME = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Atmospherics"] = "wrench",
        ["Binary"] = "arrows-left-right",
        ["Devices"] = "microchip",
        ["Disposal Pipes"] = "grip-lines",
        ["Disposals"] = "trash-alt",
        ["Heat Exchange"] = "thermometer-half",
        ["Pipes"] = "grip-lines",
        ["Station Equipment"] = "microchip",
        ["Transit Tubes"] = "bus",
    };

    // tg: RapidPipeDispenser.tsx:35
    private static readonly (string name, int bitmask)[] TOOLS =
    {
        ("Dispense", 1),
        ("Connect", 2),
        ("Destroy", 4),
        ("Reprogram", 8),
    };

    private static string IconOf(string name) => name != null && ICON_BY_CATEGORY_NAME.TryGetValue(name, out string icon) ? icon : null;

    // tg: RapidPipeDispenser.tsx:97
    public static UiNode ColorItem(TguiContext c)
    {
        UiValue data = c.Data;
        string selected_color = data.Str("selected_color");
        IReadOnlyList<KeyValuePair<string, UiValue>> paint_colors = data.Obj("paint_colors");
        string selectedCss = paint_colors.FirstOrDefault(kv => kv.Key == selected_color).Value.AsStr(null);

        return new UiNode(UiKind.LabeledItem) { Label = "Color" }.With(
            paint_colors.Select(kv =>
            {
                string colorName = kv.Key;
                return new UiNode(UiKind.ColorBox)
                {
                    Key = "color-" + colorName,
                    Height = "20px",
                    Width = "20px",
                    // tg: the 3px border: '#20b142' for the selected colour, else '#222'
                    Selected = colorName == selected_color,
                    Color = kv.Value.AsStr(null),
                    OnClick = () => c.Act("color", ("paint_color", colorName)),
                };
            }).Append(
                // tg: RapidPipeDispenser.tsx:122  <Box inline ml={2} px={1} bold color={paint_colors[selected_color]}>
                new UiNode(UiKind.Box) { Key = "selected-color", Bold = true, Color = selectedCss, Text = TguiFormat.CssCapitalize(selected_color) }));
    }

    // tg: RapidPipeDispenser.tsx:129
    private static UiNode ModeItem(TguiContext c)
    {
        double mode = c.Data.Num("mode");
        return new UiNode(UiKind.LabeledItem) { Label = "Modes" }.With(
            TOOLS.Select(tool => new UiNode(UiKind.Button)
            {
                Key = "mode-" + tool.bitmask,
                Checkbox = ((long)mode & tool.bitmask) != 0,
                Text = tool.name,
                OnClick = () => c.Act("mode", ("mode", tool.bitmask)),
            }));
    }

    // tg: RapidPipeDispenser.tsx:152
    private static UiNode CategoryItem(TguiContext c)
    {
        double rootCategoryIndex = c.Data.Num("category");
        return new UiNode(UiKind.LabeledItem) { Label = "Category" }.With(
            ROOT_CATEGORIES.Select((categoryName, i) => new UiNode(UiKind.Button)
            {
                Key = "category-" + i,
                Selected = rootCategoryIndex == i,
                Icon = IconOf(categoryName),
                Color = "transparent",
                Text = categoryName,
                OnClick = () => c.Act("category", ("category", i)),
            }));
    }

    // tg: RapidPipeDispenser.tsx:173
    private static UiNode SelectionSection(TguiContext c)
    {
        double rootCategoryIndex = c.Data.Num("category");
        return new UiNode(UiKind.Section) { Fill = true }.With(
            new UiNode(UiKind.LabeledList).With(
                CategoryItem(c),
                ModeItem(c),
                rootCategoryIndex == 0 ? ColorItem(c) : null,
                rootCategoryIndex == 0 ? LayerSelect(c) : null));
    }

    // tg: RapidPipeDispenser.tsx:189
    private static long layerToBitmask(int layer) => 1L << layer;

    // tg: RapidPipeDispenser.tsx:193
    private static UiNode LayerSelect(TguiContext c)
    {
        UiValue data = c.Data;
        long pipe_layers = (long)data.Num("pipe_layers");
        bool multi_layer = data.Truthy("multi_layer");
        int max_pipe_layers = (int)data.Num("max_pipe_layers", 1);

        return new UiNode(UiKind.LabeledItem) { Label = "Layer" }.With(
            Enumerable.Range(0, Math.Max(0, max_pipe_layers)).Select(layer => new UiNode(UiKind.Button)
            {
                Key = "layer-" + (layer + 1),
                Checkbox = multi_layer ? (pipe_layers & layerToBitmask(layer)) != 0 : layerToBitmask(layer) == pipe_layers,
                Text = TguiFormat.JsNumber(layer + 1),
                OnClick = () => c.Act("pipe_layers", ("pipe_layers", (double)layerToBitmask(layer))),
            }).Append(new UiNode(UiKind.Button)
            {
                Key = "multilayer",
                Checkbox = multi_layer,
                Tooltip = "Build on multiple pipe layers simultaneously",
                Text = "Multi",
                OnClick = () => c.Act("toggle_multi_layer"),
            }));
    }

    // tg: RapidPipeDispenser.tsx:228  the recipe's description, one line per <br>
    private static string getRecipeTooltip(UiValue recipe)
    {
        string desc = recipe.Str("pipe_desc", null);
        if (string.IsNullOrEmpty(desc))
            return null;
        return string.Join("\n", desc.Split("<br>"));
    }

    // tg: RapidPipeDispenser.tsx:247
    private static UiNode RecipeRow(TguiContext c, UiValue recipe, string shownCategory)
    {
        string pipe_name = recipe.Str("pipe_name");
        double pipe_index = recipe.Num("pipe_index");
        string tooltip = getRecipeTooltip(recipe);
        return new UiNode(UiKind.Section)
        {
            Key = "recipe-" + pipe_name,
            Title = pipe_name,
            Buttons = tooltip != null ? new UiNode(UiKind.Icon) { Key = "info-" + pipe_name, Icon = "info-circle", Tooltip = tooltip } : null,
        }.With(
            // tg: RapidPipeDispenser.tsx:278  <Stack fill wrap> of one button per direction. DEVIATION(DV352, D320): one button per recipe, the
            // chosen direction's picture (the first when the recipe is not chosen); Shift and the mouse wheel turn it in the world, where the
            // build ghost shows it on the aimed tile.
            RecipeButton(c, recipe, pipe_name, pipe_index, shownCategory));
    }

    private static UiNode RecipeButton(TguiContext c, UiValue recipe, string pipe_name, double pipe_index, string shownCategory)
    {
        IReadOnlyList<UiValue> previews = recipe.Arr("previews");
        UiValue preview = previews.FirstOrDefault(p => p.Truthy("selected")) ?? previews.FirstOrDefault();
        if (preview == null)
            return null;
        string dir = preview.Str("dir");
        UiValue flipped = preview.Field("flipped") ?? UiValue.Null;
        string turns = previews.Count > 1 ? "\nShift + mouse wheel: turn it" : "";
        return new UiNode(UiKind.Stack) { Fill = true, Wrap = true }.With(
            new UiNode(UiKind.StackItem) { Key = "variant" }.With(
                new UiNode(UiKind.ImageButton)
                {
                    Key = "preview-" + pipe_name,
                    // tg: asset={['pipes32x32', `${preview.dir}-${preview.icon_state}`]}
                    DmIcon = TguiSpritesheets.Pipes32,
                    DmIconState = dir + "-" + preview.Str("icon_state"),
                    Color = "blue",
                    Size = 58 / 32.0,
                    Selected = preview.Truthy("selected"),
                    Tooltip = preview.Str("dir_name") + turns,
                    OnClick = () =>
                    {
                        c.Act("pipe_type", ("pipe_type", pipe_index), ("category", shownCategory));
                        c.Act("setdir", ("dir", dir), ("flipped", flipped));
                    },
                }));
    }

    // tg: RapidPipeDispenser.tsx:307
    private static UiNode PipeTypeSection(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> categories = data.Arr("categories");
        // React's useState(selected_category): the value at the window's first render, kept ("" for undefined)
        string categoryName = c.Local.Get<string>("categoryName", null);
        if (categoryName == null)
        {
            categoryName = data.Str("selected_category", "");
            c.Local.SetQuiet("categoryName", categoryName);
        }

        UiValue shownCategory = categories.FirstOrDefault(category => category.Str("cat_name") == categoryName) ?? categories.FirstOrDefault();
        string shownName = shownCategory?.Str("cat_name");

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.Tabs).With(
                categories.Select(category =>
                {
                    string name = category.Str("cat_name");
                    return new UiNode(UiKind.Tab)
                    {
                        Key = "tab-" + name,
                        Icon = IconOf(name),
                        Selected = name == shownName,
                        Text = name,
                        OnClick = () => c.SetState("categoryName", name),
                    };
                })),
            new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(
                (shownCategory?.Arr("recipes") ?? Array.Empty<UiValue>()).Select(recipe => RecipeRow(c, recipe, shownName))));
    }

    // tg: RapidPipeDispenser.tsx:343
    public static UiNode SmartPipeBlockSection(TguiContext c)
    {
        UiValue init_directions = c.Data.Field("init_directions");
        UiNode Arrow(string icon, string dir) => new UiNode(UiKind.Button)
        {
            Key = "dir-" + dir,
            Icon = icon,
            Selected = init_directions.Truthy(dir),
            OnClick = () => c.Act("init_dir_setting", ("dir_flag", dir)),
        };

        return new UiNode(UiKind.Section) { Fill = true }.With(
            new UiNode(UiKind.Stack) { Vertical = true, TextAlign = "center" }.With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem).With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "dir-info",
                                Color = "transparent",
                                Icon = "info",
                                Tooltip = "This is a panel for blocking certain connection directions for the smart pipes. The button in the center resets to default (all directions can connect)",
                            }),
                        new UiNode(UiKind.StackItem).With(Arrow("arrow-up", "north")))),
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem).With(Arrow("arrow-left", "west")),
                        new UiNode(UiKind.StackItem).With(
                            new UiNode(UiKind.Button) { Key = "init-reset", Icon = "circle", OnClick = () => c.Act("init_reset") }),
                        new UiNode(UiKind.StackItem).With(Arrow("arrow-right", "east")))),
                new UiNode(UiKind.StackItem).With(Arrow("arrow-down", "south"))));
    }

    // tg: RapidPipeDispenser.tsx:421
    public static UiNode Render(TguiContext c)
    {
        double rootCategoryIndex = c.Data.Num("category");

        // tg: RapidPipeDispenser.tsx:426  <Window width={550} height={580}>
        return c.Window(550, 580, null,
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Stack) { Fill = true }.With(
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(SelectionSection(c)),
                        rootCategoryIndex == 0 ? new UiNode(UiKind.StackItem) { Width = "90px" }.With(SmartPipeBlockSection(c)) : null)),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(PipeTypeSection(c))));
    }
}

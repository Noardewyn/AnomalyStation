using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RapidTilingDevice.tsx (engineering package E13).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The tile pictures are tg's "rtd" sprite sheet (asset_cache/assets/rtd.dm), drawn from the client's copy of tg's .dmi files
// (TguiSpritesheets, DV44).

public static class RapidTilingDevice
{
    // tg: RapidTilingDevice.tsx:30
    private static UiNode TilePreview(TguiContext c)
    {
        string selected_icon = c.Data.Str("selected_icon");
        string selected_direction = c.Data.Str("selected_direction", null);
        return new UiNode(UiKind.Section) { Key = "preview", Background = "green", Width = "50px", Height = "50px" }.With(
            new UiNode(UiKind.Box)
            {
                Key = "preview-icon",
                DmIcon = TguiSpritesheets.Rtd32,
                DmIconState = selected_icon + (!string.IsNullOrEmpty(selected_direction) ? selected_direction : "south"),
            });
    }

    // tg: RapidTilingDevice.tsx:54
    private static UiNode DirectionSelect(TguiContext c)
    {
        string selected_direction = c.Data.Str("selected_direction", null);
        return new UiNode(UiKind.Section) { Fill = true }.With(
            new UiNode(UiKind.Stack) { Vertical = true }.With(
                c.Data.Arr("tile_dirs").Select(d => d.AsStr()).Select(dir => new UiNode(UiKind.StackItem) { Key = dir }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "dir-" + dir,
                        Checkbox = dir == selected_direction,
                        Color = "transparent",
                        Text = dir,
                        OnClick = () => c.Act("set_dir", ("dir", dir)),
                    }))));
    }

    // tg: RapidTilingDevice.tsx:78
    private static UiNode TileRotateSection(TguiContext c)
    {
        UiValue selected = c.Data.Field("selected_direction");
        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(TilePreview(c)),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(selected != null && !selected.IsNull ? DirectionSelect(c) : null));
    }

    // tg: RapidTilingDevice.tsx:94
    private static UiNode TileDesignSection(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> categories = data.Arr("categories");
        string selected_category = data.Str("selected_category");
        string selected_recipe = data.Str("selected_recipe");
        // React's useState(selected_category): the value at the first render, kept
        string categoryName = c.Local.Get<string>("categoryName", null);
        if (categoryName == null)
        {
            categoryName = selected_category ?? "";
            c.Local.SetQuiet("categoryName", categoryName);
        }
        UiValue shownCategory = categories.FirstOrDefault(category => category.Str("category_name") == categoryName) ?? categories.FirstOrDefault();
        string shownName = shownCategory?.Str("category_name");

        return new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Tabs).With(
                categories.Select(category =>
                {
                    string name = category.Str("category_name");
                    return new UiNode(UiKind.Tab)
                    {
                        Key = "tab-" + name,
                        Selected = name == categoryName,
                        Text = name,
                        OnClick = () => c.SetState("categoryName", name),
                    };
                })),
            new UiNode(UiKind.Fragment).With(
                (shownCategory?.Arr("recipes") ?? Array.Empty<UiValue>()).Select((recipe, i) =>
                {
                    string name = recipe.Str("name");
                    return new UiNode(UiKind.Button)
                    {
                        Key = "recipe-" + name,
                        Fluid = true,
                        Color = "transparent",
                        Height = "35px",
                        Selected = name == selected_recipe && shownName == selected_category,
                        OnClick = () => c.Act("recipe", ("category_name", shownName), ("id", (double)(i + 1))),
                    }.With(
                        new UiNode(UiKind.Box) { Key = "icon-" + name, DmIcon = TguiSpritesheets.Rtd32, DmIconState = recipe.Str("icon") + "south" },
                        new UiNode(UiKind.Box) { Text = TguiFormat.CapitalizeAll(name) });
                })));
    }

    // tg: RapidTilingDevice.tsx:145
    public static UiNode Render(TguiContext c) =>
        c.Window(500, 540, null,
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(RapidConstructionDevice.InfoSection(c)),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Stack) { Fill = true }.With(
                        new UiNode(UiKind.StackItem).With(TileRotateSection(c)),
                        new UiNode(UiKind.StackItem) { Grow = 1 }.With(TileDesignSection(c))))));
}

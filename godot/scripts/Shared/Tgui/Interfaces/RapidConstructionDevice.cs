using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RapidConstructionDevice.tsx (engineering package E13, the
// RCD family). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The design pictures are tg's "rcd-tgui" sprite sheet (asset_cache/assets/rcd.dm), drawn from the client's copy of tg's .dmi files
// (TguiSpritesheets, DV44); the Airlock Access root shows the airlock electronics' section (AirlockElectronics.AirLockMainSection).

public static class RapidConstructionDevice
{
    // tg: RapidConstructionDevice.tsx:41
    public static UiNode MatterItem(TguiContext c) =>
        new UiNode(UiKind.LabeledItem) { Label = "Units Left", Text = " " + TguiFormat.JsNumber(c.Data.Num("matterLeft")) + " Units" };

    // tg: RapidConstructionDevice.tsx:52
    public static UiNode SiloItem(TguiContext c)
    {
        bool silo_enabled = c.Data.Truthy("silo_enabled");
        return new UiNode(UiKind.LabeledItem) { Label = "Silo Link" }.With(
            new UiNode(UiKind.Button)
            {
                Key = "toggle_silo",
                Checkbox = silo_enabled,
                Color = "transparent",
                Text = silo_enabled ? "Silo Online" : "Silo Offline",
                OnClick = () => c.Act("toggle_silo"),
            });
    }

    // tg: RapidConstructionDevice.tsx:68
    private static UiNode CategoryItem(TguiContext c)
    {
        string selected_root = c.Data.Str("selected_root");
        return new UiNode(UiKind.LabeledItem) { Label = "Category" }.With(
            c.Data.Arr("root_categories").Select(r => r.AsStr()).Select(root => new UiNode(UiKind.Button)
            {
                Key = "root-" + root,
                Text = root,
                Selected = selected_root == root,
                Color = "transparent",
                OnClick = () => c.Act("root_category", ("root_category", root)),
            }));
    }

    // tg: RapidConstructionDevice.tsx:86
    public static UiNode InfoSection(TguiContext c) =>
        new UiNode(UiKind.Section).With(
            new UiNode(UiKind.LabeledList).With(
                MatterItem(c),
                c.Data.Truthy("silo_upgraded") ? SiloItem(c) : null,
                CategoryItem(c)));

    // tg: RapidConstructionDevice.tsx:102
    private static UiNode DesignSection(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> categories = data.Arr("categories");
        string selected_category = data.Str("selected_category");
        string selected_design = data.Str("selected_design");
        // React's useState(selected_category): the value at the first render, kept
        string categoryName = c.Local.Get<string>("categoryName", null);
        if (categoryName == null)
        {
            categoryName = selected_category ?? "";
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
                (shownCategory?.Arr("designs") ?? Array.Empty<UiValue>()).Select((design, i) =>
                {
                    string title = design.Str("title");
                    return new UiNode(UiKind.Button)
                    {
                        Key = "design-" + title,
                        Fluid = true,
                        Height = "31px",
                        Color = "transparent",
                        Selected = title == selected_design && shownName == selected_category,
                        OnClick = () => c.Act("design", ("category", shownName), ("index", (double)(i + 1))),
                    }.With(
                        // tg: :140  <Box className={classes(['rcd-tgui32x32', design.icon])} style={{ transform: scale(0.7) for the wide ones }}
                        new UiNode(UiKind.Box)
                        {
                            Key = "icon-" + title,
                            DmIcon = TguiSpritesheets.Rcd32,
                            DmIconState = design.Str("icon"),
                            Size = title is "full tile window" or "full tile reinforced window" or "catwalk" ? 0.7 : 1.0,
                        },
                        new UiNode(UiKind.Box) { Text = TguiFormat.CapitalizeAll(title) });
                })));
    }

    // tg: RapidConstructionDevice.tsx:162
    private static UiNode ConfigureSection(TguiContext c) =>
        new UiNode(UiKind.StackItem) { Grow = 1 }.With(
            c.Data.Str("selected_root") == "Airlock Access" ? AirlockElectronics.AirLockMainSection(c) : DesignSection(c));

    // tg: RapidConstructionDevice.tsx:177
    public static UiNode Render(TguiContext c) =>
        c.Window(450, 590, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem).With(InfoSection(c)),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Stack) { Fill = true }.With(ConfigureSection(c)))));
}

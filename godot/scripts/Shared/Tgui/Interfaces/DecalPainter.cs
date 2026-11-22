using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DecalPainter.tsx (engineering package E14). The window reads
// tg's ui_static_data (the categories with their decals, colours and dirs, the sheet's prefix) and ui_data (the chosen decal, colour,
// dir, custom colour and category) of the decal painter (decal_painter.dm:73, :91) and sends tg's acts: select_category, select_color,
// pick_custom_color and select_decal. The previews are classes of tg's "paintable_decals" sheet (TguiSpritesheets.Decals32, DV44).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Not drawn: the preview buttons' 2px margin and padding (the kit's picture button has its own).

public static class DecalPainter
{
    // tg: tgui/packages/tgui/interfaces/DecalPainter.tsx:41  cut alpha
    private static string FilterBoxColor(string color) => !color.StartsWith('#') ? color : color.Length > 7 ? color[..7] : color;

    // tg: DecalPainter.tsx:50
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> categories = data.Arr("categories");
        string activeCategory = data.Str("active_category");
        string currentColor = data.Str("current_color");
        string currentDecal = data.Str("current_decal");
        double currentDir = data.Num("current_dir");
        string prefix = data.Str("icon_prefix");

        UiValue active = categories.FirstOrDefault(category => category.Str("category") == activeCategory);
        IReadOnlyList<UiValue> decalList = active?.Arr("decal_list") ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> colorList = active?.Arr("color_list") ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> dirList = active?.Arr("dir_list") ?? Array.Empty<UiValue>();

        bool customColorSelected = !colorList.Any(color => color.Str("color") == currentColor);
        // Handle custom color icon correctly
        string previewColor = customColorSelected ? "custom" : currentColor;

        // tg: DecalPainter.tsx:76  <Window width={650} height={455}>
        return c.Window(650, 455, null,
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                // tg: DecalPainter.tsx:79
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Section) { Key = "categories", Title = "Category" }.With(
                        categories.Select(category =>
                        {
                            string name = category.Str("category");
                            return new UiNode(UiKind.Button)
                            {
                                Key = "category:" + name,
                                Text = name,
                                Selected = category == active,
                                OnClick = () => c.Act("select_category", ("category", name)),
                            };
                        }))),
                // tg: DecalPainter.tsx:96
                colorList.Count > 1
                    ? new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Section) { Key = "colors", Title = "Color" }.With(
                            colorList.Select(color => ColorButton(c, color, currentColor, customColorSelected, data.Str("current_custom_color")))))
                    : null,
                // tg: DecalPainter.tsx:132
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Key = "style", Title = "Style", Fill = true, Scrollable = true }.With(
                        new UiNode(UiKind.Stack) { Wrap = true }.With(decalList.Select(decal =>
                        {
                            string state = decal.Str("icon_state");
                            string decalName = decal.Str("name");
                            // tg: DecalPainter.tsx:135  decal.directional === 0
                            bool nondirectional = decal.Field("directional")?.Kind == UiValueKind.Number && decal.Num("directional") == 0;
                            return nondirectional
                                // Tallll button for nondirectional
                                ? IconButton(c, prefix, state, 2, previewColor, decalName, state == currentDecal)
                                // 4 buttons for directional
                                : new UiNode(UiKind.Stack) { Key = "column:" + state, Vertical = true }.With(dirList.Select(dir =>
                                    IconButton(c, prefix, state, dir.Num("dir"), previewColor, dir.Str("name") + " " + decalName,
                                        state == currentDecal && dir.Num("dir") == currentDir)));
                        }))))));
    }

    // tg: DecalPainter.tsx:99-127
    private static UiNode ColorButton(TguiContext c, UiValue color, string currentColor, bool customColorSelected, string customColor)
    {
        string name = color.Str("name");
        string value = color.Str("color");
        bool custom = value == "custom";
        return new UiNode(UiKind.Button)
        {
            Key = "color:" + name,
            Selected = custom ? customColorSelected : value == currentColor,
            OnClick = custom ? () => c.Act("pick_custom_color") : () => c.Act("select_color", ("color", value)),
        }.With(
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.ColorBox) { Key = "box", Color = custom ? customColor : FilterBoxColor(value) }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "name", Text = custom ? "Custom" : name })));
    }

    // tg: DecalPainter.tsx:181  the icon key `${icon_prefix} ${decal}_${dir}_${color.replace('#', '')}`, a button of the sheet's class
    private static UiNode IconButton(TguiContext c, string prefix, string state, double dir, string color, string label, bool selected)
    {
        string dirText = TguiFormat.JsNumber(dir);
        int hash = color.IndexOf('#');
        string key = state + "_" + dirText + "_" + (hash < 0 ? color : color.Remove(hash, 1));
        return new UiNode(UiKind.ImageButton)
        {
            Key = "decal:" + key,
            DmIcon = prefix, // "paintable_decals32x32" (TguiSpritesheets.Decals32)
            DmIconState = key,
            Tooltip = label,
            Selected = selected,
            OnClick = () => c.Act("select_decal", ("decal", state), ("dir", dir)),
        };
    }
}

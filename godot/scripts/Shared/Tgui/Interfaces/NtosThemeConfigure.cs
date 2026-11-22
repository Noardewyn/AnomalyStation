using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosThemeConfigure.tsx (M9 slice E: the Themeify app).
// Engine-free (compiled into the tests too). Button.Checkbox is a button with tgui's checkbox icons.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosThemeConfigure
{
    // tg: NtosThemeConfigure.tsx:17
    public static UiNode Render(TguiContext c)
    {
        string current = c.Data.Str("PC_device_theme");
        IReadOnlyList<UiValue> themes = c.Data.Get("themes")?.Items ?? Array.Empty<UiValue>();
        return NtosWindow.Render(c, 400, 600, null, new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true, Align = "center" }.With(
                themes.Select(theme =>
                {
                    bool checkedTheme = theme.Str("theme_ref") == current;
                    return new UiNode(UiKind.StackItem) { Key = "theme:" + theme.Str("theme_name") }.With(new UiNode(UiKind.Button)
                    {
                        Key = "theme",
                        Width = "75%",
                        Icon = checkedTheme ? "check-square-o" : "square-o",
                        Selected = checkedTheme,
                        Text = theme.Str("theme_name"),
                        OnClick = () => c.Act("PRG_change_theme", ("selected_theme", theme.Str("theme_name"))),
                    });
                })))));
    }
}

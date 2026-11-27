using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AntagInfoShade.jsx: the loyal shade's info window, from
// /datum/antagonist/shade_minion's ui_static_data (shade_minion.dm:18). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AntagInfoShade
{
    // tg: AntagInfoShade.jsx:6
    public static UiNode Render(TguiContext c)
    {
        string master_name = c.Data.Str("master_name");
        // tg: AntagInfoShade.jsx:10  <Window width={400} height={400} theme="abductor"><Window.Content backgroundColor="#9d0032">
        c.WindowTheme = "abductor";
        return c.Window(400, 400, null,
            new UiNode(UiKind.Box) { Key = "content", Background = "#9d0032", Fill = true }.With(
                // tg: AntagInfoShade.jsx:12  <Icon size={20} name="ghost" color="#660020" position="absolute" top="20%" left="28%" />
                new UiNode(UiKind.Icon) { Key = "ghost", Icon = "ghost", Color = "#660020", Size = 20 },
                new UiNode(UiKind.Section) { Key = "section", Fill = true }.With(
                    new UiNode(UiKind.Stack) { Key = "stack", Vertical = true, Fill = true, TextAlign = "center" }.With(
                        new UiNode(UiKind.StackItem) { Key = "captured", FontSize = "20px", Text = "Your soul has been captured!" },
                        new UiNode(UiKind.StackItem) { Key = "bound", FontSize = "30px", Text = "You are bound to the will of " + master_name + "!" },
                        new UiNode(UiKind.StackItem) { Key = "help", FontSize = "20px", Text = "Help them succeed in their goals at all costs." }))));
    }
}

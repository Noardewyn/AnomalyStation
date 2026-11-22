using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MinigamesMenu.tsx (gaps S4d, D327): the ghost's
// Minigames menu: CTF, Mafia, Basketball and Deathmatch. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MinigamesMenu
{
    // tg: MinigamesMenu.tsx:6
    public static UiNode Render(TguiContext c)
    {
        UiNode Game(string action, string text) => new UiNode(UiKind.StackItem) { Key = action, Grow = 1 }.With(new UiNode(UiKind.Button)
        {
            Key = "play:" + action,
            Text = text,
            Fluid = true,
            FontSize = 3,
            TextAlign = "center",
            OnClick = () => c.Act(action),
        });
        return c.Window(530, 320, "Minigames Menu", new UiNode(UiKind.Section) { Key = "games", Title = "Select Minigame", TextAlign = "center", Fill = true }.With(
            new UiNode(UiKind.Stack) { Key = "row1" }.With(Game("ctf", "CTF"), Game("mafia", "Mafia")),
            new UiNode(UiKind.Divider) { Key = "divider" },
            new UiNode(UiKind.Stack) { Key = "row2" }.With(Game("basketball", "Basketball"), Game("deathmatch", "Deathmatch"))));
    }
}

using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosCursor.tsx (gaps S4c, D327): the NtOS Cursor Replacer,
// its one option (tg's cool sword, drawn from its icon) and the notice that counts the clicks. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosCursor
{
    // tg: NtosCursor.tsx:20
    public static UiNode Render(TguiContext c)
    {
        UiValue dmi = c.Data.Get("dmi");
        int numClicked = c.UseState("numClicked", 0);
        string noticeBoxText = numClicked <= 2 ? "There's only one option... It's the sword."
            : numClicked == 3 ? "You clicked the sword. It's still the sword."
            : numClicked == 4 ? "You clicked the sword again. It's still the sword."
            : numClicked == 5 ? "Trying to click the sword again? It's still the sword."
            : "You clicked the sword " + numClicked + " times... It's still the sword.";
        return NtosWindow.Render(c, 350, 300, null, new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(
            new UiNode(UiKind.Section) { Title = "Select Cursor" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "sword", Align = "center" }.With(new UiNode(UiKind.Button)
                {
                    Key = "sword",
                    Height = "100px",
                    Width = "100px",
                    Color = numClicked >= 1 ? "green" : null,
                    OnClick = () => c.SetState("numClicked", numClicked + 1),
                }.With(new UiNode(UiKind.Box) { DmIcon = dmi?.Str("icon"), DmIconState = dmi?.Str("icon_state"), Size = 3 })),
                new UiNode(UiKind.StackItem) { Key = "notice" }.With(new UiNode(UiKind.NoticeBox) { Text = noticeBoxText })))));
    }
}

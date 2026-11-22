using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NoticeBoard.tsx (gaps S3, D298). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NoticeBoard
{
    // tg: NoticeBoard.tsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool allowed = data.Truthy("allowed");
        IReadOnlyList<UiValue> items = data.Arr("items");

        var rows = new List<UiNode>();
        if (items.Count == 0)
        {
            // tg: NoticeBoard.tsx:21
            rows.Add(new UiNode(UiKind.Section).With(new UiNode(UiKind.Box) { Color = "white", TextAlign = "center", Text = "The notice board is empty!" }));
        }
        foreach (UiValue item in items)
        {
            string itemRef = item.Str("ref");
            // tg: NoticeBoard.tsx:28  <Stack color="black" backgroundColor="white">
            rows.Add(new UiNode(UiKind.Stack) { Key = itemRef }.With(
                new UiNode(UiKind.StackItem) { Grow = 1, Background = "#ffffff", Align = "center" }.With(new UiNode(UiKind.Box) { Color = "black", TextAlign = "center", Text = item.Str("name") }),
                new UiNode(UiKind.StackItem) { Background = "#ffffff" }.With(
                    new UiNode(UiKind.Button) { Key = "examine:" + itemRef, Icon = "eye", OnClick = () => c.Act("examine", ("ref", itemRef)) },
                    new UiNode(UiKind.Button) { Key = "remove:" + itemRef, Icon = "eject", Disabled = !allowed, OnClick = () => c.Act("remove", ("ref", itemRef)) })));
        }

        // tg: NoticeBoard.tsx:18  <Window width={425} height={220}> <Window.Content backgroundColor="#704D25">
        return c.Window(425, 220, null, PaperworkWindows.Content("#704D25", rows.ToArray()));
    }
}

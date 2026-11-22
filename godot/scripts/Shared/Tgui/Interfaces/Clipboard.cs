using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Clipboard.tsx (gaps S3, D298). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Clipboard
{
    // tg: Clipboard.tsx:23
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string pen = data.Str("pen");
        bool integrated_pen = data.Truthy("integrated_pen");
        string top_paper = data.Str("top_paper");
        string top_paper_ref = data.Str("top_paper_ref");
        IReadOnlyList<UiValue> paper = data.Arr("paper");
        IReadOnlyList<UiValue> paper_ref = data.Arr("paper_ref");

        // tg: Clipboard.tsx:30
        UiNode penSection = new UiNode(UiKind.Section).With(
            !string.IsNullOrEmpty(pen)
                ? new UiNode(UiKind.LabeledList).With(new UiNode(UiKind.LabeledItem)
                {
                    Label = "Pen",
                    Buttons = new UiNode(UiKind.Button) { Key = "remove_pen", Icon = "eject", OnClick = () => c.Act("remove_pen") },
                }.With(new UiNode(UiKind.Box) { Text = pen }))
                : new UiNode(UiKind.Box) { Color = "white", TextAlign = "center", Text = integrated_pen ? "There is a pen integrated into the clipboard's clip." : "No pen attached!" });

        var rows = new List<UiNode> { penSection, new UiNode(UiKind.Divider) { Rule = true } };
        // tg: Clipboard.tsx:56
        if (!string.IsNullOrEmpty(top_paper))
            rows.Add(PaperRow(c, top_paper, top_paper_ref, pen, false));
        else
            rows.Add(new UiNode(UiKind.Section).With(new UiNode(UiKind.Box) { Color = "white", TextAlign = "center", Text = "The clipboard is empty!" }));
        if (paper.Count > 0)
            rows.Add(new UiNode(UiKind.Divider) { Rule = true });
        for (int index = 0; index < paper.Count; index++)
            rows.Add(PaperRow(c, paper[index].AsStr(), index < paper_ref.Count ? paper_ref[index].AsStr() : "", pen, true));

        // tg: Clipboard.tsx:27  <Window title="Clipboard" width={400} height={500}> <Window.Content backgroundColor="#704D25" scrollable>
        return c.Window(400, 500, "Clipboard", PaperworkWindows.Content("#704D25", rows.ToArray()));
    }

    // tg: Clipboard.tsx:57 (the top paper) and :91 (the papers under it, with the move-to-top arrow)
    private static UiNode PaperRow(TguiContext c, string name, string paperRef, string pen, bool under)
        => new UiNode(UiKind.Stack) { Key = (under ? "paper:" : "top:") + paperRef }.With(
            under
                ? new UiNode(UiKind.StackItem) { Background = "#ffffff" }.With(new UiNode(UiKind.Button)
                {
                    Key = "move_top:" + paperRef,
                    Icon = "chevron-up",
                    Color = "transparent",
                    OnClick = () => c.Act("move_top_paper", ("ref", paperRef)),
                })
                : null,
            new UiNode(UiKind.StackItem) { Grow = 1, Background = "#ffffff", Align = "center" }.With(new UiNode(UiKind.Box) { Color = "black", TextAlign = "center", Text = name }),
            new UiNode(UiKind.StackItem) { Background = "#ffffff" }.With(
                new UiNode(UiKind.Button) { Key = "edit:" + paperRef, Icon = string.IsNullOrEmpty(pen) ? "eye" : "pen", OnClick = () => c.Act("edit_paper", ("ref", paperRef)) },
                new UiNode(UiKind.Button) { Key = "rename:" + paperRef, Icon = "tag", OnClick = () => c.Act("rename_paper", ("ref", paperRef)) },
                new UiNode(UiKind.Button) { Key = "remove:" + paperRef, Icon = "eject", OnClick = () => c.Act("remove_paper", ("ref", paperRef)) }));
}

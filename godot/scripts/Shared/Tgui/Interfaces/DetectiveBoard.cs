using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DetectiveBoard/ (index.tsx, BoardTabs.tsx, Evidence.tsx, Pin.tsx; gaps J4, D298).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// DEVIATION(DV254): a rope is two clicks on the pins, not a drag from pin to pin: the kit's controls have no mouse-up on another control during a drag. Photos have no picture yet (not-translated section 107).

public static class DetectiveBoard
{
    // tg: DetectiveBoard/index.tsx:37
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> cases = data.Arr("cases");
        int current_case = (int)data.Num("current_case");

        // tg: index.tsx:295  <Window width={1200} height={800}>
        if (cases.Count == 0)
        {
            return c.Window(1200, 800, null,
                new UiNode(UiKind.Stack) { Vertical = true, Fill = true, Align = "center" }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1 },
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Icon = "search", Color = "average", Size = 15 }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Color = "red", Bold = true, FontSize = 18, Text = "You have no cases! Create the first one" }),
                    new UiNode(UiKind.StackItem) { Grow = 3 }.With(new UiNode(UiKind.Button) { Key = "add_case", Icon = "plus", Text = "Create case", OnClick = () => c.Act("add_case") })));
        }

        UiValue item = current_case >= 1 && current_case <= cases.Count ? cases[current_case - 1] : null;
        return c.Window(1200, 800, null, BoardTabs(c, cases, current_case), item == null ? null : Board(c, item));
    }

    // tg: DetectiveBoard/BoardTabs.tsx:29
    private static UiNode BoardTabs(TguiContext c, IReadOnlyList<UiValue> cases, int current_case)
    {
        var tabs = new List<UiNode>();
        for (int i = 0; i < cases.Count; i++)
        {
            UiValue item = cases[i];
            int index = i + 1;
            tabs.Add(new UiNode(UiKind.Tab)
            {
                Key = "case-" + index,
                Selected = index == current_case,
                Color = item.Str("color"),
                Text = item.Str("name"),
                OnClick = () => c.Act("set_case", ("case", index)),
            });
            if (index == current_case)
            {
                string case_ref = item.Str("ref");
                tabs.Add(new UiNode(UiKind.Button) { Key = "remove_case", Color = "transparent", Icon = "times", OnClick = () => c.Act("remove_case", ("case_ref", case_ref)) });
                tabs.Add(new UiNode(UiKind.Button) { Key = "rename_case", Color = "transparent", Icon = "pen", OnClick = () => c.Act("rename_case", ("case_ref", case_ref)) });
            }
        }
        tabs.Add(new UiNode(UiKind.Button) { Key = "add_case", Color = "transparent", Icon = "plus", OnClick = () => c.Act("add_case") });
        return new UiNode(UiKind.Stack).With(tabs.Select(t => new UiNode(UiKind.StackItem) { Key = t.Key }.With(t)));
    }

    // tg: DetectiveBoard/index.tsx:300
    private static UiNode Board(TguiContext c, UiValue item)
    {
        string case_ref = item.Str("ref");
        string connecting = c.UseState<string>("connecting", null);
        var lines = item.Arr("connections").Select(con => (
            con.Get("from").Num("x"),
            con.Get("from").Num("y"),
            con.Get("to").Num("x"),
            con.Get("to").Num("y"),
            con.Str("color"))).ToList();
        return new UiNode(UiKind.Board) { Key = case_ref, BoardLines = lines, Height = 700 }.With(
            item.Arr("evidences").Select(evidence => Evidence(c, case_ref, evidence, connecting)));
    }

    // tg: DetectiveBoard/Evidence.tsx:21
    private static UiNode Evidence(TguiContext c, string case_ref, UiValue evidence, string connecting)
    {
        string evidence_ref = evidence.Str("ref");
        bool connected = evidence.Arr("connections").Any(r => r.Text == connecting);
        return new UiNode(UiKind.Stack)
        {
            Key = evidence_ref,
            Vertical = true,
            Width = 200,
            PosX = evidence.Num("x"),
            PosY = evidence.Num("y"),
            OnMoved = (x, y) => c.Act("set_evidence_cords", ("evidence_ref", evidence_ref), ("case_ref", case_ref), ("rel_x", x), ("rel_y", y)),
        }.With(
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Stack).With(
                    // tg: DetectiveBoard/Pin.tsx: the rope starts here and ends on another pin
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "pin-" + evidence_ref,
                        Icon = "thumbtack",
                        Selected = connecting == evidence_ref,
                        OnClick = () =>
                        {
                            if (connecting == null)
                            {
                                c.SetState<string>("connecting", evidence_ref);
                                return;
                            }
                            // tg: index.tsx:165  handleMouseUpOnPin
                            if (connecting != evidence_ref && !connected)
                                c.Act("add_connection", ("from_ref", connecting), ("to_ref", evidence_ref));
                            c.SetState<string>("connecting", null);
                        },
                    }),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Bold = true, Text = evidence.Str("name") }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "remove-" + evidence_ref,
                        Icon = "trash",
                        OnClick = () => c.Act("remove_evidence", ("evidence_ref", evidence_ref)),
                    }))),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = "look-" + evidence_ref,
                Icon = evidence.Str("type") == "photo" ? "image" : "file-lines",
                Text = evidence.Str("type") == "photo" ? "Photo" : evidence.Str("text"),
                Fluid = true,
                OnClick = () => c.Act("look_evidence", ("case_ref", case_ref), ("evidence_ref", evidence_ref)),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = evidence.Str("description") }));
    }
}

using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ImplantPad.tsx (gaps DV2): the implant pad's case
// (eject), the deathrattle groups (save, set, initialize) and the inserted implant's information and lore
// (items/implants/implantpad.dm:64 ui_data). tg's <br /> between the two group lines is the section's two boxes. Engine-free
// (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ImplantPad
{
    // tg: ImplantPad.tsx:23
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasCase = data.Truthy("has_case");
        bool hasImplant = data.Truthy("has_implant");
        string saved = data.Str("saved_deathrattle_group");
        string current = data.Str("current_deathrattle_group");
        bool hasCurrent = !string.IsNullOrEmpty(current);
        // tg: ImplantPad.tsx:33  <Window width={300} height={350}>, <Window.Content scrollable>
        return c.Window(300, 350, null, new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Stack) { Key = "header", Bold = true }.With(
                new UiNode(UiKind.StackItem) { Key = "title", Grow = 1, Color = "good", Align = "center", Text = "Implant Mini-Computer" },
                new UiNode(UiKind.StackItem) { Key = "eject_item" }.With(new UiNode(UiKind.Button)
                {
                    Key = "eject",
                    Icon = "eject",
                    Disabled = !hasCase,
                    Text = "Eject Case",
                    OnClick = () => c.Act("eject_implant"),
                })),
            new UiNode(UiKind.Divider) { Key = "divider_top" },
            new UiNode(UiKind.Section) { Key = "groups" }.With(
                new UiNode(UiKind.Box) { Key = "saved", Text = "Saved Deathrattle Group: " + (string.IsNullOrEmpty(saved) ? "None" : saved) },
                new UiNode(UiKind.Box) { Key = "current", Text = "Current Deathrattle Group: " + (hasCurrent ? current : "None") }),
            new UiNode(UiKind.Stack) { Key = "group_buttons" }.With(
                new UiNode(UiKind.StackItem) { Key = "save_item" }.With(new UiNode(UiKind.Button)
                {
                    Key = "save", Disabled = !hasCurrent, Text = "Save Group", OnClick = () => c.Act("save_deathrattle_group"),
                }),
                new UiNode(UiKind.StackItem) { Key = "set_item" }.With(new UiNode(UiKind.Button)
                {
                    Key = "set", Disabled = hasCurrent || !hasCase, Text = "Set Group", OnClick = () => c.Act("set_deathrattle_group"),
                }),
                // in case you want to start a new deathrattle group
                new UiNode(UiKind.StackItem) { Key = "init_item" }.With(new UiNode(UiKind.Button)
                {
                    Key = "init", Disabled = hasCurrent || !hasCase, Text = "Initialize Group", OnClick = () => c.Act("init_deathrattle_group"),
                })),
            new UiNode(UiKind.Divider) { Key = "divider_bottom" },
            new UiNode(UiKind.Collapsible) { Key = "information", Open = true, Title = "Implant Information" }.With(
                CaseSection("information_text", hasCase, hasImplant, data.Str("case_information"))),
            new UiNode(UiKind.Collapsible) { Key = "extended", Title = "Implant Extended Information" }.With(
                CaseSection("extended_text", hasCase, hasImplant, data.Str("case_lore")))));
    }

    // tg: ImplantPad.tsx:86  the case's three states, shared by both collapsibles
    private static UiNode CaseSection(string key, bool hasCase, bool hasImplant, string text)
    {
        if (!hasCase)
            return new UiNode(UiKind.Section) { Key = key, Text = "No implant case detected. Please insert one to see its contents." };
        if (!hasImplant)
            return new UiNode(UiKind.Section) { Key = key, Text = "Implant case does not have an implant. Please insert an implant to continue." };
        return new UiNode(UiKind.Section) { Key = key, Text = text };
    }
}

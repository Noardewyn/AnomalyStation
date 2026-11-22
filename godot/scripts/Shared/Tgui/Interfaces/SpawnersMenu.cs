using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SpawnersMenu.tsx (silicons SI7, D326): the ghost
// roles' menu (a section per spawner group with its count, Jump and Spawn). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SpawnersMenu
{
    // tg: SpawnersMenu.tsx:30
    public static UiNode Render(TguiContext c)
    {
        var stack = new UiNode(UiKind.Stack) { Key = "spawners", Vertical = true };
        foreach (UiValue spawner in c.Data.Arr("spawners"))
        {
            string name = spawner.Str("name");
            var buttons = new UiNode(UiKind.Stack) { Key = "buttons" }.With(
                new UiNode(UiKind.StackItem) { Key = "left", FontSize = "14px", Color = "green", Text = spawner.Truthy("infinite") ? "Infinite" : TguiFormat.JsNumber(spawner.Num("amount_left")) + " left" },
                new UiNode(UiKind.StackItem) { Key = "act" }.With(
                    new UiNode(UiKind.Button) { Key = "jump", Text = "Jump", OnClick = () => c.Act("jump", ("name", name)) },
                    new UiNode(UiKind.Button) { Key = "spawn", Text = "Spawn", OnClick = () => c.Act("spawn", ("name", name)) }));
            string desc = spawner.Str("desc");
            var list = new UiNode(UiKind.LabeledList);
            if (!string.IsNullOrEmpty(desc))
                list.With(Item("desc", "Description", null, desc));
            else
                list.With(
                    Item("origin", "Origin", null, Or(spawner.Str("you_are_text"), "Unknown")),
                    Item("directives", "Directives", null, Or(spawner.Str("flavor_text"), "None")),
                    Item("conditions", "Conditions", "bad", Or(spawner.Str("important_text"), "None")));
            stack.With(new UiNode(UiKind.StackItem) { Key = name }.With(
                new UiNode(UiKind.Section) { Key = "section", Fill = true, Title = TguiFormat.CapitalizeAll(name), Buttons = buttons }.With(list)));
        }
        return c.Window(700, 525, "Spawners Menu", new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(stack));
    }

    // a labeled item whose long text wraps at the window's width (a wrapping box, as the kit's labels do not wrap)
    private static UiNode Item(string key, string label, string color, string text) =>
        new UiNode(UiKind.LabeledItem) { Key = key, Label = label, Color = color }.With(new UiNode(UiKind.Box) { Key = "text", Text = text, Color = color, Wrap = true });

    private static string Or(string text, string fallback) => string.IsNullOrEmpty(text) ? fallback : text;
}

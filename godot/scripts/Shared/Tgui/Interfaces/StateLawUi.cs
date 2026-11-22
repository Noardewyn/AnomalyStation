using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/StateLawUi.tsx (silicons SI1, D326: a silicon's
// State Laws window). Engine-free (compiled into the tests too). tgui-core's BlockQuote is a box with the law type's colour.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class StateLawUi
{
    // tg: tgui/packages/tgui/interfaces/StateLawUi.tsx:13
    private static string LawTypeColor(string type) => type switch
    {
        "hacked" => "red",
        "core" => "blue",
        "supplied" => "green",
        _ => null,
    };

    // tg: tgui/packages/tgui/interfaces/StateLawUi.tsx:31
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool locked = data.Truthy("locked");
        var stated = data.Arr("stated_laws").Select(v => v.AsStr()).ToHashSet();
        IReadOnlyList<UiValue> allLaws = data.Arr("all_laws");

        UiNode Law(UiValue law, int index) =>
            new UiNode(UiKind.StackItem) { Key = "law" + index, Candystripe = true }.With(
                new UiNode(UiKind.Stack) { Align = "center" }.With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "check" + index,
                        Checkbox = stated.Contains(law.Str("text")),
                        OnClick = () => c.Act("toggle_stated", ("law", law.Str("text"))),
                    }),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box)
                    {
                        Color = LawTypeColor(law.Str("type")) ?? "green",
                        Wrap = true,
                        Text = law.Str("number") + ". " + law.Str("text"),
                    })));

        UiNode stateButton = c.ButtonConfirm("state_laws", new UiNode(UiKind.Button)
        {
            Fluid = true,
            TextAlign = "center",
            FontSize = "20px",
            Color = "blue",
            Disabled = locked,
            Text = locked ? "On Cooldown" : "State Laws",
            OnClick = () => c.Act("state_laws"),
        });
        if (stateButton.Text == "Confirm?")
            stateButton.Color = "green"; // confirmColor="green"

        return c.Window(400, System.Math.Max(1, allLaws.Count) * 55 + 150, "State Laws",
            new UiNode(UiKind.Section) { Title = "Which laws do you want to include when stating them for the crew?", Scrollable = true }.With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(
                    allLaws.Count == 0
                        ? new[] { new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "No laws available!" }) }
                        : allLaws.Select(Law).ToArray())),
            new UiNode(UiKind.Section).With(stateButton));
    }
}

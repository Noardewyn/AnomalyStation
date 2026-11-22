using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Limbgrower.jsx (gaps MD3): the limb grower's
// reagents with their Remove Reagent confirm buttons, and its designs by category tab with their reagent costs and Make buttons;
// the Building... dimmer while it grows (the kit's Modal). The selected tab is the window's local state (tg's shared state).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Limbgrower
{
    // tg: Limbgrower.jsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> reagents = data.Arr("reagents");
        IReadOnlyList<UiValue> categories = data.Arr("categories");
        bool busy = data.Truthy("busy");
        string tab = c.UseState("category", categories.Count > 0 ? categories[0].Str("name") : null);
        IReadOnlyList<UiValue> designList = categories.FirstOrDefault(category => category.Str("name") == tab)?.Arr("designs") ?? Array.Empty<UiValue>();

        var content = new UiNode(UiKind.Stack) { Key = "content", Vertical = true, Fill = true };
        // tg: Limbgrower.jsx:28  {!!busy && <Dimmer fontSize="32px"><Icon name="cog" spin={1} />{' Building...'}</Dimmer>}
        if (busy)
            content.With(new UiNode(UiKind.Modal) { Key = "busy" }.With(new UiNode(UiKind.Box) { Key = "building", Icon = "cog", Spin = true, FontSize = "32px", Text = " Building..." }));

        // tg: Limbgrower.jsx:35
        content.With(new UiNode(UiKind.Section) { Key = "reagents", Title = "Reagents" }.With(
            new UiNode(UiKind.Box) { Key = "capacity", Text = TguiFormat.JsNumber(data.Num("total_reagents")) + " / " + TguiFormat.JsNumber(data.Num("max_reagents")) + " reagent capacity used." },
            new UiNode(UiKind.LabeledList) { Key = "reagent_list" }.With(reagents.Select(reagent =>
            {
                string name = reagent.Str("reagent_name");
                string type = reagent.Str("reagent_type");
                return new UiNode(UiKind.LabeledItem)
                {
                    Key = "reagent:" + name,
                    Label = name,
                    Text = TguiFormat.JsNumber(reagent.Num("reagent_amount")) + "u",
                    Buttons = c.ButtonConfirm("empty:" + name, new UiNode(UiKind.Button)
                    {
                        TextAlign = "center",
                        Width = "120px",
                        Text = "Remove Reagent",
                        Color = "bad",
                        OnClick = () => c.Act("empty_reagent", ("reagent_type", type)),
                    }),
                };
            }).ToArray())));

        // tg: Limbgrower.jsx:64
        content.With(new UiNode(UiKind.Section) { Key = "designs", Title = "Designs" }.With(
            new UiNode(UiKind.Tabs) { Key = "tabs" }.With(categories.Select(category =>
            {
                string name = category.Str("name");
                return new UiNode(UiKind.Tab)
                {
                    Key = "tab:" + name,
                    Fluid = true,
                    Selected = tab == name,
                    OnClick = () => c.SetState("category", name),
                    Text = name,
                };
            }).ToArray()),
            new UiNode(UiKind.LabeledList) { Key = "design_list" }.With(designList.Select(design =>
            {
                string name = design.Str("name");
                string path = design.Str("path");
                string parent = design.Str("parent_category");
                return new UiNode(UiKind.LabeledItem)
                {
                    Key = "design:" + name,
                    Label = name,
                    Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "make:" + name,
                        Text = "Make",
                        Color = "good",
                        OnClick = () => c.Act("make_limb", ("design_path", path), ("active_tab", parent)),
                    },
                }.With(design.Arr("needed_reagents").Select(reagent =>
                    new UiNode(UiKind.Box) { Key = "cost:" + reagent.Str("name"), Text = reagent.Str("name") + ": " + TguiFormat.JsNumber(reagent.Num("amount")) + "u" }).ToArray());
            }).ToArray())));

        return c.Window(500, 550, "Limb Grower", new UiNode(UiKind.Section) { Key = "body", Fill = true, Scrollable = true }.With(content));
    }
}

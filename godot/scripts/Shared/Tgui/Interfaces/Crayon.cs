using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Crayon.tsx (gaps J3, D298: the crayon's and spray can's stencils, cap and colours).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Crayon
{
    // tg: tgui/packages/tgui/interfaces/Crayon.tsx:21
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool is_capped = data.Truthy("is_capped");
        string selected_stencil = data.Str("selected_stencil");
        bool capOrChanges = data.Truthy("has_cap") || data.Truthy("can_change_colour");
        IReadOnlyList<UiValue> drawables = data.Arr("drawables");

        // tg: Crayon.tsx:34  <Window width={600} height={600}>
        return c.Window(600, 600, null,
            // tg: Crayon.tsx:37  {!!capOrChanges && <Section title="Basic">}
            capOrChanges
                ? new UiNode(UiKind.Section) { Title = "Basic" }.With(
                    new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Label = "Cap" }.With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "cap",
                                Icon = is_capped ? "power-off" : "times",
                                Text = is_capped ? "On" : "Off",
                                Selected = is_capped,
                                OnClick = () => c.Act("toggle_cap"),
                            }),
                        RapidPipeDispenser.ColorItem(c),
                        new UiNode(UiKind.LabeledItem).With(
                            new UiNode(UiKind.Button)
                            {
                                Key = "custom",
                                Text = "Custom color",
                                OnClick = () => c.Act("custom_color"),
                            })))
                : null,
            // tg: Crayon.tsx:58  <Section title="Stencil">
            new UiNode(UiKind.Section) { Title = "Stencil" }.With(
                new UiNode(UiKind.LabeledList).With(
                    drawables.Select(drawable =>
                        new UiNode(UiKind.LabeledItem) { Key = drawable.Str("name"), Label = drawable.Str("name") }.With(
                            drawable.Arr("items").Select(item =>
                            {
                                string name = item.Str("item");
                                return new UiNode(UiKind.Button)
                                {
                                    Key = name,
                                    Text = name,
                                    Selected = name == selected_stencil,
                                    OnClick = () => c.Act("select_stencil", ("item", name)),
                                };
                            }))))),
            // tg: Crayon.tsx:81  {!!is_literate_user && <Section title="Text">}
            data.Truthy("is_literate_user")
                ? new UiNode(UiKind.Section) { Title = "Text" }.With(
                    new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Label = "Current Buffer", Text = data.Str("text_buffer") }),
                    new UiNode(UiKind.Button)
                    {
                        Key = "new-text",
                        Text = "New Text",
                        OnClick = () => c.Act("enter_text"),
                    })
                : null);
    }
}

using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemPress.tsx (engineering package E15, plumbing). The
// window reads tg's ui_data and ui_static_data of the chemical press (pill_press.dm:91, :99) and sends change_current_volume,
// change_pill_duraton, change_product_name and change_product. The styles are tg's "chemmaster" sprite sheet (the product's class_name,
// "chemmaster32x32 <id>"), drawn from the client's copy of tg's .dmi files (TguiSpritesheets, DV44).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemPress
{
    // tg: ChemPress.tsx:32
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> packaging_types = data.Arr("packaging_types");
        string packaging_type = data.Str("packaging_type");
        // React's useState(packaging_category): the value at the first render, kept
        string categoryName = c.Local.Get<string>("categoryName", null);
        if (categoryName == null)
        {
            categoryName = data.Str("packaging_category");
            c.Local.SetQuiet("categoryName", categoryName);
        }
        UiValue shownCategory = packaging_types.FirstOrDefault(category => category.Str("cat_name") == categoryName) ?? packaging_types.FirstOrDefault();
        string shownName = shownCategory?.Str("cat_name");

        // tg: ChemPress.tsx:51  <Window width={450} height={340}>
        return c.Window(450, 340, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Product" }.With(
                        packaging_types.Select(category =>
                        {
                            string name = category.Str("cat_name");
                            return new UiNode(UiKind.Button)
                            {
                                Key = "category-" + name,
                                Checkbox = name == shownName,
                                Text = TguiFormat.CapitalizeAll(name),
                                OnClick = () => c.SetState("categoryName", name),
                            };
                        })),
                    new UiNode(UiKind.LabeledItem) { Label = "Volume" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "volume",
                            Value = data.Num("current_volume"),
                            Unit = "u",
                            Width = "43px",
                            MinValue = data.Num("min_volume"),
                            MaxValue = data.Num("max_volume"),
                            Step = 1,
                            StepPixelSize = 2,
                            OnChange = value => c.Act("change_current_volume", ("volume", value)),
                        }),
                    shownName == "pills"
                        ? new UiNode(UiKind.LabeledItem) { Label = "Duration" }.With(
                            new UiNode(UiKind.NumberInput)
                            {
                                Key = "duration",
                                Value = data.Num("pill_duration"),
                                Unit = "s",
                                Width = "43px",
                                MinValue = 0,
                                MaxValue = data.Num("max_duration"),
                                Step = 1,
                                StepPixelSize = 2,
                                OnChange = value => c.Act("change_pill_duraton", ("duration", value)),
                            })
                        : null,
                    new UiNode(UiKind.LabeledItem) { Label = "Name" }.With(
                        new UiNode(UiKind.Input)
                        {
                            Key = "name",
                            TextValue = data.Str("product_name"),
                            OnCommit = value => c.Act("change_product_name", ("name", value)),
                        }),
                    new UiNode(UiKind.LabeledItem) { Label = "Styles" }.With(
                        (shownCategory?.Arr("products") ?? Array.Empty<UiValue>()).Select(design =>
                        {
                            string @ref = design.Str("ref");
                            // tg: asset={['', design.class_name]}: the class names its sheet first ("chemmaster32x32 <id>")
                            string[] cls = design.Str("class_name").Split(' ', 2);
                            return new UiNode(UiKind.ImageButton)
                            {
                                Key = "style-" + @ref,
                                DmIcon = cls[0],
                                DmIconState = cls.Length > 1 ? cls[1] : "",
                                Size = 42.0 / 32,
                                Selected = @ref == packaging_type,
                                Color = "transparent",
                                OnClick = () => c.Act("change_product", ("ref", @ref)),
                            };
                        })))));
    }
}

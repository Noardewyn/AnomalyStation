using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/HighLuminosityEyesMenu.tsx (gaps OG1): the high
// luminosity eyes' light colour and range and the eye colour matching (eyes_augments.dm ui_data). tg's Input commits on blur; here on
// Enter or on leaving the field (OnCommit). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class HighLuminosityEyesMenu
{
    // tg: HighLuminosityEyesMenu.tsx:27  enum ToUpdate
    private const int LightColor = 0, LeftEye = 1, RightEye = 2;

    // tg: HighLuminosityEyesMenu.tsx:173
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue eyeColor = data.Field("eyeColor");
        bool hasOwner = eyeColor.Truthy("hasOwner");
        bool mode = eyeColor.Truthy("mode");
        var list = new UiNode(UiKind.LabeledList) { Key = "settings_list" }.With(
            ColorRow(c, "light", "Color", data.Str("lightColor"), LightColor, "Brings up a color pick window to change the light color.",
                "Randomizes the light color."),
            RangeRow(c, data.Num("range")));
        if (hasOwner)
            list = list.With(EyeColorRows(c, eyeColor, mode));
        return c.Window(hasOwner ? 262 : 225, hasOwner ? (mode ? 170 : 220) : 135, "High Luminosity Eyes",
            new UiNode(UiKind.Section) { Key = "settings", Fill = true, Title = "Settings" }.With(list));
    }

    // tg: HighLuminosityEyesMenu.tsx:33  LightColorDisplay (and the two eye rows of EyeColorDisplay, which are the same row)
    private static UiNode ColorRow(TguiContext c, string key, string label, string color, int toUpdate, string pickTooltip, string diceTooltip) =>
        new UiNode(UiKind.LabeledItem) { Key = key, Label = label }.With(
            new UiNode(UiKind.ColorBox) { Key = key + "_box", Color = color },
            new UiNode(UiKind.Button) { Key = key + "_pick", Icon = "palette", Tooltip = pickTooltip, OnClick = () => c.Act("pick_color", ("to_update", toUpdate)) },
            new UiNode(UiKind.Button) { Key = key + "_random", Icon = "dice", Tooltip = diceTooltip, OnClick = () => c.Act("random_color", ("to_update", toUpdate)) },
            new UiNode(UiKind.Input)
            {
                Key = key + "_input", TextValue = color, Width = 6, MaxLength = 7,
                OnCommit = value => c.Act("enter_color", ("new_color", value), ("to_update", toUpdate)),
            });

    // tg: HighLuminosityEyesMenu.tsx:65  RangeDisplay
    private static UiNode RangeRow(TguiContext c, double range) =>
        new UiNode(UiKind.LabeledItem) { Key = "range", Label = "Range" }.With(
            new UiNode(UiKind.Button) { Key = "range_minus", Icon = "minus-square-o", Tooltip = "Reduces the light range.", OnClick = () => c.Act("set_range", ("new_range", range - 1)) },
            new UiNode(UiKind.Button) { Key = "range_plus", Icon = "plus-square-o", Tooltip = "Increases the light range.", OnClick = () => c.Act("set_range", ("new_range", range + 1)) },
            new UiNode(UiKind.NumberInput)
            {
                Key = "range_input", Width = "35px", Step = 1, StepPixelSize = 5, Value = range, MinValue = 0, MaxValue = 5,
                OnChange = value => c.Act("set_range", ("new_range", value)),
            });

    // tg: HighLuminosityEyesMenu.tsx:101  EyeColorDisplay
    private static UiNode[] EyeColorRows(TguiContext c, UiValue eyeColor, bool mode)
    {
        var match = new UiNode(UiKind.LabeledItem) { Key = "match", Label = "Match Color" }.With(new UiNode(UiKind.Button)
        {
            Key = "match_toggle", Checkbox = mode, Tooltip = "Toggles whether eyecolor matches the color of the light.",
            OnClick = () => c.Act("toggle_eye_color"),
        });
        if (mode)
            return new[] { match };
        return new[]
        {
            match,
            ColorRow(c, "left", "Left Eye", eyeColor.Str("left"), LeftEye, "Brings up a color pick window to change the light color.", "Randomizes the eye color."),
            ColorRow(c, "right", "Right Eye", eyeColor.Str("right"), RightEye, "Brings up a color pick window to change the light color.", "Randomizes the eye color."),
        };
    }
}

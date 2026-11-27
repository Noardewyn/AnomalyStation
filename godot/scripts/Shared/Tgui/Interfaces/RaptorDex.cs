using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RaptorDex.tsx (the RaptorDex's scan of a raptor,
// raptor_dex.dm and raptordex.dm). DEVIATION(DV619): the raptor's picture and the friendship hearts are drawn from their icon and
// icon states (raptor_icon, raptor_icon_state, raptor_happiness), not tg's base64 pictures. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class RaptorDex
{
    // tg: RaptorDex.tsx:42
    public static UiNode Render(TguiContext c)
    {
        c.WindowTheme = "hackerman";
        return c.Window(770, 370, "Raptor Data", Content(c));
    }

    private static UiNode Modifier(string key, string label, double value, double max) => new UiNode(UiKind.LabeledItem) { Key = key, Label = label }.With(
        new UiNode(UiKind.ProgressBar)
        {
            Key = key,
            Value = value,
            MaxValue = max,
            Ranges = new[] { ("good", 0.5 * max, double.PositiveInfinity), ("average", 0.0, max * 0.5), ("bad", double.NegativeInfinity, 0.0) },
        });

    // tgui-core's capitalizeFirst: the first letter upper case, the rest kept
    private static string CapitalizeFirst(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // tg: RaptorDex.tsx:52
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("raptor_scan"))
            return new UiNode(UiKind.Modal) { Key = "empty", TextAlign = "center", Text = "No scan data present." };

        // JavaScript's Math.round: halves go up
        double speed = Math.Floor(10 / Math.Max(data.Num("raptor_speed"), 0.5) + 0.5);
        var stats = new UiNode(UiKind.LabeledList) { Key = "stats" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "health", Label = "Health", Text = TguiFormat.JsNumber(data.Num("raptor_health")) + " / " + TguiFormat.JsNumber(data.Num("raptor_max_health")) },
            new UiNode(UiKind.LabeledItem) { Key = "attack", Label = "Attack", Text = TguiFormat.JsNumber(data.Num("raptor_attack")) },
            new UiNode(UiKind.LabeledItem) { Key = "speed", Label = "Speed", Text = TguiFormat.JsNumber(speed) },
            new UiNode(UiKind.LabeledItem) { Key = "gender", Label = "Gender", Text = CapitalizeFirst(data.Str("raptor_gender")) });
        if (data.Truthy("can_grow"))
            stats.With(new UiNode(UiKind.LabeledItem) { Key = "growth", Label = "Growth" }.With(
                new UiNode(UiKind.ProgressBar) { Key = "growth", Value = data.Num("raptor_growth"), MaxValue = 100 }));

        // tg draws the happiness overlay's row of four hearts as one 72 px picture; here the hearts stand in a row, each
        // cropped to its drawn pixels
        var hearts = new UiNode(UiKind.Stack) { Key = "hearts", Justify = "center" };
        int index = 0;
        foreach (UiValue heart in data.Arr("raptor_happiness"))
            hearts.With(new UiNode(UiKind.StackItem) { Key = "heart:" + index++ }.With(
                new UiNode(UiKind.Box) { Key = "heart", DmIcon = "icons/effects/effects.dmi", DmIconState = heart.AsStr(), CropToPixels = true, Width = "20px", Height = "20px" }));

        var traits = new UiNode(UiKind.Stack) { Key = "traits", Vertical = true };
        index = 0;
        foreach (UiValue trait in data.Arr("inherited_traits"))
            traits.With(new UiNode(UiKind.StackItem) { Key = "trait:" + index++, Text = trait.AsStr() });

        return new UiNode(UiKind.Stack) { Key = "dex" }.With(
            new UiNode(UiKind.StackItem) { Key = "left", Width = "33%" }.With(
                new UiNode(UiKind.Section) { Key = "picture", TextAlign = "center", Title = TguiFormat.CapitalizeAll(data.Str("raptor_color")) }.With(
                    new UiNode(UiKind.Box) { Key = "raptor", DmIcon = data.Str("raptor_icon"), DmIconState = data.Str("raptor_icon_state"), Width = "224px", Height = "128px" }),
                new UiNode(UiKind.Section) { Key = "description", Fill = true, Title = "Description" }.With(
                    new UiNode(UiKind.Box) { Key = "text", Wrap = true, Text = data.Str("raptor_description") })),
            new UiNode(UiKind.StackItem) { Key = "middle", Width = "33%", TextAlign = "center" }.With(
                new UiNode(UiKind.Section) { Key = "stats", Title = "Stats" }.With(stats),
                new UiNode(UiKind.Section) { Key = "inherit", Title = "Inherit Modifiers" }.With(
                    new UiNode(UiKind.LabeledList) { Key = "modifiers" }.With(
                        Modifier("health", "Health", data.Num("inherited_health"), data.Num("inherited_health_max")),
                        Modifier("attack", "Attack", data.Num("inherited_attack"), data.Num("inherited_attack_max")),
                        Modifier("speed", "Speed", data.Num("inherited_speed"), data.Num("inherited_speed_max")),
                        Modifier("ability", "Capability", data.Num("inherited_ability"), data.Num("inherited_ability_max")),
                        Modifier("growthSpeed", "Growth Speed", data.Num("inherited_growth"), data.Num("inherited_growth_max"))))),
            new UiNode(UiKind.StackItem) { Key = "right", Width = "33%" }.With(
                new UiNode(UiKind.Section) { Key = "bond", TextAlign = "center", Title = "Friendship bond" }.With(hearts),
                new UiNode(UiKind.Section) { Key = "traits", TextAlign = "center", Title = "Inherited Traits" }.With(traits)));
    }
}

using System.Globalization;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Portagrav.tsx (engineering package E16). The window
// reads tg's ui_data of the portable gravity unit (portagrav.dm:216) and sends its acts: toggle_wire, toggle_power, adjust_grav and
// adjust_range. tgui's retro theme and NuclearBomb's display boxes and keypad buttons are drawn as LockedSafe draws them: the boxes as
// large bold text, the keys as big icon buttons.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Portagrav
{
    // tg: Portagrav.tsx:17
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue percentage = data.Field("percentage");
        bool on = data.Truthy("on");
        bool wiremode = data.Truthy("wiremode");
        string range = Num(data.Field("range"));
        string maxrange = Num(data.Field("maxrange"));
        string gravity = Num(data.Field("gravity"));
        string draw = data.Str("draw");

        // tg: Portagrav.tsx:21  <Window width={320} height={320} theme="retro">
        c.WindowTheme = "retro";
        // tg: Portagrav.tsx:28
        string power = !wiremode && percentage != null && percentage.Kind != UiValueKind.Null
            ? Num(percentage) + "%"
            : wiremode ? "WIRE POWERED" : "NO CELL";
        return c.Window(320, 320, null,
            new UiNode(UiKind.Box) { Key = "power", Width = "100%", Bold = true, FontSize = "18px", TextAlign = "center", Text = power + " - " + gravity + "G" },
            // tg: Portagrav.tsx:42
            new UiNode(UiKind.Box) { Key = "draw", Width = "100%", Bold = true, FontSize = "18px", TextAlign = "center", Text = (on ? draw : "OFF") + " / Rng: " + range + "/" + maxrange },
            new UiNode(UiKind.Section) { Height = "65%" }.With(
                new UiNode(UiKind.Stack).With(
                    // tg: Portagrav.tsx:51
                    new UiNode(UiKind.StackItem) { Width = "30%" }.With(
                        new UiNode(UiKind.Stack) { Vertical = true }.With(
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "Power" }),
                            new UiNode(UiKind.Divider),
                            new UiNode(UiKind.StackItem).With(Key(c, "toggle_wire", "plug", "toggle_wire", null, "Toggles if to take power from cable.")),
                            new UiNode(UiKind.StackItem).With(Key(c, "toggle_power", "power-off", "toggle_power", null)))),
                    // tg: Portagrav.tsx:81
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Stack) { Vertical = true }.With(
                            new UiNode(UiKind.Box) { TextAlign = "center", Text = "Gravity Control" },
                            new UiNode(UiKind.Divider),
                            new UiNode(UiKind.StackItem).With(Key(c, "grav_up", "arrow-up", "adjust_grav", 1)),
                            new UiNode(UiKind.Divider),
                            new UiNode(UiKind.StackItem).With(Key(c, "grav_down", "arrow-down", "adjust_grav", -1)))),
                    new UiNode(UiKind.Divider),
                    // tg: Portagrav.tsx:107
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Stack) { Vertical = true }.With(
                            new UiNode(UiKind.Box) { TextAlign = "center", Text = "Range Control" },
                            new UiNode(UiKind.Divider),
                            new UiNode(UiKind.StackItem).With(Key(c, "range_up", "arrow-up", "adjust_range", 1)),
                            new UiNode(UiKind.Divider),
                            new UiNode(UiKind.StackItem).With(Key(c, "range_down", "arrow-down", "adjust_range", -1)))))));
    }

    // NuclearBomb__Button--keypad: a 64 px key with a large icon (size={3})
    private static UiNode Key(TguiContext c, string key, string icon, string action, int? adjustment, string tooltip = null) => new(UiKind.Button)
    {
        Key = key,
        Icon = icon,
        FontSize = "30px",
        TextAlign = "center",
        Width = "64px",
        Height = "64px",
        Tooltip = tooltip,
        OnClick = adjustment is int a ? () => c.Act(action, ("adjustment", a)) : () => c.Act(action),
    };

    // a number as JavaScript prints it in a template (`${percentage}%`)
    private static string Num(UiValue v) => v == null ? "undefined" : v.Kind == UiValueKind.Number
        ? v.AsNum().ToString("R", CultureInfo.InvariantCulture)
        : v.AsStr();
}

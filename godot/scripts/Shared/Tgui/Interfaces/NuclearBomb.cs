using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NuclearBomb.tsx (syndicate N1a, D316: the nuke and Meta's
// self-destruct terminal; its keypad is LockedSafe's too, as in tg). Engine-free (compiled into the tests too). The retro theme's display
// boxes (NuclearBomb__displayBox) are boxes of large bold text; the NT logo box (NuclearBomb__NTIcon, a stylesheet picture) is left out.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NuclearBomb
{
    // tg: NuclearBomb.tsx:14  KEYPAD: three columns of four keys
    private static readonly string[][] Keypad =
    {
        new[] { "1", "4", "7", "C" },
        new[] { "2", "5", "8", "0" },
        new[] { "3", "6", "9", "E" },
    };

    // tg: NuclearBomb.tsx:24  NukeKeypad
    public static UiNode NukeKeypad(TguiContext c)
    {
        var columns = new UiNode(UiKind.Stack) { Key = "keypad" };
        foreach (string[] keyColumn in Keypad)
        {
            var column = new UiNode(UiKind.StackItem) { Key = "column:" + keyColumn[0] };
            foreach (string key in keyColumn)
            {
                string digit = key;
                column.Children.Add(new UiNode(UiKind.Button)
                {
                    Key = "key:" + key,
                    Fluid = true,
                    Bold = true,
                    TextAlign = "center",
                    FontSize = "40px",
                    Width = "55px",
                    Height = "50px",
                    Text = key,
                    OnClick = () => c.Act("keypad", ("digit", digit)),
                });
            }
            columns.Children.Add(column);
        }
        return new UiNode(UiKind.Box) { Key = "keypadBox", Width = "185px" }.With(columns);
    }

    // tg: NuclearBomb.tsx:61
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        c.WindowTheme = "retro";
        var display1 = new UiNode(UiKind.Box) { Key = "status1", Bold = true, FontSize = "24px", Text = data.Str("status1") };
        var display2 = new UiNode(UiKind.Box) { Key = "status2", Bold = true, FontSize = "24px", Text = data.Str("status2") };
        var eject = new UiNode(UiKind.Button)
        {
            Key = "eject", Icon = "eject", FontSize = "24px", TextAlign = "center", Width = "43px",
            OnClick = () => c.Act("eject_disk"),
        };
        var second = new UiNode(UiKind.Stack) { Key = "secondRow" }.With(
            new UiNode(UiKind.StackItem) { Key = "display", Grow = 1 }.With(display2),
            new UiNode(UiKind.StackItem) { Key = "ejectItem" }.With(eject));
        var arm = new UiNode(UiKind.Button)
        {
            Key = "arm", Fluid = true, Bold = true, TextAlign = "center", FontSize = "28px", Text = "ARM",
            OnClick = () => c.Act("arm"),
        };
        var anchor = new UiNode(UiKind.Button)
        {
            Key = "anchor", Fluid = true, Bold = true, TextAlign = "center", FontSize = "28px", Text = "ANCHOR",
            OnClick = () => c.Act("anchor"),
        };
        var radiation = new UiNode(UiKind.Box) { Key = "radiationBox", TextAlign = "center", Color = "#9C9987", FontSize = "80px" }
            .With(new UiNode(UiKind.Icon) { Key = "radiation", Icon = "radiation" });
        var right = new UiNode(UiKind.StackItem) { Key = "controls", Width = "129px" }
            .With(new UiNode(UiKind.Box) { Key = "controlsBox" }.With(arm, anchor, radiation));
        var lower = new UiNode(UiKind.Stack) { Key = "lowerRow" }.With(
            new UiNode(UiKind.StackItem) { Key = "keypadItem" }.With(NukeKeypad(c)),
            right);
        return c.Window(350, 442, null, display1, second, lower);
    }
}

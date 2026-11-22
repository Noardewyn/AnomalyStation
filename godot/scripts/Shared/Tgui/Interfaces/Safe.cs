using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Safe.jsx with styles/interfaces/Safe.scss (gaps package G1,
// D291). The window reads tg's ui_data of the combination safe (safe.dm:178) and sends tg's acts: open, turnright, turnleft (the amount
// of ticks) and retrieve (the item's place). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Not drawn: the engraving's outset border and the two hinges on its edge (CSS), the help note's paper, shadow and tilt, and the dial
// buttons' icon on the far side (the arrows stand before the text). The dial picture is tg's icons/ui/safe/safe_dial.png, which the icon
// tool copies, turned by 3.6 degrees a number as Safe.jsx does; an item in the safe is drawn from its own icon file and state (DV150: tg
// sends a picture by the name of its icon state).

public static class Safe
{
    /// <summary>tg's asset/simple/safe: the dial's picture, as the strip menu's slot pictures are plain PNGs of the converted tree.</summary>
    public const string DialPicture = "icons/ui/safe/safe_dial.png";

    // tg: Safe.jsx:8
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool open = data.Truthy("open");
        double dial = data.Num("dial");
        c.WindowTheme = "ntos";

        // tg: Safe.jsx:11  <Window width={625} height={800} theme="ntos"><Window.Content><Box className="Safe__engraving">
        return c.Window(625, 800, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                Dialer(c, data),
                new UiNode(UiKind.Icon) { Key = "arrow", Icon = "long-arrow-alt-down", Size = 5, Color = "label" },
                open
                    ? Contents(c, data)
                    : new UiNode(UiKind.Box)
                    {
                        Key = "dial-picture",
                        DmIcon = DialPicture,
                        Width = "400px",
                        Height = "400px",
                        Rotation = -3.6 * dial,
                    },
                open ? null : Help()));
    }

    // tg: Safe.jsx:42
    private static UiNode Dialer(TguiContext c, UiValue data)
    {
        bool open = data.Truthy("open");
        bool locked = data.Truthy("locked");
        bool broken = data.Truthy("broken");

        // tg: Safe.jsx:45  the dial button of an amount: "Left" turns the dial down ('turnright'), "Right" up ('turnleft')
        UiNode DialButton(int amount, bool right) => new UiNode(UiKind.Button)
        {
            Key = (right ? "right" : "left") + ":" + amount,
            Width = "80px",
            Disabled = open || (right && !locked) || broken,
            Icon = "arrow-" + (right ? "right" : "left"),
            Text = (right ? "Right " : "Left ") + amount,
            OnClick = () => c.Act(!right ? "turnright" : "turnleft", ("num", amount)),
        };

        return new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.Button)
            {
                Key = "open",
                Disabled = locked && !broken,
                Icon = open ? "lock" : "lock-open",
                Text = open ? "Close" : "Open",
                OnClick = () => c.Act("open"),
            },
            new UiNode(UiKind.Stack) { Key = "dialer", Fill = true }.With(
                new UiNode(UiKind.Stack).With(DialButton(50, false), DialButton(10, false), DialButton(1, false)),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Box) { Key = "dial-number", Bold = true, FontSize = "24px", Color = "#bbbbbb", TextAlign = "center", Text = TguiFormat.JsNumber(data.Num("dial")) }),
                new UiNode(UiKind.Stack).With(DialButton(1, true), DialButton(10, true), DialButton(50, true))));
    }

    // tg: Safe.jsx:78  the items of the open safe, each a button that takes it out
    private static UiNode Contents(TguiContext c, UiValue data)
    {
        var list = new UiNode(UiKind.Stack) { Key = "contents", Vertical = true, Fill = true };
        IReadOnlyList<UiValue> contents = data.Arr("contents");
        for (int index = 0; index < contents.Count; index++)
        {
            UiValue item = contents[index];
            int place = index + 1;
            list.With(new UiNode(UiKind.ImageButton)
            {
                Key = "item:" + place,
                DmIcon = item.Str("icon"),
                DmIconState = item.Str("sprite"),
                OnClick = () => c.Act("retrieve", ("index", place)),
            }.With(new UiNode(UiKind.Box) { Text = item.Str("name") }));
        }
        return list;
    }

    // tg: Safe.jsx:101
    private static UiNode Help() =>
        new UiNode(UiKind.Section) { Key = "help", Title = "Safe opening instructions (because you all keep forgetting)" }.With(
            new UiNode(UiKind.Box) { Wrap = true, Text = "1. Turn the dial left to the first number.\n"
                + "2. Turn the dial right to the second number.\n"
                + "3. Continue repeating this process for each number, switching between left and right each time.\n"
                + "4. Open the safe." },
            new UiNode(UiKind.Box) { Bold = true, Wrap = true, Text = "To lock fully, turn the dial to the left after closing the safe." });
}

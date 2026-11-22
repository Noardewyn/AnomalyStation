using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NumberInputModal.tsx, AlertModal.tsx,
// common/InputButtons.tsx and common/Loader.tsx (M5 slice 3, D148): the windows of tgui_input_number and tgui_alert;
// ListInputWindow/index.tsx and ListInputModal.tsx (M5x slice 4): the window of tgui_input_list; TextInputModal.tsx (M8z, D241): the
// window of tgui_input_text; ColorPickerModal/index.tsx (M10x slice D5, DV209): the window of tgui_color_picker.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NumberInputModal
{
    // tg: tgui/packages/tgui/interfaces/NumberInputModal.tsx:28
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double init_value = data.Num("init_value");
        bool large_buttons = data.Truthy("large_buttons");
        double max_value = data.Num("max_value", 10000);
        string message = data.Str("message");
        double min_value = data.Num("min_value");
        bool round_value = data.Truthy("round_value");
        double timeout = data.Num("timeout");
        string title = data.Str("title");

        // tg: :41  const [value, setValue] = useState(init_value); const [isValid, setIsValid] = useState(true);
        double value = c.UseState("value", init_value);
        bool isValid = c.UseState("isValid", true);

        // Dynamically changes the window height based on the message.
        double windowHeight =
            140 +
            (message.Length > 30 ? Math.Ceiling(message.Length / 3.0) : 0) +
            (message.Length > 0 && large_buttons ? 5 : 0);

        // tg: :51  handleKeyDown: Enter submits (when valid), Escape cancels
        c.OnEnter = () =>
        {
            if (c.UseState("isValid", true))
                c.Act("submit", ("entry", c.UseState("value", init_value)));
        };

        // tg: :61
        return c.Window(270, windowHeight, title,
            timeout != 0 ? Loader.Render(timeout) : null,
            new UiNode(UiKind.Section) { Fill = true }.With(
                new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                        new UiNode(UiKind.Box) { Color = "label", Text = message }),
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Stack) { Fill = true }.With(
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                            {
                                Key = "min",
                                Disabled = value == min_value,
                                Icon = "angle-double-left",
                                OnClick = () => c.SetState("value", min_value),
                                Tooltip = min_value != 0 ? $"Min ({TguiFormat.JsNumber(min_value)})" : "Min",
                            }),
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                            {
                                Key = "down",
                                Icon = "angle-down",
                                Disabled = value <= min_value,
                                OnClick = () => c.SetState("value", c.UseState("value", init_value) - 1),
                            }),
                            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.NumberInput)
                            {
                                Key = "entry",
                                AutoFocus = true,
                                Fluid = true,
                                AllowFloats = !round_value,
                                MinValue = min_value,
                                MaxValue = max_value,
                                Value = value,
                                // tg: onChange={setValue}: a state change, so the buttons that depend on the value (down at the minimum, up at the
                                // maximum, reset) are drawn for the typed one
                                OnChange = v => c.SetState("value", v),
                                OnValidation = valid => c.SetState("isValid", valid),
                                // tg: :51  the window's Enter, which the focused field takes first here
                                OnEnter = c.OnEnter,
                            }),
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                            {
                                Key = "up",
                                Icon = "angle-up",
                                Disabled = value >= max_value,
                                OnClick = () => c.SetState("value", c.UseState("value", init_value) + 1),
                            }),
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                            {
                                Key = "max",
                                Disabled = value == max_value,
                                Icon = "angle-double-right",
                                OnClick = () => c.SetState("value", max_value),
                                Tooltip = max_value != 0 ? $"Max ({TguiFormat.JsNumber(max_value)})" : "Max",
                            }),
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                            {
                                Key = "reset",
                                Disabled = value == init_value,
                                Icon = "redo",
                                OnClick = () => c.SetState("value", init_value),
                                Tooltip = init_value != 0 ? $"Reset ({TguiFormat.JsNumber(init_value)})" : "Reset",
                            }))),
                    new UiNode(UiKind.StackItem).With(
                        InputButtons.Render(c, () => c.UseState("value", init_value), disabled: !isValid)))));
    }
}

public static class TextInputModal
{
    // tg: tgui/packages/tgui/interfaces/TextInputModal.tsx:21
    private static string SanitizeMultiline(string toSanitize) => System.Text.RegularExpressions.Regex.Replace(toSanitize, "(\n|\r\n){3,}", "\n\n");

    // tg: tgui/packages/tgui/interfaces/TextInputModal.tsx:25
    private static string RemoveAllSkiplines(string toSanitize) => System.Text.RegularExpressions.Regex.Replace(toSanitize, "[\r\n]+", "");

    // tg: tgui/packages/tgui/interfaces/TextInputModal.tsx:29
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool large_buttons = data.Truthy("large_buttons");
        int max_length = (int)data.Num("max_length");
        string message = data.Str("message");
        bool multiline = data.Truthy("multiline");
        string placeholder = data.Str("placeholder");
        double timeout = data.Num("timeout");
        string title = data.Str("title");

        // tg: :41  const [input, setInput] = useState(placeholder || '');
        string input = c.UseState("input", placeholder);

        // tg: :43
        void OnType(string value)
        {
            if (value == c.UseState("input", placeholder))
                return;
            c.SetState("input", multiline ? SanitizeMultiline(value) : RemoveAllSkiplines(value));
        }

        bool visualMultiline = multiline || input.Length >= 30;
        // Dynamically changes the window height based on the message.
        double windowHeight =
            135 +
            (message.Length > 30 ? Math.Ceiling(message.Length / 4.0) : 0) +
            (visualMultiline ? 75 : 0) +
            (message.Length > 0 && large_buttons ? 5 : 0);

        // tg: :61  handleKeyDown: Enter submits (Shift+Enter is a new line of a multiline box); tg's Escape cancel is the menu's key here (D282)
        c.OnEnter = () => c.Act("submit", ("entry", c.UseState("input", placeholder)));

        // This is the length of the input in terms of unicode code points.
        // Should be equivalent to what length_char would output for the same string in BYOND.
        int char_length = new System.Globalization.StringInfo(input).LengthInTextElements;

        // tg: :75  (the host's title bar, resize row and border take 25 px of tgui's height, as StripMenu's HostChrome: DV158)
        return c.Window(325, windowHeight + 25, title,
            timeout != 0 ? Loader.Render(timeout) : null,
            new UiNode(UiKind.Section) { Fill = true }.With(
                new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Box) { Color = "label", Text = message }),
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                        // DEVIATION(DV158): tgui-core's TextArea is the kit's one-line Input (the kit has no text area); a long entry does
                        // not wrap, the window still grows as tg's
                        new UiNode(UiKind.Input)
                        {
                            Key = "entry",
                            AutoFocus = true,
                            Fluid = true,
                            MaxLength = max_length,
                            Tooltip = "Type something...",
                            TextValue = input,
                            OnInput = OnType,
                            OnEnter = c.OnEnter,
                        }),
                    new UiNode(UiKind.StackItem).With(
                        InputButtons.Render(c, () => c.UseState("input", placeholder), message: $"{char_length}/{(max_length != 0 ? max_length.ToString(System.Globalization.CultureInfo.InvariantCulture) : "\u221e")}")))));
    }
}

public static class ListInputWindow
{
    // tg: tgui/packages/tgui/interfaces/ListInputWindow/index.tsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        List<string> items = data.Arr("items").Select(i => i.AsStr()).ToList();
        string message = data.Str("message");
        string init_value = data.Str("init_value");
        bool large_buttons = data.Truthy("large_buttons");
        double timeout = data.Num("timeout");
        string title = data.Str("title");

        // Dynamically changes the window height based on the message.
        double windowHeight = 325 + Math.Ceiling(message.Length / 3.0) + (large_buttons ? 5 : 0);

        return c.Window(325, windowHeight, title,
            timeout != 0 ? Loader.Render(timeout) : null,
            ListInputModal(c, items, init_value, message,
                entry => c.Act("submit", ("entry", entry)),
                () => c.Act("cancel")));
    }

    // tg: tgui/packages/tgui/interfaces/ListInputWindow/ListInputModal.tsx:22
    private static UiNode ListInputModal(TguiContext c, List<string> items, string default_item, string message, Action<string> on_selected, Action on_cancel)
    {
        int selected = c.UseState("selected", items.IndexOf(default_item));
        bool searchBarVisible = c.UseState("searchBarVisible", items.Count > 9);
        string searchQuery = c.UseState("searchQuery", "");

        List<string> filteredItems = items.Where(item => item != null && item.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        string Entry(int at) => at >= 0 && at < filteredItems.Count ? filteredItems[at] : null;

        // User presses up or down on keyboard
        // Simulates clicking an item
        c.OnArrow = step =>
        {
            int now = c.UseState("selected", items.IndexOf(default_item));
            int len = filteredItems.Count - 1;
            if (step > 0)
                c.SetState("selected", now == len ? 0 : now + 1);
            else
                c.SetState("selected", now == 0 ? len : now - 1);
        };
        c.OnEnter = () => on_selected(Entry(c.UseState("selected", items.IndexOf(default_item))));
        // tg's hotkey mode (a letter jumps to the first item it starts) is not drawn: letters move the player (DV44)

        // User types into search bar
        void OnSearch(string query)
        {
            if (query == c.UseState("searchQuery", ""))
                return;
            c.Local.SetQuiet("searchQuery", query);
            c.SetState("selected", 0);
        }
        // User presses the search button
        void OnSearchBarToggle()
        {
            c.Local.SetQuiet("searchQuery", "");
            c.SetState("searchBarVisible", !c.UseState("searchBarVisible", items.Count > 9));
        }

        return new UiNode(UiKind.Section)
        {
            Fill = true,
            Title = message,
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "mode",
                Icon = searchBarVisible ? "search" : "font",
                Selected = true,
                Tooltip = searchBarVisible
                    ? "Search Mode. Type to search or use arrow keys to select manually."
                    : "Hotkey Mode. Type a letter to jump to the first match. Enter to select.",
                OnClick = OnSearchBarToggle,
            },
        }.With(
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    ListDisplay(filteredItems, index => { if (index != c.UseState("selected", -1)) c.SetState("selected", index); }, on_selected, selected)),
                searchBarVisible
                    ? new UiNode(UiKind.Input)
                    {
                        Key = "search",
                        Fluid = true,
                        Tooltip = "Search...",
                        TextValue = searchQuery,
                        OnInput = OnSearch,
                        OnEnter = () => on_selected(Entry(c.UseState("selected", 0))),
                    }
                    : null,
                new UiNode(UiKind.StackItem).With(
                    InputButtons.Render(c, () => Entry(selected), on_submit: () => on_selected(Entry(c.UseState("selected", items.IndexOf(default_item)))), on_cancel: on_cancel))));
    }

    // tg: tgui/packages/tgui/interfaces/ListInputWindow/ListInputModal.tsx:191
    private static UiNode ListDisplay(List<string> filteredItems, Action<int> onClick, Action<string> onDoubleClick, int selected)
    {
        return new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(
            filteredItems.Select((item, index) => new UiNode(UiKind.Button)
            {
                Key = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Color = "transparent",
                Fluid = true,
                OnClick = () => onClick(index),
                OnDoubleClick = () => onDoubleClick(item),
                Selected = index == selected,
                Text = item.Length > 0 ? char.ToUpperInvariant(item[0]) + item[1..] : item,
            }));
    }
}

public static class AlertModal
{
    // tg: tgui/packages/tgui/interfaces/AlertModal.tsx:26
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool autofocus = data.Truthy("autofocus");
        List<string> buttons = data.Arr("buttons").Select(b => b.AsStr()).ToList();
        bool large_buttons = data.Truthy("large_buttons");
        string message = data.Str("message");
        double timeout = data.Num("timeout");
        string title = data.Str("title");

        int selected = c.UseState("selected", 0);
        double windowWidth = 345 + (buttons.Count > 2 ? 55 : 0);
        // very accurate estimate of padding for each num of buttons
        double paddingMagicNumber = buttons.Count == 0 ? 0 : 67.0 / buttons.Count + 23;
        // At least one of the buttons has a long text message (textWidth: Verdana at 12 or 14 px, estimated at 0.6 em a character)
        bool isVerbose = buttons.Count > 0 && buttons.Any(button =>
            TextWidth(button, large_buttons ? 14 : 12) > windowWidth / buttons.Count - paddingMagicNumber);
        double largeSpacing = isVerbose && large_buttons ? 20 : 15;
        // Dynamically sets window dimensions
        double windowHeight =
            120 +
            (isVerbose ? largeSpacing * buttons.Count : 0) +
            (message.Length > 30 ? Math.Ceiling(message.Length / 4.0) : 0) +
            (message.Length > 0 && large_buttons ? 5 : 0);

        // tg: :68  keyDownHandler: Space or Enter choose the selected button, Escape cancels (Left/Right/Tab move the selection)
        c.OnEnter = () =>
        {
            int at = c.UseState("selected", 0);
            if (at >= 0 && at < buttons.Count)
                c.Act("choose", ("choice", buttons[at]));
        };
        _ = autofocus; // tgui's <Autofocus/>: the window takes the keyboard focus as it opens (the host focuses every new window)

        // tg: :98
        return c.Window(windowWidth, windowHeight, title,
            timeout != 0 ? Loader.Render(timeout) : null,
            new UiNode(UiKind.Section) { Fill = true }.With(
                new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Box) { Color = "label", Text = message, Wrap = true }), // HTML's flow wraps the message
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                        isVerbose ? VerticalButtons(c, buttons, selected) : HorizontalButtons(c, buttons, selected)))));
    }

    /// <summary>The width of the text in pixels, estimated (tgui measures it with a canvas in Verdana).</summary>
    public static double TextWidth(string text, double fontSize) => (text?.Length ?? 0) * fontSize * 0.62;

    // tg: tgui/packages/tgui/interfaces/AlertModal.tsx:131
    private static UiNode HorizontalButtons(TguiContext c, List<string> buttons, int selected)
    {
        bool large_buttons = c.Data.Truthy("large_buttons");
        bool swapped_buttons = c.Data.Truthy("swapped_buttons");
        return new UiNode(UiKind.Stack) { Fill = true, Justify = "space-around", Reverse = !swapped_buttons }.With(
            buttons.Select((button, index) => new UiNode(UiKind.StackItem) { Key = index.ToString(System.Globalization.CultureInfo.InvariantCulture), Grow = large_buttons ? 1 : 0 }.With(
                new UiNode(UiKind.Button)
                {
                    Fluid = large_buttons,
                    MinWidth = 5,
                    OnClick = () => c.Act("choose", ("choice", button)),
                    Selected = selected == index,
                    TextAlign = "center",
                    Text = !large_buttons ? button : button.ToUpperInvariant(),
                })));
    }

    // tg: tgui/packages/tgui/interfaces/AlertModal.tsx:166
    private static UiNode VerticalButtons(TguiContext c, List<string> buttons, int selected)
    {
        bool large_buttons = c.Data.Truthy("large_buttons");
        bool swapped_buttons = c.Data.Truthy("swapped_buttons");
        return new UiNode(UiKind.Stack) { Align = "center", Fill = true, Justify = "space-around", Reverse = !swapped_buttons, Vertical = true }.With(
            buttons.Select((button, index) => new UiNode(UiKind.StackItem) { Key = index.ToString(System.Globalization.CultureInfo.InvariantCulture), Grow = 1, Width = large_buttons ? "100%" : null }.With(
                new UiNode(UiKind.Button)
                {
                    Fluid = true,
                    MinWidth = 20,
                    OnClick = () => c.Act("choose", ("choice", button)),
                    Selected = selected == index,
                    TextAlign = "center",
                    Text = !large_buttons ? button : button.ToUpperInvariant(),
                })));
    }
}

// tg: tgui/packages/tgui/interfaces/ColorPickerModal/index.tsx (M10x slice D5): the window of tgui_color_picker.
// DEVIATION(DV209): Godot's ColorPicker where tg draws its ColorSelector (the hue and saturation squares, the HSV and RGB sliders, the
// hex field, the presets, ColorSetter.tsx), the window sized for it; the InputButtons submit the colour picked (#rrggbb).
public static class ColorPickerModal
{
    // tg: tgui/packages/tgui/interfaces/ColorPickerModal/index.tsx:28
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double timeout = data.Num("timeout");
        string message = data.Str("message");
        string default_color = data.Str("default_color", "#000000");
        string title = data.Str("title");
        if (string.IsNullOrEmpty(title))
            title = "Colour Editor";
        if (string.IsNullOrEmpty(default_color))
            default_color = "#000000";

        // the colour picked so far: kept quietly, the picker shows it already (a redraw would rebuild the picker mid-drag)
        string Picked() => c.Local.Get("color", default_color);

        return c.Window(360, string.IsNullOrEmpty(message) ? 600 : 635, title,
            timeout != 0 ? Loader.Render(timeout) : null,
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                string.IsNullOrEmpty(message) ? null : new UiNode(UiKind.StackItem) { Key = "message" }.With(new UiNode(UiKind.Section) { Fill = true }.With(
                    new UiNode(UiKind.Box) { Color = "label", Text = message })),
                new UiNode(UiKind.StackItem) { Key = "selector", Grow = 1 }.With(new UiNode(UiKind.Section) { Fill = true }.With(
                    new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                        new UiNode(UiKind.StackItem) { Key = "picker", Grow = 1 }.With(new UiNode(UiKind.ColorPicker)
                        {
                            // DEVIATION(DV230): each colour picked is sent as "preview" too, which a caller may show at once
                            Key = "picker", Color = Picked(), OnInput = hex => { c.Local.SetQuiet("color", hex); c.Act("preview", ("entry", hex)); },
                        }),
                        new UiNode(UiKind.StackItem) { Key = "buttons" }.With(InputButtons.Render(c, Picked)))))));
    }
}

public static class InputButtons
{
    // tg: tgui/packages/tgui/interfaces/common/InputButtons.tsx:20
    public static UiNode Render(TguiContext c, Func<object> input, string message = null, Action on_submit = null, Action on_cancel = null, bool disabled = false)
    {
        bool large_buttons = c.Data.Truthy("large_buttons");
        bool swapped_buttons = c.Data.Truthy("swapped_buttons");
        Action on_submit_actual = on_submit ?? (() => c.Act("submit", ("entry", input())));
        Action on_cancel_actual = on_cancel ?? (() => c.Act("cancel"));
        var submitButton = new UiNode(UiKind.Button)
        {
            Key = "submit",
            Color = "good",
            Disabled = disabled,
            Fluid = large_buttons,
            Height = large_buttons ? 2 : null,
            OnClick = on_submit_actual,
            TextAlign = "center",
            Tooltip = large_buttons ? message : null,
            Width = !large_buttons ? 6 : null,
            Text = large_buttons ? "SUBMIT" : "Submit",
        };
        var cancelButton = new UiNode(UiKind.Button)
        {
            Key = "cancel",
            Color = "bad",
            Fluid = large_buttons,
            Height = large_buttons ? 2 : null,
            OnClick = on_cancel_actual,
            TextAlign = "center",
            Width = !large_buttons ? 6 : null,
            Text = large_buttons ? "CANCEL" : "Cancel",
        };
        // tg: :73  <Flex direction={!swapped_buttons ? 'row' : 'row-reverse'} justify="space-around">
        return new UiNode(UiKind.Stack) { Align = "center", Fill = true, Justify = "space-around", Reverse = swapped_buttons }.With(
            new UiNode(UiKind.StackItem) { Grow = large_buttons ? 1 : 0 }.With(cancelButton),
            !large_buttons && !string.IsNullOrEmpty(message)
                ? new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Color = "label", TextAlign = "center", Text = message })
                : null,
            new UiNode(UiKind.StackItem) { Grow = large_buttons ? 1 : 0 }.With(submitButton));
    }
}

public static class Loader
{
    // tg: tgui/packages/tgui/interfaces/common/Loader.tsx:4
    public static UiNode Render(double value) => new UiNode(UiKind.Loader) { Value = TguiFormat.Clamp01(value) };
}

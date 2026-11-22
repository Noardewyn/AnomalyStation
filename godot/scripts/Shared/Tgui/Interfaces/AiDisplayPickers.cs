using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AiCoreDisplayPicker.tsx and AiStatusDisplayPicker.tsx
// (silicons SI4, D326): the AI's core display and status display pickers, a searchable list of picture buttons. The current picture's
// framed box is the picture.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AiDisplayPickers
{
    // tg: AiCoreDisplayPicker.tsx:32
    public static UiNode RenderCore(TguiContext c)
    {
        string searchTerm = c.UseState("searchTerm", "");
        IReadOnlyList<UiValue> options = c.Data.Get("options")?.Items ?? Array.Empty<UiValue>();
        List<UiValue> filtered = Filter(options, searchTerm);
        return c.Window(500, 600, "AI Core Display Options", new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "current" }.With(Current(c, "Current AI Core Display", c.Data.Str("current_display"), "1.2em")),
            new UiNode(UiKind.StackItem) { Key = "search" }.With(Search(c, searchTerm)),
            new UiNode(UiKind.StackItem) { Key = "options", Grow = 1 }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "list" }.With(new UiNode(UiKind.Section) { Title = "AI Core Display Options" }.With(OptionsList(c, filtered))),
                filtered.Count == 0 ? NoMatch(searchTerm) : null))));
    }

    // tg: AiStatusDisplayPicker.tsx:35
    public static UiNode RenderStatus(TguiContext c)
    {
        string searchTerm = c.UseState("searchTerm", "");
        IReadOnlyList<UiValue> options = c.Data.Get("options")?.Items ?? Array.Empty<UiValue>();
        List<UiValue> filtered = Filter(options, searchTerm);
        // Separate original emotions and new AI core options
        List<UiValue> originalOptions = filtered.Where(o => o.Truthy("is_original")).ToList();
        List<UiValue> newOptions = filtered.Where(o => !o.Truthy("is_original")).ToList();
        string emotion = c.Data.Str("current_emotion");
        return c.Window(500, 600, "AI Status Display Options", new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "current" }.With(Current(c, "Current Display", string.IsNullOrEmpty(emotion) ? "No Selection" : emotion, "1.4em")),
            new UiNode(UiKind.StackItem) { Key = "search" }.With(new UiNode(UiKind.Section).With(Search(c, searchTerm))),
            new UiNode(UiKind.StackItem) { Key = "options", Grow = 1 }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                originalOptions.Count > 0 ? new UiNode(UiKind.StackItem) { Key = "original" }.With(new UiNode(UiKind.Section) { Title = "AI Emotions" }.With(OptionsList(c, originalOptions))) : null,
                newOptions.Count > 0 ? new UiNode(UiKind.StackItem) { Key = "new" }.With(new UiNode(UiKind.Section) { Title = "Additional Status Display Options" }.With(OptionsList(c, newOptions))) : null,
                filtered.Count == 0 ? NoMatch(searchTerm) : null))));
    }

    private static List<UiValue> Filter(IReadOnlyList<UiValue> options, string searchTerm)
        => options.Where(o => o.Str("name").Contains(searchTerm ?? "", StringComparison.OrdinalIgnoreCase)).ToList();

    private static UiNode Current(TguiContext c, string title, string name, string fontSize)
    {
        UiValue icon = c.Data.Get("current_icon");
        return new UiNode(UiKind.Section) { Title = title, TextAlign = "center" }.With(
            icon != null && !icon.IsNull ? new UiNode(UiKind.Box) { Key = "icon", DmIcon = icon.Str("icon"), DmIconState = icon.Str("icon_state"), Size = 2 } : null,
            new UiNode(UiKind.Box) { Key = "name", FontSize = fontSize, TextAlign = "center", Bold = true, Color = "good", Text = name });
    }

    private static UiNode Search(TguiContext c, string searchTerm) => new UiNode(UiKind.Input)
    {
        Key = "searchTerm",
        Fluid = true,
        Tooltip = "Search display options...",
        TextValue = searchTerm,
        OnInput = value => c.SetState("searchTerm", value ?? ""),
    };

    private static UiNode NoMatch(string searchTerm)
        => new UiNode(UiKind.StackItem) { Key = "none" }.With(new UiNode(UiKind.Box) { TextAlign = "center", Color = "average", Text = "No options found matching \"" + searchTerm + "\"" });

    // tg: AiCoreDisplayPicker.tsx:117
    private static UiNode OptionsList(TguiContext c, IEnumerable<UiValue> options)
        => new UiNode(UiKind.Fragment).With(options.Select(option =>
        {
            string name = option.Str("name");
            return new UiNode(UiKind.ImageButton)
            {
                Key = name,
                DmIcon = option.Str("icon"),
                DmIconState = option.Str("icon_state"),
                Size = 2, // imageSize 64
                Tooltip = name,
                Text = name,
                OnClick = () => c.Act("select_option", ("option", name)),
            };
        }));
}

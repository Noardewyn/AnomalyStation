using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Folder.tsx (gaps S3, D298: the folders and the biscuit
// cards). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Folder
{
    // tg: Folder.tsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string theme = data.Str("theme", null);
        string bg_color = data.Str("bg_color");
        string folder_name = data.Str("folder_name");
        IReadOnlyList<UiValue> contents = data.Arr("contents");
        IReadOnlyList<UiValue> contents_ref = data.Arr("contents_ref");

        if (!string.IsNullOrEmpty(theme))
            c.WindowTheme = theme;
        var rows = new List<UiNode>();
        if (contents.Count == 0)
        {
            // tg: Folder.tsx:28
            rows.Add(new UiNode(UiKind.Section).With(new UiNode(UiKind.Box) { Color = "lightgrey", TextAlign = "center", Text = "This folder is empty!" }));
        }
        for (int index = 0; index < contents.Count; index++)
        {
            string itemRef = index < contents_ref.Count ? contents_ref[index].AsStr() : "";
            // tg: Folder.tsx:35  <Stack color="black" backgroundColor="white">
            rows.Add(new UiNode(UiKind.Stack) { Key = itemRef }.With(
                new UiNode(UiKind.StackItem) { Grow = 1, Background = "#ffffff", Align = "center" }.With(new UiNode(UiKind.Box) { Color = "black", TextAlign = "center", Text = contents[index].AsStr() }),
                new UiNode(UiKind.StackItem) { Background = "#ffffff" }.With(
                    new UiNode(UiKind.Button) { Key = "examine:" + itemRef, Icon = "search", OnClick = () => c.Act("examine", ("ref", itemRef)) },
                    new UiNode(UiKind.Button) { Key = "remove:" + itemRef, Icon = "eject", OnClick = () => c.Act("remove", ("ref", itemRef)) })));
        }

        // tg: Folder.tsx:20  <Window title={folder_name || 'Folder'} theme={theme} width={400} height={500}>
        //      <Window.Content backgroundColor={bg_color || '#7f7f7f'} scrollable>
        return c.Window(400, 500, string.IsNullOrEmpty(folder_name) ? "Folder" : folder_name,
            PaperworkWindows.Content(string.IsNullOrEmpty(bg_color) ? "#7f7f7f" : bg_color, rows.ToArray()));
    }
}

/// <summary>What the paperwork's windows share (gaps S3): tgui's <c>Window.Content</c> with a background colour, scrolled.</summary>
public static class PaperworkWindows
{
    /// <summary><c>&lt;Window.Content backgroundColor={color} scrollable&gt;</c>: one growing, scrolled item in that colour.</summary>
    public static UiNode Content(string background, params UiNode[] content)
        => new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true, Background = background }.With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(content)));
}

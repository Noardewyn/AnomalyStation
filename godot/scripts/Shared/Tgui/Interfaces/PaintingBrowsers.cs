using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PortraitPicker.jsx and NtosPortraitPrinter.jsx (silicons
// SI4d, D326): the archived paintings, one at a time with the arrows, searched by title or author; the AI picks its portrait, a console
// prints a painting onto a canvas. A painting's picture (tg's paintings_MD5 asset) is the runtime resource rsc/MD5.png (protocol 79).
// The search field sends on Enter where tgui's sends on blur.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PaintingBrowsers
{
    // tg: PortraitPicker.jsx:15
    public static UiNode RenderPortraitPicker(TguiContext c)
    {
        IReadOnlyList<UiValue> paintings = c.Data.Arr("paintings");
        int index = Index(c, paintings);
        UiValue current = paintings.Count > 0 ? paintings[index] : null;
        return c.Window(400, 406, "Portrait Picker", new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "search" }.With(Search(c)),
            new UiNode(UiKind.StackItem) { Key = "picture", Grow = 2 }.With(Picture(current, 128)),
            new UiNode(UiKind.StackItem) { Key = "arrows" }.With(new UiNode(UiKind.Section).With(Arrows(c, paintings, index,
                new UiNode(UiKind.Button) { Key = "select", Icon = "check", Text = "Select Portrait", Disabled = current == null, OnClick = () => c.Act("select", ("selected", current.Str("ref"))) }))),
            new UiNode(UiKind.StackItem) { Key = "info" }.With(new UiNode(UiKind.NoticeBox) { Color = "info", Text = "Only the 23x23 or 24x24 canvas size art can be displayed. Make sure you read the warning below before embracing the wide wonderful world of artistic expression!" }),
            new UiNode(UiKind.StackItem) { Key = "danger" }.With(new UiNode(UiKind.NoticeBox) { Color = "danger", Text = "WARNING: While Central Command loves art as much as you do, choosing erotic art will lead to severe consequences. Additionally, Central Command reserves the right to request you change your display portrait, for any reason." })));
    }

    // tg: NtosPortraitPrinter.jsx:18
    public static UiNode RenderPortraitPrinter(TguiContext c)
    {
        IReadOnlyList<UiValue> paintings = c.Data.Arr("paintings");
        int index = Index(c, paintings);
        UiValue current = paintings.Count > 0 ? paintings[index] : null;
        bool console = c.Data.Truthy("is_console");
        var actions = new UiNode(UiKind.Stack) { Vertical = true, Align = "center" }.With(
            console ? new UiNode(UiKind.StackItem) { Key = "print" }.With(new UiNode(UiKind.Button) { Key = "print", Icon = "print", Text = "Print Portrait", Disabled = current == null, OnClick = () => c.Act("print", ("selected", current.Str("ref"))) }) : null,
            new UiNode(UiKind.StackItem) { Key = "download" }.With(new UiNode(UiKind.Button) { Key = "download", Icon = "download", Text = "Download", Disabled = current == null, OnClick = () => c.Act("download", ("selected", current.Str("ref"))) }));
        return NtosWindow.Render(c, 400, 460, "Art Galaxy", new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "search" }.With(Search(c)),
            new UiNode(UiKind.StackItem) { Key = "picture", Grow = 2 }.With(Picture(current, 128)),
            new UiNode(UiKind.StackItem) { Key = "arrows" }.With(new UiNode(UiKind.Section).With(Arrows(c, paintings, index, actions)))));
    }

    // useState(listIndex), kept inside the list as it shrinks with a search
    private static int Index(TguiContext c, IReadOnlyList<UiValue> paintings) => Math.Clamp(c.UseState("listIndex", 0), 0, Math.Max(0, paintings.Count - 1));

    private static UiNode Search(TguiContext c) => new UiNode(UiKind.Section) { Title = "Search" }.With(new UiNode(UiKind.Stack).With(
        new UiNode(UiKind.StackItem) { Key = "field", Grow = 1 }.With(new UiNode(UiKind.Input)
        {
            Key = "search",
            Fluid = true,
            Tooltip = "Search Paintings...",
            TextValue = c.UseState("typed", c.Data.Str("search_string")),
            OnInput = value => c.Local.SetQuiet("typed", value ?? ""),
            OnEnter = () =>
            {
                c.Act("search", ("to_search", c.UseState("typed", "")));
                c.SetState("listIndex", 0);
            },
        }),
        new UiNode(UiKind.StackItem) { Key = "mode" }.With(new UiNode(UiKind.Button)
        {
            Key = "mode",
            Text = c.Data.Str("search_mode"),
            OnClick = () =>
            {
                c.Act("change_search_mode");
                if (!string.IsNullOrEmpty(c.Data.Str("search_string")))
                    c.SetState("listIndex", 0);
            },
        })));

    // The painting's picture (its width by its ratio), title and author, or "No paintings found."
    private static UiNode Picture(UiValue painting, int size)
    {
        if (painting == null)
            return new UiNode(UiKind.Section) { Fill = true }.With(new UiNode(UiKind.Box) { Key = "none", TextAlign = "center", Bold = true, Text = "No paintings found." });
        double ratio = painting.Num("ratio", 1);
        return new UiNode(UiKind.Section) { Fill = true }.With(new UiNode(UiKind.Stack) { Vertical = true, Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "image" }.With(new UiNode(UiKind.Box)
            {
                Key = "image",
                DmIcon = "rsc/" + painting.Str("md5") + ".png",
                DmIconState = "",
                Width = Math.Round(size * ratio) + "px",
                Height = size + "px",
            }),
            new UiNode(UiKind.StackItem) { Key = "title" }.With(new UiNode(UiKind.Box) { Key = "title", Bold = true, Text = painting.Str("title") }),
            new UiNode(UiKind.StackItem) { Key = "author" }.With(new UiNode(UiKind.Box) { Key = "author", Text = "By " + painting.Str("creator") })));
    }

    private static UiNode Arrows(TguiContext c, IReadOnlyList<UiValue> paintings, int index, UiNode middle) => new UiNode(UiKind.Stack) { Justify = "space-between", Align = "center" }.With(
        new UiNode(UiKind.StackItem) { Key = "first" }.With(new UiNode(UiKind.Button) { Key = "first", Icon = "angle-double-left", Disabled = index == 0, OnClick = () => c.SetState("listIndex", 0) }),
        new UiNode(UiKind.StackItem) { Key = "prev" }.With(new UiNode(UiKind.Button) { Key = "prev", Icon = "chevron-left", Disabled = index == 0, OnClick = () => c.SetState("listIndex", index - 1) }),
        new UiNode(UiKind.StackItem) { Key = "middle", Grow = 1 }.With(middle),
        new UiNode(UiKind.StackItem) { Key = "next" }.With(new UiNode(UiKind.Button) { Key = "next", Icon = "chevron-right", Disabled = index >= paintings.Count - 1, OnClick = () => c.SetState("listIndex", index + 1) }),
        new UiNode(UiKind.StackItem) { Key = "last" }.With(new UiNode(UiKind.Button) { Key = "last", Icon = "angle-double-right", Disabled = index >= paintings.Count - 1, OnClick = () => c.SetState("listIndex", paintings.Count - 1) }));
}

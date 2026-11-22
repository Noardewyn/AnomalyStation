using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Canvas.tsx with the parts of common/SpriteEditor a canvas
// uses (silicons SI4d, D326): the painting as a picture of its pixels, the pencil, bucket and eyedropper tools the server allows (a
// pencil click is one point; tg's SpriteEditor also strokes while the button is held), the colour picker, the implement's palette, zoom,
// finalize, and the finished painting with its plaque. The grid overlay and the shift-scroll zoom are not drawn; the plaque is not italic.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Canvas
{
    // tg: SpriteEditor/Types/types.ts: the tool flags
    private const int ToolPencil = 1, ToolDropper = 4, ToolBucket = 8;

    // tg: Canvas.tsx:287
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue editor = data.Get("editorData");
        UiValue sprite = editor?.Get("sprite");
        int width = (int)sprite.Num("width", 11), height = (int)sprite.Num("height", 11);
        double ppu = data.Num("pixelsPerUnit", 9);
        bool finalized = data.Truthy("finalized");
        double zoom = c.UseState("zoom", finalized ? 1.0 : 3.0);
        double renderWidth = ppu * zoom * width, renderHeight = ppu * zoom * height;
        IReadOnlyList<UiValue> rows = sprite.Arr("layers").FirstOrDefault()?.Get("data")?.Get("2")?.Items ?? Array.Empty<UiValue>();
        UiPixels pixels = Pixels(rows, width, height);
        return finalized
            ? Finalized(c, data, pixels, renderWidth, renderHeight, zoom, ppu)
            : Editable(c, data, editor, pixels, width, height, renderWidth, renderHeight, zoom, ppu);
    }

    // tg: Canvas.tsx:43  ZoomButtons
    private static UiNode ZoomButtons(TguiContext c, double zoom, double ppu) => new UiNode(UiKind.Stack) { Key = "zoom" }.With(
        new UiNode(UiKind.StackItem) { Key = "out" }.With(new UiNode(UiKind.Button) { Key = "out", Icon = "search-minus", Tooltip = "Zoom Out", Disabled = zoom <= 1, OnClick = () => c.SetState("zoom", Math.Max(1, zoom - 1 / ppu)) }),
        new UiNode(UiKind.StackItem) { Key = "in" }.With(new UiNode(UiKind.Button) { Key = "in", Icon = "search-plus", Tooltip = "Zoom In", Disabled = zoom >= 3, OnClick = () => c.SetState("zoom", Math.Min(3, zoom + 1 / ppu)) }));

    // tg: Canvas.tsx:96  EditableCanvas
    private static UiNode Editable(TguiContext c, UiValue data, UiValue editor, UiPixels pixels, int width, int height, double renderWidth, double renderHeight, double zoom, double ppu)
    {
        bool editable = data.Truthy("editable"), allowColorPicker = data.Truthy("allowColorPicker");
        int toolFlags = (int)editor.Num("toolFlags");
        string selected = editor.Str("serverSelectedColor");
        bool usingImplement = editable && !string.IsNullOrEmpty(selected);
        string tool = c.UseState("tool", "pencil");
        var sidebar = new List<UiNode>();
        if (allowColorPicker)
            sidebar.Add(new UiNode(UiKind.StackItem) { Key = "picker" }.With(new UiNode(UiKind.ColorPicker)
            {
                Key = "picker",
                TextValue = selected,
                OnInput = colour => c.Act(editor.Str("onSelectServerColor", "onSelectColor"), ("color", colour)),
            }));
        int maxColors = (int)editor.Num("maxServerColors");
        if (usingImplement && maxColors > 1)
            sidebar.Add(new UiNode(UiKind.StackItem) { Key = "palette", Grow = 1 }.With(Palette(c, editor, selected, maxColors)));
        bool hasSidebar = sidebar.Count > 0;

        var toolbar = new UiNode(UiKind.Stack) { Key = "tools" };
        if (editable)
        {
            foreach ((string name, string icon, int flag) in new[] { ("pencil", "pencil", ToolPencil), ("bucket", "fill-drip", ToolBucket), ("dropper", "eye-dropper", ToolDropper) })
            {
                if ((toolFlags & flag) == 0)
                    continue;
                string chosen = name;
                toolbar.Children.Add(new UiNode(UiKind.StackItem) { Key = name }.With(new UiNode(UiKind.Button)
                {
                    Key = name,
                    Icon = icon,
                    Tooltip = char.ToUpperInvariant(name[0]) + name[1..],
                    Selected = tool == name,
                    OnClick = () => c.SetState("tool", chosen),
                }));
            }
        }

        var picture = new UiNode(UiKind.Box)
        {
            Key = "canvas",
            Pixels = pixels,
            Width = renderWidth + "px",
            Height = renderHeight + "px",
            OnClickAt = editable ? (x, y) => Paint(c, editor, pixels, tool, selected, width, height, renderWidth, renderHeight, x, y) : null,
        };
        var main = new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "bar" }.With(new UiNode(UiKind.Stack) { Fill = true, Justify = "space-around" }.With(
                editable ? new UiNode(UiKind.StackItem) { Key = "toolbar" }.With(toolbar) : null,
                editable ? new UiNode(UiKind.StackItem) { Key = "spacer", Grow = 1 } : null,
                new UiNode(UiKind.StackItem) { Key = "zoom" }.With(ZoomButtons(c, zoom, ppu)))),
            new UiNode(UiKind.StackItem) { Key = "picture", Grow = 1, TextAlign = "center" }.With(picture),
            new UiNode(UiKind.StackItem) { Key = "finalize", TextAlign = "center" }.With(
                c.ButtonConfirm("finalize", new UiNode(UiKind.Button) { Key = "finalize", Text = "Finalize", OnClick = () => c.Act("finalize") })));
        UiNode content = hasSidebar
            ? new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "sidebar" }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true, Width = "350px" }.With(sidebar.ToArray())),
                new UiNode(UiKind.StackItem) { Key = "main", Grow = 1 }.With(main))
            : main;
        return c.Window(hasSidebar ? Math.Max(renderWidth, 250) + 400 : renderWidth + 90, hasSidebar ? Math.Max(renderHeight + 110, 350) : renderHeight + 110, null, content);
    }

    // tg: SpriteEditor/index.tsx Palette: the implement's colours, a click picks one, the bin removes it, the plus adds the current colour
    private static UiNode Palette(TguiContext c, UiValue editor, string selected, int maxColors)
    {
        IReadOnlyList<UiValue> colours = editor.Arr("serverPalette");
        var list = new UiNode(UiKind.Section) { Title = "Palette", Fill = true };
        for (int i = 0; i < colours.Count; i++)
        {
            string colour = colours[i].AsStr();
            int index = i + 1;
            list.Children.Add(new UiNode(UiKind.Stack) { Key = "c" + i }.With(
                new UiNode(UiKind.StackItem) { Key = "pick", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "pick",
                    Text = colour,
                    Color = "transparent",
                    Selected = string.Equals(colour, selected, StringComparison.OrdinalIgnoreCase),
                    OnClick = () => c.Act(editor.Str("onSelectServerColor", "onSelectColor"), ("color", colour)),
                }.With(new UiNode(UiKind.ColorBox) { Key = "box", Color = colour })),
                new UiNode(UiKind.StackItem) { Key = "remove" }.With(new UiNode(UiKind.Button) { Key = "remove", Icon = "trash", Tooltip = "Remove", OnClick = () => c.Act(editor.Str("onRemoveServerColor", "onRemovePaletteColor"), ("index", index)) })));
        }
        if (colours.Count < maxColors && !string.IsNullOrEmpty(selected))
            list.Children.Add(new UiNode(UiKind.Button) { Key = "add", Icon = "plus", Text = "Add " + selected, OnClick = () => c.Act(editor.Str("onAddServerColor", "onAddPaletteColor"), ("color", selected)) });
        return list;
    }

    // A click on the picture (x from its left, y from its bottom, in its own pixels): the tool at that cell (tg's Pencil, Bucket,
    // Eyedropper: a transaction of points from the top left, 0-based).
    private static void Paint(TguiContext c, UiValue editor, UiPixels pixels, string tool, string selected, int width, int height, double renderWidth, double renderHeight, double x, double y)
    {
        int px = (int)Math.Floor(x / renderWidth * width), py = height - 1 - (int)Math.Floor(y / renderHeight * height);
        if (px < 0 || py < 0 || px >= width || py >= height)
            return;
        if (tool == "dropper")
        {
            uint p = pixels.Rgba[py * width + px];
            c.Act(editor.Str("onSelectServerColor", "onSelectColor"), ("color", "#" + (p >> 8).ToString("x6", CultureInfo.InvariantCulture)));
            return;
        }
        if (string.IsNullOrEmpty(selected))
            return;
        string colour = selected.Length == 7 ? selected + "ff" : selected;
        UiValue point = UiValue.ListOf(new[] { UiValue.Of(px), UiValue.Of(py) });
        UiValue transaction = tool == "bucket"
            ? UiValue.ObjectOf(("type", UiValue.Of("bucket")), ("name", UiValue.Of("Flood Fill")), ("layer", UiValue.Of(1)), ("dir", UiValue.Of("2")), ("color", UiValue.Of(colour)), ("point", point))
            : UiValue.ObjectOf(("type", UiValue.Of("pencil")), ("name", UiValue.Of("Pencil")), ("layer", UiValue.Of(1)), ("dir", UiValue.Of("2")), ("color", UiValue.Of(colour)), ("points", UiValue.ListOf(new[] { point })));
        c.Act("spriteEditorCommand", ("command", "transaction"), ("transaction", transaction));
    }

    // tg: Canvas.tsx:213  FinalizedCanvas
    private static UiNode Finalized(TguiContext c, UiValue data, UiPixels pixels, double renderWidth, double renderHeight, double zoom, double ppu)
    {
        bool showPlaque = data.Truthy("showPlaque");
        UiValue meta = data.Get("metadata");
        string date = meta.Str("date");
        string year = DateTime.TryParseExact(date, "ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime when)
            ? " - " + (when.Year + (int)data.Num("year_offset")) : "";
        string patron = meta.Str("patron");
        return c.Window(renderWidth + 90, renderHeight + (showPlaque ? 270 : 90), null, new UiNode(UiKind.Stack) { Fill = true, Vertical = true, Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "zoom" }.With(ZoomButtons(c, zoom, ppu)),
            new UiNode(UiKind.StackItem) { Key = "picture" }.With(new UiNode(UiKind.Box) { Key = "canvas", Pixels = pixels, Width = renderWidth + "px", Height = renderHeight + "px" }),
            showPlaque ? new UiNode(UiKind.StackItem) { Key = "plaque", Width = "60%", Background = "white", Color = "black", TextAlign = "left" }.With(
                new UiNode(UiKind.Box) { Key = "title", FontSize = "18px", Bold = true, Text = System.Net.WebUtility.HtmlDecode(meta.Str("title")) },
                new UiNode(UiKind.Box) { Key = "author", Bold = true, Text = meta.Str("author") + year },
                new UiNode(UiKind.Box) { Key = "medium", Text = meta.Str("medium") },
                new UiNode(UiKind.Box) { Key = "patron", Text = string.IsNullOrEmpty(patron) ? "" : "Sponsored by " + patron }.With(
                    new UiNode(UiKind.Button) { Key = "patronage", Icon = "hand-holding-usd", Color = "transparent", OnClick = () => c.Act("patronage") })) : null));
    }

    // The canvas' colours ("#rrggbbaa" or "#rrggbb", rows from the top) as pixels; a missing colour is transparent.
    private static UiPixels Pixels(IReadOnlyList<UiValue> rows, int width, int height)
    {
        var rgba = new uint[width * height];
        for (int y = 0; y < height && y < rows.Count; y++)
        {
            IReadOnlyList<UiValue> row = rows[y].Items;
            for (int x = 0; x < width && x < row.Count; x++)
                rgba[y * width + x] = Rgba(row[x].AsStr());
        }
        return new UiPixels(width, height, rgba);
    }

    private static uint Rgba(string colour)
    {
        if (string.IsNullOrEmpty(colour) || colour[0] != '#' || !uint.TryParse(colour.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            return 0;
        return colour.Length == 7 ? value << 8 | 0xFF : colour.Length == 9 ? value : 0;
    }
}

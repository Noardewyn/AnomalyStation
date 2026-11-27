using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;

namespace Anomaly.Client.Hud.Tgui;

// M5 slice 3 (D148): the component kit's Godot half. Turns a UiNode tree (the translated interface's elements) into controls in tg's
// theme: Section, Button (and Button.Checkbox), ImageButton, LabeledList, LabeledControls, Stack/Flex, Tabs, Collapsible, NoticeBox,
// Icon, ProgressBar, RoundGauge, Knob, Input, NumberInput, Loader, Table and Modal. The whole tree is rebuilt when the window's data or local state
// changes (tgui re-renders the same way); a text field that had the focus gets it back by its key.

/// <summary>What the renderer needs from its window.</summary>
public sealed class TguiRenderEnv
{
    /// <summary>tg's icon (file, state) as a texture, or null (an ImageButton's picture).</summary>
    public Func<string, string, Texture2D> Icon;

    /// <summary>The window's local state (a collapsible's open flag, a text field's text).</summary>
    public TguiLocalState Local;

    /// <summary>Run by the host on the frame after the build, when every container has laid its content out.</summary>
    public List<Action> AfterLayout = new();

    /// <summary>The unkeyed scrolled boxes built so far: their order names them across rebuilds (<see cref="TguiRender.KeepScroll"/>).</summary>
    public int Scrolls;

    /// <summary>The text fields built, by key (the host gives the focus back after a rebuild).</summary>
    public readonly Dictionary<string, LineEdit> Fields = new(StringComparer.Ordinal);

    /// <summary>The text areas built, by key (as <see cref="Fields"/>: the host gives the focus and the caret back after a rebuild).</summary>
    public readonly Dictionary<string, TextEdit> Areas = new(StringComparer.Ordinal);

    /// <summary>A tgui-core Modal of the tree, built (the host draws it over the window content), or null.</summary>
    public Control Modal;

    /// <summary>M10 slice D: the control of a ByondUi map (the host keeps it across rebuilds), or null for an empty box.</summary>
    public Func<UiNode, Control> MapView;
}

public static class TguiRender
{
    public const int FontSize = 12;
    public const int Spacing = 6;

    public static Control Build(UiNode node, TguiRenderEnv env, bool horizontal = false)
    {
        if (node == null)
            return null;
        Control built = node.Kind switch
        {
            UiKind.Fragment => Container(node.Children, env, horizontal),
            UiKind.Box => Box(node, env),
            UiKind.Button => Button(node, env),
            UiKind.ImageButton => ImageButton(node, env),
            UiKind.Section => Section(node, env),
            UiKind.Stack => Stack(node, env),
            UiKind.StackItem => StackItem(node, env, horizontal),
            UiKind.Divider => node.Rule ? Rule() : Divider(horizontal),
            UiKind.LabeledList => LabeledList(node, env),
            UiKind.LabeledItem => Container(node.Children, env, true),
            UiKind.LabeledControls => LabeledControls(node, env),
            UiKind.LabeledControl => LabeledControl(node, env),
            UiKind.Tabs => Tabs(node, env),
            UiKind.Tab => Tab(node, env),
            UiKind.Collapsible => Collapsible(node, env),
            UiKind.NoticeBox => NoticeBox(node, env),
            UiKind.Table => Table(node, env),
            UiKind.TableRow => Container(node.Children, env, true), // a row outside a table: its cells side by side
            UiKind.TableCell => TableCell(node, env),
            UiKind.Modal => Modal(node, env),
            UiKind.Icon => Icon(node),
            UiKind.ProgressBar => ProgressBar(node),
            UiKind.RoundGauge => new TguiRoundGauge { Node = node },
            UiKind.Knob => new TguiKnob { Node = node },
            UiKind.Slider => new TguiSlider { Node = node },
            UiKind.Chart => new TguiChart { Node = node },
            UiKind.Board => TguiBoard.Build(node, env),
            UiKind.NodeGraph => TguiNodeGraph.Build(node, env), // gaps CI1
            UiKind.GraphNode or UiKind.GraphSlot => Container(node.Children, env, node.Kind == UiKind.GraphSlot), // outside a node graph: its rows
            UiKind.PaperPage => TguiPaperPage.Build(node, env), // gaps S3
            UiKind.ColorBox => ColorBox(node),
            UiKind.Overlay => Overlay(node, env),
            UiKind.ByondUi => env.MapView?.Invoke(node) ?? new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill }, // M10 slice D
            UiKind.Input => Input(node, env),
            UiKind.TextArea => TextArea(node, env),
            UiKind.Dropdown => Dropdown(node),
            UiKind.Html => Html(node, env),
            UiKind.ColorPicker => ColorPickerControl(node),
            UiKind.NumberInput => NumberInput(node, env),
            UiKind.Loader => new TguiLoader { Value = node.Value },
            UiKind.Floating => Floating(node, env),
            _ => new Control(),
        };
        if (!string.IsNullOrEmpty(node.Tooltip) && node.Kind != UiKind.Button)
            built.TooltipText = node.Tooltip;
        ApplySize(built, node);
        if (node.OnDragStart != null || node.OnDrop != null)
            DragAndDrop(built, node);
        return built;
    }

    // ---- drag and drop ----

    /// <summary>The drag a node of a window began: its <see cref="UiNode.OnDragEnd"/>, which <see cref="TguiHost"/> runs at Godot's drag end
    /// (the node's control may be gone by then: a drag start that changes the window's state rebuilds it).</summary>
    public static Action ActiveDragEnd;

    // Godot's drag forwarding: a draggable node starts a drag with its text as the preview; a drop target takes any tgui drag
    private static void DragAndDrop(Control control, UiNode node)
    {
        const string Payload = "tgui";
        Action start = node.OnDragStart, end = node.OnDragEnd, enter = node.OnDragEnter, drop = node.OnDrop;
        if (drop != null && control.MouseFilter == Control.MouseFilterEnum.Ignore)
            control.MouseFilter = Control.MouseFilterEnum.Stop; // a text takes no mouse by default
        bool inside = false;
        control.SetDragForwarding(
            Callable.From((Vector2 _) =>
            {
                if (start == null)
                    return default;
                control.SetDragPreview(Text(node.Text ?? "", null, node.Bold, FontPx(node.FontSize), null));
                ActiveDragEnd = end;
                // tg defers the start (setTimeout) so the browser takes the drag image first: the state change after this frame
                Callable.From(start).CallDeferred();
                return Variant.From(Payload);
            }),
            Callable.From((Vector2 _, Variant data) =>
            {
                bool ours = drop != null && data.VariantType == Variant.Type.String && data.AsString() == Payload;
                if (ours && !inside && enter != null)
                    Callable.From(enter).CallDeferred();
                inside = ours;
                return ours;
            }),
            Callable.From((Vector2 _, Variant data) => drop?.Invoke()));
        if (drop != null)
            control.MouseExited += () => inside = false;
    }

    // ---- containers ----

    private static BoxContainer Container(IEnumerable<UiNode> children, TguiRenderEnv env, bool horizontal, int separation = Spacing)
    {
        BoxContainer box = horizontal ? new HBoxContainer() : new VBoxContainer();
        box.AddThemeConstantOverride("separation", horizontal ? 3 : separation);
        foreach (UiNode child in children)
        {
            Control c = Build(child, env, horizontal);
            if (c != null)
                box.AddChild(c);
        }
        return box;
    }

    private static Control Box(UiNode node, TguiRenderEnv env)
    {
        // M5 slice 8: a box drawn with one of tg's sprite sheet classes (PersonalCrafting's atoms and tools)
        if (node.DmIcon != null || node.Pixels != null)
            return Picture(node, env);
        if (node.Children.Count == 0)
        {
            Label text = Text(node.Text ?? "", node.Color, node.Bold, FontPx(node.FontSize), node.TextAlign);
            if (node.Wrap) // a long text that wraps at the width of its column (HTML's normal flow)
            {
                text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                text.CustomMinimumSize = new Vector2(160, 0);
                text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            }
            if (node.Underline) // tgui's dotted underline: the text drawn lighter (a Godot label has no underline)
                text.AddThemeColorOverride("font_color", (TguiTheme.Named(node.Color) ?? TguiTheme.Text).Lightened(0.25f));
            return text;
        }
        // tg's Canister places its regulator buttons beside the knob with absolute positions: a knob and its buttons go in a row,
        // the buttons stacked.
        if (node.Children[0].Kind == UiKind.Knob && node.Children.Skip(1).All(c => c.Kind == UiKind.Button))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 2);
            row.AddChild(Build(node.Children[0], env));
            row.AddChild(Container(node.Children.Skip(1), env, false, 2));
            return row;
        }
        Control content = Container(node.Children, env, false);
        if (node.Background == null)
            return content;
        // silicons SI1: a box with tgui's backgroundColor (LawRack's coloured slots) is a panel of that colour around its children
        var panel = new PanelContainer();
        StyleBoxFlat style = Flat(TguiTheme.Named(node.Background) ?? TguiTheme.Hex(node.Background), 4);
        style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(content);
        return panel;
    }

    private static Control Stack(UiNode node, TguiRenderEnv env)
    {
        bool horizontal = !node.Vertical;
        if (node.Wrap && horizontal) // M7 slice D172: tgui's <Stack wrap>, the items in rows (the RPD's pipe previews)
        {
            var flow = new HFlowContainer();
            flow.AddThemeConstantOverride("h_separation", Spacing);
            flow.AddThemeConstantOverride("v_separation", Spacing);
            foreach (UiNode child in node.Children)
            {
                Control c = Build(child, env, true);
                if (c != null)
                    flow.AddChild(c);
            }
            flow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            return flow;
        }
        BoxContainer box = horizontal ? new HBoxContainer() : new VBoxContainer();
        box.AddThemeConstantOverride("separation", Spacing);
        IEnumerable<UiNode> children = node.Reverse ? Enumerable.Reverse(node.Children) : node.Children;
        bool around = node.Justify == "space-around";
        if (around)
            box.AddChild(Spacer(horizontal));
        foreach (UiNode child in children)
        {
            Control c = Build(child, env, horizontal);
            if (c == null)
                continue;
            box.AddChild(c);
            if (around)
                box.AddChild(Spacer(horizontal));
        }
        if (node.Fill)
        {
            box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        }
        if (node.Align == "center")
            box.Alignment = BoxContainer.AlignmentMode.Center;
        return box;
    }

    // M10x slice D5 (DV209): Godot's ColorPicker, without alpha (tg's colours are #rrggbb), each change passed on as #rrggbb
    private static Control ColorPickerControl(UiNode node)
    {
        var picker = new ColorPicker
        {
            Color = Color.FromHtml(string.IsNullOrEmpty(node.Color) ? "#000000" : node.Color),
            EditAlpha = false,
            SamplerVisible = false, // the owner (2026-10-28): no screen sampler (on macOS it asks for screen recording)
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        picker.ColorChanged += color => node.OnInput?.Invoke("#" + color.ToHtml(false));
        return picker;
    }

    // Silicons SI4d: a picture of raw pixels (the canvas being painted).
    private static Texture2D PixelsTexture(UiPixels pixels)
    {
        if (pixels.Width < 1 || pixels.Height < 1 || pixels.Rgba == null || pixels.Rgba.Length != pixels.Width * pixels.Height)
            return null;
        var bytes = new byte[pixels.Rgba.Length * 4];
        for (int i = 0; i < pixels.Rgba.Length; i++)
        {
            uint p = pixels.Rgba[i];
            bytes[i * 4] = (byte)(p >> 24);
            bytes[i * 4 + 1] = (byte)(p >> 16);
            bytes[i * 4 + 2] = (byte)(p >> 8);
            bytes[i * 4 + 3] = (byte)p;
        }
        return ImageTexture.CreateFromImage(Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, bytes));
    }

    // M5 slice 8: a sprite of tg's .dmi files (an atom's picture in tg's crafting sprite sheet), 32 px times the node's Size.
    private static Control Picture(UiNode node, TguiRenderEnv env)
    {
        float side = 32f * (float)Math.Max(0.25, node.Size);
        // M9 slice E: a picture of its own pixel size (NtOS's 40x22 header GIFs, tgui's <img>), else a square of 32 px times Size
        Vector2 box = UiSize.Of(node.Width) is UiSize { Pixels: > 0 } w && UiSize.Of(node.Height) is UiSize { Pixels: > 0 } h
            ? new Vector2((float)w.Pixels, (float)h.Pixels) : new Vector2(side, side);
        Texture2D texture = node.Pixels is UiPixels pixels ? PixelsTexture(pixels) : env.Icon?.Invoke(node.DmIcon, node.DmIconState);
        if (node.CropToPixels && texture?.GetImage() is Image image && image.GetUsedRect() is Rect2I used && used.HasArea())
            texture = new AtlasTexture { Atlas = texture, Region = used };
        var rect = new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = box,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = PictureTint(node),
        };
        if (node.Rotation != 0) // CSS rotate about the middle (the safe's dial)
        {
            rect.Resized += () => rect.PivotOffset = rect.Size / 2;
            rect.RotationDegrees = (float)node.Rotation;
        }
        // gaps P1: a picture that reads where it is clicked and hovered (tgui's BodyZoneSelector), the point from its left and bottom
        if (node.OnClickAt != null || node.OnHoverAt != null)
        {
            Action<double, double> clickAt = node.OnClickAt, hoverAt = node.OnHoverAt;
            rect.MouseFilter = Control.MouseFilterEnum.Stop;
            rect.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press)
                    clickAt?.Invoke(press.Position.X, rect.Size.Y - press.Position.Y);
                else if (e is InputEventMouseMotion motion)
                    hoverAt?.Invoke(motion.Position.X, rect.Size.Y - motion.Position.Y);
            };
            if (hoverAt != null)
                rect.MouseExited += () => hoverAt(-1, -1);
        }
        return rect;
    }

    // A picture with a hex colour is that colour's tint (tg's uni_icon(..., color=), which multiplies the icon by it: the Orion Trail's mood
    // faces, the virtual pet's body), with the node's opacity
    private static Color PictureTint(UiNode node)
    {
        Color tint = node.Color is string hex && hex.StartsWith('#') ? TguiTheme.Hex(hex) : new Color(1, 1, 1);
        tint.A = (float)(node.Opacity ?? 1);
        return tint;
    }

    // M5 slice 8: tgui-core's Floating: the anchor (children) shows the content (Content) in a popup under it while hovered; the
    // popup stays while the mouse is over it (tgui's hoverSafePolygon) and closes when the mouse leaves both.
    private static Control Floating(UiNode node, TguiRenderEnv env)
    {
        Control anchor = Container(node.Children, env, false);
        anchor.MouseFilter = Control.MouseFilterEnum.Stop;
        var popup = new PopupPanel { Exclusive = false, Unfocusable = true };
        StyleBoxFlat style = Flat(new Color(0.15f, 0.15f, 0.15f, 0.97f), 4);
        style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 6;
        popup.AddThemeStyleboxOverride("panel", style);
        anchor.AddChild(popup);
        anchor.MouseEntered += () =>
        {
            // built on the first hover (UiNode.Content): an ingredient's recipe floats its own ingredients' recipes in turn
            if (popup.GetChildCount() == 0 && node.Content?.Invoke() is UiNode shown)
            {
                Control content = Build(shown, env);
                content.CustomMinimumSize = new Vector2(320, 0);
                popup.AddChild(content);
            }
            Vector2 at = anchor.GetScreenPosition() + new Vector2(0, anchor.Size.Y);
            popup.Popup(new Rect2I((Vector2I)at, Vector2I.Zero));
        };
        return anchor;
    }

    private static Control Spacer(bool horizontal)
    {
        var spacer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        if (horizontal) spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        else spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        return spacer;
    }

    // A scrolled box keeps its place across rebuilds (every server update redraws the window: the list stays where it was), as the
    // browser keeps a tgui element's scroll while React updates it. The place is given back while the content gets its size, without
    // a frame at the top; nested containers grow in several passes, so it is tried at each until the layout ends (AfterLayout).
    public static void KeepScroll(ScrollContainer scroll, string name, TguiRenderEnv env)
    {
        if (env.Local == null)
            return;
        string key = "scroll:" + (name ?? "#" + env.Scrolls++);
        KeepBar(scroll.GetVScrollBar(), key, env);
        KeepBar(scroll.GetHScrollBar(), key + ":x", env); // a box wider than the window (NtOS's hub) keeps its sideways place too
    }

    private static void KeepBar(ScrollBar bar, string key, TguiRenderEnv env)
    {
        double saved = env.Local.Get(key, 0.0);
        bool restoring = saved > 0;
        bar.ValueChanged += value =>
        {
            if (!restoring)
                env.Local.SetQuiet(key, value);
        };
        if (!restoring)
            return;
        bar.Changed += () =>
        {
            if (restoring && bar.MaxValue > 0) // else the content is not laid out yet
                bar.Value = saved; // clamped to the content's size if the list got shorter
        };
        env.AfterLayout.Add(() => restoring = false);
    }

    private static Control StackItem(UiNode node, TguiRenderEnv env, bool horizontal)
    {
        // tg's Stack.Item is a div: block children (Sections: NtOS hub's program cards; fluid Buttons, which tgui-core draws as blocks:
        // the nuke's keypad columns, syndicate N1a) go one under another in a row's item too
        bool block = node.Children.Any(child => child.Kind == UiKind.Section || child.Kind == UiKind.Button && child.Fluid);
        Control inner = node.Children.Count == 1 ? Build(node.Children[0], env, horizontal) : Container(node.Children, env, horizontal && !block);
        // the item's own text and icon (tg's text children of a Stack.Item: NT CIMS's entry titles and gas names, NtOS's clock)
        if (node.Text != null || node.Icon != null)
        {
            Label own = Text(Label(node.Icon, node.Text), node.Color, node.Bold, FontPx(node.FontSize), node.TextAlign);
            if (node.Wrap)
                own.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (node.Opacity is double opacity)
                own.Modulate = new Color(1, 1, 1, (float)opacity);
            if (node.Children.Count == 0)
                inner = own;
            else
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 3);
                row.AddChild(own);
                row.AddChild(inner);
                inner = row;
            }
        }
        // M5 slice 8: a scrolled item (tgui's style overflowY auto: PersonalCrafting's tab list)
        if (node.Scrollable)
        {
            // CSS's overflow-y auto makes overflow-x auto too: a grown item scrolls a too-wide content itself, so the window does not
            // scroll sideways and its header (NtOS's minimize and close) stays in view; an item sized by its content keeps that width
            var scroll = new ScrollContainer { HorizontalScrollMode = node.Grow > 0 ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.Disabled };
            KeepScroll(scroll, node.Key, env);
            inner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(inner);
            inner = scroll;
        }
        // a coloured item (tgui's style background: the preferences' job rows in their department's colour)
        Container item = node.Background != null ? new PanelContainer() : new MarginContainer();
        if (node.Background != null)
            item.AddThemeStyleboxOverride("panel", Flat(TguiTheme.Named(node.Background) ?? TguiTheme.Hex(node.Background), 0));
        item.AddChild(inner);
        if (node.Grow > 0)
        {
            item.SizeFlagsStretchRatio = (float)node.Grow;
            if (horizontal) item.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            else item.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            inner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            inner.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        }
        else if (!horizontal)
            item.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (node.Align == "center")
            item.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return item;
    }

    // M5 slice 8: tgui-core's own Divider component (a horizontal rule across its box, centred in a row's height)
    private static Control Rule()
    {
        var line = new ColorRect { Color = TguiTheme.Divider, MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 2) };
        line.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var holder = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        holder.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        holder.AddChild(line);
        return holder;
    }

    private static Control Divider(bool horizontal)
    {
        var line = new ColorRect { Color = TguiTheme.Divider, MouseFilter = Control.MouseFilterEnum.Ignore };
        line.CustomMinimumSize = horizontal ? new Vector2(1, 0) : new Vector2(0, 1);
        return line;
    }

    private static Control Section(UiNode node, TguiRenderEnv env)
    {
        var panel = new PanelContainer();
        // M10 slice F: tgui-core's StyleableSection (JobSelection's departments): its background colour, its title's text colour
        panel.AddThemeStyleboxOverride("panel", Flat(node.Background != null ? (TguiTheme.Named(node.Background) ?? TguiTheme.Hex(node.Background)) : TguiTheme.SectionBg, 0));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        panel.AddChild(column);
        if (node.Title != null || node.Buttons != null || node.LabelNode != null)
        {
            var head = new MarginContainer();
            SetMargins(head, 6, 4, 6, 3);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            Control title;
            if (node.LabelNode != null) // a title that is a node (tgui's title prop given a fragment)
                title = Build(node.LabelNode, env, false);
            else
            {
                Label text = Text(node.Title ?? "", null, true, 14, null);
                if (node.Color != null)
                    text.AddThemeColorOverride("font_color", TguiTheme.Named(node.Color) ?? TguiTheme.Hex(node.Color)); // a named colour (the orbit menu's "label") or a hex
                title = text;
            }
            title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(title);
            if (node.Buttons != null)
                row.AddChild(Build(node.Buttons, env, true));
            head.AddChild(row);
            column.AddChild(head);
            var rule = new ColorRect { Color = TguiTheme.SectionRule, CustomMinimumSize = new Vector2(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore };
            column.AddChild(rule);
        }
        var body = new MarginContainer();
        SetMargins(body, 6, 6, 6, 6);
        Control content = Container(node.Children, env, false, 4);
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (node.Scrollable)
        {
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            KeepScroll(scroll, node.Key, env);
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.AddChild(content);
            body.AddChild(scroll);
            body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        }
        else
            body.AddChild(content);
        column.AddChild(body);
        // M5 slice 8: a clickable section (PersonalCrafting's "Load N more...")
        if (node.OnClick != null)
        {
            Action click = node.OnClick;
            panel.MouseFilter = Control.MouseFilterEnum.Stop;
            panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            panel.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    click();
            };
        }
        if (node.Fill)
        {
            panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        }
        return panel;
    }

    private static Control LabeledList(UiNode node, TguiRenderEnv env)
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 4);
        foreach (UiNode item in node.Children.Where(c => c.Kind == UiKind.LabeledItem))
        {
            var (labelNode, labelText, buttons) = item.LabeledRow();
            Control label = labelNode != null ? Build(labelNode, env, true) : Text(labelText, item.LabelColor ?? "label", false, FontSize, null);
            label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            grid.AddChild(label);
            // a row whose value is plain text (tgui's LabeledList.Item with a string child, coloured by its color)
            Control content = item.Children.Count == 0 && item.Text != null ? Text(item.Text, item.Color, false, FontSize, null)
                : item.Wrap ? Flow(item.Children, env) : Container(item.Children, env, true);
            content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            if (buttons != null)
            {
                // tgui-core's buttons cell: after the content, right-aligned, as narrow as its buttons
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                row.AddChild(content);
                Control cell = Build(buttons, env, true);
                cell.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                row.AddChild(cell);
                content = row;
                content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            }
            grid.AddChild(content);
        }
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return grid;
    }

    // a row of inline elements that wraps at its width (tgui's inline buttons: the scrubber's gas filters)
    private static Control Flow(IEnumerable<UiNode> children, TguiRenderEnv env)
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 3);
        flow.AddThemeConstantOverride("v_separation", 3);
        foreach (UiNode child in children)
        {
            Control c = Build(child, env, true);
            if (c != null)
                flow.AddChild(c);
        }
        return flow;
    }

    private static Control LabeledControls(UiNode node, TguiRenderEnv env)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(Spacer(true));
        foreach (UiNode child in node.Children)
        {
            row.AddChild(Build(child, env, true));
            row.AddChild(Spacer(true));
        }
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return row;
    }

    private static Control LabeledControl(UiNode node, TguiRenderEnv env)
    {
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        column.AddThemeConstantOverride("separation", 2);
        Control content = Container(node.Children, env, true);
        if (content is BoxContainer box) box.Alignment = BoxContainer.AlignmentMode.Center;
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        column.AddChild(content);
        Label label = Text(node.Label ?? "", "label", false, FontSize, "center");
        column.AddChild(label);
        return column;
    }

    private static Control Tabs(UiNode node, TguiRenderEnv env)
    {
        BoxContainer box = node.Vertical ? new VBoxContainer() : new HBoxContainer();
        box.AddThemeConstantOverride("separation", 1);
        foreach (UiNode tab in node.Children)
            box.AddChild(Build(tab, env, !node.Vertical));
        return box;
    }

    private static Control Tab(UiNode node, TguiRenderEnv env)
    {
        var button = new Button { Text = node.Children.Count > 0 ? "" : Label(node.Icon, node.Text), Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None };
        // M5 slice 8: a tab with its own content (PersonalCrafting's category and material tabs) draws it inside the tab
        if (node.Children.Count > 0)
        {
            var inside = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            SetMargins(inside, 8, 3, 8, 3);
            Control content = Container(node.Children, env, false);
            IgnoreMouse(content);
            inside.AddChild(content);
            inside.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            button.AddChild(inside);
            button.CustomMinimumSize = new Vector2(0, Math.Max(22, content.GetCombinedMinimumSize().Y + 6));
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        Color text = TguiTheme.Named(node.Color) ?? TguiTheme.Text;
        Color bg = node.Selected ? new Color(1, 1, 1, 0.075f) : new Color(0, 0, 0, 0);
        StyleBoxFlat normal = Flat(bg, 0);
        if (node.Selected)
        {
            normal.BorderWidthLeft = 3;
            normal.BorderColor = TguiTheme.Named(node.Color) ?? TguiTheme.Hex("#d4dfec");
        }
        normal.ContentMarginLeft = 8;
        normal.ContentMarginRight = 8;
        normal.ContentMarginTop = 3;
        normal.ContentMarginBottom = 3;
        StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(1, 1, 1, 0.1f);
        Style(button, normal, hover, normal, text, node.Selected ? TguiTheme.Text : text.Lightened(0.1f));
        if (node.OnClick != null)
            button.Pressed += node.OnClick;
        return button;
    }

    private static Control Collapsible(UiNode node, TguiRenderEnv env)
    {
        string key = "open:" + (node.Key ?? node.Title);
        bool open = env.Local.Get(key, node.Open);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        // tgui-core's Collapsible passes its color and bold to its button (M10x slice C: the orbit menu's sections)
        var header = new UiNode(UiKind.Button) { Icon = open ? "chevron-down" : "chevron-right", Text = node.Title, Fluid = true, Color = node.Color, Bold = node.Bold, OnClick = () => env.Local.Set(key, !open) };
        Control headerButton = Button(header, env);
        headerButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (node.Buttons != null)
        {
            // tgui-core's Collapsible draws its buttons prop after the button, in the header row (gaps P3: the DNA console's
            // genetic makeup buffers and advanced injectors)
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            row.AddChild(headerButton);
            row.AddChild(Build(node.Buttons, env, true));
            column.AddChild(row);
        }
        else
            column.AddChild(headerButton);
        if (open)
        {
            var body = new PanelContainer();
            StyleBoxFlat style = Flat(new Color(62 / 255f, 97 / 255f, 137 / 255f, 0.15f), 0);
            style.BorderWidthLeft = style.BorderWidthRight = style.BorderWidthBottom = 1;
            style.BorderColor = new Color(1, 1, 1, 0.1f);
            style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 6;
            body.AddThemeStyleboxOverride("panel", style);
            body.AddChild(Container(node.Children, env, false, 2));
            column.AddChild(body);
        }
        return column;
    }

    // ---- leaves ----

    private static Control Button(UiNode node, TguiRenderEnv env)
    {
        string icon = node.Checkbox is bool on ? (on ? "check-square-o" : "square-o") : node.Icon;
        var button = new Button
        {
            Text = Label(icon, node.Text),
            Disabled = node.Disabled,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = node.Tooltip ?? "",
            ClipText = UiSize.Of(node.Width) is UiSize { Pixels: > 0 }, // a sized button cuts its text (tgui's overflow hidden); others fit it
            Alignment = node.TextAlign switch { "center" => HorizontalAlignment.Center, "right" => HorizontalAlignment.Right, _ => node.Fluid ? HorizontalAlignment.Left : HorizontalAlignment.Center },
        };
        // tgui-core's Button.Checkbox is a button whose icon is the square and whose selected is its checked.
        bool selected = node.Selected || node.Checkbox == true;
        Color[] gradient = node.Disabled ? null : TguiTheme.LinearGradient(node.Background); // drawn behind a clear box (gaps package G4)
        Color bg = node.Disabled ? TguiTheme.ButtonDisabled.Darkened(0.55f) : gradient != null ? new Color(0, 0, 0, 0) : TguiTheme.Named(node.Background) ?? TguiTheme.ButtonColor(node.Color, selected);
        bool transparent = node.Color == "transparent" && !selected && node.Background == null;
        Color text = node.Disabled ? new Color(1, 1, 1, 0.45f) : TguiTheme.DarkText(node.Color) && !selected ? TguiTheme.Hex("#000000") : transparent ? TguiTheme.TransparentText : TguiTheme.Text;
        StyleBoxFlat normal = Flat(bg, 2);
        normal.ContentMarginLeft = normal.ContentMarginRight = 6;
        normal.ContentMarginTop = normal.ContentMarginBottom = 1;
        StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = transparent || gradient != null ? new Color(1, 1, 1, 0.12f) : bg.Lightened(0.15f);
        StyleBoxFlat pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = bg.Darkened(0.1f);
        if (node.Circular) // tgui-core's circular: border-radius 50%, with the job priorities' 1 px dark border
            foreach (StyleBoxFlat s in new[] { normal, hover, pressed })
            {
                s.SetCornerRadiusAll(64);
                s.SetBorderWidthAll(1);
                s.BorderColor = new Color(0, 0, 0, 0.3f);
            }
        if (node.StrokeColor != null) // a button's outline (gaps P3: the DNA sequencer's outline-solid outline-color-orange classes)
            foreach (StyleBoxFlat s in new[] { normal, hover, pressed })
            {
                s.SetBorderWidthAll(2);
                s.BorderColor = TguiTheme.Named(node.StrokeColor) ?? TguiTheme.Hex(node.StrokeColor);
            }
        Style(button, normal, hover, pressed, text, transparent ? TguiTheme.Text : text);
        if (gradient != null) // tgui's style.background linear-gradient, left to right, under the button's own box
        {
            var offsets = new float[gradient.Length];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = (float)i / (offsets.Length - 1);
            var behind = new TextureRect
            {
                Texture = new GradientTexture2D { Gradient = new Gradient { Offsets = offsets, Colors = gradient }, FillFrom = Vector2.Zero, FillTo = new Vector2(1, 0), Width = 64, Height = 1 },
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                ShowBehindParent = true,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            behind.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            button.AddChild(behind);
        }
        if (node.Slashed) // the Off priority's --off::after: a 2 px line, 80 % of the height, turned 40 degrees about its middle
        {
            var line = new ColorRect { Color = new Color(0, 0, 0, 0.2f), MouseFilter = Control.MouseFilterEnum.Ignore };
            button.AddChild(line);
            button.Resized += () =>
            {
                Vector2 size = button.Size;
                line.Size = new Vector2(2, size.Y * 0.8f);
                line.PivotOffset = line.Size / 2;
                line.Position = size / 2 - line.Size / 2;
                line.RotationDegrees = 40;
            };
        }
        button.CustomMinimumSize = new Vector2(0, 20);
        // a button sized in pixels on both sides (the StripMenu's 20 x 20 alternate action) holds its icon as a centred label inside, so that
        // a symbol drawn from a fallback font cannot make it taller than its size
        if (node.Children.Count == 0 && UiSize.Of(node.Width) is UiSize { Pixels: > 0 } && UiSize.Of(node.Height) is UiSize { Pixels: > 0 } && !string.IsNullOrEmpty(icon))
        {
            button.Text = "";
            Label glyph = Text(TguiTheme.Glyph(icon), node.Color, false, FontPx(node.FontSize), "center");
            glyph.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            button.AddChild(glyph);
        }
        if (node.Children.Count > 0) // tgui-core's Button takes children as its content: laid over each other (the StripMenu's slot)
        {
            button.Text = "";
            Control content = OverlayContent(node, env, FullRect: true);
            button.AddChild(content);
            normal.ContentMarginLeft = normal.ContentMarginRight = normal.ContentMarginTop = normal.ContentMarginBottom = 0;
            // a Godot button's size takes no account of its children: an unsized one (FeatureColorInput's square and "Change") takes its
            // content's, so its box is drawn round it
            if (UiSize.Of(node.Width) is not UiSize { Pixels: > 0 })
                button.Ready += () =>
                {
                    Vector2 size = button.CustomMinimumSize;
                    foreach (Node child in content.GetChildren())
                        if (child is Control c)
                            size = size.Max(c.GetCombinedMinimumSize() + new Vector2(8, 2));
                    button.CustomMinimumSize = size;
                };
        }
        if (node.Opacity is double opacity)
            button.Modulate = new Color(1, 1, 1, (float)opacity);
        int px = FontPx(node.FontSize);
        button.AddThemeFontSizeOverride("font_size", px);
        if (node.Bold) button.AddThemeColorOverride("font_outline_color", text);
        if (node.Fluid) button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (node.OnClick != null && !node.Disabled)
            button.Pressed += node.OnClick;
        if (node.OnClickWith != null && !node.Disabled)
        {
            // gaps package G1 (the loot panel): the click reads the keys held, a right click is its own
            Action<UiClick> clicked = node.OnClickWith;
            button.Pressed += () => clicked(new UiClick(Godot.Input.IsPhysicalKeyPressed(Key.Alt), Godot.Input.IsPhysicalKeyPressed(Key.Ctrl), Godot.Input.IsPhysicalKeyPressed(Key.Shift), false));
            button.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
                    clicked(new UiClick(false, false, false, true));
            };
        }
        if (node.OnDoubleClick != null && !node.Disabled)
        {
            Action twice = node.OnDoubleClick;
            button.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, DoubleClick: true, Pressed: true })
                    twice();
            };
        }
        return button;
    }

    // A box whose children lie over each other (UiKind.Overlay): the ones with an Anchor sit in a corner at their size.
    private static Control Overlay(UiNode node, TguiRenderEnv env) => OverlayContent(node, env, FullRect: false);

    private static Control OverlayContent(UiNode node, TguiRenderEnv env, bool FullRect)
    {
        var area = new Control { MouseFilter = FullRect ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Pass };
        if (FullRect)
            area.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        else
            area.CustomMinimumSize = new Vector2(UiSize.Of(node.Width) is UiSize { Pixels: > 0 } w ? (float)w.Pixels : 0, UiSize.Of(node.Height) is UiSize { Pixels: > 0 } h ? (float)h.Pixels : 0);
        foreach (UiNode child in node.Children)
        {
            Control c = Build(child, env);
            if (c == null)
                continue;
            if (child.Anchor == null)
            {
                c.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                if (c is not Godot.Button && child.OnClickAt == null && child.OnHoverAt == null)
                    c.MouseFilter = Control.MouseFilterEnum.Ignore;
            }
            else
            {
                bool right = child.Anchor.EndsWith("right", StringComparison.Ordinal), bottom = child.Anchor.StartsWith("bottom", StringComparison.Ordinal);
                Vector2 size = c.GetCombinedMinimumSize();
                if (UiSize.Of(child.Width) is UiSize { Pixels: > 0 } cw && UiSize.Of(child.Height) is UiSize { Pixels: > 0 } ch)
                    size = new Vector2((float)cw.Pixels, (float)ch.Pixels);
                c.AnchorLeft = c.AnchorRight = right ? 1 : 0;
                c.AnchorTop = c.AnchorBottom = bottom ? 1 : 0;
                // a top-left child stands PosX, PosY pixels in (CSS position: absolute with top and left: the NTOS radar's pointer)
                c.OffsetLeft = right ? -size.X : (float)child.PosX;
                c.OffsetRight = right ? 0 : (float)child.PosX + size.X;
                c.OffsetTop = bottom ? -size.Y : (float)child.PosY;
                c.OffsetBottom = bottom ? 0 : (float)child.PosY + size.Y;
            }
            area.AddChild(c);
        }
        return area;
    }

    private static Control ImageButton(UiNode node, TguiRenderEnv env)
    {
        if (node.ButtonsAlt != null && !node.Fluid)
            return ImageCard(node, env);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 2);
        // M7 slice D172: a picture-only button (no content) is tgui-core's square ImageButton of imageSize pixels (Size x 32); a selected one
        // takes the selected colour (the RPD's chosen preview)
        bool square = node.Children.Count == 0;
        float side = square ? 32f * (float)Math.Max(0.25, node.Size) : 32f;
        var button = new Button { Disabled = node.Disabled, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = square ? new Vector2(side + 6, side + 6) : new Vector2(0, 38), TooltipText = node.Tooltip ?? "" };
        Color bg = node.Disabled ? new Color(0.2f, 0.2f, 0.2f, 0.6f) : node.Selected ? TguiTheme.ButtonSelected : new Color(1, 1, 1, 0.06f);
        StyleBoxFlat normal = Flat(bg, 2);
        StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(1, 1, 1, 0.12f);
        Style(button, normal, hover, normal, TguiTheme.Text, TguiTheme.Text);
        button.SizeFlagsHorizontal = square ? Control.SizeFlags.ShrinkBegin : Control.SizeFlags.ExpandFill;
        var inside = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = square ? BoxContainer.AlignmentMode.Center : BoxContainer.AlignmentMode.Begin };
        inside.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        inside.AddThemeConstantOverride("separation", 6);
        var picture = new TextureRect
        {
            Texture = env.Icon?.Invoke(node.DmIcon, node.DmIconState),
            CustomMinimumSize = new Vector2(side, side),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = node.Disabled ? new Color(1, 1, 1, 0.4f) : Colors.White,
        };
        inside.AddChild(picture);
        Control content = Container(node.Children, env, true);
        content.MouseFilter = Control.MouseFilterEnum.Ignore;
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        IgnoreMouse(content);
        inside.AddChild(content);
        button.AddChild(inside);
        if (node.OnClick != null && !node.Disabled)
            button.Pressed += node.OnClick;
        row.AddChild(button);
        if (node.Buttons != null)
        {
            Control sideButtons = Build(node.Buttons, env, false);
            row.AddChild(sideButtons);
        }
        if (node.Fluid) row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return row;
    }

    // Gaps S2 (the Vending window's grid): tgui-core's non-fluid ImageButton with buttonsAlt, a card of imageSize (64 px) wide: the picture,
    // the buttonsAlt row (the price and the stock), the content (the name, one line, cut) under it; the side buttons over its top right
    private static Control ImageCard(UiNode node, TguiRenderEnv env)
    {
        float side = 64f * (float)Math.Max(0.25, node.Size);
        var button = new Button { Disabled = node.Disabled, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(side + 10, side + 46), TooltipText = node.Tooltip ?? "" };
        Color bg = node.Disabled ? new Color(0.2f, 0.2f, 0.2f, 0.6f) : node.Selected ? TguiTheme.ButtonSelected : new Color(1, 1, 1, 0.06f);
        StyleBoxFlat normal = Flat(bg, 2);
        StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(1, 1, 1, 0.12f);
        Style(button, normal, hover, normal, TguiTheme.Text, TguiTheme.Text);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        column.OffsetLeft = column.OffsetTop = 4;
        column.OffsetRight = column.OffsetBottom = -4;
        column.AddThemeConstantOverride("separation", 1);
        column.AddChild(new TextureRect
        {
            Texture = env.Icon?.Invoke(node.DmIcon, node.DmIconState),
            CustomMinimumSize = new Vector2(side, side),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = node.Disabled ? new Color(1, 1, 1, 0.4f) : Colors.White,
        });
        Control alt = Build(node.ButtonsAlt, env, false);
        column.AddChild(alt);
        // the buttonsAlt row stays inside the card: at its left and right edges (the price and the stock), its text made smaller to fit (the owner,
        // 2026-11-14: the prices ran into the next card); measured once it is in the window, with the window's font
        alt.Ready += () => FitRow(alt, side);
        Control content = Container(node.Children, env, false);
        content.ClipContents = true;
        content.CustomMinimumSize = new Vector2(side, 0);
        // tgui-core's content: one line cut with an ellipsis (overflow hidden), the whole name in the tooltip; a label's full width no longer widens the card
        foreach (Label name in Labels(content))
        {
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        column.AddChild(content);
        IgnoreMouse(column);
        button.AddChild(column);
        if (node.OnClick != null && !node.Disabled)
            button.Pressed += node.OnClick;
        if (node.Buttons != null)
        {
            Control sideButtons = Build(node.Buttons, env, false);
            sideButtons.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
            button.AddChild(sideButtons);
        }
        return button;
    }

    private static Control NoticeBox(UiNode node, TguiRenderEnv env)
    {
        var panel = new PanelContainer();
        // tgui-core's NoticeBox: tan with black text, or a colour (color="grey", info's blue) with white text
        Color? tinted = node.Color == "info" ? TguiTheme.Hex("#2185d0").Darkened(0.35f) : TguiTheme.Named(node.Color)?.Darkened(0.35f);
        StyleBoxFlat style = Flat(tinted ?? TguiTheme.NoticeBg, 2);
        style.ContentMarginLeft = style.ContentMarginRight = 8;
        style.ContentMarginTop = style.ContentMarginBottom = 5;
        panel.AddThemeStyleboxOverride("panel", style);
        if (node.Children.Count > 0) // a notice with its own content (InterfaceLockNoticeBox's silicon lock row)
            panel.AddChild(Container(node.Children, env, false));
        else
        {
            Label text = Text(node.Text ?? "", tinted != null ? "#ffffff" : "#000000", true, FontSize, node.TextAlign);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            panel.AddChild(text);
        }
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        // M10x slice C: a clickable notice (the orbit menu's critical points of interest)
        if (node.OnClick != null)
        {
            Action click = node.OnClick;
            panel.MouseFilter = Control.MouseFilterEnum.Stop;
            panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            panel.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    click();
            };
        }
        return panel;
    }

    // tgui-core's ColorBox: a square (its Width and Height, else 1em) of the CSS colour, its text centred on it (PowerMonitor's "M");
    // a clickable one is the RapidPipeDispenser's paint swatch with that window's 3 px border, #20b142 when selected, else #222 (M7 D172)
    private static Control ColorBox(UiNode node)
    {
        StyleBoxFlat normal = Flat(TguiTheme.Named(node.Color) ?? new Color(0, 0, 0, 0), 0);
        Control box;
        if (node.OnClick != null)
        {
            normal.BorderWidthLeft = normal.BorderWidthRight = normal.BorderWidthTop = normal.BorderWidthBottom = 3;
            normal.BorderColor = TguiTheme.Hex(node.Selected ? "#20b142" : "#222222");
            StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
            hover.BgColor = normal.BgColor.Lightened(0.15f);
            var button = new Button { FocusMode = Control.FocusModeEnum.None };
            Style(button, normal, hover, normal, TguiTheme.Text, TguiTheme.Text);
            button.Pressed += node.OnClick;
            box = button;
        }
        else
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", normal);
            box = panel;
        }
        box.CustomMinimumSize = new Vector2(UiSize.Of(node.Width) is UiSize { Pixels: > 0 } w ? (float)w.Pixels : FontSize,
            UiSize.Of(node.Height) is UiSize { Pixels: > 0 } h ? (float)h.Pixels : FontSize);
        box.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        if (!string.IsNullOrEmpty(node.Text))
            box.AddChild(Text(node.Text, null, false, FontSize, "center"));
        return box;
    }

    private static Control Icon(UiNode node)
    {
        Label label = Text(TguiTheme.Glyph(node.Icon), node.Color, false, (int)Math.Round(FontSize * Math.Max(1, node.Size) * 1.2), "center");
        label.MouseFilter = Control.MouseFilterEnum.Pass;
        if (node.OnClick != null) // the bot window's settings and toggles are bare icons (SimpleBot.tsx)
        {
            Action click = node.OnClick;
            label.MouseFilter = Control.MouseFilterEnum.Stop;
            label.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            label.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    click();
            };
        }
        if (node.Rotation != 0) // tgui-core's rotation: the glyph turned clockwise about its middle
        {
            label.Resized += () => label.PivotOffset = label.Size / 2;
            label.RotationDegrees = (float)node.Rotation;
        }
        if (node.Spin) // fa-spin: a full clockwise turn every 2 s, linear, for ever
        {
            label.Resized += () => label.PivotOffset = label.Size / 2;
            label.Ready += () => label.CreateTween().SetLoops().TweenProperty(label, "rotation_degrees", 360f, 2.0).From(0f);
        }
        return label;
    }

    // the air alarm window: tgui-core's Table (a grid: a column as wide as its widest cell) and Modal

    private static Control Table(UiNode node, TguiRenderEnv env)
    {
        // a grid, so that a column is as wide as its widest cell in every row (an HTML table's columns); the stripe is drawn per cell
        List<UiNode> rows = node.Children.Where(c => c.Kind == UiKind.TableRow).ToList();
        var grid = new GridContainer { Columns = Math.Max(1, rows.Count == 0 ? 1 : rows.Max(r => r.Children.Count)) };
        grid.AddThemeConstantOverride("h_separation", 0);
        grid.AddThemeConstantOverride("v_separation", 0);
        for (int index = 0; index < rows.Count; index++)
        {
            UiNode row = rows[index];
            Color stripe = row.Candystripe && index % 2 == 1 ? new Color(0, 0, 0, 0.25f) : new Color(0, 0, 0, 0);
            for (int column = 0; column < grid.Columns; column++)
            {
                // a column with a cell of a pixel width keeps that width (an HTML table's), the others share the rest
                double fixedWidth = rows.Select(r => column < r.Children.Count && UiSize.Of(r.Children[column].Width) is UiSize { Pixels: > 0 } w ? w.Pixels : 0).Max();
                var cell = new PanelContainer { SizeFlagsHorizontal = fixedWidth > 0 ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2((float)fixedWidth, 0) };
                cell.AddThemeStyleboxOverride("panel", Flat(stripe, 0));
                if (column < row.Children.Count)
                    cell.AddChild(Build(row.Children[column], env, true));
                grid.AddChild(cell);
            }
        }
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return grid;
    }

    private static Control TableCell(UiNode node, TguiRenderEnv env)
    {
        var cell = new MarginContainer();
        SetMargins(cell, 2, 2, 2, 2);
        if (node.Children.Count == 0)
            cell.AddChild(Text(node.Text ?? "", node.Color, node.Bold, FontSize, node.TextAlign));
        else
        {
            BoxContainer content = Container(node.Children, env, true);
            content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            content.Alignment = node.TextAlign == "center" ? BoxContainer.AlignmentMode.Center : node.TextAlign == "right" ? BoxContainer.AlignmentMode.End : BoxContainer.AlignmentMode.Begin;
            cell.AddChild(content);
        }
        return cell;
    }

    private static Control Modal(UiNode node, TguiRenderEnv env)
    {
        // over the whole content: a dimmed layer with the modal's box in its centre (tgui-core's Modal on its Dimmer)
        var layer = new ColorRect { Color = new Color(0, 0, 0, 0.75f), MouseFilter = Control.MouseFilterEnum.Stop };
        var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centre.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(centre);
        var box = new PanelContainer();
        StyleBoxFlat style = Flat(TguiTheme.WindowBg, 2);
        style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 12;
        box.AddThemeStyleboxOverride("panel", style);
        box.AddChild(Container(node.Children, env, false));
        centre.AddChild(box);
        env.Modal = layer;
        return new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
    }

    private static Control ProgressBar(UiNode node)
    {
        // tgui-core's ProgressBar: the fill is the value scaled to its range; the colour is the first range that holds the value (from
        // inclusive, to exclusive: inRange) unless given; without children the bar says its percentage.
        var bar = new ProgressBar { MinValue = node.MinValue, MaxValue = node.MaxValue, Value = node.Value, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 18) };
        Color fill = TguiTheme.Named(node.Color) ?? TguiTheme.Hex("#4972a1");
        if (node.Color == null && node.Ranges != null)
        {
            foreach ((string color, double from, double to) in node.Ranges)
            {
                if (node.Value >= from && node.Value < to)
                {
                    fill = TguiTheme.Named(color) ?? fill;
                    break;
                }
            }
        }
        bar.AddThemeStyleboxOverride("background", Flat(new Color(0, 0, 0, 0.3f), 2));
        bar.AddThemeStyleboxOverride("fill", Flat(fill, 2));
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        double scaled = (node.Value - node.MinValue) / Math.Max(1e-9, node.MaxValue - node.MinValue);
        Label says = Text(node.Text ?? TguiFormat.ToFixed(scaled * 100) + "%", null, false, FontPx(node.FontSize), "center");
        says.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        says.HorizontalAlignment = HorizontalAlignment.Center;
        says.VerticalAlignment = VerticalAlignment.Center;
        says.MouseFilter = Control.MouseFilterEnum.Ignore;
        bar.AddChild(says);
        return bar;
    }

    private static Control Input(UiNode node, TguiRenderEnv env)
    {
        string key = "input:" + (node.Key ?? "");
        var field = new LineEdit
        {
            Text = env.Local.Get(key, node.TextValue ?? ""),
            MaxLength = node.MaxLength,
            PlaceholderText = node.Tooltip ?? "",
            CustomMinimumSize = new Vector2(0, 20),
            Editable = !node.Disabled,
        };
        StyleInput(field);
        field.TextChanged += text =>
        {
            env.Local.SetQuiet(key, text);
            node.OnInput?.Invoke(text);
        };
        field.TextSubmitted += text =>
        {
            node.OnCommit?.Invoke(text);
            node.OnEnter?.Invoke();
        };
        field.FocusExited += () => node.OnCommit?.Invoke(field.Text);
        AutoFocus(field, node, env, key);
        env.Fields[key] = field;
        if (node.Fluid) field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (!string.IsNullOrEmpty(node.Icon))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            row.AddChild(Text(TguiTheme.Glyph(node.Icon), "label", false, FontSize, null));
            field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(field);
            return row;
        }
        return field;
    }

    // M9 slice E: tgui-core's TextArea (a multi-line field): its text is the window's local state while it is typed, as an Input's
    // M10x slice G6 (DV216): a browse() page, drawn as tg's chat lines are; its links go to the server's Topic
    private static Control Html(UiNode node, TguiRenderEnv env)
    {
        var text = new RichTextLabel
        {
            ScrollActive = true,
            SelectionEnabled = true,
            BbcodeEnabled = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        ChatText.Style(text, 13);
        // DV685: a browse() page's pictures (tg's photo window) between its text pieces, each piece drawn as tg's chat lines
        foreach (HtmlPiece piece in HtmlImages.Split(node.TextValue ?? ""))
        {
            if (piece.Source == null)
            {
                var links = new List<ChatLink>();
                var spans = new List<ChatSpan>();
                string plain = HtmlText.ToPlain(piece.Html, links, spans);
                ChatText.Append(text, plain, spans, links, 13);
            }
            else if (env.Icon?.Invoke(piece.Source, "") is Texture2D picture)
            {
                text.PushParagraph(HorizontalAlignment.Center);
                text.AddImage(picture, piece.Width, piece.Height);
                text.Pop();
            }
        }
        text.MetaClicked += meta => node.OnInput?.Invoke(meta.AsString());
        return text;
    }

    private static Control TextArea(UiNode node, TguiRenderEnv env)
    {
        string key = "input:" + (node.Key ?? "");
        var area = new TextEdit
        {
            Text = env.Local.Get(key, node.TextValue ?? ""),
            PlaceholderText = node.Tooltip ?? "",
            WrapMode = node.Wrap ? TextEdit.LineWrappingMode.Boundary : TextEdit.LineWrappingMode.None,
            CustomMinimumSize = new Vector2(0, UiSize.Of(node.Height) is UiSize { Pixels: > 0 } h ? (float)h.Pixels : 80),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        StyleInput(area);
        area.TextChanged += () =>
        {
            env.Local.SetQuiet(key, area.Text);
            node.OnInput?.Invoke(area.Text);
        };
        if (node.OnEnter != null) // tgui-core's onEnter: Enter sends, Shift+Enter is a new line
        {
            area.GuiInput += e =>
            {
                if (e is InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter, ShiftPressed: false })
                {
                    area.AcceptEvent();
                    node.OnEnter();
                }
            };
        }
        AutoFocus(area, node, env, key);
        env.Areas[key] = area;
        return area;
    }

    // tgui-core's autoFocus (M10x slice G6: the Adminhelp box's text area): once, when the window first shows the field
    private static void AutoFocus(Control field, UiNode node, TguiRenderEnv env, string key)
    {
        if (!node.AutoFocus || env.Local.Get("focused:" + key, false))
            return;
        env.Local.SetQuiet("focused:" + key, true);
        field.Ready += () => field.CallDeferred(Control.MethodName.GrabFocus);
    }

    // M9 slice E: tgui-core's Dropdown: Godot's option button over the options
    private static Control Dropdown(UiNode node)
    {
        if (!string.IsNullOrEmpty(node.Icon))
            return IconDropdown(node);
        var button = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 20), Disabled = node.Disabled };
        IReadOnlyList<string> options = node.Options ?? Array.Empty<string>();
        if (string.IsNullOrEmpty(node.TextValue) || !options.Contains(node.TextValue))
            button.AddItem(node.Tooltip ?? "");
        foreach (string option in options)
            button.AddItem(option);
        button.Selected = string.IsNullOrEmpty(node.TextValue) || !options.Contains(node.TextValue) ? 0 : options.ToList().IndexOf(node.TextValue);
        int offset = button.ItemCount - options.Count;
        DropdownLook(button, node, 6);
        button.ItemSelected += index =>
        {
            int i = (int)index - offset;
            if (i >= 0 && i < options.Count)
                node.OnSelected?.Invoke(options[i]);
        };
        button.AddThemeFontSizeOverride("font_size", FontSize);
        return button;
    }

    // tgui-core's Dropdown__control: a button's box in the dropdown's colour (a default blue one), so it reads as a control, not text
    private static void DropdownLook(Button button, UiNode node, int margin)
    {
        Color bg = node.Disabled ? TguiTheme.ButtonDisabled.Darkened(0.55f) : TguiTheme.ButtonColor(node.Color, false);
        StyleBoxFlat normal = Flat(bg, 2);
        normal.ContentMarginLeft = normal.ContentMarginRight = margin;
        StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = bg.Lightened(0.15f);
        StyleBoxFlat pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = bg.Darkened(0.1f);
        Color text = node.Disabled ? new Color(1, 1, 1, 0.45f) : TguiTheme.DarkText(node.Color) ? TguiTheme.Hex("#000000") : TguiTheme.Text;
        Style(button, normal, hover, pressed, text, text);
    }

    // tgui-core's Dropdown with iconOnly: a button of the icon in the dropdown's colour whose menu lists the options
    private static Control IconDropdown(UiNode node)
    {
        var button = new MenuButton { Text = TguiTheme.Glyph(node.Icon), FocusMode = Control.FocusModeEnum.None, Disabled = node.Disabled, TooltipText = node.Tooltip ?? "", Flat = false };
        DropdownLook(button, node, 4);
        IReadOnlyList<string> options = node.Options ?? Array.Empty<string>();
        PopupMenu menu = button.GetPopup();
        foreach (string option in options)
            menu.AddItem(option);
        menu.IdPressed += id =>
        {
            if (id >= 0 && id < options.Count)
                node.OnSelected?.Invoke(options[(int)id]);
        };
        button.CustomMinimumSize = new Vector2(20, 20);
        button.AddThemeFontSizeOverride("font_size", FontSize);
        return button;
    }

    private static Control NumberInput(UiNode node, TguiRenderEnv env)
    {
        string key = "input:" + (node.Key ?? "");
        bool Valid(string text, out double v) => double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
            && v >= node.MinValue && v <= node.MaxValue && (node.AllowFloats || v == Math.Floor(v));
        // The text being typed outlives a redraw (a keystroke that changes the window's state rebuilds it): kept while it is not a valid number (a
        // half-typed "1" under a minimum of 10, an empty field) or while it is the value shown; a value set otherwise (a button) replaces it.
        string typed = env.Local.Get<string>(key, null);
        string text = typed != null && (!Valid(typed, out double typedValue) || typedValue == node.Value) ? typed : node.Format?.Invoke(node.Value) ?? TguiFormat.JsNumber(node.Value);
        var field = new LineEdit { Text = text, CustomMinimumSize = new Vector2(UiSize.Of(node.Width) is UiSize width ? (float)width.Pixels : 0, 20), Alignment = HorizontalAlignment.Center, Editable = !node.Disabled };
        StyleInput(field);
        if (node.AutoFocus && !env.Local.Get("focused:" + key, false))
        {
            // tgui-core's autoFocus and autoSelect: once, when the window first shows the field
            env.Local.SetQuiet("focused:" + key, true);
            field.Ready += () =>
            {
                field.CallDeferred(Control.MethodName.GrabFocus);
                field.CallDeferred(LineEdit.MethodName.SelectAll);
            };
        }
        if (node.CommitOnly)
        {
            // tgui-core's NumberInput: the typed number is sent once, on Enter or when the field loses the focus
            bool sent = false;
            void Commit(string text)
            {
                if (sent || !Valid(text, out double v) || v == node.Value)
                    return;
                sent = true;
                node.OnChange?.Invoke(v);
            }
            field.TextSubmitted += Commit;
            field.FocusExited += () => Commit(field.Text);
        }
        else
        {
            field.TextChanged += text =>
            {
                env.Local.SetQuiet(key, text);
                bool valid = Valid(text, out double v);
                if (valid)
                    node.OnChange?.Invoke(v);
                node.OnValidation?.Invoke(valid);
            };
            field.TextSubmitted += _ => node.OnEnter?.Invoke();
        }
        env.Fields[key] = field;
        if (!string.IsNullOrEmpty(node.Unit))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            if (node.Fluid) field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(field);
            row.AddChild(Text(node.Unit, "label", false, FontSize, null));
            return row;
        }
        if (node.Fluid) field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return field;
    }

    // ---- helpers ----

    public static Label Text(string text, string color, bool bold, int fontSize, string align)
    {
        var label = new Label { Text = text ?? "", MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", TguiTheme.Named(color) ?? TguiTheme.Text);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        if (bold)
        {
            label.AddThemeConstantOverride("outline_size", 1);
            label.AddThemeColorOverride("font_outline_color", TguiTheme.Named(color) ?? TguiTheme.Text);
        }
        label.HorizontalAlignment = align switch { "center" => HorizontalAlignment.Center, "right" => HorizontalAlignment.Right, _ => HorizontalAlignment.Left };
        return label;
    }

    private static string Label(string icon, string text)
    {
        string glyph = TguiTheme.Glyph(icon);
        if (string.IsNullOrEmpty(text)) return glyph;
        return string.IsNullOrEmpty(glyph) ? text : glyph + " " + text;
    }

    public static StyleBoxFlat Flat(Color color, int radius)
    {
        var style = new StyleBoxFlat { BgColor = color };
        style.SetCornerRadiusAll(radius);
        return style;
    }

    /// <summary>The owner (2026-11-01): how opaque the buttons built now are, as their window (TguiHost.Opacity); their text and pictures stay solid.</summary>
    public static float ButtonOpacity { get; set; } = 1f;

    private static void FadeButton(StyleBox style)
    {
        if (style is StyleBoxFlat flat)
            flat.BgColor = flat.BgColor with { A = flat.BgColor.A * ButtonOpacity };
    }

    /// <summary>A title bar's close button: no box of its own, so the bar can be as low as its text.</summary>
    public static void BareCloseButton(Button close)
    {
        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
            close.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        close.AddThemeFontSizeOverride("font_size", 14);
    }

    private static void Style(Button button, StyleBox normal, StyleBox hover, StyleBox pressed, Color text, Color hoverText)
    {
        FadeButton(normal);
        FadeButton(hover);
        if (pressed != normal)
            FadeButton(pressed);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("disabled", normal);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", text);
        button.AddThemeColorOverride("font_hover_color", hoverText);
        button.AddThemeColorOverride("font_pressed_color", text);
        button.AddThemeColorOverride("font_disabled_color", text);
        button.AddThemeFontSizeOverride("font_size", FontSize);
    }

    private static void StyleInput(Control field)
    {
        StyleBoxFlat style = Flat(TguiTheme.InputBg, 2);
        style.SetBorderWidthAll(1);
        style.BorderColor = TguiTheme.InputBorder.Darkened(0.35f);
        style.ContentMarginLeft = style.ContentMarginRight = 4;
        StyleBoxFlat focus = (StyleBoxFlat)style.Duplicate();
        focus.BorderColor = TguiTheme.InputBorder;
        field.AddThemeStyleboxOverride("normal", style);
        field.AddThemeStyleboxOverride("focus", focus);
        field.AddThemeColorOverride("font_color", TguiTheme.Text);
        field.AddThemeFontSizeOverride("font_size", FontSize);
    }

    private static void SetMargins(MarginContainer m, int left, int top, int right, int bottom)
    {
        m.AddThemeConstantOverride("margin_left", left);
        m.AddThemeConstantOverride("margin_top", top);
        m.AddThemeConstantOverride("margin_right", right);
        m.AddThemeConstantOverride("margin_bottom", bottom);
    }

    private static IEnumerable<Label> Labels(Node node)
    {
        if (node is Label label)
            yield return label;
        foreach (Node child in node.GetChildren())
            foreach (Label inner in Labels(child))
                yield return inner;
    }

    // A row of labels (a card's price and stock) made to fit a width: their font one pixel smaller at a time, down to 7 px; still too wide, cut.
    private static void FitRow(Control row, float width)
    {
        List<Label> labels = Labels(row).ToList();
        if (labels.Count == 0)
            return;
        float gaps = Spacing * (labels.Count - 1);
        float Total() => gaps + labels.Sum(l => l.GetThemeFont("font").GetStringSize(l.Text, HorizontalAlignment.Left, -1, l.GetThemeFontSize("font_size")).X);
        while (Total() > width && labels.Any(l => l.GetThemeFontSize("font_size") > 7))
            foreach (Label l in labels)
                l.AddThemeFontSizeOverride("font_size", Math.Max(7, l.GetThemeFontSize("font_size") - 1));
        if (Total() > width)
            foreach (Label l in labels)
                l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
    }

    private static void IgnoreMouse(Control c)
    {
        c.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (Node child in c.GetChildren())
            if (child is Control cc)
                IgnoreMouse(cc);
    }

    private static int FontPx(object fontSize) => UiSize.Of(fontSize) is UiSize s && s.Pixels > 0 ? (int)Math.Round(s.Pixels) : FontSize;

    private static void ApplySize(Control c, UiNode node)
    {
        if (UiSize.Of(node.Width) is UiSize w)
        {
            if (w.Percent > 0) c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            else c.CustomMinimumSize = new Vector2((float)w.Pixels, c.CustomMinimumSize.Y);
        }
        if (UiSize.Of(node.MinWidth) is UiSize mw && mw.Pixels > 0)
            c.CustomMinimumSize = new Vector2(Math.Max(c.CustomMinimumSize.X, (float)mw.Pixels), c.CustomMinimumSize.Y);
        if (UiSize.Of(node.Height) is UiSize h)
        {
            if (h.Percent > 0) c.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            else c.CustomMinimumSize = new Vector2(c.CustomMinimumSize.X, Math.Max(c.CustomMinimumSize.Y, (float)h.Pixels));
        }
    }
}

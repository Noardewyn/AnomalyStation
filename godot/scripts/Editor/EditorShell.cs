using Godot;

namespace Anomaly.Client.Editor;

/// <summary>
/// The frame of the map editor's interface (docs/ai/specs/map-editor.md 5.3), in the engine's own controls: a menu bar and a tool bar at the
/// top, docks of panels around the 3D view (two at the left, two at the right, one under it), a status bar under them. A dock is a tab
/// container: a panel is a tab that is dragged by its title from one dock to another, and the borders between the docks and the view are
/// dragged to size them. A dock with no panel takes no room. The middle is left free: the mouse there is the 3D view's.
/// </summary>
public sealed partial class EditorShell : CanvasLayer
{
    private const int DockGroup = 71; // the docks' tabs are dragged among themselves only

    private const string LayoutSection = "layout";

    private readonly float _scale;
    private readonly List<PopupMenu> _menus = new();
    private TabContainer[] _docks;
    private SplitContainer _left, _right;
    // The borders that are dragged, by their names in the settings, with the offsets they start at in units of the interface's size. The engine
    // measures a border beside a side that does not stretch from that side's edge of the container: the offset is the side dock's width (the bottom
    // dock's height), below zero when the dock is the last child, and a dock is never narrower than its panel's content. A border between two rows of
    // a side is measured from where their stretch puts it.
    private (string Key, SplitContainer Split, int Default)[] _splits;
    private PanelContainer _tip;
    private Label _tipText;

    /// <param name="scale">The size of the interface: 1 is a 13 pixel font.</param>
    public EditorShell(float scale)
    {
        _scale = Mathf.Clamp(scale, 0.75f, 4f);
        Layer = 60;
        Name = "EditorShell";
        Build();
    }

    /// <summary>The menus' bar.</summary>
    public MenuBar Menu { get; private set; }

    /// <summary>The tool bar's row: the caller puts its buttons there.</summary>
    public HBoxContainer Tools { get; private set; }

    /// <summary>The free middle, over the 3D view.</summary>
    public Control Centre { get; private set; }

    /// <summary>The view's own bar, over its top edge: the caller puts the camera's and the look's switches there.</summary>
    public HFlowContainer ViewBar { get; private set; }

    /// <summary>The docks, each a tab container of panels: two at the left, two at the right, one under the view.</summary>
    public TabContainer LeftTopDock { get; private set; }

    public TabContainer LeftBottomDock { get; private set; }

    public TabContainer RightTopDock { get; private set; }

    public TabContainer RightBottomDock { get; private set; }

    public TabContainer BottomDock { get; private set; }

    /// <summary>Every dock.</summary>
    public IReadOnlyList<TabContainer> Docks => _docks;

    /// <summary>The status bar's texts: what is under the mouse, the last notice, the state of the view.</summary>
    public Label Status { get; private set; }

    public Label Notice { get; private set; }

    public Label State { get; private set; }

    /// <summary>The status bar's line for where the camera is.</summary>
    public Label Camera { get; private set; }

    /// <summary>The interface's theme: a window the editor opens (a question, a form) is given it, for a window takes no theme from a canvas layer.</summary>
    public Theme UiTheme { get; private set; }

    /// <summary>The size of the interface the shell was built with.</summary>
    public float UiScale => _scale;

    private int Px(float value) => Mathf.RoundToInt(value * _scale);

    private void Build()
    {
        UiTheme = BuildTheme();
        var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        column.AddThemeConstantOverride("separation", 0);
        root.AddChild(column);

        var top = new PanelContainer { ThemeTypeVariation = "EditorBar" };
        var topRows = new VBoxContainer();
        topRows.AddThemeConstantOverride("separation", Px(2));
        Menu = new MenuBar { Flat = true, PreferGlobalMenu = false }; // in the window on every system, beside the tools it belongs to
        Tools = new HBoxContainer();
        Tools.AddThemeConstantOverride("separation", Px(6));
        topRows.AddChild(Menu);
        topRows.AddChild(Tools);
        top.AddChild(topRows);
        column.AddChild(top);

        // [ left top over left bottom | [ the view over the bottom dock ] | right top over right bottom ]
        var outer = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        var inner = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        var middle = new VSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        _left = new VSplitContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        _right = new VSplitContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        LeftTopDock = Dock("LeftTop", 180, 80);
        LeftBottomDock = Dock("LeftBottom", 180, 80);
        RightTopDock = Dock("RightTop", 180, 80);
        RightBottomDock = Dock("RightBottom", 180, 80);
        BottomDock = Dock("Bottom", 200, 70);
        _docks = new[] { LeftTopDock, LeftBottomDock, RightTopDock, RightBottomDock, BottomDock };
        foreach (TabContainer side in new[] { LeftTopDock, LeftBottomDock, RightTopDock, RightBottomDock })
            side.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        RightBottomDock.SizeFlagsStretchRatio = 1.6f;
        Centre = new Control { Name = "View", MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        // (it goes onto a second line where the view is narrow, and only its buttons take the mouse)
        ViewBar = new HFlowContainer { Name = "ViewBar", MouseFilter = Control.MouseFilterEnum.Ignore };
        ViewBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        (ViewBar.OffsetLeft, ViewBar.OffsetTop, ViewBar.OffsetRight) = (Px(6), Px(6), -Px(6));
        ViewBar.AddThemeConstantOverride("h_separation", Px(4));
        ViewBar.AddThemeConstantOverride("v_separation", Px(4));
        Centre.AddChild(ViewBar);
        _left.AddChild(LeftTopDock);
        _left.AddChild(LeftBottomDock);
        middle.AddChild(Centre);
        middle.AddChild(BottomDock);
        _right.AddChild(RightTopDock);
        _right.AddChild(RightBottomDock);
        inner.AddChild(middle);
        inner.AddChild(_right);
        outer.AddChild(_left);
        outer.AddChild(inner);
        column.AddChild(outer);
        _splits = new (string, SplitContainer, int)[] { ("left", outer, 270), ("right", inner, -310), ("bottom", middle, -150), ("left_rows", _left, 0), ("right_rows", _right, 0) };
        foreach ((_, SplitContainer split, int offset) in _splits)
            split.SplitOffsets = new[] { Px(offset) };

        var bottom = new PanelContainer { ThemeTypeVariation = "EditorBar" };
        var status = new HBoxContainer();
        status.AddThemeConstantOverride("separation", Px(16));
        Status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        Notice = new Label { ClipText = true, HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        Notice.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
        State = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        Camera = new Label { HorizontalAlignment = HorizontalAlignment.Right, TooltipText = "Where the camera is: the tile it is over, the level that is edited and its height over that level's floor. Home goes to the map's first tile.", MouseFilter = Control.MouseFilterEnum.Stop };
        Camera.AddThemeColorOverride("font_color", new Color(0.62f, 0.63f, 0.66f));
        status.AddChild(Status);
        status.AddChild(Notice);
        status.AddChild(Camera);
        status.AddChild(State);
        bottom.AddChild(status);
        column.AddChild(bottom);

        _tipText = new Label();
        _tip = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, ThemeTypeVariation = "EditorTip", ZIndex = 10 };
        _tip.AddChild(_tipText);
        root.AddChild(_tip);
    }

    private TabContainer Dock(string name, float width, float height)
    {
        var dock = new TabContainer
        {
            Name = name,
            DragToRearrangeEnabled = true,
            TabsRearrangeGroup = DockGroup,
            CustomMinimumSize = new Vector2(Px(width), Px(height)),
        };
        // a panel is dropped anywhere on a dock that is empty (the engine takes a dragged tab on a dock's row of tabs only, and an empty dock has none)
        dock.SetDragForwarding(new Callable(), Callable.From((Vector2 _, Variant data) => DraggedPanel(data) != null), Callable.From((Vector2 _, Variant data) => DropPanel(data, dock)));
        return dock;
    }

    // The panel a drag carries when it is a tab of one of the docks, else null: the engine's own data of a dragged tab names its row of tabs (a part
    // of its dock) and its place in it.
    private Control DraggedPanel(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
            return null;
        Godot.Collections.Dictionary drag = data.AsGodotDictionary();
        if (!drag.TryGetValue("tab_type", out Variant type) || type.AsString() != "tab_container_tab"
            || !drag.TryGetValue("from_path", out Variant path) || !drag.TryGetValue("tab_index", out Variant index))
            return null;
        Node from = GetNodeOrNull(path.AsNodePath());
        while (from != null && from is not TabContainer)
            from = from.GetParent();
        if (from is not TabContainer dock || Array.IndexOf(_docks, dock) < 0)
            return null;
        int tab = index.AsInt32();
        return tab >= 0 && tab < dock.GetTabCount() ? dock.GetTabControl(tab) : null;
    }

    private void DropPanel(Variant data, TabContainer dock)
    {
        if (DraggedPanel(data) is not { } panel || panel.GetParent() == dock)
            return;
        panel.GetParent().RemoveChild(panel);
        dock.AddChild(panel);
        dock.CurrentTab = dock.GetTabCount() - 1;
    }

    // A dock with no panel to show takes no room, and a side with no dock none; while a panel is dragged every dock is there to drop it on.
    public override void _Process(double delta)
    {
        bool dragging = GetViewport().GuiIsDragging();
        foreach (TabContainer dock in Docks)
        {
            bool any = false;
            for (int i = 0; i < dock.GetTabCount() && !any; i++)
                any = !dock.IsTabHidden(i);
            dock.Visible = any || dragging;
        }
        _left.Visible = LeftTopDock.Visible || LeftBottomDock.Visible;
        _right.Visible = RightTopDock.Visible || RightBottomDock.Visible;
    }

    /// <summary>
    /// The size the interface takes when the mapper has set none: in proportion to the screen's height, 1 at 1080 pixels, in steps of a quarter. So the
    /// docks and the text take the same share of a screen of any resolution.
    /// </summary>
    public static float AutomaticScale(int screen)
        => Mathf.Clamp(Mathf.Snapped(DisplayServer.ScreenGetSize(screen).Y / 1080f, 0.25f), 0.75f, 3f);

    /// <summary>
    /// The arrangement as a text that changes when it does: the panels of each dock in their order, the one in front, the hidden ones, the borders.
    /// </summary>
    public string LayoutSignature()
    {
        var text = new System.Text.StringBuilder();
        foreach (TabContainer dock in Docks)
        {
            text.Append(dock.Name).Append(':');
            for (int i = 0; i < dock.GetTabCount(); i++)
                text.Append(dock.GetTabControl(i).Name).Append(dock.IsTabHidden(i) ? "-" : i == dock.CurrentTab ? "*" : string.Empty).Append(',');
            text.Append(';');
        }
        foreach ((_, SplitContainer split, _) in _splits)
            text.Append(BorderOffset(split)).Append(' ');
        return text.ToString();
    }

    private static int BorderOffset(SplitContainer split) => split.SplitOffsets is { Length: > 0 } offsets ? offsets[0] : 0;

    /// <summary>Writes the arrangement into the settings (not the file: the caller saves it).</summary>
    public void SaveLayout(MapEditorSettings to)
    {
        var hidden = new List<string>();
        foreach (TabContainer dock in Docks)
        {
            var titles = new List<string>();
            for (int i = 0; i < dock.GetTabCount(); i++)
            {
                titles.Add(dock.GetTabControl(i).Name);
                if (dock.IsTabHidden(i))
                    hidden.Add(dock.GetTabControl(i).Name);
            }
            to.Set(LayoutSection, dock.Name, titles.ToArray());
            to.Set(LayoutSection, dock.Name + "_front", dock.GetCurrentTabControl()?.Name.ToString() ?? string.Empty);
        }
        to.Set(LayoutSection, "hidden", hidden.ToArray());
        foreach ((string key, SplitContainer split, _) in _splits)
            to.Set(LayoutSection, "border_" + key, Mathf.RoundToInt(BorderOffset(split) / _scale));
    }

    /// <summary>
    /// Arranges the panels, which are in the docks they were built in, as the settings have them. A panel the settings do not name stays where it is, and
    /// a name with no panel is passed over, so a file of another version of the editor does no harm.
    /// </summary>
    public void LoadLayout(MapEditorSettings from)
    {
        var panels = new Dictionary<string, Control>();
        foreach (TabContainer dock in Docks)
        {
            for (int i = 0; i < dock.GetTabCount(); i++)
                panels[dock.GetTabControl(i).Name] = dock.GetTabControl(i);
        }
        foreach (TabContainer dock in Docks)
        {
            foreach (string title in from.Texts(LayoutSection, dock.Name))
            {
                if (!panels.Remove(title, out Control panel))
                    continue;
                panel.GetParent().RemoveChild(panel);
                dock.AddChild(panel);
            }
        }
        var hidden = new HashSet<string>(from.Texts(LayoutSection, "hidden"));
        foreach (TabContainer dock in Docks)
        {
            string front = from.Text(LayoutSection, dock.Name + "_front");
            for (int i = 0; i < dock.GetTabCount(); i++)
            {
                string title = dock.GetTabControl(i).Name;
                dock.SetTabHidden(i, hidden.Contains(title));
                if (title == front && !hidden.Contains(title))
                    dock.CurrentTab = i;
            }
        }
        foreach ((string key, SplitContainer split, int offset) in _splits)
            split.SplitOffsets = new[] { Px(from.Int(LayoutSection, "border_" + key, offset)) };
    }

    /// <summary>Adds a menu to the bar; its items are the caller's.</summary>
    public PopupMenu AddMenu(string title)
    {
        var menu = new PopupMenu { Name = title };
        Menu.AddChild(menu);
        _menus.Add(menu);
        return menu;
    }

    /// <summary>Puts a panel into a dock as a tab of its name.</summary>
    public void AddPanel(TabContainer dock, string title, Control panel)
    {
        panel.Name = title;
        dock.AddChild(panel);
    }

    /// <summary>A margin around a panel's content, as every panel of the docks has.</summary>
    public MarginContainer Padded(Control content)
    {
        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, Px(6));
        margin.AddChild(content);
        return margin;
    }

    /// <summary>True when a point of the screen is over the free middle, not on a button of its bar, and no menu is open: the 3D view's mouse.</summary>
    public bool OverView(Vector2 screen)
    {
        if (!Centre.GetGlobalRect().HasPoint(screen))
            return false;
        foreach (Node child in ViewBar.GetChildren())
        {
            if (child is Control { Visible: true } button && button.GetGlobalRect().HasPoint(screen))
                return false;
        }
        foreach (PopupMenu menu in _menus)
        {
            if (menu.Visible)
                return false;
        }
        return true;
    }

    /// <summary>Shows a line at the mouse over the view (what a click does there); an empty text hides it.</summary>
    public void Tip(string text, Vector2 mouse)
    {
        if (string.IsNullOrEmpty(text))
        {
            _tip.Visible = false;
            return;
        }
        _tipText.Text = text;
        _tip.Visible = true;
        _tip.ResetSize();
        Vector2 screen = GetViewport().GetVisibleRect().Size, at = mouse + new Vector2(Px(18), Px(14));
        _tip.Position = new Vector2(Mathf.Min(at.X, screen.X - _tip.Size.X - 4), Mathf.Min(at.Y, screen.Y - _tip.Size.Y - 4));
    }

    /// <summary>A filled triangle that points up or down, in the theme's text colour: the level buttons' picture, large enough to see at a glance.</summary>
    public ImageTexture ArrowIcon(bool up)
    {
        int width = Px(18), height = Px(11);
        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        var ink = new Color(0.87f, 0.88f, 0.9f);
        for (int y = 0; y < height; y++)
        {
            // (the row's half width grows from the tip to the base)
            float half = (up ? y + 1 : height - y) * (width / 2f) / height;
            for (int x = 0; x < width; x++)
            {
                if (Mathf.Abs(x + 0.5f - width / 2f) <= half)
                    image.SetPixel(x, y, ink);
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>A small cross for a row's button (take away, reset), in the theme's text colour.</summary>
    public ImageTexture CrossIcon()
    {
        int size = Px(12);
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var ink = new Color(0.87f, 0.88f, 0.9f);
        for (int i = 2; i < size - 2; i++)
        {
            foreach ((int x, int y) in new[] { (i, i), (i, size - 1 - i), (i + 1, i), (i + 1, size - 1 - i) })
            {
                if (x < size)
                    image.SetPixel(x, y, ink);
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    // A check box's picture: an outlined square, filled with the accent colour and ticked when it is on.
    private ImageTexture BoxIcon(bool on, Color accent, Color edge)
    {
        int size = Px(15);
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                bool border = x == 1 || y == 1 || x == size - 2 || y == size - 2;
                image.SetPixel(x, y, on ? accent : border ? edge : new Color(0, 0, 0, 0.25f));
            }
        }
        if (on)
        {
            // the tick: two strokes
            Vector2[] tick = { new(0.24f, 0.52f), new(0.43f, 0.72f), new(0.77f, 0.3f) };
            for (int k = 0; k < 2; k++)
            {
                for (float t = 0; t <= 1; t += 0.5f / size)
                {
                    Vector2 at = tick[k].Lerp(tick[k + 1], t) * size;
                    for (int dy = 0; dy <= Px(1); dy++)
                        image.SetPixel(Mathf.Clamp((int)at.X, 0, size - 1), Mathf.Clamp((int)at.Y + dy, 0, size - 1), Colors.White);
                }
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    // A flat dark theme, as a game engine's editor has: panels a shade lighter than the fields in them, one accent colour for what is selected.
    private Theme BuildTheme()
    {
        var font = new SystemFont
        {
            FontNames = new[] { "Inter", "SF Pro Text", "Segoe UI", "Helvetica Neue", "Noto Sans", "Arial", "sans-serif" },
            Antialiasing = TextServer.FontAntialiasing.Gray,
            SubpixelPositioning = TextServer.SubpixelPositioning.Auto,
        };
        var theme = new Theme { DefaultFont = font, DefaultFontSize = Px(13) };
        Color back = new(0.118f, 0.122f, 0.133f), panel = new(0.169f, 0.176f, 0.188f), raised = new(0.224f, 0.231f, 0.251f);
        Color accent = new(0.29f, 0.53f, 0.78f), text = new(0.87f, 0.88f, 0.9f), dim = new(0.62f, 0.63f, 0.66f), line = new(0.09f, 0.09f, 0.1f);

        StyleBoxFlat Box(Color colour, float margin = 4, float radius = 3, Color? border = null)
        {
            var box = new StyleBoxFlat { BgColor = colour };
            box.SetCornerRadiusAll(Px(radius));
            box.SetContentMarginAll(Px(margin));
            if (border is { } edge)
            {
                box.BorderColor = edge;
                box.SetBorderWidthAll(1);
            }
            return box;
        }

        foreach (string type in new[] { "Label", "Button", "CheckBox", "CheckButton", "LineEdit", "OptionButton", "PopupMenu", "Tree", "TabContainer", "TabBar", "MenuBar", "ItemList", "SpinBox" })
            theme.SetColor("font_color", type, text);
        theme.SetColor("font_placeholder_color", "LineEdit", new Color(dim, 0.7f));
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color(dim, 0.5f));
        theme.SetColor("font_hover_color", "MenuBar", Colors.White);
        theme.SetColor("font_selected_color", "TabContainer", Colors.White);
        theme.SetColor("font_unselected_color", "TabContainer", dim);
        theme.SetColor("font_hovered_color", "TabContainer", text);
        theme.SetColor("font_selected_color", "Tree", Colors.White);
        theme.SetColor("guide_color", "Tree", new Color(1, 1, 1, 0.04f));
        theme.SetColor("font_hover_color", "PopupMenu", Colors.White);
        theme.SetColor("font_accelerator_color", "PopupMenu", dim);
        theme.SetColor("font_disabled_color", "PopupMenu", new Color(dim, 0.5f));

        theme.SetStylebox("panel", "PanelContainer", Box(panel, 6, 0));
        theme.SetTypeVariation("EditorBar", "PanelContainer");
        theme.SetStylebox("panel", "EditorBar", Box(panel, 4, 0, line));
        theme.SetTypeVariation("EditorTip", "PanelContainer");
        theme.SetStylebox("panel", "EditorTip", Box(new Color(back, 0.94f), 6, 4, new Color(1, 1, 1, 0.12f)));

        theme.SetStylebox("normal", "Button", Box(raised, 5));
        theme.SetStylebox("hover", "Button", Box(raised.Lightened(0.12f), 5));
        theme.SetStylebox("pressed", "Button", Box(accent, 5));
        theme.SetStylebox("disabled", "Button", Box(new Color(raised, 0.5f), 5));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            theme.SetStylebox(state, "OptionButton", theme.GetStylebox(state == "pressed" ? "hover" : state, "Button"));
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus", "hover_pressed" })
        {
            theme.SetStylebox(state, "MenuBar", state is "hover" or "pressed" or "hover_pressed" ? Box(raised, 5) : Box(Colors.Transparent, 5));
            theme.SetStylebox(state, "CheckBox", new StyleBoxEmpty());
            theme.SetStylebox(state, "CheckButton", new StyleBoxEmpty());
        }

        ImageTexture boxOn = BoxIcon(true, accent, dim), boxOff = BoxIcon(false, accent, dim);
        foreach (string type in new[] { "CheckBox", "PopupMenu" })
        {
            theme.SetIcon("checked", type, boxOn);
            theme.SetIcon("unchecked", type, boxOff);
        }

        theme.SetStylebox("normal", "LineEdit", Box(back, 5, 3, line));
        theme.SetStylebox("focus", "LineEdit", Box(back, 5, 3, accent));
        theme.SetStylebox("read_only", "LineEdit", Box(new Color(back, 0.5f), 5, 3, line));

        theme.SetStylebox("panel", "TabContainer", Box(panel, 0, 0));
        theme.SetStylebox("tab_selected", "TabContainer", Box(panel, 7, 0));
        theme.SetStylebox("tab_unselected", "TabContainer", Box(back, 7, 0));
        theme.SetStylebox("tab_hovered", "TabContainer", Box(back.Lightened(0.06f), 7, 0));
        theme.SetStylebox("tabbar_background", "TabContainer", Box(back, 0, 0));
        theme.SetStylebox("tab_focus", "TabContainer", new StyleBoxEmpty());
        theme.SetConstant("side_margin", "TabContainer", 0);

        theme.SetStylebox("panel", "Tree", Box(back, 2, 3, line));
        theme.SetStylebox("focus", "Tree", new StyleBoxEmpty());
        theme.SetStylebox("selected", "Tree", Box(new Color(accent, 0.55f), 0, 2));
        theme.SetStylebox("selected_focus", "Tree", Box(new Color(accent, 0.7f), 0, 2));
        theme.SetStylebox("hovered", "Tree", Box(new Color(1, 1, 1, 0.05f), 0, 2));
        theme.SetConstant("v_separation", "Tree", Px(3));
        theme.SetConstant("inner_item_margin_left", "Tree", Px(4));

        theme.SetStylebox("panel", "AcceptDialog", Box(panel, 12, 0));
        theme.SetStylebox("panel", "PopupMenu", Box(panel.Lightened(0.03f), 4, 4, line));
        theme.SetStylebox("hover", "PopupMenu", Box(accent, 2, 2));
        theme.SetConstant("v_separation", "PopupMenu", Px(6));
        theme.SetConstant("h_separation", "PopupMenu", Px(10));

        // A border between two docks, or a dock and the view, is a thin dark line with no gap the map shows through (the owner, 2026-11-17); it is taken by
        // the mouse a little to either side of it, and has no handle drawn on it.
        var none = ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));
        foreach (string split in new[] { "SplitContainer", "HSplitContainer", "VSplitContainer" })
        {
            theme.SetConstant("separation", split, Px(2));
            theme.SetConstant("minimum_grab_thickness", split, Px(8));
            theme.SetConstant("autohide", split, 1);
            theme.SetStylebox("split_bar_background", split, new StyleBoxFlat { BgColor = line });
            foreach (string icon in new[] { "grabber", "h_grabber", "v_grabber" })
                theme.SetIcon(icon, split, none);
        }
        return theme;
    }
}

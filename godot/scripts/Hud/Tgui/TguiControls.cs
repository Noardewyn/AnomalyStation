using System.Linq;
using Godot;
using Anomaly.Client.Shared.Tgui;

namespace Anomaly.Client.Hud.Tgui;

// M5 slice 3 (D148): tgui-core's drawn components: RoundGauge (a half ring of coloured ranges with a needle and the value under it),
// Knob (a dial: drag up or down, or turn the wheel, to change the value; the new value is sent when the drag ends, as tgui's Knob's
// onChange), and the input modals' Loader (a thin bar of the time left).

/// <summary>tgui-core's RoundGauge.</summary>
public partial class TguiRoundGauge : Control
{
    public UiNode Node;

    public override void _Ready()
    {
        float size = (float)(Node.Size * 1.0 * UiSize.Rem * 2.2);
        CustomMinimumSize = new Vector2(size, size * 0.62f + 14);
        MouseFilter = MouseFilterEnum.Pass;
        TooltipText = Node.Format?.Invoke(Node.Value) ?? TguiFormat.ToFixed(Node.Value);
    }

    // the palette of the window that built this control (TguiTheme.Use): its draws run later, outside the build
    private readonly TguiTheme.Palette _palette = TguiTheme.Current;

    public override void _Draw()
    {
        using var themed = TguiTheme.Use(_palette);
        float w = CustomMinimumSize.X;
        float radius = w * 0.42f;
        var center = new Vector2(Size.X / 2, radius + 4);
        double min = Node.MinValue, max = Math.Max(Node.MaxValue, Node.MinValue + 1e-9);
        float Angle(double v) => Mathf.Pi + (float)(TguiFormat.Clamp01((v - min) / (max - min)) * Mathf.Pi);
        DrawArc(center, radius, Mathf.Pi, Mathf.Tau, 32, new Color(1, 1, 1, 0.12f), radius * 0.28f, true);
        if (Node.Ranges != null)
        {
            foreach ((string color, double from, double to) in Node.Ranges)
            {
                Color c = TguiTheme.Named(color) ?? TguiTheme.Text;
                DrawArc(center, radius, Angle(from), Angle(to), 16, c, radius * 0.22f, true);
            }
        }
        float a = Angle(Node.Value);
        bool alert = (Node.AlertAfter is double after && Node.Value > after) || (Node.AlertBefore is double before && Node.Value < before);
        var tip = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.95f;
        DrawLine(center, tip, alert ? TguiTheme.Named("bad").Value : TguiTheme.Text, 2.5f, true);
        DrawCircle(center, 3f, TguiTheme.Text);
        string text = Node.Format?.Invoke(Node.Value) ?? TguiFormat.ToFixed(Node.Value);
        Font font = GetThemeDefaultFont();
        int fontSize = 11;
        Vector2 textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
        DrawString(font, new Vector2((Size.X - textSize.X) / 2, center.Y + 14), text, HorizontalAlignment.Left, -1, fontSize, TguiTheme.Text);
    }
}

/// <summary>tgui-core's Knob: drag vertically (StepPixelSize pixels a step) or turn the wheel; the value is sent when the drag ends.</summary>
public partial class TguiKnob : Control
{
    public UiNode Node;
    private double _value;
    private bool _dragging;
    private float _dragAccum;

    public override void _Ready()
    {
        float size = (float)(Node.Size * UiSize.Rem * 2.6);
        CustomMinimumSize = new Vector2(size, size + 14);
        MouseFilter = MouseFilterEnum.Stop;
        _value = Node.Value;
        TooltipText = Shown();
        MouseDefaultCursorShape = CursorShape.Vsize;
    }

    private string Shown() => TguiFormat.ToFixed(_value) + (string.IsNullOrEmpty(Node.Unit) ? "" : " " + Node.Unit);

    public override void _GuiInput(InputEvent @event)
    {
        if (Node.Disabled)
            return;
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                if (button.Pressed)
                {
                    _dragging = true;
                    _dragAccum = 0;
                }
                else if (_dragging)
                {
                    _dragging = false;
                    if (_value != Node.Value)
                        Node.OnChange?.Invoke(_value);
                }
                AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                StepBy(wheel.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
                Node.OnChange?.Invoke(_value);
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging:
                _dragAccum += -motion.Relative.Y;
                float per = (float)Math.Max(0.5, Node.StepPixelSize);
                while (Math.Abs(_dragAccum) >= per)
                {
                    StepBy(Math.Sign(_dragAccum));
                    _dragAccum -= Math.Sign(_dragAccum) * per;
                }
                AcceptEvent();
                break;
        }
    }

    private void StepBy(int steps)
    {
        _value = TguiFormat.Clamp(_value + steps * Node.Step, Node.MinValue, Node.MaxValue);
        SetMeta(TguiHost.TipMeta, Shown());
        QueueRedraw();
    }

    // the palette of the window that built this control (TguiTheme.Use): its draws run later, outside the build
    private readonly TguiTheme.Palette _palette = TguiTheme.Current;

    public override void _Draw()
    {
        using var themed = TguiTheme.Use(_palette);
        float d = CustomMinimumSize.X;
        float radius = d * 0.4f;
        var center = new Vector2(Size.X / 2, radius + 2);
        double t = TguiFormat.Clamp01((_value - Node.MinValue) / Math.Max(1e-9, Node.MaxValue - Node.MinValue));
        float start = Mathf.DegToRad(135), sweep = Mathf.DegToRad(270);
        Color ring = TguiTheme.Named(Node.Color) ?? TguiTheme.Hex("#5a8fd6");
        DrawArc(center, radius, start, start + sweep, 32, new Color(1, 1, 1, 0.15f), 3, true);
        DrawArc(center, radius, start, start + (float)(sweep * t), 32, ring, 3, true);
        DrawCircle(center, radius * 0.72f, TguiTheme.Hex("#3e6189"));
        float a = start + (float)(sweep * t);
        DrawLine(center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.7f, TguiTheme.Text, 2, true);
        Font font = GetThemeDefaultFont();
        string text = Shown();
        Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 10);
        DrawString(font, new Vector2((Size.X - size.X) / 2, center.Y + radius + 12), text, HorizontalAlignment.Left, -1, 10, TguiTheme.Text);
    }
}

/// <summary>
/// tgui-core's Slider (a DraggableControl): drag sideways (StepPixelSize pixels a step, after the first 4 pixels, as tgui-core's drag
/// threshold); the new value is sent when the drag ends. A click without a drag opens a field to type the value (Enter or leaving the
/// field sends it, Esc cancels). The lighter bar behind the value is the fill value; the value shows in the middle through Format.
/// </summary>
public partial class TguiSlider : Control
{
    public UiNode Node;
    private double _value;
    private double _internal;
    private bool _pressed;
    private bool _dragging;
    private float _origin;
    private LineEdit _edit;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(60, 20);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        _value = Node.Value;
        MouseDefaultCursorShape = CursorShape.Hsize;
    }

    private string Shown() => Node.Format?.Invoke(_value) ?? TguiFormat.JsNumber(_value);

    private double Scaled(double v) => TguiFormat.Clamp01((v - Node.MinValue) / Math.Max(1e-9, Node.MaxValue - Node.MinValue));

    public override void _GuiInput(InputEvent @event)
    {
        if (Node.Disabled || _edit != null)
            return;
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                if (button.Pressed)
                {
                    _pressed = true;
                    _dragging = false;
                    _origin = button.GlobalPosition.X;
                    _internal = _value;
                }
                else if (_pressed)
                {
                    _pressed = false;
                    if (_dragging)
                    {
                        _dragging = false;
                        if (_value != Node.Value)
                            Node.OnChange?.Invoke(_value);
                    }
                    else
                        OpenEdit();
                }
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _pressed:
            {
                float diff = motion.GlobalPosition.X - _origin;
                if (!_dragging && Math.Abs(diff) > 4)
                    _dragging = true;
                if (_dragging)
                {
                    double step = Node.Step <= 0 ? 1 : Node.Step;
                    _internal = TguiFormat.Clamp(_internal + diff * step / Math.Max(0.5, Node.StepPixelSize), Node.MinValue, Node.MaxValue);
                    _value = TguiFormat.Clamp(_internal - (_internal % step), Node.MinValue, Node.MaxValue);
                    _origin = motion.GlobalPosition.X;
                    QueueRedraw();
                }
                AcceptEvent();
                break;
            }
        }
    }

    private void OpenEdit()
    {
        _edit = new LineEdit { Text = TguiFormat.JsNumber(_value), Alignment = HorizontalAlignment.Center };
        _edit.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var style = new StyleBoxFlat { BgColor = TguiTheme.InputBg, BorderColor = TguiTheme.InputBorder };
        style.SetBorderWidthAll(1);
        _edit.AddThemeStyleboxOverride("normal", style);
        _edit.AddThemeStyleboxOverride("focus", style);
        bool done = false;
        void Finish(bool commit)
        {
            if (done)
                return;
            done = true;
            string text = _edit.Text;
            _edit.QueueFree();
            _edit = null;
            if (commit && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double typed))
            {
                _value = TguiFormat.Clamp(typed, Node.MinValue, Node.MaxValue);
                if (_value != Node.Value)
                    Node.OnChange?.Invoke(_value);
            }
            QueueRedraw();
        }
        _edit.TextSubmitted += _ => Finish(true);
        _edit.FocusExited += () => Finish(true);
        _edit.GuiInput += e =>
        {
            if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
            {
                Finish(false);
                AcceptEvent();
            }
        };
        AddChild(_edit);
        _edit.GrabFocus();
        _edit.SelectAll();
    }

    // the palette of the window that built this control (TguiTheme.Use): its draws run later, outside the build
    private readonly TguiTheme.Palette _palette = TguiTheme.Current;

    public override void _Draw()
    {
        using var themed = TguiTheme.Use(_palette);
        Color color = TguiTheme.Named(Node.Color) ?? TguiTheme.Hex("#4972a1");
        DrawRect(new Rect2(0, 0, Size.X, Size.Y), new Color(0, 0, 0, 0.3f));
        if (Node.FillValue is double fillValue)
            DrawRect(new Rect2(0, 0, (float)(Size.X * Scaled(fillValue)), Size.Y), new Color(color, 0.4f));
        DrawRect(new Rect2(0, 0, (float)(Size.X * Scaled(_value)), Size.Y), color);
        string text = Shown();
        Font font = GetThemeDefaultFont();
        Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 12);
        DrawString(font, new Vector2((Size.X - size.X) / 2, (Size.Y + size.Y) / 2 - 3), text, HorizontalAlignment.Left, -1, 12, TguiTheme.Text);
    }
}

/// <summary>The input modals' Loader: the share of time left as a thin bar across the window's top.</summary>
public partial class TguiLoader : Control
{
    public double Value;

    public override void _Ready() => CustomMinimumSize = new Vector2(0, 4);

    // the palette of the window that built this control (TguiTheme.Use): its draws run later, outside the build
    private readonly TguiTheme.Palette _palette = TguiTheme.Current;

    public override void _Draw()
    {
        using var themed = TguiTheme.Use(_palette);
        DrawRect(new Rect2(0, 0, Size.X, Size.Y), new Color(0, 0, 0, 0.4f));
        DrawRect(new Rect2(0, 0, (float)(Size.X * TguiFormat.Clamp01(Value)), Size.Y), TguiTheme.Hex("#5baa27"));
    }
}

/// <summary>
/// SolarControl's chart: a black box that draws its lines over each other. A line (tgui-core's <c>Chart.Line</c>) scales its points to its x and y ranges, closes the
/// polygon along the bottom edge and fills and strokes it; fewer than two points draw nothing (their x range is empty).
/// </summary>
public partial class TguiChart : Control
{
    public UiNode Node;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, UiSize.Of(Node.Height) is UiSize height ? (float)height.Pixels : 64);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    // the palette of the window that built this control (TguiTheme.Use): its draws run later, outside the build
    private readonly TguiTheme.Palette _palette = TguiTheme.Current;

    public override void _Draw()
    {
        using var themed = TguiTheme.Use(_palette);
        if (!Node.Plain)
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);
        float pad = Node.Plain ? 0 : 4;
        var area = new Rect2(new Vector2(pad, pad), Size - new Vector2(2 * pad, 2 * pad));
        foreach (UiNode line in Node.Children)
        {
            if (line.Kind != UiKind.ChartLine || line.Data == null || line.Data.Count < 2)
                continue;
            double spanX = Math.Max(1e-9, line.RangeX.Max - line.RangeX.Min), spanY = Math.Max(1e-9, line.RangeY.Max - line.RangeY.Min);
            var points = new List<Vector2> { new(area.Position.X, area.End.Y) };
            foreach ((double x, double y) in line.Data)
                points.Add(new Vector2(area.Position.X + (float)((x - line.RangeX.Min) / spanX) * area.Size.X, area.End.Y - (float)((y - line.RangeY.Min) / spanY) * area.Size.Y));
            points.Add(new Vector2(area.End.X, area.End.Y));
            if (TguiTheme.Named(line.FillColor) is Color fill)
                DrawColoredPolygon(points.ToArray(), fill);
            if (TguiTheme.Named(line.StrokeColor) is Color stroke)
                DrawPolyline(points.Append(points[0]).ToArray(), stroke, 1f, true);
        }
    }
}

/// <summary>
/// DetectiveBoard's cork board (gaps J4, tgui's absolutely placed <c>Evidence</c> boxes over <c>Connections</c>): each child stands where the node says and drags with the left button.
/// </summary>
public partial class TguiBoard : Control
{
    public UiNode Node;

    public static Control Build(UiNode node, TguiRenderEnv env)
    {
        var board = new TguiBoard { Node = node, ClipContents = true };
        foreach (UiNode child in node.Children)
        {
            Control built = TguiRender.Build(child, env);
            if (built == null)
                continue;
            var card = new TguiBoardCard { Node = child, Position = new Vector2((float)child.PosX, (float)child.PosY) };
            card.AddChild(built);
            board.AddChild(card);
        }
        return board;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, UiSize.Of(Node.Height) is UiSize height ? (float)height.Pixels : 400);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.37f, 0.25f, 0.13f));
        if (Node.BoardLines == null)
            return;
        foreach ((double fromX, double fromY, double toX, double toY, string color) in Node.BoardLines)
            DrawLine(new Vector2((float)fromX, (float)fromY), new Vector2((float)toX, (float)toY), color == "red" ? Colors.Red : Colors.Black, 5);
    }
}

/// <summary>A child of <see cref="TguiBoard"/>: it follows the mouse while the left button holds it and reports its place when let go.</summary>
public partial class TguiBoardCard : PanelContainer
{
    public UiNode Node;
    private bool _dragging;

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            if (button.Pressed)
                _dragging = true;
            else if (_dragging)
            {
                _dragging = false;
                Node.OnMoved?.Invoke(Position.X, Position.Y);
            }
            AcceptEvent();
        }
        else if (input is InputEventMouseMotion motion && _dragging)
        {
            Position += motion.Relative;
            AcceptEvent();
        }
    }
}


using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// D315: the minimap, tg's minimap picture of the own level (protocol 67, made once a round, so the map's later changes are not on it),
/// north up and centred on the own mob, with an arrow of the view's heading; the departments' codes on their areas and the area's name
/// under the cursor (tg's hover text); with the dev trackers (the dev menu's View tab) a dot per living mob of the level and its name under
/// the cursor. In cursor mode the left button drags the map to look around (the view comes back to the mob once it moves, MinimapView),
/// its edge moves the panel anywhere, and the wheel zooms; it has no title bar and no close button (Settings > View turns it off and sizes
/// it; it keeps its share of the screen at every resolution). It redraws only when the mob moves or turns, the map is dragged or the zoom
/// changes: one textured rectangle, a few labels and the arrow.
/// </summary>
public partial class MinimapPanel : Control
{
    // the area codes' and the hover text's sizes, scaled by Settings > Interface > Minimap text size
    private int LabelFontSize => Math.Max(1, 9 * _options.MinimapTextPercent / 100);
    private int HoverFontSize => Math.Max(1, 11 * _options.MinimapTextPercent / 100);
    private const float EdgeBand = 10f; // the frame that moves the panel, in its own pixels

    /// <summary>The side in pixels at 100 per cent on a 1080-line screen.</summary>
    public const float Side = 200;
    private static readonly Color Background = new(0.05f, 0.06f, 0.08f, 0.8f);
    private static readonly Color Border = new(0.35f, 0.4f, 0.5f, 0.9f);
    private static readonly Color Edge = new(0.35f, 0.4f, 0.5f, 0.25f);
    private static readonly Color Arrow = new(1f, 0.85f, 0.2f);
    private static readonly Color PlayerDot = new(0.25f, 0.9f, 0.35f);
    private static readonly Color MobDot = new(1f, 0.3f, 0.25f);

    private PlayOptions _options;
    private Action _saved;
    private readonly Dictionary<int, (MinimapLayout Layout, ImageTexture Texture)> _levels = new();
    private readonly Dictionary<(string Code, int Size), float> _labelWidths = new();
    private (MinimapLayout Layout, ImageTexture Texture) _shown;
    private Vector2 _mob; // the own mob, world tiles
    private Vector2 _centre; // the view's centre of the last draw, world tiles
    private readonly MinimapView _view = new();
    private Vector2? _panFrom; // the pointer of a drag of the map, while the left button is down
    private float _heading = float.NaN; // radians clockwise from north
    private Vector2? _hover;
    private MinimapMobsMessage _mobs; // the dev trackers, or null

    public void Bind(PlayOptions options, WindowChrome chrome, Action saved)
    {
        _options = options;
        _saved = saved;
        Name = "Minimap";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        TextureFilter = TextureFilterEnum.Nearest; // tg's cells as whole squares
        chrome.MinSize = new Vector2(120, 120);
        chrome.DragBy(this, OnEdge);
        MouseExited += () =>
        {
            _hover = null;
            _panFrom = null;
            QueueRedraw();
        };
    }

    /// <summary>
    /// Follows the own mob (its level <paramref name="z"/> and its tile position) and the view's <paramref name="yaw"/> (Godot's, 0 north,
    /// counterclockwise); hidden without the option, a mob or its level's minimap. <paramref name="mobs"/>: the dev trackers, or null.
    /// </summary>
    public void Follow(IWorldFeed feed, bool hasOwn, int z, float x, float y, float yaw, MinimapMobsMessage mobs)
    {
        (MinimapLayout Layout, ImageTexture Texture) level = default;
        Visible = _options.Minimap && hasOwn && TryLevel(feed, z, out level);
        if (!Visible)
            return;
        // the size of Settings > View at 1080 lines, scaled so it keeps its share of the screen at every resolution (as the chat window)
        Vector2 size = Vector2.One * (Side * _options.MinimapSizePercent / 100f), scale = Vector2.One * (GetViewportRect().Size.Y / 1080f);
        if (Size != size)
            Size = size; // over the window frame's own share of a resized screen: the scale does it
        if (Scale != scale)
            Scale = scale;
        var mob = new Vector2(x, y);
        if (level.Layout == _shown.Layout && mobs == _mobs && mob.DistanceSquaredTo(_mob) < 1e-4f && Mathf.Abs(Mathf.AngleDifference(_heading, -yaw)) < 0.01f)
            return;
        if (level.Layout != _shown.Layout)
            _view.Reset();
        (_shown, _mob, _heading, _mobs) = (level, mob, -yaw, mobs);
        QueueRedraw();
    }

    private bool TryLevel(IWorldFeed feed, int z, out (MinimapLayout Layout, ImageTexture Texture) level)
    {
        if (_levels.TryGetValue(z, out level))
            return true;
        MinimapMessage map = feed.MinimapOf(z);
        if (map == null)
            return false;
        var layout = new MinimapLayout(map);
        Image image = Image.CreateFromData(map.Width, map.Height, false, Image.Format.Rgba8, layout.Rgba());
        _levels[z] = level = (layout, ImageTexture.CreateFromImage(image));
        return true;
    }

    private float PixelsPerTile => _options.MinimapZoomPercent / 100f;

    // A world tile's centre on the panel: the view's centre in the middle, north up
    private Vector2 ToPanel(float x, float y) => Size / 2 + new Vector2(x - _centre.X, _centre.Y - y) * PixelsPerTile;

    private bool OnEdge(Vector2 at) => at.X < EdgeBand || at.Y < EdgeBand || at.X > Size.X - EdgeBand || at.Y > Size.Y - EdgeBand;

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                _options.MinimapZoomPercent = MinimapLayout.Zoom(_options.MinimapZoomPercent, wheel.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
                _saved();
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _panFrom = button.Pressed && !OnEdge(button.Position) ? button.Position : null; // the edge is the window frame's drag
                if (_panFrom != null || !button.Pressed)
                    AcceptEvent();
                break;
            case InputEventMouseMotion motion:
                if (_panFrom is Vector2 from)
                {
                    _view.Drag(motion.Position.X - from.X, motion.Position.Y - from.Y, PixelsPerTile, _mob.X, _mob.Y);
                    _panFrom = motion.Position;
                }
                MouseDefaultCursorShape = _panFrom != null ? CursorShape.Drag : OnEdge(motion.Position) ? CursorShape.Move : CursorShape.Arrow;
                _hover = motion.Position;
                QueueRedraw();
                break;
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        MinimapLayout layout = _shown.Layout;
        if (layout != null)
        {
            MinimapMessage map = layout.Map;
            float ppt = PixelsPerTile;
            (float cx, float cy) = _view.Centre(_mob.X, _mob.Y, map);
            _centre = new Vector2(cx, cy);
            Vector2 topLeft = ToPanel(map.MinX - 0.5f, map.MinY + map.Height - 0.5f);
            DrawTextureRect(_shown.Texture, new Rect2(topLeft, new Vector2(map.Width, map.Height) * ppt), false);
            DrawLabels(layout, ppt);
            DrawMobs(map.Z, ppt);
            DrawArrow();
            DrawHover(layout, ppt);
        }
        DrawRect(new Rect2(Vector2.Zero, Size), Edge, false, EdgeBand); // the frame that moves the panel
        DrawRect(new Rect2(Vector2.Zero, Size), Border, false, 1f);
    }

    private void DrawLabels(MinimapLayout layout, float ppt)
    {
        Font font = ThemeDB.FallbackFont;
        float height = font.GetHeight(LabelFontSize);
        var panel = new Rect2(Vector2.Zero, Size);
        foreach (MinimapLayout.Label label in layout.Visible(ppt, LabelWidth, height))
        {
            Vector2 at = ToPanel(label.X, label.Y);
            float width = LabelWidth(label.Code);
            if (!panel.Encloses(new Rect2(at.X - width / 2, at.Y - height / 2, width, height)))
                continue; // a label cut by the edge reads as another code
            var baseline = new Vector2(at.X - width / 2, at.Y - height / 2 + font.GetAscent(LabelFontSize));
            DrawStringOutline(font, baseline, label.Code, HorizontalAlignment.Left, -1, LabelFontSize, 3, Colors.Black);
            DrawString(font, baseline, label.Code, HorizontalAlignment.Left, -1, LabelFontSize, Colors.White);
        }
    }

    private float LabelWidth(string code)
    {
        if (!_labelWidths.TryGetValue((code, LabelFontSize), out float width))
            _labelWidths[(code, LabelFontSize)] = width =ThemeDB.FallbackFont.GetStringSize(code, HorizontalAlignment.Left, -1, LabelFontSize).X;
        return width;
    }

    // the dev trackers: a dot per living mob, a player's green
    private void DrawMobs(int z, float ppt)
    {
        if (_mobs == null || _mobs.Z != z)
            return;
        float radius = Mathf.Max(2f, ppt * 0.45f);
        var panel = new Rect2(Vector2.Zero, Size);
        foreach (MinimapMob mob in _mobs.Mobs)
        {
            Vector2 at = ToPanel(mob.X, mob.Y);
            if (!panel.HasPoint(at))
                continue;
            DrawCircle(at, radius + 1f, Colors.Black);
            DrawCircle(at, radius, mob.Player ? PlayerDot : MobDot);
        }
    }

    private void DrawArrow()
    {
        Vector2 mid = ToPanel(_mob.X, _mob.Y);
        if (!new Rect2(Vector2.Zero, Size).HasPoint(mid))
            return; // the map was dragged away from the mob
        Vector2[] points = { new(0, -8), new(5.5f, 6), new(0, 3), new(-5.5f, 6) };
        for (int i = 0; i < points.Length; i++)
            points[i] = mid + points[i].Rotated(_heading);
        DrawColoredPolygon(points, Arrow);
        DrawPolyline([.. points, points[0]], Colors.Black, 1.5f, true);
    }

    // tg's hover text: the name of the area under the cursor, beside it
    private void DrawHover(MinimapLayout layout, float ppt)
    {
        if (_hover is not Vector2 at)
            return;
        Vector2 tile = (at - Size / 2) / ppt;
        int x = Mathf.RoundToInt(_centre.X + tile.X), y = Mathf.RoundToInt(_centre.Y - tile.Y);
        string name = (_mobs?.Z == layout.Map.Z ? MinimapLayout.MobAt(_mobs, x, y) : null) ?? layout.AreaNameAt(x, y); // tg's hover: a blip first
        if (string.IsNullOrEmpty(name))
            return;
        Font font = ThemeDB.FallbackFont;
        Vector2 size = font.GetStringSize(name, HorizontalAlignment.Left, -1, HoverFontSize);
        var box = new Rect2(at + new Vector2(10, -size.Y - 4), size + new Vector2(8, 4));
        box.Position = new Vector2(Mathf.Clamp(box.Position.X, 0, Mathf.Max(0, Size.X - box.Size.X)), Mathf.Clamp(box.Position.Y, 0, Mathf.Max(0, Size.Y - box.Size.Y)));
        DrawRect(box, new Color(0, 0, 0, 0.75f));
        DrawString(font, box.Position + new Vector2(4, 2 + font.GetAscent(HoverFontSize)), name, HorizontalAlignment.Left, -1, HoverFontSize, Colors.White);
    }
}

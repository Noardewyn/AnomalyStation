using Godot;

namespace Anomaly.Client.Hud;

/// <summary>A high-contrast APC bearing around the crosshair or free mouse pointer.</summary>
public partial class MultitoolIndicator : Control
{
    private float _angle;
    private Color _color;
    private float _scale = 1;

    public MultitoolIndicator() => MouseFilter = MouseFilterEnum.Ignore;

    public void Set(Vector2 center, double angle, Color color, float scale)
    {
        Position = center;
        _angle = (float)angle;
        float peak = Mathf.Max(color.R, Mathf.Max(color.G, color.B));
        _color = peak > 0 ? new Color(color.R / peak, color.G / peak, color.B / peak, 1) : Colors.White;
        _scale = scale;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 direction = new(Mathf.Sin(_angle), -Mathf.Cos(_angle));
        Vector2 side = new(-direction.Y, direction.X);
        Vector2 center = direction * (48 * _scale);
        Vector2[] arrow = { center + direction * (16 * _scale), center - direction * (11 * _scale) + side * (10 * _scale), center - direction * (6 * _scale), center - direction * (11 * _scale) - side * (10 * _scale) };
        DrawCircle(center, 20 * _scale, new Color(0.02f, 0.02f, 0.02f, 0.85f));
        DrawColoredPolygon(arrow, _color);
        DrawPolyline(new[] { arrow[0], arrow[1], arrow[2], arrow[3], arrow[0] }, Colors.White, 1.5f * _scale, true);
    }
}

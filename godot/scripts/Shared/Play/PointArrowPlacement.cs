using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

public static class PointArrowPlacement
{
    // tg spawns the arrow on the pointer's turf and moves its picture to the target using full-size map pixels.
    public static (float X, float Y) Target(float x, float y, AppearanceEntry appearance)
        => (x + appearance.PixelX / 32f, y + appearance.PixelY / 32f);

    /// <summary>tg's slide of the arrow to the pointed thing (point.dm: animate over 1.7 ds), in seconds.</summary>
    public const double SlideSeconds = 0.17;

    /// <summary>How far along its slide the arrow is <paramref name="seconds"/> after it came, 0 to 1 (BYOND's EASE_OUT of a quadratic curve).</summary>
    public static float Slide(double seconds)
    {
        double t = Math.Clamp(seconds / SlideSeconds, 0, 1);
        return (float)(1 - (1 - t) * (1 - t));
    }

    /// <summary>The distance (metres) at which the arrow is drawn at its own scale; nearer it shrinks, farther it grows, keeping its size on the screen.</summary>
    public const float ScreenScaleMetres = 4f / 3f;

    /// <summary>The arrow's scale <paramref name="metres"/> from the eye for a constant size on the screen (<paramref name="scale"/> at <see cref="ScreenScaleMetres"/>).</summary>
    public static float ScreenScale(float metres, float scale) => scale * Math.Max(metres, 0.1f) / ScreenScaleMetres;
}

namespace Anomaly.Client.Shared;

/// <summary>
/// DV96 (the owner, 2026-11-21: a shooter's crosshair, protocol 92): how far from the screen's middle the gun crosshair's lines start, the held
/// gun's cone (GunSight) as it looks at the camera's field of view, opened for a moment by each shot.
/// </summary>
public static class GunSightGap
{
    /// <summary>The nearest the lines come to the middle (a gun with no spread), and how far a shot opens them, pixels.</summary>
    public const float MinPixels = 5f, BloomPixels = 10f;

    /// <summary>
    /// The crosshair's drawing (Hud/OptionsPanel.cs, Crosshair), before the options' crosshair size: how long a gun sight's line is, where the
    /// target crosshair's arms end, and how far a line's shadow reaches past its end (KeyChips.SightReach).
    /// </summary>
    public const float LinePixels = 8f, ArmEndPixels = 15f, ShadowPixels = 2f;

    /// <summary>How long a shot keeps the lines open, seconds (they close linearly).</summary>
    public const double BloomSeconds = 0.25;

    /// <summary>
    /// The gap in screen pixels: a cone of <paramref name="coneDegrees"/> off the aim on a view <paramref name="heightPixels"/> tall whose vertical
    /// field of view is <paramref name="fovDegrees"/>, <paramref name="sinceShot"/> seconds after the last shot; at most half the height.
    /// </summary>
    public static float Pixels(float coneDegrees, float fovDegrees, float heightPixels, double sinceShot)
    {
        double half = Math.Clamp(fovDegrees, 1f, 179f) * Math.PI / 360.0;
        double cone = Math.Clamp(coneDegrees, 0f, 89f) * Math.PI / 180.0;
        double gap = heightPixels / 2.0 * Math.Tan(cone) / Math.Tan(half);
        double bloom = sinceShot >= 0 && sinceShot < BloomSeconds ? BloomPixels * (1 - sinceShot / BloomSeconds) : 0;
        return (float)Math.Clamp(Math.Max(gap, MinPixels) + bloom, MinPixels, Math.Max(MinPixels, heightPixels / 2.0));
    }
}

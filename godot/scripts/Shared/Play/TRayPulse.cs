namespace Anomaly.Client.Shared;

// DEVIATION(DV91): the owner (D289) brought tg's T-ray blink back as a slower pulse: the images show for 3 s, fading in and out, and are
// gone for 1.5 s. The server keeps them steady (steady_flick_overlay); the client fades them. Engine-free (compiled into the tests too).

/// <summary>The T-ray images' opacity over time.</summary>
public static class TRayPulse
{
    public const double Shown = 3.0, Hidden = 1.5, Fade = 0.4;

    /// <summary>tg's T-ray copy alpha (t_scanner.dm:59): how the client tells a T-ray image from the goggles' other images.</summary>
    public const byte ImageAlpha = 128;

    /// <summary>The share of the image's own alpha at <paramref name="seconds"/>: 0 to 1 over the fade in, 1, 1 to 0 over the fade out, then 0.</summary>
    public static float Opacity(double seconds)
    {
        double t = seconds % (Shown + Hidden);
        if (t < 0)
            t += Shown + Hidden;
        if (t >= Shown)
            return 0f;
        return (float)Math.Clamp(Math.Min(t, Shown - t) / Fade, 0.0, 1.0);
    }
}

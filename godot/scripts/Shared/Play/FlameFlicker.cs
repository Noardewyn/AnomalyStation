namespace Anomaly.Client.Shared.Play;

/// <summary>
/// DV688 (protocol version 99): tg's flame flicker (<c>fire_flicker_middleman</c>, light_middleman.dm:125) as a light's brightness over
/// time. tg loops its light's alpha down to 235 and back to 255 in 17 random cycles (down over 0.125 s, eased out, held, up over 0.125 s,
/// eased out, held 0.275 s; each cycle's down time shifted by up to 0.075 s, the others taken from the first cycle, as tg's loop reads
/// them). The client multiplies the light's energy by alpha / 255. tg draws the shifts from an inverse Cauchy curve; here they are even.
/// Engine-free, so it is tested without Godot.
/// </summary>
public sealed class FlameFlicker
{
    private const int Cycles = 17;
    private const double Low = 235.0 / 255.0;

    private readonly double[] _down = new double[Cycles];
    private readonly double _bottom, _up, _top;
    private readonly double _length;

    /// <summary>The flicker of the light <paramref name="seed"/> names (each light flickers on its own).</summary>
    public FlameFlicker(int seed)
    {
        var random = new Random(seed);
        double Shift(double size) => (random.NextDouble() * 2 - 1) * size;
        for (int i = 0; i < Cycles; i++)
            _down[i] = Math.Max(0.01, 0.125 + Shift(0.075));
        _bottom = Math.Max(0, 0 + Shift(0.05));
        _up = Math.Max(0.01, 0.125 + Shift(0.075));
        _top = Math.Max(0, 0.275 + Shift(0.225));
        foreach (double down in _down)
            _length += down + _bottom + _up + _top;
    }

    /// <summary>The light's brightness (235/255 to 1) <paramref name="seconds"/> into the loop.</summary>
    public double At(double seconds)
    {
        double t = seconds % _length;
        if (t < 0)
            t += _length;
        for (int i = 0; i < Cycles; i++)
        {
            double down = _down[i];
            if (t < down)
                return Mathf(1, Low, EaseOut(t / down));
            t -= down;
            if (t < _bottom)
                return Low;
            t -= _bottom;
            if (t < _up)
                return Mathf(Low, 1, EaseOut(t / _up));
            t -= _up;
            if (t < _top)
                return 1;
            t -= _top;
        }
        return 1;
    }

    // BYOND's CUBIC_EASING | EASE_OUT
    private static double EaseOut(double x)
    {
        double inv = 1 - Math.Clamp(x, 0, 1);
        return 1 - inv * inv * inv;
    }

    private static double Mathf(double from, double to, double f) => from + (to - from) * f;
}

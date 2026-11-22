namespace Anomaly.Client.Shared.Icons;

// Engine-free. A sprite's colour as one value: what a flat-colour 3D part takes from a tg picture (a held tank's, the gases' in a
// canister's window) and what the gas haze and the beams see of a gas (GasLayer).

/// <summary>The mean colour of a state's pictures.</summary>
public static class SpriteColors
{
    /// <summary>
    /// The colour of a state's first direction over its frames, weighted by alpha (0..1 sRGB), and its coverage (the mean alpha over every pixel).
    /// <paramref name="dominant"/>: the colour of its coloured pixels only (saturation above a quarter) when they are a fifth of what it covers or
    /// more: a blue oxygen tank's blue, not the grey of its valve mixed in; else of all of them.
    /// </summary>
    public static (float R, float G, float B, float Coverage) Mean(IIconState state, bool dominant = false)
    {
        var sum = new Sum();
        for (int f = 0; f < state.Frames; f++)
            sum.Add(state.Get(0, f).Pixels);
        return sum.Result(dominant);
    }

    /// <summary>The same for one picture's pixels (R, G, B, A rows: <see cref="Raster.Pixels"/>): a composed projectile's colour (ShotLook).</summary>
    public static (float R, float G, float B, float Coverage) Mean(byte[] pixels, bool dominant = false)
    {
        var sum = new Sum();
        sum.Add(pixels);
        return sum.Result(dominant);
    }

    private struct Sum
    {
        private double _r, _g, _b, _a, _cr, _cg, _cb, _ca;
        private long _pixels;

        public void Add(byte[] p)
        {
            for (int j = 0; j + 3 < p.Length; j += 4)
            {
                double alpha = p[j + 3] / 255.0, pr = p[j] / 255.0, pg = p[j + 1] / 255.0, pb = p[j + 2] / 255.0;
                _r += pr * alpha;
                _g += pg * alpha;
                _b += pb * alpha;
                _a += alpha;
                _pixels++;
                double max = Math.Max(pr, Math.Max(pg, pb)), min = Math.Min(pr, Math.Min(pg, pb));
                if (max > 0 && (max - min) / max > 0.25)
                {
                    _cr += pr * alpha;
                    _cg += pg * alpha;
                    _cb += pb * alpha;
                    _ca += alpha;
                }
            }
        }

        public readonly (float R, float G, float B, float Coverage) Result(bool dominant)
        {
            if (_a <= 0 || _pixels == 0)
                return (0.8f, 0.8f, 0.8f, 0f);
            float coverage = (float)(_a / _pixels);
            if (dominant && _ca >= 0.2 * _a)
                return ((float)(_cr / _ca), (float)(_cg / _ca), (float)(_cb / _ca), coverage);
            return ((float)(_r / _a), (float)(_g / _a), (float)(_b / _a), coverage);
        }
    }
}

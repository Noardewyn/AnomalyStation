namespace Anomaly.Client.Shared.Icons;

/// <summary>
/// A 2D affine transform in BYOND's matrix convention (<c>/matrix(a, b, c, d, e, f)</c>): <c>x' = a*x + b*y + c</c>, <c>y' = d*x + e*y + f</c>,
/// with x to the right and y up, in pixels (Anomaly.Core.DmShim.matrix; the six components of <c>AppearanceEntry.Transform</c>).
/// </summary>
public readonly struct Affine
{
    public readonly double A, B, C, D, E, F;

    public Affine(double a, double b, double c, double d, double e, double f)
    {
        A = a;
        B = b;
        C = c;
        D = d;
        E = e;
        F = f;
    }

    /// <summary>No change.</summary>
    public static Affine Identity => new(1, 0, 0, 0, 1, 0);

    /// <summary>A move by (<paramref name="x"/>, <paramref name="y"/>) pixels.</summary>
    public static Affine Translate(double x, double y) => new(1, 0, x, 0, 1, y);

    /// <summary>From the six components of a BYOND matrix (null or a wrong length: identity).</summary>
    public static Affine FromMatrix(IReadOnlyList<float> m)
        => m == null || m.Count != 6 ? Identity : new Affine(m[0], m[1], m[2], m[3], m[4], m[5]);

    /// <summary>True when this is the identity.</summary>
    public bool IsIdentity => A == 1 && B == 0 && C == 0 && D == 0 && E == 1 && F == 0;

    /// <summary>True when only a move (no rotation or scale).</summary>
    public bool IsTranslation => A == 1 && B == 0 && D == 0 && E == 1;

    /// <summary>"First this, then <paramref name="next"/>": BYOND's <c>Multiply</c> order.</summary>
    public Affine Then(Affine next) => new(
        next.A * A + next.B * D, next.A * B + next.B * E, next.A * C + next.B * F + next.C,
        next.D * A + next.E * D, next.D * B + next.E * E, next.D * C + next.E * F + next.F);

    /// <summary>Maps a point.</summary>
    public (double X, double Y) Apply(double x, double y) => (A * x + B * y + C, D * x + E * y + F);

    /// <summary>The inverse; false for a degenerate matrix.</summary>
    public bool TryInvert(out Affine inverse)
    {
        double det = A * E - B * D;
        if (Math.Abs(det) < 1e-12)
        {
            inverse = Identity;
            return false;
        }
        double ia = E / det, ib = -B / det, id = -D / det, ie = A / det;
        inverse = new Affine(ia, ib, -(ia * C + ib * F), id, ie, -(id * C + ie * F));
        return true;
    }
}

namespace Anomaly.Core.DmShim;

/// <summary>
/// The BYOND <c>/matrix</c> (BYOND ref <c>/matrix</c>): a 2D affine transform with the six components
/// <c>a b c / d e f</c>, mapping a point as <c>x' = a*x + b*y + c</c>, <c>y' = d*x + e*y + f</c>; created by
/// <c>matrix(...)</c> and used as <c>atom.transform</c>. A builtin value type: no world id, never registered. Owner:
/// values. The procs keep their DM names (<c>M.Turn(90)</c>, as the list procs do on <see cref="DmList"/>) and, like
/// BYOND's, change the matrix itself and return it; the operators are <c>Dm.add</c>, <c>Dm.sub</c>, <c>Dm.mul</c>,
/// <c>Dm.div</c>, <c>Dm.equiv</c> and the compound assignments. <c>Interpolate</c> is not implemented yet (BYOND
/// decomposes the matrices first; no M0 code needs it).
/// </summary>
[DmPath("/matrix")]
public class matrix
{
    public float a = 1;
    public float b;
    public float c;
    public float d;
    public float e = 1;
    public float f;

    /// <summary>The identity matrix (<c>new /matrix</c>).</summary>
    public matrix()
    {
    }

    internal matrix(float a, float b, float c, float d, float e, float f)
    {
        this.a = a;
        this.b = b;
        this.c = c;
        this.d = d;
        this.e = e;
        this.f = f;
    }

    /// <summary>
    /// DM <c>M.Turn(angle)</c> (BYOND ref <c>/matrix/proc/Turn</c>): rotates the matrix clockwise by angle degrees
    /// (src, then the rotation <c>cos sin 0 / -sin cos 0</c>, OpenDream <c>_NativeProc_TurnInternal</c>, which computes
    /// the sine and cosine in double and flushes subnormal results to 0); returns src.
    /// </summary>
    public matrix Turn(float angle)
    {
        (double sinD, double cosD) = Math.SinCos(Math.PI / 180.0 * angle);
        float sin = (float)sinD, cos = (float)cosD;
        if (float.IsSubnormal(sin)) sin = 0f;
        if (float.IsSubnormal(cos)) cos = 0f;
        return Multiply(new matrix(cos, sin, 0f, -sin, cos, 0f));
    }

    /// <summary>DM <c>M.Scale(x, y)</c> (BYOND ref <c>/matrix/proc/Scale</c>): scales by x and y ("If y is omitted, x is used for both"); returns src.</summary>
    public matrix Scale(float x, float? y = null)
    {
        float sy = y ?? x;
        a *= x;
        b *= x;
        c *= x;
        d *= sy;
        e *= sy;
        f *= sy;
        return this;
    }

    /// <summary>DM <c>M.Translate(x, y)</c> (BYOND ref <c>/matrix/proc/Translate</c>): moves by x and y pixels (y omitted: x for both); returns src.</summary>
    public matrix Translate(float x, float? y = null)
    {
        c += x;
        f += y ?? x;
        return this;
    }

    /// <summary>
    /// DM <c>M.Multiply(Matrix2)</c> and <c>M.Multiply(n)</c> (BYOND ref <c>/matrix/proc/Multiply</c>): "like doing the
    /// two transformations in order: src, then Matrix2"; a number scales the whole matrix; null leaves it alone and any
    /// other value makes it the zero matrix (OpenDream <c>NativeProc_Multiply</c>); returns src.
    /// </summary>
    public matrix Multiply(object Matrix2)
    {
        switch (Matrix2)
        {
            case matrix r:
            {
                float la = a, lb = b, lc = c, ld = d, le = e, lf = f;
                a = (float)(la * r.a + ld * r.b);
                b = (float)(lb * r.a + le * r.b);
                c = (float)(lc * r.a + lf * r.b + r.c);
                d = (float)(la * r.d + ld * r.e);
                e = (float)(lb * r.d + le * r.e);
                f = (float)(lc * r.d + lf * r.e + r.f);
                return this;
            }
            case null:
                return this;
            default:
                float n = Dm.TryGetNumber(Matrix2, out float factor) ? factor : 0f;
                return Scale(n, n);
        }
    }

    /// <summary>
    /// DM <c>M.Invert()</c> (BYOND ref <c>/matrix/proc/Invert</c>): inverts the matrix; "A degenerate matrix will not be
    /// changed by this proc"; returns src.
    /// </summary>
    public matrix Invert()
    {
        TryInvert();
        return this;
    }

    /// <summary>DM <c>M.Add(Matrix2)</c> (BYOND ref <c>/matrix/proc/Add</c>): adds the components of Matrix2; returns src.</summary>
    public matrix Add(matrix Matrix2)
    {
        if (Matrix2 == null) throw new DmRuntimeException("matrix.Add() needs a matrix");
        a += Matrix2.a;
        b += Matrix2.b;
        c += Matrix2.c;
        d += Matrix2.d;
        e += Matrix2.e;
        f += Matrix2.f;
        return this;
    }

    /// <summary>DM <c>M.Subtract(Matrix2)</c> (BYOND ref <c>/matrix/proc/Subtract</c>): subtracts the components of Matrix2; returns src.</summary>
    public matrix Subtract(matrix Matrix2)
    {
        if (Matrix2 == null) throw new DmRuntimeException("matrix.Subtract() needs a matrix");
        a -= Matrix2.a;
        b -= Matrix2.b;
        c -= Matrix2.c;
        d -= Matrix2.d;
        e -= Matrix2.e;
        f -= Matrix2.f;
        return this;
    }

    internal matrix Copy() => new matrix(a, b, c, d, e, f);

    // The inverse in place (OpenDream DreamObjectMatrix.TryInvert); false, and unchanged, for a degenerate matrix.
    internal bool TryInvert()
    {
        float det = (float)(a * e - d * b);
        if (det == 0f) return false;
        float oa = a, ob = b, oc = c, od = d, oe = e, of = f;
        a = oe / det;
        b = -ob / det;
        c = (float)(ob * of - oe * oc) / det;
        d = -od / det;
        e = oa / det;
        f = (float)(od * oc - oa * of) / det;
        return true;
    }
}

public static partial class Dm
{
    /// <summary>DM <c>matrix()</c> (BYOND ref <c>/proc/matrix</c>): a new identity matrix.</summary>
    public static matrix matrix() => new matrix();

    /// <summary>DM <c>matrix(Matrix)</c>: a copy of a matrix (null gives the identity; any other value is a runtime error).</summary>
    public static matrix matrix(object Matrix) => Matrix switch
    {
        matrix m => m.Copy(),
        null => new matrix(),
        _ => throw new DmRuntimeException("matrix(): " + KindOf(Matrix) + " is not a matrix"),
    };

    /// <summary>DM <c>matrix(n, MATRIX_SCALE)</c> and <c>matrix(angle, MATRIX_ROTATE)</c>: a scale or clockwise rotation matrix.</summary>
    public static matrix matrix(float a, float b)
    {
        if (b == MATRIX_SCALE) return new matrix(a, 0f, 0f, 0f, a, 0f);
        if (b == MATRIX_ROTATE) return new matrix().Turn(a);
        throw new DmRuntimeException("matrix(): unknown operation " + num2text(b));
    }

    /// <summary>DM <c>matrix(x, y, MATRIX_SCALE)</c> and <c>matrix(x, y, MATRIX_TRANSLATE)</c>.</summary>
    public static matrix matrix(float a, float b, float c)
    {
        if (c == MATRIX_SCALE) return new matrix(a, 0f, 0f, 0f, b, 0f);
        if (c == MATRIX_TRANSLATE) return new matrix(1f, 0f, a, 0f, 1f, b);
        throw new DmRuntimeException("matrix(): unknown operation " + num2text(c));
    }

    /// <summary>DM <c>matrix(a, b, c, d, e, f)</c>: a matrix with these components.</summary>
    public static matrix matrix(float a, float b, float c, float d, float e, float f) => new matrix(a, b, c, d, e, f);
}

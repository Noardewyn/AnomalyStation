using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// The <c>/matrix</c> builtin (BYOND ref <c>/matrix</c>, <c>/proc/matrix</c>, the matrix procs and "matrix operators"):
/// <c>x' = a*x + b*y + c</c>, <c>y' = d*x + e*y + f</c>; rotation is clockwise; <c>Multiply</c> is "src, then
/// Matrix2"; OpenDream <c>DreamObjectMatrix</c> and test Operators/equivalence.
/// </summary>
public class MatrixTests
{
    private static void Components(matrix m, float a, float b, float c, float d, float e, float f)
    {
        Assert.Equal(a, m.a, 5);
        Assert.Equal(b, m.b, 5);
        Assert.Equal(c, m.c, 5);
        Assert.Equal(d, m.d, 5);
        Assert.Equal(e, m.e, 5);
        Assert.Equal(f, m.f, 5);
    }

    [Fact]
    public void Construction()
    {
        // BYOND ref /proc/matrix: the identity, a copy, the components, and the MATRIX_SCALE / MATRIX_ROTATE /
        // MATRIX_TRANSLATE shortcuts.
        Components(Dm.matrix(), 1, 0, 0, 0, 1, 0);
        Components(Dm.matrix(1, 2, 3, 4, 5, 6), 1, 2, 3, 4, 5, 6);
        Components(Dm.matrix(2, Dm.MATRIX_SCALE), 2, 0, 0, 0, 2, 0);
        Components(Dm.matrix(2, 3, Dm.MATRIX_SCALE), 2, 0, 0, 0, 3, 0);
        Components(Dm.matrix(3, 4, Dm.MATRIX_TRANSLATE), 1, 0, 3, 0, 1, 4);
        Components(Dm.matrix(90, Dm.MATRIX_ROTATE), 0, 1, 0, -1, 0, 0);
        matrix original = Dm.matrix(1, 2, 3, 4, 5, 6);
        matrix copy = Dm.matrix(original);
        Assert.NotSame(original, copy);
        Components(copy, 1, 2, 3, 4, 5, 6);
        Components(Dm.matrix(null), 1, 0, 0, 0, 1, 0);
        V.Error(() => Dm.matrix(1, 99));
        V.Error(() => Dm.matrix("text"));
    }

    [Fact]
    public void ProcsChangeTheMatrixAndReturnIt()
    {
        // BYOND ref Turn ("rotated clockwise"), Scale ("If y is omitted, x is used for both"), Translate, Multiply
        // ("src, then Matrix2"), Invert ("A degenerate matrix will not be changed"), Add, Subtract.
        matrix m = Dm.matrix();
        Assert.Same(m, m.Turn(90));
        Components(m, 0, 1, 0, -1, 0, 0);            // (1, 0) east turns to (0, -1)
        Components(Dm.matrix().Scale(2), 2, 0, 0, 0, 2, 0);
        Components(Dm.matrix().Scale(2, 3), 2, 0, 0, 0, 3, 0);
        Components(Dm.matrix().Translate(3), 1, 0, 3, 0, 1, 3);
        Components(Dm.matrix().Scale(2).Translate(3, 4), 2, 0, 3, 0, 2, 4);
        Components(Dm.matrix().Scale(2).Multiply(Dm.matrix(3, 0, Dm.MATRIX_TRANSLATE)), 2, 0, 3, 0, 2, 0);
        Components(Dm.matrix(3, 0, Dm.MATRIX_TRANSLATE).Multiply(Dm.matrix(2, Dm.MATRIX_SCALE)), 2, 0, 6, 0, 2, 0);
        Components(Dm.matrix().Multiply(3f), 3, 0, 0, 0, 3, 0);
        Components(Dm.matrix(1, 2, 3, 4, 5, 6).Multiply(null), 1, 2, 3, 4, 5, 6);
        Components(Dm.matrix(2, 0, 3, 0, 2, 3).Invert(), 0.5f, 0, -1.5f, 0, 0.5f, -1.5f);
        Components(Dm.matrix(0, 0, 0, 0, 0, 0).Invert(), 0, 0, 0, 0, 0, 0);
        Components(Dm.matrix(1, 2, 3, 4, 5, 6).Add(Dm.matrix(1, 1, 1, 1, 1, 1)), 2, 3, 4, 5, 6, 7);
        Components(Dm.matrix(1, 2, 3, 4, 5, 6).Subtract(Dm.matrix(1, 1, 1, 1, 1, 1)), 0, 1, 2, 3, 4, 5);
        Components(Dm.matrix().Turn(90).Turn(-90), 1, 0, 0, 0, 1, 0);
    }

    [Fact]
    public void Operators()
    {
        // BYOND ref * ("matrix matrix: New matrix, product of A and B"; "num: New matrix scaling A by B"), + and -
        // componentwise; *=, += change the matrix itself (BYOND ref *=, +=).
        matrix m = Dm.matrix(1, 2, 3, 4, 5, 6);
        var scaled = (matrix)Dm.mul(m, 2f);
        Components(scaled, 2, 4, 6, 8, 10, 12);
        Components(m, 1, 2, 3, 4, 5, 6);
        Components((matrix)Dm.div(m, (object)2f), 0.5f, 1, 1.5f, 2, 2.5f, 3);
        Components((matrix)Dm.add(m, Dm.matrix(1, 1, 1, 1, 1, 1)), 2, 3, 4, 5, 6, 7);
        Components((matrix)Dm.sub(m, Dm.matrix(1, 1, 1, 1, 1, 1)), 0, 1, 2, 3, 4, 5);
        matrix s = Dm.matrix(2, Dm.MATRIX_SCALE);
        matrix t = Dm.matrix(3, 0, Dm.MATRIX_TRANSLATE);
        Components((matrix)Dm.mul(s, t), 2, 0, 3, 0, 2, 0);
        Components((matrix)Dm.div(Dm.matrix(2, 0, 3, 0, 2, 3), Dm.matrix(2, 0, 3, 0, 2, 3)), 1, 0, 0, 0, 1, 0);
        object target = m;
        Dm.append(ref target, Dm.matrix(1, 1, 1, 1, 1, 1));
        Assert.Same(m, target);
        Components(m, 2, 3, 4, 5, 6, 7);
        Dm.remove(ref target, Dm.matrix(1, 1, 1, 1, 1, 1));
        Components(m, 1, 2, 3, 4, 5, 6);
        V.Error(() => Dm.add(m, 1f));
        V.Error(() => Dm.div(m, (object)0f));
    }
}

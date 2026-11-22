// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/matrices.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. tg's procs on the builtin /matrix are C# extension members of the shim's matrix (receiver src) in
// GlobalProcs, as the m2-ownership table says: a builtin value type has no virtual slot for Tg to fill, so they are not
// virtual (slots.tsv lists them as /matrix roots without overrides).

/// Datum which stores information about a matrix decomposed with decompose().
// tg: code/__HELPERS/matrices.dm:2
[DmPath("/datum/decompose_matrix")]
public partial class datum_decompose_matrix : datum
{
    ///?
    // tg: code/__HELPERS/matrices.dm:4
    public float scale_x = 1;
    ///?
    // tg: code/__HELPERS/matrices.dm:6
    public float scale_y = 1;
    ///?
    // tg: code/__HELPERS/matrices.dm:8
    public float rotation = 0;
    ///?
    // tg: code/__HELPERS/matrices.dm:10
    public float shift_x = 0;
    ///?
    // tg: code/__HELPERS/matrices.dm:12
    public float shift_y = 0;
}

// tg: code/__HELPERS/matrices.dm:20 (the /matrix procs and the global procs)
public static partial class GlobalProcs
{
    /// Decomposes a matrix into scale, shift and rotation.
    ///
    /// If other operations were applied on the matrix, such as shearing, the result
    /// will not be precise.
    ///
    /// Negative scales are now supported. =)
    // tg: code/__HELPERS/matrices.dm:20
    public static object decompose(this matrix src)
    {
        object dot = null;
        datum_decompose_matrix decompose_matrix = Dm.@new<datum_decompose_matrix>();
        dot = decompose_matrix;
        float flip_sign = (src.a * src.e - src.b * src.d < 0) ? -1 : 1; // Det < 0 => only 1 axis is flipped - start doing some sign flipping
        // If both axis are flipped, nothing bad happens and Det >= 0, it just treats it like a 180° rotation
        // If only 1 axis is flipped, we need to flip one direction - in this case X, so we flip a, b and the x scaling
        decompose_matrix.scale_x = sqrt(src.a * src.a + src.d * src.d) * flip_sign;
        decompose_matrix.scale_y = sqrt(src.b * src.b + src.e * src.e);
        decompose_matrix.shift_x = src.c;
        decompose_matrix.shift_y = src.f;
        if (!Dm.truthy(decompose_matrix.scale_x) || !Dm.truthy(decompose_matrix.scale_y))
            return dot;
        // If only translated, scaled and rotated, a/xs == e/ys and -d/xs == b/xy
        float cossine = (Dm.num(Dm.div(src.a, decompose_matrix.scale_x)) + Dm.num(Dm.div(src.e, decompose_matrix.scale_y))) / 2;
        float sine = (Dm.num(Dm.div(src.b, decompose_matrix.scale_y)) - Dm.num(Dm.div(src.d, decompose_matrix.scale_x))) / 2 * flip_sign;
        decompose_matrix.rotation = arctan(cossine, sine) * flip_sign;
        return dot;
    }

    // tg: code/__HELPERS/matrices.dm:37
    public static object TurnTo(this matrix src, object old_angle = null, object new_angle = null)
    {
        return src.Turn(Dm.num(Dm.sub(new_angle, old_angle))); //BYOND handles cases such as -270, 360, 540 etc. DOES NOT HANDLE 180 TURNS WELL, THEY TWEEN AND LOOK LIKE SHIT
    }

    /**
     * Shear the transform on either or both axes.
     * * x - X axis shearing
     * * y - Y axis shearing
     */
    // tg: code/__HELPERS/matrices.dm:45
    public static object Shear(this matrix src, object x = null, object y = null)
    {
        return src.Multiply(Dm.matrix(1, Dm.num(x), 0, Dm.num(y), 1, 0));
    }

    //Dumps the matrix data in format a-f
    // tg: code/__HELPERS/matrices.dm:49
    public static object tolist(this matrix src)
    {
        DmList dot = null;
        dot = Dm.list();
        Dm.append(ref dot, src.a);
        Dm.append(ref dot, src.b);
        Dm.append(ref dot, src.c);
        Dm.append(ref dot, src.d);
        Dm.append(ref dot, src.e);
        Dm.append(ref dot, src.f);
        return dot;
    }

    //Dumps the matrix data in a matrix-grid format
    /*
    a d 0
    b e 0
    c f 1
    */
    // tg: code/__HELPERS/matrices.dm:64
    public static object togrid(this matrix src)
    {
        DmList dot = null;
        dot = Dm.list();
        Dm.append(ref dot, src.a);
        Dm.append(ref dot, src.d);
        Dm.append(ref dot, 0);
        Dm.append(ref dot, src.b);
        Dm.append(ref dot, src.e);
        Dm.append(ref dot, 0);
        Dm.append(ref dot, src.c);
        Dm.append(ref dot, src.f);
        Dm.append(ref dot, 1);
        return dot;
    }

    ///The X pixel offset of this matrix
    // tg: code/__HELPERS/matrices.dm:77
    public static object get_x_shift(this matrix src)
    {
        object dot = null;
        dot = src.c;
        return dot;
    }

    ///The Y pixel offset of this matrix
    // tg: code/__HELPERS/matrices.dm:81
    public static object get_y_shift(this matrix src)
    {
        object dot = null;
        dot = src.f;
        return dot;
    }

    ///The angle of this matrix
    // tg: code/__HELPERS/matrices.dm:85
    public static object get_angle(this matrix src)
    {
        object dot = null;
        // GAP(lead, __DEFINES/maths.cs): tg's ATAN2(x, y) (__DEFINES/maths.dm:68) has no GlobalProcs member yet; expanded
        // inline here (rules 11.1) with x = a and y = d.
        dot = -(!Dm.truthy(src.a) && !Dm.truthy(src.d) ? 0 : src.d >= 0 ? arccos(src.a / sqrt(src.a * src.a + src.d * src.d)) : -arccos(src.a / sqrt(src.a * src.a + src.d * src.d)));
        return dot;
    }

    /////////////////////
    // COLOUR MATRICES //
    /////////////////////

    /* Documenting a couple of potentially useful color matrices here to inspire ideas
    // Greyscale - indentical to saturation @ 0
    list(LUMA_R,LUMA_R,LUMA_R,0, LUMA_G,LUMA_G,LUMA_G,0, LUMA_B,LUMA_B,LUMA_B,0, 0,0,0,1, 0,0,0,0)

    // Color inversion
    list(-1,0,0,0, 0,-1,0,0, 0,0,-1,0, 0,0,0,1, 1,1,1,0)

    // Sepiatone
    list(0.393,0.349,0.272,0, 0.769,0.686,0.534,0, 0.189,0.168,0.131,0, 0,0,0,1, 0,0,0,0)
    */

    //Changes distance hues have from grey while maintaining the overall lightness. Greys are unaffected.
    //1 is identity, 0 is greyscale, >1 oversaturates colors
    // tg: code/__HELPERS/matrices.dm:105
    public static object color_matrix_saturation(object value = null)
    {
        float inv = 1 - Dm.num(value);
        float R = round(LUMA_R * inv, 0.001f);
        float G = round(LUMA_G * inv, 0.001f);
        float B = round(LUMA_B * inv, 0.001f);

        return Dm.list(Dm.add(R, value), R, R, 0, G, Dm.add(G, value), G, 0, B, B, Dm.add(B, value), 0, 0, 0, 0, 1, 0, 0, 0, 0);
    }

    //Moves all colors angle degrees around the color wheel while maintaining intensity of the color and not affecting greys
    //0 is identity, 120 moves reds to greens, 240 moves reds to blues
    // tg: code/__HELPERS/matrices.dm:115
    public static object color_matrix_rotate_hue(object angle = null)
    {
        float sin = Dm.sin(Dm.num(angle));
        float cos = Dm.cos(Dm.num(angle));
        float cos_inv_third = 0.333f * (1 - cos);
        float sqrt3_sin = sqrt(3) * sin;
        return Dm.list(
            round(cos + cos_inv_third, 0.001f), round(cos_inv_third + sqrt3_sin, 0.001f), round(cos_inv_third - sqrt3_sin, 0.001f), 0,
            round(cos_inv_third - sqrt3_sin, 0.001f), round(cos + cos_inv_third, 0.001f), round(cos_inv_third + sqrt3_sin, 0.001f), 0,
            round(cos_inv_third + sqrt3_sin, 0.001f), round(cos_inv_third - sqrt3_sin, 0.001f), round(cos + cos_inv_third, 0.001f), 0,
            0, 0, 0, 1,
            0, 0, 0, 0);
    }

    //These next three rotate values about one axis only
    //x is the red axis, y is the green axis, z is the blue axis.
    // tg: code/__HELPERS/matrices.dm:129
    public static object color_matrix_rotate_x(object angle = null)
    {
        float sinval = round(Dm.sin(Dm.num(angle)), 0.001f); float cosval = round(Dm.cos(Dm.num(angle)), 0.001f);
        return Dm.list(1, 0, 0, 0, 0, cosval, sinval, 0, 0, -sinval, cosval, 0, 0, 0, 0, 1, 0, 0, 0, 0);
    }

    // tg: code/__HELPERS/matrices.dm:133
    public static object color_matrix_rotate_y(object angle = null)
    {
        float sinval = round(Dm.sin(Dm.num(angle)), 0.001f); float cosval = round(Dm.cos(Dm.num(angle)), 0.001f);
        return Dm.list(cosval, 0, -sinval, 0, 0, 1, 0, 0, sinval, 0, cosval, 0, 0, 0, 0, 1, 0, 0, 0, 0);
    }

    // tg: code/__HELPERS/matrices.dm:137
    public static object color_matrix_rotate_z(object angle = null)
    {
        float sinval = round(Dm.sin(Dm.num(angle)), 0.001f); float cosval = round(Dm.cos(Dm.num(angle)), 0.001f);
        return Dm.list(cosval, sinval, 0, 0, -sinval, cosval, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0);
    }


    //Returns a matrix addition of A with B
    // tg: code/__HELPERS/matrices.dm:143
    public static object color_matrix_add(DmList A = null, DmList B = null)
    {
        if (!(A is DmList) || !(B is DmList))
            return Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0); // COLOR_MATRIX_IDENTITY (__DEFINES/matrices.dm:5, a list: expanded at each use)
        if (A.len != 20 || B.len != 20)
            return Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0); // COLOR_MATRIX_IDENTITY
        DmList output = Dm.list();
        output.len = 20;
        for (float value = 1, value__end = 20; value <= value__end; value++)
            output[value] = Dm.add(A[value], B[value]);
        return output;
    }

    //Returns a matrix multiplication of A with B
    // tg: code/__HELPERS/matrices.dm:155
    public static object color_matrix_multiply(DmList A = null, DmList B = null)
    {
        if (!(A is DmList) || !(B is DmList))
            return Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0); // COLOR_MATRIX_IDENTITY
        if (A.len != 20 || B.len != 20)
            return Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0); // COLOR_MATRIX_IDENTITY
        DmList output = Dm.list();
        output.len = 20;
        float x = 1;
        float y = 1;
        float offset = 0;
        for (float y__it = 1, y__end = 5; y__it <= y__end; y__it++)
        {
            y = y__it;
            offset = (y - 1) * 4;
            for (float x__it = 1, x__end = 4; x__it <= x__end; x__it++)
            {
                x = x__it;
                output[offset + x] = round(Dm.num(Dm.add(Dm.add(Dm.add(Dm.add(Dm.mul(A[offset + 1], B[x]), Dm.mul(A[offset + 2], B[x + 4])), Dm.mul(A[offset + 3], B[x + 8])), Dm.mul(A[offset + 4], B[x + 12])), (y == 5 ? B[x + 16] : 0))), 0.001f);
            }
        }
        return output;
    }

    /**
     * Converts RGB shorthands into RGBA matrices complete of constants rows (ergo a 20 keys list in byond).
     * if return_identity_on_fail is true, stack_trace is called instead of CRASH, and an identity is returned.
     */
    // tg: code/__HELPERS/matrices.dm:175
    public static object color_to_full_rgba_matrix(object color = null, float? return_identity_on_fail = null)
    {
        return_identity_on_fail ??= TRUE;
        object dot = null;
        if (!Dm.truthy(color))
            return Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0); // COLOR_MATRIX_IDENTITY
        if (istext(color))
        {
            DmList L__1 = rgb2num((string)color) as DmList;
            if (!Dm.truthy(L__1))
            {
                string message = Dm.text("Invalid/unsupported color ([]) argument in color_to_full_rgba_matrix()", color);
                if (Dm.truthy(return_identity_on_fail))
                {
                    stack_trace(message, "code/__HELPERS/matrices.dm", 183);
                    return Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0); // COLOR_MATRIX_IDENTITY
                }
                throw Dm.CRASH(message, "code/__HELPERS/matrices.dm", 185);
            }
            return Dm.list(Dm.div(L__1[1], 255), 0, 0, 0, 0, Dm.div(L__1[2], 255), 0, 0, 0, 0, Dm.div(L__1[3], 255), 0, 0, 0, 0, (L__1.len > 3 ? Dm.div(L__1[4], 255) : 1), 0, 0, 0, 0);
        }
        if (!islist(color)) //invalid format
            throw Dm.CRASH(Dm.text("Invalid/unsupported color ([]) argument in color_to_full_rgba_matrix()", color), "code/__HELPERS/matrices.dm", 188);
        DmList L = color as DmList;
        switch (L.len)
        {
            case >= 3 and <= 5: // row-by-row hexadecimals
                dot = Dm.list();
                for (float a = 1, a__end = L.len; a <= a__end; a++)
                {
                    DmList rgb = rgb2num((string)L[a]) as DmList;
                    foreach (object b in Dm.each(rgb))
                        Dm.append(ref dot, Dm.div(b, 255));
                    if (Dm.truthy(Dm.mod(length(rgb), 4))) // RGB has no alpha instruction
                        Dm.append(ref dot, a != 4 ? 0 : 1);
                }
                if (L.len < 4) //missing both alphas and constants rows
                    Dm.append(ref dot, Dm.list(0, 0, 0, 1, 0, 0, 0, 0));
                else if (L.len < 5) //missing constants row
                    Dm.append(ref dot, Dm.list(0, 0, 0, 0));
                break;
            case >= 9 and <= 12: //RGB
                dot = Dm.list(L[1], L[2], L[3], 0, L[4], L[5], L[6], 0, L[7], L[8], L[9], 0, 0, 0, 0, 1);
                for (float b = 1, b__end = 3; b <= b__end; b++) //missing constants row
                    Dm.append(ref dot, L.len < 9 + b ? 0 : L[9 + b]);
                Dm.append(ref dot, 0);
                break;
            case >= 16 and <= 20: // RGBA
                dot = L.Copy();
                if (L.len < 20) //missing constants row
                {
                    for (float b = 1, b__end = 20 - L.len; b <= b__end; b++)
                        Dm.append(ref dot, 0);
                }
                break;
            default:
            {
                string message = Dm.text("Invalid/unsupported color (list of length []) argument in color_to_full_rgba_matrix()", L.len);
                if (Dm.truthy(return_identity_on_fail))
                {
                    stack_trace(message, "code/__HELPERS/matrices.dm", 216);
                    return Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0); // COLOR_MATRIX_IDENTITY
                }
                throw Dm.CRASH(message, "code/__HELPERS/matrices.dm", 218);
            }
        }
        return dot;
    }
}

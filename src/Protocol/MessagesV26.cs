namespace Anomaly.Protocol;

// Protocol version 26 (D156, docs/ai/specs/protocol.md section 35): BYOND's animate() of a filter (BYOND ref /proc/animate, "Filters"):
// an animation track that names a filter of the atom by its position in the atom's filters list and animates the filter's parameters.
// tg pulses the health doll's wound outline (screen_objects.dm:1023), blurs a mob in high gravity (life.dm:129) and wobbles with waves
// (__HELPERS/filters.dm:369).

/// <summary>The animated parameters of a filter (<see cref="FilterVars.Mask"/>), in wire order.</summary>
[Flags]
public enum FilterVar : ushort
{
    None = 0,
    X = 1,
    Y = 2,
    Size = 4,
    Offset = 8,
    Alpha = 16,
    Radius = 32,
    Falloff = 64,
    Repeat = 128,
    Density = 256,
    Threshold = 512,
    Factor = 1024,
    /// <summary>BYOND <c>color</c> as 0xRRGGBBAA (an outline's, a drop shadow's, rays').</summary>
    Color = 2048,
    /// <summary>BYOND <c>color</c> as a colour matrix of 20 numbers (a colour filter's; tg's <c>color_transition_filter</c>).</summary>
    ColorMatrix = 4096,
    All = (1 << 13) - 1,
}

/// <summary>Values of a filter's animated parameters; <see cref="Mask"/> says which are set. Numbers are BYOND's.</summary>
public struct FilterVars : IEquatable<FilterVars>
{
    public FilterVar Mask;
    public float X, Y, Size, Offset, Alpha, Radius, Falloff, Repeat, Density, Threshold, Factor;
    public uint Rgba;

    /// <summary>The colour matrix (20 numbers) when <see cref="FilterVar.ColorMatrix"/> is set.</summary>
    public float[] Matrix;

    public readonly bool Has(FilterVar v) => (Mask & v) != 0;

    /// <summary>The number a numeric bit names.</summary>
    public readonly float Get(FilterVar v) => v switch
    {
        FilterVar.X => X,
        FilterVar.Y => Y,
        FilterVar.Size => Size,
        FilterVar.Offset => Offset,
        FilterVar.Alpha => Alpha,
        FilterVar.Radius => Radius,
        FilterVar.Falloff => Falloff,
        FilterVar.Repeat => Repeat,
        FilterVar.Density => Density,
        FilterVar.Threshold => Threshold,
        FilterVar.Factor => Factor,
        _ => 0f,
    };

    /// <summary>Sets the number a numeric bit names (and the bit).</summary>
    public void Set(FilterVar v, float value)
    {
        switch (v)
        {
            case FilterVar.X: X = value; break;
            case FilterVar.Y: Y = value; break;
            case FilterVar.Size: Size = value; break;
            case FilterVar.Offset: Offset = value; break;
            case FilterVar.Alpha: Alpha = value; break;
            case FilterVar.Radius: Radius = value; break;
            case FilterVar.Falloff: Falloff = value; break;
            case FilterVar.Repeat: Repeat = value; break;
            case FilterVar.Density: Density = value; break;
            case FilterVar.Threshold: Threshold = value; break;
            case FilterVar.Factor: Factor = value; break;
            default: return;
        }
        Mask |= v;
    }

    /// <summary>The numeric bits in wire order.</summary>
    public static readonly FilterVar[] Numbers =
    {
        FilterVar.X, FilterVar.Y, FilterVar.Size, FilterVar.Offset, FilterVar.Alpha, FilterVar.Radius, FilterVar.Falloff, FilterVar.Repeat,
        FilterVar.Density, FilterVar.Threshold, FilterVar.Factor,
    };

    public readonly bool Equals(FilterVars other)
    {
        if (Mask != other.Mask || Rgba != other.Rgba) return false;
        foreach (FilterVar v in Numbers)
            if (BitConverter.SingleToInt32Bits(Get(v)) != BitConverter.SingleToInt32Bits(other.Get(v))) return false;
        if (Matrix == null || other.Matrix == null) return Matrix == other.Matrix;
        return Matrix.AsSpan().SequenceEqual(other.Matrix);
    }

    public override readonly bool Equals(object obj) => obj is FilterVars other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Mask, Rgba, X, Y, Size, Alpha);

    public static bool operator ==(FilterVars a, FilterVars b) => a.Equals(b);

    public static bool operator !=(FilterVars a, FilterVars b) => !a.Equals(b);
}

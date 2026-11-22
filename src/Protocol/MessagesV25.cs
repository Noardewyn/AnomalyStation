namespace Anomaly.Protocol;

// Version 25 (D155, protocol.md section 34): BYOND's filters. An appearance carries its filters list in order and its render_target, the
// name an alpha, displacement or layering filter's render_source takes a picture by (BYOND ref "filters", "render_target"). tg uses them
// for the gloves of a one-handed human (human_update_icons.dm:199), the split skull of a cranial fissure (cranial_fissure.dm:53) and the
// outlines of the health doll's wounded limbs and of clickable alerts (screen_objects.dm:1023, alert.dm:127). Version 26 (D156,
// protocol.md section 35): every filter type BYOND has, each filter at its position in the list (an animation names it by it), with
// the parameters of every type.

/// <summary>The filter types a client draws (BYOND's filter <c>type</c>).</summary>
public enum FilterKind : byte
{
    /// <summary>BYOND <c>"alpha"</c>: the picture's alpha times a mask's (an icon or a render source), moved by x and y.</summary>
    Alpha = 1,

    /// <summary>BYOND <c>"outline"</c>: a line of <see cref="FilterEntry.Size"/> pixels and <see cref="FilterEntry.Rgba"/> round the opaque pixels.</summary>
    Outline = 2,

    /// <summary>BYOND <c>"displace"</c>: each pixel taken from where a map (an icon or a render source) points, times the size.</summary>
    Displace = 3,

    /// <summary>BYOND <c>"color"</c>: a colour matrix over the picture (<see cref="FilterEntry.ColorMatrix"/>, 20 numbers as an appearance's).</summary>
    Color = 4,

    /// <summary>Version 26: BYOND <c>"angular_blur"</c>: a Gaussian blur along circles round x, y (size in degrees; offset: the radius left sharp).</summary>
    AngularBlur = 5,

    /// <summary>Version 26: BYOND <c>"bloom"</c>: the channels brighter than the threshold colour spread (offset grows, size blurs) and are added, times alpha.</summary>
    Bloom = 6,

    /// <summary>Version 26: BYOND <c>"blur"</c>: a Gaussian blur of size pixels.</summary>
    Blur = 7,

    /// <summary>Version 26: BYOND <c>"drop_shadow"</c>: the picture's shape grown by offset, blurred by size, in the colour, moved by x, y and drawn under it (a negative size: inside it).</summary>
    DropShadow = 8,

    /// <summary>Version 26: BYOND <c>"layer"</c>: a second picture (an icon or a render source) with its colour, transform and blend mode, over or under.</summary>
    Layer = 9,

    /// <summary>Version 26: BYOND <c>"motion_blur"</c>: a Gaussian blur along the vector x, y (its length the size).</summary>
    MotionBlur = 10,

    /// <summary>Version 26: BYOND <c>"radial_blur"</c>: a Gaussian blur outward from x, y, of size per pixel of distance past offset.</summary>
    RadialBlur = 11,

    /// <summary>Version 26: BYOND <c>"rays"</c>: rays from x, y of length size in the colour, chosen by offset ("time"), density, threshold and factor.</summary>
    Rays = 12,

    /// <summary>Version 26: BYOND <c>"ripple"</c>: a ring distortion of size pixels at radius, period repeat, losing strength by falloff.</summary>
    Ripple = 13,

    /// <summary>Version 26: BYOND <c>"wave"</c>: a distortion of size pixels along x, y (the period their length) at phase offset.</summary>
    Wave = 14,

    /// <summary>Version 26: OpenDream's <c>"greyscale"</c> (no BYOND filter; the shim takes it as OpenDream does): the picture's luminance.</summary>
    Greyscale = 15,
}

/// <summary>One filter of an appearance (version 25). Numbers are BYOND's: pixels for x, y and size.</summary>
public sealed class FilterEntry : IEquatable<FilterEntry>
{
    /// <summary>The type.</summary>
    public FilterKind Kind { get; set; }

    /// <summary>BYOND <c>x</c> (alpha, displace).</summary>
    public float X { get; set; }

    /// <summary>BYOND <c>y</c> (alpha, displace).</summary>
    public float Y { get; set; }

    /// <summary>BYOND <c>size</c> (outline: the line's width, default 1; displace: the scale, default 1).</summary>
    public float Size { get; set; } = 1f;

    /// <summary>BYOND <c>color</c> of an outline as RGBA (default black).</summary>
    public uint Rgba { get; set; } = 0x000000FF;

    /// <summary>BYOND <c>color</c> of a colour filter, 20 numbers (4 x 5), or null.</summary>
    public float[] ColorMatrix { get; set; }

    /// <summary>BYOND <c>flags</c> (alpha: <c>MASK_INVERSE</c> 1, <c>MASK_SWAP</c> 2; outline: <c>OUTLINE_SHARP</c> 1, <c>OUTLINE_SQUARE</c> 2).</summary>
    public byte Flags { get; set; }

    /// <summary>BYOND <c>icon</c> of an alpha or displace filter: a resource, or none.</summary>
    public TableRef Icon { get; set; }

    /// <summary>The state of that icon (an <c>icon(file, state)</c> object's), or none (the icon's default state).</summary>
    public TableRef IconState { get; set; }

    /// <summary>BYOND <c>render_source</c> of an alpha, displace or layer filter: a string, the <see cref="AppearanceEntry.RenderTarget"/> of a picture, or none.</summary>
    public TableRef RenderSource { get; set; }

    /// <summary>Version 26: BYOND <c>offset</c> (bloom and drop shadow: the growth before the blur; the blurs round a centre: the radius
    /// left sharp; rays: the time; wave: the phase).</summary>
    public float Offset { get; set; }

    /// <summary>Version 26: BYOND <c>alpha</c> of a bloom (0 to 255, default 255), and tg's <c>alpha</c> of an outline (the health doll's
    /// pulse, D156: the outline's colour alpha times it over 255).</summary>
    public float Alpha { get; set; } = 255f;

    /// <summary>Version 26: BYOND <c>threshold</c> of rays (0 to 1).</summary>
    public float Threshold { get; set; }

    /// <summary>Version 26: BYOND <c>threshold</c> of a bloom, a colour as RGBA (default black).</summary>
    public uint ThresholdRgba { get; set; } = 0x000000FF;

    /// <summary>Version 26: BYOND <c>density</c> of rays.</summary>
    public float Density { get; set; }

    /// <summary>Version 26: BYOND <c>factor</c> of rays.</summary>
    public float Factor { get; set; }

    /// <summary>Version 26: BYOND <c>repeat</c> of a ripple (the period in pixels).</summary>
    public float Repeat { get; set; }

    /// <summary>Version 26: BYOND <c>radius</c> of a ripple.</summary>
    public float Radius { get; set; }

    /// <summary>Version 26: BYOND <c>falloff</c> of a ripple.</summary>
    public float Falloff { get; set; }

    /// <summary>Version 26: BYOND <c>space</c> of a colour filter (<c>FILTER_COLOR_RGB</c> 0, <c>HSV</c> 1, <c>HSL</c> 2, <c>HCY</c> 3).</summary>
    public byte Space { get; set; }

    /// <summary>Version 26: BYOND <c>transform</c> of a layer filter's second picture, six numbers a to f, or null (none).</summary>
    public float[] Transform { get; set; }

    /// <summary>Version 26: BYOND <c>blend_mode</c> of a layer filter's top picture (0 to 5).</summary>
    public byte BlendMode { get; set; }

    public bool Equals(FilterEntry other)
    {
        if (other == null) return false;
        if (Kind != other.Kind || Flags != other.Flags || Rgba != other.Rgba || Icon != other.Icon || IconState != other.IconState || RenderSource != other.RenderSource) return false;
        if (Space != other.Space || BlendMode != other.BlendMode || ThresholdRgba != other.ThresholdRgba) return false;
        if (!Same(X, other.X) || !Same(Y, other.Y) || !Same(Size, other.Size) || !Same(Offset, other.Offset) || !Same(Alpha, other.Alpha)
            || !Same(Threshold, other.Threshold) || !Same(Density, other.Density) || !Same(Factor, other.Factor) || !Same(Repeat, other.Repeat)
            || !Same(Radius, other.Radius) || !Same(Falloff, other.Falloff)) return false;
        return Same(ColorMatrix, other.ColorMatrix) && Same(Transform, other.Transform);
    }

    private static bool Same(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

    private static bool Same(float[] a, float[] b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (!Same(a[i], b[i])) return false;
        return true;
    }

    public override bool Equals(object obj) => Equals(obj as FilterEntry);

    public override int GetHashCode() => HashCode.Combine(Kind, Flags, Rgba, Icon, RenderSource, BitConverter.SingleToInt32Bits(Size), BitConverter.SingleToInt32Bits(X), BitConverter.SingleToInt32Bits(Y));

    /// <summary>A copy (the matrix copied too).</summary>
    public FilterEntry Copy() => new()
    {
        Kind = Kind, X = X, Y = Y, Size = Size, Rgba = Rgba, ColorMatrix = (float[])ColorMatrix?.Clone(), Flags = Flags,
        Icon = Icon, IconState = IconState, RenderSource = RenderSource, Offset = Offset, Alpha = Alpha, Threshold = Threshold,
        ThresholdRgba = ThresholdRgba, Density = Density, Factor = Factor, Repeat = Repeat, Radius = Radius, Falloff = Falloff, Space = Space,
        Transform = (float[])Transform?.Clone(), BlendMode = BlendMode,
    };
}

/// <summary>The flag bits of a filter on the wire (BYOND ref "filters"; the server maps the shim's values to them).</summary>
public static class FilterFlags
{
    /// <summary>Outline: <c>OUTLINE_SHARP</c>, no antialiasing.</summary>
    public const byte OutlineSharp = 1;

    /// <summary>Outline: <c>OUTLINE_SQUARE</c>, square corners.</summary>
    public const byte OutlineSquare = 2;

    /// <summary>Version 26, layer, rays and displace: <c>FILTER_OVERLAY</c>, the result drawn over the picture.</summary>
    public const byte Overlay = 1;

    /// <summary>Version 26, layer and rays: <c>FILTER_UNDERLAY</c>, the result drawn under the picture (rays with neither: alone).</summary>
    public const byte Underlay = 2;

    /// <summary>Version 26, wave and ripple: <c>WAVE_BOUNDED</c>, the distortion kept inside the picture.</summary>
    public const byte WaveBounded = 1;

    /// <summary>Version 26, wave: <c>WAVE_SIDEWAYS</c>, the distortion across the wave.</summary>
    public const byte WaveSideways = 2;

    /// <summary>Alpha: the mask's alpha is inverted.</summary>
    public const byte MaskInverse = 1;

    /// <summary>Alpha: the picture masks the mask instead (the mask's colours are drawn).</summary>
    public const byte MaskSwap = 2;
}

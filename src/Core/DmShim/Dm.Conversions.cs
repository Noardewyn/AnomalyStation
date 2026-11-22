namespace Anomaly.Core.DmShim;

// Owner: values. The shared conversions that the dynamic binder also relies on were written complete by contracts
// (num, num_or_null, truthy, not, eq, isnum, istext, islist and the numeric normalization TryGetNumber); values owns
// everything here. Semantics: docs/ai/translation-rules.md 3.5, 3.6, 4.1, 4.2; conformance rows in
// docs/ai/specs/dm-conformance.md, tests in tests/Anomaly.Core.Tests/Contracts/ConversionTests.cs and
// tests/Anomaly.Core.Tests/Dm/Values/.
public static partial class Dm
{
    /// <summary>
    /// The numeric normalization every shim member uses: a DM number may arrive boxed as <c>float</c> (the port's
    /// number), <c>int</c> (generated <c>Defines</c> constants, C# integer literals), <c>double</c>, or any other C#
    /// numeric type; a boxed C# <c>bool</c> counts as 1 or 0 (rules 3.1: a bool becomes a DM value only as a
    /// number). Text, null, objects, lists and types are not numbers.
    /// </summary>
    internal static bool TryGetNumber(object x, out float value)
    {
        switch (x)
        {
            case float f: value = f; return true;
            case int i: value = i; return true;
            case double d: value = (float)d; return true;
            case bool b: value = b ? 1f : 0f; return true;
            case long l: value = l; return true;
            case short s: value = s; return true;
            case byte by: value = by; return true;
            case sbyte sb: value = sb; return true;
            case ushort us: value = us; return true;
            case uint ui: value = ui; return true;
            case ulong ul: value = ul; return true;
            case decimal m: value = (float)m; return true;
            default: value = 0f; return false;
        }
    }

    /// <summary>
    /// A DM value to a <c>float</c> (rules 3.5): null is 0, a number keeps its value, anything else is DM's
    /// "type mismatch" runtime error, as in DM arithmetic.
    /// </summary>
    public static float num(object x)
    {
        if (x == null) return 0f;
        if (TryGetNumber(x, out float value)) return value;
        throw TypeMismatch(x, "a number");
    }

    /// <summary>A <c>float?</c> to a <c>float</c>: null is 0 (rules 3.6).</summary>
    public static float num(float? x) => x ?? 0f;

    /// <summary>A C# condition as a DM number: 1 or 0 (rules 3.1, 4.1).</summary>
    public static float num(bool b) => b ? 1f : 0f;

    /// <summary>
    /// A DM value to a <c>float?</c> (rules 3.5): null stays null, a number keeps its value, anything else is a
    /// "type mismatch" runtime error.
    /// </summary>
    public static float? num_or_null(object x)
    {
        if (x == null) return null;
        if (TryGetNumber(x, out float value)) return value;
        throw TypeMismatch(x, "a number");
    }

    /// <summary>DM truthiness of a number: non-zero (NaN is true, as <c>NaN != 0</c>). Rules 4.1.</summary>
    public static bool truthy(float x) => x != 0f;

    /// <summary>DM truthiness of a nullable number: non-null and non-zero.</summary>
    public static bool truthy(float? x) => x.HasValue && x.GetValueOrDefault() != 0f;

    /// <summary>DM truthiness of text: non-null and non-empty.</summary>
    public static bool truthy(string x) => !string.IsNullOrEmpty(x);

    /// <summary>DM truthiness of a list: non-null (an empty list is true).</summary>
    public static bool truthy(DmList x) => x != null;

    /// <summary>DM truthiness of a typepath: non-null.</summary>
    public static bool truthy(Type x) => x != null;

    /// <summary>
    /// DM truthiness of any value (BYOND ref <c>/operator/!</c>: "The number 0, an empty string, or null are all
    /// false values. Everything else is true."), so an empty list and any object are true.
    /// </summary>
    public static bool truthy(object x)
    {
        if (x == null) return false;
        if (x is string s) return s.Length != 0;
        if (TryGetNumber(x, out float value)) return value != 0f;
        return true;
    }

    /// <summary>DM <c>!x</c> in a value context: 1 when <paramref name="x"/> is false, else 0 (rules 4.1).</summary>
    public static float not(float x) => x != 0f ? 0f : 1f;

    /// <summary>DM <c>!x</c> of a nullable number, as a DM number.</summary>
    public static float not(float? x) => truthy(x) ? 0f : 1f;

    /// <summary>DM <c>!x</c> of text, as a DM number.</summary>
    public static float not(string x) => truthy(x) ? 0f : 1f;

    /// <summary>DM <c>!x</c> of any value, as a DM number.</summary>
    public static float not(object x) => truthy(x) ? 0f : 1f;

    /// <summary>
    /// DM <c>a == b</c> (rules 4.2): numbers by value whatever their box (<c>float</c>, <c>int</c>, ...; IEEE, so
    /// NaN equals nothing), text by ordinal comparison, null equals only null, everything else by reference.
    /// Values of different kinds are never equal and never raise: <c>null == 0</c>, <c>null == ""</c> and
    /// <c>"1" == 1</c> are false.
    /// </summary>
    public static bool eq(object a, object b)
    {
        if (a == null || b == null) return a == null && b == null;
        if (a is string sa) return b is string sb && string.Equals(sa, sb, StringComparison.Ordinal);
        if (a is DmResource ra) return ra.Equals(b); // one cache entry per file: equal paths are the same resource
        if (TryGetNumber(a, out float fa)) return TryGetNumber(b, out float fb) && fa == fb;
        return ReferenceEquals(a, b);
    }

    /// <summary>DM <c>a == b</c> for two numbers.</summary>
    public static bool eq(float a, float b) => a == b;

    /// <summary>DM <c>a == b</c> with a number on the right (no boxing): true only for a number of equal value.</summary>
    public static bool eq(object a, float b) => TryGetNumber(a, out float fa) && fa == b;

    /// <summary>DM <c>a == b</c> with a number on the left (no boxing).</summary>
    public static bool eq(float a, object b) => TryGetNumber(b, out float fb) && a == fb;

    /// <summary>DM <c>a == b</c> for nullable numbers: both null, or both numbers of equal value.</summary>
    public static bool eq(float? a, float? b) => a.HasValue ? b.HasValue && a.GetValueOrDefault() == b.GetValueOrDefault() : !b.HasValue;

    /// <summary>DM <c>a == b</c> for a nullable number and a number (null is not 0).</summary>
    public static bool eq(float? a, float b) => a.HasValue && a.GetValueOrDefault() == b;

    /// <summary>DM <c>a == b</c> for a number and a nullable number (null is not 0).</summary>
    public static bool eq(float a, float? b) => b.HasValue && a == b.GetValueOrDefault();

    /// <summary>DM <c>isnum(Val)</c> (BYOND ref <c>/proc/isnum</c>): whether the value is a number.</summary>
    public static bool isnum(object Val) => TryGetNumber(Val, out _);

    /// <summary>DM <c>istext(Val)</c> (BYOND ref <c>/proc/istext</c>): whether the value is text.</summary>
    public static bool istext(object Val) => Val is string;

    /// <summary>DM <c>islist(Object)</c> (BYOND ref <c>/proc/islist</c>): whether the value is a list.</summary>
    public static bool islist(object Object) => Object is DmList;

    /// <summary>
    /// DM <c>isfile(File)</c> (BYOND ref <c>/proc/isfile</c>): whether the value is a file (a resource such as
    /// <c>'icons/x.dmi'</c> or a <c>file()</c> object; OpenDream <c>NativeProc_isfile</c>: a resource value). A resource
    /// literal (<see cref="DmResource"/>) and a <c>file()</c> reference (<see cref="DmFileRef"/>, the W3.0 file seam) are
    /// files; text, numbers, null, lists and other objects give false, as in DM.
    /// </summary>
    public static bool isfile(object File) => File is DmFileRef or DmResource;

    /// <summary>
    /// DM <c>isicon(Icon)</c> (BYOND ref <c>/proc/isicon</c>): whether the value is an icon (an <c>/icon</c> object or
    /// an image resource; OpenDream <c>NativeProc_isicon</c>: a <c>DreamObjectIcon</c> or an <c>IconResource</c>): an
    /// <see cref="DmShim.icon"/> object (M6 slice 7), or a resource of an image file (<c>.dmi</c>, <c>.png</c>, <c>.jpg</c>,
    /// <c>.jpeg</c>, <c>.gif</c>, <c>.bmp</c>).
    /// </summary>
    public static bool isicon(object Icon) => Icon is DmResource { IsIcon: true } or DmShim.icon;

    // DM's "type mismatch" runtime error for a value used as another kind.
    internal static DmRuntimeException TypeMismatch(object value, string expected)
        => new DmRuntimeException("type mismatch: expected " + expected + ", got " + KindOf(value));

    // A short description of a DM value's kind for error messages.
    internal static string KindOf(object value) => value switch
    {
        null => "null",
        string => "text",
        DmList => "a list",
        DmResource r => "the resource '" + r.Path + "'",
        Type t => "the typepath " + t.Name,
        _ when TryGetNumber(value, out _) => "a number",
        _ => "an object of type " + value.GetType().Name,
    };
}

namespace Anomaly.Core.DmShim;

// Owner: values. Numeric builtins (docs/ai/translation-rules.md 4.6): float parameters and a float result, never with
// Math.Round or integer casts; min, max and clamp also have an object overload with DM's full rules. Rounding and
// lerp are computed in float (each step narrowed with an explicit cast), except round(A, B), whose quotient is
// computed in double (rules 4.6). The transcendental functions (trigonometry in
// degrees, log) use OpenDream's formulas (DMCompiler/SharedOperations.cs: sin(x / 180 * pi) and so on) evaluated in
// double and rounded to float once: the float versions (MathF) differ in the last bit between platforms (MathF.Asin(1)
// is not pi/2 on every libm), while the double result rounds to the float that BYOND's examples show
// (arcsin(1) == 90, arctan(1) == 45).
public static partial class Dm
{
    /// <summary>DM <c>round(A)</c> (BYOND ref <c>/proc/round</c>): floor, the deprecated one-argument form (<c>round(-1.45) == -2</c>).</summary>
    public static float round(float A) => MathF.Floor(A);

    /// <summary>
    /// DM <c>round(A, B)</c>: the nearest multiple of B ("rounds A to the nearest multiple of B", BYOND ref), ties
    /// rounded up: <c>floor(A / B + 0.5) * B</c> (tg __DEFINES/atmospherics/atmos_core.dm:41-42; OpenDream's
    /// ties-to-even is not followed). <c>round(0.5, 1) == 1</c>, <c>round(2.5, 1) == 3</c>, <c>round(-0.5, 1) == 0</c>,
    /// <c>round(1.45, 1.5) == 1.5</c> (BYOND ref example). The quotient plus 0.5 is computed in double and the result
    /// rounded to float once: in float, from 2^23 up there are no halves, so <c>8388609 / 1 + 0.5</c> became 8388610 and
    /// a value that is already a multiple of B moved to the next one.
    /// </summary>
    public static float round(float A, float B) => (float)(Math.Floor((double)A / B + 0.5) * B);

    /// <summary>DM <c>floor(A)</c> (BYOND ref <c>/proc/floor</c>).</summary>
    public static float floor(float A) => MathF.Floor(A);

    /// <summary>DM <c>ceil(A)</c> (BYOND ref <c>/proc/ceil</c>).</summary>
    public static float ceil(float A) => MathF.Ceiling(A);

    /// <summary>DM <c>trunc(A)</c> (BYOND ref <c>/proc/trunc</c>): toward zero.</summary>
    public static float trunc(float A) => MathF.Truncate(A);

    /// <summary>
    /// DM <c>fract(A)</c> (BYOND ref <c>/proc/fract</c>): <c>A - trunc(A)</c>, with A's sign (<c>fract(-1.5) == -0.5</c>);
    /// an infinity gives 0 and NaN stays NaN (OpenDream test <c>fract</c>).
    /// </summary>
    public static float fract(float A) => float.IsInfinity(A) ? 0f : (float)(A - MathF.Truncate(A));

    /// <summary>DM <c>sign(A)</c> (BYOND ref <c>/proc/sign</c>): 1, -1 or 0 (OpenDream <c>NativeProc_sign</c>: NaN gives 1).</summary>
    public static float sign(float A) => A == 0f ? 0f : A < 0f ? -1f : 1f;

    /// <summary>DM <c>abs(A)</c> (BYOND ref <c>/proc/abs</c>).</summary>
    public static float abs(float A) => MathF.Abs(A);

    /// <summary>DM <c>sqrt(A)</c> (BYOND ref <c>/proc/sqrt</c>; a negative A gives NaN, as OpenDream's <c>MathF.Sqrt</c>).</summary>
    public static float sqrt(float A) => MathF.Sqrt(A);

    /// <summary>DM <c>isinf(A)</c> (BYOND ref <c>/proc/isinf</c>, 515): 1 for a positive or negative infinite number, else 0
    /// (a non-number gives 0).</summary>
    public static float isinf(object A) => A is float f && float.IsInfinity(f) ? 1f : 0f;

    /// <summary>DM <c>isnan(A)</c> (BYOND ref <c>/proc/isnan</c>, 515): 1 for a NaN number, else 0 (a non-number gives
    /// 0).</summary>
    public static float isnan(object A) => A is float f && float.IsNaN(f) ? 1f : 0f;

    /// <summary>
    /// DM <c>min(A, B, C, ...)</c> with only numeric arguments (rules 4.6): the smallest; of equal values the last one
    /// (OpenDream <c>MinComparison</c>, <c>&lt;=</c>). No arguments give 0 (DM gives null; the numeric overload cannot).
    /// </summary>
    public static float min(params float[] A)
    {
        if (A == null || A.Length == 0) return 0f;
        float m = A[0];
        for (int i = 1; i < A.Length; i++)
        {
            if (A[i] <= m) m = A[i];
        }
        return m;
    }

    /// <summary>
    /// DM <c>min(A, B, C, ...)</c> with DM's full rules (BYOND ref <c>/proc/min</c>): numbers, text (compared as text)
    /// or null, "but different types may not be mixed (null values can be mixed with nums or text)"; null counts as 0
    /// against numbers and of equal values the later one wins (<c>min(1, null)</c> and <c>min(0, null)</c> are null,
    /// <c>min(null, 0) == 0</c>, OpenDream test <c>minmax</c>). A single argument that is a list (or
    /// <c>Dm.arglist(L)</c>) supplies the values; a single other argument is returned; no values give null.
    /// </summary>
    public static object min(params object[] A) => Extreme(A, max: false);

    /// <summary>DM <c>max(A, B, C, ...)</c> with only numeric arguments (rules 4.6): the largest; of equal values the first one.</summary>
    public static float max(params float[] A)
    {
        if (A == null || A.Length == 0) return 0f;
        float m = A[0];
        for (int i = 1; i < A.Length; i++)
        {
            if (A[i] > m) m = A[i];
        }
        return m;
    }

    /// <summary>
    /// DM <c>max(A, B, C, ...)</c> with DM's full rules (BYOND ref <c>/proc/max</c>; OpenDream <c>MaxComparison</c> and
    /// tests <c>Max</c>, <c>minmax</c>: <c>max(0, null)</c> and <c>max(-3, null)</c> are null, <c>max(null, 0) == 0</c>,
    /// <c>max("", null) == ""</c>).
    /// </summary>
    public static object max(params object[] A) => Extreme(A, max: true);

    /// <summary>
    /// DM <c>clamp(Value, Low, High)</c> with numbers (BYOND ref <c>/proc/clamp</c>): Low and High may come in either
    /// order ("If the Low and High values are out of order, it doesn't matter").
    /// </summary>
    public static float clamp(float Value, float Low, float High)
    {
        if (Low > High) (Low, High) = (High, Low);
        if (Value < Low) return Low;
        if (Value > High) return High;
        return Value;
    }

    /// <summary>
    /// DM <c>clamp(Value, Low, High)</c> and <c>clamp(List, Low, High)</c> with DM's full rules: a number (null as 0) is
    /// clamped; a list has its numbers (and nulls, as 0) clamped in place and is returned ("The original List, whose
    /// contents have all been clamped... the original list will be modified"); Low and High must be numbers. The items
    /// are changed by position, which an alist does not have (a number there is a key, and its positional changes are
    /// runtime errors, as OpenDream's alist tests pin them), so clamping an alist is a runtime error.
    /// </summary>
    public static object clamp(object Value, object Low, object High)
    {
        if (!TryGetNumber(Low, out float low)) throw new DmRuntimeException("clamp(): the lower bound is not a number");
        if (!TryGetNumber(High, out float high)) throw new DmRuntimeException("clamp(): the upper bound is not a number");
        if (Value is DmList list)
        {
            if (list.IsAlist) throw new DmRuntimeException("clamp(): an alist cannot be clamped in place");
            for (int i = 1; i <= list.Count; i++)
            {
                object item = list[i];
                if (item == null || TryGetNumber(item, out _)) list[i] = clamp(num(item), low, high);
            }
            return list;
        }
        if (Value == null || TryGetNumber(Value, out _)) return clamp(num(Value), low, high);
        throw new DmRuntimeException("clamp() expects a number or a list, got " + KindOf(Value));
    }

    /// <summary>DM <c>log(Y)</c> (BYOND ref <c>/proc/log</c>): the natural logarithm; Y &lt;= 0 is a runtime error (OpenDream tests <c>log_zero</c>, <c>log_negative</c>).</summary>
    public static float log(float Y)
    {
        if (!(Y > 0f)) throw new DmRuntimeException("log(" + num2text(Y) + ") is not computable");
        return (float)Math.Log(Y);
    }

    /// <summary>
    /// DM <c>log(X, Y)</c>: the logarithm of Y in base X ("X ** log(X,Y) == Y"); a base that is not positive or is 1, or
    /// Y &lt;= 0, is a runtime error (OpenDream tests <c>log_one_base</c>, <c>log_zero_base</c>). Exact when Y is an
    /// integral power of X (<c>log(2, 8) == 3</c>, <c>log(10, 0.001) == -3</c>): tg turns <c>log(2, runlevel) + 1</c>
    /// into a list index (<c>controllers/master.dm:558</c>), where a quotient of two rounded logarithms one ulp off
    /// would truncate to the wrong index (W3.0, S4).
    /// </summary>
    public static float log(float X, float Y)
    {
        if (!(X > 0f) || X == 1f || !(Y > 0f)) throw new DmRuntimeException("log(" + num2text(X) + ", " + num2text(Y) + ") is not computable");
        double quotient = Math.Log(Y) / Math.Log(X);
        double power = Math.Round(quotient);
        // Y is exactly X ** n for the nearest integer n: the exact answer n (X ** n computed in double, compared with
        // the float Y, so only true integral powers qualify).
        if (Math.Abs(quotient - power) < 1e-6 && Math.Abs(power) <= 1024 && (float)Math.Pow(X, power) == Y) return (float)power;
        return (float)quotient;
    }

    /// <summary>DM <c>lerp(A, B, factor)</c> (BYOND ref <c>/proc/lerp</c>): <c>A + (B - A) * factor</c> (<c>lerp(1, 5, 0.25) == 2</c>).</summary>
    public static float lerp(float A, float B, float factor) => (float)(A + (float)((float)(B - A) * factor));

    /// <summary>DM <c>sin(X)</c>, X in degrees (BYOND ref <c>/proc/sin</c>; <c>sin(90) == 1</c>).</summary>
    public static float sin(float X) => (float)Math.Sin(X / 180.0 * Math.PI);

    /// <summary>DM <c>cos(X)</c>, X in degrees.</summary>
    public static float cos(float X) => (float)Math.Cos(X / 180.0 * Math.PI);

    /// <summary>DM <c>tan(X)</c>, X in degrees.</summary>
    public static float tan(float X) => (float)Math.Tan(X / 180.0 * Math.PI);

    /// <summary>DM <c>arcsin(X)</c>, the result in degrees (<c>arcsin(1) == 90</c>).</summary>
    public static float arcsin(float X) => (float)(Math.Asin(X) / Math.PI * 180.0);

    /// <summary>DM <c>arccos(X)</c>, the result in degrees (<c>arccos(0) == 90</c>).</summary>
    public static float arccos(float X) => (float)(Math.Acos(X) / Math.PI * 180.0);

    /// <summary>DM <c>arctan(Num)</c>, the result in degrees from -90 to 90 (BYOND ref <c>/proc/arctan</c>; <c>arctan(1) == 45</c>).</summary>
    public static float arctan(float Num) => (float)(Math.Atan(Num) / Math.PI * 180.0);

    /// <summary>
    /// DM <c>arctan(x, y)</c>: the polar angle of the point (x, y) in degrees from -180 to 180, 0 due east and growing
    /// counter-clockwise (<c>arctan(-1, 1) == 135</c>, <c>arctan(0, -5) == -90</c>, <c>arctan(0, 0) == 0</c>).
    /// </summary>
    public static float arctan(float x, float y) => (float)(Math.Atan2(y, x) / Math.PI * 180.0);

    // min/max over DM values (OpenDream MinComparison / MaxComparison).
    private static object Extreme(object[] args, bool max)
    {
        args ??= OneNull();
        IReadOnlyList<object> values;
        if (args.Length == 1)
        {
            object only = args[0] is DmArgList arglist ? arglist.List : args[0];
            if (only is not DmList list) return args[0] is DmArgList ? throw new DmRuntimeException("arglist() needs a list") : only;
            values = list.CopyItems();
        }
        else
        {
            values = args;
        }
        if (values.Count == 0) return null;
        object best = values[0];
        for (int i = 1; i < values.Count; i++) best = max ? MaxStep(best, values[i]) : MinStep(best, values[i]);
        return best;
    }

    private static object MinStep(object current, object value)
    {
        if (TryGetNumber(value, out float v))
        {
            if (current == null) return v <= 0f ? v : current;
            if (TryGetNumber(current, out float c)) return v <= c ? v : current;
            return current;
        }
        if (value == null) return TryGetNumber(current, out float n) && n >= 0f ? null : current;
        if (value is string s)
        {
            if (current == null) return s;
            if (current is string cs) return string.CompareOrdinal(s, cs) <= 0 ? s : current;
            return current;
        }
        throw new DmRuntimeException("min(): cannot compare " + KindOf(current) + " and " + KindOf(value));
    }

    private static object MaxStep(object current, object value)
    {
        if (TryGetNumber(value, out float v))
        {
            if (current == null) return v >= 0f ? v : current;
            if (TryGetNumber(current, out float c)) return v > c ? v : current;
            return current;
        }
        if (value == null) return TryGetNumber(current, out float n) && n <= 0f ? null : current;
        if (value is string s)
        {
            if (current == null) return s;
            if (current is string cs) return string.CompareOrdinal(s, cs) > 0 ? s : current;
            return current;
        }
        throw new DmRuntimeException("max(): cannot compare " + KindOf(current) + " and " + KindOf(value));
    }
}

namespace Anomaly.Core.DmShim;

// Owner: values. DM operators that are shim calls: comparisons of mixed kinds, arithmetic with object operands,
// 24-bit bit operations, list operators and the in-place compound assignments (docs/ai/translation-rules.md sections
// 4.2-4.4, 4.10, 6.3). The value tables follow the BYOND reference ("operators", "list operators", "matrix
// operators") and, where it is silent, OpenDream's DMOpcodeHandlers and its operator tests
// (Content.Tests/DMProject/Tests/Operators, which tabulate every operator over null, numbers, text, lists, datums
// and typepaths); the conformance rows are in docs/ai/specs/dm-conformance.md.
public static partial class Dm
{
    // ---- comparison ----

    /// <summary>
    /// DM <c>a ~= b</c> (BYOND ref <c>~=</c>; rules 4.10): lists are equivalent when their items are equal in order
    /// and (BYOND 516+) their associated values are equal, shallowly; matrices compare components; other values
    /// fall back to <see cref="eq(object, object)"/>.
    /// </summary>
    public static bool equiv(object A, object B)
    {
        if (A is DmList la) return B is DmList lb && la.EquivalentTo(lb);
        if (A is matrix ma) return B is matrix mb && ma.a == mb.a && ma.b == mb.b && ma.c == mb.c && ma.d == mb.d && ma.e == mb.e && ma.f == mb.f;
        return eq(A, B);
    }

    /// <summary>
    /// DM <c>a &lt; b</c> with an <c>object</c>, text or reference operand (rules 4.2): numbers by value, null as 0
    /// against a number and as "" against text (<c>null &lt; "abc"</c> is true, <c>null &lt; ""</c> false), text by
    /// ordinal order. When B is a reference (an object, list or typepath) the result is A itself, so this is
    /// <c>truthy(A)</c> (OpenDream <c>TryGetReferenceComparisonResult</c> and its BYOND-checked test
    /// <c>comparison2</c>); other mixed kinds (a number and text, a reference and a number) are a runtime error.
    /// </summary>
    public static bool lt(object A, object B) => IsReference(B) ? truthy(A) : LessThan(A, B);

    /// <summary>DM <c>a &lt;= b</c> with an <c>object</c>, text or reference operand (rules 4.2): as <see cref="lt"/>, and <c>0 &lt;= null</c>, <c>null &lt;= 0</c>, <c>null &lt;= ""</c> are true.</summary>
    public static bool le(object A, object B)
    {
        if (IsReference(B)) return truthy(A);
        if (NullAndZeroOrEmpty(A, B)) return true;
        return eq(A, B) || LessThan(A, B);
    }

    /// <summary>DM <c>a &gt; b</c> with an <c>object</c>, text or reference operand (rules 4.2; see <see cref="lt"/>).</summary>
    public static bool gt(object A, object B) => IsReference(B) ? truthy(A) : GreaterThan(A, B);

    /// <summary>DM <c>a &gt;= b</c> with an <c>object</c>, text or reference operand (rules 4.2): as <see cref="gt"/>, and <c>0 &gt;= null</c>, <c>null &gt;= 0</c>, <c>null &gt;= ""</c> are true.</summary>
    public static bool ge(object A, object B)
    {
        if (IsReference(B)) return truthy(A);
        if (NullAndZeroOrEmpty(A, B)) return true;
        return eq(A, B) || GreaterThan(A, B);
    }

    /// <summary>
    /// DM <c>a &lt;=&gt; b</c> (BYOND ref <c>&lt;=&gt;</c>): -1 if A is less than B, 1 if greater, 0 otherwise (also for
    /// NaN). Numbers by value (null as 0), text case-sensitively (null as ""); other combinations are a runtime error.
    /// </summary>
    public static float cmp(object A, object B)
    {
        if ((A == null || TryGetNumber(A, out _)) && (B == null || TryGetNumber(B, out _)))
        {
            float a = num(A), b = num(B);
            return a < b ? -1f : a > b ? 1f : 0f;
        }
        if ((A == null || A is string) && (B == null || B is string))
        {
            int c = string.CompareOrdinal((string)A ?? "", (string)B ?? "");
            return c < 0 ? -1f : c > 0 ? 1f : 0f;
        }
        throw new DmRuntimeException("invalid <=> comparison of " + KindOf(A) + " and " + KindOf(B));
    }

    // ---- arithmetic (BYOND ref /operator; rules 4.3) ----

    /// <summary>
    /// DM <c>A + B</c> with an <c>object</c> operand (BYOND ref <c>+</c>): numbers add (null counts as 0), text
    /// concatenates, <c>null + B</c> is B and <c>A + null</c> is A, a list left operand gives a new list with B's items
    /// (and associated values) added, matrices add componentwise, a datum's <c>operator+</c> (<c>operator__add</c>,
    /// rules 7.10) is called. Text with a non-text operand, typepaths and other objects are a type-mismatch error.
    /// </summary>
    public static object add(object A, object B)
    {
        if (A is DmList list)
        {
            DmList result = list.CopyAll();
            result.AppendOp(B);
            return result;
        }
        if (A is matrix m)
        {
            if (B is matrix mb) return new matrix(m.a + mb.a, m.b + mb.b, m.c + mb.c, m.d + mb.d, m.e + mb.e, m.f + mb.f);
            throw BadOperands("+", A, B);
        }
        if (A == null) return B;
        if (TryGetNumber(A, out float a))
        {
            if (B == null) return a;
            if (TryGetNumber(B, out float b)) return a + b;
            throw BadOperands("+", A, B);
        }
        if (A is string sa)
        {
            if (B == null) return sa;
            if (B is string sb) return sa + sb;
            throw BadOperands("+", A, B);
        }
        if (TryCallOperator(A, "operator__add", B, out object overloaded)) return overloaded;
        throw BadOperands("+", A, B);
    }

    /// <summary>
    /// DM <c>A - B</c> with an <c>object</c> operand (BYOND ref <c>-</c>): numbers (null as 0 on either side), a new
    /// list without the last occurrence of B or of each of B's items, matrices componentwise, a datum's
    /// <c>operator-</c> (<c>operator__sub</c>). Text and other mixes are a type-mismatch error.
    /// </summary>
    public static object sub(object A, object B)
    {
        if ((A == null || TryGetNumber(A, out _)) && (B == null || TryGetNumber(B, out _))) return num(A) - num(B);
        if (A is DmList list)
        {
            DmList result = list.CopyAll();
            result.RemoveOp(B);
            return result;
        }
        if (A is matrix m && B is matrix mb) return new matrix(m.a - mb.a, m.b - mb.b, m.c - mb.c, m.d - mb.d, m.e - mb.e, m.f - mb.f);
        if (TryCallOperator(A, "operator__sub", B, out object overloaded)) return overloaded;
        throw BadOperands("-", A, B);
    }

    /// <summary>
    /// DM <c>A * B</c> with an <c>object</c> operand (BYOND ref <c>*</c>): a number or null A times B, where any B that
    /// is not a number counts as 0 (OpenDream <c>Multiply</c>: "Non-numbers are always treated as 0 here", and its
    /// BYOND-checked table <c>Multiplication</c>: <c>10 * "ABC" == 0</c>); a matrix times a matrix (src, then B) or a
    /// number (scaling); a datum's <c>operator*</c> (<c>operator__mul</c>). Text and other objects are a type-mismatch
    /// error.
    /// </summary>
    public static object mul(object A, object B)
    {
        if (A == null || TryGetNumber(A, out _)) return num(A) * NumberOrZero(B);
        if (A is matrix m)
        {
            if (B is matrix mb) return m.Copy().Multiply(mb);
            if (TryGetNumber(B, out float n)) return m.Copy().Scale(n, n);
            throw BadOperands("*", A, B);
        }
        if (TryCallOperator(A, "operator__mul", B, out object overloaded)) return overloaded;
        throw BadOperands("*", A, B);
    }

    /// <summary>DM <c>A / B</c> with a divisor that is not a nonzero constant: real division; a zero divisor is DM's "Division by zero" runtime error.</summary>
    public static float div(float A, float B)
    {
        if (B == 0f) throw new DmRuntimeException("Division by zero");
        return A / B;
    }

    /// <summary>
    /// DM <c>A / B</c> with an <c>object</c> operand (BYOND ref <c>/</c>): <c>null / B</c> is 0 for any B (OpenDream
    /// <c>Divide</c> and its test <c>valid_and_null</c>: <c>null / null == 0</c>); a number divided by a number
    /// ("Division by zero" error for 0); a number divided by null or anything else is an error; a matrix divided by a
    /// number scales, by a matrix multiplies by its inverse; a datum's <c>operator/</c> (<c>operator__div</c>).
    /// </summary>
    public static object div(object A, object B)
    {
        if (A == null) return 0f;
        if (TryGetNumber(A, out float a))
        {
            if (TryGetNumber(B, out float b)) return div(a, b);
            throw BadOperands("/", A, B);
        }
        if (A is matrix m)
        {
            if (TryGetNumber(B, out float n))
            {
                if (n == 0f) throw new DmRuntimeException("Division by zero");
                return new matrix(m.a / n, m.b / n, m.c / n, m.d / n, m.e / n, m.f / n);
            }
            if (B is matrix mb)
            {
                matrix inverse = mb.Copy();
                if (!inverse.TryInvert()) throw new DmRuntimeException("the divisor matrix cannot be inverted");
                return m.Copy().Multiply(inverse);
            }
            throw BadOperands("/", A, B);
        }
        if (TryCallOperator(A, "operator__div", B, out object overloaded)) return overloaded;
        throw BadOperands("/", A, B);
    }

    /// <summary>
    /// DM unary <c>-A</c> with an <c>object</c> operand: a number negated; any other value (null, text, a list, an
    /// object) gives 0 (OpenDream <c>Negate</c> and its BYOND-checked table <c>Negation</c>).
    /// </summary>
    public static object neg(object A) => TryGetNumber(A, out float a) ? -a : 0f;

    /// <summary>
    /// DM <c>A % B</c> (BYOND ref <c>%</c>): both operands truncated to integers first ("for legacy reasons"), the
    /// result has the dividend's sign (<c>-7 % 3 == -1</c>, <c>30.8 % 30 == 0</c>, <c>5 % 2.5 == 1</c>); a divisor that
    /// truncates to 0 is a "Division by zero" runtime error. The remainder of two integers has no negative zero
    /// (<c>"[-3 % 3]" == "0"</c>; OpenDream <c>ModulusValues</c> computes it in int), where the IEEE remainder would be -0.
    /// </summary>
    public static float mod(float A, float B)
    {
        float a = MathF.Truncate(A);
        float b = MathF.Truncate(B);
        if (b == 0f) throw new DmRuntimeException("Division by zero (modulo)");
        float r = a % b;
        return r == 0f ? 0f : r;
    }

    /// <summary>
    /// DM <c>A % B</c> with <c>object</c> operands: a null dividend is 0 (OpenDream test <c>Modulo</c>:
    /// <c>null % 10 == 0</c>); a divisor that is not a number (null included) and a dividend that is neither a
    /// number nor null are runtime errors.
    /// </summary>
    public static float mod(object A, object B)
    {
        if ((A == null || TryGetNumber(A, out _)) && TryGetNumber(B, out float b)) return mod(num(A), b);
        throw BadOperands("%", A, B);
    }

    /// <summary>
    /// DM <c>A %% B</c> (BYOND ref <c>%%</c>): "equivalent to B * fract(A / B)", computed in float as OpenDream
    /// does; the result has the dividend's sign (<c>-180 %% 360 == -180</c>, <c>-1 %% 3 == -1</c>,
    /// <c>540 %% 360 == 180</c>). A zero divisor gives NaN (no runtime error), as that formula does.
    /// </summary>
    public static float fmod(float A, float B)
    {
        float fraction = (float)(A / B);
        fraction = (float)(fraction - MathF.Truncate(fraction));
        return (float)(fraction * B);
    }

    /// <summary>DM <c>A %% B</c> with <c>object</c> operands (null as 0; other non-numbers are a type mismatch).</summary>
    public static float fmod(object A, object B) => fmod(num(A), num(B));

    /// <summary>DM <c>A ** B</c> (BYOND ref <c>**</c>; <c>2 ** 3 == 8</c>, <c>0 ** 0 == 1</c>), evaluated in double and rounded to float once (as the transcendental builtins, Dm.Math.cs).</summary>
    public static float pow(float A, float B) => (float)Math.Pow(A, B);

    /// <summary>
    /// DM <c>A ** B</c> with <c>object</c> operands: A must be a number or null (as 0); any B that is not a number
    /// counts as 0 (OpenDream <c>Power</c> and its table <c>Power</c>: <c>10 ** "ABC" == 1</c>, <c>null ** 2 == 0</c>).
    /// </summary>
    public static float pow(object A, object B)
    {
        if (A != null && !TryGetNumber(A, out _)) throw BadOperands("**", A, B);
        return pow(num(A), NumberOrZero(B));
    }

    /// <summary>
    /// DM <c>x++</c> / <c>++x</c> as a statement on an <c>object</c> target: "BYOND coerces every non-number including
    /// null to 0 for ++ and --" (OpenDream <c>IncrementDecrement</c>; tests <c>pre_increment</c>: <c>++"bad" == 1</c>).
    /// </summary>
    public static void inc(ref object x) => x = NumberOrZero(x) + 1f;

    /// <summary>DM <c>x++</c> as a statement on a <c>float?</c> target (null counts as 0).</summary>
    public static void inc(ref float? x) => x = (x ?? 0f) + 1f;

    /// <summary>DM <c>x--</c> / <c>--x</c> as a statement on an <c>object</c> target (non-numbers count as 0).</summary>
    public static void dec(ref object x) => x = NumberOrZero(x) - 1f;

    /// <summary>DM <c>x--</c> as a statement on a <c>float?</c> target.</summary>
    public static void dec(ref float? x) => x = (x ?? 0f) - 1f;

    /// <summary>
    /// DM <c>x++</c> in a value context: increments and returns the old value as a number (a non-number old value reads
    /// 0 here; DM returns the old value itself, so <c>null++</c> is null in DM and 0 in the port).
    /// </summary>
    public static float postinc(ref object x)
    {
        float old = NumberOrZero(x);
        x = old + 1f;
        return old;
    }

    /// <summary>DM <c>x++</c> in a value context on a <c>float?</c> target.</summary>
    public static float postinc(ref float? x)
    {
        float old = x ?? 0f;
        x = old + 1f;
        return old;
    }

    /// <summary>DM <c>++x</c> in a value context: increments and returns the new value.</summary>
    public static float preinc(ref object x)
    {
        float now = NumberOrZero(x) + 1f;
        x = now;
        return now;
    }

    /// <summary>DM <c>++x</c> in a value context on a <c>float?</c> target.</summary>
    public static float preinc(ref float? x)
    {
        float now = (x ?? 0f) + 1f;
        x = now;
        return now;
    }

    /// <summary>DM <c>x--</c> in a value context: decrements and returns the old value (see <see cref="postinc(ref object)"/>).</summary>
    public static float postdec(ref object x)
    {
        float old = NumberOrZero(x);
        x = old - 1f;
        return old;
    }

    /// <summary>DM <c>x--</c> in a value context on a <c>float?</c> target.</summary>
    public static float postdec(ref float? x)
    {
        float old = x ?? 0f;
        x = old - 1f;
        return old;
    }

    /// <summary>DM <c>--x</c> in a value context: decrements and returns the new value.</summary>
    public static float predec(ref object x)
    {
        float now = NumberOrZero(x) - 1f;
        x = now;
        return now;
    }

    /// <summary>DM <c>--x</c> in a value context on a <c>float?</c> target.</summary>
    public static float predec(ref float? x)
    {
        float now = (x ?? 0f) - 1f;
        x = now;
        return now;
    }

    // ---- compound assignments that keep DM's in-place list semantics (rules 4.3, 6.3) ----

    /// <summary>
    /// DM <c>A += B</c> on an <c>object</c> target (OpenDream <c>Append</c>): a list is changed in place (B's items and
    /// associated values added when B is a list, B itself otherwise, null included); a null target becomes B itself
    /// (the same list); a matrix adds B in place; numbers add; text concatenates; <c>A += null</c> leaves A alone.
    /// </summary>
    public static void append(ref object A, object B)
    {
        switch (A)
        {
            case DmList list:
                list.AppendOp(B);
                return;
            case null:
                A = B;
                return;
            case matrix m:
                if (B is not matrix mb) throw BadOperands("+=", A, B);
                m.Add(mb);
                return;
        }
        if (B == null) return;
        if (TryGetNumber(A, out float a) && TryGetNumber(B, out float b)) A = a + b;
        else if (A is string sa && B is string sb) A = sa + sb;
        else A = add(A, B);
    }

    /// <summary>DM <c>A += B</c> on a <c>float?</c> target (null as 0; B null leaves A alone; a non-number B is a type mismatch).</summary>
    public static void append(ref float? A, object B)
    {
        if (B == null) return;
        if (!TryGetNumber(B, out float b)) throw BadOperands("+=", A, B);
        A = A.HasValue ? A.GetValueOrDefault() + b : b;
    }

    /// <summary>
    /// DM <c>L += B</c> on a <c>DmList</c> target: in place; a null L becomes B itself when B is a list and stays null
    /// when B is null; any other B with a null L would turn the list var into a non-list, a runtime error (rules 6.3).
    /// </summary>
    public static void append(ref DmList A, object B)
    {
        if (A != null)
        {
            A.AppendOp(B);
            return;
        }
        if (B == null) return;
        A = B as DmList ?? throw ListTargetMismatch("+=", B);
    }

    /// <summary>DM <c>A.overlays += B</c> on an engine list (never null, rules 4.3): in place, no rebinding.</summary>
    public static void append(DmList A, object B) => (A ?? throw NullEngineList()).AppendOp(B);

    /// <summary>
    /// DM <c>A -= B</c> on an <c>object</c> target: a list loses the last occurrence of B or of each of B's items, in
    /// place (B read first, so <c>L -= L</c> empties L); numbers subtract with null as 0 on either side
    /// (<c>null -= 5</c> gives -5, OpenDream test <c>Remove</c>); a matrix subtracts B in place.
    /// </summary>
    public static void remove(ref object A, object B)
    {
        switch (A)
        {
            case DmList list:
                list.RemoveOp(B);
                return;
            case matrix m:
                if (B is not matrix mb) throw BadOperands("-=", A, B);
                m.Subtract(mb);
                return;
        }
        if ((A == null || TryGetNumber(A, out _)) && (B == null || TryGetNumber(B, out _))) A = num(A) - num(B);
        else A = sub(A, B);
    }

    /// <summary>DM <c>A -= B</c> on a <c>float?</c> target (null as 0 on either side).</summary>
    public static void remove(ref float? A, object B)
    {
        if (B != null && !TryGetNumber(B, out _)) throw BadOperands("-=", A, B);
        A = num(A) - num(B);
    }

    /// <summary>DM <c>L -= B</c> on a <c>DmList</c> target, in place; a null L is a runtime error (in DM it becomes a number or fails).</summary>
    public static void remove(ref DmList A, object B) => (A ?? throw ListTargetMismatch("-=", B)).RemoveOp(B);

    /// <summary>DM <c>A.overlays -= B</c> on an engine list, in place.</summary>
    public static void remove(DmList A, object B) => (A ?? throw NullEngineList()).RemoveOp(B);

    /// <summary>DM <c>A |= B</c> on a <c>float</c> target: 24-bit OR.</summary>
    public static void combine(ref float A, float B) => A = bitor(A, B);

    /// <summary>DM <c>A |= B</c> on a <c>float</c> target with an <c>object</c> operand (B null leaves A alone).</summary>
    public static void combine(ref float A, object B)
    {
        if (B == null) return;
        if (!TryGetNumber(B, out float b)) throw BadOperands("|=", A, B);
        A = bitor(A, b);
    }

    /// <summary>DM <c>A |= B</c> on a <c>float?</c> target: a null A becomes B (a number, or null).</summary>
    public static void combine(ref float? A, object B)
    {
        if (B == null) return;
        if (!TryGetNumber(B, out float b)) throw BadOperands("|=", A, B);
        A = A.HasValue ? bitor(A.GetValueOrDefault(), b) : b;
    }

    /// <summary>
    /// DM <c>A |= B</c> on an <c>object</c> target (OpenDream <c>Combine</c>): numbers OR (24-bit; B null leaves A
    /// alone); a list adds B's items it does not have yet, in place; a null target becomes B itself.
    /// </summary>
    public static void combine(ref object A, object B)
    {
        switch (A)
        {
            case DmList list:
                list.CombineOp(B);
                return;
            case null:
                A = B;
                return;
        }
        if (TryGetNumber(A, out float a))
        {
            if (B == null) return;
            if (TryGetNumber(B, out float b))
            {
                A = bitor(a, b);
                return;
            }
        }
        throw BadOperands("|=", A, B);
    }

    /// <summary>DM <c>L |= B</c> on a <c>DmList</c> target, in place; a null L becomes B when B is a list (stays null for a null B).</summary>
    public static void combine(ref DmList A, object B)
    {
        if (A != null)
        {
            A.CombineOp(B);
            return;
        }
        if (B == null) return;
        A = B as DmList ?? throw ListTargetMismatch("|=", B);
    }

    /// <summary>DM <c>A.overlays |= B</c> on an engine list, in place.</summary>
    public static void combine(DmList A, object B) => (A ?? throw NullEngineList()).CombineOp(B);

    /// <summary>DM <c>A &amp;= B</c> on a <c>float</c> target: 24-bit AND.</summary>
    public static void mask(ref float A, float B) => A = bitand(A, B);

    /// <summary>DM <c>A &amp;= B</c> on a <c>float</c> target with an <c>object</c> operand (B null gives 0).</summary>
    public static void mask(ref float A, object B)
    {
        if (B == null)
        {
            A = 0f;
            return;
        }
        if (!TryGetNumber(B, out float b)) throw BadOperands("&=", A, B);
        A = bitand(A, b);
    }

    /// <summary>DM <c>A &amp;= B</c> on a <c>float?</c> target: a null A or a null B gives 0 (OpenDream <c>Mask</c>).</summary>
    public static void mask(ref float? A, object B)
    {
        if (!A.HasValue || B == null)
        {
            A = 0f;
            return;
        }
        if (!TryGetNumber(B, out float b)) throw BadOperands("&=", A, B);
        A = bitand(A.GetValueOrDefault(), b);
    }

    /// <summary>
    /// DM <c>A &amp;= B</c> on an <c>object</c> target: numbers AND (a null A or B gives 0); a list keeps, in place, only
    /// items also in B (as many copies as B has; a non-list B keeps its first occurrence).
    /// </summary>
    public static void mask(ref object A, object B)
    {
        switch (A)
        {
            case DmList list:
                list.MaskOp(B);
                return;
            case null:
                A = 0f;
                return;
        }
        if (TryGetNumber(A, out float a))
        {
            if (B == null)
            {
                A = 0f;
                return;
            }
            if (TryGetNumber(B, out float b))
            {
                A = bitand(a, b);
                return;
            }
        }
        throw BadOperands("&=", A, B);
    }

    /// <summary>DM <c>L &amp;= B</c> on a <c>DmList</c> target, in place; a null L is a runtime error (DM would store 0).</summary>
    public static void mask(ref DmList A, object B) => (A ?? throw ListTargetMismatch("&=", B)).MaskOp(B);

    /// <summary>DM <c>A.overlays &amp;= B</c> on an engine list, in place.</summary>
    public static void mask(DmList A, object B) => (A ?? throw NullEngineList()).MaskOp(B);

    /// <summary>DM <c>A ^= B</c> on a <c>float</c> target: 24-bit XOR.</summary>
    public static void bitxor_assign(ref float A, float B) => A = bitxor(A, B);

    /// <summary>DM <c>A ^= B</c> on a <c>float</c> target with an <c>object</c> operand (B null leaves A as its integer).</summary>
    public static void bitxor_assign(ref float A, object B)
    {
        if (B == null)
        {
            A = Bits24(A);
            return;
        }
        if (!TryGetNumber(B, out float b)) throw BadOperands("^=", A, B);
        A = bitxor(A, b);
    }

    /// <summary>DM <c>A ^= B</c> on a <c>float?</c> target: <c>null ^ null</c> stays null, <c>null ^ n</c> is n.</summary>
    public static void bitxor_assign(ref float? A, object B)
    {
        if (B == null)
        {
            if (A.HasValue) A = Bits24(A.GetValueOrDefault());
            return;
        }
        if (!TryGetNumber(B, out float b)) throw BadOperands("^=", A, B);
        A = A.HasValue ? bitxor(A.GetValueOrDefault(), b) : Bits24(b);
    }

    /// <summary>
    /// DM <c>A ^= B</c> on an <c>object</c> target: numbers XOR (null as an absent operand: <c>null ^ 5 == 5</c>,
    /// <c>null ^ null</c> is null); a list keeps the symmetric difference in place (a non-list B toggles one
    /// occurrence).
    /// </summary>
    public static void bitxor_assign(ref object A, object B)
    {
        if (A is DmList list)
        {
            list.XorAssignOp(B);
            return;
        }
        A = bitxor(A, B);
    }

    /// <summary>DM <c>L ^= B</c> on a <c>DmList</c> target, in place; a null L stays null for a null B and is a runtime error otherwise.</summary>
    public static void bitxor_assign(ref DmList A, object B)
    {
        if (A != null)
        {
            A.XorAssignOp(B);
            return;
        }
        if (B != null) throw ListTargetMismatch("^=", B);
    }

    /// <summary>DM <c>A.overlays ^= B</c> on an engine list, in place.</summary>
    public static void bitxor_assign(DmList A, object B) => (A ?? throw NullEngineList()).XorAssignOp(B);

    /// <summary>
    /// DM <c>L[i] += B</c> (rules 4.3): reads the element, applies what <see cref="append(ref object, object)"/> does to
    /// it (an inner list changes in place; a null element becomes B; a number is added) and stores the result back.
    /// </summary>
    public static void append_at(DmList L, object index, object B)
    {
        object v = Element(L, index);
        append(ref v, B);
        L[index] = v;
    }

    /// <summary>DM <c>L[i] -= B</c> (rules 4.3).</summary>
    public static void remove_at(DmList L, object index, object B)
    {
        object v = Element(L, index);
        remove(ref v, B);
        L[index] = v;
    }

    /// <summary>DM <c>L[i] |= B</c> (rules 4.3).</summary>
    public static void combine_at(DmList L, object index, object B)
    {
        object v = Element(L, index);
        combine(ref v, B);
        L[index] = v;
    }

    /// <summary>DM <c>L[i] &amp;= B</c> (rules 4.3).</summary>
    public static void mask_at(DmList L, object index, object B)
    {
        object v = Element(L, index);
        mask(ref v, B);
        L[index] = v;
    }

    /// <summary>DM <c>L[i] ^= B</c> (rules 4.3).</summary>
    public static void bitxor_assign_at(DmList L, object index, object B)
    {
        object v = Element(L, index);
        bitxor_assign(ref v, B);
        L[index] = v;
    }

    /// <summary>DM <c>L[i]++</c> as a statement (rules 4.3; master.dm:300): a non-number element counts as 0.</summary>
    public static void inc_at(DmList L, object index)
    {
        object v = Element(L, index);
        inc(ref v);
        L[index] = v;
    }

    /// <summary>DM <c>L[i]--</c> as a statement.</summary>
    public static void dec_at(DmList L, object index)
    {
        object v = Element(L, index);
        dec(ref v);
        L[index] = v;
    }

    /// <summary>DM <c>L[i]++</c> in a value context: the old value as a number.</summary>
    public static float postinc_at(DmList L, object index)
    {
        object v = Element(L, index);
        float old = postinc(ref v);
        L[index] = v;
        return old;
    }

    /// <summary>DM <c>++L[i]</c> in a value context: the new value.</summary>
    public static float preinc_at(DmList L, object index)
    {
        object v = Element(L, index);
        float now = preinc(ref v);
        L[index] = v;
        return now;
    }

    /// <summary>DM <c>L[i]--</c> in a value context: the old value.</summary>
    public static float postdec_at(DmList L, object index)
    {
        object v = Element(L, index);
        float old = postdec(ref v);
        L[index] = v;
        return old;
    }

    /// <summary>DM <c>--L[i]</c> in a value context: the new value.</summary>
    public static float predec_at(DmList L, object index)
    {
        object v = Element(L, index);
        float now = predec(ref v);
        L[index] = v;
        return now;
    }

    // ---- bit operations: 24-bit (BYOND ref &, |, ^, ~, <<, >>; OpenDream masks with 0x00FFFFFF; rules 4.4) ----

    /// <summary>DM <c>A &amp; B</c> on numbers: 24-bit AND (each operand truncated and taken modulo 2^24).</summary>
    public static float bitand(float A, float B) => ToBits(A) & ToBits(B);

    /// <summary>
    /// DM <c>A &amp; B</c>: numbers (24-bit AND; a null operand gives 0) or lists (a new list of the items in both, in
    /// A's order, as many copies as both have, with A's associated values; BYOND ref <c>&amp;</c>).
    /// </summary>
    public static object bitand(object A, object B)
    {
        if (A is DmList list)
        {
            DmList result = list.CopyAll();
            result.MaskOp(B);
            return result;
        }
        if (A == null || B == null) return 0f;
        if (TryGetNumber(A, out float a) && TryGetNumber(B, out float b)) return bitand(a, b);
        throw BadOperands("&", A, B);
    }

    /// <summary>DM <c>A | B</c> on numbers: 24-bit OR.</summary>
    public static float bitor(float A, float B) => ToBits(A) | ToBits(B);

    /// <summary>
    /// DM <c>A | B</c>: <c>null | B</c> is B; numbers (24-bit OR; <c>A | null</c> is A); lists (a new list: A's items,
    /// then B's items A does not have, with their associated values; BYOND ref <c>|</c>).
    /// </summary>
    public static object bitor(object A, object B)
    {
        if (A == null) return B;
        if (A is DmList list)
        {
            DmList result = list.CopyAll();
            result.CombineOp(B);
            return result;
        }
        if (TryGetNumber(A, out float a))
        {
            if (B == null) return Bits24(a);
            if (TryGetNumber(B, out float b)) return bitor(a, b);
        }
        throw BadOperands("|", A, B);
    }

    /// <summary>DM <c>A ^ B</c> on numbers: 24-bit XOR.</summary>
    public static float bitxor(float A, float B) => ToBits(A) ^ ToBits(B);

    /// <summary>
    /// DM <c>A ^ B</c>: numbers (24-bit XOR; a null operand counts as absent: <c>null ^ 5 == 5</c>, <c>null ^ null</c>
    /// is null) or lists (items only in A first, then items only in B; BYOND ref <c>^</c>).
    /// </summary>
    public static object bitxor(object A, object B)
    {
        if (A is DmList list) return DmList.XorOp(list, B);
        if (A == null)
        {
            if (B == null) return null;
            if (TryGetNumber(B, out float onlyB)) return Bits24(onlyB);
            throw BadOperands("^", A, B);
        }
        if (TryGetNumber(A, out float a))
        {
            if (B == null) return Bits24(a);
            if (TryGetNumber(B, out float b)) return bitxor(a, b);
        }
        throw BadOperands("^", A, B);
    }

    /// <summary>DM <c>~A</c>: <c>(~A) &amp; 0xFFFFFF</c> (<c>~0 == 16777215</c>, <c>~1 == 16777214</c>).</summary>
    public static float bitnot(float A) => ~ToBits(A) & 0xFFFFFF;

    /// <summary>DM <c>~A</c> with an <c>object</c> operand: a non-number (null included) gives 16777215 (OpenDream <c>BitNot</c>; test <c>valid_and_null</c>).</summary>
    public static float bitnot(object A) => TryGetNumber(A, out float a) ? bitnot(a) : 0xFFFFFF;

    /// <summary>
    /// DM <c>A &lt;&lt; n</c>: <c>(A &lt;&lt; n) &amp; 0xFFFFFF</c> ("Bits shifted beyond the 24 low bits are lost";
    /// <c>0xBEEF &lt;&lt; 13 == 14540800</c>); a shift of 24 or more gives 0, a negative one shifts nothing.
    /// </summary>
    public static float shl(float A, float n)
    {
        int bits = ToBits(A);
        int s = ShiftCount(n);
        if (s >= 24) return 0f;
        return (bits << s) & 0xFFFFFF;
    }

    /// <summary>DM <c>A &gt;&gt; n</c>: <c>(A &amp; 0xFFFFFF) &gt;&gt; n</c> (<c>-1 &gt;&gt; 13 == 2047</c>).</summary>
    public static float shr(float A, float n)
    {
        int bits = ToBits(A);
        int s = ShiftCount(n);
        if (s >= 24) return 0f;
        return bits >> s;
    }

    /// <summary>
    /// DM <c>A &lt;&lt; n</c> with an <c>object</c> or <c>float?</c> operand (rules 4.4: every bit operation is a shim call,
    /// whatever the operand types): <c>null &lt;&lt; n == 0</c>, <c>A &lt;&lt; null == A</c> (OpenDream test
    /// <c>valid_and_null</c>); other kinds are a runtime error.
    /// </summary>
    public static float shl(object A, object n)
    {
        if (A == null) return 0f;
        if (!TryGetNumber(A, out float a)) throw BadOperands("<<", A, n);
        if (n == null) return Bits24(a);
        if (!TryGetNumber(n, out float s)) throw BadOperands("<<", A, n);
        return shl(a, s);
    }

    /// <summary>DM <c>A &gt;&gt; n</c> with an <c>object</c> or <c>float?</c> operand (<c>null &gt;&gt; n == 0</c>, <c>A &gt;&gt; null == A</c>).</summary>
    public static float shr(object A, object n)
    {
        if (A == null) return 0f;
        if (!TryGetNumber(A, out float a)) throw BadOperands(">>", A, n);
        if (n == null) return Bits24(a);
        if (!TryGetNumber(n, out float s)) throw BadOperands(">>", A, n);
        return shr(a, s);
    }

    // ---- private helpers ----

    // A DM number as its 24-bit integer: truncated toward zero, then modulo 2^24 (negative numbers wrap: -1 is
    // 0xFFFFFF); NaN and infinities are 0.
    private static int ToBits(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0;
        double t = Math.Truncate((double)value);
        long l = t >= long.MaxValue ? long.MaxValue : t <= long.MinValue ? long.MinValue : (long)t;
        return (int)(l & 0xFFFFFF);
    }

    private static float Bits24(float value) => ToBits(value);

    private static int ShiftCount(float n)
    {
        if (float.IsNaN(n) || n <= 0f) return 0;
        return n >= 24f ? 24 : (int)n;
    }

    // A value read as a number where DM treats every non-number as 0 (*, **, ++, --, unary -).
    private static float NumberOrZero(object value) => TryGetNumber(value, out float f) ? f : 0f;

    // A reference-like comparison operand: not null, not a number, not text.
    private static bool IsReference(object value) => value != null && value is not string && !TryGetNumber(value, out _);

    private static bool NullAndZeroOrEmpty(object A, object B)
    {
        if (B == null && TryGetNumber(A, out float a)) return a == 0f;
        if (A == null && TryGetNumber(B, out float b)) return b == 0f;
        return A == null && B is string s && s.Length == 0;
    }

    private static bool LessThan(object A, object B)
    {
        if (TryGetNumber(A, out float a))
        {
            if (TryGetNumber(B, out float b)) return a < b;
            if (B == null) return a < 0f;
        }
        else if (A is string sa)
        {
            if (B is string sb) return string.CompareOrdinal(sa, sb) < 0;
        }
        else if (A == null)
        {
            if (TryGetNumber(B, out float b)) return 0f < b;
            if (B is string sb) return sb.Length != 0;
            if (B == null) return false;
        }
        throw BadOperands("<", A, B);
    }

    private static bool GreaterThan(object A, object B)
    {
        if (TryGetNumber(A, out float a))
        {
            if (TryGetNumber(B, out float b)) return a > b;
            if (B == null) return a > 0f;
        }
        else if (A is string sa)
        {
            if (B is string sb) return string.CompareOrdinal(sa, sb) > 0;
        }
        else if (A == null)
        {
            if (TryGetNumber(B, out float b)) return 0f > b;
            if (B is string || B == null) return false;
        }
        throw BadOperands(">", A, B);
    }

    // The element of a list compound assignment; the list itself must exist (DM: "cannot read null[...]").
    private static object Element(DmList L, object index)
    {
        if (L == null) throw new DmRuntimeException("cannot index a null list");
        return L[index];
    }

    private static DmRuntimeException BadOperands(string op, object A, object B)
        => new DmRuntimeException("type mismatch: " + KindOf(A) + " " + op + " " + KindOf(B));

    private static DmRuntimeException ListTargetMismatch(string op, object B)
        => new DmRuntimeException("type mismatch: a null list var " + op + " " + KindOf(B) + " would not hold a list (rules 6.3)");

    private static DmRuntimeException NullEngineList() => new DmRuntimeException("an engine list is never null");

    // A DM operator overload of a datum (rules 7.10: proc/operator+ is the C# method operator__add), called with one
    // argument; false when the object defines none.
    internal static bool TryCallOperator(object target, string method, object argument, out object result)
    {
        result = null;
        if (target is not IDmObject) return false;
        System.Reflection.MethodInfo info = target.GetType().GetMethod(method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (info == null) return false;
        System.Reflection.ParameterInfo[] parameters = info.GetParameters();
        var args = new object[parameters.Length];
        if (args.Length > 0) args[0] = argument;
        try
        {
            result = info.Invoke(target, args);
        }
        catch (System.Reflection.TargetInvocationException e) when (e.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
        }
        return true;
    }
}

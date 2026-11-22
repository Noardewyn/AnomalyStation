using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Contracts;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// Comparisons, arithmetic, increments and 24-bit bit operations on DM values (docs/ai/translation-rules.md 4.2-4.4, 4.10):
/// BYOND ref "operators" (<c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, <c>%</c>, <c>%%</c>, <c>**</c>, <c>&amp;</c>,
/// <c>|</c>, <c>^</c>, <c>~</c>, <c>&lt;&lt;</c>, <c>&gt;&gt;</c>, <c>~=</c>, <c>&lt;=&gt;</c>); OpenDream's BYOND-checked
/// operator tests (Operators/*.dm: the Addition, Subtraction, Multiplication, Division, Modulo, Power and Negation
/// tables over 10, "ABC", null, a list, a datum and a typepath; comparison, comparison2, edge_comparison,
/// valid_and_null, valid_and_null_assign, bitshift_large, equivalence) and Math/IncrementAndDecrement, Math/modulus.
/// </summary>
public class OperatorTests
{
    private static readonly TestDatum Datum = new TestDatum();

    [Fact]
    public void AdditionTable()
    {
        V.Eq(20f, Dm.add(10f, 10f));
        V.Error(() => Dm.add(10f, "ABC"));
        V.Eq(10f, Dm.add(10f, null));
        V.Error(() => Dm.add(10f, Dm.list("ABC")));
        V.Error(() => Dm.add(10f, Datum));
        V.Error(() => Dm.add("ABC", 10f));
        V.Eq("ABCABC", Dm.add("ABC", "ABC"));
        V.Eq("ABC", Dm.add("ABC", null));
        V.Eq(10f, Dm.add(null, 10f));
        V.Eq("ABC", Dm.add(null, "ABC"));
        Assert.Null(Dm.add(null, null));
        DmList L = Dm.list("ABC");
        Assert.Same(L, Dm.add(null, L));              // null + x is x itself
        V.Error(() => Dm.add(Datum, 10f));
        V.Error(() => Dm.add(typeof(DmList), 10f));
        V.Error(() => Dm.add(typeof(DmList), null));
        V.Eq("added 5", Dm.add(new AddOverloadDatum(), 5f));   // operator+ (rules 7.10)
    }

    [Fact]
    public void SubtractionTable()
    {
        V.Eq(0f, Dm.sub(10f, 10f));
        V.Error(() => Dm.sub(10f, "ABC"));
        V.Eq(10f, Dm.sub(10f, null));
        V.Error(() => Dm.sub(10f, Datum));
        V.Error(() => Dm.sub("ABC", "ABC"));
        V.Error(() => Dm.sub("ABC", null));
        V.Eq(-10f, Dm.sub(null, 10f));
        V.Error(() => Dm.sub(null, "ABC"));
        V.Eq(0f, Dm.sub(null, null));
        V.Error(() => Dm.sub(Datum, 10f));
    }

    [Fact]
    public void MultiplicationTable()
    {
        V.Eq(100f, Dm.mul(10f, 10f));
        V.Eq(0f, Dm.mul(10f, "ABC"));                 // non-numbers count as 0 on the right
        V.Eq(0f, Dm.mul(10f, null));
        V.Eq(0f, Dm.mul(10f, Dm.list("ABC")));
        V.Eq(0f, Dm.mul(10f, Datum));
        V.Error(() => Dm.mul("ABC", 10f));
        V.Eq(0f, Dm.mul(null, 10f));
        V.Eq(0f, Dm.mul(null, "ABC"));
        V.Error(() => Dm.mul(Dm.list("ABC"), 10f));
        V.Error(() => Dm.mul(Datum, 10f));
    }

    [Fact]
    public void DivisionTable()
    {
        V.Eq(1f, Dm.div(10f, (object)10f));
        V.Error(() => Dm.div(10f, (object)"ABC"));
        V.Error(() => Dm.div(10f, (object)null));     // "Undefined operation error in BYOND"
        V.Error(() => Dm.div("ABC", (object)10f));
        V.Eq(0f, Dm.div(null, (object)10f));
        V.Eq(0f, Dm.div(null, (object)"ABC"));
        V.Eq(0f, Dm.div(null, (object)null));
        V.Error(() => Dm.div(Datum, (object)10f));
        V.Error(() => Dm.div(10f, (object)0f));       // "Division by zero"
        Assert.Equal(2.5f, Dm.div(5f, 2f));
        V.Error(() => Dm.div(1f, 0f));
    }

    [Fact]
    public void NegationTable()
    {
        V.Eq(-10f, Dm.neg(10f));
        V.Eq(0f, Dm.neg("ABC"));
        V.Eq(0f, Dm.neg(null));
        V.Eq(0f, Dm.neg(Dm.list("ABC")));
        V.Eq(0f, Dm.neg(Datum));
        V.Eq(0f, Dm.neg(typeof(DmList)));
    }

    [Fact]
    public void ModuloTruncatesItsOperands()
    {
        // BYOND ref %: "A and B are truncated to integers before the modulo operation"; Math/modulus, Modulo.
        Assert.Equal(0f, Dm.mod(10f, 5f));
        Assert.Equal(20f, Dm.mod(20f, 60f));
        Assert.Equal(0f, Dm.mod(30.8f, 30f));
        Assert.Equal(0f, Dm.mod(30.1f, 30f));
        Assert.Equal(1f, Dm.mod(5f, 2.5f));
        Assert.Equal(60f, Dm.mod(60f, 62.5f));
        Assert.Equal(2f, Dm.mod(32f, 6f));
        Assert.Equal(5f, Dm.mod(5f, 256f));
        Assert.Equal(-1f, Dm.mod(-7f, 3f));          // the dividend's sign (rules 4.3)
        Assert.Equal(0f, Dm.mod((object)null, 10f));
        V.Error(() => Dm.mod((object)10f, null));
        V.Error(() => Dm.mod((object)10f, "ABC"));
        V.Error(() => Dm.mod((object)"ABC", 10f));
        V.Error(() => Dm.mod(4f, 0.5f));              // the divisor truncates to 0
    }

    [Fact]
    public void FloatModuloIsBTimesFractOfAOverB()
    {
        // BYOND ref %%: "equivalent to B * fract(A / B)".
        Assert.Equal(-180f, Dm.fmod(-180f, 360f));
        Assert.Equal(-1f, Dm.fmod(-1f, 3f));
        Assert.Equal(180f, Dm.fmod(540f, 360f));
        Assert.Equal(0.5f, Dm.fmod(2.5f, 1f));
        Assert.Equal(180f, Dm.fmod((object)540, (object)360f));
        Assert.Equal(0f, Dm.fmod((object)null, 3f));
    }

    [Fact]
    public void PowerTable()
    {
        Assert.Equal(8f, Dm.pow(2f, 3f));             // BYOND ref ** example
        Assert.Equal(1e10f, Dm.pow((object)10f, 10f));
        Assert.Equal(1f, Dm.pow((object)10f, "ABC"));
        Assert.Equal(1f, Dm.pow((object)10f, null));
        Assert.Equal(0f, Dm.pow((object)null, 10f));
        Assert.Equal(1f, Dm.pow((object)null, "ABC"));
        V.Error(() => Dm.pow((object)"ABC", 1f));
        V.Error(() => Dm.pow(Datum, 1f));
        Assert.Equal(1f, Dm.pow(0f, 0f));
    }

    [Fact]
    public void ArithmeticIsSinglePrecision()
    {
        // rules 3.1: a DM number is an IEEE binary32 with 24-bit exact integers.
        Assert.True(Dm.eq(Dm.add(0.1f, 0.2f), 0.3f)); // false in double precision
        Assert.True(Dm.eq(Dm.add(16777216f, 1f), 16777216f));
        Assert.True(Dm.eq(Dm.mul(3, 0.1d), 0.3f));     // any numeric box is a float
    }

    [Fact]
    public void IncrementAndDecrementCoerceNonNumbersToZero()
    {
        // Math/IncrementAndDecrement; Operators/pre_increment, pre_decrement ("BYOND coerces every non-number including
        // null to 0 for ++ and --").
        object i = 1f;
        Dm.inc(ref i);
        V.Eq(2f, i);
        Assert.Equal(2f, Dm.postinc(ref i));
        V.Eq(3f, i);
        Assert.Equal(4f, Dm.preinc(ref i));
        Dm.dec(ref i);
        Assert.Equal(3f, Dm.postdec(ref i));
        Assert.Equal(1f, Dm.predec(ref i));
        object f = 1.5f;
        Assert.Equal(1.5f, Dm.postinc(ref f));
        V.Eq(2.5f, f);
        object text = "bad";
        Assert.Equal(1f, Dm.preinc(ref text));
        text = "bad";
        Assert.Equal(-1f, Dm.predec(ref text));
        object none = null;
        Assert.Equal(1f, Dm.preinc(ref none));
        none = null;
        Dm.dec(ref none);
        V.Eq(-1f, none);
        float? n = null;
        Dm.inc(ref n);
        Assert.Equal(1f, n);
        Assert.Equal(1f, Dm.postdec(ref n));
        Assert.Equal(0f, n);
        Assert.Equal(-1f, Dm.predec(ref n));
        n = null;
        Assert.Equal(0f, Dm.postinc(ref n));
        Assert.Equal(1f, n);
    }

    [Fact]
    public void CompoundAssignmentsWithNull()
    {
        // Operators/valid_and_null_assign (+=, -=, *=, /=, %=, &=, |=, ^=, <<=, >>= with null on either side).
        object a = 1f;
        Dm.append(ref a, 1f);
        V.Eq(2f, a);
        a = null;
        Dm.append(ref a, 1f);
        V.Eq(1f, a);
        a = 1f;
        Dm.append(ref a, null);
        V.Eq(1f, a);
        a = 1f;
        Dm.remove(ref a, 1f);
        V.Eq(0f, a);
        a = null;
        Dm.remove(ref a, 1f);
        V.Eq(-1f, a);
        a = 1f;
        Dm.remove(ref a, null);
        V.Eq(1f, a);
        a = null;
        Dm.remove(ref a, null);                       // Operators/Remove: null -= null is 0
        V.Eq(0f, a);
        a = null;
        a = Dm.mul(a, 2f);
        V.Eq(0f, a);
        a = 2f;
        a = Dm.mul(a, null);
        V.Eq(0f, a);
        a = null;
        a = Dm.div(a, (object)2f);
        V.Eq(0f, a);
        a = null;
        a = Dm.mod(a, 3f);
        V.Eq(0f, a);
        a = 1f;
        Dm.mask(ref a, 1f);
        V.Eq(1f, a);
        a = null;
        Dm.mask(ref a, 1f);
        V.Eq(0f, a);
        a = 1f;
        Dm.mask(ref a, null);
        V.Eq(0f, a);
        a = null;
        Dm.combine(ref a, 1f);
        V.Eq(1f, a);
        a = 1f;
        Dm.combine(ref a, null);
        V.Eq(1f, a);
        a = 1f;
        Dm.bitxor_assign(ref a, 1f);
        V.Eq(0f, a);
        a = null;
        Dm.bitxor_assign(ref a, 1f);
        V.Eq(1f, a);
        a = 1f;
        Dm.bitxor_assign(ref a, null);
        V.Eq(1f, a);
        a = 1f;
        a = Dm.shl(a, 1f);
        V.Eq(2f, a);
        a = null;
        a = Dm.shl(a, 1f);
        V.Eq(0f, a);
        a = 1f;
        a = Dm.shr(a, null);
        V.Eq(1f, a);
        V.Error(() => { object t = "text"; Dm.append(ref t, 1f); });
        object s = "a";
        Dm.append(ref s, "b");
        V.Eq("ab", s);
    }

    [Fact]
    public void NullableAndNumericTargets()
    {
        // The float and float? overloads of rules 4.3's table follow the same DM values.
        float? n = null;
        Dm.append(ref n, 5f);
        Assert.Equal(5f, n);
        Dm.append(ref n, null);
        Assert.Equal(5f, n);
        Dm.remove(ref n, 2f);
        Assert.Equal(3f, n);
        n = null;
        Dm.combine(ref n, 4f);
        Assert.Equal(4f, n);
        n = null;
        Dm.mask(ref n, 4f);
        Assert.Equal(0f, n);
        n = null;
        Dm.bitxor_assign(ref n, null);
        Assert.Null(n);
        Dm.bitxor_assign(ref n, 6f);
        Assert.Equal(6f, n);
        float flags = 7;
        Dm.mask(ref flags, Dm.bitnot(2));              // rules 4.4: datum_flags &= ~DF_USE_TAG
        Assert.Equal(5f, flags);
        Dm.combine(ref flags, 8);
        Assert.Equal(13f, flags);
        Dm.bitxor_assign(ref flags, 1);
        Assert.Equal(12f, flags);
        Dm.mask(ref flags, (object)null);
        Assert.Equal(0f, flags);
        V.Error(() => { float? t = null; Dm.append(ref t, "text"); });
    }

    [Fact]
    public void BitOperationsAreTwentyFourBit()
    {
        // BYOND ref &, |, ^, ~, <<, >>: "A and B must be between 0 and 2**24 - 1"; "Bits shifted beyond the 24 low
        // bits are lost"; Operators/bitshift_large, valid_and_null.
        Assert.Equal(16777214f, Dm.bitnot(1f));
        Assert.Equal(16777215f, Dm.bitnot(0f));
        Assert.Equal(16777215f, Dm.bitnot((object)null));
        Assert.Equal(16777215f, Dm.bitnot((object)"text"));
        Assert.Equal(14540800f, Dm.shl(0xBEEF, 13));
        Assert.Equal(2047f, Dm.shr(-1, 13));
        Assert.Equal(0f, Dm.shl(1, 24));
        Assert.Equal(2f, Dm.shl(1, 1));
        Assert.Equal(0f, Dm.shr(1, 1));
        Assert.Equal(0f, Dm.shl((object)null, 1f));
        Assert.Equal(1f, Dm.shl((object)1f, null));
        Assert.Equal(0f, Dm.shr((object)null, 1f));
        Assert.Equal(1f, Dm.shr((object)1f, null));
        Assert.Equal(0xFFFFFF, Dm.bitor(0xFFFFFF, 0x1000000));
        Assert.Equal(5f, Dm.bitand(-1, 5));
        Assert.Equal(3f, Dm.bitand(3.9f, 7));          // truncated
        V.Eq(1f, Dm.bitand((object)1f, 1f));
        V.Eq(0f, Dm.bitand((object)null, 1f));
        V.Eq(0f, Dm.bitand((object)1f, null));
        V.Eq(0f, Dm.bitxor((object)1f, 1f));
        V.Eq(5f, Dm.bitxor((object)null, 5f));
        V.Eq(5f, Dm.bitxor((object)5f, null));
        Assert.Null(Dm.bitxor((object)null, null));
        V.Eq(1f, Dm.bitor((object)1f, 1f));
        V.Eq(1f, Dm.bitor((object)null, 1f));
        V.Eq(1f, Dm.bitor((object)1f, null));
        V.Error(() => Dm.bitor((object)"a", 1f));
        V.Error(() => Dm.bitand((object)"a", 1f));
    }

    [Fact]
    public void ComparisonsWithNull()
    {
        // Operators/comparison and Expression/edge_comparison ("null violates the holy trichotomy").
        Assert.False(Dm.gt(null, 2f));
        Assert.False(Dm.gt(null, 0f));
        Assert.True(Dm.gt(null, -1f));
        Assert.False(Dm.gt(null, null));
        Assert.True(Dm.ge(null, 0f));
        Assert.True(Dm.ge(null, -1f));
        Assert.False(Dm.ge(null, 1f));
        Assert.True(Dm.ge(null, null));
        Assert.True(Dm.lt(null, 1f));
        Assert.False(Dm.lt(null, 0f));
        Assert.False(Dm.lt(null, null));
        Assert.True(Dm.le(null, 0f));
        Assert.False(Dm.le(null, -1f));
        Assert.True(Dm.le(null, null));
        Assert.True(Dm.gt(1f, null));
        Assert.True(Dm.ge(1f, null));
        Assert.False(Dm.lt(1f, null));
        Assert.False(Dm.le(1f, null));
        Assert.True(Dm.ge(0f, null));
        Assert.True(Dm.le(0f, null));
        Assert.False(Dm.gt(null, ""));
        Assert.True(Dm.ge(null, ""));
        Assert.True(Dm.le(null, ""));
        Assert.False(Dm.lt(null, ""));
        Assert.False(Dm.gt(null, "abc"));
        Assert.True(Dm.lt(null, "abc"));
        Assert.False(Dm.le(0.15f, null));
        Assert.False(Dm.ge(null, 0.5f));
        Assert.False(Dm.ge(-0.3f, null));
    }

    [Fact]
    public void ComparisonsOfTextAndReferences()
    {
        // Text compares as text (rules 4.2); a reference right operand leaves the left operand as the result
        // (Operators/comparison2), and a number against text is a runtime error.
        Assert.True(Dm.lt("a", "b"));
        Assert.True(Dm.lt("B", "a"));                 // case-sensitive (ordinal)
        Assert.True(Dm.ge("b", "b"));
        Assert.True(Dm.lt(1f, Datum));                // the left operand, 1, is the result
        Assert.False(Dm.lt(null, Dm.list(1f)));
        Assert.True(Dm.gt("abc", typeof(DmList)));
        Assert.False(Dm.ge(0f, Datum));
        V.Error(() => Dm.lt(1f, "a"));
        V.Error(() => Dm.lt(Datum, 1f));
        Assert.Equal(-1f, Dm.cmp(1f, 2f));
        Assert.Equal(1f, Dm.cmp("b", "a"));
        Assert.Equal(-1f, Dm.cmp("B", "a"));
        Assert.Equal(0f, Dm.cmp(null, 0f));
        Assert.Equal(0f, Dm.cmp(float.NaN, 1f));     // BYOND ref <=>: NaN gives 0
        V.Error(() => Dm.cmp(1f, "a"));
    }

    [Fact]
    public void Equivalence()
    {
        // BYOND ref ~= (516: associated values count); Operators/equivalence and valid_and_null.
        DmList l1 = Dm.list(1f, 2f, 3f);
        Assert.True(Dm.equiv(l1, l1));
        Assert.False(Dm.equiv(l1, Dm.list(1f, 2f, 3f, 4f)));
        Assert.False(Dm.equiv(l1, Dm.list(1f, 2f)));
        Assert.True(Dm.equiv(l1, Dm.list(1f, 2f, 3f)));
        DmList one = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 3f), Dm.kv("c", "hi"));
        Assert.False(Dm.equiv(one, Dm.list(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", "hi"))));
        Assert.True(Dm.equiv(one, Dm.list(Dm.kv("a", 1f), Dm.kv("b", 3f), Dm.kv("c", "hi"))));
        Assert.True(Dm.equiv(Dm.list(Dm.kv("a", null)), Dm.list("a")));
        matrix m1 = Dm.matrix(1, 2, 3, 4, 5, 6);
        matrix m2 = Dm.matrix(-1, -2, -3, -4, -5, 6);
        Assert.True(Dm.equiv(m1, m1));
        Assert.False(Dm.equiv(m1, m2));
        Assert.True(Dm.equiv(m1, Dm.matrix(1, 2, 3, 4, 5, 6)));
        Assert.False(Dm.equiv("apple", m1));
        Assert.False(Dm.equiv(m1, "apple"));
        Assert.True(Dm.equiv(1f, 1));
        Assert.True(Dm.equiv(null, null));
        Assert.False(Dm.equiv(null, 0f));
        Assert.False(Dm.equiv(Datum, new TestDatum()));
        Assert.True(Dm.equiv(Datum, Datum));
    }

    [Fact]
    public void IntegerModuloNeverGivesNegativeZero()
    {
        // BYOND ref %: "The remainder of A / B, where both A and B are integers" (both truncated first); an integer
        // remainder has no negative zero, so "[-3 % 3]" is "0" (OpenDream ModulusValues computes it in int). The IEEE
        // remainder of -3 by 3 would be -0, which DM text shows as "-0".
        Assert.Equal("0", Dm.str(Dm.mod(-3, 3)));
        Assert.Equal("0", Dm.str(Dm.mod(-6, -3)));
        Assert.Equal("0", Dm.str(Dm.mod(-0.5f, 3)));
        Assert.Equal("0", Dm.str(Dm.mod((object)(-4f), (object)2f)));
        Assert.Equal("0", Dm.text("[]", Dm.mod(-10, 5)));
        Assert.Equal("-1", Dm.str(Dm.mod(-7, 3)));
        Assert.Equal("1", Dm.str(Dm.mod(7, -3)));
        float x = -9;
        x = Dm.mod(x, 3);
        Assert.False(float.IsNegative(x));
    }

    [Fact]
    public void EveryNumberBoxComputesInSinglePrecision()
    {
        // rules 3.1: a DM number is an IEEE binary32 whatever box it arrives in (generated Defines are const int, rules
        // 3.1; the shim treats a boxed int exactly like the same float): an int box never does integer arithmetic
        // (no integer division, no exact integers past 2^24), a double box is narrowed before the operation.
        object big = 16777216, one = 1;
        Assert.True(Dm.eq(Dm.add(big, one), 16777216f));
        Assert.True(Dm.eq(Dm.add(16777217, 0), 16777216f));
        Assert.True(Dm.eq(Dm.div((object)1, (object)3), 1f / 3f));
        Assert.True(Dm.eq(Dm.mul((object)16777217, (object)1), 16777216f));
        Assert.True(Dm.eq(Dm.sub((object)int.MaxValue, one), 2147483648f));
        Assert.Equal("2.14748e+09", Dm.str(Dm.sub((object)int.MaxValue, one)));
        Assert.True(Dm.eq(Dm.add((object)0.1, (object)0.2), 0.1f + 0.2f));
        Assert.True(Dm.eq(Dm.mul((object)1e-30, (object)1e-30), 0f));        // underflows in float, not in double
        Assert.True(Dm.eq(Dm.mul((object)1e20, (object)1e20), float.PositiveInfinity));
        Assert.True(Dm.eq((object)16777217, (object)16777216));
        object counter = 16777216;
        Dm.inc(ref counter);
        Assert.True(Dm.eq(counter, 16777216f));
        Assert.Equal(1, Dm.mod((object)7, (object)3));
        Assert.Equal(16777215f, Dm.bitor((object)16777215, (object)0));
    }
}

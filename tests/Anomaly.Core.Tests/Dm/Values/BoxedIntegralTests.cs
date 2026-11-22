using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Pilot.Datum;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// A boxed integral is a DM number like the float of the same value, everywhere (rules 3.1: integer literals stay as
/// written in object contexts, <c>list_to_pick[item] = 0</c>, <c>return 1</c>, so a boxed <c>int</c> or <c>long</c>
/// reaches the shim; lead decision 14 of the v1.2 rules pass, docs/ai/specs/dm-conformance.md "Values"). Each test sets a
/// boxed <c>int</c> and a boxed <c>long</c> against the boxed <c>float</c> of the same value.
/// </summary>
public class BoxedIntegralTests
{
    private static readonly object IntOne = 1;
    private static readonly object LongOne = 1L;
    private static readonly object FloatOne = 1f;
    private static readonly object IntZero = 0;
    private static readonly object LongZero = 0L;

    [Fact]
    public void EqualityTreatsEveryBoxAsTheSameNumber()
    {
        Assert.True(Dm.eq(IntOne, FloatOne));
        Assert.True(Dm.eq(LongOne, FloatOne));
        Assert.True(Dm.eq(IntOne, LongOne));
        Assert.True(Dm.eq(IntOne, 1f));
        Assert.True(Dm.eq(1f, LongOne));
        Assert.False(Dm.eq(IntOne, "1"));                 // a number is never text
        Assert.False(Dm.eq(IntZero, null));                // nor null
        Assert.True(Dm.equiv(IntOne, FloatOne));
        Assert.False(Dm.lt(IntOne, FloatOne));
        Assert.True(Dm.le(LongOne, FloatOne));
        Assert.Equal(0f, Dm.cmp(IntOne, FloatOne));
    }

    [Fact]
    public void IsnumAndTruthinessSeeANumber()
    {
        Assert.True(Dm.isnum(IntOne));
        Assert.True(Dm.isnum(LongZero));
        Assert.False(Dm.istext(IntOne));
        Assert.True(Dm.truthy(IntOne));
        Assert.True(Dm.truthy(LongOne));
        Assert.False(Dm.truthy(IntZero));                  // 0 is false in every box (BYOND ref /operator/!)
        Assert.False(Dm.truthy(LongZero));
        Assert.Equal(1f, Dm.not(IntZero));
        Assert.Equal(1f, Dm.num(LongOne));
        Assert.False(Dm.istype(IntOne, typeof(DmList)));  // a number is never an instance
    }

    [Fact]
    public void InFindRemoveAndKeysMatchAcrossBoxes()
    {
        DmList L = Dm.list(1f, 2f, 3f);
        Assert.True(Dm.@in(IntOne, L));
        Assert.True(Dm.@in(LongOne, L));
        Assert.Equal(1f, L.Find(IntOne));
        Assert.Equal(1f, L.Find(LongOne));
        L.Remove(2L);                                      // removes the float 2
        V.Items(L, 1f, 3f);

        DmList ints = Dm.list(IntOne, LongOne, 2);
        Assert.True(Dm.@in(FloatOne, ints));
        Assert.Equal(1f, ints.Find(FloatOne));
        ints.Remove(1f);                                   // the last occurrence, whatever its box
        V.Items(ints, 1f, 2f);
        Assert.Equal(1f, ints.RemoveAll(2f));
        V.Items(ints, 1f);

        // A number is one key whatever its box: alist keys and the operators of rules 6.3.
        DmList A = Dm.alist(Dm.kv(IntOne, "one"));
        Assert.Equal("one", A[FloatOne]);
        Assert.Equal("one", A[LongOne]);
        A[LongOne] = "uno";
        Assert.Equal(1, A.Count);
        Assert.Equal("uno", A[IntOne]);
        DmList B = Dm.list(IntOne, 2L);
        Dm.remove(ref B, 2f);
        V.Items(B, 1f);
        DmList C = Dm.list(1f, 2f, 3f);
        object diff = Dm.sub(C, Dm.list(IntOne, 3L));
        V.Items((DmList)diff, 2f);
    }

    [Fact]
    public void TextAndJsonWriteTheSameDigits()
    {
        Assert.Equal(Dm.str(FloatOne), Dm.str(IntOne));
        Assert.Equal("1", Dm.str(LongOne));
        Assert.Equal("x1y", Dm.text("x[]y", IntOne));
        Assert.Equal("16777216", Dm.str(16777216));
        Assert.Equal(Dm.str(16777217f), Dm.str(16777217L)); // single precision in every box (rules 3.1)
        Assert.Equal(Dm.json_encode(Dm.list(1f, 2f)), Dm.json_encode(Dm.list(IntOne, 2L)));
        Assert.Equal("{\"a\":1}", Dm.json_encode(Dm.list(Dm.kv("a", IntOne))));
        Assert.Equal("1", Dm.json_encode(LongOne));
        Assert.Equal("a=1", Dm.list2params(Dm.list(Dm.kv("a", IntOne))));
        Assert.Equal(1f, Dm.text2num(Dm.str(IntOne)));
        Assert.Equal("1 apple", Dm.text("[] apple\\s", IntOne));  // \s sees the number 1 in an int box
        Assert.Equal("1 apple", Dm.text("[] apple\\s", LongOne));
        Assert.Equal("2 apples", Dm.text("[] apple\\s", 2L));
    }

    [Fact]
    public void ArithmeticGivesAFloat()
    {
        Assert.IsType<float>(Dm.add(IntOne, LongOne));
        Assert.True(Dm.eq(2f, Dm.add(IntOne, LongOne)));
        object x = IntOne;
        Dm.inc(ref x);
        Assert.True(Dm.eq(2f, x));
        Assert.True(Dm.eq(1f, Dm.max(IntZero, LongOne)));
    }

    [Fact]
    public void TheStateHashDoesNotSeeTheBox()
    {
        // The field hasher (object-model.md section 8) hashes a number by its value: a var holding a boxed int, a boxed
        // long or the float of the same value, alone or in a list, hashes the same.
        ulong Hash(object value)
        {
            var t = new DatumTestWorld();
            var keep = new List<object>();
            using (t.World.Enter())
            {
                test_datum_listener a = Dm.@new<test_datum_listener>();
                a.returns = value;
                a.other = Dm.@new<datum>();
                keep.Add(a);
            }
            ulong hash = t.World.ComputeStateHash();
            GC.KeepAlive(keep);
            return hash;
        }

        ulong floatHash = Hash(FloatOne);
        Assert.Equal(floatHash, Hash(IntOne));
        Assert.Equal(floatHash, Hash(LongOne));
        Assert.NotEqual(floatHash, Hash(2f));
        ulong floatList = Hash(Dm.list(FloatOne, Dm.kv("k", 0f)));
        Assert.Equal(floatList, Hash(Dm.list(IntOne, Dm.kv("k", IntZero))));
        Assert.Equal(floatList, Hash(Dm.list(LongOne, Dm.kv("k", LongZero))));
    }
}

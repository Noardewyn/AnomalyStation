using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// The shared conversions both shim packages rely on (docs/ai/translation-rules.md 3.5, 3.6, 4.1, 4.2). Expected values
/// are DM's: BYOND ref <c>/operator/!</c> (0, "" and null are the only false values), <c>/operator/==</c>,
/// <c>/proc/isnum</c>, and the rules' tables.
/// </summary>
public class ConversionTests
{
    [Fact]
    public void NumTurnsNullIntoZeroKeepsNumbersInAnyBoxAndRejectsOtherKinds()
    {
        Assert.Equal(0f, Dm.num((object)null));
        Assert.Equal(2.5f, Dm.num((object)2.5f));
        Assert.Equal(7f, Dm.num((object)7));              // a boxed int from a Defines constant
        Assert.Equal(0.25f, Dm.num((object)0.25d));
        Assert.Equal(1f, Dm.num((object)true));          // a boxed C# bool is a DM number 1
        Assert.Equal(3f, Dm.num((object)3L));
        Assert.Throws<DmRuntimeException>(() => Dm.num((object)"5")); // DM never reads text as a number
        Assert.Throws<DmRuntimeException>(() => Dm.num(new object()));
        Assert.Throws<DmRuntimeException>(() => Dm.num((object)new DmList()));
    }

    [Fact]
    public void NullTextIsDmNullEverywhere()
    {
        // A string-typed var or local that holds null is DM null (rules 3.6): 0 in arithmetic, false in conditions,
        // equal to null only, and not text (BYOND ref /proc/istext: true only for a text string).
        string none = null;
        Assert.Equal(0f, Dm.num(none));
        Assert.Null(Dm.num_or_null(none));
        Assert.False(Dm.truthy(none));
        Assert.False(Dm.truthy((object)none));
        Assert.Equal(1f, Dm.not(none));
        Assert.True(Dm.eq(none, (object)null));
        Assert.False(Dm.eq(none, ""));
        Assert.False(Dm.eq(none, 0f));
        Assert.False(Dm.istext(none));
        Assert.False(Dm.isnum(none));
    }

    [Fact]
    public void NumOfNullableAndBool()
    {
        Assert.Equal(0f, Dm.num((float?)null));
        Assert.Equal(-4f, Dm.num((float?)-4f));
        Assert.Equal(1f, Dm.num(true));
        Assert.Equal(0f, Dm.num(false));
        Assert.Equal(1f, Dm.num(3 > 2));
    }

    [Fact]
    public void NumOrNullKeepsNull()
    {
        Assert.Null(Dm.num_or_null(null));
        Assert.Equal(9f, Dm.num_or_null(9));
        Assert.Equal(0f, Dm.num_or_null(0f));
        Assert.Throws<DmRuntimeException>(() => Dm.num_or_null("x"));
    }

    [Fact]
    public void TruthinessFollowsDm()
    {
        // BYOND ref /operator/!: 0, "" and null are the only false values; everything else is true.
        Assert.False(Dm.truthy((object)null));
        Assert.False(Dm.truthy((object)0f));
        Assert.False(Dm.truthy((object)0));
        Assert.False(Dm.truthy((object)""));
        Assert.True(Dm.truthy((object)"0"));
        Assert.True(Dm.truthy((object)-1f));
        Assert.True(Dm.truthy((object)new DmList()));    // an empty list is true
        Assert.True(Dm.truthy(new object()));
        Assert.True(Dm.truthy((object)typeof(DmList)));
        Assert.True(Dm.truthy((object)float.NaN));       // NaN != 0 (OpenDream DreamValue.IsTruthy)

        Assert.False(Dm.truthy(0f));
        Assert.True(Dm.truthy(0.5f));
        Assert.False(Dm.truthy((float?)null));
        Assert.False(Dm.truthy((float?)0f));
        Assert.True(Dm.truthy((float?)2f));
        Assert.False(Dm.truthy((string)null));
        Assert.False(Dm.truthy(""));
        Assert.True(Dm.truthy(" "));
        Assert.False(Dm.truthy((DmList)null));
        Assert.True(Dm.truthy(new DmList()));
        Assert.False(Dm.truthy((Type)null));
        Assert.True(Dm.truthy(typeof(string)));
    }

    [Fact]
    public void NotIsTheDmNumberOfTheNegatedCondition()
    {
        Assert.Equal(1f, Dm.not((object)null));
        Assert.Equal(1f, Dm.not((object)""));
        Assert.Equal(0f, Dm.not((object)"a"));
        Assert.Equal(1f, Dm.not(0f));
        Assert.Equal(0f, Dm.not(3f));
        Assert.Equal(1f, Dm.not((float?)null));
        Assert.Equal(1f, Dm.not((string)null));
        Assert.Equal(0f, Dm.not(new DmList()));
    }

    [Fact]
    public void EqualityComparesNumbersByValueTextOrdinallyAndEverythingElseByReference()
    {
        Assert.True(Dm.eq((object)1f, (object)1f));       // two boxes of the same number (C# == would say false)
        Assert.True(Dm.eq((object)1, (object)1f));        // int and float boxes
        Assert.True(Dm.eq((object)0.5d, (object)0.5f));
        Assert.True(Dm.eq((object)0f, (object)(-0f)));
        Assert.False(Dm.eq((object)float.NaN, (object)float.NaN));
        Assert.True(Dm.eq((object)"abc", (object)"abc"));
        Assert.False(Dm.eq((object)"abc", (object)"ABC"));   // == is case-sensitive (rules 5.6)
        Assert.True(Dm.eq((object)null, (object)null));
        Assert.True(Dm.eq((object)true, (object)1f));    // a stray boxed C# bool is the DM number 1 (rules 3.1)
        Assert.True(Dm.eq((object)7L, (object)7));        // any numeric box

        // Different kinds are never equal and never raise (rules 4.2).
        Assert.False(Dm.eq((object)null, (object)0f));
        Assert.False(Dm.eq((object)null, (object)""));
        Assert.False(Dm.eq((object)"1", (object)1f));
        Assert.False(Dm.eq((object)0f, (object)""));

        var a = new DmList();
        var b = new DmList();
        Assert.True(Dm.eq(a, a));
        Assert.False(Dm.eq(a, b));                        // lists compare by reference
        Assert.True(Dm.eq(typeof(DmList), typeof(DmList)));
        Assert.False(Dm.eq(typeof(DmList), "/list"));
    }

    [Fact]
    public void TypedEqualityOverloadsAgreeWithTheObjectForm()
    {
        object one = 1f;
        Assert.True(Dm.eq(one, 1));         // (object, float)
        Assert.True(Dm.eq(1, one));         // (float, object)
        Assert.False(Dm.eq((object)null, 0));
        Assert.False(Dm.eq(0, (object)null));
        Assert.True(Dm.eq(2f, 2));          // (float, float)
        float? none = null;
        float? two = 2f;
        Assert.True(Dm.eq(none, none));     // (float?, float?)
        Assert.False(Dm.eq(none, 0));       // (float?, float): null is not 0
        Assert.False(Dm.eq(0, none));       // (float, float?)
        Assert.True(Dm.eq(two, 2));
        Assert.True(Dm.eq(2, two));
        Assert.False(Dm.eq(two, none));
    }

    [Fact]
    public void ValueKindTests()
    {
        Assert.True(Dm.isnum(1f));
        Assert.True(Dm.isnum(1));
        Assert.False(Dm.isnum(null));
        Assert.False(Dm.isnum("1"));
        Assert.True(Dm.istext(""));
        Assert.False(Dm.istext(null));
        Assert.False(Dm.istext(1f));
        Assert.True(Dm.islist(new DmList()));
        Assert.False(Dm.islist(null));
        Assert.False(Dm.islist("list"));
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Contracts;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// One check for each values-owned member that the other files of this folder do not exercise directly: the
/// conversions and loops the contracts package wrote (pinned in depth by tests/Anomaly.Core.Tests/Contracts), the
/// remaining overloads of the compound assignments, and the <c>_char</c> variants (docs/ai/translation-rules.md 3.5, 4.1,
/// 4.2, 4.3, 5.6, 6.5).
/// </summary>
public class MemberCoverageTests
{
    private const string Emoji = "\U0001F600";

    [Fact]
    public void ConversionsAndTruthiness()
    {
        // rules 3.5, 4.1, 4.2; BYOND ref /operator/! and /operator/==.
        Assert.Equal(0f, Dm.num((object)null));
        Assert.Equal(2f, Dm.num((float?)2f));
        Assert.Equal(1f, Dm.num(true));
        Assert.Null(Dm.num_or_null(null));
        Assert.Equal(3f, Dm.num_or_null(3));
        Assert.True(Dm.truthy(1f));
        Assert.False(Dm.truthy((float?)null));
        Assert.False(Dm.truthy(""));
        Assert.True(Dm.truthy(Dm.list()));
        Assert.True(Dm.truthy(typeof(DmList)));
        Assert.True(Dm.truthy((object)"x"));
        Assert.Equal(1f, Dm.not(0f));
        Assert.Equal(0f, Dm.not((float?)5f));
        Assert.Equal(1f, Dm.not((string)null));
        Assert.True(Dm.eq((object)1, 1f));
        Assert.True(Dm.eq(1f, (object)1d));
        Assert.True(Dm.eq(2f, 2f));
        Assert.True(Dm.eq((float?)null, (float?)null));
        Assert.False(Dm.eq((float?)null, 0f));
        Assert.False(Dm.eq(0f, (float?)null));
        Assert.True(Dm.isnum(1f));
        Assert.True(Dm.istext(""));
        Assert.True(Dm.islist(Dm.list()));
    }

    [Fact]
    public void LoopSources()
    {
        // rules 6.5; BYOND ref /proc/for/list (typed loop vars skip other types, as anything keeps every item).
        var atom = new TestAtom();
        DmList L = Dm.list(atom, null, "x");
        Assert.Equal(3, Dm.each(L).Count());
        Assert.Equal(new[] { atom }, Dm.each<TestAtom>(L).ToArray());
        Assert.Throws<DmRuntimeException>(() => Dm.each_cast<TestAtom>(L).ToArray());
        Assert.Equal(new TestAtom[] { atom, null }, Dm.each_cast<TestAtom>(Dm.list(atom, null)).ToArray());
    }

    [Fact]
    public void RemainingCompoundOverloads()
    {
        // rules 4.3's table: a float target with an object operand, a nullable target, an object list target.
        float f = 6;
        Dm.combine(ref f, (object)1f);
        Assert.Equal(7f, f);
        Dm.combine(ref f, (object)null);
        Assert.Equal(7f, f);
        Dm.mask(ref f, (object)3f);
        Assert.Equal(3f, f);
        Dm.bitxor_assign(ref f, (object)1f);
        Assert.Equal(2f, f);
        Dm.bitxor_assign(ref f, (object)null);
        Assert.Equal(2f, f);
        Dm.bitxor_assign(ref f, 7f);
        Assert.Equal(5f, f);
        float? n = 1f;
        Dm.combine(ref n, 2f);
        Assert.Equal(3f, n);
        Dm.mask(ref n, 1f);
        Assert.Equal(1f, n);
        object list = Dm.list(1f, 2f);
        Dm.combine(ref list, Dm.list(2f, 3f));
        V.Items((DmList)list, 1f, 2f, 3f);
        Dm.mask(ref list, Dm.list(3f, 1f));
        V.Items((DmList)list, 1f, 3f);
        Dm.bitxor_assign(ref list, Dm.list(3f, 4f));
        V.Items((DmList)list, 1f, 4f);
        DmList typed = Dm.list(1f);
        Dm.combine(ref typed, 2f);
        Dm.bitxor_assign(ref typed, 1f);
        V.Items(typed, 2f);
        V.Error(() => { float g = 0; Dm.combine(ref g, (object)"x"); });
    }

    [Fact]
    public void CharacterVariants()
    {
        // rules 5.6: the _char procs count characters (Unicode scalar values), the others UTF-16 code units (DV4).
        string s = Emoji + "aB" + Emoji + "ab";
        Assert.Equal(2f, Dm.findtextEx_char(s, "aB"));
        Assert.Equal(5f, Dm.findtextEx_char(s, "ab"));
        Assert.Equal(7f, Dm.findtextEx(s, "ab"));     // UTF-16: each emoji is two positions
        Assert.Equal(5f, Dm.findlasttext_char(s, "AB"));
        Assert.Equal(Emoji + "aB" + Emoji + "xy", Dm.replacetextEx_char(s, "ab", "xy"));
        Assert.Equal(Emoji + "xy" + Emoji + "xy", Dm.replacetext_char(s, "ab", "xy"));
        Assert.Equal(2f, Dm.nonspantext_char(s, "B"));
        Assert.Equal(1f, Dm.spantext_char(s, Emoji));
        Assert.Equal(Emoji, Dm.copytext_char(s, 4, 5));
        V.Items(Dm.splittext_char(s, Emoji), "", "aB", "ab");
        Assert.Equal(97f, Dm.text2ascii_char(s, 2));
        Assert.Equal("ab", Dm.splicetext_char(s, 1, 5));
    }
}

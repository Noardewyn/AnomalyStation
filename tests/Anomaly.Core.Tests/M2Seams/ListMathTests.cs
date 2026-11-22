using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.M2Seams;

/// <summary>
/// BYOND 516's arithmetic over associated values (BYOND ref "values_sum proc", "values_dot proc", "values_cut_under proc",
/// "values_cut_over proc", "values_product proc"; M2 contracts, dm-conformance.md "M2 contracts"): tg's gas mixtures keep
/// their moles in an alist and sum, weigh and prune them with these.
/// </summary>
public class ListMathTests
{
    [Fact]
    public void ValuesSumAddsTheNumbersOnly()
    {
        // The reference's example: list(/obj/crate = 12, /obj/vase = 1)
        Assert.Equal(13f, values_sum(Dm.list(Dm.kv("crate", 12), Dm.kv("vase", 1))));
        Assert.Equal(3.5f, values_sum(Dm.alist(Dm.kv("o2", 1.5f), Dm.kv("n2", 2), Dm.kv("text", "x"), Dm.kv("none", null))));
        Assert.Equal(0f, values_sum(Dm.list(1, 2, 3))); // not associative
        Assert.Equal(0f, values_sum(null));
        Assert.Equal(0f, values_sum(5));
    }

    [Fact]
    public void ValuesProductMultipliesTheNumbersOrGivesOne()
    {
        Assert.Equal(-60f, values_product(Dm.list(Dm.kv("foo", 3), Dm.kv("bar", -4), Dm.kv("banana", 5))));
        Assert.Equal(1f, values_product(Dm.list("a", "b")));
        Assert.Equal(1f, values_product(Dm.list(Dm.kv("a", "x"))));
    }

    [Fact]
    public void ValuesDotIsTheReferencesExample()
    {
        DmList first = Dm.list(Dm.kv("a", 1), Dm.kv("b", 2), Dm.kv("c", 3));
        DmList second = Dm.list(Dm.kv("b", 4), Dm.kv("c", 5), Dm.kv("d", 6));
        Assert.Equal(23f, values_dot(first, second));
        Assert.Equal(23f, values_dot(second, first));
        Assert.Equal(0f, values_dot(first, null));
    }

    [Fact]
    public void ValuesCutUnderRemovesSmallNonNumericAndNaNValues()
    {
        DmList stuff = Dm.list(Dm.kv("a", 1), Dm.kv("b", 2), Dm.kv("c", -4), Dm.kv("d", "text"), Dm.kv("e", float.NaN));
        Assert.Equal(3f, values_cut_under(stuff, 0));
        Assert.Equal(2f, stuff.len);
        Assert.Equal(1, num(stuff["a"]));
        Assert.Equal(2, num(stuff["b"]));

        DmList moles = Dm.alist(Dm.kv("o2", 0.0001f), Dm.kv("n2", 82), Dm.kv("co2", 0.0005f));
        Assert.Equal(1f, values_cut_under(moles, 0.0005f));               // strictly below
        Assert.Equal(1f, values_cut_under(moles, 0.0005f, TRUE));         // inclusive: equal goes too
        Assert.Equal(new object[] { "n2" }, moles.CopyItems());
        Assert.Equal(0f, values_cut_under(Dm.list(1, 2), 5));             // not associative: nothing happens
    }

    [Fact]
    public void ValuesCutOverRemovesLargeValues()
    {
        DmList stuff = Dm.list(Dm.kv("a", 1), Dm.kv("b", 2), Dm.kv("c", -4));
        Assert.Equal(2f, values_cut_over(stuff, 0));
        Assert.Equal(new object[] { "c" }, stuff.CopyItems());
        DmList edge = Dm.list(Dm.kv("a", 1), Dm.kv("b", 2));
        Assert.Equal(1f, values_cut_over(edge, 2, TRUE)); // inclusive: equal to Max goes too
        Assert.Equal(new object[] { "a" }, edge.CopyItems());
    }
}

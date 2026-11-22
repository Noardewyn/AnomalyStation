using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Pilot.Lists.SmokeListsWorld;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// Smoke tests of tg's sorts (code/__HELPERS/sorts/helpers.dm, sort_instance.dm), written from the DM code: timSort,
/// mergeSort and the binary insertion sort are stable (equal elements keep their order, which tg relies on for
/// tie breaks such as the subsystem init order), sort in place and honour the comparator, the associative mode and
/// the index range.
/// </summary>
public class SmokeSortTests
{
    // Items are assoc lists ("priority" = p, "id" = i) sorted with cmp_filter_data_priority (cmp.dm:85), so equal
    // priorities are told apart by their original position.
    private static DmList PriorityItems(IReadOnlyList<int> priorities)
    {
        var items = new object[priorities.Count];
        for (int i = 0; i < priorities.Count; i++)
            items[i] = Dm.list(Dm.kv("priority", (float)priorities[i]), Dm.kv("id", (float)i));
        return Dm.list(items);
    }

    private static float[] Ids(DmList sorted) => Items(sorted).Select(x => Dm.num(((DmList)x)["id"])).ToArray();

    private static float[] StableReference(IReadOnlyList<int> priorities)
        => priorities.Select((p, i) => (p, i)).OrderBy(x => x.p).Select(x => (float)x.i).ToArray();

    public static IEnumerable<object[]> Shapes()
    {
        // Sizes below and above MIN_MERGE (32), random keys with many ties, natural runs (ascending and strictly
        // descending, which countRunAndMakeAscending reverses), and two presorted halves (galloping in mergeLo/Hi).
        var rng = new Random(7);
        foreach (int n in new[] { 2, 5, 31, 32, 33, 64, 65, 200, 1000 })
            yield return new object[] { "random" + n, Enumerable.Range(0, n).Select(_ => rng.Next(0, 10)).ToArray() };
        yield return new object[] { "ascending", Enumerable.Range(0, 300).ToArray() };
        yield return new object[] { "descending", Enumerable.Range(0, 300).Reverse().ToArray() };
        yield return new object[] { "halves", Enumerable.Range(0, 250).Concat(Enumerable.Range(0, 250)).ToArray() };
        yield return new object[] { "halves-reversed", Enumerable.Range(250, 250).Concat(Enumerable.Range(0, 250)).ToArray() };
        yield return new object[] { "sawtooth", Enumerable.Range(0, 500).Select(i => (i * 37) % 23).ToArray() };
        yield return new object[] { "runs", Enumerable.Range(0, 400).Select(i => i % 50 < 25 ? i % 50 : 50 - i % 50).ToArray() };
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void SortTimIsStableAndInPlace(string shape, int[] priorities)
    {
        using var w = new SmokeListsWorld();
        DmList list = PriorityItems(priorities);
        object result = GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_filter_data_priority)));
        Assert.Same(list, result);
        Assert.True(StableReference(priorities).SequenceEqual(Ids(list)), shape);
        Assert.Empty(w.Errors);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void SortMergeIsStable(string shape, int[] priorities)
    {
        using var w = new SmokeListsWorld();
        DmList list = PriorityItems(priorities);
        GlobalProcs.sortMerge(list, ProcRef(nameof(GlobalProcs.cmp_filter_data_priority)));
        Assert.True(StableReference(priorities).SequenceEqual(Ids(list)), shape);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void SortInsertIsStable(string shape, int[] priorities)
    {
        using var w = new SmokeListsWorld();
        DmList list = PriorityItems(priorities);
        GlobalProcs.sortInsert(list, ProcRef(nameof(GlobalProcs.cmp_filter_data_priority)));
        Assert.True(StableReference(priorities).SequenceEqual(Ids(list)), shape);
    }

    [Fact]
    public void TheDefaultComparatorSortsNumbersAscending()
    {
        // sortTim's default cmp is GLOBAL_PROC_REF(cmp_numeric_asc) (helpers.dm:56); cmp_numeric_dsc reverses.
        using var w = new SmokeListsWorld();
        DmList list = NewList(5f, 3f, 9f, 1f, 3f);
        GlobalProcs.sortTim(list);
        Assert.Equal(new object[] { 1f, 3f, 3f, 5f, 9f }, Items(list));
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_numeric_dsc)));
        Assert.Equal(new object[] { 9f, 5f, 3f, 3f, 1f }, Items(list));
    }

    [Fact]
    public void AssociativeSortOrdersKeysByTheirValuesAndKeepsTheAssociations()
    {
        // associative = TRUE sorts by L[L[i]] (sort_instance.dm:2, fetchElement).
        using var w = new SmokeListsWorld();
        DmList list = Dm.list(Dm.kv("a", 3f), Dm.kv("b", 1f), Dm.kv("c", 2f), Dm.kv("d", 1f));
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_numeric_asc)), Dm.TRUE);
        Assert.Equal(new object[] { "b", "d", "c", "a" }, Items(list));
        Assert.Equal(3f, list["a"]);
        Assert.Equal(1f, list["d"]);
    }

    [Fact]
    public void TheIndexRangeSortsOnlyThatSlice()
    {
        // CREATE_SORT_INSTANCE (helpers.dm:15): toIndex is exclusive, 0 means the end; negative indexes wrap.
        using var w = new SmokeListsWorld();
        DmList list = NewList(9f, 8f, 7f, 6f, 5f);
        GlobalProcs.sortTim(list, null, null, 2, 5);
        Assert.Equal(new object[] { 9f, 6f, 7f, 8f, 5f }, Items(list));
        DmList all = NewList(3f, 1f, 2f);
        GlobalProcs.sortTim(all, null, null, 1, 0);
        Assert.Equal(new object[] { 1f, 2f, 3f }, Items(all));
    }

    [Fact]
    public void ShortListsAreReturnedUntouchedAndTheSorterIsCreatedWhenGlobIsEmpty()
    {
        using var w = new SmokeListsWorld();
        DmList one = NewList(1f);
        Assert.Same(one, GlobalProcs.sortTim(one));
        Assert.Null(GlobalProcs.sortTim(null));
        // A missing GLOB.sortInstance makes CREATE_SORT_INSTANCE create a sorter with `new` (helpers.dm:29), which it
        // does not store back into GLOB.
        GlobalProcs.GLOB.sortInstance = null;
        DmList two = NewList(2f, 1f);
        GlobalProcs.sortTim(two);
        Assert.Equal(new object[] { 1f, 2f }, Items(two));
        Assert.Null(GlobalProcs.GLOB.sortInstance);
    }

    [Fact]
    public void SortListCopiesAndSortsTextCaseInsensitively()
    {
        // sort_list (_lists.dm:733) sorts a copy with cmp_text_asc, which is sorttext(b, a): case-insensitive.
        using var w = new SmokeListsWorld();
        DmList source = NewList("banana", "Apple", "cherry", "apple");
        DmList sorted = (DmList)GlobalProcs.sort_list(source);
        Assert.NotSame(source, sorted);
        Assert.Equal(new object[] { "Apple", "apple", "banana", "cherry" }, Items(sorted));
        Assert.Equal(new object[] { "banana", "Apple", "cherry", "apple" }, Items(source));
    }

    [Fact]
    public void MinRunLengthFollowsTimsort()
    {
        // sort_instance.dm:168: n below MIN_MERGE is itself; otherwise between MIN_MERGE/2 and MIN_MERGE.
        using var w = new SmokeListsWorld();
        var sorter = new datum_sort_instance();
        Assert.Equal(31f, Dm.num(sorter.minRunLength(31f)));
        Assert.Equal(16f, Dm.num(sorter.minRunLength(32f)));
        Assert.Equal(17f, Dm.num(sorter.minRunLength(33f)));
        Assert.Equal(32f, Dm.num(sorter.minRunLength(1000f)));
    }
}

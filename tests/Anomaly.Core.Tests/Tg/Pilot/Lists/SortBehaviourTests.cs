using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Pilot.Lists.ListsBehaviourWorld;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// Behaviour of tg's sorts (code/__HELPERS/sorts/helpers.dm, sort_instance.dm) and the sort wrappers of
/// code/__HELPERS/_lists.dm, read from the DM code: exact comparator call sequences on small inputs (hand-traced from
/// the DM procs), stability and correctness against a stable reference sort on random data with fixed seeds, the
/// associative mode, CREATE_SORT_INSTANCE's index normalisation and the shared GLOB.sortInstance.
/// </summary>
public class SortBehaviourTests
{
    /// <summary>
    /// A /datum/disease/advance whose totalResistance() records the call: cmp_advdisease_resistance_asc(A, B)
    /// (cmp.dm:123) is <c>A.totalResistance() - B.totalResistance()</c>, so every comparator call appends A's id,
    /// then B's id, to the log.
    /// </summary>
    private sealed class RecordingDisease : datum_disease_advance
    {
        private readonly List<int> _log;

        public RecordingDisease(int id, float resistance, List<int> log)
        {
            Id = id;
            Resistance = resistance;
            _log = log;
        }

        public int Id { get; }

        public float Resistance { get; }

        public override object totalResistance()
        {
            _log.Add(Id);
            return Resistance;
        }
    }

    /// <summary>A sorter that counts which of tg's sort_instance procs ran (all are virtual slots, rules 7.1).</summary>
    private sealed class CountingSorter : datum_sort_instance
    {
        public readonly Dictionary<string, int> Calls = new();

        private void Count(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

        public override void mergeLo(object base1 = null, object len1 = null, object base2 = null, object len2 = null)
        {
            Count(nameof(mergeLo));
            base.mergeLo(base1, len1, base2, len2);
        }

        public override void mergeHi(object base1 = null, object len1 = null, object base2 = null, object len2 = null)
        {
            Count(nameof(mergeHi));
            base.mergeHi(base1, len1, base2, len2);
        }

        public override object gallopLeft(object key = null, object @base = null, object len = null, object hint = null)
        {
            Count(nameof(gallopLeft));
            return base.gallopLeft(key, @base, len, hint);
        }

        public override object gallopRight(object key = null, object @base = null, object len = null, object hint = null)
        {
            Count(nameof(gallopRight));
            return base.gallopRight(key, @base, len, hint);
        }

        public override void mergeAt(object i = null)
        {
            Count(nameof(mergeAt));
            base.mergeAt(i);
        }

        public override void mergeAt2(object i = null)
        {
            Count(nameof(mergeAt2));
            base.mergeAt2(i);
        }
    }

    private const string Resistance = "/proc/" + nameof(GlobalProcs.cmp_advdisease_resistance_asc);

    private static (DmList list, List<int> log) Diseases(params float[] resistances)
    {
        var log = new List<int>();
        var items = new object[resistances.Length];
        for (int i = 0; i < resistances.Length; i++) items[i] = new RecordingDisease(i + 1, resistances[i], log);
        return (Dm.list(items), log);
    }

    private static int[] Ids(DmList list) => Items(list).Select(x => ((RecordingDisease)x).Id).ToArray();

    private static (int, int)[] Pairs(List<int> log)
    {
        Assert.True(log.Count % 2 == 0, "every comparator call reads two items");
        return Enumerable.Range(0, log.Count / 2).Select(i => (log[2 * i], log[2 * i + 1])).ToArray();
    }

    // ---- exact comparator sequences (hand-traced from sort_instance.dm)

    [Fact]
    public void SortTimOnAShortListMakesTheFirstRunAscendingThenBinaryInserts()
    {
        // resistances 3, 1, 2, 5, 4 (ids 1-5). timSort(1, 6): remaining 5 < MIN_MERGE (:41), so
        // countRunAndMakeAscending(1, 6) (:137) compares (2,1) [<0: descending], (3,2) [>=0: stop] and reverses [1,3);
        // binarySort(1, 6, 3) (:99) then bisects for ids 3, 4 and 5: (1,3) (2,3), (3,4) (1,4), (1,5) (4,5).
        using var w = new ListsBehaviourWorld();
        (DmList list, List<int> log) = Diseases(3f, 1f, 2f, 5f, 4f);
        Assert.Same(list, GlobalProcs.sortTim(list, Resistance));
        Assert.Equal(new[] { (2, 1), (3, 2), (1, 3), (2, 3), (3, 4), (1, 4), (1, 5), (4, 5) }, Pairs(log));
        Assert.Equal(new[] { 2, 3, 1, 5, 4 }, Ids(list));
        Assert.Empty(w.Errors);
    }

    [Fact]
    public void SortTimKeepsEqualItemsInOrderOnTheShortPath()
    {
        // resistances 2, 1, 1: (2,1) is descending, (3,2) is 0 (>= 0 stops the strictly descending run, :151), so only
        // [1,3) is reversed; binarySort then puts id 3 after the equal id 2: (1,3) > 0, (2,3) == 0 moves right.
        using var w = new ListsBehaviourWorld();
        (DmList list, List<int> log) = Diseases(2f, 1f, 1f);
        GlobalProcs.sortTim(list, Resistance);
        Assert.Equal(new[] { (2, 1), (3, 2), (1, 3), (2, 3) }, Pairs(log));
        Assert.Equal(new[] { 2, 3, 1 }, Ids(list));
    }

    [Fact]
    public void SortTimOnAnAscendingRunComparesEachNeighbourOnce()
    {
        // A short list that is already one ascending run: countRunAndMakeAscending walks it (n-1 compares) and
        // binarySort(1, n+1, n+1) has nothing left to insert.
        using var w = new ListsBehaviourWorld();
        (DmList list, List<int> log) = Diseases(1f, 2f, 2f, 3f);
        GlobalProcs.sortTim(list, Resistance);
        Assert.Equal(new[] { (2, 1), (3, 2), (4, 3) }, Pairs(log));
        Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(list));
    }

    [Fact]
    public void SortInsertAndShortSortMergeAreAPlainBinaryInsertion()
    {
        // sortInsert calls binarySort(from, to) with no start (helpers.dm:90): `if(start <= lo)` is true for null
        // (null counts as 0), so start = lo + 1. mergeSort below MIN_MERGE calls binarySort(start, end, start)
        // (:578), the same thing. resistances 3, 1, 2, 5, 4: (1,2), (1,3) (2,3), (3,4) (1,4), (1,5) (4,5).
        var expected = new[] { (1, 2), (1, 3), (2, 3), (3, 4), (1, 4), (1, 5), (4, 5) };
        using (var w = new ListsBehaviourWorld())
        {
            (DmList list, List<int> log) = Diseases(3f, 1f, 2f, 5f, 4f);
            Assert.Same(list, GlobalProcs.sortInsert(list, Resistance));
            Assert.Equal(expected, Pairs(log));
            Assert.Equal(new[] { 2, 3, 1, 5, 4 }, Ids(list));
        }
        using (var w = new ListsBehaviourWorld())
        {
            (DmList list, List<int> log) = Diseases(3f, 1f, 2f, 5f, 4f);
            Assert.Same(list, GlobalProcs.sortMerge(list, Resistance));
            Assert.Equal(expected, Pairs(log));
            Assert.Equal(new[] { 2, 3, 1, 5, 4 }, Ids(list));
        }
    }

    // ---- stability and correctness on random data

    private static DmList Records(IReadOnlyList<int> keys)
    {
        var items = new object[keys.Count];
        for (int i = 0; i < keys.Count; i++) items[i] = Dm.list(Dm.kv("priority", (float)keys[i]), Dm.kv("id", (float)i));
        return Dm.list(items);
    }

    private static int[] RecordIds(DmList sorted) => Items(sorted).Select(x => (int)Dm.num(((DmList)x)["id"])).ToArray();

    // Enumerable.OrderBy is a stable sort: equal keys keep their input order.
    private static int[] StableOrder(IReadOnlyList<int> keys, bool descending = false)
    {
        var indexed = keys.Select((k, i) => (k, i));
        return (descending ? indexed.OrderByDescending(x => x.k) : indexed.OrderBy(x => x.k)).Select(x => x.i).ToArray();
    }

    public static IEnumerable<object[]> RandomInputs()
    {
        // Fixed seeds; sizes on both sides of MIN_MERGE (32) and of the run-length boundaries; few distinct keys (many
        // ties) and many distinct keys.
        foreach (int seed in new[] { 1, 2, 3 })
        {
            foreach (int n in new[] { 2, 3, 7, 31, 32, 33, 63, 64, 65, 128, 257, 1000, 3000 })
            {
                foreach (int distinct in new[] { 3, 1000 })
                {
                    var rng = new Random(seed * 100003 + n * 31 + distinct);
                    yield return new object[] { $"seed{seed}-n{n}-k{distinct}", Enumerable.Range(0, n).Select(_ => rng.Next(distinct)).ToArray() };
                }
            }
        }
    }

    public static IEnumerable<object[]> StructuredInputs()
    {
        // Shapes that make timSort find long natural runs and gallop: presorted blocks, a long run followed by a
        // short one and the reverse (mergeLo and mergeHi), interleaved blocks, strictly descending runs (reversed
        // by countRunAndMakeAscending) and descending runs with ties (not strict: not reversed whole).
        var rng = new Random(99);
        int[] Sorted(int n, int max) => Enumerable.Range(0, n).Select(_ => rng.Next(max)).OrderBy(x => x).ToArray();
        yield return new object[] { "two-sorted-halves", Sorted(500, 200).Concat(Sorted(500, 200)).ToArray() };
        yield return new object[] { "long-then-short", Sorted(900, 1000).Concat(Sorted(60, 1000)).ToArray() };
        yield return new object[] { "short-then-long", Sorted(60, 1000).Concat(Sorted(900, 1000)).ToArray() };
        yield return new object[] { "high-low-blocks", Enumerable.Range(0, 20).SelectMany(b => Enumerable.Range(b % 2 == 0 ? 1000 : 0, 50)).ToArray() };
        yield return new object[] { "many-runs", Enumerable.Range(0, 40).SelectMany(_ => Sorted(rng.Next(1, 80), 300)).ToArray() };
        yield return new object[] { "strictly-descending", Enumerable.Range(0, 700).Reverse().ToArray() };
        yield return new object[] { "descending-with-ties", Enumerable.Range(0, 700).Select(i => (700 - i) / 3).ToArray() };
        yield return new object[] { "all-equal", Enumerable.Repeat(5, 300).ToArray() };
        yield return new object[] { "sawtooth", Enumerable.Range(0, 1200).Select(i => i % 97).ToArray() };
        yield return new object[] { "organ-pipe", Enumerable.Range(0, 600).Select(i => i < 300 ? i : 600 - i).ToArray() };
    }

    [Theory]
    [MemberData(nameof(RandomInputs))]
    [MemberData(nameof(StructuredInputs))]
    public void SortTimIsAStableAscendingSort(string input, int[] keys)
    {
        using var w = new ListsBehaviourWorld();
        DmList list = Records(keys);
        Assert.Same(list, GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_filter_data_priority))));
        Assert.True(StableOrder(keys).SequenceEqual(RecordIds(list)), input);
        Assert.Empty(w.Errors);
    }

    [Theory]
    [MemberData(nameof(RandomInputs))]
    [MemberData(nameof(StructuredInputs))]
    public void SortTimWithADescendingComparatorIsStableToo(string input, int[] keys)
    {
        // cmp_profile_time_dsc (cmp.dm:111) is B[1] - A[1]: descending by the first item.
        using var w = new ListsBehaviourWorld();
        var items = new object[keys.Length];
        for (int i = 0; i < keys.Length; i++) items[i] = DL((float)keys[i], (float)i);
        DmList list = Dm.list(items);
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_profile_time_dsc)));
        int[] ids = Items(list).Select(x => (int)Dm.num(((DmList)x)[2f])).ToArray();
        Assert.True(StableOrder(keys, descending: true).SequenceEqual(ids), input);
    }

    [Theory]
    [MemberData(nameof(RandomInputs))]
    [MemberData(nameof(StructuredInputs))]
    public void SortMergeAndSortInsertAreStableAscendingSorts(string input, int[] keys)
    {
        using (var w = new ListsBehaviourWorld())
        {
            DmList list = Records(keys);
            GlobalProcs.sortMerge(list, ProcRef(nameof(GlobalProcs.cmp_filter_data_priority)));
            Assert.True(StableOrder(keys).SequenceEqual(RecordIds(list)), "sortMerge " + input);
        }
        if (keys.Length > 1000) return; // binary insertion moves O(n^2) items
        using (var w = new ListsBehaviourWorld())
        {
            DmList list = Records(keys);
            GlobalProcs.sortInsert(list, ProcRef(nameof(GlobalProcs.cmp_filter_data_priority)));
            Assert.True(StableOrder(keys).SequenceEqual(RecordIds(list)), "sortInsert " + input);
        }
    }

    [Fact]
    public void TheStructuredInputsReachTheGallopingMerges()
    {
        // The stability theories above are only as good as their coverage of mergeLo, mergeHi and the gallops.
        using var w = new ListsBehaviourWorld(sorter: () => Dm.@new<CountingSorter>());
        var sorter = (CountingSorter)GlobalProcs.GLOB.sortInstance;
        foreach (object[] row in StructuredInputs())
            GlobalProcs.sortTim(Records((int[])row[1]), ProcRef(nameof(GlobalProcs.cmp_filter_data_priority)));
        Assert.True(sorter.Calls.GetValueOrDefault(nameof(datum_sort_instance.mergeLo)) > 0, "mergeLo");
        Assert.True(sorter.Calls.GetValueOrDefault(nameof(datum_sort_instance.mergeHi)) > 0, "mergeHi");
        Assert.True(sorter.Calls.GetValueOrDefault(nameof(datum_sort_instance.gallopLeft)) > 0, "gallopLeft");
        Assert.True(sorter.Calls.GetValueOrDefault(nameof(datum_sort_instance.gallopRight)) > 0, "gallopRight");
        // mergeHi's galloping loop (`count1 = len1 - gallopRight(...)`, :529) runs only when one run keeps winning:
        // more gallops than merges shows that the galloping do-while loops ran, not only mergeAt's two searches.
        Assert.True(sorter.Calls[nameof(datum_sort_instance.gallopRight)] > sorter.Calls[nameof(datum_sort_instance.mergeAt)], "galloping mode");
    }

    [Fact]
    public void ARepeatedSortTimOnTheSharedSorterResetsItsState()
    {
        // timSort cuts runBases and runLens first (:35) and resets minGallop to MIN_GALLOP at the end (:89); after a
        // full merge one run is left on the stack (`//ASSERT(runBases.len == 1)`, :86).
        using var w = new ListsBehaviourWorld();
        var rng = new Random(5);
        for (int round = 0; round < 5; round++)
        {
            int[] keys = Enumerable.Range(0, 400 + round * 37).Select(_ => rng.Next(50)).ToArray();
            DmList list = Records(keys);
            GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_filter_data_priority)));
            Assert.True(StableOrder(keys).SequenceEqual(RecordIds(list)), "round " + round);
            Assert.Equal(7f, GlobalProcs.GLOB.sortInstance.minGallop);
            Assert.Equal(1f, GlobalProcs.GLOB.sortInstance.runBases.len);
            Assert.Equal(1f, GlobalProcs.GLOB.sortInstance.runLens.len);
            AssertDmEqual(keys.Length, GlobalProcs.GLOB.sortInstance.runLens[1f]);
        }
        // The mini-TimSort path (fewer than MIN_MERGE items) returns before pushing any run.
        GlobalProcs.sortTim(Records(new[] { 3, 1, 2 }), ProcRef(nameof(GlobalProcs.cmp_filter_data_priority)));
        Assert.Equal(0f, GlobalProcs.GLOB.sortInstance.runBases.len);
    }

    [Fact]
    public void NumbersSortWithTheDefaultComparatorAndNullCountsAsZero()
    {
        // sortTim's default cmp is cmp_numeric_asc (helpers.dm:56), `a - b`, where null counts as 0.
        using var w = new ListsBehaviourWorld();
        DmList list = DL(3f, null, -1f, 0f, 2.5f, 1);
        GlobalProcs.sortTim(list);
        AssertItems(list, -1f, null, 0f, 1, 2.5f, 3f);   // null (0) stays before the equal 0
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_numeric_dsc)));
        AssertItems(list, 3f, 2.5f, 1, null, 0f, -1f);
    }

    [Fact]
    public void TextSortsCaseInsensitivelyAndTiesKeepTheirOrder()
    {
        // cmp_text_asc is sorttext(b, a) (cmp.dm:7): sorttext is case-insensitive, so "A" and "a" are equal.
        using var w = new ListsBehaviourWorld();
        DmList list = DL("b", "A", "a", "B", "c", "a");
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_text_asc)));
        AssertItems(list, "A", "a", "a", "b", "B", "c");
        DmList dsc = DL("b", "A", "a", "B", "c", "a");
        GlobalProcs.sortTim(dsc, ProcRef(nameof(GlobalProcs.cmp_text_dsc)));
        AssertItems(dsc, "c", "b", "B", "A", "a", "a");
    }

    [Fact]
    public void LongTextListsSortLikeAStableCaseInsensitiveReference()
    {
        using var w = new ListsBehaviourWorld();
        var rng = new Random(77);
        string[] words = Enumerable.Range(0, 600).Select(_ =>
        {
            var chars = new char[rng.Next(1, 4)];
            for (int i = 0; i < chars.Length; i++) chars[i] = (char)((rng.Next(2) == 0 ? 'a' : 'A') + rng.Next(4));
            return new string(chars);
        }).ToArray();
        DmList list = Dm.list(words.Cast<object>().ToArray());
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_text_asc)));
        string[] expected = words.Select((s, i) => (s, i)).OrderBy(x => x.s, StringComparer.OrdinalIgnoreCase).Select(x => x.s).ToArray();
        Assert.Equal(expected, Items(list).Cast<string>().ToArray());
    }

    // ---- associative sorting: fetchElement(L, i) is L[L[i]] (sort_instance.dm:2)

    [Fact]
    public void AnAssociativeSortOrdersTheKeysByTheirValuesStably()
    {
        using var w = new ListsBehaviourWorld();
        var rng = new Random(2024);
        int n = 750;
        int[] values = Enumerable.Range(0, n).Select(_ => rng.Next(12)).ToArray();
        var pairs = new object[n];
        for (int i = 0; i < n; i++) pairs[i] = Dm.kv("k" + i, (float)values[i]);
        DmList list = Dm.list(pairs);
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_numeric_asc)), Dm.TRUE);
        string[] expected = StableOrder(values).Select(i => "k" + i).ToArray();
        Assert.Equal(expected, Items(list).Cast<string>().ToArray());
        for (int i = 0; i < n; i++)
            AssertDmEqual(values[i], list["k" + i]);       // the associations move with their keys
    }

    [Fact]
    public void AssociativeSortMergeAndSortInsertKeepTheAssociations()
    {
        using var w = new ListsBehaviourWorld();
        DmList merge = Dm.list(Dm.kv("a", 3f), Dm.kv("b", 1f), Dm.kv("c", 2f), Dm.kv("d", 1f));
        GlobalProcs.sortMerge(merge, null, Dm.TRUE);
        AssertItems(merge, "b", "d", "c", "a");
        DmList insert = Dm.list(Dm.kv("a", 3f), Dm.kv("b", 1f), Dm.kv("c", 2f), Dm.kv("d", 1f));
        GlobalProcs.sortInsert(insert, ProcRef(nameof(GlobalProcs.cmp_numeric_dsc)), Dm.TRUE);
        AssertItems(insert, "a", "c", "b", "d");
        AssertDmEqual(3f, insert["a"]);
        AssertDmEqual(1f, insert["d"]);
    }

    [Fact]
    public void WithoutTheAssociativeFlagTheKeysThemselvesAreCompared()
    {
        using var w = new ListsBehaviourWorld();
        DmList list = Dm.list(Dm.kv("b", 1f), Dm.kv("a", 2f), Dm.kv("c", 0f));
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_text_asc)));
        AssertItems(list, "a", "b", "c");
        AssertDmEqual(2f, list["a"]);
    }

    // ---- CREATE_SORT_INSTANCE (helpers.dm:15): fromIndex %= len, toIndex %= len + 1, then <= 0 wraps

    [Fact]
    public void ARangeSortsOnlyFromIndexToBeforeToIndex()
    {
        using var w = new ListsBehaviourWorld();
        DmList list = DL(9f, 5f, 4f, 3f, 2f, 1f);
        GlobalProcs.sortTim(list, null, null, 2, 5);
        AssertItems(list, 9f, 3f, 4f, 5f, 2f, 1f);
    }

    [Fact]
    public void NegativeAndOutOfRangeIndexesWrapLikeDmModulo()
    {
        using var w = new ListsBehaviourWorld();
        // fromIndex -2 on 6 items: -2 % 6 == -2, then += 6: the sort starts at 4.
        DmList a = DL(6f, 5f, 4f, 3f, 2f, 1f);
        GlobalProcs.sortTim(a, null, null, -2);
        AssertItems(a, 6f, 5f, 4f, 1f, 2f, 3f);
        // toIndex -1: -1 % 7 == -1, then += 7: 6, so the last item is left out.
        DmList b = DL(6f, 5f, 4f, 3f, 2f, 1f);
        GlobalProcs.sortTim(b, null, null, 1, -1);
        AssertItems(b, 2f, 3f, 4f, 5f, 6f, 1f);
        // fromIndex 8 on 6 items: 8 % 6 == 2.
        DmList c = DL(6f, 5f, 4f, 3f, 2f, 1f);
        GlobalProcs.sortTim(c, null, null, 8);
        AssertItems(c, 6f, 1f, 2f, 3f, 4f, 5f);
        // An explicit fromIndex of 0 is not the default (only null is): 0 % 6 == 0 wraps to 6, the last item alone.
        DmList d = DL(6f, 5f, 4f, 3f, 2f, 1f);
        GlobalProcs.sortTim(d, null, null, 0);
        AssertItems(d, 6f, 5f, 4f, 3f, 2f, 1f);
        // toIndex 7 on 6 items: 7 % 7 == 0 wraps to 7, the end.
        DmList e = DL(6f, 5f, 4f, 3f, 2f, 1f);
        GlobalProcs.sortMerge(e, null, null, 3, 7);
        AssertItems(e, 6f, 5f, 1f, 2f, 3f, 4f);
    }

    [Fact]
    public void ALongRangeIsSortedStablyAndTheRestIsUntouched()
    {
        using var w = new ListsBehaviourWorld();
        var rng = new Random(31);
        int[] keys = Enumerable.Range(0, 500).Select(_ => rng.Next(20)).ToArray();
        DmList list = Records(keys);
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_filter_data_priority)), null, 101, 401);
        int[] expected = Enumerable.Range(0, 100)
            .Concat(StableOrder(keys.Skip(100).Take(300).ToArray()).Select(i => i + 100))
            .Concat(Enumerable.Range(400, 100)).ToArray();
        Assert.Equal(expected, RecordIds(list));
    }

    [Fact]
    public void ShortListsAreReturnedAsTheyAre()
    {
        using var w = new ListsBehaviourWorld();
        Assert.Null(GlobalProcs.sortTim(null));
        Assert.Null(GlobalProcs.sortMerge(null));
        Assert.Null(GlobalProcs.sortInsert(null));
        DmList one = DL("x");
        Assert.Same(one, GlobalProcs.sortTim(one));
        DmList empty = Dm.list();
        Assert.Same(empty, GlobalProcs.sortTim(empty, ProcRef(nameof(GlobalProcs.cmp_text_asc))));
        // `return to_sort` happens before the sorter is touched.
        Assert.Null(GlobalProcs.GLOB.sortInstance.L);
    }

    [Fact]
    public void TheSortsUseGlobSortInstanceOrAThrowAwaySorter()
    {
        using var w = new ListsBehaviourWorld();
        DmList list = Dm.list(Dm.kv("x", 1f), Dm.kv("y", 2f));
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_numeric_dsc)), Dm.TRUE);
        AssertItems(list, "y", "x");
        datum_sort_instance sorter = GlobalProcs.GLOB.sortInstance;
        Assert.Same(list, sorter.L);
        Assert.Equal(ProcRef(nameof(GlobalProcs.cmp_numeric_dsc)), sorter.cmp);
        Assert.Equal(1f, sorter.associative);
        // With GLOB.sortInstance null, `sorter = new` makes one for this call only (helpers.dm:28-29).
        GlobalProcs.GLOB.sortInstance = null;
        DmList other = DL(3f, 1f, 2f);
        GlobalProcs.sortTim(other);
        AssertItems(other, 1f, 2f, 3f);
        Assert.Null(GlobalProcs.GLOB.sortInstance);
    }

    [Fact]
    public void SortTimMacroSortsNothing()
    {
        // SORT_TIM (helpers.dm:2) calls timSort(1, 0) without CREATE_SORT_INSTANCE's normalisation: remaining is -1,
        // countRunAndMakeAscending(1, 0) returns 1 at once and binarySort(1, 0, 2) loops `for(start in 2 to -1)`
        // zero times. It still points the shared sorter at the list.
        using var w = new ListsBehaviourWorld();
        DmList list = DL(3f, 1f, 2f);
        GlobalProcs.SORT_TIM(list, Dm.TRUE);
        AssertItems(list, 3f, 1f, 2f);
        Assert.Same(list, GlobalProcs.GLOB.sortInstance.L);
        Assert.Equal(1f, GlobalProcs.GLOB.sortInstance.associative);
        DmList one = DL(1f);
        GlobalProcs.SORT_TIM(one, Dm.FALSE);
        Assert.Same(list, GlobalProcs.GLOB.sortInstance.L);   // length < 2: untouched
    }

    // ---- the _lists.dm sort wrappers (:725-739)

    [Fact]
    public void SortListSortsACopyAsTextByDefault()
    {
        using var w = new ListsBehaviourWorld();
        DmList source = DL("pear", "Apple", "fig", "apple");
        DmList sorted = (DmList)GlobalProcs.sort_list(source);
        Assert.NotSame(source, sorted);
        AssertItems(sorted, "Apple", "apple", "fig", "pear");
        AssertItems(source, "pear", "Apple", "fig", "apple");
        DmList numbers = (DmList)GlobalProcs.sort_list(DL(3f, 10f, 1f), ProcRef(nameof(GlobalProcs.cmp_numeric_dsc)));
        AssertItems(numbers, 10f, 3f, 1f);
    }

    [Fact]
    public void SortNamesSortsACopyOfAtomsByName()
    {
        using var w = new ListsBehaviourWorld();
        var b = new obj { name = "beta" };
        var a = new obj { name = "Alpha" };
        var c = new mob { name = "gamma" };
        DmList source = DL(b, c, a);
        DmList asc = (DmList)GlobalProcs.sort_names(source);
        Assert.NotSame(source, asc);
        AssertItems(asc, a, b, c);
        AssertItems(GlobalProcs.sort_names(source, -1), c, b, a);
        AssertItems(GlobalProcs.sort_names(source, 0), a, b, c);   // `order >= 0` is ascending
        AssertItems(source, b, c, a);
    }

    [Fact]
    public void SortKeyAndSortRecordSortInPlace()
    {
        // Unlike sort_list and sort_names, these pass the list itself to sortTim.
        using var w = new ListsBehaviourWorld();
        var bob = new client { ckey = "bob" };
        var alice = new client { ckey = "alice" };
        var carl = new client { ckey = "carl" };
        DmList clients = DL(bob, carl, alice);
        Assert.Same(clients, GlobalProcs.sort_key(clients));
        AssertItems(clients, alice, bob, carl);
        GlobalProcs.sort_key(clients, -1);
        AssertItems(clients, carl, bob, alice);

        var r1 = new datum_record { name = "Zed" };
        var r2 = new datum_record { name = "amy" };
        var r3 = new datum_record();                     // the default name "Unknown"
        DmList records = DL(r1, r2, r3);
        Assert.Same(records, GlobalProcs.sort_record(records));
        AssertItems(records, r2, r3, r1);
        GlobalProcs.sort_record(records, -5);
        AssertItems(records, r1, r3, r2);
    }

    [Fact]
    public void SortWithTheEmbedTextComparatorOrdersNumbersAsText()
    {
        // cmp_embed_text_asc (cmp.dm:13) compares "[a]" and "[b]": "10" sorts before "9".
        using var w = new ListsBehaviourWorld();
        DmList list = DL(9f, 10f, 1f, 100f, 2f);
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_embed_text_asc)));
        AssertItems(list, 1f, 10f, 100f, 2f, 9f);
        GlobalProcs.sortTim(list, ProcRef(nameof(GlobalProcs.cmp_embed_text_dsc)));
        AssertItems(list, 9f, 2f, 100f, 10f, 1f);
    }

    [Fact]
    public void EachNewSorterGetsItsOwnRunLists()
    {
        // sort_instance.dm:30-31, var/list/runBases = list(): an object-creating default, run for each new object by
        // the pre-New hook (rules 10.2, dm_init_vars); the prototype that initial() reads has none.
        using var w = new ListsBehaviourWorld();
        datum_sort_instance a = Dm.@new<datum_sort_instance>();
        datum_sort_instance b = Dm.@new<datum_sort_instance>();
        Assert.Empty(Items(a.runBases));
        Assert.Empty(Items(a.runLens));
        Assert.NotSame(a.runBases, b.runBases);
        Assert.NotSame(a.runBases, a.runLens);
        Assert.Null(Dm.initial(a, (datum_sort_instance p) => p.runBases));
    }

    // ---- the sort_instance procs one by one

    private static datum_sort_instance Sorter(DmList list, float associative = 0)
    {
        datum_sort_instance sorter = Dm.@new<datum_sort_instance>();
        sorter.L = list;
        sorter.associative = associative;
        return sorter;
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(31, 31)]   // below MIN_MERGE: n itself
    [InlineData(32, 16)]   // 32 -> 16, no bit shifted off
    [InlineData(33, 17)]   // 33 -> 16 with r = 1
    [InlineData(64, 16)]
    [InlineData(65, 17)]
    [InlineData(100, 25)]
    [InlineData(1000, 32)] // 1000 -> 500 -> 250 -> 125 (r = 1) -> 62 -> 31, + 1
    [InlineData(4096, 16)]
    public void MinRunLengthHalvesUntilBelowMinMergeAndAddsOneIfABitWasLost(int n, int expected)
    {
        // while(n >= MIN_MERGE) { r |= (n & 1); n >>= 1 } return n + r   (sort_instance.dm:169)
        using var w = new ListsBehaviourWorld();
        AssertDmEqual((float)expected, new datum_sort_instance().minRunLength((float)n));
    }

    [Fact]
    public void CountRunReversesOnlyAStrictlyDescendingRun()
    {
        using var w = new ListsBehaviourWorld();
        // Strictly descending 5 > 4 > 3, then 3 >= 3 ends it: [1, 4) is reversed and its length returned.
        DmList strict = DL(5f, 4f, 3f, 3f, 9f);
        AssertDmEqual(3f, Sorter(strict).countRunAndMakeAscending(1f, 6f));
        AssertItems(strict, 3f, 4f, 5f, 3f, 9f);
        // An equal neighbour stops a descending run at once (stability): only 5, 4 are reversed.
        DmList ties = DL(5f, 4f, 4f, 3f);
        AssertDmEqual(2f, Sorter(ties).countRunAndMakeAscending(1f, 5f));
        AssertItems(ties, 4f, 5f, 4f, 3f);
        // An ascending run keeps equal neighbours (a[lo] <= a[lo + 1] <= ...) and is left as it is.
        DmList ascending = DL(1f, 2f, 2f, 1f);
        AssertDmEqual(3f, Sorter(ascending).countRunAndMakeAscending(1f, 5f));
        AssertItems(ascending, 1f, 2f, 2f, 1f);
        // A range of one item is a run of 1 without a comparison.
        AssertDmEqual(1f, Sorter(DL("not a number")).countRunAndMakeAscending(1f, 2f));
        // The range [lo, hi) is respected.
        DmList inner = DL(9f, 3f, 2f, 1f, 0f);
        AssertDmEqual(2f, Sorter(inner).countRunAndMakeAscending(2f, 4f));
        AssertItems(inner, 9f, 2f, 3f, 1f, 0f);
    }

    public static IEnumerable<object[]> GallopInputs()
    {
        var rng = new Random(4711);
        for (int round = 0; round < 12; round++)
        {
            int n = rng.Next(1, 40);
            yield return new object[] { Enumerable.Range(0, n).Select(_ => rng.Next(0, 8)).OrderBy(x => x).ToArray() };
        }
        yield return new object[] { new[] { 1, 2, 2, 2, 3, 5 } };
        yield return new object[] { new[] { 4 } };
        yield return new object[] { new[] { 7, 7, 7, 7, 7, 7, 7, 7, 7 } };
    }

    [Theory]
    [MemberData(nameof(GallopInputs))]
    public void GallopLeftAndRightAreTheLowerAndUpperBoundsWhateverTheHint(int[] sorted)
    {
        // gallopLeft returns the offset of the leftmost element >= key (the LEFTMOST of the equal ones, :250);
        // gallopRight the offset after the rightmost element <= key (`a[b + k - 1] <= key < a[b + k]`, :311).
        // The run is placed at base 3 behind two items that must not be read.
        using var w = new ListsBehaviourWorld();
        DmList list = Dm.list(new object[] { "x", "y" }.Concat(sorted.Select(v => (object)(float)v)).ToArray());
        datum_sort_instance sorter = Sorter(list);
        for (int key = -1; key <= 9; key++)
        {
            int lower = sorted.Count(v => v < key);
            int upper = sorted.Count(v => v <= key);
            for (int hint = 0; hint < sorted.Length; hint++)
            {
                AssertDmEqual((float)lower, sorter.gallopLeft((float)key, 3f, (float)sorted.Length, (float)hint));
                AssertDmEqual((float)upper, sorter.gallopRight((float)key, 3f, (float)sorted.Length, (float)hint));
            }
        }
    }

    [Fact]
    public void GallopingOnAnAssociativeListComparesTheValues()
    {
        // fetchElement(L, i) is L[L[i]] when associative (sort_instance.dm:2).
        using var w = new ListsBehaviourWorld();
        DmList list = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 3f), Dm.kv("c", 3f), Dm.kv("d", 8f));
        datum_sort_instance sorter = Sorter(list, 1);
        AssertDmEqual(1f, sorter.gallopLeft(3f, 1f, 4f, 0f));
        AssertDmEqual(3f, sorter.gallopRight(3f, 1f, 4f, 3f));
    }

    [Fact]
    public void SortMergeDoesNotClearTheRunsLeftOnTheSharedSorter()
    {
        // tg's mergeSort (sort_instance.dm:573) pushes runs onto runBases/runLens without the Cut() that timSort does
        // first (:35), and both leave the final run on the stack. A sortMerge of MIN_MERGE or more items on
        // GLOB.sortInstance after such a sort therefore merges a stale run and reads past the list: a runtime, which
        // the port unwinds to the caller (DV1). tg itself never calls sortMerge, so the bug is latent; it is kept.
        using var w = new ListsBehaviourWorld();
        var rng = new Random(3);
        DmList Numbers() => Dm.list(Enumerable.Range(0, 100).Select(_ => (object)(float)rng.Next(1000)).ToArray());
        DmList first = Numbers();
        GlobalProcs.sortMerge(first);                                        // a fresh sorter: correct
        float[] sorted = Nums(first);
        Assert.Equal(sorted.OrderBy(x => x).ToArray(), sorted);
        Assert.Equal(1f, GlobalProcs.GLOB.sortInstance.runBases.len);       // the run left behind
        Assert.ThrowsAny<DmRuntimeException>(() => GlobalProcs.sortMerge(Numbers()));
        // A short sortTim cuts the stacks and pushes nothing, after which sortMerge works again.
        GlobalProcs.sortTim(DL(2f, 1f));
        DmList again = Numbers();
        GlobalProcs.sortMerge(again);
        Assert.Equal(Nums(again).OrderBy(x => x).ToArray(), Nums(again));
        // A long sortTim leaves its final run too.
        GlobalProcs.sortTim(Numbers());
        Assert.ThrowsAny<DmRuntimeException>(() => GlobalProcs.sortMerge(Numbers()));
    }
}

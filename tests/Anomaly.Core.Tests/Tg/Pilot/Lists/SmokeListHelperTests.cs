using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Pilot.Lists.SmokeListsWorld;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// Smoke tests of the list helper procs of code/__HELPERS/_lists.dm, written from the DM code.
/// </summary>
public class SmokeListHelperTests
{
    [Fact]
    public void EnglishListJoinsWithCommasAndAnd()
    {
        using var w = new SmokeListsWorld();
        Assert.Equal("nothing", GlobalProcs.english_list(Dm.list()));
        Assert.Equal("nothing", GlobalProcs.english_list(null));
        Assert.Equal("a", GlobalProcs.english_list(NewList("a")));
        Assert.Equal("a and b", GlobalProcs.english_list(NewList("a", "b")));
        Assert.Equal("a, b and c", GlobalProcs.english_list(NewList("a", "b", "c")));
        Assert.Equal("a, b, and c", GlobalProcs.english_list(NewList("a", "b", "c"), null, null, null, ","));
        Assert.Equal("1, 2 or 3", GlobalProcs.english_list(NewList(1f, 2f, 3f), "none", " or "));
    }

    [Fact]
    public void ReverseRangeSwapsTheHalfOpenRangeInPlace()
    {
        // _lists.dm:862: end is exclusive, 0 means the end; negative values wrap.
        using var w = new SmokeListsWorld();
        DmList list = NewList(1f, 2f, 3f, 4f, 5f);
        Assert.Same(list, GlobalProcs.reverse_range(list));
        Assert.Equal(new object[] { 5f, 4f, 3f, 2f, 1f }, Items(list));
        DmList part = NewList(1f, 2f, 3f, 4f, 5f);
        GlobalProcs.reverse_range(part, 2, 5);
        Assert.Equal(new object[] { 1f, 4f, 3f, 2f, 5f }, Items(part));
        DmList copy = (DmList)GlobalProcs.reverseList(NewList(1f, 2f));
        Assert.Equal(new object[] { 2f, 1f }, Items(copy));
    }

    [Fact]
    public void MoveElementRotatesAndKeepsAssociations()
    {
        using var w = new SmokeListsWorld();
        DmList list = NewList("a", "b", "c", "d");
        GlobalProcs.move_element(list, 1f, 4f);
        Assert.Equal(new object[] { "b", "c", "a", "d" }, Items(list));
        DmList assoc = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 3f));
        GlobalProcs.move_element(assoc, 3f, 1f);
        Assert.Equal(new object[] { "c", "a", "b" }, Items(assoc));
        Assert.Equal(3f, assoc["c"]);
        Assert.Equal(1f, assoc["a"]);
    }

    [Fact]
    public void MoveRangeAndSwapRange()
    {
        using var w = new SmokeListsWorld();
        DmList list = NewList(1f, 2f, 3f, 4f, 5f, 6f);
        GlobalProcs.move_range(list, 1f, 5f, 2);          // [1,3) to before index 5
        Assert.Equal(new object[] { 3f, 4f, 1f, 2f, 5f, 6f }, Items(list));
        DmList swapped = NewList(1f, 2f, 3f, 4f, 5f, 6f);
        GlobalProcs.swap_range(swapped, 1f, 4f, 2);       // [1,3) with [4,6)
        Assert.Equal(new object[] { 4f, 5f, 3f, 1f, 2f, 6f }, Items(swapped));
    }

    [Fact]
    public void BitfieldToList()
    {
        using var w = new SmokeListsWorld();
        Assert.Equal(new object[] { 1f, 4f }, Items((DmList)GlobalProcs.bitfield_to_list(5)));
        Assert.Equal(new object[] { "a", "c" }, Items((DmList)GlobalProcs.bitfield_to_list(5, NewList("a", "b", "c"))));
        Assert.Empty(Items((DmList)GlobalProcs.bitfield_to_list()));
    }

    [Fact]
    public void PickWeightNeverPicksAZeroWeightAndZeroesNullWeights()
    {
        using var w = new SmokeListsWorld();
        DmList weights = Dm.list(Dm.kv("never", 0f), Dm.kv("null", null), Dm.kv("always", 3f));
        for (int i = 0; i < 50; i++)
            Assert.Equal("always", GlobalProcs.pick_weight(weights));
        Assert.Equal(0f, Dm.num(weights["null"]));         // `list_to_pick[item] = 0` writes the caller's list (a boxed int 0)
        Assert.Null(GlobalProcs.pick_weight(Dm.list()));
        DmList take = Dm.list(Dm.kv("x", 2f));
        Assert.Equal("x", GlobalProcs.pick_weight_take(take));
        Assert.Equal(1f, take["x"]);
        Assert.Equal("b", GlobalProcs.pick_weight_recursive(Dm.list(Dm.kv("a", 0f), Dm.kv(NewList("b"), 1f))));
    }

    [Fact]
    public void PickNShuffleAndPickNTake()
    {
        using var w = new SmokeListsWorld();
        DmList source = NewList(1f, 2f, 3f, 4f, 5f);
        DmList picked = (DmList)GlobalProcs.pick_n(source, 3f);
        Assert.Equal(3, picked.Count);
        Assert.Equal(3, Items(picked).Distinct().Count());
        Assert.All(Items(picked), x => Assert.Contains(x, Items(source)));
        Assert.Equal(5, ((DmList)GlobalProcs.pick_n(source, 10f)).Count);
        Assert.Equal(0, ((DmList)GlobalProcs.pick_n(source, 0f)).Count);

        DmList shuffled = (DmList)GlobalProcs.shuffle(source);
        Assert.NotSame(source, shuffled);
        Assert.Equal(new object[] { 1f, 2f, 3f, 4f, 5f }, Items(source));
        Assert.Equal(Items(source).OrderBy(x => (float)x), Items(shuffled).OrderBy(x => (float)x));

        object took = GlobalProcs.pick_n_take(source);
        Assert.Equal(4, source.Count);
        Assert.DoesNotContain(took, Items(source));
        Assert.Null(GlobalProcs.pick_n_take(Dm.list()));
    }

    [Fact]
    public void StackHelpersAndNeighbours()
    {
        using var w = new SmokeListsWorld();
        DmList stack = NewList("a", "b", "c");
        Assert.Equal("c", GlobalProcs.peek(stack));
        Assert.Equal("c", GlobalProcs.pop(stack));
        Assert.Equal("a", GlobalProcs.popleft(stack));
        Assert.Equal(new object[] { "b" }, Items(stack));
        Assert.Null(GlobalProcs.peek(Dm.list()));
        Assert.Null(GlobalProcs.pop(Dm.list()));

        DmList ring = NewList("a", "b", "c");
        Assert.Equal("b", GlobalProcs.next_list_item("a", ring));
        Assert.Equal("a", GlobalProcs.next_list_item("c", ring));
        Assert.Equal("c", GlobalProcs.previous_list_item("a", ring));
        Assert.Equal("b", GlobalProcs.previous_list_item("c", ring));
    }

    [Fact]
    public void SortedInsertWalksLeftWhileTheComparatorIsPositive()
    {
        // _lists.dm:661: with cmp_numeric_asc(thing, L[pos]) > 0 the list is kept in descending order.
        using var w = new SmokeListsWorld();
        DmList list = NewList(5f, 3f, 1f);
        GlobalProcs.sorted_insert(list, 4f, ProcRef(nameof(GlobalProcs.cmp_numeric_asc)));
        Assert.Equal(new object[] { 5f, 4f, 3f, 1f }, Items(list));
    }

    [Fact]
    public void SetOperations()
    {
        using var w = new SmokeListsWorld();
        // first - second removes the last occurrence of each item of second (BYOND list -).
        Assert.Equal(new object[] { 1f, 2f, 3f }, Items((DmList)GlobalProcs.difflist(NewList(1f, 2f, 2f, 3f), NewList(2f))));
        Assert.Equal(new object[] { 1f, 3f }, Items((DmList)GlobalProcs.difflist(NewList(1f, 2f, 2f, 3f), NewList(2f), 1)));
        Assert.Null(GlobalProcs.difflist(null, NewList(1f)));
        Assert.Equal(new object[] { 1f, 3f }, Items((DmList)GlobalProcs.unique_merge_list(NewList(1f, 2f), NewList(2f, 3f))));
        Assert.Equal(new object[] { 1f, 3f }, Items((DmList)GlobalProcs.unique_merge_list(NewList(1f, 2f, 2f), NewList(2f, 3f), 1)));

        DmList inner = NewList(9f);
        DmList unique = (DmList)GlobalProcs.unique_list(NewList(1f, 1f, inner, 2f, inner));
        Assert.Equal(new object[] { 1f, inner, 2f }, Items(unique));

        DmList inPlace = Dm.list(Dm.kv("a", 2f), 5f, "a", 5f);
        GlobalProcs.unique_list_in_place(inPlace);
        Assert.Equal(new object[] { "a", 5f }, Items(inPlace));
        Assert.Equal(2f, inPlace["a"]);
    }

    [Fact]
    public void CounterLists()
    {
        using var w = new SmokeListsWorld();
        DmList counter = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 3f));
        Assert.Equal(4f, Dm.num(GlobalProcs.counterlist_sum(counter)));
        DmList scaled = (DmList)GlobalProcs.counterlist_scale(counter, 2f);
        Assert.Equal(6f, scaled["b"]);
        DmList normal = (DmList)GlobalProcs.counterlist_normalise(counter);
        Assert.Equal(0.25f, Dm.num(normal["a"]));
        DmList zero = Dm.list(Dm.kv("a", 0f));
        Assert.Same(zero, GlobalProcs.counterlist_normalise(zero));
        GlobalProcs.counterlist_combine(counter, Dm.list(Dm.kv("b", 1f), Dm.kv("c", 2f)));
        Assert.Equal(4f, counter["b"]);
        Assert.Equal(2f, counter["c"]);

        DmList blended = (DmList)GlobalProcs.blend_weighted_lists(Dm.list(Dm.kv("a", 1f)), Dm.list(Dm.kv("b", 1f)), 0.25f);
        Assert.Equal(750f, blended["a"]);
        Assert.Equal(250f, blended["b"]);
    }

    [Fact]
    public void WeightsExpandByTheirGreatestCommonFactor()
    {
        using var w = new SmokeListsWorld();
        Assert.Equal(4f, GlobalProcs.greatest_common_factor(NewList(4f, 8f, 12f)));
        Assert.Equal(1f, GlobalProcs.greatest_common_factor(NewList(3f, 4f)));
        DmList expanded = (DmList)GlobalProcs.expand_weights(Dm.list(Dm.kv("a", 2f), Dm.kv("b", 4f), Dm.kv("c", 0f)));
        Assert.Equal(new object[] { "a", "b", "b" }, Items(expanded));
        DmList ones = (DmList)GlobalProcs.fill_with_ones(Dm.list("a", Dm.kv("b", 2f)));
        Assert.Equal(1f, Dm.num(ones["a"]));               // the literal 1 is stored as written (rules 3.1: a boxed int)
        Assert.Equal(2f, ones["b"]);
        Assert.Equal(5f, GlobalProcs.fill_with_ones(5f));
    }

    [Fact]
    public void CopiesAndComparisons()
    {
        using var w = new SmokeListsWorld();
        DmList inner = NewList(1f, 2f);
        DmList outer = Dm.list(Dm.kv("k", inner), inner);
        DmList copy = (DmList)GlobalProcs.deep_copy_list(outer);
        Assert.NotSame(inner, copy["k"]);
        Assert.Equal(new object[] { 1f, 2f }, Items((DmList)copy["k"]));
        Assert.Equal(1f, GlobalProcs.deep_compare_list(outer, copy));
        ((DmList)copy["k"]).Add(3f);
        Assert.Equal(0f, GlobalProcs.deep_compare_list(outer, copy));
        Assert.Equal(1f, GlobalProcs.deep_compare_list(null, null));
        Assert.Equal("x", GlobalProcs.deep_copy_list("x"));

        DmList alt = (DmList)GlobalProcs.deep_copy_list_alt(Dm.list(Dm.kv("a", Dm.list(Dm.kv("b", "c")))));
        Assert.Equal("c", ((DmList)alt["a"])["b"]);

        Assert.Equal(1f, GlobalProcs.compare_list(NewList(1f, "a"), NewList(1f, "a")));
        Assert.Equal(0f, GlobalProcs.compare_list(NewList(1f, "a"), NewList("a", 1f)));
        Assert.Equal(0f, GlobalProcs.compare_list(NewList(1f), null));
    }

    [Fact]
    public void AssocConversions()
    {
        using var w = new SmokeListsWorld();
        DmList used = Dm.list();
        Assert.Equal("x", GlobalProcs.avoid_assoc_duplicate_keys("x", used));
        Assert.Equal("x (2)", GlobalProcs.avoid_assoc_duplicate_keys("x", used));
        Assert.Equal("x (3)", GlobalProcs.avoid_assoc_duplicate_keys("x", used));
        Assert.Null(GlobalProcs.avoid_assoc_duplicate_keys(null, used));

        DmList inner = NewList(1f);
        DmList assoc = Dm.list(Dm.kv("a", 1f), Dm.kv("b", inner), Dm.kv("c", 1f));
        Assert.Equal(new object[] { "a", "b", "c" }, Items((DmList)GlobalProcs.assoc_to_keys(assoc)));
        Assert.Equal(new object[] { 1f, inner }, Items((DmList)GlobalProcs.assoc_to_values(assoc)));
        Assert.Null(GlobalProcs.assoc_to_values(null));
        DmList made = (DmList)GlobalProcs.make_associative(NewList("p", "q"));
        Assert.Equal(1f, made["q"]);
        Assert.Equal(2f, GlobalProcs.count_by_type(NewList(new obj_item_fish(), new obj_item(), new mob()), typeof(obj_item)));
        Assert.Equal(1f, GlobalProcs.list_clear_nulls(NewList(null, 1f, null)));
        Assert.Equal(0f, GlobalProcs.list_clear_nulls(NewList(1f)));
    }

    [Fact]
    public void RecursivePicksAndResolves()
    {
        using var w = new SmokeListsWorld();
        Assert.Equal("a", GlobalProcs.pick_recursive(NewList(NewList("a"))));
        Assert.Equal("b", GlobalProcs.pick_recursive("b"));
        DmList resolved = (DmList)GlobalProcs.recursive_list_resolve(Dm.list(Dm.kv("t", NewList(5f)), 4f));
        Assert.Equal(new object[] { 5f }, Items((DmList)resolved["t"]));
        Assert.Equal(4f, resolved[2]);                    // `. += list(x)` adds x itself
        Assert.Equal("x", GlobalProcs.DEFAULTPICK(NewList("x"), "d"));
        Assert.Equal("d", GlobalProcs.DEFAULTPICK(Dm.list(), "d"));
        Assert.Equal(new object[] { 1f, 2f }, Items((DmList)GlobalProcs.LAZY_LISTS_OR(NewList(1f), NewList(2f))));
        Assert.Null(GlobalProcs.LAZY_LISTS_OR(null, Dm.list()));
    }

    [Fact]
    public void TypeCachesFollowTheTypeTree()
    {
        // typecacheof (_lists.dm:356) over the skeleton types of this wave: /obj, /obj/item, /obj/item/fish, ...
        using var w = new SmokeListsWorld();
        DmList cache = (DmList)GlobalProcs.typecacheof(typeof(obj_item));
        Assert.Equal(1f, cache[typeof(obj_item)]);
        Assert.Equal(1f, cache[typeof(obj_item_fish)]);
        Assert.Null(cache[typeof(obj)]);
        DmList root = (DmList)GlobalProcs.typecacheof(typeof(obj_item), 1);
        Assert.Equal(new object[] { typeof(obj_item) }, Items(root));
        DmList sub = (DmList)GlobalProcs.typecacheof(typeof(obj_item), 0, 1);
        Assert.DoesNotContain(typeof(obj_item), Items(sub));
        Assert.Contains(typeof(obj_item_bodypart), Items(sub));
        DmList fromList = (DmList)GlobalProcs.typecacheof(NewList(typeof(obj_machinery), typeof(mob)));
        Assert.Equal(1f, fromList[typeof(obj_machinery_camera)]);
        Assert.Equal(1f, fromList[typeof(mob_living)]);
        Assert.Null(GlobalProcs.typecacheof(null));
        Assert.Throws<DmCrashException>(() => GlobalProcs.typecacheof("text"));

        // GAP(M3 P1 -> lead): /obj/machinery and its cameras stand for /obj and /obj/item since the generated classes made
        // /obj/item 16k types: tg's clear_nulls loop removes key by key (_lists.dm:433), O(n^2) on the shim's list.
        DmList zebra = (DmList)GlobalProcs.zebra_typecacheof(Dm.list(Dm.kv(typeof(obj_machinery), "o"), Dm.kv(typeof(obj_machinery_camera), null)), null, null, null, 1);
        Assert.Equal("o", zebra[typeof(obj_machinery_door)]);
        Assert.DoesNotContain(typeof(obj_machinery_camera), Items(zebra));
        DmList single = (DmList)GlobalProcs.zebra_typecacheof(typeof(obj_machinery), 7);
        Assert.Equal(7f, single[typeof(obj_machinery_camera)]);

        Assert.True(Dm.truthy(GlobalProcs.is_type_in_typecache(new obj_item_fish(), cache)));
        Assert.True(Dm.truthy(GlobalProcs.is_type_in_typecache(typeof(obj_item_fish), cache)));
        Assert.False(Dm.truthy(GlobalProcs.is_type_in_typecache(new mob(), cache)));
        Assert.Null(GlobalProcs.is_type_in_typecache(null, cache));

        DmList atoms = NewList(new obj_item_fish(), new mob(), new obj_item());
        Assert.Equal(2, ((DmList)GlobalProcs.typecache_filter_list(atoms, cache)).Count);
        Assert.Single(Items((DmList)GlobalProcs.typecache_filter_list_reverse(atoms, cache)));
        Assert.Single(Items((DmList)GlobalProcs.typecache_filter_multi_list_exclusion(atoms, cache, GlobalProcs.typecacheof(typeof(obj_item_fish)) as DmList)));
    }

    [Fact]
    public void TypeAndPathInList()
    {
        using var w = new SmokeListsWorld();
        var fish = new obj_item_fish();
        Assert.Equal(1f, GlobalProcs.is_type_in_list(fish, NewList(typeof(mob), typeof(obj_item))));
        Assert.Equal(0f, GlobalProcs.is_type_in_list(fish, NewList(typeof(mob))));
        Assert.Equal(0f, GlobalProcs.is_type_in_list(null, NewList(typeof(obj))));
        // zebra: the value of the closest matching type, unless return_first_match
        DmList zebra = Dm.list(Dm.kv(typeof(obj), "obj"), Dm.kv(typeof(obj_item), "item"));
        Assert.Equal("item", GlobalProcs.is_type_in_list(fish, zebra, 1));
        Assert.Equal("obj", GlobalProcs.is_type_in_list(fish, zebra, 1, 1));
        Assert.Equal("item", GlobalProcs.is_type_in_list(new obj_item(), zebra, 1));

        Assert.Equal(1f, GlobalProcs.is_path_in_list(typeof(obj_item_fish), NewList(typeof(obj_item))));
        Assert.Equal("obj", GlobalProcs.is_path_in_list(typeof(obj_item_fish), zebra, 1)); // the first match, not the closest
        Assert.Equal(0f, GlobalProcs.is_path_in_list(typeof(mob), NewList(typeof(obj_item))));
    }

    [Fact]
    public void TypeEnglishListReadsInitialNames()
    {
        using var w = new SmokeListsWorld();
        Assert.Equal("nothing", GlobalProcs.type_english_list(Dm.list()));
        // BYOND's default name of an atom type is its path's last element (BYOND ref /atom/var/name; the engine parts).
        Assert.Equal("obj and mob", GlobalProcs.type_english_list(NewList(typeof(obj), typeof(mob))));
    }

    [Fact]
    public void AssertSortedReportsAndReturns()
    {
        // stack_trace (rules 9.5) reports the runtime and returns: assert_sorted goes on to the end of the list.
        using var w = new SmokeListsWorld();
        GlobalProcs.assert_sorted(NewList(1f, 3f, 2f, 0f), "nums");
        Assert.Equal(2, w.Errors.Count);
        Assert.Contains("nums is not sorted. value at 3 (2)", w.Errors[0].Message);
        GlobalProcs.assert_sorted(NewList(1f, 2f), "fine");
        Assert.Equal(2, w.Errors.Count);
    }

    [Fact]
    public void FindRecordSearchesTheCrewOrTheLockedRecords()
    {
        using var w = new SmokeListsWorld();
        var crew = new datum_record_crew { name = "Ann" };
        var locked = new datum_record_locked { name = "Ann" };
        GlobalProcs.GLOB.manifest = new datum_manifest { general = NewList(new datum_record_crew { name = "Bob" }, crew), locked = NewList(locked) };
        Assert.Same(crew, GlobalProcs.find_record("Ann"));
        Assert.Same(locked, GlobalProcs.find_record("Ann", 1));
        Assert.Null(GlobalProcs.find_record("Cid"));
    }

    [Fact]
    public void NotTranslatedHelpersSayWhy()
    {
        using var w = new SmokeListsWorld();
        Assert.Throws<DmNotTranslatedException>(() => GlobalProcs.coords2turf(NewList(1f, 1f, 1f)));
        Assert.Throws<DmNotTranslatedException>(() => GlobalProcs.get_element_by_var(Dm.list(), "x", 1f));
        Assert.Throws<DmNotTranslatedException>(() => GlobalProcs.kvpify_list(Dm.list()));
    }
}

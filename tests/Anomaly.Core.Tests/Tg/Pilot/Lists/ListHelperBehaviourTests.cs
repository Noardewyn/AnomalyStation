using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Pilot.Lists.ListsBehaviourWorld;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// Behaviour of the helper procs of code/__HELPERS/_lists.dm, with the expected values worked out from the DM code
/// (each test names the proc and its line). The random helpers are checked by replaying the world RNG: the same
/// seed in a second world, drawing exactly what the DM proc draws (rand(L, H), in DM order), must give the same
/// result, which pins the algorithm and the order and number of draws.
/// </summary>
public class ListHelperBehaviourTests
{
    /// <summary>An /obj whose compile-time name (its prototype's, what initial() reads) is set by its constructor.</summary>
    private sealed class Widget : obj
    {
        public Widget() { name = "widget"; }
    }

    private sealed class Gadget : obj
    {
        public Gadget() { name = "gadget"; }
    }

    /// <summary>Runs <paramref name="draw"/> in a fresh world of seed <paramref name="seed"/> (the replay).</summary>
    private static T Replay<T>(ulong seed, Func<T> draw)
    {
        using var w = new ListsBehaviourWorld(seed);
        return draw();
    }

    // ---- english_list (:243), type_english_list (:265)

    [Fact]
    public void EnglishListFollowsItsSwitch()
    {
        using var w = new ListsBehaviourWorld();
        Assert.Equal("nothing", GlobalProcs.english_list(null));
        Assert.Equal("none", GlobalProcs.english_list(Dm.list(), "none"));
        Assert.Equal("a", GlobalProcs.english_list(DL("a")));
        Assert.Equal("a and b", GlobalProcs.english_list(DL("a", "b")));
        Assert.Equal("a, b and c", GlobalProcs.english_list(DL("a", "b", "c")));
        Assert.Equal("a, b, c and d", GlobalProcs.english_list(DL("a", "b", "c", "d")));
        // final_comma_text replaces comma_text only after the next-to-last item.
        Assert.Equal("a; b; c, and d", GlobalProcs.english_list(DL("a", "b", "c", "d"), null, null, "; ", ","));
        Assert.Equal("a or b", GlobalProcs.english_list(DL("a", "b"), null, " or "));
        // Items are embedded as "[x]": numbers in DM format, null as "".
        Assert.Equal("1.5, 2 and 0.25", GlobalProcs.english_list(DL(1.5f, 2f, 0.25f)));
        Assert.Equal(" and ", GlobalProcs.english_list(DL(null, null)));
        // Explicit nulls take the defaults (DM defaults apply to null arguments, rules 7.2).
        Assert.Equal("x and y", GlobalProcs.english_list(DL("x", "y"), null, null, null, null));
    }

    /// <summary>
    /// An /obj that the shim renders as an atom. The skeleton /atom of this branch does not implement IDmAtom yet (its
    /// engine part comes with the atom wave), so "[atom]" would render as its type path until then.
    /// </summary>
    private sealed class Thing : obj, IDmAtom
    {
        public Thing(string thing_name) { name = thing_name; }

        long IDmObject.dm_id => 0;

        void IDmObject.dm_assign_id(long id) { }

        void IDmObject.dm_pre_new(DmArgs args) { }

        DmList IDmAtom.contents => Dm.list();

        string IDmAtom.name => name;

        string IDmAtom.gender => "neuter";
    }

    [Fact]
    public void EnglishListOfAtomsGetsDmsArticles()
    {
        // Each item is embedded on its own ("[input[index]][comma_text]"), and DM gives an embedded atom with an
        // improper name "the ", or "The " at the start of the text, and a proper (capitalized) name nothing
        // (BYOND ref "text"): so every item that starts a piece of text is capitalized, even inside the sentence.
        using var w = new ListsBehaviourWorld();
        var sword = new Thing("sword");
        var shield = new Thing("shield");
        var bob = new Thing("Bob");
        Assert.Equal("The sword", GlobalProcs.english_list(DL(sword)));
        Assert.Equal("The sword and the shield", GlobalProcs.english_list(DL(sword, shield)));
        Assert.Equal("Bob and the sword", GlobalProcs.english_list(DL(bob, sword)));
        Assert.Equal("The sword, The shield and Bob", GlobalProcs.english_list(DL(sword, shield, bob)));
        Assert.Equal("Bob, The sword and the shield", GlobalProcs.english_list(DL(bob, sword, shield)));
    }

    [Fact]
    public void TypeEnglishListUsesTheInitialNamesOfTheTypes()
    {
        using var w = new ListsBehaviourWorld();
        Assert.Equal("widget and gadget", GlobalProcs.type_english_list(DL(typeof(Widget), typeof(Gadget))));
        Assert.Equal("widget, gadget, or widget", GlobalProcs.type_english_list(DL(typeof(Widget), typeof(Gadget), typeof(Widget)), null, " or ", null, ","));
        Assert.Equal("nothing", GlobalProcs.type_english_list(Dm.list()));
        Assert.Equal("none", GlobalProcs.type_english_list(null, "none"));
    }

    // ---- is_type_in_list (:285), is_path_in_list (:314)

    [Fact]
    public void IsTypeInListWithoutZebraIsAnyMatch()
    {
        using var w = new ListsBehaviourWorld();
        var fish = new obj_item_fish();
        AssertDmEqual(1f, GlobalProcs.is_type_in_list(fish, DL(typeof(mob), typeof(obj))));
        AssertDmEqual(0f, GlobalProcs.is_type_in_list(fish, DL(typeof(mob), typeof(obj_machinery))));
        AssertDmEqual(0f, GlobalProcs.is_type_in_list(fish, Dm.list()));
        AssertDmEqual(0f, GlobalProcs.is_type_in_list(fish, null));
        AssertDmEqual(0f, GlobalProcs.is_type_in_list(null, DL(typeof(obj))));
    }

    [Fact]
    public void IsTypeInListWithZebraReturnsTheValueOfTheClosestType()
    {
        using var w = new ListsBehaviourWorld();
        var fish = new obj_item_fish();
        // The closest (most derived) matching type wins whatever the order: `ispath(type, highest_matched_type)`.
        Assert.Equal("item", GlobalProcs.is_type_in_list(fish, Dm.list(Dm.kv(typeof(obj), "obj"), Dm.kv(typeof(obj_item), "item")), 1));
        Assert.Equal("item", GlobalProcs.is_type_in_list(fish, Dm.list(Dm.kv(typeof(obj_item), "item"), Dm.kv(typeof(obj), "obj")), 1));
        // An exact type match returns at once.
        Assert.Equal("fish", GlobalProcs.is_type_in_list(fish, Dm.list(Dm.kv(typeof(obj), "obj"), Dm.kv(typeof(obj_item_fish), "fish"), Dm.kv(typeof(obj_item), "item")), 1));
        // return_first_match: the first matching entry's value.
        Assert.Equal("obj", GlobalProcs.is_type_in_list(fish, Dm.list(Dm.kv(typeof(mob), "mob"), Dm.kv(typeof(obj), "obj"), Dm.kv(typeof(obj_item), "item")), 1, 1));
        // No match: `. = FALSE`.
        AssertDmEqual(0f, GlobalProcs.is_type_in_list(fish, Dm.list(Dm.kv(typeof(mob), "mob")), 1));
        // A falsy value for the closest type is returned as it is.
        Assert.Null(GlobalProcs.is_type_in_list(fish, Dm.list(Dm.kv(typeof(obj), "obj"), typeof(obj_item)), 1));
    }

    [Fact]
    public void IsPathInListReturnsTrueOrTheFirstMatchingValue()
    {
        using var w = new ListsBehaviourWorld();
        // `return !zebra || list_to_check[path]`: the first entry that the path derives from decides.
        AssertDmEqual(1f, GlobalProcs.is_path_in_list(typeof(obj_item_fish), DL(typeof(mob), typeof(obj_item))));
        AssertDmEqual(1f, GlobalProcs.is_path_in_list(typeof(obj_item), DL(typeof(obj_item))));
        AssertDmEqual(0f, GlobalProcs.is_path_in_list(typeof(obj), DL(typeof(obj_item))));
        DmList zebra = Dm.list(Dm.kv(typeof(obj), "obj"), Dm.kv(typeof(obj_item), "item"));
        Assert.Equal("obj", GlobalProcs.is_path_in_list(typeof(obj_item_fish), zebra, 1));
        Assert.Null(GlobalProcs.is_path_in_list(typeof(obj_item), DL(typeof(obj_item)), 1));   // no value: null
        AssertDmEqual(0f, GlobalProcs.is_path_in_list(null, DL(typeof(obj))));
        AssertDmEqual(0f, GlobalProcs.is_path_in_list(typeof(obj), null));
        // An instance is not a path: ispath() is false for it.
        AssertDmEqual(0f, GlobalProcs.is_path_in_list(new obj_item(), DL(typeof(obj_item))));
    }

    // ---- typecacheof (:356), zebra_typecacheof (:402), the typecache filters (:326-346)

    private static Type[] Types(object list) => Items(list).Cast<Type>().ToArray();

    [Fact]
    public void TypecacheOfAPathIsItsTypesofSetToTrue()
    {
        using var w = new ListsBehaviourWorld();
        DmList cache = (DmList)GlobalProcs.typecacheof(typeof(obj));
        Assert.Equal(Types(Dm.typesof(typeof(obj))), Types(cache));
        Assert.All(Types(cache), t => AssertDmEqual(1f, cache[t]));
        Assert.Contains(typeof(obj_item_fish), Types(cache));
        Assert.DoesNotContain(typeof(mob), Types(cache));
        Assert.Equal(new[] { typeof(obj_item) }, Types(GlobalProcs.typecacheof(typeof(obj_item), 1)));
        Assert.Equal(Types(GlobalProcs.subtypesof(typeof(obj))), Types(GlobalProcs.typecacheof(typeof(obj), 0, 1)));
        Assert.DoesNotContain(typeof(obj), Types(GlobalProcs.typecacheof(typeof(obj), 0, 1)));
        Assert.Null(GlobalProcs.typecacheof(null));
    }

    [Fact]
    public void TypecacheOfAListUnitesItsPaths()
    {
        using var w = new ListsBehaviourWorld();
        DmList cache = (DmList)GlobalProcs.typecacheof(DL(typeof(obj_machinery), typeof(mob)));
        Assert.Equal(Types(Dm.typesof(typeof(obj_machinery))).Concat(Types(Dm.typesof(typeof(mob)))), Types(cache));
        Assert.Equal(new[] { typeof(obj_machinery), typeof(mob) }, Types(GlobalProcs.typecacheof(DL(typeof(obj_machinery), typeof(mob)), 1)));
        Assert.Equal(Types(GlobalProcs.subtypesof(typeof(obj_machinery))).Concat(Types(GlobalProcs.subtypesof(typeof(mob)))),
            Types(GlobalProcs.typecacheof(DL(typeof(obj_machinery), typeof(mob)), 0, 1)));
        // An overlapping path keeps each type once (a key is set, not appended).
        Assert.Equal(Types(Dm.typesof(typeof(obj))), Types(GlobalProcs.typecacheof(DL(typeof(obj), typeof(obj_item)))));
        Assert.Equal(0, ((DmList)GlobalProcs.typecacheof(Dm.list())).Count);
    }

    [Fact]
    public void TypecacheOfSomethingElseCrashes()
    {
        using var w = new ListsBehaviourWorld();
        DmCrashException e = Assert.Throws<DmCrashException>(() => GlobalProcs.typecacheof("text"));
        Assert.Contains("Tried to create a typecache of text which is neither a typepath nor a list.", e.Message);
        Assert.Throws<DmCrashException>(() => GlobalProcs.typecacheof(5f));
        Assert.Throws<DmCrashException>(() => GlobalProcs.zebra_typecacheof(new obj()));
    }

    [Fact]
    public void ZebraTypecacheOfGivesSubtypesTheirLatestAncestorsValue()
    {
        using var w = new ListsBehaviourWorld();
        DmList cache = (DmList)GlobalProcs.zebra_typecacheof(Dm.list(Dm.kv(typeof(obj), "o"), Dm.kv(typeof(obj_item), "i")));
        Assert.Equal(Types(Dm.typesof(typeof(obj))), Types(cache));
        foreach (Type t in Types(cache))
            Assert.Equal(Dm.ispath(t, typeof(obj_item)) ? "i" : "o", cache[t]);
        // Later entries override earlier ones, so a parent listed after its subtype wins over it.
        DmList reversed = (DmList)GlobalProcs.zebra_typecacheof(Dm.list(Dm.kv(typeof(obj_item), "i"), Dm.kv(typeof(obj), "o")));
        Assert.All(Types(reversed), t => Assert.Equal("o", reversed[t]));
    }

    [Fact]
    public void ZebraTypecacheOfModes()
    {
        using var w = new ListsBehaviourWorld();
        // GAP(M3 P1 -> lead): /obj/machinery and its cameras stand for /obj and /obj/item since the generated classes made
        // /obj/item 16k types: tg's clear_nulls loop removes key by key (_lists.dm:433), O(n^2) on the shim's list.
        DmList withNulls = Dm.list(Dm.kv(typeof(obj_machinery), 1f), Dm.kv(typeof(obj_machinery_camera), null));
        DmList kept = (DmList)GlobalProcs.zebra_typecacheof(withNulls);
        Assert.Contains(typeof(obj_machinery_camera_xray), Types(kept));     // the key is there, with a null value
        Assert.Null(kept[typeof(obj_machinery_camera_xray)]);
        DmList cleared = (DmList)GlobalProcs.zebra_typecacheof(withNulls, null, null, null, 1);
        Assert.DoesNotContain(typeof(obj_machinery_camera), Types(cleared));
        Assert.DoesNotContain(typeof(obj_machinery_camera_xray), Types(cleared));
        Assert.Contains(typeof(obj_machinery_door), Types(cleared));

        DmList roots = (DmList)GlobalProcs.zebra_typecacheof(Dm.list(Dm.kv(typeof(obj), "o"), Dm.kv(typeof(mob), "m")), null, 1);
        Assert.Equal(new[] { typeof(obj), typeof(mob) }, Types(roots));
        Assert.Equal("m", roots[typeof(mob)]);
        DmList subs = (DmList)GlobalProcs.zebra_typecacheof(Dm.list(Dm.kv(typeof(obj_machinery), "x")), null, null, 1);
        Assert.Equal(Types(GlobalProcs.subtypesof(typeof(obj_machinery))), Types(subs));

        // A single path takes single_value; a null single_value takes the default TRUE (DM defaults apply to null),
        // so the `isnull(single_value)` branch (:407) cannot be reached.
        DmList single = (DmList)GlobalProcs.zebra_typecacheof(typeof(obj_machinery), 5);
        Assert.All(Types(single), t => AssertDmEqual(5f, single[t]));
        DmList defaulted = (DmList)GlobalProcs.zebra_typecacheof(typeof(obj_machinery), null);
        Assert.All(Types(defaulted), t => AssertDmEqual(1f, defaulted[t]));
        Assert.Equal(new[] { typeof(mob) }, Types(GlobalProcs.zebra_typecacheof(typeof(mob), 3, 1)));
        Assert.Null(GlobalProcs.zebra_typecacheof(null));
    }

    [Fact]
    public void TypecacheFiltersTestTheExactTypeOfEachAtom()
    {
        using var w = new ListsBehaviourWorld();
        var item = new obj_item();
        var fish = new obj_item_fish();
        var man = new mob();
        var cam = new obj_machinery_camera();
        DmList atoms = DL(item, fish, man, cam, fish);
        DmList cache = Dm.list(Dm.kv(typeof(obj_item), Dm.TRUE), Dm.kv(typeof(obj_item_fish), Dm.TRUE), Dm.kv(typeof(mob), Dm.FALSE));
        AssertItems(GlobalProcs.typecache_filter_list(atoms, cache), item, fish, fish);
        AssertItems(GlobalProcs.typecache_filter_list_reverse(atoms, cache), man, cam);
        DmList exclude = Dm.list(Dm.kv(typeof(obj_item_fish), Dm.TRUE));
        AssertItems(GlobalProcs.typecache_filter_multi_list_exclusion(atoms, cache, exclude), item);
        Assert.Equal(0, ((DmList)GlobalProcs.typecache_filter_list(null, cache)).Count);
    }

    // ---- list_clear_empty_weakrefs (:456), special_list_filter (:1027)

    [Fact]
    public void ListClearEmptyWeakrefsRemovesTheWeakrefsThatNoLongerResolve()
    {
        // _lists.dm:456-461: every /datum/weakref in the list whose resolve() is false is removed; other items stay;
        // the result is whether the list got shorter.
        using var w = new ListsBehaviourWorld();
        Dm.@new<datum_controller_subsystem_garbage>(); // SSgarbage, as the MC would create it (NEW_SS_GLOBAL)
        datum live = Dm.@new<datum>();
        datum gone = Dm.@new<datum>();
        object liveRef = GlobalProcs.WEAKREF(live);
        object goneRef = GlobalProcs.WEAKREF(gone);
        GlobalProcs.qdel(gone);
        DmList L = DL(liveRef, "text", goneRef, null, goneRef);
        AssertDmEqual(1f, GlobalProcs.list_clear_empty_weakrefs(L));
        Assert.Equal(new object[] { liveRef, "text", null }, Items(L));
        AssertDmEqual(0f, GlobalProcs.list_clear_empty_weakrefs(L));
        Assert.Empty(w.Errors);
    }

    /// <summary>A callback target that accepts numbers and lists.</summary>
    private sealed class NumberOrListFilter : datum
    {
        public object accept(object x) => Dm.num(Dm.isnum(x) || Dm.islist(x));
    }

    [Fact]
    public void SpecialListFilterKeepsTheItemsTheCallbackAcceptsWithoutDuplicates()
    {
        // _lists.dm:1027-1033: a non-list, an empty list or a condition that is no callback give list(); otherwise
        // . |= LIST_VALUE_WRAP_LISTS(i) for each i whose condition.Invoke(i) is true (a list item is added as one item).
        using var w = new ListsBehaviourWorld();
        datum_callback accept = Dm.@new<datum_callback>(Dm.@new<NumberOrListFilter>(), nameof(NumberOrListFilter.accept));
        DmList inner = DL(7f);
        object result = Waiting.NoSleep(GlobalProcs.special_list_filter(DL(1f, "a", 2f, 1f, inner), accept));
        Assert.Equal(new object[] { 1f, 2f, inner }, Items(result));
        Assert.Empty(Items(Waiting.NoSleep(GlobalProcs.special_list_filter(Dm.list(), accept))));
        Assert.Empty(Items(Waiting.NoSleep(GlobalProcs.special_list_filter(null, accept))));
        Assert.Empty(Items(Waiting.NoSleep(GlobalProcs.special_list_filter(DL(1f), null))));
        Assert.Empty(w.Errors);
    }

    // ---- list_clear_nulls (:448), difflist (:468), unique_merge_list (:485)

    [Fact]
    public void ListClearNullsRemovesEveryNullInPlace()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = DL(null, 1f, null, "a", null);
        AssertDmEqual(1f, GlobalProcs.list_clear_nulls(L));
        AssertItems(L, 1f, "a");
        AssertDmEqual(0f, GlobalProcs.list_clear_nulls(L));
        AssertDmEqual(0f, GlobalProcs.list_clear_nulls(DL(0f, "")));      // 0 and "" are not null
    }

    [Fact]
    public void DifflistSubtractsOrFiltersUniquely()
    {
        using var w = new ListsBehaviourWorld();
        DmList first = DL(1f, 2f, 3f, 2f, 4f, 1f);
        DmList second = DL(2f, 5f);
        // `first - second`: removes the last occurrence of each item of second.
        AssertItems(GlobalProcs.difflist(first, second), 1f, 2f, 3f, 4f, 1f);
        // skiprep: the first occurrence of each item of first that is not in second.
        AssertItems(GlobalProcs.difflist(first, second, 1), 1f, 3f, 4f);
        AssertItems(first, 1f, 2f, 3f, 2f, 4f, 1f);
        DmList inner = DL(9f);
        AssertItems(GlobalProcs.difflist(DL(inner, inner), Dm.list(), 1), inner);   // UNTYPED_LIST_ADD keeps a list whole
        Assert.Null(GlobalProcs.difflist(first, null));
        Assert.Null(GlobalProcs.difflist(null, second));
    }

    [Fact]
    public void UniqueMergeListIsTheSymmetricDifference()
    {
        using var w = new ListsBehaviourWorld();
        // `first ^ second`: the items only in first, then the items only in second.
        AssertItems(GlobalProcs.unique_merge_list(DL(1f, 2f, 3f), DL(3f, 4f, 0f)), 1f, 2f, 4f, 0f);
        // skiprep: difflist(first, second, 1) + difflist(second, first, 1).
        AssertItems(GlobalProcs.unique_merge_list(DL(1f, 1f, 2f, 4f), DL(2f, 3f, 3f), 1), 1f, 4f, 3f);
        Assert.Null(GlobalProcs.unique_merge_list(DL(1f), null));
        Assert.Null(GlobalProcs.unique_merge_list(null, DL(1f)));
    }

    // ---- pick_weight (:505), pick_weight_recursive (:543), pick_weight_take (:557)

    [Fact]
    public void PickWeightDrawsOnceAndWalksTheCumulativeWeights()
    {
        // total = rand(1, total); then each nonzero weight is subtracted in order until total <= 0.
        string[] keys = { "a", "zero", "b", "c" };
        float[] weights = { 3f, 0f, 1f, 6f };
        for (ulong seed = 1; seed <= 60; seed++)
        {
            float r = Replay(seed, () => Dm.rand(1f, 10f));
            string expected = r <= 3 ? "a" : r <= 4 ? "b" : "c";
            object picked = Replay(seed, () =>
            {
                var pairs = keys.Select((k, i) => (object)Dm.kv(k, weights[i])).ToArray();
                return GlobalProcs.pick_weight(Dm.list(pairs));
            });
            Assert.Equal(expected, picked);
        }
    }

    [Fact]
    public void PickWeightZeroesMissingWeightsAndGivesNullWhenNothingCanBePicked()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("a", null), "b", Dm.kv("c", 2f));
        for (int i = 0; i < 20; i++)
            Assert.Equal("c", GlobalProcs.pick_weight(L));
        AssertDmEqual(0f, L["a"]);                       // `list_to_pick[item] = 0` writes the caller's list
        AssertDmEqual(0f, L["b"]);
        Assert.Contains("a", Items(L));
        Assert.Null(GlobalProcs.pick_weight(Dm.list()));
        Assert.Null(GlobalProcs.pick_weight(null));
        Assert.Null(GlobalProcs.pick_weight(Dm.list(Dm.kv("x", 0f), Dm.kv("y", 0f))));
    }

    [Fact]
    public void PickWeightTakeDecrementsThePickedWeight()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("a", 2f), Dm.kv("b", 0f));
        Assert.Equal("a", GlobalProcs.pick_weight_take(L));
        AssertDmEqual(1f, L["a"]);
        Assert.Equal("a", GlobalProcs.pick_weight_take(L));
        AssertDmEqual(0f, L["a"]);
        AssertDmEqual(0f, L["b"]);
    }

    [Fact]
    public void PickWeightRecursiveDescendsIntoPickedLists()
    {
        // pick_weight(fill_with_ones(L)) until the result is not a list; fill_with_ones gives every entry without a
        // true weight (none, or 0) the weight 1.
        // Replay: rand(1, 1 + 1) over list(x = 1, inner = 1), then rand(1, 2) inside the inner list(y, z).
        for (ulong seed = 1; seed <= 30; seed++)
        {
            (float r1, float r2) = Replay(seed, () => { float a = Dm.rand(1f, 2f); return (a, a == 2f ? Dm.rand(1f, 2f) : 0f); });
            string expected = r1 == 1f ? "x" : r2 == 1f ? "y" : "z";
            Assert.Equal(expected, Replay(seed, () => GlobalProcs.pick_weight_recursive(Dm.list("x", DL("y", "z")))));
        }
    }

    // ---- pick_n (:565), fill_with_ones (:587), expand_weights (:603), greatest_common_factor (:625), pick_n_take (:637)

    private static object[] ReplayShuffle(object[] items)
    {
        // shuffle_inplace (:699): for(var/i in 1 to len - 1) Swap(i, rand(i, len))
        var result = (object[])items.Clone();
        for (int i = 1; i <= result.Length - 1; i++)
        {
            int j = (int)Dm.rand(i, result.Length);
            (result[i - 1], result[j - 1]) = (result[j - 1], result[i - 1]);
        }
        return result;
    }

    [Fact]
    public void PickNIsTheFirstNOfAShuffledCopy()
    {
        object[] source = { "a", "b", "c", "d", "e", "f" };
        for (ulong seed = 1; seed <= 20; seed++)
        {
            object[] expected = Replay(seed, () => ReplayShuffle(source)).Take(4).ToArray();
            DmList original = Dm.list(source);
            object picked = Replay(seed, () => GlobalProcs.pick_n(original, 4f));
            Assert.Equal(expected, Items(picked));
            Assert.Equal(source, Items(original));       // the input list is not shuffled
        }
        using var w = new ListsBehaviourWorld();
        Assert.Equal(6, Items(GlobalProcs.pick_n(Dm.list(source), 99f)).Length);  // n = min(n, length)
        Assert.Empty(Items(GlobalProcs.pick_n(Dm.list(source), 0f)));
        Assert.Empty(Items(GlobalProcs.pick_n(Dm.list(source), -3f)));
        Assert.Empty(Items(GlobalProcs.pick_n(Dm.list(), 2f)));
        Assert.Empty(Items(GlobalProcs.pick_n(null, 2f)));
    }

    [Fact]
    public void FillWithOnesGivesWeightOneToEveryFalsyValue()
    {
        using var w = new ListsBehaviourWorld();
        DmList filled = (DmList)GlobalProcs.fill_with_ones(Dm.list("a", Dm.kv("b", 2f), Dm.kv("c", 0f), Dm.kv("d", "w")));
        AssertItems(filled, "a", "b", "c", "d");
        AssertDmEqual(1f, filled["a"]);
        AssertDmEqual(2f, filled["b"]);
        AssertDmEqual(1f, filled["c"]);
        Assert.Equal("w", filled["d"]);
        Assert.Equal("not a list", GlobalProcs.fill_with_ones("not a list"));
        Assert.Null(GlobalProcs.fill_with_ones(null));
    }

    [Fact]
    public void ExpandWeightsRepeatsEachItemByItsWeightOverTheGreatestCommonFactor()
    {
        using var w = new ListsBehaviourWorld();
        DmList inner = DL(1f);
        DmList expanded = (DmList)GlobalProcs.expand_weights(Dm.list(Dm.kv("a", 2f), Dm.kv("b", 4f), Dm.kv("none", 0f), Dm.kv(inner, 6f)));
        AssertItems(expanded, "a", "b", "b", inner, inner, inner);
        AssertItems(GlobalProcs.expand_weights(Dm.list(Dm.kv("x", 3f), Dm.kv("y", 2f))), "x", "x", "x", "y", "y");
    }

    [Fact]
    public void GreatestCommonFactorCountsDownFromTheSmallestValue()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(3f, GlobalProcs.greatest_common_factor(DL(6f, 9f, 12f)));
        AssertDmEqual(2f, GlobalProcs.greatest_common_factor(DL(4f, 6f)));
        AssertDmEqual(7f, GlobalProcs.greatest_common_factor(DL(7f)));
        AssertDmEqual(1f, GlobalProcs.greatest_common_factor(DL(5f, 7f)));
        AssertDmEqual(5f, GlobalProcs.greatest_common_factor(DL(10f, 5f, 15f)));
        // A smallest value below 1 runs no iteration (`for(i in 0 to 1 step -1)`), so the proc returns null.
        Assert.Null(GlobalProcs.greatest_common_factor(DL(0f, 4f)));
    }

    [Fact]
    public void PickNTakeRemovesTheRandomlyChosenPosition()
    {
        object[] source = { "a", "b", "c", "d" };
        for (ulong seed = 1; seed <= 20; seed++)
        {
            int at = (int)Replay(seed, () => Dm.rand(1f, 4f));
            DmList L = Dm.list(source);
            object taken = Replay(seed, () => GlobalProcs.pick_n_take(L));
            Assert.Equal(source[at - 1], taken);
            Assert.Equal(source.Where((_, i) => i != at - 1).ToArray(), Items(L));
        }
        using var w = new ListsBehaviourWorld();
        Assert.Null(GlobalProcs.pick_n_take(Dm.list()));
    }

    // ---- pop (:645), peek (:651), popleft (:656), sorted_insert (:661)

    [Fact]
    public void StackProcs()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list("a", Dm.kv("b", 2f), "c");
        Assert.Equal("c", GlobalProcs.peek(L));
        AssertItems(L, "a", "b", "c");
        Assert.Equal("c", GlobalProcs.pop(L));
        AssertItems(L, "a", "b");
        Assert.Equal("a", GlobalProcs.popleft(L));
        AssertItems(L, "b");
        AssertDmEqual(2f, L["b"]);
        Assert.Equal("b", GlobalProcs.pop(L));
        Assert.Equal(0, L.Count);
        Assert.Null(GlobalProcs.pop(L));
        Assert.Null(GlobalProcs.popleft(L));
        Assert.Null(GlobalProcs.peek(L));
        Assert.Null(GlobalProcs.peek(null));             // length(null) is 0
    }

    [Fact]
    public void SortedInsertWalksFromTheEndWhileTheComparatorIsPositive()
    {
        // `while(pos > 0 && call(comparator)(thing, L[pos]) > 0) pos--` then Insert(pos + 1, thing): with
        // cmp_numeric_asc the list stays in descending order and an equal value goes after its equals.
        using var w = new ListsBehaviourWorld();
        string asc = ProcRef(nameof(GlobalProcs.cmp_numeric_asc));
        DmList L = Dm.list();
        foreach (float x in new[] { 3f, 1f, 4f, 1f, 5f, 9f, 2f, 6f })
            GlobalProcs.sorted_insert(L, x, asc);
        AssertItems(L, 9f, 6f, 5f, 4f, 3f, 2f, 1f, 1f);
        DmList texts = DL("b", "d");
        GlobalProcs.sorted_insert(texts, "c", ProcRef(nameof(GlobalProcs.cmp_text_dsc)));
        AssertItems(texts, "b", "c", "d");
    }

    // ---- next_list_item (:668), previous_list_item (:678)

    [Fact]
    public void NeighboursWrapAround()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = DL("a", "b", "c");
        Assert.Equal("b", GlobalProcs.next_list_item("a", L));
        Assert.Equal("a", GlobalProcs.next_list_item("c", L));
        Assert.Equal("a", GlobalProcs.next_list_item("missing", L));   // Find gives 0, then i++
        Assert.Equal("c", GlobalProcs.previous_list_item("a", L));
        Assert.Equal("a", GlobalProcs.previous_list_item("b", L));
        Assert.Equal("x", GlobalProcs.next_list_item("x", DL("x")));
        // A missing item: Find gives 0, `i--` makes -1, and L[-1] is out of bounds.
        Assert.ThrowsAny<DmRuntimeException>(() => GlobalProcs.previous_list_item("missing", L));
        // The first occurrence of a duplicate counts.
        Assert.Equal("b", GlobalProcs.next_list_item("a", DL("a", "b", "a")));
    }

    // ---- shuffle (:688), shuffle_inplace (:699)

    [Fact]
    public void ShuffleIsAFisherYatesWalkOnACopy()
    {
        object[] source = { 1f, 2f, 3f, 4f, 5f, 6f, 7f };
        for (ulong seed = 1; seed <= 20; seed++)
        {
            object[] expected = Replay(seed, () => ReplayShuffle(source));
            DmList original = Dm.list(source);
            object shuffled = Replay(seed, () => GlobalProcs.shuffle(original));
            Assert.NotSame(original, shuffled);
            Assert.Equal(expected, Items(shuffled));
            Assert.Equal(source, Items(original));

            DmList inPlace = Dm.list(source);
            Assert.Same(inPlace, Replay(seed, () => GlobalProcs.shuffle_inplace(inPlace)));
            Assert.Equal(expected, Items(inPlace));
        }
        using var w = new ListsBehaviourWorld();
        Assert.Null(GlobalProcs.shuffle(null));
        Assert.Null(GlobalProcs.shuffle_inplace(null));
        DmList one = DL("x");
        Assert.Equal(new object[] { "x" }, Items(GlobalProcs.shuffle(one)));
    }

    [Fact]
    public void ShuffleKeepsTheAssociations()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 3f), Dm.kv("d", 4f));
        DmList s = (DmList)GlobalProcs.shuffle(L);
        foreach (string k in new[] { "a", "b", "c", "d" })
            AssertDmEqual(L[k], s[k]);
    }

    // ---- unique_list (:709), unique_list_in_place (:715)

    [Fact]
    public void UniqueListKeepsFirstOccurrencesAndListsWhole()
    {
        using var w = new ListsBehaviourWorld();
        DmList inner = DL(1f, 2f);
        DmList twin = DL(1f, 2f);
        DmList result = (DmList)GlobalProcs.unique_list(DL(3f, inner, "a", 3f, inner, twin, "a", null, null));
        AssertItems(result, 3f, inner, "a", twin, null);   // lists compare by reference
        Assert.Same(inner, result[2f]);
    }

    [Fact]
    public void UniqueListInPlaceKeepsTheAssociations()
    {
        // temp = Copy(); len = 0; numbers are |= added, other keys get `inserted_list[key] = temp[key]`.
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("a", 1f), "b", 3f, "a", 3f, Dm.kv("c", "v"), 4f);
        GlobalProcs.unique_list_in_place(L);
        AssertItems(L, "a", "b", 3f, "c", 4f);
        AssertDmEqual(1f, L["a"]);
        Assert.Null(L["b"]);
        Assert.Equal("v", L["c"]);
    }

    // ---- bitfield_to_list (:741), count_by_type (:762), find_record (:778)

    [Fact]
    public void BitfieldToListReadsTwentyFourBits()
    {
        using var w = new ListsBehaviourWorld();
        AssertItems(GlobalProcs.bitfield_to_list(0b1011), 1f, 2f, 8f);
        Assert.Empty(Items(GlobalProcs.bitfield_to_list()));
        Assert.Equal(24, Items(GlobalProcs.bitfield_to_list(0xFFFFFF)).Length);
        AssertDmEqual(8388608f, Items(GlobalProcs.bitfield_to_list(0xFFFFFF))[23]);
        // DM bit operations are 24-bit (rules 4.4): of 2^24 + 2 only bit 1 is seen.
        AssertItems(GlobalProcs.bitfield_to_list(16777218f), 2f);
        DmList words = DL("w1", "w2", "w3", "w4");
        AssertItems(GlobalProcs.bitfield_to_list(0b1010, words), "w2", "w4");
        AssertItems(GlobalProcs.bitfield_to_list(0b0101, words), "w1", "w3");
        Assert.Empty(Items(GlobalProcs.bitfield_to_list(0b10000, words)));   // more bits than words: ignored
        // At most 24 words are read.
        DmList many = Dm.list(Enumerable.Range(1, 30).Select(i => (object)("w" + i)).ToArray());
        Assert.Equal(24, Items(GlobalProcs.bitfield_to_list(0xFFFFFF, many)).Length);
    }

    [Fact]
    public void CountByTypeCountsInstancesOfTheTypeAndItsSubtypes()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = DL(new obj_item(), new obj_item_fish(), new mob(), null, 5f, "obj", typeof(obj_item), new obj_item_bodypart());
        AssertDmEqual(3f, GlobalProcs.count_by_type(L, typeof(obj_item)));
        AssertDmEqual(4f, GlobalProcs.count_by_type(L, typeof(atom_movable)));
        AssertDmEqual(0f, GlobalProcs.count_by_type(L, typeof(obj_machinery)));
        AssertDmEqual(0f, GlobalProcs.count_by_type(null, typeof(obj)));
    }

    [Fact]
    public void FindRecordComparesNamesExactly()
    {
        using var w = new ListsBehaviourWorld();
        var first = new datum_record_crew { name = "Jane Doe" };
        var second = new datum_record_crew { name = "Jane Doe" };
        var locked = new datum_record_locked { name = "Jane Doe" };
        GlobalProcs.GLOB.manifest = new datum_manifest
        {
            general = DL(new datum_record_crew { name = "jane doe" }, first, second, new datum_record_locked { name = "Max" }),
            locked = DL(new datum_record_crew { name = "Max" }, locked),
        };
        Assert.Same(first, GlobalProcs.find_record("Jane Doe"));              // the first match
        Assert.Same(locked, GlobalProcs.find_record("Jane Doe", 1));
        Assert.Null(GlobalProcs.find_record("JANE DOE"));                     // `!=` is case-sensitive
        // The typed loops skip records of the other kind (for(var/datum/record/crew/target in ...)).
        Assert.Null(GlobalProcs.find_record("Max"));
        Assert.Null(GlobalProcs.find_record("Max", 1));
    }

    // ---- move_element (:801), move_range (:817), swap_range (:840), reverse_range (:862)

    private static DmList Letters(string s) => Dm.list(s.Select(c => (object)c.ToString()).ToArray());

    private static string Joined(DmList L) => string.Concat(Items(L).Cast<string>());

    [Theory]
    [InlineData("abcd", 1, 3, "bacd")]
    [InlineData("abcd", 1, 5, "bcda")]
    [InlineData("abcd", 4, 1, "dabc")]
    [InlineData("abcd", 2, 5, "acdb")]
    [InlineData("abcd", 3, 2, "acbd")]
    [InlineData("abcd", 2, 2, "abcd")]
    [InlineData("abcd", 2, 3, "abcd")]
    public void MoveElementRotatesTheRange(string start, int from, int to, string expected)
    {
        // Insert(to, null); Swap(from, to); Cut(from, from + 1), with from nudged right when from > to.
        using var w = new ListsBehaviourWorld();
        DmList L = Letters(start);
        GlobalProcs.move_element(L, (float)from, (float)to);
        Assert.Equal(expected, Joined(L));
    }

    [Fact]
    public void MoveElementKeepsAssociations()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 3f));
        GlobalProcs.move_element(L, 1f, 4f);
        AssertItems(L, "b", "c", "a");
        AssertDmEqual(1f, L["a"]);
        AssertDmEqual(3f, L["c"]);
    }

    [Theory]
    [InlineData("123456", 1, 5, 2, "341256")]  // len < distance, from < to
    [InlineData("123456", 5, 1, 2, "561234")]  // len < distance, from > to
    [InlineData("123456", 4, 2, 3, "145623")]  // len >= distance, from > to: shift the in-between items right
    [InlineData("123456", 2, 3, 2, "123456")]  // len >= distance, from <= to: nothing to do
    [InlineData("123456", 1, 7, 1, "234561")]
    public void MoveRange(string start, int from, int to, int len, string expected)
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Letters(start);
        GlobalProcs.move_range(L, (float)from, (float)to, len);
        Assert.Equal(expected, Joined(L));
    }

    [Theory]
    [InlineData("123456", 1, 4, 2, "453126")]  // no overlap: pairwise swaps
    [InlineData("123456", 4, 1, 2, "453126")]  // the same with the arguments the other way round
    [InlineData("123456", 1, 2, 3, "412356")]  // overlap, from < to
    [InlineData("123456", 3, 2, 3, "134526")]  // overlap, from > to
    [InlineData("123456", 2, 5, 1, "153426")]
    public void SwapRange(string start, int from, int to, int len, string expected)
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Letters(start);
        GlobalProcs.swap_range(L, (float)from, (float)to, len);
        Assert.Equal(expected, Joined(L));
    }

    [Theory]
    [InlineData("12345", null, null, "54321")]
    [InlineData("12345", 2f, 5f, "14325")]
    [InlineData("12345", -1f, null, "12354")]  // -1 % 5 == -1, += 5: start 4
    [InlineData("12345", 1f, -1f, "43215")]    // -1 % 6 == -1, += 6, then --end: up to 4
    [InlineData("12345", 0f, null, "12345")]   // an explicit 0 wraps to 5: one item
    [InlineData("12345", 6f, null, "54321")]   // 6 % 5 == 1
    [InlineData("12345", 3f, 3f, "12345")]
    public void ReverseRangeNormalisesLikeDmModulo(string start, float? from, float? to, string expected)
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Letters(start);
        Assert.Same(L, GlobalProcs.reverse_range(L, from, to));
        Assert.Equal(expected, Joined(L));
    }

    [Fact]
    public void ReverseRangeOfAnEmptyListAndOfNull()
    {
        using var w = new ListsBehaviourWorld();
        DmList empty = Dm.list();
        Assert.Same(empty, GlobalProcs.reverse_range(empty));
        Assert.ThrowsAny<Exception>(() => GlobalProcs.reverse_range(null));  // `inserted_list.len` of null
    }

    // ---- deep_copy_list (:892), deep_copy_list_alt (:911)

    [Fact]
    public void DeepCopyListCopiesNestedListsAsItemsKeysAndValues()
    {
        using var w = new ListsBehaviourWorld();
        DmList item = DL(1f, DL(2f));
        DmList keyList = DL("k");
        DmList value = DL("v");
        DmList source = Dm.list(item, Dm.kv("t", value), Dm.kv(keyList, "kv"), 5f);
        DmList copy = (DmList)GlobalProcs.deep_copy_list(source);
        Assert.NotSame(source, copy);
        Assert.Equal(4, copy.Count);
        DmList itemCopy = (DmList)copy[1f];
        Assert.NotSame(item, itemCopy);
        Assert.NotSame(item[2f], itemCopy[2f]);          // recursively
        AssertDmEqual(2f, ((DmList)itemCopy[2f])[1f]);
        Assert.NotSame(value, copy["t"]);
        AssertItems(copy["t"], "v");
        DmList keyCopy = (DmList)copy[3f];
        Assert.NotSame(keyList, keyCopy);
        Assert.Equal("kv", copy[keyCopy]);               // the value moved to the copied key
        Assert.Null(copy[keyList]);
        AssertDmEqual(5f, copy[4f]);
        Assert.Equal("x", GlobalProcs.deep_copy_list("x"));
        Assert.Null(GlobalProcs.deep_copy_list(null));
    }

    [Fact]
    public void DeepCopyListAltCopiesOnlyAssociatedValues()
    {
        // It skips numbers and keys without a true value, so a list item without a value is shared, not copied.
        using var w = new ListsBehaviourWorld();
        DmList item = DL(1f);
        DmList deep = Dm.list(Dm.kv("b", "c"));
        DmList source = Dm.list(item, Dm.kv("a", Dm.list(deep)), Dm.kv("n", 0f));
        DmList copy = (DmList)GlobalProcs.deep_copy_list_alt(source);
        Assert.NotSame(source, copy);
        Assert.Same(item, copy[1f]);
        DmList aCopy = (DmList)copy["a"];
        Assert.NotSame(source["a"], aCopy);
        Assert.Same(deep, aCopy[1f]);                    // list(deep): deep is an item without a value, shared
        AssertDmEqual(0f, copy["n"]);
        Assert.Equal(5f, GlobalProcs.deep_copy_list_alt(5f));
    }

    // ---- avoid_assoc_duplicate_keys (:927), make_associative (:938)

    [Fact]
    public void AvoidAssocDuplicateKeysNumbersTheRepeats()
    {
        using var w = new ListsBehaviourWorld();
        DmList used = Dm.list();
        Assert.Equal("Bob", GlobalProcs.avoid_assoc_duplicate_keys("Bob", used));
        Assert.Equal("Ann", GlobalProcs.avoid_assoc_duplicate_keys("Ann", used));
        Assert.Equal("Bob (2)", GlobalProcs.avoid_assoc_duplicate_keys("Bob", used));
        Assert.Equal("Bob (3)", GlobalProcs.avoid_assoc_duplicate_keys("Bob", used));
        AssertDmEqual(3f, used["Bob"]);
        AssertDmEqual(1f, used["Ann"]);
        Assert.Null(GlobalProcs.avoid_assoc_duplicate_keys("", used));
        Assert.Null(GlobalProcs.avoid_assoc_duplicate_keys("Bob", null));
    }

    [Fact]
    public void MakeAssociativeMapsEachItemToTrue()
    {
        using var w = new ListsBehaviourWorld();
        var o = new obj();
        DmList made = (DmList)GlobalProcs.make_associative(DL("a", o, "a", typeof(mob)));
        AssertItems(made, "a", o, typeof(mob));
        AssertDmEqual(1f, made["a"]);
        AssertDmEqual(1f, made[o]);
        Assert.Equal(0, ((DmList)GlobalProcs.make_associative(null)).Count);
        // `.[thing] = TRUE` with a number is a positional write past the end of the new list.
        Assert.ThrowsAny<DmRuntimeException>(() => GlobalProcs.make_associative(DL(1f)));
    }

    // ---- counterlist_scale, counterlist_sum, counterlist_normalise, counterlist_combine (:951-975)

    [Fact]
    public void CounterLists()
    {
        using var w = new ListsBehaviourWorld();
        DmList c = Dm.list(Dm.kv("a", 2f), Dm.kv("b", 6f), "none");
        DmList scaled = (DmList)GlobalProcs.counterlist_scale(c, 0.5f);
        AssertItems(scaled, "a", "b", "none");
        AssertDmEqual(1f, scaled["a"]);
        AssertDmEqual(3f, scaled["b"]);
        AssertDmEqual(0f, scaled["none"]);               // null * scalar is 0
        AssertDmEqual(8f, GlobalProcs.counterlist_sum(c));
        AssertDmEqual(0f, GlobalProcs.counterlist_sum(Dm.list()));
        AssertDmEqual(0f, GlobalProcs.counterlist_sum(null));
        DmList normal = (DmList)GlobalProcs.counterlist_normalise(c);
        Assert.NotSame(c, normal);
        AssertDmEqual(0.25f, normal["a"]);
        AssertDmEqual(0.75f, normal["b"]);
        DmList zero = Dm.list(Dm.kv("a", 1f), Dm.kv("b", -1f));
        Assert.Same(zero, GlobalProcs.counterlist_normalise(zero));   // the sum is 0: L itself

        DmList L1 = Dm.list(Dm.kv("a", 1f), "b");
        GlobalProcs.counterlist_combine(L1, Dm.list(Dm.kv("a", 2f), Dm.kv("b", 3f), Dm.kv("c", 4f)));
        AssertItems(L1, "a", "b", "c");
        AssertDmEqual(3f, L1["a"]);
        AssertDmEqual(3f, L1["b"]);                      // null += 3
        AssertDmEqual(4f, L1["c"]);
    }

    // ---- assoc_to_keys (:978), assoc_to_keys_features (:985), assoc_to_values (:995)

    [Fact]
    public void AssocToKeysAndValues()
    {
        using var w = new ListsBehaviourWorld();
        DmList inner = DL(7f);
        DmList L = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 1f), Dm.kv("c", inner), "d", Dm.kv("e", inner), Dm.kv(inner, 2f));
        AssertItems(GlobalProcs.assoc_to_keys(L), "a", "b", "c", "d", "e", inner);
        // `. |= LIST_VALUE_WRAP_LISTS(value)`: each distinct value once, lists whole, null too.
        AssertItems(GlobalProcs.assoc_to_values(L), 1f, inner, null, 2f);
        Assert.Null(GlobalProcs.assoc_to_values(null));
        Assert.Empty(Items(GlobalProcs.assoc_to_keys(null)));
    }

    [Fact]
    public void AssocToKeysFeaturesSkipsLockedAccessories()
    {
        using var w = new ListsBehaviourWorld();
        var open = new datum_sprite_accessory();
        var locked = new datum_sprite_accessory { locked = Dm.TRUE };
        // A key without a value passes `value?.locked`. (A non-accessory value such as text would be a runtime error
        // in DM, `"x".locked`; the port's `as` downcast of rules 3.5 reads it as null and `?.` skips the read.)
        DmList L = Dm.list(Dm.kv("open", open), Dm.kv("locked", locked), "none");
        AssertItems(GlobalProcs.assoc_to_keys_features(L), "open", "none");
    }

    // ---- compare_list (:1003), deep_compare_list (:1150)

    [Fact]
    public void CompareListComparesItemsInOrder()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(1f, GlobalProcs.compare_list(DL(1f, "a", null), DL(1, "a", null)));   // 1 == 1f in DM
        AssertDmEqual(0f, GlobalProcs.compare_list(DL(1f, "a"), DL("a", 1f)));
        AssertDmEqual(0f, GlobalProcs.compare_list(DL(1f), DL(1f, 1f)));
        AssertDmEqual(0f, GlobalProcs.compare_list(DL("a"), DL("A")));
        AssertDmEqual(1f, GlobalProcs.compare_list(Dm.list(), Dm.list()));
        AssertDmEqual(0f, GlobalProcs.compare_list(DL(DL(1f)), DL(DL(1f))));   // lists by reference
        AssertDmEqual(0f, GlobalProcs.compare_list(null, Dm.list()));
        // Only the items are compared, not the associated values.
        AssertDmEqual(1f, GlobalProcs.compare_list(Dm.list(Dm.kv("k", 1f)), Dm.list(Dm.kv("k", 2f))));
    }

    [Fact]
    public void DeepCompareListComparesNestedListsAndAssociations()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(1f, GlobalProcs.deep_compare_list(null, null));           // list_1 == list_2
        DmList same = DL(1f);
        AssertDmEqual(1f, GlobalProcs.deep_compare_list(same, same));
        AssertDmEqual(0f, GlobalProcs.deep_compare_list(null, Dm.list()));
        AssertDmEqual(1f, GlobalProcs.deep_compare_list(DL(1f, DL("x", DL(2f))), DL(1f, DL("x", DL(2f)))));
        AssertDmEqual(0f, GlobalProcs.deep_compare_list(DL(1f, DL("x")), DL(1f, DL("y"))));
        AssertDmEqual(0f, GlobalProcs.deep_compare_list(DL(1f), DL(1f, 2f)));
        AssertDmEqual(1f, GlobalProcs.deep_compare_list(Dm.list(Dm.kv("a", DL(1f))), Dm.list(Dm.kv("a", DL(1f)))));
        AssertDmEqual(0f, GlobalProcs.deep_compare_list(Dm.list(Dm.kv("a", 1f)), Dm.list(Dm.kv("a", 2f))));
        AssertDmEqual(0f, GlobalProcs.deep_compare_list(Dm.list(Dm.kv(typeof(obj), 1f)), Dm.list(Dm.kv(typeof(obj), 2f))));
        var o = new obj();
        AssertDmEqual(0f, GlobalProcs.deep_compare_list(Dm.list(Dm.kv(o, 1f)), Dm.list(Dm.kv(o, 2f))));
        // A list key's value is looked up in list_2 under list_1's key itself (:1172), which list_2 does not have:
        // two equal lists with valued list keys compare unequal.
        AssertDmEqual(0f, GlobalProcs.deep_compare_list(Dm.list(Dm.kv(DL(1f), "v")), Dm.list(Dm.kv(DL(1f), "v"))));
        DmList sharedKey = DL(1f);
        AssertDmEqual(1f, GlobalProcs.deep_compare_list(Dm.list(Dm.kv(sharedKey, "v")), Dm.list(Dm.kv(sharedKey, "v"))));
    }

    // ---- recursive_list_resolve (:1036), recursive_list_resolve_element (:1048)

    [Fact]
    public void RecursiveListResolveRebuildsTheListResolvingEachElement()
    {
        using var w = new ListsBehaviourWorld();
        DmList inner = DL(1f, DL(2f));
        DmList source = Dm.list(Dm.kv("a", "b"), "c", 5f, inner, Dm.kv("d", inner));
        DmList resolved = (DmList)GlobalProcs.recursive_list_resolve(source);
        Assert.Equal(5, resolved.Count);
        Assert.Equal("a", resolved[1f]);
        Assert.Equal("b", resolved["a"]);
        Assert.Equal("c", resolved[2f]);
        Assert.Null(resolved["c"]);
        AssertDmEqual(5f, resolved[3f]);
        DmList innerCopy = (DmList)resolved[4f];           // `. += list(resolved)`: one item, a new list
        Assert.NotSame(inner, innerCopy);
        Assert.Equal(2, innerCopy.Count);
        Assert.NotSame(inner[2f], innerCopy[2f]);
        Assert.NotSame(inner, resolved["d"]);
        Assert.Equal("x", GlobalProcs.recursive_list_resolve_element("x"));
        Assert.Null(GlobalProcs.recursive_list_resolve_element(null));
    }

    [Fact]
    public void RecursiveListResolveResolvesWeakrefs()
    {
        // _lists.dm:1052-1054: var/datum/weakref/ref = element; return ref.resolve()
        using var w = new ListsBehaviourWorld();
        Dm.@new<datum_controller_subsystem_garbage>();
        datum live = Dm.@new<datum>();
        datum gone = Dm.@new<datum>();
        object goneRef = GlobalProcs.WEAKREF(gone);
        GlobalProcs.qdel(gone);
        Assert.Same(live, GlobalProcs.recursive_list_resolve_element(GlobalProcs.WEAKREF(live)));
        Assert.Null(GlobalProcs.recursive_list_resolve_element(goneRef));
        DmList resolved = (DmList)GlobalProcs.recursive_list_resolve(Dm.list(Dm.kv("k", GlobalProcs.WEAKREF(live)), DL(GlobalProcs.WEAKREF(live))));
        Assert.Same(live, resolved["k"]);
        Assert.Equal(new object[] { live }, Items(resolved[2f]));
        Assert.Empty(w.Errors);
    }

    // ---- assert_sorted (:1213)

    [Fact]
    public void AssertSortedReportsEveryOutOfOrderPairAndGoesOn()
    {
        // stack_trace reports and returns (rules 9.5), so each bad pair is reported.
        using var w = new ListsBehaviourWorld();
        GlobalProcs.assert_sorted(DL(1f, 2f, 2f, 5f), "fine");
        Assert.Empty(w.Errors);
        GlobalProcs.assert_sorted(DL(5f, 3f, 4f, 1f), "dsc", ProcRef(nameof(GlobalProcs.cmp_numeric_dsc)));
        Assert.Single(w.Errors);
        Assert.Contains("dsc is not sorted. value at 3 (4) is in the wrong place compared to the previous value of 3 (when compared to by /proc/cmp_numeric_dsc)", w.Errors[0].Message);
        GlobalProcs.assert_sorted(DL(3f, 1f, 0f), "asc");
        Assert.Equal(3, w.Errors.Count);
        Assert.Contains("value at 2 (1)", w.Errors[1].Message);
        Assert.Contains("value at 3 (0)", w.Errors[2].Message);
        // The runtime is reported at the use site, the stack_trace call at _lists.dm:1220 (rules 9.5: world/Error's
        // rewrite of _stack_trace's message, error_handler.dm:62-66).
        Assert.EndsWith("(when compared to by /proc/cmp_numeric_asc)", w.Errors[2].Message);
        var crash = Assert.IsType<DmCrashException>(w.Errors[2]);
        Assert.Equal("code/__HELPERS/_lists.dm", crash.DmFile);
        Assert.Equal(1220, crash.DmLine);
    }

    // ---- remove_non_dm_variants (:1341), compare_lua_logs (:1365)

    [Fact]
    public void RemoveNonDmVariantsNullsTheUnconvertibleEntries()
    {
        using var w = new ListsBehaviourWorld();
        DmList nested = DL(1f, 2f);
        DmList values = Dm.list(Dm.kv("f", "fn"), "plain", nested, Dm.kv("t", DL("x")));
        DmList variants = DL(
            Dm.list(Dm.kv("key", 0f), Dm.kv("value", "function")),      // value not a list: values["f"] = null
            Dm.list(Dm.kv("key", "userdata")),                          // key not a list: values[2] = null
            Dm.list(Dm.kv("key", DL(Dm.list(Dm.kv("key", "thread")), Dm.list()))),  // recurse into the key list
            Dm.list(Dm.kv("value", DL(Dm.list(Dm.kv("key", "x"))))));   // recurse into the value list
        GlobalProcs.remove_non_dm_variants(values, variants);
        Assert.Equal("f", values[1f]);
        Assert.Null(values["f"]);
        Assert.Null(values[2f]);
        AssertItems(nested, null, 2f);
        AssertItems(values["t"], (object)null);
        Assert.Throws<DmCrashException>(() => GlobalProcs.remove_non_dm_variants(DL(1f), Dm.list()));
        GlobalProcs.remove_non_dm_variants(null, Dm.list());        // not lists: nothing
    }

    [Fact]
    public void CompareLuaLogs()
    {
        using var w = new ListsBehaviourWorld();
        DmList Log(string status, params object[] extra)
        {
            var items = new List<object> { Dm.kv("status", status), Dm.kv("name", "n"), Dm.kv("message", "m"), Dm.kv("chunk", "c") };
            items.AddRange(extra);
            return Dm.list(items.ToArray());
        }
        DmList a = Log("finished");
        AssertDmEqual(1f, GlobalProcs.compare_lua_logs(a, a));
        DmList renamed = Log("finished");
        renamed["name"] = "other";
        AssertDmEqual(0f, GlobalProcs.compare_lua_logs(Log("finished"), renamed));
        AssertDmEqual(1f, GlobalProcs.compare_lua_logs(
            Log("finished", Dm.kv("return_values", DL(1f, DL(2f))), Dm.kv("variants", DL(null))),
            Log("finished", Dm.kv("return_values", DL(1f, DL(2f))), Dm.kv("variants", DL(null)))));
        AssertDmEqual(0f, GlobalProcs.compare_lua_logs(
            Log("yield", Dm.kv("return_values", DL(1f))),
            Log("yield", Dm.kv("return_values", DL(2f)))));
        AssertDmEqual(1f, GlobalProcs.compare_lua_logs(
            Log("runtime", Dm.kv("file", "f"), Dm.kv("line", 3f), Dm.kv("stack", DL("s"))),
            Log("runtime", Dm.kv("file", "f"), Dm.kv("line", 3f), Dm.kv("stack", DL("s")))));
        AssertDmEqual(0f, GlobalProcs.compare_lua_logs(
            Log("runtime", Dm.kv("file", "f"), Dm.kv("line", 3f)),
            Log("runtime", Dm.kv("file", "f"), Dm.kv("line", 4f))));
        AssertDmEqual(1f, GlobalProcs.compare_lua_logs(Log("errored", Dm.kv("x", 1f)), Log("errored", Dm.kv("x", 2f))));
    }

    // ---- pick_recursive (:1395), blend_weighted_lists (:1404)

    [Fact]
    public void PickRecursivePicksFromItsArgumentsThenFromEachPickedList()
    {
        using var w = new ListsBehaviourWorld();
        Assert.Equal("a", GlobalProcs.pick_recursive(DL(DL("a"))));
        Assert.Equal("b", GlobalProcs.pick_recursive("b"));
        for (ulong seed = 1; seed <= 20; seed++)
        {
            object expected = Replay(seed, () =>
            {
                object r = Dm.pick(DL("x", DL("y", DL("z"))));
                while (r is DmList inner) r = Dm.pick(inner);
                return r;
            });
            Assert.Equal(expected, Replay(seed, () => GlobalProcs.pick_recursive("x", DL("y", DL("z")))));
        }
    }

    [Fact]
    public void BlendWeightedListsMixesNormalisedWeightsInThousandths()
    {
        // joined = A | B; each weight becomes round((A'[e] * (1 - blend) + B'[e] * blend) * 1000), round() with one
        // argument being floor (rules 4.6).
        using var w = new ListsBehaviourWorld();
        DmList A = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 1f));
        DmList B = Dm.list(Dm.kv("a", 1f), Dm.kv("c", 3f));
        DmList blended = (DmList)GlobalProcs.blend_weighted_lists(A, B, 0.5f);
        AssertItems(blended, "a", "b", "c");
        AssertDmEqual(375f, blended["a"]);
        AssertDmEqual(250f, blended["b"]);
        AssertDmEqual(375f, blended["c"]);
        // floor, not nearest: 1/3 * 1000 = 333.33 gives 333, 2/3 * 1000 = 666.67 gives 666.
        DmList thirds = (DmList)GlobalProcs.blend_weighted_lists(Dm.list(Dm.kv("x", 1f), Dm.kv("y", 2f)), Dm.list(), 0f);
        AssertDmEqual(333f, thirds["x"]);
        AssertDmEqual(666f, thirds["y"]);
        AssertDmEqual(1f, A["a"]);                       // the inputs keep their weights
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Pilot.Lists.ListsBehaviourWorld;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// Behaviour of the function-like macros of code/__HELPERS/_lists.dm (the LAZY* family at :52-111 and the small
/// list macros), with the expected values read from their DM expansions. The central property of a lazy list:
/// it is null while empty, the LAZY macros create it on the first write and set it back to null when a removal
/// empties it.
/// </summary>
public class LazyMacroBehaviourTests
{
    // ---- LAZYADD / LAZYREMOVE (:60, :62): `L ||= list(); L += I;` and `if(L) { L -= I; if(!length(L)) { L = null; } }`

    [Fact]
    public void LazyAddThenLazyRemoveToEmptyGivesNull()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        GlobalProcs.LAZYADD(ref L, "x");
        Assert.NotNull(L);
        AssertItems(L, "x");
        GlobalProcs.LAZYREMOVE(ref L, "x");
        Assert.Null(L);
    }

    [Fact]
    public void LazyAddKeepsAnExistingListAndAppendsInOrder()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = DL("a");
        DmList before = L;
        GlobalProcs.LAZYADD(ref L, "b");
        GlobalProcs.LAZYADD(ref L, "a");
        Assert.Same(before, L);                          // `L ||= list()` keeps a list that exists
        AssertItems(L, "a", "b", "a");                   // `+=` appends, duplicates included
    }

    [Fact]
    public void LazyAddOfAListAddsItsItems()
    {
        // `L += I` with a list I adds I's items (BYOND list +=), so LAZYADD(L, list(1, 2)) adds 1 and 2.
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        GlobalProcs.LAZYADD(ref L, DL(1f, 2f));
        AssertItems(L, 1f, 2f);
    }

    [Fact]
    public void LazyRemoveTakesTheLastOccurrenceAndKeepsANonEmptyList()
    {
        // `L -= I` removes the last occurrence; the list is nulled only when that leaves it empty.
        using var w = new ListsBehaviourWorld();
        DmList L = DL("a", "b", "a");
        GlobalProcs.LAZYREMOVE(ref L, "a");
        AssertItems(L, "a", "b");
        GlobalProcs.LAZYREMOVE(ref L, "missing");
        AssertItems(L, "a", "b");
        GlobalProcs.LAZYREMOVE(ref L, "a");
        GlobalProcs.LAZYREMOVE(ref L, "b");
        Assert.Null(L);
    }

    [Fact]
    public void LazyRemoveOfAnAlreadyEmptyListNullsIt()
    {
        // `if(L)`: an empty list is true in DM, so LAZYREMOVE on list() runs, removes nothing and then nulls it.
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list();
        GlobalProcs.LAZYREMOVE(ref L, "x");
        Assert.Null(L);
        GlobalProcs.LAZYREMOVE(ref L, "x");              // null stays null
        Assert.Null(L);
    }

    [Fact]
    public void LazyRemoveOfAListRemovesItsItems()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = DL(1f, 2f, 3f);
        GlobalProcs.LAZYREMOVE(ref L, DL(1f, 3f));
        AssertItems(L, 2f);
        GlobalProcs.LAZYREMOVE(ref L, DL(2f));
        Assert.Null(L);
    }

    // ---- LAZYOR (:64): `L ||= list(); L |= I;`

    [Fact]
    public void LazyOrAddsOnlyMissingItems()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        GlobalProcs.LAZYOR(ref L, "a");
        GlobalProcs.LAZYOR(ref L, "a");
        GlobalProcs.LAZYOR(ref L, DL("a", "b", "c"));
        AssertItems(L, "a", "b", "c");
    }

    // ---- LAZYINITLIST, UNSETEMPTY, ASSOC_UNSETEMPTY (:52-56), LAZYNULL (:76)

    [Fact]
    public void LazyInitListCreatesOnlyWhenNull()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        GlobalProcs.LAZYINITLIST(ref L);
        Assert.NotNull(L);
        Assert.Equal(0, L.Count);
        DmList existing = DL("x");
        DmList same = existing;
        GlobalProcs.LAZYINITLIST(ref existing);
        Assert.Same(same, existing);
    }

    [Fact]
    public void UnsetEmptyNullsOnlyAnEmptyList()
    {
        using var w = new ListsBehaviourWorld();
        DmList empty = Dm.list();
        GlobalProcs.UNSETEMPTY(ref empty);
        Assert.Null(empty);
        DmList full = DL(0f);
        GlobalProcs.UNSETEMPTY(ref full);
        AssertItems(full, 0f);
        DmList none = null;
        GlobalProcs.UNSETEMPTY(ref none);
        Assert.Null(none);
    }

    [Fact]
    public void AssocUnsetEmptyRemovesAKeyWhoseValueIsEmptyOrNull()
    {
        // `if (!length(L[K])) L -= K;`: length(list()) and length(null) are 0.
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("empty", Dm.list()), Dm.kv("full", DL(1f)), "novalue");
        GlobalProcs.ASSOC_UNSETEMPTY(ref L, "empty");
        GlobalProcs.ASSOC_UNSETEMPTY(ref L, "full");
        GlobalProcs.ASSOC_UNSETEMPTY(ref L, "novalue");
        AssertItems(L, "full");
    }

    [Fact]
    public void LazyNullSetsTheVarToNull()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = DL(1f);
        GlobalProcs.LAZYNULL(ref L);
        Assert.Null(L);
    }

    // ---- LAZYACCESS (:68): `(L ? (isnum(I) ? (I > 0 && I <= length(L) ? L[I] : null) : L[I]) : null)`

    [Fact]
    public void LazyAccessWithANumberIsABoundsCheckedPosition()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = DL("a", "b", "c");
        Assert.Equal("a", GlobalProcs.LAZYACCESS(L, 1f));
        Assert.Equal("c", GlobalProcs.LAZYACCESS(L, 3f));
        Assert.Null(GlobalProcs.LAZYACCESS(L, 4f));      // past the end: null, not "list index out of bounds"
        Assert.Null(GlobalProcs.LAZYACCESS(L, 0f));
        Assert.Null(GlobalProcs.LAZYACCESS(L, -1f));
        Assert.Equal("b", GlobalProcs.LAZYACCESS(L, 2));  // a boxed int is a number too (isnum)
    }

    [Fact]
    public void LazyAccessWithAnyOtherValueIsAnAssociativeLookup()
    {
        using var w = new ListsBehaviourWorld();
        var key = new obj_item();
        DmList L = Dm.list(Dm.kv("k", "v"), Dm.kv(key, 5f), Dm.kv(typeof(obj), "t"), "bare");
        Assert.Equal("v", GlobalProcs.LAZYACCESS(L, "k"));
        AssertDmEqual(5f, GlobalProcs.LAZYACCESS(L, key));
        Assert.Equal("t", GlobalProcs.LAZYACCESS(L, typeof(obj)));
        Assert.Null(GlobalProcs.LAZYACCESS(L, "bare"));    // a key without a value
        Assert.Null(GlobalProcs.LAZYACCESS(L, "missing"));
        Assert.Null(GlobalProcs.LAZYACCESS(L, "1"));       // text, not a number: a key lookup
        // In the same assoc list a number reads the key at that position.
        Assert.Equal("k", GlobalProcs.LAZYACCESS(L, 1f));
    }

    [Fact]
    public void LazyAccessOfANullOrEmptyList()
    {
        using var w = new ListsBehaviourWorld();
        Assert.Null(GlobalProcs.LAZYACCESS(null, 1f));
        Assert.Null(GlobalProcs.LAZYACCESS(null, "k"));
        Assert.Null(GlobalProcs.LAZYACCESS(Dm.list(), 1f));
        Assert.Null(GlobalProcs.LAZYACCESS(Dm.list(), "k"));
    }

    // ---- LAZYSET, LAZYSETLEN, LAZYLEN, LAZYFIND (:66-74)

    [Fact]
    public void LazySetCreatesTheListAndOverwritesTheValue()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        GlobalProcs.LAZYSET(ref L, "a", 1f);
        GlobalProcs.LAZYSET(ref L, "b", 2f);
        GlobalProcs.LAZYSET(ref L, "a", 3f);
        AssertItems(L, "a", "b");                        // a key is added once and keeps its place
        AssertDmEqual(3f, L["a"]);
        AssertDmEqual(2f, L["b"]);
    }

    [Fact]
    public void LazySetWithANumberWritesAPosition()
    {
        // `L[K] = V` with a number K is a positional write: on the fresh empty list it is out of bounds.
        using var w = new ListsBehaviourWorld();
        DmList L = DL("a", "b");
        GlobalProcs.LAZYSET(ref L, 2f, "z");
        AssertItems(L, "a", "z");
        DmList none = null;
        Assert.ThrowsAny<DmRuntimeException>(() => GlobalProcs.LAZYSET(ref none, 1f, "x"));
        Assert.NotNull(none);                            // `L ||= list()` ran before the failing write
    }

    [Fact]
    public void LazySetLenCreatesPadsAndTruncates()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        GlobalProcs.LAZYSETLEN(ref L, 3f);
        AssertItems(L, null, null, null);
        DmList longer = DL(1f, 2f, 3f);
        GlobalProcs.LAZYSETLEN(ref longer, 1f);
        AssertItems(longer, 1f);
        GlobalProcs.LAZYSETLEN(ref longer, 0f);
        Assert.NotNull(longer);                          // setting len never nulls
        Assert.Equal(0, longer.Count);
    }

    [Fact]
    public void LazyLenAndLazyFind()
    {
        using var w = new ListsBehaviourWorld();
        Assert.Equal(0f, GlobalProcs.LAZYLEN(null));
        Assert.Equal(0f, GlobalProcs.LAZYLEN(Dm.list()));
        Assert.Equal(3f, GlobalProcs.LAZYLEN(DL(1f, 1f, null)));
        // `L?.Find(V)`: null for a null list, 0 when absent, else the first position.
        Assert.Null(GlobalProcs.LAZYFIND(null, "a"));
        Assert.Equal(0f, GlobalProcs.LAZYFIND(DL("b"), "a"));
        Assert.Equal(2f, GlobalProcs.LAZYFIND(DL("b", "a", "a"), "a"));
    }

    // ---- LAZYADDASSOC, LAZYADDASSOCLIST, LAZYREMOVEASSOC, LAZYACCESSASSOC (:78-84)

    [Fact]
    public void LazyAddAssocAddsToTheValueWithDmPlus()
    {
        // `L[K] += V`: null + V is V; numbers add; text concatenates.
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        GlobalProcs.LAZYADDASSOC(ref L, "n", 2f);
        GlobalProcs.LAZYADDASSOC(ref L, "n", 0.5f);
        GlobalProcs.LAZYADDASSOC(ref L, "t", "ab");
        GlobalProcs.LAZYADDASSOC(ref L, "t", "c");
        AssertItems(L, "n", "t");
        AssertDmEqual(2.5f, L["n"]);
        Assert.Equal("abc", L["t"]);
    }

    [Fact]
    public void LazyAddAssocOfAListStoresThatListAndThenChangesItInPlace()
    {
        // null += list makes the element that very list (rules 6.3); the next += changes it in place.
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        DmList inner = DL(1f);
        GlobalProcs.LAZYADDASSOC(ref L, "k", inner);
        Assert.Same(inner, L["k"]);
        GlobalProcs.LAZYADDASSOC(ref L, "k", 2f);
        Assert.Same(inner, L["k"]);
        AssertItems(inner, 1f, 2f);
    }

    [Fact]
    public void LazyAddAssocListWrapsTheValue()
    {
        // `L[K] += list(V)`: the first call stores the new list(V); later calls add V as one item.
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        DmList v1 = DL(1f, 2f);
        GlobalProcs.LAZYADDASSOCLIST(ref L, "k", v1);
        DmList stored = (DmList)L["k"];
        Assert.NotSame(v1, stored);
        AssertItems(stored, v1);
        GlobalProcs.LAZYADDASSOCLIST(ref L, "k", "x");
        GlobalProcs.LAZYADDASSOCLIST(ref L, "k", "x");
        Assert.Same(stored, L["k"]);
        AssertItems(stored, v1, "x", "x");
        GlobalProcs.LAZYADDASSOCLIST(ref L, "j", null); // list(null) is a list with one null
        AssertItems(L["j"], (object)null);
    }

    [Fact]
    public void LazyRemoveAssocRemovesTheValueThenTheKeyThenTheList()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("k", DL("a", "b")), Dm.kv("j", DL("c")));
        GlobalProcs.LAZYREMOVEASSOC(ref L, "k", "a");
        AssertItems(L["k"], "b");
        AssertItems(L, "k", "j");
        GlobalProcs.LAZYREMOVEASSOC(ref L, "k", "b");
        AssertItems(L, "j");                              // the emptied key goes
        GlobalProcs.LAZYREMOVEASSOC(ref L, "j", "c");
        Assert.Null(L);                                  // and the emptied list
    }

    [Fact]
    public void LazyRemoveAssocIgnoresAMissingKeyAndANullList()
    {
        // `if(L?[K])`: nothing happens unless the key has a true value.
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("k", DL("a")), "bare");
        GlobalProcs.LAZYREMOVEASSOC(ref L, "missing", "a");
        GlobalProcs.LAZYREMOVEASSOC(ref L, "bare", "a");
        AssertItems(L, "k", "bare");
        DmList none = null;
        GlobalProcs.LAZYREMOVEASSOC(ref none, "k", "a");
        Assert.Null(none);
    }

    [Fact]
    public void LazyRemoveAssocOfAValueThatIsNotThereKeepsTheKey()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("k", DL("a")));
        GlobalProcs.LAZYREMOVEASSOC(ref L, "k", "zzz");
        AssertItems(L, "k");
        AssertItems(L["k"], "a");
    }

    [Fact]
    public void LazyAccessAssocWalksTwoLevelsNullSafely()
    {
        // `L?[I]?[K]`
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("a", Dm.list(Dm.kv("b", "found"))), "bare");
        Assert.Equal("found", GlobalProcs.LAZYACCESSASSOC(L, "a", "b"));
        Assert.Null(GlobalProcs.LAZYACCESSASSOC(L, "a", "c"));
        Assert.Null(GlobalProcs.LAZYACCESSASSOC(L, "bare", "b"));
        Assert.Null(GlobalProcs.LAZYACCESSASSOC(null, "a", "b"));
    }

    // ---- LAZYCOPY, LAZYLISTDUPLICATE, LAZYCLEARLIST, SANITIZE_LIST (:58, :89-93)

    [Fact]
    public void LazyCopyAlwaysGivesANewList()
    {
        // `(L?.Copy() || list())`: a copy (an empty copy is true too), or a new list for null.
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("a", 1f), "b");
        DmList copy = GlobalProcs.LAZYCOPY(L);
        Assert.NotSame(L, copy);
        AssertItems(copy, "a", "b");
        AssertDmEqual(1f, copy["a"]);                     // Copy keeps the associations
        DmList fromNull = GlobalProcs.LAZYCOPY(null);
        Assert.NotNull(fromNull);
        Assert.Equal(0, fromNull.Count);
        DmList empty = Dm.list();
        Assert.NotSame(empty, GlobalProcs.LAZYCOPY(empty));
    }

    [Fact]
    public void LazyListDuplicateNullsOnlyANullList()
    {
        // `(L ? L.Copy() : null)`: the doc comment says an empty list gives null, but an empty list is true in DM,
        // so it gives an empty copy.
        using var w = new ListsBehaviourWorld();
        Assert.Null(GlobalProcs.LAZYLISTDUPLICATE(null));
        DmList empty = Dm.list();
        DmList dup = GlobalProcs.LAZYLISTDUPLICATE(empty);
        Assert.NotNull(dup);
        Assert.NotSame(empty, dup);
        AssertItems(GlobalProcs.LAZYLISTDUPLICATE(DL(1f, 2f)), 1f, 2f);
    }

    [Fact]
    public void LazyClearListEmptiesInPlaceAndToleratesNull()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("a", 1f), "b");
        GlobalProcs.LAZYCLEARLIST(L);
        Assert.Equal(0, L.Count);
        Assert.Null(L["a"]);
        GlobalProcs.LAZYCLEARLIST(null);
    }

    [Fact]
    public void SanitizeListKeepsAListAndReplacesAnythingElse()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = DL(1f);
        Assert.Same(L, GlobalProcs.SANITIZE_LIST(L));
        foreach (object other in new object[] { null, 0f, "text", typeof(obj), new obj_item() })
        {
            object result = GlobalProcs.SANITIZE_LIST(other);
            Assert.IsType<DmList>(result);
            Assert.Equal(0, ((DmList)result).Count);
        }
    }

    // ---- LAZYORASSOCLIST (:95), LAZYINSERT (:100)

    [Fact]
    public void LazyOrAssocListBuildsASetPerKey()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        GlobalProcs.LAZYORASSOCLIST(ref L, "k", "a");
        GlobalProcs.LAZYORASSOCLIST(ref L, "k", "b");
        GlobalProcs.LAZYORASSOCLIST(ref L, "k", "a");
        GlobalProcs.LAZYORASSOCLIST(ref L, "j", "a");
        AssertItems(L, "k", "j");
        AssertItems(L["k"], "a", "b");
        AssertItems(L["j"], "a");
    }

    [Fact]
    public void LazyInsertInitializesWithTheValueWhateverTheIndex()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = null;
        GlobalProcs.LAZYINSERT(ref L, 5, "a");
        AssertItems(L, "a");
        // `list(value)` with a list value nests it.
        DmList nested = null;
        DmList inner = DL(1f, 2f);
        GlobalProcs.LAZYINSERT(ref nested, 1, inner);
        AssertItems(nested, inner);
    }

    [Fact]
    public void LazyInsertCallsInsertOnAnExistingList()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = DL("b");
        GlobalProcs.LAZYINSERT(ref L, 1, "a");
        GlobalProcs.LAZYINSERT(ref L, 3, "c");           // len+1 appends
        AssertItems(L, "a", "b", "c");
        // `index == 0 && index > length(lazylist)` is never true, so index 0 goes to Insert(0, ...), which appends.
        GlobalProcs.LAZYINSERT(ref L, 0, "z");
        AssertItems(L, "a", "b", "c", "z");
        // Insert with a list argument inserts its items.
        GlobalProcs.LAZYINSERT(ref L, 1, DL(1f, 2f));
        AssertItems(L, 1f, 2f, "a", "b", "c", "z");
    }

    // ---- LAZYMAX, LAZYMIN (:109, :111): `(isnull(lazylist) ? default : max(lazylist))`

    [Fact]
    public void LazyMaxAndMinUseTheDefaultOnlyForNull()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(7f, GlobalProcs.LAZYMAX(DL(3f, 7f, -2f), 0f));
        AssertDmEqual(-2f, GlobalProcs.LAZYMIN(DL(3f, 7f, -2f), 0f));
        Assert.Equal("dflt", GlobalProcs.LAZYMAX(null, "dflt"));
        Assert.Equal("dflt", GlobalProcs.LAZYMIN(null, "dflt"));
        // max() and min() of an empty list are null: the default is not used for an empty list.
        Assert.Null(GlobalProcs.LAZYMAX(Dm.list(), 5f));
        Assert.Null(GlobalProcs.LAZYMIN(Dm.list(), 5f));
        Assert.Equal("c", GlobalProcs.LAZYMAX(DL("a", "c", "b"), null));
    }

    // ---- LIST_VALUE_WRAP_LISTS, UNTYPED_LIST_ADD, UNTYPED_LIST_REMOVE (:14-18)

    [Fact]
    public void ListValueWrapListsWrapsOnlyLists()
    {
        using var w = new ListsBehaviourWorld();
        DmList inner = DL(1f);
        object wrapped = GlobalProcs.LIST_VALUE_WRAP_LISTS(inner);
        Assert.NotSame(inner, wrapped);
        AssertItems(wrapped, inner);
        Assert.Equal("x", GlobalProcs.LIST_VALUE_WRAP_LISTS("x"));
        Assert.Null(GlobalProcs.LIST_VALUE_WRAP_LISTS(null));
    }

    [Fact]
    public void UntypedListAddAndRemoveTreatAListAsOneItem()
    {
        using var w = new ListsBehaviourWorld();
        DmList inner = DL(1f, 2f);
        DmList L = DL(1f);
        GlobalProcs.UNTYPED_LIST_ADD(ref L, inner);
        GlobalProcs.UNTYPED_LIST_ADD(ref L, 3f);
        AssertItems(L, 1f, inner, 3f);
        GlobalProcs.UNTYPED_LIST_REMOVE(ref L, inner);  // removes the list, not 1 and 2
        AssertItems(L, 1f, 3f);
        GlobalProcs.UNTYPED_LIST_REMOVE(ref L, 1f);
        AssertItems(L, 3f);
        // With a null var, `null += list(inner)` makes the var that new wrapper list.
        DmList none = null;
        GlobalProcs.UNTYPED_LIST_ADD(ref none, inner);
        AssertItems(none, inner);
    }

    // ---- reverseList (:126), KEYBYINDEX (:759), DEFAULTPICK (:944), LAZY_LISTS_OR (:1016), is_type_in_typecache (:323)

    [Fact]
    public void ReverseListReversesACopy()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("a", 1f), "b", "c");
        DmList reversed = (DmList)GlobalProcs.reverseList(L);
        Assert.NotSame(L, reversed);
        AssertItems(reversed, "c", "b", "a");
        AssertDmEqual(1f, reversed["a"]);
        AssertItems(L, "a", "b", "c");
    }

    [Fact]
    public void KeyByIndexReturnsTheKeyAtAnInRangePosition()
    {
        using var w = new ListsBehaviourWorld();
        DmList L = Dm.list(Dm.kv("first", 1f), Dm.kv("second", 2f));
        Assert.Equal("first", GlobalProcs.KEYBYINDEX(L, 1));
        Assert.Equal("second", GlobalProcs.KEYBYINDEX(L, 2));
        Assert.Null(GlobalProcs.KEYBYINDEX(L, 3));
        Assert.Null(GlobalProcs.KEYBYINDEX(L, 0));
        Assert.Null(GlobalProcs.KEYBYINDEX(L, -1));
        Assert.Null(GlobalProcs.KEYBYINDEX(null, 1));    // length(null) is 0
    }

    [Fact]
    public void DefaultPickPicksFromANonEmptyListOrGivesTheDefault()
    {
        using var w = new ListsBehaviourWorld();
        Assert.Equal("only", GlobalProcs.DEFAULTPICK(DL("only"), "d"));
        Assert.Equal("d", GlobalProcs.DEFAULTPICK(Dm.list(), "d"));
        Assert.Equal("d", GlobalProcs.DEFAULTPICK(null, "d"));
        Assert.Equal("d", GlobalProcs.DEFAULTPICK("not a list", "d"));
        DmList choices = DL("a", "b", "c");
        for (int i = 0; i < 20; i++)
            Assert.Contains(GlobalProcs.DEFAULTPICK(choices, "d"), Items(choices));
    }

    [Fact]
    public void DefaultPickDrawsExactlyOnePickFromTheWorldRng()
    {
        // `pick(L)` from the world RNG (rules 4.13): the same seed in another world picks the same item, and a
        // second draw after it continues the same sequence.
        DmList choices = DL("a", "b", "c", "d", "e");
        for (ulong seed = 1; seed <= 20; seed++)
        {
            (object, object) expected;
            using (var w = new ListsBehaviourWorld(seed))
                expected = (Dm.pick(choices), Dm.rand(1f, 1000f));
            using (var w = new ListsBehaviourWorld(seed))
                Assert.Equal(expected, (GlobalProcs.DEFAULTPICK(choices, "d"), (object)Dm.rand(1f, 1000f)));
            // The default draws nothing.
            using (var w = new ListsBehaviourWorld(seed))
            {
                Assert.Equal("d", GlobalProcs.DEFAULTPICK(Dm.list(), "d"));
                Assert.Equal(expected.Item1, Dm.pick(choices));
            }
        }
    }

    [Fact]
    public void LazyListsOrUnitesOrCopiesOrGivesNull()
    {
        using var w = new ListsBehaviourWorld();
        DmList left = DL(1f, 2f), right = DL(2f, 3f);
        DmList both = (DmList)GlobalProcs.LAZY_LISTS_OR(left, right);
        AssertItems(both, 1f, 2f, 3f);                   // left | right
        Assert.NotSame(left, both);
        DmList onlyLeft = (DmList)GlobalProcs.LAZY_LISTS_OR(left, Dm.list());
        AssertItems(onlyLeft, 1f, 2f);
        Assert.NotSame(left, onlyLeft);                  // left_list.Copy()
        DmList onlyRight = (DmList)GlobalProcs.LAZY_LISTS_OR(null, right);
        AssertItems(onlyRight, 2f, 3f);
        Assert.NotSame(right, onlyRight);
        Assert.Null(GlobalProcs.LAZY_LISTS_OR(null, null));
        Assert.Null(GlobalProcs.LAZY_LISTS_OR(Dm.list(), Dm.list()));
        AssertItems(left, 1f, 2f);                       // the operands are not changed
    }

    [Fact]
    public void IsTypeInTypecacheReadsTheTypeOfAnInstanceOrThePathItself()
    {
        // `(A && length(L) && L[(ispath(A) ? A : A:type)])`
        using var w = new ListsBehaviourWorld();
        DmList cache = Dm.list(Dm.kv(typeof(obj_item), Dm.TRUE), Dm.kv(typeof(mob), Dm.FALSE));
        Assert.True(Dm.truthy(GlobalProcs.is_type_in_typecache(new obj_item(), cache)));
        Assert.True(Dm.truthy(GlobalProcs.is_type_in_typecache(typeof(obj_item), cache)));
        Assert.False(Dm.truthy(GlobalProcs.is_type_in_typecache(new obj_item_fish(), cache))); // exact types only
        AssertDmEqual(Dm.FALSE, GlobalProcs.is_type_in_typecache(new mob(), cache));
        Assert.Null(GlobalProcs.is_type_in_typecache(null, cache));                   // A itself
        AssertDmEqual(0f, GlobalProcs.is_type_in_typecache(new obj_item(), Dm.list())); // length(L)
        AssertDmEqual(0f, GlobalProcs.is_type_in_typecache(new obj_item(), null));
    }

    // ---- SORT_FIRST_INDEX, SORT_PRIORITY_INDEX, SORT_COMPARE_DIRECTLY (:203-205)

    [Fact]
    public void SortKeyMacros()
    {
        using var w = new ListsBehaviourWorld();
        DmList entry = Dm.list(Dm.kv("name", "n"), Dm.kv("priority", 4f));
        Assert.Equal("name", GlobalProcs.SORT_FIRST_INDEX(entry));
        AssertDmEqual(4f, GlobalProcs.SORT_PRIORITY_INDEX(entry));
        var thing = new obj();
        Assert.Same(thing, GlobalProcs.SORT_COMPARE_DIRECTLY(thing));
    }
}

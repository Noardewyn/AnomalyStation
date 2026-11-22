using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Pilot.Lists.SmokeListsWorld;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// Smoke tests of the LAZY* macros (code/__HELPERS/_lists.dm:52-111): a lazy list is null while empty, the macros
/// create it on the first write and null it again when a removal empties it.
/// </summary>
public class SmokeLazyMacroTests
{
    [Fact]
    public void LazyAddCreatesTheListAndLazyRemoveNullsItWhenEmpty()
    {
        using var w = new SmokeListsWorld();
        DmList L = null;
        GlobalProcs.LAZYADD(ref L, "a");
        GlobalProcs.LAZYADD(ref L, "b");
        Assert.Equal(new object[] { "a", "b" }, Items(L));
        GlobalProcs.LAZYREMOVE(ref L, "a");
        Assert.Equal(new object[] { "b" }, Items(L));
        GlobalProcs.LAZYREMOVE(ref L, "b");
        Assert.Null(L);
        GlobalProcs.LAZYREMOVE(ref L, "b");                 // a null list stays null
        Assert.Null(L);
    }

    [Fact]
    public void LazyAddOfAListAddsItsItemsAndLazyOrSkipsDuplicates()
    {
        // L += list(...) adds the items (BYOND list +=), L |= x only items not already present.
        using var w = new SmokeListsWorld();
        DmList L = null;
        GlobalProcs.LAZYADD(ref L, NewList(1f, 2f));
        GlobalProcs.LAZYOR(ref L, 2f);
        GlobalProcs.LAZYOR(ref L, 3f);
        Assert.Equal(new object[] { 1f, 2f, 3f }, Items(L));
    }

    [Fact]
    public void LazyInitListUnsetEmptyAndLazyNull()
    {
        using var w = new SmokeListsWorld();
        DmList L = null;
        GlobalProcs.LAZYINITLIST(ref L);
        Assert.NotNull(L);
        DmList same = L;
        GlobalProcs.LAZYINITLIST(ref L);                     // an existing list is kept, even an empty one
        Assert.Same(same, L);
        GlobalProcs.UNSETEMPTY(ref L);
        Assert.Null(L);
        DmList full = NewList(1f);
        GlobalProcs.UNSETEMPTY(ref full);
        Assert.NotNull(full);
        GlobalProcs.LAZYNULL(ref full);
        Assert.Null(full);
    }

    [Fact]
    public void LazyAccessChecksNumericBoundsAndReadsKeys()
    {
        // _lists.dm:68: a number is a bounds-checked index, anything else a key; a null list gives null.
        using var w = new SmokeListsWorld();
        DmList L = Dm.list(Dm.kv("k", "v"), "x");
        Assert.Equal("k", GlobalProcs.LAZYACCESS(L, 1f));
        Assert.Equal("x", GlobalProcs.LAZYACCESS(L, 2f));
        Assert.Null(GlobalProcs.LAZYACCESS(L, 3f));
        Assert.Null(GlobalProcs.LAZYACCESS(L, 0f));
        Assert.Equal("v", GlobalProcs.LAZYACCESS(L, "k"));
        Assert.Null(GlobalProcs.LAZYACCESS(L, "missing"));
        Assert.Null(GlobalProcs.LAZYACCESS(null, 1f));
        Assert.Equal(0f, GlobalProcs.LAZYLEN(null));
        Assert.Equal(2f, GlobalProcs.LAZYLEN(L));
        Assert.Null(GlobalProcs.LAZYFIND(null, "x"));
        Assert.Equal(2f, GlobalProcs.LAZYFIND(L, "x"));
    }

    [Fact]
    public void LazySetAndTheAssocMacros()
    {
        using var w = new SmokeListsWorld();
        DmList L = null;
        GlobalProcs.LAZYSET(ref L, "a", 1f);
        Assert.Equal(1f, L["a"]);

        DmList counts = null;
        GlobalProcs.LAZYADDASSOC(ref counts, "n", 2f);
        GlobalProcs.LAZYADDASSOC(ref counts, "n", 3f);
        Assert.Equal(5f, counts["n"]);

        // LAZYADDASSOCLIST adds list(V), so a list value is kept as one item; the inner list changes in place.
        DmList lists = null;
        DmList v = NewList(1f, 2f);
        GlobalProcs.LAZYADDASSOCLIST(ref lists, "k", v);
        DmList inner = (DmList)lists["k"];
        GlobalProcs.LAZYADDASSOCLIST(ref lists, "k", "x");
        Assert.Same(inner, lists["k"]);
        Assert.Equal(new object[] { v, "x" }, Items(inner));

        // L[K] -= V with a list V removes V's items, not V (BYOND list -=): nothing here.
        GlobalProcs.LAZYREMOVEASSOC(ref lists, "k", v);
        Assert.Equal(new object[] { v, "x" }, Items((DmList)lists["k"]));

        // LAZYREMOVEASSOC removes the value, then the empty key, then nulls the empty list.
        DmList tags = null;
        GlobalProcs.LAZYADDASSOCLIST(ref tags, "k", "a");
        GlobalProcs.LAZYADDASSOCLIST(ref tags, "j", "b");
        GlobalProcs.LAZYREMOVEASSOC(ref tags, "k", "a");
        Assert.Equal(new object[] { "j" }, Items(tags));
        GlobalProcs.LAZYREMOVEASSOC(ref tags, "j", "b");
        Assert.Null(tags);

        DmList nested = Dm.list(Dm.kv("a", Dm.list(Dm.kv("b", 7f))));
        Assert.Equal(7f, GlobalProcs.LAZYACCESSASSOC(nested, "a", "b"));
        Assert.Null(GlobalProcs.LAZYACCESSASSOC(nested, "missing", "b"));
        Assert.Null(GlobalProcs.LAZYACCESSASSOC(null, "a", "b"));
    }

    [Fact]
    public void LazyOrAssocListAndLazyInsert()
    {
        using var w = new SmokeListsWorld();
        DmList L = null;
        GlobalProcs.LAZYORASSOCLIST(ref L, "k", 1f);
        GlobalProcs.LAZYORASSOCLIST(ref L, "k", 1f);
        GlobalProcs.LAZYORASSOCLIST(ref L, "k", 2f);
        Assert.Equal(new object[] { 1f, 2f }, Items((DmList)L["k"]));

        DmList ins = null;
        GlobalProcs.LAZYINSERT(ref ins, 1, "b");
        GlobalProcs.LAZYINSERT(ref ins, 1, "a");
        Assert.Equal(new object[] { "a", "b" }, Items(ins));
    }

    [Fact]
    public void LazyCopyDuplicateClearAndSanitize()
    {
        using var w = new SmokeListsWorld();
        DmList L = NewList(1f);
        Assert.NotSame(L, GlobalProcs.LAZYCOPY(L));
        Assert.Equal(0f, GlobalProcs.LAZYCOPY(null).len);   // LAZYCOPY never nulls
        Assert.Null(GlobalProcs.LAZYLISTDUPLICATE(null));
        Assert.Equal(new object[] { 1f }, Items(GlobalProcs.LAZYLISTDUPLICATE(L)));
        Assert.Same(L, GlobalProcs.SANITIZE_LIST(L));
        Assert.IsType<DmList>(GlobalProcs.SANITIZE_LIST("text"));
        GlobalProcs.LAZYCLEARLIST(L);
        Assert.Equal(0f, L.len);
        GlobalProcs.LAZYCLEARLIST(null);
    }

    [Fact]
    public void LazyMaxAndLazyMinFallBackToTheDefault()
    {
        // The tg doc comments of the two are swapped; LAZYMAX is max().
        using var w = new SmokeListsWorld();
        Assert.Equal(9f, GlobalProcs.LAZYMAX(NewList(3f, 9f, 1f), 0f));
        Assert.Equal(1f, GlobalProcs.LAZYMIN(NewList(3f, 9f, 1f), 0f));
        Assert.Equal("d", GlobalProcs.LAZYMAX(null, "d"));
    }

    [Fact]
    public void UntypedListAddWrapsListsAndKeyByIndexChecksBounds()
    {
        using var w = new SmokeListsWorld();
        DmList L = Dm.list();
        DmList inner = NewList(1f, 2f);
        GlobalProcs.UNTYPED_LIST_ADD(ref L, inner);
        Assert.Equal(new object[] { inner }, Items(L));
        GlobalProcs.UNTYPED_LIST_REMOVE(ref L, inner);
        Assert.Equal(0f, L.len);
        Assert.Equal("b", GlobalProcs.KEYBYINDEX(NewList("a", "b"), 2));
        Assert.Null(GlobalProcs.KEYBYINDEX(NewList("a", "b"), 3));
        Assert.Null(GlobalProcs.KEYBYINDEX(NewList("a", "b"), 0));
    }

    [Fact]
    public void QdelLazyListQdelsEveryItemAndNullsTheList()
    {
        // _lists.dm:86: for(var/I in L) qdel(I); L = null;
        using var w = new SmokeListsWorld();
        Dm.@new<datum_controller_subsystem_garbage>(); // SSgarbage, as the MC would create it (NEW_SS_GLOBAL)
        datum a = Dm.@new<datum>();
        datum b = Dm.@new<datum>();
        DmList L = NewList(a, b);
        GlobalProcs.QDEL_LAZYLIST(ref L);
        Assert.Null(L);
        Assert.True(GlobalProcs.QDELETED(a));
        Assert.True(GlobalProcs.QDELETED(b));
        DmList none = null;
        GlobalProcs.QDEL_LAZYLIST(ref none);                 // a null list: nothing to qdel
        Assert.Null(none);
        Assert.Empty(w.Errors);
    }
}

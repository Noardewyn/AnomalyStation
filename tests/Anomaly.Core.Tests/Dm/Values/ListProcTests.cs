using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// The list procs and the list core (docs/ai/translation-rules.md 6.1, 6.2): BYOND ref <c>/list/proc/*</c>, "list
/// associations", <c>/list/var/len</c>, <c>/proc/alist</c>; where the reference is silent, OpenDream's
/// <c>DreamList</c> / <c>DreamProcNativeList</c> and its BYOND-checked tests (Content.Tests/DMProject/Tests/List).
/// </summary>
public class ListProcTests
{
    [Fact]
    public void AddAppendsAndFlattensListArguments()
    {
        // BYOND ref /list/proc/Add: "If an argument is itself a list, each item in the list will be added."
        DmList L = Dm.list(1f, 2f);
        L.Add(3f, Dm.list(4f, 5f));
        V.Items(L, 1f, 2f, 3f, 4f, 5f);
        L.Add(null);                                  // a null params array is one null item
        V.Items(L, 1f, 2f, 3f, 4f, 5f, null);
    }

    [Fact]
    public void AddCarriesAssociatedValuesOfAListArgument()
    {
        // As L += L2 does (OpenDream test AssocListAdd: L1 += L2 then L1["B"] == 2).
        DmList L = Dm.list("A");
        L.Add(Dm.list(Dm.kv("B", 2f)));
        V.Items(L, "A", "B");
        V.Eq(2f, L["B"]);
    }

    [Fact]
    public void NumbersAreOneNumberTypeWhateverTheirBox()
    {
        // DM has one number type (rules 3.1): 1 and 1.0 are the same item and the same key; keys made by association
        // and alist keys are stored as float.
        DmList L = Dm.list();
        L.Add(5, 2.5d, 3L, (short)4);
        V.Items(L, 5f, 2.5f, 3f, 4f);
        Assert.Equal(1f, L.Find(5f));
        Assert.Equal(2f, L.Find(2.5f));
        Assert.True(Dm.@in(4f, L));
        DmList A = Dm.alist(Dm.kv(1, "one"));
        Assert.Equal("one", A[1.0d]);
        Assert.Equal("one", A[(object)1f]);
        A[1.0d] = "uno";                              // the same key, not a second one
        Assert.Equal(1, A.Count);
        Assert.IsType<float>(Dm.each(A).Single());
        A.Add(2);
        Assert.IsType<float>(A._items[1]);
        DmList keyed = Dm.list();
        keyed[(object)"k"] = 1;
        V.Error(() => keyed[(object)7d] = 0);          // a number in any box is an index, never a key
        V.Eq(1f, keyed["k"]);
    }

    [Fact]
    public void DuplicatesAreAllowedAndShareOneAssociation()
    {
        // BYOND ref "list associations": "Each unique text string or object in a list may be associated with another
        // value"; OpenDream test AssocListIntoKey (Add("a") twice, then L["a"] = 321: len 2, L[2] == "a").
        DmList L = Dm.list();
        L.Add("a");
        L["a"] = 123f;
        L.Add("a");
        L["a"] = 321f;
        Assert.Equal(2f, L.len);
        Assert.Equal("a", L[2]);
        V.Eq(321f, L["a"]);
    }

    [Fact]
    public void RemoveTakesTheLastOccurrence()
    {
        // BYOND ref /list/proc/Remove: "Removal starts at the end of the list (highest index) so that this operation
        // is an exact reversal of Add()"; "1 if any items removed, 0 if not".
        DmList L = Dm.list(1f, 2f, 1f);
        Assert.Equal(1f, L.Remove(1f));
        V.Items(L, 1f, 2f);
        Assert.Equal(0f, L.Remove(9f));
        L = Dm.list(1f, 2f, 1f, 2f);
        L.Remove(Dm.list(1f, 2f));
        V.Items(L, 1f, 2f);
        L = Dm.list("a", "b");
        Assert.Equal(1f, L.Remove(L));                // the argument's items are read first
        Assert.Equal(0f, L.len);
    }

    [Fact]
    public void RemoveAllTakesEveryCopyAndCountsThem()
    {
        // BYOND ref /list/proc/RemoveAll: "Removes all copies of the specified items ... Returns: Number of items removed."
        DmList L = Dm.list(1f, 2f, 1f, 3f, 1f);
        Assert.Equal(3f, L.RemoveAll(1f));
        V.Items(L, 2f, 3f);
        L = Dm.list(1f, 2f, 3f, 2f, 1f);
        Assert.Equal(4f, L.RemoveAll(Dm.list(1f, 2f)));
        V.Items(L, 3f);
    }

    [Fact]
    public void CutRemovesARangeAndTheAssociationsOfItsKeys()
    {
        // BYOND ref /list/proc/Cut; OpenDream test ListCut: list(a = 10, b = 20).Cut(1, 2) leaves list("b" = 20).
        DmList A = Dm.list(1f, 2f, 3f);
        A.Cut();
        Assert.Equal(0f, A.len);
        A = Dm.list(Dm.kv("a", 10f), Dm.kv("b", 20f));
        A.Cut(1, 2);
        Assert.True(Dm.equiv(A, Dm.list(Dm.kv("b", 20f))));
        Assert.Null(A["a"]);
        A = Dm.list(1f, 2f, 3f);
        A.Cut(2);
        V.Items(A, 1f);
        A = Dm.list(1f, 2f, 3f);
        A.Cut(0, 2);                                  // Start 0 is 1 (OpenDream DreamList.Cut)
        V.Items(A, 2f, 3f);
        A.Cut(5, 9);                                  // nothing there
        V.Items(A, 2f, 3f);
        V.Error(() => A.Cut(-1));
    }

    [Fact]
    public void CopyTakesARangeWithItsAssociations()
    {
        // BYOND ref /list/proc/Copy: "Copy list[Start] through list[End-1] into a new list"; OpenDream CreateCopy
        // raises "list index out of bounds" past len+1.
        DmList L = Dm.list(Dm.kv("a", 1f), "b", 3f);
        DmList all = L.Copy();
        Assert.NotSame(L, all);
        Assert.True(Dm.equiv(L, all));
        V.Eq(1f, all["a"]);
        V.Items(L.Copy(2), "b", 3f);
        V.Items(L.Copy(2, 3), "b");
        V.Items(L.Copy(3, 2));
        V.Error(() => L.Copy(5));
        V.Error(() => L.Copy(1, 5));
        all["a"] = 9f;                                // a copy is independent
        V.Eq(1f, L["a"]);
    }

    [Fact]
    public void FindSearchesStartToEndMinusOne()
    {
        // OpenDream test ListFind (every assertion of it with numeric bounds).
        DmList L = Dm.list("a", "b", "c");
        Assert.Equal(2f, L.Find("b"));
        Assert.Equal(2f, L.Find("b", 1));
        Assert.Equal(2f, L.Find("b", 2));
        Assert.Equal(0f, L.Find("b", 3));
        Assert.Equal(2f, L.Find("b", 1, 0));
        Assert.Equal(2f, L.Find("b", 2, 0));
        Assert.Equal(0f, L.Find("b", 3, 0));
        Assert.Equal(0f, L.Find("b", 1, 1));
        Assert.Equal(0f, L.Find("b", 1, 2));
        Assert.Equal(2f, L.Find("b", 1, 3));
        Assert.Equal(0f, L.Find("z"));
    }

    [Fact]
    public void InsertPutsItemsBeforeTheIndexAndReturnsTheIndexAfterThem()
    {
        // BYOND ref /list/proc/Insert: "Returns: The index following the inserted items"; "Using Index=0 or
        // Index=list.len+1 is the same as adding to the list"; list arguments insert their elements.
        DmList L = Dm.list(1f, 2f, 3f);
        Assert.Equal(3f, L.Insert(2, "x"));
        V.Items(L, 1f, "x", 2f, 3f);
        Assert.Equal(6f, L.Insert(0, "y"));
        V.Items(L, 1f, "x", 2f, 3f, "y");
        Assert.Equal(4f, L.Insert(2, Dm.list("p", Dm.kv("q", 7f))));
        V.Items(L, 1f, "p", "q", "x", 2f, 3f, "y");
        V.Eq(7f, L["q"]);                             // associations come along
        V.Error(() => L.Insert(20, "z"));
        V.Error(() => L.Insert(1));                   // nothing to insert
    }

    [Fact]
    public void JoinFollowsJointextPositions()
    {
        // OpenDream test ListJoin (its numeric-argument assertions).
        DmList L = Dm.list(1f, 2f, 3f, 4f, 5f);
        Assert.Equal("1;2;3;4;5", L.Join(";"));
        Assert.Equal("1_2_3_4_5", L.Join("_"));
        Assert.Equal("12345", L.Join(null));
        Assert.Equal("2;3;4;5", L.Join(";", 2));
        Assert.Equal("4;5", L.Join(";", 4));
        Assert.Equal("", L.Join(";", 0));
        Assert.Equal("3;4;5", L.Join(";", -3));
        Assert.Equal("1;2;3;4;5", L.Join(";", 1, 0));
        Assert.Equal("2;3", L.Join(";", -4, -2));
        Assert.Equal("abc", Dm.list("a", "b", "c").Join(""));   // OpenDream test ListDerefJoin
    }

    [Fact]
    public void SwapKeepsAssociationsWithTheirKeys()
    {
        // BYOND ref /list/proc/Swap example: list("orange" = 3, "green" = 2, "blue" = 5).Swap(1, 3).
        DmList L = Dm.list(Dm.kv("orange", 3f), Dm.kv("green", 2f), Dm.kv("blue", 5f));
        L.Swap(1, 3);
        V.Items(L, "blue", "green", "orange");
        V.Eq(5f, L["blue"]);
        V.Eq(2f, L["green"]);
        V.Eq(3f, L["orange"]);
        V.Error(() => L.Swap(0, 1));
        V.Error(() => L.Swap(1, 4));
    }

    [Fact]
    public void SpliceCutsAndInserts()
    {
        // BYOND ref /list/proc/Splice example and its index rules: negative counts from the end, out of range is
        // clamped, an End before Start swaps them; OpenDream test ListSplice (a list argument inserts its items).
        DmList L = Dm.list("apple", "banana", "orange", "pear");
        L.Splice(2, 4, "firetruck", "camel", "joystick", "balloon");
        V.Items(L, "apple", "firetruck", "camel", "joystick", "balloon", "pear");
        L = Dm.list("foo", "bar", "test", "value");
        L.Splice(2, 4, Dm.list("lorem", "ipsum", "word", "another word"));
        V.Items(L, "foo", "lorem", "ipsum", "word", "another word", "value");
        L = Dm.list(1f, 2f, 3f, 4f);
        L.Splice(-2, 0, "x");
        V.Items(L, 1f, 2f, "x");
        L = Dm.list(1f, 2f, 3f, 4f);
        L.Splice(3, 1, "x");
        V.Items(L, "x", 3f, 4f);
        L = Dm.list(1f, 2f);
        L.Splice(10, 20, "x");
        V.Items(L, 1f, 2f, "x");
    }

    [Fact]
    public void LenPadsWithNullsAndListNTruncatesItsSize()
    {
        // BYOND ref /list: "L.len = 7 // expand list ... L contains: (1,2,3,4,5,null,null)"; OpenDream test
        // DimensionalListSizes: new /list(-1) and var/L[-0.5] are empty, var/L[1.5] has one item.
        DmList L = Dm.list(1f, 2f, 3f, 4f, 5f);
        L.len = 7;
        V.Items(L, 1f, 2f, 3f, 4f, 5f, null, null);
        Assert.Equal(0f, Dm.list_n(-1).len);
        Assert.Equal(0f, Dm.list_n(-0.5f).len);
        Assert.Equal(1f, Dm.list_n(1.5f).len);
        Assert.Equal(0f, Dm.list_n(float.NaN).len);
    }

    [Fact]
    public void AnAlistKeepsUniqueKeysAndRefusesPositionalChanges()
    {
        // BYOND ref /proc/alist ("Keys can't be repeated"); OpenDream alist tests AListAdd, AListCopy, AListBadCopy,
        // AListCut, AListFind, AListInsert, AListJoin, AListLen1-4, AListRemove, AListRemoveAll, AListSplice,
        // AListSwap, AListValidKeys.
        DmList AL = Dm.alist(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", -4f));
        AL.Add("c", "d");
        V.Eq(-4f, AL["c"]);
        Assert.Null(AL["d"]);
        Assert.Equal(4f, Dm.length(AL));
        AL.Add(Dm.list("c", "d"));
        Assert.Equal(4f, Dm.length(AL));
        Assert.Equal(1f, AL.Find("a"));
        Assert.Equal("a,b,c,d", AL.Join(","));
        DmList copy = AL.Copy();
        V.Eq(1f, copy["a"]);
        Assert.Equal(4f, copy.len);
        V.Error(() => AL.Copy(1, 2));
        V.Error(() => AL.Insert(2, "e"));
        V.Error(() => AL.Splice(1, 1, "e"));
        V.Error(() => AL.Swap(1, 1));
        V.Error(() => AL.len = 1);
        AL.len = 4;                                   // its own length is no change
        Assert.Equal(1f, AL.Remove("a", "b"));
        Assert.False(Dm.@in("a", AL));
        Assert.True(Dm.@in("c", AL));
        AL.len = 0;
        Assert.Equal(0f, Dm.length(AL));

        var datum = new object();
        DmList keys = Dm.alist();
        keys[datum] = 1f;
        keys["A"] = 2f;
        keys[5] = 3f;
        keys[(object)null] = 5f;
        V.Eq(1f, keys[datum]);
        V.Eq(2f, keys["A"]);
        V.Eq(3f, keys[5]);
        V.Eq(5f, keys[(object)null]);
    }

    [Fact]
    public void MembershipUsesTheIndexOnLargeLists()
    {
        // BYOND ref "in": "1 if A exists in List"; the port answers it from the occurrence index (O(1)).
        DmList L = Dm.list();
        for (int i = 0; i < 1000; i++) L.Add("item" + i);
        Assert.True(Dm.@in("item999", L));
        Assert.NotNull(L._index);
        Assert.False(Dm.@in("item1000", L));
        L.Cut(1, 500);
        Assert.False(Dm.@in("item0", L));
        Assert.True(Dm.@in("item500", L));
        Assert.Equal(1f, L.Find("item499"));
    }

    [Fact]
    public void ALoopKeepsItsSnapshotWhateverProcChangesTheList()
    {
        // BYOND ref /proc/for/list: "you're actually looping through a copy of that list" (rules 6.5); every list
        // proc and operator goes through the copy-on-write primitives.
        Action<DmList>[] mutations =
        {
            l => l.Add("x"),
            l => l.Remove(2f),
            l => l.RemoveAll(1f),
            l => l.Cut(1, 3),
            l => l.Insert(1, "y"),
            l => l.Splice(1, 2, "z", "w"),
            l => l.Swap(1, 3),
            l => l.len = 1,
            l => l[1] = "q",
            l => l["key"] = 9f,
            l => { object o = l; Dm.remove(ref o, 3f); },
            l => Dm.mask(l, Dm.list(1f)),
            l => Dm.bitxor_assign(l, Dm.list(2f, 7f)),
            l => Dm.combine(l, Dm.list(8f)),
            l => Dm.append(l, l),
        };
        foreach (Action<DmList> mutate in mutations)
        {
            DmList L = Dm.list(1f, 2f, 3f);
            var seen = new List<object>();
            foreach (object item in Dm.each(L))
            {
                seen.Add(item);
                mutate(L);
            }
            Assert.Equal(new object[] { 1f, 2f, 3f }, seen);
        }
    }

    [Fact]
    public void TheIndexAndTheAssociationsStayExactAfterEveryMutationPath()
    {
        // A long deterministic run of every list mutation against a naive model of BYOND's semantics: the items in
        // order, every key's association (BYOND ref "list associations": a key's value lives while the key occurs),
        // and membership, checked after every step; the occurrence index must count exactly.
        var keys = new object[] { "a", "b", "c", "d", null, 1f, 2f, 3f, new object(), new object() };
        DmList L = Dm.list();
        var items = new List<object>();
        var assoc = new List<(object Key, object Value)>();
        uint state = 12345;
        int Next(int n)
        {
            state = state * 1664525 + 1013904223;
            return (int)((state >> 8) % (uint)n);
        }
        object AnyKey() => keys[Next(keys.Length)];
        object TextKey() => keys[Next(5)];
        void DropDeadAssociations() => assoc.RemoveAll(p => !items.Any(i => Dm.eq(i, p.Key)));
        void SetAssoc(object key, object value)
        {
            int at = assoc.FindIndex(p => Dm.eq(p.Key, key));
            if (at >= 0) assoc[at] = (key, value);
            else assoc.Add((key, value));
        }
        int LastIndex(object x) => items.FindLastIndex(i => Dm.eq(i, x));

        for (int step = 0; step < 4000; step++)
        {
            int op = Next(14);
            switch (op)
            {
                case 0:
                {
                    object x = AnyKey();
                    L.Add(x);
                    items.Add(x);
                    break;
                }
                case 1:
                {
                    object x = AnyKey();
                    L.Remove(x);
                    int at = LastIndex(x);
                    if (at >= 0) items.RemoveAt(at);
                    break;
                }
                case 2:
                {
                    object x = AnyKey();
                    L.RemoveAll(x);
                    items.RemoveAll(i => Dm.eq(i, x));
                    break;
                }
                case 3 when items.Count > 0:
                {
                    int a = Next(items.Count) + 1, b = a + Next(3);
                    L.Cut(a, b);
                    items.RemoveRange(a - 1, Math.Min(b, items.Count + 1) - a);
                    break;
                }
                case 4:
                {
                    int at = Next(items.Count + 1) + 1;
                    object x = AnyKey();
                    L.Insert(at, x);
                    items.Insert(at - 1, x);
                    break;
                }
                case 5 when items.Count > 0:
                {
                    int i = Next(items.Count) + 1, j = Next(items.Count) + 1;
                    L.Swap(i, j);
                    (items[i - 1], items[j - 1]) = (items[j - 1], items[i - 1]);
                    break;
                }
                case 6 when items.Count > 0:
                {
                    int i = Next(items.Count) + 1;
                    object x = AnyKey();
                    L[i] = x;
                    items[i - 1] = x;
                    break;
                }
                case 7:
                {
                    object k = TextKey();
                    float v = Next(100);
                    L[k] = v;
                    if (!items.Any(i => Dm.eq(i, k))) items.Add(k);
                    SetAssoc(k, v);
                    break;
                }
                case 8:
                {
                    int n = Next(items.Count + 3);
                    L.len = n;
                    if (n < items.Count) items.RemoveRange(n, items.Count - n);
                    while (items.Count < n) items.Add(null);
                    break;
                }
                case 9 when items.Count > 0:
                {
                    int a = Next(items.Count) + 1, b = a + Next(3);
                    object x = AnyKey();
                    L.Splice(a, b, x);
                    int end = Math.Min(b, items.Count + 1);
                    items.RemoveRange(a - 1, end - a);
                    DropDeadAssociations();   // Splice is Cut, then Insert (BYOND ref): a cut key loses its value
                    items.Insert(a - 1, x);
                    break;
                }
                case 10:
                {
                    object x = AnyKey();
                    Dm.remove(L, Dm.list(x));
                    int at = LastIndex(x);
                    if (at >= 0) items.RemoveAt(at);
                    break;
                }
                case 11:
                {
                    object x = AnyKey();
                    Dm.combine(L, Dm.list(x));
                    if (!items.Any(i => Dm.eq(i, x))) items.Add(x);
                    break;
                }
                case 12 when Next(8) == 0:
                {
                    object x = AnyKey(), y = AnyKey();
                    DmList mask = Dm.list(x, y);
                    Dm.mask(L, mask);
                    var remaining = new List<object> { x, y };
                    var kept = new List<object>();
                    foreach (object i in items)
                    {
                        int at = remaining.FindIndex(r => Dm.eq(r, i));
                        if (at < 0) continue;
                        remaining.RemoveAt(at);
                        kept.Add(i);
                    }
                    items = kept;
                    break;
                }
                case 13:
                {
                    object x = AnyKey();
                    Dm.bitxor_assign(L, x);
                    int first = items.FindIndex(i => Dm.eq(i, x));
                    if (first >= 0) items.RemoveAt(first);
                    else items.Add(x);
                    break;
                }
            }
            DropDeadAssociations();

            Assert.Equal(items.Count, L.Count);
            for (int i = 0; i < items.Count; i++) Assert.True(Dm.eq(items[i], L._items[i]), "item " + (i + 1) + " at step " + step);
            foreach (object k in keys)
            {
                Assert.True(items.Any(i => Dm.eq(i, k)) == Dm.@in(k, L), "membership of " + k + " at step " + step + " after op " + op);
                if (Dm.isnum(k)) continue;
                int at = assoc.FindIndex(p => Dm.eq(p.Key, k));
                Assert.True(Dm.eq(at >= 0 ? assoc[at].Value : null, L[k]), "association of " + k + " at step " + step + " after op " + op);
            }
            if (L._index != null)
            {
                int counted = L._index.Values.Sum();
                Assert.Equal(items.Count, counted);
            }
        }
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Contracts;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// List operators, the in-place compound assignments, <c>in</c> and the loop sources (docs/ai/translation-rules.md 4.3,
/// 4.7, 6.3, 6.5): BYOND ref "list operators", <c>+</c>, <c>-</c>, <c>|</c>, <c>&amp;</c>, <c>^</c> and their
/// assignment forms, <c>in</c>, <c>/proc/for/list</c>; OpenDream <c>DreamList</c> operators and its BYOND-checked tests
/// (Operators/list_set_pair_preservation, List/*, List/alist/*, the Addition and Subtraction tables).
/// </summary>
public class ListOperatorTests
{
    [Fact]
    public void MinusRemovesTheLastOccurrence()
    {
        // BYOND ref /list/proc/Remove ("Removal starts at the end of the list"); OpenDream DreamList.RemoveValue
        // (LastIndexOf); rules 6.3.
        object result = Dm.sub(Dm.list(1f, 2f, 1f), 1f);
        V.Items((DmList)result, 1f, 2f);
        DmList L = Dm.list("a", "b", "a", "b");
        Dm.remove(ref L, Dm.list("a"));
        V.Items(L, "a", "b", "b");
        object o = L;
        Dm.remove(ref o, "b");
        V.Items(L, "a", "b");
        Assert.Same(L, o);                            // -= changes the list in place
    }

    [Fact]
    public void SelfOperandsAreReadBeforeTheListChanges()
    {
        // rules 6.3: "the right operand is snapshotted first, so L -= L empties L"; OpenDream test ListAppendSelf.
        DmList L = Dm.list(1f, 2f);
        Dm.append(ref L, L);
        V.Items(L, 1f, 2f, 1f, 2f);
        DmList L2 = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 2f));
        Dm.append(ref L2, L2);
        Assert.True(Dm.equiv(L2, Dm.list(Dm.kv("a", 1f), Dm.kv("b", 2f), "a", "b")));
        Dm.remove(ref L, L);
        Assert.Equal(0f, L.len);
        DmList L3 = Dm.list(1f, 2f);
        Dm.bitxor_assign(ref L3, L3);
        Assert.Equal(0f, L3.len);
        DmList L4 = Dm.list(1f, 1f);
        Dm.mask(ref L4, L4);
        V.Items(L4, 1f, 1f);
    }

    [Fact]
    public void PlusCarriesAssociatedValues()
    {
        // OpenDream test AssocListAdd.
        DmList L1 = Dm.list(Dm.kv("A", 1f));
        DmList L2 = Dm.list(Dm.kv("B", 2f));
        V.Eq(2f, ((DmList)Dm.add(L1, L2))["B"]);
        Dm.append(ref L1, L2);
        V.Eq(2f, L1["B"]);
        Assert.Equal(2f, L1.len);
        Dm.append(ref L1, L2);                        // "Lists are fun": B is added again
        Assert.Equal("B", L1[3]);
        Assert.Equal(3f, L1.len);
    }

    [Fact]
    public void PlusAndMinusFollowTheBinaryOperatorTables()
    {
        // OpenDream Operators/Addition and Subtraction tables, row list("ABC").
        var datum = new TestDatum();
        V.Items((DmList)Dm.add(Dm.list("ABC"), 10f), "ABC", 10f);
        V.Items((DmList)Dm.add(Dm.list("ABC"), "ABC"), "ABC", "ABC");
        V.Items((DmList)Dm.add(Dm.list("ABC"), null), "ABC", null);
        V.Items((DmList)Dm.add(Dm.list("ABC"), Dm.list("ABC")), "ABC", "ABC");
        V.Items((DmList)Dm.add(Dm.list("ABC"), datum), "ABC", datum);
        V.Items((DmList)Dm.add(Dm.list("ABC"), typeof(DmList)), "ABC", typeof(DmList));
        V.Items((DmList)Dm.sub(Dm.list("ABC"), 10f), "ABC");
        V.Items((DmList)Dm.sub(Dm.list("ABC"), "ABC"));
        V.Items((DmList)Dm.sub(Dm.list("ABC"), null), "ABC");
        V.Items((DmList)Dm.sub(Dm.list("ABC"), Dm.list("ABC")));
        DmList original = Dm.list("ABC");
        Assert.NotSame(original, Dm.add(original, 1f));   // + makes a new list
        V.Items(original, "ABC");
    }

    [Fact]
    public void RemovingAKeyRemovesItsValue()
    {
        // OpenDream test RemoveKey.
        DmList L = Dm.list(Dm.kv("foo", "bar"));
        Dm.remove(ref L, "foo");
        Assert.Null(L["foo"]);
    }

    [Fact]
    public void OrAddsTheMissingItemsAndKeepsExistingValues()
    {
        // BYOND ref | ("list(1,2) | list(2,3) is equivalent to list(1,2,3)"); OpenDream tests AssocListCombine and
        // list_set_pair_preservation (or_left | list("b" = 200, "c" = 300)).
        V.Items((DmList)Dm.bitor(Dm.list(1f, 2f), Dm.list(2f, 3f)), 1f, 2f, 3f);
        DmList L = Dm.list("A");
        Dm.combine(ref L, Dm.list(Dm.kv("B", 1f)));
        Assert.True(Dm.equiv(L, Dm.list("A", Dm.kv("B", 1f))));
        L = Dm.list("A");
        Dm.combine(ref L, Dm.list(Dm.kv("A", 1f)));
        Assert.True(Dm.equiv(L, Dm.list("A")));
        Assert.Null(L["A"]);                          // "If the key already exists, it won't copy the associated value"
        DmList left = Dm.list(Dm.kv("a", 10f), Dm.kv("b", 20f));
        var or = (DmList)Dm.bitor(left, Dm.list(Dm.kv("b", 200f), Dm.kv("c", 300f)));
        V.Items(or, "a", "b", "c");
        V.Eq(10f, or["a"]);
        V.Eq(20f, or["b"]);
        V.Eq(300f, or["c"]);
        V.Eq(20f, left["b"]);
        DmList one = Dm.list(1f);
        Dm.combine(ref one, 1f);
        V.Items(one, 1f);                             // a present item is not added again
    }

    [Fact]
    public void AndKeepsTheItemsInBothWithTheLeftValues()
    {
        // BYOND ref & ("only items that were in both lists, in the order of list A"); OpenDream test
        // list_set_pair_preservation (intersections keep min(left, right) copies; &= keeps the same list object).
        DmList numeric = Dm.list(7f, 1f, 7f, 9f);
        V.Items((DmList)Dm.bitand(numeric, Dm.list(7f, 9f)), 7f, 9f);
        V.Items(numeric, 7f, 1f, 7f, 9f);
        V.Items((DmList)Dm.bitand(numeric, 7f), 7f);
        DmList assoc = Dm.list(Dm.kv("a", 10f), Dm.kv("b", 20f), Dm.kv("c", 30f));
        var mask = (DmList)Dm.bitand(assoc, Dm.list("c", "a"));
        V.Items(mask, "a", "c");
        V.Eq(10f, mask["a"]);
        V.Eq(30f, mask["c"]);
        Assert.False(Dm.@in("b", mask));
        DmList inPlace = Dm.list(7f, 1f, 7f, 9f);
        object target = inPlace;
        Dm.mask(ref target, Dm.list(7f, 9f));
        Assert.Same(inPlace, target);
        V.Items(inPlace, 7f, 9f);
        Assert.True(Dm.equiv(Dm.bitand(Dm.list(1f, 1f, 1f), Dm.list(1f, 1f)), Dm.list(1f, 1f)));
    }

    [Fact]
    public void XorKeepsTheItemsInOnlyOneList()
    {
        // BYOND ref ^ ("The items found only in A come first in the result, followed by any items found only in B");
        // ^= and the scalar toggle: OpenDream test list_set_pair_preservation.
        V.Items((DmList)Dm.bitxor(Dm.list(1f, 2f), Dm.list(2f, 3f)), 1f, 3f);
        V.Items((DmList)Dm.bitxor(Dm.list(7f, 1f, 7f), Dm.list(1f, 9f)), 7f, 7f, 9f);
        var assoc = (DmList)Dm.bitxor(Dm.list(Dm.kv("a", 10f), Dm.kv("b", 20f)), Dm.list(Dm.kv("b", 200f), Dm.kv("c", 300f)));
        V.Items(assoc, "a", "c");
        V.Eq(10f, assoc["a"]);
        V.Eq(300f, assoc["c"]);
        DmList L = Dm.list(7f, 1f, 7f);
        object target = L;
        Dm.bitxor_assign(ref target, Dm.list(1f, 9f));
        Assert.Same(L, target);
        V.Items(L, 7f, 7f, 9f);
        L = Dm.list(7f, 1f, 7f);
        Dm.bitxor_assign(ref L, 7f);                  // toggles the first copy
        V.Items(L, 1f, 7f);
        L = Dm.list(7f, 1f, 7f);
        Dm.bitxor_assign(ref L, 9f);
        V.Items(L, 7f, 1f, 7f, 9f);
        DmList pairs = Dm.list(Dm.kv("a", 10f), Dm.kv("b", 20f));
        Dm.bitxor_assign(ref pairs, "b");
        Assert.Equal(1f, pairs.len);
        V.Eq(10f, pairs["a"]);
        pairs = Dm.list(Dm.kv("a", 10f), Dm.kv("b", 20f));
        Dm.bitxor_assign(ref pairs, "c");
        Assert.Equal(3f, pairs.len);
        Assert.True(Dm.@in("c", pairs));
        Assert.Null(pairs["c"]);
    }

    [Fact]
    public void ANullTargetBecomesTheListItself()
    {
        // rules 6.3: "With a null left operand, L += x makes L refer to x itself (the same list, not a copy) and
        // L |= x likewise"; a DmList target that would become a non-list raises.
        DmList source = Dm.list(1f);
        object o = null;
        Dm.append(ref o, source);
        Assert.Same(source, o);
        DmList L = null;
        Dm.append(ref L, source);
        Assert.Same(source, L);
        L = null;
        Dm.combine(ref L, source);
        Assert.Same(source, L);
        L = null;
        Dm.append(ref L, null);
        Assert.Null(L);
        V.Error(() => { DmList none = null; Dm.append(ref none, 5f); });
        V.Error(() => { DmList none = null; Dm.remove(ref none, 5f); });
        V.Error(() => Dm.append((DmList)null, 5f));
    }

    [Fact]
    public void EngineListsChangeInPlace()
    {
        // rules 4.3: A.overlays += x is Dm.append(A.overlays, x), without ref; the list object never changes.
        var atom = new TestAtom();
        DmList contents = atom.contents;
        Dm.append(atom.contents, Dm.list("a", "b"));
        Dm.remove(atom.contents, "a");
        Dm.combine(atom.contents, "c");
        Dm.mask(atom.contents, Dm.list("b", "c"));
        Dm.bitxor_assign(atom.contents, "d");
        Assert.Same(contents, atom.contents);
        V.Items(atom.contents, "b", "c", "d");
    }

    [Fact]
    public void ElementCompoundAssignmentsChangeInnerListsInPlace()
    {
        // rules 4.3 (tg LAZYADDASSOCLIST, __HELPERS/_lists.dm:80: L[K] += list(V)) and master.dm:300's
        // ++failure_strikes[type]; OpenDream tests pre_increment / pre_decrement (++list_values[1] == 1 for text).
        DmList L = Dm.list();
        Dm.append_at(L, "k", Dm.list("v"));
        var inner = (DmList)L["k"];
        V.Items(inner, "v");
        Dm.append_at(L, "k", Dm.list("w"));
        Assert.Same(inner, L["k"]);
        V.Items(inner, "v", "w");
        Dm.remove_at(L, "k", "v");
        V.Items(inner, "w");
        Dm.combine_at(L, "k", "x");
        Dm.mask_at(L, "k", Dm.list("x"));
        V.Items(inner, "x");
        Dm.bitxor_assign_at(L, "k", "y");
        V.Items(inner, "x", "y");

        DmList counts = Dm.list();
        Dm.inc_at(counts, "x");
        V.Eq(1f, counts["x"]);
        Assert.Equal(1f, Dm.postinc_at(counts, "x"));
        V.Eq(2f, counts["x"]);
        Assert.Equal(3f, Dm.preinc_at(counts, "x"));
        Assert.Equal(3f, Dm.postdec_at(counts, "x"));
        Assert.Equal(1f, Dm.predec_at(counts, "x"));
        Dm.dec_at(counts, "x");
        V.Eq(0f, counts["x"]);
        DmList text = Dm.list("a", "b", "c");
        Assert.Equal(1f, Dm.preinc_at(text, 1));
        text = Dm.list("a", "b", "c");
        Assert.Equal(-1f, Dm.predec_at(text, 1));
        DmList numbers = Dm.list(1f, 2f, 3f);
        Dm.append_at(numbers, 2, 10f);
        V.Items(numbers, 1f, 12f, 3f);
        V.Error(() => Dm.inc_at(numbers, 4));           // a position past the end is out of bounds
    }

    [Fact]
    public void AlistOperatorsKeepUniqueKeys()
    {
        // OpenDream tests AListAddOp, AListAppend, AListCombine, AListOr, AListMask, AListRemoveOp, AListSubtract.
        DmList AL = Dm.alist(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", -4f));
        var sum = (DmList)Dm.add(AL, Dm.list("c", "e"));
        V.Eq(-4f, sum["c"]);
        Assert.True(Dm.@in("e", sum));
        Assert.Equal(4f, Dm.length(sum));
        sum = (DmList)Dm.add(AL, Dm.alist(Dm.kv("c", 6f), Dm.kv("p", 3f)));
        V.Eq(-4f, sum["c"]);
        V.Eq(3f, sum["p"]);
        Assert.Equal(4f, Dm.length(sum));
        Dm.append(ref AL, "A");
        Dm.append(ref AL, Dm.list("c", "e"));
        Dm.append(ref AL, Dm.alist(Dm.kv("c", 6f), Dm.kv("p", 3f)));
        V.Eq(-4f, AL["c"]);
        V.Eq(3f, AL["p"]);
        Assert.Equal(6f, Dm.length(AL));

        DmList masked = Dm.alist(Dm.kv("a", null), Dm.kv("b", null), Dm.kv("c", null));
        Dm.mask(ref masked, "a");
        Assert.Equal(1f, masked.len);
        Assert.True(Dm.@in("a", masked));
        DmList AL2 = Dm.alist(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 3f));
        Dm.mask(ref AL2, Dm.list("a", "b", "d"));
        Assert.Equal(2f, AL2.len);
        V.Eq(1f, AL2["a"]);
        Dm.mask(ref AL2, Dm.list(Dm.kv("a", 2f), Dm.kv("e", 4f)));
        Assert.Equal(1f, AL2.len);
        V.Eq(1f, AL2["a"]);                           // the left list's value stays

        DmList AL3 = Dm.alist(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 3f));
        object holder = AL3;
        Dm.remove(ref holder, AL3);
        Assert.Equal(0f, AL3.len);
        var left = (DmList)Dm.sub(Dm.alist(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 3f)), Dm.list("a", "b", "e"));
        Assert.Equal(1f, left.len);
        Assert.True(Dm.@in("c", left));
    }

    [Fact]
    public void AnAlistIsEquivalentToAListWithTheSameKeysAndValues()
    {
        // OpenDream test AListEquivalence.
        var datum = new TestDatum();
        DmList AL = Dm.alist(Dm.kv("a", null), Dm.kv("b", null), Dm.kv("c", null));
        DmList AL2 = Dm.alist(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 3f));
        Assert.True(Dm.equiv(AL, AL));
        Assert.False(Dm.equiv(AL, AL2));
        Assert.False(Dm.equiv(AL, datum));
        Assert.True(Dm.equiv(AL, Dm.list("a", "b", "c")));
        Assert.False(Dm.equiv(AL, Dm.list(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 3f))));
        Assert.False(Dm.equiv(AL, Dm.list("a", "b", "c", "d")));
        Assert.False(Dm.equiv(AL, Dm.list("a", "b", "d")));
    }

    [Fact]
    public void InTestsListsAtomsAndTheWorld()
    {
        // BYOND ref in: "the value of List is allowed to be a non-list value, such as null"; "List can also be an atom,
        // in which case A in List is equivalent to A in List.contents"; OpenDream test ValueInList.
        DmList L = Dm.list(1f, 2f, 3f);
        Assert.False(Dm.@in(4f, L));
        Assert.True(Dm.@in(3f, L));
        Assert.True(Dm.@in("k", Dm.list(Dm.kv("k", 1f))));
        Assert.False(Dm.@in(1f, Dm.list(Dm.kv("k", 1f))));   // values are not keys
        Assert.False(Dm.@in(1f, null));
        Assert.False(Dm.@in("a", "abc"));
        var box = new TestAtom("box");
        var apple = new TestAtom("apple");
        box.contents.AppendItem(apple);
        Assert.True(Dm.@in(apple, box));
        Assert.False(Dm.@in(box, apple));
        var w = new World(1);
        using (w.Enter())
        {
            var registered = new TestAtom("thing").Register<TestAtom>(w);
            Assert.True(Dm.@in(registered, Dm.world));
            Assert.False(Dm.@in(new TestAtom("loose"), Dm.world));
            Assert.False(Dm.@in(new TestThing().Register<TestThing>(w), Dm.world));   // a datum is not in world.contents
        }
    }

    [Fact]
    public void InToIsAnInclusiveNumericRange()
    {
        // rules 4.7: A in X to Y outside a for is Dm.in_to (inclusive; false for a non-number).
        Assert.True(Dm.in_to(12f, 12, 20));
        Assert.True(Dm.in_to(20f, 12, 20));
        Assert.False(Dm.in_to(20.5f, 12, 20));
        Assert.True(Dm.in_to((object)15, 12, 20));
        Assert.False(Dm.in_to((object)"15", 12, 20));
        Assert.False(Dm.in_to((object)null, 0, 5));
    }

    [Fact]
    public void EachAsFiltersByKind()
    {
        // BYOND ref /proc/for/list: "Type can be ... area, turf, mob, obj ORed together" (for(var/x as mob|obj in L)).
        var mob = new KindMob();
        var obj = new KindObj();
        var turf = new KindTurf();
        var area = new KindArea();
        DmList L = Dm.list(mob, obj, turf, area, "text", 5f, null, new TestAtom());
        Assert.Equal(new object[] { mob, obj }, Dm.each_as(L, DmAs.mob | DmAs.obj).ToArray());
        Assert.Equal(new object[] { turf, area }, Dm.each_as(L, DmAs.turf | DmAs.area).ToArray());
        Assert.Equal(new object[] { "text", 5f }, Dm.each_as(L, DmAs.text | DmAs.num).ToArray());
        Assert.Equal(new object[] { null }, Dm.each_as(L, DmAs.@null).ToArray());
        Assert.Equal(8, Dm.each_as(L, DmAs.anything).Count());
        Assert.Empty(Dm.each_as(null, DmAs.mob));
    }

    [Fact]
    public void PairsCopyKeysAndValuesWhenTheLoopStarts()
    {
        // BYOND 516 for(var/k, v in L) (rules 6.5: "values are copied with the keys").
        DmList L = Dm.list(Dm.kv("a", 1f), "b", 3f);
        var seen = new List<(object, object)>();
        foreach ((object k, object v) in Dm.pairs(L))
        {
            seen.Add((k, v));
            L["a"] = 100f;
            L["new"] = 5f;
        }
        Assert.Equal(new (object, object)[] { ("a", 1f), ("b", null), (3f, null) }, seen);
        var mob = new KindMob();
        DmList typed = Dm.list(Dm.kv(mob, "m"), "text", null);
        Assert.Equal(new (KindMob, object)[] { (mob, "m") }, Dm.pairs<KindMob>(typed).ToArray());
        Assert.Equal(3, Dm.pairs(typed).Count());
        Assert.Empty(Dm.pairs(null));
        var box = new TestAtom();
        box.contents.AppendItem("x");
        Assert.Equal(new (object, object)[] { ("x", null) }, Dm.pairs(box).ToArray());
    }

    [Fact]
    public void PlusEqualsAddsTheItemsOfAListOneLevelDeep()
    {
        // BYOND ref "+=" / /list/proc/Add: "If an argument is itself a list, each item in the list will be added" (one
        // level: an inner list is one item, the same list, not a copy); list(L2) nests (rules 6.1), so
        // L += list(L2) adds L2 itself as one item.
        DmList inner = Dm.list(1f, 2f);
        DmList L = Dm.list("x");
        Dm.append(ref L, Dm.list(inner));
        V.Items(L, "x", inner);
        Assert.Same(inner, L[2]);
        Dm.append(ref L, inner);
        V.Items(L, "x", inner, 1f, 2f);
        L.Add(Dm.list(Dm.list(inner)));
        Assert.Equal(5f, L.len);
        V.Items(Assert.IsType<DmList>(L[5]), inner);
        object o = null;
        Dm.append(ref o, inner);                         // a null target becomes the list itself (rules 6.3)
        Assert.Same(inner, o);
        Dm.append(ref o, 3f);
        V.Items(inner, 1f, 2f, 3f);                      // so += then changes that list in place
        DmList sum = (DmList)Dm.add(Dm.list(inner), Dm.list(inner));
        V.Items(sum, inner, inner);
    }

    [Fact]
    public void MinusEqualsTakesTheLastOccurrenceAndReadsItsOperandFirst()
    {
        // BYOND ref /list/proc/Remove: "Removal starts at the end of the list (highest index) so that this operation is
        // an exact reversal of Add()"; an operand that is the list itself is read before anything is removed, so
        // L -= L empties L and L - L is empty (OpenDream test Stdlib/List/remove: L.Remove(L)).
        DmList L = Dm.list(1f, "a", 1f, "b", 1f);
        Dm.remove(ref L, 1f);
        V.Items(L, 1f, "a", 1f, "b");
        Dm.remove(ref L, Dm.list(1f, 1f));
        V.Items(L, "a", "b");
        DmList K = Dm.list(Dm.kv("k", 1f), "k", "z");    // a duplicated key keeps its value while a copy is left
        Dm.remove(ref K, "k");
        V.Items(K, "k", "z");
        V.Eq(1f, K["k"]);
        Dm.remove(ref K, "k");
        Assert.Null(K["k"]);
        DmList self = Dm.list(1f, 2f, 1f, 3f);
        DmList difference = (DmList)Dm.sub(self, self);
        Assert.Equal(0f, difference.len);
        V.Items(self, 1f, 2f, 1f, 3f);
        Dm.remove(ref self, self);
        Assert.Equal(0f, self.len);
        DmList twice = Dm.list(1f, 2f);
        object alias = twice;
        Dm.append(ref alias, twice);                     // L += L doubles L (OpenDream test ListAppendSelf)
        V.Items(twice, 1f, 2f, 1f, 2f);
        Dm.remove(ref alias, twice);
        Assert.Equal(0f, twice.len);
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// <c>for (x in L)</c> loops (docs/ai/translation-rules.md 6.5): BYOND ref <c>/proc/for/list</c>: "When you loop through a
/// list, with the exception of looping through world, you're actually looping through a copy of that list"; a typed
/// loop var skips inappropriate elements; <c>world</c> means <c>world.contents</c> (BYOND ref
/// <c>/world/var/contents</c>).
/// </summary>
public class EachTests
{
    private static List<object> All(object source) => Dm.each(source).ToList();

    [Fact]
    public void NullAndNonListsIterateNothing()
    {
        Assert.Empty(All(null));
        Assert.Empty(All("text"));
        Assert.Empty(All(5f));
        Assert.Empty(Dm.each<TestAtom>(null).ToList());
    }

    [Fact]
    public void AnUntypedLoopSeesEveryItemNullsIncluded()
    {
        DmList L = Dm.list("a", null, 3f);
        Assert.Equal(new object[] { "a", null, 3f }, All(L));
    }

    [Fact]
    public void ALoopWalksTheItemsAsTheyWereWhenItStarted()
    {
        DmList L = Dm.list(1f, 2f, 3f);
        var seen = new List<object>();
        foreach (object x in Dm.each(L))
        {
            seen.Add(x);
            if (Dm.eq(x, 1f))
            {
                L[3] = 30f;          // a write to an item not reached yet
                L.len = 1;           // and a shrink
                L[(object)"new"] = 4f;
            }
        }
        Assert.Equal(new object[] { 1f, 2f, 3f }, seen);
        Assert.Equal(new object[] { 1f, "new" }, All(L)); // the list itself changed
    }

    [Fact]
    public void NestedLoopsOverOneListEachKeepTheirSnapshot()
    {
        DmList L = Dm.list(1f, 2f);
        var pairs = new List<string>();
        foreach (object outer in Dm.each(L))
        {
            foreach (object inner in Dm.each(L))
            {
                pairs.Add(outer + ":" + inner);
                L[(object)("k" + pairs.Count)] = 0f; // appends while both loops run
            }
        }
        Assert.Equal(new[] { "1:1", "1:2", "2:1", "2:2", "2:k1", "2:k2" }, pairs);
    }

    [Fact]
    public void ALoopThatDoesNotMutateCopiesNothing()
    {
        DmList L = Dm.list(1f, 2f);
        object[] before = L._items;
        foreach (object _ in Dm.each(L))
        {
        }
        L[1] = 5f; // the snapshot was released, so this writes in place
        Assert.Same(before, L._items);
    }

    [Fact]
    public void ATypedLoopSkipsNullsAndOtherTypes()
    {
        var a = new TestAtom("a");
        var m = new TestMob("m");
        DmList L = Dm.list(a, null, "text", m, 1f);
        Assert.Equal(new TestAtom[] { a, m }, Dm.each<TestAtom>(L).ToList());
        Assert.Equal(new TestMob[] { m }, Dm.each<TestMob>(L).ToList());
    }

    [Fact]
    public void AnAsAnythingLoopKeepsNullsAndFailsOnAWrongType()
    {
        var a = new TestAtom("a");
        Assert.Equal(new TestAtom[] { a, null }, Dm.each_cast<TestAtom>(Dm.list(a, null)).ToList());
        var seen = new List<TestAtom>();
        Assert.Throws<DmRuntimeException>(() =>
        {
            foreach (TestAtom x in Dm.each_cast<TestAtom>(Dm.list(a, "text")))
                seen.Add(x);
        });
        Assert.Equal(new[] { a }, seen);
        Assert.Equal(new Type[] { typeof(DmList) }, Dm.each_cast<Type>(Dm.list(typeof(DmList))).ToList());
    }

    [Fact]
    public void AnAtomIteratesItsContents()
    {
        var box = new TestAtom("box");
        var a = new TestAtom("a");
        box.contents.AppendItem(a);
        box.contents.AppendItem("not an atom");
        Assert.Equal(new object[] { a, "not an atom" }, All(box));
        Assert.Equal(new[] { a }, Dm.each<TestAtom>(box).ToList());
    }

    [Fact]
    public void TheWorldIsLiveInIdOrderAndUntypedMeansAtoms()
    {
        var w = new World();
        using (w.Enter())
        {
            var datum = new TestDatum().Register<TestDatum>(w);
            var a = new TestAtom("a").Register<TestAtom>(w);
            var thing = new TestThing().Register<TestThing>(w);
            var m = new TestMob("m").Register<TestMob>(w);

            Assert.Equal(new object[] { a, m }, All(Dm.world));                         // world.contents: atoms only
            Assert.Equal(new[] { m }, Dm.each<TestMob>(Dm.world).ToList());
            // for(var/datum/D in world): still world.contents (tg reference_tracking.dm:101, "atoms (don't beleive its
            // lies)"); a non-atom loop type finds nothing there.
            Assert.Equal(new TestDatum[] { a, m }, Dm.each<TestDatum>(Dm.world).ToList());
            Assert.Empty(Dm.each<TestThing>(Dm.world).ToList());

            var seen = new List<string>();
            foreach (TestAtom x in Dm.each<TestAtom>(Dm.world))
            {
                seen.Add(x.name);
                if (x == a) new TestAtom("late").Register<TestAtom>(w);                // not a copy: visited
                if (x == m) w.Objects.Unregister(a.dm_id);
            }
            Assert.Equal(new[] { "a", "m", "late" }, seen);
            GC.KeepAlive(datum);
            GC.KeepAlive(thing);
        }
    }

    [Fact]
    public void ALoopWithoutInWalksTheRegistryAtomsForAnAtomTypeAndDatumsOtherwise()
    {
        // Rules 6.5: for(var/T/x) without 'in'. An atom loop type finds the world's atoms, as in world does; /datum and a
        // non-atom type find the non-atom datums, live and in id order (tg debug.dm:1033 counts atoms with 'in world',
        // :1039 datums without it; SDQL_2.dm:664 uses it "to have it not return atoms").
        var w = new World();
        using (w.Enter())
        {
            var datum = new TestDatum().Register<TestDatum>(w);
            var a = new TestAtom("a").Register<TestAtom>(w);
            var thing = new TestThing().Register<TestThing>(w);
            var m = new TestMob("m").Register<TestMob>(w);

            Assert.Equal(new TestAtom[] { a, m }, Dm.each<TestAtom>().ToList());
            Assert.Equal(new[] { m }, Dm.each<TestMob>().ToList());
            Assert.Equal(new TestDatum[] { datum, thing }, Dm.each<TestDatum>().ToList());
            Assert.Equal(new[] { thing }, Dm.each<TestThing>().ToList());
            Assert.Equal(new object[] { a, m }, Dm.each<object>().ToList());     // an untyped loop var: world.contents

            var seen = new List<TestDatum>();
            foreach (TestDatum d in Dm.each<TestDatum>())
            {
                seen.Add(d);
                if (d == datum) new TestThing().Register<TestThing>(w);              // live, not a copy: visited
                if (d == thing) w.Objects.Unregister(thing.dm_id);                   // qdel'd: no longer iterated
            }
            Assert.Equal(3, seen.Count);
            Assert.Equal(new TestDatum[] { datum, seen[2] }, Dm.each<TestDatum>().ToList());
            GC.KeepAlive(a);
            GC.KeepAlive(m);
        }
    }

    [Fact]
    public void LocateInTheWorldSearchesLikeALoopWithoutIn()
    {
        // Rules 10.4: locate(T) without a container finds the first atom of an atom type and the first non-atom datum
        // of /datum or a non-atom type, in id order, even when an atom was registered first.
        var w = new World();
        using (w.Enter())
        {
            var a = new TestAtom("a").Register<TestAtom>(w);
            var datum = new TestDatum().Register<TestDatum>(w);
            var m = new TestMob("m").Register<TestMob>(w);
            var thing = new TestThing().Register<TestThing>(w);

            Assert.Same(a, Dm.locate<TestAtom>(Dm.world));
            Assert.Same(m, Dm.locate<TestMob>(Dm.world));
            Assert.Same(datum, Dm.locate<TestDatum>(Dm.world));
            Assert.Same(thing, Dm.locate<TestThing>(Dm.world));
            Assert.Same(a, Dm.locate<object>(Dm.world));                           // an untyped search: atoms
            GC.KeepAlive(datum);
            GC.KeepAlive(thing);
        }
    }

    [Fact]
    public void ASnapshotSurvivesAnAwaitWhileAnotherProcChangesTheList()
    {
        var w = new World();
        DmList L = Dm.list(1f, 2f, 3f);
        var seen = new List<object>();

        async DmTask Walker()
        {
            foreach (object x in Dm.each(L))
            {
                seen.Add(x);
                await Dm.sleep(1);
            }
        }

        async DmTask Mutator()
        {
            await Dm.sleep(0);
            L[2] = 20f;
            L.len = 2;
        }

        using (w.Enter())
        {
            Waiting.Detach(Walker());
            Waiting.Detach(Mutator());
        }
        w.Step(20);
        Assert.Equal(new object[] { 1f, 2f, 3f }, seen);
        Assert.Equal(new object[] { 1f, 20f }, All(L));
    }
}

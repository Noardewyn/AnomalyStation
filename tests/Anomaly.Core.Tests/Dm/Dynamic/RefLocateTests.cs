using System.Runtime.CompilerServices;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// <c>ref()</c>, <c>locate()</c> by reference, by tag and by type (BYOND ref <c>/proc/ref</c>, <c>/proc/locate</c>;
/// rules 10.4; object-model.md sections 3 and 6).
/// </summary>
public class RefLocateTests
{
    [Fact]
    public void ARefIsTheWorldIdInLowercaseHexAndLocateFindsItsObject()
    {
        // object-model.md section 3: "[0x" + dm_id in lowercase hex + "]" (BYOND's shape; OpenDream
        // DreamRefManager.GetRefString "[0x{ref:x}]").
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            var made = new List<fx_datum>();
            for (int i = 0; i < 26; i++) made.Add(Dm.@new<fx_datum>());
            Assert.Equal("[0x1]", @ref(made[0]));
            Assert.Equal("[0x1a]", @ref(made[25]));
            Assert.Same(made[25], locate_ref("[0x1a]"));
            Assert.Same(made[25], locate_ref("[0x1A]"));               // hex digits in either case
            foreach (fx_datum datum in made)
                Assert.Same(datum, locate_ref(@ref(datum)));
            Assert.Null(locate_ref("[0x1b]"));                          // never created
            GC.KeepAlive(made);
        }
    }

    [Fact]
    public void ARefOfNullIsZeroAndNothingIsLocatedThere()
    {
        // OpenDream DreamRefManager: null is ref 0 (RefType.Null).
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            Assert.Equal("[0x0]", @ref(null));
            Assert.Null(locate_ref("[0x0]"));
            Assert.Null(locate_ref(null));
            Assert.Null(locate_ref(5f));
            Assert.Null(locate_ref(""));
        }
    }

    [Fact]
    public void ValuesWithoutAReferenceHaveNoRef()
    {
        // object-model.md section 3: prototypes, builtin values without an id, numbers and text have no reference text
        // (an open item: BYOND refs text and numbers too); lists, typepaths and world have one (the tests below).
        Assert.Throws<DmRuntimeException>(() => @ref(new fx_datum()));
        Assert.Throws<DmRuntimeException>(() => @ref(5f));
        Assert.Throws<DmRuntimeException>(() => @ref(5));
        Assert.Throws<DmRuntimeException>(() => @ref("text"));
        Assert.Throws<DmRuntimeException>(() => @ref(matrix()));
        Assert.Throws<DmRuntimeException>(() => @ref(typeof(string)));     // a .NET class is no DM type
    }

    [Fact]
    public void AListGetsAPerWorldRefNumberOnItsFirstRefAndLocateFindsIt()
    {
        // object-model.md section 3: a list's ref is the list kind 0x0F (BYOND's and OpenDream's RefType.DreamObjectList;
        // tg's TYPEID_NORMAL_LIST "f", __DEFINES/typeids.dm) and a number the world allocates on the list's first ref(),
        // in ref() order; an alist has OpenDream's kind 0x55 (RefType.DreamObjectAssocList) and its own numbers.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            DmList first = list(1f);
            DmList second = list();
            DmList assoc = alist(kv("k", 1f));
            Assert.Equal("[0xf000001]", @ref(second));                 // numbered by the first ref(), not by creation
            Assert.Equal("[0xf000002]", @ref(first));
            Assert.Equal("[0xf000001]", @ref(second));                 // stable
            Assert.Equal("[0x55000001]", @ref(assoc));
            Assert.Equal(@ref(first), text("\\ref[]", first));       // the \ref text macro is ref()
            Assert.Same(first, locate_ref("[0xf000002]"));
            Assert.Same(second, locate_ref(@ref(second)));
            Assert.Same(assoc, locate_ref("[0x55000001]"));
            Assert.Null(locate_ref("[0xf000003]"));                     // no list has that number
            Assert.Equal("f", copytext(@ref(first), 4, -7));             // tg's GET_TYPEID reads the kind of a plain list
            Assert.Equal("[0xf000003]", @ref(first.Copy()));             // a copy is another list
        }
    }

    [Fact]
    public void ListRefNumbersAreTheSameInEveryRunAndPerWorld()
    {
        // Allocation order is the order of the ref() calls, so two runs number the same lists alike; worlds are independent.
        static string Run()
        {
            World w = DynamicWorld.New(out _);
            using (w.Enter())
            {
                var lists = new List<DmList> { list(), list("a"), list() };
                return @ref(lists[2]) + @ref(lists[0]) + @ref(lists[2]) + @ref(lists[1]);
            }
        }

        Assert.Equal("[0xf000001][0xf000002][0xf000001][0xf000003]", Run());
        Assert.Equal(Run(), Run());
        DmList shared = list();
        World a = DynamicWorld.New(out _);
        World b = DynamicWorld.New(out _);
        using (a.Enter()) @ref(list());
        string inA, inB;
        using (a.Enter()) inA = @ref(shared);
        using (b.Enter()) inB = @ref(shared);
        Assert.Equal("[0xf000002]", inA);
        Assert.Equal("[0xf000001]", inB);
        using (b.Enter()) Assert.Same(shared, locate_ref("[0xf000001]"));
    }

    [Fact]
    public void ACollectedListIsNoLongerLocatedByItsRef()
    {
        // Like the datum registry (object-model.md section 6), the list refs are weak: a list nothing references any
        // more is gone, as a DM list is at reference count 0, and its ref then locates nothing.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            string reference = RefOfAnUnreferencedList();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.Null(locate_ref(reference));
            Assert.Equal("[0xf000002]", @ref(list()));                   // numbers are never reused
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string RefOfAnUnreferencedList() => @ref(list("gone"));

    [Fact]
    public void ATypepathHasAFixedRefFromTheTypeOrder()
    {
        // object-model.md section 3: OpenDream's typepath kind 0x09 (RefType.DreamType) and the type's ordinal in DM
        // object-tree order (DmTypes.TreeOrdinal, fixed by the generated type order), the same in every world.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            int ordinal = DmTypes.TreeOrdinal(typeof(fx_point));
            Assert.True(ordinal >= 0);
            string expected = "[0x" + (0x09000000 | ordinal).ToString("x") + "]";
            Assert.Equal(expected, @ref(typeof(fx_point)));
            Assert.Same(typeof(fx_point), locate_ref(expected));
            Assert.NotEqual(@ref(typeof(fx_point)), @ref(typeof(fx_datum)));
            Assert.Same(typeof(DmList), locate_ref(@ref(typeof(DmList))));
        }
        using (DynamicWorld.New(out _).Enter())
            Assert.Equal(expected: "[0x" + (0x09000000 | DmTypes.TreeOrdinal(typeof(fx_point))).ToString("x") + "]", @ref(typeof(fx_point)));
    }

    [Fact]
    public void WorldHasAFixedRef()
    {
        // BYOND's world kind 0x0E, number 0; tg's Lua helper compares a ref with "[0xe000001]" for global, the next
        // number of that kind (_lists.dm:1077), which the port does not make.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            Assert.Equal("[0xe000000]", @ref(world));
            Assert.Same(world, locate_ref("[0xe000000]"));
            Assert.Null(locate_ref("[0xe000001]"));
        }
    }

    [Fact]
    public void NoDatumIsGivenAnIdThatReadsAsAListTypeOrWorldRef()
    {
        // A datum's ref is its bare id, so new skips the ids whose top byte is a ref kind of the port.
        Assert.True(DmRefs.IsReservedId(0x09000000));
        Assert.True(DmRefs.IsReservedId(0x09FFFFFF));
        Assert.True(DmRefs.IsReservedId(0x0E000000));
        Assert.True(DmRefs.IsReservedId(0x0F123456));
        Assert.True(DmRefs.IsReservedId(0x55000001));
        Assert.True(DmRefs.IsReservedId(0x27000001)); // resources (OpenDream RefType.DreamResource)
        Assert.False(DmRefs.IsReservedId(0x08FFFFFF));
        Assert.False(DmRefs.IsReservedId(0x0A000000));
        Assert.False(DmRefs.IsReservedId(0x10000000));
        Assert.False(DmRefs.IsReservedId(0x109000000));                  // a wider id whose low bytes look like a kind

        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            while (w.AllocateId() < 0x08FFFFFE) { }
            fx_datum before = Dm.@new<fx_datum>();
            fx_datum after = Dm.@new<fx_datum>();
            Assert.Equal(0x08FFFFFF, before.dm_id);
            Assert.Equal(0x0A000000, after.dm_id);
            Assert.Same(after, locate_ref(@ref(after)));
            Assert.Equal("[0xa000000]", @ref(after));
        }
    }

    [Fact]
    public void TheListRefCounterIsPartOfTheStateHash()
    {
        // It decides the text of every later list ref; a world that never ref()'d a list hashes as before.
        static ulong Hash(bool refList)
        {
            World w = DynamicWorld.New(out _);
            using (w.Enter())
            {
                DmWorldState.Current.Sinks.ToString();
                if (refList) @ref(list());
            }
            return w.ComputeStateHash();
        }

        Assert.Equal(DynamicWorld.New(out _).ComputeStateHash(), Hash(false));
        Assert.NotEqual(Hash(false), Hash(true));
        Assert.Equal(Hash(true), Hash(true));
    }

    [Fact]
    public void AResourceGetsAPerWorldRefNumberByItsPathAndLocateFindsIt()
    {
        // object-model.md section 3: a resource's ref is OpenDream's RefType.DreamResource 0x27 ("Equivalent to file") and a
        // number the world allocates on the first ref() of its path, in ref() order. tg's bespoke elements key on it:
        // SSdcs.GetIdFromArguments takes REF() of an icon argument (the decal element, dcs.dm).
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            DmResource decals = resource("icons/turf/decals.dmi");
            Assert.Equal("[0x27000001]", @ref(decals));
            Assert.Equal("[0x27000002]", @ref(resource("sound/effects/bang.ogg")));
            Assert.Equal("[0x27000001]", @ref(resource("icons/turf/decals.dmi"))); // equal by path: the same ref
            Assert.Equal(decals, locate_ref("[0x27000001]"));
            Assert.Null(locate_ref("[0x27000003]"));
        }

        static ulong Hash(bool refResource)
        {
            World world = DynamicWorld.New(out _);
            using (world.Enter())
            {
                DmWorldState.Current.Sinks.ToString();
                if (refResource) @ref(resource("icons/turf/decals.dmi"));
            }
            return world.ComputeStateHash();
        }

        Assert.Equal(DynamicWorld.New(out _).ComputeStateHash(), Hash(false));
        Assert.NotEqual(Hash(false), Hash(true));
        Assert.Equal(Hash(true), Hash(true));
    }

    [Fact]
    public void AnUnregisteredObjectIsNoLongerLocatedOrIterated()
    {
        // object-model.md section 7: a qdel'd datum is no longer located or iterated (tg's qdel unregisters it).
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_point point = Dm.@new<fx_point>(1);
            string reference = @ref(point);
            Assert.Same(point, locate<fx_point>(world));
            Assert.True(w.Objects.Unregister(point.dm_id));
            Assert.Null(locate_ref(reference));
            Assert.Null(locate<fx_point>(world));
            Assert.Equal(reference, @ref(point));                       // the object keeps its id (object-model.md 7)
            GC.KeepAlive(point);
        }
    }

    [Fact]
    public void ACollectedObjectIsNoLongerLocatedByItsRef()
    {
        // object-model.md sections 3 and 6: registry entries are weak; an object nothing references any more is
        // deleted like a DM datum whose reference count dropped to zero, and its ref text then locates nothing.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_point kept = Dm.@new<fx_point>(1);
            string dropped = CreateAndDrop();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.Null(locate_ref(dropped));
            Assert.Same(kept, locate_ref(@ref(kept)));
            GC.KeepAlive(kept);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string CreateAndDrop() => @ref(Dm.@new<fx_point>(2));

    [Fact]
    public void LocateFindsTheTaggedObject()
    {
        // BYOND ref /datum/var/tag: O.tag = "My Object"; locate("My Object") is O. OpenDream LocateRef: the brackets of
        // a text that is not a reference are stripped, so locate("[abc]") is locate("abc").
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_datum plain = Dm.@new<fx_datum>();
            fx_datum tagged = Dm.@new<fx_datum>();
            tagged.tag = "My Object";
            Assert.Same(tagged, locate_ref("My Object"));
            Assert.Same(tagged, locate_ref("[My Object]"));
            Assert.Null(locate_ref("my object"));
            GC.KeepAlive(plain);
        }
    }

    [Fact]
    public void LocateOfATypeReturnsTheFirstMatchInAContainer()
    {
        // BYOND ref /proc/locate: "Types are matched in the same manner as istype()"; "the first one found";
        // a container's contents; null when there is none or no container.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_atom room = Dm.@new<fx_atom>();
            fx_movable chair = Dm.@new<fx_movable>(room);
            fx_stack stack = Dm.@new<fx_stack>(room);
            Assert.Same(stack, locate<fx_item>(room));
            Assert.Same(chair, locate<fx_movable>(room));
            Assert.Null(locate<fx_bedsheet>(room));
            Assert.Null(locate<fx_item>(null));
            DmList things = list(null, "text", stack);
            Assert.Same(stack, locate<fx_stack>(things));
            Assert.Same(room, locate<fx_atom>(world));                  // the world in id order
        }
    }
}

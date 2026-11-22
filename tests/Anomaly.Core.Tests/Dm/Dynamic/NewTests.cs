using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// <c>Dm.@new</c> and <c>Dm.new_async</c> (rules 10.1; object-model.md sections 3 and 4): construction, identity and
/// registration, the pre-New hook, <c>New</c> through the binder, builtin value types.
/// </summary>
public class NewTests
{
    [Fact]
    public void NewAssignsAnIdRegistersRunsThePreNewHookThenNew()
    {
        // object-model.md section 4: construct, id and registration, pre-New, New; ids increase in creation order.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_datum first = Dm.@new<fx_datum>("a", "b");
            var second = (fx_datum)Dm.@new(typeof(fx_datum));
            Assert.Equal(1, first.dm_id);
            Assert.Equal(2, second.dm_id);
            Assert.Equal(new[] { "pre_new", "datum.New 2" }, first.log);
            Assert.True(w.Objects.TryGet(1, out object registered));
            Assert.Same(first, registered);
        }
    }

    [Fact]
    public void ThePreNewHookAndNewSeeTheObjectsIdAndRegistration()
    {
        // object-model.md section 4: step 3 (id, registration) precedes step 5 (pre-New) and step 6 (New). The engine
        // setters that the pre-New hook's silent placement runs touch the World only for a registered object
        // (object-model.md section 2), so the hook must already see the id.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_id_probe probe = Dm.@new<fx_id_probe>();
            Assert.Equal(new object[] { 1L, true }, probe.seen_at_pre_new);
            Assert.Equal(new object[] { 1L, true }, probe.seen_at_new);
        }
    }

    [Fact]
    public void AnObjectCreatedByAVarInitializerGetsTheNextIdAfterItsCreator()
    {
        // object-model.md sections 3-5: the creator is allocated and registered before its pre-New hook runs the
        // object-creating initializers (rules 10.2), so ids follow creation order: creator 1, the inner point 2, the next
        // creation 3. The inner object's New has run by the time the creator's New runs.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_id_probe probe = Dm.@new<fx_id_probe>();
            Assert.Equal(1, probe.dm_id);
            Assert.Equal(2, probe.inner.dm_id);
            Assert.Equal((1f, 2f, 3f), (probe.inner.x, probe.inner.y, probe.inner.z));
            Assert.Equal(3, Dm.@new<fx_datum>().dm_id);
            Assert.Equal(new[] { "pre_new", "datum.New 0" }, probe.log);
        }
    }

    [Fact]
    public void AHidingNewIsInheritedBySubtypesThatDoNotOverrideIt()
    {
        // Rules 7.1: /client/New is its own slot; a subtype without a definition of its own runs its parent's override.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_client_leaf leaf = Dm.@new<fx_client_leaf>(5);
            Assert.Equal(new[] { "pre_new", "client_sub.New", "client.New 1" }, leaf.log);
        }
    }

    [Fact]
    public void ANamedLocInAnArglistPlacesTheAtom()
    {
        // BYOND ref "New proc (atom)" with arglist: new T(arglist(list("loc" = room))) has no positional argument, so
        // the argument named loc is the initial location.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_atom room = Dm.@new<fx_atom>();
            fx_movable chair = Dm.@new<fx_movable>(arglist(list(kv("loc", room))));
            Assert.Same(room, chair.loc);
            Assert.Same(room, chair.new_loc_param);
            Assert.Same(chair, room.contents[1f]);
        }
    }

    [Fact]
    public void ANewThatSleepsAndThenFailsUnderPlainNewIsReportedOnceForEach()
    {
        // waiting-model.md 9.5 ("errors are never lost and never doubled"): the no-sleep runtime when New sleeps, then
        // New's own late error when it fails, each reported once.
        World w = DynamicWorld.New(out List<string> errors);
        using (w.Enter())
            Assert.NotNull(Dm.@new<fx_failing_new>());
        Assert.Single(errors);
        w.Step(2);
        Assert.Equal(2, errors.Count);
        Assert.Equal("InvalidOperationException: New failed late", errors[1]);
    }

    [Fact]
    public void NamedArgumentsOfNewBindToTheDefinitionsParameters()
    {
        // tg code/datums/position_point_vector.dm:2: new /datum/point(_z = a.z, _pixel_x = ..., _pixel_y = ...) with
        // /datum/point/New(_x, _y, _z, _pixel_x = 0, _pixel_y = 0) (:73); _x and _y stay null, so x and y keep 0.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_point point = Dm.@new<fx_point>(named("_z", 3), named("_pixel_x", 16), named("_pixel_y", -8));
            Assert.Equal((0f, 0f, 3f, 16f, -8f), (point.x, point.y, point.z, point.pixel_x, point.pixel_y));
            fx_point positional = Dm.@new<fx_point>(1, 2, 3);
            Assert.Equal((1f, 2f, 3f, 0f, 0f), (positional.x, positional.y, positional.z, positional.pixel_x, positional.pixel_y));
            Assert.Throws<DmRuntimeException>(() => Dm.@new<fx_point>(named("pixel_x", 1)));
        }
    }

    [Fact]
    public void AnAtomsInitialLocationIsTheFirstPositionalArgumentOrTheOneNamedLoc()
    {
        // BYOND ref "New proc (atom)": "the initial location will be taken from the first positional argument, or from
        // the argument named loc if there are no positional arguments", whatever New calls its first parameter, and the
        // object is already there when New runs.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_atom room = Dm.@new<fx_atom>();
            fx_movable byPosition = Dm.@new<fx_movable>(room);
            Assert.Same(room, byPosition.loc);
            Assert.Same(room, byPosition.new_loc_param);
            fx_movable byName = Dm.@new<fx_movable>(named("loc", room));
            Assert.Same(room, byName.loc);
            fx_renamed_loc renamed = Dm.@new<fx_renamed_loc>(named("loc", room));
            Assert.Same(room, renamed.loc);
            Assert.Same(room, renamed.where);                          // "loc" means New's first parameter here
            Assert.Equal(3, room.contents.Count);
            Assert.Same(byPosition, room.contents[1f]);
        }
    }

    [Fact]
    public void TheArgumentsArePassedThroughArglist()
    {
        // Rules 7.3: new T(arglist(L)) is Dm.@new<T>(Dm.arglist(L)); text keys with values are named arguments.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_point point = Dm.@new<fx_point>(arglist(list(5, kv("_pixel_y", 4))));
            Assert.Equal((5f, 0f, 4f), (point.x, point.y, point.pixel_y));
        }
    }

    [Fact]
    public void AHidingNewIsTheOneNewCalls()
    {
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_client client = Dm.@new<fx_client>(1);
            Assert.Equal(new[] { "pre_new", "client.New 1" }, client.log);
        }
    }

    [Fact]
    public void ARuntimeErrorInNewLeavesTheObjectRegistered()
    {
        // object-model.md section 4 and DV1: the error unwinds through Dm.@new; the object stays allocated and registered.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            var error = Assert.Throws<DmCrashException>(() => Dm.@new<fx_broken>());
            Assert.Equal("bad New", error.Message);
            Assert.True(w.Objects.TryGet(1, out object broken));
            Assert.IsType<fx_broken>(broken);
        }
    }

    [Fact]
    public void NewAsyncWaitsForASleepingNew()
    {
        // BYOND: the creating proc waits while New sleeps (rules 10.1); Dm.new_async completes when New returns.
        World w = DynamicWorld.New(out List<string> errors);
        DmTask<fx_sleeping_new> created;
        using (w.Enter())
            created = Dm.new_async<fx_sleeping_new>(1);
        Assert.False(created.IsCompleted);
        w.Step(2);
        Assert.True(created.TryGetResult(out fx_sleeping_new obj));
        Assert.Equal(new[] { "pre_new", "New start", "New end" }, obj.log);
        Assert.Equal(1, obj.dm_id);
        Assert.Empty(errors);

        DmTask<object> computed;
        using (w.Enter())
            computed = Dm.new_async(typeof(fx_sleeping_new), named("ds", 1));
        w.Step(2);
        Assert.True(computed.TryGetResult(out object made));
        Assert.Equal(new[] { "pre_new", "New start", "New end" }, ((fx_sleeping_new)made).log);
    }

    [Fact]
    public void NewAsyncOfANewThatDoesNotSleepIsCompleted()
    {
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            DmTask<fx_async_datum> quick = Dm.new_async<fx_async_datum>();
            Assert.True(quick.TryGetResult(out fx_async_datum obj));
            Assert.Equal(new[] { "pre_new", "New" }, obj.log);
            DmTask<fx_point> sync = Dm.new_async<fx_point>(1);
            Assert.True(sync.TryGetResult(out fx_point point));
            Assert.Equal(1f, point.x);
        }
    }

    [Fact]
    public void NewAsyncCarriesAnErrorOfNewInItsTask()
    {
        World w = DynamicWorld.New(out List<string> errors);
        DmTask<fx_failing_new> failing;
        DmTask<fx_broken> broken;
        using (w.Enter())
        {
            failing = Dm.new_async<fx_failing_new>();
            broken = Dm.new_async<fx_broken>();
        }
        Assert.IsType<DmCrashException>(broken.Exception);
        DmTask<string> caught;
        using (w.Enter())
            caught = AwaitAndCatch(failing);
        w.Step(2);
        Assert.True(caught.TryGetResult(out string name));
        Assert.Equal(nameof(InvalidOperationException), name);
        Assert.Empty(errors);
    }

    [Fact]
    public void ANewThatSleepsUnderPlainNewFollowsTheNoSleepRule()
    {
        // object-model.md section 4, DV5: Dm.@new returns the object at once, a runtime is reported, New finishes later.
        World w = DynamicWorld.New(out List<string> errors);
        fx_sleeping_new obj;
        using (w.Enter())
            obj = Dm.@new<fx_sleeping_new>(1);
        Assert.Equal(new[] { "pre_new", "New start" }, obj.log);
        Assert.Single(errors);
        Assert.Contains("new /datum/dynfx_async/sleeper", errors[0]);
        w.Step(2);
        Assert.Equal(new[] { "pre_new", "New start", "New end" }, obj.log);
    }

    [Fact]
    public void BuiltinValueTypesAreBuiltByTheirFactoryAndGetNoId()
    {
        // object-model.md sections 3 and 4 step 1. /sound/New(file, repeat = 0, wait, channel, volume) (OpenDream
        // DMStandard Sound.dm; BYOND ref /proc/sound); /exception/New(N, F, L) (OpenDream DMStandard Exception.dm, which
        // EXCEPTION(value) calls with __FILE__ and __LINE__); new /list(Size) (BYOND ref /list).
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            sound s = Dm.@new<sound>("a.ogg", named("volume", 50));
            Assert.Equal("a.ogg", s.file);
            Assert.Equal(50f, s.volume);
            Assert.Equal(100f, Dm.@new<sound>().volume);
            exception e = Dm.@new<exception>("oops", "code/x.dm", 12);
            Assert.Equal(((object)"oops", "code/x.dm", 12f), (e.name, e.file, e.line));
            exception fromNames = Dm.@new<exception>(named("N", "n"), named("L", 3));
            Assert.Equal(((object)"n", (string)null, 3f), (fromNames.name, fromNames.file, fromNames.line));
            DmList sized = Dm.@new<DmList>(3);
            Assert.Equal(3, sized.Count);
            Assert.Null(sized[1f]);
            Assert.Equal(0, Dm.@new<DmList>().Count);
            Assert.Equal(0, w.Objects.Count);                          // none of them was registered
        }
    }

    [Fact]
    public void NewListWithSeveralSizesBuildsAListOfLists()
    {
        // BYOND ref /list: "var/grid = new/list(10,5)" creates "a list of 10 lists each having 5 elements"; OpenDream
        // DreamList.Initialize builds one level per argument, the last one of nulls, and "BYOND coerces negative/non-num
        // values to zero for multidimensional lists". tg: SSmobs keeps clients_by_zlevel = new /list(world.maxz, 0)
        // (controllers/subsystem/mobs.dm:22) and SSai_controllers ai_controllers_by_zlevel (ai_controllers.dm:149): one
        // empty list per z-level.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            DmList byZ = Dm.@new<DmList>(3, 0);
            Assert.Equal(3, byZ.Count);
            var levels = new[] { byZ[1f], byZ[2f], byZ[3f] };
            Assert.All(levels, level => Assert.Equal(0, Assert.IsType<DmList>(level).Count));
            Assert.Equal(3, levels.Distinct().Count());                   // three separate lists

            var grid = (DmList)Dm.@new(typeof(DmList), 2, 3);
            Assert.Equal(2, grid.Count);
            var row = Assert.IsType<DmList>(grid[2f]);
            Assert.Equal((3, (object)null), (row.Count, row[3f]));

            DmList cube = Dm.@new<DmList>(2, 1, 2);
            var inner = (DmList)((DmList)cube[2f])[1f];
            Assert.Equal((2, (object)null), (inner.Count, inner[2f]));

            Assert.All(new[] { Dm.@new<DmList>(2, -1), Dm.@new<DmList>(2, "x"), Dm.@new<DmList>(2, null) },
                coerced => Assert.Equal(0, Assert.IsType<DmList>(coerced[2f]).Count));
            Assert.Equal(0, Dm.@new<DmList>(0, 5).Count);
            Assert.Equal(2, Dm.@new<DmList>(named("Size", 2)).Count);    // /list/New(Size), OpenDream DMStandard List.dm
            Assert.Equal(0, w.Objects.Count);
        }
    }

    [Fact]
    public void ADatumBelowABuiltinValueTypeIsBuiltButGetsNoId()
    {
        // object-model.md section 3: /image, /mutable_appearance and the other builtin value types (and tg's subtypes
        // of them) get no id and are never registered; their var initializers and New still run.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_overlay overlay = Dm.@new<fx_overlay>(1, 2);
            Assert.Equal(0, overlay.dm_id);
            Assert.Equal(new[] { "pre_new", "New 2" }, overlay.log);
            Assert.Equal("emissive", overlay.overlays[1f]);
            Assert.Equal(0, w.Objects.Count);
            Assert.Equal(1, w.AllocateId());                           // no id was used
        }
    }

    [Fact]
    public void NewOfNullOrOfAnAbstractTypeIsARuntimeError()
    {
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            Assert.Throws<DmRuntimeException>(() => Dm.@new(null));
            Assert.Throws<DmRuntimeException>(() => Dm.@new(typeof(DmWorld)));   // no public constructor
            Assert.IsType<DmRuntimeException>(Dm.new_async(null).Exception);
        }
    }

    [Fact]
    public void NewOfADotNetTypeThatIsNoDmTypeIsARuntimeError()
    {
        // BYOND ref /proc/new: Type is "the type of object to create", a DM type; OpenDream DMOpcodeHandlers.CreateObject
        // throws ThrowCannotCreateObjectFromInvalid for any other value. A C# class without [DmPath] that derives from no
        // DM class (a host or BCL type) is no DM type: new must not hand back a StringBuilder or a bare object.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            Assert.Throws<DmRuntimeException>(() => Dm.@new(typeof(System.Text.StringBuilder)));
            Assert.Throws<DmRuntimeException>(() => Dm.@new(typeof(object)));
            Assert.Throws<DmRuntimeException>(() => Dm.@new(typeof(List<object>), 4));
            Assert.IsType<DmRuntimeException>(Dm.new_async(typeof(System.Text.StringBuilder)).Exception);
            Assert.Equal(1, w.AllocateId());                               // nothing was created or registered
        }
    }

    private static async DmTask<string> AwaitAndCatch<T>(DmTask<T> task)
    {
        try
        {
            await task;
            return "no error";
        }
        catch (Exception e)
        {
            return e.GetType().Name;
        }
    }
}

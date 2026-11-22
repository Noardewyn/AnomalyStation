using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using Anomaly.Core.Tests.Tg.Wave4.Movable;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>The procs of code/game/objects/objs.dm that M0 uses, from the DM: Initialize, Destroy, the air passthroughs, check_on_table and the small defaults.</summary>
public class ObjTests
{
    private static T NewAt<T>(int x = 3, int y = 3) where T : atom => Dm.@new<T>(MobRoom.T(x, y));

    // ---- Initialize ----

    [Fact]
    public void InitializeRunsTheParentChainAndRegistersTheIdTag()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj_test_mob_tagged>();
            Assert.True(Dm.truthy(Dm.bitand(o.flags_1, INITIALIZED_1)));
            Assert.Same(o, GLOB.objects_by_id_tag["test_mob_tag"]);
            Assert.Equal(1f, GLOB.objects_by_id_tag.len);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AnObjWithoutAnIdTagRegistersNothing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            NewAt<obj>();
            var empty = NewAt<obj>();
            empty.id_tag = "";
            Assert.Equal(0f, GLOB.objects_by_id_tag.len);
        });
    }

    [Fact]
    public void AnIdTagThatIsZeroIsFalsyAndNotRegistered()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = Dm.@new<obj_test_mob_numeric_tag>(MobRoom.T(3, 3));
            Assert.Equal(0f, GLOB.objects_by_id_tag.len); // `if (id_tag)`: 0 is false
            Assert.True(Dm.eq(o.id_tag, 0));
        });
    }

    [Fact]
    public void TgQuirk_TwoObjsWithOneIdTagLeaveTheLastAndTheFirstOnesDestroyRemovesTheKey()
    {
        // GLOB.objects_by_id_tag[id_tag] = src (objs.dm:67) overwrites; `GLOB.objects_by_id_tag -= id_tag` (:79) removes the key
        // whichever obj it belongs to.
        var room = new MobRoom();
        room.In(() =>
        {
            var first = NewAt<obj_test_mob_tagged>();
            var second = NewAt<obj_test_mob_tagged_twin>();
            Assert.Same(second, GLOB.objects_by_id_tag["test_mob_tag"]);
            qdel(first);
            Assert.Equal(0f, GLOB.objects_by_id_tag.len); // the twin lost its registration too
            Assert.False(QDELETED(second));
        });
    }

    // ---- Destroy ----

    [Fact]
    public void DestroyStopsProcessingOnSSobjForAPlainObj()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj_test_mob_processing>();
            START_PROCESSING(SSobj, o);
            Assert.True(Dm.@in(o, SSobj.processing));
            qdel(o);
            Assert.False(Dm.@in(o, SSobj.processing));
            Assert.False(Dm.truthy(Dm.bitand(o.datum_flags, DF_ISPROCESSING)));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TgQuirk_DestroyLeavesAMachineOnSSobjBecauseMachineryStopsItselfElsewhere()
    {
        // `if(!ismachinery(src)) STOP_PROCESSING(SSobj, src)` (objs.dm:73): /obj/machinery's own Destroy ends its processing
        // (SSmachines); the base proc must not touch it.
        var room = new MobRoom();
        room.In(() =>
        {
            var machine = NewAt<obj_machinery>();
            START_PROCESSING(SSobj, machine);
            qdel(machine);
            Assert.True(QDELETED(machine));
            Assert.True(Dm.@in(machine, SSobj.processing));
        });
    }

    [Fact]
    public void DestroyDefersTheTguiAndCameraLeavesAndReportsNoRuntime()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            var opaque = NewAt<obj_test_mob_opaque>();
            qdel(o);
            qdel(opaque);
            var sites = DmWorldState.Of(room.World).DeferredSites;
            Assert.DoesNotContain("code/game/objects/objs.dm:77", sites); // SStgui.close_uis: translated since M5 slice 7
            Assert.Contains("code/game/objects/objs.dm:70", sites); // SScameras.update_visibility in Initialize (opacity)
            Assert.Contains("code/game/objects/objs.dm:76", sites); // and in Destroy
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AnObjThatLeavesTheMapIsRemovedFromItsTurfContents()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            Assert.Equal(1f, MobRoom.T(3, 3).contents.len);
            qdel(o);
            Assert.Equal(0f, MobRoom.T(3, 3).contents.len);
            Assert.Null(o.loc);
        });
    }

    // ---- air ----

    [Fact]
    public void AnObjInNullspaceHasNoAir()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = Dm.@new<obj>();
            Assert.Null(Waiting.NoSleep(o.return_air()));
            Assert.Null(o.remove_air(10));
            Assert.Null(Waiting.NoSleep(o.assume_air(null)));
        });
    }

    [Fact]
    public void AnObjPassesTheAirProcsToItsLoc()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var tank = Dm.@new<obj_test_mob_air_tank>();
            tank.mixture = Dm.@new<datum_gas_mixture>();
            var inside = Dm.@new<obj>(tank);
            Assert.Same(tank.mixture, Waiting.NoSleep(inside.return_air()));
            Assert.Equal("removed 7", Dm.str(inside.remove_air(7)));
            var giver = Dm.@new<datum_gas_mixture>();
            Assert.Equal("assumed", Dm.str(Waiting.NoSleep(inside.assume_air(giver))));
            Assert.Same(giver, tank.given);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AnObjInNestedContainersReachesTheOutermostAirOneLocAtATime()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var tank = Dm.@new<obj_test_mob_air_tank>();
            tank.mixture = Dm.@new<datum_gas_mixture>();
            var box = Dm.@new<obj>(tank);
            var inside = Dm.@new<obj>(box);
            Assert.Same(tank.mixture, Waiting.NoSleep(inside.return_air()));
        });
    }

    // ---- get_dumping_location, dump_contents, unfreeze ----

    [Fact]
    public void GetDumpingLocationIsTheTurfOfTheObjOrNull()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>(2, 5);
            Assert.Same(MobRoom.T(2, 5), o.get_dumping_location());
            var inside = Dm.@new<obj>(o);
            Assert.Same(MobRoom.T(2, 5), inside.get_dumping_location()); // get_turf of a nested obj
            Assert.Null(Dm.@new<obj>().get_dumping_location());
        });
    }

    [Fact]
    public void DumpContentsIsUnimplementedOnTheBaseObj()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            var task = o.dump_contents();
            Assert.True(task.IsFaulted);
            Assert.IsType<DmCrashException>(task.Exception);
            Assert.Contains("Unimplemented.", task.Exception.Message);
        });
    }

    [Fact]
    public void UnfreezeSendsTheUnfreezeSignal()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            Chain.On(o, COMSIG_OBJ_UNFREEZE, "unfreeze");
            o.unfreeze();
            Assert.Equal(new[] { "unfreeze " + EngineTrace.D(o) + " null null null" }, EngineTrace.Of(room.World).Lines);
        });
    }

    // ---- unfasten_wrench_check ----

    [Fact]
    public void UnfastenWrenchCheckFailsAtOnceWhenTheAnchoringChangedWithoutAskingTheFloor()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj_test_mob_wrenchable>();
            // a strict world: can_be_unfasten_wrench of a plain obj is NotTranslated (M5), so a call to it would throw
            Assert.True(Dm.eq(o.unfasten_wrench_check(TRUE, null), FALSE)); // anchored (0) != prev_anchored (1)
            Assert.True(Dm.eq(NewAt<obj>().unfasten_wrench_check(TRUE, null), FALSE));
        });
    }

    [Theory]
    [InlineData(SUCCESSFUL_UNFASTEN, true)]
    [InlineData(FAILED_UNFASTEN, false)] // both are TRUE as values, but only an explicit success passes (:315-320)
    [InlineData(CANT_UNFASTEN, false)]
    public void UnfastenWrenchCheckIsTrueOnlyForAnExplicitSuccess(float result, bool expected)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj_test_mob_wrenchable_choosy>();
            o.unfasten = result;
            Assert.True(Dm.eq(o.unfasten_wrench_check(o.anchored, null), expected ? TRUE : FALSE));
            Assert.Equal(new[] { "silent" }, o.asked); // can_be_unfasten_wrench(user, TRUE): the check is silent
        });
    }

    // ---- check_on_table ----

    [Fact]
    public void CheckOnTableDoesNothingWithoutAnOffsetEvenOnATable()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            NewAt<obj_structure_table>();
            var o = NewAt<obj_test_mob_wall_mounted>();
            o.pixel_z = 9;
            o.check_on_table();
            Assert.Equal(9f, o.pixel_z); // anchored_tabletop_offset == 0: returns before touching pixel_z (:324-325)
        });
    }

    [Fact]
    public void ATableItselfNeverShifts()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var table = NewAt<obj_structure_table_test_mob_shifting>();
            table.check_on_table();
            Assert.Equal(0f, table.pixel_z);
        });
    }

    [Fact]
    public void AnAnchoredObjRisesOverATableAndFallsBackToItsInitialPixelZ()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var table = NewAt<obj_structure_table>();
            var lamp = NewAt<obj_test_mob_tabletop_shifted>(); // initial pixel_z is 3
            Assert.Equal(5f, lamp.pixel_z); // created on the table: Initialize called check_on_table
            qdel(table);
            lamp.check_on_table();
            Assert.Equal(3f, lamp.pixel_z); // initial(pixel_z), not 0
            lamp.anchored = FALSE;
            NewAt<obj_structure_table>();
            lamp.check_on_table();
            Assert.Equal(3f, lamp.pixel_z); // an unanchored obj does not rise
        });
        // GAP(M3 P1 -> lead): the table's tg var block reaches its listed leaf (Tools/Types/VarBlockLeaves.cs).
        Assert.Empty(Anomaly.Core.Tests.Tools.Types.VarBlockLeaves.Unexpected(typeof(obj_structure_table), room.RuntimeText.ToList()));
    }

    [Fact]
    public void AnObjWithNoLocAtAllIsNotOnATable()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var lamp = Dm.@new<obj_test_mob_tabletop_shifted>();
            lamp.check_on_table();
            Assert.Equal(3f, lamp.pixel_z);
        });
        Assert.Empty(room.RuntimeText);
    }

    // ---- get_demolition_modifier and the rename procs ----

    [Fact]
    public void TheDemolitionModifierIsTheObjsUnlessTheTargetIgnoresOrInvertsIt()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var axe = NewAt<obj>();
            axe.demolition_mod = 4;
            var target = NewAt<obj>();
            Assert.True(Dm.eq(axe.get_demolition_modifier(target), 4));
            ADD_TRAIT(target, TRAIT_INVERTED_DEMOLITION, "test");
            Assert.True(Dm.eq(axe.get_demolition_modifier(target), 0.25f)); // 1 / demolition_mod
            ADD_TRAIT(target, TRAIT_IGNORE_DEMOLITION, "test");
            Assert.True(Dm.eq(axe.get_demolition_modifier(target), 1)); // ignoring wins over inverting
        });
    }

    [Fact]
    public void TheRenameProcsPassTheirInputThroughAndAgreeByDefault()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            Assert.True(Dm.eq(o.rename_checks(null), TRUE));
            Assert.Equal("a name", Waiting.NoSleep(o.nameformat("a name", null)));
            Assert.Equal("a desc", Waiting.NoSleep(o.descformat("a desc", null)));
            Waiting.NoSleep(o.rename_reset());
        });
    }

    // ---- types ----

    [Fact]
    public void AnObjIsItsOwnAbstractTypeSoItIsNeverCreatedByTheCreateAndDestroyTest()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            Assert.True(Dm.eq(Dm.initial(typeof(obj), (atom p) => p.abstract_type), typeof(obj)));
            // /obj/item is its own abstract_type too (items.dm:5, its var block translated by the M2 contracts step)
            Assert.True(Dm.eq(Dm.initial(typeof(obj_item), (atom p) => p.abstract_type), typeof(obj_item)));
        });
    }
}

/// <summary>An obj with an id_tag of 0 (falsy in DM).</summary>
[DmPath("/obj/test_mob_numeric_tag")]
public class obj_test_mob_numeric_tag : obj
{
    public obj_test_mob_numeric_tag()
    {
        id_tag = 0;
    }
}

/// <summary>An opaque obj: its Initialize and Destroy reach SScameras.update_visibility (M9, deferred).</summary>
[DmPath("/obj/test_mob_opaque")]
public class obj_test_mob_opaque : obj
{
    public obj_test_mob_opaque()
    {
        opacity = TRUE;
    }
}

/// <summary>A container that answers the air procs with markers (the passthrough of objs.dm:123-139 ends here).</summary>
[DmPath("/obj/test_mob_air_tank")]
public class obj_test_mob_air_tank : obj
{
    [NotHashed] public datum_gas_mixture mixture = null;
    [NotHashed] public datum_gas_mixture given;

    public override async DmTask<object> return_air()
    {
        return mixture;
    }

    public override object remove_air(object amount = null) => "removed " + Dm.str(amount);

    public override async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        given = giver;
        return "assumed";
    }
}

/// <summary>A wrenchable obj that answers can_be_unfasten_wrench with a value of the test's choosing and records its silent flag.</summary>
[DmPath("/obj/test_mob_wrenchable/choosy")]
public class obj_test_mob_wrenchable_choosy : obj_test_mob_wrenchable
{
    [NotHashed] public object unfasten = SUCCESSFUL_UNFASTEN;
    [NotHashed] public readonly List<string> asked = new();

    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        asked.Add(Dm.truthy(silent) ? "silent" : "loud");
        return unfasten;
    }
}

/// <summary>An obj that raises over tables when anchored, and whose initial pixel_z is not 0.</summary>
[DmPath("/obj/test_mob_tabletop_shifted")]
public class obj_test_mob_tabletop_shifted : obj
{
    public obj_test_mob_tabletop_shifted()
    {
        anchored_tabletop_offset = 5;
        anchored = TRUE;
        pixel_z = 3;
    }
}

/// <summary>An anchored obj with no tabletop offset (a wall-mounted one, objs.dm:16-17).</summary>
[DmPath("/obj/test_mob_wall_mounted")]
public class obj_test_mob_wall_mounted : obj
{
    public obj_test_mob_wall_mounted()
    {
        anchored = TRUE;
    }
}

/// <summary>A table with a tabletop offset of its own: check_on_table returns for tables before anything else (:327-328).</summary>
[DmPath("/obj/structure/table/test_mob_shifting")]
public class obj_structure_table_test_mob_shifting : obj_structure_table
{
    public obj_structure_table_test_mob_shifting()
    {
        anchored_tabletop_offset = 5;
        anchored = TRUE;
    }
}

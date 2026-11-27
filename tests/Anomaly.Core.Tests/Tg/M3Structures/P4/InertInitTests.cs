using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.P4;

// M3 P4's smoke tests of the inert init (D112, docs/ai/specs/types-converter.md section 7): dm_inert_initialize of atom, turf
// and area, and InertSeam switched to [DmInert]. T2 tests P4 independently later.

/// <summary>
/// A probe that stands for a generated inert type (rules 15: a fixture path no tg path starts with): its own
/// <c>Initialize</c> throws, so it shows that an inert atom runs none of its type's code.
/// </summary>
[DmPath("/obj/structure/test_p4_inert_probe"), DmInert]
public class test_p4_inert_probe : obj_structure
{
    public test_p4_inert_probe()
    {
        max_integrity = 123;
        post_init_icon_state = "probe_done";
    }

    public override object Initialize(object mapload = null, params object[] __extra)
        => throw new InvalidOperationException("the inert probe's own Initialize ran");
}

/// <summary>The live sibling of <see cref="test_p4_inert_probe"/>: its own <c>Initialize</c> runs.</summary>
[DmPath("/obj/structure/test_p4_live_probe")]
public class test_p4_live_probe : obj_structure
{
    [NotHashed] public int initialized;

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        initialized++;
        return base.Initialize(mapload, __extra);
    }
}

public class InertInitTests
{
    /// <summary>The first class under <paramref name="root"/> in DM order that the generator marked inert and no part was written for.</summary>
    private static Type FirstInertGenerated(Type root)
    {
        foreach (object t in Dm.each(typesof(root)))
            if (t is Type type && InertSeam.IsInertType(type) && type.IsDefined(typeof(DmGeneratedOnlyAttribute), false))
                return type;
        throw new InvalidOperationException("no inert generated-only subtype of " + DmTypes.PathOf(root) + ": pick another root");
    }

    [Fact]
    public void TheSeamFollowsTheGeneratorsMark()
    {
        Assert.True(InertSeam.IsInertType(typeof(test_p4_inert_probe)));
        Assert.False(InertSeam.IsInertType(typeof(test_p4_live_probe)));
        Assert.False(InertSeam.IsInertType(typeof(obj_structure)));
        // the mark is not inherited: each inert class carries its own (types-converter.md 4.3)
        Assert.True(InertSeam.IsInertType(FirstInertGenerated(typeof(obj_machinery_door_airlock))));
    }

    [Fact]
    public void AnInertObjRunsOnlyThePresentationLinesOfAtomInitialize()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var probe = Dm.@new<test_p4_inert_probe>(ToyRoom.FloorBottomLeft());

            Assert.Empty(room.RuntimeText); // its own Initialize, which throws, never ran
            Assert.False(QDELETED(probe));
            Assert.True(Dm.truthy(Dm.bitand(probe.flags_1, INITIALIZED_1)));
            Assert.True(Dm.eq(probe.atom_integrity, 123)); // atoms_initializing_EXPENSIVE.dm:143-144
            Assert.Equal("probe_done", probe.icon_state); // :138-139
            Assert.False(Dm.@in(typeof(test_p4_inert_probe), SSatoms.BadInitializeCalls)); // a hint, and INITIALIZED_1
        }
    }

    [Fact]
    public void TheInertInitReturnsTheNormalHintAndWarnsOnASecondInit()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var probe = Dm.@new<test_p4_inert_probe>(ToyRoom.FloorBottomLeft());
            Dm.mask(ref probe.flags_1, Dm.bitnot(INITIALIZED_1));
            Assert.True(Dm.eq(probe.dm_inert_initialize(Dm.list(FALSE)), INITIALIZE_HINT_NORMAL));
            Assert.Empty(room.RuntimeText);

            Assert.True(Dm.eq(probe.dm_inert_initialize(Dm.list(FALSE)), INITIALIZE_HINT_NORMAL));
            Assert.Contains(room.RuntimeText, r => r.Contains("initialized multiple times", StringComparison.Ordinal)); // :119-120
        }
    }

    [Fact]
    public void ALiveSubtypeOfTheSameBaseRunsItsOwnInitialize()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var probe = Dm.@new<test_p4_live_probe>(ToyRoom.FloorBottomLeft());
            Assert.Equal(1, probe.initialized);
            Assert.True(Dm.truthy(Dm.bitand(probe.flags_1, INITIALIZED_1)));
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void AGeneratedInertAirlockSubtypeInitializesWithoutRunningAnyDoorCode()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            Type airlock = FirstInertGenerated(typeof(obj_machinery_door_airlock));
            var door = Dm.@new(airlock, ToyRoom.FloorBottomLeft()) as obj_machinery_door;

            Assert.Empty(room.RuntimeText); // no door, machinery or materials code ran (M3 contracts: they reach leaves)
            Assert.False(QDELETED(door));
            Assert.True(Dm.truthy(Dm.bitand(door.flags_1, INITIALIZED_1)));
            Assert.True(Dm.eq(door.atom_integrity, door.max_integrity));
            Assert.True(Dm.truthy(door.density)); // tg's data: a closed door blocks by density
            qdel(door);
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void AnInertOpenTurfGetsItsBaseturfsAndAir()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            Type floorType = FirstInertGenerated(typeof(turf_open_floor));
            turf at = locate(3, 3, 1);
            var floor = Dm.@new(floorType, at) as turf_open;

            Assert.Empty(room.RuntimeText);
            Assert.Same(floor, locate(3, 3, 1));
            Assert.True(Dm.truthy(Dm.bitand(floor.flags_1, INITIALIZED_1)));
            Assert.NotNull(floor.baseturfs); // assemble_baseturfs (turf.dm:164)
            Assert.NotNull(floor.air); // /turf/open/Initialize (LINDA_turf_tile.dm:61)
        }
    }

    [Fact]
    public void AnInertClosedTurfAndAnInertSpaceTurfInitialize()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var wall = Dm.@new(FirstInertGenerated(typeof(turf_closed)), locate(3, 3, 1)) as turf;
            Assert.True(Dm.truthy(Dm.bitand(wall.flags_1, INITIALIZED_1)));
            Assert.NotNull(wall.baseturfs);

            var space = Dm.@new(FirstInertGenerated(typeof(turf_open_space)), locate(4, 4, 1)) as turf_open_space;
            Assert.True(Dm.truthy(Dm.bitand(space.flags_1, INITIALIZED_1)));
            Assert.Same(turf_open_space.space_gas, space.air); // space_EXPENSIVE.dm:14
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void AnInertAreaRegistersAndRunsAreaInitialize()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            Type areaType = FirstInertGenerated(typeof(area_centcom_central_command_areas_supplypod_loading)); // the maintenance (D296), bar and AI areas are live
            var inert = Dm.@new(areaType) as area;

            Assert.Empty(room.RuntimeText);
            Assert.True(Dm.truthy(Dm.bitand(inert.flags_1, INITIALIZED_1)));
            Assert.True(Dm.@in(inert, GLOB.areas)); // /area/New (areas.dm:181)
            Assert.Equal("", inert.icon_state); // /area/Initialize (areas.dm:196)
            Assert.True(Dm.eq(inert.luminosity, 0)); // requires_power (areas.dm:200-201)
        }
    }
}

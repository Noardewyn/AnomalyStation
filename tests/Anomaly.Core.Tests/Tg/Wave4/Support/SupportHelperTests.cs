using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Support;

/// <summary>
/// The helpers the Move chain reads: <c>get_nested_locs</c> (__HELPERS/atoms.dm:49), <c>dir2angle</c> and
/// <c>angle2dir</c> (__HELPERS/type2type.dm:65-99) and the multiz helpers
/// (modules/mapping/space_management/multiz_helpers.dm).
/// </summary>
public class SupportHelperTests
{
    private static object[] Items(object list) => Dm.each(list).Cast<object>().ToArray();

    [Fact]
    public void NestedLocsListTheContainersInsideOutAndTheTurfOnlyWhenAsked()
    {
        using var w = new SupportWorld();
        turf floor = locate(2, 2, 1);
        var outer = Dm.@new<obj_test_support_box>(floor);
        var inner = Dm.@new<obj_test_support_box>(outer);
        var item = Dm.@new<obj_test_support_box>(inner);

        Assert.Equal(new object[] { inner, outer }, Items(get_nested_locs(item)));
        Assert.Equal(new object[] { inner, outer, floor }, Items(get_nested_locs(item, TRUE)));
        Assert.Empty(Items(get_nested_locs(outer)));
        Assert.Equal(new object[] { floor }, Items(get_nested_locs(outer, TRUE)));

        var adrift = Dm.@new<obj_test_support_box>();
        var held = Dm.@new<obj_test_support_box>(adrift);
        Assert.Empty(Items(get_nested_locs(adrift, TRUE)));
        Assert.Equal(new object[] { adrift }, Items(get_nested_locs(held, TRUE))); // no turf in nullspace
    }

    [Fact]
    public void Dir2AngleIsTgsTableAndNullForAnythingElse()
    {
        object[][] table =
        {
            new object[] { NORTH, 0 }, new object[] { SOUTH, 180 }, new object[] { EAST, 90 }, new object[] { WEST, 270 },
            new object[] { NORTHEAST, 45 }, new object[] { SOUTHEAST, 135 }, new object[] { NORTHWEST, 315 }, new object[] { SOUTHWEST, 225 },
        };
        foreach (object[] row in table)
            Assert.True(Dm.eq(dir2angle(row[0]), row[1]), "dir " + row[0]);
        Assert.Null(dir2angle(0));
        Assert.Null(dir2angle(UP));
        Assert.Null(dir2angle(null));
        Assert.Null(dir2angle("1"));
    }

    [Fact]
    public void Angle2DirRoundsToTheNearestOfEightWithDmsIntegerModulo()
    {
        using var w = new SupportWorld();
        foreach (float dir in new float[] { NORTH, NORTHEAST, EAST, SOUTHEAST, SOUTH, SOUTHWEST, WEST, NORTHWEST })
            Assert.True(Dm.eq(angle2dir(Dm.num(dir2angle(dir))), dir), "round trip " + dir);
        // (((X % 360) + 382.5) % 360) / 45: % truncates both operands, so the boundaries sit after .5 degrees.
        Assert.True(Dm.eq(angle2dir(22), NORTH));
        Assert.True(Dm.eq(angle2dir(22.9f), NORTH));
        Assert.True(Dm.eq(angle2dir(23), NORTHEAST));
        Assert.True(Dm.eq(angle2dir(337), NORTHWEST));
        Assert.True(Dm.eq(angle2dir(338), NORTH));
        Assert.True(Dm.eq(angle2dir(360), NORTH));
        Assert.True(Dm.eq(angle2dir(-90), WEST));
        Assert.True(Dm.eq(angle2dir(-45), NORTHWEST));
        Assert.True(Dm.eq(angle2dir(450), EAST));
    }

    [Fact]
    public void WithNoMultizLevelsTheMultizHelpersStayOnTheirLevel()
    {
        using var w = new SupportWorld(3, 3, 2);
        turf here = locate(2, 2, 1);
        var box = Dm.@new<obj_test_support_box>(here);
        // the levels are not linked: one entry per level, neither up nor down (ToyRoom.LinkLevels, mapping.dm:226)
        Assert.Equal(2, SSmapping.multiz_levels.Count);
        Assert.False(Dm.truthy(((DmList)SSmapping.multiz_levels[1f])[(float)Z_LEVEL_UP]));

        Assert.Null(get_step_multiz(box, UP));
        Assert.Null(get_step_multiz(box, DOWN));
        Assert.Null(get_step_multiz(box, Dm.bitor(UP, NORTH)));
        Assert.Same(locate(2, 3, 1), get_step_multiz(box, NORTH));
        Assert.Same(here, get_step_multiz(box, 0));
        Assert.Same(here, get_lowest_turf(box));
        Assert.Same(here, get_highest_turf(box));

        Assert.True(Dm.eq(get_dir_multiz(here, locate(3, 3, 1)), NORTHEAST));
        Assert.True(Dm.eq(get_dir_multiz(here, locate(2, 3, 2)), get_dir(here, locate(2, 3, 2)))); // another level, not linked: plain get_dir
        Assert.True(Dm.eq(get_dir_multiz(here, null), NONE));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void WithLinkedLevelsTheMultizHelpersGoUpAndDown()
    {
        using var w = new SupportWorld(3, 3, 3);
        // tg's manage_z_level (controllers/subsystem/mapping.dm) links the levels: 1 up to 2, 2 up to 3.
        SSmapping.multiz_levels.len = 3;
        for (float z = 1; z <= 3; z++)
            SSmapping.multiz_levels[z] = Dm.list_n(2);
        ((DmList)SSmapping.multiz_levels[1f])[(float)Z_LEVEL_UP] = TRUE;
        ((DmList)SSmapping.multiz_levels[2f])[(float)Z_LEVEL_UP] = TRUE;
        ((DmList)SSmapping.multiz_levels[2f])[(float)Z_LEVEL_DOWN] = TRUE;
        ((DmList)SSmapping.multiz_levels[3f])[(float)Z_LEVEL_DOWN] = TRUE;
        var box = Dm.@new<obj_test_support_box>(locate(2, 2, 2));

        Assert.Same(locate(2, 2, 3), get_step_multiz(box, UP));
        Assert.Same(locate(2, 2, 1), get_step_multiz(box, DOWN));
        Assert.Same(locate(2, 3, 3), get_step_multiz(box, Dm.bitor(UP, NORTH)));
        Assert.Same(locate(1, 2, 1), get_step_multiz(box, Dm.bitor(DOWN, WEST)));
        Assert.Same(locate(2, 2, 1), get_lowest_turf(box));
        Assert.Same(locate(2, 2, 3), get_highest_turf(box));

        Assert.True(Dm.eq(get_dir_multiz(locate(2, 2, 2), locate(2, 3, 3)), Dm.bitor(UP, NORTH)));
        Assert.True(Dm.eq(get_dir_multiz(locate(2, 2, 2), locate(2, 2, 1)), DOWN));
        Assert.True(Dm.eq(get_dir_multiz(locate(2, 2, 1), locate(2, 2, 3)), get_dir(locate(2, 2, 1), locate(2, 2, 3)))); // two levels apart
        Assert.Empty(w.Runtimes);
    }
}

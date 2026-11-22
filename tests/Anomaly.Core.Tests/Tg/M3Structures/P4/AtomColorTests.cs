using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.P4;

/// <summary>An inert probe with a mapped colour (rules 15 fixture path): the inert init's add_atom_colour line.</summary>
[DmPath("/obj/structure/test_p4_coloured_probe"), DmInert]
public class test_p4_coloured_probe : obj_structure
{
    public test_p4_coloured_probe() { color = "#FF0000"; }
}

// M3 P4's smoke tests of game/atom/atom_color.dm (the lead's assignment): the colour priority system.
public class AtomColorTests
{
    [Fact]
    public void AnInertAtomKeepsItsMappedColourAsTheFixedPriority()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var probe = Dm.@new<test_p4_coloured_probe>(ToyRoom.FloorBottomLeft());
            Assert.Empty(room.RuntimeText);
            Assert.Equal("#FF0000", probe.color);
            Assert.Equal(COLOUR_PRIORITY_AMOUNT, probe.atom_colours.len);
            var fixedColour = (DmList)probe.atom_colours[FIXED_COLOUR_PRIORITY];
            Assert.Equal("#FF0000", fixedColour[ATOM_COLOR_VALUE_INDEX]);
            Assert.Equal(ATOM_COLOR_TYPE_NORMAL, fixedColour[ATOM_COLOR_TYPE_INDEX]);
            Assert.True(HAS_TRAIT(probe, TRAIT_KEEP_TOGETHER)); // ADD_KEEP_TOGETHER(src, ATOM_COLOR_TRAIT), atom_color.dm:117
            Assert.True(Dm.truthy(Dm.bitand(probe.appearance_flags, KEEP_TOGETHER)));
            Assert.True(Dm.truthy(probe.is_atom_colour("#ff0000")));
        }
    }

    [Fact]
    public void TheHighestPriorityColourWinsAndRemovingItFallsBack()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var probe = Dm.@new<test_p4_coloured_probe>(ToyRoom.FloorBottomLeft());
            // update_atom_colour walks atom_colours from index 1: ADMIN_COLOUR_PRIORITY (1) beats FIXED_COLOUR_PRIORITY (4)
            Waiting.NoSleep(probe.add_atom_colour("#00FF00", ADMIN_COLOUR_PRIORITY));
            Assert.Equal("#00FF00", probe.color);
            Waiting.NoSleep(probe.remove_atom_colour(ADMIN_COLOUR_PRIORITY, "#0000FF")); // not the colour at that priority: kept
            Assert.Equal("#00FF00", probe.color);
            Waiting.NoSleep(probe.remove_atom_colour(ADMIN_COLOUR_PRIORITY, "#00FF00"));
            Assert.Equal("#FF0000", probe.color);
            Assert.False(Dm.truthy(probe.is_atom_colour("#00ff00")));
            Waiting.NoSleep(probe.add_atom_colour("#0000FF", COLOUR_PRIORITY_AMOUNT + 1)); // past the last priority: ignored
            Assert.Equal("#FF0000", probe.color);
            Assert.Empty(room.RuntimeText);
        }
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Recycling;

/// <summary>
/// tg's conveyors (recycling/conveyor.dm, the owner 2026-11-04) in a booted toy room: a switch runs every belt of its id forward
/// (a hand), backward (the right button) or off, with tg's lever sounds and icon states; what lies on a running belt rides it a tile
/// per 0.2 s and stops where the belts end; a one-way switch only runs forward; a human rides as an item does; a corner belt turns
/// what it carries; a screwdriver inverts or flips a belt (its icon state with it); a crowbar lifts a belt into a stack that keeps its
/// id, which lays belts facing the layer; a switch assembly links stacks and builds a switch where its belts are in view.
/// </summary>
public class ConveyorTests
{
    private const int Second = 20;
    private const int Y = 5;
    private const string Lever = "sound/machines/lever/lever_start.ogg", LeverStop = "sound/machines/lever/lever_stop.ogg";

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed = 31701)
    {
        var r = new AudioRoom(14, 10, seed: seed);
        mob_living_carbon_human bob = r.Join(2, Y + 3, "Bob");
        r.Step(2 * Second);
        return (r, bob);
    }

    // belts on (x0..x1, Y) facing dir, with the id
    private static List<obj_machinery_conveyor> Line(AudioRoom r, int x0, int x1, float dir, string id = "line") => r.In(() =>
    {
        var belts = new List<obj_machinery_conveyor>();
        for (int x = x0; x <= x1; x++)
            belts.Add(Dm.@new<obj_machinery_conveyor>(locate(x, Y, 1), dir, id));
        return belts;
    });

    private static T Switch<T>(AudioRoom r, int x, int y, string id = "line") where T : obj_machinery_conveyor_switch
        => r.In(() => Dm.@new<T>(locate(x, y, 1), id));

    private static (float X, float Y) At(AudioRoom r, atom_movable a) => r.In(() => (((atom)a.loc).x, ((atom)a.loc).y));

    // tg's item interactions read usr (view() in the switch assembly's), which a click sets
    private static T AsUsr<T>(AudioRoom r, mob user, Func<T> body) => r.In(() =>
    {
        mob caller = usr;
        usr = user;
        try { return body(); }
        finally { usr = caller; }
    });

    private static void Hand(AudioRoom r, obj_machinery_conveyor_switch sw, mob user, bool right = false)
        => r.In(() => right ? Waiting.NoSleep(sw.attack_hand_secondary(user)) : Waiting.NoSleep(sw.attack_hand(user)));

    [Fact]
    public void A_switch_runs_its_belts_and_what_lies_on_them_rides_to_where_they_end()
    {
        var (r, bob) = Room();
        List<obj_machinery_conveyor> belts = Line(r, 3, 6, EAST);
        var sw = Switch<obj_machinery_conveyor_switch>(r, 3, Y + 2);
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(3, Y, 1)));
        r.Step(2);
        Assert.All(belts, b => Assert.Equal("conveyor0", r.In(() => Dm.str(b.icon_state))));
        Assert.Equal("switch-off", r.In(() => Dm.str(sw.icon_state)));
        r.Step(Second);
        Assert.Equal((3f, (float)Y), At(r, wrench)); // a stopped belt carries nothing

        Hand(r, sw, bob);
        Assert.Equal(1f, r.In(() => Dm.num(sw.position)));
        Assert.Equal("switch-fwd", r.In(() => Dm.str(sw.icon_state)));
        Assert.All(belts, b => Assert.Equal("conveyor1", r.In(() => Dm.str(b.icon_state))));
        Assert.NotEmpty(r.Sounds.Of(bob, Lever));
        // a tile per 0.2 s (4 ticks): four belts carry it four tiles, off the last one, and it stops there
        r.Step(Second);
        Assert.Equal((7f, (float)Y), At(r, wrench));
        r.Step(Second);
        Assert.Equal((7f, (float)Y), At(r, wrench));

        // off: the lever's stop, the belts' still icon, and a thing put on a belt stays
        Hand(r, sw, bob);
        Assert.Equal(0f, r.In(() => Dm.num(sw.position)));
        Assert.NotEmpty(r.Sounds.Of(bob, LeverStop));
        Assert.All(belts, b => Assert.Equal("conveyor0", r.In(() => Dm.str(b.icon_state))));
        var crowbar = r.In(() => Dm.@new<obj_item_crowbar>(locate(4, Y, 1)));
        r.Step(Second);
        Assert.Equal((4f, (float)Y), At(r, crowbar));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_right_button_runs_them_backward_and_a_one_way_switch_only_forward()
    {
        var (r, bob) = Room();
        List<obj_machinery_conveyor> belts = Line(r, 3, 6, EAST);
        var sw = Switch<obj_machinery_conveyor_switch>(r, 3, Y + 2);
        var oneway = Switch<obj_machinery_conveyor_switch_oneway>(r, 5, Y + 2);
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(6, Y, 1)));

        Hand(r, sw, bob, right: true);
        Assert.Equal(-1f, r.In(() => Dm.num(sw.position)));
        Assert.Equal("switch-rev", r.In(() => Dm.str(sw.icon_state)));
        Assert.Equal(-1f, r.In(() => Dm.num(oneway.position))); // the switches of one id follow each other
        Assert.All(belts, b => Assert.Equal("conveyor-1", r.In(() => Dm.str(b.icon_state))));
        r.Step(Second);
        Assert.Equal((2f, (float)Y), At(r, wrench));

        Hand(r, sw, bob); // off
        Hand(r, oneway, bob, right: true); // a one-way switch's right button runs forward
        Assert.Equal(1f, r.In(() => Dm.num(oneway.position)));
        Assert.All(belts, b => Assert.Equal("conveyor1", r.In(() => Dm.str(b.icon_state))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_human_rides_a_belt_and_a_corner_turns_what_it_carries()
    {
        var (r, bob) = Room();
        // east along Y from x 3 to 5, then a corner at (6, Y) turning north (dir NORTHEAST goes east forward: tg's update_move_direction
        // gives a NORTHWEST belt north forward), up a belt at (6, Y + 1)
        List<obj_machinery_conveyor> belts = Line(r, 3, 5, EAST);
        var corner = r.In(() => Dm.@new<obj_machinery_conveyor>(locate(6, Y, 1), (float)NORTHWEST, "line"));
        var up = r.In(() => Dm.@new<obj_machinery_conveyor>(locate(6, Y + 1, 1), (float)NORTH, "line"));
        Assert.Equal(NORTH, r.In(() => Dm.num(corner.forwards)));
        var sw = Switch<obj_machinery_conveyor_switch>(r, 2, Y + 2);
        mob_living_carbon_human pat = r.Dummy(3, Y);
        var box = r.In(() => Dm.@new<obj_item_wrench>(locate(5, Y, 1)));
        r.Step(2);

        Hand(r, sw, bob);
        r.Step(2 * Second);
        Assert.Equal((6f, Y + 2f), At(r, box)); // east, then north at the corner, off the last belt
        Assert.Equal((6f, Y + 2f), At(r, pat));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_screwdriver_inverts_or_flips_a_belt_and_its_icon_state()
    {
        var (r, bob) = Room();
        obj_machinery_conveyor belt = Line(r, 4, 4, EAST)[0];
        var screwdriver = r.In(() => Dm.@new<obj_item_screwdriver>(locate(2, Y + 3, 1)));
        r.In(() => Waiting.NoSleep(belt.screwdriver_act(bob, screwdriver)));
        Assert.Equal(1f, r.In(() => belt.inverted));
        Assert.Equal(WEST, r.In(() => Dm.num(belt.forwards)));
        Assert.Equal("conveyor0", r.In(() => Dm.str(belt.icon_state))); // a stopped inverted belt is tg's still state, not "-0"
        var sw = Switch<obj_machinery_conveyor_switch>(r, 3, Y + 2);
        Hand(r, sw, bob);
        Assert.Equal("conveyor-1", r.In(() => Dm.str(belt.icon_state)));
        r.In(() => Waiting.NoSleep(belt.screwdriver_act_secondary(bob, screwdriver)));
        Assert.Equal("conveyor-1-flipped", r.In(() => Dm.str(belt.icon_state)));
        Assert.Equal(EAST, r.In(() => Dm.num(belt.forwards))); // inverted and flipped cancel out
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_crowbar_lifts_a_belt_into_a_stack_that_lays_belts_and_a_switch_assembly_builds_a_switch()
    {
        var (r, bob) = Room();
        obj_machinery_conveyor belt = Line(r, 3, 3, EAST, "kept")[0];
        var crowbar = r.In(() => Dm.@new<obj_item_crowbar>(locate(2, Y + 3, 1)));
        r.In(() => Waiting.Detach(belt.crowbar_act(bob, crowbar)));
        r.Step(5 * Second); // tg's 4 s pry
        Assert.True(r.In(() => QDELETED(belt)));
        obj_item_stack_conveyor stack = r.In(() => Dm.locate<obj_item_stack_conveyor>(locate(3, Y, 1)));
        Assert.NotNull(stack);
        Assert.Equal("kept", r.In(() => stack.id));

        // laid on a floor tile south of the layer, the belt faces the layer and keeps the stack's id (tg lays belts and switches on
        // floors only, isfloorturf, and the toy room's tiles are indestructible)
        r.In(() =>
        {
            Waiting.NoSleep(locate(2, Y + 2, 1).ChangeTurf(typeof(turf_open_floor_iron)));
            Waiting.NoSleep(locate(3, Y + 3, 1).ChangeTurf(typeof(turf_open_floor_iron)));
        });
        AsUsr(r, bob, () => Waiting.NoSleep(stack.interact_with_atom(locate(2, Y + 2, 1), bob)));
        obj_machinery_conveyor laid = r.In(() => Dm.locate<obj_machinery_conveyor>(locate(2, Y + 2, 1)));
        Assert.Equal(NORTH, r.In(() => Dm.num(laid.dir)));
        Assert.Equal("kept", r.In(() => laid.id));
        Assert.True(r.In(() => QDELETED(stack)), "the stack's one belt is used");

        // a switch assembly finds no belt of its own (random) id in view; linked by hand to the laid belt's id it builds a switch
        var assembly = r.In(() => Dm.@new<obj_item_conveyor_switch_construct>(locate(2, Y + 3, 1)));
        Assert.NotEqual("kept", r.In(() => assembly.id));
        Assert.Equal((float)ITEM_INTERACT_BLOCKING, AsUsr(r, bob, () => Dm.num(Waiting.NoSleep(assembly.interact_with_atom(locate(3, Y + 3, 1), bob)))));
        r.In(() => assembly.id = "kept");
        Assert.Equal((float)ITEM_INTERACT_SUCCESS, AsUsr(r, bob, () => Dm.num(Waiting.NoSleep(assembly.interact_with_atom(locate(3, Y + 3, 1), bob)))));
        obj_machinery_conveyor_switch built = r.In(() => Dm.locate<obj_machinery_conveyor_switch>(locate(3, Y + 3, 1)));
        Assert.Equal("kept", r.In(() => built.id));
        Hand(r, built, bob);
        Assert.Equal("conveyor1", r.In(() => Dm.str(laid.icon_state)));
        r.AssertNoRuntimes();
    }
}

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tests.Tg.M4aAccess.P5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aTools.P4;

/// <summary>
/// M4a P4a: the welder and the base tools (tg's weldingtool.dm, wrench.dm, screwdriver.dm, crowbar.dm, wirecutters.dm) and
/// the holder's fuel (holder.dm add_reagent and remove_reagent, properties.dm get_reagent_amount): a fresh welder holds
/// tg's max_fuel of /datum/reagent/fuel, a lit welder burns a unit on the SSobj call that brings its burn time to
/// TOOL_FUEL_BURN_INTERVAL (one unit per 6 seconds), and an unlit or empty welder refuses a task.
/// </summary>
public class P4aToolTests
{
    private const int Second = 20; // ticks per second

    internal static (CapsuleRoom Room, mob_living_carbon_human A) Room(ulong seed)
    {
        var room = new CapsuleRoom(seed);
        mob_living_carbon_human a = room.Join<mob_living_carbon_human>(5, 5, "Alice");
        room.In(() => ADD_TRAIT(a, TRAIT_ADVANCEDTOOLUSER, "test"));
        return (room, a);
    }

    /// <summary>A new <typeparamref name="T"/> in <paramref name="who"/>'s active hand (tg's put_in_hands).</summary>
    internal static T Hold<T>(CapsuleRoom room, mob_living_carbon_human who) where T : obj_item => room.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        T item = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item))), "the item reaches a hand");
        return item;
    });

    internal static float Fuel(CapsuleRoom room, obj_item_weldingtool welder) => room.In(() => Dm.num(welder.get_fuel()));

    [Fact]
    public void AFreshWelderHoldsTgsMaxFuelOfWeldingFuel()
    {
        var (room, _) = Room(4601);
        var welder = room.In(() => Dm.@new<obj_item_weldingtool>(locate(7, 7, 1)));
        room.Step(Second);

        Assert.Equal(20f, room.In(() => welder.max_fuel));
        Assert.Equal(20f, room.In(() => Dm.num((welder.reagents as datum_reagents).get_reagent_amount(typeof(datum_reagent_fuel)))));
        Assert.Equal(20f, Fuel(room, welder));
        var holder = room.In(() => welder.reagents as datum_reagents);
        Assert.Equal(20f, room.In(() => holder.total_volume));
        Assert.Equal(20f, room.In(() => holder.maximum_volume));
        Assert.Equal(1, room.In(() => holder.reagent_list.len));
        var fuel = room.In(() => holder.reagent_list[1] as datum_reagent_fuel);
        Assert.NotNull(fuel);
        Assert.Equal("Welding Fuel", room.In(() => fuel.name));
        Assert.Equal(4f, room.In(() => holder.ph)); // the fuel's ph (other_reagents.dm:1380)
        Assert.Same(holder, room.In(() => fuel.holder));
        Assert.False(room.In(() => Dm.truthy(holder.is_reacting)), "fuel alone has no reaction");
        Assert.False(room.In(() => Dm.truthy(welder.isOn())));
        Assert.Equal(0f, room.In(() => Dm.num(welder.get_temperature())));
        Assert.Equal(20f, Fuel(room, room.In(() => Dm.@new<obj_item_weldingtool>(locate(7, 8, 1)))));
        Assert.Equal(0f, Fuel(room, room.In(() => Dm.@new<obj_item_weldingtool_empty>(locate(7, 9, 1)))));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void RemovingFuelLowersTheAmountAndAnEmptiedReagentLeavesTheHolder()
    {
        var (room, _) = Room(4602);
        var welder = room.In(() => Dm.@new<obj_item_weldingtool>(locate(7, 7, 1)));
        var holder = room.In(() => welder.reagents as datum_reagents);
        Assert.Equal(3f, room.In(() => Dm.num(Waiting.NoSleep(holder.remove_reagent(typeof(datum_reagent_fuel), 3)))));
        Assert.Equal(17f, Fuel(room, welder));
        Assert.Equal(17f, room.In(() => holder.total_volume));
        // more than is there removes what is there; the empty reagent leaves the list (update_total)
        Assert.Equal(17f, room.In(() => Dm.num(Waiting.NoSleep(holder.remove_reagent(typeof(datum_reagent_fuel), 50)))));
        Assert.Equal(0, room.In(() => holder.reagent_list.len));
        Assert.Equal(CHEMICAL_NORMAL_PH, room.In(() => holder.ph));
        // the holder's maximum caps an addition
        Assert.Equal(20f, room.In(() => Dm.num(Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_fuel), 25)))));
        Assert.Equal(20f, Fuel(room, welder));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ALitWelderBurnsOneUnitOfFuelEverySixSecondsAndTurnsOffWhenEmpty()
    {
        var (room, a) = Room(4603);
        var welder = Hold<obj_item_weldingtool>(room, a);
        room.In(() => Waiting.NoSleep(welder.attack_self(a)));
        Assert.True(room.In(() => Dm.truthy(welder.isOn())));
        Assert.True(room.In(() => Dm.truthy(welder.light_on)), "the lit welder's light is on");
        Assert.Equal(15f, room.In(() => welder.force));
        Assert.Equal(BURN, room.In(() => welder.damtype));
        Assert.Equal(3800f, room.In(() => Dm.num(welder.get_temperature())));
        Assert.Equal("welder1", room.In(() => welder.inhand_icon_state));
        Assert.Contains(welder, room.In(() => SSobj.processing.CopyItems()));

        // SSobj fires every 2 seconds (processing/obj.dm:5, seconds_per_tick 2) and use(TRUE) resets burned_fuel_for once
        // it reaches TOOL_FUEL_BURN_INTERVAL (5): the third call burns a unit, one unit per 6 seconds
        room.Step(30 * Second);
        Assert.InRange(Fuel(room, welder), 14.5f, 15.5f);
        room.Step(30 * Second);
        Assert.InRange(Fuel(room, welder), 9.5f, 10.5f);

        // the last units burn out and the welder turns itself off (check_fuel)
        room.Step(70 * Second);
        Assert.Equal(0f, Fuel(room, welder));
        Assert.False(room.In(() => Dm.truthy(welder.isOn())), "an empty welder turns off");
        Assert.False(room.In(() => Dm.truthy(welder.light_on)));
        Assert.Equal(3f, room.In(() => welder.force));
        room.Step(2 * Second);
        Assert.DoesNotContain(welder, room.In(() => SSobj.processing.CopyItems()));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnlitOrEmptyWelderRefusesATask()
    {
        var (room, a) = Room(4604);
        var welder = Hold<obj_item_weldingtool>(room, a);
        Assert.False(room.In(() => Dm.truthy(welder.tool_start_check(a, amount: 1))), "an unlit welder refuses");
        room.In(() => Waiting.NoSleep(welder.attack_self(a)));
        Assert.True(room.In(() => Dm.truthy(welder.tool_start_check(a, amount: 1))), "a lit one with fuel accepts");
        Assert.False(room.In(() => Dm.truthy(welder.tool_start_check(a, amount: 25))), "not more fuel than it holds");
        Assert.False(room.In(() => Dm.truthy(welder.tool_start_check(a, amount: 1, heat_required: 5000))), "not hotter than its heat");
        room.In(() => Waiting.NoSleep(welder.attack_self(a)));
        Assert.False(room.In(() => Dm.truthy(welder.isOn())), "a second use turns it off");

        var empty = Hold<obj_item_weldingtool_empty>(room, a);
        room.In(() => Waiting.NoSleep(empty.attack_self(a)));
        Assert.False(room.In(() => Dm.truthy(empty.isOn())), "an empty welder does not light (no fuel!)");
        Assert.False(room.In(() => Dm.truthy(empty.tool_start_check(a, amount: 1))));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheBaseToolsAreLiveAndTheColouredOnesPickTgsPalette()
    {
        var (room, _) = Room(4605);
        var wrench = room.In(() => Dm.@new<obj_item_wrench>(locate(7, 7, 1)));
        var crowbar = room.In(() => Dm.@new<obj_item_crowbar>(locate(7, 8, 1)));
        var screwdriver = room.In(() => Dm.@new<obj_item_screwdriver>(locate(7, 9, 1)));
        var cutters = room.In(() => Dm.@new<obj_item_wirecutters>(locate(7, 10, 1)));
        var welder = room.In(() => Dm.@new<obj_item_weldingtool>(locate(7, 11, 1)));
        foreach (atom tool in new atom[] { wrench, crowbar, screwdriver, cutters, welder })
            Assert.False(room.In(() => InertSeam.IsInert(tool)), tool.GetType().Name + " is live");

        string[] palette = { COLOR_TOOL_BLUE, COLOR_TOOL_RED, COLOR_TOOL_PINK, COLOR_TOOL_BROWN, COLOR_TOOL_GREEN, COLOR_TOOL_CYAN, COLOR_TOOL_YELLOW };
        Assert.Contains(room.In(() => screwdriver.greyscale_colors) as string, palette);
        Assert.Contains(room.In(() => cutters.greyscale_colors) as string, palette);
        Assert.Equal(TOOL_WELDER, room.In(() => welder.tool_behaviour));
        room.AssertNoRuntimes();
    }
}

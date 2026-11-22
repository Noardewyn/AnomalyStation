using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E16 (modules/manufactorio) in a booted toy room with a cable under a row of tiles and a debug RTG (20 kW) on it: a
/// sort-router pushes ore to its filter's side, into a smelter, and anything else to its unmet side; a smelter smelts ore into a sheet it
/// sends out of its front; the router splits a stack one at a time over its outputs and a multitool turns an
/// output off; the crusher crushes sheets into sheets out of its front; the storage unit keeps what it is given and hands back the last;
/// the unloader empties a crate forward and puts the opened crate to its side; the machines' arrows are the airlock's unres arrows.
/// </summary>
public class ManufacturingTests
{
    private const int Second = 20;
    private const int Y = 5;

    private static (AudioRoom r, mob_living_carbon_human bob) LineRoom(ulong seed = 16001)
    {
        var r = new AudioRoom(14, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 2; x <= 11; x++)
                Dm.@new<obj_structure_cable>(locate(x, Y, 1));
            Dm.@new<obj_machinery_power_rtg_debug>(locate(2, Y, 1));
        });
        mob_living_carbon_human bob = r.Join(6, Y + 3, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        // SSmachines' next fire: its init fire runs only the last machine of processing (MachineryTests), which since silicons SI6b
        // is the Newscaster app's prototype newscaster (SSmodular_computers makes every program at init), so the RTG feeds the
        // powernet a fire later
        r.Step(2 * Second);
        for (int i = 0; i < 10 && r.In(() => SSmachines.times_fired) < 2; i++)
            r.Step(Second);
        return (r, bob);
    }

    private static T Make<T>(AudioRoom r, int x, int y, float dir) where T : obj_machinery_power_manufacturing => r.In(() =>
    {
        T machine = Dm.@new<T>(locate(x, y, 1));
        Waiting.NoSleep(machine.setDir(dir));
        return machine;
    });

    private static List<T> On<T>(AudioRoom r, int x, int y) where T : class => r.In(() => Dm.each<T>(locate(x, y, 1).contents).ToList());

    [Fact]
    public void TheSorterSendsOreIntoTheSmelterWhichSmeltsItIntoASheet()
    {
        var (r, bob) = LineRoom();
        var sorter = Make<obj_machinery_power_manufacturing_sorter>(r, 5, Y, SOUTH);
        var smelter = Make<obj_machinery_power_manufacturing_smelter>(r, 6, Y, WEST);
        r.In(() =>
        {
            Assert.Equal(SOUTH, Dm.num(sorter.dir_if_not_met)); // the unmet side is the sorter's dir at Initialize
            var filter = Dm.@new<datum_sortrouter_filter_is_ore>(sorter);
            filter.dir_target = EAST;
            Dm.append(sorter.sort_filters, filter);
            Assert.True(Dm.num(smelter.surplus()) >= smelter.power_cost);
        });

        // ore entering the sorter goes, a fifth of a second later, to its filter's side: into the smelter, whose input side is its back (a
        // machine receives with the direction the item travels, east, which must be the reverse of the smelter's dir, west)
        var ore = r.In(() => Dm.@new<obj_item_stack_ore_iron>(locate(5, Y + 1, 1)));
        r.In(() => Waiting.NoSleep(ore.forceMove(locate(5, Y, 1))));
        // the smelter may smelt it on its next SSmanufacturing fire (1 s), whose first offset the MC draws at random (master.dm:658,
        // next_fire), so the ore is caught on the tick it leaves the sorter's tile
        for (int i = 0; i < Second / 2 && r.In(() => ore.loc == locate(5, Y, 1)); i++)
            r.Step(1);
        Assert.Same(smelter, r.In(() => ore.loc));
        // anything else goes to the unmet side
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(5, Y + 1, 1)));
        r.In(() => Waiting.NoSleep(wrench.forceMove(locate(5, Y, 1))));
        r.Step(Second / 2);
        Assert.Equal((5f, Y - 1f), r.In(() => (((atom)wrench.loc).x, ((atom)wrench.loc).y)));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(ore)));
        Assert.Equal("smelter_on", r.In(() => Dm.str(smelter.icon_state)));

        // a smelter facing east, fed from behind as a conveyor feeds it (Bumped: the direction from the machine to the item, west), smelts
        // the ore into a sheet it sends out of its front onto the floor
        var smelter2 = Make<obj_machinery_power_manufacturing_smelter>(r, 9, Y, EAST);
        var ore2 = r.In(() => Dm.@new<obj_item_stack_ore_iron>(locate(8, Y, 1)));
        Assert.Equal(MANUFACTURING_SUCCESS, r.In(() => Dm.num(Waiting.NoSleep(smelter2.receive_resource(ore2, null, WEST)))));
        r.Step(2 * Second);
        for (int i = 0; i < 10 && r.In(() => SSmachines.times_fired) < 2; i++)
            r.Step(Second);
        var sheets = On<obj_item_stack_sheet_iron>(r, 10, Y);
        Assert.Single(sheets);
        Assert.Equal(1f, r.In(() => Dm.num(sheets[0].amount)));
        Assert.Equal(0f, r.In(() => Dm.length(Dm.sub(smelter2.contents, smelter2.circuit_value))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSortersWindowAddsTurnsShiftsAndRemovesFilters()
    {
        var (r, bob) = LineRoom();
        var sorter = Make<obj_machinery_power_manufacturing_sorter>(r, 5, Y, SOUTH);
        r.In(() =>
        {
            Dm.append(sorter.sort_filters, Dm.@new<datum_sortrouter_filter_is_stack>(sorter));
            Dm.append(sorter.sort_filters, Dm.@new<datum_sortrouter_filter_name_contains>(sorter));
            var data = (DmList)Waiting.NoSleep(sorter.ui_data(bob));
            Assert.Equal(SOUTH, Dm.num(data["unmet_dir"]));
            var filters = (DmList)data["filters"];
            Assert.Equal(2f, filters.len);
            DmList first = (DmList)filters[1], second = (DmList)filters[2];
            Assert.Equal(("input is stack", NORTH), (Dm.str(first["name"]), Dm.num(first["dir"])));
            Assert.Equal("input's name contains ", Dm.str(second["name"]));

            var ui = Dm.@new<datum_tgui>(bob, sorter, "ManufacturingSorter");
            Waiting.NoSleep(sorter.ui_act("rotate", Dm.list(Dm.kv("ref", first["ref"])), ui));
            Assert.Equal(SOUTH, ((datum_sortrouter_filter)sorter.sort_filters[1]).dir_target); // NORTH, SOUTH, EAST, WEST: the next cardinal
            Waiting.NoSleep(sorter.ui_act("rotate_unmet", Dm.list(), ui));
            Assert.Equal(EAST, Dm.num(sorter.dir_if_not_met));
            Waiting.NoSleep(sorter.ui_act("shift", Dm.list(Dm.kv("ref", first["ref"]), Dm.kv("amount", "1")), ui));
            Assert.IsType<datum_sortrouter_filter_name_contains>(sorter.sort_filters[1]);
            Waiting.NoSleep(sorter.ui_act("del_filter", Dm.list(Dm.kv("ref", first["ref"])), ui));
            Assert.Equal(1f, sorter.sort_filters.len);

            // a filter that is not editable says so; the name filter matches a part of the name
            var name_filter = (datum_sortrouter_filter_name_contains)sorter.sort_filters[1];
            name_filter.value = "wren";
            var wrench = Dm.@new<obj_item_wrench>(locate(8, 8, 1));
            Assert.True(Dm.truthy(name_filter.meets_conditions(wrench)));
            Assert.False(Dm.truthy(Dm.@new<datum_sortrouter_filter_is_mail>(sorter).meets_conditions(wrench)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRouterSplitsAStackOverItsOutputsAndAMultitoolTurnsOneOff()
    {
        var (r, bob) = LineRoom();
        var router = Make<obj_machinery_power_manufacturing_router>(r, 6, 7, NORTH);
        r.In(() =>
        {
            var stack = Dm.@new<obj_item_stack_rods>(locate(9, 9, 1), 3);
            // received going north: it goes out of its other sides in turn (south first: cardinals less NORTH), one rod at a time
            Assert.Equal(MANUFACTURING_SUCCESS, Dm.num(Waiting.NoSleep(router.receive_resource(stack, null, NORTH))));
            Assert.Equal(NORTH, router.dir);
            Assert.Equal(1f, Dm.each<obj_item_stack_rods>(locate(6, 6, 1).contents).Sum(s => Dm.num(s.amount)));
            Assert.Equal(2f, Dm.num(stack.amount));
            Assert.Equal(MANUFACTURING_SUCCESS, Dm.num(Waiting.NoSleep(router.receive_resource(stack, null, NORTH))));
            Assert.Equal(1f, Dm.each<obj_item_stack_rods>(locate(7, 7, 1).contents).Sum(s => Dm.num(s.amount))); // then east
            Assert.Contains("splitter_in", Dm.each(router.overlays).Select(o => (o as IDmAppearance)?.icon_state));

            // a router never takes from another router
            var other = Dm.@new<obj_machinery_power_manufacturing_router>(locate(9, 7, 1));
            Assert.Equal(MANUFACTURING_FAIL, Dm.num(Waiting.NoSleep(router.receive_resource(stack, other, NORTH))));
        });
        // the multitool: stand east of it to toggle the east output
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(7, 7, 1))));
        r.In(() =>
        {
            var tool = Dm.@new<obj_item_multitool>(bob.loc);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(Waiting.NoSleep(router.multitool_act(bob, tool))));
            Assert.Contains(EAST, Dm.each(router.disabled_dirs).Select(d => Dm.num(d)));
            Assert.Contains("splitter_bl", Dm.each(router.overlays).Select(o => (o as IDmAppearance)?.icon_state));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCrusherCrushesSheetsOutOfItsFront()
    {
        var (r, bob) = LineRoom();
        var crusher = Make<obj_machinery_power_manufacturing_crusher>(r, 7, Y, NORTH);
        r.In(() =>
        {
            var sheets = Dm.@new<obj_item_stack_sheet_iron>(locate(7, Y - 1, 1), 2);
            // only with the receive direction its back gives (REVERSE_DIR(NORTH): SOUTH), and never ore
            Assert.Equal(MANUFACTURING_FAIL, Dm.num(Waiting.NoSleep(crusher.receive_resource(sheets, null, NORTH))));
            Assert.Equal(MANUFACTURING_FAIL, Dm.num(Waiting.NoSleep(crusher.receive_resource(Dm.@new<obj_item_stack_ore_iron>(locate(7, Y - 1, 1)), null, SOUTH))));
            Assert.Equal(MANUFACTURING_SUCCESS, Dm.num(Waiting.NoSleep(crusher.receive_resource(sheets, null, SOUTH))));
            Assert.Same(crusher, sheets.loc);
            // the arrows: out (orange) north, in (blue) south
            List<IDmAppearance> arrows = Dm.each(crusher.overlays).OfType<IDmAppearance>().ToList();
            Assert.Contains(arrows, a => a.icon_state == "unres_1" && Dm.str(a.color) == COLOR_ORANGE && a.pixel_z == 32);
            Assert.Contains(arrows, a => a.icon_state == "unres_2" && Dm.str(a.color) == COLOR_MODERATE_BLUE && a.pixel_z == -32);
        });
        r.Step(2 * Second);
        for (int i = 0; i < 10 && r.In(() => SSmachines.times_fired) < 2; i++)
            r.Step(Second);
        var crushed = On<obj_item_stack_sheet_iron>(r, 7, Y + 1);
        Assert.Equal(2f, crushed.Sum(s => r.In(() => Dm.num(s.amount))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStorageUnitKeepsWhatItIsGivenAndTheUnloaderEmptiesACrate()
    {
        var (r, bob) = LineRoom();
        var box = Make<obj_machinery_power_manufacturing_storagebox>(r, 9, Y, SOUTH);
        var unloader = Make<obj_machinery_power_manufacturing_unloader>(r, 4, Y, NORTH);
        r.In(() =>
        {
            var wrench = Dm.@new<obj_item_wrench>(locate(9, Y + 1, 1));
            var crowbar = Dm.@new<obj_item_crowbar>(locate(9, Y + 1, 1));
            Assert.Equal(MANUFACTURING_SUCCESS, Dm.num(Waiting.NoSleep(box.receive_resource(wrench, null, SOUTH))));
            Assert.Equal(MANUFACTURING_SUCCESS, Dm.num(Waiting.NoSleep(box.receive_resource(crowbar, null, SOUTH))));
            Assert.Same(crowbar, box.request_resource()); // the last put in
            // a full closet is refused
            var crate = Dm.@new<obj_structure_closet_crate>(locate(4, Y - 1, 1));
            Dm.@new<obj_item_screwdriver>(crate);
            Assert.Equal(MANUFACTURING_FAIL, Dm.num(Waiting.NoSleep(box.receive_resource(crate, null, SOUTH))));

            // the unloader takes the crate from behind (south)
            Assert.Equal(MANUFACTURING_SUCCESS, Dm.num(Waiting.NoSleep(unloader.receive_resource(crate, null, SOUTH))));
            Assert.Same(unloader, crate.loc);
            Assert.Same(crate, unloader.request_resource());
        });
        r.Step(2 * Second);
        for (int i = 0; i < 10 && r.In(() => SSmachines.times_fired) < 2; i++)
            r.Step(Second); // the unloader's machine tick: the MC staggers each subsystem's first fire by the seed (master.dm:658)
        // its contents forward (north), the empty crate to its side (turn(NORTH, -90): east)
        Assert.Single(On<obj_item_screwdriver>(r, 4, Y + 1));
        var opened = On<obj_structure_closet_crate>(r, 5, Y);
        Assert.Single(opened);
        Assert.True(r.In(() => Dm.truthy(opened[0].opened)));
        r.AssertNoRuntimes();
    }
}

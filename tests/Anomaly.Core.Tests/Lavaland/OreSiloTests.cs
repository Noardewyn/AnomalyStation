using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Lavaland;

/// <summary>Lavaland L2a (D304): tg's ore redemption machine smelts the ore that lands on its input into the ore silo it is linked to, with points, the silo's log and its holds.</summary>
public class OreSiloTests
{
    [Fact]
    public void The_redemption_machine_smelts_ore_into_its_linked_silo_and_counts_the_points()
    {
        TgToyRoom r = TgToyRoom.Boot();
        obj_machinery_ore_silo silo = null;
        obj_machinery_mineral_ore_redemption orm = null;
        r.In(() =>
        {
            silo = Dm.@new<obj_machinery_ore_silo>(locate(3, 3, 1));
            orm = Dm.@new<obj_machinery_mineral_ore_redemption>(locate(6, 5, 1));
            var miner = Dm.@new<mob_living_carbon_human_consistent>(locate(5, 5, 1));
            Assert.Null(orm.materials.silo);
            Assert.NotNull(orm.materials.mat_container); // standalone: its own store until linked

            // a multitool carries the silo to the machine
            var multitool = Dm.@new<obj_item_multitool>(locate(5, 5, 1));
            multitool.set_buffer(silo);
            Assert.Equal(ITEM_INTERACT_SUCCESS, orm.materials.OnMultitool(orm, miner, multitool));
            Assert.Same(silo, orm.materials.silo);
            Assert.Same(silo.materials, orm.materials.mat_container);
            Assert.Contains(orm.materials, Dm.each<object>(silo.ore_connected_machines));

            // ore dropped on its input (north) is smelted into the silo
            var ore = Dm.@new<obj_item_stack_ore_iron>(locate(6, 6, 1), 10);
            Assert.True(QDELETED(ore), "the ore went in");
            var iron = (datum_material)Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_iron)));
            Assert.Equal(10f * SHEET_MATERIAL_AMOUNT, Waiting.NoSleep(silo.materials.get_material_amount(iron)));
            Assert.Equal(10f, orm.points); // tg's 1 point an iron ore
            var log = (DmList)GLOB.silo_access_logs[REF(silo)];
            var entry = (datum_ore_silo_log)log[1];
            Assert.Equal("DEPOSITED", entry.action);
            Assert.Equal(orm.name, entry.machine_name);
            Assert.Contains("+10 iron", entry.get_raw_materials(""));

            // sand is smelted to glass first; it adds to the same log line only when the noun matches
            Dm.@new<obj_item_stack_ore_glass>(locate(6, 6, 1), 4);
            var glass = (datum_material)Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_glass)));
            Assert.Equal(4f * SHEET_MATERIAL_AMOUNT, Waiting.NoSleep(silo.materials.get_material_amount(glass)));

            // the machine's window lists the silo's sheets and the ore's worth
            var data = (DmList)Waiting.NoSleep(orm.ui_data(miner));
            var rows = Dm.each<DmList>(data["materials"]).ToList();
            Assert.Contains(rows, row => Dm.eq(row["name"], "iron") && Dm.num(row["amount"]) == 10 && Dm.num(row["value"]) == 1);
            Assert.Null(data["disconnected"]);

            // sheets come out on the given turf, logged as withdrawn
            Assert.Equal(3f, Waiting.NoSleep(orm.materials.eject_sheets(iron, 3, locate(2, 2, 1))));
            Assert.Equal(3f, Dm.each<obj_item_stack_sheet_iron>(locate(2, 2, 1)).Sum(s => Dm.num(s.amount)));
            Assert.Equal("WITHDRAWN", ((datum_ore_silo_log)((DmList)GLOB.silo_access_logs[REF(silo)])[1]).action);

            // the silo puts the machine on hold: no more withdrawals
            orm.materials.toggle_holding();
            Assert.True(orm.materials.on_hold());
            Assert.Equal(0f, Waiting.NoSleep(orm.materials.eject_sheets(iron, 1, locate(2, 2, 1))));
            orm.materials.toggle_holding();

            // the silo's window lists the machine and the log
            var siloData = (DmList)Waiting.NoSleep(silo.ui_data(miner));
            Assert.Equal(1, ((DmList)siloData["machines"]).Count);
            Assert.True(((DmList)siloData["logs"]).Count >= 2);

            // deleting the silo leaves the machine with a store of its own again
            qdel(silo);
            Assert.Null(orm.materials.silo);
            Assert.NotNull(orm.materials.mat_container);
            Assert.NotSame(silo.materials, orm.materials.mat_container);
        });
        Assert.Empty(r.Runtimes);
    }

    [Fact]
    public void An_ore_box_holds_ore_and_dumps_it()
    {
        TgToyRoom r = TgToyRoom.Boot();
        r.In(() =>
        {
            var box = Dm.@new<obj_structure_ore_box>(locate(4, 4, 1));
            var miner = Dm.@new<mob_living_carbon_human_consistent>(locate(5, 4, 1));
            var ore = Dm.@new<obj_item_stack_ore_iron>(locate(4, 4, 1), 5);
            Waiting.NoSleep(ore.forceMove(box));
            Waiting.NoSleep(Dm.@new<obj_item_stack_ore_plasma>(locate(4, 4, 1), 2).forceMove(box));
            var data = (DmList)Waiting.NoSleep(box.ui_data(miner));
            var rows = Dm.each<DmList>(data["materials"]).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, row => Dm.num(row["amount"]) == 5);
            Waiting.NoSleep(box.dump_box_contents());
            Assert.Empty(Dm.each<object>(box.contents));
            Assert.Same(locate(4, 4, 1), ore.loc);
        });
        Assert.Empty(r.Runtimes);
    }
}

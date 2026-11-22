using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Lavaland;

/// <summary>Lavaland L2a (D304): tg's material container, which the ore redemption machine, the ore silo and the lathes hold their materials in.</summary>
public class MaterialContainerTests
{
    [Fact]
    public void Sheets_go_in_by_their_materials_and_come_out_as_sheets_again()
    {
        TgToyRoom r = TgToyRoom.Boot();
        r.In(() =>
        {
            turf at = locate(4, 4, 1);
            var holder = Dm.@new<obj_structure_table>(at);
            var iron = (datum_material)Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_iron)));
            var container = Dm.@new<datum_material_container>(holder, Dm.list(Dm.kv(typeof(datum_material_iron), 0), Dm.kv(typeof(datum_material_glass), 0)), 20 * SHEET_MATERIAL_AMOUNT);

            var sheets = Dm.@new<obj_item_stack_sheet_iron>(at, 10);
            Assert.Equal(10f * SHEET_MATERIAL_AMOUNT, Dm.num(Waiting.NoSleep(container.insert_item(sheets))));
            Assert.True(QDELETED(sheets), "the inserted sheets are used up");
            Assert.Equal(10f * SHEET_MATERIAL_AMOUNT, Waiting.NoSleep(container.get_material_amount(iron)));
            Assert.True(Waiting.NoSleep(container.has_materials(Dm.list(Dm.kv(iron, 4 * SHEET_MATERIAL_AMOUNT)), multiplier: 2)));
            Assert.False(Waiting.NoSleep(container.has_materials(Dm.list(Dm.kv(iron, 11 * SHEET_MATERIAL_AMOUNT)))));

            // plasma is not among its materials: rejected, and the sheets stay as they were (tg's OPTIMAL_COST counts them as 1, so
            // a full container would answer "no space" first)
            var plasma = Dm.@new<obj_item_stack_sheet_mineral_plasma>(at, 5);
            Assert.Equal(MATERIAL_INSERT_ITEM_FAILURE, Dm.num(Waiting.NoSleep(container.insert_item(plasma))));
            Assert.False(QDELETED(plasma));
            Assert.Equal(5f, Dm.num(plasma.amount));

            // more than fits: the stack is split, the rest stays out
            var more = Dm.@new<obj_item_stack_sheet_iron>(at, 15);
            Assert.Equal(10f * SHEET_MATERIAL_AMOUNT, Dm.num(Waiting.NoSleep(container.insert_item(more))));
            Assert.Equal(5f, Dm.num(more.amount));
            Assert.False(Waiting.NoSleep(container.has_space(1)));

            Assert.Equal(3f * SHEET_MATERIAL_AMOUNT, Waiting.NoSleep(container.use_amount_mat(3 * SHEET_MATERIAL_AMOUNT, iron)));
            qdel(more);
            Assert.Equal(17f, Waiting.NoSleep(container.retrieve_all()));
            var dropped = Dm.each<obj_item_stack_sheet_iron>(at).ToList();
            Assert.Equal(17f, dropped.Sum(s => Dm.num(s.amount)));
            Assert.Equal(0f, Waiting.NoSleep(container.total_amount()));
        });
        Assert.Empty(r.Runtimes);
    }
}

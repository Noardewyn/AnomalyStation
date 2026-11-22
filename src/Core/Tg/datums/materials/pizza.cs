// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/pizza.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package SK1, D333, with the sheet pizza it is made of): what is made of pizza is edible as a margherita, and
// is a fishing rod's own bait. The var block is generated (D111).

// tg: code/datums/materials/pizza.dm:1
public partial class datum_material_pizza : datum_material
{
    // tg: code/datums/materials/pizza.dm:21
    public override async DmTask on_main_applied(atom source = null, object mat_amount = null, object multiplier = null)
    {
        await base.on_main_applied(source, mat_amount, multiplier);
        await make_edible(source, mat_amount);
        ADD_TRAIT(source, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this)); //the fishing rod itself is the bait... sorta.
    }

    // tg: code/datums/materials/pizza.dm:26
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        if (IS_EDIBLE(source))
            await make_edible(source, mat_amount); // tg passes the multiplier too, a third argument make_edible does not take
    }

    // tg: code/datums/materials/pizza.dm:31
    public override async DmTask on_edible_applied(atom source = null, datum_component_edible edible = null)
    {
        foreach (datum_reagent_consumable_nutriment foodchem in Dm.each<datum_reagent_consumable_nutriment>(((datum_reagents)source.reagents).reagent_list))
        {
            // a list var's compile-time value reads null (rules 10.3), as in DM
            DmList margherita_tastes = Dm.initial(typeof(obj_item_food_pizza_margherita), (obj_item_food_pizza_margherita p) => p.tastes);
            foreach (object taste in Dm.each(margherita_tastes))
            {
                DmList data = foodchem.data as DmList;
                LAZYSET(ref data, taste, 1);
                foodchem.data = data;
            }
        }
        await source.AddComponentFrom(SOURCE_EDIBLE_MEAT_MAT, typeof(datum_component_edible), Dm.named("foodtypes", Dm.bitor(Dm.bitor(GRAIN, DAIRY), VEGETABLES)));
    }

    // tg: code/datums/materials/pizza.dm:38
    public override DmTask on_edible_removed(atom source = null, datum_component_edible edible = null)
    {
        foreach (datum_reagent_consumable_nutriment foodchem in Dm.each<datum_reagent_consumable_nutriment>(((datum_reagents)source.reagents).reagent_list))
        {
            DmList margherita_tastes = Dm.initial(typeof(obj_item_food_pizza_margherita), (obj_item_food_pizza_margherita p) => p.tastes);
            foreach (object taste in Dm.each(margherita_tastes))
            {
                DmList data = foodchem.data as DmList;
                LAZYREMOVE(ref data, taste);
                foodchem.data = data;
            }
        }
        //the edible source is removed by on_removed()
        return DmTask.CompletedTask;
    }

    // tg: code/datums/materials/pizza.dm:46
    public virtual async DmTask make_edible(atom source = null, object mat_amount = null)
    {
        if (Dm.truthy(Dm.bitand(source.material_flags, MATERIAL_NO_EDIBILITY)))
            return;
        float nutriment_count = 3 * (Dm.num(mat_amount) / SHEET_MATERIAL_AMOUNT);
        float oil_count = 2 * (Dm.num(mat_amount) / SHEET_MATERIAL_AMOUNT);
        await source.AddComponentFrom(
            SOURCE_EDIBLE_PIZZA_MAT,
            typeof(datum_component_edible),
            Dm.named("initial_reagents", Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment), nutriment_count), Dm.kv(typeof(datum_reagent_consumable_nutriment_fat_oil), oil_count))),
            Dm.named("foodtypes", Dm.bitor(Dm.bitor(GRAIN, DAIRY), VEGETABLES)),
            Dm.named("eat_time", 3 * SECONDS),
            Dm.named("tastes", Dm.initial(typeof(obj_item_food_pizza_margherita), (obj_item_food_pizza_margherita p) => p.tastes)),
            Dm.named("handmade_complexity", Dm.initial(typeof(obj_item_food_pizzaslice_margherita), (obj_item_food_pizzaslice_margherita p) => p.crafting_complexity)));
    }

    // tg: code/datums/materials/pizza.dm:58
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        source.RemoveComponentSource(SOURCE_EDIBLE_PIZZA_MAT, typeof(datum_component_edible));
    }

    // tg: code/datums/materials/pizza.dm:62
    public override void on_main_removed(atom source = null, object mat_amount = null, object multiplier = null)
    {
        base.on_main_removed(source, mat_amount, multiplier);
        REMOVE_TRAIT(source, TRAIT_ROD_REMOVE_FISHING_DUD, REF(this));
    }
}

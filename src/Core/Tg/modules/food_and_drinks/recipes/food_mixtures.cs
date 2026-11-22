// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/recipes/food_mixtures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the /datum/crafting_recipe/food procs (:1-75, M5 slice 8, the crafting menu's cooking mode) and the food chemical
// reactions below them (/datum/chemical_reaction/food, :77-368: dough, batters, cheese and the others; gaps H2, D333). The var blocks
// are generated (D111).

// tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:1
public partial class datum_crafting_recipe_food : datum_crafting_recipe
{
    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:25
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        //rarely, but a few cooking recipes (cake cat & co) don't result food items.
        // PERFORM_ALL_TESTS(focus_only/check_foodtypes) is FALSE in production (rules 1.7), so the check below (:30-50, the
        // foodtypes of the result when crafted against when spawned) never runs.
        if (!Dm.truthy(FALSE) || Dm.truthy(non_craftable) || !ispath(result, typeof(obj_item_food)))
            return;
    }

    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:52
    public override object crafting_ui_data()
    {
        DmList data = Dm.list();

        object foodtypes = get_food_types();
        data["foodtypes"] = bitfield_to_list(Dm.num(foodtypes), FOOD_FLAGS);
        data["complexity"] = get_complexity();
        data["cuisine_category"] = cuisine_category;
        data["dish_category"] = dish_category;
        data["meal_category"] = (!Dm.eq(meal_category, MEAL_COMPONENT) && Dm.truthy(Dm.bitand(foodtypes, BREAKFAST))) ? MEAL_BREAKFAST : meal_category;

        return data;
    }

    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:64
    public virtual object get_food_types()
    {
        if (ispath(result, typeof(obj_item_food)))
        {
            Type food_result = result;
            return Dm.bitand(Dm.bitor(Dm.initial(food_result, (obj_item_food f) => f.foodtypes), added_foodtypes), Dm.bitnot(removed_foodtypes));
        }
        return NONE;
    }

    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:70
    public virtual object get_complexity()
    {
        if (ispath(result, typeof(obj_item_food)))
        {
            Type food_result = result;
            return Dm.initial(food_result, (obj_item_food f) => f.crafting_complexity);
        }
        return 0;
    }
}

//////////////////////////////////////////FOOD MIXTURES////////////////////////////////////

// tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:78
public partial class datum_chemical_reaction_food : datum_chemical_reaction
{
    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:91
    public override async DmTask<object> pre_reaction_other_checks(datum_reagents holder = null)
    {
        resulting_reagent_purity = holder.get_average_purity();
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:95
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (resulting_food_path != null)
        {
            atom location = holder.my_atom.drop_location() as atom;
            for (float i = 1, i__end = Dm.num(created_volume); i <= i__end; i++)
            {
                obj_item_food result = Dm.@new(resulting_food_path, location) as obj_item_food;
                if (ispath(resulting_food_path, typeof(obj_item_food)) && resulting_reagent_purity != null)
                    ((datum_reagents)result.reagents)?.set_all_reagents_purity(Dm.num(resulting_reagent_purity));
            }
        }
        return null;
    }
}

// tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:117
public partial class datum_chemical_reaction_food_chocolatepudding : datum_chemical_reaction_food
{
    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:121
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        object dot = await base.on_reaction(holder, reaction, created_volume, __extra);
        if (!ISINRANGE(world.timeofday, 3 * HOURS + 45 * MINUTES, 4 * HOURS + 15 * MINUTES))
            return dot;
        object lastkey = holder.my_atom?.fingerprintslast;
        if (!Dm.truthy(lastkey))
            return dot;
        mob_living user = get_mob_by_ckey(lastkey) as mob_living;
        if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || !Dm.truthy(is_in_sight(user, holder.my_atom)))
            return dot;
        await user.add_mood_event("why_on_earth_are_you_making_chocolate_pudding", typeof(datum_mood_event_lost_control_of_life));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:201
public partial class datum_chemical_reaction_food_imitationcarpmeat : datum_chemical_reaction_food
{
    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:207
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        Dm.@new<obj_item_food_fishmeat_carp_imitation>(location);
        if (holder?.my_atom != null)
            qdel(holder.my_atom);
        return null;
    }
}

// tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:239
public partial class datum_chemical_reaction_food_uncooked_rice : datum_chemical_reaction_food
{
    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:244
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float i = 1, i__end = Dm.num(created_volume); i <= i__end; i++)
            Dm.@new<obj_item_food_uncooked_rice>(location);
        return null;
    }
}

// tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:335
public partial class datum_chemical_reaction_food_grounding_neutralise : datum_chemical_reaction_food
{
    // tg: code/modules/food_and_drinks/recipes/food_mixtures.dm:345
    public override async DmTask<object> pre_reaction_other_checks(datum_reagents holder = null)
    {
        foreach (datum_reagent_consumable_liquidelectricity liquid_elec in Dm.each<datum_reagent_consumable_liquidelectricity>(holder.reagent_list))
        {
            if (Dm.truthy(LAZYACCESS(liquid_elec.data, BLOOD_DATA_ENRICHED_ETHEREAL)))
                return await base.pre_reaction_other_checks(holder);
        }

        return FALSE;
    }
}

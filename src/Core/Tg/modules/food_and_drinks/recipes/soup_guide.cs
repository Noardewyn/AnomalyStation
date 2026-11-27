// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/recipes/soup_guide.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the soups' recipe guides (a soup reaction's ingredients in its pot, its foodtypes from the bowl's glass style,
// a solid food result before a reagent one). The var blocks are generated.

// tg: code/modules/food_and_drinks/recipes/soup_guide.dm:1
public partial class datum_crafting_recipe_food_reaction_soup : datum_crafting_recipe_food_reaction
{
    // tg: code/modules/food_and_drinks/recipes/soup_guide.dm:10
    public override async DmTask New(params object[] __extra)
    {
        // What are ya using this path for if it's not a food reaction?
        if (!ispath(reaction, typeof(datum_chemical_reaction_food)))
        {
            await base.New(__extra);
            return;
        }

        Type soup_reaction = reaction;
        // If the reaction has a solid food item result, it is prioritized over reagent results
        if (ispath(Dm.initial(soup_reaction, (datum_chemical_reaction_food r) => r.resulting_food_path), typeof(obj_item_food)))
        {
            result = Dm.initial(soup_reaction, (datum_chemical_reaction_food r) => r.resulting_food_path);
            result_amount = 1;
        }

        await base.New(__extra);
    }

    // tg: code/modules/food_and_drinks/recipes/soup_guide.dm:23
    public override object get_food_types()
    {
        datum_glass_style_has_foodtype soup_style = (GLOB.glass_style_singletons[expected_container] as DmList)?[result] as datum_glass_style_has_foodtype;
        if (soup_style is datum_glass_style_has_foodtype)
            return soup_style.drink_type;

        return base.get_food_types();
    }

    // tg: code/modules/food_and_drinks/recipes/soup_guide.dm:30
    public override void setup_chemical_reaction_details(datum_chemical_reaction chemical_reaction = null)
    {
        base.setup_chemical_reaction_details(chemical_reaction);
        if (chemical_reaction is not datum_chemical_reaction_food_soup soup)
            return;
        foreach (Type ingredienttype in Dm.each<Type>(soup.required_ingredients))
            LAZYSET(ref reqs, ingredienttype, soup.required_ingredients[ingredienttype]);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/recipes/tablecraft/recipes_guide.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the recipe guides of the crafting window's cooking list (recipes that are not crafted but show how a thing is
// made): a chemical reaction's ingredients, catalysts and result, a produce console order and a biogenerator product. The var
// blocks are generated.

// Food reactions

// tg: code/modules/food_and_drinks/recipes/tablecraft/recipes_guide.dm:135
public partial class datum_crafting_recipe_food_reaction : datum_crafting_recipe_food
{
    // tg: code/modules/food_and_drinks/recipes/tablecraft/recipes_guide.dm:139
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!ispath(reaction, typeof(datum_chemical_reaction)))
            return;

        if (Dm.truthy(length(GLOB.chemical_reactions_list)))
            setup_chemical_reaction_details(GLOB.chemical_reactions_list[reaction] as datum_chemical_reaction);
        else
        {
            // May be called before chemical reactions list is instantiated
            datum_chemical_reaction chemical_reaction = Dm.@new(reaction) as datum_chemical_reaction;
            setup_chemical_reaction_details(chemical_reaction);
            qdel(chemical_reaction);
        }
        await base.New(__extra);
    }

    /**
     * Sets up information for our recipe based on the chemical reaction we have set.
     */
    // tg: code/modules/food_and_drinks/recipes/tablecraft/recipes_guide.dm:156
    public virtual void setup_chemical_reaction_details(datum_chemical_reaction chemical_reaction = null)
    {
        reqs = chemical_reaction.required_reagents?.Copy();
        chem_catalysts = LAZYLISTDUPLICATE(chemical_reaction.required_catalysts);
        if (result == null && Dm.truthy(length(chemical_reaction.results)))
        {
            result = chemical_reaction.results[1] as Type;
            result_amount = chemical_reaction.results[result];
        }
    }
}

// tg: code/modules/food_and_drinks/recipes/tablecraft/recipes_guide.dm:1272
public partial class datum_crafting_recipe_food_order : datum_crafting_recipe_food
{
    // tg: code/modules/food_and_drinks/recipes/tablecraft/recipes_guide.dm:1278
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        name = Dm.text("Store Bought []", name);
    }
}

// tg: code/modules/food_and_drinks/recipes/tablecraft/recipes_guide.dm:1474
public partial class datum_crafting_recipe_food_biogenerator : datum_crafting_recipe_food
{
    // tg: code/modules/food_and_drinks/recipes/tablecraft/recipes_guide.dm:1480
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        name = Dm.text("Synthetic []", name);
    }
}

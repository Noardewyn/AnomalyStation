// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes/special.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps P5): the randomized recipes (the secret sauce, metalgen and gorgium), generated at boot or loaded from
// data/RandomizedChemRecipes.json. The old recipe papers (/obj/item/paper/secretrecipe) stay inert: they wait for SSpersistence
// (not ported) and metalgen's metal_transmute (not-translated.md section 120). The game logs are comments (logs, M10+).

// tg: code/modules/reagents/chemistry/recipes/special.dm:7
public partial class datum_chemical_reaction_randomized : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/special.dm:1  #define RNGCHEM_INPUT "input", :2 RNGCHEM_CATALYSTS "catalysts" (file-local)
    protected const string RNGCHEM_INPUT = "input";
    protected const string RNGCHEM_CATALYSTS = "catalysts";

    // tg: code/modules/reagents/chemistry/recipes/special.dm:4  #define VALID_RANDOM_RECIPE_REAGENT(chemical_flags) (file-local)
    protected static bool VALID_RANDOM_RECIPE_REAGENT(float chemical_flags) => Dm.truthy(Dm.bitand(chemical_flags, REAGENT_CAN_BE_SYNTHESIZED)) && !Dm.truthy(Dm.bitand(chemical_flags, REAGENT_NO_RANDOM_RECIPE));

    // tg: code/modules/reagents/chemistry/recipes/special.dm:41
    [DmParams("recipe_data")]
    public override async DmTask New(params object[] __extra)
    {
        object recipe_data = Dm.arg(__extra, 0);
        await base.New(__extra);

        if (!Dm.truthy(recipe_data) || !Dm.truthy(load_recipe(recipe_data)))
        {
            log_game(Dm.text("Generating recipe for []", this));
            if (!Dm.truthy(generate_recipe()))
            {
                log_game(Dm.text("Couldn't generate recipe for []", this));
                qdel(this);
            }
        }
    }

    // tg: code/modules/reagents/chemistry/recipes/special.dm:51
    public virtual object load_recipe(object recipe_data = null)
    {
        DmList data = recipe_data as DmList;
        // Timestamp
        created = text2num(data["timestamp"]);
        if (Dm.num(daysSince(created)) > persistence_period)
        {
            log_game(Dm.text("Recipe [] expired.", this));
            return FALSE;
        }
        //ingredients and catalysts
        required_reagents = unwrap_reagent_list(data["required_reagents"] as DmList) as DmList;
        if (required_reagents == null)
        {
            log_game(Dm.text("Couldn't load reagents for []", this));
            return FALSE;
        }
        required_catalysts = unwrap_reagent_list(data["required_catalysts"] as DmList) as DmList;
        if (required_catalysts == null)
        {
            log_game(Dm.text("Couldn't load catalysts for []", this));
            return FALSE;
        }

        //container
        if (possible_containers != null)
        {
            Type container = Dm.truthy(data["required_container"]) ? text2path(data["required_container"] as string) : null;
            if (container == null)
            {
                log_game(Dm.text("Couldn't load container for []", this));
                return FALSE;
            }
            required_container = container;
        }

        //temperature
        is_cold_recipe = Dm.num(data["is_cold_recipe"]);
        required_temp = Dm.num(data["required_temp"]);
        optimal_temp = Dm.num(data["optimal_temp"]);
        overheat_temp = Dm.num(data["overheat_temp"]);
        thermic_constant = Dm.num(data["thermic_constant"]);

        //pH
        optimal_ph_min = Dm.num(data["optimal_ph_min"]);
        optimal_ph_max = Dm.num(data["optimal_ph_max"]);
        determin_ph_range = Dm.num(data["determin_ph_range"]);
        H_ion_release = Dm.num(data["H_ion_release"]);

        //purity
        purity_min = Dm.num(data["purity_min"]);

        return TRUE;
    }

    // tg: code/modules/reagents/chemistry/recipes/special.dm:94
    public virtual object generate_recipe()
    {
        // Timestamp
        created = world.realtime;

        // Reagents and catalysts
        DmList remaining_possible_reagents = GetPossibleReagents(RNGCHEM_INPUT) as DmList;
        DmList remaining_possible_catalysts = GetPossibleReagents(RNGCHEM_CATALYSTS) as DmList;
        //We're going to assume we're not doing any weird partial reactions for now.
        foreach (object reagent_type in Dm.each(results))
        {
            Dm.remove(ref remaining_possible_catalysts, reagent_type);
            Dm.remove(ref remaining_possible_reagents, reagent_type);
        }

        if (remaining_possible_reagents.len < min_input_reagents)
        {
            log_game(Dm.text("Couldn't find enough reagents for []", this));
            return FALSE;
        }

        required_reagents = Dm.list();
        float in_reagent_count = Dm.min(rand(min_input_reagents, max_input_reagents), remaining_possible_reagents.len);
        for (float i = 1; i <= in_reagent_count; i++)
        {
            object r_id = pick_n_take(remaining_possible_reagents);
            required_reagents[r_id] = rand(min_input_reagent_amount, max_input_reagent_amount);
            Dm.remove(ref remaining_possible_catalysts, r_id); //Can't have same reagents both as catalyst and reagent. Or can we ?
        }

        if (remaining_possible_catalysts.len < min_catalysts)
        {
            log_game(Dm.text("Couldn't find enough catalysts for []", this));
            return FALSE;
        }

        required_catalysts = Dm.list();
        float in_catalyst_count = Dm.min(rand(min_catalysts, max_catalysts), remaining_possible_catalysts.len);
        for (float i = 1; i <= in_catalyst_count; i++)
            required_catalysts[pick_n_take(remaining_possible_catalysts)] = rand(min_input_reagent_amount, max_input_reagent_amount);

        // Container
        if (possible_containers != null)
            required_container = pick(possible_containers) as Type;
        // Temperature
        is_cold_recipe = Dm.num(pick(TRUE, FALSE));
        if (Dm.truthy(is_cold_recipe))
        {
            required_temp = rand(min_temp + 50, max_temp);
            optimal_temp = rand(min_temp + 25, required_temp - 10);
            overheat_temp = rand(min_temp, optimal_temp - 10);
            if (overheat_temp >= 200) //Otherwise it can disappear when you're mixing and I don't want this to happen here
                overheat_temp = 200;
        }
        else
        {
            required_temp = rand(min_temp, max_temp - 50);
            optimal_temp = rand(required_temp + 10, max_temp - 25);
            overheat_temp = rand(optimal_temp, max_temp + 50);
            if (overheat_temp <= 400)
                overheat_temp = 400;
        }

        //pH
        optimal_ph_min = CHEMICAL_MIN_PH + rand(0, suboptimal_range_ph);
        optimal_ph_max = Dm.max(CHEMICAL_MAX_PH - rand(0, suboptimal_range_ph), optimal_ph_min + 1); //Always ensure we've a window of 1
        determin_ph_range = suboptimal_range_ph;
        H_ion_release = rand(0, 25) / 100; // 0 - 0.25

        // Purity
        purity_min = rand(0, 4) / 10;
        return TRUE;
    }

    /**
     * Returns the reagents to select for randomizing
     * Arguments
     *
     * * kind - see above defines
    */
    // tg: code/modules/reagents/chemistry/recipes/special.dm:160
    public virtual object GetPossibleReagents(object kind = null)
    {
        return Dm.list();
    }

    /**
     * Converts a list of text reagent paths into actual reagents
     * Arguments
     *
     * * list/textreagents - the list reagents
    */
    // tg: code/modules/reagents/chemistry/recipes/special.dm:171
    public virtual object unwrap_reagent_list(DmList textreagents = null)
    {
        DmList dot = Dm.list();
        foreach (object R in Dm.each(textreagents))
        {
            Type pathR = text2path(R as string);
            if (pathR == null)
                return null;
            dot[pathR] = textreagents[R];
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/special.dm:182
public partial class datum_chemical_reaction_randomized_secret_sauce : datum_chemical_reaction_randomized
{
    // tg: code/modules/reagents/chemistry/recipes/special.dm:186
    public override object GetPossibleReagents(object kind = null)
    {
        switch (kind)
        {
            case RNGCHEM_INPUT:
            case RNGCHEM_CATALYSTS:
            {
                DmList food_reagent_ids = Dm.list();
                foreach (Type food in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_reagent_consumable))))
                {
                    float chemical_flags = Dm.initial(food, (datum_reagent r) => r.chemical_flags);
                    if (VALID_RANDOM_RECIPE_REAGENT(chemical_flags))
                        Dm.append(food_reagent_ids, food);
                }
                return food_reagent_ids;
            }
        }
        return base.GetPossibleReagents(kind);
    }
}

///Random recipe for meme chem metalgen. Always requires wittel and resets every 3 days
// tg: code/modules/reagents/chemistry/recipes/special.dm:199
public partial class datum_chemical_reaction_randomized_metalgen : datum_chemical_reaction_randomized
{
    // tg: code/modules/reagents/chemistry/recipes/special.dm:204
    public override object GetPossibleReagents(object kind = null)
    {
        switch (kind)
        {
            case RNGCHEM_INPUT:
            {
                DmList possible_ingredients = Dm.list();
                foreach (Type compound in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_reagent_medicine))))
                {
                    float chemical_flags = Dm.initial(compound, (datum_reagent r) => r.chemical_flags);
                    if (VALID_RANDOM_RECIPE_REAGENT(chemical_flags))
                        Dm.append(possible_ingredients, compound);
                }
                return possible_ingredients;
            }

            case RNGCHEM_CATALYSTS:
                return Dm.list(typeof(datum_reagent_wittel));
        }
        return null;
    }
}

///Random recipe that turns stuff into ROCKS
// tg: code/modules/reagents/chemistry/recipes/special.dm:219
public partial class datum_chemical_reaction_randomized_gorgium : datum_chemical_reaction_randomized
{
    // tg: code/modules/reagents/chemistry/recipes/special.dm:222
    public override object GetPossibleReagents(object kind = null)
    {
        DmList possible_ingredients = Dm.list();
        foreach (Type reagent in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_reagent))))
        {
            float chemical_flags = Dm.initial(reagent, (datum_reagent r) => r.chemical_flags);
            if (VALID_RANDOM_RECIPE_REAGENT(chemical_flags))
                Dm.append(possible_ingredients, reagent);
        }
        return possible_ingredients;
    }
}

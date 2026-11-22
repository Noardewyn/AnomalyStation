// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOB.stacked_metabolization_effect and init_chemical_side_effects translated (M8 slice A: metabolize reads
// them), and GLOB.reagent_containers, GLOB.name2reagent and build_name2reagentlist (E15: the plumbing machines' windows).
// GLOB.chemical_reagents_list is not built (the holder's add_reagent GAP: one datum of every reagent type at boot, each with
// a rand() mass). Gaps P2: GLOB.plant_traits. Gaps F1: the reaction lists and build_chemical_reactions_lists, with
// every live reaction (the inert ones wait for their packages). Gaps P5: the randomized recipes' branch, init_chemical_reagent_list
// and GLOB.chemical_reagents_list, built at its first read (DV309: the boiling points and the masses of the separator and the mass
// spectrometer; every other reader keeps the holder's GAP, the type's prototype). Gaps RG3: GLOB.blacklisted_metalgen_types (metalgen's
// transmutation).

public partial class datum_controller_global_vars
{
    //Pills & Patches
    /// List of containers the Chem Master machine can print
    // tg: code/_globalvars/lists/reagents.dm:6  GLOBAL_LIST_INIT(reagent_containers, list(...))
    public DmList reagent_containers;

    // tg: code/_globalvars/lists/reagents.dm:6  (GLOBAL_MANAGED(reagent_containers, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreagent_containers()
    {
        reagent_containers = Dm.list(
            Dm.kv(CAT_CONDIMENTS, Dm.list(
                typeof(obj_item_reagent_containers_cup_bottle),
                typeof(obj_item_reagent_containers_condiment_flour),
                typeof(obj_item_reagent_containers_condiment_sugar),
                typeof(obj_item_reagent_containers_condiment_rice),
                typeof(obj_item_reagent_containers_condiment_cornmeal),
                typeof(obj_item_reagent_containers_condiment_milk),
                typeof(obj_item_reagent_containers_condiment_soymilk),
                typeof(obj_item_reagent_containers_condiment_yoghurt),
                typeof(obj_item_reagent_containers_condiment_saltshaker),
                typeof(obj_item_reagent_containers_condiment_peppermill),
                typeof(obj_item_reagent_containers_condiment_soysauce),
                typeof(obj_item_reagent_containers_condiment_bbqsauce),
                typeof(obj_item_reagent_containers_condiment_enzyme),
                typeof(obj_item_reagent_containers_condiment_hotsauce),
                typeof(obj_item_reagent_containers_condiment_coldsauce),
                typeof(obj_item_reagent_containers_condiment_mayonnaise),
                typeof(obj_item_reagent_containers_condiment_ketchup),
                typeof(obj_item_reagent_containers_condiment_olive_oil),
                typeof(obj_item_reagent_containers_condiment_vegetable_oil),
                typeof(obj_item_reagent_containers_condiment_peanut_butter),
                typeof(obj_item_reagent_containers_condiment_cherryjelly),
                typeof(obj_item_reagent_containers_condiment_honey),
                typeof(obj_item_reagent_containers_condiment_pack))),
            Dm.kv(CAT_TUBES, Dm.list(
                typeof(obj_item_reagent_containers_cup_tube))),
            Dm.kv(CAT_PILLS, typecacheof(Dm.list(
                typeof(obj_item_reagent_containers_applicator_pill_style)))),
            Dm.kv(CAT_PATCHES, typecacheof(Dm.list(
                typeof(obj_item_reagent_containers_applicator_patch_style)))));
        Dm.append(ref gvars_datum_init_order, "reagent_containers");
    }

    // tg: code/_globalvars/lists/reagents.dm:54  GLOBAL_LIST_INIT(blacklisted_metalgen_types, typecacheof(list(...))) (gaps RG3)
    public DmList blacklisted_metalgen_types;

    // tg: code/_globalvars/lists/reagents.dm:54  (GLOBAL_MANAGED(blacklisted_metalgen_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblacklisted_metalgen_types()
    {
        blacklisted_metalgen_types = typecacheof(Dm.list(
            typeof(turf_closed_indestructible), //indestructible turfs should be indestructible, metalgen transmutation to plasma allows them to be destroyed
            typeof(turf_open_indestructible),
            typeof(turf_open_ai_visible),
            typeof(turf_open_chasm),
            typeof(turf_open_genturf),
            typeof(turf_open_lava),
            typeof(turf_open_mirage),
            typeof(turf_open_openspace),
            typeof(turf_open_space),
            typeof(turf_open_water))) as DmList;
        Dm.append(ref gvars_datum_init_order, "blacklisted_metalgen_types");
    }

    /// list of all /datum/chemical_reaction datums indexed by their typepath. Use this for general lookup stuff
    // tg: code/_globalvars/lists/reagents.dm:44  GLOBAL_LIST(chemical_reactions_list)
    public DmList chemical_reactions_list;

    // tg: code/_globalvars/lists/reagents.dm:44  (GLOBAL_UNMANAGED(chemical_reactions_list), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalchemical_reactions_list()
    {
        return;
    }

    /// list of all /datum/chemical_reaction datums. Used during chemical reactions. Indexed by REACTANT types
    // tg: code/_globalvars/lists/reagents.dm:46  GLOBAL_LIST(chemical_reactions_list_reactant_index)
    public DmList chemical_reactions_list_reactant_index;

    // tg: code/_globalvars/lists/reagents.dm:46  (GLOBAL_UNMANAGED(chemical_reactions_list_reactant_index), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalchemical_reactions_list_reactant_index()
    {
        return;
    }

    /// list of all /datum/chemical_reaction datums. Used for the reaction lookup UI. Indexed by PRODUCT type
    // tg: code/_globalvars/lists/reagents.dm:48  GLOBAL_LIST(chemical_reactions_list_product_index)
    public DmList chemical_reactions_list_product_index;

    // tg: code/_globalvars/lists/reagents.dm:48  (GLOBAL_UNMANAGED(chemical_reactions_list_product_index), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalchemical_reactions_list_product_index()
    {
        return;
    }

    /// list of all reactions with their associated product and result ids. Used for reaction lookups
    // tg: code/_globalvars/lists/reagents.dm:52  GLOBAL_LIST(chemical_reactions_results_lookup_list)
    public DmList chemical_reactions_results_lookup_list;

    // tg: code/_globalvars/lists/reagents.dm:52  (GLOBAL_UNMANAGED(chemical_reactions_results_lookup_list), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalchemical_reactions_results_lookup_list()
    {
        return;
    }

    /// list of all /datum/reagent datums indexed by reagent id. Used by chemistry stuff
    // tg: code/_globalvars/lists/reagents.dm:50  GLOBAL_LIST_INIT(chemical_reagents_list, init_chemical_reagent_list())
    // DEVIATION(DV309): no InitGlobal: the list is built by chemical_reagents_list_built() at its first read, so that the world's
    // random sequence at boot is unchanged (each entry draws its mass, rand(10, 800), twice)
    public DmList chemical_reagents_list;

    /// Map of reagent names to its datum path
    // tg: code/_globalvars/lists/reagents.dm:67  GLOBAL_LIST_INIT(name2reagent, build_name2reagentlist())
    public DmList name2reagent;

    // tg: code/_globalvars/lists/reagents.dm:67  (GLOBAL_MANAGED(name2reagent, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalname2reagent()
    {
        name2reagent = build_name2reagentlist() as DmList;
        Dm.append(ref gvars_datum_init_order, "name2reagent");
    }

    /// list of all plan traits
    // tg: code/_globalvars/lists/reagents.dm:69  GLOBAL_LIST_INIT(plant_traits, init_plant_traits())
    public DmList plant_traits;

    // tg: code/_globalvars/lists/reagents.dm:69  (GLOBAL_MANAGED(plant_traits, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplant_traits()
    {
        plant_traits = init_plant_traits() as DmList;
        Dm.append(ref gvars_datum_init_order, "plant_traits");
    }

    /// List of all reagent side effects
    // tg: code/_globalvars/lists/reagents.dm:71  GLOBAL_LIST_INIT(stacked_metabolization_effect, init_chemical_side_effects())
    public DmList stacked_metabolization_effect;

    // tg: code/_globalvars/lists/reagents.dm:71  (GLOBAL_MANAGED(stacked_metabolization_effect, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstacked_metabolization_effect()
    {
        stacked_metabolization_effect = init_chemical_side_effects() as DmList;
        Dm.append(ref gvars_datum_init_order, "stacked_metabolization_effect");
    }
}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/lists/reagents.dm:2  #define MAX_RANDOMIZED_REACTION_RETRY_ATTEMPTS 5 (file-local)
    private const float MAX_RANDOMIZED_REACTION_RETRY_ATTEMPTS = 5;

    /// Initialises all /datum/reagent into a list indexed by reagent id
    // tg: code/_globalvars/lists/reagents.dm:74
    public static object init_chemical_reagent_list()
    {
        DmList reagent_list = Dm.list();

        foreach (Type path in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_reagent))))
        {
            datum_reagent target_object = Dm.@new(path) as datum_reagent;
            target_object.mass = rand(10, 800);
            reagent_list[path] = target_object;
        }

        return reagent_list;
    }

    /// DEVIATION(DV309): GLOB.chemical_reagents_list, built by tg's init_chemical_reagent_list on its first read (port helper)
    public static DmList chemical_reagents_list_built() => GLOB.chemical_reagents_list ??= init_chemical_reagent_list() as DmList;

    // tg: code/_globalvars/lists/reagents.dm:83
    public static object check_recipe_for_conflicts(datum_chemical_reaction reaction = null, DmList reaction_lookup = null)
    {
        foreach (object x in Dm.each(reaction.required_reagents))
        {
            foreach (datum_chemical_reaction competitor in Dm.each<datum_chemical_reaction>(reaction_lookup[x]))
            {
                if (Dm.truthy(chem_recipes_do_conflict(competitor, reaction)))
                    return TRUE;
            }
        }
        return FALSE;
    }

    /**
     * Chemical Reactions - Initialises all /datum/chemical_reaction into a list
     * It is filtered into multiple lists within a list.
     * For example:
     * chemical_reactions_list_reactant_index[/datum/reagent/toxin/plasma] is a list of all reactions relating to plasma
     * For chemical reaction list product index - indexes reactions based off the product reagent type - see get_recipe_from_reagent_product() in helpers
     * For chemical reactions list lookup list - creates a bit list of info passed to the UI. This is saved to reduce lag from new windows opening, since it's a lot of data.
     */
    // tg: code/_globalvars/lists/reagents.dm:99
    public static void build_chemical_reactions_lists()
    {
        if (GLOB.chemical_reactions_list_reactant_index != null)
            return;

        //Randomized need to go last since they need to check against conflicts with normal recipes
        DmList paths = Dm.add(Dm.sub(subtypesof(typeof(datum_chemical_reaction)), typesof(typeof(datum_chemical_reaction_randomized))), subtypesof(typeof(datum_chemical_reaction_randomized))) as DmList;
        // GAP(gaps F1 -> the kitchen, xenobiology): inert reactions skipped until translated (the inert rule, types-converter.md 4
        // and 8). A live reaction, written or var-only under a live parent (the drinks and every chemistry reaction since gaps P5),
        // is made and indexed as tg; an inert one has tg procs nobody wrote (the food and soup mixtures, the slime extracts) and
        // would react with tg's numbers but none of its own procs, so it joins when its package writes it.
        DmList live = Dm.list();
        foreach (object path in Dm.each(paths))
        {
            if (!InertSeam.IsInertType((Type)path))
                live.Add(path);
        }
        paths = live;
        GLOB.chemical_reactions_list = Dm.list(); //typepath to reaction list
        GLOB.chemical_reactions_list_reactant_index = Dm.list(); //reagents to reaction list
        GLOB.chemical_reactions_results_lookup_list = Dm.list(); //UI glob
        GLOB.chemical_reactions_list_product_index = Dm.list(); //product to reaction list

        //Randomized recipes
        DmFileRef json_file = Dm.file("data/RandomizedChemRecipes.json");
        object json = null;
        if (Dm.truthy(Dm.fexists(json_file)))
            json = Dm.json_decode(Dm.file2text(json_file));

        DmList reactions = Dm.list();
        foreach (Type path in Dm.each_cast<Type>(paths))
        {
            datum_chemical_reaction reaction;
            if (ispath(path, typeof(datum_chemical_reaction_randomized)))
                reaction = Dm.@new(path, LAZYACCESS(json, Dm.text("[]", path))) as datum_chemical_reaction;
            else
                reaction = Dm.@new(path) as datum_chemical_reaction;
            if (!QDELETED(reaction)) // in case random recipe generation fail
                Dm.append(ref reactions, reaction);
        }
        // Ok so we're gonna do a thingTM here
        // I want to distribute all our reactions such that each reagent id links to as few as possible
        // I get the feeling there's a canonical way of doing this, but I don't know it
        // So instead, we're gonna wing it
        DmList reagent_to_react_count = Dm.list();
        foreach (datum_chemical_reaction reaction in Dm.each_cast<datum_chemical_reaction>(reactions))
        {
            if (!istype(reaction, typeof(datum_chemical_reaction_randomized)))
            {
                foreach (object reagent_id in Dm.each(reaction.required_reagents))
                    reagent_to_react_count[reagent_id] = Dm.num(reagent_to_react_count[reagent_id]) + 1;
            }
        }

        DmList reaction_lookup = GLOB.chemical_reactions_list_reactant_index;
        // Create filters based on a random reagent id in the required reagents list - this is used to speed up handle_reactions()
        // Basically, we only really need to care about ONE reagent, at least when initially filtering, since any others are ignorable
        // Doing this separately because it relies on the loop above, and this is easier to parse
        foreach (datum_chemical_reaction reaction in Dm.each_cast<datum_chemical_reaction>(reactions))
        {
            //check for collisions
            if (istype(reaction, typeof(datum_chemical_reaction_randomized)))
            {
                datum_chemical_reaction_randomized random_reaction = reaction as datum_chemical_reaction_randomized;
                float retry_attempts = 0;
                while (Dm.truthy(check_recipe_for_conflicts(random_reaction, reaction_lookup)))
                {
                    if (retry_attempts >= MAX_RANDOMIZED_REACTION_RETRY_ATTEMPTS || !Dm.truthy(random_reaction.generate_recipe()))
                    {
                        Dm.remove(ref reactions, reaction);
                        qdel(reaction);
                        break;
                    }
                    retry_attempts++;
                }

                // log results
                if (QDELETED(reaction))
                {
                    log_game(Dm.text("Couldn't regenerate [] due to conflicts in [] attempts.", reaction, retry_attempts));
                    continue;
                }
                else if (retry_attempts > 0)
                    log_game(Dm.text("Regenerated [] due to conflicts in [] attempts.", reaction, retry_attempts));
            }

            object preferred_id = null;
            foreach (object reagent_id in Dm.each(reaction.required_reagents))
            {
                if (preferred_id == null)
                {
                    preferred_id = reagent_id;
                    continue;
                }
                // If we would have less then they would, take it
                if (Dm.num(length(reaction_lookup[reagent_id])) < Dm.num(length(reaction_lookup[preferred_id])))
                {
                    preferred_id = reagent_id;
                    continue;
                }
                // If they potentially have more then us, we take it
                if (Dm.num(reagent_to_react_count[reagent_id]) < Dm.num(reagent_to_react_count[preferred_id]))
                {
                    preferred_id = reagent_id;
                    continue;
                }
            }
            if (preferred_id != null)
            {
                if (!Dm.truthy(reaction_lookup[preferred_id]))
                    reaction_lookup[preferred_id] = Dm.list();
                ((DmList)reaction_lookup[preferred_id]).Add(reaction);
            }
        }

        foreach (datum_chemical_reaction reaction in Dm.each_cast<datum_chemical_reaction>(reactions))
        {
            DmList product_ids = Dm.list();
            DmList reagents = Dm.list();
            DmList product_names = Dm.list();
            float bitflags = reaction.reaction_tags;

            if (!Dm.truthy(length(reaction.required_reagents))) //Skip impossible reactions
                continue;

            GLOB.chemical_reactions_list[reaction.type] = reaction;

            foreach (Type reagent in Dm.each_cast<Type>(reaction.required_reagents))
                Dm.append(ref reagents, Dm.list(Dm.list(Dm.kv("name", Dm.initial(reagent, (datum_reagent r) => r.name)), Dm.kv("id", reagent))));

            foreach (Type product in Dm.each_cast<Type>(reaction.results))
            {
                Dm.append(ref product_names, Dm.initial(product, (datum_reagent r) => r.name));
                Dm.append(ref product_ids, product);
            }

            object product_name;
            if (!Dm.truthy(length(product_names)))
            {
                DmList names = Dm.splittext(Dm.text("[]", reaction.type), "/");
                product_name = names[names.len];
            }
            else
                product_name = product_names[1];

            if (!istype(reaction, typeof(datum_chemical_reaction_randomized)))
            {
                //Master list of ALL reactions that is used in the UI lookup table. This is expensive to make, and we don't want to lag the server by creating it on UI request, so it's cached to send to UIs instantly.
                Dm.append(ref GLOB.chemical_reactions_results_lookup_list, Dm.list(Dm.list(
                    Dm.kv("name", product_name),
                    Dm.kv("id", reaction.type),
                    Dm.kv("bitflags", bitflags),
                    Dm.kv("reactants", reagents))));

                // Create filters based on each reagent id in the required reagents list - this is specifically for finding reactions from product(reagent) ids/typepaths.
                foreach (object id in Dm.each(product_ids))
                {
                    if (!Dm.truthy(GLOB.chemical_reactions_list_product_index[id]))
                        GLOB.chemical_reactions_list_product_index[id] = Dm.list();
                    ((DmList)GLOB.chemical_reactions_list_product_index[id]).Add(reaction);
                }
            }
        }
    }

    // tg: code/_globalvars/lists/reagents.dm:216
    public static object build_name2reagentlist()
    {
        DmList dot = Dm.list();

        //build map with keys stored separately
        DmList name_to_reagent = Dm.list();
        DmList only_names = Dm.list();
        // for (var/datum/reagent/reagent as anything in GLOB.chemical_reagents_list): GAP(lead): the list is not built (the holder's
        // add_reagent GAP, holder.cs); its keys are valid_subtypesof(/datum/reagent) in that order (init_chemical_reagent_list, :74).
        foreach (Type reagent in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_reagent))))
        {
            object name = Dm.initial(reagent, (datum_reagent r) => r.name);
            if (Dm.truthy(length(name)))
            {
                name_to_reagent[name] = reagent;
                Dm.append(ref only_names, name);
            }
        }

        //sort keys
        only_names = sort_list(only_names) as DmList;

        //build map with sorted keys
        foreach (object name in Dm.each(only_names))
            dot[name] = name_to_reagent[name];
        return dot;
    }

    // tg: code/_globalvars/lists/reagents.dm:235
    public static object init_chemical_side_effects()
    {
        DmList dot = null;
        dot = Dm.list();

        foreach (Type effect in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_stacked_metabolization_effect))))
            dot.Add(Dm.@new(effect));
        return dot;
    }
}

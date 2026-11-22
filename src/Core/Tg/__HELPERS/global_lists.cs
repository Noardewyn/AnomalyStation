// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/global_lists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only make_datum_reference_lists translated (GLOB.Initialize calls it at world start, boot package, wave 3),
// init_crafting_recipes (M5 slice 2: the stack recipes as crafting recipes; slice 8: the recipe datums in scope, those whose
// ingredients the port can hold), init_crafting_recipes_atoms (M5 slice 8, the crafting menu) and init_subtypes_w_path_keys (the
// M7 delamination slice: SSmaterials' dimensional themes) and init_subtypes (M9 slice D: the announcement system). init_plant_traits (gaps P2: the plant
// genes' table of the seed extractor and the plant analyzer).

//////////////////////////
/////Initial Building/////
//////////////////////////

public static partial class GlobalProcs
{
    /// Legacy procs that really should be replaced with proper _INIT macros
    // tg: code/__HELPERS/global_lists.dm:6
    public static async DmTask make_datum_reference_lists()
    {
        // I tried to eliminate this proc but I couldn't untangle their init-order interdependencies -Dominion/Cyberboss
        init_keybindings();
        GLOB.emote_list = init_emote_list() as DmList; // WHY DOES THIS NEED TO GO HERE? IT JUST INITS DATUMS
        await init_crafting_recipes();
        init_crafting_recipes_atoms();
    }

    /// Inits crafting recipe lists
    // tg: code/__HELPERS/global_lists.dm:14
    public static async DmTask init_crafting_recipes()
    {
        foreach (Type crafting_recipe_path in Dm.each<Type>(valid_subtypesof(typeof(datum_crafting_recipe))))
        {
            // GAP(M5 slice 8 -> chemistry, M8): the recipes of a chemical reaction (/datum/crafting_recipe/food/reaction and its
            // soups: their own New and setup_chemical_reaction_details read GLOB.chemical_reactions_list) stay inert and are not
            // created (DV25).
            if (InertSeam.IsInertType(crafting_recipe_path))
                continue;
            datum_crafting_recipe recipe = Dm.@new(crafting_recipe_path) as datum_crafting_recipe;
            if (Dm.eq(recipe.name, "") || recipe.result == null)
            {
                qdel(recipe);
                continue;
            }
            // GAP(M5 slice 8 -> chemistry, M8): a recipe whose ingredients or catalysts include a reagent is left out until the
            // reagent containers that hold them are translated (the port cannot hold its ingredients; docs/ai/specs/not-translated.md).
            if (!crafting_recipe_ingredients_held(recipe))
            {
                qdel(recipe);
                continue;
            }

            bool is_cooking = Dm.@in(recipe.category, GLOB.crafting_category_food);
            recipe.reqs = sort_list(recipe.reqs, "/proc/" + nameof(GlobalProcs.cmp_crafting_req_priority)) as DmList;

            if (is_cooking)
            {
                Dm.append(ref GLOB.cooking_recipes, recipe);
                GLOB.cooking_recipes_by_typepath[crafting_recipe_path] = recipe;
                if (!Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_MUST_BE_LEARNED)))
                    Dm.append(ref GLOB.cooking_recipes_default, recipe);
            }

            else
            {
                Dm.append(ref GLOB.crafting_recipes, recipe);
                GLOB.crafting_recipes_by_typepath[crafting_recipe_path] = recipe;
                if (!Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_MUST_BE_LEARNED)))
                    Dm.append(ref GLOB.crafting_recipes_default, recipe);
            }
        }

        // GAP(M5 slice 2 -> their stacks' modules): the recipe lists of stacks that are not translated are not declared, so
        // their entries are left out: gondola, corgi, monkey, xeno, leather, sinew, carp hides; sandstone, sandbags, diamond,
        // gold, silver, bananium, titanium, plastitanium, snow, adamantine, abductor minerals; wood, bamboo,
        // cloth, durathread, cardboard, bronze; sand (glass ore); runed metal (global_lists.dm:41-79).
        DmList global_stack_recipes = Dm.list(
            Dm.kv(typeof(obj_item_stack_sheet_glass), GLOB.glass_recipes),
            Dm.kv(typeof(obj_item_stack_sheet_plasmaglass), GLOB.pglass_recipes),
            Dm.kv(typeof(obj_item_stack_sheet_rglass), GLOB.reinforced_glass_recipes),
            Dm.kv(typeof(obj_item_stack_sheet_plasmarglass), GLOB.prglass_recipes),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_uranium), GLOB.uranium_recipes),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_plasma), GLOB.plasma_recipes),
            Dm.kv(typeof(obj_item_stack_sheet_iron), GLOB.metal_recipes),
            Dm.kv(typeof(obj_item_stack_sheet_plasteel), GLOB.plasteel_recipes),
            Dm.kv(typeof(obj_item_stack_sheet_plastic), GLOB.plastic_recipes),
            Dm.kv(typeof(obj_item_stack_rods), GLOB.rod_recipes)
        );

        foreach (object stack in Dm.each(global_stack_recipes))
        {
            foreach (object stack_recipe in Dm.each(global_stack_recipes[stack]))
            {
                if (istype(stack_recipe, typeof(datum_stack_recipe_list)))
                {
                    datum_stack_recipe_list stack_recipe_list = stack_recipe as datum_stack_recipe_list;
                    foreach (object nested_recipe in Dm.each(stack_recipe_list.recipes))
                    {
                        if (!Dm.truthy(nested_recipe))
                            continue;
                        datum_crafting_recipe_stack recipe = Dm.@new<datum_crafting_recipe_stack>(stack, nested_recipe);
                        if (!Dm.eq(recipe.name, "") && recipe.result != null)
                            Dm.append(ref GLOB.crafting_recipes, recipe);
                    }
                }
                else
                {
                    if (!Dm.truthy(stack_recipe))
                        continue;
                    datum_crafting_recipe_stack recipe = Dm.@new<datum_crafting_recipe_stack>(stack, stack_recipe);
                    if (!Dm.eq(recipe.name, "") && recipe.result != null)
                        Dm.append(ref GLOB.crafting_recipes, recipe);
                }
            }
        }

        DmList material_stack_recipes = Dm.list(
            SSmaterials.base_stack_recipes,
            SSmaterials.rigid_stack_recipes
        );

        foreach (DmList recipe_list in Dm.each<DmList>(material_stack_recipes))
        {
            foreach (object stack_recipe in Dm.each(recipe_list))
            {
                datum_crafting_recipe_stack recipe = Dm.@new<datum_crafting_recipe_stack>(typeof(obj_item_stack_sheet_iron), stack_recipe);
                recipe.steps = Dm.list(I18n.T("Use different materials in hand to make an item of that material"));
                Dm.append(ref GLOB.crafting_recipes, recipe);
            }
        }
    }

    /// Whether the port can hold every ingredient of the recipe (M5 slice 8's scope, not tg code): no reagent among its
    /// requirements or catalysts and no chemical reaction (the reagent containers are chemistry, M8).
    private static bool crafting_recipe_ingredients_held(datum_crafting_recipe recipe)
    {
        if (recipe.reaction != null || Dm.truthy(LAZYLEN(recipe.chem_catalysts)))
            return false;
        foreach (object req in Dm.each(recipe.reqs))
        {
            if (ispath(req, typeof(datum_reagent)))
                return false;
        }
        return true;
    }

    /// Inits atoms used in crafting recipes
    // tg: code/__HELPERS/global_lists.dm:104
    public static void init_crafting_recipes_atoms()
    {
        DmList recipe_lists = Dm.list(
            GLOB.crafting_recipes,
            GLOB.cooking_recipes
        );
        DmList atom_lists = Dm.list(
            GLOB.crafting_recipes_datums,
            GLOB.cooking_recipes_datums
        );

        for (int list_index = 1; list_index <= Dm.length(recipe_lists); list_index++)
        {
            DmList recipe_list = recipe_lists[list_index] as DmList;
            DmList atom_list = atom_lists[list_index] as DmList;
            foreach (datum_crafting_recipe recipe in Dm.each_cast<datum_crafting_recipe>(recipe_list))
            {
                // Result
                Dm.combine(ref atom_list, recipe.result);
                // Ingredients
                Dm.combine(ref atom_list, SANITIZE_LIST(recipe.reqs));
                // Catalysts
                Dm.combine(ref atom_list, SANITIZE_LIST(recipe.chem_catalysts));
                // Reaction data - required container
                if (recipe.reaction != null)
                {
                    object required_container = Dm.initial(recipe.reaction, (datum_chemical_reaction r) => r.required_container);
                    if (Dm.truthy(required_container))
                        Dm.combine(ref atom_list, required_container);
                }
                // Tools
                Dm.combine(ref atom_list, SANITIZE_LIST(recipe.tool_paths));
                // Machinery
                Dm.combine(ref atom_list, SANITIZE_LIST(recipe.machinery));
                // Structures
                Dm.combine(ref atom_list, SANITIZE_LIST(recipe.structures));
            }
        }
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/global_lists.dm:138  (M9 slice D: the announcement system's config entries)
    public static DmList init_subtypes(object prototype = null, DmList L = null)
    {
        if (!(L is DmList))
            L = Dm.list();
        // GAP(M3 P1 -> lead): generated-only types skipped until translated (DV25)
        foreach (Type path in Dm.each_cast<Type>(typesof_written(valid_subtypesof((Type)prototype))))
            Dm.append(ref L, Dm.@new(path));
        return L;
    }

    /// Inits GLOB.plant_traits
    // tg: code/__HELPERS/global_lists.dm:208
    public static object init_plant_traits()
    {
        DmList traits = Dm.list();
        foreach (Type trait_path in Dm.each_cast<Type>(subtypesof(typeof(datum_plant_gene))))
            Dm.append(ref traits, Dm.@new(trait_path));
        sort_list(traits, "/proc/" + nameof(cmp_typepaths_asc)); // tg discards sort_list's sorted copy: the list keeps subtypesof's order
        return traits;
    }

    // tg: code/__HELPERS/global_lists.dm:147
    public static async DmTask<object> init_subtypes_w_path_keys(object prototype = null, DmList L = null)
    {
        if (!(L is DmList))
            L = Dm.list();
        foreach (Type path in Dm.each_cast<Type>(valid_subtypesof((Type)prototype)))
            L[path] = Dm.@new(path);
        return L;
    }
}


// Gaps package S2: the money a player holds, which the vending window counts (tally_physical_credits).
public partial class datum_controller_global_vars
{
    /// A static typecache of all the money-based items that can be actively used as currency.
    // tg: code/__HELPERS/global_lists.dm:201  GLOBAL_LIST_INIT(allowed_money, typecacheof(list(/obj/item/coin, /obj/item/holochip, /obj/item/stack/spacecash)))
    public DmList allowed_money;

    // tg: code/__HELPERS/global_lists.dm:201  (GLOBAL_MANAGED(allowed_money, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalallowed_money()
    {
        allowed_money = typecacheof(Dm.list(
            typeof(obj_item_coin),
            typeof(obj_item_holochip),
            typeof(obj_item_stack_spacecash)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "allowed_money");
    }
}

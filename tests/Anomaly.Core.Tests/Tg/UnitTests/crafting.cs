// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/crafting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M5 slice 8): tg runs every recipe of GLOB.crafting_recipes + GLOB.cooking_recipes; here only the translated ones:
// the recipes whose result, requirements, tools, machinery and structures are all translated types (not inert, D112, D132:
// an inert result skips the materials of its Initialize, so tg's materials parity cannot hold for it), food left out (its meat
// and pizza materials are not created by SSmaterials, DV25: food, M8). The bottomless cup holds the reagent requirements and
// catalysts (since the restaurant: init_crafting_recipes lists the recipes with reagents). PERFORM_ALL_TESTS(crafting) is
// FALSE in the port (rules 1.7), so check_requirements runs: a recipe with its own check_requirements is left out. The room
// is the toy room (TgToyRoom.Install).

/**
 * Check if a generic atom (because both mobs and the crafter machinery can do it) can potentially craft all recipes,
 * with the exact same types required in the recipe.
 * Then, unless the recipe has the CRAFT_SKIP_MATERIALS_PARITY flag, compare the materials of the
 * crafted result with a spawned instance of the same type to ensure that they match.
 */
// tg: code/modules/unit_tests/crafting.dm:7 (/datum/unit_test/crafting; class named by rules 2.3)
public sealed class datum_unit_test_crafting : TgUnitTest
{
    //The object responsible for using the crafting component
    public atom_movable crafter;
    //The reagent holder responsible for holding reagents that may be used in a recipe.
    public obj_item_reagent_containers_cup bottomless_cup;
    ///The tools that have been spawned so far, to be reused in other recipes as well.
    public DmList tools = Dm.list();

    /// <summary>ADAPTED: the translated recipes run.</summary>
    public int processed;

    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        // construct_item records each craft in SSblackbox (tg's unit test skips it: PERFORM_ALL_TESTS(crafting), FALSE in the
        // port); tg's MC always creates it
        if (SSblackbox == null)
            Dm.@new<datum_controller_subsystem_blackbox>();
    }

    // tg: code/modules/unit_tests/crafting.dm:15
    public override async DmTask Run()
    {
        crafter = allocate<atom_movable>();

        clear_trash();

        turf turf = crafter.loc as turf;
        Type old_turf_type = turf.type;
        datum_component_personal_crafting_unit_test craft_comp = await crafter.AddComponent(typeof(datum_component_personal_crafting_unit_test)) as datum_component_personal_crafting_unit_test;
        bottomless_cup = allocate_bottomless_cup();

        DmList all_recipes = Dm.add(GLOB.crafting_recipes, GLOB.cooking_recipes) as DmList;
        foreach (datum_crafting_recipe recipe in Dm.each_cast<datum_crafting_recipe>(all_recipes))
        {
            if (Dm.truthy(recipe.non_craftable))
                continue;
            if (!Translated(recipe)) // ADAPTED (see the header)
                continue;
            //split into a different proc, so if something fails it's both easier to track and doesn't halt the loop.
            await process_recipe(craft_comp, recipe);
            processed++;
            // ADAPTED: the crafter's tile rather than the one Run started on (a turf recipe's ChangeTurf leaves the port a new turf object
            // where tg keeps the same one, so the cup and the crafter would part ways)
            if (QDELETED(bottomless_cup) || bottomless_cup.loc != crafter.loc) //The cup itself was used in a recipe, rather than its contents.
                bottomless_cup = allocate_bottomless_cup();
        }

        // ADAPTED: the port's own check that the adapted test is not empty
        TEST_NOTICE(this, Dm.text("translated recipes crafted: []", processed));
        TEST_ASSERT(processed >= 20, Dm.text("only [] translated recipes were run", processed));

        // We have one or two recipes that generate turfs (from stacks, like snow walls), which shouldn't be carried between tests
        if (turf.type != old_turf_type)
            await turf.ChangeTurf(old_turf_type);
    }

    ///Allocate a reagent container with infinite capacity and no reaction to use in crafting
    // tg: code/modules/unit_tests/crafting.dm:39
    private obj_item_reagent_containers_cup allocate_bottomless_cup()
    {
        obj_item_reagent_containers_cup cup = allocate<obj_item_reagent_containers_cup>();
        datum_reagents holder = (datum_reagents)cup.reagents;
        holder.flags = Dm.bitor(holder.flags, Dm.bitor(NO_REACT, DRAINABLE));
        holder.maximum_volume = INFINITY;
        return cup;
    }

    // ADAPTED: whether every type the recipe makes or uses is translated (see the header).
    private static bool Translated(datum_crafting_recipe recipe)
    {
        if (recipe.result == null || InertSeam.IsInertType(recipe.result))
            return false;
        // food made of meat: the food materials (meat, pizza) are not created by SSmaterials (DV25, M8), so its parity cannot hold
        if (ispath(recipe.result, typeof(obj_item_food)) || Dm.each(recipe.reqs).Any(req => ispath(req, typeof(obj_item_food))))
            return false;
        if (recipe.GetType().GetMethod(nameof(datum_crafting_recipe.check_requirements))?.DeclaringType != typeof(datum_crafting_recipe))
            return false;
        // the improvised chem heater's RefreshParts reads its subsystem's wait (spaceheater.dm:537); the toy room's Master lists no
        // SSmachines (as the lifecycle sweeps, E9; its recipe needs reagents, listed since the restaurant)
        if (ispath(recipe.result, typeof(obj_machinery_space_heater_improvised_chem_heater)))
            return false;
        foreach (DmList list in new[] { recipe.reqs, recipe.tool_paths, recipe.machinery, recipe.structures })
        {
            foreach (object path in Dm.each(list))
            {
                if (path is not Type t || InertSeam.IsInertType(t) || Dm.eq(Dm.initial(t, (datum d) => d.abstract_type), t))
                    return false;
            }
        }
        return true;
    }

    // tg: code/modules/unit_tests/crafting.dm:42
    private async DmTask process_recipe(datum_component_personal_crafting craft_comp, datum_crafting_recipe recipe)
    {
        turf turf = crafter.loc as turf;
        //Warn if uncreatables were found in the recipe if it fails
        //If it doesn't fail, then it was already handled, maybe through `unit_test_spawn_extras`
        DmList uncreatables_found = null;

        foreach ((object spawn_path, object amount) in Dm.pairs(recipe.unit_test_spawn_extras))
        {
            if (ispath(spawn_path, typeof(obj_item_stack)))
            {
                Dm.@new(spawn_path as Type, turf, /*new_amount =*/ amount, /*merge =*/ FALSE);
                continue;
            }
            for (int index = 1; index <= Dm.num(amount); index++)
                Dm.@new(spawn_path as Type, turf);
        }

        foreach ((object req_path, object amount) in Dm.pairs(recipe.reqs)) //spawn items and reagents
        {
            if (ispath(req_path, typeof(datum_reagent))) //it's a reagent
            {
                datum_reagents holder = (datum_reagents)bottomless_cup.reagents;
                if (!Dm.truthy(holder.has_reagent(req_path, Dm.num(amount))))
                    Waiting.NoSleep(holder.add_reagent(req_path as Type, Dm.num(amount) + 1, no_react: TRUE));
                continue;
            }

            // A recipe might accept an abstract base type as its reqs - e.g. obj/item/food/grown - signifying it can use any item of that type.
            // However, we cannot create those abstract base types... but we also cannot pick a random subtype everytime either,
            // as sutypes can have different custom_materials between them, so doing that will lead us to sporiadic errors.
            // The safest course of action is to make the contributor pick a valid subtype (as generic as possible. please) to use in its place.
            if (Dm.eq(Dm.initial(req_path as Type, (datum d) => d.abstract_type), req_path))
            {
                LAZYADD(ref uncreatables_found, req_path);
                continue;
            }

            if (ispath(req_path, typeof(obj_item_stack))) //it's a stack
            {
                Dm.@new(req_path as Type, turf, /*new_amount =*/ amount, /*merge =*/ FALSE);
                continue;
            }

            //it's any other item
            for (int iteration = 1; iteration <= Dm.num(amount); iteration++)
                Dm.@new(req_path as Type, turf);
        }

        foreach ((object req_path, object chem_amount) in Dm.pairs(recipe.chem_catalysts)) // spawn catalysts
        {
            object amount = chem_amount;
            datum_reagents holder = (datum_reagents)bottomless_cup.reagents;
            if (!Dm.truthy(holder.has_reagent(req_path, Dm.num(amount))))
                Waiting.NoSleep(holder.add_reagent(req_path as Type, Dm.num(amount) + 1, no_react: TRUE));
        }

        DmList bulky_objects = Dm.list();
        Dm.append(ref bulky_objects, Dm.add(recipe.structures, recipe.machinery)); //either structures and machinery could be null
        list_clear_nulls(bulky_objects); //so we clear the list
        foreach (object req_path in Dm.each(bulky_objects)) //spawn required machinery or structures
            Dm.@new(req_path as Type, turf);

        DmList needed_tools = Dm.list();
        Dm.append(ref needed_tools, Dm.add(recipe.tool_behaviors, recipe.tool_paths)); //either tool_behaviors and tool_paths could be null
        list_clear_nulls(needed_tools); //so we clear the list
        ///tool instances which have been moved to the crafter loc, which are moved back to nullspace once the recipe is done
        DmList summoned_tools = Dm.list();
        foreach (object tooltype in Dm.each(needed_tools))
        {
            obj_item tool = tools[tooltype] as obj_item;
            if (!QDELETED(tool))
                await tool.forceMove(turf);
            else
            {
                bool is_behaviour = istext(tooltype);
                Type path_to_use = is_behaviour ? typeof(obj_item) : tooltype as Type;
                tool = allocate(path_to_use, turf) as obj_item; //we shouldn't delete the tools and allocate and keep them between recipes
                if (is_behaviour)
                    tool.tool_behaviour = tooltype as string;
                tools[tooltype] = tool;
            }
            Dm.combine(ref summoned_tools, tool);
        }

        object result = await craft_comp.construct_item(crafter, recipe);

        foreach (atom_movable tool in Dm.each_cast<atom_movable>(summoned_tools))
            await tool.moveToNullspace();

        if (istext(result) || result == null) //construct_item() returned a text string telling us why it failed.
        {
            if (Dm.truthy(uncreatables_found))
                TEST_FAIL(Dm.text("Abstract types found in the requirements of []: []. Use the unit_test_spawn_extras list var of the recipe to spawn non-abstract subtypes instead.", recipe.type, english_list(uncreatables_found)));
            else
                TEST_FAIL(Dm.text("[] couldn't be crafted during unit test[]", recipe.type, Dm.truthy(result) ? result : ", result is null for some reason!"));
            clear_trash();
            return;
        }
        atom result_atom = result as atom;
        //enforcing materials parity between crafted and spawned for turfs would be more trouble than worth here
        if (Dm.truthy(Dm.bitand(recipe.crafting_flags, Dm.bitor(CRAFT_NO_MATERIALS, CRAFT_SKIP_MATERIALS_PARITY))) || isturf(result_atom))
        {
            clear_trash();
            return;
        }

        atom copycat;
        if (isstack(result_atom))
        {
            obj_item_stack stack_result = result_atom as obj_item_stack;
            copycat = Dm.@new(result_atom.type, turf, /*new_amount =*/ stack_result.amount, /*merge =*/ FALSE) as atom;
        }
        else
            copycat = Dm.@new(result_atom.type, turf) as atom;

        if (!Dm.truthy(result_atom.compare_materials(copycat)))
        {
            string warning = Dm.text("custom_materials of [] when crafted compared to only spawned don't match", result_atom.type);
            string other_info = "";
            string target_var = "custom_materials";
            DmList result_mats = result_atom.custom_materials;
            DmList copycat_mats = copycat.custom_materials;
            if (isstack(result_atom))
            {
                obj_item_stack stack_result = result_atom as obj_item_stack;
                obj_item_stack stack_copy = copycat as obj_item_stack;
                target_var = "mats_per_unit";
                result_mats = stack_result.mats_per_unit;
                copycat_mats = stack_copy.mats_per_unit;
                other_info = Dm.text(" (size of resulting stack: [])", stack_result.amount);
            }
            object what_it_should_be = result_atom.transcribe_materials_list(result_mats);
            object what_it_is = copycat.transcribe_materials_list(copycat_mats);
            string add_info = istype(recipe, typeof(datum_crafting_recipe_stack))
                ? "add the CRAFT_SKIP_MATERIALS_PARITY crafting flag to its stack_recipe datum"
                : Dm.text("set the requirements_mats_blacklist or removed_mats var of [], or add the CRAFT_SKIP_MATERIALS_PARITY crafting flag to it", recipe.type);
            TEST_FAIL(Dm.text("[]. should be: [] = [] (current value: [])[]. Fix that. Otherwise, []", warning, target_var, what_it_should_be, what_it_is, other_info, add_info));
        }

        clear_trash();
    }

    ///Clear the area around our crafting movable of objects that may mess with the unit test
    // tg: code/modules/unit_tests/crafting.dm:182
    private void clear_trash()
    {
        foreach (atom_movable trash in Dm.each<atom_movable>(Dm.sub(range(1, crafter), Dm.list(crafter, bottomless_cup))))
            qdel(trash);
    }
}

// tg: code/modules/unit_tests/crafting.dm:187 (/datum/component/personal_crafting/unit_test: test code, no DM type)
public class datum_component_personal_crafting_unit_test : datum_component_personal_crafting
{
    public datum_component_personal_crafting_unit_test()
    {
        ignored_flags = CRAFT_MUST_BE_LEARNED | CRAFT_ONE_PER_TURF | CRAFT_CHECK_DIRECTION | CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_IGNORE_DO_AFTER;
    }
}

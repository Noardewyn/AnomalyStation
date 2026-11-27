// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crafting/_crafting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 8 (crafting): the whole file. The personal crafting component (the HUD's craft button and tg's "PersonalCrafting"
// window, D148), construct_item and its checks, the mind's learned recipes and the machine crafter. Seams and leaves: the
// window's sprite sheets are the client's own .dmi pictures (ui_assets; the atoms' icons travel in atom_data, DV44 and
// crafting.cs of the asset cache), the investigate log is M10 (admin logs), and a result or component whose type has its own
// untranslated on_craft_completion or used_in_craft is not called (DV27, InertSeam.MissingCraftOverride). The reagents
// of the containers around count as requirements and are poured into the craft's holder, and a chemical reaction's recipe shows
// its container, mixing and heating steps (the restaurant: the cook's recipes that need flour, milk or salt). The var blocks are
// generated (D111); the static global_blacklist lives in TgState (rules 10.6).

//list key declarations used in check_contents(), get_surroundings() and check_tools()
// tg: code/datums/components/crafting/_crafting.dm:12
public partial class datum_component_personal_crafting : datum_component
{
    // tg: code/datums/components/crafting/_crafting.dm:2-7 (file-local defines, rules 11.4)
    internal const string CONTENTS_INSTANCES = "instances";
    internal const string CONTENTS_MACHINERY = "machinery";
    internal const string CONTENTS_STRUCTURES = "structures";
    internal const string CONTENTS_REQS_COUNT = "reqs_count";
    internal const string CONTENTS_TOOL_BEHAVIOUR = "tool_behaviour";
    internal const string CONTENTS_POSSIBLE_TOOLS = "tool_instances";

    /// The portion of time spent crafting that recipe dependant on the speed of the tools
    // tg: code/datums/components/crafting/_crafting.dm:10
    private const float RECIPE_DYNAMIC_TIME_COEFF = 0.85f;

    // tg: code/datums/components/crafting/_crafting.dm:42-43 (file-local defines)
    private const float COOKING = TRUE;
    private const float CRAFTING = FALSE;

    /// Global crafting blacklist. These should be excluded from all crafting recipes no matter what.
    // tg: code/datums/components/crafting/_crafting.dm:54  var/static/list/global_blacklist (a type-level static, rules 10.6)
    private static ref DmList global_blacklist => ref TgState.Current.datum_component_personal_crafting__global_blacklist;

    // tg: code/datums/components/crafting/_crafting.dm:16
    [DmParams("screen_loc_override")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object screen_loc_override = Dm.arg(__extra, 0);
        this.screen_loc_override = screen_loc_override;
        return null;
    }

    // tg: code/datums/components/crafting/_crafting.dm:19
    public override async DmTask RegisterWithParent()
    {
        if (!ismob(parent))
            return;

        mob user = parent as mob;
        RegisterSignal(parent, COMSIG_MOB_HUD_CREATED, nameof(on_hud_created));
        if (Dm.truthy(user.hud_used))
            on_hud_created();
    }

    // tg: code/datums/components/crafting/_crafting.dm:28
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (!ismob(parent))
            return;
        mob user = parent as mob;
        UnregisterSignal(parent, COMSIG_MOB_HUD_CREATED);
        if (user.hud_used != null)
            await user.hud_used.remove_screen_object(HUD_MOB_CRAFTING_MENU);
    }

    // tg: code/datums/components/crafting/_crafting.dm:35
    [SignalHandler]
    public virtual void on_hud_created(datum source = null)
    {
        mob user = parent as mob;
        atom_movable_screen screen_obj = Waiting.NoSleep(user.hud_used.add_screen_object(typeof(atom_movable_screen_craft), HUD_MOB_CRAFTING_MENU, HUD_GROUP_STATIC, user.hud_used.ui_style, screen_loc_override, update_screen: TRUE)) as atom_movable_screen;
        RegisterSignal(screen_obj, COMSIG_SCREEN_ELEMENT_CLICK, nameof(component_ui_interact));
    }

    /* This is what procs do:
        get_environment - gets a list of things accessable for crafting by user
        get_surroundings - takes a list of things and makes a list of key-types to values-amounts of said type in the list
        check_contents - takes a recipe and a key-type list and checks if said recipe can be done with available stuff
        check_tools - takes recipe, a key-type list, and a user and checks if there are enough tools to do the stuff, checks bugs one level deep
        construct_item - takes a recipe and a user, call all the checking procs, calls do_after, checks all the things again, calls del_reqs, creates result, calls CheckParts of said result with argument being list returned by deel_reqs
        get_used_reqs - takes recipe, a user and a list (for mats), loops over the recipes reqs var and tries to find everything in the list make by get_environment and returns a list of the components to be used
    */

    /**
     * Check that the contents of the recipe meet the requirements.
     *
     * user: The /mob that initated the crafting.
     * recipe: The /datum/crafting_recipe being attempted.
     * contents: List of items to search for the recipe's reqs.
     */
    // tg: code/datums/components/crafting/_crafting.dm:91
    public virtual object check_contents(atom a = null, datum_crafting_recipe recipe = null, DmList contents = null)
    {
        DmList item_instances = contents[CONTENTS_INSTANCES] as DmList;
        DmList machines = contents[CONTENTS_MACHINERY] as DmList;
        DmList structures = contents[CONTENTS_STRUCTURES] as DmList;
        contents = contents[CONTENTS_REQS_COUNT] as DmList;

        DmList requirements_list = Dm.list();

        // Process all requirements
        object recipe_result = null;
        if (Dm.truthy(recipe.blacklist_result))
            recipe_result = recipe.result;
        foreach ((object requirement_path_obj, object needed) in Dm.pairs(recipe.reqs))
        {
            Type requirement_path = requirement_path_obj as Type;
            float needed_amount = Dm.num(needed);
            // Check we have the appropriate amount available in the contents list
            foreach (object content_item_path in Dm.each(contents))
            {
                // Right path and not blacklisted
                if (!ispath(content_item_path, requirement_path) || Dm.@in(content_item_path, recipe.blacklist))
                    continue;
                // If we are a recipe that is blacklisting its result, make sure we skip that path
                if (Dm.truthy(recipe_result) && Dm.eq(content_item_path, recipe_result))
                    continue;

                needed_amount -= Dm.num(contents[content_item_path]);
                if (needed_amount <= 0)
                    break;
            }

            if (needed_amount > 0)
                return FALSE;

            if (!Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_COLLECT_REQUIREMENTS)))
                continue;

            // Store the instances of what we will use for recipe.check_requirements() for requirement_path
            DmList instances_list = Dm.list();
            foreach ((object instance_path, object item_instance) in Dm.pairs(item_instances))
            {
                if (ispath(instance_path, requirement_path))
                    Dm.append(ref instances_list, item_instance);
            }

            requirements_list[requirement_path] = instances_list;
        }

        foreach ((object requirement_path, object chem_amount) in Dm.pairs(recipe.chem_catalysts))
        {
            if (Dm.lt(contents[requirement_path], chem_amount))
                return FALSE;
        }

        bool mech_found = false;
        foreach (object machinery_path in Dm.each(recipe.machinery))
        {
            mech_found = false;
            foreach (object machine in Dm.each(machines))
            {
                if (ispath(machine, machinery_path as Type))// We only need one machine per key, unlike items
                {
                    mech_found = true;
                    break;
                }
            }
            if (!mech_found)
                return FALSE;
        }

        bool found = false;
        foreach (object structure_path in Dm.each(recipe.structures))
        {
            found = false;
            foreach (object structure in Dm.each(structures))
            {
                if (ispath(structure, structure_path as Type))// We only need one structure per key, unlike items
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                return FALSE;
        }

        //Skip extra requirements when unit testing, like, underwater basket weaving? Get the hell out of here
        // PERFORM_ALL_TESTS(crafting) is FALSE in production (rules 1.7)
        return Dm.truthy(FALSE) ? TRUE : recipe.check_requirements(a as mob, requirements_list);
    }

    // tg: code/datums/components/crafting/_crafting.dm:159
    public virtual DmList get_environment(atom a = null, DmList blacklist = null, float? radius_range = null)
    {
        radius_range ??= 1;
        DmList dot = Dm.list();

        if (!isturf(a.loc))
            return dot;

        foreach (atom_movable AM in Dm.each<atom_movable>(range(radius_range, a)))
        {
            if (Dm.truthy(Dm.bitand(AM.flags_1, HOLOGRAM_1)) || (Dm.truthy(blacklist) && Dm.@in(AM.type, blacklist)))
                continue;
            if (isitem(AM))
            {
                obj_item item = AM as obj_item;
                if (Dm.truthy(Dm.bitand(item.item_flags, ABSTRACT))) //let's not tempt fate, shall we?
                    continue;
            }
            Dm.append(ref dot, AM);
        }
        return dot;
    }

    // tg: code/datums/components/crafting/_crafting.dm:174
    public virtual DmList get_surroundings(atom source = null, DmList blacklist = null)
    {
        DmList dot = Dm.list();
        dot[CONTENTS_TOOL_BEHAVIOUR] = Dm.list();
        dot[CONTENTS_REQS_COUNT] = Dm.list();
        dot[CONTENTS_INSTANCES] = Dm.list();
        dot[CONTENTS_MACHINERY] = Dm.list();
        dot[CONTENTS_STRUCTURES] = Dm.list();
        DmList tool_behaviour = dot[CONTENTS_TOOL_BEHAVIOUR] as DmList;
        DmList reqs_count = dot[CONTENTS_REQS_COUNT] as DmList;
        foreach (obj @object in Dm.each<obj>(get_environment(source, blacklist)))
        {
            if (isitem(@object))
            {
                obj_item item = @object as obj_item;
                DmList instances_lazy = dot[CONTENTS_INSTANCES] as DmList;
                LAZYADDASSOCLIST(ref instances_lazy, item.type, item);
                dot[CONTENTS_INSTANCES] = instances_lazy;
                if (Dm.truthy(item.tool_behaviour))
                {
                    object current_tool_speed = tool_behaviour[item.tool_behaviour];
                    if (Dm.lt(current_tool_speed, item.toolspeed))
                        tool_behaviour[item.tool_behaviour] = item.toolspeed;
                }
                // Blacklisted items can be tools but not components
                if (Dm.truthy(is_type_in_typecache(item.type, global_blacklist)))
                    continue;
                if (isstack(item))
                {
                    obj_item_stack stack = item as obj_item_stack;
                    Dm.append_at(reqs_count, item.type, stack.amount);
                }
                else
                {
                    Dm.append_at(reqs_count, item.type, 1);
                    if (is_reagent_container(item) && Dm.truthy(item.is_drainable()) && Dm.truthy(length(((datum_reagents)item.reagents).reagent_list))) //some container that has some reagents inside it that can be drained
                    {
                        obj_item_reagent_containers container = (obj_item_reagent_containers)item;
                        foreach (datum_reagent reagent in Dm.each<datum_reagent>(((datum_reagents)container.reagents).reagent_list))
                            Dm.append_at(reqs_count, reagent.type, reagent.volume);
                    }
                }
            }
            else if (ismachinery(@object))
            {
                DmList machinery_lazy = dot[CONTENTS_MACHINERY] as DmList;
                LAZYADDASSOCLIST(ref machinery_lazy, @object.type, @object);
                dot[CONTENTS_MACHINERY] = machinery_lazy;
            }
            else if (isstructure(@object))
            {
                DmList structures_lazy = dot[CONTENTS_STRUCTURES] as DmList;
                LAZYADDASSOCLIST(ref structures_lazy, @object.type, @object);
                dot[CONTENTS_STRUCTURES] = structures_lazy;
            }
        }

        DmList within_source = Dm.list();
        foreach (obj_item item in Dm.each<obj_item>(source.contents))
        {
            Dm.append(ref within_source, item);
            if (Dm.truthy(item.atom_storage))
            {
                foreach (obj_item item_stored_within in Dm.each<obj_item>(item.contents))
                    Dm.append(ref within_source, item_stored_within);
            }
        }

        foreach (obj_item item in Dm.each_cast<obj_item>(within_source))
        {
            if (!Dm.truthy(item.tool_behaviour))
                continue;
            object current_tool_speed = tool_behaviour[item.tool_behaviour];
            if (Dm.lt(current_tool_speed, item.toolspeed))
                tool_behaviour[item.tool_behaviour] = item.toolspeed;
        }

        // .[CONTENTS_POSSIBLE_TOOLS] += instances[item_type] for each type, then += within_source: the key starts empty (null), and
        // a null element += a list takes that list's items; a fresh list here keeps the instance lists unaliased.
        DmList instances = dot[CONTENTS_INSTANCES] as DmList;
        DmList possible_tools = Dm.list();
        foreach (object item_type in Dm.each(instances))
            Dm.append(ref possible_tools, instances[item_type]);
        Dm.append(ref possible_tools, within_source);
        dot[CONTENTS_POSSIBLE_TOOLS] = possible_tools;
        return dot;
    }

    /// Returns a boolean on whether the tool requirements of the input recipe are satisfied by the input source and surroundings.
    // tg: code/datums/components/crafting/_crafting.dm:226
    public virtual object check_tools(atom source = null, datum_crafting_recipe recipe = null, DmList surroundings = null, float? final_check = null)
    {
        final_check ??= FALSE;
        if (!Dm.truthy(Dm.length(recipe.tool_behaviors)) && !Dm.truthy(Dm.length(recipe.tool_paths)))
            return TRUE;

        foreach (object required_quality in Dm.each(recipe.tool_behaviors))
        {
            if (!Dm.@in(required_quality, surroundings[CONTENTS_TOOL_BEHAVIOUR]))
                return FALSE;
        }

        DmList possible_tool_instances = surroundings[CONTENTS_POSSIBLE_TOOLS] as DmList;
        foreach (object required_path in Dm.each(recipe.tool_paths))
        {
            if (!Dm.truthy(Dm.locate_type(required_path as Type, possible_tool_instances)))
                return FALSE;
        }

        return recipe.check_tools(source, possible_tool_instances, final_check);
    }

    // tg: code/datums/components/crafting/_crafting.dm:241
    public virtual async DmTask<object> construct_item(atom crafter = null, datum_crafting_recipe recipe = null)
    {
        if (!Dm.truthy(crafter))
            return ", unknown error!"; // This should never happen, but in the event that it does...

        if (!Dm.truthy(recipe))
            return ", invalid recipe!"; // This can happen, I can't really explain why, but it can. Better safe than sorry.

        DmList contents = get_surroundings(crafter, recipe.blacklist);
        object fail_message = perform_all_checks(crafter, recipe, contents, check_tools_last: Dm.bitand(ignored_flags, CRAFT_IGNORE_DO_AFTER));
        if (Dm.truthy(fail_message))
            return fail_message;

        //If we're a mob we'll try a do_after; non mobs will instead instantly construct the item
        if (!Dm.truthy(Dm.bitand(ignored_flags, CRAFT_IGNORE_DO_AFTER)))
        {
            float recipe_time = Dm.num(recipe.time);
            float tools_used = Dm.length(recipe.tool_behaviors) + Dm.length(recipe.tool_paths);

            // If there's any, the speed of the tools used to craft the recipe influence the time spent crafting it
            if (tools_used > 0)
            {
                //get the portion of time that's affected by tool speed at all and subtract it from the full recipe time
                float dynamic_recipe_time = Dm.num(recipe.time) * RECIPE_DYNAMIC_TIME_COEFF;
                recipe_time -= dynamic_recipe_time;
                //Then divide it by the number of tools used in the recipe, and recalculate it.
                dynamic_recipe_time /= tools_used;

                DmList possible_tool_instances = contents[CONTENTS_POSSIBLE_TOOLS] as DmList;
                foreach (object tool in Dm.each(recipe.tool_paths))
                {
                    float best_speed = 10; //failsafe-ish
                    foreach (obj_item item in Dm.each_cast<obj_item>(possible_tool_instances))
                    {
                        if (!istype(item, tool as Type) || best_speed < Dm.num(item.toolspeed))
                            continue;
                        best_speed = Dm.num(item.toolspeed);
                    }
                    recipe_time += dynamic_recipe_time * best_speed;
                }

                DmList found_behaviors = contents[CONTENTS_TOOL_BEHAVIOUR] as DmList;
                foreach (object behavior in Dm.each(recipe.tool_behaviors))
                    recipe_time += dynamic_recipe_time * Dm.num(found_behaviors[behavior]);
            }

            if (!Dm.truthy(await do_after(crafter as atom_movable, round(recipe_time, 0.1f * SECONDS), target: crafter)))
                return ".";
            contents = get_surroundings(crafter, recipe.blacklist);
            fail_message = perform_all_checks(crafter, recipe, contents, check_tools_last: TRUE);
            if (Dm.truthy(fail_message))
                return fail_message;
        }

        //used to gather the material composition of the utilized requirements to transfer to the result
        DmList total_materials = Dm.list();
        DmList stuff_to_use = await get_used_reqs(recipe, crafter, total_materials);
        // GAP(M5 slice 2 -> food, M8): a part made of a material SSmaterials does not create (meat, pizza, hauntium: generated
        // only, DV25) carries a null key in its custom_materials (get_material found no datum); it is left out of the result's.
        while (Dm.@in(null, total_materials))
            Dm.remove(ref total_materials, (object)null);

        foreach ((object mat, object to_remove) in Dm.pairs(recipe.removed_mats))
        {
            object ref_mat = Dm.locate_type(mat as Type, total_materials);
            if (!Dm.truthy(ref_mat))
                continue;
            if (Dm.lt(total_materials[ref_mat], to_remove))
                Dm.remove(ref total_materials, ref_mat);
            else
                Dm.remove_at(total_materials, ref_mat, to_remove);
        }

        atom result;
        turf craft_turf = get_turf(crafter.loc);
        bool set_materials = !Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_NO_MATERIALS));
        if (ispath(recipe.result, typeof(turf)))
            result = await craft_turf.place_on_top(recipe.result) as atom;
        else if (ispath(recipe.result, typeof(obj_item_stack)))
        {
            object or__1 = recipe.result_amount;
            float res_amount = Dm.truthy(or__1) ? Dm.num(or__1) : 1;
            //we don't merge the stack right away but try to put it in the hand of the crafter
            if (set_materials)
            {
                result = Dm.@new(recipe.result, craft_turf, res_amount, /*merge =*/ FALSE, /*mat_override =*/ total_materials, /*mat_amt =*/ 1 / res_amount) as atom;
                set_materials = false; //We've already set the materials on init. Don't do it again
            }
            else
                result = Dm.@new(recipe.result, craft_turf, res_amount, FALSE) as atom;
        }
        else
        {
            result = Dm.@new(recipe.result, craft_turf) as atom;
            if (Dm.truthy(result.atom_storage) && Dm.truthy(recipe.delete_contents))
            {
                foreach (obj_item thing in Dm.each<obj_item>(result))
                    qdel(thing);
            }
        }
        await result.setDir(crafter.dir);
        if (Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_CLEARS_REAGENTS)) && result.reagents is datum_reagents result_reagents)
            await result_reagents.clear_reagents();
        datum_reagents holder = Dm.locate<datum_reagents>(stuff_to_use);
        if (Dm.truthy(holder)) //transfer reagents from ingredients to result
        {
            if (Dm.truthy(result.reagents) && Dm.truthy(Dm.bitand(recipe.crafting_flags, CRAFT_TRANSFERS_REAGENT_COMPONENTS)))
                await holder.trans_to(result.reagents, Dm.num(holder.total_volume), no_react: TRUE);
            Dm.remove(ref stuff_to_use, holder); //This is the only non-movable in our list, we need to remove it.
            qdel(holder);
        }
        // GAP(DV27): a result whose type has its own tg on_craft_completion that is not translated (an inert result, D132: a
        // spear, a flamethrower, a pod door) is not called, rather than running its parent's; its parts are then deleted below
        // like any other component instead of being stored inside it.
        if (!InertSeam.MissingCraftOverride(result, "on_craft_completion"))
            await result.on_craft_completion(stuff_to_use, recipe, crafter);
        // GAP(D112, M5 slice 8): an inert result (a type whose own code is not translated, D132) never ran its Initialize's
        // materials (dm_inert_initialize leaves them out), so it keeps its type's custom_materials as declared instead of the
        // parts' (setting them would remove material effects it never applied).
        if (set_materials && !InertSeam.IsInert(result))
            await result.set_custom_materials(total_materials);
        foreach (atom_movable component in Dm.each_cast<atom_movable>(stuff_to_use)) //delete anything that wasn't stored inside the object
        {
            if (!Dm.eq(component.loc, result) || isturf(result))
                qdel(component);
        }
        if (!Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(crafting) is FALSE in production (rules 1.7)
            SSblackbox.record_feedback("tally", "object_crafted", 1, result.type);
        return result; //Send the item back to whatever called this proc so it can handle whatever it wants to do with the new item
    }

    ///This proc performs all the necessary conditional control statement to ensure that the object is allowed to be crafted by the crafter.
    // tg: code/datums/components/crafting/_crafting.dm:337
    public virtual object perform_all_checks(atom crafter = null, datum_crafting_recipe recipe = null, DmList contents = null, object check_tools_last = null)
    {
        check_tools_last ??= FALSE;
        if (!Dm.truthy(check_contents(crafter, recipe, contents)))
            return ", missing component.";

        turf dest_turf = get_turf(crafter);

        // Mobs call perform_all_checks() twice since they don't have the CRAFT_IGNORE_DO_AFTER flag,
        // one before the do_after() and another after that. While other entities may have that flag and therefore only call the proc once.
        // Check_tools() meanwhile has a final_check arg which, if true, may perform some statements that can
        // modify some of the tools, like expending charges from a crayon or spraycan, which may make it unable
        // to meet some criterias afterward, so it's important to call that, last by the end of the final perform_all_checks().
        // For any non-final perform_all_checks() call, just keep check_tools() here because it's
        // the most imporant feedback after "missing component".
        if (!Dm.truthy(check_tools_last) && !Dm.truthy(check_tools(crafter, recipe, contents, FALSE)))
            return ", missing tool.";

        float considered_flags = Dm.bitand(recipe.crafting_flags, Dm.bitnot(ignored_flags));

        if (Dm.truthy(Dm.bitand(considered_flags, CRAFT_ONE_PER_TURF)) && Dm.truthy(Dm.locate_type(recipe.result, dest_turf)))
            return ", already one here!";

        if (Dm.truthy(Dm.bitand(considered_flags, CRAFT_CHECK_DIRECTION)))
        {
            if (!Dm.truthy(valid_build_direction(dest_turf, crafter.dir, is_fulltile: Dm.bitand(considered_flags, CRAFT_IS_FULLTILE))))
                return ", won't fit here!";
        }

        if (Dm.truthy(Dm.bitand(considered_flags, CRAFT_ON_SOLID_GROUND)))
        {
            if (isclosedturf(dest_turf))
                return ", cannot be made on a wall!";

            if (Dm.truthy(is_type_in_typecache(dest_turf, GLOB.turfs_without_ground)))
            {
                if (!Dm.truthy(Dm.locate<obj_structure_thermoplastic>(dest_turf))) // for tram construction
                    return ", must be made on solid ground!";
            }
        }

        if (Dm.truthy(Dm.bitand(considered_flags, CRAFT_CHECK_DENSITY)))
        {
            foreach (obj @object in Dm.each<obj>(dest_turf))
            {
                if (Dm.truthy(@object.density) && !Dm.truthy(Dm.bitand(@object.obj_flags, IGNORE_DENSITY)) || Dm.truthy(Dm.bitand(@object.obj_flags, BLOCKS_CONSTRUCTION)))
                    return ", something is in the way!";
            }
        }

        if (Dm.truthy(Dm.bitand(recipe.placement_checks, STACK_CHECK_CARDINALS)))
        {
            turf nearby_turf;
            foreach (object direction in Dm.each(GLOB.cardinals))
            {
                nearby_turf = get_step(dest_turf, Dm.num(direction));
                if (Dm.truthy(Dm.locate_type(recipe.result, nearby_turf)))
                {
                    to_chat(crafter, span_warning(I18n.T("\\The [] must not be built directly adjacent to another!", recipe.name)));
                    return ", can't be adjacent to another!";
                }
            }
        }

        if (Dm.truthy(Dm.bitand(recipe.placement_checks, STACK_CHECK_ADJACENT)))
        {
            if (Dm.truthy(Dm.locate_type(recipe.result, range(1, dest_turf))))
                return ", can't be near another!";
        }

        if (Dm.truthy(Dm.bitand(recipe.placement_checks, STACK_CHECK_TRAM_FORBIDDEN)))
        {
            // tg: `locate(/obj/structure/transport/linear/tram) in dest_turf || locate(/obj/structure/thermoplastic) in dest_turf`
            // parses as `locate(tram) in (dest_turf || locate(thermoplastic) in dest_turf)`, which is the tram check alone
            if (Dm.truthy(locate_transport<obj_structure_transport_linear_tram>(dest_turf)))
                return ", can't be on tram!";
        }

        if (Dm.truthy(Dm.bitand(recipe.placement_checks, STACK_CHECK_TRAM_EXCLUSIVE)))
        {
            if (!Dm.truthy(locate_transport<obj_structure_transport_linear_tram>(dest_turf)))
                return ", must be made on a tram!";
        }

        if (Dm.truthy(check_tools_last) && !Dm.truthy(check_tools(crafter, recipe, contents, TRUE)))
            return ", missing tool.";
        return null;
    }

    /**
     * get_used_reqs works like this:
     * Loop over reqs var of the recipe
     * Set var amt to the value current cycle req is pointing to, its amount of type we need to delete
     * Get var/surroundings list of things accessable to crafting by get_environment()
     * Check the type of the current cycle req
     * * If its reagent then do a while loop, inside it try to locate() reagent containers, inside such containers try to locate needed reagent, if there isn't remove thing from surroundings
     * * * Transfer a quantity (The required amount of the contained quantity, whichever is lower) of the reagent to the temporary reagents holder
     *
     * * If it's a stack, create a tally stack and then transfer an amount of the stack to the stack until it reaches the required amount.
     *
     * * If it's anything else just locate() it in the list in a while loop, for each find reduce the amt var by 1 and put the found stuff in return list
     *
     * For stacks and items, the material composition is also tallied in total_materials, to be transferred to the result after that is spawned.
     *
     * get_used_reqs returns the list of used required object the result will receive as argument of atom/CheckParts()
     * If one or some of the object types is in the 'parts' list of the recipe, they will be stored inside the contents of the result
     * The rest will instead be deleted by atom/CheckParts()
    **/
    // tg: code/datums/components/crafting/_crafting.dm:418
    public virtual async DmTask<DmList> get_used_reqs(datum_crafting_recipe recipe = null, atom atom = null, DmList total_materials = null)
    {
        total_materials ??= Dm.list();
        DmList return_list = Dm.list();

        datum_reagents holder = null;
        DmList requirements = Dm.list();
        if (Dm.truthy(recipe.reqs))
            Dm.append(ref requirements, recipe.reqs);
        if (Dm.truthy(recipe.machinery))
            Dm.append(ref requirements, recipe.machinery);
        if (Dm.truthy(recipe.structures))
            Dm.append(ref requirements, recipe.structures);

        DmList surroundings = get_environment(atom, recipe.blacklist);
        foreach (object path_key in Dm.each(requirements))
        {
            object amount_obj = recipe.reqs?[path_key];
            if (!Dm.truthy(amount_obj))
                amount_obj = recipe.machinery?[path_key];
            if (!Dm.truthy(amount_obj))
                amount_obj = recipe.structures?[path_key];
            float amount = Dm.num(amount_obj);
            if (!Dm.truthy(amount))//since machinery & structures can have 0 aka CRAFTING_MACHINERY_USE - i.e. use it, don't consume it!
                continue;
            if (ispath(path_key, typeof(datum_reagent)))
            {
                if (!Dm.truthy(holder))
                {
                    holder = Dm.@new<datum_reagents>(INFINITY, NO_REACT); //an infinite volume holder than can store reagents without reacting
                    Dm.append(ref return_list, holder);
                }
                DmList @checked = Dm.list();
                while (amount > 0)
                {
                    obj_item_reagent_containers container = Dm.locate<obj_item_reagent_containers>(Dm.sub(Dm.bitor(surroundings, return_list), @checked));
                    if (container == null) //This would only happen if the previous checks for contents and tools were flawed.
                    {
                        stack_trace(Dm.text("couldn't fulfill the required amount for []. Dangit", path_key), "code/datums/components/crafting/_crafting.dm", 443);
                        break;
                    }
                    if (QDELING(container)) //it's deleting...
                    {
                        Dm.remove(ref surroundings, container);
                        continue;
                    }
                    float reagent_volume = Dm.num(((datum_reagents)container.reagents).get_reagent_amount(path_key));
                    if (Dm.truthy(reagent_volume))
                    {
                        await ((datum_reagents)container.reagents).trans_to(holder, min(amount, reagent_volume), target_id: path_key, no_react: TRUE);
                        amount -= reagent_volume;
                    }
                    Dm.append(ref @checked, container);
                    container.update_appearance(UPDATE_ICON);
                }
            }
            else if (ispath(path_key, typeof(obj_item_stack)))
            {
                obj_item_stack tally_stack = null;
                while (amount > 0)
                {
                    obj_item_stack origin_stack = Dm.locate_type(path_key as Type, surroundings) as obj_item_stack;
                    if (origin_stack == null) //This would only happen if the previous checks for contents and tools were flawed.
                    {
                        stack_trace(Dm.text("couldn't fulfill the required amount for []. Dangit", path_key), "code/datums/components/crafting/_crafting.dm", 459);
                        break;
                    }
                    if (QDELING(origin_stack))
                        continue;
                    float amount_to_give = min(Dm.num(origin_stack.amount), amount);
                    if (!Dm.truthy(tally_stack))
                    {
                        tally_stack = await origin_stack.split_stack(amount: amount_to_give) as obj_item_stack;
                        Dm.append(ref return_list, tally_stack);
                    }
                    else
                        await origin_stack.merge(tally_stack, amount_to_give);
                    amount -= amount_to_give;
                    Dm.remove(ref surroundings, origin_stack);
                }
                if (!Dm.@in(path_key, recipe.requirements_mats_blacklist))
                {
                    foreach (object material in Dm.each(tally_stack?.custom_materials))
                        Dm.append_at(total_materials, material, tally_stack.custom_materials[material]);
                }
            }
            else
            {
                while (amount > 0)
                {
                    atom_movable item = Dm.locate_type(path_key as Type, surroundings) as atom_movable;
                    if (item == null) //This would only happen if the previous checks for contents and tools were flawed.
                    {
                        stack_trace(Dm.text("couldn't fulfill the required amount for []. Dangit", path_key), "code/datums/components/crafting/_crafting.dm", 478);
                        break;
                    }
                    if (QDELING(item))
                        continue;
                    Dm.append(ref return_list, item);
                    Dm.remove(ref surroundings, item);
                    amount--;
                    if (!Dm.@in(path_key, recipe.requirements_mats_blacklist))
                    {
                        foreach (object material in Dm.each(item.custom_materials))
                            Dm.append_at(total_materials, material, item.custom_materials[material]);
                    }
                }
            }
        }

        return return_list;
    }

    // tg: code/datums/components/crafting/_crafting.dm:491
    [SignalHandler]
    public virtual void component_ui_interact(atom_movable_screen_craft image = null, object location = null, object control = null, object @params = null, object user = null)
    {
        if (Dm.eq(user, parent))
            Waiting.Detach(ui_interact(user as mob));
    }

    // tg: code/datums/components/crafting/_crafting.dm:497
    public override object ui_state(mob user = null)
    {
        return GLOB.not_incapacitated_turf_state;
    }

    //For the UI related things we're going to assume the user is a mob rather than typesetting it to an atom as the UI isn't generated if the parent is an atom
    // tg: code/datums/components/crafting/_crafting.dm:501
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PersonalCrafting", "Crafting");
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/components/crafting/_crafting.dm:507
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["busy"] = busy;
        data["mode"] = mode;
        data["display_craftable_only"] = display_craftable_only;
        data["display_compact"] = display_compact;

        DmList surroundings = get_surroundings(user);
        DmList craftability = Dm.list();
        foreach (datum_crafting_recipe recipe in Dm.each_cast<datum_crafting_recipe>(get_visible_recipes(user)))
        {
            if (Dm.truthy(check_contents(user, recipe, surroundings)) && Dm.truthy(check_tools(user, recipe, surroundings)))
                craftability[Dm.text("[]", REF(recipe))] = TRUE;
        }

        data["craftability"] = craftability;
        return data;
    }

    // tg: code/datums/components/crafting/_crafting.dm:523
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList material_occurences = Dm.list();

        data["forced_mode"] = forced_mode;
        data["recipes"] = Dm.list();
        data["categories"] = Dm.list();
        data["foodtypes"] = FOOD_FLAGS;

        if (Dm.truthy(user.has_dna()))
        {
            mob_living_carbon carbon = user as mob_living_carbon;
            data["diet"] = ((datum_species)carbon.dna.species).get_species_diet();
        }

        foreach (datum_crafting_recipe recipe in Dm.each_cast<datum_crafting_recipe>(get_visible_recipes(user)))
        {
            if (Dm.truthy(recipe.category))
                Dm.combine_at(data, "categories", recipe.category);

            // Materials
            foreach (object req in Dm.each(recipe.reqs))
                Dm.append_at(material_occurences, req, 1);
            foreach (object req in Dm.each(recipe.chem_catalysts))
                Dm.append_at(material_occurences, req, 1);

            Dm.append_at(data, "recipes", Dm.list(build_crafting_data(recipe)));
        }

        DmList datums = Dm.truthy(mode) ? GLOB.cooking_recipes_datums : GLOB.crafting_recipes_datums;

        // Prepare atom data

        //load sprite sheets and select the correct one based on the mode
        // tg: :545-548  var/static/list/sprite_sheets = ui_assets(); var/datum/asset/spritesheet_batched/sheet = sprite_sheets[mode ? 2 : 1]:
        // a seam (DV44): the client draws each atom from its own copy of tg's .dmi files, so the sheet's picture of an atom is the
        // icon and state that add_atom_icon would put in it (crafting_atom_icon, asset_cache/assets/crafting.cs), sent in the
        // atom's atom_data entry; every picture is 32x32 there (icon_size_id), so icon_data stays empty as tg leaves it for them.

        data["icon_data"] = Dm.list();
        foreach (object atom in Dm.each(datums))
        {
            float atom_id = datums.Find(atom);
            object atom_name = "";
            if (ispath(atom, typeof(atom)))
            {
                // future todo: some atoms have intentionally misleading names,
                // and while their crafting recipe reveals their true nature,
                // the displayed name here does not
                atom_name = Dm.initial(atom as Type, (atom a) => a.name);
            }
            else if (ispath(atom, typeof(datum_reagent)))
                atom_name = Dm.initial(atom as Type, (datum_reagent r) => r.name);

            DmList atom_entry = Dm.list(
                Dm.kv("name", atom_name),
                Dm.kv("is_reagent", ispath(atom, typeof(datum_reagent)) ? TRUE : FALSE)
            );
            // the sheet's picture (see above): the icon file and state of tg's add_atom_icon
            (object icon_file, object icon_state) = crafting_atom_icon(atom as Type);
            atom_entry["icon"] = icon_file;
            atom_entry["icon_state"] = icon_state;
            Dm.append_at(data, "atom_data", Dm.list(atom_entry));
        }

        // Prepare materials data
        foreach (object atom in Dm.each(material_occurences))
        {
            if (Dm.eq(material_occurences[atom], 1))
                continue; // Don't include materials that appear only once
            float id = datums.Find(atom);
            Dm.append_at(data, "material_occurences", Dm.list(Dm.list(
                Dm.kv("atom_id", Dm.text("[]", id)),
                Dm.kv("occurences", material_occurences[atom])
            )));
        }

        return data;
    }

    // tg: code/datums/components/crafting/_crafting.dm:593
    public virtual async DmTask<object> make_action(datum_crafting_recipe recipe = null, mob user = null)
    {
        object result = await construct_item(user, recipe);
        if (istext(result)) //We failed to make an item and got a fail message
        {
            to_chat(user, span_warning(I18n.T("Construction failed[]", result)));
            return FALSE;
        }
        if (ismob(user) && isitem(result)) //In case the user is actually possessing a non mob like a machine
            await user.put_in_hands(result as obj_item);
        else if (ismovable(result) && !istype(result, typeof(obj_effect_spawner)))
        {
            atom_movable movable = result as atom_movable;
            await movable.forceMove(user.drop_location() as atom);
        }
        to_chat(user, span_notice(I18n.T("[] crafted.", recipe.name)));
        user.investigate_log(Dm.text("crafted []", recipe), INVESTIGATE_CRAFTING);
        return TRUE;
    }

    /// Returns a list of crafting recipe datums that are available given current crafting state and the user's learned recipes.
    // tg: code/datums/components/crafting/_crafting.dm:608
    public virtual DmList get_visible_recipes(mob user = null)
    {
        DmList recipes_to_show = Dm.list();
        if (Dm.eq(mode, COOKING))
        {
            Dm.append(ref recipes_to_show, GLOB.cooking_recipes_default);
            Dm.append(ref recipes_to_show, SANITIZE_LIST(((datum_mind)user.mind)?.learned_cooking_recipes));
        }
        else if (Dm.eq(mode, CRAFTING))
        {
            Dm.append(ref recipes_to_show, GLOB.crafting_recipes_default);
            Dm.append(ref recipes_to_show, SANITIZE_LIST(((datum_mind)user.mind)?.learned_crafting_recipes));
        }
        return recipes_to_show;
    }

    // tg: code/datums/components/crafting/_crafting.dm:619
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "make":
            case "make_mass":
            {
                mob user = usr;
                // locate(params["recipe"]) in (mode ? GLOB.cooking_recipes : GLOB.crafting_recipes)
                object located = Dm.locate_ref(@params?["recipe"]);
                datum_crafting_recipe crafting_recipe = Dm.@in(located, Dm.truthy(mode) ? GLOB.cooking_recipes : GLOB.crafting_recipes) ? located as datum_crafting_recipe : null;
                busy = TRUE;
                await ui_interact(user);
                if (Dm.eq(action, "make_mass"))
                {
                    float crafted_items = 0;
                    while (Dm.truthy(await make_action(crafting_recipe, user)))
                        crafted_items++;
                    if (Dm.truthy(crafted_items))
                        to_chat(user, span_notice(I18n.T("You made [] item[].", crafted_items, crafted_items == 1 ? "" : "s")));
                }
                else
                    await make_action(crafting_recipe, user);
                busy = FALSE;
                break;
            }
            case "toggle_recipes":
                display_craftable_only = Dm.truthy(display_craftable_only) ? FALSE : TRUE;
                dot = TRUE;
                break;
            case "toggle_compact":
                display_compact = Dm.truthy(display_compact) ? FALSE : TRUE;
                dot = TRUE;
                break;
            case "toggle_mode":
            {
                if (Dm.truthy(forced_mode))
                    return null;
                mode = Dm.truthy(mode) ? FALSE : TRUE;
                mob user = usr;
                await update_static_data(user);
                dot = TRUE;
                break;
            }
        }
        return dot;
    }

    // tg: code/datums/components/crafting/_crafting.dm:652
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/crafting), get_asset_datum(/datum/asset/spritesheet_batched/crafting/cooking)):
        // the asset transport is the client's (DV44); the sheets' pictures are crafting_atom_icon's (ui_static_data).
        return Dm.list();
    }

    // tg: code/datums/components/crafting/_crafting.dm:658
    public virtual DmList build_crafting_data(datum_crafting_recipe recipe = null)
    {
        DmList data = Dm.list();
        DmList datums = Dm.truthy(mode) ? GLOB.cooking_recipes_datums : GLOB.crafting_recipes_datums;

        data["ref"] = Dm.text("[]", REF(recipe));
        Type atom = recipe.result;

        data["id"] = datums.Find(atom);

        DmList recipe_data = recipe.crafting_ui_data() as DmList;
        foreach (object new_data in Dm.each(recipe_data))
            data[new_data] = recipe_data[new_data];

        // Category
        data["category"] = recipe.category;

        // Name, Description
        data["name"] = recipe.name;

        if (ispath(recipe.result, typeof(datum_reagent)))
        {
            Type reagent = recipe.result;
            if (Dm.gt(recipe.result_amount, 1))
                data["name"] = Dm.text("[] []u", data["name"], recipe.result_amount);
            object or__1 = recipe.desc;
            data["desc"] = Dm.truthy(or__1) ? or__1 : Dm.initial(reagent, (datum_reagent r) => r.description);
        }

        else if (ispath(recipe.result, typeof(obj_item_pipe)))
        {
            Type pipe_obj = recipe.result;
            Type pipe_real = Dm.initial(pipe_obj, (obj_item_pipe p) => p.pipe_type) as Type;
            object or__2 = recipe.desc;
            data["desc"] = Dm.truthy(or__2) ? or__2 : (pipe_real != null ? Dm.initial(pipe_real, (obj o) => o.desc) : null);
        }

        else
        {
            if (ispath(recipe.result, typeof(obj_item_stack)) && Dm.gt(recipe.result_amount, 1))
                data["name"] = Dm.text("[] []x", data["name"], recipe.result_amount);
            object or__3 = recipe.desc;
            data["desc"] = Dm.truthy(or__3) ? or__3 : Dm.initial(atom, (atom a) => a.desc);
        }

        if (ispath(recipe.result, typeof(obj_item_food)))
        {
            Type food = recipe.result;
            data["has_food_effect"] = Dm.truthy(Dm.initial(food, (obj_item_food f) => f.crafted_food_buff)) ? TRUE : FALSE;
        }

        // Crafting
        if (Dm.truthy(recipe.non_craftable))
            data["non_craftable"] = recipe.non_craftable;
        data["mass_craftable"] = recipe.mass_craftable;
        if (Dm.truthy(recipe.steps))
            data["steps"] = recipe.steps;

        // Tools
        if (Dm.truthy(recipe.tool_behaviors))
            data["tool_behaviors"] = recipe.tool_behaviors;
        if (Dm.truthy(recipe.tool_paths))
        {
            data["tool_paths"] = Dm.list();
            foreach (object req_atom in Dm.each(recipe.tool_paths))
                Dm.append_at(data, "tool_paths", datums.Find(req_atom));
        }

        // Machinery
        if (Dm.truthy(recipe.machinery))
        {
            data["machinery"] = Dm.list();
            foreach (object req_atom in Dm.each(recipe.machinery))
                Dm.append_at(data, "machinery", datums.Find(req_atom));
        }

        // Structures
        if (Dm.truthy(recipe.structures))
        {
            data["structures"] = Dm.list();
            foreach (object req_atom in Dm.each(recipe.structures))
                Dm.append_at(data, "structures", datums.Find(req_atom));
        }

        // Ingredients / Materials
        data["reqs"] = Dm.list();
        if (Dm.truthy(recipe.reqs.len))
        {
            foreach (object req_atom in Dm.each(recipe.reqs))
            {
                float id = datums.Find(req_atom);
                ((DmList)data["reqs"])[Dm.text("[]", id)] = recipe.reqs[req_atom];
            }
        }

        // Catalysts
        if (Dm.truthy(LAZYLEN(recipe.chem_catalysts)))
        {
            data["chem_catalysts"] = Dm.list();
            foreach ((object req_atom, object chem_amount) in Dm.pairs(recipe.chem_catalysts))
            {
                float id = datums.Find(req_atom);
                ((DmList)data["chem_catalysts"])[Dm.text("[]", id)] = chem_amount;
            }
        }

        // Reaction data
        if (ispath(recipe.reaction))
        {
            data["is_reaction"] = TRUE;
            // May be called before chemical reactions list is setup
            object or__1 = GLOB.chemical_reactions_list[recipe.reaction];
            datum_chemical_reaction reaction = (Dm.truthy(or__1) ? or__1 : Dm.@new(recipe.reaction)) as datum_chemical_reaction;
            if (reaction is datum_chemical_reaction)
            {
                if (!Dm.truthy(data["steps"]))
                    data["steps"] = Dm.list();
                if (reaction.required_container != null)
                {
                    float id = datums.Find(reaction.required_container);
                    ((DmList)data["reqs"])[Dm.text("[]", id)] = 1;
                    Dm.append_at(data, "steps", I18n.T("Add all ingredients into \\a []", Dm.initial(reaction.required_container, (atom a) => a.name)));
                }
                else if (length(recipe.reqs) > 1 || Dm.truthy(length(reaction.required_catalysts)))
                    Dm.append_at(data, "steps", I18n.T("Mix all ingredients together"));
                if (reaction.required_temp > T20C)
                    Dm.append_at(data, "steps", I18n.T("Heat up to []K", reaction.required_temp));
            }
            else
                stack_trace(Dm.text("Invalid reaction found in recipe code! ([])", recipe.reaction), "code/datums/components/crafting/_crafting.dm", 754);
        }
        else if (recipe.reaction != null)
            stack_trace(Dm.text("Invalid reaction found in recipe code! ([])", recipe.reaction), "code/datums/components/crafting/_crafting.dm", 757);

        return data;
    }

    // tg: code/datums/components/crafting/_crafting.dm:3-9 and 813-819: #undef (file-local defines end here)
}

//Mind helpers

public partial class datum_mind : datum
{
    /// proc that teaches user a non-standard crafting recipe
    // tg: code/datums/components/crafting/_crafting.dm:767
    public virtual void teach_crafting_recipe(object recipe = null)
    {
        if (!ispath(recipe, typeof(datum_crafting_recipe)))
        {
            stack_trace("Non-crafting recipe passed to teach_crafting_recipe", "code/datums/components/crafting/_crafting.dm", 769);
            return;
        }

        object learned_cooking = GLOB.cooking_recipes_by_typepath[recipe];
        if (Dm.truthy(learned_cooking))
        {
            LAZYOR(ref learned_cooking_recipes, learned_cooking);
            return;
        }

        object learned_crafting = GLOB.crafting_recipes_by_typepath[recipe];
        if (Dm.truthy(learned_crafting))
        {
            LAZYOR(ref learned_crafting_recipes, learned_crafting);
            return;
        }

        stack_trace(Dm.text("teach_crafting_recipe called with invalid recipe: []", Dm.truthy(recipe) ? recipe : "null"), "code/datums/components/crafting/_crafting.dm", 782);
    }

    /// proc that makes user forget a specific crafting recipe
    // tg: code/datums/components/crafting/_crafting.dm:785
    public virtual void forget_crafting_recipe(object recipe = null)
    {
        LAZYREMOVE(ref learned_cooking_recipes, GLOB.cooking_recipes_by_typepath[recipe]);
        LAZYREMOVE(ref learned_crafting_recipes, GLOB.crafting_recipes_by_typepath[recipe]);
    }

    // tg: code/datums/components/crafting/_crafting.dm:789
    public virtual object has_crafting_recipe(object potential_recipe = null)
    {
        if (!ispath(potential_recipe, typeof(datum_crafting_recipe)))
            throw Dm.CRASH(assertion_message("code/datums/components/crafting/_crafting.dm", 790, "ispath(potential_recipe, /datum/crafting_recipe)", "Non-crafting recipe passed to has_crafting_recipe"), "code/datums/components/crafting/_crafting.dm", 790);
        if (Dm.truthy(Dm.locate_type(potential_recipe as Type, learned_crafting_recipes)))
            return TRUE;
        if (Dm.truthy(Dm.locate_type(potential_recipe as Type, learned_cooking_recipes)))
            return TRUE;
        return FALSE;
    }
}

// tg: code/datums/components/crafting/_crafting.dm:797
public partial class datum_component_personal_crafting_machine : datum_component_personal_crafting
{
    // tg: code/datums/components/crafting/_crafting.dm:800
    public override DmList get_environment(atom crafter = null, DmList blacklist = null, float? radius_range = null)
    {
        radius_range ??= 1;
        DmList dot = Dm.list();
        turf crafter_loc = get_turf(crafter);
        foreach (atom_movable content in Dm.each_cast<atom_movable>(crafter_loc.contents))
        {
            if (Dm.truthy(Dm.bitand(content.flags_1, HOLOGRAM_1)) || (Dm.truthy(blacklist) && Dm.@in(content.type, blacklist)))
                continue;
            if (isitem(content))
            {
                obj_item item = content as obj_item;
                if (Dm.truthy(Dm.bitand(item.item_flags, ABSTRACT))) //let's not tempt fate, shall we?
                    continue;
            }
            Dm.append(ref dot, content);
        }
        return dot;
    }

    // tg: code/datums/components/crafting/_crafting.dm:812
    public override object check_tools(atom source = null, datum_crafting_recipe recipe = null, DmList surroundings = null, float? final_check = null)
    {
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/crafting/_crafting.dm:54  var/static/list/global_blacklist = typecacheof(list(...))
    internal DmList datum_component_personal_crafting__global_blacklist = typecacheof(Dm.list(
        typeof(obj_item_cautery_augment),
        typeof(obj_item_cautery_cruel_augment),
        typeof(obj_item_circular_saw_augment),
        typeof(obj_item_circular_saw_cruel_augment),
        typeof(obj_item_crowbar_cyborg),
        typeof(obj_item_hemostat_augment),
        typeof(obj_item_hemostat_cruel_augment),
        typeof(obj_item_multitool_cyborg),
        typeof(obj_item_retractor_augment),
        typeof(obj_item_retractor_cruel_augment),
        typeof(obj_item_scalpel_augment),
        typeof(obj_item_scalpel_cruel_augment),
        typeof(obj_item_screwdriver_cyborg),
        typeof(obj_item_surgicaldrill_augment),
        typeof(obj_item_surgicaldrill_cruel_augment),
        typeof(obj_item_weldingtool_largetank_cyborg),
        typeof(obj_item_wirecutters_cyborg),
        typeof(obj_item_wrench_cyborg)
    )) as DmList;
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/uses_girder_wall_recipes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 4: the whole file.

/// An element for girders, wall barricades, etc. that makes them use wall construction recipes.
/// Only really meant for recipes where you click on the girder with a stack of materials to make a wall.
// tg: code/datums/elements/uses_girder_wall_recipes.dm:3
public partial class datum_element_uses_girder_wall_recipes : datum_element
{
    // tg: code/datums/elements/uses_girder_wall_recipes.dm:5
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isstructure(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignals(target, Dm.list(COMSIG_ATOM_ITEM_INTERACTION, COMSIG_ATOM_ITEM_INTERACTION_SECONDARY), nameof(on_item_interaction));
        return dot;
    }

    // tg: code/datums/elements/uses_girder_wall_recipes.dm:11
    [DmParams("target")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_ITEM_INTERACTION, COMSIG_ATOM_ITEM_INTERACTION_SECONDARY));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/uses_girder_wall_recipes.dm:15
    [SignalHandler]
    public virtual object on_item_interaction(obj_structure structure = null, mob_living user = null, obj_item_stack stack = null, DmList modifiers = null)
    {
        if (!isstack(stack))
            return NONE;
        if (!Dm.truthy(stack.usable_for_construction))
        {
            structure.balloon_alert(user, I18n.T("unusable material!"));
            return ITEM_INTERACT_BLOCKING;
        }

        datum_girder_wall_recipe main_recipe = get_main_recipe(structure, stack) as datum_girder_wall_recipe;

        if (Dm.truthy(main_recipe))
        {
            Waiting.Detach(attempt_recipe(structure, user, stack, main_recipe, is_material_recipe: FALSE));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(stack.has_unique_girder))
        {
            structure.balloon_alert(user, I18n.T("needs a different girder!"));
            return ITEM_INTERACT_BLOCKING;
        }

        // Plasteel is used for reinforcing girders.
        if (istype(structure, typeof(obj_structure_girder)) && istype(stack, typeof(obj_item_stack_sheet_plasteel)))
            return NONE;

        datum_girder_wall_recipe material_recipe = get_material_recipe(structure, stack) as datum_girder_wall_recipe;

        if (Dm.truthy(material_recipe))
        {
            Waiting.Detach(attempt_recipe(structure, user, stack, material_recipe, is_material_recipe: TRUE));
            return ITEM_INTERACT_BLOCKING;
        }
        return null;
    }

    /// Returns the main wall recipe of the stack for the structure, if any.
    // tg: code/datums/elements/uses_girder_wall_recipes.dm:44
    public virtual object get_main_recipe(obj_structure structure = null, obj_item_stack stack = null)
    {
        foreach (datum_girder_wall_recipe recipe in Dm.each<datum_girder_wall_recipe>(GLOB.main_girder_wall_recipes))
        {
            if (!istype(stack, recipe.stack_type))
                continue;
            if (!istype(structure, recipe.girder_type))
                continue;
            if (!Dm.truthy(check_girder_state(structure, recipe)))
                continue;
            return recipe;
        }
        return null;
    }

    /// Returns the material wall recipe of the stack for the structure, if any.
    // tg: code/datums/elements/uses_girder_wall_recipes.dm:55
    public virtual object get_material_recipe(obj_structure structure = null, obj_item_stack stack = null)
    {
        foreach (datum_girder_wall_recipe recipe in Dm.each<datum_girder_wall_recipe>(GLOB.material_girder_wall_recipes))
        {
            if (!istype(structure, recipe.girder_type))
                continue;
            if (!Dm.truthy(check_girder_state(structure, recipe)))
                continue;
            return recipe;
        }
        return null;
    }

    /// Has the user attempt the wall recipe asynchronously.
    /// Assumes that the structure and stack are of valid types for the recipe.
    // tg: code/datums/elements/uses_girder_wall_recipes.dm:65
    public virtual void attempt_recipe_async(obj_structure structure = null, mob_living user = null, obj_item_stack stack = null, datum_girder_wall_recipe recipe = null, object is_material_recipe = null)
    {
        Waiting.Detach(attempt_recipe(structure, user, stack, recipe, is_material_recipe));
    }

    /// Has the user attempt the wall recipe.
    /// Assumes that the structure and stack are of valid types for the recipe.
    // tg: code/datums/elements/uses_girder_wall_recipes.dm:70
    public virtual async DmTask attempt_recipe(obj_structure structure = null, mob_living user = null, obj_item_stack stack = null, datum_girder_wall_recipe recipe = null, object is_material_recipe = null)
    {
        if (!Dm.truthy(check_recipe(structure, user, recipe)))
            return;
        if (!Dm.truthy(stack.tool_start_check(user, Dm.num(recipe.stack_amount))))
            return;

        user.visible_message(
            message: span_notice(I18n.T("\\The [] start[] building a wall on \\the [].", user, user.p_s(), structure)),
            self_message: span_notice(I18n.T("You start building a wall on \\the [].", structure)),
            blind_message: span_hear(I18n.T("You hear a series of clangs."))
        );

        // tg: code/datums/elements/uses_girder_wall_recipes.dm:84
        structure.add_fingerprint(user);
        stack.add_fingerprint(user);

        // Save refernce to the materials for the case when we place last tile in the stack
        DmList saved_mats_per_unit = stack.mats_per_unit;
        if (!Dm.truthy(await stack.use_tool(structure, user, recipe.make_delay, Dm.num(recipe.stack_amount), extra_checks: Dm.@new<datum_callback>(this, nameof(check_recipe), structure, user, recipe))))
            return;

        atom wall;
        if (ispath(recipe.wall_type, typeof(turf)))
        {
            turf structure_turf = get_turf(structure);
            wall = await structure_turf.place_on_top(recipe.wall_type) as atom;
        }
        else if (ispath(recipe.wall_type, typeof(obj)))
            wall = Dm.@new(recipe.wall_type, structure.drop_location()) as atom;
        else
            throw Dm.CRASH(Dm.text("Attempted a girder wall recipe with an invalid wall type ([])", recipe.wall_type), "code/datums/elements/uses_girder_wall_recipes.dm", 101);

        if (istype(wall, typeof(turf_closed_wall)))
        {
            turf_closed_wall griderholder = wall as turf_closed_wall;
            griderholder.girder_type = structure.type;
        }
        if (istype(wall, typeof(obj_structure_falsewall)))
        {
            obj_structure_falsewall griderholder = wall as obj_structure_falsewall;
            griderholder.girder_type = structure.type;
        }

        user.visible_message(
            message: span_notice(I18n.T("\\The [] finish[] building \\a [] on \\the [].", user, user.p_es(), wall, structure)),
            self_message: span_notice(I18n.T("You finish building \\a [] on \\the [].", wall, structure))
        );

        // tg: code/datums/elements/uses_girder_wall_recipes.dm:112
        structure.transfer_fingerprints_to(wall);
        qdel(structure);

        if (Dm.truthy(is_material_recipe))
            await wall.set_custom_materials(saved_mats_per_unit, Dm.num(recipe.stack_amount));
    }

    /// Checks if the user can do the wall recipe.
    // tg: code/datums/elements/uses_girder_wall_recipes.dm:118
    public virtual object check_recipe(obj_structure structure = null, mob_living user = null, datum_girder_wall_recipe recipe = null)
    {
        if (iswallturf(structure.loc) || Dm.truthy(Dm.locate<obj_structure_falsewall>(((atom)structure.loc).contents)))
        {
            structure.balloon_alert(user, I18n.T("wall already present!"));
            return FALSE;
        }
        if (!ispath(recipe.wall_type, typeof(obj_structure_tram)))
        {
            if (!isfloorturf(structure.loc))
            {
                structure.balloon_alert(user, I18n.T("need floor!"));
                return FALSE;
            }
        }
        else if (!Dm.truthy(locate_transport<obj_structure_transport_linear_tram>(structure.loc)))
        {
            structure.balloon_alert(user, I18n.T("need tram floor!"));
            return FALSE;
        }
        if (!Dm.truthy(check_girder_state(structure, recipe)))
            return FALSE;
        return TRUE;
    }

    /// Checks if the girder state of the structure matches the required girder state of the wall recipe.
    // tg: code/datums/elements/uses_girder_wall_recipes.dm:134
    public virtual object check_girder_state(obj_structure structure = null, datum_girder_wall_recipe recipe = null)
    {
        if (istype(structure, typeof(obj_structure_girder)))
        {
            obj_structure_girder girder = structure as obj_structure_girder;
            if (!Dm.eq(girder.state, recipe.girder_state))
                return FALSE;
        }
        return TRUE;
    }
}

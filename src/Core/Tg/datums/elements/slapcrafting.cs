// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/slapcrafting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 8 (crafting): the whole file. The recipe's link of the examine (COMSIG_TOPIC, :160) is translated; a chat link's
// click reaches it since D154 (ClientSeam.Topic), but the link is examine_more's (:133), which the minimal examine does not run
// yet (M9). The var block is generated (D111).

/// Slapcrafting component!
// tg: code/datums/elements/slapcrafting.dm:2
public partial class datum_element_slapcrafting : datum_element
{
    /**
     * Slapcraft element
     *
     * Slap it onto a item to be able to slapcraft with it
     *
     * args:
     * * slapcraft_recipes (required) = The recipe to attempt crafting.
     * Hit it with an ingredient of the recipe to attempt crafting.
     * It will check the area near the user for the rest of the ingredients and tools.
     * *
    **/
    // tg: code/datums/elements/slapcrafting.dm:18
    [DmParams("target", "slapcraft_recipes")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        DmList slapcraft_recipes = Dm.arg<DmList>(__extra, 0);
        await base.Attach(target, Dm.forward(__extra, slapcraft_recipes));
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        obj_item target_item = target as obj_item;

        if (Dm.truthy(Dm.bitand(target_item.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(target_item.item_flags, DROPDEL)))
            return null; //Don't do anything, it just shouldn't be used in crafting.

        RegisterSignal(target, COMSIG_ATOM_ATTACKBY, nameof(attempt_slapcraft));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE_TAGS, nameof(get_examine_info));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE_MORE, nameof(get_examine_more_info));
        RegisterSignal(target, COMSIG_TOPIC, nameof(topic_handler));

        this.slapcraft_recipes = slapcraft_recipes;
        return null;
    }

    // tg: code/datums/elements/slapcrafting.dm:35
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_EXAMINE, COMSIG_ATOM_EXAMINE_MORE));
        return dot;
    }

    // tg: code/datums/elements/slapcrafting.dm:39
    public virtual object attempt_slapcraft(obj_item parent_item = null, obj_item slapper = null, mob user = null)
    {
        if (slapcraft_recipes == null)
            throw Dm.CRASH("NULL SLAPCRAFT RECIPES?", "code/datums/elements/slapcrafting.dm", 42);

        //mobs that can't craft (ex: borgs) can't slapcraft.
        datum_component_personal_crafting craft_sheet = user.GetComponent(typeof(datum_component_personal_crafting)) as datum_component_personal_crafting;
        if (!Dm.truthy(craft_sheet))
            return null;

        DmList valid_recipes = null;
        foreach (Type recipe_path in Dm.each<Type>(slapcraft_recipes))
        {
            // Gotta instance it to copy the list over.
            datum_crafting_recipe recipe = Dm.@new(recipe_path) as datum_crafting_recipe;
            DmList type_ingredient_list = recipe.reqs;
            qdel(recipe);
            if (Dm.length(type_ingredient_list) == 1) // No ingredients besides itself? We use one of the tools then
            {
                type_ingredient_list = recipe.tool_paths;
                // Check the tool behaviours differently as they aren't types
                // initial(recipe.tool_behaviors): the recipe's own list (New only interns it, string_list)
                foreach (object behaviour in Dm.each(recipe.tool_behaviors))
                {
                    if (Dm.eq(slapper.tool_behaviour, behaviour))
                    {
                        LAZYADD(ref valid_recipes, recipe);
                        break;
                    }
                }
            }
            if (Dm.truthy(is_type_in_list(slapper, type_ingredient_list)))
                LAZYADD(ref valid_recipes, recipe);
        }

        if (!Dm.truthy(valid_recipes))
            return null;

        // We might use radials so we need to split the proc chain
        Waiting.Detach(slapcraft_async(parent_item, valid_recipes, user, craft_sheet));
        return null;
    }

    // tg: code/datums/elements/slapcrafting.dm:71
    public virtual async DmTask slapcraft_async(obj parent_item = null, DmList valid_recipes = null, mob user = null, datum_component_personal_crafting craft_sheet = null)
    {
        DmList recipe_choices = Dm.list();

        DmList result_to_recipe = Dm.list();

        object final_recipe = valid_recipes[1];
        object string_chosen_recipe = null;
        if (Dm.length(valid_recipes) > 1)
        {
            foreach (datum_crafting_recipe recipe in Dm.each_cast<datum_crafting_recipe>(valid_recipes))
            {
                // initial(recipe.result): the recipe's result type
                Type recipe_result = recipe.result;
                object result_name = Dm.initial(recipe_result, (atom a) => a.name);
                result_to_recipe[result_name] = recipe;
                recipe_choices[Dm.text("[]", result_name)] = Dm.image(icon: Dm.initial(recipe_result, (atom a) => a.icon), icon_state: Dm.initial(recipe_result, (atom a) => a.icon_state));
            }

            if (!Dm.truthy(recipe_choices))
                throw Dm.CRASH("No recipe choices despite validating in earlier proc", "code/datums/elements/slapcrafting.dm", 86);

            string_chosen_recipe = await show_radial_menu(user, parent_item, recipe_choices, require_near: TRUE);
            if (string_chosen_recipe == null)
                return; // they closed the thing
        }

        if (Dm.truthy(string_chosen_recipe))
            final_recipe = result_to_recipe[string_chosen_recipe];

        if (ispath(final_recipe))
        {
            object recipe_path = final_recipe;
            object or__1 = GLOB.cooking_recipes_by_typepath[recipe_path];
            final_recipe = Dm.truthy(or__1) ? or__1 : GLOB.crafting_recipes_by_typepath[recipe_path];
            if (!Dm.truthy(final_recipe))
                throw Dm.CRASH(Dm.text("Recipe not located in cooking or crafting recipes: []", recipe_path), "code/datums/elements/slapcrafting.dm", 99);
        }

        datum_crafting_recipe chosen = final_recipe as datum_crafting_recipe;
        Type final_result = chosen.result;

        to_chat(user, span_notice(I18n.T("You start crafting \\a []...", Dm.initial(final_result, (atom a) => a.name))));

        object error_string = await craft_sheet.construct_item(user, chosen);

        if (istext(error_string))
            to_chat(user, span_warning(I18n.T("Crafting failed[]", error_string)));
    }

    /// Alerts any examiners to the recipe, if they wish to know more.
    // tg: code/datums/elements/slapcrafting.dm:111
    [SignalHandler]
    public virtual void get_examine_info(atom source = null, mob user = null, DmList examine_list = null)
    {
        DmList string_results = Dm.list();
        // This list saves the recipe result names we've already used to cross-check other recipes so we don't have ', a spear, or a spear!' in the desc.
        DmList already_used_names = Dm.list(); // tg's null list var takes the first name as text; a list keeps its intent
        foreach (Type recipe in Dm.each<Type>(slapcraft_recipes))
        {
            // Identical name to a previous recipe's result? Skip in description.
            Type result = Dm.initial(recipe, (datum_crafting_recipe r) => r.result) as Type;
            object result_name = Dm.initial(result, (atom a) => a.name);
            if (Dm.@in(result_name, already_used_names))
                continue;
            Dm.append(ref already_used_names, result_name);
            Dm.append(ref string_results, Dm.list(I18n.T("\\a []", result_name)));
        }

        examine_list["crafting component"] = I18n.T("You think [] could be used to make []! Examine again to look at the details...", source, english_list(string_results));
    }

    /// Alerts any examiners to the details of the recipe.
    // tg: code/datums/elements/slapcrafting.dm:128
    [SignalHandler]
    public virtual void get_examine_more_info(atom source = null, mob user = null, DmList examine_list = null)
    {
        foreach (Type recipe in Dm.each<Type>(slapcraft_recipes))
        {
            Type result = Dm.initial(recipe, (datum_crafting_recipe r) => r.result) as Type;
            Dm.append(ref examine_list, Dm.text("<a href='byond://?src=[];check_recipe=[]'>See Recipe For []</a>", REF(source), REF(recipe), Dm.initial(result, (atom a) => a.name)));
        }
    }

    // tg: code/datums/elements/slapcrafting.dm:135
    [SignalHandler]
    public virtual void topic_handler(atom source = null, object user = null, DmList href_list = null)
    {
        if (!Dm.truthy(href_list?["check_recipe"]))
            return;

        // locate(href_list["check_recipe"]) in slapcraft_recipes
        object located = Dm.locate_ref(href_list["check_recipe"]);
        Type cur_recipe_path = Dm.@in(located, slapcraft_recipes) ? located as Type : null;

        if (cur_recipe_path == null)
            throw Dm.CRASH("null recipe!", "code/datums/elements/slapcrafting.dm", 144);

        Type result = Dm.initial(cur_recipe_path, (datum_crafting_recipe r) => r.result) as Type;

        to_chat(user, span_notice(I18n.T("You could craft \\a [] by applying one of these items to it!", Dm.initial(result, (atom a) => a.name))));

        // Gotta instance it to copy the lists over.
        datum_crafting_recipe cur_recipe = Dm.@new(cur_recipe_path) as datum_crafting_recipe;
        DmList type_ingredient_list = cur_recipe.reqs;

        // Final return string.
        string string_ingredient_list = "";

        // Check the ingredients of the crafting recipe.
        foreach (object valid_type in Dm.each(type_ingredient_list))
        {
            // Check if they're datums, specifically reagents.
            // istype(reagent_ingredient) of a typepath is never true: the reagent line of tg never shows
            if (istype(valid_type, typeof(datum_reagent)))
            {
                object reagent_amount = cur_recipe.reqs[valid_type];
                string_ingredient_list += Dm.text("[] unit[] of []\n", reagent_amount, Dm.gt(reagent_amount, 1) ? "s" : "", Dm.get_var(valid_type, "name"));
            }

            Type ingredient = valid_type as Type;
            float amount = Dm.num(cur_recipe.reqs[ingredient]);

            // If we're about to describe the ingredient that the component is based on, lower the described amount by 1 or remove it outright.
            if (Dm.eq(source.type, valid_type))
            {
                if (amount > 1)
                    amount--;
                else
                    continue;
            }
            string_ingredient_list += Dm.text("[] []\n", amount > 1 ? (Dm.text("[]", amount) + " of") : "a", Dm.initial(ingredient, (atom a) => a.name));
        }

        // If we did find ingredients then add them onto the list.
        if (Dm.truthy(Dm.length(string_ingredient_list)))
        {
            to_chat(user, span_boldnotice(I18n.T("Extra Ingredients:")));
            to_chat(user, boxed_message(span_notice(string_ingredient_list)));
        }

        string tool_list = "";

        // Paste the required tools.
        foreach (object valid_type in Dm.each(cur_recipe.tool_paths))
        {
            Type tool = valid_type as Type;
            tool_list += Dm.text("\\a []\n", Dm.initial(tool, (atom a) => a.name));
        }

        foreach (object @string in Dm.each(cur_recipe.tool_behaviors))
            tool_list += Dm.text("\\a []\n", @string);

        if (Dm.truthy(Dm.length(tool_list)))
        {
            to_chat(user, span_boldnotice(I18n.T("Required Tools:")));
            to_chat(user, boxed_message(span_notice(tool_list)));
        }

        qdel(cur_recipe);
    }

    // tg: code/__DEFINES/chat.dm:48  #define boxed_message(str) ("<div class='boxed_message'>" + str + "</div>")
    private static string boxed_message(string str) => "<div class='boxed_message'>" + str + "</div>";
}

// The static recipe lists of the slapcrafting hosts (rules 10.6: type-level statics live in TgState; one list per type, so
// that the bespoke element is shared by every instance as in tg).
public sealed partial class TgState
{
    // tg: code/game/objects/items/stacks/rods.dm:66
    internal DmList obj_item_stack_rods__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_spear), typeof(datum_crafting_recipe_stunprod), typeof(datum_crafting_recipe_teleprod), typeof(datum_crafting_recipe_wireprod));

    // tg: code/game/machinery/pipe/construction.dm:174
    internal DmList obj_item_pipe__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_ghettojetpack), typeof(datum_crafting_recipe_pipegun), typeof(datum_crafting_recipe_smoothbore_disabler), typeof(datum_crafting_recipe_improvised_pneumatic_cannon));

    // tg: code/modules/power/cell.dm:27
    internal DmList obj_item_stock_parts_power_store_cell__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_battery_match));

    // tg: code/game/objects/items/tools/extinguisher.dm:59
    internal DmList obj_item_extinguisher__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_ghettojetpack));

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:41
    internal DmList obj_item_analyzer__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_material_sniffer));

    // tg: code/modules/clothing/gloves/boxing.dm:17
    internal DmList obj_item_clothing_gloves_boxing__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_extendohand_l), typeof(datum_crafting_recipe_extendohand_r));
}

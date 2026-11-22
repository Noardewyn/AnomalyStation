// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/condiment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P5 (the CondiMaster's prints): the condiment bottles, the salt shaker and the condiment packs. The suicide acts are
// live-partial rows (the suicide verb, M10+). Gaps H2 (D333): the enzyme's, sugar's, milk's and flour's examines, which read the food
// reactions' numbers. The var blocks are generated.

///////////////////////////////////////////////Condiments
//Notes by Darem: The condiments food-subtype is for stuff you don't actually eat but you use to modify existing food. They all
// leave empty containers when used up and can be filled/re-filled with other items. Formatting for first section is identical
// to mixed-drinks code. If you want an object that starts pre-loaded, you need to make it in addition to the other code.

//Food items that aren't eaten normally and leave an empty container behind.
// tg: code/modules/reagents/reagent_containers/condiment.dm:8
public partial class obj_item_reagent_containers_condiment : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/condiment.dm:26
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy((reagents as datum_reagents).reagent_list.len))
        {
            if (Dm.truthy(icon_preempty))
            {
                icon_state = icon_preempty as string;
                icon_preempty = null;
            }
            return await base.update_icon_state(__extra);
        }

        if (Dm.truthy(icon_empty) && !Dm.truthy(icon_preempty))
        {
            icon_preempty = icon_state;
            icon_state = icon_empty as string;
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/reagent_containers/condiment.dm:43
    public virtual async DmTask<object> try_eat(atom target = null, mob_living user = null)
    {
        if (!Dm.truthy(canconsume(target as mob, user)))
            return ITEM_INTERACT_BLOCKING;

        user.changeNext_move(CLICK_CD_MELEE);
        if (target == user)
        {
            user.visible_message(
                span_notice(I18n.T("[] swallows some of the contents of \\the [].", user, this)),
                span_notice(I18n.T("You swallow some of the contents of \\the [].", this)));
        }
        else
        {
            target.visible_message(
                span_warning(I18n.T("[] attempts to feed [] from [].", user, target, this)),
                span_warning(I18n.T("[] attempts to feed you from [].", user, this)));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target)))
                return ITEM_INTERACT_BLOCKING;
            if (reagents == null || !Dm.truthy((reagents as datum_reagents).total_volume))
                return ITEM_INTERACT_BLOCKING; // The condiment might be empty after the delay.
            target.visible_message(
                span_warning(I18n.T("[] fed [] from [].", user, target, this)),
                span_warning(I18n.T("[] fed you from [].", user, this)));
            log_combat(user, target, "fed", (reagents as datum_reagents).get_reagent_log_string());
        }
        await (reagents as datum_reagents).trans_to(target, 10, transferred_by: user, methods: INGEST);
        playsound(target, Dm.resource("sound/items/drink.ogg"), rand(10, 50), TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/condiment.dm:71
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        if (!Dm.truthy(is_open_container()))
            return NONE;

        //Something like a glass or a food item. Player probably wants to transfer TO it.
        if (Dm.truthy(target.is_refillable()) || IS_EDIBLE(target))
            return await try_refill(target, user);
        //A dispenser. Transfer FROM it TO us.
        if (Dm.truthy(target.is_drainable()))
            return await try_drain(target, user);
        //Eating directly from the ketchup packet
        if (isliving(target))
            return await try_eat(target, user);

        return NONE;
    }

    // tg: code/modules/reagents/reagent_containers/condiment.dm:88
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        object dot = await base.interact_with_atom_secondary(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!Dm.truthy(is_open_container()))
            return NONE;
        //A dispenser. Transfer FROM it TO us.
        if (Dm.truthy(target.is_drainable()))
            return await try_drain(target, user);

        return NONE;
    }
}

// tg: code/modules/reagents/reagent_containers/condiment.dm:136
public partial class obj_item_reagent_containers_condiment_saltshaker : obj_item_reagent_containers_condiment
{
    // tg: code/modules/reagents/reagent_containers/condiment.dm:157
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (isturf(target))
        {
            datum_reagents holder = reagents as datum_reagents;
            if (!Dm.truthy(holder.has_reagent(typeof(datum_reagent_consumable_salt), 2)))
            {
                to_chat(user, span_warning(I18n.T("You don't have enough salt to make a pile!")));
                return null;
            }
            user.visible_message(span_notice(I18n.T("[] shakes some salt onto [].", user, target)), span_notice(I18n.T("You shake some salt onto [].", target)));
            await holder.remove_reagent(typeof(datum_reagent_consumable_salt), 2);
            Dm.@new<obj_effect_decal_cleanable_food_salt>(target);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/condiment.dm:432
public partial class obj_item_reagent_containers_condiment_pack : obj_item_reagent_containers_condiment
{
    // tg: code/modules/reagents/reagent_containers/condiment.dm:462
    public override void create_reagents(object max_vol = null, object flags = null)
    {
        base.create_reagents(max_vol, flags);
        RegisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagent_update), TRUE);
    }

    // tg: code/modules/reagents/reagent_containers/condiment.dm:466
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        //SHOULD_CALL_PARENT(FALSE)
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/condiment.dm:470
    public override async DmTask<object> try_eat(atom target = null, mob_living user = null)
    {
        await DmTask.CompletedTask;
        return NONE;
    }

    // tg: code/modules/reagents/reagent_containers/condiment.dm:473
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        //You can tear the bag open above food to put the condiments on it, obviously.
        if (IS_EDIBLE(target))
        {
            datum_reagents holder = reagents as datum_reagents;
            if (!Dm.truthy(holder.total_volume))
            {
                to_chat(user, span_warning(I18n.T("You tear open [], but there's nothing in it.", this)));
                qdel(this);
                return ITEM_INTERACT_BLOCKING;
            }
            datum_reagents target_reagents = target.reagents as datum_reagents;
            if (target_reagents.total_volume >= target_reagents.maximum_volume)
            {
                to_chat(user, span_warning(I18n.T("You tear open [], but [] is stacked so high that it just drips off!", this, target)));
                qdel(this);
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_notice(I18n.T("You tear open [] above [] and the condiments drip onto it.", this, target)));
            await holder.trans_to(target, Dm.num(amount_per_transfer_from_this), transferred_by: user);
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }

    /// Handles reagents getting added to the condiment pack.
    // tg: code/modules/reagents/reagent_containers/condiment.dm:491
    public virtual void on_reagent_update(datum_reagents reagents = null)
    {
        //SIGNAL_HANDLER
        if (!Dm.truthy(reagents.total_volume))
        {
            icon_state = "condi_empty";
            desc = "A small condiment pack. It is empty.";
            return;
        }
        datum_reagent main_reagent = reagents.get_master_reagent() as datum_reagent;

        DmList temp_list = possible_states[main_reagent.type] as DmList;
        if (Dm.truthy(length(temp_list)))
        {
            icon_state = temp_list[1] as string;
            desc = temp_list[3] as string;
        }
        else
        {
            icon_state = "condi_mixed";
            desc = Dm.text("A small condiment pack. The label says it contains [].", originalname);
        }
    }
}

// Gaps H2 (D333): the enzyme's, sugar's, milk's and flour's examines, which read the food reactions' numbers (live with the kitchen).

// tg: code/modules/reagents/reagent_containers/condiment.dm:100
public partial class obj_item_reagent_containers_condiment_enzyme : obj_item_reagent_containers_condiment
{
    // tg: code/modules/reagents/reagent_containers/condiment.dm:107
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        datum_chemical_reaction recipe = GLOB.chemical_reactions_list[typeof(datum_chemical_reaction_food_cheesewheel)] as datum_chemical_reaction;
        object milk_required = recipe.required_reagents[typeof(datum_reagent_consumable_milk)];
        object enzyme_required = recipe.required_catalysts[typeof(datum_reagent_consumable_enzyme)];
        Dm.append(ref dot, span_notice(I18n.T("[] milk, [] enzyme and you got cheese.", milk_required, enzyme_required)));
        Dm.append(ref dot, span_warning(I18n.T("Remember, the enzyme isn't used up, so return it to the bottle, dingus!")));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/condiment.dm:115
public partial class obj_item_reagent_containers_condiment_sugar : obj_item_reagent_containers_condiment
{
    // tg: code/modules/reagents/reagent_containers/condiment.dm:125
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        datum_chemical_reaction standard_recipe = GLOB.chemical_reactions_list[typeof(datum_chemical_reaction_food_cakebatter)] as datum_chemical_reaction;
        datum_chemical_reaction alt_recipe = GLOB.chemical_reactions_list[typeof(datum_chemical_reaction_food_cakebatter_vegan)] as datum_chemical_reaction;
        object flour_required = standard_recipe.required_reagents[typeof(datum_reagent_consumable_flour)];
        object eggyolk_required = standard_recipe.required_reagents[typeof(datum_reagent_consumable_eggyolk)];
        object eggwhite_required = standard_recipe.required_reagents[typeof(datum_reagent_consumable_eggwhite)];
        object sugar_required = standard_recipe.required_reagents[typeof(datum_reagent_consumable_sugar)];
        object soymilk_required = alt_recipe.required_reagents[typeof(datum_reagent_consumable_soymilk)];
        Dm.append(ref dot, span_notice(I18n.T("[] flour, [] sugar, and either [] egg yolk + [] egg white or [] soy milk yields a cake dough. You can make pie dough from it.", flour_required, sugar_required, eggyolk_required, eggwhite_required, soymilk_required)));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/condiment.dm:183
public partial class obj_item_reagent_containers_condiment_milk : obj_item_reagent_containers_condiment
{
    // tg: code/modules/reagents/reagent_containers/condiment.dm:193
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        datum_chemical_reaction recipe = GLOB.chemical_reactions_list[typeof(datum_chemical_reaction_food_cheesewheel)] as datum_chemical_reaction;
        object milk_required = recipe.required_reagents[typeof(datum_reagent_consumable_milk)];
        object enzyme_required = recipe.required_catalysts[typeof(datum_reagent_consumable_enzyme)];
        Dm.append(ref dot, span_notice(I18n.T("[] milk, [] enzyme and you got cheese.", milk_required, enzyme_required)));
        Dm.append(ref dot, span_warning(I18n.T("Remember, the enzyme isn't used up, so return it to the bottle, dingus!")));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/condiment.dm:201
public partial class obj_item_reagent_containers_condiment_flour : obj_item_reagent_containers_condiment
{
    // tg: code/modules/reagents/reagent_containers/condiment.dm:211
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        datum_chemical_reaction recipe_dough = GLOB.chemical_reactions_list[typeof(datum_chemical_reaction_food_dough)] as datum_chemical_reaction;
        datum_chemical_reaction recipe_cakebatter = GLOB.chemical_reactions_list[typeof(datum_chemical_reaction_food_cakebatter)] as datum_chemical_reaction;
        object dough_flour_required = recipe_dough.required_reagents[typeof(datum_reagent_consumable_flour)];
        object dough_water_required = recipe_dough.required_reagents[typeof(datum_reagent_water)];
        object cakebatter_flour_required = recipe_cakebatter.required_reagents[typeof(datum_reagent_consumable_flour)];
        object cakebatter_eggyolk_required = recipe_cakebatter.required_reagents[typeof(datum_reagent_consumable_eggyolk)];
        object cakebatter_sugar_required = recipe_cakebatter.required_reagents[typeof(datum_reagent_consumable_sugar)];
        Dm.append(ref dot, I18n.T("<b><i>You retreat inward and recall the teachings of... Making Dough...</i></b>"));
        Dm.append(ref dot, span_notice(I18n.T("[] flour, [] water makes normal dough. You can make flat dough from it.", dough_flour_required, dough_water_required)));
        Dm.append(ref dot, span_notice(I18n.T("[] flour, [] egg yolk (or soy milk), [] sugar makes cake dough. You can make pie dough from it.", cakebatter_flour_required, cakebatter_eggyolk_required, cakebatter_sugar_required)));
        return dot;
    }
}

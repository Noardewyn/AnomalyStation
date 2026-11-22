// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/food_storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): slipping an item into bread, cheese, cakes and cutlets. The var block is generated (D111).

/// --Food storage component--
/// This component lets you slide one item into large foods, such as bread, cheese wheels, or cakes.
/// Consuming food storages with an item inside can cause unique interactions, such as eating glass shards.

// tg: code/datums/components/food_storage.dm:5
public partial class datum_component_food_storage : datum_component
{
    // tg: code/datums/components/food_storage.dm:17
    [DmParams("_minimum_weight_class", "_bad_chance", "_good_chance")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object _minimum_weight_class = Dm.arg(__extra, 0) ?? WEIGHT_CLASS_SMALL;
        object _bad_chance = Dm.arg(__extra, 1) ?? 0;
        object _good_chance = Dm.arg(__extra, 2) ?? 100;

        RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION_SECONDARY, nameof(try_inserting_item));
        RegisterSignal(parent, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        RegisterSignal(parent, COMSIG_CLICK_CTRL, nameof(try_removing_item));
        RegisterSignal(parent, COMSIG_FOOD_EATEN, nameof(consume_food_storage));
        RegisterSignals(parent, Dm.list(COMSIG_FOOD_CONSUMED, COMSIG_OBJ_DECONSTRUCT), nameof(storage_consumed));

        atom food = (atom)parent;
        initial_volume = ((datum_reagents)food.reagents).total_volume;

        minimum_weight_class = Dm.num(_minimum_weight_class);
        bad_chance_of_discovery = Dm.num(_bad_chance);
        good_chance_of_discovery = Dm.num(_good_chance);

        Dm.combine(ref food.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
        return null;
    }

    // tg: code/datums/components/food_storage.dm:34
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ITEM_INTERACTION_SECONDARY,
            COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM,
            COMSIG_CLICK_CTRL,
            COMSIG_FOOD_CONSUMED,
            COMSIG_FOOD_EATEN,
            COMSIG_OBJ_DECONSTRUCT
        ));
        if (QDELING(parent) || QDELETED(stored_item))
            return;
        await stored_item.forceMove(stored_item.drop_location() as atom);
        stored_item = null;
    }

    /** Begins the process of inserted an item.
     *
     * Clicking on the food storage with an item will begin a do_after, which if successful inserts the item.
     *
     * Arguments
     * inserted_item - the item being placed into the food
     * user - the person inserting the item
     */
    // tg: code/datums/components/food_storage.dm:56
    [SignalHandler]
    public virtual object try_inserting_item(datum source = null, mob_living user = null, obj_item inserted_item = null, DmList modifiers = null)
    {
        // No matryoshka-ing food storage
        if (inserted_item is obj_item_storage || IS_EDIBLE(inserted_item))
            return NONE;

        //Harm intent will bypass inserting for injecting food with syringes and such
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (inserted_item.w_class > minimum_weight_class)
        {
            to_chat(user, span_warning(I18n.T("[] won't fit in [].", inserted_item, parent)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!QDELETED(stored_item))
        {
            to_chat(user, span_warning(I18n.T("There's something in [].", parent)));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(
            span_notice(I18n.T("[] begins inserting [] into [].", user, inserted_item, parent)),
            span_notice(I18n.T("You start to insert the [] into [].", inserted_item, parent))
        );

        Waiting.Detach(insert_item(inserted_item, user));
        return ITEM_INTERACT_SUCCESS;
    }

    /** Begins the process of attempting to remove the stored item.
     *
     * Clicking on food storage on grab intent will begin a do_after, which if successful removes the stored_item.
     *
     * Arguments
     * user - the person removing the item.
     */
    // tg: code/datums/components/food_storage.dm:90
    [SignalHandler]
    public virtual object try_removing_item(datum source = null, mob user = null)
    {
        atom food = (atom)parent;

        if (!Dm.truthy(Waiting.NoSleep(food.can_interact(user))))
            return CLICK_ACTION_BLOCKING;

        user.visible_message(span_notice(I18n.T("[] begins tearing at [].", user, parent)),
            span_notice(I18n.T("You start to rip into [].", parent)));

        Waiting.Detach(begin_remove_item(user));
        return CLICK_ACTION_SUCCESS;
    }

    /** Inserts the item into the food, after a do_after.
     *
     * Arguments
     * inserted_item - The item being inserted.
     * user - the person inserting the item.
     */
    // tg: code/datums/components/food_storage.dm:110
    public virtual async DmTask insert_item(obj_item inserted_item = null, mob user = null)
    {
        if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, target: parent)))
            return;
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(inserted_item)))
        {
            to_chat(user, span_warning(I18n.T("You can't seem to insert [] into [].", inserted_item, parent)));
            return;
        }

        atom food = (atom)parent;
        to_chat(user, span_notice(I18n.T("You slip [] inside [].", inserted_item, parent)));
        await inserted_item.forceMove(food);
        user.log_message(Dm.text("inserted [] into [].", inserted_item, parent), LOG_ATTACK);
        food.add_fingerprint(user);
        inserted_item.add_fingerprint(user);

        stored_item = inserted_item;
    }

    /** Removes the item from the food, after a do_after.
     *
     * Arguments
     * user - person removing the item.
     */
    // tg: code/datums/components/food_storage.dm:131
    public virtual async DmTask begin_remove_item(mob user = null)
    {
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: parent)))
            return;
        if (QDELETED(stored_item))
        {
            to_chat(user, span_warning(I18n.T("There's nothing in [].", parent)));
            return;
        }
        await remove_item(user);
    }

    /**
     * Removes the stored item, putting it in user's hands or on the ground, then updates the reference.
     */
    // tg: code/datums/components/food_storage.dm:142
    public virtual async DmTask remove_item(mob user = null)
    {
        if (Dm.truthy(await user.put_in_hands(stored_item)))
            user.visible_message(span_warning(I18n.T("[] slowly pulls [] out of [].", user, stored_item, parent)),
                span_warning(I18n.T("You slowly pull [] out of [].", stored_item, parent)));
        else
            stored_item.visible_message(span_warning(I18n.T("[] falls out of [].", stored_item, parent)));

        update_stored_item();
    }

    /** Checks for stored items when the food is eaten.
     *
     * If the food is eaten while an item is stored in it, calculates the odds that the item will be found.
     * Then, if the item is found before being bitten, the item is removed.
     * If the item is found by biting into it, calls on_accidental_consumption on the stored item.
     * Afterwards, removes the item from the food if it was discovered.
     *
     * Arguments
     * target - person doing the eating (can be the same as user)
     * user - person causing the eating to happen
     * bitecount - how many times the current food has been bitten
     * bitesize - how large bties are for this food
     */
    // tg: code/datums/components/food_storage.dm:164
    [SignalHandler]
    public virtual void consume_food_storage(datum source = null, mob_living target = null, mob_living user = null, object bitecount = null, object bitesize = null)
    {
        if (QDELETED(stored_item)) //if the stored item was deleted/null...
        {
            if (!Dm.truthy(update_stored_item())) //check if there's a replacement item
                return;
        }

        /// Chance of biting the held item = amount of bites / (intitial reagents / reagents per bite) * 100
        bad_chance_of_discovery = Dm.num(Dm.div(bitecount, Dm.div(initial_volume, bitesize))) * 100;
        /// Chance of finding the held item = bad chance - 50
        good_chance_of_discovery = bad_chance_of_discovery - 50;

        object discovered = FALSE;
        if (prob(good_chance_of_discovery)) //finding the item, without biting it
        {
            discovered = TRUE;
            to_chat(target, span_warning(I18n.T("It feels like there's something in []...!", parent)));
        }
        else if (prob(bad_chance_of_discovery)) //finding the item, BY biting it
        {
            user.log_message(Dm.text("just fed [] \\a [] which was hidden in [].", key_name(target), stored_item, parent), LOG_ATTACK);
            discovered = Waiting.NoSleep(stored_item.on_accidental_consumption(target as mob_living_carbon, user as mob_living_carbon, parent as obj_item));
            update_stored_item(); //make sure if the item was changed, the reference changes as well
        }

        if (!QDELETED(stored_item) && Dm.truthy(discovered))
            Waiting.Detach(remove_item(user));
    }

    /// When fully consumed, just drop the item out on the ground.
    // tg: code/datums/components/food_storage.dm:190
    [SignalHandler]
    public virtual void storage_consumed(datum source = null, mob_living target = null, mob_living user = null)
    {
        if (QDELETED(stored_item))
            return;
        Waiting.NoSleep(stored_item.forceMove(stored_item.drop_location() as atom));
        stored_item = null;
    }

    /** Updates the reference of the stored item.
     *
     * Checks the food's contents for if an alternate item was placed into the food.
     * If there is an alternate item, updates the reference to the new item.
     * If there isn't, updates the reference to null.
     *
     * Returns FALSE if the ref is nulled, or TRUE is another item replaced it.
     */
    // tg: code/datums/components/food_storage.dm:205
    public virtual object update_stored_item()
    {
        atom food = parent as atom;
        if (!Dm.truthy(food?.contents.len)) //if there's no items in the food or food is deleted somehow
        {
            stored_item = null;
            return FALSE;
        }

        foreach (obj_item i in Dm.each<obj_item>(food.contents)) //search the food's contents for a replacement item
        {
            if (IS_EDIBLE(i))
                continue;
            if (QDELETED(i))
                continue;

            stored_item = i; //we found something to replace it
            return TRUE;
        }

        //if there's nothing else in the food, or we found nothing valid
        stored_item = null;
        return FALSE;
    }

    /**
     * Adds context sensitivy directly to the processable file for screentips
     * Arguments:
     * * source - refers to item that will display its screentip
     * * context - refers to, in this case, an item that can be inserted into another item
     * * held_item - refers to item in user's hand, typically the one that will be inserted into the food item
     * * user - refers to user who will see the screentip when the proper context and tool are there
     */
    // tg: code/datums/components/food_storage.dm:233
    [SignalHandler]
    public virtual object on_requesting_context_from_item(datum source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;

        if (held_item == null || held_item == source)
        {
            context[SCREENTIP_CONTEXT_CTRL_LMB] = "Remove embedded item (if any)";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        if (held_item is obj_item && held_item.w_class <= WEIGHT_CLASS_SMALL && held_item != source && !IS_EDIBLE(held_item))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Embed item";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }

        return dot;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/material_container/material_container.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2a (D304): the whole file but vv_edit_var (the admin var editor's re-registering of the signals: a live-partial
// row). The var block is generated (D111). The screentip context handler is registered as tg does, with the other
// context tips deferred (the owner's rule of 2026-10-29).

/*!
	This datum should be used for handling mineral contents of machines and whatever else is supposed to hold minerals and make use of them.

	Variables:
		amount - raw amount of the mineral this container is holding, calculated by the defined value SHEET_MATERIAL_AMOUNT=SHEET_MATERIAL_AMOUNT.
		max_amount - max raw amount of mineral this container can hold.
		sheet_type - type of the mineral sheet the container handles, used for output.
		parent - object that this container is being used by, used for output.
		MAX_STACK_SIZE - size of a stack of mineral sheets. Constant.
*/

// tg: code/datums/materials/material_container/material_container.dm:15
public partial class datum_material_container : datum
{
    //The full item was consumed
    private const float MATERIAL_INSERT_ITEM_SUCCESS = 1; // tg: code/datums/materials/material_container/material_container.dm:13

    /// Sets up the proper signals and fills the list of materials with the appropriate references.
    // tg: code/datums/materials/material_container/material_container.dm:34
    [DmParams("target", "init_mats", "max_amt", "_mat_container_flags", "allowed_mats", "allowed_items", "container_signals")]
    public override async DmTask New(params object[] __extra)
    {
        object target = Dm.arg(__extra, 0);
        DmList init_mats = Dm.arg(__extra, 1) as DmList;
        object max_amt = Dm.arg(__extra, 2) ?? 0;
        object _mat_container_flags = Dm.arg(__extra, 3) ?? NONE;
        // allowed_mats = init_mats: the default when the argument is null or not passed (DM's defaults)
        DmList allowed_mats = Dm.arg(__extra, 4) as DmList ?? init_mats;
        object allowed_items = Dm.arg(__extra, 5);
        DmList container_signals = Dm.arg(__extra, 6) as DmList;

        parent = target as atom_movable;

        materials = Dm.list();
        max_amount = max(0, Dm.num(max_amt));
        mat_container_flags = _mat_container_flags;

        allowed_materials = allowed_mats ?? Dm.list();
        if (Dm.truthy(allowed_items))
        {
            if (ispath(allowed_items) && Dm.eq(allowed_items, typeof(obj_item_stack)))
                allowed_item_typecache = GLOB.typecache_stack;
            else
                allowed_item_typecache = typecacheof(allowed_items) as DmList;
        }

        //Make the assoc list material reference -> amount
        foreach (object mat in Dm.each<object>(init_mats))
        {
            object mat_ref = await SSmaterials.get_material(mat);
            if (mat_ref == null)
                continue;
            object mat_amt = init_mats[mat];
            if (mat_amt == null)
                mat_amt = 0;
            materials[mat_ref] = Dm.num(materials[mat_ref]) + Dm.num(mat_amt);
        }

        //all user handled signals
        if (Dm.truthy(length(container_signals)))
        {
            foreach (object signal in Dm.each<object>(container_signals))
                parent.RegisterSignal(this, signal, container_signals[signal]);
        }

        //can we insert into this container
        if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_NO_INSERT)))
        {
            //to insert stuff into the container
            RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_insert));
            RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION_SECONDARY, nameof(on_secondary_insert));

            //screen tips for inserting items
            parent.flags_1 = Dm.bitor(Dm.num(parent.flags_1), HAS_CONTEXTUAL_SCREENTIPS_1);
            RegisterSignal(parent, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        }

        //to see available materials
        if (Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_EXAMINE)))
            RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));

        //drop sheets when the object is deconstructed but not deleted
        RegisterSignal(parent, COMSIG_OBJ_DECONSTRUCT, nameof(drop_sheets));
    }

    // tg: code/datums/materials/material_container/material_container.dm:88
    public override object Destroy(float? force = null)
    {
        materials = null;
        allowed_materials = null;
        return base.Destroy(force);
    }

    // tg: code/datums/materials/material_container/material_container.dm:93
    public virtual void drop_sheets()
    {
        // SIGNAL_HANDLER: the sheets of a deconstructed machine drop at once (stacks are made without a wait)
        Waiting.NoSleep(retrieve_all());
    }

    // tg: code/datums/materials/material_container/material_container.dm:98
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_texts = null)
    {
        foreach (object I in Dm.each<object>(materials))
        {
            datum_material M = (datum_material)I;
            float amt = Dm.num(materials[I]) / SHEET_MATERIAL_AMOUNT;
            if (Dm.truthy(amt))
                examine_texts.Add(span_notice(I18n.T("It has [] sheets of [] stored.", amt, LOWER_TEXT(M.name))));
        }
    }

    //================================Material Insertion procs==============================

    //======================================LOW LEVEL=========================================
    /**
     * Inserts the relevant materials from an item into this material container.
     * This low level proc should not be used directly by anyone
     */
    // tg: code/datums/materials/material_container/material_container.dm:148
    public virtual async DmTask<object> insert_item_materials(obj_item source = null, float? multiplier = null, atom context = null, DmList user_data = null)
    {
        multiplier ??= 1;
        context ??= parent;
        object primary_mat = null;
        float max_mat_value = 0;
        float material_amount = 0;

        DmList item_materials = await source.get_material_composition(mat_container_flags) as DmList;
        DmList mats_consumed = Dm.list();
        foreach (object MAT in Dm.each<object>(item_materials))
        {
            if (!can_hold_material(MAT as datum_material))
                continue;
            float mat_amount = OPTIMAL_COST(Dm.num(item_materials[MAT]) * multiplier.Value);
            materials[MAT] = Dm.num(materials[MAT]) + mat_amount;
            if (Dm.num(item_materials[MAT]) > max_mat_value)
            {
                max_mat_value = Dm.num(item_materials[MAT]);
                primary_mat = MAT;
            }
            mats_consumed[MAT] = mat_amount;
            material_amount += mat_amount;
        }
        if (Dm.truthy(length(mats_consumed)) && Dm.truthy(_listen_lookup?[COMSIG_MATCONTAINER_ITEM_CONSUMED]))
            _SendSignal(COMSIG_MATCONTAINER_ITEM_CONSUMED, Dm.list(this, source, primary_mat, mats_consumed, material_amount, context, user_data));

        return primary_mat;
    }

    //===============================MID LEVEL===================================================
    /**
     * For inserting an amount of material. Use this to add materials to the container directly
     */
    // tg: code/datums/materials/material_container/material_container.dm:179
    public virtual async DmTask<object> insert_amount_mat(object amt = null, object mat = null)
    {
        if (Dm.num(amt) <= 0)
            return 0;
        float amount = OPTIMAL_COST(amt);
        if (!await has_space(amount))
            return 0;

        float total_amount_saved = await total_amount();
        if (Dm.truthy(mat))
        {
            if (!istype(mat, typeof(datum_material)))
                mat = await SSmaterials.get_material(mat);
            materials[mat] = Dm.num(materials[mat]) + amount;
        }
        else
        {
            float num_materials = length(materials);
            if (!Dm.truthy(num_materials))
                return 0;

            amount /= num_materials;
            foreach (object i in Dm.each<object>(materials))
                materials[i] = Dm.num(materials[i]) + amount;
        }
        return await total_amount() - total_amount_saved;
    }

    /**
     * Proc specifically for inserting items, use this when you want to insert any item into the container
     * this bypasses most of the material flag checks so much be used by machines like recycler, stacking machine etc that
     * does not care for such checks
     */
    // tg: code/datums/materials/material_container/material_container.dm:215
    public virtual async DmTask<object> insert_item(obj_item weapon = null, float? multiplier = null, atom context = null, float? delete_item = null, DmList user_data = null)
    {
        multiplier ??= 1;
        context ??= parent;
        delete_item ??= TRUE;
        if (QDELETED(weapon))
            return MATERIAL_INSERT_ITEM_NO_MATS;
        multiplier = CEILING(multiplier.Value, 0.01f);

        obj_item target = weapon;

        float material_amount = OPTIMAL_COST(await get_item_material_amount(target) * multiplier.Value);
        if (!Dm.truthy(material_amount))
            return MATERIAL_INSERT_ITEM_NO_MATS;
        if (HAS_TRAIT(weapon, TRAIT_IGNORED_BY_MAT_REDEMPTION))
        {
            qdel(weapon);
            return MATERIAL_INSERT_ITEM_NO_MATS;
        }
        obj_item_stack item_stack = null;
        if (isstack(weapon) && !await has_space(material_amount)) //not enough space split and feed as many sheets possible
        {
            item_stack = (obj_item_stack)weapon;
            float space_left = Dm.num(max_amount) - await total_amount();
            if (!Dm.truthy(space_left))
                return MATERIAL_INSERT_ITEM_NO_SPACE;
            float material_per_sheet = material_amount / Dm.num(item_stack.amount);
            float sheets_to_insert = floor(space_left / material_per_sheet);
            if (!Dm.truthy(sheets_to_insert))
                return MATERIAL_INSERT_ITEM_NO_SPACE;
            target = fast_split_stack(item_stack, sheets_to_insert) as obj_item;
            material_amount = await get_item_material_amount(target) * multiplier.Value;
        }
        material_amount = OPTIMAL_COST(material_amount);

        //do the insert
        object last_inserted_id = await insert_item_materials(target, multiplier, context, user_data: user_data);
        if (last_inserted_id != null)
        {
            if (Dm.truthy(delete_item) || target != weapon) //we could have split the stack ourselves
                qdel(target); //item gone
            return material_amount;
        }
        else if (item_stack != null && item_stack != target) //insertion failed, merge the split stack back into the original
        {
            obj_item_stack inserting_stack = (obj_item_stack)target;
            await item_stack.add(inserting_stack.amount);
            qdel(inserting_stack);
        }

        return MATERIAL_INSERT_ITEM_FAILURE;
    }

    //===================================HIGH LEVEL===================================================
    /**
     * inserts an item from the players hand into the container. Loops through all the contents inside recursively
     * Does all explicit checking for mat flags & callbacks to check if insertion is valid
     * This proc is what you should be using for almost all cases
     */
    // tg: code/datums/materials/material_container/material_container.dm:265  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4)
    public virtual object user_insert(obj_item held_item = null, mob_living user = null, atom context = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(user_insert__body(held_item, user, context ?? parent, dot), dot);
    }

    private async DmTask<object> user_insert__body(obj_item held_item, mob_living user, atom context, DotBox<object> dot)
    {
        dot.Value = 0;

        //All items that do not have any contents
        DmList items = Dm.list(held_item);
        //is this the first item we are ever processing
        bool first_checks = true;
        //list of items to delete
        DmList to_delete = Dm.list();
        //The status of the last insert attempt
        float inserted = 0;
        //All messages to be displayed to chat
        DmList chat_msgs = Dm.list();
        //differs from held_item when using TK
        obj_item active_held = user.get_active_held_item() as obj_item;
        //omni tools can act as any tool so get its real behaviour
        active_held = active_held.get_proxy_attacker_for(held_item) as obj_item;

        while (Dm.truthy(items.len))
        {
            //no point inserting more items
            if (inserted == MATERIAL_INSERT_ITEM_NO_SPACE)
                break;

            //Pop the 1st item out from the list
            object popped = items[1];
            Dm.remove(items, popped);

            //e.g. projectiles inside bullets are not objects
            if (popped is not obj_item target_item)
                continue;
            //can't allow abstract, hologram items, or if they item is designed to be ignored by the mat container without throwing warnings and stuff (unlike NO_MAT_REDEMPTION flag).
            if (Dm.truthy(Dm.bitand(target_item.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(target_item.flags_1, HOLOGRAM_1)) || HAS_TRAIT(target_item, TRAIT_IGNORED_BY_MAT_REDEMPTION))
                continue;
            //user defined conditions for the object the material container datum is attached to
            if (Dm.truthy(_listen_lookup?[COMSIG_MATCONTAINER_PRE_USER_INSERT]) && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_MATCONTAINER_PRE_USER_INSERT, Dm.list(this, target_item, user)), MATCONTAINER_BLOCK_INSERT)))
                continue;
            //item is either indestructible, not allowed for redemption or not in the allowed types
            if (Dm.truthy(Dm.bitand(target_item.resistance_flags, INDESTRUCTIBLE)) || Dm.truthy(Dm.bitand(target_item.item_flags, NO_MAT_REDEMPTION)) || (allowed_item_typecache != null && !Dm.truthy(is_type_in_typecache(target_item, allowed_item_typecache))))
            {
                if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_SILENT)))
                {
                    DmList status_data = chat_msgs[Dm.text("[]", MATERIAL_INSERT_ITEM_FAILURE)] as DmList ?? Dm.list();
                    DmList item_data = status_data[target_item.name] as DmList ?? Dm.list();
                    item_data["item"] = target_item;
                    item_data["count"] = Dm.num(item_data["count"]) + 1;
                    status_data[target_item.name] = item_data;
                    chat_msgs[Dm.text("[]", MATERIAL_INSERT_ITEM_FAILURE)] = status_data;
                }

                if (Dm.truthy(Dm.bitand(target_item.resistance_flags, INDESTRUCTIBLE)))
                {
                    if (target_item != active_held) //move it out of any storage medium its in so it doesn't get consumed with its parent, but only if that storage medium is not our hand
                        await target_item.forceMove(get_turf(context));
                    continue;
                }

                //storage items usually come here
                //this is so players can insert items from their bags into machines for convinience
                if (target_item.atom_storage == null || !Dm.truthy(target_item.contents.len))
                    continue;
            }
            //at this point we can check if we have enough for all items & other stuff
            if (first_checks)
            {
                //duffle bags needs to be unzipped
                if (Dm.truthy(target_item.atom_storage?.locked))
                {
                    if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_SILENT)))
                        to_chat(user, span_warning(I18n.T("[] has its storage locked", target_item)));
                    return dot.Value;
                }

                //anything that isn't a stack cannot be split so find out if we have enough space, we don't want to consume half the contents of an object & leave it in a broken state
                //for duffle bags and other storage items we can check for space 1 item at a time
                if (!isstack(target_item) && target_item.atom_storage == null)
                {
                    float total = 0;
                    foreach (obj_item weapon in Dm.each<obj_item>(target_item.get_all_contents_type(typeof(obj_item))))
                        total += await get_item_material_amount(weapon);
                    if (!await has_space(total))
                    {
                        if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_SILENT)))
                            to_chat(user, span_warning(I18n.T("[] does not have enough space for []!", parent, target_item)));
                        return dot.Value;
                    }
                }

                first_checks = false;
            }

            //if stack, check if we want to read precise amount of sheets to insert
            obj_item_stack item_stack = null;
            if (isstack(target_item) && Dm.truthy(precise_insertion))
            {
                atom current_parent = parent;
                item_stack = (obj_item_stack)target_item;
                object requested_amount = await tgui_input_number(user, I18n.T("How much do you want to insert?"), I18n.T("Inserting []s", item_stack.singular_name), item_stack.amount, item_stack.amount);
                if (!Dm.truthy(requested_amount) || QDELETED(target_item) || QDELETED(user) || QDELETED(this))
                    continue;
                if (parent != current_parent || user.get_active_held_item() != active_held)
                    continue;
                if (!Dm.eq(requested_amount, item_stack.amount)) //only split if its not the whole amount
                    target_item = fast_split_stack(item_stack, requested_amount) as obj_item; //split off the requested amount
            }

            //is this item a stack and was it split by the player?
            bool was_stack_split = item_stack != null && item_stack != target_item;
            //if it was split then item_stack has the reference to the original stack/item
            obj_item original_item = was_stack_split ? item_stack : target_item;
            //if this item is not the one the player is holding then don't remove it from their hand
            if (original_item != active_held)
                original_item = null;
            if (original_item != null && !Dm.truthy(await user.temporarilyRemoveItemFromInventory(original_item))) //remove from hand(if split remove the original stack else the target)
                return dot.Value;

            //insert the item
            object item_name = target_item.name;
            object item_count = 1;
            bool is_stack = false;
            if (isstack(target_item))
            {
                obj_item_stack the_stack = (obj_item_stack)target_item;
                item_name = the_stack.singular_name;
                item_count = the_stack.amount;
                is_stack = true;
            }

            //we typically don't want to consume bags, boxes but only their contents. so we skip processing
            inserted = target_item.atom_storage == null ? Dm.num(await insert_item(target_item, 1, context, is_stack ? TRUE : FALSE, user_data: ID_DATA(user))) : 0;
            if (inserted > 0)
            {
                dot.Value = Dm.num(dot.Value) + inserted;
                inserted /= SHEET_MATERIAL_AMOUNT; // display units inserted as sheets for improved readability

                //collect all messages to print later
                DmList status_data = chat_msgs[Dm.text("[]", MATERIAL_INSERT_ITEM_SUCCESS)] as DmList ?? Dm.list();
                DmList item_data = status_data[item_name] as DmList ?? Dm.list();
                item_data["item"] = target_item;
                item_data["count"] = Dm.num(item_data["count"]) + Dm.num(item_count);
                item_data["amount"] = Dm.num(item_data["amount"]) + inserted;
                status_data[item_name] = item_data;
                chat_msgs[Dm.text("[]", MATERIAL_INSERT_ITEM_SUCCESS)] = status_data;

                //delete the item or merge stacks if any left over
                if (is_stack)
                {
                    //player split it & machine further split that due to lack of space? merge with remaining stack
                    if (!QDELETED(target_item) && was_stack_split)
                    {
                        obj_item_stack inserting_stack = (obj_item_stack)target_item;
                        await item_stack.add(inserting_stack.amount);
                        qdel(inserting_stack);
                    }

                    //was this the original item in the players hand? put what's left back in the player's hand
                    if (!QDELETED(original_item))
                        await user.put_in_active_hand(original_item);

                    //skip processing children & other stuff. irrelevant for stacks
                    continue;
                }

                //queue the object for deletion
                to_delete.Add(target_item);
            }
            else
            {
                //collect all messages to print later
                DmList status_data = chat_msgs[Dm.text("[]", inserted)] as DmList ?? Dm.list();
                DmList item_data = status_data[item_name] as DmList ?? Dm.list();
                item_data["item"] = target_item;
                item_data["count"] = Dm.num(item_data["count"]) + Dm.num(item_count);
                status_data[item_name] = item_data;
                chat_msgs[Dm.text("[]", inserted)] = status_data;

                //player split the stack by the requested amount but even that split amount could not be salvaged. merge it back with the original
                if (was_stack_split)
                {
                    obj_item_stack inserting_stack = (obj_item_stack)target_item;
                    await item_stack.add(inserting_stack.amount);
                    qdel(inserting_stack);
                }

                //was this the original item in the players hand? put it back because we coudn't salvage it
                if (!QDELETED(original_item))
                    await user.put_in_active_hand(original_item);

                //we can stop here as remaining items will fail to insert as well
                if (inserted == MATERIAL_INSERT_ITEM_NO_SPACE)
                    break;

                //we failed to process the item so don't bother going into its contents
                //but if we are dealing with storage items like bags, boxes etc then we make a exception
                if (target_item.atom_storage == null)
                    continue;
            }

            //If any mats were consumed we can proceed to delete the parent
            //If it has children then we will process them first in the 2nd round
            //This is done so we don't delete the children when the parent is consumed
            //We only do this on the 1st iteration so we don't re-iterate through its children again
            if (Dm.truthy(target_item.contents.len))
            {
                if (Dm.truthy(target_item.atom_storage?.locked)) //can't access contents of locked storage(like duffle bags)
                    continue;
                //process children
                Dm.append(ref items, target_item.contents);
            }
        }

        //we now summarize the chat msgs collected
        if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_SILENT)))
        {
            foreach (object status in Dm.each<object>(chat_msgs))
            {
                DmList status_data = (DmList)chat_msgs[status];

                foreach (object item_name in Dm.each<object>(status_data))
                {
                    //read the params
                    DmList chat_data = (DmList)status_data[item_name];
                    obj_item item = (obj_item)chat_data["item"];
                    float count = Dm.num(chat_data["count"]);
                    float amount = Dm.num(chat_data["amount"]);

                    //decode the message
                    switch (Dm.num(Dm.text2num(status)))
                    {
                        case MATERIAL_INSERT_ITEM_SUCCESS: //no problems full item was consumed
                            if (isstack(item))
                            {
                                obj_item_stack stack = (obj_item_stack)item;
                                float sheets = min(count, amount); //minimum between sheets inserted vs sheets consumed(values differ for alloys)
                                if (sheets > 1)
                                    to_chat(user, span_notice(I18n.T("[] [][] were added to [].", sheets, stack.singular_name, plural_s(stack.singular_name), parent)));
                                else
                                    to_chat(user, span_notice(I18n.T("The [] was added to [].", stack.singular_name, parent)));
                            }
                            else
                            {
                                if (count > 1)
                                    to_chat(user, span_notice(I18n.T("[] [][], worth [] sheet[], were added to [].", count, item.name, plural_s(item.name), amount, amount == 1 ? "" : "s", parent)));
                                else
                                    to_chat(user, span_notice(I18n.T("[], worth [] sheet[], [] added to [].", Dm.text("\\The []", item), amount, amount == 1 ? "" : "s", item.p_were(), parent)));
                            }
                            break;
                        case MATERIAL_INSERT_ITEM_NO_SPACE: //no space
                            to_chat(user, span_warning(I18n.T("[] has no space to accept []!", parent, Dm.text("\\the []", item))));
                            break;
                        case MATERIAL_INSERT_ITEM_NO_MATS: //no materials inside these items
                            if (isstack(item))
                            {
                                obj_item_stack stack = (obj_item_stack)item;
                                to_chat(user, span_warning(I18n.T("The [] has no materials that can be accepted by []!", stack.singular_name, parent)));
                            }
                            else
                                to_chat(user, span_warning(I18n.T("[][] no materials that can be accepted by []!", Dm.text("\\The []", item), count > 1 ? Dm.text("[] have", plural_s(item.name)) : Dm.text(" []", item.p_have()), parent)));
                            break;
                        case MATERIAL_INSERT_ITEM_FAILURE: //could be because the material type was not accepted or other stuff
                            if (isstack(item))
                            {
                                obj_item_stack stack = (obj_item_stack)item;
                                to_chat(user, span_warning(I18n.T("The [] was rejected by []!", stack.singular_name, parent)));
                            }
                            else
                                to_chat(user, span_warning(I18n.T("[][] rejected by []!", Dm.text("\\The []", item), count > 1 ? Dm.text("[] were", plural_s(item.name)) : Dm.text(" []", item.p_were()), parent)));
                            break;
                    }
                }
            }
        }

        //finally delete the items
        foreach (obj_item deleting in Dm.each<obj_item>(to_delete))
        {
            if (!QDELETED(deleting)) //deleting parents also delete their children so we check
                qdel(deleting);
        }
        return dot.Value;
    }

    // tg: code/datums/materials/material_container/material_container.dm:496
    public virtual object on_item_insert(datum source = null, mob_living user = null, obj_item weapon = null)
    {
        // Don't insert material items with left click
        if (isstack(weapon))
            return attempt_insert(user, weapon);
        return null;
    }

    // tg: code/datums/materials/material_container/material_container.dm:502
    public virtual object on_secondary_insert(datum source = null, mob_living user = null, obj_item weapon = null)
    {
        return attempt_insert(user, weapon);
    }

    /// Proc that allows players to fill the parent with mats
    // tg: code/datums/materials/material_container/material_container.dm:507
    public virtual object attempt_insert(mob_living user = null, obj_item weapon = null)
    {
        //Allows you to attack the machine with iron sheets for e.g.
        if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_ANY_INTENT)) && Dm.truthy(user.combat_mode))
            return null;

        if (ismachinery(parent))
        {
            obj_machinery machine = (obj_machinery)parent;
            if (Dm.truthy(machine.machine_stat) || Dm.truthy(machine.panel_open))
                return null;
        }

        user_insert(weapon, user);
        return ITEM_INTERACT_SUCCESS;
    }

    //======================================Material Validation=======================================

    //=========================================LOW LEVEL===================================
    /**
     * Proc that returns TRUE if the container has space
     */
    // tg: code/datums/materials/material_container/material_container.dm:530
    public virtual async DmTask<bool> has_space(float amt = 0)
    {
        return await total_amount() + amt <= Dm.num(max_amount);
    }

    /**
     * The default check for whether we can add materials to this material container.
     */
    // tg: code/datums/materials/material_container/material_container.dm:539
    public virtual bool can_hold_material(datum_material mat = null)
    {
        if (Dm.@in(mat, allowed_materials))
            return true;
        if (mat != null && (Dm.@in(mat.id, allowed_materials) || Dm.@in(mat.type, allowed_materials)))
        {
            allowed_materials.Add(mat); // This could get messy with passing lists by ref... but if you're doing that the list expansion is probably being taken care of elsewhere anyway...
            return true;
        }
        if (Dm.truthy(_listen_lookup?[COMSIG_MATCONTAINER_MAT_CHECK]) && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_MATCONTAINER_MAT_CHECK, Dm.list(this, mat)), MATCONTAINER_ALLOW_MAT)))
        {
            allowed_materials.Add(mat);
            return true;
        }
        return false;
    }

    //===================================MID LEVEL=============================================
    /**
     * Returns the amount of a specific material in this container.
     */
    // tg: code/datums/materials/material_container/material_container.dm:563
    public virtual async DmTask<float> get_material_amount(object mat = null)
    {
        if (!istype(mat, typeof(datum_material)))
            mat = await SSmaterials.get_material(mat);
        return Dm.num(materials[mat]);
    }

    /**
     * Returns the amount of material relevant to this container;
     * if this container does not support glass, any glass in 'I' will not be taken into account
     */
    // tg: code/datums/materials/material_container/material_container.dm:575
    public virtual async DmTask<float> get_item_material_amount(obj_item item = null)
    {
        if (item == null || !Dm.truthy(item.custom_materials))
            return 0;
        float material_amount = 0;
        DmList item_materials = await item.get_material_composition(mat_container_flags) as DmList;
        foreach (object MAT in Dm.each<object>(item_materials))
        {
            if (!can_hold_material(MAT as datum_material))
                continue;
            material_amount += Dm.num(item_materials[MAT]);
        }
        return material_amount;
    }

    //=========================================HIGH LEVEL==========================================
    /// returns the total amount of material in the container
    // tg: code/datums/materials/material_container/material_container.dm:591
    public virtual async DmTask<float> total_amount()
    {
        float dot = 0;
        foreach (object i in Dm.each<object>(materials))
            dot += await get_material_amount(i);
        return dot;
    }

    /**
     * Returns TRUE if you have enough of the specified material.
     */
    // tg: code/datums/materials/material_container/material_container.dm:603
    public virtual async DmTask<bool> has_enough_of_material(object req_mat = null, float amount = 1)
    {
        return await get_material_amount(req_mat) >= OPTIMAL_COST(amount);
    }

    /**
     * Checks if its possible to afford a certain amount of materials. Takes a dictionary of materials.
     * coefficient can be thought of as the machines efficiency & multiplier as the print quantity
     */
    // tg: code/datums/materials/material_container/material_container.dm:616
    public virtual async DmTask<bool> has_materials(DmList mats = null, float coefficient = 1, float multiplier = 1)
    {
        if (!Dm.truthy(length(mats)))
            return false;

        foreach (object x in Dm.each<object>(mats)) //Loop through all required materials
        {
            float wanted = OPTIMAL_COST(Dm.num(mats[x]) * coefficient) * multiplier;
            if (!await has_enough_of_material(x, wanted))//Not a category, so just check the normal way
                return false;
        }

        return true;
    }

    //================================================Material Usage============================================

    //==================================================LOW LEVEL=======================================
    /**
     * Uses an amount of a specific material, effectively removing it.
     */
    // tg: code/datums/materials/material_container/material_container.dm:638
    public virtual async DmTask<float> use_amount_mat(object amt = null, object mat = null)
    {
        //round amount
        float amount = OPTIMAL_COST(amt);

        //get ref if necessary
        if (!istype(mat, typeof(datum_material)))
            mat = await SSmaterials.get_material(mat);

        //check if sufficient is available
        if (Dm.num(materials[mat]) < amount)
            return 0;

        //consume & return amount consumed
        materials[mat] = Dm.num(materials[mat]) - amount;
        return amount;
    }

    //=========================================MID LEVEL==========================================
    /**
     * For consuming a dictionary of materials.
     */
    // tg: code/datums/materials/material_container/material_container.dm:664
    public virtual async DmTask<object> use_materials(DmList mats = null, float coefficient = 1, float multiplier = 1)
    {
        if (mats == null || !Dm.truthy(length(mats)))
            return FALSE;

        float amount_removed = 0;
        foreach (object i in Dm.each<object>(mats))
            amount_removed += await use_amount_mat(OPTIMAL_COST(Dm.num(mats[i]) * coefficient) * multiplier, i);

        return amount_removed;
    }

    //===========================================HIGH LEVEL=======================================

    /**
     * For spawning stacks (mineral sheets or ore) at a specific location. Used by machines to output sheets.
     */
    // tg: code/datums/materials/material_container/material_container.dm:690
    public virtual async DmTask<float> retrieve_stack(object stack_amt = null, datum_material material = null, atom target = null, atom context = null, DmList user_data = null)
    {
        context ??= parent;
        //do we support sheets of this material
        object type_to_retrieve = material.sheet_type ?? material.ore_type;
        if (!Dm.truthy(type_to_retrieve))
            return 0; //Add greyscale sheet handling here later
        if (!can_hold_material(material))
            return 0;

        //requested amount greater than available amount or just an invalid value
        float amount = min(floor(Dm.num(materials[material]) / SHEET_MATERIAL_AMOUNT), Dm.num(stack_amt));
        if (amount <= 0)
            return 0;
        //auto drop location
        if (target == null)
        {
            atom parent_atom = parent;
            target = parent_atom.drop_location() as atom;
            if (target == null)
                return 0;
        }

        //eject sheets based on available amount after each iteration
        float count = 0;
        while (amount > 0)
        {
            //don't merge yet. we need to do stuff with it first
            obj_item_stack new_stack = (obj_item_stack)Dm.@new((System.Type)type_to_retrieve, target, min(amount, MAX_STACK_SIZE), FALSE);
            if (new_stack is obj_item_stack_sheet new_sheets)
                new_sheets.manufactured = TRUE;
            count += Dm.num(new_stack.amount);
            //use material & deduct work needed
            await use_amount_mat(Dm.num(new_stack.amount) * SHEET_MATERIAL_AMOUNT, material);
            amount -= Dm.num(new_stack.amount);
            //send signal
            if (Dm.truthy(_listen_lookup?[COMSIG_MATCONTAINER_STACK_RETRIEVED]))
                _SendSignal(COMSIG_MATCONTAINER_STACK_RETRIEVED, Dm.list(this, new_stack, context, user_data));
            //no point merging anything into an already full stack
            if (Dm.eq(new_stack.amount, new_stack.max_amount))
                continue;
            //now we can merge since we are done with it
            foreach (obj_item_stack item_stack in Dm.each<obj_item_stack>(target))
            {
                if (item_stack == new_stack || !Dm.eq(item_stack.type, type_to_retrieve)) //don't merge with self or different type
                    continue;
                //speed merge
                float merge_amount = min(Dm.num(item_stack.amount), Dm.num(new_stack.max_amount) - Dm.num(new_stack.get_amount()));
                await item_stack.use(merge_amount);
                await new_stack.add(merge_amount);
                break;
            }
        }
        return count;
    }

    /**
     * Proc to get all the materials and dump them as sheets
     */
    // tg: code/datums/materials/material_container/material_container.dm:747
    public virtual async DmTask<float> retrieve_all(atom target = null, atom context = null)
    {
        context ??= parent;
        float result = 0;
        foreach (object MAT in Dm.each<object>(materials))
            result += await retrieve_stack(amount2sheet(materials[MAT]), (datum_material)MAT, target, context, user_data: ID_DATA(null));
        return result;
    }

    // tg: code/datums/materials/material_container/material_container.dm:755
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["SHEET_MATERIAL_AMOUNT"] = SHEET_MATERIAL_AMOUNT;
        return data;
    }

    /// List format is list(material_name = list(amount = ..., ref = ..., etc.))
    // tg: code/datums/materials/material_container/material_container.dm:761
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        foreach (datum_material material in Dm.each<datum_material>(materials))
        {
            object amount = materials[material];

            Dm.append(ref data, Dm.list(Dm.list(
                Dm.kv("name", material.name),
                Dm.kv("ref", REF(material)),
                Dm.kv("amount", amount),
                Dm.kv("color", material.greyscale_color ?? material.color))));
        }

        return data;
    }

    /**
     * Adds context sensitivy directly to the material container file for screentips
     */
    // tg: code/datums/materials/material_container/material_container.dm:784
    public virtual object on_requesting_context_from_item(datum source = null, DmList context = null, obj_item held_item = null, mob_living user = null)
    {
        if (held_item == null)
            return NONE;
        if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_ANY_INTENT)) && Dm.truthy(user.combat_mode))
            return NONE;
        if (Dm.truthy(Dm.bitand(held_item.item_flags, ABSTRACT)))
            return NONE;
        if (Dm.truthy(Dm.bitand(held_item.flags_1, HOLOGRAM_1)) || Dm.truthy(Dm.bitand(held_item.item_flags, NO_MAT_REDEMPTION)) || (allowed_item_typecache != null && !Dm.truthy(is_type_in_typecache(held_item, allowed_item_typecache))))
            return NONE;
        // SIGNAL_HANDLER: the item's composition is made without a wait
        DmList item_materials = Waiting.NoSleep(held_item.get_material_composition(mat_container_flags)) as DmList;
        if (!Dm.truthy(length(item_materials)))
            return NONE;
        foreach (object material in Dm.each<object>(item_materials))
        {
            if (can_hold_material(material as datum_material))
                continue;
            return NONE;
        }

        if (isstack(held_item))
            context[SCREENTIP_CONTEXT_LMB] = "Insert stack";
        context[SCREENTIP_CONTEXT_RMB] = "Insert";

        return CONTEXTUAL_SCREENTIP_SET;
    }
}

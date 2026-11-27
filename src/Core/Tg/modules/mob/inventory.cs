// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated whole for M4a (P2) but the procs that stay for their milestones (change_number_of_hands M10, the wizard's extra
// hands; the
// stripping and gibbing helpers gather_belongings, drop_everything, get_all_cells: M9, M6; get_all_gear came with
// the gaps package G1's loot panel, unequip_everything with the chapel's soulstone and the memento mori's dusting) and the stack merge of put_in_hands (stacks, M5).

// tg: code/modules/mob/inventory.dm:5
public partial class mob : atom_movable
{
    ///Returns the thing we're currently holding
    // tg: code/modules/mob/inventory.dm:5
    public virtual object get_active_held_item()
    {
        return get_item_for_held_index(active_hand_index);
    }


    //Finds the opposite limb for the active one (eg: upper left arm will find the item in upper right arm)
    //So we're treating each "pair" of limbs as a team, so "both" refers to them
    // tg: code/modules/mob/inventory.dm:11
    public virtual object get_inactive_held_item()
    {
        return get_item_for_held_index(get_inactive_hand_index());
    }


    //Finds the opposite index for the active one (eg: upper left arm will find the item in upper right arm)
    //So we're treating each "pair" of limbs as a team, so "both" refers to them
    // tg: code/modules/mob/inventory.dm:17
    public virtual object get_inactive_hand_index()
    {
        float other_hand = 0;
        if (IS_RIGHT_INDEX(active_hand_index))
            other_hand = active_hand_index - 1; //finding the matching "left" limb
        else
            other_hand = active_hand_index + 1; //finding the matching "right" limb
        if (other_hand < 0 || other_hand > held_items.len)
            other_hand = 0;
        return other_hand;
    }


    // tg: code/modules/mob/inventory.dm:28
    public virtual object get_item_for_held_index(object i = null)
    {
        if (Dm.gt(i, 0) && Dm.le(i, held_items.len))
            return held_items[i];
        return null;
    }

    //Odd = left. Even = right
    // tg: code/modules/mob/inventory.dm:35
    public virtual object held_index_to_dir(object i = null)
    {
        if (IS_RIGHT_INDEX(Dm.num(i)))
            return "r";
        return "l";
    }

    //Check we have an organ for this hand slot (Dismemberment), Only relevant for humans
    // tg: code/modules/mob/inventory.dm:41
    public virtual object has_hand_for_held_index(object i = null)
    {
        return TRUE;
    }


    //Check we have an organ for our active hand slot (Dismemberment),Only relevant for humans
    // tg: code/modules/mob/inventory.dm:46
    public virtual object has_active_hand()
    {
        return has_hand_for_held_index(active_hand_index);
    }

    /**
     * Returns a list of all dropped held items.
     * If none were dropped, returns an empty list.
     */
    // tg: code/modules/mob/inventory.dm:316
    public virtual async DmTask<object> drop_all_held_items()
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (obj_item I in Dm.each<obj_item>(held_items))
            Dm.combine(ref dot, await dropItemToGround(I));
        return dot;
    }

    //The following functions are the same save for one small difference

    /**
     * Used to drop an item (if it exists) to the ground.
     * * Will return null if the item wasn't dropped.
     * * If it was, returns the item.
     * If the item can be dropped, it will be forceMove()'d to the ground and the turf's Entered() will be called.
    */
    // tg: code/modules/mob/inventory.dm:349
    public virtual async DmTask<object> dropItemToGround(obj_item to_drop = null, float? force = null, float? silent = null, float? invdrop = null, params object[] __extra)
    {
        force ??= FALSE;
        silent ??= FALSE;
        invdrop ??= TRUE;
        if (to_drop == null)
            return null;

        float x_offset = 0;
        float y_offset = 0;
        if (!Dm.truthy(Dm.bitand(to_drop.item_flags, NO_PIXEL_RANDOM_DROP)))
        {
            x_offset += rand(-6, 6);
            y_offset += rand(-6, 6);
        }
        if (!Dm.truthy(await transfer_item_to_turf__loc(to_drop, drop_location() as atom, x_offset, y_offset, force, silent, invdrop)))
            return null;

        return to_drop;
    }

    // tg: code/modules/mob/inventory.dm:82
    public virtual object get_held_index_of_item(obj_item I = null)
    {
        return held_items.Find(I);
    }

    //Returns if a certain item can be equipped to a certain slot.
    // Currently invalid for two-handed items - call obj/item/mob_can_equip() instead.
    // tg: code/modules/mob/inventory.dm:164
    public virtual async DmTask<object> can_equip(obj_item I = null, object slot = null, float? disable_warning = null, float? bypass_equip_delay_self = null, float? ignore_equipped = null, float? indirect_action = null)
    {
        disable_warning ??= FALSE;
        bypass_equip_delay_self ??= FALSE;
        ignore_equipped ??= FALSE;
        indirect_action ??= FALSE;
        return FALSE;
    }

    /**
     * Used to return a list of equipped items on a mob; does not include held items (use get_all_gear)
     *
     * Argument(s):
     * * Optional - include_flags, (see obj.flags.dm) describes which optional things to include or not (pockets, accessories, held items)
     */
    // tg: code/modules/mob/inventory.dm:463
    public virtual object get_equipped_items(float? include_flags = null)
    {
        include_flags ??= NONE;
        DmList items = Dm.list();
        foreach (obj_item item_contents in Dm.each<obj_item>(contents))
        {
            if (Dm.truthy(Dm.bitand(item_contents.item_flags, IN_INVENTORY)))
            {
                if (!Dm.truthy(Dm.bitand(include_flags, INCLUDE_PROSTHETICS)) && HAS_TRAIT_FROM(item_contents, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT)) //prostetic limbs are not equipped items, they are part of the body.
                    continue;
                if (!Dm.truthy(Dm.bitand(include_flags, INCLUDE_ABSTRACT)) && Dm.truthy(Dm.bitand(item_contents.item_flags, ABSTRACT))) //not really flavoured as items
                    continue;
                Dm.append(ref items, item_contents);
            }
        }
        if (!Dm.truthy(Dm.bitand(include_flags, INCLUDE_HELD)))
            Dm.remove(ref items, held_items);

        return items;
    }

    ///Get all items in our possession that should affect our movespeed
    // tg: code/modules/mob/inventory.dm:478
    public virtual object get_equipped_speed_mod_items()
    {
        DmList dot = null;
        dot = get_equipped_items(Dm.bitor(INCLUDE_ABSTRACT, INCLUDE_PROSTHETICS)) as DmList;
        foreach (obj_item thing in Dm.each<obj_item>(held_items))
        {
            if (Dm.truthy(Dm.bitand(thing.item_flags, SLOWS_WHILE_IN_HAND)))
                Dm.append(ref dot, thing);
        }
        return dot;
    }

    /**
     * Equip an item to the slot of the mob; if it is not possible: the item is deleted if qdel_on_fail is set, otherwise
     * the mob is warned.
     *
     * set qdel_on_fail to have it delete W if it fails to equip
     *
     * set disable_warning to disable the 'you are unable to equip that' warning.
     *
     * unset redraw_mob to prevent the mob icons from being redrawn at the end.
     *
     * Initial is used to indicate whether or not this is the initial equipment (job datums etc) or just a player doing it
     *
     * set indirect_action to allow insertions into "soft" locked objects, things that are easily opened by the owning mob
     */
    // tg: code/modules/mob/inventory.dm:516
    public virtual async DmTask<object> equip_to_slot_if_possible(obj_item W = null, object slot = null, float? qdel_on_fail = null, float? disable_warning = null, float? redraw_mob = null, float? bypass_equip_delay_self = null, float? initial = null, float? indirect_action = null)
    {
        qdel_on_fail ??= FALSE;
        disable_warning ??= FALSE;
        redraw_mob ??= TRUE;
        bypass_equip_delay_self ??= FALSE;
        initial ??= FALSE;
        indirect_action ??= FALSE;
        if (!(W is obj_item) || QDELETED(W)) //This qdeleted is to prevent stupid behavior with things that qdel during init, like say stacks
            return FALSE;
        if (!Dm.truthy(await W.mob_can_equip(this as mob_living, slot, disable_warning, bypass_equip_delay_self, indirect_action: indirect_action)))
        {
            if (Dm.truthy(qdel_on_fail))
                qdel(W);
            else if (!Dm.truthy(disable_warning))
                to_chat(this, span_warning(I18n.T("You are unable to equip that!")));
            return FALSE;
        }
        await equip_to_slot(W, slot, initial, redraw_mob, indirect_action: indirect_action); //This proc should not ever fail.
        return TRUE;
    }

    /**
     * Actually equips an item to a slot (UNSAFE)
     *
     * This is an UNSAFE proc. It merely handles the actual job of equipping. All the checks on
     * whether you can or can't equip need to be done before! Use mob_can_equip() for that task.
     *
     *In most cases you will want to use equip_to_slot_if_possible()
     */
    // tg: code/modules/mob/inventory.dm:536
    public virtual async DmTask<object> equip_to_slot(obj_item equipping = null, object slot = null, float? initial = null, float? redraw_mob = null, float? indirect_action = null)
    {
        initial ??= FALSE;
        redraw_mob ??= FALSE;
        indirect_action ??= FALSE;
        return null;
    }

    /// This proc is called after an item has been successfully handled and equipped to a slot.
    // tg: code/modules/mob/inventory.dm:540
    [ShouldCallParent]
    public virtual async DmTask<object> has_equipped(obj_item item = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        object dot = null;
        Dm.combine(ref item.item_flags, IN_INVENTORY);
        dot = await item.on_equipped(this, slot, initial);
        if (Dm.truthy(dot))
            await update_equipment_speed_mods();
        return dot;
    }

    /**
     * Equip an item to the slot or delete
     *
     * This is just a commonly used configuration for the equip_to_slot_if_possible() proc, used to
     * equip people when the round starts and when events happen and such.
     *
     * Also bypasses equip delay checks, since the mob isn't actually putting it on.
     * Initial is used to indicate whether or not this is the initial equipment (job datums etc) or just a player doing it
     * set indirect_action to allow insertions into "soft" locked objects, things that are easily opened by the owning mob
     */
    // tg: code/modules/mob/inventory.dm:564
    public virtual async DmTask<object> equip_to_slot_or_del(obj_item W = null, object slot = null, float? initial = null, float? indirect_action = null)
    {
        initial ??= FALSE;
        indirect_action ??= FALSE;
        return await equip_to_slot_if_possible(W, slot, TRUE, TRUE, FALSE, TRUE, initial, indirect_action);
    }
}

// tg: code/modules/mob/inventory.dm:50
public partial class mob : atom_movable
{
    //Finds the first available (null) index OR all available (null) indexes in held_items based on a side.
    //Lefts: 1, 3, 5, 7...
    //Rights:2, 4, 6, 8...
    // tg: code/modules/mob/inventory.dm:53
    public virtual object get_empty_held_index_for_side(float? side = null, float? all = null)
    {
        side ??= LEFT_HANDS;
        all ??= FALSE;
        DmList empty_indexes = Dm.truthy(all) ? Dm.list() : null;
        for (float i = Dm.eq(side, LEFT_HANDS) ? 1 : 2, i__end = held_items.len; i <= i__end; i += 2)
        {
            if (!Dm.truthy(held_items[i]))
            {
                if (!Dm.truthy(all))
                    return i;
                Dm.append(ref empty_indexes, i);
            }
        }
        return empty_indexes;
    }


    //Same as the above, but returns the first or ALL held *ITEMS* for the side
    // tg: code/modules/mob/inventory.dm:64
    public virtual object get_held_items_for_side(float? side = null, float? all = null)
    {
        side ??= LEFT_HANDS;
        all ??= FALSE;
        DmList holding_items = Dm.truthy(all) ? Dm.list() : null;
        for (float i = Dm.eq(side, LEFT_HANDS) ? 1 : 2, i__end = held_items.len; i <= i__end; i += 2)
        {
            obj_item I = held_items[i] as obj_item;
            if (Dm.truthy(I))
            {
                if (!Dm.truthy(all))
                    return I;
                Dm.append(ref holding_items, I);
            }
        }
        return holding_items;
    }


    // tg: code/modules/mob/inventory.dm:75
    public virtual object get_empty_held_indexes()
    {
        DmList L = null;
        for (float i = 1, i__end = held_items.len; i <= i__end; i++)
        {
            if (!Dm.truthy(held_items[i]))
                LAZYADD(ref L, i);
        }
        return L;
    }

    /// Returns what body zone is holding the passed item
    // tg: code/modules/mob/inventory.dm:86
    public virtual object get_hand_zone_of_item(obj_item I = null)
    {
        object hand_index = get_held_index_of_item(I);
        if (!Dm.truthy(hand_index))
            return null;
        if (IS_RIGHT_INDEX(Dm.num(hand_index)))
            return BODY_ZONE_R_ARM;
        return BODY_ZONE_L_ARM;
    }

    ///Find number of held items, multihand compatible
    // tg: code/modules/mob/inventory.dm:95
    public virtual object get_num_held_items()
    {
        float dot = 0;
        for (float i = 1, i__end = held_items.len; i <= i__end; i++)
        {
            if (!Dm.truthy(held_items[i]))
                continue;
            obj_item gripped_item = held_items[i] as obj_item;
            if (HAS_TRAIT_FROM(gripped_item, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT)) //prostetic limbs are not held items, they are part of the body.
                continue;
            if (Dm.truthy(Dm.bitand(gripped_item.item_flags, ABSTRACT))) //not really flavoured as items
                continue;
            dot++;
        }
        return dot;
    }

    //Sad that this will cause some overhead, but the alias seems necessary
    //*I* may be happy with a million and one references to "indexes" but others won't be
    // tg: code/modules/mob/inventory.dm:109
    public virtual object is_holding(obj_item I = null)
    {
        return get_held_index_of_item(I);
    }


    //Checks if we're holding an item of type: typepath
    // tg: code/modules/mob/inventory.dm:114
    public virtual object is_holding_item_of_type(object typepath = null)
    {
        foreach (obj_item I in Dm.each<obj_item>(held_items))
        {
            if (istype(I, typepath as Type))
                return I;
        }
        return FALSE;
    }

    // List version of above proc
    // Returns ret_item, which is either the successfully located item or null
    // tg: code/modules/mob/inventory.dm:122
    public virtual object is_holding_item_of_types(DmList typepaths = null)
    {
        foreach (object typepath in Dm.each(typepaths))
        {
            object ret_item = is_holding_item_of_type(typepath);
            return ret_item;
        }
        return null;
    }

    //Checks if we're holding a tool that has given quality
    //Returns the tool that has the best version of this quality
    // tg: code/modules/mob/inventory.dm:129
    public virtual object is_holding_tool_quality(object quality = null)
    {
        obj_item best_item = null;
        float best_quality = INFINITY;

        foreach (obj_item I in Dm.each<obj_item>(held_items))
        {
            if (Dm.eq(I.tool_behaviour, quality) && I.toolspeed < best_quality)
            {
                best_item = I;
                best_quality = I.toolspeed;
            }
        }

        return best_item;
    }


    //To appropriately fluff things like "they are holding [I] in their [get_held_index_name(get_held_index_of_item(I))]"
    //Can be overridden to pass off the fluff to something else (eg: science allowing people to add extra robotic limbs, and having this proc react to that
    // with say "they are holding [I] in their Nanotrasen Brand Utility Arm - Right Edition" or w/e
    // tg: code/modules/mob/inventory.dm:144
    public virtual object get_held_index_name(object i = null)
    {
        DmList hand = Dm.list();
        if (Dm.gt(i, 2))
            Dm.append(ref hand, "upper ");
        float num = 0;
        if (IS_RIGHT_INDEX(Dm.num(i)))
        {
            num = Dm.num(i) - 2;
            Dm.append(ref hand, "right hand");
        }
        else
        {
            num = Dm.num(i) - 1;
            Dm.append(ref hand, "left hand");
        }
        num -= (num * 0.5f);
        if (num > 1) //"upper left hand #1" seems weird, but "upper left hand #2" is A-ok
            Dm.append(ref hand, Dm.text(" #[]", num));
        return hand.Join(""); // BYOND's Join() with no glue joins with nothing
    }

    // tg: code/modules/mob/inventory.dm:167
    public virtual object can_put_in_hand(object I = null, object hand_index = null)
    {
        if (Dm.gt(hand_index, held_items.len))
            return FALSE;
        if (!Dm.truthy(put_in_hand_check(I as obj_item)))
            return FALSE;
        if (!Dm.truthy(has_hand_for_held_index(hand_index)))
            return FALSE;
        return Dm.not(held_items[hand_index]);
    }

    // tg: code/modules/mob/inventory.dm:176
    public virtual async DmTask<object> put_in_hand(obj_item I = null, object hand_index = null, float? forced = null, float? ignore_anim = null, float? visuals_only = null)
    {
        forced ??= FALSE;
        ignore_anim ??= TRUE;
        visuals_only ??= FALSE;
        if (hand_index == null || held_items.len == 0 || (!Dm.truthy(forced) && !Dm.truthy(can_put_in_hand(I, hand_index))))
            return FALSE;

        if (isturf(I.loc) && !Dm.truthy(ignore_anim))
            I.do_pickup_animation(this);
        if (Dm.truthy(get_item_for_held_index(hand_index)))
            await dropItemToGround(get_item_for_held_index(hand_index) as obj_item, force: TRUE);
        await I.forceMove(this); //this has to come before has_equipped() is called
        held_items[hand_index] = I;
        SET_PLANE_EXPLICIT(I, ABOVE_HUD_PLANE, this);
        if (Dm.truthy(I.pulledby))
            await I.pulledby.stop_pulling();
        if (!Dm.truthy(await has_equipped(I, ITEM_SLOT_HANDS, initial: visuals_only)))
            return FALSE;
        await update_held_items();
        I.pixel_x = I.base_pixel_x;
        I.pixel_y = I.base_pixel_y;
        if (QDELETED(I)) // this is here because some ABSTRACT items like slappers and circle hands could be moved from hand to hand then delete, which meant you'd have a null in your hand until you cleared it (say, by dropping it)
        {
            held_items[hand_index] = null;
            return FALSE;
        }
        if (Dm.truthy(I._listen_lookup?[COMSIG_ITEM_ENTERED_HANDS]))
            I._SendSignal(COMSIG_ITEM_ENTERED_HANDS, Dm.list(I, this, hand_index));
        return hand_index;
    }

    //Puts the item into the first available left hand if possible and calls all necessary triggers/updates. returns 1 on success.
    // tg: code/modules/mob/inventory.dm:201
    public virtual async DmTask<object> put_in_l_hand(obj_item I = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        return await put_in_hand(I, get_empty_held_index_for_side(LEFT_HANDS), visuals_only: visuals_only);
    }

    //Puts the item into the first available right hand if possible and calls all necessary triggers/updates. returns 1 on success.
    // tg: code/modules/mob/inventory.dm:205
    public virtual async DmTask<object> put_in_r_hand(obj_item I = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        return await put_in_hand(I, get_empty_held_index_for_side(RIGHT_HANDS), visuals_only: visuals_only);
    }

    // tg: code/modules/mob/inventory.dm:208
    public virtual object put_in_hand_check(obj_item I = null)
    {
        return FALSE; //nonliving mobs don't have hands
    }

    //Puts the item into our active hand if possible. returns TRUE on success.
    // tg: code/modules/mob/inventory.dm:218
    public virtual async DmTask<object> put_in_active_hand(obj_item I = null, float? forced = null, float? ignore_animation = null, float? visuals_only = null)
    {
        forced ??= FALSE;
        ignore_animation ??= TRUE;
        visuals_only ??= FALSE;
        return await put_in_hand(I, active_hand_index, forced, ignore_animation, visuals_only);
    }


    //Puts the item into our inactive hand if possible, returns TRUE on success
    // tg: code/modules/mob/inventory.dm:223
    public virtual async DmTask<object> put_in_inactive_hand(obj_item I = null, float? forced = null, float? visuals_only = null)
    {
        forced ??= FALSE;
        visuals_only ??= FALSE;
        return await put_in_hand(I, get_inactive_hand_index(), forced, visuals_only: visuals_only);
    }


    //Puts the item our active hand if possible. Failing that it tries other hands. Returns TRUE on success.
    //If both fail it drops it on the floor (or nearby tables if germ sensitive) and returns FALSE.
    //This is probably the main one you need to know :)
    // tg: code/modules/mob/inventory.dm:230
    public virtual async DmTask<object> put_in_hands(obj_item I = null, float? del_on_fail = null, float? merge_stacks = null, float? forced = null, float? ignore_animation = null, float? visuals_only = null)
    {
        del_on_fail ??= FALSE;
        merge_stacks ??= TRUE;
        forced ??= FALSE;
        ignore_animation ??= TRUE;
        visuals_only ??= FALSE;
        // DEVIATION(DV27): the interaction guard (D132, interaction.md 7, P2's site): an item whose own pickup, equipped or
        // dropped code is not translated is not put in a hand at all. It ends where tg's put_in_hands leaves an item no hand takes:
        // deleted on del_on_fail, else at the drop location (:263-305), without its own dropped (the owner's play test, 2026-10-16: cable restraints
        // made from a coil, new(null) then put_in_hands, were lost in nullspace with the 15 cable spent).
        if (InertSeam.MissingOverride(I, "pickup", this) || InertSeam.MissingOverride(I, "equipped", this) || InertSeam.MissingOverride(I, "dropped", this))
        {
            if (QDELETED(I))
                return FALSE;
            if (Dm.truthy(del_on_fail))
                qdel(I);
            else if (I.loc == null || I.loc == this)
                await I.forceMove(drop_location() as atom);
            return FALSE;
        }
        if (QDELETED(I))
            return FALSE;

        // If the item is a stack and we're already holding a stack then merge
        if (isstack(I))
        {
            obj_item_stack item_stack = I as obj_item_stack;
            obj_item_stack active_stack = get_active_held_item() as obj_item_stack;

            if (Dm.truthy(item_stack.is_zero_amount(delete_if_zero: TRUE)))
                return FALSE;

            if (Dm.truthy(merge_stacks))
            {
                if (istype(active_stack, typeof(obj_item_stack)) && Dm.truthy(active_stack.can_merge(item_stack, inhand: TRUE)))
                {
                    if (Dm.truthy(await item_stack.merge(active_stack)))
                    {
                        to_chat(usr, span_notice(I18n.T("Your [] stack now contains [] []\\s.", active_stack.name, active_stack.get_amount(), active_stack.singular_name)));
                        return TRUE;
                    }
                }
                else
                {
                    obj_item_stack inactive_stack = get_inactive_held_item() as obj_item_stack;
                    if (istype(inactive_stack, typeof(obj_item_stack)) && Dm.truthy(inactive_stack.can_merge(item_stack, inhand: TRUE)))
                    {
                        if (Dm.truthy(await item_stack.merge(inactive_stack)))
                        {
                            to_chat(usr, span_notice(I18n.T("Your [] stack now contains [] []\\s.", inactive_stack.name, inactive_stack.get_amount(), inactive_stack.singular_name)));
                            return TRUE;
                        }
                    }
                }
            }
        }

        if (Dm.truthy(await put_in_active_hand(I, forced, ignore_animation, visuals_only)))
            return TRUE;

        object hand = get_empty_held_index_for_side(LEFT_HANDS);
        if (!Dm.truthy(hand))
            hand = get_empty_held_index_for_side(RIGHT_HANDS);
        if (Dm.truthy(hand))
        {
            if (Dm.truthy(await put_in_hand(I, hand, forced, ignore_animation, visuals_only)))
                return TRUE;
        }
        if (Dm.truthy(del_on_fail))
        {
            qdel(I);
            return FALSE;
        }

        // Failed to put in hands - drop the item
        atom location = drop_location() as atom;

        // Try dropping on nearby tables if germ sensitive (except table behind you)
        if (HAS_TRAIT(I, TRAIT_GERM_SENSITIVE))
        {
            DmList dirs = Dm.list( // All dirs in clockwise order
                NORTH,
                NORTHEAST,
                EAST,
                SOUTHEAST,
                SOUTH,
                SOUTHWEST,
                WEST,
                NORTHWEST
            );
            float dir_count = dirs.len;
            float facing_dir_index = dirs.Find(dir);
            float cw_index = facing_dir_index;
            float ccw_index = facing_dir_index;
            DmList turfs_ordered = Dm.list(get_step(this, dir));

            // Build ordered list of turfs starting from the front facing
            for (float i = 1, i__end = ROUND_UP(dir_count / 2) - 1; i <= i__end; i++)
            {
                cw_index++;
                if (cw_index > dir_count)
                    cw_index = 1;
                Dm.append(ref turfs_ordered, get_step(this, Dm.num(dirs[cw_index]))); // Add next tile on your right
                ccw_index--;
                if (ccw_index <= 0)
                    ccw_index = dir_count;
                Dm.append(ref turfs_ordered, get_step(this, Dm.num(dirs[ccw_index]))); // Add next tile on your left
            }

            // Check tables on these turfs
            foreach (object turf in Dm.each(turfs_ordered))
            {
                // DM parse (rules 4.11): locate(table) in (turf || (locate(rack) in (turf || (locate(icecream_vat) in
                // turf)))); a turf is true, so only tables are looked for.
                if (Dm.truthy(Dm.locate<obj_structure_table>(Dm.truthy(turf) ? turf : (object)Dm.locate<obj_structure_rack>(Dm.truthy(turf) ? turf : (object)Dm.locate<obj_machinery_icecream_vat>(turf)))))
                {
                    location = turf as atom;
                    break;
                }
            }
        }

        await transferItemToLoc(I, location, force: TRUE, silent: TRUE, animated: Dm.not(ignore_animation));
        return FALSE;
    }

    /// Returns true if a mob is holding something
    // tg: code/modules/mob/inventory.dm:309
    public virtual object is_holding_items()
    {
        return Dm.not(Dm.not(Dm.locate<obj_item>(held_items)));
    }

    // tg: code/modules/mob/inventory.dm:323
    public virtual object canUnEquip(obj_item I = null, object force = null)
    {
        if (!Dm.truthy(I))
            return TRUE;
        if (HAS_TRAIT(I, TRAIT_NODROP) && !Dm.truthy(force))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/inventory.dm:330
    public virtual async DmTask<object> putItemFromInventoryInHandIfPossible(obj_item I = null, object hand_index = null, float? force_removal = null)
    {
        force_removal ??= FALSE;
        if (!Dm.truthy(can_put_in_hand(I, hand_index)))
            return FALSE;
        if (!Dm.truthy(await temporarilyRemoveItemFromInventory(I, force_removal)))
            return FALSE;
        await I.remove_item_from_storage(this);
        if (!Dm.truthy(await put_in_hand(I, hand_index)))
        {
            qdel(I);
            throw Dm.CRASH("Assertion failure: putItemFromInventoryInHandIfPossible", "code/modules/mob/inventory.dm", 339); //should never be possible
        }
        return TRUE;
    }

    /// Unequips and transfers an item to a given turf, if possible.
    // tg: code/modules/mob/inventory.dm:364
    public virtual async DmTask<object> transfer_item_to_turf(obj_item to_transfer = null, turf new_loc = null, float? x_offset = null, float? y_offset = null, float? force = null, float? silent = null, float? drop_item_inventory = null)
    {
        return await transfer_item_to_turf__loc(to_transfer, new_loc, x_offset, y_offset, force, silent, drop_item_inventory);
    }

    // The body of transfer_item_to_turf (inventory.dm:364-381). GAP(lead): slots.tsv types new_loc as turf (tg's
    // declaration), but tg passes drop_location(), which is the container when the mob is inside one (a closet,
    // dropItemToGround:357, closets.dm:798): the frozen turf parameter would turn it into null and the item would go to
    // nullspace. The callers that pass drop_location() (dropItemToGround) call this body with the atom, as DM does.
    private async DmTask<object> transfer_item_to_turf__loc(obj_item to_transfer, atom new_loc, float? x_offset, float? y_offset, float? force, float? silent, float? drop_item_inventory)
    {
        x_offset ??= 0;
        y_offset ??= 0;
        force ??= FALSE;
        silent ??= FALSE;
        drop_item_inventory ??= TRUE;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_DROPPING_ITEM]))
            this._SendSignal(COMSIG_MOB_DROPPING_ITEM, Dm.list(this));
        if (!Dm.truthy(await doUnEquip(to_transfer, force, new_loc, no_move: FALSE, invdrop: drop_item_inventory, silent: silent)))
            return FALSE;
        if (QDELETED(to_transfer)) // Some items may get deleted upon getting unequipped.
            return FALSE;
        to_transfer.pixel_x = to_transfer.base_pixel_x + Dm.num(x_offset);
        to_transfer.pixel_y = to_transfer.base_pixel_y + Dm.num(y_offset);
        to_transfer.do_drop_animation(this);
        return TRUE;
    }

    //for when the item will be immediately placed in a loc other than the ground
    // tg: code/modules/mob/inventory.dm:384
    public virtual async DmTask<object> transferItemToLoc(obj_item I = null, object newloc = null, float? force = null, float? silent = null, object animated = null)
    {
        force ??= FALSE;
        silent ??= TRUE;
        object dot = null;
        dot = await doUnEquip(I, force, newloc as atom, FALSE, silent: silent);
        if (!Dm.truthy(dot))
            return dot;
        // This proc wears a lot of hats for moving items around in different ways,
        // so we assume unhandled cases for checking to animate can safely be handled
        // with the same logic we handle animating putting items in container (container on your person isn't animated)
        if (animated == null)
        {
            // If the item's ultimate location is us, we don't animate putting it wherever
            animated = Dm.not(get__inventory(newloc as atom, typeof(mob)) == this);
        }
        if (Dm.truthy(animated))
            I.do_pickup_animation(newloc as atom, get_turf(this)); // GAP(lead): slots.tsv types source as turf; tg passes src (the mob), whose turf is the same start point
        return dot;
    }

    // GAP(lead): tg's global proc get(atom/loc, type) (__HELPERS/atoms.dm:312) has no GlobalProcs member yet; its body,
    // used by transferItemToLoc:395 only.
    // tg: code/__HELPERS/atoms.dm:312
    private static atom get__inventory(atom loc, Type type)
    {
        while (Dm.truthy(loc))
        {
            if (Dm.istype(loc, type))
                return loc;
            loc = loc.loc;
        }
        return null;
    }

    //visibly unequips I but it is NOT MOVED AND REMAINS IN SRC, newloc is for signal handling checks only which hints where you want to move the object after removal
    //item MUST BE FORCEMOVE'D OR QDEL'D

    // tg: code/modules/mob/inventory.dm:400
    public virtual async DmTask<object> temporarilyRemoveItemFromInventory(obj_item item_dropping = null, float? force = null, float? idrop = null, atom newloc = null)
    {
        force ??= FALSE;
        idrop ??= TRUE;
        newloc ??= this;
        return await doUnEquip(item_dropping, force, newloc, TRUE, idrop, silent: TRUE);
    }

    /**
     * ## doUnEquip
     * First and most importantly, DO NOT CALL THIS PROC
     * Use one of the above 4 helper procs instead!!
     * you may override it, but do not modify the args.
     * Returns TRUE if it managed to unequip, FALSE if it can't.
     * Args:
     * item_dropping - The item that we're unequipping.
     * force - overrides TRAIT_NODROP for things like wizarditis and admin undress.
     * newloc - The location we're dropping the item into, this could be a turf, storage, mob, etc.
     * no_move - if the item is just gonna be immediately moved afterward
     * invdrop - Arg passed to signals, prevents stuff in pockets dropping. Only set to false if it's going to immediately be replaced
     * silent - Arg passed to dropped() and signals, muting things like drop sound.
     */
    // tg: code/modules/mob/inventory.dm:417
    [DmProtected]
    public virtual async DmTask<object> doUnEquip(obj_item item_dropping = null, object force = null, atom newloc = null, object no_move = null, float? invdrop = null, float? silent = null)
    {
        invdrop ??= TRUE;
        silent ??= FALSE;
        if (!Dm.truthy(item_dropping)) //If there's nothing to drop, the drop is automatically successful. If(unEquip) should generally be used to check for TRAIT_NODROP.
            return TRUE;

        if (HAS_TRAIT(item_dropping, TRAIT_NODROP) && !Dm.truthy(force))
            return FALSE;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(item_dropping._listen_lookup?[COMSIG_ITEM_PRE_UNEQUIP]) ? NONE : item_dropping._SendSignal(COMSIG_ITEM_PRE_UNEQUIP, Dm.list(item_dropping, force, newloc, no_move, invdrop, silent))), COMPONENT_ITEM_BLOCK_UNEQUIP)) && !Dm.truthy(force))
            return FALSE;

        object hand_index = get_held_index_of_item(item_dropping);
        if (Dm.truthy(hand_index))
        {
            held_items[hand_index] = null;
            await update_held_items();
        }

        if (!Dm.truthy(item_dropping))
            return FALSE;

        if (client != null)
            Dm.remove(client.screen, item_dropping);

        foreach (mob_dead observe in Dm.each_cast<mob_dead>(observers))
        {
            if (observe.client != null)
                Dm.remove(observe.client.screen, item_dropping);
        }

        item_dropping.layer = Dm.initial(item_dropping, (obj_item p) => p.layer);
        SET_PLANE_EXPLICIT(item_dropping, Dm.initial(item_dropping, (obj_item p) => p.plane), newloc);
        item_dropping.appearance_flags = Dm.bitand(item_dropping.appearance_flags, Dm.bitnot(NO_CLIENT_COLOR)); // appearance_flags is an engine property: no ref (rules 4.3)
        Dm.mask(ref item_dropping.item_flags, Dm.bitnot(IN_INVENTORY)); //This has to come before MoveToNullspace/forceMove is called
        if (!Dm.truthy(no_move) && !Dm.truthy(Dm.bitand(item_dropping.item_flags, DROPDEL))) //item may be moved/qdel'd immedietely, don't bother moving it
        {
            if (newloc == null)
                await item_dropping.moveToNullspace();
            else
                await item_dropping.forceMove(newloc);
        }

        await has_unequipped(item_dropping, silent);
        if (Dm.truthy(item_dropping._listen_lookup?[COMSIG_ITEM_POST_UNEQUIP]))
            item_dropping._SendSignal(COMSIG_ITEM_POST_UNEQUIP, Dm.list(item_dropping, force, newloc, no_move, invdrop, silent, hand_index));
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_UNEQUIPPED_ITEM]))
            this._SendSignal(COMSIG_MOB_UNEQUIPPED_ITEM, Dm.list(this, item_dropping, force, newloc, no_move, invdrop, silent, hand_index));
        return TRUE;
    }
}

// tg: code/modules/mob/inventory.dm:212
public partial class mob_living : mob
{
    // tg: code/modules/mob/inventory.dm:212
    public override object put_in_hand_check(obj_item I = null)
    {
        if ((I is obj_item) && (Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_PICKUP)) || Dm.truthy(Dm.bitand(I.item_flags, ABSTRACT)))
            && !Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_LIVING_TRY_PUT_IN_HAND]) ? NONE : this._SendSignal(COMSIG_LIVING_TRY_PUT_IN_HAND, Dm.list(this, I))), COMPONENT_LIVING_CANT_PUT_IN_HAND)))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/inventory.dm:486
    public virtual async DmTask<object> unequip_everything()
    {
        DmList items = Dm.list();
        Dm.combine(ref items, get_equipped_items(INCLUDE_POCKETS));
        // In case something isn't actually unequipped somehow
        DmList dropped_items = Dm.list();
        foreach (object I in Dm.each(items))
        {
            object return_val = await dropItemToGround(I as obj_item);
            if (!isitem(return_val))
                continue;
            Dm.combine(ref dropped_items, return_val);
        }
        object held = await drop_all_held_items();
        if (islist(held))
            Dm.combine(ref dropped_items, held);
        return dropped_items;
    }
}

// tg: code/modules/mob/inventory.dm:548
public partial class mob : atom_movable
{
    /// This proc is called after an item has been removed from a mob but before it has been officially deslotted.
    // tg: code/modules/mob/inventory.dm:548
    [ShouldCallParent]
    public virtual async DmTask<object> has_unequipped(obj_item item = null, float? silent = null)
    {
        silent ??= FALSE;
        await item.dropped(this, silent);
        await update_equipment_speed_mods();
        return TRUE;
    }

    /**
     * Auto equip the passed in item the appropriate slot based on equipment priority
     *
     * puts the item "W" into an appropriate slot in a human's inventory
     *
     * returns 0 if it cannot, 1 if successful
     */
    // tg: code/modules/mob/inventory.dm:574
    public virtual async DmTask<object> equip_to_appropriate_slot(obj_item W = null, float? qdel_on_fail = null, float? indirect_action = null)
    {
        qdel_on_fail ??= FALSE;
        indirect_action ??= FALSE;
        if (!(W is obj_item))
            return FALSE;
        DmList slot_priority = W.slot_equipment_priority;

        if (!Dm.truthy(slot_priority))
        {
            slot_priority = Dm.list(
                ITEM_SLOT_BACK, ITEM_SLOT_ID,
                ITEM_SLOT_ICLOTHING, ITEM_SLOT_OCLOTHING,
                ITEM_SLOT_MASK, ITEM_SLOT_HEAD, ITEM_SLOT_NECK,
                ITEM_SLOT_FEET, ITEM_SLOT_GLOVES,
                ITEM_SLOT_EARS, ITEM_SLOT_EYES,
                ITEM_SLOT_BELT, ITEM_SLOT_SUITSTORE,
                ITEM_SLOT_LPOCKET, ITEM_SLOT_RPOCKET,
                ITEM_SLOT_DEX_STORAGE
            );
        }

        foreach (object slot in Dm.each(slot_priority))
        {
            if (Dm.truthy(await equip_to_slot_if_possible(W, slot, disable_warning: TRUE, redraw_mob: TRUE, indirect_action: indirect_action)))
                return TRUE;
        }

        if (Dm.truthy(qdel_on_fail))
            qdel(W);
        return FALSE;
    }
}

// tg: code/modules/mob/inventory.dm:600
public partial class obj_item : obj
{
    /// Tries to equip an item, store it in open storage, or in next best storage
    // tg: code/modules/mob/inventory.dm:600
    public virtual async DmTask<object> equip_to_best_slot(mob user = null)
    {
        if (Dm.truthy(await user.equip_to_appropriate_slot(this)))
        {
            await user.update_held_items();
            return TRUE;
        }
        else
        {
            if (Dm.truthy(equip_delay_self))
                return null;
        }

        if (Dm.truthy(user.active_storage?.attempt_insert(this, user, messages: FALSE)))
            return TRUE;

        // tg's var/static/list/equip_priorities: a constant list that nothing writes, built per call (D31: no static
        // mutable state in the core); the result is the same.
        DmList equip_priorities = Dm.list(
            ITEM_SLOT_BELT,
            ITEM_SLOT_BACK,
            ITEM_SLOT_DEX_STORAGE,
            ITEM_SLOT_OCLOTHING,
            ITEM_SLOT_ICLOTHING
        );

        DmList possible_storages = user.held_items.Copy();
        obj_item active_held = user.get_active_held_item() as obj_item;
        Dm.remove(ref possible_storages, active_held);
        if (active_held != this)
        {
            // If something else is equipping us, just in case, do it into the held item
            possible_storages.Insert(1, active_held);
        }

        foreach (object slot in Dm.each(equip_priorities))
            Dm.append(ref possible_storages, user.get_item_by_slot(slot));

        foreach (obj_item gear in Dm.each_cast<obj_item>(possible_storages))
        {
            if (Dm.truthy(gear?.atom_storage?.attempt_insert(this, user, messages: FALSE)))
                return TRUE;
        }

        to_chat(user, span_warning(I18n.T("You are unable to equip that!")));
        return FALSE;
    }
}

// tg: code/modules/mob/inventory.dm:639
public partial class mob : atom_movable
{
    /// Attempts to put an item into storage located in a given slot
    /// indirect_action - ignore "soft-locked" storages that can be easily opened
    /// del_on_fail - delete the item upon failure
    // tg: code/modules/mob/inventory.dm:639
    public virtual object equip_to_storage(obj_item item = null, object slot = null, float? indirect_action = null, float? del_on_fail = null, float? initial = null)
    {
        indirect_action ??= FALSE;
        del_on_fail ??= FALSE;
        initial ??= FALSE;
        obj_item worn_item = get_item_by_slot(slot) as obj_item;
        if (Dm.truthy(worn_item?.atom_storage?.attempt_insert(item, this, @override: TRUE, force: Dm.truthy(indirect_action) ? STORAGE_SOFT_LOCKED : STORAGE_NOT_LOCKED, messages: FALSE)))
            return TRUE;

        if (Dm.truthy(del_on_fail))
            qdel(item);
        return FALSE;
    }

    // tg: code/modules/mob/inventory.dm:648
    // GAME_VERB_HIDDEN(/mob, quick_equip, "quick-equip") expands (__DEFINES/verb.dm:66, :12-31) to the type
    // /datum/verb_metadata/mob/quick_equip (the verb registry, not declared until the command layer, M9-M10), the verb
    // below, which calls __gvb_quick_equip(arglist(args)), and the proc __gvb_quick_equip with the body (rules 7.10: the
    // verb settings leave no C#). tg's own code calls the verb as a proc (keybinding/human.dm:17).
    public virtual void quick_equip(params object[] __extra)
    {
        DmList args = Dm.args(__extra);
        Waiting.NoSleep(Dm.Call(this, nameof(__gvb_quick_equip), Dm.arglist(args)));
    }

    // tg: code/modules/mob/inventory.dm:648 (the body of quick_equip, :650)
    public virtual void __gvb_quick_equip(DmList structured_args = null)
    {
        // DEFAULT_QUEUE_OR_CALL_VERB(VERB_CALLBACK(src, PROC_REF(execute_quick_equip))) (__DEFINES/verb_manager.dm:32,
        // :25-28): the macro builds its callback argument twice, once to queue and once to call.
        if (!Dm.truthy(_queue_verb(Dm.@new<datum_callback_verb_callback>(this, nameof(execute_quick_equip)), VERB_DEFAULT_QUEUE_THRESHOLD, null)))
            Dm.@new<datum_callback_verb_callback>(this, nameof(execute_quick_equip)).InvokeAsync();
    }

    ///proc extender of [/mob/verb/quick_equip] used to make the verb queuable if the server is overloaded
    // tg: code/modules/mob/inventory.dm:653
    public virtual async DmTask execute_quick_equip()
    {
        obj_item I = get_active_held_item() as obj_item;
        if (!Dm.truthy(I))
        {
            to_chat(this, span_warning(I18n.T("You are not holding anything to equip!")));
            return;
        }
        if (!QDELETED(I))
            await I.equip_to_best_slot(this);
    }

    //used in code for items usable by both carbon and drones, this gives the proper back slot for each mob.(defibrillator, backpack watertank, ...)
    // tg: code/modules/mob/inventory.dm:662
    public virtual object getBackSlot()
    {
        return ITEM_SLOT_BACK;
    }

    // tg: code/modules/mob/inventory.dm:671
    public virtual async DmTask change_number_of_hands(object amt = null)
    {
        Dm.NotTranslated("code/modules/mob/inventory.dm:671", "change_number_of_hands: extra hands (the wizard's perk and the deathmatch modifier, M10+)");
    }
}

// tg: code/modules/mob/inventory.dm:686
public partial class mob_living : mob
{
    //GetAllContents that is reasonable and not stupid
    // tg: code/modules/mob/inventory.dm:686  (gaps package G1: the loot panel's check of what the player carries)
    public virtual object get_all_gear(float? equipment_flags = null, float? recursive = null)
    {
        equipment_flags ??= Dm.bitor(INCLUDE_ACCESSORIES, INCLUDE_PROSTHETICS);
        recursive ??= TRUE;
        DmList processing_list = get_equipped_items(Dm.bitor(Dm.bitor(INCLUDE_POCKETS, INCLUDE_HELD), equipment_flags.Value)) as DmList;
        list_clear_nulls(processing_list); // handles empty hands
        float i = 0;
        while (i < length(processing_list))
        {
            atom A = processing_list[++i] as atom;
            if (A.atom_storage != null && Dm.truthy(recursive))
                Dm.append(processing_list, A.atom_storage.return_inv());
        }
        return processing_list;
    }

    // tg: code/modules/mob/inventory.dm:697
    // syndicate X1a (D316): the contractor's extraction strips its victim with it
    public virtual object gather_belongings(float? equipment_flags = null, float? recursive = null)
    {
        equipment_flags ??= Dm.bitor(INCLUDE_ACCESSORIES, INCLUDE_PROSTHETICS);
        recursive ??= TRUE;
        DmList belongings = get_all_gear(equipment_flags, recursive) as DmList;
        foreach (obj_item_implant_storage internal_bag in Dm.each<obj_item_implant_storage>(implants))
            Dm.append(ref belongings, internal_bag.contents);
        return belongings;
    }

    // tg: code/modules/mob/inventory.dm:704
    public virtual async DmTask<object> drop_everything(object del_on_drop = null, object force = null, object del_if_nodrop = null)
    {
        Dm.NotTranslated("code/modules/mob/inventory.dm:704", "drop_everything: everything a mob carries (stripping and gibbing, M10+ and M6)");
        return default;
    }

    // tg: code/modules/mob/inventory.dm:727
    public virtual object get_all_cells(float? max_percent = null)
    {
        Dm.NotTranslated("code/modules/mob/inventory.dm:727", "get_all_cells: the power cells a mob carries (power, M6)");
        return default;
    }
}

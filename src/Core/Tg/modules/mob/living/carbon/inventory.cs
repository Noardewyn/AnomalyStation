// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the M4a inventory procs translated (M4a P2: the slots, dropping, coverage) and the internals and air tanks
// (open_internals, try_open_internals, close_internals, close_externals, close_all_airtanks and the toggles: M4c slice 1);
// equip_conspicuous_item since syndicate X4 (the blood brother's flash).

// tg: code/modules/mob/living/carbon/inventory.dm:220
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/inventory.dm:220
    public virtual object can_breathe_tube()
    {
        return get_organ_slot(ORGAN_SLOT_BREATHING_TUBE);
    }

    /// Returns the object that allows us to breathe internals - tube implant, mask or helmet
    // tg: code/modules/mob/living/carbon/inventory.dm:224
    public virtual object can_breathe_internals()
    {
        return can_breathe_tube();
    }

    /// Returns truthy if air tank is open and mob lacks apparatus, or if the tank moved away from the mob.
    // tg: code/modules/mob/living/carbon/inventory.dm:228
    public virtual object invalid_internals()
    {
        return Dm.num((@internal != null || external != null) && (!Dm.truthy(can_breathe_internals()) || (@internal != null && @internal.loc != this)));
    }
}

// tg: code/modules/mob/living/carbon/inventory.dm:291 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    /// Close the all of the open air tanks.
    // tg: code/modules/mob/living/carbon/inventory.dm:291
    public virtual async DmTask close_all_airtanks()
    {
        if (external != null)
            await close_externals();
        if (@internal != null)
            await close_internals();
    }

    /// Prepares emergency disconnect from open air tanks and notifies in chat. Usually called after mob suddenly unequips breathing apparatus.
    // tg: code/modules/mob/living/carbon/inventory.dm:330
    public virtual async DmTask cutoff_internals()
    {
        if (external == null && @internal == null)
            return;
        to_chat(this, span_notice(I18n.T("Your internals disconnect from [] and the valve closes.", (external is var or__1 && Dm.truthy(or__1) ? (object)or__1 : @internal))));
        await close_all_airtanks();
    }
}

// tg: code/modules/mob/living/carbon/inventory.dm:26
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/inventory.dm:26
    public override object get_item_by_slot(object slot_id = null)
    {
        object sw__1 = slot_id;
        if (Dm.eq(sw__1, ITEM_SLOT_HANDCUFFED))
            return handcuffed;
        else if (Dm.eq(sw__1, ITEM_SLOT_LEGCUFFED))
            return legcuffed;

        return base.get_item_by_slot(slot_id);
    }

    //This is an UNSAFE proc. Use mob_can_equip() before calling this one! Or rather use equip_to_slot_if_possible() or advanced_equip_to_slot_if_possible()
    // tg: code/modules/mob/living/carbon/inventory.dm:81
    public override async DmTask<object> equip_to_slot(obj_item equipping = null, object slot = null, float? initial = null, float? redraw_mob = null, float? indirect_action = null)
    {
        initial ??= FALSE;
        redraw_mob ??= FALSE;
        indirect_action ??= FALSE;
        // DEVIATION(DV27): the interaction guard (D132, interaction.md 7, P2's site; this is the equip that runs, /mob's
        // equip_to_slot is empty): an item whose own equipped or dropped code is not translated is not equipped. A mob's initial
        // equipment (initial = TRUE: its outfit, tg's EQUIP_OUTFIT_ITEM) is put on with tg's base code and the item's own
        // equip code skipped, so a spawned crew member wears its whole outfit (the headset, the shoes and the ID card have
        // their own equipped and dropped: reported to the lead).
        if (!Dm.truthy(initial) && (InertSeam.MissingOverride(equipping, "equipped", this) || InertSeam.MissingOverride(equipping, "dropped", this)))
            return FALSE;
        if (!Dm.truthy(slot))
            return null;

        if (!(equipping is obj_item))
            return null;

        object index = get_held_index_of_item(equipping);
        if (Dm.truthy(index))
            held_items[index] = null;

        if (Dm.truthy(equipping.pulledby))
            await equipping.pulledby.stop_pulling();

        hud_used?.update_inventory_slot(slot);

        foreach (mob_dead observe in Dm.each_cast<mob_dead>(observers))
        {
            if (observe.client != null)
                Dm.remove(observe.client.screen, equipping);
        }

        await equipping.forceMove(this); //This has to come before has_equipped is called.
        SET_PLANE_EXPLICIT(equipping, ABOVE_HUD_PLANE, this);
        equipping.appearance_flags = Dm.bitor(equipping.appearance_flags, NO_CLIENT_COLOR); // appearance_flags is an engine property: no ref (rules 4.3)
        float not_handled = FALSE;

        object sw__1 = slot;
        if (Dm.eq(sw__1, ITEM_SLOT_HANDCUFFED))
            await set_handcuffed(equipping);
        else if (Dm.eq(sw__1, ITEM_SLOT_LEGCUFFED))
        {
            legcuffed = equipping;
            update_worn_legcuffs();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_HANDS))
        {
            await put_in_hands(equipping);
            await update_held_items();
        }
        else
            not_handled = TRUE;

        //Item has been handled at this point and equipped callback can be safely called
        //We cannot call it for items that have not been handled as they are not yet correctly
        //in a slot (handled further down inheritance chain, probably living/carbon/human/equip_to_slot
        if (!Dm.truthy(not_handled) && !Dm.eq(slot, ITEM_SLOT_HANDS)) // put in hands calls equipped on its own, annoyingly
            await has_equipped(equipping, slot, initial);

        return not_handled;
    }

    // tg: code/modules/mob/living/carbon/inventory.dm:125
    public override async DmTask<object> has_equipped(obj_item item = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        object dot = null;
        dot = await base.has_equipped(item, slot, initial);
        if (!Dm.truthy(dot))
            return dot;

        hud_used?.update_locked_slots();
        if (!Dm.truthy(Dm.bitand(slot, item.slot_flags))) // Things below only update if slotted in (ie: not held)
            return dot;
        if (Dm.truthy(item.hair_mask))
        {
            LAZYADD(ref hair_masks, item.hair_mask);
            update_hair();
            await update_body(); // this is solely for lizard frills
        }
        await add_item_coverage(item);
        return dot;
    }

    /// Adds the passed item's coverage to the mob's coverage related flags
    // tg: code/modules/mob/living/carbon/inventory.dm:172
    public virtual async DmTask add_item_coverage(obj_item item = null)
    {
        float pre_coverage = obscured_slots;
        Dm.combine(ref obscured_slots, item.flags_inv);
        Dm.combine(ref covered_slots, Dm.bitor(item.flags_inv, item.transparent_protection));
        if (pre_coverage != obscured_slots)
            await item_coverage_changed(Dm.bitand(obscured_slots, Dm.bitnot(pre_coverage)), Dm.bitand(pre_coverage, Dm.bitnot(obscured_slots)));
    }

    /**
     * Called when a mob's obscured slots change
     *
     * Args
     * * added_slots - slots that were added to obscured_slots
     * * removed_slots - slots that were removed from obscured_slots
     */
    // tg: code/modules/mob/living/carbon/inventory.dm:205
    public virtual async DmTask item_coverage_changed(object added_slots = null, object removed_slots = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_CARBON_ITEM_COVERAGE_CHANGED]))
            this._SendSignal(COMSIG_CARBON_ITEM_COVERAGE_CHANGED, Dm.list(this, added_slots, removed_slots));
        await update_clothing(hidden_slots_to_inventory_slots(Dm.bitor(added_slots, removed_slots)));
        if (Dm.truthy(Dm.bitand(Dm.bitor(added_slots, removed_slots), HIDESNOUT)))
            synchronize_bodyshapes();
        if (Dm.truthy(Dm.bitand(Dm.bitor(added_slots, removed_slots), Dm.bitor(HIDEHAIR, HIDEFACIALHAIR))))
            update_hair();
        if (Dm.truthy(Dm.bitand(Dm.bitor(added_slots, removed_slots), HIDEEYES)))
            await update_eyes();
        // HIDEJUMPSUIT is for digitigrade legs, HIDEEARS is for lizard frills, HIDEHAIR is for felinid ears and lizard horns, the others should be obvious
        // future todo; we should collect a list of all bodypart overlays and what conceals/reveals them dynamically, rather than hardcoding this
        if (Dm.truthy(Dm.bitand(Dm.bitor(added_slots, removed_slots), Dm.bitor(Dm.bitor(Dm.bitor(HIDEJUMPSUIT, HIDEEARS), Dm.bitor(HIDEHAIR, HIDESNOUT)), Dm.bitor(HIDEMUTWINGS, HIDEANTENNAE)))))
            await update_body();
    }
}

// M4a contracts: the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M4a P2 translates
// them or leaves them for their milestone (docs/ai/specs/m4a-ownership.md). Overrides are not declared, so the inherited
// code keeps running until then.

// tg: code/modules/mob/living/carbon/inventory.dm:2  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    /// Convers HIDEX to ITEM_SLOT_X, should be phased out in favor of using latter everywhere later
    // tg: code/modules/mob/living/carbon/inventory.dm:2
    public static object hidden_slots_to_inventory_slots(object hidden_slots = null)
    {
        float obscured = NONE;
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDENECK)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_NECK);
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDEMASK)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_MASK);
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDEBELT)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_BELT);
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDEEYES)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_EYES);
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDEEARS)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_EARS);
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDEGLOVES)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_GLOVES);
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDEJUMPSUIT)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_ICLOTHING);
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDESHOES)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_FEET);
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDESUITSTORAGE)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_SUITSTORE);
        if (Dm.truthy(Dm.bitand(hidden_slots, HIDEHEADGEAR)))
            obscured = Dm.bitor(obscured, ITEM_SLOT_HEAD);
        return obscured;
    }
}

// tg: code/modules/mob/living/carbon/inventory.dm:45
public partial class mob_living_carbon : mob_living
{
    /// Returns items which are currently visible on the mob
    // tg: code/modules/mob/living/carbon/inventory.dm:45
    public virtual object get_visible_items()
    {
        DmList visible_items = Dm.list();
        object obscured_item_slots = hidden_slots_to_inventory_slots(obscured_slots);
        foreach (obj_item thing in Dm.each<obj_item>(get_equipped_items(Dm.bitor(INCLUDE_HELD, INCLUDE_PROSTHETICS))))
        {
            if (!Dm.truthy(Dm.bitand(get_slot_by_item(thing), obscured_item_slots)))
                Dm.append(ref visible_items, thing);
        }
        return visible_items;
    }

    // tg: code/modules/mob/living/carbon/inventory.dm:53
    public virtual async DmTask<object> equip_in_one_of_slots(obj_item equipping = null, DmList slots = null, float? qdel_on_fail = null, float? indirect_action = null)
    {
        qdel_on_fail ??= TRUE;
        indirect_action ??= FALSE;
        // tg's var/static/list/equip_slots and storage_slots: constant lists that nothing writes, built per call (D31: no
        // static mutable state in the core); the result is the same.
        DmList equip_slots = Dm.list(
            Dm.kv(LOCATION_LPOCKET, ITEM_SLOT_LPOCKET),
            Dm.kv(LOCATION_RPOCKET, ITEM_SLOT_RPOCKET),
            Dm.kv(LOCATION_HANDS, ITEM_SLOT_HANDS),
            Dm.kv(LOCATION_GLOVES, ITEM_SLOT_GLOVES),
            Dm.kv(LOCATION_EYES, ITEM_SLOT_EYES),
            Dm.kv(LOCATION_MASK, ITEM_SLOT_MASK),
            Dm.kv(LOCATION_HEAD, ITEM_SLOT_HEAD),
            Dm.kv(LOCATION_NECK, ITEM_SLOT_NECK),
            Dm.kv(LOCATION_ID, ITEM_SLOT_ID)
        );
        DmList storage_slots = Dm.list(
            Dm.kv(LOCATION_BACKPACK, ITEM_SLOT_BACK)
        );

        foreach (object slot in Dm.each(slots))
        {
            if (Dm.truthy(equip_slots[slot]))
            {
                if (Dm.truthy(await equip_to_slot_if_possible(equipping, equip_slots[slot], disable_warning: TRUE, indirect_action: indirect_action)))
                    return slot;
            }
            else if (Dm.truthy(storage_slots[slot]))
            {
                if (Dm.truthy(equip_to_storage(equipping, storage_slots[slot], indirect_action: indirect_action)))
                    return slot;
            }
        }
        if (Dm.truthy(qdel_on_fail))
            qdel(equipping);
        return null;
    }

    /// Removes the passed item's coverage from the mob's coverage related flags
    // tg: code/modules/mob/living/carbon/inventory.dm:180
    public virtual void remove_item_coverage(obj_item item = null)
    {
        refresh_obscured(); // No way to remove a single item's coverage without recalculating everything
    }

    /**
     * Open the internal air tank without checking for any breathing apparatus.
     * Returns TRUE if the air tank was opened successfully.
     * Closes any existing tanks before opening another one.
     *
     * Arguments:
     * * tank - The given tank to open and start breathing from.
     * * is_external - A boolean which indicates if the air tank must be equipped, or stored elsewhere.
     */
    // tg: code/modules/mob/living/carbon/inventory.dm:240
    public virtual async DmTask<object> open_internals(obj_item_tank target_tank = null, float? is_external = null)
    {
        is_external ??= FALSE;
        if (target_tank == null)
            return null;
        await close_all_airtanks();
        if (Dm.truthy(is_external))
            external = target_tank;
        else
            @internal = target_tank;
        target_tank.after_internals_opened(this);
        await update_mob_action_buttons();
        return TRUE;
    }

    /**
     * Opens the given internal air tank if a breathing apparatus is found. Returns TRUE if successful, FALSE otherwise.
     * Returns TRUE if the tank was opened successfully.
     *
     * Arguments:
     * * tank - The given tank we will attempt to toggle open and start breathing from.
     * * is_external - A boolean which indicates if the air tank must be equipped, or stored elsewhere.
     */
    // tg: code/modules/mob/living/carbon/inventory.dm:260
    public virtual async DmTask<object> try_open_internals(obj_item_tank target_tank = null, float? is_external = null)
    {
        is_external ??= FALSE;
        if (!Dm.truthy(can_breathe_internals()))
            return null;
        return await open_internals(target_tank, is_external);
    }

    /**
     * Actually closes the active internal or external air tank.
     * Returns TRUE if the tank was opened successfully.
     *
     * Arguments:
     * * is_external - A boolean which indicates if the air tank must be equipped, or stored elsewhere.
     */
    // tg: code/modules/mob/living/carbon/inventory.dm:272
    public virtual async DmTask<object> close_internals(float? is_external = null)
    {
        is_external ??= FALSE;
        obj_item_tank target_tank = Dm.truthy(is_external) ? external : @internal;
        if (target_tank == null)
            return null;
        if (Dm.truthy(is_external))
            external = null;
        else
            @internal = null;
        target_tank.after_internals_closed(this);
        await update_mob_action_buttons();
        //To make sure it stops at a timely manner when you turn off internals
        breathing_loop.stop();
        return TRUE;
    }

    /// Close the the currently open external (that's EX-ternal) air tank. Returns TRUE if successful.
    // tg: code/modules/mob/living/carbon/inventory.dm:287
    public virtual async DmTask<object> close_externals()
    {
        return await close_internals(TRUE);
    }

    /**
     * Prepares to open the internal air tank and notifies the mob in chat.
     * Handles displaying messages to the user before doing the actual opening.
     * Returns TRUE if the tank was opened/closed successfully.
     *
     * Arguments:
     * * tank - The given tank to toggle open and start breathing from.
     * * is_external - A boolean which indicates if the air tank must be equipped, or stored elsewhere.
     */
    // tg: code/modules/mob/living/carbon/inventory.dm:306
    public virtual async DmTask<object> toggle_open_internals(obj_item_tank target_tank = null, float? is_external = null)
    {
        is_external ??= FALSE;
        if (target_tank == null)
            return null;
        if (@internal != null || (Dm.truthy(is_external) && external != null))
            to_chat(this, span_notice(I18n.T("You switch your internals to [].", target_tank)));
        else
            to_chat(this, span_notice(I18n.T("You open [] valve.", target_tank)));
        return await open_internals(target_tank, is_external);
    }

    /**
     * Prepares to close the currently open internal air tank and notifies in chat.
     * Handles displaying messages to the user before doing the actual closing.
     * Returns TRUE if
     *
     * Arguments:
     * * is_external - A boolean which indicates if the air tank must be equipped, or stored elsewhere.
     */
    // tg: code/modules/mob/living/carbon/inventory.dm:323
    public virtual async DmTask<object> toggle_close_internals(float? is_external = null)
    {
        is_external ??= FALSE;
        if (@internal == null && external == null)
            return null;
        to_chat(this, span_notice(I18n.T("You close [] valve.", Dm.truthy(is_external) ? external : @internal)));
        return await close_internals(is_external);
    }

    /**
     * Toggles the given internal air tank open, or close the currently open one, if a compatible breathing apparatus is found.
     * Returns TRUE if the tank was opened successfully.
     *
     * Arguments:
     * * tank - The given tank to toggle open and start breathing from internally.
     */
    // tg: code/modules/mob/living/carbon/inventory.dm:343
    public virtual async DmTask<object> toggle_internals(obj_item tank = null, params object[] __extra)
    {
        // Carbons can't open their own internals tanks.
        return FALSE;
    }

    /**
     * Toggles the given external (that's EX-ternal) air tank open, or close the currently open one, if a compatible breathing apparatus is found.
     * Returns TRUE if the tank was opened successfully.
     *
     * Arguments:
     * * tank - The given tank to toggle open and start breathing from externally.
     */
    // tg: code/modules/mob/living/carbon/inventory.dm:354
    public virtual async DmTask<object> toggle_externals(obj_item tank = null)
    {
        // Carbons can't open their own externals tanks.
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/inventory.dm:358
    public virtual object get_holding_bodypart_of_item(obj_item I = null)
    {
        object index = get_held_index_of_item(I);
        return Dm.truthy(index) ? hand_bodyparts[index] : index;
    }

    ///Returns a list of all body_zones covered by clothing
    // tg: code/modules/mob/living/carbon/inventory.dm:363
    [ShouldNotOverride]
    public virtual object get_covered_body_zones()
    {
        return cover_flags2body_zones(get_all_covered_flags());
    }

    ///Returns a bitfield of all zones covered by clothing
    // tg: code/modules/mob/living/carbon/inventory.dm:370
    [ShouldNotOverride]
    public virtual object get_all_covered_flags()
    {
        float covered_flags = NONE;
        foreach (obj_item worn_item in Dm.each<obj_item>(get_equipped_items(INCLUDE_ABSTRACT)))
            covered_flags = Dm.bitor(covered_flags, worn_item.body_parts_covered);

        return covered_flags;
    }

    // tg: code/modules/mob/living/carbon/inventory.dm:413
    public virtual async DmTask<object> equip_conspicuous_item(obj_item item = null, float? delete_item_if_failed = null)
    {
        delete_item_if_failed ??= TRUE;
        DmList pockets = Dm.list(
            Dm.kv("left pocket", ITEM_SLOT_LPOCKET),
            Dm.kv("right pocket", ITEM_SLOT_RPOCKET)
        );

        object placed_in = await equip_in_one_of_slots(item, pockets, qdel_on_fail: FALSE, indirect_action: TRUE);

        if (!Dm.truthy(placed_in))
            placed_in = equip_to_storage(item, ITEM_SLOT_BACK, indirect_action: TRUE);

        if (placed_in == null && Dm.truthy(delete_item_if_failed))
            qdel(item);

        return placed_in;
    }
}

// tg: code/modules/mob/living/carbon/inventory.dm:35
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/inventory.dm:35
    public override object get_slot_by_item(obj_item looking_for = null)
    {
        if (looking_for == handcuffed)
            return ITEM_SLOT_HANDCUFFED;

        if (looking_for == legcuffed)
            return ITEM_SLOT_LEGCUFFED;

        return base.get_slot_by_item(looking_for);
    }

    // tg: code/modules/mob/living/carbon/inventory.dm:138
    public override async DmTask<object> has_unequipped(obj_item item = null, float? silent = null)
    {
        object dot = null;
        dot = await base.has_unequipped(item, silent);
        if (!Dm.truthy(dot))
            return dot;

        hud_used?.update_locked_slots();
        if (Dm.truthy(item.hair_mask))
        {
            LAZYREMOVE(ref hair_masks, item.hair_mask);
            update_hair();
            await update_body(); // this is solely for lizard frills
        }
        remove_item_coverage(item);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/inventory.dm:150
    public override async DmTask<object> doUnEquip(obj_item item_dropping = null, object force = null, atom newloc = null, object no_move = null, float? invdrop = null, float? silent = null)
    {
        invdrop ??= TRUE;
        silent ??= FALSE;
        object dot = null;
        dot = await base.doUnEquip(item_dropping, force, newloc, no_move, invdrop, silent); //Sets the default return value to what the parent returns.
        if (!Dm.truthy(dot) || !Dm.truthy(item_dropping)) //We don't want to set anything to null if the parent returned 0.
            return dot;

        if (item_dropping == handcuffed)
        {
            await set_handcuffed(null);
            if (Dm.truthy(buckled?.buckle_requires_restraints))
                await buckled.unbuckle_mob(this);
        }
        else if (item_dropping == legcuffed)
        {
            legcuffed = null;
            if (!QDELETED(this))
                update_worn_legcuffs();
        }

        // Not an else-if because we're probably equipped in another slot
        if (item_dropping == @internal && (QDELETED(this) || QDELETED(item_dropping) || item_dropping.loc != this))
        {
            await cutoff_internals();
            if (!QDELETED(this))
                await update_mob_action_buttons(UPDATE_BUTTON_STATUS);
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/inventory.dm:183
    public override void refresh_obscured()
    {
        float pre_coverage = obscured_slots;

        obscured_slots = NONE;
        covered_slots = NONE;
        foreach (obj_item other_equipped_item in Dm.each_cast<obj_item>(get_equipped_items()))
        {
            Dm.combine(ref obscured_slots, other_equipped_item.flags_inv);
            Dm.combine(ref covered_slots, Dm.bitor(other_equipped_item.flags_inv, other_equipped_item.transparent_protection));
        }

        if (HAS_TRAIT(this, TRAIT_HUSK) || HAS_TRAIT(this, TRAIT_INVISIBLE_MAN))
            obscured_slots = Dm.bitor(obscured_slots, Dm.bitor(HIDEHAIR, HIDEFACIALHAIR));

        if (pre_coverage != obscured_slots)
            Waiting.NoSleep(item_coverage_changed(Dm.bitand(obscured_slots, Dm.bitnot(pre_coverage)), Dm.bitand(pre_coverage, Dm.bitnot(obscured_slots))));
    }

    // tg: code/modules/mob/living/carbon/inventory.dm:379
    public override object is_location_accessible(object location = null, float? exluded_equipment_slots = null)
    {
        exluded_equipment_slots ??= NONE;
        object sw__1 = location;
        // Snowflake checks for these precise zones
        if (Dm.eq(sw__1, BODY_ZONE_PRECISE_EYES))
        {
            if (Dm.truthy(is_eyes_covered(Dm.bitnot(exluded_equipment_slots))) || Dm.truthy(Dm.bitand(obscured_slots, Dm.bitor(HIDEEYES, HIDEFACE))))
                return FALSE;
        }
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_MOUTH))
        {
            if (Dm.truthy(is_mouth_covered(Dm.bitnot(exluded_equipment_slots))) || Dm.truthy(Dm.bitand(obscured_slots, HIDEFACE)))
                return FALSE;
        }

        float covered_flags = NONE;
        foreach (obj_item worn_item in Dm.each<obj_item>(get_equipped_items(INCLUDE_ABSTRACT)))
        {
            if (Dm.truthy(Dm.bitand(worn_item.slot_flags, exluded_equipment_slots)))
                continue;
            if (Dm.truthy(Dm.bitand(worn_item.flags_cover, ALLOW_SURGERY_THROUGH)))
                continue;
            covered_flags = Dm.bitor(covered_flags, worn_item.body_parts_covered);
        }

        // NB: we have to convert covered_flags via cover_flags2body_zones here
        // instead of converting location via body_zones2cover_flags
        //
        // our coverage might look something like GROIN|LEGS, which would convert to list(BODY_ZONE_GROIN, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG)
        // so if we were checking "is BODY_ZONE_CHEST accessible", we would pass - this is correct!
        //
        // however, if we convert the location to body zone, we would get CHEST|GROIN
        // then we would check (CHEST|GROIN) & (GROIN|LEGS) and return FALSE - which is incorrect, the chest is perfectly accessible!
        // checking for ((CHEST|GROIN) & (GROIN|LEGS)) == (CHEST|GROIN) would also be incorrect,
        // as it would imply your chest is accessible from lacking groin coverage
        return Dm.not(Dm.@in(location, cover_flags2body_zones(covered_flags)));
    }
}

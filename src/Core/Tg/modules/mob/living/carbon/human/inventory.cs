// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the M4a inventory procs translated (M4a P2: the slot lookups, the visible items, equip_to_slot, doUnEquip,
// the coverage); smart_equip_targeted (ui-overhaul.md section 12, slice 4E: the quick-equip keys); the rest stays for its milestone:
// delete_equipment (M10; syndicate N1 ported equip_species_outfit), change_number_of_hands (the
// wizard's extra hands, M10). The internals and breathing checks
// (toggle_internals, toggle_externals, can_breathe_helmet, can_breathe_mask, can_breathe_internals) are M4c slice 1's.

// tg: code/modules/mob/living/carbon/human/inventory.dm:8
public partial class mob_living_carbon_human : mob_living_carbon
{
    /**
     * Used to return a list of equipped items on a human mob; does not by default include held items, see include_flags
     *
     * Argument(s):
     * * Optional - include_flags, (see obj.flags.dm) describes which optional things to include or not (pockets, accessories, held items)
     */
    // tg: code/modules/mob/living/carbon/human/inventory.dm:8
    public override object get_equipped_items(float? include_flags = null)
    {
        include_flags ??= NONE;
        DmList items = base.get_equipped_items(include_flags) as DmList;
        if (!Dm.truthy(Dm.bitand(include_flags, INCLUDE_POCKETS)))
            Dm.remove(ref items, Dm.list(l_store, r_store, s_store));
        if (Dm.truthy(Dm.bitand(include_flags, INCLUDE_ACCESSORIES)) && w_uniform is obj_item_clothing_under)
        {
            obj_item_clothing_under worn_under = w_uniform as obj_item_clothing_under;
            Dm.append(ref items, worn_under.attached_accessories);
        }
        return items;
    }

    // tg: code/modules/mob/living/carbon/human/inventory.dm:18
    public override async DmTask<object> can_equip(obj_item equip_target = null, object slot = null, float? disable_warning = null, float? bypass_equip_delay_self = null, float? ignore_equipped = null, float? indirect_action = null)
    {
        disable_warning ??= FALSE;
        bypass_equip_delay_self ??= FALSE;
        ignore_equipped ??= FALSE;
        indirect_action ??= FALSE;
        if (Dm.eq((!Dm.truthy(this._listen_lookup?[COMSIG_HUMAN_EQUIPPING_ITEM]) ? NONE : this._SendSignal(COMSIG_HUMAN_EQUIPPING_ITEM, Dm.list(this, equip_target, slot))), COMPONENT_BLOCK_EQUIP))
            return FALSE;
        if (HAS_TRAIT(equip_target, TRAIT_NODROP) && Dm.@in(equip_target, held_items))
        {
            if (!Dm.truthy(disable_warning))
                to_chat(this, span_warning(I18n.T("[] won't budge, it's impossible to put it on!", equip_target)));
            return FALSE;
        }
        return await ((datum_species)dna.species).can_equip(equip_target, slot, disable_warning, this, bypass_equip_delay_self, ignore_equipped, indirect_action);
    }

    // tg: code/modules/mob/living/carbon/human/inventory.dm:27
    public override object get_item_by_slot(object slot_id = null)
    {
        object sw__1 = slot_id;
        if (Dm.eq(sw__1, ITEM_SLOT_BACK))
            return back;
        else if (Dm.eq(sw__1, ITEM_SLOT_BELT))
            return belt;
        else if (Dm.eq(sw__1, ITEM_SLOT_ID))
            return wear_id;
        else if (Dm.eq(sw__1, ITEM_SLOT_EARS))
            return ears;
        else if (Dm.eq(sw__1, ITEM_SLOT_EYES))
            return glasses;
        else if (Dm.eq(sw__1, ITEM_SLOT_GLOVES))
            return gloves;
        else if (Dm.eq(sw__1, ITEM_SLOT_FEET))
            return shoes;
        else if (Dm.eq(sw__1, ITEM_SLOT_MASK))
            return wear_mask;
        else if (Dm.eq(sw__1, ITEM_SLOT_NECK))
            return wear_neck;
        else if (Dm.eq(sw__1, ITEM_SLOT_HEAD))
            return head;
        else if (Dm.eq(sw__1, ITEM_SLOT_OCLOTHING))
            return wear_suit;
        else if (Dm.eq(sw__1, ITEM_SLOT_ICLOTHING))
            return w_uniform;
        else if (Dm.eq(sw__1, ITEM_SLOT_LPOCKET))
            return l_store;
        else if (Dm.eq(sw__1, ITEM_SLOT_RPOCKET))
            return r_store;
        else if (Dm.eq(sw__1, ITEM_SLOT_SUITSTORE))
            return s_store;

        return base.get_item_by_slot(slot_id);
    }

    //This is an UNSAFE proc. Use mob_can_equip() before calling this one! Or rather use equip_to_slot_if_possible() or advanced_equip_to_slot_if_possible()
    // Initial is used to indicate whether or not this is the initial equipment (job datums etc) or just a player doing it
    // tg: code/modules/mob/living/carbon/human/inventory.dm:122
    public override async DmTask<object> equip_to_slot(obj_item equipping = null, object slot = null, float? initial = null, float? redraw_mob = null, float? indirect_action = null)
    {
        initial ??= FALSE;
        redraw_mob ??= FALSE;
        indirect_action ??= FALSE;
        if (!Dm.truthy(await base.equip_to_slot(equipping, slot, initial, redraw_mob, indirect_action))) //a check failed or the item has already found its slot
            return null;

        float not_handled = FALSE; //Added in case we make this type path deeper one day
        object sw__1 = slot;
        if (Dm.eq(sw__1, ITEM_SLOT_BACK))
        {
            if (Dm.truthy(back))
                return null;
            back = equipping;
            await update_worn_back();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_BELT))
        {
            if (Dm.truthy(belt))
                return null;
            belt = equipping;
            await update_worn_belt();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_ID))
        {
            if (Dm.truthy(wear_id))
                return null;
            wear_id = equipping;
            update_ID_card();
            await update_worn_id();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_EARS))
        {
            if (Dm.truthy(ears))
                return null;
            ears = equipping;
            await update_worn_ears();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_EYES))
        {
            if (Dm.truthy(glasses))
                return null;
            glasses = equipping;
            await update_worn_glasses();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_GLOVES))
        {
            if (Dm.truthy(gloves))
                return null;
            gloves = equipping;
            await update_worn_gloves();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_FEET))
        {
            if (Dm.truthy(shoes))
                return null;
            shoes = equipping;
            await update_worn_shoes();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_OCLOTHING))
        {
            if (Dm.truthy(wear_suit))
                return null;
            wear_suit = equipping;
            if (Dm.truthy(wear_suit.breakouttime)) //when equipping a straightjacket
            {
                ADD_TRAIT(this, TRAIT_RESTRAINED, SUIT_TRAIT);
                await stop_pulling(); //can't pull if restrained
                await update_mob_action_buttons(); //certain action buttons will no longer be usable.
            }
            await update_worn_oversuit();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_MASK))
        {
            if (Dm.truthy(wear_mask))
                return null;
            wear_mask = equipping;
            await update_worn_mask();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_HEAD))
        {
            if (Dm.truthy(head))
                return null;
            head = equipping;
            await update_worn_head();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_NECK))
        {
            if (Dm.truthy(wear_neck))
                return null;
            wear_neck = equipping;
            await update_worn_neck(); // tg passes the item (update_worn_neck(equipping)); the slot's root takes no argument, which DM drops
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_ICLOTHING))
        {
            if (Dm.truthy(w_uniform))
                return null;
            w_uniform = equipping;
            await update_worn_undersuit();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_LPOCKET))
        {
            l_store = equipping;
            update_pockets();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_RPOCKET))
        {
            r_store = equipping;
            update_pockets();
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_SUITSTORE))
        {
            if (Dm.truthy(s_store))
                return null;
            s_store = equipping;
            await update_suit_storage();
        }
        else
        {
            to_chat(this, span_danger(I18n.T("You are trying to equip this item to an unsupported inventory slot. Report this to a coder!")));
            not_handled = TRUE;
        }

        //Item is handled and in slot, valid to call callback, for this proc should always be true
        if (!Dm.truthy(not_handled))
            await has_equipped(equipping, slot, initial);

        return not_handled; //For future deeper overrides
    }

    // tg: code/modules/mob/living/carbon/human/inventory.dm:344
    public virtual async DmTask<object> equipOutfit(object outfit = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        datum_outfit O = null;

        if (ispath(outfit))
            O = await Dm.new_async((Type)outfit) as datum_outfit;
        else
        {
            O = outfit as datum_outfit;
            if (!(O is datum_outfit))
                return 0;
        }
        if (O == null)
            return 0;

        return await O.equip(this, visuals_only);
    }
}

// M4a contracts: the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M4a P2 translates
// them or leaves them for their milestone (docs/ai/specs/m4a-ownership.md). Overrides are not declared, so the inherited
// code keeps running until then.

// tg: code/modules/mob/living/carbon/human/inventory.dm:360
public partial class mob_living_carbon_human : mob_living_carbon
{
    ///A version of equipOutfit that overrides passed in outfits with their entry on the species' outfit override registry
    // tg: code/modules/mob/living/carbon/human/inventory.dm:360  (syndicate N1, D316: the operatives' outfit)
    public virtual async DmTask<object> equip_species_outfit(object outfit = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        datum_outfit outfit_to_equip;

        object override_outfit_path = (dna?.species as datum_species)?.outfit_override_registry?[outfit];
        if (Dm.truthy(override_outfit_path))
            outfit_to_equip = await Dm.new_async((Type)override_outfit_path) as datum_outfit;
        else
        {
            await ((datum_species)dna.species).give_important_for_life();
            outfit_to_equip = await Dm.new_async((Type)outfit) as datum_outfit;
        }

        if (outfit_to_equip == null)
            return FALSE;

        return await outfit_to_equip.equip(this, visuals_only);
    }

    // tg: code/modules/mob/living/carbon/human/inventory.dm:377
    public virtual async DmTask delete_equipment()
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/inventory.dm:377", "delete_equipment: M4a P2 (skeleton)");
    }

    // tg: code/modules/mob/living/carbon/human/inventory.dm:385
    public virtual async DmTask smart_equip_targeted(float? slot_type = null, string slot_item_name = null)
    {
        slot_type ??= ITEM_SLOT_BELT;
        slot_item_name ??= "belt";
        if (Dm.truthy(incapacitated))
            return;
        obj_item thing = get_active_held_item() as obj_item;
        obj_item equipped_item = get_item_by_slot(slot_type) as obj_item;
        object thing_reject = NONE;
        if (thing != null)
            thing_reject = !Dm.truthy(thing._listen_lookup?[COMSIG_HUMAN_NON_STORAGE_HOTKEY]) ? NONE : thing._SendSignal(COMSIG_HUMAN_NON_STORAGE_HOTKEY, Dm.list(this, equipped_item));
        if (equipped_item == null) // We also let you equip an item like this
        {
            if (thing == null)
            {
                to_chat(this, span_warning(I18n.T("You have no [] to take something out of!", slot_item_name)));
                return;
            }
            if (Dm.truthy(await equip_to_slot_if_possible(thing, slot_type)))
                await update_held_items();
            return;
        }
        datum_storage storage = equipped_item.atom_storage;
        if (storage == null)
        {
            if (thing == null)
                await equipped_item.attack_hand(this);
            else
            {
                if (Dm.truthy(Dm.bitand(thing_reject, COMPONENT_STORAGE_HOTKEY_HANDLED)))
                    return;
                to_chat(this, span_warning(I18n.T("You can't fit [] into your []!", thing, equipped_item.name)));
            }
            return;
        }
        if (!Dm.truthy(storage.supports_smart_equip))
            return;
        if (Dm.truthy(equipped_item.atom_storage.locked)) // Determines if container is locked before trying to put something in or take something out so we dont give out information on contents (or lack of)
        {
            to_chat(this, span_warning(I18n.T("\\The [] is locked!", equipped_item)));
            return;
        }
        if (thing != null) // put thing in storage item
        {
            if (!Dm.truthy(equipped_item.atom_storage?.attempt_insert(thing, this)))
                to_chat(this, span_warning(I18n.T("You can't fit [] into your []!", thing, equipped_item.name)));
            return;
        }
        DmList contents = storage.real_location.contents;
        if (!Dm.truthy(contents.len)) // nothing to take out
        {
            to_chat(this, span_warning(I18n.T("There's nothing in your [] to take out!", equipped_item.name)));
            return;
        }
        obj_item stored = contents[contents.len] as obj_item;
        if (stored == null || Dm.truthy(await stored.on_found(this)))
            return;
        await stored.attack_hand(this); // take out thing from item in storage slot
    }

    /// Returns the helmet if an air tank compatible helmet is equipped.
    // tg: code/modules/mob/living/carbon/human/inventory.dm:452
    public virtual object can_breathe_helmet()
    {
        if (Dm.truthy(Dm.bitand((head as obj_item_clothing)?.clothing_flags, HEADINTERNALS)))
            return head;
        return null;
    }

    /// Returns the mask if an air tank compatible mask is equipped.
    // tg: code/modules/mob/living/carbon/human/inventory.dm:457
    public virtual object can_breathe_mask()
    {
        if (Dm.truthy(Dm.bitand((wear_mask as obj_item_clothing)?.clothing_flags, MASKINTERNALS)))
            return wear_mask;
        return null;
    }

    /// Returns the object that allows us to breathe internals - tube implant, mask or helmet
    // tg: code/modules/mob/living/carbon/human/inventory.dm:462
    public override object can_breathe_internals()
    {
        return can_breathe_tube() is var or__1 && Dm.truthy(or__1) ? or__1 : can_breathe_mask() is var or__2 && Dm.truthy(or__2) ? or__2 : can_breathe_helmet();
    }

    // tg: code/modules/mob/living/carbon/human/inventory.dm:301
    [DmParams("tank", "is_external")]
    public override async DmTask<object> toggle_internals(obj_item tank = null, params object[] __extra)
    {
        object is_external = Dm.arg(__extra, 0) ?? FALSE;
        // Just close the tank if it's the one the mob already has open.
        obj_item existing_tank = Dm.truthy(is_external) ? external : @internal;
        if (tank == existing_tank)
            return await toggle_close_internals(Dm.num(is_external));

        // Use breathing tube regardless of mask.
        if (Dm.truthy(can_breathe_tube()))
            return await toggle_open_internals(tank as obj_item_tank, Dm.num(is_external));

        // Use mask in absence of tube.
        if (Dm.truthy(can_breathe_mask()))
            return await toggle_open_internals(tank as obj_item_tank, Dm.num(is_external));
        // We have a valid mask but it's pulled down
        else if (isclothing(wear_mask))
        {
            obj_item_clothing mask = wear_mask as obj_item_clothing;
            if (Dm.truthy(mask.up) && Dm.truthy(Dm.bitand(mask.visor_flags, MASKINTERNALS)) && !Dm.truthy(Dm.bitand(mask.clothing_flags, INTERNALS_ADJUST_EXEMPT)) && Dm.truthy(await mask.adjust_visor(this)))
                return await toggle_open_internals(tank as obj_item_tank, Dm.num(is_external));
        }

        // Use helmet in absence of tube or valid mask.
        if (Dm.truthy(can_breathe_helmet()))
            return await toggle_open_internals(tank as obj_item_tank, Dm.num(is_external));
        // We have a valid helmet but its visor is up
        else if (isclothing(head))
        {
            obj_item_clothing helmet = head as obj_item_clothing;
            if (Dm.truthy(helmet.up) && Dm.truthy(Dm.bitand(helmet.visor_flags, HEADINTERNALS)) && !Dm.truthy(Dm.bitand(helmet.clothing_flags, INTERNALS_ADJUST_EXEMPT)) && Dm.truthy(await helmet.adjust_visor(this)))
                return await toggle_open_internals(tank as obj_item_tank, Dm.num(is_external));
        }

        // Notify user of missing valid breathing apparatus.
        if (wear_mask != null)
        {
            // Invalid mask
            to_chat(this, span_warning(I18n.T("[] can't use []!", wear_mask, tank)));
        }
        else if (head != null)
        {
            // Invalid headgear
            to_chat(this, span_warning(I18n.T("[] isn't airtight! You need a mask!", head)));
        }
        else
        {
            // Not wearing any breathing apparatus.
            to_chat(this, span_warning(I18n.T("You need a mask!")));
        }
        return null;
    }

    /// Returns TRUE if the tank successfully toggles open/closed. Opens the tank only if a breathing apparatus is found.
    // tg: code/modules/mob/living/carbon/human/inventory.dm:341
    public override async DmTask<object> toggle_externals(obj_item tank = null)
    {
        return await toggle_internals(tank, TRUE);
    }
}

// tg: code/modules/mob/living/carbon/human/inventory.dm:62
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/inventory.dm:62
    public override object get_slot_by_item(obj_item looking_for = null)
    {
        if (looking_for == back)
            return ITEM_SLOT_BACK;

        if (looking_for == belt)
            return ITEM_SLOT_BELT;

        if (looking_for == wear_id)
            return ITEM_SLOT_ID;

        if (looking_for == ears)
            return ITEM_SLOT_EARS;

        if (looking_for == glasses)
            return ITEM_SLOT_EYES;

        if (looking_for == gloves)
            return ITEM_SLOT_GLOVES;

        if (looking_for == head)
            return ITEM_SLOT_HEAD;

        if (looking_for == wear_mask)
            return ITEM_SLOT_MASK;

        if (looking_for == wear_neck)
            return ITEM_SLOT_NECK;

        if (looking_for == head)
            return ITEM_SLOT_HEAD;

        if (looking_for == shoes)
            return ITEM_SLOT_FEET;

        if (looking_for == wear_suit)
            return ITEM_SLOT_OCLOTHING;

        if (looking_for == w_uniform)
            return ITEM_SLOT_ICLOTHING;

        if (looking_for == r_store)
            return ITEM_SLOT_RPOCKET;

        if (looking_for == l_store)
            return ITEM_SLOT_LPOCKET;

        if (looking_for == s_store)
            return ITEM_SLOT_SUITSTORE;

        return base.get_slot_by_item(looking_for);
    }

    // tg: code/modules/mob/living/carbon/human/inventory.dm:113
    public override object get_visible_items()
    {
        DmList visible_items = base.get_visible_items() as DmList;
        obj_item_clothing_under under = w_uniform as obj_item_clothing_under;
        if ((under is obj_item_clothing_under) && Dm.truthy(length(under.attached_accessories)) && Dm.@in(under, visible_items))
            Dm.append(ref visible_items, under.attached_accessories);
        return visible_items;
    }

    // tg: code/modules/mob/living/carbon/human/inventory.dm:214
    public override async DmTask<object> doUnEquip(obj_item item_dropping = null, object force = null, atom newloc = null, object no_move = null, float? invdrop = null, float? silent = null)
    {
        invdrop ??= TRUE;
        silent ??= FALSE;
        object dot = null;
        dot = await base.doUnEquip(item_dropping, force, newloc, no_move, invdrop, silent); //See mob.dm for an explanation on this and some rage about people copypasting instead of calling ..() like they should.
        if (!Dm.truthy(dot) || !Dm.truthy(item_dropping))
            return dot;
        if (item_dropping == wear_suit)
        {
            if (Dm.truthy(s_store) && Dm.truthy(invdrop))
                await dropItemToGround(s_store, TRUE); //It makes no sense for your suit storage to stay on you if you drop your suit.
            if (Dm.truthy(wear_suit.breakouttime)) //when unequipping a straightjacket
            {
                REMOVE_TRAIT(this, TRAIT_RESTRAINED, SUIT_TRAIT);
                await drop_all_held_items(); //suit is restraining
                await update_mob_action_buttons(); //certain action buttons may be usable again.
            }
            wear_suit = null;
            if (!QDELETED(this)) //no need to update we're getting deleted anyway
                await update_worn_oversuit();
        }
        else if (item_dropping == w_uniform)
        {
            w_uniform = null;
            if (!QDELETED(this))
                await update_worn_undersuit();
            if (Dm.truthy(invdrop))
            {
                if (Dm.truthy(r_store) && !Dm.truthy(await can_equip(r_store, ITEM_SLOT_RPOCKET, TRUE, ignore_equipped: TRUE)))
                    await dropItemToGround(r_store, TRUE); //Again, makes sense for pockets to drop.
                if (Dm.truthy(l_store) && !Dm.truthy(await can_equip(l_store, ITEM_SLOT_LPOCKET, TRUE, ignore_equipped: TRUE)))
                    await dropItemToGround(l_store, TRUE);
                if (Dm.truthy(wear_id) && !Dm.truthy(await can_equip(wear_id, ITEM_SLOT_ID, TRUE, ignore_equipped: TRUE)))
                    await dropItemToGround(wear_id);
                if (Dm.truthy(belt) && !Dm.truthy(await can_equip(belt, ITEM_SLOT_BELT, TRUE, ignore_equipped: TRUE)))
                    await dropItemToGround(belt);
            }
        }
        else if (item_dropping == back)
        {
            back = null;
            if (!QDELETED(this))
                await update_worn_back();
        }
        else if (item_dropping == head)
        {
            head = null;
            if (!QDELETED(this))
                await update_worn_head();
        }
        else if (item_dropping == wear_mask)
        {
            wear_mask = null;
            if (!QDELETED(this))
                await update_worn_mask();
        }
        else if (item_dropping == wear_neck)
        {
            wear_neck = null;
            if (!QDELETED(this))
                await update_worn_neck(); // tg passes the item; the slot's root takes no argument
        }
        else if (item_dropping == gloves)
        {
            gloves = null;
            if (!QDELETED(this))
                await update_worn_gloves();
        }
        else if (item_dropping == glasses)
        {
            glasses = null;
            if (!QDELETED(this))
                await update_worn_glasses();
        }
        else if (item_dropping == ears)
        {
            ears = null;
            if (!QDELETED(this))
                await update_worn_ears();
        }
        else if (item_dropping == shoes)
        {
            shoes = null;
            if (!QDELETED(this))
                await update_worn_shoes();
        }
        else if (item_dropping == belt)
        {
            belt = null;
            if (!QDELETED(this))
                await update_worn_belt();
        }
        else if (item_dropping == wear_id)
        {
            wear_id = null;
            update_ID_card();
            if (!QDELETED(this))
                await update_worn_id();
        }
        else if (item_dropping == r_store)
        {
            r_store = null;
            if (!QDELETED(this))
                update_pockets();
        }
        else if (item_dropping == l_store)
        {
            l_store = null;
            if (!QDELETED(this))
                update_pockets();
        }
        else if (item_dropping == s_store)
        {
            s_store = null;
            if (!QDELETED(this))
                await update_suit_storage();
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/inventory.dm:295
    public override async DmTask item_coverage_changed(object added_slots = null, object removed_slots = null)
    {
        await base.item_coverage_changed(added_slots, removed_slots);
        if (Dm.truthy(Dm.bitand(Dm.bitor(added_slots, removed_slots), HIDEFACE)))
        {
            sec_hud_set_security_status();
            update_visible_name();
        }
    }
}

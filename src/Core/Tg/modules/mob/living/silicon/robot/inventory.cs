// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326).

//These procs handle putting stuff in your hand. It's probably best to use these rather than setting stuff manually
//as they handle all relevant stuff like adding it to the player's screen and such

// tg: code/modules/mob/living/silicon/robot/inventory.dm:7
public partial class mob_living_silicon_robot : mob_living_silicon
{
    /**
     * Returns the thing in our active hand (whatever is in our active module-slot, in this case)
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:7
    public override object get_active_held_item()
    {
        return module_active;
    }

    /**
     * Finds the first available slot and attemps to put item item_module in it.
     *
     * Arguments
     * * item_module - the item being equipped to a slot.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:22
    public virtual async DmTask<object> activate_module(obj_item item_module = null)
    {
        if (QDELETED(item_module))
            throw Dm.CRASH("activate_module called with improper item_module", "code/modules/mob/living/silicon/robot/inventory.dm", 24);

        if (!Dm.@in(item_module, model.modules))
            throw Dm.CRASH("activate_module called with item_module not in model.modules", "code/modules/mob/living/silicon/robot/inventory.dm", 27);

        if (Dm.truthy(activated(item_module)))
        {
            to_chat(this, span_warning(I18n.T("That module is already activated.")));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_ALL_DISABLED)))
        {
            to_chat(this, span_warning(I18n.T("All modules are disabled!")));
            return FALSE;
        }

        /// What's the first free slot for the borg?
        object first_free_slot = !Dm.truthy(held_items[1]) ? 1 : (!Dm.truthy(held_items[2]) ? 2 : (!Dm.truthy(held_items[3]) ? 3 : null));

        if (!Dm.truthy(first_free_slot) || Dm.truthy(is_invalid_module_number(first_free_slot)))
        {
            to_chat(this, span_warning(I18n.T("Deactivate a module first!")));
            return FALSE;
        }

        return await put_in_hand(item_module, first_free_slot);
    }

    // tg: code/modules/mob/living/silicon/robot/inventory.dm:46
    public override async DmTask<object> put_in_hand(obj_item item_module = null, object hand_index = null, float? forced = null, float? ignore_anim = null, float? visuals_only = null)
    {
        forced ??= FALSE;
        ignore_anim ??= TRUE;
        visuals_only ??= FALSE;
        object dot = await base.put_in_hand(item_module, hand_index, forced, ignore_anim, visuals_only);
        if (!Dm.truthy(dot))
            return dot;
        item_module.mouse_opacity = Dm.num(Dm.initial(item_module, "mouse_opacity"));
        observer_screen_update(item_module);
        return dot;
    }

    ///Helper for cyborgs unequipping things.
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:54
    public virtual async DmTask<object> deactivate_module(obj_item item_module = null)
    {
        return await transferItemToLoc(item_module, newloc: model);
    }

    // tg: code/modules/mob/living/silicon/robot/inventory.dm:57
    public override async DmTask<object> doUnEquip(obj_item item_dropping = null, object force = null, atom newloc = null, object no_move = null, float? invdrop = null, float? silent = null)
    {
        //borgs can drop items that aren't part of the module (used for apparatus modules, the stored item isn't a module).
        if (model == null || !Dm.@in(item_dropping, model.modules))
            return await base.doUnEquip(item_dropping, force, newloc, no_move, invdrop, silent);

        if (newloc != model)
        {
            to_chat(this, span_notice(I18n.T("You can't drop your [] module.", item_dropping.name)));
            return FALSE;
        }

        object module_num = get_selected_module();
        object dot = await base.doUnEquip(item_dropping, force, newloc, no_move, invdrop, silent);
        if (!Dm.truthy(dot))
            return dot;
        item_dropping.mouse_opacity = MOUSE_OPACITY_OPAQUE;
        //this is the cyborg equivalent of dropped(), though we call that too in doUnEquip.
        await item_dropping.cyborg_unequip(this);
        deselect_module(module_num);
        playsound_local(get_turf(this), SFX_RUSTLE, 40, TRUE);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/inventory.dm:76
    public override object put_in_hand_check(obj_item item_equipping = null)
    {
        return Dm.@in(item_equipping, model.modules) ? TRUE : FALSE;
    }

    /**
     * Breaks the slot number, changing the icon.
     *
     * Arguments
     * * module_num - the slot number being repaired.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:85
    public virtual async DmTask<object> break_cyborg_slot(object module_num = null)
    {
        if (Dm.truthy(is_invalid_module_number(module_num, TRUE)))
            return FALSE;

        if (Dm.truthy(held_items[module_num])) //If there's a held item, unequip it first.
        {
            if (!Dm.truthy(await deactivate_module(held_items[module_num] as obj_item))) //If we fail to unequip it, then don't continue
                return FALSE;
        }

        switch (Dm.num(module_num))
        {
            case BORG_CHOOSE_MODULE_ONE:
                if (Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_ALL_DISABLED)))
                    return FALSE;

                disabled_modules = Dm.bitor(disabled_modules, BORG_MODULE_ALL_DISABLED);

                playsound(this, Dm.resource("sound/machines/warning-buzzer.ogg"), 75, TRUE, TRUE);
                audible_message(span_warning(I18n.T("[] sounds an alarm! \"CRITICAL ERROR: ALL modules OFFLINE.\"", this)));

                if (builtInCamera != null)
                {
                    builtInCamera.camera_enabled = FALSE;
                    to_chat(this, span_userdanger(I18n.T("CRITICAL ERROR: Built in security camera OFFLINE.")));
                }

                to_chat(this, span_userdanger(I18n.T("CRITICAL ERROR: ALL modules OFFLINE.")));
                break;

            case BORG_CHOOSE_MODULE_TWO:
                if (Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_TWO_DISABLED)))
                    return FALSE;

                disabled_modules = Dm.bitor(disabled_modules, BORG_MODULE_TWO_DISABLED);

                playsound(this, Dm.resource("sound/machines/warning-buzzer.ogg"), 60, TRUE, TRUE);
                audible_message(span_warning(I18n.T("[] sounds an alarm! \"SYSTEM ERROR: Module [] OFFLINE.\"", this, module_num)));
                to_chat(this, span_userdanger(I18n.T("SYSTEM ERROR: Module [] OFFLINE.", module_num)));
                break;

            case BORG_CHOOSE_MODULE_THREE:
                if (Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_THREE_DISABLED)))
                    return FALSE;

                disabled_modules = Dm.bitor(disabled_modules, BORG_MODULE_THREE_DISABLED);

                playsound(this, Dm.resource("sound/machines/warning-buzzer.ogg"), 50, TRUE, TRUE);
                audible_message(span_warning(I18n.T("[] sounds an alarm! \"SYSTEM ERROR: Module [] OFFLINE.\"", this, module_num)));
                to_chat(this, span_userdanger(I18n.T("SYSTEM ERROR: Module [] OFFLINE.", module_num)));
                break;
        }

        atom_movable_screen_robot_module_slot module = hud_used?.screen_objects?[datum_hud.HUD_KEY_HAND_SLOT(module_num)] as atom_movable_screen_robot_module_slot;
        if (module != null)
            module.icon_state = Dm.text("[] +b", module.base_icon_state);
        return TRUE;
    }

    /**
     * Breaks all of a cyborg's slots.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:137
    public virtual async DmTask break_all_cyborg_slots()
    {
        for (int cyborg_slot = 1; cyborg_slot <= 3; cyborg_slot++)
            await break_cyborg_slot(cyborg_slot);
    }

    /**
     * Repairs the slot number, updating the icon.
     *
     * Arguments
     * * module_num - the module number being repaired.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:147
    public virtual object repair_cyborg_slot(object module_num = null)
    {
        if (Dm.truthy(is_invalid_module_number(module_num, TRUE)))
            return FALSE;

        switch (Dm.num(module_num))
        {
            case BORG_CHOOSE_MODULE_ONE:
                if (!Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_ALL_DISABLED)))
                    return FALSE;

                disabled_modules = Dm.bitand(disabled_modules, Dm.bitnot(BORG_MODULE_ALL_DISABLED));
                if (builtInCamera != null)
                {
                    builtInCamera.camera_enabled = TRUE;
                    to_chat(this, span_notice(I18n.T("You hear your built in security camera focus adjust as it comes back online!")));
                }
                break;

            case BORG_CHOOSE_MODULE_TWO:
                if (!Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_TWO_DISABLED)))
                    return FALSE;

                disabled_modules = Dm.bitand(disabled_modules, Dm.bitnot(BORG_MODULE_TWO_DISABLED));
                break;

            case BORG_CHOOSE_MODULE_THREE:
                if (!Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_THREE_DISABLED)))
                    return FALSE;

                disabled_modules = Dm.bitand(disabled_modules, Dm.bitnot(BORG_MODULE_THREE_DISABLED));
                break;
        }

        to_chat(this, span_notice(I18n.T("ERROR CLEARED: Module [] back online.", module_num)));
        atom_movable_screen_robot_module_slot module = hud_used?.screen_objects?[datum_hud.HUD_KEY_HAND_SLOT(module_num)] as atom_movable_screen_robot_module_slot;
        if (module != null)
            module.icon_state = Dm.str(module.base_icon_state);
        return TRUE;
    }

    /**
     * Repairs all slots. Unbroken slots are unaffected.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:182
    public virtual void repair_all_cyborg_slots()
    {
        for (int cyborg_slot = 1; cyborg_slot <= 3; cyborg_slot++)
            repair_cyborg_slot(cyborg_slot);
    }

    /**
     * Unequips the active held item, if there is one.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:189
    public virtual async DmTask uneq_active()
    {
        if (module_active != null)
            await deactivate_module(module_active);
    }

    // Technically none of the items are dropped, only unequipped
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:194
    public override async DmTask<object> drop_all_held_items()
    {
        for (int cyborg_slot = 1; cyborg_slot <= length(held_items); cyborg_slot++)
        {
            if (!Dm.truthy(held_items[cyborg_slot]))
                continue;
            await deactivate_module(held_items[cyborg_slot] as obj_item);
        }
        return null;
    }

    /**
     * Checks if the item is currently in a slot.
     *
     * If the item is found in a slot, this returns TRUE. Otherwise, it returns FALSE
     * Arguments
     * * item_module - the item being checked
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:207
    public virtual object activated(obj_item item_module = null)
    {
        if (Dm.@in(item_module, held_items))
            return TRUE;
        if (Dm.@in(item_module.loc, held_items)) //Apparatus check
            return TRUE;
        return FALSE;
    }

    /**
     * Checks if the provided module number is a valid number.
     *
     * If the number is between 1 and 3 (if check_all_slots is true) or between 1 and the number of disabled
     * modules (if check_all_slots is false), then it returns FALSE. Otherwise, it returns TRUE.
     * Arguments
     * * module_num - the passed module num that is checked for validity.
     * * check_all_slots - TRUE = the proc checks all slots | FALSE = the proc only checks un-disabled slots
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:223
    public virtual object is_invalid_module_number(object module_num = null, float? check_all_slots = null)
    {
        check_all_slots ??= FALSE;
        if (!Dm.truthy(module_num))
            return TRUE;

        /// The number of module slots we're checking
        float max_number = 3;
        if (!Dm.truthy(check_all_slots))
        {
            if (Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_ALL_DISABLED)))
                max_number = 0;
            else if (Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_TWO_DISABLED)))
                max_number = 1;
            else if (Dm.truthy(Dm.bitand(disabled_modules, BORG_MODULE_THREE_DISABLED)))
                max_number = 2;
        }

        return Dm.num(module_num) < 1 || Dm.num(module_num) > max_number ? TRUE : FALSE;
    }

    /**
     * Returns the slot number of the selected module, or zero if no modules are selected.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:242
    public virtual object get_selected_module()
    {
        if (module_active != null)
            return held_items.Find(module_active);
        return FALSE;
    }

    /**
     * Selects the module in the slot module_num.
     * Arguments
     * * module_num - the slot number being selected
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:252
    public virtual object select_module(object module_num = null)
    {
        if (Dm.truthy(is_invalid_module_number(module_num)) || !Dm.truthy(held_items[module_num])) //If the slot number is invalid, or there's nothing there, we have nothing to equip
            return FALSE;

        atom_movable_screen_robot_module_slot module = hud_used?.screen_objects?[datum_hud.HUD_KEY_HAND_SLOT(module_num)] as atom_movable_screen_robot_module_slot;
        if (module != null && module_active != held_items[module_num])
            module.icon_state = Dm.text("[] +a", module.base_icon_state);
        module_active = held_items[module_num] as obj_item;
        return TRUE;
    }

    /**
     * Deselects the module in the slot module_num.
     * Arguments
     * * module_num - the slot number being de-selected
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:267
    public virtual object deselect_module(object module_num = null)
    {
        atom_movable_screen_robot_module_slot module = hud_used?.screen_objects?[datum_hud.HUD_KEY_HAND_SLOT(module_num)] as atom_movable_screen_robot_module_slot;
        if (module != null)
            module.icon_state = Dm.str(module.base_icon_state);
        module_active = null;
        return TRUE;
    }

    /**
     * Toggles selection of the module in the slot module_num.
     * Arguments
     * * module_num - the slot number being toggled
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:279
    public virtual object toggle_module(object module_num = null)
    {
        if (Dm.truthy(is_invalid_module_number(module_num)))
            return FALSE;

        if (Dm.eq(module_num, get_selected_module()))
        {
            deselect_module(module_num);
            return TRUE;
        }

        if (module_active != held_items[module_num])
            deselect_module(get_selected_module());

        return select_module(module_num);
    }

    /**
     * Cycles through the list of enabled modules, deselecting the current one and selecting the next one.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:295
    public virtual void cycle_modules()
    {
        float slot_start = Dm.num(get_selected_module());
        float slot_num;
        if (Dm.truthy(slot_start))
        {
            deselect_module(slot_start); //Only deselect if we have a selected slot.
            slot_num = slot_start + 1;
        }
        else
        {
            slot_num = 1;
            slot_start = 4;
        }

        while (slot_num != slot_start) //If we wrap around without finding any free slots, just give up.
        {
            if (Dm.truthy(select_module(slot_num)))
                return;
            slot_num++;
            if (slot_num > 4) // not >3 otherwise cycling with just one item on module 3 wouldn't work
                slot_num = 1; //Wrap around.
        }
    }

    // tg: code/modules/mob/living/silicon/robot/inventory.dm:312
    public override object perform_hand_swap(object held_index = null)
    {
        cycle_modules();
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/inventory.dm:316
    public override object can_hold_items(obj_item I = null)
    {
        return I != null && Dm.@in(I, model.modules) ? TRUE : FALSE; //Only if it's part of our model.
    }

    /**
     * ## Please do not use
     * Updates the observers's screens with cyborg items.
     * Currently inventory code handling for observers is tied to carbon (get_held_overlays), meaning this snowflake code for borgs is
     * necessary so observers watching borgs don't bug out. Once that's moved to the living, replace this with it.
     * Removing from the screen is handled by 'doUnEquip'
     * Arg:
     * * item_module - the item being added to the screen.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:328
    public virtual void observer_screen_update(obj_item item_module = null)
    {
        if (!Dm.truthy(observers?.len))
            return;
        foreach (mob_dead observe in Dm.each_cast<mob_dead>(observers.Copy()))
        {
            if (observe.client == null || observe.client.eye != this)
            {
                Dm.remove(observers, observe);
                if (!Dm.truthy(observers.len))
                {
                    observers = null;
                    return;
                }
            }
            Dm.append(observe.client.screen, item_module);
        }
    }
}

// tg: code/modules/mob/living/silicon/robot/inventory.dm:13
public partial class obj_item : obj
{
    /**
     * Parent proc - triggers when an item/module is unequipped from a cyborg.
     */
    // tg: code/modules/mob/living/silicon/robot/inventory.dm:13
    public virtual async DmTask cyborg_unequip(mob user = null)
    {
        return;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/strippable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8z slice B (D234-D238): the whole file. The var blocks are generated. ui_assets (the inventory placeholder pictures) travels no assets (DV44): the
// native window draws them from the client's own copy; the item pictures (icon2base64, :378) are the item's icon and icon_state in
// the payload, which the client draws from its own .dmi (DV150).

/// An element for atoms that, when dragged and dropped onto a mob, opens a strip panel.
// tg: code/datums/elements/strippable.dm:3
public partial class datum_element_strippable : datum_element
{
    // tg: code/datums/elements/strippable.dm:17
    [DmParams("target", "items", "should_strip_proc_path")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        DmList items = Dm.arg<DmList>(__extra, 0);
        object should_strip_proc_path = Dm.arg(__extra, 1);
        items ??= Dm.list();
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_MOUSEDROP_ONTO, nameof(mouse_drop_onto));

        this.items = items;
        this.should_strip_proc_path = should_strip_proc_path;
        return dot;
    }

    // tg: code/datums/elements/strippable.dm:27
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);

        UnregisterSignal(source, COMSIG_MOUSEDROP_ONTO);

        if (strip_menus != null)
        {
            qdel(strip_menus[source]);
            Dm.remove(ref strip_menus, source);
        }
        return dot;
    }

    // tg: code/datums/elements/strippable.dm:36
    [SignalHandler]
    public virtual object mouse_drop_onto(datum source = null, atom over = null, mob user = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(source._listen_lookup?[COMSIG_MOB_STRIP_MENU_OPEN]) ? (object)NONE : source._SendSignal(COMSIG_MOB_STRIP_MENU_OPEN, Dm.list(source, over, user)), COMPONENT_BLOCK_STRIP_MENU_OPEN)))
            return null;

        if (user == source)
            return null;
        if (over != user)
            return null;
        if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(source as atom, Dm.bitor(FORBID_TELEKINESIS_REACH, ALLOW_RESTING)))))
            return null;

        // Snowflake for cyborgs and bots buckling people by dragging them onto them, unless in combat mode.
        if (iscyborg(user) || isbot(user))
        {
            mob_living bot_user = user as mob_living;
            if (!Dm.truthy(bot_user.combat_mode))
                return null;
        }
        // Snowflake for xeno consumption code
        if (isalienadult(user))
        {
            mob_living_carbon_alien_adult alien = user as mob_living_carbon_alien_adult;
            if (alien != null && Dm.eq(alien.grab_state, GRAB_AGGRESSIVE) && alien.pulling == source)
                return null;
        }

        if (should_strip_proc_path != null && !Dm.truthy(Waiting.NoSleep(Dm.Call(source, should_strip_proc_path as string, user))))
            return null;

        datum_strip_menu strip_menu = LAZYACCESS(strip_menus, source) as datum_strip_menu;

        if (strip_menu == null)
        {
            strip_menu = Dm.@new<datum_strip_menu>(source, this);
            LAZYSET(ref strip_menus, source, strip_menu);
        }

        Waiting.Detach(strip_menu.ui_interact(user));
        return COMPONENT_CANCEL_MOUSEDROP_ONTO;
    }
}

/// A representation of an item that can be stripped down
// tg: code/datums/elements/strippable.dm:73
public partial class datum_strippable_item : datum
{
    /// Gets the item from the given source.
    // tg: code/datums/elements/strippable.dm:81
    public virtual object get_item(atom source = null)
    {
        return null;
    }

    /// Tries to equip the item onto the given source.
    /// Returns TRUE/FALSE depending on if it is allowed.
    /// This should be used for checking if an item CAN be equipped.
    /// It should not perform the equipping itself.
    // tg: code/datums/elements/strippable.dm:87
    public virtual async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(user._listen_lookup?[COMSIG_TRY_STRIP]) ? (object)NONE : user._SendSignal(COMSIG_TRY_STRIP, Dm.list(user, source, equipping)), COMPONENT_CANT_STRIP)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(source._listen_lookup?[COMSIG_BEING_STRIPPED]) ? (object)NONE : source._SendSignal(COMSIG_BEING_STRIPPED, Dm.list(source, user, equipping)), COMPONENT_CANT_STRIP)))
            return FALSE;

        if (HAS_TRAIT(equipping, TRAIT_NODROP))
        {
            to_chat(user, span_warning(I18n.T("You can't put [] on [], it's stuck to your hand!", equipping, source)));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(equipping.item_flags, ABSTRACT)))
            return FALSE; //I don't know a sane-sounding feedback message for trying to put a slap into someone's hand

        return TRUE;
    }

    /// Start the equipping process. This is the proc you should yield in.
    /// Returns TRUE/FALSE depending on if it is allowed.
    // tg: code/datums/elements/strippable.dm:104
    public virtual async DmTask<object> start_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        equipping.item_start_equip(source, equipping, user, show_visible_message);
        return TRUE;
    }

    /// The proc that places the item on the source. This should not yield.
    // tg: code/datums/elements/strippable.dm:110
    [ShouldNotSleep]
    public virtual object finish_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        return null;
    }

    /// Tries to unequip the item from the given source.
    /// Returns TRUE/FALSE depending on if it is allowed.
    /// This should be used for checking if it CAN be unequipped.
    /// It should not perform the unequipping itself.
    // tg: code/datums/elements/strippable.dm:117
    [ShouldNotSleep]
    public virtual object try_unequip(atom source = null, mob user = null)
    {
        obj_item item = get_item(source) as obj_item;
        if (item == null)
            return FALSE;

        if (ismob(source))
        {
            if (Dm.truthy(Dm.bitand(!Dm.truthy(user._listen_lookup?[COMSIG_TRY_STRIP]) ? (object)NONE : user._SendSignal(COMSIG_TRY_STRIP, Dm.list(user, source, item)), COMPONENT_CANT_STRIP)))
                return FALSE;
            if (Dm.truthy(Dm.bitand(!Dm.truthy(source._listen_lookup?[COMSIG_BEING_STRIPPED]) ? (object)NONE : source._SendSignal(COMSIG_BEING_STRIPPED, Dm.list(source, user, item)), COMPONENT_CANT_STRIP)))
                return FALSE;
            mob mob_source = source as mob;
            if (!Dm.truthy(item.canStrip(user, mob_source)))
                return FALSE;
        }

        return TRUE;
    }

    /// Start the unequipping process. This is the proc you should yield in.
    /// Returns TRUE/FALSE depending on if it is allowed.
    // tg: code/datums/elements/strippable.dm:137
    public virtual async DmTask<object> start_unequip(atom source = null, mob user = null)
    {
        obj_item item = get_item(source) as obj_item;
        if (item == null)
            return FALSE;

        if (HAS_TRAIT(item, TRAIT_NO_STRIP))
            return FALSE;

        source.visible_message(
            span_warning(I18n.T("[] tries to remove []'s [].", user, source, item.name)),
            span_userdanger(I18n.T("[] tries to remove your [].", user, item.name)),
            blind_message: span_hear(I18n.T("You hear rustling.")),
            ignored_mobs: user);

        to_chat(user, span_danger(I18n.T("You try to remove []'s []...", source, item.name)));
        user.log_message(Dm.text("is stripping [] of [].", key_name(source), item), LOG_ATTACK, color: "red");
        source.log_message(Dm.text("is being stripped of [] by [].", item, key_name(user)), LOG_VICTIM, color: "orange", log_globally: FALSE);
        item.add_fingerprint(null); // tg passes src (this datum) here, which is not a mob: add_fingerprint ignores it

        if (ishuman(source))
        {
            mob_living_carbon_human victim_human = source as mob_living_carbon_human;
            if (Dm.truthy(victim_human.key) && victim_human.client == null) // AKA braindead
            {
                if (!IS_UNCONSCIOUS(victim_human) && LAZYLEN(victim_human.afk_thefts) <= AFK_THEFT_MAX_MESSAGES)
                {
                    DmList new_entry = Dm.list(Dm.list(user.name, I18n.T("tried unequipping your []", item.name), world.time));
                    LAZYADD(ref victim_human.afk_thefts, new_entry);
                }
            }
            else if (Dm.truthy(victim_human.is_blind()))
                to_chat(source, span_userdanger(I18n.T("You feel someone fumble with your belongings.")));
        }

        return TRUE;
    }

    /// The proc that unequips the item from the source. This should not yield.
    // tg: code/datums/elements/strippable.dm:170
    public virtual async DmTask<object> finish_unequip(atom source = null, mob user = null)
    {
        return null;
    }

    /// Returns a STRIPPABLE_OBSCURING_* define to report on whether or not this is obscured.
    // tg: code/datums/elements/strippable.dm:173
    [ShouldNotSleep]
    public virtual object get_obscuring(atom source = null)
    {
        return STRIPPABLE_OBSCURING_NONE;
    }

    /**
     * Returns a list of alternate actions that can be performed on this strippable_item.
     * All string keys in the list must be inside tgui\packages\tgui\interfaces\StripMenu.tsx
     * You can also return null if there are no alternate actions.
     */
    // tg: code/datums/elements/strippable.dm:182
    [ShouldCallParent]
    public virtual object get_alternate_actions(atom source = null, mob user = null, obj_item item = null)
    {
        DmList alt_actions = Dm.list();
        if (item != null && Dm.truthy(item._listen_lookup?[COMSIG_ITEM_GET_STRIPPABLE_ALT_ACTIONS]))
            item._SendSignal(COMSIG_ITEM_GET_STRIPPABLE_ALT_ACTIONS, Dm.list(item, source, user, alt_actions));
        return alt_actions;
    }

    /**
     * Performs an alternate action on this strippable_item.
     * - source: The source of the action.
     * - user: The user performing the action.
     * - action_key: The key of the alternate action to perform.
     * Returns FALSE if unable to perform the action; whether it be due to the signal or some other factor.
     */
    // tg: code/datums/elements/strippable.dm:198
    [ShouldCallParent]
    public virtual async DmTask<object> perform_alternate_action(atom source = null, mob user = null, object action_key = null, obj_item item = null)
    {
        if (item != null && Dm.truthy(Dm.bitand(!Dm.truthy(item._listen_lookup?[COMSIG_ITEM_STRIPPABLE_ALT_ACTION]) ? (object)NONE : item._SendSignal(COMSIG_ITEM_STRIPPABLE_ALT_ACTION, Dm.list(item, source, user, action_key)), COMPONENT_ALT_ACTION_DONE)))
            return FALSE;
        return TRUE;
    }

    /// Returns whether or not this item should show.
    // tg: code/datums/elements/strippable.dm:205
    public virtual object should_show(atom source = null, mob user = null)
    {
        return TRUE;
    }
}

/// A preset for equipping items onto mob slots
// tg: code/datums/elements/strippable.dm:209
public partial class datum_strippable_item_mob_item_slot : datum_strippable_item
{
    // tg: code/datums/elements/strippable.dm:213
    public override object get_item(atom source = null)
    {
        if (!ismob(source))
            return null;

        mob mob_source = source as mob;
        return mob_source.get_item_by_slot(item_slot);
    }

    // tg: code/datums/elements/strippable.dm:220
    public override async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.try_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            return dot;

        if (!ismob(source))
            return FALSE;

        if (!Dm.truthy(await equipping.mob_can_equip(source as mob_living, item_slot, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] doesn't fit in that place!", equipping)));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/datums/elements/strippable.dm:234
    public override async DmTask<object> start_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.start_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            return dot;

        if (!ismob(source))
            return FALSE;

        if (!Dm.truthy(await do_after(user, get_equip_delay(equipping), source)))
            return FALSE;

        if (!Dm.truthy(await equipping.mob_can_equip(source as mob_living, item_slot, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))
            return FALSE;

        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(equipping)))
            return FALSE;

        return TRUE;
    }

    // tg: code/datums/elements/strippable.dm:253
    public override object finish_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        if (!ismob(source))
            return FALSE;

        mob mob_source = source as mob;
        Waiting.NoSleep(mob_source.equip_to_slot(equipping, item_slot));

        finish_equip_mob(equipping, source as mob, user);
        return null;
    }

    // tg: code/datums/elements/strippable.dm:262
    public override object get_obscuring(atom source = null)
    {
        if (!iscarbon(source))
            return STRIPPABLE_OBSCURING_NONE;

        mob_living_carbon carbon_source = source as mob_living_carbon;
        if (Dm.truthy(Dm.bitand(hidden_slots_to_inventory_slots(carbon_source.obscured_slots), item_slot)))
            return STRIPPABLE_OBSCURING_COMPLETELY;

        if (Dm.truthy(Dm.bitand(hidden_slots_to_inventory_slots(carbon_source.covered_slots), item_slot)))
            return STRIPPABLE_OBSCURING_INACCESSIBLE;

        return STRIPPABLE_OBSCURING_NONE;
    }

    // tg: code/datums/elements/strippable.dm:275
    public override async DmTask<object> start_unequip(atom source = null, mob user = null)
    {
        object dot = await base.start_unequip(source, user);
        if (!Dm.truthy(dot))
            return dot;

        return await start_unequip_mob(get_item(source) as obj_item, source as mob, user);
    }

    // tg: code/datums/elements/strippable.dm:282
    public override async DmTask<object> finish_unequip(atom source = null, mob user = null)
    {
        obj_item item = get_item(source) as obj_item;
        if (item == null)
            return FALSE;

        if (!ismob(source))
            return FALSE;

        return await finish_unequip_mob(item, source as mob, user);
    }

    /// Returns the delay of equipping this item to a mob
    // tg: code/datums/elements/strippable.dm:293
    public virtual object get_equip_delay(obj_item equipping = null)
    {
        return equipping.equip_delay_other;
    }
}

public static partial class GlobalProcs
{
    /// A utility function for `/datum/strippable_item`s to finish equipping an item to a mob.
    // tg: code/datums/elements/strippable.dm:297
    public static void finish_equip_mob(obj_item item = null, mob source = null, mob user = null)
    {
        user.log_message(Dm.text("has put [] on [].", item, key_name(source)), LOG_ATTACK, color: "red");
        source.log_message(Dm.text("had [] put on them by [].", item, key_name(user)), LOG_VICTIM, color: "orange", log_globally: FALSE);
    }

    /// A utility function for `/datum/strippable_item`s to start unequipping an item from a mob.
    // tg: code/datums/elements/strippable.dm:302
    public static async DmTask<object> start_unequip_mob(obj_item item = null, mob source = null, mob user = null, object strip_delay = null, float? hidden = null)
    {
        hidden ??= FALSE;
        if (!Dm.truthy(await do_after(user, (strip_delay is var or__1 && Dm.truthy(or__1) ? or__1 : item.strip_delay), source, interaction_key: REF(item), cog_icon: Dm.truthy(hidden) ? null : Dm.resource("icons/effects/progressbar.dmi"))))
            return FALSE;

        return TRUE;
    }

    /// A utility function for `/datum/strippable_item`s to finish unequipping an item from a mob.
    // tg: code/datums/elements/strippable.dm:309
    public static async DmTask<object> finish_unequip_mob(obj_item item = null, mob source = null, mob user = null)
    {
        if (!Dm.truthy(await item.doStrip(user, source)))
            return FALSE;

        user.log_message(Dm.text("has stripped [] of [].", key_name(source), item), LOG_ATTACK, color: "red");
        source.log_message(Dm.text("has been stripped of [] by [].", item, key_name(user)), LOG_VICTIM, color: "orange", log_globally: FALSE);
        return null;
    }

    /// Creates an assoc list of keys to /datum/strippable_item
    // tg: code/datums/elements/strippable.dm:522
    public static object create_strippable_list(object types = null)
    {
        DmList strippable_items = Dm.list();

        foreach (object strippable_type in Dm.each(types))
        {
            datum_strippable_item strippable_item = Dm.@new(strippable_type as Type) as datum_strippable_item;
            strippable_items[strippable_item.key] = strippable_item;
        }

        return strippable_items;
    }
}

/// A representation of the stripping UI
// tg: code/datums/elements/strippable.dm:317
public partial class datum_strip_menu : datum
{
    // tg: code/datums/elements/strippable.dm:327
    [DmParams("owner", "strippable")]
    public override async DmTask New(params object[] __extra)
    {
        atom_movable owner = Dm.arg<atom_movable>(__extra, 0);
        datum_element_strippable strippable = Dm.arg<datum_element_strippable>(__extra, 1);
        this.owner = owner;
        this.strippable = strippable;
    }

    // tg: code/datums/elements/strippable.dm:332
    public override object Destroy(float? force = null)
    {
        owner = null;
        strippable = null;

        return base.Destroy(force);
    }

    // tg: code/datums/elements/strippable.dm:338
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "StripMenu");
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/elements/strippable.dm:344
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/simple/inventory)): the slot pictures; the server sends no assets (DV44), the window draws
        // the client's own copy of icons/ui/inventory
        return Dm.list();
    }

    // tg: code/datums/elements/strippable.dm:349
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList items = Dm.list();

        foreach (object strippable_key in Dm.each(strippable.items))
        {
            datum_strippable_item item_data = strippable.items[strippable_key] as datum_strippable_item;

            if (!Dm.truthy(item_data.should_show(owner, user)))
                continue;

            DmList result = null;

            if (Dm.@in(strippable_key, LAZYACCESS(interactions, user)))
                LAZYSET(ref result, "interacting", TRUE);

            object obscuring = item_data.get_obscuring(owner);
            LAZYSET(ref result, "obscured", obscuring);
            if (!Dm.eq(obscuring, STRIPPABLE_OBSCURING_NONE) && !Dm.eq(obscuring, STRIPPABLE_OBSCURING_INACCESSIBLE))
            {
                items[strippable_key] = result;
                continue;
            }

            obj_item item = item_data.get_item(owner) as obj_item;
            if (item == null || (HAS_TRAIT(item, TRAIT_NO_STRIP) || HAS_TRAIT(item, TRAIT_EXAMINE_SKIP)))
            {
                items[strippable_key] = result;
                continue;
            }

            LAZYINITLIST(ref result);

            // DEVIATION(DV150): tg's result["icon"] is icon2base64(icon(item.icon, item.icon_state, frame = 1)), a picture; the native
            // window draws the item's own icon file and state (D236), so they are sent as they are.
            result["icon"] = item.icon;
            result["icon_state"] = item.icon_state;
            result["name"] = item.name;
            result["alternate"] = item_data.get_alternate_actions(owner, user, item);
            list_clear_nulls(result["alternate"] as DmList);
            DmList already_cried = TgState.Current.datum_strip_menu__ui_data__already_cried ??= Dm.list();
            if (length(result["alternate"]) > 3 && !Dm.@in(type, already_cried))
            {
                stack_trace(Dm.text("Too many alternate actions for []! Only three are supported at the moment! This will look bad!", type), "code/datums/elements/strippable.dm", 392);
                Dm.append(ref already_cried, type);
            }

            items[strippable_key] = result;
        }

        data["items"] = items;

        // While most `\the`s are implicit, this one is not.
        // In this case, `\The` would otherwise be used.
        // This doesn't match with what it's used for, which is to say "Stripping the alien drone",
        // as opposed to "Stripping The alien drone".
        // Human names will still show without "the", as they are proper nouns.
        data["name"] = Dm.text("\\the []", owner);

        return data;
    }

    // tg: code/datums/elements/strippable.dm:400
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        dot = TRUE;

        mob user = usr;

        if (Dm.eq(action, "use"))
        {
            object key = @params["key"];
            datum_strippable_item strippable_item = strippable.items[key] as datum_strippable_item;

            if (strippable_item == null)
                return dot;

            if (!Dm.truthy(strippable_item.should_show(owner, user)))
                return dot;

            object obscured = strippable_item.get_obscuring(owner);
            if (Dm.eq(obscured, STRIPPABLE_OBSCURING_COMPLETELY) || Dm.eq(obscured, STRIPPABLE_OBSCURING_INACCESSIBLE))
                return dot;

            object item = strippable_item.get_item(owner);
            if (item == null)
            {
                obj_item held_item = user.get_active_held_item() as obj_item;
                if (held_item == null)
                    return dot;

                if (Dm.truthy(await strippable_item.try_equip(owner, held_item, user)))
                {
                    LAZYORASSOCLIST(ref interactions, user, key);

                    // Yielding call
                    object should_finish = await strippable_item.start_equip(owner, held_item, user);

                    LAZYREMOVEASSOC(ref interactions, user, key);

                    if (!Dm.truthy(should_finish))
                        return dot;

                    if (QDELETED(this) || QDELETED(owner))
                        return dot;

                    // They equipped an item in the meantime
                    if (strippable_item.get_item(owner) != null)
                    {
                        await user.put_in_hands(held_item);
                        return dot;
                    }

                    if (!Dm.truthy(await user.Adjacent(owner)))
                    {
                        await user.put_in_hands(held_item);
                        return dot;
                    }

                    strippable_item.finish_equip(owner, held_item, user);
                }
            }
            else if (Dm.truthy(strippable_item.try_unequip(owner, user)))
            {
                LAZYORASSOCLIST(ref interactions, user, key);

                object should_unequip = await strippable_item.start_unequip(owner, user);

                LAZYREMOVEASSOC(ref interactions, user, key);

                // Yielding call
                if (!Dm.truthy(should_unequip))
                    return dot;

                if (QDELETED(this) || QDELETED(owner))
                    return dot;

                // They changed the item in the meantime
                if (strippable_item.get_item(owner) != item)
                    return dot;

                if (!Dm.truthy(await user.Adjacent(owner)))
                    return dot;

                await strippable_item.finish_unequip(owner, user);
            }
        }
        else if (Dm.eq(action, "alt"))
        {
            object key = @params["key"];
            object alt_action = @params["alternate_action"];
            datum_strippable_item strippable_item = strippable.items[key] as datum_strippable_item;

            if (strippable_item == null)
                return dot;

            if (!Dm.truthy(strippable_item.should_show(owner, user)))
                return dot;

            object obscured = strippable_item.get_obscuring(owner);
            if (Dm.eq(obscured, STRIPPABLE_OBSCURING_COMPLETELY) || Dm.eq(obscured, STRIPPABLE_OBSCURING_INACCESSIBLE))
                return dot;

            obj_item item = strippable_item.get_item(owner) as obj_item;

            if (!Dm.@in(alt_action, strippable_item.get_alternate_actions(owner, user, item)))
                return dot;

            LAZYORASSOCLIST(ref interactions, user, key);

            // Potentially yielding
            await strippable_item.perform_alternate_action(owner, user, alt_action, item);

            LAZYREMOVEASSOC(ref interactions, user, key);
        }
        return dot;
    }

    // tg: code/datums/elements/strippable.dm:503
    public override object ui_host(mob user = null)
    {
        return owner;
    }

    // tg: code/datums/elements/strippable.dm:506
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/datums/elements/strippable.dm:509
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        return Dm.min(
            ui_status_only_living(user, owner),
            ui_status_user_has_free_hands(user, owner),
            ui_status_user_is_adjacent(user, owner, allow_tk: FALSE),
            HAS_TRAIT(user, TRAIT_CAN_STRIP) ? UI_INTERACTIVE : UI_UPDATE,
            Dm.max(
                ui_status_user_is_conscious_and_lying_down(user),
                ui_status_user_is_abled(user, owner)
            )
        );
    }
}

public sealed partial class TgState
{
    // tg: code/datums/elements/strippable.dm:370  var/static/list/already_cried (a proc-level static, rules 10.6)
    internal DmList datum_strip_menu__ui_data__already_cried;
}

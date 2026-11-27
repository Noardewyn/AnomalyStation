// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/_module.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The MOD module base: its required slots, its selection (from the window, the radial or
// a pinned button), activation (an active module extends its device into the wearer's hand, or arms a middle- or alt-click use,
// the wearer's mod_select preference), deactivation, use with its cooldown, its idle and active power drain on the suit's
// process, its worn overlay (drawn on the part it needs, masked to the suit when it asks), its configuration in the window and its
// pin on the action bar; the anomaly-locked module's anomaly core slot came with the MOD modules (gaps MO2,
// datums/components/anomalock_module.dm). The var blocks are generated (D111).

// tg: code/modules/mod/modules/_module.dm:3
public partial class obj_item_mod_module : obj_item_mod
{
    // tg: code/modules/mod/modules/_module.dm:65
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.eq(module_type, MODULE_ACTIVE))
            return dot;
        if (ispath(device))
        {
            device = Dm.@new(device as Type, this);
            ADD_TRAIT(device, TRAIT_NODROP, MOD_TRAIT);
            RegisterSignal(device as datum, COMSIG_QDELETING, nameof(on_device_deletion));
            RegisterSignal(this, COMSIG_ATOM_EXITED, nameof(on_exit));
        }
        return dot;
    }

    // tg: code/modules/mod/modules/_module.dm:75
    public override object Destroy(float? force = null)
    {
        if (mod != null)
            Waiting.NoSleep(mod.uninstall(this));
        if (device != null)
        {
            // The port's guard: an inert module subtype (a space ninja's, M10+) never ran this Initialize, so its device is still the typepath.
            if (device is datum made_device)
            {
                UnregisterSignal(made_device, COMSIG_QDELETING);
                qdel(made_device); // QDEL_NULL(device)
            }
            device = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/_module.dm:82
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(length(required_slots)))
        {
            DmList slot_strings = Dm.list();
            foreach (object slot in Dm.each(required_slots))
            {
                DmList slot_list = parse_slot_flags(slot) as DmList;
                Dm.append(ref slot_strings, Dm.text("[][]", Dm.eq(length(slot_list), 1) ? "" : I18n.T("one of "), english_list(slot_list, and_text: I18n.T(" or "))));
            }
            Dm.append(ref dot, span_notice(I18n.T("Requires the MOD unit to have the following slots: []", english_list(slot_strings))));
        }
        if (HAS_TRAIT(user, TRAIT_DIAGNOSTIC_HUD))
            Dm.append(ref dot, span_notice(I18n.T("Complexity level: []", complexity)));
        return dot;
    }

    /// Looks through the MODsuit's parts to see if it has the parts required to support this module
    // tg: code/modules/mod/modules/_module.dm:94
    public virtual object has_required_parts(DmList parts = null, float? need_active = null)
    {
        need_active ??= FALSE;
        if (!Dm.truthy(length(required_slots)))
            return TRUE;
        float total_slot_flags = NONE;
        foreach (object part_slot in Dm.each(parts))
        {
            if (Dm.truthy(need_active))
            {
                datum_mod_part part_datum = parts[part_slot] as datum_mod_part;
                if (!Dm.truthy(part_datum.@sealed))
                    continue;
            }
            total_slot_flags = Dm.num(Dm.bitor(total_slot_flags, text2num(part_slot)));
        }
        DmList needed_slots = required_slots.Copy();
        foreach (object needed_slot in Dm.each(needed_slots.Copy()))
        {
            if (!Dm.truthy(Dm.bitand(needed_slot, total_slot_flags)))
                break;
            Dm.remove(ref needed_slots, needed_slot);
        }
        return Dm.num(!Dm.truthy(length(needed_slots)));
    }

    /// Additional checks for whenever a module can be installed into a suit or not
    // tg: code/modules/mod/modules/_module.dm:112
    public virtual object can_install(obj_item_mod_control mod = null)
    {
        return TRUE;
    }

    /// Called when the module is selected from the TGUI, radial or the action button
    // tg: code/modules/mod/modules/_module.dm:116
    public virtual async DmTask on_select(mob activator = null)
    {
        if (mod.wearer == null && !Dm.truthy(Dm.bitand(allow_flags, MODULE_ALLOW_UNWORN))) //No wearer and cannot be used unworn
        {
            balloon_alert(activator, I18n.T("not equipped!"));
            return;
        }
        if (((!Dm.truthy(mod.active) || Dm.truthy(mod.activating)) && !Dm.truthy(Dm.bitand(allow_flags, Dm.bitor(MODULE_ALLOW_INACTIVE, MODULE_ALLOW_UNWORN)))) || Dm.eq(module_type, MODULE_PASSIVE)) // not active
        {
            balloon_alert(activator, I18n.T("not active!"));
            return;
        }
        if (!Dm.truthy(has_required_parts(mod.mod_parts, need_active: TRUE)) && !Dm.truthy(Dm.bitand(allow_flags, MODULE_ALLOW_UNWORN))) // Doesn't have parts
        {
            balloon_alert(activator, I18n.T("required parts inactive!"));
            DmList slot_strings = Dm.list();
            foreach (object slot in Dm.each(required_slots))
            {
                DmList slot_list = parse_slot_flags(slot) as DmList;
                Dm.append(ref slot_strings, Dm.text("[][]", Dm.eq(length(slot_list), 1) ? "" : I18n.T("one of "), english_list(slot_list, and_text: I18n.T(" or "))));
            }
            to_chat(activator, span_warning(I18n.T("[] requires these slots to be deployed: []", this, english_list(slot_strings))));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return;
        }
        if (!Dm.eq(module_type, MODULE_USABLE))
        {
            if (Dm.truthy(active))
                await deactivate(activator);
            else
                await activate(activator);
        }
        else
            await used(activator);
        mod.SEND_SIGNAL(COMSIG_MOD_MODULE_SELECTED, Dm.list(mod, this));
    }

    /// Apply a cooldown until this item can be used again
    // tg: code/modules/mod/modules/_module.dm:142
    public virtual void start_cooldown(object applied_cooldown = null)
    {
        if ((applied_cooldown == null))
            applied_cooldown = cooldown_time;
        cooldown_timer = world.time + Dm.num(applied_cooldown); // COOLDOWN_START(src, cooldown_timer, applied_cooldown)
        SEND_SIGNAL(COMSIG_MODULE_COOLDOWN_STARTED, Dm.list(this, applied_cooldown));
    }

    /// Called when the module is activated
    // tg: code/modules/mod/modules/_module.dm:149
    public virtual async DmTask<object> activate(mob activator = null)
    {
        if (!(cooldown_timer <= world.time)) // !COOLDOWN_FINISHED(src, cooldown_timer)
        {
            balloon_alert(activator, I18n.T("on cooldown!"));
            return FALSE;
        }
        if (!Dm.truthy(mod.active) || Dm.truthy(mod.activating) || !Dm.truthy(mod.get_charge()))
        {
            balloon_alert(activator, I18n.T("unpowered!"));
            return FALSE;
        }
        if (!Dm.truthy(Dm.bitand(allow_flags, MODULE_ALLOW_PHASEOUT)) && istype(mod.wearer.loc, typeof(obj_effect_dummy_phased_mob)))
        {
            //specifically a to_chat because the user is phased out.
            to_chat(activator, span_warning(I18n.T("You cannot activate this right now.")));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MODULE_TRIGGERED, Dm.list(this, mod.wearer)), MOD_ABORT_USE)))
            return FALSE;
        if (Dm.eq(module_type, MODULE_ACTIVE))
        {
            if (mod.selected_module != null && !Dm.truthy(await mod.selected_module.deactivate(display_message: FALSE)))
                return FALSE;
            mod.selected_module = this;
            if (device != null)
            {
                if (Dm.truthy(await mod.wearer.put_in_hands(device as obj_item)))
                {
                    balloon_alert(activator, I18n.T("[] extended", device));
                    RegisterSignal(mod.wearer, COMSIG_ATOM_EXITED, nameof(on_exit));
                    RegisterSignal(mod.wearer, COMSIG_KB_MOB_DROPITEM_DOWN, nameof(dropkey));
                }
                else
                {
                    balloon_alert(activator, I18n.T("can't extend []!", device));
                    await mod.wearer.transferItemToLoc(device as obj_item, this, force: TRUE);
                    return FALSE;
                }
            }
            else
            {
                object used_button = mod.wearer.client?.prefs.read_preference(typeof(datum_preference_choiced_mod_select)) is var or__1 && Dm.truthy(or__1) ? or__1 : MIDDLE_CLICK;
                update_signal(used_button);
                balloon_alert(mod.wearer, I18n.T("[] activated, []-click to use", this, used_button)); // As of now, only wearers can "use" mods
            }
        }
        active = TRUE;
        SEND_SIGNAL(COMSIG_MODULE_ACTIVATED, Dm.list(this));
        mod.SEND_SIGNAL(COMSIG_MOD_MODULE_ACTIVATED, Dm.list(mod, this));
        await on_activation(activator);
        await update_clothing_slots();
        return TRUE;
    }

    /// Called when the module is deactivated
    // tg: code/modules/mod/modules/_module.dm:187
    public virtual async DmTask<object> deactivate(mob activator = null, float? display_message = null, float? deleting = null)
    {
        display_message ??= TRUE;
        deleting ??= FALSE;
        active = FALSE;
        if (Dm.eq(module_type, MODULE_ACTIVE))
        {
            mod.selected_module = null;
            if (Dm.truthy(display_message))
                balloon_alert(mod.wearer, device != null ? I18n.T("[] retracted", device) : I18n.T("[] deactivated", this));
            if (device != null)
            {
                await mod.wearer.transferItemToLoc(device as obj_item, this, force: TRUE);
                UnregisterSignal(mod.wearer, COMSIG_ATOM_EXITED);
                UnregisterSignal(mod.wearer, COMSIG_KB_MOB_DROPITEM_DOWN);
            }
            else
            {
                UnregisterSignal(mod.wearer, used_signal);
                used_signal = null;
            }
        }
        SEND_SIGNAL(COMSIG_MODULE_DEACTIVATED, Dm.list(this, mod.wearer));
        mod.SEND_SIGNAL(COMSIG_MOD_MODULE_DEACTIVATED, Dm.list(mod, this));
        await on_deactivation(activator, display_message: TRUE, deleting: FALSE); // tg passes TRUE and FALSE here, not its own arguments
        await update_clothing_slots();
        return TRUE;
    }

    /// Call to update all slots visually affected by this module
    // tg: code/modules/mod/modules/_module.dm:207
    public virtual async DmTask update_clothing_slots()
    {
        if (mod.wearer == null)
            return;

        object updated_slots = mod.slot_flags;
        if (Dm.truthy(mask_worn_overlay))
        {
            foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts()))
                updated_slots = Dm.bitor(updated_slots, part.slot_flags);
        }
        else if (Dm.truthy(length(required_slots)))
        {
            foreach (object slot in Dm.each(required_slots))
                updated_slots = Dm.bitor(updated_slots, slot);
        }
        await mod.wearer.update_clothing(updated_slots);
    }

    /// Called when the module is used
    // tg: code/modules/mod/modules/_module.dm:221
    public virtual async DmTask<object> used(mob activator = null)
    {
        if (!(cooldown_timer <= world.time)) // !COOLDOWN_FINISHED(src, cooldown_timer)
        {
            balloon_alert(activator, I18n.T("on cooldown!"));
            return FALSE;
        }
        if (!Dm.truthy(check_power(use_energy_cost)))
        {
            balloon_alert(activator, I18n.T("not enough charge!"));
            return FALSE;
        }
        if (!Dm.truthy(Dm.bitand(allow_flags, MODULE_ALLOW_PHASEOUT)) && istype(mod.wearer.loc, typeof(obj_effect_dummy_phased_mob)))
        {
            //specifically a to_chat because the user is phased out.
            to_chat(activator, span_warning(I18n.T("You cannot activate this right now.")));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MODULE_TRIGGERED, Dm.list(this, mod.wearer)), MOD_ABORT_USE)))
            return FALSE;
        start_cooldown();
        if (mod.wearer != null)
            _addtimer(Dm.@new<datum_callback>(mod.wearer, nameof(mob.update_clothing), mod.slot_flags), Dm.num(cooldown_time) + 1, file: "code/modules/mod/modules/_module.dm", line: 237); //need to run it a bit after the cooldown starts to avoid conflicts
        await update_clothing_slots();
        SEND_SIGNAL(COMSIG_MODULE_USED, Dm.list(this));
        await on_use(activator);
        return TRUE;
    }

    /// Called when an activated module without a device is used
    // tg: code/modules/mod/modules/_module.dm:243
    public virtual async DmTask<object> on_select_use(atom target = null)
    {
        if (!Dm.truthy(Dm.bitand(allow_flags, MODULE_ALLOW_INCAPACITATED)) && Dm.truthy(INCAPACITATED_IGNORING(mod.wearer, INCAPABLE_GRAB)))
            return FALSE;
        mod.wearer.face_atom(target);
        if (!Dm.truthy(await used()) || Dm.truthy(Dm.bitand(target.SEND_SIGNAL(COMSIG_ATOM_MOD_MODULE_USED, Dm.list(target, this)), COMPONENT_INTERRUPT_MODULE_USE)))
            return FALSE;
        return TRUE;
    }

    /// Called when an activated module without a device is active and the user alt/middle-clicks
    // tg: code/modules/mod/modules/_module.dm:252
    [SignalHandler]
    public virtual object on_special_click(mob source = null, atom target = null)
    {
        // tg sleeps here when the use waits (the drill's dig, a do_after): BYOND holds the click until it ends and cancels it either way,
        // so the use runs on detached (INVOKE_ASYNC) and the click is cancelled now
        Waiting.Detach(on_select_use(target));
        return COMSIG_MOB_CANCEL_CLICKON;
    }

    /// Called on the MODsuit's process
    // tg: code/modules/mod/modules/_module.dm:258
    public virtual async DmTask<object> on_process(object seconds_per_tick = null)
    {
        if (Dm.truthy(part_process) && !Dm.truthy(part_activated))
            return FALSE;
        if (Dm.truthy(active))
        {
            if (!Dm.truthy(await drain_power(active_power_cost * Dm.num(seconds_per_tick))))
            {
                await deactivate();
                return FALSE;
            }
            await on_active_process(seconds_per_tick);
        }
        else
            await drain_power(idle_power_cost * Dm.num(seconds_per_tick));
        return TRUE;
    }

    /// Called from the module's activate()
    // tg: code/modules/mod/modules/_module.dm:271
    public virtual async DmTask on_activation(mob activator = null)
    {
    }

    /// Called from the module's deactivate()
    // tg: code/modules/mod/modules/_module.dm:275
    public virtual async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
    }

    /// Called from the module's used()
    // tg: code/modules/mod/modules/_module.dm:279
    public virtual async DmTask<object> on_use(mob activator = null)
    {
        return null;
    }

    /// Called on the MODsuit's process if it is an active module
    // tg: code/modules/mod/modules/_module.dm:283
    public virtual async DmTask on_active_process(object seconds_per_tick = null)
    {
    }

    /// Called from MODsuit's install() proc, so when the module is installed
    // tg: code/modules/mod/modules/_module.dm:287
    public virtual async DmTask on_install()
    {
        if (Dm.truthy(mask_worn_overlay))
        {
            foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts(all: TRUE)))
                RegisterSignal(part, COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS, nameof(add_module_overlay));
            return;
        }

        if (!Dm.truthy(length(required_slots)))
        {
            RegisterSignal(mod, COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS, nameof(add_module_overlay));
            return;
        }

        obj_item part_item = mod.get_part_from_slot(required_slots[1]) as obj_item;
        RegisterSignal(part_item, COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS, nameof(add_module_overlay));
    }

    /// Called from MODsuit's uninstall() proc, so when the module is uninstalled
    // tg: code/modules/mod/modules/_module.dm:303
    public virtual async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        if (Dm.truthy(mask_worn_overlay))
        {
            foreach (obj_item part in Dm.each_cast<obj_item>(mod.get_parts(all: TRUE)))
                UnregisterSignal(part, COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS);
            return;
        }

        if (!Dm.truthy(length(required_slots)))
        {
            UnregisterSignal(mod, COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS);
            return;
        }

        obj_item part_item = mod.get_part_from_slot(required_slots[1]) as obj_item;
        UnregisterSignal(part_item, COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS);
    }

    /// Called when the MODsuit is activated
    // tg: code/modules/mod/modules/_module.dm:319
    public virtual async DmTask on_part_activation()
    {
    }

    /// Called when the MODsuit is deactivated
    // tg: code/modules/mod/modules/_module.dm:323
    public virtual async DmTask on_part_deactivation(float? deleting = null)
    {
    }

    /// Called when the MODsuit is equipped
    // tg: code/modules/mod/modules/_module.dm:327
    public virtual void on_equip()
    {
    }

    /// Called when the MODsuit is unequipped
    // tg: code/modules/mod/modules/_module.dm:331
    public virtual void on_unequip()
    {
    }

    /// Drains power from the suit charge
    // tg: code/modules/mod/modules/_module.dm:335
    public virtual async DmTask<object> drain_power(object amount = null)
    {
        if (!Dm.truthy(check_power(amount)))
            return FALSE;
        await mod.subtract_charge(amount);
        return TRUE;
    }

    /// Checks if there is enough power in the suit
    // tg: code/modules/mod/modules/_module.dm:342
    public virtual object check_power(object amount = null)
    {
        return mod.check_charge(amount);
    }

    /// Adds additional things to the MODsuit ui_data()
    // tg: code/modules/mod/modules/_module.dm:346
    public virtual object add_ui_data()
    {
        return Dm.list();
    }

    /// Creates a list of configuring options for this module, possible configs include number, bool, color, list, button.
    // tg: code/modules/mod/modules/_module.dm:350
    public virtual object get_configuration(mob user = null)
    {
        return Dm.list();
    }

    /// Generates an element of the get_configuration list with a display name, type and value
    // tg: code/modules/mod/modules/_module.dm:354
    public virtual object add_ui_configuration(object display_name = null, object type = null, object value = null, DmList values = null)
    {
        return Dm.list(Dm.kv("display_name", display_name), Dm.kv("type", type), Dm.kv("value", value), Dm.kv("values", values));
    }

    /// Receives configure edits from the TGUI and edits the vars
    // tg: code/modules/mod/modules/_module.dm:358
    public virtual async DmTask configure_edit(object key = null, object value = null)
    {
    }

    /// Called when the device moves to a different place on active modules
    // tg: code/modules/mod/modules/_module.dm:362
    [SignalHandler]
    public virtual void on_exit(datum source = null, atom_movable part = null, object direction = null)
    {
        if (!Dm.truthy(active))
            return;
        if (part.loc == this)
            return;
        if (part.loc == mod.wearer)
            return;
        if (part == device)
            Waiting.NoSleep(deactivate(display_message: FALSE));
    }

    /// Called when the device gets deleted on active modules
    // tg: code/modules/mod/modules/_module.dm:375
    [SignalHandler]
    public virtual void on_device_deletion(datum source = null)
    {
        if (source == device)
        {
            Waiting.NoSleep((device as atom_movable).moveToNullspace());
            device = null;
            qdel(this);
        }
    }

    /// Adds the worn overlays to the suit.
    // tg: code/modules/mod/modules/_module.dm:384
    [SignalHandler]
    public virtual void add_module_overlay(obj_item source = null, DmList overlays = null, mutable_appearance standing = null, mutable_appearance draw_target = null, object isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        if (Dm.truthy(isinhands))
            return;

        DmList added_overlays = generate_worn_overlay(source, standing) as DmList;
        if (!Dm.truthy(added_overlays))
            return;

        if (!Dm.truthy(mask_worn_overlay))
        {
            Dm.append(ref overlays, added_overlays);
            return;
        }

        foreach (mutable_appearance overlay in Dm.each_cast<mutable_appearance>(added_overlays))
        {
            overlay.add_filter("mod_mask_overlay", 1, alpha_mask_filter(icon: icon(draw_target.icon, draw_target.icon_state)) as DmList);
            Dm.append(ref overlays, overlay);
        }
    }

    /// Generates an icon to be used for the suit's worn overlays
    // tg: code/modules/mod/modules/_module.dm:403
    public virtual object generate_worn_overlay(obj_item source = null, mutable_appearance standing = null)
    {
        if (!Dm.truthy(mask_worn_overlay))
        {
            if (!Dm.truthy(has_required_parts(mod.mod_parts, need_active: TRUE)))
                return null;
        }
        else
        {
            datum_mod_part part_datum = mod.get_part_datum(source) as datum_mod_part;
            if (!Dm.truthy(part_datum?.@sealed))
                return null;
        }

        DmList dot = Dm.list();
        object used_overlay = get_current_overlay_state();
        if (!Dm.truthy(used_overlay))
            return dot;

        mutable_appearance module_icon = mutable_appearance(overlay_icon_file, used_overlay as string, layer: standing.layer + 0.1f) as mutable_appearance;
        if (Dm.truthy(use_mod_colors))
        {
            module_icon.color = mod.color;
            if (Dm.truthy(mod.cached_color_filter))
                module_icon = filter_appearance_recursive(module_icon, mod.cached_color_filter);
        }

        Dm.append(ref dot, module_icon);
        SEND_SIGNAL(COMSIG_MODULE_GENERATE_WORN_OVERLAY, Dm.list(this, dot, standing));
        return dot;
    }

    // tg: code/modules/mod/modules/_module.dm:426
    public virtual object get_current_overlay_state()
    {
        if (Dm.truthy(overlay_state_use) && !(cooldown_timer <= world.time))
            return overlay_state_use;
        if (Dm.truthy(overlay_state_active) && Dm.truthy(active))
            return overlay_state_active;
        if (Dm.truthy(overlay_state_inactive))
            return overlay_state_inactive;
        return null;
    }

    /// Updates the signal used by active modules to be activated
    // tg: code/modules/mod/modules/_module.dm:436
    public virtual void update_signal(object value = null)
    {
        if (Dm.eq(value, MIDDLE_CLICK))
            mod.selected_module.used_signal = COMSIG_MOB_MIDDLECLICKON;
        else if (Dm.eq(value, ALT_CLICK))
            mod.selected_module.used_signal = COMSIG_MOB_ALTCLICKON;
        RegisterSignal(mod.wearer, mod.selected_module.used_signal, nameof(on_special_click));
    }

    /// Pins the module to the user's action buttons
    // tg: code/modules/mod/modules/_module.dm:445
    public virtual async DmTask pin(mob user = null)
    {
        if (Dm.eq(module_type, MODULE_PASSIVE))
            return;

        datum_action_item_action_mod_pinnable_module existing_action = pinned_to[REF(user)] as datum_action_item_action_mod_pinnable_module;
        if (existing_action != null)
        {
            mod.remove_item_action(existing_action);
            return;
        }

        datum_action_item_action_mod_pinnable_module new_action = await Dm.new_async<datum_action_item_action_mod_pinnable_module>(mod, user, this);
        await mod.add_item_action(new_action);
    }

    /// On drop key, concels a device item.
    // tg: code/modules/mod/modules/_module.dm:458
    [SignalHandler]
    public virtual object dropkey(mob_living user = null)
    {
        if (user.get_active_held_item() != device)
            return null;
        Waiting.NoSleep(deactivate());
        return COMSIG_KB_ACTIVATED;
    }
}

///Anomaly Locked - Mostly just a wrapper for modules that don't need to descend from any other module but need the anomaly_locked_module component
// tg: code/modules/mod/modules/_module.dm:467
public partial class obj_item_mod_module_anomaly_locked : obj_item_mod_module
{
    // tg: code/modules/mod/modules/_module.dm:477
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_anomaly_locked_module), accepted_anomalies, prebuilt, core_removable));
        return dot;
    }
}

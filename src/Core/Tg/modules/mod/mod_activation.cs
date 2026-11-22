// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_activation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. Deploying the MODsuit's parts onto the wearer (one by one from tg's radial menu, or all at once
// with a mechanical hiss), retracting them into the control unit (an item worn under a part, the overslot, is put back), and the
// activation sequence: each deployed part seals in turn (MOD_ACTIVATION_STEP_TIME a part, its seal message and mechmove03), then
// the control unit, with synth_yes and the "nominal" voice; the sealed parts take their visor flags, sealed icon state and layer,
// and the modules that need them are activated. The var block is generated (D111).

public partial class obj_item_mod_control : obj_item_mod
{
    // tg: code/modules/mod/mod_activation.dm:1  #define MOD_ACTIVATION_STEP_FLAGS (file-local, #undef at :351)
    private const int MOD_ACTIVATION_STEP_FLAGS = IGNORE_USER_LOC_CHANGE | IGNORE_TARGET_LOC_CHANGE | IGNORE_HELD_ITEM | IGNORE_INCAPACITATED | IGNORE_SLOWDOWNS;

    /// Creates a radial menu from which the user chooses parts of the suit to deploy/retract. Repeats until all parts are extended or retracted.
    // tg: code/modules/mod/mod_activation.dm:4
    public virtual async DmTask choose_deploy(mob user = null)
    {
        if (!Dm.truthy(length(mod_parts)))
            return;
        DmList display_names = Dm.list();
        DmList items = Dm.list();
        DmList parts = get_parts() as DmList;
        foreach (obj_item part in Dm.each_cast<obj_item>(parts))
        {
            display_names[part.name] = REF(part);
            image part_image = image(icon: part.icon, icon_state: part.icon_state);
            if (part.loc != this)
                Dm.append(ref part_image.underlays, image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "module_active"));
            Dm.append(ref items, Dm.list(Dm.kv(part.name, part_image)));
        }
        object pick = await show_radial_menu(user, this, items, custom_check: null, require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(pick))
            return;
        object part_reference = display_names[pick];
        obj_item part__1 = Dm.locate_ref(part_reference) is obj_item found && Dm.@in(found, get_parts()) ? found : null; // locate(part_reference) in get_parts()
        if (!(part__1 is obj_item) || Dm.truthy(user.incapacitated))
            return;
        if (Dm.truthy(activating))
        {
            balloon_alert(user, I18n.T("currently []!", Dm.truthy(active) ? I18n.T("unsealing") : I18n.T("sealing")));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return;
        }
        object parts_to_check = Dm.sub(parts, part__1);
        if (part__1.loc == this)
        {
            if (!Dm.truthy(await deploy(user, part__1)))
                return;
            SEND_SIGNAL(COMSIG_MOD_DEPLOYED, Dm.list(this, user));
            foreach (obj_item checking_part in Dm.each_cast<obj_item>(parts_to_check))
            {
                if (checking_part.loc != this)
                    continue;
                await choose_deploy(user);
                break;
            }
        }
        else
        {
            if (!Dm.truthy(await retract(user, part__1)))
                return;
            SEND_SIGNAL(COMSIG_MOD_RETRACTED, Dm.list(this, user));
            foreach (obj_item checking_part in Dm.each_cast<obj_item>(parts_to_check))
            {
                if (checking_part.loc == this)
                    continue;
                await choose_deploy(user);
                break;
            }
        }
    }

    /// Quickly deploys all parts (or retracts if all are on the wearer)
    // tg: code/modules/mod/mod_activation.dm:48
    public virtual async DmTask<object> quick_deploy(mob user = null)
    {
        if (Dm.truthy(activating))
        {
            balloon_alert(user, I18n.T("currently []!", Dm.truthy(active) ? I18n.T("unsealing") : I18n.T("sealing")));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return FALSE;
        }
        object deploy__1 = check_retracted();
        wearer.visible_message(span_notice(I18n.T("[]'s [] [] its parts with a mechanical hiss.", wearer, this, Dm.truthy(deploy__1) ? I18n.T("deploys") : I18n.T("retracts"))),
            span_notice(I18n.T("[] [] its parts with a mechanical hiss.", this, Dm.truthy(deploy__1) ? I18n.T("deploys") : I18n.T("retracts"))),
            span_hear(I18n.T("You hear a mechanical hiss.")));
        playsound(this, Dm.resource("sound/vehicles/mecha/mechmove03.ogg"), 25, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts()))
        {
            if (Dm.truthy(deploy__1) && part.loc == this)
                await deploy(null, part);
            else if (!Dm.truthy(deploy__1) && part.loc != this)
                await retract(null, part);
        }
        if (Dm.truthy(deploy__1))
            SEND_SIGNAL(COMSIG_MOD_DEPLOYED, Dm.list(this, user));
        else
            SEND_SIGNAL(COMSIG_MOD_RETRACTED, Dm.list(this, user));
        return TRUE;
    }

    /// Deploys a part of the suit onto the user.
    // tg: code/modules/mod/mod_activation.dm:70
    public virtual async DmTask<object> deploy(mob user = null, obj_item part = null, float? instant = null)
    {
        instant ??= FALSE;
        datum_mod_part part_datum = get_part_datum(part) as datum_mod_part;
        if (wearer == null)
        {
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return FALSE; // pAI is trying to deploy it from your hands
        }
        if (part.loc != this)
        {
            if (user == null)
                return FALSE;
            balloon_alert(user, I18n.T("already deployed!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
        }
        obj_item overslot = wearer.get_item_by_slot(part.slot_flags) as obj_item;
        if (Dm.truthy(Dm.bitand(overslot?.item_flags, CAN_BE_OVERSLOT)))
        {
            if (Dm.truthy(await transfer_part_to_loc(overslot, part, force: TRUE, preserve_suit_storage: Dm.num_or_null(can_preserve_suit_storage(part, wearer.s_store)))))
            {
                part_datum.overslotting = overslot;
                RegisterSignal(part, COMSIG_ATOM_EXITED, nameof(on_overslot_exit));
            }
        }
        if (Dm.truthy(await wearer.equip_to_slot_if_possible(part, part.slot_flags, qdel_on_fail: FALSE, disable_warning: TRUE)))
        {
            ADD_TRAIT(part, TRAIT_NODROP, MOD_TRAIT);
            await wearer.update_clothing(Dm.bitor(slot_flags, part.slot_flags));
            SEND_SIGNAL(COMSIG_MOD_PART_DEPLOYED, Dm.list(this, user, part_datum));
            if (user != null)
            {
                wearer.visible_message(span_notice(I18n.T("[]'s [] deploy[] with a mechanical hiss.", wearer, part.name, part.p_s())),
                    span_notice(I18n.T("[] deploy[] with a mechanical hiss.", part, part.p_s())),
                    span_hear(I18n.T("You hear a mechanical hiss.")));
                playsound(this, Dm.resource("sound/vehicles/mecha/mechmove03.ogg"), 25, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            }
            if (!Dm.truthy(active) || Dm.truthy(part_datum.@sealed))
                return TRUE;
            if (Dm.truthy(instant))
            {
                await seal_part(part as obj_item_clothing, is_sealed: TRUE);
                return TRUE;
            }
            else if (Dm.truthy(await delayed_seal_part(part as obj_item_clothing)))
            {
                return TRUE;
            }
            balloon_alert(user, I18n.T("can't seal, retracting!"));
            await retract(user, part, instant: TRUE);
        }
        else
        {
            if (part_datum.overslotting != null && !Dm.truthy(await wearer.equip_to_slot_if_possible(part_datum.overslotting, part_datum.overslotting.slot_flags, qdel_on_fail: FALSE, disable_warning: TRUE)))
                await wearer.dropItemToGround(part_datum.overslotting, force: TRUE, silent: TRUE);
            if (user == null)
                return FALSE;
            balloon_alert(user, I18n.T("bodypart clothed!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
        }
        return FALSE;
    }

    // tg: code/modules/mod/mod_activation.dm:112
    public virtual object can_preserve_suit_storage(obj_item new_suit = null, obj_item stored_item = null)
    {
        if (!isclothing(new_suit) || stored_item == null)
            return FALSE;
        if (HAS_TRAIT(stored_item, TRAIT_NODROP))
            return FALSE;
        obj_item_clothing new_clothing = new_suit as obj_item_clothing;
        if (Dm.truthy(is_type_in_typecache(stored_item, GLOB.any_suit_storage)) || Dm.eq(stored_item.w_class, WEIGHT_CLASS_TINY))
            return TRUE;
        if (Dm.gt(stored_item.w_class, WEIGHT_CLASS_BULKY))
            return FALSE;
        return is_type_in_list(stored_item, new_clothing.allowed);
    }

    /// Moves a MOD part between the wearer and the suit without forcing valid suit-storage contents to drop.
    // tg: code/modules/mod/mod_activation.dm:125
    public virtual async DmTask<object> transfer_part_to_loc(obj_item part = null, atom newloc = null, float? force = null, float? preserve_suit_storage = null)
    {
        force ??= FALSE;
        preserve_suit_storage ??= FALSE;
        if (!Dm.truthy(preserve_suit_storage))
            return await wearer.transferItemToLoc(part, newloc, force: force);
        if (!Dm.truthy(await wearer.temporarilyRemoveItemFromInventory(part, force, idrop: FALSE, newloc: newloc)))
            return FALSE;
        await part.forceMove(newloc);
        return TRUE;
    }

    /// Retract a part of the suit from the user.
    // tg: code/modules/mod/mod_activation.dm:134
    public virtual async DmTask<object> retract(mob user = null, obj_item part = null, float? instant = null)
    {
        instant ??= FALSE;
        datum_mod_part part_datum = get_part_datum(part) as datum_mod_part;
        if (part.loc == this)
        {
            if (user == null)
                return FALSE;
            balloon_alert(user, I18n.T("already retracted!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MOD_PART_RETRACTING, Dm.list(this, user, part_datum)), MOD_CANCEL_RETRACTION)))
            return FALSE;
        float unsealing = FALSE;
        if (Dm.truthy(active) && Dm.truthy(part_datum.@sealed))
        {
            unsealing = TRUE;
            if (Dm.truthy(instant))
            {
                await seal_part(part as obj_item_clothing, is_sealed: FALSE);
            }
            else if (!Dm.truthy(await delayed_seal_part(part as obj_item_clothing)))
            {
                balloon_alert(user, I18n.T("can't unseal!"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
                return FALSE;
            }
        }
        REMOVE_TRAIT(part, TRAIT_NODROP, MOD_TRAIT);
        object preserve_suit_storage = (part_datum.overslotting is var and__1 && !Dm.truthy(and__1) ? (object)and__1 : can_preserve_suit_storage(part_datum.overslotting, wearer.s_store));
        await transfer_part_to_loc(part, this, force: TRUE, preserve_suit_storage: Dm.num_or_null(preserve_suit_storage));
        if (part_datum.overslotting != null)
        {
            obj_item overslot = part_datum.overslotting;
            if (!QDELING(wearer) && !Dm.truthy(await wearer.equip_to_slot_if_possible(overslot, overslot.slot_flags, qdel_on_fail: FALSE, disable_warning: TRUE)))
                await wearer.dropItemToGround(overslot, force: TRUE, silent: TRUE);
        }
        await wearer.update_clothing(Dm.bitor(slot_flags, part.slot_flags));
        if (user == null)
            return TRUE;
        wearer.visible_message(span_notice(I18n.T("[]'s [] retract[] back into [] with a mechanical hiss.", wearer, part.name, part.p_s(), this)),
            span_notice(I18n.T("[] retract[] back into [] with a mechanical hiss.", part, part.p_s(), this)),
            span_hear(I18n.T("You hear a mechanical hiss.")));
        if (!Dm.truthy(unsealing))
            playsound(this, Dm.resource("sound/vehicles/mecha/mechmove03.ogg"), 25, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return TRUE;
    }

    /// Starts the activation sequence, where parts of the suit activate one by one until the whole suit is on.
    // tg: code/modules/mod/mod_activation.dm:171
    public virtual async DmTask<object> toggle_activate(mob user = null, float? force_deactivate = null)
    {
        force_deactivate ??= FALSE;
        if (wearer == null)
        {
            if (!Dm.truthy(force_deactivate))
            {
                balloon_alert(user, I18n.T("not equipped!"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            }
            return FALSE;
        }
        if (!Dm.truthy(force_deactivate) && Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MOD_ACTIVATE, Dm.list(this, user)), MOD_CANCEL_ACTIVATE)))
        {
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return FALSE;
        }
        if (Dm.truthy(locked) && !Dm.truthy(active) && !Dm.truthy(await allowed__proc(user)) && !Dm.truthy(force_deactivate))
        {
            balloon_alert(user, I18n.T("access insufficient!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return FALSE;
        }
        if (!Dm.truthy(get_charge()) && !Dm.truthy(force_deactivate))
        {
            balloon_alert(user, I18n.T("no power source!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return FALSE;
        }
        if (Dm.truthy(open) && !Dm.truthy(force_deactivate))
        {
            balloon_alert(user, I18n.T("panel open!"));
            playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return FALSE;
        }
        if (Dm.truthy(activating))
        {
            if (!Dm.truthy(force_deactivate))
            {
                balloon_alert(user, I18n.T("already []!", Dm.truthy(active) ? I18n.T("shutting down") : I18n.T("starting up")));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            }
            return FALSE;
        }
        foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
        {
            if (!Dm.truthy(module.active) || Dm.truthy(Dm.bitand(module.allow_flags, MODULE_ALLOW_INACTIVE)))
                continue;
            await module.deactivate(display_message: FALSE);
        }
        activating = TRUE;
        mod_link.end_call();
        object original_active_status = active;
        to_chat(wearer, span_notice(I18n.T("MODsuit [].", Dm.truthy(active) ? I18n.T("shutting down") : I18n.T("starting up"))));
        //deploy the control unit
        if (Dm.truthy(original_active_status))
        {
            if (Dm.truthy(await delayed_activation()))
            {
                playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, frequency: 6000);
                to_chat(wearer, span_notice(I18n.T("Control unit offline. Module capability removed.")));
            }
            else
            {
                activating = FALSE;
                return null;
            }
        }

        DmList sealed_parts = Dm.list();

        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts())) //seals/unseals all deployed parts
        {
            if (part.loc == this)
                continue;
            if (!Dm.truthy(await delayed_seal_part(part as obj_item_clothing))) //shit something broke, revert it all
            {
                activating = FALSE;
                foreach (obj_item sealed_part in Dm.each_cast<obj_item>(sealed_parts))
                    await seal_part(sealed_part as obj_item_clothing, is_sealed: Dm.not(((datum_mod_part)get_part_datum(sealed_part)).@sealed));
                if (Dm.truthy(original_active_status))
                    await control_activation(is_on: TRUE);
                to_chat(wearer, span_notice(I18n.T("Critical error in sealing systems. Reverting process.")));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
                return null;
            }
            Dm.append(ref sealed_parts, part);
        }

        if (!Dm.truthy(original_active_status))
        {
            if (Dm.truthy(await delayed_activation()))
            {
                playsound(this, Dm.resource("sound/machines/synth/synth_yes.ogg"), 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, frequency: 6000);
                if (!Dm.truthy(malfunctioning))
                    wearer.playsound_local(get_turf(this), Dm.resource("sound/vehicles/mecha/nominal.ogg"), 50);
            }
            else
            {
                activating = FALSE;
                foreach (obj_item sealed_part in Dm.each_cast<obj_item>(sealed_parts))
                    await seal_part(sealed_part as obj_item_clothing, is_sealed: Dm.not(((datum_mod_part)get_part_datum(sealed_part)).@sealed));
                to_chat(wearer, span_notice(I18n.T("Critical error in sealing systems. Reverting process.")));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
                return null;
            }
        }

        to_chat(wearer, span_notice(I18n.T("Systems [], [].", Dm.truthy(active) ? I18n.T("started up. Parts sealed. Welcome") : I18n.T("shut down. Parts unsealed. Goodbye"), wearer)));
        if (ai_assistant != null)
            to_chat(ai_assistant, span_notice(I18n.T("<b>SYSTEMS []: \"[]\"</b>", Dm.truthy(active) ? I18n.T("ACTIVATED. WELCOME") : I18n.T("DEACTIVATED. GOODBYE"), ai_assistant)));
        activating = FALSE;
        SEND_SIGNAL(COMSIG_MOD_TOGGLED, Dm.list(this, user));
        return TRUE;
    }

    // tg: code/modules/mod/mod_activation.dm:250
    public virtual async DmTask<object> delayed_seal_part(obj_item_clothing part = null)
    {
        object dot = null;
        dot = FALSE;
        datum_mod_part part_datum = get_part_datum(part) as datum_mod_part;
        if (Dm.truthy(await do_after(wearer, activation_step_time, wearer, MOD_ACTIVATION_STEP_FLAGS, extra_checks: Dm.@new<datum_callback>(this, nameof(get_wearer)), cog_icon: null)))
        {
            to_chat(wearer, span_notice(I18n.T("[] [].", part, !Dm.truthy(part_datum.@sealed) ? part_datum.sealed_message : part_datum.unsealed_message)));
            playsound(this, Dm.resource("sound/vehicles/mecha/mechmove03.ogg"), 25, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            await seal_part(part, is_sealed: Dm.not(part_datum.@sealed));
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mod/mod_activation.dm:259
    public virtual async DmTask<object> delayed_activation()
    {
        object dot = null;
        dot = FALSE;
        if (Dm.truthy(await do_after(wearer, activation_step_time, wearer, MOD_ACTIVATION_STEP_FLAGS, extra_checks: Dm.@new<datum_callback>(this, nameof(get_wearer)), cog_icon: null)))
        {
            await control_activation(is_on: Dm.not(active));
            return TRUE;
        }
        return dot;
    }

    ///Seals or unseals the given part.
    // tg: code/modules/mod/mod_activation.dm:266
    public virtual async DmTask seal_part(obj_item_clothing part = null, object is_sealed = null)
    {
        datum_mod_part part_datum = get_part_datum(part) as datum_mod_part;
        part_datum.@sealed = Dm.num(is_sealed);
        if (Dm.truthy(part_datum.@sealed))
        {
            part.icon_state = Dm.text("[]-[]-sealed", skin, part.base_icon_state);
            Dm.combine(ref part.clothing_flags, part.visor_flags);
            Dm.combine(ref part.flags_inv, part.visor_flags_inv);
            Dm.combine(ref part.flags_cover, part.visor_flags_cover);
            part.heat_protection = Dm.initial(part, (obj_item_clothing p) => p.heat_protection);
            part.cold_protection = Dm.initial(part, (obj_item_clothing p) => p.cold_protection);
            part.alternate_worn_layer = part_datum.sealed_layer;
            if (Dm.truthy(Dm.bitand(part.slot_flags, ITEM_SLOT_HEAD)))
            {
                datum_component_wearertargeting protection = await part.AddComponent(typeof(datum_component_wearertargeting_earprotection), Dm.named("protection_amount", ((datum_mod_theme)this.theme).hearing_protection)) as datum_component_wearertargeting;
                protection.on_equip(this, wearer, ITEM_SLOT_HEAD);
            }
        }
        else
        {
            part.icon_state = Dm.text("[]-[]", skin, part.base_icon_state);
            Dm.mask(ref part.flags_cover, Dm.bitnot(part.visor_flags_cover));
            Dm.mask(ref part.flags_inv, Dm.bitnot(part.visor_flags_inv));
            Dm.mask(ref part.clothing_flags, Dm.bitnot(part.visor_flags));
            part.heat_protection = NONE;
            part.cold_protection = NONE;
            part.alternate_worn_layer = part_datum.unsealed_layer;
            if (Dm.truthy(Dm.bitand(part.slot_flags, ITEM_SLOT_HEAD)) && part is obj_item_clothing_head_mod)
                qdel(part.GetComponent(typeof(datum_component_wearertargeting_earprotection)));
        }
        await update_speed();
        await wearer.update_clothing(Dm.bitor(part.slot_flags, slot_flags));
        wearer.refresh_obscured();
        if (Dm.truthy(Dm.bitand(part.clothing_flags, MASKINTERNALS | HEADINTERNALS)) && Dm.truthy(wearer.invalid_internals()))
            await wearer.cutoff_internals();
        SEND_SIGNAL(COMSIG_MOD_PART_SEALED, Dm.list(this, part_datum));
        if (Dm.truthy(is_sealed))
        {
            if (!Dm.truthy(active))
                return;
            foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            {
                if (Dm.truthy(module.part_activated) || !Dm.truthy(module.has_required_parts(mod_parts, need_active: TRUE)))
                    continue;
                await module.on_part_activation();
                module.part_activated = TRUE;
            }
        }
        else
        {
            foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            {
                if (!Dm.truthy(module.part_activated) || Dm.truthy(module.has_required_parts(mod_parts, need_active: TRUE)))
                    continue;
                await module.on_part_deactivation();
                module.part_activated = FALSE;
                if (!Dm.truthy(module.active) || Dm.truthy(Dm.bitand(module.allow_flags, MODULE_ALLOW_INACTIVE)))
                    continue;
                await module.deactivate(display_message: FALSE);
            }
        }
    }

    /// Finishes the suit's activation
    // tg: code/modules/mod/mod_activation.dm:315
    public virtual async DmTask control_activation(object is_on = null)
    {
        datum_mod_part part_datum = get_part_datum(this) as datum_mod_part;
        part_datum.@sealed = Dm.num(is_on);
        active = Dm.num_or_null(is_on);
        if (Dm.truthy(active))
        {
            foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            {
                if (!Dm.truthy(module.part_activated) && Dm.truthy(module.has_required_parts(mod_parts, need_active: TRUE)))
                {
                    await module.on_part_activation();
                    module.part_activated = TRUE;
                }
            }
        }
        else
        {
            foreach (obj_item_mod_module module in Dm.each_cast<obj_item_mod_module>(modules))
            {
                if (!Dm.truthy(module.part_activated))
                    continue;
                await module.on_part_deactivation();
                module.part_activated = FALSE;
            }
        }
        update_charge_alert();
        update_appearance(UPDATE_ICON_STATE);
        object updated_slots = slot_flags;
        foreach (object slot_key in Dm.each(mod_parts))
            Dm.combine(ref updated_slots, text2num(slot_key));
        await wearer.update_clothing(updated_slots);
    }

    /// Quickly deploys all the suit parts and if successful, seals them and turns on the suit. Intended mostly for outfits.
    // tg: code/modules/mod/mod_activation.dm:338
    public virtual async DmTask quick_activation()
    {
        await control_activation(is_on: TRUE);
        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts()))
            await deploy(null, part, instant: TRUE);
    }

    /// Checks if the suit is fully retracted, with no parts outside
    // tg: code/modules/mod/mod_activation.dm:344
    public virtual object check_retracted()
    {
        foreach (obj_item part in Dm.each_cast<obj_item>(get_parts()))
        {
            if (part.loc != this)
                return FALSE;
        }
        return TRUE;
    }
}

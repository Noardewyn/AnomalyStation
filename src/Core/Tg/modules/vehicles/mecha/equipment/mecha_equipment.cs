// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/equipment/mecha_equipment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Mecha Equipment
 * All mech equippables are currently childs of this
 */
// tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:5
public partial class obj_item_mecha_parts_mecha_equipment : obj_item_mecha_parts
{
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:44
    public override object Destroy(float? force = null)
    {
        if (chassis != null)
        {
            if (Dm.truthy(LAZYLEN(chassis.occupants)))
            {
                to_chat(chassis.occupants, span_danger(I18n.T("[] is destroyed!", this)));
                playsound(chassis, destroy_sound, 50);
            }
            Waiting.NoSleep(detach(get_turf(this) as atom));
            log_message(Dm.text("[] is destroyed.", this), LOG_MECHA);
            chassis = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:54
    public override async DmTask<object> try_attach_part(mob user = null, obj_vehicle_sealed_mecha M = null, object attach_right = null)
    {
        attach_right ??= FALSE;
        if (Dm.truthy(can_attach(M, attach_right, user)))
        {
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(this)))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.truthy(await special_attaching_interaction(attach_right, M, user)))
                return ITEM_INTERACT_SUCCESS; //The rest is handled in the special interactions proc
            await attach(M, attach_right);
            user.visible_message(span_notice(I18n.T("[] attaches [] to [].", user, this, M)), span_notice(I18n.T("You attach [] to [].", this, M)));
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:64
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "detach"))
        {
            if (Dm.truthy(detachable))
            {
                chassis.ui_selected_module_index = null;
                await detach(get_turf(this) as atom);
            }
            dot = TRUE;
        }
        else if (Dm.eq(action, "toggle"))
        {
            if (Dm.truthy(can_be_toggled))
                await set_active(Dm.truthy(active) ? FALSE : TRUE);
            dot = TRUE;
        }
        else if (Dm.eq(action, "repair"))
        {
            ui.close(); // allow watching for baddies and the ingame effects
            chassis.balloon_alert(usr, I18n.T("starting repair"));
            while (Dm.truthy(await do_after(usr, 1 * SECONDS, chassis)) && Dm.num(get_integrity()) < Dm.num(max_integrity))
                await repair_damage(30);
            if (Dm.eq(get_integrity(), max_integrity))
                balloon_alert(usr, I18n.T("repair complete"));
            dot = FALSE;
        }
        object result = await handle_ui_act(action, @params, ui, state);
        if (Dm.truthy(result)) //if handle_ui_act returned anything at all lets just return that instead
            dot = result;
        return dot;
    }

    /// called after ui_act, for custom ui act handling
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:92
    public virtual DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null) => DmTask<object>.FromResult(null);

    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:96
    public virtual object get_equip_cooldown(atom target = null) => equip_cooldown;

    /**
     * Checks whether this mecha equipment can be active
     * Returns a bool
     * Arguments:
     * * target: atom we are activating/clicked on
     */
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:105
    public virtual object action_checks(atom target = null)
    {
        if (target == null)
            return FALSE;
        if (chassis == null)
            return FALSE;
        if (!Dm.truthy(active))
            return FALSE;
        if (Dm.truthy(energy_drain) && !chassis.has_charge(energy_drain))
            return FALSE;
        if (Dm.truthy(chassis.is_currently_ejecting))
            return FALSE;
        if (Dm.truthy(chassis.equipment_disabled))
        {
            to_chat(chassis.occupants, span_warning(I18n.T("Error -- Equipment control unit is unresponsive.")));
            return FALSE;
        }
        if (Dm.num(get_integrity()) <= 1)
        {
            to_chat(chassis.occupants, span_warning(I18n.T("Error -- Equipment critically damaged.")));
            return FALSE;
        }
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(chassis, COOLDOWN_MECHA_EQUIPMENT(type))))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:124
    public virtual async DmTask<object> action(mob source = null, atom target = null, DmList modifiers = null)
    {
        chassis.timer_cooldown_start(COOLDOWN_MECHA_EQUIPMENT(type), get_equip_cooldown(target)); //Cooldown is on the MECH so people dont bypass it by switching equipment
        source.SEND_SIGNAL(COMSIG_MOB_USED_MECH_EQUIPMENT, Dm.list(source, chassis));
        chassis.use_energy(energy_drain);
        return TRUE;
    }

    /**
     * Cooldown proc variant for using do_afters between activations instead of timers
     * Example of usage is mech drills, rcds
     * arguments:
     * * target: targeted atom for action activation
     * * user: occupant to display do after for
     * * interaction_key: interaction key to pass to [/proc/do_after]
     * * flags: bitfield for different checks on do_after_checks
     */
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:139
    public virtual async DmTask<object> do_after_cooldown(atom target = null, mob user = null, object interaction_key = null, object flags = null)
    {
        if (chassis == null)
            return FALSE;
        chassis.use_energy(energy_drain);
        return await do_after(user, get_equip_cooldown(target), target, extra_checks: Dm.@new<datum_callback>(this, nameof(do_after_checks), target, flags), interaction_key: interaction_key);
    }

    ///Do after wrapper for mecha equipment
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:146
    public virtual async DmTask<object> do_after_mecha(atom target = null, mob user = null, object delay = null, object flags = null)
        => await do_after(user, delay, target, extra_checks: Dm.@new<datum_callback>(this, nameof(do_after_checks), target, flags));

    /// do after checks for the mecha equipment do afters
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:150
    public virtual async DmTask<object> do_after_checks(atom target = null, object flags = null)
    {
        flags ??= MECH_DO_AFTER_DIR_CHANGE_FLAG;
        if (chassis == null)
            return FALSE;

        if (Dm.truthy(Dm.bitand(flags, MECH_DO_AFTER_DIR_CHANGE_FLAG)) && !Dm.truthy(Dm.bitand(get_dir(chassis, target), chassis.dir)))
            return FALSE;

        if (Dm.truthy(Dm.bitand(flags, MECH_DO_AFTER_ADJACENCY_FLAG)) && !Dm.truthy(await chassis.Adjacent(target)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:161
    public virtual object can_attach(obj_vehicle_sealed_mecha mech = null, object attach_right = null, mob user = null) => default_can_attach(mech, attach_right, user);

    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:164
    public virtual object default_can_attach(obj_vehicle_sealed_mecha mech = null, object attach_right = null, mob user = null)
    {
        attach_right ??= FALSE;
        if (!Dm.truthy(Dm.bitand(mech_flags, mech.mech_type)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is incompatible with []!", this, mech)));
            return FALSE;
        }
        if (Dm.eq(equipment_slot, MECHA_WEAPON))
        {
            if (Dm.truthy(attach_right))
            {
                // We need to check for length in case a mech doesn't support any arm attachments at all
                if ((mech.equip_by_category[MECHA_R_ARM] != null || !Dm.truthy(mech.max_equip_by_category[MECHA_R_ARM])) && !Dm.truthy(Waiting.NoSleep(special_attaching_interaction(attach_right, mech, user, TRUE))))
                {
                    to_chat(user, span_warning(I18n.T("\\The []'s right arm is full![]", mech, Dm.truthy(mech.equip_by_category[MECHA_L_ARM]) || !Dm.truthy(mech.max_equip_by_category[MECHA_L_ARM]) ? "" : I18n.T(" Try left arm!"))));
                    return FALSE;
                }
            }
            else
            {
                if ((mech.equip_by_category[MECHA_L_ARM] != null || !Dm.truthy(mech.max_equip_by_category[MECHA_L_ARM])) && !Dm.truthy(Waiting.NoSleep(special_attaching_interaction(attach_right, mech, user, TRUE))))
                {
                    to_chat(user, span_warning(I18n.T("\\The []'s left arm is full![]", mech, Dm.truthy(mech.equip_by_category[MECHA_R_ARM]) || !Dm.truthy(mech.max_equip_by_category[MECHA_R_ARM]) ? "" : I18n.T(" Try right arm!"))));
                    return FALSE;
                }
            }
            return TRUE;
        }
        if (Dm.truthy(unstackable))
        {
            DmList contents__1 = mech.equip_by_category[equipment_slot] as DmList;
            foreach (obj equipment in Dm.each<obj>(contents__1))
            {
                if (type == equipment.type)
                {
                    to_chat(user, span_warning(I18n.T("You can't stack more of this item ontop itself!")));
                    return FALSE;
                }
            }
        }

        if (Dm.eq(length(mech.equip_by_category[equipment_slot]), mech.max_equip_by_category[equipment_slot]))
        {
            to_chat(user, span_warning(I18n.T("This equipment slot is already full!")));
            return FALSE;
        }
        return TRUE;
    }

    /**
     * Special Attaching Interaction, used to bypass normal attachment procs.
     *
     * If an equipment needs to bypass the regular chain of events, this proc can be used to allow for that. If used, it
     * must handle actually calling attach(), as well as any feedback to the user.
     * Args:
     * * attach_right: True if attaching the the right-hand equipment slot, false otherwise.
     * * mech: ref to the mecha that we're attaching onto.
     * * user: ref to the mob doing the attaching
     * * checkonly: check if we are able to handle the attach procedure ourselves, but don't actually do it yet.
     */
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:207
    public virtual DmTask<object> special_attaching_interaction(object attach_right = null, obj_vehicle_sealed_mecha mech = null, mob user = null, object checkonly = null) => DmTask<object>.FromResult(FALSE);

    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:210
    public virtual async DmTask attach(obj_vehicle_sealed_mecha new_mecha = null, object attach_right = null)
    {
        attach_right ??= FALSE;
        LAZYADD(ref new_mecha.flat_equipment, this);
        object to_equip_slot = equipment_slot;
        if (Dm.eq(equipment_slot, MECHA_WEAPON))
        {
            if (Dm.truthy(attach_right))
                to_equip_slot = MECHA_R_ARM;
            else
                to_equip_slot = MECHA_L_ARM;
        }
        if (islist(new_mecha.equip_by_category[to_equip_slot]))
            (new_mecha.equip_by_category[to_equip_slot] as DmList).Add(this);
        else
            new_mecha.equip_by_category[to_equip_slot] = this;
        chassis = new_mecha;
        SEND_SIGNAL(COMSIG_MECHA_EQUIPMENT_ATTACHED, Dm.list(this));
        await forceMove(new_mecha);
        log_message(Dm.text("[] initialized.", this), LOG_MECHA);
        await chassis.on_equipment_attach(this);
        chassis.refresh_arm_views(); // DV361
    }

    /**
     * called to detach this equipment
     * Args:
     * * moveto: optional target to move this equipment to
     */
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:232
    public virtual async DmTask detach(atom moveto = null)
    {
        chassis.on_equipment_detach(this);
        moveto = moveto ?? get_turf(chassis) as atom;
        await forceMove(moveto);
        playsound(chassis, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        LAZYREMOVE(ref chassis.flat_equipment, this);
        object to_unequip_slot = equipment_slot;
        if (Dm.eq(equipment_slot, MECHA_WEAPON))
        {
            if (chassis.equip_by_category[MECHA_R_ARM] == this)
                to_unequip_slot = MECHA_R_ARM;
            else
                to_unequip_slot = MECHA_L_ARM;
        }
        if (islist(chassis.equip_by_category[to_unequip_slot]))
            Dm.remove(chassis.equip_by_category[to_unequip_slot] as DmList, this);
        else
            chassis.equip_by_category[to_unequip_slot] = null;
        SEND_SIGNAL(COMSIG_MECHA_EQUIPMENT_DETACHED, Dm.list(this));
        log_message(Dm.text("[] removed from equipment.", this), LOG_MECHA);
        chassis.refresh_arm_views(); // DV361
        chassis = null;
    }

    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:254
    public virtual DmTask set_active(object active__1 = null)
    {
        active = Dm.num(active__1);
        return default;
    }

    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:253
    public override object log_message(object message = null, object message_type = null, object color = null, float? log_globally = null, DmList data = null)
    {
        message_type ??= LOG_GAME;
        if (chassis != null)
            return chassis.log_message(Dm.text("ATTACHMENT: [] []", this, message), message_type, color);
        return base.log_message(message, message_type, color, log_globally, data);
    }

    /**
     * ## get_snowflake_data
     * handles the returning of snowflake data required by the UI of the mecha
     * not the prettiest of procs honeslty
     * returns:
     * * an assoc list
     * * must include an list("snowflake_id" = snowflake_id)
     */
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:269
    public virtual DmList get_snowflake_data() => Dm.list();

    /**
     * Proc for reloading weapons from HTML UI or by AI
     * note that this is old and likely broken code
     */
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:275
    public virtual object rearm() => FALSE;

    /// AI mech pilot: returns TRUE if the Ai should try to reload the mecha
    // tg: code/modules/vehicles/mecha/equipment/mecha_equipment.dm:279
    public virtual object needs_rearm() => FALSE;
}

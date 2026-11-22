// tg: code/modules/reta/reta_id_card.dm
// Gaps SC1 (D333): whole. The ID card's side of RETA: the temporary department access a requests console's emergency call
// grants, per department with its own expiry timer, kept apart from the card's own access so the expiry removes only what RETA
// added; the holder's beep and balloon at the grant and the warning at the expiry. Its vars (reta_temp_access, reta_timers) are
// set in cards_ids.cs dm_init_vars, its Destroy part runs first in cards_ids.cs Destroy (DM's override-chain order: this file's
// /obj/item/card/id/Destroy is compiled after cards_ids.dm, so it runs before the base one).
// tg: code/modules/reta/reta_id_card.dm:191-195 (the commented-out death hook) is not code.

using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class obj_item_card_id
{
    /// Grants temporary department access to this ID card
    // tg: code/modules/reta/reta_id_card.dm:13
    public object grant_reta_access(object dept, object duration_ds)
    {
        if (!Dm.truthy(GLOB.reta_dept_grants[dept]))
            return FALSE;

        // Clear existing timer for this department if any (allows extending/refreshing access)
        if (Dm.truthy(reta_timers[dept]) && !Dm.eq(reta_timers[dept], -1))
        {
            deltimer(reta_timers[dept]);
            reta_timers[dept] = null;
        }

        // Grant access flags for this department
        DmList access_flags = (DmList)GLOB.reta_dept_grants[dept];
        DmList new_access = Dm.list();

        // Initialize department access list if needed
        if (!Dm.truthy(reta_temp_access[dept]))
            reta_temp_access[dept] = Dm.list();

        foreach (object flag in Dm.each(access_flags))
        {
            if (!Dm.@in(flag, access)) // Only add if not permanently granted
            {
                // Add to department-specific temporary access
                DmList dept_access = (DmList)reta_temp_access[dept];
                Dm.combine(ref dept_access, flag);
                reta_temp_access[dept] = dept_access;
                // Add to main access list
                Dm.append(ref access, flag);
                Dm.append(ref new_access, flag);
            }
        }

        if (LAZYLEN(new_access) == 0)
            return FALSE; // No new access granted

        // Set timer for this specific department
        reta_timers[dept] = _addtimer(Dm.@new<datum_callback>(this, nameof(clear_reta_access_for_dept), dept), Dm.num(duration_ds), TIMER_UNIQUE | TIMER_OVERRIDE | TIMER_STOPPABLE, file: "code/modules/reta/reta_id_card.dm", line: 41);

        // Add to global registry for mass operations
        Dm.combine(ref GLOB.reta_active_cards, this);

        // User feedback
        mob_living_carbon_human holder = get_id_holder();
        if (holder != null)
        {
            playsound(holder, Dm.resource("sound/machines/cryo_warning.ogg"), 25, TRUE);
            holder.balloon_alert(holder, I18n.T("emergency access: []", dept));
        }

        // Enhanced logging
        DmList access_names = Dm.list();
        foreach (object flag in Dm.each(new_access))
            Dm.append(ref access_names, SSid_access?.get_access_desc(flag));

        object holder_info = holder != null ? Dm.text("held by []", holder) : "not being held";
        log_reta(Dm.text("Granted [] temporary access ([]) to ID '[]' ([]) for [] seconds", dept, english_list(access_names), Dm.truthy(registered_name) ? registered_name : "Unknown", holder_info, Dm.num(duration_ds) / 10));
        investigate_log(Dm.text("RETA: Granted [] temporary access ([]) to ID '[]' ([])", dept, english_list(access_names), Dm.truthy(registered_name) ? registered_name : "Unknown", holder_info), INVESTIGATE_ACCESSCHANGES);

        return TRUE;
    }

    /// Clears temporary access for a specific department
    // tg: code/modules/reta/reta_id_card.dm:58
    public void clear_reta_access_for_dept(object dept)
    {
        if (!Dm.truthy(reta_temp_access[dept]) || LAZYLEN(reta_temp_access[dept] as DmList) == 0)
            return;

        // User feedback before clearing
        mob_living_carbon_human holder = get_id_holder();
        if (holder != null)
        {
            holder.balloon_alert(holder, I18n.T("[] access expired", dept));
            to_chat(holder, span_warning(I18n.T("Emergency access to [] has expired.", dept)));
        }

        // Remove department's temporary access from the main access list
        DmList dept_access = (DmList)reta_temp_access[dept];
        foreach (object flag in Dm.each(dept_access))
        {
            // Only remove if no other department also grants this access
            bool still_needed = false;
            foreach (object other_dept in Dm.each(reta_temp_access))
            {
                if (!Dm.eq(other_dept, dept) && Dm.truthy(reta_temp_access[other_dept]) && Dm.@in(flag, reta_temp_access[other_dept]))
                {
                    still_needed = true;
                    break;
                }
            }

            if (!still_needed)
                Dm.remove(ref access, flag);
        }

        // Enhanced logging
        DmList access_names = Dm.list();
        foreach (object flag in Dm.each(dept_access))
            Dm.append(ref access_names, SSid_access?.get_access_desc(flag));

        object holder_info = holder != null ? Dm.text("held by []", holder) : "not being held";
        log_reta(Dm.text("Cleared [] temporary access ([]) from ID '[]' ([])", dept, english_list(access_names), Dm.truthy(registered_name) ? registered_name : "Unknown", holder_info));
        investigate_log(Dm.text("RETA: Cleared [] temporary access ([]) from ID '[]' ([])", dept, english_list(access_names), Dm.truthy(registered_name) ? registered_name : "Unknown", holder_info), INVESTIGATE_ACCESSCHANGES);

        // Clean up department data
        reta_temp_access[dept] = null;
        reta_timers[dept] = null;

        // Remove from global registry if no more temporary access
        if (!has_any_reta_access())
            Dm.remove(ref GLOB.reta_active_cards, this);
    }

    /// Clears all temporary department access from this ID card
    // tg: code/modules/reta/reta_id_card.dm:99
    public void clear_reta_access()
    {
        if (LAZYLEN(reta_temp_access) == 0)
            return;

        // User feedback before clearing
        mob_living_carbon_human holder = get_id_holder();
        if (holder != null)
        {
            holder.balloon_alert(holder, I18n.T("emergency access expired"));
            to_chat(holder, span_warning(I18n.T("Emergency access has expired.")));
        }

        // Collect all temporary access flags for logging
        DmList all_temp_access = Dm.list();
        foreach (object dept in Dm.each(reta_temp_access))
        {
            if (Dm.truthy(reta_temp_access[dept]))
                Dm.combine(ref all_temp_access, reta_temp_access[dept]);
        }

        // Remove all temporary access from the main access list
        foreach (object flag in Dm.each(all_temp_access))
            Dm.remove(ref access, flag);

        // Enhanced logging
        DmList access_names = Dm.list();
        foreach (object flag in Dm.each(all_temp_access))
            Dm.append(ref access_names, SSid_access?.get_access_desc(flag));

        object holder_info = holder != null ? Dm.text("held by []", holder) : "not being held";
        log_reta(Dm.text("Cleared all temporary access ([]) from ID '[]' ([])", english_list(access_names), Dm.truthy(registered_name) ? registered_name : "Unknown", holder_info));
        investigate_log(Dm.text("RETA: Cleared all temporary access ([]) from ID '[]' ([])", english_list(access_names), Dm.truthy(registered_name) ? registered_name : "Unknown", holder_info), INVESTIGATE_ACCESSCHANGES);

        // Clear all timers
        foreach (object dept in Dm.each(reta_timers))
        {
            if (Dm.truthy(reta_timers[dept]) && !Dm.eq(reta_timers[dept], -1))
                deltimer(reta_timers[dept]);
        }

        LAZYCLEARLIST(reta_temp_access);
        LAZYCLEARLIST(reta_timers);

        // Remove from global registry
        Dm.remove(ref GLOB.reta_active_cards, this);
    }

    /// Checks if this ID card has any temporary access
    // tg: code/modules/reta/reta_id_card.dm:139
    public bool has_any_reta_access()
    {
        foreach (object dept in Dm.each(reta_temp_access))
        {
            if (Dm.truthy(reta_temp_access[dept]) && LAZYLEN(reta_temp_access[dept] as DmList) > 0)
                return true;
        }
        return false;
    }

    /// Checks if this ID card has temporary access to a specific flag
    // tg: code/modules/reta/reta_id_card.dm:146
    public bool has_reta_access(object access_flag)
    {
        foreach (object dept in Dm.each(reta_temp_access))
        {
            if (Dm.truthy(reta_temp_access[dept]) && Dm.@in(access_flag, reta_temp_access[dept]))
                return true;
        }
        return false;
    }

    /// Checks if this ID card has temporary access for a specific department
    // tg: code/modules/reta/reta_id_card.dm:153
    public bool has_reta_access_for_dept(object dept) => Dm.truthy(reta_temp_access[dept]) && LAZYLEN(reta_temp_access[dept] as DmList) > 0;

    /// Gets all current temporary access flags for this ID card
    // tg: code/modules/reta/reta_id_card.dm:157
    public DmList get_reta_access()
    {
        DmList all_access = Dm.list();
        foreach (object dept in Dm.each(reta_temp_access))
        {
            if (Dm.truthy(reta_temp_access[dept]))
                Dm.combine(ref all_access, reta_temp_access[dept]);
        }
        return all_access;
    }

    /// Gets temporary access flags for a specific department
    // tg: code/modules/reta/reta_id_card.dm:165
    public DmList get_reta_access_for_dept(object dept) => (reta_temp_access[dept] as DmList)?.Copy() ?? Dm.list();

    /// Gets a summary of all active RETA accesses (for debugging/display)
    // tg: code/modules/reta/reta_id_card.dm:170
    public DmList get_reta_summary()
    {
        DmList summary = Dm.list();
        foreach (object dept in Dm.each(reta_temp_access))
        {
            if (Dm.truthy(reta_temp_access[dept]) && LAZYLEN(reta_temp_access[dept] as DmList) > 0)
            {
                object time_left = "unknown";
                if (Dm.truthy(reta_timers[dept]))
                {
                    float remaining = Dm.num(timeleft(reta_timers[dept]));
                    if (remaining > 0)
                        time_left = Dm.text("[]s", remaining / 10);
                }
                Dm.append(ref summary, Dm.text("[] ([] access, [] left)", dept, LAZYLEN(reta_temp_access[dept] as DmList), time_left));
            }
        }
        return summary;
    }

    /// Helper to get the human holding this ID card
    // tg: code/modules/reta/reta_id_card.dm:183
    public mob_living_carbon_human get_id_holder()
    {
        mob_living_carbon_human holder = null;
        if (loc is mob_living_carbon_human human_loc)
            holder = human_loc;
        else if (loc is obj_item_card_id && loc.loc is mob_living_carbon_human wallet_holder)
            holder = wallet_holder;
        else
        {
            // Check if worn in ID slot
            foreach (mob_living_carbon_human human in Dm.each<mob_living_carbon_human>(range(0, this)))
            {
                if (ReferenceEquals(human.get_idcard(), this))
                {
                    holder = human;
                    break;
                }
            }
        }
        return holder;
    }

    /// Cleanup temporary access when ID card is deleted
    // tg: code/modules/reta/reta_id_card.dm:196 (the RETA part of /obj/item/card/id/Destroy; called first from cards_ids.cs Destroy)
    private void reta_destroy()
    {
        // Clear all department timers
        foreach (object dept in Dm.each(reta_timers))
        {
            if (Dm.truthy(reta_timers[dept]) && !Dm.eq(reta_timers[dept], -1))
                deltimer(reta_timers[dept]);
        }
        Dm.remove(ref GLOB.reta_active_cards, this);
    }
}

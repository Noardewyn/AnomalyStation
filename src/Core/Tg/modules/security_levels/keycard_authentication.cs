// Translated from tgstation (snapshot 1d99bd7f): code/modules/security_levels/keycard_authentication.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the emergency access procs came with the M7 delamination slice and M10 G5, the device itself with gaps J4 (D298).

// tg: code/modules/security_levels/keycard_authentication.dm:167  GLOBAL_VAR_INIT(emergency_access, FALSE)
public partial class datum_controller_global_vars
{
    // tg: code/modules/security_levels/keycard_authentication.dm:1  GLOBAL_DATUM_INIT(keycard_events, /datum/events, new)
    public datum_events keycard_events;

    // tg: code/modules/security_levels/keycard_authentication.dm:1  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalkeycard_events()
    {
        keycard_events = Dm.@new<datum_events>();
        Dm.append(ref gvars_datum_init_order, nameof(keycard_events));
    }

    // tg: code/modules/station_goals/bsa.dm:3  GLOBAL_VAR_INIT(bsa_unlock, FALSE)
    public object bsa_unlock;

    // tg: code/modules/station_goals/bsa.dm:3  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbsa_unlock()
    {
        bsa_unlock = FALSE;
        Dm.append(ref gvars_datum_init_order, nameof(bsa_unlock));
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:167  GLOBAL_VAR_INIT(emergency_access, FALSE)
    public object emergency_access;

    // tg: code/modules/security_levels/keycard_authentication.dm:167  (GLOBAL_MANAGED(emergency_access, FALSE), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalemergency_access()
    {
        emergency_access = FALSE;
        Dm.append(ref gvars_datum_init_order, nameof(emergency_access));
    }
}

// tg: code/modules/security_levels/keycard_authentication.dm:10
public partial class obj_machinery_keycard_auth : obj_machinery
{
    private const string KEYCARD_RED_ALERT = "Red Alert";
    private const string KEYCARD_EMERGENCY_MAINTENANCE_ACCESS = "Emergency Maintenance Access";
    private const string KEYCARD_BSA_UNLOCK = "Bluespace Artillery Unlock";
    private const float ACCESS_GRANTING_COOLDOWN = 30 * SECONDS;

    // tg: code/modules/security_levels/keycard_authentication.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        activated = GLOB.keycard_events.addEvent("triggerEvent", Dm.@new<datum_callback>(this, nameof(triggerEvent))) as datum_callback;
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:32
    public override object Destroy(float? force = null)
    {
        GLOB.keycard_events.clearEvent("triggerEvent", activated);
        activated = null;
        return base.Destroy(force);
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:37
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:40
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "KeycardAuth", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:46
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["waiting"] = waiting;
        data["auth_required"] = event_source != null ? event_source.@event : 0;
        data["red_alert"] = Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED ? 1 : 0;
        data["emergency_maint"] = GLOB.emergency_access;
        data["bsa_unlock"] = GLOB.bsa_unlock;
        return data;
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:55
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (isdrone(user))
            return UI_CLOSE;
        if (!isanimal_or_basicmob(user))
            return await base.ui_status(user, state);
        if (!HAS_TRAIT(user, TRAIT_CAN_HOLD_ITEMS))
        {
            balloon_alert(user, I18n.T("no hands!"));
            return UI_CLOSE;
        }
        return await base.ui_status(user, state);
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:65
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot) || Dm.truthy(waiting) || !Dm.truthy(await allowed__proc(usr)))
            return dot;

        switch (action)
        {
            case "red_alert":
                if (event_source == null)
                {
                    sendEvent(KEYCARD_RED_ALERT);
                    dot = TRUE;
                }
                break;
            case "emergency_maint":
                if (event_source == null)
                {
                    sendEvent(KEYCARD_EMERGENCY_MAINTENANCE_ACCESS);
                    dot = TRUE;
                }
                break;
            case "auth_swipe":
                if (event_source != null)
                {
                    await event_source.trigger_event(usr);
                    event_source = null;
                    update_appearance();
                    dot = TRUE;
                }
                break;
            case "bsa_unlock":
                if (event_source == null)
                {
                    sendEvent(KEYCARD_BSA_UNLOCK);
                    dot = TRUE;
                }
                break;
            case "give_janitor_access":
            {
                mob_living living_user = usr as mob_living;
                if (living_user == null)
                    return TRUE;
                if (access_grant_cooldown > Dm.num(world.time)) // COOLDOWN_FINISHED, inline
                {
                    balloon_alert(usr, I18n.T("on cooldown!"));
                    return TRUE;
                }
                obj_item_card_id_advanced card = living_user.get_idcard(hand_first: TRUE) as obj_item_card_id_advanced;
                if (card == null)
                    return TRUE;
                foreach (object access_as_text in Dm.each(SSid_access.sub_department_managers_tgui))
                {
                    DmList info = SSid_access.sub_department_managers_tgui[access_as_text] as DmList;
                    if (!Dm.eq((card.trim as datum_id_trim)?.assignment, info["head"]))
                        continue;
                    access_grant_cooldown = Dm.num(world.time) + ACCESS_GRANTING_COOLDOWN; // COOLDOWN_START, inline
                    // SEND_GLOBAL_SIGNAL(COMSIG_ON_DEPARTMENT_ACCESS, info["regions"])
                    SSdcs._SendSignal(COMSIG_ON_DEPARTMENT_ACCESS, Dm.list(SSdcs, info["regions"]));
                    balloon_alert(usr, I18n.T("key access sent"));
                    return dot;
                }
                break;
            }
        }
        return dot;
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:108
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);

        if (event_source != null && !Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            Waiting.NoSleep(set_light(2, 1, "#5668E1"));
        else
            Waiting.NoSleep(set_light(0));
        return dot;
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:116
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;

        if (event_source != null && !Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            dot.Add(mutable_appearance(icon, "auth_on"));
            // DEVIATION(DV12): . += emissive_appearance(icon, "auth_on", src, alpha = src.alpha)
        }
        return dot;
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:123
    public virtual void sendEvent(object event_type = null)
    {
        triggerer = usr;
        @event = Dm.str(event_type);
        waiting = TRUE;
        GLOB.keycard_events.fireEvent("triggerEvent", this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(eventSent)), 2 * SECONDS, file: "code/modules/security_levels/keycard_authentication.dm", line: 128);
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:130
    public virtual void eventSent()
    {
        triggerer = null;
        @event = "";
        waiting = FALSE;
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:135
    public virtual void triggerEvent(object source = null)
    {
        event_source = source as obj_machinery_keycard_auth;
        update_appearance();
        _addtimer(Dm.@new<datum_callback>(this, nameof(eventTriggered)), 2 * SECONDS, file: "code/modules/security_levels/keycard_authentication.dm", line: 138);
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:140
    public virtual void eventTriggered()
    {
        event_source = null;
        update_appearance();
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:144
    public virtual async DmTask trigger_event(mob confirmer = null)
    {
        triggerer.log_message(Dm.text("triggered and [] confirmed event [].", key_name(confirmer), @event), LOG_GAME);
        message_admins(Dm.text("[] triggered and [] confirmed event []", ADMIN_LOOKUPFLW(triggerer), ADMIN_LOOKUPFLW(confirmer), @event));

        area A1 = get_area(triggerer) as area;
        deadchat_broadcast(Dm.text(" triggered [] at [].", @event, span_name(Dm.text("[]", A1?.name))), span_name(Dm.text("[]", triggerer)), triggerer, message_type: DEADCHAT_ANNOUNCEMENT);

        area A2 = get_area(confirmer) as area;
        deadchat_broadcast(Dm.text(" confirmed [] at [].", @event, span_name(Dm.text("[]", A2?.name))), span_name(Dm.text("[]", confirmer)), confirmer, message_type: DEADCHAT_ANNOUNCEMENT);
        switch (@event)
        {
            case KEYCARD_RED_ALERT:
                SSsecurity_level.set_level(SEC_LEVEL_RED);
                break;
            case KEYCARD_EMERGENCY_MAINTENANCE_ACCESS:
                await make_maint_all_access();
                break;
            case KEYCARD_BSA_UNLOCK:
                toggle_bluespace_artillery();
                break;
        }
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/security_levels/keycard_authentication.dm:168
    public static async DmTask make_maint_all_access()
    {
        foreach (area_station_maintenance area in Dm.each<area_station_maintenance>(GLOB.areas))
        {
            foreach (object zlevel_turfs in Dm.each(area.get_zlevel_turf_lists()))
            {
                foreach (object area_turf in Dm.each(zlevel_turfs))
                {
                    foreach (obj_machinery_door_airlock airlock in Dm.each<obj_machinery_door_airlock>((turf)area_turf))
                    {
                        airlock.emergency = TRUE;
                        await airlock.update_icon(ALL, 0);
                    }
                }
            }
        }

        minor_announce(I18n.T("Access restrictions on maintenance and external airlocks have been lifted."), I18n.T("Attention! Station-wide emergency declared!"), 1);
        GLOB.emergency_access = TRUE;
        // SSblackbox.record_feedback("nested tally", "keycard_auths", 1, list("emergency maintenance access", "enabled"))
        Dm.Deferred("code/modules/security_levels/keycard_authentication.dm:178", "make_maint_all_access: the blackbox tally (statistics, M10+)");
    }
    // tg: code/modules/security_levels/keycard_authentication.dm:180
    public static async DmTask revoke_maint_all_access()
    {
        foreach (area_station_maintenance area in Dm.each<area_station_maintenance>(GLOB.areas))
        {
            foreach (object zlevel_turfs in Dm.each(area.get_zlevel_turf_lists()))
            {
                foreach (object area_turf in Dm.each(zlevel_turfs))
                {
                    foreach (obj_machinery_door_airlock airlock in Dm.each<obj_machinery_door_airlock>((turf)area_turf))
                    {
                        airlock.emergency = FALSE;
                        await airlock.update_icon(ALL, 0);
                    }
                }
            }
        }

        minor_announce(I18n.T("Access restrictions in maintenance areas have been restored."), I18n.T("Attention! Station-wide emergency rescinded:"));
        GLOB.emergency_access = FALSE;
        // SSblackbox.record_feedback("nested tally", "keycard_auths", 1, list("emergency maintenance access", "disabled"))
        Dm.Deferred("code/modules/security_levels/keycard_authentication.dm:190", "revoke_maint_all_access: the blackbox tally (statistics, M10+)");
    }

    // tg: code/modules/security_levels/keycard_authentication.dm:192
    public static void toggle_bluespace_artillery()
    {
        GLOB.bsa_unlock = Dm.truthy(GLOB.bsa_unlock) ? FALSE : TRUE;
        minor_announce(I18n.T("Bluespace Artillery firing protocols have been []", Dm.truthy(GLOB.bsa_unlock) ? I18n.T("unlocked") : I18n.T("locked")), I18n.T("Weapons Systems Update:"));
        // SSblackbox.record_feedback: statistics (M10+)
    }
}

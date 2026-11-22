// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/porta_turret/turret_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4d, D316): the turret control panel. Left out: the panel's emissive overlays (DEVIATION DV12) and the
// AI's and the cyborgs' ctrl- and alt-clicks on it (_onclick/ai.dm:285-294, cyborg.dm:163-183: live-partial rows, the silicons'
// click handling is not ported). The var block is generated (D111).

// tg: code/game/machinery/porta_turret/turret_control.dm:1
public partial class obj_machinery_turretid : obj_machinery
{
    // tg: code/game/machinery/porta_turret/turret_control.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        else
            locked = FALSE;
        power_change();

        area control_area_instance = null;

        if (Dm.truthy(control_area))
        {
            control_area_instance = get_area_instance_from_text(control_area) as area;
            if (control_area_instance == null)
                log_mapping(Dm.text("Bad control_area path for [] at []: []", this, AREACOORD(this), control_area));
        }

        if (control_area_instance == null)
            control_area_instance = get_area(this) as area;

        // for(var/obj/machinery/porta_turret/T in control_area_instance): an area's contents are its turfs here
        // (Builtin/area.builtin.cs), so the turrets are looked for on the area's turfs, as BYOND lists them
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(control_area_instance?.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                foreach (obj_machinery_porta_turret T in Dm.each<obj_machinery_porta_turret>(area_turf.contents))
                    Dm.combine(ref turrets, WEAKREF(T));
            }
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:50
    public override object Destroy(float? force = null)
    {
        turrets.Cut();
        return base.Destroy(force);
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:54
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            if (Dm.truthy(enabled))
            {
                Dm.append(dot, mutable_appearance(icon, "button_left"));
                if (Dm.truthy(lethal))
                    Dm.append(dot, mutable_appearance(icon, "button_right"));
            }
            return dot;
        }

        if (Dm.truthy(enabled))
        {
            Dm.append(dot, mutable_appearance(icon, "button_left"));
            // DEVIATION(DV12): . += emissive_appearance(icon, "emissive_button_left", src), the glow on the emissive plane, is not created
            if (Dm.truthy(lethal))
            {
                Dm.append(dot, mutable_appearance(icon, "kill"));
                Dm.append(dot, mutable_appearance(icon, "button_right"));
                // DEVIATION(DV12): . += emissive_appearance(icon, "emissive_button_right", src)
            }
            else
                Dm.append(dot, mutable_appearance(icon, "stun"));
        }
        else
            Dm.append(dot, mutable_appearance(icon, "standby"));
        // DEVIATION(DV12): . += emissive_appearance(icon, "emissive_screen", src)
        return dot;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:76
    public override object power_change()
    {
        object dot = base.power_change();
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:80
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = Dm.list();
        Dm.append(dot, await base.examine(user, __extra));
        if (issilicon(user) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            Dm.append(dot, span_notice(I18n.T("Ctrl-click [] to [] turrets.", this, Dm.truthy(enabled) ? "disable" : "enable")));
            Dm.append(dot, span_notice(I18n.T("Alt-click [] to set turrets to [].", this, Dm.truthy(lethal) ? "stun" : "kill")));
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:86
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = NONE;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return dot;

        obj_item_multitool multi_tool = tool as obj_item_multitool;
        if (Dm.truthy(multi_tool.buffer) && istype(multi_tool.buffer, typeof(obj_machinery_porta_turret)))
        {
            Dm.combine(ref turrets, WEAKREF(multi_tool.buffer as datum));
            to_chat(user, span_notice(I18n.T("You link \\the [] with \\the [].", multi_tool.buffer, this)));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:96
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return NONE;

        if (issilicon(user))
            return await attack_hand(user);

        object card = tool.GetID();

        if (card == null)
            return NONE;

        if (!Dm.truthy(check_access(card as obj_item)))
        {
            to_chat(user, span_alert(I18n.T("Access denied.")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(user, span_warning(I18n.T("The turret control is unresponsive!")));
            return ITEM_INTERACT_BLOCKING;
        }

        locked = Dm.truthy(locked) ? FALSE : TRUE;
        to_chat(user, span_notice(I18n.T("You [] the panel.", Dm.truthy(locked) ? "lock" : "unlock")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:121
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        balloon_alert(user, I18n.T("access analysis module shorted"));
        Dm.combine(ref obj_flags, EMAGGED);
        locked = FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:129
    public virtual object is_ai_locked(mob user = null)
    {
        if (!issilicon(user))
            return FALSE;
        if (Dm.truthy(ailock) || Dm.truthy((user as mob_living).has_status_effect(typeof(datum_status_effect_firewalled))))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:136
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (Dm.truthy(is_ai_locked(user)))
        {
            to_chat(user, span_warning(I18n.T("There seems to be a firewall preventing you from accessing this device!")));
            return null;
        }

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "TurretControl", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:146
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        return Dm.truthy(is_ai_locked(user)) ? UI_CLOSE : await base.ui_status(user, state);
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:149
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["locked"] = locked;
        data["siliconUser"] = HAS_SILICON_ACCESS(user) ? TRUE : FALSE;
        data["enabled"] = enabled;
        data["lethal"] = lethal;
        data["shootCyborgs"] = shoot_cyborgs;
        return data;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:158
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = ui.user;
        if (Dm.truthy(is_ai_locked(user)))
            return dot;

        switch (action as string)
        {
            case "lock":
                if (!HAS_SILICON_ACCESS(user))
                    return dot;
                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                {
                    to_chat(user, span_warning(I18n.T("The turret control is unresponsive!")));
                    return dot;
                }
                locked = Dm.truthy(locked) ? FALSE : TRUE;
                return TRUE;
            case "power":
                toggle_on(user);
                return TRUE;
            case "mode":
                toggle_lethal(user);
                return TRUE;
            case "shoot_silicons":
                shoot_silicons(user);
                return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:185
    public virtual void toggle_lethal(mob user = null)
    {
        lethal = Dm.truthy(lethal) ? FALSE : TRUE;
        if (Dm.truthy(user))
        {
            string enabled_or_disabled = Dm.truthy(lethal) ? "disabled" : "enabled";
            balloon_alert(user, I18n.T("safeties []", enabled_or_disabled));
            add_hiddenprint(user);
            log_combat(user, this, Dm.text("[] lethals on", enabled_or_disabled));
        }
        updateTurrets();
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:194
    public virtual void toggle_on(mob user = null)
    {
        enabled = Dm.truthy(enabled) ? FALSE : TRUE;
        if (Dm.truthy(user))
        {
            string enabled_or_disabled = Dm.truthy(enabled) ? "enabled" : "disabled";
            balloon_alert(user, I18n.T("[]", enabled_or_disabled));
            add_hiddenprint(user);
            log_combat(user, this, Dm.text("[]", Dm.truthy(enabled) ? "enabled" : "disabled"));
        }
        updateTurrets();
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:203
    public virtual void shoot_silicons(mob user = null)
    {
        shoot_cyborgs = Dm.truthy(shoot_cyborgs) ? FALSE : TRUE;
        if (Dm.truthy(user))
        {
            string status = Dm.truthy(shoot_cyborgs) ? "Shooting Borgs" : "Not Shooting Borgs";
            balloon_alert(user, LOWER_TEXT(status));
            add_hiddenprint(user);
            log_combat(user, this, Dm.text("[]", status));
        }
        updateTurrets();
    }

    // tg: code/game/machinery/porta_turret/turret_control.dm:212
    public virtual void updateTurrets()
    {
        foreach (datum_weakref turret_ref in Dm.each_cast<datum_weakref>(turrets?.Copy()))
        {
            obj_machinery_porta_turret turret = turret_ref.resolve() as obj_machinery_porta_turret;
            if (turret == null)
            {
                Dm.remove(turrets, turret_ref);
                continue;
            }
            turret.setState(enabled, lethal, shoot_cyborgs);
        }
        update_appearance();
    }
}

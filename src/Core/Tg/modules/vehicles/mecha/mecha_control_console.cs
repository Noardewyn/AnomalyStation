// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_control_console.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/mecha_control_console.dm:1
public partial class obj_machinery_computer_mecha : obj_machinery_computer
{
    // tg: code/modules/vehicles/mecha/mecha_control_console.dm:10
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "ExosuitControlConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_control_console.dm:16
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList trackerlist = Dm.list();
        foreach (obj_vehicle_sealed_mecha MC in Dm.each<obj_vehicle_sealed_mecha>(GLOB.mechas_list))
            Dm.append(ref trackerlist, MC.trackers);

        DmList mechs = Dm.list();
        data["mechs"] = mechs;
        foreach (obj_item_mecha_parts_mecha_tracking MT in Dm.each<obj_item_mecha_parts_mecha_tracking>(trackerlist))
        {
            if (MT.chassis == null)
                continue;
            obj_vehicle_sealed_mecha M = MT.chassis;
            DmList mech_data = Dm.list(
                Dm.kv("name", M.name),
                Dm.kv("integrity", round(Dm.num(M.get_integrity()) / Dm.num(M.max_integrity) * 100)),
                Dm.kv("charge", M.cell != null ? round(Dm.num(M.cell.percent())) : null),
                Dm.kv("airtank", Dm.truthy(Dm.bitand(M.mecha_flags, IS_ENCLOSED)) ? await M.return_pressure() : null),
                Dm.kv("pilot", M.return_drivers()),
                Dm.kv("location", get_area_name(M, TRUE)),
                Dm.kv("emp_recharging", MT.recharging),
                Dm.kv("tracker_ref", REF(MT)));
            if (M is obj_vehicle_sealed_mecha_ripley workmech)
                mech_data["cargo_space"] = round(Dm.num(workmech.cargo_hold.contents.Count) / Dm.num(workmech.cargo_hold.cargo_capacity) * 100);

            mechs.Add(mech_data);
        }

        return data;
    }

    // tg: code/modules/vehicles/mecha/mecha_control_console.dm:46
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "send_message"))
        {
            if (Dm.locate_ref(@params["tracker_ref"]) is not obj_item_mecha_parts_mecha_tracking our_mecha_tracker)
                return dot;
            object message = await tgui_input_text(usr, "Input message", "Transmit message", max_length: MAX_MESSAGE_LEN);
            obj_vehicle_sealed_mecha our_mecha = our_mecha_tracker.chassis;
            if (Dm.truthy(trim(message)) && our_mecha != null)
            {
                to_chat(our_mecha.occupants, message);
                to_chat(usr, span_notice(I18n.T("Message sent.")));
                dot = TRUE;
            }
        }
        else if (Dm.eq(action, "shock"))
        {
            if (Dm.locate_ref(@params["tracker_ref"]) is not obj_item_mecha_parts_mecha_tracking our_mecha_tracker)
                return dot;
            obj_vehicle_sealed_mecha our_mecha = our_mecha_tracker.chassis;
            if (our_mecha != null)
            {
                await our_mecha_tracker.remote_emp_triggered();
                usr.log_message(Dm.text("has activated remote EMP on exosuit [], located at [], which is currently []", our_mecha, loc_name(our_mecha), Dm.truthy(LAZYLEN(our_mecha.occupants)) ? Dm.text("occupied by [].", our_mecha.occupants.Join(", ")) : "without a pilot."), LOG_ATTACK);
                usr.log_message(Dm.text("has activated remote EMP on exosuit [], located at [], which is currently []", our_mecha, loc_name(our_mecha), Dm.truthy(LAZYLEN(our_mecha.occupants)) ? Dm.text("occupied by [].", our_mecha.occupants.Join(", ")) : "without a pilot."), LOG_GAME, log_globally: FALSE);
                message_admins(Dm.text("[][] has activated remote EMP on exosuit [][], which is currently []", key_name_admin(usr), ADMIN_FLW(usr), our_mecha, ADMIN_JMP(our_mecha), Dm.truthy(LAZYLEN(our_mecha.occupants)) ? Dm.text("occupied by [][].", our_mecha.occupants.Join(","), ADMIN_FLW(our_mecha)) : "without a pilot."));
                dot = TRUE;
            }
        }
        return dot;
    }
}

// tg: code/modules/vehicles/mecha/mecha_control_console.dm:81
public partial class obj_item_mecha_parts_mecha_tracking : obj_item_mecha_parts
{
    /**
     * Returns a html formatted string describing attached mech status
     */
    // tg: code/modules/vehicles/mecha/mecha_control_console.dm:98
    public virtual async DmTask<object> get_mecha_info()
    {
        if (chassis == null)
            return FALSE;

        DmList output = Dm.list();

        object cell_charge = chassis.get_charge();
        output.Add(Dm.text("[] []", span_bold(I18n.T("Name:")), chassis.name));
        output.Add(Dm.text("[] []%", span_bold(I18n.T("Integrity:")), round(Dm.num(chassis.get_integrity()) / Dm.num(chassis.max_integrity) * 100, 0.01f)));
        output.Add(Dm.text("[] []", span_bold(I18n.T("Cell Charge:")), cell_charge == null ? I18n.T("Not Found") : Dm.text("[]%", chassis.cell.percent())));
        output.Add(Dm.text("[] []", span_bold(I18n.T("Cabin Pressure:")), Dm.truthy(Dm.bitand(chassis.mecha_flags, IS_ENCLOSED)) ? Dm.text("[] kPa", round(Dm.num(await chassis.return_pressure()), 0.01f)) : I18n.T("Not Sealed")));
        output.Add(Dm.text("[] []", span_bold(I18n.T("Pilot:")), english_list(chassis.return_drivers() as DmList, nothing_text: I18n.T("None"))));
        output.Add(Dm.text("[] []", span_bold(I18n.T("Current Location:")), get_area_name(chassis, TRUE) ?? I18n.T("Unknown")));

        if (chassis is obj_vehicle_sealed_mecha_ripley)
        {
            obj_item_mecha_parts_mecha_equipment_ejector cargo_holder = Dm.locate<obj_item_mecha_parts_mecha_equipment_ejector>(chassis.equip_by_category[MECHA_UTILITY]);
            output.Add(Dm.text("[] []%", span_bold(I18n.T("Used Cargo Space:")), round(Dm.num(cargo_holder.contents.Count) / Dm.num(cargo_holder.cargo_capacity) * 100, 0.01f)));
        }

        return jointext(output, "\n");
    }

    // tg: code/modules/vehicles/mecha/mecha_control_console.dm:117
    public override object Destroy(float? force = null)
    {
        if (chassis != null && Dm.@in(this, chassis.trackers))
            Dm.remove(chassis.trackers, this);
        chassis = null;
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/mecha_control_console.dm:124
    public override async DmTask<object> try_attach_part(mob user = null, obj_vehicle_sealed_mecha mecha_to_attach = null, object attach_right = null)
    {
        if (!Dm.truthy(Dm.bitand(mecha_to_attach.mecha_flags, flag_to_check)))
        {
            to_chat(user, span_notice(I18n.T("[] is incompatible with [].", this, mecha_to_attach)));
            return null;
        }

        foreach (obj_item_mecha_parts_mecha_tracking tracker in Dm.each<obj_item_mecha_parts_mecha_tracking>(mecha_to_attach.trackers))
        {
            if (Dm.eq(tracker.flag_to_check, flag_to_check))
            {
                to_chat(user, span_notice(I18n.T("There already exists a version of [] attached to [].", this, mecha_to_attach)));
                return null;
            }
        }

        object dot = await base.try_attach_part(user, mecha_to_attach, attach_right);
        if (!Dm.truthy(dot))
            return dot;
        Dm.append(ref mecha_to_attach.trackers, this);
        mecha_to_attach.diag_hud_set_mechtracking();
        chassis = mecha_to_attach;
        return dot;
    }

    /**
     * Attempts to EMP mech that the tracker is attached to, if there is one and tracker is not on cooldown
     */
    // tg: code/modules/vehicles/mecha/mecha_control_console.dm:144
    public virtual async DmTask remote_emp_triggered()
    {
        if (Dm.truthy(recharging))
            return;
        if (chassis == null)
            return;
        await chassis.emp_act(EMP_HEAVY);
        _addtimer(Dm.@new<datum_callback>(this, nameof(recharge)), 5 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/vehicles/mecha/mecha_control_console.dm", line: 150);
        recharging = TRUE;
    }

    /**
     * Resets recharge variable, allowing tracker to be EMP pulsed again
     */
    // tg: code/modules/vehicles/mecha/mecha_control_console.dm:156
    public virtual void recharge() => recharging = FALSE;
}

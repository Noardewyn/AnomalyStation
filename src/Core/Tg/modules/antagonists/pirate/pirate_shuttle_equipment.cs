// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/pirate/pirate_shuttle_equipment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps P4c, D298): the cargo hold pad and its control terminal (:172-422), the civilian bounty pad's base. The pirates'
// shuttle equipment, their exports (the ransom and the loot) and their CargoHoldTerminal window wait for the pirates (PORT-CHECKLIST,
// antagonists). The var blocks are generated (D111).

//Pad & Pad Terminal
// tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:173
public partial class obj_machinery_piratepad : obj_machinery
{
    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:184
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        if (tool is obj_item_multitool I)
        {
            I.set_buffer(this);
            balloon_alert(user, I18n.T("saved to multitool buffer"));
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:191
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null) => default_deconstruction_screwdriver(user, tool);

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:194
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null) => await screwdriver_act(user, tool);

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:197
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null) => await default_deconstruction_crowbar(user, tool);

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:200
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null) => await crowbar_act(user, tool);

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:203
    public virtual void set_is_sending(object value = null)
    {
        if (Dm.eq(is_sending, value))
            return;
        is_sending = Dm.num(value);
        update_appearance();
    }

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:209
    public virtual void finish_sending()
    {
        set_is_sending(FALSE);
        Dm.flick(Dm.text("[]-beam", base_icon_state), this);
    }

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:213
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(panel_open))
            icon_state = Dm.text("[]-open", base_icon_state);
        else if (Dm.truthy(is_sending))
            icon_state = Dm.text("[]", base_icon_state);
        else
            icon_state = Dm.text("[]-off", base_icon_state);
        return dot;
    }
}

// tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:222
public partial class obj_machinery_computer_piratepad_control : obj_machinery_computer
{
    ///Typecache of things that shouldn't be sold and shouldn't have their contents sold.
    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:244  var/static/list/nosell_typecache (a type static, rules 10.6)
    protected static DmList nosell_typecache
    {
        get => TgState.Current.obj_machinery_computer_piratepad_control__nosell_typecache;
        set => TgState.Current.obj_machinery_computer_piratepad_control__nosell_typecache = value;
    }

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:248
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        if (nosell_typecache == null)
            nosell_typecache = typecacheof(typeof(mob_living_silicon_robot)) as DmList;
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:254  (defined twice: its ..() is the civilian bounty file's, multitool_act__d0)
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await multitool_act__d0(user, tool);
        if (tool is obj_item_multitool I && I.buffer is obj_machinery_piratepad)
        {
            to_chat(user, span_notice(I18n.T("You link [] with [] in [] buffer.", this, I.buffer, I)));
            pad_ref = WEAKREF(I.buffer) as datum_weakref;
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:261
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (Dm.truthy(cargo_hold_id))
        {
            foreach (obj_machinery_piratepad P in Dm.each_cast<obj_machinery_piratepad>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_piratepad))))
            {
                if (Dm.eq(P.cargo_hold_id, cargo_hold_id))
                {
                    pad_ref = WEAKREF(P) as datum_weakref;
                    return;
                }
            }
        }
        else
        {
            obj_machinery_piratepad pad = Dm.locate<obj_machinery_piratepad>(range(4, this));
            pad_ref = WEAKREF(pad) as datum_weakref;
        }
    }

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:272
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, interface_type, name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:279
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["points"] = points;
        data["pad"] = pad_ref?.resolve() != null ? TRUE : FALSE;
        data["sending"] = sending;
        data["status_report"] = status_report;
        return data;
    }

    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:287
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (pad_ref?.resolve() == null)
            return dot;

        switch (Dm.str(action))
        {
            case "recalc":
                await recalc();
                dot = TRUE;
                break;
            case "send":
                await start_sending(@params["global"], usr);
                dot = TRUE;
                break;
            case "stop":
                stop_sending();
                dot = TRUE;
                break;
        }
        return dot;
    }

    /// Calculates the predicted value of the items on the pirate pad
    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:306
    public virtual async DmTask<object> recalc()
    {
        if (Dm.truthy(sending))
            return null;

        status_report = I18n.T("Predicted value: ");
        float value = 0;

        var pad = pad_ref?.resolve() as obj_machinery_piratepad;
        datum_export_report report = await pirate_export_loop(pad);

        foreach (datum_export exported_datum in Dm.each<datum_export>(report.total_amount))
        {
            status_report += Dm.text("[]", exported_datum.total_printout(report, notes: FALSE));
            status_report += " ";
            value += Dm.num(report.total_value[exported_datum]);
        }

        if (value == 0)
            status_report += "0";
        return null;
    }

    /**
     * Sorts through all items on the control pad via pirate_export_loop, then generates a printout to view in the TGUI.
     */
    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:327
    public virtual async DmTask<object> send(object check_global = null, mob user = null)
    {
        if (!Dm.truthy(sending))
            return null;

        var pad = pad_ref?.resolve() as obj_machinery_piratepad;
        datum_export_report report = await pirate_export_loop(pad, dry_run: FALSE);

        status_report = I18n.T("Sold: ");
        float value = 0;
        foreach (datum_export exported_datum in Dm.each<datum_export>(report.total_amount))
        {
            object export_text = exported_datum.total_printout(report, notes: FALSE); //Don't want nanotrasen messages, makes no sense here.
            if (!Dm.truthy(export_text))
                continue;

            status_report += Dm.text("[]", export_text);
            status_report += " ";
            value += Dm.num(report.total_value[exported_datum]);
        }

        if (total_report == null)
            total_report = report;
        else
        {
            Dm.append(ref total_report.exported_atoms, report.exported_atoms);
            foreach (datum_export exported_datum in Dm.each<datum_export>(report.total_amount))
            {
                total_report.total_amount[exported_datum] = Dm.add(total_report.total_amount[exported_datum], report.total_amount[exported_datum]);
                total_report.total_value[exported_datum] = Dm.add(total_report.total_value[exported_datum], report.total_value[exported_datum]);
            }
            playsound(loc, Dm.resource("sound/machines/wewewew.ogg"), 70, TRUE);
        }

        points += value;

        if (value == 0)
            status_report += I18n.T("Nothing");

        pad.visible_message(span_notice(I18n.T("[] activates!", pad)));
        pad.finish_sending();
        sending = FALSE;
        return null;
    }

    ///The loop that calculates the value of stuff on a pirate pad, or plain sell them if dry_run is FALSE.
    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:364
    public virtual async DmTask<datum_export_report> pirate_export_loop(obj_machinery_piratepad pad = null, object dry_run = null)
    {
        dry_run ??= TRUE;
        var report = Dm.@new<datum_export_report>();
        foreach (atom_movable item_on_pad in Dm.each_cast<atom_movable>(get_turf(pad).contents.Copy()))
        {
            if (item_on_pad == pad)
                continue;
            DmList hidden_mobs = Dm.list();
            bool skip_movable = false;
            DmList item_contents = item_on_pad.get_all_contents() as DmList;
            foreach (atom_movable thing in Dm.each_cast<atom_movable>(reverse_range(item_contents)))
            {
                ///Don't destroy/sell stuff like the captain's laser gun, or borgs.
                if (Dm.truthy(Dm.bitand(thing.resistance_flags, INDESTRUCTIBLE)) || Dm.truthy(is_type_in_typecache(thing, nosell_typecache)))
                {
                    skip_movable = true;
                    break;
                }
                if (isliving(thing))
                    Dm.append(ref hidden_mobs, thing);
            }
            if (skip_movable)
                continue;
            foreach (mob_living hidden in Dm.each_cast<mob_living>(hidden_mobs))
            {
                ///Sell mobs, but leave their contents intact.
                await export_single_item(hidden, apply_elastic: FALSE, dry_run: dry_run, external_report: report, export_markets: Dm.list(EXPORT_MARKET_STATION, EXPORT_MARKET_PIRACY));
            }
            ///there are still licing mobs inside that item. Stop, don't sell it ffs.
            if (Dm.locate<mob_living>(item_on_pad.get_all_contents()) != null)
                continue;
            await export_item_and_contents(item_on_pad, apply_elastic: FALSE, dry_run: dry_run, delete_unsold: FALSE, external_report: report, ignore_typecache: nosell_typecache, export_markets: Dm.list(EXPORT_MARKET_STATION, EXPORT_MARKET_PIRACY));
        }
        return report;
    }

    /// Prepares to sell the items on the pad
    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:391
    public virtual async DmTask start_sending(object check_global = null, mob user = null)
    {
        var pad = pad_ref?.resolve() as obj_machinery_piratepad;
        if (pad == null)
        {
            status_report = I18n.T("No pad detected. Build or link a pad.");
            pad.audible_message(span_notice(I18n.T("[] beeps.", pad)));
            return;
        }
        if (Dm.truthy(pad?.panel_open))
        {
            status_report = I18n.T("Please screwdrive pad closed to send. ");
            pad.audible_message(span_notice(I18n.T("[] beeps.", pad)));
            return;
        }
        if (Dm.truthy(sending))
            return;
        sending = TRUE;
        status_report = I18n.T("Sending... ");
        pad.visible_message(span_notice(I18n.T("[] starts charging up.", pad)));
        pad.set_is_sending(TRUE);
        sending_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(send), check_global, user), warmup_time, TIMER_STOPPABLE, file: "code/modules/antagonists/pirate/pirate_shuttle_equipment.dm", line: 410);
        if (Dm.truthy(load_holding_facility))
        {
            //We ensure that the holding facility is loaded in time in case we're selling mobs.
            await SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NINJA_HOLDING_FACILITY);
        }
    }

    /// Finishes the sending state of the pad
    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:413
    public virtual void stop_sending(object custom_report = null)
    {
        if (!Dm.truthy(sending))
            return;
        sending = FALSE;
        status_report = I18n.T("Ready for delivery.");
        if (Dm.truthy(custom_report))
            status_report = Dm.str(custom_report);
        var pad = pad_ref?.resolve() as obj_machinery_piratepad;
        pad.set_is_sending(FALSE);
        deltimer(sending_timer);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/antagonists/pirate/pirate_shuttle_equipment.dm:244  var/static/list/nosell_typecache
    internal DmList obj_machinery_computer_piratepad_control__nosell_typecache;
}

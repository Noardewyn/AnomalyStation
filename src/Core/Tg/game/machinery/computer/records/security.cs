// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/records/security.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but its circuit components (gaps SC2, D333): the security records console and its laptops in tgui's
// SecurityRecords window: the crimes and citations of the crew's records (added, edited and voided by their author or one with
// armory access), the wanted status that the security HUDs show, the security note, deleting a record, a new record from a photo,
// and the printouts: the rapsheet paper and the missing and wanted posters with the record's front photo. An EMP scrambles the
// records. The arrest console's circuit components (arrest_console_data, arrest_console_arrest) and the console's USB port wait for
// the integrated circuits, and the space ninja's drain (modules_ninja.dm) is a live-partial row (not-translated.md 198). The
// blackbox's citation records (SSblackbox.ReportCitation) are the database's. The var blocks are generated (D111).

// tg: code/game/machinery/computer/records/security.dm:8
public partial class obj_machinery_computer_records_security : obj_machinery_computer_records
{
    // tg: code/game/machinery/computer/records/security.dm:6  #define MAX_CRIME_NAME_LEN 24 (Editing this will cause UI issues.)
    private const int MAX_CRIME_NAME_LEN = 24;

    // tg: code/game/machinery/computer/records/security.dm:38
    [DmParams("mapload", "C")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/usb_port, typecacheof(list(/obj/item/circuit_component/arrest_console_data,
        // /obj/item/circuit_component/arrest_console_arrest), only_root_path = TRUE)): the integrated circuits (not-translated.md 198)
        return dot;
    }

    // tg: code/game/machinery/computer/records/security.dm:47
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);

        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))
        {
            if (prob(Dm.num(Dm.div(10, severity))))
            {
                switch (rand(1, 5))
                {
                    case 1:
                        target.name = Dm.str(generate_random_name());
                        break;

                    case 2:
                        target.gender = pick("Male", "Female", "Other");
                        break;
                    case 3:
                        target.age = rand(5, 85);
                        break;
                    case 4:
                        target.wanted_status = (string)pick(WANTED_STATUSES());
                        break;
                    case 5:
                        target.species = pick(get_selectable_species());
                        break;
                }
                continue;
            }

            else if (prob(1))
            {
                qdel(target);
                continue;
            }
        }
        return dot;
    }

    // tg: code/game/machinery/computer/records/security.dm:73
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_photo))
            return NONE;
        await insert_new_record(user, (obj_item_photo)tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/computer/records/security.dm:79
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        if (Dm.truthy(dot))
            return dot;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "SecurityRecords");
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/records/security.dm:89
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;

        data["available_statuses"] = WANTED_STATUSES();
        data["current_user"] = user.name;
        data["higher_access"] = has_armory_access(user);

        DmList records = Dm.list();
        foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))
        {
            DmList citations = Dm.list();
            foreach (datum_crime_citation warrant in Dm.each<datum_crime_citation>(target.citations))
            {
                Dm.append(ref citations, Dm.list(Dm.list(
                    Dm.kv("author", warrant.author),
                    Dm.kv("crime_ref", REF(warrant)),
                    Dm.kv("details", warrant.details),
                    Dm.kv("fine", warrant.fine),
                    Dm.kv("name", warrant.name),
                    Dm.kv("paid", warrant.paid),
                    Dm.kv("time", warrant.time),
                    Dm.kv("valid", warrant.valid),
                    Dm.kv("voider", warrant.voider))));
            }

            DmList crimes = Dm.list();
            foreach (datum_crime crime in Dm.each<datum_crime>(target.crimes))
            {
                Dm.append(ref crimes, Dm.list(Dm.list(
                    Dm.kv("author", crime.author),
                    Dm.kv("crime_ref", REF(crime)),
                    Dm.kv("details", crime.details),
                    Dm.kv("name", crime.name),
                    Dm.kv("time", crime.time),
                    Dm.kv("valid", crime.valid),
                    Dm.kv("voider", crime.voider))));
            }

            Dm.append(ref records, Dm.list(Dm.list(
                Dm.kv("age", target.age),
                Dm.kv("citations", citations),
                Dm.kv("crew_ref", REF(target)),
                Dm.kv("crimes", crimes),
                Dm.kv("fingerprint", target.fingerprint),
                Dm.kv("gender", target.gender),
                Dm.kv("name", target.name),
                Dm.kv("note", target.security_note),
                Dm.kv("rank", target.rank),
                Dm.kv("species", target.species),
                Dm.kv("trim", target.trim),
                Dm.kv("wanted_status", target.wanted_status))));
        }

        data["records"] = records;

        return data;
    }

    // tg: code/game/machinery/computer/records/security.dm:143
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["min_age"] = AGE_MIN;
        data["max_age"] = AGE_MAX;
        return data;
    }

    // tg: code/game/machinery/computer/records/security.dm:149
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = ui.user;

        datum_record_crew target = null;
        if (Dm.truthy(@params["crew_ref"]))
            target = Dm.locate_ref(@params["crew_ref"]) is datum_record_crew crew__1 && Dm.@in(crew__1, GLOB.manifest.general) ? crew__1 : null; // locate(params["crew_ref"]) in GLOB.manifest.general
        if (target == null)
            return FALSE;

        object sw__1 = action;
        if (Dm.eq(sw__1, "add_crime"))
        {
            await add_crime(user, target, @params);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "delete_record"))
        {
            investigate_log(Dm.text("[] deleted record: \"[]\".", user, target), INVESTIGATE_RECORDS);
            qdel(target);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "edit_crime"))
        {
            await edit_crime(user, target, @params);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "invalidate_crime"))
        {
            invalidate_crime(user, target, @params);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "print_record"))
        {
            print_record(user, target, @params);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "set_note"))
        {
            object note = strip_html_full(@params["note"], MAX_MESSAGE_LEN);
            investigate_log(Dm.text("[] has changed the security note of record: \"[]\" from \"[]\" to \"[]\".", user, target, target.security_note, note), INVESTIGATE_RECORDS);
            target.security_note = note;
            return TRUE;
        }
        else if (Dm.eq(sw__1, "set_wanted"))
        {
            object wanted_status = @params["status"];
            if (!Dm.truthy(wanted_status) || !Dm.@in(wanted_status, WANTED_STATUSES()))
                return FALSE;
            if (Dm.eq(wanted_status, WANTED_ARREST) && !Dm.truthy(length(target.crimes)))
                return FALSE;

            investigate_log(Dm.text("[] has been set from [] to [] by [].", target.name, target.wanted_status, wanted_status, key_name(usr)), INVESTIGATE_RECORDS);
            target.wanted_status = (string)wanted_status;

            update_matching_security_huds(target.name);

            return TRUE;
        }

        return FALSE;
    }

    /// Handles adding a crime to a particular record.
    // tg: code/game/machinery/computer/records/security.dm:207
    public virtual async DmTask<object> add_crime(mob user = null, datum_record_crew target = null, DmList @params = null)
    {
        object input_name = strip_html_full(@params["name"], MAX_CRIME_NAME_LEN);
        if (!Dm.truthy(input_name))
        {
            to_chat(usr, span_warning(I18n.T("You must enter a name for the crime.")));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 75, TRUE);
            return FALSE;
        }

        object max = config.Get(typeof(datum_config_entry_number_maxfine)); // CONFIG_GET(number/maxfine)
        if (Dm.gt(@params["fine"], max))
        {
            to_chat(usr, span_warning(I18n.T("The maximum fine is [] [].", max, MONEY_NAME)));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 75, TRUE);
            return FALSE;
        }

        object input_details = null;
        if (Dm.truthy(@params["details"]))
            input_details = strip_html_full(@params["details"], MAX_MESSAGE_LEN);

        if (Dm.eq(@params["fine"], 0))
        {
            datum_crime new_crime = Dm.@new<datum_crime>(Dm.named("name", input_name), Dm.named("details", input_details), Dm.named("author", usr));
            Dm.append(ref target.crimes, new_crime);
            investigate_log(Dm.text("New Crime: <strong>[]</strong> | Added to [] by []. Their previous status was []", input_name, target.name, key_name(user), target.wanted_status), INVESTIGATE_RECORDS);
            // SSblackbox.ReportCitation(REF(new_crime), user.ckey, user.real_name, target.name, input_name, input_details): the database's
            // citation table (rules 13, not-translated.md 198)
            target.wanted_status = WANTED_ARREST;

            update_matching_security_huds(target.name);

            return TRUE;
        }

        datum_crime_citation new_citation = Dm.@new<datum_crime_citation>(Dm.named("name", input_name), Dm.named("details", input_details), Dm.named("author", usr), Dm.named("fine", @params["fine"]));

        Dm.append(ref target.citations, new_citation);
        new_citation.alert_owner(user, this, target.name, I18n.T("You have been issued a [][] citation for []. Fines are payable at Security.", @params["fine"], MONEY_SYMBOL, input_name));
        investigate_log(Dm.text("New Citation: <strong>[]</strong> Fine: [] | Added to [] by []", input_name, @params["fine"], target.name, key_name(user)), INVESTIGATE_RECORDS);
        // SSblackbox.ReportCitation(REF(new_citation), user.ckey, user.real_name, target.name, input_name, input_details, params["fine"]): the
        // database's citation table (rules 13, not-translated.md 198)

        return TRUE;
    }

    /// Handles editing a crime on a particular record.
    // tg: code/game/machinery/computer/records/security.dm:245
    public virtual async DmTask<object> edit_crime(mob user = null, datum_record_crew target = null, DmList @params = null)
    {
        datum_crime editing_crime = Dm.locate_ref(@params["crime_ref"]) is datum_crime crime__1 && Dm.@in(crime__1, target.crimes) ? crime__1 : null; // locate(params["crime_ref"]) in target.crimes
        if (!Dm.truthy(editing_crime?.valid))
            return FALSE;

        if (!Dm.eq(user, editing_crime.author) && !Dm.truthy(has_armory_access(user))) // only warden/hos/command can edit crimes they didn't author
        {
            investigate_log(Dm.text("[] attempted to edit crime: \"[]\" for target: \"[]\" but failed due to lacking armoury access and not being the author of the crime.", user, editing_crime.name, target.name), INVESTIGATE_RECORDS);
            return FALSE;
        }

        if (Dm.truthy(@params["name"]) && length(@params["name"]) > 2 && !Dm.eq(@params["name"], editing_crime.name))
        {
            object new_name = strip_html_full(@params["name"], MAX_CRIME_NAME_LEN);
            investigate_log(Dm.text("[] edited crime: \"[]\" for target: \"[]\", changing the name to: \"[]\".", user, editing_crime.name, target.name, new_name), INVESTIGATE_RECORDS);
            editing_crime.name = new_name;
            // SSblackbox.ReportCitation(REF(editing_crime), message = new_name): the database's citation table (not-translated.md 198)
            return TRUE;
        }

        if (Dm.truthy(@params["description"]) && length(@params["description"]) > 2 && !Dm.eq(@params["name"], editing_crime.name))
        {
            object new_details = strip_html_full(@params["description"], MAX_MESSAGE_LEN);
            investigate_log(Dm.text("[] edited crime \"[]\" for target: \"[]\", changing the details to: \"[]\" from: \"[]\".", user, editing_crime.name, target.name, new_details, editing_crime.details), INVESTIGATE_RECORDS);
            editing_crime.details = new_details;
            // SSblackbox.ReportCitation(REF(editing_crime), description = new_details): the database's citation table (not-translated.md 198)
            return TRUE;
        }

        return FALSE;
    }

    /// Deletes security information from a record.
    // tg: code/game/machinery/computer/records/security.dm:271
    public override object expunge_record_info(datum_record_crew target = null)
    {
        target.citations.Cut();
        target.crimes.Cut();
        target.security_note = null;
        target.wanted_status = WANTED_NONE;

        return TRUE;
    }

    /// Only qualified personnel can edit records.
    // tg: code/game/machinery/computer/records/security.dm:280
    public virtual object has_armory_access(mob user = null)
    {
        if (HAS_SILICON_ACCESS(user))
            return TRUE;

        if (!isliving(user))
            return FALSE;
        mob_living player = (mob_living)user;

        obj_item_card_id auth = player.get_idcard(TRUE) as obj_item_card_id;
        if (auth == null)
            return FALSE;

        if (!Dm.@in(ACCESS_ARMORY, auth.GetAccess()))
            return FALSE;

        return TRUE;
    }

    /// Voids crimes, or sets someone to discharged if they have none left.
    // tg: code/game/machinery/computer/records/security.dm:298
    public virtual object invalidate_crime(mob user = null, datum_record_crew target = null, DmList @params = null)
    {
        datum_crime to_void = Dm.locate_ref(@params["crime_ref"]) is datum_crime crime__1 && Dm.@in(crime__1, target.crimes) ? crime__1 : null; // locate(params["crime_ref"]) in target.crimes
        float acquitted = TRUE;
        if (to_void == null)
        {
            to_void = Dm.locate_ref(@params["crime_ref"]) is datum_crime crime__2 && Dm.@in(crime__2, target.citations) ? crime__2 : null; // locate(params["crime_ref"]) in target.citations
            // No need to change status after invalidatation of citation
            acquitted = FALSE;
            if (to_void == null)
                return FALSE;
        }

        if (!Dm.eq(user, to_void.author) && !Dm.truthy(has_armory_access(user)))
            return FALSE;

        to_void.valid = FALSE;
        to_void.voider = user;
        investigate_log(Dm.text("[] has invalidated []'s crime: []", key_name(user), target.name, to_void.name), INVESTIGATE_RECORDS);

        foreach (datum_crime incident in Dm.each<datum_crime>(target.crimes))
        {
            if (!Dm.truthy(incident.valid))
                continue;
            acquitted = FALSE;
            break;
        }

        if (Dm.truthy(acquitted))
        {
            target.wanted_status = WANTED_DISCHARGED;
            investigate_log(Dm.text("[] has invalidated []'s last valid crime. Their status is now [].", key_name(user), target.name, WANTED_DISCHARGED), INVESTIGATE_RECORDS);

            update_matching_security_huds(target.name);
        }
        return TRUE;
    }

    /// Finishes printing, resets the printer.
    // tg: code/game/machinery/computer/records/security.dm:329
    public virtual async DmTask<object> print_finish(obj_item printable = null)
    {
        printing = FALSE;
        playsound(this, Dm.resource("sound/machines/terminal/terminal_eject.ogg"), 100, TRUE);
        await printable.forceMove(loc);

        return TRUE;
    }

    /// Handles printing records via UI. Takes the params from UI_act.
    // tg: code/game/machinery/computer/records/security.dm:337
    public virtual object print_record(mob user = null, datum_record_crew target = null, DmList @params = null)
    {
        if (Dm.truthy(printing))
        {
            balloon_alert(user, I18n.T("printer busy"));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 100, TRUE);
            return FALSE;
        }

        printing = TRUE;
        balloon_alert(user, I18n.T("printing"));
        playsound(this, Dm.resource("sound/machines/printer.ogg"), 100, TRUE);

        obj_item printable = null;
        object input_alias = (strip_html_full(@params["alias"], MAX_NAME_LEN) is var or__1 && Dm.truthy(or__1) ? or__1 : target.name);
        object input_description = (strip_html_full(@params["desc"], MAX_BROADCAST_LEN) is var or__2 && Dm.truthy(or__2) ? or__2 : I18n.T("No further details."));
        object input_header = (strip_html_full(@params["head"], 8) is var or__3 && Dm.truthy(or__3) ? or__3 : capitalize(@params["type"]));

        object sw__1 = @params["type"];
        if (Dm.eq(sw__1, "missing"))
        {
            obj_item_photo mugshot = target.get_front_photo() as obj_item_photo;
            obj_item_poster_wanted_missing missing_poster = Dm.@new<obj_item_poster_wanted_missing>(null, null, mugshot.picture.picture_image, input_alias, input_description, input_header);

            printable = missing_poster;
        }
        else if (Dm.eq(sw__1, "wanted"))
        {
            DmList crimes = target.crimes;
            if (!Dm.truthy(length(crimes)))
            {
                balloon_alert(user, I18n.T("no crimes"));
                return FALSE;
            }

            Dm.append(ref input_description, I18n.T("\n\n<b>WANTED FOR:</b>"));
            foreach (datum_crime incident in Dm.each<datum_crime>(crimes))
            {
                if (!Dm.truthy(incident.valid))
                {
                    Dm.append(ref input_description, I18n.T("<b>--REDACTED--</b>"));
                    continue;
                }
                Dm.append(ref input_description, I18n.T("\n<bCrime:</b> []\n", incident.name));
                Dm.append(ref input_description, I18n.T("<b>Details:</b> []\n", incident.details));
            }

            obj_item_photo mugshot = target.get_front_photo() as obj_item_photo;
            obj_item_poster_wanted wanted_poster = Dm.@new<obj_item_poster_wanted>(null, null, mugshot.picture.picture_image, input_alias, input_description, input_header);

            printable = wanted_poster;
        }
        else if (Dm.eq(sw__1, "rapsheet"))
        {
            DmList crimes = target.crimes;
            if (!Dm.truthy(length(crimes)))
            {
                balloon_alert(user, I18n.T("no crimes"));
                return FALSE;
            }

            obj_item_paper rapsheet = target.get_rapsheet(input_alias, Dm.str(input_header), Dm.str(input_description)) as obj_item_paper;
            printable = rapsheet;
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(print_finish), printable), 2 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/game/machinery/computer/records/security.dm", line: 387);

        return TRUE;
    }
}

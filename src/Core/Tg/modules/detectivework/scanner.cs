// Translated from tgstation (snapshot 1d99bd7f): code/modules/detectivework/scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298; the printed report's text on its paper came with the paperwork, gaps S3). The var blocks are generated.

// tg: code/modules/detectivework/scanner.dm:4
public partial class obj_item_detective_scanner : obj_item
{
    // tg: code/modules/detectivework/scanner.dm:23
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        await base.interact(user, __extra);
        if (IS_UNCONSCIOUS_OR_CRIT(user) || !Dm.truthy(user.can_read(this)) || Dm.truthy(user.is_blind()))
            return ITEM_INTERACT_BLOCKING;
        await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * safe_print_report - a wrapper proc for print_report
     *
     * Calls print_report(), and should a runtime occur within we can still reset the 'busy' state
     */
    // tg: code/modules/detectivework/scanner.dm:35
    public virtual async DmTask safe_print_report()
    {
        await print_report();
        scanner_busy = FALSE;
    }

    // tg: code/modules/detectivework/scanner.dm:39
    public virtual async DmTask print_report()
    {
        // Create our paper
        obj_item_paper report_paper = Dm.@new<obj_item_paper>(get_turf(this));

        //This could be a global count like sec and med record printouts. See GLOB.manifest.generalPrintCount AKA datacore.dm
        float frNum = ++forensicPrintCount;

        report_paper.name = Dm.text("FR-[] 'Forensic Record'", frNum);
        DmList report_text = Dm.list(Dm.text("<h1>Forensic Record - (FR-[])</h1><hr>", frNum));

        foreach (datum_detective_scanner_log log_entry in Dm.each<datum_detective_scanner_log>(log_data))
            Dm.append(ref report_text, log_entry.generate_report_text());

        Dm.append(ref report_text, "<h1>Notes:</h1><br>");

        report_paper.add_raw_text(report_text.Join(""));
        report_paper.update_appearance();

        if (ismob(loc))
        {
            mob printer = loc as mob;
            await printer.put_in_hands(report_paper);
            balloon_alert(printer, I18n.T("logs cleared"));
        }

        // Clear the logs
        log_data = Dm.list();
    }

    // tg: code/modules/detectivework/scanner.dm:66
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(user.combat_mode)) // SHOULD_SKIP_INTERACTION (__DEFINES/tools.dm:62), inline
            return NONE; // lets us put our scanner away without trying to scan the bag
        Waiting.Detach(safe_scan(user, interacting_with));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/detectivework/scanner.dm:72
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        Waiting.Detach(safe_scan(user, interacting_with));
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * safe_scan - a wrapper proc for scan()
     *
     * calls scan(), and should a runtime occur within we can still reset the 'busy' state
     */
    // tg: code/modules/detectivework/scanner.dm:81
    public virtual async DmTask safe_scan(mob user = null, atom atom_to_scan = null)
    {
        if (Dm.truthy(scanner_busy))
        {
            balloon_alert(user, I18n.T("scanner busy!"));
            return;
        }
        if (!Dm.truthy(await scan(user, atom_to_scan))) // this should only return FALSE if a runtime occurs during the scan proc, so ideally never
            balloon_alert(user, I18n.T("scanner error!")); // but in case it does, we 'error' instead of just bricking the scanner
        scanner_busy = FALSE;
    }

    /**
     * scan - scans an atom for forensic data and outputs it to the mob holding the scanner
     *
     * This should always return TRUE barring a runtime
     */
    // tg: code/modules/detectivework/scanner.dm:94
    public virtual async DmTask<object> scan(mob user = null, atom scanned_atom = null)
    {
        if (loc != user)
            return TRUE;
        // Can scan items we hold and store
        if (!Dm.@in(scanned_atom, user.get_all_contents()))
        {
            // Can remotely scan objects and mobs.
            if ((Dm.num(get_dist(scanned_atom, user)) > Dm.num(range)) || (!Dm.@in(scanned_atom, view(range, user)) && Dm.truthy(view_check)))
                return TRUE;
        }
        playsound(this, SFX_INDUSTRIAL_SCAN, 20, TRUE, -2, TRUE, FALSE);
        scanner_busy = TRUE;

        user.visible_message(span_notice(I18n.T("\\The [] points \\the [] at \\the [] and performs a forensic scan.", user, this, scanned_atom)), ignored_mobs: user);
        to_chat(user, span_notice(I18n.T("You scan \\the []. The scanner is now analysing the results...", scanned_atom)));

        // GATHER INFORMATION

        datum_detective_scanner_log log_entry = Dm.@new<datum_detective_scanner_log>();

        // Start gathering

        log_entry.scan_target = scanned_atom.name;
        log_entry.scan_time = round_timestamp();

        DmList atom_fibers = GET_ATOM_FIBRES(scanned_atom) as DmList;
        if (length(atom_fibers) > 0)
            log_entry.add_data_entry(DETSCAN_CATEGORY_FIBER, atom_fibers.Copy());

        DmList blood = GET_ATOM_BLOOD_DNA(scanned_atom) as DmList;
        if (length(blood) > 0)
            log_entry.add_data_entry(DETSCAN_CATEGORY_BLOOD, blood.Copy());

        if (ishuman(scanned_atom))
        {
            mob_living_carbon_human scanned_human = scanned_atom as mob_living_carbon_human;
            if (scanned_human.gloves == null)
            {
                log_entry.add_data_entry(DETSCAN_CATEGORY_FINGERS, md5(scanned_human.dna?.unique_identity));
            }
        }
        else if (!ismob(scanned_atom))
        {
            DmList atom_fingerprints = GET_ATOM_FINGERPRINTS(scanned_atom) as DmList;
            if (length(atom_fingerprints) > 0)
                log_entry.add_data_entry(DETSCAN_CATEGORY_FINGERS, atom_fingerprints.Copy());

            // Only get reagents from non-mobs.
            if (Dm.truthy(scanned_atom._listen_lookup?[COMSIG_ON_REAGENT_SCAN]))
                scanned_atom._SendSignal(COMSIG_ON_REAGENT_SCAN, Dm.list(scanned_atom, user));
            foreach (datum_reagent present_reagent in Dm.each<datum_reagent>((scanned_atom.reagents as datum_reagents)?.reagent_list))
            {
                log_entry.add_data_entry(DETSCAN_CATEGORY_REAGENTS, Dm.list(Dm.kv(present_reagent.name, present_reagent.volume)));

                // Get blood data from the blood reagent.
                if (!istype(present_reagent, typeof(datum_reagent_blood)))
                    continue;

                object blood_DNA = (present_reagent.data as DmList)?[BLOOD_DATA_DNA];
                object blood_type = (present_reagent.data as DmList)?[BLOOD_DATA_TYPE];
                if (!Dm.truthy(blood_DNA) || !Dm.truthy(blood_type))
                    continue;

                log_entry.add_data_entry(DETSCAN_CATEGORY_BLOOD, Dm.list(Dm.kv(blood_DNA, blood_type)));
            }
        }

        if (istype(scanned_atom, typeof(obj_item_card_id)))
        {
            obj_item_card_id user_id = scanned_atom as obj_item_card_id;
            foreach (object region in Dm.each(Dm.list(REGION_SECURITY, REGION_ENGINEERING, REGION_SUPPLY, REGION_MEDBAY, REGION_RESEARCH, REGION_COMMAND, REGION_GENERAL, REGION_CENTCOM)))
            {
                DmList access_in_region = Dm.bitand(SSid_access.accesses_by_region[region], user_id.GetAccess()) as DmList;
                if (length(access_in_region) == 0)
                    continue;
                DmList access_names = Dm.list();
                foreach (object access_num in Dm.each(access_in_region))
                    Dm.append(ref access_names, SSid_access.get_access_desc(access_num));

                log_entry.add_data_entry(DETSCAN_CATEGORY_ACCESS, Dm.list(Dm.kv(Dm.text("[]", region), english_list(access_names))));
            }
        }

        // sends it off to be modified by the items
        if (Dm.truthy(scanned_atom._listen_lookup?[COMSIG_DETECTIVE_SCANNED]))
            scanned_atom._SendSignal(COMSIG_DETECTIVE_SCANNED, Dm.list(scanned_atom, user, log_entry));

        // Perform sorting now, because probably this will be never modified
        log_entry.sort_data_entries();

        await stoplag(3 * SECONDS);
        Dm.append(ref log_data, log_entry);
        return TRUE;
    }

    // tg: code/modules/detectivework/scanner.dm:183
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        return clear_logs(user as mob_living);
    }

    // tg: code/modules/detectivework/scanner.dm:186
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (length(log_data) > 0 && !Dm.truthy(scanner_busy))
            dot.Add(span_notice(I18n.T("Alt-click to clear scanner logs.")));
        return dot;
    }

    // tg: code/modules/detectivework/scanner.dm:192
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ForensicScanner", I18n.T("Forensic Scanner"));
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/detectivework/scanner.dm:198
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList logs = Dm.list();
        foreach (datum_detective_scanner_log log in Dm.each<datum_detective_scanner_log>(log_data))
            logs.Add(Dm.list(await log.ui_data(user))); // UNTYPED_LIST_ADD

        DmList data = Dm.list();
        data["logs"] = logs;
        return data;
    }

    // tg: code/modules/detectivework/scanner.dm:207
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList categories = Dm.list();
        foreach (object key in Dm.each(GLOB.detective_scan_categories))
        {
            datum_detective_scan_category category = GLOB.detective_scan_categories[key] as datum_detective_scan_category;

            DmList category_data = Dm.list();
            category_data["name"] = category.name;
            category_data["uiIcon"] = category.ui_icon;
            category_data["uiIconColor"] = category.ui_icon_color;

            categories[category.id] = category_data;
        }

        DmList data = Dm.list();
        data["categories"] = categories;
        return data;
    }

    // tg: code/modules/detectivework/scanner.dm:223
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "clear":
                clear_logs(ui.user as mob_living);
                await ui.send_update();
                break;
            case "delete":
            {
                int index = (int)Dm.num(@params["index"]) + 1;
                if (index < 1 || index > length(log_data) || !Dm.truthy(log_data[index]))
                    return dot;
                if (Dm.truthy(scanner_busy))
                {
                    balloon_alert(ui.user, I18n.T("scanner busy!"));
                    return dot;
                }
                log_data.Cut(index, index + 1);
                balloon_alert(ui.user, I18n.T("log deleted"));
                await ui.send_update();
                break;
            }
            case "print":
                if (length(log_data) == 0)
                {
                    balloon_alert(ui.user, I18n.T("no logs!"));
                    return dot;
                }
                if (Dm.truthy(scanner_busy))
                {
                    balloon_alert(ui.user, I18n.T("scanner busy!"));
                    return dot;
                }
                scanner_busy = TRUE;
                playsound(this, Dm.resource("sound/machines/printer.ogg"), 50);
                balloon_alert(ui.user, I18n.T("printing report..."));
                _addtimer(Dm.@new<datum_callback>(this, nameof(safe_print_report)), 3 * SECONDS, file: "code/modules/detectivework/scanner.dm", line: 251);
                break;
        }
        return dot;
    }

    // tg: code/modules/detectivework/scanner.dm:253
    public virtual object clear_logs(mob_living user = null)
    {
        if (length(log_data) == 0)
        {
            balloon_alert(user, I18n.T("no logs!"));
            return CLICK_ACTION_BLOCKING;
        }
        if (Dm.truthy(scanner_busy))
        {
            balloon_alert(user, I18n.T("scanner busy!"));
            return CLICK_ACTION_BLOCKING;
        }
        balloon_alert(user, I18n.T("logs cleared"));
        log_data = Dm.list();
        return CLICK_ACTION_SUCCESS;
    }
}

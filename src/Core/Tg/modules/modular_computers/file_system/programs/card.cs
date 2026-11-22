// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/card.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4a, D327): Plexagon Access Management, the HoP's and the captain's ID card writer, with its NtosCard
// window: the two ID slots, logging in with the main card, the job templates, the access and wildcard toggles, renaming,
// demoting and the access report. The var block
// is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/card.dm:1
public partial class datum_computer_file_program_card_mod : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/card.dm:29
    public override void on_install(datum_computer_file source = null, obj_item_modular_computer computer_installing = null, mob user = null)
    {
        base.on_install(source, computer_installing, user);
        ADD_TRAIT(computer_installing, TRAIT_MODPC_TWO_ID_SLOTS, REF(this));
    }

    // tg: code/modules/modular_computers/file_system/programs/card.dm:33
    public override object Destroy(float? force = null)
    {
        if (computer != null)
            REMOVE_TRAIT(computer, TRAIT_MODPC_TWO_ID_SLOTS, REF(this));
        return base.Destroy(force);
    }

    /**
     * Authenticates the program based on the specific ID card.
     *
     * If the card has ACCESS_CHANGE_IDs, it authenticates with all options.
     * Otherwise, it authenticates depending on SSid_access.sub_department_managers_tgui
     * compared to the access on the supplied ID card.
     */
    // tg: code/modules/modular_computers/file_system/programs/card.dm:48
    public virtual async DmTask<object> authenticate(mob user = null, obj_item_card_id auth_card = null)
    {
        if (auth_card == null)
            return null;

        region_access.Cut();
        valid_access.Cut();
        job_templates.Cut();

        // If the program isn't locked to a specific department or is_centcom and we have ACCESS_CHANGE_IDS in our auth card, we're not minor.
        if ((!Dm.truthy(target_dept) || Dm.truthy(is_centcom)) && Dm.@in(ACCESS_CHANGE_IDS, auth_card.access))
        {
            minor = FALSE;
            authenticated_card = Dm.text("[]", auth_card.name);
            authenticated_user = Dm.truthy(auth_card.registered_name) ? auth_card.registered_name : "Unknown";
            job_templates = Dm.truthy(is_centcom) ? SSid_access.centcom_job_templates.Copy() : SSid_access.station_job_templates.Copy();
            valid_access = Dm.truthy(is_centcom) ? SSid_access.get_region_access_list(Dm.list(REGION_CENTCOM)) as DmList : SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList;
            await computer.update_static_data_for_all_viewers();
            return TRUE;
        }

        // Otherwise, we're minor and now we have to build a list of restricted departments we can change access for.
        DmList managers = SSid_access.sub_department_managers_tgui;
        foreach (object access_as_text in Dm.each(managers))
        {
            DmList info = managers[access_as_text] as DmList;
            object access = access_as_text;
            if (Dm.@in(access, auth_card.access) && (Dm.@in(target_dept, info["regions"]) || !Dm.truthy(target_dept)))
            {
                Dm.combine(ref region_access, info["regions"]);
                Dm.combine(ref job_templates, info["templates"]);
            }
        }

        if (Dm.truthy(length(region_access)))
        {
            minor = TRUE;
            Dm.combine(ref valid_access, SSid_access.get_region_access_list(region_access));
            authenticated_card = Dm.text("[] \\[LIMITED ACCESS\\]", auth_card.name);
            await computer.update_static_data_for_all_viewers();
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/modular_computers/file_system/programs/card.dm:84
    public override async DmTask<object> on_start(mob_living user = null)
    {
        object dot = await base.on_start(user);
        if (!Dm.truthy(dot))
            return FALSE;
        computer.crew_manifest_update = TRUE;
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/card.dm:90
    public override object kill_program(mob user = null)
    {
        computer.crew_manifest_update = FALSE;
        obj_item_card_id modified_id = computer.alt_stored_id;
        if (modified_id != null)
            GLOB.manifest.modify(modified_id.registered_name, modified_id.assignment, modified_id.get_trim_assignment());

        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/card.dm:98
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = usr;
        obj_item_card_id auth_card = computer.stored_id;
        obj_item_card_id modified_id = computer.alt_stored_id;

        switch (action)
        {
            // Log in.
            case "PRG_authenticate":
                if (computer == null || auth_card == null)
                {
                    playsound(computer, Dm.resource("sound/machines/terminal/terminal_prompt_deny.ogg"), 50, FALSE);
                    return TRUE;
                }
                if (Dm.truthy(await authenticate(user, auth_card)))
                {
                    playsound(computer, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 50, FALSE);
                    return TRUE;
                }
                break;
            // Log out.
            case "PRG_logout":
                authenticated_card = null;
                authenticated_user = null;
                playsound(computer, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, FALSE);
                return TRUE;
            // Print a report.
            case "PRG_print":
            {
                if (computer == null || !Dm.truthy(authenticated_card) || modified_id == null)
                    return TRUE;
                string contents = Dm.text("<h4>Access Report</h4>\n\t\t\t\t\t\t<u>Prepared By:</u> []<br>\n\t\t\t\t\t\t<u>For:</u> []<br>\n\t\t\t\t\t\t<hr>\n\t\t\t\t\t\t<u>Assignment:</u> []<br>\n\t\t\t\t\t\t<u>Access:</u><br>\n\t\t\t\t\t\t",
                    authenticated_user, Dm.truthy(modified_id.registered_name) ? modified_id.registered_name : "Unregistered", modified_id.assignment);

                DmList known_access_rights = SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList;
                foreach (object A in Dm.each(modified_id.access))
                {
                    if (Dm.@in(A, known_access_rights))
                        contents += Dm.text(" []", SSid_access.get_access_desc(A));
                }

                if (!Dm.truthy(computer.print_text(contents, Dm.text("access report - []", Dm.truthy(modified_id.registered_name) ? modified_id.registered_name : "Unregistered"))))
                {
                    to_chat(usr, span_notice(I18n.T("Printer is out of paper.")));
                    return TRUE;
                }
                else
                {
                    playsound(computer, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 50, FALSE);
                    computer.visible_message(span_notice(I18n.T("\\The [] prints out a paper.", computer)));
                }
                return TRUE;
            }
            // Used to fire someone. Wipes all access from their card and modifies their assignment.
            case "PRG_terminate":
                if (computer == null || !Dm.truthy(authenticated_card))
                    return TRUE;
                if (Dm.truthy(minor))
                {
                    if (!Dm.@in(modified_id.trim?.GetType(), job_templates))
                    {
                        to_chat(usr, span_notice(I18n.T("Software error: You do not have the necessary permissions to demote this card.")));
                        return TRUE;
                    }
                }

                // Set the new assignment then remove the trim.
                modified_id.assignment = Dm.truthy(is_centcom) ? "Fired" : "Demoted";
                await SSid_access.remove_trim_from_card(modified_id);

                playsound(computer, Dm.resource("sound/machines/terminal/terminal_prompt_deny.ogg"), 50, FALSE);
                return TRUE;
            // Change ID card assigned name.
            case "PRG_edit":
            {
                if (computer == null || !Dm.truthy(authenticated_card) || modified_id == null)
                    return TRUE;

                object old_name = modified_id.registered_name;

                // Sanitize the name first. We're not using the full sanitize_name proc as ID cards can have a wider variety of things on them that
                // would not pass as a formal character name, but would still be valid on an ID card created by a player.
                object new_name = sanitize(@params["name"]);

                if (!Dm.truthy(new_name))
                {
                    modified_id.registered_name = null;
                    playsound(computer, SFX_TERMINAL_TYPE, 50, FALSE);
                    modified_id.update_label();
                    // We had a name before and now we have no name, so this will unassign the card and we update the icon.
                    if (Dm.truthy(old_name))
                        await modified_id.update_icon();
                    return TRUE;
                }

                // However, we are going to reject bad names overall including names with invalid characters in them, while allowing numbers.
                new_name = reject_bad_name(new_name, allow_numbers: TRUE);

                if (!Dm.truthy(new_name))
                {
                    to_chat(usr, span_notice(I18n.T("Software error: The ID card rejected the new name as it contains prohibited characters.")));
                    return TRUE;
                }

                modified_id.registered_name = new_name as string;
                playsound(computer, SFX_TERMINAL_TYPE, 50, FALSE);
                modified_id.update_label();
                // Card wasn't assigned before and now it is, so update the icon accordingly.
                if (!Dm.truthy(old_name))
                    await modified_id.update_icon();
                return TRUE;
            }
            // Change age
            case "PRG_age":
            {
                if (computer == null || !Dm.truthy(authenticated_card) || modified_id == null)
                    return TRUE;

                object new_age = @params["id_age"];
                if (!Dm.isnum(new_age))
                {
                    stack_trace(Dm.text("[] ([]) attempted to set invalid age \\[[]\\] to []", key_name(usr), usr, new_age, modified_id), "code/modules/modular_computers/file_system/programs/card.dm", 199);
                    return TRUE;
                }

                modified_id.registered_age = Dm.num(new_age);
                playsound(computer, SFX_TERMINAL_TYPE, 50, FALSE);
                return TRUE;
            }
            // Change assignment
            case "PRG_assign":
            {
                if (computer == null || !Dm.truthy(authenticated_card) || modified_id == null)
                    return TRUE;
                object new_asignment = trim(sanitize(@params["assignment"]), MAX_NAME_LEN);
                modified_id.assignment = new_asignment as string;
                playsound(computer, SFX_TERMINAL_TYPE, 50, FALSE);
                modified_id.update_label();
                return TRUE;
            }
            // Add/remove access.
            case "PRG_access":
            {
                if (computer == null || !Dm.truthy(authenticated_card) || modified_id == null)
                    return TRUE;
                playsound(computer, SFX_TERMINAL_TYPE, 50, FALSE);
                object access_type = @params["access_target"];
                object try_wildcard = @params["access_wildcard"];
                if (!Dm.@in(access_type, valid_access))
                {
                    stack_trace(Dm.text("[] ([]) attempted to add invalid access \\[[]\\] to []", key_name(usr), usr, access_type, modified_id), "code/modules/modular_computers/file_system/programs/card.dm", 222);
                    return TRUE;
                }

                if (Dm.@in(access_type, modified_id.access))
                {
                    modified_id.remove_access(Dm.list(access_type));
                    LOG_ID_ACCESS_CHANGE(user, modified_id, Dm.text("removed []", SSid_access.get_access_desc(access_type)));
                    return TRUE;
                }

                if (!Dm.truthy(modified_id.add_access(Dm.list(access_type), try_wildcard)))
                {
                    to_chat(usr, span_notice(I18n.T("ID error: ID card rejected your attempted access modification.")));
                    LOG_ID_ACCESS_CHANGE(user, modified_id, Dm.text("failed to add [][]", SSid_access.get_access_desc(access_type), Dm.truthy(try_wildcard) ? Dm.text(" with wildcard []", try_wildcard) : ""));
                    return TRUE;
                }

                if (Dm.@in(access_type, Dm.list(ACCESS_CHANGE_IDS))) // ACCESS_ALERT_ADMINS (__DEFINES/access.dm:213), inline
                    message_admins(Dm.text("[] just added [] to an ID card [] []", ADMIN_LOOKUPFLW(user), SSid_access.get_access_desc(access_type), ADMIN_VV(modified_id), Dm.truthy(modified_id.registered_name) ? Dm.text("belonging to [].", modified_id.registered_name) : "with no registered name."));
                LOG_ID_ACCESS_CHANGE(user, modified_id, Dm.text("added []", SSid_access.get_access_desc(access_type)));
                return TRUE;
            }
            // Apply template to ID card.
            case "PRG_template":
            {
                if (computer == null || !Dm.truthy(authenticated_card) || modified_id == null)
                    return TRUE;

                playsound(computer, SFX_TERMINAL_TYPE, 50, FALSE);
                object template_name = @params["name"];

                if (!Dm.truthy(template_name))
                    return TRUE;

                foreach (object trim_path in Dm.each(job_templates))
                {
                    datum_id_trim trim = SSid_access.trim_singletons_by_path[trim_path] as datum_id_trim;
                    if (!Dm.eq(trim.assignment, template_name))
                        continue;

                    SSid_access.add_trim_access_to_card(modified_id, trim_path);
                    return TRUE;
                }

                stack_trace(Dm.text("[] ([]) attempted to apply invalid template \\[[]\\] to []", key_name(usr), usr, template_name, modified_id), "code/modules/modular_computers/file_system/programs/card.dm", 258);

                return TRUE;
            }
            case "PRG_insert_main_id":
            {
                obj_item_card_id main_id = user.get_active_held_item() as obj_item_card_id;
                if (!Dm.truthy(isidcard(main_id)))
                    return TRUE;
                await computer.insert_id(main_id, user);
                return TRUE;
            }
            case "PRG_remove_main_id":
                if (computer?.stored_id != null)
                    await computer.remove_id(user);
                return TRUE;
            case "PRG_insert_alt_id":
            {
                obj_item_card_id alt_id = user.get_active_held_item() as obj_item_card_id;
                if (!Dm.truthy(isidcard(alt_id)))
                {
                    to_chat(user, span_notice(I18n.T("You must hold an ID card to insert it into the secondary slot.")));
                    return TRUE;
                }
                await computer.insert_secondary_id(alt_id, user);
                return TRUE;
            }
            case "PRG_remove_alt_id":
                if (computer?.alt_stored_id != null)
                    computer.remove_secondary_id(user);
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/card.dm:283
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList regions = Dm.list();
        DmList tgui_region_data = SSid_access.all_region_access_tgui;
        if (Dm.truthy(is_centcom))
            regions.Add(tgui_region_data[REGION_CENTCOM]);
        else
        {
            foreach (object region in Dm.each(SSid_access.station_regions))
            {
                if ((Dm.truthy(minor) || Dm.truthy(target_dept)) && !Dm.@in(region, region_access))
                    continue;
                regions.Add(tgui_region_data[region]);
            }
        }

        data["regions"] = regions;

        data["access_flags"] = SSid_access.flags_by_access;
        data["wildcard_flags"] = SSid_access.wildcard_flags_by_wildcard;
        data["access_flag_names"] = SSid_access.access_flag_string_by_flag;
        data["show_basic"] = TRUE;
        data["templates"] = job_templates;

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/card.dm:306
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["authed_user"] = authenticated_card;

        obj_item_card_id auth_id = computer.stored_id;
        obj_item_card_id modified_id = computer.alt_stored_id;

        data["auth_card"] = auth_id != null ? get_id_ui_data(auth_id) : null;
        data["modified_card"] = modified_id != null ? get_id_ui_data(modified_id) : null;
        data["is_holding_id"] = isidcard(user.get_active_held_item());

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/card.dm:320
    public virtual DmList get_id_ui_data(obj_item_card_id card = null)
    {
        DmList data = Dm.list();

        datum_id_trim card_trim = card.trim as datum_id_trim;

        data["id_name"] = card.name;
        data["id_rank"] = Dm.truthy(card.assignment) ? card.assignment : "Unassigned";
        data["id_owner"] = Dm.truthy(card.registered_name) ? card.registered_name : "-----";
        data["access_on_card"] = card.access ?? Dm.list();
        data["wildcard_slots"] = card.wildcard_slots ?? Dm.list();
        data["id_age"] = card.registered_age;

        data["has_trim"] = card_trim != null;
        data["trim_assignment"] = Dm.truthy(card_trim?.assignment) ? card_trim.assignment : "";
        data["trim_access"] = card_trim?.access ?? Dm.list();

        return data;
    }
}

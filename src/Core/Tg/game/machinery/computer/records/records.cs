// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/records/records.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps SC2, D333): the records console's base, shared by the medical and security records consoles: the login with
// an ID, the character preview of the selected record (a char_preview map view whose appearance is the record's, drawn by the
// client's MapPicture), editing a record's fields, expunging one record or purging all of them, and a new record made from a photo.
// The var block is generated (D111).

/**
 * Records subtype for the shared functionality between medical/security/warrant consoles.
 */
// tg: code/game/machinery/computer/records/records.dm:4
public partial class obj_machinery_computer_records : obj_machinery_computer
{
    // tg: code/game/machinery/computer/records/records.dm:8
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        // || isAdminGhostAI(user): no admin ghosts yet (GAP(lead), is_helpers.cs)
        float has_access = Dm.num(Dm.truthy(authenticated) && isliving(user));
        data["authenticated"] = has_access;
        if (!Dm.truthy(has_access))
            return data;

        data["assigned_view"] = Dm.text("preview_[]_[]_records", user.ckey, REF(this)); // USER_PREVIEW_ASSIGNED_VIEW(user.ckey) (__DEFINES/computers.dm:16, inline: it reads src)
        data["station_z"] = Dm.num(Dm.truthy(z) && Dm.truthy(is_station_level(z)));

        return data;
    }

    // tg: code/game/machinery/computer/records/records.dm:21
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        if (user.client != null)
            Dm.remove(ref user.client.screen_maps, Dm.text("preview_[]_[]_records", user.ckey, REF(this))); // user.client?.screen_maps -= USER_PREVIEW_ASSIGNED_VIEW(user.ckey)
        if ((LAZYLEN(open_uis) <= 1) && character_preview_view != null) //only delete the preview if we're the last one to close the console.
            QDEL_NULL(ref character_preview_view);
    }

    // tg: code/game/machinery/computer/records/records.dm:27
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob user = ui.user;

        datum_record_crew target = null;
        if (Dm.truthy(@params["crew_ref"]))
            target = Dm.locate_ref(@params["crew_ref"]) is datum_record_crew crew__1 && Dm.@in(crew__1, GLOB.manifest.general) ? crew__1 : null; // locate(params["crew_ref"]) in GLOB.manifest.general

        object sw__1 = action;
        if (Dm.eq(sw__1, "edit_field"))
        {
            target = Dm.locate_ref(@params["ref"]) is datum_record_crew crew__2 && Dm.@in(crew__2, GLOB.manifest.general) ? crew__2 : null; // locate(params["ref"]) in GLOB.manifest.general
            object field = @params["field"];
            // !(field in target?.vars) (rules 10.7: a var name; a value that is no text names no var)
            if (!Dm.truthy(field) || !(target != null && field is string field_name && Dm.has_var(target, field_name)))
                return FALSE;

            object value = reject_bad_name(@params["value"], allow_numbers: TRUE, max_length: MAX_BROADCAST_LEN, strict: TRUE, cap_after_symbols: FALSE);
            if (!Dm.truthy(value))
                value = "Unknown";
            investigate_log(Dm.text("[] changed the field: \"[]\" with value: \"[]\" to new value: \"[]\"", key_name(user), field, Dm.get_var(target, (string)field), value), INVESTIGATE_RECORDS);
            Dm.set_var(target, (string)field, value); // target.vars[field] = value (rules 10.7)

            return TRUE;
        }
        else if (Dm.eq(sw__1, "expunge_record"))
        {
            if (target == null)
                return FALSE;
            // Don't let people off station futz with the station network.
            if (!Dm.truthy(is_station_level(z)))
            {
                balloon_alert(user, I18n.T("out of range!"));
                return TRUE;
            }

            expunge_record_info(target);
            balloon_alert(user, I18n.T("record expunged"));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_eject.ogg"), 70, TRUE);
            investigate_log(Dm.text("[] expunged the record of [].", key_name(user), target.name), INVESTIGATE_RECORDS);

            return TRUE;
        }
        else if (Dm.eq(sw__1, "login"))
        {
            authenticated = Dm.num(await secure_login(user));
            investigate_log(Dm.text("[] [] into the [].", key_name(user), Dm.truthy(authenticated) ? "successfully logged" : "failed to log", this), INVESTIGATE_RECORDS);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "logout"))
        {
            balloon_alert(user, I18n.T("logged out"));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 70, TRUE);
            authenticated = FALSE;

            return TRUE;
        }
        else if (Dm.eq(sw__1, "purge_records"))
        {
            // Don't let people off station futz with the station network.
            if (!Dm.truthy(is_station_level(z)))
            {
                balloon_alert(user, I18n.T("out of range!"));
                return TRUE;
            }

            ui.close();
            balloon_alert(user, I18n.T("purging records..."));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 70, TRUE);

            if (Dm.truthy(await do_after(user, 5 * SECONDS)))
            {
                foreach (datum_record_crew entry in Dm.each<datum_record_crew>(GLOB.manifest.general))
                    expunge_record_info(entry);

                balloon_alert(user, I18n.T("records purged"));
                playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 70, TRUE);
                investigate_log(Dm.text("[] purged all records.", key_name(user)), INVESTIGATE_RECORDS);
            }
            else
                balloon_alert(user, I18n.T("interrupted!"));

            return TRUE;
        }
        else if (Dm.eq(sw__1, "view_record"))
        {
            if (target == null)
                return FALSE;

            await update_preview(user, @params["assigned_view"], target, ui.window);
            return TRUE;
        }

        return FALSE;
    }

    /// Creates a character preview view for the UI.
    // tg: code/game/machinery/computer/records/records.dm:109
    public virtual async DmTask<object> create_character_preview_view(mob user = null, datum_tgui_window window = null)
    {
        string assigned_view = Dm.text("preview_[]_[]_records", user.ckey, REF(this)); // USER_PREVIEW_ASSIGNED_VIEW(user.ckey)
        if (Dm.truthy(user.client?.screen_maps[assigned_view]))
            return null;

        // new(null, null, src): the console rides in char_preview's preferences argument, which its typed Initialize parameter does
        // not keep (the preferences' own preview reads it; the records' preview never does)
        atom_movable_screen_map_view_char_preview new_view = Dm.@new<atom_movable_screen_map_view_char_preview>(null, null, this);
        new_view.generate_view(assigned_view);
        new_view.display_to(user, window);
        return new_view;
    }

    /// Takes a record and updates the character preview view to match it.
    // tg: code/game/machinery/computer/records/records.dm:120
    public virtual async DmTask update_preview(mob user = null, object assigned_view = null, datum_record_crew target = null, datum_tgui_window window = null)
    {
        mutable_appearance preview = Dm.@new<mutable_appearance>(target.character_appearance);
        Dm.append(ref preview.underlays, mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "static_base", alpha: 20));
        // preview.add_overlay(...): /image/proc/add_overlay(x) is `overlays |= x` (controllers/subsystem/overlays.dm:113)
        Dm.combine(ref preview.overlays, mutable_appearance(generate_icon_alpha_mask(Dm.resource("icons/effects/effects.dmi"), "scanline"), alpha: 20));

        atom_movable_screen_map_view_char_preview old_view = (user.client?.screen_maps[assigned_view] as DmList)?[1] as atom_movable_screen_map_view_char_preview;
        if (old_view == null)
        {
            character_preview_view = await create_character_preview_view(user, window) as atom_movable_screen_map_view_char_preview;
            return;
        }

        old_view.appearance = preview.appearance;
    }

    /// Expunges info from a record.
    // tg: code/game/machinery/computer/records/records.dm:133
    public virtual object expunge_record_info(datum_record_crew target = null)
    {
        return null;
    }

    /// Inserts a new record into GLOB.manifest.general. Requires a photo to be taken.
    // tg: code/game/machinery/computer/records/records.dm:137
    public virtual async DmTask<object> insert_new_record(mob user = null, obj_item_photo mugshot = null)
    {
        if (mugshot == null || !Dm.truthy(is_operational) || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
            return FALSE;

        if (!Dm.truthy(authenticated) && !Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, I18n.T("access denied"));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 70, TRUE);
            return FALSE;
        }

        if (Dm.num(mugshot.picture.psize_x) > ICON_SIZE_X || Dm.num(mugshot.picture.psize_y) > ICON_SIZE_Y)
        {
            balloon_alert(user, I18n.T("photo too large!"));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 70, TRUE);
            return FALSE;
        }

        string trimmed = copytext(mugshot.name, 9, MAX_NAME_LEN); // Remove "photo - "
        object name = await tgui_input_text(user, I18n.T("Enter the name of the new record."), I18n.T("New Record"), trimmed, max_length: MAX_NAME_LEN);
        if (!Dm.truthy(name) || !Dm.truthy(is_operational) || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)) || mugshot == null || QDELETED(mugshot) || QDELETED(this))
            return FALSE;

        Dm.@new<datum_record_crew>(Dm.named("name", name), Dm.named("character_appearance", mugshot.picture.picture_image));

        balloon_alert(user, I18n.T("record created"));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 70, TRUE);

        qdel(mugshot);

        return TRUE;
    }

    /// Secure login
    // tg: code/game/machinery/computer/records/records.dm:166
    public virtual async DmTask<object> secure_login(mob user = null)
    {
        if (!Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)) || !Dm.truthy(is_operational))
            return FALSE;

        if (!Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, I18n.T("access denied"));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 70, TRUE);
            return FALSE;
        }

        balloon_alert(user, I18n.T("logged in"));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 70, TRUE);

        return TRUE;
    }
}

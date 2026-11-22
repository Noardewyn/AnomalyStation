// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/computer_ui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: NtOS's window): the computer's tgui window, which shows NtOS's main screen ("NtosMain") or the active
// program's interface, and its PC_ actions. Left out as marked leaves: the flashlight's colour (PC_light_color: tgui_color_picker,
// the colour input, with the spray cans and paint; D261 keeps it out of M9), the removable media's disk and intelliCard (the
// computer disks, the AI) and the pAI's interact and eject (pAIs). The header images (ui_assets: /datum/asset/simple/headers) are
// the client's (DV44).

// tg: code/modules/modular_computers/computers/item/computer_ui.dm:1 (interleaved types, rules 1.9)
public partial class obj_item_modular_computer : obj_item
{
    /**
     * update_tablet_open_uis
     *
     * Will search the user to see if they have the tablet open.
     * If they don't, we'll open a new UI depending on the tab the tablet is meant to be on.
     * If they do, we'll update the interface and title, then update all static data and re-send assets.
     *
     * This is best called when you're actually changing the app, as we don't check
     * if we're swapping to the current UI repeatedly.
     * Args:
     * user - The person whose UI we're updating. Only necessary if we're opening the UI for the first time.
     */
    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:13
    public virtual async DmTask<object> update_tablet_open_uis(mob user = null)
    {
        if (user != null)
        {
            datum_tgui active_ui = SStgui.get_open_ui(user, this) as datum_tgui;
            if (active_ui == null)
            {
                if (active_program != null)
                {
                    active_ui = Dm.@new<datum_tgui>(user, this, active_program.tgui_id, active_program.filedesc);
                    await active_program.ui_interact(user, active_ui);
                }
                else
                    active_ui = Dm.@new<datum_tgui>(user, this, "NtosMain");
                return await active_ui.open();
            }
        }

        foreach (datum_tgui window in Dm.each<datum_tgui>(open_uis?.Copy()))
        {
            if (active_program != null)
            {
                window.@interface = active_program.tgui_id;
                window.title = active_program.filedesc;
                await active_program.ui_interact(window.user, window);
            }
            else
                window.@interface = "NtosMain";
            window.send_assets();
        }
        await update_static_data_for_all_viewers();
        return null;
    }

    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:35
    public override object ui_state(mob user = null)
    {
        if (inserted_pai != null && user == inserted_pai.pai)
            return GLOB.contained_state;
        return base.ui_state(user);
    }

    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:40
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(enabled))
            await ui_interact(user);
        else
            await turn_on(user);
        return null;
    }

    // Operates TGUI
    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:47
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!Dm.truthy(enabled) || !Dm.truthy(user.can_read(this, READING_CHECK_LITERACY)))
        {
            ui?.close();
            return null;
        }

        // Robots don't really need to see the screen, their wireless connection works as long as computer is on.
        if (!Dm.truthy(screen_on) && !issilicon(user))
        {
            ui?.close();
            return null;
        }

        if (honkvirus_amount > 0) // EXTRA annoying, huh!
        {
            honkvirus_amount--;
            playsound(this, Dm.resource("sound/items/bikehorn.ogg"), 30, TRUE);
        }

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
            await update_tablet_open_uis(user);
        else if (Dm.truthy(active_program?.always_update_ui))
            await active_program.ui_interact(user, ui);
        return null;
    }

    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:67
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // data += get_asset_datum(/datum/asset/simple/headers): the asset transport is the client's (DV44); the client draws the
        // headers (the battery, the signal and the programs' header icons) from its own copy of tg's images
        DmList data = Dm.list();
        if (active_program != null)
            Dm.append(ref data, await active_program.ui_assets(user));
        return data;
    }

    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:74
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        if (active_program != null)
        {
            Dm.append(ref data, await active_program.ui_static_data(user));
            return data;
        }

        data["show_imprint"] = this is obj_item_modular_computer_pda ? TRUE : FALSE;
        return data;
    }

    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:83
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = get_header_data();
        if (active_program != null)
        {
            Dm.append(ref data, await active_program.ui_data(user));
            return data;
        }

        data["pai"] = inserted_pai;
        data["has_light"] = has_light;
        data["light_on"] = light_on;
        data["comp_light_color"] = comp_light_color;

        data["login"] = Dm.list(
            Dm.kv("IDName", Dm.truthy(saved_identification) ? saved_identification : "Unknown"),
            Dm.kv("IDJob", Dm.truthy(saved_job) ? saved_job : "Unknown"));

        data["proposed_login"] = Dm.list(
            Dm.kv("IDInserted", stored_id != null ? TRUE : FALSE),
            Dm.kv("IDName", stored_id?.registered_name),
            Dm.kv("IDJob", stored_id?.assignment));

        data["removable_media"] = Dm.list();
        if (inserted_disk is obj_item)
            data["removable_media"] = Dm.add(data["removable_media"], "Eject Disk");
        // the AI restorer's intelliCard (:105-107): the AI restorer (the AI, a later milestone) holds no card

        data["programs"] = Dm.list();
        foreach (datum_computer_file_program program in Dm.each<datum_computer_file_program>(stored_files))
        {
            data["programs"] = Dm.add(data["programs"], Dm.list(Dm.list(
                Dm.kv("name", program.filename),
                Dm.kv("desc", program.filedesc),
                Dm.kv("header_program", Dm.truthy(Dm.bitand(program.program_flags, PROGRAM_HEADER)) ? TRUE : FALSE),
                Dm.kv("running", Dm.@in(program, idle_threads) ? TRUE : FALSE),
                Dm.kv("icon", program.program_icon),
                Dm.kv("alert", program.alert_pending))));
        }

        data["alert_style"] = get_security_level_relevancy();
        // SSsecurity_level?.current_security_level?.announcement_color and name_shortform: the security levels (SSsecurity_level, M10)
        data["alert_color"] = null;
        data["alert_name"] = null;

        return data;
    }

    // Handles user's GUI input
    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:130
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (usr is mob_living_carbon_human human_user && !Dm.truthy(allow_chunky))
        {
            if (Dm.truthy(human_user.check_chunky_fingers()))
            {
                balloon_alert(human_user, I18n.T("fingers are too big!"));
                return TRUE;
            }
        }

        if (Dm.eq(action, "PC_exit"))
        {
            //you can't close apps in emergency mode.
            if (cell == null || Dm.truthy(cell.charge))
                active_program.kill_program(usr);
            return TRUE;
        }
        if (Dm.eq(action, "PC_shutdown"))
        {
            shutdown_computer();
            return TRUE;
        }
        if (Dm.eq(action, "PC_minimize"))
        {
            if (active_program == null || (cell != null && !Dm.truthy(cell.charge)))
                return null;
            active_program.background_program(usr);
            return TRUE;
        }

        if (Dm.eq(action, "PC_killprogram"))
        {
            object prog = @params["name"];
            if (!(find_file_by_name(prog) is datum_computer_file_program killed_program))
                return null;

            killed_program.kill_program(usr);
            to_chat(usr, span_notice(I18n.T("Program [].[] with PID [] has been killed.", killed_program.filename, killed_program.filetype, Dm.rand(100, 999))));
            return TRUE;
        }

        if (Dm.eq(action, "PC_runprogram"))
        {
            await open_program(usr, find_file_by_name(@params["name"]) as datum_computer_file_program);
            return TRUE;
        }

        if (Dm.eq(action, "PC_toggle_light"))
        {
            toggle_flashlight();
            return TRUE;
        }

        if (Dm.eq(action, "PC_light_color"))
        {
            // tgui_color_picker(user, "Choose a new color for [src]'s flashlight.", ...), rejecting colours too dark (is_color_dark), then
            // set_flashlight_color: the colour input (D261: with the spray cans and paint)
            Dm.NotTranslated("code/modules/modular_computers/computers/item/computer_ui.dm:172", "ui_act: PC_light_color, the flashlight's colour picker (tgui_color_picker, the colour input, D261)");
            return TRUE;
        }

        if (Dm.eq(action, "PC_Eject_Disk"))
        {
            object param = @params["name"];
            mob user = usr;
            if (Dm.eq(param, "Eject Disk"))
            {
                if (!(inserted_disk is obj_item disk))
                    return null;

                if (user == null || !Dm.truthy(await Adjacent(user)))
                    await disk.forceMove(drop_location() as atom);
                else
                    await user.put_in_hands(disk);
                inserted_disk = null;
                playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50);
                return TRUE;
            }
            if (Dm.eq(param, "intelliCard"))
            {
                // the AI restorer's try_eject (the AI, a later milestone): no translated app holds an intelliCard
                return null;
            }
            if (Dm.eq(param, "ID"))
            {
                if (Dm.truthy(await remove_id(user)))
                {
                    playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50);
                    return TRUE;
                }
            }
        }

        if (Dm.eq(action, "PC_Imprint_ID"))
        {
            imprint_id();
            UpdateDisplay();
            playsound(this, Dm.resource("sound/machines/terminal/terminal_processing.ogg"), 15, TRUE);
        }

        if (Dm.eq(action, "PC_Pai_Interact"))
        {
            // remove_pai(usr) and inserted_pai.attack_self(usr): the pAI card (pAIs, a later milestone)
            if (Dm.eq(@params["option"], "eject"))
            {
                if (!ishuman(usr))
                    return null;
                remove_pai(usr);
            }
            else if (Dm.eq(@params["option"], "interact"))
                await inserted_pai.attack_self(usr);
            return TRUE;
        }

        if (active_program != null)
            return await active_program.ui_act(action, @params, ui, state);
        return null;
    }

    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:236
    public override object ui_host(mob user = null)
    {
        if (physical != null)
            return physical;
        return this;
    }

    // tg: code/modules/modular_computers/computers/item/computer_ui.dm:241
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        active_program?.ui_close(user);
    }
}

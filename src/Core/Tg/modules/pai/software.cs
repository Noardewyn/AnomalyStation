// Translated from tgstation (snapshot 1d99bd7f): code/modules/pai/software.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole file.

public partial class mob_living_silicon_pai : mob_living_silicon
{
    // tg: code/modules/pai/software.dm:1
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "PaiInterface", name);
            await ui.open();
            ui.set_autoupdate(FALSE);
        }
        return null;
    }

    // tg: code/modules/pai/software.dm:8
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["door_jack"] = hacking_cable;
        data["screen_image_interface_icon"] = Dm.initial(card.screen_image, (datum_pai_screen_image s) => s.interface_icon);
        data["installed"] = installed_software;
        data["ram"] = ram;
        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/modules/pai/software.dm:16
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["available"] = available_software;
        data["directives"] = laws.inherent;
        data["emagged"] = emagged;
        data["languages"] = languages_granted;
        data["master_name"] = master_name;
        data["master_dna"] = master_dna;
        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/modules/pai/software.dm:26
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;
        if (Dm.eq(action, "buy"))
        {
            buy_software(@params["selection"]);
            return TRUE;
        }
        if (Dm.eq(action, "change image"))
        {
            await change_image();
            return TRUE;
        }
        if (Dm.eq(action, "check dna"))
        {
            await check_dna();
            return TRUE;
        }
        // Software related ui actions
        if (Dm.truthy(available_software[action]) && !Dm.truthy(installed_software.Find(action)))
        {
            balloon_alert(ui.user, "software unavailable!");
            return FALSE;
        }
        switch (Dm.str(action))
        {
            case "Atmospheric Sensor":
                await atmos_analyzer.attack_self(this);
                return TRUE;
            case "Crew Manifest":
                await ai_roster();
                return TRUE;
            case "Crew Monitor":
                await GLOB.crewmonitor.show(usr, this);
                return TRUE;
            case "Digital Messenger":
                if (modularInterface != null)
                    await modularInterface.interact(usr);
                return TRUE;
            case "Door Jack":
                // Look to door_jack.dm for implementation
                await door_jack(@params["mode"]);
                return TRUE;
            case "Encryption Slot":
                balloon_alert(usr, Dm.text("radio frequencies []", !Dm.truthy(encrypt_mod) ? "enabled" : "disabled"));
                encrypt_mod = !Dm.truthy(encrypt_mod) ? TRUE : FALSE;
                ((obj_item_radio)radio).subspace_transmission = !Dm.truthy(((obj_item_radio)radio).subspace_transmission) ? TRUE : FALSE;
                return TRUE;
            case "Host Scan":
                await host_scan(@params["mode"]);
                return TRUE;
            case "Internal GPS":
                await internal_gps.attack_self(this);
                return TRUE;
            case "Music Synthesizer":
                await instrument.interact(this);
                return TRUE;
            case "Medical HUD":
                toggle_hud(PAI_TOGGLE_MEDICAL_HUD);
                return TRUE;
            case "Newscaster":
                await newscaster.ui_interact(this);
                return TRUE;
            case "Photography Module":
                // Look to pai_camera.dm for implementation
                await use_camera(usr, @params["mode"]);
                return TRUE;
            case "Remote Signaler":
                await signaler.ui_interact(this);
                return TRUE;
            case "Security HUD":
                toggle_hud(PAI_TOGGLE_SECURITY_HUD);
                return TRUE;
            case "Universal Translator":
                grant_languages();
                await ui.send_full_update();
                return TRUE;
        }
        return FALSE;
    }

    /**
     * Purchases the selected software from the list and deducts their
     * available ram.
     *
     * @param {string} selection - The software to purchase.
     *
     * @returns {boolean} - TRUE if the software was purchased, FALSE otherwise.
     */
    // tg: code/modules/pai/software.dm:100
    public virtual object buy_software(object selection = null)
    {
        if (!Dm.truthy(available_software[selection]) || Dm.truthy(installed_software.Find(selection)))
            return FALSE;
        float cost = Dm.num(available_software[selection]);
        if (ram < cost)
            return FALSE;
        installed_software.Add(selection);
        ram -= cost;
        var pAIhud = hud_used as datum_hud_pai;
        pAIhud?.update_software_buttons();
        switch (Dm.str(selection))
        {
            case "Atmospheric Sensor":
                atmos_analyzer = Dm.@new<obj_item_analyzer>(this);
                break;
            case "Digital Messenger":
                create_modularInterface();
                break;
            case "Internal GPS":
                internal_gps = Dm.@new<obj_item_gps_pai>(this);
                break;
            case "Music Synthesizer":
                instrument = Dm.@new<obj_item_instrument_piano_synth>(this);
                break;
            case "Newscaster":
                newscaster = Dm.@new<obj_machinery_newscaster_pai>(this);
                break;
            case "Photography Module":
                aicamera = Dm.@new<obj_item_camera_siliconcam_pai_camera>(this);
                break;
            case "Remote Signaler":
                signaler = Dm.@new<obj_item_assembly_signaler_internal>(this);
                break;
        }
        return TRUE;
    }

    /**
     * Changes the image displayed on the pAI.
     *
     * @returns {boolean} - TRUE if the image was changed, FALSE otherwise.
     */
    // tg: code/modules/pai/software.dm:134
    public virtual async DmTask<object> change_image()
    {
        DmList possible_choices = Dm.list();
        foreach (Type screen_option in Dm.each_cast<Type>(subtypesof(typeof(datum_pai_screen_image))))
        {
            var choice = Dm.@new<datum_radial_menu_choice>();
            choice.name = Dm.initial(screen_option, (datum_pai_screen_image s) => s.name);
            choice.image = image(icon: Dm.initial(screen_option, (datum_pai_screen_image s) => s.icon), icon_state: Dm.initial(screen_option, (datum_pai_screen_image s) => s.icon_state));
            possible_choices[screen_option] = choice; // possible_choices[screen_option] += choice: the list's first value
        }
        atom anchor = get_atom_on_turf(this) as atom;
        object new_image = await show_radial_menu(this, anchor, possible_choices, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), anchor), radius: 40, require_near: TRUE);
        if (new_image == null)
            return FALSE;
        card.screen_image = new_image as Type;
        card.update_appearance();
        return TRUE;
    }

    /**
     * Supporting proc for the pAI to prick it's master's hand
     * or... whatever. It must be held in order to work
     * Gives the owner a popup if they want to get the jab.
     *
     * @returns {boolean} - TRUE if a sample was taken, FALSE otherwise.
     */
    // tg: code/modules/pai/software.dm:158
    public virtual async DmTask<object> check_dna()
    {
        if (Dm.truthy(emagged)) // Their master DNA signature is scrambled anyway
        {
            to_chat(this, span_syndradio(I18n.T("You are not at liberty to do this! All agents are clandestine.")));
            return FALSE;
        }
        mob_living_carbon holder = get_holder();
        // tg's get_holder returns FALSE, not null, when no one carries the pAI, so `if(!isnull(holder))` always refuses (a tg slip): as tg
        object holder_or_false = (object)holder ?? FALSE;
        if (holder_or_false != null)
        {
            balloon_alert(this, "not being carried");
            return FALSE;
        }
        balloon_alert(this, "requesting dna sample");
        if (!Dm.eq(await tgui_alert(holder, I18n.T("[] is requesting a DNA sample from you. Will you allow it to confirm your identity?", this), "Checking DNA", Dm.list("Yes", "No")), "Yes"))
        {
            balloon_alert(this, "dna sample refused!");
            return FALSE;
        }
        holder.visible_message(span_notice(I18n.T("[] presses [] thumb against [].", holder, holder.p_their(), this)), span_notice(I18n.T("You press your thumb against [].", this)), span_notice(I18n.T("[] makes a sharp clicking sound as it extracts DNA material from [].", this, holder)));
        if (!Dm.truthy(holder.has_dna()))
        {
            balloon_alert(this, "no dna detected!");
            return FALSE;
        }
        to_chat(this, span_bolddanger(I18n.T("[]'s UE string: []", holder, holder.dna.unique_enzymes)));
        to_chat(this, span_notice(I18n.T("DNA [] our stored Master's DNA.", Dm.eq(holder.dna.unique_enzymes, master_dna) ? "matches" : "does not match")));
        return TRUE;
    }

    /**
     * Grant all languages to the current pAI.
     *
     * @returns {boolean} - TRUE if the languages were granted, FALSE otherwise.
     */
    // tg: code/modules/pai/software.dm:186
    public virtual object grant_languages()
    {
        if (Dm.truthy(languages_granted))
            return FALSE;
        grant_all_languages(source: LANGUAGE_SOFTWARE);
        languages_granted = TRUE;
        return TRUE;
    }

    /**
     * Host scan supporting proc
     *
     * Allows the pAI to scan its host's health vitals
     * using an integrated health analyzer.
     *
     * @returns {boolean} - TRUE if the scan was successful, FALSE otherwise.
     */
    // tg: code/modules/pai/software.dm:202
    public virtual async DmTask<object> host_scan(object mode = null)
    {
        switch ((int)Dm.num(mode))
        {
            case PAI_SCAN_TARGET when mode != null:
            {
                mob_living_carbon target = get_holder();
                if (target == null)
                {
                    balloon_alert(this, "not being carried!");
                    return FALSE;
                }
                healthscan(this, target);
                return TRUE;
            }
            case PAI_SCAN_MASTER:
            {
                mob_living resolved_master = find_master();
                if (resolved_master == null)
                {
                    balloon_alert(this, "no master detected!");
                    return FALSE;
                }
                if (!Dm.truthy(is_valid_z_level(get_turf(this), get_turf(resolved_master))))
                {
                    balloon_alert(this, "master out of range!");
                    return FALSE;
                }
                healthscan(this, resolved_master);
                return TRUE;
            }
        }
        stack_trace(Dm.text("Invalid mode passed to host scan: []", Dm.truthy(mode) ? mode : "null"), "code/modules/pai/software.dm", 225);
        return FALSE;
    }

    /// Huds from PAI software
    // tg: code/modules/pai/software.dm:230  #define PAI_HUD_TRAIT "pai_hud" (file-local, #undef at :252)
    private const string PAI_HUD_TRAIT = "pai_hud";

    /**
     * Proc that toggles any active huds based on the option.
     *
     * @param {string} mode - The hud to toggle.
     */
    // tg: code/modules/pai/software.dm:237
    public virtual object toggle_hud(object mode = null)
    {
        if (mode == null)
            return FALSE;
        if (Dm.eq(mode, PAI_TOGGLE_MEDICAL_HUD))
        {
            if (HAS_TRAIT_FROM(this, TRAIT_MEDICAL_HUD, PAI_HUD_TRAIT))
                REMOVE_TRAIT(this, TRAIT_MEDICAL_HUD, PAI_HUD_TRAIT);
            else
                ADD_TRAIT(this, TRAIT_MEDICAL_HUD, PAI_HUD_TRAIT);
        }
        if (Dm.eq(mode, PAI_TOGGLE_SECURITY_HUD))
        {
            if (HAS_TRAIT_FROM(this, TRAIT_SECURITY_HUD, PAI_HUD_TRAIT))
                REMOVE_TRAIT(this, TRAIT_SECURITY_HUD, PAI_HUD_TRAIT);
            else
                ADD_TRAIT(this, TRAIT_SECURITY_HUD, PAI_HUD_TRAIT);
        }
        return TRUE;
    }
}

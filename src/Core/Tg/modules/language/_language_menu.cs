// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/_language_menu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps L2 (D327): the whole file, the language menu (its LanguageMenu window): every language with whether its owner speaks,
// could speak and understands it (and how much of it), the default language to speak, and an admin's or a ghost's grant, removal
// and omnitongue with the admins' notice and log. The var block is generated (D111).

// tg: code/modules/language/_language_menu.dm:1
public partial class datum_language_menu : datum
{
    // tg: code/modules/language/_language_menu.dm:4
    [DmParams("_language_holder")]
    public override async DmTask New(params object[] __extra)
    {
        language_holder = Dm.arg(__extra, 0) as datum_language_holder;
        await base.New();
    }

    // tg: code/modules/language/_language_menu.dm:7
    public override object Destroy(float? force = null)
    {
        language_holder = null;
        return base.Destroy(force);
    }

    // tg: code/modules/language/_language_menu.dm:11
    public override object ui_state(mob user = null) => GLOB.language_menu_state;

    // tg: code/modules/language/_language_menu.dm:14
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (language_holder.selected_language == null)
            language_holder.get_selected_language();

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "LanguageMenu");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/language/_language_menu.dm:23
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        atom_movable speaker = language_holder.owner as atom_movable;
        DmList partial_languages = speaker?.get_partially_understood_languages() as DmList;
        DmList languages = Dm.list();
        data["languages"] = languages;
        foreach (Type language in Dm.each_cast<Type>(GLOB.all_languages))
        {
            DmList lang_data = Dm.list();

            lang_data["name"] = Dm.initial(language, (datum_language p) => p.name);
            lang_data["desc"] = Dm.initial(language, (datum_language p) => p.desc);
            lang_data["key"] = Dm.initial(language, (datum_language p) => p.key);
            lang_data["is_default"] = Dm.eq(language, language_holder.selected_language) ? TRUE : FALSE;
            lang_data["icon"] = Dm.initial(language, (datum_language p) => p.icon);
            lang_data["icon_state"] = Dm.initial(language, (datum_language p) => p.icon_state);
            if (speaker != null)
            {
                lang_data["can_speak"] = Dm.truthy(speaker.has_language(language, SPOKEN_LANGUAGE)) ? TRUE : FALSE;
                lang_data["could_speak"] = Dm.truthy(language_holder.omnitongue) || Dm.truthy(speaker.could_speak_language(language)) ? TRUE : FALSE;
                lang_data["can_understand"] = Dm.truthy(speaker.has_language(language, UNDERSTOOD_LANGUAGE)) ? TRUE : FALSE;
                lang_data["partial_understanding"] = partial_languages?[language] ?? 0f;
            }

            Dm.append(ref languages, Dm.list(lang_data)); // UNTYPED_LIST_ADD
        }

        data["is_living"] = isliving(speaker) ? TRUE : FALSE;
        data["admin_mode"] = Dm.truthy(check_rights_for(user.client, R_ADMIN)) || isobserver(speaker) ? TRUE : FALSE;
        data["omnitongue"] = language_holder.omnitongue;

        return data;
    }

    // tg: code/modules/language/_language_menu.dm:52
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = ui.user;
        atom_movable speaker = language_holder.owner as atom_movable;
        bool is_admin = Dm.truthy(check_rights_for(user.client, R_ADMIN));
        object language_name = @params["language_name"];
        Type language_datum = null;
        foreach (Type language in Dm.each_cast<Type>(GLOB.all_languages))
        {
            if (Dm.eq(language_name, Dm.initial(language, (datum_language p) => p.name)))
                language_datum = language;
        }

        switch (Dm.str(action))
        {
            case "select_default":
                if (language_datum != null && Dm.truthy(speaker.can_speak_language(language_datum)))
                {
                    language_holder.selected_language = language_datum;
                    dot = TRUE;
                }
                break;
            case "grant_language":
                if ((is_admin || isobserver(speaker)) && language_datum != null)
                {
                    DmList choices = Dm.list(I18n.T("Only Spoken"), I18n.T("Only Understood"), I18n.T("Both"));
                    object choice = await tgui_input_list(user, I18n.T("How do you want to add this language?"), Dm.text("[]", Dm.initial(language_datum, (datum_language p) => p.name)), choices);
                    if (choice == null)
                        return null;
                    float adding_flags = NONE;
                    if (Dm.eq(choice, I18n.T("Only Spoken")))
                        adding_flags = Dm.bitor(adding_flags, SPOKEN_LANGUAGE);
                    else if (Dm.eq(choice, I18n.T("Only Understood")))
                        adding_flags = Dm.bitor(adding_flags, UNDERSTOOD_LANGUAGE);
                    else if (Dm.eq(choice, I18n.T("Both")))
                        adding_flags = Dm.bitor(adding_flags, ALL);

                    bool ask_to_remove_block = false;
                    DmList block_being_removed_on = Dm.list();
                    if (Dm.truthy(Dm.bitand(adding_flags, SPOKEN_LANGUAGE)) && Dm.truthy(LAZYACCESS(language_holder.blocked_speaking, language_datum)))
                    {
                        ask_to_remove_block = true;
                        Dm.append(ref block_being_removed_on, I18n.T("spoken"));
                    }

                    if (ask_to_remove_block)
                    {
                        choice = await tgui_alert(user, I18n.T("Do you want to lift the blockage that's also preventing the language to be []?", block_being_removed_on.Join(I18n.T(" or "))), Dm.text("[]", Dm.initial(language_datum, (datum_language p) => p.name)), Dm.list(I18n.T("Yes"), I18n.T("No")));
                        if (Dm.eq(choice, I18n.T("Yes")))
                            language_holder.remove_blocked_language(language_datum, adding_flags, LANGUAGE_ALL);
                    }
                    language_holder.grant_language(language_datum, adding_flags);
                    if (is_admin)
                    {
                        message_admins(Dm.text("[] granted the [] language to [].", key_name_admin(user), language_name, key_name_admin(speaker)));
                        log_admin(Dm.text("[] granted the language [] to [].", key_name(user), language_name, key_name(speaker)));
                    }
                    dot = TRUE;
                }
                break;
            case "remove_language":
                if ((is_admin || isobserver(speaker)) && language_datum != null)
                {
                    DmList choices = Dm.list(I18n.T("Only Spoken"), I18n.T("Only Understood"), I18n.T("Both"));
                    object choice = await tgui_input_list(user, I18n.T("Which part do you wish to remove?"), Dm.text("[]", Dm.initial(language_datum, (datum_language p) => p.name)), choices);
                    if (choice == null)
                        return null;
                    float removing_flags = NONE;
                    if (Dm.eq(choice, I18n.T("Only Spoken")))
                        removing_flags = Dm.bitor(removing_flags, SPOKEN_LANGUAGE);
                    else if (Dm.eq(choice, I18n.T("Only Understood")))
                        removing_flags = Dm.bitor(removing_flags, UNDERSTOOD_LANGUAGE);
                    else if (Dm.eq(choice, I18n.T("Both")))
                        removing_flags = Dm.bitor(removing_flags, ALL);

                    language_holder.remove_language(language_datum, removing_flags);
                    if (is_admin)
                    {
                        message_admins(Dm.text("[] removed the [] language to [].", key_name_admin(user), language_name, key_name_admin(speaker)));
                        log_admin(Dm.text("[] removed the language [] to [].", key_name(user), language_name, key_name(speaker)));
                    }
                    dot = TRUE;
                }
                break;
            case "toggle_omnitongue":
                if (is_admin || isobserver(speaker))
                {
                    language_holder.omnitongue = Dm.truthy(language_holder.omnitongue) ? FALSE : TRUE;
                    if (is_admin)
                    {
                        message_admins(Dm.text("[] [] the ability to speak all languages (that they know) of [].", key_name_admin(user), Dm.truthy(language_holder.omnitongue) ? "enabled" : "disabled", key_name_admin(speaker)));
                        log_admin(Dm.text("[] [] the ability to speak all languages (that_they know) of [].", key_name(user), Dm.truthy(language_holder.omnitongue) ? "enabled" : "disabled", key_name(speaker)));
                    }
                    dot = TRUE;
                }
                break;
        }
        return dot;
    }
}

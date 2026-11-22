// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/ai_status_display_picker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326). The var block is generated (D111).

/**
 * AI Status Display Picker TGUI
 * Allows AIs to select status display options with search functionality
 */
// tg: code/modules/mob/living/silicon/ai/ai_status_display_picker.dm:6
public partial class datum_ai_status_display_picker : datum
{
    // tg: code/modules/mob/living/silicon/ai/ai_status_display_picker.dm:12
    [DmParams("user")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        ai_user = Dm.arg<mob_living_silicon_ai>(__extra, 0);
    }

    // tg: code/modules/mob/living/silicon/ai/ai_status_display_picker.dm:15
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (ai_user == null || user != ai_user || Dm.truthy(ai_user.incapacitated))
            return UI_CLOSE;
        return await base.ui_status(user, state);
    }

    // tg: code/modules/mob/living/silicon/ai/ai_status_display_picker.dm:20
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_status_display_picker.dm:23
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "AiStatusDisplayPicker");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_status_display_picker.dm:29
    public override void ui_close(mob user = null)
    {
        if (ai_user != null)
            ai_user.status_display_picker = null;
    }

    // No assets needed for DMIcon system

    // tg: code/modules/mob/living/silicon/ai/ai_status_display_picker.dm:35
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        // Always ensure initialization
        init_ai_status_display_options();

        DmList data = Dm.list();
        DmList options = Dm.list();

        foreach (object option_name in Dm.each(GLOB.ai_status_display_emotes))
        {
            object icon_state = GLOB.ai_status_display_emotes[option_name];
            DmList option_data = Dm.list(
                Dm.kv("name", option_name),
                Dm.kv("icon_state", icon_state),
                Dm.kv("is_original", TRUE),
                Dm.kv("icon", Dm.resource("icons/obj/machines/status_display.dmi"))
            );
            Dm.append(ref options, Dm.list(option_data));
        }

        foreach (object option_name in Dm.each(GLOB.ai_core_to_status_display_mapping))
        {
            object icon_state = GLOB.ai_core_to_status_display_mapping[option_name];
            DmList option_data = Dm.list(
                Dm.kv("name", option_name),
                Dm.kv("icon_state", icon_state),
                Dm.kv("is_original", FALSE),
                Dm.kv("icon", Dm.resource("icons/obj/machines/status_display.dmi"))
            );
            Dm.append(ref options, Dm.list(option_data));
        }

        data["options"] = options;

        return data;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_status_display_picker.dm:67
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        // Find current display emotion from first available AI status display
        object current_emotion = "None";
        object current_icon_state;
        obj_machinery_status_display_ai first_display = Dm.locate<obj_machinery_status_display_ai>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_status_display_ai)) as DmList);
        if (Dm.truthy(first_display?.emotion))
        {
            current_emotion = first_display.emotion;
            if (Dm.@in(current_emotion, GLOB.ai_status_display_emotes))
                current_icon_state = GLOB.ai_status_display_emotes[current_emotion];
            else if (Dm.@in(current_emotion, GLOB.ai_core_to_status_display_mapping))
                current_icon_state = GLOB.ai_core_to_status_display_mapping[current_emotion];
            else
                current_icon_state = "ai_download";
        }
        else
            current_icon_state = "ai_download";

        data["current_emotion"] = current_emotion;
        data["current_icon"] = Dm.list(
            Dm.kv("icon", Dm.resource("icons/obj/machines/status_display.dmi")),
            Dm.kv("icon_state", current_icon_state)
        );

        return data;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_status_display_picker.dm:93
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (ai_user == null)
            return FALSE;

        switch (Dm.str(action))
        {
            case "select_option":
            {
                object selected_option = @params["option"];
                if (!Dm.truthy(selected_option) || !Dm.@in(selected_option, GLOB.ai_status_display_all_options))
                    return FALSE;

                // Check if this is an original AI emotion (has a corresponding emote)
                bool found_emote = false;
                foreach (Type emote in Dm.each<Type>(typesof(typeof(datum_emote_ai_emotion_display))))
                {
                    if (Dm.eq(Dm.initial(emote, (datum_emote_ai_emotion_display e) => e.emotion), selected_option))
                    {
                        await ai_user.emote(Dm.initial(emote, (datum_emote e) => e.key));
                        found_emote = true;
                        break;
                    }
                }

                // If no emote found (i.e., it's a new AI core display option), directly apply it
                if (!found_emote)
                    ai_user.apply_emote_display(selected_option);

                return TRUE;
            }
        }

        return FALSE;
    }
}

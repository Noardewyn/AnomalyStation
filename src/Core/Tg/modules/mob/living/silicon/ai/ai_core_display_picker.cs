// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/ai_core_display_picker.dm
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
 * AI Core Display Picker TGUI
 * Allows AIs to select core display options with search functionality
 */
// tg: code/modules/mob/living/silicon/ai/ai_core_display_picker.dm:5
public partial class datum_ai_core_display_picker : datum
{
    // tg: code/modules/mob/living/silicon/ai/ai_core_display_picker.dm:8
    [DmParams("user")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        ai_user = Dm.arg<mob_living_silicon_ai>(__extra, 0);
    }

    // tg: code/modules/mob/living/silicon/ai/ai_core_display_picker.dm:11
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (ai_user == null || user != ai_user || Dm.truthy(ai_user.incapacitated))
            return UI_CLOSE;
        return await base.ui_status(user, state);
    }

    // tg: code/modules/mob/living/silicon/ai/ai_core_display_picker.dm:16
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_core_display_picker.dm:19
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "AiCoreDisplayPicker");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_core_display_picker.dm:25
    public override void ui_close(mob user = null)
    {
        if (ai_user != null)
            ai_user.core_display_picker = null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_core_display_picker.dm:29
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        // If no override is set, find the actual current display from the AI's icon state
        object current_display = ai_user.display_icon_override;
        if (!Dm.truthy(current_display))
        {
            // Default to "Blue" if no override
            current_display = "Blue";
        }

        // Try to identify current display
        foreach (object display_name in Dm.each(GLOB.ai_core_display_screens))
        {
            if (Dm.eq(resolve_ai_icon_sync(display_name), current_display))
            {
                current_display = display_name;
                break;
            }
        }

        data["current_display"] = current_display;

        // Get icon for current display
        object current_icon_state = resolve_ai_icon_sync(current_display);
        data["current_icon"] = Dm.list(
            Dm.kv("icon", Dm.resource("icons/mob/silicon/ai.dmi")),
            Dm.kv("icon_state", current_icon_state)
        );

        DmList options = Dm.list();

        foreach (object option_name in Dm.each(GLOB.ai_core_display_screens))
        {
            object icon_state = resolve_ai_icon_sync(option_name);
            DmList option_data = Dm.list(
                Dm.kv("name", option_name),
                Dm.kv("icon_state", icon_state),
                Dm.kv("icon", Dm.resource("icons/mob/silicon/ai.dmi"))
            );
            Dm.append(ref options, Dm.list(option_data));
        }

        data["options"] = options;

        return data;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_core_display_picker.dm:68
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "select_option":
                object chosen_option = @params["option"];
                if (Dm.@in(chosen_option, GLOB.ai_core_display_screens))
                {
                    ai_user.display_icon_override = chosen_option;
                    await ai_user.set_core_display_icon(chosen_option);
                    return TRUE;
                }
                break;
        }
        return dot;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/emote_panel/emote_panel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice B (D256): the whole file, tg's emote panel, which the emote wheel's "More..." opens (the hidden verb emote_panel,
// interface/interface.cs); its window is tgui's EmotePanel (the client's Shared/Tgui/Interfaces/EmotePanel.cs).

// tg: code/modules/emote_panel/emote_panel.dm:1
public partial class datum_emote_panel : datum
{
    // tg: code/modules/emote_panel/emote_panel.dm:4
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList emotes = Dm.list();
        DmList keys = Dm.list();

        foreach (object key in Dm.each(GLOB.emote_list))
        {
            foreach (datum_emote emote in Dm.each<datum_emote>(GLOB.emote_list[key]))
            {
                if (Dm.@in(emote.key, keys))
                    continue;
                if (Dm.@in(emote.key, blacklisted_emotes))
                    continue;
                if (Dm.truthy(await emote.can_run_emote(user, status_check: FALSE, intentional: FALSE)))
                {
                    Dm.append(ref keys, emote.key);
                    Dm.append(ref emotes, Dm.list(Dm.list(
                        Dm.kv("key", emote.key),
                        Dm.kv("name", emote.name),
                        Dm.kv("hands", emote.hands_use_check),
                        Dm.kv("visible", Dm.bitand(emote.emote_type, EMOTE_VISIBLE)),
                        Dm.kv("audible", Dm.bitand(emote.emote_type, EMOTE_AUDIBLE)),
                        Dm.kv("sound", Dm.num(emote.get_sound(user as mob_living) != null)),
                        Dm.kv("use_params", emote.message_param))));
                }
            }
        }

        data["emotes"] = emotes;

        return data;
    }

    // tg: code/modules/emote_panel/emote_panel.dm:34
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "play_emote":
            {
                object emote_key = @params["emote_key"];
                if (emote_key == null || !Dm.truthy(GLOB.emote_list[emote_key]))
                    return null;
                object use_params = @params["use_params"];
                var emote = (GLOB.emote_list[emote_key] as DmList)[1] as datum_emote;
                object emote_param = null;
                if (Dm.truthy(emote.message_param) && Dm.truthy(use_params))
                    emote_param = await tgui_input_text(ui.user, I18n.T("Add params to the emote..."), emote.message_param, max_length: MAX_MESSAGE_LEN);
                await ui.user.emote(emote_key, message: emote_param, intentional: TRUE);
                break;
            }
            case "preview_sound":
            {
                object emote_key = @params["emote_key"];
                if (emote_key == null || !Dm.truthy(GLOB.emote_list[emote_key]))
                    return null;
                var emote = (GLOB.emote_list[emote_key] as DmList)[1] as datum_emote;
                object emote_sound = get_sfx(emote.get_sound(ui.user as mob_living));
                if (!Dm.truthy(emote_sound))
                {
                    to_chat(ui.user, span_warning(I18n.T("Couldn't get a preview sound for [].", emote.name)), type: MESSAGE_TYPE_INFO);
                    return null;
                }
                Dm.output_to(ui.user, sound(emote_sound, volume: 75)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
                to_chat(ui.user, span_warning(I18n.T("Previewed sound for [].", emote.name)), type: MESSAGE_TYPE_INFO);
                break;
            }
        }
        return null;
    }

    // tg: code/modules/emote_panel/emote_panel.dm:59
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "EmotePanel");
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/emote_panel/emote_panel.dm:66
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }
}

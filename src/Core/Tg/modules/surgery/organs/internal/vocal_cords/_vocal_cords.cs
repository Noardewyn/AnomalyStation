// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/vocal_cords/_vocal_cords.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1, D339, D341): the vocal cords spoken through with the :x channel and the golems' adamantine cords, which work
// in any carbon. The var blocks are generated (D111).

// tg: code/modules/surgery/organs/internal/vocal_cords/_vocal_cords.dm:1
public partial class obj_item_organ_vocal_cords : obj_item_organ //organs that are activated through speech with the :x/MODE_KEY_VOCALCORDS channel
{
    // tg: code/modules/surgery/organs/internal/vocal_cords/_vocal_cords.dm:12
    public virtual object can_speak_with() //if there is any limitation to speaking with these cords
    {
        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/vocal_cords/_vocal_cords.dm:15
    public virtual async DmTask speak_with(object message = null) //do what the organ does
    {
        await DmTask.CompletedTask;
        return;
    }

    // tg: code/modules/surgery/organs/internal/vocal_cords/_vocal_cords.dm:18
    public virtual async DmTask handle_speech(object message = null) //actually say the message
    {
        await owner.say(message, spans: spans, sanitize: FALSE);
    }
}

// tg: code/modules/surgery/organs/internal/vocal_cords/_vocal_cords.dm:34
public partial class datum_action_item_action_organ_action_use_adamantine_vocal_cords : datum_action_item_action_organ_action_use
{
    // tg: code/modules/surgery/organs/internal/vocal_cords/_vocal_cords.dm:34
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        object message = await tgui_input_text(owner, I18n.T("Resonate a message to all nearby golems"), I18n.T("Resonate"), max_length: MAX_MESSAGE_LEN);
        if (!Dm.truthy(message))
            return FALSE;
        if (QDELETED(this) || QDELETED(owner))
            return FALSE;
        await owner.say(Dm.text(".x[]", message));
        return TRUE;
    }
}

// tg: code/modules/surgery/organs/internal/vocal_cords/_vocal_cords.dm:28
public partial class obj_item_organ_vocal_cords_adamantine : obj_item_organ_vocal_cords
{
    // tg: code/modules/surgery/organs/internal/vocal_cords/_vocal_cords.dm:43
    public override async DmTask handle_speech(object message = null)
    {
        await DmTask.CompletedTask;
        string msg = span_resonate(I18n.T("[] resonates, \"[]\"", span_name(Dm.str(owner.real_name)), message));
        foreach (object player in Dm.each(GLOB.player_list))
        {
            if (iscarbon(player))
            {
                mob_living_carbon speaker = player as mob_living_carbon;
                if (Dm.truthy(speaker.get_organ_slot(ORGAN_SLOT_ADAMANTINE_RESONATOR)))
                    to_chat(speaker, msg, type: MESSAGE_TYPE_RADIO, avoid_highlighting: Dm.num(speaker == owner));
            }
            else if (isobserver(player))
            {
                string link = FOLLOW_LINK(player, owner);
                to_chat(player, Dm.text("[] []", link, msg), type: MESSAGE_TYPE_RADIO);
            }
        }
    }
}

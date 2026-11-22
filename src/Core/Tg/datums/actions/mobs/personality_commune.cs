// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/personality_commune.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps B1 (D327): the whole file, the split personality's commune with its other self. The var block is generated (D111).

// This can probably be changed to use mind linker at some point
// tg: code/datums/actions/mobs/personality_commune.dm:2
public partial class datum_action_personality_commune : datum_action
{
    // tg: code/datums/actions/mobs/personality_commune.dm:13
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!istype(target, typeof(datum_brain_trauma_severe_split_personality)))
        {
            stack_trace(Dm.text("[] was created on a target that isn't a /datum/brain_trauma/severe/split_personality, this doesn't work.", GetType()), "code/datums/actions/mobs/personality_commune.dm", 16);
            qdel(this);
        }
    }

    // tg: code/datums/actions/mobs/personality_commune.dm:19
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        if (!istype(grant_to, typeof(mob_living_split_personality)))
            return null;

        return await base.Grant(grant_to, __extra);
    }

    // tg: code/datums/actions/mobs/personality_commune.dm:25
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return FALSE;

        datum_brain_trauma_severe_split_personality trauma = target as datum_brain_trauma_severe_split_personality;
        mob_living_split_personality non_controller = usr as mob_living_split_personality;
        client non_controller_client = non_controller.client;

        object to_send = await tgui_input_text(non_controller, I18n.T("What would you like to tell your other self?"), I18n.T("Commune"), max_length: MAX_MESSAGE_LEN);
        if (QDELETED(this) || QDELETED(trauma) || !Dm.truthy(to_send))
            return FALSE;

        mob_living_carbon_human personality_body = trauma.owner as mob_living_carbon_human;
        if (personality_body.client == non_controller_client) // We took control
            return FALSE;

        string user_message = span_boldnotice(I18n.T("You concentrate and send thoughts to your other self:"));
        string user_message_body = span_notice(Dm.text("[]", to_send));

        to_chat(non_controller, Dm.text("[] []", user_message, user_message_body));

        personality_body.balloon_alert(personality_body, I18n.T("you hear a voice"));
        to_chat(personality_body, Dm.text("[] []", fluff_text, user_message_body));

        log_directed_talk(non_controller, personality_body, to_send, LOG_SAY, Dm.text("[]", name));
        foreach (object dead_mob in Dm.each(GLOB.dead_mob_list))
        {
            if (!isobserver(dead_mob))
                continue;
            to_chat(dead_mob, Dm.text("[] [] [] []", FOLLOW_LINK(dead_mob, non_controller), span_boldnotice(Dm.text("[] []:", non_controller, name)), span_notice(Dm.text("\"[]\" to", to_send)), span_name(Dm.text("[]", trauma))));
        }

        return TRUE;
    }
}

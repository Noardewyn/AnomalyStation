// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/pierrot_throat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/pierrot_throat.dm:1
public partial class datum_disease_pierrot_throat : datum_disease
{
    // tg: code/datums/diseases/pierrot_throat.dm:16
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 1)
        {
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel a little silly.")));
        }
        else if (stage == 2)
        {
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You start seeing rainbows.")));
        }
        else if (stage == 3)
        {
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your thoughts are interrupted by a loud <b>HONK!</b>")));
        }
        else if (stage == 4)
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
                await affected_mob.say(pick(Dm.list("HONK!", "Honk!", "Honk.", "Honk?", "Honk!!", "Honk?!", "Honk...")), forced: "pierrot's throat");
        }
        return dot;
    }


    // tg: code/datums/diseases/pierrot_throat.dm:36
    public override async DmTask after_add()
    {
        RegisterSignal(affected_mob, COMSIG_MOB_SAY, nameof(handle_speech));
        await DmTask.CompletedTask;
    }


    // tg: code/datums/diseases/pierrot_throat.dm:40
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        object message = speech_args[SPEECH_MESSAGE];
        DmList split_message = splittext(message, " "); //List each word in the message
        float applied = 0;
        for (float i = 1, i__end = Dm.length(split_message); i <= i__end; i++)
        {
            if (prob(3 * stage)) //Stage 1: 3% Stage 2: 6% Stage 3: 9% Stage 4: 12%
            {
                if (Dm.truthy(findtext(split_message[i], "*")) || Dm.truthy(findtext(split_message[i], ";")) || Dm.truthy(findtext(split_message[i], ":")))
                    continue;
                split_message[i] = "HONK";
                if (applied++ > stage)
                    break;
            }
        }
        if (Dm.truthy(applied))
            Dm.combine((DmList)speech_args[SPEECH_SPANS], SPAN_CLOWN); // a little bonus
        message = jointext(split_message, " ");
        speech_args[SPEECH_MESSAGE] = message;
    }


    // tg: code/datums/diseases/pierrot_throat.dm:59
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(affected_mob, COMSIG_MOB_SAY);
        return base.Destroy(force);
    }

    // tg: code/datums/diseases/pierrot_throat.dm:63
    public override void remove_disease()
    {
        UnregisterSignal(affected_mob, COMSIG_MOB_SAY);
        base.remove_disease();
    }
}

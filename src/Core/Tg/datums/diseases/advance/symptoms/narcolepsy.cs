// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/narcolepsy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/narcolepsy.dm:9
public partial class datum_symptom_narcolepsy : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/narcolepsy.dm:28
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalTransmittable()) >= 4) //yawning (mostly just some copy+pasted code from sneezing, with a few tweaks)
            yawning = TRUE;
        if (Dm.num(A.totalStageSpeed()) >= 10) //act more often
            symptom_delay = 32.5f;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/narcolepsy.dm:37
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (A.stage == 1)
        {
            if (prob(50))
                to_chat(M, span_warning(I18n.T("You feel tired.")));
        }
        else if (A.stage == 2)
        {
            if (prob(50))
                to_chat(M, span_warning(I18n.T("You feel very tired.")));
        }
        else if (A.stage == 3)
        {
            if (prob(50))
                to_chat(M, span_warning(I18n.T("You try to focus on staying awake.")));
            await M.adjust_drowsiness_up_to(10 * SECONDS, 140 * SECONDS);
        }
        else if (A.stage == 4)
        {
            if (prob(50))
            {
                if (Dm.truthy(yawning))
                    to_chat(M, span_warning(I18n.T("You try and fail to suppress a yawn.")));
                else
                    to_chat(M, span_warning(I18n.T("You nod off for a moment."))); //you can't really yawn while nodding off, can you?
            }
            await M.adjust_drowsiness_up_to(20 * SECONDS, 140 * SECONDS);
            if (Dm.truthy(yawning))
            {
                await M.emote("yawn");
                await A.airborne_spread(6);
            }
        }
        else if (A.stage == 5)
        {
            if (prob(50))
                to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("So tired...", "You feel very sleepy.", "You have a hard time keeping your eyes open.", "You try to stay awake."))))));
            await M.adjust_drowsiness_up_to(80 * SECONDS, 140 * SECONDS);
            if (Dm.truthy(yawning))
            {
                await M.emote("yawn");
                await A.airborne_spread(6);
            }
        }
        return dot;
    }
}

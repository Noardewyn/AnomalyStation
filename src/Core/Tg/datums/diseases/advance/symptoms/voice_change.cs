// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/voice_change.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/voice_change.dm:10
public partial class datum_symptom_voice_change : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/voice_change.dm:33
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 3)
            suppress_warning = TRUE;
        if (Dm.num(A.totalStageSpeed()) >= 7) //faster change of voice
        {
            base_message_chance = 25;
            symptom_delay = 55;
            delay_variation = 0.35f;
        }
        if (Dm.num(A.totalTransmittable()) >= 14) //random language
            scramble_language = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/voice_change.dm:46
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon M = A.affected_mob;
        if (A.stage == 1 || A.stage == 2 || A.stage == 3 || A.stage == 4)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("Your throat hurts.", "You clear your throat."))))));
        }
        else
        {
            if (ishuman(M))
            {
                mob_living_carbon_human H = (mob_living_carbon_human)M;
                H.override_voice = Dm.str(H.generate_random_mob_name());
                if (Dm.truthy(scramble_language) && current_language == null) // Last part prevents rerolling language with small amounts of cure.
                {
                    current_language = pick(Dm.sub(subtypesof(typeof(datum_language)), typeof(datum_language_common)) as DmList) as Type;
                    H.add_blocked_language(Dm.sub(subtypesof(typeof(datum_language)), current_language), source: LANGUAGE_VOICECHANGE);
                    H.grant_language(current_language, source: LANGUAGE_VOICECHANGE);
                }
            }
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/voice_change.dm:64
    public override async DmTask<object> End(datum_disease_advance A = null)
    {
        await base.End(A);
        if (ishuman(A.affected_mob))
        {
            mob_living_carbon_human H = (mob_living_carbon_human)A.affected_mob;
            H.override_voice = "";
        }
        if (Dm.truthy(scramble_language))
        {
            A.affected_mob.remove_blocked_language(subtypesof(typeof(datum_language)), source: LANGUAGE_VOICECHANGE);
            A.affected_mob.remove_all_languages(LANGUAGE_VOICECHANGE); // In case someone managed to get more than one anyway.
        }
        return null;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/hallucigen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/hallucigen.dm:10
public partial class datum_symptom_hallucigen : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/hallucigen.dm:30
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 4) //fake good symptom messages
        {
            fake_healthy = TRUE;
            base_message_chance = 50;
        }
        if (Dm.num(A.totalStageSpeed()) >= 7) //stronger hallucinations
            power = 2;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/hallucigen.dm:40
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon M = A.affected_mob;
        DmList healthy_messages = Dm.list("Your lungs feel great.", "You realize you haven't been breathing.", "You don't feel the need to breathe.",
                        "Your eyes feel great.", "Your ears feel great.", "You don't feel the need to blink.");
        if (A.stage == 1 || A.stage == 2)
        {
            if (prob(base_message_chance))
            {
                if (!Dm.truthy(fake_healthy))
                    to_chat(M, span_notice(I18n.T(Dm.str(pick(Dm.list("Something appears in your peripheral vision, then winks out.", "You hear a faint whisper with no source.", "Your head aches."))))));
                else
                    to_chat(M, span_notice(I18n.T(Dm.str(pick(healthy_messages)))));
            }
        }
        else if (A.stage == 3 || A.stage == 4)
        {
            if (prob(base_message_chance))
            {
                if (!Dm.truthy(fake_healthy))
                    to_chat(M, span_danger(I18n.T(Dm.str(pick(Dm.list("Something is following you.", "You are being watched.", "You hear a whisper in your ear.", "Thumping footsteps slam toward you from nowhere."))))));
                else
                    to_chat(M, span_notice(I18n.T(Dm.str(pick(healthy_messages)))));
            }
        }
        else
        {
            if (prob(base_message_chance))
            {
                if (!Dm.truthy(fake_healthy))
                    to_chat(M, span_userdanger(I18n.T(Dm.str(pick(Dm.list("Oh, your head...", "Your head pounds.", "They're everywhere! Run!", "Something in the shadows..."))))));
                else
                    to_chat(M, span_notice(I18n.T(Dm.str(pick(healthy_messages)))));
            }
            await M.adjust_hallucinations(90 * SECONDS * Dm.num(power));
        }
        return dot;
    }
}

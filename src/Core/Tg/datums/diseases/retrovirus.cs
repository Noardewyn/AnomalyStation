// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/retrovirus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/retrovirus.dm:1
public partial class datum_disease_dna_retrovirus : datum_disease
{
    // tg: code/datums/diseases/retrovirus.dm:17
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        agent = Dm.text("Virus class [][]-[]", pick(Dm.list("A", "B", "C", "D", "E", "F")), pick(Dm.list("A", "B", "C", "D", "E", "F")), rand(50, 300));
        if (prob(40))
            cures = Dm.list(typeof(datum_reagent_medicine_mutadone));
        else
            restcure = 1;
    }

    // tg: code/datums/diseases/retrovirus.dm:25
    public override async DmTask<object> Copy()
    {
        datum_disease_dna_retrovirus D = (datum_disease_dna_retrovirus)await base.Copy();
        D.restcure = restcure;
        return D;
    }

    // tg: code/datums/diseases/retrovirus.dm:30
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (stage == 1)
        {
            if (SPT_PROB(4, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your head hurts.")));
            if (SPT_PROB(4.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel a tingling sensation in your chest.")));
            if (SPT_PROB(4.5f, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel angry.")));
            if (Dm.truthy(restcure) && affected_mob.body_position == LYING_DOWN && SPT_PROB(16, seconds_per_tick))
            {
                to_chat(affected_mob, span_notice(I18n.T("You feel better.")));
                await cure();
                return FALSE;
            }
        }
        else if (stage == 2)
        {
            if (SPT_PROB(4, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your skin feels loose.")));
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel very strange.")));
            if (SPT_PROB(2, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel a stabbing pain in your head!")));
                await affected_mob.Unconscious(40);
            }
            if (SPT_PROB(2, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your stomach churns.")));
            if (Dm.truthy(restcure) && affected_mob.body_position == LYING_DOWN && SPT_PROB(10, seconds_per_tick))
            {
                to_chat(affected_mob, span_notice(I18n.T("You feel better.")));
                await cure();
                return FALSE;
            }
        }
        else if (stage == 3)
        {
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("Your entire body vibrates.")));
            if (SPT_PROB(19, seconds_per_tick))
            {
                float sw__1 = rand(1, 3);
                if (sw__1 == 1)
                    await scramble_dna(affected_mob, 1, 0, 0, rand(15, 45));
                else if (sw__1 == 2)
                    await scramble_dna(affected_mob, 0, 1, 0, rand(15, 45));
                else if (sw__1 == 3)
                    await scramble_dna(affected_mob, 0, 0, 1, rand(15, 45));
            }
            if (Dm.truthy(restcure) && affected_mob.body_position == LYING_DOWN && SPT_PROB(10, seconds_per_tick))
            {
                to_chat(affected_mob, span_notice(I18n.T("You feel better.")));
                await cure();
                return FALSE;
            }
        }
        else if (stage == 4)
        {
            if (SPT_PROB(37, seconds_per_tick))
            {
                float sw__2 = rand(1, 3);
                if (sw__2 == 1)
                    await scramble_dna(affected_mob, 1, 0, 0, rand(50, 75));
                else if (sw__2 == 2)
                    await scramble_dna(affected_mob, 0, 1, 0, rand(50, 75));
                else if (sw__2 == 3)
                    await scramble_dna(affected_mob, 0, 0, 1, rand(50, 75));
            }
            if (Dm.truthy(restcure) && affected_mob.body_position == LYING_DOWN && SPT_PROB(2.5f, seconds_per_tick))
            {
                to_chat(affected_mob, span_notice(I18n.T("You feel better.")));
                await cure();
                return FALSE;
            }
        }
        return dot;
    }
}

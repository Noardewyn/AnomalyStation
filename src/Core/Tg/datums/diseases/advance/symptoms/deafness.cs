// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/deafness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/deafness.dm:9
public partial class datum_symptom_deafness : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/deafness.dm:30
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 4)
            suppress_warning = TRUE;
        if (Dm.num(A.totalResistance()) >= 9) //permanent deafness
            causes_permanent_deafness = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/deafness.dm:39
    [DmParams("advanced_disease")]
    public override async DmTask<object> End(datum_disease_advance advanced_disease = null)
    {
        REMOVE_TRAIT(advanced_disease.affected_mob, TRAIT_DEAF, DISEASE_TRAIT);
        return await base.End(advanced_disease);
    }

    // tg: code/datums/diseases/advance/symptoms/deafness.dm:43
    public override async DmTask<object> Activate(datum_disease_advance advanced_disease = null)
    {
        object dot = null;
        dot = await base.Activate(advanced_disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon infected_mob = advanced_disease.affected_mob;
        if (advanced_disease.stage == 3 || advanced_disease.stage == 4)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                to_chat(infected_mob, span_warning(I18n.T(Dm.str(pick(Dm.list("You hear a ringing in your ear.", "Your ears pop."))))));
        }
        else if (advanced_disease.stage == 5)
        {
            if (Dm.truthy(causes_permanent_deafness))
            {
                if (!HAS_TRAIT_FROM(infected_mob, TRAIT_DEAF, DISEASE_TRAIT))
                {
                    to_chat(infected_mob, span_userdanger(I18n.T("Your ears pop painfully and start bleeding!")));
                    // Just absolutely murder me man
                    await infected_mob.adjust_organ_loss(ORGAN_SLOT_EARS, INFINITY);
                    await infected_mob.emote("scream");
                    ADD_TRAIT(infected_mob, TRAIT_DEAF, DISEASE_TRAIT);
                }
            }
            else
            {
                to_chat(infected_mob, span_userdanger(I18n.T("Your ears pop and begin ringing loudly!")));
                obj_item_organ_ears ears = infected_mob.get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;
                float deafness_to_add = min(40 * SECONDS - Dm.num(ears.temporary_deafness), 30 * SECONDS);
                if (deafness_to_add > 0)
                    ears.adjust_temporary_deafness(deafness_to_add);
            }
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/deafness.dm:69
    [DmParams("advanced_disease")]
    public override object on_stage_change(datum_disease_advance advanced_disease = null)
    {
        object dot = null;
        dot = base.on_stage_change(advanced_disease);
        if (!Dm.truthy(dot))
            return FALSE;
        mob_living_carbon infected_mob = advanced_disease.affected_mob;
        if (advanced_disease.stage < 5 || !Dm.truthy(causes_permanent_deafness))
            REMOVE_TRAIT(infected_mob, TRAIT_DEAF, DISEASE_TRAIT);
        return TRUE;
    }
}

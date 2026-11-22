// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/shedding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/*Alopecia
 * No change to stealth
 * Slight increase to resistance
 * Increases stage speed
 * Increases transmissibility
 * Near critcal level
 * Bonus: Makes the mob lose hair.
*/
// tg: code/datums/diseases/advance/symptoms/shedding.dm:9
public partial class datum_symptom_shedding : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/shedding.dm:24
    [DmParams("disease")]
    public override async DmTask<object> Activate(datum_disease_advance disease = null)
    {
        object dot = null;
        dot = await base.Activate(disease);
        if (!Dm.truthy(dot))
            return dot;

        mob_living affected_living = disease.affected_mob;
        if (prob(base_message_chance))
            to_chat(affected_living, span_warning(I18n.T(Dm.str(pick(Dm.list("Your scalp itches.", "Your skin feels flaky."))))));
        if (ishuman(affected_living))
        {
            mob_living_carbon_human affected_human = (mob_living_carbon_human)affected_living;
            if (disease.stage == 3 || disease.stage == 4)
            {
                if ((Dm.eq(affected_human.hairstyle, "Bald")) && (!Dm.eq(affected_human.hairstyle, "Balding Hair")))
                {
                    to_chat(affected_human, span_warning(I18n.T("Your hair starts to fall out in clumps...")));
                    _addtimer(Dm.@new<datum_callback>(this, nameof(baldify), affected_human, FALSE), 5 * SECONDS, file: "code/datums/diseases/advance/symptoms/shedding.dm", line: 41);
                }
            }
            else if (disease.stage == 5)
            {
                if ((!Dm.eq(affected_human.facial_hairstyle, "Shaved")) || (!Dm.eq(affected_human.hairstyle, "Bald")))
                {
                    if (Dm.eq(affected_human.hairstyle, "Balding Hair"))
                        to_chat(affected_human, span_warning(I18n.T("The little hair you have left starts to fall out in clumps...")));
                    else
                        to_chat(affected_human, span_warning(I18n.T("Your hair starts to fall out in clumps...")));
                    _addtimer(Dm.@new<datum_callback>(this, nameof(baldify), affected_human, TRUE), 3 * SECONDS, file: "code/datums/diseases/advance/symptoms/shedding.dm", line: 49);
                }
            }
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/shedding.dm:47
    public virtual void baldify(mob_living_carbon_human baldie = null, object fully_bald = null)
    {
        if (Dm.truthy(fully_bald))
        {
            baldie.set_facial_hairstyle("Shaved", update: FALSE);
            baldie.set_hairstyle("Bald"); //this will call update_body_parts()
        }
        else
            baldie.set_hairstyle("Balding Hair");
    }
}

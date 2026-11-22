// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/dizzy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/**Dizziness
 * Increases stealth
 * Lowers resistance
 * Decreases stage speed considerably
 * Slightly reduces transmissibility
 * Intense Level
 * Bonus: Shakes the affected mob's screen for short periods.
 */
// tg: code/datums/diseases/advance/symptoms/dizzy.dm:10
public partial class datum_symptom_dizzy : datum_symptom // Not the egg
{
    // tg: code/datums/diseases/advance/symptoms/dizzy.dm:30
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 4)
            suppress_warning = TRUE;
        if (Dm.num(A.totalTransmittable()) >= 6) //druggy
            power = 2;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/dizzy.dm:39
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (A.stage == 1 || A.stage == 2 || A.stage == 3 || A.stage == 4)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel dizzy.", "Your head spins."))))));
        }
        else
        {
            to_chat(M, span_userdanger(I18n.T("A wave of dizziness washes over you!")));
            await M.adjust_dizzy_up_to(40 * SECONDS, 80 * SECONDS);
            if (Dm.num(power) >= 2)
                await M.set_drugginess(60 * SECONDS);
        }
        return dot;
    }
}

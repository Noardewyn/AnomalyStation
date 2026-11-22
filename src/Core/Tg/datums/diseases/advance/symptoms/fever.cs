// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/fever.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/fever.dm:11
public partial class datum_symptom_fever : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/fever.dm:1
    private const string FEVER_CHANGE = "fever";

    // tg: code/datums/diseases/advance/symptoms/fever.dm:30
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalResistance()) >= 5) //dangerous fever
        {
            power = 1.5f;
            @unsafe = TRUE;
        }
        if (Dm.num(A.totalResistance()) >= 10)
            power = 2.5f;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/fever.dm:40
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon M = A.affected_mob;
        if (!Dm.truthy(@unsafe) || A.stage < 4)
            to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel hot.", "You feel like you're burning."))))));
        else
            to_chat(M, span_userdanger(I18n.T(Dm.str(pick(Dm.list("You feel too hot.", "You feel like your blood is boiling."))))));
        set_body_temp(A.affected_mob, A);
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/fever.dm:59
    public virtual void set_body_temp(mob_living M = null, datum_disease_advance A = null)
    {
        if (Dm.truthy(@unsafe)) // when unsafe the fever can cause heat damage
            M.add_body_temperature_change(FEVER_CHANGE, 6 * Dm.num(power) * A.stage);
        else
        {
            // Get the max amount of change allowed before going over heat damage limit, then cap the maximum allowed temperature change from a safe fever to 5 under the heat damage limit
            float change_limit = max(Dm.num(M.get_body_temp_heat_damage_limit()) - 5 - Dm.num(M.get_body_temp_normal(apply_change: FALSE)), 0);
            M.add_body_temperature_change(FEVER_CHANGE, min(6 * Dm.num(power) * A.stage, change_limit));
        }
    }

    /// Update the body temp change based on the new stage
    // tg: code/datums/diseases/advance/symptoms/fever.dm:68
    public override object on_stage_change(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.on_stage_change(A);
        if (Dm.truthy(dot))
            set_body_temp(A.affected_mob, A);
        return dot;
    }

    /// remove the body temp change when removing symptom
    // tg: code/datums/diseases/advance/symptoms/fever.dm:74
    public override async DmTask<object> End(datum_disease_advance A = null)
    {
        await DmTask.CompletedTask;
        mob_living_carbon M = A.affected_mob;
        if (M != null)
            M.remove_body_temperature_change(FEVER_CHANGE);
        return null;
    }
}

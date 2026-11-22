// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/chills.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/chills.dm:11
public partial class datum_symptom_chills : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/chills.dm:1
    private const string CHILLS_CHANGE = "chills";

    // tg: code/datums/diseases/advance/symptoms/chills.dm:29
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStageSpeed()) >= 5) //dangerous cold
        {
            power = 1.5f;
            @unsafe = TRUE;
        }
        if (Dm.num(A.totalStageSpeed()) >= 10)
            power = 2.5f;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/chills.dm:39
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon M = A.affected_mob;
        if (!Dm.truthy(@unsafe) || A.stage < 4)
            to_chat(M, span_warning(I18n.T(Dm.str(pick(Dm.list("You feel cold.", "You shiver."))))));
        else
            to_chat(M, span_userdanger(I18n.T(Dm.str(pick(Dm.list("You feel your blood run cold.", "You feel ice in your veins.", "You feel like you can't heat up.", "You shiver violently."))))));
        set_body_temp(A.affected_mob, A);
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/chills.dm:58
    public virtual void set_body_temp(mob_living M = null, datum_disease_advance A = null)
    {
        if (Dm.truthy(@unsafe)) // when unsafe the shivers can cause cold damage
            M.add_body_temperature_change(CHILLS_CHANGE, -6 * Dm.num(power) * A.stage);
        else
        {
            // Get the max amount of change allowed before going under cold damage limit, then cap the maximum allowed temperature change from safe chills to 5 over the cold damage limit
            float change_limit = min(Dm.num(M.get_body_temp_cold_damage_limit()) + 5 - Dm.num(M.get_body_temp_normal(apply_change: FALSE)), 0);
            M.add_body_temperature_change(CHILLS_CHANGE, max(-6 * Dm.num(power) * A.stage, change_limit));
        }
    }

    /// Update the body temp change based on the new stage
    // tg: code/datums/diseases/advance/symptoms/chills.dm:67
    public override object on_stage_change(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.on_stage_change(A);
        if (Dm.truthy(dot))
            set_body_temp(A.affected_mob, A);
        return dot;
    }

    /// remove the body temp change when removing symptom
    // tg: code/datums/diseases/advance/symptoms/chills.dm:73
    public override async DmTask<object> End(datum_disease_advance A = null)
    {
        await DmTask.CompletedTask;
        mob_living_carbon M = A.affected_mob;
        if (M != null)
            M.remove_body_temperature_change(CHILLS_CHANGE);
        return null;
    }
}

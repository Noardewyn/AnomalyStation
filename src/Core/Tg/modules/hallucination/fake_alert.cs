// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/fake_alert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. A fake alert is thrown on the hallucinator's own screen with the override flag, so the real alert logic
// cannot clear it until the hallucination does. The var blocks are generated (D111).

/// Fake alert hallucination. Causes a fake alert to be thrown to the hallucinator.
// tg: code/modules/hallucination/fake_alert.dm:2
public partial class datum_hallucination_fake_alert : datum_hallucination
{
    // tg: code/modules/hallucination/fake_alert.dm:17
    [DmParams("hallucinator", "duration")]
    public override async DmTask New(params object[] __extra)
    {
        object duration = Dm.arg(__extra, 1) ?? (15 * SECONDS);
        this.duration = duration;
        await base.New(__extra);
    }

    // tg: code/modules/hallucination/fake_alert.dm:21
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(del_timer_id))
            deltimer(del_timer_id);
        if (!QDELETED(hallucinator))
            hallucinator.clear_alert(alert_category, clear_override: TRUE);
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/fake_alert.dm:28
    public override async DmTask<object> start()
    {
        object picked_type = islist(alert_type) ? pick(alert_type as DmList) : alert_type;

        feedback_details += Dm.text("Alert type: [], Actual type: []", alert_category, alert_type);

        hallucinator.throw_alert(
            category: alert_category,
            type: picked_type,
            severity: optional_severity,
            @override: TRUE);

        del_timer_id = QDEL_IN_HALLUCINATION(this, Dm.num(duration), "code/modules/hallucination/fake_alert.dm", 38, stoppable: true);
        return TRUE;
    }
}

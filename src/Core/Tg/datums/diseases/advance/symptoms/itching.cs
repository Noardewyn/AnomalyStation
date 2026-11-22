// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/itching.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/*Itching
 * No effect to stealth
 * Greatly increases resistance
 * Greatly increases stage speed
 * Slightly increases transmissibility
 * Low level
 * Bonus: Displays an annoying message! Should be used for buffing your disease.
*/
// tg: code/datums/diseases/advance/symptoms/itching.dm:9
public partial class datum_symptom_itching : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/itching.dm:30
    [DmParams("active_disease")]
    public override object Start(datum_disease_advance active_disease = null)
    {
        object dot = null;
        dot = base.Start(active_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(active_disease.totalTransmittable()) >= 6) //itch more often
            symptom_delay = 2.5f;
        if (Dm.num(active_disease.totalStageSpeed()) >= 7) //scratch
            scratch = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/itching.dm:39
    [DmParams("active_disease")]
    public override async DmTask<object> Activate(datum_disease_advance active_disease = null)
    {
        object dot = null;
        dot = await base.Activate(active_disease);
        if (!Dm.truthy(dot))
            return dot;
        // COOLDOWN_FINISHED(src, itching_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        bool announce_scratch = (this.itching_cooldown <= world.time) || (!(this.itching_cooldown <= world.time) && prob(60) && !Dm.truthy(off_cooldown_scratched));
        if (!Dm.truthy(await active_disease.affected_mob.itch(silent: Dm.num(!announce_scratch), can_scratch: scratch)) || !announce_scratch)
            return dot;
        // COOLDOWN_START(src, itching_cooldown, 5 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        this.itching_cooldown = world.time + (5 * SECONDS);
        if (!Dm.truthy(off_cooldown_scratched) && !(this.itching_cooldown <= world.time))
            off_cooldown_scratched = TRUE;
        else
            off_cooldown_scratched = FALSE;
        return dot;
    }
}

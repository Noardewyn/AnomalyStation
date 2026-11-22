// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/damages/poisoned.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111).

/// Simulates being killed by poison
// tg: code/modules/lost_crew/damages/poisoned.dm:2
public partial class datum_corpse_damage_cause_of_death_poison : datum_corpse_damage_cause_of_death
{
    // tg: code/modules/lost_crew/damages/poisoned.dm:13
    public override async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        float reagents_to_add = round(min_reagents + (max_reagents - min_reagents) * Dm.num(severity));

        await ((datum_reagents)body.reagents).add_reagent(poison, reagents_to_add);
        // we can just abuse deltatime to instantly simulate a long exposure
        await ((datum_reagents)body.reagents).metabolize(body, 2 * metabolisations_per_unit * reagents_to_add, liverless: FALSE, dead: FALSE);
        return null;
    }
}

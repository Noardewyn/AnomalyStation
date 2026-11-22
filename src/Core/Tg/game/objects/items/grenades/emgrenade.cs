// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/emgrenade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/objects/items/grenades/emgrenade.dm:1
public partial class obj_item_grenade_empgrenade : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/emgrenade.dm:7
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;
        await update_mob();
        await empulse(this, 4, 10, emp_source: this);
        qdel(this);
        return dot;
    }
}

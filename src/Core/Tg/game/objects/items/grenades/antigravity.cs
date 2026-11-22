// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/antigravity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the owner, 2026-11-18). The var block is generated (D111).

// tg: code/game/objects/items/grenades/antigravity.dm:2
public partial class obj_item_grenade_antigravity : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/antigravity.dm:10
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();

        foreach (turf lanced_turf in Dm.each<turf>(view(range, this)))
        {
            await lanced_turf.AddElement(typeof(datum_element_forced_gravity), forced_value);
            _addtimer(Dm.@new<datum_callback>(lanced_turf, nameof(datum._RemoveElement), Dm.list(typeof(datum_element_forced_gravity), forced_value)), duration, file: "code/game/objects/items/grenades/antigravity.dm", line: 19);
        }

        qdel(this);
        return dot;
    }
}

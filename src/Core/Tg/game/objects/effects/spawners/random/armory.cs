// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/armory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 I1d, the map-preview spawners): only the procs that kept Meta's spawners inert, so they spawn their loot and
// delete themselves as tg's do (tg: "If you see this while in a shift, please create a bug report"). The var blocks are generated.

// tg: code/game/objects/effects/spawners/random/armory.dm:102
public partial class obj_effect_spawner_random_armory_dragnet : obj_effect_spawner_random_armory
{
    // tg: code/game/objects/effects/spawners/random/armory.dm:108
    public override async DmTask spawn_loot(object lootcount_override = null)
    {
        await base.spawn_loot(lootcount_override);
        await Dm.new_async(typeof(obj_item_dragnet_beacon), get_turf(this)); //And give them a beacon too!
    }
}

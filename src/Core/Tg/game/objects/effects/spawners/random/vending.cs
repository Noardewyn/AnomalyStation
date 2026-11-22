// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/vending.dm
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

// tg: code/game/objects/effects/spawners/random/vending.dm:1
public partial class obj_effect_spawner_random_vending : obj_effect_spawner_random
{
    // tg: code/game/objects/effects/spawners/random/vending.dm:7
    public override async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        object vending = await base.make_item(spawn_loc, type_path_to_make);
        if (vending is obj_machinery_vending made)
            made.extended_inventory = hacked;

        return vending;
    }
}

// tg: code/game/objects/effects/spawners/random/vending.dm:14
public partial class obj_effect_spawner_random_vending_snackvend : obj_effect_spawner_random_vending
{
    // tg: code/game/objects/effects/spawners/random/vending.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(check_holidays(HOTDOG_DAY)))
            Dm.append(ref loot, typeof(obj_machinery_vending_hotdog));
        return base.Initialize(mapload, __extra);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/food_or_drink.dm
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

// tg: code/game/objects/effects/spawners/random/food_or_drink.dm:315
public partial class obj_effect_spawner_random_food_or_drink_cake_ingredients : obj_effect_spawner_random_food_or_drink
{
    // tg: code/game/objects/effects/spawners/random/food_or_drink.dm:326
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // SSstation is an inert stub until M10 (station traits): no birthday, so nothing spawns, as on a shift without the trait
        if (!HAS_TRAIT(SSstation, STATION_TRAIT_BIRTHDAY))
            spawn_loot_chance = 0;
        return base.Initialize(mapload, __extra);
    }
}

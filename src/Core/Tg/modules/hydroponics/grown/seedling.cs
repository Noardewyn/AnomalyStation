// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/seedling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/seedling.dm:2
public partial class obj_item_seeds_seedling : obj_item_seeds
{
    // tg: code/modules/hydroponics/grown/seedling.dm:18
    public override async DmTask<object> harvest(mob user = null)
    {
        mob harvester = user;
        obj_machinery_hydroponics parent = loc as obj_machinery_hydroponics;
        DmList grow_locations = get_adjacent_open_turfs(parent) as DmList;
        turf final_location = Dm.truthy(length(grow_locations)) ? pick(grow_locations) as turf : get_turf(parent);
        mob_living_basic_seedling seed_pet = Dm.@new(product, final_location) as mob_living_basic_seedling;
        await seed_pet.befriend(harvester as mob_living);
        await parent.update_tray(user: harvester, product_count: 1);
        return null;
    }
}

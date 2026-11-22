// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/weeds/starthistle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/weeds/starthistle.dm:3
public partial class obj_item_seeds_starthistle : obj_item_seeds
{
    // tg: code/modules/hydroponics/grown/weeds/starthistle.dm:22
    public override async DmTask<object> harvest(mob user = null)
    {
        obj_machinery_hydroponics parent = loc as obj_machinery_hydroponics;
        float seed_count = yield;
        if (prob(Dm.num(getYield()) * 20))
        {
            seed_count++;
            atom output_loc = (Dm.truthy(await parent.Adjacent(user)) ? user.loc : parent.loc) as atom;
            for (float i = 1; i <= seed_count; i++)
            {
                obj_item_seeds_starthistle harvestseeds = Copy() as obj_item_seeds_starthistle;
                await harvestseeds.forceMove(output_loc);
            }
        }

        await parent.update_tray(user, seed_count);
        return null;
    }
}

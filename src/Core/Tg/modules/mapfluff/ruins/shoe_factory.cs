// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/shoe_factory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the shoe factory's loot, five pairs of shoes each given a random speed modifier. The area and the notices are
// data (their var blocks are generated).

// tg: code/modules/mapfluff/ruins/shoe_factory.dm:4
public partial class obj_effect_spawner_random_mining_loot_shoe_factory : obj_effect_spawner_random_mining_loot
{
    // tg: code/modules/mapfluff/ruins/shoe_factory.dm:15
    public override async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        object shoe = await base.make_item(spawn_loc, type_path_to_make);
        if (shoe is obj_item_clothing_shoes shoes)
        {
            // shoes will spawn anywhere from -0.5 speed modifer (fast) to +0.5 speed (slow)
            float dice_result = Dm.roll("5d3");
            float slowdown_modifier = 1 - (dice_result * 0.1f);
            shoes.slowdown += slowdown_modifier;
        }
        return shoe;
    }
}

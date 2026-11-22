// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/bear/bear_ai_behavior.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Bears take the honeycombs out of the hives they find
// tg: code/modules/mob/living/basic/space_fauna/bear/bear_ai_behavior.dm:1
public partial class datum_bt_node_ai_behavior_hunt_target_find_hive : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/modules/mob/living/basic/space_fauna/bear/bear_ai_behavior.dm:4
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hive_target = null)
    {
        (hunter as mob_living_basic_bear).extract_combs(hive_target as obj_structure_beebox);
        return null;
    }
}

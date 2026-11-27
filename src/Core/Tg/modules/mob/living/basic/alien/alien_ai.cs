// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/alien/alien_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the basic aliens' behaviour tree leaves (the controllers and subtrees are var-only and load their .bt.json files).

/// Plants alien weeds on the pawn's current turf. Fails if the pawn can't plant or weeds couldn't be placed.
// tg: code/modules/mob/living/basic/alien/alien_ai.dm:42
public partial class datum_bt_node_ai_behavior_plant_alien_weeds : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/alien/alien_ai.dm:44
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_alien alien_pawn = controller.pawn as mob_living_basic_alien;
        if (!Dm.truthy(alien_pawn.can_plant_weeds) || !Dm.truthy(alien_pawn.place_weeds()))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
    }
}

/// Lays an alien egg on the pawn's current turf. Fails if the pawn can't lay eggs or an egg couldn't be placed.
// tg: code/modules/mob/living/basic/alien/alien_ai.dm:51
public partial class datum_bt_node_ai_behavior_lay_alien_egg : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/alien/alien_ai.dm:53
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_alien alien_pawn = controller.pawn as mob_living_basic_alien;
        if (!Dm.truthy(alien_pawn.can_lay_eggs) || !Dm.truthy(alien_pawn.lay_alien_egg()))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
    }
}

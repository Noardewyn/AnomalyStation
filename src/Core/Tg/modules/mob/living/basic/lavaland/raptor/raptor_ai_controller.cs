// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/raptor/raptor_ai_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The aggressive and baby controllers are var blocks (generated); the trees are tg's compiled .bt.json (BehaviorTrees/).

// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_ai_controller.dm:1
public partial class datum_ai_controller_basic_controller_raptor : datum_ai_controller_basic_controller
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_ai_controller.dm:40
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        RegisterSignal(new_pawn, COMSIG_AI_BLACKBOARD_KEY_SET(BB_RAPTOR_COWARD), nameof(on_cowardly_set));
        RegisterSignal(new_pawn, COMSIG_AI_BLACKBOARD_KEY_CLEARED(BB_RAPTOR_COWARD), nameof(on_cowardly_clear));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_ai_controller.dm:47
    [SignalHandler]
    public virtual void on_cowardly_set(datum source = null)
    {
        ADD_TRAIT(pawn, TRAIT_MOB_DIFFICULT_TO_MOUNT, REF(this));
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_ai_controller.dm:51
    [SignalHandler]
    public virtual void on_cowardly_clear(datum source = null)
    {
        REMOVE_TRAIT(pawn, TRAIT_MOB_DIFFICULT_TO_MOUNT, REF(this));
    }
}

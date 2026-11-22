// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/grab_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the EVLOG lines (the admin event logger, as movement_types.cs) (gaps package C1).

/**
 * Grabs (starts pulling) the atom at the given blackboard key.
 * Succeeds immediately if already pulling the target.
 * Fails if the target is anchored
 */
// tg: code/datums/ai/generic_behaviors/grab_target.dm:6
public partial class datum_bt_node_ai_behavior_grab_target : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/grab_target.dm:10
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        atom_movable target = controller.blackboard[target_key] as atom_movable;
        if (QDELETED(target) || Dm.truthy(target.anchored))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living our_mob = controller.pawn as mob_living;
        if (our_mob.pulling == target)
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;

        return start_async();
    }

    // tg: code/datums/ai/generic_behaviors/grab_target.dm:25
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        mob_living our_mob = controller.pawn as mob_living;
        atom_movable target = controller.blackboard[target_key] as atom_movable;
        object result = await our_mob.start_pulling(target);
        if (!Dm.truthy(async_still_valid()))
            return null;
        finish_async(Dm.truthy(result) ? AI_BEHAVIOR_SUCCEEDED : AI_BEHAVIOR_FAILED);
        return null;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/stuff_in_disposal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5; the stuffing itself since the disposal system's port, D325).

/// Grabs a downed target mob and stuffs them into a nearby disposal unit.
// tg: code/datums/ai/generic_behaviors/stuff_in_disposal.dm:2
public partial class datum_bt_node_ai_behavior_stuff_in_disposal : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/stuff_in_disposal.dm:9
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        mob_living target = controller.blackboard[attack_target_key] as mob_living;
        obj_machinery_disposal disposal = controller.blackboard[disposal_target_key] as obj_machinery_disposal;
        mob_living living_pawn = controller.pawn as mob_living;
        if (QDELETED(target) || QDELETED(disposal))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (!Dm.truthy(Waiting.NoSleep(living_pawn.Adjacent(disposal))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        return start_async();
    }

    // tg: code/datums/ai/generic_behaviors/stuff_in_disposal.dm:23
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        mob_living target = controller.blackboard[attack_target_key] as mob_living;
        obj_machinery_disposal disposal = controller.blackboard[disposal_target_key] as obj_machinery_disposal;
        mob_living living_pawn = controller.pawn as mob_living;
        object stuffed = await disposal.stuff_mob_in(target, living_pawn);
        if (!Dm.truthy(async_still_valid()))
            return null;
        if (Dm.truthy(stuffed) && !QDELETED(disposal))
            await disposal.flush__proc();
        finish_async(AI_BEHAVIOR_SUCCEEDED);
        return null;
    }

    // tg: code/datums/ai/generic_behaviors/stuff_in_disposal.dm:34
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(attack_target_key);
        controller.clear_blackboard_key(disposal_target_key);
    }
}

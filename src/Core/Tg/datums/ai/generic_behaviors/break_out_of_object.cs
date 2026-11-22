// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/break_out_of_object.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Attacks an escape target until the pawn is no longer buckled to or contained by it.
// tg: code/datums/ai/generic_behaviors/break_out_of_object.dm:2
public partial class datum_bt_node_ai_behavior_break_out_of_object : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/break_out_of_object.dm:7
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        if (!Dm.truthy(await should_attack_target(controller, target_atom)))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/ai/generic_behaviors/break_out_of_object.dm:12
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(should_attack_target(controller, target_atom))))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        Waiting.Detach(controller.ai_interact(target_atom, TRUE));
        return AI_BEHAVIOR_DELAY;
    }

    // tg: code/datums/ai/generic_behaviors/break_out_of_object.dm:18
    public virtual async DmTask<object> should_attack_target(datum_ai_controller controller = null, atom target = null)
    {
        if (QDELETED(target))
            return FALSE;
        mob_living pawn = controller.pawn as mob_living;
        if (!Dm.truthy(await target.IsReachableBy(pawn)))
            return FALSE;
        return Dm.num(pawn.loc == target || pawn.buckled == target);
    }
}

/// Variant that reads the escape target from a blackboard key instead of a direct reference.
// tg: code/datums/ai/generic_behaviors/break_out_of_object.dm:27
public partial class datum_bt_node_ai_behavior_break_out_of_object_from_bb : datum_bt_node_ai_behavior_break_out_of_object
{
    // tg: code/datums/ai/generic_behaviors/break_out_of_object.dm:31
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        return await should_attack_target(controller, target);
    }

    // tg: code/datums/ai/generic_behaviors/break_out_of_object.dm:35
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (!Dm.truthy(Waiting.NoSleep(should_attack_target(controller, target))))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        Waiting.Detach(controller.ai_interact(target, TRUE));
        return AI_BEHAVIOR_DELAY;
    }
}

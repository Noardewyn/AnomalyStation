// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/stop_and_stare.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Faces a nearby scary atom and holds still for a while.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/stop_and_stare.dm:2
public partial class datum_bt_node_ai_behavior_stop_and_stare : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/stop_and_stare.dm:8
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        atom_movable target = controller.blackboard[target_key] as atom_movable;
        return Dm.num(target != null && isturf(target.loc) && ismob(controller.pawn));
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/stop_and_stare.dm:12
    public override object get_cooldown(datum_ai_controller cooldown_for = null)
    {
        object duration = cooldown_for.blackboard[stare_duration_key];
        return Dm.truthy(duration) ? duration : base.get_cooldown(cooldown_for);
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/stop_and_stare.dm:15
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom_movable target = controller.blackboard[target_key] as atom_movable;
        if (target == null || !isturf(target.loc))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        mob pawn_mob = controller.pawn as mob;
        pawn_mob.face_atom(target);
        pawn_mob.balloon_alert_to_viewers(I18n.T("stops and stares..."));
        // Returning a long cooldown keeps this leaf RUNNING (and thus the mob standing still) for the stare.
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/stop_and_stare.dm:26
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        // Forget the cause so we can be spooked fresh next time it wanders into view.
        controller.clear_blackboard_key(target_key);
    }
}

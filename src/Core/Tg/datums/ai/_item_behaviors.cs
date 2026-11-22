// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/_item_behaviors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The var block is generated.

///yeet yourself at a thing
// tg: code/datums/ai/_item_behaviors.dm:2
public partial class datum_bt_node_ai_behavior_throw_attack : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/_item_behaviors.dm:12
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        obj_item item_pawn = controller.pawn as obj_item;
        atom throw_target = controller.blackboard[target_key] as atom;
        if (QDELETED(throw_target))
        {
            controller.clear_blackboard_key(target_key);
            controller.set_blackboard_key(throw_count_key, 0);
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
        }
        item_pawn.visible_message(span_warning(I18n.T("[] hurls towards []!", item_pawn, throw_target)));
        Waiting.NoSleep(item_pawn.throw_at(throw_target, rand(4, 5), 9));
        playsound(item_pawn.loc, attack_sound, 100, TRUE);
        controller.add_blackboard_key(throw_count_key, 1);
        if (Dm.num(controller.blackboard[throw_count_key]) >= max_attempts)
            return on_throws_exhausted(controller, throw_target, target_key, throw_count_key);
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
    }

    /// Clears target and resets throw count. Override to add extra on-exhaust logic.
    // tg: code/datums/ai/_item_behaviors.dm:28
    public virtual object on_throws_exhausted(datum_ai_controller controller = null, atom throw_target = null, object target_key = null, object throw_count_key = null)
    {
        controller.clear_blackboard_key(target_key);
        controller.set_blackboard_key(throw_count_key, 0);
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
    }
}

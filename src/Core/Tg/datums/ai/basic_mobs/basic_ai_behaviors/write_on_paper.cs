// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/write_on_paper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298: the paper wizard scrawls on blank paper). The var block is generated.

/// Scrawls a random line from the writing list onto the carried paper, then drops it. Clears the carry key on finish.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/write_on_paper.dm:2
public partial class datum_bt_node_ai_behavior_write_on_paper : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/write_on_paper.dm:8
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living wizard = controller.pawn as mob_living;
        obj_item_paper target = controller.blackboard[paper_key] as obj_item_paper;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        DmList writing_list = controller.blackboard[writing_list_key] as DmList;
        if (Dm.truthy(length(writing_list)))
        {
            target.add_raw_text(pick(writing_list));
            target.update_appearance();
        }
        if (target.loc == wizard)
            Waiting.NoSleep(target.forceMove(get_turf(wizard)));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/write_on_paper.dm:22
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(paper_key);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/run_emote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

// tg: code/datums/ai/generic_behaviors/run_emote.dm:1
public partial class datum_bt_node_ai_behavior_run_emote : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/run_emote.dm:5
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (controller.pawn is not mob_living living_pawn)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        object emote_list = controller.blackboard[emote_key];
        object emote;
        if (emote_list is DmList list)
            emote = Dm.truthy(length(list)) ? pick(list) : null;
        else
            emote = emote_list;

        if (emote == null)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        Waiting.Detach(living_pawn.emote(emote));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

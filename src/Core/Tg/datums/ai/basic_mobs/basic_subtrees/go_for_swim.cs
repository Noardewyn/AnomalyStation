// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_subtrees/go_for_swim.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Splashes about while standing in water.
// tg: code/datums/ai/basic_mobs/basic_subtrees/go_for_swim.dm:6
public partial class datum_bt_node_ai_behavior_swim_splash : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_subtrees/go_for_swim.dm:8
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        if (living_pawn == null || !iswaterturf(get_turf(living_pawn)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        if (!SPT_PROB(5, seconds_per_tick))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;
        living_pawn.manual_emote(I18n.T("splashes water all around!"));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}

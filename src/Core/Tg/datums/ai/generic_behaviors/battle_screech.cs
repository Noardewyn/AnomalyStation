// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/battle_screech.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Emotes a random screech from a list of screeches defined on the subtype.
// tg: code/datums/ai/generic_behaviors/battle_screech.dm:2
public partial class datum_bt_node_ai_behavior_battle_screech : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/battle_screech.dm:6
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        Waiting.Detach(living_pawn.emote(pick(screeches)));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

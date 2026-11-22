// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/gym/weight_machine_action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole, the weight machine's Work out action button. The var block is generated (D111).

/**
 * The action button given by the weight machine's buckle.
 * This allows users to manually trigger working out.
 */
// tg: code/game/objects/structures/gym/weight_machine_action.dm:5
public partial class datum_action_push_weights : datum_action
{
    // tg: code/game/objects/structures/gym/weight_machine_action.dm:13
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        if (Dm.truthy(DOING_INTERACTION(owner, weightpress)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/structures/gym/weight_machine_action.dm:18
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return FALSE;
        await weightpress.perform_workout(owner as mob_living);
        return dot;
    }
}

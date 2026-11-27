// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/items/vortex_recall.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/datums/actions/items/vortex_recall.dm:1
public partial class datum_action_item_action_vortex_recall : datum_action_item_action
{
    // tg: code/datums/actions/items/vortex_recall.dm:7
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        if (target is not obj_item_hierophant_club teleport_stick)
            return null;
        if (Dm.truthy(teleport_stick.teleporting))
            return FALSE;
        if (teleport_stick.beacon != null && !Dm.truthy(check_teleport_valid(owner, get_turf(teleport_stick.beacon), TELEPORT_CHANNEL_FREE)))
            return FALSE;
        return await base.IsAvailable(feedback);
    }
}

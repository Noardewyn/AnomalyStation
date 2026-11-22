// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/license_plates.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps LC1: the whole file (the filled plates' picture by the stack's size, one of six plates; the var blocks are generated).

// tg: code/game/objects/items/stacks/license_plates.dm:20
public partial class obj_item_stack_license_plates_filled : obj_item_stack_license_plates
{
    ///Override to allow for variations
    // tg: code/game/objects/items/stacks/license_plates.dm:28
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(novariants))
            return dot;
        if (amount <= (max_amount * (1f / 3)))
        {
            icon_state = Dm.text("filled_plate_[]_1", rand(1, 6));
            return dot;
        }
        if (amount <= (max_amount * (2f / 3)))
        {
            icon_state = Dm.text("filled_plate_[]_2", rand(1, 6));
            return dot;
        }
        icon_state = Dm.text("filled_plate_[]_3", rand(1, 6));
        return dot;
    }
}

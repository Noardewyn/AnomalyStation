// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/tickets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SK1 (D333): the file whole: the arcade tickets, drawn as a bigger pile the more there are. The var block is generated
// (D111).

// tg: code/game/objects/items/stacks/tickets.dm:1
public partial class obj_item_stack_arcadeticket : obj_item_stack
{
    // tg: code/game/objects/items/stacks/tickets.dm:11
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        float n = Dm.num(get_amount());
        if (n >= 12)
            icon_state = "arcade-ticket_4";
        else if (n >= 6 && n <= 12)
            icon_state = "arcade-ticket_3";
        else if (n >= 2 && n <= 6)
            icon_state = "arcade-ticket_2";
        else
            icon_state = "arcade-ticket";
        return dot;
    }
}

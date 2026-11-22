// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/ammonia_crystals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SK1 (D333): the file whole: the ammonia crystals ground into ammonia. The var block is generated (D111).

// tg: code/game/objects/items/stacks/ammonia_crystals.dm:1
public partial class obj_item_stack_ammonia_crystals : obj_item_stack
{
    // tg: code/game/objects/items/stacks/ammonia_crystals.dm:12
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_ammonia), 10));
    }
}

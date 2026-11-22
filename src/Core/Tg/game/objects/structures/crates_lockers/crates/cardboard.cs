// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates/cardboard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST3 (D333): the file whole, the cardboard crates (the plain, Mothic and Tiziran boxes): an open box keeps its closed body's
// state, its flaps drawn by the lid overlay (`lid_icon_state`). The var blocks are generated (D111).

// tg: code/game/objects/structures/crates_lockers/crates/cardboard.dm:1
public partial class obj_structure_closet_crate_cardboard : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/cardboard.dm:30
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(opened))
            icon_state = Dm.text("[]", base_icon_state);
        return dot;
    }
}

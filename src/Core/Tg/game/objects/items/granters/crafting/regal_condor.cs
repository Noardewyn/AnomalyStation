// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/crafting/regal_condor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The var block is generated (D111).

// tg: code/game/objects/items/granters/crafting/regal_condor.dm:1
public partial class obj_item_book_granter_crafting_recipe_regal_condor : obj_item_book_granter_crafting_recipe
{
    // tg: code/game/objects/items/granters/crafting/regal_condor.dm:18
    public override async DmTask recoil(mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("The book turns to dust in your hands.")));
        qdel(this);
        await DmTask.CompletedTask;
    }
}

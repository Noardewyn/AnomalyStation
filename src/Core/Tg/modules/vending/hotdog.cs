// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/hotdog.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the file's one proc (the vendor's food comes handmade). The var blocks are generated.

// tg: code/modules/vending/hotdog.dm:3
public partial class obj_machinery_vending_hotdog : obj_machinery_vending
{
    /// Cute little thing that sets it apart from the other food vending mahicnes. I mean, you don't find this every day.
    // tg: code/modules/vending/hotdog.dm:54
    public override async DmTask on_dispense(obj_item vended_item = null, float? dispense_returned = null)
    {
        // Only apply to newly dispensed items
        if (Dm.truthy(dispense_returned))
            return;
        if (istype(vended_item, typeof(obj_item_food)))
            ADD_TRAIT(vended_item, TRAIT_HANDMADE, VENDING_MACHINE_TRAIT);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/blunt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 3: the wound types). The var block is generated (D111).

// tg: code/datums/wounds/blunt.dm:1
public partial class datum_wound_blunt : datum_wound
{
    // tg: code/datums/wounds/blunt.dm:7
    public override async DmTask wound_injury(datum_wound old_wound = null, object attack_direction = null)
    {
        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (old_wound == null && current_gauze != null && Dm.truthy(Dm.bitand(wound_flags, ACCEPTS_GAUZE)))
        {
            // oops your bone injury knocked off your gauze, gotta re-apply it
            await current_gauze.forceMove(limb.drop_location() as atom);
        }

        await base.wound_injury(old_wound, attack_direction);
    }
}

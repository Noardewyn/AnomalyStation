// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/costume.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The martial gi (syndicate X3b, D316: the sleeping carp kit's uniform, a carp style factor), but its spray can recolouring (the
// gags_recolorable element is not ported). Gaps CL1 (D333): the highlander's kilt that cannot be taken off and the mech suit (with
// its reskin menu). The var blocks are generated (D111).

// tg: code/modules/clothing/under/costume.dm:94
public partial class obj_item_clothing_under_costume_kilt_highlander : obj_item_clothing_under_costume_kilt
{
    // tg: code/modules/clothing/under/costume.dm:97
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, HIGHLANDER_TRAIT);
        return dot;
    }
}

// tg: code/modules/clothing/under/costume.dm:329
public partial class obj_item_clothing_under_costume_mech_suit : obj_item_clothing_under_costume
{
    // tg: code/modules/clothing/under/costume.dm:339
    public override async DmTask setup_reskins()
    {
        await AddComponent(typeof(datum_component_reskinable_item), typeof(datum_atom_skin_mech_suit));
    }
}

// tg: code/modules/clothing/under/costume.dm:492
public partial class obj_item_clothing_under_costume_gi : obj_item_clothing_under_costume
{
    // tg: code/modules/clothing/under/costume.dm:507
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/under/costume.dm:509", "Initialize: AddElement(/datum/element/gags_recolorable), the spray can recolouring (the element is not ported)");
        Waiting.NoSleep(update_icon(UPDATE_OVERLAYS));
        return dot;
    }
}

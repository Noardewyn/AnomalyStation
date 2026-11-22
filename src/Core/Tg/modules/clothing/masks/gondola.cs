// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/gondola.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the gondola mask's spurdo speech while worn). The var block is generated (D111).

// tg: code/modules/clothing/masks/gondola.dm:1
public partial class obj_item_clothing_mask_gondola : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/gondola.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", strings("spurdo_replacement.json", "spurdo")), Dm.named("slots", ITEM_SLOT_MASK)));
        return dot;
    }
}

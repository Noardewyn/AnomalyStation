// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/moustache.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/clothing/masks/moustache.dm:10
public partial class obj_item_clothing_mask_fakemoustache_italian : obj_item_clothing_mask_fakemoustache
{
    // tg: code/modules/clothing/masks/moustache.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod),
            Dm.named("replacements", strings("italian_replacement.json", "italian")),
            Dm.named("end_string", Dm.list(" Ravioli, ravioli, give me the formuoli!", " Mamma-mia!", " Mamma-mia! That's a spicy meat-ball!", " La la la la la funiculi funicula!")),
            Dm.named("end_string_chance", 3),
            Dm.named("slots", ITEM_SLOT_MASK)));
        return dot;
    }
}

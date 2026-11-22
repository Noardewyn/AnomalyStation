// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/frenchberet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/clothing/head/frenchberet.dm:1
public partial class obj_item_clothing_head_beret_frenchberet : obj_item_clothing_head_beret
{
    // tg: code/modules/clothing/head/frenchberet.dm:7
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod),
            Dm.named("replacements", strings("french_replacement.json", "french")),
            Dm.named("end_string", Dm.list(" Honh honh honh!", " Honh!", " Zut Alors!")),
            Dm.named("end_string_chance", 3),
            Dm.named("slots", ITEM_SLOT_HEAD)));
        return dot;
    }
}

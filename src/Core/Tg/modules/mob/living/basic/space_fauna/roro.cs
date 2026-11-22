// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/roro.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/roro.dm:1
public partial class mob_living_basic_roro : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/roro.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "warble"));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        return dot;
    }
}

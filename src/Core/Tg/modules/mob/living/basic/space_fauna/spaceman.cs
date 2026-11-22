// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/spaceman.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/spaceman.dm:1
public partial class mob_living_basic_spaceman : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/spaceman.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        return dot;
    }
}

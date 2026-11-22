// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/vermin/space_bat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/vermin/space_bat.dm:2
public partial class mob_living_basic_bat : mob_living_basic
{
    // tg: code/modules/mob/living/basic/vermin/space_bat.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        add_traits(Dm.list(TRAIT_SPACEWALK, TRAIT_VENTCRAWLER_ALWAYS, TRAIT_NO_MIRROR_REFLECTION), INNATE_TRAIT);
        return dot;
    }
}

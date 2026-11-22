// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/vermin/crab.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/vermin/crab.dm:3
public partial class mob_living_basic_crab : mob_living_basic
{
    // tg: code/modules/mob/living/basic/vermin/crab.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_NODROWN, TRAIT_SWIMMER, TRAIT_VENTCRAWLER_ALWAYS), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_sideway_movement)));
        Waiting.NoSleep(AddElement(typeof(datum_element_tiny_mob_hunter), MOB_SIZE_TINY));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_flee_while_injured)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), strings("crustacean_replacement.json", "crustacean")));
        return dot;
    }
}

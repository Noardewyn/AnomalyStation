// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/vermin/axolotl.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/vermin/axolotl.dm:2
public partial class mob_living_basic_axolotl : mob_living_basic
{
    // tg: code/modules/mob/living/basic/vermin/axolotl.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_NODROWN, TRAIT_SWIMMER, TRAIT_VENTCRAWLER_ALWAYS), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_AXOLOTL, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        return dot;
    }
}

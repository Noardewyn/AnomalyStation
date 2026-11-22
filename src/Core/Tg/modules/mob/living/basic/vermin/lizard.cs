// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/vermin/lizard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2): Wags-His-Tail; the lizard hunts butterflies and cockroaches, which stay inert until C4.

/*
* ## Lizards
*
* Green things that crawl around and eat bugs. Not to be confused with the human species lizardpersons, these are just small little fellas.
*/

// tg: code/modules/mob/living/basic/vermin/lizard.dm:8
public partial class mob_living_basic_lizard : mob_living_basic
{
    /// Typecache of things that we seek out to eat. Yummy.
    // tg: code/modules/mob/living/basic/vermin/lizard.dm:43
    private static DmList edibles => TgState.Current.mob_living_basic_lizard__edibles ??= typecacheof(Dm.list(typeof(mob_living_basic_butterfly), typeof(mob_living_basic_cockroach))) as DmList;

    // tg: code/modules/mob/living/basic/vermin/lizard.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "tongue"));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_LIZARD, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), 5f, null, null, null, null, null, edibles));
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_BASIC_FOODS, typecacheof(edibles));
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_lizard__edibles;
}

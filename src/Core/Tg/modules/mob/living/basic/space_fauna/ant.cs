// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/ant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/ant.dm:1
public partial class mob_living_basic_ant : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/ant.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "clack"));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_allergenic_attack), Dm.named("allergen", BUGS), Dm.named("allergen_chance", 20f), Dm.named("histamine_add", 5f)));
        return dot;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/snake/banded_snake.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/// Banded snakes come in two kinds, and you have to look closely to tell them apart
// tg: code/modules/mob/living/basic/space_fauna/snake/banded_snake.dm:1
public partial class mob_living_basic_snake_banded : mob_living_basic_snake
{
    // tg: code/modules/mob/living/basic/space_fauna/snake/banded_snake.dm:27
    [DmParams("mapload", "special_reagent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_swarming)));
        return base.Initialize(mapload, this.poison_reagent);
    }

    // tg: code/modules/mob/living/basic/space_fauna/snake/banded_snake.dm:32
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Dm.append(ref dot, span_notice("<i>You examine the bands on the snake very closely...</i>"));
        if (Dm.eq(this.poison_reagent, typeof(datum_reagent_consumable_milk)))
        {
            Dm.append(ref dot, span_info(Dm.text("[]", pick(this.rhymes_harmless))));
            Dm.append(ref dot, span_notice("This snake is not dangerous!"));
        }
        else
        {
            Dm.append(ref dot, span_info(Dm.text("[]", pick(this.rhymes_dangerous))));
            Dm.append(ref dot, span_notice("This snake is dangerous!"));
        }
        return dot;
    }
}

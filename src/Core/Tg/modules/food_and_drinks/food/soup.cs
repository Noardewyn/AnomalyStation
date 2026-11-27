// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/soup.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the bowled foods (no germs in a bowl) and the wish soup that sometimes comes true. The var blocks are generated.

// tg: code/modules/food_and_drinks/food/soup.dm:1
public partial class obj_item_food_bowled : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/soup.dm:11
    public override async DmTask make_germ_sensitive(object mapload = null)
    {
        await DmTask.CompletedTask;
        return; // It's in a bowl
    }
}

// tg: code/modules/food_and_drinks/food/soup.dm:14
public partial class obj_item_food_bowled_wish : obj_item_food_bowled
{
    // tg: code/modules/food_and_drinks/food/soup.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(25))
        {
            desc = "A wish come true!";
            datum_reagents reagents = (datum_reagents)this.reagents;
            Waiting.NoSleep(reagents.add_reagent(typeof(datum_reagent_consumable_nutriment), 9));
            Waiting.NoSleep(reagents.add_reagent(typeof(datum_reagent_consumable_nutriment_vitamin), 1));
        }
        return dot;
    }
}

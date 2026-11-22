// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/grass_carpet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/grass_carpet.dm:24
public partial class obj_item_food_grown_grass : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/grass_carpet.dm:33
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        to_chat(user, span_notice(I18n.T("You prepare the astroturf.")));
        float grassAmt = 1 + Dm.round(Dm.num(((obj_item_seeds)seed).potency) * tile_coefficient); // The grass we're holding
        foreach (obj_item_food_grown_grass G in Dm.each<obj_item_food_grown_grass>(user.loc)) // The grass on the floor
        {
            if (G.type != type)
                continue;
            grassAmt += 1 + Dm.round(Dm.num(((obj_item_seeds)G.seed).potency) * tile_coefficient);
            qdel(G);
        }
        Dm.@new(stacktype, user.drop_location(), grassAmt);
        qdel(this);
        return null;
    }
}

// Gaps H2 (D333): the kitchen links of the food (the live-partial rows S1 left for the kitchen).

public partial class obj_item_food_grown_grass : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/grass_carpet.dm:44
    public override async DmTask make_dryable()
    {
        await AddElement(typeof(datum_element_dryable), typeof(obj_item_stack_tile_hay));
    }
}

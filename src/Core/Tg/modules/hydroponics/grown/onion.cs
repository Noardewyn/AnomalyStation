// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/onion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable,
// make_dryable) translated (gaps package H2, D333); the rest of the file is generated or waits for its package.

public partial class obj_item_food_onion_slice : obj_item_food
{
    // tg: code/modules/hydroponics/grown/onion.dm:65
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), typeof(obj_item_food_onionrings), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE);
    }

    // tg: code/modules/hydroponics/grown/onion.dm:68
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), typeof(obj_item_food_onionrings));
    }
}

public partial class obj_item_food_grown_onion : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/onion.dm:29
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_onion_slice), 2, 15, Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_grown_onion_red : obj_item_food_grown_onion
{
    // tg: code/modules/hydroponics/grown/onion.dm:49
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_onion_slice_red), 2, 15, Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_grown_onion : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/onion.dm:52
    public override async DmTask UsedforProcessing(mob_living user = null, obj_item I = null, DmList chosen_option = null, DmList created_atoms = null)
    {
        turf splat_location = get_turf(this); //we need to set up the smoke beforehand
        await do_chem_smoke(0, this, splat_location, carry: (datum_reagents)reagents, silent: FALSE);
        await base.UsedforProcessing(user, I, chosen_option, created_atoms);
        return;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/frozen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ice cream holders of the space freezy, the sundae and the honkdae came with the kitchen (gaps H2, D333: the
// ice cream vat's readers); the snow cones' crafting and the popsicles' bites with the restaurant. The var blocks are generated.

// tg: code/modules/food_and_drinks/food/frozen.dm:33
public partial class obj_item_food_spacefreezy : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/frozen.dm:49
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponent(typeof(datum_component_ice_cream_holder));
    }
}

// tg: code/modules/food_and_drinks/food/frozen.dm:53
public partial class obj_item_food_sundae : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/frozen.dm:69
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponent(typeof(datum_component_ice_cream_holder), Dm.named("y_offset", -2), Dm.named("sweetener", typeof(datum_reagent_consumable_caramel)));
    }
}

// tg: code/modules/food_and_drinks/food/frozen.dm:73
public partial class obj_item_food_honkdae : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/frozen.dm:89
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponent(typeof(datum_component_ice_cream_holder), Dm.named("y_offset", -2)); //The sugar will react with the banana forming laughter. Honk!
    }
}

// tg: code/modules/food_and_drinks/food/frozen.dm:97
public partial class obj_item_food_snowcones : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/frozen.dm:113
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        datum_reagents reagents = (datum_reagents)this.reagents;
        // replaces the ice from the input with water
        await reagents.remove_reagent(typeof(datum_reagent_consumable_ice), 15);
        await reagents.add_reagent(typeof(datum_reagent_water), 11);
        // then add 1u nutriment for free
        await reagents.add_reagent(typeof(datum_reagent_consumable_nutriment), 1);
        // the juice component will be transferred in from crafting
    }
}

// tg: code/modules/food_and_drinks/food/frozen.dm:330
public partial class obj_item_food_popsicle : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/frozen.dm:354
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        bite_consumption = ((datum_reagents)reagents).total_volume / bite_states;
        Waiting.NoSleep(update_icon()); // make sure the popsicle overlay is primed so it's not just a stick until you start eating it
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/frozen.dm:359
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("after_eat", Dm.@new<datum_callback>(this, nameof(after_bite))));
    }

    // tg: code/modules/food_and_drinks/food/frozen.dm:363
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(bitecount))
        {
            Dm.append(ref dot, Dm.initial<obj_item_food_popsicle, string>(this, p => p.overlay_state));
            return dot;
        }
        Dm.append(ref dot, Dm.text("[]_[]", Dm.initial<obj_item_food_popsicle, string>(this, p => p.overlay_state), min(bitecount, 3)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/frozen.dm:370
    public virtual void after_bite(mob_living eater = null, mob_living feeder = null, object bitecount = null)
    {
        this.bitecount = Dm.num(bitecount);
        update_appearance();
    }
}

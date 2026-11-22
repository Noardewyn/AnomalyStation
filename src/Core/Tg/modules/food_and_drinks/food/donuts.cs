// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/donuts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333: the donuts, their sprinkles, the security officers' liking and the donut box's
// sprites). The var blocks are generated (D111).

// tg: code/modules/food_and_drinks/food/donuts.dm:3
public partial class obj_item_food_donut : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/donuts.dm:1  #define DONUT_SPRINKLE_CHANCE 30 (file-local)
    private const float DONUT_SPRINKLE_CHANCE = 30;

    // tg: code/modules/food_and_drinks/food/donuts.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), Dm.named("amount_per_dunk", 10)));
        if (prob(DONUT_SPRINKLE_CHANCE))
            Waiting.NoSleep(decorate_donut());
        return dot;
    }

    ///Override for checkliked callback
    // tg: code/modules/food_and_drinks/food/donuts.dm:37
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("check_liked", Dm.@new<datum_callback>(this, nameof(check_liked))));
    }

    // tg: code/modules/food_and_drinks/food/donuts.dm:41
    public virtual async DmTask<object> decorate_donut()
    {
        if (Dm.truthy(is_decorated) || !Dm.truthy(decorated_icon))
            return null;
        is_decorated = TRUE;
        name = I18n.T("[] []", decorated_adjective, name);
        icon_state = decorated_icon; //delish~!
        inhand_icon_state = "donut2";
        await ((datum_reagents)reagents).add_reagent(typeof(datum_reagent_consumable_sprinkles), 1);
        return TRUE;
    }

    /// Returns the sprite of the donut while in a donut box
    // tg: code/modules/food_and_drinks/food/donuts.dm:52
    public virtual object in_box_sprite()
    {
        return Dm.text("[]_inbox", icon_state);
    }

    ///Override for checkliked in edible component, because all cops LOVE donuts
    // tg: code/modules/food_and_drinks/food/donuts.dm:56
    public virtual object check_liked(mob_living_carbon_human consumer = null)
    {
        obj_item_organ_liver liver = consumer.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        if (!HAS_TRAIT(consumer, TRAIT_AGEUSIA) && liver != null && HAS_TRAIT(liver, TRAIT_LAW_ENFORCEMENT_METABOLISM))
            return FOOD_LIKED;
        return null;
    }
}

// It is so stupid that we have to do this but because food crafting clears all reagents that got added during init,
// here we are adding it again (but only for crafting, maploaded and spawned donuts work fine).
// Until the issues with crafted items' reagents are resolved this will have to do
// tg: code/modules/food_and_drinks/food/donuts.dm:62
public partial class obj_item_food_donut_plain : obj_item_food_donut
{
    // tg: code/modules/food_and_drinks/food/donuts.dm:31
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        if (Dm.truthy(is_decorated))
            await ((datum_reagents)reagents).add_reagent(typeof(datum_reagent_consumable_sprinkles), 1);
    }
}

// tg: code/modules/food_and_drinks/food/donuts.dm:65
public partial class obj_item_food_donut_chaos : obj_item_food_donut
{
    // tg: code/modules/food_and_drinks/food/donuts.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        extra_reagent = Dm.pick(
            typeof(datum_reagent_consumable_nutriment),
            typeof(datum_reagent_consumable_capsaicin),
            typeof(datum_reagent_consumable_frostoil),
            typeof(datum_reagent_drug_krokodil),
            typeof(datum_reagent_toxin_plasma),
            typeof(datum_reagent_consumable_coco),
            typeof(datum_reagent_toxin_slimejelly),
            typeof(datum_reagent_consumable_banana),
            typeof(datum_reagent_consumable_berryjuice),
            typeof(datum_reagent_medicine_omnizine)
        ) as Type;
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(extra_reagent, 3));
        return dot;
    }
}

//////////////////////JELLY DONUTS/////////////////////////

// tg: code/modules/food_and_drinks/food/donuts.dm:229
public partial class obj_item_food_donut_jelly : obj_item_food_donut
{
    // Jelly donuts don't have holes, but look the same on the outside
    // tg: code/modules/food_and_drinks/food/donuts.dm:244
    public override object in_box_sprite()
    {
        return Dm.text("[]_inbox", replacetext(icon_state, "jelly", "donut"));
    }

    // tg: code/modules/food_and_drinks/food/donuts.dm:247
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (extra_reagent != null)
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(extra_reagent, 3));
        return dot;
    }
}

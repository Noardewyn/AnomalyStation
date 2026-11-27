// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/sandwichtoast.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable, make_dryable)
// came with the kitchen (gaps H2, D333); the death sandwich (eaten right with gelled back hair and jorts, or its poisoning) with the
// restaurant. The var blocks are generated.

public partial class obj_item_food_sandwich_cheese : obj_item_food_sandwich
{
    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:32
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_sandwich_grilled_cheese), rand(30 * SECONDS, 60 * SECONDS), TRUE);
    }
}

// tg: code/modules/food_and_drinks/food/sandwichtoast.dm:261
public partial class obj_item_food_sandwich_death : obj_item_food_sandwich
{
    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:274  var/static/list/correct_clothing (a type-level static, rules 10.6)
    public static ref DmList correct_clothing => ref TgState.Current.obj_item_food_sandwich_death__correct_clothing;

    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:276
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_flags = Dm.bitand(obj_flags, Dm.bitnot(UNIQUE_RENAME)); // You shouldn't be able to disguise this on account of how it kills you
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:281
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        // Only human mobs, not animals or silicons, can like/dislike by this.
        if (!ishuman(user))
            return dot;
        if (Dm.eq(check_liked(user as mob_living_carbon_human), FOOD_LIKED))
            return dot;
        to_chat(user, span_warning(I18n.T("You imagine yourself eating []. You feel a sudden sour taste in your mouth, and a horrible feeling that you've done something wrong.", this)));
        ((mob_living)user).adjust_disgust(33);
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:292
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("after_eat", Dm.@new<datum_callback>(this, nameof(after_eat))), Dm.named("check_liked", Dm.@new<datum_callback>(this, nameof(check_liked))));
    }

    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:301
    public virtual object check_liked(mob_living_carbon_human consumer = null)
    {
        // Closest thing to a mullet we have
        if (Dm.eq(consumer.hairstyle, "Gelled Back") && Dm.truthy(is_type_in_list(consumer.get_item_by_slot(ITEM_SLOT_ICLOTHING) as datum, correct_clothing)))
            return FOOD_LIKED;
        return FOOD_ALLERGIC;
    }

    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:312
    public virtual async DmTask after_eat(mob_living_carbon_human consumer = null)
    {
        // If you like it, you're eating it right.
        if (Dm.eq(check_liked(consumer), FOOD_LIKED))
            return;
        // I thought it didn't make sense for it to instantly kill you, so instead enjoy shitloads of toxin damage per bite.
        balloon_alert(consumer, I18n.T("ate it wrong!"));
        await consumer.ForceContractDisease(Dm.@new<datum_disease_death_sandwich_poisoning>());
    }

    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:320
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] starts to shove [] down [] throat the wrong way. It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        qdel(this);
        await user.gib();
        return MANUAL_SUICIDE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/food/sandwichtoast.dm:274  /obj/item/food/sandwich/death's var/static/list/correct_clothing
    internal DmList obj_item_food_sandwich_death__correct_clothing = Dm.list(typeof(obj_item_clothing_under_rank_civilian_cookjorts), typeof(obj_item_clothing_under_shorts_jeanshorts));
}

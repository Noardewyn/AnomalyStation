// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/restaurant/generic_venues.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the restaurant (its customers order foods, shown in a thought bubble with the food's own look, and eat them) and
// the bar (its customers order drinks, the reagent orders of custom_order.dm), with their portals and seats. The var blocks are
// generated.

/////RESTAURANT/////
// tg: code/modules/food_and_drinks/restaurant/generic_venues.dm:3
public partial class datum_venue_restaurant : datum_venue
{
    // tg: code/modules/food_and_drinks/restaurant/generic_venues.dm:22
    public override object get_food_appearance(object order = null)
    {
        object appearance = SSrestaurant.food_appearance_cache[order];

        if (!Dm.truthy(appearance)) //We havn't made this one before, do so now.
        {
            obj_item temp_object = Dm.@new(order as Type) as obj_item; //Make a temp object so we can see it including any overlays
            appearance = temp_object.appearance; //And then steal its appearance
            SSrestaurant.food_appearance_cache[order] = appearance; //and cache it for future orders
            qdel(temp_object);
        }

        image food_image = Dm.@new<image>();
        food_image.appearance = appearance;
        Dm.append(food_image.underlays, mutable_appearance(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "thought_bubble"));

        return food_image;
    }

    // tg: code/modules/food_and_drinks/restaurant/generic_venues.dm:37
    public override object is_correct_order(atom_movable object_used = null, object wanted_item = null)
    {
        object dot = base.is_correct_order(object_used, wanted_item);
        return Dm.truthy(dot) ? dot : Dm.num(Dm.eq(object_used.type, wanted_item));
    }

    // tg: code/modules/food_and_drinks/restaurant/generic_venues.dm:41
    public override object order_food_line(object order = null)
    {
        Type object_to_order = order as Type;
        return I18n.T("I'll take \\a []", Dm.initial(object_to_order, (obj_item i) => i.name));
    }

    // tg: code/modules/food_and_drinks/restaurant/generic_venues.dm:45
    public override async DmTask<object> on_get_order(mob_living_basic_robot_customer customer_pawn = null, obj_item order_item = null)
    {
        object transaction_result = await base.on_get_order(customer_pawn, order_item);
        if (Dm.truthy(Dm.bitand(transaction_result, TRANSACTION_HANDLED)) || !Dm.truthy(Dm.bitand(transaction_result, TRANSACTION_SUCCESS)))
            return null;

        customer_pawn.visible_message(
            span_danger(I18n.T("[] pushes [] into their mouth-shaped hole!", customer_pawn, order_item)),
            span_danger(I18n.T("You push [] into your mouth-shaped hole.", order_item))
        );
        playsound(customer_pawn, Dm.resource("sound/items/eatfood.ogg"), rand(10, 50), TRUE);
        qdel(order_item);
        return null;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/restaurant/custom_order.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the custom orders (the moth customer's piece of clothing, an ice cream of some flavours in a cone, a drink in a
// glass and a soup in a bowl of some serving). The var blocks are generated.

/**
 * custom order datums
 * Used for less generic orders (ice cream for example)
 * without snowflaking venue and customer code too much.
 */
// tg: code/modules/food_and_drinks/restaurant/custom_order.dm:6
public partial class datum_custom_order : datum
{
    /**
     * Returns the object that will be stored in the controller blackboard for the customer bot.
     * Normally but not necessarily, this would be the custom order itself. The moth clothing is a
     * good example of an exception to that.
     */
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:13
    public virtual object dispense_order()
    {
        return this;
    }

    ///Whether or not the order is correct. Only relevant if dispense_order didn't return another object.
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:17
    public virtual object is_correct_order(obj_item object_used = null)
    {
        if (Dm.truthy(Dm.bitand(object_used.SEND_SIGNAL(COMSIG_ITEM_IS_CORRECT_CUSTOM_ORDER, Dm.list(object_used, this)), COMPONENT_CORRECT_ORDER)))
            return TRUE;
        return null;
    }

    /// Returns the appearance of the order that appears when hovering over the mob with the cursor
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:22
    public virtual object get_order_appearance(params object[] __extra)
    {
        stack_trace(Dm.text("[]/get_order_appearance() not set", type), "code/modules/food_and_drinks/restaurant/custom_order.dm", 23);
        return image(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "thought_bubble"); // empty thought bubble
    }

    /// Returns the order line shout by the mob and also shown to the player when examining it.
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:27
    public virtual object get_order_line(params object[] __extra)
    {
        stack_trace(Dm.text("[]/get_order_line() not set", type), "code/modules/food_and_drinks/restaurant/custom_order.dm", 28);
        return "broken custom order pls call a coder";
    }

    /**
     * Handles what the bot does with the order when it gets it
     *
     * Return [TRANSACTION_SUCCESS] to denote the order went through successfully (Not generally necessary to include here)
     * Return [TRANSACTION_HANDLED] to not do any further handling of the order by the
     */
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:37
    public virtual async DmTask<object> handle_get_order(mob_living_basic_robot_customer customer_pawn = null, obj_item order_item = null)
    {
        await DmTask.CompletedTask;
        return NONE;
    }
}

// tg: code/modules/food_and_drinks/restaurant/custom_order.dm:40
public partial class datum_custom_order_moth_clothing : datum_custom_order
{
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:44
    [DmParams("customer", "our_venue")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living_basic_robot_customer customer = Dm.arg<mob_living_basic_robot_customer>(__extra, 0);
        datum_venue our_venue = Dm.arg<datum_venue>(__extra, 1);
        await DmTask.CompletedTask;
        datum_weakref portal_ref = our_venue.current_visitors[customer] as datum_weakref;
        obj_machinery_restaurant_portal portal = portal_ref.resolve() as obj_machinery_restaurant_portal;
        mob_living_carbon_human buffet = portal?.turned_on_portal?.resolve() as mob_living_carbon_human;
        if (buffet is not mob_living_carbon_human) // Always asks for the clothes that you have on, but this is a fallback.
        {
            wanted_clothing_type = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_clothing_head_utility_chefhat), 3),
                Dm.kv(typeof(obj_item_clothing_shoes_sneakers_black), 3),
                Dm.kv(typeof(obj_item_clothing_gloves_color_black), 1)
            ));
            return;
        }

        DmList orderable = Dm.list();

        if (!QDELETED(buffet.head))
            orderable[buffet.head.type] = 5;

        if (!QDELETED(buffet.gloves))
            orderable[buffet.gloves.type] = 5;

        if (!QDELETED(buffet.shoes))
            orderable[buffet.shoes.type] = 1;

        if (!Dm.truthy(length(orderable)))
        {
            orderable = Dm.list(
                Dm.kv(typeof(obj_item_clothing_head_utility_chefhat), 3),
                Dm.kv(typeof(obj_item_clothing_shoes_sneakers_black), 3),
                Dm.kv(typeof(obj_item_clothing_gloves_color_black), 1)
            );
        }

        wanted_clothing_type = pick_weight(orderable);
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:77
    public override object get_order_appearance(params object[] __extra)
    {
        datum_venue our_venue = Dm.arg<datum_venue>(__extra, 0);
        return our_venue.get_food_appearance(wanted_clothing_type);
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:80
    public override object get_order_line(params object[] __extra)
    {
        datum_venue our_venue = Dm.arg<datum_venue>(__extra, 0);
        return our_venue.order_food_line(wanted_clothing_type);
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:83
    public override object dispense_order()
    {
        object dot = wanted_clothing_type;
        qdel(this); // This datum is no longer needed.
        return dot;
    }
}

// tg: code/modules/food_and_drinks/restaurant/custom_order.dm:87
public partial class datum_custom_order_icecream : datum_custom_order
{
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:98  var/static/list/possible_flavors (a proc-level static, rules 10.6)
    private static ref DmList New__possible_flavors => ref TgState.Current.datum_custom_order_icecream__New__possible_flavors;

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:95
    [DmParams("customer")]
    public override async DmTask New(params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (prob(33))
            cone_type = typeof(obj_item_food_icecream_chocolate);
        New__possible_flavors ??= Dm.list();
        DmList possible_flavors = New__possible_flavors;
        foreach (object flavour in Dm.each(GLOB.ice_cream_flavours))
        {
            //only request standard flavors that are available from the ice cream vat
            if (!Dm.truthy(((datum_ice_cream_flavour)GLOB.ice_cream_flavours[flavour]).hidden) && !Dm.eq(flavour, ICE_CREAM_CUSTOM))
                Dm.append(ref possible_flavors, flavour);
        }

        for (float iteration = 1, iteration__end = rand(1, DEFAULT_MAX_ICE_CREAM_SCOOPS); iteration <= iteration__end; iteration++)
        {
            object chosen_flavor = pick(possible_flavors);
            Dm.append(ref wanted_flavors, chosen_flavor);
        }

        DmList unique_list = GlobalProcs.unique_list(wanted_flavors) as DmList;
        if (wanted_flavors.len > 1 && Dm.length(unique_list) == 1)
            icecream_name = Dm.text("[] [] ice cream ([])", make_tuple(wanted_flavors.len), wanted_flavors[1], Dm.initial(cone_type, (obj_item_food_icecream c) => c.name));
        else
        {
            sortTim(wanted_flavors, cmp: "/proc/" + nameof(GlobalProcs.cmp_text_asc));
            icecream_name = Dm.text("[] ice cream ([])", english_list(wanted_flavors), Dm.initial(cone_type, (obj_item_food_icecream c) => c.name));
        }
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:115
    public override object get_order_line(params object[] __extra)
    {
        return I18n.T("I'll take \\a []", icecream_name);
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:118
    public override object get_order_appearance(params object[] __extra)
    {
        image food_image = image(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "thought_bubble") as image;
        image i_scream = image(Dm.resource("icons/obj/service/kitchen.dmi"), Dm.initial(cone_type, (obj_item_food_icecream c) => c.icon_state)) as image;

        float added_offset = 0;
        foreach (object flavor in Dm.each(wanted_flavors))
        {
            image scoop = image(Dm.resource("icons/obj/service/kitchen.dmi"), "icecream_custom") as image;
            scoop.color = ((datum_ice_cream_flavour)GLOB.ice_cream_flavours[flavor]).color;
            scoop.pixel_z = added_offset;
            Dm.append(i_scream.overlays, scoop);
            added_offset += ICE_CREAM_SCOOP_OFFSET;
        }
        // food_image.add_overlay(i_scream): /image/proc/add_overlay(x) is `overlays |= x` (controllers/subsystem/overlays.dm:113)
        Dm.combine(food_image.overlays, i_scream);

        return food_image;
    }
}

// tg: code/modules/food_and_drinks/restaurant/custom_order.dm:133
public partial class datum_custom_order_reagent : datum_custom_order
{
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:141
    [DmParams("customer", "reagent_type")]
    public override async DmTask New(params object[] __extra)
    {
        object reagent_type = Dm.arg(__extra, 1);
        await base.New(__extra);
        this.reagent_type = reagent_type as Type;
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:145
    public override object get_order_line(params object[] __extra)
    {
        return I18n.T("I'll take []u of []", reagents_needed, Dm.initial(reagent_type, (datum_reagent r) => r.name));
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:148
    public override object get_order_appearance(params object[] __extra)
    {
        image food_image = image(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "thought_bubble") as image;
        datum_glass_style draw_as = (GLOB.glass_style_singletons[container_needed] as DmList)?[reagent_type] as datum_glass_style;
        object or__1 = draw_as?.icon;
        if (!Dm.truthy(or__1))
            or__1 = Dm.initial(reagent_type, (datum_reagent r) => r.fallback_icon);
        if (!Dm.truthy(or__1))
            or__1 = Dm.initial(container_needed, (obj_item i) => i.icon);
        object or__2 = draw_as?.icon_state;
        if (!Dm.truthy(or__2))
            or__2 = Dm.initial(reagent_type, (datum_reagent r) => r.fallback_icon_state);
        if (!Dm.truthy(or__2))
            or__2 = Dm.initial(container_needed, (obj_item i) => i.icon_state);
        image drink_image = image(
            icon: or__1,
            icon_state: or__2
        ) as image;
        // food_image.add_overlay(drink_image): /image/proc/add_overlay(x) is `overlays |= x` (controllers/subsystem/overlays.dm:113)
        Dm.combine(food_image.overlays, drink_image);
        return food_image;
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:158
    public override async DmTask<object> handle_get_order(mob_living_basic_robot_customer customer_pawn = null, obj_item order_item = null)
    {
        object dot = TRANSACTION_HANDLED;

        foreach (datum_reagent reagent in Dm.each<datum_reagent>((order_item.reagents as datum_reagents)?.reagent_list))
        {
            if (Dm.eq(reagent.type, reagent_type))
                Dm.combine(ref dot, reagent.SEND_SIGNAL(COMSIG_REAGENT_SOLD_TO_CUSTOMER, Dm.list(reagent, customer_pawn, order_item)));
        }

        playsound(customer_pawn, Dm.resource("sound/items/drink.ogg"), rand(10, 50), TRUE);
        await ((datum_reagents)order_item.reagents).clear_reagents();
        return dot;
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:168
    public override object is_correct_order(obj_item object_used = null)
    {
        if (Dm.truthy(base.is_correct_order(object_used)))
            return TRUE;
        if (!istype(object_used, container_needed) || object_used.reagents == null)
            return FALSE;

        datum_reagents holder = (datum_reagents)object_used.reagents;
        // The container must be majority reagent
        datum_reagent master_reagent = holder.get_master_reagent() as datum_reagent;
        if (!Dm.eq(master_reagent?.type, reagent_type))
            return FALSE;
        // We must fulfill the sample size threshold
        if (reagents_needed > holder.total_volume)
            return FALSE;
        // Also must be at least 1/3rd of that reagent, prevent cheese
        if (Dm.num(holder.get_reagent_amount(reagent_type)) < holder.total_volume * 0.33f)
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/food_and_drinks/restaurant/custom_order.dm:187
public partial class datum_custom_order_reagent_drink : datum_custom_order_reagent
{
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:190
    public override async DmTask<object> handle_get_order(mob_living_basic_robot_customer customer_pawn = null, obj_item order_item = null)
    {
        customer_pawn.visible_message(
            span_danger(I18n.T("[] slurps up [] in one go!", customer_pawn, order_item)),
            span_danger(I18n.T("You slurp up [] in one go.", order_item))
        );
        return await base.handle_get_order(customer_pawn, order_item);
    }
}

// tg: code/modules/food_and_drinks/restaurant/custom_order.dm:197
public partial class datum_custom_order_reagent_soup : datum_custom_order_reagent
{
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:203
    [DmParams("customer", "reagent_type")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        DmList serving_sizes = Dm.list(
            Dm.kv("small serving (15u)", 15),
            Dm.kv("medium serving (20u)", 20),
            Dm.kv("large serving (25u)", 25)
        );
        picked_serving = pick(serving_sizes);
        reagents_needed = Dm.num(serving_sizes[picked_serving]);
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:213
    public override object get_order_line(params object[] __extra)
    {
        return I18n.T("I'll take a [] of []", picked_serving, Dm.initial(reagent_type, (datum_reagent r) => r.name));
    }

    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:216
    public override async DmTask<object> handle_get_order(mob_living_basic_robot_customer customer_pawn = null, obj_item order_item = null)
    {
        customer_pawn.visible_message(
            span_danger(I18n.T("[] pours [] right down [] hatch!", customer_pawn, order_item, customer_pawn.p_their())),
            span_danger(I18n.T("You pour [] down your hatch in one go.", order_item))
        );
        return await base.handle_get_order(customer_pawn, order_item);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/restaurant/custom_order.dm:98  var/static/list/possible_flavors
    internal DmList datum_custom_order_icecream__New__possible_flavors;
}

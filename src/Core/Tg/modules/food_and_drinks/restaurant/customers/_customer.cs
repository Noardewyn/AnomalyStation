// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/restaurant/customers/_customer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the robot customers' kinds (the American, the Italian, the French, the Japanese and the salaryman, the moth, the
// Mexican, the British gentleman and bobby, the malfunctioning one): their names, lines, sprites and clothes, what they order at
// each venue, and the moth's wings and jetpack, the French flag and the Japanese hearts drawn over them. The var blocks are
// generated.

// tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:1
public partial class datum_customer_data : datum
{
    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:47
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        name_prefixes = world.file2list(prefix_file) as DmList;
        if (Dm.truthy(check_holidays(ICE_CREAM_DAY))) ///customers are more likely to order ice cream on this holiday
        {
            DmList orderable_restaurant = orderable_objects[VENUE_RESTAURANT] as DmList;
            if (Dm.truthy(orderable_restaurant?[typeof(datum_custom_order_icecream)]))
                orderable_restaurant[typeof(datum_custom_order_icecream)] = Dm.num(orderable_restaurant[typeof(datum_custom_order_icecream)]) * 3;
        }
    }

    /// Can this customer be chosen for this venue?
    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:56
    public virtual object can_use(datum_venue venue = null, obj_machinery_restaurant_portal portal = null)
    {
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:59
    public virtual object get_overlays(mob_living_basic_robot_customer customer = null)
    {
        return null;
    }

    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:62
    public virtual object get_underlays(mob_living_basic_robot_customer customer = null)
    {
        return null;
    }
}

// tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:156
public partial class datum_customer_data_french : datum_customer_data
{
    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:194
    public override object get_overlays(mob_living_basic_robot_customer customer = null)
    {
        if (Dm.truthy(((datum_ai_controller)customer.ai_controller).blackboard[BB_CUSTOMER_LEAVING]))
            return mutable_appearance(customer.icon, "french_flag", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART));
        return null;
    }
}

// tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:200
public partial class datum_customer_data_japanese : datum_customer_data
{
    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:239
    public override object get_overlays(mob_living_basic_robot_customer customer = null)
    {
        //leaving and eaten
        datum_ai_controller controller = (datum_ai_controller)customer.ai_controller;
        if (type == typeof(datum_customer_data_japanese) && Dm.truthy(controller.blackboard[BB_CUSTOMER_LEAVING]) && Dm.truthy(controller.blackboard[BB_CUSTOMER_EATING]))
            return mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "love_hearts", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART));
        return null;
    }
}

// tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:278
public partial class datum_customer_data_moth : datum_customer_data
{
    // The whole gag is taking off your hat and giving it to the customer.
    // If it takes any more effort, it loses a bit of the comedy.
    // Therefore, only show up if it's reasonable for that gag to happen.
    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:308
    public override object can_use(datum_venue venue = null, obj_machinery_restaurant_portal portal = null)
    {
        mob_living_carbon_human buffet = portal.turned_on_portal?.resolve() as mob_living_carbon_human;
        if (buffet is not mob_living_carbon_human)
            return FALSE;
        if (QDELETED(buffet.head) && QDELETED(buffet.gloves) && QDELETED(buffet.shoes))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:316
    public virtual object get_wings(mob_living_basic_robot_customer customer = null)
    {
        object customer_ref = WEAKREF(customer);
        if (!Dm.truthy(LAZYACCESS(wings_chosen, customer_ref)))
        {
            object picked_wings = pick(SSaccessories.feature_list[FEATURE_MOTH_WINGS]);
            LAZYSET(ref wings_chosen, customer_ref, (SSaccessories.feature_list[FEATURE_MOTH_WINGS] as DmList)[picked_wings]);
        }
        return wings_chosen[customer_ref];
    }

    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:323
    public override object get_underlays(mob_living_basic_robot_customer customer = null)
    {
        DmList underlays = Dm.list();

        datum_sprite_accessory_moth_wings wings = get_wings(customer) as datum_sprite_accessory_moth_wings;
        Dm.append(ref underlays, mutable_appearance(icon: Dm.resource("icons/mob/human/species/moth/moth_wings.dmi"), icon_state: Dm.text("m_moth_wings_[]_BEHIND", wings.icon_state), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));
        return underlays;
    }

    // tg: code/modules/food_and_drinks/restaurant/customers/_customer.dm:330
    public override object get_overlays(mob_living_basic_robot_customer customer = null)
    {
        DmList overlays = Dm.list();

        datum_sprite_accessory_moth_wings wings = get_wings(customer) as datum_sprite_accessory_moth_wings;
        Dm.append(ref overlays, mutable_appearance(icon: Dm.resource("icons/mob/human/species/moth/moth_wings.dmi"), icon_state: Dm.text("m_moth_wings_[]_FRONT", wings.icon_state), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));
        Dm.append(ref overlays, mutable_appearance(icon: customer.icon, icon_state: "mothbot_jetpack", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));
        return overlays;
    }
}

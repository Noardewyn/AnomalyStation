// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/restaurant/_venue.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the venue (a restaurant or a bar: opened and closed at its portals by an ID with its access, it sends a robot
// customer through a portal every minute or so while open, takes their orders and is paid for them), the restaurant portal machine
// (an ID switches its venue on and off, an ID used on it picks its venue in a radial menu; the closed door is its overlay) and the
// seating indicator placer with the seats it projects. The var blocks are generated but the portal's and the seat's: their
// linked_venue holds the venue's typepath and then the venue itself, so it is declared here as a plain value (rules 3.5).

///Represents the abstract concept of a food venue in the code.
// tg: code/modules/food_and_drinks/restaurant/_venue.dm:2
public partial class datum_venue : datum
{
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:34
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!(visit_cooldown <= world.time)) // COOLDOWN_FINISHED(src, visit_cooldown), inline (rules 11.1 (a))
            return null;
        visit_cooldown = world.time + Dm.num(rand(min_time_between_visitor, max_time_between_visitor)); // COOLDOWN_START(src, visit_cooldown, ...), inline
        if (current_visitors.len < max_guests && current_visitors.len < linked_seats.len + 1) //Not above max guests, and not more than one waiting customer.
            create_new_customer();
        return null;
    }

    ///Spawns a new customer at the portal
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:42
    public virtual void create_new_customer()
    {
        DmList customer_types_to_choose = customer_types;
        Type customer_type = null;
        obj_machinery_restaurant_portal chosen_portal = pick(restaurant_portals) as obj_machinery_restaurant_portal;

        // In practice, the list will never run out, but this is for sanity.
        while (Dm.truthy(customer_types_to_choose.len))
        {
            customer_type = pick_weight(customer_types_to_choose) as Type;

            datum_customer_data customer = SSrestaurant.all_customers[customer_type] as datum_customer_data;
            if (Dm.truthy(customer.can_use(this, chosen_portal)))
                break;

            // Only copy the list once, so that we're not mutating ourselves.
            if (customer_types_to_choose == customer_types)
                customer_types_to_choose = customer_types.Copy();

            Dm.remove(ref customer_types_to_choose, customer_type);
        }

        if (Dm.truthy(Dm.initial(customer_type, (datum_customer_data c) => c.is_unique)))
            Dm.remove(ref customer_types, customer_type);

        mob_living_basic_robot_customer new_customer = Dm.@new<mob_living_basic_robot_customer>(get_turf(chosen_portal), customer_type, this);
        current_visitors[new_customer] = WEAKREF(chosen_portal);
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:67
    public virtual async DmTask<object> order_food(mob_living_basic_robot_customer customer_pawn = null, datum_customer_data customer_data = null)
    {
        object order = pick_weight(customer_data.orderable_objects[venue_type] as DmList);
        DmList order_args = null; // Only for custom orders - arguments passed into New
        image food_image = null;
        object food_line = null;

        if (ispath(order, typeof(datum_reagent)))
        {
            // This is pain
            Type reagent_order = order as Type;
            order_args = Dm.list(Dm.kv("reagent_type", reagent_order));
            order = Dm.initial(reagent_order, (datum_reagent r) => r.restaurant_order);
        }

        if (ispath(order, typeof(datum_custom_order))) // generate the special order
        {
            DmList or__1 = order_args;
            datum_custom_order custom_order = Dm.@new(order as Type, Dm.arglist(Dm.add(Dm.list(Dm.kv("customer", customer_pawn)), Dm.truthy(or__1) ? or__1 : Dm.list()))) as datum_custom_order;
            food_image = custom_order.get_order_appearance(this) as image;
            food_line = custom_order.get_order_line(this);
            order = custom_order.dispense_order();
        }
        else
        {
            food_image = get_food_appearance(order) as image;
            food_line = order_food_line(order);
        }

        await customer_pawn.say(food_line);

        // common code for the food thoughts appearance
        food_image.loc = customer_pawn;
        food_image.pixel_w = 16;
        food_image.pixel_z = 32;
        SET_PLANE_EXPLICIT(food_image, HUD_PLANE, customer_pawn);
        food_image.plane = HUD_PLANE;
        food_image.appearance_flags = Dm.bitor(RESET_COLOR, KEEP_APART);
        customer_pawn.hud_to_show_on_hover = customer_pawn.add_alt_appearance(typeof(datum_atom_hud_alternate_appearance_basic_food_demands), "food_thoughts", food_image) as datum_atom_hud;

        return order;
    }

    ///Checks if the object used is correct for the venue
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:102
    public virtual object is_correct_order(atom_movable object_used = null, object wanted_item = null)
    {
        if (wanted_item is datum_custom_order)
        {
            datum_custom_order custom_order = (datum_custom_order)wanted_item;
            return custom_order.is_correct_order(object_used as obj_item);
        }
        return FALSE;
    }

    ///gets the appearance of the ordered object that shows up when hovering your cursor over the customer mob.
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:109
    public virtual object get_food_appearance(object order = null)
    {
        return null;
    }

    ///The line the robot says when ordering
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:113
    public virtual object order_food_line(object order = null)
    {
        return "broken venue pls call a coder";
    }

    ///Effects for when a customer receives their order at this venue
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:117
    public virtual async DmTask<object> on_get_order(mob_living_basic_robot_customer customer_pawn = null, obj_item order_item = null)
    {
        // This is an item typepath, a reagent typepath, or a custom order datum instance.
        object order = ((datum_ai_controller)customer_pawn.ai_controller).blackboard[BB_CUSTOMER_CURRENT_ORDER];

        object dot = order_item.SEND_SIGNAL(COMSIG_ITEM_SOLD_TO_CUSTOMER, Dm.list(order_item, customer_pawn));

        foreach (datum_reagent reagent in Dm.each<datum_reagent>((order_item.reagents as datum_reagents)?.reagent_list))
        {
            // Our order can be a reagent within the item we're receiving
            if (Dm.eq(reagent.type, order))
            {
                Dm.combine(ref dot, reagent.SEND_SIGNAL(COMSIG_REAGENT_SOLD_TO_CUSTOMER, Dm.list(reagent, customer_pawn, order_item)));
                break;
            }
        }

        // Order can be a /datum/custom_order instance
        if (order is datum_custom_order)
        {
            datum_custom_order special_order = (datum_custom_order)order;
            Dm.combine(ref dot, await special_order.handle_get_order(customer_pawn, order_item));
        }

        if (Dm.truthy(Dm.bitand(dot, TRANSACTION_SUCCESS)))
            customers_served++;
        return dot;
    }

    ///Toggles whether the venue is open or not
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:140
    public virtual async DmTask toggle_open()
    {
        if (Dm.truthy(open))
            await close();
        else
            await open__proc();
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:146
    public virtual async DmTask open__proc()
    {
        open = TRUE;
        foreach (obj_machinery_restaurant_portal portal in Dm.each<obj_machinery_restaurant_portal>(restaurant_portals))
            await portal.update_icon();
        visit_cooldown = world.time + 4 * SECONDS; // COOLDOWN_START(src, visit_cooldown, 4 SECONDS), inline: First one comes faster
        START_PROCESSING(SSobj, this);
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:153
    public virtual async DmTask close()
    {
        open = FALSE;
        foreach (obj_machinery_restaurant_portal portal in Dm.each<obj_machinery_restaurant_portal>(restaurant_portals))
            await portal.update_icon();
        STOP_PROCESSING(SSobj, this);
        foreach (mob_living_basic robot_customer in Dm.each<mob_living_basic>(current_visitors))
            ((datum_ai_controller)robot_customer.ai_controller).set_blackboard_key(BB_CUSTOMER_LEAVING, TRUE); //LEAVEEEEEE
    }
}

// tg: code/modules/food_and_drinks/restaurant/_venue.dm:161
public partial class obj_machinery_restaurant_portal : obj_machinery
{
    ///What venue is this portal for? Uses a typepath which is turned into an instance on Initialize
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:174
    public object linked_venue;

    /// A weak reference to the mob who turned on the portal
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:177
    public datum_weakref turned_on_portal;

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:162
    public obj_machinery_restaurant_portal()
    {
        name = "restaurant portal";
        desc = "A robot-only gate into the wonders of Space Station cuisine!";
        icon = Dm.resource("icons/obj/machines/restaurant_portal.dmi");
        icon_state = "portal";
        base_icon_state = "portal";
        anchored = 1;
        density = 0;
        circuit = typeof(obj_item_circuitboard_machine_restaurant_portal);
        layer = 2.9f;
        armor_type = typeof(datum_armor_restaurant_portal);
        resistance_flags = 50;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:188
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context(): the context tips (D291)
        if (!Dm.truthy(linked_venue))
            return dot;
        obj_item_circuitboard_machine_restaurant_portal board = circuit_value as obj_item_circuitboard_machine_restaurant_portal;
        board.venue_type = linked_venue as Type;
        linked_venue = SSrestaurant.all_venues[linked_venue];
        Dm.append(ref ((datum_venue)linked_venue).restaurant_portals, this);
        return dot;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:198
    public override object Destroy(float? force = null)
    {
        turned_on_portal = null;
        Dm.remove(ref ((datum_venue)linked_venue).restaurant_portals, this);
        linked_venue = null;
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:204
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        await base.on_construction(user, __extra);
        circuit_value.configure_machine(this);
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:208
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy((linked_venue as datum_venue)?.open)) //Any open venues
            Dm.append(ref dot, mutable_appearance(icon, "portal_door"));
        return dot;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:213
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.str(Dm.truthy(panel_open) ? Dm.text("[]-open", base_icon_state) : base_icon_state);
        return dot;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:217
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item_card_id used_id = ((mob_living)user).get_idcard(TRUE) as obj_item_card_id;

        if (!Dm.truthy(used_id))
            return await base.attack_hand(user, modifiers, __extra);

        if (!Dm.@in(((datum_venue)linked_venue).req_access, used_id.GetAccess()))
        {
            to_chat(user, span_warning(I18n.T("This card lacks the access to change this venue's status.")));
            return null;
        }

        await ((datum_venue)linked_venue).toggle_open();
        await update_icon();
        return null;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:230
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.GetID()))
            return NONE;

        if (!Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, I18n.T("insufficient access!"));
            return ITEM_INTERACT_BLOCKING;
        }

        DmList radial_items = Dm.list();
        DmList radial_results = Dm.list();

        foreach (object type_key in Dm.each(SSrestaurant.all_venues))
        {
            datum_venue venue = SSrestaurant.all_venues[type_key] as datum_venue;
            radial_items[venue.name] = image(Dm.resource("icons/obj/machines/restaurant_portal.dmi"), venue.name);
            radial_results[venue.name] = type_key;
        }

        object choice = await show_radial_menu(user, this, radial_items, null, require_near: TRUE);

        if (!Dm.truthy(choice))
            return ITEM_INTERACT_BLOCKING;

        object venue_type = radial_results[choice];
        obj_item_circuitboard_machine_restaurant_portal board = circuit_value as obj_item_circuitboard_machine_restaurant_portal;
        board.venue_type = venue_type as Type;
        datum_venue chosen_venue = SSrestaurant.all_venues[venue_type] as datum_venue;

        turned_on_portal = WEAKREF(user) as datum_weakref;

        balloon_alert(user, I18n.T("venue changed to []", chosen_venue.name));

        if (Dm.truthy(linked_venue) && Dm.@in(this, ((datum_venue)linked_venue).restaurant_portals)) //We're already linked, unlink us.
        {
            datum_venue old_venue = (datum_venue)linked_venue;
            Dm.remove(ref old_venue.restaurant_portals, this);
            if (Dm.truthy(old_venue.open) && !Dm.truthy(length(old_venue.restaurant_portals)))
                await old_venue.close();
            linked_venue = null;
        }

        linked_venue = chosen_venue;
        Dm.append(ref chosen_venue.restaurant_portals, this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:270
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:273
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:276
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open the panel first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
            return ITEM_INTERACT_SUCCESS;
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:285  add_context: the context tips (D291, a live-partial stub)
}

// tg: code/modules/food_and_drinks/restaurant/_venue.dm:306
public partial class obj_item_holosign_creator_robot_seat : obj_item_holosign_creator
{
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:315
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/food_and_drinks/restaurant/_venue.dm:318
public partial class obj_structure_holosign_robot_seat : obj_structure_holosign
{
    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:325
    public object linked_venue = typeof(datum_venue);

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:319
    public obj_structure_holosign_robot_seat()
    {
        density = 0;
        desc = "Used to indicate a place to sit for a robot tourist. I better be careful.";
        icon = Dm.resource("icons/effects/effects.dmi");
        icon_state = "eating_zone";
        layer = 3.7f;
        use_vis_overlay = 0;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:327
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        linked_venue = SSrestaurant.all_venues[linked_venue];
        DmList linked_seats = ((datum_venue)linked_venue).linked_seats;
        linked_seats[this] = Dm.add(linked_seats[this], null); // += null: the seat is listed with no robot on it
        return dot;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:332
    public override async DmTask attack_holosign(mob_living user = null, DmList modifiers = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:335
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.eq(tool.type, projector?.type) && !Dm.truthy(((datum_venue)linked_venue).linked_seats[this]))
        {
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/restaurant/_venue.dm:340
    public override object Destroy(float? force = null)
    {
        Dm.remove(((datum_venue)linked_venue).linked_seats, this);
        return base.Destroy(force);
    }
}

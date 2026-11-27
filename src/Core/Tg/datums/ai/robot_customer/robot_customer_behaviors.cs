// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/robot_customer/robot_customer_behaviors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the robot customer's behaviours (finding a free seat of its venue within seven tiles, ordering at it, waiting
// for the food with a patience that runs out, finding the portal it came from and leaving through it).

/// Searches nearby for an unclaimed seat belonging to the customer's venue.
/// Sets BB_CUSTOMER_MY_SEAT and claims it on success. Returns FAILURE if none found.
// tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:3
public partial class datum_bt_node_ai_behavior_robot_customer_find_seat : datum_bt_node_ai_behavior_robot_customer
{
    // tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:5
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic_robot_customer customer_pawn = controller.pawn as mob_living_basic_robot_customer;
        datum_customer_data customer_data = controller.blackboard[BB_CUSTOMER_CUSTOMERINFO] as datum_customer_data;
        datum_venue attending_venue = controller.blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;

        obj_structure_holosign_robot_seat found_seat = null;
        foreach (obj_structure_holosign_robot_seat potential_seat in Dm.each<obj_structure_holosign_robot_seat>(oview(7, customer_pawn)))
        {
            if (potential_seat.linked_venue != attending_venue)
                continue;
            if (Dm.truthy(attending_venue.linked_seats[potential_seat]))
                continue;
            turf seat_turf = get_turf(potential_seat);
            if (Dm.truthy(Waiting.NoSleep(seat_turf.is_blocked_turf())))
                continue;
            found_seat = potential_seat;
            break;
        }

        if (found_seat != null)
        {
            Waiting.Detach(customer_pawn.say(pick(customer_data.found_seat_lines))); // INVOKE_ASYNC(customer_pawn, TYPE_PROC_REF(/atom/movable, say), ...)
            controller.set_blackboard_key(BB_CUSTOMER_MY_SEAT, found_seat);
            attending_venue.linked_seats[found_seat] = customer_pawn;
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
        }

        if (!Dm.truthy(controller.blackboard[BB_CUSTOMER_SAID_CANT_FIND_SEAT_LINE]) || SPT_PROB(1.5f, seconds_per_tick))
        {
            Waiting.Detach(customer_pawn.say(pick(customer_data.cant_find_seat_lines))); // INVOKE_ASYNC(customer_pawn, TYPE_PROC_REF(/atom/movable, say), ...)
            controller.set_blackboard_key(BB_CUSTOMER_SAID_CANT_FIND_SEAT_LINE, TRUE);
        }

        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
    }
}

/// Places the customer's food order once they are at their seat.
// tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:36
public partial class datum_bt_node_ai_behavior_robot_customer_order_food : datum_bt_node_ai_behavior_robot_customer
{
    // tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:38
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        object async_flags = handle_async();
        if (Dm.truthy(async_flags))
            return async_flags;

        mob_living_basic_robot_customer customer_pawn = controller.pawn as mob_living_basic_robot_customer;
        obj_structure_holosign_robot_seat seat_marker = controller.blackboard[BB_CUSTOMER_MY_SEAT] as obj_structure_holosign_robot_seat;

        if (get_turf(seat_marker) == get_turf(customer_pawn))
        {
            obj_structure_chair my_seat = Dm.locate<obj_structure_chair>(get_turf(customer_pawn));
            if (my_seat != null)
                Waiting.NoSleep(customer_pawn.setDir(my_seat.dir));
        }

        return start_async();
    }

    // tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:53
    public override async DmTask<object> perform_async(datum_ai_controller controller = null)
    {
        mob_living_basic_robot_customer customer_pawn = controller.pawn as mob_living_basic_robot_customer;
        datum_customer_data customer_data = controller.blackboard[BB_CUSTOMER_CUSTOMERINFO] as datum_customer_data;
        datum_venue attending_venue = controller.blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;
        object order = null;
        if (!QDELETED(customer_pawn) && !QDELETED(attending_venue))
            order = await attending_venue.order_food(customer_pawn, customer_data);
        if (!Dm.truthy(async_still_valid()))
            return null;
        if (order != null)
            controller.set_blackboard_key(BB_CUSTOMER_CURRENT_ORDER, order);
        finish_async(AI_BEHAVIOR_SUCCEEDED);
        return null;
    }
}

/// Waits at the seat for food to arrive. Ticks down patience and checks for food placed in front.
/// Succeeds when BB_CUSTOMER_EATING is set; fails when patience runs out or BB_CUSTOMER_LEAVING is set.
/// On finish, sets BB_CUSTOMER_LEAVING and says the appropriate departure line.
// tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:70
public partial class datum_bt_node_ai_behavior_robot_customer_wait_for_food : datum_bt_node_ai_behavior_robot_customer
{
    // tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:72
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (Dm.truthy(controller.blackboard[BB_CUSTOMER_EATING]))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);

        controller.add_blackboard_key(BB_CUSTOMER_PATIENCE, Dm.num(seconds_per_tick) * -1 * SECONDS);
        if (Dm.num(controller.blackboard[BB_CUSTOMER_PATIENCE]) < 0 || Dm.truthy(controller.blackboard[BB_CUSTOMER_LEAVING]))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);

        if (SPT_PROB(0.85f, seconds_per_tick))
        {
            mob_living_basic_robot_customer customer_pawn = controller.pawn as mob_living_basic_robot_customer;
            datum_customer_data customer_data = controller.blackboard[BB_CUSTOMER_CUSTOMERINFO] as datum_customer_data;
            Waiting.Detach(customer_pawn.say(pick(customer_data.wait_for_food_lines))); // INVOKE_ASYNC(customer_pawn, TYPE_PROC_REF(/atom/movable, say), ...)
        }

        obj_structure_holosign_robot_seat seat_marker = controller.blackboard[BB_CUSTOMER_MY_SEAT] as obj_structure_holosign_robot_seat;
        if (get_turf(seat_marker) == get_turf(controller.pawn))
        {
            obj_structure_chair my_seat = Dm.locate<obj_structure_chair>(get_turf(controller.pawn));
            if (my_seat != null)
                Waiting.NoSleep(((atom)controller.pawn).setDir(my_seat.dir));
        }

        datum_venue attending_venue = controller.blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;
        turf infront_turf = get_step(controller.pawn, ((atom)controller.pawn).dir) as turf;
        foreach (obj_item I in Dm.each<obj_item>(infront_turf.contents))
        {
            if (Dm.truthy(attending_venue.is_correct_order(I, controller.blackboard[BB_CUSTOMER_CURRENT_ORDER])))
            {
                datum_ai_controller_robot_customer customer = (datum_ai_controller_robot_customer)controller;
                Waiting.NoSleep(customer.eat_order(I, attending_venue));
                break;
            }
        }

        return AI_BEHAVIOR_DELAY;
    }

    // tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:101
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        mob_living_basic_robot_customer customer_pawn = controller.pawn as mob_living_basic_robot_customer;
        datum_customer_data customer_data = controller.blackboard[BB_CUSTOMER_CUSTOMERINFO] as datum_customer_data;
        mob_living greytider = controller.blackboard[BB_CUSTOMER_CURRENT_TARGET] as mob_living;
        // Don't switch to leaving if we're heading to beat someone up or if we're being deleted.
        if (greytider != null || QDELETED(this) || QDELETED(customer_pawn))
            return;
        controller.set_blackboard_key(BB_CUSTOMER_LEAVING, TRUE);
        await customer_pawn.update_icon();
        if (Dm.truthy(succeeded))
            Waiting.Detach(customer_pawn.say(pick(customer_data.leave_happy_lines))); // INVOKE_ASYNC(customer_pawn, TYPE_PROC_REF(/atom/movable, say), ...)
        else
            Waiting.Detach(customer_pawn.say(pick(customer_data.leave_mad_lines))); // INVOKE_ASYNC(customer_pawn, TYPE_PROC_REF(/atom/movable, say), ...)
    }
}

/// Resolves the venue exit portal from attending_venue.current_visitors and stores it in BB_CUSTOMER_EXIT_PORTAL.
// tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:117
public partial class datum_bt_node_ai_behavior_robot_customer_find_exit_portal : datum_bt_node_ai_behavior_robot_customer
{
    // tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:119
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (controller.blackboard[BB_CUSTOMER_EXIT_PORTAL] != null)
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
        datum_venue attending_venue = controller.blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;
        if (attending_venue == null)
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
        datum_weakref portal_ref = attending_venue.current_visitors[controller.pawn] as datum_weakref;
        atom portal = portal_ref?.resolve() as atom;
        if (portal == null || QDELETED(portal))
            return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_FAILED);
        controller.set_blackboard_key(BB_CUSTOMER_EXIT_PORTAL, portal);
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
    }
}

/// Deletes the pawn once they have reached the exit portal.
// tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:134
public partial class datum_bt_node_ai_behavior_robot_customer_leave_venue : datum_bt_node_ai_behavior_robot_customer
{
    // tg: code/datums/ai/robot_customer/robot_customer_behaviors.dm:136
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        qdel(controller.pawn);
        return Dm.bitor(AI_BEHAVIOR_INSTANT, AI_BEHAVIOR_SUCCEEDED);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/robot_customer/robot_customer_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the robot customer's brain (its behaviour tree is robot_customer.bt.json): it is handed its order (the correct one
// is eaten, a harmless wrong item refused, a weapon or a punch warned twice and then answered with a spine break), it is pulled
// along by the venue's staff and resists anyone else. The var block is generated.

// tg: code/datums/ai/robot_customer/robot_customer_controller.dm:1
public partial class datum_ai_controller_robot_customer : datum_ai_controller
{
    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:16
    public override object Destroy(float? force = null)
    {
        clear_blackboard_key(BB_CUSTOMER_CURRENT_ORDER);
        clear_blackboard_key(BB_CUSTOMER_CUSTOMERINFO);
        clear_blackboard_key(BB_CUSTOMER_EXIT_PORTAL);
        return base.Destroy(force);
    }

    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:22
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        if (new_pawn is not mob_living_basic_robot_customer)
            return AI_CONTROLLER_INCOMPATIBLE;
        Waiting.NoSleep(new_pawn.AddElement(typeof(datum_element_relay_attackers)));
        RegisterSignal(new_pawn, COMSIG_ATOM_ATTACKBY, nameof(on_attackby));
        RegisterSignal(new_pawn, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
        RegisterSignal(new_pawn, COMSIG_LIVING_GET_PULLED, nameof(on_get_pulled));
        RegisterSignal(new_pawn, COMSIG_ATOM_ATTACK_HAND, nameof(on_get_punched));
        return await base.TryPossessPawn(new_pawn); //Run parent at end
    }

    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:32
    public override async DmTask UnpossessPawn(object destroy = null)
    {
        if (pawn == null)
        {
            stack_trace("Robot Customer AI Controller UnpossessPawn called with null pawn! This shouldn't happen in normal circumstances.", "code/datums/ai/robot_customer/robot_customer_controller.dm", 35); // and unit tests are abnormal circumstances
            return;
        }

        UnregisterSignal(pawn, Dm.list(COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_WAS_ATTACKED, COMSIG_LIVING_GET_PULLED, COMSIG_ATOM_ATTACK_HAND));
        await base.UnpossessPawn(destroy); //Run parent at end
    }

    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:42
    [SignalHandler]
    public virtual object on_attackby(datum source = null, obj_item I = null, mob_living user = null)
    {
        datum_venue attending_venue = blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;
        if (Dm.truthy(attending_venue.is_correct_order(I, blackboard[BB_CUSTOMER_CURRENT_ORDER])))
        {
            to_chat(user, span_notice(I18n.T("You hand [] to [].", I, pawn)));
            Waiting.NoSleep(eat_order(I, attending_venue));
            return COMPONENT_NO_AFTERATTACK;
        }
        else if (!Dm.truthy(I.force))
            Waiting.Detach(dont_want_that(I, user)); // INVOKE_ASYNC(src, PROC_REF(dont_want_that), I, user): tg passes the item as the chef and the user as the thing (neither is read), so the parameters are untyped (rules 3.4)
        else
            Waiting.Detach(warn_greytider(user)); // INVOKE_ASYNC(src, PROC_REF(warn_greytider), user)
        return null;
    }

    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:54
    [SignalHandler]
    public virtual void on_attacked(datum source = null, mob_living attacker = null)
    {
        Waiting.Detach(warn_greytider(attacker)); // INVOKE_ASYNC(src, PROC_REF(warn_greytider), attacker)
    }

    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:58
    public virtual async DmTask eat_order(obj_item order_item = null, datum_venue attending_venue = null)
    {
        if (Dm.truthy(blackboard[BB_CUSTOMER_EATING]))
            return;

        set_blackboard_key(BB_CUSTOMER_EATING, TRUE);
        await attending_venue.on_get_order(pawn as mob_living_basic_robot_customer, order_item);
        object our_order = blackboard[BB_CUSTOMER_CURRENT_ORDER];
        if (isdatum(our_order))
            qdel(our_order);
        clear_blackboard_key(BB_CUSTOMER_CURRENT_ORDER);
    }

    ///Called when
    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:71
    [SignalHandler]
    public virtual void on_get_pulled(datum source = null, mob_living puller = null)
    {
        Waiting.Detach(async_on_get_pulled(source, puller)); // INVOKE_ASYNC(src, PROC_REF(async_on_get_pulled), source, puller)
    }

    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:77
    public virtual async DmTask async_on_get_pulled(datum source = null, mob_living puller = null)
    {
        mob_living_basic_robot_customer customer = pawn as mob_living_basic_robot_customer;
        datum_customer_data customer_data = blackboard[BB_CUSTOMER_CUSTOMERINFO] as datum_customer_data;
        datum_venue attending_venue = blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;

        obj_item_card_id used_id = puller.get_idcard(TRUE) as obj_item_card_id;

        if (Dm.truthy(used_id) && Dm.@in(attending_venue.req_access, used_id?.GetAccess()))
        {
            await customer.say(customer_data.friendly_pull_line);
            return;
        }
        await warn_greytider(puller);
        customer.resist();
    }

    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:91
    public virtual async DmTask dont_want_that(object chef = null, object thing = null)
    {
        mob_living_basic_robot_customer customer = pawn as mob_living_basic_robot_customer;
        datum_customer_data customer_data = blackboard[BB_CUSTOMER_CUSTOMERINFO] as datum_customer_data;
        await customer.say(customer_data.wrong_item_line);
    }

    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:96
    public virtual async DmTask warn_greytider(mob_living greytider = null)
    {
        mob_living_basic_robot_customer customer = pawn as mob_living_basic_robot_customer;
        datum_venue attending_venue = blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;
        datum_customer_data customer_data = blackboard[BB_CUSTOMER_CUSTOMERINFO] as datum_customer_data;
        //Living mobs are tagged, so these will always be valid
        object greytider_ref = REF(greytider);
        attending_venue.mob_blacklist[greytider_ref] = Dm.add(attending_venue.mob_blacklist[greytider_ref], 1);

        object sw__1 = attending_venue.mob_blacklist[REF(greytider)];
        if (Dm.eq(sw__1, 1))
        {
            await customer.say(customer_data.first_warning_line);
            return;
        }
        else if (Dm.eq(sw__1, 2))
        {
            await customer.say(customer_data.second_warning_line);
            return;
        }
        else if (Dm.eq(sw__1, 3))
            await customer.say(customer_data.self_defense_line);
        set_blackboard_key(BB_CUSTOMER_CURRENT_TARGET, greytider);

        await cancel_current_plan();
    }

    // tg: code/datums/ai/robot_customer/robot_customer_controller.dm:116
    [SignalHandler]
    public virtual void on_get_punched(datum source = null, mob_living living_hitter = null)
    {
        datum_venue attending_venue = blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;

        obj_item_card_id used_id = living_hitter.get_idcard(hand_first: TRUE) as obj_item_card_id;

        if (Dm.truthy(used_id) && Dm.@in(attending_venue.req_access, used_id?.GetAccess()))
            return;

        if (Dm.truthy(living_hitter.combat_mode))
            Waiting.Detach(warn_greytider(living_hitter)); // INVOKE_ASYNC(src, PROC_REF(warn_greytider), living_hitter)
    }
}

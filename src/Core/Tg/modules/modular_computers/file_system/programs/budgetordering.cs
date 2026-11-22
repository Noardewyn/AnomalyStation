// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/budgetordering.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D298): the whole file, the NT Shopping Network app (its NtosCargo window): a department's requests and, with a budget card,
// its purchases and approvals; the shuttle loan's action since the shuttle loan event (gaps E3, D327). The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/budgetordering.dm:1
public partial class datum_computer_file_program_budgetorders : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/budgetordering.dm:37
    public virtual bool is_visible_pack(mob user = null, object paccess_to_check = null, DmList access = null, object contraband = null)
    {
        if (HAS_SILICON_ACCESS(user)) //Borgs can't buy things.
            return false;
        if (Dm.truthy(Dm.bitand(computer.obj_flags, EMAGGED)))
            return true;
        else if (Dm.truthy(contraband)) //Hide contrband when non-emagged.
            return false;
        if (!Dm.truthy(paccess_to_check)) // No required_access, allow it.
            return true;
        // if(isAdminGhostAI(user)) return TRUE: no admin ghosts yet (GAP(lead), is_helpers.cs)

        //Aquire access from the inserted ID card.
        if (!Dm.truthy(length(access)))
        {
            access = computer?.GetAccess() as DmList;
            if (!Dm.truthy(length(access)))
                return false;
        }

        if (Dm.@in(paccess_to_check, access))
            return true;

        return false;
    }

    // tg: code/modules/modular_computers/file_system/programs/budgetordering.dm:59
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["location"] = SSshuttle.supply.getStatusText();
        data["department"] = "Cargo";

        datum_bank_account buyer = SSeconomy.get_dep_account(cargo_account);
        var id_card = computer.stored_id?.GetID() as obj_item_card_id;
        if (Dm.truthy(id_card?.registered_account?.account_job?.paycheck_department))
        {
            buyer = SSeconomy.get_dep_account(id_card.registered_account.account_job.paycheck_department);
            if (Dm.@in(ACCESS_BUDGET, id_card.access))
            {
                requestonly = FALSE;
                can_approve_requests = TRUE;
                // If buyer is a departmental budget, replaces "Cargo" with that budget - we're not using the cargo budget here
                data["department"] = I18n.T("[] Requisitions", buyer.account_holder);
            }
            else
            {
                requestonly = TRUE;
                can_approve_requests = FALSE;
            }
        }
        else
            requestonly = TRUE;
        if (buyer != null)
            data["points"] = buyer.account_balance;
        // To recap above because it's kind of a mess, here's all the options:
            //Head of staff ID card: Can approve, buy, and make purchases using their own departmental budgets.
            //ID card, not a head of staff: can request items from cargo using departmental budget.
            //No ID card, can request items from cargo using the cargo budget.

        //Otherwise static data, that is being applied in ui_data as the crates visible and buyable are not static, and are determined by inserted ID.
        data["requestonly"] = requestonly;
        DmList supplies = Dm.list();
        foreach (object pack in Dm.each(SSshuttle.supply_packs))
        {
            var P = (datum_supply_pack)SSshuttle.supply_packs[pack];
            if (Dm.truthy(Dm.bitand(P.order_flags, ORDER_INVISIBLE)))
                continue;
            if (!is_visible_pack(user, P.access_view, null, Dm.bitand(P.order_flags, ORDER_CONTRABAND)) || Dm.truthy(Dm.bitand(P.order_flags, ORDER_EMAG_ONLY)))
                continue;
            if (!Dm.truthy(supplies[P.group]))
                supplies[P.group] = Dm.list(Dm.kv("name", P.group), Dm.kv("packs", Dm.list()));
            if ((Dm.truthy(Dm.bitand(P.order_flags, ORDER_EMAG_ONLY)) && (Dm.truthy(Dm.bitand(P.order_flags, ORDER_CONTRABAND)) && !Dm.truthy(contraband))) || (Dm.truthy(Dm.bitand(P.order_flags, ORDER_SPECIAL)) && !Dm.truthy(Dm.bitand(P.order_flags, ORDER_SPECIAL_ENABLED))) || Dm.truthy(Dm.bitand(P.order_flags, ORDER_POD_ONLY)))
                continue;

            object first_item = length(P.contains) > 0 ? P.contains[1] : null;
            var group_packs = (DmList)((DmList)supplies[P.group])["packs"];
            Dm.append(ref group_packs, Dm.list(Dm.list(
                Dm.kv("name", P.name),
                Dm.kv("cost", P.get_cost()),
                Dm.kv("id", Dm.text("[]", pack)),
                Dm.kv("desc", Dm.truthy(P.desc) ? P.desc : P.name), // If there is a description, use it. Otherwise use the pack's name.
                Dm.kv("first_item_icon", first_item == null ? null : Dm.text("[]", Dm.initial(first_item, "icon"))),
                Dm.kv("first_item_icon_state", first_item == null ? null : Dm.initial(first_item, "icon_state")),
                Dm.kv("goody", Dm.bitand(P.order_flags, ORDER_GOODY)),
                Dm.kv("access", P.access),
                Dm.kv("contains", P.get_contents_ui_data()))));
        }
        data["supplies"] = supplies;

        //Data regarding the User's capability to buy things.
        data["away"] = Dm.eq(SSshuttle.supply.getDockedId(), docking_away) ? TRUE : FALSE;
        data["self_paid"] = self_paid;
        data["docked"] = Dm.eq(SSshuttle.supply.mode, SHUTTLE_IDLE) ? TRUE : FALSE;
        data["loan"] = SSshuttle.shuttle_loan != null ? TRUE : FALSE;
        data["loan_dispatched"] = SSshuttle.shuttle_loan != null && Dm.truthy(SSshuttle.shuttle_loan.dispatched) ? TRUE : FALSE;
        data["can_send"] = FALSE; //There is no situation where I want the app to be able to send the shuttle AWAY from the station, but conversely is fine.
        data["can_approve_requests"] = can_approve_requests;
        data["app_cost"] = TRUE;
        object message = I18n.T("Remember to stamp and send back the supply manifests.");
        if (Dm.truthy(SSshuttle.centcom_message))
            message = SSshuttle.centcom_message;
        if (Dm.truthy(SSshuttle.supply_blocked))
            message = blockade_warning;
        data["message"] = message;
        DmList cart_list = Dm.list();
        foreach (datum_supply_order order in Dm.each<datum_supply_order>(SSshuttle.shopping_list))
        {
            if (Dm.truthy(cart_list[order.pack.name]))
            {
                var line = (DmList)((DmList)cart_list[order.pack.name])[1];
                line["amount"] = Dm.num(line["amount"]) + 1;
                line["cost"] = Dm.num(line["cost"]) + order.get_final_cost();
                if (Dm.truthy(order.department_destination))
                    line["dep_order"] = Dm.num(line["dep_order"]) + 1;
                if (order.paying_account != null)
                    line["paid"] = Dm.num(line["paid"]) + 1;
                continue;
            }

            cart_list[order.pack.name] = Dm.list(Dm.list(
                Dm.kv("cost_type", order.cost_type),
                Dm.kv("object", order.pack.name),
                Dm.kv("cost", order.get_final_cost()),
                Dm.kv("id", order.id),
                Dm.kv("amount", 1f),
                Dm.kv("orderer", order.orderer),
                Dm.kv("paid", order.paying_account != null ? TRUE : FALSE), //number of orders purchased privatly
                Dm.kv("dep_order", Dm.truthy(order.department_destination) ? TRUE : FALSE), //number of orders purchased by a department
                Dm.kv("can_be_cancelled", order.can_be_cancelled)));
        }
        DmList cart = Dm.list();
        foreach (object item_id in Dm.each(cart_list))
            Dm.append(ref cart, cart_list[item_id]);
        data["cart"] = cart;

        DmList requests = Dm.list();
        foreach (datum_supply_order order in Dm.each<datum_supply_order>(SSshuttle.request_list))
        {
            datum_supply_pack pack = order.pack;
            Dm.append(ref requests, Dm.list(Dm.list(
                Dm.kv("object", pack.name),
                Dm.kv("cost", pack.get_cost()),
                Dm.kv("orderer", order.orderer),
                Dm.kv("reason", order.reason),
                Dm.kv("id", order.id),
                Dm.kv("account", order.paying_account != null ? order.paying_account.account_holder : I18n.T("Cargo Department")))));
        }
        data["requests"] = requests;

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/budgetordering.dm:167
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["max_order"] = CARGO_MAX_ORDER;
        data["displayed_currency_full_name"] = Dm.text(" []", MONEY_NAME);
        data["displayed_currency_name"] = Dm.text(" []", MONEY_SYMBOL);
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/budgetordering.dm:174
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        switch (Dm.str(action))
        {
            case "send":
                if (!Dm.truthy(SSshuttle.supply.canMove()))
                {
                    await computer.say(safety_warning);
                    return null;
                }
                if (Dm.truthy(SSshuttle.supply_blocked))
                {
                    await computer.say(blockade_warning);
                    return null;
                }
                if (Dm.eq(SSshuttle.supply.getDockedId(), docking_home))
                {
                    await SSshuttle.moveShuttle(cargo_shuttle, docking_away, TRUE);
                    await computer.say(I18n.T("The supply shuttle is departing."));
                    user.investigate_log("sent the supply shuttle away.", INVESTIGATE_CARGO);
                }
                else
                {
                    user.investigate_log("called the supply shuttle.", INVESTIGATE_CARGO);
                    await computer.say(I18n.T("The supply shuttle has been called and will arrive in [] minute\\s.", SSshuttle.supply.timeLeft(600)));
                    await SSshuttle.moveShuttle(cargo_shuttle, docking_home, TRUE);
                }
                dot = TRUE;
                break;
            case "loan":
                if (SSshuttle.shuttle_loan == null)
                    return null;
                if (Dm.truthy(SSshuttle.supply_blocked))
                {
                    await computer.say(blockade_warning);
                    return null;
                }
                else if (!Dm.eq(SSshuttle.supply.mode, SHUTTLE_IDLE))
                    return null;
                else if (!Dm.eq(SSshuttle.supply.getDockedId(), docking_away))
                    return null;
                else if (stationcargo != TRUE)
                    return null;
                else
                {
                    await SSshuttle.shuttle_loan.loan_shuttle();
                    await computer.say(I18n.T("The supply shuttle has been loaned to CentCom."));
                    user.investigate_log("accepted a shuttle loan event.", INVESTIGATE_CARGO);
                    user.log_message("accepted a shuttle loan event.", LOG_GAME);
                    dot = TRUE;
                }
                break;
            case "add":
            {
                object id = (object)text2path(Dm.str(@params["id"])) ?? @params["id"];
                if (SSshuttle.supply_packs[id] is not datum_supply_pack pack)
                    return null;
                if (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_EMAG_ONLY | ORDER_POD_ONLY | ORDER_CONTRABAND)) || (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_SPECIAL)) && !Dm.truthy(Dm.bitand(pack.order_flags, ORDER_SPECIAL_ENABLED))))
                    return null;

                object name = "*None Provided*";
                object rank = "*None Provided*";
                object ckey = user.ckey;
                mob_living_carbon_human hwoman = null;
                if (user is mob_living_carbon_human human)
                {
                    hwoman = human;
                    rank = hwoman.get_assignment(hand_first: TRUE);
                }
                else if (issilicon(user))
                {
                    name = user.real_name;
                    rank = "Silicon";
                }

                // Our account that we want to end up paying with.
                datum_bank_account account = null;
                // Our ID card that we want to pull from for identification. Modifies either name, account, or neither depending on function.
                var id_card_customer = computer.stored_id?.GetID() as obj_item_card_id;
                if (id_card_customer == null)
                    id_card_customer = hwoman?.get_idcard(TRUE) as obj_item_card_id; //Grab from hands/mob if there's no id_card slot to prioritize.
                name = id_card_customer?.registered_account?.account_holder;

                if (Dm.truthy(self_paid))
                {
                    if (id_card_customer == null)
                    {
                        await computer.say(I18n.T("No ID card detected."));
                        return null;
                    }
                    if (Dm.@in(id_card_customer, SSeconomy.dep_cards)) // IS_DEPARTMENTAL_CARD
                    {
                        await computer.say(I18n.T("[] cannot be used to make purchases.", id_card_customer));
                        return null;
                    }
                    account = id_card_customer.registered_account;
                    name = id_card_customer.registered_account?.account_holder;
                    if (account == null)
                    {
                        await computer.say(I18n.T("Invalid bank account."));
                        return null;
                    }
                }

                object reason = "";
                datum_bank_account personal_department = null;
                if ((Dm.truthy(requestonly) && !Dm.truthy(self_paid)) || computer.stored_id?.GetID() == null)
                {
                    reason = await tgui_input_text(user, I18n.T("Reason"), name, max_length: MAX_MESSAGE_LEN);
                    if (reason == null || Dm.truthy(await base.ui_act(action, @params, ui, state)))
                        return null;
                }

                if (id_card_customer?.registered_account?.account_job != null && !Dm.truthy(self_paid)) //Find a budget to pull from
                {
                    personal_department = SSeconomy.get_dep_account(id_card_customer.registered_account.account_job.paycheck_department);
                    if (!Dm.eq(personal_department.account_holder, "Cargo Budget"))
                    {
                        object dept_choice = await tgui_alert(user, I18n.T("Which department are you requesting this for?"), I18n.T("Choose request department"), Dm.list("Cargo Budget", Dm.text("[]", personal_department.account_holder)));
                        if (!Dm.truthy(dept_choice))
                            return null;
                        if (Dm.eq(dept_choice, "Cargo Budget"))
                            personal_department = null;
                    }
                }

                if (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_GOODY)) && !Dm.truthy(self_paid))
                {
                    playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
                    await computer.say(I18n.T("ERROR: Small crates may only be purchased by private accounts."));
                    return null;
                }

                if (Dm.eq(SSshuttle.supply.get_order_count(pack), OVER_ORDER_LIMIT))
                {
                    playsound(computer, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
                    await computer.say(I18n.T("ERROR: No more then [] of any pack may be ordered at once", CARGO_MAX_ORDER));
                    return null;
                }

                if (!Dm.truthy(self_paid))
                    account = personal_department;

                turf T = get_turf(computer);
                var SO = Dm.@new<datum_supply_order>(pack, name, rank, ckey, reason, account);
                if (computer.stored_paper >= 1)
                {
                    SO.generateRequisition(T);
                    computer.stored_paper -= 1;
                    if (computer.stored_paper <= 4)
                    {
                        await computer.say(I18n.T("Paper's storage has only [] papers. Refill please!", computer.stored_paper));
                        if (computer.stored_paper <= 1)
                            await computer.say(I18n.T("Only 1 paper has left, refill please!"));
                    }
                }
                else
                    await computer.say(I18n.T("Requisition cannot be printed, paper storage is empty. Please insert more paper!"));
                if ((Dm.truthy(requestonly) && !Dm.truthy(self_paid)) || computer.stored_id?.GetID() == null)
                    Dm.append(ref SSshuttle.request_list, SO);
                else
                {
                    Dm.append(ref SSshuttle.shopping_list, SO);
                    if (Dm.truthy(self_paid))
                        await computer.say(I18n.T("Order processed. The price will be charged to []'s bank account on delivery.", account.account_holder));
                }
                playsound(computer, Dm.resource("sound/effects/coin2.ogg"), 40, TRUE);
                dot = TRUE;
                break;
            }
            case "remove":
            {
                object id = text2num(@params["id"]);
                foreach (datum_supply_order SO in Dm.each<datum_supply_order>(SSshuttle.shopping_list))
                {
                    if (Dm.eq(SO.id, id))
                    {
                        Dm.remove(ref SSshuttle.shopping_list, SO);
                        dot = TRUE;
                        break;
                    }
                }
                break;
            }
            case "clear":
                foreach (datum_supply_order cancelled_order in Dm.each<datum_supply_order>(SSshuttle.shopping_list.Copy()))
                {
                    if (Dm.truthy(cancelled_order.department_destination) || Dm.truthy(cancelled_order.can_be_cancelled))
                        continue; //don't cancel other department's orders or orders that can't be cancelled
                    Dm.remove(ref SSshuttle.shopping_list, cancelled_order);
                }
                dot = TRUE;
                break;
            case "approve":
            {
                object id = text2num(@params["id"]);
                foreach (datum_supply_order SO in Dm.each<datum_supply_order>(SSshuttle.request_list))
                {
                    if (Dm.eq(SO.id, id))
                    {
                        var id_card = computer.stored_id?.GetID() as obj_item_card_id;
                        if (id_card != null && id_card.registered_account != null)
                            SO.paying_account = SSeconomy.get_dep_account(id_card.registered_account.account_job?.paycheck_department);
                        Dm.remove(ref SSshuttle.request_list, SO);
                        Dm.append(ref SSshuttle.shopping_list, SO);
                        dot = TRUE;
                        break;
                    }
                }
                break;
            }
            case "deny":
            {
                object id = text2num(@params["id"]);
                foreach (datum_supply_order SO in Dm.each<datum_supply_order>(SSshuttle.request_list))
                {
                    if (Dm.eq(SO.id, id))
                    {
                        Dm.remove(ref SSshuttle.request_list, SO);
                        dot = TRUE;
                        break;
                    }
                }
                break;
            }
            case "denyall":
                SSshuttle.request_list.Cut();
                dot = TRUE;
                break;
            case "toggleprivate":
                self_paid = Dm.truthy(self_paid) ? FALSE : TRUE;
                dot = TRUE;
                break;
        }
        if (Dm.truthy(dot))
            post_signal(cargo_shuttle);
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/budgetordering.dm:345
    public virtual void post_signal(object command = null)
    {
        datum_radio_frequency frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS);

        if (frequency == null)
            return;

        var status_signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("command", command)));
        // tg posts with src, the program; the port's post_signal takes the obj that sends (rules 3.2), the program's computer. The
        // status displays read only the command.
        frequency.post_signal(computer, status_signal);
    }
}

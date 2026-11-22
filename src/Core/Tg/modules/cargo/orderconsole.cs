// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/orderconsole.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D299, gaps P4b (D298): the whole file. The shuttle loan's action lends the shuttle since the shuttle loan event (gaps E3, D327).

// tg: code/modules/cargo/orderconsole.dm:1
public partial class obj_machinery_computer_cargo : obj_machinery_computer
{
    // tg: code/modules/cargo/orderconsole.dm:48
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_trade_chip contract)
            return NONE;
        contract.try_to_unlock_contract(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/cargo/orderconsole.dm:55
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        if (user != null)
        {
            if (emag_card != null)
                user.visible_message(span_warning(I18n.T("[] swipes [] through []!", user, emag_card, this)));
            to_chat(user, span_notice(I18n.T("You adjust []'s routing and receiver spectrum, unlocking special supplies and contraband.", this)));
        }

        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        contraband = TRUE;

        // This also permanently sets this on the circuit board
        if (circuit_value is obj_item_circuitboard_computer_cargo board)
        {
            board.contraband = TRUE;
            board.obj_flags = Dm.bitor(board.obj_flags, EMAGGED);
        }
        await update_static_data(user);
        return TRUE;
    }

    // tg: code/modules/cargo/orderconsole.dm:73
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        await base.on_construction(user, __extra);
        circuit_value.configure_machine(this);
    }

    // tg: code/modules/cargo/orderconsole.dm:77
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, interface_type, name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/cargo/orderconsole.dm:84
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        var supply = SSshuttle.supply;
        data["department"] = "Cargo"; // Hardcoded here, for customization in budgetordering.dm AKA NT IRN
        data["location"] = supply.getStatusText();
        datum_bank_account bank = SSeconomy.get_dep_account(cargo_account);
        if (bank != null)
            data["points"] = bank.account_balance;
        data["grocery"] = SSshuttle.chef_groceries.len;
        data["away"] = Dm.eq(supply.getDockedId(), docking_away) ? TRUE : FALSE;
        data["self_paid"] = self_paid;
        data["docked"] = Dm.eq(supply.mode, SHUTTLE_IDLE) ? TRUE : FALSE;
        data["loan"] = SSshuttle.shuttle_loan != null ? TRUE : FALSE;
        data["loan_dispatched"] = SSshuttle.shuttle_loan != null && Dm.truthy(SSshuttle.shuttle_loan.dispatched) ? TRUE : FALSE;
        data["can_send"] = can_send;
        data["can_approve_requests"] = can_approve_requests;
        data["requestonly"] = requestonly;
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

    // tg: code/modules/cargo/orderconsole.dm:148
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["max_order"] = CARGO_MAX_ORDER;
        DmList supplies = Dm.list();

        DmList packs_by_group = get_packs_data_by_group();
        foreach (object group in Dm.each(packs_by_group))
        {
            var available_packs = (DmList)packs_by_group[group];
            if (!Dm.truthy(length(available_packs))) // Somehow????
                continue;
            supplies[group] = Dm.list(Dm.kv("name", group), Dm.kv("packs", available_packs));
        }
        data["supplies"] = supplies;

        data["displayed_currency_full_name"] = Dm.text(" []", MONEY_NAME);
        data["displayed_currency_name"] = Dm.text(" []", MONEY_SYMBOL);
        return data;
    }

    /**
     * returns a list of supply pack ui data by group
     */
    // tg: code/modules/cargo/orderconsole.dm:171
    public virtual DmList get_packs_data_by_group()
    {
        DmList packs_by_group = Dm.list();
        foreach (object pack_id in Dm.each(SSshuttle.supply_packs))
        {
            var pack = (datum_supply_pack)SSshuttle.supply_packs[pack_id];

            if (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_INVISIBLE)))
                continue;

            if (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_EMAG_ONLY)) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                continue;
            if (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_SPECIAL)) && !Dm.truthy(Dm.bitand(pack.order_flags, ORDER_SPECIAL_ENABLED)))
                continue;

            if (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_CONTRABAND)) && !Dm.truthy(contraband))
                continue;

            if (!Dm.truthy(is_express) && Dm.truthy(Dm.bitand(pack.order_flags, ORDER_POD_ONLY)))
                continue;

            object first_item = length(pack.contains) > 0 ? pack.contains[1] : null;
            var packs = packs_by_group[pack.group] as DmList;
            if (packs == null)
            {
                packs = Dm.list();
                packs_by_group[pack.group] = packs;
            }

            Dm.append(ref packs, Dm.list(Dm.list(
                Dm.kv("name", pack.name),
                Dm.kv("cost", pack.get_cost() * get_discount()),
                Dm.kv("id", Dm.text("[]", pack_id)),
                Dm.kv("desc", Dm.truthy(pack.desc) ? pack.desc : pack.name), // If there is a description, use it. Otherwise use the pack's name.
                Dm.kv("first_item_icon", first_item == null ? null : Dm.text("[]", Dm.initial(first_item, "icon"))),
                Dm.kv("first_item_icon_state", first_item == null ? null : Dm.initial(first_item, "icon_state")),
                Dm.kv("goody", Dm.bitand(pack.order_flags, ORDER_GOODY)),
                Dm.kv("access", pack.access),
                Dm.kv("contraband", Dm.bitand(pack.order_flags, ORDER_CONTRABAND)),
                Dm.kv("contains", pack.get_contents_ui_data()))));
        }
        return packs_by_group;
    }

    /**
     * returns the discount multiplier applied to all supply packs,
     * the discount is calculated as follows: pack_cost * get_discount()
     */
    // tg: code/modules/cargo/orderconsole.dm:215
    public virtual float get_discount() => 1;

    /**
     * adds an supply pack to the checkout cart
     * * user - the mobe doing this order
     * * id - the type of pack to order
     * * amount - the amount to order. You may not order more then 10 things at once
     */
    // tg: code/modules/cargo/orderconsole.dm:224
    public virtual async DmTask<object> add_item(mob user = null, object id = null, float amount = 1)
    {
        if (Dm.truthy(is_express))
            return null;
        id = (object)text2path(Dm.str(id)) ?? id;
        if (SSshuttle.supply_packs[id] is not datum_supply_pack pack)
            throw Dm.CRASH(Dm.text("Unknown supply pack id given by order console ui. ID: []", id), "code/modules/cargo/orderconsole.dm", 230);
        if (amount > CARGO_MAX_ORDER || amount < 1) // Holy shit fuck off
            throw Dm.CRASH("Invalid amount passed into add_item", "code/modules/cargo/orderconsole.dm", 232);

        if ((Dm.truthy(Dm.bitand(pack.order_flags, ORDER_EMAG_ONLY)) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED))) || (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_CONTRABAND)) && !Dm.truthy(contraband)) || Dm.truthy(Dm.bitand(pack.order_flags, ORDER_POD_ONLY)) || (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_SPECIAL)) && !Dm.truthy(Dm.bitand(pack.order_flags, ORDER_SPECIAL_ENABLED))))
            return null;

        object orderer_name = "*None Provided*";
        object rank = "*None Provided*";
        object ckey = user.ckey;
        if (user is mob_living_carbon_human human)
        {
            orderer_name = human.get_authentification_name();
            rank = human.get_assignment(hand_first: TRUE);
        }
        else if (HAS_SILICON_ACCESS(user))
        {
            orderer_name = user.real_name;
            rank = "Silicon";
        }

        datum_bank_account account = null;

        if (user is mob_living living_user)
        {
            var id_card = living_user.get_idcard(TRUE) as obj_item_card_id;

            bool bypass = id_card is obj_item_card_id_advanced_chameleon; //We'll bypass access restrictions

            account = id_card?.registered_account; // We can still assign an account for request department purposes.
            if (Dm.truthy(self_paid))
            {
                if (id_card == null)
                {
                    await say(I18n.T("No ID card detected."));
                    return null;
                }
                if (Dm.@in(id_card, SSeconomy.dep_cards)) // IS_DEPARTMENTAL_CARD
                {
                    await say(I18n.T("The [] rejects [].", this, id_card));
                    return null;
                }
                if (account == null)
                {
                    await say(I18n.T("Invalid bank account."));
                    return null;
                }
                var access = id_card.GetAccess() as DmList;
                if ((Dm.truthy(pack.access_view) && !Dm.@in(pack.access_view, access)) && !bypass)
                {
                    await say(I18n.T("[] lacks the requisite access for this purchase.", id_card));
                    return null;
                }
            }
        }

        // The list we are operating on right now
        bool to_requests = false;
        object reason = "";
        datum_bank_account personal_department = null;
        if (Dm.truthy(requestonly) && !Dm.truthy(self_paid) && !Dm.truthy(Dm.bitand(pack.order_flags, ORDER_GOODY)))
        {
            to_requests = true;
            reason = await tgui_input_text(user, I18n.T("Reason"), name, max_length: MAX_MESSAGE_LEN);
            if (reason == null)
                return null;

            orderer_name = account?.account_holder;
            if (account?.account_job != null)
            {
                personal_department = SSeconomy.get_dep_account(account.account_job.paycheck_department);
                if (!Dm.eq(personal_department.account_holder, "Cargo Budget"))
                {
                    object dept_choice = await tgui_alert(user, I18n.T("Which department are you requesting this for?"), I18n.T("Choose department to request from"), Dm.list("Cargo Budget", Dm.text("[]", personal_department.account_holder)));
                    if (!Dm.truthy(dept_choice))
                        return null;
                    if (Dm.eq(dept_choice, "Cargo Budget"))
                        personal_department = null;
                }
            }

            if (user is mob_living requester)
            {
                var id_card = requester.get_idcard(TRUE) as obj_item_card_id;
                var access = id_card?.GetAccess() as DmList;
                if (Dm.truthy(pack.access_view) && !Dm.@in(pack.access_view, access) && personal_department != null)
                {
                    // We want to block cargo requests when a player is requesting a restricted pack that they don't have access to.
                    // BUT only when it's requested with non-cargo funds, as cargo had direct oversight over their own purchases with their own budget.
                    // HOWEVER, this shouldn't prevent someone from buying something using their own personal funds.
                    await say(I18n.T("ERROR: User lacks the requisite access for this purchase request."));
                    return null;
                }
            }
        }

        if (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_GOODY)) && !Dm.truthy(self_paid))
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            await say(I18n.T("ERROR: Small crates may only be purchased by private accounts."));
            return null;
        }

        object similar_count = SSshuttle.supply.get_order_count(pack);
        if (Dm.eq(similar_count, OVER_ORDER_LIMIT))
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
            await say(I18n.T("ERROR: No more then [] of any pack may be ordered at once", CARGO_MAX_ORDER));
            return null;
        }

        if (!Dm.truthy(self_paid))
            account = personal_department;

        amount = Math.Clamp(amount, 1, CARGO_MAX_ORDER - Dm.num(similar_count));
        for (float count = 1; count <= amount; count++)
        {
            obj_item_coupon applied_coupon = null;
            foreach (obj_item_coupon coupon_check in Dm.each<obj_item_coupon>(loaded_coupons))
            {
                if (pack.GetType() == coupon_check.discounted_pack as Type)
                {
                    await say(I18n.T("Coupon found! []% off applied!", round(Dm.num(coupon_check.discount_pct_off) * 100)));
                    await coupon_check.moveToNullspace();
                    applied_coupon = coupon_check;
                    break;
                }
            }

            var order = Dm.@new<datum_supply_order>(pack, orderer_name, rank, ckey, reason, account, null, applied_coupon);
            if (to_requests)
                Dm.append(ref SSshuttle.request_list, order);
            else
                Dm.append(ref SSshuttle.shopping_list, order);
        }

        if (Dm.truthy(self_paid))
            await say(I18n.T("Order processed. The price will be charged to []'s bank account on delivery.", account.account_holder));
        if (Dm.truthy(requestonly) && Dm.num(message_cooldown) < Dm.world.time)
        {
            await aas_config_announce(typeof(datum_aas_config_entry_cargo_orders_announcement), Dm.list(Dm.kv("AMOUNT", amount)), this, Dm.list(RADIO_CHANNEL_SUPPLY), amount == 1 ? "Single Order" : "Multiple Orders");
            message_cooldown = Dm.world.time + 30 * SECONDS;
        }
        return TRUE;
    }

    /**
     * removes an item from the checkout cart
     * * id - the id of the cart item to remove
     */
    // tg: code/modules/cargo/orderconsole.dm:355
    public virtual async DmTask<bool> remove_item(object id = null)
    {
        foreach (datum_supply_order order in Dm.each<datum_supply_order>(SSshuttle.shopping_list))
        {
            if (!Dm.eq(order.id, id))
                continue;
            if (Dm.truthy(order.department_destination))
            {
                await say(I18n.T("Only the department that ordered this item may cancel it."));
                return false;
            }
            if (order.applied_coupon != null)
            {
                await say(I18n.T("Coupon refunded."));
                await order.applied_coupon.forceMove(get_turf(this));
                order.applied_coupon = null; // DEVIATION(DV408): tg's qdel(order) below deletes the refunded coupon (supply_order/Destroy)
            }
            Dm.remove(ref SSshuttle.shopping_list, order);
            qdel(order);
            return true;
        }
        return false;
    }

    /**
     * maps the ordename displayed on the ui to its supply pack id
     * * order_name - the name of the order
     */
    // tg: code/modules/cargo/orderconsole.dm:373
    public virtual object name_to_id(object order_name = null)
    {
        foreach (object pack in Dm.each(SSshuttle.supply_packs))
        {
            var supply = (datum_supply_pack)SSshuttle.supply_packs[pack];
            if (Dm.eq(order_name, supply.name))
                return pack;
        }
        return null;
    }

    // tg: code/modules/cargo/orderconsole.dm:380
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "send":
                if (!Dm.truthy(SSshuttle.supply.canMove()))
                {
                    await say(safety_warning);
                    return null;
                }
                if (Dm.truthy(SSshuttle.supply_blocked))
                {
                    await say(blockade_warning);
                    return null;
                }

                if (Dm.eq(SSshuttle.supply.getDockedId(), docking_home))
                {
                    await SSshuttle.moveShuttle(cargo_shuttle, docking_away, TRUE);
                    await say(I18n.T("The supply shuttle is departing."));
                    ui.user.investigate_log("sent the supply shuttle away.", INVESTIGATE_CARGO);
                }
                else
                {
                    //create the paper from the SSshuttle.shopping_list
                    if (Dm.truthy(length(SSshuttle.shopping_list)))
                    {
                        var requisition_paper = Dm.@new<obj_item_paper_requisition>(get_turf(this));
                        requisition_paper.name = I18n.T("requisition form - [] (PT: [])", server_timestamp(ic_time: TRUE), round_timestamp());
                        string requisition_text = I18n.T("<h2>[] Supply Requisition</h2>", station_name());
                        requisition_text += "<hr/>";
                        requisition_text += I18n.T("Time of Order: []<br/><br/>", UNDERLINED_HTML_TEXT(Dm.text("[]", server_timestamp(ic_time: TRUE)), I18n.T("Shift Time: []", round_timestamp())));
                        foreach (datum_supply_order order in Dm.each_cast<datum_supply_order>(SSshuttle.shopping_list))
                        {
                            requisition_text += Dm.text("<b>[]</b></br>", order.pack.name);
                            requisition_text += I18n.T("- Order ID: []</br>", order.id);
                            object restrictions = SSid_access.get_access_desc(order.pack.access);
                            if (Dm.truthy(restrictions))
                                requisition_text += I18n.T("- Access Restrictions: []</br>", restrictions);
                            requisition_text += I18n.T("- Ordered by: [] ([])</br>", order.orderer, order.orderer_rank);
                            var paying_account = order.paying_account;
                            if (paying_account != null)
                                requisition_text += I18n.T("- Paid Privately by: []<br/>", order.paying_account.account_holder);
                            object reason = order.reason;
                            if (Dm.truthy(reason))
                                requisition_text += I18n.T("- Reason Given: []</br>", reason);
                            requisition_text += "</br></br>";
                        }
                        requisition_paper.add_raw_text(requisition_text, advanced_html: TRUE);
                        requisition_paper.color = "#9ef5ff";
                        requisition_paper.update_appearance();
                    }

                    ui.user.investigate_log("called the supply shuttle.", INVESTIGATE_CARGO);
                    await say(I18n.T("The supply shuttle has been called and will arrive in [] minute\\s.", SSshuttle.supply.timeLeft(600)));
                    await SSshuttle.moveShuttle(cargo_shuttle, docking_home, TRUE);
                }

                dot = TRUE;
                break;
            case "loan":
                if (SSshuttle.shuttle_loan == null)
                    return null;
                if (Dm.truthy(SSshuttle.supply_blocked))
                {
                    await say(blockade_warning);
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
                    await say(I18n.T("The supply shuttle has been loaned to CentCom."));
                    ui.user.investigate_log("accepted a shuttle loan event.", INVESTIGATE_CARGO);
                    ui.user.log_message("accepted a shuttle loan event.", LOG_GAME);
                    dot = TRUE;
                }
                break;
            case "add":
                return await add_item(ui.user, @params["id"]);
            case "add_by_name":
                object supply_pack_id = name_to_id(@params["order_name"]);
                if (!Dm.truthy(supply_pack_id))
                    return null;
                return await add_item(ui.user, supply_pack_id);
            case "remove":
            {
                object order_name = @params["order_name"];
                //try removing at least one item with the specified name. An order may not be removed if it was from the department
                foreach (datum_supply_order order in Dm.each<datum_supply_order>(SSshuttle.shopping_list.Copy()))
                {
                    if (!Dm.eq(order.pack.name, order_name))
                        continue;
                    if (await remove_item(order.id))
                        return TRUE;
                }
                return TRUE;
            }
            case "modify":
            {
                object order_name = @params["order_name"];

                //clear out all orders with the above mentioned order_name name to make space for the new amount
                foreach (datum_supply_order order in Dm.each<datum_supply_order>(SSshuttle.shopping_list.Copy())) //find corresponding order id for the order name
                {
                    if (Dm.eq(order.pack.name, order_name))
                        await remove_item(order.id);
                }

                //now add the new amount stuff
                float amount = Dm.num(text2num(@params["amount"]));
                if (amount == 0)
                    return TRUE;
                if (amount > CARGO_MAX_ORDER)
                    return null;
                object modified_pack_id = name_to_id(order_name); //map order name to supply pack id for adding
                if (!Dm.truthy(modified_pack_id))
                    return null;
                return await add_item(ui.user, modified_pack_id, amount);
            }
            case "clear":
                //create copy of list else we will get runtimes when iterating & removing items on the same list SSshuttle.shopping_list
                foreach (datum_supply_order cancelled_order in Dm.each<datum_supply_order>(SSshuttle.shopping_list.Copy()))
                {
                    if (Dm.truthy(cancelled_order.department_destination) || !Dm.truthy(cancelled_order.can_be_cancelled))
                        continue; //don't cancel other department's orders or orders that can't be cancelled
                    await remove_item(cancelled_order.id); //remove & properly refund any coupons attached with this order
                }
                break;
            case "approve":
            {
                object id = text2num(@params["id"]);
                foreach (datum_supply_order SO in Dm.each<datum_supply_order>(SSshuttle.request_list))
                {
                    if (Dm.eq(SO.id, id))
                    {
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

    // tg: code/modules/cargo/orderconsole.dm:513
    public virtual void post_signal(object command = null)
    {
        datum_radio_frequency frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS);

        if (frequency == null)
            return;

        var status_signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("command", command)));
        frequency.post_signal(this, status_signal);
    }
}

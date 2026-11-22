// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/orders/order_computer/order_computer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the order console base (its ProduceConsole window): the cart, the cargo and express purchases,
// the free uses, and the order consoles' announcement entry. The var blocks are generated (D111).

///List of all items that can be found in the different types of order consoles, to purchase.
// tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:2 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:2  GLOBAL_LIST_EMPTY(order_console_products)
    public DmList order_console_products;

    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:2  (GLOBAL_MANAGED(order_console_products, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalorder_console_products()
    {
        order_console_products = Dm.list();
        Dm.append(ref gvars_datum_init_order, "order_console_products");
    }
}

// tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:4
public partial class obj_machinery_computer_order_console : obj_machinery_computer
{
    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:46
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(GLOB.order_console_products.len))
            return dot;
        foreach (Type path in Dm.each_cast<Type>(subtypesof(typeof(datum_orderable_item))))
        {
            if (Dm.initial(path, (datum_orderable_item p) => p.purchase_path) == null)
                continue;
            Dm.append(ref GLOB.order_console_products, Dm.@new(path));
        }
        return dot;
    }

    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:56
    public virtual float get_total_cost()
    {
        float cost = 0;
        float free_uses_remaining = Dm.num(free_uses ?? 0f);
        foreach (datum_orderable_item item in Dm.each_cast<datum_orderable_item>(grocery_list))
        {
            float amount = Dm.num(grocery_list[item]);
            if (free_uses_remaining >= amount)
                free_uses_remaining -= amount;
            else if (free_uses_remaining >= 1)
            {
                cost += (amount - free_uses_remaining) * item.cost_per_order;
                free_uses_remaining = 0;
            }
            else
                cost += amount * item.cost_per_order;
        }
        return cost;
    }

    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:69
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ProduceConsole", name);
            await ui.open();
        }
        return dot;
    }

    /**
     * points is any type of currency this machine accepts(money, mining points etc) which is displayed on the ui
     * Args:
     * card - The ID card we retrieve these points from
     */
    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:81
    public virtual float retrieve_points(obj_item_card_id id_card = null) => round(id_card.registered_account?.account_balance ?? 0);

    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:84
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["total_cost"] = get_total_cost();
        data["free_uses"] = free_uses;
        data["off_cooldown"] = world.time >= order_cooldown ? TRUE : FALSE; // COOLDOWN_FINISHED

        DmList item_amts = null;
        foreach (datum_orderable_item item in Dm.each_cast<datum_orderable_item>(GLOB.order_console_products))
        {
            if (!Dm.@in(item.category_index, order_categories))
                continue;
            item_amts ??= Dm.list();
            Dm.append(ref item_amts, Dm.list(Dm.list(
                Dm.kv("name", item.name),
                Dm.kv("amt", grocery_list[item]))));
        }
        data["item_amts"] = item_amts;
        if (user is mob_living living_user)
        {
            var id_card = living_user.get_idcard(TRUE) as obj_item_card_id;
            if (id_card != null)
                data["points"] = retrieve_points(id_card);
        }

        return data;
    }

    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:105
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["credit_type"] = credit_type;
        data["express_tooltip"] = express_tooltip;
        data["purchase_tooltip"] = purchase_tooltip;
        data["forced_express"] = forced_express;
        data["cargo_value"] = CARGO_CRATE_VALUE;
        data["cargo_cost_multiplier"] = cargo_cost_multiplier;
        data["express_cost_multiplier"] = express_cost_multiplier;
        data["order_categories"] = order_categories;
        DmList order_datums = Dm.list();
        foreach (datum_orderable_item item in Dm.each_cast<datum_orderable_item>(GLOB.order_console_products))
        {
            if (!Dm.@in(item.category_index, order_categories))
                continue;

            Dm.append(ref order_datums, Dm.list(Dm.list(
                Dm.kv("name", item.name),
                Dm.kv("desc", item.desc),
                Dm.kv("cat", item.category_index),
                Dm.kv("ref", REF(item)),
                Dm.kv("cost", round(item.cost_per_order * cargo_cost_multiplier)),
                Dm.kv("icon", Dm.initial(item.purchase_path, "icon")),
                Dm.kv("icon_state", Dm.initial(item.purchase_path, "icon_state")),
                Dm.kv("max", item.max_per_order))));
        }
        data["order_datums"] = order_datums;
        return data;
    }

    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:132
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (!isliving(usr))
            return dot;
        var living_user = (mob_living)usr;
        switch (Dm.str(action))
        {
            case "add_one":
            {
                var wanted_item = Dm.locate_ref(@params["target"]) as datum_orderable_item;
                if (!Dm.@in(wanted_item, GLOB.order_console_products))
                    wanted_item = null;
                if (Dm.num(grocery_list[wanted_item]) >= Dm.num(wanted_item.max_per_order))
                    return dot;
                else
                    grocery_list[wanted_item] = Dm.num(grocery_list[wanted_item]) + 1;
                break;
            }
            case "remove_one":
            {
                var wanted_item = Dm.locate_ref(@params["target"]) as datum_orderable_item;
                if (!Dm.@in(wanted_item, GLOB.order_console_products))
                    wanted_item = null;
                if (!Dm.truthy(grocery_list[wanted_item]))
                    return dot;
                if (Dm.num(grocery_list[wanted_item]) < 1)
                {
                    grocery_list[wanted_item] = 0f;
                    return dot;
                }
                else
                    grocery_list[wanted_item] = Dm.num(grocery_list[wanted_item]) - 1;
                if (!Dm.truthy(grocery_list[wanted_item]))
                    Dm.remove(ref grocery_list, wanted_item);
                break;
            }
            case "cart_set":
            {
                //this is null if the action doesn't need it (purchase, quickpurchase)
                var wanted_item = Dm.locate_ref(@params["target"]) as datum_orderable_item;
                if (!Dm.@in(wanted_item, GLOB.order_console_products))
                    wanted_item = null;
                grocery_list[wanted_item] = clamp(Dm.num(@params["amt"]), 0, Dm.num(wanted_item.max_per_order));
                if (!Dm.truthy(grocery_list[wanted_item]))
                    Dm.remove(ref grocery_list, wanted_item);
                break;
            }
            case "purchase":
            {
                if (!Dm.truthy(grocery_list.len) || !(world.time >= order_cooldown))
                    return dot;
                if (Dm.truthy(forced_express))
                    return await ui_act(action: "express", @params, ui, state);
                //So miners cant spam buy crates for a very low price
                if (get_total_cost() < CARGO_CRATE_VALUE)
                    return dot;

                var used_id_card = living_user.get_idcard(TRUE) as obj_item_card_id;
                if (used_id_card?.registered_account == null)
                {
                    await say(I18n.T("No bank account detected!"));
                    return dot;
                }
                if (!await purchase_items(used_id_card))
                    return dot;
                if (Dm.truthy(blackbox_key))
                    SSblackbox.record_feedback("tally", Dm.text("non_express_[]_order", blackbox_key), 1, name);
                await order_groceries(living_user, used_id_card, grocery_list);
                grocery_list.Cut();
                order_cooldown = world.time + Dm.num(cooldown_time); // COOLDOWN_START
                break;
            }
            case "express":
            {
                if (!Dm.truthy(grocery_list.len) || !(world.time >= order_cooldown))
                    return dot;
                var used_id_card = living_user.get_idcard(TRUE) as obj_item_card_id;
                if (used_id_card?.registered_account == null)
                {
                    await say(I18n.T("No bank account detected!"));
                    return dot;
                }
                if (!await purchase_items(used_id_card, express: true))
                    return dot;
                string say_message = I18n.T("Thank you for your purchase!");
                if (express_cost_multiplier > 1)
                    say_message += I18n.T(" Please note: The charge of this purchase and machine cooldown has been multiplied by []!", express_cost_multiplier);
                order_cooldown = world.time + Dm.num(cooldown_time) * express_cost_multiplier; // COOLDOWN_START
                await say(say_message);
                if (Dm.truthy(blackbox_key))
                    SSblackbox.record_feedback("tally", Dm.text("express_[]_order", blackbox_key), 1, name);
                DmList ordered_paths = Dm.list();
                foreach (datum_orderable_item item in Dm.each_cast<datum_orderable_item>(grocery_list.Copy())) //every order
                {
                    if (!Dm.@in(item.category_index, order_categories))
                    {
                        stack_trace(Dm.text("[] somehow delivered [] which is not purchasable at this order console.", this, item), "code/game/machinery/computer/orders/order_computer/order_computer.dm", 196);
                        grocery_list.Remove(item);
                        continue;
                    }
                    object purchase_path = item.get_purchased_item(living_user, used_id_card);
                    for (int amt = 1; amt <= Dm.num(grocery_list[item]); amt++) //every order amount
                        Dm.append(ref ordered_paths, purchase_path);
                }
                await podspawn(Dm.list(
                    Dm.kv("target", get_turf(living_user)),
                    Dm.kv("style", typeof(datum_pod_style_advanced)),
                    Dm.kv("spawn", ordered_paths)));
                grocery_list.Cut();
                break;
            }
        }
        return TRUE;
    }

    /**
     * Checks if an ID card is able to afford the total cost of the current console's groceries
     * and deducts the cost if they can.
     * Args:
     * card - The ID card we check for balance
     * express - Boolean on whether we need to add the express cost mulitplier
     * returns TRUE if we can afford, FALSE otherwise.
     */
    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:224
    public virtual async DmTask<bool> purchase_items(obj_item_card_id card = null, bool express = false)
    {
        float final_cost = round(get_total_cost() * (express ? express_cost_multiplier : cargo_cost_multiplier));
        if (final_cost > 0 && !Dm.truthy(subtract_points(final_cost, card)))
        {
            await say(I18n.T("Sorry, but you do not have enough [].", credit_type));
            return false;
        }
        if (isnum(free_uses)) // Factored in cost already
            free_uses = Math.Max(0, Dm.num(free_uses) - Dm.num(Dm.values_sum(grocery_list)));
        return true;
    }

    /**
     * whatever type of points was retrieved in retrieve_points() subtract those type of points from the card upon confirming order
     * Args:
     * final_cost - amount of points to subtract from this card
     * card - The ID card to subtract these points from
     * returns TRUE if successfull
     */
    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:240
    public virtual object subtract_points(float final_cost = 0, obj_item_card_id card = null)
        => card.registered_account.adjust_money(-final_cost, I18n.T("[]: Purchase", name));

    /**
     * start of the shipment of your order
     * Args:
     * purchaser - The mob who is making this purchase
     * card - The card used to place this order
     * groceries - the list of orders to be placed
     */
    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:250
    public virtual async DmTask order_groceries(mob_living purchaser = null, obj_item_card_id card = null, DmList groceries = null)
    {
        return;
    }
}

// tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:253
public partial class datum_aas_config_entry_order_console : datum_aas_config_entry
{
    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:258
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        foreach (Type subconsole in Dm.each_cast<Type>(subtypesof(typeof(obj_machinery_computer_order_console))))
        {
            object key = Dm.initial(subconsole, (obj_machinery_computer_order_console p) => p.blackbox_key);
            if (Dm.truthy(key))
                announcement_lines_map[capitalize(key)] = Dm.initial(subconsole, (obj_machinery_computer_order_console p) => p.announcement_line);
        }
    }

    // tg: code/game/machinery/computer/orders/order_computer/order_computer.dm:264
    public override object compile_announce(DmList variables_map = null, object announcement_line = null)
    {
        if (!Dm.truthy(announcement_lines_map.len))
            announcement_lines_map["Error"] = I18n.T("Unknown Error happened, while we tried to procceed an order, please report this to Nanotrasen.");
        return base.compile_announce(variables_map, announcement_line);
    }
}

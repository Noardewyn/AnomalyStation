// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/materials_market.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the stock block (:345-392, gaps P4b, D327), which the material exports sell at its locked or market value, and the
// galactic materials market console with its MatMarket window (gaps P4d). The var blocks are generated.

// tg: code/modules/cargo/materials_market.dm:5
public partial class obj_machinery_materials_market : obj_machinery
{
    /// The maximum number of stacks you can place in 1 order
    // tg: code/modules/cargo/materials_market.dm:2  (file-local, #undef at the end)
    private const float MAX_STACK_LIMIT = 10;
    /// The order rank for all galactic material market orders
    // tg: code/modules/cargo/materials_market.dm:4
    private const string GALATIC_MATERIAL_ORDER = "Galactic Materials Market";

    // tg: code/modules/cargo/materials_market.dm:23
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]_open", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (!Dm.truthy(is_operational) || !Dm.truthy(anchored))
        {
            icon_state = Dm.text("[]_off", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[]", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/cargo/materials_market.dm:33
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (Dm.eq(await default_unfasten_wrench(user, tool, time: 1.5f * SECONDS), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return dot;
    }

    // tg: code/modules/cargo/materials_market.dm:38
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null) => default_deconstruction_screwdriver(user, tool);

    // tg: code/modules/cargo/materials_market.dm:41
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null) => await default_deconstruction_crowbar(user, tool);

    // tg: code/modules/cargo/materials_market.dm:44
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack exportable)
            return NONE;

        if (!Dm.truthy(is_operational))
        {
            balloon_alert(user, I18n.T("no power!"));
            return ITEM_INTERACT_FAILURE;
        }

        DmList materials = exportable.custom_materials;
        if (materials.len != 1)
        {
            balloon_alert(user, I18n.T("alloy stacks not allowed"));
            return ITEM_INTERACT_FAILURE;
        }

        object price = SSstock_market.materials_prices[((datum)materials[1]).GetType()];
        if (!Dm.truthy(price))
        {
            balloon_alert(user, I18n.T("materials in stack are worthless"));
            return ITEM_INTERACT_FAILURE;
        }

        if (!Dm.truthy(await user.transferItemToLoc(exportable, this)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck in hand!", exportable)));
            return ITEM_INTERACT_FAILURE;
        }

        var new_block = Dm.@new<obj_item_stock_block>(drop_location());
        new_block.export_value = Dm.num(price);
        await new_block.set_custom_materials(materials);
        to_chat(user, span_notice(I18n.T("You have created a stock block worth [] []! Sell it before it becomes liquid!", new_block.export_value * Dm.num(exportable.amount), MONEY_SYMBOL)));
        playsound(this, Dm.resource("sound/machines/synth/synth_yes.ogg"), 50, FALSE);
        qdel(exportable);
        await use_energy(active_power_usage);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/cargo/materials_market.dm:75
    public override object power_change()
    {
        object dot = base.power_change();
        if (!Dm.truthy(is_operational))
            Waiting.NoSleep(set_light(0, 0));
        else
            Waiting.NoSleep(set_light(Dm.initial(this, nameof(light_range)), Dm.initial(this, nameof(light_power))));
        return dot;
    }

    /**
     * Find the order purchased either privately or by cargo budget
     * Arguments
     * * [user][mob] - the user who placed this order
     * * is_ordering_private - is the player ordering privatly. If FALSE it means they are using cargo budget
     */
    // tg: code/modules/cargo/materials_market.dm:88
    public virtual datum_supply_order find_order(mob user = null, object is_ordering_private = null)
    {
        foreach (datum_supply_order order in Dm.each<datum_supply_order>(SSshuttle.shopping_list))
        {
            // Must be a Galactic Materials Market order and payed by the null account(if ordered via cargo budget) or by correct user for private purchase
            if (Dm.eq(order.orderer_rank, GALATIC_MATERIAL_ORDER) && (
                (!Dm.truthy(is_ordering_private) && order.paying_account == null) ||
                (Dm.truthy(is_ordering_private) && order.paying_account != null && Dm.eq(order.orderer, user))))
                return order;
        }
        return null;
    }

    // tg: code/modules/cargo/materials_market.dm:98
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(anchored))
            return null;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "MatMarket", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/cargo/materials_market.dm:106
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["CARGO_CRATE_VALUE"] = CARGO_CRATE_VALUE;
        return dot;
    }

    // tg: code/modules/cargo/materials_market.dm:110
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();

        //can this player use cargo budget
        bool can_buy_via_budget = false;
        obj_item_card_id used_id_card = null;
        if (user is mob_living living_user)
        {
            used_id_card = living_user.get_idcard(TRUE) as obj_item_card_id;
            can_buy_via_budget = Dm.@in(ACCESS_CARGO, used_id_card?.GetAccess() as DmList);
        }

        //if no cargo access then force private purchase
        bool is_ordering_private = Dm.truthy(ordering_private) || !can_buy_via_budget;

        //find current order based on ordering mode & player
        datum_supply_order current_order = find_order(user, is_ordering_private ? TRUE : FALSE);

        DmList material_data = Dm.list();
        string trend_string;
        object color_string;
        Type sheet_to_buy;
        object requested_amount;
        float minimum_value_threshold = 0;
        float maximum_value_threshold = 0;
        float elastic_mult = 1;
        foreach (Type traded_mat in Dm.each_cast<Type>(SSstock_market.materials_prices))
        {
            //convert trend into text
            switch ((int)Dm.num(SSstock_market.materials_trends[traded_mat]))
            {
                case 0:
                    trend_string = "neutral";
                    break;
                case 1:
                    trend_string = "up";
                    break;
                default:
                    trend_string = "down";
                    break;
            }

            //get mat color
            object initial_colors = Dm.initial(traded_mat, (datum_material p) => p.greyscale_color) ?? Dm.initial(traded_mat, (datum_material p) => p.color);
            if (Dm.truthy(initial_colors))
                color_string = splicetext(initial_colors, 7, Dm.num(length(initial_colors)), ""); //slice it to a standard 6 char hex
            else
            {
                initial_colors = Dm.initial(traded_mat, (datum_material p) => p.color);
                if (Dm.truthy(initial_colors))
                    color_string = initial_colors;
                else
                    color_string = COLOR_CYAN;
            }

            //get sheet type from material
            sheet_to_buy = Dm.initial(traded_mat, (datum_material p) => p.sheet_type);
            if (sheet_to_buy == null)
                throw Dm.CRASH("Material with no sheet type being sold on materials market!", "code/modules/cargo/materials_market.dm", 156);

            //get the ordered amount from the order
            requested_amount = 0f;
            if (current_order != null)
                requested_amount = current_order.pack.contains[sheet_to_buy];

            object min_value_override = Dm.initial(traded_mat, (datum_material p) => p.minimum_value_override);
            if (Dm.truthy(min_value_override))
                minimum_value_threshold = Dm.num(min_value_override);
            else
                minimum_value_threshold = round(Dm.initial(traded_mat, (datum_material p) => p.value_per_unit) * SHEET_MATERIAL_AMOUNT * 0.5f);

            maximum_value_threshold = round(Dm.initial(traded_mat, (datum_material p) => p.value_per_unit) * SHEET_MATERIAL_AMOUNT * 3);

            //Pulling elastic modifier into data.
            foreach (datum_export_material_market export_est in Dm.each<datum_export_material_market>(GLOB.exports_list))
            {
                if (export_est.material_id == traded_mat)
                    elastic_mult = export_est.k_elasticity * 100;
            }

            Dm.append(ref material_data, Dm.list(Dm.list(
                Dm.kv("name", Dm.initial(traded_mat, (datum_material p) => p.name)),
                Dm.kv("price", SSstock_market.materials_prices[traded_mat]),
                Dm.kv("rarity", Dm.initial(traded_mat, (datum_material p) => p.value_per_unit)),
                Dm.kv("min_threshold", minimum_value_threshold),
                Dm.kv("max_threshold", maximum_value_threshold),
                Dm.kv("quantity", SSstock_market.materials_quantity[traded_mat]),
                Dm.kv("trend", trend_string),
                Dm.kv("color", color_string),
                Dm.kv("requested", requested_amount),
                Dm.kv("elastic", elastic_mult))));
        }

        //get account balance
        object balance = 0f;
        if (!Dm.truthy(ordering_private))
        {
            datum_bank_account dept = SSeconomy.get_dep_account(ACCOUNT_CAR);
            if (dept != null)
                balance = dept.account_balance;
        }
        else
            balance = used_id_card?.registered_account?.account_balance;

        //is market crashing
        bool market_crashing = HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING);

        //get final order cost
        float current_cost = 0;
        if (current_order != null)
            current_cost = current_order.get_final_cost();

        //pack data
        dot["catastrophe"] = market_crashing ? TRUE : FALSE;
        dot["materials"] = material_data;
        dot["creditBalance"] = balance;
        dot["orderBalance"] = current_cost;
        dot["orderingPrive"] = ordering_private;
        dot["canOrderCargo"] = can_buy_via_budget ? TRUE : FALSE;
        dot["updateTime"] = SSstock_market.next_fire - world.time;
        return dot;
    }

    // tg: code/modules/cargo/materials_market.dm:220
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        //You must have an ID to be able to do something
        var living_user = (mob_living)ui.user;
        var used_id_card = living_user.get_idcard(TRUE) as obj_item_card_id;
        if (used_id_card == null)
        {
            await say(I18n.T("No ID Found"));
            return dot;
        }
        bool can_buy_via_budget = Dm.@in(ACCESS_CARGO, used_id_card?.GetAccess() as DmList);

        //if multiple users open the UI some of them may not have the required access so we recheck
        bool is_ordering_private = Dm.truthy(ordering_private);
        if (!can_buy_via_budget) //no cargo access then force private purchase
            is_ordering_private = true;

        switch (Dm.str(action))
        {
            case "buy":
            {
                object material_str = @params["material"];
                float quantity = Dm.num(text2num(@params["quantity"]));

                //find material from its name
                Type material_bought = null;
                foreach (Type mat in Dm.each_cast<Type>(SSstock_market.materials_prices))
                {
                    if (Dm.eq(Dm.initial(mat, (datum_material p) => p.name), material_str))
                    {
                        material_bought = mat;
                        break;
                    }
                }
                if (material_bought == null)
                    throw Dm.CRASH("Invalid material name passed to materials market!", "code/modules/cargo/materials_market.dm", 250);
                Type sheet_to_buy = Dm.initial(material_bought, (datum_material p) => p.sheet_type);
                if (sheet_to_buy == null)
                    throw Dm.CRASH("Material with no sheet type being sold on materials market!", "code/modules/cargo/materials_market.dm", 253);

                //get available bank account for purchasing
                datum_bank_account account_payable = null;
                if (is_ordering_private)
                    account_payable = used_id_card.registered_account;
                else if (can_buy_via_budget)
                    account_payable = SSeconomy.get_dep_account(ACCOUNT_CAR);
                if (account_payable == null)
                {
                    await say(I18n.T("No bank account detected!"));
                    return dot;
                }

                //sanity checks for available quantity & budget
                if (quantity > Dm.num(SSstock_market.materials_quantity[material_bought]))
                {
                    await say(I18n.T("Not enough materials on the market to purchase!"));
                    return dot;
                }

                float cost = Dm.num(SSstock_market.materials_prices[material_bought]) * quantity;

                DmList things_to_order = Dm.list();
                things_to_order[sheet_to_buy] = quantity;

                // We want to count how many stacks of all sheets we're ordering to make sure they don't exceed the limit of 10
                // If we already have a custom order on SSshuttle, we should add the things to order to that order
                datum_supply_order current_order = find_order(living_user, is_ordering_private ? TRUE : FALSE);
                if (current_order != null)
                {
                    // Check if this order exceeded the market limit
                    float prior_sheets = Dm.num(current_order.pack.contains[sheet_to_buy]);
                    if (prior_sheets + quantity > Dm.num(SSstock_market.materials_quantity[material_bought]))
                    {
                        await say(I18n.T("There aren't enough sheets on the market! Please wait for more sheets to be traded before adding more."));
                        playsound(living_user, Dm.resource("sound/machines/synth/synth_no.ogg"), 35, FALSE);
                        return dot;
                    }

                    // Check if the order exceeded the purchase limit
                    float prior_stacks = ROUND_UP(prior_sheets / MAX_STACK_SIZE);
                    if (prior_stacks >= MAX_STACK_LIMIT)
                    {
                        await say(I18n.T("There are already 10 stacks of sheets on order! Please wait for them to arrive before ordering more."));
                        playsound(living_user, Dm.resource("sound/machines/synth/synth_no.ogg"), 35, FALSE);
                        return dot;
                    }

                    // Prevents you from ordering more than the available budget
                    datum_bank_account paying_account = account_payable;
                    if (current_order.paying_account != null) //order is already being paid by another account
                        paying_account = current_order.paying_account;
                    if (current_order.get_final_cost() + cost > paying_account.account_balance)
                    {
                        await say(I18n.T("Order exceeds available budget! Please send it before purchasing more."));
                        return dot;
                    }

                    // Finally Append to this order
                    current_order.append_order(things_to_order, cost);
                    return TRUE;
                }

                //Place a new order
                var mineral_pack = Dm.@new<datum_supply_pack_custom_minerals>(is_ordering_private ? living_user : "Cargo", cost, things_to_order);
                var new_order = Dm.@new<datum_supply_order_disposable_materials>(mineral_pack, living_user, GALATIC_MATERIAL_ORDER, living_user.ckey, null,
                    is_ordering_private ? account_payable : null, null, null, null, null, MONEY_SYMBOL, FALSE);
                //first time order compute the correct cost and compare
                if (new_order.get_final_cost() > account_payable.account_balance)
                {
                    await say(I18n.T("Not enough money to start purchase!"));
                    qdel(new_order);
                    return dot;
                }

                await say(I18n.T("Thank you for your purchase! It will arrive on the next cargo shuttle!"));
                Dm.append(ref SSshuttle.shopping_list, new_order);
                return TRUE;
            }
            case "toggle_budget":
                if (!can_buy_via_budget)
                    return dot;
                ordering_private = Dm.truthy(ordering_private) ? FALSE : TRUE;
                return TRUE;
            case "clear":
            {
                datum_supply_order current_order = find_order(living_user, is_ordering_private ? TRUE : FALSE);
                if (current_order != null)
                {
                    Dm.remove(ref SSshuttle.shopping_list, current_order);
                    qdel(current_order);
                    return TRUE;
                }
                break;
            }
        }
        return dot;
    }
}

// tg: code/modules/cargo/materials_market.dm:345
public partial class obj_item_stock_block : obj_item
{
    // tg: code/modules/cargo/materials_market.dm:355
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(value_warning)), 1.5f * MINUTES, TIMER_DELETE_ME, file: "code/modules/cargo/materials_market.dm", line: 357);
        _addtimer(Dm.@new<datum_callback>(this, nameof(become_liquid)), 3 * MINUTES, TIMER_DELETE_ME, file: "code/modules/cargo/materials_market.dm", line: 358);
        return dot;
    }

    // tg: code/modules/cargo/materials_market.dm:360
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        var export_mat = custom_materials[1] as datum_material;
        float quantity = Dm.num(custom_materials[export_mat]) / SHEET_MATERIAL_AMOUNT;

        if (Dm.truthy(fluid))
        {
            Dm.append(ref dot, span_warning(I18n.T("\\The [] is currently liquid! Its value is based on the market price.", this)));
            update_value();
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("\\The []'s value is still []. [] before its value becomes liquid!", this, span_boldnotice(I18n.T("locked in")), span_boldnotice(I18n.T("Sell it")))));

        Dm.append(ref dot, span_notice(I18n.T("\\The [] is worth [] [], from selling [] sheets of [].", this, quantity * export_value, MONEY_SYMBOL, quantity, export_mat.name)));
        return dot;
    }

    /// Creates a visible effect warning nearby players that a stock block is beginning to become liquid in price.
    // tg: code/modules/cargo/materials_market.dm:375
    public virtual void value_warning()
    {
        visible_message(span_warning(I18n.T("\\The [] is starting to become liquid!", this)));
        icon_state = "stock_block_fluid";
        update_appearance(UPDATE_ICON_STATE);
    }

    /// Creates a visible effect warning nearby players that a stock block has become liquid in price, and updates the value, icon_state, and fluidity vars.
    // tg: code/modules/cargo/materials_market.dm:381
    public virtual void become_liquid()
    {
        update_value();
        icon_state = "stock_block_liquid";
        update_appearance(UPDATE_ICON_STATE);
        visible_message(span_warning(I18n.T("\\The [] becomes liquid!", this)));
        fluid = TRUE;
    }

    /// Updates the value of the stock block, for examine, and for sale value. Export value becomes equal to the stock market value of that material.
    // tg: code/modules/cargo/materials_market.dm:389
    public virtual float update_value()
    {
        export_value = Dm.num(SSstock_market.materials_prices[(custom_materials[1] as datum)?.type]);
        return export_value;
    }
}

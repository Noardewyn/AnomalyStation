// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/market_telepad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the Long-To-Short-Range Bluespace Transceiver (its LTSRBT window): it receives the black market's
// LTSRBT orders and sends them to the buyer's uplink, takes money to restock the markets, and puts a loaded item on the local market.
// The loaded item's picture (tg's icon2base64(getFlatIcon(occupant))) is not made: the server renders no icons (D104). The screentips
// (add_context) are a row (D264). The var blocks are generated (D111).

// tg: code/modules/cargo/markets/market_telepad.dm:15
public partial class obj_machinery_ltsrbt : obj_machinery
{
    // tg: code/modules/cargo/markets/market_telepad.dm:1-2  (file-local, #undef at the end)
    private const float DEFAULT_RESTOCK_COST = CARGO_CRATE_VALUE * 3.375f;
    private const float PLACE_ON_MARKET_COST = PAYCHECK_LOWER * 1.2f;
    // tg: code/modules/cargo/markets/market_telepad.dm:226-227, :262
    private const float LTSRBT_MIN_PRICE = PAYCHECK_LOWER;
    private const float LTSRBT_MAX_PRICE = CARGO_CRATE_VALUE * 50;
    private const float LTSRBT_MAX_MARKET_ITEMS = 40;

    /**
     * Attacking the machinery with enough credits will restock the markets, allowing for more/better items.
     * The cost doubles each time this is done.
     */
    // tg: code/modules/cargo/markets/market_telepad.dm:53  var/static/restock_cost (a type static, rules 10.6)
    private static float restock_cost
    {
        get => TgState.Current.obj_machinery_ltsrbt__restock_cost ??= DEFAULT_RESTOCK_COST;
        set => TgState.Current.obj_machinery_ltsrbt__restock_cost = value;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:55
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context(): the screentips (D264)
        if (SSmarket != null) // a harness world without the Master (rules 10.6)
            Dm.append(ref SSmarket.telepads, this);
        ADD_TRAIT(this, TRAIT_SECLUDED_LOCATION, INNATE_TRAIT); //you cannot sell disky, boss.
        update_appearance();
        return dot;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:62
    public override object Destroy(float? force = null)
    {
        if (SSmarket != null) // a harness world without the Master (rules 10.6)
        {
            Dm.remove(ref SSmarket.telepads, this);
            // Bye bye orders.
            if (Dm.truthy(length(SSmarket.telepads)))
            {
                foreach (datum_market_purchase purchase in Dm.each<datum_market_purchase>(queue))
                    SSmarket.queue_item(purchase);
            }
            if (receiving != null)
                SSmarket.queue_item(receiving);
        }
        queue = null;
        receiving = null;
        transmitting = null;
        return base.Destroy(force);
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:92
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            Dm.append(ref dot, span_info(I18n.T("A small display reads:")));
            Dm.append(ref dot, span_tinynoticeital(I18n.T("Current market restock price: [].", EXAMINE_HINT(Dm.text("[] []", restock_cost, MONEY_SYMBOL)))));
            Dm.append(ref dot, span_tinynoticeital(I18n.T("Market placement fee: [].", EXAMINE_HINT(Dm.text("[] []", PLACE_ON_MARKET_COST, MONEY_SYMBOL)))));
            Dm.append(ref dot, span_tinynoticeital(I18n.T("Withholding tax on local items: [].", EXAMINE_HINT(Dm.text("[]%", MARKET_WITHHOLDING_TAX * 100)))));
        }
        return dot;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:100
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            icon_state = Dm.text("[]_off", base_icon_state);
        else
            icon_state = Dm.text("[][]", base_icon_state, (receiving != null || Dm.truthy(length(queue)) || occupant != null) ? "" : "_idle");
        return dot;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:107
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(state_open))
            Dm.append(ref dot, Dm.text("[]_closed", base_icon_state));
        else
        {
            var overlay = (mutable_appearance)mutable_appearance(icon, Dm.text("[]_open", base_icon_state));
            overlay.pixel_w -= 2;
            overlay.pixel_z -= 1;
            Dm.append(ref dot, overlay);
        }
        return dot;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:117
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(state_open))
            await open_machine(density_to_set: TRUE);
        else
            await close_machine();
        return dot;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:126
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        object dot = await base.open_machine(drop, density_to_set);
        playsound(this, Dm.resource("sound/machines/oven/oven_open.ogg"), 75, TRUE);
        return dot;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:130
    public override async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        object dot = await base.close_machine(target, density_to_set);
        playsound(this, Dm.resource("sound/machines/oven/oven_close.ogg"), 75, TRUE);
        return dot;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:134
    public override void set_occupant(atom_movable new_occupant = null)
    {
        base.set_occupant(new_occupant);
        if (new_occupant != null)
        {
            current_name = Dm.str(new_occupant.name);
            current_desc = Dm.str(new_occupant.desc);
        }
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:140
    public override object can_be_occupant(atom_movable occupant_atom = null) => isitem(occupant_atom) && !Dm.truthy(occupant_atom.anchored) ? TRUE : FALSE;

    // tg: code/modules/cargo/markets/market_telepad.dm:141
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == occupant)
        {
            current_price = Dm.num(Dm.initial(this, nameof(current_price)));
            current_name = "";
            current_desc = "";
            update_appearance(UPDATE_ICON_STATE);
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:151
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (Dm.truthy(state_open))
        {
            balloon_alert(user, I18n.T("close it first!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (occupant == null)
        {
            balloon_alert(user, I18n.T("nothing loaded!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            balloon_alert(user, I18n.T("machine unpowered!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (!(world.time >= recharge_cooldown)) // COOLDOWN_FINISHED
        {
            balloon_alert(user, I18n.T("on cooldown!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        await ui_interact(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:170
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        float creds_value = Dm.num(tool.get_item_credit_value());

        if (Dm.truthy(state_open))
        {
            if (Dm.locate<mob_living>(tool.get_all_contents()) != null)
            {
                await say(I18n.T("Living being detected, cannot sell!"));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 40, FALSE);
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                balloon_alert(user, I18n.T("stuck to your hands!"));
                return ITEM_INTERACT_BLOCKING;
            }
            balloon_alert(user, I18n.T("item loaded"));
            await close_machine(tool);
            return ITEM_INTERACT_SUCCESS;
        }
        else if (creds_value == 0)
        {
            balloon_alert(user, I18n.T("open the machine!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return null;

        if (creds_value < restock_cost)
        {
            await say(I18n.T("Insufficient []!", MONEY_NAME));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 40, FALSE);
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_holochip chip)
            chip.spend(restock_cost);
        else
        {
            qdel(tool);
            if (creds_value != restock_cost)
            {
                var change = Dm.@new<obj_item_holochip>(loc, creds_value - restock_cost);
                await user.put_in_hands(change);
            }
        }

        await SSmarket.restock();
        restock_cost *= 2;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:212
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "LTSRBT", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:218
    public override object ui_state(mob user = null)
    {
        if (occupant == null || !(world.time >= recharge_cooldown))
            return GLOB.never_state; //close it.
        else
            return GLOB.default_state;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:227
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["loaded_icon"] = null; // icon2base64(getFlatIcon(occupant, no_anim=TRUE)): the server renders no icons (D104)
        data["min_price"] = LTSRBT_MIN_PRICE;
        data["max_price"] = LTSRBT_MAX_PRICE;
        return data;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:234
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["name"] = current_name;
        data["price"] = current_price;
        data["desc"] = current_desc;
        return data;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:241
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (Dm.str(action))
        {
            case "change_name":
            {
                object value = @params["value"];
                if (usr?.client?.holder == null && Dm.truthy(is_ic_filtered_for_pdas(value))) // CAN_BYPASS_FILTER(usr)
                    return TRUE;
                current_name = Dm.str(trim(value, MAX_NAME_LEN));
                return TRUE;
            }
            case "change_desc":
            {
                object value = @params["value"];
                if (usr?.client?.holder == null && Dm.truthy(is_ic_filtered_for_pdas(value))) // CAN_BYPASS_FILTER(usr)
                    return TRUE;
                current_desc = Dm.str(trim(value, MAX_DESC_LEN));
                return TRUE;
            }
            case "change_price":
                current_price = clamp(Dm.num(@params["value"]), LTSRBT_MIN_PRICE, LTSRBT_MAX_PRICE);
                return TRUE;
            case "place_on_market":
                await place_on_market(usr);
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:269
    public virtual async DmTask place_on_market(mob user = null)
    {
        if (QDELETED(occupant))
            return;
        if (Dm.locate<mob_living>(occupant.get_all_contents()) != null)
        {
            await say(I18n.T("Living being detected, cannot sell!"));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 40, FALSE);
            return;
        }
        datum_bank_account account = null;
        var our_market = (datum_market)SSmarket.markets[typeof(datum_market_blackmarket)];
        // if(!isAdminGhostAI(user)): no admin ghosts yet (GAP(lead), is_helpers.cs)
        {
            if (!isliving(user))
                return;
            if (length(our_market.available_items[Dm.initial(typeof(datum_market_item_local_good), (datum_market_item p) => p.category)]) >= LTSRBT_MAX_MARKET_ITEMS)
            {
                await say(I18n.T("Local market saturated, buy some goods first!"));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 40, FALSE);
                return;
            }
            var living_user = (mob_living)user;
            var card = living_user.get_idcard(TRUE) as obj_item_card_id;
            if (card?.registered_account == null)
            {
                await say(I18n.T("No bank account to charge market fees detected!"));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 40, FALSE);
                return;
            }
            if (!Dm.truthy(card.registered_account.adjust_money(-PLACE_ON_MARKET_COST, I18n.T("Market: Placement Fee"))))
            {
                await say(I18n.T("Insufficient []!", MONEY_NAME));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 40, FALSE);
                return;
            }
            account = card.registered_account;
        }

        var item = (obj_item)occupant; //occupant, name, price and desc will be null'd once it exits the machine so we need this.
        object name_to_use = Dm.truthy(current_name) ? current_name : item.name;
        string desc_to_use = current_desc;
        if (account != null)
            desc_to_use += I18n.T("[]Seller: []", Dm.truthy(current_desc) ? " - " : "", account.account_holder);
        float price_to_use = current_price;
        await item.moveToNullspace();
        //Something happened and the item was deleted or relocated as soon as it was moved to nullspace.
        if (QDELETED(item) || item.loc != null)
        {
            await say(I18n.T("Runtime at market_placement.dm, line 153: item gone!")); //metajoke
            return;
        }
        var new_item = Dm.@new<datum_market_item_local_good>(item, account);
        new_item.name = name_to_use;
        new_item.desc = desc_to_use;
        new_item.price = price_to_use;

        our_market.add_item(new_item);

        await say(I18n.T("Item placed on the market!"));
        playsound(this, Dm.resource("sound/effects/cashregister.ogg"), 40, FALSE);
        recharge_cooldown = world.time + recharge_time * 3; // COOLDOWN_START
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:321
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        recharge_time = base_recharge_time;
        // On tier 4 recharge_time should be 20 and by default it is 80 as scanning modules should be tier 1.
        foreach (datum_stock_part_scanning_module scanning_module in Dm.each<datum_stock_part_scanning_module>(component_parts))
            recharge_time -= Dm.num(scanning_module.tier) * 1 * SECONDS;

        power_efficiency = 0;
        foreach (datum_stock_part_micro_laser laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            power_efficiency += Dm.num(laser.tier);
        // Shouldn't happen but you never know.
        if (power_efficiency == 0)
            power_efficiency = 1;
        return dot;
    }

    /// Adds /datum/market_purchase to queue unless the machine is free, then it sets the purchase to be instantly received
    // tg: code/modules/cargo/markets/market_telepad.dm:336
    public virtual void add_to_queue(datum_market_purchase purchase = null)
    {
        if (!Dm.truthy(recharge_cooldown) && receiving == null && transmitting == null)
        {
            receiving = purchase;
            update_appearance(UPDATE_ICON_STATE);
        }
        else
            Dm.append(ref queue, purchase);

        RegisterSignal(purchase, COMSIG_QDELETING, nameof(on_purchase_del));
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:345
    public virtual void on_purchase_del(datum_market_purchase purchase = null)
    {
        Dm.remove(ref queue, purchase);
        if (receiving == purchase)
            receiving = null;
        if (transmitting == purchase)
            transmitting = null;

        update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/modules/cargo/markets/market_telepad.dm:355
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return null;

        if (!(world.time >= recharge_cooldown) && receiving == null && transmitting == null)
            return null;

        turf turf = get_turf(this);
        if (receiving != null)
        {
            receiving.item = Waiting.NoSleep(receiving.entry.spawn_item(turf, receiving)) as atom_movable;
            receiving.post_purchase_effects(receiving.item);

            Waiting.NoSleep(use_energy(energy_usage_per_teleport / power_efficiency));
            do_sparks(5, TRUE, this, receiving.item);
            transmitting = receiving;
            receiving = null;

            return null;
        }
        if (transmitting != null)
        {
            if (transmitting.item.loc == turf)
            {
                Waiting.NoSleep(do_teleport(transmitting.item, get_turf(transmitting.uplink)));
                Waiting.NoSleep(use_energy(energy_usage_per_teleport / power_efficiency));
            }
            QDEL_NULL(ref transmitting);
            recharge_cooldown = world.time + recharge_time; // COOLDOWN_START
            return null;
        }

        if (Dm.truthy(length(queue)))
            receiving = pick_n_take(queue) as datum_market_purchase;
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/cargo/markets/market_telepad.dm:53  var/static/restock_cost
    internal float? obj_machinery_ltsrbt__restock_cost;
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/market_uplink.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the market uplink (its BlackMarketUplink window) and the black market uplink; the crafting recipe
// is a var block. The var blocks are generated (D111).

// tg: code/modules/cargo/markets/market_uplink.dm:1
public partial class obj_item_market_uplink : obj_item
{
    // tg: code/modules/cargo/markets/market_uplink.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // We don't want to go through this at mapload because the SSmarket isn't initialized yet.
        if (Dm.truthy(mapload))
            return dot;

        update_viewing_category();
        return dot;
    }

    /// Simple internal proc for updating the viewing_category variable.
    // tg: code/modules/cargo/markets/market_uplink.dm:30
    public virtual void update_viewing_category()
    {
        if (Dm.truthy(accessible_markets.len))
        {
            viewing_market = accessible_markets[1];
            DmList categories = (SSmarket?.markets?[viewing_market] as datum_market)?.categories; // SSmarket?: a harness world without the Master (rules 10.6)
            if (Dm.truthy(categories?.len))
                viewing_category = categories[1];
        }
    }

    // tg: code/modules/cargo/markets/market_uplink.dm:37
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!Dm.truthy(viewing_category))
            update_viewing_category();

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "BlackMarketUplink", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/cargo/markets/market_uplink.dm:46
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        var market = Dm.truthy(viewing_market) ? SSmarket.markets[viewing_market] as datum_market : null;
        obj_item_card_id id_card = null;
        if (user is mob_living livin)
            id_card = livin.get_idcard() as obj_item_card_id;
        if (id_card?.registered_account != null)
            current_user = id_card.registered_account;
        else
            current_user = null;
        data["categories"] = market?.categories;
        DmList delivery_methods = Dm.list();
        data["money"] = Dm.text("N/A []", MONEY_SYMBOL);
        if (current_user != null)
            data["money"] = current_user.account_balance;
        data["buying"] = buying;
        if (Dm.truthy(buying) && market != null)
        {
            var target_item = (market.available_items[viewing_category] as DmList)?[selected_item] as datum_market_item;
            DmList shipping_list = market.shipping;
            if (Dm.truthy(length(target_item?.shipping_override)))
                shipping_list = target_item.shipping_override;
            foreach (object delivery in Dm.each(shipping_list))
                Dm.append(ref delivery_methods, Dm.list(Dm.list(Dm.kv("name", delivery), Dm.kv("price", shipping_list[delivery])))); // UNTYPED_LIST_ADD
        }
        data["delivery_methods"] = delivery_methods;
        DmList items = Dm.list();
        data["viewing_category"] = Dm.truthy(market?.categories?[viewing_category]) ? viewing_category : null;
        data["viewing_market"] = viewing_market;
        if (Dm.truthy(viewing_category) && market != null)
        {
            if (Dm.truthy(market.available_items[viewing_category]))
            {
                var market_category = (DmList)market.available_items[viewing_category];
                foreach (object id in Dm.each(market_category))
                {
                    var item = (datum_market_item)market_category[id];
                    Dm.append(ref items, Dm.list(Dm.list(
                        Dm.kv("id", id),
                        Dm.kv("name", item.name),
                        Dm.kv("cost", item.price),
                        Dm.kv("amount", item.stock),
                        Dm.kv("desc", Dm.truthy(item.desc) ? item.desc : item.name),
                        Dm.kv("html_icon", item.html_icon))));
                }
            }
        }
        data["items"] = items;
        return data;
    }

    // tg: code/modules/cargo/markets/market_uplink.dm:88
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["delivery_method_description"] = SSmarket.shipping_method_descriptions;
        data["ltsrbt_built"] = SSmarket.telepads.len;
        DmList markets = Dm.list();
        foreach (object M in Dm.each(accessible_markets))
        {
            var BM = (datum_market)SSmarket.markets[M];
            Dm.append(ref markets, Dm.list(Dm.list(
                Dm.kv("id", Dm.text("[]", M)),
                Dm.kv("name", BM.name))));
        }
        data["markets"] = markets;
        return data;
    }

    // tg: code/modules/cargo/markets/market_uplink.dm:101
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (Dm.str(action))
        {
            case "set_category":
                if (@params["category"] == null)
                    return dot;
                if (viewing_market == null)
                    return dot;
                if (!Dm.@in(@params["category"], ((datum_market)SSmarket.markets[viewing_market]).categories))
                    return dot;
                viewing_category = @params["category"];
                dot = TRUE;
                break;
            case "set_market":
            {
                if (@params["market"] == null)
                    return dot;
                Type market = text2path(Dm.str(@params["market"]));
                if (!Dm.@in(market, accessible_markets))
                    return dot;

                viewing_market = market;

                DmList categories = ((datum_market)SSmarket.markets[viewing_market]).categories;
                if (Dm.truthy(categories?.len))
                    viewing_category = categories[1];
                else
                    viewing_category = null;
                dot = TRUE;
                break;
            }
            case "select":
                if (@params["item"] == null)
                    return dot;
                selected_item = @params["item"];
                buying = TRUE;
                dot = TRUE;
                break;
            case "cancel":
                selected_item = null;
                buying = FALSE;
                dot = TRUE;
                break;
            case "buy":
            {
                if (@params["method"] == null)
                    return dot;
                if (selected_item == null)
                {
                    buying = FALSE;
                    return dot;
                }
                var market = (datum_market)SSmarket.markets[viewing_market];
                await market.purchase(selected_item, viewing_category, @params["method"], this, usr);

                buying = FALSE;
                selected_item = null;
                break;
            }
        }
        return dot;
    }
}

// tg: code/modules/cargo/markets/market_uplink.dm:152
public partial class obj_item_market_uplink_blackmarket : obj_item_market_uplink
{
    // tg: code/modules/cargo/markets/market_uplink.dm:161
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/_market.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the market and the black market. The var blocks are generated (D111).

// tg: code/modules/cargo/markets/_market.dm:1
public partial class datum_market : datum
{
    /// Adds item to the available items and add its category if it is not in categories yet.
    // tg: code/modules/cargo/markets/_market.dm:17
    public virtual object add_item(object item = null)
    {
        if (ispath(item, typeof(datum_market_item)))
            item = Dm.@new((Type)item);
        var market_item = (datum_market_item)item;

        if (!Dm.@in(market_item.category, categories))
        {
            Dm.append(ref categories, market_item.category);
            available_items[market_item.category] = Dm.list();
        }

        ((DmList)available_items[market_item.category])[market_item.identifier] = market_item;
        RegisterSignal(market_item, COMSIG_QDELETING, nameof(on_item_del));
        return TRUE;
    }

    // tg: code/modules/cargo/markets/_market.dm:29
    public virtual void on_item_del(datum_market_item item = null)
    {
        var category_list = available_items[item.category] as DmList;
        Dm.remove(ref category_list, item.identifier);
        available_items[item.category] = category_list;
        if (!Dm.truthy(length(available_items[item.category])))
        {
            Dm.remove(ref available_items, item.category);
            Dm.remove(ref categories, item.category);
        }
    }

    /**
     * Handles buying the item for a market.
     *
     * @param identifier The identifier of the item to buy.
     * @param category The category of the item to buy.
     * @param method The shipping method to use to get the item on the station.
     * @param uplink The uplink object that is buying the item.
     * @param user The mob that is buying the item.
     */
    // tg: code/modules/cargo/markets/_market.dm:45
    public virtual async DmTask<object> purchase(object identifier = null, object category = null, object method = null, obj_item_market_uplink uplink = null, object user = null)
    {
        var item = (available_items[category] as DmList)?[identifier] as datum_market_item;
        if (item == null)
            return FALSE;

        if (uplink == null || !(Dm.@in(method, shipping) || Dm.@in(method, item.shipping_override)))
            return FALSE;

        object shipment_fee = item.shipping_override?[method];
        if (shipment_fee == null)
            shipment_fee = shipping[method];
        float price = Dm.num(item.price) + Dm.num(shipment_fee);

        if (uplink.current_user == null) ///There is no ID card on the user, or the ID card has no account
        {
            to_chat(user, span_warning(I18n.T("The uplink sparks, as it can't identify an ID card with a bank account on you.")));
            return FALSE;
        }
        float balance = uplink.current_user.account_balance;

        // I can't get the price of the item and shipping in a clean way to the UI, so I have to do this.
        if (balance < price)
        {
            to_chat(user, span_warning(I18n.T("You don't have enough [] in [] for [] with [] shipping.", MONEY_NAME, uplink, item, method)));
            return FALSE;
        }

        if (Dm.truthy(await item.buy(uplink, user as mob, method, legal_status)))
        {
            uplink.current_user.adjust_money(-price, I18n.T("Other: Third Party Transaction"));
            if (user is mob m_user)
                m_user.playsound_local(get_turf(m_user), Dm.resource("sound/machines/beep/twobeep_high.ogg"), 50, TRUE);
            return TRUE;
        }

        return FALSE;
    }

    /**
     * A proc that restocks only the EXISTING items of this market.
     * If you want to selectively restock markets, call SSmarket.restock(market_or_list_of_markets) instead.
     */
    // tg: code/modules/cargo/markets/_market.dm:81
    public virtual void restock(DmList existing_items = null)
    {
        foreach (object category in Dm.each(available_items))
        {
            var category_list = available_items[category] as DmList;
            foreach (object identifier in Dm.each(category_list))
            {
                var item = (datum_market_item)category_list[identifier];
                Dm.combine(ref existing_items, item.GetType());
                if (!Dm.truthy(item.restockable) || Dm.num(item.stock) >= item.stock_max || !prob(Dm.num(item.availability_prob)))
                    continue;
                item.stock = Dm.num(item.stock) + rand(1, item.stock_max - Dm.num(item.stock));
            }
        }
    }
}

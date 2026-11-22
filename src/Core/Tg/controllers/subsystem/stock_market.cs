// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/stock_market.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: the tradable materials' prices, trends and quantities, which the material exports sell at and the
// market events move. The materials market console that buys from it is gaps P4d. The var blocks are generated.

// tg: code/controllers/subsystem/stock_market.dm:1  SUBSYSTEM_DEF(stock_market)
public partial class datum_controller_subsystem_stock_market : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(stock_market) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSstock_market) expanded inline.
    // tg: code/controllers/subsystem/stock_market.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSstock_market != this)
        {
            if (SSstock_market is datum_controller_subsystem_stock_market)
            {
                Recover();
                qdel(SSstock_market);
            }
            SSstock_market = this;
        }
        PreInit();
    }

    // The materials are keyed by their typepaths, as tg's subtypesof; their vars are the types' initial values (rules 10.3)
    private static float ValuePerUnit(object mat) => mat is datum_material m ? m.value_per_unit : Dm.initial(mat, (datum_material p) => p.value_per_unit);
    private static object MinimumOverride(object mat) => mat is datum_material m ? m.minimum_value_override : Dm.initial(mat, (datum_material p) => p.minimum_value_override);
    private static float BaseQuantity(object mat) => mat is datum_material m ? m.tradable_base_quantity : Dm.initial(mat, (datum_material p) => p.tradable_base_quantity);

    // tg: code/controllers/subsystem/stock_market.dm:20
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        foreach (Type possible_market in Dm.each_cast<Type>(subtypesof(typeof(datum_material)) as DmList)) // I need to make this work like this, but lets hardcode it for now
        {
            if (Dm.truthy(Dm.initial(possible_market, (datum_material p) => p.tradable)))
            {
                materials_prices[possible_market] = ValuePerUnit(possible_market) * SHEET_MATERIAL_AMOUNT;
                materials_trends[possible_market] = rand(MARKET_TREND_DOWNWARD, MARKET_TREND_UPWARD); //aka -1 to 1
                materials_trend_life[possible_market] = rand(1, 3);
                float base_quantity = BaseQuantity(possible_market);
                materials_quantity[possible_market] = base_quantity + rand(-base_quantity * 0.5f, base_quantity * 0.5f);
            }
        }
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/stock_market.dm:29
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        foreach (object market in Dm.each(materials_prices.Copy()))
            handle_trends_and_price(market);
        foreach (datum_stock_market_event @event in Dm.each_cast<datum_stock_market_event>(active_events.Copy()))
            @event.handle();
    }

    ///Adjust the price of a material(either through buying or selling) ensuring it stays within limits
    // tg: code/controllers/subsystem/stock_market.dm:36
    public virtual void adjust_material_price(object mat = null, object delta = null)
    {
        var material = Waiting.NoSleep(SSmaterials.get_material(mat)) as datum_material;

        //adjust the price
        float new_price = Dm.num(materials_prices[material.type]) + Dm.num(delta);

        //get the limits
        float price_minimum = round(material.value_per_unit * SHEET_MATERIAL_AMOUNT * 0.5f);
        if (material.minimum_value_override != null)
            price_minimum = round(Dm.num(material.minimum_value_override) * SHEET_MATERIAL_AMOUNT);
        float price_maximum = round(material.value_per_unit * SHEET_MATERIAL_AMOUNT * 3);

        //clamp it down
        new_price = round(clamp(new_price, price_minimum, price_maximum));
        materials_prices[material.type] = new_price;
    }

    ///Adjust the amount of material(either through buying or selling) ensuring it stays within limits
    // tg: code/controllers/subsystem/stock_market.dm:53
    public virtual void adjust_material_quantity(object mat = null, object delta = null)
    {
        var material = Waiting.NoSleep(SSmaterials.get_material(mat)) as datum_material;

        //adjust the quantity
        float new_quantity = Dm.num(materials_quantity[material.type]) + Dm.num(delta);

        //get the upper limit
        float quantity_baseline = material.tradable_base_quantity;

        //clamp it down
        new_quantity = round(clamp(new_quantity, 0, quantity_baseline * 2));
        materials_quantity[material.type] = new_quantity;
    }

    /**
     * Handles shifts in the cost of materials, and in what direction the material is most likely to move.
     */
    // tg: code/controllers/subsystem/stock_market.dm:69
    public virtual void handle_trends_and_price(object mat = null)
    {
        if (prob(MARKET_EVENT_PROBABILITY))
            handle_market_event(mat);
        object trend = materials_trends[mat];
        float trend_life = Dm.num(materials_trend_life[mat]);

        float price_units = Dm.num(materials_prices[mat]);
        float price_minimum = round(ValuePerUnit(mat) * SHEET_MATERIAL_AMOUNT * 0.5f);
        if (MinimumOverride(mat) != null)
            price_minimum = round(Dm.num(MinimumOverride(mat)) * SHEET_MATERIAL_AMOUNT);
        float price_maximum = round(ValuePerUnit(mat) * SHEET_MATERIAL_AMOUNT * 3);
        float price_baseline = ValuePerUnit(mat) * SHEET_MATERIAL_AMOUNT;
        float quantity_baseline = BaseQuantity(mat);

        float stock_quantity = Dm.num(materials_quantity[mat]);

        if (HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING)) //We hardset to the worst possible price and lowest possible impact if sold
        {
            materials_prices[mat] = price_minimum;
            materials_quantity[mat] = stock_quantity * 2;
            materials_trends[mat] = MARKET_TREND_DOWNWARD;
            materials_trend_life[mat] = 1;
            return;
        }

        if (trend_life == 0)
        {
            ///We want to scale our trend so that if we're closer to our minimum or maximum price, we're more likely to trend the other way.
            if (price_units < price_baseline)
            {
                float chance_swap = 100 - ((clamp(price_units - price_minimum, 1, 1000) / (price_baseline - price_minimum)) * 100);
                if (prob(chance_swap))
                    materials_trends[mat] = MARKET_TREND_UPWARD;
                else
                    materials_trends[mat] = MARKET_TREND_STABLE;
            }
            else if (price_units > price_baseline)
            {
                float chance_swap = 100 - ((clamp(price_units - price_maximum, 1, 1000) / (price_maximum - price_baseline)) * 100);
                if (prob(chance_swap))
                    materials_trends[mat] = MARKET_TREND_DOWNWARD;
                else
                    materials_trends[mat] = MARKET_TREND_STABLE;
            }
            materials_trend_life[mat] = rand(1, 3); // Change our trend life for x number of fires of the subsystem
        }
        else
            materials_trend_life[mat] = Dm.num(materials_trend_life[mat]) - 1;

        float price_change = 0;
        float quantity_change = 0;
        switch ((int)Dm.num(trend))
        {
            case MARKET_TREND_UPWARD:
                price_change = ROUND_UP(gaussian(price_units * 0.30f, price_baseline * 0.15f)); //If we don't ceil, small numbers will get trapped at low values
                quantity_change = -round(gaussian(quantity_baseline * 0.15f, quantity_baseline * 0.15f));
                break;
            case MARKET_TREND_STABLE:
                price_change = round(gaussian(0, price_baseline * 0.01f));
                quantity_change = round(gaussian(0, quantity_baseline * 0.5f));
                break;
            case MARKET_TREND_DOWNWARD:
                price_change = -ROUND_UP(gaussian(price_units * 0.3f, price_baseline * 0.15f));
                quantity_change = round(gaussian(quantity_baseline * 0.15f, quantity_baseline * 0.15f));
                break;
        }
        materials_prices[mat] = round(clamp(price_units + price_change, price_minimum, price_maximum));
        materials_quantity[mat] = round(clamp(stock_quantity + quantity_change, 0, quantity_baseline * 2));
    }

    /**
     * Market events are a way to spice up the market and make it more interesting.
     * Randomly one will occur to a random material, and it will change the price of that material more drastically, or reset it to a stable price.
     * Events are also broadcast to the newscaster as a fun little fluff piece. Good way to tell some lore as well, or just make a joke.
     */
    // tg: code/controllers/subsystem/stock_market.dm:130
    public virtual void handle_market_event(object mat = null)
    {
        var event_type = pick(subtypesof(typeof(datum_stock_market_event))) as Type;
        var @event = Dm.@new(event_type) as datum_stock_market_event;
        if (Dm.truthy(@event.start_event(mat)))
            Dm.append(ref active_events, @event);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/stock_market.dm:1  SUBSYSTEM_DEF(stock_market): GLOBAL_REAL(SSstock_market, ...)
    internal datum_controller_subsystem_stock_market SSstock_market;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_stock_market SSstock_market => ref TgState.Current.SSstock_market;
}

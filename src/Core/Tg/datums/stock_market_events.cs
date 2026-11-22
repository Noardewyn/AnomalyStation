// Translated from tgstation (snapshot 1d99bd7f): code/datums/stock_market_events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: the stock market's events (a reset, a large boost or drop, hotcakes, a lockdown) and their news lines
// for the earnings report. The class declares mat (the market keys are the materials' typepaths), so its var block is hand-written;
// the kinds' var blocks are generated.

// tg: code/datums/stock_market_events.dm:1
public partial class datum_stock_market_event : datum
{
    /// The name of the event, used to describe it in the news.
    // tg: code/datums/stock_market_events.dm:3
    public string name = "Stock Market Event!";
    /// A list of company names to use for the event and the circumstance.
    // tg: code/datums/stock_market_events.dm:5  var/static/list/company_name (a type static, rules 10.6)
    private static DmList company_name => TgState.Current.datum_stock_market_event__company_name ??= Dm.list(
        "Nakamura Engineering",
        "Robust Industries, LLC",
        "MODular Solutions",
        "SolGov",
        "Australicus Industrial Mining",
        "Nanotrasen-DeForest Corporation",
        "Aussec Armory",
        "Dreamland Robotics");
    /// A list of strings selected from the event that's used to describe the event in the news.
    // tg: code/datums/stock_market_events.dm:16
    public DmList circumstance;
    /// What material is affected by the event?
    // tg: code/datums/stock_market_events.dm:18  var/datum/material/mat: object (rules 3.2): SSstock_market passes a material's typepath
    public object mat;
    /// Constant to multiply the original material value by to get the new minimum value, unless the material has a minimum override.
    // tg: code/datums/stock_market_events.dm:20
    public float price_minimum = SHEET_MATERIAL_AMOUNT * 0.5f;
    /// Constant to multiply the original material value by to get the new maximum value.
    // tg: code/datums/stock_market_events.dm:22
    public float price_maximum = SHEET_MATERIAL_AMOUNT * 3;
    /// When this event is ongoing, what direction will the price trend in?
    // tg: code/datums/stock_market_events.dm:25
    public object trend_value;
    /// When this event is triggered, for how long will its effects last?
    // tg: code/datums/stock_market_events.dm:27
    public object trend_duration;

    // tg: code/datums/stock_market_events.dm:16  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        circumstance = Dm.list();
    }

    // the material's initial vars, read off its typepath (rules 10.3)
    protected float MatValuePerUnit => mat is datum_material m ? m.value_per_unit : Dm.initial(mat, (datum_material p) => p.value_per_unit);

    /**
     * When a new stock_market_event is created, this proc is called to set up the event if there's anything that needs to happen upon it starting.
     * @param _mat The material that this event will affect.
     */
    // tg: code/datums/stock_market_events.dm:33
    public virtual object start_event(object mat = null)
    {
        if (mat is datum_material)
            return FALSE;
        this.mat = mat;
        if (trend_value != null)
        {
            SSstock_market.materials_trends[mat] = trend_value;
            if (trend_duration != null)
                SSstock_market.materials_trend_life[mat] = trend_duration;
        }
        return TRUE;
    }

    /**
     * This proc is called every tick while the event is ongoing by SSstock_market.
     */
    // tg: code/datums/stock_market_events.dm:46
    public virtual void handle()
    {
        trend_duration = Dm.num(trend_duration) - 1;
        if (Dm.num(trend_duration) <= 0)
            end_event();
    }

    /**
     * When a stock_market_event is ended, this proc is called to apply any final effects and clean up anything that needs to be cleaned up.
     */
    // tg: code/datums/stock_market_events.dm:55
    public virtual void end_event()
    {
        SSstock_market.active_events.Remove(this);
        qdel(this);
    }

    /**
     * This proc is called to create a news string for the event, which is passed along to SSstock_market to be appended to the automatic newscaster messages.
     */
    // tg: code/datums/stock_market_events.dm:62
    public virtual void create_news()
    {
        object temp_company = pick(company_name);
        object temp_circumstance = pick(circumstance);
        string mat_name = mat is datum_material m ? Dm.str(m.name) : Dm.str(Dm.initial(mat, (datum_material p) => p.name));
        SSstock_market.news_string += Dm.text("<b>[] []</b> []<b>[].</b><br>", name, temp_company, temp_circumstance, mat_name);
    }
}

// tg: code/datums/stock_market_events.dm:68
public partial class datum_stock_market_event_market_reset : datum_stock_market_event
{
    // tg: code/datums/stock_market_events.dm:78
    public override object start_event(object mat = null)
    {
        object dot = base.start_event(mat);
        SSstock_market.materials_prices[this.mat] = MatValuePerUnit * SHEET_MATERIAL_AMOUNT;
        create_news();
        return dot;
    }
}

// tg: code/datums/stock_market_events.dm:83
public partial class datum_stock_market_event_large_boost : datum_stock_market_event
{
    // tg: code/datums/stock_market_events.dm:93
    public override object start_event(object mat = null)
    {
        object dot = base.start_event(mat);
        float price_units = Dm.num(SSstock_market.materials_prices[this.mat]);
        SSstock_market.materials_prices[this.mat] = price_units + round(gaussian(price_units, price_units * 0.15f));
        SSstock_market.materials_prices[this.mat] = clamp(Dm.num(SSstock_market.materials_prices[this.mat]), price_minimum * MatValuePerUnit, price_maximum * MatValuePerUnit);
        create_news();
        return dot;
    }
}

// tg: code/datums/stock_market_events.dm:100
public partial class datum_stock_market_event_large_drop : datum_stock_market_event
{
    // tg: code/datums/stock_market_events.dm:110
    public override object start_event(object mat = null)
    {
        object dot = base.start_event(mat);
        float price_units = Dm.num(SSstock_market.materials_prices[this.mat]);
        SSstock_market.materials_prices[this.mat] = price_units - round(gaussian(price_units * 1.5f, price_units * 0.15f));
        SSstock_market.materials_prices[this.mat] = clamp(Dm.num(SSstock_market.materials_prices[this.mat]), price_minimum * MatValuePerUnit, price_maximum * MatValuePerUnit);
        create_news();
        return dot;
    }
}

// tg: code/datums/stock_market_events.dm:117
public partial class datum_stock_market_event_hotcakes : datum_stock_market_event
{
    // tg: code/datums/stock_market_events.dm:127
    public override object start_event(object mat = null)
    {
        object dot = base.start_event(mat);
        SSstock_market.materials_prices[this.mat] = round(price_maximum * MatValuePerUnit);
        create_news();
        return dot;
    }
}

// tg: code/datums/stock_market_events.dm:132
public partial class datum_stock_market_event_lockdown : datum_stock_market_event
{
    // tg: code/datums/stock_market_events.dm:141
    public override void handle()
    {
        base.handle();
        SSstock_market.materials_quantity[mat] = 0; //Force the material to be unavailable.
    }

    // tg: code/datums/stock_market_events.dm:145
    public override void end_event()
    {
        base.end_event();
        SSstock_market.materials_quantity[mat] = Dm.initial(mat, (datum_material p) => p.tradable_base_quantity); //Force the material to be available again.
        SSstock_market.materials_prices[mat] = MatValuePerUnit * SHEET_MATERIAL_AMOUNT; //Force the price to be reset once the lockdown is over.
        create_news();
    }
}

public sealed partial class TgState
{
    // tg: code/datums/stock_market_events.dm:5  var/static/list/company_name
    internal DmList datum_stock_market_event__company_name;
}

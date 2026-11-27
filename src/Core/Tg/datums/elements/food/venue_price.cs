// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/food/venue_price.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the price a robot customer pays for a food, a piece of clothing or a crayon it ordered, or for the drink of the
// reagent it ordered: a holochip on the floor, the cash register's sound and the venue's income. The var block is generated.

///This component can be used to give something value for venues
// tg: code/datums/elements/food/venue_price.dm:2
public partial class datum_element_venue_price : datum_element
{
    // tg: code/datums/elements/food/venue_price.dm:7
    [DmParams("target", "venue_price")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object venue_price = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!Dm.truthy(venue_price))
        {
            stack_trace("A venue_price element was attached to something without specifying an actual price.", "code/datums/elements/food/venue_price.dm", 10);
            return ELEMENT_INCOMPATIBLE;
        }
        this.venue_price = venue_price;

        if (isitem(target))
            RegisterSignal(target, COMSIG_ITEM_SOLD_TO_CUSTOMER, nameof(item_sold));
        if (target is datum_reagent)
            RegisterSignal(target, COMSIG_REAGENT_SOLD_TO_CUSTOMER, nameof(reagent_sold));
        return dot;
    }

    // tg: code/datums/elements/food/venue_price.dm:19
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_ITEM_SOLD_TO_CUSTOMER);
        UnregisterSignal(target, COMSIG_REAGENT_SOLD_TO_CUSTOMER);
        return dot;
    }

    // tg: code/datums/elements/food/venue_price.dm:24
    [SignalHandler]
    public virtual object item_sold(obj_item thing_sold = null, mob_living_basic_robot_customer sold_to = null)
    {
        produce_cash(sold_to, thing_sold);
        return TRANSACTION_SUCCESS;
    }

    // tg: code/datums/elements/food/venue_price.dm:30
    [SignalHandler]
    public virtual object reagent_sold(datum_reagent reagent_sold = null, mob_living_basic_robot_customer sold_to = null, obj_item container = null)
    {
        produce_cash(sold_to, container);
        return TRANSACTION_SUCCESS;
    }

    // tg: code/datums/elements/food/venue_price.dm:36
    public virtual void produce_cash(mob_living_basic_robot_customer sold_to = null, obj_item container = null)
    {
        Dm.@new<obj_item_holochip>(get_turf(container), venue_price);
        playsound(container, Dm.resource("sound/effects/cashregister.ogg"), 60, TRUE);

        datum_venue venue_to_pay = ((datum_ai_controller)sold_to.ai_controller).blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;
        venue_to_pay.total_income += Dm.num(venue_price);
    }
}

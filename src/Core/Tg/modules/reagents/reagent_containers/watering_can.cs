// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/watering_can.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (the watering cans; the advanced can refills itself with water).

// tg: code/modules/reagents/reagent_containers/watering_can.dm:1
public partial class obj_item_reagent_containers_cup_watering_can : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/watering_can.dm:15
    [DmParams("mapload", "vol")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_SNAIL, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/watering_can.dm:28
public partial class obj_item_reagent_containers_cup_watering_can_advanced : obj_item_reagent_containers_cup_watering_can
{
    // tg: code/modules/reagents/reagent_containers/watering_can.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/watering_can.dm:44
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        datum_reagents holder = reagents as datum_reagents;
        ///How much to refill
        float refill_add = Dm.min(Dm.num(volume) - Dm.num(holder.total_volume), refill_rate * Dm.num(seconds_per_tick));
        if (refill_add > 0)
            await holder.add_reagent(refill_reagent, refill_add);
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/watering_can.dm:50
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/chewable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2b, D316: the pinatas' gum and lollipops). The var block is generated (D111).

/// Anything with this element will provide the reagents inside the
/// item to the user when it is equipped.
// tg: code/datums/elements/chewable.dm:3
public partial class datum_element_chewable : datum_element
{
    // tg: code/datums/elements/chewable.dm:17
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object metabolization_amount = Dm.arg(__extra, 0);
        object slots_to_check = Dm.arg(__extra, 1);
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        obj_item target_item = (obj_item)target;

        if (Dm.truthy(metabolization_amount))
        {
            // this prevents microdosing which causes the reagent to enter and then delete itself before it can be processed
            if (Dm.num(metabolization_amount) < REAGENTS_METABOLISM)
                throw CRASH(Dm.text("Attatching /datum/element/chewable to [] requires metabolization_amount to be higher than []u. The amount used was []u!", target, REAGENTS_METABOLISM, metabolization_amount), "code/datums/elements/chewable.dm", 26);

            this.metabolization_amount = Dm.num(metabolization_amount);
        }

        this.slots_to_check = Dm.truthy(slots_to_check) ? slots_to_check : target_item.slot_flags;

        RegisterSignal(target, COMSIG_ITEM_DROPPED, nameof(on_dropped));
        RegisterSignal(target, COMSIG_ITEM_EQUIPPED, nameof(on_equipped));
        return dot;
    }

    // tg: code/datums/elements/chewable.dm:36
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        Dm.remove(processing, source);
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_DROPPED, COMSIG_ITEM_EQUIPPED));
        return dot;
    }

    // tg: code/datums/elements/chewable.dm:41
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (processing.len == 0)
            return PROCESS_KILL;

        foreach (object i in Dm.each(processing))
        {
            obj_item item = i as obj_item;
            mob chewer = item.loc as mob;

            if (chewer == null || !Dm.truthy((item.reagents as datum_reagents)?.total_volume))
            {
                Dm.remove(processing, item);
                continue;
            }

            handle_reagents(item, seconds_per_tick);
        }
        return null;
    }

    // tg: code/datums/elements/chewable.dm:54
    public virtual void handle_reagents(obj_item item = null, object seconds_per_tick = null)
    {
        datum_reagents reagents = item.reagents as datum_reagents;

        float metabolism_amount = metabolization_amount * Dm.num(seconds_per_tick);
        if (!Dm.truthy(Waiting.NoSleep(reagents.trans_to(item.loc, metabolism_amount, methods: INGEST))))
            Waiting.NoSleep(reagents.remove_all(metabolism_amount));
    }

    // tg: code/datums/elements/chewable.dm:61
    [SignalHandler]
    public virtual void on_dropped(datum source = null, params object[] __extra)
    {
        Dm.remove(processing, source);
    }

    // tg: code/datums/elements/chewable.dm:65
    [SignalHandler]
    public virtual void on_equipped(datum source = null, mob equipper = null, object slot = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(slot, slots_to_check)))
        {
            Dm.append(processing, source);
            START_PROCESSING(SSdcs, this);
        }
        else
            Dm.remove(processing, source);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/reagent_refiller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326: the service cyborg's self-refilling glass). The var block is generated (D111).

/**
 * ## Reagent refiller
 * Refills any drinks poured out of the reagent container (and is allowed within the whitelisted reagents).
 */
// tg: code/datums/components/reagent_refiller.dm:5
public partial class datum_component_reagent_refiller : datum_component
{
    // tg: code/datums/components/reagent_refiller.dm:15
    [DmParams("time_to_refill", "power_draw_callback", "power_to_draw", "whitelisted_reagents")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object time_to_refill = Dm.arg(__extra, 0) ?? 60 * SECONDS;
        datum_callback power_draw_callback = Dm.arg<datum_callback>(__extra, 1);
        object power_to_draw = Dm.arg(__extra, 2) ?? 30;
        object whitelisted_reagents = Dm.arg(__extra, 3) ?? Dm.list(typeof(datum_reagent_consumable));
        if (!is_reagent_container(parent))
            return COMPONENT_INCOMPATIBLE;

        this.time_to_refill = time_to_refill;
        this.power_draw_callback = power_draw_callback;
        this.power_to_draw = power_to_draw;
        this.whitelisted_reagents = whitelisted_reagents as DmList;

        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/reagent_refiller.dm:31
    public override object Destroy(float? force = null)
    {
        power_draw_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/reagent_refiller.dm:35
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(refill));
        RegisterSignal(parent, COMSIG_ATOM_EXITED, nameof(delete_self));
    }

    // tg: code/datums/components/reagent_refiller.dm:39
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_INTERACTING_WITH_ATOM, COMSIG_ATOM_EXITED));
    }

    // tg: code/datums/components/reagent_refiller.dm:42
    [SignalHandler]
    public virtual void delete_self()
    {
        qdel(this);
    }

    /// Preps the reagent container for being refilled
    // tg: code/datums/components/reagent_refiller.dm:48
    [SignalHandler]
    public virtual void refill()
    {
        obj_item_reagent_containers container = parent as obj_item_reagent_containers;
        datum_reagents container_reagents = container.reagents as datum_reagents;
        float amount = min(Dm.num(container.amount_per_transfer_from_this) + Dm.num(container_reagents.total_volume), Dm.num(container_reagents.total_volume));
        if (amount == 0)
            return;

        datum_reagent refill = container_reagents.get_master_reagent() as datum_reagent;
        if (!Dm.truthy(is_path_in_list(refill?.GetType(), whitelisted_reagents)))
            return;

        _addtimer(Dm.@new<datum_callback>(this, nameof(add_reagents), container, container.loc, refill.GetType(), amount), Dm.num(time_to_refill), file: "code/datums/components/reagent_refiller.dm", line: 59);
    }

    /// Refills the reagent container, and uses cell power if applicable
    // tg: code/datums/components/reagent_refiller.dm:63
    public virtual async DmTask add_reagents(obj_item_reagent_containers target = null, object oldloc = null, object reagent_to_refill = null, object amount = null)
    {
        if (QDELETED(this) || QDELETED(target))
            return;
        if (target.loc != oldloc)
            return;

        await (target.reagents as datum_reagents).add_reagent(reagent_to_refill, amount);

        if (power_draw_callback != null)
            await power_draw_callback.Invoke(power_to_draw);
    }
}

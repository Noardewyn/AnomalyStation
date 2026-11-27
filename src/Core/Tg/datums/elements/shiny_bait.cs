// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/shiny_bait.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file.

///A component that gives fishing rod TRAIT_ROD_ATTRACT_SHINY_LOVERS when the attached item is used as bait.
// tg: code/datums/elements/shiny_bait.dm:2
public partial class datum_element_shiny_bait : datum_element
{
    // tg: code/datums/elements/shiny_bait.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        obj_item item = (obj_item)target;
        RegisterSignal(item, COMSIG_ITEM_FISHING_ROD_SLOTTED, nameof(on_fishing_rod_slotted));
        RegisterSignal(item, COMSIG_ITEM_FISHING_ROD_UNSLOTTED, nameof(on_fishing_rod_unslotted));
        if (istype(item.loc, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(item.loc, TRAIT_ROD_ATTRACT_SHINY_LOVERS, REF(this));
        else if (istype(item, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(item, TRAIT_ROD_ATTRACT_SHINY_LOVERS, Dm.text("[]_rod", REF(this)));
        return dot;
    }

    // tg: code/datums/elements/shiny_bait.dm:15
    public override object Detach(datum source = null, params object[] __extra)
    {
        obj_item item = (obj_item)source;
        UnregisterSignal(item, Dm.list(
            COMSIG_ITEM_FISHING_ROD_SLOTTED,
            COMSIG_ITEM_FISHING_ROD_UNSLOTTED));
        if (istype(item.loc, typeof(obj_item_fishing_rod)))
            REMOVE_TRAIT(item.loc, TRAIT_ROD_ATTRACT_SHINY_LOVERS, REF(this));
        else if (istype(item, typeof(obj_item_fishing_rod)))
            REMOVE_TRAIT(item, TRAIT_ROD_ATTRACT_SHINY_LOVERS, Dm.text("[]_rod", REF(this)));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/shiny_bait.dm:26
    [SignalHandler]
    public virtual void on_fishing_rod_slotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        if (Dm.eq(slot, ROD_SLOT_BAIT))
            ADD_TRAIT(rod, TRAIT_ROD_ATTRACT_SHINY_LOVERS, REF(source));
    }

    // tg: code/datums/elements/shiny_bait.dm:31
    [SignalHandler]
    public virtual void on_fishing_rod_unslotted(datum source = null, obj_item_fishing_rod rod = null, object slot = null)
    {
        if (Dm.eq(slot, ROD_SLOT_BAIT))
            REMOVE_TRAIT(rod, TRAIT_ROD_ATTRACT_SHINY_LOVERS, REF(source));
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/holderloving.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file (a drone's built-in tools). The var block is generated (D111).

/** Holder Loving Component
 *
 * When you drop an object onto a turf it gets moved back into its parent holder
 *
 * Prevents you from force moving the object into any other location that isn't its parent holder
 */
// tg: code/datums/components/holderloving.dm:7
public partial class datum_component_holderloving : datum_component
{
    // tg: code/datums/components/holderloving.dm:14
    [DmParams("holder")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object holder = Dm.arg(__extra, 0);
        await DmTask.CompletedTask;
        if (!isitem(parent) || !Dm.truthy(holder))
            return COMPONENT_INCOMPATIBLE;
        this.holder = holder as atom;
        return null;
    }

    // tg: code/datums/components/holderloving.dm:19
    public override async DmTask RegisterWithParent()
    {
        await DmTask.CompletedTask;
        RegisterSignal(holder, COMSIG_QDELETING, nameof(holder_deleting));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(check_my_loc));
        RegisterSignal(parent, COMSIG_ITEM_PRE_UNEQUIP, nameof(can_be_moved));
    }

    // tg: code/datums/components/holderloving.dm:24
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await DmTask.CompletedTask;
        UnregisterSignal(holder, Dm.list(COMSIG_QDELETING));
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_DROPPED, COMSIG_ITEM_PRE_UNEQUIP));
    }

    // tg: code/datums/components/holderloving.dm:28
    public virtual void holder_deleting(datum source = null, object force = null)
    {
        qdel(parent);
    }

    // tg: code/datums/components/holderloving.dm:33
    public virtual bool is_valid_location(atom location = null)
    {
        if (location == holder || (location == holder.loc && ismob(holder.loc)))
            return true;
        return false;
    }

    // tg: code/datums/components/holderloving.dm:41
    public virtual void check_my_loc(datum source = null)
    {
        var item_parent = (obj_item)parent;
        if (!is_valid_location(item_parent.loc))
            Waiting.NoSleep(item_parent.forceMove(holder));
    }

    // tg: code/datums/components/holderloving.dm:48
    public virtual object can_be_moved(obj_item I = null, object force = null, atom newloc = null, object no_move = null, object invdrop = null, object silent = null)
    {
        //allow the item to be dropped on the turf so it can be later moved back into the holder as a convinience tool
        if (isturf(newloc) || is_valid_location(newloc))
            return NONE;

        //prevent this item from being moved anywhere else
        return COMPONENT_ITEM_BLOCK_UNEQUIP;
    }
}

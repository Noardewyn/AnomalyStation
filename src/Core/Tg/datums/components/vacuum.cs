// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/vacuum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H4 (D333, the janitor): the whole file: the janicart's vacuum upgrade, which sucks the items of each tile it moves onto into
// the hooked trash bag. The var block is generated (D111).

/**
 * # Vacuum Component
 *
 * Adds a vacuum functionality to an atom, requires a trashbag to be linked
 * using signals
 *
 */
// tg: code/datums/components/vacuum.dm:8
public partial class datum_component_vacuum : datum_component
{
    // tg: code/datums/components/vacuum.dm:12
    [DmParams("connected_bag")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        var connected_bag = Dm.arg(__extra, 0) as obj_item_storage_bag_trash;
        object dot = await base.Initialize(__extra);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;
        if (connected_bag != null)
            attach_bag(null, connected_bag);
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(suck));
        RegisterSignal(parent, COMSIG_VACUUM_BAG_ATTACH, nameof(attach_bag));
        RegisterSignal(parent, COMSIG_VACUUM_BAG_DETACH, nameof(detach_bag));
        return dot;
    }

    /**
     * Called when parent moves, deligates vacuuming functionality
     *
     * Arguments:
     * * suckee - The source of the signal
     */
    // tg: code/datums/components/vacuum.dm:28
    [SignalHandler]
    public virtual void suck(datum suckee = null)
    {
        // get tile to suck on
        var AM = suckee as atom_movable;
        var tile = AM.loc as turf;
        if (tile == null)
            return;

        // no bag attached, don't bother
        if (vacuum_bag == null)
            return;

        // suck the things
        Waiting.DetachSync(() => suck_items(tile)); // INVOKE_ASYNC
    }

    /**
     * Sucks up items as possible from a provided turf into the connected trash bag
     *
     * Arguments:
     * * tile - The tile upon which to vacuum up items
     */
    // tg: code/datums/components/vacuum.dm:50
    public virtual void suck_items(turf tile = null)
    {
        bool sucked = false;
        foreach (object potential_item in Dm.each<object>(tile))
        {
            if (potential_item is not obj_item item)
                continue;
            if (Dm.truthy(vacuum_bag.atom_storage.attempt_insert(item)))
                sucked = true; // track that we successfully sucked up something
        }

        // if we did indeed suck up something, play a funny noise
        if (sucked)
            playsound(parent as atom, SFX_RUSTLE, 50, TRUE, -5);
    }

    /**
     * Handler for when a new trash bag is attached
     *
     * Arguments:
     * * source - The source of the signal
     * * new_bag - The new bag being installed
     */
    // tg: code/datums/components/vacuum.dm:70
    [SignalHandler]
    public virtual void attach_bag(datum source = null, obj_item_storage_bag_trash new_bag = null)
    {
        vacuum_bag = new_bag;
        RegisterSignal(new_bag, COMSIG_QDELETING, nameof(detach_bag));
    }

    /**
     * Handler for when a trash bag is detached
     *
     * Arguments:
     * * source - The source of the signal
     */
    // tg: code/datums/components/vacuum.dm:82
    [SignalHandler]
    public virtual void detach_bag(datum source = null)
    {
        if (vacuum_bag != null) // null check to avoid runtime on bag being deleted then sending detach as a result from parent
        {
            UnregisterSignal(vacuum_bag, COMSIG_QDELETING);
            vacuum_bag = null;
        }
    }
}

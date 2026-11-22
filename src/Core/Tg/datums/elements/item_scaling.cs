// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/item_scaling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4a, D316: the mail shrinks on the floor). The var block and the element flags are generated.

/**
 * Element for scaling item appearances in the overworld or in inventory/storage.
 *
 * This bespoke element allows for items to have varying sizes depending on their location.
 * The overworld simply refers to items being on a turf.  Inventory includes HUD item slots,
 * and storage is anywhere a storage component is used.
 * Scaling should affect the item's icon and all attached overlays (such as blood decals).
 *
 */
// tg: code/datums/elements/item_scaling.dm:10
public partial class datum_element_item_scaling : datum_element
{
    // tg: code/datums/elements/item_scaling.dm:31
    [DmParams("target", "overworld_scaling", "storage_scaling")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object new_overworld_scaling = Dm.arg(__extra, 0);
        object new_storage_scaling = Dm.arg(__extra, 1);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        // Initial scaling set to overworld_scaling when item is spawned.
        scale(target, new_overworld_scaling);

        overworld_scaling = new_overworld_scaling;
        storage_scaling = new_storage_scaling;

        // Make sure overlays also inherit the scaling.
        ADD_KEEP_TOGETHER(target, ITEM_SCALING_TRAIT);

        // When moved sends a signal.
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(scale_by_loc));
        return dot;
    }

    // tg: code/datums/elements/item_scaling.dm:55
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, COMSIG_MOVABLE_MOVED);

        REMOVE_KEEP_TOGETHER(target, ITEM_SCALING_TRAIT);

        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/item_scaling.dm:72
    public virtual void scale(datum source = null, object scaling = null)
    {
        atom scalable_object = source as atom;
        matrix M = matrix();
        scalable_object.transform = M.Scale(Dm.num(scaling));
    }

    //Grabs any move signals and checks its loc, properly scaling it when in storage,inhand, or in world.
    // tg: code/datums/elements/item_scaling.dm:78
    [SignalHandler]
    public virtual void scale_by_loc(atom scale = null)
    {
        if (isturf(scale.loc))
            scale_overworld(scale);
        else
            scale_storage(scale);
    }

    // tg: code/datums/elements/item_scaling.dm:93
    [SignalHandler]
    public virtual void scale_overworld(datum source = null)
    {
        scale(source, overworld_scaling);
    }

    // tg: code/datums/elements/item_scaling.dm:105
    [SignalHandler]
    public virtual void scale_storage(datum source = null)
    {
        scale(source, storage_scaling);
    }
}

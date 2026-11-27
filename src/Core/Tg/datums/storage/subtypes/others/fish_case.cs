// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/others/fish_case.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish case's storage, one fish or piece of aquarium equipment, and the case that grows with its fish.
// The var blocks are generated (D111).

// tg: code/datums/storage/subtypes/others/fish_case.dm:1
public partial class datum_storage_fish_case : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fish_case.dm:6
    public override object can_insert(obj_item to_insert = null, mob user = null, float? messages = null, float? force = null)
    {
        object dot = base.can_insert(to_insert, user, messages, force);
        if (!Dm.truthy(dot))
            return dot;

        if (!HAS_TRAIT(to_insert, TRAIT_AQUARIUM_CONTENT))
        {
            if (Dm.truthy(messages) && user != null)
                user.balloon_alert(user, "can't hold!");
            return FALSE;
        }
        return dot;
    }
}

// tg: code/datums/storage/subtypes/others/fish_case.dm:17
public partial class datum_storage_fish_case_adjust_size : datum_storage_fish_case
{
    /*
     * Change the size of the storage item to match the inserted item's
     * Because of that, we also check if conditions to keep it inside another storage or pockets are still met.
     */
    // tg: code/datums/storage/subtypes/others/fish_case.dm:23
    public override void handle_enter(datum source = null, obj_item arrived = null)
    {
        base.handle_enter(source, arrived);
        if (!isitem(parent) || arrived is not obj_item)
            return;
        obj_item item_parent = (obj_item)parent;
        if (arrived.w_class <= item_parent.w_class)
            return;
        item_parent.update_weight_class(arrived.w_class);
    }

    // tg: code/datums/storage/subtypes/others/fish_case.dm:32
    public override void handle_exit(datum source = null, obj_item gone = null)
    {
        base.handle_exit(source, gone);
        if (!isitem(parent) || gone is not obj_item)
            return;
        obj_item item_parent = (obj_item)parent;
        item_parent.update_weight_class(Dm.initial(item_parent, (obj_item p) => p.w_class));
    }
}

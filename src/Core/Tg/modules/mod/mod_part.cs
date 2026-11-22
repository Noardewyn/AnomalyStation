// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_part.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file whole. The MODsuit part datum: the deployable part it handles, its sealed state, its seal messages and
// layers, and the item it is worn over (an overslotted suit or helmet), drawn under the part while the part is unsealed. The var
// block is generated (D111).

/// Datum to handle interactions between a MODsuit and its parts.
// tg: code/modules/mod/mod_part.dm:2
public partial class datum_mod_part : datum
{
    // tg: code/modules/mod/mod_part.dm:18
    public override object Destroy(float? force = null)
    {
        // To avoid qdel loops in MOD control units, since they're also a part
        if (!QDELING(part_item))
            qdel(part_item);
        part_item = null;
        overslotting = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_part.dm:26
    public virtual void set_item(obj_item new_part = null)
    {
        part_item = new_part;
        RegisterSignal(part_item, COMSIG_ITEM_GET_SEPARATE_WORN_OVERLAYS, nameof(get_separate_worn_overlays));
    }

    // If we're overslotting an item, add its visual as an underlay
    // tg: code/modules/mod/mod_part.dm:31
    [SignalHandler]
    public virtual void get_separate_worn_overlays(obj_item source = null, DmList overlays = null, mutable_appearance standing = null, mutable_appearance draw_target = null, object isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        if (!Dm.truthy(overslotting) || Dm.truthy(@sealed))
            return;

        object checked_slot = source.slot_flags;
        if (ismob(source.loc))
        {
            mob as_mob = source.loc as mob;
            checked_slot = as_mob.get_slot_by_item(source);
        }
        mutable_appearance worn_overlay = Waiting.NoSleep(overslotting.build_worn_icon(default_layer: -draw_target.layer + 0.1f, default_icon_file: get_default_icon_by_slot(checked_slot), bodyshape: bodyshape)) as mutable_appearance;
        foreach (mutable_appearance overlay in Dm.each<mutable_appearance>(worn_overlay.overlays))
            overlay.layer = draw_target.layer + 0.1f;
        Dm.append(ref overlays, worn_overlay);
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/others/pod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/storage/subtypes/others/pod.dm:2
public partial class datum_storage_pod : datum_storage
{
    // tg: code/datums/storage/subtypes/others/pod.dm:8
    public override async DmTask<object> open_storage(object to_show = null)
    {
        if (Dm.truthy(locked) && isliving(to_show)) //Observers get to see anyway
        {
            to_chat(to_show, span_warning("The storage unit will only unlock during a Red or Delta security alert."));
            return FALSE;
        }
        return await base.open_storage(to_show);
    }

    // tg: code/datums/storage/subtypes/others/pod.dm:14
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // all of these are a type below what actually spawn with
        // (IE all space suits instead of just the emergency ones)
        // because an enterprising traitor might be able to hide things,
        // like their syndicate toolbox or softsuit. may be fun?
        set_holdable(exception_hold_list: Dm.list(
            typeof(obj_item_clothing_suit_space),
            typeof(obj_item_pickaxe),
            typeof(obj_item_storage_toolbox)));

        if (SSsecurity_level == null) // a toy world has none: the safe stays as it is
            return;
        RegisterSignal(SSsecurity_level, COMSIG_SECURITY_LEVEL_CHANGED, nameof(update_lock));
        update_lock(new_level: SSsecurity_level.get_current_level_as_number());
    }

    // tg: code/datums/storage/subtypes/others/pod.dm:30
    public override void set_parent(atom new_parent = null)
    {
        base.set_parent(new_parent);
        RegisterSignal(parent, COMSIG_ATOM_AFTER_SHUTTLE_MOVE, nameof(pod_launch));
    }

    // tg: code/datums/storage/subtypes/others/pod.dm:34
    public virtual void update_lock(datum source = null, object new_level = null)
    {
        if (Dm.truthy(always_unlocked))
            return;

        locked = Dm.num(new_level) < SEC_LEVEL_RED ? STORAGE_FULLY_LOCKED : STORAGE_NOT_LOCKED;
        parent.update_appearance(UPDATE_ICON_STATE);
        if (Dm.truthy(locked)) // future todo : make `locked` a setter so this behavior can be built in (avoids exploits)
            close_all();
    }

    // tg: code/datums/storage/subtypes/others/pod.dm:44
    public virtual void pod_launch(datum source = null, turf old_turf = null)
    {
        // This check is to ignore the movement of the shuttle from the transit level to the station as it is loaded in.
        if (old_turf != null && Dm.truthy(is_reserved_level(old_turf.z)))
            return;
        // If the pod was launched, the storage will always open.
        always_unlocked = TRUE;
        locked = STORAGE_NOT_LOCKED;
        parent.update_appearance(UPDATE_ICON_STATE);
    }
}

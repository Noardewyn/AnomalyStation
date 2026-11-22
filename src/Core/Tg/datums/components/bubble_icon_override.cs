// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/bubble_icon_override.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the whole file (the lawyer's badge speaks from the "lawyer" bubble). The var block is generated.

/**
 * A component that overrides the bubble_icon variable when equipped or implanted
 * while having a simple priority system, so accessories have higher priority than
 * organs, for example.
 */
// tg: code/datums/components/bubble_icon_override.dm:7
public partial class datum_component_bubble_icon_override : datum_component
{
    // tg: code/datums/components/bubble_icon_override.dm:14
    [DmParams("bubble_icon", "priority")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object bubble_icon = Dm.arg(__extra, 0);
        object priority = Dm.arg(__extra, 1);
        if (!isclothing(parent) && !isorgan(parent))
            return COMPONENT_INCOMPATIBLE;
        this.bubble_icon = bubble_icon;
        this.priority = priority;
        return null;
    }

    // tg: code/datums/components/bubble_icon_override.dm:21
    public override async DmTask RegisterWithParent()
    {
        if (isclothing(parent))
        {
            RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equipped));
            RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_dropped));
        }
        else if (isorgan(parent))
        {
            RegisterSignal(parent, COMSIG_ORGAN_IMPLANTED, nameof(on_organ_implanted));
            RegisterSignal(parent, COMSIG_ORGAN_REMOVED, nameof(on_organ_removed));
        }
        object target = get_bubble_icon_target();
        if (Dm.truthy(target))
            register_owner(target as datum);
    }

    // tg: code/datums/components/bubble_icon_override.dm:32
    public virtual void register_owner(datum owner = null)
    {
        RegisterSignal(owner, COMSIG_GET_BUBBLE_ICON, nameof(return_bubble_icon));
        get_bubble_icon(owner);
    }

    // tg: code/datums/components/bubble_icon_override.dm:36
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ITEM_EQUIPPED,
            COMSIG_ITEM_DROPPED,
            COMSIG_ORGAN_IMPLANTED,
            COMSIG_ORGAN_REMOVED));
        object target = get_bubble_icon_target();
        if (Dm.truthy(target))
            unregister_owner(target as datum);
    }

    // tg: code/datums/components/bubble_icon_override.dm:48
    public virtual void unregister_owner(datum owner = null)
    {
        UnregisterSignal(owner, Dm.list(COMSIG_GET_BUBBLE_ICON));
        get_bubble_icon(owner);
    }

    ///Returns the potential wearer/owner of the object when the component is un/registered to/from it
    // tg: code/datums/components/bubble_icon_override.dm:53
    public virtual object get_bubble_icon_target()
    {
        if (isclothing(parent))
        {
            obj_item_clothing clothing = parent as obj_item_clothing;
            if (clothing is obj_item_clothing_accessory)
            {
                clothing = clothing.loc as obj_item_clothing;
                if (!(clothing is obj_item_clothing))
                    return null;
            }
            mob_living wearer = clothing.loc as mob_living;
            if (wearer is mob_living && Dm.truthy(Dm.bitand(wearer.get_slot_by_item(clothing), clothing.slot_flags)))
                return parent;
        }
        else if (isorgan(parent))
        {
            obj_item_organ organ = parent as obj_item_organ;
            return organ.owner;
        }
        return null;
    }

    // tg: code/datums/components/bubble_icon_override.dm:67
    [SignalHandler]
    public virtual void on_equipped(obj_item source = null, mob equipper = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, source.slot_flags)))
            register_owner(equipper);
    }

    // tg: code/datums/components/bubble_icon_override.dm:72
    [SignalHandler]
    public virtual void on_dropped(obj_item source = null, mob dropper = null)
    {
        unregister_owner(dropper);
    }

    // tg: code/datums/components/bubble_icon_override.dm:76
    [SignalHandler]
    public virtual void on_organ_implanted(obj_item_organ source = null, mob owner = null)
    {
        register_owner(owner);
    }

    // tg: code/datums/components/bubble_icon_override.dm:80
    [SignalHandler]
    public virtual void on_organ_removed(obj_item_organ source = null, mob owner = null)
    {
        unregister_owner(owner);
    }

    /**
     * Get the bubble icon with the highest priority from all instances of bubble_icon_override
     * currently registered with the target.
     */
    // tg: code/datums/components/bubble_icon_override.dm:88
    public virtual void get_bubble_icon(datum target = null)
    {
        if (QDELETED(parent))
            return;
        // get_bubble_icon_target returns the parent item itself while it is worn (tg's own quirk), which has no bubble_icon to set
        if (!(target is mob_living living))
            return;
        DmList holder = Dm.list(new object[] { null });
        if (Dm.truthy(living._listen_lookup?[COMSIG_GET_BUBBLE_ICON]))
            living._SendSignal(COMSIG_GET_BUBBLE_ICON, Dm.list(living, holder));
        object bubble_icon = holder[1];
        living.bubble_icon = (string)(Dm.truthy(bubble_icon) ? bubble_icon : Dm.initial(living, (mob_living p) => p.bubble_icon));
    }

    // tg: code/datums/components/bubble_icon_override.dm:96
    [SignalHandler]
    public virtual void return_bubble_icon(datum source = null, DmList holder = null)
    {
        object enemy_priority = holder[holder[1]];
        if (Dm.num(enemy_priority) < Dm.num(priority))
        {
            holder[1] = bubble_icon;
            holder[bubble_icon] = priority;
        }
    }
}

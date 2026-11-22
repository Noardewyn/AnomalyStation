// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/drag_pickup.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice E: the whole file (the defibrillator is dragged off one's back onto a hand). The var block is generated.

/**
 * drag_pickup element
 *
 * Allowing things to be picked up or unequipped by mouse-dragging.
 * Useful for objects which have an interaction on click
 */
// tg: code/datums/elements/drag_pickup.dm:7
public partial class datum_element_drag_pickup : datum_element
{
    // tg: code/datums/elements/drag_pickup.dm:9
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_MOUSEDROP_ONTO, nameof(pick_up));
        return await base.Attach(target, __extra);
    }

    // tg: code/datums/elements/drag_pickup.dm:15
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_MOUSEDROP_ONTO);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/drag_pickup.dm:19
    [SignalHandler]
    public virtual object pick_up(atom_movable source = null, atom over = null, mob user = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(source, FORBID_TELEKINESIS_REACH))))
            return NONE;
        if (Dm.truthy(source.anchored))
            return NONE;
        if (source.loc == user && isitem(source))
        {
            obj_item item_source = source as obj_item;
            if (!Dm.truthy(item_source.can_mob_unequip(user)))
                return COMPONENT_CANCEL_MOUSEDROP_ONTO;
        }

        if (over == user)
        {
            Waiting.Detach(user.put_in_hands(source as obj_item));
        }
        else if (istype(over, typeof(atom_movable_screen_inventory_hand)))
        {
            atom_movable_screen_inventory_hand selected_hand = over as atom_movable_screen_inventory_hand;
            Waiting.NoSleep(user.putItemFromInventoryInHandIfPossible(source as obj_item, selected_hand.held_index));
            source.add_fingerprint(user);
        }
        return COMPONENT_CANCEL_MOUSEDROP_ONTO;
    }
}

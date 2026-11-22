// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/action_item_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 3: the whole file (an item action button shows its item's look: the tank, the magboots, the breath mask).
// The var block is generated.

/**
 * Apply to an action to allow it to take an item
 * and apply it as an overlay of the action button
 */
// tg: code/datums/components/action_item_overlay.dm:5
public partial class datum_component_action_item_overlay : datum_component
{
    // tg: code/datums/components/action_item_overlay.dm:14
    [DmParams("item", "item_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        atom_movable item = Dm.arg<atom_movable>(__extra, 0);
        datum_callback item_callback = Dm.arg<datum_callback>(__extra, 1);
        if (!istype(parent, typeof(datum_action)))
            return COMPONENT_INCOMPATIBLE;

        if (!(item == null || item is atom_movable))
            throw Dm.CRASH(assertion_message("code/datums/components/action_item_overlay.dm", 18, "isnull(item) || istype(item)"), "code/datums/components/action_item_overlay.dm", 18);

        if (item == null && item_callback == null)
        {
            stack_trace(Dm.text("[] created without a reference item or an item callback - one or the other is required.", GetType()), "code/datums/components/action_item_overlay.dm", 21);
            return COMPONENT_INCOMPATIBLE;
        }

        this.item_ref = WEAKREF(item) as datum_weakref;
        this.item_callback = item_callback;
        return null;
    }

    // tg: code/datums/components/action_item_overlay.dm:27
    public override object Destroy(float? force = null)
    {
        item_ref = null;
        item_callback = null;
        item_appearance = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/action_item_overlay.dm:33
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ACTION_OVERLAY_APPLY, nameof(on_overlays_applied));

        datum_action parent_action = parent as datum_action;
        await parent_action.build_all_button_icons(UPDATE_BUTTON_OVERLAY);
    }

    // tg: code/datums/components/action_item_overlay.dm:39
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ACTION_OVERLAY_APPLY);

        // If we're being unregistered / deleted but our parent is sticking around,
        // force an overlay update to get rid of our item appearance
        if (!QDELING(parent))
        {
            datum_action parent_action = parent as datum_action;
            await parent_action.build_all_button_icons(UPDATE_BUTTON_OVERLAY);
        }
    }

    /// Signal proc for [COMSIG_ACTION_OVERLAY_APPLY], applies the item appearance if possible.
    // tg: code/datums/components/action_item_overlay.dm:49
    [SignalHandler]
    public virtual void on_overlays_applied(datum_action source = null, atom_movable_screen_movable_action_button current_button = null, object force = null)
    {
        // We're in the middle of being removed / deleted, remove our associated overlay
        if (QDELING(this))
        {
            if (Dm.truthy(item_appearance))
            {
                current_button.cut_overlay(item_appearance);
                item_appearance = null;
            }
            return;
        }

        atom_movable muse = (item_callback != null && Waiting.NoSleep(item_callback.Invoke()) is var invoked && Dm.truthy(invoked) ? invoked : item_ref?.resolve()) as atom_movable;
        if (!(muse is atom_movable))
        {
            if (Dm.truthy(item_appearance)) // New item does not exist but we have an old appearance
            {
                current_button.cut_overlay(item_appearance);
                item_appearance = null;
            }
            return;
        }

        if (Dm.truthy(item_appearance))
        {
            // For caching purposes, we will try not to update if we don't need to
            if (!Dm.truthy(force) && Dm.eq(item_appearance.icon, muse.icon) && Dm.eq(item_appearance.icon_state, muse.icon_state))
                return;
            current_button.cut_overlay(item_appearance);
        }

        mutable_appearance muse_appearance = Dm.@new<mutable_appearance>(muse.appearance);
        muse_appearance.plane = FLOAT_PLANE;
        muse_appearance.layer = FLOAT_LAYER;
        muse_appearance.pixel_x = 0;
        muse_appearance.pixel_y = 0;

        current_button.add_overlay(muse_appearance);
        item_appearance = muse_appearance;
    }
}

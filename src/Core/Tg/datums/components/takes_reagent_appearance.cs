// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/takes_reagent_appearance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H3 (D333, the bar): the whole file. The var block is generated (D111).

/**
 * ## "Takes reagent appearance" Component
 *
 * Bit of a mouthful, but when applied to an item that can hold reagents (primarily reagent containers),
 * said item will take on an appearance based on the majority share reagent inside
 *
 * This is more than just "changing the color a bit" or "applies an overlay", this is
 * an entire icon / icon state / name change, making it look like a different item entirely
 *
 * This is done by cross-referencing the glass style datums. See [/datum/glass_style] for more information about that.
 *
 * An example usage is bartender mixed drinks - each reagent gets its own fancy drink sprite
 */
// tg: code/datums/components/takes_reagent_appearance.dm:14
public partial class datum_component_takes_reagent_appearance : datum_component
{
    // tg: code/datums/components/takes_reagent_appearance.dm:26
    [DmParams("on_icon_changed", "on_icon_reset", "base_container_type")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        var on_icon_changed = Dm.arg(__extra, 0) as datum_callback;
        var on_icon_reset = Dm.arg(__extra, 1) as datum_callback;
        var base_container_type = Dm.arg(__extra, 2) as Type;
        await DmTask.CompletedTask;
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        var item_parent = (obj_item)parent;
        if (item_parent.reagents == null)
            return COMPONENT_INCOMPATIBLE;

        icon_pre_change = item_parent.icon;
        icon_state_pre_change = Dm.truthy(item_parent.base_icon_state) ? item_parent.base_icon_state : item_parent.icon_state;

        this.on_icon_changed = on_icon_changed;
        this.on_icon_reset = on_icon_reset;

        this.base_container_type = base_container_type ?? parent.GetType();
        return null;
    }

    // tg: code/datums/components/takes_reagent_appearance.dm:46
    public override object Destroy(float? force = null)
    {
        on_icon_changed = null;
        on_icon_reset = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/takes_reagent_appearance.dm:51
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_APPEARANCE, nameof(on_update_appearance));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/takes_reagent_appearance.dm:54
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_UPDATE_APPEARANCE);
        if (QDELING(parent))
            return;

        var item_parent = (obj_item)parent;
        item_parent.name = Dm.initial(item_parent.GetType(), (obj_item i) => i.name);
        item_parent.desc = Dm.initial(item_parent.GetType(), (obj_item i) => i.desc);
        item_parent.icon = icon_pre_change;
        item_parent.icon_state = Dm.str(icon_state_pre_change);
        if (on_icon_reset != null)
            await on_icon_reset.Invoke();
        item_parent.update_appearance();
    }

    /// Signal proc for [COMSIG_ATOM_UPDATE_APPEARANCE]
    /// We hook into the update appearance proc to perform our own update based on our glass style
    /// Preventing any further updates down the line on successes
    // tg: code/datums/components/takes_reagent_appearance.dm:70
    [SignalHandler]
    public virtual object on_update_appearance(datum source = null, object updates = null)
    {
        float dot = NONE;

        var main_style = get_main_reagent_style() as datum_glass_style;
        if (Dm.truthy(Dm.bitand(updates, UPDATE_NAME)))
            dot = Dm.bitor(dot, update_name(main_style));
        if (Dm.truthy(Dm.bitand(updates, UPDATE_DESC)))
            dot = Dm.bitor(dot, update_desc(main_style));
        if (Dm.truthy(Dm.bitand(updates, UPDATE_ICON)))
            dot = Dm.bitor(dot, update_icon(main_style));
        return dot;
    }

    /**
     * Performs the name update.
     *
     * * Returns [COMSIG_ATOM_NO_UPDATE_NAME] if one was complete
     * * Returns [NONE] if nothing happened
     * * Returns [NONE] if the name was reset to initial state
     */
    // tg: code/datums/components/takes_reagent_appearance.dm:91
    public virtual float update_name(datum_glass_style style = null)
    {
        if (HAS_TRAIT(parent, TRAIT_WAS_RENAMED))
            return NONE;

        var item_parent = (obj_item)parent;
        if (style == null)
        {
            // no style (reset)
            item_parent.name = Dm.initial(item_parent.GetType(), (obj_item i) => i.name);
        }
        else if (Dm.truthy(style.name))
        {
            // style
            style.set_name(item_parent);
            return COMSIG_ATOM_NO_UPDATE_NAME;
        }

        return NONE;
    }

    /**
     * Performs the description update.
     *
     * * Returns [COMSIG_ATOM_NO_UPDATE_DESC] if one was complete
     * * Returns [NONE] if nothing happened
     * * Returns [NONE] if the description was reset to initial state
     */
    // tg: code/datums/components/takes_reagent_appearance.dm:113
    public virtual float update_desc(datum_glass_style style = null)
    {
        if (HAS_TRAIT(parent, TRAIT_WAS_RENAMED))
            return NONE;

        var item_parent = (obj_item)parent;
        if (style == null)
        {
            // no style (reset)
            item_parent.desc = Dm.initial(item_parent.GetType(), (obj_item i) => i.desc);
        }
        else if (Dm.truthy(style.desc))
        {
            // style
            style.set_desc(item_parent);
            return COMSIG_ATOM_NO_UPDATE_DESC;
        }

        return NONE;
    }

    /**
     * Performs the icon update.
     *
     * * Returns [COMSIG_ATOM_NO_UPDATE_ICON] if an icon or icon state ocurred
     * * Returns [NONE] if the icon or icon state was reset to base state
     */
    // tg: code/datums/components/takes_reagent_appearance.dm:134
    public virtual float update_icon(datum_glass_style style = null)
    {
        var item_parent = (obj_item)parent;
        if (style == null)
        {
            // no style (reset)
            item_parent.icon = icon_pre_change;
            item_parent.icon_state = Dm.str(icon_state_pre_change);
        }
        else if (Dm.truthy(style.icon) && Dm.truthy(style.icon_state))
        {
            // style
            style.set_appearance(item_parent);
            on_icon_changed?.InvokeAsync(style);
            return COMSIG_ATOM_NO_UPDATE_ICON_STATE;
        }

        // Reset gets invoked regardless, as further updates may "reset" the icon yet
        on_icon_reset?.InvokeAsync();
        return NONE;
    }

    /**
     * Gets the correspinding style based on the parent's state and reagents within
     *
     * * Returns null if its reagents are empty
     * * Returns null if no majority reagent was found
     * * Otherwise returns a glass style datum
     */
    // tg: code/datums/components/takes_reagent_appearance.dm:157
    public virtual object get_main_reagent_style()
    {
        var item_parent = (obj_item)parent;
        var holder = (datum_reagents)item_parent.reagents;
        if (holder.total_volume <= 0)
            return null;
        var main_reagent = holder.get_master_reagent() as datum_reagent;
        if (main_reagent == null)
            return null;

        return (GLOB.glass_style_singletons[base_container_type] as DmList)?[main_reagent.GetType()];
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/toggles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the whole file but the hooded head type's var block (generated). The hooded suits' hood is the toggle_attached_clothing
// component (datums/components/toggle_attached_clothing.cs), the toggle suits' buttons the toggle_icon component
// (datums/components/toggle_suit.cs). [DmPath] of the hooded suit is here: the earliest .dme file that declares the type
// (rules 2.3).

/// Subtype with support for hoods
/// You no longer actually need to extend this and can just add the component yourself without a lot of this boilerplate code
// tg: code/modules/clothing/suits/toggles.dm:4
[DmPath("/obj/item/clothing/suit/hooded")]
public partial class obj_item_clothing_suit_hooded : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/toggles.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (hoodtype == null)
            return dot;
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_toggle_attached_clothing),
            Dm.named("deployable_type", hoodtype),
            Dm.named("equipped_slot", ITEM_SLOT_HEAD),
            Dm.named("action_name", "Toggle Hood"),
            Dm.named("destroy_on_removal", alternative_mode),
            Dm.named("parent_icon_state_suffix", hood_up_affix),
            Dm.named("down_overlay_state_suffix", hood_down_overlay_suffix),
            Dm.named("pre_creation_check", Dm.@new<datum_callback>(this, nameof(can_create_hood))),
            Dm.named("on_created", Dm.@new<datum_callback>(this, nameof(on_hood_created))),
            Dm.named("on_deployed", Dm.@new<datum_callback>(this, nameof(on_hood_up))),
            Dm.named("on_removed", Dm.@new<datum_callback>(this, nameof(on_hood_down))),
            Dm.named("auto_deploy_on_outfit_equip", auto_deploy_hood_on_outfit_equip)));
        return dot;
    }

    // tg: code/modules/clothing/suits/toggles.dm:35
    public override object Destroy(float? force = null)
    {
        hood = null;
        return base.Destroy(force);
    }

    /// Override to only create the hood conditionally
    // tg: code/modules/clothing/suits/toggles.dm:40
    public virtual object can_create_hood()
    {
        return TRUE;
    }

    /// Called when the hood is instantiated
    // tg: code/modules/clothing/suits/toggles.dm:44
    [ShouldCallParent]
    public virtual async DmTask on_hood_created(obj_item_clothing_head_hooded hood = null)
    {
        this.hood = hood;
        RegisterSignal(hood, COMSIG_QDELETING, nameof(on_hood_deleted));
    }

    /// Called when hood is deleted
    // tg: code/modules/clothing/suits/toggles.dm:50
    [SignalHandler]
    [ShouldCallParent]
    public virtual void on_hood_deleted()
    {
        hood = null;
    }

    /// Called when the hood is worn
    // tg: code/modules/clothing/suits/toggles.dm:56
    public virtual void on_hood_up(obj_item_clothing_head_hooded hood = null)
    {
        return;
    }

    /// Called when the hood is hidden
    // tg: code/modules/clothing/suits/toggles.dm:60
    public virtual void on_hood_down(obj_item_clothing_head_hooded hood = null)
    {
        return;
    }
}

// tg: code/modules/clothing/suits/toggles.dm:64
public partial class obj_item_clothing_suit_toggle : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/toggles.dm:68
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_toggle_icon), toggle_noun));
        return dot;
    }
}

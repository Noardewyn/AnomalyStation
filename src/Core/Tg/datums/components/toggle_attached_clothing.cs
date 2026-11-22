// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/toggle_attached_clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the whole file (the hooded suits' hoods). Every proc of the chain runs inside a signal handler or an
// Initialize, which tg does not let sleep, so the translated calls that are DmTask slots go through Waiting.NoSleep. The var
// block is generated.

/**
 * Component which allows clothing to deploy a different kind of clothing onto you.
 * The simplest example is hooded suits deploying hoods onto your head.
 */
// tg: code/datums/components/toggle_attached_clothing.dm:7
public partial class datum_component_toggle_attached_clothing : datum_component
{
    // tg: code/datums/components/toggle_attached_clothing.dm:37
    [DmParams("deployable_type", "equipped_slot", "action_name", "destroy_on_removal", "parent_icon_state_suffix", "down_overlay_state_suffix", "auto_deploy_on_outfit_equip", "pre_creation_check", "on_created", "on_deployed", "on_removed")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        Type deployable_type = Dm.arg(__extra, 0) as Type;
        object equipped_slot = Dm.arg(__extra, 1);
        object action_name = Dm.arg(__extra, 2);
        object destroy_on_removal = Dm.arg(__extra, 3);
        object parent_icon_state_suffix = Dm.arg(__extra, 4);
        object down_overlay_state_suffix = Dm.arg(__extra, 5);
        object auto_deploy_on_outfit_equip = Dm.arg(__extra, 6);
        datum_callback pre_creation_check = Dm.arg(__extra, 7) as datum_callback;
        datum_callback on_created = Dm.arg(__extra, 8) as datum_callback;
        datum_callback on_deployed = Dm.arg(__extra, 9) as datum_callback;
        datum_callback on_removed = Dm.arg(__extra, 10) as datum_callback;
        action_name ??= "Toggle";
        destroy_on_removal ??= FALSE;
        parent_icon_state_suffix ??= "";
        down_overlay_state_suffix ??= "";
        auto_deploy_on_outfit_equip ??= TRUE;
        object dot = await base.Initialize(Dm.forward(__extra, deployable_type, equipped_slot, action_name, destroy_on_removal, parent_icon_state_suffix, down_overlay_state_suffix, auto_deploy_on_outfit_equip, pre_creation_check, on_created, on_deployed, on_removed));
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        if (deployable_type == null || !Dm.truthy(equipped_slot))
            return COMPONENT_REDUNDANT;
        this.deployable_type = deployable_type;
        this.equipped_slot = equipped_slot;
        this.destroy_on_removal = destroy_on_removal;
        this.parent_icon_state_suffix = (string)parent_icon_state_suffix;
        this.down_overlay_state_suffix = (string)down_overlay_state_suffix;
        this.auto_deploy_on_outfit_equip = Dm.num(auto_deploy_on_outfit_equip);
        this.pre_creation_check = pre_creation_check;
        this.on_created = on_created;
        this.on_deployed = on_deployed;
        this.on_removed = on_removed;

        obj_item clothing_parent = parent as obj_item;
        toggle_action = Dm.@new<datum_action_item_action>(parent);
        toggle_action.name = (string)action_name;
        await clothing_parent.add_item_action(toggle_action);

        RegisterSignal(parent, COMSIG_ITEM_UI_ACTION_CLICK, nameof(on_toggle_pressed));
        RegisterSignal(parent, COMSIG_ITEM_UI_ACTION_SLOT_CHECKED, nameof(on_action_slot_checked));
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_parent_equipped));
        RegisterSignal(parent, COMSIG_ITEM_POST_UNEQUIP, nameof(remove_deployable));
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED_AS_OUTFIT, nameof(on_parent_equipped_outfit));
        if (Dm.truthy(down_overlay_state_suffix))
        {
            object initial_state = Dm.initial(clothing_parent, (atom p) => p.post_init_icon_state);
            string overlay_state = Dm.text("[][]", Dm.truthy(initial_state) ? initial_state : Dm.initial(clothing_parent, (atom p) => p.icon_state), down_overlay_state_suffix);
            undeployed_overlay = mutable_appearance(Dm.initial(clothing_parent, (obj_item p) => p.worn_icon), overlay_state, -SUIT_LAYER) as mutable_appearance;
            RegisterSignal(parent, COMSIG_ITEM_GET_WORN_OVERLAYS, nameof(on_checked_overlays));
            clothing_parent.update_slot_icon();
        }

        if (!Dm.truthy(destroy_on_removal))
            create_deployable();
        return dot;
    }

    // tg: code/datums/components/toggle_attached_clothing.dm:84
    public override object Destroy(float? force = null)
    {
        unequip_deployable();
        QDEL_NULL(ref deployable);
        QDEL_NULL(ref toggle_action);
        undeployed_overlay = null;
        pre_creation_check = null;
        on_created = null;
        on_deployed = null;
        on_removed = null;
        return base.Destroy(force);
    }

    /// Toggle deployable when the UI button is clicked
    // tg: code/datums/components/toggle_attached_clothing.dm:95
    [SignalHandler]
    public virtual object on_toggle_pressed(obj_item source = null, mob user = null, datum_action action = null)
    {
        if (action != toggle_action)
            return null;
        toggle_deployable();
        return COMPONENT_ACTION_HANDLED;
    }

    /// Called when action attempts to check what slot the item is worn in
    // tg: code/datums/components/toggle_attached_clothing.dm:103
    [SignalHandler]
    public virtual object on_action_slot_checked(obj_item_clothing source = null, mob user = null, datum_action action = null, object slot = null)
    {
        if (action != toggle_action)
            return null;
        if (!Dm.truthy(Dm.bitand(source.slot_flags, slot)))
            return COMPONENT_ITEM_ACTION_SLOT_INVALID;
        return null;
    }

    /// Apply an overlay while the item is not deployed
    // tg: code/datums/components/toggle_attached_clothing.dm:111
    [SignalHandler]
    public virtual void on_checked_overlays(obj_item source = null, DmList overlays = null, mutable_appearance standing = null, object isinhands = null, object icon_file = null, object bodyshape = null)
    {
        if (Dm.truthy(isinhands) || Dm.truthy(currently_deployed))
            return;
        Dm.append(ref overlays, undeployed_overlay);
    }

    /// Deploys gear if it is hidden, hides it if it is deployed
    // tg: code/datums/components/toggle_attached_clothing.dm:118
    public virtual void toggle_deployable()
    {
        if (Dm.truthy(currently_deployed))
        {
            remove_deployable();
            return;
        }

        obj_item parent_gear = parent as obj_item;
        if (!ishuman(parent_gear.loc))
            return;
        mob_living_carbon_human wearer = parent_gear.loc as mob_living_carbon_human;
        if (Dm.truthy(wearer.is_holding(parent_gear)))
        {
            parent_gear.balloon_alert(wearer, I18n.T("wear it first!"));
            return;
        }
        if (Dm.truthy(wearer.get_item_by_slot(equipped_slot)))
        {
            parent_gear.balloon_alert(wearer, I18n.T("slot occupied!"));
            return;
        }
        if (deployable == null && !Dm.truthy(create_deployable()))
            return;
        if (!Dm.truthy(Waiting.NoSleep(wearer.equip_to_slot_if_possible(deployable, slot: equipped_slot))))
        {
            if (Dm.truthy(destroy_on_removal))
                remove_deployable();
            return;
        }
        currently_deployed = TRUE;
        if (on_deployed != null)
            Waiting.NoSleep(on_deployed.Invoke(deployable));
        if (!string.IsNullOrEmpty(parent_icon_state_suffix))
        {
            object initial_state = Dm.initial(parent_gear, (atom p) => p.post_init_icon_state);
            parent_gear.icon_state = Dm.text("[][]", Dm.truthy(initial_state) ? initial_state : Dm.initial(parent_gear, (atom p) => p.icon_state), parent_icon_state_suffix);
            parent_gear.worn_icon_state = parent_gear.icon_state;
        }
        parent_gear.update_slot_icon();
        Waiting.NoSleep(wearer.update_mob_action_buttons());
    }

    /// Undeploy gear if it moves slots somehow
    // tg: code/datums/components/toggle_attached_clothing.dm:148
    [SignalHandler]
    public virtual void on_parent_equipped(obj_item_clothing source = null, mob equipper = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, equipped_slot)))
            return;
        remove_deployable();
    }

    /// Display deployed if worn in an outfit
    // tg: code/datums/components/toggle_attached_clothing.dm:155
    [SignalHandler]
    public virtual void on_parent_equipped_outfit(obj_item_clothing source = null, mob equipper = null, object visuals_only = null, object slot = null)
    {
        if (!Dm.truthy(auto_deploy_on_outfit_equip))
            return;
        create_deployable();
        toggle_deployable();
    }

    /// Create our gear, returns true if we actually made anything
    // tg: code/datums/components/toggle_attached_clothing.dm:163
    public virtual object create_deployable()
    {
        if (deployable != null)
            return FALSE;
        if (pre_creation_check != null && !Dm.truthy(Waiting.NoSleep(pre_creation_check.Invoke())))
            return FALSE;
        deployable = Dm.@new(deployable_type, parent) as obj_item;
        if (!(deployable is obj_item))
            stack_trace("Tried to create non-clothing item from toggled clothing.", "code/datums/components/toggle_attached_clothing.dm", 168);
        RegisterSignal(deployable, COMSIG_ITEM_DROPPED, nameof(on_deployed_dropped));
        RegisterSignal(deployable, COMSIG_ITEM_EQUIPPED, nameof(on_deployed_equipped));
        RegisterSignal(deployable, COMSIG_QDELETING, nameof(on_deployed_destroyed));
        if (on_created != null)
            Waiting.NoSleep(on_created.Invoke(deployable));
        return TRUE;
    }

    /// Undeploy gear if you drop it
    // tg: code/datums/components/toggle_attached_clothing.dm:177
    [SignalHandler]
    public virtual void on_deployed_dropped()
    {
        remove_deployable();
    }

    /// Undeploy gear if it moves slots somehow
    // tg: code/datums/components/toggle_attached_clothing.dm:182
    [SignalHandler]
    public virtual void on_deployed_equipped(obj_item_clothing source = null, mob equipper = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(source.slot_flags, slot)))
            return;
        remove_deployable();
    }

    /// Undeploy gear if it is deleted
    // tg: code/datums/components/toggle_attached_clothing.dm:190
    [SignalHandler]
    public virtual void on_deployed_destroyed()
    {
        remove_deployable();
        deployable = null;
    }

    /// Removes our deployed equipment from the wearer
    // tg: code/datums/components/toggle_attached_clothing.dm:196
    [SignalHandler]
    public virtual void remove_deployable()
    {
        unequip_deployable();
        if (!Dm.truthy(currently_deployed))
            return;
        currently_deployed = FALSE;
        if (on_removed != null)
            Waiting.NoSleep(on_removed.Invoke(deployable));

        obj_item parent_gear = parent as obj_item;
        if (Dm.truthy(destroy_on_removal))
            QDEL_NULL(ref deployable);
        if (!string.IsNullOrEmpty(parent_icon_state_suffix))
        {
            object initial_state = Dm.initial(parent_gear, (atom p) => p.post_init_icon_state);
            parent_gear.icon_state = (string)(Dm.truthy(initial_state) ? initial_state : Dm.initial(parent_gear, (atom p) => p.icon_state));
            parent_gear.worn_icon_state = parent_gear.icon_state;
        }
        parent_gear.update_slot_icon();
        Waiting.NoSleep(parent_gear.update_item_action_buttons());
    }

    /// Removes an equipped deployable atom upon its retraction or destruction
    // tg: code/datums/components/toggle_attached_clothing.dm:216
    public virtual void unequip_deployable()
    {
        if (deployable == null)
            return;
        if (!ishuman(deployable.loc))
        {
            Waiting.NoSleep(deployable.forceMove(parent as atom));
            return;
        }
        mob_living_carbon_human wearer = deployable.loc as mob_living_carbon_human;
        Waiting.NoSleep(wearer.transferItemToLoc(deployable, parent as atom, force: TRUE, silent: TRUE));
    }
}

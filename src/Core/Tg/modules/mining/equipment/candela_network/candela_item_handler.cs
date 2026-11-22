// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/candela_network/candela_item_handler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole.

/// Datum which handles interactions of items with the Candela network
// tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:7
public partial class datum_candela_item_handler : datum
{
    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:25
    [DmParams("parent", "active_slots", "storage_active_slots", "on_network_cut_callback")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New();
        parent = Dm.arg<obj_item>(__extra, 0);
        active_slots = Dm.num(Dm.arg(__extra, 1) ?? NONE);
        storage_active_slots = Dm.num(Dm.arg(__extra, 2) ?? NONE);
        on_network_cut_callback = Dm.arg<datum_callback>(__extra, 3);

        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_parent_equipped));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_parent_dropped));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_parent_moved));
    }

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:36
    public override object Destroy(float? force = null)
    {
        set_network(null);
        set_owner(null);
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED, COMSIG_MOVABLE_MOVED));
        parent = null;
        on_network_cut_callback = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:44
    public virtual void on_parent_equipped(obj_item source = null, mob_living user = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, active_slots)))
            set_owner(user);
    }

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:49
    public virtual void on_parent_dropped(obj_item source = null, mob_living user = null) => set_owner(null);

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:53
    public virtual void on_parent_moved(obj_item source = null, atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, object momentum_change = null)
    {
        if (!Dm.truthy(storage_active_slots))
            return;

        if (isitem(old_loc))
        {
            if (old_loc.loc == owner)
                set_owner(null);
            UnregisterSignal(old_loc, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
        }

        atom parent_loc = parent.loc;
        if (!isitem(parent_loc))
            return;

        RegisterSignal(parent_loc, COMSIG_ITEM_EQUIPPED, nameof(on_storage_equipped));
        RegisterSignal(parent_loc, COMSIG_ITEM_DROPPED, nameof(on_storage_dropped));
        if (Dm.truthy(Dm.bitand((parent_loc.loc as mob_living)?.get_slot_by_item(parent_loc as obj_item), storage_active_slots)))
            set_owner(parent_loc.loc as mob_living);
    }

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:73
    public virtual void on_storage_equipped(obj_item source = null, mob equipper = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, storage_active_slots)))
            set_owner(equipper as mob_living);
    }

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:79
    public virtual void on_storage_dropped(obj_item source = null, mob user = null) => set_owner(null);

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:83
    public virtual void set_owner(mob_living new_owner = null)
    {
        if (owner == new_owner)
            return;

        if (owner != null)
            network?.unlink_mob(this, owner);

        owner = new_owner;
        if (network != null)
            Dm.combine(ref network.linked_beacon_items, this);
        if (owner == null)
        {
            set_closest_node(null);
            return;
        }

        if (network != null)
        {
            network.link_mob(this, owner);
            locate_closest_node(TRUE);
        }
    }

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:100
    public virtual void set_network(datum_mining_beacon_network new_network = null, datum_component_candela_node new_closest_node = null, float? merging = null)
    {
        merging ??= FALSE;
        if (network == new_network)
        {
            if (new_closest_node != null)
                set_closest_node(new_closest_node);
            return;
        }

        if (network != null)
        {
            if (owner != null)
                network.unlink_mob(this, owner);
            network.linked_beacon_items.Remove(this);
        }

        network = new_network;

        if (network == null)
        {
            set_closest_node(null);
            return;
        }

        if (owner != null)
            network.link_mob(this, owner);

        if (Dm.truthy(merging))
            return;

        Dm.combine(ref network.linked_beacon_items, this);
        if (new_closest_node != null)
            set_closest_node(new_closest_node);
        else
            locate_closest_node();
    }

    /// Locates the closest node within our network, or cuts the connection if none could be found
    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:130
    public virtual object locate_closest_node(float? silent = null)
    {
        silent ??= FALSE;
        if (network == null)
            return null;

        turf our_turf = get_turf(parent);
        float cur_dist = MINING_BEACON_MAX_REACH + 1;
        datum_component_candela_node cur_node = null;
        foreach (datum_component_candela_node node in Dm.each<datum_component_candela_node>(network.linked_nodes))
        {
            turf node_turf = get_turf(node.parent as atom);
            float node_dist = Dm.num(get_dist_euclidean(our_turf, node_turf));
            if (node_dist >= cur_dist)
                continue;

            if (!Dm.truthy(can_see(our_turf, node_turf, MINING_BEACON_MAX_REACH)) && !Dm.truthy(can_see(node_turf, our_turf, MINING_BEACON_MAX_REACH)))
                continue;

            cur_node = node;
            cur_dist = node_dist;
        }

        if (cur_node != null)
        {
            set_closest_node(cur_node);
            return cur_node;
        }

        set_network(null);
        if (owner != null && !Dm.truthy(silent))
            parent.balloon_alert(owner, I18n.T("connection lost!"));
        return null;
    }

    /// Locates the closest node within any network to ourselves, and links to it and its network
    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:158
    public virtual object locate_closest_network()
    {
        turf our_turf = get_turf(parent);
        float cur_dist = MINING_BEACON_MAX_REACH + 1;
        datum_component_candela_node cur_node = null;
        foreach (datum_mining_beacon_network each_network in Dm.each<datum_mining_beacon_network>(GLOB.mining_beacon_networks))
        {
            foreach (datum_component_candela_node node in Dm.each<datum_component_candela_node>(each_network.linked_nodes))
            {
                turf node_turf = get_turf(node.parent as atom);
                float node_dist = Dm.num(get_dist_euclidean(our_turf, node_turf));
                if (node_dist >= cur_dist)
                    continue;

                if (!Dm.truthy(can_see(our_turf, node_turf, MINING_BEACON_MAX_REACH)) && !Dm.truthy(can_see(node_turf, our_turf, MINING_BEACON_MAX_REACH)))
                    continue;

                cur_node = node;
                cur_dist = node_dist;
            }
        }

        if (cur_node != null)
        {
            set_network(cur_node.network, cur_node);
            return network;
        }

        if (network == null)
            return null;

        set_network(null);
        if (owner != null)
            parent.balloon_alert(owner, I18n.T("connection lost!"));
        return null;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:186
    public virtual void set_closest_node(datum_component_candela_node new_node = null)
    {
        if (new_node == closest_node)
            return;

        if (closest_node != null)
            UnregisterSignal(closest_node, COMSIG_QDELETING);

        qdel(beam_visual);
        beam_visual = null;
        closest_node = new_node;

        if (closest_node == null)
            return;

        RegisterSignal(closest_node, COMSIG_QDELETING, nameof(on_node_deleted));

        if (owner == null)
            return;

        // Don't render a beam if another handler (added to the network before us) is already rendering one to the same node as to avoid spam/overlaps
        DmList handlers = network.linked_mobs[owner] as DmList;
        foreach (datum_candela_item_handler other_handler in Dm.each<datum_candela_item_handler>(handlers))
        {
            if (other_handler == this || other_handler.closest_node != closest_node) // shouldn't have a different closest node but just in case
                continue;

            if (handlers.Find(other_handler) < handlers.Find(this))
                return;
        }

        redraw_beam();
    }

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:215
    public virtual void redraw_beam()
    {
        qdel(beam_visual);
        beam_visual = null;
        beam_visual = owner.Beam(
            closest_node.parent as atom,
            "1-full", Dm.resource("icons/effects/beam.dmi"),
            beam_color: LIGHT_COLOR_MINING,
            emissive: EMISSIVE_BLOOM,
            override_target_pixel_x: closest_node.connection_pixel_x,
            override_target_pixel_y: closest_node.connection_pixel_y,
            emissive_alpha: 192,
            alpha: 128,
            layer: BELOW_MOB_LAYER) as datum_beam;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_item_handler.dm:229
    public virtual void on_node_deleted(datum source = null)
    {
        closest_node = null;
        locate_closest_node();
    }
}

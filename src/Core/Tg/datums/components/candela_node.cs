// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/candela_node.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole, with GLOB.mining_beacon_networks; the MOD candela spool's branch (on_module_used) came with the
// MOD modules (gaps MO2).

/// Component that handles stationary node behavior for the Candela mining navigation network
// tg: code/datums/components/candela_node.dm:2
public partial class datum_component_candela_node : datum_component
{
    // tg: code/datums/components/candela_node.dm:16
    [DmParams("new_network", "deployer", "connection_pixel_x", "connection_pixel_y", "power_flags", "fulton_point")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        datum_mining_beacon_network new_network = Dm.arg<datum_mining_beacon_network>(__extra, 0);
        datum_candela_item_handler deployer = Dm.arg<datum_candela_item_handler>(__extra, 1);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        connection_pixel_x = Dm.arg(__extra, 2);
        connection_pixel_y = Dm.arg(__extra, 3);
        power_flags = Dm.num(Dm.arg(__extra, 4) ?? NONE);
        fulton_point = Dm.num(Dm.arg(__extra, 5) ?? FALSE);

        set_network(new_network);
        if (!QDELETED(deployer))
            deployer.set_network(network, this);
        return null;
    }

    // tg: code/datums/components/candela_node.dm:30
    public override object Destroy(float? force = null)
    {
        set_network(null, update: FALSE);
        return base.Destroy(force);
    }

    // tg: code/datums/components/candela_node.dm:34
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_interaction));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_MOD_MODULE_USED, nameof(on_module_used));
    }

    // tg: code/datums/components/candela_node.dm:40
    public virtual void on_examine(atom_movable source = null, mob viewer = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("[] a \"Candela\" mining navigation node, capable of syncronizing various prospecting machinery and equipment, and repelling hostile fauna.", source.p_Theyre())));
        if (Dm.truthy(Dm.bitand(power_flags, CANDELA_NETWORK_BOOSTED)))
            Dm.append(ref examine_list, span_notice(I18n.T("[] additionally act[] as an energy booster for the network, []increasing ore vent outputs.", source.p_They(), source.p_s(), Dm.truthy(Dm.bitand(power_flags, CANDELA_NETWORK_POWERED)) ? "keeping all connected beacons and equipment active and " : "")));
        else if (Dm.truthy(Dm.bitand(power_flags, CANDELA_NETWORK_POWERED)))
            Dm.append(ref examine_list, span_notice(I18n.T("[] additionally act[] as a power source for the network, keeping all connected beacons and equipment active.", source.p_They(), source.p_s())));
        else
            Dm.append(ref examine_list, span_notice(I18n.T("The network is currently [].", Dm.truthy(Dm.bitand(network.powered, CANDELA_NETWORK_POWERED)) ? Dm.text("fully operational[]", Dm.truthy(Dm.bitand(network.powered, CANDELA_NETWORK_BOOSTED)) ? " and overclocked" : "") : "missing a power source")));
    }

    // tg: code/datums/components/candela_node.dm:51
    public virtual object on_item_interaction(atom_movable source = null, mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!istype(tool, typeof(obj_item_stack_candela_beacon)))
            return NONE;

        obj_item_stack_candela_beacon beacon = tool as obj_item_stack_candela_beacon;
        beacon.handler.set_network(network, this);
        beacon.balloon_alert(user, I18n.T("network linked!"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/datums/components/candela_node.dm:62
    [SignalHandler]
    public virtual object on_module_used(atom_movable source = null, obj_item_mod_module module = null)
    {
        if (!istype(module, typeof(obj_item_mod_module_candela_spool)))
            return NONE;

        obj_item_mod_module_candela_spool spool = module as obj_item_mod_module_candela_spool;
        if (!Dm.truthy(Waiting.NoSleep(spool.mod.wearer.Adjacent(parent as atom))))
            return NONE;

        spool.handler.set_network(network, this);
        spool.mod.wearer.balloon_alert(spool.mod.wearer, I18n.T("network linked!"));
        return COMPONENT_INTERRUPT_MODULE_USE;
    }

    // tg: code/datums/components/candela_node.dm:76
    public virtual object set_network(datum_mining_beacon_network new_network = null, float? merging = null, float? update = null, float? separating = null)
    {
        merging ??= FALSE;
        update ??= TRUE;
        separating ??= FALSE;
        if (network == new_network)
            return null;

        object dot = network;
        if (network != null && !Dm.truthy(separating) && !Dm.truthy(merging))
            network.remove_node(this);
        network = new_network;
        // Before add_node, as network can change from merging
        if (Dm.truthy(parent._listen_lookup?[COMSIG_CANDELA_NODE_NETWORK_CHANGED]))
            parent._SendSignal(COMSIG_CANDELA_NODE_NETWORK_CHANGED, Dm.list(parent, dot, network));

        if (network != null)
            network.add_node(this, merging);

        if (Dm.truthy(update))
            update_connections();
        return dot;
    }

    /// Refresh visual connections of our node
    /// - keep_links: Forces a redraw of all beams rather than a full recalculation of all links
    // tg: code/datums/components/candela_node.dm:95
    public virtual void update_connections(float? keep_links = null)
    {
        keep_links ??= FALSE;
        if (network == null)
        {
            QDEL_LIST_ASSOC_VAL(beam_visuals);
            return;
        }

        DmList draw_to = null;
        float our_index = network.linked_nodes.Find(this);
        if (Dm.truthy(keep_links))
        {
            draw_to = assoc_to_keys(beam_visuals) as DmList;
            QDEL_LIST_ASSOC_VAL(beam_visuals);
        }
        else
        {
            DmList linked_to = network.linked_nodes[this] as DmList;
            draw_to = linked_to.Copy();
            turf our_turf = get_turf(parent as atom);
            foreach (datum_component_candela_node link_node in Dm.each<datum_component_candela_node>(linked_to))
            {
                turf link_turf = get_turf(link_node.parent as atom);
                float link_index = network.linked_nodes.Find(link_node);
                // Don't render connections to shared nodes if there's another one that's closer to them than us (in case of equal distance, use index)
                foreach (datum_component_candela_node shared_node in Dm.each<datum_component_candela_node>(Dm.bitand(draw_to, link_node.beam_visuals)))
                {
                    float our_dist = Dm.num(get_dist_euclidean(get_turf(shared_node.parent as atom), our_turf));
                    float link_dist = Dm.num(get_dist_euclidean(get_turf(shared_node.parent as atom), link_turf));
                    if (our_dist > link_dist || our_dist == link_dist && link_index < our_index)
                        draw_to.Remove(shared_node);
                }
            }
        }

        foreach (object lost_node in Dm.each(Dm.sub(beam_visuals, draw_to)))
        {
            object old_beam = beam_visuals[lost_node];
            beam_visuals.Remove(lost_node);
            qdel(old_beam);
        }

        atom_movable movable_parent = parent as atom_movable;
        foreach (datum_component_candela_node new_node in Dm.each<datum_component_candela_node>(Dm.sub(draw_to, beam_visuals)))
        {
            if (network.linked_nodes.Find(new_node) < our_index) // Older nodes draw connections to younger ones
                continue;
            beam_visuals[new_node] = movable_parent.Beam(
                new_node.parent as atom,
                "1-full",
                Dm.resource("icons/effects/beam.dmi"),
                beam_color: LIGHT_COLOR_MINING,
                emissive: EMISSIVE_BLOOM,
                override_origin_pixel_x: connection_pixel_x,
                override_origin_pixel_y: connection_pixel_y,
                override_target_pixel_x: new_node.connection_pixel_x,
                override_target_pixel_y: new_node.connection_pixel_y,
                emissive_alpha: Dm.truthy(Dm.bitand(network.powered, CANDELA_NETWORK_BOOSTED)) ? 255 : 192,
                alpha: Dm.truthy(Dm.bitand(network.powered, CANDELA_NETWORK_POWERED)) ? 192 : 128);
        }
    }

    // Costly, but should not be called often (if at all) as all nodes should be anchored
    // tg: code/datums/components/candela_node.dm:146
    public virtual void on_moved(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        DmList located_nodes = Dm.list();
        DmList atoms_to_nodes = Dm.list();
        atom parent_atom = parent as atom;

        foreach (datum_mining_beacon_network each_network in Dm.each<datum_mining_beacon_network>(GLOB.mining_beacon_networks))
        {
            foreach (datum_component_candela_node node in Dm.each<datum_component_candela_node>(each_network.linked_nodes))
                atoms_to_nodes[node.parent] = node;
        }

        // We use can_see rather than view() because view() is not a raycast and can end up putting beams through walls, as it sees around corners in a weird fasion
        DmList our_links = network.linked_nodes[this] as DmList;
        foreach (datum_component_candela_node other_node in Dm.each<datum_component_candela_node>(our_links))
        {
            if (Dm.truthy(can_see(parent_atom, other_node.parent as atom, MINING_BEACON_MAX_REACH)) || Dm.truthy(can_see(other_node.parent as atom, parent_atom, MINING_BEACON_MAX_REACH)))
                Dm.combine(ref located_nodes, other_node);
        }

        if (Dm.length(located_nodes) > 0)
        {
            // No nodes were cut, don't do anything
            if (Dm.length(our_links) == Dm.length(located_nodes))
                return;
            // Otherwise, cut ourselves from the network and try to link back to refresh connections or find another one
            datum_mining_beacon_network old_network = network;
            set_network(null);
            set_network(old_network);
            return;
        }

        // Otherwise, go through all (nearby, can_see checks get_dist) nodes looking for matches
        float min_dist = MINING_BEACON_MAX_REACH + 1;
        datum_component_candela_node secondary_node = null;
        foreach (atom_movable thing in Dm.each<atom_movable>(atoms_to_nodes))
        {
            float node_dist = Dm.num(get_dist_euclidean(thing, parent_atom));
            if (node_dist >= min_dist)
                continue;

            if (!Dm.truthy(can_see(parent_atom, thing, MINING_BEACON_MAX_REACH)) && !Dm.truthy(can_see(thing, parent_atom, MINING_BEACON_MAX_REACH)))
                continue;

            min_dist = node_dist;
            secondary_node = atoms_to_nodes[thing] as datum_component_candela_node;
        }

        set_network(null);
        if (secondary_node != null)
            set_network(secondary_node.network);
    }
}

/// Candela beacon network datum
// tg: code/datums/components/candela_node.dm:196
public partial class datum_mining_beacon_network : datum
{
    // tg: code/datums/components/candela_node.dm:204
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.append(ref GLOB.mining_beacon_networks, this);
    }

    // tg: code/datums/components/candela_node.dm:208
    public override object Destroy(float? force = null)
    {
        linked_nodes.Cut();
        foreach (datum_candela_item_handler handler in Dm.each<datum_candela_item_handler>(linked_beacon_items.Copy()))
            handler.locate_closest_network();
        linked_beacon_items.Cut();
        linked_mobs.Cut();
        GLOB.mining_beacon_networks.Remove(this);
        return base.Destroy(force);
    }

    /// Refresh our power state, returns previous power state
    // tg: code/datums/components/candela_node.dm:218
    public virtual object update_power()
    {
        object dot = powered;
        float new_state = NONE;
        foreach (datum_component_candela_node power_node in Dm.each<datum_component_candela_node>(linked_nodes))
            new_state = Dm.bitor(new_state, power_node.power_flags);

        set_powered_state(new_state);
        return dot;
    }

    // tg: code/datums/components/candela_node.dm:226
    public virtual void add_node(datum_component_candela_node new_node = null, float? merging = null)
    {
        merging ??= FALSE;
        if (Dm.truthy(linked_nodes[new_node]))
            return;

        linked_nodes[new_node] = Dm.list();
        if (!Dm.truthy(Dm.bitand(new_node.power_flags, powered)))
            set_powered_state(Dm.bitor(powered, new_node.power_flags));

        if (Dm.truthy(merging))
            return;

        DmList all_nodes = Dm.list();
        DmList atoms_to_nodes = Dm.list();
        foreach (datum_mining_beacon_network network in Dm.each<datum_mining_beacon_network>(GLOB.mining_beacon_networks))
        {
            foreach (datum_component_candela_node node in Dm.each<datum_component_candela_node>(network.linked_nodes))
            {
                all_nodes[node] = network;
                atoms_to_nodes[node.parent] = node;
            }
        }

        DmList merged_networks = Dm.list();
        DmList need_updates = Dm.list(new_node);
        atom new_parent = new_node.parent as atom;
        foreach (atom_movable thing in Dm.each<atom_movable>(view(MINING_BEACON_MAX_REACH, new_parent)))
        {
            datum_component_candela_node actual_node = atoms_to_nodes[thing] as datum_component_candela_node;
            if (actual_node == null || actual_node == new_node)
                continue;

            // Need a can_see check to avoid beams going through walls
            if (!Dm.truthy(can_see(thing, new_parent, MINING_BEACON_MAX_REACH)) && !Dm.truthy(can_see(new_parent, thing, MINING_BEACON_MAX_REACH)))
                continue;

            if (Dm.truthy(linked_nodes[actual_node]))
            {
                Dm.combine_at(linked_nodes, new_node, actual_node);
                Dm.combine_at(linked_nodes, actual_node, new_node);
                Dm.combine(ref need_updates, actual_node);
                continue;
            }

            if (!Dm.truthy(all_nodes[actual_node]))
                continue;

            datum_mining_beacon_network network = all_nodes[actual_node] as datum_mining_beacon_network;
            Dm.combine_at(linked_nodes, new_node, actual_node);
            Dm.combine_at(network.linked_nodes, actual_node, new_node);
            Dm.combine(ref need_updates, actual_node);

            Dm.combine(ref merged_networks, network);
        }

        if (Dm.length(merged_networks) == 0)
        {
            foreach (datum_component_candela_node update_node in Dm.each<datum_component_candela_node>(need_updates))
                update_node.update_connections();
            return;
        }

        // Merge ourselves into the closest network, then merge all other located networks into it as well
        // Significantly reduces churn when placing new beacons
        datum_mining_beacon_network first_net = merged_networks[1] as datum_mining_beacon_network;
        first_net.merge_network(this);

        foreach (datum_mining_beacon_network network in Dm.each<datum_mining_beacon_network>(Dm.sub(merged_networks, first_net)))
            first_net.merge_network(network);

        foreach (datum_component_candela_node update_node in Dm.each<datum_component_candela_node>(need_updates))
            update_node.update_connections();
    }

    // tg: code/datums/components/candela_node.dm:287
    public virtual void merge_network(datum_mining_beacon_network to_merge = null)
    {
        foreach (datum_component_candela_node node in Dm.each<datum_component_candela_node>(to_merge.linked_nodes))
            node.set_network(this, TRUE, FALSE);

        foreach (datum_candela_item_handler beacon in Dm.each<datum_candela_item_handler>(to_merge.linked_beacon_items))
            beacon.set_network(this, merging: TRUE);

        foreach (object node in Dm.each(to_merge.linked_nodes))
        {
            object connections = to_merge.linked_nodes[node];
            if (Dm.truthy(linked_nodes[node]))
                Dm.combine_at(linked_nodes, node, connections);
            else
                linked_nodes[node] = connections;
        }

        Dm.combine(ref linked_beacon_items, to_merge.linked_beacon_items);

        foreach (datum_component_candela_node node in Dm.each<datum_component_candela_node>(to_merge.linked_nodes))
            node.update_connections();

        qdel(to_merge);
    }

    // tg: code/datums/components/candela_node.dm:307
    public virtual void remove_node(datum_component_candela_node node = null)
    {
        if (!Dm.truthy(linked_nodes[node]))
            return;

        if (Dm.length(linked_nodes) == 1)
        {
            qdel(this);
            return;
        }

        DmList connections = linked_nodes[node] as DmList;
        if (Dm.length(connections) == 1) // End node, no need to run separation calculations
        {
            datum_component_candela_node other_node = connections[1] as datum_component_candela_node;
            (linked_nodes[other_node] as DmList).Remove(node);
            linked_nodes.Remove(node);
            other_node.update_connections();
            // If this node was potentially powering us, check if we lost any power sources
            if (Dm.truthy(Dm.bitand(node.power_flags, powered)))
                update_power();
            return;
        }

        linked_nodes.Remove(node);
        foreach (datum_component_candela_node other_node in Dm.each<datum_component_candela_node>(connections))
        {
            // Network cut?
            if (Dm.truthy(linked_nodes[other_node]))
                (linked_nodes[other_node] as DmList).Remove(node);
        }

        // If the network wasn't separated, just update connected nodes
        // Otherwise, check_network_separation will handle updates for us
        if (!Dm.truthy(check_network_separation(connections)))
        {
            foreach (datum_component_candela_node other_node in Dm.each<datum_component_candela_node>(connections))
                other_node.update_connections();
        }

        // We might've lost more than one node from this
        if (Dm.truthy(powered))
            update_power();
    }

    // tg: code/datums/components/candela_node.dm:342
    public virtual object set_powered_state(object new_power_state = null)
    {
        if (Dm.eq(powered, new_power_state))
            return null;
        object dot = powered;
        powered = Dm.num(new_power_state);
        foreach (datum_component_candela_node node in Dm.each<datum_component_candela_node>(linked_nodes))
            node.update_connections(TRUE);
        if (Dm.truthy(_listen_lookup?[COMSIG_CANDELA_NETWORK_POWER_CHANGED]))
            _SendSignal(COMSIG_CANDELA_NETWORK_POWER_CHANGED, Dm.list(this, dot, powered));
        return dot;
    }

    /// Try to reassemble the network in case of possible separation
    /// Node that caused separation should already be removed from linked_nodes
    /// Returns FALSE if the network was not split, TRUE if it was
    /// - connections - List of all connections of the node that caused the separation.
    // tg: code/datums/components/candela_node.dm:355
    public virtual object check_network_separation(DmList connections = null)
    {
        DmList new_networks = Dm.list();
        DmList polled_nodes = Dm.list();
        foreach (datum_component_candela_node primary_node in Dm.each<datum_component_candela_node>(connections))
        {
            // We've reached this node from another primary one, ignore
            if (Dm.@in(primary_node, polled_nodes))
                continue;

            DmList node_pool = Dm.list(primary_node);
            int index = 1;
            // Breadth-first parse all nodes connected from our primary node to see which ones it can connect to
            while (index <= Dm.length(node_pool))
            {
                object link_node = node_pool[index];
                Dm.combine(ref node_pool, linked_nodes[link_node]);
                index += 1;
            }

            DmList new_net = Dm.list();
            foreach (object new_net_node in Dm.each(node_pool))
                new_net[new_net_node] = linked_nodes[new_net_node];

            Dm.append(ref new_networks, Dm.list(new_net));
            Dm.combine(ref polled_nodes, node_pool);
        }

        // No separation occured, ignore
        if (Dm.length(new_networks) <= 1)
            return FALSE;

        linked_nodes = new_networks[1] as DmList;
        DmList all_nodes = linked_nodes.Copy();
        for (int i = 2; i <= Dm.length(new_networks); i++)
        {
            DmList other_net = new_networks[i] as DmList;
            datum_mining_beacon_network new_net = Dm.@new<datum_mining_beacon_network>();
            new_net.linked_nodes = other_net;
            Dm.combine(ref all_nodes, other_net);
            foreach (datum_component_candela_node other_node in Dm.each<datum_component_candela_node>(other_net))
            {
                // Doesn't actually run add_node code as we already "added" the nodes above, so we don't do pointless merging checks
                other_node.set_network(new_net, update: FALSE, separating: TRUE);
            }
            new_net.update_power();
        }

        update_power();

        // Force a full connection recalculation on all nodes in the network
        foreach (datum_component_candela_node node in Dm.each<datum_component_candela_node>(all_nodes))
            node.update_connections();

        foreach (datum_candela_item_handler beacon in Dm.each<datum_candela_item_handler>(linked_beacon_items.Copy()))
            beacon.locate_closest_network();
        return TRUE;
    }

    // tg: code/datums/components/candela_node.dm:404
    public virtual void link_mob(datum_candela_item_handler handler = null, mob_living new_user = null)
    {
        if (!Dm.truthy(linked_mobs[new_user]))
        {
            linked_mobs[new_user] = Dm.list();
            RegisterSignal(new_user, COMSIG_MOB_CLIENT_MOVED, nameof(on_user_moved));
        }
        Dm.combine_at(linked_mobs, new_user, handler);
    }

    // tg: code/datums/components/candela_node.dm:410
    public virtual void unlink_mob(datum_candela_item_handler handler = null, mob_living user = null)
    {
        (linked_mobs[user] as DmList)?.Remove(handler);
        if (Dm.length(linked_mobs[user]) > 0)
            return;
        linked_mobs.Remove(user);
        UnregisterSignal(user, COMSIG_MOB_CLIENT_MOVED);
    }

    /// A mob linked to the network moved, check if they still can connect to us and if not, check if any handlers object and then cut the connection
    // tg: code/datums/components/candela_node.dm:418
    public virtual void on_user_moved(mob_living source = null, object direction = null, object old_dir = null, atom old_loc = null)
    {
        turf new_loc = source.loc as turf;
        DmList all_handlers = linked_mobs[source] as DmList;
        // Check if this was a valid step with no teleportation involved
        if (!isturf(new_loc) || !isturf(old_loc) || get_step(old_loc, Dm.num(direction)) != new_loc)
        {
            foreach (datum_candela_item_handler handler in Dm.each<datum_candela_item_handler>(all_handlers.Copy()))
                handler.set_network(null);
            return;
        }

        datum_component_candela_node current_closest = null;
        datum_candela_item_handler master = all_handlers[1] as datum_candela_item_handler;
        datum_component_candela_node closest_node = master.closest_node;
        if (closest_node != null && (Dm.truthy(can_see(source, closest_node.parent as atom, MINING_BEACON_MAX_REACH)) || Dm.truthy(can_see(closest_node.parent as atom, source, MINING_BEACON_MAX_REACH))))
            current_closest = closest_node;

        foreach (datum_component_candela_node network_node in Dm.each<datum_component_candela_node>(linked_nodes))
        {
            if (network_node == closest_node || Dm.num(get_dist_euclidean(get_turf(network_node.parent as atom), new_loc)) > MINING_BEACON_MAX_REACH)
                continue;

            if (current_closest != null && Dm.num(get_dist_euclidean(new_loc, get_turf(network_node.parent as atom))) >= Dm.num(get_dist_euclidean(new_loc, get_turf(current_closest.parent as atom))))
                continue;

            // Need a can_see rather than viewers() to avoid beams going through walls
            if (Dm.truthy(can_see(source, network_node.parent as atom, MINING_BEACON_MAX_REACH)) || Dm.truthy(can_see(network_node.parent as atom, source, MINING_BEACON_MAX_REACH)))
                current_closest = network_node;
        }

        if (current_closest != null)
        {
            if (current_closest == closest_node)
                return;
            foreach (datum_candela_item_handler handler in Dm.each<datum_candela_item_handler>(all_handlers))
                handler.set_closest_node(current_closest);
            return;
        }

        bool interrupt = false;
        foreach (datum_candela_item_handler handler in Dm.each<datum_candela_item_handler>(all_handlers))
        {
            if (handler.on_network_cut_callback != null && Dm.truthy(Waiting.NoSleep(handler.on_network_cut_callback.Invoke(old_loc, old_dir, interrupt ? TRUE : FALSE))))
                interrupt = true;
        }

        if (interrupt)
            return;

        foreach (datum_candela_item_handler handler in Dm.each<datum_candela_item_handler>(all_handlers.Copy()))
            handler.set_network(null);
        source.balloon_alert(source, I18n.T("connection lost!"));
    }
}

// tg: code/datums/components/candela_node.dm:191  GLOBAL_LIST_EMPTY(mining_beacon_networks)
public partial class datum_controller_global_vars
{
    public DmList mining_beacon_networks;

    public virtual void InitGlobalmining_beacon_networks()
    {
        mining_beacon_networks = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mining_beacon_networks");
    }
}

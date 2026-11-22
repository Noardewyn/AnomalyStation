// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/candela_network/candela_beacons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole. The light power and the beams follow the network; the emissive overlays stay under DV12.

// tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:2
public partial class obj_item_stack_candela_beacon : obj_item_stack
{
    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        handler = Dm.@new<datum_candela_item_handler>(
            this,
            Dm.named("active_slots", ITEM_SLOT_HANDS | ITEM_SLOT_POCKETS),
            Dm.named("storage_active_slots", ITEM_SLOT_POCKETS | ITEM_SLOT_BELT),
            Dm.named("on_network_cut_callback", Dm.@new<datum_callback>(this, nameof(on_network_cut))));
        update_appearance();
        return dot;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:25
    public override object Destroy(float? force = null)
    {
        qdel(handler);
        handler = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:29
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await place_beacon(user);
        return null;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:32
    public override async DmTask<object> interact(mob user = null, params object[] __extra) => null;

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:35
    public override async DmTask<object> merge_without_del(obj_item_stack target_stack = null, object limit = null)
    {
        object dot = await base.merge_without_del(target_stack, limit);
        if (Dm.truthy(dot))
            (target_stack as obj_item_stack_candela_beacon).handler.set_network(handler.network, handler.closest_node);
        return dot;
    }

    /// Deploy a beacon at user's feet, or at a given location if passed
    /// Can deploy a beacon for another handler, using its network instead
    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:42
    public virtual async DmTask<object> place_beacon(mob user = null, atom loc_override = null, datum_candela_item_handler handler_override = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!isturf(user.loc))
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("You need more space to place a [] here.", singular_name)));
            return FALSE;
        }

        if (Dm.locate<obj_structure_candela_beacon>(loc_override ?? user.loc) != null)
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("There is already a [] here.", singular_name)));
            return FALSE;
        }

        datum_candela_item_handler used_handler = handler_override ?? handler;
        // In case use() deletes ourselves
        datum_mining_beacon_network our_network = used_handler.network;
        if (!Dm.truthy(await use(1)))
            return FALSE;

        if (!Dm.truthy(silent))
            to_chat(user, span_notice(I18n.T("You activate and anchor [] [] in place.", Dm.truthy(amount) ? "a" : "the", singular_name)));

        playsound(user, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        obj_structure_candela_beacon beacon = Dm.@new<obj_structure_candela_beacon>(loc_override ?? user.loc, our_network, used_handler);
        transfer_fingerprints_to(beacon);
        return TRUE;
    }

    /// Callback to react to breaking LOS/reaching maximum distance with a beacon
    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:68
    public virtual async DmTask<object> on_network_cut(atom old_loc = null, object old_dir = null, object interrupt = null)
    {
        // If we did not change current_closest, there is a chance we cannot see any nodes near us, in which case we want to place a beacon on the previous turf where we *did* see one
        if (handler.closest_node != null && !Dm.truthy(interrupt))
            return await place_beacon(handler.owner, old_loc, silent: TRUE);
        return FALSE;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:74
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        handler.set_network(null);
        balloon_alert(user, I18n.T("network connection severed"));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:82
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        Dm.Deferred("code/modules/mining/equipment/candela_network/candela_beacons.dm", "update_overlays: emissive_appearance of the beacon (DEVIATION DV12)");
        return dot;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:86
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, I18n.T("Use in-hand to plant a beacon, [] in-hand to sever network link.", EXAMINE_HINT("right-click")));
        Dm.append(ref dot, I18n.T("Click on a placed beacon to form a network connection, or [] to pick it up.", EXAMINE_HINT("right-click")));
        Dm.append(ref dot, I18n.T("Automatically deployed upon reaching maximum distance or breaking line-of-sight to the network when placed in pocket or belt slots, or in storage in pocket or belt slots."));
        return dot;
    }
}

// Deployed beacon structure
// tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:101
public partial class obj_structure_candela_beacon : obj_structure
{
    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:123
    [DmParams("mapload", "new_network", "deployer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_mining_beacon_network new_network = Dm.arg<datum_mining_beacon_network>(__extra, 0);
        datum_candela_item_handler deployer = Dm.arg<datum_candela_item_handler>(__extra, 1);
        object dot = base.Initialize(mapload);
        if (new_network == null)
            new_network = Dm.@new<datum_mining_beacon_network>();
        RegisterSignal(this, COMSIG_CANDELA_NODE_NETWORK_CHANGED, nameof(on_network_changed));
        Waiting.NoSleep(AddComponent(typeof(datum_component_candela_node), new_network, deployer, Dm.named("connection_pixel_x", base_pixel_w), Dm.named("connection_pixel_y", Dm.num(base_pixel_z) + 3)));
        update_appearance();
        return dot;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:131
    public virtual void on_network_changed(datum source = null, datum_mining_beacon_network old_network = null, datum_mining_beacon_network new_network = null)
    {
        if (old_network != null)
            UnregisterSignal(old_network, COMSIG_CANDELA_NETWORK_POWER_CHANGED);
        on_power_changed(old_state: old_network?.powered, new_state: new_network?.powered);
        if (new_network != null)
            RegisterSignal(new_network, COMSIG_CANDELA_NETWORK_POWER_CHANGED, nameof(on_power_changed));
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:140
    public virtual void on_power_changed(datum source = null, object old_state = null, object new_state = null)
    {
        bool powered = Dm.truthy(Dm.bitand(new_state, CANDELA_NETWORK_POWERED));
        set_light_power(powered ? 1.7f : 1.3f);
        set_light_range(powered ? 2 : MINIMUM_USEFUL_LIGHT_RANGE);
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:147
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        Dm.Deferred("code/modules/mining/equipment/candela_network/candela_beacons.dm", "update_overlays: emissive_appearance of the beacon (DEVIATION DV12)");
        return dot;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:151
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        balloon_alert(user, I18n.T("picking up the beacon..."));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            return dot;

        obj_item_stack_candela_beacon beacon = Dm.@new<obj_item_stack_candela_beacon>(loc);
        transfer_fingerprints_to(beacon);
        if (Dm.truthy(await user.put_in_hands(beacon, del_on_fail: TRUE)))
        {
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            qdel(this);
        }
        return dot;
    }

    // tg: code/modules/mining/equipment/candela_network/candela_beacons.dm:165
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!istype(tool, typeof(obj_item_stack_candela_beacon)))
            return NONE;

        obj_item_stack_candela_beacon collection = tool as obj_item_stack_candela_beacon;
        if (collection.amount >= collection.max_amount)
        {
            balloon_alert(user, I18n.T("stack full!"));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You start picking [] up...", this)));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)) || collection.amount >= collection.max_amount)
            return ITEM_INTERACT_BLOCKING;

        await collection.add(1);
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

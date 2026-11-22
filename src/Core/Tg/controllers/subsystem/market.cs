// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/market.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, SSmarket: the markets and their items, the queued purchases and their shipping (the LTSRBT pad,
// the teleport, the launch from space, the supply pod), the restock. The var block is generated (D111).

// tg: code/controllers/subsystem/market.dm:1  SUBSYSTEM_DEF(market)
public partial class datum_controller_subsystem_market : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(market) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSmarket) expanded inline.
    // tg: code/controllers/subsystem/market.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSmarket != this)
        {
            if (SSmarket is datum_controller_subsystem_market)
            {
                Recover();
                qdel(SSmarket);
            }
            SSmarket = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/market.dm:25
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        foreach (Type market in Dm.each_cast<Type>(subtypesof(typeof(datum_market))))
            markets[market] = Dm.add(markets[market], Dm.@new(market));

        foreach (Type path in Dm.each_cast<Type>(subtypesof(typeof(datum_market_item))))
            initialize_item(path);

        return SS_INIT_SUCCESS;
    }

    /**
     * Follows the standard process for adding an item to the market from a path.
     * @param path The path of the market datum to initialize and add to the market.
     * @param market_whitelist A list of markets to which the item should be added. If null, the item is added to all markets.
     */
    // tg: code/controllers/subsystem/market.dm:39
    public virtual void initialize_item(Type path = null, DmList market_whitelist = null)
    {
        if (Dm.initial(path, (datum_market_item p) => p.item) == null || !prob(Dm.num(Dm.initial(path, (datum_market_item p) => p.availability_prob))))
            return;
        var item_instance = Dm.@new(path) as datum_market_item;
        foreach (object potential_market in Dm.each(item_instance.markets))
        {
            if (!Dm.truthy(markets[potential_market]))
            {
                stack_trace(Dm.text("SSmarket: Item [] available in market that does not exist.", item_instance), "code/controllers/subsystem/market.dm", 45);
                continue;
            }
            if (market_whitelist == null || Dm.@in(potential_market, market_whitelist))
                ((datum_market)markets[potential_market]).add_item(item_instance);
        }
    }

    /**
     * Adds an admin polled item to the market, expecting a market_item object having been created.
     */
    // tg: code/controllers/subsystem/market.dm:53
    public virtual void initialize_admin_item(datum_market_item item_instance = null, DmList market_whitelist = null)
    {
        if (item_instance == null)
            return;
        foreach (object potential_market in Dm.each(item_instance.markets))
        {
            if (!Dm.truthy(markets[potential_market]))
            {
                stack_trace(Dm.text("SSmarket: Item [] available in market that does not exist.", item_instance), "code/controllers/subsystem/market.dm", 58);
                continue;
            }
            if (market_whitelist == null || Dm.@in(potential_market, market_whitelist))
                ((datum_market)markets[potential_market]).add_item(item_instance);
        }
    }

    // tg: code/controllers/subsystem/market.dm:63
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        while (Dm.truthy(length(queued_purchases)))
        {
            var purchase = (datum_market_purchase)queued_purchases[1];
            queued_purchases.Cut(1, 2);

            var buyer = recursive_loc_check(purchase.uplink.loc as atom_movable, typeof(mob)) as mob;

            switch (Dm.str(purchase.method))
            {
                // Find a ltsrbt pad and make it handle the shipping.
                case SHIPPING_METHOD_LTSRBT:
                {
                    if (!Dm.truthy(length(telepads)))
                        continue;
                    // Prioritize pads that don't have a cooldown active.
                    obj_machinery_ltsrbt lowest_cd_pad = null;
                    // The time left of the shortest cooldown amongst all telepads.
                    float lowest_timeleft = INFINITY;
                    foreach (obj_machinery_ltsrbt pad in Dm.each_cast<obj_machinery_ltsrbt>(telepads))
                    {
                        bool no_power = Dm.truthy(Dm.bitand(pad.machine_stat, NOPOWER));
                        if (!(world.time >= pad.recharge_cooldown) || no_power) // COOLDOWN_FINISHED
                        {
                            float timeleft = no_power ? INFINITY - 1 : pad.recharge_cooldown - world.time; // COOLDOWN_TIMELEFT
                            if (timeleft <= lowest_timeleft)
                            {
                                lowest_cd_pad = pad;
                                lowest_timeleft = timeleft;
                            }
                            continue;
                        }
                        lowest_cd_pad = pad;
                        break;
                    }

                    lowest_cd_pad.add_to_queue(purchase);

                    to_chat(buyer, span_notice(I18n.T("[] flashes a message noting that the order is being processed by [].", purchase.uplink, lowest_cd_pad)));
                    break;
                }

                // Get random area, throw it somewhere there.
                case SHIPPING_METHOD_TELEPORT:
                {
                    turf targetturf = get_safe_random_station_turf_equal_weight(); //todo: split weights
                    // This shouldn't happen.
                    if (targetturf == null)
                        continue;
                    Dm.remove(ref queued_purchases, purchase);

                    to_chat(buyer, span_notice(I18n.T("[] flashes a message noting that the order is being teleported to [] in 60 seconds.", purchase.uplink, get_area(targetturf))));

                    // do_teleport does not want to teleport items from nullspace, so it just forceMoves and does sparks.
                    _addtimer(Dm.@new<datum_callback>(this, nameof(fake_teleport), purchase, targetturf), 60 * SECONDS, file: "code/controllers/subsystem/market.dm", line: 103);
                    break;
                }

                // Get the current location of the uplink if it exists, then throws the item from space at the station from a random direction.
                case SHIPPING_METHOD_LAUNCH:
                {
                    object startSide = pick(GLOB.cardinals);
                    turf T = get_turf(purchase.uplink);
                    turf pickedloc = spaceDebrisStartLoc(startSide, T.z);
                    var delivery_pod = Dm.@new<obj_structure_closet_crate_market>(pickedloc);

                    var item = await purchase.entry.spawn_item(delivery_pod, purchase) as atom;
                    purchase.post_purchase_effects(item);
                    Waiting.Detach(delivery_pod.throw_at(purchase.uplink, 3, 3, spin: FALSE));

                    to_chat(buyer, span_notice(I18n.T("[] flashes a message noting the order is being launched at the station from [].", purchase.uplink, dir2text(startSide))));
                    qdel(purchase);
                    break;
                }

                case SHIPPING_METHOD_SUPPLYPOD:
                {
                    var spawned_pod = await podspawn(Dm.list(
                        Dm.kv("target", get_turf(purchase.uplink)),
                        Dm.kv("path", typeof(obj_structure_closet_supplypod_back_to_station)))) as obj_structure_closet_supplypod;
                    await purchase.entry.spawn_item(spawned_pod, purchase);

                    to_chat(buyer, span_notice(I18n.T("[] flashes a message noting the order is being launched at your location. Right here, right now!", purchase.uplink)));
                    qdel(purchase);
                    break;
                }
            }

            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK
                break;
        }
    }

    /// Used to make a teleportation effect as do_teleport does not like moving items from nullspace.
    // tg: code/controllers/subsystem/market.dm:134
    public virtual async DmTask fake_teleport(datum_market_purchase purchase = null, turf target = null)
    {
        // Oopsie, whoopsie, the item is gone. So long, and thanks for all the money.
        if (QDELETED(purchase))
            return;
        var thing = await purchase.entry.spawn_item(target, purchase) as atom;
        purchase.post_purchase_effects(thing);
        do_sparks(5, TRUE, target);
        qdel(purchase);
    }

    /// Used to add /datum/market_purchase to queued_purchases var. Returns TRUE when queued.
    // tg: code/controllers/subsystem/market.dm:144
    public virtual object queue_item(datum_market_purchase purchase = null)
    {
        if ((Dm.eq(purchase.method, SHIPPING_METHOD_LTSRBT) && !Dm.truthy(telepads.len)) || purchase.uplink == null)
        {
            qdel(purchase);
            return FALSE;
        }
        Dm.append(ref queued_purchases, purchase);
        return TRUE;
    }

    ///A proc that restocks one or more markets, or all if the market_whitelist is null.
    // tg: code/controllers/subsystem/market.dm:152
    public virtual async DmTask restock(object market_whitelist = null)
    {
        object market_name = I18n.T("Markets");
        if (market_whitelist != null && market_whitelist is not DmList)
        {
            var market_path = (Type)market_whitelist;
            market_name = Dm.initial(market_path, (datum_market p) => p.name);
            market_whitelist = Dm.list(market_path);
        }

        DmList existing_types = Dm.list();
        foreach (object path in Dm.each(markets))
        {
            if (market_whitelist == null || Dm.@in(path, market_whitelist as DmList))
                ((datum_market)markets[path]).restock(existing_types);
        }

        foreach (Type path in Dm.each_cast<Type>(Dm.sub(subtypesof(typeof(datum_market_item)), existing_types) as DmList))
        {
            if (!Dm.truthy(Dm.initial(path, (datum_market_item p) => p.restockable)))
                continue;
            initialize_item(path, market_whitelist as DmList);
        }

        foreach (obj_machinery_ltsrbt pad in Dm.each_cast<obj_machinery_ltsrbt>(telepads))
        {
            await pad.say(I18n.T("[] restocked!", market_name));
            // playsound(src, 'sound/effects/cashregister.ogg', 40, FALSE): src is the subsystem, no atom to play at (tg plays nothing)
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/market.dm:1  SUBSYSTEM_DEF(market): GLOBAL_REAL(SSmarket, ...)
    internal datum_controller_subsystem_market SSmarket;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_market SSmarket => ref TgState.Current.SSmarket;
}

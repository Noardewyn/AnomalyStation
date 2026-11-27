// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/sources/subtypes/structures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the autowiki's tables (live-partial rows): the fish sources of structures and machines. The moisture
// traps, the toilets and the deep fryers have their tables; the holodeck's water only fishes inside the holodeck; a hydroponics
// tray fishes seeds, grass and critters while it has water and no plant; an aquarium gives its own live fish; a vending machine
// trades its products for the bait's money. The var blocks are generated (D111).

// tg: code/modules/fishing/sources/subtypes/structures.dm:34
public partial class datum_fish_source_holographic : datum_fish_source
{
    // tg: code/modules/fishing/sources/subtypes/structures.dm:49
    public override void on_fishing_spot_init(datum_component_fishing_spot spot = null)
    {
        ADD_TRAIT(spot.parent, TRAIT_UNLINKABLE_FISHING_SPOT, REF(this)); //You would have to be inside the holodeck anyway...
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:52
    public override void on_fishing_spot_del(datum_component_fishing_spot spot = null)
    {
        REMOVE_TRAIT(spot.parent, TRAIT_UNLINKABLE_FISHING_SPOT, REF(this));
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:64
    public override object reason_we_cant_fish(obj_item_fishing_rod rod = null, mob fisherman = null, atom parent = null)
    {
        object dot = base.reason_we_cant_fish(rod, fisherman, parent);
        if (!istype(get_area(fisherman), typeof(area_station_holodeck)))
            return I18n.T("You need to be inside the Holodeck to catch holographic fish.");
        return dot;
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:69
    public override void pre_challenge_started(obj_item_fishing_rod rod = null, mob user = null, datum_fishing_challenge challenge = null)
    {
        RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(check_area));
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:72
    [SignalHandler]
    public virtual void check_area(mob user = null)
    {
        if (!istype(get_area(user), typeof(area_station_holodeck)))
            interrupt_challenge(I18n.T("exited holodeck"));
    }

    // tg declares this override as (source, user, success) where the signal passes (user, challenge, success) (:77), so its
    // `user` is the challenge and the user's move signal stays registered; kept as tg has it.
    // tg: code/modules/fishing/sources/subtypes/structures.dm:77
    public override object on_challenge_completed(mob user = null, datum_fishing_challenge challenge = null, object success = null)
    {
        object dot = base.on_challenge_completed(user, challenge, success);
        UnregisterSignal(challenge, COMSIG_MOVABLE_MOVED);
        return dot;
    }
}

// tg: code/modules/fishing/sources/subtypes/structures.dm:81
public partial class datum_fish_source_hydro_tray : datum_fish_source
{
    // tg: code/modules/fishing/sources/subtypes/structures.dm:148
    public override object reason_we_cant_fish(obj_item_fishing_rod rod = null, mob fisherman = null, atom parent = null)
    {
        if (!istype(parent, typeof(obj_machinery_hydroponics_constructable)))
            return base.reason_we_cant_fish(rod, fisherman, parent);

        obj_machinery_hydroponics_constructable basin = (obj_machinery_hydroponics_constructable)parent;
        if (basin.waterlevel <= 0)
            return I18n.T("There's no water in [] to fish in.", parent);
        if (basin.myseed != null)
            return I18n.T("There's a plant growing in [].", parent);

        return base.reason_we_cant_fish(rod, fisherman, parent);
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:160
    public override void spawn_reward_from_explosion(atom location = null, object severity = null)
    {
        if (!istype(location, typeof(obj_machinery_hydroponics_constructable)))
        {
            base.spawn_reward_from_explosion(location, severity);
            return;
        }

        obj_machinery_hydroponics_constructable basin = (obj_machinery_hydroponics_constructable)location;
        if (basin.myseed != null || basin.waterlevel <= 0)
            return;
        base.spawn_reward_from_explosion(location, severity);
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:169
    public override async DmTask<object> spawn_reward(object reward_path = null, atom spawn_location = null, atom fishing_spot = null, params object[] __extra)
    {
        if (!Dm.eq(reward_path, FISHING_RANDOM_SEED))
        {
            object created = await base.spawn_reward(reward_path, spawn_location, fishing_spot, __extra);
            if (created is mob_living created_reward)
            {
                created_reward.name = I18n.T("small []", created_reward.name);
                await created_reward.update_transform(0.75f);
            }
            return created;
        }

        DmList seeds_to_draw_from = TgState.Current.datum_fish_source_hydro_tray__seeds_to_draw_from;
        if (seeds_to_draw_from == null)
        {
            seeds_to_draw_from = TgState.Current.datum_fish_source_hydro_tray__seeds_to_draw_from = subtypesof(typeof(obj_item_seeds)) as DmList;
            // These two are already covered innately
            Dm.remove(seeds_to_draw_from, typeof(obj_item_seeds_random));
            Dm.remove(seeds_to_draw_from, typeof(obj_item_seeds_grass));
            // -1 yield are unharvestable plants so we don't care
            // 20 rarirty is where most of the wacky plants are so let's ignore them
            foreach (Type seed_path in Dm.each<Type>(seeds_to_draw_from))
            {
                if (Dm.num(Dm.initial(seed_path, (obj_item_seeds p) => p.yield)) == -1 || Dm.num(Dm.initial(seed_path, (obj_item_seeds p) => p.rarity)) >= PLANT_MODERATELY_RARE)
                    Dm.remove(seeds_to_draw_from, seed_path);
            }
        }

        Type picked_path = pick(seeds_to_draw_from) as Type;
        return await Dm.new_async(picked_path, spawn_location);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/fishing/sources/subtypes/structures.dm:175  var/static/list/seeds_to_draw_from
    internal DmList datum_fish_source_hydro_tray__seeds_to_draw_from;
}

// tg: code/modules/fishing/sources/subtypes/structures.dm:218
public partial class datum_fish_source_aquarium : datum_fish_source
{
    // tg: code/modules/fishing/sources/subtypes/structures.dm:229
    public override object get_fish_table(atom location = null, float? from_explosion = null)
    {
        from_explosion ??= FALSE;
        if (istype(location, typeof(obj_machinery_fishing_portal_generator)))
        {
            obj_machinery_fishing_portal_generator portal = (obj_machinery_fishing_portal_generator)location;
            location = portal.current_linked_atom;
        }
        DmList table = Dm.list();
        foreach (obj_item_fish fish in Dm.each<obj_item_fish>(location))
        {
            if (Dm.eq(fish.status, FISH_DEAD)) //dead fish cannot be caught
                continue;
            table[fish] = 10;
        }
        if (!Dm.truthy(length(table)))
            return fish_table.Copy();
        return table;
    }
}

// tg: code/modules/fishing/sources/subtypes/structures.dm:254
public partial class datum_fish_source_vending : datum_fish_source
{
    // tg: code/modules/fishing/sources/subtypes/structures.dm:277
    public override async DmTask<object> get_modified_fish_table(obj_item_fishing_rod rod = null, mob fisherman = null, atom location = null)
    {
        if (istype(location, typeof(obj_machinery_fishing_portal_generator)))
        {
            obj_machinery_fishing_portal_generator portal = (obj_machinery_fishing_portal_generator)location;
            location = portal.current_linked_atom;
        }
        if (!istype(location, typeof(obj_machinery_vending)))
            return Dm.list();

        await DmTask.CompletedTask;
        return get_vending_table(rod, fisherman, (obj_machinery_vending)location);
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:286
    public virtual object get_vending_table(obj_item_fishing_rod rod = null, mob fisherman = null, obj_machinery_vending location = null)
    {
        DmList table = Dm.list();
        ///Create a list of products, ordered by price from highest to lowest
        DmList products = Dm.add(Dm.add(location.product_records, location.coin_records), location.hidden_records) as DmList;
        sortTim(products, "/proc/" + nameof(GlobalProcs.cmp_vending_prices));

        object credit_value = rod.bait_value?.get_item_credit_value();
        float bait_value = Dm.truthy(credit_value) ? Dm.num(credit_value) : 1;

        float highest_record_price = 0;
        foreach (datum_data_vending_product product_record in Dm.each<datum_data_vending_product>(products))
        {
            if (Dm.num(product_record.amount) <= 0)
            {
                Dm.remove(products, product_record);
                table[FISHING_DUD] = Dm.num(table[FISHING_DUD]) + PAYCHECK_LOWER; //it gets harder the emptier the machine is
                continue;
            }
            if (highest_record_price == 0)
                highest_record_price = Dm.num(product_record.price);
            float high = max(highest_record_price, bait_value);
            float low = min(highest_record_price, bait_value);

            //the smaller the difference between product price and bait value, the more likely you're to get it.
            table[product_record] = low / high * 1000; //multiply the value by 1000 for accuracy. pick_weight() doesn't work with zero decimals yet.
        }

        add_risks(table, bait_value, highest_record_price, length(products) * 0.5f);
        return table;
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:311
    public virtual void add_risks(DmList table = null, object bait_value = null, object highest_price = null, object malus_multiplier = null)
    {
        ///Using more than the money needed to buy the most expensive item (why would you do it?!) will remove the dud chance.
        if (Dm.num(bait_value) > Dm.num(highest_price))
            Dm.remove(table, FISHING_DUD);
        else
        {
            //Makes using 1 cred chips with the minigame skip (negative fishing difficulty) a bit less cheesy.
            float malus = min(PAYCHECK_LOWER - Dm.num(bait_value), Dm.num(highest_price));
            if (malus > 0)
            {
                table[FISHING_DUD] = Dm.num(table[FISHING_DUD]) + malus * Dm.num(malus_multiplier);
                table[FISHING_VENDING_CHUCK] = Dm.num(table[FISHING_VENDING_CHUCK]) + malus * Dm.num(malus_multiplier);
            }
        }
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:322  #define FISHING_PRODUCT_DIFFICULTY_MULT 1.6
    private const float FISHING_PRODUCT_DIFFICULTY_MULT = 1.6f;

    // DEVIATION(DV681): tg declares this override as (challenge, result, rod, fisherman) (:324) while every caller passes the base's
    // (result, rod, fisherman) (_fish_source.dm:173, admin.dm:57), so its `rod` is the fisherman and `rod.bait` is a runtime that
    // stops every vending machine's minigame. The port reads the arguments as the callers pass them.
    // tg: code/modules/fishing/sources/subtypes/structures.dm:324
    public override object calculate_difficulty(object result = null, obj_item_fishing_rod rod = null, mob fisherman = null, params object[] __extra)
    {
        //Using less than a minimum paycheck is going to make the challenge a tad harder.
        float bait_value = Dm.num(rod.bait_value?.get_item_credit_value());
        float base_diff = PAYCHECK_LOWER - bait_value;
        return Dm.num(base.calculate_difficulty(result, rod, fisherman, __extra)) + Dm.num(get_product_difficulty(base_diff, result as datum)) * FISHING_PRODUCT_DIFFICULTY_MULT;
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:330
    public virtual object get_product_difficulty(object diff = null, datum result = null)
    {
        if (istype(result, typeof(datum_data_vending_product)))
        {
            datum_data_vending_product product = (datum_data_vending_product)result;
            diff = min(Dm.num(diff), Dm.num(product.price)); // low priced items are easier to catch anyway
        }
        return diff;
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:338
    public override async DmTask<object> dispense_reward(object reward_path = null, mob fisherman = null, atom fishing_spot = null, obj_item_fishing_rod rod = null)
    {
        obj_machinery_vending vending = fishing_spot as obj_machinery_vending;
        if (istype(fishing_spot, typeof(obj_machinery_fishing_portal_generator)))
        {
            obj_machinery_fishing_portal_generator portal = (obj_machinery_fishing_portal_generator)fishing_spot;
            vending = portal.current_linked_atom as obj_machinery_vending;
        }

        if (Dm.eq(reward_path, FISHING_VENDING_CHUCK))
        {
            if (fishing_spot != vending) //fishing portals
                await vending.forceMove(get_turf(fishing_spot));
            await vending.tilt(fisherman, range: 4);
            return null; //Don't spawn a reward at all
        }

        atom_movable reward = await base.dispense_reward(reward_path, fisherman, fishing_spot, rod) as atom_movable;
        if (reward != null)
        {
            object creds_value = rod.bait_value?.get_item_credit_value();
            if (Dm.truthy(creds_value))
            {
                vending.credits_contained += round(Dm.num(creds_value) * VENDING_CREDITS_COLLECTION_AMOUNT);
                qdel(rod.bait_value);
            }
        }
        return reward;
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:358
    public override async DmTask<object> spawn_reward(object reward_path = null, atom spawn_location = null, atom fishing_spot = null, params object[] __extra)
    {
        if (istype(fishing_spot, typeof(obj_machinery_fishing_portal_generator)))
        {
            obj_machinery_fishing_portal_generator portal = (obj_machinery_fishing_portal_generator)fishing_spot;
            fishing_spot = portal.current_linked_atom;
        }
        if (!istype(fishing_spot, typeof(obj_machinery_vending)))
            return null;
        return await spawn_vending_reward(reward_path, spawn_location, (obj_machinery_vending)fishing_spot);
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:366
    public virtual async DmTask<object> spawn_vending_reward(object reward_path = null, atom spawn_location = null, obj_machinery_vending fishing_spot = null)
    {
        datum_data_vending_product product_record = reward_path as datum_data_vending_product;
        if (product_record == null || Dm.num(product_record.amount) <= 0)
            return null;
        return await fishing_spot.dispense(product_record, spawn_location);
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:372
    public override void pre_challenge_started(obj_item_fishing_rod rod = null, mob user = null, datum_fishing_challenge challenge = null)
    {
        RegisterSignal(rod, COMSIG_FISHING_ROD_CAUGHT_FISH, nameof(on_reward));
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:375
    public override object on_challenge_completed(mob user = null, datum_fishing_challenge challenge = null, object success = null)
    {
        object dot = base.on_challenge_completed(user, challenge, success);
        UnregisterSignal(challenge.used_rod, COMSIG_FISHING_ROD_CAUGHT_FISH);
        return dot;
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:379
    [SignalHandler]
    public virtual void on_reward(obj_item_fishing_rod rod = null, atom_movable reward = null, mob user = null)
    {
        if (reward != null && !QDELETED(rod.bait_value) && Dm.truthy(rod.bait_value.get_item_credit_value())) //you pay for what you get
            qdel(rod.bait_value); // fishing_rod.Exited() will handle clearing the hard ref.
    }
}

///subtype of fish_source/vending for custom vending machines
// tg: code/modules/fishing/sources/subtypes/structures.dm:385
public partial class datum_fish_source_vending_custom : datum_fish_source_vending
{
    // tg: code/modules/fishing/sources/subtypes/structures.dm:388
    public override object get_vending_table(obj_item_fishing_rod rod = null, mob fisherman = null, obj_machinery_vending location = null)
    {
        obj_machinery_vending_custom custom_location = (obj_machinery_vending_custom)location;
        DmList table = Dm.list();
        ///Create a list of products, ordered by price from highest to lowest
        DmList products = Dm.sub(custom_location.contents, custom_location.component_parts) as DmList;
        sortTim(products, "/proc/" + nameof(GlobalProcs.cmp_item_vending_prices));

        object credit_value = rod.bait_value?.get_item_credit_value();
        float bait_value = Dm.truthy(credit_value) ? Dm.num(credit_value) : 1;

        float highest_record_price = 0;
        foreach (obj_item stocked in Dm.each<obj_item>(products))
        {
            //count how many items of this type are in the machine
            float item_count = 1;
            foreach (obj_item thing in Dm.each<obj_item>(products))
            {
                if (stocked.type == thing.type)
                    item_count += 1;
            }
            //find what percentage of the total storage space this item occupies.
            if (ROUND_UP((item_count / custom_location.max_loaded_items) * 100) <= 20)
            {
                table[FISHING_DUD] = Dm.num(table[FISHING_DUD]) + PAYCHECK_LOWER; //it gets harder if it occupies less than 20% of the available space i.e the more free space is inside this machine
                continue;
            }
            if (highest_record_price == 0)
                highest_record_price = Dm.num(stocked.custom_price);
            float high = max(highest_record_price, bait_value);
            float low = min(highest_record_price, bait_value);

            //the smaller the difference between product price and bait value, the more likely you're to get it.
            table[stocked] = (low / high) * 1000; //multiply the value by 1000 for accuracy. pick_weight() doesn't work with zero decimals yet.
        }

        add_risks(table, bait_value, highest_record_price, length(products) * 0.5f);
        return table;
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:418
    public override object get_product_difficulty(object diff = null, datum result = null)
    {
        if (isitem(result))
        {
            obj_item product = (obj_item)result;
            diff = min(Dm.num(diff), Dm.num(product.custom_price));
        }
        return diff;
    }

    // tg: code/modules/fishing/sources/subtypes/structures.dm:424
    public override async DmTask<object> spawn_vending_reward(object reward_path = null, atom spawn_location = null, obj_machinery_vending fishing_spot = null)
    {
        if (!isitem(reward_path))
            return null;
        obj_item reward = (obj_item)reward_path;
        await reward.forceMove(spawn_location);
        return reward;
    }
}

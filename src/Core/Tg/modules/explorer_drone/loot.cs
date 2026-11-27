// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/loot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the loot generators an adventure or a resource names (maintenance loot, the trade contracts that unlock the
// exploration supply packs, the valuables, drugs, materials, weapons, syndicate fish and the pets), the antique, the trade contract
// chip and the fire lance. The var blocks are generated (D111) but for the fire lance's cell, an object the generated typepath field
// cannot hold.

public partial class datum_controller_global_vars
{
    // tg: code/modules/explorer_drone/loot.dm:1  GLOBAL_LIST_INIT(adventure_loot_generator_index, generate_generator_index())
    public DmList adventure_loot_generator_index;

    // tg: code/modules/explorer_drone/loot.dm:1  (GLOBAL_MANAGED(adventure_loot_generator_index, generate_generator_index()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaladventure_loot_generator_index()
    {
        adventure_loot_generator_index = generate_generator_index() as DmList;
        Dm.append(ref gvars_datum_init_order, "adventure_loot_generator_index");
    }
}

public static partial class GlobalProcs
{
    /// Creates generator__id => type map.
    // tg: code/modules/explorer_drone/loot.dm:4
    public static object generate_generator_index()
    {
        DmList dot = Dm.list();
        foreach (object type in Dm.each(typesof(typeof(datum_adventure_loot_generator))))
        {
            Type generator = (Type)type;
            if (!Dm.truthy(Dm.initial(generator, (datum_adventure_loot_generator g) => g.id)))
                continue;
            dot[Dm.initial(generator, (datum_adventure_loot_generator g) => g.id)] = type;
        }
        return dot;
    }
}

/// Adventure loot category identified by ID
// tg: code/modules/explorer_drone/loot.dm:13
public partial class datum_adventure_loot_generator : datum
{
    // tg: code/modules/explorer_drone/loot.dm:16
    public virtual async DmTask<object> generate()
    {
        return null;
    }

    /// Helper to transfer loot while respecting cargo space
    // tg: code/modules/explorer_drone/loot.dm:20
    public virtual async DmTask transfer_loot(obj_item_exodrone drone = null)
    {
        foreach (obj loot in Dm.each<obj>(await generate()))
            await drone.try_transfer(loot);
    }
}

/// Uses manintenance loot generators
// tg: code/modules/explorer_drone/loot.dm:25
public partial class datum_adventure_loot_generator_maintenance : datum_adventure_loot_generator
{
    // tg: code/modules/explorer_drone/loot.dm:29
    public override async DmTask<object> generate()
    {
        DmList all_loot = Dm.list();
        for (float i = 1, i__end = Dm.num(amount); i <= i__end; i++)
        {
            object lootspawn = pick_weight(GLOB.maintenance_loot);
            while (islist(lootspawn))
                lootspawn = pick_weight((DmList)lootspawn);
            atom_movable loot = await Dm.new_async((Type)lootspawn) as atom_movable;
            Dm.append(ref all_loot, loot);
        }
        return all_loot;
    }
}

/// Unlocks special cargo crates
// tg: code/modules/explorer_drone/loot.dm:40
public partial class datum_adventure_loot_generator_cargo : datum_adventure_loot_generator
{
    // tg: code/modules/explorer_drone/loot.dm:42  var/static/list/unlockable_packs
    public static ref DmList unlockable_packs => ref TgState.Current.datum_adventure_loot_generator_cargo__unlockable_packs;

    // tg: code/modules/explorer_drone/loot.dm:44
    public override async DmTask<object> generate()
    {
        unlockable_packs ??= Dm.list(typeof(datum_supply_pack_exploration_scrapyard), typeof(datum_supply_pack_exploration_catering), typeof(datum_supply_pack_exploration_shrubbery));
        DmList still_locked_packs = Dm.list();
        foreach (object pack_type in Dm.each(unlockable_packs))
        {
            datum_supply_pack pack_singleton = SSshuttle.supply_packs[pack_type] as datum_supply_pack;
            if (!Dm.truthy(Dm.bitand(pack_singleton.order_flags, ORDER_SPECIAL_ENABLED)))
                Dm.append(ref still_locked_packs, pack_type);
        }
        if (!Dm.truthy(length(still_locked_packs))) // Just give out some cash instead.
        {
            datum_adventure_loot_generator_simple_cash replacement = Dm.@new<datum_adventure_loot_generator_simple_cash>();
            return await replacement.generate();
        }
        object chosen_pack_type = pick(still_locked_packs);
        return Dm.list(Dm.@new<obj_item_trade_chip>(null, chosen_pack_type));
    }
}

/// Just picks and instatiates the path from the list
// tg: code/modules/explorer_drone/loot.dm:57
public partial class datum_adventure_loot_generator_simple : datum_adventure_loot_generator
{
    // tg: code/modules/explorer_drone/loot.dm:60
    public override async DmTask<object> generate()
    {
        object loot_type = pick(loot_list);
        return Dm.list(await Dm.new_async((Type)loot_type));
    }
}

/// Pets and pet accesories in carriers
// tg: code/modules/explorer_drone/loot.dm:95
public partial class datum_adventure_loot_generator_pet : datum_adventure_loot_generator
{
    // tg: code/modules/explorer_drone/loot.dm:105
    public override async DmTask<object> generate()
    {
        obj_item_pet_carrier carrier = await Dm.new_async(carrier_type) as obj_item_pet_carrier;
        object chosen_pet_type = pick(possible_pets);
        mob_living_basic_pet pet = await Dm.new_async((Type)chosen_pet_type) as mob_living_basic_pet;
        await carrier.add_occupant(pet);
        return Dm.list(carrier);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/explorer_drone/loot.dm:42  var/static/list/unlockable_packs
    internal DmList datum_adventure_loot_generator_cargo__unlockable_packs;
}

/// Supply pack unlocker chip
// tg: code/modules/explorer_drone/loot.dm:119
public partial class obj_item_trade_chip : obj_item
{
    // tg: code/modules/explorer_drone/loot.dm:127
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object pack_type = Dm.arg(__extra, 0);
        if (Dm.truthy(pack_type))
        {
            unlocked_pack_type = pack_type;
            name += Dm.text("- []", Dm.initial((Type)pack_type, (datum_supply_pack p) => p.name));
        }
        return dot;
    }

    // tg: code/modules/explorer_drone/loot.dm:134
    public virtual void try_to_unlock_contract(mob user = null)
    {
        var pack_singleton = SSshuttle.supply_packs[unlocked_pack_type] as datum_supply_pack;
        if (!Dm.truthy(unlocked_pack_type) || pack_singleton == null || !Dm.truthy(Dm.bitand(pack_singleton.order_flags, ORDER_SPECIAL)))
        {
            to_chat(user, span_danger(I18n.T("This chip is invalid!")));
            return;
        }
        pack_singleton.order_flags = Dm.num(Dm.bitor(pack_singleton.order_flags, ORDER_SPECIAL_ENABLED));
        to_chat(user, span_notice(I18n.T("Contract accepted into nanotrasen supply database.")));
        qdel(this);
    }
}

/// Two handed fire lance. Melts wall after short windup.
// tg: code/modules/explorer_drone/loot.dm:145
public partial class obj_item_firelance : obj_item
{
    /// The lance's cell (an object: the generated field would be a typepath, rules 3.2 V3)
    // tg: code/modules/explorer_drone/loot.dm:155
    public obj_item_stock_parts_power_store cell;

    // tg: code/modules/explorer_drone/loot.dm:145  (the type's var overrides: the hand-written cell moves the var block to the hand)
    public obj_item_firelance()
    {
        name = "fire lance";
        desc = "Melts everything in front of you. Takes a while to start and operate.";
        icon = Dm.resource("icons/obj/exploration.dmi");
        icon_state = "firelance";
        inhand_icon_state = "firelance";
        righthand_file = Dm.resource("icons/mob/inhands/items/firelance_righthand.dmi");
        lefthand_file = Dm.resource("icons/mob/inhands/items/firelance_lefthand.dmi");
    }

    // tg: code/modules/explorer_drone/loot.dm:157
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cell = Dm.@new<obj_item_stock_parts_power_store_cell>(this);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed)));
        return dot;
    }

    // tg: code/modules/explorer_drone/loot.dm:162
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(user.combat_mode))
            return null;
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/modules/explorer_drone/loot.dm:167
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/explorer_drone/loot.dm:170
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
        {
            to_chat(user, span_notice(I18n.T("You need to wield [] in two hands before you can fire it.", this)));
            return dot;
        }
        if (Dm.truthy(LAZYACCESS(user.do_afters, "firelance")))
            return dot;
        if (!Dm.truthy(await cell.use(0.2f * STANDARD_CELL_CHARGE)))
        {
            to_chat(user, span_warning(I18n.T("[]'s battery ran dry!", this)));
            return dot;
        }
        ADD_TRAIT(user, TRAIT_IMMOBILIZED, REF(this));
        to_chat(user, span_notice(I18n.T("You begin to charge []...", this)));
        inhand_icon_state = "firelance_charging";
        await user.update_held_items();
        if (Dm.truthy(await do_after(user, windup_time, interaction_key: "firelance", extra_checks: Dm.@new<datum_callback>(this, nameof(windup_checks)))))
        {
            turf start_turf = get_turf(user) as turf;
            turf last_turf = get_ranged_target_turf(start_turf, user.dir, melt_range) as turf;
            start_turf.Beam(last_turf, icon_state: "solar_beam", time: 1 * SECONDS);
            foreach (turf turf_to_melt in Dm.each<turf>(get_line(start_turf, last_turf)))
            {
                if (Dm.truthy(turf_to_melt.density))
                    await turf_to_melt.Melt();
            }
            dot = ITEM_INTERACT_SUCCESS;
        }
        inhand_icon_state = Dm.initial(this, (obj_item_firelance p) => p.inhand_icon_state);
        await user.update_held_items();
        REMOVE_TRAIT(user, TRAIT_IMMOBILIZED, REF(this));
        return dot;
    }

    /// Additional windup checks
    // tg: code/modules/explorer_drone/loot.dm:198
    public virtual object windup_checks()
    {
        return Dm.num(HAS_TRAIT(this, TRAIT_WIELDED));
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/shuttle_loan/shuttle_loan_datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E3 (D327): the whole file, every shuttle loan situation and what it ships back on the cargo shuttle. The var blocks are generated
// (D111).

/// One of the potential shuttle loans you might receive.
// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:2
public partial class datum_shuttle_loan_situation : datum
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:16
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(logging_desc))
            stack_trace(Dm.text("No logging blurb set for []!", type), "code/modules/events/shuttle_loan/shuttle_loan_datum.dm", 19);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_LOANER_SHUTTLE))
            bonus_points *= 1.15f;
    }

    /// Spawns paths added to `spawn_list`, and passes empty shuttle turfs so you can spawn more complicated things like dead bodies.
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:24
    public virtual async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        throw Dm.CRASH(Dm.text("Unimplemented get_spawned_items() on [].", type), "code/modules/events/shuttle_loan/shuttle_loan_datum.dm", 26);
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:28
public partial class datum_shuttle_loan_situation_antidote : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:34
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        Type infected_assistant = (Type)pick(Dm.list(
            typeof(obj_effect_mob_spawn_corpse_human_assistant_beesease_infection),
            typeof(obj_effect_mob_spawn_corpse_human_assistant_brainrot_infection),
            typeof(obj_effect_mob_spawn_corpse_human_assistant_spanishflu_infection)
        ));
        for (float i = 1; i <= 10; i++)
        {
            if (prob(15))
                spawn_list.Add(typeof(obj_item_reagent_containers_cup_bottle));
            else if (prob(15))
                spawn_list.Add(typeof(obj_item_reagent_containers_syringe));
            else if (prob(25))
                spawn_list.Add(typeof(obj_item_shard));
            turf assistant_turf = pick_n_take(empty_shuttle_turfs) as turf;
            await Dm.new_async(infected_assistant, assistant_turf);
        }
        spawn_list.Add(typeof(obj_structure_closet_crate));
        spawn_list.Add(typeof(obj_item_reagent_containers_cup_bottle_pierrot_throat));
        spawn_list.Add(typeof(obj_item_reagent_containers_cup_bottle_magnitis));
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:53
public partial class datum_shuttle_loan_situation_department_resupply : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:61
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        DmList crate_types = Dm.list(
            typeof(datum_supply_pack_emergency_equipment),
            typeof(datum_supply_pack_security_supplies),
            typeof(datum_supply_pack_organic_food),
            typeof(datum_supply_pack_emergency_weedcontrol),
            typeof(datum_supply_pack_engineering_tools),
            typeof(datum_supply_pack_engineering_engiequipment),
            typeof(datum_supply_pack_science_robotics),
            typeof(datum_supply_pack_science_plasma),
            typeof(datum_supply_pack_medical_supplies)
            );
        foreach (object crate in Dm.each(crate_types))
        {
            datum_supply_pack pack = SSshuttle.supply_packs[crate] as datum_supply_pack;
            await pack.generate(pick_n_take(empty_shuttle_turfs) as atom);
        }

        for (float i = 1; i <= 5; i++)
        {
            object decal = pick(typeof(obj_effect_decal_cleanable_food_flour), typeof(obj_effect_decal_cleanable_blood_gibs_robot_debris), typeof(obj_effect_decal_cleanable_blood_oil));
            await Dm.new_async((Type)decal, pick_n_take(empty_shuttle_turfs));
        }
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:81
public partial class datum_shuttle_loan_situation_syndiehijacking : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:87
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        datum_supply_pack pack = SSshuttle.supply_packs[typeof(datum_supply_pack_imports_specialops)] as datum_supply_pack;
        await pack.generate(pick_n_take(empty_shuttle_turfs) as atom);

        spawn_list.Add(typeof(mob_living_basic_trooper_syndicate_ranged_infiltrator));
        spawn_list.Add(typeof(mob_living_basic_trooper_syndicate_ranged_infiltrator));
        if (prob(75))
            spawn_list.Add(typeof(mob_living_basic_trooper_syndicate_ranged_infiltrator));
        if (prob(50))
            spawn_list.Add(typeof(mob_living_basic_trooper_syndicate_ranged_infiltrator));
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:98
public partial class datum_shuttle_loan_situation_lots_of_bees : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:105
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        datum_supply_pack pack = SSshuttle.supply_packs[typeof(datum_supply_pack_organic_hydroponics_beekeeping_fullkit)] as datum_supply_pack;
        await pack.generate(pick_n_take(empty_shuttle_turfs) as atom);

        spawn_list.Add(typeof(obj_effect_mob_spawn_corpse_human_bee_terrorist));
        spawn_list.Add(typeof(obj_effect_mob_spawn_corpse_human_cargo_tech));
        spawn_list.Add(typeof(obj_effect_mob_spawn_corpse_human_cargo_tech));
        spawn_list.Add(typeof(obj_effect_mob_spawn_corpse_human_nanotrasensoldier));
        spawn_list.Add(typeof(obj_item_gun_ballistic_automatic_pistol_no_mag));
        spawn_list.Add(typeof(obj_item_gun_ballistic_automatic_pistol_m1911_no_mag));
        spawn_list.Add(typeof(obj_item_honey_frame));
        spawn_list.Add(typeof(obj_item_honey_frame));
        spawn_list.Add(typeof(obj_item_honey_frame));
        spawn_list.Add(typeof(obj_structure_beebox_unwrenched));
        spawn_list.Add(typeof(obj_item_queen_bee_bought));
        spawn_list.Add(typeof(obj_structure_closet_crate_hydroponics));

        for (float i = 1; i <= 8; i++)
            spawn_list.Add(typeof(mob_living_basic_bee_toxin));

        for (float i = 1; i <= 5; i++)
        {
            object decal = pick(typeof(obj_effect_decal_cleanable_blood), typeof(obj_effect_decal_cleanable_insectguts));
            await Dm.new_async((Type)decal, pick_n_take(empty_shuttle_turfs));
        }

        for (float i = 1; i <= 10; i++)
        {
            Type casing = typeof(obj_item_ammo_casing_spent);
            await Dm.new_async(casing, pick_n_take(empty_shuttle_turfs));
        }
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:133
public partial class datum_shuttle_loan_situation_jc_a_bomb : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:141
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        spawn_list.Add(typeof(obj_machinery_syndicatebomb_shuttle_loan));
        if (prob(95))
            spawn_list.Add(typeof(obj_item_paper_fluff_cargo_bomb));
        else
            spawn_list.Add(typeof(obj_item_paper_fluff_cargo_bomb_allyourbase));
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:148
public partial class datum_shuttle_loan_situation_papers_please : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:156
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        Dm.append(ref spawn_list, Dm.sub(Dm.sub(subtypesof(typeof(obj_item_paperwork)), typesof(typeof(obj_item_paperwork_photocopy))), typesof(typeof(obj_item_paperwork_ancient))));
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:159
public partial class datum_shuttle_loan_situation_pizza_delivery : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:167
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        DmList naughtypizza = Dm.list(typeof(obj_item_pizzabox_bomb), typeof(obj_item_pizzabox_margherita_robo)); //oh look another blacklist, for pizza nonetheless!
        DmList nicepizza = Dm.list(typeof(obj_item_pizzabox_margherita), typeof(obj_item_pizzabox_meat), typeof(obj_item_pizzabox_vegetable), typeof(obj_item_pizzabox_mushroom));
        for (float i = 1; i <= 6; i++)
            spawn_list.Add(pick(prob(5) ? naughtypizza : nicepizza));
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:173
public partial class datum_shuttle_loan_situation_russian_party : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:179
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        datum_supply_pack pack = SSshuttle.supply_packs[typeof(datum_supply_pack_service_party)] as datum_supply_pack;
        await pack.generate(pick_n_take(empty_shuttle_turfs) as atom);

        spawn_list.Add(typeof(mob_living_basic_trooper_russian));
        spawn_list.Add(typeof(mob_living_basic_trooper_russian_ranged)); //drops a mateba
        spawn_list.Add(typeof(mob_living_basic_bear_russian));
        if (prob(75))
            spawn_list.Add(typeof(mob_living_basic_trooper_russian));
        if (prob(50))
            spawn_list.Add(typeof(mob_living_basic_bear_russian));
    }
}

// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:191
public partial class datum_shuttle_loan_situation_spider_gift : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:197
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        datum_supply_pack pack = SSshuttle.supply_packs[typeof(datum_supply_pack_imports_specialops)] as datum_supply_pack;
        await pack.generate(pick_n_take(empty_shuttle_turfs) as atom);

        spawn_list.Add(typeof(mob_living_basic_spider_giant));
        spawn_list.Add(typeof(mob_living_basic_spider_giant));
        spawn_list.Add(typeof(mob_living_basic_spider_giant_nurse));
        if (prob(50))
            spawn_list.Add(typeof(mob_living_basic_spider_giant_hunter));

        turf victim_turf = pick_n_take(empty_shuttle_turfs) as turf;

        Dm.@new<obj_effect_decal_remains_human>(victim_turf);
        Dm.@new<obj_item_clothing_shoes_jackboots_fast>(victim_turf);
        Dm.@new<obj_item_clothing_mask_balaclava>(victim_turf);

        for (float i = 1; i <= 5; i++)
        {
            turf web_turf = pick_n_take(empty_shuttle_turfs) as turf;
            Dm.@new<obj_structure_spider_stickyweb>(web_turf);
        }
    }
}

/**
 * A special shuttle loan situation enabled by the 'mail blocked' station trait.
 * It sends back a lot of mail to the station, at the cost of wrecking the cargo shuttle a little.
 */
// tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:229
public partial class datum_shuttle_loan_situation_mail_strike : datum_shuttle_loan_situation
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:217-223  the file-local defines
    private const string DENT_WALL = "dent";
    private const string CHANGE_WALL = "change";
    private const string DISMANTLE_WALL = "dismantle";

    private const string BREAK_TILE = "break";
    private const string PLATING_TILE = "plating";
    private const string RUST_TILE = "rust";

    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:242  var/static/list/wall_bad_stuff (a proc-level static, rules 10.6)
    private static ref DmList spawn_items__wall_bad_stuff => ref TgState.Current.datum_shuttle_loan_situation_mail_strike__spawn_items__wall_bad_stuff;

    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:243  var/static/list/possible_new_walls (a proc-level static, rules 10.6)
    private static ref DmList spawn_items__possible_new_walls => ref TgState.Current.datum_shuttle_loan_situation_mail_strike__spawn_items__possible_new_walls;

    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:267  var/static/list/floor_bad_stuff (a proc-level static, rules 10.6)
    private static ref DmList spawn_items__floor_bad_stuff => ref TgState.Current.datum_shuttle_loan_situation_mail_strike__spawn_items__floor_bad_stuff;

    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:237
    public override async DmTask spawn_items(DmList spawn_list = null, DmList empty_shuttle_turfs = null, DmList blocked_shutte_turfs = null)
    {
        for (float i = 1, i__end = rand(7, 12); i <= i__end; i++)
        {
            turf_closed_wall wall = pick_n_take(blocked_shutte_turfs) as turf_closed_wall;
            if (wall == null)
                continue;
            spawn_items__wall_bad_stuff ??= Dm.list(Dm.kv(DENT_WALL, 85), Dm.kv(CHANGE_WALL, 13), Dm.kv(DISMANTLE_WALL, 2));
            spawn_items__possible_new_walls ??= Dm.list(
                typeof(turf_closed_wall_mineral_sandstone),
                typeof(turf_closed_wall_mineral_wood),
                typeof(turf_closed_wall_mineral_iron),
                typeof(turf_closed_wall_metal_foam_base),
                typeof(turf_closed_wall_r_wall)
            );
            object damage_done = pick_weight(spawn_items__wall_bad_stuff);
            switch (damage_done)
            {
                case DENT_WALL:
                    for (float dent = 1, dent__end = rand(1, MAX_DENT_DECALS); dent <= dent__end; dent++)
                        wall.add_dent(prob(90) ? WALL_DENT_SHOT : WALL_DENT_HIT);
                    break;
                case CHANGE_WALL:
                    // TURF-IDENTITY (object-model.md 7.1): tg goes on denting `wall`, which BYOND has replaced in place by the new wall
                    wall = await wall.ChangeTurf(pick(Dm.sub(spawn_items__possible_new_walls, wall.type))) as turf_closed_wall;
                    if (prob(25))
                    {
                        for (float dent = 1, dent__end = rand(1, MAX_DENT_DECALS); dent <= dent__end; dent++)
                            wall.add_dent(prob(90) ? WALL_DENT_SHOT : WALL_DENT_HIT);
                    }
                    break;
                case DISMANTLE_WALL:
                    await wall.dismantle_wall();
                    break;
            }
        }

        for (float i = 1, i__end = rand(7, 12); i <= i__end; i++)
        {
            turf_open_floor floor = pick_n_take(empty_shuttle_turfs) as turf_open_floor;
            if (floor == null)
                continue;
            spawn_items__floor_bad_stuff ??= Dm.list(Dm.kv(BREAK_TILE, 65), Dm.kv(PLATING_TILE, 25), Dm.kv(RUST_TILE, 10));
            object damage_done = pick_weight(spawn_items__floor_bad_stuff);
            switch (damage_done)
            {
                case BREAK_TILE:
                    if (prob(50))
                        floor.break_tile();
                    else
                        floor.burn_tile();
                    break;
                case PLATING_TILE:
                    if (prob(25))
                        await floor.remove_tile();
                    else
                        await floor.make_plating();
                    break;
                case RUST_TILE:
                    await floor.ChangeTurf(typeof(turf_open_floor_plating_rust));
                    break;
            }
        }
        if (prob(25))
            Dm.append(ref spawn_list, pick(typeof(obj_effect_gibspawner_robot), typeof(obj_effect_gibspawner_human)));

        for (float i = 1, i__end = rand(4, 7); i <= i__end; i++)
            Dm.append(ref spawn_list, typeof(obj_structure_closet_crate_mail_full_mail_strike));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:242  var/static/list/wall_bad_stuff
    internal DmList datum_shuttle_loan_situation_mail_strike__spawn_items__wall_bad_stuff;

    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:243  var/static/list/possible_new_walls
    internal DmList datum_shuttle_loan_situation_mail_strike__spawn_items__possible_new_walls;

    // tg: code/modules/events/shuttle_loan/shuttle_loan_datum.dm:267  var/static/list/floor_bad_stuff
    internal DmList datum_shuttle_loan_situation_mail_strike__spawn_items__floor_bad_stuff;
}

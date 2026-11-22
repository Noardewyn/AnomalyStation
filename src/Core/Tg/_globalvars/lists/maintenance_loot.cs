// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/maintenance_loot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 I1a: the maintenance loot tables (data), which the maintenance random spawners pick from with the world RNG.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/_globalvars/lists/maintenance_loot.dm:17  GLOBAL_LIST_INIT(trash_loot, list(...))  //junk: useless, very easy to get, or ghetto chemistry items
    public DmList trash_loot;

    // tg: code/_globalvars/lists/maintenance_loot.dm:17  (GLOBAL_MANAGED(trash_loot, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltrash_loot()
    {
        trash_loot = Dm.list(
            Dm.kv(Dm.list(  //trash
                Dm.kv(typeof(obj_item_trash_can), 1),
                Dm.kv(typeof(obj_item_trash_candy), 1),
                Dm.kv(typeof(obj_item_trash_cheesie), 1),
                Dm.kv(typeof(obj_item_trash_chips), 1),
                Dm.kv(typeof(obj_item_trash_pistachios), 1),
                Dm.kv(typeof(obj_item_trash_popcorn), 1),
                Dm.kv(typeof(obj_item_trash_raisins), 1),
                Dm.kv(typeof(obj_item_trash_sosjerky), 1),
                Dm.kv(typeof(obj_item_trash_flare), 1),
                Dm.kv(typeof(obj_item_trash_candle), 1),
                Dm.kv(typeof(obj_item_c_tube), 1),
                Dm.kv(typeof(obj_item_disk_data), 1),
                Dm.kv(typeof(obj_item_folder_yellow), 1),
                Dm.kv(typeof(obj_item_hand_labeler), 1),
                Dm.kv(typeof(obj_item_paper), 1),
                Dm.kv(typeof(obj_item_paper_crumpled), 1),
                Dm.kv(typeof(obj_item_pen), 1),
                Dm.kv(typeof(obj_item_photo_old), 1),
                Dm.kv(typeof(obj_item_poster_random_contraband), 1),
                Dm.kv(typeof(obj_item_poster_random_official), 1),
                Dm.kv(typeof(obj_item_stack_sheet_cardboard), 1),
                Dm.kv(typeof(obj_item_storage_box), 1),
                Dm.kv(typeof(obj_effect_decal_cleanable_ash), 1),
                Dm.kv(typeof(obj_item_camera), 1),
                Dm.kv(typeof(obj_item_camera_film), 1),
                Dm.kv(typeof(obj_item_cigbutt), 1),
                Dm.kv(typeof(obj_item_coin_silver), 1),
                Dm.kv(typeof(obj_item_food_urinalcake), 1),
                Dm.kv(typeof(obj_item_light_bulb), 1),
                Dm.kv(typeof(obj_item_light_tube), 1),
                Dm.kv(typeof(obj_item_reagent_containers_cup_glass_drinkingglass), 1),
                Dm.kv(typeof(obj_item_airlock_painter), 1),
                Dm.kv(typeof(obj_item_airlock_painter_decal), 2),
                Dm.kv(typeof(obj_item_clothing_mask_breath), 1),
                Dm.kv(typeof(obj_item_rack_parts), 1),
                Dm.kv(typeof(obj_item_shard), 1),
                Dm.kv(typeof(obj_item_reagent_containers_applicator_pill_maintenance_achievement), 1),
                Dm.kv(typeof(obj_item_toy_eightball), 1)
                ), 8),
            Dm.kv(Dm.list(  //tier 1 stock parts
                Dm.kv(typeof(obj_item_stock_parts_capacitor), 1),
                Dm.kv(typeof(obj_item_stock_parts_servo), 1),
                Dm.kv(typeof(obj_item_stock_parts_matter_bin), 1),
                Dm.kv(typeof(obj_item_stock_parts_micro_laser), 1),
                Dm.kv(typeof(obj_item_stock_parts_scanning_module), 1)
                ), 1)
        );
        Dm.append(ref gvars_datum_init_order, "trash_loot");
    }

    // tg: code/_globalvars/lists/maintenance_loot.dm:74  GLOBAL_LIST_INIT(common_loot, list(...))  //common: basic items
    public DmList common_loot;

    // tg: code/_globalvars/lists/maintenance_loot.dm:74  (GLOBAL_MANAGED(common_loot, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcommon_loot()
    {
        common_loot = Dm.list(
            Dm.kv(Dm.list(  //tools
                Dm.kv(typeof(obj_item_analyzer), 1),
                Dm.kv(typeof(obj_item_crowbar), 1),
                Dm.kv(typeof(obj_item_geiger_counter), 1),
                Dm.kv(typeof(obj_item_mop), 1),
                Dm.kv(typeof(obj_item_pushbroom), 1),
                Dm.kv(typeof(obj_item_reagent_containers_cup_bucket), 1),
                Dm.kv(typeof(obj_item_screwdriver), 1),
                Dm.kv(typeof(obj_item_t_scanner), 1),
                Dm.kv(typeof(obj_item_toy_crayon_spraycan), 1),
                Dm.kv(typeof(obj_item_weldingtool), 1),
                Dm.kv(typeof(obj_item_wirecutters), 1),
                Dm.kv(typeof(obj_item_wrench), 1)
                ), 1),
            Dm.kv(Dm.list(  //equipment
                Dm.kv(typeof(obj_effect_spawner_random_clothing_gloves), 1),
                Dm.kv(typeof(obj_item_clothing_glasses_meson), 1),
                Dm.kv(typeof(obj_item_clothing_glasses_science), 1),
                Dm.kv(typeof(obj_item_clothing_gloves_color_black), 1),
                Dm.kv(typeof(obj_item_clothing_gloves_color_fyellow), 1),
                Dm.kv(typeof(obj_item_clothing_mask_gas), 1),
                Dm.kv(typeof(obj_item_clothing_shoes_sneakers_black), 1),
                Dm.kv(typeof(obj_item_clothing_suit_hazardvest), 1),
                Dm.kv(typeof(obj_item_clothing_suit_toggle_labcoat), 1),
                Dm.kv(typeof(obj_item_clothing_under_color_grey), 1),
                Dm.kv(typeof(obj_item_clothing_suit_pillow_suit), 1),
                Dm.kv(typeof(obj_item_clothing_head_pillow_hood), 1),
                Dm.kv(typeof(obj_item_shield_mattress), 1),
                Dm.kv(typeof(obj_item_spear_pillow), 1),
                Dm.kv(typeof(obj_item_pillow_random), 1),
                Dm.kv(typeof(obj_item_radio_headset), 1),
                Dm.kv(typeof(obj_item_storage_backpack), 1),
                Dm.kv(typeof(obj_item_storage_belt_fannypack), 1),
                Dm.kv(typeof(obj_item_storage_wallet_random), 1)
                ), 1),
            Dm.kv(Dm.list(  //construction and crafting
                Dm.kv(typeof(obj_item_sign), 1),
                Dm.kv(typeof(obj_item_stack_cable_coil), 1),
                Dm.kv(typeof(obj_item_stack_rods_twentyfive), 1),
                Dm.kv(typeof(obj_item_stack_sheet_iron_twenty), 1),
                Dm.kv(typeof(obj_item_stack_sheet_mineral_plasma), 1),
                Dm.kv(typeof(obj_item_stock_parts_power_store_cell), 1),
                Dm.kv(typeof(obj_effect_spawner_random_engineering_vending_restock), 1),
                //assemblies
                Dm.kv(typeof(obj_item_assembly_health), 1),
                Dm.kv(typeof(obj_item_assembly_igniter), 1),
                Dm.kv(typeof(obj_item_assembly_infra), 1),
                Dm.kv(typeof(obj_item_assembly_mousetrap), 1),
                Dm.kv(typeof(obj_item_assembly_prox_sensor), 1),
                Dm.kv(typeof(obj_item_assembly_signaler), 1),
                Dm.kv(typeof(obj_item_assembly_timer), 1),
                Dm.kv(typeof(obj_item_stack_package_wrap), 1),
                Dm.kv(typeof(obj_item_stack_wrapping_paper), 1)
                ), 1),
            Dm.kv(Dm.list(  //medical and chemicals
                Dm.kv(typeof(obj_item_grenade_chem_grenade_cleaner), 1),
                Dm.kv(typeof(obj_item_lead_pipe), 1),
                Dm.kv(typeof(obj_item_reagent_containers_cup_beaker), 1),
                Dm.kv(typeof(obj_item_reagent_containers_cup_bottle_random_buffer), 2),
                Dm.kv(typeof(obj_item_rag), 1),
                Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_pumpup), 2),
                Dm.kv(typeof(obj_item_reagent_containers_syringe), 1),
                Dm.kv(typeof(obj_item_stock_parts_power_store_cell_lead), 1),
                Dm.kv(typeof(obj_item_storage_box_matches), 1),
                Dm.kv(typeof(obj_item_storage_fancy_cigarettes_dromedaryco), 1)
                ), 1),
            Dm.kv(Dm.list(  //food
                Dm.kv(typeof(obj_item_reagent_containers_cup_glass_bottle_beer), 1),
                Dm.kv(typeof(obj_item_reagent_containers_cup_glass_coffee), 1)
                ), 1),
            Dm.kv(Dm.list(  //misc
                Dm.kv(typeof(obj_item_bodybag), 1),
                Dm.kv(typeof(obj_item_extinguisher), 1),
                Dm.kv(typeof(obj_item_grenade_smokebomb), 1),
                Dm.kv(typeof(obj_item_radio_off), 1),
                Dm.kv(typeof(obj_item_stack_spacecash_c10), 1),
                Dm.kv(typeof(obj_item_stack_medical_wrap_sticky_tape), 1),
                Dm.kv(typeof(obj_item_tank_internals_emergency_oxygen), 1),
                Dm.kv(typeof(obj_item_paper_paperslip_fishing_tip), 1),
                //light sources
                Dm.kv(typeof(obj_effect_spawner_random_decoration_glowstick), 1),
                Dm.kv(typeof(obj_item_clothing_head_utility_hardhat_red), 1),
                Dm.kv(typeof(obj_item_flashlight), 1),
                Dm.kv(typeof(obj_item_flashlight_flare), 1)
                ), 1)
        );
        Dm.append(ref gvars_datum_init_order, "common_loot");
    }

    // tg: code/_globalvars/lists/maintenance_loot.dm:170  GLOBAL_LIST_INIT(uncommon_loot, list(...))  //uncommon: useful items
    public DmList uncommon_loot;

    // tg: code/_globalvars/lists/maintenance_loot.dm:170  (GLOBAL_MANAGED(uncommon_loot, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaluncommon_loot()
    {
        uncommon_loot = Dm.list(
            Dm.kv(Dm.list(  //tools
                Dm.kv(typeof(obj_item_grenade_iedcasing_spawned), 1),
                Dm.kv(typeof(obj_item_hatchet), 1),
                Dm.kv(typeof(obj_item_melee_baton_security_cattleprod), 1),
                Dm.kv(typeof(obj_item_multitool), 1),
                Dm.kv(typeof(obj_item_pen_fountain), 1),
                Dm.kv(typeof(obj_item_restraints_handcuffs_cable), 1),
                Dm.kv(typeof(obj_item_restraints_legcuffs_bola), 1),
                Dm.kv(typeof(obj_item_emergency_bed), 1),
                Dm.kv(typeof(obj_item_spear), 1),
                Dm.kv(typeof(obj_item_weldingtool_largetank), 1),
                Dm.kv(typeof(obj_item_market_uplink_blackmarket), 1)
                ), 8),
            Dm.kv(Dm.list(  //equipment
                Dm.kv(typeof(obj_item_clothing_ears_earmuffs), 1),
                Dm.kv(typeof(obj_item_clothing_glasses_hud_diagnostic), 1),
                Dm.kv(typeof(obj_item_clothing_glasses_hud_health), 1),
                Dm.kv(typeof(obj_item_clothing_glasses_salesman), 1),
                Dm.kv(typeof(obj_item_clothing_glasses_nightmare_vision), 1),
                Dm.kv(typeof(obj_item_clothing_glasses_welding), 1),
                Dm.kv(typeof(obj_item_clothing_gloves_tackler_offbrand), 1),
                Dm.kv(typeof(obj_item_clothing_head_helmet_old), 1),
                Dm.kv(typeof(obj_item_clothing_head_utility_welding), 1),
                Dm.kv(typeof(obj_item_clothing_mask_muzzle), 1),
                Dm.kv(typeof(obj_item_clothing_suit_armor_vest_old), 1),
                Dm.kv(typeof(obj_item_storage_belt_medical), 1),
                Dm.kv(typeof(obj_item_storage_belt_utility), 1),
                Dm.kv(typeof(obj_item_pen_screwdriver), 1)
                ), 8),
            Dm.kv(Dm.list(  //strange objects
                Dm.kv(typeof(obj_item_assembly_relic), 5)
                ), 8),
            Dm.kv(Dm.list(  //construction and crafting
                Dm.kv(typeof(obj_item_beacon), 1),
                Dm.kv(typeof(obj_item_paper_fluff_stations_soap), 1),  //recipes count as crafting.
                Dm.kv(typeof(obj_item_plaque), 1),
                Dm.kv(typeof(obj_item_seeds_kronkus), 3),
                Dm.kv(typeof(obj_item_seeds_odious_puffball), 1),
                Dm.kv(typeof(obj_item_stack_sheet_mineral_wood_fifty), 1),
                Dm.kv(typeof(obj_item_stock_parts_power_store_cell_high), 1),
                Dm.kv(typeof(obj_item_storage_box_clown), 1),
                Dm.kv(typeof(obj_item_weaponcrafting_receiver), 1),
                Dm.kv(typeof(obj_item_book_granter_crafting_recipe_death_sandwich), 1),
                Dm.kv(typeof(obj_item_survivalcapsule_fishing), 1)
                ), 8),
            Dm.kv(Dm.list(  //medical and chemicals
                Dm.kv(Dm.list(  //basic healing items
                    Dm.kv(typeof(obj_item_stack_medical_wrap_gauze), 1),
                    Dm.kv(typeof(obj_item_stack_medical_mesh), 1),
                    Dm.kv(typeof(obj_item_stack_medical_suture), 1),
                    Dm.kv(typeof(obj_item_stack_medical_bandage), 1)
                    ), 4),
                Dm.kv(Dm.list(  //medical chems
                    Dm.kv(typeof(obj_item_reagent_containers_cup_bottle_multiver), 1),
                    Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen), 1),
                    Dm.kv(typeof(obj_item_reagent_containers_syringe_convermol), 1)
                    ), 4),
                Dm.kv(Dm.list(  //drinks
                    Dm.kv(typeof(obj_item_reagent_containers_cup_glass_bottle_vodka), 1),
                    Dm.kv(typeof(obj_item_reagent_containers_cup_glass_drinkingglass_filled_nuka_cola), 1),
                    Dm.kv(typeof(obj_item_reagent_containers_cup_soda_cans_grey_bull), 1)
                    ), 4),
                Dm.kv(Dm.list(  //sprayers
                    Dm.kv(typeof(obj_item_reagent_containers_spray), 1),
                    Dm.kv(typeof(obj_item_watertank), 1),
                    Dm.kv(typeof(obj_item_watertank_janitor), 1)
                    ), 4),
                Dm.kv(Dm.list(  //other chems
                    Dm.kv(typeof(obj_item_reagent_containers_cup_jerrycan_oil), 2),  //Not the most exciting reagent, but a staple. And you get a cool can for your pyrotechnics.
                    Dm.kv(typeof(obj_item_reagent_containers_cup_jerrycan_space_cleaner), 2),  //If you can't find the janitor, the medbay might also appreciate some readymade cleaner.
                    Dm.kv(typeof(obj_item_reagent_containers_cup_jerrycan_sus), 1),  //what assitant wouldn't like a vibrant jug o' phlog?
                    Dm.kv(typeof(obj_item_reagent_containers_cup_jerrycan_diethylamine), 1),  //Im sure the botanist would appreciate it, but it is also really cool for maintenance science projects.
                    Dm.kv(typeof(obj_item_reagent_containers_cup_jerrycan_concrete_mix), 2)
                    ), 2)
                ), 8),
            Dm.kv(Dm.list(  //food
                Dm.kv(typeof(obj_item_food_canned_peaches_maint), 1),
                Dm.kv(typeof(obj_item_storage_box_donkpockets), 1),
                Dm.kv(typeof(obj_item_storage_box_gum_happiness), 1),
                Dm.kv(typeof(obj_item_storage_box_ramen_beef), 1),
                Dm.kv(Dm.list(  //Donk Varieties
                    Dm.kv(typeof(obj_item_storage_box_donkpockets_donkpocketberry), 1),
                    Dm.kv(typeof(obj_item_storage_box_donkpockets_donkpockethonk), 1),
                    Dm.kv(typeof(obj_item_storage_box_donkpockets_donkpocketpizza), 1),
                    Dm.kv(typeof(obj_item_storage_box_donkpockets_donkpocketspicy), 1),
                    Dm.kv(typeof(obj_item_storage_box_donkpockets_donkpocketteriyaki), 1)
                    ), 1),
                Dm.kv(Dm.list(  //Donk-dinner Varieties
                    Dm.kv(typeof(obj_item_food_ready_donk), 1),
                    Dm.kv(typeof(obj_item_food_ready_donk_donkhiladas), 1),
                    Dm.kv(typeof(obj_item_food_ready_donk_mac_n_cheese), 1)
                    ), 1),
                Dm.kv(typeof(obj_item_food_monkeycube), 1)
                ), 8),
            Dm.kv(Dm.list(  //Donk Co. 'Donk Spike' flechette shells, these things show up everywhere in donkpocket boxes
                Dm.kv(typeof(obj_item_storage_box_donkpockets_donkpocketshell), 1)
                ), 8),
            Dm.kv(Dm.list(  //computer disks
                Dm.kv(typeof(obj_item_disk_computer_maintenance_scanner), 1),
                Dm.kv(typeof(obj_item_disk_computer_maintenance_camera), 1),
                Dm.kv(typeof(obj_item_disk_computer_maintenance_modsuit_control), 1),
                Dm.kv(typeof(obj_item_disk_computer_maintenance_spectre_meter), 1),
                Dm.kv(typeof(obj_item_disk_computer_maintenance_arcade), 1),
                Dm.kv(typeof(obj_item_disk_design_disk_liberator), 1),
                Dm.kv(typeof(obj_item_disk_computer_maintenance_theme), 3)
                ), 4),
            Dm.kv(Dm.list(  //modsuits
                Dm.kv(typeof(obj_effect_spawner_random_mod_maint), 3),
                Dm.kv(typeof(obj_item_mod_construction_broken_core), 1)
                ), 4),
            Dm.kv(Dm.list(  //music
                Dm.kv(typeof(obj_item_instrument_accordion), 5),
                Dm.kv(typeof(obj_item_instrument_banjo), 5),
                Dm.kv(typeof(obj_item_instrument_bikehorn), 2),
                Dm.kv(typeof(obj_item_instrument_eguitar), 5),
                Dm.kv(typeof(obj_item_instrument_glockenspiel), 5),
                Dm.kv(typeof(obj_item_instrument_guitar), 5),
                Dm.kv(typeof(obj_item_instrument_harmonica), 5),
                Dm.kv(typeof(obj_item_instrument_musicalmoth), 1),
                Dm.kv(typeof(obj_item_instrument_recorder), 5),
                Dm.kv(typeof(obj_item_instrument_saxophone), 5),
                Dm.kv(typeof(obj_item_instrument_trombone), 5),
                Dm.kv(typeof(obj_item_instrument_trumpet), 5),
                Dm.kv(typeof(obj_item_instrument_violin), 5),
                Dm.kv(typeof(obj_item_instrument_violin_golden), 2)
                ), 2),
            Dm.kv(Dm.list(  //fakeout items, keep this list at low relative weight
                Dm.kv(typeof(obj_item_clothing_shoes_jackboots), 1),
                Dm.kv(typeof(obj_item_dice_d20), 1)  //To balance out the stealth die of fates in oddities
                ), 1)
        );
        Dm.append(ref gvars_datum_init_order, "uncommon_loot");
    }

    // tg: code/_globalvars/lists/maintenance_loot.dm:315  GLOBAL_LIST_INIT(rarity_loot, list(...))  //rare: really good items
    public DmList rarity_loot;

    // tg: code/_globalvars/lists/maintenance_loot.dm:315  (GLOBAL_MANAGED(rarity_loot, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalrarity_loot()
    {
        rarity_loot = Dm.list(
            Dm.kv(Dm.list(  //tools
                Dm.kv(typeof(obj_item_assembly_flash_memorizer), 1),
                Dm.kv(typeof(obj_item_flashlight_flashdark), 1),
                Dm.kv(typeof(obj_item_knife_kitchen), 1),
                Dm.kv(typeof(obj_item_melee_baton_security_cattleprod_teleprod), 1),
                Dm.kv(typeof(obj_item_pen_survival), 1),
                Dm.kv(typeof(obj_item_restraints_handcuffs), 1),
                Dm.kv(typeof(obj_item_shield_buckler), 1),
                Dm.kv(typeof(obj_item_shield_improvised), 1),
                Dm.kv(typeof(obj_item_throwing_star), 1),
                Dm.kv(typeof(obj_item_weldingtool_hugetank), 1),
                Dm.kv(typeof(obj_item_fishing_rod_telescopic_master), 1),
                Dm.kv(typeof(obj_item_spess_knife), 1),
                Dm.kv(typeof(obj_item_gun_ballistic_automatic_pistol_doohickey), 1)
                ), 1),
            Dm.kv(Dm.list(  //equipment
                Dm.kv(typeof(obj_item_clothing_glasses_hud_security), 1),
                Dm.kv(typeof(obj_item_clothing_glasses_sunglasses), 1),
                Dm.kv(typeof(obj_item_clothing_gloves_color_black_security), 1),
                Dm.kv(typeof(obj_item_clothing_gloves_color_yellow), 1),
                Dm.kv(typeof(obj_item_clothing_gloves_tackler_combat), 1),
                Dm.kv(typeof(obj_item_clothing_head_helmet_toggleable_justice), 1),
                Dm.kv(typeof(obj_item_storage_belt_military_assault), 1),
                Dm.kv(typeof(obj_item_storage_belt_security), 1)
                ), 1),
            Dm.kv(Dm.list(  //paint
                Dm.kv(typeof(obj_item_paint_anycolor), 1),
                Dm.kv(typeof(obj_item_paint_black), 1),
                Dm.kv(typeof(obj_item_paint_blue), 1),
                Dm.kv(typeof(obj_item_paint_green), 1),
                Dm.kv(typeof(obj_item_paint_paint_remover), 1),
                Dm.kv(typeof(obj_item_paint_red), 1),
                Dm.kv(typeof(obj_item_paint_violet), 1),
                Dm.kv(typeof(obj_item_paint_white), 1),
                Dm.kv(typeof(obj_item_paint_yellow), 1)
                ), 1),
            Dm.kv(Dm.list(  //medical and chemicals
                Dm.kv(Dm.list(  //medkits
                    Dm.kv(typeof(obj_item_storage_box_hug_medical), 1),
                    Dm.kv(typeof(obj_item_storage_medkit_emergency), 1),
                    Dm.kv(typeof(obj_item_storage_medkit_regular), 1),
                    Dm.kv(typeof(obj_item_storage_box_bandages), 1)
                    ), 2),
                Dm.kv(Dm.list(  //medical chems
                    Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_oxandrolone), 1),
                    Dm.kv(typeof(obj_item_reagent_containers_hypospray_medipen_salacid), 1),
                    Dm.kv(typeof(obj_item_reagent_containers_syringe_contraband_methamphetamine), 1)
                    ), 2),
                Dm.kv(Dm.list(
                    Dm.kv(typeof(obj_item_paper_secretrecipe_gorgium), 1),
                    Dm.kv(typeof(obj_item_paper_secretrecipe_metalgen), 1)
                    ), 1)
                ), 1),
            Dm.kv(Dm.list(  //misc
                Dm.kv(typeof(obj_item_book_granter_crafting_recipe_dusting_pipegun_prime), 1),
                Dm.kv(typeof(obj_item_book_granter_crafting_recipe_trash_cannon), 1),
                Dm.kv(typeof(obj_item_book_granter_crafting_recipe_dusting_laser_musket_prime), 1),
                Dm.kv(typeof(obj_item_book_granter_crafting_recipe_dusting_detached_ratvarian_repeater), 1),
                Dm.kv(typeof(obj_item_book_granter_sign_language), 1),
                Dm.kv(typeof(obj_item_disk_nuclear_fake), 1),
                Dm.kv(typeof(obj_item_disk_surgery_advanced_plastic_surgery), 1),
                Dm.kv(typeof(obj_item_skillchip_brainwashing), 1),
                Dm.kv(typeof(obj_item_skillchip_intj), 1),
                Dm.kv(typeof(obj_item_tattoo_kit), 1),
                Dm.kv(typeof(obj_item_folder_ancient_paperwork), 1)
                ), 1)
        );
        Dm.append(ref gvars_datum_init_order, "rarity_loot");
    }

    // tg: code/_globalvars/lists/maintenance_loot.dm:391  GLOBAL_LIST_INIT(oddity_loot, list(...))  //oddity: strange or crazy items
    public DmList oddity_loot;

    // tg: code/_globalvars/lists/maintenance_loot.dm:391  (GLOBAL_MANAGED(oddity_loot, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaloddity_loot()
    {
        oddity_loot = Dm.list(
            Dm.kv(typeof(obj_effect_rune_teleport), 1),
            Dm.kv(typeof(obj_item_clothing_head_helmet_abductor), 1),
            Dm.kv(typeof(obj_item_clothing_shoes_jackboots_fast), 1),
            Dm.kv(typeof(obj_item_clothing_suit_armor_reactive_table), 1),
            Dm.kv(typeof(obj_item_dice_d20_fate_stealth_cursed), 1),  //Only rolls 1
            Dm.kv(typeof(obj_item_dice_d20_fate_stealth_one_use), 1),  //Looks like a d20, keep the d20 in the uncommon pool.
            Dm.kv(typeof(obj_item_shadowcloak), 1),
            Dm.kv(typeof(obj_item_spear_grey_tide), 1),
            Dm.kv(typeof(obj_item_storage_box_donkpockets_donkpocketgondola), 1),
            Dm.kv(Dm.list(  //music
                Dm.kv(typeof(obj_item_instrument_saxophone_spectral), 1),
                Dm.kv(typeof(obj_item_instrument_trombone_spectral), 1),
                Dm.kv(typeof(obj_item_instrument_trumpet_spectral), 1)
                ), 1),
            Dm.kv(typeof(obj_item_toy_cards_deck_tarot_haunted), 1)
        );
        Dm.append(ref gvars_datum_init_order, "oddity_loot");
    }

    // tg: code/_globalvars/lists/maintenance_loot.dm:418  GLOBAL_LIST_INIT(maintenance_loot, list(...))
    public DmList maintenance_loot;

    // tg: code/_globalvars/lists/maintenance_loot.dm:418  (GLOBAL_MANAGED(maintenance_loot, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmaintenance_loot()
    {
        maintenance_loot = Dm.list(
            Dm.kv(GLOB.trash_loot, maint_trash_weight),
            Dm.kv(GLOB.common_loot, maint_common_weight),
            Dm.kv(GLOB.uncommon_loot, maint_uncommon_weight),
            Dm.kv(GLOB.rarity_loot, maint_rarity_weight),
            Dm.kv(GLOB.oddity_loot, maint_oddity_weight)
        );
        Dm.append(ref gvars_datum_init_order, "maintenance_loot");
    }

    // tg: code/_globalvars/lists/maintenance_loot.dm:427  GLOBAL_LIST_INIT(dumpster_loot, GLOB.maintenance_loot.Copy())
    public DmList dumpster_loot;

    // tg: code/_globalvars/lists/maintenance_loot.dm:427  (GLOBAL_MANAGED(dumpster_loot, GLOB.maintenance_loot.Copy()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldumpster_loot()
    {
        dumpster_loot = GLOB.maintenance_loot.Copy();
        Dm.append(ref gvars_datum_init_order, "dumpster_loot");
    }

    // tg: code/_globalvars/lists/maintenance_loot.dm:429  GLOBAL_LIST_INIT(ratking_trash, list(...))  //Garbage: used by the regal rat mob when spawning garbage.
    public DmList ratking_trash;

    // tg: code/_globalvars/lists/maintenance_loot.dm:429  (GLOBAL_MANAGED(ratking_trash, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalratking_trash()
    {
        ratking_trash = Dm.list(
            typeof(obj_item_cigbutt),
            typeof(obj_item_trash_candy),
            typeof(obj_item_trash_cheesie),
            typeof(obj_item_trash_chips),
            typeof(obj_item_trash_pistachios),
            typeof(obj_item_trash_popcorn),
            typeof(obj_item_trash_raisins),
            typeof(obj_item_trash_sosjerky),
            typeof(obj_item_trash_syndi_cakes)
        );
        Dm.append(ref gvars_datum_init_order, "ratking_trash");
    }

    // tg: code/_globalvars/lists/maintenance_loot.dm:441  GLOBAL_LIST_INIT(ratking_coins, list(...))  //Coins: Used by the regal rat mob when spawning coins.
    public DmList ratking_coins;

    // tg: code/_globalvars/lists/maintenance_loot.dm:441  (GLOBAL_MANAGED(ratking_coins, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalratking_coins()
    {
        ratking_coins = Dm.list(
            typeof(obj_item_coin_iron),
            typeof(obj_item_coin_plastic),
            typeof(obj_item_coin_silver),
            typeof(obj_item_coin_titanium)
        );
        Dm.append(ref gvars_datum_init_order, "ratking_coins");
    }
}

//Maintenance loot spawner pools
public static partial class GlobalProcs
{
    // tg: code/_globalvars/lists/maintenance_loot.dm:410  #define maint_trash_weight 4500
    public const int maint_trash_weight = 4500;
    // tg: code/_globalvars/lists/maintenance_loot.dm:411  #define maint_common_weight 4500
    public const int maint_common_weight = 4500;
    // tg: code/_globalvars/lists/maintenance_loot.dm:412  #define maint_uncommon_weight 900
    public const int maint_uncommon_weight = 900;
    // tg: code/_globalvars/lists/maintenance_loot.dm:413  #define maint_rarity_weight 99
    public const int maint_rarity_weight = 99;
    // tg: code/_globalvars/lists/maintenance_loot.dm:414  #define maint_oddity_weight 1  //1 out of 10,000 would give metastation (180 spawns) a 2 in 111 chance of spawning an oddity per round, similar to xeno egg
    public const int maint_oddity_weight = 1;
    // tg: code/_globalvars/lists/maintenance_loot.dm:415  #define maint_holiday_weight 3500  // When holiday loot is enabled, it'll give every loot item a 25% chance of being a holiday item
    public const int maint_holiday_weight = 3500;
}

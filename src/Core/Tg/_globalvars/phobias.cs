// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/phobias.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3b, D316: the phobias of the brain traumas and the fear system; the bananium nuke's clown phobia). The
// type lists are tg's, one typeof per path.

public partial class datum_controller_global_vars
{
    /// Phobia types that can be pulled randomly for brain traumas.
    /// Also determines what phobias you can choose as your preference with the quirk.
    // tg: code/_globalvars/phobias.dm:5  GLOBAL_LIST_INIT(phobia_types, list(...))
    public DmList phobia_types;

    // tg: code/_globalvars/phobias.dm:5  (GLOBAL_MANAGED(phobia_types, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalphobia_types()
    {
        phobia_types = Dm.list(
            Dm.kv("aliens", "Astroxenophobia"),
            Dm.kv("anime", "Nihongophobia"),
            Dm.kv("authority", "Authoritophobia"),
            Dm.kv("birds", "Ornithophobia"),
            Dm.kv("blood", "Hemophobia"),
            Dm.kv("clowns", "Coulrophobia"),
            Dm.kv("doctors", "Iatrophobia"),
            Dm.kv("falling", "Basiphobia"),
            Dm.kv("fish", "Ichthyophobia"),
            Dm.kv("greytide", "Glaucophobia"),
            Dm.kv("guns", "Ballistophobia"),
            Dm.kv("insects", "Entomophobia"),
            Dm.kv("lizards", "Herpetophobia"),
            Dm.kv("robots", "Automatonophobia"),
            Dm.kv("security", "Scopophobia"),
            Dm.kv("skeletons", "Osteophobia"),
            Dm.kv("snakes", "Ophidiophobia"),
            Dm.kv("space", "Astrophobia"),
            Dm.kv("spiders", "Arachnophobia"),
            Dm.kv("strangers", "Xenophobia"),
            Dm.kv("the supernatural", "Phasmophobia"));
        Dm.append(ref gvars_datum_init_order, "phobia_types");
    }

    // tg: code/_globalvars/phobias.dm:29  GLOBAL_LIST_INIT(phobia_regexes, list(...))
    public DmList phobia_regexes;

    // tg: code/_globalvars/phobias.dm:29  (GLOBAL_MANAGED(phobia_regexes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalphobia_regexes()
    {
        phobia_regexes = Dm.list(
            Dm.kv("aliens", construct_phobia_regex("aliens")),
            Dm.kv("anime", construct_phobia_regex("anime")),
            Dm.kv("authority", construct_phobia_regex("authority")),
            Dm.kv("birds", construct_phobia_regex("birds")),
            Dm.kv("blood", construct_phobia_regex("blood")),
            Dm.kv("clowns", construct_phobia_regex("clowns")),
            Dm.kv("conspiracies", construct_phobia_regex("conspiracies")),
            Dm.kv("doctors", construct_phobia_regex("doctors")),
            Dm.kv("falling", construct_phobia_regex("falling")),
            Dm.kv("fish", construct_phobia_regex("fish")),
            Dm.kv("greytide", construct_phobia_regex("greytide")),
            Dm.kv("guns", construct_phobia_regex("guns")),
            Dm.kv("insects", construct_phobia_regex("insects")),
            Dm.kv("lizards", construct_phobia_regex("lizards")),
            Dm.kv("ocky icky", construct_phobia_regex("ocky icky")),
            Dm.kv("robots", construct_phobia_regex("robots")),
            Dm.kv("security", construct_phobia_regex("security")),
            Dm.kv("skeletons", construct_phobia_regex("skeletons")),
            Dm.kv("snakes", construct_phobia_regex("snakes")),
            Dm.kv("space", construct_phobia_regex("space")),
            Dm.kv("spiders", construct_phobia_regex("spiders")),
            Dm.kv("strangers", construct_phobia_regex("strangers")),
            Dm.kv("the supernatural", construct_phobia_regex("the supernatural")));
        Dm.append(ref gvars_datum_init_order, "phobia_regexes");
    }

    // tg: code/_globalvars/phobias.dm:55  GLOBAL_LIST_INIT(phobia_mobs, list(...))
    public DmList phobia_mobs;

    // tg: code/_globalvars/phobias.dm:55  (GLOBAL_MANAGED(phobia_mobs, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalphobia_mobs()
    {
        phobia_mobs = Dm.list(
            Dm.kv("aliens", typecacheof(Dm.list(typeof(mob_living_basic_alien), typeof(mob_living_basic_slime), typeof(mob_living_carbon_alien)))),
            Dm.kv("anime", typecacheof(Dm.list(typeof(mob_living_basic_guardian), typeof(mob_living_basic_migo_hatsune)))),
            Dm.kv("authority", typecacheof(Dm.list(typeof(mob_living_basic_trooper_nanotrasen), typeof(mob_living_basic_bot_secbot)))),
            Dm.kv("birds", typecacheof(Dm.list(
                typeof(mob_living_basic_chick), typeof(mob_living_basic_chicken), typeof(mob_living_basic_goose),
                typeof(mob_living_basic_parrot), typeof(mob_living_basic_pet_penguin), typeof(mob_living_basic_raptor)))),
            Dm.kv("conspiracies", typecacheof(Dm.list(typeof(mob_living_basic_drone), typeof(mob_living_basic_pet_penguin), typeof(mob_living_basic_bot_secbot)))),
            Dm.kv("doctors", typecacheof(Dm.list(typeof(mob_living_basic_bot_medbot)))),
            Dm.kv("fish", typecacheof(Dm.list(typeof(mob_living_basic_carp), typeof(mob_living_basic_space_dragon)))),
            Dm.kv("insects", typecacheof(Dm.list(
                typeof(mob_living_basic_ant), typeof(mob_living_basic_bee), typeof(mob_living_basic_butterfly),
                typeof(mob_living_basic_cockroach), typeof(mob_living_basic_mega_arachnid), typeof(mob_living_basic_mothroach)))),
            Dm.kv("lizards", typecacheof(Dm.list(typeof(mob_living_basic_lizard)))),
            Dm.kv("robots", typecacheof(Dm.list(
                typeof(mob_living_basic_bot), typeof(mob_living_basic_drone), typeof(mob_living_silicon_ai),
                typeof(mob_living_silicon_robot)))),
            Dm.kv("security", typecacheof(Dm.list(typeof(mob_living_basic_bot_secbot)))),
            Dm.kv("spiders", typecacheof(Dm.list(typeof(mob_living_basic_flesh_spider), typeof(mob_living_basic_mega_arachnid), typeof(mob_living_basic_spider_giant)))),
            Dm.kv("skeletons", typecacheof(Dm.list(typeof(mob_living_basic_skeleton)))),
            Dm.kv("snakes", typecacheof(Dm.list(typeof(mob_living_basic_snake)))),
            Dm.kv("space", typecacheof(Dm.list(typeof(mob_living_basic_voidwalker)))),
            Dm.kv("the supernatural", typecacheof(Dm.list(
                typeof(mob_dead_observer), typeof(mob_living_basic_bat), typeof(mob_living_basic_construct),
                typeof(mob_living_basic_dark_wizard), typeof(mob_living_basic_demon), typeof(mob_living_basic_faithless),
                typeof(mob_living_basic_ghost), typeof(mob_living_basic_heretic_summon), typeof(mob_living_basic_revenant),
                typeof(mob_living_basic_shade), typeof(mob_living_basic_skeleton), typeof(mob_living_basic_wizard),
                typeof(mob_living_basic_zombie), typeof(mob_living_basic_bot_mulebot_paranormal), typeof(mob_living_basic_voidwalker)))));
        Dm.append(ref gvars_datum_init_order, "phobia_mobs");
    }

    // tg: code/_globalvars/phobias.dm:130  GLOBAL_LIST_INIT(phobia_objs, list(...))
    public DmList phobia_objs;

    // tg: code/_globalvars/phobias.dm:130  (GLOBAL_MANAGED(phobia_objs, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalphobia_objs()
    {
        phobia_objs = Dm.list(
            Dm.kv("aliens", typecacheof(Dm.list(
                typeof(obj_item_abductor), typeof(obj_item_cautery_alien), typeof(obj_item_circular_saw_alien),
                typeof(obj_item_clothing_head_helmet_abductor), typeof(obj_item_clothing_mask_facehugger),
                typeof(obj_item_clothing_suit_armor_abductor), typeof(obj_item_crowbar_abductor), typeof(obj_item_gun_energy_alien),
                typeof(obj_item_hemostat_alien), typeof(obj_item_melee_baton_abductor), typeof(obj_item_organ_body_egg_alien_embryo),
                typeof(obj_item_organ_heart_gland), typeof(obj_item_radio_headset_abductor), typeof(obj_item_retractor_alien),
                typeof(obj_item_scalpel_alien), typeof(obj_item_screwdriver_abductor), typeof(obj_item_stack_sheet_mineral_abductor),
                typeof(obj_item_surgicaldrill_alien), typeof(obj_item_toy_toy_xeno), typeof(obj_item_toy_plush_abductor),
                typeof(obj_item_toy_plush_abductor_agent), typeof(obj_item_toy_plush_rouny), typeof(obj_item_weldingtool_abductor),
                typeof(obj_item_wirecutters_abductor), typeof(obj_item_wrench_abductor), typeof(obj_machinery_abductor),
                typeof(obj_structure_alien), typeof(obj_structure_bed_abductor), typeof(obj_structure_closet_abductor),
                typeof(obj_structure_statue_plasma_xeno), typeof(obj_structure_table_frame_abductor),
                typeof(obj_structure_table_abductor), typeof(obj_structure_table_optable_abductor)))),
            Dm.kv("anime", typecacheof(Dm.list(
                typeof(obj_item_clothing_head_costume_kitty_genuine), typeof(obj_item_clothing_mask_gas_ninja),
                typeof(obj_item_clothing_under_syndicate_ninja), typeof(obj_item_clothing_under_costume_seifuku),
                typeof(obj_item_energy_katana), typeof(obj_item_food_chawanmushi), typeof(obj_item_food_sashimi),
                typeof(obj_item_highfrequencyblade), typeof(obj_item_katana), typeof(obj_item_storage_belt_sheath_katana),
                typeof(obj_item_storage_belt_sheath_ninja), typeof(obj_item_storage_belt_sheath_hanzo_katana),
                typeof(obj_item_nullrod_claymore_katana), typeof(obj_item_nullrod_vibro),
                typeof(obj_item_reagent_containers_cup_glass_bottle_sake), typeof(obj_item_throwing_star), typeof(obj_item_toy_katana),
                typeof(obj_structure_mineral_door_paperframe), typeof(obj_structure_window_paperframe)))),
            Dm.kv("authority", typecacheof(Dm.list(
                typeof(obj_item_card_id_advanced_centcom), typeof(obj_item_card_id_advanced_gold),
                typeof(obj_item_card_id_advanced_silver), typeof(obj_item_clothing_under_rank_captain),
                typeof(obj_item_clothing_under_rank_centcom_commander), typeof(obj_item_clothing_under_rank_centcom_officer),
                typeof(obj_item_clothing_under_rank_civilian_head_of_personnel),
                typeof(obj_item_clothing_under_rank_engineering_chief_engineer),
                typeof(obj_item_clothing_under_rank_medical_chief_medical_officer),
                typeof(obj_item_clothing_under_rank_rnd_research_director),
                typeof(obj_item_clothing_under_rank_security_head_of_security), typeof(obj_item_clothing_under_rank_cargo_qm),
                typeof(obj_item_clothing_neck_cloak_qm), typeof(obj_item_clothing_neck_cloak_hos),
                typeof(obj_item_clothing_neck_cloak_cmo), typeof(obj_item_clothing_neck_cloak_rd),
                typeof(obj_item_clothing_neck_cloak_ce), typeof(obj_item_clothing_neck_cloak_cap),
                typeof(obj_item_clothing_neck_cloak_hop), typeof(obj_item_clothing_suit_hooded_wintercoat_captain),
                typeof(obj_item_clothing_head_hooded_winterhood_engineering_ce),
                typeof(obj_item_clothing_suit_hooded_wintercoat_science_rd), typeof(obj_item_clothing_suit_hooded_wintercoat_hop),
                typeof(obj_item_clothing_head_hooded_winterhood_medical_cmo),
                typeof(obj_item_clothing_suit_hooded_wintercoat_cargo_qm), typeof(obj_item_megaphone_command),
                typeof(obj_item_encryptionkey_heads), typeof(obj_item_radio_headset_heads), typeof(obj_item_melee_baton_telescopic),
                typeof(obj_item_stamp_centcom), typeof(obj_item_stamp_head), typeof(obj_item_toy_figure_captain),
                typeof(obj_item_toy_figure_ce), typeof(obj_item_toy_figure_cmo), typeof(obj_item_toy_figure_dsquad),
                typeof(obj_item_toy_figure_hop), typeof(obj_item_toy_figure_hos), typeof(obj_item_toy_figure_rd),
                typeof(obj_item_toy_figure_qm), typeof(obj_machinery_door_airlock_command),
                typeof(obj_structure_statue_diamond_captain), typeof(obj_structure_statue_gold_ce),
                typeof(obj_structure_statue_gold_cmo), typeof(obj_structure_statue_gold_hop), typeof(obj_structure_statue_gold_hos),
                typeof(obj_structure_statue_gold_rd), typeof(obj_item_bedsheet_captain), typeof(obj_item_bedsheet_rd),
                typeof(obj_item_bedsheet_cmo), typeof(obj_item_bedsheet_hos), typeof(obj_item_bedsheet_hop),
                typeof(obj_item_bedsheet_ce), typeof(obj_item_bedsheet_qm)))),
            Dm.kv("birds", typecacheof(Dm.list(
                typeof(obj_item_clothing_head_costume_chicken), typeof(obj_item_clothing_head_costume_griffin),
                typeof(obj_item_clothing_head_helmet_space_freedom), typeof(obj_item_clothing_mask_gas_owl_mask),
                typeof(obj_item_clothing_mask_gas_plaguedoctor), typeof(obj_item_clothing_shoes_griffin),
                typeof(obj_item_clothing_suit_costume_chickensuit), typeof(obj_item_clothing_suit_space_freedom),
                typeof(obj_item_clothing_suit_toggle_owlwings), typeof(obj_item_clothing_under_costume_griffin),
                typeof(obj_item_clothing_under_costume_owl), typeof(obj_item_food_cracker), typeof(obj_item_food_egg)))),
            Dm.kv("blood", typecacheof(Dm.list(
                typeof(obj_effect_decal_cleanable_blood), typeof(obj_item_food_fried_blood_sausage),
                typeof(obj_item_food_tiziran_sausage), typeof(obj_item_reagent_containers_blood),
                typeof(obj_item_reagent_containers_syringe), typeof(obj_machinery_iv_drip)))),
            Dm.kv("clowns", typecacheof(Dm.list(
                typeof(obj_item_bedsheet_clown), typeof(obj_item_clothing_head_chaplain_clownmitre),
                typeof(obj_item_clothing_mask_gas_clown_hat), typeof(obj_item_clothing_shoes_clown_shoes),
                typeof(obj_item_clothing_suit_chaplainsuit_clownpriest), typeof(obj_item_clothing_head_helmet_space_plasmaman_clown),
                typeof(obj_item_clothing_under_plasmaman_clown), typeof(obj_item_clothing_gloves_color_plasmaman_clown),
                typeof(obj_item_clothing_under_rank_civilian_clown), typeof(obj_item_food_burger_clown),
                typeof(obj_item_food_cheesiehonkers), typeof(obj_item_food_meatclown), typeof(obj_item_food_pie_cream),
                typeof(obj_item_grown_bananapeel), typeof(obj_item_gun_magic_staff_honk), typeof(obj_item_instrument_bikehorn),
                typeof(obj_item_megaphone_clown), typeof(obj_item_modular_computer_pda_crew_clown),
                typeof(obj_item_pneumatic_cannon_pie), typeof(obj_item_stack_ore_bananium),
                typeof(obj_item_stack_tile_mineral_bananium), typeof(obj_item_stamp_clown), typeof(obj_item_storage_backpack_clown),
                typeof(obj_item_storage_backpack_duffelbag_clown), typeof(obj_item_storage_backpack_ert_clown),
                typeof(obj_item_toy_crayon_rainbow), typeof(obj_item_toy_mecha_darkhonk), typeof(obj_item_toy_mecha_honk),
                typeof(obj_item_trash_cheesie), typeof(obj_machinery_door_airlock_bananium), typeof(obj_structure_falsewall_bananium),
                typeof(obj_structure_mecha_wreckage_honker), typeof(obj_structure_statue_bananium),
                typeof(obj_vehicle_sealed_car_clowncar), typeof(obj_vehicle_sealed_mecha_honker)))),
            Dm.kv("conspiracies", typecacheof(Dm.list(
                typeof(obj_item_abductor_gizmo), typeof(obj_item_abductor_silencer), typeof(obj_item_clothing_head_helmet_abductor),
                typeof(obj_item_clothing_suit_armor_abductor_vest), typeof(obj_item_clothing_under_rank_captain),
                typeof(obj_item_clothing_under_rank_captain_parade), typeof(obj_item_clothing_under_rank_centcom_officer),
                typeof(obj_item_clothing_under_rank_civilian_head_of_personnel),
                typeof(obj_item_clothing_under_rank_engineering_chief_engineer),
                typeof(obj_item_clothing_under_rank_medical_chief_medical_officer),
                typeof(obj_item_clothing_under_rank_rnd_research_director),
                typeof(obj_item_clothing_under_rank_rnd_research_director_alt),
                typeof(obj_item_clothing_under_rank_rnd_research_director_turtleneck),
                typeof(obj_item_clothing_under_rank_security_head_of_security),
                typeof(obj_item_clothing_under_rank_security_head_of_security_alt),
                typeof(obj_item_clothing_under_rank_security_head_of_security_grey),
                typeof(obj_item_clothing_under_rank_security_head_of_security_parade),
                typeof(obj_item_clothing_under_rank_security_head_of_security_parade_female),
                typeof(obj_item_clothing_under_rank_cargo_qm), typeof(obj_item_gun_energy_alien), typeof(obj_item_encryptionkey_heads),
                typeof(obj_item_radio_headset_heads), typeof(obj_item_megaphone_command), typeof(obj_item_melee_baton_abductor),
                typeof(obj_item_stamp_centcom), typeof(obj_item_stamp_head), typeof(obj_item_storage_belt_military_abductor),
                typeof(obj_item_toy_figure_captain), typeof(obj_item_toy_figure_ce), typeof(obj_item_toy_figure_cmo),
                typeof(obj_item_toy_figure_dsquad), typeof(obj_item_toy_figure_hop), typeof(obj_item_toy_figure_hos),
                typeof(obj_item_toy_figure_rd), typeof(obj_item_toy_figure_qm), typeof(obj_item_toy_plush_abductor),
                typeof(obj_item_toy_plush_abductor_agent), typeof(obj_machinery_atmospherics_miner),
                typeof(obj_machinery_door_airlock_centcom), typeof(obj_item_clothing_mask_madness_mask),
                typeof(obj_item_wallframe_painting_eldritch), typeof(obj_item_clothing_neck_moon_amulet),
                typeof(obj_item_melee_sickly_blade_moon)))),
            Dm.kv("doctors", typecacheof(Dm.list(
                typeof(obj_item_cautery), typeof(obj_item_circular_saw), typeof(obj_item_clothing_head_bio_hood_plague),
                typeof(obj_item_clothing_mask_gas_plaguedoctor), typeof(obj_item_clothing_suit_bio_suit_plaguedoctorsuit),
                typeof(obj_item_clothing_under_rank_medical), typeof(obj_item_healthanalyzer), typeof(obj_item_autopsy_scanner),
                typeof(obj_item_hemostat), typeof(obj_item_reagent_containers_hypospray),
                typeof(obj_item_reagent_containers_applicator), typeof(obj_item_reagent_containers_syringe),
                typeof(obj_item_retractor), typeof(obj_item_scalpel), typeof(obj_item_storage_medkit),
                typeof(obj_item_storage_pill_bottle), typeof(obj_item_surgical_drapes), typeof(obj_item_surgicaldrill),
                typeof(obj_machinery_cryo_cell), typeof(obj_machinery_dna_scannernew), typeof(obj_machinery_door_airlock_medical),
                typeof(obj_machinery_sleeper), typeof(obj_machinery_stasis), typeof(obj_structure_sign_departments_medbay)))),
            Dm.kv("fish", typecacheof(Dm.list(
                typeof(obj_item_clothing_head_hooded_carp_hood), typeof(obj_item_clothing_suit_hooded_carp_costume),
                typeof(obj_item_clothing_head_fedora_carpskin), typeof(obj_item_clothing_mask_gas_carp),
                typeof(obj_item_clothing_head_soft_fishing_hat), typeof(obj_item_cigarette_carp),
                typeof(obj_item_clothing_under_suit_carpskin), typeof(obj_item_fish), typeof(obj_item_fishing_hook),
                typeof(obj_item_fishing_rod), typeof(obj_item_food_cubancarp), typeof(obj_item_food_fishandchips),
                typeof(obj_item_food_fishfingers), typeof(obj_item_food_fishfry), typeof(obj_item_food_fishmeat),
                typeof(obj_item_food_nugget_fish), typeof(obj_item_food_volt_fish),
                typeof(obj_item_grenade_clusterbuster_spawner_spesscarp), typeof(obj_item_grenade_spawnergrenade_spesscarp),
                typeof(obj_item_knife_carp), typeof(obj_item_organ_brain_carp), typeof(obj_item_organ_heart_carp),
                typeof(obj_item_organ_liver_fish), typeof(obj_item_organ_lungs_carp), typeof(obj_item_organ_lungs_fish),
                typeof(obj_item_organ_stomach_fish), typeof(obj_item_organ_tail_fish), typeof(obj_item_organ_tongue_carp),
                typeof(obj_item_organ_fangs_carp), typeof(obj_item_storage_fancy_cigarettes_cigpack_carp),
                typeof(obj_item_storage_toolbox_fishing), typeof(obj_item_stack_sheet_animalhide_carp),
                typeof(obj_item_toy_plush_carpplushie), typeof(obj_item_storage_bag_fishing)))),
            // only the gas mask itself, not its subtypes (tg's comment at :415)
            Dm.kv("greytide", Dm.add(typecacheof(Dm.list(
                typeof(obj_item_clothing_under_color_grey), typeof(obj_item_melee_baton_security_cattleprod), typeof(obj_item_spear),
                typeof(obj_item_toy_figure_assistant), typeof(obj_structure_statue_sandstone_assistant))), typecacheof(Dm.list(typeof(obj_item_clothing_mask_gas)), only_root_path: TRUE, ignore_root_path: FALSE))),
            Dm.kv("guns", typecacheof(Dm.list(
                typeof(obj_item_ammo_box), typeof(obj_item_ammo_casing), typeof(obj_item_gun_ballistic),
                typeof(obj_item_gun_blastcannon), typeof(obj_item_gun_chem), typeof(obj_item_gun_energy),
                typeof(obj_item_gun_grenadelauncher), typeof(obj_item_gun_syringe), typeof(obj_item_mecha_ammo),
                typeof(obj_item_mecha_parts_mecha_equipment_weapon_ballistic),
                typeof(obj_item_mecha_parts_mecha_equipment_weapon_energy), typeof(obj_item_storage_belt_bandolier),
                typeof(obj_item_storage_belt_holster), typeof(obj_machinery_porta_turret), typeof(obj_machinery_power_emitter)))),
            Dm.kv("insects", typecacheof(Dm.list(
                typeof(obj_item_clothing_mask_animal_small_bee), typeof(obj_item_clothing_suit_hooded_bee_costume),
                typeof(obj_item_toy_plush_beeplushie), typeof(obj_item_toy_plush_moth), typeof(obj_structure_beebox)))),
            Dm.kv("lizards", typecacheof(Dm.list(
                typeof(obj_item_clothing_head_costume_lizard), typeof(obj_item_clothing_shoes_cowboy_lizard),
                typeof(obj_item_food_kebab_tail), typeof(obj_item_organ_tail_lizard),
                typeof(obj_item_reagent_containers_cup_glass_bottle_lizardwine), typeof(obj_item_toy_plush_lizard_plushie)))),
            Dm.kv("ocky icky", typecacheof(Dm.list(typeof(obj_item_banhammer), typeof(obj_item_toy_plush_awakenedplushie)))),
            Dm.kv("robots", typecacheof(Dm.list(
                typeof(obj_item_ai_module), typeof(obj_item_aicard), typeof(obj_item_brain_processor_positronic),
                typeof(obj_item_toy_figure_borg), typeof(obj_item_toy_talking_ai), typeof(obj_machinery_ai_law_rack),
                typeof(obj_machinery_recharge_station), typeof(obj_structure_statue_diamond_ai1),
                typeof(obj_structure_statue_diamond_ai2), typeof(obj_structure_statue_silver_medborg),
                typeof(obj_structure_statue_silver_secborg)))),
            Dm.kv("security", typecacheof(Dm.list(
                typeof(obj_effect_client_image_holder_securitron), typeof(obj_item_clothing_under_rank_security_detective),
                typeof(obj_item_clothing_under_rank_security_head_of_security), typeof(obj_item_clothing_under_rank_security_officer),
                typeof(obj_item_clothing_under_rank_security_warden), typeof(obj_item_gun_energy_taser),
                typeof(obj_item_megaphone_sec), typeof(obj_item_melee_baton), typeof(obj_item_restraints_handcuffs),
                typeof(obj_item_stamp_head_hos), typeof(obj_item_toy_figure_hos), typeof(obj_item_toy_figure_secofficer),
                typeof(obj_machinery_door_airlock_security), typeof(obj_structure_statue_gold_hos),
                typeof(obj_structure_statue_silver_sec), typeof(obj_structure_statue_silver_secborg)))),
            Dm.kv("skeletons", typecacheof(Dm.list(
                typeof(obj_effect_decal_remains_human), typeof(obj_item_clothing_suit_armor_bone), typeof(obj_item_dog_bone),
                typeof(obj_item_food_meat_slab_human_mutant_skeleton), typeof(obj_item_organ_tongue_bone),
                typeof(obj_item_stack_sheet_bone)))),
            Dm.kv("snakes", typecacheof(Dm.list(typeof(obj_item_rod_of_asclepius), typeof(obj_item_toy_plush_snakeplushie)))),
            Dm.kv("spiders", typecacheof(Dm.list(typeof(obj_structure_spider)))),
            Dm.kv("the supernatural", typecacheof(Dm.list(
                typeof(obj_effect_floating_blade), typeof(obj_effect_heretic_influence), typeof(obj_effect_heretic_rune),
                typeof(obj_effect_rune), typeof(obj_effect_visible_heretic_influence), typeof(obj_item_clothing_head_wizard),
                typeof(obj_item_clothing_mask_madness_mask), typeof(obj_item_clothing_neck_heretic_focus),
                typeof(obj_item_clothing_neck_eldritch_amulet), typeof(obj_item_clothing_suit_hooded_cultrobes),
                typeof(obj_item_clothing_suit_wizrobe), typeof(obj_item_clothing_under_rank_civilian_chaplain),
                typeof(obj_item_codex_cicatrix), typeof(obj_item_gun_magic), typeof(obj_item_melee_cultblade),
                typeof(obj_item_melee_rune_carver), typeof(obj_item_melee_sickly_blade), typeof(obj_item_necromantic_stone),
                typeof(obj_item_nullrod), typeof(obj_item_restraints_legcuffs_bola_cult), typeof(obj_item_scrying),
                typeof(obj_item_soulstone), typeof(obj_item_spellbook), typeof(obj_item_stack_sheet_hauntium),
                typeof(obj_item_stack_sheet_runed_metal), typeof(obj_item_staff), typeof(obj_item_storage_toolbox_haunted),
                typeof(obj_item_tome), typeof(obj_item_toy_cards_deck_tarot), typeof(obj_item_toy_eightball_haunted),
                typeof(obj_item_toy_eldritch_book), typeof(obj_item_toy_reality_pierce), typeof(obj_item_warp_whistle),
                typeof(obj_machinery_door_airlock_cult), typeof(obj_narsie), typeof(obj_structure_destructible_cult),
                typeof(obj_structure_destructible_eldritch_crucible), typeof(obj_structure_spirit_board)))));
        Dm.append(ref gvars_datum_init_order, "phobia_objs");
    }

    // tg: code/_globalvars/phobias.dm:539  GLOBAL_LIST_INIT(phobia_turfs, list(...))
    public DmList phobia_turfs;

    // tg: code/_globalvars/phobias.dm:539  (GLOBAL_MANAGED(phobia_turfs, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalphobia_turfs()
    {
        phobia_turfs = Dm.list(
            Dm.kv("aliens", typecacheof(Dm.list(
                typeof(turf_closed_wall_mineral_abductor), typeof(turf_open_floor_mineral_abductor),
                typeof(turf_open_floor_plating_abductor), typeof(turf_open_floor_plating_abductor2)))),
            Dm.kv("falling", typecacheof(Dm.list(typeof(turf_open_chasm), typeof(turf_open_floor_fakepit), typeof(turf_open_openspace)))),
            Dm.kv("space", typecacheof(Dm.list(typeof(turf_open_floor_fakespace), typeof(turf_open_floor_holofloor_space), typeof(turf_open_space)))),
            Dm.kv("the supernatural", typecacheof(Dm.list(typeof(turf_closed_wall_mineral_cult), typeof(turf_open_floor_cult)))));
        Dm.append(ref gvars_datum_init_order, "phobia_turfs");
    }

    // tg: code/_globalvars/phobias.dm:562  GLOBAL_LIST_INIT(phobia_species, list(...))
    public DmList phobia_species;

    // tg: code/_globalvars/phobias.dm:562  (GLOBAL_MANAGED(phobia_species, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalphobia_species()
    {
        phobia_species = Dm.list(
            Dm.kv("aliens", typecacheof(Dm.list(
                typeof(datum_species_abductor), typeof(datum_species_jelly), typeof(datum_species_pod), typeof(datum_species_shadow)))),
            Dm.kv("anime", typecacheof(Dm.list(typeof(datum_species_human_felinid)))),
            Dm.kv("conspiracies", typecacheof(Dm.list(typeof(datum_species_abductor), typeof(datum_species_lizard)))),
            Dm.kv("insects", typecacheof(Dm.list(typeof(datum_species_fly), typeof(datum_species_moth)))),
            Dm.kv("lizards", typecacheof(Dm.list(typeof(datum_species_lizard)))),
            Dm.kv("robots", typecacheof(Dm.list(typeof(datum_species_android)))),
            Dm.kv("skeletons", typecacheof(Dm.list(typeof(datum_species_plasmaman), typeof(datum_species_skeleton)))));
        Dm.append(ref gvars_datum_init_order, "phobia_species");
    }

}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/phobias.dm:1
    private const string PHOBIA_FILE = "phobia.json";

    /// Creates a regular expression to match against the given phobia
    /// Capture group 2 = the scary word
    /// Capture group 3 = an optional suffix on the scary word
    // tg: code/_globalvars/phobias.dm:589
    public static object construct_phobia_regex(object name = null)
    {
        // The host mounts tg's strings/ files before the world boots (TgStrings.Mount: the server, ToyScenario, TgTestWorld); a bare world
        // that mounts none (the lighter test harnesses) has no phobia words, and its phobias match no word. tg always has the file, where
        // strings() would CRASH on its absence and abort GLOB's init.
        if (!Dm.truthy(fexists(Dm.text("[]/[]", STRING_DIRECTORY, PHOBIA_FILE))))
            return null;
        DmList words = strings(PHOBIA_FILE, name) as DmList;
        if (!Dm.truthy(length(words)))
            throw CRASH(Dm.text("phobia [] has no entries", name), "code/_globalvars/phobias.dm", 592);
        string words_match = "";
        foreach (object word in Dm.each(words))
            words_match += Dm.text("[]|", REGEX_QUOTE(word));
        words_match = copytext(words_match, 1, -1);
        return Dm.@new<regex>(Dm.text("(\\b|\\A)([])('?s*)(\\b|\\|)", words_match), "i");
    }
}

// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/uplink_kits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3): the syndicate boxes and kits the uplink sells, the two random syndi-kits, the induction kits and
// the nuclear operative implant they carry. Many kits hold items of later packages (the stealth gear, the job kits, the nuclear
// operatives' arsenal; the MODsuits' modules came with gaps MO2): such an item is created inert, and its kit's uplink entry waits in
// UplinkWaits (uplink_items.cs) for the package that completes it. The var blocks are generated (D111).

/// last audited december 2022
// tg: code/game/objects/items/storage/uplink_kits.dm:28
public partial class obj_item_storage_box_syndicate : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:1  #define KIT_RECON "recon"
    public const string KIT_RECON = "recon";
    // tg: code/game/objects/items/storage/uplink_kits.dm:2  #define KIT_BLOODY_SPAI "bloodyspai"
    public const string KIT_BLOODY_SPAI = "bloodyspai";
    // tg: code/game/objects/items/storage/uplink_kits.dm:3  #define KIT_STEALTHY "stealth"
    public const string KIT_STEALTHY = "stealth";
    // tg: code/game/objects/items/storage/uplink_kits.dm:4  #define KIT_SCREWED "screwed"
    public const string KIT_SCREWED = "screwed";
    // tg: code/game/objects/items/storage/uplink_kits.dm:5  #define KIT_SABOTAGE "sabotage"
    public const string KIT_SABOTAGE = "sabotage";
    // tg: code/game/objects/items/storage/uplink_kits.dm:6  #define KIT_GUN "guns"
    public const string KIT_GUN = "guns";
    // tg: code/game/objects/items/storage/uplink_kits.dm:7  #define KIT_MURDER "murder"
    public const string KIT_MURDER = "murder";
    // tg: code/game/objects/items/storage/uplink_kits.dm:8  #define KIT_IMPLANTS "implant"
    public const string KIT_IMPLANTS = "implant";
    // tg: code/game/objects/items/storage/uplink_kits.dm:9  #define KIT_HACKER "hacker"
    public const string KIT_HACKER = "hacker";
    // tg: code/game/objects/items/storage/uplink_kits.dm:10  #define KIT_SNIPER "sniper"
    public const string KIT_SNIPER = "sniper";
    // tg: code/game/objects/items/storage/uplink_kits.dm:11  #define KIT_NUKEOPS_METAGAME "metaops"
    public const string KIT_NUKEOPS_METAGAME = "metaops";
    // tg: code/game/objects/items/storage/uplink_kits.dm:12  #define KIT_LORD_SINGULOTH "lordsingulo"
    public const string KIT_LORD_SINGULOTH = "lordsingulo";
    // tg: code/game/objects/items/storage/uplink_kits.dm:13  #define KIT_REVOLUTIONARY "revolutionary"
    public const string KIT_REVOLUTIONARY = "revolutionary";
    // tg: code/game/objects/items/storage/uplink_kits.dm:15  #define KIT_JAMES_BOND "bond"
    public const string KIT_JAMES_BOND = "bond";
    // tg: code/game/objects/items/storage/uplink_kits.dm:16  #define KIT_NINJA "ninja"
    public const string KIT_NINJA = "ninja";
    // tg: code/game/objects/items/storage/uplink_kits.dm:17  #define KIT_DARK_LORD "darklord"
    public const string KIT_DARK_LORD = "darklord";
    // tg: code/game/objects/items/storage/uplink_kits.dm:18  #define KIT_WHITE_WHALE_HOLY_GRAIL "white_whale_holy_grail"
    public const string KIT_WHITE_WHALE_HOLY_GRAIL = "white_whale_holy_grail";
    // tg: code/game/objects/items/storage/uplink_kits.dm:19  #define KIT_MAD_SCIENTIST "mad_scientist"
    public const string KIT_MAD_SCIENTIST = "mad_scientist";
    // tg: code/game/objects/items/storage/uplink_kits.dm:20  #define KIT_BEES "bee"
    public const string KIT_BEES = "bee";
    // tg: code/game/objects/items/storage/uplink_kits.dm:21  #define KIT_MR_FREEZE "mr_freeze"
    public const string KIT_MR_FREEZE = "mr_freeze";
    // tg: code/game/objects/items/storage/uplink_kits.dm:22  #define KIT_TRAITOR_2006 "ancient"
    public const string KIT_TRAITOR_2006 = "ancient";
    // tg: code/game/objects/items/storage/uplink_kits.dm:23  #define KIT_DEAD_MONEY "dead_money"
    public const string KIT_DEAD_MONEY = "dead_money";
    // tg: code/game/objects/items/storage/uplink_kits.dm:24  #define KIT_SAM_FISHER "sam_fisher"
    public const string KIT_SAM_FISHER = "sam_fisher";
    // tg: code/game/objects/items/storage/uplink_kits.dm:25  #define KIT_PROP_HUNT "prop_hunt"
    public const string KIT_PROP_HUNT = "prop_hunt";
}

// tg: code/game/objects/items/storage/uplink_kits.dm:30
public partial class obj_item_storage_box_syndicate_bundle_a : obj_item_storage_box_syndicate
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:30
    public override async DmTask PopulateContents()
    {
        switch (pick_weight(Dm.list(
            Dm.kv(KIT_RECON, 2),
            Dm.kv(KIT_BLOODY_SPAI, 3),
            Dm.kv(KIT_STEALTHY, 2),
            Dm.kv(KIT_SCREWED, 2),
            Dm.kv(KIT_SABOTAGE, 3),
            Dm.kv(KIT_GUN, 2),
            Dm.kv(KIT_MURDER, 2),
            Dm.kv(KIT_IMPLANTS, 1),
            Dm.kv(KIT_HACKER, 3),
            Dm.kv(KIT_SNIPER, 1),
            Dm.kv(KIT_NUKEOPS_METAGAME, 1),
            Dm.kv(KIT_REVOLUTIONARY, 2))))
        {
            case KIT_RECON:
                Dm.@new<obj_item_clothing_glasses_thermal_xray>(this); // ~8 tc?
                Dm.@new<obj_item_storage_briefcase_launchpad>(this); //6 tc
                Dm.@new<obj_item_binoculars>(this); // 2 tc?
                Dm.@new<obj_item_encryptionkey_syndicate>(this); // 2 tc
                Dm.@new<obj_item_storage_box_syndie_kit_space>(this); //4 tc
                Dm.@new<obj_item_grenade_frag>(this); // ~2 tc each?
                Dm.@new<obj_item_grenade_frag>(this);
                Dm.@new<obj_item_flashlight_emp>(this); // 4 tc
                break;
            case KIT_BLOODY_SPAI:
                Dm.@new<obj_item_card_id_advanced_chameleon_elite>(this); // 2 tc
                Dm.@new<obj_item_clothing_under_chameleon>(this); // 2 tc since it's not the full set
                Dm.@new<obj_item_clothing_mask_chameleon>(this); // Goes with above
                Dm.@new<obj_item_clothing_shoes_chameleon_noslip>(this); // 2 tc
                Dm.@new<obj_item_disk_computer_syndicate_camera_app>(this); // 1 tc
                Dm.@new<obj_item_multitool_ai_detect>(this); // 1 tc
                Dm.@new<obj_item_encryptionkey_syndicate>(this); // 2 tc
                Dm.@new<obj_item_storage_box_syndie_kit_mulligan>(this); // 4 tc
                Dm.@new<obj_item_switchblade>(this); //basically 1 tc as it can be bought from BM kits
                Dm.@new<obj_item_storage_fancy_cigarettes_cigpack_syndicate>(this); // 2 tc this shit heals
                Dm.@new<obj_item_flashlight_emp>(this); // 2 tc
                Dm.@new<obj_item_chameleon>(this); // 7 tc
                Dm.@new<obj_item_implanter_storage>(this); // 6 tc
                break;
            case KIT_STEALTHY:
                Dm.@new<obj_item_gun_energy_recharge_ebow>(this); // 10 tc
                Dm.@new<obj_item_pen_sleepy>(this); // 4 tc
                Dm.@new<obj_item_healthanalyzer_rad_laser>(this); // 3 tc
                Dm.@new<obj_item_chameleon>(this); // 7 tc
                Dm.@new<obj_item_soap_syndie>(this); // 1 tc
                Dm.@new<obj_item_clothing_glasses_thermal_syndi>(this); // 4 tc
                Dm.@new<obj_item_flashlight_emp>(this); // 2 tc
                Dm.@new<obj_item_jammer>(this); // 5 tc
                break;
            case KIT_GUN:
                Dm.@new<obj_item_gun_ballistic_revolver>(this); // 13 tc
                Dm.@new<obj_item_ammo_box_speedloader_c357>(this); // 4tc
                Dm.@new<obj_item_ammo_box_speedloader_c357>(this);
                Dm.@new<obj_item_storage_belt_holster_chameleon>(this); // 1 tc
                Dm.@new<obj_item_card_emag_doorjack>(this); // 3 tc replaced the emag with the doorjack
                Dm.@new<obj_item_grenade_c4>(this); // 1 tc
                Dm.@new<obj_item_clothing_gloves_latex_nitrile>(this); // ~1 tc for whole outfit
                Dm.@new<obj_item_clothing_mask_gas_clown_hat>(this);
                Dm.@new<obj_item_clothing_under_suit_black_really>(this);
                Dm.@new<obj_item_clothing_neck_tie_red_hitman>(this);
                break;
            case KIT_SCREWED:
            {
                Dm.@new<obj_item_sbeacondrop_bomb>(this); // 11 tc
                Dm.@new<obj_item_grenade_syndieminibomb>(this); // 6 tc
                Dm.@new<obj_item_sbeacondrop_powersink>(this); // 11 tc
                Type spess_suit = pick(GLOB.syndicate_space_suits_to_helmets) as Type;
                Dm.@new(spess_suit, this); // Above allows me to get the helmet from a variable on the object
                Type spess_helmet = GLOB.syndicate_space_suits_to_helmets[spess_suit] as Type;
                Dm.@new(spess_helmet, this); // 4 TC for the space gear
                Dm.@new<obj_item_encryptionkey_syndicate>(this); // 2 tc
                break;
            }
            case KIT_MURDER:
                Dm.@new<obj_item_melee_energy_sword_saber>(this); // 8 tc
                Dm.@new<obj_item_clothing_glasses_thermal_syndi>(this); // 4 tc
                Dm.@new<obj_item_card_emag_doorjack>(this); // 3 tc
                Dm.@new<obj_item_clothing_shoes_chameleon_noslip>(this); // 2 tc
                Dm.@new<obj_item_encryptionkey_syndicate>(this); // 2 tc
                Dm.@new<obj_item_grenade_syndieminibomb>(this); // 6 tc
                break;
            case KIT_IMPLANTS:
                Dm.@new<obj_item_implanter_freedom>(this); // 5 tc
                Dm.@new<obj_item_implanter_uplink_precharged>(this); // 10 tc is inside this thing
                Dm.@new<obj_item_implanter_emp>(this); // 1 tc
                Dm.@new<obj_item_implanter_explosive>(this); // 2 tc
                Dm.@new<obj_item_implanter_storage>(this); // 8 tc
                break;
            case KIT_HACKER: //L-L--LOOK AT YOU, HACKER
                Dm.@new<obj_item_ai_module_law_syndicate>(this); // 4 tc
                Dm.@new<obj_item_card_emag>(this); // 4 tc
                Dm.@new<obj_item_card_emag_doorjack>(this); // 3 tc
                Dm.@new<obj_item_encryptionkey_binary>(this); // 5 tc
                Dm.@new<obj_item_ai_module_law_toy_ai>(this); // ~6 tc
                Dm.@new<obj_item_multitool_ai_detect>(this); // 1 tc
                Dm.@new<obj_item_storage_toolbox_syndicate>(this); // 1 tc
                Dm.@new<obj_item_disk_computer_syndicate_camera_app>(this); // 1 tc
                Dm.@new<obj_item_clothing_glasses_thermal_syndi>(this); // 4 tc
                Dm.@new<obj_item_card_id_advanced_chameleon_elite>(this); // 2 tc
                break;
            case KIT_LORD_SINGULOTH: //currently disabled, i might return with another anti-engine kit
            {
                Dm.@new<obj_item_sbeacondrop>(this); // 10 tc
                Type spess_suit = pick(GLOB.syndicate_space_suits_to_helmets) as Type;
                Dm.@new(spess_suit, this); // Above allows me to get the helmet from a variable on the object
                Type spess_helmet = GLOB.syndicate_space_suits_to_helmets[spess_suit] as Type;
                Dm.@new(spess_helmet, this); // 4 TC for the space gear
                Dm.@new<obj_item_card_emag>(this); // 4 tc
                Dm.@new<obj_item_storage_toolbox_syndicate>(this); // 1 tc
                Dm.@new<obj_item_card_id_advanced_mining>(this);
                Dm.@new<obj_item_stack_spacecash_c10000>(this); // this is technically 10 tc but not really
                if (prob(70))
                    Dm.@new<obj_item_toy_spinningtoy>(this); //lol
                else
                    Dm.@new<obj_item_toy_spinningtoy_dark_matter>(this); //edgy lol
                break;
            }
            case KIT_SABOTAGE:
                Dm.@new<obj_item_storage_backpack_duffelbag_syndie_sabotage>(this); // 5 tc for 3 c4 and 2 x4
                Dm.@new<obj_item_disk_computer_syndicate_camera_app>(this); // 1 tc
                Dm.@new<obj_item_sbeacondrop_powersink>(this); // 11 tc
                Dm.@new<obj_item_disk_computer_virus_detomatix>(this); // 6 tc
                Dm.@new<obj_item_storage_toolbox_syndicate>(this); // 1 tc
                Dm.@new<obj_item_pizzabox_bomb>(this); // 6 tc
                Dm.@new<obj_item_storage_box_syndie_kit_emp>(this); // 2 tc
                break;
            case KIT_SNIPER: //This shit is unique so can't really balance it around tc, also no silencer because getting killed without ANY indicator on what killed you sucks
                Dm.@new<obj_item_gun_ballistic_rifle_sniper_rifle>(this); // 12 tc
                Dm.@new<obj_item_ammo_box_magazine_sniper_rounds_penetrator>(this); // 5 tc
                Dm.@new<obj_item_clothing_glasses_thermal_syndi>(this); // 4 tc
                Dm.@new<obj_item_clothing_gloves_latex_nitrile>(this); // ~ 1 tc for outfit
                Dm.@new<obj_item_clothing_mask_gas_clown_hat>(this);
                Dm.@new<obj_item_clothing_under_suit_black_really>(this);
                Dm.@new<obj_item_clothing_neck_tie_red_hitman>(this);
                break;
            case KIT_NUKEOPS_METAGAME:
                Dm.@new<obj_item_mod_control_pre_equipped_nuclear_unrestricted>(this); // 8 tc
                Dm.@new<obj_item_gun_ballistic_shotgun_bulldog_unrestricted>(this); // 8 tc
                Dm.@new<obj_item_implanter_explosive>(this); // 2 tc
                Dm.@new<obj_item_ammo_box_magazine_m12g>(this); // 2 tc
                Dm.@new<obj_item_ammo_box_magazine_m12g>(this); // 2 tc
                Dm.@new<obj_item_grenade_c4>(this); // 1 tc
                Dm.@new<obj_item_grenade_c4>(this); // 1 tc
                Dm.@new<obj_item_card_emag>(this); // 4 tc
                Dm.@new<obj_item_card_emag_doorjack>(this); // 3 tc
                break;
            case KIT_REVOLUTIONARY:
                Dm.@new<obj_item_healthanalyzer_rad_laser>(this); // 3 TC
                Dm.@new<obj_item_assembly_flash_hypnotic>(this); // 7 TC
                Dm.@new<obj_item_storage_pill_bottle_lsd>(this); // ~1 TC
                Dm.@new<obj_item_pen_sleepy>(this); // 4 TC
                Dm.@new<obj_item_gun_ballistic_revolver_nagant>(this); // 13 TC comparable to 357. revolvers
                Dm.@new<obj_item_megaphone>(this);
                Dm.@new<obj_item_bedsheet_rev>(this);
                Dm.@new<obj_item_clothing_suit_armor_vest_russian_coat>(this);
                Dm.@new<obj_item_clothing_head_helmet_rus_ushanka>(this);
                Dm.@new<obj_item_storage_box_syndie_kit_poster_box>(this);
            break;
        }
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:185
public partial class obj_item_storage_box_syndicate_bundle_b : obj_item_storage_box_syndicate
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:185
    public override async DmTask PopulateContents()
    {
        switch (pick_weight(Dm.list(
            Dm.kv(KIT_JAMES_BOND, 2),
            Dm.kv(KIT_NINJA, 1),
            Dm.kv(KIT_DARK_LORD, 1),
            Dm.kv(KIT_WHITE_WHALE_HOLY_GRAIL, 2),
            Dm.kv(KIT_MAD_SCIENTIST, 2),
            Dm.kv(KIT_BEES, 1),
            Dm.kv(KIT_MR_FREEZE, 2),
            Dm.kv(KIT_DEAD_MONEY, 2),
            Dm.kv(KIT_TRAITOR_2006, 1),
            Dm.kv(KIT_SAM_FISHER, 1),
            Dm.kv(KIT_PROP_HUNT, 1))))
        {
            case KIT_JAMES_BOND:
                Dm.@new<obj_item_gun_ballistic_automatic_pistol>(this); // 7 tc
                Dm.@new<obj_item_suppressor>(this); // 3 tc
                Dm.@new<obj_item_ammo_box_magazine_m9mm>(this); // 1 tc
                Dm.@new<obj_item_ammo_box_magazine_m9mm>(this);
                Dm.@new<obj_item_card_id_advanced_chameleon_elite>(this); // 2 tc
                Dm.@new<obj_item_clothing_under_chameleon>(this); // 1 tc
                Dm.@new<obj_item_reagent_containers_hypospray_medipen_stimulants>(this); // 5 tc
                Dm.@new<obj_item_rag>(this);
                Dm.@new<obj_item_implanter_freedom>(this); // 5 tc
                Dm.@new<obj_item_flashlight_emp>(this); // 2 tc
                Dm.@new<obj_item_grenade_c4_x4>(this); // 1ish tc
                Dm.@new<obj_item_reagent_containers_applicator_pill_cyanide>(this);
                Dm.@new<obj_item_toy_cards_deck_syndicate>(this); // 1 tc, for poker
                break;
            case KIT_NINJA:
                Dm.@new<obj_item_storage_belt_sheath_katana>(this); // Unique , hard to tell how much tc this is worth. 8 tc?
                Dm.@new<obj_item_reagent_containers_hypospray_medipen_stimulants>(this); // 5 tc
                for (int i = 1; i <= 6; i++)
                    Dm.@new<obj_item_throwing_star>(this); // 1 tc
                Dm.@new<obj_item_storage_belt_chameleon>(this); // worth some fraction of a tc
                Dm.@new<obj_item_chameleon>(this); // 7 tc
                Dm.@new<obj_item_card_id_advanced_chameleon_elite>(this); // 2 tc
                Dm.@new<obj_item_card_emag_doorjack>(this); // 3 tc
                Dm.@new<obj_item_book_granter_action_spell_smoke>(this); // ninja smoke bomb. 1 tc
                Dm.@new<obj_item_clothing_shoes_bhop>(this); // mining item, lets you jump at people, at least 2 tc
                break;
            case KIT_DARK_LORD:
                Dm.@new<obj_item_dualsaber_red>(this); // 16 tc
                Dm.@new<obj_item_dnainjector_telemut_darkbundle>(this); // ~ 4 tc for tk
                Dm.@new<obj_item_clothing_suit_hooded_chaplain_hoodie>(this);
                Dm.@new<obj_item_card_id_advanced_chameleon_elite>(this); // 2 tc
                Dm.@new<obj_item_clothing_shoes_chameleon_noslip>(this); //2 tc ,because slipping while being a dark lord sucks
                Dm.@new<obj_item_book_granter_action_spell_summonitem>(this); // ~2 tc
                Dm.@new<obj_item_book_granter_action_spell_lightningbolt>(this); // 4 tc
                break;
            case KIT_WHITE_WHALE_HOLY_GRAIL: //Unique items that don't appear anywhere else
                Dm.@new<obj_item_gun_ballistic_rifle_boltaction_harpoon>(this);
                Dm.@new<obj_item_storage_bag_harpoon_quiver>(this);
                Dm.@new<obj_item_clothing_suit_hooded_carp_costume_spaceproof>(this);
                Dm.@new<obj_item_clothing_mask_gas_carp>(this);
                Dm.@new<obj_item_grenade_spawnergrenade_spesscarp>(this);
                Dm.@new<obj_item_toy_plush_carpplushie_dehy_carp>(this); // 1 tc, for use as a personal mount
                break;
            case KIT_MAD_SCIENTIST:
                Dm.@new<obj_item_clothing_suit_toggle_labcoat_mad>(this); // 0 tc
                Dm.@new<obj_item_clothing_shoes_jackboots>(this); // 0 tc
                Dm.@new<obj_item_megaphone>(this); // 0 tc
                Dm.@new<obj_item_grenade_clusterbuster_random>(this); // 10 tc?
                Dm.@new<obj_item_grenade_clusterbuster_random>(this); // 10 tc?
                Dm.@new<obj_item_grenade_chem_grenade_bioterrorfoam>(this); // 5 tc
                Dm.@new<obj_item_assembly_signaler>(this); // 0 tc
                Dm.@new<obj_item_assembly_signaler>(this); // 0 tc
                Dm.@new<obj_item_assembly_signaler>(this); // 0 tc
                Dm.@new<obj_item_assembly_signaler>(this); // 0 tc
                Dm.@new<obj_item_storage_toolbox_syndicate>(this); // 1 tc
                Dm.@new<obj_item_pen_edagger>(this); // 2 tc
                Dm.@new<obj_item_gun_energy_wormhole_projector_core_inserted>(this); // 5 tc easily
                break;
            case KIT_BEES:
                Dm.@new<obj_item_paper_fluff_bee_objectives>(this); // 0 tc (motivation)
                Dm.@new<obj_item_clothing_suit_hooded_bee_costume>(this); // 0 tc
                Dm.@new<obj_item_clothing_mask_animal_small_bee>(this); // 0 tc
                Dm.@new<obj_item_storage_belt_fannypack_yellow>(this); // 0 tc
                Dm.@new<obj_item_grenade_spawnergrenade_buzzkill>(this); // these are the random super bees this is definitely all of the tc budget for this one
                Dm.@new<obj_item_grenade_spawnergrenade_buzzkill>(this); // 10 tc per grenade
                Dm.@new<obj_item_reagent_containers_cup_bottle_beesease>(this); // 10 tc?
                Dm.@new<obj_item_melee_beesword>(this); //priceless
                break;
            case KIT_MR_FREEZE:
                Dm.@new<obj_item_clothing_glasses_cold>(this);
                Dm.@new<obj_item_clothing_gloves_color_black_security_blu>(this);
                Dm.@new<obj_item_clothing_mask_chameleon>(this);
                Dm.@new<obj_item_clothing_suit_hooded_wintercoat>(this);
                Dm.@new<obj_item_clothing_shoes_winterboots>(this);
                Dm.@new<obj_item_grenade_gluon>(this); // whole belt is 22 and gluon is weight 4 so lets just go with like 7 total
                Dm.@new<obj_item_grenade_gluon>(this);
                Dm.@new<obj_item_grenade_gluon>(this);
                Dm.@new<obj_item_grenade_gluon>(this);
                Dm.@new<obj_item_dnainjector_geladikinesis>(this); // both abilities are probably 3 tc total
                Dm.@new<obj_item_dnainjector_cryokinesis>(this);
                Dm.@new<obj_item_gun_energy_temperature_freeze>(this); // ~6 tc
                Dm.@new<obj_item_gun_energy_laser_thermal_cryo>(this); // ~6 tc
                Dm.@new<obj_item_melee_energy_sword_saber_blue>(this); //see see it fits the theme bc its blue and ice is blue, 8 tc
                break;
            case KIT_TRAITOR_2006: //A kit so old, it's probably older than you. //This bundle is filled with the entire uplink contents traitors had access to in 2006, from OpenSS13. Notably the esword was not a choice but existed in code.
                Dm.@new<obj_item_storage_toolbox_emergency_old_ancientbundle>(this); //Items fit neatly into a classic toolbox just to remind you what the theme is.
                break;
            case KIT_DEAD_MONEY:
                for (int i = 1; i <= 4; i++)
                    Dm.@new<obj_item_clothing_neck_collar_bomb>(this); // These let you remotely kill people with a signaler, though you have to get them first.
                Dm.@new<obj_item_storage_box_syndie_kit_signaler>(this);
                Dm.@new<obj_item_mod_control_pre_equipped_responsory_inquisitory_syndie>(this); // basically a snowflake yet better elite modsuit, so like, 8 + 5 tc.
                Dm.@new<obj_item_card_id_advanced_chameleon_elite>(this); // 2 tc
                Dm.@new<obj_item_clothing_mask_chameleon>(this);
                Dm.@new<obj_item_melee_baton_telescopic_contractor_baton>(this); // 7 tc
                Dm.@new<obj_item_jammer>(this); // 5 tc
                Dm.@new<obj_item_pinpointer_crew>(this); //priceless
                break;
            case KIT_SAM_FISHER:
                Dm.@new<obj_item_clothing_under_syndicate_combat>(this);
                Dm.@new<obj_item_clothing_suit_armor_vest_marine_pmc>(this); //The armor kit is comparable to the infiltrator, 6 TC
                Dm.@new<obj_item_clothing_head_helmet_marine_pmc>(this);
                Dm.@new<obj_item_clothing_mask_gas_sechailer>(this);
                Dm.@new<obj_item_clothing_glasses_night>(this); // 3~ TC
                Dm.@new<obj_item_clothing_gloves_kaza_ruk_combatglovesplus>(this); //5TC
                Dm.@new<obj_item_clothing_shoes_jackboots>(this);
                Dm.@new<obj_item_storage_belt_military_assault_fisher>(this); //items in this belt easily costs 18 TC
                break;
            case KIT_PROP_HUNT:
                Dm.@new<obj_item_chameleon>(this); // 7 TC
                Dm.@new<obj_item_card_emag_doorjack>(this); // 3 TC
                Dm.@new<obj_item_storage_box_syndie_kit_imp_stealth>(this); //8 TC
                Dm.@new<obj_item_gun_ballistic_automatic_pistol>(this); // 7 TC
                Dm.@new<obj_item_clothing_glasses_thermal>(this); // 4 TC
            break;
        }
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:315
public partial class obj_item_storage_toolbox_emergency_old_ancientbundle : obj_item_storage_toolbox_emergency_old
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:317
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_card_emag>(this); // 4 tc
        Dm.@new<obj_item_card_emag_doorjack>(this); //emag used to do both. 3 tc
        Dm.@new<obj_item_pen_sleepy>(this); // 4 tc
        Dm.@new<obj_item_reagent_containers_applicator_pill_cyanide>(this);
        Dm.@new<obj_item_chameleon>(this); //its not the original cloaking device, but it will do. 8 tc
        Dm.@new<obj_item_gun_ballistic_revolver>(this); // 13 tc old one stays in the old box
        Dm.@new<obj_item_implanter_freedom>(this); // 5 tc
        Dm.@new<obj_item_stack_telecrystal>(this); //The failsafe/self destruct isn't an item we can physically include in the kit, but 1 TC is technically enough to buy the equivalent.
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:327
public partial class obj_item_storage_belt_military_assault_fisher : obj_item_storage_belt_military_assault
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:329
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_gun_ballistic_automatic_pistol_clandestine_fisher>(this); // 11 TC: 7 (pistol) + 3 (suppressor) + lightbreaker (1 TC, black market meme/util item)
        Dm.@new<obj_item_ammo_box_magazine_m10mm>(this); // 1 TC
        Dm.@new<obj_item_ammo_box_magazine_m10mm>(this);
        Dm.@new<obj_item_card_emag_doorjack>(this); // 3 TC
        Dm.@new<obj_item_knife_combat>(this); //comparable to the e-dagger, 2 TC
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:342
public partial class obj_item_storage_box_syndie_kit_rebarxbowsyndie : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:346
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_book_granter_crafting_recipe_dusting_rebarxbowsyndie_ammo>(this);
        Dm.@new<obj_item_gun_ballistic_rifle_rebarxbow_syndie>(this);
        Dm.@new<obj_item_storage_bag_rebar_quiver_syndicate>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:360
public partial class obj_item_storage_box_syndie_kit_forensics_spoofer : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:363
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_forensics_spoofer>(this);
        Dm.@new<obj_item_paper_syndicate_forensics_spoofer>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:367
public partial class obj_item_storage_box_syndie_kit_origami_bundle : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:371
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_book_granter_action_origami>(this);
        for (int i = 1; i <= 5; i++)
            Dm.@new<obj_item_paper>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:376
public partial class obj_item_storage_box_syndie_kit_imp_freedom : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:379
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_freedom>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:382
public partial class obj_item_storage_box_syndie_kit_imp_microbomb : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:385
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_explosive>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:388
public partial class obj_item_storage_box_syndie_kit_imp_macrobomb : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:391
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_explosive_macro>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:394
public partial class obj_item_storage_box_syndie_kit_imp_macrobomb_fake : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:397
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_explosive_macro_fake>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:400
public partial class obj_item_storage_box_syndie_kit_imp_deniability : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:403
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_tactical_deniability>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:406
public partial class obj_item_storage_box_syndie_kit_imp_uplink : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:409
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_uplink>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:412
public partial class obj_item_storage_box_syndie_kit_bioterror : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:415
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 7; i++)
            Dm.@new<obj_item_reagent_containers_syringe_bioterror>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:419
public partial class obj_item_storage_box_syndie_kit_clownpins : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:423
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 4; i++)
            Dm.@new(pin_type, this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:431
public partial class obj_item_storage_box_syndie_kit_imp_storage : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:434
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_storage>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:437
public partial class obj_item_storage_box_syndie_kit_imp_stealth : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:440
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_stealth>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:443
public partial class obj_item_storage_box_syndie_kit_imp_radio : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:446
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_radio_syndicate>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:449
public partial class obj_item_storage_box_syndie_kit_space : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:453
    public override async DmTask PopulateContents()
    {
        Type spess_suit = pick(GLOB.syndicate_space_suits_to_helmets) as Type;
        Dm.@new(spess_suit, this); // Above allows me to get the helmet from a variable on the object
        Type spess_helmet = GLOB.syndicate_space_suits_to_helmets[spess_suit] as Type;
        Dm.@new(spess_helmet, this); // 4 TC for the space gear
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:459
public partial class obj_item_storage_box_syndie_kit_emp : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:462
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 5; i++)
            Dm.@new<obj_item_grenade_empgrenade>(this);
        Dm.@new<obj_item_implanter_emp>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:467
public partial class obj_item_storage_box_syndie_kit_smoke : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:470
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 5; i++)
            Dm.@new<obj_item_grenade_smokebomb>(this);
        Dm.@new<obj_item_implanter_smoke>(this);
        Dm.@new<obj_item_clothing_mask_gas>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:476
public partial class obj_item_storage_box_syndie_kit_mail_counterfeit : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:480
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 6; i++)
            Dm.@new<obj_item_storage_mail_counterfeit_device>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:484
public partial class obj_item_storage_box_syndie_kit_chemical : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:488
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_cup_bottle_polonium>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_venom>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_fentanyl>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_anacea>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_spewium>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_syndol>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_cyanide>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_histamine>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_initropidril>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_pancuronium>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_sodium_thiopental>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_coniine>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_curare>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_amanitin>(this);
        Dm.@new<obj_item_reagent_containers_syringe>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:505
public partial class obj_item_storage_box_syndie_kit_carnivorous_blood : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:505
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_cup_bottle_carnivorous_blood>(this);
        Dm.@new<obj_item_reagent_containers_syringe>(this);
        Dm.@new<obj_item_food_meat_slab_human>(this);
        Dm.@new<obj_item_paper_guides_carnivorous_blood>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:511
public partial class obj_item_storage_box_syndie_kit_nuke : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:515
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver_nuke>(this);
        Dm.@new<obj_item_nuke_core_container>(this);
        Dm.@new<obj_item_paper_guides_antag_nuke_instructions>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:520
public partial class obj_item_storage_box_syndie_kit_supermatter : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:524
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_scalpel_supermatter>(this);
        Dm.@new<obj_item_hemostat_supermatter>(this);
        Dm.@new<obj_item_nuke_core_container_supermatter>(this);
        Dm.@new<obj_item_paper_guides_antag_supermatter_sliver>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:530
public partial class obj_item_storage_box_syndie_kit_tuberculosisgrenade : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:533
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_grenade_chem_grenade_tuberculosis>(this);
        for (int i = 1; i <= 5; i++)
            Dm.@new<obj_item_reagent_containers_hypospray_medipen_tuberculosiscure>(this);
        Dm.@new<obj_item_reagent_containers_syringe>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_tuberculosiscure>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:540
public partial class obj_item_storage_box_syndie_kit_chameleon : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:543
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_chameleon>(this);
        Dm.@new<obj_item_clothing_suit_chameleon>(this);
        Dm.@new<obj_item_clothing_gloves_chameleon>(this);
        Dm.@new<obj_item_clothing_shoes_chameleon>(this);
        Dm.@new<obj_item_clothing_glasses_chameleon>(this);
        Dm.@new<obj_item_clothing_head_chameleon>(this);
        Dm.@new<obj_item_clothing_mask_chameleon>(this);
        Dm.@new<obj_item_clothing_neck_chameleon>(this);
        Dm.@new<obj_item_storage_backpack_chameleon>(this);
        Dm.@new<obj_item_storage_belt_chameleon>(this);
        Dm.@new<obj_item_radio_headset_chameleon>(this);
        Dm.@new<obj_item_stamp_chameleon>(this);
        Dm.@new<obj_item_modular_computer_pda_chameleon>(this);
        Dm.@new<obj_item_gun_energy_laser_chameleon>(this);
        Dm.@new<obj_item_chameleon_scanner>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:560
public partial class obj_item_storage_box_syndie_kit_throwing_weapons : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:565
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 5; i++)
            Dm.@new<obj_item_throwing_star>(this);
        for (int i = 1; i <= 2; i++)
            Dm.@new<obj_item_paperplane_syndicate>(this);
        Dm.@new<obj_item_restraints_legcuffs_bola_tactical>(this);
        Dm.@new<obj_item_restraints_legcuffs_bola_tactical>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:573
public partial class obj_item_storage_box_syndie_kit_cutouts : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:573
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 3; i++)
            Dm.@new<obj_item_cardboard_cutout_adaptive>(this);
        Dm.@new<obj_item_toy_crayon_rainbow>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:578
public partial class obj_item_storage_box_syndie_kit_romerol : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:578
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_cup_bottle_romerol>(this);
        Dm.@new<obj_item_reagent_containers_syringe>(this);
        Dm.@new<obj_item_reagent_containers_dropper>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:583
public partial class obj_item_storage_box_syndie_kit_ez_clean : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:583
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 3; i++)
            Dm.@new<obj_item_grenade_chem_grenade_ez_clean>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:587
public partial class obj_item_storage_box_syndie_kit_mulligan : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:587
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        Dm.@new<obj_item_reagent_containers_syringe_mulligan>(this);
        Dm.@new<obj_item_fake_identity_kit>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:592
public partial class obj_item_storage_box_hug_reverse_revolver : obj_item_storage_box_hug
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:592
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_gun_ballistic_revolver_reverse>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:595
public partial class obj_item_storage_box_syndie_kit_mimery : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:595
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_book_granter_action_spell_mime_mimery_blockade>(this);
        Dm.@new<obj_item_book_granter_action_spell_mime_mimery_guns>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:599
public partial class obj_item_storage_box_syndie_kit_moltobeso : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:599
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_cup_bottle_moltobeso>(this);
        Dm.@new<obj_item_reagent_containers_syringe>(this);
        Dm.@new<obj_item_reagent_containers_dropper>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:604
public partial class obj_item_storage_box_syndie_kit_combat_baking : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:604
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_food_baguette_combat>(this);
        for (int i = 1; i <= 2; i++)
            Dm.@new<obj_item_food_croissant_throwing>(this);
        Dm.@new<obj_item_book_granter_crafting_recipe_combat_baking>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:610
public partial class obj_item_storage_box_syndie_kit_centcom_costume : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:610
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_centcom_officer>(this);
        Dm.@new<obj_item_clothing_shoes_sneakers_black>(this);
        Dm.@new<obj_item_clothing_gloves_color_black>(this);
        Dm.@new<obj_item_radio_headset_headset_cent_empty>(this);
        Dm.@new<obj_item_clothing_glasses_sunglasses>(this);
        Dm.@new<obj_item_storage_backpack_satchel>(this);
        Dm.@new<obj_item_modular_computer_pda_crew_heads>(this);
        Dm.@new<obj_item_clipboard>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:620
public partial class obj_item_storage_box_syndie_kit_chameleon_broken : obj_item_storage_box_syndie_kit_chameleon
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:620
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_chameleon_broken>(this);
        Dm.@new<obj_item_clothing_suit_chameleon_broken>(this);
        Dm.@new<obj_item_clothing_gloves_chameleon_broken>(this);
        Dm.@new<obj_item_clothing_shoes_chameleon_noslip_broken>(this);
        Dm.@new<obj_item_clothing_glasses_chameleon_broken>(this);
        Dm.@new<obj_item_clothing_head_chameleon_broken>(this);
        Dm.@new<obj_item_clothing_mask_chameleon_broken>(this);
        Dm.@new<obj_item_clothing_neck_chameleon_broken>(this);
        Dm.@new<obj_item_storage_backpack_chameleon_broken>(this);
        Dm.@new<obj_item_storage_belt_chameleon_broken>(this);
        Dm.@new<obj_item_radio_headset_chameleon_broken>(this);
        Dm.@new<obj_item_stamp_chameleon_broken>(this);
        Dm.@new<obj_item_modular_computer_pda_chameleon_broken>(this);
        // No chameleon laser, they can't randomise for //REASONS//
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:636
public partial class obj_item_storage_box_syndie_kit_bee_grenades : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:640
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 3; i++)
            Dm.@new<obj_item_grenade_spawnergrenade_buzzkill>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:644
public partial class obj_item_storage_box_syndie_kit_manhack_grenades : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:644
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 3; i++)
            Dm.@new<obj_item_grenade_spawnergrenade_manhacks>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:648
public partial class obj_item_storage_box_syndie_kit_feral_cat_grenades : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:648
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 5; i++)
            Dm.@new<obj_item_grenade_spawnergrenade_cat>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:652
public partial class obj_item_storage_box_syndie_kit_frag_grenades : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:652
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 7; i++)
            Dm.@new<obj_item_grenade_frag>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:656
public partial class obj_item_storage_box_syndie_kit_sleepytime : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:656
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_syndicate_bloodred_sleepytime>(this);
        Dm.@new<obj_item_reagent_containers_cup_glass_mug_coco>(this);
        Dm.@new<obj_item_toy_plush_carpplushie>(this);
        Dm.@new<obj_item_bedsheet_syndie>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:662
public partial class obj_item_storage_box_syndie_kit_demoman : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:662
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_gun_grenadelauncher>(this);
        Dm.@new<obj_item_storage_belt_grenade_full>(this);
        if (prob(1))
            Dm.@new<obj_item_clothing_head_hats_hos_shako>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:668
public partial class obj_item_storage_box_syndie_kit_core_gear : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:672
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter_freedom>(this);
        Dm.@new<obj_item_card_emag_doorjack>(this);
        Dm.@new<obj_item_reagent_containers_hypospray_medipen_stimulants>(this);
        Dm.@new<obj_item_grenade_c4>(this);
        Dm.@new<obj_item_mod_module_energy_shield>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:681
public partial class obj_item_storage_box_syndie_kit_sniper_surplus : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:685
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 7; i++)
            Dm.@new<obj_item_ammo_box_magazine_sniper_rounds_surplus>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:689
public partial class obj_item_storage_box_syndie_kit_shotgun_surplus : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:696
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 7; i++)
            Dm.@new<obj_item_ammo_box_magazine_m12g_donk>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:701
public partial class obj_item_storage_backpack_duffelbag_syndie_sabotage : obj_item_storage_backpack_duffelbag_syndie
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:703
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_grenade_c4>(this);
        Dm.@new<obj_item_grenade_c4>(this);
        Dm.@new<obj_item_grenade_c4>(this);
        Dm.@new<obj_item_grenade_c4_x4>(this);
        Dm.@new<obj_item_grenade_c4_x4>(this);
        Dm.@new<obj_item_storage_box_syndie_kit_signaler>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:711
public partial class obj_item_storage_box_syndie_kit_signaler : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:715
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 6; i++)
            Dm.@new<obj_item_assembly_signaler>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:719
public partial class obj_item_storage_box_syndie_kit_imp_deathrattle : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:723
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_implanter>(this);

        datum_deathrattle_group_standard group = await Dm.new_async<datum_deathrattle_group_standard>();

        DmList implants = Dm.list();
        for (int j = 1; j <= 8; j++)
        {
            obj_item_implantcase_deathrattle @case = Dm.@new<obj_item_implantcase_deathrattle>(this);
            Dm.append(ref implants, @case.imp);
        }

        foreach (object i in Dm.each(implants))
            group.register(i as obj_item_implant_deathrattle);
        desc += I18n.T(" The implants are registered to the \"[]\" group.", group.name);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:737
public partial class obj_item_storage_box_stickers_syndie_kit : obj_item_storage_box_stickers
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:737
    public override async DmTask PopulateContents()
    {
        DmList types = subtypesof(typeof(obj_item_sticker_syndicate));

        for (int i = 1; i <= atom_storage.max_slots; i++)
        {
            Type type = pick(types) as Type;
            Dm.@new(type, this);
        }
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:744
public partial class obj_item_storage_box_syndie_kit_pinata : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:748
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_pinata_syndie>(this);
        Dm.@new<obj_item_storage_belt_grenade>(this);
        Dm.@new<obj_item_storage_belt_military_snack>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:753
public partial class obj_item_storage_box_syndie_kit_induction_kit : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:758
    public override async DmTask PopulateContents()
    {
        // Basic weaponry, so they have something to use.
        await give_weaponry();
        // The necessary equipment to help secure that disky.
        Dm.@new<obj_item_radio_headset_syndicate_alt>(this); // 5 TC / Free for nukies
        Dm.@new<obj_item_modular_computer_pda_nukeops>(this); // ?? TC / Free for nukies
        Dm.@new<obj_item_card_id_advanced_chameleon_elite>(this); // 2 TC / Free for nukies
        Type spess_suit = pick(GLOB.syndicate_space_suits_to_helmets) as Type;
        Dm.@new(spess_suit, this); // Above allows me to get the helmet from a variable on the object
        Type spess_helmet = GLOB.syndicate_space_suits_to_helmets[spess_suit] as Type;
        Dm.@new(spess_helmet, this); // 4 TC for the space gear
        Dm.@new<obj_item_tank_jetpack_harness>(this); // They kinda need this to fly to the cruiser.
        // Tacticool gear
        Dm.@new<obj_item_clothing_shoes_combat>(this);
        Dm.@new<obj_item_clothing_under_syndicate>(this);
        Dm.@new<obj_item_clothing_gloves_fingerless>(this);
        Dm.@new<obj_item_book_manual_nuclear>(this); // Very important
        // The most important part of the kit, the implant that gives them the syndicate faction.
        Dm.@new(implant_type, this);
        // Tactical map implant so they can see the minimap with the rest of the team.
        Dm.@new<obj_item_implanter_tacmap_nuclear>(this);
        // All in all, 6+3+3+2+5+2+4 = ~25 TC of 'miscellaneous' items.
        // This is a lot of value for 10 TC, but you have to keep in mind that you NEED someone to get this stuff station-side.
        // Pretty much all of it is a bad deal for reinforcements or yourself as they already have similar or good-enough alternatives.
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:753
public partial class obj_item_storage_box_syndie_kit_induction_kit : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:783
    public virtual async DmTask give_weaponry()
    {
        Dm.@new<obj_item_gun_ballistic_automatic_pistol_clandestine>(this); // 6 TC, but free for nukies
        Dm.@new<obj_item_ammo_box_magazine_m10mm_hp>(this); // 3 TC, a reward for the teamwork involved
        Dm.@new<obj_item_ammo_box_magazine_m10mm_ap>(this); // 3 TC, a reward for the teamwork involved
        Dm.@new<obj_item_pen_edagger>(this); // 2 TC
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:789
public partial class obj_item_storage_box_syndie_kit_induction_kit_clown : obj_item_storage_box_syndie_kit_induction_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:794
    public override async DmTask give_weaponry()
    {
        // Toy gun instead of real gun
        obj_item_gun_ballistic_automatic_pistol_toy_riot_clandestine gun = Dm.@new<obj_item_gun_ballistic_automatic_pistol_toy_riot_clandestine>(this);
        qdel(gun.pin);
        obj_item_firing_pin_clown_ultra new_pin = Dm.@new<obj_item_firing_pin_clown_ultra>();
        await new_pin.gun_insert(null, gun, TRUE);

        // You need this for the gun
        Dm.@new<obj_item_dnainjector_clumsymut>(this);
        // And a bonus
        Dm.@new<obj_item_reagent_containers_spray_waterflower_lube_super>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:876
public partial class obj_item_storage_box_syndie_kit_poster_box : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:883
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= poster_count; i++)
            Dm.@new<obj_item_poster_traitor>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:887
public partial class obj_item_storage_box_syndie_kit_cowboy : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:891
    public override async DmTask PopulateContents()
    {
        generate_items_inside(Dm.list(
            Dm.kv(typeof(obj_item_clothing_shoes_cowboy_black_syndicate), 1),
            Dm.kv(typeof(obj_item_clothing_head_cowboy_black_syndicate), 1),
            Dm.kv(typeof(obj_item_storage_belt_holster_nukie_cowboy_full), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_dutch_syndicate), 1),
            Dm.kv(typeof(obj_item_lighter_skull), 1),
            Dm.kv(typeof(obj_item_sbeacondrop_horse), 1),
            Dm.kv(typeof(obj_item_food_grown_apple), 1)), this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:902
public partial class obj_item_storage_box_syndicate_contract_kit : obj_item_storage_box_syndicate
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:908
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_modular_computer_pda_syndicate_contract_uplink>(this);
        Dm.@new<obj_item_storage_box_syndicate_contractor_loadout>(this);
        Dm.@new<obj_item_melee_baton_telescopic_contractor_baton>(this);
        // Paper guide is always last.
        Dm.@new<obj_item_paper_contractor_guide>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:915
public partial class obj_item_storage_box_syndicate_contractor_loadout : obj_item_storage_box_syndicate
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:921
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_mod_control_pre_equipped_infiltrator>(this);
        Dm.@new<obj_item_clothing_head_helmet_space_syndicate_contract>(this);
        Dm.@new<obj_item_clothing_suit_space_syndicate_contract>(this);
        Dm.@new<obj_item_clothing_under_chameleon>(this);
        Dm.@new<obj_item_clothing_mask_chameleon>(this);
        Dm.@new<obj_item_card_id_advanced_chameleon_elite>(this);
        Dm.@new<obj_item_clothing_glasses_thermal_syndi>(this);
        Dm.@new<obj_item_storage_toolbox_syndicate>(this);
        Dm.@new<obj_item_jammer>(this);
        Dm.@new<obj_item_storage_fancy_cigarettes_cigpack_syndicate>(this);
        Dm.@new<obj_item_lighter>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:934
public partial class obj_item_storage_box_syndicate_horse_box : obj_item_storage_box_syndicate
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:940
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_food_monkeycube_dangerous_horse>(this);
        Dm.@new<obj_item_slimepotion_sentience_nuclear_dangerous_horse>(this);
        Dm.@new<obj_item_food_grown_apple>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:945
public partial class obj_item_storage_box_syndicate_sleeping_carp : obj_item_storage_box_syndicate
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:945
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_book_granter_martial_carp>(this);
        Dm.@new<obj_item_clothing_head_costume_snakeeater>(this);
        Dm.@new<obj_item_clothing_under_costume_gi>(this);
        Dm.@new<obj_item_clothing_shoes_sandal>(this);
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:815
public partial class obj_item_implant_nuclear_operative : obj_item_implant
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:821
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        object dot = await base.implant(target, user, silent, force, __extra);
        if (!Dm.truthy(dot) || !ishuman(target) || target.mind == null)
            return FALSE;
        mob_living_carbon_human human_target = (mob_living_carbon_human)target;

        if (Dm.truthy(IS_NUKE_OP(human_target))) // this wont proc due to ..() but i guess its good as a just-in-case?
        {
            if (human_target == user)
            {
                to_chat(user, span_userdanger(I18n.T("You're already a nuclear operative, dumbass! The implant disintegrates within you! You feel sick...")));
                await human_target.Stun(10 * SECONDS);
                await ((datum_reagents)human_target.reagents).add_reagent(typeof(datum_reagent_toxin), 10);
                return FALSE;
            }
            else
            {
                to_chat(user, span_notice(I18n.T("You finish implanting [], but you don't really notice a difference. Huh.", human_target)));
                to_chat(human_target, span_userdanger(I18n.T("Nothing seems to really happen, but you start to feel a little ill..")));
                await ((datum_reagents)human_target.reagents).add_reagent(typeof(datum_reagent_toxin), 2);
                return FALSE;
            }
        }

        if (Dm.truthy(is_valid_inductee(target))) // GTFO. Technically not foolproof but making a heartbreaker or a paradox clone a nuke op sounds hilarious
        {
            to_chat(human_target, span_notice(I18n.T("Huh? Nothing happened? But you're starting to feel a little ill...")));
            await ((datum_reagents)human_target.reagents).add_reagent(typeof(datum_reagent_toxin), 15);
            return FALSE;
        }

        datum_antagonist_nukeop nuke_datum = await Dm.new_async(antag_datum_type) as datum_antagonist_nukeop;
        nuke_datum.send_to_spawnpoint = FALSE;
        nuke_datum.give_bonus_tc = FALSE;
        nuke_datum.nukeop_outfit = null;
        if (human_target.mind != null)
            await human_target.mind.add_antag_datum(nuke_datum);
        human_target.add_faction(ROLE_SYNDICATE);
        to_chat(human_target, span_warning(I18n.T("You are now a nuclear operative. Your main objective, if you were an antagonist and willing, is presumably to assist the nuclear operative team and secure the disk.")));
        to_chat(human_target, span_userdanger(I18n.T("This implant does NOT, in any way, brainwash you. If you were a normal crew member beforehand, forcibly implanted or otherwise, you are still one and cannot assist the nuclear operatives.")));
        return TRUE;
    }

    // tg: code/game/objects/items/storage/uplink_kits.dm:853
    public override async DmTask<object> removed(mob_living target = null, float? silent = null, float? special = null)
    {
        silent ??= FALSE;
        special ??= FALSE;
        object dot = await base.removed(target, silent, special);
        if (!Dm.truthy(dot) || !isliving(target))
            return FALSE;
        mob_living living_target = target;
        await living_target.mind.remove_antag_datum(typeof(datum_antagonist_nukeop));
        living_target.remove_faction(ROLE_SYNDICATE);
        to_chat(target, span_notice(I18n.T("You feel a little less nuclear.")));
        to_chat(target, span_userdanger(I18n.T("You're no longer identified as a nuclear operative! You are free to follow any valid goals you wish, even continuing to secure the disk. Just make sure neither any turrets nor operatives kill you on sight.")));
        return TRUE;
    }

    // tg: code/game/objects/items/storage/uplink_kits.dm:865
    public virtual object is_valid_inductee(mob_living target = null)
    {
        return target.is_antag();
    }
}

// tg: code/game/objects/items/storage/uplink_kits.dm:868
public partial class obj_item_implant_nuclear_operative_clown : obj_item_implant_nuclear_operative
{
    // tg: code/game/objects/items/storage/uplink_kits.dm:873
    public override object is_valid_inductee(mob_living target = null)
    {
        return HAS_MIND_TRAIT(target, TRAIT_CLUMSY) || HAS_MIND_TRAIT(target, TRAIT_NAIVE) || Dm.truthy(base.is_valid_inductee(target));
    }
}

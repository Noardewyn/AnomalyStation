// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only IS_RIGHT_INDEX (M2 P1: /mob/proc/get_inactive_hand_index), IS_LEFT_INDEX (M2 P2: the carbon's hand
// helpers), GLOB.any_suit_storage (M4c slice 2: the suit storage slot), GLOB.security_vest_allowed (M7x slice B: what hangs on
// an armour suit) and J1's detective_vest_allowed, security_wintercoat_allowed and personal_carry_allowed (job coats' allowed lists), syndicate N2c's
// improvised_firearm_allowed, gaps CL1's chaplain_suit_allowed and gaps MO1's mining_suit_allowed translated; the constants of this file are generated (Defines), and its other function-like macros come with
// inventory (M4a).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/inventory.dm:146  #define IS_RIGHT_INDEX(value) (value % 2 == 0)
    public static bool IS_RIGHT_INDEX(float value) => Dm.mod(value, 2) == 0;

    /// Checks if the value is "left" - same as ISODD, but used primarily for hand or foot index contexts
    // tg: code/__DEFINES/inventory.dm:148  #define IS_LEFT_INDEX(value) (value % 2 != 0)
    public static bool IS_LEFT_INDEX(float value) => Dm.mod(value, 2) != 0;
}

// tg: code/__DEFINES/inventory.dm:212  (M4c slice 2)
public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/inventory.dm:212  GLOBAL_LIST_INIT(any_suit_storage, typecacheof(list(...)))
    public DmList any_suit_storage;

    // tg: code/__DEFINES/inventory.dm:212  (GLOBAL_MANAGED(any_suit_storage, typecacheof(list(...))), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalany_suit_storage()
    {
        any_suit_storage = typecacheof(Dm.list(
            typeof(obj_item_clipboard),
            typeof(obj_item_flashlight),
            typeof(obj_item_tank_internals_emergency_oxygen),
            typeof(obj_item_tank_internals_plasmaman),
            typeof(obj_item_vaporizer),
            typeof(obj_item_lighter),
            typeof(obj_item_pen),
            typeof(obj_item_modular_computer_pda),
            typeof(obj_item_toy_plush),
            typeof(obj_item_radio),
            typeof(obj_item_storage_bag_books),
            typeof(obj_item_storage_fancy_cigarettes),
            typeof(obj_item_tank_jetpack_captain),
            typeof(obj_item_stack_spacecash),
            typeof(obj_item_storage_wallet),
            typeof(obj_item_folder),
            typeof(obj_item_storage_box_matches),
            typeof(obj_item_cigarette),
            typeof(obj_item_gun_energy_laser_bluetag),
            typeof(obj_item_gun_energy_laser_redtag),
            typeof(obj_item_storage_belt_holster),
            typeof(obj_item_storage_belt_sheath)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "any_suit_storage");
    }
}

// tg: code/__DEFINES/inventory.dm:260  (M7x slice B: the armour suits' allowed list, so that the officer's disabler hangs on the vest)
public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/inventory.dm:260  GLOBAL_LIST_INIT(security_vest_allowed, list(...))
    public DmList security_vest_allowed;

    // tg: code/__DEFINES/inventory.dm:260  (GLOBAL_MANAGED(security_vest_allowed, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsecurity_vest_allowed()
    {
        security_vest_allowed = Dm.list(
            typeof(obj_item_flashlight),
            typeof(obj_item_gun_ballistic),
            typeof(obj_item_gun_energy),
            typeof(obj_item_knife_combat),
            typeof(obj_item_melee_baton),
            typeof(obj_item_reagent_containers_spray_pepper),
            typeof(obj_item_restraints_handcuffs),
            typeof(obj_item_tank_internals_emergency_oxygen),
            typeof(obj_item_tank_internals_plasmaman),
            typeof(obj_item_vaporizer),
            typeof(obj_item_storage_belt_holster_detective),
            typeof(obj_item_storage_belt_holster_nukie),
            typeof(obj_item_storage_belt_holster_energy),
            typeof(obj_item_gun_ballistic_shotgun_automatic_combat_compact),
            typeof(obj_item_gun_energy_laser_scatter_shotty_compact),
            typeof(obj_item_pen_red_security)
            );
        Dm.append(ref gvars_datum_init_order, "security_vest_allowed");
    }
}

// tg: code/__DEFINES/inventory.dm:238  (J1, D298: the detective's trench coat and the job coats' allowed lists)
public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/inventory.dm:238  GLOBAL_LIST_INIT(detective_vest_allowed, list(...))
    public DmList detective_vest_allowed;

    // tg: code/__DEFINES/inventory.dm:238  (GLOBAL_MANAGED(detective_vest_allowed, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldetective_vest_allowed()
    {
        detective_vest_allowed = Dm.list(
            typeof(obj_item_detective_scanner),
            typeof(obj_item_flashlight),
            typeof(obj_item_gun_ballistic),
            typeof(obj_item_gun_energy),
            typeof(obj_item_lighter),
            typeof(obj_item_melee_baton),
            typeof(obj_item_reagent_containers_spray_pepper),
            typeof(obj_item_restraints_handcuffs),
            typeof(obj_item_storage_fancy_cigarettes),
            typeof(obj_item_taperecorder),
            typeof(obj_item_tank_internals_emergency_oxygen),
            typeof(obj_item_tank_internals_plasmaman),
            typeof(obj_item_vaporizer),
            typeof(obj_item_storage_belt_holster_detective),
            typeof(obj_item_storage_belt_holster_nukie),
            typeof(obj_item_storage_belt_holster_energy),
            typeof(obj_item_gun_ballistic_shotgun_automatic_combat_compact),
            typeof(obj_item_gun_energy_laser_scatter_shotty_compact)
            );
        Dm.append(ref gvars_datum_init_order, "detective_vest_allowed");
    }

    // tg: code/__DEFINES/inventory.dm:279  GLOBAL_LIST_INIT(security_wintercoat_allowed, list(...))
    public DmList security_wintercoat_allowed;

    // tg: code/__DEFINES/inventory.dm:279  (GLOBAL_MANAGED(security_wintercoat_allowed, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsecurity_wintercoat_allowed()
    {
        security_wintercoat_allowed = Dm.list(
            typeof(obj_item_gun_ballistic),
            typeof(obj_item_gun_energy),
            typeof(obj_item_melee_baton),
            typeof(obj_item_reagent_containers_spray_pepper),
            typeof(obj_item_restraints_handcuffs),
            typeof(obj_item_storage_belt_holster_detective),
            typeof(obj_item_storage_belt_holster_nukie),
            typeof(obj_item_storage_belt_holster_energy),
            typeof(obj_item_gun_ballistic_shotgun_automatic_combat_compact),
            typeof(obj_item_gun_energy_laser_scatter_shotty_compact)
            );
        Dm.append(ref gvars_datum_init_order, "security_wintercoat_allowed");
    }

    //Allowed list for all chaplain suits (except the honkmother robe)
    // tg: code/__DEFINES/inventory.dm:294  GLOBAL_LIST_INIT(chaplain_suit_allowed, list(...)) (gaps CL1: the imperium monk's robe)
    public DmList chaplain_suit_allowed;

    // tg: code/__DEFINES/inventory.dm:294  (GLOBAL_MANAGED(chaplain_suit_allowed, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalchaplain_suit_allowed()
    {
        chaplain_suit_allowed = Dm.list(
            typeof(obj_item_book_bible),
            typeof(obj_item_nullrod),
            typeof(obj_item_reagent_containers_cup_glass_bottle_holywater),
            typeof(obj_item_storage_fancy_candle_box),
            typeof(obj_item_flashlight_flare_candle),
            typeof(obj_item_tank_internals_emergency_oxygen),
            typeof(obj_item_tank_internals_plasmaman),
            typeof(obj_item_vaporizer),
            typeof(obj_item_gun_ballistic_bow_divine),
            typeof(obj_item_gun_ballistic_revolver_chaplain),
            typeof(obj_item_toy_plush_carpplushie_nullrod),
            typeof(obj_item_melee_energy_sword_nullrod)
            );
        Dm.append(ref gvars_datum_init_order, "chaplain_suit_allowed");
    }

    // tg: code/__DEFINES/inventory.dm:311  GLOBAL_LIST_INIT(mining_suit_allowed, list(...)) (gaps MO1: the mining MOD theme's suit storage)
    public DmList mining_suit_allowed;

    // tg: code/__DEFINES/inventory.dm:311  (GLOBAL_MANAGED(mining_suit_allowed, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmining_suit_allowed()
    {
        mining_suit_allowed = Dm.list(
            typeof(obj_item_t_scanner_adv_mining_scanner),
            typeof(obj_item_melee_cleaving_saw),
            typeof(obj_item_climbing_hook),
            typeof(obj_item_flashlight),
            typeof(obj_item_grapple_gun),
            typeof(obj_item_tank_internals),
            typeof(obj_item_vaporizer),
            typeof(obj_item_gun_energy_recharge_kinetic_accelerator),
            typeof(obj_item_kinetic_crusher),
            typeof(obj_item_knife),
            typeof(obj_item_mining_scanner),
            typeof(obj_item_organ_monster_core),
            typeof(obj_item_storage_bag_ore),
            typeof(obj_item_pickaxe),
            typeof(obj_item_resonator),
            typeof(obj_item_spear),
            typeof(obj_item_gun_ballistic_bow_ashenbow)
            );
        Dm.append(ref gvars_datum_init_order, "mining_suit_allowed");
    }

    // tg: code/__DEFINES/inventory.dm:333  GLOBAL_LIST_INIT(personal_carry_allowed, list(...))
    public DmList personal_carry_allowed;

    // tg: code/__DEFINES/inventory.dm:333  (GLOBAL_MANAGED(personal_carry_allowed, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpersonal_carry_allowed()
    {
        personal_carry_allowed = Dm.list(
            typeof(obj_item_storage_belt_holster),
            typeof(obj_item_gun_ballistic_automatic_pistol),
            typeof(obj_item_gun_ballistic_revolver),
            typeof(obj_item_gun_energy_disabler_smoothbore)
            );
        Dm.append(ref gvars_datum_init_order, "personal_carry_allowed");
    }

    // tg: code/__DEFINES/inventory.dm:342  GLOBAL_LIST_INIT(improvised_firearm_allowed, list(...)) (syndicate N2c: the overwatch agent's
    // letterman jacket)
    public DmList improvised_firearm_allowed;

    // tg: code/__DEFINES/inventory.dm:342  (GLOBAL_MANAGED(improvised_firearm_allowed, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalimprovised_firearm_allowed()
    {
        improvised_firearm_allowed = Dm.list(
            typeof(obj_item_gun_ballistic_rifle_boltaction_pipegun),
            typeof(obj_item_gun_energy_laser_musket),
            typeof(obj_item_gun_energy_disabler_smoothbore)
            );
        Dm.append(ref gvars_datum_init_order, "improvised_firearm_allowed");
    }
}

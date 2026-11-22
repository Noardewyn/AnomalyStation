// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/right_and_wrong.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the four lists summoned_all_guns (gaps ST5: the wildcard mystery box), summoned_guns, summoned_magic and
// summoned_special_magic translated (M8 slice G: the fake items of the
// hallucination read them); the summon spells and the objective lists are the wizard's (M10).

public partial class datum_controller_global_vars
{
    // tg: code/modules/spells/spell_types/right_and_wrong.dm:14  GLOBAL_LIST_INIT(summoned_all_guns, get_sane_item_types(/obj/item/gun))
    // (gaps ST5: the wildcard mystery box's pool)
    public DmList summoned_all_guns;

    // tg: code/modules/spells/spell_types/right_and_wrong.dm:14  (GLOBAL_MANAGED(summoned_all_guns, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsummoned_all_guns()
    {
        summoned_all_guns = get_sane_item_types(typeof(obj_item_gun)) as DmList;
        Dm.append(ref gvars_datum_init_order, "summoned_all_guns");
    }

    // tg: code/modules/spells/spell_types/right_and_wrong.dm:16  GLOBAL_LIST_INIT(summoned_guns, list(...))
    public DmList summoned_guns;

    // tg: code/modules/spells/spell_types/right_and_wrong.dm:16  (GLOBAL_MANAGED(summoned_guns, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsummoned_guns()
    {
        summoned_guns = Dm.list(
            typeof(obj_item_gun_ballistic_automatic_ar),
            typeof(obj_item_gun_ballistic_automatic_battle_rifle),
            typeof(obj_item_gun_ballistic_automatic_c20r_unrestricted),
            typeof(obj_item_gun_ballistic_automatic_gyropistol),
            typeof(obj_item_gun_ballistic_automatic_l6_saw_unrestricted),
            typeof(obj_item_gun_ballistic_automatic_laser),
            typeof(obj_item_gun_ballistic_automatic_m90_unrestricted),
            typeof(obj_item_gun_ballistic_automatic_mini_uzi),
            typeof(obj_item_gun_ballistic_automatic_pistol),
            typeof(obj_item_gun_ballistic_automatic_pistol_aps),
            typeof(obj_item_gun_ballistic_automatic_pistol_clandestine_fisher),
            typeof(obj_item_gun_ballistic_automatic_pistol_deagle),
            typeof(obj_item_gun_ballistic_automatic_pistol_deagle_regal),
            typeof(obj_item_gun_ballistic_automatic_pistol_doohickey),
            typeof(obj_item_gun_ballistic_automatic_pistol_m1911),
            typeof(obj_item_gun_ballistic_automatic_proto_unrestricted),
            typeof(obj_item_gun_ballistic_automatic_smartgun),
            typeof(obj_item_gun_ballistic_automatic_tommygun),
            typeof(obj_item_gun_ballistic_automatic_wt550),
            typeof(obj_item_gun_ballistic_revolver),
            typeof(obj_item_gun_ballistic_revolver_badass),
            typeof(obj_item_gun_ballistic_revolver_c38),
            typeof(obj_item_gun_ballistic_revolver_nagant),
            typeof(obj_item_gun_ballistic_rifle_boltaction),
            typeof(obj_item_gun_ballistic_rifle_boltaction_donkrifle),
            typeof(obj_item_gun_ballistic_rifle_boltaction_harpoon),
            typeof(obj_item_gun_ballistic_rifle_rebarxbow_syndie),
            typeof(obj_item_gun_ballistic_rifle_sniper_rifle),
            typeof(obj_item_gun_ballistic_rocketlauncher_unrestricted),
            typeof(obj_item_gun_ballistic_shotgun_automatic_combat),
            typeof(obj_item_gun_ballistic_shotgun_bulldog_unrestricted),
            typeof(obj_item_gun_ballistic_shotgun_doublebarrel),
            typeof(obj_item_gun_ballistic_shotgun_hook),
            typeof(obj_item_gun_ballistic_shotgun_lethal),
            typeof(obj_item_gun_energy_alien),
            typeof(obj_item_gun_energy_disabler),
            typeof(obj_item_gun_energy_disabler_smg),
            typeof(obj_item_gun_energy_e_gun),
            typeof(obj_item_gun_energy_e_gun_advtaser),
            typeof(obj_item_gun_energy_e_gun_nuclear),
            typeof(obj_item_gun_energy_laser),
            typeof(obj_item_gun_energy_laser_soul),
            typeof(obj_item_gun_energy_laser_pistol),
            typeof(obj_item_gun_energy_laser_assault),
            typeof(obj_item_gun_energy_laser_carbine),
            typeof(obj_item_gun_energy_laser_cybersun_unrestricted),
            typeof(obj_item_gun_energy_laser_hellgun),
            typeof(obj_item_gun_energy_laser_scatter),
            typeof(obj_item_gun_energy_laser_thermal),
            typeof(obj_item_gun_energy_laser_thermal_cryo),
            typeof(obj_item_gun_energy_laser_thermal_inferno),
            typeof(obj_item_gun_energy_lasercannon),
            typeof(obj_item_gun_energy_mindflayer),
            typeof(obj_item_gun_energy_plasmacutter_adv),
            typeof(obj_item_gun_energy_pulse),
            typeof(obj_item_gun_energy_pulse_carbine),
            typeof(obj_item_gun_energy_recharge_ebow_large),
            typeof(obj_item_gun_energy_recharge_kinetic_accelerator),
            typeof(obj_item_gun_medbeam));
        Dm.append(ref gvars_datum_init_order, "summoned_guns");
    }

    // tg: code/modules/spells/spell_types/right_and_wrong.dm:78  GLOBAL_LIST_INIT(summoned_magic, list(...))
    public DmList summoned_magic;

    // tg: code/modules/spells/spell_types/right_and_wrong.dm:78  (GLOBAL_MANAGED(summoned_magic, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsummoned_magic()
    {
        summoned_magic = Dm.list(
            typeof(obj_item_book_granter_action_spell_fireball),
            typeof(obj_item_book_granter_action_spell_smoke),
            typeof(obj_item_book_granter_action_spell_blind),
            typeof(obj_item_book_granter_action_spell_mindswap),
            typeof(obj_item_book_granter_action_spell_forcewall),
            typeof(obj_item_book_granter_action_spell_knock),
            typeof(obj_item_book_granter_action_spell_barnyard),
            typeof(obj_item_book_granter_action_spell_charge),
            typeof(obj_item_book_granter_action_spell_summonitem),
            typeof(obj_item_book_granter_action_spell_lightningbolt),
            typeof(obj_item_gun_magic_staff_babel),
            typeof(obj_item_gun_magic_staff_door),
            typeof(obj_item_gun_magic_staff_healing),
            typeof(obj_item_gun_magic_wand_animate),
            typeof(obj_item_gun_magic_wand_babel),
            typeof(obj_item_gun_magic_wand_bald),
            typeof(obj_item_gun_magic_wand_death),
            typeof(obj_item_gun_magic_wand_door),
            typeof(obj_item_gun_magic_wand_fireball),
            typeof(obj_item_gun_magic_wand_freeze),
            typeof(obj_item_gun_magic_wand_hallucination),
            typeof(obj_item_gun_magic_wand_levitate),
            typeof(obj_item_gun_magic_wand_nothing),
            typeof(obj_item_gun_magic_wand_pax),
            typeof(obj_item_gun_magic_wand_pizza),
            typeof(obj_item_gun_magic_wand_plague),
            typeof(obj_item_gun_magic_wand_polymorph),
            typeof(obj_item_gun_magic_wand_prank),
            typeof(obj_item_gun_magic_wand_rebel),
            typeof(obj_item_gun_magic_wand_repulse),
            typeof(obj_item_gun_magic_wand_resurrection),
            typeof(obj_item_gun_magic_wand_swap),
            typeof(obj_item_gun_magic_wand_teleport),
            typeof(obj_item_gun_magic_wand_tentacles),
            typeof(obj_item_gun_magic_wand_zap),
            typeof(obj_item_immortality_talisman),
            typeof(obj_item_melee_ghost_sword),
            typeof(obj_item_scrying),
            typeof(obj_item_warp_whistle));
        Dm.append(ref gvars_datum_init_order, "summoned_magic");
    }

    // tg: code/modules/spells/spell_types/right_and_wrong.dm:120  GLOBAL_LIST_INIT(summoned_special_magic, list(...))
    public DmList summoned_special_magic;

    // tg: code/modules/spells/spell_types/right_and_wrong.dm:120  (GLOBAL_MANAGED(summoned_special_magic, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsummoned_special_magic()
    {
        summoned_special_magic = Dm.list(
            typeof(obj_item_gun_magic_staff_change),
            typeof(obj_item_gun_magic_staff_animate),
            typeof(obj_item_storage_belt_wands_full),
            typeof(obj_item_storage_belt_wands_full_discount),
            typeof(obj_item_antag_spawner_contract),
            typeof(obj_item_gun_magic_staff_chaos),
            typeof(obj_item_necromantic_stone));
        Dm.append(ref gvars_datum_init_order, "summoned_special_magic");
    }
}

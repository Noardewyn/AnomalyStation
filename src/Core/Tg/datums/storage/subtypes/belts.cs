// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/belts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T1 (D164): the whole file. Each belt's storage lists what the belt holds (set_holdable); the vars of the storage
// types (slots, sizes, sounds) are the generated var blocks. GLOB.tool_items is the toolbox's local copy of the list
// (obj_item_storage_toolbox.tool_items__gap).

// tg: code/datums/storage/subtypes/belts.dm:5
public partial class datum_storage_utility_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:5
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        DmList tool_items__plus = obj_item_storage_toolbox.tool_items__gap(); // GLOB.tool_items + list(...) (a new list, belts.dm:7)
        Dm.append(ref tool_items__plus, Dm.list(
            typeof(obj_item_clothing_gloves),
            typeof(obj_item_radio),
            typeof(obj_item_melee_sickly_blade_lock),
            typeof(obj_item_reagent_containers_cup_soda_cans)
        ));
        set_holdable(tool_items__plus);
    }
}

// tg: code/datums/storage/subtypes/belts.dm:18
public partial class datum_storage_medical_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:18
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(can_hold_list: Dm.list(
            typeof(obj_item_bikehorn_rubberducky),
            typeof(obj_item_blood_filter),
            typeof(obj_item_blood_scanner),
            typeof(obj_item_bonesetter),
            typeof(obj_item_cautery),
            typeof(obj_item_circular_saw),
            typeof(obj_item_clothing_glasses),
            typeof(obj_item_clothing_gloves),
            typeof(obj_item_clothing_head_utility_surgerycap),
            typeof(obj_item_clothing_mask_breath),
            typeof(obj_item_clothing_mask_muzzle),
            typeof(obj_item_clothing_mask_surgical),
            typeof(obj_item_clothing_neck_stethoscope),
            typeof(obj_item_construction_plumbing),
            typeof(obj_item_dnainjector),
            typeof(obj_item_extinguisher_mini),
            typeof(obj_item_flashlight_pen),
            typeof(obj_item_flesh_shears),
            typeof(obj_item_geiger_counter),
            typeof(obj_item_gun_syringe_syndicate),
            typeof(obj_item_healthanalyzer),
            typeof(obj_item_hemostat),
            typeof(obj_item_holosign_creator_medical),
            typeof(obj_item_implant),
            typeof(obj_item_implantcase),
            typeof(obj_item_implanter),
            typeof(obj_item_knife_ritual),
            typeof(obj_item_lazarus_injector),
            typeof(obj_item_lighter),
            typeof(obj_item_pinpointer_crew),
            typeof(obj_item_plunger),
            typeof(obj_item_radio),
            typeof(obj_item_reagent_containers_applicator),
            typeof(obj_item_reagent_containers_blood),
            typeof(obj_item_reagent_containers_cup_beaker),
            typeof(obj_item_reagent_containers_cup_bottle),
            typeof(obj_item_reagent_containers_cup_tube),
            typeof(obj_item_reagent_containers_dropper),
            typeof(obj_item_reagent_containers_hypospray),
            typeof(obj_item_reagent_containers_medigel),
            typeof(obj_item_reagent_containers_spray),
            typeof(obj_item_reagent_containers_syringe),
            typeof(obj_item_reflexhammer),
            typeof(obj_item_retractor),
            typeof(obj_item_scalpel),
            typeof(obj_item_sensor_device),
            typeof(obj_item_shears),
            typeof(obj_item_stack_medical),
            typeof(obj_item_stack_medical_wrap_sticky_tape), //surgical tape
            typeof(obj_item_stamp),
            typeof(obj_item_storage_fancy_cigarettes),
            typeof(obj_item_storage_pill_bottle),
            typeof(obj_item_surgical_drapes), //for true paramedics
            typeof(obj_item_surgicaldrill),
            typeof(obj_item_tank_internals_emergency_oxygen),
            typeof(obj_item_tourniquet),
            typeof(obj_item_wrench_medical)
        ));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:87
public partial class datum_storage_security_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:87
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_ammo_box),
            typeof(obj_item_ammo_casing_shotgun),
            typeof(obj_item_assembly_flash_handheld),
            typeof(obj_item_assembly_flash_hypnotic),
            typeof(obj_item_clothing_glasses),
            typeof(obj_item_clothing_gloves),
            typeof(obj_item_flashlight_seclite),
            typeof(obj_item_food_donut),
            typeof(obj_item_grenade),
            typeof(obj_item_holosign_creator_security),
            typeof(obj_item_knife_combat),
            typeof(obj_item_melee_baton),
            typeof(obj_item_radio),
            typeof(obj_item_reagent_containers_spray_pepper),
            typeof(obj_item_restraints_handcuffs),
            typeof(obj_item_restraints_legcuffs_bola)
        ));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:117
public partial class datum_storage_mining_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:117
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_analyzer),
            typeof(obj_item_clothing_gloves),
            typeof(obj_item_crowbar),
            typeof(obj_item_extinguisher_mini),
            typeof(obj_item_flashlight),
            typeof(obj_item_gps),
            typeof(obj_item_mining_stabilizer),
            typeof(obj_item_key_lasso),
            typeof(obj_item_knife),
            typeof(obj_item_lighter),
            typeof(obj_item_mining_scanner),
            typeof(obj_item_multitool),
            typeof(obj_item_organ_monster_core),
            typeof(obj_item_pickaxe),
            typeof(obj_item_radio),
            typeof(obj_item_reagent_containers_cup_glass),
            typeof(obj_item_reagent_containers_cup_glass_bottle),
            typeof(obj_item_reagent_containers_hypospray),
            typeof(obj_item_reagent_containers_applicator_pill),
            typeof(obj_item_resonator),
            typeof(obj_item_screwdriver),
            typeof(obj_item_shovel),
            typeof(obj_item_stack_cable_coil),
            typeof(obj_item_stack_marker_beacon),
            typeof(obj_item_stack_candela_beacon),
            typeof(obj_item_stack_medical),
            typeof(obj_item_stack_ore),
            typeof(obj_item_stack_sheet_animalhide),
            typeof(obj_item_stack_sheet_bone),
            typeof(obj_item_stack_sheet_sinew),
            typeof(obj_item_storage_bag_ore),
            typeof(obj_item_storage_fancy_cigarettes),
            typeof(obj_item_storage_pill_bottle),
            typeof(obj_item_survivalcapsule),
            typeof(obj_item_t_scanner_adv_mining_scanner),
            typeof(obj_item_weldingtool),
            typeof(obj_item_wirecutters),
            typeof(obj_item_wrench),
            typeof(obj_item_wormhole_jaunter),
            typeof(obj_item_skeleton_key),
            typeof(obj_item_crusher_trophy)
        ));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:171
public partial class datum_storage_soulstone_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:171
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_soulstone));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:179
public partial class datum_storage_champion_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:179
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_clothing_mask_luchador));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:191
public partial class datum_storage_military_belt_snack : datum_storage_military_belt
{
    // tg: code/datums/storage/subtypes/belts.dm:191
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_food),
            typeof(obj_item_reagent_containers_cup_glass)
        ));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:209
public partial class datum_storage_grenade_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:209
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_food_grown_cherry_bomb),
            typeof(obj_item_food_grown_firelemon),
            typeof(obj_item_grenade),
            typeof(obj_item_grenade_c4),
            typeof(obj_item_lighter),
            typeof(obj_item_multitool),
            typeof(obj_item_reagent_containers_cup_glass_bottle_molotov),
            typeof(obj_item_screwdriver)
        ));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:226
public partial class datum_storage_wands_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:226
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_gun_magic_wand));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:234
public partial class datum_storage_janitor_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:234
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_access_key),
            typeof(obj_item_assembly_mousetrap),
            typeof(obj_item_clothing_gloves),
            typeof(obj_item_flashlight),
            typeof(obj_item_forcefield_projector),
            typeof(obj_item_grenade_chem_grenade),
            typeof(obj_item_holosign_creator),
            typeof(obj_item_key_janitor),
            typeof(obj_item_lightreplacer),
            typeof(obj_item_melee_flyswatter),
            typeof(obj_item_paint_paint_remover),
            typeof(obj_item_plunger),
            typeof(obj_item_pushbroom),
            typeof(obj_item_reagent_containers_spray),
            typeof(obj_item_soap),
            typeof(obj_item_wirebrush)
        ));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:263
public partial class datum_storage_bandolier_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:263
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_ammo_casing_strilka310),
            typeof(obj_item_ammo_casing_shotgun),
            typeof(obj_item_ammo_casing_c357),
            typeof(obj_item_ammo_casing_junk),
            typeof(obj_item_ammo_casing_a40mm)
        ));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:286
public partial class datum_storage_sabre_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:286
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_melee_sabre));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:297
public partial class datum_storage_green_sabre_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:297
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_melee_parsnip_sabre));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:307
public partial class datum_storage_gladius_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:307
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_claymore_gladius));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:318
public partial class datum_storage_katana_sheath : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:318
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_katana));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:329
public partial class datum_storage_ninja_sheath : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:329
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_energy_katana));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:340
public partial class datum_storage_hanzo_sheath : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:340
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_nullrod_claymore_katana));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:351
public partial class datum_storage_toy_sheath : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:351
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_toy_katana));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:359
public partial class datum_storage_plant_belt : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:359
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_cultivator),
            typeof(obj_item_geneshears),
            typeof(obj_item_graft),
            typeof(obj_item_gun_energy_floragun),
            typeof(obj_item_hatchet),
            typeof(obj_item_plant_analyzer),
            typeof(obj_item_reagent_containers_cup_beaker),
            typeof(obj_item_reagent_containers_cup_bottle),
            typeof(obj_item_reagent_containers_cup_tube),
            typeof(obj_item_reagent_containers_spray_pestspray),
            typeof(obj_item_reagent_containers_spray_plantbgone),
            typeof(obj_item_secateurs),
            typeof(obj_item_seeds),
            typeof(obj_item_shovel_spade)
        ));
    }
}

// tg: code/datums/storage/subtypes/belts.dm:382
public partial class datum_storage_unfathomable_curio : datum_storage
{
    // tg: code/datums/storage/subtypes/belts.dm:382
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_ammo_box_speedloader_strilka310_lionhunter),
            typeof(obj_item_heretic_labyrinth_handbook),
            typeof(obj_item_bodypart), // Bodyparts are often used in rituals.
            typeof(obj_item_clothing_neck_eldritch_amulet),
            typeof(obj_item_clothing_neck_heretic_focus),
            typeof(obj_item_codex_cicatrix),
            typeof(obj_item_eldritch_potion),
            typeof(obj_item_food_grown_flower_poppy), // Used to regain a Living Heart.
            typeof(obj_item_food_grown_flower_harebell), // Used to reroll targets
            typeof(obj_item_melee_rune_carver),
            typeof(obj_item_melee_sickly_blade),
            typeof(obj_item_organ), // Organs are also often used in rituals.
            typeof(obj_item_reagent_containers_cup_beaker_eldritch),
            typeof(obj_item_stack_sheet_glass) // Glass is often used by moon heretics
        ));
    }
}

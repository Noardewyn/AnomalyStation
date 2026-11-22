// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/belt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: M7 slice T1 (D164): the base belt (:2-35, suicide_act is a live-partial stub, M10) and the utility belts
// (:37-190) with their PopulateContents and get_types_to_preload; M8 slice C: the EMT and ERT medical belts (:202-254); gaps SR1
// (D333): the rest of the file but the healing mining belt (its legion cores wait on mining, not-translated.md section 208): the
// mining vendor's, soulstone, abductor, assault, janitor and fannypack contents, the champion's belt, the snack rigs, the wand belts
// and the wand bandolier, the sheaths (the alt-click draw, the -full sprites, the resheath cooldown) and their counterattack action
// with its gun-powered kind. The belts that are var-only subtypes of the base come live with it and run tg's storage lists
// (datums/storage/subtypes/belts.dm). The elements of Initialize (attack_equip, strip_accessible_storage) are deferred leaves, as the
// backpack's (M9). The worn belt of a mob shows no tools in tg (human_update_icons.dm:348 draws the worn icon only): the tools show
// on the belt object itself (update_overlays).

// tg: code/game/objects/items/storage/belt.dm:2
public partial class obj_item_storage_belt : obj_item_storage
{
    // tg: code/game/objects/items/storage/belt.dm:19  suicide_act: live-partial (M10).

    // tg: code/game/objects/items/storage/belt.dm:23
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(content_overlays))
            return dot;
        foreach (obj_item I in Dm.each<obj_item>(contents))
            Dm.append(ref dot, I.get_belt_overlay());
        return dot;
    }

    // tg: code/game/objects/items/storage/belt.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/storage/belt.dm:32", "Initialize: AddElement(/datum/element/attack_equip) and AddElement(/datum/element/strip_accessible_storage) (equipping by click and the strip menu, M10+)");
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/items/storage/belt.dm:37 and :49: /utility and /utility/chief have no procs, so they are var-only
// subtypes (generated, live with the base belt).

// tg: code/game/objects/items/storage/belt.dm:56
public partial class obj_item_storage_belt_utility_chief_full : obj_item_storage_belt_utility_chief
{
    // tg: code/game/objects/items/storage/belt.dm:58
    public override async DmTask PopulateContents()
    {
        await SSwardrobe.provide_type(typeof(obj_item_screwdriver_power), this);
        await SSwardrobe.provide_type(typeof(obj_item_crowbar_power), this);
        await SSwardrobe.provide_type(typeof(obj_item_weldingtool_experimental), this);
        await SSwardrobe.provide_type(typeof(obj_item_multitool), this);
        await SSwardrobe.provide_type(typeof(obj_item_stack_cable_coil), this);
        await SSwardrobe.provide_type(typeof(obj_item_extinguisher_mini), this);
        await SSwardrobe.provide_type(typeof(obj_item_analyzer), this);
        //much roomier now that we've managed to remove two tools
    }

    // tg: code/game/objects/items/storage/belt.dm:68
    public override object get_types_to_preload()
    {
        DmList to_preload = Dm.list(); //Yes this is a pain. Yes this is the point
        Dm.append(ref to_preload, typeof(obj_item_screwdriver_power));
        Dm.append(ref to_preload, typeof(obj_item_crowbar_power));
        Dm.append(ref to_preload, typeof(obj_item_weldingtool_experimental));
        Dm.append(ref to_preload, typeof(obj_item_multitool));
        Dm.append(ref to_preload, typeof(obj_item_stack_cable_coil));
        Dm.append(ref to_preload, typeof(obj_item_extinguisher_mini));
        Dm.append(ref to_preload, typeof(obj_item_analyzer));
        return to_preload;
    }
}

// tg: code/game/objects/items/storage/belt.dm:79
public partial class obj_item_storage_belt_utility_full : obj_item_storage_belt_utility
{
    // tg: code/game/objects/items/storage/belt.dm:79
    public override async DmTask PopulateContents()
    {
        await SSwardrobe.provide_type(typeof(obj_item_screwdriver), this);
        await SSwardrobe.provide_type(typeof(obj_item_wrench), this);
        await SSwardrobe.provide_type(typeof(obj_item_weldingtool), this);
        await SSwardrobe.provide_type(typeof(obj_item_crowbar), this);
        await SSwardrobe.provide_type(typeof(obj_item_wirecutters), this);
        await SSwardrobe.provide_type(typeof(obj_item_multitool), this);
        await SSwardrobe.provide_type(typeof(obj_item_stack_cable_coil), this);
    }

    // tg: code/game/objects/items/storage/belt.dm:88
    public override object get_types_to_preload()
    {
        DmList to_preload = Dm.list(); //Yes this is a pain. Yes this is the point
        Dm.append(ref to_preload, typeof(obj_item_screwdriver));
        Dm.append(ref to_preload, typeof(obj_item_wrench));
        Dm.append(ref to_preload, typeof(obj_item_weldingtool));
        Dm.append(ref to_preload, typeof(obj_item_crowbar));
        Dm.append(ref to_preload, typeof(obj_item_wirecutters));
        Dm.append(ref to_preload, typeof(obj_item_multitool));
        Dm.append(ref to_preload, typeof(obj_item_stack_cable_coil));
        return to_preload;
    }
}

// tg: code/game/objects/items/storage/belt.dm:100
public partial class obj_item_storage_belt_utility_full_powertools : obj_item_storage_belt_utility_full
{
    // tg: code/game/objects/items/storage/belt.dm:102
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver_power>(this); // tg: code/game/objects/items/storage/belt.dm:103
        Dm.@new<obj_item_crowbar_power>(this); // tg: code/game/objects/items/storage/belt.dm:104
        Dm.@new<obj_item_weldingtool_experimental>(this); // tg: code/game/objects/items/storage/belt.dm:105
        Dm.@new<obj_item_multitool>(this); // tg: code/game/objects/items/storage/belt.dm:106
        Dm.@new<obj_item_holosign_creator_atmos>(this); // tg: code/game/objects/items/storage/belt.dm:107
        Dm.@new<obj_item_extinguisher_mini>(this); // tg: code/game/objects/items/storage/belt.dm:108
        Dm.@new<obj_item_stack_cable_coil>(this); // tg: code/game/objects/items/storage/belt.dm:109
    }
}

// tg: code/game/objects/items/storage/belt.dm:111
public partial class obj_item_storage_belt_utility_full_powertools_rcd : obj_item_storage_belt_utility_full_powertools
{
    // tg: code/game/objects/items/storage/belt.dm:111
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver_power>(this); // tg: code/game/objects/items/storage/belt.dm:112
        Dm.@new<obj_item_crowbar_power>(this); // tg: code/game/objects/items/storage/belt.dm:113
        Dm.@new<obj_item_weldingtool_experimental>(this); // tg: code/game/objects/items/storage/belt.dm:114
        Dm.@new<obj_item_multitool>(this); // tg: code/game/objects/items/storage/belt.dm:115
        Dm.@new<obj_item_construction_rcd_loaded_upgraded>(this); // tg: code/game/objects/items/storage/belt.dm:116
        Dm.@new<obj_item_extinguisher_mini>(this); // tg: code/game/objects/items/storage/belt.dm:117
        Dm.@new<obj_item_stack_cable_coil>(this); // tg: code/game/objects/items/storage/belt.dm:118
    }
}

// tg: code/game/objects/items/storage/belt.dm:120
public partial class obj_item_storage_belt_utility_full_engi : obj_item_storage_belt_utility_full
{
    // tg: code/game/objects/items/storage/belt.dm:120
    public override async DmTask PopulateContents()
    {
        await SSwardrobe.provide_type(typeof(obj_item_screwdriver), this);
        await SSwardrobe.provide_type(typeof(obj_item_wrench), this);
        await SSwardrobe.provide_type(typeof(obj_item_weldingtool_largetank), this);
        await SSwardrobe.provide_type(typeof(obj_item_crowbar), this);
        await SSwardrobe.provide_type(typeof(obj_item_wirecutters), this);
        await SSwardrobe.provide_type(typeof(obj_item_multitool), this);
        await SSwardrobe.provide_type(typeof(obj_item_stack_cable_coil), this);
    }

    // tg: code/game/objects/items/storage/belt.dm:129
    public override object get_types_to_preload()
    {
        DmList to_preload = Dm.list(); //Yes this is a pain. Yes this is the point
        Dm.append(ref to_preload, typeof(obj_item_screwdriver));
        Dm.append(ref to_preload, typeof(obj_item_wrench));
        Dm.append(ref to_preload, typeof(obj_item_weldingtool_largetank));
        Dm.append(ref to_preload, typeof(obj_item_crowbar));
        Dm.append(ref to_preload, typeof(obj_item_wirecutters));
        Dm.append(ref to_preload, typeof(obj_item_multitool));
        Dm.append(ref to_preload, typeof(obj_item_stack_cable_coil));
        return to_preload;
    }
}

// tg: code/game/objects/items/storage/belt.dm:140
public partial class obj_item_storage_belt_utility_atmostech : obj_item_storage_belt_utility
{
    // tg: code/game/objects/items/storage/belt.dm:140
    public override async DmTask PopulateContents()
    {
        await SSwardrobe.provide_type(typeof(obj_item_screwdriver), this);
        await SSwardrobe.provide_type(typeof(obj_item_wrench), this);
        await SSwardrobe.provide_type(typeof(obj_item_weldingtool), this);
        await SSwardrobe.provide_type(typeof(obj_item_crowbar), this);
        await SSwardrobe.provide_type(typeof(obj_item_wirecutters), this);
        await SSwardrobe.provide_type(typeof(obj_item_t_scanner), this);
        await SSwardrobe.provide_type(typeof(obj_item_extinguisher_mini), this);
    }

    // tg: code/game/objects/items/storage/belt.dm:149
    public override object get_types_to_preload()
    {
        DmList to_preload = Dm.list(); //Yes this is a pain. Yes this is the point
        Dm.append(ref to_preload, typeof(obj_item_screwdriver));
        Dm.append(ref to_preload, typeof(obj_item_wrench));
        Dm.append(ref to_preload, typeof(obj_item_weldingtool));
        Dm.append(ref to_preload, typeof(obj_item_crowbar));
        Dm.append(ref to_preload, typeof(obj_item_wirecutters));
        Dm.append(ref to_preload, typeof(obj_item_t_scanner));
        Dm.append(ref to_preload, typeof(obj_item_extinguisher_mini));
        return to_preload;
    }
}

// tg: code/game/objects/items/storage/belt.dm:160
public partial class obj_item_storage_belt_utility_full_inducer : obj_item_storage_belt_utility_full
{
    // tg: code/game/objects/items/storage/belt.dm:160
    public override async DmTask PopulateContents()
    {
        await SSwardrobe.provide_type(typeof(obj_item_screwdriver), this);
        await SSwardrobe.provide_type(typeof(obj_item_wrench), this);
        await SSwardrobe.provide_type(typeof(obj_item_weldingtool), this);
        await SSwardrobe.provide_type(typeof(obj_item_crowbar_red), this);
        await SSwardrobe.provide_type(typeof(obj_item_wirecutters), this);
        await SSwardrobe.provide_type(typeof(obj_item_multitool), this);
        await SSwardrobe.provide_type(typeof(obj_item_inducer), this);
    }

    // tg: code/game/objects/items/storage/belt.dm:169
    public override object get_types_to_preload()
    {
        DmList to_preload = Dm.list(); //Yes this is a pain. Yes this is the point
        Dm.append(ref to_preload, typeof(obj_item_screwdriver));
        Dm.append(ref to_preload, typeof(obj_item_wrench));
        Dm.append(ref to_preload, typeof(obj_item_weldingtool));
        Dm.append(ref to_preload, typeof(obj_item_crowbar));
        Dm.append(ref to_preload, typeof(obj_item_wirecutters));
        Dm.append(ref to_preload, typeof(obj_item_multitool));
        Dm.append(ref to_preload, typeof(obj_item_inducer));
        return to_preload;
    }
}

// tg: code/game/objects/items/storage/belt.dm:181
public partial class obj_item_storage_belt_utility_syndicate : obj_item_storage_belt_utility
{
    // tg: code/game/objects/items/storage/belt.dm:183
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver_nuke>(this); // tg: code/game/objects/items/storage/belt.dm:184
        Dm.@new<obj_item_wrench_combat>(this); // tg: code/game/objects/items/storage/belt.dm:185
        Dm.@new<obj_item_weldingtool_largetank>(this); // tg: code/game/objects/items/storage/belt.dm:186
        Dm.@new<obj_item_crowbar>(this); // tg: code/game/objects/items/storage/belt.dm:187
        Dm.@new<obj_item_wirecutters>(this); // tg: code/game/objects/items/storage/belt.dm:188
        Dm.@new<obj_item_multitool>(this); // tg: code/game/objects/items/storage/belt.dm:189
        Dm.@new<obj_item_inducer_syndicate>(this); // tg: code/game/objects/items/storage/belt.dm:190
    }
}

// M7x slice E: the full security belt (a pepper spray, handcuffs, a flashbang, a flash and a stun baton).
// tg: code/game/objects/items/storage/belt.dm:202
public partial class obj_item_storage_belt_medical_paramedic : obj_item_storage_belt_medical
{
    // tg: code/game/objects/items/storage/belt.dm:209
    public override async DmTask PopulateContents()
    {
        await SSwardrobe.provide_type(typeof(obj_item_sensor_device), this);
        await SSwardrobe.provide_type(typeof(obj_item_stack_medical_wrap_gauze_twelve), this);
        await SSwardrobe.provide_type(typeof(obj_item_tourniquet), this);
        await SSwardrobe.provide_type(typeof(obj_item_bonesetter), this);
        await SSwardrobe.provide_type(typeof(obj_item_reagent_containers_syringe), this);
        await SSwardrobe.provide_type(typeof(obj_item_reagent_containers_cup_bottle_ammoniated_mercury), this);
        await SSwardrobe.provide_type(typeof(obj_item_reagent_containers_cup_bottle_formaldehyde), this);
        update_appearance();
    }

    // tg: code/game/objects/items/storage/belt.dm:218
    public override object get_types_to_preload()
    {
        DmList to_preload = Dm.list(); //Yes this is a pain. Yes this is the point
        Dm.append(ref to_preload, typeof(obj_item_sensor_device));
        Dm.append(ref to_preload, typeof(obj_item_stack_medical_wrap_gauze_twelve));
        Dm.append(ref to_preload, typeof(obj_item_tourniquet));
        Dm.append(ref to_preload, typeof(obj_item_bonesetter));
        Dm.append(ref to_preload, typeof(obj_item_reagent_containers_syringe));
        Dm.append(ref to_preload, typeof(obj_item_reagent_containers_cup_bottle_ammoniated_mercury));
        Dm.append(ref to_preload, typeof(obj_item_reagent_containers_cup_bottle_formaldehyde));
        return to_preload;
    }
}

// tg: code/game/objects/items/storage/belt.dm:229
public partial class obj_item_storage_belt_medical_ert : obj_item_storage_belt_medical
{
    // tg: code/game/objects/items/storage/belt.dm:235
    public override async DmTask PopulateContents()
    {
        await SSwardrobe.provide_type(typeof(obj_item_sensor_device), this);
        await SSwardrobe.provide_type(typeof(obj_item_pinpointer_crew), this);
        await SSwardrobe.provide_type(typeof(obj_item_scalpel_advanced), this);
        await SSwardrobe.provide_type(typeof(obj_item_retractor_advanced), this);
        await SSwardrobe.provide_type(typeof(obj_item_stack_medical_bone_gel), this);
        await SSwardrobe.provide_type(typeof(obj_item_cautery_advanced), this);
        await SSwardrobe.provide_type(typeof(obj_item_surgical_drapes), this);
        update_appearance();
    }

    // tg: code/game/objects/items/storage/belt.dm:245
    public override object get_types_to_preload()
    {
        DmList to_preload = Dm.list();
        Dm.append(ref to_preload, typeof(obj_item_sensor_device));
        Dm.append(ref to_preload, typeof(obj_item_pinpointer_crew));
        Dm.append(ref to_preload, typeof(obj_item_scalpel_advanced));
        Dm.append(ref to_preload, typeof(obj_item_retractor_advanced));
        Dm.append(ref to_preload, typeof(obj_item_stack_medical_bone_gel));
        Dm.append(ref to_preload, typeof(obj_item_cautery_advanced));
        Dm.append(ref to_preload, typeof(obj_item_surgical_drapes));
        return to_preload;
    }
}

// tg: code/game/objects/items/storage/belt.dm:266
public partial class obj_item_storage_belt_security_full : obj_item_storage_belt_security
{
    // tg: code/game/objects/items/storage/belt.dm:267
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_spray_pepper>(this); // tg: code/game/objects/items/storage/belt.dm:268
        Dm.@new<obj_item_restraints_handcuffs>(this); // tg: code/game/objects/items/storage/belt.dm:269
        Dm.@new<obj_item_grenade_flashbang>(this); // tg: code/game/objects/items/storage/belt.dm:270
        Dm.@new<obj_item_assembly_flash_handheld>(this); // tg: code/game/objects/items/storage/belt.dm:271
        Dm.@new<obj_item_melee_baton_security_loaded>(this); // tg: code/game/objects/items/storage/belt.dm:272
        update_appearance();
    }
}

// tg: code/game/objects/items/storage/belt.dm:595  (syndicate N2a, D316: the china lake's spare shells)
public partial class obj_item_storage_belt_bandolier_china_lake_extra : obj_item_storage_belt_bandolier
{
    // tg: code/game/objects/items/storage/belt.dm:595
    public override async DmTask PopulateContents()
    {
        generate_items_inside(Dm.list(Dm.kv(typeof(obj_item_ammo_casing_a40mm), 12)), this);
    }
}

// tg: code/game/objects/items/storage/belt.dm:438  (syndicate N2a, D316: the grenadier kit's full belt)
public partial class obj_item_storage_belt_grenade_full : obj_item_storage_belt_grenade
{
    // tg: code/game/objects/items/storage/belt.dm:448
    public override async DmTask PopulateContents()
    {
        generate_items_inside(Dm.list(
            Dm.kv(typeof(obj_item_grenade_chem_grenade_incendiary), 2),
            Dm.kv(typeof(obj_item_grenade_empgrenade), 2),
            Dm.kv(typeof(obj_item_grenade_frag), 10),
            Dm.kv(typeof(obj_item_grenade_flashbang), 2),
            Dm.kv(typeof(obj_item_grenade_gluon), 4),
            Dm.kv(typeof(obj_item_grenade_smokebomb), 4),
            Dm.kv(typeof(obj_item_grenade_syndieminibomb), 2),
            Dm.kv(typeof(obj_item_multitool), 1),
            Dm.kv(typeof(obj_item_screwdriver), 1)), this);
    }
}


// Gaps SR1 (D333): the belts left (the mining vendor's, the soulstone, abductor, assault, janitor and fannypack contents, the champion's
// belt, the snack rigs, the wand belts and the wand bandolier, the sheaths and their counterattack).

// tg: code/game/objects/items/storage/belt.dm:293
public partial class obj_item_storage_belt_mining_vendor : obj_item_storage_belt_mining
{
    // tg: code/game/objects/items/storage/belt.dm:293
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_survivalcapsule>(this);
    }
}

// tg: code/game/objects/items/storage/belt.dm:328
public partial class obj_item_storage_belt_soulstone_full : obj_item_storage_belt_soulstone
{
    // tg: code/game/objects/items/storage/belt.dm:328
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++)
        {
            Dm.@new<obj_item_soulstone_mystic>(this);
        }
    }
}

// tg: code/game/objects/items/storage/belt.dm:332
public partial class obj_item_storage_belt_soulstone_full_chappy : obj_item_storage_belt_soulstone_full
{
    // tg: code/game/objects/items/storage/belt.dm:332
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++)
        {
            Dm.@new<obj_item_soulstone_anybody_chaplain>(this);
        }
    }
}

// tg: code/game/objects/items/storage/belt.dm:408
public partial class obj_item_storage_belt_military_abductor_full : obj_item_storage_belt_military_abductor
{
    // tg: code/game/objects/items/storage/belt.dm:408
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver_abductor>(this);
        Dm.@new<obj_item_wrench_abductor>(this);
        Dm.@new<obj_item_weldingtool_abductor>(this);
        Dm.@new<obj_item_crowbar_abductor>(this);
        Dm.@new<obj_item_wirecutters_abductor>(this);
        Dm.@new<obj_item_multitool_abductor>(this);
        Dm.@new<obj_item_stack_cable_coil>(this);
    }
}

// tg: code/game/objects/items/storage/belt.dm:432
public partial class obj_item_storage_belt_military_assault_full : obj_item_storage_belt_military_assault
{
    // tg: code/game/objects/items/storage/belt.dm:432
    public override async DmTask PopulateContents()
    {
        generate_items_inside(Dm.list(
            Dm.kv(typeof(obj_item_ammo_box_magazine_wt550m9), 4),
            Dm.kv(typeof(obj_item_ammo_box_magazine_wt550m9_wtap), 2)), this);
    }
}

// tg: code/game/objects/items/storage/belt.dm:580
public partial class obj_item_storage_belt_janitor_full : obj_item_storage_belt_janitor
{
    // tg: code/game/objects/items/storage/belt.dm:580
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_lightreplacer>(this);
        Dm.@new<obj_item_reagent_containers_spray_cleaner>(this);
        Dm.@new<obj_item_soap_nanotrasen>(this);
        Dm.@new<obj_item_holosign_creator>(this);
        Dm.@new<obj_item_melee_flyswatter>(this);
    }
}

// tg: code/game/objects/items/storage/belt.dm:675
public partial class obj_item_storage_belt_fannypack_black_rogue : obj_item_storage_belt_fannypack_black
{
    // tg: code/game/objects/items/storage/belt.dm:675
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_food_drug_saturnx>(this);
        Dm.@new<obj_item_reagent_containers_cup_blastoff_ampoule>(this);
        Dm.@new<obj_item_reagent_containers_hypospray_medipen_methamphetamine>(this);
    }
}

// tg: code/game/objects/items/storage/belt.dm:667
public partial class obj_item_storage_belt_fannypack_yellow_bee_terrorist : obj_item_storage_belt_fannypack_yellow
{
    // tg: code/game/objects/items/storage/belt.dm:667
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_grenade_c4>(this);
        Dm.@new<obj_item_reagent_containers_applicator_pill_cyanide>(this);
        Dm.@new<obj_item_grenade_chem_grenade_facid>(this);
    }
}

// tg: code/game/objects/items/storage/belt.dm:336
public partial class obj_item_storage_belt_champion : obj_item_storage_belt
{
    // tg: code/game/objects/items/storage/belt.dm:345
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Dm.Deferred("code/game/objects/items/storage/belt.dm:348", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -2) (fishing, M10+)");
        return dot;
    }
}

// tg: code/game/objects/items/storage/belt.dm:359
public partial class obj_item_storage_belt_military_snack : obj_item_storage_belt_military
{
    // tg: code/game/objects/items/storage/belt.dm:363
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object sponsor = pick("Donk Co.", "Waffle Corp.", "Roffle Co.", "Gorlex Marauders", "Tiger Cooperative");
        desc = I18n.T("A set of snack-tical webbing worn by athletes of the [] VR sports division.", sponsor);
        return dot;
    }
}

// tg: code/game/objects/items/storage/belt.dm:368
public partial class obj_item_storage_belt_military_snack_full : obj_item_storage_belt_military_snack
{
    // tg: code/game/objects/items/storage/belt.dm:368
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        float amount = 5;
        object rig_snacks;
        while (contents.len <= amount)
        {
            rig_snacks = pick(Dm.list(
                typeof(obj_item_food_candy),
                typeof(obj_item_food_cheesiehonkers),
                typeof(obj_item_food_cheesynachos),
                typeof(obj_item_food_chips),
                typeof(obj_item_food_cubannachos),
                typeof(obj_item_food_donkpocket),
                typeof(obj_item_food_nachos),
                typeof(obj_item_food_nugget),
                typeof(obj_item_food_rofflewaffles),
                typeof(obj_item_food_sosjerky),
                typeof(obj_item_food_spacetwinkie),
                typeof(obj_item_food_spaghetti_pastatomato),
                typeof(obj_item_food_syndicake),
                typeof(obj_item_reagent_containers_cup_glass_drinkingglass_filled_nuka_cola),
                typeof(obj_item_reagent_containers_cup_glass_dry_ramen),
                typeof(obj_item_reagent_containers_cup_soda_cans_cola),
                typeof(obj_item_reagent_containers_cup_soda_cans_dr_gibb),
                typeof(obj_item_reagent_containers_cup_soda_cans_lemon_lime),
                typeof(obj_item_reagent_containers_cup_soda_cans_pwr_game),
                typeof(obj_item_reagent_containers_cup_soda_cans_space_mountain_wind),
                typeof(obj_item_reagent_containers_cup_soda_cans_space_up),
                typeof(obj_item_reagent_containers_cup_soda_cans_starkist)));
            Dm.@new((Type)rig_snacks, this);
        }
        return dot;
    }
}

// tg: code/game/objects/items/storage/belt.dm:461
public partial class obj_item_storage_belt_wands_full : obj_item_storage_belt_wands
{
    /// Put some wands in that bad boy
    // tg: code/game/objects/items/storage/belt.dm:471
    public virtual async DmTask create_wands()
    {
        Dm.@new<obj_item_gun_magic_wand_death>(this);
        Dm.@new<obj_item_gun_magic_wand_resurrection>(this);
        Dm.@new<obj_item_gun_magic_wand_polymorph>(this);
        Dm.@new<obj_item_gun_magic_wand_teleport>(this);
        Dm.@new<obj_item_gun_magic_wand_door>(this);
        Dm.@new<obj_item_gun_magic_wand_fireball>(this);
        Dm.@new<obj_item_gun_magic_wand_shrink>(this);
    }

    // tg: code/game/objects/items/storage/belt.dm:480
    public override async DmTask PopulateContents()
    {
        await create_wands();
        foreach (obj_item_gun_magic_wand W in Dm.each<obj_item_gun_magic_wand>(contents)) //All wands in this pack come in the best possible condition
        {
            W.max_charges = Dm.initial(W, (obj_item_gun_magic_wand x) => x.max_charges);
            W.charges = W.max_charges;
        }
    }
}

/// Filled with budget wands instead of cool ones
// tg: code/game/objects/items/storage/belt.dm:487
public partial class obj_item_storage_belt_wands_full_discount : obj_item_storage_belt_wands_full
{
    // tg: code/game/objects/items/storage/belt.dm:510
    public override async DmTask create_wands()
    {
        // tg's `var/static/list/possible_options` (:489), read only through its Copy(): built here (rules 10.6, a constant list).
        DmList available_options = Dm.list(
            typeof(obj_item_gun_magic_wand_animate),
            typeof(obj_item_gun_magic_wand_babel),
            typeof(obj_item_gun_magic_wand_bald),
            typeof(obj_item_gun_magic_wand_door),
            typeof(obj_item_gun_magic_wand_fireball),
            typeof(obj_item_gun_magic_wand_freeze),
            typeof(obj_item_gun_magic_wand_hallucination),
            typeof(obj_item_gun_magic_wand_levitate),
            typeof(obj_item_gun_magic_wand_pax),
            typeof(obj_item_gun_magic_wand_pizza),
            typeof(obj_item_gun_magic_wand_plague),
            typeof(obj_item_gun_magic_wand_prank),
            typeof(obj_item_gun_magic_wand_rebel),
            typeof(obj_item_gun_magic_wand_repulse),
            typeof(obj_item_gun_magic_wand_swap),
            typeof(obj_item_gun_magic_wand_teleport),
            typeof(obj_item_gun_magic_wand_tentacles),
            typeof(obj_item_gun_magic_wand_zap));
        for (float i = 1; i <= 6; i++)
        {
            if (!Dm.truthy(Dm.length(available_options)))
                break;
            object wand_path = pick_n_take(available_options);
            Dm.@new((Type)wand_path, this);
        }
    }
}

/// Not a subtype of bandolier because it acts pretty differently
// tg: code/game/objects/items/storage/belt.dm:519
public partial class obj_item_storage_belt_wand_bandolier : obj_item_storage_belt
{
    // tg: code/game/objects/items/storage/belt.dm:528
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_SUITSTORE)))
            return;
        ADD_TRAIT(user, TRAIT_GUNFLIP, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this))); // ADD_CLOTHING_TRAIT, expanded inline (rules 11.1 (d))
        RegisterSignal(user, COMSIG_MOB_FIRED_GUN, nameof(on_gun_fired));
    }

    // tg: code/game/objects/items/storage/belt.dm:535
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        REMOVE_TRAIT(user, TRAIT_GUNFLIP, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this))); // REMOVE_CLOTHING_TRAIT, expanded inline
        UnregisterSignal(user, COMSIG_MOB_FIRED_GUN);
        return dot;
    }

    /// After we fire a gun, check if it's a wand. If it is and it's empty, do a swap
    // tg: code/game/objects/items/storage/belt.dm:541
    [SignalHandler]
    public virtual void on_gun_fired(mob_living wizard = null, obj_item_gun old_wand = null, params object[] __extra)
    {
        Waiting.Detach(equip_new_wand(wizard, old_wand as obj_item_gun_magic_wand));
    }

    // tg: code/game/objects/items/storage/belt.dm:545
    public virtual async DmTask equip_new_wand(mob_living wizard = null, obj_item_gun_magic_wand old_wand = null)
    {
        if (old_wand is null)
            return;
        if (!Dm.truthy(atom_storage.real_location.contents.len)) // no other wands
            return;
        if (old_wand.charges > 1) // It hasn't subtracted yet
            return;

        obj_item fresh_wand = null;
        foreach (obj_item_gun_magic_wand @checked in Dm.each<obj_item_gun_magic_wand>(atom_storage.real_location.contents))
        {
            if (Dm.truthy(@checked.charges))
            {
                fresh_wand = @checked;
                break;
            }
        }
        if (!Dm.truthy(fresh_wand) || Dm.truthy(await fresh_wand.on_found()))
            return; // Nothing to swap with

        await wizard.temporarilyRemoveItemFromInventory(old_wand);
        if (!Dm.truthy(await wizard.put_in_hands(fresh_wand)))
            return;
        to_chat(wizard, span_notice(I18n.T("You quickly draw [].", fresh_wand)));
        if (Dm.truthy(atom_storage.attempt_insert(old_wand, wizard)))
            return;
        await old_wand.forceMove(wizard.drop_location() as atom);
        to_chat(wizard, span_warning(I18n.T("...and drop [] on the ground.", old_wand)));
    }
}

// tg: code/game/objects/items/storage/belt.dm:679
public partial class obj_item_storage_belt_sheath : obj_item_storage_belt
{
    // tg: code/game/objects/items/storage/belt.dm:690
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob), action_slots));
        RegisterSignal(this, COMSIG_ATOM_STORED_ITEM, nameof(post_resheath));
        return dot;
    }

    // tg: code/game/objects/items/storage/belt.dm:695
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        UnregisterSignal(this, COMSIG_ATOM_STORED_ITEM);
        return dot;
    }

    // tg: code/game/objects/items/storage/belt.dm:699
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.length(contents)))
            Dm.append(ref dot, span_notice(I18n.T("Alt-click it to quickly draw the blade.")));
        return dot;
    }

    // tg: code/game/objects/items/storage/belt.dm:704
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(Dm.length(contents)))
        {
            balloon_alert(user, I18n.T("it's empty!"));
            return CLICK_ACTION_BLOCKING;
        }
        obj_item stored_item = contents[1] as obj_item;
        user.visible_message(span_notice(I18n.T("[] takes [] out of [].", user, stored_item, this)), span_notice(I18n.T("You take [] out of [].", stored_item, this)));
        await user.put_in_hands(stored_item);
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/storage/belt.dm:714
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.str(Dm.initial(this, (obj_item_storage_belt_sheath x) => x.icon_state));
        inhand_icon_state = Dm.initial(this, (obj_item_storage_belt_sheath x) => x.inhand_icon_state);
        worn_icon_state = Dm.initial(this, (obj_item_storage_belt_sheath x) => x.worn_icon_state);
        if (Dm.truthy(contents.len))
        {
            icon_state += "-full";
            inhand_icon_state = Dm.text("[]-full", inhand_icon_state);
            worn_icon_state = Dm.text("[]-full", worn_icon_state);
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/storage/belt.dm:724
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(stored_blade))
        {
            Dm.@new((Type)stored_blade, this);
            update_appearance();
        }
    }

    // tg: code/game/objects/items/storage/belt.dm:729
    [SignalHandler]
    public virtual void post_resheath(params object[] __extra)
    {
        resheath_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START, inline (rules 11.1 (a))
    }
}

// tg: code/game/objects/items/storage/belt.dm:734
public partial class datum_action_innate_blade_counter : datum_action_innate
{
    // tg: code/game/objects/items/storage/belt.dm:747
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= TRUE;
        if (!isliving(owner))
            return FALSE;
        obj_item_storage_belt_sheath owners_sheath = target as obj_item_storage_belt_sheath;
        if (!(owners_sheath.full_ability_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(I18n.T("You failed a counterattack too recently!")));
            return FALSE;
        }
        if (!Dm.truthy(Dm.length(owners_sheath.contents)))
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(I18n.T("Your sheath is empty!")));
            return FALSE;
        }
        if (!(owners_sheath.resheath_cooldown <= world.time))
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(I18n.T("You only just resheathed your blade!")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/storage/belt.dm:765
    public virtual object final_checks(atom cast_on = null)
    {
        if (!isliving(cast_on))
            return FALSE;
        if (owner == cast_on)
        {
            to_chat(owner, span_warning(I18n.T("You can't counterattack yourself!")));
            return FALSE;
        }
        mob_living target = cast_on as mob_living;
        if (!Dm.truthy(target.mind))
        {
            to_chat(owner, span_warning(I18n.T("They are too unpredictable to counterattack!")));
            return FALSE;
        }
        // tg reads the clicked mob as a sheath here (`var/obj/item/storage/belt/sheath/oursheath = target`, :776, the local that
        // shadows the action's target): its contents are the mob's, which a living mob always has.
        atom oursheath = target;
        if (!Dm.truthy(Dm.length(oursheath.contents)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/storage/belt.dm:780
    public override async DmTask<object> do_ability(mob_living swordsman = null, atom cast_on = null)
    {
        if (!Dm.truthy(final_checks(cast_on)))
            return TRUE;
        obj_item_storage_belt_sheath used_sheath = target as obj_item_storage_belt_sheath;
        RegisterSignal(swordsman, COMSIG_LIVING_CHECK_BLOCK, nameof(counter_attack));
        await swordsman.Immobilize(1 * SECONDS);
        eyed_fool = WEAKREF(cast_on) as datum_weakref;
        swordsman.visible_message(span_danger(I18n.T("[] widens [] stance, [] hand hovering over \\the []!", swordsman, swordsman.p_their(), swordsman.p_their(), used_sheath)), span_notice(I18n.T("You prepare to counterattack []!", cast_on)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(relax), swordsman, used_sheath), 1 * SECONDS, file: "code/game/objects/items/storage/belt.dm", line: 790);
        used_sheath.full_ability_cooldown = world.time + 60 * SECONDS; // COOLDOWN_START, inline
        unset_ranged_ability(swordsman);
        return TRUE;
    }

    // tg: code/game/objects/items/storage/belt.dm:793  #define COUNTERMULTIPLIER 3 (the file's define, #undef at :910: the gun-powered kind uses it too)
    protected const float COUNTERMULTIPLIER = 3;

    // tg: code/game/objects/items/storage/belt.dm:795
    [SignalHandler]
    public virtual object counter_attack(mob_living forward_thinker = null, atom attackingthing = null, object damage = null, object attack_text = null, object attack_type = null, params object[] __extra)
    {
        obj_item_storage_belt_sheath used_sheath = target as obj_item_storage_belt_sheath;
        if (!Dm.truthy(used_sheath) || !Dm.truthy(Dm.length(used_sheath.contents)) || (!Dm.eq(attack_type, MELEE_ATTACK) && !Dm.eq(attack_type, UNARMED_ATTACK)))
            return FAILED_BLOCK;

        obj_item justicetool = used_sheath.contents[1] as obj_item;
        mob_living fool = (isliving(attackingthing) ? attackingthing : attackingthing.loc) as mob_living;
        if (used_sheath.loc != forward_thinker || fool != eyed_fool?.resolve() || !Dm.truthy(Waiting.NoSleep(forward_thinker.put_in_active_hand(justicetool))))
            return FAILED_BLOCK;
        Waiting.NoSleep(do_strike(fool, forward_thinker, justicetool));
        playsound(forward_thinker, Dm.resource("sound/items/unsheath.ogg"), 50, TRUE);
        used_sheath.full_ability_cooldown = 0; // COOLDOWN_RESET, inline
        return SUCCESSFUL_BLOCK;
    }

    // tg: code/game/objects/items/storage/belt.dm:810
    public virtual async DmTask do_strike(mob_living fool = null, mob_living forward_thinker = null, obj_item justicetool = null)
    {
        object offending_hand = fool.get_active_hand();
        forward_thinker.visible_message(span_danger(I18n.T("[] swiftly draws \\the [] and strikes [] during [] attack!", forward_thinker, justicetool, fool, fool.p_their())), span_notice(I18n.T("You swiftly draw \\the [] and counter-attack []!", justicetool, fool)));
        await fool.apply_damage(
            damage: justicetool.force * COUNTERMULTIPLIER,
            damagetype: justicetool.damtype,
            def_zone: offending_hand,
            blocked: Dm.num(fool.run_armor_check(offending_hand, MELEE, armour_penetration: justicetool.armour_penetration, silent: TRUE)),
            wound_bonus: justicetool.wound_bonus * COUNTERMULTIPLIER,
            exposed_wound_bonus: justicetool.exposed_wound_bonus * COUNTERMULTIPLIER,
            sharpness: justicetool.sharpness,
            attack_direction: get_dir(forward_thinker, fool),
            attacking_item: justicetool);
    }

    // tg: code/game/objects/items/storage/belt.dm:826
    public virtual async DmTask relax(mob_living holder = null, obj_item_storage_belt_sheath active_sheath = null)
    {
        UnregisterSignal(holder, COMSIG_LIVING_CHECK_BLOCK);
    }
}

// tg: code/game/objects/items/storage/belt.dm:830
public partial class datum_action_innate_blade_counter_gunpowered : datum_action_innate_blade_counter
{
    // tg: code/game/objects/items/storage/belt.dm:836
    public override async DmTask<object> do_ability(mob_living swordsman = null, atom cast_on = null)
    {
        object dot = await base.do_ability(swordsman, cast_on);
        succeeded_attempt = FALSE;
        return dot;
    }

    // tg: code/game/objects/items/storage/belt.dm:841
    public override async DmTask do_strike(mob_living fool = null, mob_living forward_thinker = null, obj_item justicetool = null)
    {
        if (Dm.eq(forward_thinker.get_slot_by_item(target as obj_item), ITEM_SLOT_SUITSTORE))
        {
            await base.do_strike(fool, forward_thinker, justicetool);
            return;
        }
        succeeded_attempt = TRUE;
        obj_item_bodypart offending_hand = fool.get_active_hand() as obj_item_bodypart;
        obj_item_bodypart risked_hand = forward_thinker.get_active_hand() as obj_item_bodypart;
        if (iscarbon(fool) && Dm.truthy(await offending_hand.dismember(BRUTE, FALSE, WOUND_SLASH)))
        {
            forward_thinker.visible_message(span_danger(I18n.T("[] swiftly draws \\the [] and strikes [] during [] attack, sending [] arm flying!", forward_thinker, justicetool, fool, fool.p_their(), fool.p_their())),
                span_notice(I18n.T("You swiftly draw \\the [] and cut off []'s arm!", justicetool, fool)));
        }
        else
        {
            await fool.apply_damage(
                damage: justicetool.force * COUNTERMULTIPLIER,
                damagetype: justicetool.damtype,
                def_zone: offending_hand,
                blocked: Dm.num(fool.run_armor_check(offending_hand, MELEE, armour_penetration: justicetool.armour_penetration, silent: TRUE)),
                wound_bonus: justicetool.wound_bonus * COUNTERMULTIPLIER,
                exposed_wound_bonus: justicetool.exposed_wound_bonus * COUNTERMULTIPLIER,
                sharpness: justicetool.sharpness,
                attack_direction: get_dir(forward_thinker, fool),
                attacking_item: justicetool);
            forward_thinker.visible_message(span_danger(I18n.T("[] swiftly draws \\the [] and strikes [] during [] attack!", forward_thinker, justicetool, fool, fool.p_their())),
                span_notice(I18n.T("You swiftly draw \\the [] and strike them mid-attack!", justicetool)));
        }
        if (!Dm.truthy(IS_ROBOTIC_LIMB(risked_hand)))
        {
            forward_thinker.visible_message(span_danger(I18n.T("[]'s arm is unable to withstand the force of the attack!", forward_thinker)),
                span_danger(I18n.T("You feel a sharp pain as your arm is mutilated by the force of the attack!")));
            await forward_thinker.apply_damage(
                damage: 50,
                damagetype: BRUTE,
                def_zone: risked_hand,
                wound_bonus: 50,
                wound_clothing: FALSE);
        }
    }

    // tg: code/game/objects/items/storage/belt.dm:876
    public override async DmTask relax(mob_living holder = null, obj_item_storage_belt_sheath active_sheath = null)
    {
        await base.relax(holder, active_sheath);
        if (Dm.truthy(succeeded_attempt) || Dm.eq(holder.get_slot_by_item(target as obj_item), ITEM_SLOT_SUITSTORE))
            return;

        if (Dm.truthy(Dm.length(active_sheath.contents)))
        {
            obj_item denied_weapon = active_sheath.contents[1] as obj_item;
            await denied_weapon.forceMove(get_turf(holder));
            await denied_weapon.throw_at(pick(RANGE_TURFS(3, denied_weapon)) as atom, 3, 3);
        }

        // We can assume that the holder is a carbon, and thus has an actual arm, because they have a belt slot.
        obj_item_bodypart worthless_hand = holder.get_active_hand() as obj_item_bodypart;
        if (!Dm.truthy(worthless_hand))
        {
            worthless_hand = holder.get_inactive_hand() as obj_item_bodypart;
            if (!Dm.truthy(worthless_hand))
            {
                holder.visible_message(span_danger(I18n.T("[]'s sheath misfires, sending their blade flying!", holder)),
                    span_danger(I18n.T("Your sheath misfires, sending your blade flying!")));
                return;
            }
        }

        if (Dm.truthy(IS_ROBOTIC_LIMB(worthless_hand)) || !Dm.truthy(await worthless_hand.dismember(BRUTE, FALSE, WOUND_BLUNT)))
        {
            holder.visible_message(span_danger(I18n.T("[]'s arm is mutilated as they misfire [] sheathed blade!", holder, holder.p_their())),
                span_danger(I18n.T("Your arm is mutilated as you fail to safely fire your blade!")));
            await holder.apply_damage(
                damage: 50,
                damagetype: BRUTE,
                def_zone: worthless_hand,
                wound_bonus: 50,
                wound_clothing: FALSE);
            return;
        }

        holder.visible_message(span_danger(I18n.T("[]'s arm is violently torn off as they misfire [] sheathed blade!", holder, holder.p_their())),
            span_danger(I18n.T("Your arm is torn off as you fail to safely fire your blade!")));
    }
}

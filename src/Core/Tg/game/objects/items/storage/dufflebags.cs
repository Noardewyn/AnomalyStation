// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/dufflebags.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base duffel bag (M10x): zipped at spawn, unzipped by clicking it (attack_hand) or its action button, slower while open.
// The vars, the type's defaults and the other duffel bag types' var blocks are generated; syndicate N2b (D316): the operatives'
// ammunition, explosives, medical and firestarter bags; N3: the clown operatives' clown costume bag; gaps SR1 (D333): the other
// bags' contents. Left: the living duffel bag (its curse_of_hunger component, not-translated.md section 208).

// tg: code/game/objects/items/storage/dufflebags.dm:1
[DmPath("/obj/item/storage/backpack/duffelbag")]
public partial class obj_item_storage_backpack_duffelbag : obj_item_storage_backpack
{
    // tg: code/game/objects/items/storage/dufflebags.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        slowdown += zip_slowdown;
        Waiting.NoSleep(set_zipper(TRUE));
        RegisterSignal(this, COMSIG_SPEED_POTION_APPLIED, nameof(on_speed_potioned));
        return dot;
    }

    // tg: code/game/objects/items/storage/dufflebags.dm:28
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, Dm.text("[]", Dm.truthy(zipped_up) ? I18n.T("It's zipped up, preventing you from accessing its contents.") : I18n.T("It's unzipped, and harder to move in.")));
        return dot;
    }

    // tg: code/game/objects/items/storage/dufflebags.dm:32
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (loc != user) // God fuck TK
            return await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(zipped_up))
            return await attack_hand(user, modifiers as DmList);
        else
            return await attack_hand_secondary(user, modifiers as DmList);
    }

    // tg: code/game/objects/items/storage/dufflebags.dm:40
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        await attack_self(user, modifiers);
        return await base.attack_self_secondary(user, modifiers);
    }

    // If we're zipped, click to unzip
    // tg: code/game/objects/items/storage/dufflebags.dm:45
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        if (loc != user)
        {
            // Hacky, but please don't be cringe yeah?
            atom_storage.silent = TRUE;
            dot = await base.attack_hand(user, modifiers, __extra);
            atom_storage.silent = Dm.initial(atom_storage, (datum_storage s) => s.silent);
            return dot;
        }
        if (!Dm.truthy(zipped_up))
            return await base.attack_hand(user, modifiers, __extra);

        balloon_alert(user, I18n.T("unzipping..."));
        playsound(this, unzip_sfx, 100, FALSE);
        datum_callback can_unzip = Dm.@new<datum_callback>(this, nameof(zipper_matches), TRUE);
        if (!Dm.truthy(await do_after(user, unzip_duration, this, extra_checks: can_unzip)))
        {
            user.balloon_alert(user, I18n.T("unzip failed!"));
            return null;
        }
        balloon_alert(user, I18n.T("unzipped"));
        await set_zipper(FALSE);
        return TRUE;
    }

    // Vis versa
    // tg: code/game/objects/items/storage/dufflebags.dm:66
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (loc != user)
            return await base.attack_hand_secondary(user, modifiers);
        if (Dm.truthy(zipped_up))
            return SECONDARY_ATTACK_CALL_NORMAL;

        balloon_alert(user, I18n.T("zipping..."));
        playsound(this, zip_up_sfx, 100, FALSE);
        datum_callback can_zip = Dm.@new<datum_callback>(this, nameof(zipper_matches), FALSE);
        if (!Dm.truthy(await do_after(user, zip_up_duration, this, extra_checks: can_zip)))
        {
            user.balloon_alert(user, I18n.T("zip failed!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        balloon_alert(user, I18n.T("zipped"));
        await set_zipper(TRUE);
        return SECONDARY_ATTACK_CONTINUE_CHAIN;
    }

    /// Checks to see if the zipper matches the passed in state
    /// Returns true if so, false otherwise
    // tg: code/game/objects/items/storage/dufflebags.dm:84
    public virtual object zipper_matches(object matching_value = null)
    {
        return Dm.num(Dm.eq(zipped_up, matching_value));
    }

    // tg: code/game/objects/items/storage/dufflebags.dm:87
    public virtual async DmTask set_zipper(object new_zip = null)
    {
        zipped_up = Dm.num(new_zip);
        if (Dm.truthy(_listen_lookup?[COMSIG_DUFFEL_ZIP_CHANGE]))
            _SendSignal(COMSIG_DUFFEL_ZIP_CHANGE, Dm.list(this, new_zip));
        if (Dm.truthy(zipped_up))
        {
            slowdown -= zip_slowdown;
            atom_storage.set_locked(STORAGE_SOFT_LOCKED);
            atom_storage.display_contents = FALSE;
        }
        else
        {
            slowdown += zip_slowdown;
            atom_storage.set_locked(STORAGE_NOT_LOCKED);
            atom_storage.display_contents = TRUE;
        }

        if (isliving(loc))
        {
            mob_living wearer = (mob_living)loc;
            await wearer.update_equipment_speed_mods();
        }
    }

    /// Signal handler for [COMSIG_SPEED_POTION_APPLIED]. Speed potion removes the unzipped slowdown
    // tg: code/game/objects/items/storage/dufflebags.dm:104
    [SignalHandler]
    public virtual void on_speed_potioned(datum source = null)
    {
        // Don't need to touch the actual slowdown here, since the speed potion does it for us
        zip_slowdown = 0;
    }
}

// M4a P3: the PopulateContents of Meta's duffel bags (sec/surgery, syndie/hitman) wait for M4a P2's add_item_action
// (items.dm:316): their storage's quick gather (allow_quick_gather) adds the gather-mode action when they are equipped. The drone's
// came with the classic drones (silicons SI7).

// tg: code/game/objects/items/storage/dufflebags.dm (the three duffel bag types above, declared here so that their generated
// parts stay beside a hand-written class; inert until their PopulateContents are translated)
public partial class obj_item_storage_backpack_duffelbag_sec_surgery : obj_item_storage_backpack_duffelbag_sec { }
public partial class obj_item_storage_backpack_duffelbag_syndie_hitman : obj_item_storage_backpack_duffelbag_syndie { }

// tg: code/game/objects/items/storage/dufflebags.dm:302
public partial class obj_item_storage_backpack_duffelbag_syndie_ammo_darkgygax : obj_item_storage_backpack_duffelbag_syndie_ammo
{
    // tg: code/game/objects/items/storage/dufflebags.dm:306
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 6; i++)
            Dm.@new<obj_item_mecha_ammo_scattershot>(this);
        Dm.@new<obj_item_storage_belt_utility_syndicate>(this);
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:315
public partial class obj_item_storage_backpack_duffelbag_syndie_ammo_mauler : obj_item_storage_backpack_duffelbag_syndie_ammo
{
    // tg: code/game/objects/items/storage/dufflebags.dm:319
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 3; i++)
            Dm.@new<obj_item_mecha_ammo_lmg>(this);
        for (int i = 1; i <= 3; i++)
            Dm.@new<obj_item_mecha_ammo_missiles_srm>(this);
        Dm.@new<obj_item_storage_belt_utility_syndicate>(this);
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:328
public partial class obj_item_storage_backpack_duffelbag_syndie_med_medicalbundle : obj_item_storage_backpack_duffelbag_syndie_med
{
    // tg: code/game/objects/items/storage/dufflebags.dm:331
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_mod_module_magboot>(this);
        Dm.@new<obj_item_storage_medkit_tactical_premium>(this);
        Dm.@new<obj_item_gun_ballistic_automatic_l6_saw_toy>(this);
        Dm.@new<obj_item_ammo_box_foambox_riot>(this);
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:351
public partial class obj_item_storage_backpack_duffelbag_syndie_c4 : obj_item_storage_backpack_duffelbag_syndie
{
    // tg: code/game/objects/items/storage/dufflebags.dm:351
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 10; i++)
            Dm.@new<obj_item_grenade_c4>(this);
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:355
public partial class obj_item_storage_backpack_duffelbag_syndie_x4 : obj_item_storage_backpack_duffelbag_syndie
{
    // tg: code/game/objects/items/storage/dufflebags.dm:355
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 10; i++)
            Dm.@new<obj_item_grenade_c4_x4>(this);
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:359
public partial class obj_item_storage_backpack_duffelbag_syndie_firestarter : obj_item_storage_backpack_duffelbag_syndie
{
    // tg: code/game/objects/items/storage/dufflebags.dm:362
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_syndicate_soviet>(this);
        Dm.@new<obj_item_mod_control_pre_equipped_elite_flamethrower>(this);
        Dm.@new<obj_item_gun_ballistic_automatic_pistol_aps>(this);
        Dm.@new<obj_item_ammo_box_magazine_m9mm_aps_fire>(this);
        Dm.@new<obj_item_ammo_box_magazine_m9mm_aps_fire>(this);
        Dm.@new<obj_item_reagent_containers_cup_glass_bottle_vodka_badminka>(this);
        Dm.@new<obj_item_reagent_containers_hypospray_medipen_stimulants>(this);
        Dm.@new<obj_item_grenade_syndieminibomb>(this);
    }
}

// For ClownOps.
// tg: code/game/objects/items/storage/dufflebags.dm:373
public partial class obj_item_storage_backpack_duffelbag_clown_syndie : obj_item_storage_backpack_duffelbag_clown
{
    // tg: code/game/objects/items/storage/dufflebags.dm:376
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_modular_computer_pda_crew_clown>(this);
        Dm.@new<obj_item_clothing_under_rank_civilian_clown>(this);
        Dm.@new<obj_item_clothing_shoes_clown_shoes>(this);
        Dm.@new<obj_item_clothing_mask_gas_clown_hat>(this);
        Dm.@new<obj_item_bikehorn>(this);
        Dm.@new<obj_item_implanter_sad_trombone>(this);
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:212  (silicons SI7, D326: the classic drones' tools)
public partial class obj_item_storage_backpack_duffelbag_drone : obj_item_storage_backpack_duffelbag
{
    // tg: code/game/objects/items/storage/dufflebags.dm:219
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver>(this);
        Dm.@new<obj_item_wrench>(this);
        Dm.@new<obj_item_weldingtool>(this);
        Dm.@new<obj_item_crowbar>(this);
        Dm.@new<obj_item_stack_cable_coil>(this);
        Dm.@new<obj_item_wirecutters>(this);
        Dm.@new<obj_item_multitool>(this);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:193
public partial class obj_item_storage_backpack_duffelbag_sec_surgery : obj_item_storage_backpack_duffelbag_sec
{
    // tg: code/game/objects/items/storage/dufflebags.dm:193
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_scalpel>(this);
        Dm.@new<obj_item_hemostat>(this);
        Dm.@new<obj_item_retractor>(this);
        Dm.@new<obj_item_circular_saw>(this);
        Dm.@new<obj_item_bonesetter>(this);
        Dm.@new<obj_item_surgicaldrill>(this);
        Dm.@new<obj_item_cautery>(this);
        Dm.@new<obj_item_surgical_drapes>(this);
        Dm.@new<obj_item_clothing_mask_surgical>(this);
        Dm.@new<obj_item_blood_filter>(this);
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:234
public partial class obj_item_storage_backpack_duffelbag_clown_cream_pie : obj_item_storage_backpack_duffelbag_clown
{
    // tg: code/game/objects/items/storage/dufflebags.dm:234
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 10; i++)
        {
            Dm.@new<obj_item_food_pie_cream>(this);
        }
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:259
public partial class obj_item_storage_backpack_duffelbag_syndie_hitman : obj_item_storage_backpack_duffelbag_syndie
{
    // tg: code/game/objects/items/storage/dufflebags.dm:259
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_costume_buttondown_slacks_service>(this);
        Dm.@new<obj_item_clothing_neck_tie_red_hitman>(this);
        Dm.@new<obj_item_clothing_accessory_waistcoat>(this);
        Dm.@new<obj_item_clothing_suit_toggle_lawyer_black>(this);
        Dm.@new<obj_item_clothing_shoes_laceup>(this);
        Dm.@new<obj_item_clothing_gloves_color_black>(this);
        Dm.@new<obj_item_clothing_glasses_sunglasses>(this);
        Dm.@new<obj_item_clothing_head_fedora>(this);
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:340
public partial class obj_item_storage_backpack_duffelbag_syndie_med_bioterrorbundle : obj_item_storage_backpack_duffelbag_syndie_med
{
    // tg: code/game/objects/items/storage/dufflebags.dm:340
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_spray_chemsprayer_bioterror>(this);
        Dm.@new<obj_item_storage_box_syndie_kit_chemical>(this);
        Dm.@new<obj_item_gun_syringe_syndicate>(this);
        Dm.@new<obj_item_gun_ballistic_automatic_c20r_toy>(this);
        Dm.@new<obj_item_storage_box_syringes>(this);
        Dm.@new<obj_item_ammo_box_foambox_riot>(this);
        Dm.@new<obj_item_grenade_chem_grenade_bioterrorfoam>(this);
        if (prob(5))
        {
            Dm.@new<obj_item_food_pizza_pineapple>(this);
        }
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:281
public partial class obj_item_storage_backpack_duffelbag_syndie_surgery : obj_item_storage_backpack_duffelbag_syndie
{
    // tg: code/game/objects/items/storage/dufflebags.dm:281
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_scalpel_advanced>(this);
        Dm.@new<obj_item_retractor_advanced>(this);
        Dm.@new<obj_item_cautery_advanced>(this);
        Dm.@new<obj_item_surgical_drapes>(this);
        Dm.@new<obj_item_reagent_containers_medigel_sterilizine>(this);
        Dm.@new<obj_item_bonesetter>(this);
        Dm.@new<obj_item_blood_filter>(this);
        Dm.@new<obj_item_stack_medical_bone_gel>(this);
        Dm.@new<obj_item_stack_medical_wrap_sticky_tape_surgical>(this);
        Dm.@new<obj_item_emergency_bed>(this);
        Dm.@new<obj_item_clothing_suit_jacket_straight_jacket>(this);
        Dm.@new<obj_item_clothing_mask_muzzle>(this);
        Dm.@new<obj_item_brain_processor_organic_syndie>(this);
    }
}

// tg: code/game/objects/items/storage/dufflebags.dm:400
public partial class obj_item_storage_backpack_duffelbag_mining_conscript : obj_item_storage_backpack_duffelbag
{
    // tg: code/game/objects/items/storage/dufflebags.dm:400
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_glasses_meson>(this);
        Dm.@new<obj_item_t_scanner_adv_mining_scanner_lesser>(this);
        Dm.@new<obj_item_storage_bag_ore>(this);
        Dm.@new<obj_item_clothing_suit_hooded_explorer>(this);
        Dm.@new<obj_item_encryptionkey_headset_mining>(this);
        Dm.@new<obj_item_clothing_mask_gas_explorer>(this);
        Dm.@new<obj_item_card_id_advanced_mining>(this);
        Dm.@new<obj_item_gun_energy_recharge_kinetic_accelerator>(this);
        Dm.@new<obj_item_knife_combat_survival>(this);
        Dm.@new<obj_item_flashlight_seclite>(this);
        Dm.@new<obj_item_stack_candela_beacon_thirty>(this);
    }
}

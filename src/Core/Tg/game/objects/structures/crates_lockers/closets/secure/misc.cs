// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST3 (D333): the file whole, the emergency response team's closets (their contents; the closets are CentCom's and
// admins', none is mapped on the station). The var blocks are generated (D111).

// tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:2  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_ert_com : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:7
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:8
        Dm.@new<obj_item_storage_medkit_regular>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:9
        Dm.@new<obj_item_storage_box_handcuffs>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:10
        Dm.@new<obj_item_assembly_flash_handheld>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:11
        if (prob(50)) // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:12
        {
            Dm.@new<obj_item_ammo_box_magazine_m50>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:13
            Dm.@new<obj_item_ammo_box_magazine_m50>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:14
            Dm.@new<obj_item_gun_ballistic_automatic_pistol_deagle>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:15
        }
        else // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:16
        {
            Dm.@new<obj_item_ammo_box_speedloader_c357>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:17
            Dm.@new<obj_item_ammo_box_speedloader_c357>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:18
            Dm.@new<obj_item_gun_ballistic_revolver_mateba>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:19
        }
    }

    // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:21
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate(); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:22
        // Traitor steal objective // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:24
        Dm.@new<obj_item_aicard>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:25
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:28  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_ert_sec : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:33
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:34
        Dm.@new<obj_item_storage_box_flashbangs>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:35
        Dm.@new<obj_item_storage_box_teargas>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:36
        Dm.@new<obj_item_storage_box_flashes>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:37
        Dm.@new<obj_item_storage_box_handcuffs>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:38
        Dm.@new<obj_item_shield_riot_tele>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:39
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:42  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_ert_med : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:47
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:48
        Dm.@new<mob_living_basic_bot_medbot>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:49
        Dm.@new<obj_item_storage_medkit_o2>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:50
        Dm.@new<obj_item_storage_medkit_toxin>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:51
        Dm.@new<obj_item_storage_medkit_fire>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:52
        Dm.@new<obj_item_storage_medkit_brute>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:53
        Dm.@new<obj_item_storage_medkit_regular>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:54
        Dm.@new<obj_item_defibrillator_compact_combat_loaded_nanotrasen>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:55
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:58  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_ert_engi : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:63
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:64
        Dm.@new<obj_item_stack_sheet_plasteel>(this, 50); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:65
        Dm.@new<obj_item_stack_sheet_iron>(this, 50); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:66
        Dm.@new<obj_item_stack_sheet_glass>(this, 50); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:67
        Dm.@new<obj_item_stack_sheet_mineral_sandbags>(this, 30); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:68
        Dm.@new<obj_item_clothing_shoes_magboots>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:69
        Dm.@new<obj_item_storage_box_smart_metal_foam>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:70
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:71
        {
            Dm.@new<obj_item_rcd_ammo_large>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/misc.dm:72
        }
    }
}

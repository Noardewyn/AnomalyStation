// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/security_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'. Gaps CL1 (D333): the laser
// tag pins' box.

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:293
public partial class obj_item_storage_box_evidence : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:293
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:294
        {
            Dm.@new<obj_item_evidencebag>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:295
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:104
public partial class obj_item_storage_box_firingpins : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:104
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:105
        {
            Dm.@new<obj_item_firing_pin>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:106
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:9
public partial class obj_item_storage_box_flashbangs : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:9
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:10
        {
            Dm.@new<obj_item_grenade_flashbang>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:11
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:29
public partial class obj_item_storage_box_flashes : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:29
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:30
        {
            Dm.@new<obj_item_assembly_flash_handheld>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:31
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:124
public partial class obj_item_storage_box_lasertagpins : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:129
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++)
        {
            Dm.@new<obj_item_firing_pin_tag_red>(this);
            Dm.@new<obj_item_firing_pin_tag_blue>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:140
public partial class obj_item_storage_box_handcuffs : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:140
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:141
        {
            Dm.@new<obj_item_restraints_handcuffs>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:142
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:150
public partial class obj_item_storage_box_zipties : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:150
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:151
        {
            Dm.@new<obj_item_restraints_handcuffs_cable_zipties>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:152
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:78
public partial class obj_item_storage_box_prisoner : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:78
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        Dm.@new<obj_item_card_id_advanced_prisoner_one>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:80
        Dm.@new<obj_item_card_id_advanced_prisoner_two>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:81
        Dm.@new<obj_item_card_id_advanced_prisoner_three>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:82
        Dm.@new<obj_item_card_id_advanced_prisoner_four>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:83
        Dm.@new<obj_item_card_id_advanced_prisoner_five>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:84
        Dm.@new<obj_item_card_id_advanced_prisoner_six>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:85
        Dm.@new<obj_item_card_id_advanced_prisoner_seven>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:86
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:59
public partial class obj_item_storage_box_teargas : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:59
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:60
        {
            Dm.@new<obj_item_grenade_chem_grenade_teargas>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:61
        }
    }
}

// M7x slice D: the shotgun shell boxes (security_boxes.dm:164-210).

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:170
public partial class obj_item_storage_box_rubbershot : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:170
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:171
        {
            Dm.@new<obj_item_ammo_casing_shotgun_rubbershot>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:172
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:180
public partial class obj_item_storage_box_lethalshot : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:180
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:181
        {
            Dm.@new<obj_item_ammo_casing_shotgun_buckshot>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:182
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:186
public partial class obj_item_storage_box_lethalshot_old : obj_item_storage_box_lethalshot
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:186
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:187
        {
            Dm.@new<obj_item_ammo_casing_shotgun_buckshot_old>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:188
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:196
public partial class obj_item_storage_box_slugs : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:196
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:197
        {
            Dm.@new<obj_item_ammo_casing_shotgun>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:198
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:206
public partial class obj_item_storage_box_beanbag : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:206
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:207
        {
            Dm.@new<obj_item_ammo_casing_shotgun_beanbag>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:208
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:216
public partial class obj_item_storage_box_breacherslug : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:216
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:217
        {
            Dm.@new<obj_item_ammo_casing_shotgun_breacher>(this); // tg: code/game/objects/items/storage/boxes/security_boxes.dm:218
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:234  (gaps J5, D298)
public partial class obj_item_storage_box_emptysandbags : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:234
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/security_boxes.dm:235
            Dm.@new<obj_item_emptysandbag>(this);
    }
}

// Gaps SR1 (D333): the file's other boxes (stingbangs, the wall flash kit, EMPs, the security cartridges, the paywall and syndicate
// firing pins, laser tag pins, alien handcuffs, large darts, holy water grenades, fireworks, firecrackers and sparklers).

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:19
public partial class obj_item_storage_box_stingbangs : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:19
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 5; i++)
        {
            Dm.@new<obj_item_grenade_stingbang>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:68
public partial class obj_item_storage_box_emps : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:68
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 5; i++)
        {
            Dm.@new<obj_item_grenade_empgrenade>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:94
public partial class obj_item_storage_box_seccarts : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:94
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++)
        {
            Dm.@new<obj_item_disk_computer_security>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:112
public partial class obj_item_storage_box_firingpins_paywall : obj_item_storage_box_firingpins
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:112
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 5; i++)
        {
            Dm.@new<obj_item_firing_pin_paywall>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:120
public partial class obj_item_storage_box_firingpins_syndicate : obj_item_storage_box_firingpins
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:120
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 5; i++)
        {
            Dm.@new<obj_item_firing_pin_implant_pindicate>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:160
public partial class obj_item_storage_box_alienhandcuffs : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:160
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_restraints_handcuffs_alien>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:226
public partial class obj_item_storage_box_large_dart : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:226
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_ammo_casing_shotgun_dart_large>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:243
public partial class obj_item_storage_box_holy_grenades : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:243
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_grenade_chem_grenade_holy>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:252
public partial class obj_item_storage_box_fireworks : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:252
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++)
        {
            Dm.@new<obj_item_sparkler>(this);
            Dm.@new<obj_item_grenade_firecracker>(this);
        }
        Dm.@new<obj_item_toy_snappop>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:261
public partial class obj_item_storage_box_fireworks_dangerous : obj_item_storage_box_fireworks
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:261
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++)
        {
            Dm.@new<obj_item_sparkler>(this);
            Dm.@new<obj_item_grenade_firecracker>(this);
        }
        if (prob(20))
        {
            Dm.@new<obj_item_grenade_frag>(this);
        }
        else
        {
            Dm.@new<obj_item_toy_snappop>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:276
public partial class obj_item_storage_box_firecrackers : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:276
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_grenade_firecracker>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:285
public partial class obj_item_storage_box_sparklers : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:285
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_sparkler>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/security_boxes.dm:33
public partial class obj_item_storage_box_wall_flash : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/security_boxes.dm:39
    public override async DmTask PopulateContents()
    {
        float id = rand(1000, 9999);
        // FIXME what if this conflicts with an existing one?

        Dm.@new<obj_item_wallframe_button>(this);
        Dm.@new<obj_item_electronics_airlock>(this);
        obj_item_assembly_control_flasher remote = Dm.@new<obj_item_assembly_control_flasher>(this);
        remote.id = id;
        obj_item_wallframe_flasher frame = Dm.@new<obj_item_wallframe_flasher>(this);
        frame.id = id;
        Dm.@new<obj_item_assembly_flash_handheld>(this);
        Dm.@new<obj_item_screwdriver>(this);
    }
}

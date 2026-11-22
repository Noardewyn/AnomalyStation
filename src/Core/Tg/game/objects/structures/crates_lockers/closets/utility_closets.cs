// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/utility_closets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:162
public partial class obj_structure_closet_bombcloset : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:162
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_suit_utility_bomb_suit>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:164
        Dm.@new<obj_item_clothing_under_color_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:165
        Dm.@new<obj_item_clothing_shoes_sneakers_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:166
        Dm.@new<obj_item_clothing_head_utility_bomb_hood>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:167
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:169
public partial class obj_structure_closet_bombcloset_security : obj_structure_closet_bombcloset
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:169
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_suit_utility_bomb_suit_security>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:170
        Dm.@new<obj_item_clothing_under_rank_security_officer>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:171
        Dm.@new<obj_item_clothing_shoes_jackboots>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:172
        Dm.@new<obj_item_clothing_head_utility_bomb_hood_security>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:173
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:33
public partial class obj_structure_closet_emcloset : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:33
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        if (prob(40)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:36
        {
            Dm.@new<obj_item_storage_toolbox_emergency>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:37
        }
        object sw__1 = pick_weight(Dm.list(Dm.kv("small", 20), Dm.kv("aid", 20), Dm.kv("tank", 20), Dm.kv("both", 30), Dm.kv("nothing", 10))); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:39
        if (Dm.eq(sw__1, "small"))
        {
            Dm.@new<obj_item_tank_internals_emergency_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:41
            Dm.@new<obj_item_tank_internals_emergency_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:42
            Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:43
            Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:44
        }
        else if (Dm.eq(sw__1, "aid"))
        {
            Dm.@new<obj_item_tank_internals_emergency_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:47
            Dm.@new<obj_item_storage_medkit_emergency>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:48
            Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:49
        }
        else if (Dm.eq(sw__1, "tank"))
        {
            Dm.@new<obj_item_tank_internals_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:52
            Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:53
        }
        else if (Dm.eq(sw__1, "both"))
        {
            Dm.@new<obj_item_tank_internals_emergency_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:56
            Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:57
        }
        else if (Dm.eq(sw__1, "nothing"))
        {
            ; // pass()
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:71
public partial class obj_structure_closet_firecloset : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:71
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_suit_utility_fire_firefighter>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:74
        Dm.@new<obj_item_clothing_mask_gas>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:75
        Dm.@new<obj_item_tank_internals_oxygen_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:76
        Dm.@new<obj_item_extinguisher>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:77
        Dm.@new<obj_item_clothing_head_utility_hardhat_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:78
        Dm.@new<obj_item_crowbar_large_emergency>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:79
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:148
public partial class obj_structure_closet_radiation : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:148
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_geiger_counter>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:150
        Dm.@new<obj_item_clothing_suit_utility_radiation>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:151
        Dm.@new<obj_item_clothing_head_utility_radiation>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:152
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:99
public partial class obj_structure_closet_toolcloset : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:99
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        if (prob(40)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:101
        {
            Dm.@new<obj_item_clothing_suit_hazardvest>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:102
        }
        if (prob(70)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:103
        {
            Dm.@new<obj_item_flashlight>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:104
        }
        if (prob(70)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:105
        {
            Dm.@new<obj_item_screwdriver>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:106
        }
        if (prob(70)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:107
        {
            Dm.@new<obj_item_wrench>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:108
        }
        if (prob(70)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:109
        {
            Dm.@new<obj_item_weldingtool>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:110
        }
        if (prob(70)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:111
        {
            Dm.@new<obj_item_crowbar>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:112
        }
        if (prob(70)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:113
        {
            Dm.@new<obj_item_wirecutters>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:114
        }
        if (prob(70)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:115
        {
            Dm.@new<obj_item_t_scanner>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:116
        }
        if (prob(20)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:117
        {
            Dm.@new<obj_item_storage_belt_utility>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:118
        }
        if (prob(30)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:119
        {
            Dm.@new<obj_item_stack_cable_coil>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:120
        }
        if (prob(30)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:121
        {
            Dm.@new<obj_item_stack_cable_coil>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:122
        }
        if (prob(30)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:123
        {
            Dm.@new<obj_item_stack_cable_coil>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:124
        }
        if (prob(20)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:125
        {
            Dm.@new<obj_item_multitool>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:126
        }
        if (prob(40)) // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:128
        {
            Dm.@new<obj_item_clothing_head_utility_hardhat>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:129
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:23
public partial class obj_structure_closet_emcloset
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (HAS_TRAIT(SSstation, STATION_TRAIT_RADIOACTIVE_NEBULA) && prob(30))
        {
            Dm.@new<obj_structure_closet_radiation>(loc);
            return INITIALIZE_HINT_QDEL;
        }

        object dot = base.Initialize(mapload, __extra);

        if (prob(1))
            return INITIALIZE_HINT_QDEL;
        return dot;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:132
public partial class obj_structure_closet_toolcloset
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:132
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        // Since they're a traitor objective, they have to be generated immediately.
        if (prob(5))
            Dm.@new<obj_item_clothing_gloves_color_yellow>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:81  (gaps ST3, D333)
public partial class obj_structure_closet_firecloset_full : obj_structure_closet_firecloset
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:81
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_suit_utility_fire_firefighter>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:82
        Dm.@new<obj_item_clothing_mask_gas>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:83
        Dm.@new<obj_item_flashlight>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:84
        Dm.@new<obj_item_tank_internals_oxygen_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:85
        Dm.@new<obj_item_extinguisher>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:86
        Dm.@new<obj_item_clothing_head_utility_hardhat_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:87
        Dm.@new<obj_item_crowbar_large_emergency>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:88
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:175  (gaps ST3, D333)
public partial class obj_structure_closet_bombcloset_white : obj_structure_closet_bombcloset
{
    // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:175
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_suit_utility_bomb_suit_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:176
        Dm.@new<obj_item_clothing_under_color_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:177
        Dm.@new<obj_item_clothing_shoes_sneakers_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:178
        Dm.@new<obj_item_clothing_head_utility_bomb_hood_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/utility_closets.dm:179
    }
}

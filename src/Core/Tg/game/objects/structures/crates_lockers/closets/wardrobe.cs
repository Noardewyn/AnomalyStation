// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/wardrobe.dm
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

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:6
public partial class obj_structure_closet_wardrobe : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:6
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:8
        {
            Dm.@new<obj_item_clothing_under_color_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:9
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:10
        {
            Dm.@new<obj_item_clothing_under_color_jumpskirt_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:11
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:12
        {
            Dm.@new<obj_item_clothing_shoes_sneakers_brown>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:13
        }
        return;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:33
public partial class obj_structure_closet_wardrobe_black : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:33
    public override async DmTask PopulateContents(params object[] __extra)
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:34
        {
            Dm.@new<obj_item_clothing_under_color_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:35
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:36
        {
            Dm.@new<obj_item_clothing_under_color_jumpskirt_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:37
        }
        if (prob(25)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:38
        {
            Dm.@new<obj_item_clothing_suit_jacket_leather>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:39
        }
        if (prob(20)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:40
        {
            Dm.@new<obj_item_clothing_suit_jacket_leather_biker>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:41
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:42
        {
            Dm.@new<obj_item_clothing_shoes_sneakers_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:43
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:44
        {
            Dm.@new<obj_item_clothing_head_hats_tophat>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:45
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:46
        {
            Dm.@new<obj_item_clothing_head_soft_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:47
        }
        Dm.@new<obj_item_clothing_mask_bandana_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:48
        Dm.@new<obj_item_clothing_mask_bandana_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:49
        if (prob(40)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:50
        {
            Dm.@new<obj_item_clothing_mask_bandana_skull_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:51
        }
        return;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:58
public partial class obj_structure_closet_wardrobe_green : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:58
    public override async DmTask PopulateContents(params object[] __extra)
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:59
        {
            Dm.@new<obj_item_clothing_under_color_green>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:60
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:61
        {
            Dm.@new<obj_item_clothing_under_color_jumpskirt_green>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:62
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:63
        {
            Dm.@new<obj_item_clothing_shoes_sneakers_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:64
        }
        Dm.@new<obj_item_clothing_mask_bandana_green>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:65
        Dm.@new<obj_item_clothing_mask_bandana_green>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:66
        return;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:136
public partial class obj_structure_closet_wardrobe_grey : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:136
    public override async DmTask PopulateContents(params object[] __extra)
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:137
        {
            Dm.@new<obj_item_clothing_under_color_grey>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:138
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:139
        {
            Dm.@new<obj_item_clothing_under_color_jumpskirt_grey>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:140
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:141
        {
            Dm.@new<obj_item_clothing_shoes_sneakers_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:142
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:143
        {
            Dm.@new<obj_item_clothing_head_soft_grey>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:144
        }
        if (prob(50)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:145
        {
            Dm.@new<obj_item_storage_backpack_duffelbag>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:146
        }
        if (prob(40)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:147
        {
            Dm.@new<obj_item_clothing_mask_bandana_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:148
            Dm.@new<obj_item_clothing_mask_bandana_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:149
        }
        if (prob(40)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:150
        {
            Dm.@new<obj_item_clothing_under_misc_assistantformal>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:151
        }
        if (prob(40)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:152
        {
            Dm.@new<obj_item_clothing_under_misc_assistantformal>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:153
        }
        if (prob(30)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:154
        {
            Dm.@new<obj_item_clothing_suit_hooded_wintercoat>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:155
            Dm.@new<obj_item_clothing_shoes_winterboots>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:156
        }
        if (prob(30)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:157
        {
            Dm.@new<obj_item_clothing_accessory_pocketprotector>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:158
        }
        return;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:165
public partial class obj_structure_closet_wardrobe_mixed : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:165
    public override async DmTask PopulateContents(params object[] __extra)
    {
        if (prob(40)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:166
        {
            Dm.@new<obj_item_clothing_suit_jacket_bomber>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:167
        }
        Dm.@new<obj_item_clothing_under_color_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:168
        Dm.@new<obj_item_clothing_under_color_jumpskirt_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:169
        Dm.@new<obj_item_clothing_under_color_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:170
        Dm.@new<obj_item_clothing_under_color_jumpskirt_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:171
        Dm.@new<obj_item_clothing_under_color_yellow>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:172
        Dm.@new<obj_item_clothing_under_color_jumpskirt_yellow>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:173
        Dm.@new<obj_item_clothing_under_color_green>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:174
        Dm.@new<obj_item_clothing_under_color_jumpskirt_green>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:175
        Dm.@new<obj_item_clothing_under_color_orange>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:176
        Dm.@new<obj_item_clothing_under_color_jumpskirt_orange>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:177
        Dm.@new<obj_item_clothing_under_color_pink>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:178
        Dm.@new<obj_item_clothing_under_color_jumpskirt_pink>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:179
        Dm.@new<obj_item_clothing_under_color_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:180
        Dm.@new<obj_item_clothing_under_color_jumpskirt_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:181
        Dm.@new<obj_item_clothing_under_color_darkblue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:182
        Dm.@new<obj_item_clothing_under_color_jumpskirt_darkblue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:183
        Dm.@new<obj_item_clothing_under_color_teal>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:184
        Dm.@new<obj_item_clothing_under_color_jumpskirt_teal>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:185
        Dm.@new<obj_item_clothing_under_color_lightpurple>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:186
        Dm.@new<obj_item_clothing_under_color_jumpskirt_lightpurple>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:187
        Dm.@new<obj_item_clothing_mask_bandana_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:188
        Dm.@new<obj_item_clothing_mask_bandana_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:189
        Dm.@new<obj_item_clothing_mask_bandana_gold>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:190
        Dm.@new<obj_item_clothing_shoes_sneakers_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:191
        Dm.@new<obj_item_clothing_shoes_sneakers_brown>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:192
        Dm.@new<obj_item_clothing_shoes_sneakers_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:193
        if (prob(30)) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:194
        {
            Dm.@new<obj_item_clothing_suit_hooded_wintercoat>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:195
            Dm.@new<obj_item_clothing_shoes_winterboots>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:196
        }
        return;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:119
public partial class obj_structure_closet_wardrobe_pjs : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:119
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_under_misc_pj_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:120
        Dm.@new<obj_item_clothing_under_misc_pj_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:121
        Dm.@new<obj_item_clothing_head_costume_nightcap_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:122
        Dm.@new<obj_item_clothing_head_costume_nightcap_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:123
        Dm.@new<obj_item_clothing_under_misc_pj_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:124
        Dm.@new<obj_item_clothing_under_misc_pj_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:125
        Dm.@new<obj_item_clothing_head_costume_nightcap_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:126
        Dm.@new<obj_item_clothing_head_costume_nightcap_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:127
        for (float i = 1; i <= 4; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:128
        {
            Dm.@new<obj_item_clothing_shoes_sneakers_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:129
        }
        return;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:102
public partial class obj_structure_closet_wardrobe_white : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:102
    public override async DmTask PopulateContents(params object[] __extra)
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:103
        {
            Dm.@new<obj_item_clothing_under_color_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:104
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:105
        {
            Dm.@new<obj_item_clothing_under_color_jumpskirt_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:106
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:107
        {
            Dm.@new<obj_item_clothing_shoes_sneakers_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:108
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:109
        {
            Dm.@new<obj_item_clothing_head_soft_mime>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:110
        }
        Dm.@new<obj_item_clothing_mask_bandana_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:111
        Dm.@new<obj_item_clothing_mask_bandana_white>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:112
        return;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:17  (gaps ST3, D333)
public partial class obj_structure_closet_wardrobe_pink : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:20
    public override async DmTask PopulateContents(params object[] __extra)
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:21
        {
            Dm.@new<obj_item_clothing_under_color_pink>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:22
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:23
        {
            Dm.@new<obj_item_clothing_under_color_jumpskirt_pink>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:24
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:25
        {
            Dm.@new<obj_item_clothing_shoes_sneakers_brown>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:26
        }
        return; // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:27
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:70  (gaps ST3, D333)
public partial class obj_structure_closet_wardrobe_orange : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:74
    public override async DmTask PopulateContents(params object[] __extra)
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:75
        {
            Dm.@new<obj_item_clothing_under_rank_prisoner>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:76
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:77
        {
            Dm.@new<obj_item_clothing_under_rank_prisoner_skirt>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:78
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:79
        {
            Dm.@new<obj_item_clothing_shoes_sneakers_orange>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:80
        }
        return; // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:81
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:84  (gaps ST3, D333)
public partial class obj_structure_closet_wardrobe_yellow : obj_structure_closet_wardrobe
{
    // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:87
    public override async DmTask PopulateContents(params object[] __extra)
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:88
        {
            Dm.@new<obj_item_clothing_under_color_yellow>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:89
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:90
        {
            Dm.@new<obj_item_clothing_under_color_jumpskirt_yellow>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:91
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:92
        {
            Dm.@new<obj_item_clothing_shoes_sneakers_orange>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:93
        }
        Dm.@new<obj_item_clothing_mask_bandana_gold>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:94
        Dm.@new<obj_item_clothing_mask_bandana_gold>(this); // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:95
        return; // tg: code/game/objects/structures/crates_lockers/closets/wardrobe.dm:96
    }
}

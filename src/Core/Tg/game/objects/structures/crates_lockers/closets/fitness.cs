// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/fitness.dm
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

// tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:6
public partial class obj_structure_closet_athletic_mixed : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:6
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_under_shorts_purple>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:8
        Dm.@new<obj_item_clothing_under_shorts_grey>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:9
        Dm.@new<obj_item_clothing_under_shorts_black>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:10
        Dm.@new<obj_item_clothing_under_shorts_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:11
        Dm.@new<obj_item_clothing_under_shorts_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:12
        Dm.@new<obj_item_clothing_under_shorts_green>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:13
        Dm.@new<obj_item_clothing_under_costume_jabroni>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:14
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:21
public partial class obj_structure_closet_boxinggloves : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:21
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_gloves_boxing_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:23
        Dm.@new<obj_item_clothing_gloves_boxing_green>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:24
        Dm.@new<obj_item_clothing_gloves_boxing_yellow>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:25
        Dm.@new<obj_item_clothing_gloves_boxing>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:26
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:59
public partial class obj_structure_closet_lasertag_blue : obj_structure_closet_lasertag
{
    // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:59
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:61
        {
            Dm.@new<obj_item_gun_energy_laser_bluetag>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:62
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:63
        {
            Dm.@new<obj_item_clothing_suit_bluetag>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:64
        }
        Dm.@new<obj_item_clothing_head_helmet_taghelm_blue>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:65
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:44
public partial class obj_structure_closet_lasertag_red : obj_structure_closet_lasertag
{
    // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:44
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:46
        {
            Dm.@new<obj_item_gun_energy_laser_redtag>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:47
        }
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:48
        {
            Dm.@new<obj_item_clothing_suit_redtag>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:49
        }
        Dm.@new<obj_item_clothing_head_helmet_taghelm_red>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:50
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:32
public partial class obj_structure_closet_masks : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:32
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_clothing_mask_luchador>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:34
        Dm.@new<obj_item_clothing_mask_luchador_rudos>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:35
        Dm.@new<obj_item_clothing_mask_luchador_tecnicos>(this); // tg: code/game/objects/structures/crates_lockers/closets/fitness.dm:36
    }
}

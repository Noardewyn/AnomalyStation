// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/l3closet.dm
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

// tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:6
public partial class obj_structure_closet_l3closet : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:6
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_storage_bag_bio>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:7
        Dm.@new<obj_item_clothing_suit_bio_suit_general>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:8
        Dm.@new<obj_item_clothing_head_bio_hood_general>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:9
        Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:10
        Dm.@new<obj_item_tank_internals_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:11
        Dm.@new<obj_item_reagent_containers_syringe_antiviral>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:12
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:51
public partial class obj_structure_closet_l3closet_janitor : obj_structure_closet_l3closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:51
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_suit_bio_suit_janitor>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:52
        Dm.@new<obj_item_clothing_head_bio_hood_janitor>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:53
        Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:54
        Dm.@new<obj_item_tank_internals_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:55
        Dm.@new<obj_item_reagent_containers_syringe_antiviral>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:56
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:17
public partial class obj_structure_closet_l3closet_scientist : obj_structure_closet_l3closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:17
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_storage_bag_xeno>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:18
        Dm.@new<obj_item_clothing_suit_bio_suit_scientist>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:19
        Dm.@new<obj_item_clothing_head_bio_hood_scientist>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:20
        Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:21
        Dm.@new<obj_item_tank_internals_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:22
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:40
public partial class obj_structure_closet_l3closet_security : obj_structure_closet_l3closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:40
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_suit_bio_suit_security>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:41
        Dm.@new<obj_item_clothing_head_bio_hood_security>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:42
        Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:43
        Dm.@new<obj_item_tank_internals_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:44
        Dm.@new<obj_item_reagent_containers_syringe_antiviral>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:45
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:28
public partial class obj_structure_closet_l3closet_virology : obj_structure_closet_l3closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:28
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_storage_bag_bio>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:29
        Dm.@new<obj_item_clothing_suit_bio_suit_virology>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:30
        Dm.@new<obj_item_clothing_head_bio_hood_virology>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:31
        Dm.@new<obj_item_clothing_mask_breath>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:32
        Dm.@new<obj_item_tank_internals_oxygen>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:33
        Dm.@new<obj_item_reagent_containers_syringe_antiviral>(this); // tg: code/game/objects/structures/crates_lockers/closets/l3closet.dm:34
    }
}

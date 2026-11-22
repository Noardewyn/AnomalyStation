// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/bar.dm
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

// tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:14
public partial class obj_structure_closet_secure_closet_bar : obj_structure_closet_secure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:14
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 10; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:16
        {
            Dm.@new<obj_item_reagent_containers_cup_glass_bottle_beer>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:17
        }
        Dm.@new<obj_item_etherealballdeployer>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:18
        Dm.@new<obj_item_roulette_wheel_beacon>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:19
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:24  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_bar_lavaland_bartender_booze : obj_structure_closet_secure_closet_bar
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:24
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_vending_refill_cigarette>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:25
        Dm.@new<obj_item_vending_refill_boozeomat>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:26
        Dm.@new<obj_item_storage_backpack_duffelbag>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:27
        Dm.@new<obj_item_etherealballdeployer>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:28
        for (float i = 1; i <= 14; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:29
        {
            Dm.@new<obj_item_reagent_containers_cup_glass_bottle_beer_light>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:30
        }
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:31
        {
            Dm.@new<obj_item_reagent_containers_cup_glass_colocup>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:32
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:35  (gaps ST3, D333)
public partial class obj_structure_closet_secure_closet_bar_lavaland_bartender_clothes : obj_structure_closet_secure_closet_bar
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:37
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_clothing_neck_beads>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:38
        Dm.@new<obj_item_clothing_glasses_sunglasses_reagent>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:39
        Dm.@new<obj_item_clothing_suit_costume_hawaiian>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:40
        Dm.@new<obj_item_clothing_shoes_sandal_beach>(this); // tg: code/game/objects/structures/crates_lockers/closets/secure/bar.dm:41
    }
}

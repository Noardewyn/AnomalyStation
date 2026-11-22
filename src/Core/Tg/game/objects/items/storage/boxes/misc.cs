// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate teleporter kit (the syndicate campaign G2, D316); the rest of the file (gaps SR1, D333). The var blocks are generated
// (D111).

// tg: code/game/objects/items/storage/boxes/misc.dm:1
public partial class obj_item_storage_box_syndie_kit_syndicate_teleporter : obj_item_storage_box_syndie_kit
{
    // tg: code/game/objects/items/storage/boxes/misc.dm:4
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_syndicate_teleporter>(this);
        Dm.@new<obj_item_paper_syndicate_teleporter>(this);
    }
}

// Gaps SR1 (D333): the alchemist's kits, the mecha tracking beacons and the dealers' boxes.

// tg: code/game/objects/items/storage/boxes/misc.dm:37
public partial class obj_item_storage_box_alchemist_chemistry_kit : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/misc.dm:37
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_cup_mortar>(this);
        Dm.@new<obj_item_pestle>(this);
        Dm.@new<obj_item_lighter_skull>(this);
        Dm.@new<obj_item_ph_booklet>(this);
        Dm.@new<obj_item_thermometer>(this);
        Dm.@new<obj_item_storage_test_tube_rack_full>(this);
        Dm.@new<obj_item_reagent_containers_cup_glass_coffee>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/misc.dm:50
public partial class obj_item_storage_box_mechabeacons : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/misc.dm:50
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        Dm.@new<obj_item_mecha_parts_mecha_tracking>(this);
        Dm.@new<obj_item_mecha_parts_mecha_tracking>(this);
        Dm.@new<obj_item_mecha_parts_mecha_tracking>(this);
        Dm.@new<obj_item_mecha_parts_mecha_tracking>(this);
        Dm.@new<obj_item_mecha_parts_mecha_tracking>(this);
        Dm.@new<obj_item_mecha_parts_mecha_tracking>(this);
        Dm.@new<obj_item_mecha_parts_mecha_tracking>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/misc.dm:65
public partial class obj_item_storage_box_methdealer : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/misc.dm:65
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_food_drug_meth_crystal), 4),
            Dm.kv(typeof(obj_item_cigarette_pipe_crackpipe), 2));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/misc.dm:77
public partial class obj_item_storage_box_opiumdealer : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/misc.dm:77
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_food_drug_opium), 4),
            Dm.kv(typeof(obj_item_cigarette_pipe_cobpipe), 2));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/misc.dm:89
public partial class obj_item_storage_box_kronkdealer : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/misc.dm:89
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_food_drug_moon_rock), 4),
            Dm.kv(typeof(obj_item_cigarette_pipe_crackpipe), 2));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/misc.dm:9
public partial class obj_item_storage_box_alchemist_basic_chems : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/misc.dm:13
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            if (prob(1))
            {
                Dm.@new<obj_item_reagent_containers_cup_glass_coffee>(this);
                continue;
            }
            Dm.@new<obj_item_reagent_containers_cup_bottle_alchemist_basic>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/misc.dm:22
public partial class obj_item_storage_box_alchemist_random_chems : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/misc.dm:26
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            if (prob(1))
            {
                Dm.@new<obj_item_reagent_containers_cup_glass_coffee>(this);
                continue;
            }
            Dm.@new<obj_item_reagent_containers_cup_bottle_alchemist_random>(this);
        }
    }
}

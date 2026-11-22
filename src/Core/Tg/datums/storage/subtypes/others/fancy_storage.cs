// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/others/fancy_storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the cigarette, rolling paper and cigar storages came with gaps J3 (D298); gaps H2 (D333) brought the food
// boxes' (donuts, eggs, candles, chocolates, nuggets, wings, fries, pickles) and the coffee condiments display's and cartridge
// rack's.

/// Donut Boxes
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:6
public partial class datum_storage_donut_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:9
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_food_donut));
    }
}

/// Egg Boxes
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:14
public partial class datum_storage_egg_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:19
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_food_egg),
            typeof(obj_item_food_chocolateegg),
            typeof(obj_item_food_boiledegg),
            typeof(obj_item_food_scotchegg),
            typeof(obj_item_food_grown_eggy),
            typeof(obj_item_surprise_egg)));
    }
}

/// Candle Boxes
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:31
public partial class datum_storage_candle_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:34
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_flashlight_flare_candle));
    }
}

/// Cigarette Boxes
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:40
public partial class datum_storage_cigarette_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:45
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_cigarette),
            typeof(obj_item_lighter)));
    }
}

/// Rolling Paper Packs
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:53
public partial class datum_storage_rolling_paper_pack : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:57
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_rollingpaper));
    }
}

/// Cigar Boxes
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:62
public partial class datum_storage_cigar_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:66
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(typeof(obj_item_cigarette_cigar)));
    }
}

/// Heart-Shaped Chocolate Boxes
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:73
public partial class datum_storage_heart_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:77
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_food_bonbon));
    }
}

/// Nugget Boxes
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:82
public partial class datum_storage_nugget_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:85
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_food_nugget),
            typeof(obj_item_fish_fryish)));
    }
}

/// Wing Boxes
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:93
public partial class datum_storage_wing_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:96
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_food_fried_chicken)));
    }
}

/// Fry Boxes
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:103
public partial class datum_storage_fry_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:106
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_food_fries),
            typeof(obj_item_food_cheesyfries),
            typeof(obj_item_food_carrotfries)));
    }
}

/// Jars of Pickles
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:115
public partial class datum_storage_pickles_jar : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:120
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_food_pickle));
    }
}

// tg: code/datums/storage/subtypes/others/fancy_storage.dm:125
public partial class datum_storage_coffee_condi_display : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:128
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_reagent_containers_condiment_pack_sugar),
            typeof(obj_item_reagent_containers_condiment_creamer),
            typeof(obj_item_reagent_containers_condiment_pack_astrotame),
            typeof(obj_item_reagent_containers_condiment_chocolate)));
    }
}

/// Coffee Cart Racks
// tg: code/datums/storage/subtypes/others/fancy_storage.dm:138
public partial class datum_storage_coffee_cart_rack : datum_storage
{
    // tg: code/datums/storage/subtypes/others/fancy_storage.dm:141
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_coffee_cartridge));
    }
}

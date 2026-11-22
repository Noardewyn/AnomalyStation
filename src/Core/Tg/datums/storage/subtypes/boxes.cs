// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The balloon box and the matchbox (gaps J2 and J3, D298); the syndicate space and throwing weapons boxes (syndicate G3); the Donk
// pocket box (syndicate N2b, the Donk pinata drops it); the rest of the file (gaps SR1, D333): the Donk flechette box, the bandage,
// gorilla cube, snap pop, light, gum, fishing lure and beef ramen boxes.

///Donk Pocket box
// tg: code/datums/storage/subtypes/boxes.dm:15
public partial class datum_storage_box_donk_pockets : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:15
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_food_donkpocket));
    }
}

// tg: code/datums/storage/subtypes/boxes.dm:103
public partial class datum_storage_box_balloon : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:107
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_toy_balloon_long));
    }
}

// tg: code/datums/storage/subtypes/boxes.dm:81
public partial class datum_storage_box_match : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:84
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_match));
    }
}

// tg: code/datums/storage/subtypes/boxes.dm:48 (gaps package C5)
public partial class datum_storage_box_monkey_cube : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:50
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(
            can_hold_list: typeof(obj_item_food_monkeycube),
            cant_hold_list: typeof(obj_item_food_monkeycube_gorilla));
    }
}

///Syndicate space box
// tg: code/datums/storage/subtypes/boxes.dm:121
public partial class datum_storage_box_syndicate_space : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:124
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_clothing_suit_space_syndicate),
            typeof(obj_item_clothing_head_helmet_space_syndicate)));
    }
}

///Syndicate throwing weapons box
// tg: code/datums/storage/subtypes/boxes.dm:136
public partial class datum_storage_box_syndicate_throwing : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:141
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_restraints_legcuffs_bola_tactical),
            typeof(obj_item_paperplane_syndicate),
            typeof(obj_item_throwing_star)));
    }
}

// tg: code/datums/storage/subtypes/boxes.dm:150  (gaps R2: the skub fan pack)
public partial class datum_storage_box_skub : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:153
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(exception_hold_list: Dm.list(
            typeof(obj_item_skub),
            typeof(obj_item_clothing_suit_costume_wellworn_shirt_skub)));
    }
}

///Stickers anti skub box
// tg: code/datums/storage/subtypes/boxes.dm:161  (gaps R2: the anti-skub stickers pack)
public partial class datum_storage_box_anti_skub : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:161
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(exception_hold_list: typeof(obj_item_clothing_suit_costume_wellworn_shirt_skub));
    }
}

///Coffee box
// tg: code/datums/storage/subtypes/boxes.dm:27 (gaps H2, D333: the impressa coffeemaker's coffee beans)
public partial class datum_storage_box_coffee : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:30
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_food_grown_coffee));
    }
}

// tg: code/datums/storage/subtypes/boxes.dm:19
public partial class datum_storage_box_donk_bullets : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:19
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_food_donkpocket),
            typeof(obj_item_ammo_casing_shotgun)));
    }
}

///Bandage box
// tg: code/datums/storage/subtypes/boxes.dm:35
public partial class datum_storage_box_bandages : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:38
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_stack_medical_bandage),
            typeof(obj_item_reagent_containers_applicator_pill),
            typeof(obj_item_reagent_containers_applicator_patch)));
    }
}

///Gorilla cube box
// tg: code/datums/storage/subtypes/boxes.dm:58
public partial class datum_storage_box_gorilla_cube_box : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:61
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_food_monkeycube_gorilla));
    }
}

///Snappops box
// tg: code/datums/storage/subtypes/boxes.dm:73
public partial class datum_storage_box_snappops : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:76
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_toy_snappop));
    }
}

///Light Box
// tg: code/datums/storage/subtypes/boxes.dm:89
public partial class datum_storage_box_lights : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:94
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_light_tube),
            typeof(obj_item_light_bulb)));
    }
}

///Gum Box
// tg: code/datums/storage/subtypes/boxes.dm:170
public partial class datum_storage_box_gum : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:174
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_storage_bubblegum_wrapper));
    }
}

///Fishing lures box
// tg: code/datums/storage/subtypes/boxes.dm:178
public partial class datum_storage_box_fishing_lures : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:179
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        //adds an extra slot, so we can put back the lures even if we didn't take out the instructions.
        // tg's `var/static/slots` (:181): the count of the lure types, the same on every call (rules 10.6, a constant)
        float slots = Dm.length(typesof(typeof(obj_item_fishing_lure))) + 1;
        max_slots = slots;
        max_total_storage = WEIGHT_CLASS_SMALL * slots;
        await base.New(__extra);
        set_holdable(typeof(obj_item_fishing_lure)); //can only hold lures
    }
}

// tg: code/datums/storage/subtypes/boxes.dm:187
public partial class datum_storage_box_ramen_beef : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:190
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_reagent_containers_condiment_pack_beef_flavour),
            typeof(obj_item_food_spaghetti_ramen_dry)));
    }
}

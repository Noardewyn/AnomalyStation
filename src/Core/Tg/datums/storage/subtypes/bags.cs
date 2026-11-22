// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/bags.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SR1 (D333): the bags' storage kinds left (what the sheet snatchers, the book, chemistry, bio, science and construction bags,
// the quivers, the garment bags, the money bag and the fish bag hold). The trash bag's, the ore satchel's, the plant bag's, the
// tray's, the mail bag's and the rebar quiver's are in game/objects/items/storage/bags.cs with their bags. The var blocks are
// generated (D111).

///Sheet snatcher bag
// tg: code/datums/storage/subtypes/bags.dm:65
public partial class datum_storage_bag_sheet_snatcher : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:68
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(
            can_hold_list: typeof(obj_item_stack_sheet),
            cant_hold_list: Dm.list(
                typeof(obj_item_stack_sheet_mineral_sandstone),
                typeof(obj_item_stack_sheet_mineral_wood)));
    }
}

///Debug sheet snatcher bag
// tg: code/datums/storage/subtypes/bags.dm:83
public partial class datum_storage_bag_sheet_snatcher_debug : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:89
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_stack_sheet),
            typeof(obj_item_stack_sheet_mineral_sandstone),
            typeof(obj_item_stack_sheet_mineral_wood)));
    }
}

///Book bag
// tg: code/datums/storage/subtypes/bags.dm:98
public partial class datum_storage_bag_books : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:102
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_book),
            typeof(obj_item_spellbook),
            typeof(obj_item_poster)));
    }
}

///Chemistry bag
// tg: code/datums/storage/subtypes/bags.dm:142
public partial class datum_storage_bag_chemistry : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:146
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_reagent_containers_chem_pack),
            typeof(obj_item_reagent_containers_dropper),
            typeof(obj_item_reagent_containers_cup_glass_waterbottle),
            typeof(obj_item_reagent_containers_cup_beaker),
            typeof(obj_item_reagent_containers_cup_bottle),
            typeof(obj_item_reagent_containers_cup_tube),
            typeof(obj_item_reagent_containers_medigel),
            typeof(obj_item_reagent_containers_applicator),
            typeof(obj_item_reagent_containers_syringe)));
    }
}

///Bio bag
// tg: code/datums/storage/subtypes/bags.dm:161
public partial class datum_storage_bag_bio : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:165
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_bodypart),
            typeof(obj_item_food_monkeycube),
            typeof(obj_item_healthanalyzer),
            typeof(obj_item_organ),
            typeof(obj_item_reagent_containers_blood),
            typeof(obj_item_reagent_containers_dropper),
            typeof(obj_item_reagent_containers_cup_beaker),
            typeof(obj_item_reagent_containers_cup_bottle),
            typeof(obj_item_reagent_containers_cup_tube),
            typeof(obj_item_reagent_containers_hypospray_medipen),
            typeof(obj_item_reagent_containers_syringe)));
    }
}

///Xeno bag
// tg: code/datums/storage/subtypes/bags.dm:182
public partial class datum_storage_bag_xeno : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:186
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_bodypart),
            typeof(obj_item_food_deadmouse),
            typeof(obj_item_food_monkeycube),
            typeof(obj_item_organ),
            typeof(obj_item_petri_dish),
            typeof(obj_item_reagent_containers_dropper),
            typeof(obj_item_reagent_containers_cup_beaker),
            typeof(obj_item_reagent_containers_cup_bottle),
            typeof(obj_item_reagent_containers_cup_tube),
            typeof(obj_item_reagent_containers_syringe),
            typeof(obj_item_slime_extract),
            typeof(obj_item_swab)));
    }
}

///Construction bag
// tg: code/datums/storage/subtypes/bags.dm:204
public partial class datum_storage_bag_construction : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:209
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_assembly),
            typeof(obj_item_circuitboard),
            typeof(obj_item_electronics),
            typeof(obj_item_reagent_containers_cup_beaker),
            typeof(obj_item_stack_cable_coil),
            typeof(obj_item_stack_ore_bluespace_crystal),
            typeof(obj_item_stock_parts),
            typeof(obj_item_wallframe_camera),
            typeof(obj_item_rcd_ammo)), exception_hold_list: Dm.list(
            typeof(obj_item_stack_sheet),
            typeof(obj_item_stack_rods)));
    }
}

///Harpoon quiver bag
// tg: code/datums/storage/subtypes/bags.dm:227
public partial class datum_storage_bag_harpoon_quiver : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:232
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_ammo_casing_harpoon));
    }
}

///Garment bag
// tg: code/datums/storage/subtypes/bags.dm:269
public partial class datum_storage_bag_garment : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:276
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(
            can_hold_list: Dm.list(typeof(obj_item_clothing), typeof(obj_item_storage_backpack)),
            exception_hold_list: Dm.list(typeof(obj_item_storage_backpack)));
    }

    // tg: code/datums/storage/subtypes/bags.dm:283
    public override object can_insert(obj_item to_insert = null, mob user = null, float? messages = null, float? force = null)
    {
        object dot = base.can_insert(to_insert, user, messages, force);
        if (!Dm.truthy(dot))
            return FALSE;

        if (istype(to_insert, typeof(obj_item_storage_backpack)) && Dm.truthy(Dm.length(to_insert.contents)))
        {
            if (Dm.truthy(messages) && Dm.truthy(user))
                parent.balloon_alert(user, I18n.T("can't store filled backpacks!"));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/datums/storage/subtypes/bags.dm:295
    public override void item_init(datum source = null, obj_item inited = null)
    {
        base.item_init(source, inited);
        if (!istype(inited, typeof(obj_item_storage_backpack)))
            return;
        RegisterSignal(inited, COMSIG_ATOM_PRE_STORED_ITEM, nameof(block_inner_storage));
    }

    // tg: code/datums/storage/subtypes/bags.dm:301
    public override void handle_enter(datum source = null, obj_item arrived = null)
    {
        base.handle_enter(source, arrived);
        if (!istype(arrived, typeof(obj_item_storage_backpack)))
            return;
        RegisterSignal(arrived, COMSIG_ATOM_PRE_STORED_ITEM, nameof(block_inner_storage));
    }

    // tg: code/datums/storage/subtypes/bags.dm:307
    public override void handle_exit(datum source = null, obj_item gone = null)
    {
        base.handle_exit(source, gone);
        UnregisterSignal(gone, COMSIG_ATOM_PRE_STORED_ITEM);
    }

    // tg: code/datums/storage/subtypes/bags.dm:311
    [SignalHandler]
    public virtual object block_inner_storage(obj_item backpack = null, object to_insert = null, object user = null, object force = null, object messages = null, params object[] __extra)
    {
        if (Dm.truthy(user) && Dm.truthy(messages))
            parent.balloon_alert(user as mob, I18n.T("garment bag is in the way!"));
        return BLOCK_STORAGE_INSERT;
    }
}

///Quiver bag
// tg: code/datums/storage/subtypes/bags.dm:319
public partial class datum_storage_bag_quiver : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:324
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_ammo_casing_arrow));
    }
}

///Quiver bag endless
// tg: code/datums/storage/subtypes/bags.dm:333
public partial class datum_storage_bag_quiver_endless : datum_storage_bag_quiver
{
    // tg: code/datums/storage/subtypes/bags.dm:336
    public override void handle_exit(datum source = null, obj_item gone = null)
    {
        base.handle_exit(source, gone);
        obj_item_storage_bag_quiver_endless store = real_location as obj_item_storage_bag_quiver_endless;
        Dm.@new(store.arrow_path, store);
    }
}

///Money bag
// tg: code/datums/storage/subtypes/bags.dm:342
public partial class datum_storage_bag_money : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:347
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_coin),
            typeof(obj_item_stack_spacecash),
            typeof(obj_item_holochip),
            typeof(obj_item_poker_chip),
            typeof(obj_item_stack_sheet_mineral_diamond), // Precious gems and stones are something a sophisticated french thief might put in his thief bag.
            typeof(obj_item_stack_sheet_mineral_gold), // This is money, the only real money actually, if you ask a Ron Paul-style guy.
            typeof(obj_item_stack_sheet_mineral_silver),
            typeof(obj_item_stack_sheet_mineral_mythril),
            typeof(obj_item_stack_telecrystal)));
    }
}

///Fishing bag
// tg: code/datums/storage/subtypes/bags.dm:363
public partial class datum_storage_bag_fishing : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:367
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_fish));
    }
}

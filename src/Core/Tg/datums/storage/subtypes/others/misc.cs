// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/others/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the pill bottle's storage (M8 slice C), the crayon box's (gaps J3), the surgery tray's (gaps P1) and the portable
// chem mixer's (gaps P5), the implant's (syndicate G3), the briefcase gun's (syndicate G4a), the dice bag's (gaps DE1); the test
// tube rack's, the organ box's and the can ring's (gaps SR1); the toilet bong's (gaps ST5); the trading card binder's; the file's other storages come with
// their items.

///Surgery tray
// tg: code/datums/storage/subtypes/others/misc.dm:16
public partial class datum_storage_surgery_tray : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:21
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(can_hold_list: Dm.list(
            typeof(obj_item_autopsy_scanner),
            typeof(obj_item_blood_filter),
            typeof(obj_item_bonesetter),
            typeof(obj_item_cautery),
            typeof(obj_item_circular_saw),
            typeof(obj_item_clothing_mask_surgical),
            typeof(obj_item_hemostat),
            typeof(obj_item_razor),
            typeof(obj_item_reagent_containers_medigel_sterilizine),
            typeof(obj_item_retractor),
            typeof(obj_item_scalpel),
            typeof(obj_item_shears),
            typeof(obj_item_stack_medical_bone_gel),
            typeof(obj_item_stack_medical_wrap_sticky_tape_surgical),
            typeof(obj_item_surgical_drapes),
            typeof(obj_item_surgicaldrill),
            typeof(obj_item_blood_scanner)
        ));
    }
}

// tg: code/datums/storage/subtypes/others/misc.dm:168
public partial class datum_storage_pillbottle : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:173
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(can_hold_list: Dm.list(
            typeof(obj_item_reagent_containers_applicator),
            typeof(obj_item_food_bait_natural)
        ));
    }
}

///Wallet storage
// tg: code/datums/storage/subtypes/others/misc.dm:197  (gaps R2: the wallets of the station traits)
public partial class datum_storage_wallet : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:201
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(
            can_hold_list: Dm.list(
                typeof(obj_item_stack_spacecash),
                typeof(obj_item_holochip),
                typeof(obj_item_card),
                typeof(obj_item_cigarette),
                typeof(obj_item_clothing_accessory_dogtag),
                typeof(obj_item_coin),
                typeof(obj_item_coupon),
                typeof(obj_item_dice),
                typeof(obj_item_disk),
                typeof(obj_item_flashlight_pen),
                typeof(obj_item_folder_biscuit),
                typeof(obj_item_food_chococoin),
                typeof(obj_item_implanter),
                typeof(obj_item_laser_pointer),
                typeof(obj_item_lighter),
                typeof(obj_item_lipstick),
                typeof(obj_item_match),
                typeof(obj_item_paper),
                typeof(obj_item_pen),
                typeof(obj_item_photo),
                typeof(obj_item_reagent_containers_dropper),
                typeof(obj_item_reagent_containers_syringe),
                typeof(obj_item_reagent_containers_applicator),
                typeof(obj_item_screwdriver),
                typeof(obj_item_seeds),
                typeof(obj_item_spess_knife),
                typeof(obj_item_stack_medical),
                typeof(obj_item_stamp),
                typeof(obj_item_toy_crayon),
                typeof(obj_item_poker_chip)),
            cant_hold_list: Dm.list(
                typeof(obj_item_screwdriver_power)));
    }
}

// tg: code/datums/storage/subtypes/others/misc.dm:238
public partial class datum_storage_crayons : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:241
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(
            can_hold_list: typeof(obj_item_toy_crayon),
            cant_hold_list: Dm.list(
                typeof(obj_item_toy_crayon_spraycan),
                typeof(obj_item_toy_crayon_mime),
                typeof(obj_item_toy_crayon_rainbow)));
    }
}

///Portable chem mixer
// tg: code/datums/storage/subtypes/others/misc.dm:67
public partial class datum_storage_portable_chem_mixer : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:71
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_reagent_containers_cup_beaker),
            typeof(obj_item_reagent_containers_cup_bottle),
            typeof(obj_item_reagent_containers_cup_tube),
            typeof(obj_item_reagent_containers_cup_glass_waterbottle),
            typeof(obj_item_reagent_containers_condiment)));
    }
}

///Implant
// tg: code/datums/storage/subtypes/others/misc.dm:83
public partial class datum_storage_implant : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:89
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(cant_hold_list: typeof(obj_item_disk_nuclear));
    }
}

// Syndicate G4a (D316): the briefcase gun's storage.
// tg: code/datums/storage/subtypes/others/misc.dm:147
public partial class datum_storage_briefcase_gun : datum_storage_briefcase
{
    // tg: code/datums/storage/subtypes/others/misc.dm:153
    public override object can_insert(obj_item to_insert = null, mob user = null, float? messages = null, float? force = null)
    {
        object dot = base.can_insert(to_insert, user, messages, force);
        if (!Dm.truthy(dot))
            return FALSE;

        if (to_insert is obj_item_gun gun)
        {
            if (gun.weapon_weight > max_weapon_weight)
            {
                if (Dm.truthy(messages) && user != null)
                    user.balloon_alert(user, I18n.T("too heavy!"));
                return FALSE;
            }
        }

        return TRUE;
    }
}

// tg: code/datums/storage/subtypes/others/misc.dm:93  (silicons SI7, D326: a drone's built-in tools)
public partial class datum_storage_drone : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:98
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_crowbar_drone),
            typeof(obj_item_screwdriver_drone),
            typeof(obj_item_wrench_drone),
            typeof(obj_item_weldingtool_drone),
            typeof(obj_item_wirecutters_drone),
            typeof(obj_item_multitool_drone),
            typeof(obj_item_pipe_dispenser_drone),
            typeof(obj_item_t_scanner_drone),
            typeof(obj_item_analyzer_drone),
            typeof(obj_item_soap_drone)));
    }

    // tg: code/datums/storage/subtypes/others/misc.dm:113
    public override async DmTask dump_content_at(atom dest_object = null, object dump_loc = null, mob user = null)
    {
        await DmTask.CompletedTask; //no dumping of contents allowed
    }
}

///Dice storage
// tg: code/datums/storage/subtypes/others/misc.dm:254  (gaps DE1, D333: the dice bags)
public partial class datum_storage_dice : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:257
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_dice));
    }
}

///Test tube rack
// tg: code/datums/storage/subtypes/others/misc.dm:4
public partial class datum_storage_test_tube_rack : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:11
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_reagent_containers_cup_tube));
    }
}

///Organ box
// tg: code/datums/storage/subtypes/others/misc.dm:44
public partial class datum_storage_organ_box : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:48
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_organ),
            typeof(obj_item_bodypart),
            typeof(obj_item_food_icecream)));
    }

    // tg: code/datums/storage/subtypes/others/misc.dm:56
    public override void handle_enter(datum source = null, obj_item arrived = null)
    {
        base.handle_enter(source, arrived);
        if (arrived is not null && source is obj_item_storage_organbox organbox && Dm.truthy(organbox.coolant_to_spend()))
            Waiting.NoSleep(arrived.freeze());
    }

    // tg: code/datums/storage/subtypes/others/misc.dm:61
    public override void handle_exit(datum source = null, obj_item gone = null)
    {
        base.handle_exit(source, gone);
        if (gone is not null)
            gone.unfreeze();
    }
}

///Six pack beer
// tg: code/datums/storage/subtypes/others/misc.dm:181
public partial class datum_storage_sixcan : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:186
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_reagent_containers_cup_soda_cans),
            typeof(obj_item_reagent_containers_cup_glass_bottle_beer),
            typeof(obj_item_reagent_containers_cup_glass_bottle_ale),
            typeof(obj_item_reagent_containers_cup_glass_waterbottle)));
    }
}

// tg: code/datums/storage/subtypes/others/misc.dm:277
public partial class datum_storage_card_binder : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:281
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_tcgcard));
    }
}

///Toilet bong
// tg: code/datums/storage/subtypes/others/misc.dm:286  (gaps ST5)
public partial class datum_storage_toiletbong : datum_storage
{
    // tg: code/datums/storage/subtypes/others/misc.dm:293
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_food));
    }
}

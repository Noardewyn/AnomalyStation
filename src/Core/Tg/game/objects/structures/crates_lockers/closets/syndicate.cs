// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/syndicate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316: the nuclear operatives' base). The var blocks are generated (D111).

// tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:17
public partial class obj_structure_closet_syndicate_personal : obj_structure_closet_syndicate
{
    // tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:20
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_trench_tool>(this);
        Dm.@new<obj_item_clothing_glasses_night>(this);
        Dm.@new<obj_item_ammo_box_magazine_m10mm>(this);
        Dm.@new<obj_item_storage_belt_military>(this);
        Dm.@new<obj_item_storage_belt_holster_nukie>(this);
        Dm.@new<obj_item_radio_headset_syndicate>(this);
        Dm.@new<obj_item_clothing_under_syndicate>(this);
        Dm.@new<obj_item_clothing_under_syndicate_skirt>(this);
        Dm.@new<obj_item_clothing_shoes_sneakers_black>(this);
        Dm.@new<obj_item_mod_module_plasma_stabilizer>(this);
        Dm.@new<obj_item_climbing_hook_syndicate>(this);
        Dm.@new<obj_item_mod_module_thermal_regulator>(this);
        Dm.@new<obj_item_mod_module_plasma_stabilizer>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:36
public partial class obj_structure_closet_syndicate_nuclear : obj_structure_closet_syndicate
{
    // tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:39
    public override async DmTask PopulateContents(params object[] __extra)
    {
        for (int i = 1; i <= 5; i++)
            Dm.@new<obj_item_ammo_box_magazine_m10mm>(this);
        Dm.@new<obj_item_storage_box_flashbangs>(this);
        Dm.@new<obj_item_storage_box_teargas>(this);
        Dm.@new<obj_item_storage_backpack_duffelbag_syndie_med>(this);
        Dm.@new<obj_item_modular_computer_pda_syndicate>(this);
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:47
public partial class obj_structure_closet_syndicate_chemical : obj_structure_closet_syndicate
{
    // tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:52
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_box_pillbottles>(this);
        Dm.@new<obj_item_storage_box_pillbottles>(this);
        Dm.@new<obj_item_storage_box_beakers_big>(this);
        Dm.@new<obj_item_storage_box_beakers_big>(this);
        Dm.@new<obj_item_storage_box_medigels>(this);
        Dm.@new<obj_item_storage_box_medigels>(this);
        Dm.@new<obj_item_ph_booklet>(this);
        Dm.@new<obj_item_reagent_containers_dropper>(this);
        Dm.@new<obj_item_reagent_containers_cup_bottle_acidic_buffer>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:64
public partial class obj_structure_closet_syndicate_resources : obj_structure_closet_syndicate
{
    // A lot of this stuff is objective items, and it's also only used for debugging, so init times don't matter here.
    // tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:68
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();
        float common_min = 30; //Minimum amount of minerals in the stack for common minerals
        float common_max = 50; //Maximum amount of HONK in the stack for HONK common minerals
        float rare_min = 5;  //Minimum HONK of HONK in the stack HONK HONK rare minerals
        float rare_max = 20; //Maximum HONK HONK HONK in the HONK for HONK rare HONK

        float pickednum = rand(1, 50);

        //Sad trombone
        if (pickednum == 1)
        {
            var paper = Dm.@new<obj_item_paper>(this);
            paper.name = "\\improper IOU";
            paper.add_raw_text(I18n.T("Sorry man, we needed the money so we sold your stash. It's ok, we'll double our money for sure this time!"));
            paper.update_appearance();
        }

        //Iron (common ore)
        if (pickednum >= 2)
            Dm.@new<obj_item_stack_sheet_iron>(this, rand(common_min, common_max));

        //Glass (common ore)
        if (pickednum >= 5)
            Dm.@new<obj_item_stack_sheet_glass>(this, rand(common_min, common_max));

        //Plasteel (common ore) Because it has a million more uses then plasma
        if (pickednum >= 10)
            Dm.@new<obj_item_stack_sheet_plasteel>(this, rand(common_min, common_max));

        //Plasma (rare ore)
        if (pickednum >= 15)
            Dm.@new<obj_item_stack_sheet_mineral_plasma>(this, rand(rare_min, rare_max));

        //Silver (rare ore)
        if (pickednum >= 20)
            Dm.@new<obj_item_stack_sheet_mineral_silver>(this, rand(rare_min, rare_max));

        //Gold (rare ore)
        if (pickednum >= 30)
            Dm.@new<obj_item_stack_sheet_mineral_gold>(this, rand(rare_min, rare_max));

        //Uranium (rare ore)
        if (pickednum >= 40)
            Dm.@new<obj_item_stack_sheet_mineral_uranium>(this, rand(rare_min, rare_max));

        //Titanium (rare ore)
        if (pickednum >= 40)
            Dm.@new<obj_item_stack_sheet_mineral_titanium>(this, rand(rare_min, rare_max));

        //Plastitanium (rare ore)
        if (pickednum >= 40)
            Dm.@new<obj_item_stack_sheet_mineral_plastitanium>(this, rand(rare_min, rare_max));

        //Diamond (rare HONK)
        if (pickednum >= 45)
            Dm.@new<obj_item_stack_sheet_mineral_diamond>(this, rand(rare_min, rare_max));

        //Jetpack (You hit the jackpot!)
        if (pickednum == 50)
            Dm.@new<obj_item_tank_jetpack_carbondioxide>(this);
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:129
public partial class obj_structure_closet_syndicate_resources_everything : obj_structure_closet_syndicate_resources
{
    // A lot of this stuff is objective items, and it's also only used for debugging, so init times don't matter here.
    // tg: code/game/objects/structures/crates_lockers/closets/syndicate.dm:134
    public override async DmTask populate_contents_immediate()
    {
        DmList resources = Dm.list(
            typeof(obj_item_stack_sheet_iron),
            typeof(obj_item_stack_sheet_glass),
            typeof(obj_item_stack_sheet_mineral_gold),
            typeof(obj_item_stack_sheet_mineral_silver),
            typeof(obj_item_stack_sheet_mineral_plasma),
            typeof(obj_item_stack_sheet_mineral_uranium),
            typeof(obj_item_stack_sheet_mineral_diamond),
            typeof(obj_item_stack_sheet_mineral_bananium),
            typeof(obj_item_stack_sheet_plasteel),
            typeof(obj_item_stack_sheet_mineral_titanium),
            typeof(obj_item_stack_sheet_mineral_plastitanium),
            typeof(obj_item_stack_rods),
            typeof(obj_item_stack_sheet_bluespace_crystal),
            typeof(obj_item_stack_sheet_mineral_abductor),
            typeof(obj_item_stack_sheet_plastic),
            typeof(obj_item_stack_sheet_mineral_wood));

        for (int i = 1; i <= 2; i++)
        {
            foreach (Type res in Dm.each<Type>(resources))
                Dm.@new(res, this, Dm.initial(res, (obj_item_stack R) => R.max_amount));
        }
        await DmTask.CompletedTask;
    }
}
